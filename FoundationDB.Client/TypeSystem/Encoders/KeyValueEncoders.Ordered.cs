
namespace Doxense.Serialization.Encoders
{
	using JetBrains.Annotations;
	using System;
	using Doxense.Collections.Tuples;
	using Doxense.Diagnostics.Contracts;
	using Doxense.Memory;

	/// <summary>Helper class for all key/value encoders</summary>
	public static partial class KeyValueEncoders
	{

		/// <summary>Encoders that produce lexicographically ordered slices, suitable for keys where lexicographical ordering is required</summary>
		public static class Ordered
		{
			[NotNull]
			public static IKeyEncoder<Slice> BinaryEncoder => Tuples.Key<Slice>();

			[NotNull]
			public static IKeyEncoder<string> StringEncoder => Tuples.Key<string>();

			[NotNull]
			public static IKeyEncoder<int> Int32Encoder => Tuples.Key<int>();

			[NotNull]
			public static IKeyEncoder<long> Int64Encoder => Tuples.Key<long>();

			[NotNull]
			public static IKeyEncoder<ulong> UInt64Encoder => Tuples.Key<ulong>();

			[NotNull]
			public static IKeyEncoder<Guid> GuidEncoder => Tuples.Key<Guid>();

			public sealed class OrderedKeyEncoder<T> : IKeyEncoder<T>
			{
				private readonly IOrderedTypeCodec<T> m_codec;

				public OrderedKeyEncoder(IOrderedTypeCodec<T> codec)
				{
					Contract.Requires(codec != null);
					m_codec = codec;
				}

				public void WriteKeyTo(ref SliceWriter writer, T key)
				{
					//TODO: PERF: optimize this!
					writer.WriteBytes(m_codec.EncodeOrdered(key));
				}

				public void ReadKeyFrom(ref SliceReader reader, out T key)
				{
					key = m_codec.DecodeOrdered(reader.ReadToEnd());
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

				public override void WriteKeyPartsTo(ref SliceWriter writer, int count, ref STuple<T1, T2> items)
				{
					Contract.Requires(count > 0);
					if (count >= 1) m_codec1.EncodeOrderedSelfTerm(ref writer, items.Item1);
					if (count >= 2) m_codec2.EncodeOrderedSelfTerm(ref writer, items.Item2);
				}

				public override void ReadKeyPartsFrom(ref SliceReader reader, int count, out STuple<T1, T2> items)
				{
					Contract.Requires(count > 0);

					T1 key1 = count >= 1 ? m_codec1.DecodeOrderedSelfTerm(ref reader) : default(T1);
					T2 key2 = count >= 2 ? m_codec2.DecodeOrderedSelfTerm(ref reader) : default(T2);
					if (reader.HasMore) throw new InvalidOperationException($"Unexpected data at the end of composite key after {count} items");
					items = new STuple<T1, T2>(key1, key2);
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

				public override void WriteKeyPartsTo(ref SliceWriter writer, int count, ref STuple<T1, T2, T3> items)
				{
					Contract.Requires(count > 0 && count <= 3);
					if (count >= 1) m_codec1.EncodeOrderedSelfTerm(ref writer, items.Item1);
					if (count >= 2) m_codec2.EncodeOrderedSelfTerm(ref writer, items.Item2);
					if (count >= 3) m_codec3.EncodeOrderedSelfTerm(ref writer, items.Item3);
				}

				public override void ReadKeyPartsFrom(ref SliceReader reader, int count, out STuple<T1, T2, T3> items)
				{
					Contract.Requires(count > 0);

					T1 key1 = count >= 1 ? m_codec1.DecodeOrderedSelfTerm(ref reader) : default(T1);
					T2 key2 = count >= 2 ? m_codec2.DecodeOrderedSelfTerm(ref reader) : default(T2);
					T3 key3 = count >= 3 ? m_codec3.DecodeOrderedSelfTerm(ref reader) : default(T3);
					if (reader.HasMore) throw new InvalidOperationException($"Unexpected data at the end of composite key after {count} items");
					items = new STuple<T1, T2, T3>(key1, key2, key3);
				}

			}

			/// <summary>Create a simple encoder from a codec</summary>
			[NotNull]
			public static IKeyEncoder<T> Bind<T>([NotNull] IOrderedTypeCodec<T> codec)
			{
				Contract.NotNull(codec, nameof(codec));

				return new OrderedKeyEncoder<T>(codec);
			}

			/// <summary>Create a composite encoder from a pair of codecs</summary>
			[NotNull]
			public static ICompositeKeyEncoder<T1, T2> Bind<T1, T2>([NotNull] IOrderedTypeCodec<T1> codec1, [NotNull] IOrderedTypeCodec<T2> codec2)
			{
				Contract.NotNull(codec1, nameof(codec1));
				Contract.NotNull(codec2, nameof(codec2));

				return new CodecCompositeKeyEncoder<T1, T2>(codec1, codec2);
			}

			/// <summary>Create a composite encoder from a triplet of codecs</summary>
			[NotNull]
			public static ICompositeKeyEncoder<T1, T2, T3> Bind<T1, T2, T3>([NotNull] IOrderedTypeCodec<T1> codec1, [NotNull] IOrderedTypeCodec<T2> codec2, [NotNull] IOrderedTypeCodec<T3> codec3)
			{
				Contract.NotNull(codec1, nameof(codec1));
				Contract.NotNull(codec2, nameof(codec2));
				Contract.NotNull(codec3, nameof(codec3));

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

	}

}
