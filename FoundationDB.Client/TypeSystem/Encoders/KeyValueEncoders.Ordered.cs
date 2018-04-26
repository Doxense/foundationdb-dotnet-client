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
	using JetBrains.Annotations;
	using System;
	using Doxense.Collections.Tuples;
	using Doxense.Diagnostics.Contracts;
	using Doxense.Memory;

	/// <summary>Helper class for all key/value encoders</summary>
	public static partial class KeyValueEncoders
	{

		/// <summary>Encoders that produce lexicographically ordered slices, suitable for keys where lexicographical ordering is required</summary>
		[PublicAPI]
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

			public sealed class OrderedKeyEncoder<T> : IKeyEncoder<T>, IKeyEncoding
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

				public IKeyEncoding Encoding => this;

				#region IKeyEncoding...

				IDynamicKeyEncoder IKeyEncoding.GetDynamicEncoder() => throw new NotSupportedException();

				IKeyEncoder<T1> IKeyEncoding.GetEncoder<T1>()
				{
					if (typeof(T1) != typeof(T)) throw new NotSupportedException();
					return (IKeyEncoder<T1>) (object) this;
				}

				ICompositeKeyEncoder<T1, T2> IKeyEncoding.GetEncoder<T1, T2>() => throw new NotSupportedException();

				ICompositeKeyEncoder<T1, T2, T3> IKeyEncoding.GetEncoder<T1, T2, T3>() => throw new NotSupportedException();

				ICompositeKeyEncoder<T1, T2, T3, T4> IKeyEncoding.GetEncoder<T1, T2, T3, T4>() => throw new NotSupportedException();

				#endregion

			}

			public sealed class CodecCompositeKeyEncoder<T1, T2> : CompositeKeyEncoder<T1, T2>, IKeyEncoding
			{
				private readonly IOrderedTypeCodec<T1> m_codec1;
				private readonly IOrderedTypeCodec<T2> m_codec2;

				public CodecCompositeKeyEncoder(IOrderedTypeCodec<T1> codec1, IOrderedTypeCodec<T2> codec2)
				{
					m_codec1 = codec1;
					m_codec2 = codec2;
				}

				public override void WriteKeyPartsTo(ref SliceWriter writer, int count, ref (T1, T2) items)
				{
					Contract.Requires(count > 0);
					if (count >= 1) m_codec1.EncodeOrderedSelfTerm(ref writer, items.Item1);
					if (count >= 2) m_codec2.EncodeOrderedSelfTerm(ref writer, items.Item2);
				}

				public override void ReadKeyPartsFrom(ref SliceReader reader, int count, out (T1, T2) items)
				{
					Contract.Requires(count > 0);

					items.Item1 = count >= 1 ? m_codec1.DecodeOrderedSelfTerm(ref reader) : default;
					items.Item2 = count >= 2 ? m_codec2.DecodeOrderedSelfTerm(ref reader) : default;
					if (reader.HasMore) throw new InvalidOperationException($"Unexpected data at the end of composite key after {count} items");
				}

				public override IKeyEncoding Encoding => this;

				#region IKeyEncoding...

				IDynamicKeyEncoder IKeyEncoding.GetDynamicEncoder() => throw new NotSupportedException();

				IKeyEncoder<T1B> IKeyEncoding.GetEncoder<T1B>() => throw new NotSupportedException();

				ICompositeKeyEncoder<T1B, T2B> IKeyEncoding.GetEncoder<T1B, T2B>()
				{
					if (typeof(T1B) != typeof(T1) && typeof(T2B) != typeof(T2)) throw new NotSupportedException();
					return (ICompositeKeyEncoder<T1B, T2B>) (object) this;
				}

				ICompositeKeyEncoder<T1B, T2B, T3> IKeyEncoding.GetEncoder<T1B, T2B, T3>() => throw new NotSupportedException();

				ICompositeKeyEncoder<T1B, T2B, T3, T4> IKeyEncoding.GetEncoder<T1B, T2B, T3, T4>() => throw new NotSupportedException();

				#endregion

			}

			public sealed class CodecCompositeKeyEncoder<T1, T2, T3> : CompositeKeyEncoder<T1, T2, T3>, IKeyEncoding
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

				public override void WriteKeyPartsTo(ref SliceWriter writer, int count, ref (T1, T2, T3) items)
				{
					Contract.Requires(count > 0 && count <= 3);
					if (count >= 1) m_codec1.EncodeOrderedSelfTerm(ref writer, items.Item1);
					if (count >= 2) m_codec2.EncodeOrderedSelfTerm(ref writer, items.Item2);
					if (count >= 3) m_codec3.EncodeOrderedSelfTerm(ref writer, items.Item3);
				}

				public override void ReadKeyPartsFrom(ref SliceReader reader, int count, out (T1, T2, T3) items)
				{
					Contract.Requires(count > 0);

					items.Item1 = count >= 1 ? m_codec1.DecodeOrderedSelfTerm(ref reader) : default;
					items.Item2 = count >= 2 ? m_codec2.DecodeOrderedSelfTerm(ref reader) : default;
					items.Item3 = count >= 3 ? m_codec3.DecodeOrderedSelfTerm(ref reader) : default;
					if (reader.HasMore) throw new InvalidOperationException($"Unexpected data at the end of composite key after {count} items");
				}

				public override IKeyEncoding Encoding => this;

				#region IKeyEncoding...

				IDynamicKeyEncoder IKeyEncoding.GetDynamicEncoder() => throw new NotSupportedException();

				IKeyEncoder<T1B> IKeyEncoding.GetEncoder<T1B>() => throw new NotSupportedException();

				ICompositeKeyEncoder<T1B, T2B> IKeyEncoding.GetEncoder<T1B, T2B>() => throw new NotSupportedException();

				ICompositeKeyEncoder<T1B, T2B, T3B> IKeyEncoding.GetEncoder<T1B, T2B, T3B>()
				{
					if (typeof(T1B) != typeof(T1) && typeof(T2B) != typeof(T2) && typeof(T3B) != typeof(T3)) throw new NotSupportedException();
					return (ICompositeKeyEncoder<T1B, T2B, T3B>) (object) this;
				}

				ICompositeKeyEncoder<T1B, T2B, T3B, T4> IKeyEncoding.GetEncoder<T1B, T2B, T3B, T4>() => throw new NotSupportedException();

				#endregion
			}

			public sealed class CodecCompositeKeyEncoder<T1, T2, T3, T4> : CompositeKeyEncoder<T1, T2, T3, T4>, IKeyEncoding
			{
				private readonly IOrderedTypeCodec<T1> m_codec1;
				private readonly IOrderedTypeCodec<T2> m_codec2;
				private readonly IOrderedTypeCodec<T3> m_codec3;
				private readonly IOrderedTypeCodec<T4> m_codec4;

				public CodecCompositeKeyEncoder(IOrderedTypeCodec<T1> codec1, IOrderedTypeCodec<T2> codec2, IOrderedTypeCodec<T3> codec3, IOrderedTypeCodec<T4> codec4)
				{
					m_codec1 = codec1;
					m_codec2 = codec2;
					m_codec3 = codec3;
					m_codec4 = codec4;
				}

				public override void WriteKeyPartsTo(ref SliceWriter writer, int count, ref (T1, T2, T3, T4) items)
				{
					Contract.Requires(count > 0 && count <= 4);
					if (count >= 1) m_codec1.EncodeOrderedSelfTerm(ref writer, items.Item1);
					if (count >= 2) m_codec2.EncodeOrderedSelfTerm(ref writer, items.Item2);
					if (count >= 3) m_codec3.EncodeOrderedSelfTerm(ref writer, items.Item3);
					if (count >= 4) m_codec4.EncodeOrderedSelfTerm(ref writer, items.Item4);
				}

				public override void ReadKeyPartsFrom(ref SliceReader reader, int count, out (T1, T2, T3, T4) items)
				{
					Contract.Requires(count > 0);
					items.Item1 = count >= 1 ? m_codec1.DecodeOrderedSelfTerm(ref reader) : default;
					items.Item2 = count >= 2 ? m_codec2.DecodeOrderedSelfTerm(ref reader) : default;
					items.Item3 = count >= 3 ? m_codec3.DecodeOrderedSelfTerm(ref reader) : default;
					items.Item4 = count >= 4 ? m_codec4.DecodeOrderedSelfTerm(ref reader) : default;
					if (reader.HasMore) throw new InvalidOperationException($"Unexpected data at the end of composite key after {count} items");
				}

				public override IKeyEncoding Encoding => this;

				#region IKeyEncoding...

				IDynamicKeyEncoder IKeyEncoding.GetDynamicEncoder() => throw new NotSupportedException();

				IKeyEncoder<T1B> IKeyEncoding.GetEncoder<T1B>() => throw new NotSupportedException();

				ICompositeKeyEncoder<T1B, T2B> IKeyEncoding.GetEncoder<T1B, T2B>() => throw new NotSupportedException();

				ICompositeKeyEncoder<T1B, T2B, T3B> IKeyEncoding.GetEncoder<T1B, T2B, T3B>() => throw new NotSupportedException();

				ICompositeKeyEncoder<T1B, T2B, T3B, T4B> IKeyEncoding.GetEncoder<T1B, T2B, T3B, T4B>()
				{
					if (typeof(T1B) != typeof(T1) && typeof(T2B) != typeof(T2) && typeof(T3B) != typeof(T3) && typeof(T4B) != typeof(T4)) throw new NotSupportedException();
					return (ICompositeKeyEncoder<T1B, T2B, T3B, T4B>) (object) this;
				}

				#endregion
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
