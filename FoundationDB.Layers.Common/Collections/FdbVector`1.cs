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
	using System.Threading.Tasks;

	/// <summary>Represents a potentially sparse typed array in FoundationDB.</summary>
	/// <typeparam name="T">Type of the items stored in the vector</typeparam>
	/// <remarks>The default implementation uses the Tuple layer to encode the values.</remarks>
	public class FdbVector<T>
	{
		// This is just a wrapper around the non-generic FdbVector, and just adds encoding/decoding semantics.
		// By default we use the Tuple layer to encode/decode the values, but implementors just need to derive this class,
		// and override the EncodeValue/DecodeValue methods to change the serialization to something else (JSON, MessagePack, ...)

		/// <summary>Vector that is used for storage</summary>
		internal FdbVector Vector { get; private set; }

		/// <summary>Subspace used as a prefix for all items in this vector</summary>
		public FdbSubspace Subspace { get { return this.Vector.Subspace; } }

		/// <summary>Default value for sparse entries</summary>
		public T DefaultValue { get; private set; }

		/// <summary>Create a new sparse Vector</summary>
		/// <param name="subspace">Subspace where the vector will be stored</param>
		/// <param name="defaultValue">Default value for sparse entries</param>
		public FdbVector(FdbSubspace subspace, T defaultValue)
		{
			if (subspace == null) throw new ArgumentNullException("subspace");
			this.Vector = new FdbVector(subspace, FdbTuple.Pack<T>(defaultValue));
			this.DefaultValue = defaultValue;
		}

		/// <summary>Create a new sparse Vector</summary>
		/// <param name="subspace">Subspace where the vector will be stored</param>
		/// <remarks>Sparse entries will be assigned the default value for type <typeparamref name="T"/></remarks>
		public FdbVector(FdbSubspace subspace)
			: this(subspace, default(T))
		{ }

		/// <summary>Encode a <typeparamref name="T"/> into a Slice</summary>
		/// <param name="value">Value that will be stored in the vector</param>
		protected virtual Slice EncodeValue(T value)
		{
			return FdbTuple.Pack<T>(value);
		}

		/// <summary>Decode a Slice back into a <typeparamref name="T"/></summary>
		/// <param name="packed">Packed version that was generated via a previous call to <see cref="EncodeValue"/></param>
		/// <returns>Decoded value that was read from the vector</returns>
		protected virtual T DecodeValue(Slice packed)
		{
			if (packed.IsNullOrEmpty) return default(T);
			return FdbTuple.UnpackSingle<T>(packed);
		}

		/// <summary>Get the number of items in the Vector. This number includes the sparsely represented items.</summary>
		public Task<long> SizeAsync(IFdbReadOnlyTransaction tr)
		{
			return this.Vector.SizeAsync(tr);
		}

		/// <summary>Test whether the Vector is empty.</summary>
		public Task<bool> EmptyAsync(IFdbReadOnlyTransaction tr)
		{
			return this.Vector.EmptyAsync(tr);
		}

		/// <summary>Remove all items from the Vector.</summary>
		public void Clear(IFdbTransaction tr)
		{
			this.Vector.Clear(tr);
		}

		/// <summary>Push a single item onto the end of the Vector.</summary>
		public Task PushAsync(IFdbTransaction tr, T value)
		{
			return this.Vector.PushAsync(tr, EncodeValue(value));
		}

		/// <summary>Get and pops the last item off the Vector.</summary>
		public async Task<T> PopAsync(IFdbTransaction tr)
		{
			return DecodeValue(await this.Vector.PopAsync(tr).ConfigureAwait(false));
		}

		/// <summary>Set the value at a particular index in the Vector.</summary>
		public void Set(IFdbTransaction tr, long index, T value)
		{
			this.Vector.Set(tr, index, EncodeValue(value));
		}

		/// <summary>Get the item at the specified index.</summary>
		public async Task<T> GetAsync(IFdbReadOnlyTransaction tr, long index)
		{
			return DecodeValue(await this.Vector.GetAsync(tr, index).ConfigureAwait(false));
		}

		/// <summary>Grow or shrink the size of the Vector.</summary>
		public Task ResizeAsync(IFdbTransaction tr, long length)
		{
			return this.Vector.ResizeAsync(tr, length);
		}

		/// <summary>Swap the items at positions index1 and index2.</summary>
		public Task SwapAsync(IFdbTransaction tr, int index1, int index2)
		{
			return this.Vector.SwapAsync(tr, index1, index2);
		}
	}

}
