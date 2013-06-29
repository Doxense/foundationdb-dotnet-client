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

namespace FoundationDB.Client
{
	using FoundationDB.Client.Utils;
	using FoundationDB.Linq;
	using System;
	using System.Collections.Generic;
	using System.Diagnostics;
	using System.Linq;
	using System.Threading;
	using System.Threading.Tasks;

	public static class FdbMergeSortExtensions
	{

		public static FdbMergeSortSelectable<TKey, KeyValuePair<Slice, Slice>> MergeSort<TKey>(this FdbTransaction trans, IEnumerable<FdbKeySelectorPair> ranges, Func<Slice, TKey> keySelector, IComparer<TKey> keyComparer = null)
		{
			trans.EnsuresCanReadOrWrite();
			return new FdbMergeSortSelectable<TKey, KeyValuePair<Slice, Slice>>(
				ranges.Select(range => trans.GetRangeCore(range, 0, 0, FdbStreamingMode.Iterator, false, false)),
				default(int?),
				keySelector,
				TaskHelpers.Cache<KeyValuePair<Slice, Slice>>.Identity,
				keyComparer
			);
		}

		public static FdbMergeSortSelectable<TKey, TResult> MergeSort<TKey, TResult>(this FdbTransaction trans, IEnumerable<FdbKeySelectorPair> ranges, Func<Slice, TKey> keySelector, Func<KeyValuePair<Slice, Slice>, TResult> resultSelector, IComparer<TKey> keyComparer = null)
		{
			trans.EnsuresCanReadOrWrite();
			return new FdbMergeSortSelectable<TKey, TResult>(
				ranges.Select(range => trans.GetRangeCore(range, 0, 0, FdbStreamingMode.Iterator, false, false)),
				default(int?),
				keySelector,
				resultSelector,
				keyComparer
			);
		}

	}

	/// <summary>Performs a Merge Sort on several concurrent range queries</summary>
	/// <typeparam name="TKey">Type of values extracted from the keys, that will be used for sorting</typeparam>
	/// <typeparam name="TResult">Type of results returned</typeparam>
	public class FdbMergeSortSelectable<TKey, TResult> : IFdbAsyncEnumerable<TResult>, IFdbAsyncEnumerator<TResult>
	{
		// Takes several range queries that return **SORTED** lists of items
		// - Make all querie's iterators run concurrently
		// - At each step, finds the "smallest" value from all remaining iterators, transform it into a TResult and expose it as the current element
		// - Extract a TKey value from the keys and compare them with the provided comparer 

		// The order of the extracted keys MUST be the same as the order of the binary keys ! This algorithm will NOT work if extracted keys are not in the same order as there binary representation !

		const int STATE_CREATED = 0;
		const int STATE_READY = 1;
		const int STATE_ITERATING = 2;
		const int STATE_COMPLETE = 3;
		const int STATE_DISPOSED = -1;

		private int m_state;
		private FdbRangeQuery[] m_queries;
		private Func<Slice, TKey> m_keySelector;
		private IComparer<TKey> m_keyComparer;
		private Func<KeyValuePair<Slice, Slice>, TResult> m_resultSelector;

		private IteratorState[] m_iterators;
		private TResult m_current;
		private int? m_remaining;

		private struct IteratorState
		{
			public bool Active;
			public IFdbAsyncEnumerator<KeyValuePair<Slice, Slice>> Iterator;
			public Task<bool> Next;
			public bool HasCurrent;
			public TKey Current;
		}

		public FdbMergeSortSelectable(IEnumerable<FdbRangeQuery> queries, int? limit, Func<Slice, TKey> keySelector, Func<KeyValuePair<Slice, Slice>, TResult> resultSelector, IComparer<TKey> comparer = null)
		{
			Contract.Requires(queries != null && (limit == null || limit >= 0) && keySelector != null && resultSelector != null);
			m_queries = queries.ToArray();
			m_remaining = limit;
			m_keySelector = keySelector;
			m_keyComparer = comparer ?? Comparer<TKey>.Default;
			m_resultSelector = resultSelector;
		}

