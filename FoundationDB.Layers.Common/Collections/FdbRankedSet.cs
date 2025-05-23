#region Copyright (c) 2023-2025 SnowBank SAS, (c) 2005-2023 Doxense SAS
// All rights reserved.
// 
// Redistribution and use in source and binary forms, with or without
// modification, are permitted provided that the following conditions are met:
// 	* Redistributions of source code must retain the above copyright
// 	  notice, this list of conditions and the following disclaimer.
// 	* Redistributions in binary form must reproduce the above copyright
// 	  notice, this list of conditions and the following disclaimer in the
// 	  documentation and/or other materials provided with the distribution.
// 	* Neither the name of SnowBank nor the
// 	  names of its contributors may be used to endorse or promote products
// 	  derived from this software without specific prior written permission.
// 
// THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND
// ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
// WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
// DISCLAIMED. IN NO EVENT SHALL SNOWBANK SAS BE LIABLE FOR ANY
// DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
// (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
// LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
// ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
// (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
// SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
#endregion

namespace FoundationDB.Layers.Collections
{
	using System.Linq;

	/// <summary>
	/// Provides a high-contention Queue class
	/// </summary>
	[DebuggerDisplay("Location={Location}")]
	[PublicAPI]
	public class FdbRankedSet : IFdbLayer<FdbRankedSet.State>
	{
		// Based on the lost implementation that used to be at https://github.com/FoundationDB/python-layers/blob/master/lib/rankedset.py
		// => this version has been "lost to time", and only this c# port remains (archive.org does not have a copy)
		// There is also a newer implementation in Java that can be seen at https://github.com/FoundationDB/fdb-record-layer/blob/main/fdb-extensions/src/main/java/com/apple/foundationdb/async/RankedSet.java

		private const int MAX_LEVELS = 6;
		private const int LEVEL_FAN_POW = 4; // 2^X per level

		public FdbRankedSet(ISubspaceLocation? location)
			: this(location?.AsDynamic())
		{ }

		/// <summary>Initializes a new ranked set at a given location</summary>
		/// <param name="location">Subspace where the set will be stored</param>
		public FdbRankedSet(DynamicKeySubspaceLocation? location)
		{
			this.Location = location ?? throw new ArgumentNullException(nameof(location));
		}

		/// <summary>Subspace used as a prefix for all items in this table</summary>
		public DynamicKeySubspaceLocation Location { get; }

		/// <summary>Make sure that the set is initialized</summary>
		public async Task OpenAsync(IFdbTransaction trans)
		{
			Contract.NotNull(trans);

			//TODO: BUGBUG: this should be cached!
			var state = await Resolve(trans);

			// ensure that the levels are set up properly
			await state.SetupLevelsAsync(trans);
		}

		[PublicAPI]
		public sealed class State
		{

			public IDynamicKeySubspace Subspace { get;}

			public State(IDynamicKeySubspace subspace)
			{
				this.Subspace = subspace;
			}

			/// <summary>Returns the number of items in the set.</summary>
			/// <param name="trans"></param>
			/// <returns></returns>
			public Task<long> SizeAsync(IFdbReadOnlyTransaction trans)
			{
				Contract.NotNull(trans);

				return trans
					.GetRange(this.Subspace.Partition.ByKey(MAX_LEVELS - 1).ToRange())
					.Select(kv => DecodeCount(kv.Value))
					.SumAsync();
			}

			public async Task InsertAsync(IFdbTransaction trans, Slice key)
			{
				Contract.NotNull(trans);

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
						trans.AtomicIncrement64(this.Subspace.Encode(level, prevKey));
					}
					else
					{
						//Console.WriteLine("> [" + level + "] inserting and updating previous key: " + FdbKey.Dump(prevKey));
						// Insert into this level by looking at the count of the previous
						// key in the level and recounting the next lower level to correct
						// the counts
						var prevCount = DecodeCount(await trans.GetAsync(this.Subspace.Encode(level, prevKey)).ConfigureAwait(false));
						var newPrevCount = await SlowCountAsync(trans, level - 1, prevKey, key);
						var count = checked((prevCount - newPrevCount) + 1);

						// print "insert", key, "level", level, "count", count,
						// "splits", prevKey, "oldC", prevCount, "newC", newPrevCount
						trans.Set(this.Subspace.Encode(level, prevKey), EncodeCount(newPrevCount));
						trans.Set(this.Subspace.Encode(level, key), EncodeCount(count));
					}
				}
			}

