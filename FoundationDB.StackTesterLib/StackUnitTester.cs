using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using FoundationDB.Client;
using FoundationDB.Layers.Tuples;

using FoundationDB.StackTester.Utils;

namespace FoundationDB.StackTester.Utils
{
	public static class EncodingHelper
	{
		public static readonly Encoding ASCII_8_BIT = Encoding.GetEncoding(28591);

		public static string ToByteString(this Slice slice)
		{
			return ASCII_8_BIT.GetString(slice.Array, slice.Offset, slice.Count);
		}

		public static Slice FromByteString(string text)
		{
			return text == null ? Slice.Nil : text.Length == 0 ? Slice.Empty : Slice.Create(ASCII_8_BIT.GetBytes(text));
		}
	}
}

namespace FoundationDB.StackTester
{
	public class StackEntry
	{
		public int instructionIndex;
		public Object value;

		public StackEntry(int instructionIndex, Object value)
		{
			this.instructionIndex = instructionIndex;
			this.value = value;
		}
	}

	[TestClass]
	public class StackUnitTester
	{
		private FdbDatabase db;
		private FdbTransaction transaction;
		private string prefix;

		private long lastVersion = 0;
		private long committedVersion = -1;

		private List<KeyValuePair<Slice, Slice>> instructions;
		private List<StackEntry> stack = new List<StackEntry>();
		private List<Task> subTasks = new List<Task>();

		private static Random random = new Random(0);
		private static string[] mutationOperations = { "SET", "CLEAR", "CLEAR_RANGE", "CLEAR_RANGE_STARTS_WITH", "ATOMIC_OP" };

		public StackUnitTester(FdbDatabase db, string prefix)
		{
			this.db = db;
			this.prefix = prefix;
		}

		public StackUnitTester(string prefix, string clusterFile)
		{
			Fdb.Options.SetTracePath(".");
			Fdb.Start();
			this.db = Task.Run(() => Fdb.OpenAsync(clusterFile, "DB")).GetAwaiter().GetResult();
			this.prefix = prefix;
		}

		public StackUnitTester() : this("test_spec", null) { }

		[TestMethod]
		public void RunTest()
		{
			Task.Run(async () => {
				await RunTestAsync();
				foreach (Task subTask in subTasks)
					await subTask;
			}).GetAwaiter().GetResult();
		}

		static string Printable(string val)
		{
			return new string(val.SelectMany((b) =>
			{
				if (b >= 32 && b < 127 && b != '\\')
					return new char[] { b };
				else if (b == '\\')
					return "\\\\".ToArray();
				else
					return string.Format("\\x{0:X2}", (byte)b).ToArray();
			}).ToArray());
		}

		static string Printable(Slice val)
		{
			return Printable(val.ToByteString());
		}

		string StrInc(string val)
		{
			char[] chars = val.ToCharArray();
			for (int i = chars.Length - 1; i >= 0; i--) 
			{
				chars[i] = (char)(((int)chars[i] + 1) % 256);
				if (chars[i] != 0)
					return new string(chars);
			}

			chars = new char[chars.Length + 1];

			for (int i = 0; i < chars.Length; i++)
				chars[i] = (char)0xff;

			chars[chars.Length - 1] = (char)0x00;

			return new string(chars);
		}

		Slice StrInc(Slice val)
		{
			return EncodingHelper.FromByteString(StrInc(val.ToByteString()));
		}

		Slice stringToSlice(object str)
		{
			if (str is Slice)
				return (Slice)str;
			else
				return EncodingHelper.FromByteString((string)str);
		}

		private static void HandleFdbError(FdbException e, int instructionIndex, List<StackEntry> items) 
		{
			//Console.WriteLine("FdbError (" + (int)e.Code + "): " + e.Message);
			items.Add(new StackEntry(instructionIndex, FdbTuple.Pack(EncodingHelper.ASCII_8_BIT.GetBytes("ERROR"), EncodingHelper.ASCII_8_BIT.GetBytes(((int)e.Code).ToString()))));
		}

		private void AddSlice(int instructionIndex, Slice slice, List<StackEntry> items = null)
		{
			if (items == null)
				items = stack;

			if (!slice.HasValue)
				items.Add(new StackEntry(instructionIndex, EncodingHelper.FromByteString("RESULT_NOT_PRESENT")));
			else
				items.Add(new StackEntry(instructionIndex, slice));
		}

