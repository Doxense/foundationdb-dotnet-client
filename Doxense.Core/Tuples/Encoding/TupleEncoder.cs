#region Copyright (c) 2023-2024 SnowBank SAS, (c) 2005-2023 Doxense SAS
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

namespace Doxense.Collections.Tuples.Encoding
{
	using System;
	using System.Runtime.InteropServices;
	using Doxense.Collections.Tuples;
	using Doxense.Memory;
	using Doxense.Serialization.Encoders;

	/// <summary>Helper class to encode and decode tuples to and from binary buffers</summary>
	/// <remarks>This class is intended for implementors of tuples, and should not be called directly by application code!</remarks>
	public static class TupleEncoder
	{

		/// <summary>Internal helper that serializes the content of a Tuple into a TupleWriter, meant to be called by implementers of <see cref="IVarTuple"/> types.</summary>
		/// <remarks>Warning: This method will call into <see cref="ITupleSerializable.PackTo"/> if <paramref name="tuple"/> implements <see cref="ITupleSerializable"/></remarks>

		internal static void WriteTo<TTuple>(ref TupleWriter writer, TTuple tuple)
			where TTuple : IVarTuple?
		{
			Contract.Debug.Requires(tuple != null);

			// ReSharper disable once SuspiciousTypeConversion.Global
			if (tuple is ITupleSerializable ts)
			{ // optimized version
				ts.PackTo(ref writer);
				return;
			}

			int n = tuple.Count;
			// small tuples probably are faster with indexers
			//REVIEW: when should we use indexers, and when should we use foreach?
			if (n <= 4)
			{
				for (int i = 0; i < n; i++)
				{
					TuplePackers.SerializeObjectTo(ref writer, tuple[i]);
				}
			}
			else
			{
				foreach (object? item in tuple)
				{
					TuplePackers.SerializeObjectTo(ref writer, item);
				}
			}
		}

		#region Packing...

		// Without prefix

		/// <summary>Packs a tuple into a slice</summary>
		/// <param name="tuple">Tuple that must be serialized into a binary slice</param>
		[Pure]
		public static Slice Pack<TTuple>(TTuple? tuple)
			where TTuple : IVarTuple?
		{
			if (tuple == null) return Slice.Nil;
			var writer = new TupleWriter();
			WriteTo(ref writer, tuple);
			return writer.ToSlice();
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
				var tw = new TupleWriter(writer);
				WriteTo(ref tw, tuple);
				writer = tw.Output;
			}
		}

		public static void Pack<TTuple>(ref TupleWriter writer, TTuple? tuple)
			where TTuple : IVarTuple?
		{
			if (tuple != null)
			{
				WriteTo(ref writer, tuple);
			}
		}

		// With prefix

		/// <summary>Efficiently concatenates a prefix with the packed representation of a tuple</summary>
		public static Slice Pack<TTuple>(ReadOnlySpan<byte> prefix, TTuple? tuple)
			where TTuple : IVarTuple?
		{
			if (tuple == null || tuple.Count == 0) return Slice.Copy(prefix);

			var writer = new TupleWriter(checked(32 + prefix.Length));
			writer.Output.WriteBytes(prefix);
			WriteTo(ref writer, tuple);
			return writer.ToSlice();
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
			var writer = new TupleWriter(checked(tuples.Length * (16 + prefix.Length)));
			var next = new List<int>(tuples.Length);

			//TODO: use multiple buffers if item count is huge ?

			foreach (var tuple in tuples)
			{
				writer.Output.WriteBytes(prefix);
				WriteTo(ref writer, tuple);
				next.Add(writer.Output.Position);
			}

			return Slice.SplitIntoSegments(writer.Output.GetBufferUnsafe(), 0, next);
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
			var writer = new TupleWriter(checked(tuples.Length * (16 + prefix.Length)));
			var next = new List<int>(tuples.Length);

			//TODO: use multiple buffers if item count is huge ?

			foreach (var tuple in tuples)
			{
				writer.Output.WriteBytes(prefix);
				WriteTo(ref writer, tuple);
				next.Add(writer.Output.Position);
			}

			return Slice.SplitIntoSegments(writer.Output.GetBufferUnsafe(), 0, next);
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
				return Pack(prefix, (ReadOnlySpan<TTuple>) CollectionsMarshal.AsSpan(list));
			}

			var next = new List<int>((tuples as ICollection<TTuple>)?.Count ?? 0);
			var writer = new TupleWriter(checked(next.Capacity * (16 + prefix.Length)));

			//TODO: use multiple buffers if item count is huge ?

			foreach (var tuple in tuples)
			{
				writer.Output.WriteBytes(prefix);
				WriteTo(ref writer, tuple);
				next.Add(writer.Output.Position);
			}

