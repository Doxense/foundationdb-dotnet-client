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

namespace Doxense.Serialization.Encoders
{
	using System;
	using Doxense.Collections.Tuples;
	using Doxense.Memory;
	
	public interface ICompositeKeyEncoder<T1, T2> : IKeyEncoder<(T1, T2)>
	{
		/// <summary>Write some or all parts of a composite key</summary>
		void WriteKeyPartsTo(ref SliceWriter writer, int count, ref (T1, T2) key);

		/// <summary>Read some or all parts of a composite key</summary>
		void ReadKeyPartsFrom(ref SliceReader reader, int count, out (T1, T2) items);
	}

	public interface ICompositeKeyEncoder<T1, T2, T3> : IKeyEncoder<(T1, T2, T3)>
	{
		/// <summary>Write some or all parts of a composite key</summary>
		void WriteKeyPartsTo(ref SliceWriter writer, int count, ref (T1, T2, T3) key);

		/// <summary>Read some or all parts of a composite key</summary>
		void ReadKeyPartsFrom(ref SliceReader reader, int count, out (T1, T2, T3) items);
	}

	public interface ICompositeKeyEncoder<T1, T2, T3, T4> : IKeyEncoder<(T1, T2, T3, T4)>
	{
		/// <summary>Write some or all parts of a composite key</summary>
		void WriteKeyPartsTo(ref SliceWriter writer, int count, ref (T1, T2, T3, T4) key);

		/// <summary>Read some or all parts of a composite key</summary>
		void ReadKeyPartsFrom(ref SliceReader reader, int count, out (T1, T2, T3, T4) items);
	}

	public interface ICompositeKeyEncoder<T1, T2, T3, T4, T5> : IKeyEncoder<(T1, T2, T3, T4, T5)>
	{
		/// <summary>Write some or all parts of a composite key</summary>
		void WriteKeyPartsTo(ref SliceWriter writer, int count, ref (T1, T2, T3, T4, T5) key);

		/// <summary>Read some or all parts of a composite key</summary>
		void ReadKeyPartsFrom(ref SliceReader reader, int count, out (T1, T2, T3, T4, T5) items);
	}

	public interface ICompositeKeyEncoder<T1, T2, T3, T4, T5, T6> : IKeyEncoder<(T1, T2, T3, T4, T5, T6)>
	{
		/// <summary>Write some or all parts of a composite key</summary>
		void WriteKeyPartsTo(ref SliceWriter writer, int count, ref (T1, T2, T3, T4, T5, T6) key);

		/// <summary>Read some or all parts of a composite key</summary>
		void ReadKeyPartsFrom(ref SliceReader reader, int count, out (T1, T2, T3, T4, T5, T6) items);
	}

	public static partial class KeyEncoderExtensions
	{

		#region <T1, T2>

		public static void WriteKeyTo<T1, T2>(this ICompositeKeyEncoder<T1, T2> encoder, ref SliceWriter writer, T1 value1, T2 value2)
		{
			var tuple = (value1, value2);
			encoder.WriteKeyPartsTo(ref writer, 2, ref tuple);
		}

		public static Slice EncodeKey<T1, T2>(this ICompositeKeyEncoder<T1, T2> encoder, T1 item1, T2 item2)
		{
			var writer = default(SliceWriter);
			var tuple = (item1, item2);
			encoder.WriteKeyPartsTo(ref writer, 2, ref tuple);
			return writer.ToSlice();
		}

		public static Slice EncodeKey<T1, T2>(this ICompositeKeyEncoder<T1, T2> encoder, Slice prefix, T1 item1, T2 item2)
		{
			var writer = new SliceWriter(prefix.Count + 24);
			writer.WriteBytes(prefix);
			encoder.WriteKeyTo(ref writer, item1, item2);
			return writer.ToSlice();
		}

		public static Slice EncodePartialKey<T1, T2>(this ICompositeKeyEncoder<T1, T2> encoder, T1 item1)
		{
			var writer = default(SliceWriter);
			var tuple = (item1, default(T2));
			encoder.WriteKeyPartsTo(ref writer, 1, ref tuple);
			return writer.ToSlice();
		}

