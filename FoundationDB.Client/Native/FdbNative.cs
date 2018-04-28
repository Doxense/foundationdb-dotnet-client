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

// enable this to help debug native calls to fdbc.dll
//#define DEBUG_NATIVE_CALLS

namespace FoundationDB.Client.Native
{
	using System;
	using System.Collections.Generic;
	using System.IO;
	using System.Runtime.ExceptionServices;
	using System.Runtime.InteropServices;
	using System.Text;
	using Doxense.Diagnostics.Contracts;

	internal static unsafe class FdbNative
	{
		public const int FDB_API_MIN_VERSION = 200;
		public const int FDB_API_MAX_VERSION = 510;

#if __MonoCS__
		/// <summary>Name of the C API dll used for P/Invoking</summary>
		private const string FDB_C_DLL = "libfdb_c.so";
#else
		/// <summary>Name of the C API dll used for P/Invoking</summary>
		private const string FDB_C_DLL = "fdb_c";
#endif

		/// <summary>Handle on the native FDB C API library</summary>
		private static readonly UnmanagedLibrary FdbCLib;

		/// <summary>Exception that was thrown when we last tried to load the native FDB C library (or null if nothing wrong happened)</summary>
		private static readonly ExceptionDispatchInfo LibraryLoadError;

		[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
		public delegate void FdbFutureCallback(IntPtr future, IntPtr parameter);

		/// <summary>Contain all the stubs to the methods exposed by the C API library</summary>
		[System.Security.SuppressUnmanagedCodeSecurity]
		internal static class NativeMethods
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

			[DllImport(FDB_C_DLL, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi, BestFitMapping = false, ThrowOnUnmappableChar = true)]
			public static extern FutureHandle fdb_create_cluster([MarshalAs(UnmanagedType.LPStr)] string clusterFilePath);

			[DllImport(FDB_C_DLL, CallingConvention = CallingConvention.Cdecl)]
			public static extern void fdb_cluster_destroy(IntPtr cluster);

			[DllImport(FDB_C_DLL, CallingConvention = CallingConvention.Cdecl)]
			public static extern FdbError fdb_cluster_set_option(ClusterHandle cluster, FdbClusterOption option, byte* value, int valueLength);

			[DllImport(FDB_C_DLL, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi, BestFitMapping = false, ThrowOnUnmappableChar = true)]
			public static extern FutureHandle fdb_cluster_create_database(ClusterHandle cluster, [MarshalAs(UnmanagedType.LPStr)] string dbName, int dbNameLength);

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
			public static extern FutureHandle fdb_transaction_get_addresses_for_key(TransactionHandle transaction, byte* keyName, int keyNameLength);

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
			public static extern void fdb_transaction_atomic_op(TransactionHandle transaction, byte* keyName, int keyNameLength, byte* param, int paramLength, FdbMutationType operationType);

			[DllImport(FDB_C_DLL, CallingConvention = CallingConvention.Cdecl)]
			public static extern FutureHandle fdb_transaction_commit(TransactionHandle transaction);

			[DllImport(FDB_C_DLL, CallingConvention = CallingConvention.Cdecl)]
			public static extern FdbError fdb_transaction_get_committed_version(TransactionHandle transaction, out long version);

			[DllImport(FDB_C_DLL, CallingConvention = CallingConvention.Cdecl)]
			public static extern FutureHandle fdb_transaction_get_versionstamp(TransactionHandle transaction);

			[DllImport(FDB_C_DLL, CallingConvention = CallingConvention.Cdecl)]
			public static extern FutureHandle fdb_transaction_watch(TransactionHandle transaction, byte* keyName, int keyNameLength);

			[DllImport(FDB_C_DLL, CallingConvention = CallingConvention.Cdecl)]
			public static extern FutureHandle fdb_transaction_on_error(TransactionHandle transaction, FdbError error);

			[DllImport(FDB_C_DLL, CallingConvention = CallingConvention.Cdecl)]
			public static extern void fdb_transaction_reset(TransactionHandle transaction);

			[DllImport(FDB_C_DLL, CallingConvention = CallingConvention.Cdecl)]
			public static extern void fdb_transaction_cancel(TransactionHandle transaction);

			[DllImport(FDB_C_DLL, CallingConvention = CallingConvention.Cdecl)]
			public static extern FdbError fdb_transaction_add_conflict_range(TransactionHandle transaction, byte* beginKeyName, int beginKeyNameLength, byte* endKeyName, int endKeyNameLength, FdbConflictRangeType type);

			// Future

			[DllImport(FDB_C_DLL, CallingConvention = CallingConvention.Cdecl)]
			public static extern void fdb_future_destroy(IntPtr future);

			[DllImport(FDB_C_DLL, CallingConvention = CallingConvention.Cdecl)]
			public static extern void fdb_future_cancel(FutureHandle future);

			[DllImport(FDB_C_DLL, CallingConvention = CallingConvention.Cdecl)]
			public static extern void fdb_future_release_memory(FutureHandle future);

			[DllImport(FDB_C_DLL, CallingConvention = CallingConvention.Cdecl)]
			public static extern FdbError fdb_future_block_until_ready(FutureHandle futureHandle);

			[DllImport(FDB_C_DLL, CallingConvention = CallingConvention.Cdecl)]
			public static extern bool fdb_future_is_ready(FutureHandle futureHandle);

			[DllImport(FDB_C_DLL, CallingConvention = CallingConvention.Cdecl)]
			public static extern FdbError fdb_future_get_error(FutureHandle futureHandle);

			[DllImport(FDB_C_DLL, CallingConvention = CallingConvention.Cdecl)]
			public static extern FdbError fdb_future_set_callback(FutureHandle future, FdbFutureCallback callback, IntPtr callbackParameter);

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
			public static extern FdbError fdb_future_get_string_array(FutureHandle future, out byte** strings, out int count);

			[DllImport(FDB_C_DLL, CallingConvention = CallingConvention.Cdecl)]
			public static extern FdbError fdb_future_get_keyvalue_array(FutureHandle future, out FdbKeyValue* kv, out int count, out bool more);

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
				if (FdbCLib != null) FdbCLib.Dispose();
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

		private static string GetPreloadPath()
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
			if (LibraryLoadError != null) LibraryLoadError.Throw();
		}

