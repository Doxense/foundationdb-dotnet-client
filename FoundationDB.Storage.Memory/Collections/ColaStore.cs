#region Copyright (c) 2013-2014, Doxense SAS. All rights reserved.
// See License.MD for license information
#endregion

namespace FoundationDB.Storage.Memory.Core
{
	using System;
	using System.Collections.Generic;
	using System.Diagnostics;
	using System.Diagnostics.Contracts;
	using System.Runtime.InteropServices;

	public static class ColaStore
	{
		private const int NOT_FOUND = -1;

		private static readonly int[] MultiplyDeBruijnLowestBitPosition = new int[32]
		{
			0, 1, 28, 2, 29, 14, 24, 3, 30, 22, 20, 15, 25, 17, 4, 8, 
			31, 27, 13, 23, 21, 19, 16, 7, 26, 12, 18, 6, 11, 5, 10, 9
		};

		private static readonly int[] MultiplyDeBruijnHighestBitPosition = new int[32]
		{
			0, 9, 1, 10, 13, 21, 2, 29, 11, 14, 16, 18, 22, 25, 3, 30,
			8, 12, 20, 28, 15, 17, 24, 7, 19, 27, 23, 6, 26, 5, 4, 31
		};

		internal static bool IsFree(int level, int count)
		{
			Contract.Requires(level >= 0 && count >= 0);
			return (count & (1 << level)) == 0;
		}

		internal static bool IsAllocated(int level, int count)
		{
			Contract.Requires(level >= 0 && count >= 0);
			return (count & (1 << level)) != 0;
		}

		/// <summary>Finds the level that holds an absolute index</summary>
		/// <param name="index">Absolute index in a COLA array where 0 is the root, 1 is the first item of level 1, and so on</param>
		/// <param name="offset">Receive the offset in the level that contains <paramref name="index"/> is located</param>
		/// <returns>Level that contains the specified location.</returns>
		public static int FromIndex(int index, out int offset)
		{
			Contract.Requires(index >= 0);

			int level = HighestBit(index);
			offset = index - (1 << level) + 1;
			Contract.Ensures(level >= 0 && level < 31 && offset >= 0 && offset < (1 << level));
			return level;
		}

		/// <summary>Convert a (level, offset) pair into the corresponding absolute index</summary>
		/// <param name="level">Level of the location (0 for the root)</param>
		/// <param name="offset">Offset within the level of the location</param>
		/// <returns>Absolute index where 0 is the root, 1 is the first item of level 1, and so on</returns>
		public static int ToIndex(int level, int offset)
		{
			Contract.Requires(level >= 0 && level < 31 && offset >= 0 && offset < (1 << level));
			int index = (1 << level) - 1 + offset;
			Contract.Ensures(index >= 0 && index < 1 << level);
			return index;
		}

		public static int LowestBit(int value)
		{
			uint v = (uint)value;
			v = (uint)((v & -v) * 0x077CB531U);

			return MultiplyDeBruijnLowestBitPosition[v >> 27];
		}

		public static int HighestBit(int value)
		{
			// first round down to one less than a power of 2 
			uint v = (uint)value;
			v |= v >> 1;
			v |= v >> 2;
			v |= v >> 4;
			v |= v >> 8;
			v |= v >> 16;

			return MultiplyDeBruijnHighestBitPosition[(int)((v * 0x07C4ACDDU) >> 27)];
		}

		/// <summary>Computes the absolute index from a value offset (in the allocated levels)</summary>
		/// <param name="count">Number of items in the COLA array</param>
		/// <param name="arrayIndex">Offset of the value in the allocated levels of the COLA array, with 0 being the oldest (first item of the last allocated level)</param>
		/// <returns>Absolute index of the location where that value would be stored in the COLA array (from the top)</returns>
		public static int MapOffsetToIndex(int count, int arrayIndex)
		{
			Contract.Requires(count >= 0 && arrayIndex >= 0 && arrayIndex < count);

			int offset;
			int level = MapOffsetToLocation(count, arrayIndex, out offset);
			return (1 << level) - 1 + offset;
		}

