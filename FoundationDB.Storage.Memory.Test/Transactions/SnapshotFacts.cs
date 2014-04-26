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
			const int N = 1 * 1000 * 1000;

			if (File.Exists(FILE_PATH)) File.Delete(FILE_PATH);

			// insert N sequential items and bulk load with "ordered = true" to skip the sorting of levels

			Console.WriteLine("Generating " + N.ToString("N0") + " keys...");
			var data = new KeyValuePair<Slice, Slice>[N];
			var rnd = new Random();
			for (int i = 0; i < N; i++)
			{
				data[i] = new KeyValuePair<Slice, Slice>(
					Slice.FromAscii(i.ToString("D16")),
					Slice.Random(rnd, 50)
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
			Console.WriteLine("File size is " + fi.Length.ToString("N0") + " bytes (" + (fi.Length * 1.0d / N).ToString("N2") + " bytes/item, " + (fi.Length / (1048576.0 * sw.Elapsed.TotalSeconds)).ToString("N3") + " MB/sec)");

			Console.Write("Loading...");
			sw.Restart();
			using (var db = await MemoryDatabase.LoadFromAsync(FILE_PATH, this.Cancellation))
			{
				sw.Stop();
				Console.WriteLine(" done in " + sw.Elapsed.TotalSeconds.ToString("N1") + " secs (" + (fi.Length / (1048576.0 * sw.Elapsed.TotalSeconds)).ToString("N0") + " MB/sec)");
				db.Debug_Dump();

				Console.WriteLine("Checking data integrity...");
				sw.Restart();
				long n = 0;
				foreach (var batch in data.Buffered(50 * 1000))
				{
					using (var tx = db.BeginReadOnlyTransaction(this.Cancellation))
					{
						var res = await tx
							.Snapshot
							.GetRange(
								FdbKeySelector.FirstGreaterOrEqual(batch[0].Key),
								FdbKeySelector.FirstGreaterThan(batch[batch.Count - 1].Key))
							.ToListAsync()
							.ConfigureAwait(false);

						Assert.That(res.Count, Is.EqualTo(batch.Count), "Some keys are missing from {0} to {1} :(", batch[0], batch[batch.Count - 1]);

						for (int i = 0; i < res.Count; i++)
						{
							// note: Is.EqualTo(...) is slow on Slices so we speed things a bit
							if (res[i].Key != batch[i].Key) Assert.That(res[i].Key, Is.EqualTo(batch[i].Key), "Key is different :(");
							if (res[i].Value != batch[i].Value) Assert.That(res[i].Value, Is.EqualTo(batch[i].Value), "Value is different for key {0} :(", batch[i].Key);
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
