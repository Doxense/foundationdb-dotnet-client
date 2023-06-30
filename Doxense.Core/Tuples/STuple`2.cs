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
	using System.Diagnostics.CodeAnalysis;
	using System.Runtime.CompilerServices;
	using Doxense.Collections.Tuples.Encoding;
	using Doxense.Diagnostics.Contracts;
	using Doxense.Runtime.Converters;
	using JetBrains.Annotations;

	/// <summary>Tuple that holds a pair of items</summary>
	/// <typeparam name="T1">Type of the first item</typeparam>
	/// <typeparam name="T2">Type of the second item</typeparam>
	[ImmutableObject(true), DebuggerDisplay("{ToString(),nq}")]
	[PublicAPI]
	public readonly struct STuple<T1, T2> : IVarTuple, IEquatable<STuple<T1, T2>>, IEquatable<(T1, T2)>, ITupleSerializable
	{
		// This is mostly used by code that create a lot of temporary pair, to reduce the pressure on the Garbage Collector by allocating them on the stack.
		// Please note that if you return an STuple<T> as an ITuple, it will be boxed by the CLR and all memory gains will be lost

		/// <summary>First element of the pair</summary>
		public readonly T1? Item1;

		/// <summary>Second element of the pair</summary>
		public readonly T2? Item2;

		[DebuggerStepThrough]
		public STuple(T1? item1, T2? item2)
		{
			this.Item1 = item1;
			this.Item2 = item2;
		}

		public int Count => 2;

		object? IReadOnlyList<object?>.this[int index] => ((IVarTuple) this)[index];

		object? IVarTuple.this[int index]
		{
			get
			{
				switch (index)
				{
					case 0: case -2: return this.Item1;
					case 1: case -1: return this.Item2;
					default: return TupleHelpers.FailIndexOutOfRange<object>(index, 2);
				}
			}
		}

		public IVarTuple this[int? fromIncluded, int? toExcluded]
		{
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get => TupleHelpers.Splice(this, fromIncluded, toExcluded);
		}

#if USE_RANGE_API

		object? IVarTuple.this[Index index] => index.GetOffset(2) switch
		{
			0 => this.Item1,
			1 => this.Item2,
			_ => TupleHelpers.FailIndexOutOfRange<object>(index.Value, 2)
		};

		public IVarTuple this[Range range]
		{
			get
			{
				(int offset, int count) = range.GetOffsetAndLength(2);
				return count switch
				{
					0 => STuple.Empty,
					1 => offset == 0 ? STuple.Create(this.Item1) : STuple.Create(this.Item2),
					_ => this
				};
			}
		}

#endif

		/// <summary>Return the typed value of an item of the tuple, given its position</summary>
		/// <typeparam name="TItem">Expected type of the item</typeparam>
		/// <param name="index">Position of the item (if negative, means relative from the end)</param>
		/// <returns>Value of the item at position <paramref name="index"/>, adapted into type <typeparamref name="TItem"/>.</returns>
		public TItem? Get<TItem>(int index)
		{
			return index switch
			{
				0  => TypeConverters.Convert<T1, TItem>(this.Item1),
				1  => TypeConverters.Convert<T2, TItem>(this.Item2),
				-1 => TypeConverters.Convert<T2, TItem>(this.Item2),
				-2 => TypeConverters.Convert<T1, TItem>(this.Item1),
				_  => TupleHelpers.FailIndexOutOfRange<TItem>(index, 2)
			};
		}

		/// <summary>Return the value of the last item in the tuple</summary>
		public T2 Last
		{
			[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
			[return: MaybeNull]
			get => this.Item2;
		}

		/// <summary>Return a tuple without the first item</summary>
		public STuple<T2> Tail
		{
			[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
			get => new STuple<T2>(this.Item2);
		}

		IVarTuple IVarTuple.Append<T3>(T3? value) where T3 : default
		{
			return new STuple<T1, T2, T3>(this.Item1, this.Item2, value);
		}

		/// <summary>Appends a single new item at the end of the current tuple.</summary>
		/// <param name="value">Value that will be added as an embedded item</param>
		/// <returns>New tuple with one extra item</returns>
		/// <remarks>If <paramref name="value"/> is a tuple, and you want to append the *items* of this tuple, and not the tuple itself, please call <see cref="Concat"/>!</remarks>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public STuple<T1, T2, T3> Append<T3>(T3? value)
		{
			return new STuple<T1, T2, T3>(this.Item1, this.Item2, value);
			// Note: By create a STuple<T1, T2, T3> we risk an explosion of the number of combinations of Ts which could potentially cause problems at runtime (too many variants of the same generic types).
			// ex: if we have N possible types, then there could be N^3 possible variants of STuple<T1, T2, T3> that the JIT has to deal with.
			// => if this starts becoming a problem, then we should return a list tuple !
		}

		/// <summary>Appends two new items at the end of the current tuple.</summary>
		/// <param name="value1">Value that will be added as an embedded item</param>
		/// <param name="value2">Value that will be added as an embedded item</param>
		/// <returns>New tuple with two extra item</returns>
		/// <remarks>If any of <paramref name="value1"/> or <paramref name="value2"/> is a tuple, and you want to append the *items* of this tuple, and not the tuple itself, please call <see cref="Concat"/>!</remarks>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public STuple<T1, T2, T3, T4> Append<T3, T4>(T3? value1, T4? value2)
		{
			return new STuple<T1, T2, T3, T4>(this.Item1, this.Item2, value1, value2);
			// Note: By create a STuple<T1, T2, T3> we risk an explosion of the number of combinations of Ts which could potentially cause problems at runtime (too many variants of the same generic types).
			// ex: if we have N possible types, then there could be N^3 possible variants of STuple<T1, T2, T3> that the JIT has to deal with.
			// => if this starts becoming a problem, then we should return a list tuple !
		}

		/// <summary>Appends the items of a tuple at the end of the current tuple.</summary>
		/// <param name="tuple">Tuple whose items are to be appended at the end</param>
		/// <returns>New tuple composed of the current tuple's items, followed by <paramref name="tuple"/>'s items</returns>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public IVarTuple Concat(IVarTuple tuple)
		{
			return STuple.Concat(this, tuple);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public STuple<T1, T2, T3> Concat<T3>(STuple<T3> tuple)
		{
			return new STuple<T1, T2, T3>(this.Item1, this.Item2, tuple.Item1);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public STuple<T1, T2, T3, T4> Concat<T3, T4>(STuple<T3, T4> tuple)
		{
			return new STuple<T1, T2, T3, T4>(this.Item1, this.Item2, tuple.Item1, tuple.Item2);
		}

		/// <summary>Copy both items of this pair into an array at the specified offset</summary>
		public void CopyTo(object?[] array, int offset)
		{
			array[offset] = this.Item1;
			array[offset + 1] = this.Item2;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void Deconstruct(out T1? item1, out T2? item2)
		{
			item1 = this.Item1;
			item2 = this.Item2;
		}

		/// <summary>Execute a lambda Action with the content of this tuple</summary>
		/// <param name="lambda">Action that will be passed the content of this tuple as parameters</param>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void With(Action<T1?, T2?> lambda)
		{
			lambda(this.Item1, this.Item2);
		}

		/// <summary>Execute a lambda Function with the content of this tuple</summary>
		/// <param name="lambda">Action that will be passed the content of this tuple as parameters</param>
		/// <returns>Result of calling <paramref name="lambda"/> with the items of this tuple</returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public TItem With<TItem>(Func<T1?, T2?, TItem> lambda)
		{
			return lambda(this.Item1, this.Item2);
		}

		void ITupleSerializable.PackTo(ref TupleWriter writer)
		{
			TuplePackers.SerializeTo<T1>(ref writer, this.Item1);
			TuplePackers.SerializeTo<T2>(ref writer, this.Item2);
		}

		public IEnumerator<object?> GetEnumerator()
		{
			yield return this.Item1;
			yield return this.Item2;
		}

		System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
		{
			return GetEnumerator();
		}

		public override string ToString()
		{
			return string.Concat(
				"(",
				STuple.Formatter.Stringify(this.Item1), ", ",
				STuple.Formatter.Stringify(this.Item2),
				")"
			);
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
		public bool Equals(STuple<T1, T2> other)
		{
			return SimilarValueComparer.Default.Equals(this.Item1, other.Item1)
			    && SimilarValueComparer.Default.Equals(this.Item2, other.Item2);
		}

		public override int GetHashCode()
		{
			return ((IStructuralEquatable)this).GetHashCode(SimilarValueComparer.Default);
		}

		public static bool operator ==(STuple<T1, T2> left, STuple<T1, T2> right)
		{
			return SimilarValueComparer.Default.Equals(left.Item1, right.Item1)
			    && SimilarValueComparer.Default.Equals(left.Item2, right.Item2);
		}

		public static bool operator !=(STuple<T1, T2> left, STuple<T1, T2> right)
		{
			return !SimilarValueComparer.Default.Equals(left.Item1, right.Item1)
			    || !SimilarValueComparer.Default.Equals(left.Item2, right.Item2);
		}

		bool IStructuralEquatable.Equals(object? other, IEqualityComparer comparer)
		{
			if (other == null) return false;
			if (other is STuple<T1, T2> stuple)
			{
				return comparer.Equals(this.Item1, stuple.Item1)
				    && comparer.Equals(this.Item2, stuple.Item2);
			}
			if (other is ValueTuple<T1, T2> vtuple)
			{
				return comparer.Equals(this.Item1, vtuple.Item1)
				    && comparer.Equals(this.Item2, vtuple.Item2);
			}
			return TupleHelpers.Equals(this, other, comparer);
		}

		int IStructuralEquatable.GetHashCode(IEqualityComparer comparer)
		{
			return HashCodes.Combine(
				comparer.GetHashCode(this.Item1),
				comparer.GetHashCode(this.Item2)
			);
		}

		[Pure]
		public static implicit operator STuple<T1, T2>(Tuple<T1, T2> t)
		{
			Contract.NotNull(t);
			return new STuple<T1, T2>(t.Item1, t.Item2);
		}

		[Pure]
		public static explicit operator Tuple<T1, T2>(STuple<T1, T2> t)
		{
			return new Tuple<T1, T2>(t.Item1, t.Item2);
		}

		public void Fill(ref (T1, T2) t)
		{
			t.Item1 = this.Item1;
			t.Item2 = this.Item2;
		}

		/// <summary>Appends the items of a tuple at the end of the current tuple.</summary>
		/// <param name="tuple">Tuple whose items are to be appended at the end</param>
		/// <returns>New tuple composed of the current tuple's items, followed by <paramref name="tuple"/>'s items</returns>
		[Pure]
		public STuple<T1, T2, T3> Concat<T3>(ValueTuple<T3> tuple)
		{
			return new STuple<T1, T2, T3>(this.Item1, this.Item2, tuple.Item1);
		}

		/// <summary>Appends the items of a tuple at the end of the current tuple.</summary>
		/// <param name="tuple">Tuple whose items are to be appended at the end</param>
		/// <returns>New tuple composed of the current tuple's items, followed by <paramref name="tuple"/>'s items</returns>
		[Pure]
		public STuple<T1, T2, T3, T4> Concat<T3, T4>((T3, T4) tuple)
		{
			return new STuple<T1, T2, T3, T4>(this.Item1, this.Item2, tuple.Item1, tuple.Item2);
		}

		/// <summary>Appends the items of a tuple at the end of the current tuple.</summary>
		/// <param name="tuple">Tuple whose items are to be appended at the end</param>
		/// <returns>New tuple composed of the current tuple's items, followed by <paramref name="tuple"/>'s items</returns>
		[Pure]
		public STuple<T1, T2, T3, T4, T5> Concat<T3, T4, T5>((T3, T4, T5) tuple)
		{
			return new STuple<T1, T2, T3, T4, T5>(this.Item1, this.Item2, tuple.Item1, tuple.Item2, tuple.Item3);
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public (T1, T2) ToValueTuple()
		{
			return (this.Item1, this.Item2);
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static implicit operator STuple<T1, T2>((T1, T2) t)
		{
			return new STuple<T1, T2>(t.Item1, t.Item2);
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static implicit operator (T1, T2)(STuple<T1, T2> t)
		{
			return (t.Item1, t.Item2);
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		bool IEquatable<(T1, T2)>.Equals((T1, T2) other)
		{
			return SimilarValueComparer.Default.Equals(this.Item1, other.Item1)
				&& SimilarValueComparer.Default.Equals(this.Item2, other.Item2);
		}

		public static bool operator ==(STuple<T1, T2> left, (T1, T2) right)
		{
			return SimilarValueComparer.Default.Equals(left.Item1, right.Item1)
				&& SimilarValueComparer.Default.Equals(left.Item2, right.Item2);
		}

		public static bool operator ==((T1, T2) left, STuple<T1, T2> right)
		{
			return SimilarValueComparer.Default.Equals(left.Item1, right.Item1)
				&& SimilarValueComparer.Default.Equals(left.Item2, right.Item2);
		}

		public static bool operator !=(STuple<T1, T2> left, (T1, T2) right)
		{
			return !SimilarValueComparer.Default.Equals(left.Item1, right.Item1)
				|| !SimilarValueComparer.Default.Equals(left.Item2, right.Item2);
		}

		public static bool operator !=((T1, T2) left, STuple<T1, T2> right)
		{
			return !SimilarValueComparer.Default.Equals(left.Item1, right.Item1)
				|| !SimilarValueComparer.Default.Equals(left.Item2, right.Item2);
		}

		public sealed class Comparer : IComparer<STuple<T1, T2>>
		{

			public static Comparer Default { get; } = new Comparer();

			private static readonly Comparer<T1> Comparer1 = Comparer<T1>.Default;
			private static readonly Comparer<T2> Comparer2 = Comparer<T2>.Default;

			private Comparer() { }

			public int Compare(STuple<T1, T2> x, STuple<T1, T2> y)
			{
				int cmp = Comparer1.Compare(x.Item1, y.Item1);
				if (cmp == 0) cmp = Comparer2.Compare(x.Item2, y.Item2);
				return cmp;
			}

		}

		public sealed class EqualityComparer : IEqualityComparer<STuple<T1, T2>>
		{

			public static EqualityComparer Default { get; } = new EqualityComparer();

			private static readonly EqualityComparer<T1> Comparer1 = EqualityComparer<T1>.Default;
			private static readonly EqualityComparer<T2> Comparer2 = EqualityComparer<T2>.Default;

			private EqualityComparer() { }

			public bool Equals(STuple<T1, T2> x, STuple<T1, T2> y)
			{
				return Comparer1.Equals(x.Item1, y.Item1)
				    && Comparer2.Equals(x.Item2, y.Item2);
			}

			public int GetHashCode(STuple<T1, T2> obj)
			{
				return HashCodes.Combine(
					Comparer1.GetHashCode(obj.Item1),
					Comparer2.GetHashCode(obj.Item2)
				);
			}

		}

	}

}

#endif
