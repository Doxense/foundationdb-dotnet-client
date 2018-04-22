#region Copyright (c) 2013-2016, Doxense SAS. All rights reserved.
// See License.MD for license information
#endregion

//#define ENABLE_VALUETUPLES

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

	/// <summary>Tuple that can hold five items</summary>
	/// <typeparam name="T1">Type of the 1st item</typeparam>
	/// <typeparam name="T2">Type of the 2nd item</typeparam>
	/// <typeparam name="T3">Type of the 3rd item</typeparam>
	/// <typeparam name="T4">Type of the 4th item</typeparam>
	/// <typeparam name="T5">Type of the 5th item</typeparam>
	[ImmutableObject(true), DebuggerDisplay("{ToString(),nq}")]
	public struct STuple<T1, T2, T3, T4, T5> : ITuple, ITupleSerializable, IEquatable<STuple<T1, T2, T3, T4, T5>>
#if ENABLE_VALUETUPLES
		, IEquatable<ValueTuple<T1, T2, T3, T4, T5>>
#endif
	{
		// This is mostly used by code that create a lot of temporary quartets, to reduce the pressure on the Garbage Collector by allocating them on the stack.
		// Please note that if you return an STuple<T> as an ITuple, it will be boxed by the CLR and all memory gains will be lost

		/// <summary>First element of the tuple</summary>
		public readonly T1 Item1;
		/// <summary>Second element of the tuple</summary>
		public readonly T2 Item2;
		/// <summary>Third element of the tuple</summary>
		public readonly T3 Item3;
		/// <summary>Fourth element of the tuple</summary>
		public readonly T4 Item4;
		/// <summary>Fifth and last element of the tuple</summary>
		public readonly T5 Item5;

		/// <summary>Create a tuple containing for items</summary>
		[DebuggerStepThrough]
		public STuple(T1 item1, T2 item2, T3 item3, T4 item4, T5 item5)
		{
			this.Item1 = item1;
			this.Item2 = item2;
			this.Item3 = item3;
			this.Item4 = item4;
			this.Item5 = item5;
		}

		/// <summary>Number of items in this tuple</summary>
		public int Count => 5;

		/// <summary>Return the Nth item in this tuple</summary>
		public object this[int index]
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
			[Pure]
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get { return this.Item5; }
		}

		/// <summary>Return a tuple without the first item</summary>
		public STuple<T2, T3, T4, T5> Tail
		{
			[Pure]
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get { return new STuple<T2, T3, T4, T5>(this.Item2, this.Item3, this.Item4, this.Item5); }
		}

		void ITupleSerializable.PackTo(ref TupleWriter writer)
		{
			PackTo(ref writer);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		internal void PackTo(ref TupleWriter writer)
		{
			TupleSerializer<T1, T2, T3, T4, T5>.Default.PackTo(ref writer, ref this);
		}

		ITuple ITuple.Append<T6>(T6 value)
		{
			// the caller doesn't care about the return type, so just box everything into a list tuple
			return new ListTuple(new object[6] { this.Item1, this.Item2, this.Item3, this.Item4, this.Item5, value }, 0, 6);
		}

		/// <summary>Appends a single new item at the end of the current tuple.</summary>
		/// <param name="value">Value that will be added as an embedded item</param>
		/// <returns>New tuple with one extra item</returns>
		/// <remarks>If <paramref name="value"/> is a tuple, and you want to append the *items*  of this tuple, and not the tuple itself, please call <see cref="Concat"/>!</remarks>
		[Pure]
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public STuple<T1, T2, T3, T4, T5, T6> Append<T6>(T6 value)
		{
			return new STuple<T1, T2, T3, T4, T5, T6>(this.Item1, this.Item2, this.Item3, this.Item4, this.Item5, value);
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
			array[offset + 4] = this.Item5;
		}

		[EditorBrowsable(EditorBrowsableState.Never)]
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void Deconstruct(out T1 item1, out T2 item2, out T3 item3, out T4 item4, out T5 item5)
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
		public void With([NotNull] Action<T1, T2, T3, T4, T5> lambda)
		{
			lambda(this.Item1, this.Item2, this.Item3, this.Item4, this.Item5);
		}

		/// <summary>Execute a lambda Function with the content of this tuple</summary>
		/// <param name="lambda">Action that will be passed the content of this tuple as parameters</param>
		/// <returns>Result of calling <paramref name="lambda"/> with the items of this tuple</returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public TItem With<TItem>([NotNull] Func<T1, T2, T3, T4, T5, TItem> lambda)
		{
			return lambda(this.Item1, this.Item2, this.Item3, this.Item4, this.Item5);
		}

		public IEnumerator<object> GetEnumerator()
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

		public override bool Equals(object obj)
		{
			return obj != null && ((IStructuralEquatable)this).Equals(obj, SimilarValueComparer.Default);
		}

		public bool Equals(ITuple other)
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

		bool IStructuralEquatable.Equals(object other, IEqualityComparer comparer)
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
#if ENABLE_VALUETUPLES
			if (other is ValueTuple<T1, T2, T3, T4, T5> vtuple)
			{
				return comparer.Equals(this.Item1, vtuple.Item1)
					&& comparer.Equals(this.Item2, vtuple.Item2)
					&& comparer.Equals(this.Item3, vtuple.Item3)
					&& comparer.Equals(this.Item4, vtuple.Item4)
					&& comparer.Equals(this.Item5, vtuple.Item5);
			}
#endif
			return TupleHelpers.Equals(this, other, comparer);
		}

		int IStructuralEquatable.GetHashCode(IEqualityComparer comparer)
		{
			return HashCodes.Combine(
				comparer.GetHashCode(this.Item1),
				comparer.GetHashCode(this.Item2),
				comparer.GetHashCode(this.Item3),
				comparer.GetHashCode(this.Item4),
				comparer.GetHashCode(this.Item5)
			);
		}

		[Pure]
		public static implicit operator STuple<T1, T2, T3, T4, T5>([NotNull] Tuple<T1, T2, T3, T4, T5> t)
		{
			Contract.NotNull(t, nameof(t));
			return new STuple<T1, T2, T3, T4, T5>(t.Item1, t.Item2, t.Item3, t.Item4, t.Item5);
		}

		[Pure, NotNull]
		public static explicit operator Tuple<T1, T2, T3, T4, T5>(STuple<T1, T2, T3, T4, T5> t)
		{
			return new Tuple<T1, T2, T3, T4, T5>(t.Item1, t.Item2, t.Item3, t.Item4, t.Item5);
		}

