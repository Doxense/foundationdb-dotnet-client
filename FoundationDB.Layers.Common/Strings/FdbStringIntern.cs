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
	* Neither the name of Doxense nor the
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

namespace FoundationDB.Layers.Tables
{
	using FoundationDB.Client;
	using FoundationDB.Layers.Tuples;
	using System;
	using System.Collections.Generic;
	using System.Diagnostics;
	using System.Security.Cryptography;
	using System.Threading;
	using System.Threading.Tasks;

	/// <summary>Provides a class for interning (aka normalizing, aliasing) commonly-used long strings into shorter representations.</summary>
	[DebuggerDisplay("Subspace={Subspace}")]
	public class FdbStringIntern
	{
		// Based on the stringintern.py implementation at https://github.com/FoundationDB/python-layers/blob/master/lib/stringintern.py

		private const int CacheLimitBytes = 10 * 1000 * 1000;

		// note: the python layer uses 'S' and 'U' as ascii string, so we need to use Slice and not chars to respect the tuple serialization

		private static readonly Slice String2UidKey = Slice.FromChar('S');
		private static readonly Slice Uid2StringKey = Slice.FromChar('U');

		private class Uid : IEquatable<Uid>
		{
			public readonly Slice Slice;
			public readonly int HashCode;

			public Uid(Slice slice)
			{
				this.Slice = slice;
				this.HashCode = slice.GetHashCode();
			}

			public bool Equals(Uid other)
			{
				return other.HashCode == this.HashCode && other.Slice.Equals(this.Slice);
			}

			public override bool Equals(object obj)
			{
				return obj is Uid && Equals(obj as Uid);
			}

			public override int GetHashCode()
			{
				return this.HashCode;
			}
		}

		private readonly List<Uid> m_uidsInCache = new List<Uid>();
		private readonly Dictionary<Uid, string> m_uidStringCache = new Dictionary<Uid, string>(EqualityComparer<Uid>.Default);
		private readonly Dictionary<string, Uid> m_stringUidCache = new Dictionary<string, Uid>(StringComparer.Ordinal);
		private int m_bytesCached;

		private readonly Random m_rnd = new Random();
		private readonly RandomNumberGenerator m_prng = RandomNumberGenerator.Create();

		private readonly ReaderWriterLockSlim m_lock = new ReaderWriterLockSlim();

		public FdbStringIntern(FdbSubspace subspace)
		{
			if (subspace == null) throw new ArgumentNullException("subspace");

			this.Subspace = subspace;

			this.StringUidPrefix = subspace.Create(String2UidKey).Memoize();
			this.UidStringPrefix = subspace.Create(Uid2StringKey).Memoize();
		}

		public FdbSubspace Subspace { get; private set; }

		protected FdbMemoizedTuple StringUidPrefix { get; private set; }

		protected FdbMemoizedTuple UidStringPrefix { get; private set; }

		#region Private Helpers...

		public IFdbTuple UidKey(Slice uid)
		{
			return this.UidStringPrefix.Append(uid);
		}

		public IFdbTuple StringKey(string value)
		{
			return this.StringUidPrefix.Append(value);
		}

		/// <summary>Evict a random value from the cache</summary>
		private void EvictCache()
		{
			if (m_uidsInCache.Count == 0)
			{
				throw new InvalidOperationException("Cannot evict from empty cache");
			}

			// Random eviction
			int i = m_rnd.Next(m_uidsInCache.Count);

			// remove from uids_in_cache
			var uidKey = m_uidsInCache[i];
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

			int size = (value.Length * 2) + uidKey.Slice.Count;
			Interlocked.Add(ref m_bytesCached, -size);
		}

