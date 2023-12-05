#region Copyright (c) 2023-2023 SnowBank SAS
//
// All rights are reserved. Reproduction or transmission in whole or in part, in
// any form or by any means, electronic, mechanical or otherwise, is prohibited
// without the prior written consent of the copyright owner.
//
#endregion

namespace Microsoft.Extensions.Hosting
{
	using System;
	using System.Data.Common;
	using System.Globalization;
	using System.IO;
	using System.Threading;
	using System.Threading.Tasks;
	using FoundationDB.Client;
	using FoundationDB.DependencyInjection;
	using JetBrains.Annotations;
	using Microsoft.Extensions.Configuration;
	using Microsoft.Extensions.DependencyInjection;
	using Microsoft.Extensions.Diagnostics.HealthChecks;

	/// <summary>Provides extension methods for adding FoundationDB to the local DI container.</summary>
	[PublicAPI]
	public static class FdbAspireComponentExtensions
	{

		private const string ActivitySourceName = "Aspire.FoundationDb.Client";

		private const string DefaultConfigSectionName = "Aspire:FoundationDb:Client";

		/// <summary>Add support for connecting to a FoundationDB cluster</summary>
		/// <param name="builder">Application builder</param>
		/// <param name="connectionName">Name of the FoundationDB cluster or connection resource, as defined in the Aspire AppHost.</param>
		/// <param name="configureSettings">Optional callback used to configure the <see cref="FdbClientSettings">Aspire settings</see>.</param>
		/// <param name="configureProvider">Optional callback used to configure the <see cref="FdbDatabaseProviderOptions"></see>.</param>
		/// <remarks>This method is intended to be used in conjection with the Aspire SDK.</remarks>
		public static IHostApplicationBuilder AddFoundationDb(this IHostApplicationBuilder builder, string connectionName, Action<FdbClientSettings>? configureSettings = null, Action<FdbDatabaseProviderOptions>? configureProvider = null)
		{
			return AddFoundationDb(builder, connectionName, DefaultConfigSectionName, configureSettings, configureProvider);
		}

