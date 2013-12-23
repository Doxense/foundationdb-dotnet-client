#region Copyright (c) 2013-2014, Doxense SAS. All rights reserved.
// See License.MD for license information
#endregion

namespace FoundationDB.Storage.Memory.API
{
	using FoundationDB.Client;
	using FoundationDB.Client.Core;
	using FoundationDB.Client.Utils;
	using FoundationDB.Storage.Memory.Core;
	using FoundationDB.Storage.Memory.Utils;
	using System;
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
		internal const uint KEYHEAP_MAX_PAGESIZE = 1024 * 1024;
		internal const uint VALUEHEAP_MIN_PAGESIZE = 1024 * 1024;
		internal const uint VALUEHEAP_MAX_PAGESIZE = 16 * 1024 * 1024;

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
		private static readonly ReaderWriterLockSlim m_dataLock = new ReaderWriterLockSlim();
		private static readonly object m_heapLock = new object();

		private KeyHeap m_keys = new KeyHeap(KEYHEAP_MIN_PAGESIZE, KEYHEAP_MAX_PAGESIZE);
		private ValueHeap m_values = new ValueHeap(VALUEHEAP_MIN_PAGESIZE, VALUEHEAP_MAX_PAGESIZE);

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

		/// <summary>Format a user key</summary>
		/// <param name="buffer"></param>
		/// <param name="userKey"></param>
		/// <returns></returns>
		private unsafe static USlice PackUserKey(UnmanagedSliceBuilder buffer, Slice userKey)
		{
			if (buffer == null) throw new ArgumentNullException("buffer");
			if (userKey.Count == 0) throw new ArgumentException("Key cannot be empty");
			if (userKey.Count < 0 || userKey.Offset < 0 || userKey.Array == null) throw new ArgumentException("Malformed key");

			uint keySize = (uint)userKey.Count;
			var size = Key.SizeOf + keySize;
			var tmp = buffer.Allocate(size);
			var key = (Key*)tmp.Data;
			key->Size = keySize;
			key->Header = ((uint)EntryType.Key) << Entry.TYPE_SHIFT;
			key->Values = null;

			UnmanagedHelpers.CopyUnsafe(&(key->Data), userKey);
			return tmp;
		}

		private TimeSpan m_maxTransactionLifetime = TimeSpan.FromSeconds(5);

		private TransactionWindow GetActiveTransactionWindow_NeedsLocking(ulong sequence)
		{
			var window = m_currentWindow;
			var now = DateTime.UtcNow;

			if (window != null)
			{ // is it still active ?

				if (window.Closed)
				{
					window = null;
				}
				else if (now.Subtract(window.StartedUtc) > m_maxTransactionLifetime)
				{
					window.Close();
					window = null;
				}
			}

			if (window == null)
			{ // need to start a new window
				window = new TransactionWindow(DateTime.UtcNow, sequence);
				m_currentWindow = window;
				m_transactionWindows.AddFirst(window);
			}

			return window;
		}

		internal unsafe Task<long> CommitTransactionAsync(MemoryTransactionHandler trans, long readVersion, ColaRangeSet<Slice> readConflicts, ColaRangeSet<Slice> writeConflicts, ColaRangeSet<Slice> clearRanges, ColaOrderedDictionary<Slice, MemoryTransactionHandler.WriteCommand[]> writes)
		{
			if (trans == null) throw new ArgumentNullException("trans");
			if (m_disposed) ThrowDisposed();

			// version at which the transaction was created (and all reads performed)
			ulong readSequence = (ulong)readVersion;
			// commit version created by this transaction (if it writes something)
			ulong committedSequence = 0;

			//Debug.WriteLine("Comitting transaction created at readVersion " + readVersion + " ...");

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
					//Debug.WriteLine("... will create version " + committedSequence + " in window " + window.ToString());
				}
				else
				{
					//Debug.WriteLine("... which is read-only");
					window = null;
				}

				#region Read Conflict Check

