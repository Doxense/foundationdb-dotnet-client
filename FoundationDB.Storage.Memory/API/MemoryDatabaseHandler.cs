﻿#region Copyright (c) 2013-2014, Doxense SAS. All rights reserved.
// See License.MD for license information
#endregion

#undef FULLDEBUG

namespace FoundationDB.Storage.Memory.API
{
	using FoundationDB.Client;
	using FoundationDB.Client.Core;
	using FoundationDB.Client.Utils;
	using FoundationDB.Layers.Tuples;
	using FoundationDB.Storage.Memory.Core;
	using FoundationDB.Storage.Memory.IO;
	using FoundationDB.Storage.Memory.Utils;
	using System;
	using System.Collections.Concurrent;
	using System.Collections.Generic;
	using System.Diagnostics;
	using System.Diagnostics.Contracts;
	using System.Text;
	using System.Threading;
	using System.Threading.Tasks;

	internal class MemoryDatabaseHandler : IFdbDatabaseHandler, IDisposable
	{
		internal const uint MAX_KEY_SIZE = 10 * 1000;
		internal const uint MAX_VALUE_SIZE = 100 * 1000;

		internal const uint KEYHEAP_MIN_PAGESIZE = 64 * 1024;
		internal const uint KEYHEAP_MAX_PAGESIZE = 4 * 1024 * 1024;
		internal const uint VALUEHEAP_MIN_PAGESIZE = 256 * 1024;
		internal const uint VALUEHEAP_MAX_PAGESIZE = 16 * 1024 * 1024;

		internal void PopulateSystemKeys()
		{
			// we need to create the System keyspace, under \xFF

			// cheap way to generate machine & datacenter ids
			var databaseId = new Uuid128(m_uid).ToSlice();
			var machineId = Slice.FromFixed64(Environment.MachineName.GetHashCode()) + databaseId[0, 8];
			var datacenterId = Slice.FromFixed64(Environment.MachineName.GetHashCode()) + databaseId[8, 16];
			var keyServerBlob = Slice.FromFixed16(1) + Slice.FromFixed32(0xA22000) + Slice.FromFixed16(0xFDB) + Slice.FromFixed32(1) + databaseId + Slice.FromFixed32(0);
			var one = Slice.FromAscii("1");

			var systemKeys = new Dictionary<Slice, Slice>()
			{
				{ Fdb.System.BackupDataFormat, one },
				{ Fdb.System.ConfigKey("initialized"), one },
				{ Fdb.System.ConfigKey("storage_engine"), one },	// ~= memory
				{ Fdb.System.ConfigKey("storage_replicas"), one },	// single replica
				{ Fdb.System.Coordinators, Slice.FromString("local:" + m_uid.ToString("N") + "@memory") },
				{ Fdb.System.GlobalsKey("lastEpochEnd"), Slice.FromFixed64(0) },
				{ Fdb.System.InitId, Slice.FromAscii(Guid.NewGuid().ToString("N")) },

				{ Fdb.System.KeyServers, keyServerBlob },
				{ Fdb.System.KeyServers + Fdb.System.KeyServers, keyServerBlob },
				{ Fdb.System.KeyServers + Fdb.System.MaxValue, Slice.Empty },

				{ Fdb.System.ServerKeys + databaseId + Slice.FromAscii("/"), one },
				{ Fdb.System.ServerKeys + databaseId + Slice.FromAscii("/\xFF\xFF"), Slice.Empty },

				//TODO: serverList ?

				{ Fdb.System.WorkersKey("memory", "datacenter"), datacenterId },
				{ Fdb.System.WorkersKey("memory", "machine"), machineId },
				{ Fdb.System.WorkersKey("memory", "mclass"), Slice.FromAscii("unset") },
			};

			BulkLoadAsync(systemKeys, false, false, CancellationToken.None).GetAwaiter().GetResult();
		}

		#region Private Members...

		/// <summary>Set to true when the current db instance gets disposed.</summary>
		private volatile bool m_disposed;

		/// <summary>Current version of the database</summary>
		private long m_currentVersion;
		/// <summary>Oldest legal read version of the database</summary>
		private long m_oldestVersion;

		/// <summary>Unique number for this database</summary>
		private Guid m_uid;

		//TODO: replace this with an Async lock ?
		private readonly ReaderWriterLockSlim m_dataLock = new ReaderWriterLockSlim();
		private readonly object m_heapLock = new object();

		private readonly KeyHeap m_keys = new KeyHeap();
		private readonly ValueHeap m_values = new ValueHeap();

		private ColaStore<IntPtr> m_data = new ColaStore<IntPtr>(0, new NativeKeyComparer());
		private long m_estimatedSize;

		/// <summary>List of all active transaction windows</summary>
		private LinkedList<TransactionWindow> m_transactionWindows = new LinkedList<TransactionWindow>();
		/// <summary>Last transaction window</summary>
		private TransactionWindow m_currentWindow;

		// note: all scratch buffers should have a size larger than 80KB, so that they to the LOH
		/// <summary>Pool of builders uses by read operations from transactions (concurrent)</summary>
		private UnmanagedSliceBuilderPool m_scratchPool = new UnmanagedSliceBuilderPool(128 * 1024, 64);
		/// <summary>Scratch use to format keys when committing (single writer)</summary>
		private UnmanagedSliceBuilder m_scratchKey = new UnmanagedSliceBuilder(128 * 1024);
		/// <summary>Scratch use to hold values when committing (single writer)</summary>
		private UnmanagedSliceBuilder m_scratchValue = new UnmanagedSliceBuilder(128 * 1024);

		#endregion

		public MemoryDatabaseHandler(Guid uid)
		{
			m_uid = uid;
		}

		public Guid Id { get { return m_uid; } }

		public bool IsInvalid { get { return false; } }

		public bool IsClosed { get { return m_disposed; } }

		public void SetOption(FdbDatabaseOption option, Slice data)
		{
			throw new NotImplementedException();
		}

		internal long GetCurrentVersion()
		{
			m_dataLock.EnterReadLock();
			try
			{
				return Volatile.Read(ref m_currentVersion);
			}
			finally
			{
				m_dataLock.ExitReadLock();
			}
		}

		/// <summary>Format a user key using a slice buffer for temporary storage</summary>
		/// <remarks>The buffer is cleared prior to usage!</remarks>
		internal unsafe static USlice PackUserKey(UnmanagedSliceBuilder buffer, Slice userKey)
		{
			Contract.Requires(buffer != null && userKey.Array != null && userKey.Count >= 0 && userKey.Offset >= 0);
			Contract.Requires(userKey.Count <= MemoryDatabaseHandler.MAX_KEY_SIZE);

			buffer.Clear();
			uint keySize = (uint)userKey.Count;
			uint size = Key.SizeOf + keySize;
			var tmp = buffer.Allocate(size);
			var key = (Key*)tmp.Data;
			key->Size = (ushort)keySize;
			key->HashCode = UnmanagedHelpers.ComputeHashCode(ref userKey);
			key->Header = ((ushort)EntryType.Key) << Entry.TYPE_SHIFT;
			key->Values = null;

			if (keySize > 0) UnmanagedHelpers.CopyUnsafe(&(key->Data), userKey);
			return tmp;
		}

