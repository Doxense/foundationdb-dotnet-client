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

namespace FoundationDB.Layers.Tables.Tests
{
	using System.Collections.Generic;
	using System.Linq;
	using System.Threading.Tasks;
	using System;
	using Doxense.Linq;
	using FoundationDB.Client;
	using FoundationDB.Client.Tests;
	using FoundationDB.Layers.Indexing;
	using NUnit.Framework;

	[TestFixture]
	public class IndexingFacts : FdbTest
	{

		[Test]
		public async Task Task_Can_Add_Update_Remove_From_Index()
		{

			using (var db = await OpenTestPartitionAsync())
			{
				var location = db.Partition.ByKey("Indexing");

				// clear previous values
				await DeleteSubspace(db, location);


				var subspace = location.Partition.ByKey("FoosByColor");
				var index = new FdbIndex<int, string>("Foos.ByColor", subspace);

				// add items to the index
				await db.WriteAsync((tr) =>
				{
					index.Add(tr, 1, "red");
					index.Add(tr, 2, "green");
					index.Add(tr, 3, "blue");
					index.Add(tr, 4, "green");
					index.Add(tr, 5, "yellow");
				}, this.Cancellation);

#if DEBUG
				await DumpSubspace(db, subspace);
#endif

				// lookup values

				using (var tr = db.BeginTransaction(this.Cancellation))
				{
					var reds = await index.LookupAsync(tr, "red");
					Assert.That(reds, Is.EqualTo(new int[] { 1 }));

					var greens = await index.LookupAsync(tr, "green");
					Assert.That(greens, Is.EqualTo(new int[] { 2, 4 }));

					var blues = await index.LookupAsync(tr, "blue");
					Assert.That(blues, Is.EqualTo(new int[] { 3 }));

					var yellows = await index.LookupAsync(tr, "yellow");
					Assert.That(yellows, Is.EqualTo(new int[] { 5 }));
				}

				// update

				await db.WriteAsync((tr) =>
				{
					index.Update(tr, 3, "indigo", "blue");
					index.Remove(tr, 5, "yellow");
				}, this.Cancellation);

#if DEBUG
				await DumpSubspace(db, subspace);
#endif

				// check values

				using (var tr = db.BeginTransaction(this.Cancellation))
				{
					var reds = await index.LookupAsync(tr, "red");
					Assert.That(reds, Is.EqualTo(new int[] { 1 }));

					var greens = await index.LookupAsync(tr, "green");
					Assert.That(greens, Is.EqualTo(new int[] { 2, 4 }));

					var blues = await index.LookupAsync(tr, "blue");
					Assert.That(blues.Count, Is.EqualTo(0));

					var yellows = await index.LookupAsync(tr, "yellow");
					Assert.That(yellows.Count, Is.EqualTo(0));

					var indigos = await index.LookupAsync(tr, "indigo");
					Assert.That(indigos, Is.EqualTo(new int[] { 3 }));
				}

			}

		}

		[Test]
		public async Task Test_Can_Combine_Indexes()
		{

			using (var db = await OpenTestPartitionAsync())
			{

				var location = await GetCleanDirectory(db, "Indexing");

				// clear previous values
				await DeleteSubspace(db, location);

				// summon our main cast
				var characters = new List<Character>()
				{
					new Character { Id = 1, Name = "Super Man", Brand="DC", HasSuperPowers = true, IsVilain = false },
					new Character { Id = 2, Name = "Batman", Brand="DC", IsVilain = false },
					new Character { Id = 3, Name = "Joker", Brand="DC", IsVilain = true },
					new Character { Id = 4, Name = "Iron Man", Brand="Marvel", IsVilain = false },
					new Character { Id = 5, Name = "Magneto", Brand="Marvel", HasSuperPowers = true, IsVilain = true },
					new Character { Id = 6, Name = "Catwoman", Brand="DC", IsVilain = default(bool?) },
				};

				var indexBrand = new FdbIndex<long, string>("Heroes.ByBrand", location.Partition.ByKey("CharactersByBrand"));
				var indexSuperHero = new FdbIndex<long, bool>("Heroes.BySuper", location.Partition.ByKey("SuperHeros"));
				var indexAlignment = new FdbIndex<long, bool?>("Heros.ByAlignment", location.Partition.ByKey("FriendsOrFoe"));

				// index everything
				await db.WriteAsync((tr) =>
				{
					foreach (var character in characters)
					{
						indexBrand.Add(tr, character.Id, character.Brand);
						indexSuperHero.Add(tr, character.Id, character.HasSuperPowers);
						indexAlignment.Add(tr, character.Id, character.IsVilain);
					}
				}, this.Cancellation);

#if DEBUG
				await DumpSubspace(db, location);
#endif

				// super hereos only (sorry Batman!)
				using (var tr = db.BeginTransaction(this.Cancellation))
				{
					var superHeroes = await indexSuperHero.LookupAsync(tr, value: true);
					Console.WriteLine("SuperHeroes: " + string.Join(", ", superHeroes));
					Assert.That(superHeroes, Is.EqualTo(characters.Where(c => c.HasSuperPowers).Select(c => c.Id).ToList()));
				}

				// Versus !
				using (var tr = db.BeginTransaction(this.Cancellation))
				{
					var dc = await indexBrand.LookupAsync(tr, value: "DC");
					Console.WriteLine("DC: " + string.Join(", ", dc));
					Assert.That(dc, Is.EqualTo(characters.Where(c => c.Brand == "DC").Select(c => c.Id).ToList()));

					var marvel = await indexBrand.LookupAsync(tr, value: "Marvel");
					Console.WriteLine("Marvel: " + string.Join(", ", dc));
					Assert.That(marvel, Is.EqualTo(characters.Where(c => c.Brand == "Marvel").Select(c => c.Id).ToList()));
				}

				// Vilains with superpowers are the worst
				using (var tr = db.BeginTransaction(this.Cancellation))
				{
					var first = indexAlignment.Lookup(tr, value: true);
					var second = indexSuperHero.Lookup(tr, value: true);

					var merged = await first
						.Intersect(second)
						.ToListAsync();

					Assert.That(merged.Count, Is.EqualTo(1));
					Assert.That(merged[0] == characters.Single(c => c.Name == "Magneto").Id);
				}
			}

		}

		private sealed class Character
		{
			public long Id { get; set; }

			public string Name { get; set; }

			public string Brand { get; set; }

			public bool HasSuperPowers { get; set; }

			public bool? IsVilain { get; set; }
		}

	}

}
