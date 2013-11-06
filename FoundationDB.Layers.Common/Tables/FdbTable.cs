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
	using System.Linq;
	using System.Threading;
	using System.Threading.Tasks;

	[DebuggerDisplay("Name={Name}, Subspace={Subspace}")]
	public class FdbTable
	{

		public FdbTable(string name, FdbSubspace subspace)
		{
			if (name == null) throw new ArgumentNullException("name");
			if (subspace == null) throw new ArgumentNullException("subspace");

			this.Name = name;
			this.Subspace = subspace;
		}

		/// <summary>Name of the table</summary>
		public string Name { get; private set; }

		/// <summary>Subspace used as a prefix for all items in this table</summary>
		public FdbSubspace Subspace { get; private set; }

		#region Get / Set / Clear...

		public Task<Slice> GetAsync(IFdbReadOnlyTransaction trans, Slice id)
		{
			if (trans == null) throw new ArgumentNullException("trans");
			if (id == null) throw new ArgumentNullException("id");

			return trans.GetAsync(this.Subspace.Concat(id));
		}

		public void Set(IFdbTransaction trans, Slice id, Slice value)
		{
			if (trans == null) throw new ArgumentNullException("trans");
			if (id == null) throw new ArgumentNullException("id");

			trans.Set(this.Subspace.Concat(id), value);
		}

		public void Clear(IFdbTransaction trans, Slice id)
		{
			if (trans == null) throw new ArgumentNullException("trans");
			if (id == null) throw new ArgumentNullException("id");

			trans.Clear(this.Subspace.Concat(id));
		}

		public Task<List<KeyValuePair<Slice, Slice>>> GetAllAsync(IFdbReadOnlyTransaction trans)
		{
			if (trans == null) throw new ArgumentNullException("trans");

			return trans
				.GetRange(this.Subspace.ToRange()) //TODO: options ?
				.Select(kvp => new KeyValuePair<Slice, Slice>(this.Subspace.Extract(kvp.Key), kvp.Value))
				.ToListAsync();
		}

		public Task<Slice[]> GetValuesAsync(IFdbReadOnlyTransaction trans, IEnumerable<Slice> ids)
		{
			if (trans == null) throw new ArgumentNullException("trans");
			if (ids == null) throw new ArgumentNullException("ids");

			return trans.GetValuesAsync(this.Subspace.Merge(ids));
		}

		#endregion

	}

}
