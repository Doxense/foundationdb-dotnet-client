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

namespace FoundationDB.Client.Tests
{
	using FoundationDB.Client;
	using FoundationDB.Layers.Tuples;
	using NUnit.Framework;
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using System.Threading;
	using System.Threading.Tasks;

	[TestFixture]
	public class DatabaseBulkFacts
	{

		[Test]
		public async Task Test_Can_Bulk_Insert()
		{
			const int N = 2000;

			using (var db = await TestHelpers.OpenTestDatabaseAsync())
			{
				Console.WriteLine("Bulk inserting " + N + " items...");

				var location = db.Partition("Bulk");

				var data = Enumerable.Range(0, N)
					.Select((x) => new KeyValuePair<Slice, Slice>(location.Pack(x.ToString("x8")), Slice.FromGuid(Guid.NewGuid())))
					.ToArray();

				Console.WriteLine("Starting...");

				long? lastReport = null;
				int called = 0;
				long count = await db.Bulk.InsertAsync(
					data,
					new Progress<long>((n) =>
					{
						++called;
						lastReport = n;
						Console.WriteLine("Chunk #" + called + " : " + n.ToString());
					}
				));

				Console.WriteLine("Done in " + called + " chunks");

				Assert.That(count, Is.EqualTo(N));
				Assert.That(lastReport, Is.EqualTo(N));
				Assert.That(called, Is.GreaterThan(0));

				// read everything back...

				Console.WriteLine("Reading everything back...");

				var stored = await db.Attempt.ReadAsync((tr) =>
				{
					return tr.GetRangeStartsWith(location).ToArrayAsync();
				});

				Assert.That(stored.Length, Is.EqualTo(N));
				Assert.That(stored, Is.EqualTo(data));
			}
		}


	}
}
