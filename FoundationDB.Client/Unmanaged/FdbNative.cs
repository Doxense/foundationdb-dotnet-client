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

// enable this to help debug native calls to fdbc.dll
#undef DEBUG_NATIVE_CALLS

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

namespace FoundationDB.Client.Native
{
	internal static unsafe class FdbNative
	{
		public const int FDB_API_VERSION = 22;

		/// <summary>Name of the C API dll used for P/Invoking</summary>
		private const string FDB_C_DLL = "fdb_c.dll";

		/// <summary>Handle on the native FDB C API library</summary>
		private static readonly UnmanagedLibrary FdbCLib;

		/// <summary>Exception that was thrown when we last tried to load the native FDB C library (or null if nothing wrong happened)</summary>
		private static readonly Exception LibraryLoadError;

		/// <summary>Contain all the stubs to the methods exposed by the C API library</summary>
		[System.Security.SuppressUnmanagedCodeSecurity]
		public static class Stubs
		{

			// Core

			[DllImport(FDB_C_DLL, CallingConvention = CallingConvention.Cdecl)]
			public static extern FdbError fdb_select_api_version_impl(int runtimeVersion, int headerVersion);

			[DllImport(FDB_C_DLL, CallingConvention = CallingConvention.Cdecl)]
			public static extern int fdb_get_max_api_version();

			[DllImport(FDB_C_DLL, CallingConvention = CallingConvention.Cdecl)]
			public static extern IntPtr fdb_get_error(FdbError code);

			// Network

			[DllImport(FDB_C_DLL, CallingConvention = CallingConvention.Cdecl)]
			public static extern FdbError fdb_network_set_option(FdbNetworkOption option, byte* value, int value_length);

			[DllImport(FDB_C_DLL, CallingConvention = CallingConvention.Cdecl)]
			public static extern FdbError fdb_setup_network();

			[DllImport(FDB_C_DLL, CallingConvention = CallingConvention.Cdecl)]
			public static extern FdbError fdb_run_network();

			[DllImport(FDB_C_DLL, CallingConvention = CallingConvention.Cdecl)]
			public static extern FdbError fdb_stop_network();

			// Cluster

