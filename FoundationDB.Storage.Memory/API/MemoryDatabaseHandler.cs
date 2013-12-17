#region Copyright (c) 2013-2014, Doxense SAS. All rights reserved.
// See License.MD for license information
#endregion

namespace FoundationDB.Storage.Memory.API
{
	using FoundationDB.Client;
	using FoundationDB.Client.Core;
	using FoundationDB.Client.Utils;
	using FoundationDB.Storage.Memory.Core;
	using System;
	using System.Collections.Generic;
	using System.Diagnostics;
	using System.Diagnostics.Contracts;
	using System.Runtime.InteropServices;
	using System.Text;
	using System.Threading;
	using System.Threading.Tasks;

	internal class GenerationalUnmanagedHeap : IDisposable
	{
		private const int DEFAULT_GENERATIONS = 2;
		private const int DEFAULT_PAGESIZE = 0;

		private int m_generations;
		private UnmanagedMemoryHeap[] m_heaps;
		private bool m_disposed;

		public GenerationalUnmanagedHeap()
			: this(DEFAULT_GENERATIONS, DEFAULT_PAGESIZE, 0)
		{ }

		public GenerationalUnmanagedHeap(int generations, uint pageSize, uint alignment)
		{
			if (generations <= 0) throw new ArgumentOutOfRangeException("generations");

			m_generations = generations;
			var heaps = new UnmanagedMemoryHeap[generations];
			for (int i = 0; i < generations;i++)
			{
				heaps[i] = new UnmanagedMemoryHeap(pageSize, alignment);
			}

			m_generations = generations;
			m_heaps = heaps;
		}

		public int MaxGeneration { get { return m_generations; } }

		public long GetTotalMemory()
		{
			if (m_disposed) return 0;
			long total = 0;
			foreach(var heap in m_heaps)
			{
				total += heap.MemoryUsage;
			}
			return total;
		}

		public UnmanagedMemoryHeap Gen0
		{
			get
			{
				if (m_disposed) ThrowDisposed();
				return m_heaps[0];
			}
		}

		public UnmanagedMemoryHeap this[int gen]
		{
			get
			{
				if (m_disposed) ThrowDisposed();
				return m_heaps[gen];
			}
		}

		public void Dispose()
		{
			Dispose(true);
			GC.SuppressFinalize(this);
		}

		private void ThrowDisposed()
		{
			throw new ObjectDisposedException(this.GetType().Name);
		}

		private void Dispose(bool disposing)
		{
			if (!m_disposed)
			{
				m_disposed = true;
				if (m_heaps != null && m_heaps.Length > 0)
				{
					foreach(var heap in m_heaps)
					{
						if (heap != null)
						{
							try { heap.Dispose(); }
							catch { }
						}
					}
				}
			}
		}

	}

	internal class MemoryDatabaseHandler : IFdbDatabaseHandler, IDisposable
	{

		#region Private Members...

		/// <summary>Set to true when the current db instance gets disposed.</summary>
		private volatile bool m_disposed;

		/// <summary>Current version of the database</summary>
		private long m_currentVersion;
		/// <summary>Oldest legal read version of the database</summary>
		private long m_oldestVersion;

		//TODO: replace this with an Async lock ?
		private static readonly ReaderWriterLockSlim m_dataLock = new ReaderWriterLockSlim();
		private static readonly object m_heapLock = new object();

		private KeyHeap m_keys = new KeyHeap(0, 64 * 1024);
		private ValueHeap m_values = new ValueHeap(0, 1024 * 1024);

		private ColaStore<IntPtr> m_data = new ColaStore<IntPtr>(0, new NativeKeyComparer());
		private long m_estimatedSize;

		/// <summary>List of all current transactions that have been created</summary>
		private HashSet<MemoryTransactionHandler> m_pendingTransactions = new HashSet<MemoryTransactionHandler>();
		/// <summary>List of all transactions that have requested a read version, but have not committed anything yet</summary>
		private Queue<MemoryTransactionHandler> m_activeTransactions = new Queue<MemoryTransactionHandler>();

		#endregion

		public MemoryDatabaseHandler()
		{
			////HACKHACK: move this somewhere else ?
			//using(var tr = new MemoryTransactionHandler(this))
			//{
			//	tr.AccessSystemKeys = true;
			//	tr.Set(Slice.FromByte(255), Slice.Empty);
			//	tr.Set(Slice.FromByte(255) + Slice.FromByte(255), Slice.Empty);
			//	tr.CommitAsync(CancellationToken.None).Wait();
			//}
		}

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

