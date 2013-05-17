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
		public static Slice Ascii(string text)
		{
			return text == null ? Slice.Nil : text.Length == 0 ? Slice.Empty : Slice.Create(Encoding.Default.GetBytes(text));
		}

		public static Slice Base64(string base64Encoded)
		{
			return base64Encoded == null ? Slice.Nil : base64Encoded.Length == 0 ? Slice.Empty : Slice.Create(Convert.FromBase64String(base64Encoded));
		}

		public static Slice Encode(byte[] value)
		{
			return Slice.Create(value);
		}

		public static Slice Encode(int value)
		{
			//HACKHACK: use something else! (endianness depends on plateform)
			return Slice.Create(BitConverter.GetBytes(value));
		}

		public static Slice Encode(long value)
		{
			//HACKHACK: use something else! (endianness depends on plateform)
			return Slice.Create(BitConverter.GetBytes(value));
		}

		public static Slice Encode(string value)
		{
			return value == null ? Slice.Nil : value.Length == 0 ? Slice.Empty : Slice.Create(Encoding.UTF8.GetBytes(value));
		}

		public static string Dump(Slice buffer)
		{
			if (buffer.IsNullOrEmpty) return buffer.HasValue ? "<empty>" : "<null>";

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
