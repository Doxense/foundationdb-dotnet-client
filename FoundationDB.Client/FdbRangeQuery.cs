#region Copyright (c) 2023-2024 SnowBank SAS, (c) 2005-2023 Doxense SAS
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

namespace FoundationDB.Client
{
	using System.Diagnostics;
	using Doxense.Linq;

	/// <summary>Query describing an ongoing GetRange operation</summary>
	[DebuggerDisplay("Begin={Begin}, End={End}, Limit={Limit}, Mode={Streaming}, Reverse={Reverse}, Snapshot={IsSnapshot}")]
	[PublicAPI]
	internal class FdbRangeQuery<TState, TResult> : IFdbRangeQuery<TResult>
	{

		/// <summary>Construct a query with a set of initial settings</summary>
		internal FdbRangeQuery(IFdbReadOnlyTransaction transaction, KeySelector begin, KeySelector end, TState? state, Func<TState>? stateFactory, FdbKeyValueDecoder<TState, TResult> decoder, bool snapshot, FdbRangeOptions? options)
		{
			Contract.Debug.Requires(transaction != null && decoder != null);

			this.Transaction = transaction;
			this.Begin = begin;
			this.End = end;
			this.State = state;
			this.StateFactory = stateFactory;
			this.Decoder = decoder;
			this.IsSnapshot = snapshot;
			this.Options = options ?? new FdbRangeOptions();
			this.OriginalRange = KeySelectorPair.Create(begin, end);
		}

		/// <summary>Copy constructor</summary>
		private FdbRangeQuery(FdbRangeQuery<TState, TResult> query, FdbRangeOptions options)
		{
			Contract.Debug.Requires(query != null && options != null);

			this.Transaction = query.Transaction;
			this.Begin = query.Begin;
			this.End = query.End;
			this.State = query.State;
			this.StateFactory = query.StateFactory;
			this.Decoder = query.Decoder;
			this.IsSnapshot = query.IsSnapshot;
			this.Options = options;
			this.OriginalRange = query.OriginalRange;
		}

		#region Public Properties...

		/// <summary>Key selector describing the beginning of the range that will be queried</summary>
		public KeySelector Begin { get; private set; }

		/// <summary>Key selector describing the end of the range that will be queried</summary>
		public KeySelector End { get; private set; }

		/// <summary>Key selector pair describing the beginning and end of the range that will be queried</summary>
		public KeySelectorPair Range => new(this.Begin, this.End);

		/// <summary>Stores all the settings for this range query</summary>
		public FdbRangeOptions Options { get; }

		/// <summary>Original key selector pair describing the bounds of the parent range. All the results returned by the query will be bounded by this original range.</summary>
		/// <remarks>May differ from <see cref="Range"/> when combining certain operators.</remarks>
		internal KeySelectorPair OriginalRange { get; }

		/// <summary>Limit in number of rows to return</summary>
		public int? Limit => this.Options.Limit;

		/// <summary>Limit in number of bytes to return</summary>
		public int? TargetBytes => this.Options.TargetBytes;

		/// <summary>Streaming mode to enable or disable prefetching</summary>
		/// <remarks>
		/// <para>This setting changes how many items will be returned in the first batch but the storage servers</para>
		/// <para>Queries that will return all the data in the range should use <see cref="FdbStreamingMode.WantAll"/>, while queries that will only take a fraction should use <see cref="FdbStreamingMode.Iterator"/></para>
		/// </remarks>
		public FdbStreamingMode Streaming => this.Options.Streaming ?? FdbStreamingMode.Iterator;

		/// <summary>Read mode</summary>
		/// <remarks>
		/// <para>Queries that need to decode both keys and values should use <see cref="FdbFetchMode.KeysAndValues"/>, while queries that need only one or the other should use either <see cref="FdbFetchMode.KeysOnly"/> or <see cref="FdbFetchMode.ValuesOnly"/></para>
		/// </remarks>
		public FdbFetchMode Fetch => this.Options.Fetch ?? FdbFetchMode.KeysAndValues;

		/// <summary>Should we perform the range using snapshot mode ?</summary>
		public bool IsSnapshot { get; }

		/// <summary>Should the results be returned in reverse order (from last key to first key)</summary>
		public bool IsReversed => this.Options.IsReversed;

		/// <summary>Parent transaction used to perform the GetRange operation</summary>
		public IFdbReadOnlyTransaction Transaction { get; }

		internal TState? State { get; }

		internal Func<TState>? StateFactory { get; }

		/// <summary>Transformation applied to the result</summary>
		internal FdbKeyValueDecoder<TState, TResult> Decoder { get; }

