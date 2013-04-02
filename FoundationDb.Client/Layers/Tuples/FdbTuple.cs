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

namespace FoundationDb.Client.Tuples
{

	/// <summary>Tuple that holds only one item</summary>
	/// <typeparam name="T1">Type of the item</typeparam>
	[DebuggerDisplay("({Item1})")]
	public struct FdbTuple<T1> : IFdbTuple
	{

		public readonly T1 Item1;

		public FdbTuple(T1 item1)
		{
			this.Item1 = item1;
		}

		public int Count { get { return 1; } }

		public object this[int index]
		{
			get
			{
				switch(index)
				{
					case 0: return this.Item1;
					default: throw new IndexOutOfRangeException();
				}
			}
		}

		public void PackTo(FdbBufferWriter writer)
		{
			FdbTuplePacker<T1>.SerializeTo(writer, this.Item1);
		}

		IFdbTuple IFdbTuple.Append<T2>(T2 value)
		{
			return this.Append<T2>(value);
		}

		public FdbTuple<T1, T2> Append<T2>(T2 value)
		{
			return new FdbTuple<T1, T2>(this.Item1, value);
		}

		public IEnumerator<object> GetEnumerator()
		{
			yield return this.Item1;
		}

		System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
		{
			return this.GetEnumerator();
		}

		public ArraySegment<byte> ToArraySegment()
		{
			var writer = new FdbBufferWriter();
			PackTo(writer);
			return writer.ToArraySegment();
		}

		public byte[] ToBytes()
		{
			var writer = new FdbBufferWriter();
			PackTo(writer);
			return writer.GetBytes();
		}

		public override string ToString()
		{
			return "(" + this.Item1 + ")";
		}

		public override int GetHashCode()
		{
			return this.Item1 != null ? this.Item1.GetHashCode() : -1;
		}

	}

	/// <summary>Tuple that holds a pair of items</summary>
	/// <typeparam name="T1">Type of the first item</typeparam>
	/// <typeparam name="T2">Type of the second item</typeparam>
	[DebuggerDisplay("({Item1}, {Item2})")]
	public struct FdbTuple<T1, T2> : IFdbTuple
	{
		public readonly T1 Item1;
		public readonly T2 Item2;

		public FdbTuple(T1 item1, T2 item2)
		{
			this.Item1 = item1;
			this.Item2 = item2;
		}

		public int Count { get { return 2; } }

		public object this[int index]
		{
			get
			{
				switch (index)
				{
					case 0: return this.Item1;
					case 1: return this.Item2;
					default: throw new IndexOutOfRangeException();
				}
			}
		}

		public void PackTo(FdbBufferWriter writer)
		{
			FdbTuplePacker<T1>.SerializeTo(writer, this.Item1);
			FdbTuplePacker<T2>.SerializeTo(writer, this.Item2);
		}

		IFdbTuple IFdbTuple.Append<T3>(T3 value)
		{
			return this.Append<T3>(value);
		}

		public FdbTuple<T1, T2, T3> Append<T3>(T3 value)
		{
			return new FdbTuple<T1, T2, T3>(this.Item1, this.Item2, value);
		}

		public IEnumerator<object> GetEnumerator()
		{
			yield return this.Item1;
			yield return this.Item2;
		}

		System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
		{
			return this.GetEnumerator();
		}

		public ArraySegment<byte> ToArraySegment()
		{
			var writer = new FdbBufferWriter();
			PackTo(writer);
			return writer.ToArraySegment();
		}

		public byte[] ToBytes()
		{
			var writer = new FdbBufferWriter();
			PackTo(writer);
			return writer.GetBytes();
		}

		public override string ToString()
		{
			return "(" + this.Item1 + ", " + this.Item2 + ")";
		}

		public override int GetHashCode()
		{
			int h;
			h = this.Item1 != null ? this.Item1.GetHashCode() : -1;
			h ^= this.Item2 != null ? this.Item2.GetHashCode() : -1;
			return h;
		}

	}

