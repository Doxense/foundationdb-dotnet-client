#region BSD Licence
/* Copyright (c) 2013-2014, Doxense SAS
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
	using JetBrains.Annotations;
	using System;

	public interface IFdbSubspace : IFdbKey
	{
		// This interface helps solve some type resolution ambiguities at compile time between types that all implement IFdbKey but have different semantics for partitionning and concatenation

		/// <summary>Create a new subspace by adding a suffix to the key of the current subspace.</summary>
		/// <param name="suffix">Binary suffix that will be appended to the current prefix</param>
		/// <returns>New subspace whose prefix is the concatenation of the parent prefix, and <paramref name="suffix"/></returns>
		IFdbSubspace this[Slice suffix] { [NotNull] get; }

		IFdbSubspace this[IFdbKey key] { [NotNull] get; }

		/// <summary>Test if a key is inside the range of keys logically contained by this subspace</summary>
		/// <param name="key">Key to test</param>
		/// <returns>True if the key can exist inside the current subspace.</returns>
		/// <remarks>Please note that this method does not test if the key *actually* exists in the database, only if the key is not ouside the range of keys defined by the subspace.</remarks>
		bool Contains(Slice key);

		/// <summary>Remove the subspace prefix from a binary key, or throw if the key does not belong to this subspace</summary>
		/// <param name="key">Complete key that contains the current subspace prefix, and a binary suffix.</param>
		/// <returns>Binary suffix of the key (or Slice.Empty is the key is exactly equal to the subspace prefix). If the key is equal to Slice.Nil, then it will be returned unmodified. If the key is outside of the subspace, the method throws.</returns>
		/// <exception cref="System.ArgumentException">If key is outside the current subspace.</exception>
		Slice ExtractAndCheck(Slice key);
		//REVIEW: what about Extract(..) ? Merge both with an optional "bool throwIfOutside = false" ?

		FdbKeyRange ToRange(Slice suffix);

		//REVIEW: Consider adding IEquatable<IFdbSubspace> and maybe IComparable<IFdbSubspace> ?
	}

}