		private async Task<List<Object>> PopAsync(int num, bool includeMetadata = false)
		{
			List<StackEntry> items = new List<StackEntry>();
			for (int i = 0; i < num; ++i)
			{
				var item = stack.Last();
				try
				{
					if (item.value is Task<Slice>)
						AddSlice(item.instructionIndex, await (Task<Slice>)item.value, items);
					else if (item.value is Task)
					{
						await (Task)item.value;
						items.Add(new StackEntry(item.instructionIndex, EncodingHelper.FromByteString("RESULT_NOT_PRESENT")));
					}
					else
						items.Add(item);
				}
				catch (FdbException e)
				{
					HandleFdbError(e, item.instructionIndex, items);
				}
				catch (System.OperationCanceledException)
				{
					// TODO: Getting the bindings to throw a TransactionCancelled error instead of OperationCanceledException in the case handled here would be better
					HandleFdbError(new FdbException(FdbError.TransactionCancelled), item.instructionIndex, items);
				}

				stack.RemoveAt(stack.Count() - 1);
			}

			if (includeMetadata)
				return items.ToList<Object>();
			else
				return items.Select((item) => item.value).ToList();
		}

		private async Task PushRangeAsync(int instructionIndex, FdbRangeQuery query)
		{
			IFdbTuple tuple;

			if (random.NextDouble() < 0.5)
			{
				tuple = FdbTuple.Empty;
				await query.ForEachAsync((r) => {
					tuple = tuple.Append(r.Key);
					tuple = tuple.Append(r.Value);
				});
			}
			else
			{
				var results = await query.ToListAsync();
				tuple = FdbTuple.FromEnumerable(results.SelectMany((r) => new object[] { r.Key, r.Value }));
			}

			stack.Add(new StackEntry(instructionIndex, tuple.ToSlice()));
		}

		private static async Task IgnoreCancelled(Func<Task> f)
		{
			try
			{
				await f();
			}
			catch (FdbException e)
			{
				// Cancellation in C# is a little different than in the other bindings, so we sometimes have to ignore this error
				if (e.Code != FdbError.TransactionCancelled)
					throw e;
			}
		}

		private async Task<FdbTransaction> CommitAndReset(FdbTransaction tr)
		{
			try
			{
				await ((FdbTransaction)tr).CommitAsync();
				committedVersion = tr.GetCommittedVersion();
			}
			finally
			{
				this.transaction = db.BeginTransaction();
				tr = this.transaction;
			}

			return tr;
		}

		public async Task RunTestAsync()
		{
			try
			{
				await db.Attempt.ReadAsync(async (tr) => instructions = await tr.GetRangeStartsWith(FdbTuple.Create(EncodingHelper.FromByteString(prefix))).ToListAsync());

				for(int instructionIndex = 0; instructionIndex < instructions.Count; ++instructionIndex)
				{
					IFdbTuple inst = FdbTuple.Unpack(instructions[instructionIndex].Value);
					string op = inst.Get<string>(0);

					//if (op != "PUSH" && op != "SWAP")
						//Console.WriteLine(op);

					if (op == "PUSH")
					{
						stack.Add(new StackEntry(instructionIndex, inst[1]));
						continue;
					}

					IFdbReadTransaction tr = this.transaction;

					bool useDb = op.EndsWith("_DATABASE");
					if (useDb)
					{
						op = op.Substring(0, op.Length - 9);
						try
						{
							if (mutationOperations.Contains(op))
							{
								if (random.NextDouble() < 0.5)
									await db.Attempt.ChangeAsync((t) => ProcessInstruction(t, op, instructionIndex, useDb = true));
								else
									await db.Attempt.Change(async (t) => await ProcessInstruction(t, op, instructionIndex, useDb = true));

								stack.Add(new StackEntry(instructionIndex, EncodingHelper.FromByteString("RESULT_NOT_PRESENT")));
							}
							else
							{
								await db.Attempt.ReadAsync((t) => ProcessInstruction(t, op, instructionIndex, useDb = true));
							}
						}
						catch (FdbException e)
						{
							HandleFdbError(e, instructionIndex, stack);
						}
					}
					else
					{
						bool snapshot = op.EndsWith("_SNAPSHOT");
						if (snapshot)
						{
							tr = this.transaction.Snapshot;
							op = op.Substring(0, op.Length - 9);
						}

						await ProcessInstruction(tr, op, instructionIndex);
					}
				}
			}
			catch (Exception e)
			{
				Console.WriteLine("Error: " + e.Message);
				throw;
			}
		}

