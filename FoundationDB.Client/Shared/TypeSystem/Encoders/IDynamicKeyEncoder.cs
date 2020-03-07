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

#if !USE_SHARED_FRAMEWORK

namespace Doxense.Serialization.Encoders
{
	using System;
	using System.Diagnostics.CodeAnalysis;
	using Doxense.Collections.Tuples;
	using Doxense.Memory;
	using JetBrains.Annotations;

	/// <summary>Encoder that can process keys of variable size and types</summary>
	[PublicAPI]
	public interface IDynamicKeyEncoder : IKeyEncoder
	{

		new IDynamicKeyEncoding Encoding { get; }

		#region Encoding...

		/// <summary>Pack a tuple of arbitrary length into a binary slice</summary>
		/// <param name="writer">Buffer where to append the binary representation</param>
		/// <param name="items">Tuple of any size (0 to N)</param>
		/// <exception cref="System.FormatException">If some elements in <paramref name="items"/> are not supported by this type system</exception>
		void PackKey<TTuple>(ref SliceWriter writer, TTuple items) where TTuple : IVarTuple;

		/// <summary>Encode a key composed of a single element into a binary slice</summary>
		/// <typeparam name="T">Type of the element</typeparam>
		/// <param name="writer">Buffer where to append the binary representation</param>
		/// <param name="item1">Element to encode</param>
		void EncodeKey<T>(ref SliceWriter writer, [AllowNull] T item1);

		/// <summary>Encode a key composed of two elements into a binary slice</summary>
		/// <typeparam name="T1">Type of the first element</typeparam>
		/// <typeparam name="T2">Type of the second element</typeparam>
		/// <param name="writer">Buffer where to append the binary representation</param>
		/// <param name="item1">First element to encode</param>
		/// <param name="item2">Second element to encode</param>
		void EncodeKey<T1, T2>(ref SliceWriter writer, [AllowNull] T1 item1, [AllowNull] T2 item2);

		/// <summary>Encode a key composed of a three elements into a binary slice</summary>
		/// <typeparam name="T1">Type of the first element</typeparam>
		/// <typeparam name="T2">Type of the second element</typeparam>
		/// <typeparam name="T3">Type of the third element</typeparam>
		/// <param name="writer">Buffer where to append the binary representation</param>
		/// <param name="item1">First element to encode</param>
		/// <param name="item2">Second element to encode</param>
		/// <param name="item3">Third element to encode</param>
		void EncodeKey<T1, T2, T3>(ref SliceWriter writer, [AllowNull] T1 item1, [AllowNull] T2 item2, [AllowNull] T3 item3);

		/// <summary>Encode a key composed of a four elements into a binary slice</summary>
		/// <typeparam name="T1">Type of the first element</typeparam>
		/// <typeparam name="T2">Type of the second element</typeparam>
		/// <typeparam name="T3">Type of the third element</typeparam>
		/// <typeparam name="T4">Type of the fourth element</typeparam>
		/// <param name="writer">Buffer where to append the binary representation</param>
		/// <param name="item1">First element to encode</param>
		/// <param name="item2">Second element to encode</param>
		/// <param name="item3">Third element to encode</param>
		/// <param name="item4">Fourth element to encode</param>
		void EncodeKey<T1, T2, T3, T4>(ref SliceWriter writer, [AllowNull] T1 item1, [AllowNull] T2 item2, [AllowNull] T3 item3, [AllowNull] T4 item4);

		/// <summary>Encode a key composed of a four elements into a binary slice</summary>
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
		void EncodeKey<T1, T2, T3, T4, T5>(ref SliceWriter writer, [AllowNull] T1 item1, [AllowNull] T2 item2, [AllowNull] T3 item3, [AllowNull] T4 item4, [AllowNull] T5 item5);

		/// <summary>Encode a key composed of a four elements into a binary slice</summary>
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
		void EncodeKey<T1, T2, T3, T4, T5, T6>(ref SliceWriter writer, [AllowNull] T1 item1, [AllowNull] T2 item2, [AllowNull] T3 item3, [AllowNull] T4 item4, [AllowNull] T5 item5, [AllowNull] T6 item6);

		/// <summary>Encode a key composed of a four elements into a binary slice</summary>
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
		void EncodeKey<T1, T2, T3, T4, T5, T6, T7>(ref SliceWriter writer, [AllowNull] T1 item1, [AllowNull] T2 item2, [AllowNull] T3 item3, [AllowNull] T4 item4, [AllowNull] T5 item5, [AllowNull] T6 item6, [AllowNull] T7 item7);

