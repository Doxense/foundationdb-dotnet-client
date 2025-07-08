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

#pragma warning disable IL2026 // Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code
#pragma warning disable IL2091 // Target generic argument does not satisfy 'DynamicallyAccessedMembersAttribute' in target method or type. The generic parameter of the source method or type does not have matching annotations.
#pragma warning disable IL3050 // Calling members annotated with 'RequiresDynamicCodeAttribute' may break functionality when AOT compiling.

namespace SnowBank.Data.Tuples.Binary
{
	using System.Runtime.InteropServices;
	using SnowBank.Data.Tuples;
	using SnowBank.Data.Binary;
	using SnowBank.Buffers;

	/// <summary>Helper class to encode and decode tuples to and from binary buffers</summary>
	/// <remarks>This class is intended for implementors of tuples, and should not be called directly by application code!</remarks>
	public static class TupleEncoder
	{

		/// <summary>Internal helper that serializes the content of a Tuple into a TupleWriter, meant to be called by implementers of <see cref="IVarTuple"/> types.</summary>
		/// <remarks>Warning: This method will call into <see cref="ITupleSerializable.PackTo"/> if <paramref name="tuple"/> implements <see cref="ITupleSerializable"/></remarks>

		internal static void WriteTo<TTuple>(TupleWriter writer, in TTuple tuple)
			where TTuple : IVarTuple?
		{
			Contract.Debug.Requires(tuple != null);

			// ReSharper disable once SuspiciousTypeConversion.Global
			if (tuple is ITupleSerializable ts)
			{ // optimized version
				ts.PackTo(writer);
				return;
			}

			int n = tuple.Count;
			// small tuples probably are faster with indexers
			//REVIEW: when should we use indexers, and when should we use foreach?
			if (n <= 4)
			{
				for (int i = 0; i < n; i++)
				{
					TuplePackers.SerializeObjectTo(writer, tuple[i]);
				}
			}
			else
			{
				foreach (object? item in tuple)
				{
					TuplePackers.SerializeObjectTo(writer, item);
				}
			}
		}

		#region Packing...

		// Without prefix

		/// <summary>Packs a tuple into a slice</summary>
		/// <param name="tuple">Tuple that must be serialized into a binary slice</param>
		[Pure]
		public static Slice Pack<TTuple>(in TTuple? tuple)
			where TTuple : IVarTuple?
		{
			if (tuple is null) return Slice.Nil;
			var sw = new SliceWriter();
			var tw = new TupleWriter(ref sw);
			WriteTo(tw, in tuple);
			return sw.ToSlice();
		}

		/// <summary>Packs an array of N-tuples, all sharing the same buffer</summary>
		/// <param name="tuples">Sequence of N-tuples to pack</param>
		/// <returns>Array containing the buffer segment of each packed tuple</returns>
		/// <example>BatchPack([ ("Foo", 1), ("Foo", 2) ]) => [ "\x02Foo\x00\x15\x01", "\x02Foo\x00\x15\x02" ] </example>
		public static Slice[] Pack<TTuple>(TTuple[] tuples) //REVIEW: change name to PackRange or PackBatch?
			where TTuple : IVarTuple?
		{
			return Pack(default, tuples);
		}

		/// <summary>Packs an array of N-tuples, all sharing the same buffer</summary>
		/// <param name="tuples">Sequence of N-tuples to pack</param>
		/// <returns>Array containing the buffer segment of each packed tuple</returns>
		/// <example>BatchPack([ ("Foo", 1), ("Foo", 2) ]) => [ "\x02Foo\x00\x15\x01", "\x02Foo\x00\x15\x02" ] </example>
		public static Slice[] Pack<TTuple>(ReadOnlySpan<TTuple> tuples) //REVIEW: change name to PackRange or PackBatch?
			where TTuple : IVarTuple?
		{
			return Pack(default, tuples);
		}

		public static void PackTo<TTuple>(ref SliceWriter writer, TTuple? tuple)
			where TTuple : IVarTuple?
		{
			if (tuple != null)
			{
				var tw = new TupleWriter(ref writer);
				WriteTo(tw, tuple);
			}
		}

		public static void PackTo<TTuple>(TupleWriter writer, TTuple? tuple)
			where TTuple : IVarTuple?
		{
			if (tuple != null)
			{
				WriteTo(writer, tuple);
			}
		}

		// With prefix

		/// <summary>Efficiently concatenates a prefix with the packed representation of a tuple</summary>
		public static Slice Pack<TTuple>(ReadOnlySpan<byte> prefix, TTuple? tuple)
			where TTuple : IVarTuple?
		{
			if (tuple == null || tuple.Count == 0) return Slice.FromBytes(prefix);

			var sw = new SliceWriter(checked(32 + prefix.Length));
			sw.WriteBytes(prefix);
			var tw = new TupleWriter(ref sw);
			WriteTo(tw, tuple);
			return sw.ToSlice();
		}

		/// <summary>Packs an array of N-tuples, all sharing the same buffer</summary>
		/// <param name="prefix">Common prefix added to all the tuples</param>
		/// <param name="tuples">Sequence of N-tuples to pack</param>
		/// <returns>Array containing the buffer segment of each packed tuple</returns>
		/// <example>BatchPack("abc", [ ("Foo", 1), ("Foo", 2) ]) => [ "abc\x02Foo\x00\x15\x01", "abc\x02Foo\x00\x15\x02" ] </example>
		public static Slice[] Pack<TTuple>(ReadOnlySpan<byte> prefix, TTuple[] tuples)
			where TTuple : IVarTuple?
		{
			Contract.NotNull(tuples);

			// pre-allocate by supposing that each tuple will take at least 16 bytes
			var sw = new SliceWriter(checked(tuples.Length * (16 + prefix.Length)));
			var tw = new TupleWriter(ref sw);
			var next = new List<int>(tuples.Length);

			//TODO: use multiple buffers if item count is huge ?

			foreach (var tuple in tuples)
			{
				sw.WriteBytes(prefix);
				WriteTo(tw, tuple);
				next.Add(sw.Position);
			}

			return Slice.SplitIntoSegments(sw.GetBufferUnsafe(), 0, next);
		}

		/// <summary>Packs an array of N-tuples, all sharing the same buffer</summary>
		/// <param name="prefix">Common prefix added to all the tuples</param>
		/// <param name="tuples">Sequence of N-tuples to pack</param>
		/// <returns>Array containing the buffer segment of each packed tuple</returns>
		/// <example>BatchPack("abc", [ ("Foo", 1), ("Foo", 2) ]) => [ "abc\x02Foo\x00\x15\x01", "abc\x02Foo\x00\x15\x02" ] </example>
		public static Slice[] Pack<TTuple>(ReadOnlySpan<byte> prefix, ReadOnlySpan<TTuple> tuples)
			where TTuple : IVarTuple?
		{
			// pre-allocate by supposing that each tuple will take at least 16 bytes
			var sw = new SliceWriter(checked(tuples.Length * (16 + prefix.Length)));
			var tw = new TupleWriter(ref sw);
			var next = new List<int>(tuples.Length);

			//TODO: use multiple buffers if item count is huge ?

			foreach (var tuple in tuples)
			{
				sw.WriteBytes(prefix);
				WriteTo(tw, tuple);
				next.Add(sw.Position);
			}

			return Slice.SplitIntoSegments(sw.GetBufferUnsafe(), 0, next);
		}

		/// <summary>Packs a sequence of N-tuples, all sharing the same buffer</summary>
		/// <param name="prefix">Common prefix added to all the tuples</param>
		/// <param name="tuples">Sequence of N-tuples to pack</param>
		/// <returns>Array containing the buffer segment of each packed tuple</returns>
		/// <example>BatchPack("abc", [ ("Foo", 1), ("Foo", 2) ]) => [ "abc\x02Foo\x00\x15\x01", "abc\x02Foo\x00\x15\x02" ] </example>
		public static Slice[] Pack<TTuple>(ReadOnlySpan<byte> prefix, IEnumerable<TTuple> tuples)
			where TTuple : IVarTuple?
		{
			Contract.NotNull(tuples);

			// use optimized version for arrays
			if (tuples is TTuple[] array)
			{
				return Pack(prefix, array);
			}

			if (tuples is List<TTuple> list)
			{
				return Pack(prefix, CollectionsMarshal.AsSpan(list));
			}

			var next = new List<int>((tuples as ICollection<TTuple>)?.Count ?? 0);
			var sw = new SliceWriter(checked(next.Capacity * (16 + prefix.Length)));
			var tw = new TupleWriter(ref sw);

			//TODO: use multiple buffers if item count is huge ?

			foreach (var tuple in tuples)
			{
				sw.WriteBytes(prefix);
				WriteTo(tw, tuple);
				next.Add(sw.Position);
			}

			return Slice.SplitIntoSegments(sw.GetBufferUnsafe(), 0, next);
		}

		public static Slice[] Pack<TElement, TTuple>(ReadOnlySpan<byte> prefix, TElement[] elements, Func<TElement, TTuple> transform)
			where TTuple : IVarTuple?
		{
			Contract.NotNull(elements);
			Contract.NotNull(transform);

			var next = new List<int>(elements.Length);
			var sw = new SliceWriter(checked(next.Capacity * (16 + prefix.Length)));
			var tw = new TupleWriter(ref sw);

			//TODO: use multiple buffers if item count is huge ?

			foreach (var element in elements)
			{
				var tuple = transform(element);
				if (tuple == null)
				{
					next.Add(sw.Position);
				}
				else
				{
					sw.WriteBytes(prefix);
					WriteTo(tw, tuple);
					next.Add(sw.Position);
				}
			}

			return Slice.SplitIntoSegments(sw.GetBufferUnsafe(), 0, next);
		}

		public static Slice[] Pack<TElement, TTuple>(ReadOnlySpan<byte> prefix, ReadOnlySpan<TElement> elements, Func<TElement, TTuple> transform)
			where TTuple : IVarTuple?
		{
			Contract.NotNull(transform);

			var next = new List<int>(elements.Length);
			var sw = new SliceWriter(checked(next.Capacity * (16 + prefix.Length)));
			var tw = new TupleWriter(ref sw);

			//TODO: use multiple buffers if item count is huge ?

			foreach (var element in elements)
			{
				var tuple = transform(element);
				if (tuple == null)
				{
					next.Add(sw.Position);
				}
				else
				{
					sw.WriteBytes(prefix);
					WriteTo(tw, tuple);
					next.Add(sw.Position);
				}
			}

			return Slice.SplitIntoSegments(sw.GetBufferUnsafe(), 0, next);
		}

		public static Slice[] Pack<TElement, TTuple>(ReadOnlySpan<byte> prefix, IEnumerable<TElement> elements, Func<TElement, TTuple> transform)
			where TTuple : IVarTuple?
		{
			Contract.NotNull(elements);
			Contract.NotNull(transform);

			// use optimized version for arrays
			if (elements is TElement[] array)
			{
				return Pack(prefix, array, transform);
			}

			var next = new List<int>((elements as ICollection<TElement>)?.Count ?? 0);
			var sw = new SliceWriter(checked(next.Capacity * (16 + prefix.Length)));
			var tw = new TupleWriter(ref sw);

			//TODO: use multiple buffers if item count is huge ?

			foreach (var element in elements)
			{
				var tuple = transform(element);
				if (tuple == null)
				{
					next.Add(sw.Position);
				}
				else
				{
					sw.WriteBytes(prefix);
					WriteTo(tw, tuple);
					next.Add(sw.Position);
				}
			}

			return Slice.SplitIntoSegments(sw.GetBufferUnsafe(), 0, next);
		}

		// With prefix...

		/// <summary>Efficiently concatenates a prefix with the packed representation of a 1-tuple</summary>
		[Pure]
		public static Slice EncodeKey<T1>(ReadOnlySpan<byte> prefix, T1? value)
		{
			var sw = new SliceWriter();
			sw.WriteBytes(prefix);
			var tw = new TupleWriter(ref sw);
			TuplePackers.SerializeTo(tw, value);
			return sw.ToSlice();
		}

