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

using FoundationDB.Client;
using FoundationDB.Client.Utils;
using System;

namespace FoundationDB.Layers.Tuples
{

	/// <summary>Helper class that can serialize values of type <typeparamref name="T"/> to the tuple binary format</summary>
	/// <typeparam name="T">Type of values to be serialized</typeparam>
	public static class FdbTuplePacker<T>
	{

		internal static readonly Action<FdbBufferWriter, T> Serializer;

		static FdbTuplePacker()
		{
			Serializer = FdbTuplePackers.GetSerializer<T>();
			//REVIEW: bad practice to throw in static ctor...
			if (Serializer == null) throw new InvalidOperationException(String.Format("Does not know how to serialize values of type {0} into keys", typeof(T).Name));
		}

		/// <summary>Serialize a <typeparamref name="T"/> into a binary buffer</summary>
		/// <param name="writer">Target buffer</param>
		/// <param name="value">Value that will be serialized</param>
		/// <remarks>The buffer does not need to be preallocated.</remarks>
		public static void SerializeTo(FdbBufferWriter writer, T value)
		{
			FdbTuplePacker<T>.Serializer(writer, value);
		}

		/// <summary>Serialize a <typeparamref name="T"/> into a slices</summary>
		/// <param name="value">Value that will be serialized</param>
		/// <returns>Slice that contains the binary representation of <paramref name="value"/></returns>
		public static Slice Serialize(T value)
		{
			var writer = new FdbBufferWriter();
			Serializer(writer, value);
			return writer.ToSlice();
		}

	}

}
