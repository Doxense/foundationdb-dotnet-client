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

namespace FoundationDB.Layers.Interning.Tests
{
	using FoundationDB.Client;
	using FoundationDB.Client.Tests;
	using NUnit.Framework;
	using System;
	using System.Threading.Tasks;

	[TestFixture]
	public class StringInternFacts : FdbTest
	{

		[Test]
		public async Task Test_StringIntern_Example()
		{
			using (var db = await OpenTestPartitionAsync())
			{
				var stringSpace = db.Partition.ByKey("Strings");
				var dataSpace = db.Partition.ByKey("Data");

				// clear all previous data
				await DeleteSubspace(db, stringSpace);
				await DeleteSubspace(db, dataSpace);

				var stringTable = new FdbStringIntern(stringSpace);

				// insert a bunch of strings
				using (var tr = db.BeginTransaction(this.Cancellation))
				{
					tr.Set(dataSpace.Tuples.EncodeKey("a"), await stringTable.InternAsync(tr, "testing 123456789"));
					tr.Set(dataSpace.Tuples.EncodeKey("b"), await stringTable.InternAsync(tr, "dog"));
					tr.Set(dataSpace.Tuples.EncodeKey("c"), await stringTable.InternAsync(tr, "testing 123456789"));
					tr.Set(dataSpace.Tuples.EncodeKey("d"), await stringTable.InternAsync(tr, "cat"));
					tr.Set(dataSpace.Tuples.EncodeKey("e"), await stringTable.InternAsync(tr, "cat"));

					await tr.CommitAsync();
				}

#if DEBUG
				await DumpSubspace(db, stringSpace);
				await DumpSubspace(db, dataSpace);
#endif

				// check the contents of the data
				using (var tr = db.BeginTransaction(this.Cancellation))
				{
					var uid_a = await tr.GetAsync(dataSpace.Tuples.EncodeKey("a"));
					var uid_b = await tr.GetAsync(dataSpace.Tuples.EncodeKey("b"));
					var uid_c = await tr.GetAsync(dataSpace.Tuples.EncodeKey("c"));
					var uid_d = await tr.GetAsync(dataSpace.Tuples.EncodeKey("d"));
					var uid_e = await tr.GetAsync(dataSpace.Tuples.EncodeKey("e"));

					// a, b, d should be different
					Assert.That(uid_b, Is.Not.EqualTo(uid_a));
					Assert.That(uid_d, Is.Not.EqualTo(uid_a));

					// a should equal c
					Assert.That(uid_c, Is.EqualTo(uid_a));

					// d should equal e
					Assert.That(uid_e, Is.EqualTo(uid_d));

					// perform a lookup
					var str_a = await stringTable.LookupAsync(tr, uid_a);
					var str_b = await stringTable.LookupAsync(tr, uid_b);
					var str_c = await stringTable.LookupAsync(tr, uid_c);
					var str_d = await stringTable.LookupAsync(tr, uid_d);
					var str_e = await stringTable.LookupAsync(tr, uid_e);

					Assert.That(str_a, Is.EqualTo("testing 123456789"));
					Assert.That(str_b, Is.EqualTo("dog"));
					Assert.That(str_c, Is.EqualTo(str_a));
					Assert.That(str_d, Is.EqualTo("cat"));
					Assert.That(str_e, Is.EqualTo(str_d));
				}

				stringTable.Dispose();
			}
		}

	}
}
