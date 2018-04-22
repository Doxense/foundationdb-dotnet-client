
namespace Doxense.Serialization.Encoders
{
	using JetBrains.Annotations;
	using System;
	using Doxense.Diagnostics.Contracts;

	/// <summary>Helper class for all key/value encoders</summary>
	public static partial class KeyValueEncoders
	{

		/// <summary>Encoders that produce compact but unordered slices, suitable for keys that don't benefit from having lexicographical ordering</summary>
		public static class Unordered
		{

			/// <summary>Create a simple encoder from a codec</summary>
			[NotNull]
			public static IKeyEncoder<T> Bind<T>([NotNull] IUnorderedTypeCodec<T> codec)
			{
				Contract.NotNull(codec, nameof(codec));

				// ReSharper disable once SuspiciousTypeConversion.Global
				if (codec is IKeyEncoder<T> encoder) return encoder;

				return new Singleton<T>(
					(value) => codec.EncodeUnordered(value),
					(encoded) => codec.DecodeUnordered(encoded)
				);
			}

		}

	}

}
