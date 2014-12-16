#region BSD Licence
/* Copyright (c) 2013, Doxense SARL
All rights reserved.

Redistribution and use in source and binary forms, with or without
modification, are permitted provided that the following conditions are met:
	* Redistributions of source code must retain the above copyright
	  notice, this list of conditions and the following disclaimer.
	* Redistributions in binary form must reproduce the above copyright
	  notice, this list of conditions and the following disclaimer in the
	  documentation and/or other materials provided with the distribution.
	* Neither the name of Doxense nor the
	  names of its contributors may be used to endorse or promote products
	  derived from this software without specific prior written permission.

THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND
ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
DISCLAIMED. IN NO EVENT SHALL <COPYRIGHT HOLDER> BE LIABLE FOR ANY
DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
(INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
(INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
 */
#endregion

#define TRACE_COUNTING

namespace FoundationDB.Client
{
	using FoundationDB.Client.Utils;
	using JetBrains.Annotations;
	using System;
	using System.Collections.Generic;
	using System.Diagnostics;
	using System.Globalization;
	using System.Threading;
	using System.Threading.Tasks;

	public static partial class Fdb
	{

		/// <summary>Helper class for reading from the reserved System subspace</summary>
		public static class Status
		{

			private static readonly Slice StatusJsonKey = Slice.FromAscii("\xFF\xFF/status/json");

			public static async Task<SystemStatus> GetStatusAsync([NotNull] IFdbReadOnlyTransaction trans)
			{
				if (trans == null) throw new ArgumentNullException("trans");

				Slice data = await trans.GetAsync(StatusJsonKey).ConfigureAwait(false);

				if (data.IsNullOrEmpty) return null;

				string jsonText = data.ToUnicode();

				var doc = TinyJsonParser.ParseObject(jsonText);
				if (doc == null) return null;

				long rv = 0;
				if (doc.ContainsKey("cluster"))
				{
					rv = await trans.GetReadVersionAsync();
				}

				return new SystemStatus(doc, rv, jsonText);
			}

			public static async Task<SystemStatus> GetStatusAsync([NotNull] IFdbDatabase db, CancellationToken ct)
			{
				if (db == null) throw new ArgumentNullException("db");

				// we should not retry the read to the status key!
				using(var trans = db.BeginReadOnlyTransaction(ct))
				{
					trans.WithPrioritySystemImmediate();
					//note: in v3.x, the status key does not need the access to system key option.

					//TODO: set a custom timeout?
					return await GetStatusAsync(trans);
				}
			}

			[DebuggerDisplay("{Name}")]
			public struct Message
			{
				public readonly string Name;
				public readonly string Description;

				internal Message(string name, string description)
				{
					this.Name = name;
					this.Description = description;
				}

				internal static Message From(Dictionary<string, object> data, string field)
				{
					var kvp = TinyJsonParser.GetStringPair(TinyJsonParser.GetMapField(data, field), "name", "description");
					return new Message(kvp.Key ?? String.Empty, kvp.Value ?? String.Empty);
				}

				public override string ToString()
				{
					return String.Format("[{0}] {1}", this.Name, this.Description);
				}

				public override int GetHashCode()
				{
					return StringComparer.Ordinal.GetHashCode(this.Name);
				}

				public override bool Equals(object obj)
				{
					return (obj is Message) && Equals((Message)obj);
				}

				public bool Equals(Message other)
				{
					return string.Equals(this.Name, other.Name, StringComparison.Ordinal)
						&& string.Equals(this.Description, other.Description, StringComparison.Ordinal);
				}
			}

			[DebuggerDisplay("Counter={Counter}, Hz={Hz}, Roughness={Roughness}")]
			public struct LoadCounter
			{
				public readonly long? Counter;
				public readonly double Hz;
				public readonly double? Roughness;

				public LoadCounter(long counter, double hz, double roughness)
				{
					this.Counter = counter;
					this.Hz = hz;
					this.Roughness = roughness;
				}

				internal static LoadCounter From(Dictionary<string, object> data, string field)
				{
					var obj = TinyJsonParser.GetMapField(data, field);
					return new LoadCounter(
						(long)(TinyJsonParser.GetNumberField(obj, "counter") ?? 0),
						TinyJsonParser.GetNumberField(obj, "hz") ?? 0,
						TinyJsonParser.GetNumberField(obj, "roughness") ?? 0
					);
				}

				public override string ToString()
				{
					return String.Format(CultureInfo.InvariantCulture, "Counter={0:N0}, Hz={1:N1}, Roughness={2:N2}", this.Counter, this.Hz, this.Roughness);
				}
			}

			public abstract class MetricsBase
			{
				protected readonly Dictionary<string, object> m_data;

				protected MetricsBase(Dictionary<string, object> data)
				{
					m_data = data;
				}

				protected Dictionary<string, object> GetMap(string field)
				{
					return TinyJsonParser.GetMapField(m_data, field);
				}

