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
	using System.Diagnostics;
	using System.Threading;
	using System.Threading.Tasks;

	[DebuggerDisplay("Subspace={Subspace}")]
	public class FdbArray<T>
	{

		public FdbArray(FdbSubspace subspace)
			: this(subspace, FdbTupleCodec<T>.Default)
		{ }

		public FdbArray(FdbSubspace subspace, IUnorderedTypeCodec<T> codec)
			: this(subspace, KeyValueEncoders.Unordered.Bind(codec))
		{ }

		public FdbArray(FdbSubspace subspace, IKeyValueEncoder<T> encoder)
		{
			if (subspace == null) throw new ArgumentNullException("subspace");
			if (encoder == null) throw new ArgumentNullException("encoder");

			this.Subspace = subspace;
			this.Encoder = encoder;
		}

		/// <summary>Subspace used as a prefix for all items in this array</summary>
		public FdbSubspace Subspace { get; private set; }

		/// <summary>Serializer for the elements of the array</summary>
		public IKeyValueEncoder<T> Encoder { get; private set; }

		private Slice PackIndex(long index)
		{
			return this.Subspace.Pack<long>(index);
		}

		private long UnpackIndex(Slice key)
		{
			return this.Subspace.UnpackSingle<long>(key);
		}

		#region Get / Set / Clear

		public async Task<T> GetAsync(IFdbReadOnlyTransaction tr, long index)
		{
			if (tr == null) throw new ArgumentNullException("tr");
			if (index < 0) throw new IndexOutOfRangeException("Array index must be a positive integer.");

			return this.Encoder.Decode(await tr.GetAsync(this.Subspace.Pack<long>(index)).ConfigureAwait(false));
		}

		public void Set(IFdbTransaction tr, long index, T value)
		{
			if (tr == null) throw new ArgumentNullException("tr");
			if (index < 0) throw new IndexOutOfRangeException("Array index must be a positive integer.");

			tr.Set(PackIndex(index), this.Encoder.Encode(value));
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
			return lastKey < keyRange.Begin ? 0 : UnpackIndex(lastKey) + 1;
		}

		public Task<bool> EmptyAsync(IFdbReadOnlyTransaction tr)
		{
			if (tr == null) throw new ArgumentNullException("tr");

			return tr.GetRange(this.Subspace.ToRange()).AnyAsync();
		}

		public IFdbAsyncEnumerable<KeyValuePair<long, T>> All(IFdbReadOnlyTransaction tr)
		{
			if (tr == null) throw new ArgumentNullException("tr");

			return tr
				.GetRange(this.Subspace.ToRange())
				.Select(kvp => new KeyValuePair<long, T>(
					UnpackIndex(kvp.Key),
					this.Encoder.Decode(kvp.Value)
				));
		}

		#endregion

	}

}