		/// <summary>Efficiently concatenates a prefix with the packed representation of a 1-tuple</summary>
		[Pure]
		public static Slice Pack<T1>(ReadOnlySpan<byte> prefix, in ValueTuple<T1?> items)
		{
			var sw = new SliceWriter();
			sw.WriteBytes(prefix);
			var tw = new TupleWriter(ref sw);
			TuplePackers.SerializeTo(tw, items.Item1);
			return sw.ToSlice();
		}

		/// <summary>Efficiently concatenates a prefix with the packed representation of a 2-tuple</summary>
		[Pure]
		public static Slice EncodeKey<T1, T2>(ReadOnlySpan<byte> prefix, T1? value1, T2? value2)
		{
			var sw = new SliceWriter();
			sw.WriteBytes(prefix);
			var tw = new TupleWriter(ref sw);
			TuplePackers.SerializeTo(tw, value1);
			TuplePackers.SerializeTo(tw, value2);
			return sw.ToSlice();
		}

		/// <summary>Efficiently concatenates a prefix with the packed representation of a 2-tuple</summary>
		[Pure]
		public static Slice Pack<T1, T2>(ReadOnlySpan<byte> prefix, in (T1?, T2?) items)
		{
			var sw = new SliceWriter();
			sw.WriteBytes(prefix);
			var tw = new TupleWriter(ref sw);
			TuplePackers.SerializeTo(tw, items.Item1);
			TuplePackers.SerializeTo(tw, items.Item2);
			return sw.ToSlice();
		}

		/// <summary>Efficiently concatenates a prefix with the packed representation of a 3-tuple</summary>
		public static Slice EncodeKey<T1, T2, T3>(ReadOnlySpan<byte> prefix, T1? value1, T2? value2, T3? value3)
		{
			var sw = new SliceWriter();
			sw.WriteBytes(prefix);
			var tw = new TupleWriter(ref sw);
			TuplePackers.SerializeTo(tw, value1);
			TuplePackers.SerializeTo(tw, value2);
			TuplePackers.SerializeTo(tw, value3);
			return sw.ToSlice();
		}

		/// <summary>Efficiently concatenates a prefix with the packed representation of a 3-tuple</summary>
		public static Slice Pack<T1, T2, T3>(ReadOnlySpan<byte> prefix, in (T1?, T2?, T3?) items)
		{
			var sw = new SliceWriter();
			sw.WriteBytes(prefix);
			var tw = new TupleWriter(ref sw);
			TuplePackers.SerializeTo(tw, items.Item1);
			TuplePackers.SerializeTo(tw, items.Item2);
			TuplePackers.SerializeTo(tw, items.Item3);
			return sw.ToSlice();
		}

		/// <summary>Efficiently concatenates a prefix with the packed representation of a 4-tuple</summary>
		public static Slice EncodeKey<T1, T2, T3, T4>(ReadOnlySpan<byte> prefix, T1? value1, T2? value2, T3? value3, T4? value4)
		{
			var sw = new SliceWriter();
			sw.WriteBytes(prefix);
			var tw = new TupleWriter(ref sw);
			TuplePackers.SerializeTo(tw, value1);
			TuplePackers.SerializeTo(tw, value2);
			TuplePackers.SerializeTo(tw, value3);
			TuplePackers.SerializeTo(tw, value4);
			return sw.ToSlice();
		}

		/// <summary>Efficiently concatenates a prefix with the packed representation of a 4-tuple</summary>
		public static Slice Pack<T1, T2, T3, T4>(ReadOnlySpan<byte> prefix, in (T1?, T2?, T3?, T4?) items)
		{
			var sw = new SliceWriter();
			sw.WriteBytes(prefix);
			var tw = new TupleWriter(ref sw);
			TuplePackers.SerializeTo(tw, items.Item1);
			TuplePackers.SerializeTo(tw, items.Item2);
			TuplePackers.SerializeTo(tw, items.Item3);
			TuplePackers.SerializeTo(tw, items.Item4);
			return sw.ToSlice();
		}

		/// <summary>Efficiently concatenates a prefix with the packed representation of a 5-tuple</summary>
		public static Slice EncodeKey<T1, T2, T3, T4, T5>(ReadOnlySpan<byte> prefix, T1? value1, T2? value2, T3? value3, T4? value4, T5? value5)
		{
			var sw = new SliceWriter();
			sw.WriteBytes(prefix);
			var tw = new TupleWriter(ref sw);
			TuplePackers.SerializeTo(tw, value1);
			TuplePackers.SerializeTo(tw, value2);
			TuplePackers.SerializeTo(tw, value3);
			TuplePackers.SerializeTo(tw, value4);
			TuplePackers.SerializeTo(tw, value5);
			return sw.ToSlice();
		}

		/// <summary>Efficiently concatenates a prefix with the packed representation of a 5-tuple</summary>
		public static Slice Pack<T1, T2, T3, T4, T5>(ReadOnlySpan<byte> prefix, in (T1?, T2?, T3?, T4?, T5?) items)
		{
			var sw = new SliceWriter();
			sw.WriteBytes(prefix);
			var tw = new TupleWriter(ref sw);
			TuplePackers.SerializeTo(tw, items.Item1);
			TuplePackers.SerializeTo(tw, items.Item2);
			TuplePackers.SerializeTo(tw, items.Item3);
			TuplePackers.SerializeTo(tw, items.Item4);
			TuplePackers.SerializeTo(tw, items.Item5);
			return sw.ToSlice();
		}

		/// <summary>Efficiently concatenates a prefix with the packed representation of a 6-tuple</summary>
		public static Slice EncodeKey<T1, T2, T3, T4, T5, T6>(ReadOnlySpan<byte> prefix, T1? value1, T2? value2, T3? value3, T4? value4, T5? value5, T6? value6)
		{
			var sw = new SliceWriter();
			sw.WriteBytes(prefix);
			var tw = new TupleWriter(ref sw);
			TuplePackers.SerializeTo(tw, value1);
			TuplePackers.SerializeTo(tw, value2);
			TuplePackers.SerializeTo(tw, value3);
			TuplePackers.SerializeTo(tw, value4);
			TuplePackers.SerializeTo(tw, value5);
			TuplePackers.SerializeTo(tw, value6);
			return sw.ToSlice();
		}

		/// <summary>Efficiently concatenates a prefix with the packed representation of a 6-tuple</summary>
		public static Slice Pack<T1, T2, T3, T4, T5, T6>(ReadOnlySpan<byte> prefix, in (T1?, T2?, T3?, T4?, T5?, T6?) items)
		{
			var sw = new SliceWriter();
			sw.WriteBytes(prefix);
			var tw = new TupleWriter(ref sw);
			TuplePackers.SerializeTo(tw, items.Item1);
			TuplePackers.SerializeTo(tw, items.Item2);
			TuplePackers.SerializeTo(tw, items.Item3);
			TuplePackers.SerializeTo(tw, items.Item4);
			TuplePackers.SerializeTo(tw, items.Item5);
			TuplePackers.SerializeTo(tw, items.Item6);
			return sw.ToSlice();
		}

		/// <summary>Efficiently concatenates a prefix with the packed representation of a 7-tuple</summary>
		public static Slice EncodeKey<T1, T2, T3, T4, T5, T6, T7>(ReadOnlySpan<byte> prefix, T1? value1, T2? value2, T3? value3, T4? value4, T5? value5, T6? value6, T7? value7)
		{
			var sw = new SliceWriter();
			sw.WriteBytes(prefix);
			var tw = new TupleWriter(ref sw);
			TuplePackers.SerializeTo(tw, value1);
			TuplePackers.SerializeTo(tw, value2);
			TuplePackers.SerializeTo(tw, value3);
			TuplePackers.SerializeTo(tw, value4);
			TuplePackers.SerializeTo(tw, value5);
			TuplePackers.SerializeTo(tw, value6);
			TuplePackers.SerializeTo(tw, value7);
			return sw.ToSlice();
		}

		/// <summary>Efficiently concatenates a prefix with the packed representation of a 7-tuple</summary>
		public static Slice Pack<T1, T2, T3, T4, T5, T6, T7>(ReadOnlySpan<byte> prefix, in (T1?, T2?, T3?, T4?, T5?, T6?, T7?) items)
		{
			var sw = new SliceWriter();
			sw.WriteBytes(prefix);
			var tw = new TupleWriter(ref sw);
			TuplePackers.SerializeTo(tw, items.Item1);
			TuplePackers.SerializeTo(tw, items.Item2);
			TuplePackers.SerializeTo(tw, items.Item3);
			TuplePackers.SerializeTo(tw, items.Item4);
			TuplePackers.SerializeTo(tw, items.Item5);
			TuplePackers.SerializeTo(tw, items.Item6);
			TuplePackers.SerializeTo(tw, items.Item7);
			return sw.ToSlice();
		}

		/// <summary>Efficiently concatenates a prefix with the packed representation of an 8-tuple</summary>
		public static Slice EncodeKey<T1, T2, T3, T4, T5, T6, T7, T8>(ReadOnlySpan<byte> prefix, T1? value1, T2? value2, T3? value3, T4? value4, T5? value5, T6? value6, T7? value7, T8? value8)
		{
			var sw = new SliceWriter();
			sw.WriteBytes(prefix);
			var tw = new TupleWriter(ref sw);
			TuplePackers.SerializeTo(tw, value1);
			TuplePackers.SerializeTo(tw, value2);
			TuplePackers.SerializeTo(tw, value3);
			TuplePackers.SerializeTo(tw, value4);
			TuplePackers.SerializeTo(tw, value5);
			TuplePackers.SerializeTo(tw, value6);
			TuplePackers.SerializeTo(tw, value7);
			TuplePackers.SerializeTo(tw, value8);
			return sw.ToSlice();
		}

		/// <summary>Efficiently concatenates a prefix with the packed representation of an 8-tuple</summary>
		public static Slice Pack<T1, T2, T3, T4, T5, T6, T7, T8>(ReadOnlySpan<byte> prefix, in (T1?, T2?, T3?, T4?, T5?, T6?, T7?, T8?) items)
		{
			var sw = new SliceWriter();
			sw.WriteBytes(prefix);
			var tw = new TupleWriter(ref sw);
			TuplePackers.SerializeTo(tw, items.Item1);
			TuplePackers.SerializeTo(tw, items.Item2);
			TuplePackers.SerializeTo(tw, items.Item3);
			TuplePackers.SerializeTo(tw, items.Item4);
			TuplePackers.SerializeTo(tw, items.Item5);
			TuplePackers.SerializeTo(tw, items.Item6);
			TuplePackers.SerializeTo(tw, items.Item7);
			TuplePackers.SerializeTo(tw, items.Item8);
			return sw.ToSlice();
		}

		/// <summary>Efficiently concatenates a prefix with the packed representation of an 9-tuple</summary>
		public static Slice EncodeKey<T1, T2, T3, T4, T5, T6, T7, T8, T9>(ReadOnlySpan<byte> prefix, T1? value1, T2? value2, T3? value3, T4? value4, T5? value5, T6? value6, T7? value7, T8? value8, T9? value9)
		{
			var sw = new SliceWriter();
			sw.WriteBytes(prefix);
			var tw = new TupleWriter(ref sw);
			TuplePackers.SerializeTo(tw, value1);
			TuplePackers.SerializeTo(tw, value2);
			TuplePackers.SerializeTo(tw, value3);
			TuplePackers.SerializeTo(tw, value4);
			TuplePackers.SerializeTo(tw, value5);
			TuplePackers.SerializeTo(tw, value6);
			TuplePackers.SerializeTo(tw, value7);
			TuplePackers.SerializeTo(tw, value8);
			TuplePackers.SerializeTo(tw, value9);
			return sw.ToSlice();
		}

