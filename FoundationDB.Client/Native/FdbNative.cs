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

// enable this to help debug native calls to fdbc.dll
//#define DEBUG_NATIVE_CALLS

namespace FoundationDB.Client.Native
{
	using System;
	using System.Collections.Generic;
	using System.Diagnostics;
	using System.IO;
	using System.Runtime.CompilerServices;
	using System.Runtime.ExceptionServices;
	using System.Runtime.InteropServices;
	using System.Text;
	using Doxense.Diagnostics.Contracts;

	internal static unsafe class FdbNative
	{
		public const int FDB_API_MIN_VERSION = 200;
		public const int FDB_API_MAX_VERSION = 720;

#if __MonoCS__
		/// <summary>Name of the C API dll used for P/Invoking</summary>
		private const string FDB_C_DLL = "libfdb_c.so";
#else
		/// <summary>Name of the C API dll used for P/Invoking</summary>
		private const string FDB_C_DLL = "fdb_c";
#endif

		/// <summary>Handle on the native FDB C API library</summary>
		private static readonly UnmanagedLibrary? FdbCLib;

		/// <summary>Exception that was thrown when we last tried to load the native FDB C library (or null if nothing wrong happened)</summary>
		private static readonly ExceptionDispatchInfo? LibraryLoadError;

		[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
		public delegate void FdbNetworkThreadCompletionCallback(IntPtr parameter);

		[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
		public delegate void FdbFutureCallback(IntPtr future, IntPtr parameter);

		/// <summary>Contain all the stubs to the methods exposed by the C API library</summary>
		[System.Security.SuppressUnmanagedCodeSecurity]
		internal static class NativeMethods
		{

			// Core

			[DllImport(FDB_C_DLL, CallingConvention = CallingConvention.Cdecl)]
			public static extern FdbError fdb_select_api_version_impl(int runtimeVersion, int headerVersion);

			/// <summary>Returns <c>FDB_API_VERSION</c>, the current version of the FoundationDB C API.</summary>
			/// <returns>This is the maximum version that may be passed to <see cref="fdb_select_api_version_impl"/>.</returns>
			[DllImport(FDB_C_DLL, CallingConvention = CallingConvention.Cdecl)]
			public static extern int fdb_get_max_api_version();

			/// <summary>Returns a (somewhat) human-readable English message from an error code.</summary>
			/// <remarks>The return value is a statically allocated null-terminated string that must not be freed by the caller.</remarks>
			[DllImport(FDB_C_DLL, CallingConvention = CallingConvention.Cdecl)]
			public static extern IntPtr fdb_get_error(FdbError code);

			/// <summary>Evaluates a predicate against an error code.</summary>
			/// <returns>True if the code matches the specified <paramref name="predicateTest">predicate</paramref></returns>
			/// <remarks>The predicate to run should be one of the codes listed by the <see cref="FdbErrorPredicate"/> enum. Sample predicates include <see cref="FdbErrorPredicate.Retryable"/>, which can be used to determine whether the error with the given code is a retryable error or not.</remarks>
			[DllImport(FDB_C_DLL, CallingConvention = CallingConvention.Cdecl)]
			public static extern bool fdb_error_predicate(FdbErrorPredicate predicateTest, FdbError code);

			// Network

			/// <summary>Called to set network options.</summary>
			/// <remarks>
			/// If the given option is documented as taking a parameter, you must also pass a pointer to the parameter <paramref name="value"/> and the parameter values <paramref name="length"/>.
			/// If the option is documented as taking an <c>Int</c> parameter, value must point to a signed 64-bit integer (little-endian), and <paramref name="length"/> must be <c>8</c>.
			/// This memory only needs to be valid until <see cref="fdb_network_set_option"/> returns.
			/// </remarks>
			[DllImport(FDB_C_DLL, CallingConvention = CallingConvention.Cdecl)]
			public static extern FdbError fdb_network_set_option(FdbNetworkOption option, byte* value, int length);

			/// <summary>Setup the network thread.</summary>
			/// <remarks>
			/// Must be called after <see cref="fdb_select_api_version_impl"/> (and zero or more calls to <see cref="fdb_network_set_option"/>) and before any other function in this API.
			/// <see cref="fdb_setup_network"/> can only be called once.
			/// </remarks>
			[DllImport(FDB_C_DLL, CallingConvention = CallingConvention.Cdecl)]
			public static extern FdbError fdb_setup_network();

			/// <summary>Register the given callback to run at the completion of the network thread</summary>
			/// <remarks>
			/// Must be called after <see cref="fdb_setup_network"/> and prior to <see cref="fdb_run_network"/> if called at all.
			/// If there are multiple network threads running (which might occur if one is running multiple versions of the client, for example), then the callback is invoked once on each thread.
			/// When the supplied function is called, the supplied <paramref name="parameter"/> is passed to it.
			/// </remarks>
			[DllImport(FDB_C_DLL, CallingConvention = CallingConvention.Cdecl)]
			public static extern FdbError fdb_add_network_thread_completion_hook(FdbNetworkThreadCompletionCallback hook, IntPtr parameter);

			/// <summary>Run the network loop on the current thread</summary>
			/// <remarks>
			/// Must be called after <see cref="fdb_setup_network"/> before any asynchronous functions in this API can be expected to complete.
			/// Unless your program is entirely event-driven based on results of asynchronous functions in this API and has no event loop of its own, you will want to invoke this function on an auxiliary thread (which it is your responsibility to create).
			/// This function will not return until <see cref="fdb_stop_network"/> is called by you or a serious error occurs.
			/// It is not possible to run more than one network thread, and the network thread cannot be restarted once it has been stopped.
			/// This means that once <see cref="fdb_run_network"/> has been called, it is not legal to call it again for the lifetime of the running program.
			/// </remarks>
			[DllImport(FDB_C_DLL, CallingConvention = CallingConvention.Cdecl)]
			public static extern FdbError fdb_run_network();

			/// <summary>Signals the event loop invoked by <see cref="fdb_run_network"/> to terminate.</summary>
			/// <remarks>
			/// You must call this function and wait for <see cref="fdb_run_network"/> to return before allowing your program to exit, or else the behavior is undefined.
			/// This function may be called from any thread. Once the network is stopped it cannot be restarted during the lifetime of the running program.
			/// </remarks>
			[DllImport(FDB_C_DLL, CallingConvention = CallingConvention.Cdecl)]
			public static extern FdbError fdb_stop_network();

			// Cluster

			[DllImport(FDB_C_DLL, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi, BestFitMapping = false, ThrowOnUnmappableChar = true)]
			[Obsolete("Not supported any more")]
			public static extern FutureHandle fdb_create_cluster([MarshalAs(UnmanagedType.LPStr)] string? clusterFilePath);

			[DllImport(FDB_C_DLL, CallingConvention = CallingConvention.Cdecl)]
			[Obsolete("Not supported any more")]
			public static extern void fdb_cluster_destroy(IntPtr cluster);

			[DllImport(FDB_C_DLL, CallingConvention = CallingConvention.Cdecl)]
			[Obsolete("Not supported any more")]
			public static extern FdbError fdb_cluster_set_option(ClusterHandle cluster, FdbClusterOption option, byte* value, int valueLength);

			[DllImport(FDB_C_DLL, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi, BestFitMapping = false, ThrowOnUnmappableChar = true)]
			[Obsolete("Not supported any more")]
			public static extern FutureHandle fdb_cluster_create_database(ClusterHandle cluster, [MarshalAs(UnmanagedType.LPStr)] string dbName, int dbNameLength);

			// Database

			/// <summary>Creates a new database connected the specified cluster.</summary>
			/// <remarks>
			/// The caller assumes ownership of the FDBDatabase object and must destroy it with <see cref="fdb_database_destroy"/>.
			/// A single client can use this function multiple times to connect to different clusters simultaneously, with each invocation requiring its own cluster file.
			/// To connect to multiple clusters running at different, incompatible versions, the multi-version client API must be used.
			/// </remarks>
			[DllImport(FDB_C_DLL, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi, BestFitMapping = false, ThrowOnUnmappableChar = true)]
			public static extern FdbError fdb_create_database([MarshalAs(UnmanagedType.LPStr)] string? clusterFilePath, out DatabaseHandle database);

			/// <summary>Destroys an FDBDatabase object.</summary>
			/// <remarks>
			/// It must be called exactly once for each successful call to <see cref="fdb_create_database"/>.
			/// This function only destroys a handle to the database  your database will be fine!
			/// </remarks>
			[DllImport(FDB_C_DLL, CallingConvention = CallingConvention.Cdecl)]
			public static extern void fdb_database_destroy(IntPtr database);

			/// <summary>Called to set an option on an <see cref="DatabaseHandle">FDBDatabase</see>.</summary>
			/// <remarks>
			/// If the given option is documented as taking a parameter, you must also pass a pointer to the parameter <paramref name="value"/> and the parameter values <paramref name="length"/>.
			/// If the option is documented as taking an Int parameter, <paramref name="value"/> must point to a signed 64-bit integer (little-endian), and <paramref name="length"/> must be <c>8</c>.
			/// This memory only needs to be valid until <see cref="fdb_database_set_option"/> returns.
			/// </remarks>
			[DllImport(FDB_C_DLL, CallingConvention = CallingConvention.Cdecl)]
			public static extern FdbError fdb_database_set_option(DatabaseHandle handle, FdbDatabaseOption option, byte* value, int length);

			/// <summary>Creates a new transaction on the given database without using a tenant, meaning that it will operate on the entire database key-space.</summary>
			/// <remarks>The caller assumes ownership of the <see cref="TransactionHandle">FDBTransaction</see> object and must destroy it with <see cref="fdb_transaction_destroy"/>.</remarks>
			[DllImport(FDB_C_DLL, CallingConvention = CallingConvention.Cdecl)]
			public static extern FdbError fdb_database_create_transaction(DatabaseHandle database, out TransactionHandle transaction);

			/// <summary>Returns a value where 0 indicates that the client is idle and 1 (or larger) indicates that the client is saturated.</summary>
			/// <returns>By default, this value is updated every second.</returns>
			[DllImport(FDB_C_DLL, CallingConvention = CallingConvention.Cdecl)]
			public static extern double fdb_database_get_main_thread_busyness(DatabaseHandle database);

			[DllImport(FDB_C_DLL, CallingConvention = CallingConvention.Cdecl)]
			public static extern FdbError fdb_database_open_tenant(DatabaseHandle database, byte* tenantName, int tenantNameLength, out TenantHandle handle);

			// Tenant

			[DllImport(FDB_C_DLL, CallingConvention = CallingConvention.Cdecl)]
			public static extern void fdb_tenant_destroy(IntPtr tenant);

			[DllImport(FDB_C_DLL, CallingConvention = CallingConvention.Cdecl)]
			public static extern FdbError fdb_tenant_create_transaction(TenantHandle database, out TransactionHandle transaction);

			// Transaction

			/// <summary>Destroys an <see cref="TransactionHandle">FDBTransaction</see> object.</summary>
			/// <remarks>
			/// It must be called exactly once for each successful call to <see cref="fdb_database_create_transaction"/>.
			/// Destroying a transaction which has not had <see cref="fdb_transaction_commit"/> called implicitly rolls back the transaction (sets and clears do not take effect on the database).
			/// </remarks>
			[DllImport(FDB_C_DLL, CallingConvention = CallingConvention.Cdecl)]
			public static extern void fdb_transaction_destroy(IntPtr database);

			/// <summary>Called to set an option on an FDBTransaction.</summary>
			/// <remarks>
			/// If the given option is documented as taking a parameter, you must also pass a pointer to the parameter value and the parameter values length.
			/// If the option is documented as taking an Int parameter, value must point to a signed 64-bit integer (little-endian), and value_length must be 8.
			/// This memory only needs to be valid until fdb_transaction_set_option() returns.
			/// </remarks>
			[DllImport(FDB_C_DLL, CallingConvention = CallingConvention.Cdecl)]
			public static extern FdbError fdb_transaction_set_option(TransactionHandle handle, FdbTransactionOption option, byte* value, int valueLength);

			/// <summary>Sets the snapshot read version used by a transaction.</summary>
			/// <remarks>
			/// This is not needed in simple cases.
			/// If the given version is too old, subsequent reads will fail with error_code_transaction_too_old;
			/// if it is too new, subsequent reads may be delayed indefinitely and/or fail with <see cref="FdbError"><c>error_code_future_version</c></see>.
			/// If any of <c>fdb_transaction_get_*()</c> have been called on this transaction already, the result is undefined.</remarks>
			[DllImport(FDB_C_DLL, CallingConvention = CallingConvention.Cdecl)]
			public static extern void fdb_transaction_set_read_version(TransactionHandle handle, long version);

			/// <summary>Gets the read version of the <paramref name="transaction"/> snapshot</summary>
			/// <returns>Returns an <see cref="FutureHandle"><c>FDBFuture</c></see> which will be set to the transaction snapshot read version.</returns>
			/// <remarks>
			/// <para>You must first wait for the <see cref="FutureHandle"><c>FDBFuture</c></see> to be ready, check for errors, call <see cref="fdb_future_get_int64"/> to extract the version into an <c>int64_t</c> that you provide, and then destroy the <c>FDBFuture</c> with <see cref="fdb_future_destroy"/>.</para>
			/// The transaction obtains a snapshot read version automatically at the time of the first call to <c>fdb_transaction_get_*()</c> (including this one) and (unless causal consistency has been deliberately compromised by transaction options) is guaranteed to represent all transactions which were reported committed before that call.
			/// </remarks>
			[DllImport(FDB_C_DLL, CallingConvention = CallingConvention.Cdecl)]
			public static extern FutureHandle fdb_transaction_get_read_version(TransactionHandle transaction);

			/// <summary>Reads a value from the database snapshot represented by <paramref name="transaction"/></summary>
			/// <returns>Returns an <see cref="FutureHandle">FDBFuture</see> which will be set to the value of <paramref name="keyName"/> in the database.</returns>
			/// <remarks>
			/// <para>You must first wait for the <c>FDBFuture</c> to be ready, check for errors, call <see cref="fdb_future_get_value"/> to extract the value, and then destroy the <c>FDBFuture</c> with <see cref="fdb_future_destroy"/>.</para>
			/// <para>See <see cref="fdb_future_get_value"/> to see exactly how results are unpacked.</para>
			/// <para>If <paramref name="keyName"/> is not present in the database, the result is not an error, but a zero for <c>present</c> returned from that function.</para>
			/// </remarks>
			[DllImport(FDB_C_DLL, CallingConvention = CallingConvention.Cdecl)]
			public static extern FutureHandle fdb_transaction_get(TransactionHandle transaction, byte* keyName, int keyNameLength, bool snapshot);

			/// <summary>Returns a list of public network addresses as strings, one for each of the storage servers responsible for storing <see cref="keyName"/> and its associated value.</summary>
			/// <returns>Returns an <see cref="FutureHandle">FDBFuture</see> which will be set to an array of strings.</returns>
			/// <remarks>
			/// <para>You must first wait for the <c>FDBFuture</c> to be ready, check for errors, call <see cref="fdb_future_get_string_array"/> to extract the string array, and then destroy the <c>FDBFuture</c> with <see cref="fdb_future_destroy"/>.</para>
			/// </remarks>
			[DllImport(FDB_C_DLL, CallingConvention = CallingConvention.Cdecl)]
			public static extern FutureHandle fdb_transaction_get_addresses_for_key(TransactionHandle transaction, byte* keyName, int keyNameLength);

			/// <summary>Returns a list of keys that can split the given range into (roughly) equally sized chunks based on <paramref name="chunkSize"/>.</summary>
			/// <returns>Returns an <see cref="FutureHandle">FDBFuture</see> which will be set to the list of split points.</returns>
			/// <remarks>
			/// <para>You must first wait for the <c>FDBFuture</c> to be ready, check for errors, call <see cref="fdb_future_get_key_array"/> to extract the array, and then destroy the <c>FDBFuture</c> with <see cref="fdb_future_destroy"/>.</para>
			/// </remarks>
			[DllImport(FDB_C_DLL, CallingConvention = CallingConvention.Cdecl)]
			public static extern FutureHandle fdb_transaction_get_range_split_points(TransactionHandle transaction, byte* beginKeyName, int beginKeyNameLength, byte* endKeyName, int endKeyNameLength, long chunkSize);

			/// <summary>Returns an estimated byte size of the key range.</summary>
			/// <returns>Returns an <see cref="FutureHandle">FDBFuture</see> which will be set to the estimated size of the key range given.</returns>
			/// <remarks>
			/// <para>You must first wait for the <c>FDBFuture</c> to be ready, check for errors, call <see cref="fdb_future_get_int64"/> to extract the size, and then destroy the <c>FDBFuture</c> with <see cref="fdb_future_destroy"/>.</para>
			/// <para>The estimated size is calculated based on the sampling done by FDB server. The sampling algorithm works roughly in this way: the larger the key-value pair is, the more likely it would be sampled and the more accurate its sampled size would be. And due to that reason it is recommended to use this API to query against large ranges for accuracy considerations. For a rough reference, if the returned size is larger than 3MB, one can consider the size to be accurate.</para>
			/// </remarks>
			[DllImport(FDB_C_DLL, CallingConvention = CallingConvention.Cdecl)]
			public static extern FutureHandle fdb_transaction_get_estimated_range_size_bytes(TransactionHandle transaction, byte* beginKeyName, int beginKeyNameLength, byte* endKeyName, int endKeyNameLength);

			/// <summary>Resolves a key selector against the keys in the database snapshot represented by <paramref name="transaction"/>.</summary>
			/// <returns>Returns an <see cref="FutureHandle">FDBFuture</see> which will be set to the key in the database matching the key selector.</returns>
			/// <remarks>
			/// <para>You must first wait for the <c>FDBFuture</c> to be ready, check for errors, call <see cref="fdb_future_get_key"/> to extract the key, and then destroy the <c>FDBFuture</c> with <see cref="fdb_future_destroy"/>.</para>
			/// </remarks>
			[DllImport(FDB_C_DLL, CallingConvention = CallingConvention.Cdecl)]
			public static extern FutureHandle fdb_transaction_get_key(TransactionHandle transaction, byte* keyName, int keyNameLength, bool orEqual, int offset, bool snapshot);

			/// <summary>Reads all key-value pairs in the database snapshot represented by <paramref name="transaction"/> (potentially limited by <paramref name="limit"/>, <paramref name="targetBytes"/>, or <paramref name="mode"/>) which have a key lexicographically greater than or equal to the key resolved by the <c>begin</c> key selector and lexicographically less than the key resolved by the <c>end</c> key selector.</summary>
			/// <returns>Returns an <see cref="FutureHandle">FDBFuture</see> which will be set to an <c>FDBKeyValue</c> array.</returns>
			/// <remarks>
			/// <para>You must first wait for the <c>FDBFuture</c> to be ready, check for errors, call <see cref="fdb_future_get_keyvalue_array"/> to extract the key-value array, and then destroy the <c>FDBFuture</c> with <see cref="fdb_future_destroy"/>.</para>
			/// </remarks>
			[DllImport(FDB_C_DLL, CallingConvention = CallingConvention.Cdecl)]
			public static extern FutureHandle fdb_transaction_get_range(
				TransactionHandle transaction,
				/* begin */ byte* beginKeyName, int beginKeyNameLength, bool beginOrEqual, int beginOffset,
				/* end */ byte* endKeyName, int endKeyNameLength, bool endOrEqual, int endOffset,
				int limit, int targetBytes, FdbStreamingMode mode, int iteration, bool snapshot, bool reverse
			);

			/// <summary>Modify the database snapshot represented by <paramref name="transaction"/> to change the given key to have the given value. If the given key was not previously present in the database it is inserted.</summary>
			/// <remarks>
			/// The modification affects the actual database only if <paramref name="transaction"/> is later committed with <see cref="fdb_transaction_commit"/>.
			/// </remarks>
			[DllImport(FDB_C_DLL, CallingConvention = CallingConvention.Cdecl)]
			public static extern void fdb_transaction_set(TransactionHandle transaction, byte* keyName, int keyNameLength, byte* value, int valueLength);

			/// <summary>Modify the database snapshot represented by <paramref name="transaction"/> to remove the given key from the database. If the key was not previously present in the database, there is no effect.</summary>
			/// <remarks>
			/// The modification affects the actual database only if <paramref name="transaction"/> is later committed with <see cref="fdb_transaction_commit"/>.
			/// </remarks>
			[DllImport(FDB_C_DLL, CallingConvention = CallingConvention.Cdecl)]
			public static extern void fdb_transaction_clear(TransactionHandle transaction, byte* keyName, int keyNameLength);

			/// <summary>Modify the database snapshot represented by <paramref name="transaction"/> to remove all keys (if any) which are lexicographically greater than or equal to the given begin key and lexicographically less than the given end_key.</summary>
			/// <remarks>
			/// The modification affects the actual database only if <paramref name="transaction"/> is later committed with <see cref="fdb_transaction_commit"/>.
			/// Range clears are efficient with FoundationDB  clearing large amounts of data will be fast.
			/// However, this will not immediately free up disk - data for the deleted range is cleaned up in the background.
			/// For purposes of computing the transaction size, only the begin and end keys of a clear range are counted.
			/// The size of the data stored in the range does not count against the transaction size limit.
			/// </remarks>
			[DllImport(FDB_C_DLL, CallingConvention = CallingConvention.Cdecl)]
			public static extern void fdb_transaction_clear_range(
				TransactionHandle transaction,
				byte* beginKeyName, int beginKeyNameLength,
				byte* endKeyName, int endKeyNameLength
			);

			/// <summary>Modify the database snapshot represented by <paramref name="transaction"/> to perform the operation indicated by operationType with operand param to the value stored by the given key.</summary>
			/// <remarks>
			/// An atomic operation is a single database command that carries out several logical steps: reading the value of a key, performing a transformation on that value, and writing the result.
			/// Different atomic operations perform different transformations.
			/// Like other database operations, an atomic operation is used within a transaction; however, its use within a transaction will not cause the transaction to conflict.
			/// Atomic operations do not expose the current value of the key to the client but simply send the database the transformation to apply.
			/// In regard to conflict checking, an atomic operation is equivalent to a write without a read. It can only cause other transactions performing reads of the key to conflict.
			/// By combining these logical steps into a single, read-free operation, FoundationDB can guarantee that the transaction will not conflict due to the operation.
			/// This makes atomic operations ideal for operating on keys that are frequently modified. A common example is the use of a key-value pair as a counter.
			/// </remarks>
			[DllImport(FDB_C_DLL, CallingConvention = CallingConvention.Cdecl)]
			public static extern void fdb_transaction_atomic_op(TransactionHandle transaction, byte* keyName, int keyNameLength, byte* param, int paramLength, FdbMutationType operationType);

			/// <summary>Attempts to commit the sets and clears previously applied to the database snapshot represented by transaction to the actual database. The commit may or may not succeed  in particular, if a conflicting transaction previously committed, then the commit must fail in order to preserve transactional isolation. If the commit does succeed, the transaction is durably committed to the database and all subsequently started transactions will observe its effects.</summary>
			/// <returns>Returns an <see cref="FutureHandle">FDBFuture</see> representing an empty value.</returns>
			/// <remarks>
			/// <para>You must first wait for the <c>FDBFuture</c> to be ready, check for errors, and then destroy the <c>FDBFuture</c> with <see cref="fdb_future_destroy"/>.</para>
			/// <para>It is not necessary to commit a read-only transaction  you can simply call <see cref="fdb_transaction_destroy"/>.</para>
			/// </remarks>
			[DllImport(FDB_C_DLL, CallingConvention = CallingConvention.Cdecl)]
			public static extern FutureHandle fdb_transaction_commit(TransactionHandle transaction);

			/// <summary>Retrieves the database version number at which a given transaction was committed.</summary>
			/// <remarks>
			/// <see cref="fdb_transaction_commit"/> must have been called on <paramref name="transaction"/> and the resulting future must be ready and not an error before this function is called, or the behavior is undefined.
			/// Read-only transactions do not modify the database when committed and will have a committed version of -1.
			/// Keep in mind that a transaction which reads keys and then sets them to their current values may be optimized to a read-only transaction.
			/// Note that database versions are not necessarily unique to a given transaction and so cannot be used to determine in what order two transactions completed.
			/// The only use for this function is to manually enforce causal consistency when calling <see cref="fdb_transaction_set_read_version"/> on another subsequent transaction.
			/// Most applications will not call this function.
			/// </remarks>
			[DllImport(FDB_C_DLL, CallingConvention = CallingConvention.Cdecl)]
			public static extern FdbError fdb_transaction_get_committed_version(TransactionHandle transaction, out long version);

			/// <summary>Retrieves the <see cref="VersionStamp"/> which was used by any versionstamp operation in this transaction.</summary>
			/// <returns>Returns an <see cref="FutureHandle">FDBFuture</see> which will be set to the versionstamp which was used by any versionstamp operations in this transaction.</returns>
			/// <remarks>
			/// You must first wait for the <c>FDBFuture</c> to be ready, check for errors, call <see cref="fdb_future_get_key"/> to extract the key, and then destroy the <c>FDBFuture</c> with <see cref="fdb_future_destroy"/>.
			/// The future will be ready only after the successful completion of a call to <see cref="fdb_transaction_commit"/> on this Transaction. Read-only transactions do not modify the database when committed and will result in the future completing with an error. Keep in mind that a transaction which reads keys and then sets them to their current values may be optimized to a read-only transaction.
			/// Most applications will not call this function.
			/// </remarks>
			[DllImport(FDB_C_DLL, CallingConvention = CallingConvention.Cdecl)]
			public static extern FutureHandle fdb_transaction_get_versionstamp(TransactionHandle transaction);

			/// <summary></summary>
			/// <returns>Returns an <see cref="FutureHandle">FDBFuture</see> representing an empty value that will be set once the watch has detected a change to the value at the specified key.</returns>
			/// <remarks>
			/// <para>You must first wait for the <c>FDBFuture</c> to be ready, check for errors, and then destroy the <c>FDBFuture</c> with <see cref="fdb_future_destroy"/>.</para>
			/// <para>
			/// A watchs behavior is relative to the transaction that created it.
			/// A watch will report a change in relation to the keys value as readable by that transaction.
			/// The initial value used for comparison is either that of the transactions read version or the value as modified by the transaction itself prior to the creation of the watch.
			/// If the value changes and then changes back to its initial value, the watch might not report the change.
			/// </para>
			/// <para>
			/// Until the transaction that created it has been committed, a watch will not report changes made by other transactions.
			/// In contrast, a watch will immediately report changes made by the transaction itself.
			/// Watches cannot be created if the transaction has set the READ_YOUR_WRITES_DISABLE transaction option, and an attempt to do so will return an watches_disabled error.
			/// </para>
			/// <para>
			/// If the transaction used to create a watch encounters an error during commit, then the watch will be set with that error.
			/// A transaction whose commit result is unknown will set all of its watches with the commit_unknown_result error.
			/// If an uncommitted transaction is reset or destroyed, then any watches it created will be set with the <see cref="FdbError"><c>transaction_cancelled</c></see> error.
			/// </para>
			/// <para>
			/// By default, each database connection can have no more than <c>10,000</c> watches that have not yet reported a change.
			/// When this number is exceeded, an attempt to create a watch will return a <see cref="FdbError"><c>too_many_watches</c></see> error.
			/// This limit can be changed using the <c>MAX_WATCHES</c> database option.
			/// Because a watch outlives the transaction that creates it, any watch that is no longer needed should be cancelled by calling <see cref="fdb_future_cancel"/> on its returned future.
			/// </para>
			/// </remarks>
			[DllImport(FDB_C_DLL, CallingConvention = CallingConvention.Cdecl)]
			public static extern FutureHandle fdb_transaction_watch(TransactionHandle transaction, byte* keyName, int keyNameLength);

			/// <summary>Implements the recommended retry and backoff behavior for a transaction.</summary>
			/// <returns>Returns an <see cref="FutureHandle">FDBFuture</see> representing an empty value.</returns>
			/// <remarks>
			/// <para>You must first wait for the <c>FDBFuture</c> to be ready, check for errors, and then destroy the <c>FDBFuture</c> with <see cref="fdb_future_destroy"/>.</para>
			/// <para>
			/// This function knows which of the error codes generated by other <c>fdb_transaction_*()</c> functions represent temporary error conditions and which represent application errors that should be handled by the application.
			/// It also implements an exponential backoff strategy to avoid swamping the database cluster with excessive retries when there is a high level of conflict between transactions.
			/// </para>
			/// </remarks>
			[DllImport(FDB_C_DLL, CallingConvention = CallingConvention.Cdecl)]
			public static extern FutureHandle fdb_transaction_on_error(TransactionHandle transaction, FdbError error);

			/// <summary>Reset <paramref name="transaction"/> to its initial state.</summary>
			/// <remarks>
			/// This is similar to calling <see cref="fdb_transaction_destroy"/> followed by <see cref="fdb_database_create_transaction"/>.
			/// It is not necessary to call <see cref="fdb_transaction_reset"/> when handling an error with <see cref="fdb_transaction_on_error"/> since the transaction has already been reset.
			/// </remarks>
			[DllImport(FDB_C_DLL, CallingConvention = CallingConvention.Cdecl)]
			public static extern void fdb_transaction_reset(TransactionHandle transaction);

			/// <summary>Cancels the transaction.</summary>
			/// <remarks>
			/// All pending or future uses of the transaction will return a <see cref="FdbError"><c>transaction_cancelled</c></see> error.
			/// The transaction can be used again after it is reset.
			/// </remarks>
			[DllImport(FDB_C_DLL, CallingConvention = CallingConvention.Cdecl)]
			public static extern void fdb_transaction_cancel(TransactionHandle transaction);

			/// <summary>Adds a conflict range to a transaction without performing the associated read or write.</summary>
			[DllImport(FDB_C_DLL, CallingConvention = CallingConvention.Cdecl)]
			public static extern FdbError fdb_transaction_add_conflict_range(TransactionHandle transaction, byte* beginKeyName, int beginKeyNameLength, byte* endKeyName, int endKeyNameLength, FdbConflictRangeType type);

			/// <summary>Returns the approximate transaction size so far.</summary>
			/// <returns>Returns an <see cref="FutureHandle">FDBFuture</see> which will be set to the approximate transaction size so far in the returned future, which is the summation of the estimated size of mutations, read conflict ranges, and write conflict ranges.</returns>
			/// <remarks>
			/// You must first wait for the <c>FDBFuture</c> to be ready, check for errors, call <see cref="fdb_future_get_int64"/> to extract the size, and then destroy the <c>FDBFuture</c> with <see cref="fdb_future_destroy"/>.
			/// This can be called multiple times before the transaction is committed.
			/// </remarks>
			[DllImport(FDB_C_DLL, CallingConvention = CallingConvention.Cdecl)]
			public static extern FutureHandle fdb_transaction_get_approximate_size(TransactionHandle transaction);

			// Future

			/// <summary>Destroys an <see cref="FutureHandle">FDBFuture</see> object.</summary>
			/// <remarks>
			/// It must be called exactly once for each FDBFuture* returned by an API function.
			/// It may be called before or after the future is ready.
			/// It will also cancel the future (and its associated operation if the latter is still outstanding).
			/// </remarks>
			[DllImport(FDB_C_DLL, CallingConvention = CallingConvention.Cdecl)]
			public static extern void fdb_future_destroy(IntPtr future);

			/// <summary>Cancels an <see cref="FutureHandle">FDBFuture</see> object and its associated asynchronous operation.</summary>
			/// <remarks>
			/// If called before the future is ready, attempts to access its value will return an operation_cancelled error.
			/// Cancelling a future which is already ready has no effect.
			/// Note that even if a future is not ready, its associated asynchronous operation may have succesfully completed and be unable to be cancelled.
			/// </remarks>
			[DllImport(FDB_C_DLL, CallingConvention = CallingConvention.Cdecl)]
			public static extern void fdb_future_cancel(FutureHandle future);

			/// <summary>Release memory associated to the given <see cref="FutureHandle">FDBFuture</see> object.</summary>
			/// <remarks>
			/// This function may only be called after a successful (zero return value) call to <see cref="fdb_future_get_key"/>, <see cref="fdb_future_get_value"/>, or <see cref="fdb_future_get_keyvalue_array"/>.
			/// It indicates that the memory returned by the prior get call is no longer needed by the application.
			/// After this function has been called the same number of times as fdb_future_get_*(), further calls to fdb_future_get_*() will return a future_released error.
			/// It is still necessary to later destroy the future with fdb_future_destroy().
			/// Calling this function is optional, since <see cref="fdb_future_destroy"/> will also release the memory returned by get functions.
			/// However, <see cref="fdb_future_release_memory"/> leaves the future object itself intact and provides a specific error code which can be used for coordination by multiple threads racing to do something with the results of a specific future.
			/// This has proven helpful in writing binding code.
			/// </remarks>
			[DllImport(FDB_C_DLL, CallingConvention = CallingConvention.Cdecl)]
			public static extern void fdb_future_release_memory(FutureHandle future);

			/// <summary>Blocks the calling thread until the given <c>Future</c> is ready.</summary>
			/// <remarks>
			/// It will return success even if the <c>Future</c> is set to an error  you must call <see cref="fdb_future_get_error"/> to determine that.
			/// <see cref="fdb_future_block_until_ready"/> will return an error only in exceptional conditions (e.g. deadlock detected, out of memory or other operating system resources).
			/// </remarks>
			[DllImport(FDB_C_DLL, CallingConvention = CallingConvention.Cdecl)]
			public static extern FdbError fdb_future_block_until_ready(FutureHandle future);

			/// <summary>Returns non-zero if the <paramref name="future"/> is ready.</summary>
			/// <remarks>A <c>Future</c> is ready if it has been set to a value or an error.</remarks>
			[DllImport(FDB_C_DLL, CallingConvention = CallingConvention.Cdecl)]
			public static extern bool fdb_future_is_ready(FutureHandle future);

			/// <summary>Returns zero if <paramref name="future"/> is ready and not in an error state, and a non-zero error code otherwise.</summary>
			[DllImport(FDB_C_DLL, CallingConvention = CallingConvention.Cdecl)]
			public static extern FdbError fdb_future_get_error(FutureHandle future);

			/// <summary>Causes the FDBCallback function to be invoked as <c><paramref name="callback"/>(<paramref name="future"/>, <paramref name="parameter"/>)</c> when the given <paramref name="future"/> is ready.</summary>
			/// <returns>
			/// If the <c>Future</c> is already ready, the call may occur in the current thread before this function returns (but this behavior is not guaranteed).
			/// Alternatively, the call may be delayed indefinitely and take place on the thread on which <see cref="fdb_run_network"/> was invoked,
			/// and the callback is responsible for any necessary thread synchronization (and/or for posting work back to your application
			/// event loop, thread pool, etc. if your applications architecture calls for that).
			/// </returns>
			[DllImport(FDB_C_DLL, CallingConvention = CallingConvention.Cdecl)]
			public static extern FdbError fdb_future_set_callback(FutureHandle future, FdbFutureCallback callback, IntPtr parameter);

			[DllImport(FDB_C_DLL, CallingConvention = CallingConvention.Cdecl)]
			public static extern FdbError fdb_future_get_version(FutureHandle future, out long version);

			/// <summary>Extracts a 64-bit integer from a pointer to <see cref="FutureHandle">FDBFuture</see> into a caller-provided variable of type int64_t.</summary>
			/// <remarks>
			/// <paramref name="future"/> must represent a result of the appropriate type (i.e. must have been returned by a function documented as returning this type), or the results are undefined.
			/// Returns zero if future is ready and not in an error state, and a non-zero error code otherwise (in which case the value of any out parameter is undefined).
			/// </remarks>
			[DllImport(FDB_C_DLL, CallingConvention = CallingConvention.Cdecl)]
			public static extern FdbError fdb_future_get_int64(FutureHandle future, out long version);

			/// <summary>Extracts a key from an <see cref="FutureHandle">FDBFuture</see> into caller-provided variables of type <c>uint8_t*</c> (a pointer to the beginning of the key) and int (the length of the key). </summary>
			/// <remarks>
			/// <para>future must represent a result of the appropriate type (i.e. must have been returned by a function documented as returning this type), or the results are undefined.</para>
			/// <para>Returns zero if future is ready and not in an error state, and a non-zero error code otherwise (in which case the value of any out parameter is undefined).</para>
			/// <para>The memory referenced by the result is owned by the <c>FDBFuture</c> object and will be valid until either <see cref="fdb_future_destroy"/> or <see cref="fdb_future_release_memory"/> is called.</para>
			/// </remarks>
			[DllImport(FDB_C_DLL, CallingConvention = CallingConvention.Cdecl)]
			public static extern FdbError fdb_future_get_key(FutureHandle future, out byte* key, out int keyLength);

			[DllImport(FDB_C_DLL, CallingConvention = CallingConvention.Cdecl)]
			[Obsolete("Deprecated since API level 610")]
			public static extern FdbError fdb_future_get_cluster(FutureHandle future, out ClusterHandle cluster);

			[DllImport(FDB_C_DLL, CallingConvention = CallingConvention.Cdecl)]
			public static extern FdbError fdb_future_get_database(FutureHandle future, out DatabaseHandle database);

			[DllImport(FDB_C_DLL, CallingConvention = CallingConvention.Cdecl)]
			public static extern FdbError fdb_future_get_value(FutureHandle future, out bool present, out byte* value, out int valueLength);

			[DllImport(FDB_C_DLL, CallingConvention = CallingConvention.Cdecl)]
			public static extern FdbError fdb_future_get_string_array(FutureHandle future, out byte** strings, out int count);

			[DllImport(FDB_C_DLL, CallingConvention = CallingConvention.Cdecl)]
			public static extern FdbError fdb_future_get_keyvalue_array(FutureHandle future, out FdbKeyValue* kv, out int count, out bool more);

			[DllImport(FDB_C_DLL, CallingConvention = CallingConvention.Cdecl)]
			public static extern FdbError fdb_future_get_key_array(FutureHandle future, out FdbKey* keyArray, out int count);

		}

		static FdbNative()
		{
			var libraryPath = GetPreloadPath();

			if (libraryPath == null)
			{ // PInvoke will load
				return;
			}

			try
			{
				FdbCLib = UnmanagedLibrary.Load(libraryPath);
			}
			catch (Exception e)
			{
				FdbCLib?.Dispose();
				FdbCLib = null;
				if (e is BadImageFormatException && IntPtr.Size == 4)
				{
					e = new InvalidOperationException("The native FDB client is 64-bit only, and cannot be loaded in a 32-bit process.", e);
				}
				else
				{
					e = new InvalidOperationException($"An error occurred while loading the native FoundationDB library: '{libraryPath}'.", e);
				}
				LibraryLoadError = ExceptionDispatchInfo.Capture(e);
			}
			
		}

		private static string? GetPreloadPath()
		{
			// we need to provide sensible defaults for loading the native library
			// if this method returns null we'll let PInvoke deal
			// otherwise - use explicit platform-specific dll loading
			var libraryPath = Fdb.Options.NativeLibPath;

			// on non-windows, library loading by convention just works.
			// unless override is provided, just let PInvoke do the work
			if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
			{
				if (string.IsNullOrEmpty(libraryPath))
				{
					return null;
				}
				// otherwise just use the provided path
				return libraryPath;
			}

			// Impact of NativeLibPath on windows:
			// - If null, don't preload the library, and let the CLR find the file using the default P/Invoke behavior
			// - If String.Empty, call win32 LoadLibrary(FDB_C_DLL + ".dll") and let the os find the file (using the standard OS behavior)
			// - If path is folder, append the FDB_C_DLL
			var winDllWithExtension = FDB_C_DLL + ".dll";
			if (libraryPath == null)
			{
				return null;
			}
			if (libraryPath.Length == 0)
			{
				return winDllWithExtension;
			}
			var fileName = Path.GetFileName(libraryPath);
			if (String.IsNullOrEmpty(fileName))
			{
				libraryPath = Path.Combine(libraryPath, winDllWithExtension);
			}
			return libraryPath;
		}

		private static void EnsureLibraryIsLoaded()
		{
			// should be inlined
			FdbNative.LibraryLoadError?.Throw();
		}

		private static string? ToManagedString(byte* nativeString)
		{
			if (nativeString == null) return null;
			return Marshal.PtrToStringAnsi(new IntPtr(nativeString));
		}

		private static string? ToManagedString(IntPtr nativeString)
		{
			if (nativeString == IntPtr.Zero) return null;
			return Marshal.PtrToStringAnsi(nativeString);
		}

		/// <summary>Converts a string into an ANSI byte array</summary>
		/// <param name="value">String to convert (or null)</param>
		/// <param name="nullTerminated">If true, adds a terminating \0 at the end (C-style strings)</param>
		/// <returns>Byte array with the ANSI-encoded string with an optional NUL terminator, or null if <paramref name="value"/> was null</returns>
		public static Slice ToNativeString(ReadOnlySpan<char> value, bool nullTerminated)
		{
			if (value == null) return Slice.Nil;
			if (value.Length == 0) return Slice.Empty;

			byte[] result;
			if (nullTerminated)
			{ // NULL terminated ANSI string
				result = new byte[value.Length + 1];
			}
			else
			{
				result = new byte[value.Length];
			}

			fixed (char* inp = value)
			fixed (byte* outp = &result[0])
			{
				Encoding.Default.GetBytes(inp, value.Length, outp, result.Length);
			}
			return Slice.CreateUnsafe(result, 0, result.Length);
		}


		#region Core..

		/// <summary>Throws an exception if the code represents a failure.</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void DieOnError(FdbError code)
		{
			if (code != FdbError.Success)
			{
				throw CreateExceptionFromError(code);
			}
		}

		/// <summary>Returns a (somewhat) human-readable English message from an error code.</summary>
		public static string? GetErrorMessage(FdbError code)
		{
			return ToManagedString(NativeMethods.fdb_get_error(code));
		}

		/// <summary>Maps an error code into an Exception (to be thrown)</summary>
		/// <param name="code">Error code returned by a native fdb operation</param>
		/// <returns>Exception object corresponding to the error code, or null if the code is not an error</returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Exception? MapToException(FdbError code)
		{
			return code == FdbError.Success ? null : CreateExceptionFromError(code);
		}

		[MethodImpl(MethodImplOptions.NoInlining)]
		internal static Exception CreateExceptionFromError(FdbError code)
		{
			return code switch
			{
				FdbError.TimedOut => new TimeoutException("Operation timed out"),
				FdbError.LargeAllocFailed => new OutOfMemoryException("Large block allocation failed"),
				FdbError.InvalidOption => new ArgumentException("Option not valid in this context"),
				FdbError.ApiVersionUnset => new InvalidOperationException("Api version must be set"),
				//TODO: add more custom mappings?
				_ => new FdbException(code)
			};
		}

		/// <summary>fdb_error_predicate</summary>
		public static bool TestErrorPredicate(FdbErrorPredicate predicate, FdbError code)
		{
			EnsureLibraryIsLoaded();
			return NativeMethods.fdb_error_predicate(predicate, code);
		}

		/// <summary>fdb_select_api_impl</summary>
		public static FdbError SelectApiVersionImpl(int runtimeVersion, int headerVersion)
		{
			EnsureLibraryIsLoaded();
			return NativeMethods.fdb_select_api_version_impl(runtimeVersion, headerVersion);
		}

		/// <summary>fdb_select_api_impl</summary>
		public static FdbError SelectApiVersion(int version)
		{
			return SelectApiVersionImpl(version, GetMaxApiVersion());
		}

		/// <summary>fdb_get_max_api_version</summary>
		public static int GetMaxApiVersion()
		{
			EnsureLibraryIsLoaded();
			return NativeMethods.fdb_get_max_api_version();
		}

		#endregion

		#region Futures...

		public static bool FutureIsReady(FutureHandle futureHandle)
		{
			return NativeMethods.fdb_future_is_ready(futureHandle);
		}

		public static void FutureDestroy(IntPtr futureHandle)
		{
			if (futureHandle != IntPtr.Zero)
			{
				NativeMethods.fdb_future_destroy(futureHandle);
			}
		}

		public static void FutureCancel(FutureHandle futureHandle)
		{
			NativeMethods.fdb_future_cancel(futureHandle);
		}

		public static void FutureReleaseMemory(FutureHandle futureHandle)
		{
			NativeMethods.fdb_future_release_memory(futureHandle);
		}

		public static FdbError FutureGetError(FutureHandle future)
		{
			return NativeMethods.fdb_future_get_error(future);
		}

		public static FdbError FutureBlockUntilReady(FutureHandle future)
		{
#if DEBUG_NATIVE_CALLS
			Debug.WriteLine("calling fdb_future_block_until_ready(0x" + future.Handle.ToString("x") + ")...");
#endif
			var err = NativeMethods.fdb_future_block_until_ready(future);
#if DEBUG_NATIVE_CALLS
			Debug.WriteLine("fdb_future_block_until_ready(0x" + future.Handle.ToString("x") + ") => err=" + err);
#endif
			return err;
		}

		public static FdbError FutureSetCallback(FutureHandle future, FdbFutureCallback callback, IntPtr callbackParameter)
		{
			var err = NativeMethods.fdb_future_set_callback(future, callback, callbackParameter);
#if DEBUG_NATIVE_CALLS
			Debug.WriteLine("fdb_future_set_callback(0x" + future.Handle.ToString("x") + ", 0x" + callbackParameter.ToString("x") + ") => err=" + err);
#endif
			return err;
		}

		#endregion

		#region Network...

		public static FdbError NetworkSetOption(FdbNetworkOption option, byte* value, int valueLength)
		{
			EnsureLibraryIsLoaded();
			return NativeMethods.fdb_network_set_option(option, value, valueLength);
		}

		public static FdbError SetupNetwork()
		{
			EnsureLibraryIsLoaded();
			return NativeMethods.fdb_setup_network();
		}

		public static FdbError RunNetwork()
		{
			EnsureLibraryIsLoaded();
			return NativeMethods.fdb_run_network();
		}

		public static FdbError StopNetwork()
		{
			EnsureLibraryIsLoaded();
			return NativeMethods.fdb_stop_network();
		}

		public static FdbError AddNetworkThreadCompletionHook(FdbNetworkThreadCompletionCallback hook, IntPtr parameter)
		{
			EnsureLibraryIsLoaded();
			return NativeMethods.fdb_add_network_thread_completion_hook(hook, parameter);
		}

		#endregion

		#region Clusters...

		[Obsolete("Deprecated since API level 610")]
		public static FutureHandle CreateCluster(string? path)
		{
			var future = NativeMethods.fdb_create_cluster(path);
			Contract.Debug.Assert(future != null);
#if DEBUG_NATIVE_CALLS
			Debug.WriteLine("fdb_create_cluster(" + path + ") => 0x" + future.Handle.ToString("x"));
#endif

			return future;
		}

		[Obsolete("Deprecated since API level 610")]
		public static void ClusterDestroy(IntPtr handle)
		{
			if (handle != IntPtr.Zero)
			{
				NativeMethods.fdb_cluster_destroy(handle);
			}
		}

		[Obsolete("Deprecated since API level 610")]
		public static FdbError ClusterSetOption(ClusterHandle cluster, FdbClusterOption option, byte* value, int valueLength)
		{
			return NativeMethods.fdb_cluster_set_option(cluster, option, value, valueLength);
		}

		[Obsolete("Deprecated since API level 610")]
		public static FdbError FutureGetCluster(FutureHandle future, out ClusterHandle cluster)
		{
			var err = NativeMethods.fdb_future_get_cluster(future, out cluster);
#if DEBUG_NATIVE_CALLS
			Debug.WriteLine("fdb_future_get_cluster(0x" + future.Handle.ToString("x") + ") => err=" + err + ", handle=0x" + cluster.Handle.ToString("x"));
#endif
			//TODO: check if err == Success ?
			return err;
		}

		#endregion

		#region Databases...

		public static FdbError CreateDatabase(string? path, out DatabaseHandle database)
		{
			var err = NativeMethods.fdb_create_database(path, out database);
#if DEBUG_NATIVE_CALLS
			Debug.WriteLine("fdb_create_database(" + path + ") => err=" + err);
#endif

			//TODO: check if err == Success ?
			return err;
		}


		public static FdbError FutureGetDatabase(FutureHandle future, out DatabaseHandle database)
		{
			var err = NativeMethods.fdb_future_get_database(future, out database);
#if DEBUG_NATIVE_CALLS
			Debug.WriteLine("fdb_future_get_database(0x" + future.Handle.ToString("x") + ") => err=" + err + ", handle=0x" + database.Handle.ToString("x"));
#endif
			//TODO: check if err == Success ?
			return err;
		}

		public static FdbError DatabaseSetOption(DatabaseHandle database, FdbDatabaseOption option, byte* value, int valueLength)
		{
			return NativeMethods.fdb_database_set_option(database, option, value, valueLength);
		}

		public static void DatabaseDestroy(IntPtr handle)
		{
			if (handle != IntPtr.Zero)
			{
				NativeMethods.fdb_database_destroy(handle);
			}
		}

		public static double GetMainThreadBusyness(DatabaseHandle handle)
		{
			EnsureLibraryIsLoaded();
			return NativeMethods.fdb_database_get_main_thread_busyness(handle);
		}

		[Obsolete("Deprecated since API level 610")]
		public static FutureHandle ClusterCreateDatabase(ClusterHandle cluster, string name)
		{
			var future = NativeMethods.fdb_cluster_create_database(cluster, name, name == null ? 0 : name.Length);
			Contract.Debug.Assert(future != null);
#if DEBUG_NATIVE_CALLS
			Debug.WriteLine("fdb_cluster_create_database(0x" + cluster.Handle.ToString("x") + ", name: '" + name + "') => 0x" + cluster.Handle.ToString("x"));
#endif
			return future;
		}

		public static FdbError DatabaseOpenTenant(DatabaseHandle database, ReadOnlySpan<byte> name, out TenantHandle tenant)
		{
			fixed (byte* ptr = name)
			{
				var err = NativeMethods.fdb_database_open_tenant(database, ptr, name.Length, out tenant);
#if DEBUG_NATIVE_CALLS
				Debug.WriteLine("fdb_database_open_tenant(0x" + database.Handle.ToString("x") + ", '" + name + "') => err=" + err + ", handle=0x" + tenant.Handle.ToString("x"));
#endif
				return err;
			}
		}


		#endregion

		#region Tenants...

		public static void TenantDestroy(IntPtr handle)
		{
			if (handle != IntPtr.Zero)
			{
				NativeMethods.fdb_tenant_destroy(handle);
			}
		}

		public static FdbError TenantCreateTransaction(TenantHandle tenant, out TransactionHandle transaction)
		{
			var err = NativeMethods.fdb_tenant_create_transaction(tenant, out transaction);
#if DEBUG_NATIVE_CALLS
			Debug.WriteLine("fdb_tenant_create_transaction(0x" + tenant.Handle.ToString("x") + ") => err=" + err + ", handle=0x" + transaction.Handle.ToString("x"));
#endif
			return err;
		}

		#endregion

		#region Transactions...

		public static void TransactionDestroy(IntPtr handle)
		{
			if (handle != IntPtr.Zero)
			{
				NativeMethods.fdb_transaction_destroy(handle);
			}
		}

		public static FdbError TransactionSetOption(TransactionHandle transaction, FdbTransactionOption option, byte* value, int valueLength)
		{
			return NativeMethods.fdb_transaction_set_option(transaction, option, value, valueLength);
		}

		public static FdbError DatabaseCreateTransaction(DatabaseHandle database, out TransactionHandle transaction)
		{
			var err = NativeMethods.fdb_database_create_transaction(database, out transaction);
#if DEBUG_NATIVE_CALLS
			Debug.WriteLine("fdb_database_create_transaction(0x" + database.Handle.ToString("x") + ") => err=" + err + ", handle=0x" + transaction.Handle.ToString("x"));
#endif
			return err;
		}

		public static FutureHandle TransactionCommit(TransactionHandle transaction)
		{
			var future = NativeMethods.fdb_transaction_commit(transaction);
			Contract.Debug.Assert(future != null);
#if DEBUG_NATIVE_CALLS
			Debug.WriteLine("fdb_transaction_commit(0x" + transaction.Handle.ToString("x") + ") => 0x" + future.Handle.ToString("x"));
#endif
			return future;
		}

		public static FutureHandle TransactionGetVersionStamp(TransactionHandle transaction)
		{
			var future = NativeMethods.fdb_transaction_get_versionstamp(transaction);
			Contract.Debug.Assert(future != null);
#if DEBUG_NATIVE_CALLS
			Debug.WriteLine("fdb_transaction_get_versionstamp(0x" + transaction.Handle.ToString("x") + ") => 0x" + future.Handle.ToString("x"));
#endif
			return future;
		}

		public static FutureHandle TransactionWatch(TransactionHandle transaction, ReadOnlySpan<byte> key)
		{
			if (key.Length == 0) throw new ArgumentException("Key cannot be null or empty", nameof(key));

			fixed (byte* ptrKey = key)
			{
				var future = NativeMethods.fdb_transaction_watch(transaction, ptrKey, key.Length);
				Contract.Debug.Assert(future != null);
#if DEBUG_NATIVE_CALLS
				Debug.WriteLine("fdb_transaction_watch(0x" + transaction.Handle.ToString("x") + ", key: '" + FdbKey.Dump(key) + "') => 0x" + future.Handle.ToString("x"));
#endif
				return future;
			}
		}

		public static FutureHandle TransactionOnError(TransactionHandle transaction, FdbError errorCode)
		{
			var future = NativeMethods.fdb_transaction_on_error(transaction, errorCode);
			Contract.Debug.Assert(future != null);
#if DEBUG_NATIVE_CALLS
			Debug.WriteLine("fdb_transaction_on_error(0x" + transaction.Handle.ToString("x") + ", " + errorCode + ") => 0x" + future.Handle.ToString("x"));
#endif
			return future;
		}

		public static void TransactionReset(TransactionHandle transaction)
		{
#if DEBUG_NATIVE_CALLS
			Debug.WriteLine("fdb_transaction_reset(0x" + transaction.Handle.ToString("x") + ")");
#endif
			NativeMethods.fdb_transaction_reset(transaction);
		}

		public static void TransactionCancel(TransactionHandle transaction)
		{
#if DEBUG_NATIVE_CALLS
			Debug.WriteLine("fdb_transaction_cancel(0x" + transaction.Handle.ToString("x") + ")");
#endif
			NativeMethods.fdb_transaction_cancel(transaction);
		}

		public static void TransactionSetReadVersion(TransactionHandle transaction, long version)
		{
#if DEBUG_NATIVE_CALLS
			Debug.WriteLine("fdb_transaction_set_read_version(0x" + transaction.Handle.ToString("x") + ", version: " + version.ToString() + ")");
#endif
			NativeMethods.fdb_transaction_set_read_version(transaction, version);
		}

		public static FutureHandle TransactionGetReadVersion(TransactionHandle transaction)
		{
			var future = NativeMethods.fdb_transaction_get_read_version(transaction);
			Contract.Debug.Assert(future != null);
#if DEBUG_NATIVE_CALLS
			Debug.WriteLine("fdb_transaction_get_read_version(0x" + transaction.Handle.ToString("x") + ") => 0x" + future.Handle.ToString("x"));
#endif
			return future;
		}

		public static FdbError TransactionGetCommittedVersion(TransactionHandle transaction, out long version)
		{
#if DEBUG_NATIVE_CALLS
			Debug.WriteLine("fdb_transaction_get_committed_version(0x" + transaction.Handle.ToString("x") + ")");
#endif
			return NativeMethods.fdb_transaction_get_committed_version(transaction, out version);
		}

		public static FdbError FutureGetVersion(FutureHandle future, out long version)
		{
#if DEBUG_NATIVE_CALLS
			Debug.WriteLine("fdb_future_get_version(0x" + future.Handle.ToString("x") + ")");
#endif
			// for 620 or above, we must use fdb_future_get_int64
			// for 610 and below, we must use fdb_future_get_version
			return NativeMethods.fdb_future_get_version(future, out version);
		}

		public static FdbError FutureGetInt64(FutureHandle future, out long version)
		{
#if DEBUG_NATIVE_CALLS
			Debug.WriteLine("fdb_future_get_int64(0x" + future.Handle.ToString("x") + ")");
#endif
			// for 620 or above, we must use fdb_future_get_int64
			// for 610 and below, we must use fdb_future_get_version
			return NativeMethods.fdb_future_get_int64(future, out version);
		}

		public static FutureHandle TransactionGet(TransactionHandle transaction, ReadOnlySpan<byte> key, bool snapshot)
		{
			// the empty key is allowed !
			fixed (byte* ptrKey = key)
			{
				var future = NativeMethods.fdb_transaction_get(transaction, ptrKey, key.Length, snapshot);
				Contract.Debug.Assert(future != null);
#if DEBUG_NATIVE_CALLS
				Debug.WriteLine("fdb_transaction_get(0x" + transaction.Handle.ToString("x") + ", key: '" + FdbKey.Dump(key) + "', snapshot: " + snapshot + ") => 0x" + future.Handle.ToString("x"));
#endif
				return future;
			}
		}

		public static FutureHandle TransactionGetRange(TransactionHandle transaction, KeySelector begin, KeySelector end, int limit, int targetBytes, FdbStreamingMode mode, int iteration, bool snapshot, bool reverse)
		{
			fixed (byte* ptrBegin = begin.Key)
			fixed (byte* ptrEnd = end.Key)
			{
				var future = NativeMethods.fdb_transaction_get_range(
					transaction,
					ptrBegin, begin.Key.Count, begin.OrEqual, begin.Offset,
					ptrEnd, end.Key.Count, end.OrEqual, end.Offset,
					limit, targetBytes, mode, iteration, snapshot, reverse);
				Contract.Debug.Assert(future != null);
#if DEBUG_NATIVE_CALLS
				Debug.WriteLine("fdb_transaction_get_range(0x" + transaction.Handle.ToString("x") + ", begin: " + begin.PrettyPrint(FdbKey.PrettyPrintMode.Begin) + ", end: " + end.PrettyPrint(FdbKey.PrettyPrintMode.End) + ", " + snapshot + ") => 0x" + future.Handle.ToString("x"));
#endif
				return future;
			}
		}

		public static FutureHandle TransactionGetKey(TransactionHandle transaction, KeySelector selector, bool snapshot)
		{
			if (selector.Key.IsNull) throw new ArgumentException("Key cannot be null", nameof(selector));

			fixed (byte* ptrKey = selector.Key)
			{
				var future = NativeMethods.fdb_transaction_get_key(transaction, ptrKey, selector.Key.Count, selector.OrEqual, selector.Offset, snapshot);
				Contract.Debug.Assert(future != null);
#if DEBUG_NATIVE_CALLS
				Debug.WriteLine("fdb_transaction_get_key(0x" + transaction.Handle.ToString("x") + ", " + selector.ToString() + ", " + snapshot + ") => 0x" + future.Handle.ToString("x"));
#endif
				return future;
			}
		}

		public static FutureHandle TransactionGetAddressesForKey(TransactionHandle transaction, ReadOnlySpan<byte> key)
		{
			if (key.Length == 0) throw new ArgumentException("Key cannot be null or empty", nameof(key));

			fixed (byte* ptrKey = key)
			{
				var future = NativeMethods.fdb_transaction_get_addresses_for_key(transaction, ptrKey, key.Length);
				Contract.Debug.Assert(future != null);
#if DEBUG_NATIVE_CALLS
				Debug.WriteLine("fdb_transaction_get_addresses_for_key(0x" + transaction.Handle.ToString("x") + ", key: '" + FdbKey.Dump(key) + "') => 0x" + future.Handle.ToString("x"));
#endif
				return future;
			}
		}

		public static FutureHandle TransactionGetRangeSplitPoints(TransactionHandle transaction, ReadOnlySpan<byte> beginKey, ReadOnlySpan<byte> endKey, long chunkSize)
		{
			fixed (byte* ptrBeginKey = beginKey)
			fixed (byte* ptrEndKey = endKey)
			{
				var future = NativeMethods.fdb_transaction_get_range_split_points(transaction, ptrBeginKey, beginKey.Length, ptrEndKey, endKey.Length, chunkSize);
				Contract.Debug.Assert(future != null);
#if DEBUG_NATIVE_CALLS
				Debug.WriteLine("fdb_transaction_get_range_split_points(0x" + transaction.Handle.ToString("x") + ", begin: '" + FdbKey.Dump(beginKey) + "', end: '" + FdbKey.Dump(endKey) + "') => 0x" + future.Handle.ToString("x"));
#endif
				return future;
			}
		}

		public static FutureHandle TransactionGetEstimatedRangeSizeBytes(TransactionHandle transaction, ReadOnlySpan<byte> beginKey, ReadOnlySpan<byte> endKey)
		{
			fixed (byte* ptrBeginKey = beginKey)
			fixed (byte* ptrEndKey = endKey)
			{
				var future = NativeMethods.fdb_transaction_get_estimated_range_size_bytes(transaction, ptrBeginKey, beginKey.Length, ptrEndKey, endKey.Length);
				Contract.Debug.Assert(future != null);
#if DEBUG_NATIVE_CALLS
				Debug.WriteLine("fdb_transaction_get_estimated_range_size_bytes(0x" + transaction.Handle.ToString("x") + ", begin: '" + FdbKey.Dump(beginKey) + "', end: '" + FdbKey.Dump(endKey) + "') => 0x" + future.Handle.ToString("x"));
#endif
				return future;
			}
		}

		public static FdbError FutureGetValue(FutureHandle future, out bool valuePresent, out ReadOnlySpan<byte> value)
		{
			Contract.Debug.Requires(future != null);

			var err = NativeMethods.fdb_future_get_value(future, out valuePresent, out byte* ptr, out int valueLength);
#if DEBUG_NATIVE_CALLS
			Debug.WriteLine("fdb_future_get_value(0x" + future.Handle.ToString("x") + ") => err=" + err + ", present=" + valuePresent + ", valueLength=" + valueLength);
#endif
			if (valueLength > 0 && ptr != null)
			{
				value = new ReadOnlySpan<byte>(ptr, valueLength);
			}
			else
			{
				value = default;
			}
			return err;
		}

		public static FdbError FutureGetKey(FutureHandle future, out ReadOnlySpan<byte> key)
		{
			var err = NativeMethods.fdb_future_get_key(future, out byte* ptr, out int keyLength);
#if DEBUG_NATIVE_CALLS
			Debug.WriteLine("fdb_future_get_key(0x" + future.Handle.ToString("x") + ") => err=" + err + ", keyLength=" + keyLength);
#endif

			// note: fdb_future_get_key is allowed to return NULL for the empty key (not to be confused with a key that has an empty value)
			Contract.Debug.Assert(keyLength >= 0 && keyLength <= Fdb.MaxKeySize);

			if (keyLength > 0 && ptr != null)
			{
				key = new ReadOnlySpan<byte>(ptr, keyLength);
			}
			else
			{ // from the spec: "If a key selector would otherwise describe a key off the beginning of the database, it instead resolves to the empty key ''."
				key = default;
			}

			return err;
		}

		public static FdbError FutureGetKeyValueArray(FutureHandle future, out KeyValuePair<Slice, Slice>[]? result, out bool more)
		{
			result = null;

			var err = NativeMethods.fdb_future_get_keyvalue_array(future, out FdbKeyValue* kvp, out int count, out more);
#if DEBUG_NATIVE_CALLS
			Debug.WriteLine("fdb_future_get_keyvalue_array(0x" + future.Handle.ToString("x") + ") => err=" + err + ", count=" + count + ", more=" + more);
#endif

			if (err == FdbError.Success)
			{
				Contract.Debug.Assert(count >= 0, "Return count was negative");

				result = count > 0 ? new KeyValuePair<Slice, Slice>[count] : Array.Empty<KeyValuePair<Slice, Slice>>();

				if (count > 0)
				{ // convert the FdbKeyValue result into an array of slices

					Contract.Debug.Assert(kvp != null, "We have results but array pointer was null");

					// in order to reduce allocations, we want to merge all keys and values
					// into a single byte[] and return a list of Slice that will
					// link to the different chunks of this buffer.

					// first pass to compute the total size needed
					long total = 0;
					for (int i = 0; i < count; i++)
					{
						uint kl = kvp[i].KeyLength;
						uint vl = kvp[i].ValueLength;
						if (kl > int.MaxValue) throw ThrowHelper.InvalidOperationException("A Key has a length that is larger than a signed 32-bit int!");
						total += kl;
						if (vl > int.MaxValue) throw ThrowHelper.InvalidOperationException("A Value has a length that is larger than a signed 32-bit int!");
						total += vl;
					}
					if (total > int.MaxValue) throw ThrowHelper.NotSupportedException("Cannot read more than 2GB of key/value data in a single batch!");

					// allocate all memory in one chunk, and make the key/values point to it
					// Does fdb allocate all keys into a single buffer ? We could copy everything in one pass,
					// but it would rely on implementation details that could break at anytime...

					//TODO: protect against too much memory allocated ?
					// what would be a good max value? we need to at least be able to handle FDB_STREAMING_MODE_WANT_ALL

					//TODO: some keys/values will be small (32 bytes or less) while other will be big
					//consider having to copy methods, optimized for each scenario ?

					//TODO: PERF: find a way to use Memory Pooling for this?
					var page = new byte[total];
					int p = 0;
					for (int i = 0; i < result.Length; i++)
					{
						int kl = (int) kvp[i].KeyLength;
						new ReadOnlySpan<byte>(kvp[i].Key.ToPointer(), kl).CopyTo(page.AsSpan(p));
						var key = page.AsSlice(p, kl);
						p += kl;

						int vl = (int) kvp[i].ValueLength;
						new ReadOnlySpan<byte>(kvp[i].Value.ToPointer(), vl).CopyTo(page.AsSpan(p));
						var value = page.AsSlice(p, vl);
						p += vl;

						result[i] = new KeyValuePair<Slice, Slice>(key, value);
					}
				}
			}

			return err;
		}

		public static FdbError FutureGetKeyValueArrayKeysOnly(FutureHandle future, out KeyValuePair<Slice, Slice>[]? result, out bool more)
		{
			result = null;

			var err = NativeMethods.fdb_future_get_keyvalue_array(future, out FdbKeyValue* kvp, out int count, out more);
#if DEBUG_NATIVE_CALLS
			Debug.WriteLine("fdb_future_get_keyvalue_array(0x" + future.Handle.ToString("x") + ") => err=" + err + ", count=" + count + ", more=" + more);
#endif

			if (err == FdbError.Success)
			{
				Contract.Debug.Assert(count >= 0, "Return count was negative");

				result = count > 0 ? new KeyValuePair<Slice, Slice>[count] : Array.Empty<KeyValuePair<Slice, Slice>>();

				if (count > 0)
				{ // convert the FdbKeyValue result into an array of slices

					Contract.Debug.Assert(kvp != null, "We have results but array pointer was null");

					// in order to reduce allocations, we want to merge all keys and values
					// into a single byte[] and return a list of Slice that will
					// link to the different chunks of this buffer.

					// first pass to compute the total size needed
					long total = 0;
					for (int i = 0; i < count; i++)
					{
						uint kl = kvp[i].KeyLength;
						uint vl = kvp[i].ValueLength;
						if (kl > int.MaxValue) throw new InvalidOperationException("A Key has a length that is larger than a signed 32-bit int!");
						if (vl > int.MaxValue) throw new InvalidOperationException("A Value has a length that is larger than a signed 32-bit int!");
						total += kl;
					}
					if (total > int.MaxValue) throw new NotSupportedException("Cannot read more than 2GB of key data in a single batch!");

					// allocate all memory in one chunk, and make the key/values point to it
					// Does fdb allocate all keys into a single buffer ? We could copy everything in one pass,
					// but it would rely on implementation details that could break at anytime...

					//TODO: protect against too much memory allocated ?
					// what would be a good max value? we need to at least be able to handle FDB_STREAMING_MODE_WANT_ALL

					//TODO: some keys/values will be small (32 bytes or less) while other will be big
					//consider having to copy methods, optimized for each scenario ?

					//TODO: PERF: find a way to use Memory Pooling for this?
					var page = new byte[total];
					int p = 0;
					for (int i = 0; i < result.Length; i++)
					{
						int kl = checked((int) kvp[i].KeyLength);
						new ReadOnlySpan<byte>(kvp[i].Key.ToPointer(), kl).CopyTo(page.AsSpan(p));
						var key = page.AsSlice(p, kl);
						p += kl;

						result[i] = new KeyValuePair<Slice, Slice>(key, default);
					}

					Contract.Debug.Assert(p == total);
				}
			}

			return err;
		}

		public static FdbError FutureGetKeyValueArrayValuesOnly(FutureHandle future, out KeyValuePair<Slice, Slice>[]? result, out bool more, out Slice first, out Slice last)
		{
			result = null;
			first = default;
			last = default;

			var err = NativeMethods.fdb_future_get_keyvalue_array(future, out FdbKeyValue* kvp, out int count, out more);
#if DEBUG_NATIVE_CALLS
			Debug.WriteLine("fdb_future_get_keyvalue_array(0x" + future.Handle.ToString("x") + ") => err=" + err + ", count=" + count + ", more=" + more);
#endif

			if (err == FdbError.Success)
			{
				Contract.Debug.Assert(count >= 0, "Return count was negative");

				result = count > 0 ? new KeyValuePair<Slice, Slice>[count] : Array.Empty<KeyValuePair<Slice, Slice>>();

				if (count > 0)
				{ // convert the FdbKeyValue result into an array of slices

					Contract.Debug.Assert(kvp != null, "We have results but array pointer was null");

					// in order to reduce allocations, we want to merge all keys and values
					// into a single byte[] and return a list of Slice that will
					// link to the different chunks of this buffer.

					int end = count - 1;

					// first pass to compute the total size needed
					long total = 0;
					for (int i = 0; i < count; i++)
					{
						//TODO: protect against negative values or values too big ?
						uint kl = kvp[i].KeyLength;
						uint vl = kvp[i].ValueLength;
						if (kl > int.MaxValue) throw new InvalidOperationException("A Key has a length that is larger than a signed 32-bit int!");
						if (vl > int.MaxValue) throw new InvalidOperationException("A Value has a length that is larger than a signed 32-bit int!");
						if (i == 0 || i == end) total += kl;
						total += vl;
					}
					if (total > int.MaxValue) throw new NotSupportedException("Cannot read more than 2GB of value data in a single batch!");

					// allocate all memory in one chunk, and make the key/values point to it
					// Does fdb allocate all keys into a single buffer ? We could copy everything in one pass,
					// but it would rely on implementation details that could break at anytime...

					//TODO: protect against too much memory allocated ?
					// what would be a good max value? we need to at least be able to handle FDB_STREAMING_MODE_WANT_ALL

					//TODO: some keys/values will be small (32 bytes or less) while other will be big
					//consider having to copy methods, optimized for each scenario ?

					//TODO: PERF: find a way to use Memory Pooling for this?
					var page = new byte[total];
					int p = 0;
					for (int i = 0; i < result.Length; i++)
					{
						// note: even if we only read the values, we still need to keep the first and last keys,
						// because we will need them for pagination when reading multiple ranges (ex: last key will be used as selector for next chunk when going forward)
						if (i == 0 || i == end)
						{
							int kl = checked((int) kvp[i].KeyLength);
							new ReadOnlySpan<byte>(kvp[i].Key.ToPointer(), kl).CopyTo(page.AsSpan(p));
							var key = page.AsSlice(p, kl);
							p += kl;
							if (i == 0) first = key; else last = key;
						}

						int vl = checked((int) kvp[i].ValueLength);
						new ReadOnlySpan<byte>(kvp[i].Value.ToPointer(), vl).CopyTo(page.AsSpan(p));
						var value = page.AsSlice(p, vl);
						p += vl;

						result[i] = new KeyValuePair<Slice, Slice>(default, value);
					}
					Contract.Debug.Assert(p == total);
				}
			}

			return err;
		}

		public static FdbError FutureGetKeyArray(FutureHandle future, out Slice[]? result)
		{
			result = null;

			var err = NativeMethods.fdb_future_get_key_array(future, out FdbKey* kvp, out int count);
#if DEBUG_NATIVE_CALLS
			Debug.WriteLine("fdb_future_get_key_array(0x" + future.Handle.ToString("x") + ") => err=" + err + ", count=" + count);
#endif

			if (err == FdbError.Success)
			{
				Contract.Debug.Assert(count >= 0, "Return count was negative");

				if (count > 0)
				{ // convert the FdbKeyValue result into an array of slices

					Contract.Debug.Assert(kvp != null, "We have results but array pointer was null");

					//TODO: PERF: find a way to use Memory Pooling for this?
					result = new Slice[count];

					// in order to reduce allocations, we want to merge all keys
					// into a single byte[] and return a list of Slice that will
					// link to the different chunks of this buffer.

					// first pass to compute the total size needed
					long total = 0;
					for (int i = 0; i < count; i++)
					{
						uint kl = kvp[i].Length;
						if (kl > int.MaxValue) throw new InvalidOperationException("A Key has a length that is larger than a signed 32-bit int!");
						total += kl;
					}
					if (total > int.MaxValue) throw new NotSupportedException("Cannot read more than 2GB of key data in a single batch!");

					//TODO: PERF: find a way to use Memory Pooling for this?
					var page = new byte[total];
					int p = 0;
					for (int i = 0; i < result.Length; i++)
					{
						int kl = checked((int) kvp[i].Length);
						new ReadOnlySpan<byte>(kvp[i].Key.ToPointer(), kl).CopyTo(page.AsSpan(p));
						var key = page.AsSlice(p, kl);
						p += kl;

						result[i] = key;
					}

					Contract.Debug.Assert(p == total);
				}
				else
				{
					result = Array.Empty<Slice>();
				}
			}

			return err;
		}

		public static FdbError FutureGetStringArray(FutureHandle future, out string?[]? result)
		{
			result = null;

			var err = NativeMethods.fdb_future_get_string_array(future, out byte** strings, out int count);
#if DEBUG_NATIVE_CALLS
			Debug.WriteLine("fdb_future_get_string_array(0x" + future.Handle.ToString("x") + ") => err=" + err + ", count=" + count);
#endif

			if (err == FdbError.Success)
			{
				Contract.Debug.Assert(count >= 0, "Return count was negative");


				if (count > 0)
				{ // convert the keyvalue result into an array

					Contract.Debug.Assert(strings != null, "We have results but array pointer was null");

					result = new string[count];

					//TODO: if pointers are corrupted, or memory is garbled, we could very well walk around the heap, randomly copying a bunch of stuff (like passwords or jpegs of cats...)
					// there is no real way to ensure that pointers are valid, except maybe having a maximum valid size for strings, and they should probably only contain legible text ?

					for (int i = 0; i < result.Length; i++)
					{
						result[i] = ToManagedString(strings[i]);
					}
				}
				else
				{
					result = Array.Empty<string>();
				}
			}

			return err;
		}

		public static FdbError FutureGetVersionStamp(FutureHandle future, out VersionStamp stamp)
		{
			byte* ptr;
			int keyLength;
			var err = NativeMethods.fdb_future_get_key(future, out ptr, out keyLength);
#if DEBUG_NATIVE_CALLS
			Debug.WriteLine("fdb_future_get_key(0x" + future.Handle.ToString("x") + ") => err=" + err + ", keyLength=" + keyLength);
#endif

			if (keyLength != 10 || ptr == null)
			{
				//REVIEW: should we fail if len != 10? (would meed some MAJOR change in the fdb C API?)
				stamp = default;
				return err;
			}

			VersionStamp.ReadUnsafe(new ReadOnlySpan<byte>(ptr, 10), out stamp);
			return err;
		}

		public static void TransactionSet(TransactionHandle transaction, ReadOnlySpan<byte> key, ReadOnlySpan<byte> value)
		{
			fixed (byte* pKey = key)
			fixed (byte* pValue = value)
			{
#if DEBUG_NATIVE_CALLS
				Debug.WriteLine("fdb_transaction_set(0x" + transaction.Handle.ToString("x") + ", key: '" + FdbKey.Dump(key) + "', value: '" + FdbKey.Dump(value) + "')");
#endif
				NativeMethods.fdb_transaction_set(transaction, pKey, key.Length, pValue, value.Length);
			}
		}

		public static void TransactionAtomicOperation(TransactionHandle transaction, ReadOnlySpan<byte> key, ReadOnlySpan<byte> param, FdbMutationType operationType)
		{
			fixed (byte* pKey = key)
			fixed (byte* pParam = param)
			{
#if DEBUG_NATIVE_CALLS
				Debug.WriteLine("fdb_transaction_atomic_op(0x" + transaction.Handle.ToString("x") + ", key: '" + FdbKey.Dump(key) + "', param: '" + FdbKey.Dump(param) + "', " + operationType.ToString() + ")");
#endif
				NativeMethods.fdb_transaction_atomic_op(transaction, pKey, key.Length, pParam, param.Length, operationType);
			}
		}

		public static void TransactionClear(TransactionHandle transaction, ReadOnlySpan<byte> key)
		{
			fixed (byte* pKey = key)
			{
#if DEBUG_NATIVE_CALLS
				Debug.WriteLine("fdb_transaction_clear(0x" + transaction.Handle.ToString("x") + ", key: '" + FdbKey.Dump(key) + "')");
#endif
				NativeMethods.fdb_transaction_clear(transaction, pKey, key.Length);
			}
		}

		public static void TransactionClearRange(TransactionHandle transaction, ReadOnlySpan<byte> beginKey, ReadOnlySpan<byte> endKey)
		{
			fixed (byte* pBeginKey = beginKey)
			fixed (byte* pEndKey = endKey)
			{
#if DEBUG_NATIVE_CALLS
				Debug.WriteLine("fdb_transaction_clear_range(0x" + transaction.Handle.ToString("x") + ", beginKey: '" + FdbKey.Dump(beginKey) + ", endKey: '" + FdbKey.Dump(endKey) + "')");
#endif
				NativeMethods.fdb_transaction_clear_range(transaction, pBeginKey, beginKey.Length, pEndKey, endKey.Length);
			}
		}

		public static FdbError TransactionAddConflictRange(TransactionHandle transaction, ReadOnlySpan<byte> beginKey, ReadOnlySpan<byte> endKey, FdbConflictRangeType type)
		{
			fixed (byte* pBeginKey = beginKey)
			fixed (byte* pEndKey = endKey)
			{
#if DEBUG_NATIVE_CALLS
				Debug.WriteLine("fdb_transaction_add_conflict_range(0x" + transaction.Handle.ToString("x") + ", beginKey: '" + FdbKey.Dump(beginKey) + ", endKey: '" + FdbKey.Dump(endKey) + "', " + type.ToString() + ")");
#endif
				return NativeMethods.fdb_transaction_add_conflict_range(transaction, pBeginKey, beginKey.Length, pEndKey, endKey.Length, type);
			}
		}

		public static FutureHandle TransactionGetApproximateSize(TransactionHandle transaction)
		{
			var future = NativeMethods.fdb_transaction_get_approximate_size(transaction);
			Contract.Debug.Assert(future != null);
#if DEBUG_NATIVE_CALLS
			Debug.WriteLine("fdb_transaction_get_approximate_size(0x" + transaction.Handle.ToString("x") + ") => 0x" + future.Handle.ToString("x"));
#endif
			return future;
		}

		#endregion

	}

}
