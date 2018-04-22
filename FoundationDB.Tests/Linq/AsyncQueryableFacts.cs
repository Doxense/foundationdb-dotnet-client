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

namespace FoundationDB.Linq.Tests
{
	using System;
	using System.Collections.Generic;
	using System.Threading.Tasks;
	using FoundationDB.Client;
	using FoundationDB.Client.Tests;
	using FoundationDB.Layers.Indexing;
	using FoundationDB.Linq.Expressions;
	using FoundationDB.Linq.Providers;
	using NUnit.Framework;

	[TestFixture]
	public class AsyncQueryableFacts : FdbTest
	{

		[Test]
		public async Task Test_AsyncQueryable_Basics()
		{

			using(var db = await OpenTestPartitionAsync())
			{

				var location = db.Partition.ByKey("Linq");

				await db.ClearRangeAsync(location, this.Cancellation);

				await db.WriteAsync((tr) =>
				{
					tr.Set(location.Keys.Encode("Hello"), Slice.FromString("World!"));
					tr.Set(location.Keys.Encode("Narf"), Slice.FromString("Zort"));
				}, this.Cancellation);

				var range = db.Query().RangeStartsWith(location.GetPrefix());
				Assert.That(range, Is.InstanceOf<FdbAsyncSequenceQuery<KeyValuePair<Slice, Slice>>>());
				Assert.That(range.Expression, Is.InstanceOf<FdbQueryRangeExpression>());
				Log(range.Expression.DebugView);

				var projection = range.Select(kvp => kvp.Value.ToString());
				Assert.That(projection, Is.InstanceOf<FdbAsyncSequenceQuery<string>>());
				Assert.That(projection.Expression, Is.InstanceOf<FdbQueryTransformExpression<KeyValuePair<Slice, Slice>, string>>());
				Log(projection.Expression.DebugView);

				var results = await projection.ToListAsync();
				Log("ToListAsync() => [ " + String.Join(", ", results) + " ]");

				var count = await projection.CountAsync();
				Log("CountAsync() => " + count); 
				Assert.That(count, Is.EqualTo(2));

				var first = await projection.FirstAsync();
				Log("FirstAsync() => " + first);
				Assert.That(first, Is.EqualTo("World!"));
			}
		}

		[Test]
		public async Task Test_Query_Index_Single()
		{
			using (var db = await OpenTestPartitionAsync())
			{

				var location = db.Partition.ByKey("Linq");

				await db.ClearRangeAsync(location, this.Cancellation);

				var index = new FdbIndex<long, string>("Foos.ByColor", location.Partition.ByKey("Foos", "ByColor"));

				await db.WriteAsync((tr) =>
				{
					index.Add(tr, 1, "red");
					index.Add(tr, 2, "green");
					index.Add(tr, 3, "blue");
					index.Add(tr, 4, "red");
				}, this.Cancellation);

				// find all elements that are read
				var lookup = index.Query(db).Lookup(x => x == "red");

				Assert.That(lookup, Is.InstanceOf<FdbAsyncSequenceQuery<long>>());
				Assert.That(lookup.Expression, Is.InstanceOf<FdbQueryIndexLookupExpression<long, string>>());
				Log(lookup.Expression.DebugView);

				var ids = await lookup.ToListAsync();
				Log("=> [ " + String.Join(", ", ids) + " ]");

			}

		}

		[Test]
		public async Task Test_Query_Index_Range()
		{
			using (var db = await OpenTestPartitionAsync())
			{

				var location = db.Partition.ByKey("Linq");

				await db.ClearRangeAsync(location, this.Cancellation);

				var index = new FdbIndex<string, int>("Bars.ByScore", location.Partition.ByKey("Foos", "ByScore"));

				await db.WriteAsync((tr) =>
				{
					index.Add(tr, "alpha", 10);
					index.Add(tr, "bravo", 16);
					index.Add(tr, "charly", 12);
					index.Add(tr, "echo", 666);
					index.Add(tr, "foxtrot", 54321);
					index.Add(tr, "golf", 768);
					index.Add(tr, "tango", 12345);
					index.Add(tr, "sierra", 667);
					index.Add(tr, "victor", 1234);
					index.Add(tr, "whisky", 9001);
				}, this.Cancellation);

				// find all up to 100
				var lookup = index.Query(db).Lookup(x => x <= 100);
				Assert.That(lookup, Is.InstanceOf<FdbAsyncSequenceQuery<string>>());
				Assert.That(lookup.Expression, Is.InstanceOf<FdbQueryIndexLookupExpression<string, int>>());
				Log(lookup.Expression.DebugView);

				var ids = await lookup.ToListAsync();
				Log("=> [ " + String.Join(", ", ids) + " ]");
				
				// find all that are over nine thousand
				lookup = index.Query(db).Lookup(x => x >= 9000);
				Assert.That(lookup, Is.InstanceOf<FdbAsyncSequenceQuery<string>>());
				Assert.That(lookup.Expression, Is.InstanceOf<FdbQueryIndexLookupExpression<string, int>>());
				Log(lookup.Expression.DebugView);

				ids = await lookup.ToListAsync();
				Log("=> [ " + String.Join(", ", ids) + " ]");

			}

		}

	}

}
