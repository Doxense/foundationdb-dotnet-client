#region Copyright (c) 2013-2014, Doxense SAS. All rights reserved.
// See License.MD for license information
#endregion

namespace FoundationDB.Storage.Memory.Core
{
	using System;
	using System.Collections.Generic;
	using System.Diagnostics.Contracts;
	using System.Runtime.InteropServices;

	/// <summary>Represent an ordered set of elements, stored in a Cache Oblivous Lookup Array</summary>
	/// <typeparam name="T">Type of elements stored in the set</typeparam>
	/// <remarks>Inserts are in O(LogN) amortized. Lookups are in O(Log(N))</remarks>
	public class ColaOrderedSet<T> : IEnumerable<T>
	{
		private const int NOT_FOUND = -1;

		/// <summary>COLA array used to store the elements in the set</summary>
		private readonly ColaStore<T> m_items;

		private volatile int m_version;

		#region Constructors...

		public ColaOrderedSet()
			: this(0, Comparer<T>.Default)
		{ }

		public ColaOrderedSet(int capacity)
			: this(capacity, Comparer<T>.Default)
		{ }

		public ColaOrderedSet(IComparer<T> comparer)
			: this(0, comparer)
		{ }

		public ColaOrderedSet(int capacity, IComparer<T> comparer)
		{
			if (capacity < 0) throw new ArgumentOutOfRangeException("capacity", "Capacity cannot be less than zero.");
			Contract.EndContractBlock();

			m_items = new ColaStore<T>(capacity, comparer ?? Comparer<T>.Default);
		}

		#endregion

		#region Public Properties...

		/// <summary>Gets the number of elements in the immutable sorted set.</summary>
		public int Count
		{
			get { return m_items.Count; }
		}

		/// <summary>Current capacity of the set</summary>
		public int Capacity
		{
			get { return m_items.Capacity; }
		}

		public IComparer<T> Comparer
		{
			get { return m_items.Comparer; }
		}

		public T this[int index]
		{
			get
			{
				if (index < 0 || index >= m_items.Count) throw new IndexOutOfRangeException("Index is out of range");
				int offset;
				int level = ColaStore.MapOffsetToLocation(m_items.Count, index, out offset);
				Contract.Assert(level >= 0);
				return m_items.GetAt(level, offset);
			}
		}

		#endregion

		#region Public Methods...

		public void Clear()
		{
			++m_version;
			m_items.Clear();
		}

		/// <summary>Adds the specified value to this ordered set.</summary>
		/// <param name="value">The value to add.</param>
		/// <remarks>If the value already exists in the set, it will not be overwritten</remarks>
		public bool Add(T value)
		{
			++m_version;
			if (!m_items.SetOrAdd(value, overwriteExistingValue: false))
			{
				--m_version;
				return false;
			}
			return true;
		}

		/// <summary>Adds or overwrite the specified value to this ordered set.</summary>
		/// <param name="value">The value to add.</param>
		/// <remarks>If the value already exists in the set, it will be overwritten by <paramref name="value"/></remarks>
		public bool Set(T value)
		{
			++m_version;
			return m_items.SetOrAdd(value, overwriteExistingValue: true);
		}

		public bool TryRemove(T value, out T actualValue)
		{
			int offset;
			int level = m_items.Find(value, out offset, out actualValue);
			if (level != NOT_FOUND)
			{
				++m_version;
				m_items.RemoveAt(level, offset);
				return true;
			}
			return false;
		}

		public bool Remove(T value)
		{
			T _;
			return TryRemove(value, out _);
		}

		public T RemoveAt(int arrayIndex)
		{
			if (arrayIndex < 0 || arrayIndex >= m_items.Count) throw new ArgumentOutOfRangeException("arrayIndex", "Index is outside the array");

			int offset;
			int level = ColaStore.MapOffsetToLocation(m_items.Count, arrayIndex, out offset);
			Contract.Assert(level >= 0 && offset >= 0 && offset < 1 << level);

			++m_version;
			return m_items.RemoveAt(level, offset);
		}

