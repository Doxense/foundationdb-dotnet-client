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
	using SnowBank.Data.Binary;
	using SnowBank.Data.Tuples.Binary;
	using SnowBank.Runtime.Converters;

	/// <summary>Tuple that can hold six items</summary>
	/// <typeparam name="T1">Type of the 1st item</typeparam>
	/// <typeparam name="T2">Type of the 2nd item</typeparam>
	/// <typeparam name="T3">Type of the 3rd item</typeparam>
	/// <typeparam name="T4">Type of the 4th item</typeparam>
	/// <typeparam name="T5">Type of the 5th item</typeparam>
	/// <typeparam name="T6">Type of the 6th item</typeparam>
	[ImmutableObject(true), DebuggerDisplay("{ToString(),nq}")]
	[PublicAPI]
	[DebuggerNonUserCode]
	public readonly struct STuple<T1, T2, T3, T4, T5, T6> : IVarTuple
		, IEquatable<STuple<T1, T2, T3, T4, T5, T6>>, IComparable<STuple<T1, T2, T3, T4, T5, T6>>
		, IEquatable<(T1, T2, T3, T4, T5, T6)>, IComparable<(T1, T2, T3, T4, T5, T6)>
		, IComparable
		, ITupleSpanPackable
		, ITupleFormattable
		, ISpanFormattable
		, ISpanEncodable
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

		/// <summary>Fifth element of the tuple</summary>
		public readonly T5 Item5;

		/// <summary>Sixth and last element of the tuple</summary>
		public readonly T6 Item6;

		/// <summary>Create a tuple containing for items</summary>
		[DebuggerStepThrough]
		public STuple(T1 item1, T2 item2, T3 item3, T4 item4, T5 item5, T6 item6)
		{
			this.Item1 = item1;
			this.Item2 = item2;
			this.Item3 = item3;
			this.Item4 = item4;
			this.Item5 = item5;
			this.Item6 = item6;
		}

		/// <summary>Number of items in this tuple</summary>
		public int Count => 6;

		/// <inheritdoc />
		object? IReadOnlyList<object?>.this[int index] => ((IVarTuple) this)[index];

		/// <inheritdoc />
		object? IVarTuple.this[int index]
		{
			get
			{
				switch (index)
				{
					case 0: case -6: return this.Item1;
					case 1: case -5: return this.Item2;
					case 2: case -4: return this.Item3;
					case 3: case -3: return this.Item4;
					case 4: case -2: return this.Item5;
					case 5: case -1: return this.Item6;
					default: return TupleHelpers.FailIndexOutOfRange<object>(index, 6);
				}
			}
		}

		/// <inheritdoc />
		int ITuple.Length => 6;

		/// <inheritdoc />
		object? ITuple.this[int index] => ((IVarTuple) this)[index];

		/// <inheritdoc />
		public IVarTuple this[int? fromIncluded, int? toExcluded]
		{
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get => TupleHelpers.Splice(this, fromIncluded, toExcluded);
		}

		/// <inheritdoc />
		object? IVarTuple.this[Index index] => index.GetOffset(6) switch
		{
			0 => this.Item1,
			1 => this.Item2,
			2 => this.Item3,
			3 => this.Item4,
			4 => this.Item5,
			5 => this.Item6,
			_ => TupleHelpers.FailIndexOutOfRange<object>(index.Value, 6)
		};

		/// <inheritdoc />
		public IVarTuple this[Range range]
		{
			get
			{
				(int offset, int count) = range.GetOffsetAndLength(6);
				return count switch
				{
					0 => STuple.Empty,
					1 => (offset switch
					{
						0 => STuple.Create(this.Item1),
						1 => STuple.Create(this.Item2),
						2 => STuple.Create(this.Item3),
						3 => STuple.Create(this.Item4),
						4 => STuple.Create(this.Item5),
						_ => STuple.Create(this.Item6)
					}),
					2 => (offset switch
					{
						0 => STuple.Create(this.Item1, this.Item2),
						1 => STuple.Create(this.Item2, this.Item3),
						2 => STuple.Create(this.Item3, this.Item4),
						3 => STuple.Create(this.Item4, this.Item5),
						_ => STuple.Create(this.Item5, this.Item6)
					}),
					3 => (offset switch
					{
						0 => STuple.Create(this.Item1, this.Item2, this.Item3),
						1 => STuple.Create(this.Item2, this.Item3, this.Item4),
						2 => STuple.Create(this.Item3, this.Item4, this.Item5),
						_ => STuple.Create(this.Item4, this.Item5, this.Item6)
					}),
					4 => (offset switch
					{
						0 => STuple.Create(this.Item1, this.Item2, this.Item3, this.Item4),
						1 => STuple.Create(this.Item2, this.Item3, this.Item4, this.Item5),
						_ => STuple.Create(this.Item3, this.Item4, this.Item5, this.Item6)
					}),
					5 => (offset switch
					{
						0 => STuple.Create(this.Item1, this.Item2, this.Item3, this.Item4, this.Item5),
						_ => STuple.Create(this.Item2, this.Item3, this.Item4, this.Item5, this.Item6)
					}),
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
			switch(index)
			{
				case 0: case -6: return TypeConverters.Convert<T1, TItem?>(this.Item1);
				case 1: case -5: return TypeConverters.Convert<T2, TItem?>(this.Item2);
				case 2: case -4: return TypeConverters.Convert<T3, TItem?>(this.Item3);
				case 3: case -3: return TypeConverters.Convert<T4, TItem?>(this.Item4);
				case 4: case -2: return TypeConverters.Convert<T5, TItem?>(this.Item5);
				case 5: case -1: return TypeConverters.Convert<T6, TItem?>(this.Item6);
				default: return TupleHelpers.FailIndexOutOfRange<TItem>(index, 6);
			}
		}

		/// <inheritdoc />
		TItem? IVarTuple.GetFirst<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TItem>()
			where TItem : default => TypeConverters.Convert<T1, TItem?>(this.Item1);

		/// <inheritdoc />
		TItem? IVarTuple.GetLast<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TItem>()
			where TItem : default => TypeConverters.Convert<T6, TItem?>(this.Item6);

		/// <summary>Value of the last item in the tuple</summary>
		public T6 Last
		{
			[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
			[return: MaybeNull]
			get => this.Item6;
		}

		/// <summary>Tuple without the first item</summary>
		public STuple<T2, T3, T4, T5, T6> Tail
		{
			[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
			get => new(this.Item2, this.Item3, this.Item4, this.Item5, this.Item6);
		}

		/// <summary>Appends a single new item at the end of the current tuple.</summary>
		/// <param name="value">Value that will be added as an embedded item</param>
		/// <returns>New tuple with one extra item</returns>
		/// <remarks>If <paramref name="value"/> is a tuple, and you want to append the *items*  of this tuple, and not the tuple itself, please call <see cref="Concat"/>!</remarks>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		IVarTuple IVarTuple.Append<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T7>(T7 value)
		{
			// the caller probably cares about the return type, since it is using a struct, but whatever tuple type we use will end up boxing this tuple on the heap, and we will lose type information.
			// but, by returning a LinkedTuple<T6>, the tuple will still remember the exact type, and efficiently serializer/convert the values (without having to guess the type)
			return new LinkedTuple<T7>(this, value);
		}

		/// <summary>Appends a single new item at the end of the current tuple.</summary>
		/// <param name="value">Value that will be added as an embedded item</param>
		/// <returns>New tuple with one extra item</returns>
		/// <remarks>If <paramref name="value"/> is a tuple, and you want to append the *items*  of this tuple, and not the tuple itself, please call <see cref="Concat"/>!</remarks>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public STuple<T1, T2, T3, T4, T5, T6, T7> Append<T7>(T7 value)
		{
			return new(this.Item1, this.Item2, this.Item3, this.Item4, this.Item5, this.Item6, value);
		}

		/// <summary>Appends two new items at the end of the current tuple.</summary>
		/// <param name="value1">First item that will be added as an embedded item</param>
		/// <param name="value2">Second item that will be added as an embedded item</param>
		/// <returns>New tuple with two extra item</returns>
		/// <remarks>If any of <paramref name="value1"/> or <paramref name="value2"/> is a tuple, and you want to append the *items*  of this tuple, and not the tuple itself, please call <see cref="Concat"/>!</remarks>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public STuple<T1, T2, T3, T4, T5, T6, T7, T8> Append<T7, T8>(T7 value1, T8 value2)
		{
			return new(this.Item1, this.Item2, this.Item3, this.Item4, this.Item5, this.Item6, value1, value2);
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
			array[offset + 5] = this.Item6;
		}

		/// <summary>Deconstructs this tuple into its individual elements</summary>
		[EditorBrowsable(EditorBrowsableState.Never)]
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void Deconstruct(out T1 item1, out T2 item2, out T3 item3, out T4 item4, out T5 item5, out T6 item6)
		{
			item1 = this.Item1;
			item2 = this.Item2;
			item3 = this.Item3;
			item4 = this.Item4;
			item5 = this.Item5;
			item6 = this.Item6;
		}

		/// <summary>Executes a lambda Action with the content of this tuple</summary>
		/// <param name="lambda">Action that will be passed the content of this tuple as parameters</param>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void With(Action<T1, T2, T3, T4, T5, T6> lambda)
		{
			lambda(this.Item1, this.Item2, this.Item3, this.Item4, this.Item5, this.Item6);
		}

		/// <summary>Executes a lambda Function with the content of this tuple</summary>
		/// <param name="lambda">Action that will be passed the content of this tuple as parameters</param>
		/// <returns>Result of calling <paramref name="lambda"/> with the items of this tuple</returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public TItem With<TItem>(Func<T1, T2, T3, T4, T5, T6, TItem> lambda)
		{
			return lambda(this.Item1, this.Item2, this.Item3, this.Item4, this.Item5, this.Item6);
		}

		/// <inheritdoc />
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		void ITuplePackable.PackTo(TupleWriter writer)
		{
			TuplePackers.SerializeTo<T1>(writer, this.Item1);
			TuplePackers.SerializeTo<T2>(writer, this.Item2);
			TuplePackers.SerializeTo<T3>(writer, this.Item3);
			TuplePackers.SerializeTo<T4>(writer, this.Item4);
			TuplePackers.SerializeTo<T5>(writer, this.Item5);
			TuplePackers.SerializeTo<T6>(writer, this.Item6);
		}

		/// <inheritdoc />
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		bool ITupleSpanPackable.TryPackTo(ref TupleSpanWriter writer)
		{
			return TuplePackers.TrySerializeTo<T1>(ref writer, this.Item1)
				&& TuplePackers.TrySerializeTo<T2>(ref writer, this.Item2)
				&& TuplePackers.TrySerializeTo<T3>(ref writer, this.Item3)
				&& TuplePackers.TrySerializeTo<T4>(ref writer, this.Item4)
				&& TuplePackers.TrySerializeTo<T5>(ref writer, this.Item5)
				&& TuplePackers.TrySerializeTo<T6>(ref writer, this.Item6);
		}

		/// <inheritdoc />
		bool ITupleSpanPackable.TryGetSizeHint(bool embedded, out int sizeHint)
		{
			if (!TupleEncoder.TryGetSizeHint(this.Item1, embedded, out var size1)
			 || !TupleEncoder.TryGetSizeHint(this.Item2, embedded, out var size2)
			 || !TupleEncoder.TryGetSizeHint(this.Item3, embedded, out var size3)
			 || !TupleEncoder.TryGetSizeHint(this.Item4, embedded, out var size4)
			 || !TupleEncoder.TryGetSizeHint(this.Item5, embedded, out var size5)
			 || !TupleEncoder.TryGetSizeHint(this.Item6, embedded, out var size6))
			{
				sizeHint = 0;
				return false;
			}
			sizeHint = checked(size1 + size2 + size3 + size4 + size5 + size6 + (embedded ? 2 : 0));
			return true;
		}

		/// <inheritdoc />
		int ITupleFormattable.AppendItemsTo(ref FastStringBuilder sb)
		{
			sb.Append(STuple.Formatter.Stringify(this.Item1));
			sb.Append(", ");
			sb.Append(STuple.Formatter.Stringify(this.Item2));
			sb.Append(", ");
			sb.Append(STuple.Formatter.Stringify(this.Item3));
			sb.Append(", ");
			sb.Append(STuple.Formatter.Stringify(this.Item4));
			sb.Append(", ");
			sb.Append(STuple.Formatter.Stringify(this.Item5));
			sb.Append(", ");
			sb.Append(STuple.Formatter.Stringify(this.Item6));
			return 6;
		}

		/// <inheritdoc />
		public IEnumerator<object?> GetEnumerator()
		{
			yield return this.Item1;
			yield return this.Item2;
			yield return this.Item3;
			yield return this.Item4;
			yield return this.Item5;
			yield return this.Item6;
		}

		/// <inheritdoc />
		IEnumerator IEnumerable.GetEnumerator()
		{
			return GetEnumerator();
		}

		/// <inheritdoc />
		public override string ToString() => ToString(null);

		/// <inheritdoc />
		public string ToString(string? format, IFormatProvider? provider = null)
		{
			var sb = new FastStringBuilder(stackalloc char[128]);
			sb.Append('(');
			STuple.Formatter.StringifyTo(ref sb, this.Item1);
			sb.Append(", ");
			STuple.Formatter.StringifyTo(ref sb, this.Item2);
			sb.Append(", ");
			STuple.Formatter.StringifyTo(ref sb, this.Item3);
			sb.Append(", ");
			STuple.Formatter.StringifyTo(ref sb, this.Item4);
			sb.Append(", ");
			STuple.Formatter.StringifyTo(ref sb, this.Item5);
			sb.Append(", ");
			STuple.Formatter.StringifyTo(ref sb, this.Item6);
			sb.Append(')');
			return sb.ToString();
		}

		/// <inheritdoc />
		public bool TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format = default, IFormatProvider? provider = null)
		{
			var buffer = destination;
			if (!buffer.TryAppendAndAdvance('(')) goto too_small;
			if (!STuple.Formatter.TryStringifyTo(buffer, out charsWritten, this.Item1)) goto too_small;
			buffer = buffer[charsWritten..];
			if (!buffer.TryAppendAndAdvance(", ")) goto too_small;
			if (!STuple.Formatter.TryStringifyTo(buffer, out charsWritten, this.Item2)) goto too_small;
			buffer = buffer[charsWritten..];
			if (!buffer.TryAppendAndAdvance(", ")) goto too_small;
			if (!STuple.Formatter.TryStringifyTo(buffer, out charsWritten, this.Item3)) goto too_small;
			buffer = buffer[charsWritten..];
			if (!buffer.TryAppendAndAdvance(", ")) goto too_small;
			if (!STuple.Formatter.TryStringifyTo(buffer, out charsWritten, this.Item4)) goto too_small;
			buffer = buffer[charsWritten..];
			if (!buffer.TryAppendAndAdvance(", ")) goto too_small;
			if (!STuple.Formatter.TryStringifyTo(buffer, out charsWritten, this.Item5)) goto too_small;
			buffer = buffer[charsWritten..];
			if (!buffer.TryAppendAndAdvance(", ")) goto too_small;
			if (!STuple.Formatter.TryStringifyTo(buffer, out charsWritten, this.Item6)) goto too_small;
			buffer = buffer[charsWritten..];
			if (!buffer.TryAppendAndAdvance(')')) goto too_small;
			charsWritten = destination.Length - buffer.Length;
			return true;
		too_small:
			charsWritten = 0;
			return false;
		}

		/// <inheritdoc />
		public override bool Equals(object? obj)
		{
			return obj != null && ((IStructuralEquatable)this).Equals(obj, SimilarValueComparer.Default);
		}

		/// <inheritdoc />
		public bool Equals(IVarTuple? other)
		{
			return other != null && ((IStructuralEquatable)this).Equals(other, SimilarValueComparer.Default);
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool Equals(STuple<T1, T2, T3, T4, T5, T6> other) => EqualityComparer.Equals(in this, in other);

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool Equals((T1, T2, T3, T4, T5, T6) other) => EqualityComparer.Equals(in this, in other);

		public override int GetHashCode() => EqualityComparer.GetHashCode(in this);

		/// <inheritdoc />
		public int CompareTo(STuple<T1, T2, T3, T4, T5, T6> other) => Comparer.Compare(in this, in other);

		/// <inheritdoc />
		public int CompareTo(ValueTuple<T1, T2, T3, T4, T5, T6> other) => Comparer.Compare(in this, in other);

		/// <inheritdoc />
		public int CompareTo(IVarTuple? other) => other switch
		{
			null => +1,
			STuple<T1, T2, T3, T4, T5, T6> t => Comparer.Compare(in this, in t),
			_ => TupleHelpers.Compare(this, other, SimilarValueComparer.Default),
		};

		int IComparable.CompareTo(object? other) => other switch
		{
			null => +1,
			STuple<T1, T2, T3, T4, T5, T6> t => Comparer.Compare(in this, in t),
			ValueTuple<T1, T2, T3, T4, T5, T6> t => Comparer.Compare(in this, in t),
			_ => TupleHelpers.Compare(in this, other, SimilarValueComparer.Default),
		};

		int IStructuralComparable.CompareTo(object? other, IComparer comparer) => other switch
		{
			STuple<T1, T2, T3, T4, T5, T6> t => Comparer.Compare(in this, in t),
			ValueTuple<T1, T2, T3, T4, T5, T6> t => Comparer.Compare(in this, in t),
			_ => TupleHelpers.Compare(in this, other, comparer),
		};

		public static bool operator ==(STuple<T1, T2, T3, T4, T5, T6> left, STuple<T1, T2, T3, T4, T5, T6> right)
			=> EqualityComparer.Equals(in left, in right);

		public static bool operator !=(STuple<T1, T2, T3, T4, T5, T6> left, STuple<T1, T2, T3, T4, T5, T6> right)
			=> EqualityComparer.Equals(in left, in right);

		public static bool operator ==(STuple<T1, T2, T3, T4, T5, T6> left, (T1, T2, T3, T4, T5, T6) right)
			=> EqualityComparer.Equals(in left, in right);

		public static bool operator !=(STuple<T1, T2, T3, T4, T5, T6> left, (T1, T2, T3, T4, T5, T6) right)
			=> !EqualityComparer.Equals(in left, in right);

		public static bool operator ==((T1, T2, T3, T4, T5, T6) left, STuple<T1, T2, T3, T4, T5, T6> right)
			=> EqualityComparer.Equals(in right, in left);

		public static bool operator !=((T1, T2, T3, T4, T5, T6) left, STuple<T1, T2, T3, T4, T5, T6> right)
			=> !EqualityComparer.Equals(in right, in left);

		/// <inheritdoc />
		bool IStructuralEquatable.Equals(object? other, IEqualityComparer comparer)
		{
			return other switch
			{
				null => false,
				STuple<T1, T2, T3, T4, T5, T6> t => comparer.Equals(this.Item1, t.Item1) && comparer.Equals(this.Item2, t.Item2) && comparer.Equals(this.Item3, t.Item3) && comparer.Equals(this.Item4, t.Item4) && comparer.Equals(this.Item5, t.Item5) && comparer.Equals(this.Item6, t.Item6),
				ValueTuple<T1, T2, T3, T4, T5, T6> t => comparer.Equals(this.Item1, t.Item1) && comparer.Equals(this.Item2, t.Item2) && comparer.Equals(this.Item3, t.Item3) && comparer.Equals(this.Item4, t.Item4) && comparer.Equals(this.Item5, t.Item5) && comparer.Equals(this.Item6, t.Item6),
				_ => TupleHelpers.Equals(this, other, comparer)
			};
		}

		/// <inheritdoc />
		int IStructuralEquatable.GetHashCode(IEqualityComparer comparer)
		{
			return TupleHelpers.CombineHashCodes(
				6,
				TupleHelpers.ComputeHashCode(this.Item1, comparer),
				TupleHelpers.ComputeHashCode(this.Item5, comparer),
				TupleHelpers.ComputeHashCode(this.Item6, comparer)
			);
		}

		/// <inheritdoc />
		int IVarTuple.GetItemHashCode(int index, IEqualityComparer comparer)
		{
			switch (index)
			{
				case 0: return TupleHelpers.ComputeHashCode(this.Item1, comparer);
				case 1: return TupleHelpers.ComputeHashCode(this.Item2, comparer);
				case 2: return TupleHelpers.ComputeHashCode(this.Item3, comparer);
				case 3: return TupleHelpers.ComputeHashCode(this.Item4, comparer);
				case 4: return TupleHelpers.ComputeHashCode(this.Item5, comparer);
				case 5: return TupleHelpers.ComputeHashCode(this.Item6, comparer);
				default: throw new IndexOutOfRangeException();
			}
		}

		[Pure]
		public static implicit operator STuple<T1, T2, T3, T4, T5, T6>(Tuple<T1, T2, T3, T4, T5, T6> tuple)
		{
			Contract.NotNull(tuple);
			return new(tuple.Item1, tuple.Item2, tuple.Item3, tuple.Item4, tuple.Item5, tuple.Item6);
		}

		[Pure]
		public static explicit operator Tuple<T1, T2, T3, T4, T5, T6>(STuple<T1, T2, T3, T4, T5, T6> tuple)
		{
			return new(tuple.Item1, tuple.Item2, tuple.Item3, tuple.Item4, tuple.Item5, tuple.Item6);
		}

		/// <summary>Copies the content of this tuple into the tuple at the specified location</summary>
		public void Fill(ref (T1, T2, T3, T4, T5, T6) t)
		{
			t.Item1 = this.Item1;
			t.Item2 = this.Item2;
			t.Item3 = this.Item3;
			t.Item4 = this.Item4;
			t.Item5 = this.Item5;
			t.Item6 = this.Item6;
		}

		/// <summary>Appends the items of a tuple at the end of the current tuple.</summary>
		/// <param name="tuple">Tuple whose items are to be appended at the end</param>
		/// <returns>New tuple composed of the current tuple items, followed by <paramref name="tuple"/>'s items</returns>
		[Pure]
		public STuple<T1, T2, T3, T4, T5, T6, T7> Concat<T7>(STuple<T7> tuple)
		{
			return new(this.Item1, this.Item2, this.Item3, this.Item4, this.Item5, this.Item6, tuple.Item1);
		}

		/// <summary>Appends the items of a tuple at the end of the current tuple.</summary>
		/// <param name="tuple">Tuple whose items are to be appended at the end</param>
		/// <returns>New tuple composed of the current tuple items, followed by <paramref name="tuple"/>'s items</returns>
		[Pure]
		public STuple<T1, T2, T3, T4, T5, T6, T7> Concat<T7>(ValueTuple<T7> tuple)
		{
			return new(this.Item1, this.Item2, this.Item3, this.Item4, this.Item5, this.Item6, tuple.Item1);
		}

		/// <summary>Appends the items of a tuple at the end of the current tuple.</summary>
		/// <param name="tuple">Tuple whose items are to be appended at the end</param>
		/// <returns>New tuple composed of the current tuple items, followed by <paramref name="tuple"/>'s items</returns>
		[Pure]
		public STuple<T1, T2, T3, T4, T5, T6, T7, T8> Concat<T7, T8>(STuple<T7, T8> tuple)
		{
			return new(this.Item1, this.Item2, this.Item3, this.Item4, this.Item5, this.Item6, tuple.Item1, tuple.Item2);
		}

		/// <summary>Appends the items of a tuple at the end of the current tuple.</summary>
		/// <param name="tuple">Tuple whose items are to be appended at the end</param>
		/// <returns>New tuple composed of the current tuple items, followed by <paramref name="tuple"/>'s items</returns>
		[Pure]
		public STuple<T1, T2, T3, T4, T5, T6, T7, T8> Concat<T7, T8>(ValueTuple<T7, T8> tuple)
		{
			return new(this.Item1, this.Item2, this.Item3, this.Item4, this.Item5, this.Item6, tuple.Item1, tuple.Item2);
		}

		/// <summary>Returns the equivalent <see cref="ValueTuple{T1,T2,T3,T4,T5,T6}"/></summary>
		[Pure]
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public (T1, T2, T3, T4, T5, T6) ToValueTuple()
		{
			return (this.Item1, this.Item2, this.Item3, this.Item4, this.Item5, this.Item6);
		}

		[Pure]
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static implicit operator STuple<T1, T2, T3, T4, T5, T6>((T1, T2, T3, T4, T5, T6) t)
		{
			return new(t.Item1, t.Item2, t.Item3, t.Item4, t.Item5, t.Item6);
		}

		[Pure]
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static implicit operator (T1, T2, T3, T4, T5, T6) (STuple<T1, T2, T3, T4, T5, T6> t)
		{
			return (t.Item1, t.Item2, t.Item3, t.Item4, t.Item5, t.Item6);
		}

		public sealed class Comparer : IComparer<STuple<T1, T2, T3, T4, T5, T6>>
		{

			public static Comparer Default { get; } = new();

			private static readonly Comparer<T1> Comparer1 = Comparer<T1>.Default;
			private static readonly Comparer<T2> Comparer2 = Comparer<T2>.Default;
			private static readonly Comparer<T3> Comparer3 = Comparer<T3>.Default;
			private static readonly Comparer<T4> Comparer4 = Comparer<T4>.Default;
			private static readonly Comparer<T5> Comparer5 = Comparer<T5>.Default;
			private static readonly Comparer<T6> Comparer6 = Comparer<T6>.Default;

			private Comparer() { }

			/// <inheritdoc />
			public int Compare(STuple<T1, T2, T3, T4, T5, T6> x, STuple<T1, T2, T3, T4, T5, T6> y)
			{
				int cmp;
				if ((cmp = Comparer1.Compare(x.Item1, y.Item1)) != 0) return cmp;
				if ((cmp = Comparer2.Compare(x.Item2, y.Item2)) != 0) return cmp;
				if ((cmp = Comparer3.Compare(x.Item3, y.Item3)) != 0) return cmp;
				if ((cmp = Comparer4.Compare(x.Item4, y.Item4)) != 0) return cmp;
				if ((cmp = Comparer5.Compare(x.Item5, y.Item5)) != 0) return cmp;
				return Comparer6.Compare(x.Item6, y.Item6);
			}

			public static int Compare(in STuple<T1, T2, T3, T4, T5, T6> x, in STuple<T1, T2, T3, T4, T5, T6> y)
			{
				int cmp;
				if ((cmp = Comparer1.Compare(x.Item1, y.Item1)) != 0) return cmp;
				if ((cmp = Comparer2.Compare(x.Item2, y.Item2)) != 0) return cmp;
				if ((cmp = Comparer3.Compare(x.Item3, y.Item3)) != 0) return cmp;
				if ((cmp = Comparer4.Compare(x.Item4, y.Item4)) != 0) return cmp;
				if ((cmp = Comparer5.Compare(x.Item5, y.Item5)) != 0) return cmp;
				return Comparer6.Compare(x.Item6, y.Item6);
			}

			public static int Compare(in STuple<T1, T2, T3, T4, T5, T6> x, in (T1, T2, T3, T4, T5, T6) y)
			{
				int cmp;
				if ((cmp = Comparer1.Compare(x.Item1, y.Item1)) != 0) return cmp;
				if ((cmp = Comparer2.Compare(x.Item2, y.Item2)) != 0) return cmp;
				if ((cmp = Comparer3.Compare(x.Item3, y.Item3)) != 0) return cmp;
				if ((cmp = Comparer4.Compare(x.Item4, y.Item4)) != 0) return cmp;
				if ((cmp = Comparer5.Compare(x.Item5, y.Item5)) != 0) return cmp;
				return Comparer6.Compare(x.Item6, y.Item6);
			}

		}

		public sealed class EqualityComparer : IEqualityComparer<STuple<T1, T2, T3, T4, T5, T6>>
		{

			public static EqualityComparer Default { get; } = new();

			private static readonly EqualityComparer<T1> Comparer1 = EqualityComparer<T1>.Default;
			private static readonly EqualityComparer<T2> Comparer2 = EqualityComparer<T2>.Default;
			private static readonly EqualityComparer<T3> Comparer3 = EqualityComparer<T3>.Default;
			private static readonly EqualityComparer<T4> Comparer4 = EqualityComparer<T4>.Default;
			private static readonly EqualityComparer<T5> Comparer5 = EqualityComparer<T5>.Default;
			private static readonly EqualityComparer<T6> Comparer6 = EqualityComparer<T6>.Default;

			private EqualityComparer() { }

			/// <inheritdoc />
			[Pure]
			public bool Equals(STuple<T1, T2, T3, T4, T5, T6> x, STuple<T1, T2, T3, T4, T5, T6> y)
			{
				return Comparer1.Equals(x.Item1, y.Item1)
					&& Comparer2.Equals(x.Item2, y.Item2)
					&& Comparer3.Equals(x.Item3, y.Item3)
					&& Comparer4.Equals(x.Item4, y.Item4)
					&& Comparer5.Equals(x.Item5, y.Item5)
					&& Comparer6.Equals(x.Item6, y.Item6);
			}

			[Pure]
			public static bool Equals(in STuple<T1, T2, T3, T4, T5, T6> x, in STuple<T1, T2, T3, T4, T5, T6> y)
			{
				return Comparer1.Equals(x.Item1, y.Item1)
					&& Comparer2.Equals(x.Item2, y.Item2)
					&& Comparer3.Equals(x.Item3, y.Item3)
					&& Comparer4.Equals(x.Item4, y.Item4)
					&& Comparer5.Equals(x.Item5, y.Item5)
					&& Comparer6.Equals(x.Item6, y.Item6);
			}

			[Pure]
			public static bool Equals(in STuple<T1, T2, T3, T4, T5, T6> x, in (T1, T2, T3, T4, T5, T6) y)
			{
				return Comparer1.Equals(x.Item1, y.Item1)
					&& Comparer2.Equals(x.Item2, y.Item2)
					&& Comparer3.Equals(x.Item3, y.Item3)
					&& Comparer4.Equals(x.Item4, y.Item4)
					&& Comparer5.Equals(x.Item5, y.Item5)
					&& Comparer6.Equals(x.Item6, y.Item6);
			}

			/// <inheritdoc />
			[Pure]
			public int GetHashCode(STuple<T1, T2, T3, T4, T5, T6> obj)
			{
				return TupleHelpers.CombineHashCodes(
					6,
					obj.Item1 is not null ? Comparer1.GetHashCode(obj.Item1) : -1,
					obj.Item5 is not null ? Comparer5.GetHashCode(obj.Item5) : -1,
					obj.Item6 is not null ? Comparer6.GetHashCode(obj.Item6) : -1
				);
			}

			[Pure]
			public static int GetHashCode(in STuple<T1, T2, T3, T4, T5, T6> obj)
			{
				return TupleHelpers.CombineHashCodes(
					6,
					obj.Item1 is not null ? Comparer1.GetHashCode(obj.Item1) : -1,
					obj.Item5 is not null ? Comparer5.GetHashCode(obj.Item5) : -1,
					obj.Item6 is not null ? Comparer6.GetHashCode(obj.Item6) : -1
				);
			}

		}

		#region ISpanEncodable...

		/// <inheritdoc />
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		bool ISpanEncodable.TryGetSpan(out ReadOnlySpan<byte> span) { span = default; return false; }

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		bool ISpanEncodable.TryGetSizeHint(out int sizeHint) => ((ITupleSpanPackable) this).TryGetSizeHint(embedded: false, out sizeHint);

		/// <inheritdoc />
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		bool ISpanEncodable.TryEncode(Span<byte> destination, out int bytesWritten) => TupleEncoder.TryPackTo(destination, out bytesWritten, default, in this);

		#endregion

	}

}
