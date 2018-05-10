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

namespace FoundationDB.Client
{
	using System;
	using JetBrains.Annotations;

	/// <summary>Defines a type of mutation applied to a key</summary>
	[PublicAPI]
	public enum FdbMutationType
	{
		/// <summary>Invalid</summary>
		Invalid = 0,

		/// <summary>Performs an addition of little-endian integers.
		/// If the existing value in the database is not present or shorter than ``param``, it is first extended to the length of ``param`` with zero bytes.
		/// If ``param`` is shorter than the existing value in the database, the existing value is truncated to match the length of ``param``.
		/// The integers to be added must be stored in a little-endian representation.
		/// They can be signed in two's complement representation or unsigned.
		/// You can add to an integer at a known offset in the value by prepending the appropriate number of zero bytes to ``param`` and padding with zero bytes to match the length of the value.
		/// However, this offset technique requires that you know the addition will not cause the integer field within the value to overflow.
		/// </summary>
		Add = 2,

		/// <summary>Performs a bitwise ``and`` operation.
		/// If the existing value in the database is not present or shorter than ``param``, it is first extended to the length of ``param`` with zero bytes.
		/// If ``param`` is shorter than the existing value in the database, the existing value is truncated to match the length of ``param``.
		/// </summary>
		BitAnd = 6,

		/// <summary>Performs a bitwise ``or`` operation.
		/// If the existing value in the database is not present or shorter than ``param``, it is first extended to the length of ``param`` with zero bytes.
		/// If ``param`` is shorter than the existing value in the database, the existing value is truncated to match the length of ``param``.
		/// </summary>
		BitOr = 7,

		/// <summary>Performs a bitwise ``xor`` operation.
		/// If the existing value in the database is not present or shorter than ``param``, it is first extended to the length of ``param`` with zero bytes.
		/// If ``param`` is shorter than the existing value in the database, the existing value is truncated to match the length of ``param``.
		/// </summary>
		BitXor = 8,


		// Obsolete names (will be removed in the future)

		/// <summary>Deprecated name of <see cref="BitAnd"/></summary>
		[Obsolete("Use FdbMutationType.BitAnd instead")]
		And = 6,

		/// <summary>Deprecated name of <see cref="BitAnd"/></summary>
		[Obsolete("Use FdbMutationType.BitOr instead")]
		Or = 7,

		/// <summary>Deprecated name of <see cref="BitAnd"/></summary>
		[Obsolete("Use FdbMutationType.BitXor instead")]
		Xor = 8,

		/// <summary>Performs a little-endian comparison of byte strings.
		/// If the existing value in the database is not present or shorter than ``param``, it is first extended to the length of ``param`` with zero bytes.
		/// If ``param`` is shorter than the existing value in the database, the existing value is truncated to match the length of ``param``.
		/// The larger of the two values is then stored in the database.
		/// </summary>
		Max = 12,

		/// <summary>Performs a little-endian comparison of byte strings.
		/// If the existing value in the database is not present or shorter than ``param``, it is first extended to the length of ``param`` with zero bytes.
		/// If ``param`` is shorter than the existing value in the database, the existing value is truncated to match the length of ``param``.
		/// The smaller of the two values is then stored in the database.
		/// </summary>
		Min = 13,

		//TODO: XML Comments!
		VersionStampedKey = 14,

		//TODO: XML Comments!
		VersionStampedValue = 15,

	}

}
