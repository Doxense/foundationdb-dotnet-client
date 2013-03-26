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
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace FoundationDb.Client.Tuples
{

	public interface IFdbTuplePackable
	{
		/// <summary>Append al "items" of this tuple at the end of a buffer</summary>
		/// <param name="buffer">Buffer that will received the packed bytes of this tuple</param>
		void PackTo(BinaryWriteBuffer writer);
	}

	public interface IFdbTuple : IFdbTuplePackable, IEnumerable<object>
	{
		/// <summary>Returns the number of "items" in the Tuple</summary>
		int Count { get; }

		/// <summary>Create a new Tuple by appending a new value at the end the this tuple</summary>
		/// <typeparam name="T">Type of the new value</typeparam>
		/// <param name="value">Value that will be appended at the end</param>
		/// <returns>New tuple with the new value</returns>
		IFdbTuple Append<T>(T value);
	}

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

		public void PackTo(BinaryWriteBuffer writer)
		{
			FdbTuplePacker<T1>.SerializeTo(writer, this.Item1);
		}

		IFdbTuple IFdbTuple.Append<T2>(T2 value)
		{
			return new FdbTuple<T1, T2>(this.Item1, value);
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
			var writer = new BinaryWriteBuffer();
			PackTo(writer);
			return writer.ToArraySegment();
		}

		public byte[] ToBytes()
		{
			var writer = new BinaryWriteBuffer();
			PackTo(writer);
			return writer.GetBytes();
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

		public void PackTo(BinaryWriteBuffer writer)
		{
			FdbTuplePacker<T1>.SerializeTo(writer, this.Item1);
			FdbTuplePacker<T2>.SerializeTo(writer, this.Item2);
		}

		IFdbTuple IFdbTuple.Append<T3>(T3 value)
		{
			return new FdbTuple<T1, T2, T3>(this.Item1, this.Item2, value);
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
			var writer = new BinaryWriteBuffer();
			PackTo(writer);
			return writer.ToArraySegment();
		}

		public byte[] ToBytes()
		{
			var writer = new BinaryWriteBuffer();
			PackTo(writer);
			return writer.GetBytes();
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

		public void PackTo(BinaryWriteBuffer writer)
		{
			FdbTuplePacker<T1>.SerializeTo(writer, this.Item1);
			FdbTuplePacker<T2>.SerializeTo(writer, this.Item2);
			FdbTuplePacker<T3>.SerializeTo(writer, this.Item3);
		}

		IFdbTuple IFdbTuple.Append<T4>(T4 value)
		{
			return new FdbTuple<T1, T2, T3, T4>(this.Item1, this.Item2, this.Item3, value);
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
			var writer = new BinaryWriteBuffer();
			PackTo(writer);
			return writer.ToArraySegment();
		}

		public byte[] ToBytes()
		{
			var writer = new BinaryWriteBuffer();
			PackTo(writer);
			return writer.GetBytes();
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

		public void PackTo(BinaryWriteBuffer writer)
		{
			FdbTuplePacker<T1>.SerializeTo(writer, this.Item1);
			FdbTuplePacker<T2>.SerializeTo(writer, this.Item2);
			FdbTuplePacker<T3>.SerializeTo(writer, this.Item3);
			FdbTuplePacker<T4>.SerializeTo(writer, this.Item4);
		}

		IFdbTuple IFdbTuple.Append<T5>(T5 value)
		{
			var items = new List<object>(5);
			items.Add(this.Item1);
			items.Add(this.Item2);
			items.Add(this.Item3);
			items.Add(this.Item4);
			items.Add(value);
			return new FdbTupleList(items);
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
			var writer = new BinaryWriteBuffer();
			PackTo(writer);
			return writer.ToArraySegment();
		}

		public byte[] ToBytes()
		{
			var writer = new BinaryWriteBuffer();
			PackTo(writer);
			return writer.GetBytes();
		}

	}

	/// <summary>Tuple that can hold any number of items</summary>
	public class FdbTupleList : IFdbTuple
	{
		private static readonly List<object> EmptyList = new List<object>(); 

		/// <summary>List of the items in the tuple.</summary>
		/// <remarks>It is supposed to be immutable!</remarks>
		private readonly List<object> Items;

		public FdbTupleList(params object[] items)
		{
			this.Items = items.Length > 0 ? new List<object>(items) : EmptyList;
		}

		internal FdbTupleList(List<object> items)
		{
			this.Items = items;
		}

		public int Count
		{
			get { return this.Items.Count; }
		}

		public void PackTo(BinaryWriteBuffer writer)
		{
			foreach (var item in this.Items)
			{
				FdbTuplePackers.SerializeObjectTo(writer, item);
			}
		}

		public IFdbTuple Append<T>(T value)
		{
			var items = new List<object>(this.Count + 1);
			items.AddRange(this.Items);
			items.Add(value);
			return new FdbTupleList(items);
		}

		public IEnumerator<object> GetEnumerator()
		{
			return this.Items.GetEnumerator();
		}

		System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
		{
			return this.GetEnumerator();
		}

		public ArraySegment<byte> ToArraySegment()
		{
			var writer = new BinaryWriteBuffer();
			PackTo(writer);
			return writer.ToArraySegment();
		}

		public byte[] ToBytes()
		{
			var writer = new BinaryWriteBuffer();
			PackTo(writer);
			return writer.GetBytes();
		}
	}

	public static class FdbTuple
	{
		public static FdbTuple<T1> Create<T1>(T1 item1)
		{
			return new FdbTuple<T1>(item1);
		}

		public static FdbTuple<T1, T2> Create<T1, T2>(T1 item1, T2 item2)
		{
			return new FdbTuple<T1, T2>(item1, item2);
		}

		public static FdbTuple<T1, T2, T3> Create<T1, T2, T3>(T1 item1, T2 item2, T3 item3)
		{
			return new FdbTuple<T1, T2, T3>(item1, item2, item3);
		}

		public static FdbTuple<T1, T2, T3, T4> Create<T1, T2, T3, T4>(T1 item1, T2 item2, T3 item3, T4 item4)
		{
			return new FdbTuple<T1, T2, T3, T4>(item1, item2, item3, item4);
		}

		public static IFdbTuple Create(params object[] items)
		{
			var list = new List<object>(items);
			return new FdbTupleList(list);
		}

		public static IFdbTuple Create(ICollection<object> items)
		{
			if (items == null) throw new ArgumentNullException("items");
			var list = new List<object>(items.Count);
			list.AddRange(items);
			return new FdbTupleList(list);
		}

		public static IFdbTuple Create(IEnumerable<object> items)
		{
			if (items == null) throw new ArgumentNullException("items");
			var list = new FdbTupleList(items);
			return new FdbTupleList(list);
		}

		public static ArraySegment<byte> ToArraySegment(this IFdbTuple tuple)
		{
			var writer = new BinaryWriteBuffer();
			tuple.PackTo(writer);
			return writer.ToArraySegment();
		}

		public static byte[] ToBytes(this IFdbTuple tuple)
		{
			var writer = new BinaryWriteBuffer();
			tuple.PackTo(writer);
			return writer.GetBytes();
		}

	}

	public static class FdbTupleExtensions
	{
		public static void Set(this FdbTransaction transaction, IFdbTuple tuple, byte[] value)
		{
			transaction.Set(tuple.ToArraySegment(), new ArraySegment<byte>(value));
		}

		public static void Set(this FdbTransaction transaction, IFdbTuple tuple, string value)
		{
			transaction.Set(tuple.ToArraySegment(), FdbCore.GetValueBytes(value));
		}

		public static Task<byte[]> GetAsync(this FdbTransaction transaction, IFdbTuple tuple, bool snapshot = false, CancellationToken ct = default(CancellationToken))
		{
			return transaction.GetAsync(tuple.ToArraySegment(), snapshot, ct);
		}

		public static byte[] Get(this FdbTransaction transaction, IFdbTuple tuple, bool snapshot = false, CancellationToken ct = default(CancellationToken))
		{
			return transaction.Get(tuple.ToArraySegment(), snapshot, ct);
		}

	}

}
