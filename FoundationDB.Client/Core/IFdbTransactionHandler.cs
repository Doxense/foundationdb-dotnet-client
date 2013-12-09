#region BSD Licence
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

namespace FoundationDB.Client.Core
{
	using System;
	using System.Threading;
	using System.Threading.Tasks;

	/// <summary>Basic API for FoundationDB transactions</summary>
	public interface IFdbTransactionHandler : IDisposable
	{
		int Size { get; }
		bool IsClosed { get; }
		bool IsInvalid { get; }

		void SetOption(FdbTransactionOption option, Slice data);

		Task<long> GetReadVersionAsync(CancellationToken cancellationToken);
		long GetCommittedVersion();
		void SetReadVersion(long version);

		Task<Slice> GetAsync(Slice key, bool snapshot, CancellationToken cancellationToken);
		Task<Slice[]> GetValuesAsync(Slice[] keys, bool snapshot, CancellationToken cancellationToken);
		Task<FdbRangeChunk> GetRangeAsync(FdbKeySelector begin, FdbKeySelector end, FdbRangeOptions options, int iteration, bool snapshot, CancellationToken cancellationToken);
		Task<Slice> GetKeyAsync(FdbKeySelector selector, bool snapshot, CancellationToken cancellationToken);
		Task<Slice[]> GetKeysAsync(FdbKeySelector[] selectors, bool snapshot, CancellationToken cancellationToken);

		void Set(Slice key, Slice value);
		void Atomic(Slice key, Slice param, FdbMutationType type);
		void Clear(Slice key);
		void ClearRange(Slice beginKeyInclusive, Slice endKeyExclusive);
		void AddConflictRange(Slice beginKeyInclusive, Slice endKeyExclusive, FdbConflictRangeType type);

		Task<string[]> GetAddressesForKeyAsync(Slice key, CancellationToken cancellationToken);
		FdbWatch Watch(Slice key, CancellationToken cancellationToken);

		Task CommitAsync(CancellationToken cancellationToken);
		Task OnErrorAsync(FdbError code, CancellationToken cancellationToken);
		void Reset();
		void Cancel();
	}

}
