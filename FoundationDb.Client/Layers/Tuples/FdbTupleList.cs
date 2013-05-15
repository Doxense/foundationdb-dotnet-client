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
	* Neither the name of the <organization> nor the
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

using FoundationDb.Client.Utils;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace FoundationDb.Client.Tuples
{

	/// <summary>Tuple that can hold any number of items</summary>
	public class FdbTupleList : IFdbTuple, IEquatable<FdbTupleList>
	{
		private static readonly List<object> EmptyList = new List<object>();

		/// <summary>List of the items in the tuple.</summary>
		/// <remarks>It is supposed to be immutable!</remarks>
		private readonly List<object> Items;

		private int? HashCode;

		/// <summary>Create a new tuple from a list of items (copied)</summary>
		public FdbTupleList(params object[] items)
		{
			this.Items = items.Length > 0 ? new List<object>(items) : EmptyList;
		}

		/// <summary>Create a new tuple from a sequence of items (copied)</summary>
		public FdbTupleList(IEnumerable<object> items)
		{
			this.Items = new List<object>(items);
		}

		/// <summary>Wrap a List of items</summary>
		/// <remarks>The list should not mutate and should not be exposed to anyone else!</remarks>
		internal FdbTupleList(List<object> items)
		{
			this.Items = items;
		}

		#region IFdbKey Members...

		public void PackTo(FdbBufferWriter writer)
		{
			foreach (var item in this.Items)
			{
				FdbTuplePackers.SerializeObjectTo(writer, item);
			}
		}

		#endregion

		public int Count
		{
			get { return this.Items.Count; }
		}

		public object this[int index]
		{
			get { return this.Items[index]; }
		}

		IFdbTuple IFdbTuple.Append<T>(T value)
		{
			return this.Append<T>(value);
		}

		public FdbTupleList Append<T>(T value)
		{
			var list = new List<object>(this.Count + 1);
			list.AddRange(this.Items);
			list.Add(value);
			return new FdbTupleList(list);
		}

		public FdbTupleList AppendRange(object[] items)
		{
			var list = new List<object>(this.Count + items.Length);
			list.AddRange(this.Items);
			list.AddRange(items);
			return new FdbTupleList(list);
		}

		public FdbTupleList Concat(FdbTupleList tuple)
		{
			if (tuple == null) throw new ArgumentNullException("tuple");

			var items = tuple.Items;
			if (items.Count == 0) return this;

			var list = new List<object>(this.Count + items.Count);
			list.AddRange(this.Items);
			list.AddRange(items);
			return new FdbTupleList(list);
		}

		public FdbTupleList Concat(IFdbTuple tuple)
		{
			var _ = tuple as FdbTupleList;
			if (_ != null) return Concat(_);

			int count = tuple.Count;
			if (count == 0) return this;

			var list = new List<object>(this.Count + count);
			list.AddRange(this.Items);
			list.AddRange(tuple);
			return new FdbTupleList(list);
		}

		public IEnumerator<object> GetEnumerator()
		{
			return this.Items.GetEnumerator();
		}

		System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
		{
			return this.GetEnumerator();
		}

		public Slice ToSlice()
		{
			var writer = new FdbBufferWriter();
			PackTo(writer);
			return writer.GetBytes();
		}

		public override string ToString()
		{
			var sb = new StringBuilder();
			sb.Append('(');
			bool tail = false;
			foreach (var item in this.Items)
			{
				if (tail) { sb.Append(", "); } else { tail = true; }
				sb.Append(item);
			}
			return sb.Append(')').ToString();
		}

		public override int GetHashCode()
		{
			if (!this.HashCode.HasValue)
			{
				int h = 0;
				foreach (var item in this.Items)
				{
					h ^= item != null ? item.GetHashCode() : -1;
				}
				this.HashCode = h;
			}
			return this.HashCode.GetValueOrDefault();
		}

		public override bool Equals(object obj)
		{
			var tupleList = obj as FdbTupleList;
			if (tupleList != null) return this.Equals(tupleList);
			var tuple = obj as IFdbTuple;
			if (tuple != null) return this.Equals(tuple);
			return false;
		}

		private bool CompareItems(IEnumerable<object> theirs)
		{
			int p = 0;
			var mine = this.Items;
			foreach (var item in theirs)
			{
				if (item == null)
				{
					if (mine[p] != null) return false;
				}
				else
				{
					if (!item.Equals(mine[p])) return false;
				}
				p++;
			}
			return true;
		}

		public bool Equals(FdbTupleList tuple)
		{
			if (object.ReferenceEquals(this, tuple)) return true;
			if (object.ReferenceEquals(tuple, null) || tuple.Count != this.Count || this.GetHashCode() != tuple.GetHashCode()) return false;

			return CompareItems(tuple.Items);
		}

		public bool Equals(IFdbTuple tuple)
		{
			if (object.ReferenceEquals(this, tuple)) return true;
			if (object.ReferenceEquals(tuple, null) || tuple.Count != this.Count) return false;

			return CompareItems(tuple);
		}

	}

}
