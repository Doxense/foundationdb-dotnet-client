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

namespace Doxense.Collections.Tuples
{
	using System.ComponentModel;
	using Doxense.Collections.Tuples.Encoding;
	using Doxense.Memory;
	using Doxense.Serialization.Encoders;

	/// <summary>Tuple Binary Encoding</summary>
	[PublicAPI]
	public static class TuPack
	{

		/// <summary>Key encoding that uses the Tuple Binary Encoding</summary>
		public static IDynamicTypeSystem Encoding => TupleKeyEncoding.Instance;

		#region Packing...

		// Without prefix

		/// <summary>Pack a tuple into a slice</summary>
		/// <param name="tuple">Tuple that must be serialized into a binary slice</param>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Slice Pack(IVarTuple? tuple)
		{
			return TupleEncoder.Pack(tuple);
		}

		/// <summary>Pack a tuple into a slice</summary>
		/// <param name="tuple">Tuple that must be serialized into a binary slice</param>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Slice Pack<TTuple>(in TTuple? tuple)
			where TTuple : IVarTuple?
		{
			return TupleEncoder.Pack(in tuple);
		}

		/// <summary>Pack a tuple into a slice</summary>
		/// <param name="tuple">Tuple that must be serialized into a binary slice</param>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Slice Pack<T1>(STuple<T1> tuple)
		{
			return TupleEncoder.Pack(default, in tuple);
		}

		/// <summary>Pack a tuple into a slice</summary>
		/// <param name="tuple">Tuple that must be serialized into a binary slice</param>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Slice Pack<T1, T2>(STuple<T1, T2> tuple)
		{
			return TupleEncoder.Pack(default, in tuple);
		}

		/// <summary>Pack a tuple into a slice</summary>
		/// <param name="tuple">Tuple that must be serialized into a binary slice</param>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Slice Pack<T1, T2, T3>(STuple<T1, T2, T3> tuple)
		{
			return TupleEncoder.Pack(default, in tuple);
		}

		/// <summary>Pack a tuple into a slice</summary>
		/// <param name="tuple">Tuple that must be serialized into a binary slice</param>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Slice Pack<T1, T2, T3, T4>(STuple<T1, T2, T3, T4> tuple)
		{
			return TupleEncoder.Pack(default, in tuple);
		}

		/// <summary>Pack a tuple into a slice</summary>
		/// <param name="tuple">Tuple that must be serialized into a binary slice</param>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Slice Pack<T1, T2, T3, T4, T5>(STuple<T1, T2, T3, T4, T5> tuple)
		{
			return TupleEncoder.Pack(default, in tuple);
		}

		/// <summary>Pack a tuple into a slice</summary>
		/// <param name="tuple">Tuple that must be serialized into a binary slice</param>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Slice Pack<T1, T2, T3, T4, T5, T6>(STuple<T1, T2, T3, T4, T5, T6> tuple)
		{
			return TupleEncoder.Pack(default, in tuple);
		}

		/// <summary>Pack a tuple into a slice</summary>
		/// <param name="tuple">Tuple that must be serialized into a binary slice</param>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Slice Pack<T1, T2, T3, T4, T5, T6, T7>(STuple<T1, T2, T3, T4, T5, T6, T7> tuple)
		{
			return TupleEncoder.Pack(default, in tuple);
		}

		/// <summary>Pack a tuple into a slice</summary>
		/// <param name="tuple">Tuple that must be serialized into a binary slice</param>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Slice Pack<T1>(ValueTuple<T1> tuple)
		{
			return TupleEncoder.Pack(default, in tuple);
		}

		/// <summary>Pack a tuple into a slice</summary>
		/// <param name="tuple">Tuple that must be serialized into a binary slice</param>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Slice Pack<T1, T2>((T1, T2) tuple)
		{
			return TupleEncoder.Pack(default, in tuple);
		}

		/// <summary>Pack a tuple into a slice</summary>
		/// <param name="tuple">Tuple that must be serialized into a binary slice</param>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Slice Pack<T1, T2, T3>((T1, T2, T3) tuple)
		{
			return TupleEncoder.Pack(default, in tuple);
		}

		/// <summary>Pack a tuple into a slice</summary>
		/// <param name="tuple">Tuple that must be serialized into a binary slice</param>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Slice Pack<T1, T2, T3, T4>((T1, T2, T3, T4) tuple)
		{
			return TupleEncoder.Pack(default, in tuple);
		}

		/// <summary>Pack a tuple into a slice</summary>
		/// <param name="tuple">Tuple that must be serialized into a binary slice</param>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Slice Pack<T1, T2, T3, T4, T5>((T1, T2, T3, T4, T5) tuple)
		{
			return TupleEncoder.Pack(default, in tuple);
		}

		/// <summary>Pack a tuple into a slice</summary>
		/// <param name="tuple">Tuple that must be serialized into a binary slice</param>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Slice Pack<T1, T2, T3, T4, T5, T6>((T1, T2, T3, T4, T5, T6) tuple)
		{
			return TupleEncoder.Pack(default, in tuple);
		}

		/// <summary>Pack a tuple into a slice</summary>
		/// <param name="tuple">Tuple that must be serialized into a binary slice</param>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Slice Pack<T1, T2, T3, T4, T5, T6, T7>((T1, T2, T3, T4, T5, T6, T7) tuple)
		{
			return TupleEncoder.Pack(default, in tuple);
		}

		/// <summary>Pack a tuple into a slice</summary>
		/// <param name="tuple">Tuple that must be serialized into a binary slice</param>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Slice Pack<T1, T2, T3, T4, T5, T6, T7, T8>((T1, T2, T3, T4, T5, T6, T7, T8) tuple)
		{
			return TupleEncoder.Pack(default, in tuple);
		}

		/// <summary>Pack an array of N-tuples, all sharing the same buffer</summary>
		/// <param name="tuples">Sequence of N-tuples to pack</param>
		/// <returns>Array containing the buffer segment of each packed tuple</returns>
		/// <example>BatchPack([ ("Foo", 1), ("Foo", 2) ]) => [ "\x02Foo\x00\x15\x01", "\x02Foo\x00\x15\x02" ] </example>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Slice[] PackTuples(params IVarTuple[] tuples)
		{
			return TupleEncoder.Pack(default, tuples);
		}

		/// <summary>Pack an array of N-tuples, all sharing the same buffer</summary>
		/// <param name="tuples">Sequence of N-tuples to pack</param>
		/// <returns>Array containing the buffer segment of each packed tuple</returns>
		/// <example>BatchPack([ ("Foo", 1), ("Foo", 2) ]) => [ "\x02Foo\x00\x15\x01", "\x02Foo\x00\x15\x02" ] </example>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
#if NET9_0_OR_GREATER
		public static Slice[] PackTuples(params ReadOnlySpan<IVarTuple> tuples)
#else
		public static Slice[] PackTuples(ReadOnlySpan<IVarTuple> tuples)
#endif
		{
			return TupleEncoder.Pack(default, tuples);
		}

		/// <summary>Pack an array of N-tuples, all sharing the same buffer</summary>
		/// <param name="tuples">Sequence of N-tuples to pack</param>
		/// <returns>Array containing the buffer segment of each packed tuple</returns>
		/// <example>BatchPack([ ("Foo", 1), ("Foo", 2) ]) => [ "\x02Foo\x00\x15\x01", "\x02Foo\x00\x15\x02" ] </example>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Slice[] PackTuples<TTuple>(params TTuple[] tuples)
			where TTuple : IVarTuple?
		{
			return TupleEncoder.Pack<TTuple>(default, tuples);
		}

		/// <summary>Pack an array of N-tuples, all sharing the same buffer</summary>
		/// <param name="tuples">Sequence of N-tuples to pack</param>
		/// <returns>Array containing the buffer segment of each packed tuple</returns>
		/// <example>BatchPack([ ("Foo", 1), ("Foo", 2) ]) => [ "\x02Foo\x00\x15\x01", "\x02Foo\x00\x15\x02" ] </example>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
#if NET9_0_OR_GREATER
		public static Slice[] PackTuples<TTuple>(params ReadOnlySpan<TTuple> tuples)
#else
		public static Slice[] PackTuples<TTuple>(ReadOnlySpan<TTuple> tuples)
#endif
			where TTuple : IVarTuple?
		{
			return TupleEncoder.Pack<TTuple>(default, tuples);
		}

		/// <summary>Pack a sequence of N-tuples, all sharing the same buffer</summary>
		/// <param name="tuples">Sequence of N-tuples to pack</param>
		/// <returns>Array containing the buffer segment of each packed tuple</returns>
		/// <example>BatchPack([ ("Foo", 1), ("Foo", 2) ]) => [ "\x02Foo\x00\x15\x01", "\x02Foo\x00\x15\x02" ] </example>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Slice[] PackTuples([InstantHandle] this IEnumerable<IVarTuple> tuples)
		{
			return TupleEncoder.Pack(default, tuples);
		}

		/// <summary>Efficiently write the packed representation of a tuple</summary>
		/// <param name="writer">Output buffer</param>
		/// <param name="tuple">Tuple that must be serialized into a binary slice</param>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void PackTo<TTuple>(ref SliceWriter writer, TTuple? tuple)
			where TTuple : IVarTuple?
		{
			TupleEncoder.PackTo(ref writer, tuple);
		}

		// With prefix

		/// <summary>Efficiently concatenate a prefix with the packed representation of a tuple</summary>
		/// <param name="prefix">Prefix added to the start of the packed slice</param>
		/// <summary>Pack a tuple into a slice</summary>
		/// <param name="tuple">Tuple that must be serialized into a binary slice</param>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Slice Pack<TTuple>(Slice prefix, TTuple? tuple)
			where TTuple : IVarTuple?
		{
			return TupleEncoder.Pack(prefix.Span, tuple);
		}

		/// <summary>Efficiently concatenate a prefix with the packed representation of a tuple</summary>
		/// <param name="prefix">Prefix added to the start of the packed slice</param>
		/// <summary>Pack a tuple into a slice</summary>
		/// <param name="tuple">Tuple that must be serialized into a binary slice</param>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Slice Pack<TTuple>(ReadOnlySpan<byte> prefix, TTuple? tuple)
			where TTuple : IVarTuple?
		{
			return TupleEncoder.Pack(prefix, tuple);
		}

		/// <summary>Pack a tuple into a slice</summary>
		/// <param name="prefix">Prefix added to the start of the packed slice</param>
		/// <param name="tuple">Tuple that must be serialized into a binary slice</param>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Slice Pack<T1>(Slice prefix, STuple<T1> tuple)
		{
			return TupleEncoder.Pack(prefix.Span, in tuple);
		}

		/// <summary>Pack a tuple into a slice</summary>
		/// <param name="prefix">Prefix added to the start of the packed slice</param>
		/// <param name="tuple">Tuple that must be serialized into a binary slice</param>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Slice Pack<T1>(ReadOnlySpan<byte> prefix, STuple<T1> tuple)
		{
			return TupleEncoder.Pack(prefix, in tuple);
		}

		/// <summary>Pack a tuple into a slice</summary>
		/// <param name="prefix">Prefix added to the start of the packed slice</param>
		/// <param name="tuple">Tuple that must be serialized into a binary slice</param>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Slice Pack<T1, T2>(Slice prefix, STuple<T1, T2> tuple)
		{
			return TupleEncoder.Pack(prefix.Span, in tuple);
		}

		/// <summary>Pack a tuple into a slice</summary>
		/// <param name="prefix">Prefix added to the start of the packed slice</param>
		/// <param name="tuple">Tuple that must be serialized into a binary slice</param>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Slice Pack<T1, T2>(ReadOnlySpan<byte> prefix, STuple<T1, T2> tuple)
		{
			return TupleEncoder.Pack(prefix, in tuple);
		}

		/// <summary>Pack a tuple into a slice</summary>
		/// <param name="prefix">Prefix added to the start of the packed slice</param>
		/// <param name="tuple">Tuple that must be serialized into a binary slice</param>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Slice Pack<T1, T2, T3>(Slice prefix, STuple<T1, T2, T3> tuple)
		{
			return TupleEncoder.Pack(prefix.Span, in tuple);
		}

		/// <summary>Pack a tuple into a slice</summary>
		/// <param name="prefix">Prefix added to the start of the packed slice</param>
		/// <param name="tuple">Tuple that must be serialized into a binary slice</param>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Slice Pack<T1, T2, T3>(ReadOnlySpan<byte> prefix, STuple<T1, T2, T3> tuple)
		{
			return TupleEncoder.Pack(prefix, in tuple);
		}

		/// <summary>Pack a tuple into a slice</summary>
		/// <param name="prefix">Prefix added to the start of the packed slice</param>
		/// <param name="tuple">Tuple that must be serialized into a binary slice</param>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Slice Pack<T1, T2, T3, T4>(Slice prefix, STuple<T1, T2, T3, T4> tuple)
		{
			return TupleEncoder.Pack(prefix.Span, in tuple);
		}

		/// <summary>Pack a tuple into a slice</summary>
		/// <param name="prefix">Prefix added to the start of the packed slice</param>
		/// <param name="tuple">Tuple that must be serialized into a binary slice</param>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Slice Pack<T1, T2, T3, T4>(ReadOnlySpan<byte> prefix, STuple<T1, T2, T3, T4> tuple)
		{
			return TupleEncoder.Pack(prefix, in tuple);
		}

		/// <summary>Pack a tuple into a slice</summary>
		/// <param name="prefix">Prefix added to the start of the packed slice</param>
		/// <param name="tuple">Tuple that must be serialized into a binary slice</param>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Slice Pack<T1, T2, T3, T4, T5>(Slice prefix, STuple<T1, T2, T3, T4, T5> tuple)
		{
			return TupleEncoder.Pack(prefix.Span, in tuple);
		}

		/// <summary>Pack a tuple into a slice</summary>
		/// <param name="prefix">Prefix added to the start of the packed slice</param>
		/// <param name="tuple">Tuple that must be serialized into a binary slice</param>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Slice Pack<T1, T2, T3, T4, T5>(ReadOnlySpan<byte> prefix, STuple<T1, T2, T3, T4, T5> tuple)
		{
			return TupleEncoder.Pack(prefix, in tuple);
		}

		/// <summary>Pack a tuple into a slice</summary>
		/// <param name="prefix">Common prefix added to all the tuples</param>
		/// <param name="tuple">Tuple that must be serialized into a binary slice</param>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Slice Pack<T1, T2, T3, T4, T5, T6>(Slice prefix, STuple<T1, T2, T3, T4, T5, T6> tuple)
		{
			return TupleEncoder.Pack(prefix.Span, in tuple);
		}

		/// <summary>Pack a tuple into a slice</summary>
		/// <param name="prefix">Common prefix added to all the tuples</param>
		/// <param name="tuple">Tuple that must be serialized into a binary slice</param>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Slice Pack<T1, T2, T3, T4, T5, T6>(ReadOnlySpan<byte> prefix, STuple<T1, T2, T3, T4, T5, T6> tuple)
		{
			return TupleEncoder.Pack(prefix, in tuple);
		}

		/// <summary>Pack a tuple into a slice</summary>
		/// <param name="prefix">Common prefix added to all the tuples</param>
		/// <param name="tuple">Tuple that must be serialized into a binary slice</param>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Slice Pack<T1, T2, T3, T4, T5, T6, T7>(Slice prefix, STuple<T1, T2, T3, T4, T5, T6, T7> tuple)
		{
			return TupleEncoder.Pack(prefix.Span, in tuple);
		}

		/// <summary>Pack a tuple into a slice</summary>
		/// <param name="prefix">Common prefix added to all the tuples</param>
		/// <param name="tuple">Tuple that must be serialized into a binary slice</param>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Slice Pack<T1, T2, T3, T4, T5, T6, T7>(ReadOnlySpan<byte> prefix, STuple<T1, T2, T3, T4, T5, T6, T7> tuple)
		{
			return TupleEncoder.Pack(prefix, in tuple);
		}

		/// <summary>Pack an array of N-tuples, all sharing the same buffer</summary>
		/// <param name="prefix">Common prefix added to all the tuples</param>
		/// <param name="tuples">Sequence of N-tuples to pack</param>
		/// <returns>Array containing the buffer segment of each packed tuple</returns>
		/// <example>BatchPack("abc", [ ("Foo", 1), ("Foo", 2) ]) => [ "abc\x02Foo\x00\x15\x01", "abc\x02Foo\x00\x15\x02" ] </example>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Slice[] PackTuples(Slice prefix, params IVarTuple[] tuples)
		{
			return TupleEncoder.Pack(prefix.Span, tuples);
		}

		/// <summary>Pack an array of N-tuples, all sharing the same buffer</summary>
		/// <param name="prefix">Common prefix added to all the tuples</param>
		/// <param name="tuples">Sequence of N-tuples to pack</param>
		/// <returns>Array containing the buffer segment of each packed tuple</returns>
		/// <example>BatchPack("abc", [ ("Foo", 1), ("Foo", 2) ]) => [ "abc\x02Foo\x00\x15\x01", "abc\x02Foo\x00\x15\x02" ] </example>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Slice[] PackTuples(ReadOnlySpan<byte> prefix, params IVarTuple[] tuples)
		{
			return TupleEncoder.Pack(prefix, tuples);
		}

