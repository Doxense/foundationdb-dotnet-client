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

namespace FoundationDB.Client
{
	using System;
	using System.Collections.Generic;
	using System.Threading;
	using System.Threading.Tasks;

	public static class FdbTransactionals
	{

		/// <summary>
		/// Reads a value from the database.
		/// </summary>
		public static Task<Slice> GetAsync(this IFdbReadOnlyTransactional dbOrTrans, Slice key, CancellationToken cancellationToken = default(CancellationToken))
		{
			return dbOrTrans.ReadAsync((tr) => tr.GetAsync(key), cancellationToken);
		}

		/// <summary>
		/// Resolves a key selector against the keys in the database.
		/// </summary>
		public static Task<Slice> GetKeyAsync(this IFdbReadOnlyTransactional dbOrTrans, FdbKeySelector selector, CancellationToken cancellationToken = default(CancellationToken))
		{
			return dbOrTrans.ReadAsync((tr) => tr.GetKeyAsync(selector), cancellationToken);
		}

		/// <summary>
		/// Resolves several key selectors against the keys in the database.
		/// </summary>
		public static Task<Slice[]> GetKeysAsync(this IFdbReadOnlyTransactional dbOrTrans, FdbKeySelector[] selectors, CancellationToken cancellationToken = default(CancellationToken))
		{
			return dbOrTrans.ReadAsync((tr) => tr.GetKeysAsync(selectors), cancellationToken);
		}

		/// <summary>
		/// Resolves several key selectors against the keys in the database.
		/// </summary>
		public static Task<Slice[]> GetKeysAsync(this IFdbReadOnlyTransactional dbOrTrans, IEnumerable<FdbKeySelector> selectors, CancellationToken cancellationToken = default(CancellationToken))
		{
			return dbOrTrans.ReadAsync((tr) => tr.GetKeysAsync(selectors), cancellationToken);
		}

		/// <summary>
		/// Reads several values from the database.
		/// </summary>
		public static Task<Slice[]> GetValuesAsync(this IFdbReadOnlyTransactional dbOrTrans, Slice[] keys, CancellationToken cancellationToken = default(CancellationToken))
		{
			return dbOrTrans.ReadAsync((tr) => tr.GetValuesAsync(keys), cancellationToken);
		}

		/// <summary>
		/// Reads several values from the database.
		/// </summary>
		public static Task<Slice[]> GetValuesAsync(this IFdbReadOnlyTransactional dbOrTrans, IEnumerable<Slice> keys, CancellationToken cancellationToken = default(CancellationToken))
		{
			return dbOrTrans.ReadAsync((tr) => tr.GetValuesAsync(keys), cancellationToken);
		}

		/// <summary>
		/// Change the given key to have the given value in the database. If the given key was not previously present in the database it is inserted.
		/// </summary>
		public static Task SetAsync(this IFdbTransactional dbOrTrans, Slice key, Slice value, CancellationToken cancellationToken = default(CancellationToken))
		{
			return dbOrTrans.WriteAsync((tr) => tr.Set(key, value), cancellationToken);
		}

		/// <summary>
		/// Remove the given key from the database. If the key was not previously present in the database, there is no effect.
		/// </summary>
		public static Task ClearAsync(this IFdbTransactional dbOrTrans, Slice key, CancellationToken cancellationToken = default(CancellationToken))
		{
			return dbOrTrans.WriteAsync((tr) => tr.Clear(key), cancellationToken);
		}

		/// <summary>
		/// Remove all keys (if any) which are lexicographically greater than or equal to the given begin key and lexicographically less than the given end_key.
		/// Sets and clears affect the actual database only if transaction is later committed with CommitAsync().
		/// </summary>
		public static Task ClearRangeAsync(this IFdbTransactional dbOrTrans, FdbKeyRange range, CancellationToken cancellationToken = default(CancellationToken))
		{
			return dbOrTrans.WriteAsync((tr) => tr.ClearRange(range), cancellationToken);
		}

		/// <summary>
		/// Remove all keys (if any) which are lexicographically greater than or equal to the given begin key and lexicographically less than the given end_key.
		/// Sets and clears affect the actual database only if transaction is later committed with CommitAsync().
		/// </summary>
		public static Task ClearRangeAsync(this IFdbTransactional dbOrTrans, Slice beginInclusive, Slice endExclusive, CancellationToken cancellationToken = default(CancellationToken))
		{
			return dbOrTrans.WriteAsync((tr) => tr.ClearRange(beginInclusive, endExclusive), cancellationToken);
		}

		/// <summary>
		/// Perform the operation indicated by <paramref name="operationType"/> with operand <paramref name="paramBytes"/> to the value stored by the given key.
		/// </summary>
		public static Task AtomicAsync(this IFdbTransactional dbOrTrans, Slice keyBytes, Slice paramBytes, FdbMutationType operationType, CancellationToken cancellationToken = default(CancellationToken))
		{
			return dbOrTrans.WriteAsync((tr) => tr.Atomic(keyBytes, paramBytes, operationType), cancellationToken);
		}

	}

}
