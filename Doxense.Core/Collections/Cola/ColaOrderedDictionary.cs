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

namespace Doxense.Collections.Generic
{
	using System;
	using System.Collections.Generic;
	using System.Diagnostics;
	using System.Diagnostics.CodeAnalysis;
	using System.Runtime.CompilerServices;
	using System.Runtime.InteropServices;
	using System.Threading;
	using Doxense.Diagnostics.Contracts;
	using JetBrains.Annotations;

	/// <summary>Represent an ordered set of key/value pairs, stored in a Cache Oblivious Lookahead Array</summary>
	/// <typeparam name="TKey">Type of ordered keys stored in the dictionary.</typeparam>
	/// <typeparam name="TValue">Type of values stored in the dictionary.</typeparam>
	[PublicAPI]
	[DebuggerDisplay("Count={m_items.Count}"), DebuggerTypeProxy(typeof(ColaOrderedDictionary<,>.DebugView))]
	public class ColaOrderedDictionary<TKey, TValue> : IEnumerable<KeyValuePair<TKey, TValue>>
	{

		/// <summary>Debug view helper</summary>
		private sealed class DebugView
		{
			private readonly ColaOrderedDictionary<TKey, TValue> m_dictionary;

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

		private volatile int m_version;

		#region Constructors...

		public ColaOrderedDictionary(IComparer<TKey>? keyComparer = null, IEqualityComparer<TValue>? valueComparer = null)
			: this(0, keyComparer, valueComparer)
		{ }

		public ColaOrderedDictionary(int capacity)
			: this(capacity, null, null)
		{ }

		public ColaOrderedDictionary(int capacity, IComparer<TKey>? keyComparer, IEqualityComparer<TValue>? valueComparer)
		{
			m_keyComparer = keyComparer ?? Comparer<TKey>.Default;
			m_valueComparer = valueComparer ?? EqualityComparer<TValue>.Default;
			m_items = new ColaStore<KeyValuePair<TKey, TValue>>(capacity, new KeyOnlyComparer(m_keyComparer));
		}

		public ColaOrderedDictionary(ColaOrderedDictionary<TKey, TValue> copy)
		{
			m_keyComparer = copy.m_keyComparer;
			m_valueComparer = copy.m_valueComparer;
			m_items = copy.m_items.Copy();
		}

		public ColaOrderedDictionary<TKey, TValue> Copy() => new(this);

		#endregion

		#region Public Properties...

		public int Count => m_items.Count;

		public int Capacity => m_items.Capacity;

		public TValue this[TKey key]
		{
			get => GetValue(key);
			set => SetItem(key, value);
		}

		#endregion

		public void Clear()
		{
			Interlocked.Increment(ref m_version);
			m_items.Clear();
		}

		public IComparer<TKey> KeyComparer => m_keyComparer;

		public IEqualityComparer<TValue> ValueComparer => m_valueComparer;

		internal ColaStore<KeyValuePair<TKey, TValue>> Items => m_items;

		/// <summary>Adds an entry with the specified key and value to the sorted dictionary.</summary>
		/// <param name="key">The key of the entry to add.</param>
		/// <param name="value">The value of the entry to add.</param>
		/// <exception cref="System.InvalidOperationException">If an entry with the same key already exist in the dictionary.</exception>
		public void Add(TKey key, TValue value)
		{
			Contract.NotNullAllowStructs(key);

			Interlocked.Increment(ref m_version);
			if (!m_items.SetOrAdd(new KeyValuePair<TKey, TValue>(key, value), overwriteExistingValue: false))
			{
				Interlocked.Decrement(ref m_version);
				throw ErrorKeyAlreadyExists();
			}
		}

		/// <summary>Sets the specified key and value in the immutable sorted dictionary, possibly overwriting an existing value for the given key.</summary>
		/// <param name="key">The key of the entry to add.</param>
		/// <param name="value">The key value to set.</param>
		public void SetItem(TKey key, TValue value)
		{
			Contract.NotNullAllowStructs(key);
			Interlocked.Increment(ref m_version);
			m_items.SetOrAdd(new KeyValuePair<TKey, TValue>(key, value), overwriteExistingValue: true);
		}