		/// <summary>Format a user key</summary>
		internal unsafe static USlice PackUserKey(UnmanagedSliceBuilder buffer, USlice userKey)
		{
			Contract.Requires(buffer != null && userKey.Data != null);
			Contract.Requires(userKey.Count <= MemoryDatabaseHandler.MAX_KEY_SIZE);

			buffer.Clear();
			uint keySize = userKey.Count;
			var size = Key.SizeOf + keySize;
			var tmp = buffer.Allocate(size);
			var key = (Key*)tmp.Data;
			key->Size = (ushort)keySize;
			key->HashCode = UnmanagedHelpers.ComputeHashCode(ref userKey);
			key->Header = ((ushort)EntryType.Key) << Entry.TYPE_SHIFT;
			key->Values = null;

			if (keySize > 0) UnmanagedHelpers.CopyUnsafe(&(key->Data), userKey);
			return tmp;
		}

		private TimeSpan m_transactionHalfLife = TimeSpan.FromSeconds(2.5);
		private TimeSpan m_windowMaxDuration = TimeSpan.FromSeconds(5);
		private int m_windowMaxWrites = 1000;

		private TransactionWindow GetActiveTransactionWindow_NeedsLocking(ulong sequence)
		{
			var window = m_currentWindow;
			var now = DateTime.UtcNow;

			// open a new window if the previous one is already closed, or is too old
			if (window != null)
			{ // is it still active ?
				if (window.Closed || now.Subtract(window.StartedUtc) >= m_transactionHalfLife || window.CommitCount >= m_windowMaxWrites)
				{
					Log("Recycling previous window " + window);
					window = null;
				}
			}

			if (window == null)
			{ // need to start a new window
				window = new TransactionWindow(now, sequence);
				m_currentWindow = window;
				m_transactionWindows.AddFirst(window);
			}

			// check the oldest transaction window
			PurgeOldTransactionWindows(now);

			return window;
		}

		private void PurgeOldTransactionWindows(DateTime utcNow)
		{
			var stop = m_currentWindow;
			var node = m_transactionWindows.Last;
			TransactionWindow window;

			while ((node != null && (window = node.Value) != null && window != stop))
			{
				if (!window.Closed && utcNow.Subtract(window.StartedUtc) <= m_windowMaxDuration)
				{
					break;
				}
				Log("Purging old transaction window " + window.ToString());

				window.Close();
				var tmp = node.Previous;
				m_transactionWindows.RemoveLast();
				node = tmp;
			}
		}

		/// <summary>Commits the changes made by a transaction to the database.</summary>
		/// <param name="trans"></param>
		/// <param name="readVersion"></param>
		/// <param name="readConflicts"></param>
		/// <param name="writeConflicts"></param>
		/// <param name="clearRanges"></param>
		/// <param name="writes"></param>
		/// <returns></returns>
		/// <remarks>This method is not thread safe and must be called from the writer thread.</remarks>
		internal unsafe long CommitTransaction(MemoryTransactionHandler trans, long readVersion, ColaRangeSet<Slice> readConflicts, ColaRangeSet<Slice> writeConflicts, ColaRangeSet<Slice> clearRanges, ColaOrderedDictionary<Slice, MemoryTransactionHandler.WriteCommand[]> writes)
		{
			if (trans == null) throw new ArgumentNullException("trans");
			if (m_disposed) ThrowDisposed();

			// version at which the transaction was created (and all reads performed)
			ulong readSequence = (ulong)readVersion;
			// commit version created by this transaction (if it writes something)
			ulong committedSequence = 0;

			Log("Comitting transaction created at readVersion " + readVersion + " ...");

			bool hasReadConflictRanges = readConflicts != null && readConflicts.Count > 0;
			bool hasWriteConflictRanges = writeConflicts != null && writeConflicts.Count > 0;
			bool hasClears = clearRanges != null && clearRanges.Count > 0;
			bool hasWrites = writes != null && writes.Count > 0;

			bool isReadOnlyTransaction = !hasClears && !hasWrites && !hasWriteConflictRanges;

			m_dataLock.EnterUpgradeableReadLock();
			try
			{
				TransactionWindow window;

				if (!isReadOnlyTransaction)
				{
					committedSequence = (ulong)Interlocked.Increment(ref m_currentVersion);
					window = GetActiveTransactionWindow_NeedsLocking(committedSequence);
					Contract.Assert(window != null);
					Log("... will create version " + committedSequence + " in window " + window.ToString());
				}
				else
				{
					Log("... which is read-only");
					window = null;
				}

				#region Read Conflict Check

				if (hasReadConflictRanges)
				{

					var current = m_transactionWindows.First;
					while (current != null && current.Value.LastVersion >= readSequence)
					{
						if (current.Value.Conflicts(readConflicts, readSequence))
						{
							// the transaction has conflicting reads
							throw new FdbException(FdbError.NotCommitted);
						}
						current = current.Next;
					}
				}

				#endregion

				if (!isReadOnlyTransaction)
				{
					#region Clear Ranges...

					if (hasClears)
					{
						foreach (var clear in clearRanges)
						{
							//TODO!
							throw new NotImplementedException("ClearRange not yet implemented. Sorry!");
						}
					}

					#endregion

					#region Writes...

					if (hasWrites)
					{
						IntPtr singleInsert = IntPtr.Zero;
						List<IntPtr> pendingInserts = null;

						foreach (var write in writes)
						{
							Key* key;
							Value* value;

							// apply all the transformations at once on the key, add a new version if required

							// Only two allowed cases: 
							// - a single SET operation that create or update the value
							// - one or more ATOMIC operations that create or mutate the value

							// For both case, we will do a lookup in the db to get the previous value and location

							// create the lookup key
							USlice lookupKey = PackUserKey(m_scratchKey, write.Key);

							IntPtr previous;
							int offset, level = m_data.Find(lookupKey.GetPointer(), out offset, out previous);
							key = level >= 0 ? (Key*)previous : null;
							Contract.Assert((level < 0 && key == null) || (level >= 0 && offset >= 0 && key != null));

							bool valueMutated = false;
							bool currentIsDeleted = false;
							bool hasTmpData = false;

							foreach (var op in write.Value)
							{
								if (op.Type == MemoryTransactionHandler.Operation.Nop) continue;

								if (op.Type == MemoryTransactionHandler.Operation.Set)
								{
									m_scratchValue.Set(op.Value);
									hasTmpData = true;
									valueMutated = true;
									continue;
								}

								// apply the atomic operation to the previous value
								if (!hasTmpData)
								{
									m_scratchValue.Clear();
									if (key != null)
									{ // grab the current value of this key

										Value* p = key->Values;
										if ((p->Header & Value.FLAGS_DELETION) == 0)
										{
											m_scratchValue.Append(&(p->Data), p->Size);
										}
										else
										{
											m_scratchValue.Clear();
											currentIsDeleted = true;
										}
									}
									hasTmpData = true;
								}

								switch (op.Type)
								{
									case MemoryTransactionHandler.Operation.AtomicAdd:
									{
										op.ApplyAddTo(m_scratchValue);
										valueMutated = true;
										break;
									}
									case MemoryTransactionHandler.Operation.AtomicBitAnd:
									{
										op.ApplyBitAndTo(m_scratchValue);
										valueMutated = true;
										break;
									}
									case MemoryTransactionHandler.Operation.AtomicBitOr:
									{
										op.ApplyBitOrTo(m_scratchValue);
										valueMutated = true;
										break;
									}
									case MemoryTransactionHandler.Operation.AtomicBitXor:
									{
										op.ApplyBitXorTo(m_scratchValue);
										valueMutated = true;
										break;
									}
									default:
									{
										throw new InvalidOperationException();
									}
								}
							}

							if (valueMutated)
							{ // we have a new version for this key

								lock (m_heapLock)
								{
									value = m_values.Allocate(m_scratchValue.Count, committedSequence, key != null ? key->Values : null, null);
								}
								Contract.Assert(value != null);
								m_scratchValue.CopyTo(&(value->Data));
								Interlocked.Add(ref m_estimatedSize, value->Size);

								if (key != null)
								{ // mutate the previous version for this key
									var prev = key->Values;
									value->Parent = key;
									key->Values = value;
									prev->Header |= Value.FLAGS_MUTATED;
									prev->Parent = value;

									// make sure no thread seees an inconsitent view of the key
									Interlocked.MemoryBarrier();
								}
								else
								{ // add this key to the data store

									// we can reuse the lookup key (which is only missing the correct flags and pointers to the values)
									lock (m_heapLock)
									{
										key = m_keys.Append(lookupKey);
									}
									key->Values = value;
									value->Parent = key;
									Contract.Assert(key->Size == write.Key.Count);
									Interlocked.Add(ref m_estimatedSize, key->Size);

									// make sure no thread seees an inconsitent view of the key
									Interlocked.MemoryBarrier();

									if (pendingInserts != null)
									{
										pendingInserts.Add(new IntPtr(key));
									}
									else if (singleInsert != IntPtr.Zero)
									{
										pendingInserts = new List<IntPtr>();
										pendingInserts.Add(singleInsert);
										pendingInserts.Add(new IntPtr(key));
										singleInsert = IntPtr.Zero;
									}
									else
									{
										singleInsert = new IntPtr(key);
									}
								}

							}
						}

						if (singleInsert != IntPtr.Zero || pendingInserts != null)
						{
							// insert the new key into the data store
							m_dataLock.EnterWriteLock();
							try
							{
								if (singleInsert != IntPtr.Zero)
								{
									m_data.Insert(singleInsert);
								}
								else
								{
									m_data.InsertItems(pendingInserts, ordered: true);
								}
							}
							finally
							{
								m_dataLock.ExitWriteLock();
							}
						}
					}

					#endregion

					#region Merge Write Conflicts...

					if (hasWriteConflictRanges)
					{
						window.MergeWrites(writeConflicts, committedSequence);
					}

					#endregion
				}
			}
			finally
			{
				m_dataLock.ExitUpgradeableReadLock();
			}

			var version = isReadOnlyTransaction ? -1L : (long)committedSequence;

			return version;
		}

