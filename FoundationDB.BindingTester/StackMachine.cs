
// ReSharper disable MethodHasAsyncOverload
namespace FoundationDB.Client.Testing
{
	using System;
	using System.Collections.Generic;
	using System.IO;
	using System.Linq;
	using System.Threading.Tasks;
	using System.Threading;
	using Doxense.Collections.Tuples;
	using Doxense.Collections.Tuples.Encoding;
	using Doxense.Diagnostics.Contracts;
	using Microsoft.Extensions.Logging;
	using Microsoft.Extensions.Logging.Abstractions;

	public enum InstructionCode
	{
		Invalid = 0,

		// data operations
		Push,
		Dup,
		EmptyStack,
		Swap,
		Pop,
		Sub,
		Concat,
		LogStack,

		// foundationdb operations
		NewTransaction,
		UseTransaction,
		OnError,
		Get,
		GetKey,
		GetRange,
		GetReadVersion,
		GetVersionstamp,
		Set,
		SetReadVersion,
		Clear,
		ClearRange,
		AtomicOp,
		ReadConflictRange,
		WriteConflictRange,
		ReadConflictKey,
		WriteConflictKey,
		DisableWriteConflict,
		Commit,
		Reset,
		Cancel,
		GetCommittedVersion,
		GetApproximateSize,
		WaitFuture,
		GetEstimatedRangeSize,
		GetRangeSplitPoints,

		TuplePack,
		TuplePackWithVersionstamp,
		TupleUnpack,
		TupleRange,
		TupleSort,
		EncodeFloat,
		EncodeDouble,
		DecodeFloat,
		DecodeDouble,

		// Thread Operations
		StartThread,
		WaitEmpty,

		// misc
		UnitTests,

		// Directory/Subspace/Layer Creation
		DirectoryCreateSubspace,
		DirectoryCreateLayer,
		DirectoryCreateOrOpen,
		DirectoryCreate,
		DirectoryOpen,

		// Directory Management
		DirectoryChange,
		DirectorySetErrorIndex,

		// Directory Operations
		DirectoryMove,
		DirectoryMoveTo,
		DirectoryRemove,
		DirectoryRemoveIfExists,
		DirectoryList,
		DirectoryExists,

		// Subspace operation
		DirectoryPackKey,
		DirectoryUnpackKey,
		DirectoryRange,
		DirectoryContains,
		DirectoryOpenSubspace,

		// Directory Logging
		DirectoryLogSubspace,
		DirectoryLogDirectory,

		// Other
		DirectoryStripPrefix,

		// Tenants
		TenantCreate,
		TenantDelete,
		TenantSetActive,
		TenantClearActive,
		TenantList,
	}

	[Flags]
	public enum InstructionFlags
	{
		None = 0,
		Database = 1 << 0,
		Snapshot = 1 << 1,
		Tenant = 1 << 2,
		StartsWith = 1 << 3,
		Selector = 1 << 4,
	}

	public sealed record TransactionState : IDisposable
	{

		public required IFdbTransaction Transaction { get; init; }

		public FdbTenant? Tenant { get; init; }

		public bool Dead { get; private set; }

		public void Dispose()
		{
			this.Dead = true;
			this.Transaction.Dispose();
		}

	}

	public sealed record StackFuture
	{
		public (Slice, TransactionState) State { get; init; }

		public Slice Data { get; init; }
	}

	public sealed class StackMachine : IDisposable
	{

		public StackMachine(IFdbDatabase db, Slice prefix, ILogger<StackMachine>? log)
		{
			this.Db = db;
			this.Log = log ?? NullLogger<StackMachine>.Instance;
			this.Prefix = prefix;
			this.TransactionName = prefix.ToStringUtf8();
		}
		
		public IFdbDatabase Db { get; }

		private ILogger<StackMachine>? Log { get; }

		public FdbTenant? Tenant { get; private set; }

		internal ContextStack<StackItem> Stack { get; } = new();

		public Slice Prefix { get; }

		public string? TransactionName { get; private set; }

		public Dictionary<string, TransactionState> Transactions { get; } = new(StringComparer.Ordinal);

		public long LastVersion { get; private set; }

		public long DirectoryIndex { get; set; }

		public long ErrorIndex { get; set; }

		private IFdbTransaction CreateNewTransaction(CancellationToken ct)
		{
			var tr = this.Tenant != null ? this.Tenant.BeginTransaction(ct) : this.Db.BeginTransaction(ct);
			tr.Options.WithTransactionLog("CSHARP"); //REVIEW: add the name and/or counter?
			return tr;
		}