				protected List<object> GetArray(string field)
				{
					return TinyJsonParser.GetArrayField(m_data, field);
				}

				protected string GetString(string field)
				{
					return TinyJsonParser.GetStringField(m_data, field);
				}

				protected string GetString(string field1, string field2)
				{
					return TinyJsonParser.GetStringField(TinyJsonParser.GetMapField(m_data, field1), field2);
				}

				protected long? GetInt64(string field)
				{
					var x = TinyJsonParser.GetNumberField(m_data, field);
					return x.HasValue ? (long?)x.Value : null;
				}

				protected long? GetInt64(string field1, string field2)
				{
					var x = TinyJsonParser.GetNumberField(TinyJsonParser.GetMapField(m_data, field1), field2);
					return x.HasValue ? (long?)x.Value : null;
				}

				protected double? GetDouble(string field)
				{
					return TinyJsonParser.GetNumberField(m_data, field);
				}

				protected double? GetDouble(string field1, string field2)
				{
					return TinyJsonParser.GetNumberField(TinyJsonParser.GetMapField(m_data, field1), field2);
				}

				protected bool? GetBoolean(string field)
				{
					return TinyJsonParser.GetBooleanField(m_data, field);
				}

				protected bool? GetBoolean(string field1, string field2)
				{
					return TinyJsonParser.GetBooleanField(TinyJsonParser.GetMapField(m_data, field1), field2);
				}

			}

			public sealed class SystemStatus : MetricsBase
			{
				private readonly ClientStatus m_client;
				private readonly ClusterStatus m_cluster;
				private readonly long m_readVersion;
				private readonly string m_raw;

				internal SystemStatus(Dictionary<string, object> doc, long readVersion, string raw)
					: base(doc)
				{
					m_client = new ClientStatus(TinyJsonParser.GetMapField(doc, "client"));
					m_cluster = new ClusterStatus(TinyJsonParser.GetMapField(doc, "cluster"));
					m_readVersion = readVersion;
					m_raw = raw;
				}

				public ClientStatus Client { get { return m_client; } }

				public ClusterStatus Cluster { get { return m_cluster; } }

				public long ReadVersion { get { return m_readVersion; } }

				public string RawText { get { return m_raw; } }

			}

			public sealed class ClientStatus : MetricsBase
			{
				internal ClientStatus(Dictionary<string, object> data) : base(data) { }

				public string ClusterFilePath
				{
					get
					{
						return GetString("cluster_file", "path");
					}
				}

				public bool ClusterFileUpToDate
				{
					get
					{
						return GetBoolean("cluster_file", "up_to_date") ?? false;
					}
				}

				public Message[] Messages
				{
					[NotNull]
					get
					{
						var array = GetArray("messages");
						var res = new Message[array.Count];
						for (int i = 0; i < res.Length; i++)
						{
							var obj = (Dictionary<string, object>)array[i];
							var kvp = TinyJsonParser.GetStringPair(obj, "name", "description");
							res[i] = new Message(kvp.Key, kvp.Value);
						}
						return res;
					}
				}

				public long Timestamp
				{
					get
					{
						return GetInt64("timestamp") ?? 0;
					}
				}

				public bool DatabaseAvailable
				{
					get { return GetBoolean("database_status", "available") ?? false; }
				}

				public bool DatabaseHealthy
				{
					get { return GetBoolean("database_status", "healthy") ?? false; }
				}

			}

			public sealed class ClusterStatus : MetricsBase
			{

				internal ClusterStatus(Dictionary<string, object> data)
					: base(data)
				{ }

				/// <summary>Unix time of the cluster controller</summary>
				/// <remarks>Number of seconds since the Unix epoch (1970-01-01Z)</remarks>
				public long ClusterControllerTimestamp
				{
					get { return GetInt64("cluster_controller_timestamp") ?? 0; }
				}

				/// <summary>License string of the cluster</summary>
				public string License
				{
					[NotNull]
					get { return GetString("license") ?? String.Empty; }
				}

				/// <summary>List of currently active messages</summary>
				/// <remarks>Includes notifications, warnings, errors, ...</remarks>
				private Message[] m_messages;
				public Message[] Messages
				{
					[NotNull]
					get
					{
						if (m_messages == null)
						{
							var array = GetArray("messages");
							var res = new Message[array.Count];
							for (int i = 0; i < res.Length; i++)
							{
								var obj = (Dictionary<string, object>)array[i];
								var kvp = TinyJsonParser.GetStringPair(obj, "name", "description");
								res[i] = new Message(kvp.Key, kvp.Value);
							}
							m_messages = res;
						}
						return m_messages;
					}
				}

				/// <summary>Recovery state of the cluster</summary>
				public Fdb.Status.Message RecoveryState
				{
					get
					{
						return Fdb.Status.Message.From(m_data, "recovery_state");
					}
				}

