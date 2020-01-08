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

namespace FoundationDB.Client
{
	using System;
	using System.Runtime.CompilerServices;
	using JetBrains.Annotations;

	/// <summary>Represents a <see cref="IKeySubspace">Key Subspace</see> which can encode and decode keys as binary literals.</summary>
	public interface IBinaryKeySubspace : IKeySubspace
	{

		/// <summary>Return the key that is composed of the subspace's prefix and a binary suffix</summary>
		/// <param name="relativeKey">Binary suffix that will be appended to the current prefix</param>
		/// <returns>Full binary key</returns>
		Slice this[Slice relativeKey] { get; }

		/// <summary>Return the last part of the key, minus the subspace's prefix</summary>
		Slice Decode(Slice absoluteKey);
		//note: this is the same as calling ExtractKey(...) but is here for symmetry reasons with other kinds of subspaces

		/// <summary>Return a new subspace constructed by appending a binary suffix to the current subspace's prefix</summary>
		/// <param name="relativeKey">Binary suffix that will be appended to the current prefix</param>
		/// <returns>Child subspace</returns>
		IBinaryKeySubspace Partition(Slice relativeKey);
	}

	/// <summary>Represents a <see cref="IKeySubspace">Key Subspace</see> which can encode and decode keys as binary literals.</summary>
	public sealed class BinaryKeySubspace : KeySubspace, IBinaryKeySubspace
	{

		internal BinaryKeySubspace(Slice prefix, [NotNull] ISubspaceContext context)
			: base(prefix, context)
		{ }

		internal BinaryKeySubspace(Slice prefix, KeyRange range, [NotNull] ISubspaceContext context)
			: base(prefix, range, context)
		{ }

		public Slice this[Slice relativeKey]
		{
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get => Append(relativeKey);
		}

		public IBinaryKeySubspace Partition(Slice relativeKey)
		{
			return relativeKey.Count != 0 ? new BinaryKeySubspace(Append(relativeKey), this.Context) : this;
		}

		public Slice Encode(Slice relativeKey)
		{
			return Append(relativeKey);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public Slice Decode(Slice absoluteKey)
		{
			return ExtractKey(absoluteKey, boundCheck: true);
		}

	}
}
