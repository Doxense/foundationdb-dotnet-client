#region Copyright (c) 2023-2024 SnowBank SAS
//
// All rights are reserved. Reproduction or transmission in whole or in part, in
// any form or by any means, electronic, mechanical or otherwise, is prohibited
// without the prior written consent of the copyright owner.
//
#endregion

namespace OpenTelemetry.Trace
{
	using FoundationDB.Client;
	using OpenTelemetry.Metrics;

	public static class FdbClientOpenTelemtryExtensions
	{

		public static TracerProviderBuilder AddFoundationDbInstrumentation(this TracerProviderBuilder provider)
		{
			provider.AddSource(FdbClientInstrumentation.ActivityName);
			return provider;
		}

		public static MeterProviderBuilder AddFoundationDbInstrumentation(this MeterProviderBuilder provider)
		{
			provider.AddMeter(FdbClientInstrumentation.MeterName)
				.AddView(
					"db.fdb.client.transactions.duration",
					new ExplicitBucketHistogramConfiguration { Boundaries = [ 0, 0.001, 0.0025, 0.005, 0.0075, 0.01, 0.025, 0.05, 0.075, 0.1, 0.25, 0.5, 0.75, 1, 2.5, 5, 7.5, 10 ], })
				.AddView(
					"db.fdb.client.operations.duration",
					new ExplicitBucketHistogramConfiguration { Boundaries = [ 0, 0.001, 0.0025, 0.005, 0.0075, 0.01, 0.025, 0.05, 0.075, 0.1, 0.25, 0.5, 0.75, 1, 2.5, 5, 7.5, 10 ], })
				.AddView(
					"db.fdb.client.operations.size",
					new ExplicitBucketHistogramConfiguration { Boundaries = [ 0, 10, 20, 50, 100, 200, 500, 1_000, 2_000, 5_000, 10_000, 20_000, 50_000, 100_000, 200_000, 500_000, 1_000_000, 2_000_000, 5_000_000, 10_000_000 ] })
				;
			return provider;
		}

	}
}
