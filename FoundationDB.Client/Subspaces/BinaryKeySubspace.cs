#region Copyright (c) 2023-2024 SnowBank SAS, (c) 2005-2023 Doxense SAS
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

namespace FoundationDB.Client
{
	using System.Runtime.CompilerServices;

	/// <summary>Represents a <see cref="IKeySubspace">Key Subspace</see> which can encode and decode keys as binary literals.</summary>
	[PublicAPI]
	public interface IBinaryKeySubspace : IKeySubspace
	{

		/// <summary>Return the key that is composed of the subspace prefix and a binary suffix</summary>
		/// <param name="relativeKey">Binary suffix that will be appended to the current prefix</param>
		/// <returns>Full binary key</returns>
		Slice this[Slice relativeKey] { get; }

		/// <summary>Return the key that is composed of the subspace prefix and a binary suffix</summary>
		/// <param name="relativeKey">Binary suffix that will be appended to the current prefix</param>
		/// <returns>Full binary key</returns>
		Slice this[ReadOnlySpan<byte> relativeKey] { get; }

		/// <summary>Return the last part of the key, minus the subspace prefix</summary>
		Slice Decode(Slice absoluteKey);
		//note: this is the same as calling ExtractKey(...) but is here for symmetry reasons with other kinds of subspaces

		/// <summary>Return a new subspace constructed by appending a binary suffix to the current subspace prefix</summary>
		/// <param name="relativeKey">Binary suffix that will be appended to the current prefix</param>
		/// <returns>Child subspace</returns>
		IBinaryKeySubspace Partition(ReadOnlySpan<byte> relativeKey);
	}

	/// <summary>Represents a <see cref="IKeySubspace">Key Subspace</see> which can encode and decode keys as binary literals.</summary>
	public sealed class BinaryKeySubspace : KeySubspace, IBinaryKeySubspace
	{

		internal BinaryKeySubspace(Slice prefix, ISubspaceContext context)
			: base(prefix, context)
		{ }

		internal BinaryKeySubspace(Slice prefix, KeyRange range, ISubspaceContext context)
			: base(prefix, range, context)
		{ }

		public Slice this[Slice relativeKey]
		{
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get => Append(relativeKey.Span);
		}

		public Slice this[ReadOnlySpan<byte> relativeKey]
		{
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get => Append(relativeKey);
		}

		public IBinaryKeySubspace Partition(ReadOnlySpan<byte> relativeKey)
		{
			return relativeKey.Length != 0 ? new BinaryKeySubspace(Append(relativeKey), this.Context) : this;
		}

		public Slice Encode(ReadOnlySpan<byte> relativeKey)
		{
			return Append(relativeKey);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public Slice Decode(Slice absoluteKey)
		{
			return ExtractKey(absoluteKey, boundCheck: true);
		}

	}

	public static class BinaryKeySubspaceExtensions
	{

		/// <summary>Return a new subspace constructed by appending a binary suffix to the current subspace prefix</summary>
		/// <param name="subspace">Parent subspace</param>
		/// <param name="relativeKey">Binary suffix that will be appended to the current prefix</param>
		/// <returns>Child subspace</returns>
		public static IBinaryKeySubspace Partition(this IBinaryKeySubspace subspace, Slice relativeKey)
		{
			if (relativeKey.IsNull) throw Fdb.Errors.KeyCannotBeNull(nameof(relativeKey));
			return subspace.Partition(relativeKey.Span);
		}

	}
}
