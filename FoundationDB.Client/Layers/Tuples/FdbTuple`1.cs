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

	/// <summary>Tuple that holds only one item</summary>
	/// <typeparam name="T1">Type of the item</typeparam>
	[ImmutableObject(true), DebuggerDisplay("{ToString()}")]
	public struct FdbTuple<T1> : IFdbTuple
	{
		// This is mostly used by code that create a lot of temporary singleton, to reduce the pressure on the Garbage Collector by allocating them on the stack.
		// Please note that if you return an FdbTuple<T> as an IFdbTuple, it will be boxed by the CLR and all memory gains will be lost

		/// <summary>First and only item in the tuple</summary>
		public readonly T1 Item1;

		[DebuggerStepThrough]
		public FdbTuple(T1 item1)
		{
			this.Item1 = item1;
		}

		public int Count { get { return 1; } }

		public object this[int index]
		{
			get
			{
				if (index > 0 || index < -1) FdbTuple.FailIndexOutOfRange(index, 1);
				return this.Item1;
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
			if (index > 0 || index < -1) FdbTuple.FailIndexOutOfRange(index, 1);
			return FdbConverters.Convert<T1, R>(this.Item1);
		}

		/// <summary>Return the typed value of the last item in the tuple</summary>
		R IFdbTuple.Last<R>()
		{
			return FdbConverters.Convert<T1, R>(this.Item1);
		}

		public void PackTo(ref TupleWriter writer)
		{
			FdbTuplePacker<T1>.Encoder(ref writer, this.Item1);
		}

		IFdbTuple IFdbTuple.Append<T2>(T2 value)
		{
			return new FdbTuple<T1, T2>(this.Item1, value);
		}

		/// <summary>Appends a tuple as a single new item at the end of the current tuple.</summary>
		/// <param name="value">Tuple that will be added as an embedded item</param>
		/// <returns>New tuple with one extra item</returns>
		/// <remarks>If you want to append the *items*  of <paramref name="value"/>, and not the tuple itself, please call <see cref="Concat"/>!</remarks>
		[NotNull]
		public FdbTuple<T1, T2> Append<T2>(T2 value)
		{
			return new FdbTuple<T1, T2>(this.Item1, value);
		}

		/// <summary>Appends the items of a tuple at the end of the current tuple.</summary>
		/// <param name="tuple">Tuple whose items are to be appended at the end</param>
		/// <returns>New tuple composed of the current tuple's items, followed by <paramref name="tuple"/>'s items</returns>
		[NotNull]
		public IFdbTuple Concat([NotNull] IFdbTuple tuple)
		{
			return FdbTuple.Concat(this, tuple);
		}

		/// <summary>Copy the item of this singleton into an array at the specified offset</summary>
		public void CopyTo([NotNull] object[] array, int offset)
		{
			array[offset] = this.Item1;
		}

		/// <summary>Execute a lambda Action with the content of this tuple</summary>
		/// <param name="lambda">Action that will be passed the content of this tuple as parameters</param>
		public void With([NotNull] Action<T1> lambda)
		{
			lambda(this.Item1);
		}

		/// <summary>Execute a lambda Function with the content of this tuple</summary>
		/// <param name="lambda">Action that will be passed the content of this tuple as parameters</param>
		/// <returns>Result of calling <paramref name="lambda"/> with the items of this tuple</returns>
		public R With<R>([NotNull] Func<T1, R> lambda)
		{
			return lambda(this.Item1);
		}

		public IEnumerator<object> GetEnumerator()
		{
			yield return this.Item1;
		}

		System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
		{
			return this.GetEnumerator();
		}

		public Slice ToSlice()
		{
			return FdbTuple.EncodeKey(this.Item1);
		}

		Slice IFdbKey.ToFoundationDbKey()
		{
			return this.ToSlice();
		}

		public override string ToString()
		{
			// singleton tuples end with a trailing ','
			return "(" + FdbTuple.Stringify(this.Item1) + ",)";
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

		public static bool operator ==(FdbTuple<T1> left, FdbTuple<T1> right)
		{
			return SimilarValueComparer.Default.Equals(left.Item1, right.Item1);
		}

		public static bool operator !=(FdbTuple<T1> left, FdbTuple<T1> right)
		{
			return !SimilarValueComparer.Default.Equals(left.Item1, right.Item1);
		}

		bool IStructuralEquatable.Equals(object other, IEqualityComparer comparer)
		{
			if (other == null) return false;
			if (other is FdbTuple<T1>)
			{
				return comparer.Equals(this.Item1, ((FdbTuple<T1>)other).Item1);
			}
			return FdbTuple.Equals(this, other, comparer);
		}

		int IStructuralEquatable.GetHashCode(IEqualityComparer comparer)
		{
			return comparer.GetHashCode(this.Item1);
		}

		public static implicit operator FdbTuple<T1>(Tuple<T1> t)
		{
			if (t == null) throw new ArgumentNullException("t");
			return new FdbTuple<T1>(t.Item1);
		}

		public static explicit operator Tuple<T1>(FdbTuple<T1> t)
		{
			return new Tuple<T1>(t.Item1);
		}

	}

}
