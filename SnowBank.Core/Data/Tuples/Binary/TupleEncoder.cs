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
	using System.Buffers;
	using System.Runtime.InteropServices;
	using System.Text;
	using SnowBank.Data.Tuples;
	using SnowBank.Data.Binary;
	using SnowBank.Buffers;

	/// <summary>Helper class to encode and decode tuples to and from binary buffers</summary>
	/// <remarks>This class is intended for implementors of tuples, and should not be called directly by application code!</remarks>
	public static class TupleEncoder
	{

		/// <summary>Internal helper that serializes the content of a Tuple into a TupleWriter, meant to be called by implementers of <see cref="IVarTuple"/> types.</summary>
		/// <remarks>Warning: This method will call into <see cref="ITuplePackable.PackTo"/> if <paramref name="tuple"/> implements <see cref="ITuplePackable"/></remarks>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		internal static void WriteTo<TTuple>(TupleWriter writer, in TTuple? tuple)
			where TTuple : IVarTuple?
		{
			if (tuple is null)
			{
				return;
			}

			// <JIT_HACK>: if TTuple is a struct, this should be optimized by the JIT, and *hopefully* inlined into the caller!
#if DEBUG
			// but this is suboptimal in Debug builds, so we use a simple pattern here (boxing is unavoidable)
			if (tuple is ITuplePackable packable)
			{
				packable.PackTo(writer);
				return;
			}
#else
			if (typeof(TTuple).IsAssignableTo(typeof(ITuplePackable)))
			{
				((ITuplePackable) tuple).PackTo(writer);
				return;
			}
#endif
			// </JIT_HACK>

			WriteToSlow(writer, in tuple);

			[MethodImpl(MethodImplOptions.NoInlining)]
			static void WriteToSlow(TupleWriter writer, in TTuple tuple)
			{
				if (tuple!.Count != 0)
				{
					foreach (object? item in tuple)
					{
						TuplePackers.SerializeObjectTo(writer, item);
					}
				}
			}
		}

		[MustUseReturnValue, MethodImpl(MethodImplOptions.AggressiveInlining)]
		internal static bool TryWriteTo<TTuple>(ref TupleSpanWriter writer, in TTuple tuple)
			where TTuple : IVarTuple?
		{
			if (tuple is null)
			{
				return true;
			}

			// <JIT_HACK>
			// - if TTuple is a struct, this should be optimized by the JIT, and *hopefully* inlined into the caller!
#if DEBUG
			// but this is suboptimal in Debug builds, so we use a simple pattern here (boxing is unavoidable)
			if (tuple is ITupleSpanPackable ts)
			{ // optimized version
				return ts.TryPackTo(ref writer);
			}
#else
			if (typeof(TTuple).IsAssignableTo(typeof(ITupleSpanPackable)))
			{
				return ((ITupleSpanPackable) tuple).TryPackTo(ref writer);
			}
#endif
			// </JIT_HACK>

			return TryWriteToSlow(ref writer, in tuple);

			[MethodImpl(MethodImplOptions.NoInlining)]
			static bool TryWriteToSlow(ref TupleSpanWriter writer, in TTuple tuple)
			{
				if (tuple!.Count != 0)
				{
					foreach (object? item in tuple)
					{
						if (!TuplePackers.TrySerializeObjectTo(ref writer, item))
						{
							return false;
						}
					}
				}

				return true;
			}
		}

		#region Packing...

		// Without prefix

		/// <summary>Packs a tuple into a slice</summary>
		[MustUseReturnValue]
		public static bool TryPackTo<TTuple>(Span<byte> destination, out int bytesWritten, in TTuple? tuple)
			where TTuple : IVarTuple?
		{
			if (tuple is null)
			{
				bytesWritten = 0;
				return true;
			}

			var tw = new TupleSpanWriter(destination, 0);
			if (!TryWriteTo(ref tw, in tuple))
			{
				bytesWritten = 0;
				return false;
			}

			bytesWritten = tw.BytesWritten;
			return true;
		}

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

		/// <summary>Packs a tuple into a slice</summary>
		/// <param name="tuple">Tuple that must be serialized into a binary slice</param>
		/// <param name="pool">Pool used to rent the buffers</param>
		[Pure]
		public static SliceOwner Pack<TTuple>(in TTuple? tuple, ArrayPool<byte> pool)
			where TTuple : IVarTuple?
		{
			if (tuple is null) return SliceOwner.Nil;

			var sw = new SliceWriter(pool);
			var tw = new TupleWriter(ref sw);
			WriteTo(tw, in tuple);
			return sw.ToSliceOwner();
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

		/// <summary>Efficiently concatenates a prefix with the packed representation of a tuple</summary>
		public static bool TryPackTo<TTuple>(Span<byte> destination, out int bytesWritten, ReadOnlySpan<byte> prefix, TTuple? tuple)
			where TTuple : IVarTuple?
		{
			var tw = new TupleSpanWriter(destination, 0);
			if (!tw.TryWriteLiteral(prefix) || !TryWriteTo(ref tw, in tuple))
			{
				bytesWritten = 0;
				return false;
			}

			bytesWritten = tw.BytesWritten;
			return true;
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
			return Pack(prefix, tuples.AsSpan());
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

			for (int i = 0; i < tuples.Length; i++)
			{
				sw.WriteBytes(prefix);
				WriteTo(tw, in tuples[i]);
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

			if (tuples.TryGetSpan(out var span))
			{
				return Pack(prefix, span);
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

		/// <summary>Pack a 1-tuple directly into a destination buffer</summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool TryEncodeKey<T1>(Span<byte> destination, out int bytesWritten, ReadOnlySpan<byte> prefix, T1 item1)
		{
			var tw = new TupleSpanWriter(destination, 0);
			if (!tw.TryWriteLiteral(prefix) || !TuplePackers.TrySerializeTo(ref tw, item1))
			{
				bytesWritten = 0;
				return false;
			}
			bytesWritten = tw.BytesWritten;
			return true;
		}

		/// <summary>Efficiently concatenates a prefix with the packed representation of a 1-tuple</summary>
		[Pure]
		public static bool TryPackTo<T1>(Span<byte> destination, out int bytesWritten, ReadOnlySpan<byte> prefix, in STuple<T1> items)
		{
			var tw = new TupleSpanWriter(destination, 0);
			if (!tw.TryWriteLiteral(prefix) || !TuplePackers.TrySerializeTo(ref tw, items.Item1))
			{
				bytesWritten = 0;
				return false;
			}

			bytesWritten = tw.BytesWritten;
			return true;
		}

		/// <summary>Efficiently concatenates a prefix with the packed representation of a 1-tuple</summary>
		[Pure]
		public static bool TryPackTo<T1>(Span<byte> destination, out int bytesWritten, ReadOnlySpan<byte> prefix, in ValueTuple<T1> items)
		{
			var tw = new TupleSpanWriter(destination, 0);
			if (!tw.TryWriteLiteral(prefix) || !TuplePackers.TrySerializeTo(ref tw, items.Item1))
			{
				bytesWritten = 0;
				return false;
			}

			bytesWritten = tw.BytesWritten;
			return true;
		}

		/// <summary>Efficiently concatenates a prefix with the packed representation of a 1-tuple</summary>
		[Pure]
		public static Slice Pack<T1>(ReadOnlySpan<byte> prefix, in ValueTuple<T1> items)
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

		/// <summary>Pack a 2-tuple directly into a destination buffer</summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool TryEncodeKey<T1, T2>(Span<byte> destination, out int bytesWritten, ReadOnlySpan<byte> prefix, T1 item1, T2 item2)
		{
			var tw = new TupleSpanWriter(destination, 0);
			if (!tw.TryWriteLiteral(prefix) || !TuplePackers.TrySerializeTo(ref tw, item1) || !TuplePackers.TrySerializeTo(ref tw, item2))
			{
				bytesWritten = 0;
				return false;
			}
			bytesWritten = tw.BytesWritten;
			return true;
		}

		/// <summary>Efficiently concatenates a prefix with the packed representation of a 2-tuple</summary>
		[Pure]
		public static bool TryPackTo<T1, T2>(Span<byte> destination, out int bytesWritten, ReadOnlySpan<byte> prefix, in STuple<T1, T2> items)
		{
			var tw = new TupleSpanWriter(destination, 0);
			if (!tw.TryWriteLiteral(prefix)
			 || !TuplePackers.TrySerializeTo(ref tw, items.Item1)
			 || !TuplePackers.TrySerializeTo(ref tw, items.Item2))
			{
				bytesWritten = 0;
				return false;
			}

			bytesWritten = tw.BytesWritten;
			return true;
		}

		/// <summary>Efficiently concatenates a prefix with the packed representation of a 2-tuple</summary>
		[Pure]
		public static bool TryPackTo<T1, T2>(Span<byte> destination, out int bytesWritten, ReadOnlySpan<byte> prefix, in (T1, T2) items)
		{
			var tw = new TupleSpanWriter(destination, 0);
			if (!tw.TryWriteLiteral(prefix)
			 || !TuplePackers.TrySerializeTo(ref tw, items.Item1)
			 || !TuplePackers.TrySerializeTo(ref tw, items.Item2))
			{
				bytesWritten = 0;
				return false;
			}

			bytesWritten = tw.BytesWritten;
			return true;
		}

		/// <summary>Efficiently concatenates a prefix with the packed representation of a 2-tuple</summary>
		[Pure]
		public static Slice Pack<T1, T2>(ReadOnlySpan<byte> prefix, in (T1, T2) items)
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

		/// <summary>Pack a 3-tuple directly into a destination buffer</summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool TryEncodeKey<T1, T2, T3>(Span<byte> destination, out int bytesWritten, ReadOnlySpan<byte> prefix, T1 item1, T2 item2, T3 item3)
		{
			var tw = new TupleSpanWriter(destination, 0);
			if (!tw.TryWriteLiteral(prefix) || !TryWriteKeysTo(ref tw, item1, item2, item3))
			{
				bytesWritten = 0;
				return false;
			}
			bytesWritten = tw.BytesWritten;
			return true;
		}

		/// <summary>Efficiently concatenates a prefix with the packed representation of a 3-tuple</summary>
		[Pure]
		public static bool TryPackTo<T1, T2, T3>(Span<byte> destination, out int bytesWritten, ReadOnlySpan<byte> prefix, in STuple<T1, T2, T3> items)
		{
			var tw = new TupleSpanWriter(destination, 0);
			if (!tw.TryWriteLiteral(prefix)
			 || !TuplePackers.TrySerializeTo(ref tw, items.Item1)
			 || !TuplePackers.TrySerializeTo(ref tw, items.Item2)
			 || !TuplePackers.TrySerializeTo(ref tw, items.Item3))
			{
				bytesWritten = 0;
				return false;
			}

			bytesWritten = tw.BytesWritten;
			return true;
		}

		/// <summary>Efficiently concatenates a prefix with the packed representation of a 3-tuple</summary>
		[Pure]
		public static bool TryPackTo<T1, T2, T3>(Span<byte> destination, out int bytesWritten, ReadOnlySpan<byte> prefix, in (T1, T2, T3) items)
		{
			var tw = new TupleSpanWriter(destination, 0);
			if (!tw.TryWriteLiteral(prefix)
			 || !TuplePackers.TrySerializeTo(ref tw, items.Item1)
			 || !TuplePackers.TrySerializeTo(ref tw, items.Item2)
			 || !TuplePackers.TrySerializeTo(ref tw, items.Item3))
			{
				bytesWritten = 0;
				return false;
			}

			bytesWritten = tw.BytesWritten;
			return true;
		}

		/// <summary>Efficiently concatenates a prefix with the packed representation of a 3-tuple</summary>
		public static Slice Pack<T1, T2, T3>(ReadOnlySpan<byte> prefix, in (T1, T2, T3) items)
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

		/// <summary>Pack a 4-tuple directly into a destination buffer</summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool TryEncodeKey<T1, T2, T3, T4>(Span<byte> destination, out int bytesWritten, ReadOnlySpan<byte> prefix, T1 item1, T2 item2, T3 item3, T4 item4)
		{
			var tw = new TupleSpanWriter(destination, 0);
			if (!tw.TryWriteLiteral(prefix) || !TryWriteKeysTo(ref tw, item1, item2, item3, item4))
			{
				bytesWritten = 0;
				return false;
			}
			bytesWritten = tw.BytesWritten;
			return true;
		}

		/// <summary>Efficiently concatenates a prefix with the packed representation of a 4-tuple</summary>
		[Pure]
		public static bool TryPackTo<T1, T2, T3, T4>(Span<byte> destination, out int bytesWritten, ReadOnlySpan<byte> prefix, in STuple<T1, T2, T3, T4> items)
		{
			var tw = new TupleSpanWriter(destination, 0);
			if (!tw.TryWriteLiteral(prefix)
			 || !TuplePackers.TrySerializeTo(ref tw, items.Item1)
			 || !TuplePackers.TrySerializeTo(ref tw, items.Item2)
			 || !TuplePackers.TrySerializeTo(ref tw, items.Item3)
			 || !TuplePackers.TrySerializeTo(ref tw, items.Item4))
			{
				bytesWritten = 0;
				return false;
			}

			bytesWritten = tw.BytesWritten;
			return true;
		}

		/// <summary>Efficiently concatenates a prefix with the packed representation of a 4-tuple</summary>
		[Pure]
		public static bool TryPackTo<T1, T2, T3, T4>(Span<byte> destination, out int bytesWritten, ReadOnlySpan<byte> prefix, in (T1, T2, T3, T4) items)
		{
			var tw = new TupleSpanWriter(destination, 0);
			if (!tw.TryWriteLiteral(prefix)
			 || !TuplePackers.TrySerializeTo(ref tw, items.Item1)
			 || !TuplePackers.TrySerializeTo(ref tw, items.Item2)
			 || !TuplePackers.TrySerializeTo(ref tw, items.Item3)
			 || !TuplePackers.TrySerializeTo(ref tw, items.Item4))
			{
				bytesWritten = 0;
				return false;
			}

			bytesWritten = tw.BytesWritten;
			return true;
		}

		/// <summary>Efficiently concatenates a prefix with the packed representation of a 4-tuple</summary>
		public static Slice Pack<T1, T2, T3, T4>(ReadOnlySpan<byte> prefix, in (T1, T2, T3, T4) items)
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

		/// <summary>Pack a 5-tuple directly into a destination buffer</summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool TryEncodeKey<T1, T2, T3, T4, T5>(Span<byte> destination, out int bytesWritten, ReadOnlySpan<byte> prefix, T1 item1, T2 item2, T3 item3, T4 item4, T5 item5)
		{
			var tw = new TupleSpanWriter(destination, 0);
			if (!tw.TryWriteLiteral(prefix) || !TryWriteKeysTo(ref tw, item1, item2, item3, item4, item5))
			{
				bytesWritten = 0;
				return false;
			}
			bytesWritten = tw.BytesWritten;
			return true;
		}

		/// <summary>Efficiently concatenates a prefix with the packed representation of a 5-tuple</summary>
		[Pure]
		public static bool TryPackTo<T1, T2, T3, T4, T5>(Span<byte> destination, out int bytesWritten, ReadOnlySpan<byte> prefix, in STuple<T1, T2, T3, T4, T5> items)
		{
			var tw = new TupleSpanWriter(destination, 0);
			if (!tw.TryWriteLiteral(prefix)
			 || !TuplePackers.TrySerializeTo(ref tw, items.Item1)
			 || !TuplePackers.TrySerializeTo(ref tw, items.Item2)
			 || !TuplePackers.TrySerializeTo(ref tw, items.Item3)
			 || !TuplePackers.TrySerializeTo(ref tw, items.Item4)
			 || !TuplePackers.TrySerializeTo(ref tw, items.Item5))
			{
				bytesWritten = 0;
				return false;
			}

			bytesWritten = tw.BytesWritten;
			return true;
		}

		/// <summary>Efficiently concatenates a prefix with the packed representation of a 5-tuple</summary>
		[Pure]
		public static bool TryPackTo<T1, T2, T3, T4, T5>(Span<byte> destination, out int bytesWritten, ReadOnlySpan<byte> prefix, in (T1, T2, T3, T4, T5) items)
		{
			var tw = new TupleSpanWriter(destination, 0);
			if (!tw.TryWriteLiteral(prefix)
			 || !TuplePackers.TrySerializeTo(ref tw, items.Item1)
			 || !TuplePackers.TrySerializeTo(ref tw, items.Item2)
			 || !TuplePackers.TrySerializeTo(ref tw, items.Item3)
			 || !TuplePackers.TrySerializeTo(ref tw, items.Item4)
			 || !TuplePackers.TrySerializeTo(ref tw, items.Item5))
			{
				bytesWritten = 0;
				return false;
			}

			bytesWritten = tw.BytesWritten;
			return true;
		}

		/// <summary>Efficiently concatenates a prefix with the packed representation of a 5-tuple</summary>
		public static Slice Pack<T1, T2, T3, T4, T5>(ReadOnlySpan<byte> prefix, in (T1, T2, T3, T4, T5) items)
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

		/// <summary>Pack a 6-tuple directly into a destination buffer</summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool TryEncodeKey<T1, T2, T3, T4, T5, T6>(Span<byte> destination, out int bytesWritten, ReadOnlySpan<byte> prefix, T1 item1, T2 item2, T3 item3, T4 item4, T5 item5, T6 item6)
		{
			var tw = new TupleSpanWriter(destination, 0);
			if (!tw.TryWriteLiteral(prefix) || !TryWriteKeysTo(ref tw, item1, item2, item3, item4, item5, item6))
			{
				bytesWritten = 0;
				return false;
			}
			bytesWritten = tw.BytesWritten;
			return true;
		}

		/// <summary>Efficiently concatenates a prefix with the packed representation of a 6-tuple</summary>
		[Pure]
		public static bool TryPackTo<T1, T2, T3, T4, T5, T6>(Span<byte> destination, out int bytesWritten, ReadOnlySpan<byte> prefix, in STuple<T1, T2, T3, T4, T5, T6> items)
		{
			var tw = new TupleSpanWriter(destination, 0);
			if (!tw.TryWriteLiteral(prefix)
			 || !TuplePackers.TrySerializeTo(ref tw, items.Item1)
			 || !TuplePackers.TrySerializeTo(ref tw, items.Item2)
			 || !TuplePackers.TrySerializeTo(ref tw, items.Item3)
			 || !TuplePackers.TrySerializeTo(ref tw, items.Item4)
			 || !TuplePackers.TrySerializeTo(ref tw, items.Item5)
			 || !TuplePackers.TrySerializeTo(ref tw, items.Item6))
			{
				bytesWritten = 0;
				return false;
			}

			bytesWritten = tw.BytesWritten;
			return true;
		}

		/// <summary>Efficiently concatenates a prefix with the packed representation of a 6-tuple</summary>
		[Pure]
		public static bool TryPackTo<T1, T2, T3, T4, T5, T6>(Span<byte> destination, out int bytesWritten, ReadOnlySpan<byte> prefix, in (T1, T2, T3, T4, T5, T6) items)
		{
			var tw = new TupleSpanWriter(destination, 0);
			if (!tw.TryWriteLiteral(prefix)
			 || !TuplePackers.TrySerializeTo(ref tw, items.Item1)
			 || !TuplePackers.TrySerializeTo(ref tw, items.Item2)
			 || !TuplePackers.TrySerializeTo(ref tw, items.Item3)
			 || !TuplePackers.TrySerializeTo(ref tw, items.Item4)
			 || !TuplePackers.TrySerializeTo(ref tw, items.Item5)
			 || !TuplePackers.TrySerializeTo(ref tw, items.Item6))
			{
				bytesWritten = 0;
				return false;
			}

			bytesWritten = tw.BytesWritten;
			return true;
		}

		/// <summary>Efficiently concatenates a prefix with the packed representation of a 6-tuple</summary>
		public static Slice Pack<T1, T2, T3, T4, T5, T6>(ReadOnlySpan<byte> prefix, in (T1, T2, T3, T4, T5, T6) items)
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

		/// <summary>Pack a 7-tuple directly into a destination buffer</summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool TryEncodeKey<T1, T2, T3, T4, T5, T6, T7>(Span<byte> destination, out int bytesWritten, ReadOnlySpan<byte> prefix, T1 item1, T2 item2, T3 item3, T4 item4, T5 item5, T6 item6, T7 item7)
		{
			var tw = new TupleSpanWriter(destination, 0);
			if (!tw.TryWriteLiteral(prefix) || !TryWriteKeysTo(ref tw, item1, item2, item3, item4, item5, item6, item7))
			{
				bytesWritten = 0;
				return false;
			}
			bytesWritten = tw.BytesWritten;
			return true;
		}

		/// <summary>Efficiently concatenates a prefix with the packed representation of a 7-tuple</summary>
		[Pure]
		public static bool TryPackTo<T1, T2, T3, T4, T5, T6, T7>(Span<byte> destination, out int bytesWritten, ReadOnlySpan<byte> prefix, in STuple<T1, T2, T3, T4, T5, T6, T7> items)
		{
			var tw = new TupleSpanWriter(destination, 0);
			if (!tw.TryWriteLiteral(prefix)
			 || !TuplePackers.TrySerializeTo(ref tw, items.Item1)
			 || !TuplePackers.TrySerializeTo(ref tw, items.Item2)
			 || !TuplePackers.TrySerializeTo(ref tw, items.Item3)
			 || !TuplePackers.TrySerializeTo(ref tw, items.Item4)
			 || !TuplePackers.TrySerializeTo(ref tw, items.Item5)
			 || !TuplePackers.TrySerializeTo(ref tw, items.Item6)
			 || !TuplePackers.TrySerializeTo(ref tw, items.Item7))
			{
				bytesWritten = 0;
				return false;
			}

			bytesWritten = tw.BytesWritten;
			return true;
		}

		/// <summary>Efficiently concatenates a prefix with the packed representation of a 7-tuple</summary>
		[Pure]
		public static bool TryPackTo<T1, T2, T3, T4, T5, T6, T7>(Span<byte> destination, out int bytesWritten, ReadOnlySpan<byte> prefix, in (T1, T2, T3, T4, T5, T6, T7) items)
		{
			var tw = new TupleSpanWriter(destination, 0);
			if (!tw.TryWriteLiteral(prefix)
			 || !TuplePackers.TrySerializeTo(ref tw, items.Item1)
			 || !TuplePackers.TrySerializeTo(ref tw, items.Item2)
			 || !TuplePackers.TrySerializeTo(ref tw, items.Item3)
			 || !TuplePackers.TrySerializeTo(ref tw, items.Item4)
			 || !TuplePackers.TrySerializeTo(ref tw, items.Item5)
			 || !TuplePackers.TrySerializeTo(ref tw, items.Item6)
			 || !TuplePackers.TrySerializeTo(ref tw, items.Item7))
			{
				bytesWritten = 0;
				return false;
			}

			bytesWritten = tw.BytesWritten;
			return true;
		}

		/// <summary>Efficiently concatenates a prefix with the packed representation of a 7-tuple</summary>
		public static Slice Pack<T1, T2, T3, T4, T5, T6, T7>(ReadOnlySpan<byte> prefix, in (T1, T2, T3, T4, T5, T6, T7) items)
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

		/// <summary>Pack a 7-tuple directly into a destination buffer</summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool TryEncodeKey<T1, T2, T3, T4, T5, T6, T7, T8>(Span<byte> destination, out int bytesWritten, ReadOnlySpan<byte> prefix, T1 item1, T2 item2, T3 item3, T4 item4, T5 item5, T6 item6, T7 item7, T8 item8)
		{
			var tw = new TupleSpanWriter(destination, 0);
			if (!tw.TryWriteLiteral(prefix) || !TryWriteKeysTo(ref tw, item1, item2, item3, item4, item5, item6, item7))
			{
				bytesWritten = 0;
				return false;
			}
			bytesWritten = tw.BytesWritten;
			return true;
		}

		/// <summary>Efficiently concatenates a prefix with the packed representation of an 8-tuple</summary>
		[Pure]
		public static bool TryPackTo<T1, T2, T3, T4, T5, T6, T7, T8>(Span<byte> destination, out int bytesWritten, ReadOnlySpan<byte> prefix, in STuple<T1, T2, T3, T4, T5, T6, T7, T8> items)
		{
			var tw = new TupleSpanWriter(destination, 0);
			if (!tw.TryWriteLiteral(prefix)
			 || !TuplePackers.TrySerializeTo(ref tw, items.Item1)
			 || !TuplePackers.TrySerializeTo(ref tw, items.Item2)
			 || !TuplePackers.TrySerializeTo(ref tw, items.Item3)
			 || !TuplePackers.TrySerializeTo(ref tw, items.Item4)
			 || !TuplePackers.TrySerializeTo(ref tw, items.Item5)
			 || !TuplePackers.TrySerializeTo(ref tw, items.Item6)
			 || !TuplePackers.TrySerializeTo(ref tw, items.Item7)
			 || !TuplePackers.TrySerializeTo(ref tw, items.Item8))
			{
				bytesWritten = 0;
				return false;
			}

			bytesWritten = tw.BytesWritten;
			return true;
		}

		/// <summary>Efficiently concatenates a prefix with the packed representation of an 8-tuple</summary>
		[Pure]
		public static bool TryPackTo<T1, T2, T3, T4, T5, T6, T7, T8>(Span<byte> destination, out int bytesWritten, ReadOnlySpan<byte> prefix, in (T1, T2, T3, T4, T5, T6, T7, T8) items)
		{
			var tw = new TupleSpanWriter(destination, 0);
			if (!tw.TryWriteLiteral(prefix)
			 || !TuplePackers.TrySerializeTo(ref tw, items.Item1)
			 || !TuplePackers.TrySerializeTo(ref tw, items.Item2)
			 || !TuplePackers.TrySerializeTo(ref tw, items.Item3)
			 || !TuplePackers.TrySerializeTo(ref tw, items.Item4)
			 || !TuplePackers.TrySerializeTo(ref tw, items.Item5)
			 || !TuplePackers.TrySerializeTo(ref tw, items.Item6)
			 || !TuplePackers.TrySerializeTo(ref tw, items.Item7)
			 || !TuplePackers.TrySerializeTo(ref tw, items.Item8))
			{
				bytesWritten = 0;
				return false;
			}

			bytesWritten = tw.BytesWritten;
			return true;
		}

		/// <summary>Efficiently concatenates a prefix with the packed representation of an 8-tuple</summary>
		public static Slice Pack<T1, T2, T3, T4, T5, T6, T7, T8>(ReadOnlySpan<byte> prefix, in (T1, T2, T3, T4, T5, T6, T7, T8) items)
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
		public static Slice Pack<T1, T2, T3, T4, T5, T6, T7, T8, T9>(ReadOnlySpan<byte> prefix, in (T1, T2, T3, T4, T5, T6, T7, T8, T9) items)
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

		// SliceOwner/ArrayPool

		/// <summary>Efficiently concatenates a prefix with the packed representation of a 1-tuple</summary>
		[Pure]
		public static SliceOwner Pack<T1>(ArrayPool<byte> pool, ReadOnlySpan<byte> prefix, in ValueTuple<T1> items)
		{
			var sw = new SliceWriter(pool);
			sw.WriteBytes(prefix);
			var tw = new TupleWriter(ref sw);
			TuplePackers.SerializeTo(tw, items.Item1);
			return sw.ToSliceOwner();
		}

		/// <summary>Efficiently concatenates a prefix with the packed representation of a 1-tuple</summary>
		[Pure]
		public static SliceOwner Pack<T1>(ArrayPool<byte> pool, ReadOnlySpan<byte> prefix, in STuple<T1> items)
		{
			var sw = new SliceWriter(pool);
			sw.WriteBytes(prefix);
			var tw = new TupleWriter(ref sw);
			TuplePackers.SerializeTo(tw, items.Item1);
			return sw.ToSliceOwner();
		}

		/// <summary>Efficiently concatenates a prefix with the packed representation of a 2-tuple</summary>
		[Pure]
		public static SliceOwner Pack<T1, T2>(ArrayPool<byte> pool, ReadOnlySpan<byte> prefix, in STuple<T1, T2> items)
		{
			var sw = new SliceWriter(pool);
			sw.WriteBytes(prefix);
			var tw = new TupleWriter(ref sw);
			TuplePackers.SerializeTo(tw, items.Item1);
			TuplePackers.SerializeTo(tw, items.Item2);
			return sw.ToSliceOwner();
		}

		/// <summary>Efficiently concatenates a prefix with the packed representation of a 2-tuple</summary>
		[Pure]
		public static SliceOwner Pack<T1, T2>(ArrayPool<byte> pool, ReadOnlySpan<byte> prefix, in (T1, T2) items)
		{
			var sw = new SliceWriter(pool);
			sw.WriteBytes(prefix);
			var tw = new TupleWriter(ref sw);
			TuplePackers.SerializeTo(tw, items.Item1);
			TuplePackers.SerializeTo(tw, items.Item2);
			return sw.ToSliceOwner();
		}

		/// <summary>Efficiently concatenates a prefix with the packed representation of a 3-tuple</summary>
		[Pure]
		public static SliceOwner Pack<T1, T2, T3>(ArrayPool<byte> pool, ReadOnlySpan<byte> prefix, in STuple<T1, T2, T3> items)
		{
			var sw = new SliceWriter(pool);
			sw.WriteBytes(prefix);
			var tw = new TupleWriter(ref sw);
			TuplePackers.SerializeTo(tw, items.Item1);
			TuplePackers.SerializeTo(tw, items.Item2);
			TuplePackers.SerializeTo(tw, items.Item3);
			return sw.ToSliceOwner();
		}

		/// <summary>Efficiently concatenates a prefix with the packed representation of a 3-tuple</summary>
		[Pure]
		public static SliceOwner Pack<T1, T2, T3>(ArrayPool<byte> pool, ReadOnlySpan<byte> prefix, in (T1, T2, T3) items)
		{
			var sw = new SliceWriter(pool);
			sw.WriteBytes(prefix);
			var tw = new TupleWriter(ref sw);
			TuplePackers.SerializeTo(tw, items.Item1);
			TuplePackers.SerializeTo(tw, items.Item2);
			TuplePackers.SerializeTo(tw, items.Item3);
			return sw.ToSliceOwner();
		}

		/// <summary>Efficiently concatenates a prefix with the packed representation of a 4-tuple</summary>
		[Pure]
		public static SliceOwner Pack<T1, T2, T3, T4>(ArrayPool<byte> pool, ReadOnlySpan<byte> prefix, in STuple<T1, T2, T3, T4> items)
		{
			var sw = new SliceWriter(pool);
			sw.WriteBytes(prefix);
			var tw = new TupleWriter(ref sw);
			TuplePackers.SerializeTo(tw, items.Item1);
			TuplePackers.SerializeTo(tw, items.Item2);
			TuplePackers.SerializeTo(tw, items.Item3);
			TuplePackers.SerializeTo(tw, items.Item4);
			return sw.ToSliceOwner();
		}

		/// <summary>Efficiently concatenates a prefix with the packed representation of a 4-tuple</summary>
		[Pure]
		public static SliceOwner Pack<T1, T2, T3, T4>(ArrayPool<byte> pool, ReadOnlySpan<byte> prefix, in (T1, T2, T3, T4) items)
		{
			var sw = new SliceWriter(pool);
			sw.WriteBytes(prefix);
			var tw = new TupleWriter(ref sw);
			TuplePackers.SerializeTo(tw, items.Item1);
			TuplePackers.SerializeTo(tw, items.Item2);
			TuplePackers.SerializeTo(tw, items.Item3);
			TuplePackers.SerializeTo(tw, items.Item4);
			return sw.ToSliceOwner();
		}

		/// <summary>Efficiently concatenates a prefix with the packed representation of a 5-tuple</summary>
		[Pure]
		public static SliceOwner Pack<T1, T2, T3, T4, T5>(ArrayPool<byte> pool, ReadOnlySpan<byte> prefix, in STuple<T1, T2, T3, T4, T5> items)
		{
			var sw = new SliceWriter(pool);
			sw.WriteBytes(prefix);
			var tw = new TupleWriter(ref sw);
			TuplePackers.SerializeTo(tw, items.Item1);
			TuplePackers.SerializeTo(tw, items.Item2);
			TuplePackers.SerializeTo(tw, items.Item3);
			TuplePackers.SerializeTo(tw, items.Item4);
			TuplePackers.SerializeTo(tw, items.Item5);
			return sw.ToSliceOwner();
		}

		/// <summary>Efficiently concatenates a prefix with the packed representation of a 5-tuple</summary>
		[Pure]
		public static SliceOwner Pack<T1, T2, T3, T4, T5>(ArrayPool<byte> pool, ReadOnlySpan<byte> prefix, in (T1, T2, T3, T4, T5) items)
		{
			var sw = new SliceWriter(pool);
			sw.WriteBytes(prefix);
			var tw = new TupleWriter(ref sw);
			TuplePackers.SerializeTo(tw, items.Item1);
			TuplePackers.SerializeTo(tw, items.Item2);
			TuplePackers.SerializeTo(tw, items.Item3);
			TuplePackers.SerializeTo(tw, items.Item4);
			TuplePackers.SerializeTo(tw, items.Item5);
			return sw.ToSliceOwner();
		}

		/// <summary>Efficiently concatenates a prefix with the packed representation of a 6-tuple</summary>
		[Pure]
		public static SliceOwner Pack<T1, T2, T3, T4, T5, T6>(ArrayPool<byte> pool, ReadOnlySpan<byte> prefix, in STuple<T1, T2, T3, T4, T5, T6> items)
		{
			var sw = new SliceWriter(pool);
			sw.WriteBytes(prefix);
			var tw = new TupleWriter(ref sw);
			TuplePackers.SerializeTo(tw, items.Item1);
			TuplePackers.SerializeTo(tw, items.Item2);
			TuplePackers.SerializeTo(tw, items.Item3);
			TuplePackers.SerializeTo(tw, items.Item4);
			TuplePackers.SerializeTo(tw, items.Item5);
			TuplePackers.SerializeTo(tw, items.Item6);
			return sw.ToSliceOwner();
		}

		/// <summary>Efficiently concatenates a prefix with the packed representation of a 6-tuple</summary>
		[Pure]
		public static SliceOwner Pack<T1, T2, T3, T4, T5, T6>(ArrayPool<byte> pool, ReadOnlySpan<byte> prefix, in (T1, T2, T3, T4, T5, T6) items)
		{
			var sw = new SliceWriter(pool);
			sw.WriteBytes(prefix);
			var tw = new TupleWriter(ref sw);
			TuplePackers.SerializeTo(tw, items.Item1);
			TuplePackers.SerializeTo(tw, items.Item2);
			TuplePackers.SerializeTo(tw, items.Item3);
			TuplePackers.SerializeTo(tw, items.Item4);
			TuplePackers.SerializeTo(tw, items.Item5);
			TuplePackers.SerializeTo(tw, items.Item6);
			return sw.ToSliceOwner();
		}

		/// <summary>Efficiently concatenates a prefix with the packed representation of a 7-tuple</summary>
		[Pure]
		public static SliceOwner Pack<T1, T2, T3, T4, T5, T6, T7>(ArrayPool<byte> pool, ReadOnlySpan<byte> prefix, in STuple<T1, T2, T3, T4, T5, T6, T7> items)
		{
			var sw = new SliceWriter(pool);
			sw.WriteBytes(prefix);
			var tw = new TupleWriter(ref sw);
			TuplePackers.SerializeTo(tw, items.Item1);
			TuplePackers.SerializeTo(tw, items.Item2);
			TuplePackers.SerializeTo(tw, items.Item3);
			TuplePackers.SerializeTo(tw, items.Item4);
			TuplePackers.SerializeTo(tw, items.Item5);
			TuplePackers.SerializeTo(tw, items.Item6);
			TuplePackers.SerializeTo(tw, items.Item7);
			return sw.ToSliceOwner();
		}

		/// <summary>Efficiently concatenates a prefix with the packed representation of a 7-tuple</summary>
		[Pure]
		public static SliceOwner Pack<T1, T2, T3, T4, T5, T6, T7>(ArrayPool<byte> pool, ReadOnlySpan<byte> prefix, in (T1, T2, T3, T4, T5, T6, T7) items)
		{
			var sw = new SliceWriter(pool);
			sw.WriteBytes(prefix);
			var tw = new TupleWriter(ref sw);
			TuplePackers.SerializeTo(tw, items.Item1);
			TuplePackers.SerializeTo(tw, items.Item2);
			TuplePackers.SerializeTo(tw, items.Item3);
			TuplePackers.SerializeTo(tw, items.Item4);
			TuplePackers.SerializeTo(tw, items.Item5);
			TuplePackers.SerializeTo(tw, items.Item6);
			TuplePackers.SerializeTo(tw, items.Item7);
			return sw.ToSliceOwner();
		}

		/// <summary>Efficiently concatenates a prefix with the packed representation of an 8-tuple</summary>
		[Pure]
		public static SliceOwner Pack<T1, T2, T3, T4, T5, T6, T7, T8>(ArrayPool<byte> pool, ReadOnlySpan<byte> prefix, in STuple<T1, T2, T3, T4, T5, T6, T7, T8> items)
		{
			var sw = new SliceWriter(pool);
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
			return sw.ToSliceOwner();
		}

		/// <summary>Efficiently concatenates a prefix with the packed representation of an 8-tuple</summary>
		[Pure]
		public static SliceOwner Pack<T1, T2, T3, T4, T5, T6, T7, T8>(ArrayPool<byte> pool, ReadOnlySpan<byte> prefix, in (T1, T2, T3, T4, T5, T6, T7, T8) items)
		{
			var sw = new SliceWriter(pool);
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
			return sw.ToSliceOwner();
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

		/// <summary>Packs a 1-tuple directly into a slice</summary>
		public static bool TryWriteKeysTo<T1>(ref TupleSpanWriter writer, T1 item1)
		{
			return TuplePackers.TrySerializeTo(ref writer, item1);
		}

		/// <summary>Packs a 2-tuple directly into a slice</summary>
		public static void WriteKeysTo<T1, T2>(ref SliceWriter writer, T1? item1, T2? item2)
		{
			var tw = new TupleWriter(ref writer);
			TuplePackers.SerializeTo(tw, item1);
			TuplePackers.SerializeTo(tw, item2);
		}

		/// <summary>Packs a 2-tuple directly into a slice</summary>
		public static bool TryWriteKeysTo<T1, T2>(ref TupleSpanWriter writer, T1 item1, T2 item2)
		{
			return TuplePackers.TrySerializeTo(ref writer, item1)
				&& TuplePackers.TrySerializeTo(ref writer, item2);
		}

		/// <summary>Packs a 3-tuple directly into a slice</summary>
		public static void WriteKeysTo<T1, T2, T3>(ref SliceWriter writer, T1? item1, T2? item2, T3? item3)
		{
			var tw = new TupleWriter(ref writer);
			TuplePackers.SerializeTo(tw, item1);
			TuplePackers.SerializeTo(tw, item2);
			TuplePackers.SerializeTo(tw, item3);
		}

		/// <summary>Packs a 3-tuple directly into a slice</summary>
		public static bool TryWriteKeysTo<T1, T2, T3>(ref TupleSpanWriter writer, T1 item1, T2 item2, T3 item3)
		{
			return TuplePackers.TrySerializeTo(ref writer, item1)
				&& TuplePackers.TrySerializeTo(ref writer, item2)
				&& TuplePackers.TrySerializeTo(ref writer, item3);
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

		/// <summary>Packs a 4-tuple directly into a slice</summary>
		public static bool TryWriteKeysTo<T1, T2, T3, T4>(ref TupleSpanWriter writer, T1 item1, T2 item2, T3 item3, T4 item4)
		{
			return TuplePackers.TrySerializeTo(ref writer, item1)
				&& TuplePackers.TrySerializeTo(ref writer, item2)
				&& TuplePackers.TrySerializeTo(ref writer, item3)
				&& TuplePackers.TrySerializeTo(ref writer, item4);
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

		/// <summary>Packs a 5-tuple directly into a slice</summary>
		public static bool TryWriteKeysTo<T1, T2, T3, T4, T5>(ref TupleSpanWriter writer, T1 item1, T2 item2, T3 item3, T4 item4, T5 item5)
		{
			return TuplePackers.TrySerializeTo(ref writer, item1)
				&& TuplePackers.TrySerializeTo(ref writer, item2)
				&& TuplePackers.TrySerializeTo(ref writer, item3)
				&& TuplePackers.TrySerializeTo(ref writer, item4)
				&& TuplePackers.TrySerializeTo(ref writer, item5);
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
		public static bool TryWriteKeysTo<T1, T2, T3, T4, T5, T6>(ref TupleSpanWriter writer, T1 item1, T2 item2, T3 item3, T4 item4, T5 item5, T6 item6)
		{
			return TuplePackers.TrySerializeTo(ref writer, item1)
				&& TuplePackers.TrySerializeTo(ref writer, item2)
				&& TuplePackers.TrySerializeTo(ref writer, item3)
				&& TuplePackers.TrySerializeTo(ref writer, item4)
				&& TuplePackers.TrySerializeTo(ref writer, item5)
				&& TuplePackers.TrySerializeTo(ref writer, item6);
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

		/// <summary>Packs a 7-tuple directly into a slice</summary>
		public static bool TryWriteKeysTo<T1, T2, T3, T4, T5, T6, T7>(ref TupleSpanWriter writer, T1 item1, T2 item2, T3 item3, T4 item4, T5 item5, T6 item6, T7 item7)
		{
			return TuplePackers.TrySerializeTo(ref writer, item1)
				&& TuplePackers.TrySerializeTo(ref writer, item2)
				&& TuplePackers.TrySerializeTo(ref writer, item3)
				&& TuplePackers.TrySerializeTo(ref writer, item4)
				&& TuplePackers.TrySerializeTo(ref writer, item5)
				&& TuplePackers.TrySerializeTo(ref writer, item6)
				&& TuplePackers.TrySerializeTo(ref writer, item7);
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
		/// <summary>Packs a 8-tuple directly into a slice</summary>
		public static bool TryWriteKeysTo<T1, T2, T3, T4, T5, T6, T7, T8>(ref TupleSpanWriter writer, T1 item1, T2 item2, T3 item3, T4 item4, T5 item5, T6 item6, T7 item7, T8 item8)
		{
			return TuplePackers.TrySerializeTo(ref writer, item1)
				&& TuplePackers.TrySerializeTo(ref writer, item2)
				&& TuplePackers.TrySerializeTo(ref writer, item3)
				&& TuplePackers.TrySerializeTo(ref writer, item4)
				&& TuplePackers.TrySerializeTo(ref writer, item5)
				&& TuplePackers.TrySerializeTo(ref writer, item6)
				&& TuplePackers.TrySerializeTo(ref writer, item7)
				&& TuplePackers.TrySerializeTo(ref writer, item8);
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
			var packer = TuplePacker<T>.Encoders.Direct;

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
			var packer = TuplePacker<T>.Encoders.Direct;
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
			var packer = TuplePacker<TKey>.Encoders.Direct;
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

		[Pure]
		private static int GetSizeHint(int item)
			=> item == 0 ? 1
			: item >= 0 ? item switch
			{
				<= 0xFF => 2,
				<= 0xFFFF => 3,
				<= 0xFFFFFF => 4,
				_ => 5,
			} : item switch
			{
				>= -256 => 2,
				>= -65536 => 3,
				>= -16777216 => 4,
				_ => 5,
			};

		[Pure]
		private static int GetSizeHint(long item)
			=> item == 0 ? 1
			: item >= 0 ? item switch
			{
				<= 0xFF => 2,
				<= 0xFFFF => 3,
				<= 0xFFFFFF => 4,
				<= 0xFFFFFFFF => 5,
				<= 0xFFFFFFFFFF => 6,
				<= 0xFFFFFFFFFFFF => 7,
				<= 0xFFFFFFFFFFFFFF => 8,
				_ => 9,
			} : item switch
			{
				>= -256L => 2,
				>= -65536L => 3,
				>= -16777216L => 4,
				>= -4294967296L => 5,
				>= -1099511627776L => 6,
				>= -281474976710656L => 7,
				>= -72057594037927936 => 8,
				_ => 9,
			};

		[Pure]
		private static int GetSizeHint(uint item) => item switch
		{
			0 => 1,
			<= 0xFF => 2,
			<= 0xFFFF => 3,
			<= 0xFFFFFF => 4,
			_ => 5
		};

		[Pure]
		private static int GetSizeHint(ulong item) => item switch
		{
			0 => 1,
			<= 0xFF => 2,
			<= 0xFFFF => 3,
			<= 0xFFFFFF => 4,
			<= 0xFFFFFFFF => 5,
			<= 0xFFFFFFFFFF => 6,
			<= 0xFFFFFFFFFFFF => 7,
			_ => 8
		};

		[Pure]
		private static int GetSizeHint(string s)
		{
			// we have 2 bytes for prefix/suffix `02....00`
			// then for each 0x00 byte in the utf-8 encoded string, we have to replace them into `00 FF`

			int utf8Len = Encoding.UTF8.GetByteCount(s);
			// note: we use the fact that only `\0` produces 0x00 when encoded in UTF-8, so we can safely count the chars without encoding first
			int zeroes = s.AsSpan().Count('\0');
			return checked(2 + utf8Len + zeroes);
		}

		[Pure]
		private static int GetSizeHint(Slice s)
		{
			// we have 2 bytes for prefix/suffix `01....00`
			// then for each 0x00 byte in the slice, we have to replace them into `00 FF`

			int zeroes = s.Span.Count((byte) 0);
			return checked(2 + s.Count + zeroes);
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool TryGetSizeHint<T>(T item, out int sizeHint)
		{
			if (typeof(T) == typeof(int)) { sizeHint = GetSizeHint((int) (object) item!); return true; }
			if (typeof(T) == typeof(uint)) { sizeHint = GetSizeHint((uint) (object) item!); return true; }
			if (typeof(T) == typeof(long)) { sizeHint = GetSizeHint((long) (object) item!); return true; }
			if (typeof(T) == typeof(ulong)) { sizeHint = GetSizeHint((ulong) (object) item!); return true; }
			if (typeof(T) == typeof(bool)) { sizeHint = 1; return true; }
			if (typeof(T) == typeof(VersionStamp)) { sizeHint = ((VersionStamp) (object) item!).HasUserVersion ? 13 : 11; return true; }
			if (typeof(T) == typeof(Guid) || typeof(T) == typeof(Uuid128)) { sizeHint = 17; return true; }
			if (typeof(T) == typeof(Uuid96)) { sizeHint = 13; return true; }
			if (typeof(T) == typeof(Uuid80)) { sizeHint = 11; return true; }
			if (typeof(T) == typeof(Uuid64)) { sizeHint = 9; return true; }
			if (typeof(T) == typeof(Uuid48)) { sizeHint = 7; return true; }
			if (typeof(T) == typeof(float)) { sizeHint = 5; return true; }
			if (typeof(T) == typeof(double)) { sizeHint = 9; return true; }

			if (typeof(T) == typeof(Slice)) { sizeHint = GetSizeHint((Slice) (object) item!); return true; }
			if (typeof(T) == typeof(DateTime) || typeof(T) == typeof(DateTimeOffset) || typeof(T) == typeof(NodaTime.Instant)) { sizeHint = 9; return true; }

			if (item is null) { sizeHint = 1; return true; }

			if (item is string s) { sizeHint = GetSizeHint(s); return true; }

			sizeHint = 0;
			return false;
		}

		internal class Encoder<T> : IKeyEncoder<T>, IValueEncoder<T>
		{

			public static readonly Encoder<T> Default = new();

			private Encoder() { }

			public IKeyEncoding Encoding => TuPack.Encoding;

			public void WriteKeyTo(ref SliceWriter writer, T? key)
			{
				WriteKeysTo(ref writer, key);
			}

			public bool TryWriteKeyTo(Span<byte> destination, out int bytesWritten, T? key)
			{
				return TryEncodeKey(destination, out bytesWritten, default, key);
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
				return EncodeKey(default, key);
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

			public override bool TryWriteKeyPartsTo(Span<byte> destination, out int bytesWritten, int count, in (T1?, T2?) key) => count switch
			{
				2 => TryPackTo(destination, out bytesWritten, default, in key),
				1 => TryEncodeKey(destination, out bytesWritten, default, key.Item1),
				_ => throw new ArgumentOutOfRangeException(nameof(count), count, "Item count must be between 1 and 3")
			};

			public override void ReadKeyPartsFrom(ref SliceReader reader, int count, out (T1?, T2?) key)
			{
				if (count is < 1 or > 2) throw new ArgumentOutOfRangeException(nameof(count), count, "Item count must be either 1 or 2");

				var t = TuPack.Unpack(reader.ReadToEnd()).OfSize(count);
				Contract.Debug.Assert(t != null);
				key.Item1 = t.Get<T1>(0);
				key.Item2 = count == 2 ? t.Get<T2>(1)! : default;
			}

			public override bool TryReadKeyPartsFrom(ref SliceReader reader, int count, out (T1?, T2?) key)
			{
				if (count is < 1 or > 2)
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

			public override bool TryWriteKeyPartsTo(Span<byte> destination, out int bytesWritten, int count, in (T1?, T2?, T3?) key) => count switch
			{
				3 => TryPackTo(destination, out bytesWritten, default, in key),
				2 => TryEncodeKey(destination, out bytesWritten, default, key.Item1, key.Item2),
				1 => TryEncodeKey(destination, out bytesWritten, default, key.Item1),
				_ => throw new ArgumentOutOfRangeException(nameof(count), count, "Item count must be between 1 and 3")
			};

			public override void ReadKeyPartsFrom(ref SliceReader reader, int count, out (T1?, T2?, T3?) key)
			{
				if (count is < 1 or > 3) throw new ArgumentOutOfRangeException(nameof(count), count, "Item count must be between 1 and 3");

				var t = TuPack.Unpack(reader.ReadToEnd()).OfSize(count);
				Contract.Debug.Assert(t != null);
				key.Item1 = t.Get<T1>(0);
				key.Item2 = count >= 2 ? t.Get<T2>(1) : default;
				key.Item3 = count >= 3 ? t.Get<T3>(2) : default;
			}

			public override bool TryReadKeyPartsFrom(ref SliceReader reader, int count, out (T1?, T2?, T3?) key)
			{
				if (count is < 1 or > 3)
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

			public override bool TryWriteKeyPartsTo(Span<byte> destination, out int bytesWritten, int count, in (T1?, T2?, T3?, T4?) key) => count switch
			{
				4 => TryPackTo(destination, out bytesWritten, default, in key),
				3 => TryEncodeKey(destination, out bytesWritten, default, key.Item1, key.Item2, key.Item3),
				2 => TryEncodeKey(destination, out bytesWritten, default, key.Item1, key.Item2),
				1 => TryEncodeKey(destination, out bytesWritten, default, key.Item1),
				_ => throw new ArgumentOutOfRangeException(nameof(count), count, "Item count must be between 1 and 4")
			};

			public override void ReadKeyPartsFrom(ref SliceReader reader, int count, out (T1?, T2?, T3?, T4?) key)
			{
				if (count is < 1 or > 4) throw new ArgumentOutOfRangeException(nameof(count), count, "Item count must be between 1 and 4");

				var t = TuPack.Unpack(reader.ReadToEnd()).OfSize(count);
				Contract.Debug.Assert(t != null);
				key.Item1 = t.Get<T1>(0);
				key.Item2 = count >= 2 ? t.Get<T2>(1) : default;
				key.Item3 = count >= 3 ? t.Get<T3>(2) : default;
				key.Item4 = count >= 4 ? t.Get<T4>(3) : default;
			}

			public override bool TryReadKeyPartsFrom(ref SliceReader reader, int count, out (T1?, T2?, T3?, T4?) key)
			{
				if (count is < 1 or > 4)
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

			public override bool TryWriteKeyPartsTo(Span<byte> destination, out int bytesWritten, int count, in (T1?, T2?, T3?, T4?, T5?) key) => count switch
			{
				5 => TryPackTo(destination, out bytesWritten, default, in key),
				4 => TryEncodeKey(destination, out bytesWritten, default, key.Item1, key.Item2, key.Item3, key.Item4),
				3 => TryEncodeKey(destination, out bytesWritten, default, key.Item1, key.Item2, key.Item3),
				2 => TryEncodeKey(destination, out bytesWritten, default, key.Item1, key.Item2),
				1 => TryEncodeKey(destination, out bytesWritten, default, key.Item1),
				_ => throw new ArgumentOutOfRangeException(nameof(count), count, "Item count must be between 1 and 5")
			};

			public override void ReadKeyPartsFrom(ref SliceReader reader, int count, out (T1?, T2?, T3?, T4?, T5?) key)
			{
				if (count is < 1 or > 5) throw new ArgumentOutOfRangeException(nameof(count), count, "Item count must be between 1 and 5");

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
				if (count is < 1 or > 5)
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

			public override bool TryWriteKeyPartsTo(Span<byte> destination, out int bytesWritten, int count, in (T1?, T2?, T3?, T4?, T5?, T6?) key) => count switch
			{
				6 => TryPackTo(destination, out bytesWritten, default, in key),
				5 => TryEncodeKey(destination, out bytesWritten, default, key.Item1, key.Item2, key.Item3, key.Item4, key.Item5),
				4 => TryEncodeKey(destination, out bytesWritten, default, key.Item1, key.Item2, key.Item3, key.Item4),
				3 => TryEncodeKey(destination, out bytesWritten, default, key.Item1, key.Item2, key.Item3),
				2 => TryEncodeKey(destination, out bytesWritten, default, key.Item1, key.Item2),
				1 => TryEncodeKey(destination, out bytesWritten, default, key.Item1),
				_ => throw new ArgumentOutOfRangeException(nameof(count), count, "Item count must be between 1 and 6")
			};

			public override void ReadKeyPartsFrom(ref SliceReader reader, int count, out (T1?, T2?, T3?, T4?, T5?, T6?) key)
			{
				if (count is < 1 or > 6) throw new ArgumentOutOfRangeException(nameof(count), count, "Item count must be between 1 and 6");

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
				if (count is < 1 or > 6)
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
