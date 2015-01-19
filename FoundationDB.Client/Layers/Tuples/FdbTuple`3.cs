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
	using JetBrains.Annotations;
	using System;
	using System.Collections;
	using System.Collections.Generic;
	using System.ComponentModel;
	using System.Diagnostics;
	using System.Text;

	/// <summary>Tuple that can hold three items</summary>
	/// <typeparam name="T1">Type of the first item</typeparam>
	/// <typeparam name="T2">Type of the second item</typeparam>
	/// <typeparam name="T3">Type of the third item</typeparam>
	[ImmutableObject(true), DebuggerDisplay("{ToString()}")]
	public struct FdbTuple<T1, T2, T3> : IFdbTuple
	{
		// This is mostly used by code that create a lot of temporary triplet, to reduce the pressure on the Garbage Collector by allocating them on the stack.
		// Please note that if you return an FdbTuple<T> as an IFdbTuple, it will be boxed by the CLR and all memory gains will be lost

		/// <summary>First element of the triplet</summary>
		public readonly T1 Item1;
		/// <summary>Second element of the triplet</summary>
		public readonly T2 Item2;
		/// <summary>Third and last elemnt of the triplet</summary>
		public readonly T3 Item3;

		[DebuggerStepThrough]
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
					case 0: case -3: return this.Item1;
					case 1: case -2: return this.Item2;
					case 2: case -1: return this.Item3;
					default: FdbTuple.FailIndexOutOfRange(index, 3); return null;
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
					case 0: case -3: return FdbConverters.Convert<T1, R>(this.Item1);
					case 1: case -2: return FdbConverters.Convert<T2, R>(this.Item2);
					case 2: case -1: return FdbConverters.Convert<T3, R>(this.Item3);
					default: FdbTuple.FailIndexOutOfRange(index, 3); return default(R);
			}
		}

		/// <summary>Return the value of the last item in the tuple</summary>
		public T3 Last
		{
			get { return this.Item3; }
		}

		/// <summary>Return the typed value of the last item in the tuple</summary>
		R IFdbTuple.Last<R>()
		{
			return FdbConverters.Convert<T3, R>(this.Item3);
		}

		public void PackTo(ref TupleWriter writer)
		{
			FdbTuplePacker<T1>.Encoder(ref writer, this.Item1);
			FdbTuplePacker<T2>.Encoder(ref writer, this.Item2);
			FdbTuplePacker<T3>.Encoder(ref writer, this.Item3);
		}

		IFdbTuple IFdbTuple.Append<T4>(T4 value)
		{
			// here, the caller doesn't care about the exact tuple type, so we simply return a boxed List Tuple.
			return new FdbListTuple(new object[4] { this.Item1, this.Item2, this.Item3, value }, 0, 4);
		}

		/// <summary>Appends a single new item at the end of the current tuple.</summary>
		/// <param name="value">Value that will be added as an embedded item</param>
		/// <returns>New tuple with one extra item</returns>
		/// <remarks>If <paramref name="value"/> is a tuple, and you want to append the *items*  of this tuple, and not the tuple itself, please call <see cref="Concat"/>!</remarks>
		[NotNull]
		public FdbTuple<T1, T2, T3, T4> Append<T4>(T4 value)
		{
			// Here, the caller was explicitly using the FdbTuple<T1, T2, T3> struct so probably care about memory footprint, so we keep returning a struct
			return new FdbTuple<T1, T2, T3, T4>(this.Item1, this.Item2, this.Item3, value);

			// Note: By create a FdbTuple<T1, T2, T3, T4> we risk an explosion of the number of combinations of Ts which could potentially cause problems at runtime (too many variants of the same generic types). 
			// ex: if we have N possible types, then there could be N^4 possible variants of FdbTuple<T1, T2, T3, T4> that the JIT has to deal with.
			// => if this starts becoming a problem, then we should return a list tuple !
		}

		/// <summary>Copy all the items of this tuple into an array at the specified offset</summary>
		[NotNull]
		public FdbTuple<T1, T2, T3, IFdbTuple> Append(IFdbTuple value)
		{
			//note: this override exists to prevent the explosion of tuple types such as FdbTuple<T1, FdbTuple<T1, T2>, FdbTuple<T1, T2, T3>, FdbTuple<T1, T2, T4>> !
			return new FdbTuple<T1, T2, T3, IFdbTuple>(this.Item1, this.Item2, this.Item3, value);
		}

		/// <summary>Appends the items of a tuple at the end of the current tuple.</summary>
		/// <param name="tuple">Tuple whose items are to be appended at the end</param>
		/// <returns>New tuple composed of the current tuple's items, followed by <paramref name="tuple"/>'s items</returns>
		[NotNull]
		public IFdbTuple Concat([NotNull] IFdbTuple tuple)
		{
			return FdbTuple.Concat(this, tuple);
		}

		public void CopyTo(object[] array, int offset)
		{
			array[offset] = this.Item1;
			array[offset + 1] = this.Item2;
			array[offset + 2] = this.Item3;
		}

		/// <summary>Execute a lambda Action with the content of this tuple</summary>
		/// <param name="lambda">Action that will be passed the content of this tuple as parameters</param>
		public void With([NotNull] Action<T1, T2, T3> lambda)
		{
			lambda(this.Item1, this.Item2, this.Item3);
		}

		/// <summary>Execute a lambda Function with the content of this tuple</summary>
		/// <param name="lambda">Action that will be passed the content of this tuple as parameters</param>
		/// <returns>Result of calling <paramref name="lambda"/> with the items of this tuple</returns>
		public R With<R>([NotNull] Func<T1, T2, T3, R> lambda)
		{
			return lambda(this.Item1, this.Item2, this.Item3);
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

		public Slice ToSlice()
		{
			return FdbTuple.EncodeKey(this.Item1, this.Item2, this.Item3);
		}

		Slice IFdbKey.ToFoundationDbKey()
		{
			return this.ToSlice();
		}

		public override string ToString()
		{
			return new StringBuilder(32).Append('(')
				.Append(FdbTuple.Stringify(this.Item1)).Append(", ")
				.Append(FdbTuple.Stringify(this.Item2)).Append(", ")
				.Append(FdbTuple.Stringify(this.Item3)).Append(')')
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

		public static bool operator ==(FdbTuple<T1, T2, T3> left, FdbTuple<T1, T2, T3> right)
		{
			var comparer = SimilarValueComparer.Default;
			return comparer.Equals(left.Item1, right.Item1)
				&& comparer.Equals(left.Item2, right.Item2)
				&& comparer.Equals(left.Item3, right.Item3);
		}

		public static bool operator !=(FdbTuple<T1, T2, T3> left, FdbTuple<T1, T2, T3> right)
		{
			var comparer = SimilarValueComparer.Default;
			return !comparer.Equals(left.Item1, right.Item1)
				|| !comparer.Equals(left.Item2, right.Item2)
				|| !comparer.Equals(left.Item3, right.Item3);
		}

		bool IStructuralEquatable.Equals(object other, IEqualityComparer comparer)
		{
			if (other == null) return false;
			if (other is FdbTuple<T1, T2, T3>)
			{
				var tuple = (FdbTuple<T1, T2, T3>)other;
				return comparer.Equals(this.Item1, tuple.Item1)
					&& comparer.Equals(this.Item2, tuple.Item2)
					&& comparer.Equals(this.Item3, tuple.Item3);
			}
			return FdbTuple.Equals(this, other, comparer);
		}

		int IStructuralEquatable.GetHashCode(IEqualityComparer comparer)
		{
			return FdbTuple.CombineHashCodes(
				comparer.GetHashCode(this.Item1),
				comparer.GetHashCode(this.Item2),
				comparer.GetHashCode(this.Item3)
			);
		}

	}

}
