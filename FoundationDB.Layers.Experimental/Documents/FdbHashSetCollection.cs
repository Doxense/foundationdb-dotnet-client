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

namespace FoundationDB.Layers.Blobs
{
	using FoundationDB.Client;
	using FoundationDB.Client.Utils;
	using FoundationDB.Layers.Tuples;
	using FoundationDB.Linq;
	using JetBrains.Annotations;
	using System;
	using System.Collections.Generic;
	using System.Globalization;
	using System.IO;
	using System.Linq;
	using System.Threading;
	using System.Threading.Tasks;

	// THIS IS NOT AN OFFICIAL LAYER, JUST A PROTOTYPE TO TEST A FEW THINGS !

	/// <summary>Represents a collection of dictionaries of fields.</summary>
	public class FdbHashSetCollection
	{

		public FdbHashSetCollection(IFdbSubspace subspace)
		{
			if (subspace == null) throw new ArgumentNullException("subspace");

			this.Subspace = subspace.Using(TypeSystem.Tuples);
		}

		/// <summary>Subspace used as a prefix for all hashsets in this collection</summary>
		public IFdbDynamicSubspace Subspace { get; private set; }

		/// <summary>Returns the key prefix of an HashSet: (subspace, id, )</summary>
		/// <param name="id"></param>
		/// <returns></returns>
		protected virtual Slice GetKey(IFdbTuple id)
		{
			//REVIEW: should the id be encoded as a an embedded tuple or not?
			return this.Subspace.Keys.Pack(id);
		}

		/// <summary>Returns the key of a specific field of an HashSet: (subspace, id, field, )</summary>
		/// <param name="id"></param>
		/// <param name="field"></param>
		/// <returns></returns>
		protected virtual Slice GetFieldKey(IFdbTuple id, string field)
		{
			//REVIEW: should the id be encoded as a an embedded tuple or not?
			return this.Subspace.Keys.Pack(id.Append(field));
		}

		protected virtual string ParseFieldKey(IFdbTuple key)
		{
			return key.Last<string>();
		}

		#region Get

		/// <summary>Return the value of a specific field of an hashset</summary>
		/// <param name="trans">Transaction that will be used for this request</param>
		/// <param name="id">Unique identifier of the hashset</param>
		/// <param name="field">Name of the field to read</param>
		/// <returns>Value of the corresponding field, or Slice.Nil if it the hashset does not exist, or doesn't have a field with this name</returns>
		public Task<Slice> GetValueAsync([NotNull] IFdbReadOnlyTransaction trans, [NotNull] IFdbTuple id, string field)
		{
			if (trans == null) throw new ArgumentNullException("trans");
			if (id == null) throw new ArgumentNullException("id");
			if (string.IsNullOrEmpty(field)) throw new ArgumentNullException("field");

			return trans.GetAsync(GetFieldKey(id, field));
		}

		/// <summary>Return all fields of an hashset</summary>
		/// <param name="trans">Transaction that will be used for this request</param>
		/// <param name="id">Unique identifier of the hashset</param>
		/// <returns>Dictionary containing, for all fields, their associated values</returns>
		public async Task<IDictionary<string, Slice>> GetAsync([NotNull] IFdbReadOnlyTransaction trans, [NotNull] IFdbTuple id)
		{
			if (trans == null) throw new ArgumentNullException("trans");
			if (id == null) throw new ArgumentNullException("id");

			var prefix = GetKey(id);
			var results = new Dictionary<string, Slice>(StringComparer.OrdinalIgnoreCase);

			await trans
				.GetRange(FdbKeyRange.StartsWith(prefix))
				.ForEachAsync((kvp) =>
				{
					string field = this.Subspace.Keys.DecodeLast<string>(kvp.Key);
					results[field] = kvp.Value;
				})
				.ConfigureAwait(false);

			return results;
		}

