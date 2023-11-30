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

    /// <summary>
    /// Provides the client configuration settings for connecting to a FoundationDB cluster.
    /// </summary>
    public sealed class FdbClientSettings
    {
		//note: most of these settings can be used to _override_ the settings contained in the connection string which is injected by the Aspire runner
		// This connection string use global settings, as defined in the AppHost, which gets applied to all instance, unless they need to specifically change one of them, using these parameters.
		// => usually, they should be left empty, and only used to customize a specific instance

        /// <summary>The connection string of the FoundationDB server to connect to.</summary>
        public string? ConnectionString { get; set; }

		/// <summary>Overrides the API version that should be used by this instance</summary>
		public int? ApiVersion { get; set; }

		/// <summary>Overrides the root subspace location that should be used by this instance</summary>
		public string? Root { get; set; }

		public bool? ReadOnly { get; set; }

		/// <summary>Overrides the path to the cluster file that should be used by this instance</summary>
		public string? ClusterFile { get; set; }

		/// <summary>Overrides the content of the cluste file that should be used by this instance</summary>
		public string? ClusterContents { get; set; }

		public string? ClusterVersion { get; set; }

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
