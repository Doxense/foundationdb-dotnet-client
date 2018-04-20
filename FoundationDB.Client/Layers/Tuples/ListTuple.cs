#region BSD Licence
/* Copyright (c) 2013-2018, Doxense SAS
All rights reserved.

Redistribution and use in source and binary forms, with or without
modification, are permitted provided that the following conditions are met:
	* Redistributions of source code must retain the above copyright
	  notice, this list of conditions and the following disclaimer.
	* Redistributions in binary form must reproduce the above copyright
	  notice, this list of conditions and the following disclaimer in the
	  documentation and/or other materials provided with the distribution.
	* Neither the name of Doxense nor the
	  names of its contributors may be used to endorse or promote products
	  derived from this software without specific prior written permission.

THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND
ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
DISCLAIMED. IN NO EVENT SHALL <COPYRIGHT HOLDER> BE LIABLE FOR ANY
DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
(INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
(INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
 */
#endregion

namespace FoundationDB.Layers.Tuples
{
	using System;
	using System.Collections;
	using System.Collections.Generic;
	using System.Linq;
	using Doxense.Diagnostics.Contracts;
	using FoundationDB.Client;
	using FoundationDB.Client.Converters;

	/// <summary>Tuple that can hold any number of untyped items</summary>
	public sealed class ListTuple : ITuple
	{
		// We could use a FdbListTuple<T> for tuples where all items are of type T, and FdbListTuple could derive from FdbListTuple<object>.
		// => this could speed up a bit the use case of STuple.FromArray<T> or STuple.FromSequence<T>

		/// <summary>List of the items in the tuple.</summary>
		/// <remarks>It is supposed to be immutable!</remarks>
		private readonly object[] m_items;

		private readonly int m_offset;

		private readonly int m_count;

		private int? m_hashCode;

		/// <summary>Create a new tuple from a sequence of items (copied)</summary>
		internal ListTuple(IEnumerable<object> items)
		{
			m_items = items.ToArray();
			m_count = m_items.Length;
		}

		/// <summary>Wrap a List of items</summary>
		/// <remarks>The list should not mutate and should not be exposed to anyone else!</remarks>
		internal ListTuple(object[] items, int offset, int count)
		{
			Contract.Requires(items != null && offset >= 0 && count >= 0);
			Contract.Requires(offset + count <= items.Length, "inner item array is too small");

			m_items = items;
			m_offset = offset;
			m_count = count;
		}

		/// <summary>Create a new list tuple by merging the items of two tuples together</summary>
		/// <param name="left"></param>
		/// <param name="right"></param>
		internal ListTuple(ITuple a, ITuple b)
		{
			if (a == null) throw new ArgumentNullException("a");
			if (b == null) throw new ArgumentNullException("b");

			int nA = a.Count;
			int nB = b.Count;

			m_offset = 0;
			m_count = nA + nB;
			m_items = new object[m_count];

			if (nA > 0) a.CopyTo(m_items, 0);
			if (nB > 0) b.CopyTo(m_items, nA);
		}

		/// <summary>Create a new list tuple by merging the items of three tuples together</summary>
		internal ListTuple(ITuple a, ITuple b, ITuple c)
		{
			if (a == null) throw new ArgumentNullException("a");
			if (b == null) throw new ArgumentNullException("b");
			if (c == null) throw new ArgumentNullException("c");

			int nA = a.Count;
			int nB = b.Count;
			int nC = c.Count;

			m_offset = 0;
			m_count = nA + nB + nC;
			m_items = new object[m_count];

			if (nA > 0) a.CopyTo(m_items, 0);
			if (nB > 0) b.CopyTo(m_items, nA);
			if (nC > 0) c.CopyTo(m_items, nA + nB);
		}

		public int Count
		{
			get { return m_count; }
		}

		public object this[int index]
		{
			get
			{
				return m_items[m_offset + STuple.MapIndex(index, m_count)];
			}
		}

		public ITuple this[int? fromIncluded, int? toExcluded]
		{
			get
			{
				int begin = fromIncluded.HasValue ? STuple.MapIndexBounded(fromIncluded.Value, m_count) : 0;
				int end = toExcluded.HasValue ? STuple.MapIndexBounded(toExcluded.Value, m_count) : m_count;

				int len = end - begin;
				if (len <= 0) return STuple.Empty;
				if (begin == 0 && len == m_count) return this;

				Contract.Assert(m_offset + begin >= m_offset);
				Contract.Assert(len >= 0 && len <= m_count);

				return new ListTuple(m_items, m_offset + begin, len);
			}
		}

		public R Get<R>(int index)
		{
			return FdbConverters.ConvertBoxed<R>(this[index]);
		}

		public R Last<R>()
		{
			if (m_count == 0) throw new InvalidOperationException("Tuple is empty");
			return FdbConverters.ConvertBoxed<R>(m_items[m_offset + m_count - 1]);
		}

