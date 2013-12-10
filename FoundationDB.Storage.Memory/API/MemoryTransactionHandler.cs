#region Copyright (c) 2013-2014, Doxense SAS. All rights reserved.
// See License.MD for license information
#endregion

#undef DUMP_TRANSACTION_STATE

namespace FoundationDB.Storage.Memory.API
{
	using FoundationDB.Client;
	using FoundationDB.Client.Core;
	using FoundationDB.Client.Utils;
	using FoundationDB.Storage.Memory.Core;
	using System;
	using System.Diagnostics;
	using System.Diagnostics.Contracts;
	using System.Globalization;
	using System.Linq;
	using System.Runtime.InteropServices;
	using System.Threading;
	using System.Threading.Tasks;

	public class MemoryTransactionHandler : IFdbTransactionHandler, IDisposable
	{

		#region Private Fields...

		private readonly MemoryDatabaseHandler m_db;

		private volatile bool m_disposed;

		/// <summary>Buffer used to store the keys and values of this transaction</summary>
		private SliceBuffer m_buffer;

		/// <summary>Lock that protects the state of the transaction</summary>
		private readonly object m_lock = new object();
		/// <summary>List of all conflicts due to read operations</summary>
		private ColaRangeSet<Slice> m_readConflicts;
		/// <summary>List of all conflicts due to write operations</summary>
		private ColaRangeSet<Slice> m_writeConflicts;
		/// <summary>List of all ClearRange</summary>
		private ColaRangeSet<Slice> m_clears;
		/// <summary>List of all Set operations (Set, Atomic, ..)</summary>
		private ColaOrderedDictionary<Slice, WriteCommand[]> m_writes;

		/// <summary>Read version of the transaction</summary>
		private long? m_readVersion;
		/// <summary>Committed version of the transaction</summary>
		private long m_committedVersion;

		#endregion

		internal enum Operation
		{
			Nop = 0,

			Set = 1,
			//note: the AtomicXXX should match the value of FdbMutationType
			AtomicAdd = 2,
			AtomicBitAnd = 6,
			AtomicBitOr = 7,
			AtomicBitXor = 8,
		}

		[StructLayout(LayoutKind.Sequential)]
		internal struct WriteCommand
		{
			public readonly Slice Key;
			public readonly Slice Value;
			public readonly Operation Type;

			public WriteCommand(Operation type, Slice key, Slice value)
			{
				this.Type = type;
				this.Key = key;
				this.Value = value;
			}

			public override string ToString()
			{
				return String.Format(CultureInfo.InvariantCulture, "{0}({1}, {2}))", this.Type.ToString(), this.Key.ToAsciiOrHexaString(), this.Value.ToAsciiOrHexaString());
			}

			internal static byte[] PrepareValueForAtomicOperation(Slice value, int size)
			{
				if (value.Count >= size)
				{ // truncate if needed
					return value.GetBytes(0, size);
				}

				// pad with zeroes
				var tmp = new byte[size];
				value.CopyTo(tmp, 0);
				return tmp;
			}

			public Slice ApplyAdd(Slice value)
			{
				var tmp = PrepareValueForAtomicOperation(value, this.Value.Count);
				BufferAdd(tmp, 0, this.Value.Array, this.Value.Offset, this.Value.Count);
				return Slice.Create(tmp);
			}

			public void ApplyAddTo(UnmanagedSliceBuilder value)
			{
				uint size = checked((uint)this.Value.Count);

				// if the value is empty, then this is the same thing as adding to 0
				if (value.Count == 0)
				{
					value.Append(this.Value);
					return;
				}

				// truncate the value if larger, or pad it with zeroes if shorter
				value.Resize(size, 0);

				if (size > 0)
				{
					unsafe
					{
						fixed (byte* ptr = this.Value.Array)
						{
							byte* left = value.Data;
							byte* right = ptr + this.Value.Offset;

							//TODO: find a way to optimize this for common sizes like 4 or 8 bytes!
							int carry = 0;
							while (size-- > 0)
							{
								carry += *left + *right++;
								*left++ = (byte)carry;
								carry >>= 8;
							}
						}
					}
				}
			}

