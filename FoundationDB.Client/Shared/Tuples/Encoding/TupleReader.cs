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

namespace Doxense.Collections.Tuples.Encoding
{
	using System;
	using System.Diagnostics;
	using System.Runtime.CompilerServices;
	using Doxense.Diagnostics.Contracts;
	using Doxense.Memory;
	using JetBrains.Annotations;

	[DebuggerDisplay("{Input.Position}/{Input.Buffer.Count} @ {Depth}")]
	public struct TupleReader
	{
		public SliceReader Input;
		public int Depth;

		[ MethodImpl(MethodImplOptions.AggressiveInlining)]
		public TupleReader(Slice buffer)
		{
			this.Input = new SliceReader(buffer);
			this.Depth = 0;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public TupleReader(Slice buffer, int depth)
		{
			this.Input = new SliceReader(buffer);
			this.Depth = depth;
		}

		public TupleReader(SliceReader input)
		{
			this.Input = input;
			this.Depth = 0;
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static TupleReader Embedded(Slice packed)
		{
			Contract.Requires(packed.Count >= 2 && packed[0] == TupleTypes.EmbeddedTuple && packed[-1] == 0);
			return new TupleReader(packed.Substring(1, packed.Count - 2), 1);
		}
	}

}
