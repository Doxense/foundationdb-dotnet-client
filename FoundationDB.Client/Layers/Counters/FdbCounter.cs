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

namespace FoundationDB.Layers.Counters
{
	using FoundationDB.Client;
	using FoundationDB.Client.Utils;
	using FoundationDB.Layers.Tuples;
	using FoundationDB.Linq;
	using System;
	using System.Collections.Generic;
	using System.Globalization;
	using System.IO;
	using System.Linq;
	using System.Threading;
	using System.Threading.Tasks;

	/// <summary>
	/// Represents an integer value which can be incremented without conflict.
	/// Uses a sharded representation (which scales with contention) along with background coalescing..
	/// </summary>
	public class FdbCounter
	{

		// TODO: should we use a PRNG ? If two counter instances are created at the same moment, they could share the same seed ?
		private readonly Random Rng = new Random();

		/// <summary>
		/// Create a new object representing a binary large object (blob).
		/// Only keys within the subspace will be used by the object. 
		/// Other clients of the database should refrain from modifying the subspace.</summary>
		/// <param name="subspace">Subspace to be used for storing the blob data and metadata</param>
		public FdbCounter(FdbDatabase db, FdbSubspace subspace)
		{
			if (db == null) throw new ArgumentNullException("db");
			if (subspace == null) throw new ArgumentNullException("subspace");

			this.Database = db;
			this.Subspace = subspace;
		}

		public FdbCounter(FdbDatabase db, IFdbTuple tuple)
		{
			if (db == null) throw new ArgumentNullException("db");
			if (tuple == null) throw new ArgumentNullException("tuple");

			this.Database = db;
			this.Subspace = new FdbSubspace(tuple);
		}

		/// <summary>Subspace used as a prefix for all items in this table</summary>
		public FdbSubspace Subspace { get; private set; }

		public FdbDatabase Database { get; private set; }

		private Slice EncodeInt(long i)
		{
			return FdbTuple.Pack(i);
		}

		private long DecodeInt(Slice s)
		{
			return FdbTuple.Unpack(s).Get<long>(0);
		}

		private Slice RandomId()
		{
			return Slice.Random(this.Rng, 20);
		}

		private async Task Coalesce(int N)
		{
			long total = 0;

			using (var tr = this.Database.BeginTransaction())
			{
				try
				{
					// read N writes from a random place in ID space
					var loc = this.Subspace.Pack(FdbTuple.Create(RandomId()));

					List<KeyValuePair<Slice, Slice>> shards;
					if (this.Rng.NextDouble() < 0.5)
					{
						shards = await tr.GetRange(loc, this.Subspace.ToRange().End, limit: N, snapshot: true).ToListAsync();
					}
					else
					{
						shards = await tr.GetRange(this.Subspace.ToRange().Begin, loc, limit: N, reverse: true, snapshot: true).ToListAsync();
					}

					if (shards.Count > 0)
					{
						// remove read shards transaction
						foreach (var shard in shards)
						{
							checked { total += DecodeInt(shard.Value); }
							await tr.GetAsync(shard.Key); // real read for isolation
							tr.Clear(shard.Key);
						}

						tr.Set(this.Subspace.Pack(FdbTuple.Create(RandomId())), EncodeInt(total));

						// note: no .wait() on the commit below--this just goes off
						// into the ether and hopefully sometimes works :)

						// note: contrary to the pytonh impl, we will await the commit, and rely on the caller to not wait to the Coalesce task itself to complete.
						// That way, the transaction will live as long as the task, and we ensure that it gets disposed at some time
						await tr.CommitAsync();
					}
				}
				catch (FdbException)
				{
					//TODO: logging ?
					return;
				}
			}
		}

		private void BackgroundCoalesce(int n)
		{
			// fire and forget
			Coalesce(n).ContinueWith((t) =>
			{
				// observe any exceptions
				if (t.IsFaulted) { var x = t.Exception; }
				//TODO: logging ?
			});
		}

		/// <summary>
		/// Get the value of the counter.
		/// Not recommended for use with read/write transactions when the counter is being frequently updated (conflicts will be very likely).
		/// </summary>
		/// <param name="trans"></param>
		/// <returns></returns>
		public async Task<long> GetTransactional(FdbTransaction trans, CancellationToken ct = default(CancellationToken))
		{
			if (trans == null) throw new ArgumentNullException("trans");

			long total = 0;
			await trans
				.GetRangeStartsWith(this.Subspace)
				.Values()
				.ForEachAsync((v) => { checked { total += DecodeInt(v); } });

			return total;
		}

		/// <summary>
		/// Get the value of the counter.
		/// Not recommended for use with read/write transactions when the counter is being frequently updated (conflicts will be very likely).
		/// </summary>
		/// <param name="trans"></param>
		/// <returns></returns>
		public async Task<long> GetSnapshot(FdbTransaction trans, CancellationToken ct = default(CancellationToken))
		{
			if (trans == null) throw new ArgumentNullException("trans");

			long total = 0;
			await trans
				.GetRangeStartsWith(this.Subspace, snapshot: true)
				.ForEachAsync((kvp) => { checked { total += DecodeInt(kvp.Value); } });

			return total;
		}

		public void Add(FdbTransaction trans, long x)
		{
			if (trans == null) throw new ArgumentNullException("trans");

			trans.Set(this.Subspace.Pack(FdbTuple.Create(RandomId())), EncodeInt(x));

			if (this.Rng.NextDouble() < 0.1)
			{
				BackgroundCoalesce(20);
			}
		}

		public async Task AddAsync(long x, CancellationToken ct = default(CancellationToken))
		{
			ct.ThrowIfCancellationRequested();
			using (var trans = this.Database.BeginTransaction())
			{
				Add(trans, x);
				await trans.CommitAsync(ct);
			}
		}

		public async Task SetTotal(FdbTransaction trans, long x, CancellationToken ct = default(CancellationToken))
		{
			if (trans == null) throw new ArgumentNullException("trans");

			long value = await GetSnapshot(trans, ct);
			Add(trans, x - value);
		}

	}

}
