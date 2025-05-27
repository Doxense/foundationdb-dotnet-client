#region Copyright (c) 2023-2025 SnowBank SAS, (c) 2005-2023 Doxense SAS
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

namespace Doxense.Collections.Tuples
{
	using System.Collections;
	using SnowBank.Runtime.Converters;

	/// <summary>Tuple that adds a value at the end of an already existing tuple</summary>
	/// <typeparam name="T">Type of the last value of the tuple</typeparam>
	[DebuggerDisplay("{ToString(),nq}")]
	[PublicAPI]
	public sealed class LinkedTuple<T> : IVarTuple
	{
		//TODO: consider changing this to a struct ?

		// Used in scenario where we will append keys to a common base tuple
		// note: linked list are not very efficient, but we do not expect a very long chain, and the head will usually be a subspace or memoized tuple

		/// <summary>Value of the last element of the tuple</summary>
		public readonly T Tail;

		/// <summary>Link to the parent tuple that contains the head.</summary>
		public readonly IVarTuple Head;

		/// <summary>Cached size of the size of the Head tuple. Add 1 to get the size of this tuple.</summary>
		private readonly int HeadCount;

		/// <summary>Append a new value at the end of an existing tuple</summary>
		public LinkedTuple(IVarTuple head, T tail)
		{
			Contract.NotNull(head);

			this.Head = head;
			this.Tail = tail;
			this.HeadCount = head.Count;
		}

		/// <summary>Returns the number of elements in this tuple</summary>
		public int Count => this.HeadCount + 1;

		/// <inheritdoc />
		int System.Runtime.CompilerServices.ITuple.Length => this.Count;

		public object? this[int index]
		{
			get
			{
				if (index == this.HeadCount || index == -1) return this.Tail;
				if (index < -1) index++;
				return this.Head[index];
			}
		}

		public IVarTuple this[int? fromIncluded, int? toExcluded] => TupleHelpers.Splice(this, fromIncluded, toExcluded);

		public object? this[Index index]
		{
			get
			{
				int p = TupleHelpers.MapIndex(index, this.HeadCount + 1);
				if (p == this.HeadCount) return this.Tail;
				return this.Head[p];
			}
		}

		public IVarTuple this[Range range]
		{
			get
			{
				int d = this.HeadCount;
				(int offset, int count) = range.GetOffsetAndLength(d + 1);
				if (count == 0) return STuple.Empty;
				if (count == 1 && offset == d) return new STuple<T>(this.Tail);
				if (offset == 0)
				{
					if (count == d + 1) return this;
					if (count == d) return this.Head;
				}
				return TupleHelpers.Splice(this, range);
			}
		}

		public TItem? Get<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TItem>(int index)
		{
			if (index == this.HeadCount || index == -1) return TypeConverters.Convert<T, TItem?>(this.Tail);
			if (index < -1) index++;
			return this.Head.Get<TItem>(index);
		}

		public TItem? GetFirst<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TItem>()
			=> this.HeadCount > 0 ? this.Head.GetFirst<TItem>() : TypeConverters.Convert<T, TItem?>(this.Tail);

		public TItem? GetLast<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TItem>()
			=> TypeConverters.Convert<T, TItem?>(this.Tail);

		public T Last
		{
			[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
			[return: MaybeNull]
			get => this.Tail;
		}

		public IVarTuple Append<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TItem>(TItem value)
		{
			return new JoinedTuple(this.Head, new STuple<T, TItem>(this.Tail, value));
		}

		public IVarTuple Concat(IVarTuple tuple)
		{
			return STuple.Concat(this, tuple);
		}

		public void CopyTo(object?[] array, int offset)
		{
			this.Head.CopyTo(array, offset);
			array[offset + this.HeadCount] = this.Tail;
		}

		public IEnumerator<object?> GetEnumerator()
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
			return STuple.Formatter.ToString(this);
		}

		public override bool Equals(object? obj)
		{
			return obj != null && ((System.Collections.IStructuralEquatable) this).Equals(obj, SimilarValueComparer.Default);
		}

		public bool Equals(IVarTuple? other)
		{
			return !ReferenceEquals(other, null) && ((System.Collections.IStructuralEquatable) this).Equals(other, SimilarValueComparer.Default);
		}

		public override int GetHashCode()
		{
			return ((System.Collections.IStructuralEquatable) this).GetHashCode(SimilarValueComparer.Default);
		}

		bool System.Collections.IStructuralEquatable.Equals(object? other, System.Collections.IEqualityComparer comparer)
		{
			if (ReferenceEquals(this, other)) return true;
			if (other == null) return false;

			if (other is LinkedTuple<T> linked)
			{
				// must have same length
				if (linked.Count != this.Count) return false;
				// compare the tail before
				if (!comparer.Equals(this.Tail, linked.Tail)) return false;
				// compare the rest
				return this.Head.Equals(linked.Tail, comparer);
			}

			return TupleHelpers.Equals(this, other, comparer);
		}

		int System.Collections.IStructuralEquatable.GetHashCode(System.Collections.IEqualityComparer comparer)
		{
			int hc = this.Head.Count;
			return hc switch
			{
				0 => TupleHelpers.ComputeHashCode(this.Tail, comparer),
				1 => TupleHelpers.CombineHashCodes(this.Head.GetItemHashCode(0, comparer), TupleHelpers.ComputeHashCode(this.Tail, comparer)),
				_ => TupleHelpers.CombineHashCodes(this.Count, this.Head.GetItemHashCode(0, comparer), this.Head.GetItemHashCode(hc - 1, comparer), TupleHelpers.ComputeHashCode(this.Tail, comparer))
			};
		}

		int IVarTuple.GetItemHashCode(int index, IEqualityComparer comparer)
		{
			int hc = this.Head.Count;
			if (index < hc) return this.Head.GetItemHashCode(index, comparer);
			if (index == hc) return TupleHelpers.ComputeHashCode(this.Tail, comparer);
			throw new IndexOutOfRangeException();
		}

	}

}