		internal unsafe Task BulkLoadAsync(ICollection<KeyValuePair<Slice, Slice>> data, bool ordered, bool append, CancellationToken cancellationToken)
		{
			Contract.Requires(data != null);

			int count = data.Count;

			// Since we can "only" create a maximum of 28 levels, there is a maximum limit or 2^28 - 1 items that can be loaded in the database (about 268 millions)
			if (count >= 1 << 28) throw new InvalidOperationException("Data set is too large. Cannot insert more than 2^28 - 1 items in the memory database");

			// clear everything, and import the specified data

			m_dataLock.EnterWriteLock();
			try
			{

				// the fastest way to insert data, is to insert vectors that are a power of 2
				int min = ColaStore.LowestBit(count);
				int max = ColaStore.HighestBit(count);
				Contract.Assert(min <= max && max <= 28);
				if (append)
				{ // the appended layers have to be currently free
					for (int level = min; level <= max; level++)
					{
						if (!m_data.IsFree(level)) throw new InvalidOperationException(String.Format("Cannot bulk load level {0} because it is already in use", level));
					}
				}
				else
				{ // start from scratch
					m_data.Clear();
					m_estimatedSize = 0;
					//TODO: clear the key and value heaps !
					//TODO: clear the transaction windows !
					//TODO: kill all pending transactions !
				}

				m_data.EnsureCapacity(count);

				ulong sequence = (ulong)Interlocked.Increment(ref m_currentVersion);

				using (var iter = data.GetEnumerator())
				using (var writer = new LevelWriter(1 << max, m_keys, m_values))
				{
					for (int level = max; level >= min && !cancellationToken.IsCancellationRequested; level--)
					{
						if (ColaStore.IsFree(level, count)) continue;

						//TODO: consider pre-sorting the items before inserting them in the heap using m_comparer (maybe faster than doing the same with the key comparer?)

						// take of batch of values
						writer.Reset();
						int batch = 1 << level;
						while(batch-- > 0)
						{
							if (!iter.MoveNext())
							{
								throw new InvalidOperationException("Iterator stopped before reaching the expected number of items");
							}
							writer.Add(sequence, iter.Current);
						}

						// and insert it (should fit nicely in a level without cascading)
						m_data.InsertItems(writer.Data, ordered);
					}
				}
			}
			finally
			{
				m_dataLock.ExitWriteLock();
			}

			if (cancellationToken.IsCancellationRequested) return TaskHelpers.FromCancellation<object>(cancellationToken);
			return TaskHelpers.CompletedTask;
		}

		private static readonly Task<Slice> NilResult = Task.FromResult<Slice>(Slice.Nil);
		private static readonly Task<Slice> EmptyResult = Task.FromResult<Slice>(Slice.Empty);
		private static readonly Task<Slice> MaxResult = Task.FromResult<Slice>(Slice.FromByte(255));

		private void EnsureReadVersionNotInTheFuture_NeedsLocking(ulong readVersion)
		{
			if ((ulong)Volatile.Read(ref m_currentVersion) < readVersion)
			{ // a read for a future version? This is most probably a bug !
#if DEBUG
				if (Debugger.IsAttached) Debugger.Break();
#endif
				throw new FdbException(FdbError.FutureVersion);
			}
		}

