#region BSD License
/* Copyright (c) 2013-2020, Doxense SAS
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

#if !USE_SHARED_FRAMEWORK

namespace Doxense.Linq.Async.Iterators
{
	using System;
	using System.Collections.Generic;
	using System.Threading;
	using System.Threading.Tasks;
	using Doxense.Diagnostics.Contracts;
	using Doxense.Linq.Async.Expressions;
	using Doxense.Threading.Tasks;

	/// <summary>Filters an async sequence of items</summary>
	/// <typeparam name="TSource">Type of elements of the async sequence</typeparam>
	public sealed class WhereAsyncIterator<TSource> : AsyncFilterIterator<TSource, TSource>
	{
		private readonly AsyncFilterExpression<TSource> m_filter;

		public WhereAsyncIterator(IAsyncEnumerable<TSource> source, AsyncFilterExpression<TSource> filter)
			: base(source)
		{
			Contract.Requires(filter != null, "there can be only one kind of filter specified");

			m_filter = filter;
		}

		protected override AsyncIterator<TSource> Clone()
		{
			return new WhereAsyncIterator<TSource>(m_source, m_filter);
		}

		protected override async ValueTask<bool> OnNextAsync()
		{
			var iterator = m_iterator;
			var filter = m_filter;
			var ct = m_ct;
			Contract.Requires(iterator != null);

			while (!ct.IsCancellationRequested)
			{
				if (!await iterator.MoveNextAsync().ConfigureAwait(false))
				{ // completed
					return await Completed();
				}

				if (ct.IsCancellationRequested) break;

				TSource current = iterator.Current;
				if (!filter.Async)
				{
					if (!filter.Invoke(current))
					{
						continue;
					}
				}
				else
				{
					if (!await filter.InvokeAsync(current, ct).ConfigureAwait(false))
					{
						continue;
					}
				}

				return Publish(current);
			}

			return await Canceled();
		}

		public override AsyncIterator<TSource> Where(Func<TSource, bool> predicate)
		{
			return AsyncEnumerable.Filter<TSource>(
				m_source,
				m_filter.AndAlso(new AsyncFilterExpression<TSource>(predicate))
			);
		}

		public override AsyncIterator<TSource> Where(Func<TSource, CancellationToken, Task<bool>> asyncPredicate)
		{
			return AsyncEnumerable.Filter<TSource>(
				m_source,
				m_filter.AndAlso(new AsyncFilterExpression<TSource>(asyncPredicate))
			);
		}

		public override AsyncIterator<TNew> Select<TNew>(Func<TSource, TNew> selector)
		{
			return new WhereSelectAsyncIterator<TSource, TNew>(
				m_source,
				m_filter,
				new AsyncTransformExpression<TSource, TNew>(selector),
				limit: null,
				offset: null
			);
		}

		public override AsyncIterator<TNew> Select<TNew>(Func<TSource, CancellationToken, Task<TNew>> asyncSelector)
		{
			return new WhereSelectAsyncIterator<TSource, TNew>(
				m_source,
				m_filter,
				new AsyncTransformExpression<TSource, TNew>(asyncSelector),
				limit: null,
				offset: null
			);
		}

		public override AsyncIterator<TSource> Take(int limit)
		{
			if (limit < 0) throw new ArgumentOutOfRangeException(nameof(limit), "Limit cannot be less than zero");

			return new WhereSelectAsyncIterator<TSource, TSource>(
				m_source,
				m_filter,
				new AsyncTransformExpression<TSource, TSource>(TaskHelpers.CachedTasks<TSource>.Identity),
				limit: limit,
				offset: null
			);
		}

		public override async Task ExecuteAsync(Action<TSource> handler, CancellationToken ct)
		{
			Contract.NotNull(handler, nameof(handler));

			ct.ThrowIfCancellationRequested();

			var filter = m_filter;
			await using (var iter = StartInner(ct))
			{
				if (!filter.Async)
				{
					while (!ct.IsCancellationRequested && (await iter.MoveNextAsync().ConfigureAwait(false)))
					{
						var current = iter.Current;
						if (filter.Invoke(current))
						{
							handler(current);
						}
					}
				}
				else
				{
					while (!ct.IsCancellationRequested && (await iter.MoveNextAsync().ConfigureAwait(false)))
					{
						var current = iter.Current;
						if (await filter.InvokeAsync(current, ct).ConfigureAwait(false))
						{
							handler(current);
						}
					}
				}
			}

			ct.ThrowIfCancellationRequested();
		}

		public override async Task ExecuteAsync(Func<TSource, CancellationToken, Task> asyncHandler, CancellationToken ct)
		{
			Contract.NotNull(asyncHandler, nameof(asyncHandler));

			ct.ThrowIfCancellationRequested();

			var filter = m_filter;
			await using (var iter = StartInner(ct))
			{
				if (!filter.Async)
				{
					while (!ct.IsCancellationRequested && (await iter.MoveNextAsync().ConfigureAwait(false)))
					{
						var current = iter.Current;
						if (filter.Invoke(current))
						{
							await asyncHandler(current, ct).ConfigureAwait(false);
						}
					}
				}
				else
				{
					while (!ct.IsCancellationRequested && (await iter.MoveNextAsync().ConfigureAwait(false)))
					{
						var current = iter.Current;
						if (await filter.InvokeAsync(current, ct).ConfigureAwait(false))
						{
							await asyncHandler(current, ct).ConfigureAwait(false);
						}
					}
				}
			}

			ct.ThrowIfCancellationRequested();
		}

	}

}

#endif
