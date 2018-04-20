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

namespace FoundationDB.Layers.Experimental.Indexing
{
	using System;
	using System.Diagnostics;

	/// <summary>Bounds of a Compressed Bitmaps, from the Lowest Set Bit to the Highest Set Bit</summary>
	[DebuggerDisplay("[{Lowest}, {Highest}]")]
	public struct BitRange : IEquatable<BitRange>
	{
		private const int LOWEST_UNDEFINED = 0;
		private const int HIGHEST_UNDEFINED = -1;

		public static BitRange Empty => new BitRange(LOWEST_UNDEFINED, HIGHEST_UNDEFINED);

		/// <summary>Index of the lowest bit that is set to 1 in the source Bitmap</summary>
		public readonly int Lowest;

		/// <summary>Index of the highest bit that is set to 1 in the source Bitmap</summary>
		public readonly int Highest;

		public bool IsEmpty => this.Highest < this.Lowest;

		public BitRange(int lowest, int highest)
		{
			this.Lowest = lowest;
			this.Highest = highest;
		}

		#region Boilerplate code...

		//TODO: opérateurs de tests d'intersection, append, ...

		public override string ToString()
		{
			return Lowest > Highest ? "[empty]" : String.Format("[{0}, {1}]", Lowest, Highest);
		}

		public bool Equals(BitRange other)
		{
			return other.Lowest == this.Lowest && other.Highest == this.Highest;
		}

		public override bool Equals(object obj)
		{
			return obj is BitRange && Equals((BitRange)obj);
		}

		public override int GetHashCode()
		{
			return unchecked(this.Lowest * 31) ^ this.Highest;
		}

		#endregion

	}

}
