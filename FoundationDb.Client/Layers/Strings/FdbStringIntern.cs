#region BSD Licence
/* Copyright (c) 2013, Doxense SARL
All rights reserved.

Redistribution and use in source and binary forms, with or without
modification, are permitted provided that the following conditions are met:
	* Redistributions of source code must retain the above copyright
	  notice, this list of conditions and the following disclaimer.
	* Redistributions in binary form must reproduce the above copyright
	  notice, this list of conditions and the following disclaimer in the
	  documentation and/or other materials provided with the distribution.
	* Neither the name of the <organization> nor the
	  names of its contributors may be used to endorse or promote products
	  derived from this software without specific prior written permission.

THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND
ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
DISCLAIMED. IN NO EVENT SHALL <COPYRIGHT HOLDER> BE LIABLE FOR ANY
DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
(INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
(INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
 */
#endregion

#undef DEBUG_STRING_INTERNING

using FoundationDb.Client.Tuples;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace FoundationDb.Client.Tables
{

	public class FdbStringIntern
	{
		// Differences with the stringintern.py implementation at https://github.com/FoundationDB/python-layers/blob/master/lib/stringintern.py
		// * UID are byte[] but we store them as a base64 encoded string in the in-memory dictionary. They are still stored as byte[] in the tuple

		private static readonly string StringUidPrefix = "S";
		private static readonly string UidStringPrefix = "U";
		private const int CacheLimitBytes = 10 * 1000 * 1000;

		private readonly List<string> m_uidsInCache = new List<string>();
		private readonly Dictionary<string, string> m_uidStringCache = new Dictionary<string, string>();
		private readonly Dictionary<string, string> m_stringUidCache = new Dictionary<string, string>();
		private int m_bytesCached;

		private readonly Random m_rnd = new Random();
		private readonly RandomNumberGenerator m_prng = RandomNumberGenerator.Create();

		private readonly ReaderWriterLockSlim m_lock = new ReaderWriterLockSlim();

		public FdbStringIntern(FdbDatabase database, FdbSubspace subspace)
		{
			if (database == null) throw new ArgumentNullException("database");
			if (subspace == null) throw new ArgumentNullException("subspace");

			this.Database = database;
			this.Subspace = subspace;
		}

		public FdbDatabase Database { get; private set; }

		public FdbSubspace Subspace { get; private set; }

		public IFdbTuple UidKey(Slice uid)
		{
			return this.Subspace.Append(UidStringPrefix, uid);
		}

		public IFdbTuple StringKey(string value)
		{
			return this.Subspace.Append(StringUidPrefix, value);
		}

		private void EvictCache()
		{
			if (m_uidsInCache.Count == 0)
			{
				throw new InvalidOperationException("Cannot evict from empty cache");
			}

			// Random eviction
			int i = m_rnd.Next(m_uidsInCache.Count);

			// remove from uids_in_cache
			string uidKey = m_uidsInCache[i];
			m_uidsInCache[i] = m_uidsInCache[m_uidsInCache.Count - 1];
			m_uidsInCache.RemoveAt(m_uidsInCache.Count - 1);

			// remove from caches, account for bytes
			string value;
			if (!m_uidStringCache.TryGetValue(uidKey, out value) || value == null)
			{
				throw new InvalidOperationException("Error in cache evication: string not found");
			}

			m_uidStringCache.Remove(uidKey);
			m_stringUidCache.Remove(value);

			int size = (value.Length + uidKey.Length) * 2;
			Interlocked.Add(ref m_bytesCached, -size);
		}

		private void AddToCache(string value, Slice uid)
		{
			while (m_bytesCached > CacheLimitBytes)
			{
				EvictCache();
			}

			string uidKey = uid.ToBase64();

			m_lock.EnterUpgradeableReadLock();
			try
			{
				if (!m_uidStringCache.ContainsKey(uidKey))
				{

					m_lock.EnterWriteLock();
					try
					{
						m_stringUidCache[value] = uidKey;
						m_uidStringCache[uidKey] = value;
						m_uidsInCache.Add(uidKey);
					}
					finally
					{
						m_lock.ExitWriteLock();
					}

					int size = (value.Length + uidKey.Length) * 2;
					Interlocked.Add(ref m_bytesCached, size);

				}
			}
			finally
			{
				m_lock.ExitUpgradeableReadLock();
			}
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="trans"></param>
		/// <returns></returns>
		private async Task<Slice> FindUidAsync(FdbTransaction trans)
		{
			// note: we diverge from stringingern.py here by converting the UID (bytes) into Base64 in the cache.
			// this allows us to use StringComparer.Ordinal as a comparer for the Dictionary<K, V> and not EqualityComparer<byte[]>

			const int MAX_TRIES = 256;

			int tries = 0;
			while (tries < MAX_TRIES)
			{
				var bytes = new byte[4 + tries];
				m_prng.GetBytes(bytes);

				var slice = Slice.Create(bytes);
				if (m_uidStringCache.ContainsKey(slice.ToBase64()))
					continue;

				var candidate = await trans.GetAsync(UidKey(slice)).ConfigureAwait(false);
				if (!candidate.HasValue)
					return slice;

				++tries;
			}

			//TODO: another way ?
			throw new InvalidOperationException("Failed to find a free uid for interned string after " + MAX_TRIES + " attempts");
		}

		/// <summary>Look up string <paramref name="value"/> in the intern database and return its normalized representation. If value already exists, intern returns the existing representation.</summary>
		/// <param name="trans">Fdb transaction</param>
		/// <param name="value">String to intern</param>
		/// <returns>Normalized representation of the string</returns>
		/// <remarks><paramref name="value"/> must fit within a FoundationDB value</remarks>
		public Task<Slice> InternAsync(FdbTransaction trans, string value)
		{
#if DEBUG_STRING_INTERNING
			Debug.WriteLine("Want to intern: " + value);
#endif

			string uidKey;

			if (m_stringUidCache.TryGetValue(value, out uidKey))
			{
#if DEBUG_STRING_INTERNING
				Debug.WriteLine("> found in cache! " + uidKey);
#endif
				return Task.FromResult(Slice.FromBase64(uidKey));
			}

#if DEBUG_STRING_INTERNING
			Debug.WriteLine("_ not in cache, taking slow route...");
#endif

			return InternSlowAsync(trans, value);
		}

		private async Task<Slice> InternSlowAsync(FdbTransaction trans, string value)
		{
			var stringKey = StringKey(value);

			var uid = await trans.GetAsync(stringKey);
			if (uid == Slice.Nil)
			{
#if DEBUG_STRING_INTERNING
				Debug.WriteLine("_ not found in db, will create...");
#endif

				uid = await FindUidAsync(trans);
				if (uid == Slice.Nil) throw new InvalidOperationException("Failed to allocate a new uid while attempting to intern a string");
#if DEBUG_STRING_INTERNING
				Debug.WriteLine("> using new uid " + uid.ToBase64());
#endif

				trans.Set(UidKey(uid), Slice.FromString(value));
				trans.Set(stringKey, uid);

				AddToCache(value, uid);
			}
			else
			{
#if DEBUG_STRING_INTERNING
				Debug.WriteLine("> found in db with uid " + uid.ToBase64());
#endif
			}
			return uid;
		}

		/// <summary>Return the long string associated with the normalized representation <paramref name="uid"/></summary>
		/// <param name="trans"></param>
		/// <param name="uid"></param>
		/// <returns></returns>
		public Task<string> LookupAsync(FdbTransaction trans, Slice uid)
		{
			string value;
			if (m_uidStringCache.TryGetValue(uid.ToBase64(), out value))
			{
				return Task.FromResult(value);
			}

			return LookupSlowAsync(trans, uid);
		}

		private async Task<string> LookupSlowAsync(FdbTransaction trans, Slice uid)
		{
			var valueBytes = await trans.GetAsync(UidKey(uid));
			if (valueBytes == Slice.Nil) throw new KeyNotFoundException("String intern indentifier not found");

			string value = valueBytes.ToUnicode();
			AddToCache(value, uid);

			return value;
		}

	}

}
