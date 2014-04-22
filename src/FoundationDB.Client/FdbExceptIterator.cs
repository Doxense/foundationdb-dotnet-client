﻿#region BSD Licence
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

	/// <summary>Returns only the values for the keys that are in the first sub query, but not in the others</summary>
	/// <typeparam name="TSource">Type of the elements from the source async sequences</typeparam>
	/// <typeparam name="TKey">Type of the keys extracted from the source elements</typeparam>
	/// <typeparam name="TResult">Type of the elements of resulting async sequence</typeparam>
	internal sealed class FdbExceptIterator<TSource, TKey, TResult> : FdbQueryMergeIterator<TSource, TKey, TResult>
	{
		public FdbExceptIterator(IEnumerable<IFdbAsyncEnumerable<TSource>> sources, int? limit, Func<TSource, TKey> keySelector, Func<TSource, TResult> resultSelector, IComparer<TKey> comparer)
			: base(sources, limit, keySelector, resultSelector, comparer)
		{ }

		protected override FdbAsyncIterator<TResult> Clone()
		{
			return new FdbExceptIterator<TSource, TKey, TResult>(m_sources, m_limit, m_keySelector, m_resultSelector, m_keyComparer);
		}

		protected override bool FindNext(CancellationToken cancellationToken, out int index, out TSource current)
		{
			index = -1;
			current = default(TSource);

			// we only returns values of the first that are not in the others

			// - if iterator[0] is complete, then stop
			// - take X = iterator[0].Current
			// - set flag_output to true, flag_found to false
			// - for i in 1..n-1:
			//   - if iterator[i].Current < X, advance iterator i and set flag_output to false
			//   - if iterator[i].Current = X, set flag_found to true, flag_output to false
			// - if flag_output is true then output X
			// - if flag_found is true then Advance iterator[0]

			if (!m_iterators[0].Active)
			{ // primary sequence is complete
				return false;
			}


			TKey x = m_iterators[0].Current;
			bool output = true;
			bool discard = false;

			for (int i = 1; i < m_iterators.Length; i++)
			{
				if (!m_iterators[i].Active) continue;

				int cmp = m_keyComparer.Compare(m_iterators[i].Current, x);
				if (cmp <= 0)
				{
					output = false;
					if (cmp == 0) discard = true;
					AdvanceIterator(i, cancellationToken);
				}
			}

			if (output)
			{
				current = m_iterators[0].Iterator.Current;
				index = 0;
			}

			if (output || discard)
			{
				AdvanceIterator(0, cancellationToken);
			}

			return true;
		}

		/// <summary>Apply a transformation on the results of the intersection</summary>
		public override FdbAsyncIterator<TNew> Select<TNew>(Func<TResult, TNew> selector)
		{
			return new FdbExceptIterator<TSource, TKey, TNew>(
				m_sources,
				m_limit,
				m_keySelector,
				(kvp) => selector(m_resultSelector(kvp)),
				m_keyComparer
			);
		}

		/// <summary>Limit the number of elements returned by the intersection</summary>
		/// <param name="limit">Maximum number of results to return</param>
		/// <returns>New Intersect that will only return the specified number of results</returns>
		public override FdbAsyncIterator<TResult> Take(int limit)
		{
			if (limit < 0) throw new ArgumentOutOfRangeException("limit", "Value cannot be less than zero");

			if (m_limit != null && m_limit < limit) return this;

			return new FdbExceptIterator<TSource, TKey, TResult>(
				m_sources,
				limit,
				m_keySelector,
				m_resultSelector,
				m_keyComparer
			);
		}

	}
}
