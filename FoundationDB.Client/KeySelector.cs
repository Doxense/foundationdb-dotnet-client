#region BSD Licence
/* Copyright (c) 2013-2018, Doxense SAS
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
	using System;
	using System.Diagnostics;
	using System.Text;
	using JetBrains.Annotations;

	/// <summary>Defines a selector for a key in the database</summary>
	[DebuggerDisplay("{ToString(),nq}")]
	[PublicAPI]
	public readonly struct KeySelector : IEquatable<KeySelector>
	{

		/// <summary>Key of the selector</summary>
		public readonly Slice Key;

		/// <summary>If true, the selected key can be equal to <see cref="Key"/>.</summary>
		public readonly bool OrEqual;

		/// <summary>Offset of the selected key</summary>
		public readonly int Offset;

		/// <summary>Creates a new selector</summary>
		public KeySelector(Slice key, bool orEqual, int offset)
		{
			Key = key;
			this.OrEqual = orEqual;
			this.Offset = offset;
		}

		/// <summary>Empty key selector</summary>
		public static readonly KeySelector None;

		public bool Equals(KeySelector other)
		{
			return this.Offset == other.Offset && this.OrEqual == other.OrEqual && Key.Equals(other.Key);
		}

		public override bool Equals(object obj)
		{
			return obj is KeySelector selector && Equals(selector);
		}

		public override int GetHashCode()
		{
			// ReSharper disable once NonReadonlyMemberInGetHashCode
			return Key.GetHashCode() ^ this.Offset ^ (this.OrEqual ? 0 : -1);
		}

		/// <summary>Creates a key selector that will select the last key that is less than <paramref name="key"/></summary>
		public static KeySelector LastLessThan(Slice key)
		{
			// #define FDB_KEYSEL_LAST_LESS_THAN(k, l) k, l, 0, 0
			return new KeySelector(key, false, 0);
		}

		/// <summary>Creates a key selector that will select the last key that is less than or equal to <paramref name="key"/></summary>
		public static KeySelector LastLessOrEqual(Slice key)
		{
			// #define FDB_KEYSEL_LAST_LESS_OR_EQUAL(k, l) k, l, 1, 0
			return new KeySelector(key, true, 0);
		}

		/// <summary>Creates a key selector that will select the first key that is greater than <paramref name="key"/></summary>
		public static KeySelector FirstGreaterThan(Slice key)
		{
			// #define FDB_KEYSEL_FIRST_GREATER_THAN(k, l) k, l, 1, 1
			return new KeySelector(key, true, 1);
		}

		/// <summary>Creates a key selector that will select the first key that is greater than or equal to <paramref name="key"/></summary>
		public static KeySelector FirstGreaterOrEqual(Slice key)
		{
			// #define FDB_KEYSEL_FIRST_GREATER_OR_EQUAL(k, l) k, l, 0, 1
			return new KeySelector(key, false, 1);
		}

		/// <summary>Add a value to the selector's offset</summary>
		/// <param name="selector">ex: fGE('abc')</param>
		/// <param name="offset">ex: 7</param>
		/// <returns>fGE('abc')+7</returns>
		public static KeySelector operator +(KeySelector selector, int offset)
		{
			return new KeySelector(selector.Key, selector.OrEqual, selector.Offset + offset);
		}

		/// <summary>Substract a value to the selector's offset</summary>
		/// <param name="selector">ex: fGE('abc')</param>
		/// <param name="offset">ex: 7</param>
		/// <returns>fGE('abc')-7</returns>
		public static KeySelector operator -(KeySelector selector, int offset)
		{
			return new KeySelector(selector.Key, selector.OrEqual, selector.Offset - offset);
		}

		public static bool operator ==(KeySelector left, KeySelector right)
		{
			return left.Equals(right);
		}

		public static bool operator !=(KeySelector left, KeySelector right)
		{
			return !left.Equals(right);
		}

		/// <summary>Converts the value of the current <see cref="KeySelector"/> object into its equivalent string representation</summary>
		public override string ToString()
		{
			return PrettyPrint(FdbKey.PrettyPrintMode.Single);
		}

		/// <summary>Returns a displayable representation of the key selector</summary>
		[Pure]
		public string PrettyPrint(FdbKey.PrettyPrintMode mode)
		{
			var sb = new StringBuilder();
			int offset = this.Offset;
			if (offset < 1)
			{
				sb.Append(this.OrEqual ? "lLE{" : "lLT{");
			}
			else
			{
				--offset;
				sb.Append(this.OrEqual ? "fGT{" : "fGE{");
			}
			sb.Append(FdbKey.PrettyPrint(Key, mode));
			sb.Append("}");

			if (offset > 0)
				sb.Append(" + ").Append(offset);
			else if (offset < 0)
				sb.Append(" - ").Append(-offset);

			return sb.ToString();
		}

	}

}
