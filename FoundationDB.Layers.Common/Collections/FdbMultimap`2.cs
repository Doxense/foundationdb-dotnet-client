#region BSD Licence
/* Copyright (c) 2014-2015, Doxense SAS
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
	using FoundationDB.Linq;
	using JetBrains.Annotations;
	using System;
	using System.Collections.Generic;
	using System.Diagnostics;
	using System.Threading.Tasks;

	/// <summary>Multimap that tracks the number of times a specific key/value pair has been inserted or removed.</summary>
	/// <typeparam name="TKey">Type of the keys of the map</typeparam>
	/// <typeparam name="TValue">Type of the values of the map</typeparam>
	[DebuggerDisplay("Subspace={Subspace}")]
	public class FdbMultiMap<TKey, TValue>
	{
		// Inspired by https://foundationdb.com/recipes/developer/multimaps
		// It is the logical equivalent of a Map<KeyValuePair<TKey, TValue>, long> where the value would be incremented each time a specific pair of (key, value) is added (and subtracted when removed)

		// The layer stores each key/value using the following format:
		// (..., key, value) = 64-bit counter

		private static readonly Slice PlusOne = Slice.FromFixed64(1);
		private static readonly Slice MinusOne = Slice.FromFixed64(-1);

		/// <summary>Create a new multimap</summary>
		/// <param name="subspace">Location where the map will be stored in the database</param>
		/// <param name="allowNegativeValues">If true, allow negative or zero values to stay in the map.</param>
		public FdbMultiMap(IFdbSubspace subspace, bool allowNegativeValues)
			: this(subspace, allowNegativeValues, KeyValueEncoders.Tuples.CompositeKey<TKey, TValue>())
		{ }

		/// <summary>Create a new multimap, using a specific key and value encoder</summary>
		/// <param name="subspace">Location where the map will be stored in the database</param>
		/// <param name="allowNegativeValues">If true, allow negative or zero values to stay in the map.</param>
		/// <param name="encoder">Encoder for the key/value pairs</param>
		public FdbMultiMap(IFdbSubspace subspace, bool allowNegativeValues, ICompositeKeyEncoder<TKey, TValue> encoder)
		{
			if (subspace == null) throw new ArgumentNullException("subspace");
			if (encoder == null) throw new ArgumentNullException("encoder");

			this.Subspace = subspace;
			this.AllowNegativeValues = allowNegativeValues;
			this.Location = subspace.UsingEncoder(encoder);
		}

		#region Public Properties...

		/// <summary>Subspace used as a prefix for all items in this map</summary>
		public IFdbSubspace Subspace { [NotNull] get; }

		/// <summary>If true, allow negative or zero values to stay in the map.</summary>
		public bool AllowNegativeValues { get; }

		/// <summary>Subspace used to encoded the keys for the items</summary>
		protected IFdbEncoderSubspace<TKey, TValue> Location { [NotNull] get; }

		#endregion

		#region Add / Subtract / Remove...

		/// <summary>Increments the count of an (index, value) pair in the multimap.</summary>
		/// <param name="trans">Transaction used for the operation</param>
		/// <param name="key">Key of the entry</param>
		/// <param name="value">Value for the <paramref name="key"/> to increment</param>
		/// <remarks>If the (index, value) pair does not exist, its value is considered to be 0</remarks>
		/// <exception cref="System.ArgumentNullException">If <paramref name="trans"/> is null.</exception>
		public Task AddAsync([NotNull] IFdbTransaction trans, TKey key, TValue value)
		{
			//note: this method does not need to be async, but subtract is, so it's better if both methods have the same shape.
			if (trans == null) throw new ArgumentNullException("trans");

			trans.AtomicAdd(this.Location.Keys.Encode(key, value), PlusOne);
			return FoundationDB.Async.TaskHelpers.CompletedTask;
		}

		/// <summary>Decrements the count of an (index, value) pair in the multimap, and optionally removes it if the count reaches zero.</summary>
		/// <param name="trans">Transaction used for the operation</param>
		/// <param name="key">Key of the entry</param>
		/// <param name="value">Value for the <paramref name="key"/> to decrement</param>
		/// <remarks>If the updated count reaches zero or less, and AllowNegativeValues is not set, the key will be cleared from the map.</remarks>
		public async Task SubtractAsync([NotNull] IFdbTransaction trans, TKey key, TValue value)
		{
			if (trans == null) throw new ArgumentNullException("trans");

			Slice k = this.Location.Keys.Encode(key, value);
			if (this.AllowNegativeValues)
			{
				trans.AtomicAdd(k, MinusOne);
				// note: it's faster, but we will end up with counts less than or equal to 0
				// If 'k' does not already exist, its count will be set to -1
			}
			else
			{
				Slice v = await trans.GetAsync(k).ConfigureAwait(false);
				if (this.AllowNegativeValues || v.ToInt64() > 1) //note: Slice.Nil.ToInt64() will return 0
				{
					trans.AtomicAdd(k, MinusOne);
					//note: since we already read 'k', the AtomicAdd will be optimized into the equivalent of Set(k, v - 1) by the client, unless RYW has been disabled on the transaction
					//TODO: if AtomicMax ever gets implemented, we could use it to truncate the values to 0
				}
				else
				{
					trans.Clear(k);
				}
			}
		}

		/// <summary>Checks if a (key, value) pair exists</summary>
		public async Task<bool> ContainsAsync([NotNull] IFdbReadOnlyTransaction trans, TKey key, TValue value)
		{
			if (trans == null) throw new ArgumentNullException("trans");
		
			var v = await trans.GetAsync(this.Location.Keys.Encode(key, value)).ConfigureAwait(false);
			return this.AllowNegativeValues ? v.IsPresent : v.ToInt64() > 0;
		}

		/// <summary>Return the count for the value of a specific key</summary>
		/// <param name="trans"></param>
		/// <param name="key"></param>
		/// <param name="value"></param>
		/// <returns>Value for this value, or null if the index does not contains that particular value</returns>
		/// <remarks>The count can be zero or negative if AllowNegativeValues is enable.</remarks>
		public async Task<long?> GetCountAsync([NotNull] IFdbReadOnlyTransaction trans, TKey key, TValue value)
		{
			if (trans == null) throw new ArgumentNullException("trans");

			Slice v = await trans.GetAsync(this.Location.Keys.Encode(key, value)).ConfigureAwait(false);
			if (v.IsNullOrEmpty) return null;
			long c = v.ToInt64();
			return this.AllowNegativeValues || c > 0 ? c : default(long?);
		}

		/// <summary>Query that will return the values for a specific key</summary>
		/// <param name="trans"></param>
		/// <param name="key"></param>
		/// <returns></returns>
		[NotNull]
		public IFdbAsyncEnumerable<TValue> Get([NotNull] IFdbReadOnlyTransaction trans, TKey key)
		{
			if (trans == null) throw new ArgumentNullException("trans");

			var range = KeyRange.StartsWith(this.Location.Partial.Keys.Encode(key));
			if (this.AllowNegativeValues)
			{
				return trans
					.GetRange(range)
					.Select(kvp => this.Location.Keys.Decode(kvp.Key).Item2);
			}
			else
			{
				return trans
					.GetRange(range)
					.Where(kvp => kvp.Value.ToInt64() > 0) // we need to filter out zero or negative values (possible artefacts)
					.Select(kvp => this.Location.Keys.Decode(kvp.Key).Item2);
			}
		}

		/// <summary>Returns the list of values for a specific key</summary>
		/// <param name="trans"></param>
		/// <param name="key"></param>
		/// <returns>List of values for this index, or an empty list if the index does not exist</returns>
		public Task<List<TValue>> GetAsync([NotNull] IFdbReadOnlyTransaction trans, TKey key)
		{
			return Get(trans, key).ToListAsync();
		}

		/// <summary>Query that will return the counts for each value for a specific key</summary>
		/// <param name="trans"></param>
		/// <param name="key"></param>
		/// <returns></returns>
		[NotNull]
		public IFdbAsyncEnumerable<KeyValuePair<TValue, long>> GetCounts([NotNull] IFdbReadOnlyTransaction trans, TKey key)
		{
			var range = KeyRange.StartsWith(this.Location.Partial.Keys.Encode(key));

			var query = trans
				.GetRange(range)
				.Select(kvp => new KeyValuePair<TValue, long>(this.Location.Keys.Decode(kvp.Key).Item2, kvp.Value.ToInt64()));

			if (this.AllowNegativeValues)
			{
				return query;
			}
			else
			{
				return query.Where(kvp => kvp.Value > 0);
			}
		}

		/// <summary>Returns a dictionary with of the counts of each value for a specific key</summary>
		/// <param name="trans"></param>
		/// <param name="key"></param>
		/// <param name="comparer"></param>
		/// <returns></returns>
		public Task<Dictionary<TValue, long>> GetCountsAsync([NotNull] IFdbReadOnlyTransaction trans, TKey key, IEqualityComparer<TValue> comparer = null)
		{
			return GetCounts(trans, key).ToDictionaryAsync(comparer);
		}

		/// <summary>Remove all the values for a specific key</summary>
		/// <param name="trans"></param>
		/// <param name="key"></param>
		/// <returns></returns>
		public void Remove([NotNull] IFdbTransaction trans, TKey key)
		{
			if (trans == null) throw new ArgumentNullException("trans");

			trans.ClearRange(KeyRange.StartsWith(this.Location.Partial.Keys.Encode(key)));
		}

		/// <summary>Remove a value for a specific key</summary>
		/// <param name="trans"></param>
		/// <param name="key"></param>
		/// <param name="value"></param>
		/// <returns></returns>
		public void Remove([NotNull] IFdbTransaction trans, TKey key, TValue value)
		{
			if (trans == null) throw new ArgumentNullException("trans");

			trans.Clear(this.Location.Keys.Encode(key, value));
		}

		#endregion

	}

}
