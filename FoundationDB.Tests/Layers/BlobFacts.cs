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

namespace FoundationDB.Layers.Blobs.Tests
{
	using FoundationDB.Client;
	using FoundationDB.Client.Tests;
	using NUnit.Framework;
	using System;
	using System.Threading.Tasks;

	[TestFixture]
	public class BlobFacts
	{

		[Test]
		public async Task Test_FdbBlob_NotFound_Blob_Is_Empty()
		{
			using (var db = await TestHelpers.OpenTestDatabaseAsync())
			{
				var location = db.Partition("BlobsFromOuterSpace");

				// clear previous values
				await TestHelpers.DeleteSubspace(db, location);

				var blob = new FdbBlob(location.Partition("Empty"));

				using (var tr = db.BeginTransaction())
				{
					long? size = await blob.GetSizeAsync(tr);
					Assert.That(size, Is.Null, "Non existing blob should have no size");
				}

			}
		}

		[Test]
		public async Task Test_FdbBlob_Can_AppendToBlob()
		{
			using (var db = await TestHelpers.OpenTestDatabaseAsync())
			{
				var location = db.Partition("BlobsFromOuterSpace");

				// clear previous values
				await TestHelpers.DeleteSubspace(db, location);

				var blob = new FdbBlob(location.Partition("BobTheBlob"));

				using (var tr = db.BeginTransaction())
				{
					await blob.AppendAsync(tr, Slice.FromString("Attack"));
					await blob.AppendAsync(tr, Slice.FromString(" of the "));
					await blob.AppendAsync(tr, Slice.FromString("Blobs!"));

					await tr.CommitAsync();
				}

#if DEBUG
				await TestHelpers.DumpSubspace(db, location);
#endif

				using(var tr = db.BeginTransaction())
				{
					long? size = await blob.GetSizeAsync(tr);
					Assert.That(size, Is.EqualTo(20));

					Slice data = await blob.ReadToEndAsync(tr);
					Assert.That(data.ToUnicode(), Is.EqualTo("Attack of the Blobs!"));
				}

			}
		}

		[Test]
		public async Task Test_FdbBlob_CanAppendLargeChunks()
		{
			using (var db = await TestHelpers.OpenTestDatabaseAsync())
			{
				var location = db.Partition("BlobsFromOuterSpace");

				// clear previous values
				await TestHelpers.DeleteSubspace(db, location);

				var blob = new FdbBlob(location.Partition("BigBlob"));

				var data = new byte[100 * 1000];
				for (int i = 0; i < data.Length; i++) data[i] = (byte)i;

				for (int i = 0; i < 50; i++)
				{
					using (var tr = db.BeginTransaction())
					{
						await blob.AppendAsync(tr, Slice.Create(data));
						await tr.CommitAsync();
					}
				}

				using (var tr = db.BeginTransaction())
				{
					long? size = await blob.GetSizeAsync(tr);
					Assert.That(size, Is.EqualTo(50 * data.Length));

					var s = await blob.ReadAsync(tr, 1234567, 1 * 1000 * 1000);
					Assert.That(s.Count, Is.EqualTo(1 * 1000 * 1000));

					// should contains the correct data
					for (int i = 0; i < s.Count; i++)
					{
						if (s.Array[i + s.Offset] != (byte)((1234567 + i) % data.Length)) Assert.Fail("Corrupted blob chunk at " + i + ": " + s[i, i + 128].ToString());
					}
				}

			}
		}

		[Test]
		public async Task Test_FdbBlob_Can_Set_Attributes()
		{
			using (var db = await TestHelpers.OpenTestDatabaseAsync())
			{
				var location = db.Partition("BlobsFromOuterSpace");

				// clear previous values
				await TestHelpers.DeleteSubspace(db, location);

				var blob = new FdbBlob(location.Partition("Blob"));

				DateTime created = DateTime.UtcNow;
				using (var tr = db.BeginTransaction())
				{
					await blob.AppendAsync(tr, Slice.FromString("This is the value of the blob."));
					blob.SetAttribute(tr, "LastUpdated", Slice.FromInt64(created.Ticks));
					await tr.CommitAsync();
				}

#if DEBUG
				await TestHelpers.DumpSubspace(db, location);
#endif
				using (var tr = db.BeginTransaction())
				{
					var value = await blob.GetAttributeAsync(tr, "LastUpdated");
					Assert.That(value.HasValue, Is.True, "Attribute should exist");
					Assert.That(value.ToInt64(), Is.EqualTo(created.Ticks));
				}

				DateTime updated = DateTime.UtcNow;
				using (var tr = db.BeginTransaction())
				{
					await blob.AppendAsync(tr, Slice.FromString(" With some extra bytes."));
					blob.SetAttribute(tr, "LastUpdated", Slice.FromInt64(updated.Ticks));
					await tr.CommitAsync();
				}

#if DEBUG
				await TestHelpers.DumpSubspace(db, location);
#endif
				using (var tr = db.BeginTransaction())
				{
					var value = await blob.GetAttributeAsync(tr, "LastUpdated");
					Assert.That(value.HasValue, Is.True, "Attribute should exist");
					Assert.That(value.ToInt64(), Is.Not.EqualTo(created.Ticks), "Attribute should have changed");
					Assert.That(value.ToInt64(), Is.EqualTo(updated.Ticks));
				}
			}
		}

	}

}
