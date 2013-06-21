#region BSD Licence
/* Copyright (c) 2013, Doxense SARL
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

using FoundationDb.Client;
using FoundationDb.Client.Utils;
using System;
using System.Collections.Generic;

namespace FoundationDb.Layers.Tuples
{

	public interface IFdbTuple : IEnumerable<object>, IEquatable<IFdbTuple>
	{

		/// <summary>Write the binary representation of this key at the end of a buffer</summary>
		/// <param name="buffer">Buffer that will received the packed bytes of this key</param>
		void PackTo(FdbBufferWriter writer);

		/// <summary>Return the value of the key as a byte buffer</summary>
		Slice ToSlice();

		/// <summary>Returns the number of "items" in the Tuple</summary>
		int Count { get; }

		/// <summary>Return an item of the tuple, given its position</summary>
		/// <param name="index">Position of the item (if negative, means relative from the end)</param>
		/// <returns>Value of the item</returns>
		/// <exception cref="System.IndexOutOfRangeException">If index if outside the bounds of the tuple</exception>
		/// <example>
		/// ("Foo", "Bar", 123,)[0] => "Foo"
		/// ("Foo", "Bar", 123,)[-1] => 123
		/// </example>
		object this[int index] { get; }

		/// <summary>Return a section of the tuple</summary>
		/// <param name="start">Starting offset of the sub-tuple to return, or null to select from the start. Negative values means from the end</param>
		/// <param name="end">Ending offset of the sub-tuple to return or null to select until the end. Negative values means from the end</param>
		/// <returns>Tuple that only includes the selected items</returns>
		IFdbTuple this[int? start, int? end] { get; }

		T Get<T>(int index);

		/// <summary>Create a new Tuple by appending a new value at the end the this tuple</summary>
		/// <typeparam name="T">Type of the new value</typeparam>
		/// <param name="value">Value that will be appended at the end</param>
		/// <returns>New tuple with the new value</returns>
		IFdbTuple Append<T>(T value);

		/// <summary>Copy all items of the tuple into an array at a specific location</summary>
		/// <param name="array">Destination array (must be big enough to contains all the items)</param>
		/// <param name="offset">Offset at wich to start copying items</param>
		void CopyTo(object[] array, int offset);
	}

}