		/// <summary>Efficiently concatenates a prefix with the packed representation of an 9-tuple</summary>
		public static Slice Pack<T1, T2, T3, T4, T5, T6, T7, T8, T9>(ReadOnlySpan<byte> prefix, in (T1?, T2?, T3?, T4?, T5?, T6?, T7?, T8?, T9?) items)
		{
			var sw = new SliceWriter();
			sw.WriteBytes(prefix);
			var tw = new TupleWriter(ref sw);
			TuplePackers.SerializeTo(tw, items.Item1);
			TuplePackers.SerializeTo(tw, items.Item2);
			TuplePackers.SerializeTo(tw, items.Item3);
			TuplePackers.SerializeTo(tw, items.Item4);
			TuplePackers.SerializeTo(tw, items.Item5);
			TuplePackers.SerializeTo(tw, items.Item6);
			TuplePackers.SerializeTo(tw, items.Item7);
			TuplePackers.SerializeTo(tw, items.Item8);
			TuplePackers.SerializeTo(tw, items.Item9);
			return sw.ToSlice();
		}

		// EncodeKey...

		//REVIEW: do we really need "Key" in the name?
		// => we want to make it obvious that this is to pack ordered keys, but this could be used for anything else...
		// => EncodeValues? (maybe confused with unordered encoding)
		// => EncodeItems?
		// => Encode?

		/// <summary>Packs a 1-tuple directly into a slice</summary>
		public static Slice Pack<T1>(ReadOnlySpan<byte> prefix, in STuple<T1> tuple)
		{
			var sw = new SliceWriter();
			sw.WriteBytes(prefix);
			var tw = new TupleWriter(ref sw);
			TupleSerializer<T1>.Default.PackTo(tw, tuple);
			return sw.ToSlice();
		}

		/// <summary>Packs a 2-tuple directly into a slice</summary>
		public static Slice Pack<T1, T2>(ReadOnlySpan<byte> prefix, in STuple<T1, T2> tuple)
		{
			var sw = new SliceWriter();
			sw.WriteBytes(prefix);
			var tw = new TupleWriter(ref sw);
			TupleSerializer<T1, T2>.Default.PackTo(tw, tuple);
			return sw.ToSlice();
		}

		/// <summary>Packs a 3-tuple directly into a slice</summary>
		public static Slice Pack<T1, T2, T3>(ReadOnlySpan<byte> prefix, in STuple<T1, T2, T3> tuple)
		{
			var sw = new SliceWriter();
			sw.WriteBytes(prefix);
			var tw = new TupleWriter(ref sw);
			TupleSerializer<T1, T2, T3>.Default.PackTo(tw, tuple);
			return sw.ToSlice();
		}

		/// <summary>Packs a 4-tuple directly into a slice</summary>
		public static Slice Pack<T1, T2, T3, T4>(ReadOnlySpan<byte> prefix, in STuple<T1, T2, T3, T4> tuple)
		{
			var sw = new SliceWriter();
			sw.WriteBytes(prefix);
			var tw = new TupleWriter(ref sw);
			TupleSerializer<T1, T2, T3, T4>.Default.PackTo(tw, tuple);
			return sw.ToSlice();
		}

		/// <summary>Packs a 5-tuple directly into a slice</summary>
		public static Slice Pack<T1, T2, T3, T4, T5>(ReadOnlySpan<byte> prefix, in STuple<T1, T2, T3, T4, T5> tuple)
		{
			var sw = new SliceWriter();
			sw.WriteBytes(prefix);
			var tw = new TupleWriter(ref sw);
			TupleSerializer<T1, T2, T3, T4, T5>.Default.PackTo(tw, tuple);
			return sw.ToSlice();
		}

		/// <summary>Packs a 6-tuple directly into a slice</summary>
		public static Slice Pack<T1, T2, T3, T4, T5, T6>(ReadOnlySpan<byte> prefix, in STuple<T1, T2, T3, T4, T5, T6> tuple)
		{
			var sw = new SliceWriter();
			sw.WriteBytes(prefix);
			var tw = new TupleWriter(ref sw);
			TupleSerializer<T1, T2, T3, T4, T5, T6>.Default.PackTo(tw, tuple);
			return sw.ToSlice();
		}

		/// <summary>Packs a 7-tuple directly into a slice</summary>
		public static Slice Pack<T1, T2, T3, T4, T5, T6, T7>(ReadOnlySpan<byte> prefix, in STuple<T1, T2, T3, T4, T5, T6, T7> tuple)
		{
			var sw = new SliceWriter();
			sw.WriteBytes(prefix);
			var tw = new TupleWriter(ref sw);
			TupleSerializer<T1, T2, T3, T4, T5, T6, T7>.Default.PackTo(tw, tuple);
			return sw.ToSlice();
		}

		/// <summary>Packs a 8-tuple directly into a slice</summary>
		public static Slice Pack<T1, T2, T3, T4, T5, T6, T7, T8>(ReadOnlySpan<byte> prefix, in STuple<T1, T2, T3, T4, T5, T6, T7, T8> tuple)
		{
			var sw = new SliceWriter();
			sw.WriteBytes(prefix);
			var tw = new TupleWriter(ref sw);
			TupleSerializer<T1, T2, T3, T4, T5, T6, T7, T8>.Default.PackTo(tw, tuple);
			return sw.ToSlice();
		}

		/// <summary>Packs a 1-tuple directly into a slice</summary>
		public static void WriteKeysTo<T1>(ref SliceWriter writer, T1 item1)
		{
			var tw = new TupleWriter(ref writer);
			TuplePackers.SerializeTo(tw, item1);
		}

		/// <summary>Packs a 2-tuple directly into a slice</summary>
		public static void WriteKeysTo<T1, T2>(ref SliceWriter writer, T1? item1, T2? item2)
		{
			var tw = new TupleWriter(ref writer);
			TuplePackers.SerializeTo(tw, item1);
			TuplePackers.SerializeTo(tw, item2);
		}

		/// <summary>Packs a 3-tuple directly into a slice</summary>
		public static void WriteKeysTo<T1, T2, T3>(ref SliceWriter writer, T1? item1, T2? item2, T3? item3)
		{
			var tw = new TupleWriter(ref writer);
			TuplePackers.SerializeTo(tw, item1);
			TuplePackers.SerializeTo(tw, item2);
			TuplePackers.SerializeTo(tw, item3);
		}

		/// <summary>Packs a 4-tuple directly into a slice</summary>
		public static void WriteKeysTo<T1, T2, T3, T4>(ref SliceWriter writer, T1? item1, T2? item2, T3? item3, T4? item4)
		{
			var tw = new TupleWriter(ref writer);
			TuplePackers.SerializeTo(tw, item1);
			TuplePackers.SerializeTo(tw, item2);
			TuplePackers.SerializeTo(tw, item3);
			TuplePackers.SerializeTo(tw, item4);
		}

		/// <summary>Packs a 5-tuple directly into a slice</summary>
		public static void WriteKeysTo<T1, T2, T3, T4, T5>(ref SliceWriter writer, T1? item1, T2? item2, T3? item3, T4? item4, T5? item5)
		{
			var tw = new TupleWriter(ref writer);
			TuplePackers.SerializeTo(tw, item1);
			TuplePackers.SerializeTo(tw, item2);
			TuplePackers.SerializeTo(tw, item3);
			TuplePackers.SerializeTo(tw, item4);
			TuplePackers.SerializeTo(tw, item5);
		}

		/// <summary>Packs a 6-tuple directly into a slice</summary>
		public static void WriteKeysTo<T1, T2, T3, T4, T5, T6>(ref SliceWriter writer, T1? item1, T2? item2, T3? item3, T4? item4, T5? item5, T6? item6)
		{
			var tw = new TupleWriter(ref writer);
			TuplePackers.SerializeTo(tw, item1);
			TuplePackers.SerializeTo(tw, item2);
			TuplePackers.SerializeTo(tw, item3);
			TuplePackers.SerializeTo(tw, item4);
			TuplePackers.SerializeTo(tw, item5);
			TuplePackers.SerializeTo(tw, item6);
		}

		/// <summary>Packs a 6-tuple directly into a slice</summary>
		public static void WriteKeysTo<T1, T2, T3, T4, T5, T6, T7>(ref SliceWriter writer, T1? item1, T2? item2, T3? item3, T4? item4, T5? item5, T6? item6, T7? item7)
		{
			var tw = new TupleWriter(ref writer);
			TuplePackers.SerializeTo(tw, item1);
			TuplePackers.SerializeTo(tw, item2);
			TuplePackers.SerializeTo(tw, item3);
			TuplePackers.SerializeTo(tw, item4);
			TuplePackers.SerializeTo(tw, item5);
			TuplePackers.SerializeTo(tw, item6);
			TuplePackers.SerializeTo(tw, item7);
		}

		/// <summary>Packs a 6-tuple directly into a slice</summary>
		public static void WriteKeysTo<T1, T2, T3, T4, T5, T6, T7, T8>(ref SliceWriter writer, T1? item1, T2? item2, T3? item3, T4? item4, T5? item5, T6? item6, T7? item7, T8? item8)
		{
			var tw = new TupleWriter(ref writer);
			TuplePackers.SerializeTo(tw, item1);
			TuplePackers.SerializeTo(tw, item2);
			TuplePackers.SerializeTo(tw, item3);
			TuplePackers.SerializeTo(tw, item4);
			TuplePackers.SerializeTo(tw, item5);
			TuplePackers.SerializeTo(tw, item6);
			TuplePackers.SerializeTo(tw, item7);
			TuplePackers.SerializeTo(tw, item8);
		}

		/// <summary>Merges a sequence of keys with a same prefix, all sharing the same buffer</summary>
		/// <typeparam name="T">Type of the keys</typeparam>
		/// <param name="prefix">Prefix shared by all keys</param>
		/// <param name="keys">Sequence of keys to pack</param>
		/// <returns>Array of slices (for all keys) that share the same underlying buffer</returns>
		public static Slice[] EncodeKeys<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T>(ReadOnlySpan<byte> prefix, IEnumerable<T> keys)
		{
			Contract.NotNull(keys);

			// use optimized version for arrays
			if (keys is T[] array) return EncodeKeys<T>(prefix, new ReadOnlySpan<T>(array));
			if (keys is List<T> list) return EncodeKeys<T>(prefix, CollectionsMarshal.AsSpan(list));

			var next = new List<int>(keys.TryGetNonEnumeratedCount(out var count) ? count : 0);
			var sw = new SliceWriter();
			var tw = new TupleWriter(ref sw);
			var packer = TuplePacker<T>.Encoder;

			//TODO: use multiple buffers if item count is huge ?

			bool hasPrefix = prefix.Length != 0;
			foreach (var key in keys)
			{
				if (hasPrefix) sw.WriteBytes(prefix);
				packer(tw, key);
				next.Add(sw.Position);
			}

			return Slice.SplitIntoSegments(sw.GetBufferUnsafe(), 0, next);
		}

		/// <summary>Merges an array of keys with a same prefix, all sharing the same buffer</summary>
		/// <typeparam name="T">Type of the keys</typeparam>
		/// <param name="prefix">Prefix shared by all keys</param>
		/// <param name="keys">Sequence of keys to pack</param>
		/// <returns>Array of slices (for all keys) that share the same underlying buffer</returns>
		public static Slice[] EncodeKeys<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T>(ReadOnlySpan<byte> prefix, ReadOnlySpan<T> keys)
		{
			// pre-allocate by guessing that each key will take at least 8 bytes. Even if 8 is too small, we should have at most one or two buffer resize
			var next = new List<int>(keys.Length);
			var packer = TuplePacker<T>.Encoder;
			var sw = new SliceWriter(checked(keys.Length * (prefix.Length + 8)));
			var tw = new TupleWriter(ref sw);

			//TODO: use multiple buffers if item count is huge ?

			foreach (var key in keys)
			{
				if (prefix.Length > 0) sw.WriteBytes(prefix);
				packer(tw, key);
				next.Add(sw.Position);
			}

			return Slice.SplitIntoSegments(sw.GetBufferUnsafe(), 0, next);
		}