		/// <summary>Determines whether this immutable sorted set contains the specified value.</summary>
		/// <param name="value">The value to check for.</param>
		/// <returns>true if the set contains the specified value; otherwise, false.</returns>
		public bool Contains(T value)
		{
			int _;
			T __;
			return m_items.Find(value, out _, out __) >= 0;
		}

		/// <summary>Find an element </summary>
		/// <param name="value"></param>
		/// <returns>The zero-based index of the first occurrence of <paramref name="value"/> within the entire list, if found; otherwise, –1.</returns>
		public int IndexOf(T value)
		{
			T _;
			int offset, level = m_items.Find(value, out offset, out _);
			if (level >= 0)
			{
				return ColaStore.MapLocationToOffset(m_items.Count, level, offset);
			}
			return NOT_FOUND;
		}

		/// <summary>Searches the set for a given value and returns the equal value it finds, if any.</summary>
		/// <param name="value">The value to search for.</param>
		/// <param name="actualValue">The value from the set that the search found, or the original value if the search yielded no match.</param>
		/// <returns>A value indicating whether the search was successful.</returns>
		public bool TryGetValue(T value, out T actualValue)
		{
			int _;
			return m_items.Find(value, out _, out actualValue) >= 0;
		}

		/// <summary>Copy the ordered elements of the set to an array</summary>
		/// <param name="array">The one-dimensional array that is the destination of the elements copied from collection. The array must have zero-based indexing.</param>
		public void CopyTo(T[] array)
		{
			Contract.Requires(array != null);
			m_items.CopyTo(array, 0, array.Length);
		}

		/// <summary>Copies the ordered elements of the set to an array, starting at a particular array index.</summary>
		/// <param name="array">The one-dimensional array that is the destination of the elements copied from collection. The array must have zero-based indexing.</param>
		/// <param name="arrayIndex">The zero-based index in array at which copying begins.</param>
		public void CopyTo(T[] array, int arrayIndex)
		{
			Contract.Requires(array != null && arrayIndex >= 0);
			m_items.CopyTo(array, arrayIndex, m_items.Count);
		}

		public void CopyTo(T[] array, int arrayIndex, int count)
		{
			Contract.Requires(array != null && arrayIndex >= 0 && count >= 0);
			m_items.CopyTo(array, arrayIndex, count);
		}

		public ColaStore.Enumerator<T> GetEnumerator()
		{
			return new ColaStore.Enumerator<T>(m_items, reverse: false);
		}

		IEnumerator<T> IEnumerable<T>.GetEnumerator()
		{
			return this.GetEnumerator();
		}

		System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
		{
			return this.GetEnumerator();
		}

		#endregion

		//TODO: remove or set to internal !
		public void Debug_Dump()
		{
			Console.WriteLine("Dumping ColaOrderedSet<" + typeof(T).Name + "> filled at " + (100.0d * this.Count / this.Capacity).ToString("N2") + "%");
			m_items.Debug_Dump();
		}

		[StructLayout(LayoutKind.Sequential)]
		public struct Enumerator : IEnumerator<T>, IDisposable
		{
			private const int NOT_FOUND = -1;

			private readonly int m_version;
			private readonly ColaOrderedSet<T> m_parent;
			private ColaStore.Enumerator<T> m_iterator;

			internal Enumerator(ColaOrderedSet<T> parent, bool reverse)
			{
				m_version = parent.m_version;
				m_parent = parent;
				m_iterator = new ColaStore.Enumerator<T>(parent.m_items, reverse);
			}

			public bool MoveNext()
			{
				if (m_version != m_parent.m_version)
				{
					ColaStore.ThrowStoreVersionChanged();
				}

				return m_iterator.MoveNext();
			}

			public T Current
			{
				get { return m_iterator.Current; }
			}

			public void Dispose()
			{
				// we are a struct that can be copied by value, so there is no guarantee that Dispose() will accomplish anything anyway...
			}

			object System.Collections.IEnumerator.Current
			{
				get { return m_iterator.Current; }
			}

			void System.Collections.IEnumerator.Reset()
			{
				if (m_version != m_parent.m_version)
				{
					ColaStore.ThrowStoreVersionChanged();
				}
				m_iterator = new ColaStore.Enumerator<T>(m_parent.m_items, m_iterator.Reverse);
			}

		}

	}

}