		/// <summary>Pack an array of N-tuples, all sharing the same buffer</summary>
		/// <param name="prefix">Common prefix added to all the tuples</param>
		/// <param name="tuples">Sequence of N-tuples to pack</param>
		/// <returns>Array containing the buffer segment of each packed tuple</returns>
		/// <example>BatchPack("abc", [ ("Foo", 1), ("Foo", 2) ]) => [ "abc\x02Foo\x00\x15\x01", "abc\x02Foo\x00\x15\x02" ] </example>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
#if NET9_0_OR_GREATER
		public static Slice[] PackTuples(Slice prefix, params ReadOnlySpan<IVarTuple> tuples)
#else
		public static Slice[] PackTuples(Slice prefix, ReadOnlySpan<IVarTuple> tuples)
#endif
		{
			return TupleEncoder.Pack(prefix.Span, tuples);
		}

		/// <summary>Pack an array of N-tuples, all sharing the same buffer</summary>
		/// <param name="prefix">Common prefix added to all the tuples</param>
		/// <param name="tuples">Sequence of N-tuples to pack</param>
		/// <returns>Array containing the buffer segment of each packed tuple</returns>
		/// <example>BatchPack("abc", [ ("Foo", 1), ("Foo", 2) ]) => [ "abc\x02Foo\x00\x15\x01", "abc\x02Foo\x00\x15\x02" ] </example>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
#if NET9_0_OR_GREATER
		public static Slice[] PackTuples(ReadOnlySpan<byte> prefix, params ReadOnlySpan<IVarTuple> tuples)
#else
		public static Slice[] PackTuples(ReadOnlySpan<byte> prefix, ReadOnlySpan<IVarTuple> tuples)
#endif
		{
			return TupleEncoder.Pack(prefix, tuples);
		}

		/// <summary>Pack a sequence of N-tuples, all sharing the same buffer</summary>
		/// <param name="prefix">Common prefix added to all the tuples</param>
		/// <param name="tuples">Sequence of N-tuples to pack</param>
		/// <returns>Array containing the buffer segment of each packed tuple</returns>
		/// <example>BatchPack("abc", [ ("Foo", 1), ("Foo", 2) ]) => [ "abc\x02Foo\x00\x15\x01", "abc\x02Foo\x00\x15\x02" ] </example>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Slice[] PackTuples(Slice prefix, IEnumerable<IVarTuple> tuples)
		{
			return TupleEncoder.Pack(prefix.Span, tuples);
		}

