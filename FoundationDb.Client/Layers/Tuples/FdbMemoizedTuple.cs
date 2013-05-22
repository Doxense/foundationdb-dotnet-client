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

using FoundationDb.Client.Converters;
using FoundationDb.Client.Utils;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace FoundationDb.Client.Tuples
{

	public sealed class FdbMemoizedTuple : IFdbTuple
	{
		internal readonly object[] Items;

		internal readonly Slice Packed;

		internal FdbMemoizedTuple(object[] items, Slice packed)
		{
			Contract.Requires(items != null);
			Contract.Requires(packed.HasValue);

			this.Items = items;
			this.Packed = packed;
		}

		public int PackedSize
		{
			get { return this.Packed.Count; }
		}

		public void PackTo(FdbBufferWriter writer)
		{
			if (this.Packed.Count > 0)
			{
				writer.WriteBytes(this.Packed);
			}
		}

		public Slice ToSlice()
		{
			return this.Packed;
		}

		public int Count
		{
			get { return this.Items.Length; }
		}

		public object this[int index]
		{
			get { return this.Items[FdbTuple.MapIndex(index, this.Items.Length)]; }
		}

		public R Get<R>(int index)
		{
			return FdbConverters.ConvertBoxed<R>(this[index]);
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
			Array.Copy(this.Items, 0, array, offset, this.Items.Length);
		}

		public IEnumerator<object> GetEnumerator()
		{
			return ((IList<object>)this.Items).GetEnumerator();
		}

		System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
		{
			return this.GetEnumerator();
		}

		public override string ToString()
		{
			return FdbTuple.ToString(this.Items);
		}

	}

	[DebuggerDisplay("Last={this.Last}, Depth={this.Depth}")]
	public sealed class FdbLinkedTuple<T> : IFdbTuple
	{
		public readonly IFdbTuple Head;
		public readonly int Depth;
		public readonly T Tail;

		internal FdbLinkedTuple(IFdbTuple head, T tail)
		{
			Contract.Requires(head != null);

			this.Head = head;
			this.Tail = tail;
			this.Depth = head.Count + 1;
		}

		public void PackTo(FdbBufferWriter writer)
		{
			this.Head.PackTo(writer);
			FdbTuplePacker<T>.SerializeTo(writer, this.Tail);
		}

		public Slice ToSlice()
		{
			var writer = new FdbBufferWriter();
			PackTo(writer);
			return writer.ToSlice();
		}

		public int Count
		{
			get { return this.Depth + 1; }
		}

		public object this[int index]
		{
			get
			{
				if (index == this.Depth || index == -1) return this.Tail;
				return this.Head[index];
			}
		}

		public R Get<R>(int index)
		{
			if (index == this.Depth || index == -1) return FdbConverters.Convert<T, R>(this.Tail);
			return this.Head.Get<R>(index);
		}

		IFdbTuple IFdbTuple.Append<R>(R value)
		{
			return this.Append<R>(value);
		}

		public FdbLinkedTuple<R> Append<R>(R value)
		{
			return new FdbLinkedTuple<R>(this, value);
		}

		public void CopyTo(object[] array, int offset)
		{
			this.Head.CopyTo(array, offset);
			array[offset + this.Depth] = this.Tail;
		}

		public IEnumerator<object> GetEnumerator()
		{
			foreach (var item in this.Head)
			{
				yield return item;
			}
			yield return this.Tail;
		}

		System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
		{
			return this.GetEnumerator();
		}

		public override string ToString()
		{
			return FdbTuple.ToString(this);
		}

	}
}