			public Slice ApplyBitAnd(Slice value)
			{
				var tmp = PrepareValueForAtomicOperation(value, this.Value.Count);
				BufferBitAnd(tmp, 0, this.Value.Array, this.Value.Offset, this.Value.Count);
				return Slice.Create(tmp);
			}

			public void ApplyBitAndTo(UnmanagedSliceBuilder value)
			{
				uint size = checked((uint)this.Value.Count);

				// if the value is empty, then 0 AND * will always be zero
				if (value.Count == 0)
				{
					value.Resize(size, 0);
					return;
				}

				// truncate the value if larger, or pad it with zeroes if shorter
				value.Resize(size, 0);

				if (size > 0)
				{
					unsafe
					{
						fixed (byte* ptr = this.Value.Array)
						{
							byte* left = value.Data;
							byte* right = ptr + this.Value.Offset;

							//TODO: find a way to optimize this for common sizes like 4 or 8 bytes!
							while (size-- > 0)
							{
								*left++ &= *right++;
							}
						}
					}
				}
			}

			public Slice ApplyBitOr(Slice value)
			{
				var tmp = PrepareValueForAtomicOperation(value, this.Value.Count);
				BufferBitOr(tmp, 0, this.Value.Array, this.Value.Offset, this.Value.Count);
				return Slice.Create(tmp);
			}

			public void ApplyBitOrTo(UnmanagedSliceBuilder value)
			{
				uint size = checked((uint)this.Value.Count);

				// truncate the value if larger, or pad it with zeroes if shorter
				value.Resize(size, 0);

				if (size > 0)
				{
					unsafe
					{
						fixed (byte* ptr = this.Value.Array)
						{
							byte* left = value.Data;
							byte* right = ptr + this.Value.Offset;

							//TODO: find a way to optimize this for common sizes like 4 or 8 bytes!
							while (size-- > 0)
							{
								*left++ |= *right++;
							}
						}
					}
				}
			}

			public Slice ApplyBitXor(Slice value)
			{
				var tmp = PrepareValueForAtomicOperation(value, this.Value.Count);
				BufferBitXor(tmp, 0, this.Value.Array, this.Value.Offset, this.Value.Count);
				return Slice.Create(tmp);
			}

			public void ApplyBitXorTo(UnmanagedSliceBuilder value)
			{
				uint size = checked((uint)this.Value.Count);

				// truncate the value if larger, or pad it with zeroes if shorter
				value.Resize(size, 0);

				if (size > 0)
				{
					unsafe
					{
						fixed (byte* ptr = this.Value.Array)
						{
							byte* left = value.Data;
							byte* right = ptr + this.Value.Offset;

							//TODO: find a way to optimize this for common sizes like 4 or 8 bytes!
							while (size-- > 0)
							{
								*left++ ^= *right++;
							}
						}
					}
				}
			}

			internal static int BufferAdd(byte[] buffer, int offset, byte[] arg, int argOffset, int count)
			{
				// TODO: optimize this!
				int carry = 0;
				while (count-- > 0)
				{
					carry += buffer[offset] + arg[argOffset++];
					buffer[offset++] = (byte)carry;
					carry >>= 8;
				}
				return carry;
			}

			internal static void BufferBitAnd(byte[] buffer, int offset, byte[] arg, int argOffset, int count)
			{
				while (count-- > 0)
				{
					buffer[offset++] &= arg[argOffset++];
				}
			}

			internal static void BufferBitOr(byte[] buffer, int offset, byte[] arg, int argOffset, int count)
			{
				while (count-- > 0)
				{
					buffer[offset++] |= arg[argOffset++];
				}
			}

			internal static void BufferBitXor(byte[] buffer, int offset, byte[] arg, int argOffset, int count)
			{
				while (count-- > 0)
				{
					buffer[offset++] ^= arg[argOffset++];
				}
			}

