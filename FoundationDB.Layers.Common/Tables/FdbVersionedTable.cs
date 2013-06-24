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

namespace FoundationDB.Layers.Tables
{
	using FoundationDB.Client;
	using FoundationDB.Client.Utils;
	using FoundationDB.Layers.Tuples;
	using FoundationDB.Linq;
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using System.Threading;
	using System.Threading.Tasks;

	// THIS IS NOT AN OFFICIAL LAYER, JUST A PROTOTYPE TO TEST A FEW THINGS !

	/// <summary>Table that can store items and keep all past versions</summary>
	/// <typeparam name="TId">Type of the unique identifier of an item</typeparam>
	/// <typeparam name="TValue">Type of the value of an item</typeparam>
	public class FdbVersionedTable<TId, TValue>
	{
		private const int METADATA_KEY = 0;
		private const int ITEMS_KEY = 1;
		private const int LAST_VERSIONS_KEY = 2;

		/// <summary>Name of the table</summary>
		public string Name { get; private set; }

		/// <summary>Database used to perform transactions</summary>
		public FdbDatabase Database { get; private set; }

		/// <summary>Subspace used as a prefix for all items in this table</summary>
		public FdbSubspace Subspace { get; private set; }

		/// <summary>Class that can pack/unpack keys into/from tuples</summary>
		public ITupleKeyFormatter<TId> KeyReader { get; private set; }

		/// <summary>Class that can serialize/deserialize values into/from slices</summary>
		public ISliceSerializer<TValue> ValueSerializer { get; private set; }

		/// <summary>(Subspace, METADATA_KEY, attr_name) = attr_value</summary>
		protected FdbMemoizedTuple MetadataPrefix { get; private set; }

		/// <summary>(Subspace, ITEMS_KEY, key, version) = Contains the value of a specific version of the key, or empty if the last change was a deletion</summary>
		protected FdbMemoizedTuple ItemsPrefix { get; private set; }

		/// <summary>(Subspace, LATEST_VERSIONS_KEY, key) = Contains the last version for this specific key</summary>
		protected FdbMemoizedTuple VersionsPrefix { get; private set; }

		public FdbVersionedTable(string name, FdbDatabase database, FdbSubspace subspace, ITupleKeyFormatter<TId> keyReader, ISliceSerializer<TValue> valueSerializer)
		{
			if (string.IsNullOrEmpty(name)) throw new ArgumentNullException("name");
			if (database == null) throw new ArgumentNullException("database");
			if (subspace == null) throw new ArgumentNullException("subspace");
			if (keyReader == null) throw new ArgumentNullException("keyReader");
			if (valueSerializer == null) throw new ArgumentNullException("valueSerializer");

			this.Name = name;
			this.Database = database;
			this.Subspace = subspace;
			this.KeyReader = keyReader;
			this.ValueSerializer = valueSerializer;

			this.MetadataPrefix = this.Subspace.Create(METADATA_KEY).Memoize();
			this.ItemsPrefix = this.Subspace.Create(ITEMS_KEY).Memoize();
			this.VersionsPrefix = this.Subspace.Create(LAST_VERSIONS_KEY).Memoize();
		}

		public async Task<bool> OpenOrCreateAsync()
		{
			using (var tr = this.Database.BeginTransaction())
			{
				// (Subspace, ) + \00 = begin marker (= "")
				// (Susbspace, 0, Name) = Table Name
				// (Subspace, ) + \FF = end marker (= "")

				var key = await tr.GetAsync(this.MetadataPrefix).ConfigureAwait(false);
				// it should be (Subspace, 0)

				if (key == this.MetadataPrefix.ToSlice())
				{ // seems ok

					//TODO: check table name, check metadata ?
					return false;
				}

				// not found ? initialize!
				// get (Subspace, 00) and (Subspace, FF)
				var bounds = this.Subspace.Tuple.ToRange();
				tr.Set(bounds.Begin, Slice.Empty);
				tr.Set(bounds.End, Slice.Empty);
				tr.Set(this.MetadataPrefix.Append("Name"), Slice.FromString(this.Name));
				tr.Set(this.MetadataPrefix.Append("KeyType"), Slice.FromString(typeof(TId).FullName));
				tr.Set(this.MetadataPrefix.Append("ValueType"), Slice.FromString(typeof(TValue).FullName));

				await tr.CommitAsync();

				return true;
			}
		}

