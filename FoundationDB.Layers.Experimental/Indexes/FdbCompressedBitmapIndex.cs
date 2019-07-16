#region BSD License
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

namespace FoundationDB.Layers.Experimental.Indexing
{
	using System;
	using System.Collections.Generic;
	using System.Diagnostics;
	using System.Globalization;
	using System.Linq;
	using System.Threading.Tasks;
	using Doxense.Diagnostics.Contracts;
	using FoundationDB.Client;
	using JetBrains.Annotations;

	/// <summary>Simple index that maps values of type <typeparamref name="TValue"/> into lists of numerical ids</summary>
	/// <typeparam name="TValue">Type of the value being indexed</typeparam>
	[DebuggerDisplay("Name={Name}, Subspace={Subspace}, IndexNullValues={IndexNullValues})")]
	public class FdbCompressedBitmapIndex<TValue>
	{

		public FdbCompressedBitmapIndex([NotNull] string name, [NotNull] IKeySubspace subspace, IEqualityComparer<TValue> valueComparer = null, bool indexNullValues = false)
			: this(name, subspace.AsTyped<TValue>(), valueComparer, indexNullValues)
		{ }

		public FdbCompressedBitmapIndex([NotNull] string name, [NotNull] ITypedKeySubspace<TValue> subspace, IEqualityComparer<TValue> valueComparer = null, bool indexNullValues = false)
		{
			Contract.NotNull(name, nameof(name));
			Contract.NotNull(subspace, nameof(subspace));

			this.Name = name;
			this.Subspace = subspace;
			this.ValueComparer = valueComparer ?? EqualityComparer<TValue>.Default;
			this.IndexNullValues = indexNullValues;
		}

		[NotNull]
		public string Name { get; }

		[NotNull]
		public ITypedKeySubspace<TValue> Subspace { get; }

		[NotNull]
		public IEqualityComparer<TValue> ValueComparer { get; }

		/// <summary>If true, null values are inserted in the index. If false (default), they are ignored</summary>
		/// <remarks>This has no effect if <typeparamref name="TValue" /> is not a reference type</remarks>
		public bool IndexNullValues { get; }

		/// <summary>Insert a newly created entity to the index</summary>
		/// <param name="trans">Transaction to use</param>
		/// <param name="id">Id of the new entity (that was never indexed before)</param>
		/// <param name="value">Value of this entity in the index</param>
		/// <returns>True if a value was inserted into the index; or false if <paramref name="value"/> is null and <see cref="IndexNullValues"/> is false, or if this <paramref name="id"/> was already indexed at this <paramref name="value"/>.</returns>
		public async Task<bool> AddAsync([NotNull] IFdbTransaction trans, long id, TValue value)
		{
			if (trans == null) throw new ArgumentNullException(nameof(trans));

			if (this.IndexNullValues || value != null)
			{
				var key = this.Subspace.Keys[value];
				var data = await trans.GetAsync(key).ConfigureAwait(false);
				var builder = data.HasValue ? new CompressedBitmapBuilder(MutableSlice.AsUnsafeMutableSlice(data)) : CompressedBitmapBuilder.Empty;

				//TODO: wasteful to crate a builder to only set on bit ?
				builder.Set((int)id); //BUGBUG: id should be 64-bit!

				//TODO: if bit was already set, skip the set ?
				trans.Set(key, builder.ToSlice());
				return true;
			}
			return false;
		}

		/// <summary>Update the indexed values of an entity</summary>
		/// <param name="trans">Transaction to use</param>
		/// <param name="id">Id of the entity that has changed</param>
		/// <param name="newValue">Previous value of this entity in the index</param>
		/// <param name="previousValue">New value of this entity in the index</param>
		/// <returns>True if a change was performed in the index; otherwise false (if <paramref name="previousValue"/> and <paramref name="newValue"/>)</returns>
		/// <remarks>If <paramref name="newValue"/> and <paramref name="previousValue"/> are identical, then nothing will be done. Otherwise, the old index value will be deleted and the new value will be added</remarks>
		public async Task<bool> UpdateAsync([NotNull] IFdbTransaction trans, long id, TValue newValue, TValue previousValue)
		{
			if (trans == null) throw new ArgumentNullException(nameof(trans));

			if (!this.ValueComparer.Equals(newValue, previousValue))
			{
				// remove previous value
				if (this.IndexNullValues || previousValue != null)
				{
					var key = this.Subspace.Keys[previousValue];
					var data = await trans.GetAsync(key).ConfigureAwait(false);
					if (data.HasValue)
					{
						var builder = new CompressedBitmapBuilder(MutableSlice.AsUnsafeMutableSlice(data));
						builder.Clear((int)id); //BUGBUG: 64 bit id!
						trans.Set(key, builder.ToSlice());
					}
				}

				// add new value
				if (this.IndexNullValues || newValue != null)
				{
					var key = this.Subspace.Keys[newValue];
					var data = await trans.GetAsync(key).ConfigureAwait(false);
					var builder = data.HasValue ? new CompressedBitmapBuilder(MutableSlice.AsUnsafeMutableSlice(data)) : CompressedBitmapBuilder.Empty;
					builder.Set((int)id); //BUGBUG: 64 bit id!
					trans.Set(key, builder.ToSlice());
				}

				// cannot be both null, so we did at least something)
				return true;
			}
			return false;
		}

		/// <summary>Remove an entity from the index</summary>
		/// <param name="trans">Transaction to use</param>
		/// <param name="id">Id of the entity that has been deleted</param>
		/// <param name="value">Previous value of the entity in the index</param>
		public async Task<bool> RemoveAsync([NotNull] IFdbTransaction trans, long id, TValue value)
		{
			if (trans == null) throw new ArgumentNullException(nameof(trans));

			var key = this.Subspace.Keys[value];
			var data = await trans.GetAsync(key).ConfigureAwait(false);
			if (data.HasValue)
			{
				var builder = new CompressedBitmapBuilder(MutableSlice.AsUnsafeMutableSlice(data));
				builder.Clear((int)id); //BUGBUG: 64 bit id!
				trans.Set(key, builder.ToSlice());
				return true;
			}
			return false;
		}

		/// <summary>Returns a list of ids matching a specific value</summary>
		/// <param name="trans"></param>
		/// <param name="value">Value to lookup</param>
		/// <param name="reverse"></param>
		/// <returns>List of document ids matching this value for this particular index (can be empty if no document matches)</returns>
		public async Task<IEnumerable<long>> LookupAsync([NotNull] IFdbReadOnlyTransaction trans, TValue value, bool reverse = false)
		{
			var key = this.Subspace.Keys[value];
			var data = await trans.GetAsync(key).ConfigureAwait(false);
			if (data.IsNull) return null;
			if (data.IsEmpty) return Enumerable.Empty<long>();
			var bitmap = new CompressedBitmap(MutableSlice.AsUnsafeMutableSlice(data));
			if (reverse) throw new NotImplementedException(); //TODO: GetView(reverse:true) !
			return bitmap.GetView().Select(x => (long)x /*BUGBUG 64 bits*/);
		}
		
		public override string ToString()
		{
			return string.Format(CultureInfo.InvariantCulture, "BitmapIndex['{0}']", this.Name);
		}

	}

}
