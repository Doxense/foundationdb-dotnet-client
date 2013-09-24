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
	using FoundationDB.Async;
	using FoundationDB.Client;
	using FoundationDB.Layers.Tuples;
	using System;
	using System.Collections.Generic;
	using System.Diagnostics;
	using System.Threading;
	using System.Threading.Tasks;

	[DebuggerDisplay("Subspace={Subspace}")]
	public class FdbArray<T>
	{

		/// <summary>Internal array used for storage</summary>
		internal FdbArray Array { get; private set; }

		/// <summary>Subspace used as a prefix for all items in this array</summary>
		public FdbSubspace Subspace { get { return this.Array.Subspace; } }

		public FdbArray(FdbSubspace subspace)
		{
			if (subspace == null) throw new ArgumentNullException("subspace");

			this.Array = new FdbArray(subspace);
		}

		protected virtual Slice EncodeValue(T value)
		{
			return FdbTuple.Pack<T>(value);
		}

		protected virtual T DecodeValue(Slice packed)
		{
			if (packed.IsNullOrEmpty) return default(T);
			return FdbTuple.UnpackSingle<T>(packed);
		}

		public async Task<T> GetAsync(IFdbReadOnlyTransaction tr, long index)
		{
			return DecodeValue(await this.Array.GetAsync(tr, index).ConfigureAwait(false));
		}

		public void Set(IFdbTransaction tr, long index, T value)
		{
			this.Array.Set(tr, index, EncodeValue(value));
		}

		public void Clear(IFdbTransaction tr)
		{
			this.Array.Clear(tr);
		}

		public Task<long> SizeAsync(IFdbReadOnlyTransaction tr)
		{
			return this.Array.SizeAsync(tr);
		}

		public Task<bool> EmptyAsync(IFdbReadOnlyTransaction tr)
		{
			return this.Array.EmptyAsync(tr);
		}

		public FdbRangeQuery<KeyValuePair<long, T>> GetAll(IFdbReadOnlyTransaction tr)
		{
			return this.Array
				.GetAll(tr)
				.Select(kvp => new KeyValuePair<long, T>(
					kvp.Key,
					DecodeValue(kvp.Value)
				));
		}

	}

}
