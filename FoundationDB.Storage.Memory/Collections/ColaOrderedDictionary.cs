#region Copyright Doxense 2013
//
// All rights are reserved. Reproduction or transmission in whole or in part, in
// any form or by any means, electronic, mechanical or otherwise, is prohibited
// without the prior written consent of the copyright owner.
//
#endregion

namespace FoundationDB.Storage.Memory.Core
{
	using System;
	using System.Collections.Generic;
	using System.Diagnostics;
	using System.Linq;

	/// <summary>Represent an ordered set of key/value pairs, stored in a Cache Oblivious Lookahead Array</summary>
	/// <typeparam name="TKey">Type of ordered keys stored in the dictionary.</typeparam>
	/// <typeparam name="TValue">Type of values stored in the dictionary.</typeparam>
	[DebuggerDisplay("Count={m_items.Count}"), DebuggerTypeProxy(typeof(ColaOrderedDictionary<,>.DebugView))]
	public class ColaOrderedDictionary<TKey, TValue> : IEnumerable<KeyValuePair<TKey, TValue>>
	{

		/// <summary>Debug view helper</summary>
		private sealed class DebugView
		{
			private ColaOrderedDictionary<TKey, TValue> m_dictionary;

			public DebugView(ColaOrderedDictionary<TKey, TValue> dictionary)
			{
				m_dictionary = dictionary;
			}

			[DebuggerBrowsable(DebuggerBrowsableState.RootHidden)]
			public KeyValuePair<TKey, TValue>[] Items
			{
				get
				{
					var tmp = new KeyValuePair<TKey, TValue>[m_dictionary.Count];
					m_dictionary.CopyTo(tmp, 0);
					return tmp;
				}
			}
		}

		/// <summary>Wrapper for a comparer on the keys of a key/value pair</summary>
		private sealed class KeyOnlyComparer : IComparer<KeyValuePair<TKey, TValue>>
		{
			private readonly IComparer<TKey> m_comparer;

			public KeyOnlyComparer(IComparer<TKey> comparer)
			{
				m_comparer = comparer;
			}

			public int Compare(KeyValuePair<TKey, TValue> x, KeyValuePair<TKey, TValue> y)
			{
				return m_comparer.Compare(x.Key, y.Key);
			}
		}

		/// <summary>COLA array used to store the entries in the dictionary</summary>
		private readonly ColaStore<KeyValuePair<TKey, TValue>> m_items;

		/// <summary>Comparer for the keys of the dictionary</summary>
		private readonly IComparer<TKey> m_keyComparer;

		/// <summary>Comparer for the values of the dictionary</summary>
		private readonly IEqualityComparer<TValue> m_valueComparer;

		#region Constructors...

		public ColaOrderedDictionary(IComparer<TKey> keyComparer = null, IEqualityComparer<TValue> valueComparer = null)
			: this(0, keyComparer, valueComparer)
		{ }

		public ColaOrderedDictionary(int capacity)
			: this(capacity, null, null)
		{ }

		public ColaOrderedDictionary(int capacity, IComparer<TKey> keyComparer, IEqualityComparer<TValue> valueComparer)
		{
			m_keyComparer = keyComparer ?? Comparer<TKey>.Default;
			m_valueComparer = valueComparer ?? EqualityComparer<TValue>.Default;
			m_items = new ColaStore<KeyValuePair<TKey, TValue>>(capacity, new KeyOnlyComparer(m_keyComparer));
		}

		#endregion

		#region Public Properties...

		public int Count
		{
			get { return m_items.Count; }
		}

		public int Capacity
		{
			get { return m_items.Capacity; }
		}

		#endregion

		public void Clear()
		{
			m_items.Clear();
		}

		public IComparer<TKey> KeyComparer
		{
			get { return m_keyComparer; }
		}

		public IEqualityComparer<TValue> ValueComparer
		{
			get { return m_valueComparer; }
		}

		internal ColaStore<KeyValuePair<TKey, TValue>> Items
		{
			get { return m_items; }
		}

