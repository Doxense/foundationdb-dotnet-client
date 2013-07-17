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

namespace FoundationDB.Layers.Tables
{
	using FoundationDB.Client;
	using FoundationDB.Layers.Tuples;
	using System;
	using System.Collections.Generic;
	using System.Diagnostics;
	using System.Threading;
	using System.Threading.Tasks;

	[DebuggerDisplay("Subspace={Subspace}")]
	public class FdbTable
	{

		public FdbTable(FdbSubspace subspace)
		{
			if (subspace == null) throw new ArgumentNullException("subspace");

			this.Subspace = subspace;
		}

		/// <summary>Subspace used as a prefix for all items in this table</summary>
		public FdbSubspace Subspace { get; private set; }

		#region Keys...

		/// <summary>Add the namespace in front of an existing tuple</summary>
		/// <param name="id">Existing tuple</param>
		/// <returns>(namespace, tuple_items, )</returns>
		internal virtual IFdbTuple MakeKey(IFdbTuple id)
		{
			if (id == null) throw new ArgumentNullException("id");

			return this.Subspace.Append(id);
		}

		#endregion

		#region Get / Set / Clear...

		public Task<Slice> GetAsync(IFdbReadTransaction trans, IFdbTuple id, CancellationToken ct = default(CancellationToken))
		{
			if (trans == null) throw new ArgumentNullException("trans");
			if (id == null) throw new ArgumentNullException("id");

			return trans.GetAsync(MakeKey(id).ToSlice(), ct);
		}

		public void Set(IFdbTransaction trans, IFdbTuple id, Slice value)
		{
			if (trans == null) throw new ArgumentNullException("trans");
			if (id == null) throw new ArgumentNullException("id");

			trans.Set(MakeKey(id).ToSlice(), value);
		}

		public void Clear(IFdbTransaction trans, IFdbTuple id)
		{
			if (trans == null) throw new ArgumentNullException("trans");
			if (id == null) throw new ArgumentNullException("id");

			trans.Clear(MakeKey(id));
		}

		public Task<List<KeyValuePair<Slice, Slice>>> GetAllAsync(IFdbReadTransaction trans, CancellationToken ct = default(CancellationToken))
		{
			if (trans == null) throw new ArgumentNullException("trans");

			return trans
				.GetRangeStartsWith(this.Subspace) //TODO: options ?
				.ToListAsync(ct);
		}

		#endregion

		#region Transactional...

		public Task<Slice> GetAsync(FdbDatabase db, IFdbTuple id, CancellationToken ct = default(CancellationToken))
		{
			if (db == null) throw new ArgumentNullException("db");
			if (id == null) throw new ArgumentNullException("id");

			return db.Attempt.ReadAsync((tr) => this.GetAsync(tr, id, ct), ct);
		}

		public Task SetAsync(FdbDatabase db, IFdbTuple id, Slice value, CancellationToken ct = default(CancellationToken))
		{
			if (db == null) throw new ArgumentNullException("db");

			return db.Attempt.Change((tr) => this.Set(tr, id, value), ct);
		}

		public Task ClearAsync(FdbDatabase db, IFdbTuple id, CancellationToken ct = default(CancellationToken))
		{
			if (db == null) throw new ArgumentNullException("db");
			if (id == null) throw new ArgumentNullException("id");

			return db.Attempt.Change((tr) => this.Clear(tr, id), ct);
		}

		#endregion

	}

}
