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

namespace FoundationDb.Client.Tables
{
	using FoundationDb.Client.Tuples;
	using FoundationDb.Client.Utils;
	using System;
	using System.Collections.Generic;
	using System.Threading;
	using System.Threading.Tasks;

	public class FdbVersionedTable<TKey, TValue>
	{
		public const long VersionNotFound = -1;

		/// <summary>Database used to perform transactions</summary>
		public FdbDatabase Database { get; private set; }

		/// <summary>Subspace used as a prefix for all items in this table</summary>
		public FdbSubspace Subspace { get; private set; }

		/// <summary>Class that can pack/unpack keys into/from tuples</summary>
		public ITupleKeyReader<TKey> KeyReader { get; private set; }

		/// <summary>Class that can serialize/deserialize values into/from slices</summary>
		public ISliceSerializer<TValue> ValueSerializer { get; private set; }

		public FdbVersionedTable(FdbDatabase database, FdbSubspace subspace, ITupleKeyReader<TKey> keyReader, ISliceSerializer<TValue> valueSerializer, Func<TKey, long> initialVersion, Func<TKey, long, long> incrementVersion)
		{
			if (database == null) throw new ArgumentNullException("database");
			if (subspace == null) throw new ArgumentNullException("subspace");
			if (keyReader == null) throw new ArgumentNullException("keyReader");
			if (valueSerializer == null) throw new ArgumentNullException("valueSerializer");

			if (initialVersion == null) initialVersion = (_) => 0;
			if (incrementVersion == null) incrementVersion = (_, ver) => ver + 1;

			this.Database = database;
			this.Subspace = subspace;
			this.KeyReader = keyReader;
			this.ValueSerializer = valueSerializer;
		}

		/// <summary>Returns a tuple including the key and the version (namespace, key, version, )</summary>
		/// <typeparam name="TKey"></typeparam>
		/// <param name="key"></param>
		/// <returns></returns>
		protected virtual IFdbTuple MakeKey(TKey key, long version)
		{
			return this.Subspace.Append(key, version);
		}

		/// <summary>Returns a tuple with only the key (namespace, key, )</summary>
		/// <typeparam name="TKey"></typeparam>
		/// <param name="key"></param>
		/// <returns></returns>
		protected virtual IFdbTuple MakeKey(TKey key)
		{
			return this.Subspace.Append(key);
		}

		/// <summary>Returns the key of the last valid version for this entry that is not after the specified version</summary>
		/// <remarks>Slice.Nil if not valid version was found, or the key of the matchin version</remarks>
		protected virtual async Task<Slice> FindLastKnownVersionAsync(FdbTransaction trans, IFdbTuple key, long? version, bool snapshot, CancellationToken ct)
		{

			// prefix of all the version for this key: (subspace, key, )
			Slice prefixKey = key.ToSlice();

			// search selector to get the maximum version that is less or equal to the specified version
			// note: if version is null, we increment the prefix to get the last known version

			Slice searchKey = version.HasValue ? key.Append(version.Value).ToSlice() : FdbKey.Increment(prefixKey);

			// Get the last version that matches.
			var dbKey = await trans.GetKeyAsync(FdbKeySelector.LastLessOrEqual(searchKey), snapshot, ct);

			// Either we have found a version for this key, or there are no valid version. In the last case:
			// * if 'version' is specified, and the key does not exist at all, or if the version is "too early", we will get back a key from a previous entry in the db
			// * if 'version' is null, and the key does not exist at all, we will get back the key for a next entry in the db
			// In both cases, the returned key does not match the prefix, and will be treated as "not found"
			if (!dbKey.PrefixedBy(prefixKey))
			{ // "not found"
				return Slice.Nil;
			}

			// We have a valid version for this key
			return dbKey;
		}

		protected virtual Task<Slice> GetValueAtVersionAsync(FdbTransaction trans, Slice keyWithVersion, bool snapshot, CancellationToken ct)
		{
			Contract.Requires(trans != null);
			Contract.Requires(!keyWithVersion.IsNullOrEmpty);

			return trans.GetAsync(keyWithVersion, snapshot, ct);
		}