			return Slice.SplitIntoSegments(writer.Output.GetBufferUnsafe(), 0, next);
		}

		public static Slice[] Pack<TElement, TTuple>(ReadOnlySpan<byte> prefix, TElement[] elements, Func<TElement, TTuple> transform)
			where TTuple : IVarTuple?
		{
			Contract.NotNull(elements);
			Contract.NotNull(transform);

			var next = new List<int>(elements.Length);
			var writer = new TupleWriter(checked(next.Capacity * (16 + prefix.Length)));

			//TODO: use multiple buffers if item count is huge ?

			foreach (var element in elements)
			{
				var tuple = transform(element);
				if (tuple == null)
				{
					next.Add(writer.Output.Position);
				}
				else
				{
					writer.Output.WriteBytes(prefix);
					WriteTo(ref writer, tuple);
					next.Add(writer.Output.Position);
				}
			}

			return Slice.SplitIntoSegments(writer.Output.GetBufferUnsafe(), 0, next);
		}

		public static Slice[] Pack<TElement, TTuple>(ReadOnlySpan<byte> prefix, ReadOnlySpan<TElement> elements, Func<TElement, TTuple> transform)
			where TTuple : IVarTuple?
		{
			Contract.NotNull(transform);

			var next = new List<int>(elements.Length);
			var writer = new TupleWriter(checked(next.Capacity * (16 + prefix.Length)));

			//TODO: use multiple buffers if item count is huge ?

			foreach (var element in elements)
			{
				var tuple = transform(element);
				if (tuple == null)
				{
					next.Add(writer.Output.Position);
				}
				else
				{
					writer.Output.WriteBytes(prefix);
					WriteTo(ref writer, tuple);
					next.Add(writer.Output.Position);
				}
			}

			return Slice.SplitIntoSegments(writer.Output.GetBufferUnsafe(), 0, next);
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
			var writer = new TupleWriter(checked(next.Capacity * (16 + prefix.Length)));

			//TODO: use multiple buffers if item count is huge ?

			foreach (var element in elements)
			{
				var tuple = transform(element);
				if (tuple == null)
				{
					next.Add(writer.Output.Position);
				}
				else
				{
					writer.Output.WriteBytes(prefix);
					WriteTo(ref writer, tuple);
					next.Add(writer.Output.Position);
				}
			}

			return Slice.SplitIntoSegments(writer.Output.GetBufferUnsafe(), 0, next);
		}

		// With prefix...

		/// <summary>Efficiently concatenates a prefix with the packed representation of a 1-tuple</summary>
		[Pure]
		public static Slice EncodeKey<T1>(ReadOnlySpan<byte> prefix, T1? value)
		{
			var writer = new TupleWriter();
			writer.Output.WriteBytes(prefix);
			TuplePackers.SerializeTo(ref writer, value);
			return writer.ToSlice();
		}

		/// <summary>Efficiently concatenates a prefix with the packed representation of a 1-tuple</summary>
		[Pure]
		public static Slice Pack<T1>(ReadOnlySpan<byte> prefix, in ValueTuple<T1?> items)
		{
			var writer = new TupleWriter();
			writer.Output.WriteBytes(prefix);
			TuplePackers.SerializeTo(ref writer, items.Item1);
			return writer.ToSlice();
		}

		/// <summary>Efficiently concatenates a prefix with the packed representation of a 2-tuple</summary>
		[Pure]
		public static Slice EncodeKey<T1, T2>(ReadOnlySpan<byte> prefix, T1? value1, T2? value2)
		{
			var writer = new TupleWriter();
			writer.Output.WriteBytes(prefix);
			TuplePackers.SerializeTo(ref writer, value1);
			TuplePackers.SerializeTo(ref writer, value2);
			return writer.ToSlice();
		}

		/// <summary>Efficiently concatenates a prefix with the packed representation of a 2-tuple</summary>
		[Pure]
		public static Slice Pack<T1, T2>(ReadOnlySpan<byte> prefix, in (T1?, T2?) items)
		{
			var writer = new TupleWriter();
			writer.Output.WriteBytes(prefix);
			TuplePackers.SerializeTo(ref writer, items.Item1);
			TuplePackers.SerializeTo(ref writer, items.Item2);
			return writer.ToSlice();
		}

		/// <summary>Efficiently concatenates a prefix with the packed representation of a 3-tuple</summary>
		public static Slice EncodeKey<T1, T2, T3>(ReadOnlySpan<byte> prefix, T1? value1, T2? value2, T3? value3)
		{
			var writer = new TupleWriter();
			writer.Output.WriteBytes(prefix);
			TuplePackers.SerializeTo(ref writer, value1);
			TuplePackers.SerializeTo(ref writer, value2);
			TuplePackers.SerializeTo(ref writer, value3);
			return writer.ToSlice();
		}

		/// <summary>Efficiently concatenates a prefix with the packed representation of a 3-tuple</summary>
		public static Slice Pack<T1, T2, T3>(ReadOnlySpan<byte> prefix, in (T1?, T2?, T3?) items)
		{
			var writer = new TupleWriter();
			writer.Output.WriteBytes(prefix);
			TuplePackers.SerializeTo(ref writer, items.Item1);
			TuplePackers.SerializeTo(ref writer, items.Item2);
			TuplePackers.SerializeTo(ref writer, items.Item3);
			return writer.ToSlice();
		}

		/// <summary>Efficiently concatenates a prefix with the packed representation of a 4-tuple</summary>
		public static Slice EncodeKey<T1, T2, T3, T4>(ReadOnlySpan<byte> prefix, T1? value1, T2? value2, T3? value3, T4? value4)
		{
			var writer = new TupleWriter();
			writer.Output.WriteBytes(prefix);
			TuplePackers.SerializeTo(ref writer, value1);
			TuplePackers.SerializeTo(ref writer, value2);
			TuplePackers.SerializeTo(ref writer, value3);
			TuplePackers.SerializeTo(ref writer, value4);
			return writer.ToSlice();
		}

		/// <summary>Efficiently concatenates a prefix with the packed representation of a 4-tuple</summary>
		public static Slice Pack<T1, T2, T3, T4>(ReadOnlySpan<byte> prefix, in (T1?, T2?, T3?, T4?) items)
		{
			var writer = new TupleWriter();
			writer.Output.WriteBytes(prefix);
			TuplePackers.SerializeTo(ref writer, items.Item1);
			TuplePackers.SerializeTo(ref writer, items.Item2);
			TuplePackers.SerializeTo(ref writer, items.Item3);
			TuplePackers.SerializeTo(ref writer, items.Item4);
			return writer.ToSlice();
		}

		/// <summary>Efficiently concatenates a prefix with the packed representation of a 5-tuple</summary>
		public static Slice EncodeKey<T1, T2, T3, T4, T5>(ReadOnlySpan<byte> prefix, T1? value1, T2? value2, T3? value3, T4? value4, T5? value5)
		{
			var writer = new TupleWriter();
			writer.Output.WriteBytes(prefix);
			TuplePackers.SerializeTo(ref writer, value1);
			TuplePackers.SerializeTo(ref writer, value2);
			TuplePackers.SerializeTo(ref writer, value3);
			TuplePackers.SerializeTo(ref writer, value4);
			TuplePackers.SerializeTo(ref writer, value5);
			return writer.ToSlice();
		}

		/// <summary>Efficiently concatenates a prefix with the packed representation of a 5-tuple</summary>
		public static Slice Pack<T1, T2, T3, T4, T5>(ReadOnlySpan<byte> prefix, in (T1?, T2?, T3?, T4?, T5?) items)
		{
			var writer = new TupleWriter();
			writer.Output.WriteBytes(prefix);
			TuplePackers.SerializeTo(ref writer, items.Item1);
			TuplePackers.SerializeTo(ref writer, items.Item2);
			TuplePackers.SerializeTo(ref writer, items.Item3);
			TuplePackers.SerializeTo(ref writer, items.Item4);
			TuplePackers.SerializeTo(ref writer, items.Item5);
			return writer.ToSlice();
		}

		/// <summary>Efficiently concatenates a prefix with the packed representation of a 6-tuple</summary>
		public static Slice EncodeKey<T1, T2, T3, T4, T5, T6>(ReadOnlySpan<byte> prefix, T1? value1, T2? value2, T3? value3, T4? value4, T5? value5, T6? value6)
		{
			var writer = new TupleWriter();
			writer.Output.WriteBytes(prefix);
			TuplePackers.SerializeTo(ref writer, value1);
			TuplePackers.SerializeTo(ref writer, value2);
			TuplePackers.SerializeTo(ref writer, value3);
			TuplePackers.SerializeTo(ref writer, value4);
			TuplePackers.SerializeTo(ref writer, value5);
			TuplePackers.SerializeTo(ref writer, value6);
			return writer.ToSlice();
		}

		/// <summary>Efficiently concatenates a prefix with the packed representation of a 6-tuple</summary>
		public static Slice Pack<T1, T2, T3, T4, T5, T6>(ReadOnlySpan<byte> prefix, in (T1?, T2?, T3?, T4?, T5?, T6?) items)
		{
			var writer = new TupleWriter();
			writer.Output.WriteBytes(prefix);
			TuplePackers.SerializeTo(ref writer, items.Item1);
			TuplePackers.SerializeTo(ref writer, items.Item2);
			TuplePackers.SerializeTo(ref writer, items.Item3);
			TuplePackers.SerializeTo(ref writer, items.Item4);
			TuplePackers.SerializeTo(ref writer, items.Item5);
			TuplePackers.SerializeTo(ref writer, items.Item6);
			return writer.ToSlice();
		}

		/// <summary>Efficiently concatenates a prefix with the packed representation of a 7-tuple</summary>
		public static Slice EncodeKey<T1, T2, T3, T4, T5, T6, T7>(ReadOnlySpan<byte> prefix, T1? value1, T2? value2, T3? value3, T4? value4, T5? value5, T6? value6, T7? value7)
		{
			var writer = new TupleWriter();
			writer.Output.WriteBytes(prefix);
			TuplePackers.SerializeTo(ref writer, value1);
			TuplePackers.SerializeTo(ref writer, value2);
			TuplePackers.SerializeTo(ref writer, value3);
			TuplePackers.SerializeTo(ref writer, value4);
			TuplePackers.SerializeTo(ref writer, value5);
			TuplePackers.SerializeTo(ref writer, value6);
			TuplePackers.SerializeTo(ref writer, value7);
			return writer.ToSlice();
		}

		/// <summary>Efficiently concatenates a prefix with the packed representation of a 7-tuple</summary>
		public static Slice Pack<T1, T2, T3, T4, T5, T6, T7>(ReadOnlySpan<byte> prefix, in (T1?, T2?, T3?, T4?, T5?, T6?, T7?) items)
		{
			var writer = new TupleWriter();
			writer.Output.WriteBytes(prefix);
			TuplePackers.SerializeTo(ref writer, items.Item1);
			TuplePackers.SerializeTo(ref writer, items.Item2);
			TuplePackers.SerializeTo(ref writer, items.Item3);
			TuplePackers.SerializeTo(ref writer, items.Item4);
			TuplePackers.SerializeTo(ref writer, items.Item5);
			TuplePackers.SerializeTo(ref writer, items.Item6);
			TuplePackers.SerializeTo(ref writer, items.Item7);
			return writer.ToSlice();
		}

		/// <summary>Efficiently concatenates a prefix with the packed representation of a 8-tuple</summary>
		public static Slice EncodeKey<T1, T2, T3, T4, T5, T6, T7, T8>(ReadOnlySpan<byte> prefix, T1? value1, T2? value2, T3? value3, T4? value4, T5? value5, T6? value6, T7? value7, T8? value8)
		{
			var writer = new TupleWriter();
			writer.Output.WriteBytes(prefix);
			TuplePackers.SerializeTo(ref writer, value1);
			TuplePackers.SerializeTo(ref writer, value2);
			TuplePackers.SerializeTo(ref writer, value3);
			TuplePackers.SerializeTo(ref writer, value4);
			TuplePackers.SerializeTo(ref writer, value5);
			TuplePackers.SerializeTo(ref writer, value6);
			TuplePackers.SerializeTo(ref writer, value7);
			TuplePackers.SerializeTo(ref writer, value8);
			return writer.ToSlice();
		}

		/// <summary>Efficiently concatenates a prefix with the packed representation of a 8-tuple</summary>
		public static Slice Pack<T1, T2, T3, T4, T5, T6, T7, T8>(ReadOnlySpan<byte> prefix, in (T1?, T2?, T3?, T4?, T5?, T6?, T7?, T8?) items)
		{
			var writer = new TupleWriter();
			writer.Output.WriteBytes(prefix);
			TuplePackers.SerializeTo(ref writer, items.Item1);
			TuplePackers.SerializeTo(ref writer, items.Item2);
			TuplePackers.SerializeTo(ref writer, items.Item3);
			TuplePackers.SerializeTo(ref writer, items.Item4);
			TuplePackers.SerializeTo(ref writer, items.Item5);
			TuplePackers.SerializeTo(ref writer, items.Item6);
			TuplePackers.SerializeTo(ref writer, items.Item7);
			TuplePackers.SerializeTo(ref writer, items.Item8);
			return writer.ToSlice();
		}

		// EncodeKey...

		//REVIEW: do we really ned "Key" in the name?
		// => we want to make it obvious that this is to pack ordered keys, but this could be used for anything else...
		// => EncodeValues? (may be confused with unordered encoding)
		// => EncodeItems?
		// => Encode?

		/// <summary>Packs a 1-tuple directly into a slice</summary>
		public static Slice Pack<T1>(ReadOnlySpan<byte> prefix, in STuple<T1> tuple)
		{
			var writer = new TupleWriter();
			writer.Output.WriteBytes(prefix);
			TupleSerializer<T1>.Default.PackTo(ref writer, tuple);
			return writer.ToSlice();
		}

		/// <summary>Packs a 2-tuple directly into a slice</summary>
		public static Slice Pack<T1, T2>(ReadOnlySpan<byte> prefix, in STuple<T1, T2> tuple)
		{
			var writer = new TupleWriter();
			writer.Output.WriteBytes(prefix);
			TupleSerializer<T1, T2>.Default.PackTo(ref writer, tuple);
			return writer.ToSlice();
		}

		/// <summary>Packs a 3-tuple directly into a slice</summary>
		public static Slice Pack<T1, T2, T3>(ReadOnlySpan<byte> prefix, in STuple<T1, T2, T3> tuple)
		{
			var writer = new TupleWriter();
			writer.Output.WriteBytes(prefix);
			TupleSerializer<T1, T2, T3>.Default.PackTo(ref writer, tuple);
			return writer.ToSlice();
		}

		/// <summary>Packs a 4-tuple directly into a slice</summary>
		public static Slice Pack<T1, T2, T3, T4>(ReadOnlySpan<byte> prefix, in STuple<T1, T2, T3, T4> tuple)
		{
			var writer = new TupleWriter();
			writer.Output.WriteBytes(prefix);
			TupleSerializer<T1, T2, T3, T4>.Default.PackTo(ref writer, tuple);
			return writer.ToSlice();
		}

		/// <summary>Packs a 5-tuple directly into a slice</summary>
		public static Slice Pack<T1, T2, T3, T4, T5>(ReadOnlySpan<byte> prefix, in STuple<T1, T2, T3, T4, T5> tuple)
		{
			var writer = new TupleWriter();
			writer.Output.WriteBytes(prefix);
			TupleSerializer<T1, T2, T3, T4, T5>.Default.PackTo(ref writer, tuple);
			return writer.ToSlice();
		}

		/// <summary>Packs a 6-tuple directly into a slice</summary>
		public static Slice Pack<T1, T2, T3, T4, T5, T6>(ReadOnlySpan<byte> prefix, in STuple<T1, T2, T3, T4, T5, T6> tuple)
		{
			var writer = new TupleWriter();
			writer.Output.WriteBytes(prefix);
			TupleSerializer<T1, T2, T3, T4, T5, T6>.Default.PackTo(ref writer, tuple);
			return writer.Output.ToSlice();
		}

		/// <summary>Packs a 7-tuple directly into a slice</summary>
		public static Slice Pack<T1, T2, T3, T4, T5, T6, T7>(ReadOnlySpan<byte> prefix, in STuple<T1, T2, T3, T4, T5, T6, T7> tuple)
		{
			var writer = new TupleWriter();
			writer.Output.WriteBytes(prefix);
			TupleSerializer<T1, T2, T3, T4, T5, T6, T7>.Default.PackTo(ref writer, tuple);
			return writer.Output.ToSlice();
		}

		/// <summary>Packs a 1-tuple directly into a slice</summary>
		public static void WriteKeysTo<T1>(ref SliceWriter writer, T1 item1)
		{
			var tw = new TupleWriter(writer);
			TuplePackers.SerializeTo(ref tw, item1);
			writer = tw.Output;
		}

		/// <summary>Packs a 2-tuple directly into a slice</summary>
		public static void WriteKeysTo<T1, T2>(ref SliceWriter writer, T1? item1, T2? item2)
		{
			var tw = new TupleWriter(writer);
			TuplePackers.SerializeTo(ref tw, item1);
			TuplePackers.SerializeTo(ref tw, item2);
			writer = tw.Output;
		}

		/// <summary>Packs a 3-tuple directly into a slice</summary>
		public static void WriteKeysTo<T1, T2, T3>(ref SliceWriter writer, T1? item1, T2? item2, T3? item3)
		{
			var tw = new TupleWriter(writer);
			TuplePackers.SerializeTo(ref tw, item1);
			TuplePackers.SerializeTo(ref tw, item2);
			TuplePackers.SerializeTo(ref tw, item3);
			writer = tw.Output;
		}

		/// <summary>Packs a 4-tuple directly into a slice</summary>
		public static void WriteKeysTo<T1, T2, T3, T4>(ref SliceWriter writer, T1? item1, T2? item2, T3? item3, T4? item4)
		{
			var tw = new TupleWriter(writer);
			TuplePackers.SerializeTo(ref tw, item1);
			TuplePackers.SerializeTo(ref tw, item2);
			TuplePackers.SerializeTo(ref tw, item3);
			TuplePackers.SerializeTo(ref tw, item4);
			writer = tw.Output;
		}

		/// <summary>Packs a 5-tuple directly into a slice</summary>
		public static void WriteKeysTo<T1, T2, T3, T4, T5>(ref SliceWriter writer, T1? item1, T2? item2, T3? item3, T4? item4, T5? item5)
		{
			var tw = new TupleWriter(writer);
			TuplePackers.SerializeTo(ref tw, item1);
			TuplePackers.SerializeTo(ref tw, item2);
			TuplePackers.SerializeTo(ref tw, item3);
			TuplePackers.SerializeTo(ref tw, item4);
			TuplePackers.SerializeTo(ref tw, item5);
			writer = tw.Output;
		}

		/// <summary>Packs a 6-tuple directly into a slice</summary>
		public static void WriteKeysTo<T1, T2, T3, T4, T5, T6>(ref SliceWriter writer, T1? item1, T2? item2, T3? item3, T4? item4, T5? item5, T6? item6)
		{
			var tw = new TupleWriter(writer);
			TuplePackers.SerializeTo(ref tw, item1);
			TuplePackers.SerializeTo(ref tw, item2);
			TuplePackers.SerializeTo(ref tw, item3);
			TuplePackers.SerializeTo(ref tw, item4);
			TuplePackers.SerializeTo(ref tw, item5);
			TuplePackers.SerializeTo(ref tw, item6);
			writer = tw.Output;
		}

		/// <summary>Packs a 6-tuple directly into a slice</summary>
		public static void WriteKeysTo<T1, T2, T3, T4, T5, T6, T7>(ref SliceWriter writer, T1? item1, T2? item2, T3? item3, T4? item4, T5? item5, T6? item6, T7? item7)
		{
			var tw = new TupleWriter(writer);
			TuplePackers.SerializeTo(ref tw, item1);
			TuplePackers.SerializeTo(ref tw, item2);
			TuplePackers.SerializeTo(ref tw, item3);
			TuplePackers.SerializeTo(ref tw, item4);
			TuplePackers.SerializeTo(ref tw, item5);
			TuplePackers.SerializeTo(ref tw, item6);
			TuplePackers.SerializeTo(ref tw, item7);
			writer = tw.Output;
		}

		/// <summary>Packs a 6-tuple directly into a slice</summary>
		public static void WriteKeysTo<T1, T2, T3, T4, T5, T6, T7, T8>(ref SliceWriter writer, T1? item1, T2? item2, T3? item3, T4? item4, T5? item5, T6? item6, T7? item7, T8? item8)
		{
			var tw = new TupleWriter(writer);
			TuplePackers.SerializeTo(ref tw, item1);
			TuplePackers.SerializeTo(ref tw, item2);
			TuplePackers.SerializeTo(ref tw, item3);
			TuplePackers.SerializeTo(ref tw, item4);
			TuplePackers.SerializeTo(ref tw, item5);
			TuplePackers.SerializeTo(ref tw, item6);
			TuplePackers.SerializeTo(ref tw, item7);
			TuplePackers.SerializeTo(ref tw, item8);
			writer = tw.Output;
		}

		/// <summary>Merges a sequence of keys with a same prefix, all sharing the same buffer</summary>
		/// <typeparam name="T">Type of the keys</typeparam>
		/// <param name="prefix">Prefix shared by all keys</param>
		/// <param name="keys">Sequence of keys to pack</param>
		/// <returns>Array of slices (for all keys) that share the same underlying buffer</returns>
		public static Slice[] EncodeKeys<T>(ReadOnlySpan<byte> prefix, IEnumerable<T> keys)
		{
			Contract.NotNull(keys);

			// use optimized version for arrays
			if (keys is T[] array) return EncodeKeys<T>(prefix, array);

			var next = new List<int>((keys as ICollection<T>)?.Count ?? 0);
			var writer = new TupleWriter();
			var packer = TuplePacker<T>.Encoder;

			//TODO: use multiple buffers if item count is huge ?

			bool hasPrefix = prefix.Length != 0;
			foreach (var key in keys)
			{
				if (hasPrefix) writer.Output.WriteBytes(prefix);
				packer(ref writer, key);
				next.Add(writer.Output.Position);
			}

			return Slice.SplitIntoSegments(writer.Output.GetBufferUnsafe(), 0, next);
		}

		/// <summary>Merges an array of keys with a same prefix, all sharing the same buffer</summary>
		/// <typeparam name="T">Type of the keys</typeparam>
		/// <param name="prefix">Prefix shared by all keys</param>
		/// <param name="keys">Sequence of keys to pack</param>
		/// <returns>Array of slices (for all keys) that share the same underlying buffer</returns>
		public static Slice[] EncodeKeys<T>(ReadOnlySpan<byte> prefix, T?[] keys)
		{
			Contract.NotNull(keys);

			// pre-allocate by guessing that each key will take at least 8 bytes. Even if 8 is too small, we should have at most one or two buffer resize
			var writer = new TupleWriter(checked(keys.Length * (prefix.Length + 8)));
			var next = new List<int>(keys.Length);
			var packer = TuplePacker<T>.Encoder;

			//TODO: use multiple buffers if item count is huge ?

			foreach (var key in keys)
			{
				if (prefix.Length > 0) writer.Output.WriteBytes(prefix);
				packer(ref writer, key);
				next.Add(writer.Output.Position);
			}

			return Slice.SplitIntoSegments(writer.Output.GetBufferUnsafe(), 0, next);
		}

		/// <summary>Merges an array of keys with a same prefix, all sharing the same buffer</summary>
		/// <typeparam name="T">Type of the keys</typeparam>
		/// <param name="prefix">Prefix shared by all keys</param>
		/// <param name="keys">Sequence of keys to pack</param>
		/// <returns>Array of slices (for all keys) that share the same underlying buffer</returns>
		public static Slice[] EncodeKeys<T>(ReadOnlySpan<byte> prefix, ReadOnlySpan<T> keys)
		{
			// pre-allocate by guessing that each key will take at least 8 bytes. Even if 8 is too small, we should have at most one or two buffer resize
			var writer = new TupleWriter(checked(keys.Length * (prefix.Length + 8)));
			var next = new List<int>(keys.Length);
			var packer = TuplePacker<T>.Encoder;

			//TODO: use multiple buffers if item count is huge ?

			foreach (var key in keys)
			{
				if (prefix.Length > 0) writer.Output.WriteBytes(prefix);
				packer(ref writer, key);
				next.Add(writer.Output.Position);
			}

			return Slice.SplitIntoSegments(writer.Output.GetBufferUnsafe(), 0, next);
		}

		/// <summary>Merges an array of elements, all sharing the same buffer</summary>
		/// <typeparam name="TElement">Type of the elements</typeparam>
		/// <typeparam name="TKey">Type of the keys extracted from the elements</typeparam>
		/// <param name="elements">Sequence of elements to pack</param>
		/// <param name="selector">Lambda that extract the key from each element</param>
		/// <returns>Array of slices (for all keys) that share the same underlying buffer</returns>
		public static Slice[] EncodeKeys<TKey, TElement>(TElement[] elements, Func<TElement, TKey> selector)
		{
			return EncodeKeys<TKey, TElement>(default, elements, selector);
		}

		/// <summary>Merges an array of elements with a same prefix, all sharing the same buffer</summary>
		/// <typeparam name="TElement">Type of the elements</typeparam>
		/// <typeparam name="TKey">Type of the keys extracted from the elements</typeparam>
		/// <param name="prefix">Prefix shared by all keys (can be empty)</param>
		/// <param name="elements">Sequence of elements to pack</param>
		/// <param name="selector">Lambda that extract the key from each element</param>
		/// <returns>Array of slices (for all keys) that share the same underlying buffer</returns>
		public static Slice[] EncodeKeys<TKey, TElement>(ReadOnlySpan<byte> prefix, TElement[] elements, Func<TElement, TKey> selector)
		{
			Contract.NotNull(elements);
			Contract.NotNull(selector);

			// pre-allocate by guessing that each key will take at least 8 bytes. Even if 8 is too small, we should have at most one or two buffer resize
			var writer = new TupleWriter(checked(elements.Length * (prefix.Length + 8)));
			var next = new List<int>(elements.Length);
			var packer = TuplePacker<TKey>.Encoder;

			//TODO: use multiple buffers if item count is huge ?

			foreach (var value in elements)
			{
				if (prefix.Length > 0) writer.Output.WriteBytes(prefix);
				packer(ref writer, selector(value));
				next.Add(writer.Output.Position);
			}

			return Slice.SplitIntoSegments(writer.Output.GetBufferUnsafe(), 0, next);
		}

		/// <summary>Packs a sequence of keys with a same prefix, all sharing the same buffer</summary>
		/// <typeparam name="TTuple">Type of the prefix tuple</typeparam>
		/// <typeparam name="T1">Type of the keys</typeparam>
		/// <param name="prefix">Prefix shared by all keys</param>
		/// <param name="keys">Sequence of keys to pack</param>
		/// <returns>Array of slices (for all keys) that share the same underlying buffer</returns>
		public static Slice[] EncodeKeys<TTuple, T1>(TTuple prefix, IEnumerable<T1> keys)
			where TTuple : IVarTuple?
		{
			Contract.NotNullAllowStructs(prefix);
			var head = Pack(prefix);
			return EncodeKeys<T1>(head.Span, keys);
		}

		/// <summary>Packs a sequence of keys with a same prefix, all sharing the same buffer</summary>
		/// <typeparam name="TTuple">Type of the prefix tuple</typeparam>
		/// <typeparam name="T1">Type of the keys</typeparam>
		/// <param name="prefix">Prefix shared by all keys</param>
		/// <param name="keys">Sequence of keys to pack</param>
		/// <returns>Array of slices (for all keys) that share the same underlying buffer</returns>
		public static Slice[] EncodeKeys<TTuple, T1>(TTuple prefix, T1?[] keys)
			where TTuple : IVarTuple?
		{
			Contract.NotNullAllowStructs(prefix);

			var head = Pack(prefix);
			return EncodeKeys<T1>(head.Span, keys);
		}

		/// <summary>Packs a sequence of keys with a same prefix, all sharing the same buffer</summary>
		/// <typeparam name="TTuple">Type of the prefix tuple</typeparam>
		/// <typeparam name="T1">Type of the keys</typeparam>
		/// <param name="prefix">Prefix shared by all keys</param>
		/// <param name="keys">Sequence of keys to pack</param>
		/// <returns>Array of slices (for all keys) that share the same underlying buffer</returns>
		public static Slice[] EncodeKeys<TTuple, T1>(TTuple prefix, ReadOnlySpan<T1> keys)
			where TTuple : IVarTuple?
		{
			Contract.NotNullAllowStructs(prefix);

			var head = Pack(prefix);
			return EncodeKeys<T1>(head.Span, keys);
		}

		#endregion

		#region Unpacking...

		/// <summary>Unpacks a tuple from a serialized key blob</summary>
		/// <param name="packedKey">Binary key containing a previously packed tuple</param>
		/// <returns>Unpacked tuple, or the empty tuple if the key is <see cref="Slice.Empty"/></returns>
		/// <exception cref="System.ArgumentNullException">If <paramref name="packedKey"/> is equal to <see cref="Slice.Nil"/></exception>
		public static IVarTuple Unpack(Slice packedKey)
		{
			if (packedKey.IsNull) throw new ArgumentNullException(nameof(packedKey));
			if (packedKey.Count == 0) return STuple.Empty;

			return TuplePackers.Unpack(packedKey, false);
		}

		/// <summary>Unpacks a tuple from a binary representation</summary>
		/// <param name="packedKey">Binary key containing a previously packed tuple, or <see cref="Slice.Nil"/></param>
		/// <returns>Unpacked tuple, the empty tuple if <paramref name="packedKey"/> is equal to <see cref="Slice.Empty"/>, or null if the key is <see cref="Slice.Nil"/></returns>
		public static IVarTuple? UnpackOrDefault(Slice packedKey)
		{
			if (packedKey.IsNull) return null;
			if (packedKey.Count == 0) return STuple.Empty;
			return TuplePackers.Unpack(packedKey, false);
		}

		/// <summary>Unpack a tuple and only return its first element</summary>
		/// <typeparam name="T1">Type of the first value in the decoded tuple</typeparam>
		/// <param name="packedKey">Slice that should be entirely parsable as a tuple of at least 1 element</param>
		/// <returns>Decoded value of the first item in the tuple</returns>
		[Pure]
		public static T1? DecodeFirst<T1>(ReadOnlySpan<byte> packedKey)
		{
			if (packedKey.Length == 0) throw new InvalidOperationException("Cannot unpack an empty tuple");

			Span<Range> slices = stackalloc Range[1];
			if (!TuplePackers.TryUnpackFirst(packedKey, slices, out var error))
			{
				throw error ?? new InvalidOperationException("Failed to unpack tuple");
			}

			return TuplePacker<T1>.Deserialize(packedKey[slices[0]]);
		}

		/// <summary>Unpack a tuple and only return its first 2 elements</summary>
		/// <typeparam name="T1">Type of the first value in the decoded tuple</typeparam>
		/// <typeparam name="T2">Type of the second value in the decoded tuple</typeparam>
		/// <param name="packedKey">Slice that should be entirely parsable as a tuple of at least 2 elements</param>
		/// <returns>Decoded values of the first two elements in the tuple</returns>
		[Pure]
		public static (T1?, T2?) DecodeFirst<T1, T2>(ReadOnlySpan<byte> packedKey)
		{
			if (packedKey.Length == 0) throw new InvalidOperationException("Cannot unpack an empty tuple");

			Span<Range> slices = stackalloc Range[2];
			if (!TuplePackers.TryUnpackFirst(packedKey, slices, out var error))
			{
				throw error ?? new InvalidOperationException("Failed to unpack tuple");
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
		/// <returns>Decoded values of the first three elements in the tuple</returns>
		[Pure]
		public static (T1?, T2?, T3?) DecodeFirst<T1, T2, T3>(ReadOnlySpan<byte> packedKey)
		{
			if (packedKey.Length == 0) throw new InvalidOperationException("Cannot unpack an empty tuple");

			Span<Range> slices = stackalloc Range[3];
			if (!TuplePackers.TryUnpackFirst(packedKey, slices, out var error))
			{
				throw error ?? new InvalidOperationException("Failed to unpack tuple");
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
		/// <returns>Decoded values of the first four elements in the tuple</returns>
		[Pure]
		public static (T1?, T2?, T3?, T4?) DecodeFirst<T1, T2, T3, T4>(ReadOnlySpan<byte> packedKey)
		{
			if (packedKey.Length == 0) throw new InvalidOperationException("Cannot unpack an empty tuple");

			Span<Range> slices = stackalloc Range[4];
			if (!TuplePackers.TryUnpackFirst(packedKey, slices, out var error))
			{
				throw error ?? new InvalidOperationException("Failed to unpack tuple");
			}

			return (
				TuplePacker<T1>.Deserialize(packedKey[slices[0]]),
				TuplePacker<T2>.Deserialize(packedKey[slices[1]]),
				TuplePacker<T3>.Deserialize(packedKey[slices[2]]),
				TuplePacker<T4>.Deserialize(packedKey[slices[3]])
			);
		}

		/// <summary>Unpack a tuple and only return its last element</summary>
		/// <typeparam name="T1">Type of the last value in the decoded tuple</typeparam>
		/// <param name="packedKey">Slice that should be entirely parsable as a tuple of at least one element</param>
		/// <returns>Decoded value of the last item in the tuple</returns>
		[Pure]
		public static T1? DecodeLast<T1>(ReadOnlySpan<byte> packedKey)
		{
			if (packedKey.Length == 0) throw new InvalidOperationException("Cannot unpack an empty tuple");

			Span<Range> slices = stackalloc Range[1];
			if (!TuplePackers.TryUnpackLast(packedKey, slices, out var error))
			{
				throw error ?? new InvalidOperationException("Failed to unpack tuple");
			}

			return TuplePacker<T1>.Deserialize(packedKey[slices[0]]);
		}

		/// <summary>Unpack a tuple and only return its last 2 elements</summary>
		/// <typeparam name="T1">Type of the next to last value in the decoded tuple</typeparam>
		/// <typeparam name="T2">Type of the last value in the decoded tuple</typeparam>
		/// <param name="packedKey">Slice that should be entirely parsable as a tuple of at least 2 elements</param>
		/// <returns>Decoded values of the last two elements in the tuple</returns>
		[Pure]
		public static (T1?, T2?) DecodeLast<T1, T2>(ReadOnlySpan<byte> packedKey)
		{
			if (packedKey.Length == 0) throw new InvalidOperationException("Cannot unpack an empty tuple");

			Span<Range> slices = stackalloc Range[2];
			if (!TuplePackers.TryUnpackLast(packedKey, slices, out var error))
			{
				throw error ?? new InvalidOperationException("Failed to unpack tuple");
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
		/// <returns>Decoded values of the last three elements in the tuple</returns>
		[Pure]
		public static (T1?, T2?, T3?) DecodeLast<T1, T2, T3>(ReadOnlySpan<byte> packedKey)
		{
			if (packedKey.Length == 0) throw new InvalidOperationException("Cannot unpack an empty tuple");

			Span<Range> slices = stackalloc Range[3];
			if (!TuplePackers.TryUnpackLast(packedKey, slices, out var error))
			{
				throw error ?? new InvalidOperationException("Failed to unpack tuple");
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
		/// <returns>Decoded values of the last four elements in the tuple</returns>
		[Pure]
		public static (T1?, T2?, T3?, T4?) DecodeLast<T1, T2, T3, T4>(ReadOnlySpan<byte> packedKey)
		{
			if (packedKey.Length == 0) throw new InvalidOperationException("Cannot unpack an empty tuple");

			Span<Range> slices = stackalloc Range[4];
			if (!TuplePackers.TryUnpackLast(packedKey, slices, out var error))
			{
				throw error ?? new InvalidOperationException("Failed to unpack tuple");
			}

			return (
				TuplePacker<T1>.Deserialize(packedKey[slices[0]]),
				TuplePacker<T2>.Deserialize(packedKey[slices[1]]),
				TuplePacker<T3>.Deserialize(packedKey[slices[2]]),
				TuplePacker<T4>.Deserialize(packedKey[slices[3]])
			);
		}

		/// <summary>Unpack a tuple and only return its last element</summary>
		/// <typeparam name="T1">Type of the last value in the decoded tuple</typeparam>
		/// <param name="packedKey">Slice that should be entirely parsable as a tuple of at least one element</param>
		/// <returns>Decoded value of the last item in the tuple</returns>
		[Pure]
		public static bool TryDecodeLast<T1>(ReadOnlySpan<byte> packedKey, out T1? item1)
		{
			if (packedKey.Length == 0) throw new InvalidOperationException("Cannot unpack an empty tuple");

			Span<Range> slices = stackalloc Range[1];
			if (!TuplePackers.TryUnpackLast(packedKey, slices, out _))
			{
				item1 = default;
				return false;
			}

			item1 = TuplePacker<T1>.Deserialize(packedKey[slices[0]]);
			return true;
		}

		/// <summary>Unpack a tuple and only return its last 2 elements</summary>
		/// <typeparam name="T1">Type of the second value from the end of the decoded tuple</typeparam>
		/// <typeparam name="T2">Type of the last value of the decoded tuple</typeparam>
		/// <param name="packedKey">Slice that should be entirely parsable as a tuple of at least 2 elements</param>
		/// <param name="item1">First decoded element</param>
		/// <param name="item2">Second decoded element</param>
		/// <returns>Decoded values of the last 2 elements in the tuple</returns>
		[Pure]
		public static bool TryDecodeLast<T1, T2>(ReadOnlySpan<byte> packedKey, out T1? item1, out T2? item2)
		{
			if (packedKey.Length > 0)
			{
				Span<Range> slices = stackalloc Range[3];
				if (TuplePackers.TryUnpackLast(packedKey, slices, out _))
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
		/// <param name="item1">First decoded element</param>
		/// <param name="item2">Second decoded element</param>
		/// <param name="item3">Third decoded element</param>
		/// <returns>Decoded values of the last 3 elements in the tuple</returns>
		[Pure]
		public static bool TryDecodeLast<T1, T2, T3>(ReadOnlySpan<byte> packedKey, out T1? item1, out T2? item2, out T3? item3)
		{
			if (packedKey.Length > 0)
			{
				Span<Range> slices = stackalloc Range[3];
				if (TuplePackers.TryUnpackLast(packedKey, slices, out _))
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
		/// <param name="item1">First decoded element</param>
		/// <param name="item2">Second decoded element</param>
		/// <param name="item3">Third decoded element</param>
		/// <param name="item4">Fourth decoded element</param>
		/// <returns>Decoded values of the last 4 elements in the tuple</returns>
		[Pure]
		public static bool TryDecodeLast<T1, T2, T3, T4>(ReadOnlySpan<byte> packedKey, out T1? item1, out T2? item2, out T3? item3, out T4? item4)
		{
			if (packedKey.Length > 0)
			{
				Span<Range> slices = stackalloc Range[4];
				if (TuplePackers.TryUnpackLast(packedKey, slices, out _))
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

		/// <summary>Unpacks the value of a singleton tuple</summary>
		/// <typeparam name="T1">Type of the single value in the decoded tuple</typeparam>
		/// <param name="packedKey">Slice that should contain the packed representation of a tuple with a single element</param>
		/// <param name="depth"></param>
		/// <param name="tuple">Receives the decoded tuple</param>
		/// <remarks>Throws an exception if the tuple is empty or has more than one element.</remarks>
		public static void DecodeKey<T1>(ref TupleReader reader, out ValueTuple<T1?> tuple)
		{
			if (!TryDecodeNext(ref reader, out tuple.Item1, out var error)) throw error ?? new FormatException("Failed to decode first item");
			if (reader.HasMore) throw new FormatException("The key contains more than one item");
		}

		/// <summary>Unpacks the value of a singleton tuple</summary>
		public static void DecodeKey<T1>(ref TupleReader reader, out T1? item1)
		{
			if (!TryDecodeNext(ref reader, out item1, out var error)) throw new FormatException("Failed to decode first item", error);
			if (reader.HasMore) throw new FormatException("The key contains more than one item");
		}

		/// <summary>Unpacks the value of a singleton tuple</summary>
		/// <typeparam name="T1">Type of the single value in the decoded tuple</typeparam>
		/// <param name="reader">Slice that should contain the packed representation of a tuple with a single element</param>
		/// <param name="item1">Receives the decoded value</param>
		/// <return>False if if the tuple is empty, or has more than one element; otherwise, false.</return>
		public static bool TryDecodeKey<T1>(ref TupleReader reader, out T1? item1)
		{
			if (!TryDecodeNext(ref reader, out item1, out var error))
			{
				return false;
			}
			if (reader.HasMore)
			{
				return false;
			}
			return true;
		}

		/// <summary>Unpacks a key containing two elements</summary>
		/// <param name="packedKey">Slice that should contain the packed representation of a tuple with two elements</param>
		/// <param name="tuple">Receives the decoded tuple</param>
		/// <remarks>Throws an exception if the tuple is empty of has more than two elements.</remarks>
		public static void DecodeKey<T1, T2>(ref TupleReader reader, out (T1?, T2?) tuple)
		{
			if (!TryDecodeNext(ref reader, out tuple.Item1, out var error)) throw new FormatException("Failed to decode first item", error);
			if (!TryDecodeNext(ref reader, out tuple.Item2, out error)) throw new FormatException("Failed to decode second item", error);
			if (reader.HasMore) throw new FormatException("The key contains more than two items");
		}

		public static void DecodeKey<T1, T2>(ref TupleReader reader, out T1? item1, out T2? item2)
		{
			if (!TryDecodeNext(ref reader, out item1, out var error)) throw new FormatException("Failed to decode first item", error);
			if (!TryDecodeNext(ref reader, out item2, out error)) throw new FormatException("Failed to decode second item", error);
			if (reader.HasMore) throw new FormatException("The key contains more than two items");
		}

		/// <summary>Unpacks a key containing three elements</summary>
		/// <param name="packedKey">Slice that should contain the packed representation of a tuple with three elements</param>
		/// <param name="tuple">Receives the decoded tuple</param>
		/// <remarks>Throws an exception if the tuple is empty of has more than three elements.</remarks>
		public static void DecodeKey<T1, T2, T3>(ref TupleReader reader, out (T1?, T2?, T3?) tuple)
		{
			if (!TryDecodeNext(ref reader, out tuple.Item1, out var error)) throw new FormatException("Failed to decode first item", error);
			if (!TryDecodeNext(ref reader, out tuple.Item2, out error)) throw new FormatException("Failed to decode second item", error);
			if (!TryDecodeNext(ref reader, out tuple.Item3, out error)) throw new FormatException("Failed to decode third item", error);
			if (reader.HasMore) throw new FormatException("The key contains more than three items");
		}

		public static void DecodeKey<T1, T2, T3>(ref TupleReader reader, out T1? item1, out T2? item2, out T3? item3)
		{
			if (!TryDecodeNext(ref reader, out item1, out var error)) throw new FormatException("Failed to decode first item", error);
			if (!TryDecodeNext(ref reader, out item2, out error)) throw new FormatException("Failed to decode second item", error);
			if (!TryDecodeNext(ref reader, out item3, out error)) throw new FormatException("Failed to decode third item", error);
			if (reader.HasMore) throw new FormatException("The key contains more than three items");
		}

		/// <summary>Unpacks a key containing four elements</summary>
		/// <param name="packedKey">Slice that should contain the packed representation of a tuple with four elements</param>
		/// <param name="tuple">Receives the decoded tuple</param>
		/// <remarks>Throws an exception if the tuple is empty of has more than four elements.</remarks>
		public static void DecodeKey<T1, T2, T3, T4>(ref TupleReader reader, out (T1?, T2?, T3?, T4?) tuple)
		{
			if (!TryDecodeNext(ref reader, out tuple.Item1, out var error)) throw new FormatException("Failed to decode first item", error);
			if (!TryDecodeNext(ref reader, out tuple.Item2, out error)) throw new FormatException("Failed to decode second item", error);
			if (!TryDecodeNext(ref reader, out tuple.Item3, out error)) throw new FormatException("Failed to decode third item", error);
			if (!TryDecodeNext(ref reader, out tuple.Item4, out error)) throw new FormatException("Failed to decode fourth item", error);
			if (reader.HasMore) throw new FormatException("The key contains more than four items");
		}

		public static void DecodeKey<T1, T2, T3, T4>(ref TupleReader reader, out T1? item1, out T2? item2, out T3? item3, out T4? item4)
		{
			if (!TryDecodeNext(ref reader, out item1, out var error)) throw new FormatException("Failed to decode first item", error);
			if (!TryDecodeNext(ref reader, out item2, out error)) throw new FormatException("Failed to decode second item", error);
			if (!TryDecodeNext(ref reader, out item3, out error)) throw new FormatException("Failed to decode third item", error);
			if (!TryDecodeNext(ref reader, out item4, out error)) throw new FormatException("Failed to decode fourth item", error);
			if (reader.HasMore) throw new FormatException("The key contains more than four items");
		}

		/// <summary>Unpacks a key containing five elements</summary>
		/// <param name="packedKey">Slice that should contain the packed representation of a tuple with five elements</param>
		/// <param name="tuple">Receives the decoded tuple</param>
		/// <remarks>Throws an exception if the tuple is empty of has more than five elements.</remarks>
		public static void DecodeKey<T1, T2, T3, T4, T5>(ref TupleReader reader, out (T1?, T2?, T3?, T4?, T5?) tuple)
		{
			if (!TryDecodeNext(ref reader, out tuple.Item1, out var error)) throw new FormatException("Failed to decode first item", error);
			if (!TryDecodeNext(ref reader, out tuple.Item2, out error)) throw new FormatException("Failed to decode second item", error);
			if (!TryDecodeNext(ref reader, out tuple.Item3, out error)) throw new FormatException("Failed to decode third item", error);
			if (!TryDecodeNext(ref reader, out tuple.Item4, out error)) throw new FormatException("Failed to decode fourth item", error);
			if (!TryDecodeNext(ref reader, out tuple.Item5, out error)) throw new FormatException("Failed to decode fifth item", error);
			if (reader.HasMore) throw new FormatException("The key contains more than five items");
		}

		public static void DecodeKey<T1, T2, T3, T4, T5>(ref TupleReader reader, out T1? item1, out T2? item2, out T3? item3, out T4? item4, out T5? item5)
		{
			if (!TryDecodeNext(ref reader, out item1, out var error)) throw new FormatException("Failed to decode first item", error);
			if (!TryDecodeNext(ref reader, out item2, out error)) throw new FormatException("Failed to decode second item", error);
			if (!TryDecodeNext(ref reader, out item3, out error)) throw new FormatException("Failed to decode third item", error);
			if (!TryDecodeNext(ref reader, out item4, out error)) throw new FormatException("Failed to decode fourth item", error);
			if (!TryDecodeNext(ref reader, out item5, out error)) throw new FormatException("Failed to decode fifth item", error);
			if (reader.HasMore) throw new FormatException("The key contains more than five items");
		}

		/// <summary>Unpacks a key containing six elements</summary>
		/// <param name="packedKey">Slice that should contain the packed representation of a tuple with six elements</param>
		/// <param name="tuple">Receives the decoded tuple</param>
		/// <remarks>Throws an exception if the tuple is empty of has more than six elements.</remarks>
		public static void DecodeKey<T1, T2, T3, T4, T5, T6>(ref TupleReader reader, out (T1?, T2?, T3?, T4?, T5?, T6?) tuple)
		{
			if (!TryDecodeNext(ref reader, out tuple.Item1, out var error)) throw new FormatException("Failed to decode first item", error);
			if (!TryDecodeNext(ref reader, out tuple.Item2, out error)) throw new FormatException("Failed to decode second item", error);
			if (!TryDecodeNext(ref reader, out tuple.Item3, out error)) throw new FormatException("Failed to decode third item", error);
			if (!TryDecodeNext(ref reader, out tuple.Item4, out error)) throw new FormatException("Failed to decode fourth item", error);
			if (!TryDecodeNext(ref reader, out tuple.Item5, out error)) throw new FormatException("Failed to decode fifth item", error);
			if (!TryDecodeNext(ref reader, out tuple.Item6, out error)) throw new FormatException("Failed to decode sixth item", error);
			if (reader.HasMore) throw new FormatException("The key contains more than six items");
		}

		public static void DecodeKey<T1, T2, T3, T4, T5, T6>(ref TupleReader reader, out T1? item1, out T2? item2, out T3? item3, out T4? item4, out T5? item5, out T6? item6)
		{
			if (!TryDecodeNext(ref reader, out item1, out var error)) throw new FormatException("Failed to decode first item", error);
			if (!TryDecodeNext(ref reader, out item2, out error)) throw new FormatException("Failed to decode second item", error);
			if (!TryDecodeNext(ref reader, out item3, out error)) throw new FormatException("Failed to decode third item", error);
			if (!TryDecodeNext(ref reader, out item4, out error)) throw new FormatException("Failed to decode fourth item", error);
			if (!TryDecodeNext(ref reader, out item5, out error)) throw new FormatException("Failed to decode fifth item", error);
			if (!TryDecodeNext(ref reader, out item6, out error)) throw new FormatException("Failed to decode sixth item", error);
			if (reader.HasMore) throw new FormatException("The key contains more than six items");
		}

		/// <summary>Unpacks a key containing six elements</summary>
		/// <param name="packedKey">Slice that should contain the packed representation of a tuple with six elements</param>
		/// <param name="tuple">Receives the decoded tuple</param>
		/// <remarks>Throws an exception if the tuple is empty of has more than six elements.</remarks>
		public static void DecodeKey<T1, T2, T3, T4, T5, T6, T7>(ref TupleReader reader, out (T1?, T2?, T3?, T4?, T5?, T6?, T7?) tuple)
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

		public static void DecodeKey<T1, T2, T3, T4, T5, T6, T7>(ref TupleReader reader, out T1? item1, out T2? item2, out T3? item3, out T4? item4, out T5? item5, out T6? item6, out T7? item7)
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

		/// <summary>Unpacks a key containing eight elements</summary>
		/// <param name="packedKey">Slice that should contain the packed representation of a tuple with six elements</param>
		/// <param name="tuple">Receives the decoded tuple</param>
		/// <remarks>Throws an exception if the tuple is empty of has more than six elements.</remarks>
		public static void DecodeKey<T1, T2, T3, T4, T5, T6, T7, T8>(ref TupleReader reader, out (T1?, T2?, T3?, T4?, T5?, T6?, T7?, T8?) tuple)
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

		public static void DecodeKey<T1, T2, T3, T4, T5, T6, T7, T8>(ref TupleReader reader, out T1? item1, out T2? item2, out T3? item3, out T4? item4, out T5? item5, out T6? item6, out T7? item7, out T8? item8)
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

		/// <summary>Unpacks the next item in the tuple, and advance the cursor</summary>
		/// <typeparam name="T">Type of the next value in the tuple</typeparam>
		/// <param name="reader">Reader positioned at the start of the next item to read</param>
		/// <param name="value">If decoding succeeded, receives the decoded value.</param>
		/// <returns>True if the decoded succeeded (and <paramref name="value"/> receives the decoded value). False if the tuple has reached the end.</returns>
		public static bool TryDecodeNext<T>(ref TupleReader reader, out T? value, out Exception? error)
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