		internal void MarkTransaction(MemoryTransactionHandler transaction, long readVersion)
		{
			Contract.Requires(transaction != null);

			lock (m_activeTransactions)
			{
				if (m_activeTransactions.Count == 0)
				{
				}
				else
				{
				}
				m_activeTransactions.Enqueue(transaction);
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

		internal unsafe Task<long> CommitTransactionAsync(MemoryTransactionHandler trans, ColaRangeSet<Slice> readConflicts, ColaRangeSet<Slice> writeConflicts, ColaRangeSet<Slice> clearRanges, ColaOrderedDictionary<Slice, MemoryTransactionHandler.WriteCommand[]> writes)
		{
			if (trans == null) throw new ArgumentNullException("trans");
			if (m_disposed) ThrowDisposed();

			ulong sequence;

			UnmanagedSliceBuilder scratchKey = null;
			UnmanagedSliceBuilder scratchValue = null;

			m_dataLock.EnterUpgradeableReadLock();
			try
			{
				scratchKey = new UnmanagedSliceBuilder();
				scratchValue = new UnmanagedSliceBuilder();

				sequence = (ulong)Interlocked.Increment(ref m_currentVersion);

				if (clearRanges != null && clearRanges.Count > 0)
				{
					foreach (var clear in clearRanges)
					{
						//TODO!
						throw new NotImplementedException("ClearRange not yet implemented. Sorry!");
					}
				}

				if (writes != null && writes.Count > 0)
				{
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
						USlice lookupKey = PackUserKey(scratchKey, write.Key);

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
								scratchValue.Set(op.Value);
								hasTmpData = true;
								valueMutated = true;
								continue;
							}

							// apply the atomic operation to the previous value
							if (!hasTmpData)
							{
								scratchValue.Clear();
								if (key != null)
								{ // grab the current value of this key

									Value* p = key->Values;
									if ((p->Header & Value.FLAGS_DELETION) == 0)
									{
										scratchValue.Append(&(p->Data), p->Size);
									}
									else
									{
										scratchValue.Clear();
										currentIsDeleted = true;
									}
								}
								hasTmpData = true;
							}

							switch (op.Type)
							{
								case MemoryTransactionHandler.Operation.AtomicAdd:
								{
									op.ApplyAddTo(scratchValue);
									valueMutated = true;
									break;
								}
								case MemoryTransactionHandler.Operation.AtomicBitAnd:
								{
									op.ApplyBitAndTo(scratchValue);
									valueMutated = true;
									break;
								}
								case MemoryTransactionHandler.Operation.AtomicBitOr:
								{
									op.ApplyBitOrTo(scratchValue);
									valueMutated = true;
									break;
								}
								case MemoryTransactionHandler.Operation.AtomicBitXor:
								{
									op.ApplyBitXorTo(scratchValue);
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
								value = m_values.Allocate(scratchValue.Count, sequence, key != null ? key->Values : null, null);
							}
							Contract.Assert(value != null);
							scratchValue.CopyTo(&(value->Data));
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

								// insert the new key into the data store
								m_dataLock.EnterWriteLock();
								try
								{
									m_data.Insert(new IntPtr(key));
								}
								finally
								{
									m_dataLock.ExitWriteLock();
								}
							}

						}
					}
				}
			}
			finally
			{
				m_dataLock.ExitUpgradeableReadLock();
				if (scratchValue != null) scratchValue.Dispose();
				if (scratchKey != null) scratchKey.Dispose();
			}

			//TODO: IMPLEMENT REAL DATABASE HERE :)
			var version = (long)sequence;

