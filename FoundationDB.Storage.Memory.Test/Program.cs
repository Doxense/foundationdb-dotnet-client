using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FoundationDB.Storage.Memory.Core;
using FoundationDB.Client;
using FoundationDB.Storage.Memory.API.Tests;

namespace FoundationDB.Storage.Memory
{
	public class Program
	{

		public static void Main()
		{

			new MemoryTransactionFacts().Test_MiniBench().Wait();

#if false

			using (var sl = new Table<int, string>(KeyValueEncoders.Ordered.Int32Encoder, KeyValueEncoders.Values.StringEncoder))
			{
				try
				{
					ulong seq = 0;

					Console.WriteLine("Adding...");

					sl.Put(123, "Hello");
					sl.Put(456, "World");
					Dump(sl.Current);

					//Console.WriteLine("Adding more...");
					//for (int i = 0; i < 10; i++)
					//{
					//	sl.Insert(seq++, 1000 + i, "Test #" + i);
					//}
					//Dump(sl.Table);

					Console.WriteLine("Deleting");
					sl.Delete(456);
					Dump(sl.Current);

					string s;
					if (sl.TryGet(456, out s))
					{
						Console.WriteLine("Found: " + s);
					}
					else
					{
						Console.WriteLine("Not Found");
					}

				}
				catch(Exception e)
				{
					Console.Error.WriteLine(e.ToString());
					Console.WriteLine();
				}
				Console.WriteLine("[PRESS ENTER TO EXIT]");
				Console.ReadLine();
			}
#endif
		}

	}
}