		/// <summary>Returns the key of the last valid version for this entry that is not after the specified version</summary>
		/// <remarks>Slice.Nil if not valid version was found, or the key of the matchin version</remarks>
		protected virtual async Task<long?> GetLastVersionAsync(FdbTransaction trans, TId id, bool snapshot, CancellationToken ct)
		{
			Contract.Requires(trans != null);
			Contract.Requires(id != null);

			// Lookup in the Version subspace
			var tuple = GetVersionKey(id);

			var value = await trans.GetAsync(tuple, snapshot, ct).ConfigureAwait(false);

			// if the item does not exist at all, returns null
			if (value.IsNullOrEmpty) return default(long?);

			// parse the version
			long version = value.ToInt64();

			return version;
		}

		/// <summary>Returns the active revision of an item that was valid at the specified version</summary>
		/// <remarks>null if no valid version was found, or the version number of the revision if found</remarks>
		protected virtual async Task<long?> FindVersionAsync(FdbTransaction trans, TId id, long version, bool snapshot, CancellationToken ct)
		{
			Contract.Requires(trans != null);
			Contract.Requires(id != null);
			Contract.Requires(version >= 0);


			// search selector to get the maximum version that is less or equal to the specified version
			var searchKey = FdbKeySelector.LastLessOrEqual(GetItemKey(id, version));

			// Finds the corresponding key...
			var dbKey = await trans.GetKeyAsync(searchKey, snapshot, ct).ConfigureAwait(false);

			// If the key does not exist at all, or if the specified version is "too early", we will get back a key from a previous entry in the db
			if (!dbKey.PrefixedBy(GetItemKeyPrefix(id).ToSlice()))
			{ // foreign key => "not found"
				return default(long?);
			}

			// parse the version from the key
			var dbVersion = ExtractVersionFromKeyBytes(dbKey);

			return dbVersion;
		}

		protected virtual long? ExtractVersionFromKeyBytes(Slice keyBytes)
		{
			if (keyBytes.IsNullOrEmpty) return default(long?);

			// we need to unpack the key, to get the db version
			var unpacked = keyBytes.ToTuple();

			//TODO: ensure that there is a version at the end !

			// get the last item of the tuple, that should be the version
			long dbVersion = unpacked.Get<long>(-1);

			return dbVersion;
		}

		/// <summary>Compute the key prefix for all versions of an item</summary>
		/// <param name="id">Item key</param>
		/// <returns>(Subspace, ITEMS_KEY, key, )</returns>
		protected virtual IFdbTuple GetItemKeyPrefix(TId id)
		{
			return this.ItemsPrefix.Concat(this.KeyReader.Pack(id));
		}

		protected virtual IFdbTuple GetItemKey(TId id, long version)
		{
			return GetItemKeyPrefix(id).Append(version);
		}

		/// <summary>Compute the key that holds the last known version number of an item</summary>
		/// <returns>(Subspace, LAST_VERSION_KEY, key, )</returns>
		protected virtual IFdbTuple GetVersionKey(TId id)
		{
			return this.VersionsPrefix.Concat(this.KeyReader.Pack(id));
		}

		protected virtual Task<Slice> GetValueAtVersionAsync(FdbTransaction trans, TId id, long version, bool snapshot, CancellationToken ct)
		{
			var tuple = GetItemKey(id, version);
			return trans.GetAsync(tuple, snapshot, ct);
		}

		#region GetLast() ...

		private async Task<KeyValuePair<long?, TValue>> GetLastCoreAsync(FdbTransaction trans, TId id, bool snapshot, CancellationToken ct)
		{
			ct.ThrowIfCancellationRequested();

			// Get the last known version for this key
			var last = await GetLastVersionAsync(trans, id, snapshot, ct).ConfigureAwait(false);

			var value = default(TValue);
			if (last.HasValue)
			{ // extract the specified value
				var data = await GetValueAtVersionAsync(trans, id, last.Value, snapshot, ct).ConfigureAwait(false);
				value = this.ValueSerializer.Deserialize(data, default(TValue));
			}

			return new KeyValuePair<long?, TValue>(last, value);
		}

