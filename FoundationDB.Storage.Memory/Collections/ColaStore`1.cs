#region Copyright Doxense 2013
//
// All rights are reserved. Reproduction or transmission in whole or in part, in
// any form or by any means, electronic, mechanical or otherwise, is prohibited
// without the prior written consent of the copyright owner.
//
#endregion

// enables consitency checks after each operation to the set
#undef ENFORCE_INVARIANTS

namespace FoundationDB.Storage.Memory.Core
{
	using System;
	using System.Collections.Generic;
	using System.Diagnostics;
	using System.Diagnostics.Contracts;
	using System.Globalization;
	using System.Linq;

	/// <summary>Store elements in a list of ordered levels</summary>
	/// <typeparam name="T">Type of elements stored in the set</typeparam>
	public sealed class ColaStore<T>
	{

		#region Documentation

		// Based on http://supertech.csail.mit.edu/papers/sbtree.pdf (COLA)

		/*
			The cache-oblivious lookahead array (COLA) is similar to the binomial list structure [9] of Bentley and Saxe. It consists of ⌈log2 N⌉ arrays, 
			or levels, each of which is either completely full or completely empty. The kth array is of size 2^k and the arrays are stored contiguously in memory.
		
			The COLA maintains the following invariants:
			1. The kth array contains items if and only if the kth least signiﬁcant bit of the binary representation of N is a 1.
			2. Each array contains its items in ascending order by key
		 */

		// DEFINITIONS
		//
		// "Level" is the index in the list of segments with level 0 being the top (or root)
		// "Segment" is an array whose length is equal to 2^i (where i is the "level" of the segment).
		// "Doubling Array" means that each segment has double the length of its predecessor
		// "Cache Oblivious" means that the algorithm is not tuned for a specific CPU cache size (L1, L2, ou block size on disk), and amortize the cost of insertion over the lifespan of the set.
		//
		// INVARIANTS:
		//
		// * Each segment is twice the size of the previous segment, i.e.: m_levels[i].Length == 1 << i
		//		0 [ ] 1
		//		1 [ , ] 2
		//		2 [ , , , ] 4
		//		3 [ , , , , , , , ] 8
		//		4 [ , , , , , , , , , , , , , , , ] 16
		//		...
		// * A segment is either EMPTY, or completely FULL
		//		legal:		[ , , , ] ou [1,2,3,4]
		//		illegal:	[1,2,3, ]
		// * A segment has all its elements sorted
		//		legal:		[3,12,42,66]
		//		illegal:	[12,66,42,3]
		//
		// NOTES:
		//
		// - 50% of all inserts will always be done on the root (level 0), so will be O(1)
		// - 87.5% of all inserts will only touch levels 0, 1 and 2, which should be contiguous in memory
		// - For random insertions, it is difficult to predict in which level a specific value will be found, except that older values are towards the bottom, and younger values are towards the top.
		// - A range of values (ex: "from 10 to 20") can have its elements scattered in multiple segments
		// - If all inserts are ordered, then all items of level N will be sorted after all the items of level N + 1
		// - Most inserts are usually pretty fast, but every times the count goes to the next power of 2, the duration will be more and more noticeable (ie : the (2^N)th INSERT will have to merge (2^N) values)
		//
		// COST
		//
		// The cost for inserting N values is about N.Log2(N) comparisons
		// - This is amortized to Log2(N) per insert, which means that insertion is O(log(N))
		// - This means that N should stay relatively low (ideally under 2^10 items)

		#endregion

		private const int INITIAL_LEVELS = 5;	// 5 initial levels will pre-allocate space for 31 items
		private const int MAX_SPARE_ORDER = 6;	// 6 levels of spares will satisfy ~98.4% of all insertions, while only allocating the space for 63 items (~500 bytes for reference types)
		private const int NOT_FOUND = -1;

		/// <summary>Number of elements in the store</summary>
		private volatile int m_count;

		/// <summary>Array of all the segments making up the levels</summary>
		private T[][] m_levels;

		/// <summary>Shortcut to level 0 (of size 1)</summary>
		private T[] m_root;

		/// <summary>List of spare temporary buffers, used during merging</summary>
		private T[][] m_spares;

		/// <summary>Key comparer</summary>
		private readonly IComparer<T> m_comparer;

		/// <summary>Version number of the store, incremented after each modification</summary>
		private volatile int m_version;

		#region Constructors...

		/// <summary>Allocates a new store</summary>
		/// <param name="capacity">Initial capacity, or 0 for the default capacity</param>
		/// <param name="comparer">Comparer used to order the elements</param>
		public ColaStore(int capacity, IComparer<T> comparer)
		{
			if (capacity < 0) throw new ArgumentOutOfRangeException("capacity", "Capacity cannot be less than zero.");
			if (comparer == null) throw new ArgumentNullException("comparer");
			Contract.EndContractBlock();

			int levels;
			if (capacity == 0)
			{ // use the default capacity
				levels = INITIAL_LEVELS;
			}
			else
			{ // L levels will only store (2^L - 1)
				// note: there is no real penalty if the capacity was not correctly estimated, appart from the fact that all levels will not be contiguous in memory
				// 1 => 1
				// 2..3 => 2
				// 4..7 => 3
				levels = ColaStore.HighestBit(capacity) + 1;
			}
			// allocating more than 31 levels would mean having an array of length 2^31, which is not possible
			if (levels >= 31) throw new ArgumentOutOfRangeException("capacity", "Cannot allocate more than 30 levels");

			// pre-allocate the segments and spares at the same time, so that they are always at the same memory location
			var segments = new T[levels][];
			var spares = new T[MAX_SPARE_ORDER][];
			for (int i = 0; i < segments.Length; i++)
			{
				segments[i] = new T[1 << i];
				if (i < spares.Length) spares[i] = new T[1 << i];
			}

			m_levels = segments;
			m_root = segments[0];
			m_spares = spares;
			m_comparer = comparer;
		}

		[Conditional("ENFORCE_INVARIANTS")]
		private void CheckInvariants()
		{
			Contract.Assert(m_count >= 0, "Count cannot be less than zero");
			Contract.Assert(m_levels != null, "Storage array should not be null");
			Contract.Assert(m_levels.Length > 0, "Storage array should always at least contain one level");
			Contract.Assert(object.ReferenceEquals(m_root, m_levels[0]), "The root should always be the first level");
			Contract.Assert(m_count < 1 << m_levels.Length, "Count should not exceed the current capacity");

			for (int i = 0; i < m_levels.Length; i++)
			{
				var segment = m_levels[i];
				Contract.Assert(segment != null, "All segments should be allocated in memory");
				Contract.Assert(segment.Length == 1 << i, "The size of a segment should be 2^LEVEL");

				if (IsFree(i))
				{ // All unallocated segments SHOULD be filled with default(T)
					for (int j = 0; j < segment.Length; j++)
					{
						if (m_comparer.Compare(segment[j], default(T)) != 0)
						{
							if (Debugger.IsAttached) { Debug_Dump(); Debugger.Break(); }
							Contract.Assert(false, String.Format("Non-zero value at offset {0} of unused level {1} : {2}", j, i, String.Join(", ", segment)));
						}
					}
				}
				else
				{ // All allocated segments SHOULD be sorted
					T previous = segment[0];
					for (int j = 1; j < segment.Length; j++)
					{
						T x = segment[j];
						if (m_comparer.Compare(previous, x) >= 0)
						{
							if (Debugger.IsAttached) { Debug_Dump(); Debugger.Break(); }
							Contract.Assert(false, String.Format("Unsorted value {3} at offset {0} of allocated level {1} : {2}", j, i, String.Join(", ", segment), segment[j]));
						}
						previous = segment[j];
					}
				}
			}
		}

		#endregion

		#region Public Properties...

		/// <summary>Gets the number of elements in the store.</summary>
		public int Count
		{
			get { return m_count; }
		}

		/// <summary>Gets the current capacity of the store.</summary>
		public int Capacity
		{
			// note: the capacity is always 2^L - 1 where L is the number of levels
			get { return m_levels == null ? 0 : (1 << m_levels.Length) - 1; }
		}

		/// <summary>Gets the comparer used to sort the elements in the store</summary>
		public IComparer<T> Comparer
		{
			get { return m_comparer; }
		}

		/// <summary>Gets the current version of the store</summary>
		public int Version
		{
			get { return m_version; }
		}

		/// <summary>Gets the current number of levels</summary>
		/// <remarks>Note that the last level may not be currently used!</remarks>
		public int Depth
		{
			get { return m_levels.Length; }
		}

		/// <summary>Gets the index of the first currently allocated level</summary>
		public int MinLevel
		{
			get { return ColaStore.HighestBit(m_count); }
		}

		/// <summary>Gets the index of the last currently allocated level</summary>
		public int MaxLevel
		{
			get { return ColaStore.HighestBit(m_count); }
		}

		/// <summary>Gets the list of all levels</summary>
		public T[][] Levels
		{
			get { return m_levels; }
		}

		/// <summary>Returns the content of a level</summary>
		/// <param name="level">Index of the level (0-based)</param>
		/// <returns>Segment that contains all the elements of that level</returns>
		public T[] GetLevel(int level)
		{
			Contract.Assert(level >= 0 && level < m_levels.Length);
			return m_levels[level];
		}

		/// <summary>Gets of sets the value store at the specified index</summary>
		/// <param name="arrayIndex">Absolute index in the vector-array</param>
		/// <returns>Value stored at that location, or default(T) if the location is in an unallocated level</returns>
		public T this[int arrayIndex]
		{
			get
			{
				if (m_count == 1 && arrayIndex == 0) return m_root[0];
				return GetAt(arrayIndex);
			}
			set
			{
				SetAt(arrayIndex, value);
			}
		}

		#endregion

		#region Public Methods...

		/// <summary>Finds the location of an element in the array</summary>
		/// <param name="value">Value of the element to search for.</param>
		/// <param name="offset">Receives the offset of the element inside the level if found; otherwise, 0.</param>
		/// <returns>Level that contains the element if found; otherwise, -1.</returns>
		public int Find(T value, out int offset, out T actualValue)
		{
			if (!IsFree(0))
			{
				// If someone gets the last inserted key, there is a 50% change that it is in the root
				// (if not, it will the the last one of the first non-empty level)
				if (m_comparer.Compare(value, m_root[0]) == 0)
				{
					offset = 0;
					actualValue = m_root[0];
					return 0;
				}
			}

			for (int i = 1; i < m_levels.Length; i++)
			{
				if (IsFree(i))
				{ // this segment is not allocated
					continue;
				}

				var segment = m_levels[i];
				int p = ColaStore.BinarySearch<T>(segment, 0, segment.Length, value, m_comparer);
				if (p >= 0)
				{
					offset = p;
					actualValue = segment[p];
					return i;
				}
			}
			offset = 0;
			actualValue = default(T);
			return NOT_FOUND;
		}

		/// <summary>Search for the smallest element that is larger than a reference element</summary>
		/// <param name="value">Reference element</param>
		/// <param name="orEqual">If true, return the position of the value itself if it is found. If false, return the position of the closest value that is smaller.</param>
		/// <param name="offset">Receive the offset within the level of the next element, or 0 if not found</param>
		/// <param name="result">Receive the value of the next element, or default(T) if not found</param>
		/// <returns>Level of the next element, or -1 if <param name="result"/> was already the largest</returns>
		public int FindNext(T value, bool orEqual, out int offset, out T result)
		{
			int level = NOT_FOUND;
			T min = default(T);
			int minOffset = 0;

			// scan each segment for a value that would be larger, keep track of the smallest found
			for (int i = 0; i < m_levels.Length; i++)
			{
				if (IsFree(i)) continue;

				var segment = m_levels[i];
				int pos = ColaStore.BinarySearch<T>(segment, 0, segment.Length, value, m_comparer);
				if (pos >= 0)
				{ // we found an exact match in this segment
					if (orEqual)
					{
						offset = pos;
						result = segment[pos];
						return i;
					}

					// the next item in this segment should be larger
					++pos;
				}
				else
				{ // we found where it would be stored in this segment
					pos = ~pos;
				}

				if (pos < segment.Length)
				{
					if (level == NOT_FOUND || m_comparer.Compare(segment[pos], min) < 0)
					{ // we found a better candidate
						min = segment[pos];
						level = i;
						minOffset = pos; 
					}
				}
			}

			offset = minOffset;
			result = min;
			return level;
		}

		/// <summary>Search for the largest element that is smaller than a reference element</summary>
		/// <param name="value">Reference element</param>
		/// <param name="orEqual">If true, return the position of the value itself if it is found. If false, return the position of the closest value that is smaller.</param>
		/// <param name="offset">Receive the offset within the level of the previous element, or 0 if not found</param>
		/// <param name="result">Receive the value of the previous element, or default(T) if not found</param>
		/// <returns>Level of the previous element, or -1 if <param name="result"/> was already the smallest</returns>
		public int FindPrevious(T value, bool orEqual, out int offset, out T result)
		{
			int level = NOT_FOUND;
			T max = default(T);
			int maxOffset = 0;

			// scan each segment for a value that would be smaller, keep track of the smallest found
			for (int i = 0; i < m_levels.Length; i++)
			{
				if (IsFree(i)) continue;

				var segment = m_levels[i];
				int pos = ColaStore.BinarySearch<T>(segment, 0, segment.Length, value, m_comparer);
				// the previous item in this segment should be smaller
				if (pos < 0)
				{ // it is not 
					pos = ~pos;
				}
				else if (orEqual)
				{ // we found an exact match in this segment
					offset = pos;
					result = segment[pos];
					return i;
				}

				--pos;

				if (pos >= 0)
				{
					if (level == NOT_FOUND || m_comparer.Compare(segment[pos], max) > 0)
					{ // we found a better candidate
						max = segment[pos];
						level = i;
						maxOffset = pos;
					}
				}
			}

			offset = maxOffset;
			result = max;
			return level;
		}

		public IEnumerable<T> FindBetween(T begin, bool beginOrEqual, T end, bool endOrEqual, int limit)
		{
			Console.WriteLine("Looking for " + begin + (beginOrEqual ? " <= k " : " < k ") + (endOrEqual ? "<= " : "< ") + end + ", max " + limit);

			if (limit > 0)
			{
				for (int i = 0; i < m_levels.Length; i++)
				{
					if (IsFree(i)) continue;

					var segment = m_levels[i];
					Console.WriteLine("> Looking at level " + i + " : " + String.Join(", ", segment));

					int to = ColaStore.BinarySearch<T>(segment, 0, segment.Length, end, m_comparer);
					Console.WriteLine("  > binSearch(end=" + end + ") => "+ to);
					if (to >= 0)
					{
						if (!endOrEqual)
						{
							Console.WriteLine("  > excluding matching end");
							to--;
						}
					}
					else
					{
						to = ~to;
					}
					Console.WriteLine("  > to = " + to);
					if (to < 0 || to >= segment.Length) continue;

					int from = ColaStore.BinarySearch<T>(segment, 0, segment.Length, begin, m_comparer);
					Console.WriteLine("  > binSearch(begin=" + begin + ") => " + from);
					if (from >= 0)
					{
						if (!beginOrEqual)
						{
							Console.WriteLine("  > excluding matching begin");
							from++;
						}
					}
					else
					{
						from = ~from;
					}
					Console.WriteLine("  > from = " + from);
					if (from >= segment.Length) continue;

					if (from > to) continue;

					Console.WriteLine("  > fetch(" + from + "..." + to + ")");
					for (int j = from; j <= to && limit > 0; j++)
					{
						Console.WriteLine("    > " + j + " :" + segment[j]);
						yield return segment[j];
						--limit;
					}
					if (limit <= 0) break;
				}
			}
		}

		/// <summary>Return the value stored at a specific location in the array</summary>
		/// <param name="arrayIndex">Absolute index in the vector-array</param>
		/// <returns>Value stored at this location, or default(T) if the level is not allocated</returns>
		public T GetAt(int arrayIndex)
		{
			Contract.Assert(arrayIndex >= 0 && arrayIndex <= this.Capacity);

			int offset;
			int level = ColaStore.FromIndex(arrayIndex, out offset);

			return GetAt(level, offset);
		}

		/// <summary>Returns the value at a specific location in the array</summary>
		/// <param name="level">Index of the level (0-based)</param>
		/// <param name="offset">Offset in the level (0-based)</param>
		/// <returns>Returns the value at this location, or default(T) if the level is not allocated</returns>
		public T GetAt(int level, int offset)
		{
			Contract.Assert(level >= 0 && level < m_levels.Length && offset >= 0 && offset < 1 << level);
			//TODO: check if level is allocated ?

			var segment = m_levels[level];
			Contract.Assert(segment != null && segment.Length == 1 << level);
			return segment[offset];
		}

		/// <summary>Store a value at a specific location in the arrayh</summary>
		/// <param name="arrayIndex">Absolute index in the vector-array</param>
		/// <param name="value">Value to store</param>
		/// <returns>Previous value at that location</returns>
		public T SetAt(int arrayIndex, T value)
		{
			Contract.Assert(arrayIndex >= 0 && arrayIndex <= this.Capacity);

			int offset;
			int level = ColaStore.FromIndex(arrayIndex, out offset);

			return SetAt(level, offset, value);
		}

		/// <summary>Overwrites a specific location in the array with a new value, and returns its previous value</summary>
		/// <param name="level">Index of the level (0-based)</param>
		/// <param name="offset">Offset in the level (0-based)</param>
		/// <param name="value">New value for this location</param>
		/// <returns>Previous value at this location</returns>
		public T SetAt(int level, int offset, T value)
		{
			Contract.Assert(level >= 0 && level < m_levels.Length && offset >= 0 && offset < 1 << level);
			//TODO: check if level is allocated ?

			var segment = m_levels[level];
			Contract.Assert(segment != null && segment.Length == 1 << level);
			T previous = segment[offset];
			segment[offset] = value;
			return previous;
		}

		/// <summary>Clear the array</summary>
		public void Clear()
		{
			++m_version;
			for (int i = 0; i < m_levels.Length; i++)
			{
				if (i < MAX_SPARE_ORDER)
				{ // we can put this segment into the spare list
					var seg = m_levels[i];
					if (seg != null)
					{
						Array.Clear(seg, 0, seg.Length);
						m_spares[i] = seg;
					}
				}
				if (i < MAX_SPARE_ORDER)
				{
					Array.Clear(m_levels[i], 0, 1 << i);
				}
				else
				{
					m_levels[i] = null;
				}
			}
			m_count = 0;

			CheckInvariants();
		}

		/// <summary>Add a value to the array</summary>
		/// <param name="value">Value to add to the array</param>
		/// <param name="overwriteExistingValue">If <paramref name="value"/> already exists in the array and <paramref name="overwriteExistingValue"/> is true, it will be overwritten with <paramref name="value"/></param>
		/// <returns>If the value did not  if the value was been added to the array, or false if it was already there.</returns>
		public bool SetOrAdd(T value, bool overwriteExistingValue)
		{
			T _;
			int offset, level = Find(value, out offset, out _);
			if (level >= 0)
			{
				if (overwriteExistingValue)
				{
					m_levels[level][offset] = value;
				}
				return false;
			}

			Insert(value);
			return true;
		}

		/// <summary>Insert a new element in the set, and returns its index.</summary>
		/// <param name="value">Value to insert. Warning: if the value already exists, the store will be corrupted !</param>
		/// <remarks>The index is the absolute index, as if all the levels where a single, contiguous, array (0 = root, 7 = first element of level 3)</remarks>
		public void Insert(T value)
		{
			++m_version;

			if (IsFree(0))
			{ // half the inserts (when the count is even) can be done in the root
				m_root[0] = value;
			}
			else if (IsFree(1))
			{ // a quarter of the inserts only need to move the root and the value to level 1
				ColaStore.MergeSimple<T>(m_levels[1], m_root[0], value, m_comparer);
				m_root[0] = default(T);
			}
			else
			{ // we need to merge one or more levels

				var spare = GetSpare(0);
				Contract.Assert(spare != null && spare.Length == 1);
				spare[0] = value;
				MergeCascade(1, m_root, spare);
				spare[0] = default(T);
			}
			++m_count;

			CheckInvariants();
		}

		/// <summary>Remove the value at the specified location</summary>
		/// <param name="arrayIndex">Absolute index in the vector-array</param>
		/// <returns>Value that was removed</returns>
		public T RemoveAt(int arrayIndex)
		{
			Contract.Requires(arrayIndex >= 0 && arrayIndex <= this.Capacity);
			int offset, level = ColaStore.FromIndex(arrayIndex, out offset);
			return RemoveAt(level, offset);
		}

		/// <summary>Remove the value at the specified location</summary>
		/// <param name="level">Index of the level (0-based)</param>
		/// <param name="offset">Offset in the level (0-based)</param>
		/// <returns>Value that was removed</returns>
		public T RemoveAt(int level, int offset)
		{
			Contract.Assert(level >= 0 && offset >= 0 && offset < 1 << level);
			//TODO: check if level is allocated ?

			++m_version;

			var segment = m_levels[level];
			Contract.Assert(segment != null && segment.Length == 1 << level);
			T removed = segment[offset];

			if (level == 0)
			{ // removing the last inserted value
				segment[0] = default(T);
			}
			else if (level == 1)
			{ // split the first level in two
				if (IsFree(0))
				{ // move up to root

					// ex: remove 'b' at (1,1) and move the 'a' back to the root
					// 0 [_]	=> [a]
					// 1 [a,b]	=> [_,_]

					m_root[0] = segment[1 - offset];
					segment[0] = default(T);
					segment[1] = default(T);
				}
				else
				{ // merge the root in missing spot

					// ex: remove 'b' at (1,1) and move the 'c' down a level
					//		  N = 3		N = 2
					//		0 [c]	=>	0 [_]
					//		1 [a,b]	=>	1 [a,c]

					ColaStore.MergeSimple<T>(segment, m_root[0], segment[1 - offset], m_comparer);
					m_root[0] = default(T);
				}
			}
			else if ((m_count & 1) == 1)
			{ // Remove an item from an odd-numbered set

				// Since the new count will be even, we only need to merge the root in place with the level that is missing a spot

				// ex: replace the 'b' at (2,1) with the 'e' in the root
				//		  N = 5			  N = 4
				//		0 [e]		=>	0 [_]
				//		1 [_,_]			1 [_,_]
				//		2 [a,b,c,d]	=>	2 [a,c,d,e]

				ColaStore.MergeInPlace<T>(segment, offset, m_root[0], m_comparer);
				m_root[0] = default(T);
			}
			else
			{
				// we are missing a spot in out modified segment, that need to fill
				// > we will take the first non empty segment, and break it in pieces
				//  > its last item will be used to fill the empty spot
				//  > the rest of its items will be spread to all the previous empty segments

				// find the first non empty segment that can be broken
				int firstNonEmptyLevel = ColaStore.LowestBit(m_count);

				if (firstNonEmptyLevel == level)
				{ // we are the first level, this is easy !

					// move the empty spot at the start
					if (offset > 0) Array.Copy(segment, 0, segment, 1, offset);

					// and spread the rest to all the previous levels
					ColaStore.SpreadLevel(level, m_levels);
					//TODO: modify SpreadLevel(..) to take the offset of the value to skip ?
				}
				else
				{ // break that level, and merge its last item with the level that is missing one spot

					// break down this level
					T tmp = ColaStore.SpreadLevel(firstNonEmptyLevel, m_levels);

					// merge its last item with the empty spot in the modified level
					ColaStore.MergeInPlace(m_levels[level], offset, tmp, m_comparer);
				}
			}

			--m_count;

			if (m_levels.Length > MAX_SPARE_ORDER)
			{ // maybe release the last level if it is empty
				ShrinkIfRequired();
			}

			CheckInvariants();

			return removed;
		}

		public void CopyTo(T[] array, int arrayIndex, int count)
		{
			if (array == null) throw new ArgumentNullException("array");
			if (arrayIndex < 0) throw new ArgumentOutOfRangeException("Index cannot be less than zero.");
			if (count < 0) throw new ArgumentOutOfRangeException("Count cannot be less than zero.");
			if (arrayIndex > array.Length || count > (array.Length - arrayIndex)) throw new ArgumentException("Destination array is too small");
			Contract.EndContractBlock();
			
			int p = arrayIndex;
			count = Math.Min(count, m_count);
			foreach (var item in ColaStore.IterateOrdered(count, m_levels, m_comparer, false))
			{
				array[p++] = item;
			}
			Contract.Assert(p == arrayIndex + count);
		}

		/// <summary>Checks if a level is currently not allocated</summary>
		/// <param name="level">Index of the level (0-based)</param>
		/// <returns>True is the level is unallocated and does not store any elements; otherwise, false.</returns>
		public bool IsFree(int level)
		{
			Contract.Requires(level >= 0);
			return (m_count & (1 << level)) == 0;
		}

		/// <summary>Gets a temporary buffer with the length corresponding to the specified level</summary>
		/// <param name="level">Level of this spare buffer</param>
		/// <returns>Temporary buffer whose size is 2^level</returns>
		/// <remarks>The buffer should be returned after use by calling <see cref="PutSpare()"/></remarks>
		public T[] GetSpare(int level)
		{
			Contract.Requires(level >= 0 && m_spares != null);

			if (level < m_spares.Length)
			{ // this level is kept in the spare list
				var t = m_spares[level];
				if (t == null)
				{ // allocate a new one
					t = new T[1 << level];
				}
				else
				{ // remove it from the spare list
					m_spares[level] = null;
				}
				return t;
			}
			else
			{ // this level is always allocated
				return new T[1 << level];
			}
		}

		/// <summary>Return a temporary buffer after use</summary>
		/// <param name="level">Level of the temporary buffer</param>
		/// <returns>True if the buffer has been cleared and returned to the spare list, false if it was discarded</returns>
		/// <remarks>Kept buffers are cleared to prevent values from being kept alive and not garbage collected.</remarks>
		public bool PutSpare(int level, T[] spare)
		{
			Contract.Assert(level >= 0 && spare != null);

			// only clear spares that are kept alive		
			if (level < m_spares.Length && m_spares[level] == null)
			{
				// clear it in case it holds onto dead values that could be garbage collected
				Array.Clear(spare, 0, spare.Length);
				m_spares[level] = spare;
				return true;
			}
			return false;
		}

		/// <summary>Find the smallest element in the store</summary>
		/// <returns>Smallest element found, or default(T) if the store is empty</returns>
		public T Min()
		{
			switch (m_count)
			{
				case 0: return default(T);
				case 1: return m_root[0];
				case 2: return m_levels[1][0];
				default:
				{

					int level = ColaStore.LowestBit(m_count);
					int end = ColaStore.HighestBit(m_count);
					T min = m_levels[level][0];
					while (level <= end)
					{
						if (!IsFree(level) && m_comparer.Compare(min, m_levels[level][0]) > 0)
						{
							min = m_levels[level][0];
						}
						++level;
					}
					return min;
				}
			}
		}

		/// <summary>Find the largest element in the store</summary>
		/// <returns>Largest element found, or default(T) if the store is empty</returns>
		public T Max()
		{
			switch (m_count)
			{
				case 0: return default(T);
				case 1: return m_root[0];
				case 2: return m_levels[1][1];
				default:
				{
					int level = ColaStore.LowestBit(m_count);
					int end = ColaStore.HighestBit(m_count);
					T max = m_levels[level][0];
					while (level <= end)
					{
						if (!IsFree(level) && m_comparer.Compare(max, m_levels[level][0]) < 0)
						{
							max = m_levels[level][0];
						}
						++level;
					}
					return max;
				}
			}

		}

		/// <summary>Returns the smallest and largest element in the store</summary>
		/// <param name="min">Receives the value of the smallest element (or default(T) is the store is Empty)</param>
		/// <param name="max">Receives the value of the largest element (or default(T) is the store is Empty)</param>
		/// <remarks>If the store contains only one element, than min and max will be equal</remarks>
		public void GetBounds(out T min, out T max)
		{
			switch (m_count)
			{
				case 0:
				{
					min = default(T);
					max = default(T);
					break;
				}
				case 1:
				{
					min = m_root[0];
					max = min;
					break;
				}
				case 2:
				{
					min = m_levels[1][0];
					max = m_levels[1][1];
					break;
				}
				default:
				{

					int level = ColaStore.LowestBit(m_count);
					int end = ColaStore.HighestBit(m_count);
					var segment = m_levels[level];
					min = segment[0];
					max = segment[segment.Length - 1];
					while (level <= end)
					{
						if (IsFree(level)) continue;
						segment = m_levels[level];
						if (m_comparer.Compare(min, segment[0]) > 0) min = segment[0];
						if (m_comparer.Compare(max, segment[segment.Length - 1]) < 0) min = segment[segment.Length - 1];
						++level;
					}
					break;
				}
			}
		}

		public ColaStore.Iterator<T> GetIterator()
		{
			return new ColaStore.Iterator<T>(m_levels, m_count, m_comparer);
		}

		#endregion

		private void MergeCascade(int level, T[] left, T[] right)
		{
			Contract.Requires(level > 0, "level");
			Contract.Requires(left != null && left.Length == (1 << (level - 1)), "left");
			Contract.Requires(right != null && right.Length == (1 << (level - 1)), "right");

			if (IsFree(level))
			{ // target level is empty

				if (level >= m_levels.Length) Grow(level);
				Contract.Assert(level < m_levels.Length);

				ColaStore.MergeSort(m_levels[level], left, right, m_comparer);
			}
			else if (IsFree(level + 1))
			{ // the next level is empty

				//Console.WriteLine("MergeCascade(" + level + ", " + left.Length + ", " + left.Length+ ") TO NEXT");


				if (level + 1 >= m_levels.Length) Grow(level + 1);
				Contract.Assert(level + 1 < m_levels.Length);

				var spare = GetSpare(level);
				//TODO: use a 3-way merge ?
				ColaStore.MergeSort(spare, left, right, m_comparer);
				var next = m_levels[level];
				ColaStore.MergeSort(m_levels[level + 1], next, spare, m_comparer);
				Array.Clear(next, 0, next.Length);
				PutSpare(level, spare);
			}
			else
			{ // both are full, need to do a cascade merge
				Contract.Assert(level < m_levels.Length);

				//Console.WriteLine("MergeCascade(" + level + ", " + left.Length + ", " + left.Length + ") CASCADE");

				// merge N and N +1
				var spare = GetSpare(level);
				ColaStore.MergeSort(spare, left, right, m_comparer);

				// and cascade to N + 2 ...
				MergeCascade(level + 1, m_levels[level], spare);
				PutSpare(level, spare);
			}

			Array.Clear(left, 0, left.Length);
			Array.Clear(right, 0, right.Length);
		}

		/// <summary>Grow the capacity of the level array</summary>
		/// <param name="level">Minimum level required</param>
		private void Grow(int level)
		{
			Contract.Requires(level >= 0);

			// note: we want m_segments[level] to not be empty, which means there must be at least (level + 1) entries in the level array
			int current = m_levels.Length;
			int required = level + 1;
			Contract.Assert(current < required);

			var tmpSegments = new T[required][];
			Array.Copy(m_levels, tmpSegments, current);
			for (int i = current; i < required; i++)
			{
				tmpSegments[i] = new T[1 << i];
			}
			m_levels = tmpSegments;

			Contract.Ensures(m_levels != null && m_levels.Length > level);
			CheckInvariants();
			//Console.WriteLine("Grew up to " + required + " levels, capacity = " + this.Capacity);
		}

		private void ShrinkIfRequired()
		{
			int n = m_levels.Length - 1;
			if (n <= MAX_SPARE_ORDER) return;
			if (IsFree(n))
			{ // less than 50% full

				// to avoid the degenerate case of constantly Adding/Removing when at the threshold of a new level,
				// we will only remove the last level if the previous level is also empty

				if (IsFree(n - 1))
				{ // less than 25% full


					// remove the last level
					var tmpSegments = new T[n][];
					Array.Copy(m_levels, tmpSegments, n);
					m_levels = tmpSegments;
					//Console.WriteLine("Shrank down " + n + " levels, capacity = " + this.Capacity);
					CheckInvariants();
				}
			}
		}

		internal IEnumerable<T> IterateOrdered(bool reverse = false)
		{
			return ColaStore.IterateOrdered(m_count, m_levels, m_comparer, reverse);
		}

		internal IEnumerable<T> IterateUnordered()
		{
			return ColaStore.IterateUnordered(m_count, m_levels);
		}

		//TODO: remove or set to internal !
		public void Debug_Dump(Func<T, string> dump = null)
		{
			Console.WriteLine("> " + m_levels.Length + " levels:");
			for(int i = 0; i < m_levels.Length; i++)
			{
				string s = dump == null ? String.Join(", ", m_levels[i]) : String.Join(", ", m_levels[i].Select(dump));
				Console.WriteLine(String.Format(CultureInfo.InvariantCulture, "  - {0,2}: [{1}] {2}", i, IsFree(i) ? "free" : "USED", s));
			}
#if false
			Console.WriteLine("> " + m_spares.Length + " spares:");
			for (int i = 0; i < m_spares.Length; i++)
			{
				var spare = m_spares[i];
				Console.WriteLine(String.Format(CultureInfo.InvariantCulture, "> {0,2}: {1}", i, spare == null ? "<unallocated>" : String.Join(", ", spare)));
			}
#endif
			Console.WriteLine("> " + m_count + " items");
		}

	}

}
