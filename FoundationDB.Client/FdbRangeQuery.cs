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
	using System;
	using System.Collections.Generic;
	using System.Diagnostics;
	using System.Threading;
	using System.Threading.Tasks;
	using Doxense.Diagnostics.Contracts;
	using Doxense.Linq;
	using JetBrains.Annotations;

	/// <summary>Query describing an ongoing GetRange operation</summary>
	[DebuggerDisplay("Begin={Begin}, End={End}, Limit={Limit}, Mode={Mode}, Reverse={Reverse}, Snapshot={Snapshot}")]
	[PublicAPI]
	public sealed partial class FdbRangeQuery<T> : IConfigurableAsyncEnumerable<T>
	{

		/// <summary>Construct a query with a set of initial settings</summary>
		internal FdbRangeQuery(IFdbReadOnlyTransaction transaction, KeySelector begin, KeySelector end, Func<KeyValuePair<Slice, Slice>, T> transform, bool snapshot, FdbRangeOptions? options)
		{
			Contract.Debug.Requires(transaction != null && transform != null);

			this.Transaction = transaction;
			this.Begin = begin;
			this.End = end;
			this.Transform = transform;
			this.Snapshot = snapshot;
			this.Options = options ?? new FdbRangeOptions();
			this.OriginalRange = KeySelectorPair.Create(begin, end);
		}

		/// <summary>Copy constructor</summary>
		private FdbRangeQuery(FdbRangeQuery<T> query, FdbRangeOptions options)
		{
			Contract.Debug.Requires(query != null && options != null);

			this.Transaction = query.Transaction;
			this.Begin = query.Begin;
			this.End = query.End;
			this.Transform = query.Transform;
			this.Snapshot = query.Snapshot;
			this.Options = options;
			this.OriginalRange = query.OriginalRange;
		}

		#region Public Properties...

		/// <summary>Key selector describing the beginning of the range that will be queried</summary>
		public KeySelector Begin { get; private set; }

		/// <summary>Key selector describing the end of the range that will be queried</summary>
		public KeySelector End { get; private set; }

		/// <summary>Key selector pair describing the beginning and end of the range that will be queried</summary>
		public KeySelectorPair Range => new KeySelectorPair(this.Begin, this.End);

		/// <summary>Stores all the settings for this range query</summary>
		internal FdbRangeOptions Options { get; }

		/// <summary>Original key selector pair describing the bounds of the parent range. All the results returned by the query will be bounded by this original range.</summary>
		/// <remarks>May differ from <see cref="Range"/> when combining certain operators.</remarks>
		internal KeySelectorPair OriginalRange { get; }

		/// <summary>Limit in number of rows to return</summary>
		public int? Limit => this.Options.Limit;

		/// <summary>Limit in number of bytes to return</summary>
		public int? TargetBytes => this.Options.TargetBytes;

		/// <summary>Streaming mode</summary>
		public FdbStreamingMode Mode => this.Options.Mode ?? FdbStreamingMode.Iterator;

		/// <summary>Read mode</summary>
		public FdbReadMode Read => this.Options.Read ?? FdbReadMode.Both;

		/// <summary>Should we perform the range using snapshot mode ?</summary>
		public bool Snapshot { get; }

		/// <summary>Should the results be returned in reverse order (from last key to first key)</summary>
		public bool Reversed => this.Options.Reverse ?? false;

		/// <summary>Parent transaction used to perform the GetRange operation</summary>
		internal IFdbReadOnlyTransaction Transaction { get; }

		/// <summary>Transformation applied to the result</summary>
		internal Func<KeyValuePair<Slice, Slice>, T> Transform { get; }

		#endregion

		#region Fluent API

		/// <summary>Only return up to a specific number of results</summary>
		/// <param name="count">Maximum number of results to return</param>
		/// <returns>A new query object that will only return up to <paramref name="count"/> results when executed</returns>
		[Pure]
		public FdbRangeQuery<T> Take([Positive] int count)
		{
			Contract.Positive(count);

			if (this.Options.Limit == count)
			{
				return this;
			}

			return new FdbRangeQuery<T>(
				this,
				new FdbRangeOptions(this.Options) { Limit = count }
			);
		}

		/// <summary>Bypasses a specified number of elements in a sequence and then returns the remaining elements.</summary>
		/// <param name="count"></param>
		/// <returns>A new query object that will skip the first <paramref name="count"/> results when executed</returns>
		[Pure]
		public FdbRangeQuery<T> Skip([Positive] int count)
		{
			Contract.Positive(count);

			var limit = this.Options.Limit;
			var begin = this.Begin;
			var end = this.End;

			// Take(N).Skip(k) ?
			if (limit.HasValue)
			{
				// If k >= N, then the result will be empty
				// If k < N, then we need to update the begin key, and limit accordingly
				if (count >= limit.Value)
				{
					limit = 0; // hopefully this would be optimized an runtime?
				}
				else
				{
					limit -= count;
				}
			}

			if (this.Reversed)
			{
				end = end - count;
			}
			else
			{
				begin = begin + count;
			}

			return new FdbRangeQuery<T>(
				this,
				new FdbRangeOptions(this.Options) { Limit = limit }
			)
			{
				Begin = begin,
				End = end,
			};
		}

		/// <summary>Reverse the order in which the results will be returned</summary>
		/// <returns>A new query object that will return the results in reverse order when executed</returns>
		/// <remarks>Calling Reverse() on an already reversed query will cancel the effect, and the results will be returned in their natural order.
		/// Note: Combining the effects of Take()/Skip() and Reverse() may have an impact on performance, especially if the ReadYourWriteDisabled transaction is options set.</remarks>
		[Pure]
		public FdbRangeQuery<T> Reverse()
		{
			var begin = this.Begin;
			var end = this.End;
			var limit = this.Options.Limit;
			if (limit.HasValue)
			{
				// If Take() of Skip() have been called, we need to update the end bound when reversing (or begin if already reversed)
				if (!this.Reversed)
				{
					end = this.Begin + limit.Value;
				}
				else
				{
					begin = this.End - limit.Value;
				}
			}

			return new FdbRangeQuery<T>(
				this,
				new FdbRangeOptions(this.Options) { Reverse = !this.Reversed }
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
		public FdbRangeQuery<T> WithTargetBytes([Positive] int bytes)
		{
			Contract.Positive(bytes);

			return new FdbRangeQuery<T>(
				this,
				new FdbRangeOptions(this.Options) { TargetBytes = bytes }
			);
		}

		/// <summary>Use a different Streaming Mode</summary>
		/// <param name="mode">Streaming mode to use when reading the results from the database</param>
		/// <returns>A new query object that will use the specified streaming mode when executed</returns>
		[Pure]
		public FdbRangeQuery<T> WithMode(FdbStreamingMode mode)
		{
			if (!Enum.IsDefined(typeof(FdbStreamingMode), mode))
			{
				throw new ArgumentOutOfRangeException(nameof(mode), "Unsupported streaming mode");
			}

			return new FdbRangeQuery<T>(
				this,
				new FdbRangeOptions(this.Options) { Mode = mode }
			);
		}

		/// <summary>Force the query to use a specific transaction</summary>
		/// <param name="transaction">Transaction to use when executing this query</param>
		/// <returns>A new query object that will use the specified transaction when executed</returns>
		[Pure]
		public FdbRangeQuery<T> UseTransaction(IFdbReadOnlyTransaction transaction)
		{
			Contract.NotNull(transaction);

			return new FdbRangeQuery<T>(
				transaction,
				this.Begin,
				this.End,
				this.Transform,
				this.Snapshot,
				new FdbRangeOptions(this.Options)
			);
		}

		#endregion

		#region Pseudo-LINQ

		public IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken ct)
		{
			return GetAsyncEnumerator(ct, AsyncIterationHint.Default);
		}

		public IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken ct, AsyncIterationHint hint)
		{
			return new ResultIterator(this, this.Transaction, this.Transform).GetAsyncEnumerator(ct, hint);
		}

		/// <summary>Return a list of all the elements of the range results</summary>
		public Task<List<T>> ToListAsync()
		{
			// ReSharper disable once InvokeAsExtensionMethod
			return AsyncEnumerable.ToListAsync(this, this.Transaction.Cancellation);
		}

		/// <summary>Return a list of all the elements of the range results</summary>
		public Task<List<T>> ToListAsync(CancellationToken ct)
		{
			// ReSharper disable once InvokeAsExtensionMethod
			return AsyncEnumerable.ToListAsync(this, ct);
		}

		/// <summary>Return an array with all the elements of the range results</summary>
		public Task<T[]> ToArrayAsync()
		{
			// ReSharper disable once InvokeAsExtensionMethod
			return AsyncEnumerable.ToArrayAsync(this, this.Transaction.Cancellation);
		}

		/// <summary>Return an array with all the elements of the range results</summary>
		public Task<T[]> ToArrayAsync(CancellationToken ct)
		{
			// ReSharper disable once InvokeAsExtensionMethod
			return AsyncEnumerable.ToArrayAsync(this, ct);
		}

		/// <summary>Return the number of elements in the range, by reading them</summary>
		/// <remarks>This method has to read all the keys and values, which may exceed the lifetime of a transaction. Please consider using <see cref="Fdb.System.EstimateCountAsync(FoundationDB.Client.IFdbDatabase,FoundationDB.Client.KeyRange,System.Threading.CancellationToken)"/> when reading potentially large ranges.</remarks>
		public Task<int> CountAsync()
		{
			// ReSharper disable once InvokeAsExtensionMethod
			return AsyncEnumerable.CountAsync(this, this.Transaction.Cancellation);
		}

		[Pure]
		internal FdbRangeQuery<TResult> Map<TResult>(Func<KeyValuePair<Slice, Slice>, TResult> transform)
		{
			Contract.Debug.Requires(transform != null);
			return new FdbRangeQuery<TResult>(
				this.Transaction,
				this.Begin,
				this.End,
				transform,
				this.Snapshot,
				new FdbRangeOptions(this.Options)
			);
		}

		/// <summary>Projects each element of the range results into a new form.</summary>
		[Pure]
		public FdbRangeQuery<TResult> Select<TResult>(Func<T, TResult> lambda)
		{
			Contract.Debug.Requires(lambda != null);
			// note: avoid storing the query in the scope by storing the transform locally so that only 'f' and 'lambda' are kept alive
			var f = this.Transform;
			Contract.Debug.Assert(f != null);
			return Map<TResult>((x) => lambda(f(x)));
		}

		/// <summary>Filters the range results based on a predicate.</summary>
		/// <remarks>Caution: filtering occurs on the client side !</remarks>
		[Pure]
		public IAsyncEnumerable<T> Where(Func<T, bool> predicate)
		{
			return AsyncEnumerable.Where(this, predicate);
		}

		public Task<T> FirstOrDefaultAsync()
		{
			// we can optimize this by passing Limit=1
			return HeadAsync(single: false, orDefault: true);
		}

		public Task<T> FirstAsync()
		{
			// we can optimize this by passing Limit=1
			return HeadAsync(single: false, orDefault: false);
		}

		public Task<T> LastOrDefaultAsync()
		{
			//BUGBUG: if there is a Take(N) on the query, Last() will mean "The Nth key" and not the "last key in the original range".

			// we can optimize by reversing the current query and calling FirstOrDefault !
			return this.Reverse().HeadAsync(single:false, orDefault:true);
		}

		public Task<T> LastAsync()
		{
			//BUGBUG: if there is a Take(N) on the query, Last() will mean "The Nth key" and not the "last key in the original range".

			// we can optimize this by reversing the current query and calling First !
			return this.Reverse().HeadAsync(single: false, orDefault:false);
		}

		public Task<T> SingleOrDefaultAsync()
		{
			// we can optimize this by passing Limit=2
			return HeadAsync(single: true, orDefault: true);
		}

		public Task<T> SingleAsync()
		{
			// we can optimize this by passing Limit=2
			return HeadAsync(single: true, orDefault: false);
		}

		/// <summary>Return true if the range query returns at least one element, or false if there was no result.</summary>
		public Task<bool> AnyAsync()
		{
			// we can optimize this by using Limit = 1
			return AnyOrNoneAsync(any: true);
		}

		/// <summary>Return true if the range query does not return any valid elements, or false if there was at least one result.</summary>
		/// <remarks>This is a convenience method that is there to help porting layer code from other languages. This is strictly equivalent to calling "!(await query.AnyAsync())".</remarks>
		public Task<bool> NoneAsync()
		{
			// we can optimize this by using Limit = 1
			return AnyOrNoneAsync(any: false);
		}

		/// <summary>Execute an action on each key/value pair of the range results</summary>
		public Task ForEachAsync(Action<T> action)
		{
			// ReSharper disable once InvokeAsExtensionMethod
			return AsyncEnumerable.ForEachAsync(this, action, this.Transaction.Cancellation);
		}

		internal async Task<T> HeadAsync(bool single, bool orDefault)
		{
			// Optimized code path for First/Last/Single variants where we can be smart and only ask for 1 or 2 results from the db

			// we can use the EXACT streaming mode with Limit = 1|2, and it will work if TargetBytes is 0
			if ((this.TargetBytes ?? 0) != 0 || (this.Mode != FdbStreamingMode.Iterator && this.Mode != FdbStreamingMode.Exact))
			{ // fallback to the default implementation
				return await AsyncEnumerable.Head(this, single, orDefault, this.Transaction.Cancellation).ConfigureAwait(false);
			}

			//BUGBUG: do we need special handling if OriginalRange != Range ? (weird combinations of Take/Skip and Reverse)

			var tr = this.Snapshot ? this.Transaction.Snapshot : this.Transaction;
			var results = await tr.GetRangeAsync(this.Begin, this.End, limit: Math.Min(single ? 2 : 1, this.Options.Limit ?? int.MaxValue), reverse: this.Reversed, mode: FdbStreamingMode.Exact, iteration: 0).ConfigureAwait(false);

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
			return this.Transform(results[0]);
		}

		internal async Task<bool> AnyOrNoneAsync(bool any)
		{
			// Optimized code path for Any/None where we can be smart and only ask for 1 from the db

			// we can use the EXACT streaming mode with Limit = 1, and it will work if TargetBytes is 0
			if ((this.TargetBytes ?? 0) != 0 || (this.Mode != FdbStreamingMode.Iterator && this.Mode != FdbStreamingMode.Exact))
			{ // fallback to the default implementation
				// ReSharper disable InvokeAsExtensionMethod
				if (any)
					return await AsyncEnumerable.AnyAsync(this, this.Transaction.Cancellation);
				else
					return await AsyncEnumerable.NoneAsync(this, this.Transaction.Cancellation);
				// ReSharper restore InvokeAsExtensionMethod
			}

			//BUGBUG: do we need special handling if OriginalRange != Range ? (weird combinations of Take/Skip and Reverse)

			var tr = this.Snapshot ? this.Transaction.Snapshot : this.Transaction;
			var results = await tr.GetRangeAsync(this.Begin, this.End, limit: 1, reverse: this.Reversed, mode: FdbStreamingMode.Exact, iteration: 0).ConfigureAwait(false);

			return any ? !results.IsEmpty : results.IsEmpty;
		}

		#endregion

		/// <summary>Returns a printable version of the range query</summary>
		public override string ToString()
		{
			return $"Range({this.Range}, {this.Limit}, {(this.Reversed ? "reverse" : "forward")})";
		}

	}

	/// <summary>Extension methods for <see cref="FdbRangeQuery{T}"/></summary>
	public static class FdbRangeQueryExtensions
	{

		[Pure]
		public static FdbRangeQuery<TKey> Keys<TKey, TValue>(this FdbRangeQuery<KeyValuePair<TKey, TValue>> query)
		{
			Contract.NotNull(query);

			var f = query.Transform;
			//note: we only keep a reference on 'f' to allow the previous query instance to be collected.
			Contract.Debug.Assert(f != null);

			return query.Map<TKey>((x) => f(x).Key);
		}

		[Pure]
		public static FdbRangeQuery<TResult> Keys<TKey, TValue, TResult>(this FdbRangeQuery<KeyValuePair<TKey, TValue>> query, Func<TKey, TResult> transform)
		{
			Contract.NotNull(query);
			Contract.NotNull(transform);

			var f = query.Transform;
			//note: we only keep a reference on 'f' to allow the previous query instance to be collected.
			Contract.Debug.Assert(f != null);

			return query.Map<TResult>((x) => transform(f(x).Key));
		}

		[Pure]
		public static FdbRangeQuery<TValue> Values<TKey, TValue>(this FdbRangeQuery<KeyValuePair<TKey, TValue>> query)
		{
			Contract.NotNull(query);

			var f = query.Transform;
			//note: we only keep a reference on 'f' to allow the previous query instance to be collected.
			Contract.Debug.Assert(f != null);

			return query.Map<TValue>((x) => f(x).Value);
		}

		[Pure]
		public static FdbRangeQuery<TResult> Values<TKey, TValue, TResult>(this FdbRangeQuery<KeyValuePair<TKey, TValue>> query, Func<TValue, TResult> transform)
		{
			Contract.NotNull(query);
			Contract.NotNull(transform);

			var f = query.Transform;
			//note: we only keep a reference on 'f' to allow the previous query instance to be collected.
			Contract.Debug.Assert(f != null);

			return query.Map<TResult>((x) => transform(f(x).Value));
		}

	}

}
