using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using FoundationDB.Client;
using FoundationDB.Layers.Tuples;

using Utils;

namespace Utils
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

namespace Tester
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

	class Program
	{
		private FdbDatabase db;
		private FdbTransaction tr;
		private string prefix;

		private long lastVersion;

		private List<KeyValuePair<Slice, Slice>> instructions;
		private List<StackEntry> stack;

		private static List<Task> subTasks = new List<Task>();
		private static Random rng = new Random(0);

		static void Main(string[] args)
		{
			try
			{
				ExecuteAsync(() => MainAsync(args));
			}
			catch (Exception e)
			{
				if (e is AggregateException) e = (e as AggregateException).Flatten().InnerException;
				Console.Error.WriteLine("Run Error:");
				Console.Error.WriteLine(e.ToString());
				Environment.ExitCode = -1;
			}

			/*Console.WriteLine("[PRESS A KEY TO EXIT]");
			Console.ReadKey();*/
		}

		private static void ExecuteAsync(Func<Task> code)
		{
			Task.Run(code).GetAwaiter().GetResult();
		}

		private static async Task MainAsync(string[] args)
		{
			Fdb.Start();

			string prefix = args[0];
			string clusterFile = args.Length > 2 ? args[1] : null;

			FdbDatabase db = await Fdb.OpenDatabaseAsync(clusterFile, "DB");
			Program p = new Program(db, prefix);
			int result = await p.RunAsync();

			foreach (Task<int> subTask in subTasks)
				result = Math.Max(await subTask, result);

			Environment.ExitCode = result;
		}

		Program(FdbDatabase db, string prefix)
		{
			this.db = db;
			this.prefix = prefix;

			stack = new List<StackEntry>();
			lastVersion = 0;
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
			items.Add(new StackEntry(instructionIndex, FdbTuple.Pack(EncodingHelper.ASCII_8_BIT.GetBytes("ERROR"), EncodingHelper.ASCII_8_BIT.GetBytes(((int)e.Code).ToString())).ToByteString()));
		}

		private void AddSlice(int instructionIndex, Slice slice, List<StackEntry> items = null)
		{
			if (items == null)
				items = stack;

			if (!slice.HasValue)
				items.Add(new StackEntry(instructionIndex, "RESULT_NOT_PRESENT"));
			else
				items.Add(new StackEntry(instructionIndex, slice.ToByteString()));
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
						items.Add(new StackEntry(item.instructionIndex, "RESULT_NOT_PRESENT"));
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

			if (rng.NextDouble() < 0.5)
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
				tuple = FdbTuple.Create(results.SelectMany((r) => new object[] { r.Key, r.Value }));
			}

			stack.Add(new StackEntry(instructionIndex, tuple.ToSlice().ToByteString()));
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

		private async Task<int> RunAsync()
		{
			try
			{
				await db.Attempt.ReadAsync(async (tr) => instructions = await tr.GetRangeStartsWith(FdbTuple.Create(EncodingHelper.FromByteString(prefix))).ToListAsync());

				for(int instructionIndex = 0; instructionIndex < instructions.Count; ++instructionIndex)
				{
					IFdbTuple inst = FdbTuple.Unpack(instructions[instructionIndex].Value);
					string op = inst.Get<string>(0);

					/*if (op != "PUSH" && op != "SWAP")
						Console.WriteLine(op);*/

					IFdbReadTransaction tr = this.tr;

					bool useDb = op.EndsWith("_DATABASE");
					bool isMutation = false;
					if (useDb)
					{
						tr = db.BeginTransaction();
						op = op.Substring(0, op.Length - 9);
					}

					bool snapshot = op.EndsWith("_SNAPSHOT");
					if (snapshot)
					{
						tr = this.tr.Snapshot;
						op = op.Substring(0, op.Length - 9);
					}

					bool retry = true;
					while (retry)
					{
						retry = useDb;
						Task onErrorTask = null;

						try
						{
							if (op == "PUSH")
								stack.Add(new StackEntry(instructionIndex, inst[1]));
							else if (op == "DUP")
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
								this.tr = db.BeginTransaction();
							else if (op == "ON_ERROR")
							{
								var items = await PopAsync(1);
								long errorCode = (long)items[0];

								// The .NET bindings convert this error code to a different exception which we don't handle
								if (errorCode == 1501)
									HandleFdbError(new FdbException((FdbError)errorCode), instructionIndex, stack);
								else
									stack.Add(new StackEntry(instructionIndex, tr.OnErrorAsync((FdbError)errorCode)));
							}
							else if (op == "GET")
							{
								var items = await PopAsync(1);
								var task = tr.GetAsync(stringToSlice(items[0]));

								if (useDb)
									AddSlice(instructionIndex, await task);
								else
									stack.Add(new StackEntry(instructionIndex, task));
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

								// We have to check for this now because the .NET bindings will prefer TransactionCancelled to InvertedRange, but the binding tester expects the opposite
								/*if (end < begin)
									Console.Write("Inverted ");
								Console.WriteLine("Range: " + Printable(begin) + " - " + Printable(end));
								if (end < begin)
								{
									throw new FdbException(FdbError.InvertedRange);
								}*/

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

								await PushRangeAsync(instructionIndex, tr.GetRange(FdbKeyRange.StartsWith(stringToSlice(items[0])), options));
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

								await PushRangeAsync(instructionIndex, tr.GetRange(begin, end, options));
							}
							else if (op == "GET_READ_VERSION")
							{
								lastVersion = await tr.GetReadVersionAsync();
								stack.Add(new StackEntry(instructionIndex, "GOT_READ_VERSION"));
							}
							else if (op == "SET")
							{
								await IgnoreCancelled(async () =>
								{
									var items = await PopAsync(2);
									((FdbTransaction)tr).Set(stringToSlice(items[0]), stringToSlice(items[1]));
								});

								isMutation = true;
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
											((FdbTransaction)tr).AtomicAdd(stringToSlice(items[0]), stringToSlice(items[1]));
											break;
										case "AND":
											((FdbTransaction)tr).AtomicAnd(stringToSlice(items[0]), stringToSlice(items[1]));
											break;
										case "OR":
											((FdbTransaction)tr).AtomicOr(stringToSlice(items[0]), stringToSlice(items[1]));
											break;
										case "XOR":
											((FdbTransaction)tr).AtomicXor(stringToSlice(items[0]), stringToSlice(items[1]));
											break;
										default:
											throw new Exception("Invalid ATOMIC_OP: " + mutationType);
									}
								});

								isMutation = true;
							}
							else if (op == "SET_READ_VERSION")
								tr.SetReadVersion(lastVersion);
							else if (op == "CLEAR")
							{
								await IgnoreCancelled(async () =>
								{
									var items = await PopAsync(1);
									((FdbTransaction)tr).Clear(stringToSlice(items[0]));
								});

								isMutation = true;
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

								isMutation = true;
							}
							else if (op == "CLEAR_RANGE_STARTS_WITH")
							{
								await IgnoreCancelled(async () =>
								{
									var items = await PopAsync(1);
									Slice prefix = stringToSlice(items[0]);
									((FdbTransaction)tr).ClearRange(FdbKeyRange.StartsWith(prefix));
								});

								isMutation = true;
							}
							else if (op == "READ_CONFLICT_RANGE" || op == "WRITE_CONFLICT_RANGE")
							{
								await IgnoreCancelled(async () =>
								{
									var items = await PopAsync(2);

									FdbKeyRange range = new FdbKeyRange(stringToSlice(items[0]), stringToSlice(items[1]));

									// We have to check for this now because the .NET bindings will prefer TransactionCancelled to InvertedRange, but the binding tester expects the opposite
									/*if (range.End < range.Begin)
										throw new FdbException(FdbError.InvertedRange);*/

									if (op == "READ_CONFLICT_RANGE")
										((FdbTransaction)tr).AddReadConflictRange(range);
									else
										((FdbTransaction)tr).AddWriteConflictRange(range);
								});

								stack.Add(new StackEntry(instructionIndex, "SET_CONFLICT_RANGE"));
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

								stack.Add(new StackEntry(instructionIndex, "SET_CONFLICT_KEY"));
							}
							else if (op == "DISABLE_WRITE_CONFLICT")
								((FdbTransaction)tr).WithNextWriteNoWriteConflictRange();
							else if (op == "COMMIT")
							{
								await ((FdbTransaction)tr).CommitAsync();
								stack.Add(new StackEntry(instructionIndex, "RESULT_NOT_PRESENT"));
							}
							else if (op == "RESET")
								tr.Reset();
							else if (op == "CANCEL")
								((FdbTransaction)tr).Cancel();
							else if (op == "GET_COMMITTED_VERSION")
							{
								lastVersion = tr.GetCommittedVersion();
								stack.Add(new StackEntry(instructionIndex, "GOT_COMMITTED_VERSION"));
							}
							else if (op == "TUPLE_PACK")
							{
								long num = (long)(await PopAsync(1))[0];
								var items = await PopAsync((int)num);
								stack.Add(new StackEntry(instructionIndex, FdbTuple.Create((IEnumerable<object>)items).ToSlice().ToByteString()));
							}
							else if (op == "TUPLE_UNPACK")
							{
								var items = await PopAsync(1);
								foreach (var t in FdbTuple.Unpack(stringToSlice(items[0])))
									stack.Add(new StackEntry(instructionIndex, FdbTuple.Pack(t).ToByteString()));
							}
							else if (op == "TUPLE_RANGE")
							{
								long num = (long)(await PopAsync(1))[0];
								var items = await PopAsync((int)num);

								FdbKeyRange range = FdbTuple.Create((IEnumerable<object>)items).ToRange();
								stack.Add(new StackEntry(instructionIndex, range.Begin.ToByteString()));
								stack.Add(new StackEntry(instructionIndex, range.End.ToByteString()));
							}
							else if (op == "START_THREAD")
							{
								var items = await PopAsync(1);
								Program p = new Program(db, stringToSlice(items[0]).ToByteString());
								subTasks.Add(p.RunAsync());
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

								stack.Add(new StackEntry(instructionIndex, "WAITED_FOR_EMPTY"));
							}
							else if (op == "UNIT_TESTS")
							{
								// TODO
							}
							else if (op == "LOG_STACK")
							{
								Slice prefix = stringToSlice((await PopAsync(1))[0]);
								List<Object> items = await PopAsync(stack.Count, true);
								for (int i = 0; i < items.Count; ++i)
								{
									if (i % 100 == 0)
									{
										await tr.CommitAsync();
										tr.Reset();
									}

									StackEntry entry = (StackEntry)items[items.Count - i - 1];
									Slice value = stringToSlice(entry.value);
									if (value.Count > 40000)
										value = value.Substring(0, 40000);
									((FdbTransaction)tr).Set(prefix + FdbTuple.Pack(i, entry.instructionIndex), FdbTuple.Pack(value));
								}

								await tr.CommitAsync();
								tr.Reset();
							}
							else
								throw new Exception("Unknown op " + op);

							if (useDb)
							{
								if (isMutation)
								{
									await tr.CommitAsync();
									stack.Add(new StackEntry(instructionIndex, "RESULT_NOT_PRESENT"));
								}

								retry = false;
							}
						}
						catch (FdbException e)
						{
							var error = e;
							if (useDb)
								onErrorTask = tr.OnErrorAsync(e.Code);
							else
								HandleFdbError(e, instructionIndex, stack);
						}
						if (onErrorTask != null)
						{
							try
							{
								await onErrorTask;
							}
							catch (FdbException e)
							{
								HandleFdbError(e, instructionIndex, stack);
								retry = false;
							}
						}
					}
				}
			}
			catch (Exception e)
			{
				Console.WriteLine("Error: " + e.Message);
				return 2;
			}

			return 0;
		}
	}
}
