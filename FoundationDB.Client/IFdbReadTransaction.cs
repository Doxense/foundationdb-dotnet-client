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

namespace FoundationDB.Client
{
	using System;
	using System.Threading;
	using System.Threading.Tasks;

	/// <summary>Transaction that allows read operations</summary>
	public interface IFdbReadTransaction
	{
		int Id { get; }

		bool IsSnapshot { get; }

		void EnsureCanRead(CancellationToken ct = default(CancellationToken));

		Task<long> GetReadVersionAsync(CancellationToken ct = default(CancellationToken));

		Task<Slice> GetAsync(Slice keyBytes, CancellationToken ct = default(CancellationToken));

		Task<Slice[]> GetBatchValuesAsync(Slice[] keys, CancellationToken ct = default(CancellationToken));

		Task<Slice> GetKeyAsync(FdbKeySelector selector, CancellationToken ct = default(CancellationToken));

		Task<FdbRangeChunk> GetRangeAsync(FdbKeySelectorPair range, FdbRangeOptions options = null, int iteration = 0, CancellationToken ct = default(CancellationToken));

		FdbRangeQuery GetRange(FdbKeySelectorPair range, FdbRangeOptions options = null);

		FdbRangeQuery GetRangeStartsWith(Slice prefix, FdbRangeOptions options = null);

		Task OnErrorAsync(FdbError code, CancellationToken ct = default(CancellationToken));
	}

}
