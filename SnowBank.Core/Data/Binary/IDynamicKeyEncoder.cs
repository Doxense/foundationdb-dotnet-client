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
	using SnowBank.Data.Tuples;
	using SnowBank.Data.Tuples.Binary;
	using SnowBank.Buffers;

	/// <summary>Encoder that can process keys of variable size and types</summary>
	[PublicAPI]
	public interface IDynamicKeyEncoder : IKeyEncoder
	{

		/// <inheritdoc cref="IKeyEncoder.Encoding" />
		new IDynamicKeyEncoding Encoding { get; }

		#region Encoding...

		/// <summary>Packs a tuple of arbitrary length into a binary slice</summary>
		/// <param name="writer">Buffer where to append the binary representation</param>
		/// <param name="items">Tuple of any size (0 to N)</param>
		/// <exception cref="System.FormatException">If some elements in <paramref name="items"/> are not supported by this type system</exception>
		void PackKey<TTuple>(ref SliceWriter writer, TTuple items) where TTuple : IVarTuple;

		/// <summary>Encodes a key composed of a single element into a binary slice</summary>
		/// <typeparam name="T">Type of the element</typeparam>
		/// <param name="writer">Buffer where to append the binary representation</param>
		/// <param name="item1">Element to encode</param>
		void EncodeKey<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T>(ref SliceWriter writer, T? item1);

		/// <summary>Encodes a key composed of 2 elements into a binary slice</summary>
		/// <typeparam name="T1">Type of the first element</typeparam>
		/// <typeparam name="T2">Type of the second element</typeparam>
		/// <param name="writer">Buffer where to append the binary representation</param>
		/// <param name="item1">First element to encode</param>
		/// <param name="item2">Second element to encode</param>
		void EncodeKey<
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T1,
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T2
		>
			(ref SliceWriter writer, T1? item1, T2? item2);

		/// <summary>Encodes a key composed of 3 elements into a binary slice</summary>
		/// <typeparam name="T1">Type of the first element</typeparam>
		/// <typeparam name="T2">Type of the second element</typeparam>
		/// <typeparam name="T3">Type of the third element</typeparam>
		/// <param name="writer">Buffer where to append the binary representation</param>
		/// <param name="item1">First element to encode</param>
		/// <param name="item2">Second element to encode</param>
		/// <param name="item3">Third element to encode</param>
		void EncodeKey<
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T1,
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T2,
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T3
		>
			(ref SliceWriter writer, T1? item1, T2? item2, T3? item3);

		/// <summary>Encodes a key composed of 4 elements into a binary slice</summary>
		/// <typeparam name="T1">Type of the first element</typeparam>
		/// <typeparam name="T2">Type of the second element</typeparam>
		/// <typeparam name="T3">Type of the third element</typeparam>
		/// <typeparam name="T4">Type of the fourth element</typeparam>
		/// <param name="writer">Buffer where to append the binary representation</param>
		/// <param name="item1">First element to encode</param>
		/// <param name="item2">Second element to encode</param>
		/// <param name="item3">Third element to encode</param>
		/// <param name="item4">Fourth element to encode</param>
		void EncodeKey<
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T1,
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T2,
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T3,
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T4
		>
			(ref SliceWriter writer, T1? item1, T2? item2, T3? item3, T4? item4);

		/// <summary>Encodes a key composed of 5 elements into a binary slice</summary>
		/// <typeparam name="T1">Type of the first element</typeparam>
		/// <typeparam name="T2">Type of the second element</typeparam>
		/// <typeparam name="T3">Type of the third element</typeparam>
		/// <typeparam name="T4">Type of the fourth element</typeparam>
		/// <typeparam name="T5">Type of the fifth element</typeparam>
		/// <param name="writer">Buffer where to append the binary representation</param>
		/// <param name="item1">First element to encode</param>
		/// <param name="item2">Second element to encode</param>
		/// <param name="item3">Third element to encode</param>
		/// <param name="item4">Fourth element to encode</param>
		/// <param name="item5">Fifth element to encode</param>
		void EncodeKey<
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T1,
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T2,
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T3,
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T4,
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T5
		>
			(ref SliceWriter writer, T1? item1, T2? item2, T3? item3, T4? item4, T5? item5);

		/// <summary>Encodes a key composed of 6 elements into a binary slice</summary>
		/// <typeparam name="T1">Type of the first element</typeparam>
		/// <typeparam name="T2">Type of the second element</typeparam>
		/// <typeparam name="T3">Type of the third element</typeparam>
		/// <typeparam name="T4">Type of the fourth element</typeparam>
		/// <typeparam name="T5">Type of the fifth element</typeparam>
		/// <typeparam name="T6">Type of the sixth element</typeparam>
		/// <param name="writer">Buffer where to append the binary representation</param>
		/// <param name="item1">First element to encode</param>
		/// <param name="item2">Second element to encode</param>
		/// <param name="item3">Third element to encode</param>
		/// <param name="item4">Fourth element to encode</param>
		/// <param name="item5">Fifth element to encode</param>
		/// <param name="item6">Sixth element to encode</param>
		void EncodeKey<
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T1,
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T2,
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T3,
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T4,
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T5,
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T6
		>
			(ref SliceWriter writer, T1? item1, T2? item2, T3? item3, T4? item4, T5? item5, T6? item6);

		/// <summary>Encodes a key composed of 7 elements into a binary slice</summary>
		/// <typeparam name="T1">Type of the first element</typeparam>
		/// <typeparam name="T2">Type of the second element</typeparam>
		/// <typeparam name="T3">Type of the third element</typeparam>
		/// <typeparam name="T4">Type of the fourth element</typeparam>
		/// <typeparam name="T5">Type of the fifth element</typeparam>
		/// <typeparam name="T6">Type of the sixth element</typeparam>
		/// <typeparam name="T7">Type of the seventh element</typeparam>
		/// <param name="writer">Buffer where to append the binary representation</param>
		/// <param name="item1">First element to encode</param>
		/// <param name="item2">Second element to encode</param>
		/// <param name="item3">Third element to encode</param>
		/// <param name="item4">Fourth element to encode</param>
		/// <param name="item5">Fifth element to encode</param>
		/// <param name="item6">Sixth element to encode</param>
		/// <param name="item7">Seventh element to encode</param>
		void EncodeKey<
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T1,
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T2,
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T3,
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T4,
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T5,
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T6,
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T7
		>
			(ref SliceWriter writer, T1? item1, T2? item2, T3? item3, T4? item4, T5? item5, T6? item6, T7? item7);

		/// <summary>Encodes a key composed of 8 elements into a binary slice</summary>
		/// <typeparam name="T1">Type of the 1st element</typeparam>
		/// <typeparam name="T2">Type of the 2nd element</typeparam>
		/// <typeparam name="T3">Type of the 3rd element</typeparam>
		/// <typeparam name="T4">Type of the 4th element</typeparam>
		/// <typeparam name="T5">Type of the 5th element</typeparam>
		/// <typeparam name="T6">Type of the 6th element</typeparam>
		/// <typeparam name="T7">Type of the 7th element</typeparam>
		/// <typeparam name="T8">Type of the 8th element</typeparam>
		/// <param name="writer">Buffer where to append the binary representation</param>
		/// <param name="item1">1st element to encode</param>
		/// <param name="item2">2nd element to encode</param>
		/// <param name="item3">3rd element to encode</param>
		/// <param name="item4">4th element to encode</param>
		/// <param name="item5">5th element to encode</param>
		/// <param name="item6">6th element to encode</param>
		/// <param name="item7">7th element to encode</param>
		/// <param name="item8">8th element to encode</param>
		void EncodeKey<
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T1,
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T2,
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T3,
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T4,
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T5,
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T6,
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T7,
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T8
		>(ref SliceWriter writer, T1? item1, T2? item2, T3? item3, T4? item4, T5? item5, T6? item6, T7? item7, T8? item8);

		/// <summary>Encodes a key composed of 9 elements into a binary slice</summary>
		/// <typeparam name="T1">Type of the 1st element</typeparam>
		/// <typeparam name="T2">Type of the 2nd element</typeparam>
		/// <typeparam name="T3">Type of the 3rd element</typeparam>
		/// <typeparam name="T4">Type of the 4th element</typeparam>
		/// <typeparam name="T5">Type of the 5th element</typeparam>
		/// <typeparam name="T6">Type of the 6th element</typeparam>
		/// <typeparam name="T7">Type of the 7th element</typeparam>
		/// <typeparam name="T8">Type of the 8th element</typeparam>
		/// <typeparam name="T9">Type of the 9th element</typeparam>
		/// <param name="writer">Buffer where to append the binary representation</param>
		/// <param name="item1">1st element to encode</param>
		/// <param name="item2">2nd element to encode</param>
		/// <param name="item3">3rd element to encode</param>
		/// <param name="item4">4th element to encode</param>
		/// <param name="item5">5th element to encode</param>
		/// <param name="item6">6th element to encode</param>
		/// <param name="item7">7th element to encode</param>
		/// <param name="item8">8th element to encode</param>
		/// <param name="item9">9th element to encode</param>
		void EncodeKey<
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T1,
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T2,
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T3,
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T4,
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T5,
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T6,
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T7,
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T8,
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T9
		>(ref SliceWriter writer, T1? item1, T2? item2, T3? item3, T4? item4, T5? item5, T6? item6, T7? item7, T8? item8, T9? item9);

		#endregion

		#region Decoding...

		/// <summary>Decodes a binary slice into a tuple of arbitrary length</summary>
		/// <param name="packed">Binary slice produced by a previous call to <see cref="PackKey{TTuple}"/></param>
		/// <returns>Tuple of any size (0 to N)</returns>
		IVarTuple UnpackKey(Slice packed);

		/// <summary>Decodes a binary slice into a tuple of arbitrary length</summary>
		/// <param name="packed">Binary slice produced by a previous call to <see cref="PackKey{TTuple}"/></param>
		/// <returns>Tuple of any size (0 to N)</returns>
		SpanTuple UnpackKey(ReadOnlySpan<byte> packed);

		/// <summary>Attempts to decode a binary slice into a tuple of arbitrary length</summary>
		/// <param name="packed">Binary slice produced by a previous call to <see cref="PackKey{TTuple}"/></param>
		/// <param name="tuple">Tuple of any size (0 to N), if the method returns true</param>
		/// <returns><see langword="true"/> if <paramref name="packed"/> was a legal binary representation; otherwise, <see langword="false"/>.</returns>
		bool TryUnpackKey(Slice packed, [NotNullWhen(true)] out IVarTuple? tuple);

		/// <summary>Attempts to decode a binary slice into a tuple of arbitrary length</summary>
		/// <param name="packed">Binary slice produced by a previous call to <see cref="PackKey{TTuple}"/></param>
		/// <param name="tuple">Tuple of any size (0 to N), if the method returns true</param>
		/// <returns><see langword="true"/> if <paramref name="packed"/> was a legal binary representation; otherwise, <see langword="false"/>.</returns>
		bool TryUnpackKey(ReadOnlySpan<byte> packed, out SpanTuple tuple);

		/// <summary>Decodes a binary slice containing exactly on element</summary>
		/// <typeparam name="T">Expected type of the element</typeparam>
		/// <param name="packed">Binary slice produced by a previous call to <see cref="EncodeKey{T1}"/> or <see cref="EncodeKey{T1}"/></param>
		/// <returns>Value of the decoded element, or an exception if the data is invalid or the encoded tuple is empty or has more than one element</returns>
		T? DecodeKey<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T>(Slice packed);

		/// <summary>Decodes a binary slice containing exactly on element</summary>
		/// <typeparam name="T">Expected type of the element</typeparam>
		/// <param name="packed">Binary slice produced by a previous call to <see cref="EncodeKey{T1}"/> or <see cref="EncodeKey{T1}"/></param>
		/// <returns>Value of the decoded element, or an exception if the data is invalid or the encoded tuple is empty or has more than one element</returns>
		T? DecodeKey<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T>(ReadOnlySpan<byte> packed);

		/// <summary>Decodes a binary slice containing exactly two elements</summary>
		/// <typeparam name="T1">Expected type of the 1st element</typeparam>
		/// <typeparam name="T2">Expected type of the 2nd element</typeparam>
		/// <param name="packed">Binary slice produced by a previous call to <see cref="EncodeKey{T1, T2}"/> or <see cref="EncodeKey{T1, T2}"/></param>
		/// <returns>Tuple containing two elements, or an exception if the data is invalid, or the tuples has less or more than two elements</returns>
		(T1?, T2?) DecodeKey<
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T1,
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T2>
			(Slice packed);

		/// <summary>Decodes a binary slice containing exactly two elements</summary>
		/// <typeparam name="T1">Expected type of the 1st element</typeparam>
		/// <typeparam name="T2">Expected type of the 2nd element</typeparam>
		/// <param name="packed">Binary slice produced by a previous call to <see cref="EncodeKey{T1, T2}"/> or <see cref="EncodeKey{T1, T2}"/></param>
		/// <returns>Tuple containing two elements, or an exception if the data is invalid, or the tuples has less or more than two elements</returns>
		(T1?, T2?) DecodeKey<
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T1,
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T2>
			(ReadOnlySpan<byte> packed);

		/// <summary>Decodes a binary slice containing exactly three elements</summary>
		/// <typeparam name="T1">Expected type of the 1st element</typeparam>
		/// <typeparam name="T2">Expected type of the 2nd element</typeparam>
		/// <typeparam name="T3">Expected type of the 3rd element</typeparam>
		/// <param name="packed">Binary slice produced by a previous call to <see cref="EncodeKey{T1, T2, T3}"/> or <see cref="EncodeKey{T1, T2, T3}"/></param>
		/// <returns>Tuple containing three elements, or an exception if the data is invalid, or the tuples has less or more than three elements</returns>
		(T1?, T2?, T3?) DecodeKey<
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T1,
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T2,
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T3>
			(Slice packed);

		/// <summary>Decodes a binary slice containing exactly three elements</summary>
		/// <typeparam name="T1">Expected type of the 1st element</typeparam>
		/// <typeparam name="T2">Expected type of the 2nd element</typeparam>
		/// <typeparam name="T3">Expected type of the 3rd element</typeparam>
		/// <param name="packed">Binary slice produced by a previous call to <see cref="EncodeKey{T1, T2, T3}"/> or <see cref="EncodeKey{T1, T2, T3}"/></param>
		/// <returns>Tuple containing three elements, or an exception if the data is invalid, or the tuples has less or more than three elements</returns>
		(T1?, T2?, T3?) DecodeKey<
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T1,
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T2,
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T3>
			(ReadOnlySpan<byte> packed);

		/// <summary>Decodes a binary slice containing exactly four elements</summary>
		/// <typeparam name="T1">Expected type of the 1st element</typeparam>
		/// <typeparam name="T2">Expected type of the 2nd element</typeparam>
		/// <typeparam name="T3">Expected type of the 3rd element</typeparam>
		/// <typeparam name="T4">Expected type of the 4th element</typeparam>
		/// <param name="packed">Binary slice produced by a previous call to <see cref="EncodeKey{T1, T2, T3, T4}"/> or <see cref="EncodeKey{T1, T2, T3, T4}"/></param>
		/// <returns>Tuple containing four elements, or an exception if the data is invalid, or the tuples has less or more than four elements</returns>
		(T1?, T2?, T3?, T4?) DecodeKey<
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T1,
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T2,
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T3,
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T4>
			(Slice packed);

		/// <summary>Decodes a binary slice containing exactly four elements</summary>
		/// <typeparam name="T1">Expected type of the 1st element</typeparam>
		/// <typeparam name="T2">Expected type of the 2nd element</typeparam>
		/// <typeparam name="T3">Expected type of the 3rd element</typeparam>
		/// <typeparam name="T4">Expected type of the 4th element</typeparam>
		/// <param name="packed">Binary slice produced by a previous call to <see cref="EncodeKey{T1, T2, T3, T4}"/> or <see cref="EncodeKey{T1, T2, T3, T4}"/></param>
		/// <returns>Tuple containing four elements, or an exception if the data is invalid, or the tuples has less or more than four elements</returns>
		(T1?, T2?, T3?, T4?) DecodeKey<
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T1,
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T2,
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T3,
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T4>
			(ReadOnlySpan<byte> packed);

		/// <summary>Decodes a binary slice containing exactly five elements</summary>
		/// <typeparam name="T1">Expected type of the 1st element</typeparam>
		/// <typeparam name="T2">Expected type of the 2nd element</typeparam>
		/// <typeparam name="T3">Expected type of the 3rd element</typeparam>
		/// <typeparam name="T4">Expected type of the 4th element</typeparam>
		/// <typeparam name="T5">Expected type of the 5th element</typeparam>
		/// <param name="packed">Binary slice produced by a previous call to <see cref="EncodeKey{T1, T2, T3, T4, T5}"/> or <see cref="EncodeKey{T1, T2, T3, T4, T5}"/></param>
		/// <returns>Tuple containing five elements, or an exception if the data is invalid, or the tuples has less or more than five elements</returns>
		(T1?, T2?, T3?, T4?, T5?) DecodeKey<
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T1,
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T2,
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T3,
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T4,
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T5>
			(Slice packed);

		/// <summary>Decodes a binary slice containing exactly five elements</summary>
		/// <typeparam name="T1">Expected type of the 1st element</typeparam>
		/// <typeparam name="T2">Expected type of the 2nd element</typeparam>
		/// <typeparam name="T3">Expected type of the 3rd element</typeparam>
		/// <typeparam name="T4">Expected type of the 4th element</typeparam>
		/// <typeparam name="T5">Expected type of the 5th element</typeparam>
		/// <param name="packed">Binary slice produced by a previous call to <see cref="EncodeKey{T1, T2, T3, T4, T5}"/> or <see cref="EncodeKey{T1, T2, T3, T4, T5}"/></param>
		/// <returns>Tuple containing five elements, or an exception if the data is invalid, or the tuples has less or more than five elements</returns>
		(T1?, T2?, T3?, T4?, T5?) DecodeKey<
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T1,
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T2,
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T3,
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T4,
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T5>
			(ReadOnlySpan<byte> packed);

		/// <summary>Decodes a binary slice containing exactly six elements</summary>
		/// <typeparam name="T1">Expected type of the 1st element</typeparam>
		/// <typeparam name="T2">Expected type of the 2nd element</typeparam>
		/// <typeparam name="T3">Expected type of the 3rd element</typeparam>
		/// <typeparam name="T4">Expected type of the 4th element</typeparam>
		/// <typeparam name="T5">Expected type of the 5th element</typeparam>
		/// <typeparam name="T6">Expected type of the 6th element</typeparam>
		/// <param name="packed">Binary slice produced by a previous call to <see cref="EncodeKey{T1, T2, T3, T4, T5, T6}"/> or <see cref="EncodeKey{T1, T2, T3, T4, T5, T6}"/></param>
		/// <returns>Tuple containing six elements, or an exception if the data is invalid, or the tuples has less or more than six elements</returns>
		(T1?, T2?, T3?, T4?, T5?, T6?) DecodeKey<
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T1,
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T2,
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T3,
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T4,
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T5,
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T6>
			(Slice packed);

		/// <summary>Decodes a binary slice containing exactly six elements</summary>
		/// <typeparam name="T1">Expected type of the 1st element</typeparam>
		/// <typeparam name="T2">Expected type of the 2nd element</typeparam>
		/// <typeparam name="T3">Expected type of the 3rd element</typeparam>
		/// <typeparam name="T4">Expected type of the 4th element</typeparam>
		/// <typeparam name="T5">Expected type of the 5th element</typeparam>
		/// <typeparam name="T6">Expected type of the 6th element</typeparam>
		/// <param name="packed">Binary slice produced by a previous call to <see cref="EncodeKey{T1, T2, T3, T4, T5, T6}"/> or <see cref="EncodeKey{T1, T2, T3, T4, T5, T6}"/></param>
		/// <returns>Tuple containing six elements, or an exception if the data is invalid, or the tuples has less or more than six elements</returns>
		(T1?, T2?, T3?, T4?, T5?, T6?) DecodeKey<
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T1,
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T2,
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T3,
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T4,
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T5,
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T6>
			(ReadOnlySpan<byte> packed);

		/// <summary>Decodes a binary slice containing exactly seven elements</summary>
		/// <typeparam name="T1">Expected type of the 1st element</typeparam>
		/// <typeparam name="T2">Expected type of the 2nd element</typeparam>
		/// <typeparam name="T3">Expected type of the 3rd element</typeparam>
		/// <typeparam name="T4">Expected type of the 4th element</typeparam>
		/// <typeparam name="T5">Expected type of the 5th element</typeparam>
		/// <typeparam name="T6">Expected type of the 6th element</typeparam>
		/// <typeparam name="T7">Expected type of the 7th element</typeparam>
		/// <param name="packed">Binary slice produced by a previous call to <see cref="EncodeKey{T1, T2, T3, T4, T5, T6, T7}"/> or <see cref="EncodeKey{T1, T2, T3, T4, T5, T6, T7}"/></param>
		/// <returns>Tuple containing seven elements, or an exception if the data is invalid, or the tuples has less or more than seven elements</returns>
		(T1?, T2?, T3?, T4?, T5?, T6?, T7?) DecodeKey<
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T1,
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T2,
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T3,
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T4,
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T5,
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T6,
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T7>
			(Slice packed);

		/// <summary>Decodes a binary slice containing exactly seven elements</summary>
		/// <typeparam name="T1">Expected type of the 1st element</typeparam>
		/// <typeparam name="T2">Expected type of the 2nd element</typeparam>
		/// <typeparam name="T3">Expected type of the 3rd element</typeparam>
		/// <typeparam name="T4">Expected type of the 4th element</typeparam>
		/// <typeparam name="T5">Expected type of the 5th element</typeparam>
		/// <typeparam name="T6">Expected type of the 6th element</typeparam>
		/// <typeparam name="T7">Expected type of the 7th element</typeparam>
		/// <param name="packed">Binary slice produced by a previous call to <see cref="EncodeKey{T1, T2, T3, T4, T5, T6, T7}"/> or <see cref="EncodeKey{T1, T2, T3, T4, T5, T6, T7}"/></param>
		/// <returns>Tuple containing seven elements, or an exception if the data is invalid, or the tuples has less or more than seven elements</returns>
		(T1?, T2?, T3?, T4?, T5?, T6?, T7?) DecodeKey<
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T1,
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T2,
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T3,
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T4,
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T5,
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T6,
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T7>
			(ReadOnlySpan<byte> packed);

		/// <summary>Decodes a binary slice containing exactly eight elements</summary>
		/// <typeparam name="T1">Expected type of the 1st element</typeparam>
		/// <typeparam name="T2">Expected type of the 2nd element</typeparam>
		/// <typeparam name="T3">Expected type of the 3rd element</typeparam>
		/// <typeparam name="T4">Expected type of the 4th element</typeparam>
		/// <typeparam name="T5">Expected type of the 5th element</typeparam>
		/// <typeparam name="T6">Expected type of the 6th element</typeparam>
		/// <typeparam name="T7">Expected type of the 7th element</typeparam>
		/// <typeparam name="T8">Expected type of the 8th element</typeparam>
		/// <param name="packed">Binary slice produced by a previous call to <see cref="EncodeKey{T1, T2, T3, T4, T5, T6, T7, T8}"/> or <see cref="EncodeKey{T1, T2, T3, T4, T5, T6, T7, T8}"/></param>
		/// <returns>Tuple containing eight elements, or an exception if the data is invalid, or the tuples has less or more than eight elements</returns>
		(T1?, T2?, T3?, T4?, T5?, T6?, T7?, T8?) DecodeKey<
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T1,
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T2,
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T3,
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T4,
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T5,
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T6,
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T7,
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T8>
			(Slice packed);

		/// <summary>Decodes a binary slice containing exactly eight elements</summary>
		/// <typeparam name="T1">Expected type of the 1st element</typeparam>
		/// <typeparam name="T2">Expected type of the 2nd element</typeparam>
		/// <typeparam name="T3">Expected type of the 3rd element</typeparam>
		/// <typeparam name="T4">Expected type of the 4th element</typeparam>
		/// <typeparam name="T5">Expected type of the 5th element</typeparam>
		/// <typeparam name="T6">Expected type of the 6th element</typeparam>
		/// <typeparam name="T7">Expected type of the 7th element</typeparam>
		/// <typeparam name="T8">Expected type of the 8th element</typeparam>
		/// <param name="packed">Binary slice produced by a previous call to <see cref="EncodeKey{T1, T2, T3, T4, T5, T6, T7, T8}"/> or <see cref="EncodeKey{T1, T2, T3, T4, T5, T6, T7, T8}"/></param>
		/// <returns>Tuple containing eight elements, or an exception if the data is invalid, or the tuples has less or more than eight elements</returns>
		(T1?, T2?, T3?, T4?, T5?, T6?, T7?, T8?) DecodeKey<
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T1,
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T2,
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T3,
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T4,
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T5,
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T6,
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T7,
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T8>
			(ReadOnlySpan<byte> packed);

		/// <summary>Decodes a binary slice containing exactly nine elements</summary>
		/// <typeparam name="T1">Expected type of the 1st element</typeparam>
		/// <typeparam name="T2">Expected type of the 2nd element</typeparam>
		/// <typeparam name="T3">Expected type of the 3rd element</typeparam>
		/// <typeparam name="T4">Expected type of the 4th element</typeparam>
		/// <typeparam name="T5">Expected type of the 5th element</typeparam>
		/// <typeparam name="T6">Expected type of the 6th element</typeparam>
		/// <typeparam name="T7">Expected type of the 7th element</typeparam>
		/// <typeparam name="T8">Expected type of the 8th element</typeparam>
		/// <typeparam name="T9">Expected type of the 9th element</typeparam>
		/// <param name="packed">Binary slice produced by a previous call to <see cref="EncodeKey{T1, T2, T3, T4, T5, T6, T7, T8, T9}"/> or <see cref="EncodeKey{T1, T2, T3, T4, T5, T6, T7, T8, T9}"/></param>
		/// <returns>Tuple containing eight elements, or an exception if the data is invalid, or the tuples has less or more than eight elements</returns>
		(T1?, T2?, T3?, T4?, T5?, T6?, T7?, T8?, T9?) DecodeKey<
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T1,
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T2,
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T3,
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T4,
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T5,
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T6,
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T7,
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T8,
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T9>
		(Slice packed);

		/// <summary>Decodes a binary slice containing exactly nine elements</summary>
		/// <typeparam name="T1">Expected type of the 1st element</typeparam>
		/// <typeparam name="T2">Expected type of the 2nd element</typeparam>
		/// <typeparam name="T3">Expected type of the 3rd element</typeparam>
		/// <typeparam name="T4">Expected type of the 4th element</typeparam>
		/// <typeparam name="T5">Expected type of the 5th element</typeparam>
		/// <typeparam name="T6">Expected type of the 6th element</typeparam>
		/// <typeparam name="T7">Expected type of the 7th element</typeparam>
		/// <typeparam name="T8">Expected type of the 8th element</typeparam>
		/// <typeparam name="T9">Expected type of the 9th element</typeparam>
		/// <param name="packed">Binary slice produced by a previous call to <see cref="EncodeKey{T1, T2, T3, T4, T5, T6, T7, T8, T9}"/> or <see cref="EncodeKey{T1, T2, T3, T4, T5, T6, T7, T8, T9}"/></param>
		/// <returns>Tuple containing eight elements, or an exception if the data is invalid, or the tuples has less or more than eight elements</returns>
		(T1?, T2?, T3?, T4?, T5?, T6?, T7?, T8?, T9?) DecodeKey<
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T1,
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T2,
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T3,
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T4,
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T5,
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T6,
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T7,
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T8,
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T9>
		(ReadOnlySpan<byte> packed);

		/// <summary>Decodes the first element of a binary slice containing at least 1 element</summary>
		/// <typeparam name="T1">Expected type of the first element</typeparam>
		/// <param name="packed">Binary slice that contains one or more elements.</param>
		/// <param name="expectedSize">If non-null, checks that the decoded tuple has the given size</param>
		/// <returns>Decoded value of first element.</returns>
		/// <exception cref="InvalidOperationException"> the decoded tuple does not have the expected size</exception>
		T1? DecodeKeyFirst<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T1>(Slice packed, int? expectedSize = null);

		/// <summary>Decodes the first element of a binary slice containing at least 1 element</summary>
		/// <typeparam name="T1">Expected type of the first element</typeparam>
		/// <param name="packed">Binary slice that contains one or more elements.</param>
		/// <param name="expectedSize">If non-null, checks that the decoded tuple has the given size</param>
		/// <returns>Decoded value of first element.</returns>
		/// <exception cref="InvalidOperationException"> the decoded tuple does not have the expected size</exception>
		T1? DecodeKeyFirst<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T1>(ReadOnlySpan<byte> packed, int? expectedSize = null);

		/// <summary>Decodes the first 2 elements of a binary slice containing at least 2 elements</summary>
		/// <typeparam name="T1">Expected type of the first element</typeparam>
		/// <typeparam name="T2">Expected type of the second element</typeparam>
		/// <param name="packed">Binary slice that contains at least 2 elements.</param>
		/// <param name="expectedSize">If non-null, checks that the decoded tuple has the given size</param>
		/// <returns>Decoded values of the first 2 elements.</returns>
		/// <exception cref="InvalidOperationException"> the decoded tuple does not have the expected size</exception>
		(T1?, T2?) DecodeKeyFirst<
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T1,
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T2>
			(Slice packed, int? expectedSize = null);

		/// <summary>Decodes the first 2 elements of a binary slice containing at least 2 elements</summary>
		/// <typeparam name="T1">Expected type of the first element</typeparam>
		/// <typeparam name="T2">Expected type of the second element</typeparam>
		/// <param name="packed">Binary slice that contains at least 2 elements.</param>
		/// <param name="expectedSize">If non-null, checks that the decoded tuple has the given size</param>
		/// <returns>Decoded values of the first 2 elements.</returns>
		/// <exception cref="InvalidOperationException"> the decoded tuple does not have the expected size</exception>
		(T1?, T2?) DecodeKeyFirst<
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T1,
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T2>
			(ReadOnlySpan<byte> packed, int? expectedSize = null);

		/// <summary>Decodes the first 3 elements of a binary slice containing at least 3 elements</summary>
		/// <typeparam name="T1">Expected type of the first element</typeparam>
		/// <typeparam name="T2">Expected type of the second element</typeparam>
		/// <typeparam name="T3">Expected type of the third element</typeparam>
		/// <param name="packed">Binary slice that contains at least 3 elements.</param>
		/// <param name="expectedSize">If non-null, checks that the decoded tuple has the given size</param>
		/// <returns>Decoded values of the first 3 elements.</returns>
		/// <exception cref="InvalidOperationException"> the decoded tuple does not have the expected size</exception>
		(T1?, T2?, T3?) DecodeKeyFirst<
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T1,
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T2,
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T3>
			(Slice packed, int? expectedSize = null);

		/// <summary>Decodes the first 3 elements of a binary slice containing at least 3 elements</summary>
		/// <typeparam name="T1">Expected type of the first element</typeparam>
		/// <typeparam name="T2">Expected type of the second element</typeparam>
		/// <typeparam name="T3">Expected type of the third element</typeparam>
		/// <param name="packed">Binary slice that contains at least 3 elements.</param>
		/// <param name="expectedSize">If non-null, checks that the decoded tuple has the given size</param>
		/// <returns>Decoded values of the first 3 elements.</returns>
		/// <exception cref="InvalidOperationException"> the decoded tuple does not have the expected size</exception>
		(T1?, T2?, T3?) DecodeKeyFirst<
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T1,
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T2,
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T3>
			(ReadOnlySpan<byte> packed, int? expectedSize = null);

		/// <summary>Decodes the last element of a binary slice containing at least 1 element</summary>
		/// <typeparam name="T">Expected type of the last element</typeparam>
		/// <param name="packed">Binary slice that contains one or more elements.</param>
		/// <param name="expectedSize">If non-null, checks that the decoded tuple has the given size</param>
		/// <returns>Decoded value of the last element.</returns>
		/// <exception cref="InvalidOperationException"> the decoded tuple does not have the expected size</exception>
		T? DecodeKeyLast<
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T>
			(Slice packed, int? expectedSize = null);

		/// <summary>Decodes the last element of a binary slice containing at least 1 element</summary>
		/// <typeparam name="T">Expected type of the last element</typeparam>
		/// <param name="packed">Binary slice that contains one or more elements.</param>
		/// <param name="expectedSize">If non-null, checks that the decoded tuple has the given size</param>
		/// <returns>Decoded value of the last element.</returns>
		/// <exception cref="InvalidOperationException"> the decoded tuple does not have the expected size</exception>
		T? DecodeKeyLast<
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T>
			(ReadOnlySpan<byte> packed, int? expectedSize = null);

		/// <summary>Decodes the last 2 elements of a binary slice containing at least 2 elements</summary>
		/// <typeparam name="T1">Expected type of the second to last element</typeparam>
		/// <typeparam name="T2">Expected type of the last element</typeparam>
		/// <param name="packed">Binary slice that contains one or more elements.</param>
		/// <param name="expectedSize">If non-null, checks that the decoded tuple has the given size</param>
		/// <returns>Decoded values of the last 2 elements.</returns>
		/// <exception cref="InvalidOperationException"> the decoded tuple does not have the expected size</exception>
		(T1?, T2?) DecodeKeyLast<
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T1,
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T2>(Slice packed, int? expectedSize = null);

		/// <summary>Decodes the last 2 elements of a binary slice containing at least 2 elements</summary>
		/// <typeparam name="T1">Expected type of the second to last element</typeparam>
		/// <typeparam name="T2">Expected type of the last element</typeparam>
		/// <param name="packed">Binary slice that contains one or more elements.</param>
		/// <param name="expectedSize">If non-null, checks that the decoded tuple has the given size</param>
		/// <returns>Decoded values of the last 2 elements.</returns>
		/// <exception cref="InvalidOperationException"> the decoded tuple does not have the expected size</exception>
		(T1?, T2?) DecodeKeyLast<
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T1,
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T2>
			(ReadOnlySpan<byte> packed, int? expectedSize = null);

		/// <summary>Decodes the last 3 elements of a binary slice containing at least 3 elements</summary>
		/// <typeparam name="T1">Expected type of the third to last element</typeparam>
		/// <typeparam name="T2">Expected type of the second to last element</typeparam>
		/// <typeparam name="T3">Expected type of the last element</typeparam>
		/// <param name="packed">Binary slice that contains one or more elements.</param>
		/// <param name="expectedSize">If non-null, checks that the decoded tuple has the given size</param>
		/// <returns>Decoded values of the last 3 elements.</returns>
		/// <exception cref="InvalidOperationException"> the decoded tuple does not have the expected size</exception>
		(T1?, T2?, T3?) DecodeKeyLast<
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T1,
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T2,
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T3>
			(Slice packed, int? expectedSize = null);

		/// <summary>Decodes the last 3 elements of a binary slice containing at least 3 elements</summary>
		/// <typeparam name="T1">Expected type of the third to last element</typeparam>
		/// <typeparam name="T2">Expected type of the second to last element</typeparam>
		/// <typeparam name="T3">Expected type of the last element</typeparam>
		/// <param name="packed">Binary slice that contains one or more elements.</param>
		/// <param name="expectedSize">If non-null, checks that the decoded tuple has the given size</param>
		/// <returns>Decoded values of the last 3 elements.</returns>
		/// <exception cref="InvalidOperationException"> the decoded tuple does not have the expected size</exception>
		(T1?, T2?, T3?) DecodeKeyLast<
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T1,
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T2,
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T3>
			(ReadOnlySpan<byte> packed, int? expectedSize = null);

		/// <summary>Decodes the last 4 elements of a binary slice containing at least 4 elements</summary>
		/// <typeparam name="T1">Expected type of the fourth to last element</typeparam>
		/// <typeparam name="T2">Expected type of the third to last element</typeparam>
		/// <typeparam name="T3">Expected type of the second to last element</typeparam>
		/// <typeparam name="T4">Expected type of the last element</typeparam>
		/// <param name="packed">Binary slice that contains one or more elements.</param>
		/// <param name="expectedSize">If non-null, checks that the decoded tuple has the given size</param>
		/// <returns>Decoded values of the last 4 elements.</returns>
		/// <exception cref="InvalidOperationException"> the decoded tuple does not have the expected size</exception>
		(T1?, T2?, T3?, T4?) DecodeKeyLast<
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T1,
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T2,
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T3,
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T4>
			(Slice packed, int? expectedSize = null);

		/// <summary>Decodes the last 4 elements of a binary slice containing at least 4 elements</summary>
		/// <typeparam name="T1">Expected type of the fourth to last element</typeparam>
		/// <typeparam name="T2">Expected type of the third to last element</typeparam>
		/// <typeparam name="T3">Expected type of the second to last element</typeparam>
		/// <typeparam name="T4">Expected type of the last element</typeparam>
		/// <param name="packed">Binary slice that contains one or more elements.</param>
		/// <param name="expectedSize">If non-null, checks that the decoded tuple has the given size</param>
		/// <returns>Decoded values of the last 4 elements.</returns>
		/// <exception cref="InvalidOperationException"> the decoded tuple does not have the expected size</exception>
		(T1?, T2?, T3?, T4?) DecodeKeyLast<
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T1,
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T2,
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T3,
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T4>
			(ReadOnlySpan<byte> packed, int? expectedSize = null);

		#endregion

		#region Ranges...

		/// <summary>Returns a range that contains all the keys under a subspace of the encoder subspace, using the semantic of the encoding</summary>
		/// <param name="prefix">Optional binary prefix</param>
		/// <returns>Key range which derives from the semantic of the current encoding</returns>
		/// <remarks>For example, the Tuple encoding will produce ranges of the form "(Key + \x00) &lt;= x &lt; (Key + \xFF)", while a binary-based encoding would produce ranges of the form "Key &lt;= x &lt; Increment(Key)"</remarks>
		(Slice Begin, Slice End) ToRange(Slice prefix = default);

		/// <summary>Returns a key range using a tuple as a prefix</summary>
		/// <param name="prefix">Optional binary prefix that should be added before encoding the key</param>
		/// <param name="items">Tuple of any size (0 to N)</param>
		(Slice Begin, Slice End) ToRange<TTuple>(Slice prefix, TTuple items) where TTuple : IVarTuple;

		/// <summary>Returns a key range using a single element as a prefix</summary>
		/// <typeparam name="T1">Type of the element</typeparam>
		/// <param name="prefix">Optional binary prefix that should be added before encoding the key</param>
		/// <param name="item1">Element to encode</param>
		(Slice Begin, Slice End) ToKeyRange<
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T1>
			(Slice prefix, T1 item1);

		/// <summary>Returns a key range using 2 elements as a prefix</summary>
		/// <typeparam name="T1">Type of the first element</typeparam>
		/// <typeparam name="T2">Type of the second element</typeparam>
		/// <param name="prefix">Optional binary prefix that should be added before encoding the key</param>
		/// <param name="item1">First element to encode</param>
		/// <param name="item2">Second element to encode</param>
		(Slice Begin, Slice End) ToKeyRange<
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T1,
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T2>
			(Slice prefix, T1 item1, T2 item2);

		/// <summary>Returns a key range using 3 elements as a prefix</summary>
		/// <typeparam name="T1">Type of the first element</typeparam>
		/// <typeparam name="T2">Type of the second element</typeparam>
		/// <typeparam name="T3">Type of the third element</typeparam>
		/// <param name="prefix">Optional binary prefix that should be added before encoding the key</param>
		/// <param name="item1">First element to encode</param>
		/// <param name="item2">Second element to encode</param>
		/// <param name="item3">Third element to encode</param>
		(Slice Begin, Slice End) ToKeyRange<
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T1,
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T2,
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T3>
			(Slice prefix, T1 item1, T2 item2, T3 item3);

		/// <summary>Returns a key range using 4 elements as a prefix</summary>
		/// <typeparam name="T1">Type of the first element</typeparam>
		/// <typeparam name="T2">Type of the second element</typeparam>
		/// <typeparam name="T3">Type of the third element</typeparam>
		/// <typeparam name="T4">Type of the fourth element</typeparam>
		/// <param name="prefix">Optional binary prefix that should be added before encoding the key</param>
		/// <param name="item1">First element to encode</param>
		/// <param name="item2">Second element to encode</param>
		/// <param name="item3">Third element to encode</param>
		/// <param name="item4">Fourth element to encode</param>
		(Slice Begin, Slice End) ToKeyRange<
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T1,
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T2,
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T3,
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T4>
			(Slice prefix, T1 item1, T2 item2, T3 item3, T4 item4);

		/// <summary>Returns a key range using 5 elements as a prefix</summary>
		/// <typeparam name="T1">Type of the first element</typeparam>
		/// <typeparam name="T2">Type of the second element</typeparam>
		/// <typeparam name="T3">Type of the third element</typeparam>
		/// <typeparam name="T4">Type of the fourth element</typeparam>
		/// <typeparam name="T5">Type of the fifth element</typeparam>
		/// <param name="prefix">Optional binary prefix that should be added before encoding the key</param>
		/// <param name="item1">First element to encode</param>
		/// <param name="item2">Second element to encode</param>
		/// <param name="item3">Third element to encode</param>
		/// <param name="item4">Fourth element to encode</param>
		/// <param name="item5">Fifth element to encode</param>
		(Slice Begin, Slice End) ToKeyRange<
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T1,
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T2,
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T3,
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T4,
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T5>
			(Slice prefix, T1 item1, T2 item2, T3 item3, T4 item4, T5 item5);

		/// <summary>Returns a key range using 6 elements as a prefix</summary>
		/// <typeparam name="T1">Type of the first element</typeparam>
		/// <typeparam name="T2">Type of the second element</typeparam>
		/// <typeparam name="T3">Type of the third element</typeparam>
		/// <typeparam name="T4">Type of the fourth element</typeparam>
		/// <typeparam name="T5">Type of the fifth element</typeparam>
		/// <typeparam name="T6">Type of the sixth element</typeparam>
		/// <param name="prefix">Optional binary prefix that should be added before encoding the key</param>
		/// <param name="item1">First element to encode</param>
		/// <param name="item2">Second element to encode</param>
		/// <param name="item3">Third element to encode</param>
		/// <param name="item4">Fourth element to encode</param>
		/// <param name="item5">Fifth element to encode</param>
		/// <param name="item6">Sixth element to encode</param>
		(Slice Begin, Slice End) ToKeyRange<
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T1,
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T2,
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T3,
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T4,
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T5,
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T6>
			(Slice prefix, T1 item1, T2 item2, T3 item3, T4 item4, T5 item5, T6 item6);

		/// <summary>Returns a key range using 7 elements as a prefix</summary>
		/// <typeparam name="T1">Type of the first element</typeparam>
		/// <typeparam name="T2">Type of the second element</typeparam>
		/// <typeparam name="T3">Type of the third element</typeparam>
		/// <typeparam name="T4">Type of the fourth element</typeparam>
		/// <typeparam name="T5">Type of the fifth element</typeparam>
		/// <typeparam name="T6">Type of the sixth element</typeparam>
		/// <typeparam name="T7">Type of the seventh element</typeparam>
		/// <param name="prefix">Optional binary prefix that should be added before encoding the key</param>
		/// <param name="item1">First element to encode</param>
		/// <param name="item2">Second element to encode</param>
		/// <param name="item3">Third element to encode</param>
		/// <param name="item4">Fourth element to encode</param>
		/// <param name="item5">Fifth element to encode</param>
		/// <param name="item6">Sixth element to encode</param>
		/// <param name="item7">Seventh element to encode</param>
		(Slice Begin, Slice End) ToKeyRange<
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T1,
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T2,
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T3,
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T4,
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T5,
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T6,
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T7>
			(Slice prefix, T1 item1, T2 item2, T3 item3, T4 item4, T5 item5, T6 item6, T7 item7);

		/// <summary>Returns a key range using 8 elements as a prefix</summary>
		/// <typeparam name="T1">Type of the 1st element</typeparam>
		/// <typeparam name="T2">Type of the 2nd element</typeparam>
		/// <typeparam name="T3">Type of the 3rd element</typeparam>
		/// <typeparam name="T4">Type of the 4th element</typeparam>
		/// <typeparam name="T5">Type of the 5th element</typeparam>
		/// <typeparam name="T6">Type of the 6th element</typeparam>
		/// <typeparam name="T7">Type of the 7th element</typeparam>
		/// <typeparam name="T8">Type of the 8th element</typeparam>
		/// <param name="prefix">Optional binary prefix that should be added before encoding the key</param>
		/// <param name="item1">1st element to encode</param>
		/// <param name="item2">2nd element to encode</param>
		/// <param name="item3">3rd element to encode</param>
		/// <param name="item4">4th element to encode</param>
		/// <param name="item5">5th element to encode</param>
		/// <param name="item6">6th element to encode</param>
		/// <param name="item7">7th element to encode</param>
		/// <param name="item8">8th element to encode</param>
		(Slice Begin, Slice End) ToKeyRange<
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T1,
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T2,
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T3,
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T4,
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T5,
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T6,
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T7,
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T8>
			(Slice prefix, T1 item1, T2 item2, T3 item3, T4 item4, T5 item5, T6 item6, T7 item7, T8 item8);

		/// <summary>Returns a key range using 9 elements as a prefix</summary>
		/// <typeparam name="T1">Type of the 1st element</typeparam>
		/// <typeparam name="T2">Type of the 2nd element</typeparam>
		/// <typeparam name="T3">Type of the 3rd element</typeparam>
		/// <typeparam name="T4">Type of the 4th element</typeparam>
		/// <typeparam name="T5">Type of the 5th element</typeparam>
		/// <typeparam name="T6">Type of the 6th element</typeparam>
		/// <typeparam name="T7">Type of the 7th element</typeparam>
		/// <typeparam name="T8">Type of the 8th element</typeparam>
		/// <typeparam name="T9">Type of the 9th element</typeparam>
		/// <param name="prefix">Optional binary prefix that should be added before encoding the key</param>
		/// <param name="item1">1st element to encode</param>
		/// <param name="item2">2nd element to encode</param>
		/// <param name="item3">3rd element to encode</param>
		/// <param name="item4">4th element to encode</param>
		/// <param name="item5">5th element to encode</param>
		/// <param name="item6">6th element to encode</param>
		/// <param name="item7">7th element to encode</param>
		/// <param name="item8">8th element to encode</param>
		/// <param name="item9">8th element to encode</param>
		(Slice Begin, Slice End) ToKeyRange<
				[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T1,
				[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T2,
				[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T3,
				[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T4,
				[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T5,
				[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T6,
				[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T7,
				[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T8,
				[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T9>
			(Slice prefix, T1 item1, T2 item2, T3 item3, T4 item4, T5 item5, T6 item6, T7 item7, T8 item8, T9 item9);

		//note: I will be billing $999.99 to anyone who wants up to T11 !!! :(

		#endregion

	}

}