		[Conditional("FULLDEBUG")]
		private unsafe static void DumpKey(string label, IntPtr userKey)
		{
			var sb = new StringBuilder("(*) " + (label ?? "key") + " = ");
			if (userKey == IntPtr.Zero)
			{
				sb.Append("<NIL>");
			}
			else
			{
				sb.Append(userKey).Append(" => ");

				Key* key = (Key*)userKey;
				Contract.Assert(key != null);

				sb.Append('\'').Append(FdbKey.Dump(Key.GetData(key).ToSlice())).Append('\'');

				Value* value = key->Values;
				if (value != null)
				{
					sb.Append(" => [").Append(value->Sequence).Append("] ");
					if ((value->Header & Value.FLAGS_DELETION) != 0)
					{
						sb.Append("DELETED");
					}
					else if (value->Size == 0)
					{
						sb.Append("<empty>");
					}
					else
					{
						sb.Append(Value.GetData(value).ToSlice().ToAsciiOrHexaString());
					}
				}
			}
			Trace.WriteLine(sb.ToString());
		}

		private unsafe bool TryGetValueAtVersion(USlice lookupKey, ulong sequence, out USlice result)
		{
			result = default(USlice);

			IntPtr existing;
			int _, level = m_data.Find(lookupKey.GetPointer(), out _, out existing);
			if (level < 0)
			{
				return false;
			}

			Key* key = (Key*)existing;
			//TODO: aserts!

			// walk the chain of version until we find one that existed at the request version
			Value* current = key->Values;
			while (current != null)
			{
				if (current->Sequence <= sequence)
				{ // found it
					break;
				}
				current = current->Previous;
			}

			if (current == null || (current->Header & Value.FLAGS_DELETION) != 0)
			{ // this key was created after our read version, or this version is a deletion marker
				return false;
			}

			if (current->Size > 0)
			{ // the value is not empty
				result = Value.GetData(current);
			}
			return true;

		}

		/// <summary>Read the value of one or more keys, at a specific database version</summary>
		/// <param name="userKeys">List of keys to read (MUST be ordered)</param>
		/// <param name="readVersion">Version of the read</param>
		/// <returns>Array of results</returns>
		internal unsafe Slice[] GetValuesAtVersion(Slice[] userKeys, long readVersion)
		{
			if (m_disposed) ThrowDisposed();
			if (userKeys == null) throw new ArgumentNullException("userKeys");

			var results = new Slice[userKeys.Length];

			if (userKeys.Length > 0)
			{
				m_dataLock.EnterReadLock();
				try
				{
					ulong sequence = (ulong)readVersion;
					EnsureReadVersionNotInTheFuture_NeedsLocking(sequence);

					var buffer = new SliceBuffer();

					using (var scratch = m_scratchPool.Use())
					{
						var builder = scratch.Builder;

						for (int i = 0; i < userKeys.Length; i++)
						{
							// create a lookup key
							var lookupKey = PackUserKey(builder, userKeys[i]);

							USlice value;
							if (!TryGetValueAtVersion(lookupKey, sequence, out value))
							{ // this key does not exist, or was deleted at that time
								results[i] = default(Slice);
							}
							else if (value.Count == 0)
							{ // the value is the empty slice
								results[i] = Slice.Empty;
							}
							else
							{ // move this value to the slice buffer
								var data = buffer.Allocate(checked((int)value.Count));
								Contract.Assert(data.Array != null && data.Offset >= 0 && data.Count == (int)value.Count);
								UnmanagedHelpers.CopyUnsafe(data, value.Data, value.Count);
								results[i] = data;
							}
						}
					}
				}
				finally
				{
					m_dataLock.ExitReadLock();
				}
			}
			return results;
		}

		/// <summary>Walk the value chain, to return the value of a key that was the latest at a specific read version</summary>
		/// <param name="userKey">User key to resolve</param>
		/// <param name="sequence">Sequence number</param>
		/// <returns>Value of the key at that time, or null if the key was either deleted or not yet created.</returns>
		internal static unsafe Value* ResolveValueAtVersion(IntPtr userKey, ulong sequence)
		{
			if (userKey == IntPtr.Zero) return null;

			Key* key = (Key*)userKey;
			Contract.Assert((key->Header & Entry.FLAGS_DISPOSED) == 0, "Attempted to read value from a disposed key");
			Contract.Assert(key->Size <= MemoryDatabaseHandler.MAX_KEY_SIZE, "Attempted to read value from a key that is too large");

			Value* current = key->Values;
			while(current != null && current->Sequence > sequence)
			{
				current = current->Previous;
			}

			if (current == null || (current->Header & Value.FLAGS_DELETION) != 0)
			{
				return null;
			}

			Contract.Ensures((current->Header & Entry.FLAGS_DISPOSED) == 0 && current->Sequence <= sequence);
			return current;
		}

		private unsafe ColaStore.Iterator<IntPtr> ResolveCursor(USlice lookupKey, bool orEqual, int offset, ulong sequence)
		{
			var iterator = m_data.GetIterator();

			DumpKey(orEqual ? "seek(<=)" : "seek(<)", lookupKey.GetPointer());

			// seek to the closest key
			if (!iterator.Seek(lookupKey.GetPointer(), orEqual))
			{ // we are before the first key in the database!
				if (offset <= 0)
				{
					iterator.SeekBeforeFirst();
					return iterator;
				}
				else
				{
					iterator.SeekFirst();
					--offset;
				}
			}

			bool forward = offset >= 0;

			while (iterator.Current != IntPtr.Zero)
			{
				DumpKey("offset " + offset, iterator.Current);
				Value* value = ResolveValueAtVersion(iterator.Current, sequence);
				//Trace.WriteLine("[*] " + (long)value);
				if (value != null)
				{
					if (offset == 0)
					{ // we found a key that was alive, and at the correct offset
						break;
					}
					if (forward)
					{
						--offset;
					}
					else
					{
						++offset;
					}
				}

				if (forward)
				{ // move forward

					//Trace.WriteLine("> next!");
					if (!iterator.Next())
					{
						//Trace.WriteLine("  > EOF");
						break;
					}
				}
				else
				{ // move backward
					//Trace.WriteLine("> prev!");
					if (!iterator.Previous())
					{
						//Trace.WriteLine("  > EOF");
						break;
					}
				}
			}

			return iterator;
		}

