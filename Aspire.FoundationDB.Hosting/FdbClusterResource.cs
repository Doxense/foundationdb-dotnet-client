#region Copyright (c) 2023-2023 SnowBank SAS
//
// All rights are reserved. Reproduction or transmission in whole or in part, in
// any form or by any means, electronic, mechanical or otherwise, is prohibited
// without the prior written consent of the copyright owner.
//
#endregion

namespace Aspire.Hosting.ApplicationModel
{
	using System;
	using System.Collections.Generic;
	using System.Data.Common;
	using System.Globalization;
	using System.Linq;
	using System.Net;
	using Doxense.Diagnostics.Contracts;
	using FoundationDB.Client;

	public class FdbClusterResource : Resource, IFdbResource
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

		public int PortStart { get; set; } = 4550;

		internal List<FdbContainerResource> Containers { get; set; } = new();
		//REVIEW: can we get rid of this and use the app builder to find resources of type FdbContainerResource when we need them?

		internal int? LastPort { get; set; }

		public int GetNextPort()
		{
			int port = this.LastPort == null ? this.PortStart : this.LastPort.Value + 1;
			while (true)
			{
				bool conflict = false;
				foreach (var c in this.Containers)
				{
					if (c.Port == port)
					{
						conflict = true;
						break;
					}
				}

				if (!conflict)
				{
					this.LastPort = port;
					return port;
				}

				++port;
			}
		}

		internal FdbContainerResource CreateContainer(string name, bool coordinator)
		{
			Contract.Debug.Requires(name != null);

			var fdbContainer = new FdbContainerResource(name, this)
			{
				DockerTag = this.DockerTag,
				Port = GetNextPort(),
				IsCoordinator = coordinator,
			};

			this.Containers.Add(fdbContainer);

			return fdbContainer;
		}

		internal FdbContainerResource? GetCoordinator()
		{
			return this.Containers.FirstOrDefault();
		}

		public string GetConnectionString()
		{
			var coordinator = GetCoordinator();
			if (coordinator == null)
			{
				throw new DistributedApplicationException("There must be at least one fdb container available on the cluster!");
			}

			string clusterDesc = this.ClusterDescription ?? this.Name;
			string clusterId = this.ClusterId ?? this.Name;
			string coordinatorHost;
			int coordinatorPort;

			var ep = coordinator.GetEndpoint();
			switch (ep)
			{
				case IPEndPoint ip:
				{
					coordinatorHost = ip.Address.ToString();
					coordinatorPort = ip.Port;
					break;
				}
				case DnsEndPoint dns:
				{
					coordinatorHost = dns.Host.ToString();
					coordinatorPort = dns.Port;
					break;
				}
				default:
				{
					throw new InvalidOperationException("Coordinator endpoint type not supported");
				}
			}

			// Cluster File format: "<DESC>:<ID>@<HOST1>:<PORT1>[,<HOST2>:<PORT2>,...]"
			// By default, the docker image uses "docker:docker@127.0.0.1:4550"

			string contents = $"{clusterDesc}:{clusterId}@{coordinatorHost}:{coordinatorPort.ToString(CultureInfo.InvariantCulture)}";

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