		/// <summary>Try to add an entry with the specified key and value to the sorted dictionary, or update its value if it already exists.</summary>
		/// <param name="key">The key of the entry to add.</param>
		/// <param name="value">The value of the entry to add.</param>
		/// <returns>true if the key did not previously exist and was inserted; otherwise, false.</returns>
		public bool AddOrUpdate(TKey key, TValue value)
		{
			Contract.NotNullAllowStructs(key);

			int level = m_items.Find(new KeyValuePair<TKey, TValue>(key, default!), out int offset, out var entry);
			if (level >= 0)
			{ // already exists
				// keep the old key, and update the value
				Interlocked.Increment(ref m_version);
				m_items.SetAt(level, offset, new KeyValuePair<TKey, TValue>(entry.Key, value));
				return false;
			}

			Interlocked.Increment(ref m_version);
			m_items.Insert(new KeyValuePair<TKey, TValue>(key, value));
			return true;
		}

		/// <summary>Try to add an entry with the specified key and value to the sorted dictionary, if it does not already exist.</summary>
		/// <param name="key">The key of the entry to add.</param>
		/// <param name="value">The value of the entry to add.</param>
		/// <param name="actualValue">Receives the previous value if <paramref name="key"/> already exists, or <paramref name="value"/> if it was inserted</param>
		/// <returns>true if the key did not previously exist and was inserted; otherwise, false.</returns>
		public bool GetOrAdd(TKey key, TValue value, out TValue actualValue)
		{
			Contract.NotNullAllowStructs(key);

			int level = m_items.Find(new KeyValuePair<TKey, TValue>(key, default!), out _, out var entry);
			if (level >= 0)
			{ // already exists
				actualValue = entry.Value;
				return false;
			}

			Interlocked.Increment(ref m_version);
			m_items.Insert(new KeyValuePair<TKey, TValue>(key, value));
			actualValue = value;
			return true;
		}

