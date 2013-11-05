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
	using FoundationDB.Client.Serializers;
	using FoundationDB.Client.Utils;
	using System;
	using System.Collections.Generic;
	using System.IO;
	using System.Linq;
	using System.Threading;
	using System.Threading.Tasks;

	/// <summary>Provides a set of extensions methods shared by all FoundationDB transaction implementations.</summary>
	public static class FdbTransactionExtensions
	{

		#region Fluent Options...

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

		#region Get...

		public static Task<Slice> GetAsync<TKey>(this IFdbReadOnlyTransaction trans, TKey key)
			where TKey : IFdbKey
		{
			if (trans == null) throw new ArgumentNullException("trans");
			if (key == null) throw new ArgumentNullException("key");
			return trans.GetAsync(key.ToFoundationDbKey());
		}

		public static async Task<TValue> GetAsync<TValue>(this IFdbReadOnlyTransaction trans, Slice key, ISliceSerializer<TValue> serializer = null)
		{
			if (trans == null) throw new ArgumentNullException("trans");

			var value = await trans.GetAsync(key).ConfigureAwait(false);

			return FdbSliceSerializer.FromSlice(value, serializer);
		}

		public static Task<TValue> GetAsync<TKey, TValue>(this IFdbReadOnlyTransaction trans, TKey key, ISliceSerializer<TValue> serializer = null)
			where TKey : IFdbKey
		{
			if (key == null) throw new ArgumentNullException("key");
			return GetAsync<TValue>(trans, key.ToFoundationDbKey(), serializer);
		}

		#endregion

		#region Set...

		public static void Set<TKey>(this IFdbTransaction trans, TKey key, Slice value)
			where TKey : IFdbKey
		{
			if (trans == null) throw new ArgumentNullException("trans");
			if (key == null) throw new ArgumentNullException("key");

			trans.Set(key.ToFoundationDbKey(), value);
		}

		public static void Set<TValue>(this IFdbTransaction trans, Slice key, TValue value, ISliceSerializer<TValue> serializer)
		{
			if (trans == null) throw new ArgumentNullException("trans");
			if (value == null) throw new ArgumentNullException("value");

			trans.Set(key, FdbSliceSerializer.ToSlice(value, serializer));
		}

		public static void Set<TKey, TValue>(this IFdbTransaction trans, TKey key, TValue value, ISliceSerializer<TValue> serializer)
			where TKey : IFdbKey
		{
			if (key == null) throw new ArgumentNullException("key");
			Set<TValue>(trans, key.ToFoundationDbKey(), value, serializer);
		}

		public static void Set(this IFdbTransaction trans, Slice key, Stream data)
		{
			if (trans == null) throw new ArgumentNullException("trans");
			if (data == null) throw new ArgumentNullException("data");

			trans.EnsureCanWrite();

			Slice value = Slice.FromStream(data);

			trans.Set(key, value);
		}

		public static async Task SetAsync(this IFdbTransaction trans, Slice key, Stream data)
		{
			trans.EnsureCanWrite();

			Slice value = await Slice.FromStreamAsync(data, trans.Token).ConfigureAwait(false);

			trans.Set(key, value);
		}

		#endregion

		#region Atomic Ops...

		public static void AtomicAdd(this IFdbTransaction trans, Slice key, Slice value)
		{
			if (trans == null) throw new ArgumentNullException("trans");

			trans.Atomic(key, value, FdbMutationType.Add);
		}

		public static void AtomicAdd<TKey>(this IFdbTransaction trans, TKey key, Slice value)
			where TKey : IFdbKey
		{
			if (trans == null) throw new ArgumentNullException("trans");
			if (key == null) throw new ArgumentNullException("key");

			trans.Atomic(key.ToFoundationDbKey(), value, FdbMutationType.Add);
		}

		public static void AtomicAnd(this IFdbTransaction trans, Slice key, Slice mask)
		{
			if (trans == null) throw new ArgumentNullException("trans");

			trans.Atomic(key, mask, FdbMutationType.And);
		}

		public static void AtomicAnd<TKey>(this IFdbTransaction trans, TKey key, Slice mask)
			where TKey : IFdbKey
		{
			if (trans == null) throw new ArgumentNullException("trans");
			if (key == null) throw new ArgumentNullException("key");

			trans.Atomic(key.ToFoundationDbKey(), mask, FdbMutationType.And);
		}

		public static void AtomicOr(this IFdbTransaction trans, Slice key, Slice mask)
		{
			if (trans == null) throw new ArgumentNullException("trans");

			trans.Atomic(key, mask, FdbMutationType.Or);
		}

		public static void AtomicOr<TKey>(this IFdbTransaction trans, TKey key, Slice mask)
			where TKey : IFdbKey
		{
			if (trans == null) throw new ArgumentNullException("trans");
			if (key == null) throw new ArgumentNullException("key");

			trans.Atomic(key.ToFoundationDbKey(), mask, FdbMutationType.Or);
		}

		public static void AtomicXor(this IFdbTransaction trans, Slice key, Slice mask)
		{
			if (trans == null) throw new ArgumentNullException("trans");

			trans.Atomic(key, mask, FdbMutationType.Xor);
		}

		public static void AtomicXor<TKey>(this IFdbTransaction trans, TKey key, Slice mask)
			where TKey : IFdbKey
		{
			if (trans == null) throw new ArgumentNullException("trans");
			if (key == null) throw new ArgumentNullException("key");

			trans.Atomic(key.ToFoundationDbKey(), mask, FdbMutationType.Xor);
		}

		#endregion

		#region GetRange...

		public static FdbRangeQuery<KeyValuePair<Slice, Slice>> GetRange(this IFdbReadOnlyTransaction trans, FdbKeySelector beginInclusive, FdbKeySelector endExclusive, int limit, bool reverse = false)
		{
			if (trans == null) throw new ArgumentNullException("trans");

			return trans.GetRange(beginInclusive, endExclusive, new FdbRangeOptions(limit: limit, reverse: reverse));
		}

		public static FdbRangeQuery<KeyValuePair<Slice, Slice>> GetRange(this IFdbReadOnlyTransaction trans, FdbKeyRange range, FdbRangeOptions options = null)
		{
			return FdbTransactionExtensions.GetRange(trans, FdbKeySelectorPair.Create(range), options);
		}

		public static FdbRangeQuery<KeyValuePair<Slice, Slice>> GetRange(this IFdbReadOnlyTransaction trans, FdbKeyRange range, int limit, bool reverse = false)
		{
			return FdbTransactionExtensions.GetRange(trans, range, new FdbRangeOptions(limit: limit, reverse: reverse));
		}

		public static FdbRangeQuery<KeyValuePair<Slice, Slice>> GetRange(this IFdbReadOnlyTransaction trans, Slice beginKeyInclusive, Slice endKeyExclusive, FdbRangeOptions options = null)
		{
			if (trans == null) throw new ArgumentNullException("trans");

			if (beginKeyInclusive.IsNullOrEmpty) beginKeyInclusive = FdbKey.MinValue;
			if (endKeyExclusive.IsNullOrEmpty) endKeyExclusive = FdbKey.MaxValue;

			return trans.GetRange(
				FdbKeySelector.FirstGreaterOrEqual(beginKeyInclusive),
				FdbKeySelector.FirstGreaterOrEqual(endKeyExclusive),
				options
			);
		}

		public static FdbRangeQuery<KeyValuePair<Slice, Slice>> GetRange<TKey>(this IFdbReadOnlyTransaction trans, TKey beginKeyInclusive, TKey endKeyExclusive, FdbRangeOptions options = null)
			where TKey : IFdbKey
		{
			//TODO: TKey in, but Slice out ? Maybe we need to get a ISliceSerializer<TKey> to convert the slices back to a TKey ?
			if (beginKeyInclusive == null) throw new ArgumentNullException("beginKeyInclusive");
			if (endKeyExclusive == null) throw new ArgumentNullException("endKeyExclusive");
			return GetRange(trans, beginKeyInclusive.ToFoundationDbKey(), endKeyExclusive.ToFoundationDbKey(), options);
		}

		public static FdbRangeQuery<KeyValuePair<Slice, Slice>> GetRange(this IFdbReadOnlyTransaction trans, Slice beginKeyInclusive, Slice endKeyExclusive, int limit, bool reverse = false)
		{
			return GetRange(trans, beginKeyInclusive, endKeyExclusive, new FdbRangeOptions(limit: limit, reverse: reverse));
		}

		public static FdbRangeQuery<KeyValuePair<Slice, Slice>> GetRange<TKey>(this IFdbReadOnlyTransaction trans, TKey beginKeyInclusive, TKey endKeyExclusive, int limit, bool reverse = false)
			where TKey : IFdbKey
		{
			if (beginKeyInclusive == null) throw new ArgumentNullException("beginKeyInclusive");
			if (endKeyExclusive == null) throw new ArgumentNullException("endKeyExclusive");

			return GetRange(trans, beginKeyInclusive.ToFoundationDbKey(), endKeyExclusive.ToFoundationDbKey(), new FdbRangeOptions(limit: limit, reverse: reverse));
		}

		/// <summary>
		/// Create a new range query that will read all key-value pairs in the database snapshot represented by the transaction
		/// </summary>
		/// <param name="range">Pair of key selectors defining the beginning and the end of the range</param>
		/// <param name="options">Optionnal query options (Limit, TargetBytes, Mode, Reverse, ...)</param>
		/// <returns>Range query that, once executed, will return all the key-value pairs matching the providing selector pair</returns>
		public static FdbRangeQuery<KeyValuePair<Slice, Slice>> GetRange(this IFdbReadOnlyTransaction trans, FdbKeySelectorPair range, FdbRangeOptions options = null)
		{
			if (trans == null) throw new ArgumentNullException("trans");

			return trans.GetRange(range.Begin, range.End, options);
		}

		/// <summary>
		/// Reads all key-value pairs in the database snapshot represented by transaction (potentially limited by Limit, TargetBytes, or Mode)
		/// which have a key lexicographically greater than or equal to the key resolved by the begin key selector
		/// and lexicographically less than the key resolved by the end key selector.
		/// </summary>
		/// <param name="range">key selector pair defining the beginning and the end of the range</param>
		/// <param name="options">Optionnal query options (Limit, TargetBytes, Mode, Reverse, ...)</param>
		/// <param name="iteration">If streaming mode is FdbStreamingMode.Iterator, this parameter should start at 1 and be incremented by 1 for each successive call while reading this range. In all other cases it is ignored.</param>
		/// <returns></returns>
		public static Task<FdbRangeChunk> GetRangeAsync(this IFdbReadOnlyTransaction trans, FdbKeySelectorPair range, FdbRangeOptions options = null, int iteration = 0)
		{
			if (trans == null) throw new ArgumentNullException("trans");

			return trans.GetRangeAsync(range.Begin, range.End, options, iteration);
		}

		#endregion

		#region Clear...

		/// <summary>
		/// Modify the database snapshot represented by this transaction to remove the given key from the database. If the key was not previously present in the database, there is no effect.
		/// </summary>
		/// <param name="key">Key to be removed from the database.</param>
		public static void Clear<TKey>(this IFdbTransaction trans, TKey key)
			where TKey : IFdbKey
		{
			if (trans == null) throw new ArgumentNullException("trans");
			if (key == null) throw new ArgumentNullException("key");

			trans.Clear(key.ToFoundationDbKey());
		}

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
		/// Adds a conflict range to a transaction without performing the associated read or write.
		/// </summary>
		/// <param name="range">Range of the keys specifying the conflict range. The end key is excluded</param>
		/// <param name="type">One of the FDBConflictRangeType values indicating what type of conflict range is being set.</param>
		public static void AddConflictRange(this IFdbTransaction trans, FdbKeyRange range, FdbConflictRangeType type)
		{
			if (trans == null) throw new ArgumentNullException("trans");

			trans.AddConflictRange(range.Begin, range.End, type);
		}


		/// <summary>
		/// Adds a range of keys to the transaction’s read conflict ranges as if you had read the range. As a result, other transactions that write a key in this range could cause the transaction to fail with a conflict.
		/// </summary>
		public static void AddReadConflictRange(this IFdbTransaction trans, FdbKeyRange range)
		{
			AddConflictRange(trans, range, FdbConflictRangeType.Read);
		}

		/// <summary>
		/// Adds a range of keys to the transaction’s read conflict ranges as if you had read the range. As a result, other transactions that write a key in this range could cause the transaction to fail with a conflict.
		/// </summary>
		public static void AddReadConflictRange(this IFdbTransaction trans, Slice beginKeyInclusive, Slice endKeyExclusive)
		{
			if (trans == null) throw new ArgumentNullException("trans");

			trans.AddConflictRange(beginKeyInclusive, endKeyExclusive, FdbConflictRangeType.Read);
		}

		/// <summary>
		/// Adds a range of keys to the transaction’s read conflict ranges as if you had read the range. As a result, other transactions that write a key in this range could cause the transaction to fail with a conflict.
		/// </summary>
		public static void AddReadConflictRange<TKey>(this IFdbTransaction trans, TKey beginKeyInclusive, TKey endKeyExclusive)
			where TKey : IFdbKey
		{
			if (trans == null) throw new ArgumentNullException("trans");
			if (beginKeyInclusive == null) throw new ArgumentNullException("beginKeyInclusive");
			if (endKeyExclusive == null) throw new ArgumentNullException("endKeyExclusive");

			trans.AddConflictRange(beginKeyInclusive.ToFoundationDbKey(), endKeyExclusive.ToFoundationDbKey(), FdbConflictRangeType.Read);
		}

		/// <summary>
		/// Adds a key to the transaction’s read conflict ranges as if you had read the key. As a result, other transactions that write to this key could cause the transaction to fail with a conflict.
		/// </summary>
		public static void AddReadConflictKey(this IFdbTransaction trans, Slice key)
		{
			AddConflictRange(trans, FdbKeyRange.FromKey(key), FdbConflictRangeType.Read);
		}

		/// <summary>
		/// Adds a key to the transaction’s read conflict ranges as if you had read the key. As a result, other transactions that write to this key could cause the transaction to fail with a conflict.
		/// </summary>
		public static void AddReadConflictKey<TKey>(this IFdbTransaction trans, TKey key)
			where TKey : IFdbKey
		{
			if (key == null) throw new ArgumentNullException("key");
			AddConflictRange(trans, FdbKeyRange.FromKey(key.ToFoundationDbKey()), FdbConflictRangeType.Read);
		}

		/// <summary>
		/// Adds a range of keys to the transaction’s write conflict ranges as if you had cleared the range. As a result, other transactions that concurrently read a key in this range could fail with a conflict.
		/// </summary>
		public static void AddWriteConflictRange(this IFdbTransaction trans, FdbKeyRange range)
		{
			AddConflictRange(trans, range, FdbConflictRangeType.Write);
		}

		/// <summary>
		/// Adds a range of keys to the transaction’s write conflict ranges as if you had cleared the range. As a result, other transactions that concurrently read a key in this range could fail with a conflict.
		/// </summary>
		public static void AddWriteConflictRange(this IFdbTransaction trans, Slice beginKeyInclusive, Slice endKeyExclusive)
		{
			if (trans == null) throw new ArgumentNullException("trans");

			trans.AddConflictRange(beginKeyInclusive, endKeyExclusive, FdbConflictRangeType.Write);
		}

		/// <summary>
		/// Adds a range of keys to the transaction’s write conflict ranges as if you had cleared the range. As a result, other transactions that concurrently read a key in this range could fail with a conflict.
		/// </summary>
		public static void AddWriteConflictRange<TKey>(this IFdbTransaction trans, TKey beginKeyInclusive, TKey endKeyExclusive)
			where TKey : IFdbKey
		{
			if (trans == null) throw new ArgumentNullException("trans");
			if (beginKeyInclusive == null) throw new ArgumentNullException("beginKeyInclusive");
			if (endKeyExclusive == null) throw new ArgumentNullException("endKeyExclusive");

			trans.AddConflictRange(beginKeyInclusive.ToFoundationDbKey(), endKeyExclusive.ToFoundationDbKey(), FdbConflictRangeType.Write);
		}

		/// <summary>
		/// Adds a key to the transaction’s write conflict ranges as if you had cleared the key. As a result, other transactions that concurrently read this key could fail with a conflict.
		/// </summary>
		public static void AddWriteConflictKey(this IFdbTransaction trans, Slice key)
		{
			AddConflictRange(trans, FdbKeyRange.FromKey(key), FdbConflictRangeType.Write);
		}

		/// <summary>
		/// Adds a key to the transaction’s write conflict ranges as if you had cleared the key. As a result, other transactions that concurrently read this key could fail with a conflict.
		/// </summary>
		public static void AddWriteConflictKey<TKey>(this IFdbTransaction trans, TKey key)
			where TKey : IFdbKey
		{
			if (key == null) throw new ArgumentNullException("key");
			AddConflictRange(trans, FdbKeyRange.FromKey(key.ToFoundationDbKey()), FdbConflictRangeType.Write);
		}

		#endregion

		#region Watches...

		/// <summary>Reads the value associated with <paramref name="key"/>, and returns a Watch that will complete after a subsequent change to key in the database.</summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="key">Key to be looked up in the database</param>
		/// <param name="cancellationToken">Token that can be used to cancel the Watch from the outside.</param>
		/// <returns>A new Watch that will track any changes to <paramref name="key"/> in the database, and whose <see cref="FdbWatch.Value">Value</see> property contains the current value of the key.</returns>
		public static async Task<FdbWatch> GetAndWatchAsync(this IFdbTransaction trans, Slice key, CancellationToken cancellationToken)
		{
			if (trans == null) throw new ArgumentNullException("trans");
			cancellationToken.ThrowIfCancellationRequested();

			var value = await trans.GetAsync(key);
			var watch = trans.Watch(key, cancellationToken);
			watch.Value = value;

			return watch;
		}

		public static Task<FdbWatch> GetAndWatchAsync<TKey>(this IFdbTransaction trans, TKey key, CancellationToken cancellationToken)
			where TKey : IFdbKey
		{
			if (key == null) throw new ArgumentNullException("key");
			return GetAndWatchAsync(trans, key.ToFoundationDbKey(), cancellationToken);
		}

		/// <summary>Sets <paramref name="key"/> to <paramref name="value"/> and returns a Watch that will complete after a subsequent change to the key in the database.</summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="key">Name of the key to be inserted into the database.</param>
		/// <param name="value">Value to be inserted into the database.</param>
		/// <param name="cancellationToken">Token that can be used to cancel the Watch from the outside.</param>
		/// <returns>A new Watch that will track any changes to <paramref name="key"/> in the database, and whose <see cref="FdbWatch.Value">Value</see> property will be a copy of <paramref name="value"/> argument</returns>
		public static FdbWatch SetAndWatch(this IFdbTransaction trans, Slice key, Slice value, CancellationToken cancellationToken)
		{
			if (trans == null) throw new ArgumentNullException("trans");
			cancellationToken.ThrowIfCancellationRequested();

			trans.Set(key, value);
			var watch = trans.Watch(key, cancellationToken);
			watch.Value = value;

			return watch;
		}

		public static FdbWatch SetAndWatch<TKey>(this IFdbTransaction trans, TKey key, Slice value, CancellationToken cancellationToken)
			where TKey : IFdbKey
		{
			if (key == null) throw new ArgumentNullException("key");
			return SetAndWatch(trans, key.ToFoundationDbKey(), value, cancellationToken);
		}

		/// <summary>Sets <paramref name="key"/> to <paramref name="value"/> and returns a Watch that will complete after a subsequent change to the key in the database.</summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="key">Name of the key to be inserted into the database.</param>
		/// <param name="value">Value to be inserted into the database.</param>
		/// <param name="cancellationToken">Token that can be used to cancel the Watch from the outside.</param>
		/// <returns>A new Watch that will track any changes to <paramref name="key"/> in the database, and whose <see cref="FdbWatch.Value">Value</see> property will be a copy of <paramref name="value"/> argument</returns>
		public static FdbWatch SetAndWatch<TValue>(this IFdbTransaction trans, Slice key, TValue value, ISliceSerializer<TValue> serializer, CancellationToken cancellationToken)
		{
			return SetAndWatch(trans, key, FdbSliceSerializer.ToSlice(value, serializer), cancellationToken);
		}

		public static FdbWatch SetAndWatch<TKey, TValue>(this IFdbTransaction trans, TKey key, TValue value, ISliceSerializer<TValue> serializer, CancellationToken cancellationToken)
			where TKey : IFdbKey
		{
			if (key == null) throw new ArgumentNullException("key");
			return SetAndWatch(trans, key.ToFoundationDbKey(), FdbSliceSerializer.ToSlice(value, serializer), cancellationToken);
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
		/// Reads several values from the database snapshot represented by the current transaction
		/// </summary>
		/// <param name="keys">Sequence of keys to be looked up in the database</param>
		/// <returns>Task that will return an array of values, or an exception. Each item in the array will contain the value of the key at the same index in <paramref name="keys"/>, or Slice.Nil if that key does not exist.</returns>
		public static async Task<TValue[]> GetValuesAsync<TValue>(this IFdbReadOnlyTransaction trans, IEnumerable<Slice> keys, ISliceSerializer<TValue> serializer)
		{
			var results = await GetValuesAsync(trans, keys).ConfigureAwait(false);

			return FdbSliceSerializer.FromSlices(results, serializer);
		}

		public static Task<Slice[]> GetValuesAsync<TKey>(this IFdbReadOnlyTransaction trans, IEnumerable<TKey> keys)
			where TKey : IFdbKey
		{
			return GetValuesAsync(trans, keys.Select(key => key.ToFoundationDbKey()));
		}

		public static Task<TValue[]> GetValuesAsync<TKey, TValue>(this IFdbReadOnlyTransaction trans, IEnumerable<TKey> keys, ISliceSerializer<TValue> serializer)
			where TKey : IFdbKey
		{
			return GetValuesAsync<TValue>(trans, keys.Select(key => key.ToFoundationDbKey()), serializer);
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

		public static Task<KeyValuePair<Slice, Slice>[]> GetBatchAsync(this IFdbReadOnlyTransaction trans, IEnumerable<Slice> keys)
		{
			if (keys == null) throw new ArgumentNullException("keys");

			var array = keys as Slice[];
			if (array == null) array = keys.ToArray();

			return trans.GetBatchAsync(array);
		}

		public static async Task<KeyValuePair<Slice, Slice>[]> GetBatchAsync(this IFdbReadOnlyTransaction trans, Slice[] keys)
		{
			if (keys == null) throw new ArgumentNullException("keys");

			var results = await trans.GetValuesAsync(keys).ConfigureAwait(false);
			Contract.Assert(results != null && results.Length == keys.Length);

			var array = new KeyValuePair<Slice, Slice>[results.Length];
			for (int i = 0; i < array.Length;i++)
			{
				array[i] = new KeyValuePair<Slice, Slice>(keys[i], results[i]);
			}
			return array;
		}

		public static Task<KeyValuePair<Slice, TValue>[]> GetBatchAsync<TValue>(this IFdbReadOnlyTransaction trans, IEnumerable<Slice> keys, ISliceSerializer<TValue> serializer)
		{
			if (keys == null) throw new ArgumentNullException("keys");

			var array = keys as Slice[];
			if (array == null) array = keys.ToArray();

			return trans.GetBatchAsync(array, serializer);
		}

		public static async Task<KeyValuePair<Slice, TValue>[]> GetBatchAsync<TValue>(this IFdbReadOnlyTransaction trans, Slice[] keys, ISliceSerializer<TValue> serializer)
		{
			if (keys == null) throw new ArgumentNullException("keys");

			var results = await trans.GetValuesAsync(keys).ConfigureAwait(false);
			Contract.Assert(results != null && results.Length == keys.Length);

			var array = new KeyValuePair<Slice, TValue>[results.Length];
			if (serializer == null) serializer = FdbSliceSerializer<TValue>.Default;
			for (int i = 0; i < array.Length; i++)
			{
				array[i] = new KeyValuePair<Slice, TValue>(keys[i], serializer.FromSlice(results[i]));
			}
			return array;
		}

		public static Task<KeyValuePair<Slice, Slice>[]> GetBatchAsync<TKey>(this IFdbReadOnlyTransaction trans, IEnumerable<TKey> keys)
			where TKey : IFdbKey
		{
			if (keys == null) throw new ArgumentNullException("keys");
			return GetBatchAsync(trans, keys.Select(key => key.ToFoundationDbKey()).ToArray());
		}

		public static Task<KeyValuePair<Slice, TValue>[]> GetBatchAsync<TKey, TValue>(this IFdbReadOnlyTransaction trans, IEnumerable<TKey> keys, ISliceSerializer<TValue> serializer)
			where TKey : IFdbKey
		{
			if (keys == null) throw new ArgumentNullException("keys");
			return GetBatchAsync<TValue>(trans, keys.Select(key => key.ToFoundationDbKey()).ToArray(), serializer);
		}

		#endregion

	}
}
