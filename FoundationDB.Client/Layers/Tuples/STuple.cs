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

#define ENABLE_VALUETUPLES

namespace Doxense.Collections.Tuples
{
	using System;
	using System.Collections.Generic;
	using System.Diagnostics;
	using System.Globalization;
	using System.Linq;
	using System.Runtime.CompilerServices;
	using System.Text;
	using Doxense.Collections.Tuples.Encoding;
	using Doxense.Diagnostics.Contracts;
	using JetBrains.Annotations;

	/// <summary>Factory class for Tuples</summary>
	[PublicAPI]
	public readonly struct STuple : ITuple, ITupleSerializable
	{
		//note: We cannot use 'Tuple' because it's already used by the BCL in the System namespace, and we cannot use 'Tuples' either because it is part of the namespace...

		/// <summary>Empty tuple</summary>
		/// <remarks>Not to be mistaken with a 1-tuple containing 'null' !</remarks>
		[NotNull]
		public static readonly ITuple Empty = new STuple();

		#region Empty Tuple

		public int Count => 0;

		object IReadOnlyList<object>.this[int index] => throw new InvalidOperationException("Tuple is empty");

		//REVIEW: should we throw if from/to are not null, 0 or -1 ?
		public ITuple this[int? from, int? to] => this;

		public TItem Get<TItem>(int index)
		{
			throw new InvalidOperationException("Tuple is empty");
		}

		public ITuple Append<T1>(T1 value) => new STuple<T1>(value);

		public ITuple Concat(ITuple tuple)
		{
			Contract.NotNull(tuple, nameof(tuple));
			if (tuple.Count == 0) return this;
			return tuple;
		}

		void ITupleSerializable.PackTo(ref TupleWriter writer)
		{
			PackTo(ref writer);
		}

		internal void PackTo(ref TupleWriter writer)
		{
			//NO-OP
		}

		public void CopyTo(object[] array, int offset)
		{
			//NO-OP
		}

		public IEnumerator<object> GetEnumerator()
		{
			yield break;
		}

		System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
		{
			return this.GetEnumerator();
		}

		public override string ToString()
		{
			return "()";
		}

		public override int GetHashCode()
		{
			return 0;
		}

		public bool Equals(ITuple value)
		{
			return value != null && value.Count == 0;
		}

		public override bool Equals(object obj)
		{
			return Equals(obj as ITuple);
		}

		bool System.Collections.IStructuralEquatable.Equals(object other, System.Collections.IEqualityComparer comparer)
		{
			return other is ITuple tuple && tuple.Count == 0;
		}

		int System.Collections.IStructuralEquatable.GetHashCode(System.Collections.IEqualityComparer comparer)
		{
			return 0;
		}

		#endregion

		#region Creation

		/// <summary>Create a new empty tuple with 0 elements</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining), DebuggerStepThrough]
		public static STuple Create()
		{
			//note: redundant with STuple.Empty, but is here to fit nicely with the other Create<T...> overloads
			return new STuple();
		}

		/// <summary>Create a new 1-tuple, holding only one item</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining), DebuggerStepThrough]
		public static STuple<T1> Create<T1>(T1 item1)
		{
			return new STuple<T1>(item1);
		}

		/// <summary>Create a new 2-tuple, holding two items</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining), DebuggerStepThrough]
		public static STuple<T1, T2> Create<T1, T2>(T1 item1, T2 item2)
		{
			return new STuple<T1, T2>(item1, item2);
		}

		/// <summary>Create a new 3-tuple, holding three items</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining), DebuggerStepThrough]
		public static STuple<T1, T2, T3> Create<T1, T2, T3>(T1 item1, T2 item2, T3 item3)
		{
			return new STuple<T1, T2, T3>(item1, item2, item3);
		}

		/// <summary>Create a new 4-tuple, holding four items</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining), DebuggerStepThrough]
		public static STuple<T1, T2, T3, T4> Create<T1, T2, T3, T4>(T1 item1, T2 item2, T3 item3, T4 item4)
		{
			return new STuple<T1, T2, T3, T4>(item1, item2, item3, item4);
		}

		/// <summary>Create a new 5-tuple, holding five items</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining), DebuggerStepThrough]
		public static STuple<T1, T2, T3, T4, T5> Create<T1, T2, T3, T4, T5>(T1 item1, T2 item2, T3 item3, T4 item4, T5 item5)
		{
			return new STuple<T1, T2, T3, T4, T5>(item1, item2, item3, item4, item5);
		}

		/// <summary>Create a new 6-tuple, holding six items</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining), DebuggerStepThrough]
		public static STuple<T1, T2, T3, T4, T5, T6> Create<T1, T2, T3, T4, T5, T6>(T1 item1, T2 item2, T3 item3, T4 item4, T5 item5, T6 item6)
		{
			return new STuple<T1, T2, T3, T4, T5, T6>(item1, item2, item3, item4, item5, item6);
		}

		/// <summary>Create a new N-tuple, from N items</summary>
		/// <param name="items">Items to wrap in a tuple</param>
		/// <remarks>If you already have an array of items, you should call <see cref="FromArray{T}(T[])"/> instead. Mutating the array, would also mutate the tuple!</remarks>
		[NotNull]
		public static ITuple Create([NotNull] params object[] items)
		{
			Contract.NotNull(items, nameof(items));

			//note: this is a convenience method for people that wants to pass more than 3 args arguments, and not have to call CreateRange(object[]) method

			if (items.Length == 0) return new STuple();

			// We don't copy the array, and rely on the fact that the array was created by the compiler and that nobody will get a reference on it.
			return new ListTuple(items, 0, items.Length);
		}

		/// <summary>Create a new 1-tuple, holding only one item</summary>
		/// <remarks>This is the non-generic equivalent of STuple.Create&lt;object&gt;()</remarks>
		[NotNull]
		public static ITuple CreateBoxed(object item)
		{
			return new STuple<object>(item);
		}

		/// <summary>Create a new N-tuple that wraps an array of untyped items</summary>
		/// <remarks>If the original array is mutated, the tuple will reflect the changes!</remarks>
		[NotNull]
		public static ITuple Wrap([NotNull] object[] items)
		{
			//note: this method only exists to differentiate between Create(object[]) and Create<object[]>()
			Contract.NotNull(items, nameof(items));
			return FromObjects(items, 0, items.Length, copy: false);
		}

		/// <summary>Create a new N-tuple that wraps a section of an array of untyped items</summary>
		/// <remarks>If the original array is mutated, the tuple will reflect the changes!</remarks>
		[NotNull]
		public static ITuple Wrap([NotNull] object[] items, int offset, int count)
		{
			return FromObjects(items, offset, count, copy: false);
		}

		/// <summary>Create a new N-tuple by copying the content of an array of untyped items</summary>
		[NotNull]
		public static ITuple FromObjects([NotNull] object[] items)
		{
			//note: this method only exists to differentiate between Create(object[]) and Create<object[]>()
			Contract.NotNull(items, nameof(items));
			return FromObjects(items, 0, items.Length, copy: true);
		}

		/// <summary>Create a new N-tuple by copying a section of an array of untyped items</summary>
		[NotNull]
		public static ITuple FromObjects([NotNull] object[] items, int offset, int count)
		{
			return FromObjects(items, offset, count, copy: true);
		}

		/// <summary>Create a new N-tuple that wraps a section of an array of untyped items</summary>
		/// <remarks>If <paramref name="copy"/> is true, and the original array is mutated, the tuple will reflect the changes!</remarks>
		[NotNull]
		public static ITuple FromObjects([NotNull] object[] items, int offset, int count, bool copy)
		{
			Contract.NotNull(items, nameof(items));
			Contract.Positive(offset, nameof(offset));
			Contract.Positive(count, nameof(count));
			Contract.LessOrEqual(offset + count, items.Length, nameof(count), "Source array is too small");

			if (count == 0) return STuple.Empty;

			if (copy)
			{
				var tmp = new object[count];
				Array.Copy(items, offset, tmp, 0, count);
				return new ListTuple(tmp, 0, count);
			}
			else
			{
				// can mutate if passed a pre-allocated array: { var foo = new objec[123]; Create(foo); foo[42] = "bad"; }
				return new ListTuple(items, offset, count);
			}
		}

		/// <summary>Create a new tuple, from an array of typed items</summary>
		/// <param name="items">Array of items</param>
		/// <returns>Tuple with the same size as <paramref name="items"/> and where all the items are of type <typeparamref name="T"/></returns>
		[NotNull]
		public static ITuple FromArray<T>([NotNull] T[] items)
		{
			Contract.NotNull(items, nameof(items));

			return FromArray<T>(items, 0, items.Length);
		}

		/// <summary>Create a new tuple, from a section of an array of typed items</summary>
		[NotNull]
		public static ITuple FromArray<T>([NotNull] T[] items, int offset, int count)
		{
			Contract.NotNull(items, nameof(items));
			Contract.Positive(offset, nameof(offset));
			Contract.Positive(count, nameof(count));
			Contract.LessOrEqual(offset + count, items.Length, nameof(count), "Source array is too small");

			switch (count)
			{
				case 0: return Create();
				case 1: return Create<T>(items[offset]);
				case 2: return Create<T, T>(items[offset], items[offset + 1]);
				case 3: return Create<T, T, T>(items[offset], items[offset + 1], items[offset + 2]);
				case 4: return Create<T, T, T, T>(items[offset], items[offset + 1], items[offset + 2], items[offset + 3]);
				case 5: return Create<T, T, T, T, T>(items[offset], items[offset + 1], items[offset + 2], items[offset + 3], items[offset + 4]);
				case 6: return Create<T, T, T, T, T, T>(items[offset], items[offset + 1], items[offset + 2], items[offset + 3], items[offset + 4], items[offset + 5]);
				default:
				{ // copy the items in a temp array
					//TODO: we would probably benefit from having an ListTuple<T> here!
					var tmp = new object[count];
					Array.Copy(items, offset, tmp, 0, count);
					return new ListTuple(tmp, 0, count);
				}
			}
		}

		/// <summary>Create a new tuple from a sequence of typed items</summary>
		[NotNull]
		public static ITuple FromEnumerable<T>([NotNull] IEnumerable<T> items)
		{
			Contract.NotNull(items, nameof(items));

			if (items is T[] arr)
			{
				return FromArray<T>(arr, 0, arr.Length);
			}

			// may already be a tuple (because it implements IE<obj>)
			if (items is ITuple tuple)
			{
				return tuple;
			}

			object[] tmp = items.Cast<object>().ToArray();
			//TODO: we would probably benefit from having an ListTuple<T> here!
			return new ListTuple(tmp, 0, tmp.Length);
		}

		/// <summary>Concatenates two tuples together</summary>
		[NotNull]
		public static ITuple Concat([NotNull] ITuple head, [NotNull] ITuple tail)
		{
			Contract.NotNull(head, nameof(head));
			Contract.NotNull(tail, nameof(tail));

			return head.Count == 0 ? tail
			     : tail.Count == 0 ? head
			     : new JoinedTuple(head, tail);
		}

#if ENABLE_VALUETUPLES

		[Pure]
		public static STuple<T1> Create<T1>(ValueTuple<T1> tuple)
		{
			return new STuple<T1>(tuple.Item1);
		}

		[Pure]
		public static STuple<T1> Create<T1>(ref ValueTuple<T1> tuple)
		{
			return new STuple<T1>(tuple.Item1);
		}

		[Pure]
		public static STuple<T1, T2> Create<T1, T2>(ValueTuple<T1, T2> tuple)
		{
			return new STuple<T1, T2>(tuple.Item1, tuple.Item2);
		}

		[Pure]
		public static STuple<T1, T2> Create<T1, T2>(ref ValueTuple<T1, T2> tuple)
		{
			return new STuple<T1, T2>(tuple.Item1, tuple.Item2);
		}

		[Pure]
		public static STuple<T1, T2, T3> Create<T1, T2, T3>(ValueTuple<T1, T2, T3> tuple)
		{
			return new STuple<T1, T2, T3>(tuple.Item1, tuple.Item2, tuple.Item3);
		}

		[Pure]
		public static STuple<T1, T2, T3> Create<T1, T2, T3>(ref ValueTuple<T1, T2, T3> tuple)
		{
			return new STuple<T1, T2, T3>(tuple.Item1, tuple.Item2, tuple.Item3);
		}

		[Pure]
		public static STuple<T1, T2, T3, T4> Create<T1, T2, T3, T4>(ValueTuple<T1, T2, T3, T4> tuple)
		{
			return new STuple<T1, T2, T3, T4>(tuple.Item1, tuple.Item2, tuple.Item3, tuple.Item4);
		}

		[Pure]
		public static STuple<T1, T2, T3, T4> Create<T1, T2, T3, T4>(ref ValueTuple<T1, T2, T3, T4> tuple)
		{
			return new STuple<T1, T2, T3, T4>(tuple.Item1, tuple.Item2, tuple.Item3, tuple.Item4);
		}

		[Pure]
		public static STuple<T1, T2, T3, T4, T5> Create<T1, T2, T3, T4, T5>(ValueTuple<T1, T2, T3, T4, T5> tuple)
		{
			return new STuple<T1, T2, T3, T4, T5>(tuple.Item1, tuple.Item2, tuple.Item3, tuple.Item4, tuple.Item5);
		}

		[Pure]
		public static STuple<T1, T2, T3, T4, T5, T6> Create<T1, T2, T3, T4, T5, T6>(ValueTuple<T1, T2, T3, T4, T5, T6> tuple)
		{
			return new STuple<T1, T2, T3, T4, T5, T6>(tuple.Item1, tuple.Item2, tuple.Item3, tuple.Item4, tuple.Item5, tuple.Item6);
		}

		[Pure]
		public static STuple<T1, T2, T3, T4, T5, T6> Create<T1, T2, T3, T4, T5, T6>(ref ValueTuple<T1, T2, T3, T4, T5, T6> tuple)
		{
			return new STuple<T1, T2, T3, T4, T5, T6>(tuple.Item1, tuple.Item2, tuple.Item3, tuple.Item4, tuple.Item5, tuple.Item6);
		}

#endif

		#endregion

		#region Internal Helpers...

		/// <summary>Determines whether the specified tuple instances are considered equal</summary>
		/// <param name="left">Left tuple</param>
		/// <param name="right">Right tuple</param>
		/// <returns>True if the tuples are considered equal; otherwise, false. If both <paramref name="left"/> and <paramref name="right"/> are null, the methods returns true;</returns>
		/// <remarks>This method is equivalent of calling left.Equals(right), </remarks>
		public static bool Equals(ITuple left, ITuple right)
		{
			if (object.ReferenceEquals(left, null)) return object.ReferenceEquals(right, null);
			return left.Equals(right);
		}

		/// <summary>Determines whether the specifield tuple instances are considered similar</summary>
		/// <param name="left">Left tuple</param>
		/// <param name="right">Right tuple</param>
		/// <returns>True if the tuples are considered similar; otherwise, false. If both <paramref name="left"/> and <paramref name="right"/> are null, the methods returns true;</returns>
		public static bool Equivalent(ITuple left, ITuple right)
		{
			if (object.ReferenceEquals(left, null)) return object.ReferenceEquals(right, null);
			return !object.ReferenceEquals(right, null) && TupleHelpers.Equals(left, right, TupleComparisons.Default);
		}

		public static class Formatter
		{

			private const string TokenNull = "null";
			private const string TokenFalse = "false";
			private const string TokenTrue = "true";
			private const string TokenDoubleQuote = "\"";
			private const string TokenSingleQuote = "'";
			private const string TokenTupleEmpty = "()";
			private const string TokenTupleSep = ", ";
			private const string TokenTupleClose = ")";
			private const string TokenTupleSingleClose = ",)";

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
			/// Stringify&lt;Slice&gt;((...) => hexa decimal string ("01 23 45 67 89 AB CD EF")
			/// </example>
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static string Stringify<T>(T item)
			{
				if (default(T) == null)
				{
					if (item == null) return TokenNull;
				}
				// <JIT_HACK>!
				if (typeof(T) == typeof(int)) return Stringify((int) (object) item);
				if (typeof(T) == typeof(uint)) return Stringify((uint) (object) item);
				if (typeof(T) == typeof(long)) return Stringify((long) (object) item);
				if (typeof(T) == typeof(ulong)) return Stringify((ulong) (object) item);
				if (typeof(T) == typeof(bool)) return Stringify((bool) (object) item);
				if (typeof(T) == typeof(char)) return Stringify((char) (object) item);
				if (typeof(T) == typeof(Slice)) return Stringify((Slice)(object)item);
				if (typeof(T) == typeof(double)) return Stringify((double) (object) item);
				if (typeof(T) == typeof(float)) return Stringify((float) (object) item);
				if (typeof(T) == typeof(Guid)) return Stringify((Guid) (object) item);
				if (typeof(T) == typeof(Uuid128)) return Stringify((Uuid128) (object) item);
				if (typeof(T) == typeof(Uuid64)) return Stringify((Uuid64) (object) item);
				// </JIT_HACK>
				if (typeof(T) == typeof(string)) return Stringify((string) (object) item);

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
			/// Stringify((Slice)...) => hexa decimal string ("01 23 45 67 89 AB CD EF")
			/// </example>
			[NotNull]
			internal static string StringifyBoxed(object item)
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
					case Uuid64 u64:   return Stringify(u64);
				}

				// some other type
				return StringifyInternal(item);
			}

			private static string StringifyInternal(object item)
			{
				if (item is byte[] bytes) return Stringify(bytes.AsSlice());
				if (item is Slice slice) return Stringify(slice);
				if (item is ArraySegment<byte> buffer) return Stringify(buffer.AsSlice());
				//TODO: Span<T>, ReadOnlySpan<T>, Memory<T>, ReadOnlyMemory<T>, ...
				if (item is IFormattable f) return f.ToString(null, CultureInfo.InvariantCulture);

				// This will probably not give a meaningful result ... :(
				return item.ToString();
			}

			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			//TODO: escape the string? If it contains \0 or control chars, it can cause problems in the console or debugger output
			public static string Stringify(string item) => TokenDoubleQuote + item + TokenDoubleQuote; /* "hello" */

			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static string Stringify(bool item) => item ? TokenTrue : TokenFalse;

			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static string Stringify(int item) => StringConverters.ToString(item);

			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static string Stringify(uint item) => StringConverters.ToString(item);

			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static string Stringify(long item) => StringConverters.ToString(item);

			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static string Stringify(ulong item) => StringConverters.ToString(item);

			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static string Stringify(double item) => item.ToString("R", CultureInfo.InvariantCulture);

			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static string Stringify(float item) => item.ToString("R", CultureInfo.InvariantCulture);

			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static string Stringify(char item) => TokenSingleQuote + new string(item, 1) + TokenSingleQuote; /* 'X' */

			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static string Stringify(Slice item) => item.IsNull ? "null" : '`' + Slice.Dump(item, item.Count) + '`';

			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static string Stringify(byte[] item) => Stringify(item.AsSlice());

			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static string Stringify(ArraySegment<byte> item) => Stringify(item.AsSlice());

			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static string Stringify(Guid item) => item.ToString("B", CultureInfo.InstalledUICulture); /* {xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx} */

			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static string Stringify(Uuid128 item) => item.ToString("B", CultureInfo.InstalledUICulture); /* {xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx} */

			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static string Stringify(Uuid64 item) => item.ToString("B", CultureInfo.InstalledUICulture); /* {xxxxxxxx-xxxxxxxx} */

			/// <summary>Converts a list of object into a displaying string, for loggin/debugging purpose</summary>
			/// <param name="items">Array containing items to stringfy</param>
			/// <param name="offset">Start offset of the items to convert</param>
			/// <param name="count">Number of items to convert</param>
			/// <returns>String representation of the tuple in the form "(item1, item2, ... itemN,)"</returns>
			/// <example>ToString(STuple.Create("hello", 123, true, "world")) => "(\"hello\", 123, true, \"world\",)</example>
			[NotNull]
			public static string ToString(object[] items, int offset, int count)
			{
				if (items == null) return String.Empty;
				Contract.Requires(offset >= 0 && count >= 0);

				if (count <= 0)
				{ // empty tuple: "()"
					return TokenTupleEmpty;
				}

				var sb = new StringBuilder();
				sb.Append('(');
				sb.Append(StringifyBoxed(items[offset++]));

				if (count == 1)
				{ // singleton tuple : "(X,)"
					return sb.Append(TokenTupleSingleClose).ToString();
				}

				while (--count > 0)
				{
					sb.Append(TokenTupleSep /* ", " */).Append(StringifyBoxed(items[offset++]));
				}
				return sb.Append(TokenTupleClose /* ",)" */).ToString();
			}

			/// <summary>Converts a sequence of object into a displaying string, for loggin/debugging purpose</summary>
			/// <param name="items">Sequence of items to stringfy</param>
			/// <returns>String representation of the tuple in the form "(item1, item2, ... itemN,)"</returns>
			/// <example>ToString(STuple.Create("hello", 123, true, "world")) => "(\"hello\", 123, true, \"world\")</example>
			[NotNull]
			public static string ToString(IEnumerable<object> items)
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
						sb.Append(TokenTupleSep).Append(StringifyBoxed(enumerator.Current));
					}
					// add a trailing ',' for singletons
					return sb.Append(singleton ? TokenTupleSingleClose : TokenTupleClose).ToString();
				}
			}

		}

		/// <summary>Hleper to parse strings back into tuples</summary>
		public static class Deformatter
		{


			[Pure, NotNull]
			public static ITuple Parse([NotNull] string expression)
			{
				Contract.NotNullOrWhiteSpace(expression, nameof(expression));
				var parser = new Parser(expression.Trim());
				var tuple = parser.ParseExpression();
				if (parser.HasMore) throw new FormatException("Unexpected token after final ')' in Tuple expression.");
				return tuple;
			}

			/// <summary>Parse a tuple expression at the start of a string</summary>
			/// <param name="expression">String who starts with a valid Tuple expression, with optional extra characters</param>
			/// <returns>First item is the parsed tuple, and the second item is the rest of the string (or null if we consumed the whole expression)</returns>
			public static void ParseNext(string expression, out ITuple tuple, out string tail)
			{
				Contract.NotNullOrWhiteSpace(expression, nameof(expression));
				if (string.IsNullOrWhiteSpace(expression))
				{
					tuple = null;
					tail = null;
					return;
				}

				var parser = new Parser(expression.Trim());
				tuple = parser.ParseExpression();
				string s = parser.GetTail();
				tail = string.IsNullOrWhiteSpace(s) ? null : s.Trim();
			}

			private struct Parser
			{

				private const char EOF = '\xFFFF';

				public Parser(string expression)
				{
					this.Expression = expression;
					this.Cursor = 0;
				}

				public readonly string Expression;
				private int Cursor;

				public bool HasMore => this.Cursor < this.Expression.Length;

				[CanBeNull]
				public string GetTail() => this.Cursor < this.Expression.Length ? this.Expression.Substring(this.Cursor) : null;

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
					//IMPORTANT: 'keyword' doit être en lowercase!
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
				[Pure, NotNull]
				public ITuple ParseExpression()
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
								return items.Count == 0 ? STuple.Empty : new ListTuple(items);
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

								if (c == 't' || c == 'T')
								{ // true?
									if (!TryReadKeyword("true")) throw new FormatException("Unrecognized keyword in Tuple expression. Did you meant to write 'true' instead?");
									items.Add(true);
									expectItem = false;
									break;
								}

								if (c == 'f' || c == 'F')
								{ // false?
									if (!TryReadKeyword("false")) throw new FormatException("Unrecognized keyword in Tuple expression. Did you meant to write 'false' instead?");
									items.Add(false);
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
							if (dec) throw new FormatException("Redundant '.' in number that already has a decimal point.");
							if (exp) throw new FormatException("Unexpected '.' in exponent part of number.");
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
							if (dec) throw new FormatException("Redundant 'E' in number that already has an exponent.");
							exp = true;
							++p;
							continue;
						}

						if (c == '-' || c == '+')
						{
							if (!exp) throw new FormatException("Unexpected sign in number.");
							++p;
							continue;
						}

						throw new FormatException($"Unexpected token '{c}' while parsing number in Tuple expression.");
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

						if (x <= int.MaxValue) return (int) x;
						if (x <= long.MaxValue) return (long) x;
						return x;
					}

					return double.Parse(s.Substring(start, p - start), CultureInfo.InvariantCulture);
				}

				private string ReadStringLiteral()
				{
					string s = this.Expression;
					int p = this.Cursor;
					int end = p + s.Length;

					// main loop is optimistic and assumes that the string will not be escaped.
					// If we find the first instance of '\', then we switch to a secondary loop that uses a StringBuilder to decode each character

					char c = s[p++];
					if (c != '"') throw new FormatException("Expected '\"' token is missing in Tuple expression");
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
					var sb = new StringBuilder();
					if (p > start + 1) sb.Append(s.Substring(start, p - start - 1)); // copy what we have parsed so far
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
								return sb.ToString();
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
							if (c == 't') c = '\t';
							else if (c == 'r') c = '\r';
							else if (c == 'n') c = '\n';
							//TODO: \x## and \u#### syntax!
							else throw new FormatException($"Unrecognized '\\{c}' token while parsing string in Tuple expression");
							escape = false;
						}
						++p;
						sb.Append(c);
					}
				truncated_string:
					throw new FormatException("Missing double quote at end of string in Tuple expression");
				}

				private Guid ReadGuidLiteral()
				{
					var s = this.Expression;
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
							string lit = s.Substring(start, p - start);
							// Shortcut: "{} or {0} means "00000000-0000-0000-0000-000000000000"
							Guid g = lit == "" || lit == "0" ? Guid.Empty : Guid.Parse(lit);
							this.Cursor = p + 1;
							return g;
						}
						++p;
					}

					throw new FormatException("Invalid GUID in Tuple expression.");
				}

			}
		}

		#endregion
	}
}
