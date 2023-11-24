
namespace Microsoft.Extensions.Hosting
{
    using System;

    /// <summary>
    /// Provides the client configuration settings for connecting to a RabbitMQ message broker.
    /// </summary>
    public sealed class FoundationDbAspireClientSettings
    {
        /// <summary>
        /// The connection string of the RabbitMQ server to connect to.
        /// </summary>
        public string? ConnectionString { get; set; }

        /// <summary>
        /// <para>Gets or sets the maximum number of connection retry attempts.</para>
        /// <para>Default value is 5, set it to 0 to disable the retry mechanism.</para>
        /// </summary>
        public int MaxConnectRetryCount { get; set; } = 5;

        /// <summary>
        /// <para>Gets or sets a boolean value that indicates whether the RabbitMQ health check is enabled or not.</para>
        /// <para>Enabled by default.</para>
        /// </summary>
        public bool HealthChecks { get; set; } = true;

        /// <summary>
        /// <para>Gets or sets a boolean value that indicates whether the OpenTelemetry tracing is enabled or not.</para>
        /// <para>Enabled by default.</para>
        /// </summary>
        public bool Tracing { get; set; } = true;
    }
}