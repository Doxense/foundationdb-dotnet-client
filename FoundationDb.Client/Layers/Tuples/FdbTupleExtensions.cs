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

using FoundationDb.Client.Tuples;
using FoundationDb.Client.Utils;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;

namespace FoundationDb.Client
{

	public static class FdbTupleExtensions
	{

		public static bool GetBoolean(this IFdbTuple tuple, int offset)
		{
			return tuple.Get<bool>(offset);
#if REFACTORED
			object value = tuple[offset];
			if (value == null) return false;
			return Convert.ToBoolean(value);
#endif
		}

		public static int GetInt32(this IFdbTuple tuple, int offset)
		{
			return tuple.Get<Int32>(offset);
#if REFACTORED
			object value = tuple[offset];
			if (value == null) return 0;

			if (value is long) return (int)((long)value);
			if (value is int) return (int)value;
			return Convert.ToInt32(value);
#endif
		}

		public static long GetInt64(this IFdbTuple tuple, int offset)
		{
			return tuple.Get<Int64>(offset);
#if REFACTORED
			object value = tuple[offset];
			if (value == null) return 0L;

			if (value is long) return (long)value;
			return Convert.ToInt64(value);
#endif
		}

		public static string GetString(this IFdbTuple tuple, int offset)
		{
			return tuple.Get<string>(offset);
#if REFACTORED
			object value = tuple[offset];
			if (value == null) return null;

			var str = value as string;
			if (str != null) return str;
			return Convert.ToString(value, CultureInfo.InvariantCulture);
#endif
		}

		public static Guid GetGuid(this IFdbTuple tuple, int offset)
		{
			return tuple.Get<Guid>(offset);
#if REFACTORED
			object value = tuple[offset];
			if (value == null) return Guid.Empty;

			if (value is Guid) return (Guid)value;

			if (value is string)
			{
				// if the string length is 36 (32 digits and 4 dashes), is was encoded as a unicode string
				var s = (string)value;
				if (s.Length == 36 && s[8] == '-' && char.IsDigit(s[0]))
				{
					return Guid.Parse(value as string);
				}
			}
			else if (value is byte[])
			{
				return new Guid(value as byte[]);
			}

			throw new FormatException("Cannot convert tuple value into Guid");
#endif
		}

		/// <summary>Returns an array containing all the objects of a tuple</summary>
		public static object[] ToArray(this IFdbTuple tuple)
		{
			if (tuple == null) throw new ArgumentNullException("tuple");

			var items = new object[tuple.Count];
			if (items.Length > 0)
			{
				tuple.CopyTo(items, 0);
			}
			return items;
		}

		/// <summary>Returns a byte array containing the packed version of a tuple</summary>
		public static byte[] GetBytes(this IFdbTuple tuple)
		{
			return tuple.ToSlice().GetBytes();
		}

		/// <summary>Concatenates two tuples together</summary>
		public static IFdbTuple Concat(this IFdbTuple first, IFdbTuple second)
		{
			if (first == null) throw new ArgumentNullException("first");
			if (second == null) throw new ArgumentNullException("second");

			int n1 = first.Count;
			if (n1 == 0) return second;

			int n2 = second.Count;
			if (n2 == 0) return first;

			var firstList = first as FdbTupleList;
			if (firstList != null)
			{ // optimized path
				return firstList.Concat(second);
			}

			// create a new list with both
			var list = new List<object>(n1 + n2);
			list.AddRange(first);
			list.AddRange(second);
			return new FdbTupleList(list);
		}

		public static IFdbTuple AppendRange(this IFdbTuple tuple, params object[] items)
		{
			if (tuple == null) throw new ArgumentNullException("tuple");
			if (items == null) throw new ArgumentNullException("items");
			if (items.Length == 0) return tuple;

			var tupleList = tuple as FdbTupleList;
			if (tupleList != null) return tupleList.AppendRange(items);

			var list = new List<object>(tuple.Count + items.Length);
			list.AddRange(tuple);
			list.AddRange(items);
			return new FdbTupleList(list);
		}

		/// <summary>Creates a key range containing all children of this tuple, from tuple.pack()+'\0' to tuple.pack()+'\xFF'</summary>
		/// <param name="tuple">Tuple that is the suffix of all keys</param>
		/// <returns>Range of all keys suffixed by the tuple. The tuple itself will not be included</returns>
		public static FdbKeyRange ToRange(this IFdbTuple tuple)
		{
			return ToRange(tuple, false);
		}

		/// <summary>Creates a key range containing all children of tuple, optionally including the tuple itself.</summary>
		/// <param name="tuple">Tuple that is the prefix of all keys</param>
		/// <param name="includePrefix">If true, the tuple key itself is included, if false only the children keys are included</param>
		/// <returns>Range of all keys suffixed by the tuple. The tuple itself will be included if <paramref name="includePrefix"/> is true</returns>
		public static FdbKeyRange ToRange(this IFdbTuple tuple, bool includePrefix)
		{
			if (tuple == null) throw new ArgumentNullException("tuple");

			// We want to allocate only one byte[] to store both keys, and map both Slice to each chunk
			// So we will serialize the tuple two times in the same writer

			var writer = new FdbBufferWriter();

			tuple.PackTo(writer);
			writer.EnsureBytes(writer.Position + 2);
			if (!includePrefix) writer.WriteByte(0);
			int p0 = writer.Position;

			tuple.PackTo(writer);
			writer.WriteByte(0xFF);
			int p1 = writer.Position;

			return new FdbKeyRange(
				new Slice(writer.Buffer, 0, p0),
				new Slice(writer.Buffer, p0, p1 - p0)
			);
		}

		/// <summary>Creates pre-packed and isolated copy of this tuple</summary>
		/// <param name="tuple"></param>
		/// <returns>Create a copy of the tuple that can be reused frequently to pack values</returns>
		/// <remarks>If the tuple is already memoized, the current instance will be returned</remarks>
		public static FdbMemoizedTuple Memoize(this IFdbTuple tuple)
		{
			if (tuple == null) return null;

			var memoized = tuple as FdbMemoizedTuple;
			if (memoized == null)
			{
				memoized = new FdbMemoizedTuple(tuple.ToArray(), tuple.ToSlice());
			}

			return memoized;
		}

		/// <summary>Unpack a tuple from this slice</summary>
		/// <param name="slice"></param>
		/// <returns>Unpacked tuple if the slice contains data, FdbTuple.Empty if the slice is empty, or null if the slice is Slice.Nil</returns>
		public static IFdbTuple ToTuple(this Slice slice)
		{
			if (slice.IsNullOrEmpty)
			{
				return slice.HasValue ? FdbTuple.Empty : null;
			}

			return FdbTuple.Unpack(slice);
		}

	}

}
