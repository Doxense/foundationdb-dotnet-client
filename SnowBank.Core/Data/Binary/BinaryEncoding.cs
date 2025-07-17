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

namespace SnowBank.Data.Binary
{

	/// <summary>Encoder that uses a compact binary representation for values</summary>
	/// <remarks>This type mostly defers to the corresponding methods in <see cref="Slice"/></remarks>
	[PublicAPI]
	[Obsolete("Use IFdbKeyCodec<T> instead")]
	public sealed class BinaryEncoding : IValueEncoding, IKeyEncoding,
		IValueEncoder<Slice>,
		IValueEncoder<string?>,
		IValueEncoder<int>,
		IValueEncoder<uint>,
		IValueEncoder<long>,
		IValueEncoder<ulong>,
		IValueEncoder<Guid>,
		IValueEncoder<Uuid128>,
		IValueEncoder<Uuid64>,
		IValueEncoder<VersionStamp>
	{

		/// <summary>Default instance</summary>
		public static readonly BinaryEncoding Instance = new BinaryEncoding();

		/// <summary>Identity encoder</summary>
		public static IValueEncoder<Slice> SliceEncoder => BinaryEncoding.Instance;

		/// <summary>Encodes Unicode string as UTF-8 bytes</summary>
		public static IValueEncoder<string?> StringEncoder => BinaryEncoding.Instance;

		/// <summary>Encodes 32-bits signed integers as 4 bytes (high-endian)</summary>
		public static IValueEncoder<int> Int32Encoder => BinaryEncoding.Instance;

		/// <summary>Encodes 32-bits unsigned integers as 4 bytes (high-endian)</summary>
		public static IValueEncoder<uint> UInt32Encoder => BinaryEncoding.Instance;

		/// <summary>Encodes 64-bits signed integers as 4 bytes (high-endian)</summary>
		public static IValueEncoder<long> Int64Encoder => BinaryEncoding.Instance;

		/// <summary>Encodes 64-bits unsigned integers as 4 bytes (high-endian)</summary>
		public static IValueEncoder<ulong> UInt64Encoder => BinaryEncoding.Instance;

		/// <summary>Encodes 128-bits GUIDs as 16 bytes</summary>
		public static IValueEncoder<Guid> GuidEncoder => BinaryEncoding.Instance;

		/// <summary>Encodes 128-bits UUIDs as 16 bytes</summary>
		public static IValueEncoder<Uuid128> Uuid128Encoder => BinaryEncoding.Instance;

		/// <summary>Encodes 64-bits UUIDs as 16 bytes</summary>
		public static IValueEncoder<Uuid64> Uuid64Encoder => BinaryEncoding.Instance;

		/// <summary>Encodes 80-bit or 85-bits VersionStamp as 16 bytes</summary>
		public static IValueEncoder<VersionStamp> VersionStampEncoder => BinaryEncoding.Instance;

		/// <summary>Returns the encoder for the specified type</summary>
		/// <typeparam name="TValue">Type of value to encode</typeparam>
		/// <typeparam name="TStorage">Intermediate type</typeparam>
		/// <returns>Encoder for this type</returns>
		/// <exception cref="NotSupportedException"> if this type is not supported</exception>
		public IValueEncoder<TValue, TStorage> GetValueEncoder<TValue, TStorage>() where TValue : notnull
		{
			if (typeof(TStorage) != typeof(Slice))
			{
				throw new NotSupportedException("BinaryEncoding can only use Slice as the storage type.");
			}
			return (IValueEncoder<TValue, TStorage>) GetValueEncoder<TValue>();
		}

		/// <summary>Returns the encoder for the specified type</summary>
		/// <typeparam name="TValue">Type of value to encode</typeparam>
		/// <returns>Encoder for this type</returns>
		/// <exception cref="NotSupportedException"> if this type is not supported</exception>
		public IValueEncoder<TValue> GetValueEncoder<TValue>() where TValue : notnull
		{
			if (typeof(TValue) == typeof(Slice)) return (IValueEncoder<TValue>) (object) this;
			if (typeof(TValue) == typeof(string)) return (IValueEncoder<TValue>) (object) this;
			if (typeof(TValue) == typeof(int)) return (IValueEncoder<TValue>) (object) this;
			if (typeof(TValue) == typeof(uint)) return (IValueEncoder<TValue>) (object) this;
			if (typeof(TValue) == typeof(long)) return (IValueEncoder<TValue>) (object) this;
			if (typeof(TValue) == typeof(ulong)) return (IValueEncoder<TValue>) (object) this;
			if (typeof(TValue) == typeof(Guid)) return (IValueEncoder<TValue>) (object) this;
			if (typeof(TValue) == typeof(Uuid128)) return (IValueEncoder<TValue>) (object) this;
			if (typeof(TValue) == typeof(Uuid64)) return (IValueEncoder<TValue>) (object) this;
			if (typeof(TValue) == typeof(VersionStamp)) return (IValueEncoder<TValue>) (object) this;
			throw new NotSupportedException($"BinaryEncoding does not know how to encode values of type {typeof(TValue).Name}.");
		}

		/// <inheritdoc />
		public Slice EncodeValue(Slice value) => value;

		/// <inheritdoc />
		Slice IValueEncoder<Slice, Slice>.DecodeValue(Slice encoded) => encoded;

		/// <inheritdoc />
		public Slice EncodeValue(string? value) => Slice.FromString(value);

		/// <inheritdoc />
		string? IValueEncoder<string?, Slice>.DecodeValue(Slice encoded) => encoded.ToUnicode();

		/// <inheritdoc />
		public Slice EncodeValue(int value) => Slice.FromInt32(value);

		int IValueEncoder<int, Slice>.DecodeValue(Slice encoded) => encoded.ToInt32();

		/// <inheritdoc />
		public Slice EncodeValue(uint value) => Slice.FromUInt32(value);

		uint IValueEncoder<uint, Slice>.DecodeValue(Slice encoded) => encoded.ToUInt32();

		/// <inheritdoc />
		public Slice EncodeValue(long value) => Slice.FromInt64(value);

		/// <inheritdoc />
		long IValueEncoder<long, Slice>.DecodeValue(Slice encoded) => encoded.ToInt64();

		/// <inheritdoc />
		public Slice EncodeValue(ulong value) => Slice.FromUInt64(value);

		/// <inheritdoc />
		ulong IValueEncoder<ulong, Slice>.DecodeValue(Slice encoded) => encoded.ToUInt64();

		/// <inheritdoc />
		public Slice EncodeValue(Guid value) => Slice.FromGuid(value);

		/// <inheritdoc />
		Guid IValueEncoder<Guid, Slice>.DecodeValue(Slice encoded) => encoded.ToGuid();

		/// <inheritdoc />
		public Slice EncodeValue(Uuid128 value) => Slice.FromUuid128(value);

		/// <inheritdoc />
		Uuid128 IValueEncoder<Uuid128, Slice>.DecodeValue(Slice encoded) => encoded.ToUuid128();

		/// <inheritdoc />
		public Slice EncodeValue(Uuid64 value) => Slice.FromUuid64(value);
		
		/// <inheritdoc />
		Uuid64 IValueEncoder<Uuid64, Slice>.DecodeValue(Slice encoded) => encoded.ToUuid64();

		/// <inheritdoc />
		public Slice EncodeValue(VersionStamp value) => value.ToSlice();
		
		/// <inheritdoc />
		VersionStamp IValueEncoder<VersionStamp, Slice>.DecodeValue(Slice encoded) => VersionStamp.ReadFrom(encoded);

		/// <inheritdoc />
		IDynamicKeyEncoder IKeyEncoding.GetDynamicKeyEncoder() => throw new NotSupportedException();

		/// <inheritdoc />
		IKeyEncoder<T1> IKeyEncoding.GetKeyEncoder<T1>() => throw new NotSupportedException();

		/// <inheritdoc />
		ICompositeKeyEncoder<T1, T2> IKeyEncoding.GetKeyEncoder<T1, T2>() => throw new NotSupportedException();

		/// <inheritdoc />
		ICompositeKeyEncoder<T1, T2, T3> IKeyEncoding.GetKeyEncoder<T1, T2, T3>() => throw new NotSupportedException();

		/// <inheritdoc />
		ICompositeKeyEncoder<T1, T2, T3, T4> IKeyEncoding.GetKeyEncoder<T1, T2, T3, T4>() => throw new NotSupportedException();

	}

}