		/// <summary>Computes the (level, offset) pair from a value offset (in the allocated levels)</summary>
		/// <param name="count">Number of items in the COLA array</param>
		/// <param name="arrayIndex">Offset of the value in the allocated levels of the COLA array, with 0 being the oldest (first item of the last allocated level)</param>
		/// <returns>Absolute index of the location where that value would be stored in the COLA array (from the top)</returns>
		public static int MapOffsetToLocation(int count, int arrayIndex, out int offset)
		{
			if (count < 0) throw new ArgumentOutOfRangeException("count", "Count cannot be less than zero");
			if (arrayIndex < 0 || arrayIndex >= count) throw new ArgumentOutOfRangeException("arrayIndex", "Index is outside the array");

			if (count == 0)
			{ // special case for the empty array
				offset = 0;
				return 0;
			}

			// find the highest allocated level (note: 50% of values will be in this segment!)
			int level = HighestBit(count);
			int k = 1 << level;
			int p = k - 1;
			do
			{
				if ((count & k) != 0)
				{ // this level is allocated
					if (arrayIndex < k)
					{
						offset = arrayIndex;
						return level;
					}
					arrayIndex -= k;
				}
				k >>= 1;
				--level;
				p -= k;
			}
			while (k > 0);

			// should not happen !
			throw new InvalidOperationException();
		}

		public static int MapLocationToOffset(int count, int level, int offset)
		{
			Contract.Assert(count >= 0 && level >= 0 && offset >= 0 && offset < 1 << level);
			if (count < 0) throw new ArgumentOutOfRangeException("count", "Count cannot be less than zero");

			if (count == 0)
			{ // special case for the empty array
				return 0;
			}

			// compute the base location of the selected level
			int p = 0;
			int k = 1;
			for (int i = 0; i < level; i++)
			{
				if ((count & k) != 0)
				{
					p += k;
				}
				k <<= 1;
			}

			return p + offset;
		}

		internal static void ThrowDuplicateKey<T>(T value)
		{
			throw new InvalidOperationException("Cannot insert because the key already exists in the set");
		}

		internal static int BinarySearch<T>(T[] array, int offset, int count, T value, IComparer<T> comparer)
		{
			Contract.Assert(array != null && offset >= 0 && count >= 0 && comparer != null);

			// Instead of starting from the midle we will exploit the fact that, since items are usually inserted in order, the value is probably either to the left or the right of the segment.
			// Also, since most activity happens in the top levels, the search array is probably very small (size 1, 2 or 4)

			if (count == 0)
			{
				// note: there should be no array of size 0, this is probably a bug !
				return ~offset;
			}

			int end = offset - 1 + count;
			int c;

			// compare with the last item
			c = comparer.Compare(array[end], value);
			if (c == 0) return end;
			if (count == 1)
			{
				return c < 0 ? ~(offset + 1) : ~offset;
			}
			if (c < 0) return ~(end + 1);
			--end;

			// compare with the first
			c = comparer.Compare(array[offset], value);
			if (c == 0) return offset;
			if (c > 0) return ~offset;

			int cursor = offset + 1;
			while (cursor <= end)
			{
				int center = cursor + ((end - cursor) >> 1);
				c = comparer.Compare(array[center], value);
				if (c == 0)
				{ // the value is the center point
					return center;
				}
				if (c < 0)
				{ // the value is after the center point
					cursor = center + 1;
				}
				else
				{ // the value is before the center point
					end = center - 1;
				}
			}
			return ~cursor;
		}

		/// <summary>Merge two values into level 1</summary>
		/// <param name="segment">Segment for level 1 (should be of size 2)</param>
		/// <param name="left">Left value</param>
		/// <param name="right">Right value</param>
		/// <param name="comparer">Comparer to use</param>
		internal static void MergeSimple<T>(T[] segment, T left, T right, IComparer<T> comparer)
		{
			Contract.Requires(segment != null && segment.Length == 2);

			int c = comparer.Compare(left, right);
			if (c == 0) ThrowDuplicateKey(right);
			else if (c < 0)
			{
				segment[0] = left;
				segment[1] = right;
			}
			else
			{
				segment[0] = right;
				segment[1] = left;
			}
		}

