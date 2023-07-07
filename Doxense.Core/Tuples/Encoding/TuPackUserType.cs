#region Copyright (c) 2005-2023 Doxense SAS
// All rights reserved.
// 
// Redistribution and use in source and binary forms, with or without
// modification, are permitted provided that the following conditions are met:
// 	* Redistributions of source code must retain the above copyright
// 	  notice, this list of conditions and the following disclaimer.
// 	* Redistributions in binary form must reproduce the above copyright
// 	  notice, this list of conditions and the following disclaimer in the
// 	  documentation and/or other materials provided with the distribution.
// 	* Neither the name of Doxense nor the
// 	  names of its contributors may be used to endorse or promote products
// 	  derived from this software without specific prior written permission.
// 
// THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND
// ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
// WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
// DISCLAIMED. IN NO EVENT SHALL DOXENSE BE LIABLE FOR ANY
// DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
// (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
// LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
// ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
// (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
// SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
#endregion

namespace Doxense.Collections.Tuples.Encoding
{
	using System;
	using System.Diagnostics;

	/// <summary>Represent a custom user type for the TuPack encoding</summary>
	[DebuggerDisplay("{ToString()},nq")]
	public sealed class TuPackUserType : IEquatable<TuPackUserType>
	{

		public static readonly TuPackUserType Directory = new TuPackUserType(0xFE);

		public static readonly TuPackUserType System = new TuPackUserType(0xFF);

		public TuPackUserType(int type)
		{
			this.Type = type;
		}

		public TuPackUserType(int type, Slice value)
		{
			this.Type = type;
			this.Value = value;
		}

		public readonly int Type;

		public readonly Slice Value;

		public override string ToString()
		{
			switch (this.Type)
			{
				case 0xFE: return "|Directory|";
				case 0xFF: return "|System|";
			}

			if (this.Value.IsNull)
			{
				return $"|User-{this.Type:X02}|";
			}
			return $"|User-{this.Type:X02}:{this.Value:N}|";
		}

		#region Equality...

		public override bool Equals(object? obj)
		{
			return obj is TuPackUserType ut && Equals(ut);
		}

		public bool Equals(TuPackUserType? other)
		{
			if (other == null) return false;
			if (ReferenceEquals(this, other)) return true;
			return this.Type == other.Type && this.Value.Equals(other.Value);
		}

		public override int GetHashCode()
		{
			return HashCodes.Combine(this.Type, this.Value.GetHashCode());
		}

		#endregion
	}
}