			internal static WriteCommand MergeSetAndAtomicOperation(WriteCommand command, Operation op, Slice argument)
			{
				// truncate/resize the previous value to the size of the add
				int size = argument.Count;
				var tmp = PrepareValueForAtomicOperation(command.Value, size);

				switch (op)
				{
					case Operation.AtomicAdd:
					{ // do a littlee-endian ADD between the two buffers
						BufferAdd(tmp, 0, argument.Array, argument.Offset, size);
						break;
					}
					case Operation.AtomicBitAnd:
					{ // do an AND between the two buffers
						BufferBitAnd(tmp, 0, argument.Array, argument.Offset, size);
						break;
					}
					case Operation.AtomicBitOr:
					{ // do a OR between the two buffers
						BufferBitOr(tmp, 0, argument.Array, argument.Offset, size);
						break;
					}
					case Operation.AtomicBitXor:
					{ // do a XOR between the two buffers
						BufferBitXor(tmp, 0, argument.Array, argument.Offset, size);
						break;
					}
					default:
					{ // not supposed to happen
						throw new InvalidOperationException();
					}
				}

				return new WriteCommand(Operation.Set, command.Key, Slice.Create(tmp));
			}

			internal static WriteCommand MergeTwoAtomicOperations(WriteCommand command, Slice argument)
			{
				// truncate/resize the previous value to the size of the add
				int size = argument.Count;
				var tmp = PrepareValueForAtomicOperation(command.Value, size);

				switch (command.Type)
				{
					case Operation.AtomicAdd:
					{ // do a littlee-endian ADD between the two buffers
						BufferAdd(tmp, 0, argument.Array, argument.Offset, size);
						break;
					}
					case Operation.AtomicBitAnd:
					{ // do an AND between the two buffers
						BufferBitAnd(tmp, 0, argument.Array, argument.Offset, size);
						break;
					}
					case Operation.AtomicBitOr:
					{ // do a OR between the two buffers
						BufferBitOr(tmp, 0, argument.Array, argument.Offset, size);
						break;
					}
					case Operation.AtomicBitXor:
					{ // do a XOR between the two buffers
						BufferBitXor(tmp, 0, argument.Array, argument.Offset, size);
						break;
					}
					default:
					{ // not supposed to happen
						throw new InvalidOperationException();
					}
				}

				return new WriteCommand(command.Type, command.Key, Slice.Create(tmp));
			}

		}

		internal MemoryTransactionHandler(MemoryDatabaseHandler db)
		{
			Contract.Assert(db != null);

			m_db = db;

			Initialize(first: true);
		}

		public bool IsInvalid { get { return false; } }

		public bool IsClosed { get { return m_disposed; } }

		private void Initialize(bool first)
		{
			if (m_disposed) ThrowDisposed();

			lock(m_lock)
			{
				m_buffer = new SliceBuffer();
				if (first)
				{
					m_clears = new ColaRangeSet<Slice>(SliceComparer.Default);
					m_writes = new ColaOrderedDictionary<Slice, WriteCommand[]>(SliceComparer.Default);
					m_readConflicts = new ColaRangeSet<Slice>(SliceComparer.Default);
					m_writeConflicts = new ColaRangeSet<Slice>(SliceComparer.Default);
				}
				else
				{
					m_clears.Clear();
					m_writes.Clear();
					m_readConflicts.Clear();
					m_writeConflicts.Clear();
				}

				this.AccessSystemKeys = false;
			}
		}
		private static void ThrowDisposed()
		{
			throw new ObjectDisposedException("This transaction has already been disposed."); ;
		}

		public int Size
		{
			get { return m_buffer.Size; }
		}

		private void AddClearCommand_NedsLocking(FdbKeyRange range)
		{
			// merge the cleared range with the others
			m_clears.Mark(range.Begin, range.End);

			// remove all writes that where in this range
			var keys = m_writes.FindBetween(range.Begin, true, range.End, false).ToList();
			if (keys.Count > 0)
			{
				foreach(var key in keys)
				{
					m_writes.Remove(key);
				}
			}
		}

