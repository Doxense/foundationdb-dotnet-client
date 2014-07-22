#region BSD Licence
/* Copyright (c) 2013-2014, Doxense SAS
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
	using System.Diagnostics;

	/// <summary>Represents an immutable tuple where the packed bytes are cached</summary>
	[DebuggerDisplay("{ToString()}")]
	public sealed class FdbMemoizedTuple : IFdbTuple
	{
		/// <summary>Items of the tuple</summary>
		private readonly object[] m_items;

		/// <summary>Packed version of the tuple</summary>
		private Slice m_packed; //PERF: readonly struct

		internal FdbMemoizedTuple(object[] items, Slice packed)
		{
			Contract.Requires(items != null);
			Contract.Requires(packed.HasValue);

			m_items = items;
			m_packed = packed;
		}

		public int PackedSize
		{
			get { return m_packed.Count; }
		}

		public int Count
		{
			get { return m_items.Length; }
		}

		public object this[int index]
		{
			get { return m_items[FdbTuple.MapIndex(index, m_items.Length)]; }
		}

		public IFdbTuple this[int? fromIncluded, int? toExcluded]
		{
			get { return FdbTuple.Splice(this, fromIncluded, toExcluded); }
		}

		public void PackTo(ref SliceWriter writer)
		{
			if (m_packed.IsPresent)
			{
				writer.WriteBytes(m_packed);
			}
		}

		public Slice ToSlice()
		{
			return m_packed;
		}

		Slice IFdbKey.ToFoundationDbKey()
		{
			return this.ToSlice();
		}

		public FdbMemoizedTuple Copy()
		{
			return new FdbMemoizedTuple(
				(object[])(m_items.Clone()),
				m_packed.Memoize()
			);
		}

		public object[] ToArray()
		{
			var obj = new object[m_items.Length];
			Array.Copy(m_items, obj, obj.Length);
			return obj;
		}

		public R Get<R>(int index)
		{
			return FdbConverters.ConvertBoxed<R>(this[index]);
		}

		public R Last<R>()
		{
			int n = m_items.Length;
			if (n == 0) throw new InvalidOperationException("Tuple is emtpy");
			return FdbConverters.ConvertBoxed<R>(m_items[n - 1]);
		}

		IFdbTuple IFdbTuple.Append<T>(T value)
		{
			return this.Append<T>(value);
		}

		public FdbLinkedTuple<T> Append<T>(T value)
		{
			return new FdbLinkedTuple<T>(this, value);
		}

		public void CopyTo(object[] array, int offset)
		{
			Array.Copy(m_items, 0, array, offset, m_items.Length);
		}

		public IEnumerator<object> GetEnumerator()
		{
			return ((IList<object>)m_items).GetEnumerator();
		}

		System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
		{
			return this.GetEnumerator();
		}

		public override string ToString()
		{
			return FdbTuple.ToString(m_items, 0, m_items.Length);
		}

		public override bool Equals(object obj)
		{
			return Equals(obj as IFdbTuple);
		}

		public bool Equals(IFdbTuple other)
		{
			if (object.ReferenceEquals(other, null)) return false;

			var memoized = other as FdbMemoizedTuple;
			if (!object.ReferenceEquals(memoized, null))
			{
				return m_packed.Equals(memoized.m_packed);
			}

			return FdbTuple.Equals(this, other, SimilarValueComparer.Default);
		}

		public override int GetHashCode()
		{
			return m_packed.GetHashCode();
		}

		bool IStructuralEquatable.Equals(object other, System.Collections.IEqualityComparer comparer)
		{
			return FdbTuple.Equals(this, other, comparer);
		}

		int System.Collections.IStructuralEquatable.GetHashCode(System.Collections.IEqualityComparer comparer)
		{
			return FdbTuple.StructuralGetHashCode(this, comparer);
		}

	}
}
