#region BSD License
/* Copyright (c) 2013-2020, Doxense SAS
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

// ReSharper disable UnusedMember.Global
namespace FoundationDB.Client.Status
{
	using System;
	using System.Collections.Generic;
	using System.Diagnostics;
	using System.Globalization;
	using FoundationDB.Client.Utils;
	using JetBrains.Annotations;

	/// <summary>Snapshot of the state of a FoundationDB cluster</summary>
	[PublicAPI]
	public sealed class FdbSystemStatus : MetricsBase
	{
		internal FdbSystemStatus(Dictionary<string, object?>? doc, long readVersion, string raw)
			: base(doc)
		{
			this.Client = new ClientStatus(TinyJsonParser.GetMapField(doc, "client"));
			this.Cluster = new ClusterStatus(TinyJsonParser.GetMapField(doc, "cluster"));
			this.ReadVersion = readVersion;
			this.RawText = raw;
		}

		/// <summary>Details about the local Client</summary>
		public ClientStatus Client { get; }

		/// <summary>Details about the remote Cluster</summary>
		public ClusterStatus Cluster { get; }

		/// <summary>Read Version of the snapshot</summary>
		public long ReadVersion { get; }

		/// <summary>Raw JSON text of this snapshot.</summary>
		/// <remarks>This is the same value that is returned by running 'status json' in fdbcli</remarks>
		public string RawText { get; }
	}

	#region Common...

	/// <summary>Details about a notification, alert or error, as reported by a component of a FoundationDB cluster</summary>
	[DebuggerDisplay("{Name}")]
	public readonly struct Message
	{
		/// <summary>Code for this message</summary>
		public readonly string Name;

		/// <summary>User friendly description of this message</summary>
		public readonly string Description;

		internal Message(string name, string description)
		{
			this.Name = name;
			this.Description = description;
		}

		internal static Message From(Dictionary<string, object?>? data, string field)
		{
			(var key, var value) = TinyJsonParser.GetStringPair(TinyJsonParser.GetMapField(data, field), "name", "description");
			return new Message(key ?? string.Empty, value ?? string.Empty);
		}

		internal static Message[] FromArray(Dictionary<string, object?>? data, string field)
		{
			var array = TinyJsonParser.GetArrayField(data, field);
			var res = new Message[array.Count];
			for (int i = 0; i < res.Length; i++)
			{
				var obj = (Dictionary<string, object?>?) array[i];
				(var key, var value) = TinyJsonParser.GetStringPair(obj, "name", "description");
				res[i] = new Message(key!, value!);
			}
			return res;
		}

		public override string ToString()
		{
			return $"[{this.Name}] {this.Description}";
		}

		public override int GetHashCode()
		{
			return StringComparer.Ordinal.GetHashCode(this.Name);
		}

		public override bool Equals(object obj)
		{
			return obj is Message message && Equals(message);
		}

		public bool Equals(Message other)
		{
			return string.Equals(this.Name, other.Name, StringComparison.Ordinal)
				&& string.Equals(this.Description, other.Description, StringComparison.Ordinal);
		}
	}

	/// <summary>Measured quantity that changes over time</summary>
	[DebuggerDisplay("Hz={Hz}")]
	public class RateCounter : MetricsBase
	{

		internal RateCounter(Dictionary<string, object?>? data)
			: base(data)
		{
			this.Hz = GetDouble("hz") ?? 0;
		}

		/// <summary>Rate of change, per seconds, since the last snapshot ("UNIT per seconds")</summary>
		public double Hz { get; }

		public override string ToString()
		{
			return string.Format(CultureInfo.InvariantCulture, "Hz={0:N1}", this.Hz);
		}
	}

	[DebuggerDisplay("Counter={Counter}, Hz={Hz}")]
	public class LoadCounter : RateCounter
	{
		internal LoadCounter(Dictionary<string, object?>? data) : base(data)
		{
			this.Counter = GetInt64("counter") ?? 0;
		}

		/// <summary>Absolute value, since the start (ex: "UNIT")</summary>
		public long Counter { get; }

		public override string ToString()
		{
			return string.Format(CultureInfo.InvariantCulture, "Counter={0:N0}, Hz={1:N1}", this.Counter, this.Hz);
		}

	}

	/// <summary>Measured quantity that changes over time</summary>
	[DebuggerDisplay("Counter={Counter}, Hz={Hz}, Roughness={Roughness}")]
	public sealed class RoughnessCounter : LoadCounter
	{

		internal RoughnessCounter(Dictionary<string, object?>? data) : base(data)
		{
			this.Roughness = GetDouble("roughness") ?? 0;
		}

		public double Roughness { get; }

		public override string ToString()
		{
			return string.Format(CultureInfo.InvariantCulture, "Counter={0:N0}, Hz={1:N1}, Roughness={2:N2}", this.Counter, this.Hz, this.Roughness);
		}
	}

	/// <summary>Measured quantity that changes over time</summary>
	[DebuggerDisplay("Counter={Counter}, Hz={Hz}, Sectors={Sectors}")]
	public sealed class DiskCounter : LoadCounter
	{

		internal DiskCounter(Dictionary<string, object?>? data) : base(data)
		{
			this.Sectors = GetInt64("sectors") ?? 0;
		}
		public long Sectors { get; }

		public override string ToString()
		{
			return string.Format(CultureInfo.InvariantCulture, "Counter={0:N0}, Hz={1:N1}, Sectors={2:N0}", this.Counter, this.Hz, this.Sectors);
		}
	}

	[DebuggerDisplay("Seconds={Seconds}, Versions={Versions}")]
	public sealed class LagCounter : MetricsBase
	{
		internal LagCounter(Dictionary<string, object?>? data) : base(data)
		{
			this.Seconds = GetDouble("seconds") ?? 0;
			this.Versions = GetInt64("versions") ?? 0;
		}

		public double Seconds { get; }

		public long Versions { get; }

		public override string ToString()
		{
			return string.Format(CultureInfo.InvariantCulture, "Seconds={0:N3}, Versions={1:N0}", this.Seconds, this.Versions);
		}
	}

	/// <summary>Base class for all metrics containers</summary>
	public abstract class MetricsBase
	{
		protected readonly Dictionary<string, object?>? m_data;

		protected MetricsBase(Dictionary<string, object?>? data)
		{
			m_data = data;
		}

		protected Dictionary<string, object?>? GetMap(string field)
		{
			return TinyJsonParser.GetMapField(m_data, field);
		}

		protected List<object?>? GetArray(string field)
		{
			return TinyJsonParser.GetArrayField(m_data, field);
		}

		protected string? GetString(string field)
		{
			return TinyJsonParser.GetStringField(m_data, field);
		}

		protected string? GetString(string field1, string field2)
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

	#endregion

	#region Client...

	/// <summary>Description of the current status of the local FoundationDB client</summary>
	public sealed class ClientStatus : MetricsBase
	{
		internal ClientStatus(Dictionary<string, object?>? data) : base(data) { }

		private Message[]? m_messages;

		/// <summary>Path to the '.cluster' file used by the client to connect to the cluster</summary>
		public string? ClusterFilePath => GetString("cluster_file", "path");

		/// <summary>Indicates if the content of the '.cluster' file is up to date with the current topology of the cluster</summary>
		public bool ClusterFileUpToDate => GetBoolean("cluster_file", "up_to_date") ?? false;

		/// <summary>Liste of active messages for the client</summary>
		/// <remarks>The most common client messages are listed in <see cref="ClientMessages"/>.</remarks>
		public Message[] Messages => m_messages ??= Message.FromArray(m_data, "messages");

		/// <summary>Timestamp of the local client (unix time)</summary>
		/// <remarks>Number of seconds since 1970-01-01Z, using the local system clock</remarks>
		public long Timestamp => GetInt64("timestamp") ?? 0;

		/// <summary>Local system time on the client</summary>
		public DateTime SystemTime => new DateTime(checked(621355968000000000L + this.Timestamp * TimeSpan.TicksPerSecond), DateTimeKind.Utc);

		/// <summary>Specifies if the local client was able to connect to the cluster</summary>
		public bool DatabaseAvailable => GetBoolean("database_status", "available") ?? false;

		/// <summary>Specifies if the database is currently healthy</summary>
		//REVIEW: what does it mean if available=true, but healthy=false ?
		public bool DatabaseHealthy => GetBoolean("database_status", "healthy") ?? false;
	}

	/// <summary>List of well known client messages</summary>
	public static class ClientMessages
	{
		public const string InconsistentClusterFile = "inconsistent_cluster_file";
		public const string NoClusterController = "no_cluster_controller";
		public const string QuorumNotReachable = "quorum_not_reachable";
		public const string StatusIncompleteClient = "status_incomplete_client";
		public const string StatusIncompleteCluster = "status_incomplete_cluster";
		public const string StatusIncompleteCoordinators = "status_incomplete_coordinators";
		public const string StatusIncompleteError = "status_incomplete_error";
		public const string StatusIncompleteTimeout = "status_incomplete_timeout ";
		public const string UnreachableClusterController = "unreachable_cluster_controller";
	}

	#endregion

	#region Cluster...

	/// <summary>Description of the current status of a FoundationDB cluster</summary>
	public sealed class ClusterStatus : MetricsBase
	{

		internal ClusterStatus(Dictionary<string, object?>? data)
			: base(data)
		{ }

		private Message[]? m_messages;
		private ClusterConfiguration? m_configuration;
		private DataMetrics? m_dataMetrics;
		private LatencyMetrics? m_latency;
		private QosMetrics? m_qos;
		private WorkloadMetrics? m_workload;
		private ClusterClientsMetrics? m_clients;
		private Dictionary<string, ProcessStatus>? m_processes;
		private Dictionary<string, MachineStatus>? m_machines;

		/// <summary>Unix time of the cluster controller</summary>
		/// <remarks>Number of seconds since the Unix epoch (1970-01-01Z)</remarks>
		public long ClusterControllerTimestamp => GetInt64("cluster_controller_timestamp") ?? 0;

		public string ConnectionString => GetString("connection_string") ?? string.Empty;

		public bool DatabaseAvailable => GetBoolean("database_available") ?? false;

		public bool DatabaseLocked => GetBoolean("database_locked") ?? false;

		public bool FullReplication => GetBoolean("full_replication") ?? false;

		public long Generation => GetInt64("generation") ?? 0;

		/// <summary>License string of the cluster</summary>
		public string License => GetString("license") ?? String.Empty;

		/// <summary>List of currently active messages</summary>
		/// <remarks>Includes notifications, warnings, errors, ...</remarks>
		public Message[] Messages => m_messages ??= Message.FromArray(m_data, "messages");

		/// <summary>Recovery state of the cluster</summary>
		public Message RecoveryState => Message.From(m_data, "recovery_state");

		public ClusterConfiguration Configuration => m_configuration ??= new ClusterConfiguration(GetMap("configuration"));

		public DataMetrics Data => m_dataMetrics ??= new DataMetrics(GetMap("data"));

		public LatencyMetrics Latency => m_latency ??= new LatencyMetrics(GetMap("latency_probe"));

		/// <summary>QoS metrics</summary>
		public QosMetrics Qos => m_qos ??= new QosMetrics(GetMap("qos"));

		/// <summary>Workload metrics</summary>
		public WorkloadMetrics Workload => m_workload ??= new WorkloadMetrics(GetMap("workload"));

		public ClusterClientsMetrics Clients => m_clients ??= new ClusterClientsMetrics(GetMap("clients"));

		/// <summary>List of the processes that are currently active in the cluster</summary>
		public IReadOnlyDictionary<string, ProcessStatus> Processes
		{
			get
			{
				if (m_processes == null)
				{
					var obj = GetMap("processes");
					var procs = new Dictionary<string, ProcessStatus>(obj?.Count ?? 0, StringComparer.OrdinalIgnoreCase);
					if (obj != null)
					{
						//REVIEW: are ids case sensitive?
						foreach (var kvp in obj)
						{
							var item = (Dictionary<string, object?>?) kvp.Value;
							procs[kvp.Key] = new ProcessStatus(item, kvp.Key);
						}
					}

					m_processes = procs;
				}
				return m_processes;
			}
		}

		/// <summary>List of the machines that are currently active in the cluster</summary>
		public IReadOnlyDictionary<string, MachineStatus> Machines
		{
			get
			{
				if (m_machines == null)
				{
					var obj = GetMap("machines");
					var machines = new Dictionary<string, MachineStatus>(obj?.Count ?? 0, StringComparer.OrdinalIgnoreCase);
					if (obj != null)
					{
						//REVIEW: are ids case sensitive?
						foreach (var kvp in obj)
						{
							var item = (Dictionary<string, object?>?) kvp.Value;
							machines[kvp.Key] = new MachineStatus(item, kvp.Key);
						}
					}
					m_machines = machines;
				}
				return m_machines;
			}
		}

	}

	/// <summary>List of well known cluster messages</summary>
	public static class ClusterMessages
	{
		public const string UnreachableClusterController = "unreachable_cluster_controller";
		public const string ClientIssues = "client_issues";
		public const string CommitTimeout = "commit_timeout";
		public const string ReadTimeout = "read_timeout";
		public const string StatusIncomplete = "status_incomplete";
		public const string StorageServersError = "storage_servers_error";
		public const string TransactionStartTimeout = "transaction_start_timeout";
		public const string UnreachableMasterWorker = "unreachable_master_worker";
		public const string UnreachableProcesses = "unreachable_processes";
		public const string UnreadableConfiguration = "unreadable_configuration";
	}

	public sealed class ClusterConfiguration : MetricsBase
	{
		internal ClusterConfiguration(Dictionary<string, object?>? data) : base(data)
		{
			this.CoordinatorsCount = (int)(GetInt64("coordinators_count") ?? 0);
			this.StorageEngine = GetString("storage_engine") ?? string.Empty;
			this.RedundancyFactor = GetString("redundancy", "factor") ?? string.Empty;
		}

		private string[]? m_excludedServers;

		public int CoordinatorsCount { get; }

		public string StorageEngine { get; }

		public string RedundancyFactor { get; }

		public IReadOnlyList<string> ExcludedServers
		{
			get
			{
				if (m_excludedServers == null)
				{
					var arr = GetArray("excluded_servers");
					var res = arr?.Count > 0 ? new string[arr.Count] : Array.Empty<string>();
					if (arr != null)
					{
						for (int i = 0; i < res.Length; i++)
						{
							var obj = (Dictionary<string, object?>?) arr[i];
							res[i] = TinyJsonParser.GetStringField(obj, "address") ?? string.Empty;
						}
					}
					m_excludedServers = res;
				}
				return m_excludedServers;
			}
		}
	}

	public sealed class LatencyMetrics : MetricsBase
	{
		internal LatencyMetrics(Dictionary<string, object?>? data) : base(data)
		{
			//REVIEW: TimeSpans?
			this.CommitSeconds = GetDouble("commit_seconds") ?? 0;
			this.ReadSeconds = GetDouble("read_seconds") ?? 0;
			this.TransactionStartSeconds = GetDouble("transaction_start_seconds") ?? 0;
		}

		public double CommitSeconds { get; }

		public double ReadSeconds { get; set; }

		public double TransactionStartSeconds { get; }
	}

	/// <summary>Details about the volume of data stored in the cluster</summary>
	public sealed class DataMetrics : MetricsBase
	{
		internal DataMetrics(Dictionary<string, object?>? data) : base(data) { }

		public long AveragePartitionSizeBytes => GetInt64("average_partition_size_bytes") ?? 0;

		public long LeastOperatingSpaceBytesLogServer => GetInt64("least_operating_space_bytes_log_server") ?? 0;

		public long LeastOperatingSpaceBytesStorageServer => GetInt64("least_operating_space_bytes_storage_server") ?? 0;

		public long MovingDataInFlightBytes => GetInt64("moving_data", "in_flight_bytes") ?? 0;

		public long MovingDataInQueueBytes => GetInt64("moving_data", "in_queue_bytes") ?? 0;

		public long PartitionsCount => GetInt64("partitions_count") ?? 0;

		public long TotalDiskUsedBytes => GetInt64("total_disk_used_bytes") ?? 0;

		public long TotalKVUsedBytes => GetInt64("total_kv_size_bytes") ?? 0;

		public bool StateHealthy => GetBoolean("state", "healthy") ?? false;

		public string StateName => GetString("state", "name")!;
	}

	/// <summary>Details about the quality of service offered by the cluster</summary>
	public sealed class QosMetrics : MetricsBase
	{
		internal QosMetrics(Dictionary<string, object?>? data) : base(data) { }

		/// <summary>Current limiting factor for the performance of the cluster</summary>
		public Message PerformanceLimitedBy => Message.From(m_data, "performance_limited_by");

		//REVIEW: what is this?
		public long WorstQueueBytesLogServer => GetInt64("worst_queue_bytes_log_server") ?? 0;

		//REVIEW: what is this?
		public long WorstQueueBytesStorageServer => GetInt64("worst_queue_bytes_storage_server") ?? 0;
	}

	/// <summary>Details about the current wokrload of the cluster</summary>
	public sealed class WorkloadMetrics : MetricsBase
	{
		internal WorkloadMetrics(Dictionary<string, object?>? data) : base(data) { }

		private WorkloadBytesMetrics? m_bytes;
		private WorkloadOperationsMetrics? m_operations;
		private WorkloadTransactionsMetrics? m_transactions;

		/// <summary>Performance counters for the volume of data processed by the database</summary>
		public WorkloadBytesMetrics Bytes => m_bytes ??= new WorkloadBytesMetrics(GetMap("bytes"));

		/// <summary>Performance counters for the operations on the keys in the database</summary>
		public WorkloadOperationsMetrics Operations => m_operations ??= new WorkloadOperationsMetrics(GetMap("operations"));

		/// <summary>Performance counters for the transactions.</summary>
		public WorkloadTransactionsMetrics Transactions => m_transactions ??= new WorkloadTransactionsMetrics(GetMap("transactions"));
	}

	/// <summary>Throughput of a FoundationDB cluster</summary>
	public sealed class WorkloadBytesMetrics : MetricsBase
	{
		internal WorkloadBytesMetrics(Dictionary<string, object?>? data) : base(data)
		{
			this.Written = new RoughnessCounter(GetMap("written"));
		}

		/// <summary>Bytes written</summary>
		//REVIEW: this looks like the size of writes in transactions, NOT the number of bytes written to the disk!
		public RoughnessCounter Written { get; }

	}

	/// <summary>Operations workload of a FoundationDB cluster</summary>
	public sealed class WorkloadOperationsMetrics : MetricsBase
	{
		internal WorkloadOperationsMetrics(Dictionary<string, object?>? data) : base(data)
		{
			this.Reads = new RoughnessCounter(GetMap("reads"));
			this.Writes = new RoughnessCounter(GetMap("writes"));
			this.ReadRequests = new RoughnessCounter(GetMap("read_requests"));
		}

		/// <summary>Details about read operations</summary>
		public RoughnessCounter Reads { get; }

		/// <summary>Details about write operations</summary>
		public RoughnessCounter Writes { get; }

		public RoughnessCounter ReadRequests { get; }
	}

	/// <summary>Transaction workload of a FoundationDB cluster</summary>
	public sealed class WorkloadTransactionsMetrics : MetricsBase
	{
		internal WorkloadTransactionsMetrics(Dictionary<string, object?>? data) : base(data)
		{
			this.Committed = new RoughnessCounter(GetMap("committed"));
			this.Conflicted = new RoughnessCounter(GetMap("conflicted"));
			this.Started = new RoughnessCounter(GetMap("started"));
		}

		public RoughnessCounter Committed { get; }

		public RoughnessCounter Conflicted { get; }

		public RoughnessCounter Started { get; }
	}

	public sealed class ClusterClientsMetrics : MetricsBase
	{
		internal ClusterClientsMetrics(Dictionary<string, object?>? data) : base(data)
		{
			this.Count = (int) (GetInt64("count") ?? 0);
		}

		public int Count { get; }

		//TODO: "supported_versions"
	}

	#endregion

	#region Processes...

	/// <summary>Details about a FoundationDB process</summary>
	public sealed class ProcessStatus : MetricsBase
	{

		internal ProcessStatus(Dictionary<string, object?>? data, string id) : base(data)
		{
			this.Id = id;
		}

		private string? m_machineId;
		private string? m_address;
		private Message[]? m_messages;
		private ProcessNetworkMetrics? m_network;
		private ProcessCpuMetrics? m_cpu;
		private ProcessDiskMetrics? m_disk;
		private ProcessMemoryMetrics? m_memory;
		private LocalityConfiguration? m_locality;
		private ProcessRoleMetrics[]? m_roles;

		/// <summary>Unique identifier for this process.</summary>
		//TODO: is it stable across reboots? what are the conditions for a process to change its ID ?
		public string Id { get; }

		/// <summary>Identifier of the machine that is hosting this process</summary>
		/// <remarks>All processes that have the same MachineId are running on the same (physical) machine.</remarks>
		public string MachineId => m_machineId ??= GetString("machine_id") ?? string.Empty;

		/// <summary>Version of this process</summary>
		/// <example>"3.0.4"</example>
		public string Version => GetString("version") ?? string.Empty;

		public TimeSpan Uptime => TimeSpan.FromSeconds(GetDouble("uptime_seconds") ?? 0);

		/// <summary>Address and port of this process, with syntax "IP_ADDRESS:port"</summary>
		/// <example>"10.1.2.34:4500"</example>
		public string Address => m_address ??= GetString("address") ?? string.Empty;

		public string ClassSource => GetString("class_source") ?? string.Empty;

		public string ClassType => GetString("class_type") ?? string.Empty;

		/// <summary>Command line that was used to start this process</summary>
		public string CommandLine => GetString("command_line") ?? string.Empty;

		/// <summary>If true, this process is currently excluded from the cluster</summary>
		public bool Excluded => GetBoolean("excluded") ?? false;

		public string FaultDomain => GetString("fault_domain") ?? string.Empty;

		/// <summary>List of messages that are currently published by this process</summary>
		public Message[] Messages => m_messages ??= Message.FromArray(m_data, "messages");

		/// <summary>Network performance counters</summary>
		public ProcessNetworkMetrics Network => m_network ??= new ProcessNetworkMetrics(GetMap("network"));

		/// <summary>CPU performance counters</summary>
		public ProcessCpuMetrics Cpu => m_cpu ??= new ProcessCpuMetrics(GetMap("cpu"));

		/// <summary>Disk performance counters</summary>
		public ProcessDiskMetrics Disk => m_disk ??= new ProcessDiskMetrics(GetMap("disk"));

		/// <summary>Memory performance counters</summary>
		public ProcessMemoryMetrics Memory => m_memory ??= new ProcessMemoryMetrics(GetMap("memory"));

		public LocalityConfiguration Locality => m_locality ??= new LocalityConfiguration(GetMap("locality"));

		/// <summary>List of the roles assumed by this process</summary>
		/// <remarks>The key is the unique role ID in the cluster, and the value is the type of the role itself</remarks>
		public ProcessRoleMetrics[] Roles
		{
			get
			{
				if (m_roles == null)
				{
					//REVIEW: should we have (K=id, V=role) or (K=role, V=id) ?

					var arr = GetArray("roles");
					var res = arr?.Count > 0 ? new ProcessRoleMetrics[arr.Count] : Array.Empty<ProcessRoleMetrics>();
					if (arr != null)
					{
						for (int i = 0; i < res.Length; i++)
						{
							res[i] = ProcessRoleMetrics.Create((Dictionary<string, object?>?) arr[i]);
						}
					}
					m_roles = res;
				}
				return m_roles;
			}
		}

	}

	public class ProcessRoleMetrics : MetricsBase
	{
		internal ProcessRoleMetrics(Dictionary<string, object?>? data, string role) : base(data)
		{
			this.Role = role;
			this.Id = GetString("id") ?? string.Empty;
		}

		public string Id { get; }

		public string Role { get; }

		//TODO: values will vary depending on the "Role" !

		public static ProcessRoleMetrics Create(Dictionary<string, object?>? data)
		{
			string? role = TinyJsonParser.GetStringField(data, "role");
			switch (role)
			{
				case null:
					return null!; //invalid!
				case "master":
					return new MasterRoleMetrics(data);
				case "proxy":
					return new ProxyRoleMetrics(data);
				case "resolver":
					return new ResolverRoleMetrics(data);
				case "cluster_controller":
					return new ClusterControllerRoleMetrics(data);
				case "log":
					return new LogRoleMetrics(data);
				case "storage":
					return new StorageRoleMetrics(data);
				default:
					return new ProcessRoleMetrics(data, role);
			}
		}
	}

	public sealed class ProxyRoleMetrics : ProcessRoleMetrics
	{
		public ProxyRoleMetrics(Dictionary<string, object?>? data) : base(data, "proxy")
		{ }
	}

	public sealed class MasterRoleMetrics : ProcessRoleMetrics
	{
		public MasterRoleMetrics(Dictionary<string, object?>? data) : base(data, "master")
		{ }
	}

	public sealed class ResolverRoleMetrics : ProcessRoleMetrics
	{
		public ResolverRoleMetrics(Dictionary<string, object?>? data) : base(data, "resolver")
		{ }
	}

	public sealed class ClusterControllerRoleMetrics : ProcessRoleMetrics
	{
		public ClusterControllerRoleMetrics(Dictionary<string, object?>? data) : base(data, "cluster_controller")
		{ }
	}

	public abstract class DiskBasedRoleMetrics : ProcessRoleMetrics
	{
		protected DiskBasedRoleMetrics(Dictionary<string, object?>? data, string role) : base(data, role)
		{
			this.DurableBytes = new RoughnessCounter(GetMap("durable_bytes"));
			this.InputBytes = new RoughnessCounter(GetMap("input_bytes"));
		}

		public long DataVersion => GetInt64("data_version") ?? 0;

		public RoughnessCounter DurableBytes { get; }

		public RoughnessCounter InputBytes { get; }

		public long KVStoreAvailableBytes => GetInt64("kvstore_available_bytes") ?? 0;

		public long KVStoreFreeBytes => GetInt64("kvstore_free_bytes") ?? 0;

		public long KVStoreTotalBytes => GetInt64("kvstore_total_bytes") ?? 0;

		public long KVStoreUsedBytes => GetInt64("kvstore_used_bytes") ?? 0;

	}

	public sealed class StorageRoleMetrics : DiskBasedRoleMetrics
	{
		internal StorageRoleMetrics(Dictionary<string, object?>? data) : base(data, "storage")
		{
			this.BytesQueried = new RoughnessCounter(GetMap("bytes_queried"));
			this.FinishedQueries = new RoughnessCounter(GetMap("finished_queries"));
			this.KeysQueried = new RoughnessCounter(GetMap("keys_queried"));
			this.MutationBytes = new RoughnessCounter(GetMap("mutation_bytes"));
			this.Mutations = new RoughnessCounter(GetMap("mutations"));
			this.TotalQueries = new RoughnessCounter(GetMap("total_queries"));
			this.DataLag = new LagCounter(GetMap("data_lag"));
			this.DurabilityLag = new LagCounter(GetMap("durability_lag"));
		}

		public int QueryQueueMax => (int) (GetInt64("query_queue_max") ?? 0);

		public long StoredBytes => GetInt64("stored_bytes") ?? 0;

		public RoughnessCounter BytesQueried { get; }

		public RoughnessCounter FinishedQueries { get; }

		public RoughnessCounter KeysQueried { get; }

		public RoughnessCounter MutationBytes { get; }

		public RoughnessCounter Mutations { get; }

		public RoughnessCounter TotalQueries { get; }

		public LagCounter DataLag { get; }

		public long DurableVersion => GetInt64("durable_version") ?? 0;

		public LagCounter DurabilityLag { get; }

	}

	public sealed class LogRoleMetrics : DiskBasedRoleMetrics
	{
		internal LogRoleMetrics(Dictionary<string, object?>? data) : base(data, "log")
		{ }

		public long QueueDiskAvailableBytes => GetInt64("queue_disk_available_bytes") ?? 0;

		public long QueueDiskFreeBytes => GetInt64("queue_disk_free_bytes") ?? 0;

		public long QueueDiskTotalBytes => GetInt64("queue_disk_total_bytes") ?? 0;

		public long QueueDiskUsedBytes => GetInt64("queue_disk_used_bytes") ?? 0;

	}

	/// <summary>List of well known process messages</summary>
	public static class ProcessMessages
	{
		public const string FileOpenError = "file_open_error";
		public const string UnableToWriteClusterFile = "unable_to_write_cluster_file";
		public const string IoError = "io_error";
		public const string PlatformError = "platform_error";
		public const string ProcessError = "process_error";
	}

	/// <summary>Memory performance counters for a FoundationDB process</summary>
	public sealed class ProcessMemoryMetrics : MetricsBase
	{
		internal ProcessMemoryMetrics(Dictionary<string, object?>? data) : base(data)
		{ }

		public long AvailableBytes => GetInt64("available_bytes") ?? 0;

		/// <summary>Memory allocated by the process</summary>
		public long UsedBytes => GetInt64("used_bytes") ?? 0;
		// On linux, this is the VmSize as reported by /proc/[pid]/statm
		// On Window, this is PPROCESS_MEMORY_COUNTERS.PagefileUsage

		/// <summary>Memory that has been allocated but is currently unused</summary>
		public long UnusedAllocatedMemory => GetInt64("unused_allocated_memory") ?? 0;
		//note: this is currently the sum of all unused memory in the various slab allocators

		public long LimitBytes => GetInt64("limit_bytes") ?? 0;

	}

	/// <summary>CPU performance counters for a FoundationDB process</summary>
	public sealed class ProcessCpuMetrics : MetricsBase
	{
		internal ProcessCpuMetrics(Dictionary<string, object?>? data) : base(data)
		{ }

		public double UsageCores => GetDouble("usage_cores") ?? 0;
	}

	/// <summary>Disk performance counters for a FoundationDB process</summary>
	[DebuggerDisplay("Busy={Busy}, Free={FreeBytes}/{TotalBytes}, Reads={Reads.Hz}, Writes={Writes.Hz}")]
	public sealed class ProcessDiskMetrics : MetricsBase
	{

		internal ProcessDiskMetrics(Dictionary<string, object?>? data) : base(data)
		{
			this.Busy = GetDouble("busy") ?? 0;
			this.FreeBytes = GetInt64("free_bytes") ?? 0;
			this.TotalBytes = GetInt64("total_bytes") ?? 0;
			this.Reads = new DiskCounter(GetMap("reads"));
			this.Writes = new DiskCounter(GetMap("writes"));
		}

		public double Busy { get; }

		public long FreeBytes { get; }

		public long TotalBytes { get; }

		public DiskCounter Reads { get; }

		public DiskCounter Writes { get; }

	}

	/// <summary>Network performance counters for a FoundationDB process or machine</summary>
	public sealed class ProcessNetworkMetrics : MetricsBase
	{
		internal ProcessNetworkMetrics(Dictionary<string, object?>? data) : base(data)
		{
			this.CurrentConnections = (int) (GetInt64("current_connections") ?? 0);
			this.MegabitsReceived = new RateCounter(GetMap("megabits_received"));
			this.MegabitsSent = new RateCounter(GetMap("megabits_sent"));
			this.ConnectionErrors = new RateCounter(GetMap("connection_errors"));
			this.ConnectionsClosed = new RateCounter(GetMap("connections_closed"));
			this.ConnectionsEstablished = new RateCounter(GetMap("connections_established"));
		}

		public RateCounter MegabitsReceived { get; }

		public RateCounter MegabitsSent { get; }

		public int CurrentConnections { get; }

		public RateCounter ConnectionErrors { get; }

		public RateCounter ConnectionsClosed { get; }

		public RateCounter ConnectionsEstablished { get; }

	}

	#endregion

	#region Machines...

	public sealed class MachineStatus : MetricsBase
	{

		internal MachineStatus(Dictionary<string, object?>? data, string id) : base(data)
		{
			this.Id = id;
		}

		private string? m_address;
		private MachineNetworkMetrics? m_network;
		private MachineCpuMetrics? m_cpu;
		private MachineMemoryMetrics? m_memory;
		private LocalityConfiguration? m_locality;

		/// <summary>Unique identifier for this machine.</summary>
		//TODO: is it stable across reboots? what are the conditions for a process to change its ID ?
		public string Id { get; }

		/// <summary>Identifier of the data center that is hosting this machine</summary>
		/// <remarks>All machines that have the same DataCenterId are probably running on the same (physical) network.</remarks>
		public string DataCenterId => GetString("datacenter_id") ?? string.Empty;

		/// <summary>Address of this machine</summary>
		/// <example>"10.1.2.34"</example>
		public string Address => m_address ??= GetString("address") ?? string.Empty;

		/// <summary>If true, this process is currently excluded from the cluster</summary>
		public bool Excluded => GetBoolean("excluded") ?? false;

		/// <summary>Network performance counters</summary>
		public MachineNetworkMetrics Network => m_network ??= new MachineNetworkMetrics(GetMap("network"));

		/// <summary>CPU performance counters</summary>
		public MachineCpuMetrics Cpu => m_cpu ??= new MachineCpuMetrics(GetMap("cpu"));

		/// <summary>Memory performance counters</summary>
		public MachineMemoryMetrics Memory => m_memory ??= new MachineMemoryMetrics(GetMap("memory"));

		public LocalityConfiguration Locality => m_locality ??= new LocalityConfiguration(GetMap("locality"));

		public int ContributingWorkers => (int) (GetInt64("contributing_workers") ?? 0);
	}

	/// <summary>Memory performance counters for machine hosting one or more FoundationDB processes</summary>
	public sealed class MachineMemoryMetrics : MetricsBase
	{
		internal MachineMemoryMetrics(Dictionary<string, object?>? data) : base(data)
		{
			this.CommittedBytes = GetInt64("committed_bytes") ?? 0;
			this.FreeBytes = GetInt64("free_bytes") ?? 0;
			this.TotalBytes = GetInt64("total_bytes") ?? 0;
		}

		public long CommittedBytes { get; }

		public long FreeBytes { get; }

		public long TotalBytes { get; }

	}

	/// <summary>CPU performance counters for machine hosting one or more FoundationDB processes</summary>
	public sealed class MachineCpuMetrics : MetricsBase
	{
		internal MachineCpuMetrics(Dictionary<string, object?>? data) : base(data)
		{
			this.LogicalCoreUtilization = GetDouble("logical_core_utilization") ?? 0;
		}

		public double LogicalCoreUtilization { get; }

	}

	/// <summary>Network performance counters for machine hosting one or more FoundationDB processes</summary>
	public sealed class MachineNetworkMetrics : MetricsBase
	{
		internal MachineNetworkMetrics(Dictionary<string, object?>? data) : base(data)
		{
			this.MegabitsReceived = new RateCounter(GetMap("megabits_received"));
			this.MegabitsSent = new RateCounter(GetMap("megabits_sent"));
			this.TcpSegmentsRetransmitted = new RateCounter(GetMap("tcp_segments_retransmitted"));
		}

		public RateCounter MegabitsReceived { get; }

		public RateCounter MegabitsSent { get; }

		public RateCounter TcpSegmentsRetransmitted { get; }

	}

	public sealed class LocalityConfiguration : MetricsBase
	{
		internal LocalityConfiguration(Dictionary<string, object?>? data) : base(data)
		{
			this.MachineId = GetString("machineid") ?? string.Empty;
			this.ProcessId = GetString("processid") ?? string.Empty;
			this.ZoneId = GetString("zoneid") ?? string.Empty;
		}

		public string MachineId { get; }

		public string ProcessId { get; }

		public string ZoneId { get; }

	}


	#endregion

}
