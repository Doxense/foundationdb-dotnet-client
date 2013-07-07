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

namespace FoundationDB.Linq
{
	using FoundationDB.Client.Utils;
	using System;
	using System.Collections.Generic;
	using System.Threading;
	using System.Threading.Tasks;

	public static partial class FdbAsyncEnumerable
	{
		// Welcome to the wonderful world of the Monads! 

		/// <summary>Iterates over an async sequence of items</summary>
		/// <typeparam name="TSource">Type of elements of the inner async sequence</typeparam>
		/// <typeparam name="TResult">Type of elements of the outer async sequence</typeparam>
		internal sealed class WhereSelectAsyncIterator<TSource, TResult> : AsyncFilter<TSource, TResult>
		{
			private Func<TSource, bool> m_filter;
			private Func<TSource, CancellationToken, Task<bool>> m_asyncFilter;

			private Func<TSource, TResult> m_transform;
			private Func<TSource, CancellationToken, Task<TResult>> m_asyncTransform;

			private int? m_limit;

			private int? m_remaining;

			public WhereSelectAsyncIterator(
				IFdbAsyncEnumerable<TSource> source,
				Func<TSource, bool> filter,
				Func<TSource, CancellationToken, Task<bool>> asyncFilter,
				Func<TSource, TResult> transform,
				Func<TSource, CancellationToken, Task<TResult>> asyncTransform,
				int? limit
			)
				: base(source)
			{
				Contract.Requires(transform != null ^ asyncTransform != null); // at least one but not both
				Contract.Requires(filter == null || asyncFilter == null); // can have none, but not both
				Contract.Requires(limit == null || limit >= 0);

				m_filter = filter;
				m_asyncFilter = asyncFilter;
				m_transform = transform;
				m_asyncTransform = asyncTransform;
				m_limit = limit;
			}


			protected override AsyncIterator<TResult> Clone()
			{
				return new WhereSelectAsyncIterator<TSource, TResult>(m_source, m_filter, m_asyncFilter, m_transform, m_asyncTransform, m_limit);
			}

			protected override Task<bool> OnFirstAsync(CancellationToken ct)
			{
				m_remaining = m_limit;
				return base.OnFirstAsync(ct);
			}

			protected override async Task<bool> OnNextAsync(CancellationToken cancellationToken)
			{
				if (m_remaining != null && m_remaining.Value <= 0)
				{ // reached limit!
					return Completed();
				}

				while (!cancellationToken.IsCancellationRequested)
				{
					if (!await m_iterator.MoveNext(cancellationToken).ConfigureAwait(false))
					{ // completed
						return Completed();
					}

					if (cancellationToken.IsCancellationRequested) break;

					TSource current = m_iterator.Current;
					if (m_filter != null)
					{
						if (!m_filter(current)) continue;
					}
					else if (m_asyncFilter != null)
					{
						if (!await m_asyncFilter(current, cancellationToken).ConfigureAwait(false)) continue;
					}

					TResult result;
					if (m_transform != null)
					{
						result = m_transform(current);
					}
					else
					{
						result = await m_asyncTransform(current, cancellationToken).ConfigureAwait(false);
					}

					if (m_remaining != null)
					{ // decrement remaining quota
						m_remaining = m_remaining.Value - 1;
					}

					return Publish(result);
				}

				return Cancelled(cancellationToken);
			}

			public override AsyncIterator<TNew> Select<TNew>(Func<TResult, TNew> selector)
			{
				if (selector == null) throw new ArgumentNullException("selector");

				if (m_transform != null)
				{
					return new WhereSelectAsyncIterator<TSource, TNew>(
						m_source,
						m_filter,
						m_asyncFilter,
						(x) => selector(m_transform(x)),
						null,
						m_limit
					);
				}
				else
				{
					return new WhereSelectAsyncIterator<TSource, TNew>(
						m_source,
						m_filter,
						m_asyncFilter,
						null,
						async (x, ct) => selector(await m_asyncTransform(x, ct).ConfigureAwait(false)),
						m_limit
					);
				}
			}

			public override AsyncIterator<TNew> Select<TNew>(Func<TResult, CancellationToken, Task<TNew>> asyncSelector)
			{
				if (asyncSelector == null) throw new ArgumentNullException("asyncSelector");

				if (m_transform != null)
				{
					return new WhereSelectAsyncIterator<TSource, TNew>(
						m_source,
						m_filter,
						m_asyncFilter,
						null,
						(x, ct) => asyncSelector(m_transform(x), ct),
						m_limit
					);
				}
				else
				{
					return new WhereSelectAsyncIterator<TSource, TNew>(
						m_source,
						m_filter,
						m_asyncFilter,
						null,
						async (x, ct) => await asyncSelector(await m_asyncTransform(x, ct).ConfigureAwait(false), ct).ConfigureAwait(false),
						m_limit
					);
				}
			}

			public override AsyncIterator<TNew> SelectMany<TNew>(Func<TResult, IEnumerable<TNew>> selector)
			{
				if (selector == null) throw new ArgumentNullException("selector");

				if (m_filter == null && m_asyncFilter == null && m_limit == null)
				{
					if (m_transform != null)
					{
						return new SelectManyAsyncIterator<TSource, TNew>(
							m_source,
							selector: (x) => selector(m_transform(x)),
							asyncSelector: null
						);
					}
					else
					{
						return new SelectManyAsyncIterator<TSource, TNew>(
							m_source,
							selector: null,
							asyncSelector: async (x, ct) => selector(await m_asyncTransform(x, ct).ConfigureAwait(false))
						);
					}
				}

				// other cases are too complex :(
				return base.SelectMany<TNew>(selector);
			}
			public override AsyncIterator<TNew> SelectMany<TNew>(Func<TResult, CancellationToken, Task<IEnumerable<TNew>>> asyncSelector)
			{
				if (asyncSelector == null) throw new ArgumentNullException("asyncSelector");

				if (m_filter == null && m_asyncFilter == null && m_limit == null)
				{
					if (m_transform != null)
					{
						return new SelectManyAsyncIterator<TSource, TNew>(
							m_source,
							selector: null,
							asyncSelector: (x, ct) => asyncSelector(m_transform(x), ct)
						);
					}
					else
					{
						return new SelectManyAsyncIterator<TSource, TNew>(
							m_source,
							selector: null,
							asyncSelector: async (x, ct) => await asyncSelector(await m_asyncTransform(x, ct).ConfigureAwait(false), ct).ConfigureAwait(false)
						);
					}
				}

				// other cases are too complex :(
				return base.SelectMany<TNew>(asyncSelector);
			}

			public override AsyncIterator<TResult> Take(int limit)
			{
				if (limit < 0) throw new ArgumentOutOfRangeException("limit", "Limit cannot be less than zero");

				if (m_limit != null && m_limit.Value <= limit)
				{
					// we are already taking less then that
					return this;
				}

				return new WhereSelectAsyncIterator<TSource, TResult>(
					m_source,
					m_filter,
					m_asyncFilter,
					m_transform,
					m_asyncTransform,
					m_limit
				);
			}

		}

	}
}
