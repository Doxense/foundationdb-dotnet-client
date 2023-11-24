#region Copyright (c) 2023-2023 SnowBank SAS
//
// All rights are reserved. Reproduction or transmission in whole or in part, in
// any form or by any means, electronic, mechanical or otherwise, is prohibited
// without the prior written consent of the copyright owner.
//
#endregion

namespace Aspire.Hosting
{
    using System;
    using System.Globalization;
    using System.Net.Sockets;
    using System.Text.Json;
    using Aspire.Hosting.ApplicationModel;
    using FoundationDB.Client;

    /// <summary>Provides extension methods for adding FoundationDB resources to an <see cref="IDistributedApplicationBuilder"/>.</summary>
    public static class FdbBuilderExtensions
    {

        public static IResourceBuilder<FdbConnectionResource> AddFdbConnection(this IDistributedApplicationBuilder builder, string name, int apiVersion, string clusterFile)
        {
            var fdbConn = new FdbConnectionResource(name)
            {
                ApiVersion = apiVersion,
                ClusterFile = clusterFile,
            };

            return builder
                .AddResource(fdbConn)
                .WithAnnotation(new ManifestPublishingCallbackAnnotation((json) => WriteFdbConnectionToManifest(json, fdbConn)))
            ;
        }

        public static IResourceBuilder<FdbClusterResource> AddFdbCluster(this IDistributedApplicationBuilder builder, string name, int apiVersion, string root, string? clusterVersion = null, FdbVersionPolicy? rollForward = null)
        {
	        return AddFdbCluster(builder, name, apiVersion, FdbPath.Parse(root), clusterVersion, rollForward);
        }

        public static IResourceBuilder<FdbClusterResource> AddFdbCluster(this IDistributedApplicationBuilder builder, string name, int apiVersion, FdbPath root, string? clusterVersion = null, FdbVersionPolicy? rollForward = null)
        {
            Version? ver;
            if (string.IsNullOrWhiteSpace(clusterVersion) || clusterVersion == "*")
            { // version is not specified

                // Use the request ApiVersion to select the correct version
                // The version corresponding to level 720 is 7.2 (last digit is usually always 0)
                int major = (apiVersion / 100);
                int minor = (apiVersion / 10) % 10;
                ver = new Version(major, minor);
                rollForward ??= FdbVersionPolicy.LatestMajor;
            }
            else if (clusterVersion.Length > 2 && clusterVersion.EndsWith(".*", StringComparison.Ordinal))
            { // version includes a variable part
                ver = Version.Parse(clusterVersion[..^2]);
                if (ver.Minor < 0)
                { // ex: "7.*"
                    rollForward ??= FdbVersionPolicy.LatestMinor;
                }
                else
                { // ex: "7.3.x"
                    rollForward ??= FdbVersionPolicy.LatestPatch;
                }
            }
            else
            { // exact version
                ver = Version.Parse(clusterVersion);
                rollForward ??= FdbVersionPolicy.Exact;
            }

            var dockerTag = ComputeDockerTagFromVersion(ver, rollForward.Value);

            var fdbCluster = new FdbClusterResource(name)
            {
                ApiVersion = apiVersion,
                Root = root,
                ClusterVersion = ver,
                RollForward = rollForward.Value,
                DockerTag = dockerTag, 
            };

            var cluster = builder
                .AddResource(fdbCluster)
                .WithAnnotation(new ManifestPublishingCallbackAnnotation((json) => WriteFdbClusterToManifest(json, fdbCluster)))
            ;

            // Create the first node. By convention, this will also be the coordinator for the cluster
            var fdbCoorinator = fdbCluster.CreateContainer(fdbCluster.Name + "-node-01", coordinator: true);
            ConfigureContainer(builder.AddResource(fdbCoorinator), null);

            return cluster;
        }

        private static Func<string?> GetContainerAddressCallback(FdbContainerResource container) => () => container.GetAllocatedEndpoint().Address.ToString();
        private static Func<string?> GetContainerPortCallback(FdbContainerResource container) => () => container.GetAllocatedEndpoint().Port.ToString(CultureInfo.InvariantCulture);

