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
using System.Runtime.InteropServices;
using System.Text;

namespace FoundationDb.Client
{

	public struct Slice : IEquatable<Slice>, IEquatable<ArraySegment<byte>>, IEquatable<byte[]>, IComparable<Slice>
	{
		internal static readonly byte[] EmptyArray = new byte[0];

		public static readonly Slice Nil = new Slice(null, 0, 0);
		public static readonly Slice Empty = new Slice(EmptyArray, 0, 0);

		public static Slice Create(byte[] bytes)
		{
			return bytes == null ? Slice.Nil : bytes.Length == 0 ? Slice.Empty : new Slice(bytes, 0, bytes.Length);
		}

		public static Slice Create(byte[] buffer, int offset, int count)
		{
			if (buffer == null) return Nil;
			if (count == 0) return Empty;
			if (offset < 0 || offset >= buffer.Length) throw new ArgumentException("offset");
			if (count < 0 || offset + count > buffer.Length) throw new ArgumentException("count");
			return new Slice(buffer, offset, count);
		}

		internal static unsafe Slice Create(byte* ptr, int count)
		{
			if (ptr == null) return Slice.Nil;
			if (count <= 0) return Slice.Empty;

			var bytes = new byte[count];
			Marshal.Copy(new IntPtr(ptr), bytes, 0, count);
			return new Slice(bytes, 0, count);
		}

		public static Slice Allocate(int size)
		{
			if (size < 0) throw new ArgumentException("size");
			return size == 0 ? Slice.Empty : new Slice(new byte[size], 0, size);
		}

		public static Slice FromBase64(string base64String)
		{
			return base64String == null ? Slice.Nil : base64String.Length == 0 ? Slice.Empty : Slice.Create(Convert.FromBase64String(base64String));
		}

		internal Slice(byte[] array, int offset, int count)
		{
			Contract.Requires(array != null);
			Contract.Requires(offset >= 0 && offset < array.Length);
			Contract.Requires(count >= 0 && offset + count <= array.Length);

			this.Array = array;
			this.Offset = offset;
			this.Count = count;
		}

		public readonly byte[] Array;
		public readonly int Offset;
		public readonly int Count;

		/// <summary>Returns false is the slice is null, or true is the slice maps to zero or more bytes</summary>
		/// <remarks>An empty slice is NOT null</remarks>
		public bool HasValue { get { return this.Array != null; } }

		/// <summary>Return true if the slice is not null but contains 0 bytes</summary>
		/// <remarks>A null slice is NOT empty</remarks>
		public bool IsEmpty { get { return this.Count == 0 && this.HasValue; } }

		/// <summary>Return a byte array containing all the bytes of the slice, or null if the slice is null</summary>
		/// <returns>Byte array or null</returns>
		public byte[] GetBytes()
		{
			if (Count == 0) return this.Array == null ? null : Slice.EmptyArray;
			var bytes = new byte[this.Count];
			Buffer.BlockCopy(this.Array, this.Offset, bytes, 0, bytes.Length);
			return bytes;
		}

		/// <summary>Stringify a slice containing only ASCII chars</summary>
		/// <returns>ASCII string, or null if the slice is null</returns>
		public string ToAscii()
		{
			return FdbKey.Ascii(this);
		}

		/// <summary>Stringify a slice containing an UTF-8 encoded string</summary>
		/// <returns>Unicode string, or null if the slice is null</returns>
		public string ToUnicode()
		{
			return FdbKey.Unicode(this);
		}

		/// <summary>Converts a slice using Base64 encoding</summary>
		/// <returns></returns>
		public string ToBase64()
		{
			if (Count == 0) return this.Array == null ? null : String.Empty;
			return Convert.ToBase64String(this.Array, this.Offset, this.Count);
		}

