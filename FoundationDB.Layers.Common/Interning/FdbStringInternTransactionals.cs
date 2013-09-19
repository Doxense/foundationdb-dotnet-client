#region BSD Licence
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

namespace FoundationDB.Layers.Interning
{
	using FoundationDB.Client;
	using System;
	using System.Threading;
	using System.Threading.Tasks;

	public static class FdbStringInternTransactionals
	{

		/// <summary>Look up string <paramref name="value"/> in the intern database and return its normalized representation. If value already exists, intern returns the existing representation.</summary>
		/// <param name="trans">Fdb database</param>
		/// <param name="value">String to intern</param>
		/// <returns>Normalized representation of the string</returns>
		/// <remarks>The length of the string <paramref name="value"/> must not exceed the maximum FoundationDB value size</remarks>
		public static Task<Slice> InternAsync(this FdbStringIntern self, FdbDatabase db, string value, CancellationToken ct = default(CancellationToken))
		{
			return db.ReadWriteAsync((tr, _ctx) => self.InternAsync(tr, value, _ctx.Token), ct);
		}

		/// <summary>Return the long string associated with the normalized representation <paramref name="uid"/></summary>
		/// <param name="db">Fdb database</param>
		/// <param name="uid">Interned uid of the string</param>
		/// <returns>Original value of the interned string, or an exception if it does it does not exist</returns>
		public static Task<string> LookupAsync(this FdbStringIntern self, FdbDatabase db, Slice uid, CancellationToken ct = default(CancellationToken))
		{
			return db.ReadAsync((tr, _ctx) => self.LookupAsync(tr, uid, _ctx.Token), ct);
		}

	}

}
