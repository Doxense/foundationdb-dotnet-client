#region Copyright (c) 2005-2023 Doxense SAS
// All rights reserved.
// 
// Redistribution and use in source and binary forms, with or without
// modification, are permitted provided that the following conditions are met:
// 	* Redistributions of source code must retain the above copyright
// 	  notice, this list of conditions and the following disclaimer.
// 	* Redistributions in binary form must reproduce the above copyright
// 	  notice, this list of conditions and the following disclaimer in the
// 	  documentation and/or other materials provided with the distribution.
// 	* Neither the name of Doxense nor the
// 	  names of its contributors may be used to endorse or promote products
// 	  derived from this software without specific prior written permission.
// 
// THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND
// ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
// WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
// DISCLAIMED. IN NO EVENT SHALL DOXENSE BE LIABLE FOR ANY
// DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
// (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
// LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
// ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
// (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
// SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
#endregion

#if !USE_SHARED_FRAMEWORK

namespace Doxense.Collections.Tuples
{
	using System;
	using System.Collections;
	using System.Collections.Generic;
	using System.ComponentModel;
	using System.Diagnostics;
	using System.Runtime.CompilerServices;
	using Doxense.Collections.Tuples.Encoding;
	using Doxense.Diagnostics.Contracts;
	using Doxense.Runtime.Converters;
	using JetBrains.Annotations;

