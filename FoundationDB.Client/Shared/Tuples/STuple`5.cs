#region BSD License
/* Copyright (c) 2013-2020, Doxense SAS
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

	/// <summary>Tuple that can hold five items</summary>
	/// <typeparam name="T1">Type of the 1st item</typeparam>
	/// <typeparam name="T2">Type of the 2nd item</typeparam>
	/// <typeparam name="T3">Type of the 3rd item</typeparam>
	/// <typeparam name="T4">Type of the 4th item</typeparam>
	/// <typeparam name="T5">Type of the 5th item</typeparam>
	[ImmutableObject(true), DebuggerDisplay("{ToString(),nq}")]
	[PublicAPI]
	public readonly struct STuple<T1, T2, T3, T4, T5> : IVarTuple, IEquatable<STuple<T1, T2, T3, T4, T5>>, IEquatable<(T1, T2, T3, T4, T5)>, ITupleSerializable
	{
		// This is mostly used by code that create a lot of temporary quartets, to reduce the pressure on the Garbage Collector by allocating them on the stack.
		// Please note that if you return an STuple<T> as an ITuple, it will be boxed by the CLR and all memory gains will be lost

		/// <summary>First element of the tuple</summary>
		[MaybeNull]
		public readonly T1 Item1;

		/// <summary>Second element of the tuple</summary>
		[MaybeNull]
		public readonly T2 Item2;

		/// <summary>Third element of the tuple</summary>
		[MaybeNull]
		public readonly T3 Item3;

		/// <summary>Fourth element of the tuple</summary>
		[MaybeNull]
		public readonly T4 Item4;

		/// <summary>Fifth and last element of the tuple</summary>
		[MaybeNull]
		public readonly T5 Item5;

		/// <summary>Create a tuple containing for items</summary>
		[DebuggerStepThrough]
		public STuple([AllowNull] T1 item1, [AllowNull] T2 item2, [AllowNull] T3 item3, [AllowNull] T4 item4, [AllowNull] T5 item5)
		{
			this.Item1 = item1;
			this.Item2 = item2;
			this.Item3 = item3;
			this.Item4 = item4;
			this.Item5 = item5;
		}

		/// <summary>Number of items in this tuple</summary>
		public int Count => 5;

		object? IReadOnlyList<object?>.this[int index] => ((IVarTuple) this)[index];

		object? IVarTuple.this[int index]
		{
			get
			{
				switch (index)
				{
					case 0: case -5: return this.Item1;
					case 1: case -4: return this.Item2;
					case 2: case -3: return this.Item3;
					case 3: case -2: return this.Item4;
					case 4: case -1: return this.Item5;
					default: return TupleHelpers.FailIndexOutOfRange<object>(index, 5);
				}
			}
		}

		public IVarTuple this[int? fromIncluded, int? toExcluded]
		{
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get => TupleHelpers.Splice(this, fromIncluded, toExcluded);
		}

#if USE_RANGE_API

		object? IVarTuple.this[Index index] => index.GetOffset(5) switch
		{
			0 => this.Item1,
			1 => this.Item2,
			2 => this.Item3,
			3 => this.Item4,
			4 => this.Item5,
			_ => TupleHelpers.FailIndexOutOfRange<object>(index.Value, 5)
		};

		public IVarTuple this[Range range]
		{
			get
			{
				(int offset, int count) = range.GetOffsetAndLength(5);
				return count switch
				{
					0 => STuple.Empty,
					1 => (offset switch
					{
						0 => (IVarTuple) STuple.Create(this.Item1),
						1 => STuple.Create(this.Item2),
						2 => STuple.Create(this.Item3),
						3 => STuple.Create(this.Item4),
						_ => STuple.Create(this.Item5)
					}),
					2 => (offset switch
					{
						0 => (IVarTuple) STuple.Create(this.Item1, this.Item2),
						1 => STuple.Create(this.Item2, this.Item3),
						2 => STuple.Create(this.Item3, this.Item4),
						_ => STuple.Create(this.Item4, this.Item5)
					}),
					3 => (offset switch
					{
						0 => (IVarTuple) STuple.Create(this.Item1, this.Item2, this.Item3),
						1 => STuple.Create(this.Item2, this.Item3, this.Item4),
						_ => STuple.Create(this.Item3, this.Item4, this.Item5)
					}),
					4 => (offset switch
					{
						0 => (IVarTuple) STuple.Create(this.Item1, this.Item2, this.Item3, this.Item4),
						_ => STuple.Create(this.Item2, this.Item3, this.Item4, this.Item5)
					}),
					_ => this
				};
			}
		}

#endif

		/// <summary>Return the typed value of an item of the tuple, given its position</summary>
		/// <typeparam name="TItem">Expected type of the item</typeparam>
		/// <param name="index">Position of the item (if negative, means relative from the end)</param>
		/// <returns>Value of the item at position <paramref name="index"/>, adapted into type <typeparamref name="TItem"/>.</returns>
		public TItem Get<TItem>(int index)
		{
			switch(index)
			{
					case 0: case -5: return TypeConverters.Convert<T1, TItem>(this.Item1);
					case 1: case -4: return TypeConverters.Convert<T2, TItem>(this.Item2);
					case 2: case -3: return TypeConverters.Convert<T3, TItem>(this.Item3);
					case 3: case -2: return TypeConverters.Convert<T4, TItem>(this.Item4);
					case 4: case -1: return TypeConverters.Convert<T5, TItem>(this.Item5);
					default: return TupleHelpers.FailIndexOutOfRange<TItem>(index, 5);
			}
		}

		/// <summary>Return the value of the last item in the tuple</summary>
		public T5 Last
		{
			[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
			[return: MaybeNull]
			get => this.Item5;
		}

		/// <summary>Return a tuple without the first item</summary>
		public STuple<T2, T3, T4, T5> Tail
		{
			[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
			get => new STuple<T2, T3, T4, T5>(this.Item2, this.Item3, this.Item4, this.Item5);
		}

		/// <summary>Appends a single new item at the end of the current tuple.</summary>
		/// <param name="value">Value that will be added as an embedded item</param>
		/// <returns>New tuple with one extra item</returns>
		/// <remarks>If <paramref name="value"/> is a tuple, and you want to append the *items*  of this tuple, and not the tuple itself, please call <see cref="Concat"/>!</remarks>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public IVarTuple Append<T6>(T6 value)
		{
			return new LinkedTuple<T6>(this, value);
		}

		/// <summary>Appends the items of a tuple at the end of the current tuple.</summary>
		/// <param name="tuple">Tuple whose items are to be appended at the end</param>
		/// <returns>New tuple composed of the current tuple items, followed by <paramref name="tuple"/>'s items</returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public IVarTuple Concat(IVarTuple tuple)
		{
			return STuple.Concat(this, tuple);
		}

		/// <summary>Copy all the items of this tuple into an array at the specified offset</summary>
		public void CopyTo(object?[] array, int offset)
		{
			array[offset] = this.Item1;
			array[offset + 1] = this.Item2;
			array[offset + 2] = this.Item3;
			array[offset + 3] = this.Item4;
			array[offset + 4] = this.Item5;
		}

		[EditorBrowsable(EditorBrowsableState.Never)]
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void Deconstruct([MaybeNull] out T1 item1, [MaybeNull] out T2 item2, [MaybeNull] out T3 item3, [MaybeNull] out T4 item4, [MaybeNull] out T5 item5)
		{
			item1 = this.Item1;
			item2 = this.Item2;
			item3 = this.Item3;
			item4 = this.Item4;
			item5 = this.Item5;
		}

		/// <summary>Execute a lambda Action with the content of this tuple</summary>
		/// <param name="lambda">Action that will be passed the content of this tuple as parameters</param>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void With(Action<T1, T2, T3, T4, T5> lambda)
		{
			lambda(this.Item1, this.Item2, this.Item3, this.Item4, this.Item5);
		}

		/// <summary>Execute a lambda Function with the content of this tuple</summary>
		/// <param name="lambda">Action that will be passed the content of this tuple as parameters</param>
		/// <returns>Result of calling <paramref name="lambda"/> with the items of this tuple</returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public TItem With<TItem>(Func<T1, T2, T3, T4, T5, TItem> lambda)
		{
			return lambda(this.Item1, this.Item2, this.Item3, this.Item4, this.Item5);
		}

		void ITupleSerializable.PackTo(ref TupleWriter writer)
		{
			TuplePackers.SerializeTo<T1>(ref writer, this.Item1);
			TuplePackers.SerializeTo<T2>(ref writer, this.Item2);
			TuplePackers.SerializeTo<T3>(ref writer, this.Item3);
			TuplePackers.SerializeTo<T4>(ref writer, this.Item4);
			TuplePackers.SerializeTo<T5>(ref writer, this.Item5);
		}

		public IEnumerator<object?> GetEnumerator()
		{
			yield return this.Item1;
			yield return this.Item2;
			yield return this.Item3;
			yield return this.Item4;
			yield return this.Item5;
		}

		System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
		{
			return this.GetEnumerator();
		}

		public override string ToString()
		{
			return string.Join("", new[]
			{
				"(",
				STuple.Formatter.Stringify(this.Item1), ", ",
				STuple.Formatter.Stringify(this.Item2), ", ",
				STuple.Formatter.Stringify(this.Item3), ", ",
				STuple.Formatter.Stringify(this.Item4), ", ",
				STuple.Formatter.Stringify(this.Item5),
				")"
			});
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
		public bool Equals(STuple<T1, T2, T3, T4, T5> other)
		{
			return SimilarValueComparer.Default.Equals(this.Item1, other.Item1)
				&& SimilarValueComparer.Default.Equals(this.Item2, other.Item2)
				&& SimilarValueComparer.Default.Equals(this.Item3, other.Item3)
				&& SimilarValueComparer.Default.Equals(this.Item4, other.Item4)
				&& SimilarValueComparer.Default.Equals(this.Item5, other.Item5);
		}

		public override int GetHashCode()
		{
			return ((IStructuralEquatable)this).GetHashCode(SimilarValueComparer.Default);
		}

		public static bool operator ==(STuple<T1, T2, T3, T4, T5> left, STuple<T1, T2, T3, T4, T5> right)
		{
			var comparer = SimilarValueComparer.Default;
			return comparer.Equals(left.Item1, right.Item1)
				&& comparer.Equals(left.Item2, right.Item2)
				&& comparer.Equals(left.Item3, right.Item3)
				&& comparer.Equals(left.Item4, right.Item4)
				&& comparer.Equals(left.Item5, right.Item5);
		}

		public static bool operator !=(STuple<T1, T2, T3, T4, T5> left, STuple<T1, T2, T3, T4, T5> right)
		{
			var comparer = SimilarValueComparer.Default;
			return !comparer.Equals(left.Item1, right.Item1)
				|| !comparer.Equals(left.Item2, right.Item2)
				|| !comparer.Equals(left.Item3, right.Item3)
				|| !comparer.Equals(left.Item4, right.Item4)
				|| !comparer.Equals(left.Item5, right.Item5);
		}

		bool IStructuralEquatable.Equals(object? other, IEqualityComparer comparer)
		{
			if (other == null) return false;
			if (other is STuple<T1, T2, T3, T4, T5> stuple)
			{
				return comparer.Equals(this.Item1, stuple.Item1)
					&& comparer.Equals(this.Item2, stuple.Item2)
					&& comparer.Equals(this.Item3, stuple.Item3)
					&& comparer.Equals(this.Item4, stuple.Item4)
					&& comparer.Equals(this.Item5, stuple.Item5);
			}
			if (other is ValueTuple<T1, T2, T3, T4, T5> vtuple)
			{
				return comparer.Equals(this.Item1, vtuple.Item1)
					&& comparer.Equals(this.Item2, vtuple.Item2)
					&& comparer.Equals(this.Item3, vtuple.Item3)
					&& comparer.Equals(this.Item4, vtuple.Item4)
					&& comparer.Equals(this.Item5, vtuple.Item5);
			}
			return TupleHelpers.Equals(this, other, comparer);
		}

		int IStructuralEquatable.GetHashCode(IEqualityComparer comparer)
		{
			int h = HashCodes.Combine(
				comparer.GetHashCode(this.Item1),
				comparer.GetHashCode(this.Item2),
				comparer.GetHashCode(this.Item3)
			);
			h = HashCodes.Combine(h, comparer.GetHashCode(this.Item4));
			h = HashCodes.Combine(h, comparer.GetHashCode(this.Item5));
			return h;
		}

		[Pure]
		public static implicit operator STuple<T1, T2, T3, T4, T5>(Tuple<T1, T2, T3, T4, T5> t)
		{
			Contract.NotNull(t, nameof(t));
			return new STuple<T1, T2, T3, T4, T5>(t.Item1, t.Item2, t.Item3, t.Item4, t.Item5);
		}

		[Pure]
		public static explicit operator Tuple<T1, T2, T3, T4, T5>(STuple<T1, T2, T3, T4, T5> t)
		{
			return new Tuple<T1, T2, T3, T4, T5>(t.Item1, t.Item2, t.Item3, t.Item4, t.Item5);
		}

		public void Fill(ref (T1, T2, T3, T4, T5) t)
		{
			t.Item1 = this.Item1;
			t.Item2 = this.Item2;
			t.Item3 = this.Item3;
			t.Item4 = this.Item4;
			t.Item5 = this.Item5;
		}

		/// <summary>Appends the items of a tuple at the end of the current tuple.</summary>
		/// <param name="tuple">Tuple whose items are to be appended at the end</param>
		/// <returns>New tuple composed of the current tuple items, followed by <paramref name="tuple"/>'s items</returns>
		[Pure]
		public STuple<T1, T2, T3, T4, T5, T6> Concat<T6>(ValueTuple<T6> tuple)
		{
			return new STuple<T1, T2, T3, T4, T5, T6>(this.Item1, this.Item2, this.Item3, this.Item4, this.Item5, tuple.Item1);
		}

		[Pure]
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public (T1, T2, T3, T4, T5) ToValueTuple()
		{
			return (this.Item1, this.Item2, this.Item3, this.Item4, this.Item5);
		}

		[Pure]
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static implicit operator STuple<T1, T2, T3, T4, T5>((T1, T2, T3, T4, T5) t)
		{
			return new STuple<T1, T2, T3, T4, T5>(t.Item1, t.Item2, t.Item3, t.Item4, t.Item5);
		}

		[Pure]
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static implicit operator (T1, T2, T3, T4, T5) (STuple<T1, T2, T3, T4, T5> t)
		{
			return (t.Item1, t.Item2, t.Item3, t.Item4, t.Item5);
		}

		[Pure]
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		bool IEquatable<(T1, T2, T3, T4, T5)>.Equals((T1, T2, T3, T4, T5) other)
		{
			return SimilarValueComparer.Default.Equals(this.Item1, this.Item1)
				&& SimilarValueComparer.Default.Equals(this.Item2, this.Item2)
				&& SimilarValueComparer.Default.Equals(this.Item3, this.Item3)
				&& SimilarValueComparer.Default.Equals(this.Item4, this.Item4)
				&& SimilarValueComparer.Default.Equals(this.Item5, this.Item5);
		}

		public static bool operator ==(STuple<T1, T2, T3, T4, T5> left, (T1, T2, T3, T4, T5) right)
		{
			return SimilarValueComparer.Default.Equals(left.Item1, right.Item1)
				&& SimilarValueComparer.Default.Equals(left.Item2, right.Item2)
				&& SimilarValueComparer.Default.Equals(left.Item3, right.Item3)
				&& SimilarValueComparer.Default.Equals(left.Item4, right.Item4)
				&& SimilarValueComparer.Default.Equals(left.Item5, right.Item5);
		}

		public static bool operator ==((T1, T2, T3, T4, T5) left, STuple<T1, T2, T3, T4, T5> right)
		{
			return SimilarValueComparer.Default.Equals(left.Item1, right.Item1)
				&& SimilarValueComparer.Default.Equals(left.Item2, right.Item2)
				&& SimilarValueComparer.Default.Equals(left.Item3, right.Item3)
				&& SimilarValueComparer.Default.Equals(left.Item4, right.Item4)
				&& SimilarValueComparer.Default.Equals(left.Item5, right.Item5);
		}

		public static bool operator !=(STuple<T1, T2, T3, T4, T5> left, (T1, T2, T3, T4, T5) right)
		{
			return !SimilarValueComparer.Default.Equals(left.Item1, right.Item1)
				|| !SimilarValueComparer.Default.Equals(left.Item2, right.Item2)
				|| !SimilarValueComparer.Default.Equals(left.Item3, right.Item3)
				|| !SimilarValueComparer.Default.Equals(left.Item4, right.Item4)
				|| !SimilarValueComparer.Default.Equals(left.Item5, right.Item5);
		}

		public static bool operator !=((T1, T2, T3, T4, T5) left, STuple<T1, T2, T3, T4, T5> right)
		{
			return !SimilarValueComparer.Default.Equals(left.Item1, right.Item1)
				|| !SimilarValueComparer.Default.Equals(left.Item2, right.Item2)
				|| !SimilarValueComparer.Default.Equals(left.Item3, right.Item3)
				|| !SimilarValueComparer.Default.Equals(left.Item4, right.Item4)
				|| !SimilarValueComparer.Default.Equals(left.Item5, right.Item5);
		}

		public sealed class Comparer : IComparer<STuple<T1, T2, T3, T4, T5>>
		{

			public static Comparer Default { get; } = new Comparer();

			private static readonly Comparer<T1> Comparer1 = Comparer<T1>.Default;
			private static readonly Comparer<T2> Comparer2 = Comparer<T2>.Default;
			private static readonly Comparer<T3> Comparer3 = Comparer<T3>.Default;
			private static readonly Comparer<T4> Comparer4 = Comparer<T4>.Default;
			private static readonly Comparer<T5> Comparer5 = Comparer<T5>.Default;

			private Comparer() { }

			public int Compare(STuple<T1, T2, T3, T4, T5> x, STuple<T1, T2, T3, T4, T5> y)
			{
				int cmp = Comparer1.Compare(x.Item1, y.Item1);
				if (cmp == 0) cmp = Comparer2.Compare(x.Item2, y.Item2);
				if (cmp == 0) cmp = Comparer3.Compare(x.Item3, y.Item3);
				if (cmp == 0) cmp = Comparer4.Compare(x.Item4, y.Item4);
				if (cmp == 0) cmp = Comparer5.Compare(x.Item5, y.Item5);
				return cmp;
			}

		}

		public sealed class EqualityComparer : IEqualityComparer<STuple<T1, T2, T3, T4, T5>>
		{

			public static EqualityComparer Default { get; } = new EqualityComparer();

			private static readonly EqualityComparer<T1> Comparer1 = EqualityComparer<T1>.Default;
			private static readonly EqualityComparer<T2> Comparer2 = EqualityComparer<T2>.Default;
			private static readonly EqualityComparer<T3> Comparer3 = EqualityComparer<T3>.Default;
			private static readonly EqualityComparer<T4> Comparer4 = EqualityComparer<T4>.Default;
			private static readonly EqualityComparer<T5> Comparer5 = EqualityComparer<T5>.Default;

			private EqualityComparer() { }

			public bool Equals(STuple<T1, T2, T3, T4, T5> x, STuple<T1, T2, T3, T4, T5> y)
			{
				return Comparer1.Equals(x.Item1, y.Item1)
					&& Comparer2.Equals(x.Item2, y.Item2)
					&& Comparer3.Equals(x.Item3, y.Item3)
					&& Comparer4.Equals(x.Item4, y.Item4)
					&& Comparer5.Equals(x.Item5, y.Item5);
			}

			public int GetHashCode(STuple<T1, T2, T3, T4, T5> obj)
			{
				int h = HashCodes.Combine(
					Comparer1.GetHashCode(obj.Item1),
					Comparer2.GetHashCode(obj.Item2),
					Comparer3.GetHashCode(obj.Item3)
				);
				h = HashCodes.Combine(h, Comparer4.GetHashCode(obj.Item4));
				h = HashCodes.Combine(h, Comparer5.GetHashCode(obj.Item5));
				return h;
			}
		}

	}

}

#endif
