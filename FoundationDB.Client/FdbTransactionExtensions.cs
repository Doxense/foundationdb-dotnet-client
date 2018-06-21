#region BSD Licence
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

namespace FoundationDB.Client
{
	using System;
	using System.Collections.Generic;
	using System.IO;
	using System.Linq;
	using System.Runtime.CompilerServices;
	using System.Threading;
	using System.Threading.Tasks;
	using Doxense.Diagnostics.Contracts;
	using Doxense.Linq;
	using Doxense.Memory;
	using Doxense.Serialization.Encoders;
	using JetBrains.Annotations;

	/// <summary>Provides a set of extensions methods shared by all FoundationDB transaction implementations.</summary>
	[PublicAPI]
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

		#endregion

		#region Set...

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

		/// <summary>Cached 0 (32-bits)</summary>
		private static readonly Slice Zero32 = Slice.FromFixed32(0);

		/// <summary>Cached 0 (64-bits)</summary>
		private static readonly Slice Zero64 = Slice.FromFixed64(0);

		/// <summary>Cached +1 (32-bits)</summary>
		private static readonly Slice PlusOne32 = Slice.FromFixed32(1);

		/// <summary>+1 (64-bits)</summary>
		private static readonly Slice PlusOne64 = Slice.FromFixed64(1);

		/// <summary>-1 (32-bits)</summary>
		private static readonly Slice MinusOne32 = Slice.FromFixed32(-1);

		/// <summary>-1 (64-bits)</summary>
		private static readonly Slice MinusOne64 = Slice.FromFixed64(-1);

		/// <summary>Modify the database snapshot represented by this transaction to increment by one the 32-bit value stored by the given <paramref name="key"/>.</summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="key">Name of the key whose value is to be mutated.</param>
		public static void AtomicIncrement32([NotNull] this IFdbTransaction trans, Slice key)
		{
			Contract.NotNull(trans, nameof(trans));

			trans.Atomic(key, PlusOne32, FdbMutationType.Add);
		}

		/// <summary>Modify the database snapshot represented by this transaction to substract 1 from the 32-bit value stored by the given <paramref name="key"/>.</summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="key">Name of the key whose value is to be mutated.</param>
		public static void AtomicDecrement32([NotNull] this IFdbTransaction trans, Slice key)
		{
			Contract.NotNull(trans, nameof(trans));

			trans.Atomic(key, MinusOne32, FdbMutationType.Add);
		}

		/// <summary>Modify the database snapshot represented by this transaction to add 1 to the 64-bit value stored by the given <paramref name="key"/>.</summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="key">Name of the key whose value is to be mutated.</param>
		public static void AtomicIncrement64([NotNull] this IFdbTransaction trans, Slice key)
		{
			Contract.NotNull(trans, nameof(trans));

			trans.Atomic(key, PlusOne64, FdbMutationType.Add);
		}

		/// <summary>Modify the database snapshot represented by this transaction to substract 1 from the 64-bit value stored by the given <paramref name="key"/>.</summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="key">Name of the key whose value is to be mutated.</param>
		public static void AtomicDecrement64([NotNull] this IFdbTransaction trans, Slice key)
		{
			Contract.NotNull(trans, nameof(trans));

			trans.Atomic(key, MinusOne64, FdbMutationType.Add);
		}

		/// <summary>Modify the database snapshot represented by this transaction to add a signed integer to the 32-bit value stored by the given <paramref name="key"/>.</summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="key">Name of the key whose value is to be mutated.</param>
		/// <param name="value">Integer add to existing value of key. It will encoded as 4 bytes in high-endian.</param>
		public static void AtomicAdd32([NotNull] this IFdbTransaction trans, Slice key, int value)
		{
			Contract.NotNull(trans, nameof(trans));

			var arg = value == 1 ? PlusOne32 : value == -1 ? MinusOne32 : Slice.FromFixed32(value);
			trans.Atomic(key, arg, FdbMutationType.Add);
		}

