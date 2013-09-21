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
	using System.Threading;
	using System.Threading.Tasks;

	public class FdbVector<T>
	{

		internal FdbVector Vector { get; private set; }

		public FdbSubspace Subspace { get { return this.Vector.Subspace; } }

		public T DefaultValue { get; private set; }

		public FdbVector(FdbSubspace subspace, T defaultValue)
		{
			this.Vector = new FdbVector(subspace, FdbTuple.Pack<T>(defaultValue));
			this.DefaultValue = defaultValue;
		}

		protected virtual Slice EncodeValue(T value)
		{
			return FdbTuple.Pack<T>(value);
		}

		protected virtual T DecodeValue(Slice value)
		{
			if (value.IsNullOrEmpty) return default(T);
			return FdbTuple.UnpackSingle<T>(value);
		}

		public Task<long> SizeAsync(IFdbReadTransaction tr)
		{
			return this.Vector.SizeAsync(tr);
		}

		public Task EmptyAsync(IFdbReadTransaction tr)
		{
			return this.Vector.EmptyAsync(tr);
		}

		public void Clear(IFdbTransaction tr)
		{
			this.Vector.Clear(tr);
		}

		public Task PushAsync(IFdbTransaction tr, T value)
		{
			return this.Vector.PushAsync(tr, EncodeValue(value));
		}

		public async Task<T> PopAsync(IFdbTransaction tr)
		{
			return DecodeValue(await this.Vector.PopAsync(tr).ConfigureAwait(false));
		}

		public void Set(IFdbTransaction tr, long index, T value)
		{
			this.Vector.Set(tr, index, EncodeValue(value));
		}

		public async Task<T> GetAsync(IFdbReadTransaction tr, long index)
		{
			return DecodeValue(await this.Vector.GetAsync(tr, index).ConfigureAwait(false));
		}

		public Task ResizeAsync(IFdbTransaction tr, long length)
		{
			return this.Vector.ResizeAsync(tr, length);
		}

		public Task SwapAsync(IFdbTransaction tr, int index1, int index2)
		{
			return this.Vector.SwapAsync(tr, index1, index2);
		}
	}

}
