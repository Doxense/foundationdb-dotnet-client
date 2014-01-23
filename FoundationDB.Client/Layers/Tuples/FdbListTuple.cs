#region BSD Licence
/* Copyright (c) 2013, Doxense SARL
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
	using FoundationDB.Client;
	using FoundationDB.Client.Converters;
	using FoundationDB.Client.Utils;
	using System;
	using System.Collections;
	using System.Collections.Generic;
	using System.Linq;

	/// <summary>Tuple that can hold any number of untyped items</summary>
	public sealed class FdbListTuple : IFdbTuple
	{
		private static readonly object[] EmptyList = new object[0];

		/// <summary>List of the items in the tuple.</summary>
		/// <remarks>It is supposed to be immutable!</remarks>
		private readonly object[] m_items;

		private readonly int m_offset;

		private readonly int m_count;

		private int? m_hashCode;

		/// <summary>Create a new tuple from a sequence of items (copied)</summary>
		internal FdbListTuple(IEnumerable<object> items)
		{
			m_items = items.ToArray();
			m_count = m_items.Length;
		}

		/// <summary>Wrap a List of items</summary>
		/// <remarks>The list should not mutate and should not be exposed to anyone else!</remarks>
		internal FdbListTuple(object[] items, int offset, int count)
		{
			Contract.Requires(items != null && offset >= 0 && count >= 0);
			Contract.Requires(offset + count <= items.Length, "inner item array is too small");

			m_items = items;
			m_offset = offset;
			m_count = count;
		}

		public int Count
		{
			get { return m_count; }
		}

		public object this[int index]
		{
			get
			{
				return m_items[m_offset + FdbTuple.MapIndex(index, m_count)];
			}
		}

		public IFdbTuple this[int? from, int? to]
		{
			get
			{
				int start = FdbTuple.MapIndex(from ?? 0, m_count);
				int end = FdbTuple.MapIndex(to ?? -1, m_count);

				int len = end - start + 1;
				if (len <= 0) return FdbTuple.Empty;
				if (start == 0 && len == m_count) return this;

				Contract.Assert(m_offset + start >= m_offset);
				Contract.Assert(len >= 0 && len <= m_count);

				return new FdbListTuple(m_items, m_offset + start, len);
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

		IFdbTuple IFdbTuple.Append<T>(T value)
		{
			return this.Append<T>(value);
		}

		public FdbListTuple Append<T>(T value)
		{
			var list = new object[m_count + 1];
			Array.Copy(m_items, m_offset, list, 0, m_count);
			list[m_count] = value;
			return new FdbListTuple(list, 0, list.Length);
		}

		public FdbListTuple AppendRange(object[] items)
		{
			if (items == null) throw new ArgumentNullException("items");

			if (items.Length == 0) return this;

			var list = new object[m_count + items.Length];
			Array.Copy(m_items, m_offset, list, 0, m_count);
			Array.Copy(items, 0, list, m_count, items.Length);
			return new FdbListTuple(list, 0, list.Length);
		}

		public FdbListTuple Concat(FdbListTuple tuple)
		{
			if (tuple == null) throw new ArgumentNullException("tuple");

			if (tuple.m_count == 0) return this;
			if (m_count == 0) return tuple;

			var list = new object[m_count + tuple.m_count];
			Array.Copy(m_items, m_offset, list, 0, m_count);
			Array.Copy(tuple.m_items, tuple.m_offset, list, m_count, tuple.m_count);
			return new FdbListTuple(list, 0, list.Length);
		}

		public FdbListTuple Concat(IFdbTuple tuple)
		{
			var _ = tuple as FdbListTuple;
			if (_ != null) return Concat(_);

			int count = tuple.Count;
			if (count == 0) return this;

			var list = new object[m_count + count];
			Array.Copy(m_items, m_offset, list, 0, m_count);
			tuple.CopyTo(list, m_count);
			return new FdbListTuple(list, 0, list.Length);
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

		public void PackTo(ref SliceWriter writer)
		{
			for (int i = 0; i < m_count; i++)
			{
				FdbTuplePackers.SerializeObjectTo(ref writer, m_items[i + m_offset]);
			}
		}

		public Slice ToSlice()
		{
			var writer = SliceWriter.Empty;
			PackTo(ref writer);
			return writer.ToSlice();
		}

		Slice IFdbKey.ToFoundationDbKey()
		{
			return this.ToSlice();
		}

		public override string ToString()
		{
			return FdbTuple.ToString(m_items, m_offset, m_count);
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

		public bool Equals(IFdbTuple other)
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

			var list = other as FdbListTuple;
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

			return FdbTuple.Equals(this, other, comparer);
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
					
				h = FdbTuple.CombineHashCodes(h, comparer.GetHashCode(item));
			}
			if (canUseCache) m_hashCode = h;
			return h;
		}

	}

}
