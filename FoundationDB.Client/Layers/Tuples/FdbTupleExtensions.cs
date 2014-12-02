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
	using JetBrains.Annotations;
	using System;
	using System.Collections.Generic;

	/// <summary>Add extensions methods that deal with tuples on various types</summary>
	public static class FdbTupleExtensions
	{

		#region IFdbTuple extensions...

		/// <summary>Returns true if the tuple is either null or empty</summary>
		public static bool IsNullOrEmpty(this IFdbTuple tuple)
		{
			return tuple == null || tuple.Count == 0;
		}

		/// <summary>Returns true if the tuple is not null, and contains only one item</summary>
		public static bool IsSingleton(this IFdbTuple tuple)
		{
			return tuple != null && tuple.Count == 1;
		}

		/// <summary>Returns an array containing all the objects of a tuple</summary>
		[NotNull]
		public static object[] ToArray([NotNull] this IFdbTuple tuple)
		{
			if (tuple == null) throw new ArgumentNullException("tuple");

			var items = new object[tuple.Count];
			if (items.Length > 0)
			{
				tuple.CopyTo(items, 0);
			}
			return items;
		}

		/// <summary>Returns a typed array containing all the items of a tuple</summary>
		[NotNull]
		public static T[] ToArray<T>([NotNull] this IFdbTuple tuple)
		{
			if (tuple == null) throw new ArgumentNullException("tuple");

			var items = new T[tuple.Count];
			if (items.Length > 0)
			{
				for (int i = 0; i < items.Length; i++)
				{
					items[i] = tuple.Get<T>(i);
				}
			}
			return items;
		}

		/// <summary>Returns a byte array containing the packed version of a tuple</summary>
		[CanBeNull]
		public static byte[] GetBytes([NotNull] this IFdbTuple tuple)
		{
			return tuple.ToSlice().GetBytes();
		}

		/// <summary>Returns the typed value of the first item in this tuple</summary>
		/// <typeparam name="T">Expected type of the first item</typeparam>
		/// <returns>Value of the first item, adapted into type <typeparamref name="T"/>.</returns>
		public static T First<T>([NotNull] this IFdbTuple tuple)
		{
			if (tuple == null) throw new ArgumentNullException("tuple");
			return tuple.Get<T>(0);
		}

		/// <summary>Appends two values at the end of a tuple</summary>
		[NotNull]
		public static IFdbTuple Append<T1, T2>([NotNull] this IFdbTuple tuple, T1 value1, T2 value2)
		{
			if (tuple == null) throw new ArgumentNullException("tuple");
			return new FdbJoinedTuple(tuple, FdbTuple.Create<T1, T2>(value1, value2));
		}

		/// <summary>Appends three values at the end of a tuple</summary>
		[NotNull]
		public static IFdbTuple Append<T1, T2, T3>([NotNull] this IFdbTuple tuple, T1 value1, T2 value2, T3 value3)
		{
			if (tuple == null) throw new ArgumentNullException("tuple");
			return new FdbJoinedTuple(tuple, FdbTuple.Create<T1, T2, T3>(value1, value2, value3));
		}

		/// <summary>Appends four values at the end of a tuple</summary>
		[NotNull]
		public static IFdbTuple Append<T1, T2, T3, T4>([NotNull] this IFdbTuple tuple, T1 value1, T2 value2, T3 value3, T4 value4)
		{
			if (tuple == null) throw new ArgumentNullException("tuple");
			return new FdbJoinedTuple(tuple, FdbTuple.Create<T1, T2, T3, T4>(value1, value2, value3, value4));
		}

		/// <summary>Creates a key range containing all children of this tuple, from tuple.pack()+'\0' to tuple.pack()+'\xFF'</summary>
		/// <param name="tuple">Tuple that is the suffix of all keys</param>
		/// <returns>Range of all keys suffixed by the tuple. The tuple itself will not be included</returns>
		public static FdbKeyRange ToRange([NotNull] this IFdbTuple tuple)
		{
			return ToRange(tuple, false);
		}

		/// <summary>Creates a key range containing all children of tuple, optionally including the tuple itself.</summary>
		/// <param name="tuple">Tuple that is the prefix of all keys</param>
		/// <param name="includePrefix">If true, the tuple key itself is included, if false only the children keys are included</param>
		/// <returns>Range of all keys suffixed by the tuple. The tuple itself will be included if <paramref name="includePrefix"/> is true</returns>
		public static FdbKeyRange ToRange([NotNull] this IFdbTuple tuple, bool includePrefix)
		{
			if (tuple == null) throw new ArgumentNullException("tuple");

			// We want to allocate only one byte[] to store both keys, and map both Slice to each chunk
			// So we will serialize the tuple two times in the same writer

			var writer = new TupleWriter();

			tuple.PackTo(ref writer);
			writer.Output.EnsureBytes(writer.Output.Position + 2);
			if (!includePrefix) writer.Output.WriteByte(0);
			int p0 = writer.Output.Position;

			tuple.PackTo(ref writer);
			writer.Output.WriteByte(0xFF);
			int p1 = writer.Output.Position;

			return new FdbKeyRange(
				new Slice(writer.Output.Buffer, 0, p0),
				new Slice(writer.Output.Buffer, p0, p1 - p0)
			);
		}

		/// <summary>Creates pre-packed and isolated copy of this tuple</summary>
		/// <param name="tuple"></param>
		/// <returns>Create a copy of the tuple that can be reused frequently to pack values</returns>
		/// <remarks>If the tuple is already memoized, the current instance will be returned</remarks>
		[CanBeNull]
		public static FdbMemoizedTuple Memoize(this IFdbTuple tuple)
		{
			if (tuple == null) return null;

			var memoized = tuple as FdbMemoizedTuple ?? new FdbMemoizedTuple(tuple.ToArray(), tuple.ToSlice());

			return memoized;
		}

		/// <summary>Unpack a tuple from this slice</summary>
		/// <param name="slice"></param>
		/// <returns>Unpacked tuple if the slice contains data, FdbTuple.Empty if the slice is empty, or null if the slice is Slice.Nil</returns>
		[CanBeNull]
		public static IFdbTuple ToTuple(this Slice slice)
		{
			if (slice.IsNullOrEmpty)
			{
				return slice.HasValue ? FdbTuple.Empty : null;
			}

			return FdbTuple.Unpack(slice);
		}

		/// <summary>Returns a substring of the current tuple</summary>
		/// <param name="tuple">Current tuple</param>
		/// <param name="offset">Offset from the start of the current tuple (negative value means from the end)</param>
		/// <returns>Tuple that contains only the items past the first <param name="offset"/> items of the current tuple</returns>
		[NotNull]
		public static IFdbTuple Substring([NotNull] this IFdbTuple tuple, int offset)
		{
			if (tuple == null) throw new ArgumentNullException("tuple");

			return tuple[offset, null];
		}

		/// <summary>Returns a substring of the current tuple</summary>
		/// <param name="tuple">Current tuple</param>
		/// <param name="offset">Offset from the start of the current tuple (negative value means from the end)</param>
		/// <param name="count">Number of items to keep</param>
		/// <returns>Tuple that contains only the selected items from the current tuple</returns>
		[NotNull]
		public static IFdbTuple Substring([NotNull] this IFdbTuple tuple, int offset, int count)
		{
			if (tuple == null) throw new ArgumentNullException("tuple");
			if (count < 0) throw new ArgumentOutOfRangeException("count", count, "Count cannot be negative.");

			if (count == 0) return FdbTuple.Empty;

			return tuple[offset, offset + count];
		}

		/// <summary>Test if the start of current tuple is equal to another tuple</summary>
		/// <param name="left">Larger tuple</param>
		/// <param name="right">Smaller tuple</param>
		/// <returns>True if the beginning of <paramref name="left"/> is equal to <paramref name="right"/> or if both tuples are identical</returns>
		public static bool StartsWith([NotNull] this IFdbTuple left, [NotNull] IFdbTuple right)
		{
			if (left == null) throw new ArgumentNullException("left");
			if (right == null) throw new ArgumentNullException("right");

			//REVIEW: move this on IFdbTuple interface ?
			return FdbTuple.StartsWith(left, right);
		}

		/// <summary>Test if the end of current tuple is equal to another tuple</summary>
		/// <param name="left">Larger tuple</param>
		/// <param name="right">Smaller tuple</param>
		/// <returns>True if the end of <paramref name="left"/> is equal to <paramref name="right"/> or if both tuples are identical</returns>
		public static bool EndsWith([NotNull] this IFdbTuple left, [NotNull] IFdbTuple right)
		{
			if (left == null) throw new ArgumentNullException("left");
			if (right == null) throw new ArgumentNullException("right");

			//REVIEW: move this on IFdbTuple interface ?
			return FdbTuple.EndsWith(left, right);
		}

		/// <summary>Transform a tuple of N elements into a list of N singletons</summary>
		/// <param name="tuple">Tuple that contains any number of elements</param>
		/// <returns>Sequence of tuples that contains a single element</returns>
		/// <example>(123, ABC, false,).Explode() => [ (123,), (ABC,), (false,) ]</example>
		public static IEnumerable<IFdbTuple> Explode([NotNull] this IFdbTuple tuple)
		{
			if (tuple == null) throw new ArgumentNullException("tuple");

			int p = 0;
			int n = tuple.Count;
			while (p < n)
			{
				yield return tuple[p, p + 1];
			}
		}

		/// <summary>Returns a key that is immediately after the packed representation of this tuple</summary>
		/// <remarks>This is the equivalent of manually packing the tuple and incrementing the resulting slice</remarks>
		public static Slice Increment([NotNull] this IFdbTuple tuple)
		{
			if (tuple == null) throw new ArgumentNullException("tuple");
			return FdbKey.Increment(tuple.ToSlice());
		}

		/// <summary>Returns a Key Selector pair that defines the range of all items contained under this tuple</summary>
		public static FdbKeySelectorPair ToSelectorPair([NotNull] this IFdbTuple tuple)
		{
			if (tuple == null) throw new ArgumentNullException("tuple");

			return FdbKeySelectorPair.StartsWith(tuple.ToSlice());
		}

		/// <summary>Verify that this tuple has the expected size</summary>
		/// <param name="tuple">Tuple which must be of a specific size</param>
		/// <param name="size">Expected number of items in this tuple</param>
		/// <returns>The <paramref name="tuple"/> itself it it has the correct size; otherwise, an exception is thrown</returns>
		/// <exception cref="ArgumentNullException">If <paramref name="tuple"/> is null</exception>
		/// <exception cref="InvalidOperationException">If <paramref name="tuple"/> is smaller or larger than <paramref name="size"/></exception>
		[ContractAnnotation("halt <= tuple:null")]
		[NotNull]
		public static IFdbTuple OfSize(this IFdbTuple tuple, int size)
		{
			if (tuple == null || tuple.Count != size) ThrowInvalidTupleSize(tuple, size, 0);
			return tuple;
		}

		/// <summary>Verify that this tuple has at least a certain size</summary>
		/// <param name="tuple">Tuple which must be of a specific size</param>
		/// <param name="size">Expected minimum number of items in this tuple</param>
		/// <returns>The <paramref name="tuple"/> itself it it has the correct size; otherwise, an exception is thrown</returns>
		/// <exception cref="ArgumentNullException">If <paramref name="tuple"/> is null</exception>
		/// <exception cref="InvalidOperationException">If <paramref name="tuple"/> is smaller than <paramref name="size"/></exception>
		[ContractAnnotation("halt <= tuple:null")]
		[NotNull]
		public static IFdbTuple OfSizeAtLeast(this IFdbTuple tuple, int size)
		{
			if (tuple == null || tuple.Count < size) ThrowInvalidTupleSize(tuple, size, -1);
			return tuple;
		}

		/// <summary>Verify that this tuple has at most a certain size</summary>
		/// <param name="tuple">Tuple which must be of a specific size</param>
		/// <param name="size">Expected maximum number of items in this tuple</param>
		/// <returns>The <paramref name="tuple"/> itself it it has the correct size; otherwise, an exception is thrown</returns>
		/// <exception cref="ArgumentNullException">If <paramref name="tuple"/> is null</exception>
		/// <exception cref="InvalidOperationException">If <paramref name="tuple"/> is larger than <paramref name="size"/></exception>
		[ContractAnnotation("halt <= tuple:null")]
		[NotNull]
		public static IFdbTuple OfSizeAtMost(this IFdbTuple tuple, int size)
		{
			if (tuple == null || tuple.Count > size) ThrowInvalidTupleSize(tuple, size, 1);
			return tuple;
		}

		[ContractAnnotation("=> halt")]
		internal static void ThrowInvalidTupleSize(IFdbTuple tuple, int expected, int test)
		{
			if (tuple == null)
			{
				throw new ArgumentNullException("tuple");
			}
			switch(test)
			{
				case 1: throw new InvalidOperationException(String.Format("This operation requires a tuple of size {0} or less, but this tuple has {1} elements", expected, tuple.Count));
				case -1: throw new InvalidOperationException(String.Format("This operation requires a tuple of size {0} or more, but this tuple has {1} elements", expected, tuple.Count));
				default: throw new InvalidOperationException(String.Format("This operation requires a tuple of size {0}, but this tuple has {1} elements", expected, tuple.Count));
			}
		}

		/// <summary>Execute a lambda Action with the content of this tuple</summary>
		/// <param name="lambda">Action that will be passed the content of this tuple as parameters</param>
		public static void With<T1>([NotNull] this IFdbTuple tuple, [NotNull] Action<T1> lambda)
		{
			OfSize(tuple, 1);
			lambda(tuple.Get<T1>(0));
		}

		/// <summary>Execute a lambda Action with the content of this tuple</summary>
		/// <param name="lambda">Action that will be passed the content of this tuple as parameters</param>
		public static void With<T1, T2>([NotNull] this IFdbTuple tuple, [NotNull] Action<T1, T2> lambda)
		{
			OfSize(tuple, 2);
			lambda(tuple.Get<T1>(0), tuple.Get<T2>(1));
		}

		/// <summary>Execute a lambda Action with the content of this tuple</summary>
		/// <param name="lambda">Action that will be passed the content of this tuple as parameters</param>
		public static void With<T1, T2, T3>([NotNull] this IFdbTuple tuple, [NotNull] Action<T1, T2, T3> lambda)
		{
			OfSize(tuple, 3);
			lambda(tuple.Get<T1>(0), tuple.Get<T2>(1), tuple.Get<T3>(2));
		}

		/// <summary>Execute a lambda Action with the content of this tuple</summary>
		/// <param name="lambda">Action that will be passed the content of this tuple as parameters</param>
		public static void With<T1, T2, T3, T4>([NotNull] this IFdbTuple tuple, [NotNull] Action<T1, T2, T3, T4> lambda)
		{
			OfSize(tuple, 4);
			lambda(tuple.Get<T1>(0), tuple.Get<T2>(1), tuple.Get<T3>(2), tuple.Get<T4>(3));
		}

		/// <summary>Execute a lambda Action with the content of this tuple</summary>
		/// <param name="lambda">Action that will be passed the content of this tuple as parameters</param>
		public static void With<T1, T2, T3, T4, T5>([NotNull] this IFdbTuple tuple, [NotNull] Action<T1, T2, T3, T4, T5> lambda)
		{
			OfSize(tuple, 5);
			lambda(tuple.Get<T1>(0), tuple.Get<T2>(1), tuple.Get<T3>(2), tuple.Get<T4>(3), tuple.Get<T5>(4));
		}

		/// <summary>Execute a lambda Action with the content of this tuple</summary>
		/// <param name="lambda">Action that will be passed the content of this tuple as parameters</param>
		public static void With<T1, T2, T3, T4, T5, T6>([NotNull] this IFdbTuple tuple, [NotNull] Action<T1, T2, T3, T4, T5, T6> lambda)
		{
			OfSize(tuple, 6);
			lambda(tuple.Get<T1>(0), tuple.Get<T2>(1), tuple.Get<T3>(2), tuple.Get<T4>(3), tuple.Get<T5>(4), tuple.Get<T6>(5));
		}

		/// <summary>Execute a lambda Action with the content of this tuple</summary>
		/// <param name="lambda">Action that will be passed the content of this tuple as parameters</param>
		public static void With<T1, T2, T3, T4, T5, T6, T7>([NotNull] this IFdbTuple tuple, [NotNull] Action<T1, T2, T3, T4, T5, T6, T7> lambda)
		{
			OfSize(tuple, 7);
			lambda(tuple.Get<T1>(0), tuple.Get<T2>(1), tuple.Get<T3>(2), tuple.Get<T4>(3), tuple.Get<T5>(4), tuple.Get<T6>(5), tuple.Get<T7>(6));
		}

		/// <summary>Execute a lambda Action with the content of this tuple</summary>
		/// <param name="lambda">Action that will be passed the content of this tuple as parameters</param>
		public static void With<T1, T2, T3, T4, T5, T6, T7, T8>([NotNull] this IFdbTuple tuple, [NotNull] Action<T1, T2, T3, T4, T5, T6, T7, T8> lambda)
		{
			OfSize(tuple, 8);
			lambda(tuple.Get<T1>(0), tuple.Get<T2>(1), tuple.Get<T3>(2), tuple.Get<T4>(3), tuple.Get<T5>(4), tuple.Get<T6>(5), tuple.Get<T7>(6), tuple.Get<T8>(7));
		}

		/// <summary>Execute a lambda Function with the content of this tuple</summary>
		/// <param name="lambda">Action that will be passed the content of this tuple as parameters</param>
		/// <returns>Result of calling <paramref name="lambda"/> with the items of this tuple</returns>
		public static R With<T1, R>([NotNull] this IFdbTuple tuple, [NotNull] Func<T1, R> lambda)
		{
			OfSize(tuple, 1);
			return lambda(tuple.Get<T1>(0));
		}

		/// <summary>Execute a lambda Function with the content of this tuple</summary>
		/// <param name="lambda">Action that will be passed the content of this tuple as parameters</param>
		/// <returns>Result of calling <paramref name="lambda"/> with the items of this tuple</returns>
		public static R With<T1, T2, R>([NotNull] this IFdbTuple tuple, [NotNull] Func<T1, T2, R> lambda)
		{
			OfSize(tuple, 2);
			return lambda(tuple.Get<T1>(0), tuple.Get<T2>(1));
		}

		/// <summary>Execute a lambda Function with the content of this tuple</summary>
		/// <param name="lambda">Action that will be passed the content of this tuple as parameters</param>
		/// <returns>Result of calling <paramref name="lambda"/> with the items of this tuple</returns>
		public static R With<T1, T2, T3, R>([NotNull] this IFdbTuple tuple, [NotNull] Func<T1, T2, T3, R> lambda)
		{
			OfSize(tuple, 3);
			return lambda(tuple.Get<T1>(0), tuple.Get<T2>(1), tuple.Get<T3>(2));
		}

		/// <summary>Execute a lambda Function with the content of this tuple</summary>
		/// <param name="lambda">Action that will be passed the content of this tuple as parameters</param>
		/// <returns>Result of calling <paramref name="lambda"/> with the items of this tuple</returns>
		public static R With<T1, T2, T3, T4, R>([NotNull] this IFdbTuple tuple, [NotNull] Func<T1, T2, T3, T4, R> lambda)
		{
			OfSize(tuple, 4);
			return lambda(tuple.Get<T1>(0), tuple.Get<T2>(1), tuple.Get<T3>(2), tuple.Get<T4>(3));
		}

		/// <summary>Execute a lambda Function with the content of this tuple</summary>
		/// <param name="lambda">Action that will be passed the content of this tuple as parameters</param>
		/// <returns>Result of calling <paramref name="lambda"/> with the items of this tuple</returns>
		public static R With<T1, T2, T3, T4, T5, R>([NotNull] this IFdbTuple tuple, [NotNull] Func<T1, T2, T3, T4, T5, R> lambda)
		{
			OfSize(tuple, 5);
			return lambda(tuple.Get<T1>(0), tuple.Get<T2>(1), tuple.Get<T3>(2), tuple.Get<T4>(3), tuple.Get<T5>(4));
		}

		/// <summary>Execute a lambda Function with the content of this tuple</summary>
		/// <param name="lambda">Action that will be passed the content of this tuple as parameters</param>
		/// <returns>Result of calling <paramref name="lambda"/> with the items of this tuple</returns>
		public static R With<T1, T2, T3, T4, T5, T6, R>([NotNull] this IFdbTuple tuple, [NotNull] Func<T1, T2, T3, T4, T5, T6, R> lambda)
		{
			OfSize(tuple, 6);
			return lambda(tuple.Get<T1>(0), tuple.Get<T2>(1), tuple.Get<T3>(2), tuple.Get<T4>(3), tuple.Get<T5>(4), tuple.Get<T6>(5));
		}

		/// <summary>Execute a lambda Function with the content of this tuple</summary>
		/// <param name="lambda">Action that will be passed the content of this tuple as parameters</param>
		/// <returns>Result of calling <paramref name="lambda"/> with the items of this tuple</returns>
		public static R With<T1, T2, T3, T4, T5, T6, T7, R>([NotNull] this IFdbTuple tuple, [NotNull] Func<T1, T2, T3, T4, T5, T6, T7, R> lambda)
		{
			OfSize(tuple, 7);
			return lambda(tuple.Get<T1>(0), tuple.Get<T2>(1), tuple.Get<T3>(2), tuple.Get<T4>(3), tuple.Get<T5>(4), tuple.Get<T6>(5), tuple.Get<T7>(6));
		}

		/// <summary>Execute a lambda Function with the content of this tuple</summary>
		/// <param name="lambda">Action that will be passed the content of this tuple as parameters</param>
		/// <returns>Result of calling <paramref name="lambda"/> with the items of this tuple</returns>
		public static R With<T1, T2, T3, T4, T5, T6, T7, T8, R>([NotNull] this IFdbTuple tuple, [NotNull] Func<T1, T2, T3, T4, T5, T6, T7, T8, R> lambda)
		{
			OfSize(tuple, 8);
			return lambda(tuple.Get<T1>(0), tuple.Get<T2>(1), tuple.Get<T3>(2), tuple.Get<T4>(3), tuple.Get<T5>(4), tuple.Get<T6>(5), tuple.Get<T7>(6), tuple.Get<T8>(7));
		}

		#endregion

	}

}
