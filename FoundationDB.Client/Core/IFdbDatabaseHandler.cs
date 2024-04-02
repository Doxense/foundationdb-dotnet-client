﻿#region Copyright (c) 2023-2024 SnowBank SAS, (c) 2005-2023 Doxense SAS
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

namespace FoundationDB.Client.Core
{
	using JetBrains.Annotations;
	using System;
	using System.Threading;
	using System.Threading.Tasks;

	/// <summary>Basic API for FoundationDB databases</summary>
	[PublicAPI]
	public interface IFdbDatabaseHandler : IDisposable
	{

		string? ClusterFile { get; }

		bool IsInvalid { get; }

		bool IsClosed { get; }

		void SetOption(FdbDatabaseOption option, ReadOnlySpan<byte> data);

		IFdbTransactionHandler CreateTransaction(FdbOperationContext context);

		IFdbTenantHandler OpenTenant(FdbTenantName name);

		Task RebootWorkerAsync(ReadOnlySpan<char> name, bool check, int duration, CancellationToken ct);

		Task ForceRecoveryWithDataLossAsync(ReadOnlySpan<char> dcId, CancellationToken ct);

		Task CreateSnapshotAsync(ReadOnlySpan<char> uid, ReadOnlySpan<char> snapCommand, CancellationToken ct);

		/// <summary>Returns the currently selected API version for this native handler.</summary>
		int GetApiVersion();

		/// <summary>Returns the maximum API version that is supported by this native handler.</summary>
		int GetMaxApiVersion();

		/// <summary>Returns a value where 0 indicates that the client is idle and 1 (or larger) indicates that the client is saturated.</summary>
		double GetMainThreadBusyness();

	}

}
