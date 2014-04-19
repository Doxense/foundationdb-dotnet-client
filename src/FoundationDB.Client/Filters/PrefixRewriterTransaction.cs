﻿#region BSD Licence
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

namespace FoundationDB.Filters
{
	using FoundationDB.Client;
	using System;
	using System.Threading;
	using System.Threading.Tasks;

	/// <summary>[PROOF OF CONCEPT, DO NOT USE YET!] Transaction filter that automatically appends/remove a fixed prefix to all keys</summary>
	public sealed class PrefixRewriterTransaction : FdbTransactionFilter
	{
		// We will add a prefix to all keys sent to the db, and remove it on the way back

		private readonly FdbSubspace m_prefix;

		public PrefixRewriterTransaction(FdbSubspace prefix, IFdbTransaction trans, bool ownsTransaction)
			: base(trans, false, ownsTransaction)
		{
			if (prefix == null) throw new ArgumentNullException("prefix");
			m_prefix = prefix;
		}

		public FdbSubspace Prefix { get { return m_prefix; } }

		private Slice Encode(Slice key)
		{
			return m_prefix.Concat(key);
		}

		private Slice[] Encode(Slice[] keys)
		{
			return m_prefix.ConcatRange(keys);
		}

		private FdbKeySelector Encode(FdbKeySelector selector)
		{
			return new FdbKeySelector(
				m_prefix.Concat(selector.Key),
				selector.OrEqual,
				selector.Offset
			);
		}

		private FdbKeySelector[] Encode(FdbKeySelector[] selectors)
		{
			var keys = new Slice[selectors.Length];
			for (int i = 0; i < selectors.Length;i++)
			{
				keys[i] = selectors[i].Key;
			}
			keys = m_prefix.ConcatRange(keys);

			var res = new FdbKeySelector[selectors.Length];
			for (int i = 0; i < selectors.Length; i++)
			{
				res[i] = new FdbKeySelector(
					keys[i],
					selectors[i].OrEqual,
					selectors[i].Offset
				);
			}
			return res;
		}

		private Slice Decode(Slice key)
		{
			return m_prefix.Extract(key);
		}

		private Slice[] Decode(Slice[] keys)
		{
			var res = new Slice[keys.Length];
			for (int i = 0; i < keys.Length;i++)
			{
				res[i] = m_prefix.Extract(keys[i]);
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

		public override async Task<Slice> GetKeyAsync(FdbKeySelector selector)
		{
			return Decode(await base.GetKeyAsync(Encode(selector)).ConfigureAwait(false));
		}

		public override async Task<Slice[]> GetKeysAsync(FdbKeySelector[] selectors)
		{
			return Decode(await base.GetKeysAsync(Encode(selectors)).ConfigureAwait(false));
		}

		public override Task<string[]> GetAddressesForKeyAsync(Slice key)
		{
			return base.GetAddressesForKeyAsync(Encode(key));
		}

		public override FdbRangeQuery<System.Collections.Generic.KeyValuePair<Slice, Slice>> GetRange(FdbKeySelector beginInclusive, FdbKeySelector endExclusive, FdbRangeOptions options = null)
		{
			throw new NotImplementedException();
		}

		public override Task<FdbRangeChunk> GetRangeAsync(FdbKeySelector beginInclusive, FdbKeySelector endExclusive, FdbRangeOptions options = null, int iteration = 0)
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

		public override FdbWatch Watch(Slice key, CancellationToken cancellationToken)
		{
			//BUGBUG: the watch returns the key, that would need to be decoded!
			return base.Watch(Encode(key), cancellationToken);
		}

	}

}
