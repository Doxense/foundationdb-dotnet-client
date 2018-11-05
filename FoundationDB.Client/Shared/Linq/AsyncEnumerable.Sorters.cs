#region BSD License
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

#if !USE_SHARED_FRAMEWORK

namespace Doxense.Linq
{
	using System;
	using System.Collections.Generic;
	using Doxense.Diagnostics.Contracts;
	using JetBrains.Annotations;

	public static partial class AsyncEnumerable
	{
		// These classes contain the logic to sort items (by themselves or by keys)
		// They are single-use and constructed at runtime, when an ordered sequence starts enumerating.
		// In case of multiple sort keys, each sorter is linked to the next (from primary key, to the last key)

		/// <summary>Helper class for sorting a sequence of <typeparamref name="TSource"/></summary>
		/// <typeparam name="TSource">Type of the sorted elements</typeparam>
		internal abstract class SequenceSorter<TSource>
		{
			/// <summary>Fill the array with all the keys extracted from the source</summary>
			/// <param name="items"></param>
			/// <param name="count"></param>
			internal abstract void ComputeKeys([NotNull] TSource[] items, int count);

			internal abstract int CompareKeys(int index1, int index2);

			[NotNull]
			internal int[] Sort([NotNull] TSource[] items, int count)
			{
				ComputeKeys(items, count);
				var map = new int[count];
				for (int i = 0; i < map.Length; i++) map[i] = i;
				QuickSort(map, 0, count - 1);
				return map;
			}

			private void QuickSort([NotNull] int[] map, int left, int right)
			{
				do
				{
					int i = left;
					int j = right;
					int x = map[i + ((j - i) >> 1)];
					do
					{
						while (i < map.Length && CompareKeys(x, map[i]) > 0)
						{
							i++;
						}
						while (j >= 0 && CompareKeys(x, map[j]) < 0)
						{
							j--;
						}
						if (i > j) break;
						if (i < j)
						{
							int temp = map[i];
							map[i] = map[j];
							map[j] = temp;
						}
						i++;
						j--;
					} while (i <= j);

					if (j - left <= right - i)
					{
						if (left < j) QuickSort(map, left, j);
						left = i;
					}
					else
					{
						if (i < right) QuickSort(map, i, right);
						right = j;
					}
				} while (left < right);
			}
		}

		/// <summary>Helper class for sorting a sequence of <typeparamref name="TSource"/></summary>
		/// <typeparam name="TSource">Type of the sorted elements</typeparam>
		internal sealed class SequenceByElementSorter<TSource> : SequenceSorter<TSource>
		{
			private readonly IComparer<TSource> m_comparer;
			private readonly bool m_descending;

			private readonly SequenceSorter<TSource> m_next;
			private TSource[] m_items;

			public SequenceByElementSorter(IComparer<TSource> comparer, bool descending, SequenceSorter<TSource> next)
			{
				Contract.Requires(comparer != null);

				m_comparer = comparer;
				m_descending = descending;
				m_next = next;
			}

			internal override void ComputeKeys(TSource[] items, int count)
			{
				m_items = items;
			}

			internal override int CompareKeys(int index1, int index2)
			{
				var items = m_items;
				int c = m_comparer.Compare(items[index1], items[index2]);
				if (c == 0)
				{
					SequenceSorter<TSource> next;
					return (next = m_next) == null ? (index1 - index2) : next.CompareKeys(index1, index2);
				}
				return !m_descending ? c : -c;
			}

		}

		/// <summary>Helper class for sorting a sequence of <typeparamref name="TSource"/> using a key of <typeparamref name="TKey"/></summary>
		/// <typeparam name="TSource">Type of the sorted elements</typeparam>
		/// <typeparam name="TKey">Type of the keys used to sort the elements</typeparam>
		internal sealed class SequenceByKeySorter<TSource, TKey> : SequenceSorter<TSource>
		{
			private readonly Func<TSource, TKey> m_keySelector;
			private readonly IComparer<TKey> m_comparer;
			private readonly bool m_descending;

			private readonly SequenceSorter<TSource> m_next;
			private TKey[] m_keys;

			public SequenceByKeySorter(Func<TSource, TKey> keySelector, IComparer<TKey> comparer, bool descending, SequenceSorter<TSource> next)
			{
				Contract.Requires(keySelector != null && comparer != null);

				m_keySelector = keySelector;
				m_comparer = comparer;
				m_descending = descending;
				m_next = next;
			}

			internal override void ComputeKeys(TSource[] items, int count)
			{
				var selector = m_keySelector;
				var keys = new TKey[count];
				for (int i = 0; i < keys.Length; i++)
				{
					keys[i] = selector(items[i]);
				}
				m_keys = keys;
				m_next?.ComputeKeys(items, count);
			}

			internal override int CompareKeys(int index1, int index2)
			{
				Contract.Requires(m_keys != null);
				Contract.Requires(m_comparer != null);
				var keys = m_keys;
				int c = m_comparer.Compare(keys[index1], keys[index2]);
				if (c == 0)
				{
					SequenceSorter<TSource> next;
					return (next = m_next) == null ? (index1 - index2) : next.CompareKeys(index1, index2);
				}
				return !m_descending ? c : -c;
			}

		}

	}
}

#endif