#if ENABLE_VALUETUPLES

		// interop with System.ValueTuple<T1, T2, T3, T4, T5>

		public void Fill(ref ValueTuple<T1, T2, T3, T4, T5> t)
		{
			t.Item1 = this.Item1;
			t.Item2 = this.Item2;
			t.Item3 = this.Item3;
			t.Item4 = this.Item4;
			t.Item5 = this.Item5;
		}

		/// <summary>Appends the items of a tuple at the end of the current tuple.</summary>
		/// <param name="tuple">Tuple whose items are to be appended at the end</param>
		/// <returns>New tuple composed of the current tuple's items, followed by <paramref name="tuple"/>'s items</returns>
		[Pure]
		public STuple<T1, T2, T3, T4, T5, T6> Concat<T6>(ValueTuple<T6> tuple)
		{
			return new STuple<T1, T2, T3, T4, T5, T6>(this.Item1, this.Item2, this.Item3, this.Item4, this.Item5, tuple.Item1);
		}

		[Pure]
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public ValueTuple<T1, T2, T3, T4, T5> ToValueTuple()
		{
			return ValueTuple.Create(this.Item1, this.Item2, this.Item3, this.Item4, this.Item5);
		}

		[Pure]
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static implicit operator STuple<T1, T2, T3, T4, T5>(ValueTuple<T1, T2, T3, T4, T5> t)
		{
			return new STuple<T1, T2, T3, T4, T5>(t.Item1, t.Item2, t.Item3, t.Item4, t.Item5);
		}

		[Pure]
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static implicit operator ValueTuple<T1, T2, T3, T4, T5>(STuple<T1, T2, T3, T4, T5> t)
		{
			return ValueTuple.Create(t.Item1, t.Item2, t.Item3, t.Item4, t.Item5);
		}

		[Pure]
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		bool IEquatable<ValueTuple<T1, T2, T3, T4, T5>>.Equals(ValueTuple<T1, T2, T3, T4, T5> other)
		{
			return SimilarValueComparer.Default.Equals(this.Item1, this.Item1)
				&& SimilarValueComparer.Default.Equals(this.Item2, this.Item2)
				&& SimilarValueComparer.Default.Equals(this.Item3, this.Item3)
				&& SimilarValueComparer.Default.Equals(this.Item4, this.Item4)
				&& SimilarValueComparer.Default.Equals(this.Item5, this.Item5);
		}

		public static bool operator ==(STuple<T1, T2, T3, T4, T5> left, ValueTuple<T1, T2, T3, T4, T5> right)
		{
			return SimilarValueComparer.Default.Equals(left.Item1, right.Item1)
				&& SimilarValueComparer.Default.Equals(left.Item2, right.Item2)
				&& SimilarValueComparer.Default.Equals(left.Item3, right.Item3)
				&& SimilarValueComparer.Default.Equals(left.Item4, right.Item4)
				&& SimilarValueComparer.Default.Equals(left.Item5, right.Item5);
		}

		public static bool operator ==(ValueTuple<T1, T2, T3, T4, T5> left, STuple<T1, T2, T3, T4, T5> right)
		{
			return SimilarValueComparer.Default.Equals(left.Item1, right.Item1)
				&& SimilarValueComparer.Default.Equals(left.Item2, right.Item2)
				&& SimilarValueComparer.Default.Equals(left.Item3, right.Item3)
				&& SimilarValueComparer.Default.Equals(left.Item4, right.Item4)
				&& SimilarValueComparer.Default.Equals(left.Item5, right.Item5);
		}

		public static bool operator !=(STuple<T1, T2, T3, T4, T5> left, ValueTuple<T1, T2, T3, T4, T5> right)
		{
			return !SimilarValueComparer.Default.Equals(left.Item1, right.Item1)
				|| !SimilarValueComparer.Default.Equals(left.Item2, right.Item2)
				|| !SimilarValueComparer.Default.Equals(left.Item3, right.Item3)
				|| !SimilarValueComparer.Default.Equals(left.Item4, right.Item4)
				|| !SimilarValueComparer.Default.Equals(left.Item5, right.Item5);
		}

		public static bool operator !=(ValueTuple<T1, T2, T3, T4, T5> left, STuple<T1, T2, T3, T4, T5> right)
		{
			return !SimilarValueComparer.Default.Equals(left.Item1, right.Item1)
				|| !SimilarValueComparer.Default.Equals(left.Item2, right.Item2)
				|| !SimilarValueComparer.Default.Equals(left.Item3, right.Item3)
				|| !SimilarValueComparer.Default.Equals(left.Item4, right.Item4)
				|| !SimilarValueComparer.Default.Equals(left.Item5, right.Item5);
		}

#endif

		public sealed class Comparer : IComparer<STuple<T1, T2, T3, T4, T5>>
		{

			public static Comparer Default { [NotNull] get; } = new Comparer();

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

			public static EqualityComparer Default { [NotNull] get; } = new EqualityComparer();

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
				return HashCodes.Combine(
					Comparer1.GetHashCode(obj.Item1),
					Comparer2.GetHashCode(obj.Item2),
					Comparer3.GetHashCode(obj.Item3),
					Comparer4.GetHashCode(obj.Item4),
					Comparer5.GetHashCode(obj.Item5)
				);
			}
		}

	}

}
