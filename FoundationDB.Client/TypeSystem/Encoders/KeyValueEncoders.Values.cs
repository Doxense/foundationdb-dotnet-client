
namespace Doxense.Serialization.Encoders
{
	using JetBrains.Annotations;
	using System;
	using Doxense.Diagnostics.Contracts;
	using Doxense.Memory;

	/// <summary>Helper class for all key/value encoders</summary>
	public static partial class KeyValueEncoders
	{

		/// <summary>Encoders that produce compact but unordered slices, suitable for values</summary>
		public static class Values
		{
			private static readonly GenericEncoder s_default = new GenericEncoder();

			public static IValueEncoder<Slice> BinaryEncoder
			{
				[NotNull]
				get { return s_default; }
			}

			public static IValueEncoder<string> StringEncoder
			{
				[NotNull]
				get { return s_default; }
			}

			public static IValueEncoder<int> Int32Encoder
			{
				[NotNull]
				get { return s_default; }
			}

			public static IValueEncoder<long> Int64Encoder
			{
				[NotNull]
				get { return s_default; }
			}

			public static IValueEncoder<Guid> GuidEncoder
			{
				[NotNull]
				get { return s_default; }
			}

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
