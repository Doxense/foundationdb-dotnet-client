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

namespace Doxense.Linq.Async.Iterators
{
	using Doxense.Linq.Async.Expressions;

	/// <summary>Iterates over an async sequence of items</summary>
	/// <typeparam name="TSource">Type of elements of the inner async sequence</typeparam>
	/// <typeparam name="TResult">Type of elements of the outer async sequence</typeparam>
	public sealed class WhereSelectAsyncIterator<TSource, TResult> : AsyncFilterIterator<TSource, TResult>
	{
		private readonly AsyncFilterExpression<TSource>? m_filter;
		private readonly AsyncTransformExpression<TSource, TResult> m_transform;

		//note: both limit and offset are applied AFTER filtering!
		private readonly int? m_limit;
		private readonly int? m_offset;

		private int? m_remaining;
		private int? m_skipped;

		public WhereSelectAsyncIterator(
			IAsyncEnumerable<TSource> source,
			AsyncFilterExpression<TSource>? filter,
			AsyncTransformExpression<TSource, TResult> transform,
			int? limit,
			int? offset
		)
			: base(source)
		{
			Contract.Debug.Requires(transform != null); // must do at least something
			Contract.Debug.Requires((limit ?? 0) >= 0 && (offset ?? 0) >= 0); // bounds cannot be negative

			m_filter = filter;
			m_transform = transform;
			m_limit = limit;
			m_offset = offset;
		}

		protected override AsyncIterator<TResult> Clone()
		{
			return new WhereSelectAsyncIterator<TSource, TResult>(m_source, m_filter, m_transform, m_limit, m_offset);
		}

		protected override ValueTask<bool> OnFirstAsync()
		{
			m_remaining = m_limit;
			m_skipped = m_offset;
			return base.OnFirstAsync();
		}

