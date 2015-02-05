#region BSD Licence
/* Copyright (c) 2013-2015, Doxense SAS
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
	using JetBrains.Annotations;
	using System;
	using System.Collections.Generic;
	using System.Linq;

	/// <summary>Helper class for all key/value encoders</summary>
	public static class KeyValueEncoders
	{
		/// <summary>Identity function for binary slices</summary>
		public static readonly IdentityEncoder Binary = new IdentityEncoder();

		#region Nested Classes

		/// <summary>Identity encoder</summary>
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

		/// <summary>Wrapper for encoding and decoding a pair with lambda functions</summary>
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

			public T1 DecodePartialKey(Slice encoded)
			{
				return DecodeComposite(encoded, 1).Item1;
			}

			public HeadEncoder<T1, T2> Head()
			{
				return new HeadEncoder<T1, T2>(this);
			}

		}

		/// <summary>Wrapper for encoding and decoding a triplet with lambda functions</summary>
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

			public FdbTuple<T1, T2, T3> DecodeKey(Slice encoded, int items)
			{
				return DecodeComposite(encoded, items);
			}

			public HeadEncoder<T1, T2, T3> Head()
			{
				return new HeadEncoder<T1, T2, T3>(this);
			}

			public PairEncoder<T1, T2, T3> Pair()
			{
				return new PairEncoder<T1, T2, T3>(this);
			}

		}

		/// <summary>Wrapper for encoding and decoding a quad with lambda functions</summary>
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

			public FdbTuple<T1, T2, T3, T4> DecodeKey(Slice encoded, int items)
			{
				return DecodeComposite(encoded, items);
			}
		}

		/// <summary>Wrapper for a composite encoder that will only output the first key</summary>
		public struct HeadEncoder<T1, T2> : IKeyEncoder<T1>
		{
			public readonly ICompositeKeyEncoder<T1, T2> Encoder;

			public HeadEncoder(ICompositeKeyEncoder<T1, T2> encoder)
			{
				this.Encoder = encoder;
			}

			public Slice EncodeKey(T1 value)
			{
				return this.Encoder.EncodeComposite(new FdbTuple<T1, T2>(value, default(T2)), 1);
			}

			public T1 DecodeKey(Slice encoded)
			{
				return this.Encoder.DecodeComposite(encoded, 1).Item1;
			}
		}

		/// <summary>Wrapper for a composite encoder that will only output the first key</summary>
		public struct HeadEncoder<T1, T2, T3> : IKeyEncoder<T1>
		{
			public readonly ICompositeKeyEncoder<T1, T2, T3> Encoder;

			public HeadEncoder(ICompositeKeyEncoder<T1, T2, T3> encoder)
			{
				this.Encoder = encoder;
			}

			public Slice EncodeKey(T1 value)
			{
				return this.Encoder.EncodeComposite(new FdbTuple<T1, T2, T3>(value, default(T2), default(T3)), 1);
			}

			public T1 DecodeKey(Slice encoded)
			{
				return this.Encoder.DecodeComposite(encoded, 1).Item1;
			}
		}

		/// <summary>Wrapper for a composite encoder that will only output the first and second keys</summary>
		public struct PairEncoder<T1, T2, T3> : ICompositeKeyEncoder<T1, T2>
		{
			public readonly ICompositeKeyEncoder<T1, T2, T3> Encoder;

			public PairEncoder(ICompositeKeyEncoder<T1, T2, T3> encoder)
			{
				this.Encoder = encoder;
			}

			public Slice EncodeKey(T1 value1, T2 value2)
			{
				return this.Encoder.EncodeComposite(new FdbTuple<T1, T2, T3>(value1, value2, default(T3)), 2);
			}

			public Slice EncodeComposite(FdbTuple<T1, T2> key, int items)
			{
				return this.Encoder.EncodeComposite(new FdbTuple<T1, T2, T3>(key.Item1, key.Item2, default(T3)), items);
			}

			public FdbTuple<T1, T2> DecodeComposite(Slice encoded, int items)
			{
				var t = this.Encoder.DecodeComposite(encoded, items);
				return new FdbTuple<T1, T2>(t.Item1, t.Item2);
			}

			public Slice EncodeKey(FdbTuple<T1, T2> value)
			{
				return EncodeComposite(value, 2);
			}

			public FdbTuple<T1, T2> DecodeKey(Slice encoded)
			{
				return DecodeComposite(encoded, 2);
			}
			public HeadEncoder<T1, T2, T3> Head()
			{
				return new HeadEncoder<T1, T2, T3>(this.Encoder);
			}
		}

		#endregion

		/// <summary>Encoders that produce lexicographically ordered slices, suitable for keys where lexicographical ordering is required</summary>
		public static class Ordered
		{
			public static IKeyEncoder<Slice> BinaryEncoder
			{
				[NotNull]
				get { return Tuples.Key<Slice>(); }
			}

			public static IKeyEncoder<string> StringEncoder
			{
				[NotNull]
				get { return Tuples.Key<string>(); }
			}

			public static IKeyEncoder<int> Int32Encoder
			{
				[NotNull]
				get { return Tuples.Key<int>(); }
			}

			public static IKeyEncoder<long> Int64Encoder
			{
				[NotNull]
				get { return Tuples.Key<long>(); }
			}

			public static IKeyEncoder<ulong> UInt64Encoder
			{
				[NotNull]
				get { return Tuples.Key<ulong>(); }
			}

			public sealed class OrderedKeyEncoder<T> : IKeyEncoder<T>
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
			[NotNull]
			public static IKeyEncoder<T> Bind<T>([NotNull] IOrderedTypeCodec<T> codec)
			{
				if (codec == null) throw new ArgumentNullException("codec");

				return new OrderedKeyEncoder<T>(codec);
			}

			/// <summary>Create a composite encoder from a pair of codecs</summary>
			[NotNull]
			public static ICompositeKeyEncoder<T1, T2> Bind<T1, T2>([NotNull] IOrderedTypeCodec<T1> codec1, [NotNull] IOrderedTypeCodec<T2> codec2)
			{
				if (codec1 == null) throw new ArgumentNullException("codec1");
				if (codec2 == null) throw new ArgumentNullException("codec2");

				return new CodecCompositeKeyEncoder<T1, T2>(codec1, codec2);
			}

			/// <summary>Create a composite encoder from a triplet of codecs</summary>
			[NotNull]
			public static ICompositeKeyEncoder<T1, T2, T3> Bind<T1, T2, T3>([NotNull] IOrderedTypeCodec<T1> codec1, [NotNull] IOrderedTypeCodec<T2> codec2, [NotNull] IOrderedTypeCodec<T3> codec3)
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

			public static void Encode<T1, T2>(ref SliceWriter writer, [NotNull] IOrderedTypeCodec<T1> codec1, T1 value1, [NotNull] IOrderedTypeCodec<T2> codec2, T2 value2)
			{
				Contract.Assert(codec1 != null && codec2 != null);
				codec1.EncodeOrderedSelfTerm(ref writer, value1);
				codec2.EncodeOrderedSelfTerm(ref writer, value2);
			}

			public static void Encode<T1, T2, T3>(ref SliceWriter writer, [NotNull] IOrderedTypeCodec<T1> codec1, T1 value1, [NotNull] IOrderedTypeCodec<T2> codec2, T2 value2, [NotNull] IOrderedTypeCodec<T3> codec3, T3 value3)
			{
				Contract.Assert(codec1 != null && codec2 != null && codec3 != null);
				codec1.EncodeOrderedSelfTerm(ref writer, value1);
				codec2.EncodeOrderedSelfTerm(ref writer, value2);
				codec3.EncodeOrderedSelfTerm(ref writer, value3);
			}

		}

		/// <summary>Encoders that produce compact but unordered slices, suitable for keys that don't benefit from having lexicographical ordering</summary>
		public static class Unordered
		{

			/// <summary>Create a simple encoder from a codec</summary>
			[NotNull]
			public static IKeyEncoder<T> Bind<T>([NotNull] IUnorderedTypeCodec<T> codec)
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

		/// <summary>Encoders that produce compact but unordered slices, suitable for values</summary>
		public static class Values
		{
			private static readonly GenericEncoder s_default = new GenericEncoder();

			public static IValueEncoder<Slice> BinaryEncoder
			{
				[NotNull]
				get { return s_default; }
			}

			public static IValueEncoder<string> StringEncoder
			{
				[NotNull]
				get { return s_default; }
			}

			public static IValueEncoder<int> Int32Encoder
			{
				[NotNull]
				get { return s_default; }
			}

			public static IValueEncoder<long> Int64Encoder
			{
				[NotNull]
				get { return s_default; }
			}

			public static IValueEncoder<Guid> GuidEncoder
			{
				[NotNull]
				get { return s_default; }
			}

			/// <summary>Create a simple encoder from a codec</summary>
			[NotNull]
			public static IValueEncoder<T> Bind<T>([NotNull] IUnorderedTypeCodec<T> codec)
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

		/// <summary>Encoders that use the Tuple Encoding, suitable for keys</summary>
		public static class Tuples
		{

			//TODO: rename to TupleEncoder<T>!
			internal class TupleKeyEncoder<T> : IKeyEncoder<T>, IValueEncoder<T>
			{
				public static readonly TupleKeyEncoder<T> Default = new TupleKeyEncoder<T>();

				private TupleKeyEncoder() { }

				public Slice EncodeKey(T key)
				{
					return FdbTuple.EncodeKey(key);
				}

				public T DecodeKey(Slice encoded)
				{
					if (encoded.IsNullOrEmpty) return default(T); //BUGBUG
					return FdbTuple.DecodeKey<T>(encoded);
				}

				public Slice EncodeValue(T key)
				{
					return FdbTuple.EncodeKey(key);
				}

				public T DecodeValue(Slice encoded)
				{
					if (encoded.IsNullOrEmpty) return default(T); //BUGBUG
					return FdbTuple.DecodeKey<T>(encoded);
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
						case 1: return FdbTuple.EncodeKey<T1>(key.Item1);
						default: throw new ArgumentOutOfRangeException("items", items, "Item count must be either 1 or 2");
					}
				}

				public override FdbTuple<T1, T2> DecodeComposite(Slice encoded, int items)
				{
					if (items < 1 || items > 2) throw new ArgumentOutOfRangeException("items", items, "Item count must be either 1 or 2");

					var t = FdbTuple.Unpack(encoded).OfSize(items);
					Contract.Assert(t != null);

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
						case 2: return FdbTuple.EncodeKey<T1, T2>(key.Item1, key.Item2);
						case 1: return FdbTuple.EncodeKey<T1>(key.Item1);
						default: throw new ArgumentOutOfRangeException("items", items, "Item count must be between 1 and 3");
					}
				}

				public override FdbTuple<T1, T2, T3> DecodeComposite(Slice encoded, int items)
				{
					if (items < 1 || items > 3) throw new ArgumentOutOfRangeException("items", items, "Item count must be between 1 and 3");

					var t = FdbTuple.Unpack(encoded).OfSize(items);
					Contract.Assert(t != null);

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
						case 3: return FdbTuple.EncodeKey(key.Item1, key.Item2, key.Item3);
						case 2: return FdbTuple.EncodeKey(key.Item1, key.Item2);
						case 1: return FdbTuple.EncodeKey(key.Item1);
						default: throw new ArgumentOutOfRangeException("items", items, "Item count must be between 1 and 4");
					}
				}

				public override FdbTuple<T1, T2, T3, T4> DecodeComposite(Slice encoded, int items)
				{
					if (items < 1 || items > 4) throw new ArgumentOutOfRangeException("items", items, "Item count must be between 1 and 4");

					var t = FdbTuple.Unpack(encoded).OfSize(items);

					return FdbTuple.Create<T1, T2, T3, T4>(
						t.Get<T1>(0),
						items >= 2 ? t.Get<T2>(1) : default(T2),
						items >= 3 ? t.Get<T3>(2) : default(T3),
						items >= 4 ? t.Get<T4>(3) : default(T4)
					);
				}
			}

			#region Keys

			[NotNull]
			public static IKeyEncoder<T1> Key<T1>()
			{
				return TupleKeyEncoder<T1>.Default;
			}

			[NotNull]
			public static ICompositeKeyEncoder<T1, T2> CompositeKey<T1, T2>()
			{
				return TupleCompositeEncoder<T1, T2>.Default;
			}

			[NotNull]
			public static ICompositeKeyEncoder<T1, T2, T3> CompositeKey<T1, T2, T3>()
			{
				return TupleCompositeEncoder<T1, T2, T3>.Default;
			}

			#endregion

			#region Values...

			[NotNull]
			public static IValueEncoder<T> Value<T>()
			{
				return TupleKeyEncoder<T>.Default;
			}

			#endregion

		}

		#region Keys...

		/// <summary>Binds a pair of lambda functions to a key encoder</summary>
		/// <typeparam name="T">Type of the key to encode</typeparam>
		/// <param name="encoder">Lambda function called to encode a key into a binary slice</param>
		/// <param name="decoder">Lambda function called to decode a binary slice into a key</param>
		/// <returns>Key encoder usable by any Layer that works on keys of type <typeparamref name="T"/></returns>
		[NotNull]
		public static IKeyEncoder<T> Bind<T>([NotNull] Func<T, Slice> encoder, [NotNull] Func<Slice, T> decoder)
		{
			if (encoder == null) throw new ArgumentNullException("encoder");
			if (decoder == null) throw new ArgumentNullException("decoder");
			return new Singleton<T>(encoder, decoder);
		}

		/// <summary>Convert an array of <typeparamref name="T"/>s into an array of slices, using a serializer (or the default serializer if none is provided)</summary>
		[NotNull]
		public static Slice[] EncodeKeys<T>([NotNull] this IKeyEncoder<T> encoder, [NotNull] params T[] values)
		{
			if (encoder == null) throw new ArgumentNullException("encoder");
			if (values == null) throw new ArgumentNullException("values");

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
			if (encoder == null) throw new ArgumentNullException("encoder");
			if (elements == null) throw new ArgumentNullException("elements");
			if (selector == null) throw new ArgumentNullException("selector");

			TElement[] arr;
			ICollection<TElement> coll;

			if ((arr = elements as TElement[]) != null)
			{ // fast path for arrays
				return EncodeKeys<TKey, TElement>(encoder, arr, selector);
			}
			else if ((coll = elements as ICollection<TElement>) != null)
			{ // we can pre-allocate the result array
				var slices = new Slice[coll.Count];
				int p = 0;
				foreach(var item in coll)
				{
					slices[p++] = encoder.EncodeKey(selector(item));
				}
				return slices;
			}
			else
			{ // slow path
				return elements
					.Select((item) => encoder.EncodeKey(selector(item)))
					.ToArray();
			}

		}

		/// <summary>Convert an array of <typeparamref name="TElement"/>s into an array of slices, using a serializer (or the default serializer if none is provided)</summary>
		[NotNull]
		public static Slice[] EncodeKeys<TKey, TElement>([NotNull] this IKeyEncoder<TKey> encoder, [NotNull] TElement[] elements, Func<TElement, TKey> selector)
		{
			if (encoder == null) throw new ArgumentNullException("encoder");
			if (elements == null) throw new ArgumentNullException("elements");
			if (selector == null) throw new ArgumentNullException("selector");

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
			if (encoder == null) throw new ArgumentNullException("encoder");
			if (values == null) throw new ArgumentNullException("values");

			// note: T=>Slice usually is used for writing batches as fast as possible, which means that keys will be consumed immediately and don't need to be streamed

			var array = values as T[];
			if (array != null)
			{ // optimized path for arrays
				return EncodeKeys<T>(encoder, array);
			}

			var coll = values as ICollection<T>;
			if (coll != null)
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
			if (encoder == null) throw new ArgumentNullException("encoder");
			if (slices == null) throw new ArgumentNullException("slices");

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
			if (encoder == null) throw new ArgumentNullException("encoder");
			if (items == null) throw new ArgumentNullException("items");

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
			if (encoder == null) throw new ArgumentNullException("encoder");
			if (slices == null) throw new ArgumentNullException("slices");

			// Slice=>T may be filtered in LINQ queries, so we should probably stream the values (so no optimization needed)

			return slices.Select(slice => encoder.DecodeKey(slice));
		}

		/// <summary>Returns a partial encoder that will only encode the first element</summary>
		public static HeadEncoder<T1, T2> Head<T1, T2>([NotNull] this ICompositeKeyEncoder<T1, T2> encoder)
		{
			if (encoder == null) throw new ArgumentNullException("encoder");
			return new HeadEncoder<T1, T2>(encoder);
		}

		/// <summary>Returns a partial encoder that will only encode the first element</summary>
		public static HeadEncoder<T1, T2, T3> Head<T1, T2, T3>([NotNull] this ICompositeKeyEncoder<T1, T2, T3> encoder)
		{
			if (encoder == null) throw new ArgumentNullException("encoder");

			return new HeadEncoder<T1, T2, T3>(encoder);
		}

		/// <summary>Returns a partial encoder that will only encode the first and second elements</summary>
		public static PairEncoder<T1, T2, T3> Pair<T1, T2, T3>([NotNull] this ICompositeKeyEncoder<T1, T2, T3> encoder)
		{
			if (encoder == null) throw new ArgumentNullException("encoder");

			return new PairEncoder<T1, T2, T3>(encoder);
		}

		#endregion

		#region Values...

		/// <summary>Convert an array of <typeparamref name="T"/>s into an array of slices, using a serializer (or the default serializer if none is provided)</summary>
		[NotNull]
		public static Slice[] EncodeValues<T>([NotNull] this IValueEncoder<T> encoder, [NotNull] params T[] values)
		{
			if (encoder == null) throw new ArgumentNullException("encoder");
			if (values == null) throw new ArgumentNullException("values");

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
			if (encoder == null) throw new ArgumentNullException("encoder");
			if (values == null) throw new ArgumentNullException("values");

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
			if (encoder == null) throw new ArgumentNullException("encoder");
			if (slices == null) throw new ArgumentNullException("slices");

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
			if (encoder == null) throw new ArgumentNullException("encoder");
			if (items == null) throw new ArgumentNullException("items");

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
			if (encoder == null) throw new ArgumentNullException("encoder");
			if (slices == null) throw new ArgumentNullException("slices");

			// Slice=>T may be filtered in LINQ queries, so we should probably stream the values (so no optimization needed)

			return slices.Select(slice => encoder.DecodeValue(slice));
		}

		#endregion
	}

}
