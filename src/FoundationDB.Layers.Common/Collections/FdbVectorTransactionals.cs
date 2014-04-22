﻿#region BSD Licence
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

		#region Empty / Size

		/// <summary>Remove all items from the Vector.</summary>
		public static Task<bool> EmptyAsync<T>(this FdbVector<T> vector, IFdbReadOnlyTransactional db, CancellationToken cancellationToken)
		{
			if (vector == null) throw new ArgumentNullException("vector");
			if (db == null) throw new ArgumentNullException("db");

			return db.ReadAsync((tr) => vector.EmptyAsync(tr), cancellationToken);
		}

		/// <summary>Get the number of items in the Vector. This number includes the sparsely represented items.</summary>
		public static Task<long> SizeAsync<T>(this FdbVector<T> vector, IFdbReadOnlyTransactional db, CancellationToken cancellationToken)
		{
			if (vector == null) throw new ArgumentNullException("vector");
			if (db == null) throw new ArgumentNullException("db");

			return db.ReadAsync((tr) => vector.SizeAsync(tr), cancellationToken);
		}

		#endregion

		#region Clear / Resize

		/// <summary>Remove all items from the Vector.</summary>
		public static Task ClearAsync<T>(this FdbVector<T> vector, IFdbTransactional db, CancellationToken cancellationToken)
		{
			if (vector == null) throw new ArgumentNullException("vector");
			if (db == null) throw new ArgumentNullException("db");

			return db.WriteAsync((tr) => vector.Clear(tr), cancellationToken);
		}

		/// <summary>Grow or shrink the size of the Vector.</summary>
		public static Task ResizeAsync<T>(this FdbVector<T> vector, IFdbTransactional db, long length, CancellationToken cancellationToken)
		{
			if (vector == null) throw new ArgumentNullException("vector");
			if (db == null) throw new ArgumentNullException("db");

			return db.ReadWriteAsync((tr) => vector.ResizeAsync(tr, length), cancellationToken);
		}

		#endregion

		#region Push / Pop

		/// <summary>Get and pops the last item off the Vector.</summary>
		public static Task<Optional<T>> PopAsync<T>(this FdbVector<T> vector, IFdbTransactional db, CancellationToken cancellationToken)
		{
			if (vector == null) throw new ArgumentNullException("vector");
			if (db == null) throw new ArgumentNullException("db");

			return db.ReadWriteAsync((tr) => vector.PopAsync(tr), cancellationToken);
		}

		/// <summary>Push a single item onto the end of the Vector.</summary>
		public static Task PushAsync<T>(this FdbVector<T> vector, IFdbTransactional db, T value, CancellationToken cancellationToken)
		{
			if (vector == null) throw new ArgumentNullException("vector");
			if (db == null) throw new ArgumentNullException("db");

			return db.ReadWriteAsync((tr) => vector.PushAsync(tr, value), cancellationToken);
		}

		#endregion

		#region Get / Set

		/// <summary>Get the item at the specified index.</summary>
		public static Task<T> GetAsync<T>(this FdbVector<T> vector, IFdbReadOnlyTransactional db, long index, CancellationToken cancellationToken)
		{
			if (vector == null) throw new ArgumentNullException("vector");
			if (db == null) throw new ArgumentNullException("db");

			return db.ReadAsync((tr) => vector.GetAsync(tr, index), cancellationToken);
		}

		/// <summary>Set the value at a particular index in the Vector.</summary>
		public static Task SetAsync<T>(this FdbVector<T> vector, IFdbTransactional db, long index, T value, CancellationToken cancellationToken)
		{
			if (vector == null) throw new ArgumentNullException("vector");
			if (db == null) throw new ArgumentNullException("db");

			return db.WriteAsync((tr) => vector.Set(tr, index, value), cancellationToken);
		}

		#endregion

	}

}
