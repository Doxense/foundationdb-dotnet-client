﻿#region BSD Licence
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

	/// <summary>Tuple that represents the concatenation of two tuples</summary>
	[DebuggerDisplay("{ToString()}")]
	public sealed class FdbJoinedTuple : IFdbTuple
	{
		// Uses cases: joining a 'subspace' tuple (customerId, 'Users', ) with a 'key' tuple (userId, 'Contacts', 123, )

		/// <summary>First tuple (first N items)</summary>
		public readonly IFdbTuple Head;

		/// <summary>Second tuple (last M items)</summary>
		public readonly IFdbTuple Tail;

		/// <summary>Offset at which the Tail tuple starts. Items are in Head tuple if index &lt; split. Items are in Tail tuple if index &gt;= split.</summary>
		private readonly int m_split;

		/// <summary>Total size of the tuple (sum of the size of the two inner tuples)</summary>
		private readonly int m_count;

		public FdbJoinedTuple(IFdbTuple head, IFdbTuple tail)
		{
			if (head == null) throw new ArgumentNullException("head");
			if (tail == null) throw new ArgumentNullException("tail");

			this.Head = head;
			this.Tail = tail;
			m_split = head.Count;
			m_count = m_split + tail.Count;
		}

		public void PackTo(ref SliceWriter writer)
		{
			this.Head.PackTo(ref writer);
			this.Tail.PackTo(ref writer);
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
			return FdbTuple.ToString(this);
		}

		public int Count
		{
			get { return m_count; }
		}

		public object this[int index]
		{
			get
			{
				index = FdbTuple.MapIndex(index, m_count);
				return index < m_split ? this.Head[index] : this.Tail[index - m_split];
			}
		}

		public IFdbTuple this[int? fromIncluded, int? toExcluded]
		{
			get
			{
				int begin = fromIncluded.HasValue ? FdbTuple.MapIndexBounded(fromIncluded.Value, m_count) : 0;
				int end = toExcluded.HasValue ? FdbTuple.MapIndexBounded(toExcluded.Value, m_count) : m_count;

				if (end <= begin) return FdbTuple.Empty;

				int p = this.Head.Count;
				if (begin >= p)
				{ // all selected items are in the tail
					return this.Tail[begin - p, end - p];
				}
				else if (end <= p)
				{ // all selected items are in the head
					return this.Head[begin, end];
				}
				else
				{ // selected items are both in head and tail
					return new FdbJoinedTuple(this.Head[begin, null], this.Tail[null, end - p]);
				}
			}
		}

		public T Get<T>(int index)
		{
			index = FdbTuple.MapIndex(index, m_count);
			return index < m_split ? this.Head.Get<T>(index) : this.Tail.Get<T>(index - m_split);
		}

		public T Last<T>()
		{
			if (this.Tail.Count > 0)
				return this.Tail.Last<T>();
			else
				return this.Head.Last<T>();
		}

		IFdbTuple IFdbTuple.Append<T>(T value)
		{
			return new FdbLinkedTuple<T>(this, value);
		}

		public FdbLinkedTuple<T> Append<T>(T value)
		{
			return new FdbLinkedTuple<T>(this, value);
		}

		public void CopyTo(object[] array, int offset)
		{
			this.Head.CopyTo(array, offset);
			this.Tail.CopyTo(array, offset + m_split);
		}

		public IEnumerator<object> GetEnumerator()
		{
			foreach (var item in this.Head)
			{
				yield return item;
			}
			foreach (var item in this.Tail)
			{
				yield return item;
			}
		}

		System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
		{
			return this.GetEnumerator();
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

		bool System.Collections.IStructuralEquatable.Equals(object other, System.Collections.IEqualityComparer comparer)
		{
			if (object.ReferenceEquals(this, other)) return true;
			if (other == null) return false;

			var tuple = other as IFdbTuple;
			if (!object.ReferenceEquals(tuple, null))
			{
				if (tuple.Count != m_count) return false;

				using (var iter = tuple.GetEnumerator())
				{
					foreach (var item in this.Head)
					{
						if (!iter.MoveNext() || !comparer.Equals(item, iter.Current)) return false;
					}
					foreach (var item in this.Tail)
					{
						if (!iter.MoveNext() || !comparer.Equals(item, iter.Current)) return false;
					}
					return !iter.MoveNext();
				}
			}

			return false;
		}

		int System.Collections.IStructuralEquatable.GetHashCode(System.Collections.IEqualityComparer comparer)
		{
			return FdbTuple.CombineHashCodes(
				this.Head != null ? this.Head.GetHashCode(comparer) : 0,
				this.Tail != null ? this.Tail.GetHashCode(comparer) : 0
			);
		}
	}

}