		/// <summary>Add a value in the cache</summary>
		private void AddToCache(string value, Slice uid)
		{
			while (m_bytesCached > CacheLimitBytes)
			{
				EvictCache();
			}

			var uidKey = new Uid(uid);

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

					int size = (value.Length * 2) + uidKey.Slice.Count;
					Interlocked.Add(ref m_bytesCached, size);

				}
			}
			finally
			{
				m_lock.ExitUpgradeableReadLock();
			}
		}

		/// <summary>Finds a new free uid that can be used to store a new string in the table</summary>
		/// <param name="trans">Transaction used to look for and create a new uid</param>
		/// <returns>Newly created UID that is guaranteed to be globally unique</returns>
		private async Task<Slice> FindUidAsync(IFdbTransaction trans)
		{
			// note: we diverge from stringingern.py here by converting the UID (bytes) into Base64 in the cache.
			// this allows us to use StringComparer.Ordinal as a comparer for the Dictionary<K, V> and not EqualityComparer<byte[]>

			const int MAX_TRIES = 256;

			int tries = 0;
			while (tries < MAX_TRIES)
			{
				// note: we diverge from the python sample layer by not expanding the size at each retry, in order to ensure that value size keeps as small as possible
				var bytes = new byte[3 + (tries >> 1)];
				m_prng.GetBytes(bytes);

				var slice = Slice.Create(bytes);
				if (m_uidStringCache.ContainsKey(new Uid(slice)))
					continue;

				var candidate = await trans.GetAsync(UidKey(slice)).ConfigureAwait(false);
				if (!candidate.HasValue)
					return slice;

				++tries;
			}

			//TODO: another way ?
			throw new InvalidOperationException("Failed to find a free uid for interned string after " + MAX_TRIES + " attempts");
		}

		#endregion

		#region Intern...

		/// <summary>Look up string <paramref name="value"/> in the intern database and return its normalized representation. If value already exists, intern returns the existing representation.</summary>
		/// <param name="trans">Fdb transaction</param>
		/// <param name="value">String to intern</param>
		/// <returns>Normalized representation of the string</returns>
		/// <remarks><paramref name="value"/> must fit within a FoundationDB value</remarks>
		public Task<Slice> InternAsync(IFdbTransaction trans, string value)
		{
			if (trans == null) throw new ArgumentNullException("trans");
			if (value == null) throw new ArgumentNullException("value");

			if (value.Length == 0) return Task.FromResult(Slice.Empty);

#if DEBUG_STRING_INTERNING
			Debug.WriteLine("Want to intern: " + value);
#endif

			Uid uidKey;

			if (m_stringUidCache.TryGetValue(value, out uidKey))
			{
#if DEBUG_STRING_INTERNING
				Debug.WriteLine("> found in cache! " + uidKey);
#endif
				return Task.FromResult(uidKey.Slice);
			}

#if DEBUG_STRING_INTERNING
			Debug.WriteLine("_ not in cache, taking slow route...");
#endif

			return InternSlowAsync(trans, value);
		}

		private async Task<Slice> InternSlowAsync(IFdbTransaction trans, string value)
		{
			var stringKey = StringKey(value);

			var uid = await trans.GetAsync(stringKey).ConfigureAwait(false);
			if (uid == Slice.Nil)
			{
#if DEBUG_STRING_INTERNING
				Debug.WriteLine("_ not found in db, will create...");
#endif

				uid = await FindUidAsync(trans).ConfigureAwait(false);
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

		#endregion

		#region Lookup...

		/// <summary>Return the long string associated with the normalized representation <paramref name="uid"/></summary>
		public Task<string> LookupAsync(IFdbReadTransaction trans, Slice uid)
		{
			if (trans == null) throw new ArgumentNullException("trans");

			if (!uid.HasValue) throw new ArgumentException("String uid cannot be nil", "uid");

			if (uid.IsEmpty) return Task.FromResult(String.Empty);

			string value;
			if (m_uidStringCache.TryGetValue(new Uid(uid), out value))
			{
				return Task.FromResult(value);
			}

			return LookupSlowAsync(trans, uid);
		}

		private async Task<string> LookupSlowAsync(IFdbReadTransaction trans, Slice uid)
		{
			var valueBytes = await trans.GetAsync(UidKey(uid)).ConfigureAwait(false);
			if (valueBytes == Slice.Nil) throw new KeyNotFoundException("String intern indentifier not found");

			string value = valueBytes.ToUnicode();
			AddToCache(value, uid);

			return value;
		}

		#endregion

	}

}
