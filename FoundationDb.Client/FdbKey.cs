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
using System.Text;

namespace FoundationDb.Client
{

	/// <summary>Factory class for keys</summary>
	public static class FdbKey
	{
		/// <summary>Smallest possible key ('\0')</summary>
		public static readonly FdbByteKey MinValue = new FdbByteKey(new byte[1] { 0 }, 0, 1);

		/// <summary>Bigest possible key ('\xFF'), excluding the system keys</summary>
		public static readonly FdbByteKey MaxValue = new FdbByteKey(new byte[1] { 255 }, 0, 1);

		public static IFdbKey Ascii(string text)
		{
			return new FdbByteKey(Encoding.Default.GetBytes(text));
		}

		public static string Ascii(ArraySegment<byte> key)
		{
			if (key.Count == 0) return key.Array == null ? null : String.Empty;
			return Encoding.Default.GetString(key.Array, key.Offset, key.Count);
		}

		public static IFdbKey Unicode(string text)
		{
			return new FdbByteKey(Encoding.UTF8.GetBytes(text));
		}

		public static string Unicode(ArraySegment<byte> key)
		{
			if (key.Count == 0) return key.Array == null ? null : String.Empty;
			return Encoding.UTF8.GetString(key.Array, key.Offset, key.Count);
		}

		public static ArraySegment<byte> Increment(IFdbKey key)
		{
			return Increment(key.ToArraySegment());
		}

		public static ArraySegment<byte> Increment(ArraySegment<byte> buffer)
		{
			var tmp = new byte[buffer.Count];
			Array.Copy(buffer.Array, 0, tmp, 0, tmp.Length);
			int n = tmp.Length - 1;
			while (n >= 0)
			{
				byte c = (byte)((tmp[n] + 1) & 0xFF);
				if (c > 0)
				{
					tmp[n] = c;
					break;
				}
				tmp[n] = 0;
				--n;
			}
			if (n < 0) throw new OverflowException("Cannot increment FdbKey past the maximum value");
			return new ArraySegment<byte>(tmp, 0, tmp.Length);
		}

		public static string Dump(ArraySegment<byte> buffer)
		{
			if (buffer.Count == 0) return buffer.Array == null ? "<null>" : "<empty>";

			var sb = new StringBuilder(buffer.Count + 16);
			for (int i = 0; i < buffer.Count; i++)
			{
				int c = buffer.Array[buffer.Offset + i];
				if (c < 32 || c == 255) sb.Append('<').Append(c.ToString("X2")).Append('>'); else sb.Append((char)c);
			}
			return sb.ToString();
		}

		public static bool AreEqual(IFdbKey left, IFdbKey right)
		{
			if (object.ReferenceEquals(left, right)) return true;
			return AreEqual(left.ToArraySegment(), right.ToArraySegment());
		}

		public static bool AreEqual(ArraySegment<byte> left, ArraySegment<byte> right)
		{
			int n = left.Count;
			if (right.Count != n) return false;

			int pl = left.Offset;
			int pr = right.Offset;
			byte[] al = left.Array;
			byte[] ar = right.Array;

			while (n-- > 0)
			{
				if (al[pl++] != ar[pr++]) return false;
			}
			return true;
		}

		public static int Compare(ArraySegment<byte> left, ArraySegment<byte> right)
		{
			int n = Math.Min(left.Count, right.Count);

			int pl = left.Offset;
			int pr = right.Offset;
			byte[] al = left.Array;
			byte[] ar = right.Array;

			while (n-- > 0)
			{
				int d = ar[pr++] - al[pl++];
				if (d != 0) return d;
			}

			return left.Count == right.Count ? 0 : right.Count - left.Count;
		}

		/// <summary>Split a buffer containing multiple contiguous segments into an array of segments</summary>
		/// <param name="buffer">Buffer containing all the segments</param>
		/// <param name="start">Offset of the start of the first segment</param>
		/// <param name="endOffsets">Array containing, for each segment, the offset of the following segment</param>
		/// <returns>Array of segments</returns>
		/// <example>SplitIntoSegments("HelloWorld", 0, [5, 10]) => [{"Hello"}, {"World"}]</example>
		internal static ArraySegment<byte>[] SplitIntoSegments(byte[] buffer, int start, List<int> endOffsets)
		{
			var result = new ArraySegment<byte>[endOffsets.Count];
			int i = 0;
			int p = start;
			foreach (var end in endOffsets)
			{
				result[i++] = new ArraySegment<byte>(buffer, p, end - p);
				p = end;
			}

			return result;
		}

		public static IFdbKey Pack<T1, T2>(T1 item1, T2 item2)
		{
			return FdbTuple.Create<T1, T2>(item1, item2);
		}

		public static IFdbKey Pack<T1, T2, T3>(T1 item1, T2 item2, T3 item3)
		{
			return FdbTuple.Create<T1, T2, T3>(item1, item2, item3);
		}

		public static IFdbKey Pack<T1, T2, T3, T4>(T1 item1, T2 item2, T3 item3, T4 item4)
		{
			return FdbTuple.Create<T1, T2, T3, T4>(item1, item2, item3, item4);
		}

		public static IFdbKey Pack<T1, T2, T3, T4, T5>(T1 item1, T2 item2, T3 item3, T4 item4, T5 item5)
		{
			return FdbTuple.Create<T1, T2, T3, T4, T5>(item1, item2, item3, item4, item5);
		}

		public static IFdbKey Pack(params object[] items)
		{
			return FdbTuple.Create(items);
		}

		public static IFdbKey Pack(IFdbTuple prefix, params object[] items)
		{
			return prefix.AppendRange(items);
		}

	}

}