		#endregion

		#region Fluent API

		/// <summary>Only return up to a specific number of results</summary>
		/// <param name="count">Maximum number of results to return</param>
		/// <returns>A new query object that will only return up to <paramref name="count"/> results when executed</returns>
		[Pure]
		public IFdbRangeQuery<TResult> Take([Positive] int count)
		{
			Contract.Positive(count);

			if (this.Options.Limit == count)
			{
				return this;
			}

			return new FdbRangeQuery<TState, TResult>(
				this,
				this.Options with { Limit = count }
			);
		}

		/// <summary>Bypasses a specified number of elements in a sequence and then returns the remaining elements.</summary>
		/// <param name="count"></param>
		/// <returns>A new query object that will skip the first <paramref name="count"/> results when executed</returns>
		[Pure]
		public IFdbRangeQuery<TResult> Skip([Positive] int count)
		{
			Contract.Positive(count);

			var limit = this.Options.Limit;
			var begin = this.Begin;
			var end = this.End;

			// Take(N).Skip(k) ?
			if (limit.HasValue)
			{
				// If k >= N, then the result will be empty
				// If k < N, then we need to update the Begin key, and limit accordingly
				if (count >= limit.Value)
				{
					limit = 0; // hopefully this would be optimized at runtime?
				}
				else
				{
					limit -= count;
				}
			}

			if (this.IsReversed)
			{
				end -= count;
			}
			else
			{
				begin += count;
			}

			return new FdbRangeQuery<TState, TResult>(
				this,
				this.Options with { Limit = limit }
			)
			{
				Begin = begin,
				End = end,
			};
		}

		IFdbPagedQuery<TResult> IFdbRangeQuery<TResult>.Paged() => this.Paged();

		/// <inheritdoc cref="IFdbRangeQuery{TResult}.Paged"/>
		public IFdbPagedQuery<TResult> Paged() => new FdbPagedQuery<TState, TResult>(
			this.Transaction,
			this.Begin,
			this.End,
			this.State,
			this.StateFactory,
			this.Decoder,
			this.IsSnapshot,
			this.Options
		);

		IFdbRangeQuery<TResult> IFdbRangeQuery<TResult>.Reverse() => Reverse();

		/// <inheritdoc cref="IFdbRangeQuery{TResult}.Reverse"/>
		[Pure]
		public FdbRangeQuery<TState, TResult> Reverse()
		{
			var begin = this.Begin;
			var end = this.End;
			var limit = this.Options.Limit;
			if (limit.HasValue)
			{
				// If Take() of Skip() have been called, we need to update the end bound when reversing (or begin if already reversed)
				if (!this.IsReversed)
				{
					end = this.Begin + limit.Value;
				}
				else
				{
					begin = this.End - limit.Value;
				}
			}

			return new(
				this,
				this.Options with { IsReversed = !this.IsReversed }
			)
			{
				Begin = begin,
				End = end,
			};
		}

		/// <summary>Use a specific target bytes size</summary>
		/// <param name="bytes"></param>
		/// <returns>A new query object that will use the specified target bytes size when executed</returns>
		[Pure]
		public IFdbRangeQuery<TResult> WithTargetBytes([Positive] int bytes)
		{
			Contract.Positive(bytes);

			return new FdbRangeQuery<TState, TResult>(
				this,
				this.Options with { TargetBytes = bytes }
			);
		}

		/// <summary>Use a different Streaming Mode</summary>
		/// <param name="mode">Streaming mode to use when reading the results from the database</param>
		/// <returns>A new query object that will use the specified streaming mode when executed</returns>
		[Pure]
		public IFdbRangeQuery<TResult> WithMode(FdbStreamingMode mode)
		{
			if (!Enum.IsDefined(typeof(FdbStreamingMode), mode))
			{
				throw new ArgumentOutOfRangeException(nameof(mode), "Unsupported streaming mode");
			}

			return new FdbRangeQuery<TState, TResult>(
				this,
				this.Options with { Streaming = mode }
			);
		}

		/// <summary>Force the query to use a specific transaction</summary>
		/// <param name="transaction">Transaction to use when executing this query</param>
		/// <returns>A new query object that will use the specified transaction when executed</returns>
		[Pure]
		public IFdbRangeQuery<TResult> UseTransaction(IFdbReadOnlyTransaction transaction)
		{
			Contract.NotNull(transaction);

			return new FdbRangeQuery<TState, TResult>(
				transaction,
				this.Begin,
				this.End,
				this.State,
				this.StateFactory,
				this.Decoder,
				this.IsSnapshot,
				this.Options with { }
			);
		}

