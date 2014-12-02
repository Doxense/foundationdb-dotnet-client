#region BSD Licence
/* Copyright (c) 2013-2014, Doxense SAS
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
	using JetBrains.Annotations;
	using System;
	using System.Collections.Generic;
	using System.Threading;
	using System.Threading.Tasks;
	using FoundationDB.Async;

	/// <summary>
	/// Provides a high-contention Queue class
	/// </summary>
	public class FdbQueue<T>
	{
		// from https://github.com/FoundationDB/python-layers/blob/master/lib/queue.py

		// TODO: should we use a PRNG ? If two counter instances are created at the same moment, they could share the same seed ?
		private readonly Random Rng = new Random();

		/// <summary>Create a new High Contention Queue</summary>
		/// <param name="subspace">Subspace where the queue will be stored</param>
		/// <remarks>Uses the default Tuple serializer</remarks>
		public FdbQueue([NotNull] FdbSubspace subspace)
			: this(subspace, highContention: true, encoder: KeyValueEncoders.Tuples.Value<T>())
		{ }

		/// <summary>Create a new queue using either High Contention mode or Simple mode</summary>
		/// <param name="subspace">Subspace where the queue will be stored</param>
		/// <param name="highContention">If true, uses High Contention Mode (lots of popping clients). If true, uses the Simple Mode (a few popping clients).</param>
		/// <remarks>Uses the default Tuple serializer</remarks>
		public FdbQueue([NotNull] FdbSubspace subspace, bool highContention)
			: this(subspace, highContention: highContention, encoder: KeyValueEncoders.Tuples.Value<T>())
		{ }

		/// <summary>Create a new queue using either High Contention mode or Simple mode</summary>
		/// <param name="subspace">Subspace where the queue will be stored</param>
		/// <param name="highContention">If true, uses High Contention Mode (lots of popping clients). If true, uses the Simple Mode (a few popping clients).</param>
		public FdbQueue([NotNull] FdbSubspace subspace, bool highContention, [NotNull] IValueEncoder<T> encoder)
		{
			if (subspace == null) throw new ArgumentNullException("subspace");
			if (encoder == null) throw new ArgumentNullException("encocer");

			this.Subspace = subspace;
			this.HighContention = highContention;
			this.Encoder = encoder;

			this.ConflictedPop = subspace.Partition(Slice.FromAscii("pop"));
			this.ConflictedItem = subspace.Partition(Slice.FromAscii("conflict"));
			this.QueueItem = subspace.Partition(Slice.FromAscii("item"));
		}

		/// <summary>Subspace used as a prefix for all items in this table</summary>
		public FdbSubspace Subspace { [NotNull] get; private set; }

		/// <summary>If true, the queue is operating in High Contention mode that will scale better with a lot of popping clients.</summary>
		public bool HighContention { get; private set; }

		/// <summary>Serializer for the elements of the queue</summary>
		public IValueEncoder<T> Encoder { [NotNull] get; private set; }

		internal FdbSubspace ConflictedPop { get; private set; }

		internal FdbSubspace ConflictedItem { get; private set; }

		internal FdbSubspace QueueItem { get; private set; }

		/// <summary>Remove all items from the queue.</summary>
		public void Clear([NotNull] IFdbTransaction trans)
		{
			if (trans == null) throw new ArgumentNullException("trans");

			trans.ClearRange(this.Subspace);
		}

		/// <summary>Push a single item onto the queue.</summary>
		public async Task PushAsync([NotNull] IFdbTransaction trans, T value)
		{
			if (trans == null) throw new ArgumentNullException("trans");

#if DEBUG
			trans.Annotate("Push({0})", value);
#endif

			long index = await GetNextIndexAsync(trans.Snapshot, this.QueueItem).ConfigureAwait(false);

#if DEBUG
			trans.Annotate("Index = {0}", index);
#endif

			await PushAtAsync(trans, value, index).ConfigureAwait(false);
		}

		/// <summary>Pop the next item from the queue. Cannot be composed with other functions in a single transaction.</summary>
		public Task<Optional<T>> PopAsync([NotNull] IFdbDatabase db, CancellationToken cancellationToken)
		{
			if (db == null) throw new ArgumentNullException("db");

			if (cancellationToken.IsCancellationRequested)
			{
				return TaskHelpers.FromCancellation<Optional<T>>(cancellationToken);
			}

			if (this.HighContention)
			{
				return PopHighContentionAsync(db, cancellationToken);
			}
			else
			{
				return db.ReadWriteAsync((tr) => this.PopSimpleAsync(tr), cancellationToken);
			}
		}

		/// <summary>Test whether the queue is empty.</summary>
		public async Task<bool> EmptyAsync([NotNull] IFdbReadOnlyTransaction tr)
		{
			return (await GetFirstItemAsync(tr).ConfigureAwait(false)).Key.IsNull;
		}

		/// <summary>Get the value of the next item in the queue without popping it.</summary>
		public async Task<Optional<T>> PeekAsync([NotNull] IFdbReadOnlyTransaction tr)
		{
			var firstItem = await GetFirstItemAsync(tr).ConfigureAwait(false);
			if (firstItem.Key.IsNull)
			{
				return default(Optional<T>);
			}
			else
			{
				return this.Encoder.DecodeValue(firstItem.Value);
			}
		}

		#region Bulk Operations

		public Task ExportAsync(IFdbDatabase db, Action<T, long> handler, CancellationToken cancellationToken)
		{
			if (db == null) throw new ArgumentNullException("db");
			if (handler == null) throw new ArgumentNullException("handler");

			//REVIEW: is this approach correct ?

			return Fdb.Bulk.ExportAsync(
				db,
				this.QueueItem.ToRange(),
				(kvs, offset, ct) =>
				{
					foreach(var kv in kvs)
					{
						if (cancellationToken.IsCancellationRequested) cancellationToken.ThrowIfCancellationRequested();

						handler(this.Encoder.DecodeValue(kv.Value), offset);
						++offset;
					}
					return TaskHelpers.CompletedTask;
				},
				cancellationToken
			);
		}

		public Task ExportAsync(IFdbDatabase db, Func<T, long, Task> handler, CancellationToken cancellationToken)
		{
			if (db == null) throw new ArgumentNullException("db");
			if (handler == null) throw new ArgumentNullException("handler");

			//REVIEW: is this approach correct ?

			return Fdb.Bulk.ExportAsync(
				db,
				this.QueueItem.ToRange(),
				async (kvs, offset, ct) =>
				{
					foreach (var kv in kvs)
					{
						if (cancellationToken.IsCancellationRequested) cancellationToken.ThrowIfCancellationRequested();

						await handler(this.Encoder.DecodeValue(kv.Value), offset);
						++offset;
					}
				},
				cancellationToken
			);
		}

		public Task ExportAsync(IFdbDatabase db, Action<T[], long> handler, CancellationToken cancellationToken)
		{
			if (db == null) throw new ArgumentNullException("db");
			if (handler == null) throw new ArgumentNullException("handler");

			//REVIEW: is this approach correct ?

			return Fdb.Bulk.ExportAsync(
				db,
				this.QueueItem.ToRange(),
				(kvs, offset, ct) =>
				{
					handler(this.Encoder.DecodeRange(kvs), offset);
					return TaskHelpers.CompletedTask;
				},
				cancellationToken
			);
		}

		public Task ExportAsync(IFdbDatabase db, Func<T[], long, Task> handler, CancellationToken cancellationToken)
		{
			if (db == null) throw new ArgumentNullException("db");
			if (handler == null) throw new ArgumentNullException("handler");

			//REVIEW: is this approach correct ?

			return Fdb.Bulk.ExportAsync(
				db,
				this.QueueItem.ToRange(),
				(kvs, offset, ct) => handler(this.Encoder.DecodeRange(kvs), offset),
				cancellationToken
			);
		}

		#endregion

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

		private async Task PushAtAsync([NotNull] IFdbTransaction tr, T value, long index)
		{
			// Items are pushed on the queue at an (index, randomID) pair. Items pushed at the
			// same time will have the same index, and so their ordering will be random.
			// This makes pushes fast and usually conflict free (unless the queue becomes empty
			// during the push)

			Slice key = this.QueueItem.Pack(index, this.RandId());
			await tr.GetAsync(key).ConfigureAwait(false);
			tr.Set(key, this.Encoder.EncodeValue(value));
		}

		private async Task<long> GetNextIndexAsync([NotNull] IFdbReadOnlyTransaction tr, FdbSubspace subspace)
		{
			var range = subspace.ToRange();

			var lastKey = await tr.GetKeyAsync(FdbKeySelector.LastLessThan(range.End)).ConfigureAwait(false);

			if (lastKey < range.Begin)
			{
				return 0;
			}

			return subspace.Unpack(lastKey).Get<long>(0) + 1;
		}

		private Task<KeyValuePair<Slice, Slice>> GetFirstItemAsync([NotNull] IFdbReadOnlyTransaction tr)
		{
			var range = this.QueueItem.ToRange();
			return tr.GetRange(range).FirstOrDefaultAsync();
		}

		private async Task<Optional<T>> PopSimpleAsync([NotNull] IFdbTransaction tr)
		{
#if DEBUG
			tr.Annotate("PopSimple()");
#endif

			var firstItem = await GetFirstItemAsync(tr).ConfigureAwait(false);
			if (firstItem.Key.IsNull) return default(Optional<T>);

			tr.Clear(firstItem.Key);
			return this.Encoder.DecodeValue(firstItem.Value);
		}

		private Task<Slice> AddConflictedPopAsync([NotNull] IFdbDatabase db, bool forced, CancellationToken ct)
		{
			return db.ReadWriteAsync((tr) => AddConflictedPopAsync(tr, forced), ct);
		}

		private async Task<Slice> AddConflictedPopAsync([NotNull] IFdbTransaction tr, bool forced)
		{
			long index = await GetNextIndexAsync(tr.Snapshot, this.ConflictedPop).ConfigureAwait(false);

			if (index == 0 && !forced)
			{
				return Slice.Nil;
			}

			Slice waitKey = this.ConflictedPop.Pack(index, this.RandId());
			await tr.GetAsync(waitKey).ConfigureAwait(false);
			tr.Set(waitKey, Slice.Empty);
			return waitKey;
		}

		private Task<List<KeyValuePair<Slice, Slice>>> GetWaitingPopsAsync([NotNull] IFdbReadOnlyTransaction tr, int numPops)
		{
			var range = this.ConflictedPop.ToRange();
			return tr.GetRange(range, limit: numPops, reverse: false).ToListAsync();
		}

		private Task<List<KeyValuePair<Slice, Slice>>> GetItemsAsync([NotNull] IFdbReadOnlyTransaction tr, int numItems)
		{
			var range = this.QueueItem.ToRange();
			return tr.GetRange(range, limit: numItems, reverse: false).ToListAsync();
		}

		private async Task<bool> FulfillConflictedPops([NotNull] IFdbDatabase db, CancellationToken ct)
		{
			const int numPops = 100;

			using (var tr = db.BeginTransaction(ct))
			{
#if DEBUG
				tr.Annotate("FulfillConflictedPops");
#endif

				var ts = await Task.WhenAll(
					GetWaitingPopsAsync(tr.Snapshot, numPops),
					GetItemsAsync(tr.Snapshot, numPops)
				).ConfigureAwait(false);

				var pops = ts[0];
				var items = ts[1];
#if DEBUG
				tr.Annotate("pops: {0}, items: {1}", pops.Count, items.Count);
#endif

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
					//TODO: could this be replaced with a read conflict range ? (not async)
					tasks.Add(tr.GetAsync(kvp.Key));
					tasks.Add(tr.GetAsync(pop.Key));
					tr.Clear(pop.Key);
					tr.Clear(kvp.Key);

					++i;
				}

				if (i < pops.Count)
				{
					while (i < pops.Count)
					{
						//TODO: could this be replaced with a read conflict range ? (not async)
						tasks.Add(tr.GetAsync(pops[i].Key));
						tr.Clear(pops[i].Key);
						++i;
					}
				}

				// wait for all pending reads
				await Task.WhenAll(tasks).ConfigureAwait(false);

				// commit
				await tr.CommitAsync().ConfigureAwait(false);

				return pops.Count < numPops;
			}
		}

		private async Task<Optional<T>> PopHighContentionAsync([NotNull] IFdbDatabase db, CancellationToken ct)
		{
			int backOff = 10;
			Slice waitKey = Slice.Empty;

			ct.ThrowIfCancellationRequested();

			using (var tr = db.BeginTransaction(ct))
			{
#if DEBUG
				tr.Annotate("PopHighContention()");
#endif

				FdbException error = null;
				try
				{
					// Check if there are other people waiting to be popped. If so, we cannot pop before them.
					waitKey = await AddConflictedPopAsync(tr, forced: false).ConfigureAwait(false);
					if (waitKey.IsNull)
					{ // No one else was waiting to be popped
						var item = await PopSimpleAsync(tr).ConfigureAwait(false);
						await tr.CommitAsync().ConfigureAwait(false);
						return item;
					}
					else
					{
						await tr.CommitAsync().ConfigureAwait(false);
					}
				}
				catch (FdbException e)
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

				while (!ct.IsCancellationRequested)
				{
					error = null;
					try
					{
						while (!(await FulfillConflictedPops(db, ct).ConfigureAwait(false)))
						{
							//NOP ?
						}
					}
					catch (FdbException e)
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

						await tr.OnErrorAsync(error.Code).ConfigureAwait(false);
						continue;
					}

					error = null;
					try
					{
						tr.Reset();

						var sw = System.Diagnostics.Stopwatch.StartNew();

						var tmp = await tr.GetValuesAsync(new Slice[] { waitKey, resultKey }).ConfigureAwait(false);
						var value = tmp[0];
						var result = tmp[1];

						// If waitKey is present, then we have not been fulfilled
						if (value.HasValue)
						{
#if DEBUG
							tr.Annotate("Wait {0} ms : {1} / {2}", backOff, Environment.TickCount, sw.ElapsedTicks);
#endif
							//TODO: we should rewrite this using Watches !
							await Task.Delay(backOff, ct).ConfigureAwait(false);
#if DEBUG
							tr.Annotate("After wait : {0} / {1}", Environment.TickCount, sw.ElapsedTicks);
#endif
							backOff = Math.Min(1000, backOff * 2);
							continue;
						}

						if (result.IsNullOrEmpty)
						{
							return default(Optional<T>);
						}

						tr.Clear(resultKey);
						await tr.CommitAsync().ConfigureAwait(false);
						return this.Encoder.DecodeValue(result);

					}
					catch (FdbException e)
					{
						error = e;
					}

					if (error != null)
					{
						await tr.OnErrorAsync(error.Code).ConfigureAwait(false);
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
