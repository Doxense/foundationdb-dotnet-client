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

namespace FoundationDB.Layers.Queues
{
	using FoundationDB.Client;
	using FoundationDB.Layers.Tuples;
	using FoundationDB.Linq;
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using System.Threading;
	using System.Threading.Tasks;

	/// <summary>
	/// Provides a high-contention Queue class
	/// </summary>
	public class FdbQueue
	{
		// from https://github.com/FoundationDB/python-layers/blob/master/lib/queue.py

		// TODO: should we use a PRNG ? If two counter instances are created at the same moment, they could share the same seed ?
		private readonly Random Rng = new Random();

		/// <summary>
		/// Create a new object representing a binary large object (blob).
		/// Only keys within the subspace will be used by the object. 
		/// Other clients of the database should refrain from modifying the subspace.</summary>
		/// <param name="subspace">Subspace to be used for storing the blob data and metadata</param>
		public FdbQueue(FdbSubspace subspace, bool highContention)
		{
			if (subspace == null) throw new ArgumentNullException("subspace");

			this.Subspace = subspace;
			this.HighContention = highContention;

			this.ConflictedPop = subspace.Partition(Slice.FromAscii("pop"));
			this.ConflictedItem = subspace.Partition(Slice.FromAscii("conflict"));
			this.QueueItem = subspace.Partition(Slice.FromAscii("item"));
		}

		public FdbQueue(FdbSubspace subspace)
			: this(subspace, true)
		{ }

		/// <summary>Subspace used as a prefix for all items in this table</summary>
		public FdbSubspace Subspace { get; private set; }

		public bool HighContention { get; private set; }

		internal FdbSubspace ConflictedPop { get; private set; }

		internal FdbSubspace ConflictedItem { get; private set; }

		internal FdbSubspace QueueItem { get; private set; }

		/// <summary>
		/// Remove all items from the queue.
		/// </summary>
		public void ClearAsync(IFdbTransaction tr)
		{
			if (tr == null) throw new ArgumentNullException("tr");

			tr.ClearRange(this.Subspace);
		}

		/// <summary>
		/// Push a single item onto the queue.
		/// </summary>
		public async Task PushAsync(IFdbTransaction tr, Slice value, CancellationToken ct = default(CancellationToken))
		{
			long index = await GetNextIndexAsync(tr.ToSnapshotTransaction(), this.QueueItem, ct).ConfigureAwait(false);

			await PushAtAsync(tr, value, index, ct).ConfigureAwait(false);
		}

		/// <summary>
		/// Pop the next item from the queue. Cannot be composed with other functions in a single transaction.
		/// </summary>
		public Task<Slice> PopAsync(FdbDatabase db, CancellationToken ct = default(CancellationToken))
		{
			ct.ThrowIfCancellationRequested();

			if (this.HighContention)
			{
				return PopHighContentionAsync(db, ct);
			}
			else
			{
				return db.Attempt.ChangeAsync((tr, _ctx) => this.PopSimpleAsync(tr, _ctx.Token), ct);
			}
		}

		public async Task<bool> EmptyAsync(IFdbReadTransaction tr, CancellationToken ct = default(CancellationToken))
		{
			return !(await GetFirstItemAsync(tr, ct).ConfigureAwait(false)).Key.HasValue;
		}

		public async Task<Slice> PeekAsync(IFdbReadTransaction tr, CancellationToken ct = default(CancellationToken))
		{
			var firstItem = await GetFirstItemAsync(tr, ct).ConfigureAwait(false);
			if (!firstItem.Key.HasValue)
			{
				return Slice.Nil;
			}
			else
			{
				return firstItem.Value;
			}
		}

		#region Private Helpers...

		private Slice ConflictedItemKey(object subKey)
		{
			return this.ConflictedItem.Pack(subKey);
		}

		private Slice RandId()
		{
			lock (this.Rng)
			{
				return Slice.Random(this.Rng, 20);
			}
		}

