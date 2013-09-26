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
	using System.Threading;
	using System.Threading.Tasks;

	/// <summary>Transaction that allows read operations</summary>
	public interface IFdbReadOnlyTransaction : IDisposable
	{
		/// <summary>
		/// Local id of the transaction
		/// </summary>
		/// <remarks>Should only be used for diagnosticts and logging</remarks>
		int Id { get; }

		/// <summary>
		/// Context of this transaction.
		/// </summary>
		FdbOperationContext Context { get; }

		/// <summary>
		/// If true, the transaction is operating in Snapshot mode
		/// </summary>
		bool IsSnapshot { get; }

		/// <summary>Cancellation Token linked to the life time of the transaction</summary>
		/// <remarks>Will be triggered if the transaction is aborted or disposed</remarks>
		CancellationToken Token { get; }

		/// <summary>
		/// Ensure thats the transaction is in a valid state for issuing read operations.
		/// </summary>
		/// <exception cref="System.ObjectDisposedException">If <see cref="IDisposable.Dispose">Dispose()</see> has already been called on the transaction</exception>
		/// <exception cref="System.InvalidOperationException">If the transaction as already been committed, or if the database connection has been closed</exception>
		/// <exception cref="System.OperationCanceledException">If the cancellation token has been cancelled</exception>
		void EnsureCanRead();

		/// <summary>
		/// Reads a value from the database snapshot represented by by the current transaction.
		/// </summary>
		/// <param name="keyBytes">Key to be looked up in the database</param>
		/// <returns>Task that will return the value of the key if it is found, Slice.Nil if the key does not exist, or an exception</returns>
		/// <exception cref="System.ArgumentException">If the <paramref name="keyBytes"/> is null</exception>
		/// <exception cref="System.OperationCanceledException">If the cancellation token is already triggered</exception>
		/// <exception cref="System.ObjectDisposedException">If the transaction has already been completed</exception>
		/// <exception cref="System.InvalidOperationException">If the operation method is called from the Network Thread</exception>
		Task<Slice> GetAsync(Slice keyBytes);

		/// <summary>
		/// Reads several values from the database snapshot represented by the current transaction
		/// </summary>
		/// <param name="keys">Keys to be looked up in the database</param>
		/// <returns>Task that will return an array of values, or an exception. Each item in the array will contain the value of the key at the same index in <paramref name="keys"/>, or Slice.Nil if that key does not exist.</returns>
		Task<Slice[]> GetValuesAsync(Slice[] keys);

		/// <summary>
		/// Resolves a key selector against the keys in the database snapshot represented by the current transaction.
		/// </summary>
		/// <param name="selector">Key selector to resolve</param>
		/// <returns>Task that will return the key matching the selector, or an exception</returns>
		Task<Slice> GetKeyAsync(FdbKeySelector selector);

		/// <summary>
		/// Resolves several key selectors against the keys in the database snapshot represented by the current transaction.
		/// </summary>
		/// <param name="selectors">Key selectors to resolve</param>
		/// <returns>Task that will return an array of keys matching the selectors, or an exception</returns>
		Task<Slice[]> GetKeysAsync(FdbKeySelector[] selectors);

		/// <summary>
		/// Reads all key-value pairs in the database snapshot represented by transaction (potentially limited by Limit, TargetBytes, or Mode)
		/// which have a key lexicographically greater than or equal to the key resolved by the begin key selector
		/// and lexicographically less than the key resolved by the end key selector.
		/// </summary>
		/// <param name="range">Pair of key selectors defining the beginning and the end of the range</param>
		/// <param name="options">Optionnal query options (Limit, TargetBytes, Mode, Reverse, ...)</param>
		/// <param name="iteration">If streaming mode is FdbStreamingMode.Iterator, this parameter should start at 1 and be incremented by 1 for each successive call while reading this range. In all other cases it is ignored.</param>
		/// <returns></returns>
		Task<FdbRangeChunk> GetRangeAsync(FdbKeySelectorPair range, FdbRangeOptions options = null, int iteration = 0);
		
		/// <summary>
		/// Create a new range query that will read all key-value pairs in the database snapshot represented by the transaction
		/// </summary>
		/// <param name="range">Pair of key selectors defining the beginning and the end of the range</param>
		/// <param name="options">Optionnal query options (Limit, TargetBytes, Mode, Reverse, ...)</param>
		/// <returns>Range query that, once executed, will return all the key-value pairs matching the providing selector pair</returns>
		FdbRangeQuery<KeyValuePair<Slice, Slice>> GetRange(FdbKeySelectorPair range, FdbRangeOptions options = null);

		/// <summary>
		/// Returns this transaction snapshot read version.
		/// </summary>
		Task<long> GetReadVersionAsync();

	}

}
