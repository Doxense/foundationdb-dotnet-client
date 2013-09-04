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

namespace FoundationDB.Client
{
	using FoundationDB.Layers.Tuples;
	using System.Diagnostics;
	using System.Text;

	[DebuggerDisplay("{ToString()}")]
	public struct FdbKeySelector
	{
		public static readonly FdbKeySelector None = default(FdbKeySelector);

		public readonly Slice Key;
		public readonly bool OrEqual;
		public readonly int Offset;

		public FdbKeySelector(Slice key, bool orEqual, int offset)
		{
			this.Key = key;
			this.OrEqual = orEqual;
			this.Offset = offset;
		}

		public override string ToString()
		{
			var sb = new StringBuilder();
			int offset = this.Offset;
			if (offset < 1)
			{
				sb.Append(this.OrEqual ? "lLE( " : "lLT( ");
			}
			else
			{
				--offset;
				sb.Append(this.OrEqual ? "fGT( " : "fGE( ");
			}
			sb.Append(FdbKey.Dump(this.Key));
			sb.Append(" )");

			if (offset > 0) 
				sb.Append(" + ").Append(offset.ToString());
			else if (offset < 0)
				sb.Append(" - ").Append((-offset).ToString());

			return sb.ToString();
		}

		public static FdbKeySelector LastLessThan(IFdbTuple key)
		{
			return LastLessThan(key.ToSlice());
		}

		public static FdbKeySelector LastLessOrEqual(IFdbTuple key)
		{
			return LastLessOrEqual(key.ToSlice());
		}

		public static FdbKeySelector FirstGreaterThan(IFdbTuple key)
		{
			return FirstGreaterThan(key.ToSlice());
		}

		public static FdbKeySelector FirstGreaterOrEqual(IFdbTuple key)
		{
			return FirstGreaterOrEqual(key.ToSlice());
		}

		public static FdbKeySelector LastLessThan(Slice key)
		{
			// #define FDB_KEYSEL_LAST_LESS_THAN(k, l) k, l, 0, 0
			return new FdbKeySelector(key, false, 0);
		}

		public static FdbKeySelector LastLessOrEqual(Slice key)
		{
			// #define FDB_KEYSEL_LAST_LESS_OR_EQUAL(k, l) k, l, 1, 0
			return new FdbKeySelector(key, true, 0);
		}

		public static FdbKeySelector FirstGreaterThan(Slice key)
		{
			// #define FDB_KEYSEL_FIRST_GREATER_THAN(k, l) k, l, 1, 1
			return new FdbKeySelector(key, true, 1);
		}

		/// <summary>Return a key selector that will select the first key that is greater than or equal to <paramref name="key"/></summary>
		/// <param name="key"></param>
		/// <returns></returns>
		public static FdbKeySelector FirstGreaterOrEqual(Slice key)
		{
			// #define FDB_KEYSEL_FIRST_GREATER_OR_EQUAL(k, l) k, l, 0, 1
			return new FdbKeySelector(key, false, 1);
		}

		/// <summary>Add a value to the selector's offset</summary>
		/// <param name="selector">ex: fGE('abc')</param>
		/// <param name="offset">ex: 7</param>
		/// <returns>fGE('abc')+7</returns>
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