		/// <summary>Replace a value in a segment with another value, while keeping it sorted</summary>
		/// <param name="segment">Segment that will received the new value</param>
		/// <param name="offset">Offset of replaced value in the segment</param>
		/// <param name="value">New value to insert into the segment</param>
		/// <param name="comparer">Comparer to use</param>
		internal static void MergeInPlace<T>(T[] segment, int offset, T value, IComparer<T> comparer)
		{
			Contract.Requires(segment != null && offset >= 0 && comparer != null);

			// Find the spot where the new value should be inserted
			int p = BinarySearch(segment, 0, segment.Length, value, comparer);
			if (p >= 0)
			{ // this is not supposed to happen!
				ThrowDuplicateKey(value);
			}

			int index = (~p);
			Contract.Assert(index >= 0 && index <= segment.Length);
			if (index == offset)
			{ // merge in place

				//                _______ offset == index
				//				 V
				// before: [...] X [...] 
				// after:  [...] O [...]

				segment[index] = value;
				return;
			}
			if (index < offset)
			{ // shift right

				//                 ____________ index
				//                /     _______ offset
				//				 V     V
				// before: [...] # # # X [...] 
				// after:  [...] O # # # [...]

				Array.Copy(segment, index, segment, index + 1, offset - index);
				segment[index] = value;
			}
			else
			{ // shift left

				--index;

				//                 ____________ offset
				//                /     _______ index
				//				 V     V
				// before: [...] X # # # [...] 
				// after:  [...] # # # O [...]

				Array.Copy(segment, offset + 1, segment, offset, index - offset);
				segment[index] = value;
			}
		}

		/// <summary>Spread the content of a level to all the previous levels into pieces, except the first item that is returned</summary>
		/// <param name="level">Level that should be broken into chunks</param>
		/// <param name="inputs">List of all the levels</param>
		/// <returns>The last element of the broken level</returns>
		/// <remarks>The broken segment will be cleared</remarks>
		internal static T SpreadLevel<T>(int level, T[][] inputs)
		{
			Contract.Requires(level >= 0 && inputs != null && inputs.Length > level);

			// Spread all items in the target level - except the first - to the previous level (which should ALL be EMPTY)

			var source = inputs[level];

			int p = 1;
			for (int i = level - 1; i >= 0; i--)
			{
				var segment = inputs[i];
				Contract.Assert(segment != null);
				int n = segment.Length;
				Array.Copy(source, p, segment, 0, n);
				p += n;
			}
			Contract.Assert(p == source.Length);
			T res = source[0];
			Array.Clear(source, 0, source.Length);
			return res;
		}

		/// <summary>Merge two ordered segments of level N into an ordered segment of level N + 1</summary>
		/// <param name="output">Destination, level N + 1 (size 2^(N+1))</param>
		/// <param name="left">First level N segment (size 2^N)</param>
		/// <param name="right">Second level N segment (taille 2^N)</param>
		/// <param name="comparer">Comparer used for the merge</param>
		internal static void MergeSort<T>(T[] output, T[] left, T[] right, IComparer<T> comparer)
		{
			Contract.Requires(output != null && left != null && right != null && comparer != null);
			Contract.Requires(left.Length > 0 && output.Length == left.Length * 2 && right.Length == left.Length);

			int n = left.Length;
			// note: The probality to merge an array of size N is rougly 1/N (with N being a power of 2),
			// which means that we will spend roughly half the time merging arrays of size 1 into an array of size 2..

			if (n == 1)
			{ // Most frequent case (p=0.5)
				var l = left[0];
				var r = right[0];
				if (comparer.Compare(l, r) < 0)
				{
					output[0] = l;
					output[1] = r;
				}
				else
				{
					output[0] = r;
					output[1] = l;
				}
				return;
			}

			if (n == 2)
			{ // second most frequent case (p=0.25)

				// We are merging 2 pairs of ordered values into an array of size 4
				if (comparer.Compare(left[1], right[0]) < 0)
				{ // left << right
					output[0] = left[0];
					output[1] = left[1];
					output[2] = right[0];
					output[3] = right[1];
					return;
				}

				if (comparer.Compare(right[1], left[0]) < 0)
				{ // right << left
					output[0] = right[0];
					output[1] = right[1];
					output[2] = left[0];
					output[3] = left[1];
					return;
				}

				// left and right intersects
				// => just use the regular merge sort below
			}

			int pLeft = 0;
			int pRight = 0;
			int pOutput = 0;

			while (true)
			{
				if (comparer.Compare(left[pLeft], right[pRight]) < 0)
				{ // left is smaller than right => advance

					output[pOutput++] = left[pLeft++];

					if (pLeft >= n)
					{ // the left array is done, copy the remainder of the right array
						if (pRight < n) Array.Copy(right, pRight, output, pOutput, n - pRight);
						return;
					}
				}
				else
				{ // right is smaller or equal => advance

					output[pOutput++] = right[pRight++];

					if (pRight >= n)
					{ // the right array is done, copy the remainder of the left array
						if (pLeft < n) Array.Copy(left, pLeft, output, pOutput, n - pLeft);
						return;
					}
				}
			}

		}

