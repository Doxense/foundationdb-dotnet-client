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

			await DumpTree(db, location);

			await db.ReadAsync(async tr =>
			{
				// since our test folder starts already in a deep /Tests/..../ path, we need to add it to all the queries!

				var root = location.Path.ToString("N");
				var q = FqlQueryParser.Parse(root + "/users/<>/documents");
				Assert.That(q, Is.Not.Null);
				Log(q.Explain());

				Log("Listing matching folders:");
				var matches = await q.EnumerateDirectories(tr).ToListAsync();
				Log($"> found {matches.Count}");
				foreach (var match in matches)
				{
					Log($"- {match}");
				}
				Assert.That(matches, Has.Count.EqualTo(3));

			}, this.Cancellation);

		}

	}

}

#endif
