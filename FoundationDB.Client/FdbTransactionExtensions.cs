#region Copyright (c) 2023-2025 SnowBank SAS, (c) 2005-2023 Doxense SAS
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
	using System.Buffers.Binary;
	using System.Numerics;

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

		#region Key/Value Validation...

		[Pure, MethodImpl(MethodImplOptions.NoInlining), StackTraceHidden]
		private static ReadOnlySpan<byte> ToSpanKey(Slice key) => !key.IsNull ? key.Span : throw Fdb.Errors.KeyCannotBeNull();

		[Pure, MethodImpl(MethodImplOptions.NoInlining), StackTraceHidden]
		private static ReadOnlySpan<byte> ToSpanValue(Slice value) => !value.IsNull ? value.Span : throw Fdb.Errors.ValueCannotBeNull();

		#endregion

		#region Get...

		/// <summary>Reads a value from the database snapshot represented by the current transaction.</summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="key">Key to be looked up in the database</param>
		/// <returns>Task that will return the value of the key if it is found, Slice.Nil if the key does not exist, or an exception</returns>
		/// <exception cref="System.ArgumentException">If the <paramref name="key"/> is null</exception>
		/// <exception cref="System.OperationCanceledException">If the cancellation token is already triggered</exception>
		/// <exception cref="System.ObjectDisposedException">If the transaction has already been completed</exception>
		/// <exception cref="System.InvalidOperationException">If the operation method is called from the Network Thread</exception>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Task<Slice> GetAsync(this IFdbReadOnlyTransaction trans, Slice key)
			=> trans.GetAsync(ToSpanKey(key));

		/// <summary>Reads a value from the database snapshot represented by the current transaction.</summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="key">Key to be looked up in the database</param>
		/// <param name="state">State that will be forwarded to the <paramref name="decoder"/></param>
		/// <param name="decoder">Decoder that will extract the result from the value found in the database</param>
		/// <returns>Task that will return the value of the key if it is found, <see cref="Slice.Nil">Slice.Nil</see> if the key does not exist, or an exception</returns>
		/// <exception cref="System.ArgumentException">If the <paramref name="key"/> is null</exception>
		/// <exception cref="System.OperationCanceledException">If the cancellation token is already triggered</exception>
		/// <exception cref="System.ObjectDisposedException">If the transaction has already been completed</exception>
		/// <exception cref="System.InvalidOperationException">If the operation method is called from the Network Thread</exception>
		public static Task<TResult> GetAsync<TState, TResult>(this IFdbReadOnlyTransaction trans, Slice key, TState state, FdbValueDecoder<TState, TResult> decoder)
			=> trans.GetAsync(ToSpanKey(key), state, decoder);

		/// <summary>Reads a value from the database snapshot represented by the current transaction.</summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="key">Key to be looked up in the database</param>
		/// <param name="decoder">Decoder that will extract the result from the value found in the database</param>
		/// <returns>Task that will return the value of the key if it is found, <see cref="Slice.Nil">Slice.Nil</see> if the key does not exist, or an exception</returns>
		/// <exception cref="System.ArgumentException">If the <paramref name="key"/> is null</exception>
		/// <exception cref="System.OperationCanceledException">If the cancellation token is already triggered</exception>
		/// <exception cref="System.ObjectDisposedException">If the transaction has already been completed</exception>
		/// <exception cref="System.InvalidOperationException">If the operation method is called from the Network Thread</exception>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Task<TResult> GetAsync<TResult>(this IFdbReadOnlyTransaction trans, Slice key, FdbValueDecoder<TResult> decoder)
			=> trans.GetAsync(ToSpanKey(key), decoder);

		/// <summary>Reads and decodes a value from the database snapshot represented by the current transaction.</summary>
		/// <typeparam name="TValue">Type of the value.</typeparam>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="key">Key to be looked up in the database</param>
		/// <param name="encoder">Encoder used to decode the value of the key.</param>
		/// <returns>Task that will return the value of the key if it is found, Slice.Nil if the key does not exist, or an exception</returns>
		/// <exception cref="System.ArgumentException">If the <paramref name="key"/> is null</exception>
		/// <exception cref="System.OperationCanceledException">If the cancellation token is already triggered</exception>
		/// <exception cref="System.ObjectDisposedException">If the transaction has already been completed</exception>
		/// <exception cref="System.InvalidOperationException">If the operation method is called from the Network Thread</exception>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Task<TValue?> GetAsync<TValue>(this IFdbReadOnlyTransaction trans, Slice key, IValueEncoder<TValue> encoder)
			=> GetAsync(trans, ToSpanKey(key), encoder);

		/// <summary>Reads and decodes a value from the database snapshot represented by the current transaction.</summary>
		/// <typeparam name="TValue">Type of the value.</typeparam>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="key">Key to be looked up in the database</param>
		/// <param name="encoder">Encoder used to decode the value of the key.</param>
		/// <returns>Task that will return the value of the key if it is found, Slice.Nil if the key does not exist, or an exception</returns>
		/// <exception cref="System.ArgumentException">If the <paramref name="key"/> is null</exception>
		/// <exception cref="System.OperationCanceledException">If the cancellation token is already triggered</exception>
		/// <exception cref="System.ObjectDisposedException">If the transaction has already been completed</exception>
		/// <exception cref="System.InvalidOperationException">If the operation method is called from the Network Thread</exception>
		public static Task<TValue?> GetAsync<TValue>(this IFdbReadOnlyTransaction trans, ReadOnlySpan<byte> key, IValueEncoder<TValue> encoder)
		{
			Contract.NotNull(encoder);

			return trans.GetAsync(key, encoder, static (state, buffer, found) =>
			{
				//HACKHACK: TODO: OPTIMIZE: PERF: IValueEncoder should also accept ReadOnlySpan !
				return state.DecodeValue(found ? Slice.FromBytes(buffer) : Slice.Nil);
			});
		}

		/// <summary>Tries to read a value from database snapshot represented by the current transaction, and writes it to <paramref name="valueWriter"/> if found.</summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="key">Key to be looked up in the database</param>
		/// <param name="valueWriter">Buffer writer where the value will be written, if it is found</param>
		/// <returns>Task with <see langword="true"/> if the key was found; otherwise, <see langword="false"/></returns>
		public static Task<bool> TryGetAsync(IFdbReadOnlyTransaction trans, Slice key, IBufferWriter<byte> valueWriter)
		{
			return TryGetAsync(trans, ToSpanKey(key), valueWriter);
		}

		/// <summary>Tries to read a value from database snapshot represented by the current transaction, and writes it to <paramref name="valueWriter"/> if found.</summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="key">Key to be looked up in the database</param>
		/// <param name="valueWriter">Buffer writer where the value will be written, if it is found</param>
		/// <returns>Task with <see langword="true"/> if the key was found; otherwise, <see langword="false"/></returns>
		public static Task<bool> TryGetAsync(IFdbReadOnlyTransaction trans, ReadOnlySpan<byte> key, IBufferWriter<byte> valueWriter)
		{
			Contract.NotNull(valueWriter);
			return trans.GetAsync<IBufferWriter<byte>, bool>(key, valueWriter, static (vw, value, found) =>
			{
				if (!found) return false;
				vw.Write(value);
				return true;
			});
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

		#region GetValueStringAsync

		/// <inheritdoc cref="GetValueStringAsync(FoundationDB.Client.IFdbReadOnlyTransaction,System.ReadOnlySpan{byte})"/>
		public static Task<string?> GetValueStringAsync(this IFdbReadOnlyTransaction trans, Slice key)
			=> trans.GetAsync(ToSpanKey(key), static (value, found) => found ? value.ToStringUtf8() : null);

		/// <summary>Reads the value of a key from the database, into a UTF-8 encoded <see cref="string"/></summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="key">Key to be looked up in the database</param>
		/// <returns>The decoded value of the key, if it exists in the database; otherwise, <see langword="null"/>.</returns>
		public static Task<string?> GetValueStringAsync(this IFdbReadOnlyTransaction trans, ReadOnlySpan<byte> key)
			=> trans.GetAsync(key, static (value, found) => found ? value.ToStringUtf8() : null);

		/// <inheritdoc cref="GetValueStringAsync(FoundationDB.Client.IFdbReadOnlyTransaction,System.ReadOnlySpan{byte},string)"/>
		public static Task<string> GetValueStringAsync(this IFdbReadOnlyTransaction trans, Slice key, string missingValue)
			=> trans.GetAsync(ToSpanKey(key), missingValue, static (missing, value, found) => found ? value.ToStringUtf8() : missing);

		/// <summary>Reads the value of a key from the database, into a UTF-8 encoded <see cref="string"/></summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="key">Key to be looked up in the database</param>
		/// <param name="missingValue">Value returned if the key is missing</param>
		/// <returns>The decoded value of the key, if it exists in the database; otherwise, <paramref name="missingValue"/>.</returns>
		public static Task<string> GetValueStringAsync(this IFdbReadOnlyTransaction trans, ReadOnlySpan<byte> key, string missingValue)
			=> trans.GetAsync(key, missingValue, static (missing, value, found) => found ? value.ToStringUtf8() : missing);

		#endregion

		#region GetValueInt32Async

		/// <inheritdoc cref="GetValueInt32Async(FoundationDB.Client.IFdbReadOnlyTransaction,System.ReadOnlySpan{byte})"/>
		public static Task<int?> GetValueInt32Async(this IFdbReadOnlyTransaction trans, Slice key)
			=> GetValueInt32Async(trans, ToSpanKey(key));

		/// <summary>Reads the value of a key from the database, decoded as a little-endian 32-bit unsigned integer</summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="key">Key to be looked up in the database</param>
		/// <returns>The decoded value of the key, if it exists in the database; otherwise, <see langword="null"/>.</returns>
		public static Task<int?> GetValueInt32Async(this IFdbReadOnlyTransaction trans, ReadOnlySpan<byte> key)
			=> trans.GetAsync(key, static (value, found) => found ? value.ToInt32() : default(int?));

		/// <inheritdoc cref="GetValueInt32Async(FoundationDB.Client.IFdbReadOnlyTransaction,System.ReadOnlySpan{byte},int)"/>
		public static Task<int> GetValueInt32Async(this IFdbReadOnlyTransaction trans, Slice key, int missingValue)
			=> GetValueInt32Async(trans, ToSpanKey(key), missingValue);

		/// <summary>Reads the value of a key from the database, decoded as a little-endian 32-bit unsigned integer</summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="key">Key to be looked up in the database</param>
		/// <param name="missingValue">Value returned if the key is missing</param>
		/// <returns>The decoded value of the key, if it exists in the database; otherwise, <paramref name="missingValue"/>.</returns>
		public static Task<int> GetValueInt32Async(this IFdbReadOnlyTransaction trans, ReadOnlySpan<byte> key, int missingValue)
			=> trans.GetAsync(key, missingValue, static (missing, value, found) => found ? value.ToInt32() : missing);

		#endregion

		#region GetValueUInt32Async

		/// <inheritdoc cref="GetValueUInt32Async(FoundationDB.Client.IFdbReadOnlyTransaction,System.ReadOnlySpan{byte})"/>
		public static Task<uint?> GetValueUInt32Async(this IFdbReadOnlyTransaction trans, Slice key)
			=> GetValueUInt32Async(trans, ToSpanKey(key));

		/// <summary>Reads the value of a key from the database, decoded as a little-endian 32-bit unsigned integer</summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="key">Key to be looked up in the database</param>
		/// <returns>The decoded value of the key, if it exists in the database; otherwise, <see langword="null"/>.</returns>
		public static Task<uint?> GetValueUInt32Async(this IFdbReadOnlyTransaction trans, ReadOnlySpan<byte> key)
			=> trans.GetAsync(key, static (value, found) => found ? value.ToUInt32() : default(uint?));

		/// <inheritdoc cref="GetValueUInt32Async(FoundationDB.Client.IFdbReadOnlyTransaction,System.ReadOnlySpan{byte},uint)"/>
		public static Task<uint> GetValueUInt32Async(this IFdbReadOnlyTransaction trans, Slice key, uint missingValue)
			=> GetValueUInt32Async(trans, ToSpanKey(key), missingValue);

		/// <summary>Reads the value of a key from the database, decoded as a little-endian 32-bit unsigned integer</summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="key">Key to be looked up in the database</param>
		/// <param name="missingValue">Value returned if the key is missing</param>
		/// <returns>The decoded value of the key, if it exists in the database; otherwise, <paramref name="missingValue"/>.</returns>
		public static Task<uint> GetValueUInt32Async(this IFdbReadOnlyTransaction trans, ReadOnlySpan<byte> key, uint missingValue)
			=> trans.GetAsync(key, missingValue, static (missing, value, found) => found ? value.ToUInt32() : missing);

		#endregion

		#region GetValueInt64Async

		/// <inheritdoc cref="GetValueInt64Async(FoundationDB.Client.IFdbReadOnlyTransaction,System.ReadOnlySpan{byte})"/>
		public static Task<long?> GetValueInt64Async(this IFdbReadOnlyTransaction trans, Slice key)
			=> GetValueInt64Async(trans, ToSpanKey(key));

		/// <summary>Reads the value of a key from the database, decoded as a little-endian 64-bit signed integer</summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="key">Key to be looked up in the database</param>
		/// <returns>The decoded value of the key, if it exists in the database; otherwise, <see langword="null"/>.</returns>
		public static Task<long?> GetValueInt64Async(this IFdbReadOnlyTransaction trans, ReadOnlySpan<byte> key)
			=> trans.GetAsync(key, static (value, found) => found ? value.ToInt64() : default(long?));

		/// <inheritdoc cref="GetValueInt64Async(FoundationDB.Client.IFdbReadOnlyTransaction,System.ReadOnlySpan{byte},long)"/>
		public static Task<long> GetValueInt64Async(this IFdbReadOnlyTransaction trans, Slice key, long missingValue)
			=> GetValueInt64Async(trans, ToSpanKey(key), missingValue);

		/// <summary>Reads the value of a key from the database, decoded as a little-endian 64-bit signed integer</summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="key">Key to be looked up in the database</param>
		/// <param name="missingValue">Value returned if the key is missing</param>
		/// <returns>The decoded value of the key, if it exists in the database; otherwise, <paramref name="missingValue"/>.</returns>
		public static Task<long> GetValueInt64Async(this IFdbReadOnlyTransaction trans, ReadOnlySpan<byte> key, long missingValue)
			=> trans.GetAsync(key, missingValue, static (missing, value, found) => found ? value.ToInt64() : missing);

		#endregion

		#region GetValueUInt64Async

		/// <inheritdoc cref="GetValueUInt64Async(FoundationDB.Client.IFdbReadOnlyTransaction,System.ReadOnlySpan{byte})"/>
		public static Task<ulong?> GetValueUInt64Async(this IFdbReadOnlyTransaction trans, Slice key)
			=> GetValueUInt64Async(trans, ToSpanKey(key));

		/// <summary>Reads the value of a key from the database, decoded as a little-endian 64-bit unsigned integer</summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="key">Key to be looked up in the database</param>
		/// <returns>The decoded value of the key, if it exists in the database; otherwise, <see langword="null"/>.</returns>
		public static Task<ulong?> GetValueUInt64Async(this IFdbReadOnlyTransaction trans, ReadOnlySpan<byte> key)
			=> trans.GetAsync(key, static (value, found) => found ? value.ToUInt64() : default(ulong?));

		/// <inheritdoc cref="GetValueUInt64Async(FoundationDB.Client.IFdbReadOnlyTransaction,System.ReadOnlySpan{byte},ulong)"/>
		public static Task<ulong> GetValueUInt64Async(this IFdbReadOnlyTransaction trans, Slice key, ulong missingValue)
			=> GetValueUInt64Async(trans, ToSpanKey(key), missingValue);

		/// <summary>Reads the value of a key from the database, decoded as a little-endian 64-bit unsigned integer</summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="key">Key to be looked up in the database</param>
		/// <param name="missingValue">Value returned if the key is missing</param>
		/// <returns>The decoded value of the key, if it exists in the database; otherwise, <paramref name="missingValue"/>.</returns>
		public static Task<ulong> GetValueUInt64Async(this IFdbReadOnlyTransaction trans, ReadOnlySpan<byte> key, ulong missingValue)
			=> trans.GetAsync(key, missingValue, static (missing, value, found) => found ? value.ToUInt64() : missing);

		#endregion

		#region GetValueUuid128Async

		/// <inheritdoc cref="GetValueUuid128Async(FoundationDB.Client.IFdbReadOnlyTransaction,System.ReadOnlySpan{byte})"/>
		public static Task<Uuid128?> GetValueUuid128Async(this IFdbReadOnlyTransaction trans, Slice key)
			=> GetValueUuid128Async(trans, ToSpanKey(key));

		/// <summary>Reads the value of a key from the database, decoded as 128-bit UUID</summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="key">Key to be looked up in the database</param>
		/// <returns>The decoded value of the key, if it exists in the database; otherwise, <see langword="null"/>.</returns>
		public static Task<Uuid128?> GetValueUuid128Async(this IFdbReadOnlyTransaction trans, ReadOnlySpan<byte> key)
			=> trans.GetAsync(key, static (value, found) => found ? value.ToUuid128() : default(Uuid128?));

		/// <inheritdoc cref="GetValueUuid128Async(FoundationDB.Client.IFdbReadOnlyTransaction,System.ReadOnlySpan{byte},Uuid128)"/>
		public static Task<Uuid128> GetValueUuid128Async(this IFdbReadOnlyTransaction trans, Slice key, Uuid128 missingValue)
			=> GetValueUuid128Async(trans, ToSpanKey(key), missingValue);

		/// <summary>Reads the value of a key from the database, decoded as 128-bit UUID</summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="key">Key to be looked up in the database</param>
		/// <param name="missingValue">Value returned if the key is missing</param>
		/// <returns>The decoded value of the key, if it exists in the database; otherwise, <paramref name="missingValue"/>.</returns>
		public static Task<Uuid128> GetValueUuid128Async(this IFdbReadOnlyTransaction trans, ReadOnlySpan<byte> key, Uuid128 missingValue)
			=> trans.GetAsync(key, missingValue, static (missing, value, found) => found ? value.ToUuid128() : missing);

		#endregion

		#endregion

		#region Set...

		/// <summary>
		/// Modify the database snapshot represented by transaction to change the given key to have the given value. If the given key was not previously present in the database it is inserted.
		/// The modification affects the actual database only if transaction is later committed with CommitAsync().
		/// </summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="key">Name of the key to be inserted into the database.</param>
		/// <param name="value">Value to be inserted into the database.</param>
		public static void Set(this IFdbTransaction trans, Slice key, Slice value)
			=> trans.Set(ToSpanKey(key), ToSpanValue(value));

		/// <summary>
		/// Modify the database snapshot represented by transaction to change the given key to have the given value. If the given key was not previously present in the database it is inserted.
		/// The modification affects the actual database only if transaction is later committed with CommitAsync().
		/// </summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="key">Name of the key to be inserted into the database.</param>
		/// <param name="value">Value to be inserted into the database.</param>
		public static void Set(this IFdbTransaction trans, ReadOnlySpan<byte> key, Slice value)
			=> trans.Set(key, ToSpanValue(value));

		/// <summary>
		/// Modify the database snapshot represented by transaction to change the given key to have the given value. If the given key was not previously present in the database it is inserted.
		/// The modification affects the actual database only if transaction is later committed with CommitAsync().
		/// </summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="key">Name of the key to be inserted into the database.</param>
		/// <param name="value">Value to be inserted into the database.</param>
		public static void Set(this IFdbTransaction trans, Slice key, ReadOnlySpan<byte> value)
			=> trans.Set(ToSpanKey(key), value);

		/// <summary>Set the value of a key in the database, using a custom value encoder.</summary>
		/// <typeparam name="TValue">Type of the value</typeparam>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="key">Key to set</param>
		/// <param name="value">Value of the key</param>
		/// <param name="encoder">Encoder used to convert <paramref name="value"/> into a binary literal.</param>
		public static void Set<TValue>(this IFdbTransaction trans, Slice key, IValueEncoder<TValue> encoder, TValue value)
			=> Set<TValue>(trans, ToSpanKey(key), encoder, value);

		/// <summary>Set the value of a key in the database, using a custom value encoder.</summary>
		/// <typeparam name="TValue">Type of the value</typeparam>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="key">Key to set</param>
		/// <param name="encoder">Encoder used to convert <paramref name="value"/> into a binary literal.</param>
		/// <param name="value">Value of the key</param>
		public static void Set<TValue>(this IFdbTransaction trans, ReadOnlySpan<byte> key, IValueEncoder<TValue> encoder, TValue value)
		{
			Contract.NotNull(trans);
			Contract.NotNull(encoder);

			var writer = new SliceWriter(ArrayPool<byte>.Shared);

			encoder.WriteValueTo(ref writer, value);

			trans.Set(key, writer.ToSpan());

			writer.Dispose();
		}

		/// <summary>Set the value of a key in the database, using custom key and value encoders.</summary>
		/// <typeparam name="TKey">Type of the keys</typeparam>
		/// <typeparam name="TValue">Type of the value</typeparam>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="keyEncoder">Encoder used to convert <paramref name="key"/> into a binary literal.</param>
		/// <param name="key">Argument that will be used to generate the name of the key to set in the database</param>
		/// <param name="valueEncoder">Encoder used to convert <paramref name="value"/> into a binary literal.</param>
		/// <param name="value">Argument that will be used to generate the value for the key</param>
		public static void Set<TKey, TValue>(this IFdbTransaction trans, IKeyEncoder<TKey> keyEncoder, TKey key, IValueEncoder<TValue> valueEncoder, TValue value)
		{
			Contract.NotNull(trans);
			Contract.NotNull(keyEncoder);
			Contract.NotNull(valueEncoder);

			// use the same pooled buffer to write both the key and value
			var writer = new SliceWriter(ArrayPool<byte>.Shared);

			keyEncoder.WriteKeyTo(ref writer, key);
			var keySpan = writer.ToSpan();

			valueEncoder.WriteValueTo(ref writer, value);
			var valueSpan = writer.ToSpan()[keySpan.Length..];

			trans.Set(keySpan, valueSpan);

			writer.Dispose();
		}

		/// <summary>Sets the value of a key in the database as a UTF-8 encoded string</summary>
		public static void SetValueString(this IFdbTransaction trans, Slice key, string value) => SetValueString(trans, ToSpanKey(key), value);

		/// <summary>Sets the value of a key in the database as a UTF-8 encoded string</summary>
		public static void SetValueString(this IFdbTransaction trans, ReadOnlySpan<byte> key, string value)
		{
			Contract.NotNull(value);
			SetValueString(trans, key, value.AsSpan());
		}

		/// <summary>Sets the value of a key in the database as a UTF-8 encoded string</summary>
		public static void SetValueString(this IFdbTransaction trans, Slice key, ReadOnlySpan<char> value) => SetValueString(trans, ToSpanKey(key), value);

		/// <summary>Sets the value of a key in the database as a UTF-8 encoded string</summary>
		public static void SetValueString(this IFdbTransaction trans, ReadOnlySpan<byte> key, ReadOnlySpan<char> value)
		{
			if (value.Length == 0)
			{
				trans.Set(key, default(ReadOnlySpan<byte>));
				return;
			}

			int byteCount = Encoding.UTF8.GetByteCount(value);
			if (byteCount <= 128)
			{
				SetValueStringSmall(trans, key, value, byteCount);
			}
			else
			{
				SetValueStringLarge(trans, key, value, byteCount);
			}

			static void SetValueStringSmall(IFdbTransaction trans, ReadOnlySpan<byte> key, ReadOnlySpan<char> value, int byteCount)
			{
				Span<byte> scratch = stackalloc byte[byteCount];
				int len = Encoding.UTF8.GetBytes(value, scratch);
				trans.Set(key, scratch[..len]);
			}

			static void SetValueStringLarge(IFdbTransaction trans, ReadOnlySpan<byte> key, ReadOnlySpan<char> value, int byteCount)
			{
				var array = ArrayPool<byte>.Shared.Rent(byteCount);
				int len = Encoding.UTF8.GetBytes(value, array);
				trans.Set(key, array.AsSpan(0, len));
				ArrayPool<byte>.Shared.Return(array);
			}

		}

		#region Int32

		/// <summary>Sets the value of a key in the database as a 32-bits little-endian signed integer</summary>
		public static void SetValueInt32(this IFdbTransaction trans, Slice key, int value) => SetValueInt32(trans, ToSpanKey(key), value);

		/// <summary>Sets the value of a key in the database as a 32-bits little-endian signed integer</summary>
		public static void SetValueInt32(this IFdbTransaction trans, ReadOnlySpan<byte> key, int value)
		{
			value = BitConverter.IsLittleEndian ? value : BinaryPrimitives.ReverseEndianness(value);
			trans.Set(key, MemoryMarshal.CreateReadOnlySpan(ref Unsafe.As<int, byte>(ref value), 4));
		}

		/// <summary>Sets the value of a key in the database as a 32-bits little-endian signed integer</summary>
		public static void SetValueInt32Compact(this IFdbTransaction trans, Slice key, int value) => SetValueInt64Compact(trans, ToSpanKey(key), value);

		/// <summary>Sets the value of a key in the database as a 32-bits little-endian signed integer</summary>
		public static void SetValueInt32Compact(this IFdbTransaction trans, ReadOnlySpan<byte> key, int value) => SetValueInt64Compact(trans, key, value);

		#endregion

		#region UInt32

		/// <summary>Sets the value of a key in the database as a 32-bits little-endian unsigned integer</summary>
		public static void SetValueUInt32(this IFdbTransaction trans, Slice key, uint value) => SetValueUInt32(trans, ToSpanKey(key), value);

		/// <summary>Sets the value of a key in the database as a 32-bits little-endian unsigned integer</summary>
		public static void SetValueUInt32(this IFdbTransaction trans, ReadOnlySpan<byte> key, uint value)
		{
			value = BitConverter.IsLittleEndian ? value : BinaryPrimitives.ReverseEndianness(value);
			trans.Set(key, MemoryMarshal.CreateReadOnlySpan(ref Unsafe.As<uint, byte>(ref value), 4));
		}

		/// <summary>Sets the value of a key in the database as a 32-bits little-endian unsigned integer</summary>
		public static void SetValueUInt32Compact(this IFdbTransaction trans, Slice key, uint value) => SetValueUInt64Compact(trans, ToSpanKey(key), value);

		/// <summary>Sets the value of a key in the database as a 32-bits little-endian unsigned integer</summary>
		public static void SetValueUInt32Compact(this IFdbTransaction trans, ReadOnlySpan<byte> key, uint value) => SetValueUInt64Compact(trans, key, value);

		#endregion

		#region Int64

		/// <summary>Sets the value of a key in the database as a 64-bits little-endian signed integer</summary>
		public static void SetValueInt64(this IFdbTransaction trans, Slice key, long value) => SetValueInt64(trans, ToSpanKey(key), value);

		/// <summary>Sets the value of a key in the database as a 64-bits little-endian signed integer</summary>
		public static void SetValueInt64(this IFdbTransaction trans, ReadOnlySpan<byte> key, long value)
		{
			value = BitConverter.IsLittleEndian ? value : BinaryPrimitives.ReverseEndianness(value);
			trans.Set(key, MemoryMarshal.CreateReadOnlySpan(ref Unsafe.As<long, byte>(ref value), 8));
		}

		/// <summary>Sets the value of a key in the database as a 64-bits little-endian signed integer</summary>
		public static void SetValueInt64Compact(this IFdbTransaction trans, Slice key, long value) => SetValueInt64Compact(trans, ToSpanKey(key), value);

		/// <summary>Sets the value of a key in the database as a 64-bits little-endian signed integer</summary>
		public static void SetValueInt64Compact(this IFdbTransaction trans, ReadOnlySpan<byte> key, long value)
		{
			unchecked
			{
				ulong v = Math.Min(Math.Max(1, (ulong) value), ulong.MaxValue - 1);
				int n = ((7 + BitOperations.Log2(BitOperations.RoundUpToPowerOf2(v + 1))) / 8);
				v = BitConverter.IsLittleEndian ? (ulong) value : BinaryPrimitives.ReverseEndianness((ulong) value);
				trans.Set(key, MemoryMarshal.CreateReadOnlySpan(ref Unsafe.As<ulong, byte>(ref v), n));
			}
		}

		#endregion

		#region UInt64

		/// <summary>Sets the value of a key in the database as a 64-bits little-endian unsigned integer</summary>
		public static void SetValueUInt64(this IFdbTransaction trans, Slice key, ulong value) => SetValueUInt64(trans, ToSpanKey(key), value);

		/// <summary>Sets the value of a key in the database as a 64-bits little-endian unsigned integer</summary>
		public static void SetValueUInt64(this IFdbTransaction trans, ReadOnlySpan<byte> key, ulong value)
		{
			value = BitConverter.IsLittleEndian ? value : BinaryPrimitives.ReverseEndianness(value);
			trans.Set(key, MemoryMarshal.CreateReadOnlySpan(ref Unsafe.As<ulong, byte>(ref value), 8));
		}

		/// <summary>Sets the value of a key in the database as a 64-bits little-endian unsigned integer</summary>
		public static void SetValueUInt64Compact(this IFdbTransaction trans, Slice key, ulong value) => SetValueUInt64Compact(trans, ToSpanKey(key), value);

		/// <summary>Sets the value of a key in the database as a 64-bits little-endian unsigned integer</summary>
		public static void SetValueUInt64Compact(this IFdbTransaction trans, ReadOnlySpan<byte> key, ulong value)
		{
			ulong v = Math.Min(Math.Max(1, value), ulong.MaxValue - 1);
			int n = ((7 + BitOperations.Log2(BitOperations.RoundUpToPowerOf2(v + 1))) / 8);
			v = BitConverter.IsLittleEndian ? value : BinaryPrimitives.ReverseEndianness(value);
			trans.Set(key, MemoryMarshal.CreateReadOnlySpan(ref Unsafe.As<ulong, byte>(ref v), n));
		}

		#endregion

		#region UUIDs

		/// <summary>Sets the value of a key in the database as a 128-bits UUID</summary>
		public static void SetValueGuid(this IFdbTransaction trans, Slice key, Guid value) => SetValueGuid(trans, ToSpanKey(key), value);

		/// <summary>Sets the value of a key in the database as a 128-bits UUID</summary>
		public static void SetValueGuid(this IFdbTransaction trans, ReadOnlySpan<byte> key, Guid value)
		{
			Span<byte> scratch = stackalloc byte[16];
			new Uuid128(value).WriteTo(scratch);
			trans.Set(key, scratch);
		}

		/// <summary>Sets the value of a key in the database as a 128-bits UUID</summary>
		public static void SetValueUuid128(this IFdbTransaction trans, Slice key, Uuid128 value) => SetValueUuid128(trans, ToSpanKey(key), value);

		/// <summary>Sets the value of a key in the database as a 128-bits UUID</summary>
		public static void SetValueUuid128(this IFdbTransaction trans, ReadOnlySpan<byte> key, Uuid128 value)
		{
			Span<byte> scratch = stackalloc byte[Uuid128.SizeOf];
			value.WriteTo(scratch);
			trans.Set(key, scratch);
		}

		/// <summary>Sets the value of a key in the database as a 96-bits UUID</summary>
		public static void SetValueUuid96(this IFdbTransaction trans, Slice key, Uuid96 value) => SetValueUuid96(trans, ToSpanKey(key), value);

		/// <summary>Sets the value of a key in the database as a 96-bits UUID</summary>
		public static void SetValueUuid96(this IFdbTransaction trans, ReadOnlySpan<byte> key, Uuid96 value)
		{
			Span<byte> scratch = stackalloc byte[Uuid96.SizeOf];
			value.WriteTo(scratch);
			trans.Set(key, scratch);
		}

		/// <summary>Sets the value of a key in the database as a 96-bits UUID</summary>
		public static void SetValueUuid80(this IFdbTransaction trans, Slice key, Uuid80 value) => SetValueUuid80(trans, ToSpanKey(key), value);

		/// <summary>Sets the value of a key in the database as a 96-bits UUID</summary>
		public static void SetValueUuid80(this IFdbTransaction trans, ReadOnlySpan<byte> key, Uuid80 value)
		{
			Span<byte> scratch = stackalloc byte[Uuid96.SizeOf];
			value.WriteTo(scratch);
			trans.Set(key, scratch);
		}

		/// <summary>Sets the value of a key in the database as a 64-bits UUID</summary>
		public static void SetValueUuid64(this IFdbTransaction trans, Slice key, Uuid64 value) => SetValueUuid64(trans, ToSpanKey(key), value);

		/// <summary>Sets the value of a key in the database as a 64-bits UUID</summary>
		public static void SetValueUuid64(this IFdbTransaction trans, ReadOnlySpan<byte> key, Uuid64 value)
		{
			Span<byte> scratch = stackalloc byte[Uuid64.SizeOf];
			value.WriteTo(scratch);
			trans.Set(key, scratch);
		}

		/// <summary>Sets the value of a key in the database as a 48-bits UUID</summary>
		public static void SetValueUuid48(this IFdbTransaction trans, Slice key, Uuid48 value) => SetValueUuid48(trans, ToSpanKey(key), value);

		/// <summary>Sets the value of a key in the database as a 48-bits UUID</summary>
		public static void SetValueUuid48(this IFdbTransaction trans, ReadOnlySpan<byte> key, Uuid48 value)
		{
			Span<byte> scratch = stackalloc byte[Uuid48.SizeOf];
			value.WriteTo(scratch);
			trans.Set(key, scratch);
		}

		#endregion

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
		/// <exception cref="FdbException">If this operation exceeded the maximum allowed size for a transaction.</exception>
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
		/// <exception cref="FdbException">If this operation exceeded the maximum allowed size for a transaction.</exception>
		public static void SetValues(this IFdbTransaction trans, (Slice Key, Slice Value)[] keyValuePairs)
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
		/// <exception cref="FdbException">If this operation exceeded the maximum allowed size for a transaction.</exception>
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
		/// <param name="keyValuePairs">Array of key and value pairs</param>
		/// <remarks>
		/// Only use this method if you know that the approximate size of count of keys and values will not exceed the maximum size allowed per transaction.
		/// If the list and size of the keys and values is not known in advance, consider using a bulk operation provided by the <see cref="Fdb.Bulk"/> helper class.
		/// </remarks>
		/// <exception cref="ArgumentNullException">If either <paramref name="trans"/> or <paramref name="keyValuePairs"/> is null.</exception>
		/// <exception cref="FdbException">If this operation exceeded the maximum allowed size for a transaction.</exception>
		public static void SetValues(this IFdbTransaction trans, params ReadOnlySpan<(Slice Key, Slice Value)> keyValuePairs)
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
		/// <exception cref="FdbException">If this operation exceeded the maximum allowed size for a transaction.</exception>
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
		/// <exception cref="FdbException">If this operation exceeded the maximum allowed size for a transaction.</exception>
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
		/// <exception cref="FdbException">If this operation exceeded the maximum allowed size for a transaction.</exception>
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
		/// <param name="keyValuePairs">Sequence of key and value pairs</param>
		/// <remarks>
		/// Only use this method if you know that the approximate size of count of keys and values will not exceed the maximum size allowed per transaction.
		/// If the list and size of the keys and values is not known in advance, consider using a bulk operation provided by the <see cref="Fdb.Bulk"/> helper class.
		/// </remarks>
		/// <exception cref="ArgumentNullException">If either <paramref name="trans"/> or <paramref name="keyValuePairs"/> is null.</exception>
		/// <exception cref="FdbException">If this operation exceeded the maximum allowed size for a transaction.</exception>
		public static void SetValues(this IFdbTransaction trans, IEnumerable<(Slice Key, Slice Value)> keyValuePairs)
		{
			Contract.NotNull(trans);
			Contract.NotNull(keyValuePairs);

			if (keyValuePairs is (Slice, Slice)[] arr)
			{
				SetValues(trans, arr.AsSpan());
			}
			else if (keyValuePairs is List<(Slice, Slice)> lst)
			{
				SetValues(trans, CollectionsMarshal.AsSpan(lst));
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
		/// <exception cref="FdbException">If this operation exceeded the maximum allowed size for a transaction.</exception>
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
				using var keyIterator = keys.GetEnumerator();
				using var valueIterator = values.GetEnumerator();

				while(keyIterator.MoveNext())
				{
					if (!valueIterator.MoveNext())
					{
						throw new ArgumentException("Both key and value sequences must have the same size.", nameof(values));
					}

					Set(trans, keyIterator.Current, valueIterator.Current);
				}
				if (valueIterator.MoveNext())
				{
					throw new ArgumentException("Both key and values sequences must have the same size.", nameof(values));
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
			trans.Atomic(ToSpanKey(key), ToSpanValue(param), mutation);
		}

		/// <summary>Modify the database snapshot represented by this transaction to add the value of <paramref name="value"/> to the value stored by the given <paramref name="key"/>.</summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="key">Name of the key whose value is to be mutated.</param>
		/// <param name="value">Value to add to existing value of key.</param>
		public static void AtomicAdd(this IFdbTransaction trans, Slice key, Slice value)
		{
			trans.Atomic(ToSpanKey(key), ToSpanValue(value), FdbMutationType.Add);
		}

		/// <summary>Modify the database snapshot represented by this transaction to add the value of <paramref name="value"/> to the value stored by the given <paramref name="key"/>.</summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="key">Name of the key whose value is to be mutated.</param>
		/// <param name="value">Value to add to existing value of key.</param>
		public static void AtomicAdd(this IFdbTransaction trans, ReadOnlySpan<byte> key, ReadOnlySpan<byte> value)
		{
			trans.Atomic(key, value, FdbMutationType.Add);
		}

		/// <inheritdoc cref="AtomicCompareAndClear(FoundationDB.Client.IFdbTransaction,System.ReadOnlySpan{byte},System.ReadOnlySpan{byte})"/>
		public static void AtomicCompareAndClear(this IFdbTransaction trans, Slice key, Slice comparand)
			=> trans.Atomic(ToSpanKey(key), ToSpanValue(comparand), FdbMutationType.CompareAndClear);

		/// <summary>Modify the database snapshot represented by this transaction to clear the value of <paramref name="key"/> only if it is equal to <paramref name="comparand"/>.</summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="key">Name of the key whose value is to be cleared.</param>
		/// <param name="comparand">Value that the key must have, in order to be cleared.</param>
		/// <remarks>
		/// If the <paramref name="key"/> does not exist, or has a different value than <paramref name="comparand"/> then no changes will be performed.
		/// This method requires API version 610 or greater.
		/// </remarks>
		public static void AtomicCompareAndClear(this IFdbTransaction trans, ReadOnlySpan<byte> key, ReadOnlySpan<byte> comparand)
			=> trans.Atomic(key, comparand, FdbMutationType.CompareAndClear);

		/// <summary>Atomically clear the key only if its value is equal to 4 consecutive zero bytes.</summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="key">Name of the key whose value is to be conditionally cleared.</param>
		/// <remarks>This method requires API version 610 or greater.</remarks>
		public static void AtomicClearIfZero32(this IFdbTransaction trans, Slice key)
			=> AtomicClearIfZero32(trans, ToSpanKey(key));

		/// <summary>Atomically clear the key only if its value is equal to 4 consecutive zero bytes.</summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="key">Name of the key whose value is to be conditionally cleared.</param>
		/// <remarks>This method requires API version 610 or greater.</remarks>
		public static void AtomicClearIfZero32(this IFdbTransaction trans, ReadOnlySpan<byte> key)
		{
			int zero = 0;
			var span = MemoryMarshal.CreateReadOnlySpan(ref Unsafe.As<int, byte>(ref zero), 4);
			trans.Atomic(key, span, FdbMutationType.CompareAndClear);
		}

		/// <summary>Atomically clear the key only if its value is equal to 8 consecutive zero bytes.</summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="key">Name of the key whose value is to be conditionally cleared.</param>
		/// <remarks>This method requires API version 610 or greater.</remarks>
		public static void AtomicClearIfZero64(this IFdbTransaction trans, Slice key)
			=> AtomicClearIfZero64(trans, ToSpanKey(key));

		/// <summary>Atomically clear the key only if its value is equal to 8 consecutive zero bytes.</summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="key">Name of the key whose value is to be conditionally cleared.</param>
		/// <remarks>This method requires API version 610 or greater.</remarks>
		public static void AtomicClearIfZero64(this IFdbTransaction trans, ReadOnlySpan<byte> key)
		{
			long zero = 0;
			var span = MemoryMarshal.CreateReadOnlySpan(ref Unsafe.As<long, byte>(ref zero), 8);
			trans.Atomic(key, span, FdbMutationType.CompareAndClear);
		}

		/// <summary>Modify the database snapshot represented by this transaction to increment by <see langword="1"/> the 32-bit value stored by the given <paramref name="key"/>.</summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="key">Name of the key whose value is to be mutated.</param>
		public static void AtomicIncrement32(this IFdbTransaction trans, Slice key)
			=> AtomicAdd32(trans, ToSpanKey(key), 1);

		/// <summary>Modify the database snapshot represented by this transaction to increment by <see langword="1"/> the 32-bit value stored by the given <paramref name="key"/>.</summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="key">Name of the key whose value is to be mutated.</param>
		public static void AtomicIncrement32(this IFdbTransaction trans, ReadOnlySpan<byte> key)
			=> AtomicAdd32(trans, key, 1);

		/// <summary>Modify the database snapshot represented by this transaction to subtract <see langword="1"/> from the 32-bit value stored by the given <paramref name="key"/>.</summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="key">Name of the key whose value is to be mutated.</param>
		public static void AtomicDecrement32(this IFdbTransaction trans, Slice key)
			=> AtomicAdd32(trans, ToSpanKey(key), -1);

		/// <summary>Modify the database snapshot represented by this transaction to subtract <see langword="1"/> from the 32-bit value stored by the given <paramref name="key"/>.</summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="key">Name of the key whose value is to be mutated.</param>
		public static void AtomicDecrement32(this IFdbTransaction trans, ReadOnlySpan<byte> key)
			=> AtomicAdd32(trans, key, -1);

		/// <summary>Modify the database snapshot represented by this transaction to subtract <see langword="1"/> from the 32-bit value stored by the given <paramref name="key"/>.</summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="key">Name of the key whose value is to be mutated.</param>
		/// <param name="clearIfZero">If <see langword="true"/>, automatically clear the key if it reaches zero. If <see langword="false"/>, the key can remain with a value of 0 in the database.</param>
		/// <remarks>This method requires API version 610 or greater.</remarks>
		public static void AtomicDecrement32(this IFdbTransaction trans, Slice key, bool clearIfZero)
		{
			var keySpan = ToSpanKey(key);
			AtomicAdd32(trans, keySpan, -1);
			if (clearIfZero)
			{
				AtomicClearIfZero32(trans, keySpan);
			}
		}

		/// <summary>Modify the database snapshot represented by this transaction to subtract <see langword="1"/> from the 32-bit value stored by the given <paramref name="key"/>.</summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="key">Name of the key whose value is to be mutated.</param>
		/// <param name="clearIfZero">If <see langword="true"/>, automatically clear the key if it reaches zero. If <see langword="false"/>, the key can remain with a value of 0 in the database.</param>
		/// <remarks>This method requires API version 610 or greater.</remarks>
		public static void AtomicDecrement32(this IFdbTransaction trans, ReadOnlySpan<byte> key, bool clearIfZero)
		{
			AtomicAdd32(trans, key, -1);
			if (clearIfZero)
			{
				AtomicClearIfZero32(trans, key);
			}
		}

		/// <summary>Modify the database snapshot represented by this transaction to add <see langword="1"/> to the 64-bit value stored by the given <paramref name="key"/>.</summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="key">Name of the key whose value is to be mutated.</param>
		public static void AtomicIncrement64(this IFdbTransaction trans, Slice key)
			=> AtomicAdd64(trans, ToSpanKey(key), 1);

		/// <summary>Modify the database snapshot represented by this transaction to add <see langword="1"/> to the 64-bit value stored by the given <paramref name="key"/>.</summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="key">Name of the key whose value is to be mutated.</param>
		public static void AtomicIncrement64(this IFdbTransaction trans, ReadOnlySpan<byte> key)
			=> AtomicAdd64(trans, key, 1);

		/// <summary>Modify the database snapshot represented by this transaction to subtract <see langword="1"/> from the 64-bit value stored by the given <paramref name="key"/>.</summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="key">Name of the key whose value is to be mutated.</param>
		public static void AtomicDecrement64(this IFdbTransaction trans, Slice key)
			=> AtomicAdd64(trans, ToSpanKey(key), -1);

		/// <summary>Modify the database snapshot represented by this transaction to subtract <see langword="1"/> from the 64-bit value stored by the given <paramref name="key"/>.</summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="key">Name of the key whose value is to be mutated.</param>
		public static void AtomicDecrement64(this IFdbTransaction trans, ReadOnlySpan<byte> key)
			=> AtomicAdd64(trans, key, -1);

		/// <summary>Modify the database snapshot represented by this transaction to subtract <see langword="1"/> from the 64-bit value stored by the given <paramref name="key"/>.</summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="key">Name of the key whose value is to be mutated.</param>
		/// <param name="clearIfZero">If <see langword="true"/>, automatically clear the key if it reaches zero. If <see langword="false"/>, the key can remain with a value of 0 in the database.</param>
		/// <remarks>This method requires API version 610 or greater.</remarks>
		public static void AtomicDecrement64(this IFdbTransaction trans, Slice key, bool clearIfZero)
		{
			var keySpan = ToSpanKey(key);
			AtomicAdd64(trans, keySpan, -1);
			if (clearIfZero)
			{
				AtomicClearIfZero64(trans, keySpan);
			}
		}

		/// <summary>Modify the database snapshot represented by this transaction to subtract <see langword="1"/> from the 64-bit value stored by the given <paramref name="key"/>.</summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="key">Name of the key whose value is to be mutated.</param>
		/// <param name="clearIfZero">If <see langword="true"/>, automatically clear the key if it reaches zero. If <see langword="false"/>, the key can remain with a value of 0 in the database.</param>
		/// <remarks>This method requires API version 610 or greater.</remarks>
		public static void AtomicDecrement64(this IFdbTransaction trans, ReadOnlySpan<byte> key, bool clearIfZero)
		{
			AtomicAdd64(trans, key, -1);
			if (clearIfZero)
			{
				AtomicClearIfZero64(trans, key);
			}
		}

		/// <summary>Modify the database snapshot represented by this transaction to add a signed integer to the 32-bit value stored by the given <paramref name="key"/>.</summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="key">Name of the key whose value is to be mutated.</param>
		/// <param name="value">Integer add to existing value of key. It will be encoded as 4 bytes in little-endian.</param>
		public static void AtomicAdd32(this IFdbTransaction trans, Slice key, int value)
			=> trans.AtomicAdd32(ToSpanKey(key), value);

		/// <summary>Modify the database snapshot represented by this transaction to add a signed integer to the 32-bit value stored by the given <paramref name="key"/>.</summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="key">Name of the key whose value is to be mutated.</param>
		/// <param name="value">Integer add to existing value of key. It will be encoded as 4 bytes in little-endian.</param>
		public static void AtomicAdd32(this IFdbTransaction trans, ReadOnlySpan<byte> key, int value)
		{
			if (value == 0) return;

			Span<byte> tmp = stackalloc byte[4];
			BinaryPrimitives.WriteInt32LittleEndian(tmp, value);
			trans.Atomic(key, tmp, FdbMutationType.Add);
		}

		/// <summary>Modify the database snapshot represented by this transaction to add an unsigned integer to the 32-bit value stored by the given <paramref name="key"/>.</summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="key">Name of the key whose value is to be mutated.</param>
		/// <param name="value">Integer add to existing value of key. It will be encoded as 4 bytes in little-endian.</param>
		public static void AtomicAdd32(this IFdbTransaction trans, Slice key, uint value)
			=> trans.AtomicAdd32(ToSpanKey(key), value);

		/// <summary>Modify the database snapshot represented by this transaction to add an unsigned integer to the 32-bit value stored by the given <paramref name="key"/>.</summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="key">Name of the key whose value is to be mutated.</param>
		/// <param name="value">Integer add to existing value of key. It will be encoded as 4 bytes in little-endian.</param>
		public static void AtomicAdd32(this IFdbTransaction trans, ReadOnlySpan<byte> key, uint value)
		{
			if (value == 0) return;

			Span<byte> tmp = stackalloc byte[4];
			BinaryPrimitives.WriteUInt32LittleEndian(tmp, value);
			trans.Atomic(key, tmp, FdbMutationType.Add);
		}

		/// <summary>Modify the database snapshot represented by this transaction to add a signed integer to the 64-bit value stored by the given <paramref name="key"/>.</summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="key">Name of the key whose value is to be mutated.</param>
		/// <param name="value">Integer add to existing value of key. It will be encoded as 8 bytes in little-endian.</param>
		public static void AtomicAdd64(this IFdbTransaction trans, Slice key, long value)
			=> trans.AtomicAdd64(ToSpanKey(key), value);

		/// <summary>Modify the database snapshot represented by this transaction to add a signed integer to the 64-bit value stored by the given <paramref name="key"/>.</summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="key">Name of the key whose value is to be mutated.</param>
		/// <param name="value">Integer add to existing value of key. It will be encoded as 8 bytes in little-endian.</param>
		public static void AtomicAdd64(this IFdbTransaction trans, ReadOnlySpan<byte> key, long value)
		{
			if (value == 0) return;

			Span<byte> tmp = stackalloc byte[8];
			BinaryPrimitives.WriteInt64LittleEndian(tmp, value);
			trans.Atomic(key, tmp, FdbMutationType.Add);
		}

		/// <summary>Modify the database snapshot represented by this transaction to add an unsigned integer to the 64-bit value stored by the given <paramref name="key"/>.</summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="key">Name of the key whose value is to be mutated.</param>
		/// <param name="value">Integer add to existing value of key. It will be encoded as 8 bytes in little-endian.</param>
		public static void AtomicAdd64(this IFdbTransaction trans, Slice key, ulong value)
			=> trans.AtomicAdd64(ToSpanKey(key), value);

		/// <summary>Modify the database snapshot represented by this transaction to add an unsigned integer to the 64-bit value stored by the given <paramref name="key"/>.</summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="key">Name of the key whose value is to be mutated.</param>
		/// <param name="value">Integer add to existing value of key. It will be encoded as 8 bytes in little-endian.</param>
		public static void AtomicAdd64(this IFdbTransaction trans, ReadOnlySpan<byte> key, ulong value)
		{
			if (value == 0) return;

			Span<byte> tmp = stackalloc byte[8];
			BinaryPrimitives.WriteUInt64LittleEndian(tmp, value);
			trans.Atomic(key, tmp, FdbMutationType.Add);
		}

		/// <summary>Modify the database snapshot represented by this transaction to perform a bitwise AND between <paramref name="mask"/> and the value stored by the given <paramref name="key"/>.</summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="key">Name of the key whose value is to be mutated.</param>
		/// <param name="mask">Bit mask.</param>
		public static void AtomicAnd(this IFdbTransaction trans, Slice key, Slice mask)
			=> trans.Atomic(ToSpanKey(key), ToSpanValue(mask), FdbMutationType.BitAnd);

		/// <summary>Modify the database snapshot represented by this transaction to perform a bitwise OR between <paramref name="mask"/> and the value stored by the given <paramref name="key"/>.</summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="key">Name of the key whose value is to be mutated.</param>
		/// <param name="mask">Bit mask.</param>
		public static void AtomicOr(this IFdbTransaction trans, Slice key, Slice mask)
			=> trans.Atomic(ToSpanKey(key), ToSpanValue(mask), FdbMutationType.BitOr);

		/// <summary>Modify the database snapshot represented by this transaction to perform a bitwise OR between <paramref name="mask"/> and the value stored by the given <paramref name="key"/>.</summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="key">Name of the key whose value is to be mutated.</param>
		/// <param name="mask">Bit mask.</param>
		public static void AtomicOr(this IFdbTransaction trans, ReadOnlySpan<byte> key, ReadOnlySpan<byte> mask)
			=> trans.Atomic(key, mask, FdbMutationType.BitOr);

		/// <summary>Modify the database snapshot represented by this transaction to perform a bitwise XOR between <paramref name="mask"/> and the value stored by the given <paramref name="key"/>.</summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="key">Name of the key whose value is to be mutated.</param>
		/// <param name="mask">Bit mask.</param>
		public static void AtomicXor(this IFdbTransaction trans, Slice key, Slice mask)
			=> trans.Atomic(ToSpanKey(key), ToSpanValue(mask), FdbMutationType.BitXor);

		/// <summary>Modify the database snapshot represented by this transaction to perform a bitwise XOR between <paramref name="mask"/> and the value stored by the given <paramref name="key"/>.</summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="key">Name of the key whose value is to be mutated.</param>
		/// <param name="mask">Bit mask.</param>
		public static void AtomicXor(this IFdbTransaction trans, ReadOnlySpan<byte> key, ReadOnlySpan<byte> mask)
			=> trans.Atomic(key, mask, FdbMutationType.BitXor);

		/// <summary>Modify the database snapshot represented by this transaction to append to a value, unless it would become larger that the maximum value size supported by the database.</summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="key">Name of the key whose value is to be mutated.</param>
		/// <param name="value">Value to append.</param>
		public static void AtomicAppendIfFits(this IFdbTransaction trans, Slice key, Slice value)
			=> trans.Atomic(ToSpanKey(key), ToSpanValue(value), FdbMutationType.AppendIfFits);

		/// <summary>Modify the database snapshot represented by this transaction to append to a value, unless it would become larger that the maximum value size supported by the database.</summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="key">Name of the key whose value is to be mutated.</param>
		/// <param name="value">Value to append.</param>
		public static void AtomicAppendIfFits(this IFdbTransaction trans, ReadOnlySpan<byte> key, ReadOnlySpan<byte> value)
			=> trans.Atomic(key, value, FdbMutationType.AppendIfFits);

		/// <summary>Modify the database snapshot represented by this transaction to update a value if it is larger than the value in the database.</summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="key">Name of the key whose value is to be mutated.</param>
		/// <param name="value">Bit mask.</param>
		public static void AtomicMax(this IFdbTransaction trans, Slice key, Slice value)
			=> trans.Atomic(ToSpanKey(key), ToSpanValue(value), FdbMutationType.Max);

		/// <summary>Modify the database snapshot represented by this transaction to update a value if it is larger than the value in the database.</summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="key">Name of the key whose value is to be mutated.</param>
		/// <param name="value">Bit mask.</param>
		public static void AtomicMax(this IFdbTransaction trans, ReadOnlySpan<byte> key, ReadOnlySpan<byte> value)
			=> trans.Atomic(key, value, FdbMutationType.Max);

		/// <summary>Modify the database snapshot represented by this transaction to update a value if it is smaller than the value in the database.</summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="key">Name of the key whose value is to be mutated.</param>
		/// <param name="value">Bit mask.</param>
		public static void AtomicMin(this IFdbTransaction trans, Slice key, Slice value)
			=> trans.Atomic(ToSpanKey(key), ToSpanValue(value), FdbMutationType.Min);

		/// <summary>Modify the database snapshot represented by this transaction to update a value if it is smaller than the value in the database.</summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="key">Name of the key whose value is to be mutated.</param>
		/// <param name="value">Bit mask.</param>
		public static void AtomicMin(this IFdbTransaction trans, ReadOnlySpan<byte> key, ReadOnlySpan<byte> value)
			=> trans.Atomic(key, value, FdbMutationType.Min);

		private static readonly Slice IncompleteToken = Slice.Repeat(0xFF, 10);

		/// <summary>Find the location of the VersionStamp in a key or value</summary>
		/// <param name="buffer">Buffer that must contains <paramref name="token"/> once and only once</param>
		/// <param name="token">Token that represents the VersionStamp</param>
		/// <param name="argName"></param>
		/// <returns>Offset in <paramref name="buffer"/> where the stamp was found</returns>
		private static int GetVersionStampOffset(ReadOnlySpan<byte> buffer, ReadOnlySpan<byte> token, string argName)
		{
			// the buffer MUST contain one incomplete stamp, either the random token of the current transaction or the default token (all-FF)

			if (buffer.Length < token.Length)
			{
				throw new ArgumentException("The key is too small to contain a VersionStamp.", argName);
			}

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
				p = buffer.IndexOf(IncompleteToken.Span);
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
		private static NotSupportedException FailVersionStampNotSupported(int apiVersion) => new($"VersionStamps are not supported at API version {apiVersion}. You need to select at least API Version 400 or above.");

		/// <summary>Set the <paramref name="value"/> of the <paramref name="key"/> in the database, with the <see cref="VersionStamp"/> replaced by the resolved version at commit time.</summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="key">Name of the key whose value is to be mutated. This key must contain a single <see cref="VersionStamp"/>, whose position will be automatically detected.</param>
		/// <param name="value">New value for this key.</param>
		public static void SetVersionStampedKey(this IFdbTransaction trans, Slice key, Slice value)
			=> SetVersionStampedKey(trans, ToSpanKey(key), ToSpanValue(value));

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
				using var writer = new SliceWriter(key.Length + 2, ArrayPool<byte>.Shared);
				writer.WriteBytes(key);
				writer.WriteUInt16(checked((ushort) offset)); // 16-bits little endian
				trans.Atomic(writer.ToSpan(), value, FdbMutationType.VersionStampedKey);
			}
			else
			{ // starting from 520, the offset is 32 bits
				using var writer = new SliceWriter(key.Length + 4, ArrayPool<byte>.Shared);
				writer.WriteBytes(key);
				writer.WriteUInt32(checked((uint) offset)); // 32-bits little endian
				trans.Atomic(writer.ToSpan(), value, FdbMutationType.VersionStampedKey);
			}

		}

		/// <summary>Set the <paramref name="value"/> of the <paramref name="key"/> in the database, with the <see cref="VersionStamp"/> replaced by the resolved version at commit time.</summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="key">Name of the key whose value is to be mutated. This key must contain a single <see cref="VersionStamp"/>, whose start is defined by <paramref name="stampOffset"/>.</param>
		/// <param name="stampOffset">Offset in <paramref name="key"/> where the 80-bit VersionStamp is located.</param>
		/// <param name="value">New value for this key.</param>
		public static void SetVersionStampedKey(this IFdbTransaction trans, Slice key, int stampOffset, Slice value)
			=> SetVersionStampedKey(trans, ToSpanKey(key), stampOffset, ToSpanValue(value));

		/// <summary>Set the <paramref name="value"/> of the <paramref name="key"/> in the database, with the <see cref="VersionStamp"/> replaced by the resolved version at commit time.</summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="key">Name of the key whose value is to be mutated. This key must contain a single <see cref="VersionStamp"/>, whose start is defined by <paramref name="stampOffset"/>.</param>
		/// <param name="stampOffset">Offset in <paramref name="key"/> where the 80-bit VersionStamp is located.</param>
		/// <param name="value">New value for this key.</param>
		public static void SetVersionStampedKey(this IFdbTransaction trans, ReadOnlySpan<byte> key, int stampOffset, ReadOnlySpan<byte> value)
		{
			Contract.NotNull(trans);
			Contract.Positive(stampOffset);
			if (stampOffset > key.Length - 10)
			{
				throw new ArgumentException("The VersionStamp overflows past the end of the key.", nameof(stampOffset));
			}

			int apiVer = trans.Context.GetApiVersion();
			if (apiVer < 400)
			{ // introduced in 400
				throw FailVersionStampNotSupported(apiVer);
			}

			if (apiVer < 520)
			{ // prior to 520, the offset is only 16-bits
				if (stampOffset > 0xFFFF) throw new ArgumentException("The offset is too large to fit within 16-bits.");
				using var writer = new SliceWriter(key.Length + 2, ArrayPool<byte>.Shared);
				writer.WriteBytes(key);
				writer.WriteUInt16(checked((ushort) stampOffset)); //stored as 32-bits in Little Endian
				trans.Atomic(writer.ToSpan(), value, FdbMutationType.VersionStampedKey);
			}
			else
			{ // starting from 520, the offset is 32 bits
				using var writer = new SliceWriter(key.Length + 4, ArrayPool<byte>.Shared);
				writer.WriteBytes(key);
				writer.WriteUInt32(checked((uint) stampOffset)); //stored as 32-bits in Little Endian
				trans.Atomic(writer.ToSpan(), value, FdbMutationType.VersionStampedKey);
			}
		}

		/// <summary>Set the <paramref name="value"/> of the <paramref name="key"/> in the database, with the first 10 bytes overwritten with the transaction's <see cref="VersionStamp"/>.</summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="key">Name of the key whose value is to be mutated.</param>
		/// <param name="value">Value whose first 10 bytes will be overwritten by the database with the resolved VersionStamp at commit time. The rest of the value will be untouched.</param>
		public static void SetVersionStampedValue(this IFdbTransaction trans, Slice key, Slice value)
			=> SetVersionStampedValue(trans, ToSpanKey(key), ToSpanValue(value));

		/// <summary>Set the <paramref name="value"/> of the <paramref name="key"/> in the database, with the first 10 bytes overwritten with the transaction's <see cref="VersionStamp"/>.</summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="key">Name of the key whose value is to be mutated.</param>
		/// <param name="value">Value whose first 10 bytes will be overwritten by the database with the resolved VersionStamp at commit time. The rest of the value will be untouched.</param>
		public static void SetVersionStampedValue(this IFdbTransaction trans, ReadOnlySpan<byte> key, ReadOnlySpan<byte> value)
		{
			Contract.NotNull(trans);
			if (value.Length < 10)
			{
				throw new ArgumentException("The value must be at least 10 bytes long.", nameof(value));
			}

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

				using var writer = new SliceWriter(value.Length + 4, ArrayPool<byte>.Shared);
				writer.WriteBytes(value);
				writer.WriteUInt32(checked((uint) offset));
				trans.Atomic(key, writer.ToSpan(), FdbMutationType.VersionStampedValue);
			}
		}

		/// <summary>Set the <paramref name="value"/> of the <paramref name="key"/> in the database, with the first 10 bytes overwritten with the transaction's <see cref="VersionStamp"/>.</summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="key">Name of the key whose value is to be mutated.</param>
		/// <param name="value">Value of the key. The 10 bytes starting at <paramref name="stampOffset"/> will be overwritten by the database with the resolved VersionStamp at commit time. The rest of the value will be untouched.</param>
		/// <param name="stampOffset">Offset in <paramref name="value"/> where the 80-bit VersionStamp is located. Prior to API version 520, it can only be located at offset 0.</param>
		public static void SetVersionStampedValue(this IFdbTransaction trans, Slice key, Slice value, int stampOffset)
			=> SetVersionStampedValue(trans, ToSpanKey(key), ToSpanValue(value), stampOffset);

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
				using var writer = new SliceWriter(value.Length + 4, ArrayPool<byte>.Shared);
				writer.WriteBytes(value);
				writer.WriteUInt32(checked((uint) stampOffset));
				trans.Atomic(key, writer.ToSpan(), FdbMutationType.VersionStampedValue);
			}

		}

		#endregion

		#region GetRange...

		/// <summary>Create a new range query that will read all key-value pairs in the database snapshot represented by the transaction</summary>
		public static IFdbKeyValueRangeQuery GetRange(this IFdbReadOnlyTransaction trans, KeyRange range, FdbRangeOptions? options = null)
		{
			var sp = KeySelectorPair.Create(range);
			return trans.GetRange(sp.Begin, sp.End, options);
		}

		/// <summary>Create a new range query that will read all key-value pairs in the database snapshot represented by the transaction</summary>
		public static IFdbKeyValueRangeQuery GetRange(this IFdbReadOnlyTransaction trans, Slice beginKeyInclusive, Slice endKeyExclusive, FdbRangeOptions? options = null)
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
		public static IFdbKeyValueRangeQuery GetRange(this IFdbReadOnlyTransaction trans, KeySelectorPair range, FdbRangeOptions? options = null)
		{
			Contract.NotNull(trans);

			return trans.GetRange(range.Begin, range.End, options);
		}

		#endregion

		#region GetRange<T>...

		/// <summary>Create a new range query that will read all key-value pairs in the database snapshot represented by the transaction</summary>
		public static IFdbRangeQuery<TResult> GetRange<TResult>(this IFdbReadOnlyTransaction trans, KeyRange range, Func<KeyValuePair<Slice, Slice>, TResult> transform, FdbRangeOptions? options = null)
		{
			return GetRange(trans, KeySelectorPair.Create(range), transform, options);
		}

		/// <summary>Create a new range query that will read all key-value pairs in the database snapshot represented by the transaction</summary>
		public static IFdbRangeQuery<TResult> GetRange<TResult>(this IFdbReadOnlyTransaction trans, Slice beginKeyInclusive, Slice endKeyExclusive, Func<KeyValuePair<Slice, Slice>, TResult> transform, FdbRangeOptions? options = null)
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
		public static IFdbRangeQuery<TResult> GetRange<TState, TResult>(this IFdbReadOnlyTransaction trans, Slice beginKeyInclusive, Slice endKeyExclusive, TState state, FdbKeyValueDecoder<TState, TResult> decoder, FdbRangeOptions? options = null)
		{
			Contract.NotNull(trans);
			Contract.NotNull(decoder);

			if (beginKeyInclusive.IsNullOrEmpty) beginKeyInclusive = FdbKey.MinValue;
			if (endKeyExclusive.IsNullOrEmpty) endKeyExclusive = FdbKey.MaxValue;

			return trans.GetRange(
				KeySelector.FirstGreaterOrEqual(beginKeyInclusive),
				KeySelector.FirstGreaterOrEqual(endKeyExclusive),
				state,
				decoder,
				options
			);
		}

		/// <summary>Create a new range query that will read all key-value pairs in the database snapshot represented by the transaction</summary>
		public static IFdbRangeQuery<TResult> GetRange<TResult>(this IFdbReadOnlyTransaction trans, KeySelectorPair range, Func<KeyValuePair<Slice, Slice>, TResult> transform, FdbRangeOptions? options = null)
		{
			Contract.NotNull(trans);

			return trans.GetRange(range.Begin, range.End, transform, options);
		}

		#endregion

		#region GetRangeKeys...

		/// <summary>Create a new range query that will read the keys of all key-value pairs in the database snapshot represented by the transaction</summary>
		[Pure, LinqTunnel]
		public static IFdbRangeQuery<Slice> GetRangeKeys(this IFdbReadOnlyTransaction trans, KeySelector beginInclusive, KeySelector endExclusive, FdbRangeOptions? options = null)
		{
			Contract.NotNull(trans);
			return trans.GetRange(
				beginInclusive,
				endExclusive,
				new SliceBuffer(),
				static (s, k, _) => s.Intern(k),
				options?.OnlyKeys() ?? FdbRangeOptions.KeysOnly
			);
		}

		/// <summary>Create a new range query that will read the keys of all key-value pairs in the database snapshot represented by the transaction</summary>
		[Pure, LinqTunnel]
		public static IFdbRangeQuery<Slice> GetRangeKeys(this IFdbReadOnlyTransaction trans, KeyRange range, FdbRangeOptions? options = null)
		{
			Contract.NotNull(trans);
			var selectors = KeySelectorPair.Create(range);
			return trans.GetRange(
				selectors.Begin, 
				selectors.End, 
				new SliceBuffer(),
				static (s, k, _) => s.Intern(k),
				options?.OnlyKeys() ?? FdbRangeOptions.KeysOnly
			);
		}

		/// <summary>Create a new range query that will read the keys of all key-value pairs in the database snapshot represented by the transaction</summary>
		[Pure, LinqTunnel]
		public static IFdbRangeQuery<Slice> GetRangeKeys(this IFdbReadOnlyTransaction trans, Slice beginKeyInclusive, Slice endKeyExclusive, FdbRangeOptions? options = null)
		{
			Contract.NotNull(trans);
			return trans.GetRange(
				KeySelector.FirstGreaterOrEqual(beginKeyInclusive.IsNullOrEmpty ? FdbKey.MinValue : beginKeyInclusive),
				KeySelector.FirstGreaterOrEqual(endKeyExclusive.IsNullOrEmpty ? FdbKey.MaxValue : endKeyExclusive),
				new SliceBuffer(),
				static (s, k, _) => s.Intern(k),
				options?.OnlyKeys() ?? FdbRangeOptions.KeysOnly
			);
		}

		/// <summary>Create a new range query that will read the keys of all key-value pairs in the database snapshot represented by the transaction</summary>
		[Pure, LinqTunnel]
		public static IFdbRangeQuery<Slice> GetRangeKeys(this IFdbReadOnlyTransaction trans, KeySelectorPair range, FdbRangeOptions? options = null)
		{
			Contract.NotNull(trans);
			return trans.GetRange(
				range.Begin,
				range.End,
				new SliceBuffer(),
				static (s, k, _) => s.Intern(k),
				options?.OnlyKeys() ?? FdbRangeOptions.KeysOnly
			);
		}

		#endregion

		#region GetRangeValues...

		/// <summary>Create a new range query that will read the values of all key-value pairs in the database snapshot represented by the transaction</summary>
		[Pure, LinqTunnel]
		public static IFdbRangeQuery<Slice> GetRangeValues(this IFdbReadOnlyTransaction trans, KeySelector beginInclusive, KeySelector endExclusive, FdbRangeOptions? options = null)
		{
			Contract.NotNull(trans);
			return trans.GetRange(
				beginInclusive,
				endExclusive,
				new SliceBuffer(),
				static (s, _, v) => s.Intern(v),
				options?.OnlyValues() ?? FdbRangeOptions.ValuesOnly
			);
		}

		/// <summary>Create a new range query that will read the values of all key-value pairs in the database snapshot represented by the transaction</summary>
		[Pure, LinqTunnel]
		public static IFdbRangeQuery<Slice> GetRangeValues(this IFdbReadOnlyTransaction trans, KeyRange range, FdbRangeOptions? options = null)
		{
			Contract.NotNull(trans);
			var selectors = KeySelectorPair.Create(range);
			return trans.GetRange(
				selectors.Begin,
				selectors.End,
				new SliceBuffer(),
				static (s, _, v) => s.Intern(v),
				options?.OnlyValues() ?? FdbRangeOptions.ValuesOnly
			);
		}

		/// <summary>Create a new range query that will read the values of all key-value pairs in the database snapshot represented by the transaction</summary>
		[Pure, LinqTunnel]
		public static IFdbRangeQuery<Slice> GetRangeValues(this IFdbReadOnlyTransaction trans, Slice beginKeyInclusive, Slice endKeyExclusive, FdbRangeOptions? options = null)
		{
			Contract.NotNull(trans);
			return trans.GetRange(
				KeySelector.FirstGreaterOrEqual(beginKeyInclusive.IsNullOrEmpty ? FdbKey.MinValue : beginKeyInclusive),
				KeySelector.FirstGreaterOrEqual(endKeyExclusive.IsNullOrEmpty ? FdbKey.MaxValue : endKeyExclusive),
				new SliceBuffer(),
				static (s, _, v) => s.Intern(v),
				options?.OnlyValues() ?? FdbRangeOptions.ValuesOnly
			);
		}

		/// <summary>Create a new range query that will read the values of all key-value pairs in the database snapshot represented by the transaction</summary>
		[Pure, LinqTunnel]
		public static IFdbRangeQuery<Slice> GetRangeValues(this IFdbReadOnlyTransaction trans, KeySelectorPair range, FdbRangeOptions? options = null)
		{
			Contract.NotNull(trans);
			return trans.GetRange(
				range.Begin,
				range.End,
				new SliceBuffer(),
				static (s, _, v) => s.Intern(v),
				options?.OnlyValues() ?? FdbRangeOptions.ValuesOnly
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
		public static Task<FdbRangeChunk> GetRangeAsync(this IFdbReadOnlyTransaction trans, Slice beginInclusive, Slice endExclusive, FdbRangeOptions? options = null, int iteration = 0)
		{
			var range = KeySelectorPair.Create(beginInclusive, endExclusive);
			return trans.GetRangeAsync(range.Begin, range.End, options, iteration);
		}

		/// <summary>
		/// Reads all key-value pairs in the database snapshot represented by transaction (potentially limited by Limit, TargetBytes, or Mode)
		/// which have a key lexicographically greater than or equal to the key resolved by the begin key selector
		/// and lexicographically less than the key resolved by the end key selector.
		/// </summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="beginInclusive">Key defining the beginning (inclusive) of the range</param>
		/// <param name="endExclusive">Key defining the end (exclusive) of the range</param>
		/// <param name="state">State that will be forwarded to the <paramref name="decoder"/></param>
		/// <param name="decoder">Decoder that will extract the result from the value found in the database</param>
		/// <param name="options">Optional query options (Limit, TargetBytes, Mode, Reverse, ...)</param>
		/// <param name="iteration">If streaming mode is FdbStreamingMode.Iterator, this parameter should start at 1 and be incremented by 1 for each successive call while reading this range. In all other cases it is ignored.</param>
		/// <returns></returns>
		public static Task<FdbRangeChunk<TResult>> GetRangeAsync<TState, TResult>(this IFdbReadOnlyTransaction trans, Slice beginInclusive, Slice endExclusive, TState state, FdbKeyValueDecoder<TState, TResult> decoder, FdbRangeOptions? options, int iteration = 0)
		{
			var range = KeySelectorPair.Create(beginInclusive, endExclusive);
			return trans.GetRangeAsync<TState, TResult>(range.Begin, range.End, state, decoder, options, iteration);
		}

		#endregion

		#region VisitRangeAsync...

		/// <summary>Visits all key-value pairs in the database snapshot represent by the transaction</summary>
		/// <typeparam name="TState">Type of the state that is passed to the handler</typeparam>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="range">Range of keys defining the beginning (inclusive) and the end (exclusive) of the range</param>
		/// <param name="state">State that will be forwarded to the <paramref name="visitor"/></param>
		/// <param name="visitor">Lambda called for each key-value pair, in order.</param>
		/// <param name="options">Optional query options (Limit, TargetBytes, Mode, Reverse, ...)</param>
		/// <returns></returns>
		public static Task VisitRangeAsync<TState>(this IFdbReadOnlyTransaction trans, KeyRange range, TState state, FdbKeyValueAction<TState> visitor, FdbRangeOptions? options = null)
		{
			return VisitRangeAsync(trans, range.Begin, range.End, state, visitor, options);
		}

		/// <summary>Visits all key-value pairs in the database snapshot represent by the transaction</summary>
		/// <typeparam name="TState">Type of the state that is passed to the handler</typeparam>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="beginInclusive">Key defining the beginning (inclusive) of the range</param>
		/// <param name="endExclusive">Key defining the end (exclusive) of the range</param>
		/// <param name="state">State that will be forwarded to the <paramref name="visitor"/></param>
		/// <param name="visitor">Lambda called for each key-value pair, in order.</param>
		/// <param name="options">Optional query options (Limit, TargetBytes, Mode, Reverse, ...)</param>
		/// <returns></returns>
		public static Task VisitRangeAsync<TState>(this IFdbReadOnlyTransaction trans, Slice beginInclusive, Slice endExclusive, TState state, FdbKeyValueAction<TState> visitor, FdbRangeOptions? options = null)
		{
			Contract.NotNull(trans);

			if (beginInclusive.IsNullOrEmpty) beginInclusive = FdbKey.MinValue;
			if (endExclusive.IsNullOrEmpty) endExclusive = FdbKey.MaxValue;

			return trans.VisitRangeAsync(
				KeySelector.FirstGreaterOrEqual(beginInclusive),
				KeySelector.FirstGreaterOrEqual(endExclusive),
				state,
				visitor,
				options
			);
		}

		#endregion

		#region GetAddressesForKeyAsync...

		/// <summary>Returns a list of public network addresses as strings, one for each of the storage servers responsible for storing <paramref name="key"/> and its associated value</summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="key">Name of the key whose location is to be queried.</param>
		/// <returns>Task that will return an array of strings, or an exception</returns>
		/// <remarks><para>Starting from API level 630, the addresses include the port ("IP:PORT") by default. Below 630 it does not include the port, unless option <see cref="FdbTransactionOption.IncludePortInAddress"/> is set.</para></remarks>
		public static Task<string[]> GetAddressesForKeyAsync(this IFdbReadOnlyTransaction trans, Slice key)
		{
			return trans.GetAddressesForKeyAsync(ToSpanKey(key));
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
		public static Task<Slice[]> GetRangeSplitPointsAsync(this IFdbReadOnlyTransaction trans, Slice beginKey, Slice endKey, long chunkSize)
		{
			return trans.GetRangeSplitPointsAsync(ToSpanKey(beginKey), ToSpanKey(endKey), chunkSize);
		}

		#endregion

		#region GetEstimatedRangeSizeBytesAsync...

		/// <summary>Returns an estimated byte size of the key range.</summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="beginKey">Name of the key of the start of the range</param>
		/// <param name="endKey">Name of the key of the end of the range</param>
		/// <returns>Task that will return an estimated byte size of the key range, or an exception</returns>
		/// <remarks>The estimated size is calculated based on the sampling done by FDB server. The sampling algorithm works roughly in this way: the larger the key-value pair is, the more likely it would be sampled and the more accurate its sampled size would be. And due to that reason it is recommended to use this API to query against large ranges for accuracy considerations. For a rough reference, if the returned size is larger than 3MB, one can consider the size to be accurate.</remarks>
		public static Task<long> GetEstimatedRangeSizeBytesAsync(this IFdbReadOnlyTransaction trans, Slice beginKey, Slice endKey)
		{
			return trans.GetEstimatedRangeSizeBytesAsync(ToSpanKey(beginKey), ToSpanKey(endKey));
		}

		/// <summary>Returns an estimated byte size of the key range.</summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="range">Range of keys</param>
		/// <returns>Task that will return an estimated byte size of the key range, or an exception</returns>
		/// <remarks>The estimated size is calculated based on the sampling done by FDB server. The sampling algorithm works roughly in this way: the larger the key-value pair is, the more likely it would be sampled and the more accurate its sampled size would be. And due to that reason it is recommended to use this API to query against large ranges for accuracy considerations. For a rough reference, if the returned size is larger than 3MB, one can consider the size to be accurate.</remarks>
		public static Task<long> GetEstimatedRangeSizeBytesAsync(this IFdbReadOnlyTransaction trans, KeyRange range)
		{
			return trans.GetEstimatedRangeSizeBytesAsync(ToSpanKey(range.Begin), ToSpanKey(range.End));
		}

		#endregion

		#region CheckValueAsync...

		/// <summary>Checks if the value from the database snapshot represented by the current transaction is equal to some <paramref name="expected"/> value.</summary>
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
			return trans.CheckValueAsync(ToSpanKey(key), expected);
		}

		#endregion

		#region Clear...

		/// <inheritdoc cref="IFdbTransaction.Clear"/>
		public static void Clear(this IFdbTransaction trans, Slice key)
		{
			trans.Clear(ToSpanKey(key));
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

			trans.ClearRange(ToSpanKey(range.Begin), range.End.HasValue ? ToSpanKey(range.End) : FdbKey.MaxValueSpan);
		}

		/// <inheritdoc cref="IFdbTransaction.ClearRange"/>
		public static void ClearRange(this IFdbTransaction trans, Slice beginKeyInclusive, Slice endKeyExclusive)
		{
			trans.ClearRange(ToSpanKey(beginKeyInclusive), ToSpanKey(endKeyExclusive));
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
			trans.AddConflictRange(ToSpanKey(beginKeyInclusive), ToSpanKey(endKeyExclusive), type);
		}

		/// <summary>
		/// Adds a conflict range to a transaction without performing the associated read or write.
		/// </summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="range">Range of the keys specifying the conflict range. The end key is excluded</param>
		/// <param name="type">One of the FDBConflictRangeType values indicating what type of conflict range is being set.</param>
		public static void AddConflictRange(this IFdbTransaction trans, KeyRange range, FdbConflictRangeType type)
		{
			trans.AddConflictRange(ToSpanKey(range.Begin), range.End.HasValue ? ToSpanKey(range.End) : FdbKey.MaxValueSpan, type);
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
			return trans.Watch(ToSpanKey(key), ct);
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
		/// <param name="decoder">Decoder used to decode the results into values of type <typeparamref name="TValue"/></param>
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
		/// <param name="decoder">Decoder used to decode the results into values of type <typeparamref name="TValue"/></param>
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
		/// <param name="decoder">Decoder used to decode the results into values of type <typeparamref name="TValue"/></param>
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
		public static Task<List<T>> QueryAsync<T>(this IFdbReadOnlyRetryable db, [InstantHandle] Func<IFdbReadOnlyTransaction, IAsyncQuery<T>> handler, CancellationToken ct)
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
		public static Task<List<T>> QueryAsync<T>(this IFdbReadOnlyRetryable db, [InstantHandle] Func<IFdbReadOnlyTransaction, Task<IAsyncQuery<T>>> handler, CancellationToken ct)
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

		/// <summary>Runs a query inside a read-only transaction context, with retry-logic.</summary>
		/// <param name="db">Database used for the operation</param>
		/// <param name="handler">Lambda function that returns an async enumerable. The function may be called multiple times if the transaction conflicts.</param>
		/// <param name="ct">Token used to cancel the operation</param>
		/// <returns>Task returning the list of all the elements of the async enumerable returned by the last successful call to <paramref name="handler"/>.</returns>
		public static Task<List<T>> QueryAsync<T>(this IFdbReadOnlyRetryable db, [InstantHandle] Func<IFdbReadOnlyTransaction, Task<IAsyncLinqQuery<T>>> handler, CancellationToken ct)
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

		/// <summary>Runs a query inside a read-only transaction context, with retry-logic.</summary>
		/// <param name="db">Database used for the operation</param>
		/// <param name="handler">Lambda function that returns an async enumerable. The function may be called multiple times if the transaction conflicts.</param>
		/// <param name="ct">Token used to cancel the operation</param>
		/// <returns>Task returning the list of all the elements of the async enumerable returned by the last successful call to <paramref name="handler"/>.</returns>
		public static Task<List<T>> QueryAsync<T>(this IFdbReadOnlyRetryable db, [InstantHandle] Func<IFdbReadOnlyTransaction, IAsyncEnumerable<T>> handler, CancellationToken ct)
		{
			Contract.NotNull(db);
			Contract.NotNull(handler);

			return db.ReadAsync(async (tr) =>
			{
				var query = handler(tr) ?? throw new InvalidOperationException("The query handler returned a null sequence");
#if NET10_0_OR_GREATER
				return await query.ToListAsync(ct).ConfigureAwait(false);
#else
				return await query.ToAsyncQuery(ct).ToListAsync().ConfigureAwait(false);
#endif
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
#if NET10_0_OR_GREATER
				return await query.ToListAsync(ct).ConfigureAwait(false);
#else
				return await query.ToAsyncQuery(ct).ToListAsync().ConfigureAwait(false);
#endif
			}, ct);
		}

		#endregion

	}
}
