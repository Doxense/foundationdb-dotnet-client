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
using System.Text;

namespace FoundationDb.Client.Tuples
{

	/// <summary>Factory class for keys</summary>
	public static class FdbKey
	{
		/// <summary>Smallest possible key ('\0')</summary>
		public static readonly FdbByteKey MinValue = new FdbByteKey(new byte[1] { 0 }, 0, 1);

		/// <summary>Bigest possible key ('\xFF'), exclusing the system keys</summary>
		public static readonly FdbByteKey MaxValue = new FdbByteKey(new byte[1] { 255 }, 0, 1);

		public static IFdbKey Ascii(string text)
		{
			return new FdbByteKey(Encoding.Default.GetBytes(text));
		}

		public static IFdbKey Unicode(string text)
		{
			return new FdbByteKey(Encoding.UTF8.GetBytes(text));
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
