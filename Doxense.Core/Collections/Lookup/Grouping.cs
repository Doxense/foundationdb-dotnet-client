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

namespace Doxense.Collections.Lookup
{
	using System.Runtime;
	using Doxense.Memory;

	/// <summary>Implémentation d'un grouping de plusieurs éléments sous une même clé</summary>
	/// <typeparam name="TKey">Type of keys</typeparam>
	/// <typeparam name="TElement">Type of elements</typeparam>
	/// <remarks>This mutable version of <c>System.Linq.Lookup&lt;K, V&gt;.Grouping&lt;K, V&gt;</c>, which is internal and readonly.</remarks>
	public class Grouping<TKey, TElement> : IGrouping<TKey, TElement>, IList<TElement>
	{
		// IMPORTANT: contrary to the LINQ implementation (readonly), the items can be MODIFIED!
		// => it is possible to add/remove/filter values after the grouping has been created.
		// => it is not thread-safe!

		#region Private Members...

		// note: these values are modified by the caller

		/// <summary>Key of this grouping</summary>
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
		internal TKey m_key;

		/// <summary>Buffer that contains the elements in this grouping.</summary>
		/// <remarks>Number of actual elements is given by <see cref="m_count"/>. The tail of the buffer is unused.</remarks>
		internal TElement[] m_elements;
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.

		/// <summary>Number of entries in the array</summary>
		internal int m_count;

		//REVIEW: use ReadOnlyMemory<TElement> instead?

		#endregion

		#region Public Properties...

		/// <summary>Get the key of the Doxense.Collections.Lookup.Grouping&lt;TKey, TElement&gt;</summary>
		public TKey Key
		{
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get => m_key;
		}

		/// <summary>Get the number of elements contained in the Doxense.Collections.Lookup.Grouping&lt;TKey, TElement&gt;</summary>
		public int Count
		{
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get => m_count;
		}

		/// <summary>Get the first element of the grouping (or default(T) if empty)</summary>
		public TElement? Head
		{
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get => m_count > 0 ? m_elements[0] : default;
		}

		/// <summary>Get the last element of the grouping (or default(T) if empty)</summary>
		public TElement? Last => m_count > 0 ? m_elements[m_count - 1] : default;
		//REVIEW: either "Head/Tail" or "First"/"Last"

		public TElement this[int index]
		{
			get
			{
				if (index < 0 || index >= m_count) ThrowHelper.ThrowArgumentOutOfRangeIndex(index);
				return m_elements[index];
			}
			[DoesNotReturn]
			set => throw new NotSupportedException();
		}

		#endregion

		#region Public Methods...

		/// <summary>Add an item to this grouping</summary>
		/// <param name="element"></param>
		public void Add(TElement element)
		{
			int count = m_count;
			if (count == m_elements.Length)
			{
				// note: may throw if count is > 2^29 due to the max size of .NET objects in memory!
				Array.Resize(ref m_elements, checked(count * 2));
			}
			m_elements[count] = element;
			m_count = count + 1;
		}

		private void EnsureCapacity(int capacity)
		{
			if (m_elements.Length < capacity)
			{ // too small, must resize
				capacity = BitHelpers.NextPowerOfTwo(capacity);
				Array.Resize(ref m_elements, capacity);
			}
		}

		/// <summary>Add a batch of elements to this grouping</summary>
		/// <param name="elements">Sequence of elements (can be empty)</param>
		public void AddRange(IEnumerable<TElement> elements)
		{
			if (elements is ICollection<TElement> collection)
			{ // we know the length, we can resize the buffer to be large enough in one step.

				int n = collection.Count;
				if (n == 0) return;

				// Ensure that we have enough capacity for the new elements
				EnsureCapacity(m_count + n);
				collection.CopyTo(m_elements, m_count);
				m_count += n;
			}
			else
			{ // we don't know the number, we have to add them one by one, and maybe need multiple buffer resize
				foreach (var element in elements)
				{
					Add(element);
				}
			}
		}

		/// <summary>Add all the elements of another grouping to this grouping</summary>
		/// <param name="grouping">Grouping that must be merged with this one</param>
		/// <remarks>No attempt will be made to dedup the items. If the both grouping contain the same item, it will be duplicated!</remarks>
		public void AddRange(Grouping<TKey, TElement> grouping)
		{
			Contract.NotNull(grouping);

			int n = grouping.m_count;
			if (n == 0) return;

			// ajoutes les éléments à la fin de notre buffer, en se resizant si besoin
			EnsureCapacity(m_count + n);
			Array.Copy(grouping.m_elements, 0, m_elements, m_count, n);
			m_count += n;
		}