		private FdbMergeSortSelectable(FdbMergeSortSelectable<TKey, TResult> parent)
		{
			m_queries = parent.m_queries;
			m_remaining = parent.m_remaining;
			m_keySelector = parent.m_keySelector;
			m_keyComparer = parent.m_keyComparer;
			m_resultSelector = parent.m_resultSelector;
		}

		/// <summary>Limit the number of elements returned by the MergeSort</summary>
		/// <param name="limit">Maximum number of results to return</param>
		/// <returns>New MergeSort that will only return the specified number of results</returns>
		public FdbMergeSortSelectable<TKey, TResult> Take(int limit)
		{
			if (limit < 0) throw new ArgumentOutOfRangeException("limit", "Value cannot be less than zero");

			return new FdbMergeSortSelectable<TKey, TResult>(this)
			{
				m_remaining = limit
			};
		}

		/// <summary>Apply a transformation on the results of the merge sort</summary>
		public FdbMergeSortSelectable<TKey, TNewResult> Select<TNewResult>(Func<TResult, TNewResult> selector)
		{
			return new FdbMergeSortSelectable<TKey, TNewResult>(
				m_queries,
				m_remaining,
				m_keySelector,
				(kvp) => selector(m_resultSelector(kvp)),
				m_keyComparer
			);
		}

		/// <summary>Starts executing the MergeSort</summary>
		public IFdbAsyncEnumerator<TResult> GetEnumerator()
		{
			var iterator = new FdbMergeSortSelectable<TKey, TResult>(m_queries, m_remaining, m_keySelector, m_resultSelector, m_keyComparer);
			iterator.m_state = STATE_READY;
			return iterator;
		}

		/// <summary>Runs the merge sort, calling the specified action with each result, as it arrives</summary>
		/// <param name="action">Action executed on each result</param>
		/// <param name="ct">Cancellation token (optionnal)</param>
		/// <returns>Task that completes once all results have been processed</returns>
		public Task ForEachAsync(Action<TResult> action, CancellationToken ct = default(CancellationToken))
		{
			return FdbAsyncEnumerable.ForEachAsync<TResult>(this, action, ct);
		}

		/// <summary>Runs the merge sort, and returns the results in an ordered list.</summary>
		/// <param name="ct">Cancellation token (optionnal)</param>
		/// <returns>List that contains all the results of the merge sort, or an empty collection if it produced no results</returns>
		public Task<List<TResult>> ToListAsync(CancellationToken ct = default(CancellationToken))
		{
			if (m_remaining != null && m_remaining.Value <= 1000)
			{
				return FdbAsyncEnumerable.ToListAsync<TResult>(this, m_remaining.Value, ct);
			}
			else
			{
				return FdbAsyncEnumerable.ToListAsync<TResult>(this, ct);
			}
		}

		/// <summary>Runs the merge sort, and returns the results in an ordered list.</summary>
		/// <param name="ct">Cancellation token (optionnal)</param>
		/// <returns>List that contains all the results of the merge sort, or an empty collection if it produced no results</returns>
		public Task<TResult[]> ToArrayAsync(CancellationToken ct = default(CancellationToken))
		{
			if (m_remaining != null && m_remaining.Value <= 1000)
			{
				return FdbAsyncEnumerable.ToArrayAsync<TResult>(this, m_remaining.Value, ct);
			}
			else
			{
				return FdbAsyncEnumerable.ToArrayAsync<TResult>(this, ct);
			}
		}

