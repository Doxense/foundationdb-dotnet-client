#region BSD Licence
/* Copyright (c) 2013-2015, Doxense SAS
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
	using Doxense.Diagnostics.Contracts;
	using FoundationDB.Linq;
	using JetBrains.Annotations;

	/// <summary>Provides a set of extensions methods shared by all FoundationDB transaction implementations.</summary>
	public static class FdbTransactionExtensions
	{

		#region Fluent Options...

		/// <summary>Allows this transaction to read system keys (those that start with the byte 0xFF)</summary>
		public static TTransaction WithReadAccessToSystemKeys<TTransaction>([NotNull] this TTransaction trans)
			where TTransaction : IFdbReadOnlyTransaction
		{
			trans.SetOption(Fdb.ApiVersion >= 300 ? FdbTransactionOption.ReadSystemKeys : FdbTransactionOption.AccessSystemKeys);
			//TODO: cache this into a local variable ?
			return trans;
		}

		/// <summary>Allows this transaction to read and modify system keys (those that start with the byte 0xFF)</summary>
		public static TTransaction WithWriteAccessToSystemKeys<TTransaction>([NotNull] this TTransaction trans)
			where TTransaction : IFdbTransaction
		{
			trans.SetOption(FdbTransactionOption.AccessSystemKeys);
			//TODO: cache this into a local variable ?
			return trans;
		}

		/// <summary>Specifies that this transaction should be treated as highest priority and that lower priority transactions should block behind this one. Use is discouraged outside of low-level tools</summary>
		public static TTransaction WithPrioritySystemImmediate<TTransaction>([NotNull] this TTransaction trans)
			where TTransaction : IFdbReadOnlyTransaction
		{
			trans.SetOption(FdbTransactionOption.PrioritySystemImmediate);
			//TODO: cache this into a local variable ?
			return trans;
		}

		/// <summary>Specifies that this transaction should be treated as low priority and that default priority transactions should be processed first. Useful for doing batch work simultaneously with latency-sensitive work</summary>
		public static TTransaction WithPriorityBatch<TTransaction>([NotNull] this TTransaction trans)
			where TTransaction : IFdbReadOnlyTransaction
		{
			trans.SetOption(FdbTransactionOption.PriorityBatch);
			//TODO: cache this into a local variable ?
			return trans;
		}

		/// <summary>Reads performed by a transaction will not see any prior mutations that occurred in that transaction, instead seeing the value which was in the database at the transaction's read version. This option may provide a small performance benefit for the client, but also disables a number of client-side optimizations which are beneficial for transactions which tend to read and write the same keys within a single transaction. Also note that with this option invoked any outstanding reads will return errors when transaction commit is called (rather than the normal behavior of commit waiting for outstanding reads to complete).</summary>
		public static TTransaction WithReadYourWritesDisable<TTransaction>([NotNull] this TTransaction trans)
			where TTransaction : IFdbTransaction
		{
			trans.SetOption(FdbTransactionOption.ReadYourWritesDisable);
			return trans;
		}

		/// <summary>Snapshot reads performed by a transaction will see the results of writes done in the same transaction.</summary>
		public static TTransaction WithSnapshotReadYourWritesEnable<TTransaction>([NotNull] this TTransaction trans)
			where TTransaction : IFdbReadOnlyTransaction
		{
			trans.SetOption(FdbTransactionOption.SnapshotReadYourWriteEnable);
			return trans;
		}

		/// <summary>Reads performed by a transaction will not see the results of writes done in the same transaction.</summary>
		public static TTransaction WithSnapshotReadYourWritesDisable<TTransaction>([NotNull] this TTransaction trans)
			where TTransaction : IFdbReadOnlyTransaction
		{
			trans.SetOption(FdbTransactionOption.SnapshotReadYourWriteDisable);
			return trans;
		}

		/// <summary>Disables read-ahead caching for range reads. Under normal operation, a transaction will read extra rows from the database into cache if range reads are used to page through a series of data one row at a time (i.e. if a range read with a one row limit is followed by another one row range read starting immediately after the result of the first).</summary>
		public static TTransaction WithReadAheadDisable<TTransaction>([NotNull] this TTransaction trans)
			where TTransaction : IFdbReadOnlyTransaction
		{
			trans.SetOption(FdbTransactionOption.ReadAheadDisable);
			return trans;
		}

		/// <summary>The next write performed on this transaction will not generate a write conflict range. As a result, other transactions which read the key(s) being modified by the next write will not conflict with this transaction. Care needs to be taken when using this option on a transaction that is shared between multiple threads. When setting this option, write conflict ranges will be disabled on the next write operation, regardless of what thread it is on.</summary>
		public static TTransaction WithNextWriteNoWriteConflictRange<TTransaction>([NotNull] this TTransaction trans)
			where TTransaction : IFdbTransaction
		{
			trans.SetOption(FdbTransactionOption.NextWriteNoWriteConflictRange);
			return trans;
		}

		/// <summary>Set a timeout in milliseconds which, when elapsed, will cause the transaction automatically to be cancelled.
		/// Valid parameter values are [TimeSpan.Zero, TimeSpan.MaxValue].
		/// If set to 0, will disable all timeouts.
		/// All pending and any future uses of the transaction will throw an exception.
		/// The transaction can be used again after it is reset.
		/// </summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="timeout">Timeout (with millisecond precision), or TimeSpan.Zero for infinite timeout</param>
		public static TTransaction WithTimeout<TTransaction>([NotNull] this TTransaction trans, TimeSpan timeout)
			where TTransaction : IFdbReadOnlyTransaction
		{
			return WithTimeout<TTransaction>(trans, timeout == TimeSpan.Zero ? 0 : (int)Math.Ceiling(timeout.TotalMilliseconds));
		}

		/// <summary>Set a timeout in milliseconds which, when elapsed, will cause the transaction automatically to be cancelled.
		/// Valid parameter values are [0, int.MaxValue].
		/// If set to 0, will disable all timeouts.
		/// All pending and any future uses of the transaction will throw an exception.
		/// The transaction can be used again after it is reset.
		/// </summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="milliseconds">Timeout in millisecond, or 0 for infinite timeout</param>
		public static TTransaction WithTimeout<TTransaction>([NotNull] this TTransaction trans, int milliseconds)
			where TTransaction : IFdbReadOnlyTransaction
		{
			trans.Timeout = milliseconds;
			return trans;
		}

		/// <summary>Set a maximum number of retries after which additional calls to onError will throw the most recently seen error code.
		/// Valid parameter values are [-1, int.MaxValue].
		/// If set to -1, will disable the retry limit.</summary>
		public static TTransaction WithRetryLimit<TTransaction>([NotNull] this TTransaction trans, int retries)
			where TTransaction : IFdbReadOnlyTransaction
		{
			trans.RetryLimit = retries;
			return trans;
		}

		/// <summary>Set the maximum amount of backoff delay incurred in the call to onError if the error is retryable.
		/// Defaults to 1000 ms. Valid parameter values are [0, int.MaxValue].
		/// If the maximum retry delay is less than the current retry delay of the transaction, then the current retry delay will be clamped to the maximum retry delay.
		/// </summary>
		public static TTransaction WithMaxRetryDelay<TTransaction>([NotNull] this TTransaction trans, int milliseconds)
			where TTransaction : IFdbReadOnlyTransaction
		{
			trans.MaxRetryDelay = milliseconds;
			return trans;
		}

		/// <summary>Set the maximum amount of backoff delay incurred in the call to onError if the error is retryable.
		/// Defaults to 1000 ms. Valid parameter values are [TimeSpan.Zero, TimeSpan.MaxValue].
		/// If the maximum retry delay is less than the current retry delay of the transaction, then the current retry delay will be clamped to the maximum retry delay.
		/// </summary>
		public static TTransaction WithMaxRetryDelay<TTransaction>([NotNull] this TTransaction trans, TimeSpan delay)
			where TTransaction : IFdbReadOnlyTransaction
		{
			return WithMaxRetryDelay<TTransaction>(trans, delay == TimeSpan.Zero ? 0 : (int)Math.Ceiling(delay.TotalMilliseconds));
		}

		#endregion

		#region Get...

		/// <summary>Reads a value from the database snapshot represented by by the current transaction.</summary>
		/// <typeparam name="TKey">Type of the key that implements IFdbKey.</typeparam>
		/// <param name="trans">Transaction instance</param>
		/// <param name="key">Key to be looked up in the database</param>
		/// <returns>Task that will return the value of the key if it is found, Slice.Nil if the key does not exist, or an exception</returns>
		/// <exception cref="System.ArgumentException">If the <paramref name="key"/> is null</exception>
		/// <exception cref="System.OperationCanceledException">If the cancellation token is already triggered</exception>
		/// <exception cref="System.ObjectDisposedException">If the transaction has already been completed</exception>
		/// <exception cref="System.InvalidOperationException">If the operation method is called from the Network Thread</exception>
		public static Task<Slice> GetAsync<TKey>(this IFdbReadOnlyTransaction trans, TKey key)
			where TKey : IFdbKey
		{
			if (trans == null) throw new ArgumentNullException("trans");
			if (key == null) throw new ArgumentNullException("key");
			return trans.GetAsync(key.ToFoundationDbKey());
		}

		/// <summary>Reads and decode a value from the database snapshot represented by by the current transaction.</summary>
		/// <typeparam name="TValue">Type of the value.</typeparam>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="key">Key to be looked up in the database</param>
		/// <param name="encoder">Encoder used to decode the value of the key.</param>
		/// <returns>Task that will return the value of the key if it is found, Slice.Nil if the key does not exist, or an exception</returns>
		/// <exception cref="System.ArgumentException">If the <paramref name="key"/> is null</exception>
		/// <exception cref="System.OperationCanceledException">If the cancellation token is already triggered</exception>
		/// <exception cref="System.ObjectDisposedException">If the transaction has already been completed</exception>
		/// <exception cref="System.InvalidOperationException">If the operation method is called from the Network Thread</exception>
		public static async Task<TValue> GetAsync<TValue>([NotNull] this IFdbReadOnlyTransaction trans, Slice key, [NotNull] IValueEncoder<TValue> encoder)
		{
			Contract.NotNull(trans, nameof(trans));
			Contract.NotNull(encoder, nameof(encoder));

			return encoder.DecodeValue(await trans.GetAsync(key).ConfigureAwait(false));
		}

		/// <summary>Reads and decode a value from the database snapshot represented by by the current transaction.</summary>
		/// <typeparam name="TKey">Type of the key that implements IFdbKey.</typeparam>
		/// <typeparam name="TValue">Type of the value.</typeparam>
		/// <param name="trans">Transaction instance</param>
		/// <param name="key">Key to be looked up in the database</param>
		/// <param name="encoder">Encoder used to decode the value of the key.</param>
		/// <returns>Task that will return the value of the key if it is found, Slice.Nil if the key does not exist, or an exception</returns>
		/// <exception cref="System.ArgumentException">If the <paramref name="key"/> is null</exception>
		/// <exception cref="System.OperationCanceledException">If the cancellation token is already triggered</exception>
		/// <exception cref="System.ObjectDisposedException">If the transaction has already been completed</exception>
		/// <exception cref="System.InvalidOperationException">If the operation method is called from the Network Thread</exception>
		public static Task<TValue> GetAsync<TKey, TValue>([NotNull] this IFdbReadOnlyTransaction trans, TKey key, [NotNull] IValueEncoder<TValue> encoder)
			where TKey : IFdbKey
		{
			if (key == null) throw new ArgumentNullException("key");
			return GetAsync<TValue>(trans, key.ToFoundationDbKey(), encoder);
		}

		#endregion

		#region Set...

		/// <summary>Set the value of a key in the database.</summary>
		/// <typeparam name="TKey">Type of the key that implements IFdbKey.</typeparam>
		/// <param name="trans">Transaction instance</param>
		/// <param name="key"></param>
		/// <param name="value"></param>
		public static void Set<TKey>([NotNull] this IFdbTransaction trans, TKey key, Slice value)
			where TKey : IFdbKey
		{
			if (trans == null) throw new ArgumentNullException("trans");
			if (key == null) throw new ArgumentNullException("key");

			trans.Set(key.ToFoundationDbKey(), value);
		}

		/// <summary>Set the value of a key in the database, using a custom value encoder.</summary>
		/// <typeparam name="TValue">Type of the value</typeparam>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="key">Key to set</param>
		/// <param name="value">Value of the key</param>
		/// <param name="encoder">Encoder used to convert <paramref name="value"/> into a binary slice.</param>
		public static void Set<TValue>([NotNull] this IFdbTransaction trans, Slice key, TValue value, [NotNull] IValueEncoder<TValue> encoder)
		{
			Contract.NotNull(trans, nameof(trans));
			Contract.NotNull(encoder, nameof(encoder));

			trans.Set(key, encoder.EncodeValue(value));
		}

		/// <summary>Set the value of a key in the database, using a custom value encoder.</summary>
		/// <typeparam name="TKey">Type of the key that implements IFdbKey.</typeparam>
		/// <typeparam name="TValue">Type of the value</typeparam>
		/// <param name="trans">Transaction instance</param>
		/// <param name="key">Key to set</param>
		/// <param name="value">Value of the key</param>
		/// <param name="encoder">Encoder used to convert <paramref name="value"/> into a binary slice.</param>
		public static void Set<TKey, TValue>([NotNull] this IFdbTransaction trans, TKey key, TValue value, [NotNull] IValueEncoder<TValue> encoder)
			where TKey : IFdbKey
		{
			if (key == null) throw new ArgumentNullException("key");
			Set<TValue>(trans, key.ToFoundationDbKey(), value, encoder);
		}

		/// <summary>Set the value of a key in the database, using the content of a Stream</summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="key">Key to set</param>
		/// <param name="data">Stream that holds the content of the key, whose length should not exceed the allowed maximum value size.</param>
		/// <remarks>This method works best with streams that do not block, like a <see cref="MemoryStream"/>. For streams that may block, consider using <see cref="SetAsync(IFdbTransaction, Slice, Stream)"/> instead.</remarks>
		public static void Set([NotNull] this IFdbTransaction trans, Slice key, [NotNull] Stream data)
		{
			Contract.NotNull(trans, nameof(trans));
			Contract.NotNull(data, nameof(data));

			trans.EnsureCanWrite();

			Slice value = Slice.FromStream(data);

			trans.Set(key, value);
		}

		/// <summary>Set the value of a key in the database, by reading the content of a Stream asynchronously</summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="key">Key to set</param>
		/// <param name="data">Stream that holds the content of the key, whose length should not exceed the allowed maximum value size.</param>
		/// <remarks>If reading from the stream takes more than 5 seconds, the transaction will not be able to commit. For streams that are stored in memory, like a MemoryStream, consider using <see cref="Set(IFdbTransaction, Slice, Stream)"/> instead.</remarks>
		public static async Task SetAsync([NotNull] this IFdbTransaction trans, Slice key, [NotNull] Stream data)
		{
			Contract.NotNull(trans, nameof(trans));
			Contract.NotNull(data, nameof(data));

			trans.EnsureCanWrite();

			Slice value = await Slice.FromStreamAsync(data, trans.Cancellation).ConfigureAwait(false);

			trans.Set(key, value);
		}

		#endregion

		#region SetValues

		/// <summary>Set the values of a list of keys in the database.</summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="keyValuePairs">Array of key and value pairs</param>
		/// <remarks>
		/// Only use this method if you know that the approximate size of count of keys and values will not exceed the maximum size allowed per transaction.
		/// If the list and size of the keys and values is not known in advance, consider using a bulk operation provided by the <see cref="Fdb.Bulk"/> helper class.
		/// </remarks>
		/// <exception cref="ArgumentNullException">If either <paramref name="trans"/> or <paramref name="keyValuePairs"/> is null.</exception>
		/// <exception cref="FdbException">If this operation would exceed the maximum allowed size for a transaction.</exception>
		public static void SetValues([NotNull] this IFdbTransaction trans, KeyValuePair<Slice, Slice>[] keyValuePairs)
		{
			Contract.NotNull(trans, nameof(trans));
			Contract.NotNull(keyValuePairs, nameof(keyValuePairs));

			foreach (var kv in keyValuePairs)
			{
				trans.Set(kv.Key, kv.Value);
			}
		}

		/// <summary>Set the values of a list of keys in the database.</summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="keys">Array of keys to set</param>
		/// <param name="values">Array of values for each key. Must be in the same order as <paramref name="keys"/> and have the same length.</param>
		/// <remarks>
		/// Only use this method if you know that the approximate size of count of keys and values will not exceed the maximum size allowed per transaction.
		/// If the list and size of the keys and values is not known in advance, consider using a bulk operation provided by the <see cref="Fdb.Bulk"/> helper class.
		/// </remarks>
		/// <exception cref="ArgumentNullException">If either <paramref name="trans"/>, <paramref name="keys"/> or <paramref name="values"/> is null.</exception>
		/// <exception cref="ArgumentException">If the <paramref name="values"/> does not have the same length as <paramref name="keys"/>.</exception>
		/// <exception cref="FdbException">If this operation would exceed the maximum allowed size for a transaction.</exception>
		public static void SetValues([NotNull] this IFdbTransaction trans, [NotNull] Slice[] keys, [NotNull] Slice[] values)
		{
			Contract.NotNull(trans, nameof(trans));
			Contract.NotNull(keys, nameof(keys));
			Contract.NotNull(values, nameof(values));
			if (values.Length != keys.Length) throw new ArgumentException("Both key and value arrays must have the same size.", nameof(values));

			for (int i = 0; i < keys.Length;i++)
			{
				trans.Set(keys[i], values[i]);
			}
		}

		/// <summary>Set the values of a sequence of keys in the database.</summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="keyValuePairs">Sequence of key and value pairs</param>
		/// <remarks>
		/// Only use this method if you know that the approximate size of count of keys and values will not exceed the maximum size allowed per transaction.
		/// If the list and size of the keys and values is not known in advance, consider using a bulk operation provided by the <see cref="Fdb.Bulk"/> helper class.
		/// </remarks>
		/// <exception cref="ArgumentNullException">If either <paramref name="trans"/> or <paramref name="keyValuePairs"/> is null.</exception>
		/// <exception cref="FdbException">If this operation would exceed the maximum allowed size for a transaction.</exception>
		public static void SetValues([NotNull] this IFdbTransaction trans, [NotNull] IEnumerable<KeyValuePair<Slice, Slice>> keyValuePairs)
		{
			Contract.NotNull(trans, nameof(trans));
			Contract.NotNull(keyValuePairs, nameof(keyValuePairs));

			foreach (var kv in keyValuePairs)
			{
				trans.Set(kv.Key, kv.Value);
			}
		}

		/// <summary>Set the values of a sequence of keys in the database.</summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="keys">Sequence of keys to set</param>
		/// <param name="values">Sequence of values for each key. Must be in the same order as <paramref name="keys"/> and have the same number of elements.</param>
		/// <remarks>
		/// Only use this method if you know that the approximate size of count of keys and values will not exceed the maximum size allowed per transaction.
		/// If the list and size of the keys and values is not known in advance, consider using a bulk operation provided by the <see cref="Fdb.Bulk"/> helper class.
		/// </remarks>
		/// <exception cref="ArgumentNullException">If either <paramref name="trans"/>, <paramref name="keys"/> or <paramref name="values"/> is null.</exception>
		/// <exception cref="ArgumentException">If the <paramref name="values"/> does not have the same number of elements as <paramref name="keys"/>.</exception>
		/// <exception cref="FdbException">If this operation would exceed the maximum allowed size for a transaction.</exception>
		public static void SetValues([NotNull] this IFdbTransaction trans, [NotNull] IEnumerable<Slice> keys, [NotNull] IEnumerable<Slice> values)
		{
			Contract.NotNull(trans, nameof(trans));
			Contract.NotNull(keys, nameof(keys));
			Contract.NotNull(values, nameof(values));

			using(var keyIter = keys.GetEnumerator())
			using(var valueIter = values.GetEnumerator())
			{
				while(keyIter.MoveNext())
				{
					if (!valueIter.MoveNext()) throw new ArgumentException("Both key and value sequences must have the same size.", nameof(values));
					trans.Set(keyIter.Current, valueIter.Current);
				}
				if (valueIter.MoveNext()) throw new ArgumentException("Both key and values sequences must have the same size.", nameof(values));
			}
		}

		#endregion

		#region Atomic Ops...

		/// <summary>Modify the database snapshot represented by this transaction to add the value of <paramref name="value"/> to the value stored by the given <paramref name="key"/>.</summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="key">Name of the key whose value is to be mutated.</param>
		/// <param name="value">Value to add to existing value of key.</param>
		public static void AtomicAdd([NotNull] this IFdbTransaction trans, Slice key, Slice value)
		{
			Contract.NotNull(trans, nameof(trans));

			trans.Atomic(key, value, FdbMutationType.Add);
		}

		/// <summary>Modify the database snapshot represented by this transaction to add <paramref name="value"/> to the value stored by the given <paramref name="key"/>.</summary>
		/// <typeparam name="TKey">Type of the key that implements <see cref="IFdbKey"/>.</typeparam>
		/// <param name="trans">Transaction instance</param>
		/// <param name="key">Name of the key whose value is to be mutated.</param>
		/// <param name="value">Value to add to existing value of key.</param>
		public static void AtomicAdd<TKey>([NotNull] this IFdbTransaction trans, TKey key, Slice value)
			where TKey : IFdbKey
		{
			if (trans == null) throw new ArgumentNullException("trans");
			if (key == null) throw new ArgumentNullException("key");

			trans.Atomic(key.ToFoundationDbKey(), value, FdbMutationType.Add);
		}

		/// <summary>Modify the database snapshot represented by this transaction to perform a bitwise AND between <paramref name="mask"/> and the value stored by the given <paramref name="key"/>.</summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="key">Name of the key whose value is to be mutated.</param>
		/// <param name="mask">Bit mask.</param>
		public static void AtomicAnd([NotNull] this IFdbTransaction trans, Slice key, Slice mask)
		{
			//TODO: rename this to AtomicBitAnd(...) ?
			Contract.NotNull(trans, nameof(trans));

			trans.Atomic(key, mask, FdbMutationType.BitAnd);
		}

		/// <summary>Modify the database snapshot represented by this transaction to perform a bitwise AND between <paramref name="mask"/> and the value stored by the given <paramref name="key"/>.</summary>
		/// <typeparam name="TKey">Type of the key that implements <see cref="IFdbKey"/>.</typeparam>
		/// <param name="trans">Transaction instance</param>
		/// <param name="key">Name of the key whose value is to be mutated.</param>
		/// <param name="mask">Bit mask.</param>
		public static void AtomicAnd<TKey>([NotNull] this IFdbTransaction trans, TKey key, Slice mask)
			where TKey : IFdbKey
		{
			//TODO: rename this to AtomicBitAnd(...) ?
			if (trans == null) throw new ArgumentNullException("trans");
			if (key == null) throw new ArgumentNullException("key");

			trans.Atomic(key.ToFoundationDbKey(), mask, FdbMutationType.BitAnd);
		}

		/// <summary>Modify the database snapshot represented by this transaction to perform a bitwise OR between <paramref name="mask"/> and the value stored by the given <paramref name="key"/>.</summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="key">Name of the key whose value is to be mutated.</param>
		/// <param name="mask">Bit mask.</param>
		public static void AtomicOr([NotNull] this IFdbTransaction trans, Slice key, Slice mask)
		{
			//TODO: rename this to AtomicBitOr(...) ?
			Contract.NotNull(trans, nameof(trans));

			trans.Atomic(key, mask, FdbMutationType.BitOr);
		}

		/// <summary>Modify the database snapshot represented by this transaction to perform a bitwise OR between <paramref name="mask"/> and the value stored by the given <paramref name="key"/>.</summary>
		/// <typeparam name="TKey">Type of the key that implements <see cref="IFdbKey"/>.</typeparam>
		/// <param name="trans">Transaction instance</param>
		/// <param name="key">Name of the key whose value is to be mutated.</param>
		/// <param name="mask">Bit mask.</param>
		public static void AtomicOr<TKey>(this IFdbTransaction trans, TKey key, Slice mask)
			where TKey : IFdbKey
		{
			//TODO: rename this to AtomicBitOr(...) ?
			if (trans == null) throw new ArgumentNullException("trans");
			if (key == null) throw new ArgumentNullException("key");

			trans.Atomic(key.ToFoundationDbKey(), mask, FdbMutationType.BitOr);
		}

		/// <summary>Modify the database snapshot represented by this transaction to perform a bitwise XOR between <paramref name="mask"/> and the value stored by the given <paramref name="key"/>.</summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="key">Name of the key whose value is to be mutated.</param>
		/// <param name="mask">Bit mask.</param>
		public static void AtomicXor([NotNull] this IFdbTransaction trans, Slice key, Slice mask)
		{
			//TODO: rename this to AtomicBitXOr(...) ?
			Contract.NotNull(trans, nameof(trans));

			trans.Atomic(key, mask, FdbMutationType.BitXor);
		}

		/// <summary>Modify the database snapshot represented by this transaction to perform a bitwise XOR between <paramref name="mask"/> and the value stored by the given <paramref name="key"/>.</summary>
		/// <typeparam name="TKey">Type of the key that implements <see cref="IFdbKey"/>.</typeparam>
		/// <param name="trans">Transaction instance</param>
		/// <param name="key">Name of the key whose value is to be mutated.</param>
		/// <param name="mask">Bit mask.</param>
		public static void AtomicXor<TKey>(this IFdbTransaction trans, TKey key, Slice mask)
			where TKey : IFdbKey
		{
			//TODO: rename this to AtomicBitXOr(...) ?
			if (trans == null) throw new ArgumentNullException("trans");
			if (key == null) throw new ArgumentNullException("key");

			trans.Atomic(key.ToFoundationDbKey(), mask, FdbMutationType.BitXor);
		}

		/// <summary>Modify the database snapshot represented by this transaction to update a value if it is larger than the value in the database.</summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="key">Name of the key whose value is to be mutated.</param>
		/// <param name="value">Bit mask.</param>
		public static void AtomicMax([NotNull] this IFdbTransaction trans, Slice key, Slice value)
		{
			Contract.NotNull(trans, nameof(trans));

			trans.Atomic(key, value, FdbMutationType.Max);
		}

		/// <summary>Modify the database snapshot represented by this transaction to update a value if it is larger than the value in the database.</summary>
		/// <typeparam name="TKey">Type of the key that implements <see cref="IFdbKey"/>.</typeparam>
		/// <param name="trans">Transaction instance</param>
		/// <param name="key">Name of the key whose value is to be mutated.</param>
		/// <param name="value">Bit mask.</param>
		public static void AtomicMax<TKey>(this IFdbTransaction trans, TKey key, Slice value)
			where TKey : IFdbKey
		{
			if (trans == null) throw new ArgumentNullException("trans");
			if (key == null) throw new ArgumentNullException("key");

			trans.Atomic(key.ToFoundationDbKey(), value, FdbMutationType.Max);
		}

		/// <summary>Modify the database snapshot represented by this transaction to update a value if it is smaller than the value in the database.</summary>
		/// <param name="trans">Transaction instance</param>
		/// <param name="key">Name of the key whose value is to be mutated.</param>
		/// <param name="value">Bit mask.</param>
		public static void AtomicMin([NotNull] this IFdbTransaction trans, Slice key, Slice value)
		{
			Contract.NotNull(trans, nameof(trans));

			trans.Atomic(key, value, FdbMutationType.Min);
		}

		/// <summary>Modify the database snapshot represented by this transaction to update a value if it is smaller than the value in the database.</summary>
		/// <typeparam name="TKey">Type of the key that implements <see cref="IFdbKey"/>.</typeparam>
		/// <param name="trans">Transaction instance</param>
		/// <param name="key">Name of the key whose value is to be mutated.</param>
		/// <param name="value">Bit mask.</param>
		public static void AtomicMin<TKey>(this IFdbTransaction trans, TKey key, Slice value)
			where TKey : IFdbKey
		{
			if (trans == null) throw new ArgumentNullException("trans");
			if (key == null) throw new ArgumentNullException("key");

			trans.Atomic(key.ToFoundationDbKey(), value, FdbMutationType.Min);
		}

		#endregion

		#region GetRange...

		public static FdbRangeQuery<KeyValuePair<Slice, Slice>> GetRange([NotNull] this IFdbReadOnlyTransaction trans, KeySelector beginInclusive, KeySelector endExclusive, int limit, bool reverse = false)
		{
			Contract.NotNull(trans, nameof(trans));

			return trans.GetRange(beginInclusive, endExclusive, new FdbRangeOptions(limit: limit, reverse: reverse));
		}

		public static FdbRangeQuery<KeyValuePair<Slice, Slice>> GetRange([NotNull] this IFdbReadOnlyTransaction trans, KeyRange range, FdbRangeOptions options = null)
		{
			return FdbTransactionExtensions.GetRange(trans, KeySelectorPair.Create(range), options);
		}

		public static FdbRangeQuery<KeyValuePair<Slice, Slice>> GetRange([NotNull] this IFdbReadOnlyTransaction trans, KeyRange range, int limit, bool reverse = false)
		{
			return FdbTransactionExtensions.GetRange(trans, range, new FdbRangeOptions(limit: limit, reverse: reverse));
		}

		public static FdbRangeQuery<KeyValuePair<Slice, Slice>> GetRange([NotNull] this IFdbReadOnlyTransaction trans, Slice beginKeyInclusive, Slice endKeyExclusive, FdbRangeOptions options = null)
		{
			Contract.NotNull(trans, nameof(trans));

			if (beginKeyInclusive.IsNullOrEmpty) beginKeyInclusive = FdbKey.MinValue;
			if (endKeyExclusive.IsNullOrEmpty) endKeyExclusive = FdbKey.MaxValue;

			return trans.GetRange(
				KeySelector.FirstGreaterOrEqual(beginKeyInclusive),
				KeySelector.FirstGreaterOrEqual(endKeyExclusive),
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

		public static FdbRangeQuery<KeyValuePair<Slice, Slice>> GetRange([NotNull] this IFdbReadOnlyTransaction trans, Slice beginKeyInclusive, Slice endKeyExclusive, int limit, bool reverse = false)
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
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="range">Pair of key selectors defining the beginning and the end of the range</param>
		/// <param name="options">Optionnal query options (Limit, TargetBytes, Mode, Reverse, ...)</param>
		/// <returns>Range query that, once executed, will return all the key-value pairs matching the providing selector pair</returns>
		public static FdbRangeQuery<KeyValuePair<Slice, Slice>> GetRange([NotNull] this IFdbReadOnlyTransaction trans, KeySelectorPair range, FdbRangeOptions options = null)
		{
			Contract.NotNull(trans, nameof(trans));

			return trans.GetRange(range.Begin, range.End, options);
		}

		/// <summary>
		/// Reads all key-value pairs in the database snapshot represented by transaction (potentially limited by Limit, TargetBytes, or Mode)
		/// which have a key lexicographically greater than or equal to the key resolved by the begin key selector
		/// and lexicographically less than the key resolved by the end key selector.
		/// </summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="range">key selector pair defining the beginning and the end of the range</param>
		/// <param name="options">Optionnal query options (Limit, TargetBytes, Mode, Reverse, ...)</param>
		/// <param name="iteration">If streaming mode is FdbStreamingMode.Iterator, this parameter should start at 1 and be incremented by 1 for each successive call while reading this range. In all other cases it is ignored.</param>
		/// <returns></returns>
		public static Task<FdbRangeChunk> GetRangeAsync([NotNull] this IFdbReadOnlyTransaction trans, KeySelectorPair range, FdbRangeOptions options = null, int iteration = 0)
		{
			Contract.NotNull(trans, nameof(trans));

			return trans.GetRangeAsync(range.Begin, range.End, options, iteration);
		}

		/// <summary>
		/// Reads all key-value pairs in the database snapshot represented by transaction (potentially limited by Limit, TargetBytes, or Mode)
		/// which have a key lexicographically greater than or equal to the key resolved by the begin key selector
		/// and lexicographically less than the key resolved by the end key selector.
		/// </summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="range">Range of keys defining the beginning (inclusive) and the end (exclusive) of the range</param>
		/// <param name="options">Optionnal query options (Limit, TargetBytes, Mode, Reverse, ...)</param>
		/// <param name="iteration">If streaming mode is FdbStreamingMode.Iterator, this parameter should start at 1 and be incremented by 1 for each successive call while reading this range. In all other cases it is ignored.</param>
		/// <returns></returns>
		public static Task<FdbRangeChunk> GetRangeAsync([NotNull] this IFdbReadOnlyTransaction trans, KeyRange range, FdbRangeOptions options = null, int iteration = 0)
		{
			Contract.NotNull(trans, nameof(trans));

			var sp = KeySelectorPair.Create(range);
			return trans.GetRangeAsync(sp.Begin, sp.End, options, iteration);
		}

		/// <summary>
		/// Reads all key-value pairs in the database snapshot represented by transaction (potentially limited by Limit, TargetBytes, or Mode)
		/// which have a key lexicographically greater than or equal to the key resolved by the begin key selector
		/// and lexicographically less than the key resolved by the end key selector.
		/// </summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="beginInclusive">Key defining the beginning (inclusive) of the range</param>
		/// <param name="endExclusive">Key defining the end (exclusive) of the range</param>
		/// <param name="options">Optionnal query options (Limit, TargetBytes, Mode, Reverse, ...)</param>
		/// <param name="iteration">If streaming mode is FdbStreamingMode.Iterator, this parameter should start at 1 and be incremented by 1 for each successive call while reading this range. In all other cases it is ignored.</param>
		/// <returns></returns>
		public static Task<FdbRangeChunk> GetRangeAsync([NotNull] this IFdbReadOnlyTransaction trans, Slice beginInclusive, Slice endExclusive, FdbRangeOptions options = null, int iteration = 0)
		{
			Contract.NotNull(trans, nameof(trans));

			var range = KeySelectorPair.Create(beginInclusive, endExclusive);
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
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="range">Pair of keys defining the range to clear.</param>
		public static void ClearRange([NotNull] this IFdbTransaction trans, KeyRange range)
		{
			Contract.NotNull(trans, nameof(trans));

			trans.ClearRange(range.Begin, range.End);
		}

		#endregion

		#region Conflict Ranges...

		/// <summary>
		/// Adds a conflict range to a transaction without performing the associated read or write.
		/// </summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="range">Range of the keys specifying the conflict range. The end key is excluded</param>
		/// <param name="type">One of the FDBConflictRangeType values indicating what type of conflict range is being set.</param>
		public static void AddConflictRange([NotNull] this IFdbTransaction trans, KeyRange range, FdbConflictRangeType type)
		{
			Contract.NotNull(trans, nameof(trans));

			trans.AddConflictRange(range.Begin, range.End, type);
		}


		/// <summary>
		/// Adds a range of keys to the transaction’s read conflict ranges as if you had read the range. As a result, other transactions that write a key in this range could cause the transaction to fail with a conflict.
		/// </summary>
		public static void AddReadConflictRange([NotNull] this IFdbTransaction trans, KeyRange range)
		{
			AddConflictRange(trans, range, FdbConflictRangeType.Read);
		}

		/// <summary>
		/// Adds a range of keys to the transaction’s read conflict ranges as if you had read the range. As a result, other transactions that write a key in this range could cause the transaction to fail with a conflict.
		/// </summary>
		public static void AddReadConflictRange([NotNull] this IFdbTransaction trans, Slice beginKeyInclusive, Slice endKeyExclusive)
		{
			Contract.NotNull(trans, nameof(trans));

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
		public static void AddReadConflictKey([NotNull] this IFdbTransaction trans, Slice key)
		{
			AddConflictRange(trans, KeyRange.FromKey(key), FdbConflictRangeType.Read);
		}

		/// <summary>
		/// Adds a key to the transaction’s read conflict ranges as if you had read the key. As a result, other transactions that write to this key could cause the transaction to fail with a conflict.
		/// </summary>
		public static void AddReadConflictKey<TKey>(this IFdbTransaction trans, TKey key)
			where TKey : IFdbKey
		{
			if (key == null) throw new ArgumentNullException("key");
			AddConflictRange(trans, KeyRange.FromKey(key.ToFoundationDbKey()), FdbConflictRangeType.Read);
		}

		/// <summary>
		/// Adds a range of keys to the transaction’s write conflict ranges as if you had cleared the range. As a result, other transactions that concurrently read a key in this range could fail with a conflict.
		/// </summary>
		public static void AddWriteConflictRange([NotNull] this IFdbTransaction trans, KeyRange range)
		{
			AddConflictRange(trans, range, FdbConflictRangeType.Write);
		}

		/// <summary>
		/// Adds a range of keys to the transaction’s write conflict ranges as if you had cleared the range. As a result, other transactions that concurrently read a key in this range could fail with a conflict.
		/// </summary>
		public static void AddWriteConflictRange([NotNull] this IFdbTransaction trans, Slice beginKeyInclusive, Slice endKeyExclusive)
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
		public static void AddWriteConflictKey([NotNull] this IFdbTransaction trans, Slice key)
		{
			AddConflictRange(trans, KeyRange.FromKey(key), FdbConflictRangeType.Write);
		}

		/// <summary>
		/// Adds a key to the transaction’s write conflict ranges as if you had cleared the key. As a result, other transactions that concurrently read this key could fail with a conflict.
		/// </summary>
		public static void AddWriteConflictKey<TKey>(this IFdbTransaction trans, TKey key)
			where TKey : IFdbKey
		{
			if (key == null) throw new ArgumentNullException("key");
			AddConflictRange(trans, KeyRange.FromKey(key.ToFoundationDbKey()), FdbConflictRangeType.Write);
		}

		#endregion

		#region Watches...

		/// <summary>Reads the value associated with <paramref name="key"/>, and returns a Watch that will complete after a subsequent change to key in the database.</summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="key">Key to be looked up in the database</param>
		/// <param name="cancellationToken">Token that can be used to cancel the Watch from the outside.</param>
		/// <returns>A new Watch that will track any changes to <paramref name="key"/> in the database, and whose <see cref="FdbWatch.Value">Value</see> property contains the current value of the key.</returns>
		public static async Task<FdbWatch> GetAndWatchAsync([NotNull] this IFdbTransaction trans, Slice key, CancellationToken cancellationToken)
		{
			Contract.NotNull(trans, nameof(trans));
			cancellationToken.ThrowIfCancellationRequested();

			var value = await trans.GetAsync(key);
			var watch = trans.Watch(key, cancellationToken);
			watch.Value = value;

			return watch;
		}

		/// <summary>Reads the value associated with <paramref name="key"/>, and returns a Watch that will complete after a subsequent change to key in the database.</summary>
		/// <typeparam name="TKey">Type of the key, which must implement the IFdbKey interface</typeparam>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="key">Key to be looked up in the database</param>
		/// <param name="cancellationToken">Token that can be used to cancel the Watch from the outside.</param>
		/// <returns>A new Watch that will track any changes to <paramref name="key"/> in the database, and whose <see cref="FdbWatch.Value">Value</see> property contains the current value of the key.</returns>
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
		public static FdbWatch SetAndWatch([NotNull] this IFdbTransaction trans, Slice key, Slice value, CancellationToken cancellationToken)
		{
			Contract.NotNull(trans, nameof(trans));
			cancellationToken.ThrowIfCancellationRequested();

			trans.Set(key, value);
			var watch = trans.Watch(key, cancellationToken);
			watch.Value = value;

			return watch;
		}

		/// <summary>Sets <paramref name="key"/> to <paramref name="value"/> and returns a Watch that will complete after a subsequent change to the key in the database.</summary>
		/// <typeparam name="TKey">Type of the key, which must implement the IFdbKey interface</typeparam>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="key">Name of the key to be inserted into the database.</param>
		/// <param name="value">Value to be inserted into the database.</param>
		/// <param name="cancellationToken">Token that can be used to cancel the Watch from the outside.</param>
		/// <returns>A new Watch that will track any changes to <paramref name="key"/> in the database, and whose <see cref="FdbWatch.Value">Value</see> property will be a copy of <paramref name="value"/> argument</returns>
		public static FdbWatch SetAndWatch<TKey>(this IFdbTransaction trans, TKey key, Slice value, CancellationToken cancellationToken)
			where TKey : IFdbKey
		{
			if (key == null) throw new ArgumentNullException("key");
			return SetAndWatch(trans, key.ToFoundationDbKey(), value, cancellationToken);
		}

		/// <summary>Sets <paramref name="key"/> to <paramref name="value"/> and returns a Watch that will complete after a subsequent change to the key in the database.</summary>
		/// <typeparam name="TValue">Type of the value</typeparam>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="key">Name of the key to be inserted into the database.</param>
		/// <param name="value">Value to be inserted into the database.</param>
		/// <param name="encoder">Encoder use to convert <paramref name="value"/> into a slice</param>
		/// <param name="cancellationToken">Token that can be used to cancel the Watch from the outside.</param>
		/// <returns>A new Watch that will track any changes to <paramref name="key"/> in the database, and whose <see cref="FdbWatch.Value">Value</see> property will be a copy of <paramref name="value"/> argument</returns>
		public static FdbWatch SetAndWatch<TValue>([NotNull] this IFdbTransaction trans, Slice key, TValue value, [NotNull] IValueEncoder<TValue> encoder, CancellationToken cancellationToken)
		{
			Contract.NotNull(encoder, nameof(encoder));
			cancellationToken.ThrowIfCancellationRequested();
			return SetAndWatch(trans, key, encoder.EncodeValue(value), cancellationToken);
		}

		/// <summary>Sets <paramref name="key"/> to <paramref name="value"/> and returns a Watch that will complete after a subsequent change to the key in the database.</summary>
		/// <typeparam name="TKey">Type of the key, which must implement the IFdbKey interface</typeparam>
		/// <typeparam name="TValue">Type of the value</typeparam>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="key">Name of the key to be inserted into the database.</param>
		/// <param name="value">Value to be inserted into the database.</param>
		/// <param name="encoder">Encoder use to convert <paramref name="value"/> into a slice</param>
		/// <param name="cancellationToken">Token that can be used to cancel the Watch from the outside.</param>
		/// <returns>A new Watch that will track any changes to <paramref name="key"/> in the database, and whose <see cref="FdbWatch.Value">Value</see> property will be a copy of <paramref name="value"/> argument</returns>
		public static FdbWatch SetAndWatch<TKey, TValue>(this IFdbTransaction trans, TKey key, TValue value, [NotNull] IValueEncoder<TValue> encoder, CancellationToken cancellationToken)
			where TKey : IFdbKey
		{
			if (key == null) throw new ArgumentNullException("key");
			if (encoder == null) throw new ArgumentNullException("encoder");
			cancellationToken.ThrowIfCancellationRequested();
			return SetAndWatch(trans, key.ToFoundationDbKey(), encoder.EncodeValue(value), cancellationToken);
		}

		#endregion

		#region Batching...

		/// <summary>
		/// Reads several values from the database snapshot represented by the current transaction
		/// </summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="keys">Sequence of keys to be looked up in the database</param>
		/// <returns>Task that will return an array of values, or an exception. The position of each item in the array is the same as its coresponding key in <paramref name="keys"/>. If a key does not exist in the database, its value will be Slice.Nil.</returns>
		[ItemNotNull]
		public static Task<Slice[]> GetValuesAsync([NotNull] this IFdbReadOnlyTransaction trans, [NotNull] IEnumerable<Slice> keys)
		{
			Contract.NotNull(trans, nameof(trans));
			Contract.NotNull(keys, nameof(keys));

			var array = keys as Slice[] ?? keys.ToArray();

			return trans.GetValuesAsync(array);
		}

		/// <summary>
		/// Reads several values from the database snapshot represented by the current transaction.
		/// </summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="keys">Sequence of keys to be looked up in the database</param>
		/// <param name="decoder">Decoder used to decoded the results into values of type <typeparamref name="TValue"/></param>
		/// <returns>Task that will return an array of decoded values, or an exception. The position of each item in the array is the same as its coresponding key in <paramref name="keys"/>. If a key does not exist in the database, its value depends on the behavior of <paramref name="decoder"/>.</returns>
		[ItemNotNull]
		public static async Task<TValue[]> GetValuesAsync<TValue>([NotNull] this IFdbReadOnlyTransaction trans, [NotNull] IEnumerable<Slice> keys, [NotNull] IValueEncoder<TValue> decoder)
		{
			Contract.NotNull(trans, nameof(trans));
			Contract.NotNull(decoder, nameof(decoder));

			return decoder.DecodeValues(await GetValuesAsync(trans, keys).ConfigureAwait(false));
		}

		/// <summary>
		/// Reads several values from the database snapshot represented by the current transaction
		/// </summary>
		/// <param name="keys">Sequence of keys to be looked up in the database</param>
		/// <returns>Task that will return an array of values, or an exception. The position of each item in the array is the same as its coresponding key in <paramref name="keys"/>. If a key does not exist in the database, its value will be Slice.Nil.</returns>
		[ItemNotNull]
		public static Task<Slice[]> GetValuesAsync<TKey>(this IFdbReadOnlyTransaction trans, [NotNull] IEnumerable<TKey> keys)
			where TKey : IFdbKey
		{
			if (keys == null) throw new ArgumentNullException("keys");

			return GetValuesAsync(trans, keys.Select(key => key.ToFoundationDbKey()));
		}

		/// <summary>
		/// Reads several values from the database snapshot represented by the current transaction.
		/// </summary>
		/// <param name="keys">Sequence of keys to be looked up in the database</param>
		/// <param name="decoder">Decoder used to decoded the results into values of type <typeparamref name="TValue"/></param>
		/// <returns>Task that will return an array of decoded values, or an exception. The position of each item in the array is the same as its coresponding key in <paramref name="keys"/>. If a key does not exist in the database, its value depends on the behavior of <paramref name="decoder"/>.</returns>
		[ItemNotNull]
		public static Task<TValue[]> GetValuesAsync<TKey, TValue>(this IFdbReadOnlyTransaction trans, [NotNull] IEnumerable<TKey> keys, [NotNull] IValueEncoder<TValue> decoder)
			where TKey : IFdbKey
		{
			if (keys == null) throw new ArgumentNullException("keys");

			return GetValuesAsync<TValue>(trans, keys.Select(key => key.ToFoundationDbKey()), decoder);
		}

		/// <summary>
		/// Resolves several key selectors against the keys in the database snapshot represented by the current transaction.
		/// </summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="selectors">Sequence of key selectors to resolve</param>
		/// <returns>Task that will return an array of keys matching the selectors, or an exception</returns>
		[ItemNotNull]
		public static Task<Slice[]> GetKeysAsync([NotNull] this IFdbReadOnlyTransaction trans, [NotNull] IEnumerable<KeySelector> selectors)
		{
			Contract.NotNull(trans, nameof(trans));
			Contract.NotNull(selectors, nameof(selectors));

			var array = selectors as KeySelector[] ?? selectors.ToArray();

			return trans.GetKeysAsync(array);
		}

		/// <summary>
		/// Reads several values from the database snapshot represented by the current transaction.
		/// </summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="keys">Sequence of keys to be looked up in the database</param>
		/// <returns>Task that will return an array of key/value pairs, or an exception. Each pair in the array will contain the key at the same index in <paramref name="keys"/>, and its corresponding value in the database or Slice.Nil if that key does not exist.</returns>
		/// <remarks>This method is equivalent to calling <see cref="IFdbReadOnlyTransaction.GetValuesAsync"/>, except that it will return the keys in addition to the values.</remarks>
		[ItemNotNull]
		public static Task<KeyValuePair<Slice, Slice>[]> GetBatchAsync([NotNull] this IFdbReadOnlyTransaction trans, [NotNull] IEnumerable<Slice> keys)
		{
			Contract.NotNull(trans, nameof(trans));
			Contract.NotNull(keys, nameof(keys));

			var array = keys as Slice[] ?? keys.ToArray();

			return trans.GetBatchAsync(array);
		}

		/// <summary>
		/// Reads several values from the database snapshot represented by the current transaction.
		/// </summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="keys">Array of keys to be looked up in the database</param>
		/// <returns>Task that will return an array of key/value pairs, or an exception. Each pair in the array will contain the key at the same index in <paramref name="keys"/>, and its corresponding value in the database or Slice.Nil if that key does not exist.</returns>
		/// <remarks>This method is equivalent to calling <see cref="IFdbReadOnlyTransaction.GetValuesAsync"/>, except that it will return the keys in addition to the values.</remarks>
		[ItemNotNull]
		public static async Task<KeyValuePair<Slice, Slice>[]> GetBatchAsync([NotNull] this IFdbReadOnlyTransaction trans, [NotNull] Slice[] keys)
		{
			Contract.NotNull(trans, nameof(trans));
			Contract.NotNull(keys, nameof(keys));

			var results = await trans.GetValuesAsync(keys).ConfigureAwait(false);
			Contract.Assert(results != null && results.Length == keys.Length);

			var array = new KeyValuePair<Slice, Slice>[results.Length];
			for (int i = 0; i < array.Length;i++)
			{
				array[i] = new KeyValuePair<Slice, Slice>(keys[i], results[i]);
			}
			return array;
		}

		/// <summary>
		/// Reads several values from the database snapshot represented by the current transaction.
		/// </summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="keys">Array of keys to be looked up in the database</param>
		/// <param name="decoder">Decoder used to decoded the results into values of type <typeparamref name="TValue"/></param>
		/// <returns>Task that will return an array of pairs of key and decoded values, or an exception. The position of each item in the array is the same as its coresponding key in <paramref name="keys"/>. If a key does not exist in the database, its value depends on the behavior of <paramref name="decoder"/>.</returns>
		[ItemNotNull]
		public static Task<KeyValuePair<Slice, TValue>[]> GetBatchAsync<TValue>([NotNull] this IFdbReadOnlyTransaction trans, [NotNull] IEnumerable<Slice> keys, [NotNull] IValueEncoder<TValue> decoder)
		{
			Contract.NotNull(trans, nameof(trans));
			Contract.NotNull(keys, nameof(keys));

			var array = keys as Slice[] ?? keys.ToArray();

			return trans.GetBatchAsync(array, decoder);
		}

		/// <summary>
		/// Reads several values from the database snapshot represented by the current transaction.
		/// </summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="keys">Sequence of keys to be looked up in the database</param>
		/// <param name="decoder">Decoder used to decoded the results into values of type <typeparamref name="TValue"/></param>
		/// <returns>Task that will return an array of pairs of key and decoded values, or an exception. The position of each item in the array is the same as its coresponding key in <paramref name="keys"/>. If a key does not exist in the database, its value depends on the behavior of <paramref name="decoder"/>.</returns>
		[ItemNotNull]
		public static async Task<KeyValuePair<Slice, TValue>[]> GetBatchAsync<TValue>([NotNull] this IFdbReadOnlyTransaction trans, [NotNull] Slice[] keys, [NotNull] IValueEncoder<TValue> decoder)
		{
			Contract.NotNull(trans, nameof(trans));
			Contract.NotNull(keys, nameof(keys));
			Contract.NotNull(decoder, nameof(decoder));

			var results = await trans.GetValuesAsync(keys).ConfigureAwait(false);
			Contract.Assert(results != null && results.Length == keys.Length);

			var array = new KeyValuePair<Slice, TValue>[results.Length];
			for (int i = 0; i < array.Length; i++)
			{
				array[i] = new KeyValuePair<Slice, TValue>(keys[i], decoder.DecodeValue(results[i]));
			}
			return array;
		}

		/// <summary>
		/// Reads several values from the database snapshot represented by the current transaction.
		/// </summary>
		/// <param name="keys">Sequence of keys to be looked up in the database</param>
		/// <returns>Task that will return an array of key/value pairs, or an exception. Each pair in the array will contain the key at the same index in <paramref name="keys"/>, and its corresponding value in the database or Slice.Nil if that key does not exist.</returns>
		/// <remarks>This method is equivalent to calling <see cref="IFdbReadOnlyTransaction.GetValuesAsync"/>, except that it will return the keys in addition to the values.</remarks>
		[ItemNotNull]
		public static Task<KeyValuePair<Slice, Slice>[]> GetBatchAsync<TKey>(this IFdbReadOnlyTransaction trans, [NotNull] IEnumerable<TKey> keys)
			where TKey : IFdbKey
		{
			if (keys == null) throw new ArgumentNullException("keys");
			return GetBatchAsync(trans, keys.Select(key => key.ToFoundationDbKey()).ToArray());
		}

		/// <summary>
		/// Reads several values from the database snapshot represented by the current transaction.
		/// </summary>
		/// <param name="keys">Sequence of keys to be looked up in the database</param>
		/// <param name="decoder">Decoder used to decoded the results into values of type <typeparamref name="TValue"/></param>
		/// <returns>Task that will return an array of pairs of key and decoded values, or an exception. The position of each item in the array is the same as its coresponding key in <paramref name="keys"/>. If a key does not exist in the database, its value depends on the behavior of <paramref name="decoder"/>.</returns>
		[ItemNotNull]
		public static Task<KeyValuePair<Slice, TValue>[]> GetBatchAsync<TKey, TValue>(this IFdbReadOnlyTransaction trans, [NotNull] IEnumerable<TKey> keys, [NotNull] IValueEncoder<TValue> decoder)
			where TKey : IFdbKey
		{
			if (keys == null) throw new ArgumentNullException("keys");
			return GetBatchAsync<TValue>(trans, keys.Select(key => key.ToFoundationDbKey()).ToArray(), decoder);
		}

		#endregion

		#region Queries...

		/// <summary>Runs a query inside a read-only transaction context, with retry-logic.</summary>
		/// <param name="db">Database used for the operation</param>
		/// <param name="handler">Lambda function that returns an async enumerable. The function may be called multiple times if the transaction conflicts.</param>
		/// <param name="cancellationToken">Token used to cancel the operation</param>
		/// <returns>Task returning the list of all the elements of the async enumerable returned by the last successfull call to <paramref name="handler"/>.</returns>
		[ItemNotNull]
		public static Task<List<T>> QueryAsync<T>([NotNull] this IFdbReadOnlyRetryable db, [NotNull, InstantHandle] Func<IFdbReadOnlyTransaction, IFdbAsyncEnumerable<T>> handler, CancellationToken cancellationToken)
		{
			Contract.NotNull(db, nameof(db));
			Contract.NotNull(handler, nameof(handler));

			return db.ReadAsync(
				(tr) =>
				{
					var query = handler(tr);
					if (query == null) throw new InvalidOperationException("The query handler returned a null sequence");
					return query.ToListAsync();
				},
				cancellationToken
			);
		}

		#endregion

	}
}