		#endregion

		#region Pseudo-LINQ

		//note: these methods are more optimized than regular AsyncLINQ methods, in that they can customize the query settings to return the least data possible over the network.
		// ex: FirstOrDefault can set the Limit to 1, LastOrDefault can set Reverse to true, etc...

		public IAsyncEnumerator<TResult> GetAsyncEnumerator(CancellationToken ct)
		{
			return GetAsyncEnumerator(ct, AsyncIterationHint.Default);
		}

		public IAsyncEnumerator<TResult> GetAsyncEnumerator(CancellationToken ct, AsyncIterationHint hint)
		{
			return new FdbResultIterator<TState, TResult>(this, GetState()).GetAsyncEnumerator(ct, hint);
		}

		/// <summary>Returns a list of all the elements of the range results</summary>
		public Task<List<TResult>> ToListAsync()
		{
			// ReSharper disable once InvokeAsExtensionMethod
			return AsyncEnumerable.ToListAsync(this, this.Transaction.Cancellation);
		}

		/// <summary>Returns a list of all the elements of the range results</summary>
		[Obsolete("The transaction already contains a cancellation token")]
		public Task<List<TResult>> ToListAsync(CancellationToken ct)
		{
			//TODO: REVIEW: this method creates a lot of false positives on the rule that detect an overload that accepts a cancellation token, even though there is already one embedded in the source transaction.
			// => ex: "tr.GetRange(....).Select(...).ToListAsync()" will have a hint on ToListAsync() that proposes to pass a cancellation token, which is most probably already used by the transaction.
			// Should we simply remove this overload? What are the use cases where the caller must use a _different_ token here than the one from the transaction??

			// ReSharper disable once InvokeAsExtensionMethod
			return AsyncEnumerable.ToListAsync(this, ct);
		}

		/// <summary>Returns an array with all the elements of the range results</summary>
		public Task<TResult[]> ToArrayAsync()
		{
			// ReSharper disable once InvokeAsExtensionMethod
			return AsyncEnumerable.ToArrayAsync(this, this.Transaction.Cancellation);
		}

		/// <summary>Returns an array with all the elements of the range results</summary>
		[Obsolete("The transaction already contains a cancellation token")]
		public Task<TResult[]> ToArrayAsync(CancellationToken ct)
		{
			//TODO: REVIEW: this method creates a lot of false positives on the rule that detect an overload that accepts a cancellation token, even though there is already one embedded in the source transaction.
			// => ex: "tr.GetRange(....).Select(...).ToArrayAsync()" will have a hint on ToArrayAsync() that proposes to pass a cancellation token, which is most probably already used by the transaction.
			// Should we simply remove this overload? What are the use cases where the caller must use a _different_ token here than the one from the transaction??

			// ReSharper disable once InvokeAsExtensionMethod
			return AsyncEnumerable.ToArrayAsync(this, ct);
		}

		/// <inheritdoc />
		public Task<Dictionary<TKey, TValue>> ToDictionary<TKey, TValue>(Func<TResult, TKey> keySelector, Func<TResult, TValue> valueSelector, IEqualityComparer<TKey>? keyComparer = null)
			where TKey : notnull
		{
			// ReSharper disable once InvokeAsExtensionMethod
			return AsyncEnumerable.ToDictionaryAsync(this, keySelector, valueSelector, keyComparer, this.Transaction.Cancellation);
		}

		/// <summary>Returns the number of elements in the range, by reading them</summary>
		/// <remarks>This method has to read all the keys and values, which may exceed the lifetime of a transaction. Please consider using <see cref="Fdb.System.EstimateCountAsync(FoundationDB.Client.IFdbDatabase,FoundationDB.Client.KeyRange,System.Threading.CancellationToken)"/> when reading potentially large ranges.</remarks>
		public Task<int> CountAsync()
		{
			// ReSharper disable once InvokeAsExtensionMethod
			return AsyncEnumerable.CountAsync(this, this.Transaction.Cancellation);
		}

		[Pure]
		internal FdbRangeQuery<TState, TOther> Map<TOther>(Func<TResult, TOther> lambda)
		{
			Contract.Debug.Requires(lambda != null);

			return new FdbRangeQuery<TState, TOther>(
				this.Transaction,
				this.Begin,
				this.End,
				this.State,
				this.StateFactory,
				Combine(this.Decoder, lambda),
				this.IsSnapshot,
				this.Options with { }
			);

			static FdbKeyValueDecoder<TState, TOther> Combine(FdbKeyValueDecoder<TState, TResult> decoder, Func<TResult, TOther> transform)
			{
				return (s, k, v) => transform(decoder(s, k, v));
			}
		}

