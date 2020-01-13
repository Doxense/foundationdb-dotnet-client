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

namespace FoundationDB.Layers.Collections
{
	using System;
	using System.Collections.Generic;
	using System.Diagnostics;
	using System.Threading;
	using System.Threading.Tasks;
	using Doxense.Collections.Tuples;
	using Doxense.Serialization.Encoders;
	using FoundationDB.Client;
#if DEBUG
	using FoundationDB.Filters.Logging;
#endif
	using JetBrains.Annotations;

	/// <summary>Provides a high-contention Queue class</summary>
	[DebuggerDisplay("Location={Location}")]
	public class FdbQueue<T>
	{
		/// <summary>Create a new queue using either High Contention mode or Simple mode</summary>
		/// <param name="location">Subspace where the queue will be stored</param>
		/// <param name="encoder">Encoder for the values stored in this queue</param>
		/// <remarks>Uses the default Tuple serializer</remarks>
		public FdbQueue([NotNull] ISubspaceLocation location, IValueEncoder<T> encoder = null)
			: this(location.AsTyped<VersionStamp>(), encoder)
		{ }

		/// <summary>Create a new queue using either High Contention mode or Simple mode</summary>
		/// <param name="location">Subspace where the queue will be stored</param>
		/// <param name="encoder">Encoder for the values stored in this queue</param>
		public FdbQueue([NotNull] TypedKeySubspaceLocation<VersionStamp> location, IValueEncoder<T> encoder = null)
		{
			this.Location = location ?? throw new ArgumentNullException(nameof(location));
			this.Encoder = encoder ?? TuPack.Encoding.GetValueEncoder<T>();
		}

		/// <summary>Subspace used as a prefix for all items in this table</summary>
		[NotNull]
		public TypedKeySubspaceLocation<VersionStamp> Location { get; }

		/// <summary>Serializer for the elements of the queue</summary>
		[NotNull]
		public IValueEncoder<T> Encoder { get; }

		/// <summary>Remove all items from the queue.</summary>
		public async Task ClearAsync([NotNull] IFdbTransaction tr)
		{
			if (tr == null) throw new ArgumentNullException(nameof(tr));

			var subspace = await this.Location.Resolve(tr);
			tr.ClearRange(subspace.ToRange());
		}

		/// <summary>Push a single item onto the queue.</summary>
		public async Task PushAsync([NotNull] IFdbTransaction tr, T value)
		{
			if (tr == null) throw new ArgumentNullException(nameof(tr));

#if DEBUG
			tr.Annotate("Push({0})", value);
#endif
			var subspace = await this.Location.Resolve(tr);

			//BUGBUG: can be called multiple times per transaction, so need a unique stamp _per_ transaction!!
			tr.SetVersionStampedKey(subspace[tr.CreateUniqueVersionStamp()], this.Encoder.EncodeValue(value));
		}

		private static readonly FdbRangeOptions SingleOptions = new FdbRangeOptions() { Limit = 1, Mode = FdbStreamingMode.Exact };

		/// <summary>Pop the next item from the queue. Cannot be composed with other functions in a single transaction.</summary>
		public async Task<(T Value, bool HasValue)> PopAsync([NotNull] IFdbTransaction tr)
		{
			if (tr == null) throw new ArgumentNullException(nameof(tr));
#if DEBUG
			tr.Annotate("Pop()");
#endif
			var subspace = await this.Location.Resolve(tr);

			var first = await tr.GetRangeAsync(subspace.ToRange(), SingleOptions);
			if (first.IsEmpty)
			{
#if DEBUG
				tr.Annotate("Got nothing");
#endif
				return default;
			}

			tr.Clear(first[0].Key);
#if DEBUG
			if (tr.IsLogged()) tr.Annotate($"Got key {subspace.Decode(first[0].Key)} = {first[0].Value:V}");
#endif
			return (this.Encoder.DecodeValue(first[0].Value), true);
		}

		/// <summary>Test whether the queue is empty.</summary>
		public async Task<bool> EmptyAsync([NotNull] IFdbReadOnlyTransaction tr)
		{
			var subspace = await this.Location.Resolve(tr);

			var first = await tr.GetRangeAsync(subspace.ToRange(), FdbQueue<T>.SingleOptions);
			return first.IsEmpty;
		}

		/// <summary>Get the value of the next item in the queue without popping it.</summary>
		public async Task<(T Value, bool HasValue)> PeekAsync([NotNull] IFdbReadOnlyTransaction tr)
		{
			var subspace = await this.Location.Resolve(tr);

			var first = await tr.GetRangeAsync(subspace.ToRange(), FdbQueue<T>.SingleOptions);
			if (first.IsEmpty) return default;

			return (this.Encoder.DecodeValue(first[0].Value), true);
		}

		#region Bulk Operations

		public Task ExportAsync(IFdbDatabase db, Action<T, long> handler, CancellationToken ct)
		{
			if (db == null) throw new ArgumentNullException(nameof(db));
			if (handler == null) throw new ArgumentNullException(nameof(handler));

			//REVIEW: is this approach correct ?

			return Fdb.Bulk.ExportAsync(
				db,
				this.Location,
				(kvs, _, offset, __) =>
				{
					foreach(var kv in kvs)
					{
						if (ct.IsCancellationRequested) ct.ThrowIfCancellationRequested();

						handler(this.Encoder.DecodeValue(kv.Value), offset);
						++offset;
					}
					return Task.CompletedTask;
				},
				ct
			);
		}

		public Task ExportAsync(IFdbDatabase db, Func<T, long, Task> handler, CancellationToken ct)
		{
			if (db == null) throw new ArgumentNullException(nameof(db));
			if (handler == null) throw new ArgumentNullException(nameof(handler));

			//REVIEW: is this approach correct ?

			return Fdb.Bulk.ExportAsync(
				db,
				this.Location,
				async (kvs, _, offset, __) =>
				{
					foreach (var kv in kvs)
					{
						if (ct.IsCancellationRequested) ct.ThrowIfCancellationRequested();

						await handler(this.Encoder.DecodeValue(kv.Value), offset);
						++offset;
					}
				},
				ct
			);
		}

		public Task ExportAsync(IFdbDatabase db, Action<T[], long> handler, CancellationToken ct)
		{
			if (db == null) throw new ArgumentNullException(nameof(db));
			if (handler == null) throw new ArgumentNullException(nameof(handler));

			//REVIEW: is this approach correct ?

			return Fdb.Bulk.ExportAsync(
				db,
				this.Location,
				(kvs, _, offset, __) =>
				{
					handler(this.Encoder.DecodeValues(kvs), offset);
					return Task.CompletedTask;
				},
				ct
			);
		}

		public Task ExportAsync(IFdbDatabase db, Func<T[], long, Task> handler, CancellationToken ct)
		{
			if (db == null) throw new ArgumentNullException(nameof(db));
			if (handler == null) throw new ArgumentNullException(nameof(handler));

			//REVIEW: is this approach correct ?

			return Fdb.Bulk.ExportAsync(
				db,
				this.Location,
				(kvs, _, offset, __) => handler(this.Encoder.DecodeValues(kvs), offset),
				ct
			);
		}

		#endregion

	}

}