				if (hasReadConflictRanges)
				{

					var current = m_transactionWindows.First;
					while (current != null)
					{
						if (current.Value.LastVersion < readSequence)
						{
							break;
						}
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
							m_scratchKey.Clear();
							USlice lookupKey = PackUserKey(m_scratchKey, write.Key);

							IntPtr previous;
							int offset, level = m_data.Find(lookupKey.GetPointer(), out offset, out previous);
							key = level >= 0 ? (Key*)previous.ToPointer() : null;
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
									Thread.MemoryBarrier();
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
									Thread.MemoryBarrier();

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

			return Task.FromResult(version);
		}

		internal unsafe Task BulkLoadAsync(ICollection<KeyValuePair<Slice, Slice>> data, bool ordered, CancellationToken cancellationToken)
		{
			Contract.Requires(data != null);

			int count = data.Count;

			// Since we can "only" create a maximum of 28 levels, there is a maximum limit or 2^28 - 1 items that can be loaded in the database (about 268 millions)
			if (count >= 1 << 28) throw new InvalidOperationException("Data set is too large. Cannot insert more than 2^28 - 1 items in the memory database");

			// clear everything, and import the specified data

			m_dataLock.EnterWriteLock();
			try
			{
				m_data.Clear();
				m_estimatedSize = 0;
				//TODO: clear the key and value heaps !
				//TODO: clear the transaction windows !
				//TODO: kill all pending transactions !

				// the fastest way to insert data, is to insert vectors that are a power of 2
				int min = ColaStore.LowestBit(count);
				int max = ColaStore.HighestBit(count);
				Contract.Assert(min <= max && max <= 28);

				m_data.EnsureCapacity(count);

				ulong sequence = (ulong)Interlocked.Increment(ref m_currentVersion);

				using (var iter = data.GetEnumerator())
				{
					var list = new List<IntPtr>(1 << max);

					for (int level = max; level >= min && !cancellationToken.IsCancellationRequested; level--)
					{
						if (ColaStore.IsFree(level, count)) continue;

						//TODO: consider pre-sorting the items before inserting them in the heap using m_comparer (maybe faster than doing the same with the key comparer?)

						// take of batch of values
						list.Clear();
						int batch = 1 << level;
						while(batch-- > 0)
						{
							if (!iter.MoveNext())
							{
								throw new InvalidOperationException("Iterator stopped before reaching the expected number of items");
							}

							// allocate the key
							var tmp = PackUserKey(m_scratchKey, iter.Current.Key);
							Key* key = m_keys.Append(tmp);
							Contract.Assert(key != null, "key == null");

							// allocate the value
							Slice userValue = iter.Current.Value;
							uint size = checked((uint)userValue.Count);
							Value* value = m_values.Allocate(size, sequence, null, key);
							Contract.Assert(value != null, "value == null");
							UnmanagedHelpers.CopyUnsafe(&(value->Data), userValue);

							key->Values = value;

							list.Add(new IntPtr(key));
						}

						// and insert it (should fit nicely in a level without cascading)
						m_data.InsertItems(list, ordered);
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

				Key* key = (Key*)userKey.ToPointer();
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

			Key* key = (Key*)existing.ToPointer();
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

		internal unsafe Task<Slice> GetValueAtVersionAsync(Slice userKey, long readVersion)
		{
			if (m_disposed) ThrowDisposed();
			if (userKey.Count <= 0) throw new ArgumentException("Key cannot be empty");

			m_dataLock.EnterReadLock();
			try
			{
				ulong sequence = (ulong)readVersion;
				EnsureReadVersionNotInTheFuture_NeedsLocking(sequence);

				// create a lookup key
				using (var scratch = m_scratchPool.Use())
				{
					var lookupKey = PackUserKey(scratch.Builder, userKey);

					USlice value;
					if (!TryGetValueAtVersion(lookupKey, sequence, out value))
					{ // la clé n'existe pas (ou plus à cette version)
						return NilResult;
					}

					if (value.Count == 0)
					{ // la clé existe, mais est vide
						return EmptyResult;
					}

					return Task.FromResult(value.ToSlice());
				}
			}
			finally
			{
				m_dataLock.ExitReadLock();
			}
		}

		internal unsafe Task<Slice[]> GetValuesAtVersionAsync(Slice[] userKeys, long readVersion)
		{
			if (m_disposed) ThrowDisposed();
			if (userKeys == null) throw new ArgumentNullException("userKeys");

			var results = new Slice[userKeys.Length];

			m_dataLock.EnterReadLock();
			try
			{
				ulong sequence = (ulong)readVersion;
				EnsureReadVersionNotInTheFuture_NeedsLocking(sequence);

				var buffer = new SliceBuffer();

				using(var scratch = m_scratchPool.Use())
				{

					for (int i = 0; i < userKeys.Length; i++)
					{
						// create a lookup key
						var lookupKey = PackUserKey(scratch.Builder, userKeys[i]);

						USlice value;
						if (!TryGetValueAtVersion(lookupKey, sequence, out value))
						{ // cette clé n'existe pas ou plus a cette version
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
				return Task.FromResult(results);
			}
			finally
			{
				m_dataLock.ExitReadLock();
			}
		}

		/// <summary>Walk the value chain, to return the value of a key that was the latest at a specific read version</summary>
		/// <param name="userKey">User key to resolve</param>
		/// <param name="sequence">Sequence number</param>
		/// <returns>Value of the key at that time, or null if the key was either deleted or not yet created.</returns>
		private static unsafe Value* ResolveValueAtVersion(IntPtr userKey, ulong sequence)
		{
			if (userKey == IntPtr.Zero) return null;

			Key* key = (Key*)userKey.ToPointer();
			Contract.Assert((key->Header & Entry.FLAGS_DISPOSED) == 0, "Attempted to read value from a disposed key");
			Contract.Assert(key->Size > 0, "Attempted to read value from an empty key");

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
				var value = ResolveValueAtVersion(iterator.Current, sequence);
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

		internal unsafe Task<Slice> GetKeyAtVersion(FdbKeySelector selector, long readVersion)
		{
			if (m_disposed) ThrowDisposed();
			if (selector.Key.IsNullOrEmpty) throw new ArgumentException("selector");

			m_dataLock.EnterReadLock();
			try
			{
				ulong sequence = (ulong)readVersion;
				EnsureReadVersionNotInTheFuture_NeedsLocking(sequence);

				// TODO: convert all selectors to a FirstGreaterThan ?

				using (var scratch = m_scratchPool.Use())
				{
					var lookupKey = PackUserKey(scratch.Builder, selector.Key);

					var iterator = ResolveCursor(lookupKey, selector.OrEqual, selector.Offset, sequence);
					Contract.Assert(iterator != null);

					if (iterator.Current == IntPtr.Zero)
					{
						if (iterator.Direction <= 0)
						{
							// specs: "If a key selector would otherwise describe a key off the beginning of the database, it instead resolves to the empty key ''."
							return EmptyResult;
						}
						else
						{
							//TODO: access to system keys !
							return MaxResult;
						}
					}

					//Trace.WriteLine("> Found it !");

					// we want the key!
					Key* key = (Key*)iterator.Current.ToPointer();
					Contract.Assert(key != null && key->Size > 0);

					var tmp = Key.GetData(key);
					return Task.FromResult(tmp.ToSlice());
				}
			}
			finally
			{
				m_dataLock.ExitReadLock();
			}

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

					for (int i = 0; i < selectors.Length; i++)
					{
						var selector = selectors[i];

						var lookupKey = PackUserKey(scratch.Builder, selector.Key);

						var iterator = ResolveCursor(lookupKey, selector.OrEqual, selector.Offset, sequence);
						Contract.Assert(iterator != null);

						if (iterator.Current == IntPtr.Zero)
						{
							//Trace.WriteLine("> NOTHING :(");
							results[i] = default(Slice);
							continue;
						}

						// we want the key!
						Key* key = (Key*)iterator.Current.ToPointer();
						Contract.Assert(key != null && key->Size > 0);

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

		internal unsafe Task<FdbRangeChunk> GetRangeAtVersion(FdbKeySelector begin, FdbKeySelector end, int limit, int targetBytes, FdbStreamingMode mode, int iteration, bool reverse, long readVersion)
		{
			if (m_disposed) ThrowDisposed();

			//HACKHACK
			var results = new List<KeyValuePair<Slice, Slice>>(limit);

			if (limit == 0) limit = 10000;
			if (targetBytes == 0) targetBytes = int.MaxValue;

			bool done = false;

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
						if (iterator.Current == IntPtr.Zero) iterator.SeekLast();
						DumpKey("endKey", iterator.Current);

						// note: since the end is NOT included in the result, we need to already move the cursor once
						iterator.Previous();
					}

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
				}

				bool hasMore = !done;

				var chunk = new FdbRangeChunk(
					hasMore,
					results.ToArray(),
					iteration,
					reverse
				);
				return Task.FromResult(chunk);

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

		/// <summary>Perform a complete garbage collection</summary>
		public void Collect()
		{

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

				//TODO: need to lock and ensure that all pending transactions are done

				if (m_keys != null) m_keys.Dispose();
				if (m_values != null) m_values.Dispose();
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
			Trace.WriteLine("Dumping content of Database");
			m_dataLock.EnterReadLock();
			try
			{
				Trace.WriteLine("> Version: " + m_currentVersion);
				Trace.WriteLine("> Estimated size: " + m_estimatedSize.ToString("N0") + " bytes");
				//Trace.WriteLine("> Content: {0} keys", m_data.Count.ToString("N0"));

				Trace.WriteLine("> Transaction windows: " + m_transactionWindows.Count);
				foreach(var window in m_transactionWindows)
				{
					Trace.WriteLine("  > " + window.ToString() + ": " + window.CommitCount + " commits" + (window.Closed ? " [CLOSED]" : ""));
				}

				//Trace.WriteLine(String.Format("> Keys: {0} bytes in {1} pages", m_keys.Gen0.MemoryUsage.ToString("N0"), m_keys.Gen0.PageCount.ToString("N0")));
				//Trace.WriteLine(String.Format("> Values: {0} bytes in {1} pages", m_values.MemoryUsage.ToString("N0"), m_values.PageCount.ToString("N0")));
				lock (m_heapLock)
				{
					unsafe
					{
						m_keys.Debug_Dump(detailed);
						m_values.Debug_Dump(detailed);
					}
				}

#if false || FULLDEBUG
#if false
				int p = 0;
				foreach (var kvp in m_data.IterateUnordered())
				{
					DumpKey((p++).ToString(), kvp);
				}
#endif
				Trace.WriteLine("> Storage:");
				m_data.Debug_Dump((userKey) =>
				{
					if (userKey == IntPtr.Zero) return "<null>";
					unsafe
					{
						Key* key = (Key*)userKey.ToPointer();
						return FdbKey.Dump(new USlice(&(key->Data), key->Size).ToSlice());
					}
				});
#endif
				Trace.WriteLine("");
			}
			finally
			{
				m_dataLock.ExitReadLock();
			}
		}

	}

}
