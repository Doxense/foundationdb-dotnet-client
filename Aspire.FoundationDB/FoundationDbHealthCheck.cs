
namespace Microsoft.Extensions.Hosting
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using FoundationDB.Client;
    using Microsoft.Extensions.Diagnostics.HealthChecks;

    internal sealed record FoundationDbHealthCheckOptions
    {

        public required IFdbDatabaseProvider Provider { get; set; }

    }

    internal sealed class FoundationDbHealthCheck : IHealthCheck
    {
        public FoundationDbHealthCheck(FoundationDbHealthCheckOptions options)
        {
            this.Options = options;
        }

        public FoundationDbHealthCheckOptions Options { get; set; }

        public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = new CancellationToken())
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var rv = await this.Options.Provider.ReadAsync(tr => tr.GetReadVersionAsync(), cancellationToken);
                return HealthCheckResult.Healthy(data: new Dictionary<string, object>()
                {
                    ["readVersion"] = rv,
                });
            }
            catch (Exception ex)
            {
                return HealthCheckResult.Unhealthy(exception: ex);
            }
        }
    }
}
