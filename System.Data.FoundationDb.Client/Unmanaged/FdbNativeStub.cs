using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace System.Data.FoundationDb.Client
{
	internal static unsafe class FdbNativeStub
	{
		public const int FDB_API_VERSION = 21;

		private const string DLL_X86 = "fdb_c.dll";
		private const string DLL_X64 = "fdb_c.dll";

		private static readonly UnmanagedLibrary s_lib;

		#region Delegates ...

		private delegate IntPtr FdbGetErrorDelegate(FdbError code);

		private delegate FdbError FdbSelectApiVersionImplDelegate(int runtimeVersion, int headerVersion);

		private delegate int FdbGetMaxApiVersionDelegate();

		private delegate FdbError FdbNetworkSetOptionDelegate(FdbNetworkOption option, byte* value, int value_length);
		private delegate FdbError FdbSetupNetworkDelegate();
		private delegate FdbError FdbRunNetworkDelegate();
		private delegate FdbError FdbStopNetworkDelegate();

		private delegate void FdbFutureDestroy(IntPtr futureHandle);
		private delegate FdbError FdbFutureBlockUntilReady(IntPtr futureHandle);
		private delegate bool FdbFutureIsReady(IntPtr futureHandle);
		private delegate bool FdbFutureIsError(IntPtr futureHandle);
		private delegate FdbError FdbFutureGetErrorDelegate(IntPtr futureHandle, ref byte* description);
		private delegate FdbError FdbFutureSetCallbackDelegate(IntPtr futureHandle, FdbFutureCallback callback, IntPtr callbackParameter);

		private delegate /*Future*/IntPtr FdbCreateClusterDelegate(/*String*/IntPtr clusterFilePath);
		private delegate void FdbClusterDestroyDelegate(/*FDBCluster*/IntPtr cluster);
		private delegate FdbError FdbClusterSetOptionDelegate(/*FDBCluster*/IntPtr cluster, FdbClusterOption option, byte* value, int valueLength);
		private delegate FdbError FdbFutureGetClusterDelegate(/*Future*/IntPtr future, out /*FDBCluster*/IntPtr cluster);

		private delegate void FdbDatabaseDestroyDelegate(/*FDBDatabase*/IntPtr database);
		private delegate /*Future*/IntPtr FdbClusterCreateDatabaseDelegate(IntPtr cluster, /*String*/IntPtr dbName, int dbNameLength);
		private delegate FdbError FdbFutureGetDatabaseDelegate(/*Future*/IntPtr future, out /*FDBDatabase*/IntPtr database);

		private delegate FdbError FdbDatabaseCreateTransactionDelegate(/*FDBDatabase*/IntPtr database, out IntPtr transaction);
		private delegate void FdbTransactionSetDelegate(IntPtr database, byte* keyName, int keyNameLength, byte* value, int valueLength);
		private delegate /*Future*/IntPtr FdbTransactionCommitDelegate(IntPtr transaction);

		#endregion

		#region Stubs...

		private static readonly FdbGetErrorDelegate s_stub_fdbGetError;
		private static readonly FdbSelectApiVersionImplDelegate s_stub_fdbSelectApiVersionImpl;
		private static readonly FdbGetMaxApiVersionDelegate s_stub_fdbGetMaxApiVersion;

		private static readonly FdbNetworkSetOptionDelegate s_stub_fdbNetworkSetOption;
		private static readonly FdbSetupNetworkDelegate s_stub_fdbSetupNetwork;
		private static readonly FdbRunNetworkDelegate s_stud_fdbRunNetwork;
		private static readonly FdbStopNetworkDelegate s_stub_fdbStopNetwork;

		private static readonly FdbFutureDestroy s_stub_fdbFutureDestroy;
		private static readonly FdbFutureIsError s_stub_fdbFutureIsError;
		private static readonly FdbFutureIsReady s_stub_fdbFutureIsReady;
		private static readonly FdbFutureBlockUntilReady s_stub_fdbFutureBlockUntilReady;
		private static readonly FdbFutureGetErrorDelegate s_stub_fdbFutureGetError;
		private static readonly FdbFutureSetCallbackDelegate s_stub_fdbFutureSetCallback;

		private static readonly FdbCreateClusterDelegate s_stub_fdbCreateCluster;
		private static readonly FdbClusterDestroyDelegate s_stub_fdbClusterDestroy;
		private static readonly FdbClusterSetOptionDelegate s_stub_fdbClusterSetOption;
		private static readonly FdbFutureGetClusterDelegate s_stub_fdbFutureGetCluster;

		private static readonly FdbDatabaseDestroyDelegate s_stub_fdbDatabaseDestroy;
		private static readonly FdbClusterCreateDatabaseDelegate s_stub_fdbClusterCreateDatabase;
		private static readonly FdbFutureGetDatabaseDelegate s_stub_fdbFutureGetDatabase;

		private static readonly FdbDatabaseCreateTransactionDelegate s_stub_fdbDatabaseCreateTransaction;
		private static readonly FdbTransactionSetDelegate s_stub_fdbTransactionSet;
		private static readonly FdbTransactionCommitDelegate s_stub_fdbTransactionCommit;

		private static readonly Exception s_error;

		#endregion

		static FdbNativeStub()
		{
			try
			{
				s_lib = UnmanagedLibrary.LoadLibrary(
					Path.Combine(FdbCore.NativeLibPath, DLL_X86),
					Path.Combine(FdbCore.NativeLibPath, DLL_X64)
				);

				s_lib.Bind(ref s_stub_fdbGetError, "fdb_get_error");
				s_lib.Bind(ref s_stub_fdbSelectApiVersionImpl, "fdb_select_api_version_impl");
				s_lib.Bind(ref s_stub_fdbGetMaxApiVersion, "fdb_get_max_api_version");

				s_lib.Bind(ref s_stub_fdbNetworkSetOption, "fdb_network_set_option");
				s_lib.Bind(ref s_stub_fdbSetupNetwork, "fdb_setup_network");
				s_lib.Bind(ref s_stud_fdbRunNetwork, "fdb_run_network");
				s_lib.Bind(ref s_stub_fdbStopNetwork, "fdb_stop_network");

				s_lib.Bind(ref s_stub_fdbFutureDestroy, "fdb_future_destroy");
				s_lib.Bind(ref s_stub_fdbFutureIsError, "fdb_future_is_error");
				s_lib.Bind(ref s_stub_fdbFutureIsReady, "fdb_future_is_ready");
				s_lib.Bind(ref s_stub_fdbFutureBlockUntilReady, "fdb_future_block_until_ready");
				s_lib.Bind(ref s_stub_fdbFutureGetError, "fdb_future_get_error");
				s_lib.Bind(ref s_stub_fdbFutureSetCallback, "fdb_future_set_callback");

				s_lib.Bind(ref s_stub_fdbCreateCluster, "fdb_create_cluster");
				s_lib.Bind(ref s_stub_fdbClusterDestroy, "fdb_cluster_destroy");
				s_lib.Bind(ref s_stub_fdbClusterSetOption, "fdb_cluster_set_option");
				s_lib.Bind(ref s_stub_fdbFutureGetCluster, "fdb_future_get_cluster");

				s_lib.Bind(ref s_stub_fdbClusterCreateDatabase, "fdb_cluster_create_database");
				s_lib.Bind(ref s_stub_fdbDatabaseDestroy, "fdb_database_destroy");
				s_lib.Bind(ref s_stub_fdbFutureGetDatabase, "fdb_future_get_database");

				s_lib.Bind(ref s_stub_fdbDatabaseCreateTransaction, "fdb_database_create_transaction");
				s_lib.Bind(ref s_stub_fdbTransactionSet, "fdb_transaction_set");
				s_lib.Bind(ref s_stub_fdbTransactionCommit, "fdb_transaction_commit");

			}
			catch (Exception e)
			{
				if (s_lib != null) s_lib.Dispose();
				s_lib = null;
				s_error = e;
			}
		}

		public static bool IsLoaded
		{
			get { return s_error == null && s_lib != null; }
		}

		private static void EnsureLibraryIsLoaded()
		{
			// should be inlined
			if (s_error != null || s_lib == null) FailLibraryDidNotLoad();
		}

		private static void FailLibraryDidNotLoad()
		{
			throw new InvalidOperationException("An error occured while loading native FoundationDB library", s_error);
		}

		#region API Basics..

		public static string GetError(FdbError code)
		{
			EnsureLibraryIsLoaded();
			var ptr = s_stub_fdbGetError(code);
			return ptr == IntPtr.Zero ? null : Marshal.PtrToStringAnsi(ptr);
		}

		public static FdbError SelectApiVersionImpl(int runtimeVersion, int headerVersion)
		{
			EnsureLibraryIsLoaded();
			return s_stub_fdbSelectApiVersionImpl(runtimeVersion, headerVersion);
		}

		public static FdbError SelectApiVersion(int version)
		{
			return SelectApiVersionImpl(version, FDB_API_VERSION);
		}

		public static int GetMaxApiVersion()
		{
			EnsureLibraryIsLoaded();
			return s_stub_fdbGetMaxApiVersion();
		}

		#endregion

		#region Futures...

		public static void FutureDestroy(IntPtr futureHandle)
		{
			EnsureLibraryIsLoaded();
			s_stub_fdbFutureDestroy(futureHandle);
		}

		public static bool FutureIsError(IntPtr futureHandle)
		{
			EnsureLibraryIsLoaded();
			return s_stub_fdbFutureIsError(futureHandle);
		}

		public static FdbError FutureGetError(IntPtr futureHandle)
		{
			EnsureLibraryIsLoaded();
			byte* _ = null;
			return s_stub_fdbFutureGetError(futureHandle, ref _);
		}

		public static bool FutureIsReady(IntPtr futureHandle)
		{
			EnsureLibraryIsLoaded();
			return s_stub_fdbFutureIsError(futureHandle);
		}

		public static FdbError FutureSetCallback(IntPtr futureHandle, FdbFutureCallback callback, IntPtr callbackParameter)
		{
			EnsureLibraryIsLoaded();
			return s_stub_fdbFutureSetCallback(futureHandle, callback, callbackParameter);
		}

		#endregion

		#region Network...

		public static FdbError NetworkSetOption(FdbNetworkOption option, byte* value, int valueLength)
		{
			EnsureLibraryIsLoaded();
			return s_stub_fdbNetworkSetOption(option, value, valueLength);
		}

		public static FdbError SetupNetwork()
		{
			return s_stub_fdbSetupNetwork();
		}

		public static FdbError RunNetwork()
		{
			return s_stud_fdbRunNetwork();
		}

		public static FdbError StopNetwork()
		{
			return s_stub_fdbStopNetwork();
		}

		#endregion

		#region Clusters...

		public static FutureHandle CreateCluster(string path)
		{
			EnsureLibraryIsLoaded();

			using (var ptr = SafeAnsiStringHandle.FromString(path))
			{
				var future = new FutureHandle();
				var handle = s_stub_fdbCreateCluster(ptr.DangerousGetHandle());
				future.TrySetHandle(handle);
				return future;
			}
		}

		public static void ClusterDestroy(IntPtr handle)
		{
			EnsureLibraryIsLoaded();
			RuntimeHelpers.PrepareConstrainedRegions();
			try { }
			finally
			{
				s_stub_fdbClusterDestroy(handle);
			}
		}

		public static FdbError FutureGetCluster(FutureHandle future, out ClusterHandle cluster)
		{
			EnsureLibraryIsLoaded();
			cluster = new ClusterHandle();
			FdbError err;

			RuntimeHelpers.PrepareConstrainedRegions();
			try { }
			finally
			{
				IntPtr handle;
				err = s_stub_fdbFutureGetCluster(future.Handle, out handle);
				//TODO: check is err == Success ?
				cluster.TrySetHandle(handle);
			}
			return err;
		}

		#endregion

		#region Databases...

		public static FdbError FutureGetDatabase(FutureHandle future, out DatabaseHandle database)
		{
			EnsureLibraryIsLoaded();

			database = new DatabaseHandle();
			FdbError err;

			RuntimeHelpers.PrepareConstrainedRegions();
			try { }
			finally
			{
				IntPtr handle;
				err = s_stub_fdbFutureGetDatabase(future.Handle, out handle);
				//TODO: check is err == Success ?
				database.TrySetHandle(handle);
			}
			return err;
		}

		public static void DatabaseDestroy(IntPtr handle)
		{
			EnsureLibraryIsLoaded();

			RuntimeHelpers.PrepareConstrainedRegions();
			try { }
			finally
			{
				s_stub_fdbDatabaseDestroy(handle);
			}
		}

		public static FutureHandle CreateClusterDatabase(ClusterHandle cluster, string name)
		{
			EnsureLibraryIsLoaded();

			int len = name == null ? 0 : name.Length;

			using (var ptr = SafeAnsiStringHandle.FromString(name))
			{
				var future = new FutureHandle();

				RuntimeHelpers.PrepareConstrainedRegions();
				try { }
				finally
				{
					var handle = s_stub_fdbClusterCreateDatabase(cluster.Handle, ptr.DangerousGetHandle(), len);
					future.TrySetHandle(handle);
				}
				return future;
			}
		}

		#endregion

		#region Transactions...

		public static void TransactionDestroy(IntPtr handle)
		{
			EnsureLibraryIsLoaded();

			RuntimeHelpers.PrepareConstrainedRegions();
			try { }
			finally
			{
				s_stub_fdbDatabaseDestroy(handle);
			}
		}

		public static FdbError DatabaseCreateTransaction(DatabaseHandle database, out TransactionHandle transaction)
		{
			EnsureLibraryIsLoaded();
			transaction = new TransactionHandle();
			FdbError err;

			RuntimeHelpers.PrepareConstrainedRegions();
			try { }
			finally
			{
				IntPtr handle;
				err = s_stub_fdbDatabaseCreateTransaction(database.Handle, out handle);
				transaction.TrySetHandle(handle);
			}
			return err;

		}

		public static void TransactionSet(TransactionHandle transaction, byte[] key, byte[] value)
		{
			EnsureLibraryIsLoaded();
			//TODO: nullcheck!
			fixed (byte* pKey = key)
			fixed (byte* pValue = value)
			{
				s_stub_fdbTransactionSet(transaction.Handle, pKey, key.Length, pValue, value.Length);
			}
		}

		public static FutureHandle TransactionCommit(TransactionHandle transaction)
		{
			EnsureLibraryIsLoaded();
			var future = new FutureHandle();

			RuntimeHelpers.PrepareConstrainedRegions();
			try { }
			finally
			{
				var handle = s_stub_fdbTransactionCommit(transaction.Handle);
				future.TrySetHandle(handle);
			}
			return future;
		}

		#endregion

	}

}