			return Task.FromResult(version);
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
				using (var scratch = new UnmanagedSliceBuilder())
				{
					var lookupKey = PackUserKey(scratch, userKey);

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

				using(var scratch = new UnmanagedSliceBuilder())
				{

					for (int i = 0; i < userKeys.Length; i++)
					{
						// create a lookup key
						var lookupKey = PackUserKey(scratch, userKeys[i]);

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

				using (var scratch = new UnmanagedSliceBuilder())
				{
					var lookupKey = PackUserKey(scratch, selector.Key);

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

				using (var scratch = new UnmanagedSliceBuilder())
				{

					for (int i = 0; i < selectors.Length; i++)
					{
						var selector = selectors[i];

						var lookupKey = PackUserKey(scratch, selector.Key);

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

					using (var scratch = new UnmanagedSliceBuilder())
					{
						// first resolve the end to get the stop point
						iterator = ResolveCursor(PackUserKey(scratch, end.Key), end.OrEqual, end.Offset, sequence);
						stopKey = iterator.Current; // note: can be ZERO !

						// now, set the cursor to the begin of the range
						iterator = ResolveCursor(PackUserKey(scratch, begin.Key), begin.OrEqual, begin.Offset, sequence);
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

					using (var scratch = new UnmanagedSliceBuilder())
					{
						// first resolve the begin to get the stop point
						iterator = ResolveCursor(PackUserKey(scratch, begin.Key), begin.OrEqual, begin.Offset, sequence);
						DumpKey("resolved(" + begin + ")", iterator.Current);
						if (iterator.Current == IntPtr.Zero) iterator.SeekFirst();
						stopKey = iterator.Current; // note: can be ZERO !

						DumpKey("stopKey", stopKey);

						// now, set the cursor to the end of the range
						iterator = ResolveCursor(PackUserKey(scratch, end.Key), end.OrEqual, end.Offset, sequence);
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
		public void GC()
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
				//TODO?
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

				//Trace.WriteLine(String.Format("> Keys: {0} bytes in {1} pages", m_keys.Gen0.MemoryUsage.ToString("N0"), m_keys.Gen0.PageCount.ToString("N0")));
				//Trace.WriteLine(String.Format("> Values: {0} bytes in {1} pages", m_values.MemoryUsage.ToString("N0"), m_values.PageCount.ToString("N0")));
				lock (m_heapLock)
				{
					if (detailed)
					{
						//m_keys.Gen0.Dump(detailed: false);
						//m_values.Gen0.Dump(detailed: false);
					}
					//m_keys.Gen0.DumpToDisk("keys.bin");
					//m_values.Gen0.DumpToDisk("values.bin");

					unsafe
					{
						m_keys.Debug_Dump();
						m_values.Debug_Dump();
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

		[StructLayout(LayoutKind.Sequential, Pack = 1)]
		internal unsafe struct Entry
		{
			/// <summary>Default alignement for objects (8 by default)</summary>
			public const int ALIGNMENT = 8; // MUST BE A POWER OF 2 !
			public const int ALIGNMENT_MASK = ~(ALIGNMENT - 1);

			/// <summary>This entry has been moved to another page by the last GC</summary>
			public const uint FLAGS_MOVED = 0x100;

			/// <summary>This key has been flaged as being unreachable by current of future transaction (won't survive the next GC)</summary>
			public const uint FLAGS_UNREACHABLE = 0x2000;

			/// <summary>The entry has been disposed and should be access anymore</summary>
			public const uint FLAGS_DISPOSED = 0x80000000;

			public const int TYPE_SHIFT = 29;
			public const uint TYPE_MASK_AFTER_SHIFT = 0x3;

			// Object Layout
			// ==============

			// Offset	Field	Type	Desc
			// 
			//      0	HEADER	uint	Type, Flags, ...
			//		4	SIZE	uint	Size of the data
			//		... object fields ...
			//		x	DATA	byte[]	Value of the object, size in the SIZE field
			//		y	(pad)	0..7	padding bytes (set to 00 or FF ?)
			//
			// HEADER: bit flags
			// - bit 31: DISPOSED, set if object is disposed
			// - bit 29-30: TYPE

			/// <summary>Various flags (TODO: enum?)</summary>
			public uint Header;

			/// <summary>Size of the key (in bytes)</summary>
			public uint Size;

			/// <summary>Return the type of the object</summary>
			public static unsafe EntryType GetObjectType(void* item)
			{
				return item == null ? EntryType.Free : (EntryType)((((Entry*)item)->Header >> TYPE_SHIFT) & TYPE_MASK_AFTER_SHIFT);
			}

			/// <summary>Checks if the object is disposed</summary>
			public static unsafe bool IsDisposed(void* item)
			{
				return item == null || (((Entry*)item)->Header & FLAGS_DISPOSED) != 0;
			}

			internal static byte* Align(byte* ptr)
			{
				long r = ((long)ptr) & (ALIGNMENT - 1);
				if (r > 0) ptr += ALIGNMENT - r;
				return ptr;
			}

			internal static bool IsAligned(void* ptr)
			{
				return (((long)ptr) & (ALIGNMENT - 1)) == 0;
			}

			internal static int Padding(void* ptr)
			{
				return (int) (((long)ptr) & (ALIGNMENT - 1));
			}
		}

		public enum EntryType
		{
			Free = 0,
			Key = 1,
			Value = 2,
			Search = 3
		}

		[StructLayout(LayoutKind.Sequential, Pack = 1)]
		internal unsafe struct Key
		{
			public static readonly uint SizeOf = (uint)Marshal.OffsetOf(typeof(Key), "Data").ToInt32();

			/// <summary>The key has been inserted after the last GC</summary>
			public const uint FLAGS_NEW = 0x1000;
			/// <summary>The key has been created/mutated since the last GC</summary>
			public const uint FLAGS_MUTATED = 0x2000;
			/// <summary>There is a watch listening on this key</summary>
			public const uint FLAGS_HAS_WATCH = 0x2000;

			/// <summary>Various flags (TODO: enum?)</summary>
			public uint Header;
			/// <summary>Size of the key (in bytes)</summary>
			public uint Size;
			/// <summary>Pointer to the head of the value chain for this key (should not be null)</summary>
			public Value* Values;
			/// <summary>Offset to the first byte of the key</summary>
			public byte Data;

			public static USlice GetData(Key* self)
			{
				if (self == null) return default(USlice);
				Contract.Assert((self->Header & Entry.FLAGS_DISPOSED) == 0, "Attempt to read a key that was disposed");
				return new USlice(&(self->Data), self->Size);
			}

			public static bool StillAlive(Key* self, ulong sequence)
			{
				if (self == null) return false;

				if ((self->Header & Entry.FLAGS_UNREACHABLE) != 0)
				{ // we have been marked as dead

					var value = self->Values;
					if (value == null) return false;

					// check if the last value is a deletion?
					if (value->Sequence <= sequence && (value->Header & Value.FLAGS_DELETION) != 0)
					{ // it is deleted
						return false;
					}
				}

				return true;
			}

			public static bool IsDisposed(Key* self)
			{
				return (self->Header & Entry.FLAGS_DISPOSED) != 0;
			}

			/// <summary>Return the address of the following value in the heap</summary>
			internal static Key* WalkNext(Key* self)
			{
				Contract.Requires(self != null && Entry.GetObjectType(self) == EntryType.Key);

				return (Key*)Entry.Align((byte*)self + Key.SizeOf + self->Size);
			}

		}

		[StructLayout(LayoutKind.Sequential, Pack = 1)]
		internal unsafe struct Value
		{
			public static readonly uint SizeOf = (uint)Marshal.OffsetOf(typeof(Value), "Data").ToInt32();

			/// <summary>This value is a deletion marker</summary>
			public const uint FLAGS_DELETION = 0x1;
			/// <summary>This value has been mutated and is not up to date</summary>
			public const uint FLAGS_MUTATED = 0x2;

			/// <summary>Various flags (TDB)</summary>
			public uint Header;
			/// <summary>Size of the value</summary>
			public uint Size;
			/// <summary>Version were this version of the key first appeared</summary>
			public ulong Sequence;
			/// <summary>Pointer to the previous version of this key, or NULL if this is the earliest known</summary>
			public Value* Previous;
			/// <summary>Pointer to the parent node (can be a Key or a Value)</summary>
			public void* Parent;
			/// <summary>Offset to the first byte of the value</summary>
			public byte Data;

			public static USlice GetData(Value* value)
			{
				if (value == null) return default(USlice);

				Contract.Assert((value->Header & Entry.FLAGS_DISPOSED) == 0, "Attempt to read a value that was disposed");
				return new USlice(&(value->Data), value->Size);
			}

			public static bool StillAlive(Value* value, ulong sequence)
			{
				if (value == null) return false;
				if ((value->Header & Value.FLAGS_MUTATED) != 0)
				{
					return value->Sequence >= sequence;
				}
				return true;
			}

			public static bool IsDisposed(Value* value)
			{
				return (value->Header & Entry.FLAGS_DISPOSED) != 0;
			}

			/// <summary>Return the address of the following value in the heap</summary>
			internal static Value* WalkNext(Value* self)
			{
				Contract.Requires(self != null && Entry.GetObjectType(self) == EntryType.Value);

				return (Value*)Entry.Align((byte*)self + Value.SizeOf + self->Size);
			}

		}

		internal unsafe abstract class EntryPage : IDisposable
		{
			protected byte* m_current;
			protected byte* m_start;
			protected byte* m_end;
			protected uint m_capacity;
			protected int m_count;
			protected SafeHandle m_handle;

			public EntryPage(SafeHandle handle, uint capacity)
			{
				Contract.Requires(handle != null && !handle.IsInvalid && !handle.IsClosed);

				m_handle = handle;
				m_capacity = capacity;
				m_start = (byte*) handle.DangerousGetHandle().ToPointer();
				m_end = m_start + capacity;
				m_current = m_start;

				CheckInvariants();
			}

			[Conditional("DEBUG")]
			protected void CheckInvariants()
			{
				Contract.Assert(!m_handle.IsInvalid, "Memory handle should not be invalid");
				Contract.Assert(!m_handle.IsClosed, "Memory handle should not be closed");
				Contract.Ensures(Entry.IsAligned(m_current), "Current pointer should always be aligned");
				Contract.Assert(m_current <= m_start + m_capacity, "Current pointer should never be outside the page");
			}

			public int Count { get { return m_count; } }

			public long MemoryUsage { get { return (long)m_current - (long)m_start; } }

			public abstract EntryType Type { get; }

			public void Dispose()
			{
				if (!m_handle.IsClosed)
				{
					m_start = null;
					m_current = null;
					m_handle.Close();
				}
			}

			internal static byte* Align(byte* ptr, byte* end)
			{
				long r = ((long)ptr) & (Entry.ALIGNMENT - 1);
				if (r > 0) ptr += Entry.ALIGNMENT - r;
				if (ptr > end) return end;
				return ptr;
			}

			protected byte* TryAllocate(uint size)
			{
				// try to allocate an amount of memory
				// - returns null if the page is full, or too small
				// - returns a pointer to the allocated space

				byte* ptr = m_current;
				byte* end = m_end;
				byte* next = ptr + size;
				if (next > m_end)
				{ // does not fit in this page
					return null;
				}

				// update the cursor for the next value
				next = (byte*) (((long)next + Entry.ALIGNMENT - 1) & Entry.ALIGNMENT_MASK);
				if (next > end) next = end;
				m_current = next;
				++m_count;

				CheckInvariants();
				return ptr;
			}

			public void Swap(EntryPage target)
			{
				Contract.Requires(target != null);

				var old = m_handle;
				m_handle = target.m_handle;
				m_count = target.Count;
				m_capacity = target.m_capacity;
				m_start = target.m_start;
				m_end = target.m_end;
				m_current = target.m_current;
				old.Dispose();
				CheckInvariants();
			}

			[Conditional("DEBUG")]
			public abstract void Debug_Dump();
		}

		internal abstract class ElasticHeap<TPage>
			where TPage : EntryPage
		{
			protected List<TPage> m_pages = new List<TPage>();
			protected int m_gen;
			protected uint m_pageSize;
			protected TPage m_current;

			public ElasticHeap(int generation, uint pageSize)
			{
				if (generation < 0) throw new ArgumentOutOfRangeException("generation");

				m_gen = generation;
				m_pageSize = pageSize;
			}

			[Conditional("DEBUG")]
			public void Debug_Dump()
			{
				Console.WriteLine("# Dumping elastic " + typeof(TPage).Name + " heap (" + m_pages.Count + " pages)");
				foreach(var page in m_pages)
				{
					page.Debug_Dump();
				}
			}
		}

		internal unsafe class KeyHeap : ElasticHeap<KeyHeap.Page>
		{

			public static Page CreateNewPage(uint size, uint alignment)
			{
				// the size of the page should also be aligned
				var pad = size & (alignment - 1);
				if (pad != 0)
				{
					size += alignment - pad;
				}
				if (size > int.MaxValue) throw new OutOfMemoryException();

				UnmanagedHelpers.SafeLocalAllocHandle handle = null;
				try
				{
					handle = UnmanagedHelpers.AllocMemory(size);
					return new Page(handle, size);
				}
				catch (Exception)
				{
					if (!handle.IsClosed) handle.Dispose();
					throw;
				}
			}

			/// <summary>Page of memory used to store Keys</summary>
			public sealed unsafe class Page : EntryPage
			{

				public Page(SafeHandle handle, uint capacity)
					: base(handle, capacity)
				{ }

				public override EntryType Type
				{
					get { return EntryType.Key; }
				}

				/// <summary>Copy an existing value to this page, and return the pointer to the copy</summary>
				/// <param name="value">Value that must be copied to this page</param>
				/// <returns>Pointer to the copy in this page</returns>
				public Key* TryAppend(Key* value)
				{
					Contract.Requires(value != null && Entry.GetObjectType(value) == EntryType.Value);

					uint rawSize = Key.SizeOf + value->Size;
					var entry = (Key*)TryAllocate(rawSize);
					if (entry == null) return null; // this page is full

					UnmanagedHelpers.CopyUnsafe((byte*)entry, (byte*)value, rawSize);

					return entry;
				}

				public Key* TryAppend(USlice buffer)
				{
					Contract.Requires(buffer.Data != null
						&& buffer.Count >= Key.SizeOf
						&& ((Key*)buffer.Data)->Size == buffer.Count - Key.SizeOf);

					var entry = (Key*)TryAllocate(buffer.Count);
					if (entry == null) return null; // this page is full

					UnmanagedHelpers.CopyUnsafe((byte*)entry, buffer.Data, buffer.Count);
					entry->Header = ((uint)EntryType.Key) << Entry.TYPE_SHIFT;

					return entry;
				}

				public void Collect(KeyHeap.Page target, ulong sequence)
				{
					var current = (Key*)m_start;
					var end = (Key*)m_current;

					while (current < end)
					{
						bool keep = Key.StillAlive(current, sequence);

						if (keep)
						{ // copy to the target page

							var moved = target.TryAppend(current);
							if (moved == null) throw new InvalidOperationException("The target page was too small");

							var values = current->Values;
							if (values != null)
							{
								values->Parent = moved;
							}

							current->Header |= Entry.FLAGS_MOVED | Entry.FLAGS_DISPOSED;
						}
						else
						{
							current->Header |= Entry.FLAGS_DISPOSED;
						}

						current = Key.WalkNext(current);
					}


				}

				public override void Debug_Dump()
				{
					Contract.Requires(m_start != null && m_current != null);
					Key* current = (Key*)m_start;
					Key* end = (Key*)m_current;

					Trace.WriteLine("## KeyPage: count=" + m_count.ToString("N0") + ", used=" + this.MemoryUsage.ToString("N0") + ", capacity=" + m_capacity.ToString("N0") + ", start=0x" + new IntPtr(m_start).ToString("X8") + ", end=0x" + new IntPtr(m_current).ToString("X8"));

					while (current < end)
					{
						Trace.WriteLine("   - [" + Entry.GetObjectType(current).ToString() + "] 0x" + new IntPtr(current).ToString("X8") + " : " + current->Header.ToString("X8") + ", size=" + current->Size + " : " + FdbKey.Dump(Key.GetData(current).ToSlice()));
						var value = current->Values;
						while (value != null)
						{
							Trace.WriteLine("     -> [" + Entry.GetObjectType(value) + "] 0x" + new IntPtr(value).ToString("X8") + " @ " + value->Sequence + " : " + Value.GetData(value).ToSlice().ToAsciiOrHexaString());
							value = value->Previous;
						}
						current = Key.WalkNext(current);
					}
				}
			}

			public KeyHeap(int gen, uint pageSize)
				: base(gen, pageSize)
			{ }

			public Key* Append(USlice buffer)
			{
				Page page;
				var entry = (page = m_current) != null ? page.TryAppend(buffer) : null;
				if (entry == null)
				{
					if (buffer.Count > m_pageSize >> 1)
					{ // if the value is too big, it will use its own page

						page = CreateNewPage(buffer.Count, Entry.ALIGNMENT);
						m_pages.Add(page);
					}
					else
					{ // allocate a new page and try again

						page = CreateNewPage(m_pageSize, Entry.ALIGNMENT);
						m_current = page;
					}

					Contract.Assert(page != null);
					m_pages.Add(page);
					entry = page.TryAppend(buffer);
					Contract.Assert(entry != null);
				}
				Contract.Assert(entry != null);
				return entry;
			}


			public void Collect(ulong sequence)
			{
				foreach(var page in m_pages)
				{
					var target = CreateNewPage(m_pageSize, Entry.ALIGNMENT);
					page.Collect(target, sequence);
					page.Swap(target);
				}

			}

		}

		internal unsafe class ValueHeap : ElasticHeap<ValueHeap.Page>
		{

			public static Page CreateNewPage(uint size, uint alignment)
			{
				//Console.WriteLine("Created value page: " + size);
				// the size of the page should also be aligned
				var pad = size & (alignment - 1);
				if (pad != 0)
				{
					size += alignment - pad;
				}
				if (size > int.MaxValue) throw new OutOfMemoryException();

				UnmanagedHelpers.SafeLocalAllocHandle handle = null;
				try
				{
					handle = UnmanagedHelpers.AllocMemory(size);
					return new Page(handle, size);
				}
				catch (Exception)
				{
					if (!handle.IsClosed) handle.Dispose();
					throw;
				}
			}

			/// <summary>Page of memory used to store Values</summary>
			public sealed class Page : EntryPage
			{

				public Page(SafeHandle handle, uint capacity)
					: base(handle, capacity)
				{ }

				public override EntryType Type
				{
					get { return EntryType.Value; }
				}

				/// <summary>Copy an existing value to this page, and return the pointer to the copy</summary>
				/// <param name="value">Value that must be copied to this page</param>
				/// <returns>Pointer to the copy in this page</returns>
				public Value* TryAppend(Value* value)
				{
					Contract.Requires(value != null && Entry.GetObjectType(value) == EntryType.Value);

					uint rawSize = Value.SizeOf + value->Size;
					Value* entry = (Value*)TryAllocate(rawSize);
					if (entry == null) return null; // the page is full

					UnmanagedHelpers.CopyUnsafe((byte*)entry, (byte*)value, rawSize);

					return entry;
				}

				public Value* TryAppend(USlice buffer)
				{
					Contract.Requires(buffer.Data != null
						&& buffer.Count >= Value.SizeOf
						&& ((Key*)buffer.Data)->Size == buffer.Count - Value.SizeOf);

					var entry = (Value*)TryAllocate(buffer.Count);
					if (entry == null) return null; // the page is full
					UnmanagedHelpers.CopyUnsafe((byte*)entry, buffer.Data, buffer.Count);

					return entry;
				}

				public Value* TryAllocate(uint dataSize, ulong sequence, Value* previous, void* parent)
				{
					Value* entry = (Value*)TryAllocate(Value.SizeOf + dataSize);
					if (entry == null) return null; // the page is full

					entry->Header = ((uint)EntryType.Value) << Entry.TYPE_SHIFT;
					entry->Size = dataSize;
					entry->Sequence = sequence;
					entry->Previous = previous;
					entry->Parent = parent;

					return entry;
				}

				public void Collect(Page target, ulong sequence)
				{
					var current = (Value*)m_start;
					var end = (Value*)m_current;

					while (current < end)
					{
						bool keep = Value.StillAlive(current, sequence);

						void* parent = current->Parent;

						if (keep)
						{ // copy to the target page

							var moved = target.TryAppend(current);
							if (moved == null) throw new InvalidOperationException(); // ??

							// update the parent
							switch (Entry.GetObjectType(parent))
							{
								case EntryType.Key:
									{
										((Key*)parent)->Values = moved;
										break;
									}
								case EntryType.Value:
									{
										((Value*)parent)->Previous = moved;
										break;
									}
								case EntryType.Free:
									{
										//NO-OP
										break;
									}
								default:
									{
										throw new InvalidOperationException("Unexpected parent while moving value");
									}
							}
							current->Header |= Entry.FLAGS_MOVED | Entry.FLAGS_DISPOSED;
						}
						else
						{
							// we need to kill the link from the parent
							switch (Entry.GetObjectType(parent))
							{
								case EntryType.Key:
									{
										((Key*)parent)->Values = null;
										break;
									}
								case EntryType.Value:
									{
										((Value*)parent)->Previous = null;
										break;
									}
								case EntryType.Free:
									{
										//NO-OP
										break;
									}
								default:
									{
										throw new InvalidOperationException("Unexpected parent while destroying value");
									}
							}

							current->Header |= Entry.FLAGS_DISPOSED;
						}

						current = Value.WalkNext(current);
					}


				}

				public override void Debug_Dump()
				{
					Contract.Requires(m_start != null && m_current != null);
					Value* current = (Value*)m_start;
					Value* end = (Value*)m_current;

					Trace.WriteLine("## ValuePage: count=" + m_count.ToString("N0") + ", used=" + this.MemoryUsage.ToString("N0") + ", capacity=" + m_capacity.ToString("N0") + ", start=0x" + new IntPtr(m_start).ToString("X8") + ", end=0x" + new IntPtr(m_current).ToString("X8"));

					while (current < end)
					{
						Trace.WriteLine("   - [" + Entry.GetObjectType(current).ToString() + "] 0x" + new IntPtr(current).ToString("X8") + " : " + current->Header.ToString("X8") + ", seq=" + current->Sequence + ", size=" + current->Size + " : " + Value.GetData(current).ToSlice().ToAsciiOrHexaString());
						if (current->Previous != null) Trace.WriteLine("     -> Previous: [" + Entry.GetObjectType(current->Previous) + "] 0x" + new IntPtr(current->Previous).ToString("X8"));
						if (current->Parent != null) Trace.WriteLine("     <- Parent: [" + Entry.GetObjectType(current->Parent) + "] 0x" + new IntPtr(current->Parent).ToString("X8"));

						current = Value.WalkNext(current);
					}
				}

			}

			public ValueHeap(int gen, uint pageSize)
				: base(gen, pageSize)
			{ }

			public Value* Allocate(uint dataSize, ulong sequence, Value* previous, void* parent)
			{
				Page page;
				var entry = (page = m_current) != null ? page.TryAllocate(dataSize, sequence, previous, parent) : null;
				if (entry == null)
				{
					uint size = dataSize + Value.SizeOf;
					if (size > m_pageSize >> 1)
					{ // if the value is too big, it will use its own page

						page = CreateNewPage(size, Entry.ALIGNMENT);
						m_pages.Add(page);

					}
					else
					{ // allocate a new page and try again

						page = CreateNewPage(m_pageSize, Entry.ALIGNMENT);
						m_current = page;
					}

					Contract.Assert(page != null);
					m_pages.Add(page);
					entry = page.TryAllocate(dataSize, sequence, previous, parent);
					Contract.Assert(entry != null);
				}
				Contract.Assert(entry != null);
				return entry;
			}

			public void Collect(ulong sequence)
			{
				foreach(var page in m_pages)
				{
					var target = CreateNewPage(m_pageSize, Entry.ALIGNMENT);
					if (page.Count == 1)
					{ // this is a standalone page
						page.Collect(target, sequence);
						page.Swap(target);
					}
					else
					{
						page.Collect(target, sequence);
						page.Swap(target);
					}
				}

			}

		}

		private unsafe sealed class NativeKeyComparer : IComparer<IntPtr>
		{
			// Keys:
			// * KEY_SIZE		UInt16		Size of the key
			// * KEY_FLAGS		UInt16		Misc flags
			// * VALUE_PTR		IntPtr		Pointer to the head of the value chain for this key
			// * KEY_BYTES		variable	Content of the key
			// * (padding)		variable	padding to align the size to 4 or 8 bytes

			public int Compare(IntPtr left, IntPtr right)
			{
				// unwrap as pointers to the Key struct
				var leftKey = (Key*)left.ToPointer();
				var rightKey = (Key*)right.ToPointer();

				//if (leftKey == rightKey) return 0;
				//if (leftKey == null) return -1;
				//if (rightKey == null) return +1;

				uint leftCount, rightCount;

				if (leftKey == null || (leftCount = leftKey->Size) == 0) return rightKey == null || rightKey->Size == 0 ? 0 : -1;
				if (rightKey == null || (rightCount = rightKey->Size) == 0) return +1;

#if true
				int c = UnmanagedHelpers.NativeMethods.memcmp(&(leftKey->Data), &(rightKey->Data), leftCount < rightCount ? leftCount : rightCount);
				if (c == 0) c = (int)leftCount - (int)rightCount;
				return c;
#else
				return UnmanagedHelpers.CompareUnsafe(
					&(leftKey->Data), leftCount,
					&(rightKey->Data), rightCount
				);
#endif
			}
		}

	}

}
