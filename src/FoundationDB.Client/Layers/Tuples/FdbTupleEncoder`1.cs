﻿#region BSD Licence
/* Copyright (c) 2013-2014, Doxense SAS
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

namespace FoundationDB.Layers.Tuples
{
	using FoundationDB.Client;
	using FoundationDB.Client.Utils;
	using System;

	public sealed class FdbTupleCodec<T> : FdbTypeCodec<T>, IValueEncoder<T>
	{

		private static volatile FdbTupleCodec<T> s_defaultSerializer;

		public static FdbTupleCodec<T> Default
		{
			get
			{
				if (s_defaultSerializer == null)
				{
					s_defaultSerializer = new FdbTupleCodec<T>(default(T));
				}
				return s_defaultSerializer;
			}
		}

		private readonly T m_missingValue;

		public FdbTupleCodec(T missingValue)
		{
			m_missingValue = missingValue;
		}

		public override Slice EncodeOrdered(T value)
		{
			return FdbTuple.Pack<T>(value);
		}

		public override void EncodeOrderedSelfTerm(ref SliceWriter output, T value)
		{
			FdbTuplePacker<T>.Encoder(ref output, value);
		}

		public override T DecodeOrdered(Slice input)
		{
			return FdbTuple.UnpackSingle<T>(input);
		}

		public override T DecodeOrderedSelfTerm(ref SliceReader input)
		{
			T value;
			if (!FdbTuple.UnpackNext<T>(ref input, out value))
			{
				return m_missingValue;
			}
			return value;
		}

		public Slice EncodeValue(T value)
		{
			return EncodeUnordered(value);
		}

		public T DecodeValue(Slice encoded)
		{
			return DecodeUnordered(encoded);
		}
	}

}
