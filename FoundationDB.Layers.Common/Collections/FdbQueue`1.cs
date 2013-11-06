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

	/// <summary>Provides a high-contention Queue class with typed values</summary>
	/// <typeparam name="T">Type of the items stored in the queue</typeparam>
	/// <remarks>The default implementation uses the Tuple layer to encode the values.</remarks>
	public class FdbQueue<T>
	{
		// This is just a wrapper around the non-generic FdbQueue, and just adds encoding/decoding semantics.
		// By default we use the Tuple layer to encode/decode the values, but implementors just need to derive this class,
		// and override the EncodeValue/DecodeValue methods to change the serialization to something else (JSON, MessagePack, ...)

		/// <summary>Queue that is used for storage</summary>
		internal FdbQueue Queue { get; private set; }

		/// <summary>Subspace used as a prefix for all items in this queue</summary>
		public FdbSubspace Subspace { get { return this.Queue.Subspace; } }

		/// <summary>Serializer for the elements of the queue</summary>
		public IFdbValueEncoder<T> Serializer { get; private set; }

		/// <summary>Create a new High Contention Queue</summary>
		/// <param name="subspace">Subspace where the queue will be stored</param>
		/// <remarks>Uses the default Tuple serializer</remarks>
		public FdbQueue(FdbSubspace subspace)
			: this(subspace, highContention: true, serializer: FdbTupleCodec<T>.Default)
		{ }

		/// <summary>Create a new queue using either High Contention mode or Simple mode</summary>
		/// <param name="subspace">Subspace where the queue will be stored</param>
		/// <param name="highContention">If true, uses High Contention Mode (lots of popping clients). If true, uses the Simple Mode (a few popping clients).</param>
		/// <remarks>Uses the default Tuple serializer</remarks>
		public FdbQueue(FdbSubspace subspace, bool highContention)
			: this(subspace, highContention: highContention, serializer: FdbTupleCodec<T>.Default)
		{ }

		/// <summary>Create a new queue using either High Contention mode or Simple mode</summary>
		/// <param name="subspace">Subspace where the queue will be stored</param>
		/// <param name="highContention">If true, uses High Contention Mode (lots of popping clients). If true, uses the Simple Mode (a few popping clients).</param>
		/// <param name="serializer">Serializer used to pack and unpack the elements of the queue</param>
		public FdbQueue(FdbSubspace subspace, bool highContention, IFdbValueEncoder<T> serializer)
		{
			if (serializer == null) throw new ArgumentNullException("serializer");

			this.Queue = new FdbQueue(subspace, highContention);
			this.Serializer = serializer;
		}

		/// <summary>Remove all items from the queue.</summary>
		public void ClearAsync(IFdbTransaction tr)
		{
			this.Queue.ClearAsync(tr);
		}

		/// <summary>Push a single item onto the queue.</summary>
		public Task PushAsync(IFdbTransaction tr, T value)
		{
			return this.Queue.PushAsync(tr, this.Serializer.Encode(value));
		}

		/// <summary>Pop the next item from the queue. Cannot be composed with other functions in a single transaction.</summary>
		public async Task<T> PopAsync(IFdbDatabase db, CancellationToken ct = default(CancellationToken))
		{
			return this.Serializer.Decode(await this.Queue.PopAsync(db, ct).ConfigureAwait(false));
		}

		/// <summary>Test whether the queue is empty.</summary>
		public Task<bool> EmptyAsync(IFdbReadOnlyTransaction tr)
		{
			return this.Queue.EmptyAsync(tr);
		}

		/// <summary>Get the value of the next item in the queue without popping it.</summary>
		public async Task<T> PeekAsync(IFdbReadOnlyTransaction tr)
		{
			return this.Serializer.Decode(await this.Queue.PeekAsync(tr).ConfigureAwait(false));
		}
	}

}
