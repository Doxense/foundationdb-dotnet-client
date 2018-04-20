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

namespace FoundationDB.Linq
{
	using FoundationDB.Async;
	using FoundationDB.Client.Utils;
	using JetBrains.Annotations;
	using System;
	using System.Threading;
	using System.Threading.Tasks;

	/// <summary>Filters an async sequence of items</summary>
	/// <typeparam name="TSource">Type of elements of the async sequence</typeparam>
	internal sealed class FdbWhereAsyncIterator<TSource> : FdbAsyncFilterIterator<TSource, TSource>
	{
		private readonly AsyncFilterExpression<TSource> m_filter;

		public FdbWhereAsyncIterator([NotNull] IFdbAsyncEnumerable<TSource> source, AsyncFilterExpression<TSource> filter)
			: base(source)
		{
			Contract.Requires(filter != null, "there can be only one kind of filter specified");

			m_filter = filter;
		}

		protected override FdbAsyncIterator<TSource> Clone()
		{
			return new FdbWhereAsyncIterator<TSource>(m_source, m_filter);
		}

		protected override async Task<bool> OnNextAsync(CancellationToken cancellationToken)
		{
			while (!cancellationToken.IsCancellationRequested)
			{
				if (!await m_iterator.MoveNextAsync(cancellationToken).ConfigureAwait(false))
				{ // completed
					return Completed();
				}

				if (cancellationToken.IsCancellationRequested) break;

				TSource current = m_iterator.Current;
				if (!m_filter.Async)
				{
					if (!m_filter.Invoke(current))
					{
						continue;
					}
				}
				else
				{
					if (!await m_filter.InvokeAsync(current, cancellationToken).ConfigureAwait(false))
					{
						continue;
					}
				}

				return Publish(current);
			}

			return Canceled(cancellationToken);
		}

		public override FdbAsyncIterator<TSource> Where(Func<TSource, bool> predicate)
		{
			return FdbAsyncEnumerable.Filter<TSource>(
				m_source,
				m_filter.AndAlso(new AsyncFilterExpression<TSource>(predicate))
			);
		}

		public override FdbAsyncIterator<TSource> Where(Func<TSource, CancellationToken, Task<bool>> asyncPredicate)
		{
			return FdbAsyncEnumerable.Filter<TSource>(
				m_source,
				m_filter.AndAlso(new AsyncFilterExpression<TSource>(asyncPredicate))
			);
		}

		public override FdbAsyncIterator<TNew> Select<TNew>(Func<TSource, TNew> selector)
		{
			return new FdbWhereSelectAsyncIterator<TSource, TNew>(
				m_source,
				m_filter,
				new AsyncTransformExpression<TSource, TNew>(selector),
				limit: null,
				offset: null
			);
		}

		public override FdbAsyncIterator<TNew> Select<TNew>(Func<TSource, CancellationToken, Task<TNew>> asyncSelector)
		{
			return new FdbWhereSelectAsyncIterator<TSource, TNew>(
				m_source,
				m_filter,
				new AsyncTransformExpression<TSource, TNew>(asyncSelector),
				limit: null,
				offset: null
			);
		}

		public override FdbAsyncIterator<TSource> Take(int limit)
		{
			if (limit < 0) throw new ArgumentOutOfRangeException("limit", "Limit cannot be less than zero");

			return new FdbWhereSelectAsyncIterator<TSource, TSource>(
				m_source,
				m_filter,
				new AsyncTransformExpression<TSource, TSource>(TaskHelpers.Cache<TSource>.Identity),
				limit: limit,
				offset: null
			);
		}

		public override async Task ExecuteAsync(Action<TSource> handler, CancellationToken ct)
		{
			if (handler == null) throw new ArgumentNullException("handler");

			if (ct.IsCancellationRequested) ct.ThrowIfCancellationRequested();

			using (var iter = StartInner())
			{
				if (!m_filter.Async)
				{
					while (!ct.IsCancellationRequested && (await iter.MoveNextAsync(ct).ConfigureAwait(false)))
					{
						var current = iter.Current;
						if (m_filter.Invoke(current))
						{
							handler(current);
						}
					}
				}
				else
				{
					while (!ct.IsCancellationRequested && (await iter.MoveNextAsync(ct).ConfigureAwait(false)))
					{
						var current = iter.Current;
						if (await m_filter.InvokeAsync(current, ct).ConfigureAwait(false))
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
			if (asyncHandler == null) throw new ArgumentNullException("asyncHandler");

			if (ct.IsCancellationRequested) ct.ThrowIfCancellationRequested();

			using (var iter = StartInner())
			{
				if (!m_filter.Async)
				{
					while (!ct.IsCancellationRequested && (await iter.MoveNextAsync(ct).ConfigureAwait(false)))
					{
						var current = iter.Current;
						if (m_filter.Invoke(current))
						{
							await asyncHandler(current, ct).ConfigureAwait(false);
						}
					}
				}
				else
				{
					while (!ct.IsCancellationRequested && (await iter.MoveNextAsync(ct).ConfigureAwait(false)))
					{
						var current = iter.Current;
						if (await m_filter.InvokeAsync(current, ct).ConfigureAwait(false))
						{
							await asyncHandler(current, ct).ConfigureAwait(false);
						}
					}
				}
			}

			if (ct.IsCancellationRequested) ct.ThrowIfCancellationRequested();
		}

	}

}
