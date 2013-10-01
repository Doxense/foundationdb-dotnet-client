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

namespace FoundationDB.Layers.Documents.Tests
{
	using FoundationDB.Client;
	using FoundationDB.Client.Tests;
	using FoundationDB.Layers.Tuples;
	using Newtonsoft.Json;
	using Newtonsoft.Json.Linq;
	using NUnit.Framework;
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using System.Threading.Tasks;

	[TestFixture]
	public class DocumentCollectionFacts
	{

		public class JsonNetConverter<TDocument, TId> : IDocumentHandler<TDocument, TId, JObject>
		{

			public JsonNetConverter(Func<TDocument, TId> idSelector, string idName = null)
			{
				this.IdName = idName ?? "Id";
				this.IdSelector = idSelector;
			}

			public string IdName { get; set; }

			public Func<TDocument, TId> IdSelector { get; set; }

			public JObject Pack(TDocument document)
			{
				if (document == null) throw new ArgumentNullException("document");
				return JObject.FromObject(document);
			}

			public TId GetId(TDocument document)
			{
				if (document == null) throw new ArgumentNullException("document");
				return this.IdSelector(document);
			}

			public TDocument Unpack(JObject packed, TId id)
			{
				if (packed == null) throw new ArgumentNullException("packed");

				packed[this.IdName] = JToken.FromObject(id);
				return packed.ToObject<TDocument>();
			}

			public KeyValuePair<IFdbTuple, Slice>[] Split(JObject document)
			{
				return ((IEnumerable<KeyValuePair<string, JToken>>)document)
					.Where(kvp => kvp.Key != this.IdName)
					.Select((kvp) => new KeyValuePair<IFdbTuple, Slice>(
						FdbTuple.Create(kvp.Key),
						Serialize(kvp.Value)
					))
					.ToArray();
			}

			public JObject Build(KeyValuePair<IFdbTuple, Slice>[] parts)
			{
				var obj = new JObject();
				foreach(var part in parts)
				{
					string key = part.Key.Get<string>(0);
					JToken value = Deserialize(part.Value);
					obj.Add(key, value);
				}
				return obj;
			}

			private static Slice Serialize(JToken token)
			{
				return Slice.FromString(JsonConvert.SerializeObject(token));
			}

			private static JToken Deserialize(Slice slice)
			{
				return JToken.Parse(slice.ToUnicode());
			}

		}

		private class Book
		{
			public int Id { get; set; }
			public string Title { get; set; }
			public string Author { get; set; }
			public DateTime Published { get; set; }
			public int Pages { get; set; }
		}

		[Test]
		public async Task Test_Can_Insert_And_Retrieve_Document()
		{
			using (var db = await TestHelpers.OpenTestPartitionAsync())
			{
				var location = db.Partition("Books");

				// clear previous values
				await TestHelpers.DeleteSubspace(db, location);

				var converter = new JsonNetConverter<Book, int>(
					(book) => book.Id
				);

				var docs = new FdbDocumentCollection<Book, int, JObject>(location, converter);

				var book1 = new Book
				{
					Id = 42,
					Title = "On the Origin of Species" ,
					Author = "Charles Darwin",
					Published = new DateTime(1859, 11, 24),
					Pages = 502,
				};

				// store the document

				await docs.InsertAsync(db, book1);

#if DEBUG
				await TestHelpers.DumpSubspace(db, location);
#endif

				var copy = await docs.LoadAsync(db, 42);

				Assert.That(copy.Id, Is.EqualTo(42));
				Assert.That(copy.Title, Is.EqualTo("On the Origin of Species"));
				Assert.That(copy.Author, Is.EqualTo("Charles Darwin"));
				Assert.That(copy.Published, Is.EqualTo(new DateTime(1859, 11, 24)));
				Assert.That(copy.Pages, Is.EqualTo(502));

				var book2 = new Book
				{
					Id = 43,
					Title = "Foundation and Empire",
					Author = "Isaac Asimov",
					Published = new DateTime(1952, 1, 1),
					Pages = 247
				};

				await docs.InsertAsync(db, book2);

#if DEBUG
				await TestHelpers.DumpSubspace(db, location);
#endif

			}
		}


	}

}
