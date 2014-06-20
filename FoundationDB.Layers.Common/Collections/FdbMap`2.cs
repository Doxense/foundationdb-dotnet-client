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

namespace FoundationDB.Layers.Collections
{
	using FoundationDB.Client;
	using FoundationDB.Linq;
	using JetBrains.Annotations;
	using System;
	using System.Collections.Generic;
	using System.Diagnostics;
	using System.Threading;
	using System.Threading.Tasks;

	[DebuggerDisplay("Name={Name}, Subspace={Subspace}")]
	public class FdbMap<TKey, TValue>
	{

		public FdbMap([NotNull] string name, [NotNull] FdbSubspace subspace, [NotNull] IValueEncoder<TValue> valueEncoder)
			: this(name, subspace, KeyValueEncoders.Tuples.Key<TKey>(), valueEncoder)
		{ }

		public FdbMap([NotNull] string name, [NotNull] FdbSubspace subspace, [NotNull] IKeyEncoder<TKey> keyEncoder, [NotNull] IValueEncoder<TValue> valueEncoder)
		{
			if (name == null) throw new ArgumentNullException("name");
			if (subspace == null) throw new ArgumentNullException("subspace");
			if (keyEncoder == null) throw new ArgumentNullException("keyEncoder");
			if (valueEncoder == null) throw new ArgumentNullException("valueEncoder");

			this.Name = name;
			this.Subspace = subspace;
			this.Location = new FdbEncoderSubspace<TKey>(subspace, keyEncoder);
			this.ValueEncoder = valueEncoder;
		}

		#region Public Properties...

		/// <summary>Name of the map</summary>
		// REVIEW: do we really need this property?
		public string Name { [NotNull] get; private set; }

		/// <summary>Subspace used as a prefix for all items in this map</summary>
		public FdbSubspace Subspace { [NotNull] get; private set; }

		/// <summary>Subspace used to encoded the keys for the items</summary>
		protected FdbEncoderSubspace<TKey> Location { [NotNull] get; private set; }

		/// <summary>Class that can serialize/deserialize values into/from slices</summary>
		public IValueEncoder<TValue> ValueEncoder { [NotNull] get; private set; }

		#endregion

		#region Get / Set / Clear...

		/// <summary>Returns the value of an existing entry in the map</summary>
		/// <param name="trans">Transaction used for the operation</param>
		/// <param name="id">Key of the entry to read from the map</param>
		/// <returns>Value of the entry if it exists; otherwise, throws an exception</returns>
		/// <exception cref="System.ArgumentNullException">If either <paramref name="trans"/> or <paramref name="id"/> is null.</exception>
		/// <exception cref="System.Collections.Generic.KeyNotFoundException">If the map does not contain an entry with this key.</exception>
		public async Task<TValue> GetAsync([NotNull] IFdbReadOnlyTransaction trans, TKey id)
		{
			if (trans == null) throw new ArgumentNullException("trans");
			if (id == null) throw new ArgumentNullException("id");

			var data = await this.Location.GetAsync(trans, id).ConfigureAwait(false);

			if (data.IsNull) throw new KeyNotFoundException("The given id was not present in the map.");
			return this.ValueEncoder.DecodeValue(data);
		}

		/// <summary>Returns the value of an entry in the map if it exists.</summary>
		/// <param name="trans">Transaction used for the operation</param>
		/// <param name="id">Key of the entry to read from the map</param>
		/// <returns>Optional with the value of the entry it it exists, or an empty result if it is not present in the map.</returns>
		public async Task<Optional<TValue>> TryGetAsync([NotNull] IFdbReadOnlyTransaction trans, TKey id)
		{
			if (trans == null) throw new ArgumentNullException("trans");
			if (id == null) throw new ArgumentNullException("id");

			var data = await this.Location.GetAsync(trans, id).ConfigureAwait(false);

			if (data.IsNull) return default(Optional<TValue>);
			return this.ValueEncoder.DecodeValue(data);
		}

		/// <summary>Add or update an entry in the map</summary>
		/// <param name="trans">Transaction used for the operation</param>
		/// <param name="id">Key of the entry to add or update</param>
		/// <param name="value">New value of the entry</param>
		/// <remarks>If the entry did not exist, it will be created. If not, its value will be replace with <paramref name="value"/>.</remarks>
		public void Set([NotNull] IFdbTransaction trans, TKey id, TValue value)
		{
			if (trans == null) throw new ArgumentNullException("trans");
			if (id == null) throw new ArgumentNullException("id");

			this.Location.Set(trans, id, this.ValueEncoder.EncodeValue(value));
		}

		/// <summary>Remove an entry from the map</summary>
		/// <param name="trans">Transaction used for the operation</param>
		/// <param name="id">Key of the entry to remove</param>
		/// <remarks>If the entry did not exist, the operation will not do anything.</remarks>
		public void Clear([NotNull] IFdbTransaction trans, TKey id)
		{
			if (trans == null) throw new ArgumentNullException("trans");
			if (id == null) throw new ArgumentNullException("id");

			this.Location.Clear(trans, id);
		}

		/// <summary>Reads all the entries in the map within a single transaction</summary>
		/// <param name="trans">Transaction used for the operation</param>
		/// <returns>Async sequence of pairs of keys and values, ordered by keys ascending.</returns>
		/// <remarks>This can be dangerous if the map contains a lot of entries! You should always use .Take() to limit the number of results returned.</remarks>
		[NotNull]
		public IFdbAsyncEnumerable<KeyValuePair<TKey, TValue>> All([NotNull] IFdbReadOnlyTransaction trans, FdbRangeOptions options = null)
		{
			if (trans == null) throw new ArgumentNullException("trans");

			return trans
				.GetRange(this.Location.ToRange(), options)
				.Select((kvp) => new KeyValuePair<TKey, TValue>(
					this.Location.DecodeKey(kvp.Key),
					this.ValueEncoder.DecodeValue(kvp.Value)
				));
		}

		/// <summary>Reads all the value in the map without decoding them</summary>
		/// <param name="trans">Transaction used for the operation</param>
		/// <returns>Async sequence of values as slices, ordered by keys ascending.</returns>
		/// <remarks>This can be dangerous if the map contains a lot of entries! You should always use .Take() to limit the number of results returned.</remarks>
		[NotNull]
		public IFdbAsyncEnumerable<Slice> AllValuesAsSlices([NotNull] IFdbReadOnlyTransaction trans, FdbRangeOptions options = null)
		{
			if (trans == null) throw new ArgumentNullException("trans");

			return trans
				.GetRange(this.Location.ToRange(), options)
				.Values();
		}

		/// <summary>Reads the values of multiple entries in the map</summary>
		/// <param name="trans">Transaction used for the operation</param>
		/// <param name="ids">List of the keys to read</param>
		/// <returns>Array of results, in the same order as specified in <paramref name="ids"/>.</returns>
		public async Task<Optional<TValue>[]> GetValuesAsync([NotNull] IFdbReadOnlyTransaction trans, [NotNull] IEnumerable<TKey> ids)
		{
			if (trans == null) throw new ArgumentNullException("trans");
			if (ids == null) throw new ArgumentNullException("ids");

			var results = await this.Location.GetValuesAsync(trans, ids).ConfigureAwait(false);

			return Optional.DecodeRange(this.ValueEncoder, results);
		}

		#endregion

		#region Bulk Operations...

		/// <summary>Exports the content of this map out of the database, by using as many transactions as necessary.</summary>
		/// <param name="db">Database used for the operation</param>
		/// <param name="handler">Handler called for each entry in the map. Calls to the handler are serialized, so it does not need to take locks. Any exception will abort the export and be thrown to the caller</param>
		/// <param name="cancellationToken">Token used to cancel the operation.</param>
		/// <returns>Task that completes once all the entries have been processed.</returns>
		/// <remarks>This method does not guarantee that the export will be a complete and coherent snapshot of the map. Any change made to the map while the export is running may be partially exported.</remarks>
		public Task ExportAsync([NotNull] IFdbDatabase db, [NotNull] Func<KeyValuePair<TKey, TValue>, CancellationToken, Task> handler, CancellationToken cancellationToken)
		{
			if (db == null) throw new ArgumentNullException("db");
			if (handler == null) throw new ArgumentNullException("handler");

			return Fdb.Bulk.ExportAsync(
				db, 
				this.Location.ToRange(),
				async (batch, _, ct) =>
				{
					foreach (var item in batch)
					{
						await handler(
							new KeyValuePair<TKey, TValue>(
								this.Location.DecodeKey(item.Key),
								this.ValueEncoder.DecodeValue(item.Value)
							),
							cancellationToken
						);
					}
				},
				cancellationToken
			);
		}

		/// <summary>Exports the content of this map out of the database, by using as many transactions as necessary.</summary>
		/// <param name="db">Database used for the operation</param>
		/// <param name="handler">Handler called for each batch of items in the map. Calls to the handler are serialized, so it does not need to take locks. Any exception will abort the export and be thrown to the caller</param>
		/// <param name="cancellationToken">Token used to cancel the operation.</param>
		/// <returns>Task that completes once all the entries have been processed.</returns>
		/// <remarks>This method does not guarantee that the export will be a complete and coherent snapshot of the map, except that all the items in a single batch are from the same snapshot. Any change made to the map while the export is running may be partially exported.</remarks>
		public Task ExportAsync([NotNull] IFdbDatabase db, [NotNull] Func<KeyValuePair<TKey, TValue>[], CancellationToken, Task> handler, CancellationToken cancellationToken)
		{
			if (db == null) throw new ArgumentNullException("db");
			if (handler == null) throw new ArgumentNullException("handler");

			var keyEncoder = (IKeyEncoder<TKey>) this.Location;
			var valueEncoder = this.ValueEncoder;

			return Fdb.Bulk.ExportAsync(
				db,
				this.Location.ToRange(),
				async (batch, _, ct) =>
				{
					var items = new KeyValuePair<TKey, TValue>[batch.Length];

					for (int i = 0; i < batch.Length; i++)
					{
						items[i] = new KeyValuePair<TKey, TValue>(
							keyEncoder.DecodeKey(batch[i].Key),
							valueEncoder.DecodeValue(batch[i].Value)
						);
					}

					await handler(items, cancellationToken);
				},
				cancellationToken
			);
		}

		#endregion
	}

}