		private static string ToManagedString(byte* nativeString)
		{
			if (nativeString == null) return null;
			return Marshal.PtrToStringAnsi(new IntPtr(nativeString));
		}

		private static string ToManagedString(IntPtr nativeString)
		{
			if (nativeString == IntPtr.Zero) return null;
			return Marshal.PtrToStringAnsi(nativeString);
		}

		/// <summary>Converts a string into an ANSI byte array</summary>
		/// <param name="value">String to convert (or null)</param>
		/// <param name="nullTerminated">If true, adds a terminating \0 at the end (C-style strings)</param>
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
			return Slice.CreateUnsafe(result, 0, result.Length);
		}


		#region Core..

		/// <summary>fdb_get_error</summary>
		public static string GetError(FdbError code)
		{
			return ToManagedString(NativeMethods.fdb_get_error(code));
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
			Debug.WriteLine("fdb_future_set_callback(0x" + future.Handle.ToString("x") + ", 0x" + ptrCallback.ToString("x") + ") => err=" + err);
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

		#endregion

		#region Clusters...

		public static FutureHandle CreateCluster(string path)
		{
			var future = NativeMethods.fdb_create_cluster(path);
			Contract.Assert(future != null);
#if DEBUG_NATIVE_CALLS
			Debug.WriteLine("fdb_create_cluster(" + path + ") => 0x" + future.Handle.ToString("x"));
#endif

			return future;
		}

		public static void ClusterDestroy(IntPtr handle)
		{
			if (handle != IntPtr.Zero)
			{
				NativeMethods.fdb_cluster_destroy(handle);
			}
		}

		public static FdbError ClusterSetOption(ClusterHandle cluster, FdbClusterOption option, byte* value, int valueLength)
		{
			return NativeMethods.fdb_cluster_set_option(cluster, option, value, valueLength);
		}

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

		public static FutureHandle ClusterCreateDatabase(ClusterHandle cluster, string name)
		{
			var future = NativeMethods.fdb_cluster_create_database(cluster, name, name == null ? 0 : name.Length);
			Contract.Assert(future != null);
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
			Contract.Assert(future != null);
#if DEBUG_NATIVE_CALLS
			Debug.WriteLine("fdb_transaction_commit(0x" + transaction.Handle.ToString("x") + ") => 0x" + future.Handle.ToString("x"));
#endif
			return future;
		}

		public static FutureHandle TransactionGetVersionStamp(TransactionHandle transaction)
		{
			var future = NativeMethods.fdb_transaction_get_versionstamp(transaction);
			Contract.Assert(future != null);
#if DEBUG_NATIVE_CALLS
			Debug.WriteLine("fdb_transaction_get_versionstamp(0x" + transaction.Handle.ToString("x") + ") => 0x" + future.Handle.ToString("x"));
#endif
			return future;
		}

		public static FutureHandle TransactionWatch(TransactionHandle transaction, Slice key)
		{
			if (key.IsNullOrEmpty) throw new ArgumentException("Key cannot be null or empty", "key");

			fixed (byte* ptrKey = key.Array)
			{
				var future = NativeMethods.fdb_transaction_watch(transaction, ptrKey + key.Offset, key.Count);
				Contract.Assert(future != null);
#if DEBUG_NATIVE_CALLS
				Debug.WriteLine("fdb_transaction_watch(0x" + transaction.Handle.ToString("x") + ", key: '" + FdbKey.Dump(key) + "') => 0x" + future.Handle.ToString("x"));
#endif
				return future;
			}
		}

		public static FutureHandle TransactionOnError(TransactionHandle transaction, FdbError errorCode)
		{
			var future = NativeMethods.fdb_transaction_on_error(transaction, errorCode);
			Contract.Assert(future != null);
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
			Contract.Assert(future != null);
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
			return NativeMethods.fdb_future_get_version(future, out version);
		}

		public static FutureHandle TransactionGet(TransactionHandle transaction, Slice key, bool snapshot)
		{
			if (key.IsNull) throw new ArgumentException("Key cannot be null", "key");

			// the empty key is allowed !
			if (key.Count == 0) key = Slice.Empty;

			fixed (byte* ptrKey = key.Array)
			{
				var future = NativeMethods.fdb_transaction_get(transaction, ptrKey + key.Offset, key.Count, snapshot);
				Contract.Assert(future != null);
#if DEBUG_NATIVE_CALLS
				Debug.WriteLine("fdb_transaction_get(0x" + transaction.Handle.ToString("x") + ", key: '" + FdbKey.Dump(key) + "', snapshot: " + snapshot + ") => 0x" + future.Handle.ToString("x"));
#endif
				return future;
			}
		}

		public static FutureHandle TransactionGetRange(TransactionHandle transaction, KeySelector begin, KeySelector end, int limit, int targetBytes, FdbStreamingMode mode, int iteration, bool snapshot, bool reverse)
		{
			fixed (byte* ptrBegin = begin.Key.Array)
			fixed (byte* ptrEnd = end.Key.Array)
			{
				var future = NativeMethods.fdb_transaction_get_range(
					transaction,
					ptrBegin + begin.Key.Offset, begin.Key.Count, begin.OrEqual, begin.Offset,
					ptrEnd + end.Key.Offset, end.Key.Count, end.OrEqual, end.Offset,
					limit, targetBytes, mode, iteration, snapshot, reverse);
				Contract.Assert(future != null);
#if DEBUG_NATIVE_CALLS
					Debug.WriteLine("fdb_transaction_get_range(0x" + transaction.Handle.ToString("x") + ", begin: " + begin.PrettyPrint(FdbKey.PrettyPrintMode.Begin) + ", end: " + end.PrettyPrint(FdbKey.PrettyPrintMode.End) + ", " + snapshot + ") => 0x" + future.Handle.ToString("x"));
#endif
				return future;
			}
		}

		public static FutureHandle TransactionGetKey(TransactionHandle transaction, KeySelector selector, bool snapshot)
		{
			if (selector.Key.IsNull) throw new ArgumentException("Key cannot be null", "selector");

			fixed (byte* ptrKey = selector.Key.Array)
			{
				var future = NativeMethods.fdb_transaction_get_key(transaction, ptrKey + selector.Key.Offset, selector.Key.Count, selector.OrEqual, selector.Offset, snapshot);
				Contract.Assert(future != null);
#if DEBUG_NATIVE_CALLS
				Debug.WriteLine("fdb_transaction_get_key(0x" + transaction.Handle.ToString("x") + ", " + selector.ToString() + ", " + snapshot + ") => 0x" + future.Handle.ToString("x"));
#endif
				return future;
			}
		}

		public static FutureHandle TransactionGetAddressesForKey(TransactionHandle transaction, Slice key)
		{
			if (key.IsNullOrEmpty) throw new ArgumentException("Key cannot be null or empty", "key");

			fixed (byte* ptrKey = key.Array)
			{
				var future = NativeMethods.fdb_transaction_get_addresses_for_key(transaction, ptrKey + key.Offset, key.Count);
				Contract.Assert(future != null);
#if DEBUG_NATIVE_CALLS
				Debug.WriteLine("fdb_transaction_get_addresses_for_key(0x" + transaction.Handle.ToString("x") + ", key: '" + FdbKey.Dump(key) + "') => 0x" + future.Handle.ToString("x"));
#endif
				return future;
			}
		}

		public static FdbError FutureGetValue(FutureHandle future, out bool valuePresent, out Slice value)
		{
			byte* ptr;
			int valueLength;
			var err = NativeMethods.fdb_future_get_value(future, out valuePresent, out ptr, out valueLength);
#if DEBUG_NATIVE_CALLS
			Debug.WriteLine("fdb_future_get_value(0x" + future.Handle.ToString("x") + ") => err=" + err + ", present=" + valuePresent + ", valueLength=" + valueLength);
#endif
			if (ptr != null && valueLength >= 0)
			{
				var bytes = new byte[valueLength];
				Marshal.Copy(new IntPtr(ptr), bytes, 0, valueLength);
				value = Slice.CreateUnsafe(bytes, 0, valueLength);
			}
			else
			{
				value = Slice.Nil;
			}
			return err;
		}

		public static FdbError FutureGetKey(FutureHandle future, out Slice key)
		{
			byte* ptr;
			int keyLength;
			var err = NativeMethods.fdb_future_get_key(future, out ptr, out keyLength);
#if DEBUG_NATIVE_CALLS
			Debug.WriteLine("fdb_future_get_key(0x" + future.Handle.ToString("x") + ") => err=" + err + ", keyLength=" + keyLength);
#endif

			// note: fdb_future_get_key is allowed to return NULL for the empty key (not to be confused with a key that has an empty value)
			Contract.Assert(keyLength >= 0 && keyLength <= Fdb.MaxKeySize);

			if (keyLength <= 0 || ptr == null)
			{ // from the spec: "If a key selector would otherwise describe a key off the beginning of the database, it instead resolves to the empty key ''."
				key = Slice.Empty;
			}
			else
			{
				key = Slice.Copy(ptr, keyLength);
			}
			return err;
		}

		public static FdbError FutureGetKeyValueArray(FutureHandle future, out KeyValuePair<Slice, Slice>[] result, out bool more)
		{
			result = null;

			int count;
			FdbKeyValue* kvp;

			var err = NativeMethods.fdb_future_get_keyvalue_array(future, out kvp, out count, out more);
#if DEBUG_NATIVE_CALLS
			Debug.WriteLine("fdb_future_get_keyvalue_array(0x" + future.Handle.ToString("x") + ") => err=" + err + ", count=" + count + ", more=" + more);
#endif

			if (Fdb.Success(err))
			{
				Contract.Assert(count >= 0, "Return count was negative");

				result = new KeyValuePair<Slice, Slice>[count];

				if (count > 0)
				{ // convert the keyvalue result into an array

					Contract.Assert(kvp != null, "We have results but array pointer was null");

					// in order to reduce allocations, we want to merge all keys and values
					// into a single byte{] and return  list of Slice that will
					// link to the different chunks of this buffer.

					// first pass to compute the total size needed
					int total = 0;
					for (int i = 0; i < count; i++)
					{
						//TODO: protect against negative values or values too big ?
						Contract.Assert(kvp[i].KeyLength >= 0 && kvp[i].ValueLength >= 0);
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
							page.AsSlice(p, kl),
							page.AsSlice(p + kl, vl)
						);

						p += kl + vl;
					}
					Contract.Assert(p == total);
				}
			}

			return err;
		}

