#region Copyright (c) 2023-2024 SnowBank SAS
//
// All rights are reserved. Reproduction or transmission in whole or in part, in
// any form or by any means, electronic, mechanical or otherwise, is prohibited
// without the prior written consent of the copyright owner.
//
#endregion

namespace FoundationDB.Client
{
	using System;
	using System.Collections.Generic;
	using System.Diagnostics.Metrics;

	public static class FdbMetricsReporter
	{

		private const string METER_NAME = "FdbClient";
		private const string VERSION = "0.1.0"; //TODO: which version?

		private static Meter Meter { get; } = new(METER_NAME, VERSION); //TODO: version?

		static FdbMetricsReporter()
		{

		}

		private static UpDownCounter<int> TransactionsExecuting { get; } = Meter.CreateUpDownCounter<int>(
			"db.fdb.client.operations.executing",
			unit: "{transaction}",
			description: "The number of currently executing fdb transactions."
		);

		private static Counter<int> OperationsStarted { get; } = Meter.CreateCounter<int>(
			"db.fdb.client.operations.started",
			unit: "{transaction}",
			description: "The number of fdb operations that have been attempted."
		);

		private static Counter<int> OperationsSucceeded { get; } = Meter.CreateCounter<int>(
			"db.fdb.client.operations.succeeded", //REVIEW: TODO: what name for this?
			unit: "{operation}",
			description: "The number of fdb operation attempts that have completed successfully."
		);

		private static Counter<int> OperationsCommitted { get; } = Meter.CreateCounter<int>(
			"db.fdb.client.operations.committed", //REVIEW: TODO: what name for this?
			unit: "{operation}",
			description: "The number of fdb operation attempts that have committed changes to the database."
		);

		private static Counter<int> OperationsFailed { get; } = Meter.CreateCounter<int>(
			"db.fdb.client.operations.failed",
			unit: "{operation}",
			description: "The number of fdb operation attempts which have failed."
		);

		private static Counter<int> OperationsConflicted { get; } = Meter.CreateCounter<int>(
			"db.fdb.client.operations.conflicted", //REVIEW: TODO: what name for this?
			unit: "{operation}",
			description: "The number of fdb operation attempts which resulted in a conflict."
		);

		private static Histogram<double> OperationDuration { get; } = Meter.CreateHistogram<double>(
			"db.client.operation.duration",
			unit: "s",
			description: "The duration of fdb transactions, in seconds."
		);

		private static Histogram<int> OperationSize { get; } = Meter.CreateHistogram<int>(
			"db.client.operation.size",
			unit: "By",
			description: "The estimated size of an fdb operation, in bytes."
		);

		internal static void ReportTransactionStart(IFdbTransaction tr)
		{
			TransactionsExecuting.Add(1);
		}

		internal static void ReportTransactionStop(IFdbTransaction tr)
		{
			TransactionsExecuting.Add(-1);
		}

		internal static void ReportOperationStarted(FdbOperationContext context)
		{
			OperationsStarted.Add(1, new KeyValuePair<string, object?>("operation.type", (context.Mode & FdbTransactionMode.ReadOnly) != 0 ? "read-only" : "read-write"));
		}

		internal static void ReportOperationDuration(FdbOperationContext context)
		{
			if (OperationDuration.Enabled)
			{
				var elapsed = context.ElapsedTotal;
				if (elapsed > TimeSpan.Zero)
				{
					OperationDuration.Record(elapsed.TotalSeconds, new KeyValuePair<string, object?>("operation.retries", context.Retries));
				}
			}
		}

		internal static void ReportOperationSuccess(FdbOperationContext context)
		{
			OperationsSucceeded.Add(1, new KeyValuePair<string, object?>("operation.retries", context.Retries));
		}

		internal static void ReportOperationCommitted(IFdbTransaction trans, FdbOperationContext context)
		{
			OperationsCommitted.Add(1);
			if (OperationSize.Enabled)
			{
				OperationSize.Record(trans.Size);
			}
		}

		internal static void ReportOperationFailed(IFdbTransaction trans, FdbOperationContext context, FdbError error)
		{
			OperationsFailed.Add(1, new KeyValuePair<string, object?>("error.type", error.ToString()));
			if (error == FdbError.NotCommitted)
			{
				OperationsConflicted.Add(1);
			}
			if (OperationSize.Enabled)
			{
				OperationSize.Record(trans.Size);
			}
		}

	}

}
