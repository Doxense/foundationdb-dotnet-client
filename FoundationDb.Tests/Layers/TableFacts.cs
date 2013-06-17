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

namespace FoundationDb.Layers.Tables.Tests
{
	using FoundationDb.Client;
	using FoundationDb.Client.Tests;
	using FoundationDb.Layers.Tuples;
	using NUnit.Framework;
	using System;
	using System.Threading.Tasks;

	[TestFixture]
	public class TableFacts
	{

		[Test]
		public async Task Test_FdbTable_Read_Write_Delete()
		{

			using (var db = await TestHelpers.OpenTestDatabaseAsync())
			{

				var subspace = new FdbSubspace(FdbTuple.Create("TestTable"));

				// clear previous values
				await TestHelpers.DeleteSubspace(db, subspace);

				var table = new FdbTable(subspace);

				string secret ="world:" + Guid.NewGuid().ToString();

				// read non existing value
				using (var tr = db.BeginTransaction())
				{
					var value = await table.GetAsync(tr, FdbTuple.Create("hello"));
					Assert.That(value, Is.EqualTo(Slice.Nil));
				}

				// write value
				using (var tr = db.BeginTransaction())
				{
			
					table.Set(tr, FdbTuple.Create("hello"), Slice.FromString(secret));

					await tr.CommitAsync();
				}

#if DEBUG
				await TestHelpers.DumpSubspace(db, subspace);
#endif

				// read value back
				using (var tr = db.BeginTransaction())
				{
					var value = await table.GetAsync(tr, FdbTuple.Create("hello"));
					Assert.That(value, Is.Not.EqualTo(Slice.Nil));
					Assert.That(value.ToString(), Is.EqualTo(secret));
				}

				// directly read the value, behind the table's back
				using (var tr = db.BeginTransaction())
				{
					var value = await tr.GetAsync(subspace.Tuple.Append("hello"));
					Assert.That(value, Is.Not.EqualTo(Slice.Nil));
					Assert.That(value.ToString(), Is.EqualTo(secret));
				}

				// delete the value
				using (var tr = db.BeginTransaction())
				{
					table.Clear(tr, FdbTuple.Create("hello"));
					await tr.CommitAsync();
				}

#if DEBUG
				await TestHelpers.DumpSubspace(db, subspace);
#endif

				// verifiy that it is gone
				using (var tr = db.BeginTransaction())
				{
					var value = await table.GetAsync(tr, FdbTuple.Create("hello"));
					Assert.That(value, Is.EqualTo(Slice.Nil));

					// also check directly
					value = await tr.GetAsync(subspace.Tuple.Append("hello"));
					Assert.That(value, Is.EqualTo(Slice.Nil));
				}

			}

		}

		[Test]
		public async Task Test_FdbTable_List()
		{
			using (var db = await TestHelpers.OpenTestDatabaseAsync())
			{
				var subspace = new FdbSubspace(FdbTuple.Create("TestTable"));

				// clear previous values
				await TestHelpers.DeleteSubspace(db, subspace);

				var table = new FdbTable(subspace);

				// write a bunch of keys
				using (var tr = db.BeginTransaction())
				{
					table.Set(tr, FdbTuple.Create("foo"), Slice.FromString("foo_value"));
					table.Set(tr, FdbTuple.Create("bar"), Slice.FromString("bar_value"));

					await tr.CommitAsync();
				}

#if DEBUG
				await TestHelpers.DumpSubspace(db, subspace);
#endif

				// read them back

				using (var tr = db.BeginTransaction())
				{
					var value = await table.GetAsync(tr, FdbTuple.Create("foo"));
					Assert.That(value, Is.Not.EqualTo(Slice.Nil));
					Assert.That(value.ToString(), Is.EqualTo("foo_value"));

					value = await table.GetAsync(tr, FdbTuple.Create("bar"));
					Assert.That(value, Is.Not.EqualTo(Slice.Nil));
					Assert.That(value.ToString(), Is.EqualTo("bar_value"));

					value = await table.GetAsync(tr, FdbTuple.Create("baz"));
					Assert.That(value, Is.EqualTo(Slice.Nil));

				}

			}
		}

	}

}
