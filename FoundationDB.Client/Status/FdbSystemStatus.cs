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

namespace FoundationDB.Client.Status
{
	using FoundationDB.Client.Status;
	using FoundationDB.Client.Utils;
	using JetBrains.Annotations;
	using System;
	using System.Collections.Generic;
	using System.Diagnostics;
	using System.Globalization;
	using System.Threading;
	using System.Threading.Tasks;

	/// <summary>Snapshot of the state of a FoundationDB cluster</summary>
	public sealed class FdbSystemStatus : MetricsBase
	{
		private readonly ClientStatus m_client;
		private readonly ClusterStatus m_cluster;
		private readonly long m_readVersion;
		private readonly string m_raw;

		internal FdbSystemStatus(Dictionary<string, object> doc, long readVersion, string raw)
			: base(doc)
		{
			m_client = new ClientStatus(TinyJsonParser.GetMapField(doc, "client"));
			m_cluster = new ClusterStatus(TinyJsonParser.GetMapField(doc, "cluster"));
			m_readVersion = readVersion;
			m_raw = raw;
		}

		/// <summary>Details about the local Client</summary>
		public ClientStatus Client { get { return m_client; } }

		/// <summary>Details about the remote Cluster</summary>
		public ClusterStatus Cluster { get { return m_cluster; } }

		/// <summary>Read Version of the snapshot</summary>
		public long ReadVersion { get { return m_readVersion; } }

		/// <summary>Raw JSON text of this snapshot.</summary>
		/// <remarks>This is the same value that is returned by running 'status json' in fdbcli</remarks>
		public string RawText { get { return m_raw; } }

	}

	#region Common...

	/// <summary>Details about a notification, alert or error, as reported by a component of a FoundationDB cluster</summary>
	[DebuggerDisplay("{Name}")]
	public struct Message
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

		internal static Message From(Dictionary<string, object> data, string field)
		{
			var kvp = TinyJsonParser.GetStringPair(TinyJsonParser.GetMapField(data, field), "name", "description");
			return new Message(kvp.Key ?? String.Empty, kvp.Value ?? String.Empty);
		}

