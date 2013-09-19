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
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

	/// <summary>Wraps an FDB_TRANSACTION handle</summary>
	public partial class FdbDatabase
	{

		private BulkOperations m_bulkOperations;

		/// <summary>Returns a version of this transaction that perform snapshotted operations</summary>
		public BulkOperations Bulk
		{
			get
			{
				ThrowIfDisposed();
				return m_bulkOperations ?? (m_bulkOperations = new BulkOperations(this));
			}
		}

		/// <summary>Wrapper on a transaction, that will use Snmapshot mode on all read operations</summary>
		public sealed class BulkOperations
		{
			private readonly FdbDatabase m_parent;

			public BulkOperations(FdbDatabase parent)
			{
				if (parent == null) throw new ArgumentNullException("parent");
				m_parent = parent;
			}

			/// <summary>Insert a (large) sequence of key/value pairs into the database, by using as many transactions as necessary</summary>
			/// <param name="data">Sequence of key/value pairs</param>
			/// <param name="ct">Cancellation Token</param>
			/// <returns>Total number of values inserted in the database</returns>
			public async Task<long> InsertAsync(IEnumerable<KeyValuePair<Slice, Slice>> data, IProgress<long> progress = null, CancellationToken ct = default(CancellationToken))
			{
				if (data == null) throw new ArgumentNullException("data");

				if (ct.IsCancellationRequested) ct.ThrowIfCancellationRequested();

				// we will batch keys into chunks (bounding by count and bytes),
				// then attempt to insert that batch in the database.

				int maxBatchCount = 1000;
				int maxBatchSize = 10 * 1000;

				var chunk = new List<KeyValuePair<Slice, Slice>>();

				long items = 0;
				using (var iterator = data.GetEnumerator())
				{
					if (progress != null) progress.Report(0);

					while (!ct.IsCancellationRequested)
					{
						chunk.Clear();
						int bytes = 0;

						while (iterator.MoveNext())
						{
							var pair = iterator.Current;
							chunk.Add(pair);
							bytes += pair.Key.Count + pair.Value.Count;

							if (chunk.Count > maxBatchCount || bytes > maxBatchSize)
							{ // chunk is big enough
								break;
							}
						}

						if (chunk.Count == 0)
						{ // no more data, we are done
							break;
						}

						await m_parent.Change((tr) =>
						{
							foreach (var pair in chunk)
							{
								tr.Set(pair.Key, pair.Value);
							}
						}, ct).ConfigureAwait(false);

						items += chunk.Count;

						if (progress != null) progress.Report(items);
					}
				}

				if (ct.IsCancellationRequested) ct.ThrowIfCancellationRequested();

				return items;
			}

		}

	}

}
