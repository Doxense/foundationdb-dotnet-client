#region BSD License
/* Copyright (c) 2013-2020, Doxense SAS
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

namespace Doxense.Collections.Tuples.Encoding
{
	using System;
	using System.Diagnostics.CodeAnalysis;
	using Doxense.Memory;
	using Doxense.Serialization.Encoders;

	/// <summary>Type codec that uses the Tuple Encoding format</summary>
	/// <typeparam name="T">Type of the values encoded by this codec</typeparam>
	public sealed class TupleCodec<T> : TypeCodec<T>, IValueEncoder<T>
	{

		private static TupleCodec<T>? s_defaultSerializer;

		public static TupleCodec<T> Default => s_defaultSerializer ??= new TupleCodec<T>(default!);

		private readonly T m_missingValue;

		public TupleCodec(T missingValue)
		{
			m_missingValue = missingValue;
		}

		public override Slice EncodeOrdered(T? value)
		{
			return TupleEncoder.EncodeKey(default, value);
		}

		public override void EncodeOrderedSelfTerm(ref SliceWriter output, T? value)
		{
			//HACKHACK: we lose the current depth!
			var writer = new TupleWriter(output);
			TuplePackers.SerializeTo(ref writer, value);
			output = writer.Output;
		}

		public override T? DecodeOrdered(Slice input)
		{
			return TuPack.DecodeKey<T>(input);
		}

		public override T? DecodeOrderedSelfTerm(ref SliceReader input)
		{
			//HACKHACK: we lose the current depth!
			var reader = new TupleReader(input);
			bool res = TuPack.DecodeNext<T>(ref reader, out T value);
			input = reader.Input;
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
