#region BSD Licence
/* Copyright (c) 2013-2018, Doxense SAS
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
	using System.Threading;
	using System.Threading.Tasks;
	using FoundationDB.Layers.Directories;
	using NUnit.Framework;

	/// <summary>Base class for all FoundationDB tests</summary>
	public abstract class FdbTest
	{

		private CancellationTokenSource m_cts;
		private CancellationToken m_ct;
		private Stopwatch m_timer;

		[SetUp]
		protected void BeforeEachTest()
		{
			lock (this)
			{
				m_cts = null;
				m_ct = CancellationToken.None;
			}

			//note: some test runners fail with a nulref in the Test.FullName property ...
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
		protected Task<FdbDirectorySubspace> GetCleanDirectory(IFdbDatabase db, params string[] path)
		{
			return TestHelpers.GetCleanDirectory(db, path, this.Cancellation);
		}

		[DebuggerStepThrough]
		protected Task DumpSubspace(IFdbDatabase db, IKeySubspace subspace)
		{
			return TestHelpers.DumpSubspace(db, subspace, this.Cancellation);
		}

		[DebuggerStepThrough]
		protected async Task DeleteSubspace(IFdbDatabase db, IKeySubspace subspace)
		{
			using (var tr = db.BeginTransaction(this.Cancellation))
			{
				tr.ClearRange(subspace);
				await tr.CommitAsync();
			}
		}

		#region Logging...

		// These methods are just there to help with the problem of culture-aware string formatting

		[DebuggerStepThrough]
		protected static void Log(string text)
		{
			Console.WriteLine(text);
		}

		[DebuggerStepThrough]
		protected static void Log()
		{
			Log(String.Empty);
		}

		[DebuggerStepThrough]
		protected static void Log(object item)
		{
			if (item == null)
			{
				Log("null");
			}
			else
			{
				Log(String.Format(CultureInfo.InvariantCulture, "[{0}] {1}", item.GetType().Name, item));
			}
		}

		[DebuggerStepThrough]
		protected static void Log(string format, object arg0)
		{
			Log(String.Format(CultureInfo.InvariantCulture, format, arg0));
		}

		[DebuggerStepThrough]
		protected static void Log(string format, object arg0, object arg1)
		{
			Log(String.Format(CultureInfo.InvariantCulture, format, arg0, arg1));
		}

		[DebuggerStepThrough]
		protected static void Log(string format, params object[] args)
		{
			Log(String.Format(CultureInfo.InvariantCulture, format, args));
		}

		#endregion

	}
}