		public Task<KeyValuePair<long?, TValue>> GetLastAsync(FdbTransaction trans, TId id, bool snapshot = false, CancellationToken ct = default(CancellationToken))
		{
			if (trans == null) throw new ArgumentNullException("trans");
			if (id == null) throw new ArgumentNullException("key");

			return GetLastCoreAsync(trans, id, snapshot, ct);
		}

		#endregion

		#region GetVersion()...

		private async Task<KeyValuePair<long?, TValue>> GetVersionCoreAsync(FdbTransaction trans, TId id, long version, bool snapshot, CancellationToken ct)
		{
			ct.ThrowIfCancellationRequested();

			long? dbVersion;

			if (version == long.MaxValue)
			{ // caller wants the latest
				dbVersion = await GetLastVersionAsync(trans, id, snapshot, ct).ConfigureAwait(false);
			}
			else
			{ // find the version alive at this time
				dbVersion = await FindVersionAsync(trans, id, version, snapshot, ct).ConfigureAwait(false);
			}

			if (dbVersion.HasValue)
			{ // we have found a valid record that contains this version, read the value
				// note that it could be a deletion

				var data = await GetValueAtVersionAsync(trans, id, dbVersion.Value, snapshot, ct).ConfigureAwait(false);
				// note: returns Slice.Empty if the value is deleted at this version
				if (!data.IsNullOrEmpty)
				{
					var value = this.ValueSerializer.Deserialize(data, default(TValue));
					return new KeyValuePair<long?, TValue>(dbVersion, value);
				}
			}

			// not found, or deleted
			return new KeyValuePair<long?, TValue>(dbVersion, default(TValue));
		}

		/// <summary>Return the value of an item at a specific version</summary>
		/// <param name="id">Key of the item to read</param>
		/// <param name="version">Time at which the item must exist</param>
		/// <returns>If there was a version at this time, returns a pair with the actual version number and the value. If not, return (null, Slice.Nil). If the value was deleted at this time, returns (version, Slice.Nil)</returns>
		public Task<KeyValuePair<long?, TValue>> GetVersionAsync(FdbTransaction trans, TId id, long version, bool snapshot = false, CancellationToken ct = default(CancellationToken))
		{
			// item did not exist yet => (null, Slice.Nil)
			// item exist at this time => (item.Version, item.Value)
			// item is deleted at this time => (item.Version, Slice.Empty)

			if (trans == null) throw new ArgumentNullException("trans");

			// note: if TKey is a struct, it cannot be null. So we allow 0, false, default(TStruct), ...
			if (id == null) throw new ArgumentNullException("key");

			if (version < 0) throw new ArgumentOutOfRangeException("version", "Version cannot be less than zero");

			return GetVersionCoreAsync(trans, id, version, snapshot, ct);
		}

		#endregion

		#region SetLast() ...

		public async Task<bool> Contains(FdbTransaction trans, TId id, long? version = null, bool snapshot = false, CancellationToken ct = default(CancellationToken))
		{
			ct.ThrowIfCancellationRequested();

			long? dbVersion;

			if (version.HasValue)
			{
				dbVersion = await FindVersionAsync(trans, id, version.Value, snapshot, ct).ConfigureAwait(false);
			}
			else
			{
				dbVersion = await GetLastVersionAsync(trans, id, snapshot, ct).ConfigureAwait(false);
			}

			if (!dbVersion.HasValue) return false;

			// We still need to check if the last version was a deletion

			var data = await GetValueAtVersionAsync(trans, id, dbVersion.Value, snapshot, ct).ConfigureAwait(false);

			return data.IsNullOrEmpty;
		}

