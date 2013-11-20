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
	public class FdbDocumentCollection<TDocument, TId>
		where TDocument : class
	{

		public const int DefaultChunkSize = 1 << 20; // 1 MB

		public FdbDocumentCollection(FdbSubspace subspace, Func<TDocument, TId> selector, IValueEncoder<TDocument> valueEncoder)
			: this(subspace, selector, KeyValueEncoders.Tuples.CompositeKey<TId, int>(), valueEncoder)
		{ }

		public FdbDocumentCollection(FdbSubspace subspace, Func<TDocument, TId> selector, ICompositeKeyEncoder<TId, int> keyEncoder, IValueEncoder<TDocument> valueEncoder)
		{
			if (subspace == null) throw new ArgumentNullException("subspace");
			if (selector == null) throw new ArgumentNullException("selector");
			if (keyEncoder == null) throw new ArgumentNullException("keyEncoder");
			if (valueEncoder == null) throw new ArgumentNullException("valueEncoder");

			this.Subspace = subspace;
			this.IdSelector = selector;
			this.KeyEncoder = keyEncoder;
			this.ValueEncoder = valueEncoder;
		}

		/// <summary>Subspace used as a prefix for all hashsets in this collection</summary>
		public FdbSubspace Subspace { get; private set; }
		
		/// <summary>Encode the document IDs into keys</summary>
		public ICompositeKeyEncoder<TId, int> KeyEncoder { get; private set; }

		/// <summary>Encode the documents into values</summary>
		public IValueEncoder<TDocument> ValueEncoder { get; private set; }

		/// <summary>Lambda used to extract the ID from a document</summary>
		public Func<TDocument, TId> IdSelector { get; private set; }

		/// <summary>Maximum size of a document chunk (1 MB by default)</summary>
		public int ChunkSize { get; private set; }

		public void Insert(IFdbTransaction trans, TDocument document)
		{
			if (trans == null) throw new ArgumentNullException("trans");
			if (document == null) throw new ArgumentNullException("document");

			var id = this.IdSelector(document);
			if (id == null) throw new InvalidOperationException("Cannot insert a document with a null identifier");

			// encode the document
			var packed = this.ValueEncoder.EncodeValue(document);

			// Key Prefix = ...(id,)
			var key = this.Subspace.EncodePartial(this.KeyEncoder, id);

			// clear previous value
			trans.ClearRange(FdbKeyRange.StartsWith(key));

			int remaining = packed.Count;
			if (remaining <= this.ChunkSize)
			{ // stored as a single element

				// Key = ...(id,)
				trans.Set(key, packed);
			}
			else
			{ // splits in as many chunks as necessary

				// Key = ...(id, N) where N is the chunk index (0-based)
				int p = 0;
				int index = 0;
				while (remaining > 0)
				{
					int sz = Math.Max(remaining, this.ChunkSize);

					trans.Set(this.Subspace.Encode(this.KeyEncoder, id, index), packed.Substring(p, sz));

					++index;
					p += sz;
					remaining -= sz;
				}
			}
		}

		public async Task<TDocument> LoadAsync(IFdbReadOnlyTransaction trans, TId id)
		{
			if (trans == null) throw new ArgumentNullException("trans");
			if (id == null) throw new ArgumentNullException("id"); // only for ref types

			var key = this.Subspace.EncodePartial(this.KeyEncoder, id);

			var parts = await trans
				.GetRange(FdbKeyRange.StartsWith(key)) //TODO: options ?
				.ToListAsync();

			// merge all the chunks together
			var packed = Slice.Join(Slice.Empty, parts.Select(kvp => kvp.Value));

			return this.ValueEncoder.DecodeValue(packed);
		}

		public void Delete(IFdbTransaction trans, TId id)
		{
			if (trans == null) throw new ArgumentNullException("trans");
			if (id == null) throw new ArgumentNullException("id");

			trans.ClearRange(FdbKeyRange.StartsWith(this.Subspace.EncodePartial(this.KeyEncoder, id)));
		}

		public void Delete(IFdbTransaction trans, TDocument document)
		{
			if (trans == null) throw new ArgumentNullException("trans");
			if (document == null) throw new ArgumentNullException("document");

			var id = this.IdSelector(document);
			if (id == null) throw new InvalidOperationException();

			Delete(trans, id);
		}

		#region Transactional...

		public async Task InsertAsync(IFdbTransactional dbOrTrans, TDocument document, CancellationToken cancellationToken = default(CancellationToken))
		{
			if (dbOrTrans == null) throw new ArgumentNullException("dbOrTrans");

			await dbOrTrans.WriteAsync((tr) => this.Insert(tr, document), cancellationToken);

		}

		public Task<TDocument> LoadAsync(IFdbReadOnlyTransactional dbOrTrans, TId id, CancellationToken cancellationToken = default(CancellationToken))
		{
			if (dbOrTrans == null) throw new ArgumentNullException("dbOrTrans");
			if (id == null) throw new ArgumentNullException("id");

			return dbOrTrans.ReadAsync((tr) => LoadAsync(tr, id), cancellationToken);
		}

		public Task DeleteAsync(IFdbTransactional dbOrTrans, TId id, CancellationToken cancellationToken = default(CancellationToken))
		{
			if (dbOrTrans == null) throw new ArgumentNullException("dbOrTrans");
			if (id == null) throw new ArgumentNullException("id");

			return dbOrTrans.WriteAsync((tr) => this.Delete(tr, id), cancellationToken);
		}

		public Task DeleteAsync(IFdbTransactional dbOrTrans, TDocument document, CancellationToken cancellationToken = default(CancellationToken))
		{
			if (dbOrTrans == null) throw new ArgumentNullException("dbOrTrans");
			if (document == null) throw new ArgumentNullException("document");

			return dbOrTrans.WriteAsync((tr) => this.Delete(tr, document), cancellationToken);
		}

		#endregion

	}

}
