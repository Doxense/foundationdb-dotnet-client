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

	public static class FdbQueueTransactionals
	{
		/// <summary>Remove all items from the queue.</summary>
		public static Task ClearAsync(this FdbQueue queue, IFdbTransactional dbOrTrans, CancellationToken ct = default(CancellationToken))
		{
			if (dbOrTrans == null) throw new ArgumentNullException("dbOrTrans");

			return dbOrTrans.WriteAsync((tr) => queue.ClearAsync(tr), ct);
		}

		/// <summary>Remove all items from the queue.</summary>
		public static Task ClearAsync<T>(this FdbQueue<T> queue, IFdbTransactional dbOrTrans, CancellationToken ct = default(CancellationToken))
		{
			if (dbOrTrans == null) throw new ArgumentNullException("db");

			return dbOrTrans.WriteAsync((tr) => queue.ClearAsync(tr), ct);
		}

		/// <summary>Test whether the queue is empty.</summary>
		public static Task<bool> EmptyAsync(this FdbQueue queue, IFdbReadOnlyTransactional dbOrTrans, CancellationToken ct = default(CancellationToken))
		{
			if (dbOrTrans == null) throw new ArgumentNullException("db");

			return dbOrTrans.ReadAsync((tr) => queue.EmptyAsync(tr), ct);
		}

		/// <summary>Test whether the queue is empty.</summary>
		public static Task<bool> EmptyAsync<T>(this FdbQueue<T> queue, IFdbReadOnlyTransactional dbOrTrans, CancellationToken ct = default(CancellationToken))
		{
			if (dbOrTrans == null) throw new ArgumentNullException("db");

			return dbOrTrans.ReadAsync((tr) => queue.EmptyAsync(tr), ct);
		}

		/// <summary>Push a single item onto the queue.</summary>
		public static Task PushAsync(this FdbQueue queue, IFdbTransactional dbOrTrans, Slice value, CancellationToken ct = default(CancellationToken))
		{
			if (dbOrTrans == null) throw new ArgumentNullException("db");

			return dbOrTrans.ReadWriteAsync((tr) => queue.PushAsync(tr, value), ct);
		}

		/// <summary>Push a single item onto the queue.</summary>
		public static Task PushAsync<T>(this FdbQueue<T> queue, IFdbTransactional dbOrTrans, T value, CancellationToken ct = default(CancellationToken))
		{
			if (dbOrTrans == null) throw new ArgumentNullException("db");

			return dbOrTrans.ReadWriteAsync((tr) => queue.PushAsync(tr, value), ct);
		}

		/// <summary>Get the value of the next item in the queue without popping it.</summary>
		public static Task<Slice> PeekAsync(this FdbQueue queue, IFdbReadOnlyTransactional dbOrTrans, CancellationToken ct = default(CancellationToken))
		{
			if (dbOrTrans == null) throw new ArgumentNullException("db");

			return dbOrTrans.ReadAsync((tr) => queue.PeekAsync(tr), ct);
		}

		/// <summary>Get the value of the next item in the queue without popping it.</summary>
		public static Task<T> PeekAsync<T>(this FdbQueue<T> queue, IFdbTransactional dbOrTrans, CancellationToken ct = default(CancellationToken))
		{
			if (dbOrTrans == null) throw new ArgumentNullException("db");

			return dbOrTrans.ReadWriteAsync((tr) => queue.PeekAsync(tr), ct);
		}

	}

}
