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

namespace FoundationDb.Client
{
	using System;
	using System.Diagnostics;

	/// <summary>Query describing a GetRange operation that will be performed at a later time</summary>
	/// <remarks>This object is reusable</remarks>
	[DebuggerDisplay("Begin={Begin}, End={End}, Limit={Limit}, TargetBytes={TargetBytes}, Mode={Mode}, Snapshot={Snapshot}, Reverse={Reverse}")]
	public sealed class FdbRangeSelector
	{
		/// <summary>Key selector describing the beginning of the range</summary>
		public FdbKeySelector Begin { get; internal set; }

		/// <summary>Key selector describing the end of the range</summary>
		public FdbKeySelector End { get; internal set; }

		/// <summary>Limit in number of rows to return</summary>
		public int Limit { get; internal set; }

		/// <summary>Limit in number of bytes to return</summary>
		public int TargetBytes { get; internal set; }

		/// <summary>Streaming mode</summary>
		public FdbStreamingMode Mode { get; internal set; }

		/// <summary>Should we perform the range using snapshot mode ?</summary>
		public bool Snapshot { get; internal set; }

		/// <summary>Should the results returned in reverse order (from last key to first key)</summary>
		public bool Reverse { get; internal set; }

		//TODO: fluent API ?
		// => should it be mutable ("modify_this(); return this;") or immutable ("return modified_copy_of_this();") ?

		public bool HasLimit { get { return this.Limit > 0; } }

		public override string ToString()
		{
			return "(" + this.Begin.ToString() + " ... " + this.End.ToString() + ")";
		}
	}

}
