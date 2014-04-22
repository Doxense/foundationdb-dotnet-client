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

namespace FoundationDB.Layers.Documents
{
	using FoundationDB.Client;
	using FoundationDB.Client.Utils;
	using FoundationDB.Layers.Tuples;
	using FoundationDB.Linq;
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using System.Threading;
	using System.Threading.Tasks;

	/// <summary>Interface that defines a class that knows of to chop instances of <typeparamref name="TDocument"/> into slices</summary>
	/// <typeparam name="TDocument">Type of documents</typeparam>
	public interface IDocumentSplitter<in TDocument>
	{
		KeyValuePair<IFdbTuple, Slice>[] Split(TDocument document);
	}

	/// <summary>Interface that defines a class that knows of to reconstruct instances of <typeparamref name="TDocument"/> from slices</summary>
	/// <typeparam name="TDocument">Type of documents</typeparam>
	public interface IDocumentBuilder<out TDocument>
	{

		TDocument Build(KeyValuePair<IFdbTuple, Slice>[] parts);
	}

	/// <summary>Interface that defines a class that knows of to store and retrieve serialized versions of <typeparamref name="TDocument"/> instances into a document collection</summary>
	/// <typeparam name="TDocument">Type of documents that are being processed</typeparam>
	/// <typeparam name="TId">Type of the unique identifier of each documents</typeparam>
	/// <typeparam name="TInternal">Type of the internal representation of a document while being sliced/reconstructed</typeparam>
	public interface IDocumentHandler<TDocument, TId, TInternal> : IDocumentSplitter<TInternal>, IDocumentBuilder<TInternal>
	{
		/// <summary>Prepare a document by packing it into the internal storage representation</summary>
		/// <param name="document">Document that must be written to storage</param>
		/// <returns>Internal pre-serialized version of the document</returns>
		TInternal Pack(TDocument document);

		/// <summary>Return the unique identifier of a packed document</summary>
		/// <param name="packed">Packed document (result of calling Pack on the document instance)</param>
		/// <returns>Unique identifier of the document</returns>
		TId GetId(TDocument packed);

		/// <summary>Reconstruct a document from a packed version and the unique identifier</summary>
		/// <param name="packed">Packed document (read from the database)</param>
		/// <param name="id">Unique identifier of the document</param>
		/// <returns>Reconstructed instance of the document</returns>
		TDocument Unpack(TInternal packed, TId id);
	}

	/// <summary>Collection of generic document handlers</summary>
	public static class FdbDocumentHandlers
	{

		/// <summary>Docuemnt handler that handle dictionarys of string to objects</summary>
		/// <typeparam name="TDictionary"></typeparam>
		/// <typeparam name="TId"></typeparam>
		public sealed class DictionaryHandler<TDictionary, TId> : IDocumentHandler<TDictionary, TId, List<KeyValuePair<string, IFdbTuple>>>
			where TDictionary : IDictionary<string, object>, new()
		{

			public DictionaryHandler(string idName = null, IEqualityComparer<string> comparer = null)
			{
				m_keyComparer = comparer ?? EqualityComparer<string>.Default;
				this.IdName = idName ?? "id";
			}

			private IEqualityComparer<string> m_keyComparer;

			public string IdName { get; private set; }

			public KeyValuePair<IFdbTuple, Slice>[] Split(List<KeyValuePair<string, IFdbTuple>> document)
			{
				if (document == null) throw new ArgumentNullException("document");

				return document
					// don't include the id
					.Where(kvp => !m_keyComparer.Equals(kvp.Key, this.IdName))
					// convert into tuples
					.Select(kvp => new KeyValuePair<IFdbTuple, Slice>(
						FdbTuple.Create(kvp.Key),
						FdbTuple.Create(kvp.Value).ToSlice()
					))
					.ToArray();
			}

			public List<KeyValuePair<string, IFdbTuple>> Build(KeyValuePair<IFdbTuple, Slice>[] parts)
			{
				if (parts == null) throw new ArgumentNullException("parts");

				var list = new List<KeyValuePair<string, IFdbTuple>>(parts.Length);
				foreach(var part in parts)
				{
					list.Add(new KeyValuePair<string, IFdbTuple>(
						part.Key.Last<string>(),
						FdbTuple.Unpack(part.Value)
					));
				}
				return list;
			}

			public TId GetId(TDictionary document)
			{
				return (TId)document[this.IdName];
			}

			public void SetId(Dictionary<string, IFdbTuple> document, TId id)
			{
				document[this.IdName] = FdbTuple.Create(id);
			}

			public List<KeyValuePair<string, IFdbTuple>> Pack(TDictionary document)
			{
				var dic = new List<KeyValuePair<string, IFdbTuple>>(document.Count);

				// convert everything, except the Id
				foreach(var kvp in document)
				{
					if (!m_keyComparer.Equals(kvp.Key, this.IdName))
					{
						dic.Add(new KeyValuePair<string, IFdbTuple>(kvp.Key, FdbTuple.Create(kvp.Key)));
					}
				}

				return dic;
			}

			public TDictionary Unpack(List<KeyValuePair<string, IFdbTuple>> packed, TId id)
			{
				var dic = new TDictionary();
				dic.Add(this.IdName, id);
				foreach(var kvp in packed)
				{
					dic.Add(kvp.Key, kvp.Value[0]);
				}
				return dic;
			}

		}

	}

}