	/// <summary>Tuple that can hold three items</summary>
	/// <typeparam name="T1">Type of the first item</typeparam>
	/// <typeparam name="T2">Type of the second item</typeparam>
	/// <typeparam name="T3">Type of the third item</typeparam>
	[DebuggerDisplay("({Item1}, {Item2}, {Item3})")]
	public struct FdbTuple<T1, T2, T3> : IFdbTuple
	{
		public readonly T1 Item1;
		public readonly T2 Item2;
		public readonly T3 Item3;

		public FdbTuple(T1 item1, T2 item2, T3 item3)
		{
			this.Item1 = item1;
			this.Item2 = item2;
			this.Item3 = item3;
		}

		public int Count { get { return 3; } }

		public object this[int index]
		{
			get
			{
				switch (index)
				{
					case 0: return this.Item1;
					case 1: return this.Item2;
					case 2: return this.Item3;
					default: throw new IndexOutOfRangeException();
				}
			}
		}

		public void PackTo(FdbBufferWriter writer)
		{
			FdbTuplePacker<T1>.SerializeTo(writer, this.Item1);
			FdbTuplePacker<T2>.SerializeTo(writer, this.Item2);
			FdbTuplePacker<T3>.SerializeTo(writer, this.Item3);
		}

		IFdbTuple IFdbTuple.Append<T4>(T4 value)
		{
			return this.Append<T4>(value);
		}

		public FdbTuple<T1, T2, T3, T4> Append<T4>(T4 value)
		{
			return new FdbTuple<T1, T2, T3, T4>(this.Item1, this.Item2, this.Item3, value);
		}

		public IEnumerator<object> GetEnumerator()
		{
			yield return this.Item1;
			yield return this.Item2;
			yield return this.Item3;
		}

		System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
		{
			return this.GetEnumerator();
		}

		public ArraySegment<byte> ToArraySegment()
		{
			var writer = new FdbBufferWriter();
			PackTo(writer);
			return writer.ToArraySegment();
		}

		public byte[] ToBytes()
		{
			var writer = new FdbBufferWriter();
			PackTo(writer);
			return writer.GetBytes();
		}

		public override string ToString()
		{
			return new StringBuilder().Append('(').Append(this.Item1).Append(", ").Append(this.Item2).Append(", ").Append(this.Item3).Append(')').ToString();
		}

		public override int GetHashCode()
		{
			int h;
			h = this.Item1 != null ? this.Item1.GetHashCode() : -1;
			h ^= this.Item2 != null ? this.Item2.GetHashCode() : -1;
			h ^= this.Item3 != null ? this.Item3.GetHashCode() : -1;
			return h;
		}

	}

	/// <summary>Tuple that can hold four items</summary>
	/// <typeparam name="T1">Type of the first item</typeparam>
	/// <typeparam name="T2">Type of the second item</typeparam>
	/// <typeparam name="T3">Type of the third item</typeparam>
	/// <typeparam name="T4">Type of the fourth item</typeparam>
	[DebuggerDisplay("({Item1}, {Item2}, {Item3}, {Item4})")]
	public struct FdbTuple<T1, T2, T3, T4> : IFdbTuple
	{
		public readonly T1 Item1;
		public readonly T2 Item2;
		public readonly T3 Item3;
		public readonly T4 Item4;

		public FdbTuple(T1 item1, T2 item2, T3 item3, T4 item4)
		{
			this.Item1 = item1;
			this.Item2 = item2;
			this.Item3 = item3;
			this.Item4 = item4;
		}

		public int Count { get { return 4; } }

		public object this[int index]
		{
			get
			{
				switch (index)
				{
					case 0: return this.Item1;
					case 1: return this.Item2;
					case 2: return this.Item3;
					case 3: return this.Item4;
					default: throw new IndexOutOfRangeException();
				}
			}
		}

		public void PackTo(FdbBufferWriter writer)
		{
			FdbTuplePacker<T1>.SerializeTo(writer, this.Item1);
			FdbTuplePacker<T2>.SerializeTo(writer, this.Item2);
			FdbTuplePacker<T3>.SerializeTo(writer, this.Item3);
			FdbTuplePacker<T4>.SerializeTo(writer, this.Item4);
		}

		IFdbTuple IFdbTuple.Append<T5>(T5 value)
		{
			return this.Append<T5>(value);
		}

