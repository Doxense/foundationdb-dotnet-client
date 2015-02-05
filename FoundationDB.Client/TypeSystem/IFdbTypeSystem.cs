using System;
using FoundationDB.Layers.Tuples;
using JetBrains.Annotations;

namespace FoundationDB.Client //REVIEW: what namespace?
{
	/// <summary>Type system that handles values of arbitrary sizes and types</summary>
	public interface IFdbTypeSystem
	{

		FdbKeyRange ToRange(Slice key);

		/// <summary>Pack a tuple of arbitrary length into a binary slice</summary>
		/// <param name="writer">Buffer where to append the binary representation</param>
		/// <param name="items">Tuple of any size (0 to N)</param>
		/// <exception cref="System.FormatException">If some elements in <paramref name="items"/> are not supported by this type system</exception>
		void PackKey(ref SliceWriter writer, IFdbTuple items);

		/// <summary>Encode a key composed of a single element into a binary slice</summary>
		/// <typeparam name="T">Type of the element</typeparam>
		/// <param name="writer">Buffer where to append the binary representation</param>
		/// <param name="item1">Element to encode</param>
		void EncodeKey<T>(ref SliceWriter writer, T item1);

		/// <summary>Encode a key composed of two elements into a binary slice</summary>
		/// <typeparam name="T1">Type of the first element</typeparam>
		/// <typeparam name="T2">Type of the second element</typeparam>
		/// <param name="writer">Buffer where to append the binary representation</param>
		/// <param name="item1">First element to encode</param>
		/// <param name="item2">Second element to encode</param>
		void EncodeKey<T1, T2>(ref SliceWriter writer, T1 item1, T2 item2);

		/// <summary>Encode a key composed of a three elements into a binary slice</summary>
		/// <typeparam name="T1">Type of the first element</typeparam>
		/// <typeparam name="T2">Type of the second element</typeparam>
		/// <typeparam name="T3">Type of the third element</typeparam>
		/// <param name="writer">Buffer where to append the binary representation</param>
		/// <param name="item1">First element to encode</param>
		/// <param name="item2">Second element to encode</param>
		/// <param name="item3">Third element to encode</param>
		void EncodeKey<T1, T2, T3>(ref SliceWriter writer, T1 item1, T2 item2, T3 item3);

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
		void EncodeKey<T1, T2, T3, T4>(ref SliceWriter writer, T1 item1, T2 item2, T3 item3, T4 item4);

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
		void EncodeKey<T1, T2, T3, T4, T5>(ref SliceWriter writer, T1 item1, T2 item2, T3 item3, T4 item4, T5 item5);

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
		void EncodeKey<T1, T2, T3, T4, T5, T6>(ref SliceWriter writer, T1 item1, T2 item2, T3 item3, T4 item4, T5 item5, T6 item6);

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
		void EncodeKey<T1, T2, T3, T4, T5, T6, T7>(ref SliceWriter writer, T1 item1, T2 item2, T3 item3, T4 item4, T5 item5, T6 item6, T7 item7);


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
		void EncodeKey<T1, T2, T3, T4, T5, T6, T7, T8>(ref SliceWriter writer, T1 item1, T2 item2, T3 item3, T4 item4, T5 item5, T6 item6, T7 item7, T8 item8);

		/// <summary>Decode a binary slice into a tuple or arbitrary length</summary>
		/// <param name="packed">Binary slice produced by a previous call to <see cref="PackKey"/></param>
		/// <returns>Tuple of any size (0 to N)</returns>
		IFdbTuple UnpackKey(Slice packed);

		/// <summary>Decode a binary slice containing exactly on element</summary>
		/// <typeparam name="T">Expected type of the element</typeparam>
		/// <param name="packed">Binary slice produced by a previous call to <see cref="PackKey"/> or <see cref="EncodeKey{T1}"/></param>
		/// <returns>Tuple containing a single element, or an exception if the data is invalid, or the tuples has less or more than 1 element</returns>
		T DecodeKey<T>(Slice packed);

		T DecodeKeyFirst<T>(Slice packed);

		T DecodeKeyLast<T>(Slice packed);

		/// <summary>Decode a binary slice containing exactly two elements</summary>
		/// <typeparam name="T1">Expected type of the first element</typeparam>
		/// <typeparam name="T2">Expected type of the second element</typeparam>
		/// <param name="packed">Binary slice produced by a previous call to <see cref="PackKey"/> or <see cref="EncodeKey{T1, T2}"/></param>
		/// <returns>Tuple containing two elements, or an exception if the data is invalid, or the tuples has less or more than two elements</returns>
		FdbTuple<T1, T2> DecodeKey<T1, T2>(Slice packed);

		/// <summary>Decode a binary slice containing exactly three elements</summary>
		/// <typeparam name="T1">Expected type of the first element</typeparam>
		/// <typeparam name="T2">Expected type of the second element</typeparam>
		/// <typeparam name="T3">Expected type of the third element</typeparam>
		/// <param name="packed">Binary slice produced by a previous call to <see cref="PackKey"/> or <see cref="EncodeKey{T1, T2, T3}"/></param>
		/// <returns>Tuple containing three elements, or an exception if the data is invalid, or the tuples has less or more than three elements</returns>
		FdbTuple<T1, T2, T3> DecodeKey<T1, T2, T3>(Slice packed);

		/// <summary>Decode a binary slice containing exactly four elements</summary>
		/// <typeparam name="T1">Expected type of the first element</typeparam>
		/// <typeparam name="T2">Expected type of the second element</typeparam>
		/// <typeparam name="T3">Expected type of the third element</typeparam>
		/// <typeparam name="T4">Expected type of the fourth element</typeparam>
		/// <param name="packed">Binary slice produced by a previous call to <see cref="PackKey"/> or <see cref="EncodeKey{T1, T2, T3, T4}"/></param>
		/// <returns>Tuple containing four elements, or an exception if the data is invalid, or the tuples has less or more than four elements</returns>
		FdbTuple<T1, T2, T3, T4> DecodeKey<T1, T2, T3, T4>(Slice packed);

		/// <summary>Decode a binary slice containing exactly five elements</summary>
		/// <typeparam name="T1">Expected type of the first element</typeparam>
		/// <typeparam name="T2">Expected type of the second element</typeparam>
		/// <typeparam name="T3">Expected type of the third element</typeparam>
		/// <typeparam name="T4">Expected type of the fourth element</typeparam>
		/// <typeparam name="T5">Expected type of the fifth element</typeparam>
		/// <param name="packed">Binary slice produced by a previous call to <see cref="PackKey"/> or <see cref="EncodeKey{T1, T2, T3, T4, T5}"/></param>
		/// <returns>Tuple containing five elements, or an exception if the data is invalid, or the tuples has less or more than five elements</returns>
		FdbTuple<T1, T2, T3, T4, T5> DecodeKey<T1, T2, T3, T4, T5>(Slice packed);

	}

}