		protected virtual long ExtractVersionFromKeyBytes(Slice keyBytes)
		{
			if (keyBytes.IsNullOrEmpty) return VersionNotFound;

			// we need to unpack the key, to get the db version
			var unpacked = FdbTuple.Unpack(keyBytes);

			//TODO: ensure that there is a version at the end !

			// get the last item of the tuple, that should be the version
			long dbVersion = unpacked.Get<long>(-1);

			return dbVersion;
		}

		#region GetAsync() ...

		private async Task<Slice> GetLastCoreAsync(FdbTransaction trans, IFdbTuple keyWithoutVersion, bool snapshot, CancellationToken ct)
		{
			ct.ThrowIfCancellationRequested();

			var keyBytes = keyWithoutVersion.ToSlice();

			var last = await FindLastKnownVersionAsync(trans, keyWithoutVersion, null, snapshot, ct);

			if (last.IsNullOrEmpty)
			{ // "not found"
				return Slice.Nil;
			}

			return await GetValueAtVersionAsync(trans, last, snapshot, ct);
		}

		public Task<Slice> GetLastAsync(FdbTransaction trans, TKey key, bool snapshot = false, CancellationToken ct = default(CancellationToken))
		{
			if (trans == null) throw new ArgumentNullException("trans");
			if (key == null) throw new ArgumentNullException("key");

			return GetLastCoreAsync(trans, MakeKey(key), snapshot, ct);
		}

		private async Task<KeyValuePair<long, Slice>> GetVersionCoreAsync(FdbTransaction trans, IFdbTuple keyWithoutVersion, long version, bool snapshot, CancellationToken ct)
		{
			ct.ThrowIfCancellationRequested();

			var keyOnlyBytes = keyWithoutVersion.ToSlice();
			var versionnedKeyBytes = keyWithoutVersion.Append(version).ToSlice();

			var key = await trans.GetKeyAsync(FdbKeySelector.LastLessOrEqual(versionnedKeyBytes), snapshot, ct);
			if (!key.PrefixedBy(keyOnlyBytes))
			{ // "not found"
				return new KeyValuePair<long, Slice>(VersionNotFound, Slice.Nil);
			}

			// we need to unpack the key, to get the db version
			var unpacked = FdbTuple.Unpack(key);
			//TODO: ensure that there is a version at the end !
			long dbVersion = unpacked.Get<long>(-1);

			// we have found a valid record that contains this version, read the value
			var value = await trans.GetAsync(key, snapshot, ct);

			return new KeyValuePair<long, Slice>(dbVersion, value);
		}

		public Task<KeyValuePair<long, Slice>> GetVersionAsync(FdbTransaction trans, TKey key, long version, bool snapshot = false, CancellationToken ct = default(CancellationToken))
		{
			if (trans == null) throw new ArgumentNullException("trans");

			// note: if TKey is a struct, it cannot be null. So we allow 0, false, default(TStruct), ...
			if (key == null) throw new ArgumentNullException("key");

			if (version < 0) throw new ArgumentOutOfRangeException("version", "Version cannot be less than zero");

			return GetVersionCoreAsync(trans, MakeKey(key), version, snapshot, ct);
		}

		#endregion

		#region Set() ...

		public async Task<long> SetLastAsync(FdbTransaction trans, TKey key, TValue value, bool snapshot = false, CancellationToken ct = default(CancellationToken))
		{
			var prefix = MakeKey(key);

			// Find the last version
			var last = await FindLastKnownVersionAsync(trans, prefix, null, snapshot, ct);

			long version;
			if (last.IsNullOrEmpty)
			{ // first version for this key !

				version = 0; //TODO: get "initial version" ?
				last = prefix.Append(version).ToSlice();
			}
			else
			{
				version = ExtractVersionFromKeyBytes(last);
			}

			trans.Set(last, this.ValueSerializer.Serialize(value));

			return version;
		}

		#endregion
	}

}
