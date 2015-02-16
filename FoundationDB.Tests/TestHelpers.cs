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
	using FoundationDB.Filters.Logging;
	using FoundationDB.Layers.Directories;
	using FoundationDB.Layers.Tuples;
	using JetBrains.Annotations;
	using NUnit.Framework;
	using System;
	using System.Threading;
	using System.Threading.Tasks;

	internal static class TestHelpers
	{
		// change these to target a specific test cluster

		public static readonly string TestClusterFile = null;
		public static readonly string TestDbName = "DB";
		public static readonly Slice TestGlobalPrefix = Slice.FromAscii("T");
		public static readonly string[] TestPartition = new string[] { "Tests", Environment.MachineName };
		public static readonly int DefaultTimeout = 15 * 1000;

		//TODO: move these methods to FdbTest ?

		/// <summary>Connect to the local test database</summary>
		public static Task<IFdbDatabase> OpenTestDatabaseAsync(CancellationToken ct)
		{
			var subspace = new FdbSubspace(TestGlobalPrefix.Memoize());
			return Fdb.OpenAsync(TestClusterFile, TestDbName, subspace, false, ct);
		}

		/// <summary>Connect to the local test database</summary>
		public static async Task<IFdbDatabase> OpenTestPartitionAsync(CancellationToken ct)
		{
			var db = await Fdb.Directory.OpenNamedPartitionAsync(TestClusterFile, TestDbName, TestPartition, false, ct);
			db.DefaultTimeout = DefaultTimeout;
			return db;
		}

		public static async Task<FdbDirectorySubspace> GetCleanDirectory([NotNull] IFdbDatabase db, [NotNull] string[] path, CancellationToken ct)
		{
			Assert.That(db, Is.Not.Null, "null db");
			Assert.That(path, Is.Not.Null.And.Length.GreaterThan(0), "invalid path");

			// do not log
			db = db.WithoutLogging();

			// remove previous
			await db.Directory.TryRemoveAsync(path, ct);

			// create new
			var subspace = await db.Directory.CreateAsync(path, ct);
			Assert.That(subspace, Is.Not.Null);
			Assert.That(db.GlobalSpace.Contains(subspace.Key), Is.True);
			return subspace;
		}

		public static async Task DumpSubspace([NotNull] IFdbDatabase db, [NotNull] IFdbSubspace subspace, CancellationToken ct)
		{
			Assert.That(db, Is.Not.Null);
			Assert.That(db.GlobalSpace.Contains(subspace.ToFoundationDbKey()), Is.True, "Using a location outside of the test database partition!!! This is probably a bug in the test...");

			// do not log
			db = db.WithoutLogging();

			using (var tr = db.BeginTransaction(ct))
			{
				await DumpSubspace(tr, subspace).ConfigureAwait(false);
			}
		}

		public static async Task DumpSubspace([NotNull] IFdbReadOnlyTransaction tr, [NotNull] IFdbSubspace subspace)
		{
			Assert.That(tr, Is.Not.Null);

			Console.WriteLine("Dumping content of subspace " + subspace.ToString() + " :");
			int count = 0;
			await tr
				.GetRange(FdbKeyRange.StartsWith(subspace.ToFoundationDbKey()))
				.ForEachAsync((kvp) =>
				{
					var key = subspace.ExtractKey(kvp.Key, boundCheck: true);
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
						
					Console.WriteLine("- " + keyDump + " = " + kvp.Value.ToString());
				});

			if (count == 0)
				Console.WriteLine("> empty !");
			else
				Console.WriteLine("> Found " + count + " values");
		}

		public static async Task AssertThrowsFdbErrorAsync([NotNull] Func<Task> asyncTest, FdbError expectedCode, string message = null, object[] args = null)
		{
			try
			{
				await asyncTest();
				Assert.Fail(message, args);
			}
			catch (AssertionException) { throw; }
			catch (Exception e)
			{
				Assert.That(e, Is.InstanceOf<FdbException>().With.Property("Code").EqualTo(expectedCode), message, args);
			}
		}

	}

}
