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
	using System.Collections.Generic;
	using JetBrains.Annotations;

	/// <summary>Represents a sub-partition of the global key space.</summary>
	/// <remarks>
	/// A subspace is the logical equivalent of a key prefix that is implicitly prepended to all keys generated from it.
	/// A "vanilla" data subspace does not imply any encoding scheme by default, but can be wrapped into a more complex subspace which includes Key Codec.
	/// </remarks>
	/// 
	/// <example>In pseudo code, and given a 'MySubspaceImpl' that implement <see cref="IKeySubspace"/>:
	/// <code>
	/// subspace = new MySubspaceImpl({ABC})
	/// subspace.ConcatKey({123}) => {ABC123}
	/// subspace.ExtractKey({ABC123}) => {123}
	/// subspace.ExtractKey({DEF123}) => ERROR
	/// </code>
	/// </example>
	[PublicAPI]
	public interface IKeySubspace
	{
		// This interface helps solve some type resolution ambiguities at compile time between types that all implement IFdbKey but have different semantics for partitionning and concatenation

		/// <summary>Returns the prefix of this subspace</summary>
		Slice GetPrefix();

		/// <summary>Return a key range that contains all the keys in this subspace, including the prefix itself</summary>
		/// <returns>Return the range: Key &lt;= x &lt;= Increment(Key)</returns>
		[Pure]
		KeyRange ToRange();

		/// <summary>Return a key range that contains all the keys under a suffix in this subspace</summary>
		/// <param name="suffix">Binary suffix that will be appended to the current prefix, before computing the range</param>
		/// <returns>Return the range: (this.Key + suffix) &lt;= x &lt;= Increment(this.Key + suffix)</returns>
		[Pure]
		KeyRange ToRange(Slice suffix);

		/// <summary>Create a new subspace by adding a suffix to the key of the current subspace.</summary>
		/// <param name="suffix">Binary suffix that will be appended to the current prefix</param>
		/// <returns>New subspace whose prefix is the concatenation of the parent prefix, and <paramref name="suffix"/></returns>
		IKeySubspace this[Slice suffix]
		{
			[Pure, NotNull] get;
		}
		//REVIEW this should probably be renamed into GetSubspace(suffix) or Partition(suffix) in order to make it explicit that it is for creating subspaces instances that can be reused multiple times, and not single-use to generate a single key!

		/// <summary>Test if a key is inside the range of keys logically contained by this subspace</summary>
		/// <param name="key">Key to test</param>
		/// <returns>True if the key can exist inside the current subspace.</returns>
		/// <remarks>Please note that this method does not test if the key *actually* exists in the database, only if the key is not ouside the range of keys defined by the subspace.</remarks>
		[Pure]
		bool Contains(Slice key);

		/// <summary>Check that a key fits inside this subspace, and return '' or '\xFF' if it is outside the bounds</summary>
		/// <param name="key">Key that needs to be checked</param>
		/// <param name="allowSystemKeys">If true, allow keys that starts with \xFF even if this subspace is not the Empty subspace or System subspace itself.</param>
		/// <returns>The <paramref name="key"/> unchanged if it is contained in the namespace, Slice.Empty if it was before the subspace, or FdbKey.MaxValue if it was after.</returns>
		Slice BoundCheck(Slice key, bool allowSystemKeys);

		/// <summary>Return the key that is composed of the subspace's prefix and a binary suffix</summary>
		/// <param name="suffix">Binary suffix that will be appended to the current prefix</param>
		/// <returns>Full binary key</returns>
		[Pure]
		Slice ConcatKey(Slice suffix);

		/// <summary>Concatenate a batch of keys under this subspace</summary>
		/// <param name="suffixes">List of suffixes to process</param>
		/// <returns>Array of <see cref="Slice"/> which is equivalent to calling <see cref="ConcatKey(Slice)"/> on each entry in <paramref name="suffixes"/></returns>
		[Pure, NotNull]
		Slice[] ConcatKeys([NotNull] IEnumerable<Slice> suffixes);
		//REVIEW: could this be done via an extension method?

		/// <summary>Remove the subspace prefix from a binary key, and only return the tail, or Slice.Nil if the key does not fit inside the namespace</summary>
		/// <param name="key">Complete key that contains the current subspace prefix, and a binary suffix</param>
		/// <param name="boundCheck">If true, verify that <paramref name="key"/> is inside the bounds of the subspace</param>
		/// <returns>Binary suffix of the key (or Slice.Empty if the key is exactly equal to the subspace prefix). If the key is outside of the subspace, returns Slice.Nil</returns>
		/// <remarks>This is the inverse operation of <see cref="ConcatKey(Slice)"/></remarks>
		/// <exception cref="System.ArgumentException">If <paramref name="boundCheck"/> is true and <paramref name="key"/> is outside the current subspace.</exception>
		[Pure]
		Slice ExtractKey(Slice key, bool boundCheck = false);

		/// <summary>Remove the subspace prefix from a batch of binary keys, and only return the tail, or Slice.Nil if a key does not fit inside the namespace</summary>
		/// <param name="keys">Sequence of complete keys that contains the current subspace prefix, and a binary suffix</param>
		/// <param name="boundCheck">If true, verify that each key in <paramref name="keys"/> is inside the bounds of the subspace</param>
		/// <returns>Array of only the binary suffix of the keys, Slice.Empty for a key that is exactly equal to the subspace prefix, or Slice.Nil for a key that is outside of the subspace</returns>
		/// <exception cref="System.ArgumentException">If <paramref name="boundCheck"/> is true and at least one key in <paramref name="keys"/> is outside the current subspace.</exception>
		[Pure, NotNull]
		Slice[] ExtractKeys([NotNull] IEnumerable<Slice> keys, bool boundCheck = false);
		//REVIEW: could this be done via an extension method?

		/// <summary>Return a new slice buffer, initialized with the subspace prefix, that can be used for custom key serialization</summary>
		/// <param name="capacity">If non-zero, the expected buffer capacity. The size of the subspace prefix will be added to this value.</param>
		/// <returns>Instance of a SliceWriter with the prefix of this subspace already copied.</returns>
		[Pure]
		SliceWriter GetWriter(int capacity = 0);
		//REVIEW: this is an internal implementation detail that may be moved to a different interface?

	}

}
