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

#pragma warning disable IL2091 // Target generic argument does not satisfy 'DynamicallyAccessedMembersAttribute' in target method or type. The generic parameter of the source method or type does not have matching annotations.

namespace SnowBank.Data.Tuples
{
	using System.Collections;
	using System.ComponentModel;
	using SnowBank.Buffers.Text;
	using SnowBank.Data.Tuples.Binary;
	using SnowBank.Runtime.Converters;

	/// <summary>Tuple that holds a pair of items</summary>
	/// <typeparam name="T1">Type of the first item</typeparam>
	/// <typeparam name="T2">Type of the second item</typeparam>
	[ImmutableObject(true), DebuggerDisplay("{ToString(),nq}")]
	[PublicAPI]
	[DebuggerNonUserCode]
	public readonly struct STuple<T1, T2> : IVarTuple
		, IEquatable<STuple<T1, T2>>, IComparable<STuple<T1, T2>>
		, IEquatable<(T1, T2)>, IComparable<(T1, T2)>
		, IComparable
		, ITupleSpanPackable, ITupleFormattable
	{
		// This is mostly used by code that create a lot of temporary pair, to reduce the pressure on the Garbage Collector by allocating them on the stack.
		// Please note that if you return an STuple<T> as an ITuple, it will be boxed by the CLR and all memory gains will be lost

		/// <summary>First element of the pair</summary>
		public readonly T1 Item1;

		/// <summary>Second element of the pair</summary>
		public readonly T2 Item2;

		[DebuggerStepThrough]
		public STuple(T1 item1, T2 item2)
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


		/// <inheritdoc />
		int ITuple.Length => 2;

		/// <inheritdoc />
		object? ITuple.this[int index] => ((IVarTuple) this)[index];

		public IVarTuple this[int? fromIncluded, int? toExcluded]
		{
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get => TupleHelpers.Splice(this, fromIncluded, toExcluded);
		}

		object? IVarTuple.this[Index index] => index.GetOffset(2) switch
		{
			0 => this.Item1,
			1 => this.Item2,
			_ => TupleHelpers.FailIndexOutOfRange<object?>(index.Value, 2)
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

		/// <summary>Return the typed value of an item of the tuple, given its position</summary>
		/// <typeparam name="TItem">Expected type of the item</typeparam>
		/// <param name="index">Position of the item (if negative, means relative from the end)</param>
		/// <returns>Value of the item at position <paramref name="index"/>, adapted into type <typeparamref name="TItem"/>.</returns>
		public TItem? Get<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TItem>(int index)
		{
			return index switch
			{
				0  => TypeConverters.Convert<T1, TItem?>(this.Item1),
				1  => TypeConverters.Convert<T2, TItem?>(this.Item2),
				-1 => TypeConverters.Convert<T2, TItem?>(this.Item2),
				-2 => TypeConverters.Convert<T1, TItem?>(this.Item1),
				_  => TupleHelpers.FailIndexOutOfRange<TItem>(index, 2)
			};
		}

		TItem? IVarTuple.GetFirst<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TItem>()
			where TItem : default => TypeConverters.Convert<T1, TItem?>(this.Item1);

		TItem? IVarTuple.GetLast<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TItem>()
			where TItem : default => TypeConverters.Convert<T2, TItem?>(this.Item2);

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
			get => new(this.Item2);
		}

		IVarTuple IVarTuple.Append<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T3>(T3 value) where T3 : default
		{
			return new STuple<T1, T2, T3>(this.Item1, this.Item2, value);
		}

		/// <summary>Appends a single new item at the end of the current tuple.</summary>
		/// <param name="value">Value that will be added as an embedded item</param>
		/// <returns>New tuple with one extra item</returns>
		/// <remarks>If <paramref name="value"/> is a tuple, and you want to append the *items* of this tuple, and not the tuple itself, please call <see cref="Concat"/>!</remarks>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public STuple<T1, T2, T3> Append<T3>(T3 value)
		{
			return new STuple<T1, T2, T3>(this.Item1, this.Item2, value);
			// Note: By create a STuple<T1, T2, T3> we risk an explosion of the number of combinations of Ts which could potentially cause problems at runtime (too many variants of the same generic types).
			// ex: if we have N possible types, then there could be N^3 possible variants of STuple<T1, T2, T3> that the JIT has to deal with.
			// => if this starts becoming a problem, then we should return a list tuple !
		}

		/// <summary>Appends two new items at the end of the current tuple.</summary>
		/// <param name="value1">First item that will be added as an embedded item</param>
		/// <param name="value2">Second item that will be added as an embedded item</param>
		/// <returns>New tuple with two extra item</returns>
		/// <remarks>If any of <paramref name="value1"/> or <paramref name="value2"/> is a tuple, and you want to append the *items* of this tuple, and not the tuple itself, please call <see cref="Concat"/>!</remarks>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public STuple<T1, T2, T3, T4> Append<T3, T4>(T3 value1, T4 value2)
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
		public void Deconstruct(out T1 item1, out T2 item2)
		{
			item1 = this.Item1;
			item2 = this.Item2;
		}

		/// <summary>Execute a lambda Action with the content of this tuple</summary>
		/// <param name="lambda">Action that will be passed the content of this tuple as parameters</param>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void With(Action<T1, T2> lambda)
		{
			lambda(this.Item1, this.Item2);
		}

		/// <summary>Execute a lambda Function with the content of this tuple</summary>
		/// <param name="lambda">Action that will be passed the content of this tuple as parameters</param>
		/// <returns>Result of calling <paramref name="lambda"/> with the items of this tuple</returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public TItem With<TItem>(Func<T1, T2, TItem> lambda)
		{
			return lambda(this.Item1, this.Item2);
		}

		/// <inheritdoc />
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		void ITuplePackable.PackTo(TupleWriter writer)
		{
			TuplePackers.SerializeTo<T1>(writer, this.Item1);
			TuplePackers.SerializeTo<T2>(writer, this.Item2);
		}

		/// <inheritdoc />
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		bool ITupleSpanPackable.TryPackTo(ref TupleSpanWriter writer)
		{
			return TuplePackers.TrySerializeTo<T1>(ref writer, this.Item1)
				&& TuplePackers.TrySerializeTo<T2>(ref writer, this.Item2);
		}

		/// <inheritdoc />
		int ITupleFormattable.AppendItemsTo(ref FastStringBuilder sb)
		{
			sb.Append(STuple.Formatter.Stringify(this.Item1));
			sb.Append(", ");
			sb.Append(STuple.Formatter.Stringify(this.Item2));
			return 2;
		}

		public IEnumerator<object?> GetEnumerator()
		{
			yield return this.Item1;
			yield return this.Item2;
		}

		IEnumerator IEnumerable.GetEnumerator()
		{
			return GetEnumerator();
		}

		public override string ToString()
		{
			var sb = new FastStringBuilder(stackalloc char[128]);
			sb.Append('(');
			STuple.Formatter.StringifyTo(ref sb, this.Item1);
			sb.Append(", ");
			STuple.Formatter.StringifyTo(ref sb, this.Item2);
			sb.Append(')');
			return sb.ToString();
		}

		public override bool Equals(object? obj)
		{
			return obj is not null && ((IStructuralEquatable) this).Equals(obj, SimilarValueComparer.Default);
		}

		public bool Equals(IVarTuple? other)
		{
			return other is not null && ((IStructuralEquatable) this).Equals(other, SimilarValueComparer.Default);
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool Equals(STuple<T1, T2> other) => EqualityComparer.Equals(in this, in other);

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool Equals((T1, T2) other) => EqualityComparer.Equals(in this, in other);

		public override int GetHashCode() => EqualityComparer.GetHashCode(in this);

		/// <inheritdoc />
		public int CompareTo(STuple<T1, T2> other) => Comparer.Compare(in this, in other);

		/// <inheritdoc />
		public int CompareTo(ValueTuple<T1, T2> other) => Comparer.Compare(in this, in other);

		/// <inheritdoc />
		public int CompareTo(IVarTuple? other) => other switch
		{
			null => +1,
			STuple<T1, T2> t => Comparer.Compare(in this, in t),
			_ => TupleHelpers.Compare(this, other, SimilarValueComparer.Default),
		};

		int IComparable.CompareTo(object? other) => other switch
		{
			null => +1,
			STuple<T1, T2> t => Comparer.Compare(in this, in t),
			ValueTuple<T1, T2> t => Comparer.Compare(in this, in t),
			_ => TupleHelpers.Compare(in this, other, SimilarValueComparer.Default),
		};

		int IStructuralComparable.CompareTo(object? other, IComparer comparer) => other switch
		{
			STuple<T1, T2> t => Comparer.Compare(in this, in t),
			ValueTuple<T1, T2> t => Comparer.Compare(in this, in t),
			_ => TupleHelpers.Compare(in this, other, comparer),
		};

		public static bool operator ==(STuple<T1, T2> left, STuple<T1, T2> right)
			=> EqualityComparer.Equals(in left, in right);

		public static bool operator !=(STuple<T1, T2> left, STuple<T1, T2> right)
			=> !EqualityComparer.Equals(in left, in right);

		public static bool operator ==(STuple<T1, T2> left, (T1, T2) right)
			=> EqualityComparer.Equals(in left, in right);

		public static bool operator !=(STuple<T1, T2> left, (T1, T2) right)
			=> !EqualityComparer.Equals(in left, in right);

		public static bool operator ==((T1, T2) left, STuple<T1, T2> right)
			=> EqualityComparer.Equals(in right, in left);

		public static bool operator !=((T1, T2) left, STuple<T1, T2> right)
			=> !EqualityComparer.Equals(in right, in left);

		bool IStructuralEquatable.Equals(object? other, IEqualityComparer comparer) => other switch
		{
			null => false,
			STuple<T1, T2> t => comparer.Equals(this.Item1, t.Item1) && comparer.Equals(this.Item2, t.Item2),
			ValueTuple<T1, T2> t => comparer.Equals(this.Item1, t.Item1) && comparer.Equals(this.Item2, t.Item2),
			_ => TupleHelpers.Equals(this, other, comparer)
		};

		int IStructuralEquatable.GetHashCode(IEqualityComparer comparer)
		{
			return TupleHelpers.CombineHashCodes(
				TupleHelpers.ComputeHashCode(this.Item1, comparer),
				TupleHelpers.ComputeHashCode(this.Item2, comparer)
			);
		}

		int IVarTuple.GetItemHashCode(int index, IEqualityComparer comparer)
		{
			switch (index)
			{
				case 0: return TupleHelpers.ComputeHashCode(this.Item1, comparer);
				case 1: return TupleHelpers.ComputeHashCode(this.Item2, comparer);
				default: throw new IndexOutOfRangeException();
			}
		}

		[Pure]
		public static implicit operator STuple<T1, T2>(Tuple<T1, T2> t)
		{
			Contract.NotNull(t);
			return new(t.Item1, t.Item2);
		}

		[Pure]
		public static explicit operator Tuple<T1, T2>(STuple<T1, T2> t)
		{
			return new(t.Item1, t.Item2);
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
			return new(this.Item1, this.Item2, tuple.Item1);
		}

		/// <summary>Appends the items of a tuple at the end of the current tuple.</summary>
		/// <param name="tuple">Tuple whose items are to be appended at the end</param>
		/// <returns>New tuple composed of the current tuple's items, followed by <paramref name="tuple"/>'s items</returns>
		[Pure]
		public STuple<T1, T2, T3, T4> Concat<T3, T4>((T3, T4) tuple)
		{
			return new(this.Item1, this.Item2, tuple.Item1, tuple.Item2);
		}

		/// <summary>Appends the items of a tuple at the end of the current tuple.</summary>
		/// <param name="tuple">Tuple whose items are to be appended at the end</param>
		/// <returns>New tuple composed of the current tuple's items, followed by <paramref name="tuple"/>'s items</returns>
		[Pure]
		public STuple<T1, T2, T3, T4, T5> Concat<T3, T4, T5>((T3, T4, T5) tuple)
		{
			return new(this.Item1, this.Item2, tuple.Item1, tuple.Item2, tuple.Item3);
		}

		/// <summary>Appends the items of a tuple at the end of the current tuple.</summary>
		/// <param name="tuple">Tuple whose items are to be appended at the end</param>
		/// <returns>New tuple composed of the current tuple's items, followed by <paramref name="tuple"/>'s items</returns>
		[Pure]
		public STuple<T1, T2, T3, T4, T5, T6> Concat<T3, T4, T5, T6>((T3, T4, T5, T6) tuple)
		{
			return new(this.Item1, this.Item2, tuple.Item1, tuple.Item2, tuple.Item3, tuple.Item4);
		}

		/// <summary>Appends the items of a tuple at the end of the current tuple.</summary>
		/// <param name="tuple">Tuple whose items are to be appended at the end</param>
		/// <returns>New tuple composed of the current tuple's items, followed by <paramref name="tuple"/>'s items</returns>
		[Pure]
		public STuple<T1, T2, T3, T4, T5, T6, T7> Concat<T3, T4, T5, T6, T7>((T3, T4, T5, T6, T7) tuple)
		{
			return new(this.Item1, this.Item2, tuple.Item1, tuple.Item2, tuple.Item3, tuple.Item4, tuple.Item5);
		}

		/// <summary>Appends the items of a tuple at the end of the current tuple.</summary>
		/// <param name="tuple">Tuple whose items are to be appended at the end</param>
		/// <returns>New tuple composed of the current tuple's items, followed by <paramref name="tuple"/>'s items</returns>
		[Pure]
		public STuple<T1, T2, T3, T4, T5, T6, T7, T8> Concat<T3, T4, T5, T6, T7, T8>((T3, T4, T5, T6, T7, T8) tuple)
		{
			return new(this.Item1, this.Item2, tuple.Item1, tuple.Item2, tuple.Item3, tuple.Item4, tuple.Item5, tuple.Item6);
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

		public sealed class Comparer : IComparer<STuple<T1, T2>>
		{

			public static Comparer Default { get; } = new();

			private static readonly Comparer<T1> Comparer1 = Comparer<T1>.Default;
			private static readonly Comparer<T2> Comparer2 = Comparer<T2>.Default;

			private Comparer() { }

			public int Compare(STuple<T1, T2> x, STuple<T1, T2> y)
			{
				int cmp = Comparer1.Compare(x.Item1, y.Item1);
				if (cmp == 0) cmp = Comparer2.Compare(x.Item2, y.Item2);
				return cmp;
			}

			public static int Compare(in STuple<T1, T2> x, in STuple<T1, T2> y)
			{
				int cmp = Comparer1.Compare(x.Item1, y.Item1);
				if (cmp == 0) cmp = Comparer2.Compare(x.Item2, y.Item2);
				return cmp;
			}

			public static int Compare(in STuple<T1, T2> x, in (T1, T2) y)
			{
				int cmp = Comparer1.Compare(x.Item1, y.Item1);
				if (cmp == 0) cmp = Comparer2.Compare(x.Item2, y.Item2);
				return cmp;
			}

		}

		public sealed class EqualityComparer : IEqualityComparer<STuple<T1, T2>>
		{

			public static EqualityComparer Default { get; } = new();

			private static readonly EqualityComparer<T1> Comparer1 = EqualityComparer<T1>.Default;
			private static readonly EqualityComparer<T2> Comparer2 = EqualityComparer<T2>.Default;

			private EqualityComparer() { }

			public bool Equals(STuple<T1, T2> x, STuple<T1, T2> y)
			{
				return Comparer1.Equals(x.Item1, y.Item1) 
					&& Comparer2.Equals(x.Item2, y.Item2);
			}

			public static bool Equals(in STuple<T1, T2> x, in STuple<T1, T2> y)
			{
				return Comparer1.Equals(x.Item1, y.Item1) 
					&& Comparer2.Equals(x.Item2, y.Item2);
			}

			public static bool Equals(in STuple<T1, T2> x, in (T1, T2) y)
			{
				return Comparer1.Equals(x.Item1, y.Item1) 
					&& Comparer2.Equals(x.Item2, y.Item2);
			}

			public int GetHashCode(STuple<T1, T2> obj)
			{
				return TupleHelpers.CombineHashCodes(
					obj.Item1 is not null ? Comparer1.GetHashCode(obj.Item1) : -1,
					obj.Item2 is not null ? Comparer2.GetHashCode(obj.Item2) : -1
				);
			}

			public static int GetHashCode(in STuple<T1, T2> obj)
			{
				return TupleHelpers.CombineHashCodes(
					obj.Item1 is not null ? Comparer1.GetHashCode(obj.Item1) : -1,
					obj.Item2 is not null ? Comparer2.GetHashCode(obj.Item2) : -1
				);
			}

		}

	}

}