		internal static int[] CreateCursors(int count, out int min)
		{
			min = LowestBit(count);
			var cursors = new int[HighestBit(count) + 1];
			int k = 1;
			for (int i = 0; i < cursors.Length; i++)
			{
				if (i < min || (count & k) == 0) cursors[i] = NOT_FOUND;
				k <<= 1;
			}
			return cursors;
		}

		/// <summary>Find the next smallest key pointed by a list of cursors</summary>
		/// <param name="inputs">List of source arrays</param>
		/// <param name="cursors">Lit of cursors in source arrays</param>
		/// <param name="comparer">Key comparer</param>
		/// <param name="result">Received the next smallest element if the method returns true; otherwise set to default(T)</param>
		/// <returns>The index of the level that returned the value, or -1 if all levels are done</returns>
		internal static int IterateFindNext<T>(T[][] inputs, int[] cursors, int min, int max, IComparer<T> comparer, out T result)
		{
			Contract.Requires(inputs != null && cursors != null && min >= 0 && max >= min && comparer != null);
			//Trace.WriteLine("IterateFindNext(" + min + ".." + max + ")");

			int index = NOT_FOUND;
			int pos = NOT_FOUND;
			var next = default(T);

			// look for the smallest element
			// note: we scan from the bottom up, because older items are usually in the lower levels
			for (int i = max; i >= min; i--)
			{
				int cursor = cursors[i];
				if (cursor < 0) continue;
				var segment = inputs[i];
				if (cursor >= segment.Length) continue;
				var x = segment[cursor];
				if (index == NOT_FOUND || comparer.Compare(x, next) < 0)
				{ // found a candidate
					index = i;
					pos = cursor;
					next = x;
				}
			}

			if (index != NOT_FOUND)
			{
				++pos;
				if (pos >= (1 << index))
				{ // this array is done
					pos = NOT_FOUND;
				}
				cursors[index] = pos;
				result = next;
				return index;
			}

			result = default(T);
			return NOT_FOUND;
		}

		/// <summary>Find the next largest key pointed by a list of cursors</summary>
		/// <param name="inputs">List of source arrays</param>
		/// <param name="cursors">Lit of cursors in source arrays</param>
		/// <param name="comparer">Key comparer</param>
		/// <param name="result">Received the next largest element if the method returns true; otherwise set to default(T)</param>
		/// <returns>The index of the level that returned the value, or -1 if all levels are done</returns>
		internal static int IterateFindPrevious<T>(T[][] inputs, int[] cursors, int min, int max, IComparer<T> comparer, out T result)
		{
			Contract.Requires(inputs != null && cursors != null && min >= 0 && max >= min && comparer != null);
			// NOT TESTED !!!!!
			// NOT TESTED !!!!!
			// NOT TESTED !!!!!

			//Trace.WriteLine("IterateFindPrevious(" + min + ".." + max + ")");

			int index = NOT_FOUND;
			int pos = NOT_FOUND;
			var next = default(T);

			// look for the largest element
			// note: we scan from the top down, because more recent items are usually in the upper levels
			for (int i = min; i >= max; i--)
			{
				int cursor = cursors[i];
				if (cursor < 0) continue;
				var segment = inputs[i];
				if (cursor >= segment.Length) continue;
				var x = segment[cursor];
				if (index == NOT_FOUND || comparer.Compare(x, next) < 0)
				{ // found a candidate
					index = i;
					pos = cursor;
					next = x;
				}
			}

			if (index != NOT_FOUND)
			{
				--pos;
				if (pos < 0)
				{ // this array is done
					pos = NOT_FOUND;
				}
				cursors[index] = pos;
				result = next;
				return index;
			}

			result = default(T);
			return NOT_FOUND;
		}

