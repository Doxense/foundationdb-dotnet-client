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

namespace FoundationDb.Client.Tables
{
	using FoundationDb.Client.Tuples;
	using System;
	using System.Threading;
	using System.Threading.Tasks;

	public class FdbTable
	{

		public FdbTable(FdbDatabase database, FdbSubspace subspace)
		{
			if (database == null) throw new ArgumentNullException("database");
			if (subspace == null) throw new ArgumentNullException("subspace");

			this.Database = database;
			this.Subspace = subspace;
		}

		/// <summary>Database used to perform transactions</summary>
		public FdbDatabase Database { get; private set; }

		/// <summary>Subspace used as a prefix for all items in this table</summary>
		public FdbSubspace Subspace { get; private set; }

		#region Keys...

		/// <summary>Add the namespace in front of an existing tuple</summary>
		/// <param name="tuple">Existing tuple</param>
		/// <returns>(namespace, tuple_items, )</returns>
		protected virtual IFdbTuple MakeKey(IFdbTuple tuple)
		{
			return this.Subspace.AppendRange(tuple);
		}

		#endregion

		#region GetAsync() ...

		public Task<Slice> GetAsync(FdbTransaction trans, IFdbTuple tuple, bool snapshot = false, CancellationToken ct = default(CancellationToken))
		{
			return trans.GetAsync(MakeKey(tuple).ToSlice(), snapshot, ct);
		}

		public async Task<Slice> GetAsync(IFdbTuple tuple, bool snapshot = false, CancellationToken ct = default(CancellationToken))
		{
			using (var trans = this.Database.BeginTransaction())
			{
				return await GetAsync(trans, tuple, snapshot, ct).ConfigureAwait(false);
			}
		}

		#endregion

		#region Set() ...

		public void Set(FdbTransaction trans, IFdbTuple tuple, Slice value)
		{
			trans.Set(MakeKey(tuple).ToSlice(), value);
		}

		public async Task SetAsync(IFdbTuple tuple, Slice value)
		{
			using (var trans = this.Database.BeginTransaction())
			{
				Set(trans, tuple, value);
				await trans.CommitAsync().ConfigureAwait(false);
			}
		}

		#endregion
	}

}
