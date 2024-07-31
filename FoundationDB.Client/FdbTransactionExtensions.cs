#region Copyright (c) 2023-2024 SnowBank SAS, (c) 2005-2023 Doxense SAS
// All rights reserved.
// 
// Redistribution and use in source and binary forms, with or without
// modification, are permitted provided that the following conditions are met:
// 	* Redistributions of source code must retain the above copyright
// 	  notice, this list of conditions and the following disclaimer.
// 	* Redistributions in binary form must reproduce the above copyright
// 	  notice, this list of conditions and the following disclaimer in the
// 	  documentation and/or other materials provided with the distribution.
// 	* Neither the name of SnowBank nor the
// 	  names of its contributors may be used to endorse or promote products
// 	  derived from this software without specific prior written permission.
// 
// THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND
// ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
// WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
// DISCLAIMED. IN NO EVENT SHALL SNOWBANK SAS BE LIABLE FOR ANY
// DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
// (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
// LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
// ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
// (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
// SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
#endregion

namespace FoundationDB.Client
{
	using System;
	using System.Buffers;
	using System.Buffers.Binary;
	using System.Collections.Generic;
	using System.IO;
	using System.Linq;
	using System.Runtime.CompilerServices;
	using System.Runtime.InteropServices;
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

		/// <summary>Configure the options of this transaction.</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static TTransaction WithOptions<TTransaction>(this TTransaction trans, Action<IFdbTransactionOptions> configure)
			where TTransaction : IFdbReadOnlyTransaction
		{
			configure(trans.Options);
			return trans;
		}

		#endregion

		#region Get...

		/// <summary>Reads a value from the database snapshot represented by by the current transaction.</summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="key">Key to be looked up in the database</param>
		/// <returns>Task that will return the value of the key if it is found, Slice.Nil if the key does not exist, or an exception</returns>
		/// <exception cref="System.ArgumentException">If the <paramref name="key"/> is null</exception>
		/// <exception cref="System.OperationCanceledException">If the cancellation token is already triggered</exception>
		/// <exception cref="System.ObjectDisposedException">If the transaction has already been completed</exception>
		/// <exception cref="System.InvalidOperationException">If the operation method is called from the Network Thread</exception>
		public static Task<Slice> GetAsync(this IFdbReadOnlyTransaction trans, ReadOnlyMemory<byte> key)
		{
			return trans.GetAsync(key.Span);
		}

		/// <summary>Reads a value from the database snapshot represented by by the current transaction.</summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="key">Key to be looked up in the database</param>
		/// <returns>Task that will return the value of the key if it is found, Slice.Nil if the key does not exist, or an exception</returns>
		/// <exception cref="System.ArgumentException">If the <paramref name="key"/> is null</exception>
		/// <exception cref="System.OperationCanceledException">If the cancellation token is already triggered</exception>
		/// <exception cref="System.ObjectDisposedException">If the transaction has already been completed</exception>
		/// <exception cref="System.InvalidOperationException">If the operation method is called from the Network Thread</exception>
		public static Task<Slice> GetAsync(this IFdbReadOnlyTransaction trans, Slice key)
		{
			if (key.IsNull) throw Fdb.Errors.KeyCannotBeNull();
			return trans.GetAsync(key.Span);
		}

		/// <summary>Read and decode a value from the database snapshot represented by the current transaction.</summary>
		/// <typeparam name="TValue">Type of the value.</typeparam>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="key">Key to be looked up in the database</param>
		/// <param name="encoder">Encoder used to decode the value of the key.</param>
		/// <returns>Task that will return the value of the key if it is found, Slice.Nil if the key does not exist, or an exception</returns>
		/// <exception cref="System.ArgumentException">If the <paramref name="key"/> is null</exception>
		/// <exception cref="System.OperationCanceledException">If the cancellation token is already triggered</exception>
		/// <exception cref="System.ObjectDisposedException">If the transaction has already been completed</exception>
		/// <exception cref="System.InvalidOperationException">If the operation method is called from the Network Thread</exception>
		public static async Task<TValue> GetAsync<TValue>(this IFdbReadOnlyTransaction trans, ReadOnlyMemory<byte> key, IValueEncoder<TValue> encoder)
		{
			Contract.NotNull(trans);
			Contract.NotNull(encoder);

			return encoder.DecodeValue(await trans.GetAsync(key).ConfigureAwait(false))!;
		}

		/// <summary>Read and decode a value from the database snapshot represented by the current transaction.</summary>
		/// <typeparam name="TValue">Type of the value.</typeparam>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="key">Key to be looked up in the database</param>
		/// <param name="encoder">Encoder used to decode the value of the key.</param>
		/// <returns>Task that will return the value of the key if it is found, Slice.Nil if the key does not exist, or an exception</returns>
		/// <exception cref="System.ArgumentException">If the <paramref name="key"/> is null</exception>
		/// <exception cref="System.OperationCanceledException">If the cancellation token is already triggered</exception>
		/// <exception cref="System.ObjectDisposedException">If the transaction has already been completed</exception>
		/// <exception cref="System.InvalidOperationException">If the operation method is called from the Network Thread</exception>
		public static async Task<TValue> GetAsync<TValue>(this IFdbReadOnlyTransaction trans, Slice key, IValueEncoder<TValue> encoder)
		{
			Contract.NotNull(trans);
			Contract.NotNull(encoder);

			return encoder.DecodeValue(await trans.GetAsync(key).ConfigureAwait(false))!;
		}

		/// <summary>Add a read conflict range on the <c>\xff/metadataVersion</c> key</summary>
		/// <remarks>
		/// This is only required if a previous read to that key was done with snapshot isolation, to guarantee that the transaction will conflict!
		/// Please note that any external change to the key, unrelated to the current application code, could also trigger a conflict!
		/// </remarks>
		public static void ProtectAgainstMetadataVersionChange(this IFdbTransaction trans)
		{
			trans.AddReadConflictRange(Fdb.System.MetadataVersionKey, Fdb.System.MetadataVersionKeyEnd);
		}

		#endregion

		#region Set...

		/// <summary>
		/// Modify the database snapshot represented by transaction to change the given key to have the given value. If the given key was not previously present in the database it is inserted.
		/// The modification affects the actual database only if transaction is later committed with CommitAsync().
		/// </summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="key">Name of the key to be inserted into the database.</param>
		/// <param name="value">Value to be inserted into the database.</param>
		public static void Set(this IFdbTransaction trans, ReadOnlyMemory<byte> key, ReadOnlyMemory<byte> value)
		{
			trans.Set(key.Span, value.Span);
		}

		/// <summary>
		/// Modify the database snapshot represented by transaction to change the given key to have the given value. If the given key was not previously present in the database it is inserted.
		/// The modification affects the actual database only if transaction is later committed with CommitAsync().
		/// </summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="key">Name of the key to be inserted into the database.</param>
		/// <param name="value">Value to be inserted into the database.</param>
		public static void Set(this IFdbTransaction trans, Slice key, Slice value)
		{
			if (key.IsNull) throw Fdb.Errors.KeyCannotBeNull();
			if (value.IsNull) throw Fdb.Errors.ValueCannotBeNull();
			trans.Set(key.Span, value.Span);
		}

		/// <summary>
		/// Modify the database snapshot represented by transaction to change the given key to have the given value. If the given key was not previously present in the database it is inserted.
		/// The modification affects the actual database only if transaction is later committed with CommitAsync().
		/// </summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="key">Name of the key to be inserted into the database.</param>
		/// <param name="value">Value to be inserted into the database.</param>
		public static void Set(this IFdbTransaction trans, ReadOnlySpan<byte> key, Slice value)
		{
			if (value.IsNull) throw Fdb.Errors.ValueCannotBeNull();
			trans.Set(key, value.Span);
		}

		/// <summary>
		/// Modify the database snapshot represented by transaction to change the given key to have the given value. If the given key was not previously present in the database it is inserted.
		/// The modification affects the actual database only if transaction is later committed with CommitAsync().
		/// </summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="key">Name of the key to be inserted into the database.</param>
		/// <param name="value">Value to be inserted into the database.</param>
		public static void Set(this IFdbTransaction trans, Slice key, ReadOnlySpan<byte> value)
		{
			if (key.IsNull) throw Fdb.Errors.KeyCannotBeNull();
			trans.Set(key.Span, value);
		}

		/// <summary>Set the value of a key in the database, using a custom value encoder.</summary>
		/// <typeparam name="TValue">Type of the value</typeparam>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="key">Key to set</param>
		/// <param name="value">Value of the key</param>
		/// <param name="encoder">Encoder used to convert <paramref name="value"/> into a binary slice.</param>
		public static void Set<TValue>(this IFdbTransaction trans, Slice key, TValue value, IValueEncoder<TValue> encoder)
		{
			Contract.NotNull(trans);
			Contract.NotNull(encoder);
			if (key.IsNull) throw Fdb.Errors.KeyCannotBeNull();

			//TODO: "EncodeValueToBuffer" in a pooled buffer?
			trans.Set(key.Span, encoder.EncodeValue(value).Span);
		}

		/// <summary>Set the value of a key in the database, using a custom value encoder.</summary>
		/// <typeparam name="TValue">Type of the value</typeparam>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="key">Key to set</param>
		/// <param name="value">Value of the key</param>
		/// <param name="encoder">Encoder used to convert <paramref name="value"/> into a binary slice.</param>
		public static void Set<TValue>(this IFdbTransaction trans, ReadOnlyMemory<byte> key, TValue value, IValueEncoder<TValue> encoder)
		{
			Contract.NotNull(trans);
			Contract.NotNull(encoder);

			//TODO: "EncodeValueToBuffer" in a pooled buffer?
			trans.Set(key.Span, encoder.EncodeValue(value).Span);
		}

		/// <summary>Set the value of a key in the database, using the content of a Stream</summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="key">Key to set</param>
		/// <param name="data">Stream that holds the content of the key, whose length should not exceed the allowed maximum value size.</param>
		/// <remarks>This method works best with streams that do not block, like a <see cref="MemoryStream"/>. For streams that may block, consider using <see cref="SetAsync(IFdbTransaction, Slice, Stream)"/> instead.</remarks>
		public static void Set(this IFdbTransaction trans, Slice key, Stream data)
		{
			Contract.NotNull(trans);
			Contract.NotNull(data);
			if (key.IsNull) throw Fdb.Errors.KeyCannotBeNull();

			trans.EnsureCanWrite();

			var value = Slice.FromStream(data);

			trans.Set(key.Span, value.Span);
		}

		/// <summary>Set the value of a key in the database, by reading the content of a Stream asynchronously</summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="key">Key to set</param>
		/// <param name="data">Stream that holds the content of the key, whose length should not exceed the allowed maximum value size.</param>
		/// <remarks>If reading from the stream takes more than 5 seconds, the transaction will not be able to commit. For streams that are stored in memory, like a MemoryStream, consider using <see cref="Set(IFdbTransaction, Slice, Stream)"/> instead.</remarks>
		public static async Task SetAsync(this IFdbTransaction trans, ReadOnlyMemory<byte> key, Stream data)
		{
			Contract.NotNull(trans);
			Contract.NotNull(data);

			trans.EnsureCanWrite();

			var value = await Slice.FromStreamAsync(data, trans.Cancellation).ConfigureAwait(false);

			trans.Set(key.Span, value.Span);
		}

