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
	using JetBrains.Annotations;
	using System;
	using System.Collections;
	using System.Collections.Generic;
	using System.ComponentModel;
	using System.Diagnostics;
	using System.Text;

	/// <summary>Tuple that can hold four items</summary>
	/// <typeparam name="T1">Type of the first item</typeparam>
	/// <typeparam name="T2">Type of the second item</typeparam>
	/// <typeparam name="T3">Type of the third item</typeparam>
	/// <typeparam name="T4">Type of the fourth item</typeparam>
	[ImmutableObject(true), DebuggerDisplay("{ToString()}")]
	public struct FdbTuple<T1, T2, T3, T4> : IFdbTuple
	{
		// This is mostly used by code that create a lot of temporary quartets, to reduce the pressure on the Garbage Collector by allocating them on the stack.
		// Please note that if you return an FdbTuple<T> as an IFdbTuple, it will be boxed by the CLR and all memory gains will be lost

		/// <summary>First element of the quartet</summary>
		public readonly T1 Item1;
		/// <summary>Second element of the quartet</summary>
		public readonly T2 Item2;
		/// <summary>Third element of the quartet</summary>
		public readonly T3 Item3;
		/// <summary>Fourth and last element of the quartet</summary>
		public readonly T4 Item4;

		/// <summary>Create a tuple containing for items</summary>
		[DebuggerStepThrough]
		public FdbTuple(T1 item1, T2 item2, T3 item3, T4 item4)
		{
			this.Item1 = item1;
			this.Item2 = item2;
			this.Item3 = item3;
			this.Item4 = item4;
		}

		/// <summary>Number of items in this tuple</summary>
		public int Count { get { return 4; } }

		/// <summary>Return the Nth item in this tuple</summary>
		public object this[int index]
		{
			get
			{
				switch (index)
				{
					case 0: case -4: return this.Item1;
					case 1: case -3: return this.Item2;
					case 2: case -2: return this.Item3;
					case 3: case -1: return this.Item4;
					default: FdbTuple.FailIndexOutOfRange(index, 4); return null;
				}
			}
		}

		public IFdbTuple this[int? fromIncluded, int? toExcluded]
		{
			get { return FdbTuple.Splice(this, fromIncluded, toExcluded); }
		}

		/// <summary>Return the typed value of an item of the tuple, given its position</summary>
		/// <typeparam name="R">Expected type of the item</typeparam>
		/// <param name="index">Position of the item (if negative, means relative from the end)</param>
		/// <returns>Value of the item at position <paramref name="index"/>, adapted into type <typeparamref name="R"/>.</returns>
		public R Get<R>(int index)
		{
			switch(index)
			{
					case 0: case -4: return FdbConverters.Convert<T1, R>(this.Item1);
					case 1: case -3: return FdbConverters.Convert<T2, R>(this.Item2);
					case 2: case -2: return FdbConverters.Convert<T3, R>(this.Item3);
					case 3: case -1: return FdbConverters.Convert<T4, R>(this.Item4);
					default: FdbTuple.FailIndexOutOfRange(index, 4); return default(R);
			}
		}

		public void PackTo(ref TupleWriter writer)
		{
			FdbTuplePacker<T1>.Encoder(ref writer, this.Item1);
			FdbTuplePacker<T2>.Encoder(ref writer, this.Item2);
			FdbTuplePacker<T3>.Encoder(ref writer, this.Item3);
			FdbTuplePacker<T4>.Encoder(ref writer, this.Item4);
		}

		IFdbTuple IFdbTuple.Append<T5>(T5 value)
		{
			// the caller doesn't care about the return type, so just box everything into a list tuple
			return new FdbListTuple(new object[5] { this.Item1, this.Item2, this.Item3, this.Item4, value }, 0, 5);
		}

		/// <summary>Appends a single new item at the end of the current tuple.</summary>
		/// <param name="value">Value that will be added as an embedded item</param>
		/// <returns>New tuple with one extra item</returns>
		/// <remarks>If <paramref name="value"/> is a tuple, and you want to append the *items*  of this tuple, and not the tuple itself, please call <see cref="Concat"/>!</remarks>
		[NotNull]
		public FdbLinkedTuple<T5> Append<T5>(T5 value)
		{
			// the caller probably cares about the return type, since it is using a struct, but whatever tuple type we use will end up boxing this tuple on the heap, and we will loose type information.
			// but, by returning a FdbLinkedTuple<T5>, the tuple will still remember the exact type, and efficiently serializer/convert the values (without having to guess the type)
			return new FdbLinkedTuple<T5>(this, value);
		}

		/// <summary>Appends the items of a tuple at the end of the current tuple.</summary>
		/// <param name="tuple">Tuple whose items are to be appended at the end</param>
		/// <returns>New tuple composed of the current tuple's items, followed by <paramref name="tuple"/>'s items</returns>
		public IFdbTuple Concat(IFdbTuple tuple)
		{
			return FdbTuple.Concat(this, tuple);
		}

		/// <summary>Copy all the items of this tuple into an array at the specified offset</summary>
		public void CopyTo(object[] array, int offset)
		{
			array[offset] = this.Item1;
			array[offset + 1] = this.Item2;
			array[offset + 2] = this.Item3;
			array[offset + 3] = this.Item4;
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

		public Slice ToSlice()
		{
			return FdbTuple.Pack(this.Item1, this.Item2, this.Item3, this.Item4);
		}

		Slice IFdbKey.ToFoundationDbKey()
		{
			return this.ToSlice();
		}

		public override string ToString()
		{
			return new StringBuilder(48).Append('(')
				.Append(FdbTuple.Stringify(this.Item1)).Append(", ")
				.Append(FdbTuple.Stringify(this.Item2)).Append(", ")
				.Append(FdbTuple.Stringify(this.Item3)).Append(", ")
				.Append(FdbTuple.Stringify(this.Item4)).Append(')')
				.ToString();
		}

		public override bool Equals(object obj)
		{
			return obj != null && ((IStructuralEquatable)this).Equals(obj, SimilarValueComparer.Default);
		}

		public bool Equals(IFdbTuple other)
		{
			return other != null && ((IStructuralEquatable)this).Equals(other, SimilarValueComparer.Default);
		}

		public override int GetHashCode()
		{
			return ((IStructuralEquatable)this).GetHashCode(SimilarValueComparer.Default);
		}

		public static bool operator ==(FdbTuple<T1, T2, T3, T4> left, FdbTuple<T1, T2, T3, T4> right)
		{
			var comparer = SimilarValueComparer.Default;
			return comparer.Equals(left.Item1, right.Item1)
				&& comparer.Equals(left.Item2, right.Item2)
				&& comparer.Equals(left.Item3, right.Item3)
				&& comparer.Equals(left.Item4, right.Item4);
		}

		public static bool operator !=(FdbTuple<T1, T2, T3, T4> left, FdbTuple<T1, T2, T3, T4> right)
		{
			var comparer = SimilarValueComparer.Default;
			return !comparer.Equals(left.Item1, right.Item1)
				|| !comparer.Equals(left.Item2, right.Item2)
				|| !comparer.Equals(left.Item3, right.Item3)
				|| !comparer.Equals(left.Item4, right.Item4);
		}

		bool IStructuralEquatable.Equals(object other, IEqualityComparer comparer)
		{
			if (other == null) return false;
			if (other is FdbTuple<T1, T2, T3, T4>)
			{
				var tuple = (FdbTuple<T1, T2, T3, T4>)other;
				return comparer.Equals(this.Item1, tuple.Item1)
					&& comparer.Equals(this.Item2, tuple.Item2)
					&& comparer.Equals(this.Item3, tuple.Item3)
					&& comparer.Equals(this.Item4, tuple.Item4);
			}
			return FdbTuple.Equals(this, other, comparer);
		}

		int IStructuralEquatable.GetHashCode(IEqualityComparer comparer)
		{
			return FdbTuple.CombineHashCodes(
				comparer.GetHashCode(this.Item1),
				comparer.GetHashCode(this.Item2),
				comparer.GetHashCode(this.Item3),
				comparer.GetHashCode(this.Item4)
			);
		}

	}

}
