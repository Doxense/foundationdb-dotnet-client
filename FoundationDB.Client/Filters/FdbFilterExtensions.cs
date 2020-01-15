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

namespace FoundationDB.Filters
{
	using System;
	using Doxense.Diagnostics.Contracts;
	using FoundationDB.Client;
	using JetBrains.Annotations;

	[PublicAPI]
	public static class FdbFilterExtensions
	{
		/// <summary>Return a read-only view of this transaction, that will only allow read operations.</summary>
		/// <param name="trans">Transaction to secure</param>
		/// <returns>The same transaction instance if it is already read-only, or a thin read-only wrapper around the transaction if it is writable.</returns>
		[NotNull]
		public static IFdbReadOnlyTransaction AsReadOnly([NotNull] this IFdbTransaction trans)
		{
			Contract.NotNull(trans, nameof(trans));

			if (trans.IsReadOnly)
			{ // this is already read-only
				return trans;
			}
			else
			{ // we must protect this transaction by wrapping it with a read-only filter
				return new ReadOnlyTransactionFilter(trans, ownsTransaction: false);
			}
		}
	}

}
