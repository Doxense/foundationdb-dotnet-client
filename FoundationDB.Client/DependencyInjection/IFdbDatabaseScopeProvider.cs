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

namespace FoundationDB.Client
{
	using System;
	using System.Threading;
	using System.Threading.Tasks;
	using JetBrains.Annotations;

	public interface IFdbDatabaseScopeProvider : IDisposable
	{

		/// <summary>Returns the parent scope (or null if this is the top-level scope)</summary>
		[CanBeNull]
		IFdbDatabaseScopeProvider Parent { get; }

		/// <summary>Return an instance of the database, once it is ready</summary>
		/// <remarks>During the startup of the application, the task returned will wait until the database becomes ready. After that, the task will immediately return the database singleton.</remarks>
		[ItemNotNull]
		ValueTask<IFdbDatabase> GetDatabase(CancellationToken ct);

		/// <summary>Create a scope that will use the database provided by this instance, and which also needs to perform some initialization steps before being ready (ex: using the DirectoryLayer to open subspaces, ...)</summary>
		/// <param name="start">Handler that will be called AFTER the database becomes ready, but BEFORE any consumer of this scope can run.</param>
		/// <param name="lifetime">Optional cancellation token that can be used to externally abort the new scope</param>
		[Pure, NotNull]
		IFdbDatabaseScopeProvider<TState> CreateScope<TState>([NotNull] Func<IFdbDatabase, CancellationToken, Task<(IFdbDatabase Db, TState State)>> start, CancellationToken lifetime = default);

		/// <summary>If <c>true</c>, the database instance is ready. If <c>false</c>, the provider is either not started, or the connection is still pending.</summary>
		bool IsAvailable { get; }

		/// <summary>Cancellation token that is tied to the lifetime of this scope</summary>
		/// <remarks>It will become cancelled if the scope (or one of its parents) is stopped.</remarks>
		CancellationToken Cancellation { get; }

	}

	public interface IFdbDatabaseScopeProvider<TState> : IFdbDatabaseScopeProvider
	{
		/// <summary>Return both the underlying database and the scope's State, once they are ready</summary>
		ValueTask<(IFdbDatabase Database, TState State)> GetDatabaseAndState(CancellationToken ct = default);

		/// <summary>Return the scope's State, once it is ready.</summary>
		ValueTask<TState> GetState(IFdbReadOnlyTransaction tr);
	}

}
