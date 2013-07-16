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

namespace FoundationDB.Client
{
	using System;
	using System.Collections.Generic;
	using System.IO;
	using System.Linq;
	using System.Threading;
	using System.Threading.Tasks;

	public static class FdbTransactionExtensions
	{

		internal static IFdbReadTransaction ToSnapshotTransaction(this IFdbReadTransaction trans)
		{
			if (trans.IsSnapshot) return trans;
			//TODO: better way at doing this ?

			if (trans is FdbTransaction) return (trans as FdbTransaction).Snapshot;

			throw new InvalidOperationException("This transaction is not in snapshot mode");
		}


		#region Set...

		public static void Set(this IFdbTransaction trans, Slice keyBytes, Stream data)
		{
			if (trans == null) throw new ArgumentNullException("trans");
			if (data == null) throw new ArgumentNullException("data");

			trans.EnsureCanReadOrWrite();

			Slice value = Slice.FromStream(data);

			trans.Set(keyBytes, value);
		}

		public static async Task SetAsync(this IFdbTransaction trans, Slice keyBytes, Stream data, CancellationToken ct = default(CancellationToken))
		{
			trans.EnsureCanReadOrWrite(ct);

			Slice value = await Slice.FromStreamAsync(data, ct).ConfigureAwait(false);

			trans.Set(keyBytes, value);
		}

		#endregion

		#region GetRange...

		public static FdbRangeQuery GetRange(this IFdbReadTransaction trans, FdbKeySelector beginInclusive, FdbKeySelector endExclusive, FdbRangeOptions options = null)
		{
			if (trans == null) throw new ArgumentNullException("trans");

			return trans.GetRange(FdbKeySelectorPair.Create(beginInclusive, endExclusive), options);
		}

		public static FdbRangeQuery GetRange(this IFdbReadTransaction trans, Slice beginInclusive, Slice endExclusive, FdbRangeOptions options = null)
		{
			if (beginInclusive.IsNullOrEmpty) beginInclusive = FdbKey.MinValue;
			if (endExclusive.IsNullOrEmpty) endExclusive = FdbKey.MaxValue;

			return trans.GetRange(
				FdbKeySelectorPair.Create(
					FdbKeySelector.FirstGreaterOrEqual(beginInclusive),
					FdbKeySelector.FirstGreaterOrEqual(endExclusive)
				),
				options
			);
		}

		public static FdbRangeQuery GetRangeInclusive(this IFdbReadTransaction trans, FdbKeySelector beginInclusive, FdbKeySelector endInclusive, FdbRangeOptions options = null)
		{
			if (trans == null) throw new ArgumentNullException("trans");

			return trans.GetRange(FdbKeySelectorPair.Create(beginInclusive, endInclusive + 1), options);
		}

		#endregion

		#region Batching...

		public static async Task<Slice[]> GetBatchValuesAsync(this IFdbReadTransaction trans, Slice[] keys, CancellationToken ct = default(CancellationToken))
		{
			if (keys == null) throw new ArgumentNullException("keys");

			trans.EnsureCanRead(ct);

			//TODO: we should maybe limit the number of concurrent requests, if there are too many keys to read at once ?

			var tasks = new List<Task<Slice>>(keys.Length);
			for (int i = 0; i < keys.Length; i++)
			{
				tasks.Add(trans.GetAsync(keys[i], ct));
			}

			var results = await Task.WhenAll(tasks).ConfigureAwait(false);

			return results;
		}

		public static Task<List<KeyValuePair<int, Slice>>> GetBatchIndexedAsync(this IFdbReadTransaction trans, IEnumerable<Slice> keys, CancellationToken ct = default(CancellationToken))
		{
			if (keys == null) throw new ArgumentNullException("keys");

			ct.ThrowIfCancellationRequested();
			return trans.GetBatchIndexedAsync(keys.ToArray(), ct);
		}

		public static async Task<List<KeyValuePair<int, Slice>>> GetBatchIndexedAsync(this IFdbReadTransaction trans, Slice[] keys, CancellationToken ct = default(CancellationToken))
		{
			var results = await trans.GetBatchValuesAsync(keys, ct).ConfigureAwait(false);

			return results
				.Select((data, i) => new KeyValuePair<int, Slice>(i, data))
				.ToList();
		}

		public static Task<List<KeyValuePair<Slice, Slice>>> GetBatchAsync(this IFdbReadTransaction trans, IEnumerable<Slice> keys, CancellationToken ct = default(CancellationToken))
		{
			if (keys == null) throw new ArgumentNullException("keys");

			ct.ThrowIfCancellationRequested();
			return trans.GetBatchAsync(keys.ToArray(), ct);
		}

		public static async Task<List<KeyValuePair<Slice, Slice>>> GetBatchAsync(this IFdbReadTransaction trans, Slice[] keys, CancellationToken ct = default(CancellationToken))
		{
			if (keys == null) throw new ArgumentNullException("keys");

			var indexedResults = await trans.GetBatchIndexedAsync(keys, ct).ConfigureAwait(false);

			ct.ThrowIfCancellationRequested();

			return indexedResults
				.Select((kvp) => new KeyValuePair<Slice, Slice>(keys[kvp.Key], kvp.Value))
				.ToList();
		}

		#endregion


	}
}
