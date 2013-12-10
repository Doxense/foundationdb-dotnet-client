#region Copyright (c) 2013-2014, Doxense SAS. All rights reserved.
// See License.MD for license information
#endregion

namespace FoundationDB.Storage.Memory
{
	using System;
	using FoundationDB.Storage.Memory.API.Tests;
	using FoundationDB.Storage.Memory.Core.Test;

	public class Program
	{

		public static void Main()
		{
			//new ColaStoreFacts().Test_MiniBench();
			//new ColaOrderedSetFacts().Test_MiniBench();
			new ColaOrderedDictionaryFacts().Test_MiniBench();
			//new MemoryTransactionFacts().Test_MiniBench().Wait();
		}

	}
}
