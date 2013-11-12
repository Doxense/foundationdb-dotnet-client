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
	using System.Threading;
	using System.Threading.Tasks;

	public static class FdbArrayTransactionals
	{

		#region FdbArray<T>...

		public static Task<T> GetAsync<T>(this FdbArray<T> array, IFdbReadOnlyTransactional dbOrTrans, long index, CancellationToken ct = default(CancellationToken))
		{
			if (array == null) throw new ArgumentNullException("array");
			if (dbOrTrans == null) throw new ArgumentNullException("dbOrTrans");

			return dbOrTrans.ReadAsync((tr) => array.GetAsync(tr, index), ct);
		}

		public static Task SetAsync<T>(this FdbArray<T> array, IFdbTransactional dbOrTrans, long index, T value, CancellationToken ct = default(CancellationToken))
		{
			if (array == null) throw new ArgumentNullException("array");
			if (dbOrTrans == null) throw new ArgumentNullException("dbOrTrans");

			return dbOrTrans.WriteAsync((tr) => array.Set(tr, index, value), ct);
		}

		public static Task ClearAsync<T>(this FdbArray<T> array, IFdbTransactional dbOrTrans, CancellationToken ct = default(CancellationToken))
		{
			if (array == null) throw new ArgumentNullException("array");
			if (dbOrTrans == null) throw new ArgumentNullException("dbOrTrans");

			return dbOrTrans.WriteAsync((tr) => array.Clear(tr));
		}

		public static Task<long> SizeAsync<T>(this FdbArray<T> array, IFdbReadOnlyTransactional dbOrTrans, CancellationToken ct = default(CancellationToken))
		{
			if (array == null) throw new ArgumentNullException("array");
			if (dbOrTrans == null) throw new ArgumentNullException("dbOrTrans");

			return dbOrTrans.ReadAsync((tr) => array.SizeAsync(tr), ct);
		}

		public static Task<bool> EmptyAsync<T>(this FdbArray<T> array, IFdbReadOnlyTransactional dbOrTrans, CancellationToken ct = default(CancellationToken))
		{
			if (array == null) throw new ArgumentNullException("array");
			if (dbOrTrans == null) throw new ArgumentNullException("dbOrTrans");

			return dbOrTrans.ReadAsync((tr) => array.EmptyAsync(tr), ct);
		}

		public static Task<List<KeyValuePair<long, T>>> ToListAsync<T>(this FdbArray<T> array, IFdbReadOnlyTransactional dbOrTrans, CancellationToken ct = default(CancellationToken))
		{
			if (array == null) throw new ArgumentNullException("array");
			if (dbOrTrans == null) throw new ArgumentNullException("dbOrTrans");

			return dbOrTrans.ReadAsync((tr) => array.All(tr).ToListAsync(), ct);
		}

		#endregion

	}

}
