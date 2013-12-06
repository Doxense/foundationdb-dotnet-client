#region Copyright (c) 2013-2014, Doxense SAS. All rights reserved.
// See License.MD for license information
#endregion

namespace FoundationDB.Storage.Memory
{
	using System;
	using FoundationDB.Storage.Memory.API.Tests;

	public class Program
	{

		public static void Main()
		{
			new MemoryTransactionFacts().Test_MiniBench().Wait();
		}

	}
}
