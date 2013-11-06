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

	/// <summary>Tuple that adds a value at the end of an already existing tuple</summary>
	/// <typeparam name="T">Type of the last value of the tuple</typeparam>
	[DebuggerDisplay("{ToString()}")]
	public sealed class FdbLinkedTuple<T> : IFdbTuple
	{
		// Used in scenario where we will append keys to a common base tuple
		// note: linked list are not very efficient, but we do not expect a very long chain, and the head will usually be a subspace or memoized tuple

		/// <summary>Value of the last element of the tuple</summary>
		public readonly T Tail;

		/// <summary>Link to the parent tuple that contains the head.</summary>
		public readonly IFdbTuple Head;

		/// <summary>Cached size of the size of the Head tuple. Add 1 to get the size of this tuple.</summary>
		public readonly int Depth;

		/// <summary>Append a new value at the end of an existing tuple</summary>
		internal FdbLinkedTuple(IFdbTuple head, T tail)
		{
			Contract.Requires(head != null);

			this.Head = head;
			this.Tail = tail;
			this.Depth = head.Count;
		}

		/// <summary>Pack this tuple into a buffer</summary>
		public void PackTo(ref SliceWriter writer)
		{
			this.Head.PackTo(ref writer);
			FdbTuplePacker<T>.SerializeTo(ref writer, this.Tail);
		}

		/// <summary>Pack this tuple into a slice</summary>
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

		/// <summary>Returns the number of elements in this tuple</summary>
		public int Count
		{
			get { return this.Depth + 1; }
		}

		public object this[int index]
		{
			get
			{
				if (index == this.Depth || index == -1) return this.Tail;
				if (index < -1) index++;
				return this.Head[index];
			}
		}

		public IFdbTuple this[int? from, int? to]
		{
			get { return FdbTuple.Splice(this, from, to); }
		}

		public R Get<R>(int index)
		{
			if (index == this.Depth || index == -1) return FdbConverters.Convert<T, R>(this.Tail);
			if (index < -1) index++;
			return this.Head.Get<R>(index);
		}

		public R Last<R>()
		{
			return FdbConverters.Convert<T, R>(this.Tail);
		}

		IFdbTuple IFdbTuple.Append<R>(R value)
		{
			return this.Append<R>(value);
		}

		public FdbLinkedTuple<R> Append<R>(R value)
		{
			return new FdbLinkedTuple<R>(this, value);
		}

		public void CopyTo(object[] array, int offset)
		{
			this.Head.CopyTo(array, offset);
			array[offset + this.Depth] = this.Tail;
		}

		public IEnumerator<object> GetEnumerator()
		{
			foreach (var item in this.Head)
			{
				yield return item;
			}
			yield return this.Tail;
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

			var linked = other as FdbLinkedTuple<T>;
			if (!object.ReferenceEquals(linked, null))
			{
				// must have same length
				if (linked.Count != this.Count) return false;
				// compare the tail before
				if (!comparer.Equals(this.Tail, linked.Tail)) return false;
				// compare the rest
				return this.Head.Equals(linked.Tail, comparer);
			}

			return FdbTuple.Equals(this, other, comparer);
		}

		int IStructuralEquatable.GetHashCode(System.Collections.IEqualityComparer comparer)
		{
			return FdbTuple.CombineHashCodes(
				this.Head != null ? this.Head.GetHashCode(comparer) : 0,
				comparer.GetHashCode(this.Tail)
			);
		}

	}
}