		internal unsafe Task<Slice[]> GetKeysAtVersion(FdbKeySelector[] selectors, long readVersion)
		{
			if (m_disposed) ThrowDisposed();
			if (selectors == null) throw new ArgumentNullException("selectors");

			var results = new Slice[selectors.Length];

			m_dataLock.EnterReadLock();
			try
			{
				ulong sequence = (ulong)readVersion;
				EnsureReadVersionNotInTheFuture_NeedsLocking(sequence);

				// TODO: convert all selectors to a FirstGreaterThan ?
				var buffer = new SliceBuffer();

				using (var scratch = m_scratchPool.Use())
				{
					var builder = scratch.Builder;

					for (int i = 0; i < selectors.Length; i++)
					{
						var selector = selectors[i];

						var lookupKey = PackUserKey(builder, selector.Key);

						var iterator = ResolveCursor(lookupKey, selector.OrEqual, selector.Offset, sequence);
						Contract.Assert(iterator != null);

						if (iterator.Current == IntPtr.Zero)
						{
							//Trace.WriteLine("> NOTHING :(");
							results[i] = default(Slice);
							continue;
						}

						// we want the key!
						Key* key = (Key*)iterator.Current;
						Contract.Assert(key != null && key->Size <= MemoryDatabaseHandler.MAX_KEY_SIZE);

						var data = buffer.Allocate(checked((int)key->Size));
						Contract.Assert(data.Array != null && data.Offset >= 0 && data.Count == (int)key->Size);
						UnmanagedHelpers.CopyUnsafe(data, &(key->Data), key->Size);
						results[i] = data;
					}
				}
			}
			finally
			{
				m_dataLock.ExitReadLock();
			}

			return Task.FromResult(results);
		}

		private static unsafe KeyValuePair<Slice, Slice> CopyResultToManagedMemory(SliceBuffer buffer, Key* key, Value* value)
		{
			Contract.Requires(buffer != null && key != null && value != null);

			var keyData = buffer.Allocate(checked((int)key->Size));
			UnmanagedHelpers.CopyUnsafe(keyData, &(key->Data), key->Size);

			var valueData = buffer.Allocate(checked((int)value->Size));
			UnmanagedHelpers.CopyUnsafe(valueData, &(value->Data), value->Size);

			return new KeyValuePair<Slice, Slice>(keyData, valueData);
		}

		/// <summary>Range iterator that will return the keys and values at a specific sequence</summary>
		internal sealed unsafe class RangeIterator : IDisposable
		{
			private readonly MemoryDatabaseHandler m_handler;
			private readonly ulong m_sequence;
			private readonly ColaStore.Iterator<IntPtr> m_iterator;
			private readonly IntPtr m_stopKey;
			private readonly IComparer<IntPtr> m_comparer;
			private readonly long m_limit;
			private readonly long m_targetBytes;
			private readonly bool m_reverse;
			private bool m_done;
			private long m_readKeys;
			private long m_readBytes;
			private Key* m_currentKey;
			private Value* m_currentValue;
			private bool m_disposed;

			internal RangeIterator(MemoryDatabaseHandler handler, ulong sequence, ColaStore.Iterator<IntPtr> iterator, IntPtr stopKey, IComparer<IntPtr> comparer, bool reverse)
			{
				Contract.Requires(handler != null && iterator != null && comparer != null);
				m_handler = handler;
				m_sequence = sequence;
				m_iterator = iterator;
				m_stopKey = stopKey;
				m_comparer = comparer;
				m_reverse = reverse;
			}

			public long Sequence { get { return (long)m_sequence; } }

			public long Count { get { return m_readKeys; } }

			public long Bytes { get { return m_readBytes; } }

			public long TargetBytes { get { return m_targetBytes; } }

			public bool Reverse { get { return m_reverse; } }

			public Key* Key { get { return m_currentKey; } }

			public Value* Value { get { return m_currentValue; } }

			public bool Done { get { return m_done; } }

			public bool MoveNext()
			{
				if (m_done || m_disposed) return false;

				bool gotOne = false;

				while (!gotOne)
				{
					var current = m_iterator.Current;
					DumpKey("current", current);

					Value* value = MemoryDatabaseHandler.ResolveValueAtVersion(current, m_sequence);
					if (value != null)
					{
						if (m_stopKey != IntPtr.Zero)
						{
							int c = m_comparer.Compare(current, m_stopKey);
							if (m_reverse ? (c < 0 /* BEGIN KEY IS INCLUDED! */) : (c >= 0 /* END KEY IS EXCLUDED! */))
							{	// we reached the end, stop there !
								DumpKey("stopped at ", current);
								MarkAsDone();
								break;
							}
						}
						Key* key = (Key*)current;
						++m_readKeys;
						m_readBytes += checked(key->Size + value->Size);
						m_currentKey = key;
						m_currentValue = value;
						gotOne = true;
					}

					// prepare for the next value
					if (!(m_reverse ? m_iterator.Previous() : m_iterator.Next()))
					{
						// out of data to read ?
						MarkAsDone();
						break;
					}
				}

				if (gotOne)
				{ // we have found a value
					return true;
				}

				m_currentKey = null;
				m_currentValue = null;
				return false;
			}

			private void MarkAsDone()
			{
				m_done = true;
			}

			public void Dispose()
			{
				if (!m_disposed)
				{
					m_disposed = true;
					m_currentKey = null;
					m_currentValue = null;
					//TODO: release any locks taken
				}
			}
		}

