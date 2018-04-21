#region BSD Licence
/* Copyright (c) 2013-2018, Doxense SAS
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
	using System;
	using System.Collections.Generic;
	using System.Threading;
	using System.Threading.Tasks;
	using Doxense.Diagnostics.Contracts;
	using JetBrains.Annotations;

	/// <summary>Iterates over an async sequence of items</summary>
	/// <typeparam name="TSource">Type of elements of the inner async sequence</typeparam>
	/// <typeparam name="TResult">Type of elements of the outer async sequence</typeparam>
	internal sealed class FdbSelectManyAsyncIterator<TSource, TResult> : FdbAsyncFilterIterator<TSource, TResult>
	{
		private readonly AsyncTransformExpression<TSource, IEnumerable<TResult>> m_selector;
		private IEnumerator<TResult> m_batch;

		public FdbSelectManyAsyncIterator([NotNull] IFdbAsyncEnumerable<TSource> source, AsyncTransformExpression<TSource, IEnumerable<TResult>> selector)
			: base(source)
		{
			// Must have at least one, but not both
			Contract.Requires(selector != null);

			m_selector = selector;
		}

		protected override FdbAsyncIterator<TResult> Clone()
		{
			return new FdbSelectManyAsyncIterator<TSource, TResult>(m_source, m_selector);
		}

		protected override async Task<bool> OnNextAsync(CancellationToken ct)
		{
			// if we are in a batch, iterate over it
			// if not, wait for the next batch

			while (!ct.IsCancellationRequested)
			{

				if (m_batch == null)
				{

					if (!await m_iterator.MoveNextAsync(ct).ConfigureAwait(false))
					{ // inner completed
						return Completed();
					}

					if (ct.IsCancellationRequested) break;

					IEnumerable<TResult> sequence;
					if (!m_selector.Async)
					{
						sequence = m_selector.Invoke(m_iterator.Current);
					}
					else
					{
						sequence = await m_selector.InvokeAsync(m_iterator.Current, ct).ConfigureAwait(false);
					}
					if (sequence == null) throw new InvalidOperationException("The inner sequence returned a null collection");

					m_batch = sequence.GetEnumerator();
					Contract.Assert(m_batch != null);
				}

				if (!m_batch.MoveNext())
				{ // the current batch is exhausted, move to the next
					m_batch.Dispose();
					m_batch = null;
					continue;
				}

				return Publish(m_batch.Current);
			}

			return Canceled(ct);
		}

		protected override void Cleanup()
		{
			try
			{
				if (m_batch != null)
				{
					m_batch.Dispose();
				}
			}
			finally
			{
				m_batch = null;
				base.Cleanup();
			}
		}
	}

	/// <summary>Iterates over an async sequence of items</summary>
	/// <typeparam name="TSource">Type of elements of the inner async sequence</typeparam>
	/// <typeparam name="TCollection">Type of the elements of the sequences produced from each <typeparamref name="TSource"/> elements</typeparam>
	/// <typeparam name="TResult">Type of elements of the outer async sequence</typeparam>
	internal sealed class FdbSelectManyAsyncIterator<TSource, TCollection, TResult> : FdbAsyncFilterIterator<TSource, TResult>
	{
		private readonly AsyncTransformExpression<TSource, IEnumerable<TCollection>> m_collectionSelector;
		private readonly Func<TSource, TCollection, TResult> m_resultSelector;
		private TSource m_sourceCurrent;
		private IEnumerator<TCollection> m_batch;

		public FdbSelectManyAsyncIterator(
			[NotNull] IFdbAsyncEnumerable<TSource> source,
			AsyncTransformExpression<TSource, IEnumerable<TCollection>> collectionSelector,
			[NotNull] Func<TSource, TCollection, TResult> resultSelector
		)
			: base(source)
		{
			Contract.Requires(collectionSelector != null && resultSelector != null);

			m_collectionSelector = collectionSelector;
			m_resultSelector = resultSelector;
		}

		protected override FdbAsyncIterator<TResult> Clone()
		{
			return new FdbSelectManyAsyncIterator<TSource, TCollection, TResult>(m_source, m_collectionSelector, m_resultSelector);
		}

		protected override async Task<bool> OnNextAsync(CancellationToken ct)
		{
			// if we are in a batch, iterate over it
			// if not, wait for the next batch

			while (!ct.IsCancellationRequested)
			{

				if (m_batch == null)
				{

					if (!await m_iterator.MoveNextAsync(ct).ConfigureAwait(false))
					{ // inner completed
						return Completed();
					}

					if (ct.IsCancellationRequested) break;

					m_sourceCurrent = m_iterator.Current;

					IEnumerable<TCollection> sequence;

					if (!m_collectionSelector.Async)
					{
						sequence = m_collectionSelector.Invoke(m_sourceCurrent);
					}
					else
					{
						sequence = await m_collectionSelector.InvokeAsync(m_sourceCurrent, ct).ConfigureAwait(false);
					}
					if (sequence == null) throw new InvalidOperationException("The inner sequence returned a null collection");

					m_batch = sequence.GetEnumerator();
					Contract.Requires(m_batch != null);
				}

				if (!m_batch.MoveNext())
				{ // the current batch is exhausted, move to the next
					m_batch.Dispose();
					m_batch = null;
					m_sourceCurrent = default(TSource);
					continue;
				}

				return Publish(m_resultSelector(m_sourceCurrent, m_batch.Current));
			}

			return Canceled(ct);
		}

		protected override void Cleanup()
		{
			try
			{
				if (m_batch != null)
				{
					m_batch.Dispose();
				}
			}
			finally
			{
				m_batch = null;
				base.Cleanup();
			}
		}

	}

}
