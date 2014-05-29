#region BSD Licence
/* Copyright (c) 2013-2014, Doxense SAS
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

namespace FoundationDB.Client
{
	using FoundationDB.Async;
	using FoundationDB.Client.Utils;
	using JetBrains.Annotations;
	using System;
	using System.Collections.Generic;
	using System.ComponentModel;
	using System.Diagnostics;
	using System.Globalization;
	using System.IO;
	using System.Linq;
	using System.Text;
	using System.Threading;
	using System.Threading.Tasks;

	/// <summary>Delimits a section of a byte array</summary>
	[ImmutableObject(true), DebuggerDisplay("Count={Count}, Offset={Offset}"), DebuggerTypeProxy(typeof(Slice.DebugView))]
	public struct Slice : IEquatable<Slice>, IEquatable<ArraySegment<byte>>, IEquatable<byte[]>, IComparable<Slice>
	{
		#region Static Members...

		/// <summary>Cached empty array of bytes</summary>
		internal static readonly byte[] EmptyArray = new byte[0];

		/// <summary>Cached empty array of slices</summary>
		internal static readonly Slice[] EmptySliceArray = new Slice[0];

		/// <summary>Cached array of bytes from 0 to 255</summary>
		internal static readonly byte[] ByteSprite;

		/// <summary>Null slice ("no segment")</summary>
		public static readonly Slice Nil = default(Slice);

		/// <summary>Empty slice ("segment of 0 bytes")</summary>
		public static readonly Slice Empty = new Slice(EmptyArray, 0, 0);

		[System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA2207:InitializeValueTypeStaticFieldsInline")]
		static Slice()
		{
			var tmp = new byte[256];
			for (int i = 0; i < tmp.Length; i++) tmp[i] = (byte)i;
			ByteSprite = tmp;
		}

		#endregion

		/// <summary>Pointer to the buffer (or null for <see cref="Slice.Nil"/>)</summary>
		public readonly byte[] Array;

		/// <summary>Offset of the first byte of the slice in the parent buffer</summary>
		public readonly int Offset;

		/// <summary>Number of bytes in the slice</summary>
		public readonly int Count;

		internal Slice(byte[] array, int offset, int count)
		{
			Contract.Requires(array != null && offset >= 0 && offset <= array.Length && count >= 0 && offset + count <= array.Length);
			this.Array = array;
			this.Offset = offset;
			this.Count = count;
		}

		/// <summary>Creates a slice mapping an entire buffer</summary>
		/// <param name="bytes"></param>
		/// <returns></returns>
		public static Slice Create(byte[] bytes)
		{
			return 
				bytes == null ? Slice.Nil :
				bytes.Length == 0 ? Slice.Empty :
				new Slice(bytes, 0, bytes.Length);
		}

		/// <summary>Creates a slice from an Array Segment</summary>
		/// <param name="arraySegment">Segment of buffer to convert</param>
		public static Slice Create(ArraySegment<byte> arraySegment)
		{
			return Create(arraySegment.Array, arraySegment.Offset, arraySegment.Count);
		}

		/// <summary>Creates a slice mapping a section of a buffer</summary>
		/// <param name="buffer">Original buffer</param>
		/// <param name="offset">Offset into buffer</param>
		/// <param name="count">Number of bytes</param>
		public static Slice Create(byte[] buffer, int offset, int count)
		{
			SliceHelpers.EnsureBufferIsValid(buffer, offset, count);
			if (count == 0)
			{
				if (offset != 0) throw new ArgumentException("offset");
				return buffer == null ? Nil : Empty;
			}
			return new Slice(buffer, offset, count);
		}

		/// <summary>Create a new empty slice of a specified size containing all zeroes</summary>
		/// <param name="size"></param>
		/// <returns></returns>
		public static Slice Create(int size)
		{
			if (size < 0) throw new ArgumentException("size");
			return size == 0 ? Slice.Empty : new Slice(new byte[size], 0, size);
		}

		/// <summary>Creates a new slice with a copy of an unmanaged memory buffer</summary>
		/// <param name="ptr">Pointer to unmanaged buffer</param>
		/// <param name="count">Number of bytes in the buffer</param>
		/// <returns>Slice with a managed copy of the data</returns>
		internal static unsafe Slice Create(byte* ptr, int count)
		{
			if (count == 0)
			{
				return ptr == null ? Slice.Nil : Slice.Empty;
			}
			if (ptr == null) throw new ArgumentNullException("ptr");
			if (count < 0) throw new ArgumentOutOfRangeException("count");

			if (count == 1)
			{
				return Slice.FromByte(*ptr);
			}

			var bytes = new byte[count];
			SliceHelpers.CopyBytesUnsafe(bytes, 0, ptr, count);
			return new Slice(bytes, 0, count);
		}

		/// <summary>Create a new slice filled with random bytes taken from a random number generator</summary>
		/// <param name="prng">Pseudo random generator to use (needs locking if instance is shared)</param>
		/// <param name="count">Number of random bytes to generate</param>
		/// <returns>Slice of <paramref name="count"/> bytes taken from <paramref name="prng"/></returns>
		/// <remarks>Warning: <see cref="System.Random"/> is not thread-safe ! If the <paramref name="prng"/> instance is shared between threads, then it needs to be locked before calling this method.</remarks>
		public static Slice Random([NotNull] Random prng, int count)
		{
			if (prng == null) throw new ArgumentNullException("prng");
			if (count < 0) throw new ArgumentOutOfRangeException("count", count, "Count cannot be negative");
			if (count == 0) return Slice.Empty;

			var bytes = new byte[count];
			prng.NextBytes(bytes);
			return new Slice(bytes, 0, count);
		}

		/// <summary>Create a new slice filled with random bytes taken from a cryptographic random number generator</summary>
		/// <param name="rng">Random generator to use (needs locking if instance is shared)</param>
		/// <param name="count">Number of random bytes to generate</param>
		/// <param name="nonZeroBytes">If true, produce a sequence of non-zero bytes.</param>
		/// <returns>Slice of <paramref name="count"/> bytes taken from <paramref name="rng"/></returns>
		/// <remarks>Warning: All RNG implementations may not be thread-safe ! If the <paramref name="rng"/> instance is shared between threads, then it may need to be locked before calling this method.</remarks>
		public static Slice Random([NotNull] System.Security.Cryptography.RandomNumberGenerator rng, int count, bool nonZeroBytes = false)
		{
			if (rng == null) throw new ArgumentNullException("rng");
			if (count < 0) throw new ArgumentOutOfRangeException("count", count, "Count cannot be negative");
			if (count == 0) return Slice.Empty;

			var bytes = new byte[count];

			if (nonZeroBytes)
				rng.GetNonZeroBytes(bytes);
			else
				rng.GetBytes(bytes);

			return new Slice(bytes, 0, count);
		}

		/// <summary>Reports the zero-based index of the first occurrence of the specified slice in this source.</summary>
		/// <param name="source">The slice Input slice</param>
		/// <param name="value">The slice to seek</param>
		/// <returns></returns>
		public static int Find(Slice source, Slice value)
		{
			const int NOT_FOUND = -1;

			SliceHelpers.EnsureSliceIsValid(ref source);
			SliceHelpers.EnsureSliceIsValid(ref value);

			int m = value.Count;
			if (m == 0) return 0;

			int n = source.Count;
			if (n == 0) return NOT_FOUND;

			if (m == n) return source.Equals(value) ? 0 : NOT_FOUND;
			if (m <= n)
			{
				byte[] src = source.Array;
				int p = source.Offset;
				byte firstByte = value[0];

				// note: this is a very simplistic way to find a value, and is optimized for the case where the separator is only one byte (most common) 
				while (n-- > 0)
				{
					if (src[p++] == firstByte)
					{ // possible match ?
						if (m == 1 || SliceHelpers.SameBytesUnsafe(src, p, value.Array, value.Offset + 1, m - 1))
						{
							return p - source.Offset - 1;
						}
					}
				}
			}

			return NOT_FOUND;
		}

		/// <summary>Concatenates all the elements of a slice array, using the specified separator between each element.</summary>
		/// <param name="separator">The slice to use as a separator. Can be empty.</param>
		/// <param name="values">An array that contains the elements to concatenate.</param>
		/// <returns>A slice that consists of the elements in a value delimited by the <paramref name="separator"/> slice. If <paramref name="values"/> is an empty array, the method returns <see cref="Slice.Empty"/>.</returns>
		/// <exception cref="ArgumentNullException">If <paramref name="values"/> is null.</exception>
		public static Slice Join(Slice separator, [NotNull] Slice[] values)
		{
			if (values == null) throw new ArgumentNullException("values");

			int count = values.Length;
			if (count == 0) return Slice.Empty;
			if (count == 1) return values[0];
			return Join(separator, values, 0, count);
		}

		/// <summary>Concatenates the specified elements of a slice array, using the specified separator between each element.</summary>
		/// <param name="separator">The slice to use as a separator. Can be empty.</param>
		/// <param name="values">An array that contains the elements to concatenate.</param>
		/// <param name="startIndex">The first element in <paramref name="values"/> to use.</param>
		/// <param name="count">The number of elements of <paramref name="values"/> to use.</param>
		/// <returns>A slice that consists of the slices in <paramref name="values"/> delimited by the <paramref name="separator"/> slice. -or- <see cref="Slice.Empty"/> if <paramref name="count"/> is zero, <paramref name="values"/> has no elements, or <paramref name="separator"/> and all the elements of <paramref name="values"/> are <see cref="Slice.Empty"/>.</returns>
		/// <exception cref="ArgumentNullException">If <paramref name="values"/> is null.</exception>
		/// <exception cref="ArgumentOutOfRangeException">If <paramref name="startIndex"/> or <paramref name="count"/> is less than zero. -or- <paramref name="startIndex"/> plus <paramref name="count"/> is greater than the number of elements in <paramref name="values"/>.</exception>
		public static Slice Join(Slice separator, [NotNull] Slice[] values, int startIndex, int count)
		{
			// Note: this method is modeled after String.Join() and should behave the same
			// - Only difference is that Slice.Nil and Slice.Empty are equivalent (either for separator, or for the elements of the array)

			if (values == null) throw new ArgumentNullException("values");
			//REVIEW: support negative indexing ?
			if (startIndex < 0) throw new ArgumentOutOfRangeException("startIndex", startIndex, "Start index must be a positive integer");
			if (count < 0) throw new ArgumentOutOfRangeException("count", count, "Count must be a positive integer");
			if (startIndex > values.Length - count) throw new ArgumentOutOfRangeException("startIndex", startIndex, "Start index must fit within the array");

			if (count == 0) return Slice.Empty;
			if (count == 1) return values[startIndex];

			int size = 0;
			for (int i = 0; i < values.Length; i++) size += values[i].Count;
			size += (values.Length - 1) * separator.Count;

			// if the size overflows, that means that the resulting buffer would need to be >= 2 GB, which is not possible!
			if (size < 0) throw new OutOfMemoryException();

			//note: we want to make sure the buffer of the writer will be the exact size (so that we can use the result as a byte[] without copying again)
			var tmp = new byte[size];
			var writer = new SliceWriter(tmp);
			for (int i = 0; i < values.Length; i++)
			{
				if (i > 0) writer.WriteBytes(separator);
				writer.WriteBytes(values[i]);
			}
			Contract.Assert(writer.Buffer.Length == size);
			return writer.ToSlice();
		}

		/// <summary>Concatenates the specified elements of a slice sequence, using the specified separator between each element.</summary>
		/// <param name="separator">The slice to use as a separator. Can be empty.</param>
		/// <param name="values">A sequence will return the elements to concatenate.</param>
		/// <returns>A slice that consists of the slices in <paramref name="values"/> delimited by the <paramref name="separator"/> slice. -or- <see cref="Slice.Empty"/> if <paramref name="values"/> has no elements, or <paramref name="separator"/> and all the elements of <paramref name="values"/> are <see cref="Slice.Empty"/>.</returns>
		/// <exception cref="ArgumentNullException">If <paramref name="values"/> is null.</exception>
		public static Slice Join(Slice separator, [NotNull] IEnumerable<Slice> values)
		{
			if (values == null) throw new ArgumentNullException("values");
			var array = (values as Slice[]) ?? values.ToArray();
			return Join(separator, array, 0, array.Length);
		}

		/// <summary>Concatenates the specified elements of a slice array, using the specified separator between each element.</summary>
		/// <param name="separator">The slice to use as a separator. Can be empty.</param>
		/// <param name="values">An array that contains the elements to concatenate.</param>
		/// <param name="startIndex">The first element in <paramref name="values"/> to use.</param>
		/// <param name="count">The number of elements of <paramref name="values"/> to use.</param>
		/// <returns>A byte array that consists of the slices in <paramref name="values"/> delimited by the <paramref name="separator"/> slice. -or- an emtpy array if <paramref name="count"/> is zero, <paramref name="values"/> has no elements, or <paramref name="separator"/> and all the elements of <paramref name="values"/> are <see cref="Slice.Empty"/>.</returns>
		/// <exception cref="ArgumentNullException">If <paramref name="values"/> is null.</exception>
		/// <exception cref="ArgumentOutOfRangeException">If <paramref name="startIndex"/> or <paramref name="count"/> is less than zero. -or- <paramref name="startIndex"/> plus <paramref name="count"/> is greater than the number of elements in <paramref name="values"/>.</exception>
		[NotNull]
		public static byte[] JoinBytes(Slice separator, [NotNull] Slice[] values, int startIndex, int count)
		{
			// Note: this method is modeled after String.Join() and should behave the same
			// - Only difference is that Slice.Nil and Slice.Empty are equivalent (either for separator, or for the elements of the array)

			if (values == null) throw new ArgumentNullException("values");
			//REVIEW: support negative indexing ?
			if (startIndex < 0) throw new ArgumentOutOfRangeException("startIndex", startIndex, "Start index must be a positive integer");
			if (count < 0) throw new ArgumentOutOfRangeException("count", count, "Count must be a positive integer");
			if (startIndex > values.Length - count) throw new ArgumentOutOfRangeException("startIndex", startIndex, "Start index must fit within the array");

			if (count == 0) return Slice.EmptyArray;
			if (count == 1) return values[0].GetBytes();

			int size = 0;
			for (int i = 0; i < values.Length; i++) size += values[i].Count;
			size += (values.Length - 1) * separator.Count;

			// if the size overflows, that means that the resulting buffer would need to be >= 2 GB, which is not possible!
			if (size < 0) throw new OutOfMemoryException();

			//note: we want to make sure the buffer of the writer will be the exact size (so that we can use the result as a byte[] without copying again)
			var tmp = new byte[size];
			int p = 0;
			for (int i = 0; i < values.Length; i++)
			{
				if (i > 0) separator.WriteTo(tmp, ref p);
				values[i].WriteTo(tmp, ref p);
			}
			Contract.Assert(p == tmp.Length);
			return tmp;
		}

		/// <summary>Concatenates the specified elements of a slice sequence, using the specified separator between each element.</summary>
		/// <param name="separator">The slice to use as a separator. Can be empty.</param>
		/// <param name="values">A sequence will return the elements to concatenate.</param>
		/// <returns>A byte array that consists of the slices in <paramref name="values"/> delimited by the <paramref name="separator"/> slice. -or- an empty array if <paramref name="values"/> has no elements, or <paramref name="separator"/> and all the elements of <paramref name="values"/> are <see cref="Slice.Empty"/>.</returns>
		/// <exception cref="ArgumentNullException">If <paramref name="values"/> is null.</exception>
		[NotNull]
		public static byte[] JoinBytes(Slice separator, [NotNull] IEnumerable<Slice> values)
		{
			if (values == null) throw new ArgumentNullException("values");
			var array = (values as Slice[]) ?? values.ToArray();
			return JoinBytes(separator, array, 0, array.Length);
		}

		/// <summary>Returns a slice array that contains the sub-slices in <paramref name="input"/> that are delimited by <paramref name="separator"/>. A parameter specifies whether to return empty array elements.</summary>
		/// <param name="input">Input slice that must be split into sub-slices</param>
		/// <param name="separator">Separator that delimits the sub-slices in <paramref name="input"/>. Cannot be empty or nil</param>
		/// <param name="options"><see cref="StringSplitOptions.RemoveEmptyEntries"/> to omit empty array alements from the array returned; or <see cref="StringSplitOptions.None"/> to include empty array elements in the array returned.</param>
		/// <returns>An array whose elements contain the sub-slices that are delimited by <paramref name="separator"/>.</returns>
		/// <exception cref="System.ArgumentException">If <paramref name="separator"/> is empty, or if <paramref name="options"/> is not one of the <see cref="StringSplitOptions"/> values.</exception>
		/// <remarks>If <paramref name="input"/> does not contain the delimiter, the returned array consists of a single element that repeats the input, or an empty array if input is itself empty.
		/// To reduce memory usage, the sub-slices returned in the array will all share the same underlying buffer of the input slice.</remarks>
		[NotNull]
		public static Slice[] Split(Slice input, Slice separator, StringSplitOptions options = StringSplitOptions.None)
		{
			// this method is made to behave the same way as String.Split(), especially the following edge cases
			// - Empty.Split(..., StringSplitOptions.None) => { Empty }
			// - Empty.Split(..., StringSplitOptions.RemoveEmptyEntries) => { }
			// differences:
			// - If input is Nil, it is considered equivalent to Empty
			// - If separator is Nil or Empty, the method throws

			var list = new List<Slice>();

			if (separator.Count <= 0) throw new ArgumentException("Separator must have at least one byte", "separator");
			if (options < StringSplitOptions.None || options > StringSplitOptions.RemoveEmptyEntries) throw new ArgumentException("options");

			bool skipEmpty = options.HasFlag(StringSplitOptions.RemoveEmptyEntries);
			if (input.Count == 0)
			{
				return skipEmpty ? Slice.EmptySliceArray : new Slice[1] { Slice.Empty };
			}

			while (input.Count > 0)
			{
				int p = Find(input, separator);
				if (p < 0)
				{ // last chunk
					break;
				}
				if (p == 0)
				{ // empty chunk
					if (!skipEmpty) list.Add(Slice.Empty);
				}
				else
				{
					list.Add(input.Substring(0, p));
				}
				// note: we checked earlier that separator.Count > 0, so we are guaranteed to advance the cursor
				input = input.Substring(p + separator.Count);
			}

			if (input.Count > 0 || !skipEmpty)
			{
				list.Add(input);
			}

			return list.ToArray();
		}

		/// <summary>Decode a Base64 encoded string into a slice</summary>
		public static Slice FromBase64(string base64String)
		{
			return base64String == null ? Slice.Nil : base64String.Length == 0 ? Slice.Empty : Slice.Create(Convert.FromBase64String(base64String));
		}

		/// <summary>Encode an unsigned 8-bit integer into a slice</summary>
		public static Slice FromByte(byte value)
		{
			return new Slice(ByteSprite, value, 1);
		}

		#region 16-bit integers

		/// <summary>Encode a signed 16-bit integer into a variable size slice (1 or 2 bytes) in little-endian</summary>
		public static Slice FromInt16(short value)
		{
			if (value >= 0)
			{
				if (value <= 255)
				{
					return Slice.FromByte((byte)value);
				}
				else
				{
					return new Slice(new byte[] { (byte)value, (byte)(value >> 8) }, 0, 2);
				}
			}

			return FromFixed16(value);
		}

		/// <summary>Encode a signed 16-bit integer into a 2-byte slice in little-endian</summary>
		public static Slice FromFixed16(short value)
		{
			return new Slice(
				new byte[]
				{ 
					(byte)value,
					(byte)(value >> 8)
				},
				0,
				2
			);
		}

		/// <summary>Encode an unsigned 16-bit integer into a variable size slice (1 or 2 bytes) in little-endian</summary>
		public static Slice FromUInt16(ushort value)
		{
			if (value <= 255)
			{
				return Slice.FromByte((byte)value);
			}
			else
			{
				return FromFixedU16(value);
			}
		}

		/// <summary>Encode an unsigned 16-bit integer into a 2-byte slice in little-endian</summary>
		/// <remarks>0x1122 => 11 22</remarks>
		public static Slice FromFixedU16(ushort value)
		{
			return new Slice(
				new byte[]
				{ 
					(byte)value,
					(byte)(value >> 8)
				},
				0,
				2
			);
		}

		/// <summary>Encode an unsigned 16-bit integer into a 2-byte slice in big-endian</summary>
		/// <remarks>0x1122 => 22 11</remarks>
		public static Slice FromFixedU16BE(ushort value)
		{
			return new Slice(
				new byte[]
				{ 
					(byte)(value >> 8),
					(byte)value
				},
				0,
				4
			);
		}

		/// <summary>Encode an unsigned 16-bit integer into 7-bit encoded unsigned int (aka 'Varint16')</summary>
		public static Slice FromVarint16(ushort value)
		{
			if (value < 128)
			{
				return FromByte((byte)value);
			}
			else
			{
				var writer = new SliceWriter(3);
				writer.WriteVarint16(value);
				return writer.ToSlice();
			}
		}

		#endregion

		#region 32-bit integers

		/// <summary>Encode a signed 32-bit integer into a variable size slice (1, 2 or 4 bytes) in little-endian</summary>
		public static Slice FromInt32(int value)
		{
			if (value >= 0)
			{
				if (value <= 255)
				{
					return Slice.FromByte((byte)value);
				}
				if (value <= 65535)
				{
					return new Slice(new byte[] { (byte)value, (byte)(value >> 8) }, 0, 2);
				}
			}

			return FromFixed32(value);
		}

		/// <summary>Encode a signed 32-bit integer into a 4-byte slice in little-endian</summary>
		public static Slice FromFixed32(int value)
		{
			return new Slice(
				new byte[]
				{ 
					(byte)value,
					(byte)(value >> 8),
					(byte)(value >> 16),
					(byte)(value >> 24)
				},
				0,
				4
			);
		}

		/// <summary>Encode an unsigned 32-bit integer into a variable size slice (1, 2 or 4 bytes) in little-endian</summary>
		public static Slice FromUInt32(uint value)
		{
			if (value <= 255)
			{
				return Slice.FromByte((byte)value);
			}
			if (value <= 65535)
			{
				return new Slice(new byte[] { (byte)value, (byte)(value >> 8) }, 0, 2);
			}

			return FromFixedU32(value);
		}

		/// <summary>Encode an unsigned 32-bit integer into a 4-byte slice in little-endian</summary>
		/// <remarks>0x11223344 => 11 22 33 44</remarks>
		public static Slice FromFixedU32(uint value)
		{
			return new Slice(
				new byte[]
				{ 
					(byte)value,
					(byte)(value >> 8),
					(byte)(value >> 16),
					(byte)(value >> 24)
				},
				0,
				4
			);
		}

		/// <summary>Encode an unsigned 32-bit integer into a 4-byte slice in big-endian</summary>
		/// <remarks>0x11223344 => 44 33 22 11</remarks>
		public static Slice FromFixedU32BE(uint value)
		{
			return new Slice(
				new byte[]
				{ 
					(byte)(value >> 24),
					(byte)(value >> 16),
					(byte)(value >> 8),
					(byte)value
				},
				0,
				4
			);
		}

		/// <summary>Encode an unsigned 32-bit integer into 7-bit encoded unsigned int (aka 'Varint32')</summary>
		public static Slice FromVarint32(uint value)
		{
			if (value < 128)
			{
				return FromByte((byte)value);
			}
			else
			{
				var writer = new SliceWriter(5);
				writer.WriteVarint32(value);
				return writer.ToSlice();
			}
		}

		#endregion

		#region 64-bit integers

		/// <summary>Encode a signed 64-bit integer into a variable size slice (1, 2, 4 or 8 bytes) in little-endian</summary>
		public static Slice FromInt64(long value)
		{
			if (value >= 0 && value <= int.MaxValue)
			{
				return FromInt32((int)value);
			}
			return FromFixed64(value);
		}

		/// <summary>Encode a signed 64-bit integer into a 8-byte slice in little-endian</summary>
		public static Slice FromFixed64(long value)
		{
			return new Slice(
				new byte[]
				{ 
					(byte)value,
					(byte)(value >> 8),
					(byte)(value >> 16),
					(byte)(value >> 24),
					(byte)(value >> 32),
					(byte)(value >> 40),
					(byte)(value >> 48),
					(byte)(value >> 56)
				}, 
				0, 
				8
			);
		}

		/// <summary>Encode an unsigned 64-bit integer into a variable size slice (1, 2, 4 or 8 bytes) in little-endian</summary>
		public static Slice FromUInt64(ulong value)
		{
			if (value <= 255)
			{
				return Slice.FromByte((byte)value);
			}
			if (value <= 65535)
			{
				return new Slice(new byte[] { (byte)value, (byte)(value >> 8) }, 0, 2);
			}

			if (value <= uint.MaxValue)
			{
				return new Slice(
					new byte[]
					{ 
						(byte)value,
						(byte)(value >> 8),
						(byte)(value >> 16),
						(byte)(value >> 24)
					},
					0,
					4
				);
			}

			return FromFixedU64(value);
		}

		/// <summary>Encode an unsigned 64-bit integer into a 8-byte slice in little-endian</summary>
		/// <remarks>0x1122334455667788 => 11 22 33 44 55 66 77 88</remarks>
		public static Slice FromFixedU64(ulong value)
		{
			return new Slice(
				new byte[]
				{
					(byte)value,
					(byte)(value >> 8),
					(byte)(value >> 16),
					(byte)(value >> 24),
					(byte)(value >> 32),
					(byte)(value >> 40),
					(byte)(value >> 48),
					(byte)(value >> 56)
				},
				0,
				8
			);
		}

		/// <summary>Encode an unsigned 64-bit integer into a 8-byte slice in big-endian</summary>
		/// <remarks>0x1122334455667788 => 88 77 66 55 44 33 22 11</remarks>
		public static Slice FromFixedU64BE(ulong value)
		{
			return new Slice(
				new byte[]
				{
					(byte)(value >> 56),
					(byte)(value >> 48),
					(byte)(value >> 40),
					(byte)(value >> 32),
					(byte)(value >> 24),
					(byte)(value >> 16),
					(byte)(value >> 8),
					(byte)value
				},
				0,
				8
			);
		}

		/// <summary>Encode an unsigned 64-bit integer into 7-bit encoded unsigned int (aka 'Varint64')</summary>
		public static Slice FromVarint64(ulong value)
		{
			if (value < 128)
			{
				return FromByte((byte)value);
			}
			else
			{
				var writer = new SliceWriter(10);
				writer.WriteVarint64(value);
				return writer.ToSlice();
			}
		}

		#endregion

		/// <summary>Create a 16-byte slice containing a System.Guid encoding according to RFC 4122 (Big Endian)</summary>
		/// <remarks>WARNING: Slice.FromGuid(guid).GetBytes() will not produce the same result as guid.ToByteArray() !
		/// If you need to produce Microsoft compatible byte arrays, use Slice.Create(guid.ToByteArray()) but then you shoud NEVER use Slice.ToGuid() to decode such a value !</remarks>
		public static Slice FromGuid(Guid value)
		{
			// UUID are stored using the RFC4122 format (Big Endian), while .NET's System.GUID use Little Endian
			// => we will convert the GUID into a UUID under the hood, and hope that it gets converted back when read from the db

			return new Uuid(value).ToSlice();
		}

		/// <summary>Create a 16-byte slice containing an RFC 4122 compliant 128-bit UUID</summary>
		/// <remarks>You should never call this method on a slice created from the result of calling System.Guid.ToByteArray() !</remarks>
		public static Slice FromUuid(Uuid value)
		{
			// UUID should already be in the RFC 4122 ordering
			return value.ToSlice();
		}

		/// <summary>Dangerously create a slice containing string converted to ASCII. All non-ASCII characters may be corrupted or converted to '?'</summary>
		/// <remarks>WARNING: if you put a string that contains non-ASCII chars, it will be silently corrupted! This should only be used to store keywords or 'safe' strings.
		/// Note: depending on your default codepage, chars from 128 to 255 may be preserved, but only if they are decoded using the same codepage at the other end !</remarks>
		public static Slice FromAscii(string text)
		{
			return text == null ? Slice.Nil : text.Length == 0 ? Slice.Empty : Slice.Create(Encoding.Default.GetBytes(text));
		}

		/// <summary>Create a slice containing the UTF-8 bytes of the string <paramref name="value"/></summary>
		public static Slice FromString(string value)
		{
			return value == null ? Slice.Nil : value.Length == 0 ? Slice.Empty : Slice.Create(Encoding.UTF8.GetBytes(value));
		}

		/// <summary>Create a slice that holds the UTF-8 encoded representation of <paramref name="value"/></summary>
		/// <param name="value"></param>
		/// <returns>The returned slice is only guaranteed to hold 1 byte for ASCII chars (0..127). For non-ASCII chars, the size can be from 1 to 6 bytes.
		/// If you need to use ASCII chars, you should use Slice.FromByte() instead</returns>
		public static Slice FromChar(char value)
		{
			if (value < 128)
			{ // ASCII
				return Slice.FromByte((byte)value);
			}

			// note: Encoding.UTF8.GetMaxByteCount(1) returns 6, but allocate 8 to stay aligned
			var tmp = new byte[8];
			int n = Encoding.UTF8.GetBytes(new char[] { value }, 0, 1, tmp, 0);
			return n == 1 ? FromByte(tmp[0]) : new Slice(tmp, 0, n);
		}

		/// <summary>Convert an hexadecimal digit (0-9A-Fa-f) into the corresponding decimal value</summary>
		/// <param name="c">Hexadecimal digit (case insensitive)</param>
		/// <returns>Decimal value between 0 and 15, or an exception</returns>
		private static int NibbleToDecimal(char c)
		{
			int x = c - 48;
			if (x < 10) return x;
			if (x >= 17 && x <= 42) return x - 7;
			if (x >= 49 && x <= 74) return x - 39;
			throw new FormatException("Input is not a valid hexadecimal digit");
		}

		/// <summary>Convert an hexadecimal encoded string ("1234AA7F") into a slice</summary>
		/// <param name="hexaString">String contains a sequence of pairs of hexadecimal digits with no separating spaces.</param>
		/// <returns>Slice containing the decoded byte array, or an exeception if the string is empty or has an odd length</returns>
		public static Slice FromHexa(string hexaString)
		{
			if (string.IsNullOrEmpty(hexaString)) return hexaString == null ? Slice.Nil : Slice.Empty;

			if ((hexaString.Length & 1) != 0) throw new ArgumentException("Hexadecimal string must be of even length", "hexaString");

			var buffer = new byte[hexaString.Length >> 1];
			for (int i = 0; i < hexaString.Length; i += 2)
			{
				buffer[i >> 1] = (byte) ((NibbleToDecimal(hexaString[i]) << 4) | NibbleToDecimal(hexaString[i + 1]));
			}
			return new Slice(buffer, 0, buffer.Length);
		}

		/// <summary>Returns true is the slice is not null</summary>
		/// <remarks>An empty slice is NOT considered null</remarks>
		public bool HasValue { get { return this.Array != null; } }

		/// <summary>Returns true if the slice is null</summary>
		/// <remarks>An empty slice is NOT considered null</remarks>
		public bool IsNull { get { return this.Array == null; } }

		/// <summary>Return true if the slice is not null but contains 0 bytes</summary>
		/// <remarks>A null slice is NOT empty</remarks>
		public bool IsEmpty { get { return this.Count == 0 && this.Array != null; } }

		/// <summary>Returns true if the slice is null or empty, or false if it contains at least one byte</summary>
		public bool IsNullOrEmpty { get { return this.Count == 0; } }

		/// <summary>Returns true if the slice contains at least one byte, or false if it is null or empty</summary>
		public bool IsPresent { get { return this.Count > 0; } }

		/// <summary>Return a byte array containing all the bytes of the slice, or null if the slice is null</summary>
		/// <returns>Byte array with a copy of the slice, or null</returns>
		[Pure][CanBeNull]
		public byte[] GetBytes()
		{
			if (this.Count == 0) return this.Array == null ? null : Slice.EmptyArray;
			SliceHelpers.EnsureSliceIsValid(ref this);

			var tmp = new byte[this.Count];
			SliceHelpers.CopyBytesUnsafe(tmp, 0, this.Array, this.Offset, this.Count);
			return tmp;
		}

		/// <summary>Return a byte array containing a subset of the bytes of the slice, or null if the slice is null</summary>
		/// <returns>Byte array with a copy of a subset of the slice, or null</returns>
		[Pure][CanBeNull]
		public byte[] GetBytes(int offset, int count)
		{
			//TODO: throw if this.Array == null ? (what does "Slice.Nil.GetBytes(..., 0)" mean ?)

			if (offset < 0) throw new ArgumentOutOfRangeException("offset");
			if (count < 0 || offset + count > this.Count) throw new ArgumentOutOfRangeException("count");

			if (count == 0) return this.Array == null ? null : Slice.EmptyArray;
			SliceHelpers.EnsureSliceIsValid(ref this);

			var tmp = new byte[count];
			SliceHelpers.CopyBytesUnsafe(tmp, 0, this.Array, this.Offset + offset, count);
			return tmp;
		}

		/// <summary>Return a stream that wraps this slice</summary>
		/// <returns>Stream that will read the slice from the start.</returns>
		/// <remarks>
		/// You can use this method to convert text into specific encodings, load bitmaps (JPEG, PNG, ...), or any serialization format that requires a Stream or TextReader instance.
		/// Disposing this stream will have no effect on the slice.
		/// </remarks>
		[Pure][NotNull]
		public SliceStream AsStream()
		{
			SliceHelpers.EnsureSliceIsValid(ref this);
			return new SliceStream(this);
		}

		/// <summary>Stringify a slice containing only ASCII chars</summary>
		/// <returns>ASCII string, or null if the slice is null</returns>
		[Pure][CanBeNull]
		public string ToAscii()
		{
			if (this.Count == 0) return this.HasValue ? String.Empty : default(string);
			SliceHelpers.EnsureSliceIsValid(ref this);
			return Encoding.Default.GetString(this.Array, this.Offset, this.Count);
		}

		/// <summary>Stringify a slice containing an UTF-8 encoded string</summary>
		/// <returns>Unicode string, or null if the slice is null</returns>
		[Pure][CanBeNull]
		public string ToUnicode()
		{
			if (this.Count == 0) return this.HasValue ? String.Empty : default(string);
			SliceHelpers.EnsureSliceIsValid(ref this);
			return Encoding.UTF8.GetString(this.Array, this.Offset, this.Count);
		}

		/// <summary>Converts a slice using Base64 encoding</summary>
		[Pure][CanBeNull]
		public string ToBase64()
		{
			if (this.Count == 0) return this.Array == null ? null : String.Empty;
			SliceHelpers.EnsureSliceIsValid(ref this);
			return Convert.ToBase64String(this.Array, this.Offset, this.Count);
		}

		/// <summary>Converts a slice into a string with each byte encoded into hexadecimal (lowercase)</summary>
		/// <returns>"0123456789abcdef"</returns>
		[Pure][CanBeNull]
		public string ToHexaString()
		{
			if (this.Count == 0) return this.Array == null ? null : String.Empty;
			var buffer = this.Array;
			int p = this.Offset;
			int n = this.Count;
			var sb = new StringBuilder(n * 2);
			while (n-- > 0)
			{
				byte b = buffer[p++];
				int x = b >> 4;
				sb.Append((char)(x + (x < 10 ? 48 : 87)));
				x = b & 0xF;
				sb.Append((char)(x + (x < 10 ? 48 : 87)));
			}
			return sb.ToString();
		}

		/// <summary>Converts a slice into a string with each byte encoded into hexadecimal (uppercase) separated by a char</summary>
		/// <param name="sep">Character used to separate the hexadecimal pairs (ex: ' ')</param>
		/// <returns>"01 23 45 67 89 ab cd ef"</returns>
		[Pure][CanBeNull]
		public string ToHexaString(char sep)
		{
			if (this.Count == 0) return this.Array == null ? null : String.Empty;
			var buffer = this.Array;
			int p = this.Offset;
			int n = this.Count;
			var sb = new StringBuilder(n * 3);
			while (n-- > 0)
			{
				if (sb.Length > 0) sb.Append(sep);
				byte b = buffer[p++];
				int x = b >> 4;
				sb.Append((char)(x + (x < 10 ? 48 : 55)));
				x = b & 0xF;
				sb.Append((char)(x + (x < 10 ? 48 : 55)));
			}
			return sb.ToString();
		}

		private static StringBuilder EscapeString(StringBuilder sb, byte[] buffer, int offset, int count, Encoding encoding)
		{
			if (sb == null) sb = new StringBuilder(count + 16);
			foreach(var c in encoding.GetChars(buffer, offset, count))
			{
				if ((c >= ' ' && c <= '~') || (c >= 880 && c <= 2047) || (c >= 12352 && c <= 12591))
					sb.Append(c);
				else if (c == '\n')
					sb.Append(@"\n");
				else if (c == '\r')
					sb.Append(@"\r");
				else if (c == '\t')
					sb.Append(@"\t");
				else if (c > 127)
					sb.Append(@"\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
				else // pas clean!
					sb.Append(@"\x").Append(((int)c).ToString("x2", CultureInfo.InvariantCulture));
			}
			return sb;
		}

		/// <summary>Helper method that dumps the slice as a string (if it contains only printable ascii chars) or an hex array if it contains non printable chars. It should only be used for logging and troubleshooting !</summary>
		/// <returns>Returns either "'abc'", "&lt;00 42 7F&gt;", or "{ ...JSON... }". Returns "''" for Slice.Empty, and "" for <see cref="Slice.Nil"/></returns>
		[Pure][NotNull]
		public string ToAsciiOrHexaString() //REVIEW: rename this to ToPrintableString() ?
		{
			//REVIEW: rename this to ToFriendlyString() ? or ToLoggableString() ?
			if (this.Count == 0) return this.Array != null ? "''" : String.Empty;

			var buffer = this.Array;
			int n = this.Count;
			int p = this.Offset;

			// look for UTF-8 BOM
			if (n >= 3 && buffer[p] == 0xEF && buffer[p + 1] == 0xBB && buffer[p + 2] == 0xBF)
			{ // this is supposed to be an UTF-8 string
				return EscapeString(new StringBuilder(n).Append('\''), buffer, p + 3, n - 3, Encoding.UTF8).Append('\'').ToString();
			}

			if (n >= 2)
			{
				// look for JSON objets or arrays
				if ((buffer[p] == '{' && buffer[p + n - 1] == '}') || (buffer[p] == '[' && buffer[p + n - 1] == ']'))
				{
					return EscapeString(new StringBuilder(n + 16), buffer, p, n, Encoding.UTF8).Append('\'').ToString();
				}
			}

			// do a first path on the slice to look for binary of possible text
			bool mustEscape = false;
			while (n-- > 0)
			{
				byte b = buffer[p++];
				if (b >= 32 && b < 127) continue;

				// we accept via escaping the following special chars: CR, LF, TAB
				if (b == 10 || b == 13 || b == 9)
				{
					mustEscape = true;
					continue;
				}

				//TODO: are there any chars above 128 that could be accepted ?

				// this looks like binary
				return "<" + ToHexaString(' ') + ">";
			}

			if (!mustEscape)
			{ // only printable chars found
				return new StringBuilder(n + 2).Append('\'').Append(Encoding.ASCII.GetString(buffer, this.Offset, this.Count)).Append('\'').ToString();
			}
			else
			{ // some escaping required
				return EscapeString(new StringBuilder(n + 2).Append('\''), buffer, this.Offset, this.Count, Encoding.UTF8).Append('\'').ToString();
			}
		}

		/// <summary>Converts a slice into a byte</summary>
		/// <returns>Value of the first and only byte of the slice, or 0 if the slice is null or empty.</returns>
		/// <exception cref="System.FormatException">If the slice has more than one byte</exception>
		[Pure]
		public byte ToByte()
		{
			if (this.Count == 0) return 0;
			if (this.Count > 1) throw new FormatException("Cannot convert slice into a Byte because it is larger than 1 byte");
			SliceHelpers.EnsureSliceIsValid(ref this);
			return this.Array[this.Offset];
		}

		/// <summary>Converts a slice into a boolean.</summary>
		/// <returns>False if the slice is empty, or is equal to the byte 0; otherwise, true.</returns>
		[Pure]
		public bool ToBool()
		{
			SliceHelpers.EnsureSliceIsValid(ref this);
			// Anything appart from nil/empty, or the byte 0 itself is considered truthy.
			return this.Count > 1 || (this.Count == 1 && this.Array[this.Offset] != 0);
			//TODO: consider checking if the slice consist of only zeroes ? (ex: Slice.FromFixed32(0) could be considered falsy ...)
		}

		/// <summary>Converts a slice into a little-endian encoded, signed 16-bit integer.</summary>
		/// <returns>0 of the slice is null or empty, a signed integer, or an error if the slice has more than 2 bytes</returns>
		/// <exception cref="System.FormatException">If there are more than 2 bytes in the slice</exception>
		[Pure]
		public short ToInt16()
		{
			SliceHelpers.EnsureSliceIsValid(ref this);
			switch (this.Count)
			{
				case 0: return 0;
				case 1: return this.Array[this.Offset];
				case 2: return (short) (this.Array[this.Offset] | (this.Array[this.Offset + 1] << 8));
				default: throw new FormatException("Cannot convert slice into an Int16 because it is larger than 2 bytes");
			}
		}

		/// <summary>Converts a slice into a little-endian encoded, unsigned 16-bit integer.</summary>
		/// <returns>0 of the slice is null or empty, an unsigned integer, or an error if the slice has more than 2 bytes</returns>
		/// <exception cref="System.FormatException">If there are more than 2 bytes in the slice</exception>
		[Pure]
		public ushort ToUInt16()
		{
			SliceHelpers.EnsureSliceIsValid(ref this);
			switch (this.Count)
			{
				case 0: return 0;
				case 1: return this.Array[this.Offset];
				case 2: return (ushort)(this.Array[this.Offset] | (this.Array[this.Offset + 1] << 8));
				default: throw new FormatException("Cannot convert slice into an UInt16 because it is larger than 2 bytes");
			}
		}

		/// <summary>Converts a slice into a little-endian encoded, signed 32-bit integer.</summary>
		/// <returns>0 of the slice is null or empty, a signed integer, or an error if the slice has more than 4 bytes</returns>
		/// <exception cref="System.FormatException">If there are more than 4 bytes in the slice</exception>
		[Pure]
		public int ToInt32()
		{
			if (this.Count == 0) return 0;
			if (this.Count > 4) throw new FormatException("Cannot convert slice into an Int32 because it is larger than 4 bytes");
			SliceHelpers.EnsureSliceIsValid(ref this);

			var buffer = this.Array;
			int n = this.Count;
			int p = this.Offset + n - 1;
			int value = 0;

			while (n-- > 0)
			{
				value = (value << 8) | buffer[p--];
			}
			return value;
		}

		/// <summary>Converts a slice into a little-endian encoded, unsigned 32-bit integer.</summary>
		/// <returns>0 of the slice is null or empty, an unsigned integer, or an error if the slice has more than 4 bytes</returns>
		/// <exception cref="System.FormatException">If there are more than 4 bytes in the slice</exception>
		[Pure]
		public uint ToUInt32()
		{
			if (this.Count == 0) return 0;
			if (this.Count > 4) throw new FormatException("Cannot convert slice into an UInt32 because it is larger than 4 bytes");
			SliceHelpers.EnsureSliceIsValid(ref this);

			var buffer = this.Array;
			int n = this.Count;
			int p = this.Offset + n - 1;
			uint value = 0;

			while (n-- > 0)
			{
				value = (value << 8) | buffer[p--];
			}
			return value;
		}

		/// <summary>Converts a slice into a little-endian encoded, signed 64-bit integer.</summary>
		/// <returns>0 of the slice is null or empty, a signed integer, or an error if the slice has more than 8 bytes</returns>
		/// <exception cref="System.FormatException">If there are more than 8 bytes in the slice</exception>
		[Pure]
		public long ToInt64()
		{
			if (this.Count == 0) return 0L;
			if (this.Count > 8) throw new FormatException("Cannot convert slice into an Int64 because it is larger than 8 bytes");
			SliceHelpers.EnsureSliceIsValid(ref this);

			var buffer = this.Array;
			int n = this.Count;
			int p = this.Offset + n - 1;
			long value = 0;

			while (n-- > 0)
			{
				value = (value << 8) | buffer[p--];
			}

			return value;
		}

		/// <summary>Converts a slice into a little-endian encoded, unsigned 64-bit integer.</summary>
		/// <returns>0 of the slice is null or empty, an unsigned integer, or an error if the slice has more than 8 bytes</returns>
		/// <exception cref="System.FormatException">If there are more than 8 bytes in the slice</exception>
		[Pure]
		public ulong ToUInt64()
		{
			if (this.Count == 0) return 0L;
			if (this.Count > 8) throw new FormatException("Cannot convert slice into an UInt64 because it is larger than 8 bytes");
			SliceHelpers.EnsureSliceIsValid(ref this);

			var buffer = this.Array;
			int n = this.Count;
			int p = this.Offset + n - 1;
			ulong value = 0;

			while (n-- > 0)
			{
				value = (value << 8) | buffer[p--];
			}

			return value;
		}

		/// <summary>Read a variable-length, little-endian encoded, unsigned integer from a specific location in the slice</summary>
		/// <param name="offset">Relative offset of the first byte</param>
		/// <param name="bytes">Number of bytes to read (up to 8)</param>
		/// <returns>Decoded unsigned integer.</returns>
		/// <exception cref="System.ArgumentOutOfRangeException">If <paramref name="bytes"/> is less than zero, or more than 8.</exception>
		[Pure]
		public ulong ReadUInt64(int offset, int bytes)
		{
			if (bytes < 0 || bytes > 8) throw new ArgumentOutOfRangeException("bytes");

			ulong value = 0;
			var buffer = this.Array;
			int p = UnsafeMapToOffset(offset);
			if (bytes > 0)
			{
				value = buffer[p++];
				--bytes;

				while (bytes-- > 0)
				{
					value <<= 8;
					value |= buffer[p++];
				}
			}
			return value;
		}

		/// <summary>Converts a slice into a Guid.</summary>
		/// <returns>Native Guid decoded from the Slice.</returns>
		/// <remarks>The slice can either be a 16-byte RFC4122 GUID, or an ASCII string of 36 chars</remarks>
		[Pure]
		public Guid ToGuid()
		{
			if (this.Count == 0) return default(Guid);
			SliceHelpers.EnsureSliceIsValid(ref this);

			if (this.Count == 16)
			{ // direct byte array

				// UUID are stored using the RFC4122 format (Big Endian), while .NET's System.GUID use Little Endian
				// we need to swap the byte order of the Data1, Data2 and Data3 chunks, to ensure that Guid.ToString() will return the proper value.

				return new Uuid(this).ToGuid();
			}

			if (this.Count == 36)
			{ // string representation (ex: "da846709-616d-4e82-bf55-d1d3e9cde9b1")
				return Guid.Parse(this.ToAscii());
			}

			throw new FormatException("Cannot convert slice into a Guid because it has an incorrect size");
		}

		/// <summary>Converts a slice into an Uuid.</summary>
		/// <returns>Uuid decoded from the Slice.</returns>
		/// <remarks>The slice can either be a 16-byte RFC4122 GUID, or an ASCII string of 36 chars</remarks>
		[Pure]
		public Uuid ToUuid()
		{
			if (this.Count == 0) return default(Uuid);
			SliceHelpers.EnsureSliceIsValid(ref this);

			if (this.Count == 16)
			{
				return new Uuid(this);
			}

			if (this.Count == 36)
			{
				return Uuid.Parse(this.ToAscii());
			}

			throw new FormatException("Cannot convert slice into Uuid because it has an incorrect size");
		}

		/// <summary>Returns a new slice that contains an isolated copy of the buffer</summary>
		/// <returns>Slice that is equivalent, but is isolated from any changes to the buffer</returns>
		[Pure]
		public Slice Memoize()
		{
			if (this.Count == 0) return this.Array == null ? Slice.Nil : Slice.Empty;
			return new Slice(GetBytes(), 0, this.Count);
		}

		/// <summary>Map an offset in the slice into the absolute offset in the buffer, without any bound checking</summary>
		/// <param name="index">Relative offset (negative values mean from the end)</param>
		/// <returns>Absolute offset in the buffer</returns>
		private int UnsafeMapToOffset(int index)
		{
			int p = NormalizeIndex(index);
			Contract.Requires(p >= 0 & p < this.Count);
			return this.Offset + p;
		}

		/// <summary>Map an offset in the slice into the absolute offset in the buffer</summary>
		/// <param name="index">Relative offset (negative values mean from the end)</param>
		/// <returns>Absolute offset in the buffer</returns>
		/// <exception cref="IndexOutOfRangeException">If the index is outside the slice</exception>
		private int MapToOffset(int index)
		{
			int p = NormalizeIndex(index);
			if (p < 0 || p >= this.Count) FailIndexOutOfBound(index);
			checked { return this.Offset + p; }
		}

		/// <summary>Normalize negative index values into offset from the start</summary>
		/// <param name="index">Relative offset (negative values mean from the end)</param>
		/// <returns>Relative offset from the start of the slice</returns>
		private int NormalizeIndex(int index)
		{
			checked { return index < 0 ? index + this.Count : index; }
		}

		/// <summary>Returns the value of one byte in the slice</summary>
		/// <param name="index">Offset of the byte (negative values means start from the end)</param>
		public byte this[int index]
		{
			get { return this.Array[MapToOffset(index)]; }
		}

		/// <summary>Returns a substring of the current slice that fits withing the specified index range</summary>
		/// <param name="start">The starting position of the substring. Positive values means from the start, negative values means from the end</param>
		/// <param name="end">The end position (exlucded) of the substring. Positive values means from the start, negative values means from the end</param>
		/// <returns>Subslice</returns>
		public Slice this[int start, int end]
		{
			get
			{
				start = NormalizeIndex(start);
				end = NormalizeIndex(end);

				// bound check
				if (start < 0) start = 0;
				if (end > this.Count) end = this.Count;

				if (start >= end) return Slice.Empty;
				if (start == 0 && end == this.Count) return this;

				checked { return new Slice(this.Array, this.Offset + start, end - start); }
			}
		}

		[ContractAnnotation("=> halt")]
		private static void FailIndexOutOfBound(int index)
		{
			throw new IndexOutOfRangeException("Index is outside the slice");
		}

		/// <summary>Copy this slice into another buffer, and move the cursor</summary>
		/// <param name="buffer">Buffer where to copy this slice</param>
		/// <param name="cursor">Offset into the destination buffer</param>
		public void WriteTo(byte[] buffer, ref int cursor)
		{
			SliceHelpers.EnsureBufferIsValid(buffer, cursor, this.Count);
			SliceHelpers.EnsureSliceIsValid(ref this);

			if (this.Count > 0)
			{
				SliceHelpers.CopyBytes(buffer, cursor, this.Array, this.Offset, this.Count);
				cursor += this.Count;
			}
		}

		/// <summary>Copy this slice into another buffer</summary>
		/// <param name="buffer">Buffer where to copy this slice</param>
		/// <param name="offset">Offset into the destination buffer</param>
		public void CopyTo(byte[] buffer, int offset)
		{
			SliceHelpers.EnsureBufferIsValid(buffer, offset, this.Count);
			SliceHelpers.EnsureSliceIsValid(ref this);

			SliceHelpers.CopyBytesUnsafe(buffer, offset, this.Array, this.Offset, this.Count);
		}

		/// <summary>Retrieves a substring from this instance. The substring starts at a specified character position.</summary>
		/// <param name="offset">The starting position of the substring. Positive values mmeans from the start, negative values means from the end</param>
		/// <returns>A slice that is equivalent to the substring that begins at <paramref name="offset"/> (from the start or the end depending on the sign) in this instance, or Slice.Empty if <paramref name="offset"/> is equal to the length of the slice.</returns>
		/// <remarks>The substring does not copy the original data, and refers to the same buffer as the original slice. Any change to the parent slice's buffer will be seen by the substring. You must call Memoize() on the resulting substring if you want a copy</remarks>
		/// <example>{"ABCDE"}.Substring(0) => {"ABC"}
		/// {"ABCDE"}.Substring(1} => {"BCDE"}
		/// {"ABCDE"}.Substring(-2} => {"DE"}
		/// {"ABCDE"}.Substring(5} => Slice.Empty
		/// Slice.Empty.Substring(0) => Slice.Empty
		/// Slice.Nil.Substring(0) => Slice.Emtpy
		/// </example>
		/// <exception cref="System.ArgumentOutOfRangeException"><paramref name="offset"/> indicates a position not within this instance, or <paramref name="offset"/> is less than zero</exception>
		[Pure]
		public Slice Substring(int offset)
		{
			if (offset == 0) return this;

			// negative values means from the end
			if (offset < 0) offset = this.Count + offset;

			if (offset < 0) throw new ArgumentOutOfRangeException("offset", "Offset cannot be less then start of the slice");
			if (offset > this.Count) throw new ArgumentOutOfRangeException("offset", "Offset cannot be larger than end of slice");

			return this.Count == offset ? Slice.Empty : new Slice(this.Array, this.Offset + offset, this.Count - offset);
		}

		/// <summary>Retrieves a substring from this instance. The substring starts at a specified character position and has a specified length.</summary>
		/// <param name="offset">The starting position of the substring. Positive values means from the start, negative values means from the end</param>
		/// <param name="count">Number of bytes in the substring</param>
		/// <returns>A slice that is equivalent to the substring of length <paramref name="count"/> that begins at <paramref name="offset"/> (from the start or the end depending on the sign) in this instance, or Slice.Empty if count is zero.</returns>
		/// <remarks>The substring does not copy the original data, and refers to the same buffer as the original slice. Any change to the parent slice's buffer will be seen by the substring. You must call Memoize() on the resulting substring if you want a copy</remarks>
		/// <example>{"ABCDE"}.Substring(0, 3) => {"ABC"}
		/// {"ABCDE"}.Substring(1, 3} => {"BCD"}
		/// {"ABCDE"}.Substring(-2, 2} => {"DE"}
		/// Slice.Empty.Substring(0, 0) => Slice.Empty
		/// Slice.Nil.Substring(0, 0) => Slice.Emtpy
		/// </example>
		/// <exception cref="System.ArgumentOutOfRangeException"><paramref name="offset"/> plus <paramref name="count"/> indicates a position not within this instance, or <paramref name="offset"/> or <paramref name="count"/> is less than zero</exception>
		[Pure]
		public Slice Substring(int offset, int count)
		{
			if (count == 0) return Slice.Empty;

			// negative values means from the end
			if (offset < 0) offset = this.Count + offset;

			if (offset < 0 || offset >= this.Count) throw new ArgumentOutOfRangeException("offset", "Offset must be inside the slice");
			if (count < 0) throw new ArgumentOutOfRangeException("count", "Count must be a positive integer");
			if (offset > this.Count - count) throw new ArgumentOutOfRangeException("count", "Offset and count must refer to a location within the slice");

			return new Slice(this.Array, this.Offset + offset, count);
		}

		/// <summary>Returns a slice array that contains the sub-slices in this instance that are delimited by the specified separator</summary>
		/// <param name="separator">The slice that delimits the sub-slices in this instance.</param>
		/// <param name="options"><see cref="StringSplitOptions.RemoveEmptyEntries"/> to omit empty array elements from the array returned; or <see cref="StringSplitOptions.None"/> to include empty array elements in the array returned.</param>
		/// <returns>An array whose elements contains the sub-slices in this instance that are delimited by the value of <paramref name="separator"/>.</returns>
		[Pure]
		public Slice[] Split(Slice separator, StringSplitOptions options = StringSplitOptions.None)
		{
			return Split(this, separator, options);
		}

		/// <summary>Reports the zero-based index of the first occurence of the specified slice in this instance.</summary>
		/// <param name="value">The slice to seek</param>
		/// <returns>The zero-based index of <paramref name="value"/> if that slice is found, or -1 if it is not. If <paramref name="value"/> is <see cref="Slice.Empty"/>, then the return value is -1.</returns>
		[Pure]
		public int IndexOf(Slice value)
		{
			return Find(this, value);
		}

		/// <summary>Reports the zero-based index of the first occurence of the specified slice in this instance. The search starts at a specified position.</summary>
		/// <param name="value">The slice to seek</param>
		/// <param name="startIndex">The search starting position</param>
		/// <returns>The zero-based index of <paramref name="value"/> if that slice is found, or -1 if it is not. If <paramref name="value"/> is <see cref="Slice.Empty"/>, then the return value is startIndex</returns>
		[Pure]
		public int IndexOf(Slice value, int startIndex)
		{
			//REVIEW: support negative indexing ?
			if (startIndex < 0 || startIndex > this.Count) throw new ArgumentOutOfRangeException("startIndex", startIndex, "Start index must be inside the buffer");
			if (this.Count == 0)
			{
				return value.Count == 0 ? startIndex : -1;
			}
			var tmp = startIndex == 0 ? this : new Slice(this.Array, this.Offset + startIndex, this.Count - startIndex);
			return Find(tmp, value);
		}

		/// <summary>Determines whether the beginning of this slice instance matches a specified slice.</summary>
		/// <param name="value">The slice to compare</param>
		/// <returns><b>true</b> if <paramref name="value"/> matches the beginning of this slice; otherwise, <b>false</b></returns>
		[Pure]
		public bool StartsWith(Slice value)
		{
			if (!value.HasValue) throw new ArgumentNullException("value");

			// any strings starts with the empty string
			if (value.Count == 0) return true;

			// prefix cannot be bigger
			if (value.Count > this.Count) return false;

			return SliceHelpers.SameBytes(this.Array, this.Offset, value.Array, value.Offset, value.Count);
		}

		/// <summary>Determines whether the end of this slice instance matches a specified slice.</summary>
		/// <param name="value">The slice to compare to the substring at the end of this instance.</param>
		/// <returns><b>true</b> if <paramref name="value"/> matches the end of this slice; otherwise, <b>false</b></returns>
		[Pure]
		public bool EndsWith(Slice value)
		{
			if (!value.HasValue) throw new ArgumentNullException("value");

			// any strings ends with the empty string
			if (value.Count == 0) return true;

			// suffix cannot be bigger
			if (value.Count > this.Count) return false;

			return SliceHelpers.SameBytes(this.Array, this.Offset + this.Count - value.Count, value.Array, value.Offset, value.Count);
		}

		/// <summary>Equivalent of StartsWith, but the returns false if both slices are identical</summary>
		[Pure]
		public bool PrefixedBy(Slice parent)
		{
			// empty is a parent of everyone
			if (parent.Count == 0) return true;

			// we must have at least one more byte then the parent
			if (this.Count <= parent.Count) return false;

			// must start with the same bytes
			return SliceHelpers.SameBytes(parent.Array, parent.Offset, this.Array, this.Offset, parent.Count);
		}

		/// <summary>Equivalent of EndsWith, but the returns false if both slices are identical</summary>
		public bool SuffixedBy(Slice parent)
		{
			// empty is a parent of everyone
			if (parent.IsNullOrEmpty) return true;
			// empty is not a child of anything
			if (this.IsNullOrEmpty) return false;

			// we must have at least one more byte then the parent
			if (this.Count <= parent.Count) return false;

			// must start with the same bytes
			return SliceHelpers.SameBytes(parent.Array, parent.Offset + this.Count - parent.Count, this.Array, this.Offset, parent.Count);
		}

		/// <summary>Append/Merge a slice at the end of the current slice</summary>
		/// <param name="tail">Slice that must be appended</param>
		/// <returns>Merged slice if both slices are contigous, or a new slice containg the content of the current slice, followed by the tail slice</returns>
		[Pure]
		public Slice Concat(Slice tail)
		{
			if (tail.Count == 0) return this;
			if (this.Count == 0) return tail;

			SliceHelpers.EnsureSliceIsValid(ref tail);
			SliceHelpers.EnsureSliceIsValid(ref this);

			// special case: adjacent segments ?
			if (object.ReferenceEquals(this.Array, tail.Array) && this.Offset + this.Count == tail.Offset)
			{
				return new Slice(this.Array, this.Offset, this.Count + tail.Count);
			}

			byte[] tmp = new byte[this.Count + tail.Count];
			SliceHelpers.CopyBytesUnsafe(tmp, 0, this.Array, this.Offset, this.Count);
			SliceHelpers.CopyBytesUnsafe(tmp, this.Count, tail.Array, tail.Offset, tail.Count);
			return new Slice(tmp, 0, tmp.Length);
		}

		/// <summary>Append an array of slice at the end of the current slice, all sharing the same buffer</summary>
		/// <param name="slices">Slices that must be appended</param>
		/// <returns>Array of slices (for all keys) that share the same underlying buffer</returns>
		[Pure][NotNull]
		public Slice[] ConcatRange([NotNull] Slice[] slices)
		{
			if (slices == null) throw new ArgumentNullException("slices");
			SliceHelpers.EnsureSliceIsValid(ref this);

			// pre-allocate by computing final buffer capacity
			var prefixSize = this.Count;
			var capacity = slices.Sum((slice) => prefixSize + slice.Count);
			var writer = new SliceWriter(capacity);
			var next = new List<int>(slices.Length);

			//TODO: use multiple buffers if item count is huge ?

			foreach (var slice in slices)
			{
				writer.WriteBytes(this);
				writer.WriteBytes(slice);
				next.Add(writer.Position);
			}

			return FdbKey.SplitIntoSegments(writer.Buffer, 0, next);
		}

		/// <summary>Append a sequence of slice at the end of the current slice, all sharing the same buffer</summary>
		/// <param name="slices">Slices that must be appended</param>
		/// <returns>Array of slices (for all keys) that share the same underlying buffer</returns>
		[Pure][NotNull]
		public Slice[] ConcatRange([NotNull] IEnumerable<Slice> slices)
		{
			if (slices == null) throw new ArgumentNullException("slices");

			// use optimized version for arrays
			var array = slices as Slice[];
			if (array != null) return ConcatRange(array);

			var next = new List<int>();
			var writer = SliceWriter.Empty;

			//TODO: use multiple buffers if item count is huge ?

			foreach (var slice in slices)
			{
				writer.WriteBytes(this);
				writer.WriteBytes(slice);
				next.Add(writer.Position);
			}

			return FdbKey.SplitIntoSegments(writer.Buffer, 0, next);

		}

		/// <summary>Concatenate two slices together</summary>
		public static Slice Concat(Slice a, Slice b)
		{
			return a.Concat(b);
		}

		/// <summary>Concatenate three slices together</summary>
		public static Slice Concat(Slice a, Slice b, Slice c)
		{
			int count = a.Count + b.Count + c.Count;
			if (count == 0) return Slice.Empty;
			var writer = new SliceWriter(count);
			writer.WriteBytes(a);
			writer.WriteBytes(b);
			writer.WriteBytes(c);
			return writer.ToSlice();
		}

		/// <summary>Concatenate an array of slices into a single slice</summary>
		public static Slice Concat(params Slice[] args)
		{
			int count = 0;
			for (int i = 0; i < args.Length; i++) count += args[i].Count;
			if (count == 0) return Slice.Empty;
			var writer = new SliceWriter(count);
			for (int i = 0; i < args.Length; i++) writer.WriteBytes(args[i]);
			return writer.ToSlice();
		}

		/// <summary>Implicitly converts a Slice into an ArraySegment&lt;byte&gt;</summary>
		public static implicit operator ArraySegment<byte>(Slice value)
		{
			if (!value.HasValue) return default(ArraySegment<byte>);
			return new ArraySegment<byte>(value.Array, value.Offset, value.Count);
		}

		/// <summary>Implicitly converts an ArraySegment&lt;byte&gt; into a Slice</summary>
		public static implicit operator Slice(ArraySegment<byte> value)
		{
			return new Slice(value.Array, value.Offset, value.Count);
		}

		#region Slice arithmetics...

		/// <summary>Compare two slices for equality</summary>
		/// <returns>True if the slices contains the same bytes</returns>
		public static bool operator ==(Slice a, Slice b)
		{
			return a.Equals(b);
		}

		/// <summary>Compare two slices for inequality</summary>
		/// <returns>True if the slice do not contain the same bytes</returns>
		public static bool operator !=(Slice a, Slice b)
		{
			return !a.Equals(b);
		}

		/// <summary>Compare two slices</summary>
		/// <returns>True if <paramref name="a"/> is lexicographically less than <paramref name="a"/>; otherwise, false.</returns>
		public static bool operator <(Slice a, Slice b)
		{
			return a.CompareTo(b) < 0;
		}

		/// <summary>Compare two slices</summary>
		/// <returns>True if <paramref name="a"/> is lexicographically less than or equal to <paramref name="a"/>; otherwise, false.</returns>
		public static bool operator <=(Slice a, Slice b)
		{
			return a.CompareTo(b) <= 0;
		}

		/// <summary>Compare two slices</summary>
		/// <returns>True if <paramref name="a"/> is lexicographically greater than <paramref name="a"/>; otherwise, false.</returns>
		public static bool operator >(Slice a, Slice b)
		{
			return a.CompareTo(b) > 0;
		}

		/// <summary>Compare two slices</summary>
		/// <returns>True if <paramref name="a"/> is lexicographically greater than or equal to <paramref name="a"/>; otherwise, false.</returns>
		public static bool operator >=(Slice a, Slice b)
		{
			return a.CompareTo(b) >= 0;
		}

		/// <summary>Append/Merge two slices together</summary>
		/// <param name="a">First slice</param>
		/// <param name="b">Second slice</param>
		/// <returns>Merged slices if both slices are contigous, or a new slice containg the content of the first slice, followed by the second</returns>
		public static Slice operator +(Slice a, Slice b)
		{
			return a.Concat(b);
		}

		/// <summary>Appends a byte at the end of the slice</summary>
		/// <param name="a">First slice</param>
		/// <param name="b">Byte to append at the end</param>
		/// <returns>New slice with the byte appended</returns>
		public static Slice operator +(Slice a, byte b)
		{
			if (a.Count == 0) return Slice.FromByte(b);
			var tmp = new byte[a.Count + 1];
			SliceHelpers.CopyBytesUnsafe(tmp, 0, a.Array, a.Offset, a.Count);
			tmp[a.Count] = b;
			return new Slice(tmp, 0, tmp.Length);
		}

		/// <summary>Remove <paramref name="n"/> bytes at the end of slice <paramref name="s"/></summary>
		/// <returns>Smaller slice</returns>
		public static Slice operator -(Slice s, int n)
		{
			if (n < 0) throw new ArgumentOutOfRangeException("n", "Cannot subtract a negative number from a slice");
			if (n > s.Count) throw new ArgumentOutOfRangeException("n", "Cannout substract more bytes than the slice contains");

			if (n == 0) return s;
			if (n == s.Count) return Slice.Empty;

			return new Slice(s.Array, s.Offset, s.Count - n);
		}

		// note: We also need overloads with Nullable<Slice>'s to be able to do things like "if (slice == null)", "if (slice != null)" or "if (null != slice)".
		// For structs that have "==" / "!=" operators, the compiler will think that when you write "slice == null", you really mean "(Slice?)slice == default(Slice?)", and that would ALWAYS false if you don't have specialized overloads to intercept.

		/// <summary>Determines whether two specified instances of <see cref="Slice"/> are equal</summary>
		public static bool operator ==(Slice? a, Slice? b)
		{
			return a.GetValueOrDefault().Equals(b.GetValueOrDefault());
		}

		/// <summary>Determines whether two specified instances of <see cref="Slice"/> are not equal</summary>
		public static bool operator !=(Slice? a, Slice? b)
		{
			return !a.GetValueOrDefault().Equals(b.GetValueOrDefault());
		}

		/// <summary>Determines whether one specified <see cref="Slice"/> is less than another specified <see cref="Slice"/>.</summary>
		public static bool operator <(Slice? a, Slice? b)
		{
			return a.GetValueOrDefault() < b.GetValueOrDefault();
		}

		/// <summary>Determines whether one specified <see cref="Slice"/> is less than or equal to another specified <see cref="Slice"/>.</summary>
		public static bool operator <=(Slice? a, Slice? b)
		{
			return a.GetValueOrDefault() <= b.GetValueOrDefault();
		}

		/// <summary>Determines whether one specified <see cref="Slice"/> is greater than another specified <see cref="Slice"/>.</summary>
		public static bool operator >(Slice? a, Slice? b)
		{
			return a.GetValueOrDefault() > b.GetValueOrDefault();
		}

		/// <summary>Determines whether one specified <see cref="Slice"/> is greater than or equal to another specified <see cref="Slice"/>.</summary>
		public static bool operator >=(Slice? a, Slice? b)
		{
			return a.GetValueOrDefault() >= b.GetValueOrDefault();
		}

		/// <summary>Concatenates two <see cref="Slice"/> together.</summary>
		public static Slice operator +(Slice? a, Slice? b)
		{
			// note: makes "slice + null" work!
			return a.GetValueOrDefault().Concat(b.GetValueOrDefault());
		}

		#endregion

		/// <summary>Returns a printable representation of the key</summary>
		/// <remarks>You can roundtrip the result of calling slice.ToString() by passing it to <see cref="Slice.Unescape"/>(string) and get back the original slice.</remarks>
		public override string ToString()
		{
			return Slice.Dump(this);
		}

		/// <summary>Returns a printable representation of a key</summary>
		/// <remarks>This may not be efficient, so it should only be use for testing/logging/troubleshooting</remarks>
		[NotNull]
		public static string Dump(Slice value)
		{
			const int MAX_SIZE = 1024;

			if (value.Count == 0) return value.HasValue ? "<empty>" : "<null>";

			SliceHelpers.EnsureSliceIsValid(ref value);

			var buffer = value.Array;
			int count = Math.Min(value.Count, MAX_SIZE);
			int pos = value.Offset;

			var sb = new StringBuilder(count + 16);
			while (count-- > 0)
			{
				int c = buffer[pos++];
				if (c < 32 || c >= 127 || c == 60)
				{
					sb.Append('<');
					int x = c >> 4;
					sb.Append((char)(x + (x < 10 ? 48 : 55)));
					x = c & 0xF;
					sb.Append((char)(x + (x < 10 ? 48 : 55)));
					sb.Append('>');
				}
				else
				{
					sb.Append((char)c);
				}
			}
			if (value.Count > MAX_SIZE) sb.Append("[...]");
			return sb.ToString();
		}

		/// <summary>Decode the string that was generated by slice.ToString() or Slice.Dump(), back into the original slice</summary>
		/// <remarks>This may not be efficient, so it should only be use for testing/logging/troubleshooting</remarks>
		public static Slice Unescape(string value)
		{
			var writer = SliceWriter.Empty;
			for (int i = 0; i < value.Length; i++)
			{
				char c = value[i];
				if (c == '<')
				{
					if (value[i + 3] != '>') throw new FormatException(String.Format("Invalid escape character at offset {0}", i));
					c = (char)(NibbleToDecimal(value[i + 1]) << 4 | NibbleToDecimal(value[i + 2]));
					i += 3;
				}
				writer.WriteByte((byte)c);
			}
			return writer.ToSlice();
		}

		#region Streams...

		/// <summary>Read the content of a stream into a slice</summary>
		/// <param name="data">Source stream, that must be in a readable state</param>
		/// <returns>Slice containing the stream content (or <see cref="Slice.Nil"/> if the stream is <see cref="Stream.Null"/>)</returns>
		public static Slice FromStream([NotNull] Stream data)
		{
			if (data == null) throw new ArgumentNullException("data");

			// special case for empty values
			if (data == Stream.Null) return Slice.Nil;
			if (!data.CanRead) throw new InvalidOperationException("Cannot read from provided stream");

			if (data.Length == 0) return Slice.Empty;
			if (data.Length > int.MaxValue) throw new InvalidOperationException("Streams of more than 2GB are not supported");
			//TODO: other checks?

			int length;
			checked { length = (int)data.Length; }

			if (data is MemoryStream || data is UnmanagedMemoryStream) // other types of already completed streams ?
			{ // read synchronously
				return LoadFromNonBlockingStream(data, length);
			}

			// read asynchronoulsy
			return LoadFromBlockingStream(data, length);
		}

		/// <summary>Asynchronously read the content of a stream into a slice</summary>
		/// <param name="data">Source stream, that must be in a readable state</param>
		/// <param name="cancellationToken">Optional cancellation token for this operation</param>
		/// <returns>Slice containing the stream content (or <see cref="Slice.Nil"/> if the stream is <see cref="Stream.Null"/>)</returns>
		public static Task<Slice> FromStreamAsync([NotNull] Stream data, CancellationToken cancellationToken)
		{
			if (data == null) throw new ArgumentNullException("data");
			// special case for empty values
			if (data == Stream.Null) return Task.FromResult(Slice.Nil);
			if (data.Length == 0) return Task.FromResult(Slice.Empty);

			if (!data.CanRead) throw new ArgumentException("Cannot read from provided stream", "data");
			if (data.Length > int.MaxValue) throw new InvalidOperationException("Streams of more than 2GB are not supported");
			//TODO: other checks?

			if (cancellationToken.IsCancellationRequested) return TaskHelpers.FromCancellation<Slice>(cancellationToken);

			int length;
			checked { length = (int)data.Length; }

			if (data is MemoryStream || data is UnmanagedMemoryStream) // other types of already completed streams ?
			{ // read synchronously
				return Task.FromResult(LoadFromNonBlockingStream(data, length));
			}

			// read asynchronoulsy
			return LoadFromBlockingStreamAsync(data, length, 0, cancellationToken);
		}

		/// <summary>Read from a non-blocking stream that already contains all the data in memory (MemoryStream, UnmanagedStream, ...)</summary>
		/// <param name="source">Source stream</param>
		/// <param name="length">Number of bytes to read from the stream</param>
		/// <returns>Slice containing the loaded data</returns>
		private static Slice LoadFromNonBlockingStream([NotNull] Stream source, int length)
		{
			Contract.Requires(source != null && source.CanRead && source.Length <= int.MaxValue);

			var ms = source as MemoryStream;
			if (ms != null)
			{ // Already holds onto a byte[]

				//note: should be use GetBuffer() ? It can throws and is dangerous (could mutate)
				return Slice.Create(ms.ToArray());
			}

			// read it in bulk, without buffering

			var buffer = new byte[length]; //TODO: round up to avoid fragmentation ?

			// note: reading should usually complete with only one big read, but loop until completed, just to be sure
			int p = 0;
			int r = length;
			while (r > 0)
			{
				int n = source.Read(buffer, p, r);
				if (n <= 0) throw new InvalidOperationException(String.Format("Unexpected end of stream at {0} / {1} bytes", p, length));
				p += n;
				r -= n;
			}
			Contract.Assert(r == 0 && p == length);

			return Slice.Create(buffer);
		}

		/// <summary>Synchronously read from a blocking stream (FileStream, NetworkStream, ...)</summary>
		/// <param name="source">Source stream</param>
		/// <param name="length">Number of bytes to read from the stream</param>
		/// <param name="chunkSize">If non zero, max amount of bytes to read in one chunk. If zero, tries to read everything at once</param>
		/// <returns>Slice containing the loaded data</returns>
		private static Slice LoadFromBlockingStream([NotNull] Stream source, int length, int chunkSize = 0)
		{
			Contract.Requires(source != null && source.CanRead && source.Length <= int.MaxValue && chunkSize >= 0);

			if (chunkSize == 0) chunkSize = int.MaxValue;

			var buffer = new byte[length]; //TODO: round up to avoid fragmentation ?

			// note: reading should usually complete with only one big read, but loop until completed, just to be sure
			int p = 0;
			int r = length;
			while (r > 0)
			{
				int c = Math.Max(r, chunkSize);
				int n = source.Read(buffer, p, c);
				if (n <= 0) throw new InvalidOperationException(String.Format("Unexpected end of stream at {0} / {1} bytes", p, length));
				p += n;
				r -= n;
			}
			Contract.Assert(r == 0 && p == length);

			return Slice.Create(buffer);
		}

		/// <summary>Asynchronously read from a blocking stream (FileStream, NetworkStream, ...)</summary>
		/// <param name="source">Source stream</param>
		/// <param name="length">Number of bytes to read from the stream</param>
		/// <param name="chunkSize">If non zero, max amount of bytes to read in one chunk. If zero, tries to read everything at once</param>
		/// <param name="cancellationToken">Optional cancellation token for this operation</param>
		/// <returns>Slice containing the loaded data</returns>
		private static async Task<Slice> LoadFromBlockingStreamAsync([NotNull] Stream source, int length, int chunkSize, CancellationToken cancellationToken)
		{
			Contract.Requires(source != null && source.CanRead && source.Length <= int.MaxValue && chunkSize >= 0);

			if (chunkSize == 0) chunkSize = int.MaxValue;

			var buffer = new byte[length]; //TODO: round up to avoid fragmentation ?

			// note: reading should usually complete with only one big read, but loop until completed, just to be sure
			int p = 0;
			int r = length;
			while (r > 0)
			{
				int c = Math.Min(r, chunkSize);
				int n = await source.ReadAsync(buffer, p, c, cancellationToken);
				if (n <= 0) throw new InvalidOperationException(String.Format("Unexpected end of stream at {0} / {1} bytes", p, length));
				p += n;
				r -= n;
			}
			Contract.Assert(r == 0 && p == length);

			return Slice.Create(buffer);
		}

		#endregion

		#region Equality, Comparison...

		/// <summary>Checks if an object is equal to the current slice</summary>
		/// <param name="obj">Object that can be either another slice, a byte array, or a byte array segment.</param>
		/// <returns>true if the object represents a sequence of bytes that has the same size and same content as the current slice.</returns>
		public override bool Equals(object obj)
		{
			if (obj == null) return this.Array == null;
			if (obj is Slice) return Equals((Slice)obj);
			if (obj is ArraySegment<byte>) return Equals((ArraySegment<byte>)obj);
			if (obj is byte[]) return Equals((byte[])obj);
			return false;
		}

		/// <summary>Gets the hash code for this slice</summary>
		/// <returns>A 32-bit signed hash code calculated from all the bytes in the slice.</returns>
		public override int GetHashCode()
		{
			SliceHelpers.EnsureSliceIsValid(ref this);
			if (this.Array == null) return 0;
			return SliceHelpers.ComputeHashCodeUnsafe(this.Array, this.Offset, this.Count);
		}

		/// <summary>Checks if another slice is equal to the current slice.</summary>
		/// <param name="other">Slice compared with the current instance</param>
		/// <returns>true if both slices have the same size and contain the same sequence of bytes; otherwise, false.</returns>
		public bool Equals(Slice other)
		{
			SliceHelpers.EnsureSliceIsValid(ref other);
			SliceHelpers.EnsureSliceIsValid(ref this);

			// note: Slice.Nil != Slice.Empty
			if (this.Array == null) return other.Array == null;
			if (other.Array == null) return false;

			return this.Count == other.Count && SliceHelpers.SameBytesUnsafe(this.Array, this.Offset, other.Array, other.Offset, this.Count);
		}

		/// <summary>Lexicographically compare this slice with another one, and return an indication of their relative sort order</summary>
		/// <param name="other">Slice to compare with this instance</param>
		/// <returns>Returns a NEGATIVE value if the current slice is LESS THAN <paramref name="other"/>, ZERO if it is EQUAL TO <paramref name="other"/>, and a POSITIVE value if it is GREATER THAN <paramref name="other"/>.</returns>
		/// <remarks>If both this instance and <paramref name="other"/> are Nil or Empty, the comparison will return ZERO. If only <paramref name="other"/> is Nil or Empty, it will return a NEGATIVE value. If only this instance is Nil or Empty, it will return a POSITIVE value.</remarks>
		public int CompareTo(Slice other)
		{
			if (this.Count == 0) return other.Count == 0 ? 0 : -1;
			if (other.Count == 0) return +1;
			SliceHelpers.EnsureSliceIsValid(ref other);
			SliceHelpers.EnsureSliceIsValid(ref this);
			return SliceHelpers.CompareBytesUnsafe(this.Array, this.Offset, this.Count, other.Array, other.Offset, other.Count);
		}

		/// <summary>Checks if the content of a byte array segment matches the current slice.</summary>
		/// <param name="other">Byte array segment compared with the current instance</param>
		/// <returns>true if both segment and slice have the same size and contain the same sequence of bytes; otherwise, false.</returns>
		public bool Equals(ArraySegment<byte> other)
		{
			return this.Count == other.Count && SliceHelpers.SameBytes(this.Array, this.Offset, other.Array, other.Offset, this.Count);
		}

		/// <summary>Checks if the content of a byte array matches the current slice.</summary>
		/// <param name="other">Byte array compared with the current instance</param>
		/// <returns>true if the both array and slice have the same size and contain the same sequence of bytes; otherwise, false.</returns>
		public bool Equals(byte[] other)
		{
			if (other == null) return this.Array == null;
			return this.Count == other.Length && SliceHelpers.SameBytes(this.Array, this.Offset, other, 0, this.Count);
		}

		#endregion

		private sealed class DebugView
		{
			private readonly Slice m_slice;

			public DebugView(Slice slice)
			{
				m_slice = slice;
			}

			public byte[] Data
			{
				get { return m_slice.GetBytes(); }
			}

			public int Count
			{
				get { return m_slice.Count; }
			}

		}

	}

}
