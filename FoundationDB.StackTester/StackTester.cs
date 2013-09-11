using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

using FoundationDB.StackTester;

namespace FoundationDB.Tester
{
	class StackTester
	{
		static void Main(string[] args)
		{
			try
			{
				StackUnitTester tester = new StackUnitTester(args[0], args.Count() > 1 ? args[1] : null);
				tester.RunTest();
			}
			catch (Exception e)
			{
				if (e is AggregateException) e = (e as AggregateException).Flatten().InnerException;
				Console.Error.WriteLine("StackTester Error:");
				Console.Error.WriteLine(e.ToString());
				Environment.ExitCode = -1;
			}

			/*Console.WriteLine("[PRESS A KEY TO EXIT]");
			Console.ReadKey();*/
		}
	}
}
