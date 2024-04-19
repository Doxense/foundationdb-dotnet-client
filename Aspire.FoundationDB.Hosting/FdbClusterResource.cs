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

	public class FdbClusterResource : ContainerResource, IFdbResource
	{

		public FdbClusterResource(string name) : base(name) { }

		/// <summary>The minimum API version that must be supported by the cluster</summary>
		public required int ApiVersion { get; set; }

		/// <summary>If specified, the minimum version of the cluster to be deployed.</summary>
		/// <remarks>If <c>null</c>, the best version compatible with <see cref="ApiVersion"/> will be selected</remarks>
		public required Version ClusterVersion { get; set; }

		/// <summary>Strategy used to select the actual runtime version of the deployed cluster.</summary>
		/// <remarks>The strategy works similarily to the <c>rollForward</c> property of the <c>global.json</c> file, see https://learn.microsoft.com/en-us/dotnet/core/tools/global-json.</remarks>
		public required FdbVersionPolicy RollForward { get; set; }

		public required string DockerTag { get; set; }

		public string? NativeLibraryPath { get; set; }

		public bool DisableNativePreloading { get; set; }

		public FdbPath Root { get; set; } = FdbPath.Root;

		public string? ClusterDescription { get; set; } = "docker";

		public string? ClusterId { get; set; } = "docker";

		public ReferenceExpression ConnectionStringExpression => ReferenceExpression.Create($"{GetConnectionString()}");

		public string GetConnectionString()
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
			return builder.ConnectionString;
		}

	}

}
