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

namespace FoundationDB.Filters
{
	using FoundationDB.Client;
	using System;
	using System.Linq;
	using System.Threading;
	using System.Threading.Tasks;

	/// <summary>[PROOF OF CONCEPT, DO NOT USE YET!] Transaction filter that automatically appends/remove a fixed prefix to all keys</summary>
	public sealed class PrefixRewriterTransaction : FdbTransactionFilter
	{
		// We will add a prefix to all keys sent to the db, and remove it on the way back

		public PrefixRewriterTransaction(IKeySubspace prefix, IFdbTransaction trans, bool ownsTransaction)
			: base(trans, false, ownsTransaction)
		{
			this.Prefix = prefix ?? throw new ArgumentNullException(nameof(prefix));
		}

		public IKeySubspace Prefix { get; }

		private Slice Encode(Slice key)
		{
			return this.Prefix[key];
		}

		private Slice[] Encode(Slice[] keys)
		{
			return keys.Select(k => this.Prefix[k]).ToArray();
		}

		private KeySelector Encode(KeySelector selector)
		{
			return new KeySelector(
				this.Prefix[selector.Key],
				selector.OrEqual,
				selector.Offset
			);
		}

		private KeySelector[] Encode(KeySelector[] selectors)
		{
			var keys = new Slice[selectors.Length];
			for (int i = 0; i < selectors.Length;i++)
			{
				keys[i] = this.Prefix[selectors[i].Key];
			}

			var res = new KeySelector[selectors.Length];
			for (int i = 0; i < selectors.Length; i++)
			{
				res[i] = new KeySelector(
					keys[i],
					selectors[i].OrEqual,
					selectors[i].Offset
				);
			}
			return res;
		}

		private Slice Decode(Slice key)
		{
			return this.Prefix.ExtractKey(key);
		}

		private Slice[] Decode(Slice[] keys)
		{
			var res = new Slice[keys.Length];
			for (int i = 0; i < keys.Length;i++)
			{
				res[i] = this.Prefix.ExtractKey(keys[i]);
			}
			return res;
		}

		public override Task<Slice> GetAsync(Slice key)
		{
			return base.GetAsync(Encode(key));
		}

		public override Task<Slice[]> GetValuesAsync(Slice[] keys)
		{
			return base.GetValuesAsync(Encode(keys));
		}

		public override async Task<Slice> GetKeyAsync(KeySelector selector)
		{
			return Decode(await base.GetKeyAsync(Encode(selector)).ConfigureAwait(false));
		}

		public override async Task<Slice[]> GetKeysAsync(KeySelector[] selectors)
		{
			return Decode(await base.GetKeysAsync(Encode(selectors)).ConfigureAwait(false));
		}

		public override Task<string[]> GetAddressesForKeyAsync(Slice key)
		{
			return base.GetAddressesForKeyAsync(Encode(key));
		}

		public override FdbRangeQuery<TResult> GetRange<TResult>(KeySelector beginInclusive, KeySelector endExclusive, Func<System.Collections.Generic.KeyValuePair<Slice, Slice>, TResult> selector, FdbRangeOptions options = null)
		{
			throw new NotImplementedException();
		}

		public override Task<FdbRangeChunk> GetRangeAsync(KeySelector beginInclusive, KeySelector endExclusive, FdbRangeOptions options = null, int iteration = 0)
		{
			throw new NotImplementedException();
		}


		public override void Set(Slice key, Slice value)
		{
			base.Set(Encode(key), value);
		}

		public override void Clear(Slice key)
		{
			base.Clear(Encode(key));
		}

		public override void ClearRange(Slice beginKeyInclusive, Slice endKeyExclusive)
		{
			base.ClearRange(Encode(beginKeyInclusive), Encode(endKeyExclusive));
		}

		public override void Atomic(Slice key, Slice param, FdbMutationType mutation)
		{
			base.Atomic(Encode(key), param, mutation);
		}

		public override void AddConflictRange(Slice beginKeyInclusive, Slice endKeyExclusive, FdbConflictRangeType type)
		{
			base.AddConflictRange(Encode(beginKeyInclusive), Encode(endKeyExclusive), type);
		}

		public override FdbWatch Watch(Slice key, CancellationToken ct)
		{
			//BUGBUG: the watch returns the key, that would need to be decoded!
			return base.Watch(Encode(key), ct);
		}

	}

}
