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

		public static IFdbAsyncEnumerable<KeyValuePair<Slice, Slice>> MergeSort<TKey>(this FdbTransaction trans, IEnumerable<FdbKeySelectorPair> ranges, Func<KeyValuePair<Slice, Slice>, TKey> keySelector, IComparer<TKey> keyComparer = null)
		{
			trans.EnsuresCanReadOrWrite();
			return new FdbMergeSortSelectable<KeyValuePair<Slice, Slice>, TKey, KeyValuePair<Slice, Slice>>(
				ranges.Select(range => trans.GetRangeCore(range, 0, 0, FdbStreamingMode.Iterator, false, false)),
				default(int?),
				keySelector,
				TaskHelpers.Cache<KeyValuePair<Slice, Slice>>.Identity,
				keyComparer
			);
		}

		public static IFdbAsyncEnumerable<TResult> MergeSort<TKey, TResult>(this FdbTransaction trans, IEnumerable<FdbKeySelectorPair> ranges, Func<KeyValuePair<Slice, Slice>, TKey> keySelector, Func<KeyValuePair<Slice, Slice>, TResult> resultSelector, IComparer<TKey> keyComparer = null)
		{
			trans.EnsuresCanReadOrWrite();
			return new FdbMergeSortSelectable<KeyValuePair<Slice, Slice>, TKey, TResult>(
				ranges.Select(range => trans.GetRangeCore(range, 0, 0, FdbStreamingMode.Iterator, false, false)),
				default(int?),
				keySelector,
				resultSelector,
				keyComparer
			);
		}

		public static IFdbAsyncEnumerable<KeyValuePair<Slice, Slice>> Intersect<TKey>(this FdbTransaction trans, IEnumerable<FdbKeySelectorPair> ranges, Func<KeyValuePair<Slice, Slice>, TKey> keySelector, IComparer<TKey> keyComparer = null)
		{
			trans.EnsuresCanReadOrWrite();
			return new FdbIntersectIterator<KeyValuePair<Slice, Slice>, TKey, KeyValuePair<Slice, Slice>>(
				ranges.Select(range => trans.GetRangeCore(range, 0, 0, FdbStreamingMode.Iterator, false, false)),
				default(int?),
				keySelector,
				TaskHelpers.Cache<KeyValuePair<Slice, Slice>>.Identity,
				keyComparer
			);
		}

		public static IFdbAsyncEnumerable<TResult> Intersect<TKey, TResult>(this FdbTransaction trans, IEnumerable<FdbKeySelectorPair> ranges, Func<KeyValuePair<Slice, Slice>, TKey> keySelector, Func<KeyValuePair<Slice, Slice>, TResult> resultSelector, IComparer<TKey> keyComparer = null)
		{
			trans.EnsuresCanReadOrWrite();
			return new FdbIntersectIterator<KeyValuePair<Slice, Slice>, TKey, TResult>(
				ranges.Select(range => trans.GetRangeCore(range, 0, 0, FdbStreamingMode.Iterator, false, false)),
				default(int?),
				keySelector,
				resultSelector,
				keyComparer
			);
		}

		public static IFdbAsyncEnumerable<TResult> Intersect<TKey, TResult>(this IFdbAsyncEnumerable<TResult> first, IFdbAsyncEnumerable<TResult> second, Func<TResult, TKey> keySelector, IComparer<TKey> keyComparer = null)
		{
			return new FdbIntersectIterator<TResult, TKey, TResult>(
				new[] { first, second },
				null,
				keySelector,
				TaskHelpers.Cache<TResult>.Identity,
				keyComparer
			);
		}

		public static IFdbAsyncEnumerable<TResult> Intersect<TResult>(this IFdbAsyncEnumerable<TResult> first, IFdbAsyncEnumerable<TResult> second, IComparer<TResult> comparer = null)
		{
			return new FdbIntersectIterator<TResult, TResult, TResult>(
				new [] { first, second },
				null,
				TaskHelpers.Cache<TResult>.Identity,
				TaskHelpers.Cache<TResult>.Identity,
				comparer
			);
		}

	}

	internal class FdbMergeSortSelectable<TSource, TKey, TResult> : FdbQueryMergeIterator<TSource, TKey, TResult>
	{

		public FdbMergeSortSelectable(IEnumerable<IFdbAsyncEnumerable<TSource>> sources, int? limit, Func<TSource, TKey> keySelector, Func<TSource, TResult> resultSelector, IComparer<TKey> comparer)
			: base(sources, limit, keySelector, resultSelector, comparer)
		{ }

		protected override FdbAsyncEnumerable.AsyncIterator<TResult> Clone()
		{
			return new FdbMergeSortSelectable<TSource, TKey, TResult>(m_sources, m_limit, m_keySelector, m_resultSelector, m_keyComparer);
		}

		protected override bool FindNext(CancellationToken ct, out int index, out TSource current)
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
					AdvanceIterator(index, ct);
				}
			}

			return index != -1;
		}


		/// <summary>Apply a transformation on the results of the merge sort</summary>
		public override FdbAsyncEnumerable.AsyncIterator<TNew> Select<TNew>(Func<TResult, TNew> selector)
		{
			return new FdbMergeSortSelectable<TSource, TKey, TNew>(
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
		public override FdbAsyncEnumerable.AsyncIterator<TResult> Take(int limit)
		{
			if (limit < 0) throw new ArgumentOutOfRangeException("limit", "Value cannot be less than zero");

			if (m_limit != null && m_limit < limit) return this;

			return new FdbMergeSortSelectable<TSource, TKey, TResult>(
				m_sources,
				limit,
				m_keySelector,
				m_resultSelector,
				m_keyComparer
			);
		}

	}

}
