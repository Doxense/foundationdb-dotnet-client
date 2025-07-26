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

namespace SnowBank.Data.Tuples.Binary
{
	using System.Collections;
	using SnowBank.Buffers;
	using SnowBank.Buffers.Text;
	using SnowBank.Runtime.Converters;

	/// <summary>Tuple that has a fixed arbitrary binary prefix</summary>
	[DebuggerDisplay("{ToString(),nq}")]
	[PublicAPI]
	public sealed class PrefixedTuple : IVarTuple, IComparable, ITupleSpanPackable, ITupleFormattable
	{
		// Used in scenario where we will append keys to a common base tuple
		// note: linked list are not very efficient, but we do not expect a very long chain, and the head will usually be a subspace or memoized tuple

		private readonly Slice m_prefix;
		private readonly IVarTuple m_items;

		public PrefixedTuple(Slice prefix, IVarTuple items)
		{
			Contract.Debug.Requires(!prefix.IsNull && items != null);

			m_prefix = prefix;
			m_items = items;
		}

		/// <summary>Binary prefix to all the keys produced by this tuple</summary>
		public Slice Prefix => m_prefix;

		/// <inheritdoc />
		void ITuplePackable.PackTo(TupleWriter writer)
		{
			PackTo(writer);
		}

		/// <inheritdoc />
		bool ITupleSpanPackable.TryPackTo(ref TupleSpanWriter writer)
		{
			return TryPackTo(ref writer);
		}

		/// <inheritdoc />
		bool ITupleSpanPackable.TryGetSizeHint(bool embedded, out int sizeHint)
		{
			if (m_items is ITupleSpanPackable tsp && tsp.TryGetSizeHint(embedded, out var size))
			{
				sizeHint = checked(size + m_prefix.Count);
				return true;
			}

			sizeHint = 0;
			return false;
		}

		/// <inheritdoc />
		int ITupleFormattable.AppendItemsTo(ref FastStringBuilder sb)
		{
			return STuple.Formatter.AppendItemsTo(ref sb, this);
		}

		internal void PackTo(TupleWriter writer)
		{
			writer.Output.WriteBytes(m_prefix);
			TupleEncoder.WriteTo(writer, m_items);
		}

		internal bool TryPackTo(ref TupleSpanWriter writer)
		{
			return writer.TryWriteLiteral(m_prefix.Span)
				&& TupleEncoder.TryWriteTo(ref writer, m_items);
		}

		public Slice ToSlice()
		{
			var sw = new SliceWriter();
			var writer = new TupleWriter(ref sw);
			PackTo(writer);
			return sw.ToSlice();
		}

		/// <inheritdoc />
		public int Count => m_items.Count;

		/// <inheritdoc />
		int System.Runtime.CompilerServices.ITuple.Length => this.Count;

		public object? this[int index] => m_items[index];

		public IVarTuple this[int? fromIncluded, int? toExcluded] => m_items[fromIncluded, toExcluded];
		//REVIEW: should we allow this? this silently drops the prefix from the result...

		public object? this[Index index] => m_items[index];

		public IVarTuple this[Range range] => m_items[range];
		//REVIEW: should we allow this? this silently drops the prefix from the result...

		public T? Get<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T>(int index)
			=> m_items.Get<T>(index);

		public T? GetFirst<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T>()
			=> m_items.GetFirst<T>();

		public T? GetLast<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T>()
			=> m_items.GetLast<T>();

		IVarTuple IVarTuple.Append<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T>(T value)
			where T : default => Append<T>(value);

		IVarTuple IVarTuple.Concat(IVarTuple tuple)
		{
			return Concat(tuple);
		}

		public PrefixedTuple Append<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T>(T value)
		{
			return new PrefixedTuple(m_prefix, m_items.Append<T>(value));
		}

		[Pure]
		public PrefixedTuple Concat(IVarTuple tuple)
		{
			Contract.NotNull(tuple);
			if (tuple.Count == 0) return this;

			return new PrefixedTuple(m_prefix, m_items.Concat(tuple));
		}

		public void CopyTo(object?[] array, int offset)
		{
			m_items.CopyTo(array, offset);
		}

		public IEnumerator<object?> GetEnumerator()
		{
			return m_items.GetEnumerator();
		}

		System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
		{
			return this.GetEnumerator();
		}

		/// <summary>Returns a human-readable representation of this tuple</summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public override string ToString() => ToString(null);

		/// <summary>Returns a human-readable representation of this tuple</summary>
		[Pure]
		public string ToString(string? format, IFormatProvider? provider = null)
		{
			//TODO: should we add the prefix to the string representation ?
			// => something like "<prefix>(123, 'abc', true)"
			return STuple.Formatter.ToString(this);
		}

		public override bool Equals(object? obj)
		{
			return obj != null && ((IStructuralEquatable)this).Equals(obj, SimilarValueComparer.Default);
		}

		public bool Equals(IVarTuple? other)
		{
			return !object.ReferenceEquals(other, null) && ((IStructuralEquatable)this).Equals(other, SimilarValueComparer.Default);
		}

		public override int GetHashCode()
		{
			return ((IStructuralEquatable)this).GetHashCode(SimilarValueComparer.Default);
		}

		public int CompareTo(IVarTuple? other) => TupleHelpers.Compare(this, other, SimilarValueComparer.Default);

		public int CompareTo(object? other) => TupleHelpers.Compare(this, other, SimilarValueComparer.Default);

		int IStructuralComparable.CompareTo(object? other, IComparer comparer) => TupleHelpers.Compare(this, other, comparer);

		bool IStructuralEquatable.Equals(object? other, IEqualityComparer comparer)
		{
			if (object.ReferenceEquals(this, other)) return true;
			if (other == null) return false;

			if (other is PrefixedTuple linked)
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

			return TupleHelpers.Equals(this, other, comparer);
		}

		int IStructuralEquatable.GetHashCode(IEqualityComparer comparer)
		{
			return HashCode.Combine(
				m_prefix.GetHashCode(),
				comparer.GetHashCode(m_items)
			);
		}

		int IVarTuple.GetItemHashCode(int index, IEqualityComparer comparer)
		{
			return m_items.GetItemHashCode(index, comparer);
		}

	}
}
