#region Copyright (c) 2023-2024 SnowBank SAS
//
// All rights are reserved. Reproduction or transmission in whole or in part, in
// any form or by any means, electronic, mechanical or otherwise, is prohibited
// without the prior written consent of the copyright owner.
//
#endregion

namespace Aspire.FoundationDb.Client
{
	using System;
	using FoundationDB.Client;

	/// <summary>Provides the client configuration settings for connecting to a FoundationDB cluster.</summary>
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

		/// <summary>Overrides the read-only flag that should be used by this instance</summary>
		public bool? ReadOnly { get; set; }

		/// <summary>Overrides the path to the cluster file that should be used by this instance</summary>
		public string? ClusterFile { get; set; }

		/// <summary>Overrides the content of the cluster file that should be used by this instance</summary>
		public string? ClusterContents { get; set; }

		/// <summary>Overrides the cluster version that should be used by this instance</summary>
		public string? ClusterVersion { get; set; }

		/// <summary>Overrides the path to the native library that should be used by this instance</summary>
		/// <remarks>
		/// <para>If <see cref="string.Empty">empty</see>, pre-loading of the native C API library will be enabled, and the operating system will handle the binding of the library.</para>
		/// <para>If not empty, the native C API library will be pre-loaded using the specified path. If the file does not exist, is not readable, or is corrupted, the startup will fail.</para>
		/// </remarks>
		public string? NativeLibraryPath { get; set; }

		/// <summary>Overrides the default transaction timeout that should be used by this instance</summary>
		/// <remarks>See <see cref="IFdbTransactionOptions.Timeout"/> for more information.</remarks>
		public TimeSpan? DefaultTimeout { get; set; }

		/// <summary>Overrides the default transaction retry limit that should be used by this instance</summary>
		/// <remarks>See <see cref="IFdbTransactionOptions.RetryLimit"/> for more information.</remarks>
		public int? DefaultRetryLimit { get; set; }

		/// <summary>Overrides the default tracing options that should be used by this instance</summary>
		/// <remarks>
		/// <para>See <see cref="IFdbTransactionOptions.Tracing"/> for more information.</para>
		/// <para>The default tracing options are <see cref="FdbTracingOptions.RecordTransactions"/> and <see cref="FdbTracingOptions.RecordOperations"/></para>
		/// </remarks>
		public int? DefaultTracing { get; set; }

		/// <summary>Gets or sets a boolean value that indicates whether the FoundationDB health check is disabled or not.</summary>
		/// <remarks><para>Enabled by default.</para></remarks>
		public bool DisableHealthChecks { get; set; }

		/// <summary>Gets or sets a boolean value that indicates whether the OpenTelemetry tracing is disabled or not.</summary>
		/// <remarks><para>Tracing is enabled by default.</para></remarks>
		public bool DisableTracing { get; set; }

		/// <summary>Gets or sets a boolean value that indicates whether the OpenTelemetry metrics are disabled or not.</summary>
		/// <remarks><para>Metrics are enabled by default.</para></remarks>
		public bool DisableMetrics { get; set; }

	}

}
