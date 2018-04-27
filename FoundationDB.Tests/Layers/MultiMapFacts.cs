#region BSD Licence
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
	using Doxense.Serialization.Encoders;
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

				var location = await GetCleanDirectory(db, "Collections", "MultiMaps");

				var map = new FdbMultiMap<string, string>(location.Partition.ByKey("Foos"), allowNegativeValues: false);

				// read non existing value
				using (var tr = db.BeginTransaction(this.Cancellation))
				{
					bool res = await map.ContainsAsync(tr, "hello", "world");
					Assert.That(res, Is.False, "ContainsAsync('hello','world')");

					long? count = await map.GetCountAsync(tr, "hello", "world");
					Assert.That(count, Is.Null, "GetCountAsync('hello', 'world')");
				}

				// add some values
				using (var tr = db.BeginTransaction(this.Cancellation))
				{
					await map.AddAsync(tr, "hello", "world");
					await map.AddAsync(tr, "foo", "bar");
					await map.AddAsync(tr, "foo", "baz");
					await tr.CommitAsync();
				}

#if DEBUG
				await DumpSubspace(db, location);
#endif

				// read values back
				using (var tr = db.BeginTransaction(this.Cancellation))
				{
					long? count = await map.GetCountAsync(tr, "hello", "world");
					Assert.That(count, Is.EqualTo(1), "hello:world");
					count = await map.GetCountAsync(tr, "foo", "bar");
					Assert.That(count, Is.EqualTo(1), "foo:bar");
					count = await map.GetCountAsync(tr, "foo", "baz");
					Assert.That(count, Is.EqualTo(1), "foo:baz");
				}

				// directly read the value, behind the table's back
				using (var tr = db.BeginTransaction(this.Cancellation))
				{
					var loc = map.Subspace.AsDynamic();
					var value = await tr.GetAsync(loc.Keys.Encode("hello", "world"));
					Assert.That(value, Is.Not.EqualTo(Slice.Nil));
					Assert.That(value.ToInt64(), Is.EqualTo(1));
				}

				// delete the value
				using (var tr = db.BeginTransaction(this.Cancellation))
				{
					map.Remove(tr, "hello", "world");
					await tr.CommitAsync();
				}

#if DEBUG
				await DumpSubspace(db, location);
#endif

				// verifiy that it is gone
				using (var tr = db.BeginTransaction(this.Cancellation))
				{
					long? count = await map.GetCountAsync(tr, "hello", "world");
					Assert.That(count, Is.Null);

					// also check directly
					var loc = map.Subspace.AsDynamic();
					var data = await tr.GetAsync(loc.Keys.Encode("hello", "world"));
					Assert.That(data, Is.EqualTo(Slice.Nil));
				}

			}

		}

	}

}