		/// <summary>Encode a key composed of a four elements into a binary slice</summary>
		/// <typeparam name="T1">Type of the first element</typeparam>
		/// <typeparam name="T2">Type of the second element</typeparam>
		/// <typeparam name="T3">Type of the third element</typeparam>
		/// <typeparam name="T4">Type of the fourth element</typeparam>
		/// <typeparam name="T5">Type of the fifth element</typeparam>
		/// <typeparam name="T6">Type of the sixth element</typeparam>
		/// <typeparam name="T7">Type of the seventh element</typeparam>
		/// <typeparam name="T8">Type of the eighth element</typeparam>
		/// <param name="writer">Buffer where to append the binary representation</param>
		/// <param name="item1">First element to encode</param>
		/// <param name="item2">Second element to encode</param>
		/// <param name="item3">Third element to encode</param>
		/// <param name="item4">Fourth element to encode</param>
		/// <param name="item5">Fifth element to encode</param>
		/// <param name="item6">Sixth element to encode</param>
		/// <param name="item7">Seventh element to encode</param>
		/// <param name="item8">Eighth element to encode</param>
		void EncodeKey<T1, T2, T3, T4, T5, T6, T7, T8>(ref SliceWriter writer, [AllowNull] T1 item1, [AllowNull] T2 item2, [AllowNull] T3 item3, [AllowNull] T4 item4, [AllowNull] T5 item5, [AllowNull] T6 item6, [AllowNull] T7 item7, [AllowNull] T8 item8);

		#endregion

		#region Decoding...

		/// <summary>Decode a binary slice into a tuple of arbitrary length</summary>
		/// <param name="packed">Binary slice produced by a previous call to <see cref="PackKey{TTuple}"/></param>
		/// <returns>Tuple of any size (0 to N)</returns>
		IVarTuple UnpackKey(Slice packed);

		/// <summary>Attempt to decode a binary slice into a tuple of arbitrary length</summary>
		/// <param name="packed">Binary slice produced by a previous call to <see cref="PackKey{TTuple}"/></param>
		/// <param name="tuple">Tuple of any size (0 to N), if the method returns true</param>
		/// <returns>True if <paramref name="packed"/> was a legal binary representation; otherwise, false.</returns>
		bool TryUnpackKey(Slice packed, [NotNullWhen(true)] out IVarTuple? tuple);

		/// <summary>Decode a binary slice containing exactly on element</summary>
		/// <typeparam name="T">Expected type of the element</typeparam>
		/// <param name="packed">Binary slice produced by a previous call to <see cref="EncodeKey{T1}"/> or <see cref="EncodeKey{T1}"/></param>
		/// <returns>Tuple containing a single element, or an exception if the data is invalid, or the tuples has less or more than 1 element</returns>
		T DecodeKey<T>(Slice packed);

		T DecodeKeyFirst<T>(Slice packed);

		T DecodeKeyLast<T>(Slice packed);

		/// <summary>Decode a binary slice containing exactly two elements</summary>
		/// <typeparam name="T1">Expected type of the first element</typeparam>
		/// <typeparam name="T2">Expected type of the second element</typeparam>
		/// <param name="packed">Binary slice produced by a previous call to <see cref="EncodeKey{T1, T2}"/> or <see cref="EncodeKey{T1, T2}"/></param>
		/// <returns>Tuple containing two elements, or an exception if the data is invalid, or the tuples has less or more than two elements</returns>
		(T1, T2) DecodeKey<T1, T2>(Slice packed);

		/// <summary>Decode a binary slice containing exactly three elements</summary>
		/// <typeparam name="T1">Expected type of the first element</typeparam>
		/// <typeparam name="T2">Expected type of the second element</typeparam>
		/// <typeparam name="T3">Expected type of the third element</typeparam>
		/// <param name="packed">Binary slice produced by a previous call to <see cref="EncodeKey{T1, T2, T3}"/> or <see cref="EncodeKey{T1, T2, T3}"/></param>
		/// <returns>Tuple containing three elements, or an exception if the data is invalid, or the tuples has less or more than three elements</returns>
		(T1, T2, T3) DecodeKey<T1, T2, T3>(Slice packed);

