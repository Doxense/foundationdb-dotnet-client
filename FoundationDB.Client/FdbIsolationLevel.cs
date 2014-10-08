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

namespace FoundationDB.Client
{
	using System;

	/// <summary>Specifies the isolation level of FoundationDB transactions</summary>
	public enum FdbIsolationLevel
	{
		//note: we use the same values as System.Data.IsolationLevel so we can cast to and from, without requiring a reference to System.Data.dll

		/// <summary>The database will use optimistic locking to ensure that all committed transactions behaved as if they were processed sequentialy by the database, even though they may have executed concurrently. If the transaction reads a key that was written to by a recently commited transaction, it will conflict at commit time. This is the default isolation level.</summary>
		Serializable = 0x100000,

		/// <summary>This transaction reads from a snapshot of the database taken at the time of the first read operation. The transaction will not see any write from any other transactions, including itself.</summary>
		Snapshot = 0x1000000,

		/// <summary>This transaction may see new writes that have been successfully committed to the database, resulting in non-repeatable reads or phantom data.</summary>
		//note: this would be used by custom transactions that reset themselves everything 5 seconds (or after every past_version error)
		ReadCommitted = 0x1000,

		/// <summary>A dirty read is possible, meaning that no shared locks are issued and no exclusive locks are honored.</summary>
		//note: this would be used by fake in-memory database handlers that would use a regular SortedDictionary<Slice, Slice> without much locking
		ReadUncommitted = 0x100,

		/// <summary>A different isolation level than the one specified is being used, but the level cannot be determined.</summary>
		Unspecified = -1
	}

}
