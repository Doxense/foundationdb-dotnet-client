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
	using System.Collections.Generic;
	using System.Linq;
	using System.Runtime.CompilerServices;
	using Doxense.Collections.Tuples;
	using Doxense.Diagnostics.Contracts;
	using Doxense.Memory;
	using Doxense.Serialization.Encoders;
	using JetBrains.Annotations;

	/// <summary>Helper class for all key/value encoders</summary>
	[PublicAPI]
	public static partial class KeyValueEncoders
	{
		/// <summary>Identity function for binary slices</summary>
		public static readonly IdentityEncoder Binary = new IdentityEncoder();

		#region Nested Classes

		/// <summary>Identity encoder</summary>
		public sealed class IdentityEncoder : IKeyEncoder<Slice>, IValueEncoder<Slice>
		{

			internal IdentityEncoder() { }

			public void WriteKeyTo(ref SliceWriter writer, Slice key)
			{
				writer.WriteBytes(key);
			}

			public void ReadKeyFrom(ref SliceReader reader, out Slice value)
			{
				value = reader.ReadToEnd();
			}

			public Slice EncodeValue(Slice value)
			{
				return value;
			}

			public Slice DecodeValue(Slice encoded)
			{
				return encoded;
			}
		}

		/// <summary>Wrapper for encoding and decoding a singleton with lambda functions</summary>
		internal sealed class Singleton<T> : IKeyEncoder<T>, IValueEncoder<T>
		{
			private readonly Func<T, Slice> m_encoder;
			private readonly Func<Slice, T> m_decoder;

			public Singleton(Func<T, Slice> encoder, Func<Slice, T> decoder)
			{
				Contract.Requires(encoder != null && decoder != null);

				m_encoder = encoder;
				m_decoder = decoder;
			}

			public Type[] GetTypes()
			{
				return new[] { typeof(T) };
			}

			public void WriteKeyTo(ref SliceWriter writer, T value)
			{
				writer.WriteBytes(m_encoder(value));
			}

			public void ReadKeyFrom(ref SliceReader reader, out T value)
			{
				value = m_decoder(reader.ReadToEnd());
			}

			public Slice EncodeValue(T value)
			{
				return m_encoder(value);
			}

			public T DecodeValue(Slice encoded)
			{
				return m_decoder(encoded);
			}

		}

		/// <summary>Wrapper for encoding and decoding a pair with lambda functions</summary>
		public abstract class CompositeKeyEncoder<T1, T2> : ICompositeKeyEncoder<T1, T2>
		{

			public abstract void WriteKeyPartsTo(ref SliceWriter writer, int count, ref STuple<T1, T2> items);

			public abstract void ReadKeyPartsFrom(ref SliceReader reader, int count, out STuple<T1, T2> items);

			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public void WriteKeyTo(ref SliceWriter writer, STuple<T1, T2> items)
			{
				WriteKeyPartsTo(ref writer, 2, ref items);
			}

			public void ReadKeyFrom(ref SliceReader reader, out STuple<T1, T2> items)
			{
				ReadKeyPartsFrom(ref reader, 2, out items);
			}

		}

		/// <summary>Wrapper for encoding and decoding a triplet with lambda functions</summary>
		public abstract class CompositeKeyEncoder<T1, T2, T3> : ICompositeKeyEncoder<T1, T2, T3>
		{

			public abstract void WriteKeyPartsTo(ref SliceWriter writer, int count, ref STuple<T1, T2, T3> items);

			public abstract void ReadKeyPartsFrom(ref SliceReader reader, int count, out STuple<T1, T2, T3> items);

			public void WriteKeyTo(ref SliceWriter writer, STuple<T1, T2, T3> items)
			{
				WriteKeyPartsTo(ref writer, 3, ref items);
			}

			public void ReadKeyFrom(ref SliceReader reader, out STuple<T1, T2, T3> items)
			{
				ReadKeyPartsFrom(ref reader, 3, out items);
			}

		}

		/// <summary>Wrapper for encoding and decoding a quad with lambda functions</summary>
		public abstract class CompositeKeyEncoder<T1, T2, T3, T4> : ICompositeKeyEncoder<T1, T2, T3, T4>
		{

			public abstract void WriteKeyPartsTo(ref SliceWriter writer, int count, ref STuple<T1, T2, T3, T4> items);

			public abstract void ReadKeyPartsFrom(ref SliceReader reader, int count, out STuple<T1, T2, T3, T4> items);

			public void WriteKeyTo(ref SliceWriter writer, STuple<T1, T2, T3, T4> items)
			{
				WriteKeyPartsTo(ref writer, 4, ref items);
			}

			public void ReadKeyFrom(ref SliceReader reader, out STuple<T1, T2, T3, T4> items)
			{
				ReadKeyPartsFrom(ref reader, 4, out items);
			}

		}

		/// <summary>Wrapper for encoding and decoding a quad with lambda functions</summary>
		public abstract class CompositeKeyEncoder<T1, T2, T3, T4, T5> : ICompositeKeyEncoder<T1, T2, T3, T4, T5>
		{

			public abstract void WriteKeyPartsTo(ref SliceWriter writer, int count, ref STuple<T1, T2, T3, T4, T5> items);

			public abstract void ReadKeyPartsFrom(ref SliceReader reader, int count, out STuple<T1, T2, T3, T4, T5> items);

			public void WriteKeyTo(ref SliceWriter writer, STuple<T1, T2, T3, T4, T5> items)
			{
				WriteKeyPartsTo(ref writer, 5, ref items);
			}

			public void ReadKeyFrom(ref SliceReader reader, out STuple<T1, T2, T3, T4, T5> items)
			{
				ReadKeyPartsFrom(ref reader, 5, out items);
			}

		}

		#endregion

		#region Keys...

		/// <summary>Binds a pair of lambda functions to a key encoder</summary>
		/// <typeparam name="T">Type of the key to encode</typeparam>
		/// <param name="encoder">Lambda function called to encode a key into a binary slice</param>
		/// <param name="decoder">Lambda function called to decode a binary slice into a key</param>
		/// <returns>Key encoder usable by any Layer that works on keys of type <typeparamref name="T"/></returns>
		[NotNull]
		public static IKeyEncoder<T> Bind<T>([NotNull] Func<T, Slice> encoder, [NotNull] Func<Slice, T> decoder)
		{
			Contract.NotNull(encoder, nameof(encoder));
			Contract.NotNull(decoder, nameof(decoder));
			return new Singleton<T>(encoder, decoder);
		}

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

		#region Values...

		/// <summary>Convert an array of <typeparamref name="T"/>s into an array of slices, using a serializer (or the default serializer if none is provided)</summary>
		[NotNull]
		public static Slice[] EncodeValues<T>([NotNull] this IValueEncoder<T> encoder, [NotNull] params T[] values)
		{
			Contract.NotNull(encoder, nameof(encoder));
			Contract.NotNull(values, nameof(values));

			var slices = new Slice[values.Length];
			for (int i = 0; i < values.Length; i++)
			{
				slices[i] = encoder.EncodeValue(values[i]);
			}

			return slices;
		}

		/// <summary>Transform a sequence of <typeparamref name="T"/>s into a sequence of slices, using a serializer (or the default serializer if none is provided)</summary>
		[NotNull]
		public static IEnumerable<Slice> EncodeValues<T>([NotNull] this IValueEncoder<T> encoder, [NotNull] IEnumerable<T> values)
		{
			Contract.NotNull(encoder, nameof(encoder));
			Contract.NotNull(values, nameof(values));

			// note: T=>Slice usually is used for writing batches as fast as possible, which means that keys will be consumed immediately and don't need to be streamed

			var array = values as T[];
			if (array != null)
			{ // optimized path for arrays
				return EncodeValues<T>(encoder, array);
			}

			var coll = values as ICollection<T>;
			if (coll != null)
			{ // optimized path when we know the count
				var slices = new List<Slice>(coll.Count);
				foreach (var value in coll)
				{
					slices.Add(encoder.EncodeValue(value));
				}
				return slices;
			}

			return values.Select(value => encoder.EncodeValue(value));
		}

		/// <summary>Convert an array of slices back into an array of <typeparamref name="T"/>s, using a serializer (or the default serializer if none is provided)</summary>
		[NotNull]
		public static T[] DecodeValues<T>([NotNull] this IValueEncoder<T> encoder, [NotNull] params Slice[] slices)
		{
			Contract.NotNull(encoder, nameof(encoder));
			Contract.NotNull(slices, nameof(slices));

			var values = new T[slices.Length];
			for (int i = 0; i < slices.Length; i++)
			{
				values[i] = encoder.DecodeValue(slices[i]);
			}

			return values;
		}

		/// <summary>Convert the values of an array of key value pairs of slices back into an array of <typeparamref name="T"/>s, using a serializer (or the default serializer if none is provided)</summary>
		[NotNull]
		public static T[] DecodeValues<T>([NotNull] this IValueEncoder<T> encoder, [NotNull] KeyValuePair<Slice, Slice>[] items)
		{
			Contract.NotNull(encoder, nameof(encoder));
			Contract.NotNull(items, nameof(items));

			var values = new T[items.Length];
			for (int i = 0; i < items.Length; i++)
			{
				values[i] = encoder.DecodeValue(items[i].Value);
			}

			return values;
		}

		/// <summary>Transform a sequence of slices back into a sequence of <typeparamref name="T"/>s, using a serializer (or the default serializer if none is provided)</summary>
		[NotNull]
		public static IEnumerable<T> DecodeValues<T>([NotNull] this IValueEncoder<T> encoder, [NotNull] IEnumerable<Slice> slices)
		{
			Contract.NotNull(encoder, nameof(encoder));
			Contract.NotNull(slices, nameof(slices));

			// Slice=>T may be filtered in LINQ queries, so we should probably stream the values (so no optimization needed)

			return slices.Select(slice => encoder.DecodeValue(slice));
		}

		#endregion
	}

}
