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

using FoundationDb.Client;
using FoundationDb.Layers.Tuples;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace FoundationDb.Layers.Arrays
{

	public class FdbArray
	{

		public FdbArray(FdbDatabase database, FdbSubspace subspace)
		{
			if (database == null) throw new ArgumentNullException("database");
			if (subspace == null) throw new ArgumentNullException("subspace");

			this.Database = database;
			this.Subspace = subspace;
		}

		public FdbDatabase Database { get; private set; }

		public FdbSubspace Subspace { get; private set; }

		#region GetKeyBytes() ...

		public Slice GetKeyBytes(int key)
		{
			return this.Subspace.Pack<int>(key);
		}

		public Slice GetKeyBytes(long key)
		{
			return this.Subspace.Pack<long>(key);
		}

		#endregion

		#region Key()...

		public IFdbTuple Key(int index)
		{
			return this.Subspace.Create<int>(index);
		}

		public IFdbTuple Key(long index)
		{
			return this.Subspace.Create<long>(index);
		}

		#endregion

		#region GetAsync() ...

		public Task<Slice> GetAsync(FdbTransaction trans, int key, bool snapshot = false, CancellationToken ct = default(CancellationToken))
		{
			return trans.GetAsync(GetKeyBytes(key), snapshot, ct);
		}

		public Task<Slice> GetAsync(FdbTransaction trans, long key, bool snapshot = false, CancellationToken ct = default(CancellationToken))
		{
			return trans.GetAsync(GetKeyBytes(key), snapshot, ct);
		}

		public async Task<Slice> GetAsync(int key, bool snapshot = false, CancellationToken ct = default(CancellationToken))
		{
			using (var trans = this.Database.BeginTransaction())
			{
				return await GetAsync(trans, key, snapshot, ct).ConfigureAwait(false);
			}
		}

		public async Task<Slice> GetAsync(long key, bool snapshot = false, CancellationToken ct = default(CancellationToken))
		{
			using (var trans = this.Database.BeginTransaction())
			{
				return await GetAsync(trans, key, snapshot, ct).ConfigureAwait(false);
			}
		}

		#endregion

		#region Set() ...

		public void Set(FdbTransaction trans, int key, Slice value)
		{
			trans.Set(GetKeyBytes(key), value);
		}

		public void Set(FdbTransaction trans, long key, Slice value)
		{
			trans.Set(GetKeyBytes(key), value);
		}

		public async Task SetAsync<TKey>(int key, Slice value)
		{
			using (var trans = this.Database.BeginTransaction())
			{
				Set(trans, key, value);
				await trans.CommitAsync().ConfigureAwait(false);
			}
		}

		public async Task SetAsync(long key, Slice value)
		{
			using (var trans = this.Database.BeginTransaction())
			{
				Set(trans, key, value);
				await trans.CommitAsync().ConfigureAwait(false);
			}
		}

		#endregion

		#region GetValuesAsync()

		public Task<List<Slice>> GetValuesAsync(bool snapshot = false, CancellationToken ct = default(CancellationToken))
		{
			throw new NotImplementedException();
		}


		#endregion
	}

	public static class FdbArrayExtensions
	{

		public static FdbArray Array(this FdbDatabase db, string name)
		{
			return new FdbArray(db, new FdbSubspace(name));
		}

		public static FdbArray Array(this FdbDatabase db, IFdbTuple prefix)
		{
			return new FdbArray(db, new FdbSubspace(prefix));
		}

	}

}