		/// <summary>Remove an element from this grouping</summary>
		/// <param name="element">Element to remove</param>
		/// <returns><c>true</c> if the element was present and has been removed; otherwise, <c>false</c></returns>
		/// <remarks>Uses <c>EqualityComparer&lt;T&gt;.Default</c> to compare the elements.
		/// Warning: will only remove the first occurrence found, in case of duplicates!</remarks>
		public bool Remove(TElement element)
		{
			if (m_count >= 0)
			{
				int index = Array.IndexOf<TElement>(m_elements, element, 0, m_count);
				if (index >= 0)
				{
					RemoveAtInternal(index);
					return true;
				}
			}
			return false;
		}

		/// <summary>Remove an element from this grouping</summary>
		/// <param name="element">Element to remove</param>
		/// <param name="comparer">Comparer used to find the element</param>
		/// <returns><c>true</c> if the element was present and has been removed; otherwise, <c>false</c></returns>
		/// <remarks>Warning: will only remove the first occurrence found, in case of duplicates!</remarks>
		public bool Remove(TElement element, IEqualityComparer<TElement> comparer)
		{
			Contract.NotNull(comparer);

			int count = m_count;
			var elements = m_elements;
			for (int i = 0; i < count; i++)
			{
				if (comparer.Equals(element, elements[i]))
				{
					RemoveAtInternal(i);
					return true;
				}
			}
			return false;
		}

		/// <summary>Remove the element at the specified position</summary>
		public void RemoveAt(int index)
		{
			if ((uint) index >= m_count) throw ThrowHelper.ArgumentOutOfRangeException(nameof(index));
			RemoveAtInternal(index);
		}

		/// <summary>Remove and return the last element of the grouping</summary>
		/// <returns>Last element</returns>
		/// <exception cref="System.InvalidOperationException">If the grouping was already empty</exception>
		public TElement Pop()
		{
			if (m_count == 0) throw new InvalidOperationException("Cannot remove last item from an empty grouping");
			int p = m_count - 1;
			var result = m_elements[p];
			RemoveAtInternal(p);
			return result;
		}

		/// <summary>Update the last element, or add it if the grouping was empty</summary>
		/// <param name="newValue">New value for the last element</param>
		public void UpdateLast(TElement newValue)
		{
			if (m_count == 0)
				Add(newValue);
			else
				m_elements[m_count - 1] = newValue;
		}

		/// <summary>Clear all elements from this grouping</summary>
		public void Clear()
		{
			if (m_count > 0)
			{
				if (m_elements.Length <= 4)
				{ // on peut réutiliser le tableau (en le vidant)
					for (int i = 0; i < m_count; i++)
					{
						m_elements[i] = default!;
					}
				}
				else
				{ // on repart de zéro
					m_elements = new TElement[1];
				}
				m_count = 0;
			}
		}

		/// <summary>Remove all elements that match the specified predicate</summary>
		/// <param name="match">Predicate that returns <c>true</c> for each element to remove, and <c>false</c> for elements to keep.</param>
		/// <remarks>Number of removed elements (0 if grouping was empty, or no match found)</remarks>
		public int RemoveAll(Func<TElement, bool> match)
		{
			Contract.NotNull(match);

			// look for the first match
			// => any elements before will be kept
			int count = m_count;
			var elements = m_elements;
			int index = 0;
			while ((index < count) && !match(elements[index]))
			{ // keep this element
				index++;
			}

			if (index >= count)
			{ // nothing to remove
				return 0;
			}

			// advance through the buffer, and shift all chunks of elements that are kept towards the start of the buffer.
			int cursor = index + 1;
			while (cursor < count)
			{
				while ((cursor < count) && match(elements[cursor]))
				{
					cursor++;
				}
				if (cursor < count)
				{
					elements[index++] = elements[cursor++];
				}
			}

			// clear the tail of the buffer
			Array.Clear(elements, index, count - index);
			int deleted = count - index;
			m_count = index;
			ShrinkIfNeeded();
			return deleted;
		}

