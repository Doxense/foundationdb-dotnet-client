﻿#region BSD Licence
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

namespace FoundationDB.Client.Utils
{
	using System;
	using System.Diagnostics;
	using System.Globalization;
#if !NET_4_0
	using System.Runtime.CompilerServices;
#endif
	using System.Security;
	using System.Threading;

	internal static class Logging
	{
		#region Private Fields...

		private static readonly object s_lock = new object();

		private static bool s_initialized;

		private static bool s_enabled;

		private static bool s_appDomainShutdown;

		private static TraceSource s_traceSource;

		#endregion

		#region Public Members...

		/// <summary>Return true if logging is enabled; otherwise false</summary>
		public static bool On
		{
#if !NET_4_0
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
			get
			{
				if (!s_initialized)
				{
					InitializeLogging();
				}
				return s_enabled;
			}
		}

		/// <summary>Return the TraceSource used for logging</summary>
		public static TraceSource Source
		{
			get
			{
				if (!s_initialized) InitializeLogging();
				return s_enabled ? s_traceSource : null;
			}
		}

		/// <summary>Set/Update the trave level</summary>
		/// <param name="level"></param>
		public static void SetLevel(SourceLevels level)
		{
			lock (s_lock)
			{
				if (s_initialized) Close();
				s_enabled = false;
				if (level != SourceLevels.Off)
				{
					s_traceSource = new TraceSource("FoundationDB.Client", level);
					s_enabled = true;
				}
				s_initialized = true;
			}
		}

		public static bool IsVerbose
		{
#if !NET_4_0
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
			get { return ShouldTrace(TraceEventType.Verbose); }
		}

		public static bool IsInformation
		{
#if !NET_4_0
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
			get { return ShouldTrace(TraceEventType.Information); }
		}

		public static bool IsWarning
		{
#if !NET_4_0
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
			get { return ShouldTrace(TraceEventType.Warning); }
		}

		public static bool IsError
		{
#if !NET_4_0
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
			get { return ShouldTrace(TraceEventType.Error); }
		}

		public static bool IsCritical
		{
#if !NET_4_0
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
			get { return ShouldTrace(TraceEventType.Critical); }
		}

		private static bool ShouldTrace(TraceEventType level)
		{
			if (!s_enabled || s_appDomainShutdown) return false;

			if (!s_initialized) InitializeLogging();

			var source = s_traceSource;
			return source != null && source.Switch.ShouldTrace(level);
		}

		private static void PrintLine(TraceEventType eventType, int id, string msg)
		{
			if (s_traceSource != null)
			{
				s_traceSource.TraceEvent(eventType, id, "[" + Thread.CurrentThread.ManagedThreadId.ToString("d4", CultureInfo.InvariantCulture) + "] " + msg);
			}
		}

		public static void Verbose(string msg)
		{
			if (Logging.IsVerbose)
			{
				PrintLine(TraceEventType.Verbose, 0, msg);
			}
		}

		public static void Verbose(object obj, string method, string msg)
		{
			if (Logging.IsVerbose)
			{
				PrintLine(TraceEventType.Verbose, 0, GetObjectUniqueId(obj, method) + " - " + msg);
			}
		}

		public static void Info(string msg)
		{
			if (Logging.IsInformation)
			{
				PrintLine(TraceEventType.Information, 0, msg);
			}
		}

		public static void Info(object obj, string method, string msg)
		{
			if (Logging.IsInformation)
			{
				PrintLine(TraceEventType.Information, 0, GetObjectUniqueId(obj, method) + " - " + msg);
			}
		}

		public static void Warning(string msg)
		{
			if (Logging.IsWarning)
			{
				PrintLine(TraceEventType.Warning, 0, msg);
			}
		}

		public static void Warning(object obj, string method, string msg)
		{
			if (Logging.IsWarning)
			{
				PrintLine(TraceEventType.Warning, 0, GetObjectUniqueId(obj, method) + " - " + msg);
			}
		}

		public static void Error(string msg)
		{
			if (Logging.IsError)
			{
				PrintLine(TraceEventType.Error, 0, msg);
			}
		}

		public static void Error(object obj, string method, string msg)
		{
			if (Logging.IsError)
			{
				PrintLine(TraceEventType.Error, 0, GetObjectUniqueId(obj, method) + " - " + msg);
			}
		}

		public static void Exception(object obj, string method, Exception e)
		{
			if (Logging.IsError)
			{
				// flatten aggregate exceptions
				if (e is AggregateException) e = (e as AggregateException).Flatten().InnerException;

				string msg = String.Format("Exception in {0} - {1}.", GetObjectUniqueId(obj, method), e.Message);
				if (!string.IsNullOrWhiteSpace(e.StackTrace)) msg += "\r\n" + e.StackTrace;
			}
		}

		#endregion

		#region Internal Helpers...

		private static void InitializeLogging()
		{
			lock (s_lock)
			{
				if (s_initialized) return;

				s_traceSource = new TraceSource("FoundationDB.Client");

				bool shouldTrace = false;
				try
				{
					shouldTrace = s_traceSource.Switch.ShouldTrace(TraceEventType.Critical);
				}
				catch (SecurityException)
				{
					Close();
					shouldTrace = false;
				}

				if (shouldTrace)
				{ // register to cleanup when AppDomain is unloaded
					var appDomain = AppDomain.CurrentDomain;

					appDomain.DomainUnload += AppDomainUnloadEvent;
					appDomain.ProcessExit += ProcessExistEvent;

				}

				s_enabled = shouldTrace;
				s_initialized = true;
			}
		}

		private static void Close()
		{
			if (s_traceSource != null)
			{
				s_traceSource.Close();
			}
		}

		private static void ProcessExistEvent(object sender, EventArgs e)
		{
			Close();
			s_appDomainShutdown = true;
		}

		private static void AppDomainUnloadEvent(object sender, EventArgs e)
		{
			Close();
			s_appDomainShutdown = true;
		}

		private static string GetObjectUniqueId(object obj, string method)
		{
			string suffix = method != null ? ("::" + method + "()") : String.Empty;

			// create a friendly name for this object
			if (obj == null)
			{
				return "(null)" + suffix;
			}

			//TODO: custom name for FdbDatabase, FdbTransaction, ... ?

			var tr = obj as IFdbReadOnlyTransaction;
			if (tr != null)
			{
				return "FdbTransaction#" + tr.Id.ToString(CultureInfo.InvariantCulture) + suffix;
			}

			var db = obj as FdbDatabase;
			if (db != null)
			{
				return "FdbDatabase('" + db.Name + "')" + suffix;
			}

			var type = obj as Type;
			if (type != null)
			{
				return type.Name + suffix;
			}

			return obj.GetType().Name + "#" + obj.GetHashCode().ToString(CultureInfo.InvariantCulture) + suffix;
		}

		#endregion

	}
}
