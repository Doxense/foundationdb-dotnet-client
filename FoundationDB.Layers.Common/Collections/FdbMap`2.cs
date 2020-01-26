#region BSD License
/* Copyright (c) 2013-2020, Doxense SAS
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
	using System.Linq;
	using System.Threading;
	using System.Threading.Tasks;
	using Doxense.Diagnostics.Contracts;
	using Doxense.Serialization.Encoders;
	using FoundationDB.Client;

	[DebuggerDisplay("Location={Location}")]
	public class FdbMap<TKey, TValue>
	{

		public FdbMap(ISubspaceLocation location, IValueEncoder<TValue> valueEncoder)
			: this(location.AsTyped<TKey>(), valueEncoder)
		{ }

		public FdbMap(TypedKeySubspaceLocation<TKey> location, IValueEncoder<TValue> valueEncoder)
		{
			this.Location = location ?? throw new ArgumentNullException(nameof(location));
			this.ValueEncoder = valueEncoder ?? throw new ArgumentNullException(nameof(valueEncoder));
		}

		#region Public Properties...

		/// <summary>Subspace used to encoded the keys for the items</summary>
		public TypedKeySubspaceLocation<TKey> Location { get; }

		/// <summary>Class that can serialize/deserialize values into/from slices</summary>
		public IValueEncoder<TValue> ValueEncoder { get; }

		#endregion

		public sealed class State
		{

			public ITypedKeySubspace<TKey> Subspace { get; }

			public IValueEncoder<TValue> ValueEncoder { get; }

			internal State(ITypedKeySubspace<TKey> subspace, IValueEncoder<TValue> encoder)
			{
				Contract.Requires(subspace != null && encoder != null);
				this.Subspace = subspace;
				this.ValueEncoder = encoder;
			}

			#region Get / Set / Remove...

			/// <summary>Returns the value of an existing entry in the map</summary>
			/// <param name="trans">Transaction used for the operation</param>
			/// <param name="id">Key of the entry to read from the map</param>
			/// <returns>Value of the entry if it exists; otherwise, throws an exception</returns>
			/// <exception cref="System.ArgumentNullException">If either <paramref name="trans"/> or <paramref name="id"/> is null.</exception>
			/// <exception cref="System.Collections.Generic.KeyNotFoundException">If the map does not contain an entry with this key.</exception>
			public async Task<TValue> GetAsync(IFdbReadOnlyTransaction trans, TKey id)
			{
				if (trans == null) throw new ArgumentNullException(nameof(trans));
				if (id == null) throw new ArgumentNullException(nameof(id));

				var data = await trans.GetAsync(this.Subspace[id]).ConfigureAwait(false);

				if (data.IsNull) throw new KeyNotFoundException("The given id was not present in the map.");
				return this.ValueEncoder.DecodeValue(data)!;
			}

			/// <summary>Returns the value of an entry in the map if it exists.</summary>
			/// <param name="trans">Transaction used for the operation</param>
			/// <param name="id">Key of the entry to read from the map</param>
			/// <returns>Optional with the value of the entry it it exists, or an empty result if it is not present in the map.</returns>
			public async Task<(TValue Value, bool HasValue)> TryGetAsync(IFdbReadOnlyTransaction trans, TKey id)
			{
				if (trans == null) throw new ArgumentNullException(nameof(trans));
				if (id == null) throw new ArgumentNullException(nameof(id));

				var data = await trans.GetAsync(this.Subspace[id]).ConfigureAwait(false);

				if (data.IsNull) return (default(TValue), false);
				return (this.ValueEncoder.DecodeValue(data), true);
			}

			/// <summary>Add or update an entry in the map</summary>
			/// <param name="trans">Transaction used for the operation</param>
			/// <param name="id">Key of the entry to add or update</param>
			/// <param name="value">New value of the entry</param>
			/// <remarks>If the entry did not exist, it will be created. If not, its value will be replace with <paramref name="value"/>.</remarks>
			public void Set(IFdbTransaction trans, TKey id, TValue value)
			{
				if (trans == null) throw new ArgumentNullException(nameof(trans));
				if (id == null) throw new ArgumentNullException(nameof(id));

				trans.Set(this.Subspace[id], this.ValueEncoder.EncodeValue(value));
			}

			/// <summary>Remove a single entry from the map</summary>
			/// <param name="trans">Transaction used for the operation</param>
			/// <param name="id">Key of the entry to remove</param>
			/// <remarks>If the entry did not exist, the operation will not do anything.</remarks>
			public void Remove(IFdbTransaction trans, TKey id)
			{
				if (trans == null) throw new ArgumentNullException(nameof(trans));
				if (id == null) throw new ArgumentNullException(nameof(id));

				trans.Clear(this.Subspace[id]);
			}

			/// <summary>Create a query that will attempt to read all the entries in the map within a single transaction.</summary>
			/// <param name="trans">Transaction used for the operation</param>
			/// <param name="options"></param>
			/// <returns>Async sequence of pairs of keys and values, ordered by keys ascending.</returns>
			/// <remarks>CAUTION: This can be dangerous if the map contains a lot of entries! You should always use .Take() to limit the number of results returned.</remarks>
			public IAsyncEnumerable<KeyValuePair<TKey, TValue>> All(IFdbReadOnlyTransaction trans, FdbRangeOptions? options = null)
			{
				if (trans == null) throw new ArgumentNullException(nameof(trans));

				return trans
					.GetRange(this.Subspace.ToRange(), options)
					.Select(kv => DecodeItem(this.Subspace, this.ValueEncoder, kv));
			}

			/// <summary>Reads the values of multiple entries in the map</summary>
			/// <param name="trans">Transaction used for the operation</param>
			/// <param name="ids">List of the keys to read</param>
			/// <returns>Array of results, in the same order as specified in <paramref name="ids"/>.</returns>
			public async Task<TValue[]> GetValuesAsync(IFdbReadOnlyTransaction trans, IEnumerable<TKey> ids)
			{
				if (trans == null) throw new ArgumentNullException(nameof(trans));
				if (ids == null) throw new ArgumentNullException(nameof(ids));

				var kv = await trans.GetValuesAsync(ids.Select(id => this.Subspace[id])).ConfigureAwait(false);
				if (kv.Length == 0) return Array.Empty<TValue>();

				var result = new TValue[kv.Length];
				var decoder = this.ValueEncoder;
				for (int i = 0; i < kv.Length; i++)
				{
					result[i] = decoder.DecodeValue(kv[i])!;
				}

				return result;
			}

			#endregion

			#region Bulk Operations...

			/// <summary>Clear all the entries in the map</summary>
			/// <param name="trans">Transaction used for the operation</param>
			/// <remarks>This will delete EVERYTHING in the map!</remarks>
			public void Clear(IFdbTransaction trans)
			{
				if (trans == null) throw new ArgumentNullException(nameof(trans));

				trans.ClearRange(this.Subspace.ToRange());
			}

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
			public Task ImportAsync(IFdbDatabase db, IEnumerable<KeyValuePair<TKey, TValue>> items, CancellationToken ct)
			{
				if (db == null) throw new ArgumentNullException(nameof(db));
				if (items == null) throw new ArgumentNullException(nameof(items));

				return Fdb.Bulk.InsertAsync(
					db,
					items,
					(item, tr) => Set(tr, item.Key, item.Value),
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
			public Task ImportAsync(IFdbDatabase db, IEnumerable<TValue> items, Func<TValue, TKey> keySelector, CancellationToken ct)
			{
				if (db == null) throw new ArgumentNullException(nameof(db));
				if (items == null) throw new ArgumentNullException(nameof(items));
				if (keySelector == null) throw new ArgumentException(nameof(keySelector));

				return Fdb.Bulk.InsertAsync(
					db,
					items,
					(item, tr) => Set(tr, keySelector(item), item),
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
			public Task ImportAsync<TElement>(IFdbDatabase db, IEnumerable<TElement> items, Func<TElement, TKey> keySelector, Func<TElement, TValue> valueSelector, CancellationToken ct)
			{
				if (db == null) throw new ArgumentNullException(nameof(db));
				if (items == null) throw new ArgumentNullException(nameof(items));
				if (keySelector == null) throw new ArgumentException(nameof(keySelector));
				if (valueSelector == null) throw new ArgumentException(nameof(valueSelector));

				return Fdb.Bulk.InsertAsync(
					db,
					items,
					(item, tr) => Set(tr, keySelector(item), valueSelector(item)),
					ct
				);
			}

			#endregion

			#endregion

		}

		public async ValueTask<State> ResolveState(IFdbReadOnlyTransaction tr)
		{
			var subspace = await this.Location.Resolve(tr);
			if (subspace == null) throw new InvalidOperationException($"Location '{this.Location} referenced by Map Layer was not found.");
			//TODO: store in transaction context?
			return new State(subspace, this.ValueEncoder);
		}

		#region Export...

		/// <summary>Exports the content of this map out of the database, by using as many transactions as necessary.</summary>
		/// <param name="db">Database used for the operation</param>
		/// <param name="handler">Handler called for each entry in the map. Calls to the handler are serialized, so it does not need to take locks. Any exception will abort the export and be thrown to the caller</param>
		/// <param name="ct">Token used to cancel the operation.</param>
		/// <returns>Task that completes once all the entries have been processed.</returns>
		/// <remarks>This method does not guarantee that the export will be a complete and coherent snapshot of the map. Any change made to the map while the export is running may be partially exported.</remarks>
		public Task ExportAsync(IFdbDatabase db, Action<KeyValuePair<TKey, TValue>> handler, CancellationToken ct)
		{
			if (db == null) throw new ArgumentNullException(nameof(db));
			if (handler == null) throw new ArgumentNullException(nameof(handler));

			return Fdb.Bulk.ExportAsync(
				db,
				this.Location,
				(batch, loc, _, __) =>
				{
					var encoder = this.ValueEncoder;
					foreach (var item in batch)
					{
						handler(DecodeItem(loc, encoder, item));
					}
					return Task.CompletedTask;
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
		public Task ExportAsync(IFdbDatabase db, Func<KeyValuePair<TKey, TValue>, CancellationToken, Task> handler, CancellationToken ct)
		{
			if (db == null) throw new ArgumentNullException(nameof(db));
			if (handler == null) throw new ArgumentNullException(nameof(handler));

			return Fdb.Bulk.ExportAsync(
				db,
				this.Location,
				async (batch, loc, _, __) =>
				{
					var encoder = this.ValueEncoder;
					foreach (var item in batch)
					{
						await handler(DecodeItem(loc, encoder, item), ct);
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
		public Task ExportAsync(IFdbDatabase db, Action<KeyValuePair<TKey, TValue>[]> handler, CancellationToken ct)
		{
			if (db == null) throw new ArgumentNullException(nameof(db));
			if (handler == null) throw new ArgumentNullException(nameof(handler));

			return Fdb.Bulk.ExportAsync(
				db,
				this.Location,
				(batch, loc, _, __) =>
				{
					if (batch.Length > 0)
					{
						handler(DecodeItems(loc, this.ValueEncoder, batch));
					}
					return Task.CompletedTask;
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
		public Task ExportAsync(IFdbDatabase db, Func<KeyValuePair<TKey, TValue>[], CancellationToken, Task> handler, CancellationToken ct)
		{
			if (db == null) throw new ArgumentNullException(nameof(db));
			if (handler == null) throw new ArgumentNullException(nameof(handler));

			return Fdb.Bulk.ExportAsync(
				db,
				this.Location,
				(batch, loc, _, tok) => handler(DecodeItems(loc, this.ValueEncoder, batch), tok),
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
		public async Task<TResult> AggregateAsync<TResult>(IFdbDatabase db, Func<TResult> init, Func<TResult, KeyValuePair<TKey, TValue>[], TResult> handler, CancellationToken ct)
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
				this.Location,
				(batch, loc, _, __) =>
				{
					state = handler(state!, DecodeItems(loc, this.ValueEncoder, batch));
					return Task.CompletedTask;
				},
				ct
			);

			return state!;
		}

		/// <summary>Exports the content of this map out of the database, by using as many transactions as necessary.</summary>
		/// <param name="db">Database used for the operation</param>
		/// <param name="init">Handler that is called once before the first batch, to produce the initial state.</param>
		/// <param name="handler">Handler called for each batch of items in the map. It is given the previous state, and should return the updated state. Calls to the handler are serialized, so it does not need to take locks. Any exception will abort the export and be thrown to the caller</param>
		/// <param name="finish">Handler that is called one after the last batch, to produce the final result out of the last state.</param>
		/// <param name="ct">Token used to cancel the operation.</param>
		/// <returns>Task that completes once all the entries have been processed and return the result of calling <paramref name="finish"/> with the state return by the last call to <paramref name="handler"/> if there was at least one batch, or the result of <paramref name="init"/> if the map was empty.</returns>
		/// <remarks>This method does not guarantee that the export will be a complete and coherent snapshot of the map, except that all the items in a single batch are from the same snapshot. Any change made to the map while the export is running may be partially exported.</remarks>
		public async Task<TResult> AggregateAsync<TState, TResult>(IFdbDatabase db, Func<TState> init, Func<TState, KeyValuePair<TKey, TValue>[], TState> handler, Func<TState, TResult> finish, CancellationToken ct)
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
				this.Location,
				(batch, loc, _, __) =>
				{
					state = handler(state!, DecodeItems(loc, this.ValueEncoder, batch));
					return Task.CompletedTask;
				},
				ct
			);

			ct.ThrowIfCancellationRequested();

			var result = default(TResult);
			if (finish != null)
			{
				result = finish(state!);
			}

			return result!;
		}

		private static KeyValuePair<TKey, TValue> DecodeItem(ITypedKeySubspace<TKey> subspace, IValueEncoder<TValue> valueEncoder, KeyValuePair<Slice, Slice> item)
		{
			return new KeyValuePair<TKey, TValue>(
				subspace.Decode(item.Key)!,
				valueEncoder.DecodeValue(item.Value)!
			);
		}

		private static KeyValuePair<TKey, TValue>[] DecodeItems(ITypedKeySubspace<TKey> subspace, IValueEncoder<TValue> valueEncoder, KeyValuePair<Slice, Slice>[] batch)
		{
			Contract.Requires(batch != null);

			var items = new KeyValuePair<TKey, TValue>[batch.Length];
			for (int i = 0; i < batch.Length; i++)
			{
				items[i] = new KeyValuePair<TKey, TValue>(
					subspace.Decode(batch[i].Key)!,
					valueEncoder.DecodeValue(batch[i].Value)!
				);
			}
			return items;
		}

		#endregion

	}

}