		/// <summary>Merges an array of elements with a same prefix, all sharing the same buffer</summary>
		/// <typeparam name="TElement">Type of the elements</typeparam>
		/// <typeparam name="TKey">Type of the keys extracted from the elements</typeparam>
		/// <param name="prefix">Prefix shared by all keys (can be empty)</param>
		/// <param name="elements">Sequence of elements to pack</param>
		/// <param name="selector">Lambda that extract the key from each element</param>
		/// <returns>Array of slices (for all keys) that share the same underlying buffer</returns>
		public static Slice[] EncodeKeys<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TKey, TElement>(ReadOnlySpan<byte> prefix, ReadOnlySpan<TElement> elements, Func<TElement, TKey> selector)
		{
			Contract.NotNull(selector);

			// pre-allocate by guessing that each key will take at least 8 bytes. Even if 8 is too small, we should have at most one or two buffer resize
			var next = new List<int>(elements.Length);
			var packer = TuplePacker<TKey>.Encoder;
			var sw = new SliceWriter(checked(elements.Length * (prefix.Length + 8)));
			var tw = new TupleWriter(ref sw);

			//TODO: use multiple buffers if item count is huge ?

			foreach (var value in elements)
			{
				if (prefix.Length > 0) sw.WriteBytes(prefix);
				packer(tw, selector(value));
				next.Add(sw.Position);
			}

			return Slice.SplitIntoSegments(sw.GetBufferUnsafe(), 0, next);
		}

		#endregion

		#region Unpacking...

		[DoesNotReturn, MethodImpl(MethodImplOptions.NoInlining)]
		private static void ThrowFailedToUnpackTuple(Exception? error)
		{
			throw error ?? new InvalidOperationException("Failed to unpack tuple");
		}

		/// <summary>Unpack a tuple and only return its first element</summary>
		/// <typeparam name="T1">Type of the first value in the decoded tuple</typeparam>
		/// <param name="packedKey">Slice that should be entirely parsable as a tuple of at least 1 element</param>
		/// <param name="expectedSize">If not <see langword="null"/>, verifies that the tuple has the expected size</param>
		/// <returns>Decoded value of the first item in the tuple</returns>
		[Pure]
		public static T1? DecodeFirst<
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T1
		>
			(ReadOnlySpan<byte> packedKey, int? expectedSize)
		{
			if (packedKey.Length == 0) throw new InvalidOperationException("Cannot unpack an empty tuple");

			Span<Range> slices = stackalloc Range[1];
			if (!TuplePackers.TryUnpackFirst(packedKey, slices, expectedSize, out var error))
			{
				ThrowFailedToUnpackTuple(error);
			}

			return TuplePacker<T1>.Deserialize(packedKey[slices[0]]);
		}

		/// <summary>Unpack a tuple and only return its first 2 elements</summary>
		/// <typeparam name="T1">Type of the first value in the decoded tuple</typeparam>
		/// <typeparam name="T2">Type of the second value in the decoded tuple</typeparam>
		/// <param name="packedKey">Slice that should be entirely parsable as a tuple of at least 2 elements</param>
		/// <param name="expectedSize">If not <see langword="null"/>, verifies that the tuple has the expected size</param>
		/// <returns>Decoded values of the first two elements in the tuple</returns>
		[Pure]
		public static (T1?, T2?) DecodeFirst<
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T1,
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T2
		>
			(ReadOnlySpan<byte> packedKey, int? expectedSize)
		{
			if (packedKey.Length == 0) throw new InvalidOperationException("Cannot unpack an empty tuple");

			Span<Range> slices = stackalloc Range[2];
			if (!TuplePackers.TryUnpackFirst(packedKey, slices, expectedSize, out var error))
			{
				ThrowFailedToUnpackTuple(error);
			}

			return (
				TuplePacker<T1>.Deserialize(packedKey[slices[0]]),
				TuplePacker<T2>.Deserialize(packedKey[slices[1]])
			);
		}

		/// <summary>Unpack a tuple and only return its first 3 elements</summary>
		/// <typeparam name="T1">Type of the third value from the end of the decoded tuple</typeparam>
		/// <typeparam name="T2">Type of the second value from the end of the decoded tuple</typeparam>
		/// <typeparam name="T3">Type of the third value in the decoded tuple</typeparam>
		/// <param name="packedKey">Slice that should be entirely parsable as a tuple of at least 3 elements</param>
		/// <param name="expectedSize">If not <see langword="null"/>, verifies that the tuple has the expected size</param>
		/// <returns>Decoded values of the first three elements in the tuple</returns>
		[Pure]
		public static (T1?, T2?, T3?) DecodeFirst<
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T1,
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T2,
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T3
		>
			(ReadOnlySpan<byte> packedKey, int? expectedSize)
		{
			if (packedKey.Length == 0) throw new InvalidOperationException("Cannot unpack an empty tuple");

			Span<Range> slices = stackalloc Range[3];
			if (!TuplePackers.TryUnpackFirst(packedKey, slices, expectedSize, out var error))
			{
				ThrowFailedToUnpackTuple(error);
			}

			return (
				TuplePacker<T1>.Deserialize(packedKey[slices[0]]),
				TuplePacker<T2>.Deserialize(packedKey[slices[1]]),
				TuplePacker<T3>.Deserialize(packedKey[slices[2]])
			);
		}

		/// <summary>Unpack a tuple and only return its first 4 elements</summary>
		/// <typeparam name="T1">Type of the third value from the end of the decoded tuple</typeparam>
		/// <typeparam name="T2">Type of the second value from the end of the decoded tuple</typeparam>
		/// <typeparam name="T3">Type of the third value in the decoded tuple</typeparam>
		/// <typeparam name="T4">Type of the fourth  value in the decoded tuple</typeparam>
		/// <param name="packedKey">Slice that should be entirely parsable as a tuple of at least 4 elements</param>
		/// <param name="expectedSize">If not <see langword="null"/>, verifies that the tuple has the expected size</param>
		/// <returns>Decoded values of the first four elements in the tuple</returns>
		[Pure]
		public static (T1?, T2?, T3?, T4?) DecodeFirst<
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T1,
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T2,
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T3,
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T4
		>
			(ReadOnlySpan<byte> packedKey, int? expectedSize)
		{
			if (packedKey.Length == 0) throw new InvalidOperationException("Cannot unpack an empty tuple");

			Span<Range> slices = stackalloc Range[4];
			if (!TuplePackers.TryUnpackFirst(packedKey, slices, expectedSize, out var error))
			{
				ThrowFailedToUnpackTuple(error);
			}

			return (
				TuplePacker<T1>.Deserialize(packedKey[slices[0]]),
				TuplePacker<T2>.Deserialize(packedKey[slices[1]]),
				TuplePacker<T3>.Deserialize(packedKey[slices[2]]),
				TuplePacker<T4>.Deserialize(packedKey[slices[3]])
			);
		}

		#region DecodeLast...

		/// <summary>Unpack a tuple and only return its last element</summary>
		/// <typeparam name="T1">Type of the last value in the decoded tuple</typeparam>
		/// <param name="packedKey">Slice that should be entirely parsable as a tuple of at least one element</param>
		/// <param name="expectedSize">If not <see langword="null"/>, verifies that the tuple has the expected size</param>
		/// <returns>Decoded value of the last item in the tuple</returns>
		/// <exception cref="InvalidOperationException">If the decoded tuple does not have the expected size</exception>
		[Pure]
		public static T1? DecodeLast<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T1>
			(ReadOnlySpan<byte> packedKey, int? expectedSize)
		{
			if (packedKey.Length == 0) throw new InvalidOperationException("Cannot unpack an empty tuple");

			Span<Range> slices = stackalloc Range[1];
			if (!TuplePackers.TryUnpackLast(packedKey, slices, expectedSize, out var error))
			{
				ThrowFailedToUnpackTuple(error);
			}

			return TuplePacker<T1>.Deserialize(packedKey[slices[0]]);
		}

		/// <summary>Unpack a tuple and only return its last 2 elements</summary>
		/// <typeparam name="T1">Type of the next to last value in the decoded tuple</typeparam>
		/// <typeparam name="T2">Type of the last value in the decoded tuple</typeparam>
		/// <param name="packedKey">Slice that should be entirely parsable as a tuple of at least 2 elements</param>
		/// <param name="expectedSize">If not <see langword="null"/>, verifies that the tuple has the expected size</param>
		/// <returns>Decoded values of the last two elements in the tuple</returns>
		/// <exception cref="InvalidOperationException">If the decoded tuple does not have the expected size</exception>
		[Pure]
		public static (T1?, T2?) DecodeLast<
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T1,
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T2
		>
			(ReadOnlySpan<byte> packedKey, int? expectedSize)
		{
			if (packedKey.Length == 0) throw new InvalidOperationException("Cannot unpack an empty tuple");

			Span<Range> slices = stackalloc Range[2];
			if (!TuplePackers.TryUnpackLast(packedKey, slices, expectedSize, out var error))
			{
				ThrowFailedToUnpackTuple(error);
			}

			return (
				TuplePacker<T1>.Deserialize(packedKey[slices[0]]),
				TuplePacker<T2>.Deserialize(packedKey[slices[1]])
			);
		}

		/// <summary>Unpack a tuple and only return its last 3 elements</summary>
		/// <typeparam name="T1">Type of the third value from the end of the decoded tuple</typeparam>
		/// <typeparam name="T2">Type of the second value from the end of the decoded tuple</typeparam>
		/// <typeparam name="T3">Type of the last value in the decoded tuple</typeparam>
		/// <param name="packedKey">Slice that should be entirely parsable as a tuple of at least 3 elements</param>
		/// <param name="expectedSize">If not <see langword="null"/>, verifies that the tuple has the expected size</param>
		/// <returns>Decoded values of the last three elements in the tuple</returns>
		/// <exception cref="InvalidOperationException">If the decoded tuple does not have the expected size</exception>
		[Pure]
		public static (T1?, T2?, T3?) DecodeLast<
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T1,
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T2,
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T3
		>
			(ReadOnlySpan<byte> packedKey, int? expectedSize)
		{
			if (packedKey.Length == 0) throw new InvalidOperationException("Cannot unpack an empty tuple");

			Span<Range> slices = stackalloc Range[3];
			if (!TuplePackers.TryUnpackLast(packedKey, slices, expectedSize, out var error))
			{
				ThrowFailedToUnpackTuple(error);
			}

			return (
				TuplePacker<T1>.Deserialize(packedKey[slices[0]]),
				TuplePacker<T2>.Deserialize(packedKey[slices[1]]),
				TuplePacker<T3>.Deserialize(packedKey[slices[2]])
			);
		}

		/// <summary>Unpack a tuple and only return its last 4 elements</summary>
		/// <typeparam name="T1">Type of the fourth value from the end of the decoded tuple</typeparam>
		/// <typeparam name="T2">Type of the third value from the end of the decoded tuple</typeparam>
		/// <typeparam name="T3">Type of the second value from the end of the decoded tuple</typeparam>
		/// <typeparam name="T4">Type of the last value in the decoded tuple</typeparam>
		/// <param name="packedKey">Slice that should be entirely parsable as a tuple of at least 4 elements</param>
		/// <param name="expectedSize">If not <see langword="null"/>, verifies that the tuple has the expected size</param>
		/// <returns>Decoded values of the last four elements in the tuple</returns>
		/// <exception cref="InvalidOperationException">If the decoded tuple does not have the expected size</exception>
		[Pure]
		public static (T1?, T2?, T3?, T4?) DecodeLast<
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T1,
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T2,
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T3,
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T4
		>(ReadOnlySpan<byte> packedKey, int? expectedSize)
		{
			if (packedKey.Length == 0) throw new InvalidOperationException("Cannot unpack an empty tuple");

			Span<Range> slices = stackalloc Range[4];
			if (!TuplePackers.TryUnpackLast(packedKey, slices, expectedSize, out var error))
			{
				ThrowFailedToUnpackTuple(error);
			}

			return (
				TuplePacker<T1>.Deserialize(packedKey[slices[0]]),
				TuplePacker<T2>.Deserialize(packedKey[slices[1]]),
				TuplePacker<T3>.Deserialize(packedKey[slices[2]]),
				TuplePacker<T4>.Deserialize(packedKey[slices[3]])
			);
		}

