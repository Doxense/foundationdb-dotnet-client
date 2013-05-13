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
	public static class FdbValue
	{
		public static ArraySegment<byte> Ascii(string text)
		{
			return new ArraySegment<byte>(Encoding.Default.GetBytes(text));
		}

		public static string Ascii(ArraySegment<byte> asciiBytes)
		{
			if (asciiBytes.Count == 0) return asciiBytes.Array == null ? null : String.Empty;
			return Encoding.Default.GetString(asciiBytes.Array, asciiBytes.Offset, asciiBytes.Count);
		}

		public static ArraySegment<byte> Base64(string base64Encoded)
		{
			return new ArraySegment<byte>(Convert.FromBase64String(base64Encoded));
		}

		public static string Base64(ArraySegment<byte> base64Encoded)
		{
			if (base64Encoded.Count == 0) return base64Encoded.Array == null ? null : String.Empty;
			return Convert.ToBase64String(base64Encoded.Array, base64Encoded.Offset, base64Encoded.Count);
		}

		public static ArraySegment<byte> Encode(byte[] value)
		{
			return value == null ? Fdb.Nil : value.Length == 0 ? Fdb.Empty : new ArraySegment<byte>(value);
		}

		public static ArraySegment<byte> Encode(int value)
		{
			//HACKHACK: use something else! (endianness depends on plateform)
			return new ArraySegment<byte>(BitConverter.GetBytes(value));
		}

		public static ArraySegment<byte> Encode(long value)
		{
			//HACKHACK: use something else! (endianness depends on plateform)
			return new ArraySegment<byte>(BitConverter.GetBytes(value));
		}

		public static ArraySegment<byte> Encode(string value)
		{
			if (value == null) return Fdb.Nil;
			if (value.Length == 0) return Fdb.Empty;
			return new ArraySegment<byte>(Encoding.UTF8.GetBytes(value));
		}

		public static string Decode(ArraySegment<byte> bytes)
		{
			if (bytes.Count == 0)
				return bytes.Array == null ? null : String.Empty;
			else
				return Encoding.UTF8.GetString(bytes.Array, bytes.Offset, bytes.Count);
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

	}

}
