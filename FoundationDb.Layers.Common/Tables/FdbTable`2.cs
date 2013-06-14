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

namespace FoundationDb.Layers.Tables
{
	using FoundationDb.Client;
	using FoundationDb.Layers.Tuples;
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using System.Threading;
	using System.Threading.Tasks;

	public class FdbTable<TKey, TValue>
	{

		public FdbTable(FdbDatabase database, FdbSubspace subspace, ITupleKeyReader<TKey> keyReader, ISliceSerializer<TValue> valueSerializer)
		{
			if (database == null) throw new ArgumentNullException("database");
			if (subspace == null) throw new ArgumentNullException("subspace");
			if (keyReader == null) throw new ArgumentNullException("keyReader");
			if (valueSerializer == null) throw new ArgumentNullException("valueSerializer");

			this.Database = database;
			this.Subspace = subspace;
			this.KeyReader = keyReader;
			this.ValueSerializer = valueSerializer;
		}

		/// <summary>Database used to perform transactions</summary>
		public FdbDatabase Database { get; private set; }

		/// <summary>Subspace used as a prefix for all items in this table</summary>
		public FdbSubspace Subspace { get; private set; }
		
		/// <summary>Class that can pack/unpack keys into/from tuples</summary>
		public ITupleKeyReader<TKey> KeyReader { get; private set; }

		/// <summary>Class that can serialize/deserialize values into/from slices</summary>
		public ISliceSerializer<TValue> ValueSerializer { get; private set; }

		public Slice GetKeyBytes(TKey key)
		{
			return this.KeyReader.Append(this.Subspace.Tuple, key).ToSlice();
		}

		/// <summary>Returns a tuple (namespace, key, )</summary>
		/// <typeparam name="TKey"></typeparam>
		/// <param name="key"></param>
		/// <returns></returns>
		public IFdbTuple Key(TKey key)
		{
			return this.Subspace.Append<TKey>(key);
		}

		public async Task<TValue> GetAsync(FdbTransaction trans, TKey key, bool snapshot = false, CancellationToken ct = default(CancellationToken))
		{
			if (trans == null) throw new ArgumentNullException("trans");

			Slice data = await trans.GetAsync(GetKeyBytes(key), snapshot, ct).ConfigureAwait(false);

			if (!data.HasValue) return default(TValue);
			return this.ValueSerializer.Deserialize(data, default(TValue));
		}

		public async Task<TValue> GetAsync(TKey key, bool snapshot = false, CancellationToken ct = default(CancellationToken))
		{
			if (ct.IsCancellationRequested) ct.ThrowIfCancellationRequested();

			using (var trans = this.Database.BeginTransaction())
			{
				return await this.GetAsync(key, snapshot, ct);
			}
		}

		public void Set(FdbTransaction trans, TKey key, TValue value)
		{
			if (trans == null) throw new ArgumentNullException("trans");

			trans.Set(GetKeyBytes(key), this.ValueSerializer.Serialize(value));
		}

		public async Task SetAsync(TKey key, TValue value)
		{
			using (var trans = this.Database.BeginTransaction())
			{
				Set(trans, key, value);
				await trans.CommitAsync().ConfigureAwait(false);
			}
		}

		public Task<List<KeyValuePair<TKey, TValue>>> GetAllAsync(FdbTransaction trans, bool snapshot = false, CancellationToken ct = default(CancellationToken))
		{
			if (trans == null) throw new ArgumentNullException("trans");

			//TODO: make it simpler / configurable ?
			int offset = this.Subspace.Tuple.Count;
			TValue missing = default(TValue);

			return trans
				.GetRangeStartsWith(this.Subspace.Tuple, snapshot: snapshot)
				.ReadAllAsync(
					(key) => this.KeyReader.Unpack(FdbTuple.Unpack(key), offset),
					(value) => this.ValueSerializer.Deserialize(value, missing),
					(key, value) => new KeyValuePair<TKey, TValue>(key, value),
					ct
				);
		}

		public async Task<List<KeyValuePair<TKey, TValue>>> GetBatchIndexedAsync(FdbTransaction trans, IEnumerable<TKey> ids, bool snapshot = false, CancellationToken ct = default(CancellationToken))
		{
			var keys = ids.ToArray();

			var results = await trans.GetBatchIndexedAsync(keys.Select(GetKeyBytes), snapshot, ct);

			//TODO: make it simpler / configurable ?
			TValue missing = default(TValue);

			return results.Select((kvp) => new KeyValuePair<TKey, TValue>(
				keys[kvp.Key],
				this.ValueSerializer.Deserialize(kvp.Value, missing)
			)).ToList();
		}

		public async Task<List<TValue>> GetBatchAsync(FdbTransaction trans, IEnumerable<TKey> ids, bool snapshot = false, CancellationToken ct = default(CancellationToken))
		{
			var results = await trans.GetBatchAsync(ids.Select(GetKeyBytes), snapshot, ct);

			//TODO: make it simpler / configurable ?
			TValue missing = default(TValue);

			return results.Select((kvp) => this.ValueSerializer.Deserialize(kvp.Value, missing)).ToList();
		}
	}

}