			[DllImport(FDB_C_DLL, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
			public static extern FutureHandle fdb_create_cluster(string clusterFilePath);

			[DllImport(FDB_C_DLL, CallingConvention = CallingConvention.Cdecl)]
			public static extern void fdb_cluster_destroy(IntPtr cluster);

			[DllImport(FDB_C_DLL, CallingConvention = CallingConvention.Cdecl)]
			public static extern FdbError fdb_cluster_set_option(ClusterHandle cluster, FdbClusterOption option, byte* value, int valueLength);

			[DllImport(FDB_C_DLL, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
			public static extern FutureHandle fdb_cluster_create_database(ClusterHandle cluster, string dbName, int dbNameLength);

			// Database

			[DllImport(FDB_C_DLL, CallingConvention = CallingConvention.Cdecl)]
			public static extern void fdb_database_destroy(IntPtr database);

			[DllImport(FDB_C_DLL, CallingConvention = CallingConvention.Cdecl)]
			public static extern FdbError fdb_database_set_option(DatabaseHandle handle, FdbDatabaseOption option, byte* value, int valueLength);

			[DllImport(FDB_C_DLL, CallingConvention = CallingConvention.Cdecl)]
			public static extern FdbError fdb_database_create_transaction(DatabaseHandle database, out TransactionHandle transaction);

			// Transaction

			[DllImport(FDB_C_DLL, CallingConvention = CallingConvention.Cdecl)]
			public static extern void fdb_transaction_destroy(IntPtr database);

			[DllImport(FDB_C_DLL, CallingConvention = CallingConvention.Cdecl)]
			public static extern FdbError fdb_transaction_set_option(TransactionHandle handle, FdbTransactionOption option, byte* value, int valueLength);

			[DllImport(FDB_C_DLL, CallingConvention = CallingConvention.Cdecl)]
			public static extern void fdb_transaction_set_read_version(TransactionHandle handle, long version);

			[DllImport(FDB_C_DLL, CallingConvention = CallingConvention.Cdecl)]
			public static extern FutureHandle fdb_transaction_get_read_version(TransactionHandle transaction);

			[DllImport(FDB_C_DLL, CallingConvention = CallingConvention.Cdecl)]
			public static extern FutureHandle fdb_transaction_get(TransactionHandle transaction, byte* keyName, int keyNameLength, bool snapshot);

			[DllImport(FDB_C_DLL, CallingConvention = CallingConvention.Cdecl)]
			public static extern FutureHandle fdb_transaction_get_key(TransactionHandle transaction, byte* keyName, int keyNameLength, bool orEqual, int offset, bool snapshot);

			[DllImport(FDB_C_DLL, CallingConvention = CallingConvention.Cdecl)]
			public static extern FutureHandle fdb_transaction_get_range(
				TransactionHandle transaction,
				byte* beginKeyName, int beginKeyNameLength, bool beginOrEqual, int beginOffset,
				byte* endKeyName, int endKeyNameLength, bool endOrEqual, int endOffset,
				int limit, int targetBytes, FdbStreamingMode mode, int iteration, bool snapshot, bool reverse
			);

			[DllImport(FDB_C_DLL, CallingConvention = CallingConvention.Cdecl)]
			public static extern void fdb_transaction_set(TransactionHandle transaction, byte* keyName, int keyNameLength, byte* value, int valueLength);

			[DllImport(FDB_C_DLL, CallingConvention = CallingConvention.Cdecl)]
			public static extern void fdb_transaction_clear(TransactionHandle transaction, byte* keyName, int keyNameLength);

			[DllImport(FDB_C_DLL, CallingConvention = CallingConvention.Cdecl)]
			public static extern void fdb_transaction_clear_range(
				TransactionHandle transaction,
				byte* beginKeyName, int beginKeyNameLength,
				byte* endKeyName, int endKeyNameLength
			);

			[DllImport(FDB_C_DLL, CallingConvention = CallingConvention.Cdecl)]
			public static extern FutureHandle fdb_transaction_commit(TransactionHandle transaction);

			[DllImport(FDB_C_DLL, CallingConvention = CallingConvention.Cdecl)]
			public static extern FdbError fdb_transaction_get_committed_version(TransactionHandle transaction, out long version);

			[DllImport(FDB_C_DLL, CallingConvention = CallingConvention.Cdecl)]
			public static extern FutureHandle fdb_transaction_on_error(TransactionHandle transaction, FdbError error);

			[DllImport(FDB_C_DLL, CallingConvention = CallingConvention.Cdecl)]
			public static extern void fdb_transaction_reset(TransactionHandle transaction);

			// Future

			[DllImport(FDB_C_DLL, CallingConvention = CallingConvention.Cdecl)]
			public static extern void fdb_future_destroy(IntPtr future);

			[DllImport(FDB_C_DLL, CallingConvention = CallingConvention.Cdecl)]
			public static extern FdbError fdb_future_block_until_ready(FutureHandle futureHandle);

			[DllImport(FDB_C_DLL, CallingConvention = CallingConvention.Cdecl)]
			public static extern bool fdb_future_is_ready(FutureHandle futureHandle);

			[DllImport(FDB_C_DLL, CallingConvention = CallingConvention.Cdecl)]
			public static extern bool fdb_future_is_error(FutureHandle futureHandle);

			[DllImport(FDB_C_DLL, CallingConvention = CallingConvention.Cdecl)]
			public static extern FdbError fdb_future_set_callback(FutureHandle future, FdbFutureCallback callback, IntPtr callbackParameter);

			[DllImport(FDB_C_DLL, CallingConvention = CallingConvention.Cdecl)]
			public static extern FdbError fdb_future_get_error(FutureHandle future, IntPtr* description);

			[DllImport(FDB_C_DLL, CallingConvention = CallingConvention.Cdecl)]
			public static extern FdbError fdb_future_get_version(FutureHandle future, out long version);

			[DllImport(FDB_C_DLL, CallingConvention = CallingConvention.Cdecl)]
			public static extern FdbError fdb_future_get_key(FutureHandle future, out byte* key, out int keyLength);

			[DllImport(FDB_C_DLL, CallingConvention = CallingConvention.Cdecl)]
			public static extern FdbError fdb_future_get_cluster(FutureHandle future, out ClusterHandle cluster);

			[DllImport(FDB_C_DLL, CallingConvention = CallingConvention.Cdecl)]
			public static extern FdbError fdb_future_get_database(FutureHandle future, out DatabaseHandle database);

			[DllImport(FDB_C_DLL, CallingConvention = CallingConvention.Cdecl)]
			public static extern FdbError fdb_future_get_value(FutureHandle future, out bool present, out byte* value, out int valueLength);

			[DllImport(FDB_C_DLL, CallingConvention = CallingConvention.Cdecl)]
			public static extern FdbError fdb_future_get_keyvalue_array(FutureHandle future, out FdbKeyValue* kv, out int count, out bool more);
		}

		static FdbNative()
		{
			// Impact of NativeLibPath:
			// - If null, don't preload the library, and let the CLR find the file using the default P/Invoke behavior
			// - If String.Empty, call win32 LoadLibrary("fdb_c.dll") and let the os find the file (using the standard OS behavior)
			// - Else, combine the path with "fdb_c.dll" and call LoadLibrary with the resulting (relative or absolute) path

			if (Fdb.Options.NativeLibPath != null)
			{
				try
				{
					FdbCLib = UnmanagedLibrary.LoadLibrary(Path.Combine(Fdb.Options.NativeLibPath, FDB_C_DLL));
				}
				catch (Exception e)
				{
					if (FdbCLib != null) FdbCLib.Dispose();
					FdbCLib = null;
					LibraryLoadError = e;
				}
			}
		}

		/// <summary>Returns true if the C API dll has been loaded properly</summary>
		public static bool IsLoaded
		{
			get { return LibraryLoadError == null && FdbCLib != null; }
		}

		private static void EnsureLibraryIsLoaded()
		{
			// should be inlined
			if (LibraryLoadError != null || FdbCLib == null) FailLibraryDidNotLoad();
		}

		private static void FailLibraryDidNotLoad()
		{
			throw new InvalidOperationException("An error occured while loading native FoundationDB library", LibraryLoadError);
		}

		private static string ToManagedString(byte* nativeString)
		{
			if (nativeString == null) return null;
			return Marshal.PtrToStringAnsi(new IntPtr((void*)nativeString));
		}

		private static string ToManagedString(IntPtr nativeString)
		{
			if (nativeString == IntPtr.Zero) return null;
			return Marshal.PtrToStringAnsi(nativeString);
		}

		/// <summary>Converts a string into an ANSI byte array</summary>
		/// <param name="value">String to convert (or null)</param>
		/// <param name="nullTerminated">If true, adds a terminating \0 at the end (C-style strings)</param>
		/// <param name="length">Receives the size of the string including the optional NUL terminator (or 0 if <paramref name="value"/> is null)</param>
		/// <returns>Byte array with the ANSI-encoded string with an optional NUL terminator, or null if <paramref name="value"/> was null</returns>
		public static Slice ToNativeString(string value, bool nullTerminated)
		{
			if (value == null) return Slice.Nil;
			if (value.Length == 0) return Slice.Empty;

			byte[] result;
			if (nullTerminated)
			{ // NULL terminated ANSI string
				result = new byte[value.Length + 1];
				Encoding.Default.GetBytes(value, 0, value.Length, result, 0);
			}
			else
			{
				result = Encoding.Default.GetBytes(value);
			}
			return new Slice(result, 0, result.Length);
		}

		#region Core..

		/// <summary>fdb_get_error</summary>
		public static string GetError(FdbError code)
		{
			return ToManagedString(Stubs.fdb_get_error(code));
		}

		/// <summary>fdb_select_api_impl</summary>
		public static FdbError SelectApiVersionImpl(int runtimeVersion, int headerVersion)
		{
			EnsureLibraryIsLoaded();
			return Stubs.fdb_select_api_version_impl(runtimeVersion, headerVersion);
		}

		/// <summary>fdb_select_api_impl</summary>
		public static FdbError SelectApiVersion(int version)
		{
			return SelectApiVersionImpl(version, FDB_API_VERSION);
		}

		/// <summary>fdb_get_max_api_version</summary>
		public static int GetMaxApiVersion()
		{
			EnsureLibraryIsLoaded();
			return Stubs.fdb_get_max_api_version();
		}

		#endregion

		#region Futures...

		public static bool FutureIsReady(FutureHandle futureHandle)
		{
			return Stubs.fdb_future_is_ready(futureHandle);
		}

		public static void FutureDestroy(IntPtr futureHandle)
		{
			if (futureHandle != IntPtr.Zero)
			{
				Stubs.fdb_future_destroy(futureHandle);
			}
		}

		public static bool FutureIsError(FutureHandle futureHandle)
		{
			return Stubs.fdb_future_is_error(futureHandle);
		}

		/// <summary>Return the error got from a FDBFuture</summary>
		/// <param name="futureHandle"></param>
		/// <returns></returns>
		public static FdbError FutureGetError(FutureHandle future)
		{
			return Stubs.fdb_future_get_error(future, null);
		}

		public static FdbError FutureGetError(FutureHandle future, out string description)
		{
			var ptr = IntPtr.Zero;
			var err = Stubs.fdb_future_get_error(future, &ptr);
			description = ToManagedString(ptr);
			return err;
		}

		public static FdbError FutureBlockUntilReady(FutureHandle future)
		{
#if DEBUG_NATIVE_CALLS
			Debug.WriteLine("calling fdb_future_block_until_ready(0x" + future.Handle.ToString("x") + ")...");
#endif
			var err = Stubs.fdb_future_block_until_ready(future);
#if DEBUG_NATIVE_CALLS
			Debug.WriteLine("fdb_future_block_until_ready(0x" + future.Handle.ToString("x") + ") => err=" + err);
#endif
			return err;
		}

		public static FdbError FutureSetCallback(FutureHandle future, FdbFutureCallback callback, IntPtr callbackParameter)
		{
			var err = Stubs.fdb_future_set_callback(future, callback, callbackParameter);
#if DEBUG_NATIVE_CALLS
			Debug.WriteLine("fdb_future_set_callback(0x" + future.Handle.ToString("x") + ", 0x" + ptrCallback.ToString("x") + ") => err=" + err);
#endif
			return err;
		}

		#endregion

		#region Network...

		public static FdbError NetworkSetOption(FdbNetworkOption option, byte* value, int valueLength)
		{
			EnsureLibraryIsLoaded();
			return Stubs.fdb_network_set_option(option, value, valueLength);
		}

		public static FdbError SetupNetwork()
		{
			EnsureLibraryIsLoaded();
			return Stubs.fdb_setup_network();
		}

		public static FdbError RunNetwork()
		{
			EnsureLibraryIsLoaded();
			return Stubs.fdb_run_network();
		}

		public static FdbError StopNetwork()
		{
			EnsureLibraryIsLoaded();
			return Stubs.fdb_stop_network();
		}

		#endregion

		#region Clusters...

		public static FutureHandle CreateCluster(string path)
		{
			var future = Stubs.fdb_create_cluster(path);
#if DEBUG_NATIVE_CALLS
			Debug.WriteLine("fdb_create_cluster(" + path + ") => 0x" + future.Handle.ToString("x"));
#endif
			return future;
		}

		public static void ClusterDestroy(IntPtr handle)
		{
			if (handle != IntPtr.Zero)
			{
				Stubs.fdb_cluster_destroy(handle);
			}
		}

		public static FdbError ClusterSetOption(ClusterHandle cluster, FdbClusterOption option, byte* value, int valueLength)
		{
			return Stubs.fdb_cluster_set_option(cluster, option, value, valueLength);
		}

		public static FdbError FutureGetCluster(FutureHandle future, out ClusterHandle cluster)
		{
			var err = Stubs.fdb_future_get_cluster(future, out cluster);
#if DEBUG_NATIVE_CALLS
			Debug.WriteLine("fdb_future_get_cluster(0x" + future.Handle.ToString("x") + ") => err=" + err + ", handle=0x" + cluster.Handle.ToString("x"));
#endif
			//TODO: check if err == Success ?
			return err;
		}

		#endregion

		#region Databases...

		public static FdbError FutureGetDatabase(FutureHandle future, out DatabaseHandle database)
		{
			var err = Stubs.fdb_future_get_database(future, out database);
#if DEBUG_NATIVE_CALLS
			Debug.WriteLine("fdb_future_get_database(0x" + future.Handle.ToString("x") + ") => err=" + err + ", handle=0x" + database.Handle.ToString("x"));
#endif
			//TODO: check if err == Success ?
			return err;
		}

		public static FdbError DatabaseSetOption(DatabaseHandle database, FdbDatabaseOption option, byte* value, int valueLength)
		{
			return Stubs.fdb_database_set_option(database, option, value, valueLength);
		}

		public static void DatabaseDestroy(IntPtr handle)
		{
			if (handle != IntPtr.Zero)
			{
				Stubs.fdb_database_destroy(handle);
			}
		}

		public static FutureHandle ClusterCreateDatabase(ClusterHandle cluster, string name)
		{
			var future = Stubs.fdb_cluster_create_database(cluster, name, name.Length);
#if DEBUG_NATIVE_CALLS
			Debug.WriteLine("fdb_cluster_create_database(0x" + cluster.Handle.ToString("x") + ", name: '" + name + "') => 0x" + cluster.Handle.ToString("x"));
#endif
			return future;
		}

		#endregion

		#region Transactions...

		public static void TransactionDestroy(IntPtr handle)
		{
			if (handle != IntPtr.Zero)
			{
				Stubs.fdb_transaction_destroy(handle);
			}
		}

		public static FdbError TransactionSetOption(TransactionHandle transaction, FdbTransactionOption option, byte* value, int valueLength)
		{
			return Stubs.fdb_transaction_set_option(transaction, option, value, valueLength);
		}

		public static FdbError DatabaseCreateTransaction(DatabaseHandle database, out TransactionHandle transaction)
		{
			var err = Stubs.fdb_database_create_transaction(database, out transaction);
#if DEBUG_NATIVE_CALLS
			Debug.WriteLine("fdb_database_create_transaction(0x" + database.Handle.ToString("x") + ") => err=" + err + ", handle=0x" + transaction.Handle.ToString("x"));
#endif
			return err;
		}

		public static FutureHandle TransactionCommit(TransactionHandle transaction)
		{
			var future = Stubs.fdb_transaction_commit(transaction);
#if DEBUG_NATIVE_CALLS
			Debug.WriteLine("fdb_transaction_commit(0x" + transaction.Handle.ToString("x") + ") => 0x" + future.Handle.ToString("x"));
#endif
			return future;
		}

		public static FutureHandle TransactionOnError(TransactionHandle transaction, FdbError errorCode)
		{
			var future = Stubs.fdb_transaction_on_error(transaction, errorCode);
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
			Stubs.fdb_transaction_reset(transaction);
		}

		public static void TransactionSetReadVersion(TransactionHandle transaction, long version)
		{
#if DEBUG_NATIVE_CALLS
			Debug.WriteLine("fdb_transaction_set_read_version(0x" + transaction.Handle.ToString("x") + ", version: " + version.ToString() + ")");
#endif
			Stubs.fdb_transaction_set_read_version(transaction, version);
		}

		public static FutureHandle TransactionGetReadVersion(TransactionHandle transaction)
		{
			var future = Stubs.fdb_transaction_get_read_version(transaction);
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
			return Stubs.fdb_transaction_get_committed_version(transaction, out version);
		}

		public static FdbError FutureGetVersion(FutureHandle future, out long version)
		{
#if DEBUG_NATIVE_CALLS
			Debug.WriteLine("fdb_future_get_version(0x" + future.Handle.ToString("x") + ")");
#endif
			return Stubs.fdb_future_get_version(future, out version);
		}

		public static FutureHandle TransactionGet(TransactionHandle transaction, Slice key, bool snapshot)
		{
			if (key.IsNullOrEmpty) throw new ArgumentException("Key cannot be null or empty", "key");

			fixed (byte* ptrKey = key.Array)
			{
				var future = Stubs.fdb_transaction_get(transaction, ptrKey + key.Offset, key.Count, snapshot);
#if DEBUG_NATIVE_CALLS
				Debug.WriteLine("fdb_transaction_get(0x" + transaction.Handle.ToString("x") + ", key: '" + FdbKey.Dump(key) + "', snapshot: " + snapshot + ") => 0x" + future.Handle.ToString("x"));
#endif
				return future;
			}
		}

		public static FutureHandle TransactionGetRange(TransactionHandle transaction, FdbKeySelector begin, FdbKeySelector end, int limit, int targetBytes, FdbStreamingMode mode, int iteration, bool snapshot, bool reverse)
		{
			fixed (byte* ptrBegin = begin.Key.Array)
			fixed (byte* ptrEnd = end.Key.Array)
			{
				var future = Stubs.fdb_transaction_get_range(
					transaction,
					ptrBegin + begin.Key.Offset, begin.Key.Count, begin.OrEqual, begin.Offset,
					ptrEnd + end.Key.Offset, end.Key.Count, end.OrEqual, end.Offset,
					limit, targetBytes, mode, iteration, snapshot, reverse);
#if DEBUG_NATIVE_CALLS
					Debug.WriteLine("fdb_transaction_get_range(0x" + transaction.Handle.ToString("x") + ", begin: {'" + FdbKey.Dump(begin.Key) + "'," + begin.OrEqual + "," + begin.Offset + "}, end: {'" + FdbKey.Dump(end.Key) + "'," + end.OrEqual + "," + end.Offset + "}, " + snapshot + ") => 0x" + future.Handle.ToString("x"));
#endif
				return future;
			}
		}

		public static FutureHandle TransactionGetKey(TransactionHandle transaction, FdbKeySelector selector, bool snapshot)
		{
			if (selector.Key.IsNullOrEmpty) throw new ArgumentException("Key cannot be null or empty", "selector");

			fixed (byte* ptrKey = selector.Key.Array)
			{
				var future = Stubs.fdb_transaction_get_key(transaction, ptrKey + selector.Key.Offset, selector.Key.Count, selector.OrEqual, selector.Offset, snapshot);
#if DEBUG_NATIVE_CALLS
				Debug.WriteLine("fdb_transaction_get_key(0x" + transaction.Handle.ToString("x") + ", {'" + FdbKey.Dump(selector.Key) + "'," + selector.OrEqual + "," + selector.Offset + "}, " + snapshot + ") => 0x" + future.Handle.ToString("x"));
#endif
				return future;
			}
		}

		public static FdbError FutureGetValue(FutureHandle future, out bool valuePresent, out Slice value)
		{
			byte* ptr = null;
			int valueLength = 0;
			var err = Stubs.fdb_future_get_value(future, out valuePresent, out ptr, out valueLength);
#if DEBUG_NATIVE_CALLS
			Debug.WriteLine("fdb_future_get_value(0x" + future.Handle.ToString("x") + ") => err=" + err + ", present=" + valuePresent + ", valueLength=" + valueLength);
#endif
			if (ptr != null && valueLength >= 0)
			{
				var bytes = new byte[valueLength];
				Marshal.Copy(new IntPtr(ptr), bytes, 0, valueLength);
				value = new Slice(bytes, 0, valueLength);
			}
			else
			{
				value = Slice.Nil;
			}
			return err;
		}

		public static FdbError FutureGetKey(FutureHandle future, out Slice key)
		{
			byte* ptr = null;
			int keyLength = 0;
			var err = Stubs.fdb_future_get_key(future, out ptr, out keyLength);
#if DEBUG_NATIVE_CALLS
			Debug.WriteLine("fdb_future_get_key(0x" + future.Handle.ToString("x") + ") => err=" + err + ", keyLength=" + keyLength);
#endif
			key = Slice.Create(ptr, keyLength);
			return err;
		}

		public static FdbError FutureGetKeyValueArray(FutureHandle future, out KeyValuePair<Slice, Slice>[] result, out bool more)
		{
			result = null;
			more = false;

			int count;
			FdbKeyValue* kvp;

			var err = Stubs.fdb_future_get_keyvalue_array(future, out kvp, out count, out more);
#if DEBUG_NATIVE_CALLS
			Debug.WriteLine("fdb_future_get_keyvalue_array(0x" + future.Handle.ToString("x") + ") => err=" + err + ", count=" + count + ", more=" + more);
#endif

			if (Fdb.Success(err))
			{
				Debug.Assert(count >= 0, "Return count was negative");

				result = new KeyValuePair<Slice, Slice>[count];

				if (count > 0)
				{ // convert the keyvalue result into an array

					Debug.Assert(kvp != null, "We have results but array pointer was null");

					// in order to reduce allocations, we want to merge all keys and values
					// into a single byte{] and return  list of Slice that will
					// link to the different chunks of this buffer.

					// first pass to compute the total size needed
					int total = 0;
					for (int i = 0; i < count; i++)
					{
						//TODO: protect against negative values or values too big ?
						Debug.Assert(kvp[i].KeyLength >= 0 && kvp[i].KeyLength >= 0);
						total += kvp[i].KeyLength + kvp[i].ValueLength;
					}

					// allocate all memory in one chunk, and make the key/values point to it
					// Does fdb allocate all keys into a single buffer ? We could copy everything in one pass,
					// but it would rely on implementation details that could break at anytime...

					//TODO: protect against too much memory allocated ?
					// what would be a good max value? we need to at least be able to handle FDB_STREAMING_MODE_WANT_ALL

					var page = new byte[total];
					int p = 0;
					for (int i = 0; i < result.Length; i++)
					{
						int kl = kvp[i].KeyLength;
						int vl = kvp[i].ValueLength;

						//TODO: some keys/values will be small (32 bytes or less) while other will be big
						//consider having to copy methods, optimized for each scenario ?

						Marshal.Copy(kvp[i].Key, page, p, kl);
						Marshal.Copy(kvp[i].Value, page, p + kl, vl);

						result[i] = new KeyValuePair<Slice, Slice>(
							new Slice(page, p, kl),
							new Slice(page, p + kl, vl)
						);

						p += kl + vl;
					}
					Debug.Assert(p == total);
				}
			}

			return err;
		}

		public static void TransactionSet(TransactionHandle transaction, Slice key, Slice value)
		{
			fixed (byte* pKey = key.Array)
			fixed (byte* pValue = value.Array)
			{
#if DEBUG_NATIVE_CALLS
				Debug.WriteLine("fdb_transaction_set(0x" + transaction.Handle.ToString("x") + ", key: '" + FdbKey.Dump(key) + "', value: '" + FdbKey.Dump(value) + "')");
#endif
				Stubs.fdb_transaction_set(transaction, pKey + key.Offset, key.Count, pValue + value.Offset, value.Count);
			}
		}

		public static void TransactionClear(TransactionHandle transaction, Slice key)
		{
			fixed (byte* pKey = key.Array)
			{
#if DEBUG_NATIVE_CALLS
				Debug.WriteLine("fdb_transaction_clear(0x" + transaction.Handle.ToString("x") + ", key: '" + FdbKey.Dump(key) + "')");
#endif
				Stubs.fdb_transaction_clear(transaction, pKey + key.Offset, key.Count);
			}
		}

		public static void TransactionClearRange(TransactionHandle transaction, Slice beginKey, Slice endKey)
		{
			fixed (byte* pBeginKey = beginKey.Array)
			fixed (byte* pEndKey = endKey.Array)
			{
#if DEBUG_NATIVE_CALLS
				Debug.WriteLine("fdb_transaction_clear_range(0x" + transaction.Handle.ToString("x") + ", beginKey: '" + FdbKey.Dump(beginKey) + ", endKey: '" + FdbKey.Dump(endKey) + "')");
#endif
				Stubs.fdb_transaction_clear_range(transaction, pBeginKey + beginKey.Offset, beginKey.Count, pEndKey + endKey.Offset, endKey.Count);
			}
		}

		#endregion

	}

}