		private async Task PushAtAsync(IFdbTransaction tr, Slice value, long index, CancellationToken ct)
		{
			// Items are pushed on the queue at an (index, randomID) pair. Items pushed at the
			// same time will have the same index, and so their ordering will be random.
    		// This makes pushes fast and usually conflict free (unless the queue becomes empty
			// during the push)

			Slice key = this.QueueItem.Pack(index, this.RandId());
			await tr.GetAsync(key, ct).ConfigureAwait(false);
			tr.Set(key, value);
		}

		private async Task<long> GetNextIndexAsync(IFdbReadTransaction tr, FdbSubspace subspace, CancellationToken ct)
		{
			var range = subspace.ToRange();

			var lastKey = await tr.GetKeyAsync(
				FdbKeySelector.LastLessThan(range.End),
				ct
			).ConfigureAwait(false);

			if (lastKey < range.Begin)
			{
				return 0;
			}

			return subspace.Unpack(lastKey).Get<long>(0) + 1;
		}

		private Task<KeyValuePair<Slice, Slice>> GetFirstItemAsync(IFdbReadTransaction tr, CancellationToken ct)
		{
			var range = this.QueueItem.ToRange();

			return tr.GetRange(range, new FdbRangeOptions { Limit = 1 }).SingleOrDefaultAsync(ct);
		}

		private async Task<Slice> PopSimpleAsync(IFdbTransaction tr, CancellationToken ct)
		{
			var firstItem = await GetFirstItemAsync(tr, ct).ConfigureAwait(false);
			if (!firstItem.Key.HasValue) return Slice.Nil;

			tr.Clear(firstItem.Key);
			return firstItem.Value;
		}

		private Task<Slice> AddConflictedPopAsync(FdbDatabase db, bool forced, CancellationToken ct)
		{
			return db.Attempt.ChangeAsync((tr, _ctx) => AddConflictedPopAsync(tr, forced, _ctx.Token), ct);
		}

		private async Task<Slice> AddConflictedPopAsync(IFdbTransaction tr, bool forced, CancellationToken ct)
		{
			long index = await GetNextIndexAsync(tr.ToSnapshotTransaction(), this.ConflictedPop, ct).ConfigureAwait(false);

			if (index == 0 && !forced)
			{
				return Slice.Nil;
			}

			Slice waitKey = this.ConflictedPop.Pack(index, this.RandId());
			await tr.GetAsync(waitKey).ConfigureAwait(false);
			tr.Set(waitKey, Slice.Empty);
			return waitKey;
		}

		private FdbRangeQuery GetWaitingPops(IFdbReadTransaction tr, int numPops)
		{
			var range = this.ConflictedPop.ToRange();
			return tr.GetRange(range, new FdbRangeOptions { Limit = numPops });
		}

		private FdbRangeQuery GetItems(IFdbReadTransaction tr, int numItems)
		{
			var range = this.QueueItem.ToRange();
			return tr.GetRange(range, new FdbRangeOptions { Limit = numItems });
		}

		private async Task<bool> FulfillConflictedPops(FdbDatabase db, CancellationToken ct)
		{
			int numPops = 100;

			ct.ThrowIfCancellationRequested();

			using(var tr = db.BeginTransaction())
			{
				var ts = await Task.WhenAll(
					GetWaitingPops(tr.Snapshot, numPops).ToListAsync(ct),
					GetItems(tr.Snapshot, numPops).ToListAsync(ct)
				).ConfigureAwait(false);

				var pops = ts[0];
				var items = ts[1];

				var tasks = new List<Task>(pops.Count);

				int i = 0;
				int n = Math.Min(pops.Count, items.Count);
				while (i < n)
				{
					var pop = pops[i];
					var kvp = items[i];

					var key = this.ConflictedPop.Unpack(pop.Key);
					var storageKey = this.ConflictedItemKey(key[1]);

					tr.Set(storageKey, kvp.Value);
					tasks.Add(tr.GetAsync(kvp.Key, ct));
					tasks.Add(tr.GetAsync(pop.Key, ct));
					tr.Clear(pop.Key);
					tr.Clear(kvp.Key);

					++i;
				}

				if (i < pops.Count)
				{
					while(i < pops.Count)
					{
						tasks.Add(tr.GetAsync(pops[i].Key, ct));
						tr.Clear(pops[i].Key);
						++i;
					}
				}

				// wait for all pending reads
				await Task.WhenAll(tasks).ConfigureAwait(false);

				// commit
				await tr.CommitAsync(ct).ConfigureAwait(false);

				return pops.Count < numPops;
			}
		}

