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

namespace Doxense.Linq
{

	/// <summary>
	/// Defines the intent of a consumer of an async iterator
	/// </summary>
	public enum AsyncIterationHint
	{
		/// <summary>
		/// Use the default settings. The provider will make no attempt at optimizing the query.
		/// </summary>
		Default = 0,

		/// <summary>
		/// The query will be consumed by chunks and may be aborted at any point. The provider will produce small chunks of data for the first few reads but should still be efficient if the caller consume all the sequence.
		/// </summary>
		Iterator,

		/// <summary>
		/// The query will consume all the items in the source. The provider will produce large chunks of data immediately, and reduce the number of pages needed to consume the sequence.
		/// </summary>
		All,

		/// <summary>
		/// The query will consume the first element (or a very small fraction) of the source. The provider will only produce data in small chunks and expect the caller to abort after one or two iterations. This can also be used to reduce the latency of the first result.
		/// </summary>
		Head,

		/// <summary>
		/// The query will consume all (or most of) the items of a very large sequence of data. The provider will use the appropriate page size in ordre to optimize the bandwith.
		/// </summary>
		Bulk,

	}

}

#endif
