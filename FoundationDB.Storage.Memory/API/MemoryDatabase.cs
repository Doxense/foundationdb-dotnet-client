using FoundationDB.Client;
using FoundationDB.Client.Utils;
using FoundationDB.Storage.Memory.Core;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace FoundationDB.Storage.Memory.API
{

	public class MemoryDatabase : IFdbDatabase, IDisposable
	{
		/// <summary>Global counters used to generate the transaction's local id (for debugging purpose)</summary>
		private static int s_transactionCounter;

		#region Private Members...

		/// <summary>Set to true when the current db instance gets disposed.</summary>
		private volatile bool m_disposed;

		private readonly string m_name;

		private readonly bool m_readOnly;

		private FdbSubspace m_globalSpace;

		private readonly CancellationTokenSource m_cts = new CancellationTokenSource();

		private long m_version = 0;

		//TODO: replace this with an Async lock ?
		private static readonly ReaderWriterLockSlim m_dataLock = new ReaderWriterLockSlim();
		private static readonly object m_heapLock = new object();

		private UnmanagedMemoryHeap m_keyHeap = new UnmanagedMemoryHeap(64 * 1024);
		private UnmanagedMemoryHeap m_valueHeap = new UnmanagedMemoryHeap(1024 * 1024);

		private ColaStore<IntPtr> m_data = new ColaStore<IntPtr>(0, new NativeKeyComparer());
		private long m_estimatedSize;

		/// <summary>Default Timeout value for all transactions</summary>
		private int m_defaultTimeout;

		/// <summary>Default RetryLimit value for all transactions</summary>
		private int m_defaultRetryLimit;

		#endregion

		public MemoryDatabase(string name, FdbSubspace subspace, bool readOnly)
		{
			if (string.IsNullOrEmpty(name)) throw new ArgumentException("name");

			m_name = name;
			m_readOnly = readOnly;
			ChangeGlobalSpace(subspace);

			//HACKHACK: move this somewhere else ?
			using(var tr = BeginTransaction(FdbTransactionMode.Default, CancellationToken.None, null))
			{
				tr.WithAccessToSystemKeys();
				tr.Set(Slice.FromByte(255), Slice.Empty);
				tr.Set(Slice.FromByte(255) + Slice.FromByte(255), Slice.Empty);
				tr.CommitAsync().Wait();
			}
		}

		public string Name
		{
			get { return m_name; }
		}

		public CancellationToken Token
		{
			get { return m_cts.Token; }
		}

		/// <summary>Change the current global namespace.</summary>
		/// <remarks>Do NOT call this, unless you know exactly what you are doing !</remarks>
		internal void ChangeGlobalSpace(FdbSubspace subspace)
		{
			subspace = subspace ?? FdbSubspace.Empty;
			m_globalSpace = new FdbSubspace(subspace.Key);
		}

		public FdbSubspace GlobalSpace
		{
			get { return m_globalSpace; }
		}

		public bool IsReadOnly
		{
			get { return m_readOnly; }
		}

		public void SetOption(FdbDatabaseOption option)
		{
			throw new NotImplementedException();
		}

		public void SetOption(FdbDatabaseOption option, string value)
		{
			throw new NotImplementedException();
		}

		public void SetOption(FdbDatabaseOption option, long value)
		{
			throw new NotImplementedException();
		}

		#region Default Transaction Settings...

		/// <summary>Default Timeout value (in milliseconds) for all transactions created from this database instance.</summary>
		/// <remarks>Only effective for future transactions</remarks>
		public int DefaultTimeout
		{
			get { return m_defaultTimeout; }
			set
			{
				if (value < 0) throw new ArgumentOutOfRangeException("value", value, "Timeout value cannot be negative");
				m_defaultTimeout = value;
			}
		}

		/// <summary>Default Retry Limit value for all transactions created from this database instance.</summary>
		/// <remarks>Only effective for future transactions</remarks>
		public int DefaultRetryLimit
		{
			get { return m_defaultRetryLimit; }
			set
			{
				if (value < 0) throw new ArgumentOutOfRangeException("value", value, "RetryLimit value cannot be negative");
				m_defaultRetryLimit = value;
			}
		}

		#endregion

		internal long GetCurrentVersion()
		{
			//TODO: locking ?
			return Volatile.Read(ref m_version);
		}

		/// <summary>Format a user key</summary>
		/// <param name="buffer"></param>
		/// <param name="userKey"></param>
		/// <returns></returns>
		private unsafe static USlice PackUserKey(UnmanagedSliceBuilder buffer, Slice userKey, uint flags, Value* values)
		{
			if (buffer == null) throw new ArgumentNullException("buffer");
			if (userKey.Count == 0) throw new ArgumentException("Key cannot be empty");
			if (userKey.Count < 0 || userKey.Offset < 0 || userKey.Array == null) throw new ArgumentException("Malformed key");

			uint keySize = (uint)userKey.Count;
			var size = Key.SizeOf + keySize;
			var tmp = buffer.Allocate(size);
			var key = (Key*)tmp.Data;
			key->Size = keySize;
			key->Flags = flags;
			key->Values = values;

			UnmanagedHelpers.CopyUnsafe(&(key->Data), userKey);
			return tmp;
		}

		internal unsafe Task<long> CommitTransactionAsync(MemoryTransaction trans, ColaRangeSet<Slice> readConflicts, ColaRangeSet<Slice> writeConflicts, ColaRangeSet<Slice> clearRanges, ColaOrderedDictionary<Slice, MemoryTransaction.WriteCommand[]> writes)
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

				sequence = (ulong)Interlocked.Increment(ref m_version);

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
						USlice lookupKey = PackUserKey(scratchKey, write.Key, 0, null);

						IntPtr previous;
						int offset, level = m_data.Find(lookupKey.GetPointer(), out offset, out previous);
						key = level >= 0 ? (Key*)previous.ToPointer() : null;
						Contract.Assert((level < 0 && key == null) || (level >= 0 && offset >= 0 && key != null));

						bool valueMutated = false;
						bool currentIsDeleted = false;
						bool hasTmpData = false;

						foreach (var op in write.Value)
						{
							if (op.Type == MemoryTransaction.Operation.Nop) continue;

							if (op.Type == MemoryTransaction.Operation.Set)
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
									if ((p->Flags & Value.FLAGS_DELETION) == 0)
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
								case MemoryTransaction.Operation.AtomicAdd:
								{
									op.ApplyAddTo(scratchValue);
									valueMutated = true;
									break;
								}
								case MemoryTransaction.Operation.AtomicBitAnd:
								{
									op.ApplyBitAndTo(scratchValue);
									valueMutated = true;
									break;
								}
								case MemoryTransaction.Operation.AtomicBitOr:
								{
									op.ApplyBitOrTo(scratchValue);
									valueMutated = true;
									break;
								}
								case MemoryTransaction.Operation.AtomicBitXor:
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
								value = (Value*)m_valueHeap.Allocate(scratchValue.Count + Value.SizeOf);
							}
							Contract.Assert(value != null);
							value->Sequence = sequence;
							value->Previous = key != null ? key->Values : null;
							value->Size = scratchValue.Count;
							value->Flags = 0; //TODO
							value->MovedTo = null;
							scratchValue.CopyTo(&(value->Data));
							Interlocked.Add(ref m_estimatedSize, value->Size);

							if (key != null)
							{ // mutate the previous version for this key
								key->Values->Flags |= Value.FLAGS_MUTATED;
								key->Values = value;

								// make sure no thread seees an inconsitent view of the key
								Thread.MemoryBarrier();
							}
							else
							{ // add this key to the data store

								// we can reuse the lookup key (which is only missing the correct flags and pointers to the values)
								lock (m_heapLock)
								{
									var heapKey = m_keyHeap.MemoizeAligned(lookupKey);
									key = (Key*)heapKey.Data;
								}
								key->Values = value;
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

		private void EnsureReadVersionNotInTheFuture_NeedsLocking(ulong readVersion)
		{
			if ((ulong)Volatile.Read(ref m_version) < readVersion)
			{ // a read for a future version? This is most probably a bug !
#if DEBUG
				if (Debugger.IsAttached) Debugger.Break();
#endif
				throw new FdbException(FdbError.FutureVersion);
			}
		}

		[Conditional("DEBUG")]
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
					if ((value->Flags & Value.FLAGS_DELETION) != 0)
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

			if (current == null || (current->Flags & Value.FLAGS_DELETION) != 0)
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
					var lookupKey = PackUserKey(scratch, userKey, 0, null);

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
						var lookupKey = PackUserKey(scratch, userKeys[i], 0, null);

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
			Contract.Assert((key->Flags & Key.FLAGS_DISPOSED) == 0, "Attempted to read value from a disposed key");
			Contract.Assert(key->Size > 0, "Attempted to read value from an empty key");

			Value* current = key->Values;
			while(current != null && current->Sequence > sequence)
			{
				current = current->Previous;
			}

			if (current == null || (current->Flags & Value.FLAGS_DELETION) != 0)
			{
				return null;
			}

			Contract.Ensures((current->Flags & Value.FLAGS_DISPOSED) == 0 && current->Sequence <= sequence);
			return current;
		}

		private unsafe ColaStore.Iterator<IntPtr> ResolveCursor(USlice lookupKey, bool orEqual, int offset, ulong sequence)
		{
			var iterator = m_data.GetIterator();

			// seek to the closest key
			iterator.Seek2(lookupKey.GetPointer(), orEqual);
			DumpKey(orEqual ?"seek(<=)" : "seek(<)", lookupKey.GetPointer());

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
					if (!iterator.Next2())
					{
						//Trace.WriteLine("  > EOF");
						break;
					}
				}
				else
				{ // move backward
					//Trace.WriteLine("> prev!");
					if (!iterator.Previous2())
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
					var lookupKey = PackUserKey(scratch, selector.Key, 0, null);

					var iterator = ResolveCursor(lookupKey, selector.OrEqual, selector.Offset, sequence);
					Contract.Assert(iterator != null);

					if (iterator.Current == IntPtr.Zero)
					{
						//Trace.WriteLine("> NOTHING :(");
						return NilResult;
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

						var lookupKey = PackUserKey(scratch, selector.Key, 0, null);

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
			if (limit == 0) limit = 10000;
			if (targetBytes == 0) targetBytes = int.MaxValue;
			var results = new List<KeyValuePair<Slice, Slice>>(limit);

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
						iterator = ResolveCursor(PackUserKey(scratch, end.Key, 0, null), end.OrEqual, end.Offset, sequence);
						stopKey = iterator.Current; // note: can be ZERO !

						// now, set the cursor to the begin of the range
						iterator = ResolveCursor(PackUserKey(scratch, begin.Key, 0, null), begin.OrEqual, begin.Offset, sequence);
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

						if (!iterator.Next2())
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
						iterator = ResolveCursor(PackUserKey(scratch, begin.Key, 0, null), begin.OrEqual, begin.Offset, sequence);
						DumpKey("resolved(" + begin + ")", iterator.Current);
						if (iterator.Current == IntPtr.Zero) iterator.SeekFirst();
						stopKey = iterator.Current; // note: can be ZERO !

						DumpKey("stopKey", stopKey);

						// now, set the cursor to the end of the range
						iterator = ResolveCursor(PackUserKey(scratch, end.Key, 0, null), end.OrEqual, end.Offset, sequence);
						DumpKey("resolved(" + end + ")", iterator.Current);
						if (iterator.Current == IntPtr.Zero) iterator.SeekLast();
						DumpKey("endKey", iterator.Current);

						// note: since the end is NOT included in the result, we need to already move the cursor once
						iterator.Previous2();
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

						if (!iterator.Previous2())
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

		public IFdbTransaction BeginTransaction(FdbTransactionMode mode, CancellationToken cancellationToken = default(CancellationToken), FdbOperationContext context = null)
		{
			if (m_disposed) ThrowDisposed();

			if (context == null) context = new FdbOperationContext(this, mode, cancellationToken);
			return CreateTransaction(context);
		}

		internal MemoryTransaction CreateTransaction(FdbOperationContext context)
		{
			if (m_disposed) ThrowDisposed();
			Contract.Assert(context != null);

			//TODO: locking?

			// force the transaction to be read-only, if the database itself is read-only
			var mode = context.Mode;
			if (m_readOnly) mode |= FdbTransactionMode.ReadOnly;

			int id = Interlocked.Increment(ref s_transactionCounter);

			var tr = new MemoryTransaction(this, context, id, mode);

			if (m_defaultTimeout != 0) tr.Timeout = m_defaultTimeout;
			if (m_defaultRetryLimit != 0) tr.RetryLimit = m_defaultRetryLimit;

			return tr;
		}


		#region Transactionals...

		#region IFdbReadOnlyTransactional methods...

		/// <summary>Runs a transactional lambda function against this database, inside a read-only transaction context, with retry logic.</summary>
		/// <param name="asyncHandler">Asynchronous lambda function that is passed a new read-only transaction on each retry.</param>
		/// <param name="cancellationToken">Optional cancellation token that will be passed to the transaction context, and that can also be used to abort the retry loop.</param>
		public Task ReadAsync(Func<IFdbReadOnlyTransaction, Task> asyncHandler, CancellationToken cancellationToken = default(CancellationToken))
		{
			return FdbOperationContext.RunReadAsync(this, asyncHandler, null, cancellationToken);
		}

		public Task ReadAsync(Func<IFdbReadOnlyTransaction, Task> asyncHandler, Action<IFdbReadOnlyTransaction> onDone, CancellationToken cancellationToken = default(CancellationToken))
		{
			return FdbOperationContext.RunReadAsync(this, asyncHandler, onDone, cancellationToken);
		}

		/// <summary>Runs a transactional lambda function against this database, inside a read-only transaction context, with retry logic.</summary>
		/// <param name="asyncHandler">Asynchronous lambda function that is passed a new read-only transaction on each retry. The result of the task will also be the result of the transactional.</param>
		/// <param name="cancellationToken">Optional cancellation token that will be passed to the transaction context, and that can also be used to abort the retry loop.</param>
		public Task<R> ReadAsync<R>(Func<IFdbReadOnlyTransaction, Task<R>> asyncHandler, CancellationToken cancellationToken = default(CancellationToken))
		{
			return FdbOperationContext.RunReadWithResultAsync<R>(this, asyncHandler, null, cancellationToken);
		}

		public Task<R> ReadAsync<R>(Func<IFdbReadOnlyTransaction, Task<R>> asyncHandler, Action<IFdbReadOnlyTransaction> onDone, CancellationToken cancellationToken = default(CancellationToken))
		{
			return FdbOperationContext.RunReadWithResultAsync<R>(this, asyncHandler, onDone, cancellationToken);
		}

		#endregion

		#region IFdbTransactional methods...

		/// <summary>Runs a transactional lambda function against this database, inside a write-only transaction context, with retry logic.</summary>
		/// <param name="handler">Lambda function that is passed a new read-write transaction on each retry. It should only call non-async methods, such as Set, Clear or any atomic operation.</param>
		/// <param name="cancellationToken">Optional cancellation token that will be passed to the transaction context, and that can also be used to abort the retry loop.</param>
		public Task WriteAsync(Action<IFdbTransaction> handler, CancellationToken cancellationToken = default(CancellationToken))
		{
			return FdbOperationContext.RunWriteAsync(this, handler, null, cancellationToken);
		}

		public Task WriteAsync(Action<IFdbTransaction> handler, Action<IFdbTransaction> onDone, CancellationToken cancellationToken = default(CancellationToken))
		{
			return FdbOperationContext.RunWriteAsync(this, handler, onDone, cancellationToken);
		}

		/// <summary>Runs a transactional lambda function against this database, inside a read-write transaction context, with retry logic.</summary>
		/// <param name="asyncHandler">Asynchronous lambda function that is passed a new read-write transaction on each retry.</param>
		/// <param name="cancellationToken">Optional cancellation token that will be passed to the transaction context, and that can also be used to abort the retry loop.</param>
		public Task ReadWriteAsync(Func<IFdbTransaction, Task> asyncHandler, CancellationToken cancellationToken = default(CancellationToken))
		{
			return FdbOperationContext.RunWriteAsync(this, asyncHandler, null, cancellationToken);
		}

		public Task ReadWriteAsync(Func<IFdbTransaction, Task> asyncHandler, Action<IFdbTransaction> onDone, CancellationToken cancellationToken = default(CancellationToken))
		{
			return FdbOperationContext.RunWriteAsync(this, asyncHandler, onDone, cancellationToken);
		}

		/// <summary>Runs a transactional lambda function against this database, inside a read-write transaction context, with retry logic.</summary>
		/// <param name="asyncHandler">Asynchronous lambda function that is passed a new read-write transaction on each retry. The result of the task will also be the result of the transactional.</param>
		/// <param name="cancellationToken">Optional cancellation token that will be passed to the transaction context, and that can also be used to abort the retry loop.</param>
		public Task<R> ReadWriteAsync<R>(Func<IFdbTransaction, Task<R>> asyncHandler, CancellationToken cancellationToken = default(CancellationToken))
		{
			return FdbOperationContext.RunWriteWithResultAsync<R>(this, asyncHandler, null, cancellationToken);
		}

		public Task<R> ReadWriteAsync<R>(Func<IFdbTransaction, Task<R>> asyncHandler, Action<IFdbTransaction> onDone, CancellationToken cancellationToken = default(CancellationToken))
		{
			return FdbOperationContext.RunWriteWithResultAsync<R>(this, asyncHandler, onDone, cancellationToken);
		}

		#endregion

		#endregion

		public void Dispose()
		{
			if (!m_disposed)
			{
				m_disposed = true;
				try { m_cts.Cancel(); } catch {}
				m_cts.Dispose();
			}
		}

		private void ThrowDisposed()
		{
			throw new ObjectDisposedException("The database has already been disposed");
		}

		public void Debug_Dump(bool detailed = false)
		{
			Trace.WriteLine("Dumping content of Database " + this.Name);
			m_dataLock.EnterReadLock();
			try
			{
				Trace.WriteLine("> Version: " + m_version);
				Trace.WriteLine("> Estimated size: " + m_estimatedSize.ToString("N0") + " bytes");
				//Trace.WriteLine("> Content: {0} keys", m_data.Count.ToString("N0"));

				Trace.WriteLine(String.Format("> Keys: {0} bytes in {1} pages", m_keyHeap.MemoryUsage.ToString("N0"), m_keyHeap.Pages.ToString("N0")));
				Trace.WriteLine(String.Format("> Values: {0} bytes in {1} pages", m_valueHeap.MemoryUsage.ToString("N0"), m_valueHeap.Pages.ToString("N0")));
				lock (m_heapLock)
				{
					if (detailed)
					{
						m_keyHeap.Dump(detailed: false);
						m_valueHeap.Dump(detailed: false);
					}
					m_keyHeap.DumpToDisk("keys.bin");
					m_valueHeap.DumpToDisk("values.bin");
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
		private unsafe struct Key
		{
			public static readonly uint SizeOf = (uint)Marshal.OffsetOf(typeof(Key), "Data").ToInt32();

			/// <summary>This jey has been moved to another page by the last GC</summary>
			public const uint FLAGS_MOVED = 0x100;
			/// <summary>This key has been flaged as being unreachable by current of future transaction (won't survive the next GC)</summary>
			public const uint FLAGS_UNREACHABLE = 0x2000;
			/// <summary>This key is being moved to another page by the current GC</summary>
			public const uint FLAGS_MOVING = 0x400;

			public const uint FLAGS_DISPOSED = 0x8000;

			/// <summary>Size of the key (in bytes)</summary>
			public uint Size;
			/// <summary>Various flags (TODO: enum?)</summary>
			public uint Flags;
			/// <summary>Pointer to the head of the value chain for this key (should not be null)</summary>
			public Value* Values;
			/// <summary>Offset to the first byte of the key</summary>
			public byte Data;

			/// <summary>Rewire the Values pointer after a successfull moved during a GC</summary>
			public static void RewireValuesPointerAfterGC(Key* self)
			{
				Contract.Requires(self != null && self->Values != null);

				// the value pointed to by Values has been moved
				Value* old = self->Values;
				if (old == null)
				{ // ?
					Contract.Assert((self->Flags & Key.FLAGS_DISPOSED) != 0);
				}
				else if ((old->Flags & Value.FLAGS_DISPOSED) != 0)
				{ // the value has been deleted, this key should also be marked as dead
					self->Values = null;
				}
				else
				{
					self->Values = old->MovedTo;
					old->Flags |= Value.FLAGS_UNREACHABLE;
				}
			}

			public static USlice GetData(Key* key)
			{
				if (key == null) return default(USlice);

				Contract.Assert((key->Flags & FLAGS_DISPOSED) == 0, "Attempt to read a value that was disposed");
				return new USlice(&(key->Data), key->Size);
			}

		}

		[StructLayout(LayoutKind.Sequential, Pack = 1)]
		private unsafe struct Value
		{
			public static readonly uint SizeOf = (uint)Marshal.OffsetOf(typeof(Value), "Data").ToInt32();

			/// <summary>This value is a deletion marker</summary>
			public const uint FLAGS_DELETION = 0x1;
			/// <summary>This value has been mutated and is not up to date</summary>
			public const uint FLAGS_MUTATED = 0x2;

			/// <summary>This value has been moved to another page by the last GC</summary>
			public const uint FLAGS_MOVED = 0x0100;
			/// <summary>This value has been flagged as unreachable by any current of future transaction (won't survive the next GC)</summary>
			public const uint FLAGS_UNREACHABLE = 0x0200;
			/// <summary>This value is being moved to another page by the current GC</summary>
			public const uint FLAGS_MOVING = 0x0400;

			/// <summary>This value is dead, and should not be linked to anymore</summary>
			public const uint FLAGS_DISPOSED = 0x8000;

			/// <summary>Version were this version of the key first appeared</summary>
			public ulong Sequence;
			/// <summary>Pointer to the previous version of this key, or NULL if this is the earliest known</summary>
			public Value* Previous;
			/// <summary>Various flags (TDB)</summary>
			public uint Flags;
			/// <summary>Size of the value</summary>
			public uint Size;
			/// <summary>Pointer to the new location of this value, after a garbage collection</summary>
			public Value* MovedTo;
			/// <summary>Offset to the first byte of the value</summary>
			public byte Data;

			/// <summary>Rewire the Previous pointer after a successfull moved during a GC</summary>
			public static void RewirePreviousPointerAfterGC(Value* self)
			{
				Contract.Requires(self != null && self->Previous != null);

				// the value pointed to by Previous has been moved
				Value* old = self->Previous;

				if ((old->Flags & FLAGS_DISPOSED) != 0)
				{ // the previous version is dead!
					self->Previous = null;
				}
				else
				{
					self->Previous = old->MovedTo;
					old->Flags |= FLAGS_UNREACHABLE;
				}

				Thread.MemoryBarrier();
			}

			public static USlice GetData(Value* value)
			{
				if (value == null) return default(USlice);

				Contract.Assert((value->Flags & FLAGS_DISPOSED) == 0, "Attempt to read a value that was disposed");
				return new USlice(&(value->Data), value->Size);
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
				// if both pointers are the same, then they are equal (hopefully :) )
				if (left == right) return 0;
				// handle the nulls
				if (left == IntPtr.Zero) return -1;
				if (right == IntPtr.Zero) return +1;

				// unwrap as pointers to the Key struct
				var leftKey = (Key*)left.ToPointer();
				var rightKey = (Key*)right.ToPointer();

				Contract.Assert(leftKey->Size != 0 && rightKey->Size != 0);

				return UnmanagedHelpers.CompareUnsafe(
					&(leftKey->Data), leftKey->Size,
					&(rightKey->Data), rightKey->Size
				);
			}
		}

	}

}