		/// <summary>Projects each element of the range results into a new form.</summary>
		/// <param name="lambda">Function that is invoked for each source element, and will return the corresponding transformed element.</param>
		/// <returns>New range query that outputs the sequence of transformed elements</returns>
		/// <example><c>query.Select((kv) => $"{kv.Key:K} = {kv.Value:V}")</c></example>
		[Pure]
		public IFdbRangeQuery<TOther> Select<TOther>(Func<TResult, TOther> lambda)
		{
			Contract.NotNull(lambda);
			return Map<TOther>(lambda);
		}

		/// <summary>Projects each element of the range results into a new form.</summary>
		/// <param name="lambda">Function that is invoked with both the element value and its index in the sequence (0-based), and will return the corresponding transformed element.</param>
		/// <returns>New range query that outputs the sequence of transformed elements</returns>
		/// <example><c>query.Select((kv, i) => $"#{i}: {kv.Key:K} = {kv.Value:V}")</c></example>
		[Pure]
		public IFdbRangeQuery<TOther> Select<TOther>(Func<TResult, int, TOther> lambda)
		{
			Contract.Debug.Requires(lambda != null);

			return new FdbRangeQuery<TState, TOther>(
				this.Transaction,
				this.Begin,
				this.End,
				this.State,
				this.StateFactory,
				Combine(this.Decoder, lambda),
				this.IsSnapshot,
				this.Options with { }
			);

			static FdbKeyValueDecoder<TState, TOther> Combine(FdbKeyValueDecoder<TState, TResult> transform, Func<TResult, int, TOther> lambda)
			{
				Contract.Debug.Assert(transform != null);
				int counter = 0;
				return (s, k, v) => lambda(transform(s, k, v), counter++);
			}
		}

		/// <summary>Filters the range results based on a predicate.</summary>
		/// <remarks>Caution: filtering occurs on the client side !</remarks>
		/// <example><c>query.Where((kv) => kv.Key.StartsWith(prefix))</c> or <c>query.Where((kv) => !kv.Value.IsNull)</c></example>
		[Pure]
		public IAsyncEnumerable<TResult> Where(Func<TResult, bool> predicate)
		{
			return AsyncEnumerable.Where(this, predicate);
		}

		/// <summary>Returns the first result of the query, or the default for this type if the query yields no results.</summary>
		public Task<TResult> FirstOrDefaultAsync()
		{
			// we can optimize this by passing Limit=1
			return HeadAsync(single: false, orDefault: true);
		}

		/// <summary>Returns the first result of the query, or an exception if the query yields no result.</summary>
		/// <exception cref="InvalidOperationException">If the query yields no result</exception>
		public Task<TResult> FirstAsync()
		{
			// we can optimize this by passing Limit=1
			return HeadAsync(single: false, orDefault: false);
		}

		/// <summary>Returns the last result of the query, or the default for this type if the query yields no results.</summary>
		public Task<TResult> LastOrDefaultAsync()
		{
			//BUGBUG: if there is a Take(N) on the query, Last() will mean "The Nth key" and not the "last key in the original range".

			// we can optimize by reversing the current query and calling FirstOrDefault !
			return this.Reverse().HeadAsync(single:false, orDefault:true);
		}

		/// <summary>Returns the last result of the query, or an exception if the query yields no result.</summary>
		/// <exception cref="InvalidOperationException">If the query yields no result</exception>
		public Task<TResult> LastAsync()
		{
			//BUGBUG: if there is a Take(N) on the query, Last() will mean "The Nth key" and not the "last key in the original range".

			// we can optimize this by reversing the current query and calling First !
			return this.Reverse().HeadAsync(single: false, orDefault:false);
		}

		/// <summary>Returns the only result of the query, the default for this type if the query yields no results, or an exception if it yields two or more results.</summary>
		/// <exception cref="InvalidOperationException">If the query yields two or more results</exception>
		public Task<TResult> SingleOrDefaultAsync()
		{
			// we can optimize this by passing Limit=2
			return HeadAsync(single: true, orDefault: true);
		}

		/// <summary>Returns the only result of the query, or an exception if it yields either zero, or more than one result.</summary>
		/// <exception cref="InvalidOperationException">If the query yields two or more results</exception>
		public Task<TResult> SingleAsync()
		{
			// we can optimize this by passing Limit=2
			return HeadAsync(single: true, orDefault: false);
		}