		private void AddWriteCommand_NeedsLocking(WriteCommand command)
		{
			var commands = new WriteCommand[1];
			commands[0] = command;

			if (!m_writes.GetOrAdd(command.Key, commands, out commands))
			{ // there is already a command for that key

				if (command.Type == Operation.Set)
				{ // Set always overwrites everything
					if (commands.Length == 1)
					{ // reuse the command array
						commands[0] = command;
						return;
					}
					// overwrite 
					m_writes.SetItem(command.Key, new[] { command });
					return;
				}

				var last = commands[commands.Length - 1];
				if (last.Type == Operation.Set)
				{ // "SET(X) x ATOMIC(op, P)" are merged into "SET(X')" with X' = atomic(op, X, P)
					Contract.Assert(commands.Length == 1);

					command = WriteCommand.MergeSetAndAtomicOperation(last, command.Type, command.Value);
					// update in place
					commands[commands.Length - 1] = command;
					return;

				}

				if (last.Type == command.Type)
				{ // atomics of the same kind can be merged

					command = WriteCommand.MergeTwoAtomicOperations(last, command.Value);
					// update in place
					commands[commands.Length - 1] = command;
					return;
				}

				// just queue the command at the end
				Array.Resize<WriteCommand>(ref commands, commands.Length + 1);
				commands[commands.Length - 1] = command;

				m_writes.SetItem(command.Key, commands);

			}
		}

		private void AddWriteConflict_NeedsLocking(FdbKeyRange range)
		{
			m_writeConflicts.Mark(range.Begin, range.End);
		}

		private void AddReadConflict_NeedsLocking(FdbKeyRange range)
		{
			m_readConflicts.Mark(range.Begin, range.End);
		}

		private void CheckAccessToSystemKeys(Slice key, bool end = false)
		{
			if (!this.AccessSystemKeys && key[0] == 0xFF)
			{ // access to system keys is not allowed
				if (!end || key.Count > 1)
				{
					throw new FdbException(FdbError.KeyOutsideLegalRange);
				}
			}
		}

		public async Task<Slice> GetAsync(Slice key, bool snapshot, CancellationToken cancellationToken)
		{
			Contract.Requires(key.HasValue);
			cancellationToken.ThrowIfCancellationRequested();

			CheckAccessToSystemKeys(key);

			FdbKeyRange range;
			lock (m_buffer)
			{
				range = m_buffer.InternRangeFromKey(key);
			}

			// we need the read version
			EnsureHasReadVersion();

			var result = await m_db.GetValueAtVersionAsync(range.Begin, m_readVersion.Value).ConfigureAwait(false);

			if (!snapshot)
			{
				lock (m_lock)
				{
					AddReadConflict_NeedsLocking(range);
				}
			}
			return result;
		}

		public async Task<Slice[]> GetValuesAsync(Slice[] keys, bool snapshot, CancellationToken cancellationToken)
		{
			Contract.Requires(keys != null);
			cancellationToken.ThrowIfCancellationRequested();

			// order and check the keys
			var ordered = new Slice[keys.Length];
			for (int i = 0; i < keys.Length;i++)
			{
				var key = keys[i];
				if (key.IsNullOrEmpty) throw new ArgumentException("Key cannot be null or empty");
				CheckAccessToSystemKeys(key);
				ordered[i] = key;
			}
			//Array.Sort(ordered, SliceComparer.Default);

			// we need the read version
			EnsureHasReadVersion();

			FdbKeyRange[] ranges = new FdbKeyRange[ordered.Length];
			lock (m_buffer)
			{
				for (int i = 0; i < ordered.Length; i++)
				{
					ranges[i] = m_buffer.InternRangeFromKey(ordered[i]);
					ordered[i] = ranges[i].Begin;
				}
			}

			var results = await m_db.GetValuesAtVersionAsync(ordered, m_readVersion.Value).ConfigureAwait(false);

			if (!snapshot)
			{
				lock (m_lock)
				{
					for (int i = 0; i < ranges.Length; i++)
					{
						AddReadConflict_NeedsLocking(ranges[i]);
					}
				}
			}

			return results;
		}