		#endregion

		#region TryDecodeLast...

		/// <summary>Unpack a tuple and only return its last element</summary>
		/// <typeparam name="T1">Type of the last value in the decoded tuple</typeparam>
		/// <param name="packedKey">Slice that should be entirely parsable as a tuple of at least one element</param>
		/// <param name="expectedSize">If not <see langword="null"/>, verifies that the tuple has the expected size</param>
		/// <param name="item1">Receives the decoded value of the last item</param>
		/// <returns>Decoded value of the last item in the tuple</returns>
		[Pure]
		public static bool TryDecodeFirst<
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T1
		>
			(ReadOnlySpan<byte> packedKey, int? expectedSize, out T1? item1)
		{
			Contract.Debug.Requires(expectedSize is null or >= 1);

			if (packedKey.Length != 0)
			{
				Span<Range> slices = stackalloc Range[1];
				if (TuplePackers.TryUnpackFirst(packedKey, slices, expectedSize, out _))
				{
					item1 = TuplePacker<T1>.Deserialize(packedKey[slices[0]]);
					return true;
				}
			}

			item1 = default;
			return false;
		}

		/// <summary>Unpack a tuple and only return its last 2 elements</summary>
		/// <typeparam name="T1">Type of the second value from the end of the decoded tuple</typeparam>
		/// <typeparam name="T2">Type of the last value of the decoded tuple</typeparam>
		/// <param name="packedKey">Slice that should be entirely parsable as a tuple of at least 2 elements</param>
		/// <param name="expectedSize">If not <see langword="null"/>, verifies that the tuple has the expected size</param>
		/// <param name="item1">First decoded element</param>
		/// <param name="item2">Second decoded element</param>
		/// <returns>Decoded values of the last 2 elements in the tuple</returns>
		[Pure]
		public static bool TryDecodeFirst<
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T1,
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T2
		>
			(ReadOnlySpan<byte> packedKey, int? expectedSize, out T1? item1, out T2? item2)
		{
			Contract.Debug.Requires(expectedSize is null or >= 2);

			if (packedKey.Length > 0)
			{
				Span<Range> slices = stackalloc Range[2];
				if (TuplePackers.TryUnpackFirst(packedKey, slices, expectedSize, out _))
				{
					item1 = TuplePacker<T1>.Deserialize(packedKey[slices[0]]);
					item2 = TuplePacker<T2>.Deserialize(packedKey[slices[1]]);
					return true;
				}
			}

			item1 = default;
			item2 = default;
			return false;
		}

		/// <summary>Unpack a tuple and only return its last 3 elements</summary>
		/// <typeparam name="T1">Type of the third value from the end of the decoded tuple</typeparam>
		/// <typeparam name="T2">Type of the second value from the end of the decoded tuple</typeparam>
		/// <typeparam name="T3">Type of the last value of the decoded tuple</typeparam>
		/// <param name="packedKey">Slice that should be entirely parsable as a tuple of at least 3 elements</param>
		/// <param name="expectedSize">If not <see langword="null"/>, verifies that the tuple has the expected size</param>
		/// <param name="item1">First decoded element</param>
		/// <param name="item2">Second decoded element</param>
		/// <param name="item3">Third decoded element</param>
		/// <returns>Decoded values of the last 3 elements in the tuple</returns>
		[Pure]
		public static bool TryDecodeFirst<
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T1,
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T2,
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T3
		>
			(ReadOnlySpan<byte> packedKey, int? expectedSize, out T1? item1, out T2? item2, out T3? item3)
		{
			Contract.Debug.Requires(expectedSize is null or >= 3);

			if (packedKey.Length > 0)
			{
				Span<Range> slices = stackalloc Range[3];
				if (TuplePackers.TryUnpackFirst(packedKey, slices, expectedSize, out _))
				{
					item1 = TuplePacker<T1>.Deserialize(packedKey[slices[0]]);
					item2 = TuplePacker<T2>.Deserialize(packedKey[slices[1]]);
					item3 = TuplePacker<T3>.Deserialize(packedKey[slices[2]]);
					return true;
				}
			}

			item1 = default;
			item2 = default;
			item3 = default;
			return false;
		}

		/// <summary>Unpack a tuple and only return its last 4 elements</summary>
		/// <typeparam name="T1">Type of the fourth value from the end of the decoded tuple</typeparam>
		/// <typeparam name="T2">Type of the third value from the end of the decoded tuple</typeparam>
		/// <typeparam name="T3">Type of the second value from the end of the decoded tuple</typeparam>
		/// <typeparam name="T4">Type of the last value in the decoded tuple</typeparam>
		/// <param name="packedKey">Slice that should be entirely parsable as a tuple of at least 4 elements</param>
		/// <param name="expectedSize">If not <see langword="null"/>, verifies that the tuple has the expected size</param>
		/// <param name="item1">First decoded element</param>
		/// <param name="item2">Second decoded element</param>
		/// <param name="item3">Third decoded element</param>
		/// <param name="item4">Fourth decoded element</param>
		/// <returns>Decoded values of the last 4 elements in the tuple</returns>
		[Pure]
		public static bool TryDecodeFirst<
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T1,
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T2,
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T3,
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T4
		>(ReadOnlySpan<byte> packedKey, int? expectedSize, out T1? item1, out T2? item2, out T3? item3, out T4? item4)
		{
			Contract.Debug.Requires(expectedSize is null or >= 4);

			if (packedKey.Length > 0)
			{
				Span<Range> slices = stackalloc Range[4];
				if (TuplePackers.TryUnpackFirst(packedKey, slices, expectedSize, out _))
				{
					item1 = TuplePacker<T1>.Deserialize(packedKey[slices[0]]);
					item2 = TuplePacker<T2>.Deserialize(packedKey[slices[1]]);
					item3 = TuplePacker<T3>.Deserialize(packedKey[slices[2]]);
					item4 = TuplePacker<T4>.Deserialize(packedKey[slices[3]]);
					return true;
				}
			}

			item1 = default;
			item2 = default;
			item3 = default;
			item4 = default;
			return false;
		}

		#endregion

		#region TryDecodeLast...

		/// <summary>Unpack a tuple and only return its last element</summary>
		/// <typeparam name="T1">Type of the last value in the decoded tuple</typeparam>
		/// <param name="packedKey">Slice that should be entirely parsable as a tuple of at least one element</param>
		/// <param name="expectedSize">If not <see langword="null"/>, verifies that the tuple has the expected size</param>
		/// <param name="item1">Receives the decoded value of the last item</param>
		/// <returns>Decoded value of the last item in the tuple</returns>
		[Pure]
		public static bool TryDecodeLast<
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T1
		>
			(ReadOnlySpan<byte> packedKey, int? expectedSize, out T1? item1)
		{
			Contract.Debug.Requires(expectedSize is null or >= 1);

			if (packedKey.Length != 0)
			{
				Span<Range> slices = stackalloc Range[1];
				if (TuplePackers.TryUnpackLast(packedKey, slices, expectedSize, out _))
				{
					item1 = TuplePacker<T1>.Deserialize(packedKey[slices[0]]);
					return true;
				}
			}

			item1 = default;
			return false;
		}

		/// <summary>Unpack a tuple and only return its last 2 elements</summary>
		/// <typeparam name="T1">Type of the second value from the end of the decoded tuple</typeparam>
		/// <typeparam name="T2">Type of the last value of the decoded tuple</typeparam>
		/// <param name="packedKey">Slice that should be entirely parsable as a tuple of at least 2 elements</param>
		/// <param name="expectedSize">If not <see langword="null"/>, verifies that the tuple has the expected size</param>
		/// <param name="item1">First decoded element</param>
		/// <param name="item2">Second decoded element</param>
		/// <returns>Decoded values of the last 2 elements in the tuple</returns>
		[Pure]
		public static bool TryDecodeLast<
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T1,
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T2
		>
			(ReadOnlySpan<byte> packedKey, int? expectedSize, out T1? item1, out T2? item2)
		{
			Contract.Debug.Requires(expectedSize is null or >= 2);

			if (packedKey.Length > 0)
			{
				Span<Range> slices = stackalloc Range[2];
				if (TuplePackers.TryUnpackLast(packedKey, slices, expectedSize, out _))
				{
					item1 = TuplePacker<T1>.Deserialize(packedKey[slices[0]]);
					item2 = TuplePacker<T2>.Deserialize(packedKey[slices[1]]);
					return true;
				}
			}

			item1 = default;
			item2 = default;
			return false;
		}

		/// <summary>Unpack a tuple and only return its last 3 elements</summary>
		/// <typeparam name="T1">Type of the third value from the end of the decoded tuple</typeparam>
		/// <typeparam name="T2">Type of the second value from the end of the decoded tuple</typeparam>
		/// <typeparam name="T3">Type of the last value of the decoded tuple</typeparam>
		/// <param name="packedKey">Slice that should be entirely parsable as a tuple of at least 3 elements</param>
		/// <param name="expectedSize">If not <see langword="null"/>, verifies that the tuple has the expected size</param>
		/// <param name="item1">First decoded element</param>
		/// <param name="item2">Second decoded element</param>
		/// <param name="item3">Third decoded element</param>
		/// <returns>Decoded values of the last 3 elements in the tuple</returns>
		[Pure]
		public static bool TryDecodeLast<
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T1,
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T2,
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T3
		>(ReadOnlySpan<byte> packedKey, int? expectedSize, out T1? item1, out T2? item2, out T3? item3)
		{
			Contract.Debug.Requires(expectedSize is null or >= 3);

			if (packedKey.Length > 0)
			{
				Span<Range> slices = stackalloc Range[3];
				if (TuplePackers.TryUnpackLast(packedKey, slices, expectedSize, out _))
				{
					item1 = TuplePacker<T1>.Deserialize(packedKey[slices[0]]);
					item2 = TuplePacker<T2>.Deserialize(packedKey[slices[1]]);
					item3 = TuplePacker<T3>.Deserialize(packedKey[slices[2]]);
					return true;
				}
			}

			item1 = default;
			item2 = default;
			item3 = default;
			return false;
		}

		/// <summary>Unpack a tuple and only return its last 4 elements</summary>
		/// <typeparam name="T1">Type of the fourth value from the end of the decoded tuple</typeparam>
		/// <typeparam name="T2">Type of the third value from the end of the decoded tuple</typeparam>
		/// <typeparam name="T3">Type of the second value from the end of the decoded tuple</typeparam>
		/// <typeparam name="T4">Type of the last value in the decoded tuple</typeparam>
		/// <param name="packedKey">Slice that should be entirely parsable as a tuple of at least 4 elements</param>
		/// <param name="expectedSize">If not <see langword="null"/>, verifies that the tuple has the expected size</param>
		/// <param name="item1">First decoded element</param>
		/// <param name="item2">Second decoded element</param>
		/// <param name="item3">Third decoded element</param>
		/// <param name="item4">Fourth decoded element</param>
		/// <returns>Decoded values of the last 4 elements in the tuple</returns>
		[Pure]
		public static bool TryDecodeLast<
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T1,
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T2,
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T3,
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T4
		>(ReadOnlySpan<byte> packedKey, int? expectedSize, out T1? item1, out T2? item2, out T3? item3, out T4? item4)
		{
			Contract.Debug.Requires(expectedSize is null or >= 4);

			if (packedKey.Length > 0)
			{
				Span<Range> slices = stackalloc Range[4];
				if (TuplePackers.TryUnpackLast(packedKey, slices, expectedSize, out _))
				{
					item1 = TuplePacker<T1>.Deserialize(packedKey[slices[0]]);
					item2 = TuplePacker<T2>.Deserialize(packedKey[slices[1]]);
					item3 = TuplePacker<T3>.Deserialize(packedKey[slices[2]]);
					item4 = TuplePacker<T4>.Deserialize(packedKey[slices[3]]);
					return true;
				}
			}

			item1 = default;
			item2 = default;
			item3 = default;
			item4 = default;
			return false;
		}

