
namespace FoundationDB.Client.Testing
{
	using System;
	using System.Runtime.CompilerServices;
	using Doxense.Collections.Tuples;
	using Doxense.Collections.Tuples.Encoding;
	using Doxense.Diagnostics.Contracts;

	public sealed record TestInstruction
	{

		public InstructionCode Code { get; init; }

		public InstructionFlags Flags { get; init; }

		public IVarTuple Args { get; init; }

		public string Command { get; init; }

		public T? GetValue<T>() => this.Args.Count > 0 ? this.Args.Get<T>(0) : throw new InvalidOperationException($"Operation {this.Command} did not provide any argument");

		public TestInstruction(InstructionCode code, string cmd, IVarTuple? args, InstructionFlags flags)
		{
			this.Code = code;
			this.Flags = flags;
			this.Command = cmd;
			this.Args = args ?? STuple.Empty;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool IsDatabase() => this.Flags.HasFlag(InstructionFlags.Database);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool IsSnapshot() => this.Flags.HasFlag(InstructionFlags.Snapshot);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool IsTenant() => this.Flags.HasFlag(InstructionFlags.Tenant);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool IsStartsWith() => this.Flags.HasFlag(InstructionFlags.StartsWith);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool IsSelector() => this.Flags.HasFlag(InstructionFlags.Selector);

		public override string ToString() => this.Args.Count > 0 ? $"{this.Command} {STuple.Formatter.Stringify(this.Args[0])}" : this.Command;

		private static void ParseOption(ref string input, ref InstructionFlags flags, string suffix, InstructionFlags flag)
		{
			if (input.Contains(suffix))
			{
				flags |= flag;
				input = input.Replace(suffix, "");
			}
		}

		public static TestInstruction Parse(Slice literal)
		{
			return Parse(SlicedTuple.Unpack(literal));
		}

		public static TestInstruction Parse(IVarTuple tup)
		{
			Contract.Debug.Requires(tup != null && tup.Count > 0);

			var cmd = tup.Get<string>(0)!;
			var flags = InstructionFlags.None; //TODO!
			var input = cmd;
			ParseOption(ref input, ref flags, "_DATABASE", InstructionFlags.Database);
			ParseOption(ref input, ref flags, "_TENANT", InstructionFlags.Tenant);
			ParseOption(ref input, ref flags, "_SNAPSHOT", InstructionFlags.Snapshot);
			ParseOption(ref input, ref flags, "_STARTS_WITH", InstructionFlags.StartsWith);
			ParseOption(ref input, ref flags, "_SELECTOR", InstructionFlags.Selector);

			var code = input switch
			{
				"PUSH" => InstructionCode.Push,
				"DUP" => InstructionCode.Dup,
				"EMPTY_STACK" => InstructionCode.EmptyStack,
				"SWAP" => InstructionCode.Swap,
				"POP" => InstructionCode.Pop,
				"SUB" => InstructionCode.Sub,
				"CONCAT" => InstructionCode.Concat,
				"LOG_STACK" => InstructionCode.LogStack,

				"NEW_TRANSACTION" => InstructionCode.NewTransaction,
				"USE_TRANSACTION" => InstructionCode.UseTransaction,
				"ON_ERROR" => InstructionCode.OnError,
				"GET" => InstructionCode.Get,
				"GET_KEY" => InstructionCode.GetKey,
				"GET_RANGE" => InstructionCode.GetRange,
				"GET_READ_VERSION" => InstructionCode.GetReadVersion,
				"GET_VERSIONSTAMP" => InstructionCode.GetVersionstamp,

				"SET" => InstructionCode.Set,
				"SET_READ_VERSION" => InstructionCode.SetReadVersion,
				"CLEAR" => InstructionCode.Clear,
				"CLEAR_RANGE" => InstructionCode.ClearRange,
				"ATOMIC_OP" => InstructionCode.AtomicOp,
				"READ_CONFLICT_RANGE" => InstructionCode.ReadConflictRange,
				"WRITE_CONFLICT_RANGE" => InstructionCode.WriteConflictRange,
				"READ_CONFLICT_KEY" => InstructionCode.ReadConflictKey,
				"WRITE_CONFLICT_KEY" => InstructionCode.WriteConflictKey,
				"DISABLE_WRITE_CONFLICT" => InstructionCode.DisableWriteConflict,
				"COMMIT" => InstructionCode.Commit,
				"RESET" => InstructionCode.Reset,
				"CANCEL" => InstructionCode.Cancel,
				"GET_COMMITTED_VERSION" => InstructionCode.GetCommittedVersion,
				"GET_APPROXIMATE_SIZE" => InstructionCode.GetApproximateSize,
				"WAIT_FUTURE" => InstructionCode.WaitFuture,
				"GET_ESTIMATED_RANGE_SIZE" => InstructionCode.GetEstimatedRangeSize,
				"GET_RANGE_SPLIT_POINTS" => InstructionCode.GetRangeSplitPoints,

				"TUPLE_PACK" => InstructionCode.TuplePack,
				"TUPLE_PACK_WITH_VERSIONSTAMP" => InstructionCode.TuplePackWithVersionstamp,
				"TUPLE_UNPACK" => InstructionCode.TupleUnpack,
				"TUPLE_RANGE" => InstructionCode.TupleRange,
				"TUPLE_SORT" => InstructionCode.TupleSort,
				"ENCODE_FLOAT" => InstructionCode.EncodeFloat,
				"ENCODE_DOUBLE" => InstructionCode.EncodeDouble,
				"DECODE_FLOAT" => InstructionCode.DecodeFloat,
				"DECODE_DOUBLE" => InstructionCode.DecodeDouble,

				"START_THREAD" => InstructionCode.StartThread,
				"WAIT_EMPTY" => InstructionCode.WaitEmpty,

				"UNIT_TESTS" => InstructionCode.UnitTests,

				"DIRECTORY_CREATE_SUBSPACE" => InstructionCode.DirectoryCreateSubspace,
				"DIRECTORY_CREATE_LAYER" => InstructionCode.DirectoryCreateLayer,
				"DIRECTORY_CREATE_OR_OPEN" => InstructionCode.DirectoryCreateOrOpen,
				"DIRECTORY_CREATE" => InstructionCode.DirectoryCreate,
				"DIRECTORY_OPEN" => InstructionCode.DirectoryOpen,

				"DIRECTORY_CHANGE" => InstructionCode.DirectoryChange,
				"DIRECTORY_SET_ERROR_INDEX" => InstructionCode.DirectorySetErrorIndex,

				"DIRECTORY_MOVE" => InstructionCode.DirectoryMove,
				"DIRECTORY_MOVE_TO" => InstructionCode.DirectoryMoveTo,
				"DIRECTORY_REMOVE" => InstructionCode.DirectoryRemove,
				"DIRECTORY_REMOVE_IF_EXISTS" => InstructionCode.DirectoryRemoveIfExists,
				"DIRECTORY_LIST" => InstructionCode.DirectoryList,
				"DIRECTORY_EXISTS" => InstructionCode.DirectoryExists,

				"DIRECTORY_PACK_KEY" => InstructionCode.DirectoryPackKey,
				"DIRECTORY_UNPACK_KEY" => InstructionCode.DirectoryUnpackKey,
				"DIRECTORY_RANGE" => InstructionCode.DirectoryRange,
				"DIRECTORY_CONTAINS" => InstructionCode.DirectoryContains,
				"DIRECTORY_OPEN_SUBSPACE" => InstructionCode.DirectoryOpenSubspace,

				"DIRECTORY_LOG_SUBSPACE" => InstructionCode.DirectoryLogSubspace,
				"DIRECTORY_LOG_DIRECTORY" => InstructionCode.DirectoryLogDirectory,

				"DIRECTORY_STRIP_PREFIX" => InstructionCode.DirectoryStripPrefix,

				"TENANT_CREATE" => InstructionCode.TenantCreate,
				"TENANT_DELETE" => InstructionCode.TenantDelete,
				"TENANT_SET_ACTIVE" => InstructionCode.TenantSetActive,
				"TENANT_CLEAR_ACTIVE" => InstructionCode.TenantClearActive,
				"TENANT_LIST" => InstructionCode.TenantList,

				_ => throw new NotSupportedException($"Unsupported instruction: `{input}`"),
			};

			return new TestInstruction(code, cmd, tup.Count > 1 ? tup[1..] : SlicedTuple.Empty, flags);
		}

		public static FdbMutationType ParseMutationType(string input)
		{
			return input switch
			{
				"ADD" => FdbMutationType.Add,
				"AND" => FdbMutationType.BitAnd,
				"BIT_AND" => FdbMutationType.BitAnd,
				"OR" => FdbMutationType.BitOr,
				"BIT_OR" => FdbMutationType.BitOr,
				"XOR" => FdbMutationType.BitXor,
				"BIT_XOR" => FdbMutationType.BitXor,
				"APPEND_IF_FITS" => FdbMutationType.AppendIfFits,
				"MAX" => FdbMutationType.Max,
				"MIN" => FdbMutationType.Min,
				"SET_VERSIONSTAMPED_KEY" => FdbMutationType.VersionStampedKey,
				"SET_VERSIONSTAMPED_VALUE" => FdbMutationType.VersionStampedValue,
				"BYTE_MIN" => FdbMutationType.ByteMin,
				"BYTE_MAX" => FdbMutationType.ByteMax,
				"COMPARE_AND_CLEAR" => FdbMutationType.CompareAndClear,
				_ => throw new NotSupportedException($"Invalid mutation type: {input}"),
			};
		}

		public static TestInstruction Push<T>(T item) => new(InstructionCode.Push, "PUSH", STuple.Create(item), InstructionFlags.None);

		public static TestInstruction EmptyStack() => new(InstructionCode.EmptyStack, "EMPTY_STACK", null, InstructionFlags.None);

		public static TestInstruction Pop() => new(InstructionCode.Pop, "POP", null, InstructionFlags.None);

		public static TestInstruction Dup() => new(InstructionCode.Dup, "DUP", null, InstructionFlags.None);

		public static TestInstruction Swap() => new(InstructionCode.Swap, "SWAP", null, InstructionFlags.None);

		public static TestInstruction Sub() => new(InstructionCode.Sub, "SUB", null, InstructionFlags.None);

		public static TestInstruction Concat() => new(InstructionCode.Concat, "CONCAT", null, InstructionFlags.None);

		public static TestInstruction LogStack() => new(InstructionCode.LogStack, "LOG_STACK", null, InstructionFlags.None);

		public static TestInstruction NewTransaction() => new(InstructionCode.NewTransaction, "NEW_TRANSACTION", null, InstructionFlags.None);

		public static TestInstruction UseTransaction() => new(InstructionCode.UseTransaction, "USE_TRANSACTION", null, InstructionFlags.None);

		public static TestInstruction Get() => new(InstructionCode.Get, "GET", null, InstructionFlags.None);
		public static TestInstruction GetDatabase() => new(InstructionCode.Get, "GET_DATABASE", null, InstructionFlags.Database);
		public static TestInstruction GetSnapshot() => new(InstructionCode.Get, "GET_SNAPSHOT", null, InstructionFlags.Snapshot);

		public static TestInstruction Set() => new(InstructionCode.Set, "SET", null, InstructionFlags.None);
		public static TestInstruction SetDatabase() => new(InstructionCode.Set, "SET_DATABASE", null, InstructionFlags.Database);

		public static TestInstruction Clear() => new(InstructionCode.Clear, "CLEAR", null, InstructionFlags.None);
		public static TestInstruction ClearDatabase() => new(InstructionCode.Clear, "CLEAR_DATABASE", null, InstructionFlags.Database);

	}
}