		/// <summary>Adds an entry with the specified key and value to the sorted dictionary.</summary>
		/// <param name="key">The key of the entry to add.</param>
		/// <param name="value">The value of the entry to add.</param>
		/// <exception cref="System.InvalidOperationException">If an entry with the same key already exist in the dictionary.</exception>
		public void Add(TKey key, TValue value)
		{
			if (!m_items.SetOrAdd(new KeyValuePair<TKey, TValue>(key, value), overwriteExistingValue: false))
			{
				throw new InvalidOperationException("An entry with the same key but a different value already exists.");
			}
		}

		/// <summary>Sets the specified key and value in the immutable sorted dictionary, possibly overwriting an existing value for the given key.</summary>
		/// <param name="key">The key of the entry to add.</param>
		/// <param name="value">The key value to set.</param>
		public void SetItem(TKey key, TValue value)
		{
			m_items.SetOrAdd(new KeyValuePair<TKey, TValue>(key, value), overwriteExistingValue: true);
		}

		/// <summary>Try to add an entry with the specified key and value to the sorted dictionary, or update its value if it already exists.</summary>
		/// <param name="key">The key of the entry to add.</param>
		/// <param name="value">The value of the entry to add.</param>
		/// <returns>true if the key did not previously exist and was inserted; otherwise, false.</returns>
		public bool AddOrUpdate(TKey key, TValue value)
		{
			KeyValuePair<TKey, TValue> entry;
			int offset, level = m_items.Find(new KeyValuePair<TKey, TValue>(key, default(TValue)), out offset, out entry);
			if (level >= 0)
			{ // already exists
				// keep the old key, and update the value
				m_items.SetAt(level, offset, new KeyValuePair<TKey, TValue>(entry.Key, value));
				return false;
			}

			m_items.Insert(new KeyValuePair<TKey, TValue>(key, value));
			return true;
		}

		/// <summary>Try to add an entry with the specified key and value to the sorted dictionary, if it does not already exists.</summary>
		/// <param name="key">The key of the entry to add.</param>
		/// <param name="value">The value of the entry to add.</param>
		/// <returns>true if the key did not previously exist and was inserted; otherwise, false.</returns>
		public bool GetOrAdd(TKey key, TValue value, out TValue actualValue)
		{
			KeyValuePair<TKey, TValue> entry;
			int _, level = m_items.Find(new KeyValuePair<TKey, TValue>(key, default(TValue)), out _, out entry);
			if (level >= 0)
			{ // already exists
				actualValue = entry.Value;
				return false;
			}

			m_items.Insert(new KeyValuePair<TKey, TValue>(key, value));
			actualValue = value;
			return true;
		}

		public bool ContainsKey(TKey key)
		{
			int _;
			KeyValuePair<TKey, TValue> __;
			return m_items.Find(new KeyValuePair<TKey, TValue>(key, default(TValue)), out _, out __) >= 0;
		}

		public bool ContainsValue(TValue value)
		{
			foreach(var kvp in m_items.IterateUnordered())
			{
				if (m_valueComparer.Equals(kvp.Value)) return true;
			}
			return false;
		}

		/// <summary>Determines whether this dictionary contains a specified key.</summary>
		/// <param name="equalKey">The key to search for.</param>
		/// <param name="actualKey">The matching key located in the dictionary if found, or equalkey if no match is found.</param>
		/// <returns>true if a match for <paramref name="equalKey"/> is found; otherwise, false.</returns>
		public bool TryGetKey(TKey equalKey, out TKey actualKey)
		{
			KeyValuePair<TKey, TValue> entry;
			int _, level = m_items.Find(new KeyValuePair<TKey, TValue>(equalKey, default(TValue)), out _, out entry);
			if (level < 0)
			{
				actualKey = equalKey;
				return false;
			}
			actualKey = entry.Key;
			return true;
		}

		/// <summary>Gets the value associated with the specified key.</summary>
		/// <param name="key">The key to search for.</param>
		/// <param name="value"></param>
		/// <returns>true if a match for <paramref name="key"/> is found; otherwise, false.</returns>
		public bool TryGetValue(TKey key, out TValue value)
		{
			KeyValuePair<TKey, TValue> entry;
			int _, level = m_items.Find(new KeyValuePair<TKey, TValue>(key, default(TValue)), out _, out entry);
			if (level < 0)
			{
				value = default(TValue);
				return false;
			}
			value = entry.Value;
			return true;
		}