		/// <summary>Modify the database snapshot represented by this transaction to add an unsigned integer to the 32-bit value stored by the given <paramref name="key"/>.</summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="key">Name of the key whose value is to be mutated.</param>
		/// <param name="value">Integer add to existing value of key. It will encoded as 4 bytes in high-endian.</param>
		public static void AtomicAdd32([NotNull] this IFdbTransaction trans, Slice key, uint value)
		{
			Contract.NotNull(trans, nameof(trans));

			var arg = value == 1 ? PlusOne32 : value == uint.MaxValue ? MinusOne32 : Slice.FromFixedU32(value);
			trans.Atomic(key, arg, FdbMutationType.Add);
		}

		/// <summary>Modify the database snapshot represented by this transaction to add a signed integer to the 64-bit value stored by the given <paramref name="key"/>.</summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="key">Name of the key whose value is to be mutated.</param>
		/// <param name="value">Integer add to existing value of key. It will encoded as 8 bytes in high-endian.</param>
		public static void AtomicAdd64([NotNull] this IFdbTransaction trans, Slice key, long value)
		{
			Contract.NotNull(trans, nameof(trans));

			var arg = value == 1 ? PlusOne64 : value == -1 ? MinusOne64 : Slice.FromFixed64(value);
			trans.Atomic(key, arg, FdbMutationType.Add);
		}

