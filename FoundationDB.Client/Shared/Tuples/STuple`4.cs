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

namespace Doxense.Collections.Tuples
{
	using System;
	using System.Collections;
	using System.Collections.Generic;
	using System.ComponentModel;
	using System.Diagnostics;
	using System.Runtime.CompilerServices;
	using Doxense.Diagnostics.Contracts;
	using Doxense.Runtime.Converters;
	using JetBrains.Annotations;

	/// <summary>Tuple that can hold four items</summary>
	/// <typeparam name="T1">Type of the first item</typeparam>
	/// <typeparam name="T2">Type of the second item</typeparam>
	/// <typeparam name="T3">Type of the third item</typeparam>
	/// <typeparam name="T4">Type of the fourth item</typeparam>
	[ImmutableObject(true), DebuggerDisplay("{ToString(),nq}")]
	[PublicAPI]
	public readonly struct STuple<T1, T2, T3, T4> : ITuple, IEquatable<STuple<T1, T2, T3, T4>>, IEquatable<(T1, T2, T3, T4)>
	{
		// This is mostly used by code that create a lot of temporary quartets, to reduce the pressure on the Garbage Collector by allocating them on the stack.
		// Please note that if you return an STuple<T> as an ITuple, it will be boxed by the CLR and all memory gains will be lost

		/// <summary>First element of the quartet</summary>
		public readonly T1 Item1;
		/// <summary>Second element of the quartet</summary>
		public readonly T2 Item2;
		/// <summary>Third element of the quartet</summary>
		public readonly T3 Item3;
		/// <summary>Fourth and last element of the quartet</summary>
		public readonly T4 Item4;

		/// <summary>Create a tuple containing for items</summary>
		[DebuggerStepThrough]
		public STuple(T1 item1, T2 item2, T3 item3, T4 item4)
		{
			this.Item1 = item1;
			this.Item2 = item2;
			this.Item3 = item3;
			this.Item4 = item4;
		}

		/// <summary>Number of items in this tuple</summary>
		public int Count => 4;

		/// <summary>Return the Nth item in this tuple</summary>
		public object this[int index]
		{
			get
			{
				switch (index)
				{
					case 0: case -4: return this.Item1;
					case 1: case -3: return this.Item2;
					case 2: case -2: return this.Item3;
					case 3: case -1: return this.Item4;
					default: return TupleHelpers.FailIndexOutOfRange<object>(index, 4);
				}
			}
		}

		public ITuple this[int? fromIncluded, int? toExcluded]
		{
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get { return TupleHelpers.Splice(this, fromIncluded, toExcluded); }
		}

		/// <summary>Return the typed value of an item of the tuple, given its position</summary>
		/// <typeparam name="TItem">Expected type of the item</typeparam>
		/// <param name="index">Position of the item (if negative, means relative from the end)</param>
		/// <returns>Value of the item at position <paramref name="index"/>, adapted into type <typeparamref name="TItem"/>.</returns>
		public TItem Get<TItem>(int index)
		{
			switch(index)
			{
					case 0: case -4: return TypeConverters.Convert<T1, TItem>(this.Item1);
					case 1: case -3: return TypeConverters.Convert<T2, TItem>(this.Item2);
					case 2: case -2: return TypeConverters.Convert<T3, TItem>(this.Item3);
					case 3: case -1: return TypeConverters.Convert<T4, TItem>(this.Item4);
					default: return TupleHelpers.FailIndexOutOfRange<TItem>(index, 4);
			}
		}

		/// <summary>Return the value of the last item in the tuple</summary>
		public T4 Last
		{
			[Pure]
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get { return this.Item4; }
		}

		/// <summary>Return a tuple without the first item</summary>
		public STuple<T2, T3, T4> Tail
		{
			[Pure]
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get { return new STuple<T2, T3, T4>(this.Item2, this.Item3, this.Item4); }
		}

		ITuple ITuple.Append<T5>(T5 value)
		{
			// the caller doesn't care about the return type, so just box everything into a list tuple
			return new ListTuple(new object[5] { this.Item1, this.Item2, this.Item3, this.Item4, value }, 0, 5);
		}

		/// <summary>Appends a single new item at the end of the current tuple.</summary>
		/// <param name="value">Value that will be added as an embedded item</param>
		/// <returns>New tuple with one extra item</returns>
		/// <remarks>If <paramref name="value"/> is a tuple, and you want to append the *items*  of this tuple, and not the tuple itself, please call <see cref="Concat"/>!</remarks>
		[NotNull, Pure]
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public LinkedTuple<T5> Append<T5>(T5 value)
		{
			// the caller probably cares about the return type, since it is using a struct, but whatever tuple type we use will end up boxing this tuple on the heap, and we will loose type information.
			// but, by returning a LinkedTuple<T5>, the tuple will still remember the exact type, and efficiently serializer/convert the values (without having to guess the type)
			return new LinkedTuple<T5>(this, value);
		}

		/// <summary>Appends the items of a tuple at the end of the current tuple.</summary>
		/// <param name="tuple">Tuple whose items are to be appended at the end</param>
		/// <returns>New tuple composed of the current tuple's items, followed by <paramref name="tuple"/>'s items</returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public ITuple Concat(ITuple tuple)
		{
			return STuple.Concat(this, tuple);
		}

		/// <summary>Copy all the items of this tuple into an array at the specified offset</summary>
		public void CopyTo(object[] array, int offset)
		{
			array[offset] = this.Item1;
			array[offset + 1] = this.Item2;
			array[offset + 2] = this.Item3;
			array[offset + 3] = this.Item4;
		}

		[EditorBrowsable(EditorBrowsableState.Never)]
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void Deconstruct(out T1 item1, out T2 item2, out T3 item3, out T4 item4)
		{
			item1 = this.Item1;
			item2 = this.Item2;
			item3 = this.Item3;
			item4 = this.Item4;
		}

		/// <summary>Execute a lambda Action with the content of this tuple</summary>
		/// <param name="lambda">Action that will be passed the content of this tuple as parameters</param>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void With([NotNull] Action<T1, T2, T3, T4> lambda)
		{
			lambda(this.Item1, this.Item2, this.Item3, this.Item4);
		}

		/// <summary>Execute a lambda Function with the content of this tuple</summary>
		/// <param name="lambda">Action that will be passed the content of this tuple as parameters</param>
		/// <returns>Result of calling <paramref name="lambda"/> with the items of this tuple</returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public TItem With<TItem>([NotNull] Func<T1, T2, T3, T4, TItem> lambda)
		{
			return lambda(this.Item1, this.Item2, this.Item3, this.Item4);
		}

		public IEnumerator<object> GetEnumerator()
		{
			yield return this.Item1;
			yield return this.Item2;
			yield return this.Item3;
			yield return this.Item4;
		}

		System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
		{
			return this.GetEnumerator();
		}

		public override string ToString()
		{
			return string.Concat(
				"(",
				STuple.Formatter.Stringify(this.Item1), ", ",
				STuple.Formatter.Stringify(this.Item2), ", ",
				STuple.Formatter.Stringify(this.Item3), ", ",
				STuple.Formatter.Stringify(this.Item4),
				")"
			);
		}

		public override bool Equals(object obj)
		{
			return obj != null && ((IStructuralEquatable)this).Equals(obj, SimilarValueComparer.Default);
		}

		public bool Equals(ITuple other)
		{
			return other != null && ((IStructuralEquatable)this).Equals(other, SimilarValueComparer.Default);
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool Equals(STuple<T1, T2, T3, T4> other)
		{
			return SimilarValueComparer.Default.Equals(this.Item1, other.Item1)
				&& SimilarValueComparer.Default.Equals(this.Item2, other.Item2)
				&& SimilarValueComparer.Default.Equals(this.Item3, other.Item3)
				&& SimilarValueComparer.Default.Equals(this.Item4, other.Item4);
		}

		public override int GetHashCode()
		{
			return ((IStructuralEquatable)this).GetHashCode(SimilarValueComparer.Default);
		}

		public static bool operator ==(STuple<T1, T2, T3, T4> left, STuple<T1, T2, T3, T4> right)
		{
			var comparer = SimilarValueComparer.Default;
			return comparer.Equals(left.Item1, right.Item1)
				&& comparer.Equals(left.Item2, right.Item2)
				&& comparer.Equals(left.Item3, right.Item3)
				&& comparer.Equals(left.Item4, right.Item4);
		}

		public static bool operator !=(STuple<T1, T2, T3, T4> left, STuple<T1, T2, T3, T4> right)
		{
			var comparer = SimilarValueComparer.Default;
			return !comparer.Equals(left.Item1, right.Item1)
				|| !comparer.Equals(left.Item2, right.Item2)
				|| !comparer.Equals(left.Item3, right.Item3)
				|| !comparer.Equals(left.Item4, right.Item4);
		}

		bool IStructuralEquatable.Equals(object other, IEqualityComparer comparer)
		{
			if (other == null) return false;
			if (other is STuple<T1, T2, T3, T4> stuple)
			{
				return comparer.Equals(this.Item1, stuple.Item1)
					&& comparer.Equals(this.Item2, stuple.Item2)
					&& comparer.Equals(this.Item3, stuple.Item3)
					&& comparer.Equals(this.Item4, stuple.Item4);
			}
			if (other is ValueTuple<T1, T2, T3, T4> vtuple)
			{
				return comparer.Equals(this.Item1, vtuple.Item1)
					&& comparer.Equals(this.Item2, vtuple.Item2)
					&& comparer.Equals(this.Item3, vtuple.Item3)
					&& comparer.Equals(this.Item4, vtuple.Item4);
			}
			return TupleHelpers.Equals(this, other, comparer);
		}

		int IStructuralEquatable.GetHashCode(IEqualityComparer comparer)
		{
			return HashCodes.Combine(
				comparer.GetHashCode(this.Item1),
				comparer.GetHashCode(this.Item2),
				comparer.GetHashCode(this.Item3),
				comparer.GetHashCode(this.Item4)
			);
		}

		[Pure]
		public static implicit operator STuple<T1, T2, T3, T4>([NotNull] Tuple<T1, T2, T3, T4> t)
		{
			Contract.NotNull(t, nameof(t));
			return new STuple<T1, T2, T3, T4>(t.Item1, t.Item2, t.Item3, t.Item4);
		}

		[Pure, NotNull]
		public static explicit operator Tuple<T1, T2, T3, T4>(STuple<T1, T2, T3, T4> t)
		{
			return new Tuple<T1, T2, T3, T4>(t.Item1, t.Item2, t.Item3, t.Item4);
		}

		public void Fill(ref (T1, T2, T3, T4) t)
		{
			t.Item1 = this.Item1;
			t.Item2 = this.Item2;
			t.Item3 = this.Item3;
			t.Item4 = this.Item4;
		}

		/// <summary>Appends the items of a tuple at the end of the current tuple.</summary>
		/// <param name="tuple">Tuple whose items are to be appended at the end</param>
		/// <returns>New tuple composed of the current tuple's items, followed by <paramref name="tuple"/>'s items</returns>
		[Pure]
		public STuple<T1, T2, T3, T4, T5> Concat<T5>(ValueTuple<T5> tuple)
		{
			return new STuple<T1, T2, T3, T4, T5>(this.Item1, this.Item2, this.Item3, this.Item4, tuple.Item1);
		}

		/// <summary>Appends the items of a tuple at the end of the current tuple.</summary>
		/// <param name="tuple">Tuple whose items are to be appended at the end</param>
		/// <returns>New tuple composed of the current tuple's items, followed by <paramref name="tuple"/>'s items</returns>
		[Pure]
		public STuple<T1, T2, T3, T4, T5, T6> Concat<T5, T6>((T5, T6) tuple)
		{
			return new STuple<T1, T2, T3, T4, T5, T6>(this.Item1, this.Item2, this.Item3, this.Item4, tuple.Item1, tuple.Item2);
		}

		[Pure]
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public (T1, T2, T3, T4) ToValueTuple()
		{
			return (this.Item1, this.Item2, this.Item3, this.Item4);
		}

		[Pure]
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static implicit operator STuple<T1, T2, T3, T4>((T1, T2, T3, T4) t)
		{
			return new STuple<T1, T2, T3, T4>(t.Item1, t.Item2, t.Item3, t.Item4);
		}

		[Pure]
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static implicit operator (T1, T2, T3, T4) (STuple<T1, T2, T3, T4> t)
		{
			return (t.Item1, t.Item2, t.Item3, t.Item4);
		}

		[Pure]
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		bool IEquatable<(T1, T2, T3, T4)>.Equals((T1, T2, T3, T4) other)
		{
			return SimilarValueComparer.Default.Equals(this.Item1, this.Item1)
				&& SimilarValueComparer.Default.Equals(this.Item2, this.Item2)
				&& SimilarValueComparer.Default.Equals(this.Item3, this.Item3)
				&& SimilarValueComparer.Default.Equals(this.Item4, this.Item4);
		}

		public static bool operator ==(STuple<T1, T2, T3, T4> left, (T1, T2, T3, T4) right)
		{
			return SimilarValueComparer.Default.Equals(left.Item1, right.Item1)
				&& SimilarValueComparer.Default.Equals(left.Item2, right.Item2)
				&& SimilarValueComparer.Default.Equals(left.Item3, right.Item3)
				&& SimilarValueComparer.Default.Equals(left.Item4, right.Item4);
		}

		public static bool operator ==((T1, T2, T3, T4) left, STuple<T1, T2, T3, T4> right)
		{
			return SimilarValueComparer.Default.Equals(left.Item1, right.Item1)
				&& SimilarValueComparer.Default.Equals(left.Item2, right.Item2)
				&& SimilarValueComparer.Default.Equals(left.Item3, right.Item3)
				&& SimilarValueComparer.Default.Equals(left.Item4, right.Item4);
		}

		public static bool operator !=(STuple<T1, T2, T3, T4> left, (T1, T2, T3, T4) right)
		{
			return !SimilarValueComparer.Default.Equals(left.Item1, right.Item1)
				|| !SimilarValueComparer.Default.Equals(left.Item2, right.Item2)
				|| !SimilarValueComparer.Default.Equals(left.Item3, right.Item3)
				|| !SimilarValueComparer.Default.Equals(left.Item4, right.Item4);
		}

		public static bool operator !=((T1, T2, T3, T4) left, STuple<T1, T2, T3, T4> right)
		{
			return !SimilarValueComparer.Default.Equals(left.Item1, right.Item1)
				|| !SimilarValueComparer.Default.Equals(left.Item2, right.Item2)
				|| !SimilarValueComparer.Default.Equals(left.Item3, right.Item3)
				|| !SimilarValueComparer.Default.Equals(left.Item4, right.Item4);
		}

		public sealed class Comparer : IComparer<STuple<T1, T2, T3, T4>>
		{

			public static Comparer Default { [NotNull] get; } = new Comparer();

			private static readonly Comparer<T1> Comparer1 = Comparer<T1>.Default;
			private static readonly Comparer<T2> Comparer2 = Comparer<T2>.Default;
			private static readonly Comparer<T3> Comparer3 = Comparer<T3>.Default;
			private static readonly Comparer<T4> Comparer4 = Comparer<T4>.Default;

			private Comparer() { }

			public int Compare(STuple<T1, T2, T3, T4> x, STuple<T1, T2, T3, T4> y)
			{
				int cmp = Comparer1.Compare(x.Item1, y.Item1);
				if (cmp == 0) cmp = Comparer2.Compare(x.Item2, y.Item2);
				if (cmp == 0) cmp = Comparer3.Compare(x.Item3, y.Item3);
				if (cmp == 0) cmp = Comparer4.Compare(x.Item4, y.Item4);
				return cmp;
			}

		}

		public sealed class EqualityComparer : IEqualityComparer<STuple<T1, T2, T3, T4>>
		{

			public static EqualityComparer Default { [NotNull] get; } = new EqualityComparer();

			private static readonly EqualityComparer<T1> Comparer1 = EqualityComparer<T1>.Default;
			private static readonly EqualityComparer<T2> Comparer2 = EqualityComparer<T2>.Default;
			private static readonly EqualityComparer<T3> Comparer3 = EqualityComparer<T3>.Default;
			private static readonly EqualityComparer<T4> Comparer4 = EqualityComparer<T4>.Default;

			private EqualityComparer() { }

			public bool Equals(STuple<T1, T2, T3, T4> x, STuple<T1, T2, T3, T4> y)
			{
				return Comparer1.Equals(x.Item1, y.Item1)
					&& Comparer2.Equals(x.Item2, y.Item2)
					&& Comparer3.Equals(x.Item3, y.Item3)
					&& Comparer4.Equals(x.Item4, y.Item4);
			}

			public int GetHashCode(STuple<T1, T2, T3, T4> obj)
			{
				return HashCodes.Combine(
					Comparer1.GetHashCode(obj.Item1),
					Comparer2.GetHashCode(obj.Item2),
					Comparer3.GetHashCode(obj.Item3),
					Comparer4.GetHashCode(obj.Item4)
				);
			}
		}

	}

}
