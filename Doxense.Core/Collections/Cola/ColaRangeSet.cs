#region Copyright (c) 2023 SnowBank SAS, (c) 2005-2023 Doxense SAS
// All rights reserved.
// 
// Redistribution and use in source and binary forms, with or without
// modification, are permitted provided that the following conditions are met:
// 	* Redistributions of source code must retain the above copyright
// 	  notice, this list of conditions and the following disclaimer.
// 	* Redistributions in binary form must reproduce the above copyright
// 	  notice, this list of conditions and the following disclaimer in the
// 	  documentation and/or other materials provided with the distribution.
// 	* Neither the name of SnowBank nor the
// 	  names of its contributors may be used to endorse or promote products
// 	  derived from this software without specific prior written permission.
// 
// THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND
// ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
// WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
// DISCLAIMED. IN NO EVENT SHALL SNOWBANK SAS BE LIABLE FOR ANY
// DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
// (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
// LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
// ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
// (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
// SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
#endregion

// enables consitency checks after each operation to the set
#define ENFORCE_INVARIANTS

namespace Doxense.Collections.Generic
{
	using System;
	using System.Collections.Generic;
	using System.Diagnostics;
	using System.Globalization;
	using Doxense.Diagnostics.Contracts;
	using JetBrains.Annotations;

	/// <summary>Represent an ordered list of ranges, stored in a Cache Oblivious Lookup Array</summary>
	/// <typeparam name="TKey">Type of keys stored in the set</typeparam>
	[PublicAPI]
	[DebuggerDisplay("Count={m_items.Count}, Bounds={m_bounds.Begin}..{m_bounds.End}")]
	public sealed class ColaRangeSet<TKey> : IEnumerable<ColaRangeSet<TKey>.Entry>
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
			public TKey? Begin { get; internal set; }
			public TKey? End { get; internal set; }

			public Entry(TKey? begin, TKey? end)
			{
				this.Begin = begin;
				this.End = end;
			}

			internal void ReplaceWith(Entry other)
			{
				this.Begin = other.Begin;
				this.End = other.End;
			}

			internal void Update(TKey? begin, TKey? end)
			{
				this.Begin = begin;
				this.End = end;
			}

			internal bool Contains(TKey key, IComparer<TKey> comparer)
			{
				return comparer.Compare(key, this.Begin) >= 0 && comparer.Compare(key, this.End) < 0;
			}

			public override string ToString()
			{
				return string.Format(CultureInfo.InvariantCulture, "[{0}, {1})", this.Begin, this.End);
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

			public int Compare(Entry? x, Entry? y)
			{
				Contract.Debug.Requires(x != null && y != null);
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

		public ColaRangeSet(IComparer<TKey>? keyComparer)
			: this(0, keyComparer)
		{ }

		public ColaRangeSet(int capacity, IComparer<TKey>? keyComparer)
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

		public int Count => m_items.Count;

		public int Capacity => m_items.Capacity;

		public IComparer<TKey> Comparer => m_comparer;

		public Entry Bounds => m_bounds;

		private bool Resolve(Entry previous, Entry candidate)
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

		private TKey Min(TKey? a, TKey? b)
		{
			Contract.Debug.Requires(a != null && b != null);
			return m_comparer.Compare(a, b) <= 0 ? a : b;
		}

		private TKey Max(TKey? a, TKey? b)
		{
			Contract.Debug.Requires(a != null && b != null);
			return m_comparer.Compare(a, b) >= 0 ? a : b;
		}

		public void Clear()
		{
			m_items.Clear();
			m_bounds.Update(default(TKey), default(TKey));
		}

		public void Mark(TKey begin, TKey end)
		{
			if (m_comparer.Compare(begin, end) >= 0) throw new InvalidOperationException($"End key `{begin}` must be greater than the Begin key `{end}`.");

			var entry = new Entry(begin, end);
			Entry? cursor;

			switch (m_items.Count)
			{
				case 0:
				{ // the list empty

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
					int level;
					bool inserted = false;

					// once inserted, will it conflict with the previous entry ?
					if ((level = m_items.FindPrevious(entry, true, out int offset, out cursor)) >= 0)
					{
						if (Resolve(cursor!, entry))
						{
							entry = cursor!;
							inserted = true;
						}
					}

					// also check for potential conflicts with the next entries
					while (true)
					{
						level = m_items.FindNext(entry, false, out offset, out cursor);
						if (level < 0) break;

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

		/// <summary>Checks if there is at least one range that contains the specified key</summary>
		/// <param name="key">Key to test</param>
		/// <returns>True if the key is contained by one range; otherwise, false.</returns>
		public bool ContainsKey(TKey key)
		{
			if (m_bounds.Contains(key, m_comparer))
			{
				int level = m_items.FindPrevious(new Entry(key, key), true, out _, out var entry);
				return level >= 0 && entry!.Contains(key, m_comparer);
			}
			return false;
		}

		public ColaStore.Enumerator<Entry> GetEnumerator()
		{
			return new ColaStore.Enumerator<Entry>(m_items, reverse: false);
		}

		public IEnumerable<Entry> IterateOrdered()
		{
			return m_items.IterateOrdered(false);
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
			Console.WriteLine("Dumping ColaRangeSet<" + typeof(TKey).Name + "> filled at " + (100.0d * this.Count / this.Capacity).ToString("N2") + "%");
			m_items.Debug_Dump();
#endif
		}

		public override string ToString()
		{
			if (m_items.Count == 0) return "{ }";
			return "{ " + string.Join(", ", this) + " }";
		}

	}

}
