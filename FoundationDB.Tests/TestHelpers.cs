#region BSD License
/* Copyright (c) 2013-2020, Doxense SAS
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
	using System;
	using System.Threading;
	using System.Threading.Tasks;
	using Doxense.Collections.Tuples;
	using FoundationDB.Filters.Logging;
	using NUnit.Framework;

	internal static class TestHelpers
	{
		public const string TestClusterFile = null;
		public static readonly Slice TestGlobalPrefix = new byte[] { 0x2, (byte)'T', (byte) 'E', (byte)'S', (byte)'T', 0x0 }.AsSlice();
		public const int DefaultTimeout = 15_000;

		//TODO: move these methods to FdbTest ?

		/// <summary>Connect to the local test database</summary>
		public static Task<IFdbDatabase> OpenTestDatabaseAsync(CancellationToken ct)
		{
			var options = new FdbConnectionOptions
			{
				ClusterFile = TestClusterFile,
				Root = FdbPath.Root, // core tests cannot rely on the DirectoryLayer!
				DefaultTimeout = TimeSpan.FromMilliseconds(DefaultTimeout),
			};
			return Fdb.OpenAsync(options, ct);
		}

		/// <summary>Connect to the local test partition</summary>
		public static Task<IFdbDatabase> OpenTestPartitionAsync(CancellationToken ct)
		{
			var options = new FdbConnectionOptions
			{
				ClusterFile = TestClusterFile,
				Root = FdbPath.MakeAbsolute("Tests", "Fdb", "Environment.MachineName"),
				DefaultTimeout = TimeSpan.FromMilliseconds(DefaultTimeout),
			};
			return Fdb.OpenAsync(options, ct);
		}

		public static Task CleanSubspace(IFdbDatabase db, IKeySubspace subspace, CancellationToken ct)
		{
			Assert.That(subspace, Is.Not.Null, "null db");
			Assert.That(subspace.GetPrefix(), Is.Not.EqualTo(Slice.Empty), "Cannot clean the root of the database!");

			return db.WriteAsync(tr => tr.ClearRange(subspace.ToRange()), ct);
		}

		public static async Task CleanLocation(IFdbDatabase db, ISubspaceLocation location, CancellationToken ct)
		{
			Assert.That(db, Is.Not.Null, "null db");
			if (location.Path.Count == 0 && location.Prefix.Count == 0)
			{
				Assert.Fail("Cannot clean the root of the database!");
			}

			// if the prefix part is empty, then we simply recursively remove the corresponding sub-directory tree
			// If it is not empty, we only remove the corresponding subspace (without touching the sub-directories!)

			await db.WithoutLogging().WriteAsync(async tr =>
			{
				if (location.Path.Count == 0)
				{ // subspace under the root of the partition

					// get and clear subspace
					tr.ClearRange(KeyRange.StartsWith(location.Prefix));
				}
				else if (location.Prefix.Count == 0)
				{
					// remove previous
					await db.DirectoryLayer.TryRemoveAsync(tr, location.Path);

					// create new
					_ = await db.DirectoryLayer.CreateAsync(tr, location.Path);
				}
				else
				{ // subspace under a directory subspace

					// make sure the parent path exists!
					var subspace = await db.DirectoryLayer.CreateOrOpenAsync(tr, location.Path);

					// get and clear subspace
					tr.ClearRange(subspace.Partition[location.Prefix].ToRange());
				}
			}, ct);

		}

		public static async Task DumpSubspace(IFdbDatabase db, IKeySubspace subspace, CancellationToken ct)
		{
			Assert.That(db, Is.Not.Null);

			// do not log
			db = db.WithoutLogging();

			using (var tr = await db.BeginTransactionAsync(ct))
			{
				await DumpSubspace(tr, subspace).ConfigureAwait(false);
			}
		}

		public static async Task DumpLocation(IFdbDatabase db, ISubspaceLocation path, CancellationToken ct)
		{
			Assert.That(db, Is.Not.Null);

			// do not log
			db = db.WithoutLogging();

			using (var tr = await db.BeginTransactionAsync(ct))
			{
				var subspace = await path.Resolve(tr);
				if (subspace == null)
				{
					FdbTest.Log($"Dumping content of subspace {path}:");
					FdbTest.Log("> EMPTY!");
					return;
				}

				await DumpSubspace(tr, subspace).ConfigureAwait(false);

				if (path.Prefix.Count == 0)
				{
					var names = await db.DirectoryLayer.TryListAsync(tr, path.Path);
					if (names != null)
					{
						foreach (var name in names)
						{
							var child = await db.DirectoryLayer.TryOpenAsync(tr, path.Path[name]);
							if (child != null)
							{
								await DumpSubspace(tr, child);
							}
						}
					}
				}
			}
		}

		public static async Task DumpSubspace(IFdbReadOnlyTransaction tr, IKeySubspace subspace)
		{
			Assert.That(tr, Is.Not.Null);
			Assert.That(subspace, Is.Not.Null);

			FdbTest.Log($"Dumping content of {subspace} at {subspace.GetPrefix():K}:");
			int count = 0;
			await tr
				.GetRange(KeyRange.StartsWith(subspace.GetPrefix()))
				.ForEachAsync((kvp) =>
				{
					var key = subspace.ExtractKey(kvp.Key, boundCheck: true);
					++count;
					string keyDump;
					try
					{
						// attempts decoding it as a tuple
						keyDump = TuPack.Unpack(key).ToString()!;
					}
					catch (Exception)
					{
						// not a tuple, dump as bytes
						keyDump = "'" + key.ToString() + "'";
					}
						
					FdbTest.Log("- " + keyDump + " = " + kvp.Value.ToString());
				});

			if (count == 0)
				FdbTest.Log("> empty !");
			else
				FdbTest.Log("> Found " + count + " values");
		}

		public static async Task AssertThrowsFdbErrorAsync(Func<Task> asyncTest, FdbError expectedCode, string message = null, object[] args = null)
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