        private static IResourceBuilder<FdbContainerResource> ConfigureContainer(IResourceBuilder<FdbContainerResource> container, FdbContainerResource? coordinator)
        {
            return container
                .WithAnnotation(new ManifestPublishingCallbackAnnotation((json) => WriteFdbContainerToManifest(json, container.Resource)))
                .WithAnnotation(new ServiceBindingAnnotation(ProtocolType.Tcp, port: container.Resource.Port, containerPort: 4550))
                .WithAnnotation(new ContainerImageAnnotation { Image = "foundationdb/foundationdb", Tag = container.Resource.DockerTag })
                .WithVolumeMount("fdb_data", "/var/fdb/data", VolumeMountType.Named, isReadOnly: false)
                .WithEnvironment((context) =>
                {
                    if (!string.IsNullOrWhiteSpace(container.Resource.ProcessClass))
                    {
                        context.EnvironmentVariables["FDB_PROCESS_CLASS"] = container.Resource.ProcessClass;
                    }

                    // we use the "host" mode so that we can talk to the node from the host
                    context.EnvironmentVariables["FDB_NETWORKING_MODE"] = "host";
                    context.EnvironmentVariables["FDB_PORT"] =  container.Resource.Port.ToString(CultureInfo.InvariantCulture);
                    if (coordinator != null)
                    { // connect to the coordinator
                        context.EnvironmentVariables["FDB_COORDINATOR"] = coordinator.Port.ToString(CultureInfo.InvariantCulture);
                        context.EnvironmentVariables["FDB_COORDINATOR_PORT"] = coordinator.Port.ToString(CultureInfo.InvariantCulture);
                    }
                    else
                    { // we are the coordinator
                        context.EnvironmentVariables["FDB_COORDINATOR_PORT"] = container.Resource.Port.ToString(CultureInfo.InvariantCulture);
                    }
                });
        }

        public static IResourceBuilder<FdbClusterResource> WithReplicas(this IResourceBuilder<FdbClusterResource> builder, int count)
        {
            var fdbCluster = builder.Resource;

            // we always include at least one container, so we only have to define the additional replicas as more container
            if (count > fdbCluster.Containers.Count)
            {
                var fdbCoordinator = fdbCluster.GetCoordinator()!;

                for (int i = fdbCluster.Containers.Count; i < count; i++)
                {
                    var fdbContainer = fdbCluster.CreateContainer(fdbCluster.Name + "-node-" + i.ToString("D02"), coordinator: false);

                    ConfigureContainer(builder.ApplicationBuilder.AddResource(fdbContainer), fdbCoordinator);
                }

            }
            else if (count < fdbCluster.Containers.Count)
            {
                throw new InvalidOperationException("Cannot reduce the number of replicas already defined on this cluster");
            }

            return builder;
        }

        private static void WriteFdbClusterToManifest(Utf8JsonWriter jsonWriter, FdbClusterResource cluster)
        {
            jsonWriter.WriteString("type", "fdb.cluster.v0");
            jsonWriter.WriteNumber("apiVersion", cluster.ApiVersion);
            jsonWriter.WriteString("version", cluster.ClusterVersion.ToString());
            jsonWriter.WriteString("rollForward", cluster.RollForward.ToString());
        }

        private static void WriteFdbContainerToManifest(Utf8JsonWriter jsonWriter, FdbContainerResource container)
        {
            jsonWriter.WriteString("type", "fdb.container.v0");
            jsonWriter.WriteString("parent", container.Parent.Name);
            jsonWriter.WriteNumber("port", container.Port);
            if (!string.IsNullOrWhiteSpace(container.DockerTag))
            {
                jsonWriter.WriteString("tag", container.DockerTag);
            }
            if (!string.IsNullOrWhiteSpace(container.ProcessClass))
            {
                jsonWriter.WriteString("class", container.ProcessClass);
            }
        }