		/// <summary>Decode a binary slice containing exactly four elements</summary>
		/// <typeparam name="T1">Expected type of the first element</typeparam>
		/// <typeparam name="T2">Expected type of the second element</typeparam>
		/// <typeparam name="T3">Expected type of the third element</typeparam>
		/// <typeparam name="T4">Expected type of the fourth element</typeparam>
		/// <param name="packed">Binary slice produced by a previous call to <see cref="EncodeKey{T1, T2, T3, T4}"/> or <see cref="EncodeKey{T1, T2, T3, T4}"/></param>
		/// <returns>Tuple containing four elements, or an exception if the data is invalid, or the tuples has less or more than four elements</returns>
		(T1, T2, T3, T4) DecodeKey<T1, T2, T3, T4>(Slice packed);

		/// <summary>Decode a binary slice containing exactly five elements</summary>
		/// <typeparam name="T1">Expected type of the first element</typeparam>
		/// <typeparam name="T2">Expected type of the second element</typeparam>
		/// <typeparam name="T3">Expected type of the third element</typeparam>
		/// <typeparam name="T4">Expected type of the fourth element</typeparam>
		/// <typeparam name="T5">Expected type of the fifth element</typeparam>
		/// <param name="packed">Binary slice produced by a previous call to <see cref="EncodeKey{T1, T2, T3, T4, T5}"/> or <see cref="EncodeKey{T1, T2, T3, T4, T5}"/></param>
		/// <returns>Tuple containing five elements, or an exception if the data is invalid, or the tuples has less or more than five elements</returns>
		(T1, T2, T3, T4, T5) DecodeKey<T1, T2, T3, T4, T5>(Slice packed);

		/// <summary>Decode a binary slice containing exactly six elements</summary>
		/// <typeparam name="T1">Expected type of the first element</typeparam>
		/// <typeparam name="T2">Expected type of the second element</typeparam>
		/// <typeparam name="T3">Expected type of the third element</typeparam>
		/// <typeparam name="T4">Expected type of the fourth element</typeparam>
		/// <typeparam name="T5">Expected type of the fifth element</typeparam>
		/// <typeparam name="T6">Expected type of the sixth element</typeparam>
		/// <param name="packed">Binary slice produced by a previous call to <see cref="EncodeKey{T1, T2, T3, T4, T5, T6}"/> or <see cref="EncodeKey{T1, T2, T3, T4, T5, T6}"/></param>
		/// <returns>Tuple containing five elements, or an exception if the data is invalid, or the tuples has less or more than five elements</returns>
		(T1, T2, T3, T4, T5, T6) DecodeKey<T1, T2, T3, T4, T5, T6>(Slice packed);

		#endregion

		#region Ranges...

		/// <summary>Return a range that contains all the keys under a subspace of the encoder subspace, using the semantic of the encoding</summary>
		/// <param name="prefix">Optional binary prefix</param>
		/// <returns>Key range which derives from the semantic of the current encoding</returns>
		/// <remarks>For example, the Tuple encoding will produce ranges of the form "(Key + \x00) &lt;= x &lt; (Key + \xFF)", while a binary-based encoding would produce ranges of the form "Key &lt;= x &lt; Increment(Key)"</remarks>
		(Slice Begin, Slice End) ToRange(Slice prefix = default);

		/// <summary>Return a key range using a tuple as a prefix</summary>
		/// <param name="prefix">Optional binary prefix that should be added before encoding the key</param>
		/// <param name="items">Tuple of any size (0 to N)</param>
		(Slice Begin, Slice End) ToRange<TTuple>(Slice prefix, TTuple items) where TTuple : IVarTuple;

		/// <summary>Return a key range using a single element as a prefix</summary>
		/// <typeparam name="T1">Type of the element</typeparam>
		/// <param name="prefix">Optional binary prefix that should be added before encoding the key</param>
		/// <param name="item1">Element to encode</param>
		(Slice Begin, Slice End) ToKeyRange<T1>(Slice prefix, [AllowNull] T1 item1);

		/// <summary>Return a key range using two elements as a prefix</summary>
		/// <typeparam name="T1">Type of the first element</typeparam>
		/// <typeparam name="T2">Type of the second element</typeparam>
		/// <param name="prefix">Optional binary prefix that should be added before encoding the key</param>
		/// <param name="item1">First element to encode</param>
		/// <param name="item2">Second element to encode</param>
		(Slice Begin, Slice End) ToKeyRange<T1, T2>(Slice prefix, [AllowNull] T1 item1, [AllowNull] T2 item2);

