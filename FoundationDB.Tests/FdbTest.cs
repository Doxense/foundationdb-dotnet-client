#region BSD License
/* Copyright (c) 2013-2019, Doxense SAS
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

namespace FoundationDB.Client.Tests
{
	using System;
	using System.Diagnostics;
	using System.Globalization;
	using System.IO;
	using System.Reflection;
	using System.Threading;
	using System.Threading.Tasks;
	using JetBrains.Annotations;
	using NUnit.Framework;

	/// <summary>Base class for all FoundationDB tests</summary>
	public abstract class FdbTest
	{

		private CancellationTokenSource m_cts;
		private CancellationToken m_ct;
		private Stopwatch m_timer;

		protected int OverrideApiVersion = 0;

		[OneTimeSetUp]
		protected void BeforeAllTests()
		{
			// We must ensure that FDB is running before executing the tests
			// => By default, we always use 
			if (Fdb.ApiVersion == 0)
			{
				int version = OverrideApiVersion;
				if (version == 0) version = Fdb.GetDefaultApiVersion();
				if (version > Fdb.GetMaxApiVersion())
				{
					Assume.That(version, Is.LessThanOrEqualTo(Fdb.GetMaxApiVersion()), "Unit tests require that the native fdb client version be at least equal to the current binding version!");
				}
				Fdb.Start(version);
			}
			else if (OverrideApiVersion != 0 && OverrideApiVersion != Fdb.ApiVersion)
			{
				//note: cannot change API version on the fly! :(
				Assume.That(Fdb.ApiVersion, Is.EqualTo(OverrideApiVersion), "The API version selected is not what this test is expecting!");
			}
		}

		[SetUp]
		protected void BeforeEachTest()
		{
			lock (this)
			{
				m_cts = null;
				m_ct = CancellationToken.None;
			}

			//note: some test runners fail with a null-ref in the Test.FullName property ...
			string fullName;
			try
			{
				fullName = TestContext.CurrentContext.Test.FullName;
			}
			catch
			{
				fullName = this.GetType().Name + ".???";
			}
			Trace.WriteLine("=== " + fullName + "() === " + DateTime.Now.TimeOfDay);

			m_timer = Stopwatch.StartNew();
		}

		[TearDown]
		protected void AfterEachTest()
		{
			m_timer.Stop();
			if (m_cts != null)
			{
				try { m_cts.Cancel(); } catch { }
				m_cts.Dispose();
			}
		}

		/// <summary>Time elapsed since the start of the current test</summary>
		protected TimeSpan TestElapsed
		{
			[DebuggerStepThrough]
			get { return m_timer.Elapsed; }
		}

		/// <summary>Cancellation token usable by any test</summary>
		protected CancellationToken Cancellation
		{
			[DebuggerStepThrough]
			get
			{
				if (m_cts == null) SetupCancellation();
				return m_ct;
			}
		}

		private void SetupCancellation()
		{
			lock (this)
			{
				if (m_cts == null)
				{
					m_cts = new CancellationTokenSource();
					m_ct = m_cts.Token;
				}
			}
		}

		/// <summary>Connect to the local test database</summary>
		[DebuggerStepThrough]
		protected Task<IFdbDatabase> OpenTestDatabaseAsync()
		{
			return TestHelpers.OpenTestDatabaseAsync(this.Cancellation);
		}

		/// <summary>Connect to the local test database</summary>
		[DebuggerStepThrough]
		protected Task<IFdbDatabase> OpenTestPartitionAsync()
		{
			return TestHelpers.OpenTestPartitionAsync(this.Cancellation);
		}

		[DebuggerStepThrough]
		protected Task CleanLocation<TSubspace>(IFdbDatabase db, ISubspaceLocation<TSubspace> location)
			where TSubspace : IKeySubspace
		{
			return TestHelpers.CleanLocation(db, location, this.Cancellation);
		}

		[DebuggerStepThrough]
		protected Task CleanSubspace(IFdbDatabase db, IKeySubspace subspace)
		{
			return TestHelpers.CleanSubspace(db, subspace, this.Cancellation);
		}

		[DebuggerStepThrough]
		protected Task DumpSubspace(IFdbDatabase db, IKeySubspace subspace)
		{
			return TestHelpers.DumpSubspace(db, subspace, this.Cancellation);
		}

		[DebuggerStepThrough]
		protected Task DumpSubspace<TSubspace>(IFdbDatabase db, ISubspaceLocation<TSubspace> path)
			where TSubspace : IKeySubspace
		{
			return TestHelpers.DumpLocation(db, path, this.Cancellation);
		}

		[DebuggerStepThrough]
		protected Task DumpSubspace(IFdbReadOnlyTransaction tr, IKeySubspace subspace)
		{
			return TestHelpers.DumpSubspace(tr, subspace);
		}

		[DebuggerStepThrough]
		protected async Task DumpSubspace<TSubspace>(IFdbReadOnlyTransaction tr, ISubspaceLocation<TSubspace> location)
			where TSubspace : IKeySubspace
		{
			var subspace = await location.Resolve(tr);
			await TestHelpers.DumpSubspace(tr, subspace);
		}

		[DebuggerStepThrough]
		protected async Task DeleteSubspace(IFdbDatabase db, IKeySubspace subspace)
		{
			using (var tr = await db.BeginTransactionAsync(this.Cancellation))
			{
				tr.ClearRange(subspace);
				await tr.CommitAsync();
			}
		}

		#region Logging...

		// These methods are just there to help with the problem of culture-aware string formatting

		// Quand on est exécuté depuis VS en mode debug on préfère écrire dans Trace. Sinon, on écrit dans la Console...
		private static readonly bool AttachedToDebugger = Debugger.IsAttached;

		/// <summary>Indique si on fonctionne sous un runner qui préfère utiliser la console pour l'output des logs</summary>
		private static readonly bool MustOutputLogsOnConsole = DetectConsoleTestRunner();

		private static bool DetectConsoleTestRunner()
		{
			// TeamCity
			if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("TEAMCITY_VERSION"))) return true;

			string host = Assembly.GetEntryAssembly()?.GetName().Name;
			return host == "TestDriven.NetCore.AdHoc" // TestDriven.NET
			       || host == "testhost";                // ReSharper Test Runner
		}

		[DebuggerNonUserCode]
		private static void WriteToLog(string message, bool lineBreak = true)
		{
			if (MustOutputLogsOnConsole)
			{ // write to stdout
				if (lineBreak)
					Console.Out.WriteLine(message);
				else
					Console.Out.Write(message);
			}
			else if (AttachedToDebugger)
			{ // write to the VS 'output' tab
				if (lineBreak)
					Trace.WriteLine(message);
				else
					Trace.Write(message);
			}
			else
			{ // write to NUnit's realtime log
				if (lineBreak)
				{
					TestContext.Progress.WriteLine(message);
				}
				else
				{
					TestContext.Progress.Write(message);
				}
			}
		}

		[DebuggerNonUserCode]
		private static void WriteToErrorLog(string message)
		{
			if (MustOutputLogsOnConsole)
			{ // write to stderr
				Console.Error.WriteLine(message);
			}
			else if (AttachedToDebugger)
			{ // write to the VS 'output' tab
				Trace.WriteLine("ERROR: " + message);
				TestContext.Error.WriteLine(message);
			}
			else
			{ // write to NUnit's stderr
				TestContext.Error.WriteLine(message);
			}
		}

		[DebuggerStepThrough]
		public static void Log(string text)
		{
			WriteToLog(text);
		}

		public static void Log<T>(T value)
		{
			switch (value)
			{
				case string s:
					Log(s ?? "<null>");
					break;
				case null:
					Log($"({typeof(T).Name}) <null>");
					break;
				case IFormattable fmt:
					Log(fmt.ToString());
					break;
				default:
					Log($"({value.GetType().Name}) {value}");
					break;
			}
		}

		[DebuggerStepThrough]
		public static void Log()
		{
			WriteToLog(string.Empty);
		}

		[DebuggerNonUserCode]
		[StringFormatMethod("format")]
		public static void Log([NotNull] string format, object arg0)
		{
			WriteToLog(String.Format(CultureInfo.InvariantCulture, format, arg0));
		}

		[DebuggerNonUserCode]
		[StringFormatMethod("format")]
		public static void Log([NotNull] string format, object arg0, object arg1)
		{
			WriteToLog(String.Format(CultureInfo.InvariantCulture, format, arg0, arg1));
		}

		[DebuggerNonUserCode]
		[StringFormatMethod("format")]
		public static void Log([NotNull] string format, params object[] args)
		{
			WriteToLog(String.Format(CultureInfo.InvariantCulture, format, args));
		}

		[DebuggerNonUserCode]
		public static void LogError(string text)
		{
			WriteToErrorLog(text);
		}

		[DebuggerStepThrough]
		public static void Dump(object item)
		{
			if (item == null)
			{
				WriteToLog("null");
			}
			else
			{
				WriteToLog(string.Format(CultureInfo.InvariantCulture, "[{0}] {1}", item.GetType().Name, item));
			}
		}

		#endregion

		protected static Slice Key(string text)
		{
			return Slice.FromStringUtf8(text);
		}

		protected static Slice Value(string text)
		{
			return Slice.FromStringUtf8(text);
		}

	}
}