		public async Task<Slice> GetKeyAsync(FdbKeySelector selector, bool snapshot, CancellationToken cancellationToken)
		{
			Contract.Requires(selector.Key.HasValue);
			cancellationToken.ThrowIfCancellationRequested();

			CheckAccessToSystemKeys(selector.Key, end: true);

			Trace.WriteLine("## GetKey " + selector + ", snapshot=" + snapshot);

			FdbKeyRange keyRange;
			lock (m_buffer)
			{
				keyRange = m_buffer.InternRangeFromKey(selector.Key);
				selector = new FdbKeySelector(keyRange.Begin, selector.OrEqual, selector.Offset);
			}

			// we need the read version
			EnsureHasReadVersion();

			var result = await m_db.GetKeyAtVersion(selector, m_readVersion.Value).ConfigureAwait(false);

			int c = result.CompareTo(selector.Key);

			FdbKeyRange resultRange;
			if (c == 0)
			{ // the result is identical to the key
				resultRange = keyRange;
				result = keyRange.Begin;
			}
			else
			{ // intern the result
				lock(m_buffer)
				{
					resultRange = m_buffer.InternRangeFromKey(result);
					result = resultRange.Begin;
				}
			}

			if (!snapshot)
			{
				lock (m_lock)
				{
					//TODO: use the result to create the conflict range (between the resolver key and the returned key)
					if (c == 0)
					{ // the key itself was selected, so it can only conflict if it gets deleted by another transaction
						// [ result, result+\0 )
						AddReadConflict_NeedsLocking(resultRange);
					}
					else if (c < 0)
					{ // the result is before the selected key, so any change between them (including deletion of the result) will conflict
						// orEqual == true  => [ result, key + \0 )
						// orEqual == false => [ result, key )
						AddReadConflict_NeedsLocking(FdbKeyRange.Create(resultRange.Begin, selector.OrEqual ? keyRange.End : keyRange.Begin));
					}
					else
					{ // the result is after the selected key, so any change between it and the result will conflict
						// orEqual == true  => [ key + \0, result + \0 )
						// orEqual == false => [ key , result + \0 )
						AddReadConflict_NeedsLocking(FdbKeyRange.Create(selector.OrEqual ? keyRange.End : keyRange.Begin, resultRange.End));
					}
				}
			}
			return result;
		}

		public async Task<Slice[]> GetKeysAsync(FdbKeySelector[] selectors, bool snapshot, CancellationToken cancellationToken)
		{
			Contract.Requires(selectors != null);

			cancellationToken.ThrowIfCancellationRequested();

			// order and check the keys
			var ordered = new FdbKeySelector[selectors.Length];
			for (int i = 0; i < selectors.Length; i++)
			{
				if (selectors[i].Key.IsNullOrEmpty) throw new ArgumentException("Key cannot be null or empty");
				//CheckAccessToSystemKeys(key);
				ordered[i] = selectors[i];
			}
			//Array.Sort(ordered, SliceComparer.Default);

			// we need the read version
			EnsureHasReadVersion();

			//FdbKeyRange[] ranges = new FdbKeyRange[ordered.Length];
			lock (m_buffer)
			{
				for (int i = 0; i < ordered.Length; i++)
				{
					ordered[i] = m_buffer.InternSelector(ordered[i]);
				}
			}

			var results = await m_db.GetKeysAtVersion(ordered, m_readVersion.Value).ConfigureAwait(false);

			if (!snapshot)
			{
				lock (m_lock)
				{
					//TODO!
				}
			}

			return results;
		}

