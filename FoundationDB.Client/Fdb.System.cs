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
	using FoundationDB.Client.Utils;
	using System;
	using System.Collections.Generic;
	using System.Diagnostics;
	using System.Threading;
	using System.Threading.Tasks;

	public static partial class Fdb
	{

		/// <summary>Helper class for reading from the reserved System subspace</summary>
		public static class System
		{
			/// <summary>"\xFF\xFF"</summary>
			public static readonly Slice MaxValue = Slice.FromAscii("\xFF\xFF");

			/// <summary>"\xFF\x00"</summary>
			public static readonly Slice MinValue = Slice.FromAscii("\xFF\x00");

			/// <summary>"\xFF/conf/"</summary>
			public static readonly Slice ConfigPrefix = Slice.FromAscii("\xFF/conf/");

			/// <summary>"\xFF/coordinators"</summary>
			public static readonly Slice Coordinators = Slice.FromAscii("\xFF/coordinators");

			/// <summary>"\xFF/keyServer/(key_boundary)" => (..., node_id, ...)</summary>
			public static readonly Slice KeyServers = Slice.FromAscii("\xFF/keyServers/");

			/// <summary>"\xFF/serverKeys/(node_id)/(key_boundary)" => ('' | '1')</summary>
			public static readonly Slice ServerKeys = Slice.FromAscii("\xFF/serverKeys/");

			/// <summary>"\xFF/serverList/(node_id)" => (..., node_id, machine_id, datacenter_id, ...)</summary>
			public static readonly Slice ServerList = Slice.FromAscii("\xFF/serverList/");

			/// <summary>"\xFF/workers/(ip:port)/..." => datacenter + machine + mclass</summary>
			public static readonly Slice Workers = Slice.FromAscii("\xFF/workers/");

			/// <summary>Return the corresponding key for a config attribute</summary>
			/// <param name="name">"foo"</param>
			/// <returns>"\xFF/config/foo"</returns>
			public static Slice GetConfigKey(string name)
			{
				if (string.IsNullOrEmpty(name)) throw new ArgumentException("Config key cannot be null or empty", "name");
				return ConfigPrefix.Concat(Slice.FromAscii(name));
			}

			/// <summary>Returns a list of keys k such that <paramref name="begin"/> &lt;= k &lt; <paramref name="end"/> and k is located at the start of a contiguous range stored on a single server</summary>
			/// <param name="trans">Transaction to use for the operation</param>
			/// <param name="begin">First key (inclusive) of the range to inspect</param>
			/// <param name="end">End key (exclusive) of the range to inspect</param>
			/// <returns>List of keys that mark the start of a new chunk</returns>
			/// <remarks>This method is not transactional. It will return an answer no older than the Transaction object it is passed, but the returned boundaries are an estimate and may not represent the exact boundary locations at any database version.</remarks>
			public static async Task<List<Slice>> GetBoundaryKeysAsync(IFdbReadOnlyTransaction trans, Slice begin, Slice end)
			{
				if (trans == null) throw new ArgumentNullException("trans");
				Contract.Assert(trans.Context != null && trans.Context.Database != null);

				using (var shadow = trans.Context.Database.BeginReadOnlyTransaction(trans.Token))
				{
					// We don't want to change the state of the transaction, so we will create another one at the same read version
					var readVersion = await trans.GetReadVersionAsync().ConfigureAwait(false);
					shadow.SetReadVersion(readVersion);

					//TODO: we may need to also copy options like RetryLimit and Timeout ?

					return await GetBoundaryKeysInternalAsync(shadow, begin, end).ConfigureAwait(false);
				}
			}

			/// <summary>Returns a list of keys k such that <paramref name="begin"/> &lt;= k &lt; <paramref name="end"/> and k is located at the start of a contiguous range stored on a single server</summary>
			/// <param name="db">Database to use for the operation</param>
			/// <param name="begin">First key (inclusive) of the range to inspect</param>
			/// <param name="end">End key (exclusive) of the range to inspect</param>
			/// <param name="cancellationToken">Token used to cancel the operation</param>
			/// <returns>List of keys that mark the start of a new chunk</returns>
			/// <remarks>This method is not transactional. It will return an answer no older than the Database object it is passed, but the returned boundaries are an estimate and may not represent the exact boundary locations at any database version.</remarks>
			public static async Task<List<Slice>> GetBoundaryKeysAsync(IFdbDatabase db, Slice begin, Slice end, CancellationToken cancellationToken = default(CancellationToken))
			{
				if (db == null) throw new ArgumentNullException("db");

				using (var trans = db.BeginReadOnlyTransaction(cancellationToken))
				{
					return await GetBoundaryKeysInternalAsync(trans, begin, end).ConfigureAwait(false);
				}
			}

			public static Task<List<FdbKeyRange>> GetChunksAsync(IFdbDatabase db, FdbKeyRange range, CancellationToken cancellationToken = default(CancellationToken))
			{
				return GetChunksAsync(db, range.Begin, range.End, cancellationToken);
			}

			public static async Task<List<FdbKeyRange>> GetChunksAsync(IFdbDatabase db, Slice begin, Slice end, CancellationToken cancellationToken = default(CancellationToken))
			{
				var boundaries = await GetBoundaryKeysAsync(db, begin, end, cancellationToken).ConfigureAwait(false);

				var chunks = new List<FdbKeyRange>(boundaries.Count + 2);
				int count = boundaries.Count;
				if (boundaries.Count == 0)
				{
					chunks.Add(new FdbKeyRange(begin, end));
					return chunks;
				}

				var k = boundaries[0];
				if (k != begin) chunks.Add(new FdbKeyRange(begin, k));

				for (int i = 1; i < boundaries.Count; i++)
				{
					chunks.Add(new FdbKeyRange(k, boundaries[i]));
					k = boundaries[i];
				}

				if (k != end) chunks.Add(new FdbKeyRange(k, end));

				return chunks;
			}

			private static async Task<List<Slice>> GetBoundaryKeysInternalAsync(IFdbReadOnlyTransaction trans, Slice begin, Slice end)
			{
				Contract.Requires(trans != null);

				var results = new List<Slice>();
				while (begin < end)
				{
					FdbException error = null;
					Slice lastBegin = begin;
					try
					{
						trans.WithAccessToSystemKeys();
						var chunk = await trans.Snapshot.GetRangeAsync(KeyServers + begin, KeyServers + end).ConfigureAwait(false);

						if (chunk.Count > 0)
						{
							foreach (var kvp in chunk.Chunk)
							{
								results.Add(kvp.Key.Substring(KeyServers.Count));
							}
							begin = chunk.Last.Key.Substring(KeyServers.Count) + (byte)0;
						}
						if (!chunk.HasMore)
						{
							begin = end;
						}
					}
					catch (FdbException e)
					{
						error = e;
					}

					if (error != null)
					{
						if (error.Code == FdbError.PastVersion && begin != lastBegin)
						{ // if we get a PastVersion and *something* has happened, then we are no longer transactionnal
							trans.Reset();
						}
						else
						{
							await trans.OnErrorAsync(error.Code).ConfigureAwait(false);
						}
					}
				}

				return results;
			}

			/// <summary>Estimate the number of keys in the specified range.</summary>
			/// <param name="db">Database used for the operation</param>
			/// <param name="range">Range defining the keys to count</param>
			/// <param name="cancellationToken">Token used to cancel the operation</param>
			/// <returns>Number of keys k such that range.Begin &lt;= k &gt; range.End</returns>
			/// <remarks>If the range contains a large of number keys, the operation may need more than one transaction to complete, meaning that the number will not be transactionally accurate.</remarks>
			public static Task<long> EstimateCountAsync(IFdbDatabase db, FdbKeyRange range, CancellationToken cancellationToken = default(CancellationToken))
			{
				return EstimateCountAsync(db, range.Begin, range.End, cancellationToken);
			}

			/// <summary>Estimate the number of keys in the specified range.</summary>
			/// <param name="db">Database used for the operation</param>
			/// <param name="beginInclusive">Key defining the beginning of the range</param>
			/// <param name="endExclusive">Key defining the end of the range</param>
			/// <param name="cancellationToken">Token used to cancel the operation</param>
			/// <returns>Number of keys k such that <paramref name="beginInclusive"/> &lt;= k &gt; <paramref name="endExclusive"/></returns>
			/// <remarks>If the range contains a large of number keys, the operation may need more than one transaction to complete, meaning that the number will not be transactionally accurate.</remarks>
			public static async Task<long> EstimateCountAsync(IFdbDatabase db, Slice beginInclusive, Slice endExclusive, CancellationToken cancellationToken = default(CancellationToken))
			{
				const int INIT_WINDOW_SIZE = 1 << 10; // start at 1024
				const int MAX_WINDOW_SIZE = 1 << 16; // never use more than 65536

				if (db == null) throw new ArgumentNullException("db");
				if (endExclusive < beginInclusive) throw new ArgumentException("The end key cannot be less than the begin key", "endExclusive");

				cancellationToken.ThrowIfCancellationRequested();

				// To count the number of items in the range, we will scan it using a key selector with an offset equal to our window size
				// > if the returned key is still inside the range, we add the window size to the counter, and start again from the current key
				// > if the returned key is outside the range, we reduce the size of the window, and start again from the previous key
				// > if the returned key is exactly equal to the end of range, OR if the window size was 1, then we stop

				// Since we don't know in advance if the range contains 1 key or 1 Billion keys, choosing a good value for the window size is critical:
				// > if it is too small and the range is very large, we will need too many sequential reads and the network latency will quickly add up
				// > if it is too large and the range is small, we will spend too many times halving the window size until we get the correct value

				// A few optimizations are possible:
				// > we could start with a small window size, and then double its size on every full segment (up to a maximum)
				// > for the last segment, we don't need to wait for a GetKey to complete before issuing the next, so we could split the segment into 4 (or more), do the GetKeyAsync() in parallel, detect the quarter that cross the boundary, and iterate again until the size is small
				// > once the window size is small enough, we can switch to using GetRange to read the last segment in one shot, instead of iterating with window size 16, 8, 4, 2 and 1 (the wost case being 2^N - 1 items remaning)

				Debug.WriteLine("EstimateCount(" + FdbKey.Dump(beginInclusive) + " .. " + FdbKey.Dump(endExclusive) + "):");

				// note: we make a copy of the keys because the operation could take a long time and the key's could prevent a potentially large underlying buffer from being GCed
				var cursor = beginInclusive.Memoize();
				var end = endExclusive.Memoize();

				using (var tr = db.BeginReadOnlyTransaction(cancellationToken))
				{
					// start looking for the first key in the range
					cursor = await tr.Snapshot.GetKeyAsync(FdbKeySelector.FirstGreaterOrEqual(cursor)).ConfigureAwait(false);
					if (cursor >= end)
					{ // the range is empty !
						return 0;
					}

					// we already have seen one key, so add it to the count
					int iter = 1;
					int counter = 1;
					// start with a medium-sized window
					int windowSize = INIT_WINDOW_SIZE;
					bool last = false;

					while (cursor < end)
					{
						Contract.Assert(windowSize > 0);

						Slice next = Slice.Nil;
						FdbException error = null;
						try
						{
							var selector = FdbKeySelector.FirstGreaterOrEqual(cursor) + windowSize;
							Debug.WriteLine("> [" + counter + " + " + windowSize + "] " + selector);
							next = await tr.Snapshot.GetKeyAsync(selector).ConfigureAwait(false);
							++iter;
						}
						catch (FdbException e)
						{
							error = e;
						}

						if (error != null)
						{
							// => from this point, the count returned will not be transactionally accurate
							if (error.Code == FdbError.PastVersion)
							{ // the transaction used up its time window
								tr.Reset();
							}
							else
							{ // check to see if we can continue...
								await tr.OnErrorAsync(error.Code).ConfigureAwait(false);
							}
							// retry
							continue;
						}

						if (next > end)
						{ // we have reached past the end, switch to binary search

							last = true;

							// if window size is already 1, then we have counted everything (the range.End key does not exist in the db)
							if (windowSize == 1) break;

							//TODO: if we have less than, say, a hundred keys, switch to a simple get range....		
							windowSize >>= 1;
							Debug.WriteLine("  -> REWIND AND TRY AGAIN WITH STEP " + windowSize);
							continue;
						}

						// the range is not finished, advance the cursor
						counter += windowSize;
						cursor = next;

						if (!last)
						{ // double the size of the window if we are not in the last segment
							windowSize = Math.Min(windowSize << 1, MAX_WINDOW_SIZE);
						}
					}

					Debug.WriteLine("# Found " + counter + " keys in " + iter + " iterations.");

					return counter;
				}
			}

		}

	}
}
