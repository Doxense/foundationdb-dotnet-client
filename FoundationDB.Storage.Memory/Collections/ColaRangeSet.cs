#region Copyright (c) 2013-2014, Doxense SAS. All rights reserved.
// See License.MD for license information
#endregion

// enables consitency checks after each operation to the set
#define ENFORCE_INVARIANTS

namespace FoundationDB.Storage.Memory.Core
{
	using System;
	using System.Collections.Generic;
	using System.Diagnostics;
	using System.Diagnostics.Contracts;
	using System.Globalization;

	/// <summary>Represent an ordered list of ranges, stored in a Cache Oblivous Lookup Array</summary>
	/// <typeparam name="TKey">Type of keys stored in the set</typeparam>
	[DebuggerDisplay("Count={m_items.Count}, Bounds={m_bounds.Begin}..{m_bounds.End}")]
	public class ColaRangeSet<TKey> : IEnumerable<ColaRangeSet<TKey>.Entry>
	{
		// We store the ranges in a COLA array that is sorted by the Begin keys
		// The range are mutable, which allows for efficient merging
		
		// INVARIANTS
		// * If there is at least on range, the set is not empty
		// * The Begin key is INCLUDED in range, but the End key is EXCLUDED from the range (ie: Begin <= K < End)
		// * The End key of a range is always GREATER than or EQUAL to the Begin key of a range (ie: ranges are not backwards)
		// * The End key of a range is always strictly LESS than the Begin key of the next range (ie: there are gaps between ranges)

		// This should give us a sorted set of disjoint ranges

		/// <summary>Mutable range</summary>
		public sealed class Entry
		{
			public TKey Begin { get; internal set; }
			public TKey End { get; internal set; }

			public Entry(TKey begin, TKey end)
			{
				this.Begin = begin;
				this.End = end;
			}

			internal void ReplaceWith(Entry other)
			{
				this.Begin = other.Begin;
				this.End = other.End;
			}

			internal void Update(TKey begin, TKey end)
			{
				this.Begin = begin;
				this.End = end;
			}

			public override string ToString()
			{
				return String.Format(CultureInfo.InvariantCulture, "[{0}, {1})", this.Begin, this.End);
			}
		}

		/// <summary>Range comparer that only test the Begin key</summary>
		private sealed class BeginKeyComparer : IComparer<Entry>
		{
			private readonly IComparer<TKey> m_comparer;
			
			public BeginKeyComparer(IComparer<TKey> comparer)
			{
				m_comparer = comparer;
			}

			public int Compare(Entry x, Entry y)
			{
				return m_comparer.Compare(x.Begin, y.Begin);
			}

		}

		private readonly ColaStore<Entry> m_items;
		private readonly IComparer<TKey> m_comparer;
		private readonly Entry m_bounds;

		public ColaRangeSet()
			: this(0, null)
		{ }

		public ColaRangeSet(int capacity)
			: this(capacity, null)
		{ }

		public ColaRangeSet(IComparer<TKey> keyComparer)
			: this(0, keyComparer)
		{ }

		public ColaRangeSet(int capacity, IComparer<TKey> keyComparer)
		{
			m_comparer = keyComparer ?? Comparer<TKey>.Default;
			if (capacity == 0) capacity = 15;
			m_items = new ColaStore<Entry>(capacity, new BeginKeyComparer(m_comparer));
			m_bounds = new Entry(default(TKey), default(TKey));
		}

		[Conditional("ENFORCE_INVARIANTS")]
		private void CheckInvariants()
		{
		}

		public int Count { get { return m_items.Count; } }

		public int Capacity { get { return m_items.Capacity; } }

		public IComparer<TKey> Comparer { get { return m_comparer; } }

		public Entry Bounds { get { return m_bounds; } }

		protected virtual bool Resolve(Entry previous, Entry candidate)
		{

			int c = m_comparer.Compare(previous.Begin, candidate.Begin);
			if (c == 0)
			{ // they share the same begin key !

				if (m_comparer.Compare(previous.End, candidate.End) < 0)
				{ // candidate replaces the previous ony
					previous.ReplaceWith(candidate);
				}
				return true;
			}

			if (c < 0)
			{ // b is to the right
				if (m_comparer.Compare(previous.End, candidate.Begin) < 0)
				{ // there is a gap in between
					return false;
				}
				// they touch or overlap
				previous.Update(previous.Begin, Max(previous.End, candidate.End));
				return true;
			}
			else
			{ // b is to the left
				if (m_comparer.Compare(candidate.End, previous.Begin) < 0)
				{ // there is a gap in between
					return false;
				}
				// they touch or overlap
				previous.Update(candidate.Begin, Max(previous.End, candidate.End));
				return true;
			}
		}

