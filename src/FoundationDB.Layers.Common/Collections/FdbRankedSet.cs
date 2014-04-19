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

namespace FoundationDB.Layers.Collections
{
	using FoundationDB.Client;
#if DEBUG
	using FoundationDB.Filters.Logging;
#endif
	using FoundationDB.Layers.Tuples;
	using FoundationDB.Linq;
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using System.Threading;
	using System.Threading.Tasks;
	using FoundationDB.Async;
	using System.Globalization;
	using FoundationDB.Client.Utils;

	/// <summary>
	/// Provides a high-contention Queue class
	/// </summary>
	public class FdbRankedSet
	{
		// from https://github.com/FoundationDB/python-layers/blob/master/lib/rankedset.py

		private const int MAX_LEVELS = 6;
		private const int LEVEL_FAN_POW = 4; // 2^X per level

		// TODO: should we use a PRNG ? If two counter instances are created at the same moment, they could share the same seed ?
		private readonly Random Rng = new Random();

		/// <summary>Create a new queue using either High Contention mode or Simple mode</summary>
		/// <param name="subspace">Subspace where the queue will be stored</param>
		/// <param name="highContention">If true, uses High Contention Mode (lots of popping clients). If true, uses the Simple Mode (a few popping clients).</param>
		public FdbRankedSet(FdbSubspace subspace)
		{
			if (subspace == null) throw new ArgumentNullException("subspace");

			this.Subspace = subspace;
		}

		public Task OpenAsync(IFdbTransaction trans)
		{
			if (trans == null) throw new ArgumentNullException("trans");
			return SetupLevelsAsync(trans);
		}

		/// <summary>Subspace used as a prefix for all items in this table</summary>
		public FdbSubspace Subspace { get; private set; }

		/// <summary>Returns the number of items in the set.</summary>
		/// <param name="trans"></param>
		/// <returns></returns>
		public Task<long> SizeAsync(IFdbReadOnlyTransaction trans)
		{
			if (trans == null) throw new ArgumentNullException("trans");

			return trans
				.GetRange(this.Subspace.Partition(MAX_LEVELS - 1).ToRange())
				.Select(kv => DecodeCount(kv.Value))
				.SumAsync();
		}

		public async Task InsertAsync(IFdbTransaction trans, Slice key)
		{
			if (trans == null) throw new ArgumentNullException("trans");

			if (await ContainsAsync(trans, key).ConfigureAwait(false))
			{
				return;
			}

			int keyHash = key.GetHashCode(); //TODO: proper hash function?
			//Console.WriteLine("Inserting " + key + " with hash " + keyHash.ToString("x"));
			for(int level = 0; level < MAX_LEVELS; level++)
			{
				var prevKey = await GetPreviousNodeAsync(trans, level, key);

				if ((keyHash & ((1 << (level * LEVEL_FAN_POW)) - 1)) != 0)
				{
					//Console.WriteLine("> [" + level + "] Incrementing previous key: " + FdbKey.Dump(prevKey));
					trans.AtomicAdd(this.Subspace.Partition(level, prevKey), EncodeCount(1));
				}
				else
				{
					//Console.WriteLine("> [" + level + "] inserting and updating previous key: " + FdbKey.Dump(prevKey));
					// Insert into this level by looking at the count of the previous
					// key in the level and recounting the next lower level to correct
					// the counts
					var prevCount = DecodeCount(await trans.GetAsync(this.Subspace.Pack(level, prevKey)).ConfigureAwait(false));
					var newPrevCount = await SlowCountAsync(trans, level - 1, prevKey, key);
					var count = checked((prevCount - newPrevCount) + 1);

					// print "insert", key, "level", level, "count", count,
					// "splits", prevKey, "oldC", prevCount, "newC", newPrevCount
					trans.Set(this.Subspace.Pack(level, prevKey), EncodeCount(newPrevCount));
					trans.Set(this.Subspace.Pack(level, key), EncodeCount(count));
				}
			}
		}

		public async Task<bool> ContainsAsync(IFdbReadOnlyTransaction trans, Slice key)
		{
			if (trans == null) throw new ArgumentNullException("trans");
			if (key.IsNull) throw new ArgumentException("Empty key not allowed in set", "key");

			return (await trans.GetAsync(this.Subspace.Pack(0, key)).ConfigureAwait(false)).HasValue;
		}

		public async Task EraseAsync(IFdbTransaction trans, Slice key)
		{
			if (trans == null) throw new ArgumentNullException("trans");

			if (!(await ContainsAsync(trans, key).ConfigureAwait(false)))
			{
				return;
			}

			for (int level = 0; level < MAX_LEVELS; level++)
			{
				// This could be optimized with hash
				var k = this.Subspace.Partition(level, key);
				var c = await trans.GetAsync(k).ConfigureAwait(false);
				if (c.HasValue) trans.Clear(k);
				if (level == 0) continue;

				var prevKey = await GetPreviousNodeAsync(trans, level, key);
				Contract.Assert(prevKey != key);
				long countChange = -1;
				if (c.HasValue) countChange += DecodeCount(c);

				trans.AtomicAdd(this.Subspace.Pack(level, prevKey), EncodeCount(countChange));
			}
		}