		public FdbTuple<T1, T2, T3, T4, T5> Append<T5>(T5 value)
		{
			return new FdbTuple<T1, T2, T3, T4, T5>(this.Item1, this.Item2, this.Item3, this.Item4, value);
		}

		public IEnumerator<object> GetEnumerator()
		{
			yield return this.Item1;
			yield return this.Item2;
			yield return this.Item3;
			yield return this.Item4;
		}

		System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
		{
			return this.GetEnumerator();
		}

		public ArraySegment<byte> ToArraySegment()
		{
			var writer = new FdbBufferWriter();
			PackTo(writer);
			return writer.ToArraySegment();
		}

		public byte[] ToBytes()
		{
			var writer = new FdbBufferWriter();
			PackTo(writer);
			return writer.GetBytes();
		}

		public override string ToString()
		{
			return new StringBuilder().Append('(').Append(this.Item1).Append(", ").Append(this.Item2).Append(", ").Append(this.Item3).Append(", ").Append(this.Item4).Append(')').ToString();
		}

		public override int GetHashCode()
		{
			int h;
			h = this.Item1 != null ? this.Item1.GetHashCode() : -1;
			h ^= this.Item2 != null ? this.Item2.GetHashCode() : -1;
			h ^= this.Item3 != null ? this.Item3.GetHashCode() : -1;
			h ^= this.Item4 != null ? this.Item4.GetHashCode() : -1;
			return h;
		}

	}

	/// <summary>Tuple that can hold four items</summary>
	/// <typeparam name="T1">Type of the first item</typeparam>
	/// <typeparam name="T2">Type of the second item</typeparam>
	/// <typeparam name="T3">Type of the third item</typeparam>
	/// <typeparam name="T4">Type of the fourth item</typeparam>
	[DebuggerDisplay("({Item1}, {Item2}, {Item3}, {Item4})")]
	public struct FdbTuple<T1, T2, T3, T4, T5> : IFdbTuple
	{
		public readonly T1 Item1;
		public readonly T2 Item2;
		public readonly T3 Item3;
		public readonly T4 Item4;
		public readonly T5 Item5;

		public FdbTuple(T1 item1, T2 item2, T3 item3, T4 item4, T5 item5)
		{
			this.Item1 = item1;
			this.Item2 = item2;
			this.Item3 = item3;
			this.Item4 = item4;
			this.Item5 = item5;
		}

		public int Count { get { return 5; } }

		public object this[int index]
		{
			get
			{
				switch (index)
				{
					case 0: return this.Item1;
					case 1: return this.Item2;
					case 2: return this.Item3;
					case 3: return this.Item4;
					case 4: return this.Item5;
					default: throw new IndexOutOfRangeException();
				}
			}
		}

		public void PackTo(FdbBufferWriter writer)
		{
			FdbTuplePacker<T1>.SerializeTo(writer, this.Item1);
			FdbTuplePacker<T2>.SerializeTo(writer, this.Item2);
			FdbTuplePacker<T3>.SerializeTo(writer, this.Item3);
			FdbTuplePacker<T4>.SerializeTo(writer, this.Item4);
			FdbTuplePacker<T5>.SerializeTo(writer, this.Item5);
		}

		IFdbTuple IFdbTuple.Append<T6>(T6 value)
		{
			var items = new List<object>(6);
			items.Add(this.Item1);
			items.Add(this.Item2);
			items.Add(this.Item3);
			items.Add(this.Item4);
			items.Add(this.Item5);
			items.Add(value);
			return new FdbTupleList(items);
		}

		public IEnumerator<object> GetEnumerator()
		{
			yield return this.Item1;
			yield return this.Item2;
			yield return this.Item3;
			yield return this.Item4;
			yield return this.Item5;
		}

		System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
		{
			return this.GetEnumerator();
		}

		public ArraySegment<byte> ToArraySegment()
		{
			var writer = new FdbBufferWriter();
			PackTo(writer);
			return writer.ToArraySegment();
		}

		public byte[] ToBytes()
		{
			var writer = new FdbBufferWriter();
			PackTo(writer);
			return writer.GetBytes();
		}

