#region BSD License
/* Copyright (c) 2013-2018, Doxense SAS
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

namespace FoundationDB.Layers.Collections.Tests
{
	using System;
	using System.Threading.Tasks;
	using FoundationDB.Client;
	using FoundationDB.Client.Tests;
	using NUnit.Framework;

	[TestFixture]
	public class MultiMapFacts : FdbTest
	{

		[Test]
		public async Task Test_FdbMultiMap_Read_Write_Delete()
		{

			using (var db = await OpenTestPartitionAsync())
			{

				var location = db.Root["Collections"]["MultiMaps"];
				await CleanLocation(db, location);

				var mapFoos = new FdbMultiMap<string, string>(location.ByKey("Foos"), allowNegativeValues: false);

				// read non existing value
				using (var tr = await db.BeginTransactionAsync(this.Cancellation))
				{
					var foos = await mapFoos.Resolve(tr);
					bool res = await foos.ContainsAsync(tr, "hello", "world");
					Assert.That(res, Is.False, "ContainsAsync('hello','world')");

					long? count = await foos.GetCountAsync(tr, "hello", "world");
					Assert.That(count, Is.Null, "GetCountAsync('hello', 'world')");
				}

				// add some values
				await db.WriteAsync(async tr =>
				{
					var foos = await mapFoos.Resolve(tr);
					await foos.AddAsync(tr, "hello", "world");
					await foos.AddAsync(tr, "foo", "bar");
					await foos.AddAsync(tr, "foo", "baz");
				}, this.Cancellation);

#if DEBUG
				await DumpSubspace(db, location);
#endif

				// read values back
				await db.ReadAsync(async tr =>
				{
					var foos = await mapFoos.Resolve(tr);
					long? count = await foos.GetCountAsync(tr, "hello", "world");
					Assert.That(count, Is.EqualTo(1), "hello:world");
					count = await foos.GetCountAsync(tr, "foo", "bar");
					Assert.That(count, Is.EqualTo(1), "foo:bar");
					count = await foos.GetCountAsync(tr, "foo", "baz");
					Assert.That(count, Is.EqualTo(1), "foo:baz");
				}, this.Cancellation);

				// directly read the value, behind the table's back
				await db.ReadAsync(async tr =>
				{
					var foos = await mapFoos.Resolve(tr);
					var loc = foos.Subspace.AsDynamic();
					var value = await tr.GetAsync(loc.Encode("hello", "world"));
					Assert.That(value, Is.Not.EqualTo(Slice.Nil));
					Assert.That(value.ToInt64(), Is.EqualTo(1));
				}, this.Cancellation);

				// delete the value
				await db.WriteAsync(async tr =>
				{
					var foos = await mapFoos.Resolve(tr);
					foos.Remove(tr, "hello", "world");
				}, this.Cancellation);

#if DEBUG
				await DumpSubspace(db, location);
#endif

				// verify that it is gone
				await db.ReadAsync(async tr =>
				{
					var foos = await mapFoos.Resolve(tr);

					long? count = await foos.GetCountAsync(tr, "hello", "world");
					Assert.That(count, Is.Null);

					// also check directly
					var loc = foos.Subspace.AsDynamic();
					var data = await tr.GetAsync(loc.Encode("hello", "world"));
					Assert.That(data, Is.EqualTo(Slice.Nil));
				}, this.Cancellation);

			}

		}

	}

}