		/// <summary>Returns a new slice that contains an isolated copy of the buffer</summary>
		/// <returns>Slice that is equivalent, but is isolated from any changes to the buffer</returns>
		internal Slice Memoize()
		{
			if (!HasValue) return Slice.Nil;
			if (Count == 0) return Slice.Empty;
			return new Slice(GetBytes(), 0, this.Count);
		}

		/// <summary>Implicitly converts a Slice into an ArraySegment&lt;byte&gt;</summary>
		public static implicit operator ArraySegment<byte>(Slice value)
		{
			return new ArraySegment<byte>(value.Array, value.Offset, value.Count);
		}

		/// <summary>Implicitly converts an ArraySegment&lt;byte&gt; into a Slice</summary>
		public static implicit operator Slice(ArraySegment<byte> value)
		{
			return new Slice(value.Array, value.Offset, value.Count);
		}

		/// <summary>Compare two slices for equality</summary>
		/// <returns>True if the slice contains the same bytes</returns>
		public static bool operator ==(Slice a, Slice b)
		{
			return a.Equals(b);
		}

		/// <summary>Compare two slices for inequality</summary>
		/// <returns>True if the slice do not contain the same bytes</returns>
		public static bool operator !=(Slice a, Slice b)
		{
			return !a.Equals(b);
		}

		public override bool Equals(object obj)
		{
			if (obj == null) return this.Array == null;
			if (obj is Slice) return Equals((Slice)obj);
			if (obj is ArraySegment<byte>) return Equals((ArraySegment<byte>)obj);
			if (obj is byte[]) return Equals((byte[])obj);
			return false;
		}

		public override int GetHashCode()
		{
			if (this.Array != null)
			{
				//TODO: use a better hash algorithm? (CityHash, SipHash, ...)

				// <HACKHACK>: unoptimized 32 bits FNV-1a implementation
				uint h = 2166136261; // FNV1 32 bits offset basis
				var bytes = this.Array;
				int p = this.Offset;
				int count = this.Count;
				while(count-- > 0)
				{
					h = (h ^ bytes[p++]) * 16777619; // FNV1 32 prime
				}
				return (int)h;
				// </HACKHACK>
			}
			return 0;
		}

		public bool Equals(Slice other)
		{
			return this.Count == other.Count && SameBytes(this.Array, this.Offset, other.Array, other.Offset, this.Count);
		}

		public int CompareTo(Slice other)
		{
			if (!other.HasValue) return this.HasValue ? 1 : 0;
			return CompareBytes(this.Array, this.Offset, this.Count, other.Array, other.Offset, other.Count);
		}

		public bool Equals(ArraySegment<byte> other)
		{
			return this.Count == other.Count && SameBytes(this.Array, this.Offset, other.Array, other.Offset, this.Count);
		}

		public bool Equals(byte[] other)
		{
			if (other == null) return this.Array == null;
			return this.Count == other.Length && SameBytes(this.Array, this.Offset, other, 0, this.Count);
		}

		internal static bool SameBytes(byte[] left, int leftOffset, byte[] right, int rightOffset, int count)
		{
			Contract.Requires(count >= 0);
			Contract.Requires(leftOffset >= 0);
			Contract.Requires(rightOffset >= 0);

			if (left == null) return object.ReferenceEquals(right, null);
			if (object.ReferenceEquals(left, right)) return leftOffset == rightOffset;

			//TODO: ensure that there are enough bytes on both sides

			while (count-- > 0)
			{
				if (left[leftOffset++] != right[rightOffset++]) return false;
			}
			return true;
		}

		internal static int CompareBytes(byte[] left, int leftOffset, int leftCount, byte[] right, int rightOffset, int rightCount)
		{
			if (leftCount == rightCount && leftOffset == rightOffset && object.ReferenceEquals(left, right))
				return 0;

			int n = Math.Min(leftCount, rightCount);

			while (n-- > 0)
			{
				int d = right[rightOffset++] - left[leftOffset++];
				if (d != 0) return d;
			}

			return rightCount - leftCount;
		}

	}

}
