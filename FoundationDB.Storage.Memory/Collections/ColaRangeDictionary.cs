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

	/// <summary>Represent an ordered list of ranges, each associated with a specific value, stored in a Cache Oblivous Lookup Array</summary>
	/// <typeparam name="TKey">Type of the keys stored in the set</typeparam>
	/// <typeparam name="TValue">Type of the values associated with each range</typeparam>
	[DebuggerDisplay("Count={m_items.Count}, Bounds={m_bounds.Begin}..{m_bounds.End}")]
	public class ColaRangeDictionary<TKey, TValue> : IEnumerable<ColaRangeDictionary<TKey, TValue>.Entry>
	{
		// This class is equivalent to ColaRangeSet<TKey>, except that we have an extra value stored in each range.
		// That means that we only merge ranges with the same value, and split/truncate/overwrite ranges with different values

		// INVARIANTS
		// * If there is at least on range, the set is not empty (ie: Begin <= End)
		// * The Begin key is INCLUDED in range, but the End key is EXCLUDED from the range (ie: Begin <= K < End)
		// * The End key of a range MUST be GREATER THAN or EQUAL TO the Begin key of a range (ie: ranges are not backwards)
		// * The End key of a range CANNOT be GREATER THAN the Begin key of the next range (ie: ranges do not overlap)
		// * If the End key of a range is EQUAL TO the Begin key of the next range, then they MUST have a DIFFERENT value

		/// <summary>Mutable range</summary>
		public sealed class Entry
		{
			public TKey Begin { get; internal set; }
			public TKey End { get; internal set; }
			public TValue Value { get; internal set; }

			public Entry(TKey begin, TKey end, TValue value)
			{
				this.Begin = begin;
				this.End = end;
				this.Value = value;
			}

			internal void ReplaceWith(Entry other)
			{
				this.Begin = other.Begin;
				this.End = other.End;
				this.Value = other.Value;
			}

			internal void Update(TKey begin, TKey end)
			{
				this.Begin = begin;
				this.End = end;
			}

			internal void Update(TKey begin, TKey end, TValue value)
			{
				this.Begin = begin;
				this.End = end;
				this.Value = value;
			}

			public override string ToString()
			{
				return String.Format(CultureInfo.InvariantCulture, "({0} ~ {1}, {2})", this.Begin, this.End, this.Value);
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
				if (x == null) Debugger.Break();
				if (y == null) Debugger.Break();
				return m_comparer.Compare(x.Begin, y.Begin);
			}

		}

		private readonly ColaStore<Entry> m_items;
		private readonly IComparer<TKey> m_keyComparer;
		private readonly IComparer<TValue> m_valueComparer;
		private readonly Entry m_bounds;

		public ColaRangeDictionary()
			: this(0, null, null)
		{ }

		public ColaRangeDictionary(int capacity)
			: this(capacity, null, null)
		{ }

		public ColaRangeDictionary(IComparer<TKey> keyComparer, IComparer<TValue> valueComparer)
			: this(0, keyComparer, valueComparer)
		{ }

		public ColaRangeDictionary(int capacity, IComparer<TKey> keyComparer, IComparer<TValue> valueComparer)
		{
			m_keyComparer = keyComparer ?? Comparer<TKey>.Default;
			m_valueComparer = valueComparer ?? Comparer<TValue>.Default;
			if (capacity == 0) capacity = 15;
			m_items = new ColaStore<Entry>(capacity, new BeginKeyComparer(m_keyComparer));
			m_bounds = new Entry(default(TKey), default(TKey), default(TValue));
		}

		[Conditional("ENFORCE_INVARIANTS")]
		private void CheckInvariants()
		{
		}

		public int Count { get { return m_items.Count; } }

		public int Capacity { get { return m_items.Capacity; } }

		public IComparer<TKey> KeyComparer { get { return m_keyComparer; } }

		public IComparer<TValue> ValueComparer { get { return m_valueComparer; } }

		public Entry Bounds { get { return m_bounds; } }

		private bool Resolve(Entry previous, Entry cancidate, out Entry extra, bool reversed)
		{
			if (0 == m_valueComparer.Compare(previous.Value, cancidate.Value))
			{
				extra = null;
				return ResolveSameValue(previous, cancidate);
			}
			else
			{
				return ResolveDifferent(previous, cancidate, out extra, reversed);
			}
		}

		/// <summary>Attempts to resolve the insertion with an identical value</summary>
		/// <param name="previous">Previous entry that may be sharing a slot with the new entry</param>
		/// <param name="candidate">New entry</param>
		/// <returns>If true, the resolution was resolved and nothing needs to be inserted. If false, then the candidate must be inserted.</returns>
		private bool ResolveSameValue(Entry previous, Entry candidate)
		{

			int c = m_keyComparer.Compare(previous.Begin, candidate.Begin);
			if (c == 0)
			{ // they share the same begin key !

				if (m_keyComparer.Compare(previous.End, candidate.End) < 0)
				{ // candidate replaces the previous ony
					previous.ReplaceWith(candidate);
				}
				return true;
			}

			if (c < 0)
			{ // b is to the right
				if (m_keyComparer.Compare(previous.End, candidate.Begin) < 0)
				{ // there is a gap in between
					return false;
				}
				// they touch or overlap
				previous.Update(previous.Begin, Max(previous.End, candidate.End));
				return true;
			}
			else
			{ // b is to the left
				if (m_keyComparer.Compare(candidate.End, previous.Begin) < 0)
				{ // there is a gap in between
					return false;
				}
				// they touch or overlap
				previous.Update(candidate.Begin, Max(previous.End, candidate.End));
				return true;
			}
		}


		/// <summary>Attempts to resolve the insertion with a different value</summary>
		/// <param name="previous">Previous entry that may be sharing a slot with the new value</param>
		/// <param name="candidate">New range</param>
		/// <returns>If true, the resolution was resolved and nothing needs to be inserted. If false, then the candidate must be inserted.</returns>
		private bool ResolveDifferent(Entry previous, Entry candidate, out Entry extra, bool reversed)
		{
			extra = null;

			int c = m_keyComparer.Compare(previous.Begin, candidate.Begin);
			if (c == 0)
			{ // they share the same begin key !

				// 3 possibilites:

				// * [....PREV....)
				// * [....NEW.....)

				// * [....PREV......)
				// * [....NEW....)

				// * [....PREV....)
				// * [.......NEW.......)

				c = m_keyComparer.Compare(previous.End, candidate.End);
				if (c <= 0)
				{ // candidate replaces the previous ony
					previous.ReplaceWith(candidate);
					return true;
				}
				else
				{ // we need to split
					previous.Update(candidate.End, previous.End, previous.Value);
					//note: in this case, the new range will be inserted BEFORE the current one
					return false;
				}
			}

			if (c < 0)
			{ // candidate is to the right

				c = m_keyComparer.Compare(previous.End, candidate.Begin);
				if (c <= 0)
				{ // there are disjoint or contiguous

					// disjoint
					//   [--------)						
					// +             [========)
					// = [--------)  [========)

					// contiguouys
					//   [--------)						
					// +          [========)
					// = [--------|========)

					return false;
				}

				c = m_keyComparer.Compare(candidate.End, previous.End);

				if (c < 0)
				{ // contained

					//   [---------------)
					// +     [=====)
					// = [---|=====|-----)

					// split previous and insert new in between
					extra = new Entry(candidate.End, previous.End, previous.Value);
					previous.End = candidate.Begin;
					return false; // insert new
				}

				// they are overlapping

				// overlapping at the end
				//   [-----------)
				// +     [=======)
				// = [---|=======)

				// partially overlapping
				//   [--------)
				// +       [=========)
				// = [-----|=========)

				// truncate previous, insert new
				if (reversed)
					candidate.Begin = previous.End;
				else
					previous.End = candidate.Begin;
				return false;
			}
			else
			{ // b is to the left
				c = m_keyComparer.Compare(candidate.End, previous.Begin);
				if (c <= 0)
				{ // there are disjoint or contiguous

					// disjoint
					//               [--------)						
					// + [========)
					// = [========)  [--------)

					// contiguous
					//            [--------)						
					// + [========)
					// = [========|--------)

					return false;
				}

				// they are overlapping

				c = m_keyComparer.Compare(previous.End, candidate.End);
				if (c < 0)
				{ // completely overlapping

					// covering
					//     [----)
					// + [===========)
					// = [===========)

					// overwrite previous with new
					previous.ReplaceWith(candidate);
					return true;
				}

				// overlapping at the end
				//       [-----------)
				// + [=======)
				// = [=======|-------)

				// partially overlapping
				//         [--------)
				// + [=========)
				// = [=========|----)

				// truncate previous, insert new
				if (reversed)
					candidate.End = previous.Begin;
				else
					previous.Begin = candidate.End;
				return false;
			}
		}

		protected TKey Min(TKey a, TKey b)
		{
			return m_keyComparer.Compare(a, b) <= 0 ? a : b;
		}

		protected TKey Max(TKey a, TKey b)
		{
			return m_keyComparer.Compare(a, b) >= 0 ? a : b;
		}

		public void Clear()
		{
			m_items.Clear();
			m_bounds.Update(default(TKey), default(TKey), default(TValue));
		}

		public void Mark(TKey key, TValue value)
		{
			Mark(key, key, value);
		}

		public void Mark(TKey begin, TKey end, TValue value)
		{
			if (m_keyComparer.Compare(begin, end) > 0) throw new InvalidOperationException("End key should cannot be less than the Begin key.");

			// adds a new interval to the dictionary by overwriting or splitting any previous interval
			// * if there are no interval, or the interval is disjoint from all other intervals, it is inserted as-is
			// * if the new interval completly overwrites one or more intervals, they will be replaced by the new interval
			// * if the new interval partially overlaps with one or more intervals, they will be split into chunks, and the new interval will be inserted between them

			// Examples:
			// { } + [0..1,A] => { [0..1,A] }
			// { [0..1,A] } + [2..3,B] => { [0..1,A], [2..3,B] }
			// { [4..5,A] } + [0..10,B] => { [0..10,B] }
			// { [0..10,A] } + [4..5,B] => { [0..4,A], [4..5,B], [5..10,A] }
			// { [2..4,A], [6..8,B] } + [3..7,C] => { [2..3,A], [3..7,C], [7..8,B] }
			// { [1..2,A], [2..3,B], ..., [9..10,Y] } + [0..10,Z] => { [0..10,Z] }

			var entry = new Entry(begin, end, value);
			Entry cursor, extra = null;

			//Console.WriteLine("# Inserting " + entry);

			switch (m_items.Count)
			{
				case 0:
				{ // the list empty

					//Console.WriteLine("> empty: inserted");

					// no checks required
					m_items.Insert(entry);
					m_bounds.Update(entry.Begin, entry.End, default(TValue));
					break;
				}

				case 1:
				{ // there is only one value

					// * disjoint
					// *  - => insert
					// * contiguous:
					// *  - same value => merge
					// *  - else => insert
					// * completely contained:
					//    - same value => skip
					//    - => break + insert
					// * completely covering:
					//    - => overwrite
					// * partially overlapping
					//    - same value => merge
					//    - => truncate + insert

					cursor = m_items[0];

					if (!Resolve(cursor, entry, out extra, false))
					{ // insert
						m_items.Insert(entry);
						if (extra != null) m_items.Insert(extra);
						m_bounds.Update(
							Min(entry.Begin, cursor.Begin),
							Max(extra != null ? extra.End : entry.End, cursor.End),
							default(TValue)
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

					if (m_keyComparer.Compare(begin, m_bounds.End) > 0)
					{ // completely to the right
						m_items.Insert(entry);
						m_bounds.Update(m_bounds.Begin, end, default(TValue));
						break;
					}
					if (m_keyComparer.Compare(end, m_bounds.Begin) < 0)
					{ // completely to the left
						m_items.Insert(entry);
						m_bounds.Update(begin, m_bounds.End, default(TValue));
						break;
					}
					if (m_keyComparer.Compare(begin, m_bounds.Begin) <= 0 && m_keyComparer.Compare(end, m_bounds.End) >= 0)
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
						if (Resolve(cursor, entry, out extra, false))
						{
							entry = cursor;
							inserted = true;
						}
						else if (extra != null)
						{
							m_items.Insert(extra);
						}
					}

					// also check for potential conflicts with the next entries
					while (extra == null)
					{
						level = m_items.FindNext(entry, false, out offset, out cursor);
						if (level < 0) break;

						//Console.WriteLine("> Next: " + cursor);

						if (inserted)
						{ // we already have inserted the key so conflicts will remove the next segment
							if (Resolve(entry, cursor, out extra, true))
							{ // next segment has been removed
								m_items.RemoveAt(level, offset);
							}
							else
							{
								break;
							}
						}
						else
						{ // we havent inserted the key yet, so in case of conflict, we will use the next segment's slot
							if (Resolve(cursor, entry, out extra, true))
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
						Max(m_bounds.End, extra != null ? extra.End : entry.End),
						default(TValue)
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
			Console.WriteLine("Dumping ColaRangeDictionary<" + typeof(TKey).Name + "> filled at " + (100.0d * this.Count / this.Capacity).ToString("N2") + "%");
			m_items.Debug_Dump();
		}

		public override string ToString()
		{
			if (m_items.Count == 0) return "{ }";

			var sb = new System.Text.StringBuilder();
			Entry previous = null;
			foreach(var item in this)
			{
				if (previous == null)
				{
					sb.Append('[');
				}
				else if (m_keyComparer.Compare(previous.End, item.Begin) < 0)
				{
					sb.Append(previous.End).Append(") [");
				}
				else
				{
					sb/*.Append(previous.End)*/.Append('|');
				}

				sb.Append(item.Begin).Append("..(").Append(item.Value).Append(")..");
				previous = item;
			}
			if (previous != null)
			{
				sb.Append(previous.End).Append(")");
			}

			return sb.ToString();
			//return "{ " + String.Join("; ", this) + " }";
		}

	}

}