		/// <summary>Returns <see langword="true"/> if the range query yields at least one element, or <see langword="false"/> if there was no result.</summary>
		public Task<bool> AnyAsync()
		{
			// we can optimize this by using Limit = 1
			return AnyOrNoneAsync(any: true);
		}

		/// <summary>Returns <see langword="true"/> if the range query does not yield any result, or <see langword="false"/> if there was at least one result.</summary>
		/// <remarks>This is a convenience method that is there to help porting layer code from other languages. This is strictly equivalent to calling "!(await query.AnyAsync())".</remarks>
		public Task<bool> NoneAsync()
		{
			// we can optimize this by using Limit = 1
			return AnyOrNoneAsync(any: false);
		}

		/// <summary>Executes an action on each of the range results</summary>
		public Task ForEachAsync(Action<TResult> action)
		{
			// ReSharper disable once InvokeAsExtensionMethod
			return AsyncEnumerable.ForEachAsync(this, action, this.Transaction.Cancellation);
		}

		/// <summary>Executes an action on each of the range results</summary>
		public async Task<TAggregate> ForEachAsync<TAggregate>(TAggregate aggregate, Action<TAggregate, TResult> action)
		{
			// ReSharper disable once InvokeAsExtensionMethod
			await foreach (var item in this.ConfigureAwait(false))
			{
				action(aggregate, item);
			}
			return aggregate;
		}

		protected TState GetState() => this.StateFactory != null ? this.StateFactory() : this.State!;

		internal async Task<TResult> HeadAsync(bool single, bool orDefault)
		{
			// Optimized code path for First/Last/Single variants where we can be smart and only ask for 1 or 2 results from the db

			// we can use the EXACT streaming mode with Limit = 1|2, and it will work if TargetBytes is 0
			if ((this.TargetBytes ?? 0) != 0 || (this.Streaming != FdbStreamingMode.Iterator && this.Streaming != FdbStreamingMode.Exact))
			{ // fallback to the default implementation
				return await AsyncEnumerable.Head(this, single, orDefault, this.Transaction.Cancellation).ConfigureAwait(false);
			}

			//BUGBUG: do we need special handling if OriginalRange != Range ? (weird combinations of Take/Skip and Reverse)

			var tr = this.IsSnapshot ? this.Transaction.Snapshot : this.Transaction;

			var results = await tr.GetRangeAsync<TState, TResult>(
				this.Begin,
				this.End,
				GetState(),
				this.Decoder,
				new FdbRangeOptions()
				{
					Limit = Math.Min(single ? 2 : 1, this.Options.Limit ?? int.MaxValue),
					IsReversed = this.IsReversed,
					Streaming = FdbStreamingMode.Exact,
				},
				iteration: 0
			).ConfigureAwait(false);

			if (results.IsEmpty)
			{ // no result
				if (!orDefault) throw new InvalidOperationException("The range was empty");
				return default!;
			}

			if (single && results.Count > 1)
			{ // there was more than one result
				throw new InvalidOperationException("The range contained more than one element");
			}

			// we have a result
			return results[0];
		}

		internal async Task<bool> AnyOrNoneAsync(bool any)
		{
			// Optimized code path for Any/None where we can be smart and only ask for 1 from the db

			// we can use the EXACT streaming mode with Limit = 1, and it will work if TargetBytes is 0
			if ((this.TargetBytes ?? 0) != 0 || (this.Streaming != FdbStreamingMode.Iterator && this.Streaming != FdbStreamingMode.Exact))
			{ // fallback to the default implementation
				// ReSharper disable InvokeAsExtensionMethod
				return any
					? await AsyncEnumerable.AnyAsync(this, this.Transaction.Cancellation).ConfigureAwait(false)
					: await AsyncEnumerable.NoneAsync(this, this.Transaction.Cancellation).ConfigureAwait(false);
				// ReSharper restore InvokeAsExtensionMethod
			}

			//BUGBUG: do we need special handling if OriginalRange != Range ? (weird combinations of Take/Skip and Reverse)

			var tr = this.IsSnapshot ? this.Transaction.Snapshot : this.Transaction;
			var results = await tr.GetRangeAsync(
				this.Begin,
				this.End,
				new FdbRangeOptions()
				{
					Limit = 1,
					IsReversed = this.IsReversed,
					Streaming = FdbStreamingMode.Exact,
				},
				iteration: 0
			).ConfigureAwait(false);

			return any ? !results.IsEmpty : results.IsEmpty;
		}

		#endregion

		/// <summary>Returns a human-readable representation of this query</summary>
		public override string ToString()
		{
			return $"Range({this.Range}, {this.Limit}, {(this.IsReversed ? "reverse" : "forward")})";
		}

	}
}