		/// <summary>Attempts to update the last version of an item, if it exists</summary>
		/// <param name="id">Key of the item to update</param>
		/// <param name="value">New value for this item</param>
		/// <returns>If there are no known versions for this item, return null. Otherwise, update the item and return the version number</returns>
		public async Task<long?> TryUpdateLastAsync(FdbTransaction trans, TId id, TValue value, bool snapshot = false, CancellationToken ct = default(CancellationToken))
		{
			ct.ThrowIfCancellationRequested();

			// Find the last version
			var last = await GetLastVersionAsync(trans, id, snapshot, ct);

			if (last.HasValue)
			{
				Slice data = this.ValueSerializer.Serialize(value);
				trans.Set(GetItemKey(id, last.Value), data);
			}

			return last;
		}

		/// <summary>Attempts to update a previous version of an item, if it exists</summary>
		/// <param name="id">Key of the item to update</param>
		/// <param name="version">Version number (that must exist) of the item to change</param>
		/// <param name="updater">Func that will be passed the previous value, and return the new value</param>
		/// <returns>True if the previous version has been changed, false if it did not exist</returns>
		public async Task<bool> TryUpdateVersionAsync(FdbTransaction trans, TId id, long version, Func<TValue, TValue> updater, bool snapshot = false, CancellationToken ct = default(CancellationToken))
		{
			ct.ThrowIfCancellationRequested();

			var data = await GetValueAtVersionAsync(trans, id, version, snapshot, ct).ConfigureAwait(false);
			if (!data.HasValue)
			{ // this version does not exist !
				return false;
			}

			// parse previous value
			var value = this.ValueSerializer.Deserialize(data, default(TValue));

			// call the update lambda that will return a new value
			value = updater(value);

			// serialize the new value
			data = this.ValueSerializer.Serialize(value);

			// update
			trans.Set(GetItemKey(id, version), data);

			return true;
		}

		/// <summary>Attempts to create a new version of an item</summary>
		/// <param name="id">Key of the item to create</param>
		/// <param name="version">New version number of the item</param>
		/// <param name="value">New value of the item</param>
		/// <returns>Return the previous version number for this item, or null if it was the first version</returns>
		public async Task<long?> TryCreateNewVersionAsync(FdbTransaction trans, TId id, long version, TValue value, bool snapshot = false, CancellationToken ct = default(CancellationToken))
		{
			ct.ThrowIfCancellationRequested();

			// Ensure that the specified version number is indeed the last value
			var last = await GetLastVersionAsync(trans, id, snapshot, ct).ConfigureAwait(false);

			if (last.HasValue && last.Value >= version)
			{
				//REVIEW: should we throw FdbError.FutureVersion instead ?
				throw new InvalidOperationException("The table already contains a newer entry for this item");
			}

			// We can insert the new version

			Slice data = this.ValueSerializer.Serialize(value);

			//HACK to emulate Delete, remove me!
			if (!data.HasValue) data = Slice.Empty;
			//end of ugly hack

			// (subspace, ITEMS_KEY, key, version) = data
			trans.Set(GetItemKey(id, version), data);
			// (subspace, LAST_VERSIONS_KEY, key) = version
			trans.Set(GetVersionKey(id), Slice.FromInt64(version));

			return last;
		}

		#endregion

		#region List...

		public async Task<List<KeyValuePair<TId, TValue>>> SelectLatestAsync(FdbTransaction trans, bool includeDeleted = false, bool snapshot = false, CancellationToken ct = default(CancellationToken))
		{
			//REVIEW: pagination ? filtering ???

			ct.ThrowIfCancellationRequested();

			// get all latest versions...

			var versions = await trans
				.GetRangeStartsWith(this.ItemsPrefix, 0, snapshot)
				.Select(
					(key) => this.KeyReader.Unpack(FdbTuple.UnpackWithoutPrefix(key, this.VersionsPrefix.Packed)),
					(value) => value.ToInt64()
				)
				.ToListAsync(ct)
				.ConfigureAwait(false);

			// now read all the values

			var indexedResults = await trans
				.GetBatchIndexedAsync(
					versions.Select(kvp => GetItemKey(kvp.Key, kvp.Value)),
					snapshot,
					ct
				).ConfigureAwait(false);

			var results = indexedResults
				.Select((kvp) => new KeyValuePair<TId, TValue>(
					versions[kvp.Key].Key,
					this.ValueSerializer.Deserialize(kvp.Value, default(TValue))
				))
				.ToList();

			return results;
		}

		#endregion

	}

}