		public async Task<FdbRangeChunk> GetRangeAsync(FdbKeySelector beginInclusive, FdbKeySelector endExclusive, FdbRangeOptions options, int iteration, bool snapshot, CancellationToken cancellationToken)
		{
			Contract.Requires(beginInclusive.Key.HasValue && endExclusive.Key.HasValue && options != null);

			cancellationToken.ThrowIfCancellationRequested();

			//TODO: check system keys

			Trace.WriteLine("## GetRange " + beginInclusive + " <= k < " + endExclusive + ", limit=" + options.Limit + ", reverse=" + options.Reverse + ", snapshot=" + snapshot);

			lock (m_buffer)
			{
				beginInclusive = m_buffer.InternSelector(beginInclusive);
				endExclusive = m_buffer.InternSelector(endExclusive);
			}

			// we need the read version
			EnsureHasReadVersion();

			var result = await m_db.GetRangeAtVersion(beginInclusive, endExclusive, options.Limit.Value, options.TargetBytes.Value, options.Mode.Value, iteration, options.Reverse.Value, m_readVersion.Value).ConfigureAwait(false);

			if (!snapshot)
			{
				lock (m_lock)
				{
					//TODO: use the result to create the conflict range (between the resolver key and the returned key)
					//AddReadConflict_NeedsLocking(range);
				}
			}
			return result;
		}

		public void Set(Slice key, Slice value)
		{
			// check
			if (key.IsNullOrEmpty) throw new ArgumentException("Key cannot be null or empty");
			if (value.IsNull) throw new ArgumentNullException("Value cannot be null");
			CheckAccessToSystemKeys(key);

			// first thing is copy the data in our own buffer, and only use those for the rest
			FdbKeyRange range;
			lock (m_buffer)
			{
				range = m_buffer.InternRangeFromKey(key);
				value = m_buffer.Intern(value);
			}

			lock (m_lock)
			{
				AddWriteConflict_NeedsLocking(range);
				AddWriteCommand_NeedsLocking(new WriteCommand(Operation.Set, range.Begin, value));
			}
		}

		public void Atomic(Slice key, Slice param, FdbMutationType mutation)
		{
			// check
			if (key.IsNullOrEmpty) throw new ArgumentException("Key cannot be null or empty");
			if (param.IsNull) throw new ArgumentNullException("Parameter cannot be null");
			CheckAccessToSystemKeys(key);

			if (mutation != FdbMutationType.Add && mutation != FdbMutationType.And && mutation != FdbMutationType.Or && mutation != FdbMutationType.Xor)
			{
				//TODO: throw an FdbException instead?
				throw new ArgumentException("Invalid mutation type", "mutation");
			}

			FdbKeyRange range;
			lock (m_buffer)
			{
				range = m_buffer.InternRangeFromKey(key);
				param = m_buffer.Intern(param);
			}

			lock (m_lock)
			{
				AddWriteConflict_NeedsLocking(range);
				AddWriteCommand_NeedsLocking(new WriteCommand((Operation)mutation, range.Begin, param));
			}
		}

		public void Clear(Slice key)
		{
			// check
			if (key.IsNullOrEmpty) throw new ArgumentException("Key cannot be null or empty");
			CheckAccessToSystemKeys(key);

			FdbKeyRange range;
			lock (m_buffer)
			{
				range = m_buffer.InternRangeFromKey(key);
			}

			lock (m_lock)
			{
				AddWriteConflict_NeedsLocking(range);
				AddClearCommand_NedsLocking(range);
			}
		}

		public void ClearRange(Slice beginKeyInclusive, Slice endKeyExclusive)
		{
			// check
			if (beginKeyInclusive.IsNullOrEmpty) throw new ArgumentException("Begin key cannot be null or empty");
			if (endKeyExclusive.IsNullOrEmpty) throw new ArgumentException("End key cannot be null or empty");
			CheckAccessToSystemKeys(beginKeyInclusive);
			CheckAccessToSystemKeys(endKeyExclusive, end: true);

			FdbKeyRange range;
			lock (m_buffer)
			{
				range = m_buffer.InternRange(beginKeyInclusive, endKeyExclusive);
			}

			lock (m_lock)
			{
				AddWriteConflict_NeedsLocking(range);
				AddClearCommand_NedsLocking(range);
			}
		}

