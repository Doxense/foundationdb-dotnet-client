#region Copyright (c) 2023-2024 SnowBank SAS, (c) 2005-2023 Doxense SAS
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

#if NET8_0_OR_GREATER

namespace FoundationDB.Client.Tests
{
	[TestFixture]
	[Parallelizable(ParallelScope.All)]
	public class FqlQueriesFacts : FdbTest
	{

		[Test]
		public async Task Test_Can_Filter_Directories()
		{
			var db = await OpenTestPartitionAsync();

			var location = db.Root;
			await CleanLocation(db, location);

			// create the structure of directories
			await db.WriteAsync(async tr =>
			{
				var parent = (await location.Resolve(tr, createIfMissing: true))!;

				foreach (var path in (string[])
				[
					"foo/bar/baz/jazz",
					"users/alice/documents/photos",
					"users/alice/documents/music",
					"users/bob/documents/photos",
					"users/bob/documents/music",
					"users/charlie/documents/photos",
					"users/charlie/documents/music",
				])
				{
					await parent.CreateAsync(tr, FdbPath.Relative(FdbPath.Parse(path)));
				}
			}, this.Cancellation);

			// populate with some fake data
			await db.WriteAsync(async tr =>
			{
				var rnd = new Random(123456);

				var genres = (string[]) [ "rock", "techno", "folk", "classical" ];
				
				foreach (var user in (string[]) [ "alice", "bob", "charlie" ])
				{
					var subspace = (await db.Root[FdbPath.Parse($"users/{user}/documents/music")].Resolve(tr).ConfigureAwait(false))!;
					Log($"Filling {subspace.Path} with pseudo data");

					tr.SetValueString(subspace.Encode("name"), "Albums");
					tr.SetValueInt32(subspace.Encode("count"), 100);
					
					for (int i = 0; i < 100; i++)
					{
						tr.Set(subspace.Encode(0, $"album{i:D4}"), Slice.FromString("{ /* some dummy json */ }"));
						tr.Set(subspace.Encode(1, $"album{i:D4}", Uuid128.NewUuid()), Slice.Empty);
						tr.Set(subspace.Encode(2, $"album{i:D4}", genres[rnd.Next(genres.Length)]), Slice.Empty);
						tr.Set(subspace.Encode(3, $"album{i:D4}", 1970 + rnd.Next(55)), Slice.Empty);
					}
				}
			}, this.Cancellation);

			//await DumpTree(db, location);

			await db.ReadAsync(async tr =>
			{
				// find all the "techno" albums from all the users
				var q = FqlQueryParser.Parse("./users/<>/documents/music(2,<string>,\"techno\")");
				Assert.That(q, Is.Not.Null);
				Log(q.Explain());

				Log("Listing matching folders:");
				await foreach (var match in q.Scan(tr, db.Root))
				{
					Log($"- {match.Path}{match.Tuple} = {match.Value:V}");
				}

			}, this.Cancellation);

		}

	}

}

#endif
