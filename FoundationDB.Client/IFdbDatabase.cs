#region BSD Licence
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

namespace FoundationDB.Client
{
	using JetBrains.Annotations;
	using System;
	using System.Threading;

	/// <summary>Database connection context.</summary>
	[PublicAPI]
	public interface IFdbDatabase : IFdbRetryable, IDynamicKeySubspace, IDisposable
	{
		/// <summary>Name of the database</summary>
		[NotNull]
		string Name { get; }

		/// <summary>Cluster of the database</summary>
		[NotNull]
		IFdbCluster Cluster { get; }

		/// <summary>Returns a cancellation token that is linked with the lifetime of this database instance</summary>
		/// <remarks>The token will be cancelled if the database instance is disposed</remarks>
		CancellationToken Cancellation { get; }

		/// <summary>Returns the global namespace used by this database instance</summary>
		/// <remarks>Makes a copy of the subspace tuple, so you should not call this property a lot. Use any of the Partition(..) methods to create a subspace of the database</remarks>
		[NotNull]
		IDynamicKeySubspace GlobalSpace { get; }

		/// <summary>Directory partition of this database instance</summary>
		[NotNull]
		FdbDatabasePartition Directory { get; }

		/// <summary>If true, this database instance will only allow starting read-only transactions.</summary>
		bool IsReadOnly { get; }

		/// <summary>Set a parameter-less option on this database</summary>
		/// <param name="option">Option to set</param>
		void SetOption(FdbDatabaseOption option);

		/// <summary>Set an option on this database that takes a string value</summary>
		/// <param name="option">Option to set</param>
		/// <param name="value">Value of the parameter (can be null)</param>
		void SetOption(FdbDatabaseOption option, string value);

		/// <summary>Set an option on this database that takes an integer value</summary>
		/// <param name="option">Option to set</param>
		/// <param name="value">Value of the parameter</param>
		void SetOption(FdbDatabaseOption option, long value);

		/// <summary>Default Timeout value (in milliseconds) for all transactions created from this database instance.</summary>
		/// <remarks>Only effective for future transactions</remarks>
		int DefaultTimeout { get; set; }

		/// <summary>Default Retry Limit value for all transactions created from this database instance.</summary>
		/// <remarks>Only effective for future transactions</remarks>
		int DefaultRetryLimit { get; set; }

		int DefaultMaxRetryDelay { get; set; }

		/// <summary>Start a new transaction on this database, with the specified mode</summary>
		/// <param name="mode">Mode of the transaction (read-only, read-write, ....)</param>
		/// <param name="ct">Optional cancellation token that can abort all pending async operations started by this transaction.</param>
		/// <param name="context">Existing parent context, if the transaction needs to be linked with a retry loop, or a parent transaction. If null, will create a new standalone context valid only for this transaction</param>
		/// <returns>New transaction instance that can read from or write to the database.</returns>
		/// <remarks>You MUST call Dispose() on the transaction when you are done with it. You SHOULD wrap it in a 'using' statement to ensure that it is disposed in all cases.</remarks>
		/// <example>
		/// using(var tr = db.BeginTransaction(CancellationToken.None))
		/// {
		///		tr.Set(Slice.FromString("Hello"), Slice.FromString("World"));
		///		tr.Clear(Slice.FromString("OldValue"));
		///		await tr.CommitAsync();
		/// }</example>
		[NotNull]
		IFdbTransaction BeginTransaction(FdbTransactionMode mode, CancellationToken ct, FdbOperationContext context = null);

	}

}