		/// <summary>Set the value of a key in the database, by reading the content of a Stream asynchronously</summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="key">Key to set</param>
		/// <param name="data">Stream that holds the content of the key, whose length should not exceed the allowed maximum value size.</param>
		/// <remarks>If reading from the stream takes more than 5 seconds, the transaction will not be able to commit. For streams that are stored in memory, like a MemoryStream, consider using <see cref="Set(IFdbTransaction, Slice, Stream)"/> instead.</remarks>
		public static async Task SetAsync(this IFdbTransaction trans, Slice key, Stream data)
		{
			Contract.NotNull(trans);
			Contract.NotNull(data);
			if (key.IsNull) throw Fdb.Errors.KeyCannotBeNull();

			trans.EnsureCanWrite();

			var value = await Slice.FromStreamAsync(data, trans.Cancellation).ConfigureAwait(false);

			trans.Set(key.Span, value.Span);
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
		public static void SetValues(this IFdbTransaction trans, KeyValuePair<Slice, Slice>[] keyValuePairs)
		{
			Contract.NotNull(trans);
			Contract.NotNull(keyValuePairs);

			foreach (var kv in keyValuePairs)
			{
				trans.Set(kv.Key, kv.Value);
			}
		}

		/// <summary>Set the values of a list of keys in the database.</summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="keyValuePairs">Array of key and value pairs</param>
		/// <remarks>
		/// Only use this method if you know that the approximate size of count of keys and values will not exceed the maximum size allowed per transaction.
		/// If the list and size of the keys and values is not known in advance, consider using a bulk operation provided by the <see cref="Fdb.Bulk"/> helper class.
		/// </remarks>
		/// <exception cref="ArgumentNullException">If either <paramref name="trans"/> or <paramref name="keyValuePairs"/> is null.</exception>
		/// <exception cref="FdbException">If this operation would exceed the maximum allowed size for a transaction.</exception>
		public static void SetValues(this IFdbTransaction trans, ReadOnlySpan<KeyValuePair<Slice, Slice>> keyValuePairs)
		{
			Contract.NotNull(trans);

			for(int i = 0; i < keyValuePairs.Length; i++)
			{
				Set(trans, keyValuePairs[i].Key, keyValuePairs[i].Value);
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
		public static void SetValues(this IFdbTransaction trans, Slice[] keys, Slice[] values)
		{
			Contract.NotNull(trans);
			Contract.NotNull(keys);
			Contract.NotNull(values);

			trans.SetValues(keys.AsSpan(), values.AsSpan());
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
		public static void SetValues(this IFdbTransaction trans, ReadOnlySpan<Slice> keys, ReadOnlySpan<Slice> values)
		{
			Contract.NotNull(trans);
			if (values.Length != keys.Length) throw new ArgumentException("Both key and value arrays must have the same size.", nameof(values));

			for (int i = 0; i < keys.Length; i++)
			{
				Set(trans, keys[i], values[i]);
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
		public static void SetValues(this IFdbTransaction trans, IEnumerable<KeyValuePair<Slice, Slice>> keyValuePairs)
		{
			Contract.NotNull(trans);
			Contract.NotNull(keyValuePairs);

			if (keyValuePairs is KeyValuePair<Slice, Slice>[] arr)
			{
				SetValues(trans, arr.AsSpan());
			}
			else if (keyValuePairs is List<KeyValuePair<Slice, Slice>> lst)
			{
				SetValues(trans, CollectionsMarshal.AsSpan(lst));
			}
			else if (keyValuePairs is Dictionary<Slice, Slice> dict)
			{
				foreach (var kv in dict)
				{
					Set(trans, kv.Key, kv.Value);
				}
			}
			else
			{
				foreach (var kv in keyValuePairs)
				{
					Set(trans, kv.Key, kv.Value);
				}
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
		public static void SetValues(this IFdbTransaction trans, IEnumerable<Slice> keys, IEnumerable<Slice> values)
		{
			Contract.NotNull(trans);
			Contract.NotNull(keys);
			Contract.NotNull(values);

			// attempt to extract a Span from both keys & values
			// if we fail, then fallback to a slow enumeration

			ReadOnlySpan<Slice> k;
			switch (keys)
			{
				case Slice[] arr:
				{
					k = arr.AsSpan();
					break;
				}
				case List<Slice> lst:
				{
					k = CollectionsMarshal.AsSpan(lst);
					break;
				}
				default:
				{
					SetValuesSlow(trans, keys, values);
					return;
				}
			}

			switch (values)
			{
				case Slice[] arr:
				{
					SetValues(trans, k, arr.AsSpan());
					break;
				}
				case List<Slice> lst:
				{
					SetValues(trans, k, CollectionsMarshal.AsSpan(lst));
					break;
				}
				default:
				{
					SetValuesSlow(trans, keys, values);
					break;
				}
			}


			static void SetValuesSlow(IFdbTransaction trans, IEnumerable<Slice> keys, IEnumerable<Slice> values)
			{
				using(var keyIter = keys.GetEnumerator())
				using(var valueIter = values.GetEnumerator())
				{
					while(keyIter.MoveNext())
					{
						if (!valueIter.MoveNext())
						{
							throw new ArgumentException("Both key and value sequences must have the same size.", nameof(values));
						}

						Set(trans, keyIter.Current, valueIter.Current);
					}
					if (valueIter.MoveNext())
					{
						throw new ArgumentException("Both key and values sequences must have the same size.", nameof(values));
					}
				}


			}

		}

		#endregion

		#region Atomic Ops...

		/// <summary>Modify the database snapshot represented by this transaction to perform the operation indicated by <paramref name="mutation"/> with operand <paramref name="param"/> to the value stored by the given key.</summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="key">Name of the key whose value is to be mutated.</param>
		/// <param name="param">Parameter with which the atomic operation will mutate the value associated with key_name.</param>
		/// <param name="mutation">Type of mutation that should be performed on the key</param>
		public static void Atomic(this IFdbTransaction trans, Slice key, Slice param, FdbMutationType mutation)
		{
			if (key.IsNull) throw Fdb.Errors.KeyCannotBeNull();
			trans.Atomic(key.Span, param.Span, mutation);
		}

		/// <summary>Modify the database snapshot represented by this transaction to add the value of <paramref name="value"/> to the value stored by the given <paramref name="key"/>.</summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="key">Name of the key whose value is to be mutated.</param>
		/// <param name="value">Value to add to existing value of key.</param>
		public static void AtomicAdd(this IFdbTransaction trans, Slice key, Slice value)
		{
			trans.Atomic(key, value, FdbMutationType.Add);
		}

		/// <summary>Modify the database snapshot represented by this transaction to add the value of <paramref name="value"/> to the value stored by the given <paramref name="key"/>.</summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="key">Name of the key whose value is to be mutated.</param>
		/// <param name="value">Value to add to existing value of key.</param>
		public static void AtomicAdd(this IFdbTransaction trans, ReadOnlySpan<byte> key, ReadOnlySpan<byte> value)
		{
			trans.Atomic(key, value, FdbMutationType.Add);
		}

		/// <summary>Modify the database snapshot represented by this transaction to clear the value of <paramref name="key"/> only if it is equal to <paramref name="comparand"/>.</summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="key">Name of the key whose value is to be cleared.</param>
		/// <param name="comparand">Value that the key must have, in order to be cleared.</param>
		/// <remarks>
		/// If the <paramref name="key"/> does not exist, or has a different value than <paramref name="comparand"/> then no changes will be performed.
		/// This method requires API version 610 or greater.
		/// </remarks>
		public static void AtomicCompareAndClear(this IFdbTransaction trans, Slice key, Slice comparand)
		{
			trans.Atomic(key, comparand, FdbMutationType.CompareAndClear);
		}

		/// <summary>Modify the database snapshot represented by this transaction to clear the value of <paramref name="key"/> only if it is equal to <paramref name="comparand"/>.</summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="key">Name of the key whose value is to be cleared.</param>
		/// <param name="comparand">Value that the key must have, in order to be cleared.</param>
		/// <remarks>
		/// If the <paramref name="key"/> does not exist, or has a different value than <paramref name="comparand"/> then no changes will be performed.
		/// This method requires API version 610 or greater.
		/// </remarks>
		public static void AtomicCompareAndClear(this IFdbTransaction trans, ReadOnlySpan<byte> key, ReadOnlySpan<byte> comparand)
		{
			trans.Atomic(key, comparand, FdbMutationType.CompareAndClear);
		}

		/// <summary>Cached 0 (32-bits)</summary>
		private static readonly Slice Zero32 = Slice.FromFixed32(0);

		/// <summary>Cached 0 (64-bits)</summary>
		private static readonly Slice Zero64 = Slice.FromFixed64(0);

		/// <summary>Atomically clear the key only if its value is equal to 4 consecutive zero bytes.</summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="key">Name of the key whose value is to be conditionally cleared.</param>
		/// <remarks>This method requires API version 610 or greater.</remarks>
		public static void AtomicClearIfZero32(this IFdbTransaction trans, Slice key)
		{
			trans.Atomic(key, Zero32, FdbMutationType.CompareAndClear);
		}

		/// <summary>Atomically clear the key only if its value is equal to 4 consecutive zero bytes.</summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="key">Name of the key whose value is to be conditionally cleared.</param>
		/// <remarks>This method requires API version 610 or greater.</remarks>
		public static void AtomicClearIfZero32(this IFdbTransaction trans, ReadOnlySpan<byte> key)
		{
			trans.Atomic(key, Zero32.Span, FdbMutationType.CompareAndClear);
		}

		/// <summary>Atomically clear the key only if its value is equal to 8 consecutive zero bytes.</summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="key">Name of the key whose value is to be conditionally cleared.</param>
		/// <remarks>This method requires API version 610 or greater.</remarks>
		public static void AtomicClearIfZero64(this IFdbTransaction trans, Slice key)
		{
			trans.Atomic(key, Zero64, FdbMutationType.CompareAndClear);
		}

		/// <summary>Atomically clear the key only if its value is equal to 8 consecutive zero bytes.</summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="key">Name of the key whose value is to be conditionally cleared.</param>
		/// <remarks>This method requires API version 610 or greater.</remarks>
		public static void AtomicClearIfZero64(this IFdbTransaction trans, ReadOnlySpan<byte> key)
		{
			trans.Atomic(key, Zero64.Span, FdbMutationType.CompareAndClear);
		}

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
		public static void AtomicIncrement32(this IFdbTransaction trans, Slice key)
		{
			trans.Atomic(key, PlusOne32, FdbMutationType.Add);
		}

		/// <summary>Modify the database snapshot represented by this transaction to increment by one the 32-bit value stored by the given <paramref name="key"/>.</summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="key">Name of the key whose value is to be mutated.</param>
		public static void AtomicIncrement32(this IFdbTransaction trans, ReadOnlySpan<byte> key)
		{
			trans.Atomic(key, PlusOne32.Span, FdbMutationType.Add);
		}

		/// <summary>Modify the database snapshot represented by this transaction to subtract 1 from the 32-bit value stored by the given <paramref name="key"/>.</summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="key">Name of the key whose value is to be mutated.</param>
		public static void AtomicDecrement32(this IFdbTransaction trans, Slice key)
		{
			trans.Atomic(key, MinusOne32, FdbMutationType.Add);
		}

		/// <summary>Modify the database snapshot represented by this transaction to subtract 1 from the 32-bit value stored by the given <paramref name="key"/>.</summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="key">Name of the key whose value is to be mutated.</param>
		public static void AtomicDecrement32(this IFdbTransaction trans, ReadOnlySpan<byte> key)
		{
			trans.Atomic(key, MinusOne32.Span, FdbMutationType.Add);
		}

		/// <summary>Modify the database snapshot represented by this transaction to subtract 1 from the 32-bit value stored by the given <paramref name="key"/>.</summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="key">Name of the key whose value is to be mutated.</param>
		/// <param name="clearIfZero">If <see langword="true"/>, automatically clear the key if it reaches zero. If <see langword="false"/>, the key can remain with a value of 0 in the database.</param>
		/// <remarks>This method requires API version 610 or greater.</remarks>
		public static void AtomicDecrement32(this IFdbTransaction trans, Slice key, bool clearIfZero)
		{
			trans.Atomic(key, MinusOne32, FdbMutationType.Add);
			if (clearIfZero)
			{
				trans.Atomic(key, Zero32, FdbMutationType.CompareAndClear);
			}
		}

		/// <summary>Modify the database snapshot represented by this transaction to subtract 1 from the 32-bit value stored by the given <paramref name="key"/>.</summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="key">Name of the key whose value is to be mutated.</param>
		/// <param name="clearIfZero">If <see langword="true"/>, automatically clear the key if it reaches zero. If <see langword="false"/>, the key can remain with a value of 0 in the database.</param>
		/// <remarks>This method requires API version 610 or greater.</remarks>
		public static void AtomicDecrement32(this IFdbTransaction trans, ReadOnlySpan<byte> key, bool clearIfZero)
		{
			trans.Atomic(key, MinusOne32.Span, FdbMutationType.Add);
			if (clearIfZero)
			{
				trans.Atomic(key, Zero32.Span, FdbMutationType.CompareAndClear);
			}
		}

		/// <summary>Modify the database snapshot represented by this transaction to add 1 to the 64-bit value stored by the given <paramref name="key"/>.</summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="key">Name of the key whose value is to be mutated.</param>
		public static void AtomicIncrement64(this IFdbTransaction trans, Slice key)
		{
			trans.Atomic(key, PlusOne64, FdbMutationType.Add);
		}

		/// <summary>Modify the database snapshot represented by this transaction to add 1 to the 64-bit value stored by the given <paramref name="key"/>.</summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="key">Name of the key whose value is to be mutated.</param>
		public static void AtomicIncrement64(this IFdbTransaction trans, ReadOnlySpan<byte> key)
		{
			trans.Atomic(key, PlusOne64.Span, FdbMutationType.Add);
		}

		/// <summary>Modify the database snapshot represented by this transaction to subtract 1 from the 64-bit value stored by the given <paramref name="key"/>.</summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="key">Name of the key whose value is to be mutated.</param>
		public static void AtomicDecrement64(this IFdbTransaction trans, Slice key)
		{
			trans.Atomic(key, MinusOne64, FdbMutationType.Add);
		}

		/// <summary>Modify the database snapshot represented by this transaction to subtract 1 from the 64-bit value stored by the given <paramref name="key"/>.</summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="key">Name of the key whose value is to be mutated.</param>
		public static void AtomicDecrement64(this IFdbTransaction trans, ReadOnlySpan<byte> key)
		{
			trans.Atomic(key, MinusOne64.Span, FdbMutationType.Add);
		}

		/// <summary>Modify the database snapshot represented by this transaction to subtract 1 from the 64-bit value stored by the given <paramref name="key"/>.</summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="key">Name of the key whose value is to be mutated.</param>
		/// <param name="clearIfZero">If <see langword="true"/>, automatically clear the key if it reaches zero. If <see langword="false"/>, the key can remain with a value of 0 in the database.</param>
		/// <remarks>This method requires API version 610 or greater.</remarks>
		public static void AtomicDecrement64(this IFdbTransaction trans, Slice key, bool clearIfZero)
		{
			trans.Atomic(key, MinusOne64, FdbMutationType.Add);
			if (clearIfZero)
			{
				trans.Atomic(key, Zero64, FdbMutationType.CompareAndClear);
			}
		}

		/// <summary>Modify the database snapshot represented by this transaction to subtract 1 from the 64-bit value stored by the given <paramref name="key"/>.</summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="key">Name of the key whose value is to be mutated.</param>
		/// <param name="clearIfZero">If <see langword="true"/>, automatically clear the key if it reaches zero. If <see langword="false"/>, the key can remain with a value of 0 in the database.</param>
		/// <remarks>This method requires API version 610 or greater.</remarks>
		public static void AtomicDecrement64(this IFdbTransaction trans, ReadOnlySpan<byte> key, bool clearIfZero)
		{
			trans.Atomic(key, MinusOne64.Span, FdbMutationType.Add);
			if (clearIfZero)
			{
				trans.Atomic(key, Zero64.Span, FdbMutationType.CompareAndClear);
			}
		}

		/// <summary>Modify the database snapshot represented by this transaction to add a signed integer to the 32-bit value stored by the given <paramref name="key"/>.</summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="key">Name of the key whose value is to be mutated.</param>
		/// <param name="value">Integer add to existing value of key. It will encoded as 4 bytes in high-endian.</param>
		public static void AtomicAdd32(this IFdbTransaction trans, Slice key, int value)
		{
			if (key.IsNull) throw Fdb.Errors.KeyCannotBeNull();
			trans.AtomicAdd32(key.Span, value);
		}

		/// <summary>Modify the database snapshot represented by this transaction to add a signed integer to the 32-bit value stored by the given <paramref name="key"/>.</summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="key">Name of the key whose value is to be mutated.</param>
		/// <param name="value">Integer add to existing value of key. It will encoded as 4 bytes in high-endian.</param>
		public static void AtomicAdd32(this IFdbTransaction trans, ReadOnlySpan<byte> key, int value)
		{
			if (value == 1)
			{
				trans.Atomic(key, PlusOne32.Span, FdbMutationType.Add);
			}
			else if (value == -1)
			{
				trans.Atomic(key, MinusOne32.Span, FdbMutationType.Add);
			}
			else
			{
				Span<byte> tmp = stackalloc byte[4];
				BinaryPrimitives.WriteInt32LittleEndian(tmp, value);
				trans.Atomic(key, tmp, FdbMutationType.Add);
			}
		}

		/// <summary>Modify the database snapshot represented by this transaction to add an unsigned integer to the 32-bit value stored by the given <paramref name="key"/>.</summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="key">Name of the key whose value is to be mutated.</param>
		/// <param name="value">Integer add to existing value of key. It will encoded as 4 bytes in high-endian.</param>
		public static void AtomicAdd32(this IFdbTransaction trans, Slice key, uint value)
		{
			if (key.IsNull) throw Fdb.Errors.KeyCannotBeNull();
			trans.AtomicAdd32(key.Span, value);
		}

		/// <summary>Modify the database snapshot represented by this transaction to add an unsigned integer to the 32-bit value stored by the given <paramref name="key"/>.</summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="key">Name of the key whose value is to be mutated.</param>
		/// <param name="value">Integer add to existing value of key. It will encoded as 4 bytes in high-endian.</param>
		public static void AtomicAdd32(this IFdbTransaction trans, ReadOnlySpan<byte> key, uint value)
		{
			if (value == 1)
			{
				trans.Atomic(key, PlusOne32.Span, FdbMutationType.Add);
			}
			else if (value == uint.MaxValue)
			{
				trans.Atomic(key, MinusOne32.Span, FdbMutationType.Add);
			}
			else
			{
				Span<byte> tmp = stackalloc byte[4];
				BinaryPrimitives.WriteUInt32LittleEndian(tmp, value);
				trans.Atomic(key, tmp, FdbMutationType.Add);
			}
		}

		/// <summary>Modify the database snapshot represented by this transaction to add a signed integer to the 64-bit value stored by the given <paramref name="key"/>.</summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="key">Name of the key whose value is to be mutated.</param>
		/// <param name="value">Integer add to existing value of key. It will encoded as 8 bytes in high-endian.</param>
		public static void AtomicAdd64(this IFdbTransaction trans, Slice key, long value)
		{
			if (key.IsNull) throw Fdb.Errors.KeyCannotBeNull();
			trans.AtomicAdd64(key.Span, value);
		}

		/// <summary>Modify the database snapshot represented by this transaction to add a signed integer to the 64-bit value stored by the given <paramref name="key"/>.</summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="key">Name of the key whose value is to be mutated.</param>
		/// <param name="value">Integer add to existing value of key. It will encoded as 8 bytes in high-endian.</param>
		public static void AtomicAdd64(this IFdbTransaction trans, ReadOnlySpan<byte> key, long value)
		{
			if (value == 1)
			{
				trans.Atomic(key, PlusOne64.Span, FdbMutationType.Add);
			}
			else if (value == -1)
			{
				trans.Atomic(key, MinusOne64.Span, FdbMutationType.Add);
			}
			else
			{
				Span<byte> tmp = stackalloc byte[8];
				BinaryPrimitives.WriteInt64LittleEndian(tmp, value);
				trans.Atomic(key, tmp, FdbMutationType.Add);
			}
		}

		/// <summary>Modify the database snapshot represented by this transaction to add an unsigned integer to the 64-bit value stored by the given <paramref name="key"/>.</summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="key">Name of the key whose value is to be mutated.</param>
		/// <param name="value">Integer add to existing value of key. It will encoded as 8 bytes in high-endian.</param>
		public static void AtomicAdd64(this IFdbTransaction trans, Slice key, ulong value)
		{
			if (key.IsNull) throw Fdb.Errors.KeyCannotBeNull();
			trans.AtomicAdd64(key.Span, value);
		}

		/// <summary>Modify the database snapshot represented by this transaction to add an unsigned integer to the 64-bit value stored by the given <paramref name="key"/>.</summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="key">Name of the key whose value is to be mutated.</param>
		/// <param name="value">Integer add to existing value of key. It will encoded as 8 bytes in high-endian.</param>
		public static void AtomicAdd64(this IFdbTransaction trans, ReadOnlySpan<byte> key, ulong value)
		{
			if (value == 1)
			{
				trans.Atomic(key, PlusOne64.Span, FdbMutationType.Add);
			}
			else if (value == ulong.MaxValue)
			{
				trans.Atomic(key, MinusOne64.Span, FdbMutationType.Add);
			}
			else
			{
				Span<byte> tmp = stackalloc byte[8];
				BinaryPrimitives.WriteUInt64LittleEndian(tmp, value);
				trans.Atomic(key, tmp, FdbMutationType.Add);
			}
		}

		/// <summary>Modify the database snapshot represented by this transaction to perform a bitwise AND between <paramref name="mask"/> and the value stored by the given <paramref name="key"/>.</summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="key">Name of the key whose value is to be mutated.</param>
		/// <param name="mask">Bit mask.</param>
		public static void AtomicAnd(this IFdbTransaction trans, Slice key, Slice mask)
		{
			if (key.IsNull) throw Fdb.Errors.KeyCannotBeNull();

			//TODO: rename this to AtomicBitAnd(...) ?
			trans.Atomic(key, mask, FdbMutationType.BitAnd);
		}

		/// <summary>Modify the database snapshot represented by this transaction to perform a bitwise OR between <paramref name="mask"/> and the value stored by the given <paramref name="key"/>.</summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="key">Name of the key whose value is to be mutated.</param>
		/// <param name="mask">Bit mask.</param>
		public static void AtomicOr(this IFdbTransaction trans, Slice key, Slice mask)
		{
			//TODO: rename this to AtomicBitOr(...) ?
			trans.Atomic(key, mask, FdbMutationType.BitOr);
		}

		/// <summary>Modify the database snapshot represented by this transaction to perform a bitwise OR between <paramref name="mask"/> and the value stored by the given <paramref name="key"/>.</summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="key">Name of the key whose value is to be mutated.</param>
		/// <param name="mask">Bit mask.</param>
		public static void AtomicOr(this IFdbTransaction trans, ReadOnlySpan<byte> key, ReadOnlySpan<byte> mask)
		{
			//TODO: rename this to AtomicBitOr(...) ?
			trans.Atomic(key, mask, FdbMutationType.BitOr);
		}

		/// <summary>Modify the database snapshot represented by this transaction to perform a bitwise XOR between <paramref name="mask"/> and the value stored by the given <paramref name="key"/>.</summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="key">Name of the key whose value is to be mutated.</param>
		/// <param name="mask">Bit mask.</param>
		public static void AtomicXor(this IFdbTransaction trans, Slice key, Slice mask)
		{
			//TODO: rename this to AtomicBitXOr(...) ?
			trans.Atomic(key, mask, FdbMutationType.BitXor);
		}

		/// <summary>Modify the database snapshot represented by this transaction to perform a bitwise XOR between <paramref name="mask"/> and the value stored by the given <paramref name="key"/>.</summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="key">Name of the key whose value is to be mutated.</param>
		/// <param name="mask">Bit mask.</param>
		public static void AtomicXor(this IFdbTransaction trans, ReadOnlySpan<byte> key, ReadOnlySpan<byte> mask)
		{
			//TODO: rename this to AtomicBitXOr(...) ?
			trans.Atomic(key, mask, FdbMutationType.BitXor);
		}

		/// <summary>Modify the database snapshot represented by this transaction to append to a value, unless it would become larger that the maximum value size supported by the database.</summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="key">Name of the key whose value is to be mutated.</param>
		/// <param name="value">Value to append.</param>
		public static void AtomicAppendIfFits(this IFdbTransaction trans, Slice key, Slice value)
		{
			trans.Atomic(key, value, FdbMutationType.AppendIfFits);
		}

		/// <summary>Modify the database snapshot represented by this transaction to append to a value, unless it would become larger that the maximum value size supported by the database.</summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="key">Name of the key whose value is to be mutated.</param>
		/// <param name="value">Value to append.</param>
		public static void AtomicAppendIfFits(this IFdbTransaction trans, ReadOnlySpan<byte> key, ReadOnlySpan<byte> value)
		{
			trans.Atomic(key, value, FdbMutationType.AppendIfFits);
		}

		/// <summary>Modify the database snapshot represented by this transaction to update a value if it is larger than the value in the database.</summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="key">Name of the key whose value is to be mutated.</param>
		/// <param name="value">Bit mask.</param>
		public static void AtomicMax(this IFdbTransaction trans, Slice key, Slice value)
		{
			trans.Atomic(key, value, FdbMutationType.Max);
		}

		/// <summary>Modify the database snapshot represented by this transaction to update a value if it is larger than the value in the database.</summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="key">Name of the key whose value is to be mutated.</param>
		/// <param name="value">Bit mask.</param>
		public static void AtomicMax(this IFdbTransaction trans, ReadOnlySpan<byte> key, ReadOnlySpan<byte> value)
		{
			trans.Atomic(key, value, FdbMutationType.Max);
		}

		/// <summary>Modify the database snapshot represented by this transaction to update a value if it is smaller than the value in the database.</summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="key">Name of the key whose value is to be mutated.</param>
		/// <param name="value">Bit mask.</param>
		public static void AtomicMin(this IFdbTransaction trans, Slice key, Slice value)
		{
			trans.Atomic(key, value, FdbMutationType.Min);
		}

		/// <summary>Modify the database snapshot represented by this transaction to update a value if it is smaller than the value in the database.</summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="key">Name of the key whose value is to be mutated.</param>
		/// <param name="value">Bit mask.</param>
		public static void AtomicMin(this IFdbTransaction trans, ReadOnlySpan<byte> key, ReadOnlySpan<byte> value)
		{
			trans.Atomic(key, value, FdbMutationType.Min);
		}

		/// <summary>Find the location of the VersionStamp in a key or value</summary>
		/// <param name="buffer">Buffer that must contains <paramref name="token"/> once and only once</param>
		/// <param name="token">Token that represents the VersionStamp</param>
		/// <param name="argName"></param>
		/// <returns>Offset in <paramref name="buffer"/> where the stamp was found</returns>
		private static int GetVersionStampOffset(ReadOnlySpan<byte> buffer, ReadOnlySpan<byte> token, string argName)
		{
			// the buffer MUST contain one incomplete stamp, either the random token of the current transaction or the default token (all-FF)

			if (buffer.Length < token.Length) throw new ArgumentException("The key is too small to contain a VersionStamp.", argName);

			int p = token.Length != 0 ? buffer.IndexOf(token) : -1;
			if (p >= 0)
			{ // found a candidate spot, we have to make sure that it is only present once in the key!

				if (buffer[(p + token.Length)..].IndexOf(token) >= 0)
				{
					throw argName == "key"
						? new ArgumentException("The key should only contain one occurrence of a VersionStamp.", argName)
						: new ArgumentException("The value should only contain one occurrence of a VersionStamp.", argName);
				}
			}
			else
			{ // not found, maybe it is using the default incomplete stamp (all FF) ?
				p = buffer.IndexOf(VersionStamp.IncompleteToken.Span);
				if (p < 0)
				{
					throw argName == "key"
						? new ArgumentException("The key should contain at least one VersionStamp.", argName)
						: new ArgumentException("The value should contain at least one VersionStamp.", argName);
				}
			}
			Contract.Debug.Assert(p >= 0 && p + token.Length <= buffer.Length);

			return p;
		}

		[Pure, MethodImpl(MethodImplOptions.NoInlining)]
		private static NotSupportedException FailVersionStampNotSupported(int apiVersion)
		{
			return new NotSupportedException($"VersionStamps are not supported at API version {apiVersion}. You need to select at least API Version 400 or above.");
		}

		/// <summary>Set the <paramref name="value"/> of the <paramref name="key"/> in the database, with the <see cref="VersionStamp"/> replaced by the resolved version at commit time.</summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="key">Name of the key whose value is to be mutated. This key must contain a single <see cref="VersionStamp"/>, whose position will be automatically detected.</param>
		/// <param name="value">New value for this key.</param>
		public static void SetVersionStampedKey(this IFdbTransaction trans, Slice key, Slice value)
		{
			if (key.IsNull) throw Fdb.Errors.KeyCannotBeNull();
			if (value.IsNull) throw Fdb.Errors.ValueCannotBeNull();

			SetVersionStampedKey(trans, key.Span, value.Span);
		}

		/// <summary>Set the <paramref name="value"/> of the <paramref name="key"/> in the database, with the <see cref="VersionStamp"/> replaced by the resolved version at commit time.</summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="key">Name of the key whose value is to be mutated. This key must contain a single <see cref="VersionStamp"/>, whose position will be automatically detected.</param>
		/// <param name="value">New value for this key.</param>
		public static void SetVersionStampedKey(this IFdbTransaction trans, ReadOnlySpan<byte> key, ReadOnlySpan<byte> value)
		{
			Contract.NotNull(trans);

			Span<byte> token = stackalloc byte[10];
			trans.CreateVersionStamp().WriteTo(token);
			var offset = GetVersionStampOffset(key, token, nameof(key));

			int apiVer = trans.Context.Database.GetApiVersion();
			if (apiVer < 400)
			{ // introduced in 400
				throw FailVersionStampNotSupported(apiVer);
			}

			if (apiVer < 520)
			{ // prior to 520, the offset is only 16-bits
				var writer = new SliceWriter(key.Length + 2, ArrayPool<byte>.Shared);
				writer.WriteBytes(key);
				writer.WriteFixed16(checked((ushort) offset)); // 16-bits little endian
				trans.Atomic(writer.ToSpan(), value, FdbMutationType.VersionStampedKey);
				writer.Release();
			}
			else
			{ // starting from 520, the offset is 32 bits
				var writer = new SliceWriter(key.Length + 4, ArrayPool<byte>.Shared);
				writer.WriteBytes(key);
				writer.WriteFixed32(checked((uint) offset)); // 32-bits little endian
				trans.Atomic(writer.ToSpan(), value, FdbMutationType.VersionStampedKey);
				writer.Release();
			}

		}

		/// <summary>Set the <paramref name="value"/> of the <paramref name="key"/> in the database, with the <see cref="VersionStamp"/> replaced by the resolved version at commit time.</summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="key">Name of the key whose value is to be mutated. This key must contain a single <see cref="VersionStamp"/>, whose start is defined by <paramref name="stampOffset"/>.</param>
		/// <param name="stampOffset">Offset in <paramref name="key"/> where the 80-bit VersionStamp is located.</param>
		/// <param name="value">New value for this key.</param>
		public static void SetVersionStampedKey(this IFdbTransaction trans, Slice key, int stampOffset, Slice value)
		{
			if (key.IsNull) throw Fdb.Errors.KeyCannotBeNull();
			if (value.IsNull) throw Fdb.Errors.ValueCannotBeNull();

			SetVersionStampedKey(trans, key.Span, stampOffset, value.Span);
		}

		/// <summary>Set the <paramref name="value"/> of the <paramref name="key"/> in the database, with the <see cref="VersionStamp"/> replaced by the resolved version at commit time.</summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="key">Name of the key whose value is to be mutated. This key must contain a single <see cref="VersionStamp"/>, whose start is defined by <paramref name="stampOffset"/>.</param>
		/// <param name="stampOffset">Offset in <paramref name="key"/> where the 80-bit VersionStamp is located.</param>
		/// <param name="value">New value for this key.</param>
		public static void SetVersionStampedKey(this IFdbTransaction trans, ReadOnlySpan<byte> key, int stampOffset, ReadOnlySpan<byte> value)
		{
			Contract.NotNull(trans);
			Contract.Positive(stampOffset);
			if (stampOffset > key.Length - 10) throw new ArgumentException("The VersionStamp overflows past the end of the key.", nameof(stampOffset));

			int apiVer = trans.Context.GetApiVersion();
			if (apiVer < 400)
			{ // introduced in 400
				throw FailVersionStampNotSupported(apiVer);
			}

			if (apiVer < 520)
			{ // prior to 520, the offset is only 16-bits
				if (stampOffset > 0xFFFF) throw new ArgumentException("The offset is too large to fit within 16-bits.");
				var writer = new SliceWriter(key.Length + 2, ArrayPool<byte>.Shared);
				writer.WriteBytes(key);
				writer.WriteFixed16(checked((ushort) stampOffset)); //stored as 32-bits in Little Endian
				trans.Atomic(writer.ToSpan(), value, FdbMutationType.VersionStampedKey);
				writer.Release();
			}
			else
			{ // starting from 520, the offset is 32 bits
				var writer = new SliceWriter(key.Length + 4, ArrayPool<byte>.Shared);
				writer.WriteBytes(key);
				writer.WriteFixed32(checked((uint) stampOffset)); //stored as 32-bits in Little Endian
				trans.Atomic(writer.ToSpan(), value, FdbMutationType.VersionStampedKey);
				writer.Release();
			}
		}

		/// <summary>Set the <paramref name="value"/> of the <paramref name="key"/> in the database, with the first 10 bytes overwritten with the transaction's <see cref="VersionStamp"/>.</summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="key">Name of the key whose value is to be mutated.</param>
		/// <param name="value">Value whose first 10 bytes will be overwritten by the database with the resolved VersionStamp at commit time. The rest of the value will be untouched.</param>
		public static void SetVersionStampedValue(this IFdbTransaction trans, Slice key, Slice value)
		{
			if (key.IsNull) throw Fdb.Errors.KeyCannotBeNull();
			if (value.IsNull) throw Fdb.Errors.ValueCannotBeNull();

			SetVersionStampedValue(trans, key.Span, value.Span);
		}

		/// <summary>Set the <paramref name="value"/> of the <paramref name="key"/> in the database, with the first 10 bytes overwritten with the transaction's <see cref="VersionStamp"/>.</summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="key">Name of the key whose value is to be mutated.</param>
		/// <param name="value">Value whose first 10 bytes will be overwritten by the database with the resolved VersionStamp at commit time. The rest of the value will be untouched.</param>
		public static void SetVersionStampedValue(this IFdbTransaction trans, ReadOnlySpan<byte> key, ReadOnlySpan<byte> value)
		{
			Contract.NotNull(trans);
			if (value.Length < 10) throw new ArgumentException("The value must be at least 10 bytes long.", nameof(value));

			int apiVer = trans.Context.GetApiVersion();
			if (apiVer < 400)
			{ // introduced in 400
				throw FailVersionStampNotSupported(apiVer);
			}

			if (apiVer < 520)
			{ // prior to 520, the stamp is always at offset 0
				trans.Atomic(key, value, FdbMutationType.VersionStampedValue);
			}
			else
			{ // starting from 520, the offset is stored in the last 32 bits

				int offset;
				{
					Span<byte> token = stackalloc byte[10];
					trans.CreateVersionStamp().WriteTo(token);
					offset = GetVersionStampOffset(value, token, nameof(value));
				}
				Contract.Debug.Requires(offset >=0 && offset <= value.Length - 10);

				var writer = new SliceWriter(value.Length + 4, ArrayPool<byte>.Shared);
				writer.WriteBytes(value);
				writer.WriteFixed32(checked((uint) offset));
				trans.Atomic(key, writer.ToSpan(), FdbMutationType.VersionStampedValue);
				writer.Release();
			}
		}

		/// <summary>Set the <paramref name="value"/> of the <paramref name="key"/> in the database, with the first 10 bytes overwritten with the transaction's <see cref="VersionStamp"/>.</summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="key">Name of the key whose value is to be mutated.</param>
		/// <param name="value">Value of the key. The 10 bytes starting at <paramref name="stampOffset"/> will be overwritten by the database with the resolved VersionStamp at commit time. The rest of the value will be untouched.</param>
		/// <param name="stampOffset">Offset in <paramref name="value"/> where the 80-bit VersionStamp is located. Prior to API version 520, it can only be located at offset 0.</param>
		public static void SetVersionStampedValue(this IFdbTransaction trans, Slice key, Slice value, int stampOffset)
		{
			if (key.IsNull) throw Fdb.Errors.KeyCannotBeNull();
			if (value.IsNull) throw Fdb.Errors.ValueCannotBeNull();

			SetVersionStampedValue(trans, key.Span, value.Span, stampOffset);
		}

		/// <summary>Set the <paramref name="value"/> of the <paramref name="key"/> in the database, with the first 10 bytes overwritten with the transaction's <see cref="VersionStamp"/>.</summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="key">Name of the key whose value is to be mutated.</param>
		/// <param name="value">Value of the key. The 10 bytes starting at <paramref name="stampOffset"/> will be overwritten by the database with the resolved VersionStamp at commit time. The rest of the value will be untouched.</param>
		/// <param name="stampOffset">Offset in <paramref name="value"/> where the 80-bit VersionStamp is located. Prior to API version 520, it can only be located at offset 0.</param>
		public static void SetVersionStampedValue(this IFdbTransaction trans, ReadOnlySpan<byte> key, ReadOnlySpan<byte> value, int stampOffset)
		{
			Contract.NotNull(trans);
			Contract.Positive(stampOffset);
			if (stampOffset > key.Length - 10) throw new ArgumentException("The VersionStamp overflows past the end of the value.", nameof(stampOffset));

			int apiVer = trans.Context.GetApiVersion();
			if (apiVer < 400)
			{ // introduced in 400
				throw FailVersionStampNotSupported(apiVer);
			}

			if (apiVer < 520)
			{ // prior to 520, the stamp is always at offset 0
				if (stampOffset != 0) throw new InvalidOperationException("Prior to API version 520, the VersionStamp can only be located at offset 0. Please update to API Version 520 or above!");
				// let it slide!
				trans.Atomic(key, value, FdbMutationType.VersionStampedValue);
			}
			else
			{ // starting from 520, the offset is stored in the last 32 bits
				var writer = new SliceWriter(value.Length + 4, ArrayPool<byte>.Shared);
				writer.WriteBytes(value);
				writer.WriteFixed32(checked((uint) stampOffset));
				trans.Atomic(key, writer.ToSpan(), FdbMutationType.VersionStampedValue);
				writer.Release();
			}

		}

		#endregion

		#region GetRange...

		/// <summary>Create a new range query that will read all key-value pairs in the database snapshot represented by the transaction</summary>
		[Obsolete("Use the overload that takes an FdbRangeOptions argument, or use LINQ to configure the query!")]
		public static FdbRangeQuery<KeyValuePair<Slice, Slice>> GetRange(this IFdbReadOnlyTransaction trans, KeySelector beginInclusive, KeySelector endExclusive, int limit, bool reverse = false)
		{
			Contract.NotNull(trans);

			return trans.GetRange(beginInclusive, endExclusive, new FdbRangeOptions(limit: limit, reverse: reverse));
		}

		/// <summary>Create a new range query that will read all key-value pairs in the database snapshot represented by the transaction</summary>
		public static FdbRangeQuery<KeyValuePair<Slice, Slice>> GetRange(this IFdbReadOnlyTransaction trans, KeyRange range, FdbRangeOptions? options = null)
		{
			var sp = KeySelectorPair.Create(range);
			return trans.GetRange(sp.Begin, sp.End, options);
		}

		/// <summary>Create a new range query that will read all key-value pairs in the database snapshot represented by the transaction</summary>
		[Obsolete("Use the overload that takes an FdbRangeOptions argument, or use LINQ to configure the query!")]
		public static FdbRangeQuery<KeyValuePair<Slice, Slice>> GetRange(this IFdbReadOnlyTransaction trans, KeyRange range, int limit, bool reverse = false)
		{
			return GetRange(trans, range, new FdbRangeOptions(limit: limit, reverse: reverse));
		}

		/// <summary>Create a new range query that will read all key-value pairs in the database snapshot represented by the transaction</summary>
		public static FdbRangeQuery<KeyValuePair<Slice, Slice>> GetRange(this IFdbReadOnlyTransaction trans, Slice beginKeyInclusive, Slice endKeyExclusive, FdbRangeOptions? options = null)
		{
			Contract.NotNull(trans);

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
		public static FdbRangeQuery<KeyValuePair<Slice, Slice>> GetRange(this IFdbReadOnlyTransaction trans, Slice beginKeyInclusive, Slice endKeyExclusive, int limit, bool reverse = false)
		{
			return GetRange(trans, beginKeyInclusive, endKeyExclusive, new FdbRangeOptions(limit: limit, reverse: reverse));
		}

		/// <summary>Create a new range query that will read all key-value pairs in the database snapshot represented by the transaction</summary>
		public static FdbRangeQuery<KeyValuePair<Slice, Slice>> GetRange(this IFdbReadOnlyTransaction trans, KeySelectorPair range, FdbRangeOptions? options = null)
		{
			Contract.NotNull(trans);

			return trans.GetRange(range.Begin, range.End, options);
		}

		#endregion

		#region GetRange<T>...

		/// <summary>Create a new range query that will read all key-value pairs in the database snapshot represented by the transaction</summary>
		public static FdbRangeQuery<TResult> GetRange<TResult>(this IFdbReadOnlyTransaction trans, KeyRange range, Func<KeyValuePair<Slice, Slice>, TResult> transform, FdbRangeOptions? options = null)
		{
			return GetRange(trans, KeySelectorPair.Create(range), transform, options);
		}

		/// <summary>Create a new range query that will read all key-value pairs in the database snapshot represented by the transaction</summary>
		public static FdbRangeQuery<TResult> GetRange<TResult>(this IFdbReadOnlyTransaction trans, Slice beginKeyInclusive, Slice endKeyExclusive, Func<KeyValuePair<Slice, Slice>, TResult> transform, FdbRangeOptions? options = null)
		{
			Contract.NotNull(trans);

			if (beginKeyInclusive.IsNullOrEmpty) beginKeyInclusive = FdbKey.MinValue;
			if (endKeyExclusive.IsNullOrEmpty) endKeyExclusive = FdbKey.MaxValue;

			return trans.GetRange(
				KeySelector.FirstGreaterOrEqual(beginKeyInclusive),
				KeySelector.FirstGreaterOrEqual(endKeyExclusive),
				transform,
				options
			);
		}

		/// <summary>Create a new range query that will read all key-value pairs in the database snapshot represented by the transaction</summary>
		public static FdbRangeQuery<TResult> GetRange<TResult>(this IFdbReadOnlyTransaction trans, KeySelectorPair range, Func<KeyValuePair<Slice, Slice>, TResult> transform, FdbRangeOptions? options = null)
		{
			Contract.NotNull(trans);

			return trans.GetRange(range.Begin, range.End, transform, options);
		}

		#endregion

		#region GetRangeKeys...

		private static readonly Func<KeyValuePair<Slice, Slice>, Slice> KeyValuePairToKey = (kv) => kv.Key;

		[Pure, LinqTunnel]
		public static FdbRangeQuery<Slice> OnlyKeys(this FdbRangeQuery<KeyValuePair<Slice, Slice>> query)
		{
			if (query.Read == FdbReadMode.Values) throw new InvalidOperationException("Cannot extract keys because the source query only read the values.");
			return new FdbRangeQuery<Slice>(query.Transaction, query.Begin, query.End, KeyValuePairToKey, query.Snapshot, query.Options.OnlyKeys());
		}

		/// <summary>Create a new range query that will read the keys of all key-value pairs in the database snapshot represented by the transaction</summary>
		[Pure, LinqTunnel]
		public static FdbRangeQuery<Slice> GetRangeKeys(this IFdbReadOnlyTransaction trans, KeySelector beginInclusive, KeySelector endExclusive, FdbRangeOptions? options = null)
		{
			Contract.NotNull(trans);
			return trans.GetRange(
				beginInclusive,
				endExclusive,
				KeyValuePairToKey,
				options?.OnlyKeys() ?? new FdbRangeOptions { Read = FdbReadMode.Keys }
			);
		}

		/// <summary>Create a new range query that will read the keys of all key-value pairs in the database snapshot represented by the transaction</summary>
		[Pure, LinqTunnel]
		public static FdbRangeQuery<Slice> GetRangeKeys(this IFdbReadOnlyTransaction trans, KeyRange range, FdbRangeOptions? options = null)
		{
			Contract.NotNull(trans);
			var selectors = KeySelectorPair.Create(range);
			return trans.GetRange(
				selectors.Begin, 
				selectors.End, 
				KeyValuePairToKey,
				options?.OnlyKeys() ?? new FdbRangeOptions { Read = FdbReadMode.Keys }
			);
		}

		/// <summary>Create a new range query that will read the keys of all key-value pairs in the database snapshot represented by the transaction</summary>
		[Pure, LinqTunnel]
		public static FdbRangeQuery<Slice> GetRangeKeys(this IFdbReadOnlyTransaction trans, Slice beginKeyInclusive, Slice endKeyExclusive, FdbRangeOptions? options = null)
		{
			Contract.NotNull(trans);
			return trans.GetRange(
				KeySelector.FirstGreaterOrEqual(beginKeyInclusive.IsNullOrEmpty ? FdbKey.MinValue : beginKeyInclusive),
				KeySelector.FirstGreaterOrEqual(endKeyExclusive.IsNullOrEmpty ? FdbKey.MaxValue : endKeyExclusive),
				KeyValuePairToKey,
				options?.OnlyKeys() ?? new FdbRangeOptions { Read = FdbReadMode.Keys }
			);
		}

		/// <summary>Create a new range query that will read the keys of all key-value pairs in the database snapshot represented by the transaction</summary>
		[Pure, LinqTunnel]
		public static FdbRangeQuery<Slice> GetRangeKeys(this IFdbReadOnlyTransaction trans, KeySelectorPair range, FdbRangeOptions? options = null)
		{
			Contract.NotNull(trans);
			return trans.GetRange(
				range.Begin,
				range.End,
				KeyValuePairToKey,
				options?.OnlyKeys() ?? new FdbRangeOptions { Read = FdbReadMode.Keys }
			);
		}

		#endregion

		#region GetRangeValues...

		private static readonly Func<KeyValuePair<Slice, Slice>, Slice> KeyValuePairToValue = (kv) => kv.Value;

		[Pure, LinqTunnel]
		public static FdbRangeQuery<Slice> OnlyValues(this FdbRangeQuery<KeyValuePair<Slice, Slice>> query)
		{
			if (query.Read == FdbReadMode.Keys) throw new InvalidOperationException("Cannot extract values because the source query only read the keys.");
			return new FdbRangeQuery<Slice>(query.Transaction, query.Begin, query.End, KeyValuePairToValue, query.Snapshot, query.Options.OnlyValues());
		}

		/// <summary>Create a new range query that will read the values of all key-value pairs in the database snapshot represented by the transaction</summary>
		[Pure, LinqTunnel]
		public static FdbRangeQuery<Slice> GetRangeValues(this IFdbReadOnlyTransaction trans, KeySelector beginInclusive, KeySelector endExclusive, FdbRangeOptions? options = null)
		{
			Contract.NotNull(trans);
			return trans.GetRange(
				beginInclusive,
				endExclusive,
				KeyValuePairToValue,
				options?.OnlyValues() ?? new FdbRangeOptions { Read = FdbReadMode.Values }
			);
		}

		/// <summary>Create a new range query that will read the values of all key-value pairs in the database snapshot represented by the transaction</summary>
		[Pure, LinqTunnel]
		public static FdbRangeQuery<Slice> GetRangeValues(this IFdbReadOnlyTransaction trans, KeyRange range, FdbRangeOptions? options = null)
		{
			Contract.NotNull(trans);
			var selectors = KeySelectorPair.Create(range);
			return trans.GetRange(
				selectors.Begin,
				selectors.End,
				KeyValuePairToValue,
				options?.OnlyValues() ?? new FdbRangeOptions { Read = FdbReadMode.Values }
			);
		}

		/// <summary>Create a new range query that will read the values of all key-value pairs in the database snapshot represented by the transaction</summary>
		[Pure, LinqTunnel]
		public static FdbRangeQuery<Slice> GetRangeValues(this IFdbReadOnlyTransaction trans, Slice beginKeyInclusive, Slice endKeyExclusive, FdbRangeOptions? options = null)
		{
			Contract.NotNull(trans);
			return trans.GetRange(
				KeySelector.FirstGreaterOrEqual(beginKeyInclusive.IsNullOrEmpty ? FdbKey.MinValue : beginKeyInclusive),
				KeySelector.FirstGreaterOrEqual(endKeyExclusive.IsNullOrEmpty ? FdbKey.MaxValue : endKeyExclusive),
				KeyValuePairToValue,
				options?.OnlyValues() ?? new FdbRangeOptions { Read = FdbReadMode.Values }
			);
		}

		/// <summary>Create a new range query that will read the values of all key-value pairs in the database snapshot represented by the transaction</summary>
		[Pure, LinqTunnel]
		public static FdbRangeQuery<Slice> GetRangeValues(this IFdbReadOnlyTransaction trans, KeySelectorPair range, FdbRangeOptions? options = null)
		{
			Contract.NotNull(trans);
			return trans.GetRange(
				range.Begin,
				range.End,
				KeyValuePairToValue,
				options?.OnlyValues() ?? new FdbRangeOptions { Read = FdbReadMode.Values }
			);
		}

		#endregion

		#region GetRangeAsync...

		/// <summary>
		/// Reads all key-value pairs in the database snapshot represented by transaction (potentially limited by Limit, TargetBytes, or Mode)
		/// which have a key lexicographically greater than or equal to the key resolved by the begin key selector
		/// and lexicographically less than the key resolved by the end key selector.
		/// </summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="beginInclusive">key selector defining the beginning of the range</param>
		/// <param name="endExclusive">key selector defining the end of the range</param>
		/// <param name="options">Optional query options (Limit, TargetBytes, Mode, Reverse, ...)</param>
		/// <param name="iteration">If streaming mode is FdbStreamingMode.Iterator, this parameter should start at 1 and be incremented by 1 for each successive call while reading this range. In all other cases it is ignored.</param>
		/// <returns></returns>
		public static Task<FdbRangeChunk> GetRangeAsync(this IFdbReadOnlyTransaction trans, KeySelector beginInclusive, KeySelector endExclusive, FdbRangeOptions? options, int iteration = 0)
		{
			int limit = options?.Limit ?? 0;
			bool reverse = options?.Reverse ?? false;
			int targetBytes = options?.TargetBytes ?? 0;
			var mode = options?.Mode ?? FdbStreamingMode.Iterator;
			var read = options?.Read ?? FdbReadMode.Both;

			return trans.GetRangeAsync(beginInclusive, endExclusive, limit, reverse, targetBytes, mode, read, iteration);
		}

		/// <summary>
		/// Reads all key-value pairs in the database snapshot represented by transaction (potentially limited by Limit, TargetBytes, or Mode)
		/// which have a key lexicographically greater than or equal to the key resolved by the begin key selector
		/// and lexicographically less than the key resolved by the end key selector.
		/// </summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="range">key selector pair defining the beginning and the end of the range</param>
		/// <param name="options">Optional query options (Limit, TargetBytes, Mode, Reverse, ...)</param>
		/// <param name="iteration">If streaming mode is FdbStreamingMode.Iterator, this parameter should start at 1 and be incremented by 1 for each successive call while reading this range. In all other cases it is ignored.</param>
		/// <returns></returns>
		public static Task<FdbRangeChunk> GetRangeAsync(this IFdbReadOnlyTransaction trans, KeySelectorPair range, FdbRangeOptions? options, int iteration = 0)
		{
			return trans.GetRangeAsync(range.Begin, range.End, options, iteration);
		}

		/// <summary>
		/// Reads all key-value pairs in the database snapshot represented by transaction (potentially limited by Limit, TargetBytes, or Mode)
		/// which have a key lexicographically greater than or equal to the key resolved by the begin key selector
		/// and lexicographically less than the key resolved by the end key selector.
		/// </summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="range">Range of keys defining the beginning (inclusive) and the end (exclusive) of the range</param>
		/// <param name="limit">Maximum number of items to return</param>
		/// <param name="reverse">If true, results are returned in reverse order (from last to first)</param>
		/// <param name="targetBytes">Maximum number of bytes to read</param>
		/// <param name="mode">Streaming mode (defaults to <see cref="FdbStreamingMode.Iterator"/>)</param>
		/// <param name="read">Read mode (defaults to <see cref="FdbReadMode.Both"/>)</param>
		/// <param name="iteration">If <paramref name="mode">streaming mode</paramref> is <see cref="FdbStreamingMode.Iterator"/>, this parameter should start at 1 and be incremented by 1 for each successive call while reading this range. In all other cases it is ignored.</param>
		/// <returns></returns>
		public static Task<FdbRangeChunk> GetRangeAsync(this IFdbReadOnlyTransaction trans, KeyRange range, int limit = 0, bool reverse = false, int targetBytes = 0, FdbStreamingMode mode = FdbStreamingMode.Iterator, FdbReadMode read = FdbReadMode.Both, int iteration = 0)
		{
			var sp = KeySelectorPair.Create(range);
			return trans.GetRangeAsync(sp.Begin, sp.End, limit, reverse, targetBytes, mode, read, iteration);
		}

		/// <summary>
		/// Reads all key-value pairs in the database snapshot represented by transaction (potentially limited by Limit, TargetBytes, or Mode)
		/// which have a key lexicographically greater than or equal to the key resolved by the begin key selector
		/// and lexicographically less than the key resolved by the end key selector.
		/// </summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="beginInclusive">Key defining the beginning (inclusive) of the range</param>
		/// <param name="endExclusive">Key defining the end (exclusive) of the range</param>
		/// <param name="limit">Maximum number of items to return</param>
		/// <param name="reverse">If true, results are returned in reverse order (from last to first)</param>
		/// <param name="targetBytes">Maximum number of bytes to read</param>
		/// <param name="mode">Streaming mode (defaults to <see cref="FdbStreamingMode.Iterator"/>)</param>
		/// <param name="read">Read mode (defaults to <see cref="FdbReadMode.Both"/>)</param>
		/// <param name="iteration">If <paramref name="mode">streaming mode</paramref> is <see cref="FdbStreamingMode.Iterator"/>, this parameter should start at 1 and be incremented by 1 for each successive call while reading this range. In all other cases it is ignored.</param>
		/// <returns></returns>
		public static Task<FdbRangeChunk> GetRangeAsync(this IFdbReadOnlyTransaction trans, Slice beginInclusive, Slice endExclusive, int limit = 0, bool reverse = false, int targetBytes = 0, FdbStreamingMode mode = FdbStreamingMode.Iterator, FdbReadMode read = FdbReadMode.Both, int iteration = 0)
		{
			var range = KeySelectorPair.Create(beginInclusive, endExclusive);
			return trans.GetRangeAsync(range.Begin, range.End, limit, reverse, targetBytes, mode, read, iteration);
		}

		/// <summary>
		/// Reads all key-value pairs in the database snapshot represented by transaction (potentially limited by Limit, TargetBytes, or Mode)
		/// which have a key lexicographically greater than or equal to the key resolved by the begin key selector
		/// and lexicographically less than the key resolved by the end key selector.
		/// </summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="range">Range of keys defining the beginning (inclusive) and the end (exclusive) of the range</param>
		/// <param name="options">Optional query options (Limit, TargetBytes, Mode, Reverse, ...)</param>
		/// <param name="iteration">If streaming mode is FdbStreamingMode.Iterator, this parameter should start at 1 and be incremented by 1 for each successive call while reading this range. In all other cases it is ignored.</param>
		/// <returns></returns>
		public static Task<FdbRangeChunk> GetRangeAsync(this IFdbReadOnlyTransaction trans, KeyRange range, FdbRangeOptions? options, int iteration = 0)
		{
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
		/// <param name="options">Optional query options (Limit, TargetBytes, Mode, Reverse, ...)</param>
		/// <param name="iteration">If streaming mode is FdbStreamingMode.Iterator, this parameter should start at 1 and be incremented by 1 for each successive call while reading this range. In all other cases it is ignored.</param>
		/// <returns></returns>
		public static Task<FdbRangeChunk> GetRangeAsync(this IFdbReadOnlyTransaction trans, Slice beginInclusive, Slice endExclusive, FdbRangeOptions? options, int iteration = 0)
		{
			var range = KeySelectorPair.Create(beginInclusive, endExclusive);
			return trans.GetRangeAsync(range.Begin, range.End, options, iteration);
		}

		#endregion

		#region GetAddressesForKeyAsync...

		/// <summary>Returns a list of public network addresses as strings, one for each of the storage servers responsible for storing <paramref name="key"/> and its associated value</summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="key">Name of the key whose location is to be queried.</param>
		/// <returns>Task that will return an array of strings, or an exception</returns>
		public static Task<string[]> GetAddressesForKeyAsync(this IFdbReadOnlyTransaction trans, ReadOnlyMemory<byte> key)
		{
			return trans.GetAddressesForKeyAsync(key.Span);
		}

		/// <summary>Returns a list of public network addresses as strings, one for each of the storage servers responsible for storing <paramref name="key"/> and its associated value</summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="key">Name of the key whose location is to be queried.</param>
		/// <returns>Task that will return an array of strings, or an exception</returns>
		/// <remarks><para>Starting from API level 630, the addresses include the port ("IP:PORT") by default. Below 630 it does not include the port, unless option <see cref="FdbTransactionOption.IncludePortInAddress"/> is set.</para></remarks>
		public static Task<string[]> GetAddressesForKeyAsync(this IFdbReadOnlyTransaction trans, Slice key)
		{
			if (key.IsNull) throw Fdb.Errors.KeyCannotBeNull();
			return trans.GetAddressesForKeyAsync(key.Span);
		}

		#endregion

		#region GetRangeSplitPointsAsync...

		/// <summary>Returns a list of keys that can split the given range into (roughly) equally sized chunks based on <paramref name="chunkSize"/>.</summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="beginKey">Name of the key of the start of the range</param>
		/// <param name="endKey">Name of the key of the end of the range</param>
		/// <param name="chunkSize">Size of chunks that will be used to split the range</param>
		/// <returns>Task that will return an array of keys that split the range in equally sized chunks, or an exception</returns>
		/// <remarks>The returned split points contain the start key and end key of the given range</remarks>
		public static Task<Slice[]> GetRangeSplitPointsAsync(this IFdbReadOnlyTransaction trans, ReadOnlyMemory<byte> beginKey, ReadOnlyMemory<byte> endKey, long chunkSize)
		{
			return trans.GetRangeSplitPointsAsync(beginKey.Span, endKey.Span, chunkSize);
		}

		/// <summary>Returns a list of keys that can split the given range into (roughly) equally sized chunks based on <paramref name="chunkSize"/>.</summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="beginKey">Name of the key of the start of the range</param>
		/// <param name="endKey">Name of the key of the end of the range</param>
		/// <param name="chunkSize">Size of chunks that will be used to split the range</param>
		/// <returns>Task that will return an array of keys that split the range in equally sized chunks, or an exception</returns>
		/// <remarks>The returned split points contain the start key and end key of the given range</remarks>
		public static Task<Slice[]> GetRangeSplitPointsAsync(this IFdbReadOnlyTransaction trans, Slice beginKey, Slice endKey, long chunkSize)
		{
			if (beginKey.IsNull) throw Fdb.Errors.KeyCannotBeNull();
			if (endKey.IsNull) throw Fdb.Errors.KeyCannotBeNull();
			return trans.GetRangeSplitPointsAsync(beginKey.Span, endKey.Span, chunkSize);
		}

		#endregion

		#region GetEstimatedRangeSizeBytesAsync...

		/// <summary>Returns an estimated byte size of the key range.</summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="beginKey">Name of the key of the start of the range</param>
		/// <param name="endKey">Name of the key of the end of the range</param>
		/// <returns>Task that will return an estimated byte size of the key range, or an exception</returns>
		/// <remarks>The estimated size is calculated based on the sampling done by FDB server. The sampling algorithm works roughly in this way: the larger the key-value pair is, the more likely it would be sampled and the more accurate its sampled size would be. And due to that reason it is recommended to use this API to query against large ranges for accuracy considerations. For a rough reference, if the returned size is larger than 3MB, one can consider the size to be accurate.</remarks>
		public static Task<long> GetEstimatedRangeSizeBytesAsync(this IFdbReadOnlyTransaction trans, ReadOnlyMemory<byte> beginKey, ReadOnlyMemory<byte> endKey)
		{
			return trans.GetEstimatedRangeSizeBytesAsync(beginKey.Span, endKey.Span);
		}

		/// <summary>Returns an estimated byte size of the key range.</summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="beginKey">Name of the key of the start of the range</param>
		/// <param name="endKey">Name of the key of the end of the range</param>
		/// <returns>Task that will return an estimated byte size of the key range, or an exception</returns>
		/// <remarks>The estimated size is calculated based on the sampling done by FDB server. The sampling algorithm works roughly in this way: the larger the key-value pair is, the more likely it would be sampled and the more accurate its sampled size would be. And due to that reason it is recommended to use this API to query against large ranges for accuracy considerations. For a rough reference, if the returned size is larger than 3MB, one can consider the size to be accurate.</remarks>
		public static Task<long> GetEstimatedRangeSizeBytesAsync(this IFdbReadOnlyTransaction trans, Slice beginKey, Slice endKey)
		{
			if (beginKey.IsNull) throw Fdb.Errors.KeyCannotBeNull();
			if (endKey.IsNull) throw Fdb.Errors.KeyCannotBeNull();
			return trans.GetEstimatedRangeSizeBytesAsync(beginKey.Span, endKey.Span);
		}

		#endregion

		#region CheckValueAsync...

		/// <summary>Check if the value from the database snapshot represented by the current transaction is equal to some <paramref name="expected"/> value.</summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="key">Key to be looked up in the database</param>
		/// <param name="expected">Expected value for this key</param>
		/// <returns>Task that will return the value of the key if it is found, Slice.Nil if the key does not exist, or an exception</returns>
		/// <exception cref="System.ArgumentException">If the <paramref name="key"/> is null</exception>
		/// <exception cref="System.OperationCanceledException">If the cancellation token is already triggered</exception>
		/// <exception cref="System.ObjectDisposedException">If the transaction has already been completed</exception>
		/// <exception cref="System.InvalidOperationException">If the operation method is called from the Network Thread</exception>
		/// <returns>Return the result of the check, plus the actual value of the key.</returns>
		public static Task<(FdbValueCheckResult Result, Slice Actual)> CheckValueAsync(this IFdbReadOnlyTransaction trans, Slice key, Slice expected)
		{
			if (key.IsNull) throw Fdb.Errors.KeyCannotBeNull();
			return trans.CheckValueAsync(key.Span, expected);
		}

		#endregion

		#region Clear...

		/// <summary>
		/// Modify the database snapshot represented by this transaction to remove the given key from the database. If the key was not previously present in the database, there is no effect.
		/// </summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="key">Name of the key to be removed from the database.</param>
		public static void Clear(this IFdbTransaction trans, Slice key)
		{
			if (key.IsNull) throw Fdb.Errors.KeyCannotBeNull();

			trans.Clear(key.Span);
		}

		#endregion

		#region ClearRange...

		/// <summary>
		/// Modify the database snapshot represented by this transaction to remove all keys (if any) which are lexicographically greater than or equal to the given begin key and lexicographically less than the given end_key.
		/// Sets and clears affect the actual database only if transaction is later committed with CommitAsync().
		/// </summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="range">Pair of keys defining the range to clear.</param>
		public static void ClearRange(this IFdbTransaction trans, KeyRange range)
		{
			Contract.NotNull(trans);

			ClearRange(trans, range.Begin, range.End.HasValue ? range.End : FdbKey.MaxValue);
		}

		/// <summary>
		/// Modify the database snapshot represented by this transaction to remove all keys (if any) which are lexicographically greater than or equal to the given begin key and lexicographically less than the given end_key.
		/// Sets and clears affect the actual database only if transaction is later committed with CommitAsync().
		/// </summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="beginKeyInclusive">Name of the key specifying the beginning of the range to clear.</param>
		/// <param name="endKeyExclusive">Name of the key specifying the end of the range to clear.</param>
		public static void ClearRange(this IFdbTransaction trans, Slice beginKeyInclusive, Slice endKeyExclusive)
		{
			if (beginKeyInclusive.IsNull) throw Fdb.Errors.KeyCannotBeNull(nameof(beginKeyInclusive));
			if (endKeyExclusive.IsNull) throw Fdb.Errors.KeyCannotBeNull(nameof(endKeyExclusive));

			trans.ClearRange(beginKeyInclusive.Span, endKeyExclusive.Span);
		}

		#endregion

		#region Conflict Ranges...

		/// <summary>
		/// Adds a conflict range to a transaction without performing the associated read or write.
		/// </summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="beginKeyInclusive">Key specifying the beginning of the conflict range. The key is included</param>
		/// <param name="endKeyExclusive">Key specifying the end of the conflict range. The key is excluded</param>
		/// <param name="type">One of the <see cref="FdbConflictRangeType"/> values indicating what type of conflict range is being set.</param>
		public static void AddConflictRange(this IFdbTransaction trans, Slice beginKeyInclusive, Slice endKeyExclusive, FdbConflictRangeType type)
		{
			if (beginKeyInclusive.IsNull) throw Fdb.Errors.KeyCannotBeNull(nameof(beginKeyInclusive));
			if (endKeyExclusive.IsNull) throw Fdb.Errors.KeyCannotBeNull(nameof(endKeyExclusive));

			trans.AddConflictRange(beginKeyInclusive.Span, endKeyExclusive.Span, type);
		}

		/// <summary>
		/// Adds a conflict range to a transaction without performing the associated read or write.
		/// </summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="range">Range of the keys specifying the conflict range. The end key is excluded</param>
		/// <param name="type">One of the FDBConflictRangeType values indicating what type of conflict range is being set.</param>
		public static void AddConflictRange(this IFdbTransaction trans, KeyRange range, FdbConflictRangeType type)
		{
			trans.AddConflictRange(range.Begin, range.End.HasValue ? range.End : FdbKey.MaxValue, type);
		}

		/// <summary>
		/// Adds a range of keys to the transaction’s read conflict ranges as if you had read the range. As a result, other transactions that write a key in this range could cause the transaction to fail with a conflict.
		/// </summary>
		public static void AddReadConflictRange(this IFdbTransaction trans, KeyRange range)
		{
			AddConflictRange(trans, range, FdbConflictRangeType.Read);
		}

		/// <summary>
		/// Adds a range of keys to the transaction’s read conflict ranges as if you had read the range. As a result, other transactions that write a key in this range could cause the transaction to fail with a conflict.
		/// </summary>
		public static void AddReadConflictRange(this IFdbTransaction trans, Slice beginKeyInclusive, Slice endKeyExclusive)
		{
			trans.AddConflictRange(beginKeyInclusive, endKeyExclusive, FdbConflictRangeType.Read);
		}

		/// <summary>
		/// Adds a range of keys to the transaction’s read conflict ranges as if you had read the range. As a result, other transactions that write a key in this range could cause the transaction to fail with a conflict.
		/// </summary>
		public static void AddReadConflictRange(this IFdbTransaction trans, ReadOnlySpan<byte> beginKeyInclusive, ReadOnlySpan<byte> endKeyExclusive)
		{
			trans.AddConflictRange(beginKeyInclusive, endKeyExclusive, FdbConflictRangeType.Read);
		}

		/// <summary>
		/// Adds a key to the transaction’s read conflict ranges as if you had read the key. As a result, other transactions that write to this key could cause the transaction to fail with a conflict.
		/// </summary>
		public static void AddReadConflictKey(this IFdbTransaction trans, Slice key)
		{
			AddConflictRange(trans, KeyRange.FromKey(key), FdbConflictRangeType.Read);
		}

		/// <summary>
		/// Adds a range of keys to the transaction’s write conflict ranges as if you had cleared the range. As a result, other transactions that concurrently read a key in this range could fail with a conflict.
		/// </summary>
		public static void AddWriteConflictRange(this IFdbTransaction trans, KeyRange range)
		{
			AddConflictRange(trans, range, FdbConflictRangeType.Write);
		}

		/// <summary>
		/// Adds a range of keys to the transaction’s write conflict ranges as if you had cleared the range. As a result, other transactions that concurrently read a key in this range could fail with a conflict.
		/// </summary>
		public static void AddWriteConflictRange(this IFdbTransaction trans, Slice beginKeyInclusive, Slice endKeyExclusive)
		{
			trans.AddConflictRange(beginKeyInclusive, endKeyExclusive, FdbConflictRangeType.Write);
		}

		/// <summary>
		/// Adds a range of keys to the transaction’s write conflict ranges as if you had cleared the range. As a result, other transactions that concurrently read a key in this range could fail with a conflict.
		/// </summary>
		public static void AddWriteConflictRange(this IFdbTransaction trans, ReadOnlySpan<byte> beginKeyInclusive, ReadOnlySpan<byte> endKeyExclusive)
		{
			trans.AddConflictRange(beginKeyInclusive, endKeyExclusive, FdbConflictRangeType.Write);
		}

		/// <summary>
		/// Adds a key to the transaction’s write conflict ranges as if you had cleared the key. As a result, other transactions that concurrently read this key could fail with a conflict.
		/// </summary>
		public static void AddWriteConflictKey(this IFdbTransaction trans, Slice key)
		{
			AddConflictRange(trans, KeyRange.FromKey(key), FdbConflictRangeType.Write);
		}

		#endregion

		#region Watch...

		/// <summary>Watch a key for any change in the database.</summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="key">Key to watch</param>
		/// <param name="ct">CancellationToken used to abort the watch if the caller doesn't want to wait anymore. Note that you can manually cancel the watch by calling Cancel() on the returned FdbWatch instance</param>
		/// <returns>FdbWatch that can be awaited and will complete when the key has changed in the database, or cancellation occurs. You can call Cancel() at any time if you are not interested in watching the key anymore. You MUST always call Dispose() if the watch completes or is cancelled, to ensure that resources are released properly.</returns>
		/// <remarks>You can directly await an FdbWatch, or obtain a Task&lt;Slice&gt; by reading the <see cref="FdbWatch.Task"/> property.</remarks>
		[Pure]
		public static FdbWatch Watch(this IFdbTransaction trans, Slice key, CancellationToken ct)
		{
			if (key.IsNull) throw Fdb.Errors.KeyCannotBeNull();
			return trans.Watch(key.Span, ct);
		}

		#endregion

		#region Batching...

		/// <summary>Reads several values from the database snapshot represented by the current transaction</summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="keys">Keys to be looked up in the database</param>
		/// <returns>Task that will return an array of values, or an exception. Each item in the array will contain the value of the key at the same index in <paramref name="keys"/>, or Slice.Nil if that key does not exist.</returns>
		public static Task<Slice[]> GetValuesAsync(this IFdbReadOnlyTransaction trans, Slice[] keys)
		{
			Contract.NotNull(keys);
			return trans.GetValuesAsync(keys.AsSpan());
		}

		/// <summary>Reads several values from the database snapshot represented by the current transaction</summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="keys">Sequence of keys to be looked up in the database</param>
		/// <returns>Task that will return an array of values, or an exception. The position of each item in the array is the same as its corresponding key in <paramref name="keys"/>. If a key does not exist in the database, its value will be Slice.Nil.</returns>
		public static Task<Slice[]> GetValuesAsync(this IFdbReadOnlyTransaction trans, IEnumerable<Slice> keys)
		{
			Contract.NotNull(trans);
			Contract.NotNull(keys);

			return keys switch
			{
				Slice[] arr      => trans.GetValuesAsync(arr.AsSpan()),
				List<Slice> list => trans.GetValuesAsync(CollectionsMarshal.AsSpan(list)),
				_                => trans.GetValuesAsync(keys.ToArray()),
			};
		}

		/// <summary>
		/// Reads several values from the database snapshot represented by the current transaction.
		/// </summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="keys">Sequence of keys to be looked up in the database</param>
		/// <param name="decoder">Decoder used to decoded the results into values of type <typeparamref name="TValue"/></param>
		/// <returns>Task that will return an array of decoded values, or an exception. The position of each item in the array is the same as its corresponding key in <paramref name="keys"/>. If a key does not exist in the database, its value depends on the behavior of <paramref name="decoder"/>.</returns>
		public static async Task<TValue[]> GetValuesAsync<TValue>(this IFdbReadOnlyTransaction trans, IEnumerable<Slice> keys, IValueEncoder<TValue> decoder)
		{
			Contract.NotNull(trans);
			Contract.NotNull(decoder);

			return decoder.DecodeValues(await GetValuesAsync(trans, keys).ConfigureAwait(false));
		}

		/// <summary>Resolves several key selectors against the keys in the database snapshot represented by the current transaction.</summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="selectors">Key selectors to resolve</param>
		/// <returns>Task that will return an array of keys matching the selectors, or an exception</returns>
		public static Task<Slice[]> GetKeysAsync(this IFdbReadOnlyTransaction trans, KeySelector[] selectors)
		{
			Contract.NotNull(trans);
			Contract.NotNull(selectors);
			return trans.GetKeysAsync(selectors.AsSpan());
		}

		/// <summary>Resolves several key selectors against the keys in the database snapshot represented by the current transaction.</summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="selectors">Sequence of key selectors to resolve</param>
		/// <returns>Task that will return an array of keys matching the selectors, or an exception</returns>
		public static Task<Slice[]> GetKeysAsync(this IFdbReadOnlyTransaction trans, IEnumerable<KeySelector> selectors)
		{
			Contract.NotNull(trans);
			Contract.NotNull(selectors);

			return selectors switch
			{
				KeySelector[] arr => trans.GetKeysAsync(arr.AsSpan()),
				List<KeySelector> list => trans.GetKeysAsync(CollectionsMarshal.AsSpan(list)),
				_ => trans.GetKeysAsync(CollectionsMarshal.AsSpan(selectors.ToList())),
			};
		}

		/// <summary>
		/// Reads several values from the database snapshot represented by the current transaction.
		/// </summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="keys">Sequence of keys to be looked up in the database</param>
		/// <returns>Task that will return an array of key/value pairs, or an exception. Each pair in the array will contain the key at the same index in <paramref name="keys"/>, and its corresponding value in the database or Slice.Nil if that key does not exist.</returns>
		/// <remarks>This method is equivalent to calling <see cref="IFdbReadOnlyTransaction.GetValuesAsync"/>, except that it will return the keys in addition to the values.</remarks>
		public static Task<KeyValuePair<Slice, Slice>[]> GetBatchAsync(this IFdbReadOnlyTransaction trans, IEnumerable<Slice> keys)
		{
			Contract.NotNull(trans);
			Contract.NotNull(keys);

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
		public static async Task<KeyValuePair<Slice, Slice>[]> GetBatchAsync(this IFdbReadOnlyTransaction trans, Slice[] keys)
		{
			Contract.NotNull(trans);
			Contract.NotNull(keys);

			var results = await trans.GetValuesAsync(keys).ConfigureAwait(false);
			Contract.Debug.Assert(results != null && results.Length == keys.Length);

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
		/// <returns>Task that will return an array of pairs of key and decoded values, or an exception. The position of each item in the array is the same as its corresponding key in <paramref name="keys"/>. If a key does not exist in the database, its value depends on the behavior of <paramref name="decoder"/>.</returns>
		public static Task<KeyValuePair<Slice, TValue>[]> GetBatchAsync<TValue>(this IFdbReadOnlyTransaction trans, IEnumerable<Slice> keys, IValueEncoder<TValue> decoder)
		{
			Contract.NotNull(trans);
			Contract.NotNull(keys);

			var array = keys as Slice[] ?? keys.ToArray();

			return trans.GetBatchAsync(array, decoder);
		}

		/// <summary>
		/// Reads several values from the database snapshot represented by the current transaction.
		/// </summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="keys">Sequence of keys to be looked up in the database</param>
		/// <param name="decoder">Decoder used to decoded the results into values of type <typeparamref name="TValue"/></param>
		/// <returns>Task that will return an array of pairs of key and decoded values, or an exception. The position of each item in the array is the same as its corresponding key in <paramref name="keys"/>. If a key does not exist in the database, its value depends on the behavior of <paramref name="decoder"/>.</returns>
		public static async Task<KeyValuePair<Slice, TValue>[]> GetBatchAsync<TValue>(this IFdbReadOnlyTransaction trans, Slice[] keys, IValueEncoder<TValue> decoder)
		{
			Contract.NotNull(trans);
			Contract.NotNull(keys);
			Contract.NotNull(decoder);

			var results = await trans.GetValuesAsync(keys).ConfigureAwait(false);
			Contract.Debug.Assert(results != null && results.Length == keys.Length);

			var array = new KeyValuePair<Slice, TValue>[results.Length];
			for (int i = 0; i < array.Length; i++)
			{
				array[i] = new KeyValuePair<Slice, TValue>(keys[i], decoder.DecodeValue(results[i])!);
			}
			return array;
		}

		#endregion

		#region Queries...

		/// <summary>Runs a query inside a read-only transaction context, with retry-logic.</summary>
		/// <param name="db">Database used for the operation</param>
		/// <param name="handler">Lambda function that returns an async enumerable. The function may be called multiple times if the transaction conflicts.</param>
		/// <param name="ct">Token used to cancel the operation</param>
		/// <returns>Task returning the list of all the elements of the async enumerable returned by the last successful call to <paramref name="handler"/>.</returns>
		public static Task<List<T>> QueryAsync<T>(this IFdbReadOnlyRetryable db, [InstantHandle] Func<IFdbReadOnlyTransaction, IAsyncEnumerable<T>> handler, CancellationToken ct)
		{
			Contract.NotNull(db);
			Contract.NotNull(handler);

			return db.ReadAsync((tr) =>
			{
				var query = handler(tr) ?? throw new InvalidOperationException("The query handler returned a null sequence");
				// ReSharper disable once MethodSupportsCancellation
				return query.ToListAsync();
			}, ct);
		}

		/// <summary>Runs a query inside a read-only transaction context, with retry-logic.</summary>
		/// <param name="db">Database used for the operation</param>
		/// <param name="handler">Lambda function that returns an async enumerable. The function may be called multiple times if the transaction conflicts.</param>
		/// <param name="ct">Token used to cancel the operation</param>
		/// <returns>Task returning the list of all the elements of the async enumerable returned by the last successful call to <paramref name="handler"/>.</returns>
		public static Task<List<T>> QueryAsync<T>(this IFdbReadOnlyRetryable db, [InstantHandle] Func<IFdbReadOnlyTransaction, Task<IAsyncEnumerable<T>>> handler, CancellationToken ct)
		{
			Contract.NotNull(db);
			Contract.NotNull(handler);

			return db.ReadAsync(async (tr) =>
			{
				var query = (await handler(tr).ConfigureAwait(false)) ?? throw new InvalidOperationException("The query handler returned a null sequence");
				// ReSharper disable once MethodSupportsCancellation
				return await query.ToListAsync().ConfigureAwait(false);
			}, ct);
		}

		#endregion

	}
}