        private static void WriteFdbConnectionToManifest(Utf8JsonWriter jsonWriter, FdbConnectionResource connection)
        {
            jsonWriter.WriteString("type", "fdb.connection.v0");
            jsonWriter.WriteNumber("apiVersion", connection.ApiVersion);
            jsonWriter.WriteString("clusterFile", connection.ClusterFile);
        }

        public static string ComputeDockerTagFromVersion(Version version, FdbVersionPolicy rollForward)
        {
            //TODO: maybe query the docker hub API, but use a local cache?
            // => https://registry.hub.docker.com/v2/repositories/foundationdb/foundationdb/tags?name=X.Y&ordering=last_updated

            switch (rollForward)
            {
                case FdbVersionPolicy.Exact:
                {
                    return version.Major.ToString(CultureInfo.InvariantCulture) + "." + version.Minor.ToString(CultureInfo.InvariantCulture) + "." + version.Build.ToString(CultureInfo.InvariantCulture);
                }
                case FdbVersionPolicy.Latest:
                { // I like to live dangerously!
                    return "latest";
                }
                case FdbVersionPolicy.LatestPatch:
                { // Keep major.minor but use the latest patch (ie: X.Y.*)
                    switch (version.Major, version.Minor)
                    {
                        case (7, 3):
                        {
                            return "7.3.27";
                        }
                        case (7, 2):
                        {
                            return "7.2.9";
                        }
                        case (7, 1):
                        {
                            return "7.1.43";
                        }
                        default:
                        {
                            throw version.Major switch
                            {
                                < 7 => ErrorVersionIsTooOldMajor(version),
                                7 => ErrorVersionIsTooOldMinor(version),
                                _ => ErrorVersionIsGreaterThanSupportedByThisPackage(version)
                            };
                        }
                    }
                }
                case FdbVersionPolicy.LatestMinor:
                { // Keep major but use latest patch of latest minor (ie: X.*.*)
                    switch (version.Major)
                    {
                        case 7:
                        {
                            if (version.Minor > 3)
                            {
                                throw ErrorVersionIsGreaterThanSupportedByThisPackage(version);
                            }

                            return version is { Minor: 3, Build: > 27 }
                                ? "7.3." + version.Build.ToString(CultureInfo.InvariantCulture)
                                : "7.3.27";
                        }
                        default:
                        {
                            throw version.Major < 7 ? ErrorVersionIsTooOldMajor(version) : ErrorVersionIsGreaterThanSupportedByThisPackage(version);
                        }
                    }
                }
                case FdbVersionPolicy.LatestMajor:
                { // Use the latest (stable) version available

                    if (version.Major > 7 || (version.Major == 7 && version.Minor > 3))
                    {
                        throw ErrorVersionIsGreaterThanSupportedByThisPackage(version);
                    }

                    if (version is { Major: 7, Minor: 3, Build: > 27 })
                    {
                        return "7.3." + version.Build.ToString(CultureInfo.InvariantCulture);
                    }
                    else
                    {
                        return "7.3.27";
                    }
                }
                default:
                {
                    throw new InvalidOperationException($"The roll forward policy '{rollForward}' is not supported.");
                }
            }
        }

        private static Exception ErrorVersionIsTooOldMajor(Version version)
        {
            return new InvalidOperationException($"There are no docker images available for version {version}. The first docker images available start from 7.1.0. Please use the 'latestMajor' policy, or select a version of 7.1 or greater.");
        }

        private static Exception ErrorVersionIsTooOldMinor(Version version)
        {
            return new InvalidOperationException($"There are no docker images available for version {version}. The first docker images available start from 7.1.0. Please use the 'latestMinor' policy, or select a version of 7.1 or greater.");
        }

        private static Exception ErrorVersionIsGreaterThanSupportedByThisPackage(Version version)
        {
            return new NotImplementedException($"The selected version {version} is too recent and is not supported by this NuGet package. Please update the NuGet package or select an older version.");
        }

    }

}
