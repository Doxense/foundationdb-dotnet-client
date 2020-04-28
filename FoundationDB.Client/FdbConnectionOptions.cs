#region BSD License
/* Copyright (c) 2013-2020, Doxense SAS
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
	using System.Diagnostics;
	using System.Globalization;
	using System.Text;
	using JetBrains.Annotations;

	/// <summary>Settings used when establishing the connection with a FoundationDB cluster</summary>
	[DebuggerDisplay("{" + nameof(ToString) + "(),nq}")]
	public sealed class FdbConnectionOptions
	{
		//REVIEW: rename this to "FdbConnectionString"? (so that it feels more like ADO.NET?)
		
		[Obsolete("This value should not be used anymore.")]
		public const string DefaultDbName = "DB";

		/// <summary>Full path to a specific 'fdb.cluster' file</summary>
		public string? ClusterFile { get; set; }

		/// <summary>Default database name</summary>
		/// <remarks>Only "DB" is supported for now</remarks>
		[Obsolete("This property should not be used anymore, and its value will be ignored.")]
		public string DbName { get; set; } = DefaultDbName;

		/// <summary>If true, opens a read-only view of the database</summary>
		/// <remarks>If set to true, only read-only transactions will be allowed on the database instance</remarks>
		public bool ReadOnly { get; set; }

		/// <summary>Default timeout for all transactions, in milliseconds precision (or infinite if 0)</summary>
		public TimeSpan DefaultTimeout { get; set; } // sec

		/// <summary>Default maximum number of retries for all transactions (or infinite if 0)</summary>
		public int DefaultRetryLimit { get; set; }

		/// <summary>Default maximum retry delay for all transactions (or infinite if 0)</summary>
		public int DefaultMaxRetryDelay { get; set; }

		/// <summary>Default root location used by the database (empty prefix by default)</summary>
		/// <remarks>If specified, all started transactions will be automatically rooted to this location.</remarks>
		public FdbPath? Root { get; set; }

		/// <summary>If set, specify the datacenter ID that was passed to fdbserver processes running in the same datacenter as this client, for better location-aware load balancing.</summary>
		public string? DataCenterId { get; set; }

		/// <summary>If set, specify the machine ID that was passed to fdbserver processes running on the same machine as this client, for better location-aware load balancing.</summary>
		public string? MachineId { get; set; }


		public override string ToString()
		{
			var sb = new StringBuilder();
			AddKeyValue(sb, "cluster_file", this.ClusterFile ?? "default");
			if (this.Root != null) AddKeyValue(sb, "root", this.Root.ToString());
			//REVIEW: cannot serialize subspace into a string ! :(
			if (this.ReadOnly) AddKeyword(sb, "readonly");
			if (this.DefaultTimeout > TimeSpan.Zero) AddKeyValue(sb, "timeout", this.DefaultTimeout.TotalSeconds);
			if (this.DefaultRetryLimit > 0) AddKeyValue(sb, "retry_limit", this.DefaultRetryLimit);
			if (this.DefaultMaxRetryDelay > 0) AddKeyValue(sb, "retry_delay", this.DefaultMaxRetryDelay);
			AddKeyValue(sb, "dc_id", this.DataCenterId);
			AddKeyValue(sb, "machine_id", this.MachineId);
			return sb.ToString();
		}

		private static void AddKeyValue(StringBuilder sb, string key, string? value)
		{
			if (value == null) return;
			if (value.IndexOf(' ') >= 0 || value.IndexOf(';') >= 0)
			{ // encode '"' into '\"'
				value = "\"" + value.Replace("\"", "\\\"") + "\"";
			}

			if (sb.Length > 0) sb.Append("; ");
			sb.Append(key).Append('=').Append(value);
		}

		private static void AddKeyValue(StringBuilder sb, string key, long value)
		{
			if (sb.Length > 0) sb.Append("; ");
			sb.Append(key).Append('=').Append(value.ToString(CultureInfo.InvariantCulture));
		}

		private static void AddKeyValue(StringBuilder sb, string key, double value)
		{
			if (sb.Length > 0) sb.Append("; ");
			sb.Append(key).Append('=').Append(value.ToString("R", CultureInfo.InvariantCulture));
		}

		private static void AddKeyword(StringBuilder sb, string key)
		{
			if (sb.Length > 0) sb.Append("; ");
			sb.Append(key);
		}

	}

}