				private DataMetrics m_dataMetrics;
				public DataMetrics Data
				{
					get { return m_dataMetrics ?? (m_dataMetrics = new DataMetrics(GetMap("data"))); }
				}

				/// <summary>QoS metrics</summary>
				private QosMetrics m_qos;
				public QosMetrics Qos
				{
					get { return m_qos ?? (m_qos = new QosMetrics(GetMap("qos"))); }
				}

				/// <summary>Workload metrics</summary>
				private WorkloadMetrics m_workload;
				public WorkloadMetrics Workload
				{
					get { return m_workload ?? (m_workload = new WorkloadMetrics(GetMap("workload"))); }
				}

			}

			public sealed class DataMetrics : MetricsBase
			{
				internal DataMetrics(Dictionary<string, object> data) : base(data) { }

				public long AveragePartitionSizeBytes
				{
					get { return GetInt64("average_partition_size_bytes") ?? 0; }
				}

				public long LeastOperatingSpaceBytesLogServer
				{
					get { return GetInt64("least_operating_space_bytes_log_server") ?? 0; }
				}

				public long LeastOperatingSpaceBytesStorageServer
				{
					get { return GetInt64("least_operating_space_bytes_storage_server") ?? 0; }
				}

				public long MovingDataInFlightBytes
				{
					get { return GetInt64("moving_data", "in_flight_bytes") ?? 0; }
				}

				public long MovingDataInQueueBytes
				{
					get { return GetInt64("moving_data", "in_queue_bytes") ?? 0; }
				}

				public long PartitionsCount
				{
					get { return GetInt64("partitions_count") ?? 0; }
				}

				public long TotalDiskUsedBytes
				{
					get { return GetInt64("total_disk_used_bytes") ?? 0; }
				}

				public long TotalKVUsedBytes
				{
					get { return GetInt64("total_kv_size_bytes") ?? 0; }
				}

				public bool StateHealthy
				{
					get { return GetBoolean("state", "healthy") ?? false; }
				}

				public string StateName
				{
					get { return GetString("state", "name"); }
				}

			}

			public sealed class QosMetrics : MetricsBase
			{
				internal QosMetrics(Dictionary<string, object> data) : base(data) { }

				public Fdb.Status.Message PerformanceLimitedBy
				{
					get { return Fdb.Status.Message.From(m_data, "performance_limited_by"); }
				}

				public long WorstQueueBytesLogServer
				{
					get { return GetInt64("worst_queue_bytes_log_server") ?? 0; }
				}

				public long WorstQueueBytesStorageServer
				{
					get { return GetInt64("worst_queue_bytes_storage_server") ?? 0; }
				}

			}

			public sealed class WorkloadMetrics : MetricsBase
			{
				internal WorkloadMetrics(Dictionary<string, object> data) : base(data) { }

				private WorkloadBytesMetrics m_bytes;
				public WorkloadBytesMetrics Bytes
				{
					get { return m_bytes ?? (m_bytes = new WorkloadBytesMetrics(GetMap("bytes"))); }
				}

				private WorkloadOperationsMetrics m_operations;
				public WorkloadOperationsMetrics Operations
				{
					get { return m_operations ?? (m_operations = new WorkloadOperationsMetrics(GetMap("operations"))); }
				}

				private WorkloadTransactionsMetrics m_transactions;
				public WorkloadTransactionsMetrics Transactions
				{
					get { return m_transactions ?? (m_transactions = new WorkloadTransactionsMetrics(GetMap("transactions"))); }
				}
			}

			public sealed class WorkloadBytesMetrics : MetricsBase
			{
				internal WorkloadBytesMetrics(Dictionary<string, object> data)
					: base(data)
				{
					this.Written = LoadCounter.From(data, "written");
				}

				public Fdb.Status.LoadCounter Written { get; private set; }

			}

			public sealed class WorkloadOperationsMetrics : MetricsBase
			{
				internal WorkloadOperationsMetrics(Dictionary<string, object> data)
					: base(data)
				{
					this.Reads = LoadCounter.From(data, "reads");
					this.Writes = LoadCounter.From(data, "writes");
				}

				public Fdb.Status.LoadCounter Reads { get; private set; }

				public Fdb.Status.LoadCounter Writes { get; private set; }
			}

			public sealed class WorkloadTransactionsMetrics : MetricsBase
			{
				internal WorkloadTransactionsMetrics(Dictionary<string, object> data)
					: base(data)
				{
					this.Committed = LoadCounter.From(data, "committed");
					this.Conflicted = LoadCounter.From(data, "conflicted");
					this.Started = LoadCounter.From(data, "started");
				}

				public Fdb.Status.LoadCounter Committed { get; private set; }

				public Fdb.Status.LoadCounter Conflicted { get; private set; }

				public Fdb.Status.LoadCounter Started { get; private set; }
			}

		}

	}

}
