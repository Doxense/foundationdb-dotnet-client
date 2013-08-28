﻿#region BSD Licence
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

namespace FoundationDB.Layers.Tuples
{
	using FoundationDB.Client;
	using FoundationDB.Client.Utils;
	using System;
	using System.Threading;
	using System.Threading.Tasks;

	/// <summary>Extensions methods to add FdbSubspace overrides to various types</summary>
	public static class FdbSubspaceExtensions
	{

		/// <summary>Clear the entire content of a subspace</summary>
		public static void ClearRange(this IFdbTransaction trans, FdbSubspace subspace)
		{
			Contract.Requires(trans != null && subspace != null);

			trans.ClearRange(FdbKeyRange.StartsWith(subspace.Key));
		}

		/// <summary>Returns all the keys inside of a subspace</summary>
		public static FdbRangeQuery GetRangeStartsWith(this IFdbReadTransaction trans, FdbSubspace subspace, FdbRangeOptions options = null)
		{
			Contract.Requires(trans != null && subspace != null);

			return trans.GetRange(FdbKeyRange.StartsWith(subspace.Key), options);
		}

		/// <summary>Read a key inside a subspace</summary>
		/// <example>
		/// Both lines are equivalent:
		/// tr.GetAsync(new FdbSubspace("Hello"), FdbTuple.Create("World"));
		/// tr.GetAsync(FdbTuple.Create("Hello", "World"));
		/// </example>
		public static Task<Slice> GetAsync(this IFdbReadTransaction trans, FdbSubspace subspace, IFdbTuple key, CancellationToken ct = default(CancellationToken))
		{
			Contract.Requires(trans != null && subspace != null && key != null);

			return trans.GetAsync(subspace.Concat(key), ct);
		}

		/// <summary>Write a key inside a subspace</summary>
		/// <example>
		/// Both lines are equivalent:
		/// tr.Set(new FdbSubspace("Hello"), FdbTuple.Create("World"), some_value);
		/// tr.Set(FdbTuple.Create("Hello", "World"), some_value);
		/// </example>
		public static void Set(this IFdbTransaction trans, FdbSubspace subspace, IFdbTuple key, Slice value)
		{
			Contract.Requires(trans != null && subspace != null && key != null);

			trans.Set(subspace.Concat(key), value);
		}

	}
}