		public void AddConflictRange(Slice beginKeyInclusive, Slice endKeyExclusive, FdbConflictRangeType type)
		{
			// check
			if (beginKeyInclusive.IsNullOrEmpty) throw new ArgumentException("Begin key cannot be null or empty");
			if (endKeyExclusive.IsNullOrEmpty) throw new ArgumentException("End key cannot be null or empty");
			if (type != FdbConflictRangeType.Read && type != FdbConflictRangeType.Write) throw new ArgumentOutOfRangeException("type", "Invalid range conflict type");

			CheckAccessToSystemKeys(beginKeyInclusive);
			CheckAccessToSystemKeys(endKeyExclusive, end: true);

			FdbKeyRange range;
			lock(m_buffer)
			{
				range = m_buffer.InternRange(beginKeyInclusive, endKeyExclusive);
			}

			lock (m_lock)
			{
				switch (type)
				{
					case FdbConflictRangeType.Read:
					{
						AddReadConflict_NeedsLocking(range);
						break;
					}
					case FdbConflictRangeType.Write:
					{
						AddWriteConflict_NeedsLocking(range);
						break;
					}
				}
			}
		}

		public void Reset()
		{
			Initialize(true);
		}

		public async Task CommitAsync(CancellationToken cancellationToken)
		{
			cancellationToken.ThrowIfCancellationRequested();

			if (!m_readVersion.HasValue)
			{
				EnsureHasReadVersion();
			}

#if DUMP_TRANSACTION_STATE
			Trace.WriteLine(String.Format(CultureInfo.InvariantCulture, "=== COMMITING TRANSACTION {0} ===", this.Id));
			Trace.WriteLine(String.Format(CultureInfo.InvariantCulture, "# ReadVersion: {0}", m_readVersion ?? -1));

			if (m_readConflicts.Count == 0)
			{
				Trace.WriteLine("# Read  Conflicts: none");
			}
			else
			{
				Trace.WriteLine(String.Format(CultureInfo.InvariantCulture, "# Read  Conflicts: ({0}) => {1}", m_readConflicts.Count, m_readConflicts.ToString()));
			}

			if (m_writeConflicts.Count == 0)
			{
				Trace.WriteLine("# Write Conflicts: none");
			}
			else
			{
				Trace.WriteLine(String.Format(CultureInfo.InvariantCulture, "# Write Conflicts: ({0}) => {1}", m_writeConflicts.Count, m_writeConflicts.ToString()));
			}

			if (m_clears.Count == 0)
			{
				Trace.WriteLine("# Clears: none");
			}
			else
			{
				Trace.WriteLine(String.Format(CultureInfo.InvariantCulture, "# Clears: ({0})", m_clears.Count));
				foreach (var op in m_clears)
				{
					Trace.WriteLine("  > " + new FdbKeyRange(op.Begin, op.End));
				}
			}

			if (m_writes.Count == 0)
			{
				Trace.WriteLine("# Writes: none");
			}
			else
			{
				Trace.WriteLine(String.Format(CultureInfo.InvariantCulture, "# Writes: ({0})", m_writes.Count));
				foreach (var op in m_writes)
				{
					Trace.WriteLine("  > " + String.Join("; ", op.Value));
				}
			}

			var pages = m_buffer.GetPages();
			Trace.WriteLine(String.Format(CultureInfo.InvariantCulture, "# Slice buffer: {0} bytes in {1} pages ({2} allocated, {3:##0.00}% wasted)", m_buffer.Size, pages.Length, m_buffer.Allocated, 100.0 - (m_buffer.Size * 100.0 / m_buffer.Allocated)));
			foreach(var page in pages)
			{
				Trace.WriteLine("  > " + page.ToString());
			}
#endif

			m_committedVersion = await m_db.CommitTransactionAsync(this, m_readConflicts, m_writeConflicts, m_clears, m_writes).ConfigureAwait(false);
#if DUMP_TRANSACTION_STATE
			Trace.WriteLine("=== DONE with commit version " + m_committedVersion);
#endif
		}

