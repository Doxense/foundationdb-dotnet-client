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

namespace Doxense.Serialization.Json.Encoders
{
	using System.Buffers;
	using SnowBank.Data.Binary;
	using SnowBank.Buffers;

	/// <summary>Codecs that encodes CLR types into either database keys (ordered) or values (unordered)</summary>
	[PublicAPI]
	public static class CrystalJsonCodec
	{

		internal static readonly CrystalJsonSettings DefaultJsonSettings = CrystalJsonSettings.JsonCompact.WithEnumAsStrings().WithIso8601Dates();

		public static CrystalJsonCodec<T> GetEncoder<T>(CrystalJsonSettings? settings = null, ICrystalJsonTypeResolver? resolver = null) => new(settings, resolver);

		public static CrystalJsonCodec<T> GetEncoder<T>(IJsonConverter<T> converter, CrystalJsonSettings? settings = null, ICrystalJsonTypeResolver? resolver = null) => new(settings, resolver);

	}

	/// <summary>Codec that encodes <typeparamref name="T"/> instances into either database keys (ordered) or values (unordered)</summary>
	[PublicAPI]
	public class CrystalJsonCodec<T> : TypeCodec<T>, IValueEncoder<T>
	{

		private readonly CrystalJsonSettings m_settings;
		private readonly ICrystalJsonTypeResolver m_resolver;
		private readonly IJsonConverter<T>? m_converter;

		public CrystalJsonCodec()
			: this(null, null)
		{ }

		public CrystalJsonCodec(CrystalJsonSettings? settings = null, ICrystalJsonTypeResolver? resolver = null)
		{
			m_settings = settings ?? CrystalJsonCodec.DefaultJsonSettings;
			m_resolver = resolver ?? CrystalJson.DefaultResolver;
		}

		public CrystalJsonCodec(IJsonConverter<T> converter, CrystalJsonSettings? settings = null, ICrystalJsonTypeResolver? resolver = null)
		{
			m_converter = converter;
			m_settings = settings ?? CrystalJsonCodec.DefaultJsonSettings;
			m_resolver = CrystalJson.DefaultResolver;
		}

		public CrystalJsonSettings Settings => m_settings;

		public ICrystalJsonTypeResolver Resolver => m_resolver;

		protected virtual void EncodeInternal(ref SliceWriter writer, T? document, bool selfTerm)
		{
			using var buffer = document != null
				? m_converter?.ToSlice(document, ArrayPool<byte>.Shared, m_settings, m_resolver) ?? CrystalJson.ToSlice(document, ArrayPool<byte>.Shared, m_settings, m_resolver)
				: SliceOwner.Nil;

			if (selfTerm)
			{
				writer.EnsureBytes(buffer.Count + 5);
				writer.WriteVarInt32((uint) buffer.Count);
			}

			if (buffer.Count > 0)
			{
				writer.WriteBytes(buffer.Span);
			}
		}

		protected virtual T? DecodeInternal(ref SliceReader reader, bool selfTerm)
		{
			int count = selfTerm ? (int) reader.ReadVarInt32() : reader.Remaining;
			if (count < 0)
			{
				throw new FormatException("Negative size");
			}

			if (count == 0)
			{
				return default;
			}

			var encoded = reader.ReadBytes(count);
			return m_converter != null
				? m_converter.Unpack(encoded, m_resolver)
				: CrystalJson.Deserialize<T?>(encoded, default, m_settings, m_resolver);
		}

		#region Ordered...

		public override Slice EncodeOrdered(T? value)
		{
			return EncodeUnordered(value);
		}

		public override T? DecodeOrdered(Slice input)
		{
			return DecodeUnordered(input);
		}

		public override void EncodeOrderedTo(ref SliceWriter output, T? value)
		{
			EncodeInternal(ref output, value, selfTerm: true);
		}

		public override T? DecodeOrderedFrom(ref SliceReader input)
		{
			return DecodeInternal(ref input, selfTerm: true);
		}

		#endregion

		#region Unordered...

		public override Slice EncodeUnordered(T? value)
		{
			var writer = default(SliceWriter);
			EncodeInternal(ref writer, value, selfTerm: false);
			return writer.ToSlice();
		}

		public override T? DecodeUnordered(Slice encoded)
		{
			if (encoded.IsNullOrEmpty) return default!;
			var reader = new SliceReader(encoded);
			return DecodeInternal(ref reader, selfTerm: false);
		}

		public override void EncodeUnorderedSelfTerm(ref SliceWriter output, T? value)
		{
			EncodeInternal(ref output, value, selfTerm: true);
		}

		public override T? DecodeUnorderedSelfTerm(ref SliceReader input)
		{
			return DecodeInternal(ref input, selfTerm: true);
		}

		#endregion

		#region Value...

		public Slice EncodeValue(T? value)
		{
			return EncodeUnordered(value);
		}

		public T? DecodeValue(Slice encoded)
		{
			return DecodeUnordered(encoded);
		}

		#endregion

	}

}
