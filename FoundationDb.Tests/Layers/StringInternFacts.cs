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
	* Neither the name of the <organization> nor the
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

using FoundationDb.Client;
using FoundationDb.Client.Tables;
using FoundationDb.Client.Tuples;
using NUnit.Framework;
using System;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace FoundationDb.Tests
{

	[TestFixture]
	public class StringInternFacts
	{

		[TestFixtureSetUp]
		public void Setup()
		{
			//TODO: cleanup ?
		}

		[TestFixtureTearDown]
		public void Teardown()
		{
			Fdb.Stop();
		}

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