		public static Slice EncodePartialKey<T1, T2>(this ICompositeKeyEncoder<T1, T2> encoder, Slice prefix, T1 item1)
		{
			var writer = new SliceWriter(prefix.Count + 16);
			writer.WriteBytes(prefix);
			var tuple = (item1, default(T2));
			encoder.WriteKeyPartsTo(ref writer, 1, ref tuple);
			return writer.ToSlice();
		}

		public static Slice EncodeKeyParts<T1, T2>(this ICompositeKeyEncoder<T1, T2> encoder, int count, (T1, T2) items)
		{
			var writer = default(SliceWriter);
			encoder.WriteKeyPartsTo(ref writer, count, ref items);
			return writer.ToSlice();
		}

		public static STuple<T1, T2> DecodeKey<T1, T2>(this ICompositeKeyEncoder<T1, T2> decoder, Slice encoded)
		{
			var reader = new SliceReader(encoded);
			decoder.ReadKeyFrom(ref reader, out var items);
			//TODO: throw if extra bytes?
			return items;
		}

		public static STuple<T1, T2> DecodeKeyParts<T1, T2>(this ICompositeKeyEncoder<T1, T2> encoder, int count, Slice encoded)
		{
			var reader = new SliceReader(encoded);
			encoder.ReadKeyPartsFrom(ref reader, count, out var items);
			return items;
		}

		#endregion

		#region <T1, T2, T3>

		public static void WriteKeyTo<T1, T2, T3>(this ICompositeKeyEncoder<T1, T2, T3> encoder, ref SliceWriter writer, T1 value1, T2 value2, T3 value3)
		{
			var tuple = (value1, value2, value3);
			encoder.WriteKeyPartsTo(ref writer, 3, ref tuple);
		}

		public static Slice EncodeKey<T1, T2, T3>(this ICompositeKeyEncoder<T1, T2, T3> encoder, T1 item1, T2 item2, T3 item3)
		{
			var writer = default(SliceWriter);
			var tuple = (item1, item2, item3);
			encoder.WriteKeyPartsTo(ref writer, 3, ref tuple);
			return writer.ToSlice();
		}

		public static Slice EncodeKey<T1, T2, T3>(this ICompositeKeyEncoder<T1, T2, T3> encoder, Slice prefix, T1 item1, T2 item2, T3 item3)
		{
			var writer = new SliceWriter(prefix.Count + 32);
			writer.WriteBytes(prefix);
			encoder.WriteKeyTo(ref writer, item1, item2, item3);
			return writer.ToSlice();
		}

		public static Slice EncodeKeyParts<T1, T2, T3>(this ICompositeKeyEncoder<T1, T2, T3> encoder, int count, (T1, T2, T3) items)
		{
			var writer = default(SliceWriter);
			encoder.WriteKeyPartsTo(ref writer, count, ref items);
			return writer.ToSlice();
		}

		public static STuple<T1, T2, T3> DecodeKey<T1, T2, T3>(this ICompositeKeyEncoder<T1, T2, T3> decoder, Slice encoded)
		{
			var reader = new SliceReader(encoded);
			decoder.ReadKeyFrom(ref reader, out var items);
			//TODO: throw if extra bytes?
			return items;
		}

		public static STuple<T1, T2, T3> DecodeKeyParts<T1, T2, T3>(this ICompositeKeyEncoder<T1, T2, T3> encoder, int count, Slice encoded)
		{
			var reader = new SliceReader(encoded);
			encoder.ReadKeyPartsFrom(ref reader, count, out var items);
			return items;
		}

		#endregion

		#region <T1, T2, T3, T4>

		public static void WriteKeyTo<T1, T2, T3, T4>(this ICompositeKeyEncoder<T1, T2, T3, T4> encoder, ref SliceWriter writer, T1 value1, T2 value2, T3 value3, T4 value4)
		{
			var tuple = (value1, value2, value3, value4);
			encoder.WriteKeyPartsTo(ref writer, 4, ref tuple);
		}

		public static Slice EncodeKey<T1, T2, T3, T4>(this ICompositeKeyEncoder<T1, T2, T3, T4> encoder, T1 item1, T2 item2, T3 item3, T4 item4)
		{
			var writer = default(SliceWriter);
			var tuple = (item1, item2, item3, item4);
			encoder.WriteKeyPartsTo(ref writer, 4, ref tuple);
			return writer.ToSlice();
		}

