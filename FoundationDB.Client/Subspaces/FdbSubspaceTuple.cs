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

namespace FoundationDB.Layers.Tuples
{
	using FoundationDB.Client;
	using FoundationDB.Client.Converters;
	using FoundationDB.Client.Utils;
	using System.Collections;
	using System.Collections.Generic;
	using System.Diagnostics;

	/// <summary>Tuple that is rooted under an existing subspace (that can have an arbitrary binary prefix)</summary>
	[DebuggerDisplay("{ToString()}")]
	public sealed class FdbSubspaceTuple : IFdbTuple
	{
		// Used in scenario where we will append keys to a common base tuple
		// note: linked list are not very efficient, but we do not expect a very long chain, and the head will usually be a subspace or memoized tuple

		private readonly FdbSubspace m_subspace;
		private readonly IFdbTuple m_items;

		internal FdbSubspaceTuple(FdbSubspace subspace, IFdbTuple items)
		{
			Contract.Requires(subspace != null);
			Contract.Requires(items != null);

			m_subspace = subspace;
			m_items = items;
		}

		/// <summary>Parent subspace that created this tuple</summary>
		public FdbSubspace Subspace { get { return m_subspace; } }

		public void PackTo(ref SliceWriter writer)
		{
			writer.WriteBytes(this.m_subspace.Key);
			this.m_items.PackTo(ref writer);
		}

		public Slice ToSlice()
		{
			var writer = SliceWriter.Empty;
			PackTo(ref writer);
			return writer.ToSlice();
		}

		Slice IFdbKey.ToFoundationDbKey()
		{
			return this.ToSlice();
		}

		public int Count
		{
			get { return this.m_items.Count; }
		}

		public object this[int index]
		{
			get
			{
				return this.m_items[index];
			}
		}

		public IFdbTuple this[int? from, int? to]
		{
			get { return this.m_items[from, to]; }
		}

		public R Get<R>(int index)
		{
			return this.m_items.Get<R>(index);
		}

		public R Last<R>()
		{
			return this.m_items.Last<R>();
		}

		IFdbTuple IFdbTuple.Append<R>(R value)
		{
			return this.Append<R>(value);
		}

		public FdbSubspaceTuple Append<R>(R value)
		{
			return new FdbSubspaceTuple(this.m_subspace, this.m_items.Append<R>(value));
		}

		public void CopyTo(object[] array, int offset)
		{
			this.m_items.CopyTo(array, offset);
		}

		public IEnumerator<object> GetEnumerator()
		{
			return this.m_items.GetEnumerator();
		}

		System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
		{
			return this.GetEnumerator();
		}

		public override string ToString()
		{
			return FdbTuple.ToString(this);
		}

		public override bool Equals(object obj)
		{
			return obj != null && ((IStructuralEquatable)this).Equals(obj, SimilarValueComparer.Default);
		}

		public bool Equals(IFdbTuple other)
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

			var linked = other as FdbSubspaceTuple;
			if (!object.ReferenceEquals(linked, null))
			{
				// Should all of these tuples be considered equal ?
				// * Head=(A,B) + Tail=(C,)
				// * Head=(A,) + Tail=(B,C,)
				// * Head=() + Tail=(A,B,C,)

				// first check the subspaces
				if (!object.ReferenceEquals(linked.m_subspace, this.m_subspace) && linked.m_subspace.Key != this.m_subspace.Key)
				{ // they are from different tuples
					return false;
				}

				if (this.m_items.Count != linked.m_items.Count)
				{ // there's no way they would be equal
					return false;
				}

				return comparer.Equals(this.m_items, linked.m_items);
			}

			return FdbTuple.Equals(this, other, comparer);
		}

		int IStructuralEquatable.GetHashCode(System.Collections.IEqualityComparer comparer)
		{
			return FdbTuple.CombineHashCodes(
				this.m_subspace.GetHashCode(),
				comparer.GetHashCode(this.m_items)
			);
		}

	}
}
