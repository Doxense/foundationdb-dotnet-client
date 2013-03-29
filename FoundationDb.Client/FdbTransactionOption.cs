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
	* Neither the name of the <organization> nor the
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

using System;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace FoundationDb.Client
{

	public enum FdbTransactionOption
	{
		None = 0,

		/// <summary>The transaction, if not self-conflicting, may be committed a second time after commit succeeds, in the event of a fault
		/// Parameter: Option takes no parameter
		/// </summary>
		CausalWriteRisky = 10,

		/// <summary>The read version will be committed, and usually will be the latest committed, but might not be the latest committed in the event of a fault or partition
		// Parameter: Option takes no parameter
		/// </summary>
		CausalReadRisky = 20,

		/// <summary>
		/// Parameter: Option takes no parameter
		CausalReadDisable = 21,
		/// </summary>

		/// <summary>
		/// Parameter: Option takes no parameter
		/// </summary>
		CheckWritesEnable = 50,

		/// <summary>Reads performed by a transaction will not see any prior mutations that occured in that transaction, instead seeing the value which was in the database at the transaction's read version. This option may provide a small performance benefit for the client, but also disables a number of client-side optimizations which are beneficial for transactions which tend to read and write the same keys within a single transaction. Also note that with this option invoked any outstanding reads will return errors when transaction commit is called (rather than the normal behavior of commit waiting for outstanding reads to complete).
		/// Parameter: Option takes no parameter
		/// </summary>
		ReadYourWrites = 51,

		/// <summary>Disables read-ahead caching for range reads. Under normal operation, a transaction will read extra rows from the database into cache if range reads are used to page through a series of data one row at a time (i.e. if a range read with a one row limit is followed by another one row range read starting immediately after the result of the first).
		/// Parameter: Option takes no parameter
		/// </summary>
		ReadAheadDisable = 52,

		/// <summary>
		/// Parameter: Option takes no parameter
		/// </summary>
		DurabilityDataCenter = 110,

		/// <summary>
		/// Parameter: Option takes no parameter
		/// </summary>
		DurabilityRisky = 120,

		/// <summary>
		/// Parameter: Option takes no parameter
		/// </summary>
		DevNullIsWebScale = 130,

		/// <summary>Specifies that this transaction should be treated as highest priority and that lower priority transactions should block behind this one. Use is discouraged outside of low-level tools
		/// Parameter: Option takes no parameter
		/// </summary>
		PrioritySystemImmediate = 200,

		/// <summary>Specifies that this transaction should be treated as low priority and that default priority transactions should be processed first. Useful for doing batch work simultaneously with latency-sensitive work
		/// Parameter: Option takes no parameter
		/// </summary>
		PriorityBatch = 201,

		/// <summary>This is a write-only transaction which sets the initial configuration
		/// Parameter: Option takes no parameter
		/// </summary>
		InitializeNewDatabase = 300,

		/// <summary>Allows this transaction to read and modify system keys (those that start with the byte 0xFF)
		/// Parameter: Option takes no parameter
		/// </summary>
		AccessSystemKeys = 301,

		/// <summary>
		/// Parameter: Option takes no parameter
		/// </summary>
		DebugDump = 400
	}

}
