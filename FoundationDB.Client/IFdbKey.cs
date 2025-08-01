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

namespace FoundationDB.Client
{

	/// <summary>Represents a key in the database, that can be serialized into bytes</summary>
	/// <remarks>
	/// <para>Types that implement this interface usually wrap a parent subspace, as well as the elements that make up a key inside this subspace.</para>
	/// <para>For example, a <see cref="FdbTupleKey{string,int}"/> will wrap the items <c>("hello", 123)</c>, which will be later converted into bytes using the Tuple Encoding</para>
	/// <para>Keys that are reused multiple times can be converted into a <see cref="FdbRawKey"/> using the <see cref="FdbKeyHelpers.Memoize{TKey}"/> method, which wraps the complete key in a <see cref="Slice"/>.</para>
	/// </remarks>
	public interface IFdbKey : ISpanEncodable, ISpanFormattable
		, IEquatable<FdbRawKey>, IComparable<FdbRawKey>
		, IEquatable<Slice>, IComparable<Slice>
		, IEquatable<IFdbKey>
		, IComparable
#if NET9_0_OR_GREATER
		, IEquatable<ReadOnlySpan<byte>>, IComparable<ReadOnlySpan<byte>>
#endif
	{

		/// <summary>Optional subspace that contains this key</summary>
		/// <remarks>If <c>null</c>, this is most probably a key that as already been encoded.</remarks>
		[Pure]
		IKeySubspace? GetSubspace();

		/// <inheritdoc cref="IEquatable{T}.Equals(T?)"/>
		[Pure]
		bool FastEqualTo<TOtherKey>(in TOtherKey key) where TOtherKey : struct, IFdbKey;

		/// <inheritdoc cref="IComparable{T}.CompareTo(T?)"/>
		[Pure]
		int FastCompareTo<TOtherKey>(in TOtherKey key) where TOtherKey : struct, IFdbKey;

#if !NET9_0_OR_GREATER

		/// <inheritdoc cref="IEquatable{T}.Equals(T?)"/>
		[Pure]
		bool Equals(ReadOnlySpan<byte> other);

		/// <inheritdoc cref="IComparable{T}.CompareTo(T?)"/>
		[Pure]
		int CompareTo(ReadOnlySpan<byte> other);

#endif

		/// <summary>Tests if the key is <c>Nil</c></summary>
		bool IsNull => false;

	}

}