			public async Task<bool> ContainsAsync(IFdbReadOnlyTransaction trans, Slice key)
			{
				Contract.NotNull(trans);
				if (key.IsNull) throw new ArgumentException("Empty key not allowed in set", nameof(key));

				return (await trans.GetAsync(this.Subspace.Encode(0, key)).ConfigureAwait(false)).HasValue;
			}

			public async Task EraseAsync(IFdbTransaction trans, Slice key)
			{
				Contract.NotNull(trans);

				if (!(await ContainsAsync(trans, key).ConfigureAwait(false)))
				{
					return;
				}

				for (int level = 0; level < MAX_LEVELS; level++)
				{
					// This could be optimized with hash
					var k = this.Subspace.Encode(level, key);
					var c = await trans.GetAsync(k).ConfigureAwait(false);
					if (c.HasValue) trans.Clear(k);
					if (level == 0) continue;

					var prevKey = await GetPreviousNodeAsync(trans, level, key);
					Contract.Debug.Assert(prevKey != key);
					long countChange = -1;
					if (c.HasValue) countChange += DecodeCount(c);

					trans.AtomicAdd64(this.Subspace.Encode(level, prevKey), countChange);
				}
			}

			public async Task<long?> Rank(IFdbReadOnlyTransaction trans, Slice key)
			{
				Contract.NotNull(trans);
				Slice.ThrowIfNull(key, "Empty key not allowed in set");

				if (!(await ContainsAsync(trans, key).ConfigureAwait(false)))
				{
					return null;
				}

				long r = 0;
				var rankKey = Slice.Empty;
				for(int level = MAX_LEVELS - 1; level >= 0; level--)
				{
					var lss = this.Subspace.Partition.ByKey(level);
					long lastCount = 0;
					var kcs = await trans.GetRange(
						KeySelector.FirstGreaterOrEqual(lss.Encode(rankKey)),
						KeySelector.FirstGreaterThan(lss.Encode(key))
					).ToListAsync().ConfigureAwait(false);
					foreach (var kc in kcs)
					{
						rankKey = lss.Decode<Slice>(kc.Key);
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
					var lss = this.Subspace.Partition.ByKey(level);
					var kcs = await trans.GetRange(lss.Encode(key), lss.ToRange().End).ToListAsync().ConfigureAwait(false);

					if (kcs.Count == 0) break;

					foreach(var kc in kcs)
					{
						key = lss.Decode<Slice>(kc.Key);
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

			private static Slice EncodeCount(long c) => Slice.FromFixed64(c);

			private static long DecodeCount(Slice v) => v.ToInt64();

			private Task<long> SlowCountAsync(IFdbReadOnlyTransaction trans, int level, Slice beginKey, Slice endKey)
			{
				if (level == -1)
				{
					return Task.FromResult(beginKey.IsPresent ? 1L : 0);
				}

				return trans
					.GetRange(this.Subspace.Encode(level, beginKey), this.Subspace.Encode(level, endKey))
					.Select(kv => DecodeCount(kv.Value))
					.SumAsync();
			}

			internal async Task SetupLevelsAsync(IFdbTransaction trans)
			{
				var ks = Enumerable.Range(0, MAX_LEVELS)
					.Select((l) => this.Subspace.Encode(l, Slice.Empty))
					.ToList();

				var res = await trans.GetValuesAsync(ks).ConfigureAwait(false);
				for (int l = 0; l < res.Length; l++)
				{
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

				var k = this.Subspace.Encode(level, key);
				//Console.WriteLine(k);
				//Console.WriteLine("GetPreviousNode(" + level + ", " + key + ")");
				//Console.WriteLine(KeySelector.LastLessThan(k) + " <= x < " + KeySelector.FirstGreaterOrEqual(k));
				var kv = await trans
					.Snapshot
					.GetRange(
						KeySelector.LastLessThan(k),
						KeySelector.FirstGreaterOrEqual(k)
					)
					.FirstAsync()
					.ConfigureAwait(false);
				//Console.WriteLine("Found " + FdbKey.Dump(kv.Key));

				var prevKey = this.Subspace.DecodeLast<Slice>(kv.Key);
				trans.AddReadConflictRange(kv.Key + FdbKey.MinValue, k);
				trans.AddReadConflictKey(this.Subspace.Encode(0, prevKey));
				return prevKey;
			}

			#endregion

		}

		public async ValueTask<State> Resolve(IFdbReadOnlyTransaction tr)
		{
			var subspace = await this.Location.Resolve(tr);
			return new State(subspace);
		}

	}

}
