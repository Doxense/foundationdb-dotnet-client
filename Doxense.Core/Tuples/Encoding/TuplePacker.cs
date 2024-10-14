#region Copyright (c) 2023-2024 SnowBank SAS, (c) 2005-2023 Doxense SAS
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

namespace Doxense.Collections.Tuples.Encoding
{
	using Doxense.Memory;

	/// <summary>Helper class that can serialize values of type <typeparamref name="T"/> to the tuple binary format</summary>
	/// <typeparam name="T">Type of values to be serialized</typeparam>
	public static class TuplePacker<T>
	{

		internal static readonly TuplePackers.Encoder<T> Encoder = TuplePackers.GetSerializer<T>(required: true)!;

		internal static readonly TuplePackers.Decoder<T?> Decoder = TuplePackers.GetDeserializer<T>(required: true);

		/// <summary>Serialize a <typeparamref name="T"/> using a Tuple Writer</summary>
		/// <param name="writer">Target buffer</param>
		/// <param name="value">Value that will be serialized</param>
		/// <remarks>
		/// The buffer does not need to be pre-allocated.
		/// This method supports embedded tuples.
		/// </remarks>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void SerializeTo(ref TupleWriter writer, T? value)
		{
			Encoder(ref writer, value);
		}

		public static void SerializeBoxedTo(ref TupleWriter writer, object? value)
		{
			Encoder(ref writer, (T) value!);
		}

		/// <summary>Serialize a <typeparamref name="T"/> into a binary buffer</summary>
		/// <param name="writer">Target buffer</param>
		/// <param name="value">Value that will be serialized</param>
		/// <remarks>
		/// The buffer does not need to be pre-allocated.
		/// This method DOES NOT support embedded tuples, and assumes that we are serializing a top-level Tuple!
		/// If you need support for embedded tuples, use <see cref="SerializeTo(ref TupleWriter,T)"/> instead!
		/// </remarks>
		public static void SerializeTo(ref SliceWriter writer, T? value)
		{
			var tw = new TupleWriter(writer);
			Encoder(ref tw, value);
			writer = tw.Output;
			//REVIEW: we loose the depth information here! :(
		}

		/// <summary>Serialize a value of type <typeparamref name="T"/> into a tuple segment</summary>
		/// <param name="value">Value that will be serialized</param>
		/// <returns>Slice that contains the binary representation of <paramref name="value"/></returns>
		public static Slice Serialize(T? value)
		{
			var writer = new TupleWriter();
			Encoder(ref writer, value);
			return writer.Output.ToSlice();
		}

		/// <summary>Deserialize a tuple segment into a value of type <typeparamref name="T"/></summary>
		/// <param name="slice">Slice that contains the binary representation of a tuple item</param>
		/// <returns>Decoded value, or an exception if the item type is not compatible</returns>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static T? Deserialize(Slice slice) => Decoder(slice.Span);

		/// <summary>Deserialize a tuple segment into a value of type <typeparamref name="T"/></summary>
		/// <param name="slice">Slice that contains the binary representation of a tuple item</param>
		/// <returns>Decoded value, or an exception if the item type is not compatible</returns>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static T? Deserialize(ReadOnlySpan<byte> slice)
		{
			//<JIT_HACK>
			// - In Release builds, this will be cleaned up and inlined by the JIT as a direct invocation of the correct WriteXYZ method
			// - In Debug builds, we have to disabled this, because it would be too slow
#if !DEBUG
			// non-nullable
			if (typeof(T) == typeof(int)) return (T?) (object) TuplePackers.DeserializeInt32(slice);
			if (typeof(T) == typeof(long)) return (T?) (object) TuplePackers.DeserializeInt64(slice);
			if (typeof(T) == typeof(bool)) return (T?) (object) TuplePackers.DeserializeBoolean(slice);
			if (typeof(T) == typeof(float)) return (T?) (object) TuplePackers.DeserializeSingle(slice);
			if (typeof(T) == typeof(double)) return (T?) (object) TuplePackers.DeserializeDouble(slice);
			if (typeof(T) == typeof(VersionStamp)) return (T?) (object) TuplePackers.DeserializeVersionStamp(slice);
			if (typeof(T) == typeof(Guid)) return (T?) (object) TuplePackers.DeserializeGuid(slice);
			if (typeof(T) == typeof(Uuid128)) return (T?) (object) TuplePackers.DeserializeUuid128(slice);
			if (typeof(T) == typeof(Uuid96)) return (T?) (object) TuplePackers.DeserializeUuid96(slice);
			if (typeof(T) == typeof(Uuid80)) return (T?) (object) TuplePackers.DeserializeUuid80(slice);
			if (typeof(T) == typeof(Uuid64)) return (T?) (object) TuplePackers.DeserializeUuid64(slice);
			if (typeof(T) == typeof(TimeSpan)) return (T?) (object) TuplePackers.DeserializeTimeSpan(slice);
			if (typeof(T) == typeof(Slice)) return (T?) (object) TuplePackers.DeserializeSlice(slice);
			// nullable
			if (typeof(T) == typeof(int?)) return (T?) (object?) TuplePackers.DeserializeInt32Nullable(slice);
			if (typeof(T) == typeof(long?)) return (T?) (object?) TuplePackers.DeserializeInt64Nullable(slice);
			if (typeof(T) == typeof(bool?)) return (T?) (object?) TuplePackers.DeserializeBooleanNullable(slice);
			if (typeof(T) == typeof(float?)) return (T?) (object?) TuplePackers.DeserializeSingleNullable(slice);
			if (typeof(T) == typeof(double?)) return (T?) (object?) TuplePackers.DeserializeDoubleNullable(slice);
			if (typeof(T) == typeof(VersionStamp?)) return (T?) (object?) TuplePackers.DeserializeVersionStampNullable(slice);
			if (typeof(T) == typeof(Guid?)) return (T?) (object?) TuplePackers.DeserializeGuidNullable(slice);
			if (typeof(T) == typeof(Uuid128?)) return (T?) (object?) TuplePackers.DeserializeUuid128Nullable(slice);
			if (typeof(T) == typeof(Uuid96?)) return (T?) (object?) TuplePackers.DeserializeUuid96Nullable(slice);
			if (typeof(T) == typeof(Uuid80?)) return (T?) (object?) TuplePackers.DeserializeUuid80Nullable(slice);
			if (typeof(T) == typeof(TimeSpan?)) return (T?) (object?) TuplePackers.DeserializeTimeSpanNullable(slice);
			if (typeof(T) == typeof(Uuid64?)) return (T?) (object?) TuplePackers.DeserializeUuid64Nullable(slice);
#endif
			//</JIT_HACK>

			return Decoder(slice);
		}

	}

}
