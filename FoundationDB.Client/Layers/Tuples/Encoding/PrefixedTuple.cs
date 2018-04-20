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

namespace FoundationDB.Layers.Tuples
{
	using System;
	using System.Collections;
	using System.Collections.Generic;
	using System.Diagnostics;
	using Doxense.Diagnostics.Contracts;
	using FoundationDB.Client;
	using FoundationDB.Client.Converters;
	using JetBrains.Annotations;

	/// <summary>Tuple that has a fixed abitrary binary prefix</summary>
	[DebuggerDisplay("{ToString()}")]
	public sealed class PrefixedTuple : ITuple
	{
		// Used in scenario where we will append keys to a common base tuple
		// note: linked list are not very efficient, but we do not expect a very long chain, and the head will usually be a subspace or memoized tuple

		private Slice m_prefix; //PERF: readonly struct
		private readonly ITuple m_items;

		internal PrefixedTuple(Slice prefix, ITuple items)
		{
			Contract.Requires(!prefix.IsNull && items != null);

			m_prefix = prefix;
			m_items = items;
		}

		/// <summary>Binary prefix to all the keys produced by this tuple</summary>
		public Slice Prefix
		{
			get { return m_prefix; }
		}

		public void PackTo(ref TupleWriter writer)
		{
			writer.Output.WriteBytes(m_prefix);
			m_items.PackTo(ref writer);
		}

		public Slice ToSlice()
		{
			var writer = new TupleWriter();
			PackTo(ref writer);
			return writer.Output.ToSlice();
		}

		public int Count
		{
			get { return m_items.Count; }
		}

		public object this[int index]
		{
			get { return m_items[index]; }
		}

		public ITuple this[int? fromIncluded, int? toExcluded]
		{
			get { return m_items[fromIncluded, toExcluded]; }
		}

		public R Get<R>(int index)
		{
			return m_items.Get<R>(index);
		}

		public R Last<R>()
		{
			return m_items.Last<R>();
		}

		ITuple ITuple.Append<R>(R value)
		{
			return this.Append<R>(value);
		}

		ITuple ITuple.Concat(ITuple tuple)
		{
			return this.Concat(tuple);
		}

		[NotNull]
		public PrefixedTuple Append<R>(R value)
		{
			return new PrefixedTuple(m_prefix, m_items.Append<R>(value));
		}

		[NotNull]
		public PrefixedTuple Concat([NotNull] ITuple tuple)
		{
			if (tuple == null) throw new ArgumentNullException("tuple");
			if (tuple.Count == 0) return this;

			return new PrefixedTuple(m_prefix, m_items.Concat(tuple));
		}

		public void CopyTo([NotNull] object[] array, int offset)
		{
			m_items.CopyTo(array, offset);
		}

		public IEnumerator<object> GetEnumerator()
		{
			return m_items.GetEnumerator();
		}

		System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
		{
			return this.GetEnumerator();
		}

		public override string ToString()
		{
			//TODO: should we add the prefix to the string representation ?
			// => something like "<prefix>(123, 'abc', true)"
			return STuple.ToString(this);
		}

		public override bool Equals(object obj)
		{
			return obj != null && ((IStructuralEquatable)this).Equals(obj, SimilarValueComparer.Default);
		}

		public bool Equals(ITuple other)
		{
			return !object.ReferenceEquals(other, null) && ((IStructuralEquatable)this).Equals(other, SimilarValueComparer.Default);
		}

		public override int GetHashCode()
		{
			return ((IStructuralEquatable)this).GetHashCode(SimilarValueComparer.Default);
		}

		bool System.Collections.IStructuralEquatable.Equals(object other, System.Collections.IEqualityComparer comparer)
		{
			if (object.ReferenceEquals(this, other)) return true;
			if (other == null) return false;

			var linked = other as PrefixedTuple;
			if (!object.ReferenceEquals(linked, null))
			{
				// Should all of these tuples be considered equal ?
				// * Head=(A,B) + Tail=(C,)
				// * Head=(A,) + Tail=(B,C,)
				// * Head=() + Tail=(A,B,C,)

				// first check the subspaces
				if (!linked.m_prefix.Equals(m_prefix))
				{ // they have a different prefix
					return false;
				}

				if (m_items.Count != linked.m_items.Count)
				{ // there's no way they would be equal
					return false;
				}

				return comparer.Equals(m_items, linked.m_items);
			}

			return STuple.Equals(this, other, comparer);
		}

		int IStructuralEquatable.GetHashCode(System.Collections.IEqualityComparer comparer)
		{
			return STuple.CombineHashCodes(
				m_prefix.GetHashCode(),
				comparer.GetHashCode(m_items)
			);
		}

	}
}
