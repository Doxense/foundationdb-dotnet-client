#region BSD Licence
/* Copyright (c) 2013-2015, Doxense SAS
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
	using FoundationDB.Async;
	using JetBrains.Annotations;

	public static partial class FdbAsyncEnumerable
	{

		/// <summary>Represent an async sequence that returns its elements according to a specific sort order</summary>
		/// <typeparam name="TSource">Type of the elements of the sequence</typeparam>
		internal class OrderedSequence<TSource> : IFdbAsyncOrderedEnumerable<TSource>
		{
			// If an instance of the base <TSource> class is constructed, it will sort by the items themselves (using a Comparer<TSource>)
			// If an instance of the derived <TSource, TKey> class is constructed, then it will sort the a key extracted the each item (sing a Comparer<TKey>)

			protected readonly IFdbAsyncEnumerable<TSource> m_source;
			private readonly IComparer<TSource> m_comparer; // null if comparing using keys
			protected readonly bool m_descending;
			protected readonly OrderedSequence<TSource> m_parent;// null if primary sort key

			public OrderedSequence(IFdbAsyncEnumerable<TSource> source, IComparer<TSource> comparer, bool descending, OrderedSequence<TSource> parent)
			{
				Contract.Requires(source != null);

				m_source = source;
				m_descending = descending;
				m_comparer = comparer ?? Comparer<TSource>.Default;
				m_parent = parent;
			}

			protected OrderedSequence(IFdbAsyncEnumerable<TSource> source, bool descending, OrderedSequence<TSource> parent)
			{
				Contract.Requires(source != null);

				m_source = source;
				m_descending = descending;
				m_parent = parent;
			}

			[NotNull]
			internal virtual SequenceSorter<TSource> GetEnumerableSorter(SequenceSorter<TSource> next)
			{
				var sorter = new SequenceByElementSorter<TSource>(m_comparer, m_descending, next);
				if (m_parent == null) return sorter;
				return m_parent.GetEnumerableSorter(sorter);
			}

			public IFdbAsyncEnumerator<TSource> GetEnumerator(FdbAsyncMode mode = FdbAsyncMode.Default)
			{
				var sorter = GetEnumerableSorter(null);
				var enumerator = default(IFdbAsyncEnumerator<TSource>);
				try
				{
					enumerator = m_source.GetEnumerator(mode);
					return new OrderedEnumerator<TSource>(enumerator, sorter);
				}
				catch (Exception)
				{
					if (enumerator != null) enumerator.Dispose();
					throw;
				}
			}

			IAsyncEnumerator<TSource> IAsyncEnumerable<TSource>.GetEnumerator()
			{
				return GetEnumerator(FdbAsyncMode.All);
			}

			[NotNull]
			public IFdbAsyncOrderedEnumerable<TSource> CreateOrderedEnumerable<TKey>([NotNull] Func<TSource, TKey> keySelector, IComparer<TKey> comparer, bool descending)
			{
				if (keySelector == null) throw new ArgumentNullException("keySelector");

				return new OrderedSequence<TSource, TKey>(this, keySelector, comparer, descending, this);
			}

		}

		/// <summary>Represent an async sequence that returns its elements according to a specific sort order</summary>
		/// <typeparam name="TSource">Type of the elements of the sequence</typeparam>
		/// <typeparam name="TKey">Type of the keys used to sort the elements</typeparam>
		internal sealed class OrderedSequence<TSource, TKey> : OrderedSequence<TSource>
		{
			private readonly Func<TSource, TKey> m_keySelector;
			private readonly IComparer<TKey> m_keyComparer;

			public OrderedSequence(IFdbAsyncEnumerable<TSource> source, Func<TSource, TKey> keySelector, IComparer<TKey> comparer, bool descending, OrderedSequence<TSource> parent)
				: base(source, descending, parent)
			{
				Contract.Requires(keySelector != null);
				m_keySelector = keySelector;
				m_keyComparer = comparer ?? Comparer<TKey>.Default;
			}

			internal override SequenceSorter<TSource> GetEnumerableSorter(SequenceSorter<TSource> next)
			{
				var sorter = new SequenceByKeySorter<TSource, TKey>(m_keySelector, m_keyComparer, m_descending, next);
				if (m_parent == null) return sorter;
				return m_parent.GetEnumerableSorter(sorter);
			}

		}

		/// <summary>Iterator that will sort all the items produced by an inner iterator, before outputting the results all at once</summary>
		internal sealed class OrderedEnumerator<TSource> : IFdbAsyncEnumerator<TSource>
		{
			// This iterator must first before EVERY items of the source in memory, before being able to sort them.
			// The first MoveNext() will return only once the inner sequence has finished (succesfully), which can take some time!
			// Ordering is done in-memory using QuickSort

			private readonly IFdbAsyncEnumerator<TSource> m_inner;
			private readonly SequenceSorter<TSource> m_sorter;

			private TSource[] m_items;
			private int[] m_map;
			private int m_offset;
			private TSource m_current;

			public OrderedEnumerator(IFdbAsyncEnumerator<TSource> enumerator, SequenceSorter<TSource> sorter)
			{
				Contract.Requires(enumerator != null && sorter != null);
				m_inner = enumerator;
				m_sorter = sorter;
			}

			private async Task<bool> ReadAllThenSort(CancellationToken ct)
			{
				if (m_offset == -1) return false; // already EOF or Disposed

				// first we need to spool everything from the inner iterator into memory
				var buffer = new FdbAsyncEnumerable.Buffer<TSource>();

				var inner = m_inner;
				var iterator = inner as FdbAsyncIterator<TSource>;
				if (iterator != null)
				{
					await iterator.ExecuteAsync((x) => buffer.Add(x), ct).ConfigureAwait(false);
				}
				else
				{
					while (await inner.MoveNextAsync(ct).ConfigureAwait(false))
					{
						buffer.Add(inner.Current);
					}
				}

				if (buffer.Count == 0)
				{ // the list was empty!
					Completed();
					return false;
				}

				// then we need to sort everything...
				m_items = buffer.GetBuffer();
				m_map = m_sorter.Sort(m_items, buffer.Count);

				// and only then we can start outputing the first value
				// (after that, all MoveNext operations will be non-async
				Publish(0);
				return true;
			}

			public Task<bool> MoveNextAsync(CancellationToken cancellationToken)
			{
				// Firt call will be slow (and async), but the rest of the calls will use the results already sorted in memory, and should be as fast as possible!

				if (m_map == null)
				{
					return ReadAllThenSort(cancellationToken);
				}

				int pos = checked(m_offset + 1);
				if (pos < m_map.Length)
				{
					Publish(pos);
					return TaskHelpers.TrueTask;
				}
				else
				{
					Completed();
					return TaskHelpers.FalseTask;
				}
			}

			private void Publish(int offset)
			{
				Contract.Requires(m_items != null && m_map != null && offset >= 0 && offset < m_map.Length);
				m_current = m_items[m_map[offset]];
				m_offset = offset;
			}

			private void Completed()
			{
				m_current = default(TSource);
				m_offset = -1;
				m_items = null;
				m_map = null;

			}

			public TSource Current
			{
				get { return m_current; }
			}

			public void Dispose()
			{
				Completed();
			}
		}

	}

}
