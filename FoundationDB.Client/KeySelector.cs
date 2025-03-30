#region Copyright (c) 2023-2025 SnowBank SAS, (c) 2005-2023 Doxense SAS
// All rights reserved.
// 
// Redistribution and use in source and binary forms, with or without
// modification, are permitted provided that the following conditions are met:
// 	* Redistributions of source code must retain the above copyright
// 	  notice, this list of conditions and the following disclaimer.
// 	* Redistributions in binary form must reproduce the above copyright
// 	  notice, this list of conditions and the following disclaimer in the
// 	  documentation and/or other materials provided with the distribution.
// 	* Neither the name of SnowBank nor the
// 	  names of its contributors may be used to endorse or promote products
// 	  derived from this software without specific prior written permission.
// 
// THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND
// ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
// WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
// DISCLAIMED. IN NO EVENT SHALL SNOWBANK SAS BE LIABLE FOR ANY
// DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
// (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
// LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
// ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
// (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
// SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
#endregion

namespace FoundationDB.Client
{
	using System.Diagnostics;
	using System.Diagnostics.CodeAnalysis;
	using System.Text;
	using System.Runtime.CompilerServices;

	/// <summary>Defines a selector for a key in the database</summary>
	[DebuggerDisplay("{ToString(),nq}")]
	[PublicAPI]
	public readonly struct KeySelector : IEquatable<KeySelector>, IFormattable
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
			this.Key = key;
			this.OrEqual = orEqual;
			this.Offset = offset;
		}

		/// <inheritdoc />
		public bool Equals(KeySelector other)
		{
			return this.Offset == other.Offset && this.OrEqual == other.OrEqual && this.Key.Equals(other.Key);
		}

		/// <inheritdoc />
		public override bool Equals([NotNullWhen(true)] object? obj)
		{
			return obj is KeySelector selector && Equals(selector);
		}

		/// <inheritdoc />
		public override int GetHashCode()
		{
			// ReSharper disable once NonReadonlyMemberInGetHashCode
			return this.Key.GetHashCode() ^ this.Offset ^ (this.OrEqual ? 0 : -1);
		}

		/// <summary>Deconstructs a key selector into its basic elements</summary>
		/// <param name="key"><see cref="Key"/> field of the selector</param>
		/// <param name="orEqual"><see cref="OrEqual"/> field of the selector</param>
		/// <param name="offset"><see cref="Offset"/> field of the selector</param>
		public void Deconstruct(out Slice key, out bool orEqual, out int offset)
		{
			key = this.Key;
			orEqual = this.OrEqual;
			offset = this.Offset;
		}

		/// <summary>Creates a key selector that will select the last key that is less than <paramref name="key"/></summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static KeySelector LastLessThan(Slice key)
		{
			// #define FDB_KEYSEL_LAST_LESS_THAN(k, l) k, l, 0, 0
			return new KeySelector(key, false, 0);
		}

		/// <summary>Creates a key selector that will select the last key that is less than or equal to <paramref name="key"/></summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static KeySelector LastLessOrEqual(Slice key)
		{
			// #define FDB_KEYSEL_LAST_LESS_OR_EQUAL(k, l) k, l, 1, 0
			return new KeySelector(key, true, 0);
		}

		/// <summary>Creates a key selector that will select the first key that is greater than <paramref name="key"/></summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
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

		/// <summary>Adds a value to the selector's offset</summary>
		/// <param name="selector">ex: fGE('abc')</param>
		/// <param name="offset">ex: 7</param>
		/// <returns><c>fGE{'abc'} + 7</c></returns>
		public static KeySelector operator +(KeySelector selector, int offset)
		{
			return new KeySelector(selector.Key, selector.OrEqual, checked(selector.Offset + offset));
		}

		/// <summary>Subtracts a value from the selector's offset</summary>
		/// <param name="selector">ex: fGE('abc')</param>
		/// <param name="offset">ex: 7</param>
		/// <returns><c>fGE{'abc'} - 7</c></returns>
		public static KeySelector operator -(KeySelector selector, int offset)
		{
			return new KeySelector(selector.Key, selector.OrEqual, checked(selector.Offset - offset));
		}

		/// <summary>Increments the selector's offset</summary>
		/// <param name="selector">ex: fGE('abc')</param>
		/// <returns><c>fGE{'abc'} + 1</c></returns>
		public static KeySelector operator ++(KeySelector selector)
		{
			return new KeySelector(selector.Key, selector.OrEqual, checked(selector.Offset + 1));
		}

		/// <summary>Decrement the selector's offset</summary>
		/// <param name="selector">ex: fGE('abc')</param>
		/// <returns><c>fGE{'abc'} - 1</c></returns>
		public static KeySelector operator --(KeySelector selector)
		{
			return new KeySelector(selector.Key, selector.OrEqual, checked(selector.Offset - 1));
		}

		/// <summary>Tests if two key selectors are equal</summary>
		public static bool operator ==(KeySelector left, KeySelector right)
		{
			return left.Equals(right);
		}

		/// <summary>Tests if two key selectors are not equal</summary>
		public static bool operator !=(KeySelector left, KeySelector right)
		{
			return !left.Equals(right);
		}

		/// <summary>Converts the value of the current <see cref="KeySelector"/> object into its equivalent string representation</summary>
		public override string ToString() => PrettyPrint(FdbKey.PrettyPrintMode.Single);

		/// <inheritdoc />
		public string ToString(string? format, IFormatProvider? formatProvider) => ToString();

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
			sb.Append(FdbKey.PrettyPrint(this.Key, mode))
			  .Append('}');

			if (offset > 0)
			{
				sb.Append(" + ").Append(offset);
			}
			else if (offset < 0)
			{
				sb.Append(" - ").Append(-offset);
			}

			return sb.ToString();
		}
	}

}