		/// <summary>Gets the existing key and value associated with the specified key.</summary>
		/// <param name="key">The key to search for.</param>
		/// <param name="entry">The matching key and value pair located in the dictionary if found.</param>
		/// <returns>true if a match for <paramref name="key"/> is found; otherwise, false.</returns>
		public bool TryGetKeyValue(TKey key, out KeyValuePair<TKey, TValue> entry)
		{
			int _, level = m_items.Find(new KeyValuePair<TKey, TValue>(key, default(TValue)), out _, out entry);
			return level >= 0;
		}

		/// <summary>Removes the entry with the specified key from the dictionary.</summary>
		/// <param name="key">The key of the entry to remove.</param>
		/// <returns>true if the value was found and removed from the dictionary; otherwise, false.</returns>
		/// <remarks>It is NOT allowed to remove keys while iterating on the dictionary at the same time!</remarks>
		public bool Remove(TKey key)
		{
			KeyValuePair<TKey, TValue> _;
			int offset, level = m_items.Find(new KeyValuePair<TKey, TValue>(key, default(TValue)), out offset, out _);

			if (level >= 0)
			{
				m_items.RemoveAt(level, offset);
				return true;
			}
			return false;
		}

		/// <summary>Remove the entries with the specified keys from the dictionary.</summary>
		/// <param name="keys">The keys of the entries to remove.</param>
		/// <returns>Number of entries that were found and removed.</returns>
		/// <remarks>It is NOT allowed to remove keys while iterating on the dictionary at the same time!</remarks>
		public int RemoveRange(IEnumerable<TKey> keys)
		{
			if (keys == null) throw new ArgumentNullException("keys");

			// we need to protect against people passing in the result of calling FindBetween,
			// because we can't remove while iterating at the same time !

			int count = 0;
			foreach (var key in keys)
			{
				if (Remove(key)) ++count;
			}
			return count;
		}

		/// <summary>Enumerate all the keys in the dictionary that are in the specified range</summary>
		/// <param name="begin">Start of the range</param>
		/// <param name="beginOrEqual">If true, the <paramref name="begin"/> key is included in the range</param>
		/// <param name="end">End of the range</param>
		/// <param name="endOrEqual">If true, the <paramref name="end"/> key is included in the range</param>
		/// <returns>Unordered list of the all the keys in the dictionary that are in the range.</returns>
		/// <remarks>There is no guarantee in the actual order of the keys returned. It is also not allowed to remove keys while iterating over the sequence.</remarks>
		public IEnumerable<TKey> FindBetween(TKey begin, bool beginOrEqual, TKey end, bool endOrEqual)
		{
			// return the unordered list of all the keys that are between the begin/end pair.
			// each bound is included in the list if its corresponding 'orEqual' is set to true

			if (m_items.Count > 0)
			{
				var start = new KeyValuePair<TKey, TValue>(begin, default(TValue));
				var stop = new KeyValuePair<TKey, TValue>(end, default(TValue));

				foreach (var kvp in m_items.FindBetween(start, beginOrEqual, stop, endOrEqual, int.MaxValue))
				{
					yield return kvp.Key;
				}
			}
		}

		/// <summary>Returns an enumerator that iterates through the ordered dictionary</summary>
		public ColaStore.Enumerator<KeyValuePair<TKey, TValue>> GetEnumerator()
		{
			return new ColaStore.Enumerator<KeyValuePair<TKey, TValue>>(m_items, reverse: false);
		}

		IEnumerator<KeyValuePair<TKey, TValue>> IEnumerable<KeyValuePair<TKey, TValue>>.GetEnumerator()
		{
			return this.GetEnumerator();
		}

		System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
		{
			return this.GetEnumerator();
		}

		internal void CopyTo(KeyValuePair<TKey, TValue>[] array, int index)
		{
			m_items.CopyTo(array, index, m_items.Count);
		}

		//TODO: remove or set to internal !
		public void Debug_Dump()
		{
			Console.WriteLine("Dumping ColaOrderedDictionary<" + typeof(TKey).Name + ", " + typeof(TValue).Name + "> filled at " + (100.0d * this.Count / this.Capacity).ToString("N2") + "%");
			m_items.Debug_Dump();
		}

	}
}
