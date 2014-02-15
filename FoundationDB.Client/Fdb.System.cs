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
				while(begin < end)
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
					catch(FdbException e)
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
		
		}

	}

}