		public static FdbError FutureGetStringArray(FutureHandle future, out string[] result)
		{
			result = null;

			int count;
			byte** strings;

			var err = NativeMethods.fdb_future_get_string_array(future, out strings, out count);
#if DEBUG_NATIVE_CALLS
			Debug.WriteLine("fdb_future_get_string_array(0x" + future.Handle.ToString("x") + ") => err=" + err + ", count=" + count);
#endif

			if (Fdb.Success(err))
			{
				Contract.Assert(count >= 0, "Return count was negative");

				result = new string[count];

				if (count > 0)
				{ // convert the keyvalue result into an array

					Contract.Assert(strings != null, "We have results but array pointer was null");

					//TODO: if pointers are corrupted, or memory is garbled, we could very well walk around the heap, randomly copying a bunch of stuff (like passwords or jpegs of cats...)
					// there is no real way to ensure that pointers are valid, except maybe having a maximum valid size for strings, and they should probably only contain legible text ?

					for (int i = 0; i < result.Length; i++)
					{
						result[i] = ToManagedString(strings[i]);
					}
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
				stamp = default;
				return err;
			}

			VersionStamp.ReadUnsafe(ptr, 10, out stamp);
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
				NativeMethods.fdb_transaction_set(transaction, pKey + key.Offset, key.Count, pValue + value.Offset, value.Count);
			}
		}

