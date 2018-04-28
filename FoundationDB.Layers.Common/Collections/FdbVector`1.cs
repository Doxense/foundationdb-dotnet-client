#region BSD Licence
/* Copyright (c) 2013-2018, Doxense SAS
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
	using System;
	using System.Linq;
	using System.Threading.Tasks;
	using Doxense.Collections.Tuples;
	using Doxense.Diagnostics.Contracts;
	using Doxense.Linq;
	using Doxense.Serialization.Encoders;
	using FoundationDB.Client;
	using JetBrains.Annotations;

	/// <summary>Represents a potentially sparse array in FoundationDB.</summary>
	[PublicAPI]
	public class FdbVector<T>
	{
		// from https://apple.github.io/foundationdb/vector.html

		// Vector stores each of its values using its index as the key.
		// The size of a vector is equal to the index of its last key + 1.
		//
		// For indexes smaller than the vector's size that have no associated key
		// in the database, the value will be the specified defaultValue.
		//
		// If the last value in the vector has the default value, its key will
		// always be set so that size can be determined.
		//
		// By creating Vector with a Subspace, all kv pairs modified by the
		// layer will have keys that start within that Subspace.

		// Implementation note:
		// - vector.py uses Thread Local Storage that does not work well with async and Tasks in .NET
		//   so we wont be able to 'store' the current transaction in the vector object itself

		/// <summary>Create a new sparse Vector</summary>
		/// <param name="subspace">Subspace where the vector will be stored</param>
		/// <param name="defaultValue">Default value for sparse entries</param>
		/// <param name="encoder">Encoder used for the values of this vector</param>
		public FdbVector([NotNull] IKeySubspace subspace, T defaultValue = default(T), IValueEncoder<T> encoder = null)
			: this(subspace.AsDynamic(), defaultValue, encoder)
		{ }

		/// <summary>Create a new sparse Vector</summary>
		/// <param name="subspace">Subspace where the vector will be stored</param>
		/// <param name="defaultValue">Default value for sparse entries</param>
		/// <param name="encoder">Encoder used for the values of this vector</param>
		public FdbVector([NotNull] IDynamicKeySubspace subspace, T defaultValue, IValueEncoder<T> encoder = null)
		{
			if (subspace == null) throw new ArgumentNullException(nameof(subspace));

			this.Subspace = subspace;
			this.DefaultValue = defaultValue;
			this.Encoder = encoder ?? TuPack.Encoding.GetValueEncoder<T>();
		}


		/// <summary>Subspace used as a prefix for all items in this vector</summary>
		[NotNull]
		public IDynamicKeySubspace Subspace { get; }

		/// <summary>Default value for sparse entries</summary>
		public T DefaultValue { get; }

		[NotNull]
		public IValueEncoder<T> Encoder { get; }

		/// <summary>Get the number of items in the Vector. This number includes the sparsely represented items.</summary>
		public Task<long> SizeAsync([NotNull] IFdbReadOnlyTransaction tr)
		{
			if (tr == null) throw new ArgumentNullException(nameof(tr));

			return ComputeSizeAsync(tr);
		}

		/// <summary>Push a single item onto the end of the Vector.</summary>
		public async Task PushAsync([NotNull] IFdbTransaction tr, T value)
		{
			if (tr == null) throw new ArgumentNullException(nameof(tr));

			var size = await ComputeSizeAsync(tr).ConfigureAwait(false);

			tr.Set(GetKeyAt(size), this.Encoder.EncodeValue(value));
		}

		/// <summary>Get the value of the last item in the Vector.</summary>
		public Task<T> BackAsync([NotNull] IFdbReadOnlyTransaction tr)
		{
			if (tr == null) throw new ArgumentNullException(nameof(tr));

			return tr
				.GetRange(this.Subspace.Keys.ToRange())
				.Select((kvp) => this.Encoder.DecodeValue(kvp.Value))
				.LastOrDefaultAsync();
		}

		/// <summary>Get the value of the first item in the Vector.</summary>
		public Task<T> FrontAsync([NotNull] IFdbReadOnlyTransaction tr)
		{
			return GetAsync(tr, 0);
		}

		/// <summary>Get and pops the last item off the Vector.</summary>
		public async Task<(T Value, bool HasValue)> PopAsync([NotNull] IFdbTransaction tr)
		{
			if (tr == null) throw new ArgumentNullException(nameof(tr));

			var keyRange = this.Subspace.Keys.ToRange();

			// Read the last two entries so we can check if the second to last item
			// is being represented sparsely. If so, we will be required to set it
			// to the default value
			var lastTwo = await tr
				.GetRange(keyRange, new FdbRangeOptions { Reverse = true, Limit = 2 })
				.ToListAsync()
				.ConfigureAwait(false);

			// Vector was empty
			if (lastTwo.Count == 0) return default;

			//note: keys are reversed so indices[0] = last, indices[1] = second to last
			var indices = lastTwo.Select(kvp => this.Subspace.Keys.DecodeFirst<long>(kvp.Key)).ToList();

			if (indices[0] == 0)
			{ // Vector has size one
				//pass
			}
			else if (lastTwo.Count == 1 || indices[0] > indices[1] + 1)
			{ // Second to last item is being represented sparsely
				tr.Set(GetKeyAt(indices[0] - 1), this.Encoder.EncodeValue(this.DefaultValue));
			}

			tr.Clear(lastTwo[0].Key);

			return (this.Encoder.DecodeValue(lastTwo[0].Value), true);
		}

		/// <summary>Swap the items at positions i1 and i2.</summary>
		public async Task SwapAsync([NotNull] IFdbTransaction tr, long index1, long index2)
		{
			if (tr == null) throw new ArgumentNullException(nameof(tr));

			if (index1 < 0 || index2 < 0) throw new IndexOutOfRangeException($"Indices ({index1}, {index2}) must be positive");

			var k1 = GetKeyAt(index1);
			var k2 = GetKeyAt(index2);

			long currentSize = await ComputeSizeAsync(tr).ConfigureAwait(false);

			if (index1 >= currentSize || index2 >= currentSize) throw new IndexOutOfRangeException($"Indices ({index1}, {index2}) are out of range");

			var vs = await tr.GetValuesAsync(new[] { k1, k2 }).ConfigureAwait(false);
			var v1 = vs[0];
			var v2 = vs[1];

			if (!v2.IsNullOrEmpty)
			{
				tr.Set(k1, v2);
			}
			else if (v1.IsPresent && index1 < currentSize - 1)
			{
				tr.Clear(k1);
			}

			if (!v1.IsNullOrEmpty)
			{
				tr.Set(k2, v1);
			}
			else if (v2.IsPresent && index2 < currentSize - 1)
			{
				tr.Clear(k2);
			}
		}

		/// <summary>Get the item at the specified index.</summary>
		public async Task<T> GetAsync([NotNull] IFdbReadOnlyTransaction tr, long index)
		{
			if (tr == null) throw new ArgumentNullException(nameof(tr));
			if (index < 0) throw new IndexOutOfRangeException($"Index {index} must be positive");

			var start = GetKeyAt(index);
			var end = this.Subspace.Keys.ToRange().End;

			var output = await tr
				.GetRange(start, end)
				.FirstOrDefaultAsync()
				.ConfigureAwait(false);

			if (output.Key.HasValue)
			{
				if (output.Key == start)
				{ // The requested index had an associated key
					return this.Encoder.DecodeValue(output.Value);
				}

				// The requested index is sparsely represented
				return this.DefaultValue;
			}

			// We requested a value past the end of the vector
			throw new IndexOutOfRangeException($"Index {index} out of range");
		}

		/// <summary>[NOT YET IMPLEMENTED] Get a range of items in the Vector, returned as an async sequence.</summary>
		public IAsyncEnumerable<T> GetRangeAsync([NotNull] IFdbReadOnlyTransaction tr, long startIndex, long endIndex, long step)
		{
			if (tr == null) throw new ArgumentNullException(nameof(tr));

			//BUGUBG: implement FdbVector.GetRangeAsync() !

			throw new NotImplementedException();
		}

		/// <summary>Set the value at a particular index in the Vector.</summary>
		public void Set([NotNull] IFdbTransaction tr, long index, T value)
		{
			if (tr == null) throw new ArgumentNullException(nameof(tr));

			tr.Set(GetKeyAt(index), this.Encoder.EncodeValue(value));
		}

		/// <summary>Test whether the Vector is empty.</summary>
		public async Task<bool> EmptyAsync([NotNull] IFdbReadOnlyTransaction tr)
		{
			if (tr == null) throw new ArgumentNullException(nameof(tr));

			return (await ComputeSizeAsync(tr).ConfigureAwait(false)) == 0;
		}

		/// <summary>Grow or shrink the size of the Vector.</summary>
		public async Task ResizeAsync([NotNull] IFdbTransaction tr, long length)
		{
			if (tr == null) throw new ArgumentNullException(nameof(tr));

			long currentSize = await ComputeSizeAsync(tr).ConfigureAwait(false);

			if (length < currentSize)
			{
				tr.ClearRange(GetKeyAt(length), this.Subspace.Keys.ToRange().End);

				// Check if the new end of the vector was being sparsely represented
				if (await ComputeSizeAsync(tr).ConfigureAwait(false) < length)
				{
					tr.Set(GetKeyAt(length - 1), this.Encoder.EncodeValue(this.DefaultValue));
				}
			}
			else if (length > currentSize)
			{
				tr.Set(GetKeyAt(length - 1), this.Encoder.EncodeValue(this.DefaultValue));
			}
		}

		/// <summary>Remove all items from the Vector.</summary>
		public void Clear([NotNull] IFdbTransaction tr)
		{
			if (tr == null) throw new ArgumentNullException(nameof(tr));

			tr.ClearRange(this.Subspace);
		}

		#region Private Helpers...

		private async Task<long> ComputeSizeAsync(IFdbReadOnlyTransaction tr)
		{
			Contract.Requires(tr != null);

			var keyRange = this.Subspace.Keys.ToRange();

			var lastKey = await tr.GetKeyAsync(KeySelector.LastLessOrEqual(keyRange.End)).ConfigureAwait(false);

			if (lastKey < keyRange.Begin)
			{
				return 0;
			}

			return this.Subspace.Keys.DecodeFirst<long>(lastKey) + 1;
		}

		private Slice GetKeyAt(long index)
		{
			return this.Subspace.Keys.Encode(index);
		}

		#endregion
	}

}