		/// <summary>Iterate over all the values in the set, using their natural order</summary>
		internal static IEnumerable<T> IterateOrdered<T>(int count, T[][] inputs, IComparer<T> comparer, bool reverse)
		{
			Contract.Requires(count >= 0 && inputs != null && comparer != null && count < (1 << inputs.Length));
			// NOT TESTED !!!!!
			// NOT TESTED !!!!!
			// NOT TESTED !!!!!

			Contract.Requires(count >= 0 && inputs != null && comparer != null);

			// We will use a list of N cursors, set to the start of their respective levels.
			// A each turn, look for the smallest key referenced by the cursors, return that one, and advance its cursor.
			// Once a cursor is past the end of its level, it is set to -1 and is ignored for the rest of the operation

			if (count > 0)
			{
				// setup the cursors, with the empty levels already marked as completed
				var cursors = new int[inputs.Length];
				for (int i = 0; i < cursors.Length; i++)
				{
					if (ColaStore.IsFree(i, count))
					{
						cursors[i] = NOT_FOUND;
					}
				}

				// pre compute the first/last active level
				int min = ColaStore.LowestBit(count);
				int max = ColaStore.HighestBit(count);

				while (count-- > 0)
				{
					T item;
					int pos;
					if (reverse)
					{
						pos = IterateFindPrevious(inputs, cursors, min, max, comparer, out item);
					}
					else
					{
						pos = IterateFindNext(inputs, cursors, min, max, comparer, out item);
					}

					if (pos == NOT_FOUND)
					{ // we unexpectedly ran out of stuff before the end ?
						//TODO: should we fail or stop here ?
						throw new InvalidOperationException("Not enough data in the source arrays to fill the output array");
					}
					yield return item;

					// update the bounds if needed
					if (pos == max)
					{
						if (cursors[max] == NOT_FOUND) --max;
					}
					else if (pos == min)
					{
						if (cursors[min] == NOT_FOUND) ++min;
					}
				}
			}
		}

		/// <summary>Iterate over all the values in the set, without any order guarantee</summary>
		internal static IEnumerable<T> IterateUnordered<T>(int count, T[][] inputs)
		{
			Contract.Requires(count >= 0 && inputs != null && count < (1 << inputs.Length));

			for (int i = 0; i < inputs.Length; i++)
			{
				if (ColaStore.IsFree(i, count)) continue;
				var segment = inputs[i];
				Contract.Assert(segment != null && segment.Length == 1 << i);
				for (int j = 0; j < segment.Length; j++)
				{
					yield return segment[j];
				}
			}
		}

		internal static void ThrowStoreVersionChanged()
		{
			throw new InvalidOperationException("The version of the store has changed. This usually means that the collection has been modified while it was being enumerated");
		}

		[StructLayout(LayoutKind.Sequential)]
		public struct Enumerator<T> : IEnumerator<T>, IDisposable
		{
			private readonly ColaStore<T> m_items;
			private readonly bool m_reverse;
			private int[] m_cursors;
			private T m_current;
			private int m_min;
			private int m_max;

			internal Enumerator(ColaStore<T> items, bool reverse)
			{
				m_items = items;
				m_reverse = reverse;
				m_cursors = ColaStore.CreateCursors(m_items.Count, out m_min);
				m_max = m_cursors.Length - 1;
				m_current = default(T);
			}

			public bool MoveNext()
			{
				int pos;
				if (m_reverse)
				{
					pos = ColaStore.IterateFindPrevious(m_items.Levels, m_cursors, m_min, m_max, m_items.Comparer, out m_current);
				}
				else
				{
					pos = ColaStore.IterateFindNext(m_items.Levels, m_cursors, m_min, m_max, m_items.Comparer, out m_current);
				}

				if (pos == NOT_FOUND)
				{ // that was the last item!
					return false;
				}

				// update the bounds if necessary
				if (pos == m_max)
				{
					if (m_cursors[m_max] == NOT_FOUND) --m_max;
				}
				else if (pos == m_min)
				{
					if (m_cursors[m_min] == NOT_FOUND) ++m_min;
				}

				return true;
			}

			public T Current
			{
				get { return m_current; }
			}

			public bool Reverse
			{
				get { return m_reverse; }
			}

			public void Dispose()
			{
				// we are a struct that can be copied by value, so there is no guarantee that Dispose() will accomplish anything anyway...
			}

			object System.Collections.IEnumerator.Current
			{
				get { return m_current; }
			}

			void System.Collections.IEnumerator.Reset()
			{
				m_cursors = ColaStore.CreateCursors(m_items.Count, out m_min);
				m_max = m_cursors.Length - 1;
				m_current = default(T);
			}

		}

		public sealed class Iterator<T>
		{
			private const int DIRECTION_PREVIOUS = -1;
			private const int DIRECTION_SEEK = 0;
			private const int DIRECTION_NEXT = +1;

