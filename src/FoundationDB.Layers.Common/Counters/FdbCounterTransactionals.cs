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

namespace FoundationDB.Layers.Counters
{
	using System;
	using System.Threading;
	using System.Threading.Tasks;

	[Obsolete("This is obsoleted by atomic operations")]
	public static class FdbCounterTransactionals
	{

		/// <summary>
		/// Get the value of the counter.
		/// Not recommended for use with read/write transactions when the counter is being frequently updated (conflicts will be very likely).
		/// </summary>
		public static Task<long> GetTransactionalAsync(this FdbCounter self, CancellationToken cancellationToken)
		{
			return self.Database.ReadAsync((tr) => self.GetTransactional(tr), cancellationToken);
		}

		/// <summary>
		/// Get the value of the counter with snapshot isolation (no transaction conflicts).
		/// </summary>
		public static Task<long> GetSnapshotAsync(this FdbCounter self, CancellationToken cancellationToken)
		{
			return self.Database.ReadAsync((tr) => self.GetSnapshot(tr), cancellationToken);
		}

		/// <summary>
		/// Add the value x to the counter.
		/// </summary>
		public static Task AddAsync(this FdbCounter self, long x, CancellationToken cancellationToken)
		{
			return self.Database.WriteAsync((tr) => self.Add(tr, x), cancellationToken);
		}

		/// <summary>
		/// Set the counter to value x.
		/// </summary>
		public static Task SetTotalAsync(this FdbCounter self, long x, CancellationToken cancellationToken)
		{
			return self.Database.ReadWriteAsync((tr) => self.SetTotal(tr, x), cancellationToken);
		}

	}

}
