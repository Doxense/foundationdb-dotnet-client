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

	/// <summary>Returns only the values for the keys that are in all the sub queries</summary>
	/// <typeparam name="TSource">Type of the elements from the source async sequences</typeparam>
	/// <typeparam name="TKey">Type of the keys extracted from the source elements</typeparam>
	/// <typeparam name="TResult">Type of the elements of resulting async sequence</typeparam>
	internal class FdbIntersectIterator<TSource, TKey, TResult> : FdbQueryMergeIterator<TSource, TKey, TResult>
	{
		public FdbIntersectIterator(IEnumerable<IFdbAsyncEnumerable<TSource>> sources, int? limit, Func<TSource, TKey> keySelector, Func<TSource, TResult> resultSelector, IComparer<TKey> comparer)
			: base(sources, limit, keySelector, resultSelector, comparer)
		{ }

		protected override FdbAsyncIterator<TResult> Clone()
		{
			return new FdbIntersectIterator<TSource, TKey, TResult>(m_sources, m_limit, m_keySelector, m_resultSelector, m_keyComparer);
		}

		protected override bool FindNext(CancellationToken ct, out int index, out TSource current)
		{
			//Console.WriteLine("FindNext called");

			index = -1;
			current = default(TSource);

			// we only returns a value if all are equal
			// if not, find the current max, and advance all iterators that are lower

			TKey min = default(TKey);
			TKey max = default(TKey);

			for (int i = 0; i < m_iterators.Length; i++)
			{
				if (!m_iterators[i].Active)
				{ // all must be still active!
					//Console.WriteLine("> STOP");
					return false;
				}

				//Console.WriteLine(">> " + i + ": " + m_iterators[i].Iterator.Current + " => " + m_iterators[i].Current);

				TKey key = m_iterators[i].Current;
				if (index == -1)
				{
					min = max = key;
					index = i;
				}
				else
				{
					if (m_keyComparer.Compare(key, min) < 0) { min = key; }
					if (m_keyComparer.Compare(key, max) > 0) { max = key; index = i; }
				}
			}

			//Console.WriteLine("> index=" + index + "; min=" + min + "; max=" + max);

			if (index == -1)
			{ // no values ?
				//Console.WriteLine("> None!");
				return false;
			}

			if (m_keyComparer.Compare(min, max) == 0)
			{ // all equal !
				// return the value of the first max encountered
				current = m_iterators[index].Iterator.Current;
				//Console.WriteLine("> All! #" + index + " = " + current);

				// advance everyone !
				for (int i = 0; i < m_iterators.Length;i++)
				{
					if (m_iterators[i].Active) AdvanceIterator(i, ct);
				}
				return true;
			}

			// advance all the values that are lower than the max
			//Console.WriteLine("> Different (max is " + index + " at " + max + ")");
			for (int i = 0; i < m_iterators.Length; i++)
			{
				if (m_iterators[i].Active && m_keyComparer.Compare(m_iterators[i].Current, max) < 0)
				{
					//Console.WriteLine("> advancing " + i);
					AdvanceIterator(i, ct);
				}
			}

			// keep searching
			index = -1;
			return true;
		}

		/// <summary>Apply a transformation on the results of the intersection</summary>
		public override FdbAsyncIterator<TNew> Select<TNew>(Func<TResult, TNew> selector)
		{
			return new FdbIntersectIterator<TSource, TKey, TNew>(
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

			return new FdbIntersectIterator<TSource, TKey, TResult>(
				m_sources,
				limit,
				m_keySelector,
				m_resultSelector,
				m_keyComparer
			);
		}

	}
}
