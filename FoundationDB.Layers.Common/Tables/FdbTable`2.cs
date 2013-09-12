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
	using FoundationDB.Async;
	using FoundationDB.Client;
	using FoundationDB.Layers.Tuples;
	using FoundationDB.Linq;
	using System;
	using System.Collections.Generic;
	using System.Diagnostics;
	using System.Linq;
	using System.Threading;
	using System.Threading.Tasks;

	[DebuggerDisplay("Name={Name}, Subspace={Subspace}")]
	public class FdbTable<TKey, TValue>
	{

		public FdbTable(string name, FdbSubspace subspace, ITupleFormatter<TKey> keyReader, ISliceSerializer<TValue> valueSerializer)
		{
			if (name == null) throw new ArgumentNullException("name");
			if (subspace == null) throw new ArgumentNullException("subspace");
			if (keyReader == null) throw new ArgumentNullException("keyReader");
			if (valueSerializer == null) throw new ArgumentNullException("valueSerializer");

			this.Table = new FdbTable(name, subspace);
			this.KeyReader = keyReader;
			this.ValueSerializer = valueSerializer;
		}

		#region Public Properties...

		public string Name { get { return this.Table.Name; } }

		/// <summary>Subspace used as a prefix for all items in this table</summary>
		public FdbSubspace Subspace { get { return this.Table.Subspace; } }
		
		/// <summary>Class that can pack/unpack keys into/from tuples</summary>
		public ITupleFormatter<TKey> KeyReader { get; private set; }

		/// <summary>Class that can serialize/deserialize values into/from slices</summary>
		public ISliceSerializer<TValue> ValueSerializer { get; private set; }

		internal FdbTable Table { get; private set; }

		#endregion

		#region Public Methods...

		internal Slice MakeKey(TKey key)
		{
			return this.Table.MakeKey(this.KeyReader.ToTuple(key));
		}

		public async Task<TValue> GetAsync(IFdbReadTransaction trans, TKey key, CancellationToken ct = default(CancellationToken))
		{
			if (trans == null) throw new ArgumentNullException("trans");

			Slice data = await this.Table.GetAsync(trans, this.KeyReader.ToTuple(key), ct).ConfigureAwait(false);

			if (data.IsNull) return default(TValue);
			return this.ValueSerializer.Deserialize(data, default(TValue));
		}

		public void Set(IFdbTransaction trans, TKey key, TValue value)
		{
			if (trans == null) throw new ArgumentNullException("trans");

			this.Table.Set(trans, this.KeyReader.ToTuple(key), this.ValueSerializer.Serialize(value));
		}

		public void Clear(IFdbTransaction trans, TKey key)
		{
			if (trans == null) throw new ArgumentNullException("trans");

			this.Table.Clear(trans, this.KeyReader.ToTuple(key));
		}

		public Task<List<KeyValuePair<TKey, TValue>>> GetAllAsync(IFdbReadTransaction trans, CancellationToken ct = default(CancellationToken))
		{
			if (trans == null) throw new ArgumentNullException("trans");

			ct.ThrowIfCancellationRequested();

			var subspace = this.Table.Subspace;
			var missing = default(TValue);

			return trans
				.GetRangeStartsWith(this.Subspace) //TODO: options?
				.Select(
					(key) => this.KeyReader.FromTuple(subspace.Unpack(key)),
					(value) => this.ValueSerializer.Deserialize(value, missing)
				)
				.ToListAsync(ct);
		}

		public async Task<List<TValue>> GetValuesAsync(IFdbReadTransaction trans, IEnumerable<TKey> ids, CancellationToken ct = default(CancellationToken))
		{
			if (trans == null) throw new ArgumentNullException("trans");
			if (ids == null) throw new ArgumentNullException("ids");

			ct.ThrowIfCancellationRequested();

			var results = await trans.GetValuesAsync(ids.Select(MakeKey), ct).ConfigureAwait(false);

			return results.Select((value) => this.ValueSerializer.Deserialize(value, default(TValue))).ToList();
		}

		#endregion

	}

}
