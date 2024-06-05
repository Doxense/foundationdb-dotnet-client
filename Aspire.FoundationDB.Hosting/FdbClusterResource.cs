#region Copyright (c) 2023-2024 SnowBank SAS
//
// All rights are reserved. Reproduction or transmission in whole or in part, in
// any form or by any means, electronic, mechanical or otherwise, is prohibited
// without the prior written consent of the copyright owner.
//
#endregion

namespace Aspire.Hosting.ApplicationModel
{
	using System;
	using System.Data.Common;
	using System.Globalization;
	using FoundationDB.Client;

	/// <summary>A resource that represents a FoundationDB cluster</summary>
	public class FdbClusterResource : ContainerResource, IFdbResource
	{

		/// <summary>A resource that represents a FoundationDB cluster</summary>
		/// <param name="name">The name of the resource.</param>
		/// <param name="entrypoint">An optional container entrypoint.</param>
		public FdbClusterResource(string name, string? entrypoint = null) : base(name, entrypoint) { }

		/// <summary>The minimum API version that must be supported by the cluster</summary>
		public required int ApiVersion { get; set; }

		/// <summary>If specified, the minimum version of the cluster to be deployed.</summary>
		/// <remarks>If <c>null</c>, the best version compatible with <see cref="ApiVersion"/> will be selected</remarks>
		public required Version ClusterVersion { get; set; }

		/// <summary>Strategy used to select the actual runtime version of the deployed cluster.</summary>
		/// <remarks>The strategy works similarily to the <c>rollForward</c> property of the <c>global.json</c> file, see https://learn.microsoft.com/en-us/dotnet/core/tools/global-json.</remarks>
		public required FdbVersionPolicy RollForward { get; set; }

		/// <summary>Tag of the docker image that will be used to run the cluster locally. (ex: "latest", "7.3.36", ...)</summary>
		public required string DockerTag { get; set; }

		/// <summary>Path to the local native client library ('fdb_c.dll' or 'libfdb_c.so')</summary>
		public string? NativeLibraryPath { get; set; }

		/// <summary>Specifies if native pre-loading should be enabled or disabled</summary>
		public bool DisableNativePreloading { get; set; }

		/// <summary>Default transaction timeout</summary>
		/// <remarks>See <see cref="IFdbTransactionOptions.Timeout"/></remarks>
		public TimeSpan? DefaultTimeout { get; set; }

		/// <summary>Default transaction retry limit</summary>
		/// <remarks>See <see cref="IFdbTransactionOptions.RetryLimit"/></remarks>
		public int? DefaultRetryLimit { get; set; }

		/// <summary>Specifies if the FoundationDB cluster is mounted in read-only mode by default.</summary>
		public bool? ReadOnly { get; set; }

		/// <summary>Specifies the default root path of the partition used by all processes.</summary>
		public FdbPath Root { get; set; } = FdbPath.Root;

		/// <summary>Specifies the 'description' part of the locally generated 'fdb.cluster' file.</summary>
		public string? ClusterDescription { get; set; } = "docker";

		/// <summary>Specified the 'id' part of the locally generated 'fdb.cluster' file.</summary>
		public string? ClusterId { get; set; } = "docker";

		/// <inheritdoc />
		public ReferenceExpression ConnectionStringExpression => ReferenceExpression.Create($"{GetConnectionString()}");

		private string GetConnectionString()
		{
			string clusterDesc = this.ClusterDescription ?? this.Name;
			string clusterId = this.ClusterId ?? this.Name;

			var ep = this.GetEndpoint("tcp");

			var coordinatorHost = ep.Host;
			if (coordinatorHost == "localhost") coordinatorHost = "127.0.0.1";
			var coordinatorPort = ep.Port;

			// Cluster File format: "<DESC>:<ID>@<HOST1>:<PORT1>[,<HOST2>:<PORT2>,...]"
			// By default, the docker image uses "docker:docker@127.0.0.1:4550"

			string contents = $"{clusterDesc}:{clusterId}@{coordinatorHost}:{coordinatorPort.ToString(CultureInfo.InvariantCulture)}";

			//TODO: replace this with a proper use of ReferenceExpression?

			var builder = new DbConnectionStringBuilder
			{
				["ApiVersion"] = this.ApiVersion,
				["Root"] = this.Root.ToString(),
				["ClusterFileContents"] = contents,
				["ClusterVersion"] = this.ClusterVersion.ToString(),
				//TODO: more options? Debug? TraceId? Timeout? ...
			};
			if (this.DisableNativePreloading)
			{
				builder["DisableNativePreloading"] = true;
			}
			else if (!string.IsNullOrEmpty(this.NativeLibraryPath))
			{
				builder["NativeLibrary"] = this.NativeLibraryPath;
			}

			if (this.DefaultTimeout.HasValue)
			{
				if (this.DefaultTimeout.Value < TimeSpan.Zero) throw new InvalidOperationException("Default timeout must be a positive value");
				builder["DefaultTimeout"] = this.DefaultTimeout.Value.TotalSeconds.ToString("R", CultureInfo.InvariantCulture);
			}

			if (this.DefaultRetryLimit.HasValue)
			{
				if (this.DefaultRetryLimit.Value < 0) throw new InvalidOperationException("Default retry limit must be a positive value");
				builder["DefaultRetryLimit"] = this.DefaultRetryLimit.Value.ToString(CultureInfo.InvariantCulture);
			}

			if (this.ReadOnly == true)
			{
				builder["ReadOnly"] = true;
			}

			return builder.ConnectionString;
		}

	}

}
