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

namespace FoundationDB.Async
{
	using System;
	using System.Threading;
	using System.Threading.Tasks;

	/// <summary>Defines a pump that can move items between a source and a target</summary>
	public interface IAsyncPump<T> : IDisposable
	{
		/// <summary>Source of the pump (that produces new items)</summary>
		IAsyncSource<T> Source { get; }

		/// <summary>Target of the tump (that will consume the items)</summary>
		IAsyncTarget<T> Target { get; }

		/// <summary>True if all the items of the source have been consumed by the target</summary>
		bool IsCompleted { get; }

		/// <summary>Consume all the items of the source by passing them to the Target</summary>
		/// <param name="stopOnFirstError">If true, aborts on the first error. If false, continue processing items until the source has finished.</param>
		/// <param name="cancellationToken">Cancellation token that can be used to abort the pump at any time. Any unprocessed items will be lost.</param>
		/// <returns>Task that will complete successfully if all the items from the source have been processed by the target, or fails if an error occurred or the pump was cancelled.</returns>
		Task PumpAsync(bool stopOnFirstError, CancellationToken cancellationToken);
	}

}
