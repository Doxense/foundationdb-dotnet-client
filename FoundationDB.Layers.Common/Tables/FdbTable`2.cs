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

		public FdbTable(string name, FdbSubspace subspace, IValueEncoder<TValue> valueEncoder)
			: this(name, subspace, KeyValueEncoders.Tuples.Key<TKey>(), valueEncoder)
		{ }

		public FdbTable(string name, FdbSubspace subspace, IKeyEncoder<TKey> keyEncoder, IValueEncoder<TValue> valueEncoder)
		{
			if (name == null) throw new ArgumentNullException("name");
			if (subspace == null) throw new ArgumentNullException("subspace");
			if (keyEncoder == null) throw new ArgumentNullException("keyEncoder");
			if (valueEncoder == null) throw new ArgumentNullException("valueEncoder");

			this.Name = name;
			this.Subspace = subspace;
			this.KeyEncoder = keyEncoder;
			this.ValueEncoder = valueEncoder;
		}

		#region Public Properties...

		/// <summary>Name of the table</summary>
		public string Name { get; private set; }

		/// <summary>Subspace used as a prefix for all items in this table</summary>
		public FdbSubspace Subspace { get; private set; }

		/// <summary>Class that can pack/unpack keys into/from slices</summary>
		public IKeyEncoder<TKey> KeyEncoder { get; private set; }

		/// <summary>Class that can serialize/deserialize values into/from slices</summary>
		public IValueEncoder<TValue> ValueEncoder { get; private set; }

		#endregion

		#region Get / Set / Clear...

		public async Task<TValue> GetAsync(IFdbReadOnlyTransaction trans, TKey id)
		{
			if (trans == null) throw new ArgumentNullException("trans");
			if (id == null) throw new ArgumentNullException("id");

			var data = await trans.GetAsync(this.Subspace.Encode<TKey>(this.KeyEncoder, id)).ConfigureAwait(false);

			if (data.IsNull) throw new KeyNotFoundException(); //TODO: message!
			return this.ValueEncoder.DecodeValue(data);
		}

		public async Task<Optional<TValue>> TryGetAsync(IFdbReadOnlyTransaction trans, TKey id)
		{
			if (trans == null) throw new ArgumentNullException("trans");
			if (id == null) throw new ArgumentNullException("id");

			var data = await trans.GetAsync(this.Subspace.Encode<TKey>(this.KeyEncoder, id)).ConfigureAwait(false);

			if (data.IsNull) return default(Optional<TValue>);
			return this.ValueEncoder.DecodeValue(data);
		}

		public void Set(IFdbTransaction trans, TKey id, TValue value)
		{
			if (trans == null) throw new ArgumentNullException("trans");
			if (id == null) throw new ArgumentNullException("id");

			trans.Set(this.Subspace.Encode<TKey>(this.KeyEncoder, id), this.ValueEncoder.EncodeValue(value));
		}

		public void Clear(IFdbTransaction trans, TKey id)
		{
			if (trans == null) throw new ArgumentNullException("trans");
			if (id == null) throw new ArgumentNullException("id");

			trans.Clear(this.Subspace.Encode<TKey>(this.KeyEncoder, id));
		}

		public IFdbAsyncEnumerable<KeyValuePair<TKey, TValue>> All(IFdbReadOnlyTransaction trans)
		{
			if (trans == null) throw new ArgumentNullException("trans");

			return trans
				.GetRange(this.Subspace.ToRange()) //TODO: options ?
				.Select((kvp) => new KeyValuePair<TKey, TValue>(
					this.Subspace.Decode<TKey>(this.KeyEncoder, kvp.Key),
					this.ValueEncoder.DecodeValue(kvp.Value)
				));
		}

		public async Task<Optional<TValue>[]> GetValuesAsync(IFdbReadOnlyTransaction trans, IEnumerable<TKey> ids)
		{
			if (trans == null) throw new ArgumentNullException("trans");
			if (ids == null) throw new ArgumentNullException("ids");

			var results = await trans
				.GetValuesAsync(this.Subspace.EncodeRange(this.KeyEncoder, ids))
				.ConfigureAwait(false);

			return Optional.DecodeRange(this.ValueEncoder, results);
		}

		#endregion

	}

}