		/// <summary>Pack a sequence of N-tuples, all sharing the same buffer</summary>
		/// <param name="prefix">Common prefix added to all the tuples</param>
		/// <param name="tuples">Sequence of N-tuples to pack</param>
		/// <returns>Array containing the buffer segment of each packed tuple</returns>
		/// <example>BatchPack("abc", [ ("Foo", 1), ("Foo", 2) ]) => [ "abc\x02Foo\x00\x15\x01", "abc\x02Foo\x00\x15\x02" ] </example>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Slice[] PackTuples(ReadOnlySpan<byte> prefix, IEnumerable<IVarTuple> tuples)
		{
			return TupleEncoder.Pack(prefix, tuples);
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Slice[] PackTuples<TElement, TTuple>(Slice prefix, TElement[] elements, Func<TElement, TTuple> transform)
			where TTuple : IVarTuple?
		{
			return TupleEncoder.Pack(prefix.Span, elements, transform);
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Slice[] PackTuples<TElement, TTuple>(ReadOnlySpan<byte> prefix, TElement[] elements, Func<TElement, TTuple> transform)
			where TTuple : IVarTuple?
		{
			return TupleEncoder.Pack(prefix, elements, transform);
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Slice[] PackTuples<TElement, TTuple>(Slice prefix, IEnumerable<TElement> elements, Func<TElement, TTuple> transform)
			where TTuple : IVarTuple?
		{
			return TupleEncoder.Pack(prefix.Span, elements, transform);
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Slice[] PackTuples<TElement, TTuple>(ReadOnlySpan<byte> prefix, IEnumerable<TElement> elements, Func<TElement, TTuple> transform)
			where TTuple : IVarTuple?
		{
			return TupleEncoder.Pack(prefix, elements, transform);
		}

		#endregion

		#region Encode

		//REVIEW: EncodeKey/EncodeKeys? Encode/EncodeRange? EncodeValues? EncodeItems?

		/// <summary>Pack a 1-tuple directly into a slice</summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Slice EncodeKey<T1>(T1 item1)
		{
			return TupleEncoder.EncodeKey(prefix: default, item1);
		}

		/// <summary>Pack a 2-tuple directly into a slice</summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Slice EncodeKey<T1, T2>(T1 item1, T2 item2)
		{
			return TupleEncoder.EncodeKey(prefix: default, item1, item2);
		}

		/// <summary>Pack a 3-tuple directly into a slice</summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Slice EncodeKey<T1, T2, T3>(T1 item1, T2 item2, T3 item3)
		{
			return TupleEncoder.EncodeKey(prefix: default, item1, item2, item3);
		}

		/// <summary>Pack a 4-tuple directly into a slice</summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Slice EncodeKey<T1, T2, T3, T4>(T1 item1, T2 item2, T3 item3, T4 item4)
		{
			return TupleEncoder.EncodeKey(prefix: default, item1, item2, item3, item4);
		}

		/// <summary>Pack a 5-tuple directly into a slice</summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Slice EncodeKey<T1, T2, T3, T4, T5>(T1 item1, T2 item2, T3 item3, T4 item4, T5 item5)
		{
			return TupleEncoder.EncodeKey(prefix: default, item1, item2, item3, item4, item5);
		}

		/// <summary>Pack a 6-tuple directly into a slice</summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Slice EncodeKey<T1, T2, T3, T4, T5, T6>(T1 item1, T2 item2, T3 item3, T4 item4, T5 item5, T6 item6)
		{
			return TupleEncoder.EncodeKey(prefix: default, item1, item2, item3, item4, item5, item6);
		}

		/// <summary>Pack a 6-tuple directly into a slice</summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Slice EncodeKey<T1, T2, T3, T4, T5, T6, T7>(T1 item1, T2 item2, T3 item3, T4 item4, T5 item5, T6 item6, T7 item7)
		{
			return TupleEncoder.EncodeKey(prefix: default, item1, item2, item3, item4, item5, item6, item7);
		}

		/// <summary>Pack a 6-tuple directly into a slice</summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Slice EncodeKey<T1, T2, T3, T4, T5, T6, T7, T8>(T1 item1, T2 item2, T3 item3, T4 item4, T5 item5, T6 item6, T7 item7, T8 item8)
		{
			return TupleEncoder.EncodeKey(prefix: default, item1, item2, item3, item4, item5, item6, item7, item8);
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Slice[] EncodeKeys<TKey>(IEnumerable<TKey> keys)
		{
			return TupleEncoder.EncodeKeys(prefix: default, keys);
		}

		/// <summary>Encodes a sequence of keys with a common prefix, all sharing the same buffer</summary>
		/// <typeparam name="T">Type of the keys</typeparam>
		/// <param name="prefix">Prefix shared by all keys</param>
		/// <param name="keys">Sequence of keys to pack</param>
		/// <returns>Array of slices (for all keys) that share the same underlying buffer</returns>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Slice[] EncodePrefixedKeys<T>(Slice prefix, IEnumerable<T> keys)
		{
			return TupleEncoder.EncodeKeys(prefix.Span, keys);
		}

		/// <summary>Encodes a sequence of keys with a common prefix, all sharing the same buffer</summary>
		/// <typeparam name="T">Type of the keys</typeparam>
		/// <param name="prefix">Prefix shared by all keys</param>
		/// <param name="keys">Sequence of keys to pack</param>
		/// <returns>Array of slices (for all keys) that share the same underlying buffer</returns>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Slice[] EncodePrefixedKeys<T>(ReadOnlySpan<byte> prefix, IEnumerable<T> keys)
		{
			return TupleEncoder.EncodeKeys(prefix, keys);
		}

		/// <summary>Encodes a sequence of keys, all sharing the same buffer</summary>
		/// <typeparam name="TKey">Type of the keys</typeparam>
		/// <param name="keys">Sequence of keys to pack</param>
		/// <returns>Array of slices (fo all keys) that share the same underlyinh buffer</returns>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Slice[] EncodeKeys<TKey>(TKey[] keys)
		{
			Contract.NotNull(keys);
			return TupleEncoder.EncodeKeys(prefix: default, new ReadOnlySpan<TKey>(keys));
		}

		/// <summary>Encodes an array of keys with a common prefix, all sharing the same buffer</summary>
		/// <typeparam name="T">Type of the keys</typeparam>
		/// <param name="prefix">Prefix shared by all keys</param>
		/// <param name="keys">Sequence of keys to pack</param>
		/// <returns>Array of slices (for all keys) that share the same underlying buffer</returns>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Slice[] EncodePrefixedKeys<T>(Slice prefix, T[] keys)
		{
			Contract.NotNull(keys);
			return TupleEncoder.EncodeKeys(prefix.Span, new ReadOnlySpan<T>(keys));
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Slice[] EncodeKeys<TKey>(ReadOnlySpan<TKey> keys)
		{
			return TupleEncoder.EncodeKeys(prefix: default, keys);
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		[EditorBrowsable(EditorBrowsableState.Never)]
		public static Slice[] EncodeKeys<TKey>(Span<TKey> keys)
		{
			// This only exists to fix an overload resolution ambiguity in the following situation:
			// > var keys = TuPack.EncodeKeys(..., someArray.AsSpan(...))
			// In this case, AsSpan(..) returns a Span<T> which is not ReadOnlySpan<T>, so the compiler will prefer the (IEnumerable<T>) overload instead of (ReadOnlySpan<T>)
			return TupleEncoder.EncodeKeys(prefix: default, (ReadOnlySpan<TKey>) keys);
		}

		/// <summary>Encodes an array of keys with a common prefix, all sharing the same buffer</summary>
		/// <typeparam name="T">Type of the keys</typeparam>
		/// <param name="prefix">Prefix shared by all keys</param>
		/// <param name="keys">Sequence of keys to encode</param>
		/// <returns>Array of slices (for all keys) that share the same underlying buffer</returns>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Slice[] EncodePrefixedKeys<T>(Slice prefix, ReadOnlySpan<T> keys)
		{
			return TupleEncoder.EncodeKeys(prefix.Span, keys);
		}

		/// <summary>Encodes a span of keys with a same prefix, all sharing the same buffer</summary>
		/// <typeparam name="T">Type of the keys</typeparam>
		/// <param name="prefix">Prefix shared by all keys</param>
		/// <param name="keys">Sequence of keys to pack</param>
		/// <returns>Array of slices (for all keys) that share the same underlying buffer</returns>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Slice[] EncodePrefixedKeys<T>(ReadOnlySpan<byte> prefix, ReadOnlySpan<T> keys)
		{
			return TupleEncoder.EncodeKeys(prefix, keys);
		}

		/// <summary>Encodes a span of keys with a common prefix, all sharing the same buffer</summary>
		/// <typeparam name="T">Type of the keys</typeparam>
		/// <param name="prefix">Prefix shared by all keys</param>
		/// <param name="keys">Sequence of keys to pack</param>
		/// <returns>Array of slices (for all keys) that share the same underlying buffer</returns>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		[EditorBrowsable(EditorBrowsableState.Never)]
		public static Slice[] EncodePrefixedKeys<T>(ReadOnlySpan<byte> prefix, Span<T> keys)
		{
			// This only exists to fix an overload resolution ambiguity issue between ReadOnlySpan<T> and IEnumerable<T>:
			// > var keys = TuPack.EncodePrefixKeys(..., someArray.AsSpan(...))
			// In this case, AsSpan(..) returns a Span<T> which is not ReadOnlySpan<T>, so the compiler will prefer the (..., IEnumerable<T>) overload instead of (..., ReadOnlySpan<T>)
			return TupleEncoder.EncodeKeys(prefix, (ReadOnlySpan<T>) keys);
		}

		/// <summary>Merge an array of keys with a same prefix, all sharing the same buffer</summary>
		/// <typeparam name="T">Type of the keys</typeparam>
		/// <param name="prefix">Prefix shared by all keys</param>
		/// <param name="keys">Sequence of keys to pack</param>
		/// <returns>Array of slices (for all keys) that share the same underlying buffer</returns>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		[EditorBrowsable(EditorBrowsableState.Never)]
		public static Slice[] EncodePrefixedKeys<T>(Slice prefix, Span<T> keys)
		{
			// This only exists to fix an overload resolution ambiguity issue between ReadOnlySpan<T> and IEnumerable<T>:
			// > var keys = TuPack.EncodePrefixKeys(..., someArray.AsSpan(...))
			// In this case, AsSpan(..) returns a Span<T> which is not ReadOnlySpan<T>, so the compiler will prefer the (..., IEnumerable<T>) overload instead of (..., ReadOnlySpan<T>)
			return TupleEncoder.EncodeKeys(prefix.Span, (ReadOnlySpan<T>) keys);
		}

		/// <summary>Merge an array of elements, all sharing the same buffer</summary>
		/// <typeparam name="TElement">Type of the elements</typeparam>
		/// <typeparam name="TKey">Type of the keys extracted from the elements</typeparam>
		/// <param name="elements">Sequence of elements to pack</param>
		/// <param name="selector">Lambda that extract the key from each element</param>
		/// <returns>Array of slices (for all keys) that share the same underlying buffer</returns>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Slice[] EncodeKeys<TKey, TElement>(TElement[] elements, Func<TElement, TKey> selector)
		{
			Contract.NotNull(elements);
			return TupleEncoder.EncodeKeys(default, new ReadOnlySpan<TElement>(elements), selector);
		}

		/// <summary>Merge an array of elements, all sharing the same buffer</summary>
		/// <typeparam name="TElement">Type of the elements</typeparam>
		/// <typeparam name="TKey">Type of the keys extracted from the elements</typeparam>
		/// <param name="elements">Sequence of elements to pack</param>
		/// <param name="selector">Lambda that extract the key from each element</param>
		/// <returns>Array of slices (for all keys) that share the same underlying buffer</returns>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Slice[] EncodeKeys<TKey, TElement>(ReadOnlySpan<TElement> elements, Func<TElement, TKey> selector)
		{
			return TupleEncoder.EncodeKeys(default, elements, selector);
		}

		/// <summary>Merge an array of elements with a same prefix, all sharing the same buffer</summary>
		/// <typeparam name="TElement">Type of the elements</typeparam>
		/// <typeparam name="TKey">Type of the keys extracted from the elements</typeparam>
		/// <param name="prefix">Prefix shared by all keys (can be empty)</param>
		/// <param name="elements">Sequence of elements to pack</param>
		/// <param name="selector">Lambda that extract the key from each element</param>
		/// <returns>Array of slices (for all keys) that share the same underlying buffer</returns>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Slice[] EncodePrefixedKeys<TKey, TElement>(Slice prefix, TElement[] elements, Func<TElement, TKey> selector)
		{
			Contract.NotNull(elements);
			return TupleEncoder.EncodeKeys(prefix.Span, new ReadOnlySpan<TElement>(elements), selector);
		}

		/// <summary>Merge an array of elements with a same prefix, all sharing the same buffer</summary>
		/// <typeparam name="TElement">Type of the elements</typeparam>
		/// <typeparam name="TKey">Type of the keys extracted from the elements</typeparam>
		/// <param name="prefix">Prefix shared by all keys (can be empty)</param>
		/// <param name="elements">Sequence of elements to pack</param>
		/// <param name="selector">Lambda that extract the key from each element</param>
		/// <returns>Array of slices (for all keys) that share the same underlying buffer</returns>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Slice[] EncodePrefixedKeys<TKey, TElement>(Slice prefix, ReadOnlySpan<TElement> elements, Func<TElement, TKey> selector)
		{
			return TupleEncoder.EncodeKeys(prefix.Span, elements, selector);
		}

		/// <summary>Merge an array of elements with a same prefix, all sharing the same buffer</summary>
		/// <typeparam name="TElement">Type of the elements</typeparam>
		/// <typeparam name="TKey">Type of the keys extracted from the elements</typeparam>
		/// <param name="prefix">Prefix shared by all keys (can be empty)</param>
		/// <param name="elements">Sequence of elements to pack</param>
		/// <param name="selector">Lambda that extract the key from each element</param>
		/// <returns>Array of slices (for all keys) that share the same underlying buffer</returns>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Slice[] EncodePrefixedKeys<TKey, TElement>(ReadOnlySpan<byte> prefix, TElement[] elements, Func<TElement, TKey> selector)
		{
			return TupleEncoder.EncodeKeys(prefix, elements, selector);
		}

		/// <summary>Merge an array of elements with a same prefix, all sharing the same buffer</summary>
		/// <typeparam name="TElement">Type of the elements</typeparam>
		/// <typeparam name="TKey">Type of the keys extracted from the elements</typeparam>
		/// <param name="prefix">Prefix shared by all keys (can be empty)</param>
		/// <param name="elements">Sequence of elements to pack</param>
		/// <param name="selector">Lambda that extract the key from each element</param>
		/// <returns>Array of slices (for all keys) that share the same underlying buffer</returns>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Slice[] EncodePrefixedKeys<TKey, TElement>(ReadOnlySpan<byte> prefix, ReadOnlySpan<TElement> elements, Func<TElement, TKey> selector)
		{
			return TupleEncoder.EncodeKeys(prefix, elements, selector);
		}

		/// <summary>Pack a sequence of keys with a same prefix, all sharing the same buffer</summary>
		/// <typeparam name="T">Type of the keys</typeparam>
		/// <param name="prefix">Prefix shared by all keys</param>
		/// <param name="keys">Sequence of keys to pack</param>
		/// <returns>Array of slices (for all keys) that share the same underlying buffer</returns>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Slice[] EncodePrefixedKeys<T>(IVarTuple prefix, IEnumerable<T> keys)
		{
			Contract.NotNull(prefix);

			return EncodePrefixedKeys(Pack(prefix), keys);
		}

		/// <summary>Pack a sequence of keys with a same prefix, all sharing the same buffer</summary>
		/// <typeparam name="T">Type of the keys</typeparam>
		/// <param name="prefix">Prefix shared by all keys</param>
		/// <param name="keys">Sequence of keys to pack</param>
		/// <returns>Array of slices (for all keys) that share the same underlying buffer</returns>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Slice[] EncodePrefixedKeys<T>(IVarTuple prefix, T[] keys)
		{
			Contract.NotNull(prefix);
			Contract.NotNull(keys);

			return EncodePrefixedKeys(Pack(prefix), new ReadOnlySpan<T>(keys));
		}

		/// <summary>Pack a sequence of keys with a same prefix, all sharing the same buffer</summary>
		/// <typeparam name="T">Type of the keys</typeparam>
		/// <param name="prefix">Prefix shared by all keys</param>
		/// <param name="keys">Sequence of keys to pack</param>
		/// <returns>Array of slices (for all keys) that share the same underlying buffer</returns>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
#if NET9_0_OR_GREATER
		public static Slice[] EncodePrefixedKeys<T>(IVarTuple prefix, params ReadOnlySpan<T?> keys)
#else
		public static Slice[] EncodePrefixedKeys<T>(IVarTuple prefix, ReadOnlySpan<T?> keys)
#endif
		{
			Contract.NotNull(prefix);

			return EncodePrefixedKeys(Pack(prefix), keys);
		}

		#endregion

		#region Ranges...

		/// <summary>Create a range that selects all tuples that are stored under the specified subspace: 'prefix\x00' &lt;= k &lt; 'prefix\xFF'</summary>
		/// <param name="prefix">Subspace binary prefix (that will be excluded from the range)</param>
		/// <returns>Range including all possible tuples starting with the specified prefix.</returns>
		/// <remarks>TuPack.ToRange(Slice.FromAscii("abc")) returns the range [ 'abc\x00', 'abc\xFF' )</remarks>
		[Pure]
		public static (Slice Begin, Slice End) ToRange(Slice prefix)
		{
			if (prefix.IsNull) throw new ArgumentNullException(nameof(prefix));
			//note: there is no guarantee that prefix is a valid packed tuple (could be any exotic binary prefix)

			// prefix => [ prefix."\0", prefix."\xFF" )
			return (
				prefix + 0x00,
				prefix + 0xFF
			);
		}

		/// <summary>Create a range that selects all the tuples of greater length than the specified <paramref name="tuple"/>, and that start with the specified elements: packed(tuple)+'\x00' &lt;= k &lt; packed(tuple)+'\xFF'</summary>
		/// <example>TuPack.ToRange(STuple.Create("a", "b")) includes all tuples ("a", "b", ...), but not the tuple ("a", "b") itself.</example>
		[Pure]
		public static (Slice Begin, Slice End) ToRange<TTuple>(TTuple tuple)
			where TTuple : IVarTuple?
		{
			if (tuple is null) throw Contract.FailArgumentNull(nameof(tuple));

			// tuple => [ packed."\0", packed."\xFF" )
			var packed = TupleEncoder.Pack(in tuple);
			return (
				packed + 0x00,
				packed + 0xFF
			);
		}

		/// <summary>Create a range that selects all the tuples of greater length than the specified <paramref name="tuple"/>, and that start with the specified elements: packed(tuple)+'\x00' &lt;= k &lt; packed(tuple)+'\xFF'</summary>
		/// <example>ToRange(STuple.Create("a", "b")) includes all tuples ("a", "b", ...), but not the tuple ("a", "b") itself.</example>
		[Pure]
		public static (Slice Begin, Slice End) ToRange<T1>(STuple<T1> tuple)
		{
			Contract.NotNull(tuple);

			// tuple => [ packed."\0", packed."\xFF" )
			var packed = TupleEncoder.EncodeKey(default, tuple.Item1);
			return (
				packed + 0x00,
				packed + 0xFF
			);
		}

		/// <summary>Create a range that selects all the tuples of greater length than the specified <paramref name="tuple"/>, and that start with the specified elements: packed(tuple)+'\x00' &lt;= k &lt; packed(tuple)+'\xFF'</summary>
		/// <example>ToRange(STuple.Create("a", "b")) includes all tuples ("a", "b", ...), but not the tuple ("a", "b") itself.</example>
		[Pure]
		public static (Slice Begin, Slice End) ToRange<T1>(ValueTuple<T1> tuple)
		{
			Contract.NotNull(tuple);

			// tuple => [ packed."\0", packed."\xFF" )
			var packed = TupleEncoder.EncodeKey(default, tuple.Item1);
			return (
				packed + 0x00,
				packed + 0xFF
			);
		}

		/// <summary>Create a range that selects all the tuples of greater length than the specified element, and that start with the specified elements: packed(tuple)+'\x00' &lt;= k &lt; packed(tuple)+'\xFF'</summary>
		/// <example>ToRange(STuple.Create("a", "b")) includes all tuples ("a", "b", ...), but not the tuple ("a", "b") itself.</example>
		[Pure]
		public static (Slice Begin, Slice End) ToKeyRange<T1>(T1 item1)
		{
			// tuple => [ packed."\0", packed."\xFF" )
			var packed = TupleEncoder.EncodeKey(default, item1);
			return (
				packed + 0x00,
				packed + 0xFF
			);
		}

		/// <summary>Create a range that selects all the tuples of greater length than the specified element, and that start with the specified elements: packed(tuple)+'\x00' &lt;= k &lt; packed(tuple)+'\xFF'</summary>
		/// <example>ToRange(STuple.Create("a", "b")) includes all tuples ("a", "b", ...), but not the tuple ("a", "b") itself.</example>
		[Pure]
		public static (Slice Begin, Slice End) ToPrefixedKeyRange<T1>(Slice prefix, T1 item1)
		{
			// tuple => [ prefix.packed."\0", prefix.packed."\xFF" )
			var packed = TupleEncoder.EncodeKey(prefix.Span, item1);
			return (
				packed + 0x00,
				packed + 0xFF
			);
		}

		/// <summary>Create a range that selects all the tuples of greater length than the specified element, and that start with the specified elements: packed(tuple)+'\x00' &lt;= k &lt; packed(tuple)+'\xFF'</summary>
		/// <example>ToRange(STuple.Create("a", "b")) includes all tuples ("a", "b", ...), but not the tuple ("a", "b") itself.</example>
		[Pure]
		public static (Slice Begin, Slice End) ToPrefixedKeyRange<T1>(ReadOnlySpan<byte> prefix, T1 item1)
		{
			// tuple => [ prefix.packed."\0", prefix.packed."\xFF" )
			var packed = TupleEncoder.EncodeKey(prefix, item1);
			return (
				packed + 0x00,
				packed + 0xFF
			);
		}

		/// <summary>Create a range that selects all the tuples of greater length than the specified <paramref name="tuple"/>, and that start with the specified elements: packed(tuple)+'\x00' &lt;= k &lt; packed(tuple)+'\xFF'</summary>
		/// <example>ToRange(STuple.Create("a", "b")) includes all tuples ("a", "b", ...), but not the tuple ("a", "b") itself.</example>
		[Pure]
		public static (Slice Begin, Slice End) ToRange<T1, T2>(STuple<T1, T2> tuple)
		{
			Contract.NotNull(tuple);

			// tuple => [ packed."\0", packed."\xFF" )
			var packed = TupleEncoder.Pack(default, in tuple);
			return (
				packed + 0x00,
				packed + 0xFF
			);
		}

		/// <summary>Create a range that selects all the tuples of greater length than the specified <paramref name="tuple"/>, and that start with the specified elements: packed(tuple)+'\x00' &lt;= k &lt; packed(tuple)+'\xFF'</summary>
		[Pure]
		public static (Slice Begin, Slice End) ToRange<T1, T2>((T1, T2) tuple)
		{
			Contract.NotNull(tuple);

			// tuple => [ packed."\0", packed."\xFF" )
			var packed = TupleEncoder.Pack(default, in tuple);
			return (
				packed + 0x00,
				packed + 0xFF
			);
		}

		/// <summary>Create a range that selects all the tuples of greater length than the specified items, and that start with the specified elements: packed(tuple)+'\x00' &lt;= k &lt; packed(tuple)+'\xFF'</summary>
		/// <example>ToKeyRange("a", "b") includes all tuples ("a", "b", ...), but not the tuple ("a", "b") itself.</example>
		[Pure]
		public static (Slice Begin, Slice End) ToKeyRange<T1, T2>(T1 item1, T2 item2)
		{
			// tuple => [ packed."\0", packed."\xFF" )
			var packed = TupleEncoder.EncodeKey(default, item1, item2);
			return (
				packed + 0x00,
				packed + 0xFF
			);
		}

		/// <summary>Create a range that selects all the tuples of greater length than the specified items, and that start with the specified elements: packed(tuple)+'\x00' &lt;= k &lt; packed(tuple)+'\xFF'</summary>
		/// <example>ToPrefixedKeyRange(..., "a", "b")) includes all tuples ("a", "b", ...), but not the tuple ("a", "b") itself.</example>
		[Pure]
		public static (Slice Begin, Slice End) ToPrefixedKeyRange<T1, T2>(Slice prefix, T1 item1, T2 item2)
		{
			// tuple => [ prefix.packed."\0", prefix.packed."\xFF" )
			var packed = TupleEncoder.EncodeKey(prefix.Span, item1, item2);
			return (
				packed + 0x00,
				packed + 0xFF
			);
		}

		/// <summary>Create a range that selects all the tuples of greater length than the specified items, and that start with the specified elements: packed(tuple)+'\x00' &lt;= k &lt; packed(tuple)+'\xFF'</summary>
		/// <example>ToPrefixedKeyRange(..., "a", "b")) includes all tuples ("a", "b", ...), but not the tuple ("a", "b") itself.</example>
		[Pure]
		public static (Slice Begin, Slice End) ToPrefixedKeyRange<T1, T2>(ReadOnlySpan<byte> prefix, T1 item1, T2 item2)
		{
			// tuple => [ prefix.packed."\0", prefix.packed."\xFF" )
			var packed = TupleEncoder.EncodeKey(prefix, item1, item2);
			return (
				packed + 0x00,
				packed + 0xFF
			);
		}

		/// <summary>Create a range that selects all the tuples of greater length than the specified <paramref name="tuple"/>, and that start with the specified elements: packed(tuple)+'\x00' &lt;= k &lt; packed(tuple)+'\xFF'</summary>
		[Pure]
		public static (Slice Begin, Slice End) ToRange<T1, T2, T3>(STuple<T1, T2, T3> tuple)
		{
			Contract.NotNull(tuple);

			// tuple => [ packed."\0", packed."\xFF" )
			var packed = TupleEncoder.Pack(default, in tuple);
			return (
				packed + 0x00,
				packed + 0xFF
			);
		}

		/// <summary>Create a range that selects all the tuples of greater length than the specified <paramref name="tuple"/>, and that start with the specified elements: packed(tuple)+'\x00' &lt;= k &lt; packed(tuple)+'\xFF'</summary>
		[Pure]
		public static (Slice Begin, Slice End) ToRange<T1, T2, T3>((T1, T2, T3) tuple)
		{
			// tuple => [ packed."\0", packed."\xFF" )
			var packed = TupleEncoder.Pack(default, in tuple);
			return (
				packed + 0x00,
				packed + 0xFF
			);
		}

		/// <summary>Create a range that selects all the tuples of greater length than the specified items, and that start with the specified elements: packed(tuple)+'\x00' &lt;= k &lt; packed(tuple)+'\xFF'</summary>
		[Pure]
		public static (Slice Begin, Slice End) ToKeyRange<T1, T2, T3>(T1 item1, T2 item2, T3 item3)
		{
			// tuple => [ packed."\0", packed."\xFF" )
			var packed = TupleEncoder.EncodeKey(default, item1, item2, item3);
			return (
				packed + 0x00,
				packed + 0xFF
			);
		}

		/// <summary>Create a range that selects all the tuples of greater length than the specified items, and that start with the specified elements: packed(tuple)+'\x00' &lt;= k &lt; packed(tuple)+'\xFF'</summary>
		[Pure]
		public static (Slice Begin, Slice End) ToPrefixedKeyRange<T1, T2, T3>(Slice prefix, T1 item1, T2 item2, T3 item3)
		{
			// tuple => [ prefix.packed."\0", prefix.packed."\xFF" )
			var packed = TupleEncoder.EncodeKey(prefix.Span, item1, item2, item3);
			return (
				packed + 0x00,
				packed + 0xFF
			);
		}

		/// <summary>Create a range that selects all the tuples of greater length than the specified items, and that start with the specified elements: packed(tuple)+'\x00' &lt;= k &lt; packed(tuple)+'\xFF'</summary>
		[Pure]
		public static (Slice Begin, Slice End) ToPrefixedKeyRange<T1, T2, T3>(ReadOnlySpan<byte> prefix, T1 item1, T2 item2, T3 item3)
		{
			// tuple => [ prefix.packed."\0", prefix.packed."\xFF" )
			var packed = TupleEncoder.EncodeKey(prefix, item1, item2, item3);
			return (
				packed + 0x00,
				packed + 0xFF
			);
		}

		/// <summary>Create a range that selects all the tuples of greater length than the specified <paramref name="tuple"/>, and that start with the specified elements: packed(tuple)+'\x00' &lt;= k &lt; packed(tuple)+'\xFF'</summary>
		[Pure]
		public static (Slice Begin, Slice End) ToRange<T1, T2, T3, T4>(STuple<T1, T2, T3, T4> tuple)
		{
			Contract.NotNull(tuple);

			// tuple => [ packed."\0", packed."\xFF" )
			var packed = TupleEncoder.Pack(default, in tuple);
			return (
				packed + 0x00,
				packed + 0xFF
			);
		}

		/// <summary>Create a range that selects all the tuples of greater length than the specified <paramref name="tuple"/>, and that start with the specified elements: packed(tuple)+'\x00' &lt;= k &lt; packed(tuple)+'\xFF'</summary>
		[Pure]
		public static (Slice Begin, Slice End) ToRange<T1, T2, T3, T4>((T1, T2, T3, T4) tuple)
		{
			// tuple => [ packed."\0", packed."\xFF" )
			var packed = TupleEncoder.Pack(default, in tuple);
			return (
				packed + 0x00,
				packed + 0xFF
			);
		}

		/// <summary>Create a range that selects all the tuples of greater length than the specified items, and that start with the specified elements: packed(tuple)+'\x00' &lt;= k &lt; packed(tuple)+'\xFF'</summary>
		[Pure]
		public static (Slice Begin, Slice End) ToKeyRange<T1, T2, T3, T4>(T1 item1, T2 item2, T3 item3, T4 item4)
		{
			// tuple => [ packed."\0", packed."\xFF" )
			var packed = TupleEncoder.EncodeKey(default, item1, item2, item3, item4);
			return (
				packed + 0x00,
				packed + 0xFF
			);
		}

		/// <summary>Create a range that selects all the tuples of greater length than the specified items, and that start with the specified elements: packed(tuple)+'\x00' &lt;= k &lt; packed(tuple)+'\xFF'</summary>
		[Pure]
		public static (Slice Begin, Slice End) ToPrefixedKeyRange<T1, T2, T3, T4>(Slice prefix, T1 item1, T2 item2, T3 item3, T4 item4)
		{
			// tuple => [ prefix.packed."\0", prefix.packed."\xFF" )
			var packed = TupleEncoder.EncodeKey(prefix.Span, item1, item2, item3, item4);
			return (
				packed + 0x00,
				packed + 0xFF
			);
		}

		/// <summary>Create a range that selects all the tuples of greater length than the specified items, and that start with the specified elements: packed(tuple)+'\x00' &lt;= k &lt; packed(tuple)+'\xFF'</summary>
		[Pure]
		public static (Slice Begin, Slice End) ToPrefixedKeyRange<T1, T2, T3, T4>(ReadOnlySpan<byte> prefix, T1 item1, T2 item2, T3 item3, T4 item4)
		{
			// tuple => [ prefix.packed."\0", prefix.packed."\xFF" )
			var packed = TupleEncoder.EncodeKey(prefix, item1, item2, item3, item4);
			return (
				packed + 0x00,
				packed + 0xFF
			);
		}

		/// <summary>Create a range that selects all the tuples of greater length than the specified <paramref name="tuple"/>, and that start with the specified elements: packed(tuple)+'\x00' &lt;= k &lt; packed(tuple)+'\xFF'</summary>
		[Pure]
		public static (Slice Begin, Slice End) ToRange<T1, T2, T3, T4, T5>(STuple<T1, T2, T3, T4, T5> tuple)
		{
			Contract.NotNull(tuple);

			// tuple => [ packed."\0", packed."\xFF" )
			var packed = TupleEncoder.Pack(default, in tuple);
			return (
				packed + 0x00,
				packed + 0xFF
			);
		}

		/// <summary>Create a range that selects all the tuples of greater length than the specified <paramref name="tuple"/>, and that start with the specified elements: packed(tuple)+'\x00' &lt;= k &lt; packed(tuple)+'\xFF'</summary>
		[Pure]
		public static (Slice Begin, Slice End) ToRange<T1, T2, T3, T4, T5>((T1, T2, T3, T4, T5) tuple)
		{
			// tuple => [ packed."\0", packed."\xFF" )
			var packed = TupleEncoder.Pack(default, in tuple);
			return (
				packed + 0x00,
				packed + 0xFF
			);
		}

		/// <summary>Create a range that selects all the tuples of greater length than the specified items, and that start with the specified elements: packed(tuple)+'\x00' &lt;= k &lt; packed(tuple)+'\xFF'</summary>
		[Pure]
		public static (Slice Begin, Slice End) ToKeyRange<T1, T2, T3, T4, T5>(T1 item1, T2 item2, T3 item3, T4 item4, T5 item5)
		{
			// tuple => [ packed."\0", packed."\xFF" )
			var packed = TupleEncoder.EncodeKey(default, item1, item2, item3, item4, item5);
			return (
				packed + 0x00,
				packed + 0xFF
			);
		}

		/// <summary>Create a range that selects all the tuples of greater length than the specified items, and that start with the specified elements: packed(tuple)+'\x00' &lt;= k &lt; packed(tuple)+'\xFF'</summary>
		[Pure]
		public static (Slice Begin, Slice End) ToPrefixedKeyRange<T1, T2, T3, T4, T5>(Slice prefix, T1 item1, T2 item2, T3 item3, T4 item4, T5 item5)
		{
			// tuple => [ prefix.packed."\0", prefix.packed."\xFF" )
			var packed = TupleEncoder.EncodeKey(prefix.Span, item1, item2, item3, item4, item5);
			return (
				packed + 0x00,
				packed + 0xFF
			);
		}

		/// <summary>Create a range that selects all the tuples of greater length than the specified items, and that start with the specified elements: packed(tuple)+'\x00' &lt;= k &lt; packed(tuple)+'\xFF'</summary>
		[Pure]
		public static (Slice Begin, Slice End) ToPrefixedKeyRange<T1, T2, T3, T4, T5>(ReadOnlySpan<byte> prefix, T1 item1, T2 item2, T3 item3, T4 item4, T5 item5)
		{
			// tuple => [ prefix.packed."\0", prefix.packed."\xFF" )
			var packed = TupleEncoder.EncodeKey(prefix, item1, item2, item3, item4, item5);
			return (
				packed + 0x00,
				packed + 0xFF
			);
		}

		/// <summary>Create a range that selects all the tuples of greater length than the specified <paramref name="tuple"/>, and that start with the specified elements: packed(tuple)+'\x00' &lt;= k &lt; packed(tuple)+'\xFF'</summary>
		[Pure]
		public static (Slice Begin, Slice End) ToRange<T1, T2, T3, T4, T5, T6>(STuple<T1, T2, T3, T4, T5, T6> tuple)
		{
			Contract.NotNull(tuple);

			// tuple => [ packed."\0", packed."\xFF" )
			var packed = TupleEncoder.Pack(default, in tuple);
			return (
				packed + 0x00,
				packed + 0xFF
			);
		}

		/// <summary>Create a range that selects all the tuples of greater length than the specified <paramref name="tuple"/>, and that start with the specified elements: packed(tuple)+'\x00' &lt;= k &lt; packed(tuple)+'\xFF'</summary>
		[Pure]
		public static (Slice Begin, Slice End) ToRange<T1, T2, T3, T4, T5, T6>((T1, T2, T3, T4, T5, T6) tuple)
		{
			// tuple => [ packed."\0", packed."\xFF" )
			var packed = TupleEncoder.Pack(default, in tuple);
			return (
				packed + 0x00,
				packed + 0xFF
			);
		}

		/// <summary>Create a range that selects all the tuples of greater length than the specified items, and that start with the specified elements: packed(tuple)+'\x00' &lt;= k &lt; packed(tuple)+'\xFF'</summary>
		[Pure]
		public static (Slice Begin, Slice End) ToKeyRange<T1, T2, T3, T4, T5, T6>(T1 item1, T2 item2, T3 item3, T4 item4, T5 item5, T6 item6)
		{
			// tuple => [ packed."\0", packed."\xFF" )
			var packed = TupleEncoder.EncodeKey(default, item1, item2, item3, item4, item5, item6);
			return (
				packed + 0x00,
				packed + 0xFF
			);
		}

		/// <summary>Create a range that selects all the tuples of greater length than the specified items, and that start with the specified elements: packed(tuple)+'\x00' &lt;= k &lt; packed(tuple)+'\xFF'</summary>
		[Pure]
		public static (Slice Begin, Slice End) ToPrefixedKeyRange<T1, T2, T3, T4, T5, T6>(Slice prefix, T1 item1, T2 item2, T3 item3, T4 item4, T5 item5, T6 item6)
		{
			// tuple => [ prefix.packed."\0", prefix.packed."\xFF" )
			var packed = TupleEncoder.EncodeKey(prefix.Span, item1, item2, item3, item4, item5, item6);
			return (
				packed + 0x00,
				packed + 0xFF
			);
		}

		/// <summary>Create a range that selects all the tuples of greater length than the specified items, and that start with the specified elements: packed(tuple)+'\x00' &lt;= k &lt; packed(tuple)+'\xFF'</summary>
		[Pure]
		public static (Slice Begin, Slice End) ToPrefixedKeyRange<T1, T2, T3, T4, T5, T6>(ReadOnlySpan<byte> prefix, T1 item1, T2 item2, T3 item3, T4 item4, T5 item5, T6 item6)
		{
			// tuple => [ prefix.packed."\0", prefix.packed."\xFF" )
			var packed = TupleEncoder.EncodeKey(prefix, item1, item2, item3, item4, item5, item6);
			return (
				packed + 0x00,
				packed + 0xFF
			);
		}

		/// <summary>Create a range that selects all the tuples of greater length than the specified <paramref name="tuple"/>, and that start with the specified elements: packed(tuple)+'\x00' &lt;= k &lt; packed(tuple)+'\xFF'</summary>
		[Pure]
		public static (Slice Begin, Slice End) ToRange<T1, T2, T3, T4, T5, T6, T7>(STuple<T1, T2, T3, T4, T5, T6, T7> tuple)
		{
			Contract.NotNull(tuple);

			// tuple => [ packed."\0", packed."\xFF" )
			var packed = TupleEncoder.Pack(default, in tuple);
			return (
				packed + 0x00,
				packed + 0xFF
			);
		}

		/// <summary>Create a range that selects all the tuples of greater length than the specified <paramref name="tuple"/>, and that start with the specified elements: packed(tuple)+'\x00' &lt;= k &lt; packed(tuple)+'\xFF'</summary>
		[Pure]
		public static (Slice Begin, Slice End) ToRange<T1, T2, T3, T4, T5, T6, T7>((T1, T2, T3, T4, T5, T6, T7) tuple)
		{
			// tuple => [ packed."\0", packed."\xFF" )
			var packed = TupleEncoder.Pack(default, in tuple);
			return (
				packed + 0x00,
				packed + 0xFF
			);
		}

		/// <summary>Create a range that selects all the tuples of greater length than the specified items, and that start with the specified elements: packed(tuple)+'\x00' &lt;= k &lt; packed(tuple)+'\xFF'</summary>
		[Pure]
		public static (Slice Begin, Slice End) ToKeyRange<T1, T2, T3, T4, T5, T6, T7>(T1 item1, T2 item2, T3 item3, T4 item4, T5 item5, T6 item6, T7 item7)
		{
			// tuple => [ packed."\0", packed."\xFF" )
			var packed = TupleEncoder.EncodeKey(default, item1, item2, item3, item4, item5, item6, item7);
			return (
				packed + 0x00,
				packed + 0xFF
			);
		}

		/// <summary>Create a range that selects all the tuples of greater length than the specified items, and that start with the specified elements: packed(tuple)+'\x00' &lt;= k &lt; packed(tuple)+'\xFF'</summary>
		[Pure]
		public static (Slice Begin, Slice End) ToPrefixedKeyRange<T1, T2, T3, T4, T5, T6, T7>(Slice prefix, T1 item1, T2 item2, T3 item3, T4 item4, T5 item5, T6 item6, T7 item7)
		{
			// tuple => [ prefix.packed."\0", prefix.packed."\xFF" )
			var packed = TupleEncoder.EncodeKey(prefix.Span, item1, item2, item3, item4, item5, item6, item7);
			return (
				packed + 0x00,
				packed + 0xFF
			);
		}

		/// <summary>Create a range that selects all the tuples of greater length than the specified items, and that start with the specified elements: packed(tuple)+'\x00' &lt;= k &lt; packed(tuple)+'\xFF'</summary>
		[Pure]
		public static (Slice Begin, Slice End) ToPrefixedKeyRange<T1, T2, T3, T4, T5, T6, T7>(ReadOnlySpan<byte> prefix, T1 item1, T2 item2, T3 item3, T4 item4, T5 item5, T6 item6, T7 item7)
		{
			// tuple => [ prefix.packed."\0", prefix.packed."\xFF" )
			var packed = TupleEncoder.EncodeKey(prefix, item1, item2, item3, item4, item5, item6, item7);
			return (
				packed + 0x00,
				packed + 0xFF
			);
		}

		/// <summary>Create a range that selects all the tuples of greater length than the specified <paramref name="tuple"/>, and that start with the specified elements: packed(tuple)+'\x00' &lt;= k &lt; packed(tuple)+'\xFF'</summary>
		[Pure]
		public static (Slice Begin, Slice End) ToRange<T1, T2, T3, T4, T5, T6, T7, T8>((T1, T2, T3, T4, T5, T6, T7, T8) tuple)
		{
			// tuple => [ packed."\0", packed."\xFF" )
			var packed = TupleEncoder.Pack(default, in tuple);
			return (
				packed + 0x00,
				packed + 0xFF
			);
		}

		/// <summary>Create a range that selects all the tuples of greater length than the specified items, and that start with the specified elements: packed(tuple)+'\x00' &lt;= k &lt; packed(tuple)+'\xFF'</summary>
		[Pure]
		public static (Slice Begin, Slice End) ToKeyRange<T1, T2, T3, T4, T5, T6, T7, T8>(T1 item1, T2 item2, T3 item3, T4 item4, T5 item5, T6 item6, T7 item7, T8 item8)
		{
			// tuple => [ packed."\0", packed."\xFF" )
			var packed = TupleEncoder.EncodeKey(default, item1, item2, item3, item4, item5, item6, item7, item8);
			return (
				packed + 0x00,
				packed + 0xFF
			);
		}

		/// <summary>Create a range that selects all the tuples of greater length than the specified items, and that start with the specified elements: packed(tuple)+'\x00' &lt;= k &lt; packed(tuple)+'\xFF'</summary>
		[Pure]
		public static (Slice Begin, Slice End) ToPrefixedKeyRange<T1, T2, T3, T4, T5, T6, T7, T8>(Slice prefix, T1 item1, T2 item2, T3 item3, T4 item4, T5 item5, T6 item6, T7 item7, T8 item8)
		{
			// tuple => [ packed."\0", packed."\xFF" )
			var packed = TupleEncoder.EncodeKey(prefix.Span, item1, item2, item3, item4, item5, item6, item7, item8);
			return (
				packed + 0x00,
				packed + 0xFF
			);
		}

		/// <summary>Create a range that selects all the tuples of greater length than the specified items, and that start with the specified elements: packed(tuple)+'\x00' &lt;= k &lt; packed(tuple)+'\xFF'</summary>
		[Pure]
		public static (Slice Begin, Slice End) ToPrefixedKeyRange<T1, T2, T3, T4, T5, T6, T7, T8>(ReadOnlySpan<byte> prefix, T1 item1, T2 item2, T3 item3, T4 item4, T5 item5, T6 item6, T7 item7, T8 item8)
		{
			// tuple => [ packed."\0", packed."\xFF" )
			var packed = TupleEncoder.EncodeKey(prefix, item1, item2, item3, item4, item5, item6, item7, item8);
			return (
				packed + 0x00,
				packed + 0xFF
			);
		}

		/// <summary>Create a range that selects all the tuples of greater length than the specified <paramref name="tuple"/>, and that start with the specified elements: packed(tuple)+'\x00' &lt;= k &lt; packed(tuple)+'\xFF'</summary>
		/// <example>TuPack.ToRange(Slice.FromInt32(42), STuple.Create("a", "b")) includes all tuples \x2A.("a", "b", ...), but not the tuple \x2A.("a", "b") itself.</example>
		/// <remarks>If <paramref name="prefix"/> is the packed representation of a tuple, then unpacking the resulting key will produce a valid tuple. If not, then the resulting key will need to be truncated first before unpacking.</remarks>
		[Pure]
		public static (Slice Begin, Slice End) ToRange<TTuple>(Slice prefix, TTuple tuple)
			where TTuple : IVarTuple?
		{
			Contract.NotNull(tuple);

			// tuple => [ prefix.packed."\0", prefix.packed."\xFF" )
			var packed = TupleEncoder.Pack(prefix.Span, tuple);
			return (
				packed + 0x00,
				packed + 0xFF
			);
		}

		/// <summary>Create a range that selects all the tuples of greater length than the specified <paramref name="tuple"/>, and that start with the specified elements: packed(tuple)+'\x00' &lt;= k &lt; packed(tuple)+'\xFF'</summary>
		/// <example>TuPack.ToRange(Slice.FromInt32(42), STuple.Create("a", "b")) includes all tuples \x2A.("a", "b", ...), but not the tuple \x2A.("a", "b") itself.</example>
		/// <remarks>If <paramref name="prefix"/> is the packed representation of a tuple, then unpacking the resulting key will produce a valid tuple. If not, then the resulting key will need to be truncated first before unpacking.</remarks>
		[Pure]
		public static (Slice Begin, Slice End) ToRange<TTuple>(ReadOnlySpan<byte> prefix, TTuple tuple)
			where TTuple : IVarTuple?
		{
			Contract.NotNull(tuple);

			// tuple => [ prefix.packed."\0", prefix.packed."\xFF" )
			var packed = TupleEncoder.Pack(prefix, tuple);
			return (
				packed + 0x00,
				packed + 0xFF
			);
		}

		/// <summary>Create a range that selects all the tuples of greater length than the specified <paramref name="tuple"/>, and that start with the specified elements: packed(tuple)+'\x00' &lt;= k &lt; packed(tuple)+'\xFF'</summary>
		/// <example>TuPack.ToRange(STuple.Create("a")) includes all tuples ("a", ...), but not the tuple ("a") itself.</example>
		[Pure]
		public static (Slice Begin, Slice End) ToRange<T1>(Slice prefix, STuple<T1> tuple)
		{
			Contract.NotNull(tuple);

			// tuple => [ packed."\0", packed."\xFF" )
			var packed = TupleEncoder.Pack(prefix.Span, tuple);
			return (
				packed + 0x00,
				packed + 0xFF
			);
		}

		/// <summary>Create a range that selects all the tuples of greater length than the specified <paramref name="tuple"/>, and that start with the specified elements: packed(tuple)+'\x00' &lt;= k &lt; packed(tuple)+'\xFF'</summary>
		/// <example>TuPack.ToRange(STuple.Create("a")) includes all tuples ("a", ...), but not the tuple ("a") itself.</example>
		[Pure]
		public static (Slice Begin, Slice End) ToRange<T1>(ReadOnlySpan<byte> prefix, STuple<T1> tuple)
		{
			Contract.NotNull(tuple);

			// tuple => [ packed."\0", packed."\xFF" )
			var packed = TupleEncoder.Pack(prefix, tuple);
			return (
				packed + 0x00,
				packed + 0xFF
			);
		}

		/// <summary>Create a range that selects all the tuples of greater length than the specified <paramref name="tuple"/>, and that start with the specified elements: packed(tuple)+'\x00' &lt;= k &lt; packed(tuple)+'\xFF'</summary>
		/// <example>TuPack.ToRange(STuple.Create("a", "b")) includes all tuples ("a", "b", ...), but not the tuple ("a", "b") itself.</example>
		[Pure]
		public static (Slice Begin, Slice End) ToRange<T1, T2>(Slice prefix, STuple<T1, T2> tuple)
		{
			Contract.NotNull(tuple);

			// tuple => [ packed."\0", packed."\xFF" )
			var packed = TupleEncoder.Pack(prefix.Span, in tuple);
			return (
				packed + 0x00,
				packed + 0xFF
			);
		}

		/// <summary>Create a range that selects all the tuples of greater length than the specified <paramref name="tuple"/>, and that start with the specified elements: packed(tuple)+'\x00' &lt;= k &lt; packed(tuple)+'\xFF'</summary>
		/// <example>TuPack.ToRange(STuple.Create("a", "b")) includes all tuples ("a", "b", ...), but not the tuple ("a", "b") itself.</example>
		[Pure]
		public static (Slice Begin, Slice End) ToRange<T1, T2>(ReadOnlySpan<byte> prefix, STuple<T1, T2> tuple)
		{
			Contract.NotNull(tuple);

			// tuple => [ packed."\0", packed."\xFF" )
			var packed = TupleEncoder.Pack(prefix, in tuple);
			return (
				packed + 0x00,
				packed + 0xFF
			);
		}

		/// <summary>Create a range that selects all the tuples of greater length than the specified <paramref name="tuple"/>, and that start with the specified elements: packed(tuple)+'\x00' &lt;= k &lt; packed(tuple)+'\xFF'</summary>
		[Pure]
		public static (Slice Begin, Slice End) ToRange<T1, T2, T3>(Slice prefix, STuple<T1, T2, T3> tuple)
		{
			Contract.NotNull(tuple);

			// tuple => [ packed."\0", packed."\xFF" )
			var packed = TupleEncoder.Pack(prefix.Span, in tuple);
			return (
				packed + 0x00,
				packed + 0xFF
			);
		}

		/// <summary>Create a range that selects all the tuples of greater length than the specified <paramref name="tuple"/>, and that start with the specified elements: packed(tuple)+'\x00' &lt;= k &lt; packed(tuple)+'\xFF'</summary>
		[Pure]
		public static (Slice Begin, Slice End) ToRange<T1, T2, T3>(ReadOnlySpan<byte> prefix, STuple<T1, T2, T3> tuple)
		{
			Contract.NotNull(tuple);

			// tuple => [ packed."\0", packed."\xFF" )
			var packed = TupleEncoder.Pack(prefix, in tuple);
			return (
				packed + 0x00,
				packed + 0xFF
			);
		}

		/// <summary>Create a range that selects all the tuples of greater length than the specified <paramref name="tuple"/>, and that start with the specified elements: packed(tuple)+'\x00' &lt;= k &lt; packed(tuple)+'\xFF'</summary>
		[Pure]
		public static (Slice Begin, Slice End) ToRange<T1, T2, T3, T4>(Slice prefix, STuple<T1, T2, T3, T4> tuple)
		{
			Contract.NotNull(tuple);

			// tuple => [ packed."\0", packed."\xFF" )
			var packed = TupleEncoder.Pack(prefix.Span, in tuple);
			return (
				packed + 0x00,
				packed + 0xFF
			);
		}

		/// <summary>Create a range that selects all the tuples of greater length than the specified <paramref name="tuple"/>, and that start with the specified elements: packed(tuple)+'\x00' &lt;= k &lt; packed(tuple)+'\xFF'</summary>
		[Pure]
		public static (Slice Begin, Slice End) ToRange<T1, T2, T3, T4>(ReadOnlySpan<byte> prefix, STuple<T1, T2, T3, T4> tuple)
		{
			Contract.NotNull(tuple);

			// tuple => [ packed."\0", packed."\xFF" )
			var packed = TupleEncoder.Pack(prefix, in tuple);
			return (
				packed + 0x00,
				packed + 0xFF
			);
		}

		/// <summary>Create a range that selects all the tuples of greater length than the specified <paramref name="tuple"/>, and that start with the specified elements: packed(tuple)+'\x00' &lt;= k &lt; packed(tuple)+'\xFF'</summary>
		[Pure]
		public static (Slice Begin, Slice End) ToRange<T1, T2, T3, T4, T5>(Slice prefix, STuple<T1, T2, T3, T4, T5> tuple)
		{
			Contract.NotNull(tuple);

			// tuple => [ packed."\0", packed."\xFF" )
			var packed = TupleEncoder.Pack(prefix.Span, in tuple);
			return (
				packed + 0x00,
				packed + 0xFF
			);
		}

		/// <summary>Create a range that selects all the tuples of greater length than the specified <paramref name="tuple"/>, and that start with the specified elements: packed(tuple)+'\x00' &lt;= k &lt; packed(tuple)+'\xFF'</summary>
		[Pure]
		public static (Slice Begin, Slice End) ToRange<T1, T2, T3, T4, T5>(ReadOnlySpan<byte> prefix, STuple<T1, T2, T3, T4, T5> tuple)
		{
			Contract.NotNull(tuple);

			// tuple => [ packed."\0", packed."\xFF" )
			var packed = TupleEncoder.Pack(prefix, in tuple);
			return (
				packed + 0x00,
				packed + 0xFF
			);
		}

		/// <summary>Create a range that selects all the tuples of greater length than the specified <paramref name="tuple"/>, and that start with the specified elements: packed(tuple)+'\x00' &lt;= k &lt; packed(tuple)+'\xFF'</summary>
		[Pure]
		public static (Slice Begin, Slice End) ToRange<T1, T2, T3, T4, T5, T6>(Slice prefix, STuple<T1, T2, T3, T4, T5, T6> tuple)
		{
			Contract.NotNull(tuple);

			// tuple => [ packed."\0", packed."\xFF" )
			var packed = TupleEncoder.Pack(prefix.Span, in tuple);
			return (
				packed + 0x00,
				packed + 0xFF
			);
		}

		/// <summary>Create a range that selects all the tuples of greater length than the specified <paramref name="tuple"/>, and that start with the specified elements: packed(tuple)+'\x00' &lt;= k &lt; packed(tuple)+'\xFF'</summary>
		[Pure]
		public static (Slice Begin, Slice End) ToRange<T1, T2, T3, T4, T5, T6>(ReadOnlySpan<byte> prefix, STuple<T1, T2, T3, T4, T5, T6> tuple)
		{
			Contract.NotNull(tuple);

			// tuple => [ packed."\0", packed."\xFF" )
			var packed = TupleEncoder.Pack(prefix, in tuple);
			return (
				packed + 0x00,
				packed + 0xFF
			);
		}

		/// <summary>Create a range that selects all the tuples of greater length than the specified <paramref name="tuple"/>, and that start with the specified elements: packed(tuple)+'\x00' &lt;= k &lt; packed(tuple)+'\xFF'</summary>
		[Pure]
		public static (Slice Begin, Slice End) ToRange<T1, T2, T3, T4, T5, T6, T7>(Slice prefix, STuple<T1, T2, T3, T4, T5, T6, T7> tuple)
		{
			Contract.NotNull(tuple);

			// tuple => [ packed."\0", packed."\xFF" )
			var packed = TupleEncoder.Pack(prefix.Span, in tuple);
			return (
				packed + 0x00,
				packed + 0xFF
			);
		}

		/// <summary>Create a range that selects all the tuples of greater length than the specified <paramref name="tuple"/>, and that start with the specified elements: packed(tuple)+'\x00' &lt;= k &lt; packed(tuple)+'\xFF'</summary>
		[Pure]
		public static (Slice Begin, Slice End) ToRange<T1, T2, T3, T4, T5, T6, T7>(ReadOnlySpan<byte> prefix, STuple<T1, T2, T3, T4, T5, T6, T7> tuple)
		{
			Contract.NotNull(tuple);

			// tuple => [ packed."\0", packed."\xFF" )
			var packed = TupleEncoder.Pack(prefix, in tuple);
			return (
				packed + 0x00,
				packed + 0xFF
			);
		}

		#endregion

		#region Unpacking...

		/// <summary>Unpack a tuple from a serialized key blob</summary>
		/// <param name="packedKey">Binary key containing a previously packed tuple</param>
		/// <returns>Unpacked tuple, or the empty tuple if the key is <see cref="Slice.Empty"/></returns>
		/// <exception cref="System.ArgumentNullException">If <paramref name="packedKey"/> is equal to <see cref="Slice.Nil"/></exception>
		[Pure]
		public static IVarTuple Unpack(Slice packedKey)
		{
			if (packedKey.IsNull) throw new ArgumentNullException(nameof(packedKey), "Cannot unpack tuple from Nil");
			if (packedKey.Count == 0) return STuple.Empty;
			return Unpack(packedKey.Span).ToTuple(packedKey);
		}

		/// <summary>Unpack a tuple from a serialized key blob</summary>
		/// <param name="packedKey">Binary key containing a previously packed tuple</param>
		/// <param name="tuple">Unpacked tuple, or the empty tuple if the key is <see cref="Slice.Empty"/></param>
		/// <exception cref="System.ArgumentNullException">If <paramref name="packedKey"/> is equal to <see cref="Slice.Nil"/></exception>
		[Pure]
		public static bool TryUnpack(Slice packedKey, [MaybeNullWhen(false)] out IVarTuple tuple)
		{
			if (!packedKey.IsNull && TryUnpack(packedKey.Span, out var st))
			{
				tuple = st.ToTuple(packedKey);
				return true;
			}
			tuple = null;
			return false;
		}

		/// <summary>Unpack a tuple from a serialized key blob</summary>
		/// <param name="packedKey">Binary key containing a previously packed tuple</param>
		/// <param name="tuple">Unpacked tuple, or the empty tuple if the key is <see cref="Slice.Empty"/></param>
		/// <exception cref="System.ArgumentNullException">If <paramref name="packedKey"/> is equal to <see cref="Slice.Nil"/></exception>
		[Pure]
		public static bool TryUnpack(ReadOnlySpan<byte> packedKey, out SpanTuple tuple)
		{
			if (packedKey.Length == 0)
			{
				tuple = SpanTuple.Empty;
				return true;
			}

			var reader = new TupleReader(packedKey);
			return TuplePackers.TryUnpack(ref reader, out tuple, out _);
		}

		/// <summary>Unpack a tuple from a serialized key blob</summary>
		/// <param name="packedKey">Binary key containing a previously packed tuple</param>
		/// <returns>Unpacked tuple, or the empty tuple if the key is <see cref="Slice.Empty"/></returns>
		/// <exception cref="System.ArgumentNullException">If <paramref name="packedKey"/> is equal to <see cref="Slice.Nil"/></exception>
		[Pure]
		public static SpanTuple Unpack(ReadOnlySpan<byte> packedKey)
		{
			if (packedKey.Length == 0) return SpanTuple.Empty;

			var reader = new TupleReader(packedKey);
			return TuplePackers.Unpack(ref reader);
		}

		/// <summary>Unpack a tuple from a binary representation</summary>
		/// <param name="packedKey">Binary key containing a previously packed tuple, or Slice.Nil</param>
		/// <returns>Unpacked tuple, the empty tuple if <paramref name="packedKey"/> is equal to <see cref="Slice.Empty"/>, or null if the key is <see cref="Slice.Nil"/></returns>
		[Pure]
		public static IVarTuple? UnpackOrDefault(Slice packedKey)
		{
			if (packedKey.IsNull) return null;
			if (packedKey.Count == 0) return STuple.Empty;
			return TuplePackers.Unpack(packedKey, embedded: false);
		}

		#region DecodeFirst...

		/// <summary>Unpack a tuple and only return its first element</summary>
		/// <typeparam name="T1">Type of the first value in the decoded tuple</typeparam>
		/// <param name="packedKey">Slice that should be entirely parsable as a tuple of at least 1 element</param>
		/// <param name="expectedSize">If not <see langword="null"/>, verifies that the tuple has the expected size</param>
		/// <returns>Decoded value of the first item in the tuple</returns>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static T1? DecodeFirst<T1>(Slice packedKey, int? expectedSize = null)
			=> TupleEncoder.DecodeFirst<T1>(packedKey.Span, expectedSize);

		/// <summary>Unpack a tuple and only return its first element</summary>
		/// <typeparam name="T1">Type of the first value in the decoded tuple</typeparam>
		/// <param name="packedKey">Slice that should be entirely parsable as a tuple of at least 1 element</param>
		/// <param name="expectedSize">If not <see langword="null"/>, verifies that the tuple has the expected size</param>
		/// <returns>Decoded value of the first item in the tuple</returns>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static T1? DecodeFirst<T1>(ReadOnlySpan<byte> packedKey, int? expectedSize = null)
			=> TupleEncoder.DecodeFirst<T1>(packedKey, expectedSize);

		/// <summary>Unpack a tuple and only return its first 2 elements</summary>
		/// <typeparam name="T1">Type of the first value in the decoded tuple</typeparam>
		/// <typeparam name="T2">Type of the second value in the decoded tuple</typeparam>
		/// <param name="packedKey">Slice that should be entirely parsable as a tuple of at least 2 elements</param>
		/// <param name="expectedSize">If not <see langword="null"/>, verifies that the tuple has the expected size</param>
		/// <returns>Decoded values of the first two elements in the tuple</returns>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static (T1?, T2?) DecodeFirst<T1, T2>(Slice packedKey, int? expectedSize = null)
			=> TupleEncoder.DecodeFirst<T1, T2>(packedKey.Span, expectedSize);

		/// <summary>Unpack a tuple and only return its first 2 elements</summary>
		/// <typeparam name="T1">Type of the first value in the decoded tuple</typeparam>
		/// <typeparam name="T2">Type of the second value in the decoded tuple</typeparam>
		/// <param name="packedKey">Slice that should be entirely parsable as a tuple of at least 2 elements</param>
		/// <param name="expectedSize">If not <see langword="null"/>, verifies that the tuple has the expected size</param>
		/// <returns>Decoded values of the first two elements in the tuple</returns>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static (T1?, T2?) DecodeFirst<T1, T2>(ReadOnlySpan<byte> packedKey, int? expectedSize = null)
			=> TupleEncoder.DecodeFirst<T1, T2>(packedKey, expectedSize);

		/// <summary>Unpack a tuple and only return its first 3 elements</summary>
		/// <typeparam name="T1">Type of the third value from the end of the decoded tuple</typeparam>
		/// <typeparam name="T2">Type of the second value from the end of the decoded tuple</typeparam>
		/// <typeparam name="T3">Type of the last value in the decoded tuple</typeparam>
		/// <param name="packedKey">Slice that should be entirely parsable as a tuple of at least 3 elements</param>
		/// <param name="expectedSize">If not <see langword="null"/>, verifies that the tuple has the expected size</param>
		/// <returns>Decoded values of the first three elements in the tuple</returns>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static (T1?, T2?, T3?) DecodeFirst<T1, T2, T3>(Slice packedKey, int? expectedSize = null)
			=> TupleEncoder.DecodeFirst<T1, T2, T3>(packedKey.Span, expectedSize);

		/// <summary>Unpack a tuple and only return its first 3 elements</summary>
		/// <typeparam name="T1">Type of the third value from the end of the decoded tuple</typeparam>
		/// <typeparam name="T2">Type of the second value from the end of the decoded tuple</typeparam>
		/// <typeparam name="T3">Type of the third value in the decoded tuple</typeparam>
		/// <param name="packedKey">Slice that should be entirely parsable as a tuple of at least 3 elements</param>
		/// <param name="expectedSize">If not <see langword="null"/>, verifies that the tuple has the expected size</param>
		/// <returns>Decoded values of the first three elements in the tuple</returns>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static (T1?, T2?, T3?) DecodeFirst<T1, T2, T3>(ReadOnlySpan<byte> packedKey, int? expectedSize = null)
			=> TupleEncoder.DecodeFirst<T1, T2, T3>(packedKey, expectedSize);

		/// <summary>Unpack a tuple and only return its first 4 elements</summary>
		/// <typeparam name="T1">Type of the third value from the end of the decoded tuple</typeparam>
		/// <typeparam name="T2">Type of the second value from the end of the decoded tuple</typeparam>
		/// <typeparam name="T3">Type of the third value in the decoded tuple</typeparam>
		/// <typeparam name="T4">Type of the fourth  value in the decoded tuple</typeparam>
		/// <param name="packedKey">Slice that should be entirely parsable as a tuple of at least 4 elements</param>
		/// <param name="expectedSize">If not <see langword="null"/>, verifies that the tuple has the expected size</param>
		/// <returns>Decoded values of the first four elements in the tuple</returns>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static (T1?, T2?, T3?, T4?) DecodeFirst<T1, T2, T3, T4>(Slice packedKey, int? expectedSize = null)
			=> TupleEncoder.DecodeFirst<T1, T2, T3, T4>(packedKey.Span, expectedSize);

		/// <summary>Unpack a tuple and only return its first 4 elements</summary>
		/// <typeparam name="T1">Type of the third value from the end of the decoded tuple</typeparam>
		/// <typeparam name="T2">Type of the second value from the end of the decoded tuple</typeparam>
		/// <typeparam name="T3">Type of the third value in the decoded tuple</typeparam>
		/// <typeparam name="T4">Type of the fourth  value in the decoded tuple</typeparam>
		/// <param name="packedKey">Slice that should be entirely parsable as a tuple of at least 4 elements</param>
		/// <param name="expectedSize">If not <see langword="null"/>, verifies that the tuple has the expected size</param>
		/// <returns>Decoded values of the first four elements in the tuple</returns>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static (T1?, T2?, T3?, T4?) DecodeFirst<T1, T2, T3, T4>(ReadOnlySpan<byte> packedKey, int? expectedSize = null)
			=> TupleEncoder.DecodeFirst<T1, T2, T3, T4>(packedKey, expectedSize);

		#endregion

		#region DecodeLast...

		/// <summary>Unpack a tuple and only return its last element</summary>
		/// <typeparam name="T1">Type of the last value in the decoded tuple</typeparam>
		/// <param name="packedKey">Slice that should be entirely parsable as a tuple of at least one element</param>
		/// <param name="expectedSize">If not <see langword="null"/>, verifies that the tuple has the expected size</param>
		/// <returns>Decoded value of the last item in the tuple</returns>
		/// <exception cref="InvalidOperationException">If the decoded tuple does not have the expected size</exception>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static T1? DecodeLast<T1>(Slice packedKey, int? expectedSize = null)
			=> TupleEncoder.DecodeLast<T1>(packedKey.Span, expectedSize);

		/// <summary>Unpack a tuple and only return its last element</summary>
		/// <typeparam name="T1">Type of the last value in the decoded tuple</typeparam>
		/// <param name="packedKey">Slice that should be entirely parsable as a tuple of at least one element</param>
		/// <param name="expectedSize">If not <see langword="null"/>, verifies that the tuple has the expected size</param>
		/// <returns>Decoded value of the last item in the tuple</returns>
		/// <exception cref="InvalidOperationException">If the decoded tuple does not have the expected size</exception>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static T1? DecodeLast<T1>(ReadOnlySpan<byte> packedKey, int? expectedSize = null)
			=> TupleEncoder.DecodeLast<T1>(packedKey, expectedSize);

		/// <summary>Unpack a tuple and only return its last 2 elements</summary>
		/// <typeparam name="T1">Type of the next to last value in the decoded tuple</typeparam>
		/// <typeparam name="T2">Type of the last value in the decoded tuple</typeparam>
		/// <param name="packedKey">Slice that should be entirely parsable as a tuple of at least 2 elements</param>
		/// <param name="expectedSize">If not <see langword="null"/>, verifies that the tuple has the expected size</param>
		/// <returns>Decoded values of the last two elements in the tuple</returns>
		/// <exception cref="InvalidOperationException">If the decoded tuple does not have the expected size</exception>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static (T1?, T2?) DecodeLast<T1, T2>(Slice packedKey, int? expectedSize = null)
			=> TupleEncoder.DecodeLast<T1, T2>(packedKey.Span, expectedSize);

		/// <summary>Unpack a tuple and only return its last 2 elements</summary>
		/// <typeparam name="T1">Type of the next to last value in the decoded tuple</typeparam>
		/// <typeparam name="T2">Type of the last value in the decoded tuple</typeparam>
		/// <param name="packedKey">Slice that should be entirely parsable as a tuple of at least 2 elements</param>
		/// <param name="expectedSize">If not <see langword="null"/>, verifies that the tuple has the expected size</param>
		/// <returns>Decoded values of the last two elements in the tuple</returns>
		/// <exception cref="InvalidOperationException">If the decoded tuple does not have the expected size</exception>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static (T1?, T2?) DecodeLast<T1, T2>(ReadOnlySpan<byte> packedKey, int? expectedSize = null)
			=> TupleEncoder.DecodeLast<T1, T2>(packedKey, expectedSize);

		/// <summary>Unpack a tuple and only return its last 3 elements</summary>
		/// <typeparam name="T1">Type of the third value from the end of the decoded tuple</typeparam>
		/// <typeparam name="T2">Type of the second value from the end of the decoded tuple</typeparam>
		/// <typeparam name="T3">Type of the last value in the decoded tuple</typeparam>
		/// <param name="packedKey">Slice that should be entirely parsable as a tuple of at least 3 elements</param>
		/// <param name="expectedSize">If not <see langword="null"/>, verifies that the tuple has the expected size</param>
		/// <returns>Decoded values of the last three elements in the tuple</returns>
		/// <exception cref="InvalidOperationException">If the decoded tuple does not have the expected size</exception>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static (T1?, T2?, T3?) DecodeLast<T1, T2, T3>(Slice packedKey, int? expectedSize = null)
			=> TupleEncoder.DecodeLast<T1, T2, T3>(packedKey.Span, expectedSize);

		/// <summary>Unpack a tuple and only return its last 3 elements</summary>
		/// <typeparam name="T1">Type of the third value from the end of the decoded tuple</typeparam>
		/// <typeparam name="T2">Type of the second value from the end of the decoded tuple</typeparam>
		/// <typeparam name="T3">Type of the last value in the decoded tuple</typeparam>
		/// <param name="packedKey">Slice that should be entirely parsable as a tuple of at least 3 elements</param>
		/// <param name="expectedSize">If not <see langword="null"/>, verifies that the tuple has the expected size</param>
		/// <returns>Decoded values of the last three elements in the tuple</returns>
		/// <exception cref="InvalidOperationException">If the decoded tuple does not have the expected size</exception>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static (T1?, T2?, T3?) DecodeLast<T1, T2, T3>(ReadOnlySpan<byte> packedKey, int? expectedSize = null)
			=> TupleEncoder.DecodeLast<T1, T2, T3>(packedKey, expectedSize);

		/// <summary>Unpack a tuple and only return its last 4 elements</summary>
		/// <typeparam name="T1">Type of the fourth value from the end of the decoded tuple</typeparam>
		/// <typeparam name="T2">Type of the third value from the end of the decoded tuple</typeparam>
		/// <typeparam name="T3">Type of the second value from the end of the decoded tuple</typeparam>
		/// <typeparam name="T4">Type of the last value in the decoded tuple</typeparam>
		/// <param name="packedKey">Slice that should be entirely parsable as a tuple of at least 4 elements</param>
		/// <param name="expectedSize">If not <see langword="null"/>, verifies that the tuple has the expected size</param>
		/// <returns>Decoded values of the last four elements in the tuple</returns>
		/// <exception cref="InvalidOperationException">If the decoded tuple does not have the expected size</exception>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static (T1?, T2?, T3?, T4?) DecodeLast<T1, T2, T3, T4>(Slice packedKey, int? expectedSize = null)
			=> TupleEncoder.DecodeLast<T1, T2, T3, T4>(packedKey.Span, expectedSize);

		/// <summary>Unpack a tuple and only return its last 4 elements</summary>
		/// <typeparam name="T1">Type of the fourth value from the end of the decoded tuple</typeparam>
		/// <typeparam name="T2">Type of the third value from the end of the decoded tuple</typeparam>
		/// <typeparam name="T3">Type of the second value from the end of the decoded tuple</typeparam>
		/// <typeparam name="T4">Type of the last value in the decoded tuple</typeparam>
		/// <param name="packedKey">Slice that should be entirely parsable as a tuple of at least 4 elements</param>
		/// <param name="expectedSize">If not <see langword="null"/>, verifies that the tuple has the expected size</param>
		/// <returns>Decoded values of the last four elements in the tuple</returns>
		/// <exception cref="InvalidOperationException">If the decoded tuple does not have the expected size</exception>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static (T1?, T2?, T3?, T4?) DecodeLast<T1, T2, T3, T4>(ReadOnlySpan<byte> packedKey, int? expectedSize = null)
			=> TupleEncoder.DecodeLast<T1, T2, T3, T4>(packedKey, expectedSize);

		#endregion

		#region TryDecodeLast...

		/// <summary>Unpack a tuple and only return the last element</summary>
		/// <typeparam name="T1">Type of the last value of the decoded tuple</typeparam>
		/// <param name="packedKey">Slice that should be entirely parsable as a tuple of at least 2 elements</param>
		/// <param name="item1">Last decoded element</param>
		/// <returns><see langword="true"/> if the tuple was decoded successfuly, or <see langword="false"/> if the tuple is too small, or if there was an error while decoding</returns>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool TryDecodeFirst<T1>(Slice packedKey, out T1? item1)
			=> TupleEncoder.TryDecodeFirst(packedKey.Span, null, out item1);

		/// <summary>Unpack a tuple and only return the last element</summary>
		/// <typeparam name="T1">Type of the last value of the decoded tuple</typeparam>
		/// <param name="packedKey">Slice that should be entirely parsable as a tuple of at least 2 elements</param>
		/// <param name="item1">Last decoded element</param>
		/// <returns><see langword="true"/> if the tuple was decoded successfuly, or <see langword="false"/> if the tuple is too small, or if there was an error while decoding</returns>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool TryDecodeFirst<T1>(ReadOnlySpan<byte> packedKey, out T1? item1)
			=> TupleEncoder.TryDecodeFirst(packedKey, null, out item1);

		/// <summary>Unpack a tuple and only return the last element</summary>
		/// <typeparam name="T1">Type of the last value of the decoded tuple</typeparam>
		/// <param name="packedKey">Slice that should be entirely parsable as a tuple of at least 2 elements</param>
		/// <param name="expectedSize">Expected size of the decoded tuple</param>
		/// <param name="item1">Last decoded element</param>
		/// <returns><see langword="true"/> if the tuple was decoded successfuly, or <see langword="false"/> if the tuple is too small, or if there was an error while decoding</returns>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool TryDecodeFirst<T1>(Slice packedKey, int expectedSize, out T1? item1)
			=> TupleEncoder.TryDecodeFirst(packedKey.Span, expectedSize, out item1);

		/// <summary>Unpack a tuple and only return the last element</summary>
		/// <typeparam name="T1">Type of the last value of the decoded tuple</typeparam>
		/// <param name="packedKey">Slice that should be entirely parsable as a tuple of at least 2 elements</param>
		/// <param name="expectedSize">Expected size of the decoded tuple</param>
		/// <param name="item1">Last decoded element</param>
		/// <returns><see langword="true"/> if the tuple was decoded successfuly, or <see langword="false"/> if the tuple is too small, or if there was an error while decoding</returns>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool TryDecodeFirst<T1>(ReadOnlySpan<byte> packedKey, int expectedSize, out T1? item1)
			=> TupleEncoder.TryDecodeFirst(packedKey, expectedSize, out item1);

		/// <summary>Unpack a tuple and only return its last 2 elements</summary>
		/// <typeparam name="T1">Type of the second value from the end of the decoded tuple</typeparam>
		/// <typeparam name="T2">Type of the last value in the decoded tuple</typeparam>
		/// <param name="packedKey">Slice that should be entirely parsable as a tuple of at least 2 elements</param>
		/// <param name="item1">First decoded element</param>
		/// <param name="item2">Second decoded element</param>
		/// <returns><see langword="true"/> if the tuple was decoded successfuly, or <see langword="false"/> if the tuple is too small, or if there was an error while decoding</returns>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool TryDecodeFirst<T1, T2>(Slice packedKey, out T1? item1, out T2? item2)
			=> TupleEncoder.TryDecodeFirst(packedKey.Span, null, out item1, out item2);

		/// <summary>Unpack a tuple and only return its last 2 elements</summary>
		/// <typeparam name="T1">Type of the second value from the end of the decoded tuple</typeparam>
		/// <typeparam name="T2">Type of the last value in the decoded tuple</typeparam>
		/// <param name="packedKey">Slice that should be entirely parsable as a tuple of at least 2 elements</param>
		/// <param name="item1">First decoded element</param>
		/// <param name="item2">Second decoded element</param>
		/// <returns><see langword="true"/> if the tuple was decoded successfuly, or <see langword="false"/> if the tuple is too small, or if there was an error while decoding</returns>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool TryDecodeFirst<T1, T2>(ReadOnlySpan<byte> packedKey, out T1? item1, out T2? item2)
			=> TupleEncoder.TryDecodeFirst(packedKey, null, out item1, out item2);

		/// <summary>Unpack a tuple and only return its last 2 elements</summary>
		/// <typeparam name="T1">Type of the second value from the end of the decoded tuple</typeparam>
		/// <typeparam name="T2">Type of the last value in the decoded tuple</typeparam>
		/// <param name="packedKey">Slice that should be entirely parsable as a tuple of at least 2 elements</param>
		/// <param name="item1">First decoded element</param>
		/// <param name="item2">Second decoded element</param>
		/// <param name="expectedSize">Expected size of the decoded tuple</param>
		/// <returns><see langword="true"/> if the tuple was decoded successfuly, or <see langword="false"/> if the tuple is too small, or if there was an error while decoding</returns>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool TryDecodeFirst<T1, T2>(Slice packedKey, int expectedSize, out T1? item1, out T2? item2)
			=> TupleEncoder.TryDecodeFirst(packedKey.Span, expectedSize, out item1, out item2);

		/// <summary>Unpack a tuple and only return its last 2 elements</summary>
		/// <typeparam name="T1">Type of the second value from the end of the decoded tuple</typeparam>
		/// <typeparam name="T2">Type of the last value in the decoded tuple</typeparam>
		/// <param name="packedKey">Slice that should be entirely parsable as a tuple of at least 2 elements</param>
		/// <param name="item1">First decoded element</param>
		/// <param name="item2">Second decoded element</param>
		/// <param name="expectedSize">Expected size of the decoded tuple</param>
		/// <returns><see langword="true"/> if the tuple was decoded successfuly, or <see langword="false"/> if the tuple is too small, or if there was an error while decoding</returns>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool TryDecodeFirst<T1, T2>(ReadOnlySpan<byte> packedKey, int expectedSize, out T1? item1, out T2? item2)
			=> TupleEncoder.TryDecodeFirst(packedKey, expectedSize, out item1, out item2);

		/// <summary>Unpack a tuple and only return its last 3 elements</summary>
		/// <typeparam name="T1">Type of the third value from the end of the decoded tuple</typeparam>
		/// <typeparam name="T2">Type of the second value from the end of the decoded tuple</typeparam>
		/// <typeparam name="T3">Type of the last value in the decoded tuple</typeparam>
		/// <param name="packedKey">Slice that should be entirely parsable as a tuple of at least 3 elements</param>
		/// <param name="item1">First decoded element</param>
		/// <param name="item2">Second decoded element</param>
		/// <param name="item3">Third decoded element</param>
		/// <returns><see langword="true"/> if the tuple was decoded successfuly, or <see langword="false"/> if the tuple is too small, or if there was an error while decoding</returns>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool TryDecodeFirst<T1, T2, T3>(Slice packedKey, out T1? item1, out T2? item2, out T3? item3)
			=> TupleEncoder.TryDecodeFirst(packedKey.Span, null, out item1, out item2, out item3);

		/// <summary>Unpack a tuple and only return its last 3 elements</summary>
		/// <typeparam name="T1">Type of the third value from the end of the decoded tuple</typeparam>
		/// <typeparam name="T2">Type of the second value from the end of the decoded tuple</typeparam>
		/// <typeparam name="T3">Type of the last value in the decoded tuple</typeparam>
		/// <param name="packedKey">Slice that should be entirely parsable as a tuple of at least 3 elements</param>
		/// <param name="item1">First decoded element</param>
		/// <param name="item2">Second decoded element</param>
		/// <param name="item3">Third decoded element</param>
		/// <returns><see langword="true"/> if the tuple was decoded successfuly, or <see langword="false"/> if the tuple is too small, or if there was an error while decoding</returns>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool TryDecodeFirst<T1, T2, T3>(ReadOnlySpan<byte> packedKey, out T1? item1, out T2? item2, out T3? item3)
			=> TupleEncoder.TryDecodeFirst(packedKey, null, out item1, out item2, out item3);

		/// <summary>Unpack a tuple and only return its last 3 elements</summary>
		/// <typeparam name="T1">Type of the third value from the end of the decoded tuple</typeparam>
		/// <typeparam name="T2">Type of the second value from the end of the decoded tuple</typeparam>
		/// <typeparam name="T3">Type of the last value in the decoded tuple</typeparam>
		/// <param name="packedKey">Slice that should be entirely parsable as a tuple of at least 3 elements</param>
		/// <param name="item1">First decoded element</param>
		/// <param name="item2">Second decoded element</param>
		/// <param name="item3">Third decoded element</param>
		/// <param name="expectedSize">Expected size of the decoded tuple</param>
		/// <returns><see langword="true"/> if the tuple was decoded successfuly, or <see langword="false"/> if the tuple is too small, or if there was an error while decoding</returns>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool TryDecodeFirst<T1, T2, T3>(Slice packedKey, int expectedSize, out T1? item1, out T2? item2, out T3? item3)
			=> TupleEncoder.TryDecodeFirst(packedKey.Span, expectedSize, out item1, out item2, out item3);

		/// <summary>Unpack a tuple and only return its last 3 elements</summary>
		/// <typeparam name="T1">Type of the third value from the end of the decoded tuple</typeparam>
		/// <typeparam name="T2">Type of the second value from the end of the decoded tuple</typeparam>
		/// <typeparam name="T3">Type of the last value in the decoded tuple</typeparam>
		/// <param name="packedKey">Slice that should be entirely parsable as a tuple of at least 3 elements</param>
		/// <param name="item1">First decoded element</param>
		/// <param name="item2">Second decoded element</param>
		/// <param name="item3">Third decoded element</param>
		/// <param name="expectedSize">Expected size of the decoded tuple</param>
		/// <returns><see langword="true"/> if the tuple was decoded successfuly, or <see langword="false"/> if the tuple is too small, or if there was an error while decoding</returns>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool TryDecodeFirst<T1, T2, T3>(ReadOnlySpan<byte> packedKey, int expectedSize, out T1? item1, out T2? item2, out T3? item3)
			=> TupleEncoder.TryDecodeFirst(packedKey, expectedSize, out item1, out item2, out item3);

		/// <summary>Unpack a tuple and only return its last 4 elements</summary>
		/// <typeparam name="T1">Type of the fourth value from the end of the decoded tuple</typeparam>
		/// <typeparam name="T2">Type of the third value from the end of the decoded tuple</typeparam>
		/// <typeparam name="T3">Type of the second value from the end of the decoded tuple</typeparam>
		/// <typeparam name="T4">Type of the last value in the decoded tuple</typeparam>
		/// <param name="packedKey">Slice that should be entirely parsable as a tuple of at least 4 elements</param>
		/// <param name="item1">First decoded element</param>
		/// <param name="item2">Second decoded element</param>
		/// <param name="item3">Third decoded element</param>
		/// <param name="item4">Fourth decoded element</param>
		/// <returns><see langword="true"/> if the tuple was decoded successfuly, or <see langword="false"/> if the tuple is too small, or if there was an error while decoding</returns>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool TryDecodeFirst<T1, T2, T3, T4>(Slice packedKey, out T1? item1, out T2? item2, out T3? item3, out T4? item4)
			=> TupleEncoder.TryDecodeFirst(packedKey.Span, null, out item1, out item2, out item3, out item4);

		/// <summary>Unpack a tuple and only return its last 4 elements</summary>
		/// <typeparam name="T1">Type of the fourth value from the end of the decoded tuple</typeparam>
		/// <typeparam name="T2">Type of the third value from the end of the decoded tuple</typeparam>
		/// <typeparam name="T3">Type of the second value from the end of the decoded tuple</typeparam>
		/// <typeparam name="T4">Type of the last value in the decoded tuple</typeparam>
		/// <param name="packedKey">Slice that should be entirely parsable as a tuple of at least 4 elements</param>
		/// <param name="item1">First decoded element</param>
		/// <param name="item2">Second decoded element</param>
		/// <param name="item3">Third decoded element</param>
		/// <param name="item4">Fourth decoded element</param>
		/// <returns><see langword="true"/> if the tuple was decoded successfuly, or <see langword="false"/> if the tuple is too small, or if there was an error while decoding</returns>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool TryDecodeFirst<T1, T2, T3, T4>(ReadOnlySpan<byte> packedKey, out T1? item1, out T2? item2, out T3? item3, out T4? item4)
			=> TupleEncoder.TryDecodeFirst(packedKey, null, out item1, out item2, out item3, out item4);

		/// <summary>Unpack a tuple and only return its last 4 elements</summary>
		/// <typeparam name="T1">Type of the fourth value from the end of the decoded tuple</typeparam>
		/// <typeparam name="T2">Type of the third value from the end of the decoded tuple</typeparam>
		/// <typeparam name="T3">Type of the second value from the end of the decoded tuple</typeparam>
		/// <typeparam name="T4">Type of the last value in the decoded tuple</typeparam>
		/// <param name="packedKey">Slice that should be entirely parsable as a tuple of at least 4 elements</param>
		/// <param name="item1">First decoded element</param>
		/// <param name="item2">Second decoded element</param>
		/// <param name="item3">Third decoded element</param>
		/// <param name="item4">Fourth decoded element</param>
		/// <param name="expectedSize">Expected size of the decoded tuple</param>
		/// <returns><see langword="true"/> if the tuple was decoded successfuly, or <see langword="false"/> if the tuple is too small, or if there was an error while decoding</returns>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool TryDecodeFirst<T1, T2, T3, T4>(Slice packedKey, int expectedSize, out T1? item1, out T2? item2, out T3? item3, out T4? item4)
			=> TupleEncoder.TryDecodeFirst(packedKey.Span, expectedSize, out item1, out item2, out item3, out item4);

		/// <summary>Unpack a tuple and only return its last 4 elements</summary>
		/// <typeparam name="T1">Type of the fourth value from the end of the decoded tuple</typeparam>
		/// <typeparam name="T2">Type of the third value from the end of the decoded tuple</typeparam>
		/// <typeparam name="T3">Type of the second value from the end of the decoded tuple</typeparam>
		/// <typeparam name="T4">Type of the last value in the decoded tuple</typeparam>
		/// <param name="packedKey">Slice that should be entirely parsable as a tuple of at least 4 elements</param>
		/// <param name="item1">First decoded element</param>
		/// <param name="item2">Second decoded element</param>
		/// <param name="item3">Third decoded element</param>
		/// <param name="item4">Fourth decoded element</param>
		/// <param name="expectedSize">Expected size of the decoded tuple</param>
		/// <returns><see langword="true"/> if the tuple was decoded successfuly, or <see langword="false"/> if the tuple is too small, or if there was an error while decoding</returns>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool TryDecodeFirst<T1, T2, T3, T4>(ReadOnlySpan<byte> packedKey, int expectedSize, out T1? item1, out T2? item2, out T3? item3, out T4? item4)
			=> TupleEncoder.TryDecodeFirst(packedKey, expectedSize, out item1, out item2, out item3, out item4);

		#endregion

		#region TryDecodeLast...

		/// <summary>Unpack a tuple and only return the last element</summary>
		/// <typeparam name="T1">Type of the last value of the decoded tuple</typeparam>
		/// <param name="packedKey">Slice that should be entirely parsable as a tuple of at least 2 elements</param>
		/// <param name="item1">Last decoded element</param>
		/// <returns><see langword="true"/> if the tuple was decoded successfuly, or <see langword="false"/> if the tuple is too small, or if there was an error while decoding</returns>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool TryDecodeLast<T1>(Slice packedKey, out T1? item1)
			=> TupleEncoder.TryDecodeLast(packedKey.Span, null, out item1);

		/// <summary>Unpack a tuple and only return the last element</summary>
		/// <typeparam name="T1">Type of the last value of the decoded tuple</typeparam>
		/// <param name="packedKey">Slice that should be entirely parsable as a tuple of at least 2 elements</param>
		/// <param name="item1">Last decoded element</param>
		/// <returns><see langword="true"/> if the tuple was decoded successfuly, or <see langword="false"/> if the tuple is too small, or if there was an error while decoding</returns>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool TryDecodeLast<T1>(ReadOnlySpan<byte> packedKey, out T1? item1)
			=> TupleEncoder.TryDecodeLast(packedKey, null, out item1);

		/// <summary>Unpack a tuple and only return the last element</summary>
		/// <typeparam name="T1">Type of the last value of the decoded tuple</typeparam>
		/// <param name="packedKey">Slice that should be entirely parsable as a tuple of at least 2 elements</param>
		/// <param name="expectedSize">Expected size of the decoded tuple</param>
		/// <param name="item1">Last decoded element</param>
		/// <returns><see langword="true"/> if the tuple was decoded successfuly, or <see langword="false"/> if the tuple is too small, or if there was an error while decoding</returns>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool TryDecodeLast<T1>(Slice packedKey, int expectedSize, out T1? item1)
			=> TupleEncoder.TryDecodeLast(packedKey.Span, expectedSize, out item1);

		/// <summary>Unpack a tuple and only return the last element</summary>
		/// <typeparam name="T1">Type of the last value of the decoded tuple</typeparam>
		/// <param name="packedKey">Slice that should be entirely parsable as a tuple of at least 2 elements</param>
		/// <param name="expectedSize">Expected size of the decoded tuple</param>
		/// <param name="item1">Last decoded element</param>
		/// <returns><see langword="true"/> if the tuple was decoded successfuly, or <see langword="false"/> if the tuple is too small, or if there was an error while decoding</returns>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool TryDecodeLast<T1>(ReadOnlySpan<byte> packedKey, int expectedSize, out T1? item1)
			=> TupleEncoder.TryDecodeLast(packedKey, expectedSize, out item1);

		/// <summary>Unpack a tuple and only return its last 2 elements</summary>
		/// <typeparam name="T1">Type of the second value from the end of the decoded tuple</typeparam>
		/// <typeparam name="T2">Type of the last value in the decoded tuple</typeparam>
		/// <param name="packedKey">Slice that should be entirely parsable as a tuple of at least 2 elements</param>
		/// <param name="item1">First decoded element</param>
		/// <param name="item2">Second decoded element</param>
		/// <returns><see langword="true"/> if the tuple was decoded successfuly, or <see langword="false"/> if the tuple is too small, or if there was an error while decoding</returns>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool TryDecodeLast<T1, T2>(Slice packedKey, out T1? item1, out T2? item2)
			=> TupleEncoder.TryDecodeLast(packedKey.Span, null, out item1, out item2);

		/// <summary>Unpack a tuple and only return its last 2 elements</summary>
		/// <typeparam name="T1">Type of the second value from the end of the decoded tuple</typeparam>
		/// <typeparam name="T2">Type of the last value in the decoded tuple</typeparam>
		/// <param name="packedKey">Slice that should be entirely parsable as a tuple of at least 2 elements</param>
		/// <param name="item1">First decoded element</param>
		/// <param name="item2">Second decoded element</param>
		/// <returns><see langword="true"/> if the tuple was decoded successfuly, or <see langword="false"/> if the tuple is too small, or if there was an error while decoding</returns>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool TryDecodeLast<T1, T2>(ReadOnlySpan<byte> packedKey, out T1? item1, out T2? item2)
			=> TupleEncoder.TryDecodeLast(packedKey, null, out item1, out item2);

		/// <summary>Unpack a tuple and only return its last 2 elements</summary>
		/// <typeparam name="T1">Type of the second value from the end of the decoded tuple</typeparam>
		/// <typeparam name="T2">Type of the last value in the decoded tuple</typeparam>
		/// <param name="packedKey">Slice that should be entirely parsable as a tuple of at least 2 elements</param>
		/// <param name="item1">First decoded element</param>
		/// <param name="item2">Second decoded element</param>
		/// <param name="expectedSize">Expected size of the decoded tuple</param>
		/// <returns><see langword="true"/> if the tuple was decoded successfuly, or <see langword="false"/> if the tuple is too small, or if there was an error while decoding</returns>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool TryDecodeLast<T1, T2>(Slice packedKey, int expectedSize, out T1? item1, out T2? item2)
			=> TupleEncoder.TryDecodeLast(packedKey.Span, expectedSize, out item1, out item2);

		/// <summary>Unpack a tuple and only return its last 2 elements</summary>
		/// <typeparam name="T1">Type of the second value from the end of the decoded tuple</typeparam>
		/// <typeparam name="T2">Type of the last value in the decoded tuple</typeparam>
		/// <param name="packedKey">Slice that should be entirely parsable as a tuple of at least 2 elements</param>
		/// <param name="item1">First decoded element</param>
		/// <param name="item2">Second decoded element</param>
		/// <param name="expectedSize">Expected size of the decoded tuple</param>
		/// <returns><see langword="true"/> if the tuple was decoded successfuly, or <see langword="false"/> if the tuple is too small, or if there was an error while decoding</returns>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool TryDecodeLast<T1, T2>(ReadOnlySpan<byte> packedKey, int expectedSize, out T1? item1, out T2? item2)
			=> TupleEncoder.TryDecodeLast(packedKey, expectedSize, out item1, out item2);

		/// <summary>Unpack a tuple and only return its last 3 elements</summary>
		/// <typeparam name="T1">Type of the third value from the end of the decoded tuple</typeparam>
		/// <typeparam name="T2">Type of the second value from the end of the decoded tuple</typeparam>
		/// <typeparam name="T3">Type of the last value in the decoded tuple</typeparam>
		/// <param name="packedKey">Slice that should be entirely parsable as a tuple of at least 3 elements</param>
		/// <param name="item1">First decoded element</param>
		/// <param name="item2">Second decoded element</param>
		/// <param name="item3">Third decoded element</param>
		/// <returns><see langword="true"/> if the tuple was decoded successfuly, or <see langword="false"/> if the tuple is too small, or if there was an error while decoding</returns>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool TryDecodeLast<T1, T2, T3>(Slice packedKey, out T1? item1, out T2? item2, out T3? item3)
			=> TupleEncoder.TryDecodeLast(packedKey.Span, null, out item1, out item2, out item3);

		/// <summary>Unpack a tuple and only return its last 3 elements</summary>
		/// <typeparam name="T1">Type of the third value from the end of the decoded tuple</typeparam>
		/// <typeparam name="T2">Type of the second value from the end of the decoded tuple</typeparam>
		/// <typeparam name="T3">Type of the last value in the decoded tuple</typeparam>
		/// <param name="packedKey">Slice that should be entirely parsable as a tuple of at least 3 elements</param>
		/// <param name="item1">First decoded element</param>
		/// <param name="item2">Second decoded element</param>
		/// <param name="item3">Third decoded element</param>
		/// <returns><see langword="true"/> if the tuple was decoded successfuly, or <see langword="false"/> if the tuple is too small, or if there was an error while decoding</returns>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool TryDecodeLast<T1, T2, T3>(ReadOnlySpan<byte> packedKey, out T1? item1, out T2? item2, out T3? item3)
			=> TupleEncoder.TryDecodeLast(packedKey, null, out item1, out item2, out item3);

		/// <summary>Unpack a tuple and only return its last 3 elements</summary>
		/// <typeparam name="T1">Type of the third value from the end of the decoded tuple</typeparam>
		/// <typeparam name="T2">Type of the second value from the end of the decoded tuple</typeparam>
		/// <typeparam name="T3">Type of the last value in the decoded tuple</typeparam>
		/// <param name="packedKey">Slice that should be entirely parsable as a tuple of at least 3 elements</param>
		/// <param name="item1">First decoded element</param>
		/// <param name="item2">Second decoded element</param>
		/// <param name="item3">Third decoded element</param>
		/// <param name="expectedSize">Expected size of the decoded tuple</param>
		/// <returns><see langword="true"/> if the tuple was decoded successfuly, or <see langword="false"/> if the tuple is too small, or if there was an error while decoding</returns>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool TryDecodeLast<T1, T2, T3>(Slice packedKey, int expectedSize, out T1? item1, out T2? item2, out T3? item3)
			=> TupleEncoder.TryDecodeLast(packedKey.Span, expectedSize, out item1, out item2, out item3);

		/// <summary>Unpack a tuple and only return its last 3 elements</summary>
		/// <typeparam name="T1">Type of the third value from the end of the decoded tuple</typeparam>
		/// <typeparam name="T2">Type of the second value from the end of the decoded tuple</typeparam>
		/// <typeparam name="T3">Type of the last value in the decoded tuple</typeparam>
		/// <param name="packedKey">Slice that should be entirely parsable as a tuple of at least 3 elements</param>
		/// <param name="item1">First decoded element</param>
		/// <param name="item2">Second decoded element</param>
		/// <param name="item3">Third decoded element</param>
		/// <param name="expectedSize">Expected size of the decoded tuple</param>
		/// <returns><see langword="true"/> if the tuple was decoded successfuly, or <see langword="false"/> if the tuple is too small, or if there was an error while decoding</returns>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool TryDecodeLast<T1, T2, T3>(ReadOnlySpan<byte> packedKey, int expectedSize, out T1? item1, out T2? item2, out T3? item3)
			=> TupleEncoder.TryDecodeLast(packedKey, expectedSize, out item1, out item2, out item3);

		/// <summary>Unpack a tuple and only return its last 4 elements</summary>
		/// <typeparam name="T1">Type of the fourth value from the end of the decoded tuple</typeparam>
		/// <typeparam name="T2">Type of the third value from the end of the decoded tuple</typeparam>
		/// <typeparam name="T3">Type of the second value from the end of the decoded tuple</typeparam>
		/// <typeparam name="T4">Type of the last value in the decoded tuple</typeparam>
		/// <param name="packedKey">Slice that should be entirely parsable as a tuple of at least 4 elements</param>
		/// <param name="item1">First decoded element</param>
		/// <param name="item2">Second decoded element</param>
		/// <param name="item3">Third decoded element</param>
		/// <param name="item4">Fourth decoded element</param>
		/// <returns><see langword="true"/> if the tuple was decoded successfuly, or <see langword="false"/> if the tuple is too small, or if there was an error while decoding</returns>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool TryDecodeLast<T1, T2, T3, T4>(Slice packedKey, out T1? item1, out T2? item2, out T3? item3, out T4? item4)
			=> TupleEncoder.TryDecodeLast(packedKey.Span, null, out item1, out item2, out item3, out item4);

		/// <summary>Unpack a tuple and only return its last 4 elements</summary>
		/// <typeparam name="T1">Type of the fourth value from the end of the decoded tuple</typeparam>
		/// <typeparam name="T2">Type of the third value from the end of the decoded tuple</typeparam>
		/// <typeparam name="T3">Type of the second value from the end of the decoded tuple</typeparam>
		/// <typeparam name="T4">Type of the last value in the decoded tuple</typeparam>
		/// <param name="packedKey">Slice that should be entirely parsable as a tuple of at least 4 elements</param>
		/// <param name="item1">First decoded element</param>
		/// <param name="item2">Second decoded element</param>
		/// <param name="item3">Third decoded element</param>
		/// <param name="item4">Fourth decoded element</param>
		/// <returns><see langword="true"/> if the tuple was decoded successfuly, or <see langword="false"/> if the tuple is too small, or if there was an error while decoding</returns>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool TryDecodeLast<T1, T2, T3, T4>(ReadOnlySpan<byte> packedKey, out T1? item1, out T2? item2, out T3? item3, out T4? item4)
			=> TupleEncoder.TryDecodeLast(packedKey, null, out item1, out item2, out item3, out item4);

		/// <summary>Unpack a tuple and only return its last 4 elements</summary>
		/// <typeparam name="T1">Type of the fourth value from the end of the decoded tuple</typeparam>
		/// <typeparam name="T2">Type of the third value from the end of the decoded tuple</typeparam>
		/// <typeparam name="T3">Type of the second value from the end of the decoded tuple</typeparam>
		/// <typeparam name="T4">Type of the last value in the decoded tuple</typeparam>
		/// <param name="packedKey">Slice that should be entirely parsable as a tuple of at least 4 elements</param>
		/// <param name="item1">First decoded element</param>
		/// <param name="item2">Second decoded element</param>
		/// <param name="item3">Third decoded element</param>
		/// <param name="item4">Fourth decoded element</param>
		/// <param name="expectedSize">Expected size of the decoded tuple</param>
		/// <returns><see langword="true"/> if the tuple was decoded successfuly, or <see langword="false"/> if the tuple is too small, or if there was an error while decoding</returns>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool TryDecodeLast<T1, T2, T3, T4>(Slice packedKey, int expectedSize, out T1? item1, out T2? item2, out T3? item3, out T4? item4)
			=> TupleEncoder.TryDecodeLast(packedKey.Span, expectedSize, out item1, out item2, out item3, out item4);

		/// <summary>Unpack a tuple and only return its last 4 elements</summary>
		/// <typeparam name="T1">Type of the fourth value from the end of the decoded tuple</typeparam>
		/// <typeparam name="T2">Type of the third value from the end of the decoded tuple</typeparam>
		/// <typeparam name="T3">Type of the second value from the end of the decoded tuple</typeparam>
		/// <typeparam name="T4">Type of the last value in the decoded tuple</typeparam>
		/// <param name="packedKey">Slice that should be entirely parsable as a tuple of at least 4 elements</param>
		/// <param name="item1">First decoded element</param>
		/// <param name="item2">Second decoded element</param>
		/// <param name="item3">Third decoded element</param>
		/// <param name="item4">Fourth decoded element</param>
		/// <param name="expectedSize">Expected size of the decoded tuple</param>
		/// <returns><see langword="true"/> if the tuple was decoded successfuly, or <see langword="false"/> if the tuple is too small, or if there was an error while decoding</returns>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool TryDecodeLast<T1, T2, T3, T4>(ReadOnlySpan<byte> packedKey, int expectedSize, out T1? item1, out T2? item2, out T3? item3, out T4? item4)
			=> TupleEncoder.TryDecodeLast(packedKey, expectedSize, out item1, out item2, out item3, out item4);

		#endregion

		#region DecodeKey...

		/// <summary>Unpack the value of a singleton tuple</summary>
		/// <typeparam name="T1">Type of the single value in the decoded tuple</typeparam>
		/// <param name="packedKey">Slice that should contain the packed representation of a tuple with a single element</param>
		/// <returns>Decoded value of the only item in the tuple. Throws an exception if the tuple is empty of has more than one element.</returns>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static T1? DecodeKey<T1>(Slice packedKey) => DecodeKey<T1>(packedKey.Span);

		/// <summary>Unpack the value of a singleton tuple</summary>
		/// <typeparam name="T1">Type of the single value in the decoded tuple</typeparam>
		/// <param name="packedKey">Slice that should contain the packed representation of a tuple with a single element</param>
		/// <returns>Decoded value of the only item in the tuple. Throws an exception if the tuple is empty of has more than one element.</returns>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static T1? DecodeKey<T1>(ReadOnlySpan<byte> packedKey)
		{
			var reader = new TupleReader(packedKey);
			TupleEncoder.DecodeKey(ref reader, out T1? item1);
			return item1;
		}

		/// <summary>Unpack the value of a singleton tuple</summary>
		/// <typeparam name="T1">Type of the single value in the decoded tuple</typeparam>
		/// <param name="packedKey">Slice that should contain the packed representation of a tuple with a single element</param>
		/// <param name="item1">Decoded value of the only item in the tuple. Throws an exception if the tuple is empty of has more than one element.</param>
		/// <returns><c>true</c> if the packed key was successfully unpacked.</returns>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool TryDecodeKey<T1>(Slice packedKey, out T1? item1) => TryDecodeKey<T1>(packedKey.Span, out item1);

		/// <summary>Unpack the value of a singleton tuple</summary>
		/// <typeparam name="T1">Type of the single value in the decoded tuple</typeparam>
		/// <param name="packedKey">Slice that should contain the packed representation of a tuple with a single element</param>
		/// <param name="item1">Decoded value of the only item in the tuple. Throws an exception if the tuple is empty of has more than one element.</param>
		/// <returns><c>true</c> if the packed key was successfully unpacked.</returns>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool TryDecodeKey<T1>(ReadOnlySpan<byte> packedKey, out T1? item1)
		{
			var reader = new TupleReader(packedKey);
			return TupleEncoder.TryDecodeKey(ref reader, out item1, out _);
		}

		/// <summary>Unpack a key containing two elements</summary>
		/// <param name="packedKey">Slice that should contain the packed representation of a tuple with two elements</param>
		/// <returns>Decoded value of the elements int the tuple. Throws an exception if the tuple is empty of has more than two elements.</returns>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static (T1?, T2?) DecodeKey<T1, T2>(Slice packedKey) => DecodeKey<T1, T2>(packedKey.Span);

		/// <summary>Unpack a key containing two elements</summary>
		/// <param name="packedKey">Slice that should contain the packed representation of a tuple with two elements</param>
		/// <returns>Decoded value of the elements int the tuple. Throws an exception if the tuple is empty of has more than two elements.</returns>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static (T1?, T2?) DecodeKey<T1, T2>(ReadOnlySpan<byte> packedKey)
		{
			var reader = new TupleReader(packedKey);
			TupleEncoder.DecodeKey(ref reader, out (T1?, T2?) tuple);
			return tuple;
		}

		/// <summary>Unpack a key containing three elements</summary>
		/// <param name="packedKey">Slice that should contain the packed representation of a tuple with three elements</param>
		/// <returns>Decoded value of the elements int the tuple. Throws an exception if the tuple is empty of has more than three elements.</returns>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static (T1?, T2?, T3?) DecodeKey<T1, T2, T3>(Slice packedKey) => DecodeKey<T1, T2, T3>(packedKey.Span);

		/// <summary>Unpack a key containing three elements</summary>
		/// <param name="packedKey">Slice that should contain the packed representation of a tuple with three elements</param>
		/// <returns>Decoded value of the elements int the tuple. Throws an exception if the tuple is empty of has more than three elements.</returns>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static (T1?, T2?, T3?) DecodeKey<T1, T2, T3>(ReadOnlySpan<byte> packedKey)
		{
			var reader = new TupleReader(packedKey);
			TupleEncoder.DecodeKey(ref reader, out (T1?, T2?, T3?) tuple);
			return tuple;
		}

		/// <summary>Unpack a key containing four elements</summary>
		/// <param name="packedKey">Slice that should contain the packed representation of a tuple with four elements</param>
		/// <returns>Decoded value of the elements int the tuple. Throws an exception if the tuple is empty of has more than four elements.</returns>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static (T1?, T2?, T3?, T4?) DecodeKey<T1, T2, T3, T4>(Slice packedKey) => DecodeKey<T1, T2, T3, T4>(packedKey.Span);

		/// <summary>Unpack a key containing four elements</summary>
		/// <param name="packedKey">Slice that should contain the packed representation of a tuple with four elements</param>
		/// <returns>Decoded value of the elements int the tuple. Throws an exception if the tuple is empty of has more than four elements.</returns>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static (T1?, T2?, T3?, T4?) DecodeKey<T1, T2, T3, T4>(ReadOnlySpan<byte> packedKey)
		{
			var reader = new TupleReader(packedKey);
			TupleEncoder.DecodeKey(ref reader, out (T1?, T2?, T3?, T4?) tuple);
			return tuple;
		}

		/// <summary>Unpack a key containing five elements</summary>
		/// <param name="packedKey">Slice that should contain the packed representation of a tuple with five elements</param>
		/// <returns>Decoded value of the elements int the tuple. Throws an exception if the tuple is empty of has more than five elements.</returns>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static (T1?, T2?, T3?, T4?, T5?) DecodeKey<T1, T2, T3, T4, T5>(Slice packedKey) => DecodeKey<T1, T2, T3, T4, T5>(packedKey.Span);

		/// <summary>Unpack a key containing five elements</summary>
		/// <param name="packedKey">Slice that should contain the packed representation of a tuple with five elements</param>
		/// <returns>Decoded value of the elements int the tuple. Throws an exception if the tuple is empty of has more than five elements.</returns>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static (T1?, T2?, T3?, T4?, T5?) DecodeKey<T1, T2, T3, T4, T5>(ReadOnlySpan<byte> packedKey)
		{
			var reader = new TupleReader(packedKey);
			TupleEncoder.DecodeKey(ref reader, out (T1?, T2?, T3?, T4?, T5?) tuple);
			return tuple;
		}

		/// <summary>Unpack a key containing six elements</summary>
		/// <param name="packedKey">Slice that should contain the packed representation of a tuple with six elements</param>
		/// <returns>Decoded value of the elements int the tuple. Throws an exception if the tuple is empty of has more than six elements.</returns>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static (T1?, T2?, T3?, T4?, T5?, T6?) DecodeKey<T1, T2, T3, T4, T5, T6>(Slice packedKey) => DecodeKey<T1, T2, T3, T4, T5, T6>(packedKey.Span);

		/// <summary>Unpack a key containing six elements</summary>
		/// <param name="packedKey">Slice that should contain the packed representation of a tuple with six elements</param>
		/// <returns>Decoded value of the elements int the tuple. Throws an exception if the tuple is empty of has more than six elements.</returns>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static (T1?, T2?, T3?, T4?, T5?, T6?) DecodeKey<T1, T2, T3, T4, T5, T6>(ReadOnlySpan<byte> packedKey)
		{
			var reader = new TupleReader(packedKey);
			TupleEncoder.DecodeKey(ref reader, out (T1?, T2?, T3?, T4?, T5?, T6?) tuple);
			return tuple;
		}

		/// <summary>Unpack a key containing seven elements</summary>
		/// <param name="packedKey">Slice that should contain the packed representation of a tuple with seven elements</param>
		/// <returns>Decoded value of the elements int the tuple. Throws an exception if the tuple is empty of has more than seven elements.</returns>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static (T1?, T2?, T3?, T4?, T5?, T6?, T7?) DecodeKey<T1, T2, T3, T4, T5, T6, T7>(Slice packedKey) => DecodeKey<T1, T2, T3, T4, T5, T6, T7>(packedKey.Span);

		/// <summary>Unpack a key containing seven elements</summary>
		/// <param name="packedKey">Slice that should contain the packed representation of a tuple with seven elements</param>
		/// <returns>Decoded value of the elements int the tuple. Throws an exception if the tuple is empty of has more than seven elements.</returns>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static (T1?, T2?, T3?, T4?, T5?, T6?, T7?) DecodeKey<T1, T2, T3, T4, T5, T6, T7>(ReadOnlySpan<byte> packedKey)
		{
			var reader = new TupleReader(packedKey);
			TupleEncoder.DecodeKey(ref reader, out (T1?, T2?, T3?, T4?, T5?, T6?, T7?) tuple);
			return tuple;
		}

		/// <summary>Unpack a key containing eight elements</summary>
		/// <param name="packedKey">Slice that should contain the packed representation of a tuple with eight elements</param>
		/// <returns>Decoded value of the elements int the tuple. Throws an exception if the tuple is empty of has more than eight elements.</returns>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static (T1?, T2?, T3?, T4?, T5?, T6?, T7?, T8?) DecodeKey<T1, T2, T3, T4, T5, T6, T7, T8>(Slice packedKey) => DecodeKey<T1, T2, T3, T4, T5, T6, T7, T8>(packedKey.Span);

		/// <summary>Unpack a key containing eight elements</summary>
		/// <param name="packedKey">Slice that should contain the packed representation of a tuple with eight elements</param>
		/// <returns>Decoded value of the elements int the tuple. Throws an exception if the tuple is empty of has more than eight elements.</returns>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static (T1?, T2?, T3?, T4?, T5?, T6?, T7?, T8?) DecodeKey<T1, T2, T3, T4, T5, T6, T7, T8>(ReadOnlySpan<byte> packedKey)
		{
			var reader = new TupleReader(packedKey);
			TupleEncoder.DecodeKey(ref reader, out (T1?, T2?, T3?, T4?, T5?, T6?, T7?, T8?) tuple);
			return tuple;
		}

		#endregion

		#endregion

		#region EncodePrefixedKey...

		//note: they are equivalent to the Pack<...>() methods, they only take a binary prefix

		/// <summary>Efficiently concatenate a prefix with the packed representation of a 1-tuple</summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Slice EncodePrefixedKey<T1>(Slice prefix, T1? value) => TupleEncoder.EncodeKey(prefix.Span, value);

		/// <summary>Efficiently concatenate a prefix with the packed representation of a 1-tuple</summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Slice EncodePrefixedKey<T1>(ReadOnlySpan<byte> prefix, T1? value) => TupleEncoder.EncodeKey(prefix, value);

		/// <summary>Efficiently concatenate a prefix with the packed representation of a 2-tuple</summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Slice EncodePrefixedKey<T1, T2>(Slice prefix, T1? value1, T2? value2)
		{
			return TupleEncoder.EncodeKey(prefix.Span, value1, value2);
		}

		/// <summary>Efficiently concatenate a prefix with the packed representation of a 2-tuple</summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Slice EncodePrefixedKey<T1, T2>(ReadOnlySpan<byte> prefix, T1? value1, T2? value2)
		{
			return TupleEncoder.EncodeKey(prefix, value1, value2);
		}

		/// <summary>Efficiently concatenate a prefix with the packed representation of a 3-tuple</summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Slice EncodePrefixedKey<T1, T2, T3>(Slice prefix, T1? value1, T2? value2, T3? value3)
		{
			return TupleEncoder.EncodeKey(prefix.Span, value1, value2, value3);
		}

		/// <summary>Efficiently concatenate a prefix with the packed representation of a 3-tuple</summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Slice EncodePrefixedKey<T1, T2, T3>(ReadOnlySpan<byte> prefix, T1? value1, T2? value2, T3? value3)
		{
			return TupleEncoder.EncodeKey(prefix, value1, value2, value3);
		}

		/// <summary>Efficiently concatenate a prefix with the packed representation of a 4-tuple</summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Slice EncodePrefixedKey<T1, T2, T3, T4>(Slice prefix, T1? value1, T2? value2, T3? value3, T4? value4)
		{
			return TupleEncoder.EncodeKey(prefix.Span, value1, value2, value3, value4);
		}

		/// <summary>Efficiently concatenate a prefix with the packed representation of a 4-tuple</summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Slice EncodePrefixedKey<T1, T2, T3, T4>(ReadOnlySpan<byte> prefix, T1? value1, T2? value2, T3? value3, T4? value4)
		{
			return TupleEncoder.EncodeKey(prefix, value1, value2, value3, value4);
		}

		/// <summary>Efficiently concatenate a prefix with the packed representation of a 5-tuple</summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Slice EncodePrefixedKey<T1, T2, T3, T4, T5>(Slice prefix, T1? value1, T2? value2, T3? value3, T4? value4, T5? value5)
		{
			return TupleEncoder.EncodeKey(prefix.Span, value1, value2, value3, value4, value5);
		}

		/// <summary>Efficiently concatenate a prefix with the packed representation of a 5-tuple</summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Slice EncodePrefixedKey<T1, T2, T3, T4, T5>(ReadOnlySpan<byte> prefix, T1? value1, T2? value2, T3? value3, T4? value4, T5? value5)
		{
			return TupleEncoder.EncodeKey(prefix, value1, value2, value3, value4, value5);
		}

		/// <summary>Efficiently concatenate a prefix with the packed representation of a 6-tuple</summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Slice EncodePrefixedKey<T1, T2, T3, T4, T5, T6>(Slice prefix, T1? value1, T2? value2, T3? value3, T4? value4, T5? value5, T6? value6)
		{
			return TupleEncoder.EncodeKey(prefix.Span, value1, value2, value3, value4, value5, value6);
		}

		/// <summary>Efficiently concatenate a prefix with the packed representation of a 6-tuple</summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Slice EncodePrefixedKey<T1, T2, T3, T4, T5, T6>(ReadOnlySpan<byte> prefix, T1? value1, T2? value2, T3? value3, T4? value4, T5? value5, T6? value6)
		{
			return TupleEncoder.EncodeKey(prefix, value1, value2, value3, value4, value5, value6);
		}

		/// <summary>Efficiently concatenate a prefix with the packed representation of a 7-tuple</summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Slice EncodePrefixedKey<T1, T2, T3, T4, T5, T6, T7>(Slice prefix, T1? value1, T2? value2, T3? value3, T4? value4, T5? value5, T6? value6, T7? value7)
		{
			return TupleEncoder.EncodeKey(prefix.Span, value1, value2, value3, value4, value5, value6, value7);
		}

		/// <summary>Efficiently concatenate a prefix with the packed representation of a 7-tuple</summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Slice EncodePrefixedKey<T1, T2, T3, T4, T5, T6, T7>(ReadOnlySpan<byte> prefix, T1? value1, T2? value2, T3? value3, T4? value4, T5? value5, T6? value6, T7? value7)
		{
			return TupleEncoder.EncodeKey(prefix, value1, value2, value3, value4, value5, value6, value7);
		}

		/// <summary>Efficiently concatenate a prefix with the packed representation of a 8-tuple</summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Slice EncodePrefixedKey<T1, T2, T3, T4, T5, T6, T7, T8>(Slice prefix, T1? value1, T2? value2, T3? value3, T4? value4, T5? value5, T6? value6, T7? value7, T8? value8)
		{
			return TupleEncoder.EncodeKey(prefix.Span, value1, value2, value3, value4, value5, value6, value7, value8);
		}

		/// <summary>Efficiently concatenate a prefix with the packed representation of a 8-tuple</summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Slice EncodePrefixedKey<T1, T2, T3, T4, T5, T6, T7, T8>(ReadOnlySpan<byte> prefix, T1? value1, T2? value2, T3? value3, T4? value4, T5? value5, T6? value6, T7? value7, T8? value8)
		{
			return TupleEncoder.EncodeKey(prefix, value1, value2, value3, value4, value5, value6, value7, value8);
		}

		#endregion

	}

}
