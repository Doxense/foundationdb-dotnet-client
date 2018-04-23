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
	using System;
	using System.Collections.Generic;
	using System.Diagnostics;
	using Doxense.Diagnostics.Contracts;

	[DebuggerDisplay("Mode={m_mode}")]
	public sealed class KeyRangeComparer : IComparer<KeyRange>, IEqualityComparer<KeyRange>
	{
		private const int BOTH = 0;
		private const int BEGIN = 1;
		private const int END = 2;

		public static readonly KeyRangeComparer Default = new KeyRangeComparer(BOTH);
		public static readonly KeyRangeComparer Begin = new KeyRangeComparer(BEGIN);
		public static readonly KeyRangeComparer End = new KeyRangeComparer(END);

		private readonly int m_mode;

		private KeyRangeComparer(int mode)
		{
			Contract.Requires(mode >= BOTH && mode <= END);
			m_mode = mode;
		}

		public int Compare(KeyRange x, KeyRange y)
		{
			switch (m_mode)
			{
				case BEGIN: return x.Begin.CompareTo(y.Begin);
				case END: return x.End.CompareTo(y.End);
				default: return x.CompareTo(y);
			}
		}

		public bool Equals(KeyRange x, KeyRange y)
		{
			switch(m_mode)
			{
				case BEGIN: return x.Begin.Equals(y.Begin);
				case END: return x.End.Equals(y.End);
				default: return x.Equals(y);
			}
		}

		public int GetHashCode(KeyRange obj)
		{
			switch(m_mode)
			{
				case BEGIN: return obj.Begin.GetHashCode();
				case END: return obj.End.GetHashCode();
				default: return obj.GetHashCode();
			}
		}
	}

}