		#endregion

		#region DecodeKey<T1>...

		/// <summary>Unpacks the value of a singleton tuple</summary>
		/// <typeparam name="T1">Type of the single value in the decoded tuple</typeparam>
		/// <param name="reader">Slice that should contain the packed representation of a tuple with a single element</param>
		/// <param name="tuple">Receives the decoded tuple</param>
		/// <remarks>Throws an exception if the tuple is empty or has more than one element.</remarks>
		public static void DecodeKey<
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T1
		>(ref TupleReader reader, out ValueTuple<T1?> tuple)
		{
			if (!TryDecodeNext(ref reader, out tuple.Item1, out var error)) throw error ?? new FormatException("Failed to decode first item");
			if (reader.HasMore) throw new FormatException("The key contains more than one item");
		}

		/// <summary>Unpacks the value of a singleton tuple</summary>
		public static void DecodeKey<
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T1
		>(ref TupleReader reader, out T1? item1)
		{
			if (!TryDecodeNext(ref reader, out item1, out var error)) throw new FormatException("Failed to decode first item", error);
			if (reader.HasMore) throw new FormatException("The key contains more than one item");
		}

		/// <summary>Unpacks the value of a singleton tuple</summary>
		/// <typeparam name="TKey">Type of the single value in the decoded tuple</typeparam>
		/// <param name="reader">Slice that should contain the packed representation of a tuple with a single element</param>
		/// <param name="key">Receives the decoded value</param>
		/// <param name="error"></param>
		/// <return>False if the tuple is empty, or has more than one element; otherwise, false.</return>
		public static bool TryDecodeKey<
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TKey
		>(ref TupleReader reader, out TKey? key, out Exception? error)
		{
			if (!TryDecodeNext(ref reader, out key, out error))
			{
				return false;
			}
			if (reader.HasMore)
			{
				return false;
			}
			return true;
		}

		#endregion

		#region DecodeKey<T1, T2>...

		/// <summary>Unpacks a key containing two elements</summary>
		/// <param name="reader">Slice that should contain the packed representation of a tuple with two elements</param>
		/// <param name="tuple">Receives the decoded tuple</param>
		/// <remarks>Throws an exception if the tuple is empty of has more than two elements.</remarks>
		public static void DecodeKey<
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T1,
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T2
		>(ref TupleReader reader, out (T1?, T2?) tuple)
		{
			if (!TryDecodeNext(ref reader, out tuple.Item1, out var error)) throw new FormatException("Failed to decode first item", error);
			if (!TryDecodeNext(ref reader, out tuple.Item2, out error)) throw new FormatException("Failed to decode second item", error);
			if (reader.HasMore) throw new FormatException("The key contains more than two items");
		}

		public static void DecodeKey<
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T1,
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T2
		>(ref TupleReader reader, out T1? item1, out T2? item2)
		{
			if (!TryDecodeNext(ref reader, out item1, out var error)) throw new FormatException("Failed to decode first item", error);
			if (!TryDecodeNext(ref reader, out item2, out error)) throw new FormatException("Failed to decode second item", error);
			if (reader.HasMore) throw new FormatException("The key contains more than two items");
		}

		#endregion

		#region DecodeKey<T1, T2, T3>...

		/// <summary>Unpacks a key containing three elements</summary>
		/// <param name="reader">Slice that should contain the packed representation of a tuple with three elements</param>
		/// <param name="tuple">Receives the decoded tuple</param>
		/// <remarks>Throws an exception if the tuple is empty of has more than three elements.</remarks>
		public static void DecodeKey<
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T1,
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T2,
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T3
		>(ref TupleReader reader, out (T1?, T2?, T3?) tuple)
		{
			if (!TryDecodeNext(ref reader, out tuple.Item1, out var error)) throw new FormatException("Failed to decode first item", error);
			if (!TryDecodeNext(ref reader, out tuple.Item2, out error)) throw new FormatException("Failed to decode second item", error);
			if (!TryDecodeNext(ref reader, out tuple.Item3, out error)) throw new FormatException("Failed to decode third item", error);
			if (reader.HasMore) throw new FormatException("The key contains more than three items");
		}

		public static void DecodeKey<
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T1,
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T2,
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T3
		>
			(ref TupleReader reader, out T1? item1, out T2? item2, out T3? item3)
		{
			if (!TryDecodeNext(ref reader, out item1, out var error)) throw new FormatException("Failed to decode first item", error);
			if (!TryDecodeNext(ref reader, out item2, out error)) throw new FormatException("Failed to decode second item", error);
			if (!TryDecodeNext(ref reader, out item3, out error)) throw new FormatException("Failed to decode third item", error);
			if (reader.HasMore) throw new FormatException("The key contains more than three items");
		}

		#endregion

		#region DecodeKey<T1, T2, T3, T4>...

		/// <summary>Unpacks a key containing four elements</summary>
		/// <param name="reader">Slice that should contain the packed representation of a tuple with four elements</param>
		/// <param name="tuple">Receives the decoded tuple</param>
		/// <remarks>Throws an exception if the tuple is empty of has more than four elements.</remarks>
		public static void DecodeKey<
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T1,
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T2,
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T3,
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T4
		>
			(ref TupleReader reader, out (T1?, T2?, T3?, T4?) tuple)
		{
			if (!TryDecodeNext(ref reader, out tuple.Item1, out var error)) throw new FormatException("Failed to decode first item", error);
			if (!TryDecodeNext(ref reader, out tuple.Item2, out error)) throw new FormatException("Failed to decode second item", error);
			if (!TryDecodeNext(ref reader, out tuple.Item3, out error)) throw new FormatException("Failed to decode third item", error);
			if (!TryDecodeNext(ref reader, out tuple.Item4, out error)) throw new FormatException("Failed to decode fourth item", error);
			if (reader.HasMore) throw new FormatException("The key contains more than four items");
		}

		public static void DecodeKey<
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T1,
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T2,
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T3,
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T4
		>(ref TupleReader reader, out T1? item1, out T2? item2, out T3? item3, out T4? item4)
		{
			if (!TryDecodeNext(ref reader, out item1, out var error)) throw new FormatException("Failed to decode first item", error);
			if (!TryDecodeNext(ref reader, out item2, out error)) throw new FormatException("Failed to decode second item", error);
			if (!TryDecodeNext(ref reader, out item3, out error)) throw new FormatException("Failed to decode third item", error);
			if (!TryDecodeNext(ref reader, out item4, out error)) throw new FormatException("Failed to decode fourth item", error);
			if (reader.HasMore) throw new FormatException("The key contains more than four items");
		}

		#endregion

		#region DecodeKey<T1, T2, T3, T4, T5>...

		/// <summary>Unpacks a key containing five elements</summary>
		/// <param name="reader">Slice that should contain the packed representation of a tuple with five elements</param>
		/// <param name="tuple">Receives the decoded tuple</param>
		/// <remarks>Throws an exception if the tuple is empty of has more than five elements.</remarks>
		public static void DecodeKey<
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T1,
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T2,
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T3,
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T4,
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T5
		>(ref TupleReader reader, out (T1?, T2?, T3?, T4?, T5?) tuple)
		{
			if (!TryDecodeNext(ref reader, out tuple.Item1, out var error)) throw new FormatException("Failed to decode first item", error);
			if (!TryDecodeNext(ref reader, out tuple.Item2, out error)) throw new FormatException("Failed to decode second item", error);
			if (!TryDecodeNext(ref reader, out tuple.Item3, out error)) throw new FormatException("Failed to decode third item", error);
			if (!TryDecodeNext(ref reader, out tuple.Item4, out error)) throw new FormatException("Failed to decode fourth item", error);
			if (!TryDecodeNext(ref reader, out tuple.Item5, out error)) throw new FormatException("Failed to decode fifth item", error);
			if (reader.HasMore) throw new FormatException("The key contains more than five items");
		}

		public static void DecodeKey<
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T1,
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T2,
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T3,
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T4,
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T5
		>
			(ref TupleReader reader, out T1? item1, out T2? item2, out T3? item3, out T4? item4, out T5? item5)
		{
			if (!TryDecodeNext(ref reader, out item1, out var error)) throw new FormatException("Failed to decode first item", error);
			if (!TryDecodeNext(ref reader, out item2, out error)) throw new FormatException("Failed to decode second item", error);
			if (!TryDecodeNext(ref reader, out item3, out error)) throw new FormatException("Failed to decode third item", error);
			if (!TryDecodeNext(ref reader, out item4, out error)) throw new FormatException("Failed to decode fourth item", error);
			if (!TryDecodeNext(ref reader, out item5, out error)) throw new FormatException("Failed to decode fifth item", error);
			if (reader.HasMore) throw new FormatException("The key contains more than five items");
		}

		#endregion

		#region DecodeKey<T1, T2, T3, T4, T5, T6>...

		/// <summary>Unpacks a key containing six elements</summary>
		/// <param name="reader">Slice that should contain the packed representation of a tuple with six elements</param>
		/// <param name="tuple">Receives the decoded tuple</param>
		/// <remarks>Throws an exception if the tuple is empty of has more than six elements.</remarks>
		public static void DecodeKey<
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T1,
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T2,
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T3,
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T4,
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T5,
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T6
		>
			(ref TupleReader reader, out (T1?, T2?, T3?, T4?, T5?, T6?) tuple)
		{
			if (!TryDecodeNext(ref reader, out tuple.Item1, out var error)) throw new FormatException("Failed to decode first item", error);
			if (!TryDecodeNext(ref reader, out tuple.Item2, out error)) throw new FormatException("Failed to decode second item", error);
			if (!TryDecodeNext(ref reader, out tuple.Item3, out error)) throw new FormatException("Failed to decode third item", error);
			if (!TryDecodeNext(ref reader, out tuple.Item4, out error)) throw new FormatException("Failed to decode fourth item", error);
			if (!TryDecodeNext(ref reader, out tuple.Item5, out error)) throw new FormatException("Failed to decode fifth item", error);
			if (!TryDecodeNext(ref reader, out tuple.Item6, out error)) throw new FormatException("Failed to decode sixth item", error);
			if (reader.HasMore) throw new FormatException("The key contains more than six items");
		}

		public static void DecodeKey<
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T1,
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T2,
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T3,
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T4,
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T5,
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T6
		>
			(ref TupleReader reader, out T1? item1, out T2? item2, out T3? item3, out T4? item4, out T5? item5, out T6? item6)
		{
			if (!TryDecodeNext(ref reader, out item1, out var error)) throw new FormatException("Failed to decode first item", error);
			if (!TryDecodeNext(ref reader, out item2, out error)) throw new FormatException("Failed to decode second item", error);
			if (!TryDecodeNext(ref reader, out item3, out error)) throw new FormatException("Failed to decode third item", error);
			if (!TryDecodeNext(ref reader, out item4, out error)) throw new FormatException("Failed to decode fourth item", error);
			if (!TryDecodeNext(ref reader, out item5, out error)) throw new FormatException("Failed to decode fifth item", error);
			if (!TryDecodeNext(ref reader, out item6, out error)) throw new FormatException("Failed to decode sixth item", error);
			if (reader.HasMore) throw new FormatException("The key contains more than six items");
		}

