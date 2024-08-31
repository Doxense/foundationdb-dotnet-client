#region Copyright (c) 2023-2024 SnowBank SAS, (c) 2005-2023 Doxense SAS
// All rights reserved.
// 
// Redistribution and use in source and binary forms, with or without
// modification, are permitted provided that the following conditions are met:
// 	* Redistributions of source code must retain the above copyright
// 	  notice, this list of conditions and the following disclaimer.
// 	* Redistributions in binary form must reproduce the above copyright
// 	  notice, this list of conditions and the following disclaimer in the
// 	  documentation and/or other materials provided with the distribution.
// 	* Neither the name of SnowBank nor the
// 	  names of its contributors may be used to endorse or promote products
// 	  derived from this software without specific prior written permission.
// 
// THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND
// ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
// WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
// DISCLAIMED. IN NO EVENT SHALL SNOWBANK SAS BE LIABLE FOR ANY
// DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
// (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
// LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
// ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
// (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
// SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
#endregion

namespace FoundationDB.Layers.Blobs
{
	using System;
	using System.Collections.Generic;
	using System.Threading.Tasks;
	using Doxense.Collections.Tuples;
	using Doxense.Diagnostics.Contracts;
	using FoundationDB.Client;
	using JetBrains.Annotations;

	// THIS IS NOT AN OFFICIAL LAYER, JUST A PROTOTYPE TO TEST A FEW THINGS !

	/// <summary>Represents a collection of dictionaries of fields.</summary>
	[PublicAPI]
	public class FdbHashSetCollection
	{

		public FdbHashSetCollection(IKeySubspace subspace)
		{
			if (subspace == null) throw new ArgumentNullException(nameof(subspace));

			this.Subspace = subspace.AsDynamic();
		}

		/// <summary>Subspace used as a prefix for all hashsets in this collection</summary>
		public IDynamicKeySubspace Subspace { get; }

		/// <summary>Returns the key prefix of an HashSet: (subspace, id, )</summary>
		/// <param name="id"></param>
		/// <returns></returns>
		protected virtual Slice GetKey(IVarTuple id)
		{
			//REVIEW: should the id be encoded as a an embedded tuple or not?
			return this.Subspace.Pack(id);
		}

		/// <summary>Returns the key of a specific field of an HashSet: (subspace, id, field, )</summary>
		/// <param name="id"></param>
		/// <param name="field"></param>
		/// <returns></returns>
		protected virtual Slice GetFieldKey(IVarTuple id, string field)
		{
			//REVIEW: should the id be encoded as a an embedded tuple or not?
			return this.Subspace.Pack(id.Append(field));
		}

		protected virtual string ParseFieldKey(IVarTuple key)
		{
			return key.Last<string>()!;
		}

		#region Get

		/// <summary>Return the value of a specific field of an hashset</summary>
		/// <param name="trans">Transaction that will be used for this request</param>
		/// <param name="id">Unique identifier of the hashset</param>
		/// <param name="field">Name of the field to read</param>
		/// <returns>Value of the corresponding field, or <see cref="Slice.Nil"/> if it the hashset does not exist, or doesn't have a field with this name</returns>
		public Task<Slice> GetValueAsync(IFdbReadOnlyTransaction trans, IVarTuple id, string field)
		{
			if (trans == null) throw new ArgumentNullException(nameof(trans));
			if (id == null) throw new ArgumentNullException(nameof(id));
			if (string.IsNullOrEmpty(field)) throw new ArgumentNullException(nameof(field));

			return trans.GetAsync(GetFieldKey(id, field));
		}

		/// <summary>Return all fields of an hashset</summary>
		/// <param name="trans">Transaction that will be used for this request</param>
		/// <param name="id">Unique identifier of the hashset</param>
		/// <returns>Dictionary containing, for all fields, their associated values</returns>
		public async Task<IDictionary<string, Slice>> GetAsync(IFdbReadOnlyTransaction trans, IVarTuple id)
		{
			if (trans == null) throw new ArgumentNullException(nameof(trans));
			if (id == null) throw new ArgumentNullException(nameof(id));

			var prefix = GetKey(id);
			var results = new Dictionary<string, Slice>(StringComparer.OrdinalIgnoreCase);

			await trans
				.GetRange(KeyRange.StartsWith(prefix))
				.ForEachAsync((kvp) =>
				{
					string field = this.Subspace.DecodeLast<string>(kvp.Key)!;
					results[field] = kvp.Value;
				})
				.ConfigureAwait(false);

			return results;
		}

