#region BSD Licence
/* Copyright (c) 2013, Doxense SARL
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
	using FoundationDB.Client.Utils;
	using FoundationDB.Layers.Tuples;
	using System;
	using System.Collections.Generic;
	using System.Linq;

	/// <summary>Helper class for all key/value encoders</summary>
	public static class KeyValueEncoders
	{

		#region Nested Classes

		internal sealed class Singleton<T> : IKeyValueEncoder<T>
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

			public Slice Encode(T value)
			{
				return m_encoder(value);
			}

			public T Decode(Slice encoded)
			{
				return m_decoder(encoded);
			}
		}

		internal sealed class Composite<T1, T2> : IKeyValueEncoder<T1, T2>
		{
			private readonly Func<T1, T2, Slice> m_encoder;
			private readonly Func<Slice, FdbTuple<T1, T2>> m_decoder;

			public Composite(Func<T1, T2, Slice> encoder, Func<Slice, FdbTuple<T1, T2>> decoder)
			{
				Contract.Requires(encoder != null && decoder != null);

				m_encoder = encoder;
				m_decoder = decoder;
			}

			public Type[] GetTypes()
			{
				return new[] { typeof(T1), typeof(T2) };
			}

			public Slice Encode(T1 value1, T2 value2)
			{
				return m_encoder(value1, value2);
			}

			public FdbTuple<T1, T2> Decode(Slice encoded)
			{
				return m_decoder(encoded);
			}
		}

		internal sealed class Composite<T1, T2, T3> : IKeyValueEncoder<T1, T2, T3>
		{
			private readonly Func<T1, T2, T3, Slice> m_encoder;
			private readonly Func<Slice, FdbTuple<T1, T2, T3>> m_decoder;

			public Composite(Func<T1, T2, T3, Slice> encoder, Func<Slice, FdbTuple<T1, T2, T3>> decoder)
			{
				Contract.Requires(encoder != null && decoder != null);

				m_encoder = encoder;
				m_decoder = decoder;
			}

			public Type[] GetTypes()
			{
				return new[] { typeof(T1), typeof(T2), typeof(T3) };
			}

			public Slice Encode(T1 value1, T2 value2, T3 value3)
			{
				return m_encoder(value1, value2, value3);
			}

			public FdbTuple<T1, T2, T3> Decode(Slice encoded)
			{
				return m_decoder(encoded);
			}
		}

		#endregion

		/// <summary>Encoders that produce lexicographically ordered slices, suitable for use as keys</summary>
		public static class Ordered
		{

			/// <summary>Create a simple encoder from a codec</summary>
			public static IKeyValueEncoder<T> Bind<T>(IOrderedTypeCodec<T> codec)
			{
				if (codec == null) throw new ArgumentNullException("codec");

				return new Singleton<T>(
					(value) => codec.EncodeOrdered(value),
					(encoded) => codec.DecodeOrdered(encoded)
				);
			}

			/// <summary>Create a composite encoder from a pair of codecs</summary>
			public static IKeyValueEncoder<T1, T2> Bind<T1, T2>(IOrderedTypeCodec<T1> codec1, IOrderedTypeCodec<T2> codec2)
			{
				if (codec1 == null) throw new ArgumentNullException("codec1");
				if (codec2 == null) throw new ArgumentNullException("codec2");

				return new Composite<T1, T2>(
					(value1, value2) =>
					{
						var writer = SliceWriter.Empty;
						codec1.EncodeOrderedSelfTerm(ref writer, value1);
						codec2.EncodeOrderedSelfTerm(ref writer, value2);
						return writer.ToSlice();
					},
					(encoded) =>
					{
						var reader = new SliceReader(encoded);
						T1 value1 = codec1.DecodeOrderedSelfTerm(ref reader);
						T2 value2 = codec2.DecodeOrderedSelfTerm(ref reader);
						return new FdbTuple<T1, T2>(value1, value2);
					}
				);
			}

			/// <summary>Create a composite encoder from a triplet of codecs</summary>
			public static IKeyValueEncoder<T1, T2, T3> Bind<T1, T2, T3>(IOrderedTypeCodec<T1> codec1, IOrderedTypeCodec<T2> codec2, IOrderedTypeCodec<T3> codec3)
			{
				if (codec1 == null) throw new ArgumentNullException("codec1");
				if (codec2 == null) throw new ArgumentNullException("codec2");
				if (codec3 == null) throw new ArgumentNullException("codec2");

				return new Composite<T1, T2, T3>(
					(value1, value2, value3) =>
					{
						var writer = SliceWriter.Empty;
						codec1.EncodeOrderedSelfTerm(ref writer, value1);
						codec2.EncodeOrderedSelfTerm(ref writer, value2);
						codec3.EncodeOrderedSelfTerm(ref writer, value3);
						return writer.ToSlice();
					},
					(encoded) =>
					{
						var reader = new SliceReader(encoded);
						T1 value1 = codec1.DecodeOrderedSelfTerm(ref reader);
						T2 value2 = codec2.DecodeOrderedSelfTerm(ref reader);
						T3 value3 = codec3.DecodeOrderedSelfTerm(ref reader);
						return new FdbTuple<T1, T2, T3>(value1, value2, value3);
					}
				);
			}

			public static void Partial<T1>(ref SliceWriter writer, IOrderedTypeCodec<T1> codec1, T1 value1)
			{
				Contract.Assert(codec1 != null);
				codec1.EncodeOrderedSelfTerm(ref writer, value1);
			}

			public static void Encode<T1, T2>(ref SliceWriter writer, IOrderedTypeCodec<T1> codec1, T1 value1, IOrderedTypeCodec<T2> codec2, T2 value2)
			{
				Contract.Assert(codec1 != null && codec2 != null);
				codec1.EncodeOrderedSelfTerm(ref writer, value1);
				codec2.EncodeOrderedSelfTerm(ref writer, value2);
			}

			public static void Encode<T1, T2, T3>(ref SliceWriter writer, IOrderedTypeCodec<T1> codec1, T1 value1, IOrderedTypeCodec<T2> codec2, T2 value2, IOrderedTypeCodec<T3> codec3, T3 value3)
			{
				Contract.Assert(codec1 != null && codec2 != null);
				codec1.EncodeOrderedSelfTerm(ref writer, value1);
				codec2.EncodeOrderedSelfTerm(ref writer, value2);
				codec3.EncodeOrderedSelfTerm(ref writer, value3);
			}

		}

		/// <summary>Encoders that produce compact but unordered slices, suitable for use as values, or unordered keys</summary>
		public static class Unordered
		{

			/// <summary>Create a simple encoder from a codec</summary>
			public static IKeyValueEncoder<T> Bind<T>(IUnorderedTypeCodec<T> codec)
			{
				if (codec == null) throw new ArgumentNullException("codec");

				var encoder = codec as IKeyValueEncoder<T>;
				if (encoder != null) return encoder;

				return new Singleton<T>(
					(value) => codec.EncodeUnordered(value),
					(encoded) => codec.DecodeUnordered(encoded)
				);
			}

			/// <summary>Create a composite encoder from a pair of codecs</summary>
			public static IKeyValueEncoder<T1, T2> Bind<T1, T2>(IUnorderedTypeCodec<T1> codec1, IUnorderedTypeCodec<T2> codec2)
			{
				if (codec1 == null) throw new ArgumentNullException("codec1");
				if (codec2 == null) throw new ArgumentNullException("codec2");

				return new Composite<T1, T2>(
					(value1, value2) =>
					{
						var writer = SliceWriter.Empty;
						codec1.EncodeUnorderedSelfTerm(ref writer, value1);
						codec2.EncodeUnorderedSelfTerm(ref writer, value2);
						return writer.ToSlice();
					},
					(encoded) =>
					{
						var reader = new SliceReader(encoded);
						T1 value1 = codec1.DecodeUnorderedSelfTerm(ref reader);
						T2 value2 = codec2.DecodeUnorderedSelfTerm(ref reader);
						return new FdbTuple<T1, T2>(value1, value2);
					}
				);
			}

			/// <summary>Create a composite encoder from a triplet of codecs</summary>
			public static IKeyValueEncoder<T1, T2, T3> Bind<T1, T2, T3>(IUnorderedTypeCodec<T1> codec1, IUnorderedTypeCodec<T2> codec2, IUnorderedTypeCodec<T3> codec3)
			{
				if (codec1 == null) throw new ArgumentNullException("codec1");
				if (codec2 == null) throw new ArgumentNullException("codec2");
				if (codec3 == null) throw new ArgumentNullException("codec2");

				return new Composite<T1, T2, T3>(
					(value1, value2, value3) =>
					{
						var writer = SliceWriter.Empty;
						codec1.EncodeUnorderedSelfTerm(ref writer, value1);
						codec2.EncodeUnorderedSelfTerm(ref writer, value2);
						codec3.EncodeUnorderedSelfTerm(ref writer, value3);
						return writer.ToSlice();
					},
					(encoded) =>
					{
						var reader = new SliceReader(encoded);
						T1 value1 = codec1.DecodeUnorderedSelfTerm(ref reader);
						T2 value2 = codec2.DecodeUnorderedSelfTerm(ref reader);
						T3 value3 = codec3.DecodeUnorderedSelfTerm(ref reader);
						return new FdbTuple<T1, T2, T3>(value1, value2, value3);
					}
				);
			}

		}

		public static class Tuples
		{

			public static IKeyValueEncoder<T1> Default<T1>()
			{
				return Ordered.Bind<T1>(FdbTupleCodec<T1>.Default);
			}

			public static IKeyValueEncoder<T1, T2> Default<T1, T2>()
			{
				return Ordered.Bind<T1, T2>(FdbTupleCodec<T1>.Default, FdbTupleCodec<T2>.Default);
			}

			public static IKeyValueEncoder<T1, T2, T3> Default<T1, T2, T3>()
			{
				return Ordered.Bind<T1, T2, T3>(FdbTupleCodec<T1>.Default, FdbTupleCodec<T2>.Default, FdbTupleCodec<T3>.Default);
			}

			/// <summary>Encodes and decodes tuples as ordered keys</summary>
			internal sealed class TupleEncoder : IKeyValueEncoder<IFdbTuple>
			{

				public TupleEncoder()
				{ }

				public Slice Encode(IFdbTuple value)
				{
					return value.ToSlice();
				}

				public IFdbTuple Decode(Slice encoded)
				{
					return FdbTuple.Unpack(encoded);
				}

				public Type[] GetTypes()
				{
					return new[] { typeof(IFdbTuple) };
				}
			}

		}

		public static IKeyValueEncoder<T> Bind<T>(Func<T, Slice> encoder, Func<Slice, T> decoder)
		{
			if (encoder == null) throw new ArgumentNullException("encoder");
			if (decoder == null) throw new ArgumentNullException("decoder");
			return new Singleton<T>(encoder, decoder);
		}

		public static IKeyValueEncoder<T1, T2> Bind<T1, T2>(Func<T1, T2, Slice> encoder, Func<Slice, FdbTuple<T1, T2>> decoder)
		{
			if (encoder == null) throw new ArgumentNullException("encoder");
			if (decoder == null) throw new ArgumentNullException("decoder");
			return new Composite<T1, T2>(encoder, decoder);
		}

		public static IKeyValueEncoder<T1, T2, T3> Bind<T1, T2, T3>(Func<T1, T2, T3, Slice> encoder, Func<Slice, FdbTuple<T1, T2, T3>> decoder)
		{
			if (encoder == null) throw new ArgumentNullException("encoder");
			if (decoder == null) throw new ArgumentNullException("decoder");
			return new Composite<T1, T2, T3>(encoder, decoder);
		}

		/// <summary>Convert an array of <typeparamref name="T">s into an array of slices, using a serializer (or the default serializer if none is provided)</summary>
		public static Slice[] EncodeRange<T>(this IKeyValueEncoder<T> encoder, T[] values)
		{
			if (values == null) throw new ArgumentNullException("values");
			if (encoder == null) throw new ArgumentNullException("encoder");

			var slices = new Slice[values.Length];
			for (int i = 0; i < values.Length; i++)
			{
				slices[i] = encoder.Encode(values[i]);
			}
			return slices;
		}

		/// <summary>Transform a sequence of <typeparamref name="T">s into a sequence of slices, using a serializer (or the default serializer if none is provided)</summary>
		public static IEnumerable<Slice> EncodeRange<T>(this IKeyValueEncoder<T> encoder, IEnumerable<T> values)
		{
			if (values == null) throw new ArgumentNullException("values");
			if (encoder == null) throw new ArgumentNullException("encoder");

			var array = values as T[];
			if (array != null) return EncodeRange<T>(encoder, array);

			return values.Select(value => encoder.Encode(value));
		}

		/// <summary>Convert an array of slices back into an array of <typeparamref name="T"/>s, using a serializer (or the default serializer if none is provided)</summary>
		public static T[] DecodeRange<T>(this IKeyValueEncoder<T> encoder, Slice[] slices)
		{
			if (encoder == null) throw new ArgumentNullException("encoder");
			if (slices == null) throw new ArgumentNullException("slices");

			var values = new T[slices.Length];
			for (int i = 0; i < slices.Length; i++)
			{
				values[i] = encoder.Decode(slices[i]);
			}
			return values;
		}

		/// <summary>Convert an array of slices back into an array of <typeparamref name="T"/>s, using a serializer (or the default serializer if none is provided)</summary>
		public static List<T> DecodeRange<T>(this IKeyValueEncoder<T> encoder, List<Slice> slices)
		{
			if (encoder == null) throw new ArgumentNullException("encoder");
			if (slices == null) throw new ArgumentNullException("slices");

			var values = new List<T>(slices.Count);
			foreach(var slice in slices)
			{
				values.Add(encoder.Decode(slice));
			}
			return values;
		}

		/// <summary>Transform a sequence of slices back into a sequence of <typeparamref name="T"/>s, using a serializer (or the default serializer if none is provided)</summary>
		public static IEnumerable<T> DecodeRange<T>(this IKeyValueEncoder<T> encoder, IEnumerable<Slice> slices)
		{
			if (encoder == null) throw new ArgumentNullException("encoder");
			if (slices == null) throw new ArgumentNullException("slices");

			var array = slices as Slice[];
			if (array != null) return DecodeRange<T>(encoder, array);

			return slices.Select(slice => encoder.Decode(slice));
		}
	}

}