		private async Task<Slice> PopHighContentionAsync(FdbDatabase db, CancellationToken ct)
		{
			int backOff = 10;
			Slice waitKey = Slice.Empty;

			ct.ThrowIfCancellationRequested();

			using(var tr = db.BeginTransaction())
			{
				FdbException error = null;
				try
				{
					// Check if there are other people waiting to be popped. If so, we cannot pop before them.
					waitKey = await AddConflictedPopAsync(tr, forced: false, ct: ct).ConfigureAwait(false);
					if (!waitKey.HasValue)
					{ // No one else was waiting to be popped
						var item = await PopSimpleAsync(tr, ct).ConfigureAwait(false);
						await tr.CommitAsync(ct).ConfigureAwait(false);
						return item;
					}
					else
					{
						await tr.CommitAsync(ct).ConfigureAwait(false);
					}
				}
				catch(FdbException e)
				{
					// note: cannot await inside a catch(..) block, so flag the error and process it below
					error = e;
				}

				if (error != null)
				{ // If we didn't succeed, then register our pop request
					waitKey = await AddConflictedPopAsync(db, forced: true, ct: ct).ConfigureAwait(false);
				}

				// The result of the pop will be stored at this key once it has been fulfilled
				var resultKey = ConflictedItemKey(this.ConflictedPop.UnpackLast<Slice>(waitKey));

				tr.Reset();

				// Attempt to fulfill outstanding pops and then poll the database 
				// checking if we have been fulfilled

				while(!ct.IsCancellationRequested)
				{
					error = null;
					try
					{
						while(!(await FulfillConflictedPops(db, ct).ConfigureAwait(false)))
						{
							//NOP ?
						}
					}
					catch(FdbException e)
					{
						// cannot await in catch(..) block so process it below
						error = e;
					}

					if (error != null && error.Code != FdbError.NotCommitted)
					{
						// If the error is 1020 (not_committed), then there is a good chance 
						// that somebody else has managed to fulfill some outstanding pops. In
						// that case, we proceed to check whether our request has been fulfilled.
						// Otherwise, we handle the error in the usual fashion.

						await tr.OnErrorAsync(error.Code, ct).ConfigureAwait(false);
						continue;
					}

					error = null;
					try
					{
						tr.Reset();

						var tmp = await tr.GetValuesAsync(new Slice[] { waitKey, resultKey }).ConfigureAwait(false);
						var value = tmp[0];
						var result = tmp[1];

						// If waitKey is present, then we have not been fulfilled
						if (value.HasValue)
						{
							await Task.Delay(backOff, ct).ConfigureAwait(false);
							backOff = Math.Min(1000, backOff * 2);
							continue;
						}

						if (result.IsNullOrEmpty)
						{
							return Slice.Nil;
						}

						tr.Clear(resultKey);
						await tr.CommitAsync(ct).ConfigureAwait(false);
						return result;

					}
					catch(FdbException e)
					{
						error = e;
					}

					if (error != null)
					{
						await tr.OnErrorAsync(error.Code, ct).ConfigureAwait(false);
					}
				}

				ct.ThrowIfCancellationRequested();
				// make the compiler happy
				throw new InvalidOperationException();
			}
		}

		#endregion
	}

}
