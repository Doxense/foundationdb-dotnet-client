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
	using System.IO;
	using System.Linq;
	using System.Threading;
	using System.Threading.Tasks;

	public static class FdbTransactionExtensions
	{

		#region Fluent...

		/// <summary>Allows this transaction to read and modify system keys (those that start with the byte 0xFF)</summary>
		public static TTransaction WithAccessToSystemKeys<TTransaction>(this TTransaction trans)
			where TTransaction : IFdbReadOnlyTransaction
		{
			trans.SetOption(FdbTransactionOption.AccessSystemKeys);
			//TODO: cache this into a local variable ?
			return trans;
		}

		/// <summary>Specifies that this transaction should be treated as highest priority and that lower priority transactions should block behind this one. Use is discouraged outside of low-level tools</summary>
		public static TTransaction WithPrioritySystemImmediate<TTransaction>(this TTransaction trans)
			where TTransaction : IFdbReadOnlyTransaction
		{
			trans.SetOption(FdbTransactionOption.PrioritySystemImmediate);
			//TODO: cache this into a local variable ?
			return trans;
		}

		/// <summary>Specifies that this transaction should be treated as low priority and that default priority transactions should be processed first. Useful for doing batch work simultaneously with latency-sensitive work</summary>
		public static TTransaction WithPriorityBatch<TTransaction>(this TTransaction trans)
			where TTransaction : IFdbReadOnlyTransaction
		{
			trans.SetOption(FdbTransactionOption.PriorityBatch);
			//TODO: cache this into a local variable ?
			return trans;
		}

		/// <summary>The next write performed on this transaction will not generate a write conflict range. As a result, other transactions which read the key(s) being modified by the next write will not conflict with this transaction. Care needs to be taken when using this option on a transaction that is shared between multiple threads. When setting this option, write conflict ranges will be disabled on the next write operation, regardless of what thread it is on.</summary>
		public static TTransaction WithNextWriteNoWriteConflictRange<TTransaction>(this TTransaction trans)
			where TTransaction : IFdbReadOnlyTransaction
		{
			trans.SetOption(FdbTransactionOption.NextWriteNoWriteConflictRange);
			return trans;
		}

		/// <summary>Set a timeout in milliseconds which, when elapsed, will cause the transaction automatically to be cancelled. Valid parameter values are ``[0, INT_MAX]``. If set to 0, will disable all timeouts. All pending and any future uses of the transaction will throw an exception. The transaction can be used again after it is reset.</summary>
		/// <param name="timeout">Timeout (with millisecond precision), or TimeSpan.Zero for infinite timeout</param>
		public static TTransaction WithTimeout<TTransaction>(this TTransaction trans, TimeSpan timeout)
			where TTransaction : IFdbReadOnlyTransaction
		{
			return WithTimeout<TTransaction>(trans, timeout == TimeSpan.Zero ? 0 : (int)Math.Ceiling(timeout.TotalMilliseconds));
		}

		/// <summary>Set a timeout in milliseconds which, when elapsed, will cause the transaction automatically to be cancelled. Valid parameter values are ``[0, INT_MAX]``. If set to 0, will disable all timeouts. All pending and any future uses of the transaction will throw an exception. The transaction can be used again after it is reset.</summary>
		/// <param name="milliseconds">Timeout in millisecond, or 0 for infinite timeout</param>
		public static TTransaction WithTimeout<TTransaction>(this TTransaction trans, int milliseconds)
			where TTransaction : IFdbReadOnlyTransaction
		{
			trans.Timeout = milliseconds;
			return trans;
		}

		/// <summary>Set a maximum number of retries after which additional calls to onError will throw the most recently seen error code. Valid parameter values are ``[-1, INT_MAX]``. If set to -1, will disable the retry limit.</summary>
		public static TTransaction WithRetryLimit<TTransaction>(this TTransaction trans, int retries)
			where TTransaction : IFdbReadOnlyTransaction
		{
			trans.RetryLimit = retries;
			return trans;
		}

		#endregion

		#region Set...

		public static void Set(this IFdbTransaction trans, Slice keyBytes, Stream data)
		{
			if (trans == null) throw new ArgumentNullException("trans");
			if (data == null) throw new ArgumentNullException("data");

			trans.EnsureCanWrite();

			Slice value = Slice.FromStream(data);

			trans.Set(keyBytes, value);
		}

		public static async Task SetAsync(this IFdbTransaction trans, Slice keyBytes, Stream data)
		{
			trans.EnsureCanWrite();

			Slice value = await Slice.FromStreamAsync(data, trans.Token).ConfigureAwait(false);

			trans.Set(keyBytes, value);
		}

		#endregion

		#region Atomic Ops...

		public static void AtomicAdd(this IFdbTransaction trans, Slice keyBytes, Slice valueBytes)
		{
			if (trans == null) throw new ArgumentNullException("trans");

			trans.Atomic(keyBytes, valueBytes, FdbMutationType.Add);
		}

		public static void AtomicAnd(this IFdbTransaction trans, Slice keyBytes, Slice maskBytes)
		{
			if (trans == null) throw new ArgumentNullException("trans");

			trans.Atomic(keyBytes, maskBytes, FdbMutationType.And);
		}

		public static void AtomicOr(this IFdbTransaction trans, Slice keyBytes, Slice maskBytes)
		{
			if (trans == null) throw new ArgumentNullException("trans");

			trans.Atomic(keyBytes, maskBytes, FdbMutationType.Or);
		}

		public static void AtomicXor(this IFdbTransaction trans, Slice keyBytes, Slice maskBytes)
		{
			if (trans == null) throw new ArgumentNullException("trans");

			trans.Atomic(keyBytes, maskBytes, FdbMutationType.Xor);
		}

		#endregion

		#region GetRange...

		public static FdbRangeQuery<KeyValuePair<Slice, Slice>> GetRange(this IFdbReadOnlyTransaction trans, FdbKeySelector beginInclusive, FdbKeySelector endExclusive, FdbRangeOptions options = null)
		{
			if (trans == null) throw new ArgumentNullException("trans");

			return trans.GetRange(FdbKeySelectorPair.Create(beginInclusive, endExclusive), options);
		}

		public static FdbRangeQuery<KeyValuePair<Slice, Slice>> GetRange(this IFdbReadOnlyTransaction trans, FdbKeySelector beginInclusive, FdbKeySelector endExclusive, int limit, bool reverse = false)
		{
			return GetRange(trans, beginInclusive, endExclusive, new FdbRangeOptions(limit: limit, reverse: reverse));
		}

		public static FdbRangeQuery<KeyValuePair<Slice, Slice>> GetRange(this IFdbReadOnlyTransaction trans, FdbKeyRange range, FdbRangeOptions options = null)
		{
			if (trans == null) throw new ArgumentNullException("trans");

			return trans.GetRange(FdbKeySelectorPair.Create(range), options);
		}

		public static FdbRangeQuery<KeyValuePair<Slice, Slice>> GetRange(this IFdbReadOnlyTransaction trans, FdbKeyRange range, int limit, bool reverse = false)
		{
			return GetRange(trans, range, new FdbRangeOptions(limit: limit, reverse: reverse));
		}

		public static FdbRangeQuery<KeyValuePair<Slice, Slice>> GetRange(this IFdbReadOnlyTransaction trans, Slice beginInclusive, Slice endExclusive, FdbRangeOptions options = null)
		{
			if (beginInclusive.IsNullOrEmpty) beginInclusive = FdbKey.MinValue;
			if (endExclusive.IsNullOrEmpty) endExclusive = FdbKey.MaxValue;

			return trans.GetRange(
				FdbKeySelectorPair.Create(
					FdbKeySelector.FirstGreaterOrEqual(beginInclusive),
					FdbKeySelector.FirstGreaterOrEqual(endExclusive)
				),
				options
			);
		}

		public static FdbRangeQuery<KeyValuePair<Slice, Slice>> GetRange(this IFdbReadOnlyTransaction trans, Slice beginInclusive, Slice endExclusive, int limit, bool reverse = false)
		{
			return GetRange(trans, beginInclusive, endExclusive, new FdbRangeOptions(limit: limit, reverse: reverse));
		}

		#endregion

		#region Clear...

		/// <summary>
		/// Modify the database snapshot represented by this transaction to remove all keys (if any) which are lexicographically greater than or equal to the given begin key and lexicographically less than the given end_key.
		/// Sets and clears affect the actual database only if transaction is later committed with CommitAsync().
		/// </summary>
		/// <param name="range">Pair of keys defining the range to clear.</param>
		public static void ClearRange(this IFdbTransaction trans, FdbKeyRange range)
		{
			if (trans == null) throw new ArgumentNullException("trans");

			trans.ClearRange(range.Begin, range.End);
		}

		#endregion

		#region Conflict Ranges...

		/// <summary>
		/// Adds a range of keys to the transaction’s read conflict ranges as if you had read the range. As a result, other transactions that write a key in this range could cause the transaction to fail with a conflict.
		/// </summary>
		public static void AddReadConflictRange(this IFdbTransaction transaction, FdbKeyRange range)
		{
			if (transaction == null) throw new ArgumentNullException("transaction");

			transaction.AddConflictRange(range, FdbConflictRangeType.Read);
		}

		/// <summary>
		/// Adds a range of keys to the transaction’s read conflict ranges as if you had read the range. As a result, other transactions that write a key in this range could cause the transaction to fail with a conflict.
		/// </summary>
		public static void AddReadConflictRange(this IFdbTransaction transaction, Slice beginInclusive, Slice endExclusice)
		{
			if (transaction == null) throw new ArgumentNullException("transaction");

			transaction.AddConflictRange(new FdbKeyRange(beginInclusive, endExclusice), FdbConflictRangeType.Read);
		}

		/// <summary>
		/// Adds a key to the transaction’s read conflict ranges as if you had read the key. As a result, other transactions that write to this key could cause the transaction to fail with a conflict.
		/// </summary>
		public static void AddReadConflictKey(this IFdbTransaction transaction, Slice key)
		{
			if (transaction == null) throw new ArgumentNullException("transaction");

			var range = FdbKeyRange.FromKey(key);

			transaction.AddConflictRange(range, FdbConflictRangeType.Read);
		}

		/// <summary>
		/// Adds a range of keys to the transaction’s write conflict ranges as if you had cleared the range. As a result, other transactions that concurrently read a key in this range could fail with a conflict.
		/// </summary>
		public static void AddWriteConflictRange(this IFdbTransaction transaction, FdbKeyRange range)
		{
			if (transaction == null) throw new ArgumentNullException("transaction");

			transaction.AddConflictRange(range, FdbConflictRangeType.Write);
		}

		/// <summary>
		/// Adds a range of keys to the transaction’s write conflict ranges as if you had cleared the range. As a result, other transactions that concurrently read a key in this range could fail with a conflict.
		/// </summary>
		public static void AddWriteConflictRange(this IFdbTransaction transaction, Slice beginInclusive, Slice endExclusice)
		{
			if (transaction == null) throw new ArgumentNullException("transaction");

			transaction.AddConflictRange(new FdbKeyRange(beginInclusive, endExclusice), FdbConflictRangeType.Write);
		}

		/// <summary>
		/// Adds a key to the transaction’s write conflict ranges as if you had cleared the key. As a result, other transactions that concurrently read this key could fail with a conflict.
		/// </summary>
		public static void AddWriteConflictKey(this IFdbTransaction transaction, Slice key)
		{
			if (transaction == null) throw new ArgumentNullException("transaction");

			transaction.AddConflictRange(FdbKeyRange.FromKey(key), FdbConflictRangeType.Write);
		}

		#endregion

		#region Batching...

		/// <summary>
		/// Reads several values from the database snapshot represented by the current transaction
		/// </summary>
		/// <param name="keys">Sequence of keys to be looked up in the database</param>
		/// <returns>Task that will return an array of values, or an exception. Each item in the array will contain the value of the key at the same index in <paramref name="keys"/>, or Slice.Nil if that key does not exist.</returns>
		public static Task<Slice[]> GetValuesAsync(this IFdbReadOnlyTransaction trans, IEnumerable<Slice> keys)
		{
			if (keys == null) throw new ArgumentNullException("keys");

			var array = keys as Slice[];
			if (array == null) array = keys.ToArray();

			return trans.GetValuesAsync(array);
		}

		/// <summary>
		/// Resolves several key selectors against the keys in the database snapshot represented by the current transaction.
		/// </summary>
		/// <param name="selectors">Sequence of key selectors to resolve</param>
		/// <returns>Task that will return an array of keys matching the selectors, or an exception</returns>
		public static Task<Slice[]> GetKeysAsync(this IFdbReadOnlyTransaction trans, IEnumerable<FdbKeySelector> selectors)
		{
			if (selectors == null) throw new ArgumentNullException("selectors");

			var array = selectors as FdbKeySelector[];
			if (array == null) array = selectors.ToArray();

			return trans.GetKeysAsync(array);
		}

		public static Task<List<KeyValuePair<Slice, Slice>>> GetBatchAsync(this IFdbReadOnlyTransaction trans, IEnumerable<Slice> keys)
		{
			if (keys == null) throw new ArgumentNullException("keys");

			var array = keys as Slice[];
			if (array == null) array = keys.ToArray();

			return trans.GetBatchAsync(array);
		}

		public static async Task<List<KeyValuePair<Slice, Slice>>> GetBatchAsync(this IFdbReadOnlyTransaction trans, Slice[] keys)
		{
			if (keys == null) throw new ArgumentNullException("keys");

			var results = await trans.GetValuesAsync(keys).ConfigureAwait(false);

			return results
				.Select((value, i) => new KeyValuePair<Slice, Slice>(keys[i], value))
				.ToList();
		}

		#endregion


	}
}