	/// <summary>Tuple that holds only one item</summary>
	/// <typeparam name="T1">Type of the item</typeparam>
	[ImmutableObject(true), DebuggerDisplay("{ToString(),nq}")]
	[PublicAPI]
	public readonly struct STuple<T1> : IVarTuple, IEquatable<STuple<T1>>, IEquatable<ValueTuple<T1>>, ITupleSerializable
	{
		// This is mostly used by code that create a lot of temporary singleton, to reduce the pressure on the Garbage Collector by allocating them on the stack.
		// Please note that if you return an STuple<T> as an ITuple, it will be boxed by the CLR and all memory gains will be lost

		/// <summary>First and only item in the tuple</summary>
		public readonly T1? Item1;

		[DebuggerStepThrough]
		public STuple(T1? item1)
		{
			this.Item1 = item1;
		}

		public int Count => 1;

		object? IReadOnlyList<object?>.this[int index] => ((IVarTuple) this)[index];

		object? IVarTuple.this[int index]
		{
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get
			{
				if (index > 0 || index < -1) return TupleHelpers.FailIndexOutOfRange<object>(index, 1);
				return this.Item1;
			}
		}

		public IVarTuple this[int? fromIncluded, int? toExcluded] => TupleHelpers.Splice(this, fromIncluded, toExcluded);

#if USE_RANGE_API

		object? IVarTuple.this[Index index] => index.GetOffset(1) switch
		{
			0 => this.Item1,
			_ => TupleHelpers.FailIndexOutOfRange<object>(index.Value, 1)
		};

		public IVarTuple this[Range range]
		{
			get
			{
				(_, int count) = range.GetOffsetAndLength(1);
				return count == 0 ? STuple.Empty : this;
			}
		}

#endif

		/// <summary>Return the typed value of an item of the tuple, given its position</summary>
		/// <typeparam name="TItem">Expected type of the item</typeparam>
		/// <param name="index">Position of the item (if negative, means relative from the end)</param>
		/// <returns>Value of the item at position <paramref name="index"/>, adapted into type <typeparamref name="TItem"/>.</returns>
		public TItem? Get<TItem>(int index)
		{
			if (index > 0 || index < -1) return TupleHelpers.FailIndexOutOfRange<TItem>(index, 1);
			return TypeConverters.Convert<T1, TItem>(this.Item1);
		}

		IVarTuple IVarTuple.Append<T2>(T2? value) where T2 : default
		{
			return new STuple<T1, T2>(this.Item1, value);
		}

		/// <summary>Appends a single new item at the end of the current tuple.</summary>
		/// <param name="value">Value that will be added as an embedded item</param>
		/// <returns>New tuple with one extra item</returns>
		/// <remarks>If <paramref name="value"/> is a tuple, and you want to append the *items* of this tuple, and not the tuple itself, please call <see cref="Concat"/>!</remarks>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public STuple<T1, T2> Append<T2>(T2? value)
		{
			return new STuple<T1, T2>(this.Item1, value);
		}

		/// <summary>Appends two new items at the end of the current tuple.</summary>
		/// <param name="value1">Value that will be added as an embedded item</param>
		/// <param name="value2">Value that will be added as an embedded item</param>
		/// <returns>New tuple with one extra item</returns>
		/// <remarks>If any of <paramref name="value1"/> or <paramref name="value2"/> is a tuple, and you want to append the *items* of this tuple, and not the tuple itself, please call <see cref="Concat"/>!</remarks>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public STuple<T1, T2, T3> Append<T2, T3>(T2? value1, T3? value2)
		{
			return new STuple<T1, T2, T3>(this.Item1, value1, value2);
		}

		/// <summary>Appends the items of a tuple at the end of the current tuple.</summary>
		/// <param name="tuple">Tuple whose items are to be appended at the end</param>
		/// <returns>New tuple composed of the current tuple items, followed by <paramref name="tuple"/>'s items</returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public IVarTuple Concat(IVarTuple tuple)
		{
			return STuple.Concat(this, tuple);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public STuple<T1, T2> Concat<T2>(STuple<T2> tuple)
		{
			return new STuple<T1, T2>(this.Item1, tuple.Item1);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public STuple<T1, T2, T3> Concat<T2, T3>(STuple<T2, T3> tuple)
		{
			return new STuple<T1, T2, T3>(this.Item1, tuple.Item1, tuple.Item2);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public STuple<T1, T2, T3, T4> Concat<T2, T3, T4>(STuple<T2, T3, T4> tuple)
		{
			return new STuple<T1, T2, T3, T4>(this.Item1, tuple.Item1, tuple.Item2, tuple.Item3);
		}

		/// <summary>Copy the item of this singleton into an array at the specified offset</summary>
		public void CopyTo(object?[] array, int offset)
		{
			array[offset] = this.Item1;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void Deconstruct(out T1? item1)
		{
			item1 = this.Item1;
		}

		/// <summary>Execute a lambda Action with the content of this tuple</summary>
		/// <param name="lambda">Action that will be passed the content of this tuple as parameters</param>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void With(Action<T1?> lambda)
		{
			lambda(this.Item1);
		}

		/// <summary>Execute a lambda Function with the content of this tuple</summary>
		/// <param name="lambda">Action that will be passed the content of this tuple as parameters</param>
		/// <returns>Result of calling <paramref name="lambda"/> with the items of this tuple</returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public TItem With<TItem>(Func<T1?, TItem> lambda)
		{
			return lambda(this.Item1);
		}

		void ITupleSerializable.PackTo(ref TupleWriter writer)
		{
			TuplePackers.SerializeTo<T1>(ref writer, this.Item1);
		}

		public IEnumerator<object?> GetEnumerator()
		{
			yield return this.Item1;
		}

		System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
		{
			return GetEnumerator();
		}

		public override string ToString()
		{
			// singleton tuples end with a trailing ','
			return "(" + STuple.Formatter.Stringify(this.Item1) + ",)";
		}

		public override bool Equals(object? obj)
		{
			return obj != null && ((IStructuralEquatable)this).Equals(obj, SimilarValueComparer.Default);
		}

		public bool Equals(IVarTuple? other)
		{
			return other != null && ((IStructuralEquatable)this).Equals(other, SimilarValueComparer.Default);
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool Equals(STuple<T1> other)
		{
			return SimilarValueComparer.Default.Equals(this.Item1, other.Item1);
		}

		public override int GetHashCode()
		{
			return ((IStructuralEquatable)this).GetHashCode(SimilarValueComparer.Default);
		}

		public static bool operator ==(STuple<T1> left, STuple<T1> right)
		{
			return SimilarValueComparer.Default.Equals(left.Item1, right.Item1);
		}

		public static bool operator !=(STuple<T1> left, STuple<T1> right)
		{
			return !SimilarValueComparer.Default.Equals(left.Item1, right.Item1);
		}

		bool IStructuralEquatable.Equals(object? other, IEqualityComparer comparer)
		{
			if (other == null) return false;
			if (other is STuple<T1> stuple)
			{
				return comparer.Equals(this.Item1, stuple.Item1);
			}
			if (other is ValueTuple<T1> vtuple)
			{
				return comparer.Equals(this.Item1, vtuple.Item1);
			}
			return TupleHelpers.Equals(this, other, comparer);
		}

		int IStructuralEquatable.GetHashCode(IEqualityComparer comparer)
		{
			return comparer.GetHashCode(this.Item1);
		}

		[Pure]
		public static implicit operator STuple<T1>(Tuple<T1> t)
		{
			Contract.NotNull(t);
			return new STuple<T1>(t.Item1);
		}

		[Pure]
		public static explicit operator Tuple<T1>(STuple<T1> t)
		{
			return new Tuple<T1>(t.Item1);
		}

		public void Fill(ref ValueTuple<T1> t)
		{
			t.Item1 = this.Item1;
		}

		/// <summary>Appends the items of a tuple at the end of the current tuple.</summary>
		/// <param name="tuple">Tuple whose items are to be appended at the end</param>
		/// <returns>New tuple composed of the current tuple items, followed by <paramref name="tuple"/>'s items</returns>
		[Pure]
		public STuple<T1, T2> Concat<T2>(ValueTuple<T2> tuple)
		{
			return new STuple<T1, T2>(this.Item1, tuple.Item1);
		}

		/// <summary>Appends the items of a tuple at the end of the current tuple.</summary>
		/// <param name="tuple">Tuple whose items are to be appended at the end</param>
		/// <returns>New tuple composed of the current tuple items, followed by <paramref name="tuple"/>'s items</returns>
		[Pure]
		public STuple<T1, T2, T3> Concat<T2, T3>((T2, T3) tuple)
		{
			return new STuple<T1, T2, T3>(this.Item1, tuple.Item1, tuple.Item2);
		}

		/// <summary>Appends the items of a tuple at the end of the current tuple.</summary>
		/// <param name="tuple">Tuple whose items are to be appended at the end</param>
		/// <returns>New tuple composed of the current tuple items, followed by <paramref name="tuple"/>'s items</returns>
		[Pure]
		public STuple<T1, T2, T3, T4> Concat<T2, T3, T4>((T2, T3, T4) tuple)
		{
			return new STuple<T1, T2, T3, T4>(this.Item1, tuple.Item1, tuple.Item2, tuple.Item3);
		}

		/// <summary>Appends the items of a tuple at the end of the current tuple.</summary>
		/// <param name="tuple">Tuple whose items are to be appended at the end</param>
		/// <returns>New tuple composed of the current tuple items, followed by <paramref name="tuple"/>'s items</returns>
		[Pure]
		public STuple<T1, T2, T3, T4, T5> Concat<T2, T3, T4, T5>((T2, T3, T4, T5) tuple)
		{
			return new STuple<T1, T2, T3, T4, T5>(this.Item1, tuple.Item1, tuple.Item2, tuple.Item3, tuple.Item4);
		}

		/// <summary>Appends the items of a tuple at the end of the current tuple.</summary>
		/// <param name="tuple">Tuple whose items are to be appended at the end</param>
		/// <returns>New tuple composed of the current tuple items, followed by <paramref name="tuple"/>'s items</returns>
		[Pure]
		public STuple<T1, T2, T3, T4, T5, T6> Concat<T2, T3, T4, T5, T6>((T2, T3, T4, T5, T6) tuple)
		{
			return new STuple<T1, T2, T3, T4, T5, T6>(this.Item1, tuple.Item1, tuple.Item2, tuple.Item3, tuple.Item4, tuple.Item5);
		}

		[Pure]
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public ValueTuple<T1> ToValueTuple()
		{
			return new ValueTuple<T1>(this.Item1);
		}

		[Pure]
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static implicit operator STuple<T1>(ValueTuple<T1> t)
		{
			return new STuple<T1>(t.Item1);
		}

		[Pure]
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static implicit operator ValueTuple<T1>(STuple<T1> t)
		{
			return new ValueTuple<T1>(t.Item1);
		}

		[Pure]
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		bool IEquatable<ValueTuple<T1>>.Equals(ValueTuple<T1> other)
		{
			return SimilarValueComparer.Default.Equals(this.Item1, other.Item1);
		}

		public static bool operator ==(STuple<T1> left, ValueTuple<T1> right)
		{
			return SimilarValueComparer.Default.Equals(left.Item1, right.Item1);
		}

		public static bool operator ==(ValueTuple<T1> left, STuple<T1> right)
		{
			return SimilarValueComparer.Default.Equals(left.Item1, right.Item1);
		}

		public static bool operator !=(STuple<T1> left, ValueTuple<T1> right)
		{
			return !SimilarValueComparer.Default.Equals(left.Item1, right.Item1);
		}

		public static bool operator !=(ValueTuple<T1> left, STuple<T1> right)
		{
			return !SimilarValueComparer.Default.Equals(left.Item1, right.Item1);
		}

		public sealed class Comparer : IComparer<STuple<T1>>
		{
			public static Comparer Default { get; } = new Comparer();

			private static readonly Comparer<T1> Comparer1 = Comparer<T1>.Default;

			private Comparer() { }

			public int Compare(STuple<T1> x, STuple<T1> y)
			{
				return Comparer1.Compare(x.Item1, y.Item1);
			}
		}

		public sealed class EqualityComparer : IEqualityComparer<STuple<T1>>
		{
			public static EqualityComparer Default { get; } = new EqualityComparer();

			private static readonly EqualityComparer<T1> Comparer1 = EqualityComparer<T1>.Default;

			private EqualityComparer() { }

			public bool Equals(STuple<T1> x, STuple<T1> y)
			{
				return Comparer1.Equals(x.Item1, y.Item1);
			}

			public int GetHashCode(STuple<T1> obj)
			{
				return Comparer1.GetHashCode(obj.Item1);
			}

		}

	}

}

#endif