		private static IHostApplicationBuilder AddFoundationDb(this IHostApplicationBuilder builder, string connectionName, string configurationSectionName, Action<FdbClientSettings>? configureSettings, Action<FdbDatabaseProviderOptions>? configureProvider)
		{
			ArgumentNullException.ThrowIfNull(builder);

			var configSection = builder.Configuration.GetSection(configurationSectionName);

			var settings = new FdbClientSettings();
			configSection.Bind(settings);

			if (builder.Configuration.GetConnectionString(connectionName) is { } connectionString)
			{
				settings.ConnectionString = connectionString;
			}

			configureSettings?.Invoke(settings);

			// parse the connection string:
			var cnx = !string.IsNullOrWhiteSpace(settings.ConnectionString) ? new DbConnectionStringBuilder()
			{
				ConnectionString = settings.ConnectionString,
			} : null;
			//note: in general, the value in the connection string takes precedence other the values in the configuration section, which act like a default value (or when running outside of the Aspire AppHost)

			builder.Services.AddFoundationDb(options =>
			{
				options.AutoStart = true;

				// ApiVersion=123
				int apiVersion;
				if (cnx != null && cnx.ContainsKey("ApiVersion"))
				{
					var apiVersionLiteral = (string) cnx["ApiVersion"];
					if (!int.TryParse(apiVersionLiteral, NumberStyles.Integer, CultureInfo.InvariantCulture, out apiVersion) || apiVersion <= 0)
					{
						throw new InvalidOperationException("Invalid ApiVersion parameter in connection string.");
					}
				}
				else if (settings.ApiVersion is > 0)
				{ // use the value specified in the settings
					apiVersion = settings.ApiVersion.Value;
				}
				else
				{
					throw new InvalidOperationException($"Missing required ApiVersion parameter. It must be specified either be injected via the Aspire ConnectionString, or via the {DefaultConfigSectionName}:{nameof(settings.ApiVersion)} configuration option.");
				}
				options.ApiVersion = apiVersion;

				// Cluster File Path

				// either specified directly with ClusterFile=/path/to/file.cluster, or indirectly via ClusterFileContenst=desc:id@ip:port
				string? clusterFilePath, clusterFileContents;
				if (cnx != null && (cnx.ContainsKey("ClusterFile") || cnx.ContainsKey("ClusterFileContents")))
				{
					clusterFilePath = cnx.ContainsKey("ClusterFile") ? ((string?) cnx["ClusterFile"])?.Trim() : null;
					clusterFileContents = cnx.ContainsKey("ClusterFileContents") ? ((string?) cnx["ClusterFileContents"])?.Trim() : null;
				}
				else
				{
					clusterFilePath = !string.IsNullOrWhiteSpace(settings.ClusterFile) ? settings.ClusterFile.Trim() : null;
					clusterFileContents = !string.IsNullOrWhiteSpace(settings.ClusterContents) ? settings.ClusterContents.Trim() : null;
				}

				//REVIEW: what to do if BOTH are specified? write the content to the specified path instead of a temp file ??
				if (!string.IsNullOrWhiteSpace(clusterFilePath))
				{
					options.ConnectionOptions.ClusterFile = clusterFilePath;
				}
				else if (!string.IsNullOrWhiteSpace(clusterFileContents))
				{
					//HACKHACK: BUGBUG: TODO: we need to find a proper location for this file, and which will not conflict with other processes!
					clusterFilePath = Path.GetFullPath("local-" + connectionName + ".cluster");
					File.WriteAllText(clusterFilePath, clusterFileContents);
					options.ConnectionOptions.ClusterFile = clusterFilePath;
				}
				else
				{
					// use system default cluster file
					options.ConnectionOptions.ClusterFile = null;
				}

				// Root=/path/to/root
				var rootPathLiteral = cnx != null && cnx.ContainsKey("Root") ? ((string?) cnx["Root"])?.Trim() : !string.IsNullOrWhiteSpace(settings.Root) ? settings.Root.Trim() : null;
				if (!string.IsNullOrWhiteSpace(rootPathLiteral))
				{
					if (!FdbPath.TryParse(rootPathLiteral, out var rootPath))
					{
						throw new InvalidOperationException("Invalid Root Path parameter");
					}
					options.ConnectionOptions.Root = rootPath;
				}

				// ReadOnly=(true|false) (default: false)
				if (cnx != null && cnx.ContainsKey("ReadOly"))
				{
					options.ConnectionOptions.ReadOnly = !string.Equals((string) cnx["ReadOnly"], "true", StringComparison.OrdinalIgnoreCase);
				}
				else
				{
					options.ConnectionOptions.ReadOnly = settings.ReadOnly ?? false;
				}

				// ClusterVersion=7.2.5
				var clusterVersionLiteral = cnx != null && cnx.ContainsKey("ClusterVersion") ? ((string?) cnx["ClusterVersion"])?.Trim() : !string.IsNullOrWhiteSpace(settings.ClusterVersion) ? settings.ClusterVersion.Trim() : null;
				if (clusterVersionLiteral != null)
				{
					if (!Version.TryParse(clusterVersionLiteral, out var clusterVersion))
					{
						throw new InvalidOperationException("Invalid Cluster Version parameter");
					}

					//TODO: how can we use the clusterVersion?
					// => ideally we would like to be able to select the correct FDB C client dll that is compatible,
					//    but we would need to add support for multiversion clients to the binding!
				}

				// run additionall custom configuration
				configureProvider?.Invoke(options);
			});

			if (settings.Tracing)
			{
				builder.Services.AddOpenTelemetry()
					.WithTracing(traceBuilder => traceBuilder.AddSource(ActivitySourceName));
			}

			if (settings.HealthChecks)
			{
				var check = new HealthCheckRegistration(
					"FoundationDb.Client",
					sp =>
					{
						try
						{
							// if the IConnection can't be resolved, make a health check that will fail
							var options = new FdbHealthCheckOptions()
							{
								Provider = sp.GetRequiredService<IFdbDatabaseProvider>(),
							};
							return new FdbHealthCheck(options);
						}
						catch (Exception ex)
						{
							return new FailedHealthCheck(ex);
						}
					},
					failureStatus: default,
					tags: default);

				var healthCheckKey = $"Aspire.HealthChecks.{check.Name}";
				if (!builder.Properties.ContainsKey(healthCheckKey))
				{
					builder.Properties[healthCheckKey] = true;
					builder.Services.AddHealthChecks().Add(check);
				}
			}
			return builder;
		}

		private sealed class FailedHealthCheck : IHealthCheck
		{
			private readonly Exception Error;

			public FailedHealthCheck(Exception error)
			{
				Error = error;
			}

			public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default) =>
				!cancellationToken.IsCancellationRequested
					? Task.FromResult(new HealthCheckResult(context.Registration.FailureStatus, exception: Error))
					: Task.FromCanceled<HealthCheckResult>(cancellationToken);
		}

	}

}
