using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;
using FoundationDb.Client;
using System.Threading.Tasks;
using System.Threading;
using FoundationDb.Client.Tuples;
using FoundationDb.Client.Tables;
using System.Diagnostics;

namespace FoundationDb.Tests
{

	[TestFixture]
	public class StringInternFacts
	{

		[Test]
		public async Task Test_StringIntern_Example()
		{
			using(var db = await Fdb.OpenLocalDatabaseAsync("DB"))
			{
				var location = new FdbSubspace("BigStrings");
				var strs = new FdbStringIntern(db, location);

				using (var tr = db.BeginTransaction())
				{
					tr.Set("0", await strs.InternAsync(tr, "testing 123456789"));
					tr.Set("1", await strs.InternAsync(tr, "dog"));
					tr.Set("2", await strs.InternAsync(tr, "testing 123456789"));
					tr.Set("3", await strs.InternAsync(tr, "cat"));
					tr.Set("4", await strs.InternAsync(tr, "cat"));

					tr.Set("9", "last");
					tr.Set(":", "guard");

					await tr.CommitAsync();
				}

				using (var tr = db.BeginTransaction())
				{

					Debug.WriteLine("GetRange('0'..'9') ....");
		
					var results = await tr.GetRangeAsync(
						FdbKeySelector.FirstGreaterOrEqual(FdbKey.Ascii("0")),
						FdbKeySelector.FirstGreaterThan(FdbKey.Ascii("9"))
					);

					Debug.WriteLine("Found " + results.Page.Length + " results");
					foreach (var kvp in results.Page)
					{
						Debug.WriteLine(FdbKey.Dump(kvp.Key) + " : " + FdbKey.Dump(kvp.Value));
					}

					Debug.WriteLine("GetRange((BigStrings,*)) ....");

					results = await tr.GetRangeAsync(FdbTuple.Create("BigStrings"));

					Debug.WriteLine("Found " + results.Page.Length + " results");
					foreach (var kvp in results.Page)
					{
						Debug.WriteLine(FdbKey.Dump(kvp.Key) + " : " + FdbKey.Dump(kvp.Value));
					}

					//var r = await tr.GetKeyAsync(FdbKeySelector.FirstGreaterOrEqual(FdbKey.Ascii("0")));
					//Debug.WriteLine("First_Greater_Or_Equal('0') => " + DumpStr(r));
					//r = await tr.GetKeyAsync(FdbKeySelector.FirstGreaterThan(FdbKey.Ascii("0")));
					//Debug.WriteLine("First_Greater_Than('0') => " + DumpStr(r));

					//r = await tr.GetKeyAsync(FdbKeySelector.LastLessOrEqual(FdbKey.Ascii("4")));
					//Debug.WriteLine("Last_Less_Or_Equal('4') => " + DumpStr(r));
					//r = await tr.GetKeyAsync(FdbKeySelector.LastLessThan(FdbKey.Ascii("4")));
					//Debug.WriteLine("Last_Less_Than('4') => " + DumpStr(r));

					//r = await tr.GetKeyAsync(FdbKeySelector.LastLessOrEqual(FdbKey.Ascii("9")));
					//Debug.WriteLine("Last_Less_Or_Equal('9') => " + DumpStr(r));
					//r = await tr.GetKeyAsync(FdbKeySelector.LastLessThan(FdbKey.Ascii("9")));
					//Debug.WriteLine("Last_Less_Than('9') => " + DumpStr(r));

					//r = await tr.GetKeyAsync(FdbKeySelector.LastLessThan(FdbKey.Ascii("0")));
					//Debug.WriteLine("Last_Less_Than('0') => " + DumpStr(r));
					//r = await tr.GetKeyAsync(FdbKeySelector.FirstGreaterThan(FdbKey.Ascii("9")));
					//Debug.WriteLine("First_Greater_Than('9') => " + DumpStr(r));

					//r = await tr.GetKeyAsync(FdbKeySelector.LastLessThan(FdbKey.Ascii("\0")));
					//Debug.WriteLine("Last_Less_Than(NUL) => " + DumpStr(r));

				}
			}
		}

		private static string DumpHex(ArraySegment<byte> seg)
		{
			var sb = new StringBuilder();
			int n = seg.Count;
			int i = seg.Offset;
			while (n-- > 0)
			{
				sb.Append(seg.Array[i++].ToString("x2"));
			}
			return sb.ToString();
		}

		[Test]
		public void Test_Connecting_To_Cluster_With_Cancelled_Token_Should_Fail()
		{
			using (var cts = new CancellationTokenSource())
			{
				cts.Cancel();

				Assert.Throws<OperationCanceledException>(() => Fdb.OpenLocalClusterAsync(cts.Token).GetAwaiter().GetResult());
			}
		}

	}
}
