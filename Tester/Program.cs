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
	class Program
	{
		private FdbDatabase db;
		private FdbTransaction tr;
		private string prefix;

		private long lastVersion;

		private List<KeyValuePair<Slice, Slice>> instructions;
		private List<Object> stack;

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
			await p.RunAsync();

			foreach (Task subTask in subTasks)
			{
				await subTask;
			}
		}

		Program(FdbDatabase db, string prefix)
		{
			this.db = db;
			this.prefix = prefix;

			stack = new List<object>();
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

		private async Task<List<object>> PopAsync(int num)
		{
			List<object> items = new List<object>();
			for (int i = 0; i < num; ++i)
			{
				var item = stack.Last();
				try
				{
					if (item is Task<Slice>)
					{
						Slice val = await (Task<Slice>)item;
						if (!val.HasValue)
							items.Add("RESULT_NOT_PRESENT");
						else
							items.Add(val.ToByteString());
					}
					else if (item is Task)
					{
						await (Task)item;
						items.Add("RESULT_NOT_PRESENT");
					}
					else
						items.Add(item);
				}
				catch (FdbException e)
				{
					Console.WriteLine("FdbError (" + (int)e.Code + "): " + e.Message);
					items.Add(FdbTuple.Pack(EncodingHelper.ASCII_8_BIT.GetBytes("ERROR"), EncodingHelper.ASCII_8_BIT.GetBytes(((int)e.Code).ToString())).ToByteString());
				}

				stack.RemoveAt(stack.Count() - 1);
			}

			return items;
		}

		private async Task PushRangeAsync(FdbRangeQuery query)
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

			stack.Add(tuple.ToSlice().ToByteString());
		}

		private async Task RunAsync()
		{
			await db.Attempt.ReadAsync(async (tr) => instructions = await tr.GetRangeStartsWith(FdbTuple.Create(EncodingHelper.FromByteString(prefix))).ToListAsync());

			foreach(var i in instructions) 
			{
				IFdbTuple inst = FdbTuple.Unpack(i.Value);
				string op = inst.Get<string>(0);

				//Console.WriteLine(op);
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

				try
				{
					if (op == "PUSH")
						stack.Add(inst[1]);
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
						stack.Add((long)items[0] - (long)items[1]);
					}
					else if (op == "WAIT_FUTURE")
						stack.Add((await PopAsync(1))[0]);
					else if (op == "NEW_TRANSACTION")
						this.tr = db.BeginTransaction();
					else if (op == "ON_ERROR")
					{
						var items = await PopAsync(1);
						long errorCode = (long)items[0];
						stack.Add(tr.OnErrorAsync((FdbError)errorCode));
					}
					else if (op == "GET")
					{
						var items = await PopAsync(1);
						stack.Add(tr.GetAsync(stringToSlice(items[0])));
					}
					else if (op == "GET_KEY")
					{
						var items = await PopAsync(3);
						stack.Add(tr.GetKeyAsync(new FdbKeySelector(stringToSlice(items[0]), (long)items[1] != 0, (int)(long)items[2])));
					}
					else if (op == "GET_RANGE")
					{
						var items = await PopAsync(5);
						FdbRangeOptions options = new FdbRangeOptions();
						options.Limit = (int?)(long?)items[2];
						options.Reverse = (long)items[3] != 0;
						options.Mode = (FdbStreamingMode)(long)items[4];
						await PushRangeAsync(tr.GetRange(stringToSlice(items[0]), stringToSlice(items[1]), options));
					}
					else if (op == "GET_RANGE_STARTS_WITH")
					{
						var items = await PopAsync(4);
						FdbRangeOptions options = new FdbRangeOptions();
						options.Limit = (int?)(long?)items[1];
						options.Reverse = (long)items[2] != 0;
						options.Mode = (FdbStreamingMode)(long)items[3];
						await PushRangeAsync(tr.GetRangeStartsWith(stringToSlice(items[0]), options));
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

						await PushRangeAsync(tr.GetRange(begin, end, options));
					}
					else if (op == "GET_READ_VERSION")
					{
						lastVersion = await tr.GetReadVersionAsync();
						stack.Add("GOT_READ_VERSION");
					}
					else if (op == "SET")
					{
						var items = await PopAsync(2);
						((FdbTransaction)tr).Set(stringToSlice(items[0]), stringToSlice(items[1]));
						isMutation = true;
					}
					else if (op == "ATOMIC_OP")
					{
						var items = await PopAsync(3);
						string mutationType = stringToSlice(items[0]).ToByteString();

						switch(mutationType) 
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

						isMutation = true;
					}
					else if (op == "SET_READ_VERSION")
						tr.SetReadVersion(lastVersion);
					else if (op == "CLEAR")
					{
						var items = await PopAsync(1);
						((FdbTransaction)tr).Clear(stringToSlice(items[0]));
						isMutation = true;
					}
					else if (op == "CLEAR_RANGE")
					{
						var items = await PopAsync(2);
						((FdbTransaction)tr).ClearRange(stringToSlice(items[0]), stringToSlice(items[1]));
						isMutation = true;
					}
					else if (op == "CLEAR_RANGE_STARTS_WITH")
					{
						// Don't know if there is a good way to do this, so I'm faking it
						var items = await PopAsync(1);
						Slice prefix = stringToSlice(items[0]);
						((FdbTransaction)tr).ClearRange(prefix, StrInc(prefix));
						isMutation = true;
					}
					else if (op == "READ_CONFLICT_RANGE" || op == "WRITE_CONFLICT_RANGE")
					{
						var items = await PopAsync(2);
						FdbKeyRange range = new FdbKeyRange(stringToSlice(items[0]), stringToSlice(items[1]));

						if(op == "READ_CONFLICT_RANGE")
							((FdbTransaction)tr).AddReadConflictRange(range);
						else
							((FdbTransaction)tr).AddWriteConflictRange(range);

						stack.Add("SET_CONFLICT_RANGE");
					}
					else if (op == "READ_CONFLICT_KEY" || op == "WRITE_CONFLICT_KEY")
					{
						var items = await PopAsync(1);
						Slice key = stringToSlice(items[0]);						

						if(op == "READ_CONFLICT_KEY")
							((FdbTransaction)tr).AddReadConflictKey(key);
						else
							((FdbTransaction)tr).AddWriteConflictKey(key);

						stack.Add("SET_CONFLICT_KEY");
					}
					else if (op == "DISABLE_WRITE_CONFLICT")
						((FdbTransaction)tr).WithNextWriteNoWriteConflictRange();
					else if (op == "COMMIT") 
					{
						await ((FdbTransaction)tr).CommitAsync();
						stack.Add("RESULT_NOT_PRESENT");
						tr.Reset();
						//stack.Add(((FdbTransaction)tr).CommitAsync());
					}
					else if (op == "RESET")
						tr.Reset();
					else if (op == "CANCEL") 
						((FdbTransaction)tr).Cancel();
					else if (op == "GET_COMMITTED_VERSION")
					{
						lastVersion = tr.GetCommittedVersion();
						stack.Add("GOT_COMMITTED_VERSION");
					}
					else if (op == "TUPLE_PACK")
					{
						long num = (long)(await PopAsync(1))[0];
						var items = await PopAsync((int)num);
						stack.Add(FdbTuple.Create((IEnumerable<object>)items).ToSlice().ToByteString());
					}
					else if (op == "TUPLE_UNPACK")
					{
						var items = await PopAsync(1);
						foreach (var t in FdbTuple.Unpack(stringToSlice(items[0])))
							stack.Add(FdbTuple.Pack(t).ToByteString());
					}
					else if (op == "TUPLE_RANGE")
					{
						long num = (long)(await PopAsync(1))[0];
						var items = await PopAsync((int)num);

						FdbSubspace subspace = new FdbSubspace(FdbTuple.Create((IEnumerable<object>)items));
						stack.Add(subspace.ToRange().Begin.ToByteString());
						stack.Add(subspace.ToRange().End.ToByteString());
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

						stack.Add("WAITED_FOR_EMPTY");
					}
					else if (op == "UNIT_TESTS")
					{
						// TODO
					}
					else
						throw new Exception("Unknown op " + op);

					if (useDb && isMutation)
					{
						await tr.CommitAsync();
						stack.Add("RESULT_NOT_PRESENT");
					}
				}
				catch (FdbException e)
				{
					Console.WriteLine("FdbError (" + (int)e.Code + "): " + e.Message);
					stack.Add(FdbTuple.Pack(EncodingHelper.ASCII_8_BIT.GetBytes("ERROR"), EncodingHelper.ASCII_8_BIT.GetBytes(((int)e.Code).ToString())).ToByteString());
				}
				catch (Exception e)
				{
					Console.WriteLine("Error: " + e.Message);
				}
			}
		}
	}
}
