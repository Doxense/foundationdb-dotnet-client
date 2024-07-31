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

namespace FoundationDB.Client.Tests
{
	using System;
	using System.Collections.Generic;
	using System.Diagnostics;
	using System.Reflection;
	using System.Runtime.CompilerServices;
	using System.Threading;
	using System.Threading.Tasks;
	using Doxense.Collections.Tuples;
	using NUnit.Framework;
	using NUnit.Framework.Internal;

	/// <summary>Base class for all FoundationDB tests</summary>
	public abstract class FdbTest
	{

		private CancellationTokenSource? m_cts;
		private CancellationToken m_ct;
		private Stopwatch? m_timer;

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

			// call the hook if defined on the derived test class
			OnBeforeAllTests().GetAwaiter().GetResult();
		}

		protected virtual Task OnBeforeAllTests() => Task.CompletedTask;

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

			// call the hook if defined on the derived test class
			OnBeforeEachTest().GetAwaiter().GetResult();

			m_timer = Stopwatch.StartNew();
		}

		protected virtual Task OnBeforeEachTest() => Task.CompletedTask;

		[TearDown]
		protected void AfterEachTest()
		{
			m_timer?.Stop();
			if (m_cts != null)
			{
				try { m_cts.Cancel(); } catch { }
				m_cts.Dispose();
			}

			// call the hook if defined on the derived test class
			OnAfterEachTest().GetAwaiter().GetResult();
		}

		protected virtual Task OnAfterEachTest() => Task.CompletedTask;

		[OneTimeTearDown]
		private void AfterAllTests()
		{
			// call the hook if defined on the derived test class
			OnAfterAllTests().GetAwaiter().GetResult();
		}

		protected virtual Task OnAfterAllTests() => Task.CompletedTask;

		/// <summary>Time elapsed since the start of the current test</summary>
		protected TimeSpan TestElapsed
		{
			[DebuggerStepThrough]
			get { return m_timer?.Elapsed ?? TimeSpan.Zero; }
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
		protected Task CleanLocation(IFdbDatabase db, ISubspaceLocation location)
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
		protected Task DumpSubspace(IFdbDatabase db, ISubspaceLocation path)
		{
			return TestHelpers.DumpLocation(db, path, this.Cancellation);
		}

		[DebuggerStepThrough]
		protected Task DumpSubspace(IFdbReadOnlyTransaction tr, IKeySubspace subspace)
		{
			return TestHelpers.DumpSubspace(tr, subspace);
		}

		[DebuggerStepThrough]
		protected async Task DumpSubspace(IFdbReadOnlyTransaction tr, ISubspaceLocation location)
		{
			var subspace = await location.Resolve(tr);
			if (subspace != null)
			{
				await TestHelpers.DumpSubspace(tr, subspace);
			}
			else
			{
				Log($"# Location {location} not found!");
			}
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

		// Quand on est exécuté depuis VS en mode debug on préfère écrire dans Trace. Sinon, on écrit dans la Console...
		private static readonly bool AttachedToDebugger = Debugger.IsAttached;

		/// <summary>Indique si on fonctionne sous un runner qui préfère utiliser la console pour l'output des logs</summary>
		private static readonly bool MustOutputLogsOnConsole = DetectConsoleTestRunner();

		private static bool DetectConsoleTestRunner()
		{
			// TeamCity
			if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("TEAMCITY_VERSION"))) return true;

			string? host = Assembly.GetEntryAssembly()?.GetName().Name;
			return host == "TestDriven.NetCore.AdHoc" // TestDriven.NET
			    || host == "testhost";                // ReSharper Test Runner
		}

		[DebuggerNonUserCode]
		private static void WriteToLog(string message, bool lineBreak = true)
		{
			if (MustOutputLogsOnConsole)
			{ // write to stdout
				if (lineBreak)
				{
					Console.Out.WriteLine(message);
				}
				else
				{
					Console.Out.Write(message);
				}
			}
			else if (AttachedToDebugger)
			{ // write to the VS 'output' tab
				if (lineBreak)
				{
					Trace.WriteLine(message);
				}
				else
				{
					Trace.Write(message);
				}
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
		public static void Log(string? text)
		{
			WriteToLog(text ?? string.Empty);
		}

		[DebuggerStepThrough]
		public static void Log(ref DefaultInterpolatedStringHandler text)
		{
			WriteToLog(text.ToStringAndClear());
		}

		public static void Log<T>(T value)
		{
			if (default(T) == null && value == null)
			{
				Log("<null>");
				return;
			}

			switch (value)
			{
				case string s:
				{
					Log(s);
					break;
				}
				case null:
				{
					Log($"({typeof(T).Name}) <null>");
					break;
				}
				case IFormattable fmt:
				{
					Log(fmt.ToString());
					break;
				}
				default:
				{
					Log($"({value.GetType().Name}) {value}");
					break;
				}
			}
		}

		[DebuggerStepThrough]
		public static void Log()
		{
			WriteToLog(string.Empty);
		}

		[DebuggerStepThrough]
		public static void Dump(object? item)
		{
			if (item == null)
			{
				WriteToLog("null");
			}
			else
			{
				WriteToLog($"[{item.GetType().Name}] {item}");
			}
		}

		#endregion

		/// <summary>Converts a string into an utf-8 encoded key</summary>
		protected static Slice Literal(string text) => Slice.FromByteString(text);

		/// <summary>Converts a 1-tuple into a binary key</summary>
		protected static Slice Key<T1>(T1 item1) => TuPack.EncodeKey(item1);

		/// <summary>Converts a 2-tuple into a binary key</summary>
		protected static Slice Key<T1, T2>(T1 item1, T2 item2) => TuPack.EncodeKey(item1, item2);

		/// <summary>Converts a 3-tuple into a binary key</summary>
		protected static Slice Key<T1, T2, T3>(T1 item1, T2 item2, T3 item3) => TuPack.EncodeKey(item1, item2, item3);

		/// <summary>Converts a 4-tuple into a binary key</summary>
		protected static Slice Key<T1, T2, T3, T4>(T1 item1, T2 item2, T3 item3, T4 item4) => TuPack.EncodeKey(item1, item2, item3, item4);

		/// <summary>Converts a 5-tuple into a binary key</summary>
		protected static Slice Key<T1, T2, T3, T4, T5>(T1 item1, T2 item2, T3 item3, T4 item4, T5 item5) => TuPack.EncodeKey(item1, item2, item3, item4, item5);

		/// <summary>Pack a tuple into a binary key</summary>
		protected static Slice Pack(IVarTuple items) => TuPack.Pack(items);

		/// <summary>Pack a tuple into a binary key</summary>
		protected static Slice Pack<T1>(STuple<T1> items) => TuPack.Pack(items);

		/// <summary>Pack a tuple into a binary key</summary>
		protected static Slice Pack<T1, T2>(STuple<T1, T2> items) => TuPack.Pack(items);
		/// <summary>Pack a tuple into a binary key</summary>
		protected static Slice Pack<T1, T2>(ValueTuple<T1, T2> items) => TuPack.Pack(items);

		/// <summary>Pack a tuple into a binary key</summary>
		protected static Slice Pack<T1, T2, T3>(STuple<T1, T2, T3> items) => TuPack.Pack(items);
		/// <summary>Pack a tuple into a binary key</summary>
		protected static Slice Pack<T1, T2, T3>(ValueTuple<T1, T2, T3> items) => TuPack.Pack(items);

		/// <summary>Pack a tuple into a binary key</summary>
		protected static Slice Pack<T1, T2, T3, T4>(STuple<T1, T2, T3, T4> items) => TuPack.Pack(items);
		/// <summary>Pack a tuple into a binary key</summary>
		protected static Slice Pack<T1, T2, T3, T4>(ValueTuple<T1, T2, T3, T4> items) => TuPack.Pack(items);

		/// <summary>Pack a tuple into a binary key</summary>
		protected static Slice Pack<T1, T2, T3, T4, T5>(STuple<T1, T2, T3, T4, T5> items) => TuPack.Pack(items);
		/// <summary>Pack a tuple into a binary key</summary>
		protected static Slice Pack<T1, T2, T3, T4, T5>(ValueTuple<T1, T2, T3, T4, T5> items) => TuPack.Pack(items);

		/// <summary>Converts a string into an utf-8 encoded value</summary>
		protected static Slice Value(string text) => Slice.FromStringUtf8(text);

		protected Task<T> DbRead<T>(IFdbRetryable db, Func<IFdbReadOnlyTransaction, Task<T>> handler)
		{
			return db.ReadAsync(handler, this.Cancellation);
		}

		protected Task<List<T>> DbQuery<T>(IFdbRetryable db, Func<IFdbReadOnlyTransaction, IAsyncEnumerable<T>> handler)
		{
			return db.QueryAsync(handler, this.Cancellation);
		}

		protected Task DbWrite(IFdbRetryable db, Action<IFdbTransaction> handler)
		{
			return db.WriteAsync(handler, this.Cancellation);
		}

		protected Task DbWrite(IFdbRetryable db, Func<IFdbTransaction, Task> handler)
		{
			return db.WriteAsync(handler, this.Cancellation);
		}

		protected Task DbVerify(IFdbRetryable db, Func<IFdbReadOnlyTransaction, Task> handler)
		{
			return db.ReadAsync(async (tr) => { await handler(tr); return true; }, this.Cancellation);
		}

		/// <summary>Wait for a task that should complete within the specified time.</summary>
		/// <param name="task">The task that will be awaited.</param>
		/// <param name="timeoutMs">The maximum allowed time (in milliseconds) for the task to complete.</param>
		/// <param name="taskExpression">Expression that generated the task (for logging purpose)</param>
		/// <remarks>
		/// <para>The test will abort if the task did not complete (successfully or not) within the specified timeout.</para>
		/// <para>The <see cref="Cancellation">test cancellation token</see> should be used by the task in order for this safety feature to work! If the task is not linked to this token, it will not cancel, and could timeout indefinitely.</para>
		/// </remarks>
		public Task Await(Task task, int timeoutMs, [CallerArgumentExpression(nameof(task))] string? taskExpression = null)
		{
			return Await(task, TimeSpan.FromMilliseconds(timeoutMs), taskExpression!);
		}

		/// <summary>Wait for a task that should complete within the specified time.</summary>
		/// <param name="task">The task that will be awaited.</param>
		/// <param name="timeoutMs">The maximum allowed time (in milliseconds) for the task to complete.</param>
		/// <param name="taskExpression">Expression that generated the task (for logging purpose)</param>
		/// <remarks>
		/// <para>The test will abort if the task did not complete (successfully or not) within the specified timeout.</para>
		/// <para>The <see cref="Cancellation">test cancellation token</see> should be used by the task in order for this safety feature to work! If the task is not linked to this token, it will not cancel, and could timeout indefinitely.</para>
		/// </remarks>
		public Task Await(ValueTask task, int timeoutMs, [CallerArgumentExpression(nameof(task))] string? taskExpression = null)
		{
			return Await(task, TimeSpan.FromMilliseconds(timeoutMs), taskExpression!);
		}

		/// <summary>Wait for a task that should complete within the specified time.</summary>
		/// <param name="task">The task that will be awaited.</param>
		/// <param name="timeout">The maximum allowed time for the task to complete.</param>
		/// <param name="taskExpression">Expression that generated the task (for logging purpose)</param>
		/// <remarks>
		/// <para>The test will abort if the task did not complete (successfully or not) within the specified <paramref name="timeout"/>.</para>
		/// <para>The <see cref="Cancellation">test cancellation token</see> should be used by the task in order for this safety feature to work! If the task is not linked to this token, it will not cancel, and could timeout indefinitely.</para>
		/// </remarks>
		public Task Await(Task task, TimeSpan timeout, [CallerArgumentExpression(nameof(task))] string? taskExpression = null)
		{
			return m_cts?.IsCancellationRequested == true ? Task.FromCanceled<bool>(m_cts.Token)
			     : task.IsCompleted ? task
			     : WaitForInternal(task, timeout, throwIfExpired: true, taskExpression!);
		}

		/// <summary>Attend que la task s'exécute, avec un délai d'attente maximum, ou que le timeout d'exécution du test se déclenche</summary>
		public Task Await(ValueTask task, TimeSpan timeout, [CallerArgumentExpression(nameof(task))] string? taskExpression = null)
		{
			return m_cts?.IsCancellationRequested == true ? Task.FromCanceled<bool>(m_cts.Token)
				: task.IsCompleted ? task.AsTask()
				: WaitForInternal(task.AsTask(), timeout, throwIfExpired: true, taskExpression!);
		}

		/// <summary>Attend que la task s'exécute, avec un délai d'attente maximum, ou que le timeout d'exécution du test se déclenche</summary>
		public Task<TResult> Await<TResult>(Task<TResult> task, int timeoutMs, [CallerArgumentExpression(nameof(task))] string? taskExpression = null)
		{
			return Await(task, TimeSpan.FromMilliseconds(timeoutMs), taskExpression);
		}

		/// <summary>Attend que la task s'exécute, avec un délai d'attente maximum, ou que le timeout d'exécution du test se déclenche</summary>
		public Task<TResult> Await<TResult>(ValueTask<TResult> task, int timeoutMs, [CallerArgumentExpression(nameof(task))] string? taskExpression = null)
		{
			return Await(task, TimeSpan.FromMilliseconds(timeoutMs), taskExpression);
		}

		/// <summary>Attend que la task s'exécute, avec un délai d'attente maximum, ou que le timeout d'exécution du test se déclenche</summary>
		public Task<TResult> Await<TResult>(Task<TResult> task, TimeSpan timeout, [CallerArgumentExpression(nameof(task))] string? taskExpression = null)
		{
			return m_cts?.IsCancellationRequested == true  ? Task.FromCanceled<TResult>(m_cts.Token)
				: task.IsCompleted ? task
				: WaitForInternal(task, timeout, taskExpression!);
		}

		/// <summary>Attend que la task s'exécute, avec un délai d'attente maximum, ou que le timeout d'exécution du test se déclenche</summary>
		public Task<TResult> Await<TResult>(ValueTask<TResult> task, TimeSpan timeout, [CallerArgumentExpression(nameof(task))] string? taskExpression = null)
		{
			return m_cts?.IsCancellationRequested == true  ? Task.FromCanceled<TResult>(m_cts.Token)
				: task.IsCompleted ? task.AsTask()
				: WaitForInternal(task.AsTask(), timeout, taskExpression!);
		}

		[StackTraceHidden]
		private async Task<bool> WaitForInternal(Task task, TimeSpan delay, bool throwIfExpired, string taskExpression)
		{
			if (!task.IsCompleted)
			{
				var ct = this.Cancellation;
				if (task != (await Task.WhenAny(task, Task.Delay(delay, ct)).ConfigureAwait(false)))
				{ // timeout!
					if (ct.IsCancellationRequested)
					{
						Log("### Wait aborted due to test cancellation! ###");
						Assert.Fail("Test execution has been aborted because it took too long to execute!");
					}

					if (throwIfExpired)
					{
						Log("### Wait aborted due to timeout! ###");
						Assert.Fail($"Operation took more than {delay} to execute: {taskExpression}");
					}

					return false;
				}
			}

			if (task.Status != TaskStatus.RanToCompletion)
			{ // re-throw error
				var ex = task.Exception!.Unwrap();
				if (ex is AggregateException { InnerExceptions.Count: 1 } aggEx)
				{
					ex = aggEx.InnerExceptions[0];
				}
				//Assert.Fail($"Task '{taskExpression}' failed with following error: {ex}");
				throw ex;
			}

			return true;
		}

		private async Task<TResult> WaitForInternal<TResult>(Task<TResult> task, TimeSpan delay, string taskExpression)
		{
			bool? success = null;
			Exception? error = null;
			if (!task.IsCompleted)
			{
				var ct = this.Cancellation;
				if (task != (await Task.WhenAny(task, Task.Delay(delay, ct)).ConfigureAwait(false)))
				{ // timeout!
					success = false;

					if (ct.IsCancellationRequested)
					{
						Log("### Wait aborted due to test cancellation! ###");
						Assert.Fail("Test execution has been aborted because it took too long to execute!");
					}
					else
					{
						Log("### Wait aborted due to timeout! ###");
						Assert.Fail($"Operation took more than {delay} to execute: {taskExpression}");
					}
				}

				if (!task.IsCompleted)
				{
					Assert.Fail($"Task did not complete in time ({task.Status})");
				}
			}

			// return result or throw error
			try
			{
				return await task;
			}
			catch (Exception ex)
			{
				Assert.Fail($"Task '{taskExpression}' failed with following error: {ex}");
				throw null!;
			}
		}

	}

}
