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
	using System;
	using System.Threading;
	using System.Threading.Tasks;

	public static class FdbVectorTransactionals
	{

		#region Clear / Resize

		public static Task ClearAsync(this FdbVector vector, FdbDatabase db, CancellationToken ct = default(CancellationToken))
		{
			return db.WriteAsync((tr) => vector.Clear(tr), ct);
		}

		public static Task ClearAsync<T>(this FdbVector<T> vector, FdbDatabase db, CancellationToken ct = default(CancellationToken))
		{
			return db.WriteAsync((tr) => vector.Clear(tr), ct);
		}

		public static Task ResizeAsync(this FdbVector vector, FdbDatabase db, long length, CancellationToken ct = default(CancellationToken))
		{
			return db.ReadWriteAsync((tr) => vector.ResizeAsync(tr, length), ct);
		}

		public static Task ResizeAsync<T>(this FdbVector<T> vector, FdbDatabase db, long length, CancellationToken ct = default(CancellationToken))
		{
			return db.ReadWriteAsync((tr) => vector.ResizeAsync(tr, length), ct);
		}

		#endregion

		#region Push / Pop

		public static Task<Slice> PopAsync(this FdbVector vector, FdbDatabase db, CancellationToken ct = default(CancellationToken))
		{
			return db.ReadWriteAsync((tr) => vector.PopAsync(tr), ct);
		}

		public static Task<T> PopAsync<T>(this FdbVector<T> vector, FdbDatabase db, CancellationToken ct = default(CancellationToken))
		{
			return db.ReadWriteAsync((tr) => vector.PopAsync(tr), ct);
		}

		public static Task PushAsync(this FdbVector vector, FdbDatabase db, long index, Slice value, CancellationToken ct = default(CancellationToken))
		{
			return db.WriteAsync((tr) => vector.Set(tr, index, value), ct);
		}

		public static Task PushAsync<T>(this FdbVector<T> vector, FdbDatabase db, long index, T value, CancellationToken ct = default(CancellationToken))
		{
			return db.WriteAsync((tr) => vector.Set(tr, index, value), ct);
		}

		#endregion

		#region Get / Set

		public static Task<Slice> GetAsync(this FdbVector vector, FdbDatabase db, long index, CancellationToken ct = default(CancellationToken))
		{
			return db.ReadAsync((tr) => vector.GetAsync(tr, index), ct);
		}

		public static Task<T> GetAsync<T>(this FdbVector<T> vector, FdbDatabase db, long index, CancellationToken ct = default(CancellationToken))
		{
			return db.ReadAsync((tr) => vector.GetAsync(tr, index), ct);
		}

		public static Task SetAsync(this FdbVector vector, FdbDatabase db, long index, Slice value, CancellationToken ct = default(CancellationToken))
		{
			return db.WriteAsync((tr) => vector.Set(tr, index, value), ct);
		}

		public static Task SetAsync<T>(this FdbVector<T> vector, FdbDatabase db, long index, T value, CancellationToken ct = default(CancellationToken))
		{
			return db.WriteAsync((tr) => vector.Set(tr, index, value), ct);
		}

		#endregion

	}

}
