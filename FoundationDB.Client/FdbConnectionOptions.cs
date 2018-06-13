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
	using System;
	using JetBrains.Annotations;

	/// <summary>Settings used when establishing the connection with a FoundationDB cluster</summary>
	public sealed class FdbConnectionOptions
	{

		public const string DefaultDbName = "DB";

		/// <summary>Full path to a specific 'fdb.cluster' file</summary>
		[CanBeNull]
		public string ClusterFile { get; set; }

		/// <summary>Default database name</summary>
		/// <remarks>Only "DB" is supported for now</remarks>
		public string DbName { get; set; } = FdbConnectionOptions.DefaultDbName;

		/// <summary>If true, opens a read-only view of the database</summary>
		/// <remarks>If set to true, only read-only transactions will be allowed on the database instance</remarks>
		public bool ReadOnly { get; set; }

		/// <summary>Default timeout for all transactions, in milliseconds precision (or infinite if 0)</summary>
		public TimeSpan DefaultTimeout { get; set; } // sec

		/// <summary>Default maximum number of retries for all transactions (or infinite if 0)</summary>
		public int DefaultRetryLimit { get; set; }

		public int DefaultMaxRetryDelay { get; set; }

		/// <summary>Global subspace in use by the database (empty prefix by default)</summary>
		/// <remarks>If <see cref="PartitionPath"/> is also set, this subspace will be used to locate the top-level Directory Layer, and the actual GlobalSpace of the database will be the partition</remarks>
		[CanBeNull]
		public IKeySubspace GlobalSpace { get; set; }

		/// <summary>If specified, open the named partition at the specified path</summary>
		/// <remarks>If <see cref="GlobalSpace"/> is also set, it will be used to locate the top-level Directory Layer.</remarks>
		[CanBeNull, ItemNotNull]
		public string[] PartitionPath { get; set; }

	}
}
