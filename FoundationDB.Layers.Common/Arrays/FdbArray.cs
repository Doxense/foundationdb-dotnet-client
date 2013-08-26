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

namespace FoundationDB.Layers.Arrays
{
	using FoundationDB.Client;
	using FoundationDB.Layers.Tuples;
	using System;
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

		public FdbSubspace Subspace { get; private set; }

		#region Key management...

		public Slice Key(int index)
		{
			return this.Subspace.Pack<int>(index);
		}

		public Slice Key(long index)
		{
			return this.Subspace.Pack<long>(index);
		}

		#endregion

		#region Get / Set / Clear

		public Task<Slice> GetAsync(IFdbReadTransaction trans, int key, CancellationToken ct = default(CancellationToken))
		{
			return trans.GetAsync(Key(key), ct);
		}

		public Task<Slice> GetAsync(IFdbReadTransaction trans, long key, CancellationToken ct = default(CancellationToken))
		{
			return trans.GetAsync(Key(key), ct);
		}

		public void Set(IFdbTransaction trans, int key, Slice value)
		{
			trans.Set(Key(key), value);
		}

		public void Set(IFdbTransaction trans, long key, Slice value)
		{
			trans.Set(Key(key), value);
		}

		public void Clear(IFdbTransaction trans)
		{
			trans.ClearRange(this.Subspace);
		}

		#endregion

	}

}
