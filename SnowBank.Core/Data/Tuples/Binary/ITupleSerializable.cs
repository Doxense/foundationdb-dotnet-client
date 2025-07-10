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
	using SnowBank.Buffers.Text;

	/// <summary>Represents an object that can serialize itself using the Tuple Binary Encoding format</summary>
	[PublicAPI]
	public interface ITuplePackable
	{
		/// <summary>Appends the packed bytes of this instance to the end of a buffer</summary>
		/// <param name="writer">Buffer that will receive the packed bytes of this instance</param>
		void PackTo(TupleWriter writer);

	}

	[Obsolete("Replace with ITuplePackable and optionally implement ITupleSpanPackable as well")]
	public interface ITupleSerializable : ITuplePackable
	{
		//TODO: remove this once we know all uses have been replaced
	}

	/// <summary>Represents an object that can serialize itself using the Tuple Binary Encoding format</summary>
	public interface ITupleSpanPackable : ITuplePackable
	{
		/// <summary>Appends the packed bytes of this instance to the end of a buffer, if it is large enough</summary>
		/// <param name="writer">Buffer that will receive the packed bytes of this instance</param>
		/// <remarks><c>true</c> if the operation was successful, or <c>false</c> if it was too small</remarks>
		/// <remarks>
		/// <para>If this method returns <c>false</c>, the caller should retry with a larger buffer.</para>
		/// <para>Implementors of this method should ONLY return <c>false</c> when the buffer is too small, and <b>MUST</b> throw exceptions if the data is invalid, or cannot be formatted even with a larger buffer. Failure to do so may cause an infinite loop!</para>
		/// </remarks>
		bool TryPackTo(ref TupleSpanWriter writer);
	}

	/// <summary>Represents a tuple that can formats its item into a string representation</summary>
	[PublicAPI]
	public interface ITupleFormattable
	{

		/// <summary>Writes the string representation of the items of this tuple</summary>
		/// <param name="sb">Output buffer</param>
		/// <returns>Number of items written</returns>
		/// <remarks>This method should not emit any <c>'('</c> or <c>')'</c> delimiters, and only adds <c>", "</c> internal separators</remarks>
		int AppendItemsTo(ref FastStringBuilder sb);

	}

	/// <summary>Represents an object that can serialize or deserialize tuples of type <typeparamref name="TTuple"/>, using the Tuple Binary Encoding format</summary>
	/// <typeparam name="TTuple">Type of tuples that can be processed by this instance</typeparam>
	[PublicAPI]
	public interface ITupleSerializer<TTuple> //REVIEW: ITuplePacker<T> ?
		where TTuple : IVarTuple
	{
		/// <summary>Appends the packed bytes of an item to the end of a buffer</summary>
		/// <param name="writer">Buffer that will receive the packed bytes of this instance</param>
		/// <param name="tuple">Tuple that will be packed</param>
		void PackTo(TupleWriter writer, in TTuple tuple);

		/// <summary>Decode the packed bytes from a buffer, and return the corresponding item</summary>
		/// <param name="reader">Buffer that contains the bytes to decode</param>
		/// <param name="tuple">Receives the decoded tuple</param>
		/// <returns>
		/// The value of <paramref name="reader"/> will be updated to point to either the end of the buffer, or the next "element" if there are more bytes available.
		/// </returns>
		[Pure]
		void UnpackFrom(ref TupleReader reader, out TTuple tuple);

	}

}
