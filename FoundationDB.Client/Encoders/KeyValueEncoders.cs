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
		public static IdentityEncoder Binary = new IdentityEncoder();

		#region Nested Classes

		public sealed class IdentityEncoder : IKeyEncoder<Slice>, IValueEncoder<Slice>
		{

			internal IdentityEncoder() { }

			public Slice EncodeKey(Slice key)
			{
				return key;
			}

			public Slice DecodeKey(Slice encoded)
			{
				return encoded;
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

			public Slice EncodeKey(T value)
			{
				return m_encoder(value);
			}

			public T DecodeKey(Slice encoded)
			{
				return m_decoder(encoded);
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

		public abstract class CompositeKeyEncoder<T1, T2> : ICompositeKeyEncoder<T1, T2>
		{

			public abstract Slice EncodeComposite(FdbTuple<T1, T2> key, int items);

			public abstract FdbTuple<T1, T2> DecodeComposite(Slice encoded, int items);

			public Slice EncodeKey(FdbTuple<T1, T2> key)
			{
				return EncodeComposite(key, 2);
			}

			public virtual Slice EncodeKey(T1 item1, T2 item2)
			{
				return EncodeComposite(FdbTuple.Create<T1, T2>(item1, item2), 2);
			}

			public virtual FdbTuple<T1, T2> DecodeKey(Slice encoded)
			{
				return DecodeComposite(encoded, 2);
			}

			public Slice EncodeKey(T1 item1)
			{
				return EncodeComposite(FdbTuple.Create<T1, T2>(item1, default(T2)), 1);
			}

			public T1 DecodePartialKey(Slice encoded)
			{
				return DecodeComposite(encoded, 1).Item1;
			}
		}

		public abstract class CompositeKeyEncoder<T1, T2, T3> : ICompositeKeyEncoder<T1, T2, T3>
		{

			public abstract Slice EncodeComposite(FdbTuple<T1, T2, T3> items, int count);

			public abstract FdbTuple<T1, T2, T3> DecodeComposite(Slice encoded, int count);

			public Slice EncodeKey(FdbTuple<T1, T2, T3> key)
			{
				return EncodeComposite(key, 3);
			}

			public virtual Slice EncodeKey(T1 item1, T2 item2, T3 item3)
			{
				return EncodeComposite(FdbTuple.Create<T1, T2, T3>(item1, item2, item3), 3);
			}

			public virtual FdbTuple<T1, T2, T3> DecodeKey(Slice encoded)
			{
				return DecodeComposite(encoded, 3);
			}

			public Slice EncodeKey(T1 item1, T2 item2)
			{
				return EncodeComposite(FdbTuple.Create<T1, T2, T3>(item1, item2, default(T3)), 2);
			}

			public Slice EncodeKey(T1 item1)
			{
				return EncodeComposite(FdbTuple.Create<T1, T2, T3>(item1, default(T2), default(T3)), 1);
			}

			public FdbTuple<T1, T2, T3> DecodeKey(Slice encoded, int items)
			{
				return DecodeComposite(encoded, items);
			}
		}

		public abstract class CompositeKeyEncoder<T1, T2, T3, T4> : ICompositeKeyEncoder<T1, T2, T3, T4>
		{

			public abstract Slice EncodeComposite(FdbTuple<T1, T2, T3, T4> items, int count);

			public abstract FdbTuple<T1, T2, T3, T4> DecodeComposite(Slice encoded, int count);

			public Slice EncodeKey(FdbTuple<T1, T2, T3, T4> key)
			{
				return EncodeComposite(key, 4);
			}

			public virtual Slice EncodeKey(T1 item1, T2 item2, T3 item3, T4 item4)
			{
				return EncodeComposite(FdbTuple.Create<T1, T2, T3, T4>(item1, item2, item3, item4), 4);
			}

			public virtual FdbTuple<T1, T2, T3, T4> DecodeKey(Slice encoded)
			{
				return DecodeComposite(encoded, 4);
			}

			public Slice EncodeKey(T1 item1, T2 item2, T3 item3)
			{
				return EncodeComposite(FdbTuple.Create<T1, T2, T3, T4>(item1, item2, item3, default(T4)), 3);
			}

			public Slice EncodeKey(T1 item1, T2 item2)
			{
				return EncodeComposite(FdbTuple.Create<T1, T2, T3, T4>(item1, item2, default(T3), default(T4)), 2);
			}

			public Slice EncodeKey(T1 item1)
			{
				return EncodeComposite(FdbTuple.Create<T1, T2, T3, T4>(item1, default(T2), default(T3), default(T4)), 1);
			}

			public FdbTuple<T1, T2, T3, T4> DecodeKey(Slice encoded, int items)
			{
				return DecodeComposite(encoded, items);
			}
		}

		#endregion

		/// <summary>Encoders that produce lexicographically ordered slices, suitable for use as keys</summary>
		public static class Ordered
		{
			public static IKeyEncoder<Slice> BinaryEncoder { get { return Tuples.Key<Slice>(); } }

			public static IKeyEncoder<string> StringEncoder { get { return Tuples.Key<string>(); } }

			public static IKeyEncoder<int> Int32Encoder { get { return Tuples.Key<int>(); } }

			public static IKeyEncoder<long> Int64Encoder { get { return Tuples.Key<long>(); } }

			public static IKeyEncoder<ulong> UInt64Encoder { get { return Tuples.Key<ulong>(); } }

			internal sealed class OrderedKeyEncoder<T> : IKeyEncoder<T>
			{
				private readonly IOrderedTypeCodec<T> m_codec;

				public OrderedKeyEncoder(IOrderedTypeCodec<T> codec)
				{
					Contract.Requires(codec != null);
					m_codec = codec;
				}

				public Slice EncodeKey(T key)
				{
					return m_codec.EncodeOrdered(key);
				}

				public T DecodeKey(Slice encoded)
				{
					return m_codec.DecodeOrdered(encoded);
				}
			}

			public sealed class CodecCompositeKeyEncoder<T1, T2> : CompositeKeyEncoder<T1, T2>
			{
				private readonly IOrderedTypeCodec<T1> m_codec1;
				private readonly IOrderedTypeCodec<T2> m_codec2;

				public CodecCompositeKeyEncoder(IOrderedTypeCodec<T1> codec1, IOrderedTypeCodec<T2> codec2)
				{
					m_codec1 = codec1;
					m_codec2 = codec2;
				}

				public override Slice EncodeComposite(FdbTuple<T1, T2> items, int count)
				{
					Contract.Requires(count > 0);

					var writer = SliceWriter.Empty;
					if (count >= 1) m_codec1.EncodeOrderedSelfTerm(ref writer, items.Item1);
					if (count >= 2) m_codec2.EncodeOrderedSelfTerm(ref writer, items.Item2);
					return writer.ToSlice();
				}

				public override FdbTuple<T1, T2> DecodeComposite(Slice encoded, int count)
				{
					Contract.Requires(count > 0);

					var reader = new SliceReader(encoded);
					T1 key1 = default(T1);
					T2 key2 = default(T2);
					if (count >= 1) key1 = m_codec1.DecodeOrderedSelfTerm(ref reader);
					if (count >= 2) key2 = m_codec2.DecodeOrderedSelfTerm(ref reader);
					if (reader.HasMore) throw new InvalidOperationException(String.Format("Unexpected data at the end of composite key after {0} items", count));
					return FdbTuple.Create<T1, T2>(key1, key2);
				}
			}

			public sealed class CodecCompositeKeyEncoder<T1, T2, T3> : CompositeKeyEncoder<T1, T2, T3>
			{
				private readonly IOrderedTypeCodec<T1> m_codec1;
				private readonly IOrderedTypeCodec<T2> m_codec2;
				private readonly IOrderedTypeCodec<T3> m_codec3;

				public CodecCompositeKeyEncoder(IOrderedTypeCodec<T1> codec1, IOrderedTypeCodec<T2> codec2, IOrderedTypeCodec<T3> codec3)
				{
					m_codec1 = codec1;
					m_codec2 = codec2;
					m_codec3 = codec3;
				}

				public override Slice EncodeComposite(FdbTuple<T1, T2, T3> items, int count)
				{
					Contract.Requires(count > 0 && count <= 3);

					var writer = SliceWriter.Empty;
					if (count >= 1) m_codec1.EncodeOrderedSelfTerm(ref writer, items.Item1);
					if (count >= 2) m_codec2.EncodeOrderedSelfTerm(ref writer, items.Item2);
					if (count >= 3) m_codec3.EncodeOrderedSelfTerm(ref writer, items.Item3);
					return writer.ToSlice();
				}

				public override FdbTuple<T1, T2, T3> DecodeComposite(Slice encoded, int count)
				{
					Contract.Requires(count > 0);

					var reader = new SliceReader(encoded);
					T1 key1 = default(T1);
					T2 key2 = default(T2);
					T3 key3 = default(T3);
					if (count >= 1) key1 = m_codec1.DecodeOrderedSelfTerm(ref reader);
					if (count >= 2) key2 = m_codec2.DecodeOrderedSelfTerm(ref reader);
					if (count >= 3) key3 = m_codec3.DecodeOrderedSelfTerm(ref reader);
					if (reader.HasMore) throw new InvalidOperationException(String.Format("Unexpected data at the end of composite key after {0} items", count));
					return FdbTuple.Create<T1, T2, T3>(key1, key2, key3);
				}
			}

			/// <summary>Create a simple encoder from a codec</summary>
			public static IKeyEncoder<T> Bind<T>(IOrderedTypeCodec<T> codec)
			{
				if (codec == null) throw new ArgumentNullException("codec");

				return new OrderedKeyEncoder<T>(codec);
			}

			/// <summary>Create a composite encoder from a pair of codecs</summary>
			public static ICompositeKeyEncoder<T1, T2> Bind<T1, T2>(IOrderedTypeCodec<T1> codec1, IOrderedTypeCodec<T2> codec2)
			{
				if (codec1 == null) throw new ArgumentNullException("codec1");
				if (codec2 == null) throw new ArgumentNullException("codec2");

				return new CodecCompositeKeyEncoder<T1, T2>(codec1, codec2);
			}

			/// <summary>Create a composite encoder from a triplet of codecs</summary>
			public static ICompositeKeyEncoder<T1, T2, T3> Bind<T1, T2, T3>(IOrderedTypeCodec<T1> codec1, IOrderedTypeCodec<T2> codec2, IOrderedTypeCodec<T3> codec3)
			{
				if (codec1 == null) throw new ArgumentNullException("codec1");
				if (codec2 == null) throw new ArgumentNullException("codec2");
				if (codec3 == null) throw new ArgumentNullException("codec2");

				return new CodecCompositeKeyEncoder<T1, T2, T3>(codec1, codec2, codec3);
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
			public static IKeyEncoder<T> Bind<T>(IUnorderedTypeCodec<T> codec)
			{
				if (codec == null) throw new ArgumentNullException("codec");

				var encoder = codec as IKeyEncoder<T>;
				if (encoder != null) return encoder;

				return new Singleton<T>(
					(value) => codec.EncodeUnordered(value),
					(encoded) => codec.DecodeUnordered(encoded)
				);
			}

		}

		public static class Values
		{
			private static readonly GenericEncoder s_default = new GenericEncoder();

			public static IValueEncoder<Slice> BinaryEncoder { get { return s_default; } }

			public static IValueEncoder<string> StringEncoder { get { return s_default; } }

			public static IValueEncoder<int> Int32Encoder { get { return s_default; } }

			public static IValueEncoder<long> Int64Encoder { get { return s_default; } }

			public static IValueEncoder<Guid> GuidEncoder { get { return s_default; } }

			/// <summary>Create a simple encoder from a codec</summary>
			public static IValueEncoder<T> Bind<T>(IUnorderedTypeCodec<T> codec)
			{
				if (codec == null) throw new ArgumentNullException("codec");

				var encoder = codec as IValueEncoder<T>;
				if (encoder != null) return encoder;

				return new Singleton<T>(
					(value) => codec.EncodeUnordered(value),
					(encoded) => codec.DecodeUnordered(encoded)
				);
			}

			internal sealed class GenericEncoder : IValueEncoder<Slice>, IValueEncoder<string>, IValueEncoder<int>, IValueEncoder<long>, IValueEncoder<Guid>
			{

				public Slice EncodeValue(Slice value)
				{
					return value;
				}

				Slice IValueEncoder<Slice>.DecodeValue(Slice encoded)
				{
					return encoded;
				}

				public Slice EncodeValue(string value)
				{
					return Slice.FromString(value);
				}

				string IValueEncoder<string>.DecodeValue(Slice encoded)
				{
					return encoded.ToUnicode();
				}

				public Slice EncodeValue(int value)
				{
					return Slice.FromInt32(value);
				}

				int IValueEncoder<int>.DecodeValue(Slice encoded)
				{
					return encoded.ToInt32();
				}

				public Slice EncodeValue(long value)
				{
					return Slice.FromInt64(value);
				}

				long IValueEncoder<long>.DecodeValue(Slice encoded)
				{
					return encoded.ToInt64();
				}

				public Slice EncodeValue(Guid value)
				{
					return Slice.FromGuid(value);
				}

				Guid IValueEncoder<Guid>.DecodeValue(Slice encoded)
				{
					return encoded.ToGuid();
				}

			}

		}

		public static class Tuples
		{

			internal class TupleKeyEncoder<T> : IKeyEncoder<T>, IValueEncoder<T>
			{
				public static readonly TupleKeyEncoder<T> Default = new TupleKeyEncoder<T>();

				private TupleKeyEncoder() { }

				public Slice EncodeKey(T key)
				{
					return FdbTuple.Pack<T>(key);
				}

				public T DecodeKey(Slice encoded)
				{
					if (encoded.IsNullOrEmpty) return default(T); //BUGBUG
					return FdbTuple.UnpackSingle<T>(encoded);
				}

				public Slice EncodeValue(T key)
				{
					return FdbTuple.Pack<T>(key);
				}

				public T DecodeValue(Slice encoded)
				{
					if (encoded.IsNullOrEmpty) return default(T); //BUGBUG
					return FdbTuple.UnpackSingle<T>(encoded);
				}
	
			}

			internal class TupleCompositeEncoder<T1, T2> : CompositeKeyEncoder<T1, T2>
			{

				public static readonly TupleCompositeEncoder<T1, T2> Default = new TupleCompositeEncoder<T1, T2>();

				private TupleCompositeEncoder() { }

				public override Slice EncodeComposite(FdbTuple<T1, T2> key, int items)
				{
					switch (items)
					{
						case 2: return key.ToSlice();
						case 1: return FdbTuple.Pack<T1>(key.Item1);
						default: throw new ArgumentOutOfRangeException("items", items, "Item count must be either 1 or 2");
					}
				}

				public override FdbTuple<T1, T2> DecodeComposite(Slice encoded, int items)
				{
					if (items < 1 || items > 2) throw new ArgumentOutOfRangeException("items", items, "Item count must be either 1 or 2");

					var t = FdbTuple.Unpack(encoded);
					Contract.Assert(t != null);
					if (t.Count != items) throw new ArgumentException(String.Format("Was expected {0} items, but decoded tuple only has {1}", items, t.Count));

					return FdbTuple.Create<T1, T2>(
						t.Get<T1>(0),
						items >= 2 ? t.Get<T2>(1) : default(T2)
					);
				}
			}

			internal class TupleCompositeEncoder<T1, T2, T3> : CompositeKeyEncoder<T1, T2, T3>
			{

				public static readonly TupleCompositeEncoder<T1, T2, T3> Default = new TupleCompositeEncoder<T1, T2, T3>();

				private TupleCompositeEncoder() { }

				public override Slice EncodeComposite(FdbTuple<T1, T2, T3> key, int items)
				{
					switch (items)
					{
						case 3: return key.ToSlice();
						case 2: return FdbTuple.Pack<T1, T2>(key.Item1, key.Item2);
						case 1: return FdbTuple.Pack<T1>(key.Item1);
						default: throw new ArgumentOutOfRangeException("items", items, "Item count must be between 1 and 3");
					}
				}

				public override FdbTuple<T1, T2, T3> DecodeComposite(Slice encoded, int items)
				{
					if (items < 1 || items > 3) throw new ArgumentOutOfRangeException("items", items, "Item count must be between 1 and 3");

					var t = FdbTuple.Unpack(encoded);
					Contract.Assert(t != null);
					if (t.Count != items) throw new ArgumentException(String.Format("Was expected {0} items, but decoded tuple only has {1}", items, t.Count));

					return FdbTuple.Create<T1, T2, T3>(
						t.Get<T1>(0),
						items >= 2 ? t.Get<T2>(1) : default(T2),
						items >= 3 ? t.Get<T3>(2) : default(T3)
					);
				}
			}

			internal class TupleCompositeEncoder<T1, T2, T3, T4> : CompositeKeyEncoder<T1, T2, T3, T4>
			{

				public static readonly TupleCompositeEncoder<T1, T2, T3, T4> Default = new TupleCompositeEncoder<T1, T2, T3, T4>();

				private TupleCompositeEncoder() { }

				public override Slice EncodeComposite(FdbTuple<T1, T2, T3, T4> key, int items)
				{
					switch (items)
					{
						case 4: return key.ToSlice();
						case 3: return FdbTuple.Pack(key.Item1, key.Item2, key.Item3);
						case 2: return FdbTuple.Pack(key.Item1, key.Item2);
						case 1: return FdbTuple.Pack(key.Item1);
						default: throw new ArgumentOutOfRangeException("items", items, "Item count must be between 1 and 4");
					}
				}

				public override FdbTuple<T1, T2, T3, T4> DecodeComposite(Slice encoded, int items)
				{
					if (items < 1 || items > 4) throw new ArgumentOutOfRangeException("items", items, "Item count must be between 1 and 4");

					var t = FdbTuple.Unpack(encoded);
					Contract.Assert(t != null);
					if (t.Count != items) throw new ArgumentException(String.Format("Was expected {0} items, but decoded tuple only has {1}", items, t.Count));

					return FdbTuple.Create<T1, T2, T3, T4>(
						t.Get<T1>(0),
						items >= 2 ? t.Get<T2>(1) : default(T2),
						items >= 3 ? t.Get<T3>(2) : default(T3),
						items >= 4 ? t.Get<T4>(3) : default(T4)
					);
				}
			}

			#region Keys

			public static IKeyEncoder<T1> Key<T1>()
			{
				return TupleKeyEncoder<T1>.Default;
			}

			public static ICompositeKeyEncoder<T1, T2> CompositeKey<T1, T2>()
			{
				return TupleCompositeEncoder<T1, T2>.Default;
			}

			public static ICompositeKeyEncoder<T1, T2, T3> CompositeKey<T1, T2, T3>()
			{
				return TupleCompositeEncoder<T1, T2, T3>.Default;
			}

			#endregion

			#region Values...

			public static IValueEncoder<T> Value<T>()
			{
				return TupleKeyEncoder<T>.Default;
			}

			#endregion

		}

		#region Keys...

		public static IKeyEncoder<T> Bind<T>(Func<T, Slice> encoder, Func<Slice, T> decoder)
		{
			if (encoder == null) throw new ArgumentNullException("encoder");
			if (decoder == null) throw new ArgumentNullException("decoder");
			return new Singleton<T>(encoder, decoder);
		}

		/// <summary>Convert an array of <typeparamref name="T">s into an array of slices, using a serializer (or the default serializer if none is provided)</summary>
		public static Slice[] EncodeRange<T>(this IKeyEncoder<T> encoder, T[] values)
		{
			if (values == null) throw new ArgumentNullException("values");
			if (encoder == null) throw new ArgumentNullException("encoder");

			var slices = new Slice[values.Length];
			for (int i = 0; i < values.Length; i++)
			{
				slices[i] = encoder.EncodeKey(values[i]);
			}
			return slices;
		}

		/// <summary>Transform a sequence of <typeparamref name="T">s into a sequence of slices, using a serializer (or the default serializer if none is provided)</summary>
		public static IEnumerable<Slice> EncodeRange<T>(this IKeyEncoder<T> encoder, IEnumerable<T> values)
		{
			if (values == null) throw new ArgumentNullException("values");
			if (encoder == null) throw new ArgumentNullException("encoder");

			var array = values as T[];
			if (array != null) return EncodeRange<T>(encoder, array);

			return values.Select(value => encoder.EncodeKey(value));
		}

		/// <summary>Convert an array of slices back into an array of <typeparamref name="T"/>s, using a serializer (or the default serializer if none is provided)</summary>
		public static T[] DecodeRange<T>(this IKeyEncoder<T> encoder, Slice[] slices)
		{
			if (encoder == null) throw new ArgumentNullException("encoder");
			if (slices == null) throw new ArgumentNullException("slices");

			var values = new T[slices.Length];
			for (int i = 0; i < slices.Length; i++)
			{
				values[i] = encoder.DecodeKey(slices[i]);
			}
			return values;
		}

		/// <summary>Convert an array of slices back into an array of <typeparamref name="T"/>s, using a serializer (or the default serializer if none is provided)</summary>
		public static List<T> DecodeRange<T>(this IKeyEncoder<T> encoder, List<Slice> slices)
		{
			if (encoder == null) throw new ArgumentNullException("encoder");
			if (slices == null) throw new ArgumentNullException("slices");

			var values = new List<T>(slices.Count);
			foreach (var slice in slices)
			{
				values.Add(encoder.DecodeKey(slice));
			}
			return values;
		}

		/// <summary>Transform a sequence of slices back into a sequence of <typeparamref name="T"/>s, using a serializer (or the default serializer if none is provided)</summary>
		public static IEnumerable<T> DecodeRange<T>(this IKeyEncoder<T> encoder, IEnumerable<Slice> slices)
		{
			if (encoder == null) throw new ArgumentNullException("encoder");
			if (slices == null) throw new ArgumentNullException("slices");

			var array = slices as Slice[];
			if (array != null) return DecodeRange<T>(encoder, array);

			return slices.Select(slice => encoder.DecodeKey(slice));
		}

		#endregion

		#region Values...

		/// <summary>Convert an array of <typeparamref name="T">s into an array of slices, using a serializer (or the default serializer if none is provided)</summary>
		public static Slice[] EncodeRange<T>(this IValueEncoder<T> encoder, T[] values)
		{
			if (values == null) throw new ArgumentNullException("values");
			if (encoder == null) throw new ArgumentNullException("encoder");

			var slices = new Slice[values.Length];
			for (int i = 0; i < values.Length; i++)
			{
				slices[i] = encoder.EncodeValue(values[i]);
			}
			return slices;
		}

		/// <summary>Transform a sequence of <typeparamref name="T">s into a sequence of slices, using a serializer (or the default serializer if none is provided)</summary>
		public static IEnumerable<Slice> EncodeRange<T>(this IValueEncoder<T> encoder, IEnumerable<T> values)
		{
			if (values == null) throw new ArgumentNullException("values");
			if (encoder == null) throw new ArgumentNullException("encoder");

			var array = values as T[];
			if (array != null) return EncodeRange<T>(encoder, array);

			return values.Select(value => encoder.EncodeValue(value));
		}

		/// <summary>Convert an array of slices back into an array of <typeparamref name="T"/>s, using a serializer (or the default serializer if none is provided)</summary>
		public static T[] DecodeRange<T>(this IValueEncoder<T> encoder, Slice[] slices)
		{
			if (encoder == null) throw new ArgumentNullException("encoder");
			if (slices == null) throw new ArgumentNullException("slices");

			var values = new T[slices.Length];
			for (int i = 0; i < slices.Length; i++)
			{
				values[i] = encoder.DecodeValue(slices[i]);
			}
			return values;
		}

		/// <summary>Convert an array of slices back into an array of <typeparamref name="T"/>s, using a serializer (or the default serializer if none is provided)</summary>
		public static List<T> DecodeRange<T>(this IValueEncoder<T> encoder, List<Slice> slices)
		{
			if (encoder == null) throw new ArgumentNullException("encoder");
			if (slices == null) throw new ArgumentNullException("slices");

			var values = new List<T>(slices.Count);
			foreach (var slice in slices)
			{
				values.Add(encoder.DecodeValue(slice));
			}
			return values;
		}

		/// <summary>Transform a sequence of slices back into a sequence of <typeparamref name="T"/>s, using a serializer (or the default serializer if none is provided)</summary>
		public static IEnumerable<T> DecodeRange<T>(this IValueEncoder<T> encoder, IEnumerable<Slice> slices)
		{
			if (encoder == null) throw new ArgumentNullException("encoder");
			if (slices == null) throw new ArgumentNullException("slices");

			var array = slices as Slice[];
			if (array != null) return DecodeRange<T>(encoder, array);

			return slices.Select(slice => encoder.DecodeValue(slice));
		}

		#endregion
	}

}
