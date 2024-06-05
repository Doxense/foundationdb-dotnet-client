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
	using System.Collections.Generic;
	using System.Threading;
	using System.Threading.Tasks;
	using FoundationDB.Client;
	using Microsoft.Extensions.Diagnostics.HealthChecks;

	internal sealed record FdbHealthCheckOptions
	{

		public required IFdbDatabaseProvider Provider { get; set; }

	}

	internal sealed class FdbHealthCheck : IHealthCheck
	{
		public FdbHealthCheck(FdbHealthCheckOptions options)
		{
			this.Options = options;
		}

		public FdbHealthCheckOptions Options { get; set; }

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