		public override string ToString()
		{
			return new StringBuilder().Append('(').Append(this.Item1).Append(", ").Append(this.Item2).Append(", ").Append(this.Item3).Append(", ").Append(this.Item4).Append(", ").Append(this.Item5).Append(')').ToString();
		}

		public override int GetHashCode()
		{
			int h;
			h = this.Item1 != null ? this.Item1.GetHashCode() : -1;
			h ^= this.Item2 != null ? this.Item2.GetHashCode() : -1;
			h ^= this.Item3 != null ? this.Item3.GetHashCode() : -1;
			h ^= this.Item4 != null ? this.Item4.GetHashCode() : -1;
			h ^= this.Item5 != null ? this.Item5.GetHashCode() : -1;
			return h;
		}

	}

	/// <summary>Factory class for Tuples</summary>
	public static class FdbTuple
	{
		/// <summary>Empty tuple</summary>
		/// <remarks>Not to be mistaken with a 1-tuple containing 'null' !</remarks>
		public static readonly IFdbTuple Empty = new EmptyTuple();

		/// <summary>Empty tuple (singleton that is used as a base for other tuples)</summary>
		internal sealed class EmptyTuple : IFdbTuple
		{

			public int Count
			{
				get { return 0; }
			}

			object IFdbTuple.this[int index]
			{
				get { throw new IndexOutOfRangeException(); }
			}

			IFdbTuple IFdbTuple.Append<T1>(T1 value)
			{
				return this.Append<T1>(value);
			}

			public FdbTuple<T1> Append<T1>(T1 value)
			{
				return new FdbTuple<T1>(value);
			}

			public IFdbTuple AppendRange(IFdbTuple value)
			{
				return value;
			}

			public void PackTo(FdbBufferWriter writer)
			{
				//NO-OP
			}

			public byte[] ToBytes()
			{
				return Fdb.Empty.Array;
			}

			public IEnumerator<object> GetEnumerator()
			{
				yield break;
			}

			System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
			{
				return this.GetEnumerator();
			}

			public override string ToString()
			{
				return "()";
			}

			public override int GetHashCode()
			{
				return 0;
			}

		}

		/// <summary>Create a new 1-tuple, holding only one item</summary>
		public static FdbTuple<T1> Create<T1>(T1 item1)
		{
			return new FdbTuple<T1>(item1);
		}

		/// <summary>Create a new 2-tuple, holding two items</summary>
		public static FdbTuple<T1, T2> Create<T1, T2>(T1 item1, T2 item2)
		{
			return new FdbTuple<T1, T2>(item1, item2);
		}

		/// <summary>Create a new 3-tuple, holding three items</summary>
		public static FdbTuple<T1, T2, T3> Create<T1, T2, T3>(T1 item1, T2 item2, T3 item3)
		{
			return new FdbTuple<T1, T2, T3>(item1, item2, item3);
		}

		/// <summary>Create a new 4-tuple, holding four items</summary>
		public static FdbTuple<T1, T2, T3, T4> Create<T1, T2, T3, T4>(T1 item1, T2 item2, T3 item3, T4 item4)
		{
			return new FdbTuple<T1, T2, T3, T4>(item1, item2, item3, item4);
		}

		/// <summary>Create a new 5-tuple, holding five items</summary>
		public static FdbTuple<T1, T2, T3, T4, T5> Create<T1, T2, T3, T4, T5>(T1 item1, T2 item2, T3 item3, T4 item4, T5 item5)
		{
			return new FdbTuple<T1, T2, T3, T4, T5>(item1, item2, item3, item4, item5);
		}

		/// <summary>Create a new N-tuple, from N items</summary>
		public static FdbTupleList Create(params object[] items)
		{
			var list = new List<object>(items);
			return new FdbTupleList(list);
		}

		/// <summary>Create a new N-tuple from a sequence of items</summary>
		public static FdbTupleList Create(IEnumerable<object> items)
		{
			if (items == null) throw new ArgumentNullException("items");

			var tuple = items as FdbTupleList;
			if (tuple == null)
			{
				tuple = new FdbTupleList(new List<object>(items));
			}
			return tuple;
		}

	}

}
