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

	/// <summary>Iterates over an async sequence of items</summary>
	/// <typeparam name="TSource">Type of elements of the inner async sequence</typeparam>
	/// <typeparam name="TResult">Type of elements of the outer async sequence</typeparam>
	internal sealed class FdbSelectManyAsyncIterator<TSource, TResult> : FdbAsyncFilter<TSource, TResult>
	{
		private Func<TSource, IEnumerable<TResult>> m_selector;
		private Func<TSource, CancellationToken, Task<IEnumerable<TResult>>> m_asyncSelector;
		private IEnumerator<TResult> m_batch;

		public FdbSelectManyAsyncIterator(IFdbAsyncEnumerable<TSource> source, Func<TSource, IEnumerable<TResult>> selector, Func<TSource, CancellationToken, Task<IEnumerable<TResult>>> asyncSelector)
			: base(source)
		{
			// Must have at least one, but not both
			Contract.Requires(selector != null ^ asyncSelector != null);

			m_selector = selector;
			m_asyncSelector = asyncSelector;
		}

		protected override FdbAsyncIterator<TResult> Clone()
		{
			return new FdbSelectManyAsyncIterator<TSource, TResult>(m_source, m_selector, m_asyncSelector);
		}

		protected override async Task<bool> OnNextAsync(CancellationToken cancellationToken)
		{
			// if we are in a batch, iterate over it
			// if not, wait for the next batch

			while (!cancellationToken.IsCancellationRequested)
			{

				if (m_batch == null)
				{

					if (!await m_iterator.MoveNext(cancellationToken).ConfigureAwait(false))
					{ // inner completed
						return Completed();
					}

					if (cancellationToken.IsCancellationRequested) break;

					IEnumerable<TResult> sequence;
					if (m_selector != null)
					{
						sequence = m_selector(m_iterator.Current);
					}
					else
					{
						sequence = await m_asyncSelector(m_iterator.Current, cancellationToken).ConfigureAwait(false);
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

			return Canceled(cancellationToken);
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
	/// <typeparam name="TResult">Type of elements of the outer async sequence</typeparam>
	internal sealed class SelectManyAsyncIterator<TSource, TCollection, TResult> : FdbAsyncFilter<TSource, TResult>
	{
		private Func<TSource, IEnumerable<TCollection>> m_collectionSelector;
		private Func<TSource, CancellationToken, Task<IEnumerable<TCollection>>> m_asyncCollectionSelector;
		private Func<TSource, TCollection, TResult> m_resultSelector;
		private TSource m_sourceCurrent;
		private IEnumerator<TCollection> m_batch;

		public SelectManyAsyncIterator(
			IFdbAsyncEnumerable<TSource> source,
			Func<TSource, IEnumerable<TCollection>> collectionSelector,
			Func<TSource, CancellationToken, Task<IEnumerable<TCollection>>> asyncCollectionSelector,
			Func<TSource, TCollection, TResult> resultSelector
		)
			: base(source)
		{
			// must have at least one but not both
			Contract.Requires(collectionSelector != null ^ asyncCollectionSelector != null);
			Contract.Requires(resultSelector != null);

			m_collectionSelector = collectionSelector;
			m_asyncCollectionSelector = asyncCollectionSelector;
			m_resultSelector = resultSelector;
		}

		protected override FdbAsyncIterator<TResult> Clone()
		{
			return new SelectManyAsyncIterator<TSource, TCollection, TResult>(m_source, m_collectionSelector, m_asyncCollectionSelector, m_resultSelector);
		}

		protected override async Task<bool> OnNextAsync(CancellationToken cancellationToken)
		{
			// if we are in a batch, iterate over it
			// if not, wait for the next batch

			while (!cancellationToken.IsCancellationRequested)
			{

				if (m_batch == null)
				{

					if (!await m_iterator.MoveNext(cancellationToken).ConfigureAwait(false))
					{ // inner completed
						return Completed();
					}

					if (cancellationToken.IsCancellationRequested) break;

					m_sourceCurrent = m_iterator.Current;

					IEnumerable<TCollection> sequence;

					if (m_collectionSelector != null)
					{
						sequence = m_collectionSelector(m_sourceCurrent);
					}
					else
					{
						sequence = await m_asyncCollectionSelector(m_sourceCurrent, cancellationToken).ConfigureAwait(false);
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

			return Canceled(cancellationToken);
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