		ITuple ITuple.Append<T>(T value)
		{
			return this.Append<T>(value);
		}

		public ListTuple Append<T>(T value)
		{
			var list = new object[m_count + 1];
			Array.Copy(m_items, m_offset, list, 0, m_count);
			list[m_count] = value;
			return new ListTuple(list, 0, list.Length);
		}

		public ListTuple AppendRange(object[] items)
		{
			if (items == null) throw new ArgumentNullException("items");

			if (items.Length == 0) return this;

			var list = new object[m_count + items.Length];
			Array.Copy(m_items, m_offset, list, 0, m_count);
			Array.Copy(items, 0, list, m_count, items.Length);
			return new ListTuple(list, 0, list.Length);
		}

		public ListTuple Concat(ListTuple tuple)
		{
			if (tuple == null) throw new ArgumentNullException("tuple");

			if (tuple.m_count == 0) return this;
			if (m_count == 0) return tuple;

			var list = new object[m_count + tuple.m_count];
			Array.Copy(m_items, m_offset, list, 0, m_count);
			Array.Copy(tuple.m_items, tuple.m_offset, list, m_count, tuple.m_count);
			return new ListTuple(list, 0, list.Length);
		}

		public ListTuple Concat(ITuple tuple)
		{
			var _ = tuple as ListTuple;
			if (_ != null) return Concat(_);

			int count = tuple.Count;
			if (count == 0) return this;

			var list = new object[m_count + count];
			Array.Copy(m_items, m_offset, list, 0, m_count);
			tuple.CopyTo(list, m_count);
			return new ListTuple(list, 0, list.Length);
		}

		ITuple ITuple.Concat(ITuple tuple)
		{
			return this.Concat(tuple);
		}

		public void CopyTo(object[] array, int offset)
		{
			Array.Copy(m_items, m_offset, array, offset, m_count);
		}

		public IEnumerator<object> GetEnumerator()
		{
			if (m_offset == 0 && m_count == m_items.Length)
			{
				return ((IList<object>)m_items).GetEnumerator();
			}
			return Enumerate(m_items, m_offset, m_count);
		}

		System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
		{
			return this.GetEnumerator();
		}

		private static IEnumerator<object> Enumerate(object[] items, int offset, int count)
		{
			while (count-- > 0)
			{
				yield return items[offset++];
			}
		}

		public void PackTo(ref TupleWriter writer)
		{
			for (int i = 0; i < m_count; i++)
			{
				TuplePackers.SerializeObjectTo(ref writer, m_items[i + m_offset]);
			}
		}

		public Slice ToSlice()
		{
			var writer = new TupleWriter();
			PackTo(ref writer);
			return writer.Output.ToSlice();
		}

		public override string ToString()
		{
			return STuple.ToString(m_items, m_offset, m_count);
		}

		private bool CompareItems(IEnumerable<object> theirs, IEqualityComparer comparer)
		{
			int p = 0;
			foreach (var item in theirs)
			{
				if (p >= m_count) return false;

				if (item == null)
				{
					if (m_items[p + m_offset] != null) return false;
				}
				else
				{
					if (!comparer.Equals(item, m_items[p + m_offset])) return false;
				}
				p++;
			}
			return p >= m_count;
		}

		public override bool Equals(object obj)
		{
			return obj != null && ((IStructuralEquatable)this).Equals(obj, SimilarValueComparer.Default);
		}

		public bool Equals(ITuple other)
		{
			return !object.ReferenceEquals(other, null) && ((IStructuralEquatable)this).Equals(other, SimilarValueComparer.Default);
		}

		public override int GetHashCode()
		{
			return ((IStructuralEquatable)this).GetHashCode(SimilarValueComparer.Default);
		}

		bool IStructuralEquatable.Equals(object other, IEqualityComparer comparer)
		{
			if (object.ReferenceEquals(this, other)) return true;
			if (other == null) return false;

			var list = other as ListTuple;
			if (!object.ReferenceEquals(list, null))
			{
				if (list.m_count != m_count) return false;

				if (list.m_offset == 0 && list.m_count == list.m_items.Length)
				{
					return CompareItems(list.m_items, comparer);
				}
				else
				{
					return CompareItems(list, comparer);
				}
			}

			return STuple.Equals(this, other, comparer);
		}

		int IStructuralEquatable.GetHashCode(System.Collections.IEqualityComparer comparer)
		{
			// the cached hashcode is only valid for the default comparer!
			bool canUseCache = object.ReferenceEquals(comparer, SimilarValueComparer.Default);
			if (m_hashCode.HasValue && canUseCache)
			{
				return m_hashCode.Value;
			}

			int h = 0;
			for (int i = 0; i < m_count; i++)
			{
				var item = m_items[i + m_offset];
					
				h = STuple.CombineHashCodes(h, comparer.GetHashCode(item));
			}
			if (canUseCache) m_hashCode = h;
			return h;
		}

	}

}
