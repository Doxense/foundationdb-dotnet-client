#region Copyright (c) 2023-2023 SnowBank SAS
//
// All rights are reserved. Reproduction or transmission in whole or in part, in
// any form or by any means, electronic, mechanical or otherwise, is prohibited
// without the prior written consent of the copyright owner.
//
#endregion

namespace Aspire.Hosting.ApplicationModel
{
	using System.Data.Common;
	using FoundationDB.Client;

	public class FdbConnectionResource : Resource, IFdbResource
	{

		public FdbConnectionResource(string name) : base(name) { }

		public int ApiVersion { get; set; }

		public FdbPath Root { get; set; }

		public string? ClusterFile { get; set; }

		public string? ClusterContents { get; set; }

		public Version? ClusterVersion { get; set; }

		//TODO: more options? Debug? TraceId? Timeout? ....

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
