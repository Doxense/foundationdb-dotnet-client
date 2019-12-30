#region BSD License
/* Copyright (c) 2013-2020, Doxense SAS
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
	using System;
	using System.Threading.Tasks;
	using FoundationDB.Client;
	using FoundationDB.Client.Tests;
	using NUnit.Framework;

	[TestFixture]
	public class BlobFacts : FdbTest
	{

		[Test]
		public async Task Test_FdbBlob_NotFound_Blob_Is_Empty()
		{
			using (var db = await OpenTestPartitionAsync())
			{
				var location = db.Directory["BlobsFromOuterSpace"];
				await CleanLocation(db, location);

				var blob = new FdbBlob(location.ByKey("Empty"));

				long? size;

				using (var tr = await db.BeginReadOnlyTransactionAsync(this.Cancellation))
				{
					var metadata = await blob.Resolve(tr);

					size = await metadata.GetSizeAsync(tr);
					Assert.That(size, Is.Null, "Non existing blob should have no size");
				}

				size = await db.ReadAsync(async (tr) =>
				{
					var metadata = await blob.Resolve(tr);
					return await metadata.GetSizeAsync(tr);
				}, this.Cancellation);
				Assert.That(size, Is.Null, "Non existing blob should have no size");

			}
		}

		[Test]
		public async Task Test_FdbBlob_Can_AppendToBlob()
		{
			using (var db = await OpenTestPartitionAsync())
			{
				var location = db.Directory["BlobsFromOuterSpace"];
				await CleanLocation(db, location);

				var blob = new FdbBlob(location.ByKey("BobTheBlob"));

				using (var tr = await db.BeginTransactionAsync(this.Cancellation))
				{
					var metadata = await blob.Resolve(tr);

					await metadata.AppendAsync(tr, Value("Attack"));
					await metadata.AppendAsync(tr, Value(" of the "));
					await metadata.AppendAsync(tr, Value("Blobs!"));

					await tr.CommitAsync();
				}

#if DEBUG
				await DumpSubspace(db, location);
#endif

				using (var tr = await db.BeginTransactionAsync(this.Cancellation))
				{
					var metadata = await blob.Resolve(tr);

					long? size = await metadata.GetSizeAsync(tr);
					Assert.That(size, Is.EqualTo(20));

					var data = await metadata.ReadAsync(tr, 0, (int)(size ?? 0));
					Assert.That(data.ToUnicode(), Is.EqualTo("Attack of the Blobs!"));
				}

			}
		}

		[Test]
		public async Task Test_FdbBlob_CanAppendLargeChunks()
		{
			using (var db = await OpenTestPartitionAsync())
			{
				var location = db.Directory["BlobsFromOuterSpace"];
				await CleanLocation(db, location);

				var blob = new FdbBlob(location.ByKey("BigBlob"));

				var data = new byte[100 * 1000];
				for (int i = 0; i < data.Length; i++) data[i] = (byte)i;

				for (int i = 0; i < 50; i++)
				{
					using (var tr = await db.BeginTransactionAsync(this.Cancellation))
					{
						var metadata = await blob.Resolve(tr);
						await metadata.AppendAsync(tr, data.AsSlice());
						await tr.CommitAsync();
					}
				}

				using (var tr = await db.BeginTransactionAsync(this.Cancellation))
				{
					var metadata = await blob.Resolve(tr);

					long? size = await metadata.GetSizeAsync(tr);
					Assert.That(size, Is.EqualTo(50 * data.Length));

					var s = await metadata.ReadAsync(tr, 1234567, 1 * 1000 * 1000);
					Assert.That(s.Count, Is.EqualTo(1 * 1000 * 1000));

					// should contains the correct data
					for (int i = 0; i < s.Count; i++)
					{
						if (s.Array[i + s.Offset] != (byte)((1234567 + i) % data.Length)) Assert.Fail("Corrupted blob chunk at " + i + ": " + s[i, i + 128].ToString());
					}
				}

			}
		}

	}

}