		public static void TransactionAtomicOperation(TransactionHandle transaction, Slice key, Slice param, FdbMutationType operationType)
		{
			fixed (byte* pKey = key.Array)
			fixed (byte* pParam = param.Array)
			{
#if DEBUG_NATIVE_CALLS
				Debug.WriteLine("fdb_transaction_atomic_op(0x" + transaction.Handle.ToString("x") + ", key: '" + FdbKey.Dump(key) + "', param: '" + FdbKey.Dump(param) + "', " + operationType.ToString() + ")");
#endif
				NativeMethods.fdb_transaction_atomic_op(transaction, pKey + key.Offset, key.Count, pParam + param.Offset, param.Count, operationType);
			}
		}

		public static void TransactionClear(TransactionHandle transaction, Slice key)
		{
			fixed (byte* pKey = key.Array)
			{
#if DEBUG_NATIVE_CALLS
				Debug.WriteLine("fdb_transaction_clear(0x" + transaction.Handle.ToString("x") + ", key: '" + FdbKey.Dump(key) + "')");
#endif
				NativeMethods.fdb_transaction_clear(transaction, pKey + key.Offset, key.Count);
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
				NativeMethods.fdb_transaction_clear_range(transaction, pBeginKey + beginKey.Offset, beginKey.Count, pEndKey + endKey.Offset, endKey.Count);
			}
		}

		public static FdbError TransactionAddConflictRange(TransactionHandle transaction, Slice beginKey, Slice endKey, FdbConflictRangeType type)
		{
			fixed (byte* pBeginKey = beginKey.Array)
			fixed (byte* pEndKey = endKey.Array)
			{
#if DEBUG_NATIVE_CALLS
				Debug.WriteLine("fdb_transaction_add_conflict_range(0x" + transaction.Handle.ToString("x") + ", beginKey: '" + FdbKey.Dump(beginKey) + ", endKey: '" + FdbKey.Dump(endKey) + "', " + type.ToString() + ")");
#endif
				return NativeMethods.fdb_transaction_add_conflict_range(transaction, pBeginKey + beginKey.Offset, beginKey.Count, pEndKey + endKey.Offset, endKey.Count, type);
			}
		}

		#endregion

	}

}
