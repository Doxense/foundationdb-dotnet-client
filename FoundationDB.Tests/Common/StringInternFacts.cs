#region Copyright (c) 2023-2025 SnowBank SAS, (c) 2005-2023 Doxense SAS
// All rights reserved.
// 
// Redistribution and use in source and binary forms, with or without
// modification, are permitted provided that the following conditions are met:
// 	* Redistributions of source code must retain the above copyright
// 	  notice, this list of conditions and the following disclaimer.
// 	* Redistributions in binary form must reproduce the above copyright
// 	  notice, this list of conditions and the following disclaimer in the
// 	  documentation and/or other materials provided with the distribution.
// 	* Neither the name of SnowBank nor the
// 	  names of its contributors may be used to endorse or promote products
// 	  derived from this software without specific prior written permission.
// 
// THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND
// ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
// WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
// DISCLAIMED. IN NO EVENT SHALL SNOWBANK SAS BE LIABLE FOR ANY
// DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
// (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
// LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
// ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
// (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
// SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
#endregion

namespace FoundationDB.Layers.Interning.Tests
{

	[TestFixture]
	[Obsolete]
	public class StringInternFacts : FdbTest
	{

		[Test]
		public async Task Test_StringIntern_Example()
		{
			using (var db = await OpenTestPartitionAsync())
			{
				var location = db.Root;
				await CleanLocation(db, location);

				var stringSpace = location.ByKey("Strings");
				var dataSpace = location.ByKey("Data").AsTyped<string>();

				var stringTable = new FdbStringIntern(stringSpace);

				// insert a bunch of strings
				await stringTable.WriteAsync(db, async (tr, table) =>
				{
					Assert.That(table, Is.Not.Null);
					var va = await table.InternAsync(tr, "testing 123456789");
					var vb = await table.InternAsync(tr, "dog");
					var vc = await table.InternAsync(tr, "testing 123456789");
					var vd = await table.InternAsync(tr, "cat");
					var ve = await table.InternAsync(tr, "cat");

					var subspace = await dataSpace.Resolve(tr);
					tr.Set(subspace["a"], va);
					tr.Set(subspace["b"], vb);
					tr.Set(subspace["c"], vc);
					tr.Set(subspace["d"], vd);
					tr.Set(subspace["e"], ve);
				}, this.Cancellation);

#if DEBUG
				await DumpSubspace(db, stringSpace);
				await DumpSubspace(db, dataSpace);
#endif

				// check the contents of the data
				await stringTable.ReadAsync(db, async (tr, table) =>
				{
					var subspace = await dataSpace.Resolve(tr);
					var uid_a = await tr.GetAsync(subspace["a"]);
					var uid_b = await tr.GetAsync(subspace["b"]);
					var uid_c = await tr.GetAsync(subspace["c"]);
					var uid_d = await tr.GetAsync(subspace["d"]);
					var uid_e = await tr.GetAsync(subspace["e"]);

					// a, b, d should be different
					Assert.That(uid_b, Is.Not.EqualTo(uid_a));
					Assert.That(uid_d, Is.Not.EqualTo(uid_a));

					// a should equal c
					Assert.That(uid_c, Is.EqualTo(uid_a));

					// d should equal e
					Assert.That(uid_e, Is.EqualTo(uid_d));

					// perform a lookup
					var str_a = await table.LookupAsync(tr, uid_a);
					var str_b = await table.LookupAsync(tr, uid_b);
					var str_c = await table.LookupAsync(tr, uid_c);
					var str_d = await table.LookupAsync(tr, uid_d);
					var str_e = await table.LookupAsync(tr, uid_e);

					Assert.That(str_a, Is.EqualTo("testing 123456789"));
					Assert.That(str_b, Is.EqualTo("dog"));
					Assert.That(str_c, Is.EqualTo(str_a));
					Assert.That(str_d, Is.EqualTo("cat"));
					Assert.That(str_e, Is.EqualTo(str_d));
				}, this.Cancellation);

				stringTable.Dispose();
			}
		}

	}
}
