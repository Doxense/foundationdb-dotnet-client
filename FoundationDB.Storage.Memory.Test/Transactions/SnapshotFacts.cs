#region Copyright (c) 2013-2014, Doxense SAS. All rights reserved.
// See License.MD for license information
#endregion

namespace FoundationDB.Storage.Memory.API.Tests
{
	using FoundationDB.Client;
	using FoundationDB.Layers.Tuples;
	using FoundationDB.Storage.Memory.Tests;
	using FoundationDB.Linq;
	using NUnit.Framework;
	using System;
	using System.Collections.Generic;
	using System.Diagnostics;
	using System.Threading.Tasks;
	using System.Linq;
	using System.IO;

	[TestFixture]
	public class SnapshotFacts : FdbTest
	{

		[Test]
		public async Task Test_Can_Save_And_Reload_Snapshot()
		{
			const string FILE_PATH = ".\\test.pndb";
			const int N = 10 * 1000 * 1000;

			if (File.Exists(FILE_PATH)) File.Delete(FILE_PATH);

			// insert N sequential items and bulk load with "ordered = true" to skip the sorting of levels

			Console.WriteLine("Generating " + N.ToString("N0") + " keys...");
			var data = new KeyValuePair<Slice, Slice>[N];
			var rnd = new Random();
			for (int i = 0; i < N; i++)
			{
				data[i] = new KeyValuePair<Slice, Slice>(
				 FdbTuple.Pack(i),
				 Slice.FromFixed32(rnd.Next())
				);
			}

			var sw = new Stopwatch();

			using (var db = MemoryDatabase.CreateNew())
			{
				Console.Write("Inserting ...");
				sw.Restart();
				await db.BulkLoadAsync(data, ordered: true);
				sw.Stop();
				Console.WriteLine(" done in " + sw.Elapsed.TotalSeconds.ToString("N1") + " secs");

				db.Debug_Dump();

				Console.Write("Saving...");
				sw.Restart();
				await db.SaveSnapshotAsync(FILE_PATH, null, this.Cancellation);
				sw.Stop();
				Console.WriteLine(" done in " + sw.Elapsed.TotalSeconds.ToString("N1") + " secs");
			}

			var fi = new FileInfo(FILE_PATH);
			Assert.That(fi.Exists, Is.True, "Snapshot file not found");
			Console.WriteLine("File size is " + fi.Length.ToString("N0") + " bytes (" + (fi.Length * 1.0d / N).ToString("N2") + " bytes/item)");

			Console.Write("Loading...");
			sw.Restart();
			using (var db = await MemoryDatabase.LoadFromAsync(FILE_PATH, this.Cancellation))
			{
				sw.Stop();
				Console.WriteLine(" done in " + sw.Elapsed.TotalSeconds.ToString("N1") + " secs");
				db.Debug_Dump();

				Console.WriteLine("Checking data integrity...");
				sw.Restart();
				long n = 0;
				foreach (var batch in data.Buffered(10 * 1000))
				{
					using (var tx = db.BeginReadOnlyTransaction(this.Cancellation))
					{
						var res = await tx.GetValuesAsync(batch.Select(kv => kv.Key)).ConfigureAwait(false);
						for (int i = 0; i < res.Length; i++)
						{
							if (res[i].IsNull)
								Assert.Fail("Key {0} is missing ({1})", batch[i].Key, batch[i].Value);
							else
								Assert.That(res[i], Is.EqualTo(batch[i].Value), "Key {0} is different", batch[i].Key);
						}
					}
					n += batch.Count;
					Console.Write("\r" + n.ToString("N0"));
				}
				sw.Stop();
				Console.WriteLine(" done in " + sw.Elapsed.TotalSeconds.ToString("N1") + " secs");
			}

			Console.WriteLine("Content of database are identical ^_^");
		}

	}
}