		Task<bool> IFdbAsyncEnumerator<TResult>.MoveNext(CancellationToken cancellationToken)
		{
			cancellationToken.ThrowIfCancellationRequested();

			switch(m_state)
			{
				case STATE_READY:
				{ // first we need to open all the iterators
					// open all iterators

					if (m_remaining != null && m_remaining.Value < 0)
					{ // empty list ??
						OnComplete();
						return FdbAsyncEnumerable.FalseTask;
					}

					m_iterators = m_queries.Select(query =>
					{
						var state = new IteratorState();
						state.Active = true;
						state.Iterator = query.GetEnumerator();
						state.Next = state.Iterator.MoveNext(cancellationToken);
						return state;
					}).ToArray();

					if (Interlocked.CompareExchange(ref m_state, STATE_ITERATING, STATE_READY) != STATE_READY)
					{
						return FdbAsyncEnumerable.FalseTask;
					}
					return FindNextAsync(cancellationToken);
				}
				case STATE_ITERATING:
				{
					return FindNextAsync(cancellationToken);
				}
				case STATE_COMPLETE:
				{
					return FdbAsyncEnumerable.FalseTask;
				}
				default:
				{
					throw new InvalidOperationException();
				}
			}
		}

		/// <summary>Finds the next smallest item from all the active iterators</summary>
		private async Task<bool> FindNextAsync(CancellationToken ct)
		{
			if (m_remaining != null && m_remaining.Value <= 0)
			{
				OnComplete();
				return false;
			}

			// find the minimum from all the iterators
			int index = -1;
			TKey min = default(TKey);

			for (int i = 0; i < m_iterators.Length;i++)
			{
				if (!m_iterators[i].Active) continue;

				var state = m_iterators[i];

				if (!m_iterators[i].HasCurrent)
				{
					if (!await m_iterators[i].Next.ConfigureAwait(false))
					{ // this one is done, remove it
						m_iterators[i].Iterator.Dispose();
						m_iterators[i] = default(IteratorState);
						continue;
					}

					m_iterators[i].Current = m_keySelector(m_iterators[i].Iterator.Current.Key);
					m_iterators[i].HasCurrent = true;
				}

				if (index == -1 || m_keyComparer.Compare(m_iterators[i].Current, min) < 0)
				{
					min = m_iterators[i].Current;
					index = i;
				}
			}

			if (index == -1)
			{ // nothing left anymore ?
				Interlocked.CompareExchange(ref m_state, STATE_COMPLETE, STATE_ITERATING);
				return false;
			}

			// store the current pair
			m_current = m_resultSelector(m_iterators[index].Iterator.Current);

			// advance the current iterator
			m_iterators[index].HasCurrent = false;
			m_iterators[index].Current = default(TKey);
			if (m_state != STATE_ITERATING)
			{
				OnComplete();
				return false;
			}

			if (m_remaining != null)
			{
				var remaining = m_remaining.Value - 1;
				if (remaining < 0)
				{
					OnComplete();
					return false;
				}
				m_remaining = remaining;
			}

			m_iterators[index].Next = m_iterators[index].Iterator.MoveNext(ct);
			return true;
		}

		TResult IFdbAsyncEnumerator<TResult>.Current
		{
			get { return m_current; }
		}

		private void OnComplete()
		{
			if (Interlocked.CompareExchange(ref m_state, STATE_COMPLETE, STATE_ITERATING) == STATE_ITERATING)
			{
				Cleanup();
			}
		}

		private void Cleanup()
		{
			var iterators = Interlocked.Exchange(ref m_iterators, null);
			if (iterators != null)
			{
				List<Exception> errors = null;

				for (int i = 0; i < iterators.Length; i++)
				{
					if (iterators[i].Active && iterators[i].Iterator != null)
					{
						try
						{
							iterators[i].Iterator.Dispose();
							iterators[i] = new IteratorState();
							iterators[i].Active = false;
						}
						catch (Exception e)
						{
							(errors ?? (errors = new List<Exception>())).Add(e);
						}
					}
				}

				if (errors != null) throw new AggregateException(errors);
			}
		}

		public void Dispose()
		{
			if (Interlocked.Exchange(ref m_state, STATE_DISPOSED) != STATE_DISPOSED)
			{
				try
				{
					Cleanup();
				}
				finally
				{
					m_current = default(TResult);
					m_iterators = null;
					m_remaining = 0;
				}
			}
		}

	}

}
