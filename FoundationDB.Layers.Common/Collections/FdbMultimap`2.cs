#region Copyright (c) 2023-2025 SnowBank SAS, (c) 2005-2023 Doxense SAS
// All rights reserved.
// 
// Redistribution and use in source and binary forms, with or without
// modification, are permitted provided that the following conditions are met:
// 	* Redistributions of source code must retain the above copyright
// 	  notice, this list of conditions and the following disclaimer.
// 	* Redistributions in binary form must reproduce the above copyright
// 	  notice, this list of conditions and the following disclaimer in the
// 	  documentation and/or other materials provided with the distribution.
// 	* Neither the name of SnowBank nor the
// 	  names of its contributors may be used to endorse or promote products
// 	  derived from this software without specific prior written permission.
// 
// THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND
// ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
// WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
// DISCLAIMED. IN NO EVENT SHALL SNOWBANK SAS BE LIABLE FOR ANY
// DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
// (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
// LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
// ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
// (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
// SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
#endregion

namespace FoundationDB.Layers.Collections
{

	/// <summary>Multimap that tracks the number of times a specific key/value pair has been inserted or removed.</summary>
	/// <typeparam name="TKey">Type of the keys of the map</typeparam>
	/// <typeparam name="TValue">Type of the values of the map</typeparam>
	[DebuggerDisplay("Location={Location}, AllowNegativeValues={AllowNegativeValues}")]
	[PublicAPI]
	public class FdbMultiMap<TKey, TValue> : IFdbLayer<FdbMultiMap<TKey, TValue>.State>
	{

		// Inspired by https://apple.github.io/foundationdb/multimaps.html
		// It is the logical equivalent of a Map<KeyValuePair<TKey, TValue>, long> where the value would be incremented each time a specific pair of (key, value) is added (and subtracted when removed)

		// The layer stores each key/value using the following format:
		// (..., key, value) = 64-bit counter

		/// <summary>Create a new multimap</summary>
		/// <param name="subspace">Location where the map will be stored in the database</param>
		/// <param name="allowNegativeValues">If true, allow negative or zero values to stay in the map.</param>
		public FdbMultiMap(ISubspaceLocation subspace, bool allowNegativeValues)
			: this(subspace.AsTyped<TKey, TValue>(), allowNegativeValues)
		{ }

		/// <summary>Create a new multimap, using a specific key and value encoder</summary>
		/// <param name="subspace">Location where the map will be stored in the database</param>
		/// <param name="allowNegativeValues">If true, allow negative or zero values to stay in the map.</param>
		public FdbMultiMap(TypedKeySubspaceLocation<TKey, TValue> subspace, bool allowNegativeValues)
		{
			this.Location = subspace ?? throw new ArgumentNullException(nameof(subspace));
			this.AllowNegativeValues = allowNegativeValues;
		}

		#region Public Properties...

		/// <summary>Subspace used to encode the keys for the items</summary>
		public TypedKeySubspaceLocation<TKey, TValue> Location { get; }

		/// <summary>If true, allow negative or zero values to stay in the map.</summary>
		public bool AllowNegativeValues { get; }

		#endregion

		[PublicAPI]
		public sealed class State
		{

			public ITypedKeySubspace<TKey, TValue> Subspace { get; }

			public bool AllowNegativeValues { get; }

			internal State(ITypedKeySubspace<TKey, TValue> subspace, bool allowNegativeValues)
			{
				this.Subspace = subspace;
				this.AllowNegativeValues = allowNegativeValues;
			}

			#region Add / Subtract / Remove...

			/// <summary>Increments the count of an (index, value) pair in the multimap.</summary>
			/// <param name="trans">Transaction used for the operation</param>
			/// <param name="key">Key of the entry</param>
			/// <param name="value">Value for the <paramref name="key"/> to increment</param>
			/// <remarks>If the (index, value) pair does not exist, its value is considered to be 0</remarks>
			/// <exception cref="System.ArgumentNullException">If <paramref name="trans"/> is null.</exception>
			public void Add(IFdbTransaction trans, TKey key, TValue value)
			{
				//note: this method does not need to be async, but subtract is, so it's better if both methods have the same shape.
				Contract.NotNull(trans);

				trans.AtomicIncrement64(this.Subspace[key, value]);
			}

			/// <summary>Decrements the count of an (index, value) pair in the multimap, and optionally removes it if the count reaches zero.</summary>
			/// <param name="trans">Transaction used for the operation</param>
			/// <param name="key">Key of the entry</param>
			/// <param name="value">Value for the <paramref name="key"/> to decrement</param>
			/// <remarks>If the updated count reaches zero or less, and AllowNegativeValues is not set, the key will be cleared from the map.</remarks>
			public void Subtract(IFdbTransaction trans, TKey key, TValue value)
			{
				Contract.NotNull(trans);

				// decrement, and optionally clear the key if it reaches zero
				trans.AtomicDecrement64(this.Subspace[key, value], clearIfZero: !this.AllowNegativeValues);
			}

			/// <summary>Checks if a (key, value) pair exists</summary>
			public async Task<bool> ContainsAsync(IFdbReadOnlyTransaction trans, TKey key, TValue value)
			{
				Contract.NotNull(trans);

				var v = await trans.GetAsync(this.Subspace[key, value]).ConfigureAwait(false);
				return this.AllowNegativeValues ? v.IsPresent : v.ToInt64() > 0;
			}

			/// <summary>Return the count for the value of a specific key</summary>
			/// <param name="trans"></param>
			/// <param name="key"></param>
			/// <param name="value"></param>
			/// <returns>Value for this value, or null if the index does not contain that particular value</returns>
			/// <remarks>The count can be zero or negative if AllowNegativeValues is enabled.</remarks>
			public async Task<long?> GetCountAsync(IFdbReadOnlyTransaction trans, TKey key, TValue value)
			{
				Contract.NotNull(trans);

				var v = await trans.GetAsync(this.Subspace[key, value]).ConfigureAwait(false);
				if (v.IsNullOrEmpty) return null;
				long c = v.ToInt64();
				return this.AllowNegativeValues || c > 0 ? c : null;
			}

			/// <summary>Query that will return the values for a specific key</summary>
			/// <param name="trans"></param>
			/// <param name="key"></param>
			/// <returns></returns>
			public IAsyncQuery<TValue?> Get(IFdbReadOnlyTransaction trans, TKey key)
			{
				Contract.NotNull(trans);

				var range = KeyRange.StartsWith(this.Subspace.EncodePartial(key));
				if (this.AllowNegativeValues)
				{
					return trans
						.GetRange(range)
						.Select(kvp => this.Subspace.Decode(kvp.Key).Item2);
				}
				else
				{
					return trans
						.GetRange(range)
						.Where(kvp => kvp.Value.ToInt64() > 0) // we need to filter out zero or negative values (possible artefacts)
						.Select(kvp => this.Subspace.Decode(kvp.Key).Item2);
				}
			}

			/// <summary>Returns the list of values for a specific key</summary>
			/// <param name="trans"></param>
			/// <param name="key"></param>
			/// <returns>List of values for this index, or an empty list if the index does not exist</returns>
			public Task<List<TValue?>> GetAsync(IFdbReadOnlyTransaction trans, TKey key)
			{
				return Get(trans, key).ToListAsync();
			}

			/// <summary>Query that will return the counts for each value for a specific key</summary>
			/// <param name="trans"></param>
			/// <param name="key"></param>
			/// <returns></returns>
			public IAsyncQuery<(TValue? Value, long Count)> GetCounts(IFdbReadOnlyTransaction trans, TKey key)
			{
				var range = KeyRange.StartsWith(this.Subspace.EncodePartial(key));

				var query = trans
					.GetRange(range)
					.Select(kvp => (Value: this.Subspace.Decode(kvp.Key).Item2, Count: kvp.Value.ToInt64()));

				return this.AllowNegativeValues
					? query
					: query.Where(x => x.Count > 0);
			}

			/// <summary>Remove all the values for a specific key</summary>
			public void Remove(IFdbTransaction trans, TKey key)
			{
				Contract.NotNull(trans);

				trans.ClearRange(KeyRange.StartsWith(this.Subspace.EncodePartial(key)));
			}

			/// <summary>Remove a value for a specific key</summary>
			public void Remove(IFdbTransaction trans, TKey key, TValue value)
			{
				Contract.NotNull(trans);

				trans.Clear(this.Subspace[key, value]);
			}

			#endregion

		}

		/// <inheritdoc />
		public async ValueTask<State> Resolve(IFdbReadOnlyTransaction tr)
		{
			var subspace = await this.Location.Resolve(tr);
			return new State(subspace, this.AllowNegativeValues);
		}

		/// <inheritdoc />
		string IFdbLayer.Name => nameof(FdbMultiMap<,>);

	}

}
