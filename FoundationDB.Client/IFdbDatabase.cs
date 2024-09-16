#region Copyright (c) 2023-2024 SnowBank SAS, (c) 2005-2023 Doxense SAS
// All rights reserved.
// 
// Redistribution and use in source and binary forms, with or without
// modification, are permitted provided that the following conditions are met:
// 	* Redistributions of source code must retain the above copyright
// 	  notice, this list of conditions and the following disclaimer.
// 	* Redistributions in binary form must reproduce the above copyright
// 	  notice, this list of conditions and the following disclaimer in the
// 	  documentation and/or other materials provided with the distribution.
// 	* Neither the name of SnowBank nor the
// 	  names of its contributors may be used to endorse or promote products
// 	  derived from this software without specific prior written permission.
// 
// THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND
// ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
// WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
// DISCLAIMED. IN NO EVENT SHALL SNOWBANK SAS BE LIABLE FOR ANY
// DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
// (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
// LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
// ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
// (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
// SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
#endregion

namespace FoundationDB.Client
{
	using JetBrains.Annotations;
	using FoundationDB.Filters.Logging;

	/// <summary>Database connection context.</summary>
	[PublicAPI]
	public interface IFdbDatabase : IFdbRetryable, IDisposable
	{
		/// <summary>Name of the database</summary>
		[Obsolete("This property is not supported anymore and will always return \"DB\".")]
		string Name { get; }

		/// <summary>Path to the cluster file used to connect to the database</summary>
		/// <remarks>If null, the default path for this platform will be used</remarks>
		string? ClusterFile { get; }

		/// <summary>Returns a cancellation token that is linked with the lifetime of this database instance</summary>
		/// <remarks>The token will be cancelled if the database instance is disposed</remarks>
		CancellationToken Cancellation { get; }

		/// <summary>Returns the root path used by this database instance</summary>
		FdbDirectorySubspaceLocation Root { get; }

		/// <summary>Directory Layer used by this database instance</summary>
		FdbDirectoryLayer DirectoryLayer { get; }

		/// <summary>If true, this database instance will only allow starting read-only transactions.</summary>
		bool IsReadOnly { get; }

		/// <summary>Helper that can set options for this database</summary>
		IFdbDatabaseOptions Options { get; }

		/// <summary>Sets the default log handler for this database</summary>
		/// <param name="handler">Default handler that is attached to any new transction, and will be invoked when they complete.</param>
		/// <param name="options"></param>
		/// <remarks>This handler may not be called if logging is disabled, if a transaction overrides its handler, or if it calls <see cref="IFdbReadOnlyTransaction.StopLogging"/></remarks>
		void SetDefaultLogHandler(Action<FdbTransactionLog> handler, FdbLoggingOptions options = default);

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
		[Obsolete("Use BeginTransaction() instead")]
		ValueTask<IFdbTransaction> BeginTransactionAsync(FdbTransactionMode mode, CancellationToken ct, FdbOperationContext? context = null);

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
		IFdbTransaction BeginTransaction(FdbTransactionMode mode, CancellationToken ct, FdbOperationContext? context = null);

		IFdbTenant GetTenant(FdbTenantName name);

		/// <summary>Return the currently enforced API version for this database instance.</summary>
		int GetApiVersion();

		/// <summary>Returns a value between 0 and 1 that reflect the saturation of the client main thread.</summary>
		/// <returns>Value between 0 (no activity) and 1 (completly saturated)</returns>
		/// <remarks>The value is updated in the background at regular interval (by default every second).</remarks>
		double GetMainThreadBusyness();

		Task RebootWorkerAsync(string name, bool check, int duration, CancellationToken ct);

		Task ForceRecoveryWithDataLossAsync(string dcId, CancellationToken ct);

		Task CreateSnapshotAsync(string uid, string snapCommand, CancellationToken ct);

		/// <summary>Return the protocol version reported by the coordinator the client is connected to.</summary>
		Task<ulong> GetServerProtocolAsync(CancellationToken ct);

		Task<Slice> GetClientStatus(CancellationToken ct);

	}

}