		/// <summary>Return one or more fields of an hashset</summary>
		/// <param name="trans">Transaction that will be used for this request</param>
		/// <param name="id">Unique identifier of the hashset</param>
		/// <param name="fields">List of the fields to read</param>
		/// <returns>Dictionary containing the values of the selected fields, or Slice.Empty if that particular field does not exist.</returns>
		public async Task<IDictionary<string, Slice>> GetAsync([NotNull] IFdbReadOnlyTransaction trans, [NotNull] IFdbTuple id, [NotNull] params string[] fields)
		{
			if (trans == null) throw new ArgumentNullException("trans");
			if (id == null) throw new ArgumentNullException("id");
			if (fields == null) throw new ArgumentNullException("fields");

			var keys = FdbTuple.EncodePrefixedKeys(GetKey(id), fields);

			var values = await trans.GetValuesAsync(keys).ConfigureAwait(false);
			Contract.Assert(values != null && values.Length == fields.Length);

			var results = new Dictionary<string, Slice>(values.Length, StringComparer.OrdinalIgnoreCase);
			for (int i = 0; i < fields.Length; i++)
			{
				results[fields[i]] = values[i];
			}
			return results;
		}

		#endregion

		#region Set

		public void SetValue(IFdbTransaction trans, IFdbTuple id, string field, Slice value)
		{
			if (trans == null) throw new ArgumentNullException("trans");
			if (id == null) throw new ArgumentNullException("id");
			if (string.IsNullOrEmpty(field)) throw new ArgumentNullException("field");

			trans.Set(GetFieldKey(id, field), value);
		}

		public void Set(IFdbTransaction trans, IFdbTuple id, IEnumerable<KeyValuePair<string, Slice>> fields)
		{
			if (trans == null) throw new ArgumentNullException("trans");
			if (id == null) throw new ArgumentNullException("id");
			if (fields == null) throw new ArgumentNullException("fields");

			foreach (var field in fields)
			{
				if (string.IsNullOrEmpty(field.Key)) throw new ArgumentException("Field cannot have an empty name", "fields");
				trans.Set(GetFieldKey(id, field.Key), field.Value);
			}
		}

		#endregion

		#region Delete

		/// <summary>Remove a field of an hashset</summary>
		/// <param name="trans"></param>
		/// <param name="id"></param>
		/// <param name="field"></param>
		public void DeleteValue(IFdbTransaction trans, IFdbTuple id, string field)
		{
			if (trans == null) throw new ArgumentNullException("trans");
			if (id == null) throw new ArgumentNullException("id");
			if (string.IsNullOrEmpty(field)) throw new ArgumentNullException("field");

			trans.Clear(GetFieldKey(id, field));
		}

		/// <summary>Remove all fields of an hashset</summary>
		/// <param name="id"></param>
		public void Delete(IFdbTransaction trans, IFdbTuple id)
		{
			if (trans == null) throw new ArgumentNullException("trans");
			if (id == null) throw new ArgumentNullException("id");

			// remove all fields of the hash
			trans.ClearRange(FdbKeyRange.StartsWith(GetKey(id)));
		}

		/// <summary>Remove one or more fields of an hashset</summary>
		/// <param name="trans"></param>
		/// <param name="id"></param>
		/// <param name="fields"></param>
		public void Delete(IFdbTransaction trans, IFdbTuple id, params string[] fields)
		{
			if (trans == null) throw new ArgumentNullException("trans");
			if (id == null) throw new ArgumentNullException("id");
			if (fields == null) throw new ArgumentNullException("fields");

			foreach (var field in fields)
			{
				if (string.IsNullOrEmpty(field)) throw new ArgumentException("Field cannot have an empty name", "fields");
				trans.Clear(GetFieldKey(id, field));
			}
		}

		#endregion

		#region Keys

		/// <summary>Return the list the names of all fields of an hashset</summary>
		/// <param name="trans">Transaction that will be used for this request</param>
		/// <param name="id">Unique identifier of the hashset</param>
		/// <returns>List of all fields. If the list is empty, the hashset does not exist</returns>
		public Task<List<string>> GetKeys(IFdbReadOnlyTransaction trans, IFdbTuple id, CancellationToken cancellationToken = default(CancellationToken))
		{
			//note: As of Beta2, FDB does not have a fdb_get_range that only return the keys. That means that we will have to also read the values from the db, in order to just get the names of the fields :(
			//TODO: find a way to optimize this ?

			if (trans == null) throw new ArgumentNullException("trans");
			if (id == null) throw new ArgumentNullException("id");

			var prefix = GetKey(id);
			var results = new Dictionary<string, Slice>(StringComparer.OrdinalIgnoreCase);

			return trans
				.GetRange(FdbKeyRange.StartsWith(prefix))
				.Select((kvp) => ParseFieldKey(FdbTuple.Unpack(kvp.Key)))
				.ToListAsync(cancellationToken);
		}

		#endregion
	}

}
