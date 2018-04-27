#region Copyright Doxense SAS 2013-2016
//
// All rights are reserved. Reproduction or transmission in whole or in part, in
// any form or by any means, electronic, mechanical or otherwise, is prohibited
// without the prior written consent of the copyright owner.
//
#endregion

using Doxense.Async;

namespace Doxense.Linq
{
	using System;
	using System.Collections.Generic;
	using System.Threading;
	using System.Threading.Tasks;
	using Doxense.Diagnostics.Contracts;
	using Doxense.Linq.Async.Iterators;
	using JetBrains.Annotations;

	public static partial class AsyncEnumerable
	{

		/// <summary>Represent an async sequence that returns its elements according to a specific sort order</summary>
		/// <typeparam name="TSource">Type of the elements of the sequence</typeparam>
		internal class OrderedSequence<TSource> : IAsyncOrderedEnumerable<TSource>
		{
			// If an instance of the base <TSource> class is constructed, it will sort by the items themselves (using a Comparer<TSource>)
			// If an instance of the derived <TSource, TKey> class is constructed, then it will sort the a key extracted the each item (sing a Comparer<TKey>)

			protected readonly IAsyncEnumerable<TSource> m_source;
			private readonly IComparer<TSource> m_comparer; // null if comparing using keys
			protected readonly bool m_descending;
			protected readonly OrderedSequence<TSource> m_parent;// null if primary sort key

			public OrderedSequence(IAsyncEnumerable<TSource> source, IComparer<TSource> comparer, bool descending, OrderedSequence<TSource> parent)
			{
				Contract.Requires(source != null);

				m_source = source;
				m_descending = descending;
				m_comparer = comparer ?? Comparer<TSource>.Default;
				m_parent = parent;
			}

			protected OrderedSequence(IAsyncEnumerable<TSource> source, bool descending, OrderedSequence<TSource> parent)
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

			public IAsyncEnumerator<TSource> GetEnumerator(CancellationToken ct, AsyncIterationHint mode)
			{
				ct.ThrowIfCancellationRequested();
				var sorter = GetEnumerableSorter(null);
				var enumerator = default(IAsyncEnumerator<TSource>);
				try
				{
					enumerator = m_source.GetEnumerator(ct, mode);
					return new OrderedEnumerator<TSource>(enumerator, sorter, ct);
				}
				catch (Exception)
				{
					enumerator?.Dispose();
					throw;
				}
			}

			public IAsyncOrderedEnumerable<TSource> CreateOrderedEnumerable<TKey>(Func<TSource, TKey> keySelector, IComparer<TKey> comparer, bool descending)
			{
				Contract.NotNull(keySelector, nameof(keySelector));

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

			public OrderedSequence(IAsyncEnumerable<TSource> source, Func<TSource, TKey> keySelector, IComparer<TKey> comparer, bool descending, OrderedSequence<TSource> parent)
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
		internal sealed class OrderedEnumerator<TSource> : IAsyncEnumerator<TSource>
		{
			// This iterator must first before EVERY items of the source in memory, before being able to sort them.
			// The first MoveNext() will return only once the inner sequence has finished (succesfully), which can take some time!
			// Ordering is done in-memory using QuickSort

			private readonly IAsyncEnumerator<TSource> m_inner;
			private readonly SequenceSorter<TSource> m_sorter;

			private TSource[] m_items;
			private int[] m_map;
			private int m_offset;
			private TSource m_current;
			private readonly CancellationToken m_ct;

			public OrderedEnumerator(IAsyncEnumerator<TSource> enumerator, SequenceSorter<TSource> sorter, CancellationToken ct)
			{
				Contract.Requires(enumerator != null && sorter != null);
				m_inner = enumerator;
				m_sorter = sorter;
				m_ct = ct;
			}

			private async Task<bool> ReadAllThenSort()
			{
				if (m_offset == -1) return false; // already EOF or Disposed

				// first we need to spool everything from the inner iterator into memory
				var buffer = new Buffer<TSource>();

				var inner = m_inner;
				if (inner is AsyncIterator<TSource> iterator)
				{
					await iterator.ExecuteAsync((x) => buffer.Add(x), m_ct).ConfigureAwait(false);
				}
				else
				{
					while (await inner.MoveNextAsync().ConfigureAwait(false))
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

			public Task<bool> MoveNextAsync()
			{
				// Firt call will be slow (and async), but the rest of the calls will use the results already sorted in memory, and should be as fast as possible!

				if (m_map == null)
				{
					return ReadAllThenSort();
				}

				int pos = checked(m_offset + 1);
				if (pos < m_map.Length)
				{
					Publish(pos);
					return TaskHelpers.True;
				}
				else
				{
					Completed();
					return TaskHelpers.False;
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

			public TSource Current => m_current;

			public void Dispose()
			{
				Completed();
			}
		}

	}

}
