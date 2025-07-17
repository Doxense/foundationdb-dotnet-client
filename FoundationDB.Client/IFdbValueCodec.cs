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

namespace FoundationDB.Client
{
	using System.Buffers;

	using SnowBank.Runtime;

	public interface IFdbValueCodec<TValue, out TEncoded>
		where TEncoded : struct, ISpanEncodable
	{

		TEncoded EncodeValue(in TValue value);

		TValue DecodeValue(ReadOnlySpan<byte> encoded);

	}

	public sealed class FdbVarTupleValueCodec : IFdbValueCodec<IVarTuple, FdbVarTupleValue>
	{

		public static readonly FdbVarTupleValueCodec Instance = new();

		private FdbVarTupleValueCodec() { }

		/// <inheritdoc />
		public FdbVarTupleValue EncodeValue(in IVarTuple value) => new(value);

		/// <inheritdoc />
		public IVarTuple DecodeValue(ReadOnlySpan<byte> encoded) => TuPack.Unpack(encoded).ToTuple();

	}

	public sealed class FdbValueSpanEncoderCodec<TValue, TCodec> : IFdbValueCodec<TValue, FdbValue<TValue, TCodec>>
		where TCodec : struct, ISpanEncoder<TValue>, ISpanDecoder<TValue>
	{

		public static readonly FdbValueSpanEncoderCodec<TValue, TCodec> Instance = new();

		private FdbValueSpanEncoderCodec() { }

		/// <inheritdoc />
		public FdbValue<TValue, TCodec> EncodeValue(in TValue value) => new(value);

		/// <inheritdoc />
		public TValue DecodeValue(ReadOnlySpan<byte> encoded)
			=> TCodec.TryDecode(encoded, out var value)
				? value!
				: throw new FormatException($"Failed to decode value of type '{typeof(TValue).GetFriendlyName()}'");
	}

	public sealed class FdbValueCodec<TValue, TEncoded> : IFdbValueCodec<TValue, TEncoded>
		where TEncoded : struct, ISpanEncodable
	{

		public Func<TValue, TEncoded> Encode { get; }

#if NET9_0_OR_GREATER

		public Func<ReadOnlySpan<byte>, TValue> Decode { get; }

		public FdbValueCodec(Func<TValue, TEncoded> encode, Func<ReadOnlySpan<byte>, TValue> decode)
		{
			this.Encode = encode;
			this.Decode = decode;
		}

#else

		public SpanDecoder Decode { get; }

		public delegate TValue SpanDecoder(ReadOnlySpan<byte> encoded);

		public FdbValueCodec(Func<TValue, TEncoded> encode, SpanDecoder decode)
		{
			this.Encode = encode;
			this.Decode = decode;
		}

#endif

		/// <inheritdoc />
		public TEncoded EncodeValue(in TValue value) => this.Encode(value);

		/// <inheritdoc />
		public TValue DecodeValue(ReadOnlySpan<byte> encoded) => this.Decode(encoded);

	}

	public sealed class FdbValueEncoderCodec<TValue> : IFdbValueCodec<TValue, FdbRawValue>
	{

		public FdbValueEncoderCodec(IValueEncoder<TValue> encoder)
		{
			this.Encoder = encoder;
		}

		public IValueEncoder<TValue> Encoder { get; }

		/// <inheritdoc />
		public FdbRawValue EncodeValue(in TValue value)
		{
			return new(this.Encoder.EncodeValue(value));
		}

		/// <inheritdoc />
		public TValue DecodeValue(ReadOnlySpan<byte> encoded)
		{
			using var owner = Slice.FromBytes(encoded, ArrayPool<byte>.Shared);
			return this.Encoder.DecodeValue(owner.Data)!;
		}

	}

}
