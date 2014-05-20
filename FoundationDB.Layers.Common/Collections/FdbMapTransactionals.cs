#region BSD Licence
/* Copyright (c) 2013-2014, Doxense SAS
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
	using JetBrains.Annotations;
	using System;
	using System.Threading;
	using System.Threading.Tasks;

	public static class FdbMapExtensions
	{

		/// <summary>Returns the value of an existing entry in the map</summary>
		/// <param name="db">Transactional used for the operation</param>
		/// <param name="id">Key of the entry to read from the map</param>
		/// <param name="cancellationToken">Token used to cancel the operation</param>
		/// <returns>Value of the entry if it exists; otherwise, throws an exception</returns>
		/// <exception cref="System.ArgumentNullException">If either <paramref name="db"/> or <paramref name="id"/> is null.</exception>
		/// <exception cref="System.Collections.Generic.KeyNotFoundException">If the map does not contain an entry with this key.</exception>
		public static Task<TValue> GetAsync<TKey, TValue>(this FdbMap<TKey, TValue> map, [NotNull] IFdbReadOnlyTransactional db, TKey id, CancellationToken cancellationToken)
		{
			if (map == null) throw new ArgumentNullException("map");
			if (db == null) throw new ArgumentNullException("db");

			return db.ReadAsync((tr) => map.GetAsync(tr, id), cancellationToken);
		}

		/// <summary>Returns the value of an entry in the map if it exists.</summary>
		/// <param name="db">Transactional used for the operation</param>
		/// <param name="id">Key of the entry to read from the map</param>
		/// <param name="cancellationToken">Token used to cancel the operation</param>
		/// <returns>Optional with the value of the entry it it exists, or an empty result if it is not present in the map.</returns>
		public static Task<Optional<TValue>> TryGetAsync<TKey, TValue>(this FdbMap<TKey, TValue> map, [NotNull] IFdbReadOnlyTransactional db, TKey id, CancellationToken cancellationToken)
		{
			if (map == null) throw new ArgumentNullException("map");
			if (db == null) throw new ArgumentNullException("db");

			return db.ReadAsync((tr) => map.TryGetAsync(tr, id), cancellationToken);
		}

		/// <summary>Add or update an entry in the map</summary>
		/// <param name="db">Transactional used for the operation</param>
		/// <param name="id">Key of the entry to add or update</param>
		/// <param name="value">New value of the entry</param>
		/// <param name="cancellationToken">Token used to cancel the operation</param>
		/// <remarks>If the entry did not exist, it will be created. If not, its value will be replace with <paramref name="value"/>.</remarks>
		public static Task SetAsync<TKey, TValue>(this FdbMap<TKey, TValue> map, [NotNull] IFdbTransactional db, TKey id, TValue value, CancellationToken cancellationToken)
		{
			if (map == null) throw new ArgumentNullException("map");
			if (db == null) throw new ArgumentNullException("db");

			return db.WriteAsync((tr) => map.Set(tr, id, value), cancellationToken);
		}

		/// <summary>Remove an entry from the map</summary>
		/// <param name="db">Transactional used for the operation</param>
		/// <param name="id">Key of the entry to remove</param>
		/// <param name="cancellationToken">Token used to cancel the operation</param>
		/// <remarks>If the entry did not exist, the operation will not do anything.</remarks>
		public static Task ClearAsync<TKey, TValue>(this FdbMap<TKey, TValue> map, [NotNull] IFdbTransactional db, TKey id, CancellationToken cancellationToken)
		{
			if (map == null) throw new ArgumentNullException("map");
			if (db == null) throw new ArgumentNullException("db");

			return db.WriteAsync((tr) => map.Clear(tr, id), cancellationToken);
		}

	}

}
