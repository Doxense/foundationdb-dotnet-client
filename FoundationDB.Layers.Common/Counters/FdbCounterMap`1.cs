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

namespace FoundationDB.Layers.Counters
{
	using FoundationDB.Client;
	using FoundationDB.Layers.Tuples;
	using System;
	using System.Threading.Tasks;

	/// <summary>Container for multiple 64-bit signed counters, indexed by a key of type <typeparamref name="T"/></summary>
	/// <typeparam name="T">Type of the key for each counter</typeparam>
	public class FdbCounterMap<T>
	{
		// This is just a wrapper around the non-generic FDbCounterList, and just adds encoding/decoding semantics.
		// By default we use the Tuple layer to encode/decode the keys, but implementors just need to derive this class,
		// and override the EncodeKey/DecodeKey methods to change the serialization to something else

		/// <summary>Queue that is used for storage</summary>
		internal FdbCounterMap Map { get; private set; }

		/// <summary>Subspace used as a prefix for all items in this counter list</summary>
		public FdbSubspace Subspace { get { return this.Map.Subspace; } }

		public FdbCounterMap(FdbSubspace subspace)
		{
			this.Map = new FdbCounterMap(subspace);
		}

		/// <summary>Encode a <typeparamref name="T"/> into a Slice</summary>
		/// <param name="value">Key that will be stored in the counter list</param>
		protected virtual Slice EncodeKey(T value)
		{
			return FdbTuple.Pack(value);
		}

		/// <summary>Decode a Slice back into a <typeparamref name="T"/></summary>
		/// <param name="packed">Packed version that was generated via a previous call to <see cref="EncodeKey"/></param>
		/// <returns>Decoded key</returns>
		protected virtual T DecodeKey(Slice packed)
		{
			if (packed.IsNullOrEmpty) return default(T);

			return FdbTuple.UnpackSingle<T>(packed);
		}

		public void Add(IFdbTransaction transaction, T counterKey, long value)
		{
			this.Map.Add(transaction, EncodeKey(counterKey), value);
		}

		public void Subtract(IFdbTransaction transaction, T counterKey, long value)
		{
			this.Map.Add(transaction, EncodeKey(counterKey), -value);
		}

		public void Increment(IFdbTransaction transaction, T counterKey)
		{
			this.Map.Add(transaction, EncodeKey(counterKey), 1);
		}

		public void Decrement(IFdbTransaction transaction, T counterKey)
		{
			this.Map.Add(transaction, EncodeKey(counterKey), -1);
		}

		public Task<long?> ReadAsync(IFdbReadOnlyTransaction transaction, T counterKey)
		{
			return this.Map.ReadAsync(transaction, EncodeKey(counterKey));
		}

		public Task<long> AddThenReadAsync(IFdbTransaction transaction, T counterKey, long value)
		{
			return this.Map.AddThenReadAsync(transaction, EncodeKey(counterKey), value);
		}

		public Task<long> SubtractThenReadAsync(IFdbTransaction transaction, T counterKey, long value)
		{
			return this.Map.AddThenReadAsync(transaction, EncodeKey(counterKey), -value);
		}

		public Task<long> IncrementThenReadAsync(IFdbTransaction transaction, T counterKey)
		{
			return this.Map.AddThenReadAsync(transaction, EncodeKey(counterKey), 1);
		}

		public Task<long> DecrementThenReadAsync(IFdbTransaction transaction, T counterKey)
		{
			return this.Map.AddThenReadAsync(transaction, EncodeKey(counterKey), -1);
		}

		public Task<long> ReadThenAddAsync(IFdbTransaction transaction, T counterKey, long value)
		{
			return this.Map.ReadThenAddAsync(transaction, EncodeKey(counterKey), value);
		}

		public Task<long> ReadThenSubtractAsync(IFdbTransaction transaction, T counterKey, long value)
		{
			return this.Map.ReadThenAddAsync(transaction, EncodeKey(counterKey), -value);
		}

		public Task<long> ReadThenIncrementAsync(IFdbTransaction transaction, T counterKey)
		{
			return this.Map.ReadThenAddAsync(transaction, EncodeKey(counterKey), 1);
		}

		public Task<long> ReadThenDecrementAsync(IFdbTransaction transaction, T counterKey)
		{
			return this.Map.ReadThenAddAsync(transaction, EncodeKey(counterKey), -1);
		}

	}
}