		internal unsafe Task<FdbRangeChunk> GetRangeAtVersion(FdbKeySelector begin, FdbKeySelector end, int limit, int targetBytes, FdbStreamingMode mode, int iteration, bool reverse, long readVersion)
		{
			if (m_disposed) ThrowDisposed();

			//HACKHACK
			var results = new List<KeyValuePair<Slice, Slice>>(limit);

			if (limit == 0) limit = 10000;
			if (targetBytes == 0) targetBytes = int.MaxValue;

			//bool done = false;

			m_dataLock.EnterReadLock();
			try
			{
				ulong sequence = (ulong)readVersion;
				EnsureReadVersionNotInTheFuture_NeedsLocking(sequence);

				// TODO: convert all selectors to a FirstGreaterThan ?
				var buffer = new SliceBuffer();

				ColaStore.Iterator<IntPtr> iterator;
				IntPtr stopKey;

				if (!reverse)
				{ // forward range read: we read from beginKey, and stop once we reach a key >= endKey

					using (var scratch = m_scratchPool.Use())
					{
						// first resolve the end to get the stop point
						iterator = ResolveCursor(PackUserKey(scratch.Builder, end.Key), end.OrEqual, end.Offset, sequence);
						stopKey = iterator.Current; // note: can be ZERO !

						// now, set the cursor to the begin of the range
						iterator = ResolveCursor(PackUserKey(scratch.Builder, begin.Key), begin.OrEqual, begin.Offset, sequence);
						if (iterator.Current == IntPtr.Zero) iterator.SeekFirst();
					}

#if REFACTORED
					while (limit > 0 && targetBytes > 0)
					{
						DumpKey("current", iterator.Current);

						Value* value = ResolveValueAtVersion(iterator.Current, sequence);
						if (value != null)
						{
							if (stopKey != IntPtr.Zero && m_data.Comparer.Compare(iterator.Current, stopKey) >= 0) /* END KEY IS EXCLUDED! */
							{ // we reached the end, stop there !
								done = true;
								break;
							}
		
							var item = CopyResultToManagedMemory(buffer, (Key*)iterator.Current.ToPointer(), value);
							results.Add(item);
							--limit;
							targetBytes -= item.Key.Count + item.Value.Count;
							if (targetBytes < 0) targetBytes = 0;
						}

						if (!iterator.Next())
						{ // out of data to read ?
							done = true;
							break;
						}
					}
#endif
				}
				else
				{ // reverse range read: we start from the key before endKey, and stop once we read a key < beginKey

					using (var scratch = m_scratchPool.Use())
					{
						// first resolve the begin to get the stop point
						iterator = ResolveCursor(PackUserKey(scratch.Builder, begin.Key), begin.OrEqual, begin.Offset, sequence);
						DumpKey("resolved(" + begin + ")", iterator.Current);
						if (iterator.Current == IntPtr.Zero) iterator.SeekFirst();
						stopKey = iterator.Current; // note: can be ZERO !

						DumpKey("stopKey", stopKey);

						// now, set the cursor to the end of the range
						iterator = ResolveCursor(PackUserKey(scratch.Builder, end.Key), end.OrEqual, end.Offset, sequence);
						DumpKey("resolved(" + end + ")", iterator.Current);
						if (iterator.Current == IntPtr.Zero)
						{
							iterator.SeekLast();
							DumpKey("endKey", iterator.Current);
						}
						else
						{
							// note: since the end is NOT included in the result, we need to already move the cursor once
							iterator.Previous();
						}
					}

#if REFACTORED
					while (limit > 0 && targetBytes > 0)
					{
						DumpKey("current", iterator.Current);

						Value* value = ResolveValueAtVersion(iterator.Current, sequence);
						if (value != null)
						{
							if (stopKey != IntPtr.Zero && m_data.Comparer.Compare(iterator.Current, stopKey) < 0) /* BEGIN KEY IS INCLUDED! */
							{ // we reached past the beginning, stop there !
								DumpKey("stopped at ", iterator.Current);
								done = true;
								break;
							}

							var item = CopyResultToManagedMemory(buffer, (Key*)iterator.Current.ToPointer(), value);
							results.Add(item);
							--limit;
							targetBytes -= item.Key.Count + item.Value.Count;
							if (targetBytes < 0) targetBytes = 0;
						}

						if (!iterator.Previous())
						{ // out of data to read ?
							done = true;
							break;
						}
					}
#endif
				}

				// run the iterator until we reach the end of the range, the end of the database, or any count or size limit
				using (var rangeIterator = new RangeIterator(this, sequence, iterator, stopKey, m_data.Comparer, reverse))
				{
					while (rangeIterator.MoveNext())
					{
						var item = CopyResultToManagedMemory(buffer, rangeIterator.Key, rangeIterator.Value);
						results.Add(item);

						if (limit > 0 && rangeIterator.Count >= limit) break;
						if (targetBytes > 0 && rangeIterator.Bytes >= targetBytes) break;
					}

					bool hasMore = !rangeIterator.Done;

					var chunk = new FdbRangeChunk(
						hasMore,
						results.ToArray(),
						iteration,
						reverse
					);
					return Task.FromResult(chunk);
				}
			}
			finally
			{
				m_dataLock.ExitReadLock();
			}
		}

		public IFdbTransactionHandler CreateTransaction(FdbOperationContext context)
		{
			if (m_disposed) ThrowDisposed();
			Contract.Assert(context != null);

			MemoryTransactionHandler transaction = null;
			try
			{
				transaction = new MemoryTransactionHandler(this);
				//m_pendingTransactions.Add(transaction);
				return transaction;
			}
			catch(Exception)
			{
				if (transaction != null)
				{
					transaction.Dispose();
					//m_pendingTransactions.Remove(transaction);
				}
				throw;
			}
		}

		/// <summary>Return the read version of the oldest pending transaction</summary>
		/// <returns>Sequence number of the oldest active transaction, or the current read version if there are no pending transactions</returns>
		private ulong GetOldestReadVersion()
		{
			//HACKHACK: TODO!
			return (ulong)Volatile.Read(ref m_currentVersion);
		}

		#region Loading & Saving...

		internal async Task<long> SaveSnapshotAsync(string path, MemorySnapshotOptions options, CancellationToken cancellationToken)
		{
			Contract.Requires(path != null && options != null);

			if (string.IsNullOrWhiteSpace(path)) throw new ArgumentNullException("path");
			cancellationToken.ThrowIfCancellationRequested();

			// while we are generating the snapshot on the disk:
			// * readers can read without any problems
			// * writers can mutate values of existing keys, but cannot INSERT new keys

			var attributes = new Dictionary<string, IFdbTuple>(StringComparer.Ordinal);

			// Flags bits:
			// 0-3: FileType (4 bits)
			//		0: Versionned Snapshot
			//		1: Compact Snapshot
			//		2-15: reserved

			SnapshotFormat.Flags headerFlags = SnapshotFormat.Flags.None;
			switch (options.Mode)
			{
				case MemorySnapshotMode.Full:
				case MemorySnapshotMode.Last:
				{
					headerFlags |= SnapshotFormat.Flags.TYPE_SNAPSHOT_VERSIONNED;
					break;
				}
				case MemorySnapshotMode.Compact:
				{
					headerFlags |= SnapshotFormat.Flags.TYPE_SNAPSHOT_COMPACT;
					break;
				}
				default:
				{
					throw new InvalidOperationException("Invalid snapshot mode");
				}
			}

			attributes["version"] = FdbTuple.Create(1, 0);
			attributes["host"] = FdbTuple.Create(Environment.MachineName);
			attributes["timestamp"] = FdbTuple.Create(DateTimeOffset.Now.ToString("O"));

			if (options.Compressed)
			{ // file is compressed

				headerFlags |= SnapshotFormat.Flags.COMPRESSED;
				//TODO: specify compression algorithm...
				attributes["compression"] = FdbTuple.Create(true);
				attributes["compression.algorithm"] = FdbTuple.Create("lz4");
			}

			if (options.Signed)
			{ // file will have a cryptographic signature
				//TODO: specifiy digital signing algorithm
				headerFlags |= SnapshotFormat.Flags.SIGNED;
				attributes["signature"] = FdbTuple.Create(true);
				attributes["signature.algorithm"] = FdbTuple.Create("pkcs1");
			}

			if (options.Encrypted)
			{ // file will be encrypted
				//TODO: specify crypto algo, key sizes, initialization vectors, ...
				headerFlags |= SnapshotFormat.Flags.ENCRYPTED;
				attributes["encryption"] = FdbTuple.Create(true);
				attributes["encryption.algorithm"] = FdbTuple.Create("pkcs1");
				attributes["encryption.keysize"] = FdbTuple.Create(4096); //ex: RSA 4096 ?
			}

			//m_dataLock.EnterReadLock();
			try
			{

				// take the current version of the db (that will be used for the snapshot)
				ulong sequence = (ulong)Volatile.Read(ref m_currentVersion);
				long timestamp = DateTime.UtcNow.Ticks;
				int levels = m_data.Depth;
				int count = m_data.Count;

				using (var output = new Win32SnapshotFile(path))
				{
					var snapshot = new SnapshotWriter(output, levels, SnapshotFormat.PAGE_SIZE, SnapshotFormat.FLUSH_SIZE);

					//Console.WriteLine("> Writing header....");
					await snapshot.WriteHeaderAsync(
						headerFlags,
						new Uuid128(m_uid),
						sequence,
						count,
						timestamp,
						attributes
					).ConfigureAwait(false);

					//Console.WriteLine("> Writing level data...");
					for (int level = levels - 1; level >= 0; level--)
					{
						if (ColaStore.IsFree(level, count))
						{ // this level is not allocated
							//Console.WriteLine("  > Skipping empty level " + level);
							continue;
						}

						//Console.WriteLine("  > Dumping " + levels + " levels...");
						await snapshot.WriteLevelAsync(level, m_data.GetLevel(level), cancellationToken);
					}

					// Write the JumpTable to the end of the file
					//Console.WriteLine("> Writing Jump Table...");
					await snapshot.WriteJumpTableAsync(cancellationToken);

					// flush any remaining data to the disc
					//Console.WriteLine("> Flushing...");
					await snapshot.FlushAsync(cancellationToken);

					//Console.WriteLine("> Final file size if " + output.Length.ToString("N0") + " bytes");
				}
				//Console.WriteLine("> Done!");

				return (long)sequence;
			}
			finally
			{
				//m_dataLock.ExitReadLock();
			}
		}