		internal static Message[] FromArray(Dictionary<string, object> data, string field)
		{
			var array = TinyJsonParser.GetArrayField(data, field);
			var res = new Message[array.Count];
			for (int i = 0; i < res.Length; i++)
			{
				var obj = (Dictionary<string, object>)array[i];
				var kvp = TinyJsonParser.GetStringPair(obj, "name", "description");
				res[i] = new Message(kvp.Key, kvp.Value);
			}
			return res;
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

	/// <summary>Measured quantity that changes over time</summary>
	[DebuggerDisplay("Counter={Counter}, Hz={Hz}, Roughness={Roughness}")]
	public struct LoadCounter
	{
		/// <summary>Absolute value, since the start (ex: "UNIT")</summary>
		public readonly long Counter;
		/// <summary>Rate of change, per seconds, since the last snapshot ("UNIT per seconds")</summary>
		public readonly double Hz;
		//REVIEW: what is this ?
		public readonly double Roughness;

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

	/// <summary>Base class for all metrics containers</summary>
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

	#endregion

	#region Client...

	/// <summary>Description of the current status of the local FoundationDB client</summary>
	public sealed class ClientStatus : MetricsBase
	{
		internal ClientStatus(Dictionary<string, object> data) : base(data) { }

		private Message[] m_messages;

		/// <summary>Path to the '.cluster' file used by the client to connect to the cluster</summary>
		public string ClusterFilePath
		{
			get { return GetString("cluster_file", "path"); }
		}

		/// <summary>Indicates if the content of the '.cluster' file is up to date with the current topology of the cluster</summary>
		public bool ClusterFileUpToDate
		{
			get { return GetBoolean("cluster_file", "up_to_date") ?? false; }
		}

		/// <summary>Liste of active messages for the client</summary>
		/// <remarks>The most common client messages are listed in <see cref="ClientMessages"/>.</remarks>
		public Message[] Messages
		{
			[NotNull]
			get { return m_messages ?? (m_messages = Message.FromArray(m_data, "messages")); }
		}

		/// <summary>Timestamp of the local client (unix time)</summary>
		/// <remarks>Number of seconds since 1970-01-01Z, using the local system clock</remarks>
		public long Timestamp
		{
			get { return GetInt64("timestamp") ?? 0; }
		}

		/// <summary>Local system time on the client</summary>
		public DateTime SystemTime
		{
			get { return new DateTime(checked(621355968000000000L + this.Timestamp * TimeSpan.TicksPerSecond), DateTimeKind.Utc); }
		}

		/// <summary>Specifies if the local client was able to connect to the cluster</summary>
		public bool DatabaseAvailable
		{
			get { return GetBoolean("database_status", "available") ?? false; }
		}

		/// <summary>Specifies if the database is currently healthy</summary>
		//REVIEW: what does it mean if available=true, but healthy=false ?
		public bool DatabaseHealthy
		{
			get { return GetBoolean("database_status", "healthy") ?? false; }
		}

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

		internal ClusterStatus(Dictionary<string, object> data)
			: base(data)
		{ }

		private Message[] m_messages;
		private DataMetrics m_dataMetrics;
		private QosMetrics m_qos;
		private WorkloadMetrics m_workload;
		private Dictionary<string, ProcessStatus> m_processes;
		private Dictionary<string, MachineStatus> m_machines;

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
		public Message[] Messages
		{
			[NotNull]
			get { return m_messages ?? (m_messages = Message.FromArray(m_data, "messages")); }
		}

		/// <summary>Recovery state of the cluster</summary>
		public Message RecoveryState
		{
			get { return Message.From(m_data, "recovery_state"); }
		}

		public DataMetrics Data
		{
			get { return m_dataMetrics ?? (m_dataMetrics = new DataMetrics(GetMap("data"))); }
		}

		/// <summary>QoS metrics</summary>
		public QosMetrics Qos
		{
			get { return m_qos ?? (m_qos = new QosMetrics(GetMap("qos"))); }
		}

		/// <summary>Workload metrics</summary>
		public WorkloadMetrics Workload
		{
			get { return m_workload ?? (m_workload = new WorkloadMetrics(GetMap("workload"))); }
		}

		/// <summary>List of the processes that are currently active in the cluster</summary>
		public IReadOnlyDictionary<string, ProcessStatus> Processes
		{
			get
			{
				if (m_processes == null)
				{
					var obj = GetMap("processes");
					var procs = new Dictionary<string, ProcessStatus>(obj.Count, StringComparer.OrdinalIgnoreCase);
					//REVIEW: are ids case sensitive?
					foreach (var kvp in obj)
					{
						var item = (Dictionary<string, object>)kvp.Value;
						procs[kvp.Key] = new ProcessStatus(item, kvp.Key);
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
					var machines = new Dictionary<string, MachineStatus>(obj.Count, StringComparer.OrdinalIgnoreCase);
					//REVIEW: are ids case sensitive?
					foreach(var kvp in obj)
					{
						var item = (Dictionary<string, object>)kvp.Value;
						machines[kvp.Key] = new MachineStatus(item, kvp.Key);
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

	/// <summary>Details about the volume of data stored in the cluster</summary>
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

	/// <summary>Details about the quality of service offered by the cluster</summary>
	public sealed class QosMetrics : MetricsBase
	{
		internal QosMetrics(Dictionary<string, object> data) : base(data) { }

		/// <summary>Current limiting factor for the performance of the cluster</summary>
		public Message PerformanceLimitedBy
		{
			get { return Message.From(m_data, "performance_limited_by"); }
		}

		//REVIEW: what is this?
		public long WorstQueueBytesLogServer
		{
			get { return GetInt64("worst_queue_bytes_log_server") ?? 0; }
		}

		//REVIEW: what is this?
		public long WorstQueueBytesStorageServer
		{
			get { return GetInt64("worst_queue_bytes_storage_server") ?? 0; }
		}

	}

	/// <summary>Details about the current wokrload of the cluster</summary>
	public sealed class WorkloadMetrics : MetricsBase
	{
		internal WorkloadMetrics(Dictionary<string, object> data) : base(data) { }

		private WorkloadBytesMetrics m_bytes;
		private WorkloadOperationsMetrics m_operations;
		private WorkloadTransactionsMetrics m_transactions;

		/// <summary>Performance counters for the volume of data processed by the database</summary>
		public WorkloadBytesMetrics Bytes
		{
			get { return m_bytes ?? (m_bytes = new WorkloadBytesMetrics(GetMap("bytes"))); }
		}

		/// <summary>Performance counters for the operations on the keys in the database</summary>
		public WorkloadOperationsMetrics Operations
		{
			get { return m_operations ?? (m_operations = new WorkloadOperationsMetrics(GetMap("operations"))); }
		}

		/// <summary>Performance counters for the transactions.</summary>
		public WorkloadTransactionsMetrics Transactions
		{
			get { return m_transactions ?? (m_transactions = new WorkloadTransactionsMetrics(GetMap("transactions"))); }
		}
	}

	/// <summary>Throughput of a FoundationDB cluster</summary>
	public sealed class WorkloadBytesMetrics : MetricsBase
	{
		internal WorkloadBytesMetrics(Dictionary<string, object> data)
			: base(data)
		{
			this.Written = LoadCounter.From(data, "written");
		}

		/// <summary>Bytes written</summary>
		//REVIEW: this looks like the size of writes in transactions, NOT the number of bytes written to the disk!
		public LoadCounter Written { get; private set; }

	}

	/// <summary>Operations workload of a FoundationDB cluster</summary>
	public sealed class WorkloadOperationsMetrics : MetricsBase
	{
		internal WorkloadOperationsMetrics(Dictionary<string, object> data)
			: base(data)
		{
			this.Reads = LoadCounter.From(data, "reads");
			this.Writes = LoadCounter.From(data, "writes");
		}

		/// <summary>Details about read operations</summary>
		public LoadCounter Reads { get; private set; }

		/// <summary>Details about write operations</summary>
		public LoadCounter Writes { get; private set; }
	}

	/// <summary>Transaction workload of a FoundationDB cluster</summary>
	public sealed class WorkloadTransactionsMetrics : MetricsBase
	{
		internal WorkloadTransactionsMetrics(Dictionary<string, object> data)
			: base(data)
		{
			this.Committed = LoadCounter.From(data, "committed");
			this.Conflicted = LoadCounter.From(data, "conflicted");
			this.Started = LoadCounter.From(data, "started");
		}

		public LoadCounter Committed { get; private set; }

		public LoadCounter Conflicted { get; private set; }

		public LoadCounter Started { get; private set; }
	}

	#endregion

	#region Processes...

	/// <summary>Details about a FoundationDB process</summary>
	public sealed class ProcessStatus : MetricsBase
	{

		internal ProcessStatus(Dictionary<string, object> data, string id)
			: base(data)
		{
			this.Id = id;
		}

		private string m_machineId;
		private string m_address;
		private Message[] m_messages;
		private ProcessNetworkMetrics m_network;
		private ProcessCpuMetrics m_cpu;
		private ProcessDiskMetrics m_disk;
		private ProcessMemoryMetrics m_memory;
		private KeyValuePair<string, string>[] m_roles;

		/// <summary>Unique identifier for this process.</summary>
		//TODO: is it stable accross reboots? what are the conditions for a process to change its ID ?
		public string Id { [NotNull] get; private set; }

		/// <summary>Identifier of the machine that is hosting this process</summary>
		/// <remarks>All processes that have the same MachineId are running on the same (physical) machine.</remarks>
		public string MachineId
		{
			[NotNull]
			get { return m_machineId ?? (m_machineId = GetString("machine_id") ?? String.Empty); }
		}

		/// <summary>Version of this process</summary>
		/// <example>"3.0.4"</example>
		public string Version
		{
			[NotNull]
			get { return GetString("version") ?? String.Empty; }
		}

		/// <summary>Address and port of this process, with syntax "IP_ADDRESS:port"</summary>
		/// <example>"10.1.2.34:4500"</example>
		public string Address
		{
			[NotNull]
			get { return m_address ?? (m_address = GetString("address") ?? String.Empty); }
		}

		/// <summary>Command line that was used to start this process</summary>
		public string CommandLine
		{
			[NotNull]
			get { return GetString("command_line") ?? String.Empty; }
		}

		/// <summary>If true, this process is currently excluded from the cluster</summary>
		public bool Excluded
		{
			get { return GetBoolean("excluded") ?? false; }
		}

		/// <summary>List of messages that are currently published by this process</summary>
		public Message[] Messages
		{
			[NotNull]
			get { return m_messages ?? (m_messages = Message.FromArray(m_data, "messages")); }
		}

		/// <summary>Network performance counters</summary>
		public ProcessNetworkMetrics Network
		{
			get { return m_network ?? (m_network = new ProcessNetworkMetrics(GetMap("network"))); }
		}

		/// <summary>CPU performance counters</summary>
		public ProcessCpuMetrics Cpu
		{
			get { return m_cpu ?? (m_cpu = new ProcessCpuMetrics(GetMap("cpu"))); }
		}

		/// <summary>Disk performance counters</summary>
		public ProcessDiskMetrics Disk
		{
			get { return m_disk ?? (m_disk = new ProcessDiskMetrics(GetMap("disk"))); }
		}

		/// <summary>Memory performance counters</summary>
		public ProcessMemoryMetrics Memory
		{
			get { return m_memory ?? (m_memory = new ProcessMemoryMetrics(GetMap("memory"))); }
		}

		/// <summary>List of the roles assumed by this process</summary>
		/// <remarks>The key is the unique role ID in the cluster, and the value is the type of the role itself</remarks>
		public KeyValuePair<string, string>[] Roles
		{
			get
			{
				if (m_roles == null)
				{
					//REVIEW: should we have (K=id, V=role) or (K=role, V=id) ?

					var arr = GetArray("roles");
					var res = new KeyValuePair<string, string>[arr.Count];
					for (int i = 0; i < res.Length; i++)
					{
						var obj = (Dictionary<string, object>)arr[i];
						res[i] = TinyJsonParser.GetStringPair(obj, "id", "role");
					}
					m_roles = res;
				}
				return m_roles;
			}
		}

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

	/// <summary>Memory performane counters for a FoundationDB process</summary>
	public sealed class ProcessMemoryMetrics : MetricsBase
	{
		internal ProcessMemoryMetrics(Dictionary<string, object> data)
			: base(data)
		{ }

		public long AvailableBytes
		{
			get { return GetInt64("available_bytes") ?? 0; }
		}

		public long UsedBytes
		{
			get { return GetInt64("used_bytes") ?? 0; }
		}

	}

	/// <summary>CPU performane counters for a FoundationDB process</summary>
	public sealed class ProcessCpuMetrics : MetricsBase
	{
		internal ProcessCpuMetrics(Dictionary<string, object> data)
			: base(data)
		{ }

		public double UsageCores
		{
			get { return GetDouble("usage_cores") ?? 0; }
		}

	}

	/// <summary>Disk performane counters for a FoundationDB process</summary>
	public sealed class ProcessDiskMetrics : MetricsBase
	{
		internal ProcessDiskMetrics(Dictionary<string, object> data)
			: base(data)
		{ }

		public double Busy
		{
			get { return GetDouble("busy") ?? 0; }
		}
	}

	/// <summary>Network performane counters for a FoundationDB process or machine</summary>
	public sealed class ProcessNetworkMetrics : MetricsBase
	{
		internal ProcessNetworkMetrics(Dictionary<string, object> data)
			: base(data)
		{
			this.MegabitsReceived = LoadCounter.From(data, "megabits_received");
			this.MegabitsSent = LoadCounter.From(data, "megabits_sent");
		}

		public LoadCounter MegabitsReceived { get; private set; }

		public LoadCounter MegabitsSent { get; private set; }

	}

	#endregion

	#region Machines...

	public sealed class MachineStatus : MetricsBase
	{

		internal MachineStatus(Dictionary<string, object> data, string id)
			: base(data)
		{
			this.Id = id;
		}

		private string m_address;
		private MachineNetworkMetrics m_network;
		private MachineCpuMetrics m_cpu;
		private MachineMemoryMetrics m_memory;

		/// <summary>Unique identifier for this machine.</summary>
		//TODO: is it stable accross reboots? what are the conditions for a process to change its ID ?
		public string Id { [NotNull] get; private set; }

		/// <summary>Identifier of the data center that is hosting this machine</summary>
		/// <remarks>All machines that have the same DataCenterId are probably running on the same (physical) network.</remarks>
		public string DataCenterId
		{
			[NotNull]
			get { return GetString("datacenter_id") ?? String.Empty; }
		}

		/// <summary>Address of this machine</summary>
		/// <example>"10.1.2.34"</example>
		public string Address
		{
			[NotNull]
			get { return m_address ?? (m_address = GetString("address") ?? String.Empty); }
		}

		/// <summary>If true, this process is currently excluded from the cluster</summary>
		public bool Excluded
		{
			get { return GetBoolean("excluded") ?? false; }
		}

		/// <summary>Network performance counters</summary>
		public MachineNetworkMetrics Network
		{
			get { return m_network ?? (m_network = new MachineNetworkMetrics(GetMap("network"))); }
		}

		/// <summary>CPU performance counters</summary>
		public MachineCpuMetrics Cpu
		{
			get { return m_cpu ?? (m_cpu = new MachineCpuMetrics(GetMap("cpu"))); }
		}

		/// <summary>Memory performance counters</summary>
		public MachineMemoryMetrics Memory
		{
			get { return m_memory ?? (m_memory = new MachineMemoryMetrics(GetMap("memory"))); }
		}
	}

	/// <summary>Memory performane counters for machine hosting one or more FoundationDB processes</summary>
	public sealed class MachineMemoryMetrics : MetricsBase
	{
		internal MachineMemoryMetrics(Dictionary<string, object> data)
			: base(data)
		{
			this.CommittedBytes = GetInt64("committed_bytes") ?? 0;
			this.FreeBytes = GetInt64("free_bytes") ?? 0;
			this.TotalBytes = GetInt64("total_bytes") ?? 0;
		}

		public long CommittedBytes { get; private set; }

		public long FreeBytes { get; private set; }

		public long TotalBytes { get; private set; }

	}

	/// <summary>CPU performane counters for machine hosting one or more FoundationDB processes</summary>
	public sealed class MachineCpuMetrics : MetricsBase
	{
		internal MachineCpuMetrics(Dictionary<string, object> data)
			: base(data)
		{
			this.LogicalCoreUtilization = GetDouble("logical_core_utilization") ?? 0;
		}

		public double LogicalCoreUtilization { get; private set; }

	}

	/// <summary>Network performane counters for machine hosting one or more FoundationDB processes</summary>
	public sealed class MachineNetworkMetrics : MetricsBase
	{
		internal MachineNetworkMetrics(Dictionary<string, object> data)
			: base(data)
		{
			this.MegabitsReceived = LoadCounter.From(data, "megabits_received");
			this.MegabitsSent = LoadCounter.From(data, "megabits_sent");
			this.TcpSegmentsRetransmitted = LoadCounter.From(data, "tcp_segments_retransmitted");
		}

		public LoadCounter MegabitsReceived { get; private set; }

		public LoadCounter MegabitsSent { get; private set; }

		public LoadCounter TcpSegmentsRetransmitted { get; private set; }

	}


	#endregion

}