			private readonly T[][] m_levels;
			private readonly int m_count;
			private readonly IComparer<T> m_comparer;
			private readonly int[] m_cursors;
			private readonly int m_min;
			private T m_current;
			private int m_currentLevel;
			private int m_direction;

			internal Iterator(T[][] levels, int count, IComparer<T> comparer)
			{
				Contract.Requires(levels != null && count >= 0 && comparer != null);
				m_levels = levels;
				m_count = count;
				m_comparer = comparer;

				m_cursors = ColaStore.CreateCursors(m_count, out m_min);
			}

			[Conditional("FULLDEBUG")]
			private void Debug_Dump(string label = null)
			{
				Trace.WriteLine("* Cursor State: " + label); 
				for (int i = m_min; i < m_cursors.Length; i++)
				{
					if (ColaStore.IsFree(i, m_count))
					{
						Trace.WriteLine("  - L" + i + ": unallocated");
						continue;
					}

					int p = m_cursors[i];
					Trace.WriteLine("  - L" + i + ": " + p + " [" + (1 << i) + "] = " + (p < 0 ? "<BEFORE>" : (p >= (1 << i)) ? "<AFTER>" : ("" + m_levels[i][p])));
				}
				Trace.WriteLine(" > Current at " + m_currentLevel + " : " + m_current);
			}