		internal Task LoadSnapshotAsync(string path, MemorySnapshotOptions options, CancellationToken cancellationToken)
		{
			if (string.IsNullOrWhiteSpace(path)) throw new ArgumentNullException("path");

			//TODO: should this run on the writer thread ?
			return Task.Run(() => LoadSnapshotInternal(path, options, cancellationToken), cancellationToken);
		}

		private void LoadSnapshotInternal(string path, MemorySnapshotOptions options, CancellationToken cancellationToken)
		{ 
			Contract.Requires(path != null && options != null);

			var attributes = new Dictionary<string, IFdbTuple>(StringComparer.Ordinal);

			//m_dataLock.EnterWriteLock();
			try
			{
				using (var source = Win32MemoryMappedFile.OpenRead(path))
				{
					var snapshot = new SnapshotReader(source);

					// Read the header
					//Console.WriteLine("> Reading Header");
					snapshot.ReadHeader(cancellationToken);

					// Read the jump table (at the end)
					//Console.WriteLine("> Reading Jump Table");
					snapshot.ReadJumpTable(cancellationToken);

					// we should have enough information to allocate memory
					m_data.Clear();
					m_estimatedSize = 0;

					using (var writer = new LevelWriter(1 << snapshot.Depth, m_keys, m_values))
					{
						// Read the levels
						for (int level = snapshot.Depth - 1; level >= 0; level--)
						{
							if (!snapshot.HasLevel(level))
							{
								continue;
							}

							//Console.WriteLine("> Reading Level " + level);
							//TODO: right we read the complete level before bulkloading it
							// we need to be able to bulk load directly from the stream!
							snapshot.ReadLevel(level, writer, cancellationToken);

							m_data.InsertItems(writer.Data, ordered: true);
							writer.Reset();
						}
					}

					m_uid = snapshot.Id.ToGuid();
					m_currentVersion = (long)snapshot.Sequence;

					//Console.WriteLine("> done!");
				}
			}
			finally
			{
				//m_dataLock.ExitWriteLock();
			}
		}

		#endregion

		#region Writer Thread...

		private sealed class CommitState : TaskCompletionSource<object>
		{
			public CommitState(MemoryTransactionHandler trans)
				: base()
			{
				Contract.Requires(trans != null);
				this.Transaction = trans;
			}

			public void MarkAsCompleted()
			{
				if (!this.Task.IsCompleted)
				{
					ThreadPool.UnsafeQueueUserWorkItem((state) => { ((CommitState)state).TrySetResult(null); }, this);
				}
			}

			public void MarkAsFailed(Exception e)
			{
				if (!this.Task.IsCompleted)
				{
					ThreadPool.UnsafeQueueUserWorkItem(
						(state) =>
						{
							var items = (Tuple<CommitState, Exception>)state;
							items.Item1.TrySetException(items.Item2);
						},
						Tuple.Create(this, e)
					);
				}
			}

			public void MarkAsCancelled()
			{
				if (!this.Task.IsCompleted)
				{
					ThreadPool.UnsafeQueueUserWorkItem((state) => { ((CommitState)state).TrySetResult(null); }, this);
				}
			}

			public MemoryTransactionHandler Transaction { get; private set; }

		}

		[Conditional("FULL_DEBUG")]
		private static void Log(string msg)
		{
			Trace.WriteLine("MemoryDatabaseHandler[#" + Thread.CurrentThread.ManagedThreadId + "]: " + msg);
		}

		private const int STATE_IDLE = 0;
		private const int STATE_RUNNNING = 1;
		private const int STATE_SHUTDOWN = 2;

		private int m_eventLoopState = STATE_IDLE;
		private AutoResetEvent m_writerEvent = new AutoResetEvent(false);
		private ConcurrentQueue<CommitState> m_writerQueue = new ConcurrentQueue<CommitState>();
		private ManualResetEvent m_shutdownEvent = new ManualResetEvent(false);