		/// <summary>Return one or more fields of an hashset</summary>
		/// <param name="trans">Transaction that will be used for this request</param>
		/// <param name="id">Unique identifier of the hashset</param>
		/// <param name="fields">List of the fields to read</param>
		/// <returns>Dictionary containing the values of the selected fields, or <see cref="Slice.Empty"/> if that particular field does not exist.</returns>
		public async Task<IDictionary<string, Slice>> GetAsync(IFdbReadOnlyTransaction trans, IVarTuple id, params string[] fields)
		{
			if (trans == null) throw new ArgumentNullException(nameof(trans));
			if (id == null) throw new ArgumentNullException(nameof(id));
			if (fields == null) throw new ArgumentNullException(nameof(fields));

			var keys = TuPack.EncodePrefixedKeys(GetKey(id), fields);

			var values = await trans.GetValuesAsync(keys).ConfigureAwait(false);
			Contract.Debug.Assert(values != null && values.Length == fields.Length);

			var results = new Dictionary<string, Slice>(values.Length, StringComparer.OrdinalIgnoreCase);
			for (int i = 0; i < fields.Length; i++)
			{
				results[fields[i]] = values[i];
			}
			return results;
		}

		#endregion

		#region Set

		public void SetValue(IFdbTransaction trans, IVarTuple id, string field, Slice value)
		{
			if (trans == null) throw new ArgumentNullException(nameof(trans));
			if (id == null) throw new ArgumentNullException(nameof(id));
			if (string.IsNullOrEmpty(field)) throw new ArgumentNullException(nameof(field));

			trans.Set(GetFieldKey(id, field), value);
		}

		public void Set(IFdbTransaction trans, IVarTuple id, IEnumerable<KeyValuePair<string, Slice>> fields)
		{
			if (trans == null) throw new ArgumentNullException(nameof(trans));
			if (id == null) throw new ArgumentNullException(nameof(id));
			if (fields == null) throw new ArgumentNullException(nameof(fields));

			foreach (var field in fields)
			{
				if (string.IsNullOrEmpty(field.Key)) throw new ArgumentException("Field cannot have an empty name", nameof(fields));
				trans.Set(GetFieldKey(id, field.Key), field.Value);
			}
		}

		#endregion

		#region Delete

		/// <summary>Remove a field of an hashset</summary>
		public void DeleteValue(IFdbTransaction trans, IVarTuple id, string field)
		{
			if (trans == null) throw new ArgumentNullException(nameof(trans));
			if (id == null) throw new ArgumentNullException(nameof(id));
			if (string.IsNullOrEmpty(field)) throw new ArgumentNullException(nameof(field));

			trans.Clear(GetFieldKey(id, field));
		}

		/// <summary>Remove all fields of an hashset</summary>
		public void Delete(IFdbTransaction trans, IVarTuple id)
		{
			if (trans == null) throw new ArgumentNullException(nameof(trans));
			if (id == null) throw new ArgumentNullException(nameof(id));

			// remove all fields of the hash
			trans.ClearRange(KeyRange.StartsWith(GetKey(id)));
		}

		/// <summary>Remove one or more fields of an hashset</summary>
		public void Delete(IFdbTransaction trans, IVarTuple id, params string[] fields)
		{
			if (trans == null) throw new ArgumentNullException(nameof(trans));
			if (id == null) throw new ArgumentNullException(nameof(id));
			if (fields == null) throw new ArgumentNullException(nameof(fields));

			foreach (var field in fields)
			{
				if (string.IsNullOrEmpty(field)) throw new ArgumentException("Field cannot have an empty name", nameof(fields));
				trans.Clear(GetFieldKey(id, field));
			}
		}

		#endregion

		#region Keys

		/// <summary>Return the list the names of all fields of an hashset</summary>
		/// <param name="trans">Transaction that will be used for this request</param>
		/// <param name="id">Unique identifier of the hashset</param>
		/// <returns>List of all fields. If the list is empty, the hashset does not exist</returns>
		public Task<List<string>> GetKeys(IFdbReadOnlyTransaction trans, IVarTuple id)
		{
			if (trans == null) throw new ArgumentNullException(nameof(trans));
			if (id == null) throw new ArgumentNullException(nameof(id));

			var prefix = GetKey(id);
			return trans
				.GetRangeKeys(KeyRange.StartsWith(prefix))
				.Select((k) => ParseFieldKey(TuPack.Unpack(k)))
				.ToListAsync();
		}

		#endregion
	}

}
