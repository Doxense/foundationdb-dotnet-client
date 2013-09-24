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

namespace FoundationDB.Layers.Collections
{
	using FoundationDB.Client;
	using FoundationDB.Layers.Tuples;
	using System;
	using System.Collections.Generic;
	using System.Diagnostics;
	using System.Threading;
	using System.Threading.Tasks;

	[DebuggerDisplay("Subspace={Subspace}")]
	public class FdbArray
	{

		public FdbArray(FdbSubspace subspace)
		{
			if (subspace == null) throw new ArgumentNullException("subspace");

			this.Subspace = subspace;
		}

		/// <summary>Subspace used as a prefix for all items in this array</summary>
		public FdbSubspace Subspace { get; private set; }

		#region Get / Set / Clear

		public Task<Slice> GetAsync(IFdbReadOnlyTransaction tr, long index)
		{
			if (tr == null) throw new ArgumentNullException("tr");
			if (index < 0) throw new IndexOutOfRangeException("Array index must be a positive integer.");

			return tr.GetAsync(this.Subspace.Pack<long>(index));
		}

		public void Set(IFdbTransaction tr, long index, Slice value)
		{
			if (tr == null) throw new ArgumentNullException("tr");
			if (index < 0) throw new IndexOutOfRangeException("Array index must be a positive integer.");

			tr.Set(this.Subspace.Pack<long>(index), value);
		}

		public void Clear(IFdbTransaction tr)
		{
			if (tr == null) throw new ArgumentNullException("tr");

			tr.ClearRange(this.Subspace);
		}

		public async Task<long> SizeAsync(IFdbReadOnlyTransaction tr)
		{
			if (tr == null) throw new ArgumentNullException("tr");

			var keyRange = this.Subspace.ToRange();
			var lastKey = await tr.GetKeyAsync(FdbKeySelector.LastLessOrEqual(keyRange.End)).ConfigureAwait(false);
			return lastKey < keyRange.Begin ? 0 : this.Subspace.UnpackSingle<long>(lastKey) + 1;
		}

		public Task<bool> EmptyAsync(IFdbReadOnlyTransaction tr)
		{
			if (tr == null) throw new ArgumentNullException("tr");

			return tr.GetRange(this.Subspace.ToRange()).AnyAsync();
		}

		public FdbRangeQuery<KeyValuePair<long, Slice>> GetAll(IFdbReadOnlyTransaction tr)
		{
			if (tr == null) throw new ArgumentNullException("tr");

			return tr
				.GetRange(this.Subspace.ToRange())
				.Select(kvp => new KeyValuePair<long, Slice>(
					this.Subspace.UnpackSingle<long>(kvp.Key),
					kvp.Value
				));
		}

		#endregion

	}

}