		/// <summary>Modify the database snapshot represented by this transaction to add an unsigned integer to the 64-bit value stored by the given <paramref name="key"/>.</summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="key">Name of the key whose value is to be mutated.</param>
		/// <param name="value">Integer add to existing value of key. It will encoded as 8 bytes in high-endian.</param>
		public static void AtomicAdd64([NotNull] this IFdbTransaction trans, Slice key, ulong value)
		{
			Contract.NotNull(trans, nameof(trans));

			var arg = value == 1 ? PlusOne64 : value == ulong.MaxValue ? MinusOne64 : Slice.FromFixedU64(value);
			trans.Atomic(key, arg, FdbMutationType.Add);
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

		/// <summary>Modify the database snapshot represented by this transaction to append to a value, unless it would become larger that the maximum value size supported by the database.</summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="key">Name of the key whose value is to be mutated.</param>
		/// <param name="value">Value to append.</param>
		public static void AtomicAppendIfFits([NotNull] this IFdbTransaction trans, Slice key, Slice value)
		{
			Contract.NotNull(trans, nameof(trans));

			trans.Atomic(key, value, FdbMutationType.AppendIfFits);
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

		/// <summary>Modify the database snapshot represented by this transaction to update a value if it is smaller than the value in the database.</summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="key">Name of the key whose value is to be mutated.</param>
		/// <param name="value">Bit mask.</param>
		public static void AtomicMin([NotNull] this IFdbTransaction trans, Slice key, Slice value)
		{
			Contract.NotNull(trans, nameof(trans));

			trans.Atomic(key, value, FdbMutationType.Min);
		}

		/// <summary>Find the location of the VersionStamp in a key or value</summary>
		/// <param name="buffer">Buffer that must contains <paramref name="token"/> once and only once</param>
		/// <param name="token">Token that represents the VersionStamp</param>
		/// <param name="argName"></param>
		/// <returns>Offset in <paramref name="buffer"/> where the stamp was found</returns>
		private static int GetVersionStampOffset(Slice buffer, Slice token, string argName)
		{
			// the buffer MUST contain one incomplete stamp, either the random token of the current transsaction or the default token (all-FF)

			if (buffer.Count < token.Count) throw new ArgumentException("The key is too small to contain a VersionStamp.", argName);

			int p = token.HasValue ? buffer.IndexOf(token) : -1;
			if (p >= 0)
			{ // found a candidate spot, we have to make sure that it is only present once in the key!

				if (buffer.IndexOf(token, p + token.Count) >= 0)
				{
					if (argName == "key")
						throw new ArgumentException("The key should only contain one occurrence of a VersionStamp.", argName);
					else
						throw new ArgumentException("The value should only contain one occurrence of a VersionStamp.", argName);
				}
			}
			else
			{ // not found, maybe it is using the default incomplete stamp (all FF) ?
				p = buffer.IndexOf(VersionStamp.IncompleteToken);
				if (p < 0)
				{
					if (argName == "key")
						throw new ArgumentException("The key should contain at least one VersionStamp.", argName);
					else 
						throw new ArgumentException("The value should contain at least one VersionStamp.", argName);
				}
			}
			Contract.Assert(p >= 0 && p + token.Count <= buffer.Count);

			return p;
		}

		[Pure, NotNull, MethodImpl(MethodImplOptions.NoInlining)]
		private static Exception FailVersionStampNotSupported()
		{
			return new NotSupportedException($"VersionStamps are not supported at API version {Fdb.ApiVersion}. You need to select at least API Version 400 or above.");
		}

		/// <summary>Set the <paramref name="value"/> of the <paramref name="key"/> in the database, with the <see cref="VersionStamp"/> replaced by the resolved version at commit time.</summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="key">Name of the key whose value is to be mutated. This key must contain a single <see cref="VersionStamp"/>, whose position will be automatically detected.</param>
		/// <param name="value">New value for this key.</param>
		public static void SetVersionStampedKey([NotNull] this IFdbTransaction trans, Slice key, Slice value)
		{
			Contract.NotNull(trans, nameof(trans));

			//TODO: PERF: optimize this to not have to allocate!
			var token = trans.CreateVersionStamp().ToSlice();
			var offset = GetVersionStampOffset(key, token, nameof(key));

			int apiVer = Fdb.ApiVersion;
			if (apiVer < 400)
			{ // introduced in 400
				throw FailVersionStampNotSupported();
			}

			Slice arg;
			if (apiVer < 520)
			{ // prior to 520, the offset is only 16-bits
				var writer = new SliceWriter(key.Count + 2);
				writer.WriteBytes(in key);
				writer.WriteFixed16(checked((ushort) offset)); // 16-bits little endian
				arg = writer.ToSlice();
			}
			else
			{ // starting from 520, the offset is 32 bits
				var writer = new SliceWriter(key.Count + 4);
				writer.WriteBytes(in key);
				writer.WriteFixed32(checked((uint) offset)); // 32-bits little endian
				arg = writer.ToSlice();
			}

			trans.Atomic(arg, value, FdbMutationType.VersionStampedKey);
		}

		/// <summary>Set the <paramref name="value"/> of the <paramref name="key"/> in the database, with the <see cref="VersionStamp"/> replaced by the resolved version at commit time.</summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="key">Name of the key whose value is to be mutated. This key must contain a single <see cref="VersionStamp"/>, whose start is defined by <paramref name="stampOffset"/>.</param>
		/// <param name="stampOffset">Offset in <paramref name="key"/> where the 80-bit VersionStamp is located.</param>
		/// <param name="value">New value for this key.</param>
		public static void SetVersionStampedKey([NotNull] this IFdbTransaction trans, Slice key, int stampOffset, Slice value)
		{
			Contract.NotNull(trans, nameof(trans));
			Contract.Positive(stampOffset, nameof(stampOffset));
			if (stampOffset > key.Count - 10) throw new ArgumentException("The VersionStamp overflows past the end of the key.", nameof(stampOffset));

			int apiVer = Fdb.ApiVersion;
			if (apiVer < 400)
			{ // introduced in 400
				throw FailVersionStampNotSupported();
			}

			Slice arg;
			if (apiVer < 520)
			{ // prior to 520, the offset is only 16-bits
				if (stampOffset > 0xFFFF) throw new ArgumentException("The offset is too large to fit within 16-bits.");
				var writer = new SliceWriter(key.Count + 2);
				writer.WriteBytes(in key);
				writer.WriteFixed16(checked((ushort) stampOffset)); //stored as 32-bits in Little Endian
				arg = writer.ToSlice();
			}
			else
			{ // starting from 520, the offset is 32 bits
				var writer = new SliceWriter(key.Count + 4);
				writer.WriteBytes(in key);
				writer.WriteFixed32(checked((uint) stampOffset)); //stored as 32-bits in Little Endian
				arg = writer.ToSlice();
			}

			trans.Atomic(arg, value, FdbMutationType.VersionStampedKey);
		}

		/// <summary>Set the <paramref name="value"/> of the <paramref name="key"/> in the database, with the first 10 bytes overwritten with the transaction's <see cref="VersionStamp"/>.</summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="key">Name of the key whose value is to be mutated.</param>
		/// <param name="value">Value whose first 10 bytes will be overwritten by the database with the resolved VersionStamp at commit time. The rest of the value will be untouched.</param>
		public static void SetVersionStampedValue([NotNull] this IFdbTransaction trans, Slice key, Slice value)
		{
			Contract.NotNull(trans, nameof(trans));
			if (value.Count < 10) throw new ArgumentException("The value must be at least 10 bytes long.", nameof(value));

			int apiVer = Fdb.ApiVersion;
			if (apiVer < 400)
			{ // introduced in 400
				throw FailVersionStampNotSupported();
			}

			Slice arg;
			if (apiVer < 520)
			{ // prior to 520, the stamp is always at offset 0
				arg = value;
			}
			else
			{ // starting from 520, the offset is stored in the last 32 bits

				//TODO: PERF: optimize this to not have to allocate!
				var token = trans.CreateVersionStamp().ToSlice();
				var offset = GetVersionStampOffset(key, token, nameof(key));
				Contract.Requires(offset >=0 && offset <= key.Count - 10);

				var writer = new SliceWriter(value.Count + 4);
				writer.WriteBytes(in value);
				writer.WriteFixed32(checked((uint) offset));
				arg = writer.ToSlice();
			}

			trans.Atomic(key, arg, FdbMutationType.VersionStampedValue);
		}

		/// <summary>Set the <paramref name="value"/> of the <paramref name="key"/> in the database, with the first 10 bytes overwritten with the transaction's <see cref="VersionStamp"/>.</summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="key">Name of the key whose value is to be mutated.</param>
		/// <param name="value">Value of the key. The 10 bytes starting at <paramref name="stampOffset"/> will be overwritten by the database with the resolved VersionStamp at commit time. The rest of the value will be untouched.</param>
		/// <param name="stampOffset">Offset in <paramref name="value"/> where the 80-bit VersionStamp is located. Prior to API version 520, it can only be located at offset 0.</param>
		public static void SetVersionStampedValue([NotNull] this IFdbTransaction trans, Slice key, Slice value, int stampOffset)
		{
			Contract.NotNull(trans, nameof(trans));
			Contract.Positive(stampOffset, nameof(stampOffset));
			if (stampOffset > key.Count - 10) throw new ArgumentException("The VersionStamp overflows past the end of the value.", nameof(stampOffset));

			int apiVer = Fdb.ApiVersion;
			if (apiVer < 400)
			{ // introduced in 400
				throw FailVersionStampNotSupported();
			}

			Slice arg;
			if (apiVer < 520)
			{ // prior to 520, the stamp is always at offset 0
				if (stampOffset != 0) throw new InvalidOperationException("Prior to API version 520, the VersionStamp can only be located at offset 0. Please update to API Version 520 or above!");
				// let it slide!
				arg = value;
			}
			else
			{ // starting from 520, the offset is stored in the last 32 bits

				//TODO: PERF: optimize this to not have to allocate!
				var token = trans.CreateVersionStamp().ToSlice();
				var offset = GetVersionStampOffset(key, token, nameof(key));
				Contract.Requires(offset >=0 && offset <= key.Count - 10);

				var writer = new SliceWriter(value.Count + 4);
				writer.WriteBytes(in value);
				writer.WriteFixed32(checked((uint) offset));
				arg = writer.ToSlice();
			}

			trans.Atomic(key, arg, FdbMutationType.VersionStampedValue);
		}

		#endregion

		#region GetRange...

		/// <summary>Create a new range query that will read all key-value pairs in the database snapshot represented by the transaction</summary>
		[Obsolete("Use the overload that takes an FdbRangeOptions argument, or use LINQ to configure the query!")]
		public static FdbRangeQuery<KeyValuePair<Slice, Slice>> GetRange([NotNull] this IFdbReadOnlyTransaction trans, KeySelector beginInclusive, KeySelector endExclusive, int limit, bool reverse = false)
		{
			Contract.NotNull(trans, nameof(trans));

			return trans.GetRange(beginInclusive, endExclusive, new FdbRangeOptions(limit: limit, reverse: reverse));
		}

		/// <summary>Create a new range query that will read all key-value pairs in the database snapshot represented by the transaction</summary>
		public static FdbRangeQuery<KeyValuePair<Slice, Slice>> GetRange([NotNull] this IFdbReadOnlyTransaction trans, KeyRange range, FdbRangeOptions options = null)
		{
			return GetRange(trans, KeySelectorPair.Create(range), options);
		}

		/// <summary>Create a new range query that will read all key-value pairs in the database snapshot represented by the transaction</summary>
		[Obsolete("Use the overload that takes an FdbRangeOptions argument, or use LINQ to configure the query!")]
		public static FdbRangeQuery<KeyValuePair<Slice, Slice>> GetRange([NotNull] this IFdbReadOnlyTransaction trans, KeyRange range, int limit, bool reverse = false)
		{
			return GetRange(trans, range, new FdbRangeOptions(limit: limit, reverse: reverse));
		}

		/// <summary>Create a new range query that will read all key-value pairs in the database snapshot represented by the transaction</summary>
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

		/// <summary>Create a new range query that will read all key-value pairs in the database snapshot represented by the transaction</summary>
		[Obsolete("Use the overload that takes an FdbRangeOptions argument, or use LINQ to configure the query!")]
		public static FdbRangeQuery<KeyValuePair<Slice, Slice>> GetRange([NotNull] this IFdbReadOnlyTransaction trans, Slice beginKeyInclusive, Slice endKeyExclusive, int limit, bool reverse = false)
		{
			return GetRange(trans, beginKeyInclusive, endKeyExclusive, new FdbRangeOptions(limit: limit, reverse: reverse));
		}

		/// <summary>Create a new range query that will read all key-value pairs in the database snapshot represented by the transaction</summary>
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
		/// Modify the database snapshot represented by this transaction to remove all keys (if any) which are lexicographically greater than or equal to the given begin key and lexicographically less than the given end_key.
		/// Sets and clears affect the actual database only if transaction is later committed with CommitAsync().
		/// </summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="range">Pair of keys defining the range to clear.</param>
		public static void ClearRange([NotNull] this IFdbTransaction trans, KeyRange range)
		{
			Contract.NotNull(trans, nameof(trans));

			trans.ClearRange(range.Begin, range.End.HasValue ? range.End : FdbKey.MaxValue);
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

			trans.AddConflictRange(range.Begin, range.End.HasValue ? range.End : FdbKey.MaxValue, type);
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
		/// Adds a key to the transaction’s read conflict ranges as if you had read the key. As a result, other transactions that write to this key could cause the transaction to fail with a conflict.
		/// </summary>
		public static void AddReadConflictKey([NotNull] this IFdbTransaction trans, Slice key)
		{
			AddConflictRange(trans, KeyRange.FromKey(key), FdbConflictRangeType.Read);
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
			Contract.NotNull(trans, nameof(trans));

			trans.AddConflictRange(beginKeyInclusive, endKeyExclusive, FdbConflictRangeType.Write);
		}

		/// <summary>
		/// Adds a key to the transaction’s write conflict ranges as if you had cleared the key. As a result, other transactions that concurrently read this key could fail with a conflict.
		/// </summary>
		public static void AddWriteConflictKey([NotNull] this IFdbTransaction trans, Slice key)
		{
			AddConflictRange(trans, KeyRange.FromKey(key), FdbConflictRangeType.Write);
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

		#endregion

		#region Queries...

		/// <summary>Runs a query inside a read-only transaction context, with retry-logic.</summary>
		/// <param name="db">Database used for the operation</param>
		/// <param name="handler">Lambda function that returns an async enumerable. The function may be called multiple times if the transaction conflicts.</param>
		/// <param name="ct">Token used to cancel the operation</param>
		/// <returns>Task returning the list of all the elements of the async enumerable returned by the last successfull call to <paramref name="handler"/>.</returns>
		[ItemNotNull]
		public static Task<List<T>> QueryAsync<T>([NotNull] this IFdbReadOnlyRetryable db, [NotNull, InstantHandle] Func<IFdbReadOnlyTransaction, IAsyncEnumerable<T>> handler, CancellationToken ct)
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
				ct
			);
		}

		#endregion

	}
}
