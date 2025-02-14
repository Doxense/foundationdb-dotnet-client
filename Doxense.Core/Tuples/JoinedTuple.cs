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

namespace Doxense.Collections.Tuples
{
	using System.Collections;
	using Doxense.Runtime.Converters;

	/// <summary>Tuple that represents the concatenation of two tuples</summary>
	[DebuggerDisplay("{ToString(),nq}")]
	[PublicAPI]
	public sealed class JoinedTuple : IVarTuple
	{
		// Uses cases: joining a 'subspace' tuple (customerId, 'Users', ) with a 'key' tuple (userId, 'Contacts', 123, )

		/// <summary>First tuple (first N items)</summary>
		public readonly IVarTuple Head;

		/// <summary>Second tuple (last M items)</summary>
		public readonly IVarTuple Tail;

		/// <summary>Offset at which the Tail tuple starts. Items are in Head tuple if index &lt; split. Items are in Tail tuple if index &gt;= split.</summary>
		private readonly int HeadCount;

		public JoinedTuple(IVarTuple head, IVarTuple tail)
		{
			Contract.NotNull(head);
			Contract.NotNull(tail);

			this.Head = head;
			this.Tail = tail;
			this.HeadCount = head.Count;
			this.Count = this.HeadCount + tail.Count;
		}

		public override string ToString()
		{
			return STuple.Formatter.ToString(this);
		}

		/// <inheritdoc />
		public int Count { get; }

		/// <inheritdoc />
		int System.Runtime.CompilerServices.ITuple.Length => this.Count;

		public object? this[int index]
		{
			get
			{
				int p = TupleHelpers.MapIndex(index, this.Count);
				return p < this.HeadCount ? this.Head[p] : this.Tail[p - this.HeadCount];
			}
		}

		public IVarTuple this[int? fromIncluded, int? toExcluded]
		{
			get
			{
				int begin = fromIncluded.HasValue ? TupleHelpers.MapIndexBounded(fromIncluded.Value, this.Count) : 0;
				int end = toExcluded.HasValue ? TupleHelpers.MapIndexBounded(toExcluded.Value, this.Count) : this.Count;

				if (end <= begin) return STuple.Empty;

				int p = this.Head.Count;
				if (begin >= p)
				{ // all selected items are in the tail
					return this.Tail[begin - p, end - p];
				}
				if (end <= p)
				{ // all selected items are in the head
					return this.Head[begin, end];
				}
				// selected items are both in head and tail
				return new JoinedTuple(this.Head[begin, null], this.Tail[null, end - p]);
			}
		}

		public object? this[Index index]
		{
			get
			{
				int p = TupleHelpers.MapIndex(index, this.Count);
				return p < this.HeadCount ? this.Head[p] : this.Tail[p - this.HeadCount];
			}
		}

		public IVarTuple this[Range range]
		{
			get
			{
				int lenHead = this.Head.Count;
				int lenTail = this.Tail.Count;
				(int offset, int count) = range.GetOffsetAndLength(lenHead + lenTail);
				if (count == 0) return STuple.Empty;
				if (offset == 0)
				{
					if (count == lenHead + lenTail) return this;
					if (count == lenHead) return this.Head;
				}
				if (offset == lenHead && count == lenTail)
				{
					return this.Tail;
				}
				return TupleHelpers.Splice(this, range);
			}
		}

		public T? Get<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T>(int index)
		{
			index = TupleHelpers.MapIndex(index, this.Count);
			return index < this.HeadCount ? this.Head.Get<T>(index) : this.Tail.Get<T>(index - this.HeadCount);
		}

		public T? GetFirst<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T>()
			=> this.HeadCount > 0 ? this.Head.GetFirst<T>() : this.Tail.GetFirst<T>();

		public T? GetLast<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T>()
			=> this.HeadCount >= this.Count ? this.Head.GetLast<T>() : this.Tail.GetLast<T>();

		IVarTuple IVarTuple.Append<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T>(T value)
			=> new LinkedTuple<T>(this, value);

		public LinkedTuple<T> Append<T>(T value)
			=> new(this, value);

		public IVarTuple Concat(IVarTuple tuple)
		{
			Contract.NotNull(tuple);

			int n1 = tuple.Count;
			if (n1 == 0) return this;

			int n2 = this.Count;

			if (n1 + n2 >= 10)
			{ // it's getting big, merge to a new List tuple
				return STuple.Concat(this.Head, this.Tail, tuple);
			}
			// REVIEW: should we always concat with the tail?
			return STuple.Concat(this.Head, this.Tail.Concat(tuple));
		}

		public void CopyTo(object?[] array, int offset)
		{
			this.Head.CopyTo(array, offset);
			this.Tail.CopyTo(array, offset + this.HeadCount);
		}

		public IEnumerator<object?> GetEnumerator()
		{
			foreach (var item in this.Head)
			{
				yield return item;
			}
			foreach (var item in this.Tail)
			{
				yield return item;
			}
		}

		System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
		{
			return this.GetEnumerator();
		}

		public override bool Equals(object? obj)
		{
			return obj != null && ((IStructuralEquatable)this).Equals(obj, SimilarValueComparer.Default);
		}

		public bool Equals(IVarTuple? other)
		{
			return !ReferenceEquals(other, null) && ((IStructuralEquatable) this).Equals(other, SimilarValueComparer.Default);
		}

		public override int GetHashCode()
		{
			return ((IStructuralEquatable)this).GetHashCode(SimilarValueComparer.Default);
		}

		bool System.Collections.IStructuralEquatable.Equals(object? other, System.Collections.IEqualityComparer comparer)
		{
			if (ReferenceEquals(this, other)) return true;
			if (other is null) return false;

			if (other is IVarTuple tuple)
			{
				if (tuple.Count != this.Count) return false;

				using (var iter = tuple.GetEnumerator())
				{
					foreach (var item in this.Head)
					{
						if (!iter.MoveNext() || !comparer.Equals(item, iter.Current)) return false;
					}
					foreach (var item in this.Tail)
					{
						if (!iter.MoveNext() || !comparer.Equals(item, iter.Current)) return false;
					}
					return !iter.MoveNext();
				}
			}

			return false;
		}

		int System.Collections.IStructuralEquatable.GetHashCode(System.Collections.IEqualityComparer comparer)
		{
			int tc = this.Tail.Count;
			return tc switch
			{
				0 => this.Head.GetHashCode(comparer),
				1 => this.HeadCount switch
				{
					0 => this.Tail.GetHashCode(comparer),
					1 => TupleHelpers.CombineHashCodes(this.Head.GetItemHashCode(0, comparer), this.Tail.GetItemHashCode(0, comparer)),
					_ => TupleHelpers.CombineHashCodes(this.Count, this.Head.GetItemHashCode(0, comparer), this.Tail.GetItemHashCode(tc - 2, comparer), this.Tail.GetItemHashCode(tc - 1, comparer))
				},
				_ => this.HeadCount switch
				{
					0 => this.Tail.GetHashCode(comparer),
					_ => TupleHelpers.CombineHashCodes(this.Count, this.Head.GetItemHashCode(0, comparer), this.Tail.GetItemHashCode(tc - 2, comparer), this.Tail.GetItemHashCode(tc - 1, comparer))
				}
			};
		}

		int IVarTuple.GetItemHashCode(int index, IEqualityComparer comparer)
		{
			if (index < this.HeadCount) return this.Head.GetItemHashCode(index, comparer);
			if (index < this.Count) return this.Tail.GetItemHashCode(index - this.HeadCount, comparer);
			throw new IndexOutOfRangeException();
		}


	}

}
