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
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace FoundationDb.Client.Tables
{

	public class FdbTable
	{

		public FdbTable(FdbDatabase database, FdbSubspace subspace)
		{
			if (database == null) throw new ArgumentNullException("database");
			if (subspace == null) throw new ArgumentNullException("subspace");

			this.Database = database;
			this.Subspace = subspace;
		}

		public FdbDatabase Database { get; private set; }

		public FdbSubspace Subspace { get; private set; }

		#region GetKeyBytes() ...

		public Slice GetKeyBytes<TKey>(TKey key)
		{
			return this.Subspace.GetKeyBytes<TKey>(key);
		}

		public Slice GetKeyBytes(Slice key)
		{
			return this.Subspace.GetKeyBytes(key);
		}

		public Slice GetKeyBytes(IFdbKey tuple)
		{
			return this.Subspace.GetKeyBytes(tuple);
		}

		#endregion

		#region

		/// <summary>Returns a tuple (namespace, key, )</summary>
		/// <typeparam name="TKey"></typeparam>
		/// <param name="key"></param>
		/// <returns></returns>
		public IFdbTuple Key<TKey>(TKey key)
		{
			return this.Subspace.Append<TKey>(key);
		}

		/// <summary>Add the namespace in front of an existing tuple</summary>
		/// <param name="tuple">Existing tuple</param>
		/// <returns>(namespace, tuple_items, )</returns>
		public IFdbTuple Key(IFdbTuple tuple)
		{
			return this.Subspace.AppendRange(tuple);
		}

		#endregion

		#region GetAsync() ...

		public Task<Slice> GetAsync<TKey>(FdbTransaction trans, TKey key, bool snapshot = false, CancellationToken ct = default(CancellationToken))
		{
			return trans.GetAsync(GetKeyBytes(key), snapshot, ct);
		}

		public Task<Slice> GetAsync(FdbTransaction trans, Slice key, bool snapshot = false, CancellationToken ct = default(CancellationToken))
		{
			return trans.GetAsync(GetKeyBytes(key), snapshot, ct);
		}

		public Task<Slice> GetAsync(FdbTransaction trans, IFdbKey tuple, bool snapshot = false, CancellationToken ct = default(CancellationToken))
		{
			return trans.GetAsync(GetKeyBytes(tuple), snapshot, ct);
		}

		public async Task<Slice> GetAsync<TKey>(TKey key, bool snapshot = false, CancellationToken ct = default(CancellationToken))
		{
			using (var trans = this.Database.BeginTransaction())
			{
				return await GetAsync(trans, key, snapshot, ct);
			}
		}

		public async Task<Slice> GetAsync(Slice key, bool snapshot = false, CancellationToken ct = default(CancellationToken))
		{
			using (var trans = this.Database.BeginTransaction())
			{
				return await GetAsync(trans, key, snapshot, ct);
			}
		}

		public async Task<Slice> GetAsync(IFdbKey tuple, bool snapshot = false, CancellationToken ct = default(CancellationToken))
		{
			using (var trans = this.Database.BeginTransaction())
			{
				return await GetAsync(trans, tuple, snapshot, ct);
			}
		}

		#endregion

		#region Set() ...

		public void Set<TKey>(FdbTransaction trans, TKey key, Slice value)
		{
			trans.Set(GetKeyBytes<TKey>(key), value);
		}

		public void Set(FdbTransaction trans, Slice key, Slice value)
		{
			trans.Set(GetKeyBytes(key), value);
		}

		public void Set(FdbTransaction trans, IFdbKey tuple, Slice value)
		{
			trans.Set(GetKeyBytes(tuple), value);
		}

		public async Task SetAsync<TKey>(TKey key, Slice value)
		{
			using (var trans = this.Database.BeginTransaction())
			{
				Set(trans, key, value);
				await trans.CommitAsync();
			}
		}

		public async Task SetAsync(Slice key, Slice value)
		{
			using (var trans = this.Database.BeginTransaction())
			{
				Set(trans, key, value);
				await trans.CommitAsync();
			}
		}

		public async Task SetAsync(IFdbKey tuple, Slice value)
		{
			using (var trans = this.Database.BeginTransaction())
			{
				Set(trans, tuple, value);
				await trans.CommitAsync();
			}
		}


		#endregion
	}

	public static class FdbTableExtensions
	{


		public static FdbTable Table(this FdbDatabase db, string tableName)
		{
			return new FdbTable(db, new FdbSubspace(tableName));
		}

		public static FdbTable Table(this FdbDatabase db, IFdbTuple prefix)
		{
			return new FdbTable(db, new FdbSubspace(prefix));
		}

	}

}
