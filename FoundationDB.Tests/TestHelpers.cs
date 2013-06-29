#region BSD Licence
/* Copyright (c) 2013, Doxense SARL
All rights reserved.

Redistribution and use in source and binary forms, with or without
modification, are permitted provided that the following conditions are met:
	* Redistributions of source code must retain the above copyright
	  notice, this list of conditions and the following disclaimer.
	* Redistributions in binary form must reproduce the above copyright
	  notice, this list of conditions and the following disclaimer in the
	  documentation and/or other materials provided with the distribution.
	* Neither the name of Doxense nor the
	  names of its contributors may be used to endorse or promote products
	  derived from this software without specific prior written permission.

THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND
ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
DISCLAIMED. IN NO EVENT SHALL <COPYRIGHT HOLDER> BE LIABLE FOR ANY
DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
(INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
(INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
 */
#endregion

namespace FoundationDB.Client.Tests
{
	using FoundationDB.Layers.Tuples;
	using NUnit.Framework;
	using System;
	using System.Threading.Tasks;

	internal static class TestHelpers
	{
		// change these to target a specific test cluster

		public const string TestClusterFile = null; // null will your defaut fdb.cluster file
		public const string TestDbName = "DB"; // note cannot change this as of Beta2
		public const string TestPartition = "T"; // set it to some string to restrict the tests to a partition of the database, or null to overwrite everything

		/// <summary>Connect to the local test database</summary>
		/// <returns></returns>
		public static Task<FdbDatabase> OpenTestDatabaseAsync()
		{
			return Fdb.OpenDatabaseAsync(TestClusterFile, TestDbName, string.IsNullOrEmpty(TestPartition) ? FdbSubspace.Empty : new FdbSubspace(TestPartition));
		}

		public static async Task DumpSubspace(FdbDatabase db, FdbSubspace subspace)
		{
			Assert.That(subspace.Tuple.StartsWith(db.Namespace.Tuple), Is.True, "Using a location outside of the test database partition!!! This is probably a bug in the test...");

			using (var tr = db.BeginTransaction())
			{
				Console.WriteLine("Dumping content of subspace " + subspace.ToString() + " :");
				int count = 0;
				await tr
					.GetRangeStartsWith(subspace.Tuple)
					.ForEachAsync((key, value) =>
					{
						++count;
						string keyDump = null;
						try
						{
							// attemps decoding it as a tuple
							keyDump = key.ToTuple().ToString();
						}
						catch (Exception)
						{
							// not a tuple, dump as bytes
							keyDump = "'" + key.ToString() + "'";
						}
						
						Console.WriteLine("- " + keyDump + " = " + value.ToString());
					});

				if (count == 0)
					Console.WriteLine("> empty !");
				else
					Console.WriteLine("> Found " + count + " values");
			}
		}

		public static async Task DeleteSubspace(FdbDatabase db, FdbSubspace subspace)
		{
			using (var tr = db.BeginTransaction())
			{
				tr.ClearRange(subspace.Tuple);
				await tr.CommitAsync();
			}
		}

	}

}
