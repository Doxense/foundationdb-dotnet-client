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

namespace FoundationDB.Client
{
	using System;
	using System.Diagnostics;
	using Doxense.Collections.Tuples;
	using Doxense.Serialization.Encoders;

	[DebuggerDisplay("Name={Name}, Id={Id}, Prefix={Prefix}")]
	public sealed record FdbTenantMetadata
	{

		/// <summary>Name of the tenant</summary>
		public FdbTenantName Name { get; init; }

		/// <summary>Unique internal identifier of this tenant</summary>
		public long Id { get; init; }

		/// <summary>Key prefix (in the global keyspace) of this tenant</summary>
		/// <remarks>
		/// <para>Any transaction operation on this tenant will add/remove this prefix to keys passed as parameters or returned from range or key resolution queries.</para>
		/// <para>For example, if the tenant as prefix <c>`some_prefix`</c>, and a transaction on this tenant reads the key <c>`HELLO`</c>, it will actually read the global key <c>`some_prefixHELLO`</c>.</para>
		/// <para>Another tenant with prefix <c>`other_prefix`</c> reading the same key, will read the global key <c>`other_prefixHELLO`</c> which is not shared.</para>
		/// </remarks>
		public Slice Prefix { get; init; }

		/// <summary>Range that encompass all the keys of this tenant (from the global keyspace)</summary>
		/// <remarks>Should only be used for admin or maintenance operation, from the global keyspace!</remarks>
		public KeyRange Range { get; init; }

		/// <summary>Raw metadata value, as stored in the database</summary>
		public Slice Raw { get; init; }

		public FdbTenantSubspace GetSubspace(IDynamicKeyEncoder? encoder = null, ISubspaceContext? context = null)
		{
			return new FdbTenantSubspace(this, encoder ?? TuPack.Encoding.GetDynamicKeyEncoder(), context ?? SubspaceContext.Default);
		}

	}

}
