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

namespace FoundationDB.Layers.Counters
{
	using FoundationDB.Client;
	using System;
	using System.Threading.Tasks;

	public sealed class FdbCounterMap
	{

		private static readonly Slice PlusOne = Slice.FromFixed64(1);
		private static readonly Slice MinusOne = Slice.FromFixed64(-1);

		public FdbCounterMap(FdbSubspace subspace)
		{
			if (subspace == null) throw new ArgumentNullException("subspace");

			this.Subspace = subspace;
		}

		/// <summary>Subspace used as a prefix for all items in this counter list</summary>
		public FdbSubspace Subspace { get; private set; }

		private Slice GetCounterKey(Slice counterKey)
		{
			if (counterKey.IsNull) throw new ArgumentNullException("counterKey");
			return this.Subspace.Concat(counterKey);
		}

		/// <summary>Add a value to a counter in one atomic operation</summary>
		/// <param name="transaction"></param>
		/// <param name="counterKey">Key of the counter, relative to the list's subspace</param>
		/// <param name="value">Value that will be added</param>
		/// <remarks>This operation will not cause the current transaction to conflict. It may create conflicts for transactions that would read the value of the counter.</remarks>
		public void Add(IFdbTransaction transaction, Slice counterKey, long value)
		{
			if (transaction == null) throw new ArgumentNullException("transaction");

			//REVIEW: we could no-op if value == 0 but this may change conflict behaviour for other transactions...
			Slice param = value == 1 ? PlusOne : value == -1 ? MinusOne : Slice.FromFixed64(value);
			transaction.AtomicAdd(GetCounterKey(counterKey), param);
		}

		/// <summary>Subtract a value from a counter in one atomic operation</summary>
		/// <param name="transaction">Transaction to use for the operation</param>
		/// <param name="counterKey">Key of the counter, relative to the list's subspace</param>
		/// <param name="value">Value that will be substracted. If the value is zero</param>
		/// <remarks>This operation will not cause the current transaction to conflict. It may create conflicts for transactions that would read the value of the counter.</remarks>
		public void Subtract(IFdbTransaction transaction, Slice counterKey, long value)
		{
			Add(transaction, counterKey, -value);
		}

		/// <summary>Increment the value of a counter in one atomic operation</summary>
		/// <param name="transaction">Transaction to use for the operation</param>
		/// <param name="counterKey">Key of the counter, relative to the list's subspace</param>
		/// <remarks>This operation will not cause the current transaction to conflict. It may create conflicts for transactions that would read the value of the counter.</remarks>
		public void Increment(IFdbTransaction transaction, Slice counterKey)
		{
			Add(transaction, counterKey, 1);
		}

		/// <summary>Decrement the value of a counter in one atomic operation</summary>
		/// <param name="transaction">Transaction to use for the operation</param>
		/// <param name="counterKey">Key of the counter, relative to the list's subspace</param>
		/// <remarks>This operation will not cause the current transaction to conflict. It may create conflicts for transactions that would read the value of the counter.</remarks>
		public void Decrement(IFdbTransaction transaction, Slice counterKey)
		{
			Add(transaction, counterKey, -1);
		}

		/// <summary>Read the value of a counter</summary>
		/// <param name="transaction">Transaction to use for the operation</param>
		/// <param name="counterKey">Key of the counter, relative to the list's subspace</param>
		/// <returns></returns>
		public async Task<long?> ReadAsync(IFdbReadOnlyTransaction transaction, Slice counterKey)
		{
			var data = await transaction.GetAsync(GetCounterKey(counterKey)).ConfigureAwait(false);
			if (data.IsNullOrEmpty) return default(long?);
			return data.ToInt64();
		}

		/// <summary>Adds a value to a counter, and return its new value.</summary>
		/// <param name="transaction">Transaction to use for the operation</param>
		/// <param name="counterKey">Key of the counter, relative to the list's subspace</param>
		/// <returns>New value of the counter. Returns <paramref name="value"/> if the counter did not exist previously.</returns>
		/// <remarks>This method WILL conflict with other transactions!</remarks>
		public async Task<long> AddThenReadAsync(IFdbTransaction transaction, Slice counterKey, long value)
		{
			if (transaction == null) throw new ArgumentNullException("transaction");

			var key = GetCounterKey(counterKey);
			var res = await transaction.GetAsync(key).ConfigureAwait(false);

			if (!res.IsNullOrEmpty) value += res.ToInt64();
			transaction.Set(key, Slice.FromFixed64(value));

			return value;
		}

		public Task<long> SubtractThenReadAsync(IFdbTransaction transaction, Slice counterKey, long value)
		{
			return AddThenReadAsync(transaction, counterKey, -value);
		}

		public Task<long> IncrementThenReadAsync(IFdbTransaction transaction, Slice counterKey)
		{
			return AddThenReadAsync(transaction, counterKey, 1);
		}

		public Task<long> DecrementThenReadAsync(IFdbTransaction transaction, Slice counterKey)
		{
			return AddThenReadAsync(transaction, counterKey, -1);
		}

		/// <summary>Adds a value to a counter, but return its previous value.</summary>
		/// <param name="transaction">Transaction to use for the operation</param>
		/// <param name="counterKey">Key of the counter, relative to the list's subspace</param>
		/// <returns>Previous value of the counter. Returns 0 if the counter did not exist previously.</returns>
		/// <remarks>This method WILL conflict with other transactions!</remarks>
		public async Task<long> ReadThenAddAsync(IFdbTransaction transaction, Slice counterKey, long value)
		{
			if (transaction == null) throw new ArgumentNullException("transaction");

			var key = GetCounterKey(counterKey);
			var res = await transaction.GetAsync(key).ConfigureAwait(false);

			long previous = res.IsNullOrEmpty ? 0 : res.ToInt64();
			transaction.Set(key, Slice.FromFixed64(value + previous));

			return previous;
		}

		public Task<long> ReadThenSubtractAsync(IFdbTransaction transaction, Slice counterKey, long value)
		{
			return ReadThenAddAsync(transaction, counterKey, -value);
		}

		public Task<long> ReadThenIncrementAsync(IFdbTransaction transaction, Slice counterKey)
		{
			return ReadThenAddAsync(transaction, counterKey, 1);
		}

		public Task<long> ReadThenDecrementAsync(IFdbTransaction transaction, Slice counterKey)
		{
			return ReadThenAddAsync(transaction, counterKey, -1);
		}

	}

}
