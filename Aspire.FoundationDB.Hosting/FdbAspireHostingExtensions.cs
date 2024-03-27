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
	using System.Net;
	using System.Net.Sockets;
	using System.Text;
	using Aspire.Hosting.ApplicationModel;
	using Aspire.Hosting.Publishing;
	using Doxense.Diagnostics.Contracts;
	using FoundationDB.Client;
	using JetBrains.Annotations;

	/// <summary>Provides extension methods for adding FoundationDB resources to an <see cref="IDistributedApplicationBuilder"/>.</summary>
	[PublicAPI]
	public static class FdbAspireHostingExtensions
	{

		#region Fdb Cluster Connections...

		/// <summary>Add a connection to an external FoundationDB cluster</summary>
		/// <param name="builder">Builder for the distributed application</param>
		/// <param name="name">Name of the FoundationDB cluster resource (ex: "fdb")</param>
		/// <param name="apiVersion">API version that is requested by the application</param>
		/// <param name="root">Base subspace location used by the application, in the cluster keyspace.</param>
		/// <param name="clusterFile">Path to the cluster file, or <c>null</c> if the default cluster file should be used.</param>
		/// <param name="clusterVersion">If not <c>null</c>, the known version of the remote cluster, which can be used to infer the appropriate version of the local FDB client library that should be used to connect to this cluster.</param>
		public static IResourceBuilder<FdbConnectionResource> AddFoundationDb(this IDistributedApplicationBuilder builder, string name, int apiVersion, string root, string? clusterFile = null, string? clusterVersion = null)
		{
			return AddFoundationDb(builder, name, apiVersion, FdbPath.Parse(root), clusterFile, clusterVersion);
		}

		/// <summary>Add a connection to an external FoundationDB cluster</summary>
		/// <param name="builder">Builder for the distributed application</param>
		/// <param name="name">Name of the FoundationDB cluster resource (ex: "fdb")</param>
		/// <param name="apiVersion">API version that is requested by the application</param>
		/// <param name="root">Root subspace location used by the application, in the cluster keyspace.</param>
		/// <param name="clusterFile">Path to the cluster file, or <c>null</c> if the default cluster file should be used.</param>
		/// <param name="clusterVersion">If not <c>null</c>, the known version of the remote cluster, which can be used to infer the appropriate version of the local FDB client library that should be used to connect to this cluster.</param>
		public static IResourceBuilder<FdbConnectionResource> AddFoundationDb(this IDistributedApplicationBuilder builder, string name, int apiVersion, FdbPath root, string? clusterFile = null, string? clusterVersion = null)
		{
			Contract.NotNull(builder);
			Contract.NotNullOrWhiteSpace(name);
			Contract.GreaterThan(apiVersion, 0);

			//REVIEW: TODO: should we allow formats like "7.2" or "7.2.*" to mean "I don't know exactly, but it is 7.2.something, figure it out!" ?
			//REVIEW: should we also include a "RollForward" policy here? The version of the cluster is fixed, we can only use this to select a client version with more limited wriggle room (cannot jump minor or major version, for example)
			Version? ver = null;
			if (!string.IsNullOrWhiteSpace(clusterVersion) && !Version.TryParse(clusterVersion, out ver))
			{
				throw new ArgumentException("Malformed cluster version", nameof(clusterVersion));
			}

			var fdbConn = new FdbConnectionResource(name)
			{
				ApiVersion = apiVersion,
				Root = root,
				ClusterFile = clusterFile,
				ClusterVersion = ver,
			};

			return builder
				.AddResource(fdbConn)
				.WithAnnotation(new ManifestPublishingCallbackAnnotation((ctx) => WriteFdbConnectionToManifest(ctx, fdbConn)))
			;
		}

		public static IResourceBuilder<FdbConnectionResource> WithDefaultClusterFile(this IResourceBuilder<FdbConnectionResource> builder)
		{
			builder.Resource.ClusterFile = null;
			builder.Resource.ClusterContents = null;
			return builder;
		}

		public static IResourceBuilder<FdbConnectionResource> WithClusterFile(this IResourceBuilder<FdbConnectionResource> builder, string clusterFile)
		{
			builder.Resource.ClusterFile = clusterFile;
			builder.Resource.ClusterContents = null;
			return builder;
		}

		public static IResourceBuilder<FdbConnectionResource> WithClusterContents(this IResourceBuilder<FdbConnectionResource> builder, string clusterContents)
		{
			builder.Resource.ClusterFile = null;
			builder.Resource.ClusterContents = clusterContents;
			return builder;
		}

		public static IResourceBuilder<FdbConnectionResource> WithClusterContents(this IResourceBuilder<FdbConnectionResource> builder, string description, string id, params EndPoint[] coordinators)
		{
			// "<DESC>:<ID>@<HOST1>:<PORT1>[,<HOST2>:<PORT2>,....]"

			var sb = new StringBuilder();
			sb.Append(description).Append(':').Append(id);
			for (int i = 0; i < coordinators.Length; i++)
			{
				sb.Append(i == 0 ? '@' : ',');
				switch (coordinators[i])
				{
					case IPEndPoint ip:
					{
						sb.Append(ip.Address.ToString()).Append(':').Append(ip.Port.ToString(CultureInfo.InvariantCulture));
						break;
					}
					case DnsEndPoint dns:
					{
						sb.Append(dns.Host).Append(':').Append(dns.Port.ToString(CultureInfo.InvariantCulture));
						break;
					}
					default:
					{
						throw new ArgumentException("Unsupported coordinator endpoint type", nameof(coordinators));
					}
				}
			}

			builder.Resource.ClusterFile = null;
			builder.Resource.ClusterContents = sb.ToString();
			return builder;
		}

		public static IResourceBuilder<FdbConnectionResource> WithClusterVersion(this IResourceBuilder<FdbConnectionResource> builder, string version)
		{
			builder.Resource.ClusterVersion = Version.Parse(version);
			return builder;
		}

		#endregion

		#region Locally hosted FDB Cluster using Docker containers...

		/// <summary>Add a local FoundationDB cluster to the application</summary>
		/// <param name="builder">Builder for the distributed application</param>
		/// <param name="name">Name of the FoundationDB cluster resource (ex: "fdb")</param>
		/// <param name="apiVersion">API version that is requested by the application</param>
		/// <param name="root">Root subspace location used by the application, in the cluster keyspace.</param>
		/// <param name="clusterVersion">If not <c>null</c>, specifies the targeted version for the cluster nodes (ex: "7.2.5", "7.3.27", "7.2.*", "7.*", ..)</param>
		/// <param name="rollForward">Specifies the policy used to optionally select a more recent version</param>
		public static IResourceBuilder<FdbClusterResource> AddFoundationDbContainer(this IDistributedApplicationBuilder builder, string name, int apiVersion, string root, string? clusterVersion = null, FdbVersionPolicy? rollForward = null)
		{
			return AddFoundationDbContainer(builder, name, apiVersion, FdbPath.Parse(root), clusterVersion, rollForward);
		}

		/// <summary>Add a local FoundationDB cluster to the application</summary>
		/// <param name="builder">Builder for the distributed application</param>
		/// <param name="name">Name of the FoundationDB cluster resource (ex: "fdb")</param>
		/// <param name="apiVersion">API version that is requested by the application</param>
		/// <param name="root">Root subspace location used by the application, in the cluster keyspace.</param>
		/// <param name="clusterVersion">If not <c>null</c>, specifies the targeted version for the cluster nodes (ex: "7.2.5", "7.3.27", "7.2.*", "7.*", ..)</param>
		/// <param name="rollForward">Specifies the policy used to optionally select a more recent version</param>
		public static IResourceBuilder<FdbClusterResource> AddFoundationDbContainer(this IDistributedApplicationBuilder builder, string name, int apiVersion, FdbPath root, string? clusterVersion = null, FdbVersionPolicy? rollForward = null)
		{
			Contract.NotNull(builder);
			Contract.NotNullOrWhiteSpace(name);
			Contract.GreaterThan(apiVersion, 0);

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

			// select the docker image tag that corresponds to the version and specified rollforward policy
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
				.WithAnnotation(new ManifestPublishingCallbackAnnotation((ctx) => WriteFdbClusterToManifest(ctx, fdbCluster)))
			;

			// Create the first node. By convention, this will also be the coordinator for the cluster
			var fdbCoorinator = fdbCluster.CreateContainer(fdbCluster.Name + "-node-01", coordinator: true);
			ConfigureContainer(builder.AddResource(fdbCoorinator), null);

			return cluster;
		}

		/// <summary>Add and configure a FoundationDB docker container to the cluster</summary>
		/// <param name="container">Container resource to configure</param>
		/// <param name="coordinator">Current cluster coordinator (or <c>null</c> if this new container will become the coordinator)</param>
		private static IResourceBuilder<FdbContainerResource> ConfigureContainer(IResourceBuilder<FdbContainerResource> container, FdbContainerResource? coordinator)
		{
			return container
				.WithAnnotation(new ManifestPublishingCallbackAnnotation((ctx) => WriteFdbContainerToManifest(ctx, container.Resource)))
				.WithAnnotation(new EndpointAnnotation(ProtocolType.Tcp, port: container.Resource.Port, containerPort: 4550)) // default container port is set to 4550
				.WithAnnotation(new ContainerImageAnnotation { Image = "foundationdb/foundationdb", Tag = container.Resource.DockerTag })
				.WithVolumeMount("fdb_data", "/var/fdb/data", isReadOnly: false) //HACKHACK: TODO: make this configurable!
				.WithEnvironment((context) =>
				{
					// override the process class, if specified. The default is "unset"
					if (!string.IsNullOrWhiteSpace(container.Resource.ProcessClass))
					{
						context.EnvironmentVariables["FDB_PROCESS_CLASS"] = container.Resource.ProcessClass;
					}

					// we use the "host" mode so that we can talk to the node from the host
					context.EnvironmentVariables["FDB_NETWORKING_MODE"] = "host";
					//REVIEW: TODO: if the "*.docker.internal" names get supported by Aspire, maybe we can switch to those?
					// => if we have more than one fdb container, we need them to be able to talk to each other, AND to the host!

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

		/// <summary>Select the number of nodes that will compose the FoundationDB cluster</summary>
		/// <param name="builder">FoundationDB Cluster resource builder</param>
		/// <param name="count">Number of replicas required (must be greather than or equal to 1)</param>
		/// <exception cref="InvalidOperationException">If the replica count is already greater than <paramref name="count"/>.</exception>
		public static IResourceBuilder<FdbClusterResource> WithReplicas(this IResourceBuilder<FdbClusterResource> builder, int count)
		{
			Contract.NotNull(builder);
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

		/// <summary>Specifies the path to the native FoundationDB C library that should be used by the application</summary>
		/// <param name="builder">FDB cluster builder</param>
		/// <param name="nativeLibraryPath">Path to the library on the host. The path may be rewritten if required.</param>
		/// <remarks>
		/// <para>This should only be used for local developement, or very specific deployments where the application must use a very specific build of the native library.</para>
		/// <para>The file must be present on the host. If a project that references this resource runs inside a Docker image, the path may be rewritten to where the library was copied inside the container image.</para>
		/// </remarks>
		public static IResourceBuilder<FdbClusterResource> WithNativeLibrary(this IResourceBuilder<FdbClusterResource> builder, string? nativeLibraryPath)
		{
			Contract.NotNull(builder);
			var fdbCluster = builder.Resource;
			fdbCluster.NativeLibraryPath = !string.IsNullOrWhiteSpace(nativeLibraryPath) ? nativeLibraryPath.Trim() : null;
			return builder;
		}

		#endregion

		private static void WriteFdbClusterToManifest(ManifestPublishingContext context, FdbClusterResource cluster)
		{
			var jsonWriter = context.Writer;
			jsonWriter.WriteString("type", "fdb.cluster.v0");
			jsonWriter.WriteNumber("apiVersion", cluster.ApiVersion);
			jsonWriter.WriteString("root", cluster.Root.ToString());
			jsonWriter.WriteString("clusterId", cluster.ClusterId);
			jsonWriter.WriteString("clusterDesc", cluster.ClusterDescription);
			jsonWriter.WriteString("version", cluster.ClusterVersion.ToString());
			jsonWriter.WriteString("rollForward", cluster.RollForward.ToString());
			jsonWriter.WriteNumber("portStart", cluster.PortStart);
		}

		private static void WriteFdbContainerToManifest(ManifestPublishingContext context, FdbContainerResource container)
		{
			var jsonWriter = context.Writer;
			jsonWriter.WriteString("type", "fdb.container.v0");
			jsonWriter.WriteString("parent", container.Parent.Name);
			if (container.IsCoordinator)
			{
				jsonWriter.WriteBoolean("coordinator", true);
			}
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

		private static void WriteFdbConnectionToManifest(ManifestPublishingContext context, FdbConnectionResource connection)
		{
			var jsonWriter = context.Writer;
			jsonWriter.WriteString("type", "fdb.connection.v0");
			jsonWriter.WriteNumber("apiVersion", connection.ApiVersion);
			jsonWriter.WriteString("root", connection.Root.ToString());
			if (connection.ClusterVersion != null)
			{
				jsonWriter.WriteString("version", connection.ClusterVersion.ToString());
			}
			if (!string.IsNullOrWhiteSpace(connection.ClusterFile))
			{
				jsonWriter.WriteString("clusterFile", connection.ClusterFile);
			}
			if (!string.IsNullOrWhiteSpace(connection.ClusterContents))
			{
				jsonWriter.WriteString("clusterFileContents", connection.ClusterContents);
			}

			if (connection.DisableNativePreloading)
			{
				jsonWriter.WriteBoolean("disableNativePreloading", true);
			}
			else if (!string.IsNullOrWhiteSpace(connection.NativeLibraryPath))
			{
				jsonWriter.WriteString("nativeLibrary", connection.ClusterContents);
			}

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
							return "7.3.35";
						}
						case (7, 2):
						{
							return "7.2.9";
						}
						case (7, 1):
						{
							return "7.1.59";
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

							return version is { Minor: 3, Build: > 35 }
								? "7.3." + version.Build.ToString(CultureInfo.InvariantCulture)
								: "7.3.35";
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

					if (version is { Major: 7, Minor: 3, Build: > 35 })
					{
						return "7.3." + version.Build.ToString(CultureInfo.InvariantCulture);
					}
					else
					{
						return "7.3.35";
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
