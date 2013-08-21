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

namespace FoundationDB.Layers.Indexing
{
	using FoundationDB.Client;
	using FoundationDB.Layers.Tuples;
	using FoundationDB.Linq;
	using System;
	using System.Collections.Generic;
	using System.Diagnostics;
	using System.Globalization;
	using System.Threading;
	using System.Threading.Tasks;

	/// <summary>Simple index that maps values of type <typeparamref name="TValue"/> into lists of ids of type <typeparamref name="TId"/></summary>
	/// <typeparam name="TId">Type of the unique id of each document or entity</typeparam>
	/// <typeparam name="TValue">Type of the value being indexed</typeparam>
	[DebuggerDisplay("Name={Name}, Subspace={Subspace}, IndexNullValues={IndexNullValues})")]
	public class FdbIndex<TId, TValue>
	{

		public FdbIndex(string name, FdbSubspace subspace, IEqualityComparer<TValue> valueComparer = null, bool indexNullValues = false)
		{
			if (name == null) throw new ArgumentNullException("name");
			if (subspace == null) throw new ArgumentNullException("subspace");

			this.Name = name;
			this.Subspace = subspace;
			this.ValueComparer = valueComparer ?? EqualityComparer<TValue>.Default;
			this.IndexNullValues = indexNullValues;
		}

		public string Name { get; private set; }

		public FdbSubspace Subspace { get; private set; }

		public IEqualityComparer<TValue> ValueComparer { get; private set; }

		/// <summary>If true, null values are inserted in the index. If false (default), they are ignored</summary>
		/// <remarks>This has no effect if <typeparam name="TValue" /> is not a reference type</remarks>
		public bool IndexNullValues { get; private set; }

		/// <summary>Insert a newly created entity to the index</summary>
		/// <param name="trans">Transaction to use</param>
		/// <param name="id">Id of the new entity (that was never indexed before)</param>
		/// <param name="value">Value of this entity in the index</param>
		/// <returns>True if a value was inserted into the index; otherwise false (if value is null and <see cref="IndexNullValue"/> is false)</returns>
		public bool Add(IFdbTransaction trans, TId id, TValue value)
		{
			if (this.IndexNullValues || value != null)
			{
				trans.Set(this.Subspace.Create(value, id), Slice.Empty);
				return true;
			}
			return false;
		}

		/// <summary>Update the indexed values of an entity</summary>
		/// <param name="trans">Transaction to use</param>
		/// <param name="id">Id of the entity that has changed</param>
		/// <param name="newValue">Previous value of this entity in the index</param>
		/// <param name="previousValue">New value of this entity in the index</param>
		/// <returns>True if a change was performed in the index; otherwise false (if <paramref name="previousValue"/> and <paramref name="newValue"/>)</returns>
		/// <remarks>If <paramref name="newValue"/> and <paramref name="previousValue"/> are identical, then nothing will be done. Otherwise, the old index value will be deleted and the new value will be added</remarks>
		public bool Update(IFdbTransaction trans, TId id, TValue newValue, TValue previousValue)
		{
			if (!this.ValueComparer.Equals(newValue, previousValue))
			{
				// remove previous value
				if (this.IndexNullValues || previousValue != null)
				{
					trans.Clear(this.Subspace.Pack(previousValue, id));
				}

				// add new value
				if (this.IndexNullValues || newValue != null)
				{
					trans.Set(this.Subspace.Pack(newValue, id), Slice.Empty);
				}

				// cannot be both null, so we did at least something)
				return true;
			}
			return false;
		}

		/// <summary>Remove an entity from the index</summary>
		/// <param name="trans">Transaction to use</param>
		/// <param name="id">Id of the entity that has been deleted</param>
		/// <param name="value">Previous value of the entity in the index</param>
		public void Remove(IFdbTransaction trans, TId id, TValue value)
		{
			if (trans == null) throw new ArgumentNullException("trans");

			trans.Clear(this.Subspace.Create(value, id));
		}

		/// <summary>Returns a list of ids matching a specific value</summary>
		/// <param name="trans"></param>
		/// <param name="value">Value to lookup</param>
		/// <param name="reverse"></param>
		/// <param name="ct"></param>
		/// <returns>List of document ids matching this value for this particular index (can be empty if no document matches)</returns>
		public Task<List<TId>> LookupAsync(IFdbReadTransaction trans, TValue value, bool reverse = false, CancellationToken ct = default(CancellationToken))
		{
			if (ct.IsCancellationRequested) ct.ThrowIfCancellationRequested();

			var query = Lookup(trans, value, reverse);
			//TODO: limits? paging? ...
			return query.ToListAsync(ct);
		}

		/// <summary>Returns a query that will return all id of the entities that have the specified value in this index</summary>
		/// <param name="trans">Transaction to use</param>
		/// <param name="value">Value to lookup</param>
		/// <param name="reverse">If true, returns the results in reverse identifier order</param>
		/// <returns>Range query that returns all the ids of entities that match the value</returns>
		public IFdbAsyncEnumerable<TId> Lookup(IFdbReadTransaction trans, TValue value, bool reverse = false)
		{
			var prefix = this.Subspace.Pack(value);

			return trans
				.GetRangeStartsWith(prefix, new FdbRangeOptions { Reverse = reverse })
				.Keys((key) => this.Subspace.UnpackLast<TId>(key));
		}

		public IFdbAsyncEnumerable<TId> LookupGreaterThan(IFdbReadTransaction trans, TValue value, bool orEqual, bool reverse = false)
		{
			var space = this.Subspace.Tuple.ToSelectorPair();

			var prefix = this.Subspace.Pack(value);
			if (!orEqual) prefix = FdbKey.Increment(prefix);

			space = new FdbKeySelectorPair(
				FdbKeySelector.FirstGreaterThan(prefix),
				space.Stop
			);

			return trans
				.GetRange(space, new FdbRangeOptions { Reverse = reverse })
				.Keys((key) => this.Subspace.UnpackLast<TId>(key));
		}


		public IFdbAsyncEnumerable<TId> LookupLessThan(IFdbReadTransaction trans, TValue value, bool orEqual, bool reverse = false)
		{
			var space = this.Subspace.Tuple.ToSelectorPair();

			var prefix = this.Subspace.Pack(value);
			if (orEqual) prefix = FdbKey.Increment(prefix);

			space = new FdbKeySelectorPair(
				space.Start,
				FdbKeySelector.FirstGreaterThan(prefix)
			);

			return trans
				.GetRange(space, new FdbRangeOptions { Reverse = reverse })
				.Keys((key) => this.Subspace.UnpackLast<TId>(key));
		}

		public override string ToString()
		{
			return String.Format(CultureInfo.InvariantCulture, "Index['{0}']", this.Name);
		}

	}

}
