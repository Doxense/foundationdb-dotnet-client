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
	using FoundationDB.Layers.Tuples;
	using JetBrains.Annotations;
	using System;
	using System.Collections.Generic;
	using System.Threading;
	using System.Threading.Tasks;

	/// <summary>Provides a set of extensions methods shared by all FoundationDB database implementations.</summary>
	public static class FdbDatabaseExtensions
	{

		#region Transactions...

		/// <summary>Start a new read-only transaction on this database</summary>
		/// <param name="db">Database instance</param>
		/// <param name="cancellationToken">Optional cancellation token that can abort all pending async operations started by this transaction.</param>
		/// <returns>New transaction instance that can read from the database.</returns>
		/// <remarks>You MUST call Dispose() on the transaction when you are done with it. You SHOULD wrap it in a 'using' statement to ensure that it is disposed in all cases.</remarks>
		/// <example>
		/// using(var tr = db.BeginReadOnlyTransaction(CancellationToken.None))
		/// {
		///		var result = await tr.Get(Slice.FromString("Hello"));
		///		var items = await tr.GetRange(FdbKeyRange.StartsWith(Slice.FromString("ABC"))).ToListAsync();
		/// }</example>
		[NotNull]
		public static IFdbReadOnlyTransaction BeginReadOnlyTransaction(this IFdbDatabase db, CancellationToken cancellationToken)
		{
			if (db == null) throw new ArgumentNullException("db");
			return db.BeginTransaction(FdbTransactionMode.ReadOnly, cancellationToken, default(FdbOperationContext));
		}

		/// <summary>Start a new transaction on this database</summary>
		/// <param name="db">Database instance</param>
		/// <param name="cancellationToken">Optional cancellation token that can abort all pending async operations started by this transaction.</param>
		/// <returns>New transaction instance that can read from or write to the database.</returns>
		/// <remarks>You MUST call Dispose() on the transaction when you are done with it. You SHOULD wrap it in a 'using' statement to ensure that it is disposed in all cases.</remarks>
		/// <example>
		/// using(var tr = db.BeginTransaction(CancellationToken.None))
		/// {
		///		tr.Set(Slice.FromString("Hello"), Slice.FromString("World"));
		///		tr.Clear(Slice.FromString("OldValue"));
		///		await tr.CommitAsync();
		/// }</example>
		[NotNull]
		public static IFdbTransaction BeginTransaction(this IFdbDatabase db, CancellationToken cancellationToken)
		{
			if (db == null) throw new ArgumentNullException("db");
			return db.BeginTransaction(FdbTransactionMode.Default, cancellationToken, default(FdbOperationContext));
		}

		#endregion

		#region Options...

		/// <summary>Set the size of the client location cache. Raising this value can boost performance in very large databases where clients access data in a near-random pattern. Defaults to 100000.</summary>
		/// <param name="db">Database instance</param>
		/// <param name="size">Max location cache entries</param>
		public static void SetLocationCacheSize(this IFdbDatabase db, int size)
		{
			if (db == null) throw new ArgumentNullException("db");
			if (size < 0) throw new FdbException(FdbError.InvalidOptionValue, "Location cache size must be a positive integer");

			//REVIEW: we can't really change this to a Property, because we don't have a way to get the current value for the getter, and set only properties are weird...
			//TODO: cache this into a local variable ?
			db.SetOption(FdbDatabaseOption.LocationCacheSize, size);
		}

		/// <summary>Set the maximum number of watches allowed to be outstanding on a database connection. Increasing this number could result in increased resource usage. Reducing this number will not cancel any outstanding watches. Defaults to 10000 and cannot be larger than 1000000.</summary>
		/// <param name="db">Database instance</param>
		/// <param name="count">Max outstanding watches</param>
		public static void SetMaxWatches(this IFdbDatabase db, int count)
		{
			if (db == null) throw new ArgumentNullException("db");
			if (count < 0) throw new FdbException(FdbError.InvalidOptionValue, "Maximum outstanding watches count must be a positive integer");

			//REVIEW: we can't really change this to a Property, because we don't have a way to get the current value for the getter, and set only properties are weird...
			//TODO: cache this into a local variable ?
			db.SetOption(FdbDatabaseOption.MaxWatches, count);
		}

		/// <summary>Specify the machine ID that was passed to fdbserver processes running on the same machine as this client, for better location-aware load balancing.</summary>
		/// <param name="db">Database instance</param>
		/// <param name="hexId">Hexadecimal ID</param>
		public static void SetMachineId(this IFdbDatabase db, string hexId)
		{
			if (db == null) throw new ArgumentNullException("db");
			//REVIEW: we can't really change this to a Property, because we don't have a way to get the current value for the getter, and set only properties are weird...
			//TODO: cache this into a local variable ?
			db.SetOption(FdbDatabaseOption.MachineId, hexId);
		}

		/// <summary>Specify the datacenter ID that was passed to fdbserver processes running in the same datacenter as this client, for better location-aware load balancing.</summary>
		/// <param name="db">Database instance</param>
		/// <param name="hexId">Hexadecimal ID</param>
		public static void SetDataCenterId(this IFdbDatabase db, string hexId)
		{
			if (db == null) throw new ArgumentNullException("db");
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
		public static bool IsKeyValid(this IFdbDatabase db, Slice key)
		{
			Exception _;
			return FdbDatabase.ValidateKey(db, key, false, true, out _);
		}

		/// <summary>Checks that a key is inside the global namespace of this database, and contained in the optional legal key space specified by the user</summary>
		/// <param name="db">Database instance</param>
		/// <param name="key">Key to verify</param>
		/// <param name="endExclusive">If true, the key is allowed to be one past the maximum key allowed by the global namespace</param>
		/// <exception cref="FdbException">If the key is outside of the allowed keyspace, throws an FdbException with code FdbError.KeyOutsideLegalRange</exception>
		internal static void EnsureKeyIsValid(this IFdbDatabase db, Slice key, bool endExclusive = false)
		{
			Exception ex;
			if (!FdbDatabase.ValidateKey(db, key, endExclusive, false, out ex)) throw ex;
		}

		/// <summary>Checks that one or more keys are inside the global namespace of this database, and contained in the optional legal key space specified by the user</summary>
		/// <param name="db">Database instance</param>
		/// <param name="keys">Array of keys to verify</param>
		/// <param name="endExclusive">If true, the keys are allowed to be one past the maximum key allowed by the global namespace</param>
		/// <exception cref="FdbException">If at least on key is outside of the allowed keyspace, throws an FdbException with code FdbError.KeyOutsideLegalRange</exception>
		internal static void EnsureKeysAreValid(this IFdbDatabase db, Slice[] keys, bool endExclusive = false)
		{
			if (keys == null) throw new ArgumentNullException("keys");
			foreach (var key in keys)
			{
				Exception ex;
				if (!FdbDatabase.ValidateKey(db, key, endExclusive, false, out ex)) throw ex;
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
		public static Slice Extract(this IFdbDatabase db, Slice keyAbsolute)
		{
			return db.GlobalSpace.Extract(keyAbsolute);
		}

		#endregion

		#region Unpack...

		/// <summary>Unpack a key using the current namespace of the database</summary>
		/// <param name="db">Database instance</param>
		/// <param name="key">Key that should fit inside the current namespace of the database</param>
		[CanBeNull]
		public static IFdbTuple Unpack(this IFdbDatabase db, Slice key)
		{
			return db.GlobalSpace.Unpack(key);
		}

		/// <summary>Unpack a key using the current namespace of the database</summary>
		/// <param name="db">Database instance</param>
		/// <param name="key">Key that should fit inside the current namespace of the database</param>
		public static T UnpackLast<T>(this IFdbDatabase db, Slice key)
		{
			return db.GlobalSpace.UnpackLast<T>(key);
		}

		/// <summary>Unpack a key using the current namespace of the database</summary>
		/// <param name="db">Database instance</param>
		/// <param name="key">Key that should fit inside the current namespace of the database</param>
		public static T UnpackSingle<T>(this IFdbDatabase db, Slice key)
		{
			return db.GlobalSpace.UnpackSingle<T>(key);
		}

		#endregion

		#region Standard Operations

		//REVIEW: this may be too dangerous!
		// Users may call GetAsync() or SetAsync() multiple times, outside of a transaction...

		/// <summary>Read a single key from the database, using a dedicated transaction.</summary>
		/// <param name="db">Database instance</param>
		/// <param name="key"></param>
		/// <param name="cancellationToken"></param>
		/// <returns></returns>
		/// <remarks>
		/// Use this method only if you intend to perform a single operation inside your execution context (ex: HTTP request).
		/// If you need to read several keys at once, use a version of <see cref="GetValuesAsync"/>.
		/// If you need to combine multiple read or write operations, consider using on of the multiple <see cref="ReadAsync"/> or <see cref="ReadWriteAsync"/> overrides.
		/// </remarks>
		public static Task<Slice> GetAsync(this IFdbDatabase db, Slice key, CancellationToken cancellationToken)
		{
			if (db == null) throw new ArgumentNullException("db");
			return db.ReadAsync((tr) => tr.GetAsync(key), cancellationToken);
		}

		/// <summary>Read a list of keys from the database, using a dedicated transaction.</summary>
		/// <param name="db">Database instance</param>
		/// <param name="keys"></param>
		/// <param name="cancellationToken"></param>
		/// <returns></returns>
		/// <remarks>
		/// Use this method only if you intend to perform a single operation inside your execution context (ex: HTTP request).
		/// If you need to combine multiple read or write operations, consider using on of the multiple <see cref="ReadAsync"/> or <see cref="ReadWriteAsync"/> overrides.
		/// </remarks>
		public static Task<Slice[]> GetValuesAsync(this IFdbDatabase db, [NotNull] Slice[] keys, CancellationToken cancellationToken)
		{
			if (db == null) throw new ArgumentNullException("db");
			return db.ReadAsync((tr) => tr.GetValuesAsync(keys), cancellationToken);
		}

		/// <summary>Read a sequence of keys from the database, using a dedicated transaction.</summary>
		/// <param name="db">Database instance</param>
		/// <remarks>
		/// Use this method only if you intend to perform a single operation inside your execution context (ex: HTTP request).
		/// If you need to combine multiple read or write operations, consider using on of the multiple <see cref="ReadAsync"/> or <see cref="ReadWriteAsync"/> overrides.
		/// </remarks>
		public static Task<Slice[]> GetValuesAsync(this IFdbDatabase db, [NotNull] IEnumerable<Slice> keys, CancellationToken cancellationToken)
		{
			if (db == null) throw new ArgumentNullException("db");
			return db.ReadAsync((tr) => tr.GetValuesAsync(keys), cancellationToken);
		}

		/// <summary>Resolve a single key selector from the database, using a dedicated transaction.</summary>
		/// <param name="db">Database instance</param>
		/// <remarks>
		/// Use this method only if you intend to perform a single operation inside your execution context (ex: HTTP request).
		/// If you need to combine multiple read or write operations, consider using on of the multiple <see cref="ReadAsync"/> or <see cref="ReadWriteAsync"/> overrides.
		/// </remarks>
		public static Task<Slice> GetKeyAsync(this IFdbDatabase db, FdbKeySelector keySelector, CancellationToken cancellationToken)
		{
			if (db == null) throw new ArgumentNullException("db");
			return db.ReadAsync((tr) => tr.GetKeyAsync(keySelector), cancellationToken);
		}

		/// <summary>Resolve a list of key selectors from the database, using a dedicated transaction.</summary>
		/// <param name="db">Database instance</param>
		/// <remarks>
		/// Use this method only if you intend to perform a single operation inside your execution context (ex: HTTP request).
		/// If you need to combine multiple read or write operations, consider using on of the multiple <see cref="ReadAsync"/> or <see cref="ReadWriteAsync"/> overrides.
		/// </remarks>
		public static Task<Slice[]> GetKeysAsync(this IFdbDatabase db, [NotNull] FdbKeySelector[] keySelectors, CancellationToken cancellationToken)
		{
			if (db == null) throw new ArgumentNullException("db");
			if (keySelectors == null) throw new ArgumentNullException("keySelectors");
			return db.ReadAsync((tr) => tr.GetKeysAsync(keySelectors), cancellationToken);
		}

		/// <summary>Resolve a sequence of key selectors from the database, using a dedicated transaction.</summary>
		/// <param name="db">Database instance</param>
		/// <remarks>
		/// Use this method only if you intend to perform a single operation inside your execution context (ex: HTTP request).
		/// If you need to combine multiple read or write operations, consider using on of the multiple <see cref="ReadAsync"/> or <see cref="ReadWriteAsync"/> overrides.
		/// </remarks>
		public static Task<Slice[]> GetKeysAsync(this IFdbDatabase db, [NotNull] IEnumerable<FdbKeySelector> keySelectors, CancellationToken cancellationToken)
		{
			if (db == null) throw new ArgumentNullException("db");
			if (keySelectors == null) throw new ArgumentNullException("keySelectors");
			return db.ReadAsync((tr) => tr.GetKeysAsync(keySelectors), cancellationToken);
		}

		/// <summary>Read a single page of a range query from the database, using a dedicated transaction.</summary>
		/// <param name="db">Database instance</param>
		/// <remarks>
		/// Use this method only if you intend to perform a single operation inside your execution context (ex: HTTP request).
		/// If you need to combine multiple read or write operations, consider using on of the multiple <see cref="ReadAsync"/> or <see cref="ReadWriteAsync"/> overrides.
		/// </remarks>
		public static Task<FdbRangeChunk> GetRangeAsync(this IFdbDatabase db, FdbKeySelector beginInclusive, FdbKeySelector endExclusive, FdbRangeOptions options, int iteration, CancellationToken cancellationToken)
		{
			if (db == null) throw new ArgumentNullException("db");
			return db.ReadAsync((tr) => tr.GetRangeAsync(beginInclusive, endExclusive, options, iteration), cancellationToken);
		}

		/// <summary>Set the value of a single key in the database, using a dedicated transaction.</summary>
		/// <param name="db">Database instance</param>
		/// <remarks>
		/// Use this method only if you intend to perform a single operation inside your execution context (ex: HTTP request).
		/// If you need to combine multiple read or write operations, consider using on of the multiple <see cref="ReadAsync"/> or <see cref="ReadWriteAsync"/> overrides.
		/// </remarks>
		public static Task SetAsync(this IFdbDatabase db, Slice key, Slice value, CancellationToken cancellationToken)
		{
			if (db == null) throw new ArgumentNullException("db");
			return db.WriteAsync((tr) => tr.Set(key, value), cancellationToken);
		}

		/// <summary>Clear a single key in the database, using a dedicated transaction.</summary>
		/// <param name="db">Database instance</param>
		/// <remarks>
		/// Use this method only if you intend to perform a single operation inside your execution context (ex: HTTP request).
		/// If you need to combine multiple read or write operations, consider using on of the multiple <see cref="ReadAsync"/> or <see cref="ReadWriteAsync"/> overrides.
		/// </remarks>
		public static Task ClearAsync(this IFdbDatabase db, Slice key, CancellationToken cancellationToken)
		{
			if (db == null) throw new ArgumentNullException("db");
			return db.WriteAsync((tr) => tr.Clear(key), cancellationToken);
		}

		/// <summary>Clear a single range in the database, using a dedicated transaction.</summary>
		/// <remarks>
		/// Use this method only if you intend to perform a single operation inside your execution context (ex: HTTP request).
		/// If you need to combine multiple read or write operations, consider using on of the multiple <see cref="ReadAsync"/> or <see cref="ReadWriteAsync"/> overrides.
		/// </remarks>
		public static Task ClearRangeAsync(this IFdbDatabase db, Slice beginKeyInclusive, Slice endKeyExclusive, CancellationToken cancellationToken)
		{
			if (db == null) throw new ArgumentNullException("db");
			return db.WriteAsync((tr) => tr.ClearRange(beginKeyInclusive, endKeyExclusive), cancellationToken);
		}

		/// <summary>Clear a single range in the database, using a dedicated transaction.</summary>
		/// <remarks>
		/// Use this method only if you intend to perform a single operation inside your execution context (ex: HTTP request).
		/// If you need to combine multiple read or write operations, consider using on of the multiple <see cref="ReadAsync"/> or <see cref="ReadWriteAsync"/> overrides.
		/// </remarks>
		public static Task ClearRangeAsync(this IFdbDatabase db, FdbKeyRange range, CancellationToken cancellationToken)
		{
			if (db == null) throw new ArgumentNullException("db");
			return db.WriteAsync((tr) => tr.ClearRange(range), cancellationToken);
		}

		/// <summary>Atomically add to the value of a single key in the database, using a dedicated transaction.</summary>
		/// <param name="db"></param>
		/// <param name="key"></param>
		/// <param name="value"></param>
		/// <param name="cancellationToken"></param>
		/// <returns></returns>
		/// <remarks>
		/// Use this method only if you intend to perform a single operation inside your execution context (ex: HTTP request).
		/// If you need to combine multiple read or write operations, consider using on of the multiple <see cref="ReadAsync"/> or <see cref="ReadWriteAsync"/> overrides.
		/// </remarks>
		public static Task AtomicAdd(this IFdbDatabase db, Slice key, Slice value, CancellationToken cancellationToken)
		{
			if (db == null) throw new ArgumentNullException("db");
			return db.WriteAsync((tr) => tr.Atomic(key, value, FdbMutationType.Add), cancellationToken);
		}

		/// <summary>Atomically perform a bitwise AND to the value of a single key in the database, using a dedicated transaction.</summary>
		/// <remarks>
		/// Use this method only if you intend to perform a single operation inside your execution context (ex: HTTP request).
		/// If you need to combine multiple read or write operations, consider using on of the multiple <see cref="ReadAsync"/> or <see cref="ReadWriteAsync"/> overrides.
		/// </remarks>
		public static Task AtomicBitAnd(this IFdbDatabase db, Slice key, Slice value, CancellationToken cancellationToken)
		{
			if (db == null) throw new ArgumentNullException("db");
			return db.WriteAsync((tr) => tr.Atomic(key, value, FdbMutationType.BitAnd), cancellationToken);
		}

		/// <summary>Atomically perform a bitwise OR to the value of a single key in the database, using a dedicated transaction.</summary>
		/// <remarks>
		/// Use this method only if you intend to perform a single operation inside your execution context (ex: HTTP request).
		/// If you need to combine multiple read or write operations, consider using on of the multiple <see cref="ReadAsync"/> or <see cref="ReadWriteAsync"/> overrides.
		/// </remarks>
		public static Task AtomicBitOr(this IFdbDatabase db, Slice key, Slice value, CancellationToken cancellationToken)
		{
			if (db == null) throw new ArgumentNullException("db");
			return db.WriteAsync((tr) => tr.Atomic(key, value, FdbMutationType.BitOr), cancellationToken);
		}

		/// <summary>Atomically perform a bitwise XOR to the value of a single key in the database, using a dedicated transaction.</summary>
		/// <remarks>
		/// Use this method only if you intend to perform a single operation inside your execution context (ex: HTTP request).
		/// If you need to combine multiple read or write operations, consider using on of the multiple <see cref="ReadAsync"/> or <see cref="ReadWriteAsync"/> overrides.
		/// </remarks>
		public static Task AtomicBitXor(this IFdbDatabase db, Slice key, Slice value, CancellationToken cancellationToken)
		{
			if (db == null) throw new ArgumentNullException("db");
			return db.WriteAsync((tr) => tr.Atomic(key, value, FdbMutationType.BitXor), cancellationToken);
		}

		#endregion

		#region Watches...

		/// <summary>Reads the value associated with <paramref name="key"/>, and returns a Watch that will complete after a subsequent change to key in the database.</summary>
		/// <param name="db">Database instance.</param>
		/// <param name="key">Key to be looked up in the database</param>
		/// <param name="cancellationToken">Token that can be used to cancel the Watch from the outside.</param>
		/// <returns>A new Watch that will track any changes to <paramref name="key"/> in the database, and whose <see cref="FdbWatch.Value">Value</see> property contains the current value of the key.</returns>
		public static Task<FdbWatch> GetAndWatch(this IFdbDatabase db, Slice key, CancellationToken cancellationToken)
		{
			if (db == null) throw new ArgumentNullException("db");

			return db.ReadWriteAsync(async (tr) =>
			{
				var result = await tr.GetAsync(key).ConfigureAwait(false);
				var watch = tr.Watch(key, cancellationToken);
				watch.Value = result.Memoize();
				return watch;
			}, cancellationToken);
		}

		public static Task<FdbWatch> GetAndWatch<TKey>(this IFdbDatabase db, TKey key, CancellationToken cancellationToken)
			where TKey : IFdbKey
		{
			if (key == null) throw new ArgumentNullException("key");
			return GetAndWatch(db, key.ToFoundationDbKey(), cancellationToken);
		}

		/// <summary>Sets <paramref name="key"/> to <paramref name="value"/> and returns a Watch that will complete after a subsequent change to the key in the database.</summary>
		/// <param name="db">Database instance.</param>
		/// <param name="key">Name of the key to be inserted into the database.</param>
		/// <param name="value">Value to be inserted into the database.</param>
		/// <param name="cancellationToken">Token that can be used to cancel the Watch from the outside.</param>
		/// <returns>A new Watch that will track any changes to <paramref name="key"/> in the database, and whose <see cref="FdbWatch.Value">Value</see> property will be a copy of <paramref name="value"/> argument</returns>
		public static async Task<FdbWatch> SetAndWatch(this IFdbDatabase db, Slice key, Slice value, CancellationToken cancellationToken)
		{
			if (db == null) throw new ArgumentNullException("db");

			FdbWatch watch = default(FdbWatch);

			await db.WriteAsync((tr) =>
			{
				tr.Set(key, value);
				watch = tr.Watch(key, cancellationToken);
			}, cancellationToken).ConfigureAwait(false);

			watch.Value = value.Memoize();
			return watch;
		}

		public static Task<FdbWatch> SetAndWatch<TKey>(this IFdbDatabase db, TKey key, Slice value, CancellationToken cancellationToken)
			where TKey : IFdbKey
		{
			if (key == null) throw new ArgumentNullException("key");
			return SetAndWatch(db, key.ToFoundationDbKey(), value, cancellationToken);
		}

		#endregion

	}

}