		protected TKey Min(TKey a, TKey b)
		{
			return m_comparer.Compare(a, b) <= 0 ? a : b;
		}

		protected TKey Max(TKey a, TKey b)
		{
			return m_comparer.Compare(a, b) >= 0 ? a : b;
		}

		public void Clear()
		{
			m_items.Clear();
			m_bounds.Update(default(TKey), default(TKey));
		}

		public void Mark(TKey key)
		{
			Mark(key, key);
		}

		public void Mark(TKey begin, TKey end)
		{
			if (m_comparer.Compare(begin, end) > 0) throw new InvalidOperationException("End key should cannot be less than the Begin key.");

			var entry = new Entry(begin, end);
			Entry cursor;

			//Console.WriteLine("# Inserting " + entry);

			switch (m_items.Count)
			{
				case 0:
				{ // the list empty

					//Console.WriteLine("> empty: inserted");

					// no checks required
					m_items.Insert(entry);
					m_bounds.ReplaceWith(entry);
					break;
				}

				case 1:
				{ // there is only one value

					cursor = m_items[0];
					if (!Resolve(cursor, entry))
					{ // no conflict
						m_items.Insert(entry);
						m_bounds.Update(
							Min(entry.Begin, cursor.Begin),
							Max(entry.End, cursor.End)
						);
					}
					else
					{ // merged with the previous range
						m_bounds.ReplaceWith(cursor);
					}
					break;
				}
				default:
				{
					// check with the bounds first

					if (m_comparer.Compare(begin, m_bounds.End) > 0)
					{ // completely to the right
						m_items.Insert(entry);
						m_bounds.Update(m_bounds.Begin, end);
						break;
					}
					if (m_comparer.Compare(end, m_bounds.Begin) < 0)
					{ // completely to the left
						m_items.Insert(entry);
						m_bounds.Update(begin, m_bounds.End);
						break;
					}
					if (m_comparer.Compare(begin, m_bounds.Begin) <= 0 && m_comparer.Compare(end, m_bounds.End) >= 0)
					{ // overlaps with all the ranges
						// note :if we are here, there was at least 2 items, so just clear everything
						m_items.Clear();
						m_items.Insert(entry);
						m_bounds.ReplaceWith(entry);
						break;
					}


					// overlaps with existing ranges, we may need to resolve conflicts
					int offset, level;
					bool inserted = false;

					// once inserted, will it conflict with the previous entry ?
					if ((level = m_items.FindPrevious(entry, true, out offset, out cursor)) >= 0)
					{
						if (Resolve(cursor, entry))
						{
							entry = cursor;
							inserted = true;
						}
					}

					// also check for potential conflicts with the next entries
					while (true)
					{
						level = m_items.FindNext(entry, false, out offset, out cursor);
						if (level < 0) break;

						//Console.WriteLine("> Next: " + cursor);

						if (inserted)
						{ // we already have inserted the key so conflicts will remove the next segment
							if (Resolve(entry, cursor))
							{ // next segment has been removed
								//Console.WriteLine("  > folded with previous: " + entry);
								m_items.RemoveAt(level, offset);
							}
							else
							{
								break;
							}
						}
						else
						{ // we havent inserted the key yet, so in case of conflict, we will use the next segment's slot
							if (Resolve(cursor, entry))
							{
								//Console.WriteLine("  > merged in place: " + cursor);
								inserted = true;
							}
							else
							{
								break;
							}
						}
					}

					if (!inserted)
					{ // no conflict, we have to insert the new range
						//Console.WriteLine("> inserted: " + entry);
						m_items.Insert(entry);
					}

					m_bounds.Update(
						Min(m_bounds.Begin, entry.Begin),
						Max(m_bounds.End, entry.End)
					);

					break;
				}
			}

			//TODO: check constraints !
		}

		public ColaStore.Enumerator<Entry> GetEnumerator()
		{
			return new ColaStore.Enumerator<Entry>(m_items, reverse: false);
		}

		IEnumerator<Entry> IEnumerable<Entry>.GetEnumerator()
		{
			return this.GetEnumerator();
		}

		System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
		{
			return this.GetEnumerator();
		}

		[Conditional("DEBUG")]
		//TODO: remove or set to internal !
		public void Debug_Dump()
		{
			Console.WriteLine("Dumping ColaRangeSet<" + typeof(TKey).Name + "> filled at " + (100.0d * this.Count / this.Capacity).ToString("N2") + "%");
			m_items.Debug_Dump();
		}

		public override string ToString()
		{
			if (m_items.Count == 0) return "{ }";
			return "{ " + String.Join(", ", this) + " }";
		}

	}

}