		/// <summary>Return a key range using three elements as a prefix</summary>
		/// <typeparam name="T1">Type of the first element</typeparam>
		/// <typeparam name="T2">Type of the second element</typeparam>
		/// <typeparam name="T3">Type of the third element</typeparam>
		/// <param name="prefix">Optional binary prefix that should be added before encoding the key</param>
		/// <param name="item1">First element to encode</param>
		/// <param name="item2">Second element to encode</param>
		/// <param name="item3">Third element to encode</param>
		(Slice Begin, Slice End) ToKeyRange<T1, T2, T3>(Slice prefix, [AllowNull] T1 item1, [AllowNull] T2 item2, [AllowNull] T3 item3);

		/// <summary>Return a key range using four elements as a prefix</summary>
		/// <typeparam name="T1">Type of the first element</typeparam>
		/// <typeparam name="T2">Type of the second element</typeparam>
		/// <typeparam name="T3">Type of the third element</typeparam>
		/// <typeparam name="T4">Type of the fourth element</typeparam>
		/// <param name="prefix">Optional binary prefix that should be added before encoding the key</param>
		/// <param name="item1">First element to encode</param>
		/// <param name="item2">Second element to encode</param>
		/// <param name="item3">Third element to encode</param>
		/// <param name="item4">Fourth element to encode</param>
		(Slice Begin, Slice End) ToKeyRange<T1, T2, T3, T4>(Slice prefix, [AllowNull] T1 item1, [AllowNull] T2 item2, [AllowNull] T3 item3, [AllowNull] T4 item4);

		/// <summary>Return a key range using five elements as a prefix</summary>
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
		(Slice Begin, Slice End) ToKeyRange<T1, T2, T3, T4, T5>(Slice prefix, [AllowNull] T1 item1, [AllowNull] T2 item2, [AllowNull] T3 item3, [AllowNull] T4 item4, [AllowNull] T5 item5);

		/// <summary>Return a key range using six elements as a prefix</summary>
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
		(Slice Begin, Slice End) ToKeyRange<T1, T2, T3, T4, T5, T6>(Slice prefix, [AllowNull] T1 item1, [AllowNull] T2 item2, [AllowNull] T3 item3, [AllowNull] T4 item4, [AllowNull] T5 item5, [AllowNull] T6 item6);

		/// <summary>Return a key range using seven elements as a prefix</summary>
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
		(Slice Begin, Slice End) ToKeyRange<T1, T2, T3, T4, T5, T6, T7>(Slice prefix, [AllowNull] T1 item1, [AllowNull] T2 item2, [AllowNull] T3 item3, [AllowNull] T4 item4, [AllowNull] T5 item5, [AllowNull] T6 item6, [AllowNull] T7 item7);

		/// <summary>Return a key range using eight elements as a prefix</summary>
		/// <typeparam name="T1">Type of the first element</typeparam>
		/// <typeparam name="T2">Type of the second element</typeparam>
		/// <typeparam name="T3">Type of the third element</typeparam>
		/// <typeparam name="T4">Type of the fourth element</typeparam>
		/// <typeparam name="T5">Type of the fifth element</typeparam>
		/// <typeparam name="T6">Type of the sixth element</typeparam>
		/// <typeparam name="T7">Type of the seventh element</typeparam>
		/// <typeparam name="T8">Type of the eighth element</typeparam>
		/// <param name="prefix">Optional binary prefix that should be added before encoding the key</param>
		/// <param name="item1">First element to encode</param>
		/// <param name="item2">Second element to encode</param>
		/// <param name="item3">Third element to encode</param>
		/// <param name="item4">Fourth element to encode</param>
		/// <param name="item5">Fifth element to encode</param>
		/// <param name="item6">Sixth element to encode</param>
		/// <param name="item7">Seventh element to encode</param>
		/// <param name="item8">Eighth element to encode</param>
		(Slice Begin, Slice End) ToKeyRange<T1, T2, T3, T4, T5, T6, T7, T8>(Slice prefix, [AllowNull] T1 item1, [AllowNull] T2 item2, [AllowNull] T3 item3, [AllowNull] T4 item4, [AllowNull] T5 item5, [AllowNull] T6 item6, [AllowNull] T7 item7, [AllowNull] T8 item8);

		//note: I will be billing $999.99 to anyone who wants up to T11 !!! :(

		#endregion

	}

}

#endif
