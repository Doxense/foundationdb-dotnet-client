#region Copyright (c) 2013-2016, Doxense SAS. All rights reserved.
// See License.MD for license information
#endregion

namespace Doxense.Collections.Tuples.Encoding
{
	using System;
	using JetBrains.Annotations;

	/// <summary>Represents an object that can serialize itself using the Tuple Binary Encoding format</summary>
	public interface ITupleSerializable //REVIEW: ITuplePackable?
	{
		/// <summary>Appends the packed bytes of this instance to the end of a buffer</summary>
		/// <param name="writer">Buffer that will received the packed bytes of this instance</param>
		void PackTo(ref TupleWriter writer);

		//note: there is not UnpackFrom, because it does not play way with constructors and readonly fields!
		// => use ITupleSerializer<T> for this!
	}

	/// <summary>Represents an object that can serialize or deserialize tuples of type <typeparamref name="TTuple"/>, using the Tuple Binary Encoding format</summary>
	/// <typeparam name="TTuple">Type of tuples that can be processed by this instance</typeparam>
	public interface ITupleSerializer<TTuple> //REVIEW: ITuplePacker<T> ?
		where TTuple : ITuple
	{
		/// <summary>Appends the packed bytes of an item to the end of a buffer</summary>
		/// <param name="writer">Buffer that will received the packed bytes of this instance</param>
		/// <param name="tuple">Tuple that will be packed</param>
		void PackTo(ref TupleWriter writer, ref TTuple tuple);

		/// <summary>Decode the packed bytes from a buffer, and return the corresponding item</summary>
		/// <param name="reader">Buffer that contains the bytes the decode</param>
		/// <param name="tuple">Receives the decoded tuple</param>
		/// <returns>
		/// The value of <paramref name="reader"/> will be updated to point to either the end of the buffer, or the next "element" if there are more bytes available.
		/// </returns>
		[Pure]
		void UnpackFrom(ref TupleReader reader, out TTuple tuple);

	}
}