			/// <summary>Set the cursor just before the first key in the store</summary>
			public void SeekBeforeFirst()
			{
				var cursors = m_cursors;
				for (int i = m_min; i < cursors.Length; i++)
				{
					cursors[i] = -1;
				}
				m_currentLevel = NOT_FOUND;
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
				T min = default(T);
				int minLevel = NOT_FOUND;

				var cursors = m_cursors;

				for (int i = m_min; i < cursors.Length; i++)
				{
					if (IsFree(i, m_count)) continue;

					cursors[i] = 0;
					var segment = m_levels[i];
					Contract.Assert(segment != null && segment.Length == 1 << i);
					if (minLevel < 0 || m_comparer.Compare(segment[0], min) < 0)
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
				T max = default(T);
				int maxLevel = NOT_FOUND;

				var cursors = m_cursors;

				for (int i = m_min; i < cursors.Length; i++)
				{
					if (IsFree(i, m_count)) continue;
					var segment = m_levels[i];
					Contract.Assert(segment != null && segment.Length == 1 << i);
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



			/// <summary>Seek the iterator at the smallest value that is closest to the desired item</summary>
			/// <param name="item">Item to seek to</param>
			/// <param name="orEqual">If true, then seek to this item is found. If false, seek to the previous value</param>
			/// <param name="reverse">If true, the cursors are setup for moving backward (by calling Previous). Is false, the cursors are set up for moving forward (by calling Next)</param>
			public bool Seek(T item, bool orEqual)
			{
				// Goal: we want to find the item key itself (if it exists and orEqual==true), or the max key that is stricly less than item
				// We can use BinarySearch to look in each segment for where that key would be, but we have to compensate for the fact that BinarySearch looks for the smallest key that is greater than or equal to the search key.

				// Also, the iterator can be used to move:
				// - forward: from the current location, find the smallest key that is greater than the current cursor position
				// - backward: from the current location, find the largest key that is smaller than the current cursor position

				T max = default(T);
				int maxLevel = NOT_FOUND;
				bool exact = false;

				var cursors = m_cursors;
				var count = m_count;

				for (int i = m_min; i < cursors.Length; i++)
				{
					if (IsFree(i, count)) continue;

					var segment = m_levels[i];

					int pos = BinarySearch(segment, 0, segment.Length, item, m_comparer);

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
						if (!exact && (m_min < 0 || m_comparer.Compare(segment[pos], max) > 0))
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
				var count = m_count;

				T prev = m_current;
				T min = default(T);
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
						if (!IsFree(i, count) && ((pos = cursors[i]) < m_levels[i].Length)) cursors[i] = pos + 1;
					}
					Debug_Dump("Next:reverse");
				}

				for (int i = m_min; i < cursors.Length; i++)
				{
					if (IsFree(i, count)) continue;

					pos = cursors[i];
					if (pos < 0) continue; //??

					var segment = m_levels[i];

					T x = default(T);
					while(pos < segment.Length && m_comparer.Compare((x = segment[pos]), prev) < 0)
					{ // cannot be less than the previous value
						cursors[i] = ++pos;
					}
					if (pos >= segment.Length) continue;

					if (minLevel < 0 || m_comparer.Compare(x, min) < 0)
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
				var count = m_count;

				T prev = m_current;
				T max = default(T);
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
						if (!IsFree(i, count) && ((pos = cursors[i]) >= 0)) cursors[i] = pos - 1;
					}
					Debug_Dump("Previous:reverse");
				}

				for (int i = m_min; i < cursors.Length; i++)
				{
					if (IsFree(i, count)) continue;

					pos = cursors[i];
					var segment = m_levels[i];
					if (pos >= segment.Length) continue; //??

					T x = default(T);
					while (pos >= 0 && m_comparer.Compare((x = segment[pos]), prev) > 0)
					{ // cannot be more than the previous value
						cursors[i] = --pos;
					}
					if (pos < 0) continue;

					if (maxLevel < 0 || m_comparer.Compare(x, max) > 0)
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

#if false
			/// <summary>Seek the iterator at the smallest value that is closest to the desired item</summary>
			/// <param name="item">Item to seek to</param>
			/// <param name="orEqual">If true, then seek to this item is found. If false, seek to the previous value</param>
			/// <param name="reverse">If true, the cursors are setup for moving backward (by calling Previous). Is false, the cursors are set up for moving forward (by calling Next)</param>
			public void Seek(T item, bool orEqual)
			{
				//Trace.WriteLine("# Seeking to " + item + " (orEqual = " + orEqual + ")");

				var cursors = m_cursors;

				T max = default(T);
				int maxLevel = NOT_FOUND;

				Debug_Dump("Seek:prepare");

				bool exact = false;
				for (int i = m_min; i <= m_max; i++)
				{
					if (ColaStore.IsFree(i, m_count)) continue;

					var segment = m_levels[i];
					int pos = ColaStore.BinarySearch<T>(segment, 0, segment.Length, item, m_comparer);
					//Trace.WriteLine("  > binsearch L" + i + " => " + pos + " (~" + (~pos) + ")");
					if (pos >= 0)
					{ // exact match!
						if (orEqual)
						{
							exact = true;
							cursors[i] = pos + 1;
							max = segment[pos];
							maxLevel = i;
							continue;
						}

					}
					else
					{
						pos = ~pos;
					}

					--pos;

					// note: the binary search points to the position where it would go, if it was found,
					// but the selectors want the PREVIOUS version

					cursors[i] = pos;
					if (pos >= 0 && pos < segment.Length)
					{
						if (!exact && (maxLevel == NOT_FOUND || m_comparer.Compare(segment[pos], max) > 0))
						{
							max = segment[pos];
							maxLevel = i;
						}
					}
				}

				if (!exact)
				{
					Debug_Dump("Seek:before_tweak");
					// we did not find an exact match, meaning that we found the closest candidate, and all the other cursors point to values less than that
					for (int i = m_min; i <= m_max; i++)
					{
						if (cursors[i] < (1 << i) && i != maxLevel) ++cursors[i];
					}
				}

				m_current = max;
				m_currentLevel = maxLevel;
				Debug_Dump("Seek:done");
			}
#endif

			/// <summary>Value of the current entry</summary>
			public T Current
			{
				get { return m_current; }
			}

			/// <summary>Checks if the current position of the iterator is valid</summary>
			public bool Valid
			{
				get { return m_currentLevel >= 0; }
			}

			/// <summary>Direction of the last operation</summary>
			public int Direction
			{
				get { return m_direction; }
			}

#if false
			[Obsolete("DOES NOT WORK")]
			public bool Next()
			{

				//Trace.WriteLine("# Looking for next between levels " + m_min + " and " + m_max);
				T min = default(T);
				int minLevel = NOT_FOUND;

				// cursors:
				// -1 = we are before the first item
				// 2^L = we are after the last item

				var count = m_count;
				var cursors = m_cursors;

				// first, we need to increment the last position
				if (m_currentLevel >= 0)
				{
					cursors[m_currentLevel]++;
				}

				bool hasIdleSegment = false;

				for (int i = m_min; i <= m_max; i++)
				{
					if (ColaStore.IsFree(i, count))
					{
						//Trace.WriteLine("  > level " + i + " is unallocated");
						continue;
					}

					int pos = cursors[i];
					var segment = m_levels[i];
					if (pos >= segment.Length)
					{ // we have passed the end of this one
						//Trace.WriteLine("  > level " + i + " is done");
						continue;
					}

					if (pos < 0)
					{ // we still haven't entered this one
						hasIdleSegment = true;
						//Trace.WriteLine("  > level " + i + " is not opened yet");
						continue;
					}

					// it is active!
					//Trace.WriteLine("  > level " + i + " is at pos " + pos);
					var x = segment[pos];
					int c = minLevel < 0 ? -1 : m_comparer.Compare(x, min);
					if (c < 0)
					{
						min = x;
						minLevel = i;
					}
				}

				if (minLevel >= 0)
				{ // we have found the next item
					//Trace.WriteLine("  > and the winner is level " + minLevel + " !");
					m_current = min;
					m_currentLevel = minLevel;
					Debug_Dump("Next:found");
					return true;
				}

				if (hasIdleSegment)
				{ // did not find anthing, but there were some unopened segments, open them and try again

					//Trace.WriteLine("  > not all hope is lost, there are still opened levels!");
					for (int i = m_min; i < m_max; i++)
					{
						if (!IsFree(i, count) && cursors[i] == -1)
						{
							//Trace.WriteLine("    > opening level " + i);
							cursors[i] = 0;
						}
					}
					Debug_Dump("Next:opened");

					return Next();
				}

				//Trace.WriteLine("  > nope :(");

				// all segments are done, we have reached the end
				m_current = default(T);
				m_currentLevel = NOT_FOUND;
				Debug_Dump("Next:done");
				return false;
			}

			[Obsolete("DOES NOT WORK")]
			public void ReverseDirection()
			{
				var cursors = m_cursors;
				var count = m_count;

				for (int i = m_min; i < m_max; i++)
				{
					if (ColaStore.IsFree(i, count) || i == m_currentLevel) continue;

					int p = cursors[i];
					//if (p == 1 << i) cursors[i] = p - 1;
					if (p >= 0) cursors[i] = p - 1;
				}

				Debug_Dump("ReverseDirection:done");
			}

			[Obsolete("DOES NOT WORK")]
			public bool Previous()
			{

				//Trace.WriteLine("# Looking for previous between levels " + m_min + " and " + m_max);
				T max = default(T);
				int maxLevel = NOT_FOUND;

				// cursors:
				// -1 = we are before the first item
				// 2^L = we are after the last item

				var count = m_count;
				var cursors = m_cursors;

				// first, we need to decrement the last position
				if (m_currentLevel >= 0)
				{
					cursors[m_currentLevel]--;
				}

				bool hasIdleSegment = false;

				for (int i = m_min; i <= m_max; i++)
				{
					if (ColaStore.IsFree(i, count))
					{
						//Trace.WriteLine("  > level " + i + " is unallocated");
						continue;
					}

					int pos = cursors[i];
					if (pos < 0)
					{ // we have passed the start of this one
						//Trace.WriteLine("  > level " + i + " is done");
						continue;
					}

					var segment = m_levels[i];
					if (pos >= segment.Length)
					{ // we still haven't entered this one
						hasIdleSegment = true;
						//Trace.WriteLine("  > level " + i + " is not opened yet");
						continue;
					}

					// it is active!
					//Trace.WriteLine("  > level " + i + " is at pos " + pos);
					var x = segment[pos];
					int c = maxLevel < 0 ? +1 : m_comparer.Compare(x, max);
					if (c > 0)
					{
						max = x;
						maxLevel = i;
					}
				}

				if (maxLevel >= 0)
				{ // we have found the previous item
					//Trace.WriteLine("  > and the winner is level " + minLevel + " !");
					m_current = max;
					m_currentLevel = maxLevel;
					Debug_Dump("Previous:found");
					return true;
				}

				if (hasIdleSegment)
				{ // did not find anything, but there were some unopened segments, open them and try again

					//Trace.WriteLine("  > not all hope is lost, there are still opened levels!");
					for (int i = m_min; i < m_max; i++)
					{
						if (!IsFree(i, count) && cursors[i] == 1 << i)
						{
							//Trace.WriteLine("    > opening level " + i);
							cursors[i]--;
						}
					}

					Debug_Dump("Previous:opened");

					return Next();
				}

				//Trace.WriteLine("  > nope :(");

				// all segments are done, we have reached the end
				m_current = default(T);
				m_currentLevel = NOT_FOUND;
				Debug_Dump("Previous:notfound");
				return false;
			}

#endif

		}

	}
}