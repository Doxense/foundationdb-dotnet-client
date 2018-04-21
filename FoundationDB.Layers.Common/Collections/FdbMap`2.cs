#region BSD Licence
/* Copyright (c) 2013-2018, Doxense SAS
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
	using System;
	using System.Collections.Generic;
	using System.Diagnostics;
	using System.Threading;
	using System.Threading.Tasks;
	using Doxense.Diagnostics.Contracts;
	using FoundationDB.Async;
	using FoundationDB.Client;
	using FoundationDB.Linq;
	using JetBrains.Annotations;

	[DebuggerDisplay("Name={Name}, Subspace={Subspace}")]
	public class FdbMap<TKey, TValue>
	{

		public FdbMap([NotNull] string name, [NotNull] IKeySubspace subspace, [NotNull] IValueEncoder<TValue> valueEncoder)
			: this(name, subspace, KeyValueEncoders.Tuples.Key<TKey>(), valueEncoder)
		{ }

		public FdbMap([NotNull] string name, [NotNull] IKeySubspace subspace, [NotNull] IKeyEncoder<TKey> keyEncoder, [NotNull] IValueEncoder<TValue> valueEncoder)
		{
			if (name == null) throw new ArgumentNullException(nameof(name));
			if (subspace == null) throw new ArgumentNullException(nameof(subspace));
			if (keyEncoder == null) throw new ArgumentNullException(nameof(keyEncoder));
			if (valueEncoder == null) throw new ArgumentNullException(nameof(valueEncoder));

			this.Name = name;
			this.Subspace = subspace;
			this.Location = subspace.UsingEncoder(keyEncoder);
			this.ValueEncoder = valueEncoder;
		}

		#region Public Properties...

		/// <summary>Name of the map</summary>
		// REVIEW: do we really need this property?
		public string Name { [NotNull] get; private set; }

		/// <summary>Subspace used as a prefix for all items in this map</summary>
		public IKeySubspace Subspace { [NotNull] get; private set; }

		/// <summary>Subspace used to encoded the keys for the items</summary>
		protected ITypedKeySubspace<TKey> Location { [NotNull] get; private set; }

		/// <summary>Class that can serialize/deserialize values into/from slices</summary>
		public IValueEncoder<TValue> ValueEncoder { [NotNull] get; private set; }

		#endregion

		#region Get / Set / Remove...

		/// <summary>Returns the value of an existing entry in the map</summary>
		/// <param name="trans">Transaction used for the operation</param>
		/// <param name="id">Key of the entry to read from the map</param>
		/// <returns>Value of the entry if it exists; otherwise, throws an exception</returns>
		/// <exception cref="System.ArgumentNullException">If either <paramref name="trans"/> or <paramref name="id"/> is null.</exception>
		/// <exception cref="System.Collections.Generic.KeyNotFoundException">If the map does not contain an entry with this key.</exception>
		public async Task<TValue> GetAsync([NotNull] IFdbReadOnlyTransaction trans, TKey id)
		{
			if (trans == null) throw new ArgumentNullException(nameof(trans));
			if (id == null) throw new ArgumentNullException(nameof(id));

			var data = await trans.GetAsync(this.Location.Keys.Encode(id)).ConfigureAwait(false);

			if (data.IsNull) throw new KeyNotFoundException("The given id was not present in the map.");
			return this.ValueEncoder.DecodeValue(data);
		}

		/// <summary>Returns the value of an entry in the map if it exists.</summary>
		/// <param name="trans">Transaction used for the operation</param>
		/// <param name="id">Key of the entry to read from the map</param>
		/// <returns>Optional with the value of the entry it it exists, or an empty result if it is not present in the map.</returns>
		public async Task<Optional<TValue>> TryGetAsync([NotNull] IFdbReadOnlyTransaction trans, TKey id)
		{
			if (trans == null) throw new ArgumentNullException(nameof(trans));
			if (id == null) throw new ArgumentNullException(nameof(id));

			var data = await trans.GetAsync(this.Location.Keys.Encode(id)).ConfigureAwait(false);

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
			if (trans == null) throw new ArgumentNullException(nameof(trans));
			if (id == null) throw new ArgumentNullException(nameof(id));

			trans.Set(this.Location.Keys.Encode(id), this.ValueEncoder.EncodeValue(value));
		}

		/// <summary>Remove a single entry from the map</summary>
		/// <param name="trans">Transaction used for the operation</param>
		/// <param name="id">Key of the entry to remove</param>
		/// <remarks>If the entry did not exist, the operation will not do anything.</remarks>
		public void Remove([NotNull] IFdbTransaction trans, TKey id)
		{
			if (trans == null) throw new ArgumentNullException(nameof(trans));
			if (id == null) throw new ArgumentNullException(nameof(id));

			trans.Clear(this.Location.Keys.Encode(id));
		}

		/// <summary>Create a query that will attempt to read all the entries in the map within a single transaction.</summary>
		/// <param name="trans">Transaction used for the operation</param>
		/// <returns>Async sequence of pairs of keys and values, ordered by keys ascending.</returns>
		/// <remarks>CAUTION: This can be dangerous if the map contains a lot of entries! You should always use .Take() to limit the number of results returned.</remarks>
		[NotNull]
		public IFdbAsyncEnumerable<KeyValuePair<TKey, TValue>> All([NotNull] IFdbReadOnlyTransaction trans, FdbRangeOptions options = null)
		{
			if (trans == null) throw new ArgumentNullException(nameof(trans));

			return trans
				.GetRange(this.Location.ToRange(), options)
				.Select(this.DecodeItem);
		}

		/// <summary>Reads the values of multiple entries in the map</summary>
		/// <param name="trans">Transaction used for the operation</param>
		/// <param name="ids">List of the keys to read</param>
		/// <returns>Array of results, in the same order as specified in <paramref name="ids"/>.</returns>
		public async Task<Optional<TValue>[]> GetValuesAsync([NotNull] IFdbReadOnlyTransaction trans, [NotNull] IEnumerable<TKey> ids)
		{
			if (trans == null) throw new ArgumentNullException(nameof(trans));
			if (ids == null) throw new ArgumentNullException(nameof(ids));

			var results = await trans.GetValuesAsync(this.Location.Keys.Encode(ids)).ConfigureAwait(false);

			return Optional.DecodeRange(this.ValueEncoder, results);
		}

		#endregion

		#region Bulk Operations...

		private KeyValuePair<TKey, TValue> DecodeItem(KeyValuePair<Slice, Slice> item)
		{
			return new KeyValuePair<TKey, TValue>(
				this.Location.Keys.Decode(item.Key),
				this.ValueEncoder.DecodeValue(item.Value)
			);
		}

		[NotNull]
		private KeyValuePair<TKey, TValue>[] DecodeItems(KeyValuePair<Slice, Slice>[] batch)
		{
			Contract.Requires(batch != null);

			var keyEncoder = this.Location.Keys;
			var valueEncoder = this.ValueEncoder;

			var items = new KeyValuePair<TKey, TValue>[batch.Length];
			for (int i = 0; i < batch.Length; i++)
			{
				items[i] = new KeyValuePair<TKey, TValue>(
					keyEncoder.Decode(batch[i].Key),
					valueEncoder.DecodeValue(batch[i].Value)
				);
			}
			return items;
		}

		/// <summary>Clear all the entries in the map</summary>
		/// <param name="trans">Transaction used for the operation</param>
		/// <remarks>This will delete EVERYTHING in the map!</remarks>
		public void Clear([NotNull] IFdbTransaction trans)
		{
			if (trans == null) throw new ArgumentNullException(nameof(trans));

			trans.ClearRange(this.Location.ToRange());
		}

		#region Export...

		/// <summary>Exports the content of this map out of the database, by using as many transactions as necessary.</summary>
		/// <param name="db">Database used for the operation</param>
		/// <param name="handler">Handler called for each entry in the map. Calls to the handler are serialized, so it does not need to take locks. Any exception will abort the export and be thrown to the caller</param>
		/// <param name="ct">Token used to cancel the operation.</param>
		/// <returns>Task that completes once all the entries have been processed.</returns>
		/// <remarks>This method does not guarantee that the export will be a complete and coherent snapshot of the map. Any change made to the map while the export is running may be partially exported.</remarks>
		public Task ExportAsync([NotNull] IFdbDatabase db, [NotNull] Action<KeyValuePair<TKey, TValue>> handler, CancellationToken ct)
		{
			if (db == null) throw new ArgumentNullException(nameof(db));
			if (handler == null) throw new ArgumentNullException(nameof(handler));

			return Fdb.Bulk.ExportAsync(
				db,
				this.Location.ToRange(),
				(batch, _, __) =>
				{
					foreach (var item in batch)
					{
						handler(DecodeItem(item));
					}
					return TaskHelpers.CompletedTask;
				},
				ct
			);
		}

		/// <summary>Exports the content of this map out of the database, by using as many transactions as necessary.</summary>
		/// <param name="db">Database used for the operation</param>
		/// <param name="handler">Handler called for each entry in the map. Calls to the handler are serialized, so it does not need to take locks. Any exception will abort the export and be thrown to the caller</param>
		/// <param name="ct">Token used to cancel the operation.</param>
		/// <returns>Task that completes once all the entries have been processed.</returns>
		/// <remarks>This method does not guarantee that the export will be a complete and coherent snapshot of the map. Any change made to the map while the export is running may be partially exported.</remarks>
		public Task ExportAsync([NotNull] IFdbDatabase db, [NotNull] Func<KeyValuePair<TKey, TValue>, CancellationToken, Task> handler, CancellationToken ct)
		{
			if (db == null) throw new ArgumentNullException(nameof(db));
			if (handler == null) throw new ArgumentNullException(nameof(handler));

			return Fdb.Bulk.ExportAsync(
				db,
				this.Location.ToRange(),
				async (batch, _, __) =>
				{
					foreach (var item in batch)
					{
						await handler(DecodeItem(item), ct);
					}
				},
				ct
			);
		}

		/// <summary>Exports the content of this map out of the database, by using as many transactions as necessary.</summary>
		/// <param name="db">Database used for the operation</param>
		/// <param name="handler">Handler called for each batch of items in the map. Calls to the handler are serialized, so it does not need to take locks. Any exception will abort the export and be thrown to the caller</param>
		/// <param name="ct">Token used to cancel the operation.</param>
		/// <returns>Task that completes once all the entries have been processed.</returns>
		/// <remarks>This method does not guarantee that the export will be a complete and coherent snapshot of the map, except that all the items in a single batch are from the same snapshot. Any change made to the map while the export is running may be partially exported.</remarks>
		public Task ExportAsync([NotNull] IFdbDatabase db, [NotNull] Action<KeyValuePair<TKey, TValue>[]> handler, CancellationToken ct)
		{
			if (db == null) throw new ArgumentNullException(nameof(db));
			if (handler == null) throw new ArgumentNullException(nameof(handler));

			return Fdb.Bulk.ExportAsync(
				db,
				this.Location.ToRange(),
				(batch, _, __) =>
				{
					if (batch.Length > 0)
					{
						handler(DecodeItems(batch));
					}
					return TaskHelpers.CompletedTask;
				},
				ct
			);
		}

		/// <summary>Exports the content of this map out of the database, by using as many transactions as necessary.</summary>
		/// <param name="db">Database used for the operation</param>
		/// <param name="handler">Handler called for each batch of items in the map. Calls to the handler are serialized, so it does not need to take locks. Any exception will abort the export and be thrown to the caller</param>
		/// <param name="ct">Token used to cancel the operation.</param>
		/// <returns>Task that completes once all the entries have been processed.</returns>
		/// <remarks>This method does not guarantee that the export will be a complete and coherent snapshot of the map, except that all the items in a single batch are from the same snapshot. Any change made to the map while the export is running may be partially exported.</remarks>
		public Task ExportAsync([NotNull] IFdbDatabase db, [NotNull] Func<KeyValuePair<TKey, TValue>[], CancellationToken, Task> handler, CancellationToken ct)
		{
			if (db == null) throw new ArgumentNullException(nameof(db));
			if (handler == null) throw new ArgumentNullException(nameof(handler));

			return Fdb.Bulk.ExportAsync(
				db,
				this.Location.ToRange(),
				(batch, _, tok) => handler(DecodeItems(batch), tok),
				ct
			);
		}

		/// <summary>Exports the content of this map out of the database, by using as many transactions as necessary.</summary>
		/// <param name="db">Database used for the operation</param>
		/// <param name="init">Handler that is called once before the first batch, to produce the initial state.</param>
		/// <param name="handler">Handler called for each batch of items in the map. It is given the previous state, and should return the updated state. Calls to the handler are serialized, so it does not need to take locks. Any exception will abort the export and be thrown to the caller</param>
		/// <param name="ct">Token used to cancel the operation.</param>
		/// <returns>Task that completes once all the entries have been processed and return the result of the last call to <paramref name="handler"/> if there was at least one batch, or the result of <paramref name="init"/> if the map was empty.</returns>
		/// <remarks>This method does not guarantee that the export will be a complete and coherent snapshot of the map, except that all the items in a single batch are from the same snapshot. Any change made to the map while the export is running may be partially exported.</remarks>
		public async Task<TResult> AggregateAsync<TResult>([NotNull] IFdbDatabase db, Func<TResult> init, [NotNull] Func<TResult, KeyValuePair<TKey, TValue>[], TResult> handler, CancellationToken ct)
		{
			if (db == null) throw new ArgumentNullException(nameof(db));
			if (handler == null) throw new ArgumentNullException(nameof(handler));

			var state = default(TResult);
			if (init != null)
			{
				state = init();
			}

			await Fdb.Bulk.ExportAsync(
				db,
				this.Location.ToRange(),
				(batch, _, __) =>
				{
					state = handler(state, DecodeItems(batch));
					return TaskHelpers.CompletedTask;
				},
				ct
			);

			return state;
		}

		/// <summary>Exports the content of this map out of the database, by using as many transactions as necessary.</summary>
		/// <param name="db">Database used for the operation</param>
		/// <param name="init">Handler that is called once before the first batch, to produce the initial state.</param>
		/// <param name="handler">Handler called for each batch of items in the map. It is given the previous state, and should return the updated state. Calls to the handler are serialized, so it does not need to take locks. Any exception will abort the export and be thrown to the caller</param>
		/// <param name="finish">Handler that is called one after the last batch, to produce the final result out of the last state.</param>
		/// <param name="ct">Token used to cancel the operation.</param>
		/// <returns>Task that completes once all the entries have been processed and return the result of calling <paramref name="finish"/> with the state return by the last call to <paramref name="handler"/> if there was at least one batch, or the result of <paramref name="init"/> if the map was empty.</returns>
		/// <remarks>This method does not guarantee that the export will be a complete and coherent snapshot of the map, except that all the items in a single batch are from the same snapshot. Any change made to the map while the export is running may be partially exported.</remarks>
		public async Task<TResult> AggregateAsync<TState, TResult>([NotNull] IFdbDatabase db, Func<TState> init, [NotNull] Func<TState, KeyValuePair<TKey, TValue>[], TState> handler, Func<TState, TResult> finish, CancellationToken ct)
		{
			if (db == null) throw new ArgumentNullException(nameof(db));
			if (handler == null) throw new ArgumentNullException(nameof(handler));

			var state = default(TState);
			if (init != null)
			{
				state = init();
			}

			await Fdb.Bulk.ExportAsync(
				db,
				this.Location.ToRange(),
				(batch, _, __) =>
				{
					state = handler(state, DecodeItems(batch));
					return TaskHelpers.CompletedTask;
				},
				ct
			);

			ct.ThrowIfCancellationRequested();

			var result = default(TResult);
			if (finish != null)
			{
				result = finish(state);
			}

			return result;
		}

		#endregion

		#region Import...

		/// <summary>Imports a potentially large sequence of items into the map.</summary>
		/// <param name="db">Database used for the operation</param>
		/// <param name="items">Sequence of items to import. If the item already exists in the map, its value will be overwritten.</param>
		/// <param name="ct">Token used to cancel the operation</param>
		/// <remarks>
		/// <p>Any previously existing items in the map will remain. If you want to get from the previous content, you need to clear the map before hand.</p>
		/// <p>Other transactions may see a partial view of the map while the sequence is being imported. If this is a problem, you may need to import the map into a temporary subspace, and then 'publish' the final result using an indirection layer (like the Directory Layer)</p>
		/// <p>If the import operation fails midway, all items that have already been successfully imported will be kept in the database.</p>
		/// </remarks>
		public Task ImportAsync([NotNull] IFdbDatabase db, [NotNull] IEnumerable<KeyValuePair<TKey, TValue>> items, CancellationToken ct)
		{
			if (db == null) throw new ArgumentNullException(nameof(db));
			if (items == null) throw new ArgumentNullException(nameof(items));

			return Fdb.Bulk.InsertAsync(
				db,
				items,
				(item, tr) => this.Set(tr, item.Key, item.Value),
				ct		
			);
		}

		/// <summary>Imports a potentially large sequence of items into the map, using a specific key selector.</summary>
		/// <param name="db">Database used for the operation</param>
		/// <param name="items">Sequence of elements to import. If an item with the same key already exists in the map, its value will be overwritten.</param>
		/// <param name="keySelector">Lambda that will extract the key of an element</param>
		/// <param name="ct">Token used to cancel the operation</param>
		/// <remarks>
		/// <p>Any previously existing items in the map will remain. If you want to get from the previous content, you need to clear the map before hand.</p>
		/// <p>Other transactions may see a partial view of the map while the sequence is being imported. If this is a problem, you may need to import the map into a temporary subspace, and then 'publish' the final result using an indirection layer (like the Directory Layer)</p>
		/// <p>If the import operation fails midway, all items that have already been successfully imported will be kept in the database.</p>
		/// </remarks>
		public Task ImportAsync([NotNull] IFdbDatabase db, [NotNull] IEnumerable<TValue> items, [NotNull] Func<TValue, TKey> keySelector, CancellationToken ct)
		{
			if (db == null) throw new ArgumentNullException(nameof(db));
			if (items == null) throw new ArgumentNullException(nameof(items));
			if (keySelector == null) throw new ArgumentException("keySelector");

			return Fdb.Bulk.InsertAsync(
				db,
				items,
				(item, tr) => this.Set(tr, keySelector(item), item),
				ct
			);
		}

		/// <summary>Imports a potentially large sequence of elements into the map, using specific key and value selectors.</summary>
		/// <param name="db">Database used for the operation</param>
		/// <param name="items">Sequence of elements to import. If an item with the same key already exists in the map, its value will be overwritten.</param>
		/// <param name="keySelector">Lambda that will return the key of an element</param>
		/// <param name="valueSelector">Lambda that will return the value of an element</param>
		/// <param name="ct">Token used to cancel the operation</param>
		/// <remarks>
		/// <p>Any previously existing items in the map will remain. If you want to get from the previous content, you need to clear the map before hand.</p>
		/// <p>Other transactions may see a partial view of the map while the sequence is being imported. If this is a problem, you may need to import the map into a temporary subspace, and then 'publish' the final result using an indirection layer (like the Directory Layer)</p>
		/// <p>If the import operation fails midway, all items that have already been successfully imported will be kept in the database.</p>
		/// </remarks>
		public Task ImportAsync<TElement>([NotNull] IFdbDatabase db, [NotNull] IEnumerable<TElement> items, [NotNull] Func<TElement, TKey> keySelector, [NotNull] Func<TElement, TValue> valueSelector, CancellationToken ct)
		{
			if (db == null) throw new ArgumentNullException(nameof(db));
			if (items == null) throw new ArgumentNullException(nameof(items));
			if (keySelector == null) throw new ArgumentException("keySelector");
			if (valueSelector == null) throw new ArgumentException("valueSelector");

			return Fdb.Bulk.InsertAsync(
				db,
				items,
				(item, tr) => this.Set(tr, keySelector(item), valueSelector(item)),
				ct
			);
		}

		#endregion

		#endregion
	}

}