		protected override async ValueTask<bool> OnNextAsync()
		{
			if (m_remaining is <= 0)
			{ // reached limit!
				return await Completed().ConfigureAwait(false);
			}

			var iterator = m_iterator;
			Contract.Debug.Requires(iterator != null);

			while (!m_ct.IsCancellationRequested)
			{
				if (!await iterator.MoveNextAsync().ConfigureAwait(false))
				{ // completed
					return await Completed().ConfigureAwait(false);
				}
				if (m_ct.IsCancellationRequested) break;

				#region Filtering...

				TSource current = iterator.Current;
				var filter = m_filter;
				if (filter != null)
				{
					if (!filter.Async)
					{
						if (!filter.Invoke(current)) continue;
					}
					else
					{
						if (!await filter.InvokeAsync(current, m_ct).ConfigureAwait(false)) continue;
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
					// we can now start outputting results...
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
					result = await m_transform.InvokeAsync(current, m_ct).ConfigureAwait(false);
				}

				#endregion

				#region Publishing...

				// decrement remaining quota
				--m_remaining;

				return Publish(result);

				#endregion
			}

			return await Canceled().ConfigureAwait(false);
		}

		public override AsyncIterator<TNew> Select<TNew>(Func<TResult, TNew> selector)
		{
			Contract.NotNull(selector);

			return new WhereSelectAsyncIterator<TSource, TNew>(
				m_source,
				m_filter,
				m_transform.Then(new AsyncTransformExpression<TResult, TNew>(selector)),
				m_limit,
				m_offset
			);
		}

		public override AsyncIterator<TNew> Select<TNew>(Func<TResult, CancellationToken, Task<TNew>> asyncSelector)
		{
			Contract.NotNull(asyncSelector);

			return new WhereSelectAsyncIterator<TSource, TNew>(
				m_source,
				m_filter,
				m_transform.Then(new AsyncTransformExpression<TResult, TNew>(asyncSelector)),
				m_limit,
				m_offset
			);
		}

		public override AsyncIterator<TNew> SelectMany<TNew>(Func<TResult, IEnumerable<TNew>> selector)
		{
			Contract.NotNull(selector);

			if (m_filter == null && m_limit == null && m_offset == null)
			{
				return new SelectManyAsyncIterator<TSource, TNew>(
					m_source,
					m_transform.Then(new AsyncTransformExpression<TResult, IEnumerable<TNew>>(selector))
				);
			}

			// other cases are too complex :(
			return base.SelectMany(selector);
		}

		public override AsyncIterator<TNew> SelectMany<TNew>(Func<TResult, CancellationToken, Task<IEnumerable<TNew>>> asyncSelector)
		{
			Contract.NotNull(asyncSelector);

			if (m_filter == null && m_limit == null && m_offset == null)
			{
				return new SelectManyAsyncIterator<TSource, TNew>(
					m_source,
					m_transform.Then(new AsyncTransformExpression<TResult, IEnumerable<TNew>>(asyncSelector))
				);
			}

			// other cases are too complex :(
			return base.SelectMany(asyncSelector);
		}

		public override AsyncIterator<TResult> Take(int limit)
		{
			if (limit < 0) throw new ArgumentOutOfRangeException(nameof(limit), "Limit cannot be less than zero");

			if (m_limit != null && m_limit.Value <= limit)
			{
				// we are already taking less then that
				return this;
			}

			return new WhereSelectAsyncIterator<TSource, TResult>(
				m_source,
				m_filter,
				m_transform,
				limit,
				m_offset
			);
		}

		public override AsyncIterator<TResult> Skip(int offset)
		{
			if (offset < 0) throw new ArgumentOutOfRangeException(nameof(offset), "Offset cannot be less than zero");

			if (offset == 0) return this;

			if (m_offset != null) offset += m_offset.Value;

			return new WhereSelectAsyncIterator<TSource, TResult>(
				m_source,
				m_filter,
				m_transform,
				m_limit,
				offset
			);
		}

		public override AsyncIterator<TResult> Where(Func<TResult, bool> predicate)
		{
			Contract.NotNull(predicate);

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
				// but currently WhereSelectAsyncIterator<> filters before transformations !

				return new WhereSelectAsyncIterator<TSource, TResult>(
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

		public override AsyncIterator<TResult> Where(Func<TResult, CancellationToken, Task<bool>> asyncPredicate)
		{
			Contract.NotNull(asyncPredicate);

			// note: the only possible optimization here is if TSource == TResult, then we can combine both predicates
			// remember: limit/offset are applied AFTER the filtering, so can only combine if they are null
			if (m_limit == null && (m_offset == null || m_offset.Value == 0) && typeof(TSource) == typeof(TResult)) //BUGBUG: type comparison maybe should check derived classes also ?
			{
				var asyncFilter = new AsyncFilterExpression<TSource>((Func<TSource, CancellationToken, Task<bool>>)(Delegate)asyncPredicate);
				if (m_filter != null) asyncFilter = m_filter.AndAlso(asyncFilter);

				//BUGBUG: if the query already has a select, it should be evaluated BEFORE the new filter,
				// but currently WhereSelectAsyncIterator<> filters before transformations !

				return new WhereSelectAsyncIterator<TSource, TResult>(
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
			Contract.NotNull(action);

			int? remaining = m_limit;
			int? skipped = m_offset;

			await using(var iterator = StartInner(ct))
			{
				while (remaining == null || remaining.Value > 0)
				{
					if (!await iterator.MoveNextAsync().ConfigureAwait(false))
					{ // completed
						break;
					}

					// Filter...

					TSource current = iterator.Current;
					var filter = m_filter;
					if (filter != null)
					{
						if (!filter.Async)
						{
							// ReSharper disable once MethodHasAsyncOverloadWithCancellation
							if (!filter.Invoke(current)) continue;
						}
						else
						{
							if (!await filter.InvokeAsync(current, ct).ConfigureAwait(false)) continue;
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
						// we can now start outputting results...
						skipped = null;
					}

					// Transform...

					TResult result;
					if (!m_transform.Async)
					{
						// ReSharper disable once MethodHasAsyncOverloadWithCancellation
						result = m_transform.Invoke(current);
					}
					else
					{
						result = await m_transform.InvokeAsync(current, ct).ConfigureAwait(false);
					}

					// Publish...

					// decrement remaining quota
					--remaining;
					action(result);
				}

				ct.ThrowIfCancellationRequested();
			}
		}

		public override async Task ExecuteAsync<TState>(TState state, Action<TState, TResult> action, CancellationToken ct)
		{
			Contract.NotNull(action);

			int? remaining = m_limit;
			int? skipped = m_offset;

			await using(var iterator = StartInner(ct))
			{
				while (remaining == null || remaining.Value > 0)
				{
					if (!await iterator.MoveNextAsync().ConfigureAwait(false))
					{ // completed
						break;
					}

					// Filter...

					TSource current = iterator.Current;
					var filter = m_filter;
					if (filter != null)
					{
						if (!filter.Async)
						{
							// ReSharper disable once MethodHasAsyncOverloadWithCancellation
							if (!filter.Invoke(current)) continue;
						}
						else
						{
							if (!await filter.InvokeAsync(current, ct).ConfigureAwait(false)) continue;
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
						// we can now start outputting results...
						skipped = null;
					}

					// Transform...

					TResult result;
					if (!m_transform.Async)
					{
						// ReSharper disable once MethodHasAsyncOverloadWithCancellation
						result = m_transform.Invoke(current);
					}
					else
					{
						result = await m_transform.InvokeAsync(current, ct).ConfigureAwait(false);
					}

					// Publish...

					// decrement remaining quota
					--remaining;
					action(state, result);
				}

				ct.ThrowIfCancellationRequested();
			}
		}

		public override async Task<TAggregate> ExecuteAsync<TAggregate>(TAggregate seed, Func<TAggregate, TResult, TAggregate> action, CancellationToken ct)
		{
			Contract.NotNull(action);

			int? remaining = m_limit;
			int? skipped = m_offset;

			await using(var iterator = StartInner(ct))
			{
				while (remaining == null || remaining.Value > 0)
				{
					if (!await iterator.MoveNextAsync().ConfigureAwait(false))
					{ // completed
						break;
					}

					// Filter...

					TSource current = iterator.Current;
					var filter = m_filter;
					if (filter != null)
					{
						if (!filter.Async)
						{
							// ReSharper disable once MethodHasAsyncOverloadWithCancellation
							if (!filter.Invoke(current)) continue;
						}
						else
						{
							if (!await filter.InvokeAsync(current, ct).ConfigureAwait(false)) continue;
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
						// we can now start outputting results...
						skipped = null;
					}

					// Transform...

					TResult result;
					if (!m_transform.Async)
					{
						// ReSharper disable once MethodHasAsyncOverloadWithCancellation
						result = m_transform.Invoke(current);
					}
					else
					{
						result = await m_transform.InvokeAsync(current, ct).ConfigureAwait(false);
					}

					// Publish...

					// decrement remaining quota
					--remaining;
					seed = action(seed, result);
				}

				ct.ThrowIfCancellationRequested();
			}

			return seed;
		}

		public override async Task ExecuteAsync(Func<TResult, CancellationToken, Task> asyncAction, CancellationToken ct)
		{
			Contract.NotNull(asyncAction);

			int? remaining = m_limit;
			int? skipped = m_offset;

			await using (var iterator = StartInner(ct))
			{
				while (remaining == null || remaining.Value > 0)
				{
					if (!await iterator.MoveNextAsync().ConfigureAwait(false))
					{ // completed
						break;
					}

					// Filter...

					TSource current = iterator.Current;
					var filter = m_filter;
					if (filter != null)
					{
						if (!filter.Async)
						{
							// ReSharper disable once MethodHasAsyncOverloadWithCancellation
							if (!filter.Invoke(current)) continue;
						}
						else
						{
							if (!await filter.InvokeAsync(current, ct).ConfigureAwait(false)) continue;
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
						// we can now start outputting results...
						skipped = null;
					}

					// Transform...

					TResult result;
					if (!m_transform.Async)
					{
						// ReSharper disable once MethodHasAsyncOverloadWithCancellation
						result = m_transform.Invoke(current);
					}
					else
					{
						result = await m_transform.InvokeAsync(current, ct).ConfigureAwait(false);
					}

					// Publish...

					// decrement remaining quota
					--remaining;

					await asyncAction(result, ct).ConfigureAwait(false);
				}

				ct.ThrowIfCancellationRequested();
			}
		}

	}

}
