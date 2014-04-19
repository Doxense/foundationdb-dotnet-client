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

namespace FoundationDB.Filters.Logging
{
	using FoundationDB.Client;
	using System;
	using System.Threading;

	/// <summary>Database filter that logs all the transactions</summary>
	public sealed class FdbLoggedDatabase : FdbDatabaseFilter
	{

		/// <summary>Handler called everytime a transaction is successfully committed</summary>
		public Action<FdbLoggedTransaction> OnCommitted { get; private set; }

		public FdbLoggedDatabase(IFdbDatabase database, bool forceReadOnly, bool ownsDatabase, Action<FdbLoggedTransaction> onCommitted)
			: base(database, forceReadOnly, ownsDatabase)
		{
			this.OnCommitted = onCommitted;
		}

		public override IFdbTransaction BeginTransaction(FdbTransactionMode mode, CancellationToken cancellationToken = default(CancellationToken), FdbOperationContext context = null)
		{
			return new FdbLoggedTransaction(
				base.BeginTransaction(mode, cancellationToken, context),
				true,
				this.OnCommitted
			);
		}
	}

}
