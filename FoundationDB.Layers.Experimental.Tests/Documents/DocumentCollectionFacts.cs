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

namespace FoundationDB.Layers.Documents.Tests
{
	using System;
	using System.Threading.Tasks;
	using FoundationDB.Client.Tests;
	using FoundationDB.Types.Json;
	using FoundationDB.Types.ProtocolBuffers;
	using NUnit.Framework;

	[TestFixture]
	public class DocumentCollectionFacts : FdbTest
	{

		private class Book
		{
			public int Id { get; set; }
			public string Title { get; set; }
			public string Author { get; set; }
			public DateTime Published { get; set; }
			public int Pages { get; set; }
		}

		private static Book[] GetBooks()
		{
			return new Book[]
			{
				new Book
				{
					Id = 42,
					Title = "On the Origin of Species",
					Author = "Charles Darwin",
					Published = new DateTime(1859, 11, 24),
					Pages = 502,
				},
				new Book
				{
					Id = 43,
					Title = "Foundation and Empire",
					Author = "Isaac Asimov",
					Published = new DateTime(1952, 1, 1),
					Pages = 247
				}
			};
		}

		[Test]
		public async Task Test_Can_Insert_And_Retrieve_Json_Documents()
		{
			using (var db = await OpenTestPartitionAsync())
			{
				var location = db.Directory["Books"]["JSON"];
				await CleanLocation(db, location);

				var docs = new FdbDocumentCollection<Book, int>(
					location,
					(book) => book.Id,
					new JsonNetCodec<Book>()
				);

				var books = GetBooks();

				// store a document
				var book1 = books[0];
				await db.ReadWriteAsync((tr) => docs.InsertAsync(tr, book1), this.Cancellation);
#if DEBUG
				await DumpSubspace(db, location);
#endif

				// retrieve the document
				var copy = await db.ReadAsync((tr) =>docs.LoadAsync(tr, book1.Id), this.Cancellation);

				Assert.That(copy, Is.Not.Null);
				Assert.That(copy.Id, Is.EqualTo(book1.Id));
				Assert.That(copy.Title, Is.EqualTo(book1.Title));
				Assert.That(copy.Author, Is.EqualTo(book1.Author));
				Assert.That(copy.Published, Is.EqualTo(book1.Published));
				Assert.That(copy.Pages, Is.EqualTo(book1.Pages));

				// store another document
				var book2 = books[1];
				await db.ReadWriteAsync((tr) => docs.InsertAsync(tr, book2), this.Cancellation);
#if DEBUG
				await DumpSubspace(db, location);
#endif
			}
		}

		[Test]
		public async Task Test_Can_Insert_And_Retrieve_ProtoBuf_Documents()
		{
			using (var db = await OpenTestPartitionAsync())
			{
				var location = db.Directory["Books"]["ProtoBuf"];
				await CleanLocation(db, location);

				// quickly define the metatype for Books, because I'm too lazy to write a .proto for this, or add [ProtoMember] attributes everywhere
				var metaType = ProtoBuf.Meta.RuntimeTypeModel.Default.Add(typeof(Book), false);
				metaType.Add("Id", "Title", "Author", "Published", "Pages");
				metaType.CompileInPlace();

				var docs = new FdbDocumentCollection<Book, int>(
					location,
					(book) => book.Id,
					new ProtobufCodec<Book>()
				);

				var books = GetBooks();

				// store a document
				var book1 = books[0];
				await db.ReadWriteAsync((tr) => docs.InsertAsync(tr, book1), this.Cancellation);
#if DEBUG
				await DumpSubspace(db, location);
#endif

				// retrieve the document
				var copy = await db.ReadAsync((tr) => docs.LoadAsync(tr, 42), this.Cancellation);

				Assert.That(copy, Is.Not.Null);
				Assert.That(copy.Id, Is.EqualTo(book1.Id));
				Assert.That(copy.Title, Is.EqualTo(book1.Title));
				Assert.That(copy.Author, Is.EqualTo(book1.Author));
				Assert.That(copy.Published, Is.EqualTo(book1.Published));
				Assert.That(copy.Pages, Is.EqualTo(book1.Pages));

				// store another document
				var book2 = books[1];
				await db.ReadWriteAsync((tr) => docs.InsertAsync(tr, book2), this.Cancellation);
#if DEBUG
				await DumpSubspace(db, location);
#endif
			}
		}

	}

}