		#endregion

		#region DecodeKey<T1, T2, T3, T4, T5, T6, T7>...

		/// <summary>Unpacks a key containing six elements</summary>
		/// <param name="reader">Slice that should contain the packed representation of a tuple with six elements</param>
		/// <param name="tuple">Receives the decoded tuple</param>
		/// <remarks>Throws an exception if the tuple is empty of has more than six elements.</remarks>
		public static void DecodeKey<
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T1,
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T2,
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T3,
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T4,
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T5,
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T6,
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T7
		>(ref TupleReader reader, out (T1?, T2?, T3?, T4?, T5?, T6?, T7?) tuple)
		{
			if (!TryDecodeNext(ref reader, out tuple.Item1, out var error)) throw new FormatException("Failed to decode first item", error);
			if (!TryDecodeNext(ref reader, out tuple.Item2, out error)) throw new FormatException("Failed to decode second item", error);
			if (!TryDecodeNext(ref reader, out tuple.Item3, out error)) throw new FormatException("Failed to decode third item", error);
			if (!TryDecodeNext(ref reader, out tuple.Item4, out error)) throw new FormatException("Failed to decode fourth item", error);
			if (!TryDecodeNext(ref reader, out tuple.Item5, out error)) throw new FormatException("Failed to decode fifth item", error);
			if (!TryDecodeNext(ref reader, out tuple.Item6, out error)) throw new FormatException("Failed to decode sixth item", error);
			if (!TryDecodeNext(ref reader, out tuple.Item7, out error)) throw new FormatException("Failed to decode seventh item", error);
			if (reader.HasMore) throw new FormatException("The key contains more than seven items");
		}

		public static void DecodeKey<
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T1,
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T2,
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T3,
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T4,
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T5,
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T6,
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T7
		>(ref TupleReader reader, out T1? item1, out T2? item2, out T3? item3, out T4? item4, out T5? item5, out T6? item6, out T7? item7)
		{
			if (!TryDecodeNext(ref reader, out item1, out var error)) throw new FormatException("Failed to decode first item", error);
			if (!TryDecodeNext(ref reader, out item2, out error)) throw new FormatException("Failed to decode second item", error);
			if (!TryDecodeNext(ref reader, out item3, out error)) throw new FormatException("Failed to decode third item", error);
			if (!TryDecodeNext(ref reader, out item4, out error)) throw new FormatException("Failed to decode fourth item", error);
			if (!TryDecodeNext(ref reader, out item5, out error)) throw new FormatException("Failed to decode fifth item", error);
			if (!TryDecodeNext(ref reader, out item6, out error)) throw new FormatException("Failed to decode sixth item", error);
			if (!TryDecodeNext(ref reader, out item7, out error)) throw new FormatException("Failed to decode seventh item", error);
			if (reader.HasMore) throw new FormatException("The key contains more than seven items");
		}

		#endregion

		#region DecodeKey<T1, T2, T3, T4, T5, T6, T7>...

		/// <summary>Unpacks a key containing eight elements</summary>
		/// <param name="reader">Slice that should contain the packed representation of a tuple with six elements</param>
		/// <param name="tuple">Receives the decoded tuple</param>
		/// <remarks>Throws an exception if the tuple is empty of has more than six elements.</remarks>
		public static void DecodeKey<
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T1,
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T2,
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T3,
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T4,
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T5,
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T6,
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T7,
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T8
		>(ref TupleReader reader, out (T1?, T2?, T3?, T4?, T5?, T6?, T7?, T8?) tuple)
		{
			if (!TryDecodeNext(ref reader, out tuple.Item1, out var error)) throw new FormatException("Failed to decode first item", error);
			if (!TryDecodeNext(ref reader, out tuple.Item2, out error)) throw new FormatException("Failed to decode second item", error);
			if (!TryDecodeNext(ref reader, out tuple.Item3, out error)) throw new FormatException("Failed to decode third item", error);
			if (!TryDecodeNext(ref reader, out tuple.Item4, out error)) throw new FormatException("Failed to decode fourth item", error);
			if (!TryDecodeNext(ref reader, out tuple.Item5, out error)) throw new FormatException("Failed to decode fifth item", error);
			if (!TryDecodeNext(ref reader, out tuple.Item6, out error)) throw new FormatException("Failed to decode sixth item", error);
			if (!TryDecodeNext(ref reader, out tuple.Item7, out error)) throw new FormatException("Failed to decode seventh item", error);
			if (!TryDecodeNext(ref reader, out tuple.Item8, out error)) throw new FormatException("Failed to decode eight item", error);
			if (reader.HasMore) throw new FormatException("The key contains more than eight items");
		}

		public static void DecodeKey<
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T1,
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T2,
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T3,
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T4,
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T5,
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T6,
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T7,
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T8
		>
			(ref TupleReader reader, out T1? item1, out T2? item2, out T3? item3, out T4? item4, out T5? item5, out T6? item6, out T7? item7, out T8? item8)
		{
			if (!TryDecodeNext(ref reader, out item1, out var error)) throw new FormatException("Failed to decode first item", error);
			if (!TryDecodeNext(ref reader, out item2, out error)) throw new FormatException("Failed to decode second item", error);
			if (!TryDecodeNext(ref reader, out item3, out error)) throw new FormatException("Failed to decode third item", error);
			if (!TryDecodeNext(ref reader, out item4, out error)) throw new FormatException("Failed to decode fourth item", error);
			if (!TryDecodeNext(ref reader, out item5, out error)) throw new FormatException("Failed to decode fifth item", error);
			if (!TryDecodeNext(ref reader, out item6, out error)) throw new FormatException("Failed to decode sixth item", error);
			if (!TryDecodeNext(ref reader, out item7, out error)) throw new FormatException("Failed to decode seventh item", error);
			if (!TryDecodeNext(ref reader, out item8, out error)) throw new FormatException("Failed to decode eight item", error);
			if (reader.HasMore) throw new FormatException("The key contains more than eight items");
		}

		/// <summary>Unpacks a key containing eight elements</summary>
		/// <param name="reader">Slice that should contain the packed representation of a tuple with six elements</param>
		/// <param name="tuple">Receives the decoded tuple</param>
		/// <remarks>Throws an exception if the tuple is empty of has more than six elements.</remarks>
		public static void DecodeKey<
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T1,
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T2,
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T3,
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T4,
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T5,
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T6,
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T7,
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T8,
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T9
		>(ref TupleReader reader, out (T1?, T2?, T3?, T4?, T5?, T6?, T7?, T8?, T9?) tuple)
		{
			if (!TryDecodeNext(ref reader, out tuple.Item1, out var error)) throw new FormatException("Failed to decode first item", error);
			if (!TryDecodeNext(ref reader, out tuple.Item2, out error)) throw new FormatException("Failed to decode second item", error);
			if (!TryDecodeNext(ref reader, out tuple.Item3, out error)) throw new FormatException("Failed to decode third item", error);
			if (!TryDecodeNext(ref reader, out tuple.Item4, out error)) throw new FormatException("Failed to decode fourth item", error);
			if (!TryDecodeNext(ref reader, out tuple.Item5, out error)) throw new FormatException("Failed to decode fifth item", error);
			if (!TryDecodeNext(ref reader, out tuple.Item6, out error)) throw new FormatException("Failed to decode sixth item", error);
			if (!TryDecodeNext(ref reader, out tuple.Item7, out error)) throw new FormatException("Failed to decode seventh item", error);
			if (!TryDecodeNext(ref reader, out tuple.Item8, out error)) throw new FormatException("Failed to decode eight item", error);
			if (!TryDecodeNext(ref reader, out tuple.Item9, out error)) throw new FormatException("Failed to decode ninth item", error);
			if (reader.HasMore) throw new FormatException("The key contains more than nine items");
		}

