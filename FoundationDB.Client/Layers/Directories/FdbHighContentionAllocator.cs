#region Copyright (c) 2023 SnowBank SAS, (c) 2005-2023 Doxense SAS
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

namespace FoundationDB.Layers.Allocators
{
	using System;
	using System.Diagnostics;
	using System.Threading.Tasks;
	using Doxense.Diagnostics.Contracts;
	using FoundationDB.Client;
	using FoundationDB.Filters.Logging;

	/// <summary>Custom allocator that generates unique integer values with low probability of conflicts</summary>
	[DebuggerDisplay("Location={" + nameof(Location) + "}")]
	public sealed class FdbHighContentionAllocator : IFdbLayer<FdbHighContentionAllocator.State>
	{
		private const int COUNTERS = 0;
		private const int RECENT = 1;

		private readonly Random m_rnd = new Random();

		/// <summary>Create an allocator operating under a specific location</summary>
		public FdbHighContentionAllocator(ISubspaceLocation location)
		{
			Contract.NotNull(location);

			this.Location = location.AsTyped<int, long>();
		}

		/// <summary>Location of the allocator</summary>
		public TypedKeySubspaceLocation<int, long> Location { get; }

		public async ValueTask<State> Resolve(IFdbReadOnlyTransaction tr)
		{
			var subspace = await this.Location.Resolve(tr);
			if (subspace == null) throw new InvalidOperationException($"Location '{this.Location}' referenced by this high contention allocator was not found.");
			return new State(subspace, m_rnd);
		}

		[DebuggerDisplay("Subspace={" + nameof(Subspace) + "}")]
		public sealed class State
		{
			/// <summary>Location of the allocator</summary>
			public ITypedKeySubspace<int, long> Subspace { get; }

			private Random Rng { get; }

			public State(ITypedKeySubspace<int, long> subspace, Random rng)
			{
				this.Subspace = subspace;
				this.Rng = rng;
			}

			/// <summary>Returns a 64-bit integer that
			/// 1) has never and will never be returned by another call to this
			///    method on the same subspace
			/// 2) is nearly as short as possible given the above
			/// </summary>
			public Task<long> AllocateAsync(IFdbTransaction trans)
			{
				return FdbHighContentionAllocator.AllocateAsync(trans, this.Subspace, this.Rng);
			}

		}

		public static async Task<long> AllocateAsync(IFdbTransaction trans, ITypedKeySubspace<int, long> subspace, Random rng)
		{
			Contract.NotNull(trans);

			// find the current window size, by reading the last entry in the 'counters' subspace
			long start = 0, count = 0;
			var kv = await trans
				.Snapshot
				.GetRange(subspace.EncodePartialRange(COUNTERS))
				.LastOrDefaultAsync();

			if (kv.Key.Count != 0)
			{
				start = subspace.DecodeLast(kv.Key);
				count = kv.Value.ToInt64();
			}

			// check if the window is full
			int window = GetWindowSize(start);
			if ((count + 1) * 2 >= window)
			{ // advance the window
				if (FdbDirectoryLayer.AnnotateTransactions) trans.Annotate("Advance allocator window size to {0} starting at {1}", window, start + window);
				trans.ClearRange(subspace[COUNTERS, 0], subspace[COUNTERS, start + 1]);
				start += window;
				count = 0;
				trans.ClearRange(subspace[RECENT, 0], subspace[RECENT, start]);
			}

			// Increment the allocation count for the current window
			trans.AtomicAdd64(subspace[COUNTERS, start], 1);

			// As of the snapshot being read from, the window is less than half
			// full, so this should be expected to take 2 tries.  Under high
			// contention (and when the window advances), there is an additional
			// subsequent risk of conflict for this transaction.
			while (true)
			{
				// Find a random free slot in the current window...
				long candidate;
				lock (rng)
				{
					candidate = start + rng.Next(window);
				}

				// test if the key is used
				var key = subspace[RECENT, candidate];
				var value = await trans.GetAsync(key).ConfigureAwait(false);

				if (value.IsNull)
				{ // free slot

					// mark as used
					trans.Set(key, Slice.Empty);
					if (FdbDirectoryLayer.AnnotateTransactions) trans.Annotate("Allocated prefix {0} from window [{1}..{2}] ({3} used)", candidate, start, start + window - 1, count + 1);
					return candidate;
				}

				// no luck this time, try again...
			}

		}

		private static int GetWindowSize(long start)
		{
			if (start < 0) throw new ArgumentOutOfRangeException(nameof(start), start, "Start offset must be a positive integer");

			if (start < 255) return 64;
			if (start < 65535) return 1024;
			return 8192;
		}

	}

}
