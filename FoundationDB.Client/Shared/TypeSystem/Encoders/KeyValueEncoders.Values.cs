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

	/// <summary>Helper class for all key/value encoders</summary>
	public static partial class KeyValueEncoders
	{

		/// <summary>Encoders that produce compact but unordered slices, suitable for values</summary>
		[PublicAPI]
		public static class Values
		{
			private static readonly GenericEncoder s_default = new GenericEncoder();

			[NotNull]
			public static IValueEncoder<Slice> BinaryEncoder => s_default;

			[NotNull]
			public static IValueEncoder<string> StringEncoder => s_default;

			[NotNull]
			public static IValueEncoder<int> Int32Encoder => s_default;

			[NotNull]
			public static IValueEncoder<long> Int64Encoder => s_default;

			[NotNull]
			public static IValueEncoder<Guid> GuidEncoder => s_default;

			/// <summary>Create a simple encoder from a codec</summary>
			[NotNull]
			public static IValueEncoder<T> Bind<T>([NotNull] IUnorderedTypeCodec<T> codec)
			{
				Contract.NotNull(codec, nameof(codec));

				if (codec is IValueEncoder<T> encoder) return encoder;

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

	}

}
