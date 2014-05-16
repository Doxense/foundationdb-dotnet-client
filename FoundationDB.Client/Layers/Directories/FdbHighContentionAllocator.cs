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

namespace FoundationDB.Layers.Directories
{
	using FoundationDB.Client;
	using System;
	using System.Diagnostics;
	using System.Threading.Tasks;

	[DebuggerDisplay("Subspace={Subspace}")]
	public sealed class FdbHighContentionAllocator
	{
		private const int COUNTERS = 0;
		private const int RECENT = 1;

		private readonly Random m_rnd = new Random();

		public FdbHighContentionAllocator(FdbSubspace subspace)
		{
			if (subspace == null) throw new ArgumentException("subspace");

			this.Subspace = subspace;
			this.Counters = subspace.Partition(COUNTERS);
			this.Recent = subspace.Partition(RECENT);
		}

		/// <summary>Location of the allocator</summary>
		public FdbSubspace Subspace { get; private set; }

		private FdbSubspace Counters { get; set; }

		private FdbSubspace Recent { get; set; }

		/// <summary>Returns a 64-bit integer that
		/// 1) has never and will never be returned by another call to this
		///    method on the same subspace
		/// 2) is nearly as short as possible given the above
		/// </summary>
		public async Task<long> AllocateAsync(IFdbTransaction trans)
		{
			// find the current window size, by reading the last entry in the 'counters' subspace
			long start = 0, count = 0;
			var kv = await trans
				.Snapshot
				.GetRange(this.Counters.ToRange())
				.LastOrDefaultAsync();

			if (kv.Key.IsPresent)
			{
				start = this.Counters.UnpackSingle<long>(kv.Key);
				count = kv.Value.ToInt64();
			}

			// check if the window is full
			int window = GetWindowSize(start);
			if ((count + 1) * 2 >= window)
			{ // advance the window
				trans.ClearRange(this.Counters.Key, this.Counters.Pack(start) + FdbKey.MinValue);
				start += window;
				trans.ClearRange(this.Recent.Key, this.Recent.Pack(start));
			}

			// Increment the allocation count for the current window
			trans.AtomicAdd(this.Counters.Pack(start), Slice.FromFixed64(1));

			// As of the snapshot being read from, the window is less than half
            // full, so this should be expected to take 2 tries.  Under high
            // contention (and when the window advances), there is an additional
            // subsequent risk of conflict for this transaction.
			while (true)
			{
				// Find a random free slot in the current window...
				long candidate;
				lock (m_rnd)
				{
					candidate = start + m_rnd.Next(window);
				}

				// test if the key is used
				var key = this.Recent.Pack(candidate);
				var value = await trans.GetAsync(key).ConfigureAwait(false);

				if (value.IsNull)
				{ // free slot

					// mark as used
					trans.Set(key, Slice.Empty);
					return candidate;
				}

				// no luck this time, try again...
			}
		}

		private static int GetWindowSize(long start)
		{
			if (start < 0) throw new ArgumentOutOfRangeException("start", start, "Start offset must be a positive integer");

			if (start < 255) return 64;
			if (start < 65535) return 1024;
			return 8192;
		}

	}

}