		public async Task ProcessInstruction(IFdbReadTransaction tr, string op, int instructionIndex, bool useDb = false)
		{
			try
			{
				if (op == "DUP")
					stack.Add(stack.Last());
				else if (op == "EMPTY_STACK")
					stack.Clear();
				else if (op == "SWAP")
				{
					var items = await PopAsync(1);
					int index = (int)(long)items[0];
					index = stack.Count() - index - 1;
					if (stack.Count() > index + 1)
					{
						var tmp = stack[index];
						stack[index] = stack.Last();
						stack[stack.Count - 1] = tmp;
					}
				}
				else if (op == "POP")
					await PopAsync(1);
				else if (op == "SUB")
				{
					var items = await PopAsync(2);
					stack.Add(new StackEntry(instructionIndex, (long)items[0] - (long)items[1]));
				}
				else if (op == "WAIT_FUTURE")
					stack.Add((StackEntry)(await PopAsync(1, true))[0]);
				else if (op == "NEW_TRANSACTION")
				{
					this.transaction = db.BeginTransaction();
					committedVersion = -1;
				}
				else if (op == "ON_ERROR")
				{
					var items = await PopAsync(1);
					long errorCode = (long)items[0];

					// The .NET bindings convert this error code to a different exception which we don't handle
					if (errorCode == 1501)
						HandleFdbError(new FdbException((FdbError)errorCode), instructionIndex, stack);
					else
						stack.Add(new StackEntry(instructionIndex, ((FdbTransaction)tr).OnErrorAsync((FdbError)errorCode)));
				}
				else if (op == "GET")
				{
					var items = await PopAsync(1);
					if (random.NextDouble() < 0.5)
					{
						Slice[] values;
						if(random.NextDouble() < 0.5)
							values = (await tr.GetBatchAsync(new List<Slice> { stringToSlice(items[0]) })).Select((kv) => kv.Value).ToArray();
						else
							values = await tr.GetValuesAsync(new List<Slice> { stringToSlice(items[0]) });

						Assert.AreEqual(values.Length, 1, "GetBatch/GetValues returned wrong number of results: " + values.Length);
						AddSlice(instructionIndex, values[0]);
					}
					else
					{
						var task = tr.GetAsync(stringToSlice(items[0]));

						if (useDb)
							AddSlice(instructionIndex, await task);
						else
							stack.Add(new StackEntry(instructionIndex, task));
					}
				}
				else if (op == "GET_KEY")
				{
					var items = await PopAsync(3);
					var task = tr.GetKeyAsync(new FdbKeySelector(stringToSlice(items[0]), (long)items[1] != 0, (int)(long)items[2]));

					if (useDb)
						AddSlice(instructionIndex, await task);
					else
						stack.Add(new StackEntry(instructionIndex, task));
				}
				else if (op == "GET_RANGE")
				{
					var items = await PopAsync(5);
					Slice begin = stringToSlice(items[0]);
					Slice end = stringToSlice(items[1]);

					FdbRangeOptions options = new FdbRangeOptions();
					options.Limit = (int?)(long?)items[2];
					options.Reverse = (long)items[3] != 0;
					options.Mode = (FdbStreamingMode)(long)items[4];

					// We have to check for this now because the .NET bindings will prefer TransactionCancelled to ExactModeWithoutLimits, but the binding tester expects the opposite
					if ((options.Limit == null || options.Limit == 0) && options.Mode == FdbStreamingMode.Exact)
						throw new FdbException(FdbError.ExactModeWithoutLimits);

					await PushRangeAsync(instructionIndex, tr.GetRange(stringToSlice(items[0]), stringToSlice(items[1]), options));
				}
				else if (op == "GET_RANGE_STARTS_WITH")
				{
					var items = await PopAsync(4);
					FdbRangeOptions options = new FdbRangeOptions();
					options.Limit = (int?)(long?)items[1];
					options.Reverse = (long)items[2] != 0;
					options.Mode = (FdbStreamingMode)(long)items[3];

					// We have to check for this now because the .NET bindings will prefer TransactionCancelled to ExactModeWithoutLimits, but the binding tester expects the opposite
					if ((options.Limit == null || options.Limit == 0) && options.Mode == FdbStreamingMode.Exact)
						throw new FdbException(FdbError.ExactModeWithoutLimits);

					FdbRangeQuery rangeQuery = null;
					int choice = random.Next(3);
					if(choice == 0)
						rangeQuery = tr.GetRange(FdbKeyRange.StartsWith(stringToSlice(items[0])), options);
					else if (choice == 1)
					{
						var value = await tr.GetAsync(stringToSlice(items[0]));
						if (!value.HasValue)
							rangeQuery = tr.GetRange(FdbKeyRange.PrefixedBy(stringToSlice(items[0])), options);
					}

					if(rangeQuery == null)
						rangeQuery = tr.GetRangeStartsWith(stringToSlice(items[0]), options);

					await PushRangeAsync(instructionIndex, rangeQuery);
				}
				else if (op == "GET_RANGE_SELECTOR")
				{
					var items = await PopAsync(9);
					FdbKeySelector begin = new FdbKeySelector(stringToSlice(items[0]), (long)items[1] != 0, (int)(long)items[2]);
					FdbKeySelector end = new FdbKeySelector(stringToSlice(items[3]), (long)items[4] != 0, (int)(long)items[5]);

					FdbRangeOptions options = new FdbRangeOptions();
					options.Limit = (int?)(long?)items[6];
					options.Reverse = (long)items[7] != 0;
					options.Mode = (FdbStreamingMode)(long)items[8];

					// We have to check for this now because the .NET bindings will prefer TransactionCancelled to ExactModeWithoutLimits, but the binding tester expects the opposite
					if ((options.Limit == null || options.Limit == 0) && options.Mode == FdbStreamingMode.Exact)
						throw new FdbException(FdbError.ExactModeWithoutLimits);

					FdbRangeQuery rangeQuery;
					if (random.NextDouble() < 0.5)
						rangeQuery = tr.GetRange(begin, end, options);
					else
						rangeQuery = tr.GetRangeInclusive(begin, end - 1, options);

					await PushRangeAsync(instructionIndex, rangeQuery);
				}
				else if (op == "GET_READ_VERSION")
				{
					lastVersion = await tr.GetReadVersionAsync();
					stack.Add(new StackEntry(instructionIndex, EncodingHelper.FromByteString("GOT_READ_VERSION")));
				}
				else if (op == "SET")
				{
					await IgnoreCancelled(async () =>
					{
						var items = await PopAsync(2);
						((FdbTransaction)tr).Set(stringToSlice(items[0]), stringToSlice(items[1]));
					});
				}
				else if (op == "ATOMIC_OP")
				{
					await IgnoreCancelled(async () =>
					{
						var items = await PopAsync(3);
						string mutationType = stringToSlice(items[0]).ToByteString();

						switch (mutationType)
						{
							case "ADD":
								((FdbTransaction)tr).AtomicAdd(stringToSlice(items[1]), stringToSlice(items[2]));
								break;
							case "AND":
								((FdbTransaction)tr).AtomicAnd(stringToSlice(items[1]), stringToSlice(items[2]));
								break;
							case "OR":
								((FdbTransaction)tr).AtomicOr(stringToSlice(items[1]), stringToSlice(items[2]));
								break;
							case "XOR":
								((FdbTransaction)tr).AtomicXor(stringToSlice(items[1]), stringToSlice(items[2]));
								break;
							default:
								throw new Exception("Invalid ATOMIC_OP: " + mutationType);
						}
					});
				}
				else if (op == "SET_READ_VERSION")
					((FdbTransaction)tr).SetReadVersion(lastVersion);
				else if (op == "CLEAR")
				{
					await IgnoreCancelled(async () =>
					{
						var items = await PopAsync(1);
						((FdbTransaction)tr).Clear(stringToSlice(items[0]));
					});
				}
				else if (op == "CLEAR_RANGE")
				{
					await IgnoreCancelled(async () =>
					{
						var items = await PopAsync(2);
						Slice begin = stringToSlice(items[0]);
						Slice end = stringToSlice(items[1]);

						// We have to check for this now because the .NET bindings will prefer TransactionCancelled to InvertedRange, but the binding tester expects the opposite
						/*if (end < begin)
							throw new FdbException(FdbError.InvertedRange);*/

						((FdbTransaction)tr).ClearRange(stringToSlice(items[0]), stringToSlice(items[1]));
					});
				}
				else if (op == "CLEAR_RANGE_STARTS_WITH")
				{
					await IgnoreCancelled(async () =>
					{
						var items = await PopAsync(1);
						Slice prefix = stringToSlice(items[0]);
						((FdbTransaction)tr).ClearRange(FdbKeyRange.StartsWith(prefix));
					});
				}
				else if (op == "READ_CONFLICT_RANGE" || op == "WRITE_CONFLICT_RANGE")
				{
					await IgnoreCancelled(async () =>
					{
						var items = await PopAsync(2);

						FdbKeyRange range = new FdbKeyRange(stringToSlice(items[0]), stringToSlice(items[1]));

						// We have to check for this now because the .NET bindings will prefer TransactionCancelled to InvertedRange, but the binding tester expects the opposite
						if (range.End < range.Begin)
							throw new FdbException(FdbError.InvertedRange);

						if (op == "READ_CONFLICT_RANGE")
						{
							if (random.NextDouble() < 0.5)
								((FdbTransaction)tr).AddReadConflictRange(range);
							else
								((FdbTransaction)tr).AddReadConflictRange(range.Begin, range.End);
						}
						else
						{
							if(random.NextDouble() < 0.5)
								((FdbTransaction)tr).AddWriteConflictRange(range);
							else
								((FdbTransaction)tr).AddWriteConflictRange(range.Begin, range.End);
						}
					});

					stack.Add(new StackEntry(instructionIndex, EncodingHelper.FromByteString("SET_CONFLICT_RANGE")));
				}
				else if (op == "READ_CONFLICT_KEY" || op == "WRITE_CONFLICT_KEY")
				{
					await IgnoreCancelled(async () =>
					{
						var items = await PopAsync(1);
						Slice key = stringToSlice(items[0]);

						if (op == "READ_CONFLICT_KEY")
							((FdbTransaction)tr).AddReadConflictKey(key);
						else
							((FdbTransaction)tr).AddWriteConflictKey(key);
					});

					stack.Add(new StackEntry(instructionIndex, EncodingHelper.FromByteString("SET_CONFLICT_KEY")));
				}
				else if (op == "DISABLE_WRITE_CONFLICT")
					((FdbTransaction)tr).WithNextWriteNoWriteConflictRange();
				else if (op == "COMMIT")
				{
					await CommitAndReset((FdbTransaction)tr);
					stack.Add(new StackEntry(instructionIndex, EncodingHelper.FromByteString("RESULT_NOT_PRESENT")));
				}
				else if (op == "RESET")
					((FdbTransaction)tr).Reset();
				else if (op == "CANCEL")
					((FdbTransaction)tr).Cancel();
				else if (op == "GET_COMMITTED_VERSION")
				{
					if (committedVersion > 0)
						lastVersion = committedVersion;
					else
						lastVersion = ((FdbTransaction)tr).GetCommittedVersion();

					stack.Add(new StackEntry(instructionIndex, EncodingHelper.FromByteString("GOT_COMMITTED_VERSION")));
				}
				else if (op == "TUPLE_PACK")
				{
					long num = (long)(await PopAsync(1))[0];
					var items = await PopAsync((int)num);
					stack.Add(new StackEntry(instructionIndex, FdbTuple.FromEnumerable(items).ToSlice()));
				}
				else if (op == "TUPLE_UNPACK")
				{
					var items = await PopAsync(1);
					foreach (var t in FdbTuple.Unpack(stringToSlice(items[0])))
						stack.Add(new StackEntry(instructionIndex, FdbTuple.PackBoxed(t)));
				}
				else if (op == "TUPLE_RANGE")
				{
					long num = (long)(await PopAsync(1))[0];
					var items = await PopAsync((int)num);

					FdbKeyRange range = FdbTuple.FromEnumerable(items).ToRange();
					stack.Add(new StackEntry(instructionIndex, range.Begin));
					stack.Add(new StackEntry(instructionIndex, range.End));
				}
				else if (op == "START_THREAD")
				{
					var items = await PopAsync(1);
					StackUnitTester tester = new StackUnitTester(db, stringToSlice(items[0]).ToByteString());
					subTasks.Add(tester.RunTestAsync());
				}
				else if (op == "WAIT_EMPTY")
				{
					var items = await PopAsync(1);
					bool doneOnce = false;

					await db.Attempt.ReadAsync(async (t) =>
					{
						var results = await t.GetRangeStartsWith(stringToSlice(items[0])).ToListAsync();
						if (!doneOnce || results.Count == 1)
						{
							doneOnce = true;
							throw new FdbException((FdbError)1020);
						}
					});

					stack.Add(new StackEntry(instructionIndex, EncodingHelper.FromByteString("WAITED_FOR_EMPTY")));
				}
				else if (op == "UNIT_TESTS")
				{
					try
					{
						db.SetLocationCacheSize(100001);

						FdbTransaction trTest = db.BeginTransaction();
						trTest.WithPrioritySystemImmediate();
						trTest.WithPriorityBatch();
						//trTest.WithCausalReadRisky();
						//trTest.WithCausalWriteRisky();
						//trTest.WithReadYourWritesDisable();
						//trTest.WithReadAheadDisable();
						trTest.WithAccessToSystemKeys();
						//trTest.WithDurabilityDevNullIsWebScale();
						trTest.WithTimeout(1000);
						trTest.Timeout = 2000;
						trTest.WithRetryLimit(5);
						trTest.RetryLimit = 6;

						Slice val = await trTest.GetAsync(EncodingHelper.FromByteString("\xff"));
						await ((FdbTransaction)tr).CommitAsync();

						await TestWatches();
						await TestLocality();
					}
					catch (FdbException e)
					{
						throw new Exception("Unit tests failed: " + e.Message, e);
					}
				}
				else if (op == "LOG_STACK")
				{
					Slice prefix = stringToSlice((await PopAsync(1))[0]);
					List<Object> items = await PopAsync(stack.Count, true);
					for (int i = 0; i < items.Count; ++i)
					{
						if (i % 100 == 0)
							tr = await CommitAndReset((FdbTransaction)tr);

						StackEntry entry = (StackEntry)items[items.Count - i - 1];
						Slice packedValue = FdbTuple.PackBoxed(entry.value);
						if (packedValue.Count > 40000)
							packedValue = packedValue.Substring(0, 40000);
						((FdbTransaction)tr).Set(prefix + FdbTuple.Pack(i, entry.instructionIndex), packedValue);
					}

					await CommitAndReset((FdbTransaction)tr);
				}
				else
					throw new Exception("Unknown op " + op);
			}
			catch (FdbException e)
			{
				if (useDb)
					throw;

				HandleFdbError(e, instructionIndex, stack);
			}
		}