		public bool ContainsKey(TKey key)
		{
			Contract.NotNullAllowStructs(key);

			return m_items.Find(new KeyValuePair<TKey, TValue>(key, default!), out _, out _) >= 0;
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
		/// <param name="actualKey">The matching key located in the dictionary if found, or <paramref name="equalKey"/> if no match is found.</param>
		/// <returns>true if a match for <paramref name="equalKey"/> is found; otherwise, false.</returns>
		public bool TryGetKey(TKey equalKey, out TKey actualKey)
		{
			Contract.NotNullAllowStructs(equalKey);

			int level = m_items.Find(new KeyValuePair<TKey, TValue>(equalKey, default!), out _, out var entry);
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
		public bool TryGetValue(TKey key, [MaybeNullWhen(false)] out TValue value)
		{
			Contract.NotNullAllowStructs(key);

			int level = m_items.Find(new KeyValuePair<TKey, TValue>(key, default!), out _, out var entry);
			if (level < 0)
			{
				value = default!;
				return false;
			}
			value = entry.Value;
			return true;
		}

		[Pure]
		public TValue GetValue(TKey key)
		{
			Contract.NotNullAllowStructs(key);

			int level = m_items.Find(new KeyValuePair<TKey, TValue>(key, default!), out _, out var entry);
			if (level < 0)
			{
				throw ErrorKeyNotFound();
			}
			return entry.Value;
		}

		/// <summary>Gets the existing key and value associated with the specified key.</summary>
		/// <param name="key">The key to search for.</param>
		/// <param name="entry">The matching key and value pair located in the dictionary if found.</param>
		/// <returns>true if a match for <paramref name="key"/> is found; otherwise, false.</returns>
		public bool TryGetKeyValue(TKey key, out KeyValuePair<TKey, TValue> entry)
		{
			Contract.NotNullAllowStructs(key);

			int level = m_items.Find(new KeyValuePair<TKey, TValue>(key, default!), out _, out entry);
			return level >= 0;
		}

		/// <summary>Removes the entry with the specified key from the dictionary.</summary>
		/// <param name="key">The key of the entry to remove.</param>
		/// <returns>true if the value was found and removed from the dictionary; otherwise, false.</returns>
		/// <remarks>It is NOT allowed to remove keys while iterating on the dictionary at the same time!</remarks>
		public bool Remove(TKey key)
		{
			Contract.NotNullAllowStructs(key);

			int level = m_items.Find(new KeyValuePair<TKey, TValue>(key, default!), out int offset, out _);

			if (level >= 0)
			{
				Interlocked.Increment(ref m_version);
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
			Contract.NotNull(keys);

			// we need to protect against people passing in the result of calling FindBetween,
			// because we can't remove while iterating at the same time !

			int count = 0;
			foreach (var key in keys)
			{
				if (Remove(key)) ++count;
			}
			return count;
		}

		public IEnumerable<TKey> IterateAndRemoveRange(TKey begin, bool beginEqual, TKey end, bool endEqual)
		{
			// This is the worst case scenario for COLA: this operation is VERY SLOW!
			// => It should be optimized, but for this it would need to modify the levels directly

			var kvNext = new KeyValuePair<TKey, TValue>(begin, default!);
			var cmp = m_keyComparer;

			do
			{
				int level = m_items.FindNext(kvNext, beginEqual, out var offset, out var candidate);
				int p = cmp.Compare(candidate.Key, end);
				if (level < 0 || endEqual ? (p > 0) : (p >= 0))
				{
					break;
				}

				yield return candidate.Key;
				m_items.RemoveAt(level, offset);
				if (!endEqual && p == 0)
				{ // prevent one extra search operation!
					break;
				}
			}
			while (true);
		}

		public bool Lookup(TKey key, bool orEqual, out KeyValuePair<TKey, TValue> item)
		{
			return -1 != m_items.FindNext(new KeyValuePair<TKey, TValue>(key, default!), orEqual, out _, out item);
		}

		/// <summary>Enumerate all the keys in the dictionary that are in the specified range</summary>
		/// <param name="begin">Start of the range</param>
		/// <param name="beginOrEqual">If true, the <paramref name="begin"/> key is included in the range</param>
		/// <param name="end">End of the range</param>
		/// <param name="endOrEqual">If true, the <paramref name="end"/> key is included in the range</param>
		/// <returns>Unordered list of the all the keys in the dictionary that are in the range.</returns>
		/// <remarks>There is no guarantee in the actual order of the keys returned. It is also not allowed to remove keys while iterating over the sequence.</remarks>
		public IEnumerable<KeyValuePair<TKey, TValue>> Scan(TKey begin, bool beginOrEqual, TKey end, bool endOrEqual)
		{
			// return the unordered list of all the keys that are between the begin/end pair.
			// each bound is included in the list if its corresponding 'orEqual' is set to true

			if (m_items.Count > 0)
			{
				var iter = m_items.GetIterator();
				if (!iter.Seek(new KeyValuePair<TKey, TValue>(begin, default!), beginOrEqual))
				{ // starts before
					iter.SeekFirst();
				}

				var cmp = m_keyComparer;
				do
				{
					int p = cmp.Compare(iter.Current.Key, end);
					if (endOrEqual ? (p > 0) : (p >= 0))
					{
						yield break;
					}

					yield return iter.Current;
				}
				while (iter.Next());
			}
		}

		/// <summary>Enumerate all the keys in the dictionary that are in the specified range</summary>
		/// <param name="begin">Start of the range</param>
		/// <param name="beginOrEqual">If true, the <paramref name="begin"/> key is included in the range</param>
		/// <param name="end">End of the range</param>
		/// <param name="endOrEqual">If true, the <paramref name="end"/> key is included in the range</param>
		/// <returns>Unordered list of the all the keys in the dictionary that are in the range.</returns>
		/// <remarks>There is no guarantee in the actual order of the keys returned. It is also not allowed to remove keys while iterating over the sequence.</remarks>
		public IEnumerable<KeyValuePair<TKey, TValue>> ScanReverse(TKey begin, bool beginOrEqual, TKey end, bool endOrEqual)
		{
			// return the unordered list of all the keys that are between the begin/end pair.
			// each bound is included in the list if its corresponding 'orEqual' is set to true

			if (m_items.Count > 0)
			{
				var iter = m_items.GetIterator();
				if (!iter.Seek(new KeyValuePair<TKey, TValue>(end, default!), endOrEqual))
				{ // starts at the end
					iter.SeekAfterLast();
				}

				var cmp = m_keyComparer;
				do
				{
					int p = cmp.Compare(iter.Current.Key, begin);
					if (beginOrEqual ? (p < 0) : (p <= 0))
					{
						yield break;
					}

					yield return iter.Current;
				}
				while (iter.Previous());
			}
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public ColaStore<KeyValuePair<TKey, TValue>>.Iterator GetIterator()
			=> m_items.GetIterator();

		/// <summary>Returns an enumerator that iterates through the ordered dictionary</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public ColaStore.Enumerator<KeyValuePair<TKey, TValue>> GetEnumerator()
			=> new (m_items, reverse: false);

		public IEnumerable<KeyValuePair<TKey, TValue>> IterateOrdered()
			=> m_items.IterateOrdered();

		IEnumerator<KeyValuePair<TKey, TValue>> IEnumerable<KeyValuePair<TKey, TValue>>.GetEnumerator()
			=> this.GetEnumerator();

		System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
			=> this.GetEnumerator();

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		internal void CopyTo(KeyValuePair<TKey, TValue>[] array, int index)
		{
			m_items.CopyTo(array, index, m_items.Count);
		}

		[MethodImpl(MethodImplOptions.NoInlining)]
		private static Exception ErrorKeyNotFound()
		{
			return new KeyNotFoundException();
		}

		[MethodImpl(MethodImplOptions.NoInlining)]
		private static Exception ErrorKeyAlreadyExists()
		{
			return new InvalidOperationException("An entry with the same key but a different value already exists.");
		}

		//TODO: remove or set to internal !
		[Conditional("DEBUG")]
		public void Debug_Dump()
		{
#if DEBUG
			System.Diagnostics.Trace.WriteLine("Dumping ColaOrderedDictionary<" + typeof(TKey).Name + ", " + typeof(TValue).Name + "> filled at " + (100.0d * this.Count / this.Capacity).ToString("N2") + "%");
			m_items.Debug_Dump();
#endif
		}

		[StructLayout(LayoutKind.Sequential)]
		public struct Enumerator : IEnumerator<KeyValuePair<TKey, TValue>>
		{
			private readonly int m_version;
			private readonly ColaOrderedDictionary<TKey, TValue> m_parent;
			private ColaStore.Enumerator<KeyValuePair<TKey, TValue>> m_iterator;

			internal Enumerator(ColaOrderedDictionary<TKey, TValue> parent, bool reverse)
			{
				m_version = parent.m_version;
				m_parent = parent;
				m_iterator = new ColaStore.Enumerator<KeyValuePair<TKey, TValue>>(parent.m_items, reverse);
			}

			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public bool MoveNext()
			{
				if (m_version != m_parent.m_version)
				{
					throw ColaStore.ErrorStoreVersionChanged();
				}

				return m_iterator.MoveNext();
			}

			public KeyValuePair<TKey, TValue> Current => m_iterator.Current;

			public void Dispose()
			{
				// we are a struct that can be copied by value, so there is no guarantee that Dispose() will accomplish anything anyway...
			}

			object System.Collections.IEnumerator.Current => m_iterator.Current;

			void System.Collections.IEnumerator.Reset()
			{
				if (m_version != m_parent.m_version)
				{
					throw ColaStore.ErrorStoreVersionChanged();
				}
				m_iterator = new ColaStore.Enumerator<KeyValuePair<TKey, TValue>>(m_parent.m_items, m_iterator.Reverse);
			}

		}

	}

}
