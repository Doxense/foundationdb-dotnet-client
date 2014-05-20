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
	using FoundationDB.Client.Utils;
	using JetBrains.Annotations;
	using System;
	using System.Collections.Generic;
	using System.Threading;
	using System.Threading.Tasks;

	/// <summary>Iterates over an async sequence of items</summary>
	/// <typeparam name="TSource">Type of elements of the inner async sequence</typeparam>
	/// <typeparam name="TResult">Type of elements of the outer async sequence</typeparam>
	internal sealed class FdbWhereSelectAsyncIterator<TSource, TResult> : FdbAsyncFilter<TSource, TResult>
	{
		private readonly Func<TSource, bool> m_filter;
		private readonly Func<TSource, CancellationToken, Task<bool>> m_asyncFilter;

		private readonly Func<TSource, TResult> m_transform;
		private readonly Func<TSource, CancellationToken, Task<TResult>> m_asyncTransform;

		//note: both limit and offset are applied AFTER filtering!
		private readonly int? m_limit;
		private readonly int? m_offset;

		private int? m_remaining;
		private int? m_skipped;

		public FdbWhereSelectAsyncIterator(
			[NotNull] IFdbAsyncEnumerable<TSource> source,
			Func<TSource, bool> filter,
			Func<TSource, CancellationToken, Task<bool>> asyncFilter,
			Func<TSource, TResult> transform,
			Func<TSource, CancellationToken, Task<TResult>> asyncTransform,
			int? limit,
			int? offset
		)
			: base(source)
		{
			Contract.Requires(transform != null ^ asyncTransform != null); // at least one but not both
			Contract.Requires(filter == null || asyncFilter == null); // can have none, but not both
			Contract.Requires(limit == null || limit.Value >= 0);
			Contract.Requires(offset == null || offset.Value >= 0);

			m_filter = filter;
			m_asyncFilter = asyncFilter;
			m_transform = transform;
			m_asyncTransform = asyncTransform;
			m_limit = limit;
			m_offset = offset;
		}

		protected override FdbAsyncIterator<TResult> Clone()
		{
			return new FdbWhereSelectAsyncIterator<TSource, TResult>(m_source, m_filter, m_asyncFilter, m_transform, m_asyncTransform, m_limit, m_offset);
		}

		protected override Task<bool> OnFirstAsync(CancellationToken ct)
		{
			m_remaining = m_limit;
			m_skipped = m_offset;
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

				#region Filtering...

				TSource current = m_iterator.Current;
				if (m_filter != null)
				{
					if (!m_filter(current)) continue;
				}
				else if (m_asyncFilter != null)
				{
					if (!await m_asyncFilter(current, cancellationToken).ConfigureAwait(false)) continue;
				}

				#endregion

				#region Skipping...

				if (m_skipped != null)
				{
					if (m_skipped.Value > 0)
					{ // skip this result
						m_skipped = m_skipped.Value - 1;
						continue;
					}
					// we can now start outputing results...
					m_skipped = null;
				}

				#endregion

				#region Transforming...

				TResult result;
				if (m_transform != null)
				{
					result = m_transform(current);
				}
				else
				{
					result = await m_asyncTransform(current, cancellationToken).ConfigureAwait(false);
				}

				#endregion

				#region Publishing...

				if (m_remaining != null)
				{ // decrement remaining quota
					m_remaining = m_remaining.Value - 1;
				}

				return Publish(result);
	
				#endregion
			}

			return Canceled(cancellationToken);
		}

		public override FdbAsyncIterator<TNew> Select<TNew>(Func<TResult, TNew> selector)
		{
			if (selector == null) throw new ArgumentNullException("selector");

			if (m_transform != null)
			{
				return new FdbWhereSelectAsyncIterator<TSource, TNew>(
					m_source,
					m_filter,
					m_asyncFilter,
					(x) => selector(m_transform(x)),
					null,
					m_limit,
					m_offset
				);
			}
			else
			{
				return new FdbWhereSelectAsyncIterator<TSource, TNew>(
					m_source,
					m_filter,
					m_asyncFilter,
					null,
					async (x, ct) => selector(await m_asyncTransform(x, ct).ConfigureAwait(false)),
					m_limit,
					m_offset
				);
			}
		}

		public override FdbAsyncIterator<TNew> Select<TNew>(Func<TResult, CancellationToken, Task<TNew>> asyncSelector)
		{
			if (asyncSelector == null) throw new ArgumentNullException("asyncSelector");

			if (m_transform != null)
			{
				return new FdbWhereSelectAsyncIterator<TSource, TNew>(
					m_source,
					m_filter,
					m_asyncFilter,
					null,
					(x, ct) => asyncSelector(m_transform(x), ct),
					m_limit,
					m_offset
				);
			}
			else
			{
				return new FdbWhereSelectAsyncIterator<TSource, TNew>(
					m_source,
					m_filter,
					m_asyncFilter,
					null,
					async (x, ct) => await asyncSelector(await m_asyncTransform(x, ct).ConfigureAwait(false), ct).ConfigureAwait(false),
					m_limit,
					m_offset
				);
			}
		}

		public override FdbAsyncIterator<TNew> SelectMany<TNew>(Func<TResult, IEnumerable<TNew>> selector)
		{
			if (selector == null) throw new ArgumentNullException("selector");

			if (m_filter == null && m_asyncFilter == null && m_limit == null && m_offset == null)
			{
				if (m_transform != null)
				{
					return new FdbSelectManyAsyncIterator<TSource, TNew>(
						m_source,
						selector: (x) => selector(m_transform(x)),
						asyncSelector: null
					);
				}
				else
				{
					return new FdbSelectManyAsyncIterator<TSource, TNew>(
						m_source,
						selector: null,
						asyncSelector: async (x, ct) => selector(await m_asyncTransform(x, ct).ConfigureAwait(false))
					);
				}
			}

			// other cases are too complex :(
			return base.SelectMany<TNew>(selector);
		}

		public override FdbAsyncIterator<TNew> SelectMany<TNew>(Func<TResult, CancellationToken, Task<IEnumerable<TNew>>> asyncSelector)
		{
			if (asyncSelector == null) throw new ArgumentNullException("asyncSelector");

			if (m_filter == null && m_asyncFilter == null && m_limit == null && m_offset == null)
			{
				if (m_transform != null)
				{
					return new FdbSelectManyAsyncIterator<TSource, TNew>(
						m_source,
						selector: null,
						asyncSelector: (x, ct) => asyncSelector(m_transform(x), ct)
					);
				}
				else
				{
					return new FdbSelectManyAsyncIterator<TSource, TNew>(
						m_source,
						selector: null,
						asyncSelector: async (x, ct) => await asyncSelector(await m_asyncTransform(x, ct).ConfigureAwait(false), ct).ConfigureAwait(false)
					);
				}
			}

			// other cases are too complex :(
			return base.SelectMany<TNew>(asyncSelector);
		}

		public override FdbAsyncIterator<TResult> Take(int limit)
		{
			if (limit < 0) throw new ArgumentOutOfRangeException("limit", "Limit cannot be less than zero");

			if (m_limit != null && m_limit.Value <= limit)
			{
				// we are already taking less then that
				return this;
			}

			return new FdbWhereSelectAsyncIterator<TSource, TResult>(
				m_source,
				m_filter,
				m_asyncFilter,
				m_transform,
				m_asyncTransform,
				limit,
				m_offset
			);
		}

		public override FdbAsyncIterator<TResult> Skip(int offset)
		{
			if (offset < 0) throw new ArgumentOutOfRangeException("offset", "Offset cannot be less than zero");

			if (offset == 0) return this;

			if (m_offset != null) offset += m_offset.Value;

			return new FdbWhereSelectAsyncIterator<TSource, TResult>(
				m_source,
				m_filter,
				m_asyncFilter,
				m_transform,
				m_asyncTransform,
				m_limit,
				offset
			);
		}

		public override FdbAsyncIterator<TResult> Where(Func<TResult, bool> predicate)
		{
			if (predicate == null) throw new ArgumentNullException("predicate");

			// note: the only possible optimization here is if TSource == TResult, then we can combine both predicates
			// remember: limit/offset are applied AFTER the filtering, so can only combine if they are null
			if (m_asyncFilter == null && m_limit == null && (m_offset == null || m_offset.Value == 0) && typeof(TSource) == typeof(TResult))
			{
				var filter = (Func<TSource, bool>)(Delegate)predicate;
				if (m_filter != null) filter = (x) => m_filter(x) && filter(x);

				return new FdbWhereSelectAsyncIterator<TSource, TResult>(
					m_source,
					filter,
					null,
					m_transform,
					m_asyncTransform,
					m_limit,
					m_offset
				);
			}

			// no easy optimization...
			return base.Where(predicate);
		}

		public override FdbAsyncIterator<TResult> Where(Func<TResult, CancellationToken, Task<bool>> asyncPredicate)
		{
			if (asyncPredicate == null) throw new ArgumentNullException("asyncPredicate");

			// note: the only possible optimization here is if TSource == TResult, then we can combine both predicates
			if (m_filter == null && m_limit == null && (m_offset == null || m_offset.Value == 0) && typeof(TSource) == typeof(TResult))
			{
				var asyncFilter = (Func<TSource, CancellationToken, Task<bool>>)(Delegate)asyncPredicate;
				if (m_asyncFilter != null) asyncFilter = async (x, ct) => await m_asyncFilter(x, ct) && await asyncFilter(x, ct);

				return new FdbWhereSelectAsyncIterator<TSource, TResult>(
					m_source,
					null,
					asyncFilter,
					m_transform,
					m_asyncTransform,
					m_limit,
					m_offset
				);
			}

			// no easy optimization...
			return base.Where(asyncPredicate);
		}

	}

}
