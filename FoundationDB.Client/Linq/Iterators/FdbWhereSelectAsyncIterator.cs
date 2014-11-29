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
	using System.Collections.Generic;
	using System.Threading;
	using System.Threading.Tasks;

	/// <summary>Iterates over an async sequence of items</summary>
	/// <typeparam name="TSource">Type of elements of the inner async sequence</typeparam>
	/// <typeparam name="TResult">Type of elements of the outer async sequence</typeparam>
	internal sealed class FdbWhereSelectAsyncIterator<TSource, TResult> : FdbAsyncFilterIterator<TSource, TResult>
	{
		private readonly AsyncFilterExpression<TSource> m_filter;
		private readonly AsyncTransformExpression<TSource, TResult> m_transform;

		//note: both limit and offset are applied AFTER filtering!
		private readonly int? m_limit;
		private readonly int? m_offset;

		private int? m_remaining;
		private int? m_skipped;

		public FdbWhereSelectAsyncIterator(
			[NotNull] IFdbAsyncEnumerable<TSource> source,
			AsyncFilterExpression<TSource> filter,
			AsyncTransformExpression<TSource, TResult> transform,
			int? limit,
			int? offset
		)
			: base(source)
		{
			Contract.Requires(transform != null); // must do at least something
			Contract.Requires((limit ?? 0) >= 0 && (offset ?? 0) >= 0); // bounds cannot be negative

			m_filter = filter;
			m_transform = transform;
			m_limit = limit;
			m_offset = offset;
		}

		protected override FdbAsyncIterator<TResult> Clone()
		{
			return new FdbWhereSelectAsyncIterator<TSource, TResult>(m_source, m_filter, m_transform, m_limit, m_offset);
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
					if (!m_filter.Async)
					{
						if (!m_filter.Invoke(current)) continue;
					}
					else
					{
						if (!await m_filter.InvokeAsync(current, cancellationToken).ConfigureAwait(false)) continue;
					}
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
				if (!m_transform.Async)
				{
					result = m_transform.Invoke(current);
				}
				else
				{
					result = await m_transform.InvokeAsync(current, cancellationToken).ConfigureAwait(false);
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

			return new FdbWhereSelectAsyncIterator<TSource, TNew>(
				m_source,
				m_filter,
				m_transform.Then(new AsyncTransformExpression<TResult, TNew>(selector)),
				m_limit,
				m_offset
			);
		}

		public override FdbAsyncIterator<TNew> Select<TNew>(Func<TResult, CancellationToken, Task<TNew>> asyncSelector)
		{
			if (asyncSelector == null) throw new ArgumentNullException("asyncSelector");

			return new FdbWhereSelectAsyncIterator<TSource, TNew>(
				m_source,
				m_filter,
				m_transform.Then(new AsyncTransformExpression<TResult, TNew>(asyncSelector)),
				m_limit,
				m_offset
			);
		}

		public override FdbAsyncIterator<TNew> SelectMany<TNew>(Func<TResult, IEnumerable<TNew>> selector)
		{
			if (selector == null) throw new ArgumentNullException("selector");

			if (m_filter == null && m_limit == null && m_offset == null)
			{
				return new FdbSelectManyAsyncIterator<TSource, TNew>(
					m_source,
					m_transform.Then(new AsyncTransformExpression<TResult, IEnumerable<TNew>>(selector))
				);
			}

			// other cases are too complex :(
			return base.SelectMany<TNew>(selector);
		}

		public override FdbAsyncIterator<TNew> SelectMany<TNew>(Func<TResult, CancellationToken, Task<IEnumerable<TNew>>> asyncSelector)
		{
			if (asyncSelector == null) throw new ArgumentNullException("asyncSelector");

			if (m_filter == null && m_limit == null && m_offset == null)
			{
				return new FdbSelectManyAsyncIterator<TSource, TNew>(
					m_source,
					m_transform.Then(new AsyncTransformExpression<TResult, IEnumerable<TNew>>(asyncSelector))
				);
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
				m_transform,
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
				m_transform,
				m_limit,
				offset
			);
		}

		public override FdbAsyncIterator<TResult> Where(Func<TResult, bool> predicate)
		{
			if (predicate == null) throw new ArgumentNullException("predicate");

			// note: the only possible optimization here is if TSource == TResult, then we can combine both predicates
			// remember: limit/offset are applied AFTER the filtering, so can only combine if they are null
			// also, since transform is done after filtering, we can only optimize if transform is null (not allowed!) or the identity function
			if (m_limit == null
				&& (m_offset == null || m_offset.Value == 0)
				&& typeof(TSource) == typeof(TResult) //BUGBUG: type comparison maybe should check derived classes also ?
				&& m_transform.IsIdentity())
			{
				var filter = new AsyncFilterExpression<TSource>((Func<TSource, bool>)(Delegate)predicate);
				if (m_filter != null) filter = m_filter.AndAlso(filter);

				//BUGBUG: if the query already has a select, it should be evaluated BEFORE the new filter,
				// but currently FdbWhereSelectAsyncIterator<> filters before transformations !

				return new FdbWhereSelectAsyncIterator<TSource, TResult>(
					m_source,
					filter,
					m_transform,
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
			// remember: limit/offset are applied AFTER the filtering, so can only combine if they are null
			if (m_limit == null && (m_offset == null || m_offset.Value == 0) && typeof(TSource) == typeof(TResult)) //BUGBUG: type comparison maybe should check derived classes also ?
			{
				var asyncFilter = new AsyncFilterExpression<TSource>((Func<TSource, CancellationToken, Task<bool>>)(Delegate)asyncPredicate);
				if (m_filter != null) asyncFilter = m_filter.AndAlso(asyncFilter);

				//BUGBUG: if the query already has a select, it should be evaluated BEFORE the new filter,
				// but currently FdbWhereSelectAsyncIterator<> filters before transformations !

				return new FdbWhereSelectAsyncIterator<TSource, TResult>(
					m_source,
					asyncFilter,
					m_transform,
					m_limit,
					m_offset
				);
			}

			// no easy optimization...
			return base.Where(asyncPredicate);
		}

		public override async Task ExecuteAsync(Action<TResult> action, CancellationToken ct)
		{
			if (action == null) throw new ArgumentNullException("action");

			int? remaining = m_limit;
			int? skipped = m_offset;

			using (var iterator = StartInner())
			{
				while (remaining == null || remaining.Value > 0)
				{
					if (!await iterator.MoveNext(ct).ConfigureAwait(false))
					{ // completed
						break;
					}

					// Filter...

					TSource current = iterator.Current;
					if (m_filter != null)
					{
						if (!m_filter.Async)
						{
							if (!m_filter.Invoke(current)) continue;
						}
						else
						{
							if (!await m_filter.InvokeAsync(current, ct).ConfigureAwait(false)) continue;
						}
					}

					// Skip...

					if (skipped != null)
					{
						if (skipped.Value > 0)
						{ // skip this result
							skipped = skipped.Value - 1;
							continue;
						}
						// we can now start outputing results...
						skipped = null;
					}

					// Transform...

					TResult result;
					if (!m_transform.Async)
					{
						result = m_transform.Invoke(current);
					}
					else
					{
						result = await m_transform.InvokeAsync(current, ct).ConfigureAwait(false);
					}

					// Publish...

					if (remaining != null)
					{ // decrement remaining quota
						remaining = remaining.Value - 1;
					}
					action(result);
				}

				ct.ThrowIfCancellationRequested();
			}
		}

		public override async Task ExecuteAsync(Func<TResult, CancellationToken, Task> asyncAction, CancellationToken ct)
		{
			if (asyncAction == null) throw new ArgumentNullException("asyncAction");

			int? remaining = m_limit;
			int? skipped = m_offset;

			using (var iterator = StartInner())
			{
				while (remaining == null || remaining.Value > 0)
				{
					if (!await iterator.MoveNext(ct).ConfigureAwait(false))
					{ // completed
						break;
					}

					// Filter...

					TSource current = iterator.Current;
					if (m_filter != null)
					{
						if (!m_filter.Async)
						{
							if (!m_filter.Invoke(current)) continue;
						}
						else
						{
							if (!await m_filter.InvokeAsync(current, ct).ConfigureAwait(false)) continue;
						}
					}

					// Skip...

					if (skipped != null)
					{
						if (skipped.Value > 0)
						{ // skip this result
							skipped = skipped.Value - 1;
							continue;
						}
						// we can now start outputing results...
						skipped = null;
					}

					// Transform...

					TResult result;
					if (!m_transform.Async)
					{
						result = m_transform.Invoke(current);
					}
					else
					{
						result = await m_transform.InvokeAsync(current, ct).ConfigureAwait(false);
					}

					// Publish...

					if (remaining != null)
					{ // decrement remaining quota
						remaining = remaining.Value - 1;
					}
					await asyncAction(result, ct).ConfigureAwait(false);
				}

				ct.ThrowIfCancellationRequested();
			}
		}
	}

}