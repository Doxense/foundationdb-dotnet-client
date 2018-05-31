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

#if !USE_SHARED_FRAMEWORK

namespace Doxense.Serialization.Encoders
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using Doxense.Diagnostics.Contracts;
	using Doxense.Memory;
	using JetBrains.Annotations;

	public static class KeyEncoderExtensions
	{

		#region <T1>

		public static Slice EncodeKey<T1>([NotNull] this IKeyEncoder<T1> encoder, T1 value)
		{
			var writer = default(SliceWriter);
			encoder.WriteKeyTo(ref writer, value);
			return writer.ToSlice();
		}

		public static Slice EncodeKey<T1>([NotNull] this IKeyEncoder<T1> encoder, Slice prefix, T1 value)
		{
			var writer = new SliceWriter(prefix.Count + 16); // ~16 bytes si T1 = Guid
			writer.WriteBytes(prefix);
			encoder.WriteKeyTo(ref writer, value);
			return writer.ToSlice();
		}

		public static T1 DecodeKey<T1>([NotNull] this IKeyEncoder<T1> decoder, Slice encoded)
		{
			var reader = new SliceReader(encoded);
			decoder.ReadKeyFrom(ref reader, out T1 item);
			//TODO: should we fail if extra bytes?
			return item;
		}

		#endregion

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

		public static (T1, T2) DecodeKey<T1, T2>(this ICompositeKeyEncoder<T1, T2> decoder, Slice encoded)
		{
			var reader = new SliceReader(encoded);
			decoder.ReadKeyFrom(ref reader, out var items);
			//TODO: throw if extra bytes?
			return items;
		}

		public static (T1, T2) DecodeKeyParts<T1, T2>(this ICompositeKeyEncoder<T1, T2> encoder, int count, Slice encoded)
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

		public static (T1, T2, T3) DecodeKey<T1, T2, T3>(this ICompositeKeyEncoder<T1, T2, T3> decoder, Slice encoded)
		{
			var reader = new SliceReader(encoded);
			decoder.ReadKeyFrom(ref reader, out var items);
			//TODO: throw if extra bytes?
			return items;
		}

		public static (T1, T2, T3) DecodeKeyParts<T1, T2, T3>(this ICompositeKeyEncoder<T1, T2, T3> encoder, int count, Slice encoded)
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

		public static (T1, T2, T3, T4) DecodeKey<T1, T2, T3, T4>(this ICompositeKeyEncoder<T1, T2, T3, T4> decoder, Slice encoded)
		{
			var reader = new SliceReader(encoded);
			decoder.ReadKeyFrom(ref reader, out var items);
			//TODO: throw if extra bytes?
			return items;
		}

		public static (T1, T2, T3, T4) DecodeKeyParts<T1, T2, T3, T4>(this ICompositeKeyEncoder<T1, T2, T3, T4> encoder, int count, Slice encoded)
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

		public static (T1, T2, T3, T4, T5) DecodeKey<T1, T2, T3, T4, T5>(this ICompositeKeyEncoder<T1, T2, T3, T4, T5> decoder, Slice encoded)
		{
			var reader = new SliceReader(encoded);
			decoder.ReadKeyFrom(ref reader, out var items);
			//TODO: throw if extra bytes?
			return items;
		}

		public static (T1, T2, T3, T4, T5) DecodeKeyParts<T1, T2, T3, T4, T5>(this ICompositeKeyEncoder<T1, T2, T3, T4, T5> encoder, int count, Slice encoded)
		{
			var reader = new SliceReader(encoded);
			encoder.ReadKeyPartsFrom(ref reader, count, out var items);
			return items;
		}


		#endregion

		#region Batched...

		/// <summary>Convert an array of <typeparamref name="T"/>s into an array of slices, using a serializer (or the default serializer if none is provided)</summary>
		[NotNull]
		public static Slice[] EncodeKeys<T>([NotNull] this IKeyEncoder<T> encoder, [NotNull] params T[] values)
		{
			Contract.NotNull(encoder, nameof(encoder));
			Contract.NotNull(values, nameof(values));

			var slices = new Slice[values.Length];
			for (int i = 0; i < values.Length; i++)
			{
				slices[i] = encoder.EncodeKey(values[i]);
			}
			return slices;
		}

		/// <summary>Convert an array of <typeparamref name="TElement"/>s into an array of slices, using a serializer (or the default serializer if none is provided)</summary>
		[NotNull]
		public static Slice[] EncodeKeys<TKey, TElement>([NotNull] this IKeyEncoder<TKey> encoder, [NotNull] IEnumerable<TElement> elements, Func<TElement, TKey> selector)
		{
			Contract.NotNull(encoder, nameof(encoder));
			Contract.NotNull(elements, nameof(elements));
			Contract.NotNull(selector, nameof(selector));

			TElement[] arr;
			ICollection<TElement> coll;

			if ((arr = elements as TElement[]) != null)
			{ // fast path for arrays
				return EncodeKeys<TKey, TElement>(encoder, arr, selector);
			}
			if ((coll = elements as ICollection<TElement>) != null)
			{ // we can pre-allocate the result array
				var slices = new Slice[coll.Count];
				int p = 0;
				foreach(var item in coll)
				{
					slices[p++] = encoder.EncodeKey(selector(item));
				}
				return slices;
			}
			// slow path
			return elements.Select((item) => encoder.EncodeKey(selector(item))).ToArray();
		}

		/// <summary>Convert an array of <typeparamref name="TElement"/>s into an array of slices, using a serializer (or the default serializer if none is provided)</summary>
		[NotNull]
		public static Slice[] EncodeKeys<TKey, TElement>([NotNull] this IKeyEncoder<TKey> encoder, [NotNull] TElement[] elements, Func<TElement, TKey> selector)
		{
			Contract.NotNull(encoder, nameof(encoder));
			Contract.NotNull(elements, nameof(elements));
			Contract.NotNull(selector, nameof(selector));

			var slices = new Slice[elements.Length];
			for (int i = 0; i < elements.Length; i++)
			{
				slices[i] = encoder.EncodeKey(selector(elements[i]));
			}
			return slices;
		}

		/// <summary>Transform a sequence of <typeparamref name="T"/>s into a sequence of slices, using a serializer (or the default serializer if none is provided)</summary>
		[NotNull]
		public static IEnumerable<Slice> EncodeKeys<T>([NotNull] this IKeyEncoder<T> encoder, [NotNull] IEnumerable<T> values)
		{
			Contract.NotNull(encoder, nameof(encoder));
			Contract.NotNull(values, nameof(values));

			// note: T=>Slice usually is used for writing batches as fast as possible, which means that keys will be consumed immediately and don't need to be streamed

			if (values is T[] array)
			{ // optimized path for arrays
				return EncodeKeys<T>(encoder, array);
			}

			if (values is ICollection<T> coll)
			{ // optimized path when we know the count
				var slices = new List<Slice>(coll.Count);
				foreach (var value in coll)
				{
					slices.Add(encoder.EncodeKey(value));
				}
				return slices;
			}

			// "slow" path
			return values.Select(value => encoder.EncodeKey(value));
		}

		/// <summary>Convert an array of slices back into an array of <typeparamref name="T"/>s, using a serializer (or the default serializer if none is provided)</summary>
		[NotNull]
		public static T[] DecodeKeys<T>([NotNull] this IKeyEncoder<T> encoder, [NotNull] params Slice[] slices)
		{
			Contract.NotNull(encoder, nameof(encoder));
			Contract.NotNull(slices, nameof(slices));

			var values = new T[slices.Length];
			for (int i = 0; i < slices.Length; i++)
			{
				values[i] = encoder.DecodeKey(slices[i]);
			}
			return values;
		}

		/// <summary>Convert the keys of an array of key value pairs of slices back into an array of <typeparamref name="T"/>s, using a serializer (or the default serializer if none is provided)</summary>
		[NotNull]
		public static T[] DecodeKeys<T>([NotNull] this IKeyEncoder<T> encoder, [NotNull] KeyValuePair<Slice, Slice>[] items)
		{
			Contract.NotNull(encoder, nameof(encoder));
			Contract.NotNull(items, nameof(items));

			var values = new T[items.Length];
			for (int i = 0; i < items.Length; i++)
			{
				values[i] = encoder.DecodeKey(items[i].Key);
			}
			return values;
		}

		/// <summary>Transform a sequence of slices back into a sequence of <typeparamref name="T"/>s, using a serializer (or the default serializer if none is provided)</summary>
		[NotNull]
		public static IEnumerable<T> DecodeKeys<T>([NotNull] this IKeyEncoder<T> encoder, [NotNull] IEnumerable<Slice> slices)
		{
			Contract.NotNull(encoder, nameof(encoder));
			Contract.NotNull(slices, nameof(slices));

			// Slice=>T may be filtered in LINQ queries, so we should probably stream the values (so no optimization needed)

			return slices.Select(slice => encoder.DecodeKey(slice));
		}

		#endregion

	}
}

#endif
