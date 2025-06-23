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

	/// <summary>Serializer for keys composed of a single element</summary>
	/// <typeparam name="T1">Type of the key</typeparam>
	public sealed class TupleSerializer<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T1> : ITupleSerializer<STuple<T1>>
	{

		/// <summary>Gets the default instance of the <see cref="TupleSerializer{T1}"/> class.</summary>
		public static TupleSerializer<T1> Default { get; } = new();

		/// <summary>Packs the specified tuple into the writer.</summary>
		/// <param name="writer">The writer to pack the tuple into.</param>
		/// <param name="tuple">The tuple to pack.</param>
		public void PackTo(ref TupleWriter writer, in STuple<T1> tuple)
		{
			TuplePackers.SerializeTo(ref writer, tuple.Item1);
		}

		/// <summary>Unpacks a tuple from the specified reader.</summary>
		/// <param name="reader">The reader to unpack the tuple from.</param>
		/// <param name="tuple">The unpacked tuple.</param>
		public void UnpackFrom(ref TupleReader reader, out STuple<T1> tuple)
		{
			TupleEncoder.DecodeKey(ref reader, out tuple);
		}

	}

	/// <summary>Serializer for keys composed of 2 elements</summary>
	public sealed class TupleSerializer<
		[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T1,
		[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T2>
		: ITupleSerializer<STuple<T1, T2>>
	{

		/// <summary>Gets the default instance of the <see cref="TupleSerializer{T1, T2}"/> class.</summary>
		public static TupleSerializer<T1, T2> Default { get; } = new();

		/// <summary>Packs the specified tuple into the writer.</summary>
		/// <param name="writer">The writer to pack the tuple into.</param>
		/// <param name="tuple">The tuple to pack.</param>
		public void PackTo(ref TupleWriter writer, in STuple<T1, T2> tuple)
		{
			TuplePackers.SerializeTo(ref writer, tuple.Item1);
			TuplePackers.SerializeTo(ref writer, tuple.Item2);
		}

		/// <summary>Unpacks a tuple from the specified reader.</summary>
		/// <param name="reader">The reader to unpack the tuple from.</param>
		/// <param name="tuple">The unpacked tuple.</param>
		public void UnpackFrom(ref TupleReader reader, out STuple<T1, T2> tuple)
		{
			TupleEncoder.DecodeKey(ref reader, out tuple);
		}

	}

	/// <summary>Serializer for keys composed of 3 elements</summary>
	public sealed class TupleSerializer<
		[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T1,
		[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T2,
		[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T3>
		: ITupleSerializer<STuple<T1, T2, T3>>
	{
		/// <summary>Gets the default instance of the <see cref="TupleSerializer{T1, T2, T3}"/> class.</summary>
		public static TupleSerializer<T1, T2, T3> Default { get; } = new();

		/// <summary>Packs the specified tuple into the writer.</summary>
		/// <param name="writer">The writer to pack the tuple into.</param>
		/// <param name="tuple">The tuple to pack.</param>
		public void PackTo(ref TupleWriter writer, in STuple<T1, T2, T3> tuple)
		{
			TuplePackers.SerializeTo(ref writer, tuple.Item1);
			TuplePackers.SerializeTo(ref writer, tuple.Item2);
			TuplePackers.SerializeTo(ref writer, tuple.Item3);
		}

		/// <summary>Unpacks a tuple from the specified reader.</summary>
		/// <param name="reader">The reader to unpack the tuple from.</param>
		/// <param name="tuple">The unpacked tuple.</param>
		public void UnpackFrom(ref TupleReader reader, out STuple<T1, T2, T3> tuple)
		{
			TupleEncoder.DecodeKey(ref reader, out tuple);
		}

	}

	/// <summary>Serializer for keys composed of 4 elements</summary>
	public sealed class TupleSerializer<
		[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T1,
		[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T2,
		[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T3,
		[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T4>
		: ITupleSerializer<STuple<T1, T2, T3, T4>>
	{
		/// <summary>Gets the default instance of the <see cref="TupleSerializer{T1, T2, T3, T4}"/> class.</summary>
		public static TupleSerializer<T1, T2, T3, T4> Default { get; } = new();

		/// <summary>Packs the specified tuple into the writer.</summary>
		/// <param name="writer">The writer to pack the tuple into.</param>
		/// <param name="tuple">The tuple to pack.</param>
		public void PackTo(ref TupleWriter writer, in STuple<T1, T2, T3, T4> tuple)
		{
			TuplePackers.SerializeTo(ref writer, tuple.Item1);
			TuplePackers.SerializeTo(ref writer, tuple.Item2);
			TuplePackers.SerializeTo(ref writer, tuple.Item3);
			TuplePackers.SerializeTo(ref writer, tuple.Item4);
		}

		/// <summary>Unpacks a tuple from the specified reader.</summary>
		/// <param name="reader">The reader to unpack the tuple from.</param>
		/// <param name="tuple">The unpacked tuple.</param>
		public void UnpackFrom(ref TupleReader reader, out STuple<T1, T2, T3, T4> tuple)
		{
			TupleEncoder.DecodeKey(ref reader, out tuple);
		}

	}

	/// <summary>Serializer for keys composed of 5 elements</summary>
	public sealed class TupleSerializer<
		[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T1,
		[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T2,
		[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T3,
		[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T4,
		[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T5>
		: ITupleSerializer<STuple<T1, T2, T3, T4, T5>>
	{

		/// <summary>Gets the default instance of the <see cref="TupleSerializer{T1, T2, T3, T4, T5}"/> class.</summary>
		public static TupleSerializer<T1, T2, T3, T4, T5> Default { get; } = new();

		/// <summary>Packs the specified tuple into the writer.</summary>
		/// <param name="writer">The writer to pack the tuple into.</param>
		/// <param name="tuple">The tuple to pack.</param>
		public void PackTo(ref TupleWriter writer, in STuple<T1, T2, T3, T4, T5> tuple)
		{
			TuplePackers.SerializeTo(ref writer, tuple.Item1);
			TuplePackers.SerializeTo(ref writer, tuple.Item2);
			TuplePackers.SerializeTo(ref writer, tuple.Item3);
			TuplePackers.SerializeTo(ref writer, tuple.Item4);
			TuplePackers.SerializeTo(ref writer, tuple.Item5);
		}

		/// <summary>Unpacks a tuple from the specified reader.</summary>
		/// <param name="reader">The reader to unpack the tuple from.</param>
		/// <param name="tuple">The unpacked tuple.</param>
		public void UnpackFrom(ref TupleReader reader, out STuple<T1, T2, T3, T4, T5> tuple)
		{
			TupleEncoder.DecodeKey(ref reader, out tuple);
		}

	}

	/// <summary>Serializer for keys composed of 6 elements</summary>
	public sealed class TupleSerializer<
		[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T1,
		[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T2,
		[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T3,
		[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T4,
		[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T5,
		[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T6>
		: ITupleSerializer<STuple<T1, T2, T3, T4, T5, T6>>
	{

		/// <summary>Gets the default instance of the <see cref="TupleSerializer{T1, T2, T3, T4, T5, T6}"/> class.</summary>
		public static TupleSerializer<T1, T2, T3, T4, T5, T6> Default { get; } = new();

		/// <summary>Packs the specified tuple into the writer.</summary>
		/// <param name="writer">The writer to pack the tuple into.</param>
		/// <param name="tuple">The tuple to pack.</param>
		public void PackTo(ref TupleWriter writer, in STuple<T1, T2, T3, T4, T5, T6> tuple)
		{
			TuplePackers.SerializeTo(ref writer, tuple.Item1);
			TuplePackers.SerializeTo(ref writer, tuple.Item2);
			TuplePackers.SerializeTo(ref writer, tuple.Item3);
			TuplePackers.SerializeTo(ref writer, tuple.Item4);
			TuplePackers.SerializeTo(ref writer, tuple.Item5);
			TuplePackers.SerializeTo(ref writer, tuple.Item6);
		}

		/// <summary>Unpacks a tuple from the specified reader.</summary>
		/// <param name="reader">The reader to unpack the tuple from.</param>
		/// <param name="tuple">The unpacked tuple.</param>
		public void UnpackFrom(ref TupleReader reader, out STuple<T1, T2, T3, T4, T5, T6> tuple)
		{
			TupleEncoder.DecodeKey(ref reader, out tuple);
		}

	}

	/// <summary>Serializer for keys composed of 7 elements</summary>
	public sealed class TupleSerializer<
		[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T1,
		[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T2,
		[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T3,
		[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T4,
		[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T5,
		[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T6,
		[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T7>
		: ITupleSerializer<STuple<T1, T2, T3, T4, T5, T6, T7>>
	{

		/// <summary>Gets the default instance of the <see cref="TupleSerializer{T1, T2, T3, T4, T5, T6, T7}"/> class.</summary>
		public static TupleSerializer<T1, T2, T3, T4, T5, T6, T7> Default { get; } = new();

		/// <summary>Packs the specified tuple into the writer.</summary>
		/// <param name="writer">The writer to pack the tuple into.</param>
		/// <param name="tuple">The tuple to pack.</param>
		public void PackTo(ref TupleWriter writer, in STuple<T1, T2, T3, T4, T5, T6, T7> tuple)
		{
			TuplePackers.SerializeTo(ref writer, tuple.Item1);
			TuplePackers.SerializeTo(ref writer, tuple.Item2);
			TuplePackers.SerializeTo(ref writer, tuple.Item3);
			TuplePackers.SerializeTo(ref writer, tuple.Item4);
			TuplePackers.SerializeTo(ref writer, tuple.Item5);
			TuplePackers.SerializeTo(ref writer, tuple.Item6);
			TuplePackers.SerializeTo(ref writer, tuple.Item7);
		}

		/// <summary>Unpacks a tuple from the specified reader.</summary>
		/// <param name="reader">The reader to unpack the tuple from.</param>
		/// <param name="tuple">The unpacked tuple.</param>
		public void UnpackFrom(ref TupleReader reader, out STuple<T1, T2, T3, T4, T5, T6, T7> tuple)
		{
			TupleEncoder.DecodeKey(ref reader, out tuple);
		}

	}

	/// <summary>Serializer for keys composed of 8 elements</summary>
	public sealed class TupleSerializer<
		[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T1,
		[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T2,
		[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T3,
		[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T4,
		[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T5,
		[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T6,
		[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T7,
		[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T8>
		: ITupleSerializer<STuple<T1, T2, T3, T4, T5, T6, T7, T8>>
	{

		/// <summary>Gets the default instance of the <see cref="TupleSerializer{T1, T2, T3, T4, T5, T6, T7, T8}"/> class.</summary>
		public static TupleSerializer<T1, T2, T3, T4, T5, T6, T7, T8> Default { get; } = new();

		/// <summary>Packs the specified tuple into the writer.</summary>
		/// <param name="writer">The writer to pack the tuple into.</param>
		/// <param name="tuple">The tuple to pack.</param>
		public void PackTo(ref TupleWriter writer, in STuple<T1, T2, T3, T4, T5, T6, T7, T8> tuple)
		{
			TuplePackers.SerializeTo(ref writer, tuple.Item1);
			TuplePackers.SerializeTo(ref writer, tuple.Item2);
			TuplePackers.SerializeTo(ref writer, tuple.Item3);
			TuplePackers.SerializeTo(ref writer, tuple.Item4);
			TuplePackers.SerializeTo(ref writer, tuple.Item5);
			TuplePackers.SerializeTo(ref writer, tuple.Item6);
			TuplePackers.SerializeTo(ref writer, tuple.Item7);
			TuplePackers.SerializeTo(ref writer, tuple.Item8);
		}

		/// <summary>Unpacks a tuple from the specified reader.</summary>
		/// <param name="reader">The reader to unpack the tuple from.</param>
		/// <param name="tuple">The unpacked tuple.</param>
		public void UnpackFrom(ref TupleReader reader, out STuple<T1, T2, T3, T4, T5, T6, T7, T8> tuple)
		{
			TupleEncoder.DecodeKey(ref reader, out tuple);
		}

	}

}
