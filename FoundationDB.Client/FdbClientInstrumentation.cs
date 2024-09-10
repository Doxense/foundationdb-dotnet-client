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
	using System.Diagnostics;
	using System.Diagnostics.Metrics;

	public static class FdbClientInstrumentation
	{

		public const string ActivityName = "FoundationDB.Client";
		public const string ActivityVersion = "0.1.0"; //TODO: which version?
		public const string MeterName = "FoundationDB.Client";
		public const string MeterVersion = "0.1.0"; //TODO: which version?
		public const string HealthCheckName = "FoundationDB.Client";

		internal static readonly ActivitySource ActivitySource = new(ActivityName, ActivityVersion);

		private static Meter Meter { get; } = new(MeterName, MeterVersion); //TODO: version?

		private static UpDownCounter<int> TransactionsExecuting { get; } = Meter.CreateUpDownCounter<int>(
			"db.fdb.client.transactions.executing",
			unit: "{transaction}",
			description: "The number of currently executing fdb transactions."
		);

		private static Histogram<double> TransactionsDuration { get; } = Meter.CreateHistogram<double>(
			"db.fdb.client.transactions.duration",
			unit: "s",
			description: "The duration of fdb operations, in seconds."
		);

		private static Counter<int> OperationsStarted { get; } = Meter.CreateCounter<int>(
			"db.fdb.client.operations.started",
			unit: "{transaction}",
			description: "The number of fdb operations that have been started."
		);

		private static Counter<int> OperationsCompleted { get; } = Meter.CreateCounter<int>(
			"db.fdb.client.operations.completed",
			unit: "{transaction}",
			description: "The number of fdb operations that have been completed."
		);

		private static Histogram<double> OperationsDuration { get; } = Meter.CreateHistogram<double>(
			"db.fdb.client.operations.duration",
			unit: "s",
			description: "The duration of fdb operations, in seconds."
		);

		private static Histogram<int> OperationSize { get; } = Meter.CreateHistogram<int>(
			"db.fdb.client.operations.size",
			unit: "By",
			description: "The estimated size of an fdb operation, in bytes."
		);

		private static Counter<long> ApiCalls { get; } = Meter.CreateCounter<long>(
			"db.fdb.client.api.calls",
			"{calls}",
			description: "The number of keyspace operations that have been performed"
		);

		internal static void ReportTransactionStart(FdbOperationContext context, IFdbTransaction tr)
		{
			TransactionsExecuting.Add(1);
		}

		internal static void ReportTransactionStop(FdbOperationContext context)
		{
			TransactionsExecuting.Add(-1);

			if (TransactionsDuration.Enabled)
			{
				var elapsedTotal = context.ElapsedTotal;
				if (elapsedTotal > TimeSpan.Zero)
				{
					TransactionsDuration.Record(
						elapsedTotal.TotalSeconds,
						new KeyValuePair<string, object?>("operation.retries", context.Retries),
						new KeyValuePair<string, object?>("operation.status", context.PreviousError)
					);
				}
			}
		}

		internal static void ReportOperationStarted(FdbOperationContext context, IFdbTransaction tr)
		{
			if (OperationsStarted.Enabled)
			{
				bool readOnly = (context.Mode & FdbTransactionMode.ReadOnly) != 0;
				if (context.Retries == 0)
				{ // first attempt
					OperationsStarted.Add(
						1,
						new KeyValuePair<string, object?>("operation.readonly", readOnly)
					);
				}
				else
				{ // retry after failure
					OperationsStarted.Add(
						1,
						new KeyValuePair<string, object?>("operation.readonly", readOnly),
						new KeyValuePair<string, object?>("operation.previous", context.PreviousError)
					);
				}
			}
		}

		internal static void ReportOperationCompleted(FdbOperationContext context, IFdbTransaction? trans, FdbError error = FdbError.Success)
		{
			if (OperationsDuration.Enabled)
			{
				var elapsed = context.Elapsed;
				if (elapsed > TimeSpan.Zero)
				{
					OperationsDuration.Record(
						elapsed.TotalSeconds,
						new KeyValuePair<string, object?>("operation.retries", context.Retries),
						new KeyValuePair<string, object?>("operation.status", error)
					);
				}
			}

			OperationsCompleted.Add(
				1,
				new KeyValuePair<string, object?>("operation.retries", context.Retries),
				new KeyValuePair<string, object?>("operation.status", error)
			);

			if (error != FdbError.Success && trans != null && OperationSize.Enabled)
			{ // we need to record the size for failed attempts
				int size = trans.Size;
				if (size > 0)
				{
					OperationSize.Record(
						size,
						new KeyValuePair<string, object?>("operation.retries", context.Retries),
						new KeyValuePair<string, object?>("operation.status", FdbError.Success)
					);
				}
			}

		}

		internal static void ReportOperationCommitted(IFdbTransaction trans, FdbOperationContext context)
		{
			if (OperationsCompleted.Enabled)
			{
				OperationsCompleted.Add(
					1,
					new KeyValuePair<string, object?>("operation.retries", context.Retries),
					new KeyValuePair<string, object?>("operation.status", FdbError.Success)
				);
			}

			if (OperationSize.Enabled)
			{
				int size = trans.Size;
				if (size > 0)
				{
					OperationSize.Record(
						size,
						new KeyValuePair<string, object?>("operation.retries", context.Retries),
						new KeyValuePair<string, object?>("operation.status", FdbError.Success)
					);
				}
			}
		}

		private static readonly KeyValuePair<string, object?> CachedCallGet = new("operation.type", "get");
		private static readonly KeyValuePair<string, object?> CachedCallGetKey = new("operation.type", "get_key");
		private static readonly KeyValuePair<string, object?> CachedCallGetRange = new("operation.type", "get_range");
		private static readonly KeyValuePair<string, object?> CachedCallSet = new("operation.type", "set");
		private static readonly KeyValuePair<string, object?> CachedCallClear = new("operation.type", "clear");
		private static readonly KeyValuePair<string, object?> CachedCallClearRange = new("operation.type", "clear_range");
		private static readonly KeyValuePair<string, object?> CachedCallAtomic = new("operation.type", "atomic");

		internal static void ReportGet(FdbTransaction trans, int count = 1)
		{
			if (trans.Tracing.HasFlag(FdbTracingOptions.RecordApiCalls))
			{
				ApiCalls.Add(count, CachedCallGet);
			}
		}

		internal static void ReportGetKey(FdbTransaction trans, int count = 1)
		{
			if (trans.Tracing.HasFlag(FdbTracingOptions.RecordApiCalls))
			{
				ApiCalls.Add(count, CachedCallGetKey);
			}
		}

		internal static void ReportGetRange(FdbTransaction trans)
		{
			if (trans.Tracing.HasFlag(FdbTracingOptions.RecordApiCalls))
			{
				ApiCalls.Add(1, CachedCallGetRange);
			}
		}

		internal static void ReportSet(FdbTransaction trans)
		{
			if (trans.Tracing.HasFlag(FdbTracingOptions.RecordApiCalls))
			{
				ApiCalls.Add(1, CachedCallSet);
			}
		}

		internal static void ReportClear(FdbTransaction trans)
		{
			if (trans.Tracing.HasFlag(FdbTracingOptions.RecordApiCalls))
			{
				ApiCalls.Add(1, CachedCallClear);
			}
		}

		internal static void ReportClearRange(FdbTransaction trans)
		{
			if (trans.Tracing.HasFlag(FdbTracingOptions.RecordApiCalls))
			{
				ApiCalls.Add(1, CachedCallClearRange);
			}
		}

		internal static void ReportAtomicOp(FdbTransaction trans, FdbMutationType op)
		{
			if (trans.Tracing.HasFlag(FdbTracingOptions.RecordApiCalls))
			{
				ApiCalls.Add(1, CachedCallAtomic);
			}
		}

	}

}
