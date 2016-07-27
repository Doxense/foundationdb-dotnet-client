#region Copyright (c) 2013-2014, Doxense SAS. All rights reserved.
// See License.MD for license information
#endregion

// enables consitency checks after each operation to the set
//#define ENFORCE_INVARIANTS

using FoundationDB.Layers.Tuples;

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
	public sealed class ColaRangeDictionary<TKey, TValue> : IEnumerable<ColaRangeDictionary<TKey, TValue>.Entry>
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

			/// <summary>Overwrite this range with another one</summary>
			/// <param name="other">New range that will overwrite the current instance</param>
			internal void Set(Entry other)
			{
				this.Begin = other.Begin;
				this.End = other.End;
				this.Value = other.Value;
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
#if DEBUG
				if (x == null || y == null) Debugger.Break();
#endif
				return m_comparer.Compare(x.Begin, y.Begin);
			}
		}

		private sealed class EndKeyComparer : IComparer<Entry>
		{
			private readonly IComparer<TKey> m_comparer;

			public EndKeyComparer(IComparer<TKey> comparer)
			{
				m_comparer = comparer;
			}

			public int Compare(Entry x, Entry y)
			{
#if DEBUG
				if (x == null || y == null) Debugger.Break();
#endif
				return m_comparer.Compare(x.End, y.End);
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
#if ENFORCE_INVARIANTS
			Contract.Assert(m_bounds != null);
			Debug.WriteLine("INVARIANTS:" + this.ToString() + " <> " + m_bounds.ToString());

			if (m_items.Count == 0)
			{
				Contract.Assert(EqualityComparer<TKey>.Default.Equals(m_bounds.Begin, default(TKey)));
				Contract.Assert(EqualityComparer<TKey>.Default.Equals(m_bounds.End, default(TKey)));
			}
			else if (m_items.Count == 1)
			{
				Contract.Assert(EqualityComparer<TKey>.Default.Equals(m_bounds.Begin, m_items[0].Begin));
				Contract.Assert(EqualityComparer<TKey>.Default.Equals(m_bounds.End, m_items[0].End));
			}
			else
			{
				Entry previous = null;
				Entry first = null;
				foreach (var item in this)
				{
					Contract.Assert(m_keyComparer.Compare(item.Begin, item.End) < 0, "End key should be after begin");

					if (previous == null)
					{
						first = item;
					}
					else
					{
						int c = m_keyComparer.Compare(previous.End, item.Begin);
						if (c > 0) Contract.Assert(false, String.Format("Range overlapping: {0} and {1}", previous, item));
						if (c == 0 && m_valueComparer.Compare(previous.Value, item.Value) == 0) Contract.Assert(false, String.Format("Unmerged ranges: {0} and {1}", previous, item));
					}
					previous = item;
				}
				Contract.Assert(EqualityComparer<TKey>.Default.Equals(m_bounds.Begin, first.Begin), String.Format("Min bound {0} does not match with {1}", m_bounds.Begin, first.Begin));
				Contract.Assert(EqualityComparer<TKey>.Default.Equals(m_bounds.End, previous.End), String.Format("Max bound {0} does not match with {1}", m_bounds.End, previous.End));
			}
#endif
		}

		public int Count { get { return m_items.Count; } }

		public int Capacity { get { return m_items.Capacity; } }

		public IComparer<TKey> KeyComparer { get { return m_keyComparer; } }

		public IComparer<TValue> ValueComparer { get { return m_valueComparer; } }

		public Entry Bounds { get { return m_bounds; } }

		private Entry GetBeginRangeIntersecting(Entry range)
		{
			// look for the first existing range that is intersected by the start of the new range

			Entry cursor;
			int offset, level = m_items.FindPrevious(range, true, out offset, out cursor);
			if (level < 0)
			{
				return null;
			}
			return cursor;
		}

		private Entry GetEndRangeIntersecting(Entry range)
		{
			// look for the last existing range that is intersected by the end of the new range

			Entry cursor;
			int offset, level = m_items.FindPrevious(range, true, out offset, out cursor);
			if (level < 0)
			{
				return null;
			}
			return cursor;
		}

		private TKey Min(TKey a, TKey b)
		{
			return m_keyComparer.Compare(a, b) <= 0 ? a : b;
		}

		private TKey Max(TKey a, TKey b)
		{
			return m_keyComparer.Compare(a, b) >= 0 ? a : b;
		}

		public void Clear()
		{
			m_items.Clear();
			m_bounds.Begin = default(TKey);
			m_bounds.End = default(TKey);

			CheckInvariants();
		}

		/// <summary>
		/// Removes everything between begin and end then translates everything
		/// </summary>
		/// <param name="begin">begin key</param>
		/// <param name="end">end key</param>
		/// <param name="offset">offset to apply</param>
		/// <param name="applyOffset">func to apply offset to a key</param>
		public void Remove(TKey begin, TKey end, TKey offset, Func<TKey, TKey, TKey> applyOffset)
		{
			if (m_keyComparer.Compare(begin, end) >= 0) throw new InvalidOperationException("End key must be greater than the Begin key.");

			try
			{
				var entry = new Entry(begin, end, default(TValue));
				var iterator = m_items.GetIterator();
				var comparer = m_keyComparer;
				if (!iterator.Seek(entry, true))
				{
					//on ne trouve pas l'item exacte, on prends le premier.
					iterator.SeekFirst();
				}
				var cursor = iterator.Current;
				var c1 = comparer.Compare(begin, cursor.Begin);
				var c2 = comparer.Compare(end, cursor.End);
				List<Entry> toRemove = null;
				//begin < cursor.Begin
				if (c1 < 0)
				{
					var c3 = comparer.Compare(end, cursor.Begin);
					//end <= cursor.Begin
					//         [++++++++++
					// ------[
					//ou
					//       [++++++
					// ------[
					if (c3 <= 0)
					{
						TranslateAfter(null, offset, applyOffset);
						return;
					}
					//end < cursor.End
					//     [+++++++++++[
					//-----------[
					if (c2 < 0)
					{
						cursor.Begin = applyOffset(end, offset);
						cursor.End = applyOffset(cursor.End, offset);
						TranslateAfter(cursor, offset, applyOffset);
						return;
					}
					//end == cursor.End
					//     [+++++++++[
					//---------------[
					if (c2 == 0)
					{
						m_items.RemoveItem(cursor);
						TranslateAfter(cursor, offset, applyOffset);
						return;
					}
					//end > cursor.End
					//      [+++++++++[
					//-------------------...
					if (c2 > 0)
					{
						toRemove = new List<Entry>();
						toRemove.Add(cursor);
						while (iterator.Next())
						{
							cursor = iterator.Current;
							c2 = comparer.Compare(end, cursor.End);
							c3 = comparer.Compare(end, cursor.Begin);
							//end <= cursor.Begin
							//       [+++++
							// ----[
							//ou
							//       [+++++
							// ------[
							if (c3 <= 0)
							{
								//on set cursor pour que la translation soit faite correctement
								cursor = entry;
								break;
							}
							//end > cursor.Begin
							if (c3 > 0)
							{
								//end < cursor.End
								//     [+++++++++++
								// ----------[
								if (c2 < 0)
								{
									cursor.Begin = begin;
									cursor.End = applyOffset(cursor.End, offset);
									break;
								}
								// end >= cursor.End
								//      [+++++++++[
								// ---------------[
								//ou
								//      [+++++++[
								// ----------------...
								if (c2 >= 0)
								{
									toRemove.Add(cursor);
									if (c2 == 0) break;
								}
							}
						}
						m_items.RemoveItems(toRemove);
						TranslateAfter(cursor, offset, applyOffset);
						return;
					}
				}
				//begin == cursor.Begin
				else if (c1 == 0)
				{
					//end < cursor.End
					// [+++++++++[
					// [-----[
					if (c2 < 0)
					{
						cursor.Begin = begin;
						cursor.End = applyOffset(cursor.End, offset);
						TranslateAfter(cursor, offset, applyOffset);
						return;
					}
					//end == cursor.End
					// [++++++++[
					// [--------[
					else if (c2 == 0)
					{
						toRemove = new List<Entry>();
						toRemove.Add(cursor);
					}
					// end > cursor.End
					// [+++++++[
					// [-----------....
					else
					{
						toRemove = new List<Entry>();
						toRemove.Add(cursor);
						while (iterator.Next())
						{
							cursor = iterator.Current;
							var c3 = comparer.Compare(end, cursor.Begin);
							c2 = comparer.Compare(end, cursor.End);
							//end < cursor.Begin
							//                [++++++++[
							//---------[
							//ou
							//         [+++++++[
							//---------[
							if (c3 <= 0)
							{
								break;
							}
							else
							{
								//end < cursor.End
								// [++++++++++++[
								//-----[
								if (c2 < 0)
								{
									cursor.Begin = begin;
									cursor.End = applyOffset(cursor.End, offset);
									break;
								}
								//end >= cursor.End
								// [+++++++++[
								//---------------...
								//ou
								// [+++++++++[
								//-----------[
								if (c2 >= 0)
								{
									toRemove.Add(cursor);
									if (c2 == 0) break;
								}
							}
						}
					}
					m_items.RemoveItems(toRemove);
					TranslateAfter(cursor, offset, applyOffset);
					return;
				}
				//begin > cursor.Begin
				else
				{
					//end < cursor.End
					//   [++++++++++++[
					//      [----[
					// = [++[[++++[
					if (c2 < 0)
					{
						var oldEnd = cursor.End;
						cursor.End = begin;
						TranslateAfter(cursor, offset, applyOffset);
						m_items.Insert(new Entry(begin, applyOffset(oldEnd, offset), cursor.Value));
						return;
					}
					//end == cursor.End
					// [+++++++++++++[
					//       [-------[
					if (c2 == 0)
					{
						cursor.End = begin;
						TranslateAfter(cursor, offset, applyOffset);
						return;
					}
					//end > cursor.End
					// [+++++++++++++[
					//       [-------------
					else
					{
						cursor.End = begin;
						while (iterator.Next())
						{
							cursor = iterator.Current;
							var c3 = comparer.Compare(end, cursor.Begin);
							c2 = comparer.Compare(end, cursor.End);
							//end <= cursor.Begin
							//      [++++++++++++[
							// --[
							//ou
							//      [++++++++++++[
							// -----[
							if (c3 <= 0)
							{
								break;
							}
							else
							{
								//end < cursor.End
								//     [+++++++++++++[
								// ------------[
								if (c2 < 0)
								{
									cursor.Begin = begin;
									cursor.End = applyOffset(cursor.End, offset);
									break;
								}
								//end >= cursor.End
								//   [+++++++++++[
								//--------------------...
								//ou
								//   [+++++++++++[
								//---------------[
								else
								{
									if(toRemove == null) toRemove = new List<Entry>();
									toRemove.Add(cursor);
									if (c2 == 0) break;
								}
							}
						}

						if (toRemove != null) m_items.RemoveItems(toRemove);
						TranslateAfter(cursor, offset, applyOffset);
						return;
					}
				}
			}
			finally
			{
				CheckInvariants();
			}
		}

		private void TranslateAfter(Entry lastOk, TKey offset, Func<TKey, TKey, TKey> applyKeyOffset)
		{
			var iterator = m_items.GetIterator();
			//null il faut tout décaller
			if (lastOk == null)
			{
				if (!iterator.SeekFirst()) return;
			}
			else
			{
				if (!iterator.Seek(lastOk, true))
				{
					//l'element passé en parametre à été supprimé
					//on cherche l'élément suivant
					//si tout à été supprimé on sort.
					if (!iterator.SeekFirst()) return;
					var c = m_keyComparer.Compare(lastOk.End, iterator.Current.Begin);
					while (c > 0 && iterator.Next())
					{
						c = m_keyComparer.Compare(lastOk.End, iterator.Current.Begin);
					}
				}
				//on veut décaller les suivants de celui passé en parametre
				else iterator.Next();
			}
			do
			{
				var cursor = iterator.Current;
				//dans le cas ou tout à été supprimé après le lastOK l'iterator est déjà au bout quand on arrive ici...
				if (cursor == null) break;
				cursor.Begin = applyKeyOffset(cursor.Begin, offset);
				cursor.End = applyKeyOffset(cursor.End, offset);
			}
			while (iterator.Next());
			//on décalle les bounds correctement
			if (iterator.SeekFirst()) m_bounds.Begin = iterator.Current.Begin;
			if (iterator.SeekLast()) m_bounds.End = iterator.Current.End;
		}

		public void Mark(TKey begin, TKey end, TValue value)
		{
			if (m_keyComparer.Compare(begin, end) >= 0) throw new InvalidOperationException("End key must be greater than the Begin key.");

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
			Entry cursor;
			var cmp = m_keyComparer;
			int c1, c2;

			try
			{

				switch (m_items.Count)
				{
					case 0:
					{ // the list empty

						// no checks required
						m_items.Insert(entry);
						m_bounds.Begin = entry.Begin;
						m_bounds.End = entry.End;
						break;
					}

					case 1:
					{ // there is only one value

						cursor = m_items[0];

						c1 = cmp.Compare(begin, cursor.End);
						if (c1 >= 0)
						{
							// [--------)  [========)
							// or [--------|========)
							if (c1 == 0 && m_valueComparer.Compare(cursor.Value, value) == 0)
							{
								cursor.End = end;
							}
							else
							{
								m_items.Insert(entry);
							}
							m_bounds.End = end;
							return;
						}
						c1 = cmp.Compare(end, cursor.Begin);
						if (c1 <= 0)
						{
							// [========)  [--------)
							// or [========|--------)
							if (c1 == 0 && m_valueComparer.Compare(cursor.Value, value) == 0)
							{
								cursor.Begin = begin;
							}
							else
							{
								m_items.Insert(entry);
							}
							m_bounds.Begin = begin;
							return;
						}

						c1 = cmp.Compare(begin, cursor.Begin);
						c2 = cmp.Compare(end, cursor.End);
						if (c1 == 0)
						{ // same start
							if (c2 == 0)
							{ // same end
								//   [--------)
								// + [========)
								// = [========)
								cursor.Value = value;
							}
							else if (c2 < 0)
							{
								//   [----------)
								// + [======)
								// = [======|---)
								if (m_valueComparer.Compare(cursor.Value, value) != 0)
								{
									cursor.Begin = end;
									m_items.Insert(entry);
								}
							}
							else
							{
								//   [------)
								// + [==========)
								// = [==========)
								cursor.Set(entry);
								m_bounds.End = end;
							}
						}
						else if (c1 > 0)
						{ // entry is to the right
							if (c2 >= 0)
							{
								//   [------)
								// +     [=======)
								// = [---|=======)

								cursor.End = begin;
								m_items.Insert(entry);
								if (c2 > 0) m_bounds.End = end;
							}
							else
							{
								//   [-----------)
								// +     [====)
								// = [---|====|--)
								var tmp = new Entry(end, cursor.End, cursor.Value);
								cursor.End = begin;
								m_items.InsertItems(entry, tmp);
							}
						}
						else
						{ // entry is to the left
							if (c2 >= 0)
							{
								cursor.Set(entry);
								m_bounds.End = end;
							}
							else
							{
								cursor.Begin = end;
								m_items.Insert(entry);
							}
							m_bounds.Begin = begin;
						}
						break;
					}

					default:
					{
						// check with the bounds first

						if (cmp.Compare(begin, m_bounds.End) > 0)
						{ // completely to the right
							m_items.Insert(entry);
							m_bounds.End = end;
							break;
						}
						if (cmp.Compare(end, m_bounds.Begin) < 0)
						{ // completely to the left
							m_items.Insert(entry);
							m_bounds.Begin = begin;
							break;
						}
						if (cmp.Compare(begin, m_bounds.Begin) <= 0 && cmp.Compare(end, m_bounds.End) >= 0)
						{ // overlaps with all the ranges
							// note :if we are here, there was at least 2 items, so just clear everything
							m_items.Clear();
							m_items.Insert(entry);
							m_bounds.Begin = entry.Begin;
							m_bounds.End = entry.End;
							break;
						}

						// note: we have already bound checked, so we know that there is at least one overlap !

						bool inserted = false;

						// => we will try to find the first range and last range in the dictionary that would be impacted, mutate them and delete all ranges in between

						var iterator = m_items.GetIterator();
						// seek to the range that starts before (or at) the new range's begin point
						if (!iterator.Seek(entry, true))
						{ // the new range will go into first position
							// => still need to check if we are overlapping with the next ranges
							iterator.SeekFirst();
							//Console.WriteLine("  . new lower bound, but intersects with first range...");
							m_bounds.Begin = begin;
						}

						m_bounds.End = Max(m_bounds.End, end);

						cursor = iterator.Current;

						c1 = cmp.Compare(cursor.Begin, begin);
						c2 = cmp.Compare(cursor.End, end);
						if (c1 >= 0)
						{
							if (c2 == 0)
							{ // same end
								//   [-------)..           [-------)..
								// + [=======)        + [==========)
								// = [=======)..      = [==========)..
								cursor.Set(entry);
								return;
							}

							if (c2 > 0)
							{ // truncate begin
								//   [----------)..        [----------)..
								// + [=======)	      + [=======)
								// = [=======|--)..   = [=======|-----)..
								cursor.Begin = end;
								m_items.Insert(entry);
								return;
							}

							// replace + propagate
							//   [-------)???..            [-----)????..
							// + [==========)         + [============)
							// = [==========)..       = [============)..

							cursor.Set(entry);
							//we keep the reference to cursor to be able to modify it later
							entry = cursor;
							inserted = true;
							//TODO: need to propagate !
						}
						else
						{
							if (c2 == 0)
							{ // same end
								//   [------------)
								//       [========)
								// = [---|========)

								cursor.End = begin;
								m_items.Insert(entry);
								return;
							}

							if (c2 > 0)
							{
								//   [------------)
								//       [=====)
								// = [---|=====|--)

								var tmp = new Entry(end, cursor.End, cursor.Value);
								cursor.End = begin;
								m_items.InsertItems(entry, tmp);
								return;
							}

							int c3 = cmp.Compare(begin, cursor.End);
							if (c3 >= 0)
							{
								if (c3 == 0 && m_valueComparer.Compare(value, cursor.Value) == 0)
								{ // touching same value => merge
									cursor.End = end;
									entry = cursor;
									inserted = true;
								}
								else
								{
									//   [---)
									//         [=====????
									// = [---) [=====????
								}
							}
							else
							{
								//   [--------????
								//       [====????
								// = [---|====????
								cursor.End = begin;
							}
						}

						// if we end up here, it means that we may be overlapping with following items
						// => we need to delete them until we reach the last one, which we need to either delete or mutate
						// => also, if we haven't inserted the entry yet, we will reuse the first deleted range to insert the entry, and only insert at the end if we haven't found a spot

						List<Entry> deleted = null;

						while (true)
						{
							if (!iterator.Next())
							{ // we reached past the end of the db
								break;
							}

							// cursor: existing range that we need to either delete or mutate
							cursor = iterator.Current;

							c1 = cmp.Compare(cursor.Begin, end);
							if (c1 == 0)
							{ // touching the next range
								if (m_valueComparer.Compare(value, cursor.Value) == 0)
								{ // contiguous block with same value => merge
									//         [===========)
									//   [=====)
									// = [=================)
									if (inserted)
									{
										if (cmp.Compare(cursor.End, entry.End) > 0)
										{
											entry.End = cursor.End;
										}
										//note: we can't really delete while iterating with a cursor, so just mark it for deletion
										if (deleted == null) deleted = new List<Entry>();
										deleted.Add(cursor);
									}
									else
									{
										cursor.Begin = begin;
										entry = cursor;
										inserted = true;
									}
									break;
								}
								else
								{
									//         [-----------)
									//   [=====)
									// = [=====|-----------)
								}
								break;
							}
							else if (c1 > 0)
							{ // we are past the inserted range, nothing to do any more
								//            [------------)
								//   [=====)
								// = [=====)  [------------)
								//Console.WriteLine("  . no overlap => break");
								break;
							}

							c1 = cmp.Compare(cursor.End, end);
							if (c1 <= 0)
							{ // we are completely covered => delete

								//      [-------)           [-------)
								// + [...=======)      + [...=======...)
								// = [...=======)      = [...=======...)
								if (!inserted)
								{ // use that slot to insert ourselves
									cursor.Set(entry);
									//get the reference to be able to eventually merge it afterwards
									entry = cursor;
									inserted = true;
								}
								else
								{
									//note: we can't really delete while iterating with a cursor, so just mark it for deletion
									if (deleted == null) deleted = new List<Entry>();
									deleted.Add(cursor);

								}
							}
							else
							{ // we are only partially overlapped

								//       [------------)
								//   [....========)
								// = [....========|---)

								cursor.Begin = end;
								break;
							}
						}

						if (deleted != null && deleted.Count > 0)
						{
							m_items.RemoveItems(deleted);
						}

						if (!inserted)
						{ // we did not find an existing spot to re-use, so we need to insert the new range
							m_items.Insert(entry);
						}
						break;
					}
				}
			}
			finally
			{
				CheckInvariants();
			}
		}

		/// <summary>Checks if there is at least one range in the dictionary that intersects with the specified range, and matches the predicate</summary>
		/// <param name="begin">Lower bound of the intersection</param>
		/// <param name="end">Higher bound (excluded) of the intersection</param>
		/// <param name="arg">Value that is passed as the second argument to <paramref name="predicate"/></param>
		/// <param name="predicate">Predicate called for each intersected range.</param>
		/// <returns>True if there was at least one intersecting range, and <paramref name="predicate"/> returned true for that range.</returns>
		public bool Intersect(TKey begin, TKey end, TValue arg, Func<TValue, TValue, bool> predicate)
		{
			if (m_items.Count == 0) return false;

			var cmp = m_keyComparer;
			if (cmp.Compare(m_bounds.Begin, end) >= 0) return false;
			if (cmp.Compare(m_bounds.End, begin) <= 0) return false;

			var entry = new Entry(begin, end, default(TValue));

			var iterator = m_items.GetIterator();
			if (!iterator.Seek(entry, true))
			{ // starts before
				iterator.SeekFirst();
			}

			do
			{
				var cursor = iterator.Current;
				
				// A and B intersects if: CMP(B.end, A.begin) <= 0 .OR. CMP(A.end, B.begin) <= 0

				if (cmp.Compare(end, cursor.Begin) <= 0)
				{ 
					return false;
				}

				if (cmp.Compare(cursor.End, begin) > 0 && predicate(cursor.Value, arg))
				{
					return true;
				}
			}
			while(iterator.Next());

			return false;
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
#if DEBUG
			Debug.WriteLine("Dumping ColaRangeDictionary<" + typeof(TKey).Name + "> filled at " + (100.0d * this.Count / this.Capacity).ToString("N2") + "%");
			m_items.Debug_Dump();
#endif
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
					sb.Append(previous.End).Append('|');
				}

				sb.Append(item.Begin).Append("..(").Append(item.Value).Append(")..");
				previous = item;
			}
			if (previous != null)
			{
				sb.Append(previous.End).Append(")");
			}

			return sb.ToString();
		}

	}

}
