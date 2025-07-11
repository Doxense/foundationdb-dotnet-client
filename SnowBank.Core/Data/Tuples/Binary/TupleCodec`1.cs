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

namespace SnowBank.Data.Tuples.Binary
{
	using SnowBank.Data.Binary;
	using SnowBank.Buffers;

	/// <summary>Type codec that uses the Tuple Encoding format</summary>
	/// <typeparam name="T">Type of the values encoded by this codec</typeparam>
	public sealed class TupleCodec<T> : TypeCodec<T>, IValueEncoder<T>
	{

		public static TupleCodec<T> Default => new(default);

		private readonly T? m_missingValue;

		public TupleCodec(T? missingValue)
		{
			m_missingValue = missingValue;
		}

		public override Slice EncodeOrdered(T? value)
		{
			return TupleEncoder.EncodeKey(default, value);
		}

		public override void EncodeOrderedTo(ref SliceWriter output, T? value)
		{
			//HACKHACK: we lose the current depth!
			var writer = new TupleWriter(ref output);
			TuplePackers.SerializeTo(writer, value);
		}

		public override T? DecodeOrdered(Slice input)
		{
			return TuPack.DecodeKey<T?>(input);
		}

		public override T? DecodeOrderedFrom(ref SliceReader input)
		{
			// decode the next value from the tail
			var reader = new TupleReader(input.Tail.Span);
			bool res = TupleEncoder.TryDecodeNext<T?>(ref reader, out var value, out var error);
			if (error != null) throw error;

			// advance the original reader by the same amount that was consumed
			input.Skip(reader.Cursor);

			return res ? value : m_missingValue;
		}

		public Slice EncodeValue(T? value)
		{
			return EncodeUnordered(value);
		}

		public T? DecodeValue(Slice encoded)
		{
			return DecodeUnordered(encoded);
		}

	}

}
