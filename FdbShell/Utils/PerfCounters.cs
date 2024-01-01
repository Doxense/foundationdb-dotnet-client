#region Copyright (c) 2023-2024 SnowBank SAS, (c) 2005-2023 Doxense SAS
// All rights reserved.
// 
// Redistribution and use in source and binary forms, with or without
// modification, are permitted provided that the following conditions are met:
// 	* Redistributions of source code must retain the above copyright
// 	  notice, this list of conditions and the following disclaimer.
// 	* Redistributions in binary form must reproduce the above copyright
// 	  notice, this list of conditions and the following disclaimer in the
// 	  documentation and/or other materials provided with the distribution.
// 	* Neither the name of SnowBank nor the
// 	  names of its contributors may be used to endorse or promote products
// 	  derived from this software without specific prior written permission.
// 
// THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND
// ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
// WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
// DISCLAIMED. IN NO EVENT SHALL SNOWBANK SAS BE LIABLE FOR ANY
// DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
// (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
// LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
// ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
// (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
// SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
#endregion

namespace FdbShell
{
	using System;
	using System.Diagnostics;

	public static class PerfCounters
	{

		static PerfCounters()
		{
			var p = Process.GetCurrentProcess();
			ProcessName = p.ProcessName;
			ProcessId = p.Id;

			if (OperatingSystem.IsWindows())
			{
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
		}

		public static readonly string ProcessName;
		public static readonly int ProcessId;

		public static readonly PerformanceCounterCategory? CategoryProcess;
		public static readonly PerformanceCounter? ProcessorTime;
		public static readonly PerformanceCounter? UserTime;
		public static readonly PerformanceCounter? PrivateBytes;
		public static readonly PerformanceCounter? VirtualBytes;
		public static readonly PerformanceCounter? VirtualBytesPeak;
		public static readonly PerformanceCounter? WorkingSet;
		public static readonly PerformanceCounter? WorkingSetPeak;
		public static readonly PerformanceCounter? HandleCount;

		public static readonly PerformanceCounterCategory? CategoryNetClrMemory;
		public static readonly PerformanceCounter? ClrBytesInAllHeaps;
		public static readonly PerformanceCounter? ClrTimeInGC;
		public static readonly PerformanceCounter? ClrGen0Collections;
		public static readonly PerformanceCounter? ClrGen1Collections;
		public static readonly PerformanceCounter? ClrGen2Collections;

	}

}
