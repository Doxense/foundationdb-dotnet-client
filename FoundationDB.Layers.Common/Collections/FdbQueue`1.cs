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
	using FoundationDB.Linq;
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using System.Threading;
	using System.Threading.Tasks;

	/// <summary>
	/// Provides a high-contention Queue class with typed values
	/// </summary>
	/// <typeparam name="T">Type of the items stored in the queue</typeparam>
	public class FdbQueue<T>
	{

		internal FdbQueue Queue { get; private set; }

		public FdbQueue(FdbSubspace subspace)
			: this(subspace, true)
		{ }

		public FdbQueue(FdbSubspace subspace, bool highContention)
		{
			this.Queue = new FdbQueue(subspace, highContention);
		}

		protected virtual Slice EncodeValue(T value)
		{
			return FdbTuple.Pack(value);
		}

		protected virtual T DecodeValue(Slice packed)
		{
			if (packed.IsNullOrEmpty) return default(T);

			//REVIEW: we could use an UnpackSingle<T> that checks that there's only one value ..?
			return FdbTuple.UnpackLast<T>(packed);
		}

		public void ClearAsync(IFdbTransaction tr)
		{
			this.Queue.ClearAsync(tr);
		}

		public Task PushAsync(IFdbTransaction tr, T value, CancellationToken ct = default(CancellationToken))
		{
			return this.Queue.PushAsync(tr, EncodeValue(value), ct);
		}

		public async Task<T> PopAsync(FdbDatabase db, CancellationToken ct = default(CancellationToken))
		{
			return DecodeValue(await this.Queue.PopAsync(db, ct).ConfigureAwait(false));
		}

		public Task<bool> EmptyAsync(IFdbReadTransaction tr, CancellationToken ct = default(CancellationToken))
		{
			return this.Queue.EmptyAsync(tr, ct);
		}

		public async Task<T> PeekAsync(IFdbReadTransaction tr, CancellationToken ct = default(CancellationToken))
		{
			return DecodeValue(await this.Queue.PeekAsync(tr, ct).ConfigureAwait(false));
		}
	}

}
