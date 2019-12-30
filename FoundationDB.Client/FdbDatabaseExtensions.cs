#region BSD License
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
	using System.Threading;
	using System.Threading.Tasks;
	using Doxense.Diagnostics.Contracts;
	using JetBrains.Annotations;

	/// <summary>Provides a set of extensions methods shared by all FoundationDB database implementations.</summary>
	[PublicAPI]
	public static class FdbDatabaseExtensions
	{

		#region Transactions...

		/// <summary>Start a new read-only transaction on this database</summary>
		/// <param name="db">Database instance</param>
		/// <param name="ct">Optional cancellation token that can abort all pending async operations started by this transaction.</param>
		/// <returns>New transaction instance that can read from the database.</returns>
		/// <remarks>You MUST call Dispose() on the transaction when you are done with it. You SHOULD wrap it in a 'using' statement to ensure that it is disposed in all cases.</remarks>
		/// <example>
		/// <code>
		/// using(var tr = db.BeginReadOnlyTransaction(CancellationToken.None))
		/// {
		///		var result = await tr.Get(Slice.FromString("Hello"));
		///		var items = await tr.GetRange(KeyRange.StartsWith(Slice.FromString("ABC"))).ToListAsync();
		/// }
		/// </code>
		/// </example>
		[Pure, ItemNotNull]
		public static async ValueTask<IFdbReadOnlyTransaction> BeginReadOnlyTransactionAsync([NotNull] this IFdbDatabase db, CancellationToken ct)
		{
			Contract.NotNull(db, nameof(db));
			return await db.BeginTransactionAsync(FdbTransactionMode.ReadOnly, ct, default(FdbOperationContext));
		}

		/// <summary>Start a new transaction on this database</summary>
		/// <param name="db">Database instance</param>
		/// <param name="ct">Optional cancellation token that can abort all pending async operations started by this transaction.</param>
		/// <returns>New transaction instance that can read from or write to the database.</returns>
		/// <remarks>You MUST call Dispose() on the transaction when you are done with it. You SHOULD wrap it in a 'using' statement to ensure that it is disposed in all cases.</remarks>
		/// <example>
		/// using(var tr = db.BeginTransaction(CancellationToken.None))
		/// {
		///		tr.Set(Slice.FromString("Hello"), Slice.FromString("World"));
		///		tr.Clear(Slice.FromString("OldValue"));
		///		await tr.CommitAsync();
		/// }</example>
		[Pure, ItemNotNull]
		public static ValueTask<IFdbTransaction> BeginTransactionAsync([NotNull] this IFdbDatabase db, CancellationToken ct)
		{
			Contract.NotNull(db, nameof(db));
			return db.BeginTransactionAsync(FdbTransactionMode.Default, ct, default(FdbOperationContext));
		}

		#endregion

		#region Options...

		/// <summary>Set the size of the client location cache. Raising this value can boost performance in very large databases where clients access data in a near-random pattern. Defaults to 100000.</summary>
		/// <param name="db">Database instance</param>
		/// <param name="size">Max location cache entries</param>
		public static void SetLocationCacheSize([NotNull] this IFdbDatabase db, int size)
		{
			Contract.NotNull(db, nameof(db));
			if (size < 0) throw new FdbException(FdbError.InvalidOptionValue, "Location cache size must be a positive integer");

			//REVIEW: we can't really change this to a Property, because we don't have a way to get the current value for the getter, and set only properties are weird...
			//TODO: cache this into a local variable ?
			db.SetOption(FdbDatabaseOption.LocationCacheSize, size);
		}

		/// <summary>Set the maximum number of watches allowed to be outstanding on a database connection. Increasing this number could result in increased resource usage. Reducing this number will not cancel any outstanding watches. Defaults to 10000 and cannot be larger than 1000000.</summary>
		/// <param name="db">Database instance</param>
		/// <param name="count">Max outstanding watches</param>
		public static void SetMaxWatches([NotNull] this IFdbDatabase db, int count)
		{
			Contract.NotNull(db, nameof(db));
			if (count < 0) throw new FdbException(FdbError.InvalidOptionValue, "Maximum outstanding watches count must be a positive integer");

			//REVIEW: we can't really change this to a Property, because we don't have a way to get the current value for the getter, and set only properties are weird...
			//TODO: cache this into a local variable ?
			db.SetOption(FdbDatabaseOption.MaxWatches, count);
		}

		/// <summary>Specify the machine ID that was passed to fdbserver processes running on the same machine as this client, for better location-aware load balancing.</summary>
		/// <param name="db">Database instance</param>
		/// <param name="hexId">Hexadecimal ID</param>
		public static void SetMachineId([NotNull] this IFdbDatabase db, string hexId)
		{
			Contract.NotNull(db, nameof(db));
			//REVIEW: we can't really change this to a Property, because we don't have a way to get the current value for the getter, and set only properties are weird...
			//TODO: cache this into a local variable ?
			db.SetOption(FdbDatabaseOption.MachineId, hexId);
		}

		/// <summary>Specify the datacenter ID that was passed to fdbserver processes running in the same datacenter as this client, for better location-aware load balancing.</summary>
		/// <param name="db">Database instance</param>
		/// <param name="hexId">Hexadecimal ID</param>
		public static void SetDataCenterId([NotNull] this IFdbDatabase db, string hexId)
		{
			Contract.NotNull(db, nameof(db));
			//REVIEW: we can't really change this to a Property, because we don't have a way to get the current value for the getter, and set only properties are weird...
			//TODO: cache this into a local variable ?
			db.SetOption(FdbDatabaseOption.DataCenterId, hexId);
		}

		#endregion

		#region Key Validation...

		/// <summary>Test if a key is allowed to be used with this database instance</summary>
		/// <param name="db">Database instance</param>
		/// <param name="key">Key to test</param>
		/// <returns>Returns true if the key is not null or empty, does not exceed the maximum key size, and is contained in the global key space of this database instance. Otherwise, returns false.</returns>
		[Pure]
		public static bool IsKeyValid([NotNull] this IFdbDatabase db, in Slice key)
		{
			Exception _;
			return FdbDatabase.ValidateKey(db, in key, false, true, out _);
		}

		/// <summary>Checks that a key is inside the global namespace of this database, and contained in the optional legal key space specified by the user</summary>
		/// <param name="db">Database instance</param>
		/// <param name="key">Key to verify</param>
		/// <param name="endExclusive">If true, the key is allowed to be one past the maximum key allowed by the global namespace</param>
		/// <exception cref="FdbException">If the key is outside of the allowed keyspace, throws an FdbException with code FdbError.KeyOutsideLegalRange</exception>
		internal static void EnsureKeyIsValid([NotNull] this IFdbDatabase db, in Slice key, bool endExclusive = false)
		{
			if (!FdbDatabase.ValidateKey(db, in key, endExclusive, false, out Exception ex)) throw ex;
		}

		/// <summary>Checks that a key is inside the global namespace of this database, and contained in the optional legal key space specified by the user</summary>
		/// <param name="db">Database instance</param>
		/// <param name="key">Key to verify</param>
		/// <param name="endExclusive">If true, the key is allowed to be one past the maximum key allowed by the global namespace</param>
		/// <exception cref="FdbException">If the key is outside of the allowed keyspace, throws an FdbException with code FdbError.KeyOutsideLegalRange</exception>
		internal static void EnsureKeyIsValid([NotNull] this IFdbDatabase db, ReadOnlySpan<byte> key, bool endExclusive = false)
		{
			Exception ex;
			if (!FdbDatabase.ValidateKey(db, key, endExclusive, false, out ex)) throw ex;
		}

		/// <summary>Checks that one or more keys are inside the global namespace of this database, and contained in the optional legal key space specified by the user</summary>
		/// <param name="db">Database instance</param>
		/// <param name="keys">Array of keys to verify</param>
		/// <param name="endExclusive">If true, the keys are allowed to be one past the maximum key allowed by the global namespace</param>
		/// <exception cref="FdbException">If at least on key is outside of the allowed keyspace, throws an FdbException with code FdbError.KeyOutsideLegalRange</exception>
		internal static void EnsureKeysAreValid([NotNull] this IFdbDatabase db, Slice[] keys, bool endExclusive = false)
		{
			Contract.NotNull(keys, nameof(keys));
			for (int i = 0; i < keys.Length; i++)
			{
				Exception ex;
				if (!FdbDatabase.ValidateKey(db, in keys[i], endExclusive, false, out ex)) throw ex;
			}
		}

		/// <summary>Remove the global namespace prefix of this database form the key, and return the rest of the bytes, or Slice.Nil is the key is outside the namespace</summary>
		/// <param name="db">Database instance</param>
		/// <param name="keyAbsolute">Binary key that starts with the namespace prefix, followed by some bytes</param>
		/// <returns>Binary key that contain only the bytes after the namespace prefix</returns>
		/// <example>
		/// // db with namespace prefix equal to"&lt;02&gt;Foo&lt;00&gt;"
		/// db.Extract('&lt;02&gt;Foo&lt;00&gt;&lt;02&gt;Bar&lt;00&gt;') => '&gt;&lt;02&gt;Bar&lt;00&gt;'
		/// db.Extract('&lt;02&gt;Foo&lt;00&gt;') => Slice.Empty
		/// db.Extract('&lt;02&gt;TopSecret&lt;00&gt;&lt;02&gt;Password&lt;00&gt;') => Slice.Nil
		/// db.Extract(Slice.Nil) => Slice.Nil
		/// </example>
		[Pure]
		public static Slice Extract([NotNull] this IFdbDatabase db, Slice keyAbsolute)
		{
			return db.GlobalSpace.ExtractKey(keyAbsolute);
		}

		#endregion

		#region Standard Operations

		//REVIEW: this is too dangerous!
		// Users may call GetAsync() or SetAsync() multiple times, outside of a transaction !!!

		/// <summary>Read a single key from the database, using a dedicated transaction.</summary>
		/// <remarks>
		/// Use this method only if you intend to perform a single operation inside your execution context (ex: HTTP request).
		/// If you need to read several keys at once, use a version of <see cref="GetValuesAsync(FoundationDB.Client.IFdbReadOnlyRetryable,System.Collections.Generic.IEnumerable{System.Slice},System.Threading.CancellationToken)"/>.
		/// If you need to combine multiple read or write operations, consider using on of the multiple <see cref="IFdbReadOnlyRetryable.ReadAsync{TResult}(System.Func{FoundationDB.Client.IFdbReadOnlyTransaction,System.Threading.Tasks.Task{TResult}},System.Threading.CancellationToken)"/> or <see cref="IFdbRetryable.ReadWriteAsync{TResult}(System.Func{FoundationDB.Client.IFdbTransaction,System.Threading.Tasks.Task{TResult}},System.Threading.CancellationToken)"/> overrides.
		/// </remarks>
		public static Task<Slice> GetAsync([NotNull] this IFdbReadOnlyRetryable db, Slice key, CancellationToken ct)
		{
			Contract.NotNull(db, nameof(db));
			return db.ReadAsync((tr) => tr.GetAsync(key), ct);
		}

		/// <summary>Read a list of keys from the database, using a dedicated transaction.</summary>
		/// <remarks>
		/// Use this method only if you intend to perform a single operation inside your execution context (ex: HTTP request).
		/// If you need to combine multiple read or write operations, consider using on of the multiple <see cref="IFdbReadOnlyRetryable.ReadAsync{TResult}(System.Func{FoundationDB.Client.IFdbReadOnlyTransaction,System.Threading.Tasks.Task{TResult}},System.Threading.CancellationToken)"/> or <see cref="IFdbRetryable.ReadWriteAsync{TResult}(System.Func{FoundationDB.Client.IFdbTransaction,System.Threading.Tasks.Task{TResult}},System.Threading.CancellationToken)"/> overrides.
		/// </remarks>
		[ItemNotNull]
		public static Task<Slice[]> GetValuesAsync([NotNull] this IFdbReadOnlyRetryable db, [NotNull] Slice[] keys, CancellationToken ct)
		{
			Contract.NotNull(db, nameof(db));
			return db.ReadAsync((tr) => tr.GetValuesAsync(keys), ct);
		}

		/// <summary>Read a sequence of keys from the database, using a dedicated transaction.</summary>
		/// <remarks>
		/// Use this method only if you intend to perform a single operation inside your execution context (ex: HTTP request).
		/// If you need to combine multiple read or write operations, consider using on of the multiple <see cref="IFdbReadOnlyRetryable.ReadAsync{TResult}(System.Func{FoundationDB.Client.IFdbReadOnlyTransaction,System.Threading.Tasks.Task{TResult}},System.Threading.CancellationToken)"/> or <see cref="IFdbRetryable.ReadWriteAsync{TResult}(System.Func{FoundationDB.Client.IFdbTransaction,System.Threading.Tasks.Task{TResult}},System.Threading.CancellationToken)"/> overrides.
		/// </remarks>
		[ItemNotNull]
		public static Task<Slice[]> GetValuesAsync([NotNull] this IFdbReadOnlyRetryable db, [NotNull] IEnumerable<Slice> keys, CancellationToken ct)
		{
			Contract.NotNull(db, nameof(db));
			return db.ReadAsync((tr) => tr.GetValuesAsync(keys), ct);
		}

		/// <summary>Resolve a single key selector from the database, using a dedicated transaction.</summary>
		/// <remarks>
		/// Use this method only if you intend to perform a single operation inside your execution context (ex: HTTP request).
		/// If you need to combine multiple read or write operations, consider using on of the multiple <see cref="IFdbReadOnlyRetryable.ReadAsync{TResult}(System.Func{FoundationDB.Client.IFdbReadOnlyTransaction,System.Threading.Tasks.Task{TResult}},System.Threading.CancellationToken)"/> or <see cref="IFdbRetryable.ReadWriteAsync{TResult}(System.Func{FoundationDB.Client.IFdbTransaction,System.Threading.Tasks.Task{TResult}},System.Threading.CancellationToken)"/> overrides.
		/// </remarks>
		public static Task<Slice> GetKeyAsync([NotNull] this IFdbReadOnlyRetryable db, KeySelector keySelector, CancellationToken ct)
		{
			Contract.NotNull(db, nameof(db));
			return db.ReadAsync((tr) => tr.GetKeyAsync(keySelector), ct);
		}

		/// <summary>Resolve a list of key selectors from the database, using a dedicated transaction.</summary>
		/// <remarks>
		/// Use this method only if you intend to perform a single operation inside your execution context (ex: HTTP request).
		/// If you need to combine multiple read or write operations, consider using on of the multiple <see cref="IFdbReadOnlyRetryable.ReadAsync{TResult}(System.Func{FoundationDB.Client.IFdbReadOnlyTransaction,System.Threading.Tasks.Task{TResult}},System.Threading.CancellationToken)"/> or <see cref="IFdbRetryable.ReadWriteAsync{TResult}(System.Func{FoundationDB.Client.IFdbTransaction,System.Threading.Tasks.Task{TResult}},System.Threading.CancellationToken)"/> overrides.
		/// </remarks>
		[ItemNotNull]
		public static Task<Slice[]> GetKeysAsync([NotNull] this IFdbReadOnlyRetryable db, [NotNull] KeySelector[] keySelectors, CancellationToken ct)
		{
			Contract.NotNull(db, nameof(db));
			Contract.NotNull(keySelectors, nameof(keySelectors));
			return db.ReadAsync((tr) => tr.GetKeysAsync(keySelectors), ct);
		}

		/// <summary>Resolve a sequence of key selectors from the database, using a dedicated transaction.</summary>
		/// <remarks>
		/// Use this method only if you intend to perform a single operation inside your execution context (ex: HTTP request).
		/// If you need to combine multiple read or write operations, consider using on of the multiple <see cref="IFdbReadOnlyRetryable.ReadAsync{TResult}(System.Func{FoundationDB.Client.IFdbReadOnlyTransaction,System.Threading.Tasks.Task{TResult}},System.Threading.CancellationToken)"/> or <see cref="IFdbRetryable.ReadWriteAsync{TResult}(System.Func{FoundationDB.Client.IFdbTransaction,System.Threading.Tasks.Task{TResult}},System.Threading.CancellationToken)"/> overrides.
		/// </remarks>
		[ItemNotNull]
		public static Task<Slice[]> GetKeysAsync([NotNull] this IFdbReadOnlyRetryable db, [NotNull] IEnumerable<KeySelector> keySelectors, CancellationToken ct)
		{
			Contract.NotNull(db, nameof(db));
			Contract.NotNull(keySelectors, nameof(keySelectors));
			return db.ReadAsync((tr) => tr.GetKeysAsync(keySelectors), ct);
		}

		/// <summary>Read a single page of a range query from the database, using a dedicated transaction.</summary>
		/// <remarks>
		/// Use this method only if you intend to perform a single operation inside your execution context (ex: HTTP request).
		/// If you need to combine multiple read or write operations, consider using on of the multiple <see cref="IFdbReadOnlyRetryable.ReadAsync{TResult}(System.Func{FoundationDB.Client.IFdbReadOnlyTransaction,System.Threading.Tasks.Task{TResult}},System.Threading.CancellationToken)"/> or <see cref="IFdbRetryable.ReadWriteAsync{TResult}(System.Func{FoundationDB.Client.IFdbTransaction,System.Threading.Tasks.Task{TResult}},System.Threading.CancellationToken)"/> overrides.
		/// </remarks>
		public static Task<FdbRangeChunk> GetRangeAsync([NotNull] this IFdbReadOnlyRetryable db, KeySelector beginInclusive, KeySelector endExclusive, FdbRangeOptions options, int iteration, CancellationToken ct)
		{
			Contract.NotNull(db, nameof(db));
			return db.ReadAsync((tr) => tr.GetRangeAsync(beginInclusive, endExclusive, options, iteration), ct);
		}

		/// <summary>Set the value of a single key in the database, using a dedicated transaction.</summary>
		/// <remarks>
		/// Use this method only if you intend to perform a single operation inside your execution context (ex: HTTP request).
		/// If you need to combine multiple read or write operations, consider using on of the multiple <see cref="IFdbRetryable.WriteAsync(System.Action{FoundationDB.Client.IFdbTransaction},System.Threading.CancellationToken)"/> or <see cref="IFdbRetryable.ReadWriteAsync{TResult}(System.Func{FoundationDB.Client.IFdbTransaction,System.Threading.Tasks.Task{TResult}},System.Threading.CancellationToken)"/> overrides.
		/// </remarks>
		public static Task SetAsync([NotNull] this IFdbRetryable db, Slice key, Slice value, CancellationToken ct)
		{
			Contract.NotNull(db, nameof(db));
			return db.WriteAsync((tr) => tr.Set(key, value), ct);
		}

		/// <summary>Set the values of a sequence of keys in the database, using a dedicated transaction.</summary>
		/// <remarks>
		/// Use this method only if you intend to perform a single operation inside your execution context (ex: HTTP request).
		/// If you need to combine multiple read or write operations, consider using on of the multiple <see cref="IFdbRetryable.WriteAsync(System.Action{FoundationDB.Client.IFdbTransaction},System.Threading.CancellationToken)"/> or <see cref="IFdbRetryable.ReadWriteAsync{TResult}(System.Func{FoundationDB.Client.IFdbTransaction,System.Threading.Tasks.Task{TResult}},System.Threading.CancellationToken)"/> overrides.
		/// </remarks>
		public static Task SetValuesAsync([NotNull] this IFdbRetryable db, IEnumerable<KeyValuePair<Slice, Slice>> items, CancellationToken ct)
		{
			Contract.NotNull(db, nameof(db));
			return db.WriteAsync((tr) =>
			{
				foreach (var kv in items)
				{
					tr.Set(kv.Key, kv.Value);
				}
			}, ct);
		}

		/// <summary>Set the values of a sequence of keys in the database, using a dedicated transaction.</summary>
		/// <remarks>
		/// Use this method only if you intend to perform a single operation inside your execution context (ex: HTTP request).
		/// If you need to combine multiple read or write operations, consider using on of the multiple <see cref="IFdbRetryable.WriteAsync(System.Action{FoundationDB.Client.IFdbTransaction},System.Threading.CancellationToken)"/> or <see cref="IFdbRetryable.ReadWriteAsync{TResult}(System.Func{FoundationDB.Client.IFdbTransaction,System.Threading.Tasks.Task{TResult}},System.Threading.CancellationToken)"/> overrides.
		/// </remarks>
		public static Task SetValuesAsync([NotNull] this IFdbRetryable db, IEnumerable<(Slice Key, Slice Value)> items, CancellationToken ct)
		{
			Contract.NotNull(db, nameof(db));
			return db.WriteAsync((tr) =>
			{
				foreach (var kv in items)
				{
					tr.Set(kv.Key, kv.Value);
				}
			}, ct);
		}

		/// <summary>Clear a single key in the database, using a dedicated transaction.</summary>
		/// <remarks>
		/// Use this method only if you intend to perform a single operation inside your execution context (ex: HTTP request).
		/// If you need to combine multiple read or write operations, consider using on of the multiple <see cref="IFdbRetryable.WriteAsync(System.Action{FoundationDB.Client.IFdbTransaction},System.Threading.CancellationToken)"/> or <see cref="IFdbRetryable.ReadWriteAsync{TResult}(System.Func{FoundationDB.Client.IFdbTransaction,System.Threading.Tasks.Task{TResult}},System.Threading.CancellationToken)"/> overrides.
		/// </remarks>
		public static Task ClearAsync([NotNull] this IFdbRetryable db, Slice key, CancellationToken ct)
		{
			Contract.NotNull(db, nameof(db));
			return db.WriteAsync((tr) => tr.Clear(key), ct);
		}

		/// <summary>Clear a single range in the database, using a dedicated transaction.</summary>
		/// <remarks>
		/// Use this method only if you intend to perform a single operation inside your execution context (ex: HTTP request).
		/// If you need to combine multiple read or write operations, consider using on of the multiple <see cref="IFdbRetryable.WriteAsync(System.Action{FoundationDB.Client.IFdbTransaction},System.Threading.CancellationToken)"/> or <see cref="IFdbRetryable.ReadWriteAsync{TResult}(System.Func{FoundationDB.Client.IFdbTransaction,System.Threading.Tasks.Task{TResult}},System.Threading.CancellationToken)"/> overrides.
		/// </remarks>
		public static Task ClearRangeAsync([NotNull] this IFdbRetryable db, Slice beginKeyInclusive, Slice endKeyExclusive, CancellationToken ct)
		{
			Contract.NotNull(db, nameof(db));
			return db.WriteAsync((tr) => tr.ClearRange(beginKeyInclusive, endKeyExclusive), ct);
		}

		/// <summary>Clear a single range in the database, using a dedicated transaction.</summary>
		/// <remarks>
		/// Use this method only if you intend to perform a single operation inside your execution context (ex: HTTP request).
		/// If you need to combine multiple read or write operations, consider using on of the multiple <see cref="IFdbRetryable.WriteAsync(System.Action{FoundationDB.Client.IFdbTransaction},System.Threading.CancellationToken)"/> or <see cref="IFdbRetryable.ReadWriteAsync{TResult}(System.Func{FoundationDB.Client.IFdbTransaction,System.Threading.Tasks.Task{TResult}},System.Threading.CancellationToken)"/> overrides.
		/// </remarks>
		public static Task ClearRangeAsync([NotNull] this IFdbRetryable db, KeyRange range, CancellationToken ct)
		{
			Contract.NotNull(db, nameof(db));
			return db.WriteAsync((tr) => tr.ClearRange(range), ct);
		}

		/// <summary>Atomically add to the value of a single key in the database, using a dedicated transaction.</summary>
		/// <remarks>
		/// Use this method only if you intend to perform a single operation inside your execution context (ex: HTTP request).
		/// If you need to combine multiple read or write operations, consider using on of the multiple <see cref="IFdbRetryable.WriteAsync(System.Action{FoundationDB.Client.IFdbTransaction},System.Threading.CancellationToken)"/> or <see cref="IFdbRetryable.ReadWriteAsync{TResult}(System.Func{FoundationDB.Client.IFdbTransaction,System.Threading.Tasks.Task{TResult}},System.Threading.CancellationToken)"/> overrides.
		/// </remarks>
		public static Task AtomicAdd([NotNull] this IFdbRetryable db, Slice key, Slice value, CancellationToken ct)
		{
			Contract.NotNull(db, nameof(db));
			return db.WriteAsync((tr) => tr.Atomic(key, value, FdbMutationType.Add), ct);
		}

		/// <summary>Atomically compare and optionally clear the value of a single key in the database, using a dedicated transaction.</summary>
		/// <remarks>
		/// Use this method only if you intend to perform a single operation inside your execution context (ex: HTTP request).
		/// If you need to combine multiple read or write operations, consider using on of the multiple <see cref="IFdbRetryable.WriteAsync(System.Action{FoundationDB.Client.IFdbTransaction},System.Threading.CancellationToken)"/> or <see cref="IFdbRetryable.ReadWriteAsync{TResult}(System.Func{FoundationDB.Client.IFdbTransaction,System.Threading.Tasks.Task{TResult}},System.Threading.CancellationToken)"/> overrides.
		/// </remarks>
		public static Task AtomicCompareAndClear([NotNull] this IFdbRetryable db, Slice key, Slice comparand, CancellationToken ct)
		{
			Contract.NotNull(db, nameof(db));
			return db.WriteAsync((tr) => tr.Atomic(key, comparand, FdbMutationType.CompareAndClear), ct);
		}

		/// <summary>Atomically perform a bitwise AND to the value of a single key in the database, using a dedicated transaction.</summary>
		/// <remarks>
		/// Use this method only if you intend to perform a single operation inside your execution context (ex: HTTP request).
		/// If you need to combine multiple read or write operations, consider using on of the multiple <see cref="IFdbRetryable.WriteAsync(System.Action{FoundationDB.Client.IFdbTransaction},System.Threading.CancellationToken)"/> or <see cref="IFdbRetryable.ReadWriteAsync{TResult}(System.Func{FoundationDB.Client.IFdbTransaction,System.Threading.Tasks.Task{TResult}},System.Threading.CancellationToken)"/> overrides.
		/// </remarks>
		public static Task AtomicBitAnd([NotNull] this IFdbRetryable db, Slice key, Slice value, CancellationToken ct)
		{
			Contract.NotNull(db, nameof(db));
			return db.WriteAsync((tr) => tr.Atomic(key, value, FdbMutationType.BitAnd), ct);
		}

		/// <summary>Atomically perform a bitwise OR to the value of a single key in the database, using a dedicated transaction.</summary>
		/// <remarks>
		/// Use this method only if you intend to perform a single operation inside your execution context (ex: HTTP request).
		/// If you need to combine multiple read or write operations, consider using on of the multiple <see cref="IFdbRetryable.WriteAsync(System.Action{FoundationDB.Client.IFdbTransaction},System.Threading.CancellationToken)"/> or <see cref="IFdbRetryable.ReadWriteAsync{TResult}(System.Func{FoundationDB.Client.IFdbTransaction,System.Threading.Tasks.Task{TResult}},System.Threading.CancellationToken)"/> overrides.
		/// </remarks>
		public static Task AtomicBitOr([NotNull] this IFdbRetryable db, Slice key, Slice value, CancellationToken ct)
		{
			Contract.NotNull(db, nameof(db));
			return db.WriteAsync((tr) => tr.Atomic(key, value, FdbMutationType.BitOr), ct);
		}

		/// <summary>Atomically perform a bitwise XOR to the value of a single key in the database, using a dedicated transaction.</summary>
		/// <remarks>
		/// Use this method only if you intend to perform a single operation inside your execution context (ex: HTTP request).
		/// If you need to combine multiple read or write operations, consider using on of the multiple <see cref="IFdbRetryable.WriteAsync(System.Action{FoundationDB.Client.IFdbTransaction},System.Threading.CancellationToken)"/> or <see cref="IFdbRetryable.ReadWriteAsync{TResult}(System.Func{FoundationDB.Client.IFdbTransaction,System.Threading.Tasks.Task{TResult}},System.Threading.CancellationToken)"/> overrides.
		/// </remarks>
		public static Task AtomicBitXor([NotNull] this IFdbRetryable db, Slice key, Slice value, CancellationToken ct)
		{
			Contract.NotNull(db, nameof(db));
			return db.WriteAsync((tr) => tr.Atomic(key, value, FdbMutationType.BitXor), ct);
		}

		/// <summary>Atomically update a value if it is larger than the value in the database, using a dedicated transaction.</summary>
		/// <remarks>
		/// Use this method only if you intend to perform a single operation inside your execution context (ex: HTTP request).
		/// If you need to combine multiple read or write operations, consider using on of the multiple <see cref="IFdbRetryable.WriteAsync(System.Action{FoundationDB.Client.IFdbTransaction},System.Threading.CancellationToken)"/> or <see cref="IFdbRetryable.ReadWriteAsync{TResult}(System.Func{FoundationDB.Client.IFdbTransaction,System.Threading.Tasks.Task{TResult}},System.Threading.CancellationToken)"/> overrides.
		/// </remarks>
		public static Task AtomicMax([NotNull] this IFdbRetryable db, Slice key, Slice value, CancellationToken ct)
		{
			Contract.NotNull(db, nameof(db));
			return db.WriteAsync((tr) => tr.Atomic(key, value, FdbMutationType.Max), ct);
		}

		/// <summary>Atomically update a value if it is smaller than the value in the database, using a dedicated transaction.</summary>
		/// <remarks>
		/// Use this method only if you intend to perform a single operation inside your execution context (ex: HTTP request).
		/// If you need to combine multiple read or write operations, consider using on of the multiple <see cref="IFdbRetryable.WriteAsync(System.Action{FoundationDB.Client.IFdbTransaction},System.Threading.CancellationToken)"/> or <see cref="IFdbRetryable.ReadWriteAsync{TResult}(System.Func{FoundationDB.Client.IFdbTransaction,System.Threading.Tasks.Task{TResult}},System.Threading.CancellationToken)"/> overrides.
		/// </remarks>
		public static Task AtomicMin([NotNull] this IFdbRetryable db, Slice key, Slice value, CancellationToken ct)
		{
			Contract.NotNull(db, nameof(db));
			return db.WriteAsync((tr) => tr.Atomic(key, value, FdbMutationType.Min), ct);
		}

		#endregion

	}

}
