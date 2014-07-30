using FoundationDB.Async;
using FoundationDB.Client;
using FoundationDB.Filters.Logging;
using FoundationDB.Layers.Directories;
using FoundationDB.Layers.Tuples;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace FdbShell
{

	public static class PerfCounters
	{

		static PerfCounters()
		{
			var p = Process.GetCurrentProcess();
			ProcessName = p.ProcessName;
			ProcessId = p.Id;

			CategoryProcess = new PerformanceCounterCategory("Process");

			ProcessorTime = new PerformanceCounter("Process", "% Processor Time", ProcessName);
			UserTime = new PerformanceCounter("Process", "% User Time", ProcessName);

			PrivateBytes = new PerformanceCounter("Process", "Private Bytes", ProcessName);
			VirtualBytes = new PerformanceCounter("Process", "Virtual Bytes", ProcessName);
			VirtualBytesPeak = new PerformanceCounter("Process", "Virtual Bytes Peak", ProcessName);
			WorkingSet = new PerformanceCounter("Process", "Working Set", ProcessName);
			WorkingSetPeak = new PerformanceCounter("Process", "Working Set Peak", ProcessName);
			HandleCount = new PerformanceCounter("Process", "Handle Count", ProcessName);

			CategoryNetClrMemory = new PerformanceCounterCategory(".NET CLR Memory");
			ClrBytesInAllHeaps = new PerformanceCounter(".NET CLR Memory", "# Bytes in all Heaps", ProcessName);
			ClrTimeInGC = new PerformanceCounter(".NET CLR Memory", "% Time in GC", ProcessName);
			ClrGen0Collections = new PerformanceCounter(".NET CLR Memory", "# Gen 0 Collections", p.ProcessName, true);
			ClrGen1Collections = new PerformanceCounter(".NET CLR Memory", "# Gen 1 Collections", p.ProcessName, true);
			ClrGen2Collections = new PerformanceCounter(".NET CLR Memory", "# Gen 1 Collections", p.ProcessName, true);
		}

		public static readonly string ProcessName;
		public static readonly int ProcessId;

		public static readonly PerformanceCounterCategory CategoryProcess;
		public static readonly PerformanceCounter ProcessorTime;
		public static readonly PerformanceCounter UserTime;
		public static readonly PerformanceCounter PrivateBytes;
		public static readonly PerformanceCounter VirtualBytes;
		public static readonly PerformanceCounter VirtualBytesPeak;
		public static readonly PerformanceCounter WorkingSet;
		public static readonly PerformanceCounter WorkingSetPeak;
		public static readonly PerformanceCounter HandleCount;

		public static readonly PerformanceCounterCategory CategoryNetClrMemory;
		public static readonly PerformanceCounter ClrBytesInAllHeaps;
		public static readonly PerformanceCounter ClrTimeInGC;
		public static readonly PerformanceCounter ClrGen0Collections;
		public static readonly PerformanceCounter ClrGen1Collections;
		public static readonly PerformanceCounter ClrGen2Collections;

	}

}
