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
	using FoundationDB.Client;

	public enum FdbVersionPolicy
    {
        /// <summary>Use the exact version specified</summary>
        Exact = 0,

        /// <summary>Select the latest version published to the docker registry.</summary>
        /// <remarks>Please note that there is no guarantee that the latest version is stable or is compatible with the selected API level.</remarks>
        Latest,

        /// <summary>Select the latest compatible version published to the docker registry, that is greater than or equal to the version requested.</summary>
        /// <remarks>
        /// <para>For example, if version <c>6.0.3</c> is requested, but <c>7.3.5</c> is currently available, it will be used instead.</para>
        /// <para>If a newer version is available, but is known to break compatiblity (by removing support for the API level selected), then it will not be included in the selection process.</para>
        /// </remarks>
        LatestMajor,

        /// <summary>Select the latest compatible minor version published to the docker registry, that is greater than or equal to the version requested.</summary>
        /// <remarks>
        /// <para>For example, if version <c>6.0.3</c> is requested, but <c>6.2.7</c> is the latest <c>6.x</c> version available, it will be used even if there is a more recent <c>7.x</c> version.</para>
        /// <para>If a newer version is available, but is known to break compatiblity (by removing support for the API level selected), then it will not be included in the selection process.</para>
        /// </remarks>
        LatestMinor,
        
        /// <summary>Select the latest stable patch version available for the minor version requested.</summary>
        /// <remarks>
        /// <para>For example, if version <c>6.0.3</c> is requested, but <c>6.0.7</c> is the latest <c>6.0.x</c> version available, it will be used even if there is a more recent <c>6.1.x</c> or <c>7.x</c> version.</para>
        /// <para>If a newer version is available, but is known to break compatiblity (by removing support for the API level selected), then it will not be included in the selection process.</para>
        /// </remarks>
        LatestPatch,

    }

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

        public List<FdbContainerResource> Containers { get; set; } = new();

        public int PortStart { get; set; } = 4550;

        public FdbPath Root { get; set; } = FdbPath.Root;

        public string? ClusterDescription { get; set; } = "docker";

        public string? ClusterId { get; set; } = "docker";

        internal int? LastPort { get; set; }

        internal string DockerTag { get; set; }

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

            var ep = coordinator.GetAllocatedEndpoint();

            string clusterFileContents = (this.ClusterDescription ?? this.Name) + ":" + (this.ClusterId ?? this.Name) + "@" + ep.Address.ToString() + ":" + ep.Port.ToString(CultureInfo.InvariantCulture);

            var builder = new DbConnectionStringBuilder();
            builder["ApiVersion"] = this.ApiVersion;
            builder["Root"] = this.Root.ToString();
            builder["ClusterFileContents"] = clusterFileContents;
            builder["ClientVersion"] = this.ClusterVersion?.ToString();
            return builder.ConnectionString;
        }

    }

}