		private IFdbTransaction GetCurrentTransaction()
		{
			var name = this.TransactionName;
			if (string.IsNullOrEmpty(name)) throw new InvalidOperationException("No transaction name specified");
			if (!this.Transactions.TryGetValue(name, out var state)) throw new InvalidOperationException($"No transaction with name '{name}'");
			return state.Transaction;
		}

		private void Push(TestInstruction instr, IVarTuple arg, int index)
		{
			this.Stack.Push(new(instr, arg, index));
		}

		private void Push<T>(TestInstruction instr, T value, int index)
		{
			this.Stack.Push(new(instr, STuple.Create(value), index));
		}

		private void PushFuture<TState>(TestInstruction instr, TState state, Func<TState, Task<IVarTuple>> handler, int index)
		{
			PushFuture(instr, handler(state), index);
		}

		private void PushFuture(TestInstruction instr, Task<IVarTuple> future, int index)
		{
			this.Stack.Push(new(instr, future, index));
		}

		private async ValueTask<T> PopAsync<T>()
		{
			var item = await this.Stack.Pop().Resolve().ConfigureAwait(false);
			return item.As<T>()!;
		}

		private ValueTask<StackResult> PopAsync()
		{
			return this.Stack.Pop().Resolve();
		}

		private ValueTask<StackResult> PeekAsync()
		{
			return this.Stack.Peek().Resolve();
		}

		private async Task<IVarTuple> MapVoidFuture(Task t)
		{
			await t.ConfigureAwait(false);
			return STuple.Create("RESULT_NOT_PRESENT");
		}

		public async Task RunTest(CancellationToken ct)
		{
			var db = this.Db;

			this.Stack.Clear();

			var instructions = await db.ReadAsync(
				tr => tr
					.GetRangeValues(KeyRange.StartsWith(this.Prefix))
					.Select((v) => TestInstruction.Parse(v))
					.ToListAsync(),
				ct);

			int index = 0;
			foreach (var instr in instructions)
			{
				await Execute(instr, index, ct);
				++index;
			}
		}

		public async Task Run(TestSuite suite, CancellationToken ct)
		{
			var instructions = suite.Instructions;
			this.Log?.LogInformation($"Execute Test '{suite.Name}' with {instructions.Length:N0} instructions");
			for (int i = 0; i < instructions.Length; i++)
			{
				await Execute(instructions[i], i, ct);
				//Dump();
			}
			this.Log?.LogInformation("Test executed");
		}