		private async Task TestWatches()
		{
			FdbTransaction tr = db.BeginTransaction();

			tr.Set(EncodingHelper.FromByteString("w0"), EncodingHelper.FromByteString("0"));
			tr.Clear(EncodingHelper.FromByteString("w1"));

			await tr.CommitAsync();
			tr = db.BeginTransaction();

			List<FdbWatch> watches = new List<FdbWatch>();
			watches.Add(tr.Watch(EncodingHelper.FromByteString("w0")));
			watches.Add(tr.Watch(EncodingHelper.FromByteString("w1")));

			await Task.Delay(1000);

			foreach (FdbWatch w in watches)
				Assert.IsFalse(w.HasChanged, "Watch triggered early 1");

			tr.Set(EncodingHelper.FromByteString("w0"), EncodingHelper.FromByteString("0"));
			tr.Clear(EncodingHelper.FromByteString("w1"));
			await tr.CommitAsync();
			tr = db.BeginTransaction();

			await Task.Delay(5000);

			foreach (FdbWatch w in watches)
				Assert.IsFalse(w.HasChanged, "Watch triggered early 2");

			tr.Set(EncodingHelper.FromByteString("w0"), EncodingHelper.FromByteString("a"));
			tr.Set(EncodingHelper.FromByteString("w1"), EncodingHelper.FromByteString("b"));

			await tr.CommitAsync();

			foreach (FdbWatch w in watches)
				await w;
		}

		private async Task TestLocality()
		{
			// Locality not fully implemented
			await Task.Yield();
		}
	}
}
