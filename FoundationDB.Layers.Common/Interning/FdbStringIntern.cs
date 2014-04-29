#region BSD Licence
/* Copyright (c) 2013-2014, Doxense SAS
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

namespace FoundationDB.Layers.Interning
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
	[Obsolete("FIXME! This version of the layer has a MAJOR bug!")]
	public class FdbStringIntern : IDisposable
	{
		//BUGBUGBUG: the current implementation has a bug with the cache, when a transaction fails to commit!
		//TODO: rewrite this to use typed subspaces !

		// Based on the stringintern.py implementation at https://github.com/FoundationDB/python-layers/blob/master/lib/stringintern.py

		private const int CacheLimitBytes = 10 * 1000 * 1000;

		// note: the python layer uses 'S' and 'U' as ascii string, so we need to use Slice and not chars to respect the tuple serialization
		private static readonly Slice String2UidKey = Slice.FromChar('S');
		private static readonly Slice Uid2StringKey = Slice.FromChar('U');

		private sealed class Uid : IEquatable<Uid>
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
				return !object.ReferenceEquals(other, null) && other.HashCode == this.HashCode && other.Slice.Equals(this.Slice);
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

		}

		public FdbSubspace Subspace { get; private set; }

		#region Private Helpers...

		protected virtual Slice UidKey(Slice uid)
		{
			return this.Subspace.Pack(Uid2StringKey, uid);
		}

		protected virtual Slice StringKey(string value)
		{
			return this.Subspace.Pack(String2UidKey, value);
		}

		/// <summary>Evict a random value from the cache</summary>
		private void EvictCache()
		{
			m_lock.EnterWriteLock();
			try
			{
				if (m_uidsInCache.Count == 0)
				{
					//note: there is probably a desync between the content of the cache, and the value of m_bytesCached !
					throw new InvalidOperationException("Cannot evict from empty cache");
				}

				// Random eviction
				// note: Random is not thread-safe, but we are in a write-lock so we are ok
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

				// tries to get an accurante idea of the in-memory size: chars are 2-byte in .NET, but the UID key is 1-byte
				int size = checked ((value.Length * 2) + uidKey.Slice.Count);
				Interlocked.Add(ref m_bytesCached, -size);
			}
			finally
			{
				m_lock.ExitWriteLock();
			}
		}

		/// <summary>Add a value in the cache</summary>
		private void AddToCache(string value, Slice uid)
		{
			while (Volatile.Read(ref m_bytesCached) > CacheLimitBytes)
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
			const int MAX_TRIES = 256;

			int tries = 0;
			while (tries < MAX_TRIES)
			{
				// note: we diverge from the python sample layer by not expanding the size at each retry, in order to ensure that value size keeps as small as possible
				Slice slice;
				lock (m_prng)
				{ // note: not all PRNG implementations are thread-safe !
					slice = Slice.Random(m_prng, 4 + (tries >> 1));
				}

				if (m_uidStringCache.ContainsKey(new Uid(slice)))
					continue;

				var candidate = await trans.GetAsync(UidKey(slice)).ConfigureAwait(false);
				if (candidate.IsNull)
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
		/// <remarks>The length of the string <paramref name="value"/> must not exceed the maximum FoundationDB value size</remarks>
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
				if (uid.IsNull) throw new InvalidOperationException("Failed to allocate a new uid while attempting to intern a string");
#if DEBUG_STRING_INTERNING
				Debug.WriteLine("> using new uid " + uid.ToBase64());
#endif

				trans.Set(UidKey(uid), Slice.FromString(value));
				trans.Set(stringKey, uid);

				//BUGBUG: if the transaction fails to commit, we will inserted a bad value in the cache!
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
		public Task<string> LookupAsync(IFdbReadOnlyTransaction trans, Slice uid)
		{
			if (trans == null) throw new ArgumentNullException("trans");
			if (uid.IsNull) throw new ArgumentException("String uid cannot be nil", "uid");

			if (uid.IsEmpty) return Task.FromResult(String.Empty);

			string value;
			if (m_uidStringCache.TryGetValue(new Uid(uid), out value))
			{
				return Task.FromResult(value);
			}

			return LookupSlowAsync(trans, uid);
		}

		private async Task<string> LookupSlowAsync(IFdbReadOnlyTransaction trans, Slice uid)
		{
			var valueBytes = await trans.GetAsync(UidKey(uid)).ConfigureAwait(false);
			if (valueBytes.IsNull) throw new KeyNotFoundException("String intern indentifier not found");

			string value = valueBytes.ToUnicode();

			//BUGBUG: if the uid has just been Interned in the current transaction, and if the transaction fails to commit (conflict, ...) we will insert a bad value in the cache!
			AddToCache(value, uid);

			return value;
		}

		#endregion

		public void Dispose()
		{
			Dispose(true);
			GC.SuppressFinalize(this);
		}

		protected virtual void Dispose(bool disposing)
		{
			if (disposing)
			{
				m_lock.Dispose();
			}
		}

	}

}
