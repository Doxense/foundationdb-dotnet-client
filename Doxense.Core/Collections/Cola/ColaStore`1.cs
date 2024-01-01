#region Copyright (c) 2023-2024 SnowBank SAS, (c) 2005-2023 Doxense SAS
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
//#define ENFORCE_INVARIANTS

namespace Doxense.Collections.Generic
{
	using System;
	using System.Collections.Generic;
	using System.Diagnostics;
	using System.Diagnostics.CodeAnalysis;
	using System.Globalization;
	using System.Linq;
	using System.Runtime.CompilerServices;
	using System.Threading;
	using Doxense.Diagnostics.Contracts;
	using JetBrains.Annotations;

	/// <summary>Store elements in a list of ordered levels</summary>
	/// <typeparam name="T">Type of elements stored in the set</typeparam>
	[PublicAPI]
	[DebuggerDisplay("Count={m_count}, Depth={m_levels.Length}")]
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
		//		legal:		[ , , , ] or [1,2,3,4]
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
		private readonly T[] m_root;

		/// <summary>List of spare temporary buffers, used during merging</summary>
		private readonly T[]?[] m_spares;
#if ENFORCE_INVARIANTS
		private bool[] m_spareUsed;
#endif

		/// <summary>Key comparer</summary>
		private readonly IComparer<T> m_comparer;

		#region Constructors...

		/// <summary>Allocates a new store</summary>
		/// <param name="capacity">Initial capacity, or 0 for the default capacity</param>
		/// <param name="comparer">Comparer used to order the elements</param>
		public ColaStore(int capacity, IComparer<T> comparer)
		{
			Contract.Positive(capacity);
			Contract.NotNull(comparer);

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
			if (levels >= 31) throw new ArgumentOutOfRangeException(nameof(capacity), "Cannot allocate more than 30 levels");

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
#if ENFORCE_INVARIANTS
			m_spareUsed = new bool[spares.Length];
#endif
		}

		public ColaStore(ColaStore<T> copy)
		{
			m_count = copy.m_count;
			var levels = copy.m_levels;
			var tmp = new T[levels.Length][];
			for (int i = 0; i < levels.Length; i++)
			{
				tmp[i] = levels[i].ToArray();
			}
			m_levels = tmp;
			m_root = tmp[0];
			m_spares = copy.m_spares.ToArray();
			m_comparer = copy.m_comparer;
#if ENFORCE_INVARIANTS
			m_spareUsed = copy.m_spareUsed.ToArray();
#endif
		}

		public ColaStore<T> Copy() => new(this);

		[Conditional("ENFORCE_INVARIANTS")]
		private void CheckInvariants()
		{
#if ENFORCE_INVARIANTS
			Contract.Debug.Invariant(m_count >= 0, "Count cannot be less than zero");
			Contract.Debug.Invariant(m_levels != null, "Storage array should not be null");
			Contract.Debug.Invariant(m_levels.Length > 0, "Storage array should always at least contain one level");
			Contract.Debug.Invariant(object.ReferenceEquals(m_root, m_levels[0]), "The root should always be the first level");
			Contract.Debug.Invariant(m_count < 1 << m_levels.Length, "Count should not exceed the current capacity");

			for (int i = 0; i < m_levels.Length; i++)
			{
				var segment = m_levels[i];
				Contract.Debug.Invariant(segment != null, "All segments should be allocated in memory");
				Contract.Debug.Invariant(segment.Length == 1 << i, "The size of a segment should be 2^LEVEL");

				if (IsFree(i))
				{ // All unallocated segments SHOULD be filled with default(T)
					for (int j = 0; j < segment.Length; j++)
					{
						if (!EqualityComparer<T>.Default.Equals(segment[j], default(T)))
						{
							if (Debugger.IsAttached) { Debug_Dump(); Debugger.Break(); }
							Contract.Debug.Invariant(false, String.Format("Non-zero value at offset {0} of unused level {1} : {2}", j, i, String.Join(", ", segment)));
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
							Contract.Debug.Invariant(false, String.Format("Unsorted value {3} at offset {0} of allocated level {1} : {2}", j, i, String.Join(", ", segment), segment[j]));
						}
						previous = segment[j];
					}
				}

				if (i < m_spares.Length)
				{
					Contract.Debug.Invariant(!m_spareUsed[i], "A spare level wasn't returned after being used!");
					var spare = m_spares[i];
					if (spare == null) continue;
					// All spare segments SHOULD be filled with default(T)
					for (int j = 0; j < spare.Length; j++)
					{
						if (!EqualityComparer<T>.Default.Equals(spare[j], default(T)))
						{
							if (Debugger.IsAttached) { Debug_Dump(); Debugger.Break(); }
							Contract.Debug.Invariant(false, String.Format("Non-zero value at offset {0} of spare level {1} : {2}", j, i, String.Join(", ", spare)));
						}
					}

				}
			}
#endif
		}

		#endregion

		#region Public Properties...

		/// <summary>Gets the number of elements in the store.</summary>
		public int Count => m_count;

		/// <summary>Gets the current capacity of the store.</summary>
		public int Capacity => (1 << m_levels.Length) - 1;
		// note: the capacity is always 2^L - 1 where L is the number of levels

		/// <summary>Gets the comparer used to sort the elements in the store</summary>
		public IComparer<T> Comparer => m_comparer;

		/// <summary>Gets the current number of levels</summary>
		/// <remarks>Note that the last level may not be currently used!</remarks>
		public int Depth => m_levels.Length;

		/// <summary>Gets the index of the first currently allocated level</summary>
		public int MinLevel => ColaStore.HighestBit(m_count);

		/// <summary>Gets the index of the last currently allocated level</summary>
		public int MaxLevel => ColaStore.HighestBit(m_count);

		/// <summary>Gets the list of all levels</summary>
		public T[][] Levels => m_levels;

		/// <summary>Returns the content of a level</summary>
		/// <param name="level">Index of the level (0-based)</param>
		/// <returns>Segment that contains all the elements of that level</returns>
		public T[] GetLevel(int level)
		{
			Contract.Debug.Requires(level >= 0 && level < m_levels.Length);
			return m_levels[level];
		}

		/// <summary>Gets of sets the value store at the specified index</summary>
		/// <param name="arrayIndex">Absolute index in the vector-array</param>
		/// <returns>Value stored at that location, or default(T) if the location is in an unallocated level</returns>
		public T this[int arrayIndex]
		{
			get => m_count == 1 && arrayIndex == 0 ? m_root[0] : GetAt(arrayIndex);
			set => SetAt(arrayIndex, value);
		}

		#endregion

		#region Public Methods...

		/// <summary>Finds the location of an element in the array</summary>
		/// <param name="value">Value of the element to search for.</param>
		/// <param name="offset">Receives the offset of the element inside the level if found; otherwise, 0.</param>
		/// <param name="actualValue">Receives the original instance of the value that was found</param>
		/// <returns>Level that contains the element if found; otherwise, -1.</returns>
		public int Find(T value, out int offset, out T actualValue)
		//REVIEW => TryFind?
		{
			if ((m_count & 1) != 0)
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

			var levels = m_levels;
			for (int i = 1; i < levels.Length; i++)
			{
				if (IsFree(i))
				{ // this segment is not allocated
					continue;
				}

				int p = ColaStore.BinarySearch<T>(levels[i], 0, 1 << i, value, m_comparer);
				if (p >= 0)
				{
					offset = p;
					actualValue = levels[i][p];
					return i;
				}
			}
			offset = 0;
			actualValue = default!;
			return NOT_FOUND;
		}

		/// <summary>Search for the smallest element that is larger than a reference element</summary>
		/// <param name="value">Reference element</param>
		/// <param name="orEqual">If true, return the position of the value itself if it is found. If false, return the position of the closest value that is smaller.</param>
		/// <param name="offset">Receive the offset within the level of the next element, or 0 if not found</param>
		/// <param name="result">Receive the value of the next element, or default(T) if not found</param>
		/// <returns>Level of the next element, or -1 if <param name="result"/> was already the largest</returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public int FindNext(T value, bool orEqual, out int offset, out T result)
		{
			return ColaStore.FindNext<T>(m_levels, m_count, value, orEqual, m_comparer, out offset, out result);
		}
		
		/// <summary>Search for the smallest element that is larger than a reference element</summary>
		/// <param name="value">Reference element</param>
		/// <param name="orEqual">If true, return the position of the value itself if it is found. If false, return the position of the closest value that is smaller.</param>
		/// <param name="comparer"></param>
		/// <param name="offset">Receive the offset within the level of the next element, or 0 if not found</param>
		/// <param name="result">Receive the value of the next element, or default(T) if not found</param>
		/// <returns>Level of the next element, or -1 if <param name="result"/> was already the largest</returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public int FindNext(T value, bool orEqual, IComparer<T>? comparer, out int offset, out T result)
		{
			return ColaStore.FindNext<T>(m_levels, m_count, value, orEqual, comparer ?? m_comparer, out offset, out result);
		}

		/// <summary>Search for the largest element that is smaller than a reference element</summary>
		/// <param name="value">Reference element</param>
		/// <param name="orEqual">If true, return the position of the value itself if it is found. If false, return the position of the closest value that is smaller.</param>
		/// <param name="offset">Receive the offset within the level of the previous element, or 0 if not found</param>
		/// <param name="result">Receive the value of the previous element, or default(T) if not found</param>
		/// <returns>Level of the previous element, or -1 if <param name="result"/> was already the smallest</returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public int FindPrevious(T value, bool orEqual, out int offset, out T? result)
		{
			return ColaStore.FindPrevious<T>(m_levels, m_count, value, orEqual, m_comparer, out offset, out result);
		}

		/// <summary>Search for the largest element that is smaller than a reference element</summary>
		/// <param name="value">Reference element</param>
		/// <param name="orEqual">If true, return the position of the value itself if it is found. If false, return the position of the closest value that is smaller.</param>
		/// <param name="comparer"></param>
		/// <param name="offset">Receive the offset within the level of the previous element, or 0 if not found</param>
		/// <param name="result">Receive the value of the previous element, or default(T) if not found</param>
		/// <returns>Level of the previous element, or -1 if <param name="result"/> was already the smallest</returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public int FindPrevious(T value, bool orEqual, IComparer<T>? comparer, out int offset, out T? result)
		{
			return ColaStore.FindPrevious<T>(m_levels, m_count, value, orEqual, comparer ?? m_comparer, out offset, out result);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public IEnumerable<T> FindBetween(T beginInclusive, T endExclusive, int limit)
		{
			return ColaStore.FindBetween<T>(m_levels, m_count, beginInclusive, true, endExclusive, false, limit, m_comparer);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public IEnumerable<T> FindBetween(T begin, bool beginOrEqual, T end, bool endOrEqual, int limit, IComparer<T>? comparer = null)
		{
			return ColaStore.FindBetween<T>(m_levels, m_count, begin, beginOrEqual, end, endOrEqual, limit, comparer ?? m_comparer);
		}

		/// <summary>Return the value stored at a specific location in the array</summary>
		/// <param name="arrayIndex">Absolute index in the vector-array</param>
		/// <returns>Value stored at this location, or default(T) if the level is not allocated</returns>
		public T GetAt(int arrayIndex)
		{
			Contract.Debug.Requires(arrayIndex >= 0 && arrayIndex <= this.Capacity);

			int level = ColaStore.FromIndex(arrayIndex, out var offset);

			return GetAt(level, offset);
		}

		/// <summary>Returns the value at a specific location in the array</summary>
		/// <param name="level">Index of the level (0-based)</param>
		/// <param name="offset">Offset in the level (0-based)</param>
		/// <returns>Returns the value at this location, or default(T) if the level is not allocated</returns>
		public T GetAt(int level, int offset)
		{
			Contract.Debug.Requires(level >= 0 && level < m_levels.Length && offset >= 0 && offset < 1 << level);
			//TODO: check if level is allocated ?

			var segment = m_levels[level];
			Contract.Debug.Assert(segment != null && segment.Length == 1 << level);
			return segment[offset];
		}

		/// <summary>Store a value at a specific location in the arrayh</summary>
		/// <param name="arrayIndex">Absolute index in the vector-array</param>
		/// <param name="value">Value to store</param>
		/// <returns>Previous value at that location</returns>
		public T SetAt(int arrayIndex, T value)
		{
			Contract.Debug.Requires(arrayIndex >= 0 && arrayIndex <= this.Capacity);

			int level = ColaStore.FromIndex(arrayIndex, out var offset);

			return SetAt(level, offset, value);
		}

		/// <summary>Overwrites a specific location in the array with a new value, and returns its previous value</summary>
		/// <param name="level">Index of the level (0-based)</param>
		/// <param name="offset">Offset in the level (0-based)</param>
		/// <param name="value">New value for this location</param>
		/// <returns>Previous value at this location</returns>
		public T SetAt(int level, int offset, T value)
		{
			Contract.Debug.Requires(level >= 0 && level < m_levels.Length && offset >= 0 && offset < 1 << level);
			//TODO: check if level is allocated ?

			var segment = m_levels[level];
			Contract.Debug.Assert(segment != null && segment.Length == 1 << level);
			T previous = segment[offset];
			segment[offset] = value;
			return previous;
		}

		/// <summary>Clear the array</summary>
		public void Clear()
		{
			var levels = m_levels;
			for (int i = 0; i < levels.Length; i++)
			{
				if (i < MAX_SPARE_ORDER)
				{
					Array.Clear(levels[i], 0, 1 << i);
				}
				else
				{
					//note: array will be truncated at the end, we just want to help the GC by cutting the link to the array!
					levels[i] = default!;
				}
			}
			m_count = 0;
			if (levels.Length > MAX_SPARE_ORDER)
			{
				Array.Resize(ref m_levels, MAX_SPARE_ORDER);
			}

			CheckInvariants();
		}

		/// <summary>Add a value to the array</summary>
		/// <param name="value">Value to add to the array</param>
		/// <param name="overwriteExistingValue">If <paramref name="value"/> already exists in the array and <paramref name="overwriteExistingValue"/> is true, it will be overwritten with <paramref name="value"/></param>
		/// <returns>If the value did not  if the value was been added to the array, or false if it was already there.</returns>
		public bool SetOrAdd(T value, bool overwriteExistingValue)
		{
			int level = Find(value, out var offset, out _);
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
			if (IsFree(0))
			{ // half the inserts (when the count is even) can be done in the root
				m_root[0] = value;
			}
			else if (IsFree(1))
			{ // a quarter of the inserts only need to move the root and the value to level 1
				ColaStore.MergeSimple<T>(m_levels[1], m_root[0], value, m_comparer);
				m_root[0] = default!;
			}
			else
			{ // we need to merge one or more levels

				var spare = GetSpare(0);
#if DEBUG
				if (object.ReferenceEquals(spare, m_root) && System.Diagnostics.Debugger.IsAttached) System.Diagnostics.Debugger.Break();
#endif
				Contract.Debug.Assert(spare != null && spare.Length == 1);
				spare[0] = value;
				MergeCascade(1, m_root, spare);
				PutSpare(0, spare);
				m_root[0] = default!;
			}

			Interlocked.Increment(ref m_count);

			CheckInvariants();
		}

		/// <summary>Insert two elements in the set.</summary>
		public void InsertItems(T first, T second)
		{
			Contract.Debug.Requires(m_comparer.Compare(first, second) != 0, "Cannot insert the same value twice");

			if (IsFree(1))
			{
				ColaStore.MergeSimple<T>(m_levels[1], first, second, m_comparer);
			}
			else
			{
				//Console.WriteLine("InsertItems([2]) Cascade");
				var spare = GetSpare(1);
				spare[0] = first;
				spare[1] = second;
				var segment = m_levels[1];
				MergeCascade(2, segment, spare);
				segment[0] = default!;
				segment[1] = default!;
				PutSpare(1, spare);
			}

			Interlocked.Add(ref m_count, 2);

			CheckInvariants();
		}

		/// <summary>Insert one or more new elements in the set.</summary>
		/// <param name="values">Array of elements to insert. Warning: if a value already exist, the store will be corrupted !</param>
		/// <param name="ordered">If true, the entries in <paramref name="values"/> are guaranteed to already be sorted (using the store default comparer).</param>
		/// <remarks>The best performances are achieved when inserting a number of items that is a power of 2. The worst performances are when doubling the size of a store that is full.
		/// Warning: if <paramref name="ordered"/> is true but <paramref name="values"/> is not sorted, or is sorted using a different comparer, then the store will become corrupted !
		/// </remarks>
		public void InsertItems(List<T> values, bool ordered = false)
		{
			Contract.NotNull(values);

			int count = values.Count;
			T[] segment, spare;

			if (count < 2)
			{
				if (count == 1)
				{
					Insert(values[0]);
				}
				return;
			}

			if (count == 2)
			{
				if (IsFree(1))
				{
					segment = m_levels[1];
					if (ordered)
					{
						segment[0] = values[0];
						segment[1] = values[1];
					}
					else
					{
						ColaStore.MergeSimple<T>(segment, values[0], values[1], m_comparer);
					}
				}
				else
				{
					spare = GetSpare(1);
					spare[0] = values[0];
					spare[1] = values[1];
					segment = m_levels[1];
					MergeCascade(2, segment, spare);
					segment[0] = default!;
					segment[1] = default!;
					PutSpare(1, spare);
				}
			}
			else
			{
				// Inserting a size that is a power of 2 is very simple:
				// * either the corresponding level is empty, in that case we just copy the items and do a quicksort
				// * or it is full, then we just need to do a cascade merge
				// For non-power of 2s, we can split decompose them into a suite of power of 2s and insert them one by one

				int min = ColaStore.LowestBit(count);
				int max = ColaStore.HighestBit(count);

				if (max >= m_levels.Length)
				{ // we need to allocate new levels
					Grow(max);
				}

				int p = 0;
				for (int i = min; i <= max; i++)
				{
					if (ColaStore.IsFree(i, count)) continue;

					segment = m_levels[i];
					if (IsFree(i))
					{ // the target level is free, we can copy and sort in place
						values.CopyTo(p, segment, 0, segment.Length);
						if (!ordered) Array.Sort(segment, 0, segment.Length, m_comparer);
						p += segment.Length;
						Interlocked.Add(ref m_count, segment.Length);
					}
					else
					{ // the target level is used, we will have to do a cascade merge, using a spare
						spare = GetSpare(i);
						values.CopyTo(p, spare, 0, spare.Length);
						if (!ordered) Array.Sort(spare, 0, spare.Length, m_comparer);
						p += segment.Length;
						MergeCascade(i + 1, segment, spare);
						Array.Clear(segment, 0, segment.Length);
						PutSpare(i, spare);
						Interlocked.Add(ref m_count, segment.Length);
					}
				}
				Contract.Debug.Assert(p == count);
			}

			CheckInvariants();
		}

		/// <summary>Remove the value at the specified location</summary>
		/// <param name="arrayIndex">Absolute index in the vector-array</param>
		/// <returns>Value that was removed</returns>
		public T RemoveAt(int arrayIndex)
		{
			Contract.Debug.Requires(arrayIndex >= 0 && arrayIndex <= this.Capacity);
			int level = ColaStore.FromIndex(arrayIndex, out var offset);
			return RemoveAt(level, offset);
		}

		/// <summary>Remove the value at the specified location</summary>
		/// <param name="level">Index of the level (0-based)</param>
		/// <param name="offset">Offset in the level (0-based)</param>
		/// <returns>Value that was removed</returns>
		public T RemoveAt(int level, int offset)
		{
			Contract.Debug.Requires(level >= 0 && offset >= 0 && offset < 1 << level);
			//TODO: check if level is allocated ?

			var segment = m_levels[level];
			Contract.Debug.Assert(segment != null && segment.Length == 1 << level);
			T removed = segment[offset];

			if (level == 0)
			{ // removing the last inserted value
				segment[0] = default!;
			}
			else if (level == 1)
			{ // split the first level in two
				if (IsFree(0))
				{ // move up to root

					// ex: remove 'b' at (1,1) and move the 'a' back to the root
					// 0 [_]	=> [a]
					// 1 [a,b]	=> [_,_]

					m_root[0] = segment[1 - offset];
					segment[0] = default!;
					segment[1] = default!;
				}
				else
				{ // merge the root in missing spot

					// ex: remove 'b' at (1,1) and move the 'c' down a level
					//		  N = 3		N = 2
					//		0 [c]	=>	0 [_]
					//		1 [a,b]	=>	1 [a,c]

					ColaStore.MergeSimple<T>(segment, m_root[0], segment[1 - offset], m_comparer);
					m_root[0] = default!;
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
				m_root[0] = default!;
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

			Interlocked.Decrement(ref m_count);

			if (m_levels.Length > MAX_SPARE_ORDER)
			{ // maybe release the last level if it is empty
				ShrinkIfRequired();
			}

			CheckInvariants();

			return removed;
		}

		public bool RemoveItem(T item)
		{
			int level = Find(item, out var offset, out _);
			if (level < 0) return false;
			_ = RemoveAt(level, offset);
			CheckInvariants();
			return true;
		}

		public int RemoveItems(IEnumerable<T> items)
		{
			Contract.NotNull(items);

			int count = 0;

			//TODO: optimize this !!!!
			foreach (var item in items)
			{
				int level = Find(item, out var offset, out _);
				if (level >= 0)
				{
					RemoveAt(level, offset);
					++count;
				}

			}
			CheckInvariants();
			return count;
		}

		public void CopyTo(T[] array, int arrayIndex, int count)
		{
			Contract.NotNull(array);
			if (arrayIndex < 0) throw new ArgumentOutOfRangeException(nameof(arrayIndex), "Index cannot be less than zero.");
			if (count < 0) throw new ArgumentOutOfRangeException(nameof(count), "Count cannot be less than zero.");
			if (arrayIndex > array.Length || count > (array.Length - arrayIndex)) throw new ArgumentException("Destination array is too small");

			int p = arrayIndex;
			count = Math.Min(count, m_count);
			foreach (var item in ColaStore.IterateOrdered(count, m_levels, m_comparer, false))
			{
				array[p++] = item;
			}
			Contract.Debug.Ensures(p == arrayIndex + count);
		}

		/// <summary>Checks if a level is currently not allocated</summary>
		/// <param name="level">Index of the level (0-based)</param>
		/// <returns>True is the level is unallocated and does not store any elements; otherwise, false.</returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool IsFree(int level)
		{
			Contract.Debug.Requires(level >= 0);
			return (m_count & (1 << level)) == 0;
		}

		/// <summary>Gets a temporary buffer with the length corresponding to the specified level</summary>
		/// <param name="level">Level of this spare buffer</param>
		/// <returns>Temporary buffer whose size is 2^level</returns>
		/// <remarks>The buffer should be returned after use by calling <see cref="PutSpare"/></remarks>
		public T[] GetSpare(int level)
		{
			Contract.Debug.Requires(level >= 0 && m_spares != null);

			if (level < m_spares.Length)
			{ // this level is kept in the spare list

#if ENFORCE_INVARIANTS
				Contract.Debug.Assert(!m_spareUsed[level], "this spare is already in use!");
#endif

				var t = m_spares[level];
				if (t == null)
				{ // allocate a new one
					t = new T[1 << level];
					m_spares[level] = t;
				}
#if ENFORCE_INVARIANTS
				m_spareUsed[level] = true;
#endif
				return t;
			}
			else
			{ // this level is always allocated
				return new T[1 << level];
			}
		}

		/// <summary>Return a temporary buffer after use</summary>
		/// <param name="level">Level of the temporary buffer</param>
		/// <param name="spare"></param>
		/// <returns>True if the buffer has been cleared and returned to the spare list, false if it was discarded</returns>
		/// <remarks>Kept buffers are cleared to prevent values from being kept alive and not garbage collected.</remarks>
		public bool PutSpare(int level, T?[] spare)
		{
			Contract.Debug.Requires(level >= 0 && spare != null);

#if ENFORCE_INVARIANTS
			// make sure that we do not mix levels and spares
			for (int i = 0; i < m_levels.Length; i++)
			{
				if (object.ReferenceEquals(m_levels[i], spare)) Debugger.Break();
			}
#endif

			// only clear spares that are kept alive
			if (level < m_spares.Length)
			{
#if ENFORCE_INVARIANTS
				Contract.Debug.Assert(m_spareUsed[level], "this spare wasn't used");
#endif

				// clear it in case it holds onto dead values that could be garbage collected
				spare[0] = default!;
				if (level > 0)
				{
					spare[1] = default!;
					if (level > 1) Array.Clear(spare, 2, spare.Length - 2);
				}
#if ENFORCE_INVARIANTS
				m_spareUsed[level] = false;
#endif
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
				case 0: return default!;
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
				case 0: return default!;
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
		public bool TryGetBounds([MaybeNullWhen(false)] out T min, [MaybeNullWhen(false)] out T max)
		{
			switch (m_count)
			{
				case 0:
				{
					min = default!;
					max = default!;
					return false;
				}
				case 1:
				{
					min = m_root[0];
					max = min;
					return true;
				}
				case 2:
				{
					min = m_levels[1][0];
					max = m_levels[1][1];
					return true;
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
					return true;
				}
			}
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public Iterator GetIterator()
		{
			return new Iterator(this);
		}

		/// <summary>Pre-allocate memory in the store so that it can store a specified amount of items</summary>
		/// <param name="minimumRequired">Number of items that will be inserted in the store</param>
		public void EnsureCapacity(int minimumRequired)
		{
			int level = ColaStore.HighestBit(minimumRequired);
			if ((1 << level) < minimumRequired) ++level;

			if (level >= m_levels.Length)
			{
				Grow(level);
			}
		}

		#endregion

		private void MergeCascade(int level, T[] left, T[] right)
		{
			Contract.Debug.Requires(level > 0, "level");
			Contract.Debug.Requires(left != null && left.Length == (1 << (level - 1)), "left");
			Contract.Debug.Requires(right != null && right.Length == (1 << (level - 1)), "right");

			if (IsFree(level))
			{ // target level is empty

				if (level >= m_levels.Length) Grow(level);
				Contract.Debug.Assert(level < m_levels.Length);

				ColaStore.MergeSort(m_levels[level], left, right, m_comparer);
			}
			else if (IsFree(level + 1))
			{ // the next level is empty

				if (level + 1 >= m_levels.Length) Grow(level + 1);
				Contract.Debug.Assert(level + 1 < m_levels.Length);

				var spare = GetSpare(level);
				ColaStore.MergeSort(spare, left, right, m_comparer);
				var next = m_levels[level];
				ColaStore.MergeSort(m_levels[level + 1], next, spare, m_comparer);
				Array.Clear(next, 0, next.Length);
				PutSpare(level, spare);
			}
			else
			{ // both are full, need to do a cascade merge

				Contract.Debug.Assert(level < m_levels.Length);

				// merge N and N +1
				var spare = GetSpare(level);
				ColaStore.MergeSort(spare, left, right, m_comparer);

				// and cascade to N + 2 ...
				var next = m_levels[level];
				MergeCascade(level + 1, next, spare);
				Array.Clear(next, 0, next.Length);
				PutSpare(level, spare);
			}
		}

		/// <summary>Grow the capacity of the level array</summary>
		/// <param name="level">Minimum level required</param>
		private void Grow(int level)
		{
			Contract.Debug.Requires(level >= 0);

			// note: we want m_segments[level] to not be empty, which means there must be at least (level + 1) entries in the level array
			int current = m_levels.Length;
			int required = level + 1;
			Contract.Debug.Assert(current < required);

			var tmpSegments = m_levels;
			Array.Resize(ref tmpSegments, required);
			for (int i = current; i < required; i++)
			{
				tmpSegments[i] = new T[1 << i];
			}
			m_levels = tmpSegments;

			Contract.Debug.Ensures(m_levels != null && m_levels.Length > level);
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
				}
			}
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		internal IEnumerable<T> IterateOrdered(bool reverse = false)
		{
			return ColaStore.IterateOrdered(m_count, m_levels, m_comparer, reverse);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		internal IEnumerable<T> IterateUnordered()
		{
			return ColaStore.IterateUnordered(m_count, m_levels);
		}

		//TODO: remove or set to internal !
		[Conditional("DEBUG")]
		public void Debug_Dump(Func<T, string>? dump = null)
		{
#if DEBUG
			System.Diagnostics.Trace.WriteLine("> " + m_levels.Length + " levels:");
			for(int i = 0; i < m_levels.Length; i++)
			{
				string s = dump == null ? string.Join(", ", m_levels[i]) : string.Join(", ", m_levels[i].Select(dump));
				System.Diagnostics.Trace.WriteLine(string.Format(CultureInfo.InvariantCulture, "  - {0,2}|{1}: {2}", i, IsFree(i) ? "_" : "#", s));
			}
#if false
			System.Diagnostics.Trace.WriteLine("> " + m_spares.Length + " spares:");
			for (int i = 0; i < m_spares.Length; i++)
			{
				var spare = m_spares[i];
				System.Diagnostics.Trace.WriteLine(string.Format(CultureInfo.InvariantCulture, "> {0,2}: {1}", i, spare == null ? "<unallocated>" : String.Join(", ", spare)));
			}
#endif
			System.Diagnostics.Trace.WriteLine("> " + m_count + " items");
#endif
		}

		[DebuggerDisplay("Current={m_current}, Level={m_currentLevel} ({m_min})")]
		public sealed class Iterator
		{
			private const int DIRECTION_PREVIOUS = -1;
			private const int DIRECTION_SEEK = 0;
			private const int DIRECTION_NEXT = +1;

			private readonly T[][] m_levels;
			private readonly int m_count;
			private readonly IComparer<T> m_comparer;
			private readonly int[] m_cursors;
			private readonly int m_min;
			private T? m_current;
			private int m_currentLevel;
			private int m_direction;
#if DEBUG
			private ColaStore<T> m_parent; // usefull when troubleshooting to have the pointer to the parent!
#endif

			internal Iterator(ColaStore<T> store)
			{
				Contract.Debug.Requires(store != null);
				m_levels = store.m_levels;
				m_count = store.m_count;
				m_comparer = store.m_comparer;

				m_cursors = ColaStore.CreateCursors(m_count, out m_min);
#if DEBUG
				m_parent = store;
#endif
			}

			[Conditional("FULL_DEBUG")]
			private void Debug_Dump(string? label = null)
			{
#if FULL_DEBUG
				System.Diagnostics.Trace.WriteLine("* Cursor State: " + label);
				for (int i = m_min; i < m_cursors.Length; i++)
				{
					if (ColaStore.IsFree(i, m_count))
					{
						System.Diagnostics.Trace.WriteLine("  - L" + i + ": unallocated");
						continue;
					}

					int p = m_cursors[i];
					System.Diagnostics.Trace.WriteLine("  - L" + i + ": " + p + " [" + (1 << i) + "] = " + (p < 0 ? "<BEFORE>" : (p >= (1 << i)) ? "<AFTER>" : ("" + m_levels[i][p])));
				}
				System.Diagnostics.Trace.WriteLine(" > Current at " + m_currentLevel + " : " + m_current);
#endif
			}

			/// <summary>Set the cursor just before the first key in the store</summary>
			public void SeekBeforeFirst()
			{
				var cursors = m_cursors;
				cursors[m_min] = -1;
				for (int i = m_min + 1; i < cursors.Length; i++)
				{
					cursors[i] = 0;
				}
				m_currentLevel = m_min;
				m_current = default(T);
				m_direction = DIRECTION_SEEK;
			}

			/// <summary>Set the cursor just before the first key in the store</summary>
			public void SeekAfterLast()
			{
				var cursors = m_cursors;
				for (int i = m_min; i < cursors.Length; i++)
				{
					cursors[i] = 1 << i;
				}
				m_currentLevel = NOT_FOUND;
				m_current = default(T);
				m_direction = DIRECTION_SEEK;
			}

			/// <summary>Seek the cursor to the smallest key in the store</summary>
			public bool SeekFirst()
			{
				var min = default(T);
				int minLevel = NOT_FOUND;

				var cursors = m_cursors;
				var levels = m_levels;
				var cmp = m_comparer;
				var count = m_count;

				for (int i = m_min; i < cursors.Length; i++)
				{
					if (ColaStore.IsFree(i, count)) continue;

					cursors[i] = 0;
					var segment = levels[i];
					Contract.Debug.Assert(segment != null && segment.Length == 1 << i);
					if (minLevel < 0 || cmp.Compare(segment[0], min) < 0)
					{
						min = segment[0];
						minLevel = i;
					}
				}

				m_current = min;
				m_currentLevel = minLevel;
				m_direction = DIRECTION_SEEK;

				Debug_Dump("SeekFirst");

				return minLevel >= 0;
			}

			/// <summary>Seek the cursor to the largest key in the store</summary>
			public bool SeekLast()
			{
				var max = default(T);
				int maxLevel = NOT_FOUND;

				var cursors = m_cursors;

				for (int i = m_min; i < cursors.Length; i++)
				{
					if (ColaStore.IsFree(i, m_count)) continue;
					var segment = m_levels[i];
					Contract.Debug.Assert(segment != null && segment.Length == 1 << i);
					int pos = segment.Length - 1;
					cursors[i] = pos;
					if (maxLevel < 0 || m_comparer.Compare(segment[pos], max) > 0)
					{
						max = segment[segment.Length - 1];
						maxLevel = i;
					}
				}

				m_current = max;
				m_currentLevel = maxLevel;
				m_direction = DIRECTION_SEEK;

				Debug_Dump("SeekLast");

				return maxLevel >= 0;
			}

			/// <summary>Seek the iterator at the smallest value that is less (or equal) to the desired item</summary>
			/// <param name="item">Item to seek to</param>
			/// <param name="orEqual">When <see cref="item"/> exists: if <c>true</c>, then seek to this item. If <c>false</c>, seek to the previous entry.</param>
			public bool Seek(T item, bool orEqual)
			{
				// Goal: we want to find the item key itself (if it exists and orEqual==true), or the max key that is stricly less than item
				// We can use BinarySearch to look in each segment for where that key would be, but we have to compensate for the fact that BinarySearch looks for the smallest key that is greater than or equal to the search key.

				// Also, the iterator can be used to move:
				// - forward: from the current location, find the smallest key that is greater than the current cursor position
				// - backward: from the current location, find the largest key that is smaller than the current cursor position

				var max = default(T);
				int maxLevel = NOT_FOUND;
				bool exact = false;

				var cursors = m_cursors;
				var count = m_count;

				for (int i = m_min; i < cursors.Length; i++)
				{
					if (ColaStore.IsFree(i, count)) continue;

					var segment = m_levels[i];

					int pos = ColaStore.BinarySearch(segment, 0, segment.Length, item, m_comparer);

					if (pos >= 0)
					{ // we found a match in this segment

						if (orEqual)
						{ // the item exist and is allowed
							max = segment[pos];
							maxLevel = i;
							exact = true; // stop checking for the max in other levels
						}
						else
						{ // the previous value is by definition less than 'item'
							--pos;
						}
					}
					else
					{ // not in this segment

						pos = ~pos; // <- position of where item would be place in this segment == position of the first item that is larger than item
						// since segment[pos] > item, and item is not in segment, then segment[pos - 1] < item
						--pos;
					}

					// bound check

					if (pos < 0)
					{ // the value would be before this segment
						cursors[i] = 0;
					}
					else if (pos >= segment.Length)
					{ // the value would be after this segment
						cursors[i] = segment.Length;
					}
					else
					{
						cursors[i] = pos;
						if (!exact && (maxLevel < 0 || m_comparer.Compare(segment[pos], max) > 0))
						{
							max = segment[pos];
							maxLevel = i;
						}
					}
				}

				m_currentLevel = maxLevel;
				m_current = max;
				m_direction = DIRECTION_SEEK;
				Debug_Dump("Seek");
				return maxLevel >= 0;
			}

			/// <summary>Move the cursor the the smallest value that is greater than the current value</summary>
			public bool Next()
			{
				// invalid position, or no more values
				if (m_currentLevel < 0) return false;

				var cursors = m_cursors;
				var levels = m_levels;
				var count = m_count;

				var prev = m_current;
				var min = default(T);
				int minLevel = NOT_FOUND;
				int pos;

				if (m_direction >= DIRECTION_SEEK)
				{ // we know that the current position CANNOT be the next value, so increment that cursor
					cursors[m_currentLevel]++;
					Debug_Dump("Next:continue");
				}
				else
				{ // previous call was a Previous()
					// we know that the current is the largest value of all the current cursors. Since we want even larger than that, we have to increment ALL the cursors
					for (int i = m_min; i < cursors.Length; i++)
					{
						if (!ColaStore.IsFree(i, count) && ((pos = cursors[i]) < levels[i].Length))
						{
							cursors[i] = pos + 1;
						}
					}
					Debug_Dump("Next:reverse");
				}

				var cmp = m_comparer;
				for (int i = m_min; i < cursors.Length; i++)
				{
					if (ColaStore.IsFree(i, count)) continue;

					pos = cursors[i];
					if (pos < 0) continue; //??

					var segment = levels[i];

					var x = default(T);
					while(pos < segment.Length && cmp.Compare((x = segment[pos]), prev) < 0)
					{ // cannot be less than the previous value
						cursors[i] = ++pos;
					}
					if (pos >= segment.Length) continue;

					if (minLevel < 0 || cmp.Compare(x, min) < 0)
					{ // new minimum
						min = x;
						minLevel = i;
					}
				}

				m_current = min;
				m_currentLevel = minLevel;
				m_direction = DIRECTION_NEXT;
				return minLevel >= 0;
			}

			/// <summary>Move the cursor the the largest value that is smaller than the current value</summary>
			public bool Previous()
			{
				// invalid position, or no more values
				if (m_currentLevel < 0) return false;

				var cursors = m_cursors;
				var levels = m_levels;
				var count = m_count;

				var prev = m_current;
				var max = default(T);
				int pos;
				int maxLevel = NOT_FOUND;

				if (m_direction <= DIRECTION_SEEK)
				{ // we know that the current position CANNOT be the next value, so decrement that cursor
					cursors[m_currentLevel]--;
					Debug_Dump("Previous:continue");
				}
				else
				{ // previous call was a call to Seek(), or Next()
					// we know that the current is the smallest value of all the current cursors. Since we want even smaller than that, we have to decrement ALL the cursors
					for (int i = m_min; i < cursors.Length; i++)
					{
						if (!ColaStore.IsFree(i, count) && ((pos = cursors[i]) >= 0))
						{
							cursors[i] = pos - 1;
						}
					}
					Debug_Dump("Previous:reverse");
				}

				var cmp = m_comparer;
				for (int i = m_min; i < cursors.Length; i++)
				{
					if (ColaStore.IsFree(i, count)) continue;

					pos = cursors[i];
					var segment = levels[i];
					if (pos >= segment.Length) continue; //??

					var x = default(T);
					while (pos >= 0 && cmp.Compare((x = segment[pos]), prev) > 0)
					{ // cannot be more than the previous value
						cursors[i] = --pos;
					}
					if (pos < 0) continue;

					if (maxLevel < 0 || cmp.Compare(x, max) > 0)
					{ // new maximum
						max = x;
						maxLevel = i;
					}
				}

				m_current = max;
				m_currentLevel = maxLevel;
				m_direction = DIRECTION_PREVIOUS;
				return maxLevel >= 0;
			}

			/// <summary>Value of the current entry</summary>
			public T? Current => m_current;

			/// <summary>Checks if the current position of the iterator is valid</summary>
			public bool Valid => m_currentLevel >= 0;

			/// <summary>Direction of the last operation</summary>
			public int Direction => m_direction;

			/// <summary>Current position of the cursor</summary>
			/// <remarks>This can be used to efficiently "remove" an item if we already know its location</remarks>
			internal (int Level, int Offset) Position => (m_currentLevel, m_cursors[m_currentLevel]);

		}

	}

}