		internal Task EnqueueCommit(MemoryTransactionHandler trans)
		{
			if (trans == null) throw new ArgumentNullException("trans");

			if (Volatile.Read(ref m_eventLoopState) == STATE_SHUTDOWN)
			{
				throw new FdbException(FdbError.OperationFailed, "The database has already been disposed");
			}

			var entry = new CommitState(trans);
			try
			{
				m_writerQueue.Enqueue(entry);

				// wake up the writer thread if needed
				// note: we need to set the event BEFORE changing the eventloop state, because the writer thread may be in the process of shutting down
				m_writerEvent.Set();
				Log("Enqueued new commit");

				if (Interlocked.CompareExchange(ref m_eventLoopState, STATE_RUNNNING, STATE_IDLE) == STATE_IDLE)
				{ // we have to start the event loop
					Log("Starting new Writer EventLoop...");
					var _ = Task.Factory.StartNew(() => WriteEventLoop(), CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
				}
			}
			catch (Exception e)
			{
				entry.SetException(e);
			}
			return entry.Task;
		}

		/// <summary>Event loop that is called to process all the writes to the database</summary>
		private void WriteEventLoop()
		{
			TimeSpan quanta = TimeSpan.FromSeconds(30);

			// confirm that we can still run
			if (Interlocked.CompareExchange(ref m_eventLoopState, STATE_RUNNNING, STATE_RUNNNING) != STATE_RUNNNING)
			{ // a shutdown was retquested, exit immediately
				Log("WriteEventLoop fast abort");
				return;
			}

			Log("WriteEventLoop started");

			try
			{
				bool keepGoing = true;
				while (keepGoing)
				{
					// Wait() will:
					// - return true if we have a new entry to process
					// - return false if the quanta timeout has expired
					// - throw an OperationCanceledException if the cancellation token was triggered
					if (m_writerEvent.WaitOne(quanta))
					{
						Log("WriteEventLoop wake up");
						CommitState entry;

						// process all the pending writes
						while (Volatile.Read(ref m_eventLoopState) != STATE_SHUTDOWN && m_writerQueue.TryDequeue(out entry))
						{
							if (entry.Task.IsCompleted)
							{ // the task has already been completed/cancelled?
								continue;
							}

							try
							{
								Log("WriteEventLoop process transaction");
								//TODO: work !
								entry.Transaction.CommitInternal();
								entry.MarkAsCompleted();
							}
							catch (Exception e)
							{
								Log("WriteEventLoop transaction failed: " + e.Message);
								entry.MarkAsFailed(new FdbException(FdbError.InternalError, "The transaction failed to commit", e));
							}
						}

						if (Volatile.Read(ref m_eventLoopState) == STATE_SHUTDOWN)
						{ // we have been asked to shutdown
							Log("WriteEventLoop shutdown requested");
							// drain the commit queue, and mark all of them as failed
							while (m_writerQueue.TryDequeue(out entry))
							{
								if (entry != null) entry.MarkAsCancelled();
							}
							keepGoing = false;
						}
					}
					else
					{ // try to step down

						Log("WriteEventLoop no activity");
						Interlocked.CompareExchange(ref m_eventLoopState, STATE_IDLE, STATE_RUNNNING);
						// check again if nobody was trying to queue a write at the same time
						if (!m_writerEvent.WaitOne(TimeSpan.Zero, false) || Interlocked.CompareExchange(ref m_eventLoopState, STATE_RUNNNING, STATE_IDLE) == STATE_IDLE)
						{ // either there were no pending writes, or we lost the race and will be replaced by another thread
							Log("WriteEventLoop will step down");
							keepGoing = false; // stop
						}
#if DEBUG
						else
						{
							Log("WriteEventLoop will resume");
						}
#endif
					}
				}
				Log("WriteEventLoop exit");
			}
			catch(Exception)
			{
				//TODO: fail all pending commits ?
				// reset the state to IDLE so that another write can restart us
				Interlocked.CompareExchange(ref m_eventLoopState, STATE_IDLE, STATE_RUNNNING);
				throw;
			}
			finally
			{
				if (Volatile.Read(ref m_eventLoopState) == STATE_SHUTDOWN)
				{
					m_shutdownEvent.Set();
				}
			}
		}

		private void StopWriterEventLoop()
		{
			// signal a shutdown
			Log("WriterEventLoop requesting stop...");
			int oldState;
			if ((oldState = Interlocked.Exchange(ref m_eventLoopState, STATE_SHUTDOWN)) != STATE_SHUTDOWN)
			{
				switch (oldState)
				{
					case STATE_RUNNNING:
					{
						// need to wake up the thread, if it was waiting for new writes
						m_writerEvent.Set();
						// and wait for it to finish...
						if (!m_shutdownEvent.WaitOne(TimeSpan.FromSeconds(5)))
						{
							// what should we do ?
						}
						Log("WriterEventLoop stopped");
						break;
					}
					default:
					{ // not running, or already shutdown ?
						m_shutdownEvent.Set();
						break;
					}
				}
			}
		}

		#endregion

		/// <summary>Perform a complete garbage collection</summary>
		public void Collect()
		{
			// - determine the old read version that is in use
			// - look for all the windows that are older than that
			// - collect all keys that were modified in these windows (value changed, or deleted)
			// - for all heap pages that are above a freespace threshold, merge them into fewer full pages

			m_dataLock.EnterUpgradeableReadLock();
			try
			{

				// collect everything that is oldest than the oldest active read version.
				ulong sequence = GetOldestReadVersion();

				lock (m_heapLock)
				{
					// purge the dead values
					m_values.Collect(sequence);

					// pack the keys
					//m_keys.Collect(sequence);
					//BUGBUG: need to purge the colastore also !
				}

				m_oldestVersion = (long)sequence;
			}
			finally
			{
				m_dataLock.ExitUpgradeableReadLock();
			}


		}

		public void Dispose()
		{
			if (!m_disposed)
			{
				m_disposed = true;

				StopWriterEventLoop();
				//TODO: need to lock and ensure that all pending transactions are done

				m_writerEvent.Dispose();
				m_shutdownEvent.Dispose();

				m_keys.Dispose();
				m_values.Dispose();
				if (m_transactionWindows != null)
				{
					foreach (var window in m_transactionWindows)
					{
						if (window != null) window.Dispose();
					}
				}
				if (m_scratchPool != null) m_scratchPool.Dispose();
				m_scratchKey.Dispose();
				m_scratchValue.Dispose();
			}
		}

		private void ThrowDisposed()
		{
			throw new ObjectDisposedException("The database has already been disposed");
		}

		[Conditional("DEBUG")]
		public void Debug_Dump(bool detailed = false)
		{
			Debug.WriteLine("Dumping content of Database");
			m_dataLock.EnterReadLock();
			try
			{
				Debug.WriteLine("> Version: {0}", m_currentVersion);
				Debug.WriteLine("> Items: {0:N0}", m_data.Count);
				Debug.WriteLine("> Estimated size: {0:N0} bytes", m_estimatedSize);
				Debug.WriteLine("> Transaction windows: {0}", m_transactionWindows.Count);
				foreach(var window in m_transactionWindows)
				{
					Debug.WriteLine("  > {0} : {1:N0} commits{2}", window.ToString(), window.CommitCount, window.Closed ? " [CLOSED]" : "");
				}
				long cmps, eqs, ghcs;
				NativeKeyComparer.GetCounters(out cmps, out eqs, out ghcs);
				Debug.WriteLine("> Comparisons: {0:N0} compares, {1:N0} equals, {2:N0} hashcodes", cmps, eqs, ghcs);
				NativeKeyComparer.ResetCounters();
				lock (m_heapLock)
				{
					unsafe
					{
						m_keys.Debug_Dump(detailed);
						m_values.Debug_Dump(detailed);
					}
				}
				Debug.WriteLine("");
			}
			finally
			{
				m_dataLock.ExitReadLock();
			}
		}

	}

}
