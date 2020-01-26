#region BSD License
/* Copyright (c) 2013-2020, Doxense SAS
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
	using System.Threading.Tasks;

	/// <summary>Represents a FoundationDB Layer that uses a metadata cache to speed up operations</summary>
	/// <typeparam name="TState">Type of the state that is linked to each transaction lifetime.</typeparam>
	public interface IFdbLayer<TState>
	{

		// A typical Layer will be a thin wrapper over a subspace location and some options/encoders/helpers
		// When a transaction wants to interact with the Layer, is must first call Resolve(...) at least once within the transaction,
		// and then call any method necessary on that state object.
		// Storing and reusing state instances outside the transaction is NOT ALLOWED.
		// The Layer implementation should try to cache complex metadata in order to reduce the latency.
		// If the layer uses lower level layers, it should NOT attempt to cache these in its own state, and instead call these layer's own "Resolve" methods
		// This means that layers should not cache the TState type itself, and instead cache a "TCache" object, and create a new "TState" instance for each new transaction, that wraps the TCache

		/// <summary>Resolve the state for this layer, applicable to the current transaction</summary>
		/// <param name="tr">Transaction that will be used to interact with this layer</param>
		/// <returns>State handler that is valid only for this transaction.</returns>
		/// <remarks>
		/// Even though most layers will attempt to cache complex metadata, it is best practice to not call this methods multiple times within the same transaction.
		/// Accessing the returned state instance outside of the <paramref name="tr">transaction</paramref> scope (after commit, in the next retry loop attempt, ...) has undefined behavior and should be avoided.
		/// </remarks>
		ValueTask<TState> Resolve(IFdbReadOnlyTransaction tr);
	}
}
