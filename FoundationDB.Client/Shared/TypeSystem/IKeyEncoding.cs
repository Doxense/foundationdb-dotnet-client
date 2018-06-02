#region BSD Licence
/* Copyright (c) 2013-2018, Doxense SAS
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
	using JetBrains.Annotations;

	/// <summary>Type system that handles encoding and decoding of differnt types of keys</summary>
	/// <remarks>
	/// An implementation of this interface knows to create different types of Key Encoders that will all use the same "binary format" to encode and decode keys of various shapes.
	/// A good analogy for values would be a 'JSON' encoding, or 'XML' encoding.
	/// </remarks>
	public interface IKeyEncoding //REVIEW: rename to "IKeyEncodingScheme"? "IKeyTypeSystem"?
	{

		/// <summary>Returns an encoder which can process keys of any size and types</summary>
		/// <returns>Encoder that encodes dynamic keys</returns>
		/// <exception cref="NotSupportedException">If this encoding does not support dynamic keys</exception>
		[NotNull]
		IDynamicKeyEncoder GetDynamicKeyEncoder();

		/// <summary>Returns an encoder which can process keys composed of a single element of a fixed type</summary>
		/// <typeparam name="T1">Type of the element to encode</typeparam>
		/// <returns>Key encoder</returns>
		/// <exception cref="NotSupportedException">If this encoding does not support static keys</exception>
		[NotNull]
		IKeyEncoder<T1> GetKeyEncoder<T1>();

		/// <summary>Returns an encoder which can process keys composed of a two elements of fixed types</summary>
		/// <typeparam name="T1">Type of the first element to encode</typeparam>
		/// <typeparam name="T2">Type of the second element to encode</typeparam>
		/// <returns>Composite key encoder</returns>
		/// <exception cref="NotSupportedException">If this encoding does not support static keys of size 2</exception>
		[NotNull]
		ICompositeKeyEncoder<T1, T2> GetKeyEncoder<T1, T2>();

		/// <summary>Returns an encoder which can process keys composed of a three elements of fixed types</summary>
		/// <typeparam name="T1">Type of the first element to encode</typeparam>
		/// <typeparam name="T2">Type of the second element to encode</typeparam>
		/// <typeparam name="T3">Type of the third element to encode</typeparam>
		/// <returns>Composite key encoder</returns>
		/// <exception cref="NotSupportedException">If this encoding does not support static keys of size 3</exception>
		[NotNull]
		ICompositeKeyEncoder<T1, T2, T3> GetKeyEncoder<T1, T2, T3>();

		/// <summary>Returns an encoder which can process keys composed of a four elements of fixed types</summary>
		/// <typeparam name="T1">Type of the first element to encode</typeparam>
		/// <typeparam name="T2">Type of the second element to encode</typeparam>
		/// <typeparam name="T3">Type of the third element to encode</typeparam>
		/// <typeparam name="T4">Type of the fourth element to encode</typeparam>
		/// <returns>Composite key encoder</returns>
		/// <exception cref="NotSupportedException">If this encoding does not support static keys of size 4</exception>
		[NotNull]
		ICompositeKeyEncoder<T1, T2, T3, T4> GetKeyEncoder<T1, T2, T3, T4>();

	}
}

#endif
