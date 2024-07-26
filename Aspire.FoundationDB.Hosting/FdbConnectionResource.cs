#region Copyright (c) 2023-2024 SnowBank SAS
//
// All rights are reserved. Reproduction or transmission in whole or in part, in
// any form or by any means, electronic, mechanical or otherwise, is prohibited
// without the prior written consent of the copyright owner.
//
#endregion

namespace Aspire.Hosting.ApplicationModel
{
	using System;
	using System.Data.Common;
	using FoundationDB.Client;

	/// <summary>Represents an externally hosted FoundationDB Cluster in a distributed application.</summary>
	public class FdbConnectionResource : Resource, IFdbResource
	{

		public FdbConnectionResource(string name) : base(name) { }

		/// <summary>The minimum API version that must be supported by the cluster</summary>
		/// <remarks>See <see cref="Fdb.Start(int)"/> for more information.</remarks>
		public int ApiVersion { get; set; }

		/// <summary>Default root location used by the database</summary>
		/// <remarks>See <see cref="FdbConnectionOptions.Root"/> for more information.</remarks>
		public FdbPath Root { get; set; }

		/// <summary>Full path to a specific 'fdb.cluster' file</summary>
		/// <remarks>
		/// <para>See <see cref="FdbConnectionOptions.ClusterFile"/> for more information.</para>
		/// <para>This property and <see cref="ClusterContents"/> are mutally exclusive.</para>
		/// </remarks>
		public string? ClusterFile { get; set; }

		/// <summary>Connection string to the cluster</summary>
		/// <remarks>
		/// <para>See <see cref="FdbConnectionOptions.ConnectionString"/> for more information.</para>
		/// <para>This property and <see cref="ClusterFile"/> are mutally exclusive.</para>
		/// </remarks>
		public string? ClusterContents { get; set; }

		/// <summary>If specified, the minimum version of the cluster to be deployed.</summary>
		/// <remarks>The actual version of the cluster may be different, depending on the <see cref="FdbVersionPolicy">versioning policy</see> used during deployment</remarks>
		public Version? ClusterVersion { get; set; }

		/// <summary>Path to the local native client library ('fdb_c.dll' or 'libfdb_c.so')</summary>
		/// <remarks>
		/// <para>This value if ignored if <see cref="DisableNativePreloading"/> is set to <see langword="true"/>.</para>
		/// <para>See <see cref="Fdb.Options.SetNativeLibPath"/> for more information.</para>
		/// </remarks>
		public string? NativeLibraryPath { get; set; }

		/// <summary>Specifies if native pre-loading should be enabled or disabled</summary>
		/// <remarks>
		/// <para>If <see langword="true"/>, the value of <see cref="NativeLibraryPath"/> will be ignored.</para>
		/// <para>See <see cref="Fdb.Options.DisableNativeLibraryPreloading"/> for more informations.</para>
		/// </remarks>
		public bool DisableNativePreloading { get; set; }

		//TODO: more options? Debug? TraceId? Timeout? ....

		public ReferenceExpression ConnectionStringExpression => ReferenceExpression.Create($"{GetConnectionString()}");

		/// <summary>Returns the corresponding connection string for this resource</summary>
		public string? GetConnectionString()
		{
			var builder = new DbConnectionStringBuilder();

			builder["ApiVersion"] = this.ApiVersion;

			if (!this.Root.IsEmpty)
			{
				builder["Root"] = this.Root.ToString();
			}

			if (!string.IsNullOrWhiteSpace(this.ClusterFile))
			{
				builder["ClusterFile"] = this.ClusterFile;
			}

			if (!string.IsNullOrWhiteSpace(this.ClusterContents))
			{
				builder["ClusterFileContents"] = this.ClusterContents;
			}

			if (this.ClusterVersion != null)
			{
				builder["ClusterVersion"] = this.ClusterVersion.ToString();
			}

			return builder.ConnectionString;
		}

	}

}