		/// <summary>Remove the element at the given position</summary>
		private void RemoveAtInternal(int index)
		{
			var elements = m_elements;
			int after = m_count - index - 1;
			if (after > 0)
			{ // si ce n'est pas le dernier, il faut shifter  les items suivants
				Array.Copy(elements, index + 1, elements, index, after);
			}
			--m_count;

			// vide le dernier slot pour éviter les leaks
			elements[m_count] = default!;

			ShrinkIfNeeded();
		}

		/// <summary>Maybe reduce the size of the buffer, if possible</summary>
		/// <remarks>Shrink the length by 2 if less than 1/8th is used</remarks>
		private void ShrinkIfNeeded()
		{
			// if less than 1/8th is allocated, reduce the buffer size by 50%
			int n = m_elements.Length;
			if (m_count < (n >> 3) && n > 1)
			{
				Array.Resize(ref m_elements, n >> 1);
			}
		}

		public int IndexOf(Func<TElement, bool> match)
		{
			int n = m_count;
			var elements = m_elements;
			for (int i = 0; i < n; i++)
			{
				if (match(elements[i])) return i;
			}
			return -1;
		}

		public ReadOnlySpan<TElement> Span
		{
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get => m_elements.AsSpan(0, m_count);
		}

		public TElement[] ToArray()
		{
			int count = m_count;
			var elements = m_elements;
			if (count == 0) return [ ];

			var t = new TElement[count];
			if (count == 1)
			{
				t[0] = elements[0];
			}
			else
			{
				Array.Copy(elements, 0, t, 0, count);
			}
			return t;
		}

		public List<TElement> ToList()
		{
			int count = m_count;
			var elements = m_elements;

			var list = new List<TElement>(count);
			for (int i = 0; i < count; i++)
			{
				list.Add(elements[i]);
			}
			return list;
		}

		/// <summary>Run an action on each element in this grouping</summary>
		/// <param name="action">Lambda that will be called with each element, one by one</param>
		public void Visit(Action<TElement> action)
		{
			int count = m_count;
			var elements = m_elements;
			for (int i = 0; i < count; i++)
			{
				action(elements[i]);
			}
		}

		/// <summary>Run an action on each element in this grouping</summary>
		/// <param name="action">Lambda that will be called with each element, one by one</param>
		public void Visit(Action<TKey, TElement> action)
		{
			int count = m_count;
			var elements = m_elements;
			for (int i = 0; i < count; i++)
			{
				action(m_key, elements[i]);
			}
		}

		/// <summary>Convert the grouping into a sequence of key/value pairs</summary>
		/// <param name="selector">Optional filter (return <c>true</c> for elements to keep, and <c>false</c> for elements to discard)</param>
		/// <returns>Sequence of KeyValuePair where Key is the grouping's key, and Value is an element of this gouping</returns>
		public IEnumerable<KeyValuePair<TKey, TElement>> Map(Func<TElement, bool>? selector = null)
		{
			int count = m_count;
			var elements = m_elements;
			for (int i = 0; i < count; i++)
			{
				if (selector == null || selector(elements[i]))
				{
					yield return new KeyValuePair<TKey, TElement>(m_key, elements[i]);
				}
			}
		}

		#endregion

		#region IEnumerable<TElement> ...