		public static void DecodeKey<
				[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T1,
				[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T2,
				[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T3,
				[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T4,
				[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T5,
				[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T6,
				[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T7,
				[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T8,
				[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T9
			>
			(ref TupleReader reader, out T1? item1, out T2? item2, out T3? item3, out T4? item4, out T5? item5, out T6? item6, out T7? item7, out T8? item8, out T9? item9)
		{
			if (!TryDecodeNext(ref reader, out item1, out var error)) throw new FormatException("Failed to decode first item", error);
			if (!TryDecodeNext(ref reader, out item2, out error)) throw new FormatException("Failed to decode second item", error);
			if (!TryDecodeNext(ref reader, out item3, out error)) throw new FormatException("Failed to decode third item", error);
			if (!TryDecodeNext(ref reader, out item4, out error)) throw new FormatException("Failed to decode fourth item", error);
			if (!TryDecodeNext(ref reader, out item5, out error)) throw new FormatException("Failed to decode fifth item", error);
			if (!TryDecodeNext(ref reader, out item6, out error)) throw new FormatException("Failed to decode sixth item", error);
			if (!TryDecodeNext(ref reader, out item7, out error)) throw new FormatException("Failed to decode seventh item", error);
			if (!TryDecodeNext(ref reader, out item8, out error)) throw new FormatException("Failed to decode eight item", error);
			if (!TryDecodeNext(ref reader, out item9, out error)) throw new FormatException("Failed to decode ninth item", error);
			if (reader.HasMore) throw new FormatException("The key contains more than nine items");
		}

		#endregion

		/// <summary>Skips the next item in the tuple, and advance the cursor</summary>
		/// <param name="reader">Reader positioned at the start of the item to skip</param>
		/// <param name="error"></param>
		/// <returns><c>true</c> if an item was successfully skipped; otherwise, <c>false</c> if the tuple has reached the end or the next item is malformed.</returns>
		public static bool TrySkipNext(ref TupleReader reader, out Exception? error)
		{
			if (!reader.HasMore)
			{
				error = null;
				return false;
			}

			if (!TupleParser.TryParseNext(ref reader, out var token, out error))
			{
				if (error != null)
				{
					return false;
				}
			}
			return true;
		}

		/// <summary>Unpacks the next item in the tuple, and advance the cursor</summary>
		/// <typeparam name="T">Type of the next value in the tuple</typeparam>
		/// <param name="reader">Reader positioned at the start of the next item to read</param>
		/// <param name="value">If decoding succeeded, receives the decoded value.</param>
		/// <param name="error">If non-null, error that describes why the decoding failed</param>
		/// <returns><c>true</c> if the decoded succeeded (and <paramref name="value"/> receives the decoded value); otherwise, <c>false</c> if the tuple has reached the end or the next item is malformed.</returns>
		public static bool TryDecodeNext<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T>
			(ref TupleReader reader, out T? value, out Exception? error)
		{
			if (!reader.HasMore)
			{
				value = default;
				error = null;
				return false;
			}

			if (!TupleParser.TryParseNext(ref reader, out var token, out error))
			{
				if (error != null)
				{
					value = default;
					return false;
				}
			}

			value = TuplePacker<T>.Decoder(reader.Input[token]);
			return true;
		}

		#endregion

		#region Encoders...

		internal class Encoder<T> : IKeyEncoder<T>, IValueEncoder<T>
		{

			public static readonly Encoder<T> Default = new();

			private Encoder() { }

			public IKeyEncoding Encoding => TuPack.Encoding;

			public void WriteKeyTo(ref SliceWriter writer, T? key)
			{
				TupleEncoder.WriteKeysTo(ref writer, key);
			}

			public void ReadKeyFrom(ref SliceReader reader, out T? key)
			{
				key = !reader.HasMore
					? default //BUGBUG
					: TuPack.DecodeKey<T>(reader.ReadToEnd());
			}

			public bool TryReadKeyFrom(ref SliceReader reader, out T? key)
			{
				return TuPack.TryDecodeKey<T>(reader.ReadToEnd(), out key);
			}

			public Slice EncodeValue(T? key)
			{
				return TupleEncoder.EncodeKey(default, key);
			}

			public T? DecodeValue(Slice encoded)
			{
				if (encoded.IsNullOrEmpty) return default; //BUGBUG
				return TuPack.DecodeKey<T>(encoded);
			}

			public T? DecodeValue(ReadOnlySpan<byte> encoded)
			{
				return TuPack.DecodeKey<T>(encoded);
			}

		}

		internal class CompositeEncoder<T1, T2> : CompositeKeyEncoder<T1, T2>
		{

			public static readonly CompositeEncoder<T1, T2> Default = new();

			private CompositeEncoder() { }

			public override IKeyEncoding Encoding => TuPack.Encoding;

			public override void WriteKeyPartsTo(ref SliceWriter writer, int count, in (T1?, T2?) key)
			{
				switch (count)
				{
					case 2: WriteKeysTo(ref writer, key.Item1, key.Item2); break;
					case 1: WriteKeysTo(ref writer, key.Item1); break;
					default: throw new ArgumentOutOfRangeException(nameof(count), count, "Item count must be either 1 or 2");
				}
			}

			public override void ReadKeyPartsFrom(ref SliceReader reader, int count, out (T1?, T2?) key)
			{
				if (count != 1 & count != 2) throw new ArgumentOutOfRangeException(nameof(count), count, "Item count must be either 1 or 2");

				var t = TuPack.Unpack(reader.ReadToEnd()).OfSize(count);
				Contract.Debug.Assert(t != null);
				key.Item1 = t.Get<T1>(0);
				key.Item2 = count == 2 ? t.Get<T2>(1)! : default;
			}

			public override bool TryReadKeyPartsFrom(ref SliceReader reader, int count, out (T1?, T2?) key)
			{
				if (count != 1 & count != 2)
				{
					key = default;
					return false;
				}

				if (!TuPack.TryUnpack(reader.ReadToEnd(), out var t) || t.Count != count)
				{
					key = default;
					return false;
				}

				key.Item1 = t.Get<T1>(0);
				key.Item2 = count == 2 ? t.Get<T2>(1)! : default;
				return true;
			}

		}

		internal class CompositeEncoder<T1, T2, T3> : CompositeKeyEncoder<T1, T2, T3>
		{

			public static readonly CompositeEncoder<T1, T2, T3> Default = new();

			private CompositeEncoder() { }

			public override IKeyEncoding Encoding => TuPack.Encoding;

			public override void WriteKeyPartsTo(ref SliceWriter writer, int count, in (T1?, T2?, T3?) key)
			{
				switch (count)
				{
					case 3: WriteKeysTo(ref writer, key.Item1, key.Item2, key.Item3); break;
					case 2: WriteKeysTo(ref writer, key.Item1, key.Item2); break;
					case 1: WriteKeysTo(ref writer, key.Item1); break;
					default: throw new ArgumentOutOfRangeException(nameof(count), count, "Item count must be between 1 and 3");
				}
			}

			public override void ReadKeyPartsFrom(ref SliceReader reader, int count, out (T1?, T2?, T3?) key)
			{
				if (count < 1 | count > 3) throw new ArgumentOutOfRangeException(nameof(count), count, "Item count must be between 1 and 3");

				var t = TuPack.Unpack(reader.ReadToEnd()).OfSize(count);
				Contract.Debug.Assert(t != null);
				key.Item1 = t.Get<T1>(0);
				key.Item2 = count >= 2 ? t.Get<T2>(1) : default;
				key.Item3 = count >= 3 ? t.Get<T3>(2) : default;
			}

			public override bool TryReadKeyPartsFrom(ref SliceReader reader, int count, out (T1?, T2?, T3?) key)
			{
				if (count < 1 | count > 3)
				{
					key = default;
					return false;
				}

				if (!TuPack.TryUnpack(reader.ReadToEnd(), out var t) || t.Count != count)
				{
					key = default;
					return false;
				}

				key.Item1 = t.Get<T1>(0);
				key.Item2 = count >= 2 ? t.Get<T2>(1) : default;
				key.Item3 = count >= 3 ? t.Get<T3>(2) : default;
				return true;
			}

		}

		internal class CompositeEncoder<T1, T2, T3, T4> : CompositeKeyEncoder<T1, T2, T3, T4>
		{

			public static readonly CompositeEncoder<T1, T2, T3, T4> Default = new();

			private CompositeEncoder() { }

			public override IKeyEncoding Encoding => TuPack.Encoding;

			public override void WriteKeyPartsTo(ref SliceWriter writer, int count, in (T1?, T2?, T3?, T4?) key)
			{
				switch (count)
				{
					case 4: WriteKeysTo(ref writer, key.Item1, key.Item2, key.Item3, key.Item4); break;
					case 3: WriteKeysTo(ref writer, key.Item1, key.Item2, key.Item3); break;
					case 2: WriteKeysTo(ref writer, key.Item1, key.Item2); break;
					case 1: WriteKeysTo(ref writer, key.Item1); break;
					default: throw new ArgumentOutOfRangeException(nameof(count), count, "Item count must be between 1 and 4");
				}
			}

			public override void ReadKeyPartsFrom(ref SliceReader reader, int count, out (T1?, T2?, T3?, T4?) key)
			{
				if (count < 1 || count > 4) throw new ArgumentOutOfRangeException(nameof(count), count, "Item count must be between 1 and 4");

				var t = TuPack.Unpack(reader.ReadToEnd()).OfSize(count);
				Contract.Debug.Assert(t != null);
				key.Item1 = t.Get<T1>(0);
				key.Item2 = count >= 2 ? t.Get<T2>(1) : default;
				key.Item3 = count >= 3 ? t.Get<T3>(2) : default;
				key.Item4 = count >= 4 ? t.Get<T4>(3) : default;
			}

			public override bool TryReadKeyPartsFrom(ref SliceReader reader, int count, out (T1?, T2?, T3?, T4?) key)
			{
				if (count < 1 || count > 4)
				{
					key = default;
					return false;
				}

				if (!TuPack.TryUnpack(reader.ReadToEnd(), out var t) || t.Count != count)
				{
					key = default;
					return false;
				}

				key.Item1 = t.Get<T1>(0);
				key.Item2 = count >= 2 ? t.Get<T2>(1) : default;
				key.Item3 = count >= 3 ? t.Get<T3>(2) : default;
				key.Item4 = count >= 4 ? t.Get<T4>(3) : default;
				return true;
			}

		}

		internal class CompositeEncoder<T1, T2, T3, T4, T5> : CompositeKeyEncoder<T1, T2, T3, T4, T5>
		{

			public static readonly CompositeEncoder<T1, T2, T3, T4, T5> Default = new();

			private CompositeEncoder() { }

			public override IKeyEncoding Encoding => TuPack.Encoding;

			public override void WriteKeyPartsTo(ref SliceWriter writer, int count, in (T1?, T2?, T3?, T4?, T5?) key)
			{
				switch (count)
				{
					case 5: WriteKeysTo(ref writer, key.Item1, key.Item2, key.Item3, key.Item4, key.Item5); break;
					case 4: WriteKeysTo(ref writer, key.Item1, key.Item2, key.Item3, key.Item4); break;
					case 3: WriteKeysTo(ref writer, key.Item1, key.Item2, key.Item3); break;
					case 2: WriteKeysTo(ref writer, key.Item1, key.Item2); break;
					case 1: WriteKeysTo(ref writer, key.Item1); break;
					default: throw new ArgumentOutOfRangeException(nameof(count), count, "Item count must be between 1 and 5");
				}
			}

			public override void ReadKeyPartsFrom(ref SliceReader reader, int count, out (T1?, T2?, T3?, T4?, T5?) key)
			{
				if (count < 1 || count > 5) throw new ArgumentOutOfRangeException(nameof(count), count, "Item count must be between 1 and 5");

				var t = TuPack.Unpack(reader.ReadToEnd()).OfSize(count);
				Contract.Debug.Assert(t != null);
				key.Item1 = t.Get<T1>(0);
				key.Item2 = count >= 2 ? t.Get<T2>(1) : default;
				key.Item3 = count >= 3 ? t.Get<T3>(2) : default;
				key.Item4 = count >= 4 ? t.Get<T4>(3) : default;
				key.Item5 = count >= 5 ? t.Get<T5>(4) : default;
			}

			public override bool TryReadKeyPartsFrom(ref SliceReader reader, int count, out (T1?, T2?, T3?, T4?, T5?) key)
			{
				if (count < 1 || count > 5)
				{
					key = default;
					return false;
				}

				if (!TuPack.TryUnpack(reader.ReadToEnd(), out var t) || t.Count != count)
				{
					key = default;
					return false;
				}

				key.Item1 = t.Get<T1>(0);
				key.Item2 = count >= 2 ? t.Get<T2>(1) : default;
				key.Item3 = count >= 3 ? t.Get<T3>(2) : default;
				key.Item4 = count >= 4 ? t.Get<T4>(3) : default;
				key.Item5 = count >= 5 ? t.Get<T5>(4) : default;
				return true;
			}

		}

		internal class CompositeEncoder<T1, T2, T3, T4, T5, T6> : CompositeKeyEncoder<T1, T2, T3, T4, T5, T6>
		{

			public static readonly CompositeEncoder<T1, T2, T3, T4, T5, T6> Default = new();

			private CompositeEncoder() { }

			public override IKeyEncoding Encoding => TuPack.Encoding;

			public override void WriteKeyPartsTo(ref SliceWriter writer, int count, in (T1?, T2?, T3?, T4?, T5?, T6?) key)
			{
				switch (count)
				{
					case 6: WriteKeysTo(ref writer, key.Item1, key.Item2, key.Item3, key.Item4, key.Item5, key.Item6); break;
					case 5: WriteKeysTo(ref writer, key.Item1, key.Item2, key.Item3, key.Item4, key.Item5); break;
					case 4: WriteKeysTo(ref writer, key.Item1, key.Item2, key.Item3, key.Item4); break;
					case 3: WriteKeysTo(ref writer, key.Item1, key.Item2, key.Item3); break;
					case 2: WriteKeysTo(ref writer, key.Item1, key.Item2); break;
					case 1: WriteKeysTo(ref writer, key.Item1); break;
					default: throw new ArgumentOutOfRangeException(nameof(count), count, "Item count must be between 1 and 6");
				}
			}

			public override void ReadKeyPartsFrom(ref SliceReader reader, int count, out (T1?, T2?, T3?, T4?, T5?, T6?) key)
			{
				if (count < 1 || count > 6) throw new ArgumentOutOfRangeException(nameof(count), count, "Item count must be between 1 and 6");

				var t = TuPack.Unpack(reader.ReadToEnd()).OfSize(count);
				Contract.Debug.Assert(t != null);
				key.Item1 = t.Get<T1>(0);
				key.Item2 = count >= 2 ? t.Get<T2>(1) : default;
				key.Item3 = count >= 3 ? t.Get<T3>(2) : default;
				key.Item4 = count >= 4 ? t.Get<T4>(3) : default;
				key.Item5 = count >= 5 ? t.Get<T5>(4) : default;
				key.Item6 = count >= 6 ? t.Get<T6>(5) : default;
			}

			public override bool TryReadKeyPartsFrom(ref SliceReader reader, int count, out (T1?, T2?, T3?, T4?, T5?, T6?) key)
			{
				if (count < 1 || count > 6)
				{
					key = default;
					return false;
				}

				if (!TuPack.TryUnpack(reader.ReadToEnd(), out var t) || t.Count != count)
				{
					key = default;
					return false;
				}

				key.Item1 = t.Get<T1>(0);
				key.Item2 = count >= 2 ? t.Get<T2>(1) : default;
				key.Item3 = count >= 3 ? t.Get<T3>(2) : default;
				key.Item4 = count >= 4 ? t.Get<T4>(3) : default;
				key.Item5 = count >= 5 ? t.Get<T5>(4) : default;
				key.Item6 = count >= 6 ? t.Get<T6>(5) : default;
				return true;
			}

		}

		#endregion

	}

}