		public static Slice EncodeKey<T1, T2, T3, T4>(this ICompositeKeyEncoder<T1, T2, T3, T4> encoder, Slice prefix, T1 item1, T2 item2, T3 item3, T4 item4)
		{
			var writer = new SliceWriter(prefix.Count + 48);
			writer.WriteBytes(prefix);
			encoder.WriteKeyTo(ref writer, item1, item2, item3, item4);
			return writer.ToSlice();
		}

		public static Slice EncodeKeyParts<T1, T2, T3, T4>(this ICompositeKeyEncoder<T1, T2, T3, T4> encoder, int count, (T1, T2, T3, T4) items)
		{
			var writer = default(SliceWriter);
			encoder.WriteKeyPartsTo(ref writer, count, ref items);
			return writer.ToSlice();
		}

		public static STuple<T1, T2, T3, T4> DecodeKey<T1, T2, T3, T4>(this ICompositeKeyEncoder<T1, T2, T3, T4> decoder, Slice encoded)
		{
			var reader = new SliceReader(encoded);
			decoder.ReadKeyFrom(ref reader, out var items);
			//TODO: throw if extra bytes?
			return items;
		}

		public static STuple<T1, T2, T3, T4> DecodeKeyParts<T1, T2, T3, T4>(this ICompositeKeyEncoder<T1, T2, T3, T4> encoder, int count, Slice encoded)
		{
			var reader = new SliceReader(encoded);
			encoder.ReadKeyPartsFrom(ref reader, count, out var items);
			return items;
		}

		#endregion

		#region <T1, T2, T3, T4, T5>

		public static void WriteKeyTo<T1, T2, T3, T4, T5>(this ICompositeKeyEncoder<T1, T2, T3, T4, T5> encoder, ref SliceWriter writer, T1 value1, T2 value2, T3 value3, T4 value4, T5 value5)
		{
			var tuple = (value1, value2, value3, value4, value5);
			encoder.WriteKeyPartsTo(ref writer, 5, ref tuple);
		}
		
		public static Slice EncodeKey<T1, T2, T3, T4, T5>(this ICompositeKeyEncoder<T1, T2, T3, T4, T5> encoder, T1 item1, T2 item2, T3 item3, T4 item4, T5 item5)
		{
			var writer = default(SliceWriter);
			var tuple = (item1, item2, item3, item4, item5);
			encoder.WriteKeyPartsTo(ref writer, 5, ref tuple);
			return writer.ToSlice();
		}

		public static Slice EncodeKey<T1, T2, T3, T4, T5>(this ICompositeKeyEncoder<T1, T2, T3, T4, T5> encoder, Slice prefix, T1 item1, T2 item2, T3 item3, T4 item4, T5 item5)
		{
			var writer = new SliceWriter(prefix.Count + 56);
			writer.WriteBytes(prefix);
			encoder.WriteKeyTo(ref writer, item1, item2, item3, item4, item5);
			return writer.ToSlice();
		}

		public static Slice EncodeKeyParts<T1, T2, T3, T4, T5>(this ICompositeKeyEncoder<T1, T2, T3, T4, T5> encoder, int count, (T1, T2, T3, T4, T5) items)
		{
			var writer = default(SliceWriter);
			encoder.WriteKeyPartsTo(ref writer, count, ref items);
			return writer.ToSlice();
		}

		public static STuple<T1, T2, T3, T4, T5> DecodeKey<T1, T2, T3, T4, T5>(this ICompositeKeyEncoder<T1, T2, T3, T4, T5> decoder, Slice encoded)
		{
			var reader = new SliceReader(encoded);
			decoder.ReadKeyFrom(ref reader, out var items);
			//TODO: throw if extra bytes?
			return items;
		}

		public static STuple<T1, T2, T3, T4, T5> DecodeKeyParts<T1, T2, T3, T4, T5>(this ICompositeKeyEncoder<T1, T2, T3, T4, T5> encoder, int count, Slice encoded)
		{
			var reader = new SliceReader(encoded);
			encoder.ReadKeyPartsFrom(ref reader, count, out var items);
			return items;
		}


		#endregion

	}

}
