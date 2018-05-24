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
	using System;
	using Doxense.Diagnostics.Contracts;
	using JetBrains.Annotations;

	public sealed class BinaryEncoding : IValueEncoding, IValueEncoder<Slice>, IValueEncoder<string>, IValueEncoder<int>, IValueEncoder<long>, IValueEncoder<Guid>
	{

		[NotNull]
		public static readonly BinaryEncoding Instance = new BinaryEncoding();

		/// <summary>Identity encoder</summary>
		[NotNull]
		public static IValueEncoder<Slice> SliceEncoder => BinaryEncoding.Instance;

		/// <summary>Encodes Unicode string as UTF-8 bytes</summary>
		[NotNull]
		public static IValueEncoder<string> StringEncoder => BinaryEncoding.Instance;

		/// <summary>Encodes 32-bits signed integers as 4 bytes (high-endian)</summary>
		[NotNull]
		public static IValueEncoder<int> Int32Encoder => BinaryEncoding.Instance;

		/// <summary>Encodes 64-bits signed integers as 4 bytes (high-endian)</summary>
		[NotNull]
		public static IValueEncoder<long> Int64Encoder => BinaryEncoding.Instance;

		/// <summary>Encodes 128-bits GUIDs as 16 bytes</summary>
		[NotNull]
		public static IValueEncoder<Guid> GuidEncoder => BinaryEncoding.Instance;

		public IValueEncoder<TValue, TStorage> GetValueEncoder<TValue, TStorage>()
		{
			if (typeof(TStorage) != typeof(Slice)) throw new NotSupportedException("BinaryEncoding can only use Slice as the storage type.");
			return (IValueEncoder<TValue, TStorage>) (object) GetValueEncoder<TValue>();
		}

		public IValueEncoder<TValue> GetValueEncoder<TValue>()
		{
			if (typeof(TValue) == typeof(Slice)) return (IValueEncoder<TValue>) (object) this;
			if (typeof(TValue) == typeof(string)) return (IValueEncoder<TValue>) (object) this;
			if (typeof(TValue) == typeof(int)) return (IValueEncoder<TValue>) (object) this;
			if (typeof(TValue) == typeof(long)) return (IValueEncoder<TValue>) (object) this;
			if (typeof(TValue) == typeof(Guid)) return (IValueEncoder<TValue>) (object) this;
			throw new NotSupportedException($"BinaryEncoding does not know how to encode values of type {typeof(TValue).Name}.");
		}

		public Slice EncodeValue(Slice value)
		{
			return value;
		}

		Slice IValueEncoder<Slice, Slice>.DecodeValue(Slice encoded)
		{
			return encoded;
		}

		public Slice EncodeValue(string value)
		{
			return Slice.FromString(value);
		}

		string IValueEncoder<string, Slice>.DecodeValue(Slice encoded)
		{
			return encoded.ToUnicode();
		}

		public Slice EncodeValue(int value)
		{
			return Slice.FromInt32(value);
		}

		int IValueEncoder<int, Slice>.DecodeValue(Slice encoded)
		{
			return encoded.ToInt32();
		}

		public Slice EncodeValue(long value)
		{
			return Slice.FromInt64(value);
		}

		long IValueEncoder<long, Slice>.DecodeValue(Slice encoded)
		{
			return encoded.ToInt64();
		}

		public Slice EncodeValue(Guid value)
		{
			return Slice.FromGuid(value);
		}

		Guid IValueEncoder<Guid, Slice>.DecodeValue(Slice encoded)
		{
			return encoded.ToGuid();
		}

	}


}