		[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
		public struct Enumerator : IEnumerator<TElement>
		{
			private readonly Grouping<TKey, TElement> m_grouping;
			private int m_index;
			private TElement m_current;

			internal Enumerator(Grouping<TKey, TElement> grouping)
			{
				m_grouping = grouping;
				m_index = 0;
				m_current = default!;
			}

			public bool MoveNext()
			{
				var grouping = m_grouping;
				if (m_index < grouping.m_count)
				{
					m_current = grouping.m_elements[m_index];
					++m_index;
					return true;
				}
				return MoveNextRare();
			}

			private bool MoveNextRare()
			{
				m_index = m_grouping.m_count + 1;
				m_current = default!;
				return false;
			}

			public TElement Current
			{
				[TargetedPatchingOptOut("Performance critical to inline this type of method across NGen image boundaries")]
				[return: MaybeNull]
				get => m_current;
			}

			object? System.Collections.IEnumerator.Current
			{
				get
				{
					if (m_index == 0 || m_index == (m_grouping.Count + 1))
					{
						ThrowEnumOpCantHappen();
					}
					return m_current;
				}
			}

			[DoesNotReturn, ContractAnnotation("=> halt"), StackTraceHidden]
			private static void ThrowEnumOpCantHappen()
			{
				throw new InvalidOperationException("Enumeration has either not started or has already finished.");
			}

			void System.Collections.IEnumerator.Reset()
			{
				m_index = 0;
				m_current = default!;
			}

			[System.Runtime.TargetedPatchingOptOut("Performance critical to inline across NGen image boundaries")]
			public void Dispose()
			{
				//NOP
			}
		}

		[TargetedPatchingOptOut("Performance critical to inline across NGen image boundaries")]
		public Enumerator GetEnumerator()
		{
			return new Enumerator(this);
		}

		[TargetedPatchingOptOut("Performance critical to inline across NGen image boundaries")]
		IEnumerator<TElement> IEnumerable<TElement>.GetEnumerator()
		{
			return new Enumerator(this);
		}

		[TargetedPatchingOptOut("Performance critical to inline across NGen image boundaries")]
		System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
		{
			return new Enumerator(this);
		}

		#endregion

		#region ICollection<TElement> ...

		bool ICollection<TElement>.Contains(TElement item)
		{
			for (int i = 0; i < m_count; i++)
			{
				if (object.Equals(item, m_elements[i])) return true;
			}
			return false;
		}

		/// <summary>Copy the elements of this grouping into an array</summary>
		/// <remarks>The destinatino array must be large enough to receive all elements.</remarks>
		/// <exception cref="System.ArgumentNullException">If <paramref name="array"/> is <c>null</c></exception>
		/// <exception cref="System.InvalidOperationException">If the target array is not large enough, or <paramref name="arrayIndex"/> is negative</exception>
		public void CopyTo(TElement[] array, int arrayIndex)
		{
			Array.Copy(m_elements, 0, array, arrayIndex, m_count);
		}

		bool ICollection<TElement>.IsReadOnly => false;

		#endregion

		#region IList<TElement> ...

		int IList<TElement>.IndexOf(TElement item)
		{
			return Array.IndexOf(m_elements, item, 0, m_count);
		}

		void IList<TElement>.Insert(int index, TElement item)
		{
			throw new NotSupportedException();
		}

		#endregion

		#region Pseudo-LINQ

		public bool Any(Func<TElement, bool> predicate)
		{
			int count = m_count;
			if (count == 0) return false;
			var elements = m_elements;
			for (int i = 0; i < count; i++)
			{
				if (predicate(elements[i])) return true;
			}
			return false;
		}

		public bool All(Func<TElement, bool> predicate)
		{
			int count = m_count;
			if (count == 0) return false;
			var elements = m_elements;
			for (int i = 0; i < count; i++)
			{
				if (!predicate(elements[i])) return false;
			}
			return true;
		}

		public IEnumerable<TElement> Where(Func<TElement, bool> predicate)
		{
			int count = m_count;
			var elements = m_elements;
			for (int i = 0; i < count; i++)
			{
				var element = elements[i];
				if (predicate(element)) yield return element;
			}
		}

		#endregion

		public sealed class EqualityComparer : IEqualityComparer<Grouping<TKey, TElement>>, IEqualityComparer<TKey>
		{
			private readonly IEqualityComparer<TKey> m_comparer;

			public EqualityComparer()
			{
				m_comparer = EqualityComparer<TKey>.Default;
			}

			public EqualityComparer(IEqualityComparer<TKey> comparer)
			{
				Contract.NotNull(comparer);
				m_comparer = comparer;
			}

			public bool Equals(TKey? x, TKey? y)
			{
				return m_comparer.Equals(x, y);
			}

			public int GetHashCode(TKey obj)
			{
				return m_comparer.GetHashCode(obj!);
			}

			public bool Equals(Grouping<TKey, TElement>? x, Grouping<TKey, TElement>? y)
			{
				return x == null ? y == null : y != null && m_comparer.Equals(x.Key, y.Key);
			}

			public int GetHashCode(Grouping<TKey, TElement>? obj)
			{
				return obj == null ? -1 : m_comparer.GetHashCode(obj.Key!);
			}
		}

		public sealed class Comparer : IComparer<Grouping<TKey, TElement>>, IComparer<TKey>
		{
			public static readonly IComparer<Grouping<TKey, TElement>> Default = new Comparer();

			private readonly IComparer<TKey> m_comparer;

			public Comparer()
			{
				m_comparer = Comparer<TKey>.Default;
			}

			public Comparer(IComparer<TKey> comparer)
			{
				Contract.NotNull(comparer);
				m_comparer = comparer;
			}

			public int Compare(TKey? x, TKey? y)
			{
				return m_comparer.Compare(x, y);
			}

			public int Compare(Grouping<TKey, TElement>? x, Grouping<TKey, TElement>? y)
			{
				return x == null ? (y == null ? 0 : -1)
					: y == null ? +1
					: m_comparer.Compare(x.Key, y.Key);
			}
		}

	}

