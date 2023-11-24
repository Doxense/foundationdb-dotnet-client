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
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Diagnostics.HealthChecks;

    /// <summary>Provides extension methods for adding FoundationDB resources to an <see cref="IDistributedApplicationBuilder"/>.</summary>
    public static class AspireFoundationDbExtensions
    {

        private const string ActivitySourceName = "Aspire.FoundationDb.Client";

        private const string DefaultConfigSectionName = "Aspire:FoundationDb:Client";

        public static IHostApplicationBuilder AddFoundationDb(this IHostApplicationBuilder builder, string connectionName, Action<FoundationDbAspireClientSettings>? configure = null)
        {
            return AddFoundationDb(builder, DefaultConfigSectionName, configure, connectionName);
        }

        private static IHostApplicationBuilder AddFoundationDb(this IHostApplicationBuilder builder, string configurationSectionName, Action<FoundationDbAspireClientSettings>? configureSettings, string connectionName)
        {
            ArgumentNullException.ThrowIfNull(builder);

            var configSection = builder.Configuration.GetSection(configurationSectionName);

            var settings = new FoundationDbAspireClientSettings();
            configSection.Bind(settings);

            if (builder.Configuration.GetConnectionString(connectionName) is { } connectionString)
            {
                settings.ConnectionString = connectionString;
            }

            configureSettings?.Invoke(settings);

            // parse the connection string:
            // "ApiVersion=XXX"
            // "ClusterFile=XXX"
            // "ClusterFileContents=XXX",
            var cnx = new DbConnectionStringBuilder()
            {
                ConnectionString = settings.ConnectionString,
            };

            var apiVersionLiteral = cnx.ContainsKey("ApiVersion") ? (string?) cnx["ApiVersion"] : null;
            var clusterFilePath = cnx.ContainsKey("ClusterFile") ? (string?) cnx["ClusterFile"] : null;
            var clusterFileContents = cnx.ContainsKey("ClusterFileContents") ? (string?) cnx["ClusterFileContents"] : null;
            var rootPathLiteral = cnx.ContainsKey("Root") ? (string?) cnx["Root"] : null;

            if (string.IsNullOrWhiteSpace(apiVersionLiteral))
            {
	            throw new InvalidOperationException("Missing required ApiVersion parameter");
            }
            if (!int.TryParse(apiVersionLiteral, NumberStyles.Integer, CultureInfo.InvariantCulture, out var apiVersion) || apiVersion <= 0)
            {
                throw new InvalidOperationException("Invalid ApiVersion parameter");
            }

            FdbPath rootPath;
            if (!string.IsNullOrWhiteSpace(rootPathLiteral))
            {
				//TODO: we need a TryParse method here!
				try
				{
					rootPath = FdbPath.Parse(rootPathLiteral);
				}
				catch (Exception e)
				{
					throw new InvalidOperationException("Invalid Root parameter", e);
				}
            }
            else
            {
                rootPath = FdbPath.Root;
            }

            builder.Services.AddFoundationDb(apiVersion, options =>
            {
                options.AutoStart = true;
                options.ConnectionOptions.Root = rootPath;

                if (!string.IsNullOrWhiteSpace(clusterFilePath))
                {
                    options.ConnectionOptions.ClusterFile = clusterFilePath;
                }
                else if (!string.IsNullOrWhiteSpace(clusterFileContents))
                {
                    //REVIEW: we need to find a proper location for this file, and which will not conflict with other processes!
                    clusterFilePath = Path.GetFullPath("local-" + connectionName + ".cluster");
                    File.WriteAllText(clusterFilePath, clusterFileContents);
                    options.ConnectionOptions.ClusterFile = clusterFilePath;
                }
                else
                {
                    // use system default cluster file?
                }
            });

            if (settings.Tracing)
            {
                // Note that RabbitMQ.Client v6.6 doesn't have built-in support for tracing. See https://github.com/rabbitmq/rabbitmq-dotnet-client/pull/1261

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
                            var options = new FoundationDbHealthCheckOptions()
                            {
                                Provider = sp.GetRequiredService<IFdbDatabaseProvider>(),
                            };
                            return new FoundationDbHealthCheck(options);
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
	        private readonly Exception m_ex;

	        public FailedHealthCheck(Exception ex)
	        {
		        m_ex = ex;
	        }

	        public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
            {
                return Task.FromResult(new HealthCheckResult(context.Registration.FailureStatus, exception: m_ex));
            }
        }

    }

}
