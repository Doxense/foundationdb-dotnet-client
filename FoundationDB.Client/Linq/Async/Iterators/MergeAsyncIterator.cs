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

namespace Doxense.Linq.Async.Iterators
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using System.Threading.Tasks;
	using Doxense.Async;
	using Doxense.Diagnostics.Contracts;

	/// <summary>Performs a Merge Sort on several concurrent range queries</summary>
	/// <typeparam name="TSource">Type of the elements in the source queries</typeparam>
	/// <typeparam name="TKey">Type of values extracted from the keys, that will be used for sorting</typeparam>
	/// <typeparam name="TResult">Type of results returned</typeparam>
	public abstract class MergeAsyncIterator<TSource, TKey, TResult> : AsyncIterator<TResult>
	{
		// Takes several range queries that return **SORTED** lists of items
		// - Make all querie's iterators run concurrently
		// - At each step, finds the "smallest" value from all remaining iterators, transform it into a TResult and expose it as the current element
		// - Extract a TKey value from the keys and compare them with the provided comparer

		// The order of the extracted keys MUST be the same as the order of the binary keys ! This algorithm will NOT work if extracted keys are not in the same order as there binary representation !

		protected IEnumerable<IAsyncEnumerable<TSource>> m_sources;
		protected Func<TSource, TKey> m_keySelector;
		protected IComparer<TKey> m_keyComparer;
		protected Func<TSource, TResult> m_resultSelector;
		protected int? m_limit;

		protected IteratorState[] m_iterators;
		protected int? m_remaining;

		protected struct IteratorState
		{
			public bool Active;
			public IAsyncEnumerator<TSource> Iterator;
			public Task<bool> Next;
			public bool HasCurrent;
			public TKey Current;
		}

		protected MergeAsyncIterator(IEnumerable<IAsyncEnumerable<TSource>> sources, int? limit, Func<TSource, TKey> keySelector, Func<TSource, TResult> resultSelector, IComparer<TKey> comparer)
		{
			Contract.Requires(sources != null && (limit == null || limit >= 0) && keySelector != null && resultSelector != null);
			m_sources = sources;
			m_limit = limit;
			m_keySelector = keySelector;
			m_keyComparer = comparer ?? Comparer<TKey>.Default;
			m_resultSelector = resultSelector;
		}

		protected override Task<bool> OnFirstAsync()
		{
			if (m_remaining != null && m_remaining.Value < 0)
			{ // empty list ??
				return TaskHelpers.FromResult(Completed());
			}

			// even if the caller only wants the first, we will probably need to read more than that...
			var mode = m_mode;
			if (mode == AsyncIterationHint.Head) mode = AsyncIterationHint.Iterator;

			var sources = m_sources.ToArray();
			var iterators = new IteratorState[sources.Length];
			try
			{
				// start all the iterators
				for (int i = 0; i < sources.Length;i++)
				{
					var state = new IteratorState
					{
						Active = true,
						Iterator = sources[i].GetEnumerator(m_ct, mode)
					};
					state.Next = state.Iterator.MoveNextAsync();

					iterators[i] = state;
				}

				m_remaining = m_limit;
				return TaskHelpers.FromResult(iterators.Length > 0);
			}
			catch(Exception e)
			{
				// dispose already opened iterators
				var tmp = iterators;
				iterators = null;
				try { Cleanup(tmp); } catch { }
				return Task.FromException<bool>(e);
			}
			finally
			{
				m_iterators = iterators;
			}
		}

		/// <summary>Finds the next smallest item from all the active iterators</summary>
		protected override async Task<bool> OnNextAsync()
		{
			if (m_remaining != null && m_remaining.Value <= 0)
			{
				return Completed();
			}

			int index;
			TSource current;

			do
			{
				// ensure all iterators are ready
				for (int i = 0; i < m_iterators.Length;i++)
				{
					if (!m_iterators[i].Active) continue;

					if (!m_iterators[i].HasCurrent)
					{
						if (!await m_iterators[i].Next.ConfigureAwait(false))
						{ // this one is done, remove it
							m_iterators[i].Iterator.Dispose();
							m_iterators[i] = default(IteratorState);
							continue;
						}

						m_iterators[i].Current = m_keySelector(m_iterators[i].Iterator.Current);
						m_iterators[i].HasCurrent = true;
					}

				}

				// find the next value to advance
				if (!FindNext(out index, out current))
				{ // nothing left anymore ?
					return Completed();
				}
			}
			while(index < 0);

			var result = m_resultSelector(current);

			// store the current pair
			if (!Publish(result))
			{ // something happened..
				return false;
			}

			// advance the current iterator
			m_remaining = m_remaining - 1;

			return true;
		}

		protected abstract bool FindNext(out int index, out TSource current);

		protected void AdvanceIterator(int index)
		{
			m_iterators[index].HasCurrent = false;
			m_iterators[index].Current = default(TKey);
			m_iterators[index].Next = m_iterators[index].Iterator.MoveNextAsync();
		}

		private static void Cleanup(IteratorState[] iterators)
		{
			if (iterators != null)
			{
				List<Exception> errors = null;

				for (int i = 0; i < iterators.Length; i++)
				{
					if (iterators[i].Active && iterators[i].Iterator != null)
					{
						try
						{
							var iterator = iterators[i].Iterator;
							iterators[i] = new IteratorState();
							iterator.Dispose();
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

		protected override void Cleanup()
		{
			try
			{
				Cleanup(m_iterators);
			}
			finally
			{
				m_iterators = null;
				m_remaining = 0;
			}
		}

	}

}
