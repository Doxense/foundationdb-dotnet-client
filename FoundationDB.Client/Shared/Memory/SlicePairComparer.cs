#region BSD License
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

#if !USE_SHARED_FRAMEWORK

namespace Doxense.Memory
{
	using System;
	using System.Collections.Generic;
	using Doxense.Diagnostics.Contracts;

	/// <summary>Performs optimized equality and comparison checks on key/value pairs of <see cref="Slice"/></summary>
	public sealed class SlicePairComparer : IComparer<KeyValuePair<Slice, Slice>>, IEqualityComparer<KeyValuePair<Slice, Slice>>
	{
		//TODO: move this inside Slmice? (Slice.PairComparer.Default ...)

		private const int BOTH = 0;
		private const int KEY_ONLY = 1;
		private const int VALUE_ONLY = 2;

		/// <summary>Compare both keys and values</summary>
		public static readonly SlicePairComparer Default = new SlicePairComparer(BOTH);

		/// <summary>Compare only the key of the pair</summary>
		public static readonly SlicePairComparer KeyOnly = new SlicePairComparer(KEY_ONLY);

		/// <summary>Compare only the value of the pair</summary>
		public static readonly SlicePairComparer ValueOnly = new SlicePairComparer(VALUE_ONLY);

		private readonly int m_mode;

		private SlicePairComparer(int mode)
		{
			Contract.Debug.Requires(mode >= BOTH && mode <= VALUE_ONLY);
			m_mode = mode;
		}

		public int Compare(KeyValuePair<Slice, Slice> x, KeyValuePair<Slice, Slice> y)
		{
			switch (m_mode)
			{
				case KEY_ONLY: return x.Key.CompareTo(y.Key);
				case VALUE_ONLY: return x.Value.CompareTo(y.Value);
				default:
				{
					int c = x.Key.CompareTo(y.Key);
					if (c == 0) c = x.Value.CompareTo(y.Value);
					return c;
				}
			}
		}

		public bool Equals(KeyValuePair<Slice, Slice> x, KeyValuePair<Slice, Slice> y)
		{
			switch(m_mode)
			{
				case KEY_ONLY: return x.Key.Equals(y.Key);
				case VALUE_ONLY: return x.Value.Equals(y.Value);
				default: return x.Key.Equals(y.Key) && x.Value.Equals(y.Value);
			}
		}

		public int GetHashCode(KeyValuePair<Slice, Slice> obj)
		{
			switch(m_mode)
			{
				case KEY_ONLY: return obj.Key.GetHashCode();
				case VALUE_ONLY: return obj.Value.GetHashCode();
				default: return (obj.Key.GetHashCode() * 31) + obj.Value.GetHashCode();
			}
		}
	}

}

#endif
