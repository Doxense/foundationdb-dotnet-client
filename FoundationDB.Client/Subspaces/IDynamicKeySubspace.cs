#region BSD License
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
	using Doxense.Serialization.Encoders;
	using JetBrains.Annotations;

	/// <summary>Represents a <see cref="IKeySubspace">Key Subspace</see> which can encode and decode keys of arbitrary size.</summary>
	/// <remarks>This is usefull when dealing with subspaces that store keys of different types and shapes.</remarks>
	/// <example>In pseudo code, we obtain a dynamic subspace that wraps a prefix, and uses the <see cref="Doxense.Collections.Tuples.TuPack">Tuple Encoder Format</see> to encode variable-size tuples into binary:
	/// <code>
	/// subspace = {...}.OpenOrCreate(..., "/some/path/to/data", TypeSystem.Tuples)
	/// subspace.GetPrefix() => {prefix}
	/// subspace.Keys.Pack(("Hello", "World")) => (PREFIX, 'Hello', 'World') => {prefix}.'\x02Hello\x00\x02World\x00'
	/// subspace.Keys.Encode("Hello", "World") => (PREFIX, 'Hello', 'World') => {prefix}.'\x02Hello\x00\x02World\x00'
	/// subspace.Keys.Decode({prefix}'\x02Hello\x00\x15\x42') => ('Hello', 0x42)
	/// </code>
	/// </example>
	[PublicAPI]
	public interface IDynamicKeySubspace : IKeySubspace
	{

		/// <summary>View of the keys of this subspace</summary>
		[NotNull]
		DynamicKeys Keys { get; }

		/// <summary>Returns an helper object that knows how to create sub-partitions of this subspace</summary>
		[NotNull]
		DynamicPartition Partition { get; }

		/// <summary>Encoding used to generate and parse the keys of this subspace</summary>
		[NotNull] IKeyEncoding Encoding { get; }

	}
}
