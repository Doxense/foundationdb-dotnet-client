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

	/// <summary>Type system that handles encoding and decoding of different types of values</summary>
	/// <remarks>
	/// An implementation of this interface knows to create different types of Value Encoders that will all use the same "binary format" to encode and decode values of various shapes.
	/// A good analogy for values would be a 'JSON' encoding, or 'XML' encoding.
	/// </remarks>
	/// <seealso cref="IValueEncoder{TValue,TStorage}"/>
	/// <seealso cref="IKeyEncoding">For keys</seealso>
	[PublicAPI]
	public interface IValueEncoding
	{
		/// <summary>Returns an encoder which can process values of a fixed type</summary>
		/// <typeparam name="TValue">Type of the element to encode</typeparam>
		/// <typeparam name="TStorage">Type of the encoded form of the values (Slice, string, ...)</typeparam>
		/// <returns>Value encoder</returns>
		IValueEncoder<TValue, TStorage> GetValueEncoder<TValue, TStorage>() where TValue : notnull;

		/// <summary>Returns an encoder which can process values of a fixed type</summary>
		/// <typeparam name="TValue">Type of the element to encode</typeparam>
		/// <returns>Value encoder</returns>
		IValueEncoder<TValue> GetValueEncoder<TValue>() where TValue : notnull;
		//TODO: C#8: default implementation is to return GetValueEncoder<TValue, Slice>() !

	}

}
