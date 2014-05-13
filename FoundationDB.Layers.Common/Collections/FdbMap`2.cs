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

namespace FoundationDB.Layers.Collections
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
	public class FdbMap<TKey, TValue>
	{

		public FdbMap(string name, FdbSubspace subspace, IValueEncoder<TValue> valueEncoder)
			: this(name, subspace, KeyValueEncoders.Tuples.Key<TKey>(), valueEncoder)
		{ }

		public FdbMap(string name, FdbSubspace subspace, IKeyEncoder<TKey> keyEncoder, IValueEncoder<TValue> valueEncoder)
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
		public string Name { get; private set; }

		/// <summary>Subspace used as a prefix for all items in this map</summary>
		public FdbSubspace Subspace { get; private set; }

		/// <summary>Subspace used to encoded the keys for the items</summary>
		protected FdbEncoderSubspace<TKey> Location { get; private set; }

		/// <summary>Class that can serialize/deserialize values into/from slices</summary>
		public IValueEncoder<TValue> ValueEncoder { get; private set; }

		#endregion

		#region Get / Set / Clear...

		/// <summary>Returns the value of an existing entry in the map</summary>
		/// <param name="trans">Transaction used for the operation</param>
		/// <param name="id">Key of the entry to read from the map</param>
		/// <returns>Value of the entry if it exists; otherwise, throws an exception</returns>
		/// <exception cref="System.ArgumentNullException">If either <paramref name="trans"/> or <paramref name="id"/> is null.</exception>
		/// <exception cref="System.Collections.Generic.KeyNotFoundException">If the map does not contain an entry with this key.</exception>
		public async Task<TValue> GetAsync(IFdbReadOnlyTransaction trans, TKey id)
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
		public async Task<Optional<TValue>> TryGetAsync(IFdbReadOnlyTransaction trans, TKey id)
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
		public void Set(IFdbTransaction trans, TKey id, TValue value)
		{
			if (trans == null) throw new ArgumentNullException("trans");
			if (id == null) throw new ArgumentNullException("id");

			this.Location.Set(trans, id, this.ValueEncoder.EncodeValue(value));
		}

		/// <summary>Remove an entry from the map</summary>
		/// <param name="trans">Transaction used for the operation</param>
		/// <param name="id">Key of the entry to remove</param>
		/// <remarks>If the entry did not exist, the operation will not do anything.</remarks>
		public void Clear(IFdbTransaction trans, TKey id)
		{
			if (trans == null) throw new ArgumentNullException("trans");
			if (id == null) throw new ArgumentNullException("id");

			this.Location.Clear(trans, id);
		}

		/// <summary>Reads all the entries in the map</summary>
		/// <param name="trans">Transaction used for the operation</param>
		/// <returns>Async sequence of pairs of keys and values, ordered by keys ascending.</returns>
		/// <remarks>This can be dangerous if the map contains a lot of entries! You should always use .Take() to limit the number of results returned.</remarks>
		public IFdbAsyncEnumerable<KeyValuePair<TKey, TValue>> All(IFdbReadOnlyTransaction trans)
		{
			if (trans == null) throw new ArgumentNullException("trans");

			return trans
				.GetRange(this.Location.ToRange()) //TODO: options ?
				.Select((kvp) => new KeyValuePair<TKey, TValue>(
					this.Location.DecodeKey(kvp.Key),
					this.ValueEncoder.DecodeValue(kvp.Value)
				));
		}

		/// <summary>Reads the values of multiple entries in the map</summary>
		/// <param name="trans">Transaction used for the operation</param>
		/// <param name="ids">List of the keys to read</param>
		/// <returns>Array of results, in the same order as specified in <paramref name="ids"/>.</returns>
		public async Task<Optional<TValue>[]> GetValuesAsync(IFdbReadOnlyTransaction trans, IEnumerable<TKey> ids)
		{
			if (trans == null) throw new ArgumentNullException("trans");
			if (ids == null) throw new ArgumentNullException("ids");

			var results = await this.Location.GetValuesAsync(trans, ids).ConfigureAwait(false);

			return Optional.DecodeRange(this.ValueEncoder, results);
		}

		#endregion

	}

}
