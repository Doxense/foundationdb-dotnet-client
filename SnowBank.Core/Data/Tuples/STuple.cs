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

namespace SnowBank.Data.Tuples
{
	using System.Collections;
	using System.Globalization;
	using System.Text;
	using SnowBank.Data.Tuples.Binary;
	using SnowBank.Runtime.Converters;
	using SnowBank.Text;

	/// <summary>Factory class for Tuples</summary>
	[PublicAPI]
	public readonly struct STuple : IVarTuple, ITupleSerializable
	{
		//note: We cannot use 'Tuple' because it's already used by the BCL in the System namespace, and we cannot use 'Tuples' either because it is part of the namespace...

		/// <summary>Empty tuple</summary>
		/// <remarks>Not to be mistaken with a 1-tuple containing 'null' !</remarks>
		public static readonly IVarTuple Empty = new STuple();

		#region Empty Tuple

		/// <inheritdoc />
		public int Count => 0;

		/// <inheritdoc />
		object IReadOnlyList<object?>.this[int index] => throw new InvalidOperationException("Tuple is empty");

		/// <inheritdoc />
		object IVarTuple.this[int index] => throw new InvalidOperationException("Tuple is empty");

		/// <inheritdoc />
		int System.Runtime.CompilerServices.ITuple.Length => 0;

		/// <inheritdoc />
		object System.Runtime.CompilerServices.ITuple.this[int index] => throw new InvalidOperationException("Tuple is empty");

		//REVIEW: should we throw if from/to are not null, 0 or -1 ?
		IVarTuple IVarTuple.this[int? from, int? to] => this;

		/// <inheritdoc />
		object IVarTuple.this[Index index] => TupleHelpers.FailIndexOutOfRange<object>(index, 0);

		/// <inheritdoc />
		IVarTuple IVarTuple.this[Range range]
		{
			get
			{
				_ = range.GetOffsetAndLength(0);
				return this;
			}
		}

		/// <inheritdoc />
		TItem IVarTuple.Get<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TItem>(int index)
			=> throw TupleHelpers.FailTupleIsEmpty();

		/// <inheritdoc />
		TItem IVarTuple.GetFirst<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TItem>()
			=> throw TupleHelpers.FailTupleIsEmpty();

		/// <inheritdoc />
		TItem IVarTuple.GetLast<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TItem>()
			=> throw TupleHelpers.FailTupleIsEmpty();

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public IVarTuple Append<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T1>(T1 value) => new STuple<T1>(value);

		/// <inheritdoc />
		public IVarTuple Concat(IVarTuple tuple)
		{
			Contract.NotNull(tuple);
			return tuple.Count == 0 ? this : tuple;
		}

		/// <inheritdoc />
		public void CopyTo(object?[] array, int offset)
		{
			//NO-OP
		}

		/// <inheritdoc />
		public IEnumerator<object?> GetEnumerator()
		{
			yield break;
		}

		System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
		{
			return this.GetEnumerator();
		}

		/// <inheritdoc />
		public override string ToString()
		{
			return "()";
		}

		/// <inheritdoc />
		public override int GetHashCode()
		{
			return 0;
		}

		/// <inheritdoc />
		public bool Equals(IVarTuple? value)
		{
			return value != null && value.Count == 0;
		}

		/// <inheritdoc />
		public override bool Equals(object? obj)
		{
			return Equals(obj as IVarTuple);
		}

		/// <inheritdoc />
		bool System.Collections.IStructuralEquatable.Equals(object? other, System.Collections.IEqualityComparer comparer)
		{
			return other is IVarTuple tuple && tuple.Count == 0;
		}

		/// <inheritdoc />
		int System.Collections.IStructuralEquatable.GetHashCode(System.Collections.IEqualityComparer comparer)
		{
			return 0;
		}

		/// <inheritdoc />
		int IVarTuple.GetItemHashCode(int index, IEqualityComparer comparer)
		{
			return index == 0 ? 0 : throw new IndexOutOfRangeException();
		}

		/// <inheritdoc />
		void ITupleSerializable.PackTo(ref TupleWriter writer)
		{
			//NOP
		}

		#endregion

		#region Creation

		/// <summary>Create a new empty tuple with 0 elements</summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining), DebuggerStepThrough]
		[OverloadResolutionPriority(1)]
		public static STuple Create()
		{
			//note: redundant with STuple.Empty, but is here to fit nicely with the other Create<T...> overloads
			return new STuple();
		}

		/// <summary>Create a new 1-tuple, holding only one item</summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining), DebuggerStepThrough]
		public static STuple<T1> Create<T1>(T1 item1)
		{
			return new STuple<T1>(item1);
		}

		/// <summary>Create a new 2-tuple, holding two items</summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining), DebuggerStepThrough]
		[OverloadResolutionPriority(1)]
		public static STuple<T1, T2> Create<T1, T2>(T1 item1, T2 item2)
		{
			return new STuple<T1, T2>(item1, item2);
		}

		/// <summary>Create a new 3-tuple, holding three items</summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining), DebuggerStepThrough]
		[OverloadResolutionPriority(1)]
		public static STuple<T1, T2, T3> Create<T1, T2, T3>(T1 item1, T2 item2, T3 item3)
		{
			return new STuple<T1, T2, T3>(item1, item2, item3);
		}

		/// <summary>Create a new 4-tuple, holding four items</summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining), DebuggerStepThrough]
		[OverloadResolutionPriority(1)]
		public static STuple<T1, T2, T3, T4> Create<T1, T2, T3, T4>(T1 item1, T2 item2, T3 item3, T4 item4)
		{
			return new STuple<T1, T2, T3, T4>(item1, item2, item3, item4);
		}

		/// <summary>Create a new 5-tuple, holding five items</summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining), DebuggerStepThrough]
		[OverloadResolutionPriority(1)]
		public static STuple<T1, T2, T3, T4, T5> Create<T1, T2, T3, T4, T5>(T1 item1, T2 item2, T3 item3, T4 item4, T5 item5)
		{
			return new STuple<T1, T2, T3, T4, T5>(item1, item2, item3, item4, item5);
		}

		/// <summary>Create a new 6-tuple, holding six items</summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining), DebuggerStepThrough]
		[OverloadResolutionPriority(1)]
		public static STuple<T1, T2, T3, T4, T5, T6> Create<T1, T2, T3, T4, T5, T6>(T1 item1, T2 item2, T3 item3, T4 item4, T5 item5, T6 item6)
		{
			return new STuple<T1, T2, T3, T4, T5, T6>(item1, item2, item3, item4, item5, item6);
		}

		/// <summary>Create a new 7-tuple, holding seven items</summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining), DebuggerStepThrough]
		[OverloadResolutionPriority(1)]
		public static STuple<T1, T2, T3, T4, T5, T6, T7> Create<T1, T2, T3, T4, T5, T6, T7>(T1 item1, T2 item2, T3 item3, T4 item4, T5 item5, T6 item6, T7 item7)
		{
			return new STuple<T1, T2, T3, T4, T5, T6, T7>(item1, item2, item3, item4, item5, item6, item7);
		}

		/// <summary>Create a new N-tuple, from N items</summary>
		/// <param name="items">Items to wrap in a tuple</param>
		/// <remarks>If you already have an array of items, you should call <see cref="FromArray{T}(T[])"/> instead. Mutating the array, would also mutate the tuple!</remarks>
		[Pure]
#if NET9_0_OR_GREATER
		public static IVarTuple Create(object?[] items)
#else
		public static IVarTuple Create(params object?[] items)
#endif
		{
			Contract.NotNull(items);

			//note: this is a convenience method for people that wants to pass more than 3 args arguments, and not have to call CreateRange(object[]) method

			if (items.Length == 0) return STuple.Empty;

			// We don't copy the array, and rely on the fact that the array was created by the compiler and that nobody will get a reference on it.
			return new ListTuple<object?>(items.AsMemory());
		}

		/// <summary>Create a new N-tuple, from N items</summary>
		/// <param name="items">Items to wrap in a tuple</param>
		/// <remarks>If you already have an array of items, you should call <see cref="FromArray{T}(T[])"/> instead. Mutating the array, would also mutate the tuple!</remarks>
		[Pure]
#if NET9_0_OR_GREATER
		public static IVarTuple Create(params ReadOnlySpan<object?> items)
#else
		public static IVarTuple Create(ReadOnlySpan<object?> items)
#endif
		{
			//note: this is a convenience method for people that wants to pass more than 3 args arguments, and not have to call CreateRange(object[]) method

			if (items.Length == 0) return STuple.Empty;

			// We don't copy the array, and rely on the fact that the array was created by the compiler and that nobody will get a reference on it.
			return new ListTuple<object?>(items.ToArray());
		}

		/// <summary>Create a new 1-tuple, holding only one item</summary>
		/// <remarks>This is the non-generic equivalent of <c>STuple.Create&lt;object&gt;(item)</c></remarks>
		[Pure]
		public static IVarTuple CreateBoxed(object? item)
		{
			return new STuple<object?>(item);
		}

		/// <summary>Create a new N-tuple that wraps a sequence of untyped items</summary>
		/// <remarks>If the original sequence is mutated, the tuple will reflect the changes!</remarks>
		[Pure]
		public static IVarTuple Wrap(ReadOnlyMemory<object?> items)
		{
			//note: this method only exists to differentiate between Create(ROM<object>) and Create<ROM<object>>()
			if (items.Length == 0) return STuple.Empty;
			return new ListTuple<object?>(items);
		}

		/// <summary>Create a new N-tuple that wraps an array of untyped items</summary>
		/// <remarks>If the original array is mutated, the tuple will reflect the changes!</remarks>
		[Pure]
		public static IVarTuple Wrap(object?[] items)
		{
			//note: this method only exists to differentiate between Create(object[]) and Create<object[]>()
			Contract.NotNull(items);
			if (items.Length == 0) return STuple.Empty;
			return new ListTuple<object?>(items.AsMemory());
		}

		/// <summary>Create a new N-tuple that wraps a section of an array of untyped items</summary>
		/// <remarks>If the original array is mutated, the tuple will reflect the changes!</remarks>
		[Pure]
		public static IVarTuple Wrap(object?[] items, int offset, int count)
		{
			if (count == 0) return STuple.Empty;
			return new ListTuple<object?>(items.AsMemory(offset, count));
		}

		/// <summary>Create a new N-tuple by copying the content of an array of untyped items</summary>
		[Pure]
		public static IVarTuple FromObjects(ReadOnlyMemory<object?> items)
		{
			return new ListTuple<object?>(items.ToArray().AsMemory());
		}

		/// <summary>Create a new N-tuple by copying the content of an array of untyped items</summary>
		[Pure]
		public static IVarTuple FromObjects(ReadOnlySpan<object?> items)
		{
			return new ListTuple<object?>(items.ToArray().AsMemory());
		}

		/// <summary>Create a new N-tuple by copying the content of an array of untyped items</summary>
		[Pure]
		public static IVarTuple FromObjects(object?[] items)
		{
			//note: this method only exists to differentiate between Create(object[]) and Create<object[]>()
			Contract.NotNull(items);
			if (items.Length == 0) return STuple.Empty;
			return new ListTuple<object?>(items.ToArray().AsMemory());
		}

		/// <summary>Create a new N-tuple by copying a section of an array of untyped items</summary>
		public static IVarTuple FromObjects(object?[] items, int offset, int count)
		{
			if (count == 0) return STuple.Empty;
			return new ListTuple<object?>(items.AsMemory(offset, count).ToArray().AsMemory());
		}

		/// <summary>Create a new N-tuple that wraps a section of an array of untyped items</summary>
		/// <remarks>If <paramref name="copy"/> is true, and the original array is mutated, the tuple will reflect the changes!</remarks>
		[Pure]
		public static IVarTuple FromObjects(ReadOnlyMemory<object?> items, bool copy)
		{
			if (items.Length == 0) return STuple.Empty;
			return new ListTuple<object?>(copy ? items.ToArray().AsMemory() : items);
		}

		/// <summary>Wraps an array into a tuple with the same items</summary>
		/// <remarks>Changing the array will impact the tuple, which may break the expectations of some consumers, who assume that a tuple is immutable.</remarks>
		public static IVarTuple WrapArray<T>(T[] items)
		{
			Contract.NotNull(items);
			if (items.Length == 0) return STuple.Empty;
			return new ListTuple<T>(items.AsMemory());
		}

		/// <summary>Wraps an array into a tuple with the same items</summary>
		/// <remarks>Changing the array will impact the tuple, which may break the expectations of some consumers, who assume that a tuple is immutable.</remarks>
		public static IVarTuple WrapArray<T>(ReadOnlyMemory<T> items)
		{
			if (items.Length == 0) return STuple.Empty;
			return new ListTuple<T>(items);
		}

		/// <summary>Creates a new tuple, from an array of typed items</summary>
		/// <param name="items">Array of items</param>
		/// <returns>Tuple with the same size as <paramref name="items"/> and where all the items are of type <typeparamref name="T"/></returns>
		[Pure]
		public static IVarTuple FromArray<T>(T[] items)
		{
			Contract.NotNull(items);

			return FromArray<T>(items.AsSpan());
		}

		/// <summary>Creates a new tuple, from a section of an array of typed items</summary>
		[Pure]
		public static IVarTuple FromArray<T>(T[] items, int offset, int count)
		{
			return FromArray<T>(items.AsSpan(offset, count));
		}

		/// <summary>Creates a new tuple, from a section of an array of typed items</summary>
		[Pure]
		public static IVarTuple FromArray<T>(ReadOnlySpan<T> items)
		{
			switch (items.Length)
			{
				case 0: return STuple.Empty;
				case 1: return Create<T>(items[0]);
				case 2: return Create<T, T>(items[0], items[1]);
				case 3: return Create<T, T, T>(items[0], items[1], items[2]);
				default:
				{ // copy the items in a temp array
					return new ListTuple<T>(items.ToArray().AsMemory());
				}
			}
		}

		/// <summary>Creates a new tuple from a sequence of typed items</summary>
		[Pure]
		public static IVarTuple FromEnumerable<T>(IEnumerable<T> items)
		{
			Contract.NotNull(items);

			if (items is T[] arr)
			{
				return FromArray<T>(arr.AsSpan());
			}

			if (items is ListTuple<T> lt)
			{
				return lt;
			}

			// may already be a tuple (because it implements IE<obj>)
			if (typeof(T) == typeof(object) && items is IVarTuple vt)
			{
				return vt;
			}

			if (items is IList<T> list)
			{
				switch (list.Count)
				{
					case 0: return STuple.Empty;
					case 1: return Create<T>(list[0]);
					case 2: return Create<T, T>(list[0], list[1]);
					case 3: return Create<T, T, T>(list[0], list[1], list[2]);
					default:
					{ // copy the items in a temp array
						return new ListTuple<T>(items.ToArray().AsMemory());
					}
				}
			}

			return new ListTuple<T>(items.ToArray().AsMemory());
		}

		/// <summary>Concatenates two tuples together</summary>
		[Pure]
		public static IVarTuple Concat(IVarTuple head, IVarTuple tail)
		{
			Contract.NotNull(head);
			Contract.NotNull(tail);

			return tail.Count == 0 ? head
			     : head.Count == 0 ? tail
			     : new JoinedTuple(head, tail);
		}

		/// <summary>Concatenates two tuples together</summary>
		[Pure]
		public static IVarTuple Concat(IVarTuple head, IVarTuple middle, IVarTuple tail)
		{
			Contract.NotNull(head);
			Contract.NotNull(middle);
			Contract.NotNull(tail);

			int numA = head.Count;
			int numB = middle.Count;
			int numC = tail.Count;

			if (numC == 0) return Concat(head, middle);
			if (numB == 0) return Concat(head, tail);
			if (numA == 0) return Concat(middle, tail);

			var tmp = new object?[checked(numA + numB + numC)];
			head.CopyTo(tmp, 0);
			middle.CopyTo(tmp, numA);
			tail.CopyTo(tmp, numA + numB);

			return new ListTuple<object?>(tmp.AsMemory());
		}

		/// <summary>Converts a <see cref="ValueTuple{T1}"/> into the equivalent <see cref="STuple{T1}"/> of the same size</summary>
		[Pure]
		public static STuple<T1> Create<T1>(ValueTuple<T1> tuple)
			=> new(tuple.Item1);

		/// <summary>Converts a <see cref="ValueTuple{T1}"/> into the equivalent <see cref="STuple{T1}"/> of the same size</summary>
		[Pure]
		public static STuple<T1> Create<T1>(ref ValueTuple<T1> tuple)
			=> new(tuple.Item1);

		/// <summary>Converts a <see cref="ValueTuple{T1,T2}"/> into the equivalent <see cref="STuple{T1,T2}"/> of the same size</summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		[OverloadResolutionPriority(1)]
		public static STuple<T1, T2> Create<T1, T2>((T1, T2) tuple)
			=> new(tuple.Item1, tuple.Item2);

		/// <summary>Converts a <see cref="ValueTuple{T1,T2}"/> into the equivalent <see cref="STuple{T1,T2}"/> of the same size</summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		[OverloadResolutionPriority(1)]
		public static STuple<T1, T2> Create<T1, T2>(ref (T1, T2) tuple)
			=> new(tuple.Item1, tuple.Item2);

		/// <summary>Converts a <see cref="ValueTuple{T1,T2,T3}"/> into the equivalent <see cref="STuple{T1,T2,T3}"/> of the same size</summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		[OverloadResolutionPriority(1)]
		public static STuple<T1, T2, T3> Create<T1, T2, T3>((T1, T2, T3) tuple)
			=> new(tuple.Item1, tuple.Item2, tuple.Item3);

		/// <summary>Converts a <see cref="ValueTuple{T1,T2,T3}"/> into the equivalent <see cref="STuple{T1,T2,T3}"/> of the same size</summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		[OverloadResolutionPriority(1)]
		public static STuple<T1, T2, T3> Create<T1, T2, T3>(ref (T1, T2, T3) tuple)
			=> new(tuple.Item1, tuple.Item2, tuple.Item3);

		/// <summary>Converts a <see cref="ValueTuple{T1,T2,T3,T4}"/> into the equivalent <see cref="STuple{T1,T2,T3,T4}"/> of the same size</summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		[OverloadResolutionPriority(1)]
		public static STuple<T1, T2, T3, T4> Create<T1, T2, T3, T4>((T1, T2, T3, T4) tuple)
			=> new(tuple.Item1, tuple.Item2, tuple.Item3, tuple.Item4);

		/// <summary>Converts a <see cref="ValueTuple{T1,T2,T3,T4}"/> into the equivalent <see cref="STuple{T1,T2,T3,T4}"/> of the same size</summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		[OverloadResolutionPriority(1)]
		public static STuple<T1, T2, T3, T4> Create<T1, T2, T3, T4>(ref (T1, T2, T3, T4) tuple)
			=> new(tuple.Item1, tuple.Item2, tuple.Item3, tuple.Item4);

		/// <summary>Converts a <see cref="ValueTuple{T1,T2,T3,T4,T5}"/> into the equivalent <see cref="STuple{T1,T2,T3,T4,T5}"/> of the same size</summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		[OverloadResolutionPriority(1)]
		public static STuple<T1, T2, T3, T4, T5> Create<T1, T2, T3, T4, T5>((T1, T2, T3, T4, T5) tuple)
			=> new(tuple.Item1, tuple.Item2, tuple.Item3, tuple.Item4, tuple.Item5);

		/// <summary>Converts a <see cref="ValueTuple{T1,T2,T3,T4,T5}"/> into the equivalent <see cref="STuple{T1,T2,T3,T4,T5}"/> of the same size</summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		[OverloadResolutionPriority(1)]
		public static STuple<T1, T2, T3, T4, T5> Create<T1, T2, T3, T4, T5>(ref (T1, T2, T3, T4, T5) tuple)
			=> new(tuple.Item1, tuple.Item2, tuple.Item3, tuple.Item4, tuple.Item5);

		/// <summary>Converts a <see cref="ValueTuple{T1,T2,T3,T4,T5,T6}"/> into the equivalent <see cref="STuple{T1,T2,T3,T4,T5,T6}"/> of the same size</summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		[OverloadResolutionPriority(1)]
		public static STuple<T1, T2, T3, T4, T5, T6> Create<T1, T2, T3, T4, T5, T6>((T1, T2, T3, T4, T5, T6) tuple)
			=> new(tuple.Item1, tuple.Item2, tuple.Item3, tuple.Item4, tuple.Item5, tuple.Item6);

		/// <summary>Converts a <see cref="ValueTuple{T1,T2,T3,T4,T5,T6}"/> into the equivalent <see cref="STuple{T1,T2,T3,T4,T5,T6}"/> of the same size</summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		[OverloadResolutionPriority(1)]
		public static STuple<T1, T2, T3, T4, T5, T6> Create<T1, T2, T3, T4, T5, T6>(ref (T1, T2, T3, T4, T5, T6) tuple)
			=> new(tuple.Item1, tuple.Item2, tuple.Item3, tuple.Item4, tuple.Item5, tuple.Item6);

		/// <summary>Converts a <see cref="ValueTuple{T1,T2,T3,T4,T5,T6,T7}"/> into the equivalent <see cref="STuple{T1,T2,T3,T4,T5,T6,T7}"/> of the same size</summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		[OverloadResolutionPriority(1)]
		public static STuple<T1, T2, T3, T4, T5, T6, T7> Create<T1, T2, T3, T4, T5, T6, T7>((T1, T2, T3, T4, T5, T6, T7) tuple)
			=> new(tuple.Item1, tuple.Item2, tuple.Item3, tuple.Item4, tuple.Item5, tuple.Item6, tuple.Item7);

		/// <summary>Converts a <see cref="ValueTuple{T1,T2,T3,T4,T5,T6,T7}"/> into the equivalent <see cref="STuple{T1,T2,T3,T4,T5,T6,T7}"/> of the same size</summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		[OverloadResolutionPriority(1)]
		public static STuple<T1, T2, T3, T4, T5, T6, T7> Create<T1, T2, T3, T4, T5, T6, T7>(ref (T1, T2, T3, T4, T5, T6, T7) tuple)
			=> new(tuple.Item1, tuple.Item2, tuple.Item3, tuple.Item4, tuple.Item5, tuple.Item6, tuple.Item7);

		#endregion

		#region Internal Helpers...

		/// <summary>Determines whether the specified tuple instances are considered equal</summary>
		/// <param name="left">Left tuple</param>
		/// <param name="right">Right tuple</param>
		/// <returns>True if the tuples are considered equal; otherwise, false. If both <paramref name="left"/> and <paramref name="right"/> are null, the methods returns true;</returns>
		/// <remarks>This method is equivalent of calling left.Equals(right), </remarks>
		public static bool Equals(IVarTuple left, IVarTuple right)
		{
			if (ReferenceEquals(left, null)) return ReferenceEquals(right, null);
			return left.Equals(right);
		}

		/// <summary>Determines whether the specified tuple instances are considered similar</summary>
		/// <param name="left">Left tuple</param>
		/// <param name="right">Right tuple</param>
		/// <returns>True if the tuples are considered similar; otherwise, false. If both <paramref name="left"/> and <paramref name="right"/> are null, the methods returns true;</returns>
		public static bool Equivalent(IVarTuple left, IVarTuple right)
		{
			if (ReferenceEquals(left, null)) return ReferenceEquals(right, null);
			return !ReferenceEquals(right, null) && TupleHelpers.Equals(left, right, TupleComparisons.Default);
		}

		/// <summary>Methods for formatting tuples into strings</summary>
		[PublicAPI]
		public static class Formatter
		{

			private const string TokenNull = "null";
			private const string TokenFalse = "false";
			private const string TokenTrue = "true";
			private const string TokenTupleEmpty = "()";

			/// <summary>Converts any object into a displayable string, for logging/debugging purpose</summary>
			/// <param name="item">Object to stringify</param>
			/// <returns>String representation of the object</returns>
			/// <example>
			/// Stringify&lt;{REF_TYPE}&gt;(null) => "nil"
			/// Stringify&lt;string&gt;{string}("hello") => "\"hello\""
			/// Stringify&lt;int&gt;(123) => "123"
			/// Stringify&lt;double&gt;(123.4d) => "123.4"
			/// Stringify&lt;bool&gt;(true) => "true"
			/// Stringify&lt;char&gt;('Z') => "'Z'"
			/// Stringify&lt;Slice&gt;((...) => hexadecimal string ("01 23 45 67 89 AB CD EF")
			/// </example>
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static string Stringify<T>(T? item)
			{
				if (item is null) return TokenNull;

				if (default(T) is not null)
				{
					// <JIT_HACK>!
					if (typeof(T) == typeof(int)) return Stringify((int) (object) item);
					if (typeof(T) == typeof(uint)) return Stringify((uint) (object) item);
					if (typeof(T) == typeof(long)) return Stringify((long) (object) item);
					if (typeof(T) == typeof(ulong)) return Stringify((ulong) (object) item);
					if (typeof(T) == typeof(bool)) return Stringify((bool) (object) item);
					if (typeof(T) == typeof(char)) return Stringify((char) (object) item);
					if (typeof(T) == typeof(Slice)) return Stringify((Slice) (object) item);
					if (typeof(T) == typeof(double)) return Stringify((double) (object) item);
					if (typeof(T) == typeof(float)) return Stringify((float) (object) item);
					if (typeof(T) == typeof(Guid)) return Stringify((Guid) (object) item);
					if (typeof(T) == typeof(Uuid128)) return Stringify((Uuid128) (object) item);
					if (typeof(T) == typeof(Uuid96)) return Stringify((Uuid96) (object) item);
					if (typeof(T) == typeof(Uuid80)) return Stringify((Uuid80) (object) item);
					if (typeof(T) == typeof(Uuid64)) return Stringify((Uuid64) (object) item);
					if (typeof(T) == typeof(DateTime)) return Stringify((DateTime) (object) item);
					if (typeof(T) == typeof(DateTimeOffset)) return Stringify((DateTimeOffset) (object) item);
					if (typeof(T) == typeof(NodaTime.Instant)) return Stringify((NodaTime.Instant) (object) item);
					// </JIT_HACK>
				}
				else
				{
					if (item is string s) return Stringify(s);

					// <JIT_HACK>!
					if (typeof(T) == typeof(int?)) return Stringify((int) (object) item);
					if (typeof(T) == typeof(uint?)) return Stringify((uint) (object) item);
					if (typeof(T) == typeof(long?)) return Stringify((long) (object) item);
					if (typeof(T) == typeof(ulong?)) return Stringify((ulong) (object) item);
					if (typeof(T) == typeof(bool?)) return Stringify((bool) (object) item);
					if (typeof(T) == typeof(char?)) return Stringify((char) (object) item);
					if (typeof(T) == typeof(Slice?)) return Stringify((Slice) (object) item);
					if (typeof(T) == typeof(double?)) return Stringify((double) (object) item);
					if (typeof(T) == typeof(float?)) return Stringify((float) (object) item);
					if (typeof(T) == typeof(Guid?)) return Stringify((Guid) (object) item);
					if (typeof(T) == typeof(Uuid128?)) return Stringify((Uuid128) (object) item);
					if (typeof(T) == typeof(Uuid96?)) return Stringify((Uuid96) (object) item);
					if (typeof(T) == typeof(Uuid80?)) return Stringify((Uuid80) (object) item);
					if (typeof(T) == typeof(Uuid64?)) return Stringify((Uuid64) (object) item);
					if (typeof(T) == typeof(DateTime?)) return Stringify((DateTime) (object) item);
					if (typeof(T) == typeof(DateTimeOffset?)) return Stringify((DateTimeOffset) (object) item);
					if (typeof(T) == typeof(NodaTime.Instant?)) return Stringify((NodaTime.Instant) (object) item);
					// </JIT_HACK>
				}

				// some other type
				return StringifyInternal(item);
			}

			/// <summary>Converts any object into a displayable string, for logging/debugging purpose</summary>
			/// <param name="item">Object to stringify</param>
			/// <returns>String representation of the object</returns>
			/// <example>
			/// Stringify(null) => "nil"
			/// Stringify("hello") => "\"hello\""
			/// Stringify(123) => "123"
			/// Stringify(123.4d) => "123.4"
			/// Stringify(true) => "true"
			/// Stringify('Z') => "'Z'"
			/// Stringify((Slice)...) => hexadecimal string ("01 23 45 67 89 AB CD EF")
			/// </example>
			internal static string StringifyBoxed(object? item)
			{
				switch (item)
				{
					case null:         return TokenNull;
					case string s:     return Stringify(s);
					case int i:        return Stringify(i);
					case long l:       return Stringify(l);
					case uint u:       return Stringify(u);
					case ulong ul:     return Stringify(ul);
					case bool b:       return Stringify(b);
					case char c:       return Stringify(c);
					case Slice sl:     return Stringify(sl);
					case double d:     return Stringify(d);
					case float f:      return Stringify(f);
					case Guid guid:    return Stringify(guid);
					case Uuid128 u128: return Stringify(u128);
					case Uuid96 u96:   return Stringify(u96);
					case Uuid80 u80:   return Stringify(u80);
					case Uuid64 u64:   return Stringify(u64);
					case DateTime dt:  return Stringify(dt);
					case DateTimeOffset dto: return Stringify(dto);
					case NodaTime.Instant t: return Stringify(t);
				}

				// some other type
				return StringifyInternal(item);
			}

			[MethodImpl(MethodImplOptions.NoInlining)]
			private static string StringifyInternal(object? item) => item switch
			{
				null => TokenNull,
				string s => Stringify(s),
				byte[] bytes => Stringify(bytes.AsSlice()),
				Slice slice => Stringify(slice),
				ArraySegment<byte> buffer => Stringify(buffer.AsSlice()),
				//TODO: Memory<T>, ReadOnlyMemory<T>, ...
				IFormattable f => f.ToString(null, CultureInfo.InvariantCulture),
				_ => item.ToString() ?? TokenNull
			};

			/// <summary>Encodes a value into a tuple text literal</summary>
			[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
			//TODO: escape the string? If it contains \0 or control chars, it can cause problems in the console or debugger output
			public static string Stringify(string? item) => string.IsNullOrEmpty(item) ? "\"\"" : string.Concat("\"", item, "\""); /* "hello" */

			/// <summary>Encodes a value into a tuple text literal</summary>
			[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static string Stringify(bool item) => item ? TokenTrue : TokenFalse;

			/// <summary>Encodes a value into a tuple text literal</summary>
			[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static string Stringify(int item) => StringConverters.ToString(item);

			/// <summary>Encodes a value into a tuple text literal</summary>
			[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static string Stringify(uint item) => StringConverters.ToString(item);

			/// <summary>Encodes a value into a tuple text literal</summary>
			[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static string Stringify(long item) => StringConverters.ToString(item);

			/// <summary>Encodes a value into a tuple text literal</summary>
			[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static string Stringify(ulong item) => StringConverters.ToString(item);

			/// <summary>Encodes a value into a tuple text literal</summary>
			[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static string Stringify(double item) => item.ToString("R", CultureInfo.InvariantCulture);

			/// <summary>Encodes a value into a tuple text literal</summary>
			[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static string Stringify(float item) => item.ToString("R", CultureInfo.InvariantCulture);

			/// <summary>Encodes a value into a tuple text literal</summary>
			[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static string Stringify(char item) => new([ '\'', item, '\'']); /* 'X' */

			/// <summary>Encodes a value into a tuple text literal</summary>
			[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static string Stringify(ReadOnlySpan<byte> item) => item.Length == 0 ? "``" : ('`' + Slice.Dump(item, item.Length) + '`');

			/// <summary>Encodes a value into a tuple text literal</summary>
			[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static string Stringify(Slice item) => item.IsNull ? "null" : item.Count == 0 ? "``" : ('`' + Slice.Dump(item, item.Count) + '`');

			/// <summary>Encodes a value into a tuple text literal</summary>
			[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static string Stringify(byte[]? item) => item == null ? "null" : item.Length == 0 ? "``" : ('`' + Slice.Dump(item, item.Length) + '`');

			/// <summary>Encodes a value into a tuple text literal</summary>
			[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static string Stringify(ArraySegment<byte> item) => Stringify(item.AsSlice());

			/// <summary>Encodes a value into a tuple text literal</summary>
			[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static string Stringify(Guid item) => item.ToString("B", CultureInfo.InstalledUICulture); /* {xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx} */

			/// <summary>Encodes a value into a tuple text literal</summary>
			[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static string Stringify(Uuid128 item) => item.ToString("B", CultureInfo.InstalledUICulture); /* {xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx} */

			/// <summary>Encodes a value into a tuple text literal</summary>
			[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static string Stringify(Uuid96 item) => item.ToString("B", CultureInfo.InstalledUICulture); /* {XXXXXXXX-XXXXXXXX-XXXXXXXX} */

			/// <summary>Encodes a value into a tuple text literal</summary>
			[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static string Stringify(Uuid80 item) => item.ToString("B", CultureInfo.InstalledUICulture); /* {XXXX-XXXXXXXX-XXXXXXXX} */

			/// <summary>Encodes a value into a tuple text literal</summary>
			[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static string Stringify(Uuid64 item) => item.ToString("B", CultureInfo.InstalledUICulture); /* {XXXXXXXX-XXXXXXXX} */

			/// <summary>Encodes a value into a tuple text literal</summary>
			[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static string Stringify(DateTime item) => "\"" + item.ToString("O", CultureInfo.InstalledUICulture) + "\""; /* "yyyy-mm-ddThh:mm:ss.ffffff" */

			/// <summary>Encodes a value into a tuple text literal</summary>
			[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static string Stringify(DateTimeOffset item) => "\"" + item.ToString("O", CultureInfo.InstalledUICulture) + "\""; /* "yyyy-mm-ddThh:mm:ss.ffffff+hh:mm" */

			/// <summary>Encodes a value into a tuple text literal</summary>
			[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static string Stringify(NodaTime.Instant item) => "\"" + item.ToDateTimeUtc().ToString("O", CultureInfo.InstalledUICulture) + "\""; /* "yyyy-mm-ddThh:mm:ss.ffffff" */

			/// <summary>Converts a list of object into a displaying string, for loggin/debugging purpose</summary>
			/// <param name="items">Array containing items to stringify</param>
			/// <param name="offset">Start offset of the items to convert</param>
			/// <param name="count">Number of items to convert</param>
			/// <returns>String representation of the tuple in the form "(item1, item2, ... itemN,)"</returns>
			/// <example>ToString(STuple.Create("hello", 123, true, "world")) => "(\"hello\", 123, true, \"world\",)</example>
			public static string ToString(object?[]? items, int offset, int count)
			{
				Contract.Debug.Requires(offset >= 0 && count >= 0);
				if (items == null) return string.Empty;
				return ToString(items.AsSpan(offset, count));
			}

			public static string ToString<T>(ReadOnlySpan<T> items)
			{
				if (items.Length == 0)
				{ // empty tuple: "()"
					return TokenTupleEmpty;
				}

				bool boxed = typeof(T) == typeof(object);

				int offset = 0;

				var sb = new StringBuilder();
				sb.Append('(');
				sb.Append(boxed ? StringifyBoxed(items[offset++]) : Stringify<T>(items[offset++]));

				if (items.Length == 1)
				{ // singleton tuple : "(X,)"
					return sb.Append(",)").ToString();
				}

				while (offset < items.Length)
				{
					sb.Append(", ").Append(boxed ? StringifyBoxed(items[offset++]) : Stringify<T>(items[offset++]));
				}
				return sb.Append(')').ToString();
			}

			public static string ToString(ReadOnlySpan<object?> items)
			{
				if (items.Length == 0)
				{ // empty tuple: "()"
					return TokenTupleEmpty;
				}

				int offset = 0;

				var sb = new StringBuilder();
				sb.Append('(');
				sb.Append(StringifyBoxed(items[offset++]));

				if (items.Length == 1)
				{ // singleton tuple : "(X,)"
					return sb.Append(",)").ToString();
				}

				while (offset < items.Length)
				{
					sb.Append(", ").Append(StringifyBoxed(items[offset++]));
				}
				return sb.Append(')').ToString();
			}

			/// <summary>Converts a sequence of object into a displaying string, for loggin/debugging purpose</summary>
			/// <param name="items">Sequence of items to stringify</param>
			/// <returns>String representation of the tuple in the form "(item1, item2, ... itemN,)"</returns>
			/// <example>ToString(STuple.Create("hello", 123, true, "world")) => "(\"hello\", 123, true, \"world\")</example>
			public static string ToString(IEnumerable<object?>? items)
			{
				if (items == null) return string.Empty;

				if (items is object[] arr) return ToString(arr, 0, arr.Length);

				using (var enumerator = items.GetEnumerator())
				{
					if (!enumerator.MoveNext())
					{ // empty tuple : "()"
						return TokenTupleEmpty;
					}

					var sb = new StringBuilder();
					sb.Append('(').Append(StringifyBoxed(enumerator.Current));
					bool singleton = true;
					while (enumerator.MoveNext())
					{
						singleton = false;
						sb.Append(", ").Append(StringifyBoxed(enumerator.Current));
					}
					// add a trailing ',' for singletons
					return (singleton ? sb.Append(",)") : sb.Append(')')).ToString();
				}
			}

			/// <summary>Converts a sequence of object into a displaying string, for loggin/debugging purpose</summary>
			/// <param name="items">Sequence of items to stringify</param>
			/// <returns>String representation of the tuple in the form "(item1, item2, ... itemN,)"</returns>
			/// <example>ToString(STuple.Create("hello", 123, true, "world")) => "(\"hello\", 123, true, \"world\")</example>
			internal static string ToString(SpanTuple items)
			{
				using (var enumerator = items.GetEnumerator())
				{
					if (!enumerator.MoveNext())
					{ // empty tuple : "()"
						return TokenTupleEmpty;
					}

					var sb = new StringBuilder();
					sb.Append('(').Append(StringifyBoxed(enumerator.Current));
					bool singleton = true;
					while (enumerator.MoveNext())
					{
						singleton = false;
						sb.Append(", ").Append(StringifyBoxed(enumerator.Current));
					}
					// add a trailing ',' for singletons
					return (singleton ? sb.Append(",)") : sb.Append(')')).ToString();
				}
			}

		}

		/// <summary>Methods for parsing tuples from strings</summary>
		public static class Deformatter
		{

			/// <summary>Parses a tuple expression from a string</summary>
			[Pure]
			public static IVarTuple Parse(string expression)
			{
				Contract.NotNullOrWhiteSpace(expression);
				var parser = new Parser(expression.Trim());
				var tuple = parser.ParseExpression();
				if (parser.HasMore) throw new FormatException("Unexpected token after final ')' in Tuple expression.");
				return tuple;
			}

			/// <summary>Parses a tuple expression at the start of a string</summary>
			/// <param name="expression">String that starts with a valid Tuple expression, with optional extra characters</param>
			/// <param name="tuple"></param>
			/// <param name="tail"></param>
			/// <returns>First item is the parsed tuple, and the second item is the rest of the string (or null if we consumed the whole expression)</returns>
			public static void ParseNext(string expression, out IVarTuple? tuple, out string? tail)
			{
				Contract.NotNullOrWhiteSpace(expression);
				if (string.IsNullOrWhiteSpace(expression))
				{
					tuple = null;
					tail = null;
					return;
				}

				var parser = new Parser(expression.Trim());
				tuple = parser.ParseExpression();
				string? s = parser.GetTail();
				tail = string.IsNullOrWhiteSpace(s) ? null : s.Trim();
			}

			private struct Parser
			{

				private const char EOF = '\xFFFF';

				// to reduce boxing, we pre-allocate some well known singletons

				private static readonly object FalseSingleton = false;
				private static readonly object TrueSingleton = true;
				private static readonly object ZeroSingleton = 0;
				private static readonly object OneSingleton = 1;
				private static readonly object TwoSingleton = 2;
				private static readonly object ThreeSingleton = 3;
				private static readonly object FourSingleton = 4;

				public Parser(string expression)
				{
					this.Expression = expression;
					this.Cursor = 0;
				}

				private readonly string Expression;
				
				private int Cursor;

				public readonly bool HasMore => this.Cursor < this.Expression.Length;

				public readonly string? GetTail() => this.Cursor < this.Expression.Length ? this.Expression[this.Cursor..] : null;

				private char ReadNext()
				{
					int p = this.Cursor;
					string s = this.Expression;
					if ((uint) p >= (uint) s.Length) return EOF;
					char c = s[p];
					this.Cursor = p + 1;
					return c;
				}

				[MethodImpl(MethodImplOptions.AggressiveInlining)]
				private char PeekNext()
				{
					int p = this.Cursor;
					string s = this.Expression;
					return (uint) p < (uint) s.Length ? s[p] : EOF;
				}

				[MethodImpl(MethodImplOptions.AggressiveInlining)]
				private void Advance()
				{
					++this.Cursor;
				}

				private bool TryReadKeyword(string keyword)
				{
					//IMPORTANT: 'keyword' must be lowercased !
					int p = this.Cursor;
					string s = this.Expression;
					int r = keyword.Length;
					if ((uint) (p + r) > (uint) s.Length) return false; // not enough
					for (int i = 0; i < r; i++)
					{
						if (char.ToLowerInvariant(s[p + i]) != keyword[i]) return false;
					}
					this.Cursor = p + r;
					return true;
				}

				/// <summary>Parse a tuple</summary>
				[Pure]
				public IVarTuple ParseExpression()
				{

					char c = ReadNext();
					if (c != '(')
					{
						throw new FormatException("Invalid tuple expression. Valid tuple must start with '(' and end with ')'.");
					}

					bool expectItem = true;

					var items = new List<object>();
					while (true)
					{
						c = PeekNext();
						switch (c)
						{
							case ')':
							{
								//note: we accept a terminal ',' without the last item, to allow "(123,)" as a valid tuple.
								if (expectItem && items.Count > 1) throw new FormatException("Missing item before last ',' in Tuple expression");
								Advance();
								return FromEnumerable(items);
							}
							case EOF:
							{
								throw new FormatException("Missing ')' at the end of tuple expression.");
							}

							case ',':
							{
								if (expectItem) throw new FormatException("Missing ',' before next item in Tuple expression.");
								Advance();
								expectItem = true;
								break;
							}

							case '"':
							{ // string literal
								string s = ReadStringLiteral();
								items.Add(s);
								expectItem = false;
								break;
							}
							case '\'':
							{ // single char literal
								Advance();
								char x = ReadNext();
								c = ReadNext();
								if (c != '\'') throw new FormatException("Missing quote after character. Single quotes are for single characters. For strings, use double quotes!");
								items.Add(x);
								expectItem = false;
								break;
							}
							case '{':
							{ // Guid
								Guid g = ReadGuidLiteral();
								items.Add(g);
								expectItem = false;
								break;
							}
							case '(':
							{ // embedded tuple!
								var sub = ParseExpression();
								items.Add(sub);
								expectItem = false;
								break;
							}
							case '|':
							{ // Custom type?
								var x = ReadCustomTypeLiteral();
								items.Add(x);
								expectItem = false;
								break;
							}

							default:
							{
								if (char.IsWhiteSpace(c))
								{ // ignore whitespaces
									Advance();
									break;
								}

								if (char.IsDigit(c) || c == '-')
								{ // number!
									items.Add(ReadNumberLiteral());
									expectItem = false;
									break;
								}

								if (c is 't' or 'T')
								{ // true?
									if (!TryReadKeyword("true")) throw new FormatException("Unrecognized keyword in Tuple expression. Did you meant to write 'true' instead?");
									items.Add(TrueSingleton);
									expectItem = false;
									break;
								}

								if (c is 'f' or 'F')
								{ // false?
									if (!TryReadKeyword("false")) throw new FormatException("Unrecognized keyword in Tuple expression. Did you meant to write 'false' instead?");
									items.Add(FalseSingleton);
									expectItem = false;
									break;
								}

								throw new FormatException($"Invalid token '{c}' in Tuple expression.");
							}
						}
					}
				}

				private object ReadNumberLiteral()
				{
					bool dec = false;
					bool neg = false;
					bool exp = false;

					string s = this.Expression;
					int start = this.Cursor;
					int end = s.Length;
					int p = start;
					ulong x = 0;

					char c = s[p];
					if (c == '-')
					{
						neg = true;
					}
					else if (c != '+')
					{
						x = (ulong) (c - '0');
					}
					++p;

					while (p < end)
					{
						c = s[p];
						if (char.IsDigit(c))
						{
							x = checked(x * 10 + (ulong) (c - '0'));
							++p;
							continue;
						}

						if (c == '.')
						{
							if (dec) throw FailRedundantDotInNumber();
							if (exp) throw FailUnexpectedDotInNumberExponent();
							dec = true;
							++p;
							continue;
						}

						if (c == ',' || c == ')' || char.IsWhiteSpace(c))
						{
							break;
						}

						if (c == 'E')
						{
							if (dec) throw FailRedundantExponentInNumber();
							exp = true;
							++p;
							continue;
						}

						if (c is '-' or '+')
						{
							if (!exp) throw FailUnexpectedSignInNumber();
							++p;
							continue;
						}

						throw FailInvalidTokenInNumber(c);
					}

					this.Cursor = p;

					if (!dec && !exp)
					{
						if (neg)
						{
							if (x < int.MaxValue) return -((int) x);
							if (x < long.MaxValue) return -((long) x);
							if (x == 1UL + long.MaxValue) return long.MinValue;
							throw new OverflowException("Parsed number is too large");
						}

						if (x <= int.MaxValue)
						{
							// to reduce boxing, we have a few pre-allocated singletons for the most common values (0, 1, 2, ...)
							return x switch
							{
								0 => ZeroSingleton,
								1 => OneSingleton,
								2 => TwoSingleton,
								3 => ThreeSingleton,
								4 => FourSingleton,
								_ => (int) x
							};
						}

						if (x <= long.MaxValue) return (long) x;
						return x;
					}

					return double.Parse(s.AsSpan(start, p - start), CultureInfo.InvariantCulture);
				}

				[Pure, MethodImpl(MethodImplOptions.NoInlining)]
				private static FormatException FailRedundantDotInNumber() => new("Redundant '.' in number that already has a decimal point.");

				[Pure, MethodImpl(MethodImplOptions.NoInlining)]
				private static FormatException FailUnexpectedDotInNumberExponent() => new("Unexpected '.' in exponent part of number.");

				[Pure, MethodImpl(MethodImplOptions.NoInlining)]
				private static FormatException FailRedundantExponentInNumber() => new("Redundant 'E' in number that already has an exponent.");

				private static FormatException FailUnexpectedSignInNumber() => new("Unexpected sign in number.");

				private static FormatException FailInvalidTokenInNumber(char c) => new($"Unexpected token '{c}' while parsing number in Tuple expression.");

				private string ReadStringLiteral()
				{
					string s = this.Expression;
					int p = this.Cursor;
					int end = p + s.Length;

					// main loop is optimistic and assumes that the string will not be escaped.
					// If we find the first instance of '\', then we switch to a secondary loop that uses a StringBuilder to decode each character

					char c = s[p++];
					if (c != '"') throw FailMissingExpectedBackslashToken();
					int start = p;

					while (p < end)
					{
						c = s[p];
						if (c == '"')
						{
							this.Cursor = p + 1;
							return s.Substring(start, p - start);
						}

						if (c == '\\')
						{ // string is escaped, will need to decode the content
							++p;
							goto parse_escaped_string;
						}
						++p;
					}
					goto truncated_string;

				parse_escaped_string:
					bool escape = true;
					var sb = StringBuilderCache.Acquire(128);
					if (p > start + 1) sb.Append(s.AsSpan(start, p - start - 1)); // copy what we have parsed so far
					while (p < end)
					{
						c = s[p];
						if (c == '"')
						{
							if (escape)
							{
								escape = false;
							}
							else
							{
								this.Cursor = p + 1;
								return StringBuilderCache.GetStringAndRelease(sb);
							}
						}
						else if (c == '\\')
						{
							if (!escape)
							{ // start of escape sequence
								escape = true;
								++p;
								continue;
							}
							escape = false;
						}
						else if (escape)
						{
							c = c switch
							{
								't' => '\t',
								'r' => '\r',
								'n' => '\n',
								//TODO: \x## and \u#### syntax!
								_ => throw FailInvalidTokenInString(c)
							};
							escape = false;
						}
						++p;
						sb.Append(c);
					}
				truncated_string:
					throw FailMissingDoubleAtEndOfString();
				}

				[Pure, MethodImpl(MethodImplOptions.NoInlining)]
				private static FormatException FailMissingExpectedBackslashToken() => new("Expected '\"' token is missing in Tuple expression");

				[Pure, MethodImpl(MethodImplOptions.NoInlining)]
				private static FormatException FailInvalidTokenInString(char c) => new($"Unexpected token '\\{c}' while parsing string in Tuple expression");

				[Pure, MethodImpl(MethodImplOptions.NoInlining)]
				private static FormatException FailMissingDoubleAtEndOfString() => new("Missing double quote at end of string in Tuple expression");

				private Guid ReadGuidLiteral()
				{
					var s = this.Expression.AsSpan();
					int p = this.Cursor;
					int end = s.Length;
					char c = s[p];
					if (s[p] != '{') throw new FormatException($"Unexpected token '{c}' at start of GUID in Tuple expression");
					++p;
					int start = p;
					while (p < end)
					{
						c = s[p];
						if (c == '}')
						{
							var lit = s.Slice(start, p - start);
							// Shortcut: "{} or {0} means "00000000-0000-0000-0000-000000000000"
							Guid g = lit is "" or "0" ? Guid.Empty : Guid.Parse(lit);
							this.Cursor = p + 1;
							return g;
						}
						++p;
					}

					throw new FormatException("Invalid GUID in Tuple expression.");
				}

				private TuPackUserType ReadCustomTypeLiteral()
				{
					var s = this.Expression.AsSpan();
					int p = this.Cursor;
					int end = s.Length;
					char c = s[p];
					if (s[p] != '|') throw new FormatException($"Unexpected token '{c}' at start of User Type in Tuple expression");
					++p;
					int start = p;
					while (p < end)
					{
						c = s[p];
						if (c == '|')
						{
							var lit = s.Slice(start, p - start);
							TuPackUserType ut;
							if (lit is "System")
							{
								ut = TuPackUserType.System;
							}
							else if (lit is "Directory")
							{
								ut = TuPackUserType.Directory;
							}
							else if (lit.StartsWith("User-", StringComparison.Ordinal))
							{
								throw new NotImplementedException("Implementation parsing of custom user types in Tuple expressions");
							}
							else
							{
								break;
							}
							this.Cursor = p + 1;
							return ut;
						}
						++p;
					}

					throw new FormatException("Invalid custom User Type in Tuple expression.");
				}
			}
		}

		#endregion

	}
}
