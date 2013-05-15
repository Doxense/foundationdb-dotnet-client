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
	* Neither the name of the <organization> nor the
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

using FoundationDb.Client.Tuples;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;

namespace FoundationDb.Client.Tables
{

	/// <summary>Simple indexe that indexes instances of type <typeparamref name="TElement"/>, by a value of type <typeparamref name="TValue"/> and a reference of type <typeparamref name="TId"/></summary>
	/// <typeparam name="TElement">Type of document of entity being indexed (User, Order, ForumPost, ...)</typeparam>
	/// <typeparam name="TValue">Type of the value being indexed</typeparam>
	/// <typeparam name="TId">Type of the unique id of each document or entity</typeparam>
	public class FdbSimpleIndex<TValue, TId>
	{

		public FdbSimpleIndex(FdbDatabase database, FdbSubspace subspace)
		{
			if (database == null) throw new ArgumentNullException("database");
			if (subspace == null) throw new ArgumentNullException("subspace");

			this.Database = database;
			this.Subspace = subspace;
		}

		public FdbDatabase Database { get; private set; }

		public FdbSubspace Subspace { get; private set; }

		public void AddOrUpdate(FdbTransaction trans, TId id, TValue value)
		{
			trans.Set(this.Subspace.Append(value, id), Slice.Empty);
		}

		public void Remove(FdbTransaction trans, TId id, TValue value)
		{
			trans.Clear(this.Subspace.Append(value, id));
		}

		public async Task<List<TId>> LookupAsync(FdbTransaction trans, TValue value, bool snapshot = false, CancellationToken ct = default(CancellationToken))
		{
			var results = await trans.GetRangeAsync(this.Subspace.Append(value), snapshot, ct);
			return results
				.Page
				.Select(kvp => FdbTuple.Unpack(kvp.Key))
				.Select(tuple => (TId)(tuple[tuple.Count - 1]))
				.ToList();
		}

	}

}
