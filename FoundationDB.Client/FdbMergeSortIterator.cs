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
	using FoundationDB.Linq;
	using System;
	using System.Collections.Generic;
	using System.Threading;

	/// <summary>Merge all the elements of several ordered queries into one single async sequence</summary>
	/// <typeparam name="TSource">Type of the elements from the source async sequences</typeparam>
	/// <typeparam name="TKey">Type of the keys extracted from the source elements</typeparam>
	/// <typeparam name="TResult">Type of the elements of resulting async sequence</typeparam>
	internal sealed class FdbMergeSortIterator<TSource, TKey, TResult> : FdbQueryMergeIterator<TSource, TKey, TResult>
	{

		public FdbMergeSortIterator(IEnumerable<IFdbAsyncEnumerable<TSource>> sources, int? limit, Func<TSource, TKey> keySelector, Func<TSource, TResult> resultSelector, IComparer<TKey> comparer)
			: base(sources, limit, keySelector, resultSelector, comparer)
		{ }

		protected override FdbAsyncIterator<TResult> Clone()
		{
			return new FdbMergeSortIterator<TSource, TKey, TResult>(m_sources, m_limit, m_keySelector, m_resultSelector, m_keyComparer);
		}

		protected override bool FindNext(CancellationToken cancellationToken, out int index, out TSource current)
		{
			index = -1;
			current = default(TSource);
			TKey min = default(TKey);

			for (int i = 0; i < m_iterators.Length; i++)
			{
				if (!m_iterators[i].Active) continue;

				if (index == -1 || m_keyComparer.Compare(m_iterators[i].Current, min) < 0)
				{
					min = m_iterators[i].Current;
					index = i;
				}
			}

			if (index >= 0)
			{
				current = m_iterators[index].Iterator.Current;
				if (m_remaining == null || m_remaining.Value > 1)
				{ // start getting the next value on this iterator
					AdvanceIterator(index, cancellationToken);
				}
			}

			return index != -1;
		}


		/// <summary>Apply a transformation on the results of the merge sort</summary>
		public override FdbAsyncIterator<TNew> Select<TNew>(Func<TResult, TNew> selector)
		{
			return new FdbMergeSortIterator<TSource, TKey, TNew>(
				m_sources,
				m_limit,
				m_keySelector,
				(kvp) => selector(m_resultSelector(kvp)),
				m_keyComparer
			);
		}

		/// <summary>Limit the number of elements returned by the MergeSort</summary>
		/// <param name="limit">Maximum number of results to return</param>
		/// <returns>New MergeSort that will only return the specified number of results</returns>
		public override FdbAsyncIterator<TResult> Take(int limit)
		{
			if (limit < 0) throw new ArgumentOutOfRangeException("limit", "Value cannot be less than zero");

			if (m_limit != null && m_limit < limit) return this;

			return new FdbMergeSortIterator<TSource, TKey, TResult>(
				m_sources,
				limit,
				m_keySelector,
				m_resultSelector,
				m_keyComparer
			);
		}

	}

}