		public long GetCommittedVersion()
		{
			return m_committedVersion;
		}

		public void SetReadVersion(long version)
		{
			throw new NotImplementedException();
		}

		public async Task OnErrorAsync(FdbError code, CancellationToken cancellationToken)
		{
			cancellationToken.ThrowIfCancellationRequested();

			switch (code)
			{
				case FdbError.TimedOut:
				case FdbError.PastVersion:
				{ // wait a bit

					//HACKHACK: implement a real back-off delay logic
					await Task.Delay(15, cancellationToken).ConfigureAwait(false);
					return;
				}
				default:
				{
					throw new FdbException(code);
				}
			}
		}

		public FdbWatch Watch(Slice key, System.Threading.CancellationToken cancellationToken)
		{
			Contract.Requires(key.HasValue);
			cancellationToken.ThrowIfCancellationRequested();

			throw new NotSupportedException();
		}

		public Task<string[]> GetAddressesForKeyAsync(Slice key, CancellationToken cancellationToken)
		{
			if (cancellationToken.IsCancellationRequested) return TaskHelpers.FromCancellation<string[]>(cancellationToken);

			throw new NotImplementedException();
		}

		private long EnsureHasReadVersion()
		{
			if (!m_readVersion.HasValue)
			{
				m_readVersion = m_db.GetCurrentVersion();
			}
			return m_readVersion.Value;
		}

		public Task<long> GetReadVersionAsync(CancellationToken cancellationToken)
		{
			if (cancellationToken.IsCancellationRequested) return TaskHelpers.FromCancellation<long>(cancellationToken);
			return Task.FromResult(EnsureHasReadVersion());
		}

		public void Cancel()
		{
			if (m_disposed) ThrowDisposed();
			throw new NotImplementedException();
		}

		public int RetryLimit { get; set; }

		public int Timeout { get; set; }

		public bool AccessSystemKeys { get; set; }

		public void SetOption(FdbTransactionOption option, Slice data)
		{
			switch(option)
			{
				case FdbTransactionOption.AccessSystemKeys:
				{
					if (data.IsNullOrEmpty)
					{
						this.AccessSystemKeys = true;
					}
					else
					{
						if (data.Count == 8)
						{ // spec says that ints should be passed as 8 bytes integers
							this.AccessSystemKeys = data.ToInt64() != 0;
						}
						else
						{
							this.AccessSystemKeys = data.ToBool();
						}
					}
					break;
				}
				case FdbTransactionOption.RetryLimit:
				{
					if (data.Count != 8) throw new FdbException(FdbError.InvalidOptionValue);
					long value = data.ToInt64();
					if (value < 0 || value >= int.MaxValue) throw new FdbException(FdbError.InvalidOptionValue);
					this.RetryLimit = (int)value;
					break;
				}

				case FdbTransactionOption.Timeout:
				{
					if (data.Count != 8) throw new FdbException(FdbError.InvalidOptionValue);
					long value = data.ToInt64();
					if (value < 0 || value >= int.MaxValue) throw new FdbException(FdbError.InvalidOptionValue);
					this.Timeout = (int)value;
					break;
				}
				default:
				{
					throw new FdbException(FdbError.InvalidOption);
				}
			}
		}

		public void Dispose()
		{
			if (m_disposed)
			{
				//TODO: locking ?
				m_disposed = true;

				//TODO!
				m_buffer = null;
				m_readConflicts = null;
				m_writeConflicts = null;
				m_clears = null;
				m_writes = null;
			}

			GC.SuppressFinalize(this);
		}
	}

}