	public static class Grouping
	{

		/// <summary>Create a new grouping that contains a single element</summary>
		/// <param name="key">Key for this gouping</param>
		/// <param name="element">Single element that will be stored in the grouping</param>
		public static Grouping<TKey, TElement> Create<TKey, TElement>(TKey key, TElement element)
		{
			return new Grouping<TKey, TElement>
			{
				m_key = key,
				m_elements = [ element ],
				m_count = 1
			};
		}

		/// <summary>Create a new grouping from an already allocated array of elements</summary>
		/// <param name="key">Key for this grouping</param>
		/// <param name="elements">Array of elements taht will be stored in the grouping</param>
		/// <remarks>The grouping will used the specified array as the backing store. This array should not be modified or returned into a pool, as long as the grouping is used!</remarks>
		public static Grouping<TKey, TElement> Create<TKey, TElement>(TKey key, params TElement[] elements)
		{
			return new Grouping<TKey, TElement>
			{
				m_key = key,
				m_elements = elements,
				m_count = elements.Length
			};
		}

		/// <summary>Create a new grouping from a collection of elements</summary>
		/// <param name="key">Key for this grouping</param>
		/// <param name="elements">Collection of elements that will be stored in the grouping</param>
		public static Grouping<TKey, TElement> Create<TKey, TElement>(TKey key, ICollection<TElement> elements)
		{
			return new Grouping<TKey, TElement>
			{
				m_key = key,
				m_elements = elements.ToArray(),
				m_count = elements.Count
			};
		}

		/// <summary>Create a new grouping from a sequence of elements</summary>
		/// <param name="key">Key for this grouping</param>
		/// <param name="elements">Sequence of elements that will be stored in the grouping</param>
		public static Grouping<TKey, TElement> Create<TKey, TElement>(TKey key, IEnumerable<TElement> elements)
		{
			var t = elements.ToArray();
			return new Grouping<TKey, TElement>
			{
				m_key = key,
				m_elements = t,
				m_count = t.Length
			};
		}

		/// <summary>Convertit un IGrouping LINQ</summary>
		/// <param name="grouping">Grouping LINQ à convertir en Grouping modifiable</param>
		public static Grouping<TKey, TElement> Create<TKey, TElement>(IGrouping<TKey, TElement> grouping)
		{
			var t = grouping.ToArray();
			return new Grouping<TKey, TElement>
			{
				m_key = grouping.Key,
				m_elements = t,
				m_count = t.Length
			};
		}

		/// <summary>Create a new grouping from a key/value pair singleton</summary>
		public static Grouping<TKey, TElement> FromPair<TKey, TElement>(KeyValuePair<TKey, TElement> pair)
		{
			return new Grouping<TKey, TElement>
			{
				m_key = pair.Key,
				m_elements = [ pair.Value ],
				m_count = 1
			};
		}

		/// <summary>Create a new grouping from a key/values pair</summary>
		public static Grouping<TKey, TElement> FromPair<TKey, TElement>(KeyValuePair<TKey, TElement[]> pair)
		{
			return Create(pair.Key, pair.Value);
		}

		/// <summary>Create a new grouping from a key/values pair</summary>
		public static Grouping<TKey, TElement> FromPair<TKey, TElement>(KeyValuePair<TKey, ICollection<TElement>> pair)
		{
			return Create(pair.Key, pair.Value);
		}

		/// <summary>Create a new grouping from a key/values pair</summary>
		public static Grouping<TKey, TElement> FromPair<TKey, TElement>(KeyValuePair<TKey, IEnumerable<TElement>> pair)
		{
			return Create(pair.Key, pair.Value);
		}

		/// <summary>Convert from a LINQ grouping</summary>
		[ContractAnnotation("null => null; notnull => notnull")]
		public static Grouping<TKey, TElement>? FromLinq<TKey, TElement>(IGrouping<TKey, TElement>? grouping)
		{
			return grouping != null ? (grouping as Grouping<TKey, TElement>) ?? Create(grouping) : null;
		}

	}

}