		public async Task<long?> Rank(IFdbReadOnlyTransaction trans, Slice key)
		{
			if (trans == null) throw new ArgumentNullException("trans");
			if (key.IsNull) throw new ArgumentException("Empty key not allowed in set", "key");

			if (!(await ContainsAsync(trans, key).ConfigureAwait(false)))
			{
				return default(long?);
			}

			long r = 0;
			var rankKey = Slice.Empty;
			for(int level = MAX_LEVELS - 1; level >= 0; level--)
			{
				var lss = this.Subspace.Partition(level);
				long lastCount = 0;
				var kcs = await trans.GetRange(
					FdbKeySelector.FirstGreaterOrEqual(lss.Pack(rankKey)),
					FdbKeySelector.FirstGreaterThan(lss.Pack(key))
				).ToListAsync().ConfigureAwait(false);
				foreach (var kc in kcs)
				{
					rankKey = lss.UnpackSingle<Slice>(kc.Key);
					lastCount = DecodeCount(kc.Value);
					r += lastCount;
				}
				r -= lastCount;
				if (rankKey == key)
				{
					break;
				}
			}
			return r;
		}

		public async Task<Slice> GetNthAsync(IFdbReadOnlyTransaction trans, long rank)
		{
			if (rank < 0) return Slice.Nil;

			long r = rank;
			var key = Slice.Empty;
			for (int level = MAX_LEVELS - 1; level >= 0; level--)
			{
				var lss = this.Subspace.Partition(level);
				var kcs = await trans.GetRange(lss.Pack(key), lss.ToRange().End).ToListAsync().ConfigureAwait(false);

				if (kcs.Count == 0) break;

				foreach(var kc in kcs)
				{
					key = lss.UnpackSingle<Slice>(kc.Key);
					long count = DecodeCount(kc.Value);
					if (key.IsPresent && r == 0)
					{
						return key;
					}
					if (count > r)
					{
						break;
					}
					r -= count;
				}
			}
			return Slice.Nil;
		}

		//TODO: get_range

		/// <summary>Clears the entire set.</summary>
		public Task ClearAllAsync(IFdbTransaction trans)
		{
			trans.ClearRange(this.Subspace.ToRange());
			return SetupLevelsAsync(trans);
		}

		#region Private Helpers...

		private static Slice EncodeCount(long c)
		{
			return Slice.FromFixed64(c);
		}

		private static long DecodeCount(Slice v)
		{
			return v.ToInt64();
		}

		private Task<long> SlowCountAsync(IFdbReadOnlyTransaction trans, int level, Slice beginKey, Slice endKey)
		{
			if (level == -1)
			{
				return Task.FromResult<long>(beginKey.IsPresent ? 1 : 0);
			}

			return trans
				.GetRange(this.Subspace.Pack(level, beginKey), this.Subspace.Pack(level, endKey))
				.Select(kv => DecodeCount(kv.Value))
				.SumAsync();
		}

		private async Task SetupLevelsAsync(IFdbTransaction trans)
		{
			var ks = Enumerable.Range(0, MAX_LEVELS)
				.Select((l) => this.Subspace.Pack(l, Slice.Empty))
				.ToList();

			var res = await trans.GetValuesAsync(ks).ConfigureAwait(false);
			for (int l = 0; l < res.Length; l++)
			{
				//Console.WriteLine(ks[l]);
				if (res[l].IsNull) trans.Set(ks[l], EncodeCount(0));
			}
		}

		private async Task<Slice> GetPreviousNodeAsync(IFdbTransaction trans, int level, Slice key)
		{
			// GetPreviousNodeAsync looks for the previous node on a level, but "doesn't care"
			// about the contents of that node. It therefore uses a non-isolated (snaphot)
			// read and explicitly adds a conflict range that is exclusive of the actual,
			// found previous node. This allows an increment of that node not to trigger
			// a transaction conflict. We also add a conflict key on the found previous
			// key in level 0. This allows detection of erasures.

			var k = this.Subspace.Pack(level, key);
			//Console.WriteLine(k);
			//Console.WriteLine("GetPreviousNode(" + level + ", " + key + ")");
			//Console.WriteLine(FdbKeySelector.LastLessThan(k) + " <= x < " + FdbKeySelector.FirstGreaterOrEqual(k));
			var kv = await trans
				.Snapshot
				.GetRange(
					FdbKeySelector.LastLessThan(k),
					FdbKeySelector.FirstGreaterOrEqual(k)
				)
				.FirstAsync()
				.ConfigureAwait(false);
			//Console.WriteLine("Found " + FdbKey.Dump(kv.Key));

			var prevKey = this.Subspace.UnpackLast<Slice>(kv.Key);
			trans.AddReadConflictRange(kv.Key + FdbKey.MinValue, k);
			trans.AddReadConflictKey(this.Subspace.Pack(0, prevKey));
			return prevKey;
		}



		#endregion

	}

}