		public async Task Execute(TestInstruction instr, int index, CancellationToken ct)
		{
			this.Log?.LogDebug($"> @{index}: {instr}");

			switch (instr.Code)
			{
				case InstructionCode.Push:
				{ // PUSH <item>
					if (instr.Args == null) throw new InvalidOperationException("Missing argument for PUSH command");
					Push(instr, instr.Args, index);
					break;
				}
				case InstructionCode.Dup:
				{ // DUP
					this.Stack.Dup();
					break;
				}
				case InstructionCode.EmptyStack:
				{ // EMPTY_STACK
					this.Stack.Clear();
					break;
				}
				case InstructionCode.Swap:
				{ // SWAP
					var depth = await PopAsync<int>();
					this.Stack.Swap(0, depth);
					break;
				}
				case InstructionCode.Pop:
				{ // POP
					this.Stack.Pop();
					break;
				}
				case InstructionCode.Sub:
				{ // SUB
					var a = await PopAsync<long>();
					var b = await PopAsync<long>();
					var r = a - b;
					Push(instr, r, 0);
					break;
				}
				case InstructionCode.Concat:
				{ // CONCAT
					var a = await PopAsync();
					var b = await PopAsync();
					switch (a.Value[0], b.Value[0])
					{
						case (string strA, string strB):
						{
							var r = strA + strB;
							Push(instr, r, 0);
							break;
						}
						case (Slice sliceA, Slice sliceB):
						{
							var r = sliceA + sliceB;
							Push(instr, r, 0);
							break;
						}
						default:
						{
							throw new InvalidOperationException("Can only CONCAT byte or unicode strings");
						}
					}
					break;
				}
				case InstructionCode.LogStack:
				{ // LOG_STACK

					var prefix = await PopAsync<Slice>();

					var results = this.Stack.ToArray();
					var items = new List<StackResult>(results.Length);
					foreach (var result in results)
					{
						items.Add(await result.Resolve());
					}

					var kvs = items.Select((item, i) => KeyValuePair.Create(prefix + TuPack.EncodeKey(i, item.Index), TuPack.Pack(item.Value).Truncate(40_000))).ToArray();

					if (true) //HACKHACK
					{
						for(int i = 0; i < kvs.Length; i++)
						{
							var (k, v) = kvs[i];
							this.Log?.LogDebug($"- {TuPack.Unpack(k)} = {TuPack.Unpack(v)}");
						}
					}
					else
					{
						await this.Db.WriteAsync((tr) => tr.SetValues(kvs), ct);
					}

					this.Stack.Clear();
					break;
				}
				case InstructionCode.NewTransaction:
				{ // NEW_TRANSACTION

					var name = this.TransactionName;
					Contract.Debug.Assert(!string.IsNullOrWhiteSpace(name));

					if (this.Transactions.TryGetValue(name, out var state))
					{
						state.Dispose();
					}

					state = new TransactionState()
					{
						Transaction = CreateNewTransaction(ct),
						Tenant = this.Tenant,
					};

					this.Transactions[name] = state;
					break;
				}
				case InstructionCode.UseTransaction:
				{ // USE_TRANSACTION

					var name = await PopAsync<string>();
					if (string.IsNullOrEmpty(name)) throw new InvalidOperationException("No transaction name specified for USE_TRANSACTION");

					if (!this.Transactions.TryGetValue(name, out var state))
					{
						state = new TransactionState()
						{
							Transaction = CreateNewTransaction(ct),
							Tenant = this.Tenant,
						};

						this.Transactions[name] = state;
					}
					this.TransactionName = name;
					break;
				}
				case InstructionCode.GetReadVersion:
				{
					PushFuture(
						instr,
						GetCurrentTransaction(),
						async (tr) =>
						{
							var rv = await tr.GetReadVersionAsync();
							this.LastVersion = rv;
							return STuple.Create("GOT_READ_VERSION");
						},
						index);
					break;
				}
				case InstructionCode.Reset:
				{ // RESET
					var tr = GetCurrentTransaction();

					tr.Reset();
					break;
				}
				case InstructionCode.Commit:
				{
					var tr = GetCurrentTransaction();

					var t = tr.CommitAsync();
					PushFuture(instr, MapVoidFuture(t), index);
					break;
				}
				case InstructionCode.Cancel:
				{
					var tr = GetCurrentTransaction();

					tr.Cancel();
					break;
				}
				case InstructionCode.OnError:
				{ // ON_ERROR
					throw new NotImplementedException();
				}

				case InstructionCode.Get:
				{ // GET, GET_SNAPSHOT, GET_DATABASE
					var key = await PopAsync<Slice>();

					Task<Slice> future;
					if (instr.IsDatabase())
					{
						future = this.Db.ReadAsync(tr => instr.IsSnapshot() ? tr.Snapshot.GetAsync(key) : tr.GetAsync(key), ct);
					}
					else
					{
						var tr = GetCurrentTransaction();
						future = instr.IsSnapshot()
							? tr.Snapshot.GetAsync(key)
							: tr.GetAsync(key);
					}

					PushFuture(instr, future, async (t) =>
					{
						var res = await t.ConfigureAwait(false);
						if (res.IsNull) return STuple.Create("RESULT_NOT_PRESENT");
						return STuple.Create(res);
					}, index);
					break;
				}
				case InstructionCode.GetRange:
				{
					KeySelector begin;
					KeySelector end;
					int limit;
					bool reverse;
					FdbStreamingMode streamingMode;
					if (instr.IsSelector())
					{
						var beginKey = await PopAsync<Slice>();
						var beginOrEqual = await PopAsync<bool>();
						var beginOffset = await PopAsync<int>();
						begin = new KeySelector(beginKey, beginOrEqual, beginOffset);
						var endKey = await PopAsync<Slice>();
						var endOrEqual = await PopAsync<bool>();
						var endOffset = await PopAsync<int>();
						end = new KeySelector(endKey, endOrEqual, endOffset);
						limit = await PopAsync<int>();
						reverse = await PopAsync<bool>();
						streamingMode = (FdbStreamingMode) await PopAsync<int>();
					}
					else if (instr.IsStartsWith())
					{
						var prefix = await PopAsync<Slice>();
						(begin, end) = KeySelectorPair.StartsWith(prefix);
						limit = await PopAsync<int>();
						reverse = await PopAsync<bool>();
						streamingMode = (FdbStreamingMode) await PopAsync<int>();
					}
					else
					{
						var beginKey = await PopAsync<Slice>();
						begin = KeySelector.FirstGreaterOrEqual(beginKey);
						var endKey = await PopAsync<Slice>();
						end = KeySelector.FirstGreaterOrEqual(endKey);
						limit = await PopAsync<int>();
						reverse = await PopAsync<bool>();
						streamingMode = (FdbStreamingMode) await PopAsync<int>();
					}

					Task<FdbRangeChunk> future;
					if (instr.IsDatabase())
					{
						future = this.Db.ReadAsync(tr => instr.IsSnapshot() ? tr.Snapshot.GetRangeAsync(begin, end, limit: limit, reverse: reverse, mode: streamingMode) : tr.GetRangeAsync(begin, end, limit: limit, reverse: reverse, mode: streamingMode), ct);
					}
					else
					{
						var tr = GetCurrentTransaction();
						future = instr.IsSnapshot()
							? tr.Snapshot.GetRangeAsync(begin, end, limit: limit, reverse: reverse, mode: streamingMode)
							: tr.GetRangeAsync(begin, end, limit: limit, reverse: reverse, mode: streamingMode);
					}

					PushFuture(instr, future, async (t) =>
					{
						var chunk = await t;
						var res = STuple.Empty;
						foreach (var (k, v) in chunk.Items)
						{
							res = res.Append(k);
							res = res.Append(v);
						}
						return res;
					}, index);

					break;
				}

				case InstructionCode.Set:
				{
					var key = await PopAsync<Slice>();
					var value = await PopAsync<Slice>();

					if (instr.IsDatabase())
					{
						var res = this.Db.WriteAsync(tr => tr.Set(key, value), ct);
						PushFuture(instr, MapVoidFuture(res), index);
					}
					else
					{
						var tr = GetCurrentTransaction();
						tr.Set(key, value);
					}
					break;
				}
				case InstructionCode.Clear:
				{
					var key = await PopAsync<Slice>();

					if (instr.IsDatabase())
					{
						var res = this.Db.WriteAsync(tr => tr.Clear(key), ct);
						PushFuture(instr, MapVoidFuture(res), index);
					}
					else
					{
						var tr = GetCurrentTransaction();
						tr.Clear(key);
					}
					break;
				}
				case InstructionCode.ClearRange:
				{
					KeyRange range;
					if (instr.IsStartsWith())
					{
						var prefix = await PopAsync<Slice>();
						range = KeyRange.StartsWith(prefix);
					}
					else
					{
						var beginKey = await PopAsync<Slice>();
						var endKey = await PopAsync<Slice>();
						range = new KeyRange(beginKey, endKey);
					}

					if (instr.IsDatabase())
					{
						var res = this.Db.WriteAsync(tr => tr.ClearRange(range), ct);
						PushFuture(instr, MapVoidFuture(res), index);
					}
					else
					{
						var tr = GetCurrentTransaction();
						tr.ClearRange(range);
					}
					break;
				}

				case InstructionCode.EncodeFloat:
				{
					var bytes = await PopAsync<Slice>();
					var x = bytes.ToSingleBE();
					Push(instr, x, index);
					break;
				}
				case InstructionCode.DecodeFloat:
				{
					var x = await PopAsync<float>();
					var bytes = Slice.FromSingleBE(x);
					Push(instr, bytes, index);
					break;
				}

				case InstructionCode.EncodeDouble:
				{
					var bytes = await PopAsync<Slice>();
					var x = bytes.ToDoubleBE();
					Push(instr, x, index);
					break;
				}
				case InstructionCode.DecodeDouble:
				{
					var x = await PopAsync<double>();
					var bytes = Slice.FromDoubleBE(x);
					Push(instr, bytes, index);
					break;
				}

				case InstructionCode.WaitFuture:
				{
					var item = this.Stack.Peek();
					if (!item.IsCompleted)
					{
						await item.Resolve();
					}
					break;
				}
				default:
				{
					throw new NotSupportedException($"Unsupported operation {instr.Code}");
				}
			}
		}

		public void Dump()
		{
			var log = this.Log;
			if (log == null) return;

			log.LogInformation($"Stack: [{this.Stack.Count}]");
			int p = 0;
			foreach (var item in this.Stack.ToArray())
			{
				if (item.TryResolve(out var res))
				{
					log.LogInformation($"  [{p}] {res.Value}, @{res.Index}");
				}
				else
				{
					log.LogInformation($"  [{p}] FUTURE<{item.Future.AsTask().Status}>, @{item.Index}");
				}
				++p;
			}
		}

		public void Dispose()
		{
			// TODO release managed resources here
		}

	}

}
