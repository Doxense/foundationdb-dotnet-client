#region Copyright (c) 2023-2024 SnowBank SAS
//
// All rights are reserved. Reproduction or transmission in whole or in part, in
// any form or by any means, electronic, mechanical or otherwise, is prohibited
// without the prior written consent of the copyright owner.
//
#endregion

namespace Doxense.Serialization.Json.Encoders
{
	using System.Buffers;
	using Doxense.Memory;
	using Doxense.Serialization.Encoders;

	/// <summary>Codecs that encodes CLR types into either database keys (ordered) or values (unordered)</summary>
	public static class CrystalJsonCodec
	{

		internal static readonly CrystalJsonSettings DefaultJsonSettings = CrystalJsonSettings.JsonCompact.WithEnumAsStrings().WithIso8601Dates();

		public static CrystalJsonCodec<T> GetEncoder<T>(CrystalJsonSettings? settings = null, ICrystalJsonTypeResolver? resolver = null) => new(settings, resolver);

		public static CrystalJsonCodec<T> GetEncoder<T>(IJsonConverter<T> converter, CrystalJsonSettings? settings = null, ICrystalJsonTypeResolver? resolver = null) => new(settings, resolver);

	}

	/// <summary>Codec that encodes <typeparamref name="T"/> instances into either database keys (ordered) or values (unordered)</summary>
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
				? m_converter.JsonDeserialize(encoded, m_resolver)
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
