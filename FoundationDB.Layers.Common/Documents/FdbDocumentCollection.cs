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

	/// <summary>Represents a collection of dictionaries of fields.</summary>
	public class FdbDocumentCollection<TDocument, TId, TInternal>
		where TDocument : class
		where TInternal : class
	{

		public FdbDocumentCollection(FdbSubspace subspace, IDocumentHandler<TDocument, TId, TInternal> converter)
		{
			if (subspace == null) throw new ArgumentNullException("subspace");
			if (converter == null) throw new ArgumentNullException("converter");

			this.Subspace = subspace;
			this.Converter = converter;
		}

		/// <summary>Subspace used as a prefix for all hashsets in this collection</summary>
		public FdbSubspace Subspace { get; private set; }


		/// <summary>Return the id of a document</summary>
		public IDocumentHandler<TDocument, TId, TInternal> Converter { get; private set; }

		/// <summary>Returns the key prefix of an HashSet: (subspace, id, )</summary>
		/// <param name="id"></param>
		/// <returns></returns>
		protected virtual IFdbTuple GetDocumentPrefix(TId id)
		{
			return this.Subspace.Create(id);
		}

		/// <summary>Returns the key of a specific field of an HashSet: (subspace, id, field, )</summary>
		/// <param name="id"></param>
		/// <param name="field"></param>
		/// <returns></returns>
		protected virtual Slice GetFieldKey(IFdbTuple prefix, IFdbTuple field)
		{
			return prefix.Concat(field).ToSlice();
		}

		public void Insert(FdbTransaction trans, TDocument document)
		{
			if (trans == null) throw new ArgumentNullException("trans");
			if (document == null) throw new ArgumentNullException("document");

			var id = this.Converter.GetId(document);
			if (id == null) throw new InvalidOperationException("Cannot insert a document with a null identifier");

			var packed = this.Converter.Pack(document);

			var parts = this.Converter.Split(packed);
			Contract.Assert(parts != null);

			var prefix = GetDocumentPrefix(id);

			// clear any previous version of the document
			trans.ClearRange(prefix);
			
			foreach(var part in parts)
			{
				if (part.Value != Slice.Nil)
				{
					trans.Set(GetFieldKey(prefix, part.Key), part.Value);
				}
			}		
		}

		public async Task<TDocument> LoadAsync(FdbTransaction trans, TId id, CancellationToken ct = default(CancellationToken))
		{
			if (trans == null) throw new ArgumentNullException("trans");
			if (id == null) throw new ArgumentNullException("id"); // only for ref types

			var prefix = GetDocumentPrefix(id).ToSlice();

			var parts = await trans
				.GetRangeStartsWith(prefix, 0, true, false)
				.Select(kvp => new KeyValuePair<IFdbTuple, Slice>(
					FdbTuple.UnpackWithoutPrefix(kvp.Key, prefix),
					kvp.Value
				))
				.ToArrayAsync(ct);

			if (parts == null || parts.Length == 0)
			{ // document not found
				return default(TDocument);
			}

			if (ct.IsCancellationRequested) ct.ThrowIfCancellationRequested();

			var packed = this.Converter.Build(parts);
			if (packed == null) throw new InvalidOperationException();

			var doc = this.Converter.Unpack(packed, id);
			return doc;
		}

		public void Delete(FdbTransaction trans, TId id)
		{
			if (trans == null) throw new ArgumentNullException("trans");
			if (id == null) throw new ArgumentNullException("id");

			trans.ClearRange(GetDocumentPrefix(id));
		}

		public void Delete(FdbTransaction trans, TDocument document)
		{
			if (trans == null) throw new ArgumentNullException("trans");
			if (document == null) throw new ArgumentNullException("document");

			var id = this.Converter.GetId(document);
			if (id == null) throw new InvalidOperationException();

			Delete(trans, id);
		}

		#region Transactional...

		public async Task InsertAsync(FdbDatabase db, TDocument document, CancellationToken ct = default(CancellationToken))
		{
			if (db == null) throw new ArgumentNullException("db");

			await db.Attempt(
				(trans, _document) => { Insert(trans, _document); },
				document,
				ct
			);

		}

		public Task<TDocument> LoadAsync(FdbDatabase db, TId id, CancellationToken ct = default(CancellationToken))
		{
			if (db == null) throw new ArgumentNullException("db");
			if (id == null) throw new ArgumentNullException("id");

			return db.AttemptAsync(
				(trans) => LoadAsync(trans, id, ct),
				ct
			);
		}

		public Task DeleteAsync(FdbDatabase db, TId id, CancellationToken ct = default(CancellationToken))
		{
			if (db == null) throw new ArgumentNullException("db");
			if (id == null) throw new ArgumentNullException("id");

			return db.Attempt(
				(tr, _id) => Delete(tr, _id),
				id,
				ct
			);
		}

		#endregion

	}

}
