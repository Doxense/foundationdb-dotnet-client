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
using System;
using System.Diagnostics;

namespace FoundationDb.Client
{

	[DebuggerDisplay("Key=[{Key.Count}], OrEqual={OrEqual}, Offset={Offset}")]
	public struct FdbKeySelector
	{
		public readonly ArraySegment<byte> Key;
		public readonly bool OrEqual;
		public readonly int Offset;

		public FdbKeySelector(ArraySegment<byte> key, bool orEqual, int offset)
		{
			this.Key = key;
			this.OrEqual = orEqual;
			this.Offset = offset;
		}

		public static FdbKeySelector LastLessThan(IFdbKey key)
		{
			return LastLessThan(key.ToArraySegment());
		}

		public static FdbKeySelector LastLessOrEqual(IFdbKey key)
		{
			return LastLessOrEqual(key.ToArraySegment());
		}

		public static FdbKeySelector FirstGreaterThan(IFdbKey key)
		{
			return FirstGreaterThan(key.ToArraySegment());
		}

		public static FdbKeySelector FirstGreaterOrEqual(IFdbKey key)
		{
			return FirstGreaterOrEqual(key.ToArraySegment());
		}

		public static FdbKeySelector LastLessThan(ArraySegment<byte> key)
		{
			// #define FDB_KEYSEL_LAST_LESS_THAN(k, l) k, l, 0, 0
			return new FdbKeySelector(key, false, 0);
		}

		public static FdbKeySelector LastLessOrEqual(ArraySegment<byte> key)
		{
			// #define FDB_KEYSEL_LAST_LESS_OR_EQUAL(k, l) k, l, 1, 0
			return new FdbKeySelector(key, true, 0);
		}

		public static FdbKeySelector FirstGreaterThan(ArraySegment<byte> key)
		{
			// #define FDB_KEYSEL_FIRST_GREATER_THAN(k, l) k, l, 1, 1
			return new FdbKeySelector(key, true, 1);
		}

		public static FdbKeySelector FirstGreaterOrEqual(ArraySegment<byte> key)
		{
			// #define FDB_KEYSEL_FIRST_GREATER_OR_EQUAL(k, l) k, l, 0, 1
			return new FdbKeySelector(key, false, 1);
		}

		public static FdbKeySelector operator +(FdbKeySelector selector, int offset)
		{
			return new FdbKeySelector(selector.Key, selector.OrEqual, selector.Offset + offset);
		}

		public static FdbKeySelector operator -(FdbKeySelector selector, int offset)
		{
			return new FdbKeySelector(selector.Key, selector.OrEqual, selector.Offset - offset);
		}

	}

}
