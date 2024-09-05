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

namespace SnowBank.Testing
{
	using System.Diagnostics;
	using System.Globalization;
	using System.IO;
	using System.Net;
	using System.Net.NetworkInformation;
	using System.Reflection;
	using System.Runtime;
	using System.Runtime.CompilerServices;
	using System.Runtime.InteropServices;
	using System.Text;
	using System.Xml;
	using Doxense.Diagnostics;
	using Doxense.Mathematics.Statistics;
	using Doxense.Reactive.Disposables;
	using Doxense.Runtime.Comparison;
	using Doxense.Serialization;
	using Doxense.Tools;
	using Microsoft.Extensions.DependencyInjection;
	using Microsoft.Extensions.Logging;
	using NodaTime;
	using NUnit.Framework.Internal;
	using ILogger = Microsoft.Extensions.Logging.ILogger;

	/// <summary>Base class for simple unit tests. Provides a set of usefull services (logging, cancellation, async helpers, ...)</summary>
	[DebuggerNonUserCode]
	[PublicAPI]
	[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
	public abstract class SimpleTest
	{
		private Stopwatch? m_testTimer;
		private Instant m_testStart;
		private CancellationTokenSource? m_cts;

		public IClock Clock { get; set; } = SystemClock.Instance;

		static SimpleTest()
		{
#if !NETFRAMEWORK
			//TODO: REVIEW: do we still need to do this hack in .NET 6+? It was required with early .NET Core apps
			Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
#endif

			// JIT warmup of a few common types
			RobustBenchmark.Warmup();
			CrystalJson.Warmup();

			if (!GCSettings.IsServerGC)
			{
				WriteToLog("#########################################################################");
				WriteToLog("WARN: Server GC is *NOT* enabled! Performance may be slower than expected");
				WriteToLog("#########################################################################");
				WriteToLog("");
			}
		}

		[OneTimeSetUp][DebuggerNonUserCode]
		public static void BeforeEverything()
		{
			WriteToLog($"### {TestContext.CurrentContext.Test.ClassName} [START] @ {DateTime.Now.TimeOfDay}");
		}

		[OneTimeTearDown][DebuggerNonUserCode]
		public static void AfterEverything()
		{
			WriteToLog($"### {TestContext.CurrentContext.Test.ClassName} [{TestContext.CurrentContext.Result.Outcome}] ({TestContext.CurrentContext.Result.FailCount} failed) @ {DateTime.Now.TimeOfDay}");
		}

		[SetUp]
		public void BeforeEachTest()
		{
			WriteToLog($"=>= {TestContext.CurrentContext.Test.FullName} @ {DateTime.Now.TimeOfDay}");
			m_cts = new CancellationTokenSource();
			m_testTimer = new Stopwatch();
			try
			{
				OnBeforeEachTest();
			}
			catch (Exception e)
			{
				LogError($"### {TestContext.CurrentContext.Test.FullName} [BEFORE CRASH] => {e}");
				throw;
			}
			finally
			{
				m_testStart = this.Clock.GetCurrentInstant();
				m_testTimer.Start();
			}
		}

		protected virtual void OnBeforeEachTest()
		{ }

		[TearDown]
		public void AfterEachTest()
		{
			var currentContext = TestContext.CurrentContext;
			try
			{
				m_testTimer?.Stop();
				m_cts?.Cancel();

				// dispose any IDisposable services that were used during the execution
				if (this.CustomServices != null)
				{
					try
					{
						foreach (var provider in this.CustomServices)
						{
							//REVIEW: TODO: this could block the thread!
							provider.DisposeAsync().GetAwaiter().GetResult();
						}
					}
					finally
					{
						this.CustomServices = null;
					}
				}

				OnAfterEachTest();
			}
			catch (Exception e)
			{
				LogError($"### {currentContext.Test.FullName} [AFTER CRASH] => {e}");
				throw;
			}
			finally
			{
				var elapsed = m_testTimer?.Elapsed;
				WriteToLog($"=<= {currentContext.Result.Outcome} {currentContext.Test.Name}() in {elapsed?.TotalMilliseconds:N1} ms ({currentContext.AssertCount} asserts, {currentContext.Result.FailCount} failed)");
				m_cts?.Dispose();
			}
		}

		protected virtual void OnAfterEachTest()
		{ }

		[DebuggerNonUserCode]
		protected static void FullGc()
		{
			GC.Collect();
			GC.WaitForPendingFinalizers();
			GC.Collect();
		}

		#region Timeouts...

		public void ResetTimer()
		{
			m_testTimer = Stopwatch.StartNew();
		}

		protected TimeSpan TestElapsed => m_testTimer?.Elapsed ?? TimeSpan.Zero;

		protected Instant TestStartedAt => m_testStart;

		protected Duration ElapsedSinceTestStart(Instant now) => now - m_testStart;

		public CancellationToken Cancellation
		{
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get => m_cts?.Token ?? default;
		}

		/// <summary>Fait en sorte que le token <see cref="Cancellation"/> se déclenche dans le délai indiqué, quoi qu'il se produise pendant l'exécution du test</summary>
		public void SetExecutionTimeout(TimeSpan delay)
		{
			var cts = m_cts ?? throw new InvalidOperationException("Cannot set execution delay outside of a test");
			Task.Delay(delay, cts.Token).ContinueWith((_) =>
			{
				Log($"### TEST TIMED OUT AFTER {delay} !!! ###");
				cts.Cancel();
			}, TaskContinuationOptions.NotOnCanceled);
		}

		/// <summary>Mesure la durée d'exécution d'une fonction de test</summary>
		/// <param name="handler">Function invoquée</param>
		/// <returns>Durée d'exécution</returns>
		public static TimeSpan Time(Action  handler)
		{
			var sw = Stopwatch.StartNew();
			handler();
			sw.Stop();
			return sw.Elapsed;
		}

		/// <summary>Mesure la durée d'exécution d'une fonction de test</summary>
		/// <param name="handler">Function invoquée</param>
		/// <returns>Durée d'exécution</returns>
		public static async Task<TimeSpan> Time(Func<Task> handler)
		{
			var sw = Stopwatch.StartNew();
			await handler().ConfigureAwait(false);
			sw.Stop();
			return sw.Elapsed;
		}

		/// <summary>Mesure la durée d'exécution d'une fonction de test</summary>
		/// <typeparam name="TResult">Type de résultat de la fonction</typeparam>
		/// <param name="handler">Function invoquée</param>
		/// <returns>Tuple contenant le résultat de la fonction, et sa durée d'execution</returns>
		public static (TResult Result, TimeSpan Elapsed) Time<TResult>(Func<TResult> handler)
		{
			var sw = Stopwatch.StartNew();
			var result = handler();
			sw.Stop();
			return (result, sw.Elapsed);
		}

		/// <summary>Mesure la durée d'exécution d'une fonction de test</summary>
		/// <typeparam name="TResult">Type de résultat de la fonction</typeparam>
		/// <param name="handler">Function invoquée</param>
		/// <returns>Tuple contenant le résultat de la fonction, et sa durée d'execution</returns>
		public static async Task<(TResult Result, TimeSpan Elapsed)> Time<TResult>(Func<Task<TResult>> handler)
		{
			var sw = Stopwatch.StartNew();
			var result = await handler().ConfigureAwait(false);
			sw.Stop();
			return (result, sw.Elapsed);
		}

		/// <summary>Pause l'exécution du test pendant un certain temps</summary>
		/// <remarks>Le délai est automatiquement interrompu si le timeout d'exécution du test se déclenche</remarks>
		[Obsolete("Warning: waiting for a fixed time delay in a test is not good practice!")]
		public Task Wait(int milliseconds)
		{
			return Wait(TimeSpan.FromMilliseconds(milliseconds));
		}

		/// <summary>Pause l'exécution du test pendant un certain temps</summary>
		/// <remarks>Le délai est automatiquement interrompu si le timeout d'exécution du test se déclenche</remarks>
		[Obsolete("Warning: waiting for a fixed time delay in a test is not good practice!")]
		public async Task Wait(TimeSpan delay)
		{
			var start = this.Clock.GetCurrentInstant();
			try
			{
				await Task.Delay(delay, this.Cancellation).ConfigureAwait(false);
				var end = this.Clock.GetCurrentInstant();
				await OnWaitOperationCompleted("Delay", delay.ToString(), success: true, null, start, end).ConfigureAwait(false);
			}
			catch (Exception)
			{
				var end = this.Clock.GetCurrentInstant();
				await OnWaitOperationCompleted("Delay", delay.ToString(), success: false, null, start, end).ConfigureAwait(false);
			}
		}

		/// <summary>Spin de manière asynchrone jusqu'à ce qu'une condition soit réalisée, l'expiration d'un timeout, ou l'annulation du test</summary>
		public Task<TimeSpan> WaitUntil([InstantHandle] Func<bool> condition, TimeSpan timeout, string message, TimeSpan? ticks = null, [CallerArgumentExpression(nameof(condition))] string? conditionExpression = null)
		{
			return WaitUntil(
				condition,
				timeout,
				(elapsed, error) =>
				{
					Assert.Fail($"Operation took too long to execute: {message}{Environment.NewLine}Condition: {conditionExpression}{Environment.NewLine}Elapsed: {elapsed}{(error != null ? ($"{Environment.NewLine}Exception: {error}") : null)}");
				},
				ticks,
				conditionExpression);
		}

		public Task<TimeSpan> WaitUntilEqual<TValue>([InstantHandle] Func<TValue> condition, TValue comparand, TimeSpan timeout, string? message, TimeSpan? ticks = null, IEqualityComparer<TValue>? comparer = null, [CallerArgumentExpression(nameof(condition))] string? conditionExpression = null)
		{
			comparer ??= EqualityComparer<TValue>.Default;
			return WaitUntil(
				() => comparer.Equals(condition(), comparand),
				timeout,
				(elapsed, error) =>
				{
					Assert.That(condition(), Is.EqualTo(comparand), $"Operation took too long to execute: {message}{Environment.NewLine}Condition: {conditionExpression} == {comparand}{Environment.NewLine}Elapsed: {elapsed}{(error != null ? ($"{Environment.NewLine}Exception: {error}") : null)}");
				},
				ticks,
				conditionExpression
			);
		}

		/// <summary>Spin de manière asynchrone jusqu'à ce qu'une condition soit réalisée, l'expiration d'un timeout, ou l'annulation du test</summary>
		public async Task<TimeSpan> WaitUntil([InstantHandle] Func<bool> condition, TimeSpan timeout, Action<TimeSpan, Exception?>  onFail, TimeSpan? ticks = null, [CallerArgumentExpression(nameof(condition))] string? conditionExpression = null)
		{
			var ct = this.Cancellation;

			var max = ticks ?? TimeSpan.FromMilliseconds(250);
			if (max > timeout) max = TimeSpan.FromTicks(timeout.Ticks / 10);
			var delay = TimeSpan.FromMilliseconds(Math.Min(5, max.TotalMilliseconds));
			// heuristic to get a sensible value
			if (max <= TimeSpan.FromMilliseconds(50)) Assert.Fail("Ticks must be at least greater than 50ms!");

			var start = this.Clock.GetCurrentInstant();
			bool success = false;
			Exception? error = null;
			try
			{
				var sw = Stopwatch.StartNew();
				while (!ct.IsCancellationRequested)
				{
					try
					{
						if (condition())
						{
							sw.Stop();
							success = true;
							break;
						}
					}
					catch (Exception e)
					{
						error = e;
						if (e is AssertionException) throw;
						onFail(sw.Elapsed, e);
						Assert.Fail($"Operation failed will polling expression '{conditionExpression}': {e}");
					}

					await Task.Delay(delay, this.Cancellation).ConfigureAwait(false);
					if (sw.Elapsed >= timeout)
					{
						onFail(sw.Elapsed, null);
					}

					delay += delay;
					if (delay > max) delay = max;
				}

				ct.ThrowIfCancellationRequested();
				return sw.Elapsed;
			}
			finally
			{
				var end = this.Clock.GetCurrentInstant();
				await OnWaitOperationCompleted(nameof(WaitUntil), conditionExpression!, success, error, start, end).ConfigureAwait(false);
			}
		}

		/// <summary>Spin de manière asynchrone jusqu'à ce qu'une condition soit réalisée, l'expiration d'un timeout, ou l'annulation du test</summary>
		public async Task<TimeSpan> WaitUntil([InstantHandle] Func<Task<bool>> condition, TimeSpan timeout, string message, TimeSpan? ticks = null, [CallerArgumentExpression(nameof(condition))] string? conditionExpression = null)
		{
			var ct = this.Cancellation;

			var max = ticks ?? TimeSpan.FromMilliseconds(250);
			if (max > timeout) max = TimeSpan.FromTicks(timeout.Ticks / 10);
			var delay = TimeSpan.FromMilliseconds(Math.Min(5, max.TotalMilliseconds));
			// heuristic to get a sensible value
			if (max <= TimeSpan.FromMilliseconds(50)) Assert.Fail("Ticks must be at least greater than 50ms!");

			var start = this.Clock.GetCurrentInstant();
			bool success = false;
			Exception? error = null;
			try
			{
				var sw = Stopwatch.StartNew();
				while (!ct.IsCancellationRequested)
				{
					try
					{
						if (await condition().ConfigureAwait(false))
						{
							sw.Stop();
							break;
						}
					}
					catch (Exception e)
					{
						error = e;
						if (e is AssertionException) throw;
						Assert.Fail($"Operation failed will polling expression '{conditionExpression}': {e}");
					}

					await Task.Delay(delay, this.Cancellation).ConfigureAwait(false);
					if (sw.Elapsed >= timeout)
					{
						Assert.Fail($"Operation took too lonk to execute: {message}{Environment.NewLine}Condition: {conditionExpression}{Environment.NewLine}Elapsed: {sw.Elapsed}");
					}

					delay += delay;
					if (delay > max) delay = max;
				}

				ct.ThrowIfCancellationRequested();
				success = true;
				return sw.Elapsed;
			}
			finally
			{
				var end = this.Clock.GetCurrentInstant();
				await OnWaitOperationCompleted(nameof(WaitUntil), conditionExpression!, success, error, start, end).ConfigureAwait(false);
			}
		}

		protected virtual Task OnWaitOperationCompleted(string operation, string conditionExpression, bool success, Exception? error, Instant startedAt, Instant endedAt)
		{
			return Task.CompletedTask;
		}

		#endregion

		#region Async Stuff...

		/// <summary>Attend que toutes les tasks soient exécutées, ou que le timeout d'exécution du test se déclenche</summary>
		public Task WhenAll(IEnumerable<Task> tasks, TimeSpan timeout, [CallerArgumentExpression(nameof(tasks))] string? tasksExpression = null)
		{
			var ts = (tasks as Task[]) ?? tasks.ToArray();
			if (m_cts?.IsCancellationRequested ?? false) return Task.FromCanceled(m_cts.Token);
			if (ts.Length == 0) return Task.CompletedTask;
			if (ts.Length == 1 && ts[0].IsCompleted) return ts[0];
			return WaitForInternal(Task.WhenAll(ts), timeout, throwIfExpired: true, $"WhenAll({tasksExpression})");
		}

		/// <summary>Attend que toutes les tasks soient exécutées, ou que le timeout d'exécution du test se déclenche</summary>
		public async Task<TResult[]> WhenAll<TResult>(IEnumerable<Task<TResult>> tasks, TimeSpan timeout, [CallerArgumentExpression(nameof(tasks))] string? tasksExpression = null)
		{
			var ts = (tasks as Task<TResult>[]) ?? tasks.ToArray();
			this.Cancellation.ThrowIfCancellationRequested();
			if (ts.Length == 0) return [ ];
			await WaitForInternal(Task.WhenAll(ts), timeout, throwIfExpired: true, $"WhenAll({tasksExpression})").ConfigureAwait(false);
			var res = new TResult[ts.Length];
			for (int i = 0; i < res.Length; i++)
			{
				res[i] = ts[i].Result;
			}
			return res;
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

		/// <summary>Wait for a task that should complete within the specified time.</summary>
		/// <remarks>
		/// <para>The test will abort if the task did not complete (successfully or not) within the specified <paramref name="timeout"/>.</para>
		/// <para>The <see cref="Cancellation">test cancellation token</see> should be used by the task in order for this safety feature to work! If the task is not linked to this token, it will not cancel, and could timeout indefinitely.</para>
		/// </remarks>
		public Task Await(ValueTask task, TimeSpan timeout, [CallerArgumentExpression(nameof(task))] string? taskExpression = null)
		{
			return m_cts?.IsCancellationRequested == true ? Task.FromCanceled<bool>(m_cts.Token)
				: task.IsCompleted ? task.AsTask()
				: WaitForInternal(task.AsTask(), timeout, throwIfExpired: true, taskExpression!);
		}

		/// <summary>Wait for a task that should complete within the specified time.</summary>
		/// <remarks>
		/// <para>The test will abort if the task did not complete (successfully or not) within the specified timeout.</para>
		/// <para>The <see cref="Cancellation">test cancellation token</see> should be used by the task in order for this safety feature to work! If the task is not linked to this token, it will not cancel, and could timeout indefinitely.</para>
		/// </remarks>
		public Task<TResult> Await<TResult>(Task<TResult> task, int timeoutMs, [CallerArgumentExpression(nameof(task))] string? taskExpression = null)
		{
			return Await(task, TimeSpan.FromMilliseconds(timeoutMs), taskExpression);
		}

		/// <summary>Wait for a task that should complete within the specified time.</summary>
		/// <remarks>
		/// <para>The test will abort if the task did not complete (successfully or not) within the specified timeout.</para>
		/// <para>The <see cref="Cancellation">test cancellation token</see> should be used by the task in order for this safety feature to work! If the task is not linked to this token, it will not cancel, and could timeout indefinitely.</para>
		/// </remarks>
		public Task<TResult> Await<TResult>(ValueTask<TResult> task, int timeoutMs, [CallerArgumentExpression(nameof(task))] string? taskExpression = null)
		{
			return Await(task, TimeSpan.FromMilliseconds(timeoutMs), taskExpression);
		}

		/// <summary>Wait for a task that should complete within the specified time.</summary>
		/// <remarks>
		/// <para>The test will abort if the task did not complete (successfully or not) within the specified <paramref name="timeout"/>.</para>
		/// <para>The <see cref="Cancellation">test cancellation token</see> should be used by the task in order for this safety feature to work! If the task is not linked to this token, it will not cancel, and could timeout indefinitely.</para>
		/// </remarks>
		public Task<TResult> Await<TResult>(Task<TResult> task, TimeSpan timeout, [CallerArgumentExpression(nameof(task))] string? taskExpression = null)
		{
			return m_cts?.IsCancellationRequested == true  ? Task.FromCanceled<TResult>(m_cts.Token)
				: task.IsCompleted ? task
				: WaitForInternal(task, timeout, taskExpression!);
		}

		/// <summary>Wait for a task that should complete within the specified time.</summary>
		/// <remarks>
		/// <para>The test will abort if the task did not complete (successfully or not) within the specified <paramref name="timeout"/>.</para>
		/// <para>The <see cref="Cancellation">test cancellation token</see> should be used by the task in order for this safety feature to work! If the task is not linked to this token, it will not cancel, and could timeout indefinitely.</para>
		/// </remarks>
		public Task<TResult> Await<TResult>(ValueTask<TResult> task, TimeSpan timeout, [CallerArgumentExpression(nameof(task))] string? taskExpression = null)
		{
			return m_cts?.IsCancellationRequested == true  ? Task.FromCanceled<TResult>(m_cts.Token)
				: task.IsCompleted ? task.AsTask()
				: WaitForInternal(task.AsTask(), timeout, taskExpression!);
		}

		[StackTraceHidden]
		private async Task<bool> WaitForInternal(Task task, TimeSpan delay, bool throwIfExpired, string taskExpression)
		{
			bool success = false;
			Exception? error = null;
			var start = this.Clock.GetCurrentInstant();

			try
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
					error = ex;
					Assert.Fail($"Task '{taskExpression}' failed with following error: {ex}");
				}

				success = true;
				return true;
			}
			finally
			{
				var end = this.Clock.GetCurrentInstant();
				await OnWaitOperationCompleted(nameof(Await), taskExpression, success, error, start, end).ConfigureAwait(false);
			}
		}

		private async Task<TResult> WaitForInternal<TResult>(Task<TResult> task, TimeSpan delay, string taskExpression)
		{
			bool? success = null;
			Exception? error = null;
			var start = this.Clock.GetCurrentInstant();
			try
			{
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
					return await task.ConfigureAwait(false);
				}
				catch (Exception ex)
				{
					success = false;
					error = ex;
					Assert.Fail($"Task '{taskExpression}' failed with following error: {ex}");
					throw null!;
				}
			}
			finally
			{
				var end = this.Clock.GetCurrentInstant();
				await OnWaitOperationCompleted(nameof(Await), taskExpression, success ?? true, error, start, end).ConfigureAwait(false);
			}
		}

		#endregion

		#region Files & Paths...

		protected static string GetAssemblyLocation(Assembly assembly)
		{
			string path = assembly.Location;
			if (string.IsNullOrEmpty(path)) throw new InvalidOperationException($"Failed to get location of assembly {assembly}");

			// Resharper sometimes returns @"file:///xxxxx" or @"file:\xxxx"
			if (path.StartsWith("file:\\", StringComparison.OrdinalIgnoreCase))
			{
				path = path[6..];
			}
			else if (path.StartsWith("file:///", StringComparison.OrdinalIgnoreCase))
			{
				path = path[8..];
			}

			path = Path.GetDirectoryName(path) ?? ".";
			return path;
		}

		/// <summary>[TEST ONLY] Computes the path to a file, relative to the source file that is calling this method</summary>
		/// <param name="relativePath">Path, relative to the current source path, of the target file or folder</param>
		/// <param name="path"><b>DO NOT SPECIFY!</b>! The method relies on the fact that the compiler will pass the pass of the current file, which will be used to infer the path to the resource relative to it!</param>
		/// <remarks>Note: this method of computing the path is relatively safe when needing to read test resources (like samples, data sets, ...) but can break if a refactoring moves the files apart!</remarks>
		public string MapPathRelativeToCallerSourcePath(string relativePath, [CallerFilePath] string? path = null)
		{
			if (string.IsNullOrEmpty(relativePath))
			{
				relativePath = ".";
			}
			return Path.Combine(Path.GetDirectoryName(path)!, relativePath);
		}

		/// <summary>Computes the path to a temporary path or file that can safely be used by this test</summary>
		/// <param name="relative">If not null, name of the file or folder appended to the temporary path. By convention, should end with '/' if this is a folder, so that the intent is clearly visible.</param>
		/// <param name="clearFiles">If <see langword="true"/>, will clean the content of the folder (or the file)</param>
		/// <returns>Path to a temporary folder (or file). The folder will be created if required. The file will be removed if already present (from a previous test run).</returns>
		/// <example>
		/// GetTemporaryPath() => "c:\blah\workdir\testoutput\FooBarFacts"
		/// GetTemporaryPath("Zorglub") => "c:\blah\workdir\testoutput\FooBarFacts\Zorglub"
		/// GetTemporaryPath("Zorglub/") => "c:\blah\workdir\testoutput\FooBarFacts\Zorglub\"
		/// GetTemporaryPath("Frobs.zip") => "c:\blah\workdir\testoutput\FooBarFacts\Frobs.zip"
		/// </example>
		public string GetTemporaryPath(string? relative = null, bool clearFiles = false)
		{
			// We want to build: "{WORK_DIR}/{TEST_CLASS_NAME}[/sufix]"

			var context = TestContext.CurrentContext;
			string basePath = context != null! ? context.TestDirectory : Environment.CurrentDirectory;
			if (basePath.IndexOf(@"\bin\Debug", StringComparison.OrdinalIgnoreCase) > 0 || basePath.IndexOf(@"\bin\Release", StringComparison.OrdinalIgnoreCase) > 0)
			{
				basePath = Path.Combine(basePath, "TestOutput");
			}

			// Grab the name of the current test

			string className = context?.Test.ClassName ?? GetType().Name;
			string? testName = context?.Test.MethodName;

			string path = Path.Combine(basePath, className.Replace(".", "_").Replace("`", "_"));
			if (!string.IsNullOrEmpty(testName))
			{
				path = Path.Combine(basePath, testName.Replace(".", "_").Replace("`", "_"));
			}

			if (!string.IsNullOrEmpty(relative))
			{
				path = Path.Combine(path, relative);
				if (path.Contains("/")) path = path.Replace("/", "\\");
			}
			else
			{
				path += "\\";
			}

			path = Path.GetFullPath(path);
#if DEBUG
			Log($"# TempPath(\"{relative}\") => {path}");
#endif

			if (path.EndsWith('\\'))
			{ // this is a folder, create it if required

				if (!Directory.Exists(path))
				{
					Directory.CreateDirectory(path);
				}
				else if (clearFiles)
				{
					Assert.That(path, Does.StartWith(basePath), "*DANGER* Attempted to clear temp folder OUTSIDE the main test folder!");
					foreach(var entry in new DirectoryInfo(path).EnumerateFileSystemInfos())
					{
						entry.Delete();
					}
				}
			}
			else
			{ // this is a file, create the parent folder if required
				string containerFolder = Path.GetDirectoryName(path)!;
				if (!Directory.Exists(containerFolder))
				{
					Directory.CreateDirectory(containerFolder);
				}

				if (clearFiles && File.Exists(path))
				{
					File.Delete(path);
				}
			}

			return path;
		}

		/// <summary>Returns the FQDN of the running host (if possible)</summary>
		/// <returns>ex: "srv42.acme.local"</returns>
		public string GetCurrentFqdn()
		{
			string domainName = IPGlobalProperties.GetIPGlobalProperties().DomainName;
			string hostName = Dns.GetHostName();

			domainName = "." + domainName;
			if (!hostName.EndsWith(domainName, StringComparison.OrdinalIgnoreCase)) hostName += domainName;

			return hostName;
		}

		#endregion

		#region Logging...

		/// <summary>If <see langword="true"/>, we are running with an attached debugger that prefers logs to be written to the Trace/Debug output.</summary>
		protected static readonly bool AttachedToDebugger = Debugger.IsAttached;

		/// <summary>If <see langword="true"/>, we are running under Console Test Runner that prefers logs to be written to the Console output</summary>
		public static readonly bool MustOutputLogsOnConsole = DetectConsoleTestRunner();

		private static bool DetectConsoleTestRunner()
		{
			// TeamCity
			if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("TEAMCITY_VERSION")))
			{
				return true;
			}

			string? host = Assembly.GetEntryAssembly()?.GetName().Name;
			return host == "TestDriven.NetCore.AdHoc" // TestDriven.NET
				|| host == "testhost";                // ReSharper Test Runner
		}

		[DebuggerNonUserCode]
		private static void WriteToLog(string? message, bool lineBreak = true)
		{
			if (MustOutputLogsOnConsole)
			{ // force output to the console
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
			{ // outputs to the Output console (visible while the test is running under a debugger)
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
			{ // output to stdout

				//note: before NUnit 3.6, the text had to be XML encoded, but this has been fixed since v3.6.0 (cf https://github.com/nunit/nunit/issues/1891)
				//message = message.Replace("&", "&amp;").Replace("<", "&lt;");

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
		private static void WriteToErrorLog(string? message)
		{
			if (MustOutputLogsOnConsole)
			{ // force output to the console
				Console.Error.WriteLine(message);
			}
			else if (AttachedToDebugger)
			{ // outputs to the Output console (visible while the test is running under a debugger)
				Trace.WriteLine("ERROR: " + message);
				TestContext.Error.WriteLine(message);
			}
			else
			{ // output to stderr
				TestContext.Error.WriteLine(message);
			}
		}

		[DebuggerNonUserCode]
		protected void LogElapsed(string? text) => Log($"{this.TestElapsed} {text}");

		/// <summary>Writes a message to the output log</summary>
		[DebuggerNonUserCode, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void Log(string? text) => WriteToLog(text);

		/// <summary>Writes a message to the output log</summary>
		[DebuggerNonUserCode]
		public static void Log(ref DefaultInterpolatedStringHandler handler) => WriteToLog(handler.ToStringAndClear());

		[DebuggerNonUserCode]
		public static void Log(object? item) => Log(item as string ?? Stringify(item));

		[DebuggerNonUserCode]
		protected static void LogPartial(string? text) => WriteToLog(text, lineBreak: false);

		[DebuggerNonUserCode]
		public static void Log() => WriteToLog(string.Empty);

		[DebuggerNonUserCode]
		public static void LogError(string? text) => WriteToErrorLog(text);

		[DebuggerNonUserCode]
		public static void LogError(ref DefaultInterpolatedStringHandler handler) => WriteToErrorLog(handler.ToStringAndClear());

		[DebuggerNonUserCode]
		public static void LogError(string? text, Exception e) => WriteToErrorLog(text + Environment.NewLine + e);

		[DebuggerNonUserCode]
		public static void LogError(ref DefaultInterpolatedStringHandler handler, Exception e)
		{
			handler.AppendLiteral(Environment.NewLine);
			handler.AppendLiteral(e.ToString());
			WriteToErrorLog(handler.ToStringAndClear());
		}

		/// <summary>Writes the current stack trace to the output log</summary>
		/// <param name="skip">Number of stack frames to skip (usually at least 2)</param>
		[DebuggerNonUserCode]
		protected static void DumpStackTrace(int skip = 2)
		{
			var stack = Environment.StackTrace.Split('\n');

			// drop the bottom of the stack (System and NUnit stuff...
			int last = stack.Length - 1;
			while(last > skip && (stack[last].IndexOf("   at System.", StringComparison.Ordinal) >= 0 || stack[last].IndexOf("   at NUnit.Framework.", StringComparison.Ordinal) >= 0))
			{
				--last;
			}

			Log($"> {string.Join("\n> ", stack, skip, last - skip + 1)}");
		}

		/// <summary>Format as a one-line compact JSON representation of the value</summary>
		[DebuggerNonUserCode]
		protected static string Jsonify(object? item) => CrystalJson.Serialize(item, CrystalJsonSettings.Json);

		/// <summary>Format as a one-line compact JSON representation of the value</summary>
		[DebuggerNonUserCode]
		protected static string Jsonify<T>(T? item) => CrystalJson.Serialize(item, CrystalJsonSettings.Json);

		/// <summary>Format as a one-line, human-reabable, textual representation of the value</summary>
		[DebuggerNonUserCode]
		protected static string Stringify(object? item)
		{
			switch (item)
			{
				case null:
				{
					return "<null>";
				}
				case string str:
				{ // hack to prevent CRLF to break the layout
					return str.Length == 0
						? "\"\""
						: "\"" + str.Replace(@"\", @"\\").Replace("\r", @"\r").Replace("\n", @"\n").Replace("\0", @"\0").Replace(@"""", @"\""") + "\"";
				}
				case int i:
				{
					return i.ToString(CultureInfo.InvariantCulture);
				}
				case long l:
				{
					return l.ToString(CultureInfo.InvariantCulture) + "L";
				}
				case uint ui:
				{
					return ui.ToString(CultureInfo.InvariantCulture) + "U";
				}
				case ulong ul:
				{
					return ul.ToString(CultureInfo.InvariantCulture) + "UL";
				}
				case double d:
				{
					return d.ToString("R", CultureInfo.InvariantCulture);
				}
				case float f:
				{
					return f.ToString("R", CultureInfo.InvariantCulture) + "F";
				}
				case Guid g:
				{
					return "{" + g.ToString() + "}";
				}
				case JsonValue json:
				{
					return json.ToJson();
				}
				case StringBuilder sb:
				{
					return sb.ToString();
				}
			}

			var type = item.GetType();
			if (type.Name.StartsWith("ValueTuple`", StringComparison.Ordinal))
			{
				return item.ToString()!;
			}

			if (type.IsAssignableTo(typeof(Task)) || type.IsGenericInstanceOf(typeof(Task<>)))
			{
				throw new AssertionException("Cannot stringify a Task! You probably forget to add 'await' somewhere the code!");
			}

			// Formattable
			if (item is IFormattable formattable)
			{
				// use the most appropriate format, depending on the value type
				string? fmt = null;
				if (item is int or uint or long or ulong)
				{
					fmt = "N0";
				}
				else if (item is double or float)
				{
					fmt = "R";
				}
				else if (item is DateTime or DateTimeOffset)
				{
					fmt = "O";
				}
				return $"({item.GetType().GetFriendlyName()}) {formattable.ToString(fmt, CultureInfo.InvariantCulture)}";
			}

			if (type.IsArray)
			{ // Array
				Array arr = (Array) item;
				var elType = type.GetElementType()!;
				if (typeof(IFormattable).IsAssignableFrom(elType))
				{
					return $"({elType.GetFriendlyName()}[{arr.Length}]) [ {string.Join(", ", arr.Cast<IFormattable>().Select(x => x.ToString(null, CultureInfo.InvariantCulture)))} ]";
				}
				return $"({elType.GetFriendlyName()}[{arr.Length}]) {CrystalJson.Serialize(item)}";
			}

			// Alea Jacta Est
			return $"({type.GetFriendlyName()}) {CrystalJson.Serialize(item)}";
		}

		protected virtual ILoggerFactory Loggers => TestLoggerFactory.Instance;

		protected static void ConfigureLogging(IServiceCollection services, Action<ILoggingBuilder>? configure = null)
		{
			services.AddLogging(logging =>
			{
				logging.AddProvider(TestLoggerProvider.Instance);
				configure?.Invoke(logging);
			});
		}

		private class TestLoggerProvider : ILoggerProvider
		{

			public static readonly ILoggerProvider Instance = new TestLoggerProvider();

			public void Dispose() { }

			public ILogger CreateLogger(string categoryName)
			{
				return new TestLogger(categoryName);
			}

		}

		private class TestLoggerFactory : ILoggerFactory
		{

			public static readonly ILoggerFactory Instance = new TestLoggerFactory();

			public void Dispose() { }

			public ILogger CreateLogger(string categoryName)
			{
				return new TestLogger(categoryName);
			}

			public void AddProvider(ILoggerProvider provider)
			{
				//
			}
		}

		internal class TestLogger : ILogger
		{

			public string Category { get; }

			private string Prefix { get; }

			public TestLogger(string category)
			{
				this.Category = category;
				this.Prefix = "[" + category + "] ";
			}

			public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
			{
				string msg = this.Prefix + formatter(state, exception);
				switch (logLevel)
				{
					case LogLevel.Trace:
					case LogLevel.Debug:
					case LogLevel.Information:
					{
						SimpleTest.Log(msg);
						break;
					}
					case LogLevel.Warning:
					case LogLevel.Error:
					case LogLevel.Critical:
					{
						SimpleTest.LogError(msg);
						break;
					}
				}
			}

			public bool IsEnabled(LogLevel logLevel) => true;

			public IDisposable BeginScope<TState>(TState state) where TState : notnull => Disposable.Create(() => {});

		}

		#endregion

		#region Dependency Injection...

		/// <summary>List of any service provider that was created by the test method</summary>
		private List<ServiceProvider>? CustomServices { get; set; }

		/// <summary>Setup a <see cref="IServiceProvider">service provider</see> for use inside this test method</summary>
		/// <param name="configure">Handler that will customize the service provider</param>
		/// <returns>Service provider that can be used during this method</returns>
		protected IServiceProvider CreateServices(Action<IServiceCollection> configure)
		{
			var services = new ServiceCollection();

			services.AddSingleton(TestContext.CurrentContext);
			services.AddSingleton(TestContext.Parameters);
			services.AddSingleton(this);
			services.AddSingleton(this.Clock);
			services.AddSingleton(this.Rnd);
			ConfigureLogging(services);
			configure(services);

			var provider = services.BuildServiceProvider(new ServiceProviderOptions() { ValidateOnBuild = true, });
			(this.CustomServices ??= []).Add(provider);
			return provider;
		}

		#endregion

		#region Dump JSON...

		/// <summary>Outputs a human-readable representation of a JSON Value</summary>
		[DebuggerNonUserCode]
		public static void Dump(JsonValue? value)
		{
			WriteToLog(value?.ToJson(CrystalJsonSettings.JsonIndented) ?? "<null>");
		}

		/// <summary>Outputs a human-readable representation of a JSON Array</summary>
		[DebuggerNonUserCode]
		public static void Dump(JsonArray? value)
		{
			if (value == null)
			{
				WriteToLog("[0] <null_array>");
				return;
			}

			if (value.Count == 0)
			{
				WriteToLog("[0] <empty_array>");
				return;
			}

			WriteToLog($"[{value.Count}] ", lineBreak: false);
			if (value.All(JsonType.Number) || value.All(JsonType.Boolean))
			{ // vector of numbers
				WriteToLog(value.ToJson(CrystalJsonSettings.Json));
				return;
			}
			WriteToLog(value.ToJson(CrystalJsonSettings.JsonIndented));
		}

		/// <summary>Outputs a human-readable representation of a JSON Object</summary>
		[DebuggerNonUserCode]
		public static void Dump(JsonObject? value)
		{
			WriteToLog(value?.ToJson(CrystalJsonSettings.JsonIndented) ?? "<null>");
		}

		#endregion

		#region Dump XML..

		/// <summary>Outputs a human-readable representation of an XML snippet</summary>
		[DebuggerNonUserCode]
		public static void DumpXml(string xmlText, string? label = null, int indent = 0)
		{
			var doc = new XmlDocument();
			doc.LoadXml(xmlText);
			DumpXml(doc, label, indent);
		}

		/// <summary>Outputs a human-readable representation of an XML document</summary>
		[DebuggerNonUserCode]
		public static void DumpXml(XmlDocument? doc, string? label = null, int indent = 0)
		{
			DumpXml(doc?.DocumentElement, label, indent);
		}

		/// <summary>Outputs a human-readable representation of an XML node</summary>
		[DebuggerNonUserCode]
		public static void DumpXml(XmlNode? node, string? label = null, int indent = 0)
		{
			if (node == null) { Log($"{label}: <null>"); return; }

			//TODO: formatter proprement le XML!
			if (label != null) Log($"{label}:");

			var sb = new StringBuilder();
			var settings = new XmlWriterSettings
			{
				Indent = true
			};
			using (var writer = XmlWriter.Create(sb, settings))
			{
				writer.WriteNode(node.CreateNavigator()!, true);
			}

			string xml = sb.ToString();
			if (indent > 0)
			{ // poor man's indentation (pattent pending)
				xml = xml.Replace("\n", "\n" + new string('\t', indent));
			}
			Log(xml);
		}

		/// <summary>Outputs a human-readable representation of an XML document</summary>
		[DebuggerNonUserCode]
		public static void DumpXml(System.Xml.Linq.XDocument? doc, string? label = null, int indent = 0)
		{
			DumpXml(doc?.Root, label, indent);
		}

		/// <summary>Outputs a human-readable representation of an XML element</summary>
		public static void DumpXml(System.Xml.Linq.XElement? node, string? label = null, int indent = 0)
		{
			if (node == null) { Log($"{label}: <null>"); return; }

			if (label != null) Log($"{label}:");

			string xml = node.ToString();
			if (indent > 0)
			{ // poor man's indentation (pattent pending)
				xml = xml.Replace("\n", "\n" + new string('\t', indent));
			}
			Log(xml);
		}

		#endregion

		#region Dump (generic)...

		/// <summary>Tests if a type is one of the many shapes of Task or ValueTask</summary>
		/// <remarks>Used to detect common mistakes like passing a task to Dump(...) without first awaiting it</remarks>
		protected static bool IsTaskLike(Type t)
		{
			if (typeof(Task).IsAssignableFrom(t)) return true;
			if (t == typeof(ValueTask)) return true;
			//TODO: ValueTask<...>
			return false;
		}

		/// <summary>ERROR: you should await the task first, before dumping the result!</summary>
		[Obsolete("You forgot to await the task!", error: true)]
		protected static void Dump<T>(Task<T> value) => Assert.Fail($"Cannot dump the content of a Task<{typeof(T).GetFriendlyName()}>! Most likely you forgot to 'await' the method that produced this value.");

		/// <summary>ERROR: you should await the task first, before dumping the result!</summary>
		[Obsolete("You forgot to await the task!", error: true)]
		protected static void Dump<T>(ValueTask<T> value) => Assert.Fail($"Cannot dump the content of a ValueTask<{typeof(T).GetFriendlyName()}>! Most likely you forgot to 'await' the method that produced this value.");

		/// <summary>Outputs a human-readable JSON representation of a value</summary>
		/// <remarks>
		/// <para>WARNING: the type MUST be serializable as JSON! It will fail if the object has cyclic references or does not support serialization.</para>
		/// <para>One frequent case is a an object that was previously safe to serialize, but has been refactored to include internal complex objects, which will break any test calling this method!</para>
		/// </remarks>
		[DebuggerNonUserCode]
		public static void Dump<T>(T value)
		{
			if (IsTaskLike(typeof(T)))
			{
				Assert.Fail($"Cannot dump the content of a {typeof(T).GetFriendlyName()}! Most likely you work to 'await' the method that produced this value!");
			}
			WriteToLog(CrystalJson.Serialize(value, CrystalJsonSettings.JsonIndented.WithNullMembers().WithEnumAsStrings()));
		}

		/// <summary>Outputs a human-readable JSON representation of a value</summary>
		/// <remarks>
		/// <para>WARNING: the type MUST be serializable as JSON! It will fail if the object has cyclic references or does not support serialization.</para>
		/// <para>One frequent case is a an object that was previously safe to serialize, but has been refactored to include internal complex objects, which will break any test calling this method!</para>
		/// </remarks>
		[DebuggerNonUserCode]
		public static void Dump<T>(string label, T value)
		{
			WriteToLog($"{label}: <{(value != null ? value.GetType() : typeof(T)).GetFriendlyName()}>");
			WriteToLog(CrystalJson.Serialize(value, CrystalJsonSettings.JsonIndented.WithEnumAsStrings()));
		}

		/// <summary>Output a compact human-readable JSON representation of a value</summary>
		/// <remarks>
		/// <para>WARNING: the type MUST be serializable as JSON! It will fail if the object has cyclic references or does not support serialization.</para>
		/// <para>One frequent case is a an object that was previously safe to serialize, but has been refactored to include internal complex objects, which will break any test calling this method!</para>
		/// </remarks>
		[DebuggerNonUserCode]
		public static void DumpCompact<T>(T value)
		{
			WriteToLog(CrystalJson.Serialize(value, CrystalJsonSettings.Json));
		}

		/// <summary>Output a compact human-readable JSON representation of a value</summary>
		/// <remarks>
		/// <para>WARNING: the type MUST be serializable as JSON! It will fail if the object has cyclic references or does not support serialization.</para>
		/// <para>One frequent case is a an object that was previously safe to serialize, but has been refactored to include internal complex objects, which will break any test calling this method!</para>
		/// </remarks>
		[DebuggerNonUserCode]
		public static void DumpCompact<T>(string label, T value)
		{
			WriteToLog($"{label,-10}: {CrystalJson.Serialize(value, CrystalJsonSettings.Json)}");
		}

		/// <summary>Output the result of performing a JSON Diff between two instances of the same type</summary>
		/// <typeparam name="T">Type of the values to compare</typeparam>
		/// <param name="actual">Observed value</param>
		/// <param name="expected">Expected value</param>
		/// <returns><see langword="true"/> if there is at least one difference, or <see langword="false"/> if both objects are equivalent (at least their JSON representation)</returns>
		/// <remarks>
		/// <para>WARNING: the type MUST be serializable as JSON! It will fail if the object has cyclic references or does not support serialization.</para>
		/// <para>One frequent case is a an object that was previously safe to serialize, but has been refactored to include internal complex objects, which will break any test calling this method!</para>
		/// </remarks>
		[DebuggerNonUserCode]
		public static bool DumpDifferences<T>(T actual, T expected)
		{
			bool found = false;

			foreach (var (name, left, right) in ModelComparer.ComputeDifferences(actual, expected))
			{
				if (!found)
				{
					Log($"# Found differences between actual and expected {typeof(T).GetFriendlyName()} values:");
					found = true;
				}
				Log($"  * [{name}] {Stringify(left)} != {Stringify(right)}");
			}
			return found;
		}

		#endregion

		#region Dump Hexa...

		/// <summary>Output an hexadecimal dump of the buffer, similar to the view in a binary file editor.</summary>
		[DebuggerNonUserCode]
		public static void DumpHexa(byte[] buffer, HexaDump.Options options = HexaDump.Options.Default)
		{
			DumpHexa(buffer.AsSlice(), options);
		}

		/// <summary>Output an hexadecimal dump of the buffer, similar to the view in a binary file editor.</summary>
		[DebuggerNonUserCode]
		public static void DumpHexa(Slice buffer, HexaDump.Options options = HexaDump.Options.Default)
		{
			WriteToLog(HexaDump.Format(buffer, options), lineBreak: false);
		}

		/// <summary>Output an hexadecimal dump of the buffer, similar to the view in a binary file editor.</summary>
		[DebuggerNonUserCode]
		public static void DumpHexa(ReadOnlySpan<byte> buffer, HexaDump.Options options = HexaDump.Options.Default)
		{
			WriteToLog(HexaDump.Format(buffer, options), lineBreak: false);
		}

		/// <summary>Output an hexadecimal dump of the buffer, similar to the view in a binary file editor.</summary>
		[DebuggerNonUserCode]
		public static void DumpHexa<T>(ReadOnlySpan<T> array, HexaDump.Options options = HexaDump.Options.Default)
			where T : struct
		{
			WriteToLog($"Dumping memory content of {typeof(T).GetFriendlyName()}[{array.Length:N0}]:");
			WriteToLog(HexaDump.Format(MemoryMarshal.AsBytes(array), options), lineBreak: false);
		}

		/// <summary>Output an hexadecimal dump of two buffers, side by side, similar to the view in a binary diff tool.</summary>
		[DebuggerNonUserCode]
		public static void DumpVersus(byte[] left, byte[] right)
		{
			DumpVersus(left.AsSlice(), right.AsSlice());
		}

		/// <summary>Output an hexadecimal dump of two buffers, side by side, similar to the view in a binary diff tool.</summary>
		[DebuggerNonUserCode]
		public static void DumpVersus(Slice left, Slice right)
		{
			WriteToLog(HexaDump.Versus(left, right), lineBreak: false);
		}

		/// <summary>Output an hexadecimal dump of two buffers, side by side, similar to the view in a binary diff tool.</summary>
		[DebuggerNonUserCode]
		public static void DumpVersus(ReadOnlySpan<byte> left, ReadOnlySpan<byte> right)
		{
			WriteToLog(HexaDump.Versus(left, right), lineBreak: false);
		}

		#endregion

		#region Randomness...

		/// <summary>Returns the random generator used by this test run</summary>
		protected Randomizer Rnd => TestContext.CurrentContext.Random;

		#region 32-bit...

		/// <summary>Return a random 32-bit positive integer</summary>
		/// <returns>Random value from <see langword="0"/> to <see langword="0xFFFFFFFFu"/> (included)</returns>
		protected static uint NextRawBits32(Random rnd) => (uint) rnd.NextInt64(0, 0x100000000L);

		/// <summary>Return a random 32-bit positive integer, with only the specified number of bits of enthropy</summary>
		/// <returns>Random value from <see langword="0"/> to <c>(<see langword="1u"/> &lt;&lt; <paramref name="bits"/>) - <see langword="1"/></c> (included)</returns>
		/// <example><c>NextRawBits32(rnd, 9)</c> will return values that range from <see langword="0"/> to <see langword="511"/> (included)</example>
		protected static uint NextRawBits32(Random rnd, int bits) => bits switch
		{
			32 => NextRawBits32(rnd),
			>= 1 and < 32 => (uint) rnd.NextInt64(0, 0x1L << bits),
			_ => throw new ArgumentOutOfRangeException(nameof(bits), bits, "Can only generate between 1 and 32 bits or randomness")
		};

		/// <summary>Return a random 31-bit positive integer X, such that <see langword="0"/> &lt;= X &lt; <see cref="int.MaxValue"/></summary>
		/// <remarks>The result cannot be negative, and does not include <see cref="int.MaxValue"/></remarks>
		protected int NextInt32() => NextInt32(TestContext.CurrentContext.Random);

		/// <summary>Return a random 31-bit positive integer X, such that <see langword="0"/> &lt;= X &lt; <paramref name="max"/></summary>
		/// <remarks>The result cannot be negative, and does not include <see cref="int.MaxValue"/></remarks>
		protected int NextInt32(int max) => NextInt32(TestContext.CurrentContext.Random, max);

		/// <summary>Return a random 33-bit integer X, such that <paramref name="min"/> &lt;= X &lt; <paramref name="max"/>></summary>
		/// <remarks>The result cannot be negative, and does not include <see cref="int.MaxValue"/></remarks>
		protected int NextInt32(int min, int max) => NextInt32(TestContext.CurrentContext.Random, min, max);

		/// <summary>Return a random 31-bit positive integer X, such that <see langword="0"/> &lt;= X &lt; <see cref="int.MaxValue"/></summary>
		/// <remarks>The result cannot be negative, and does not include <see cref="int.MaxValue"/></remarks>
		protected static int NextInt32(Random rnd) => rnd.Next();

		/// <summary>Return a random 31-bit positive integer X, such that <see langword="0"/> &lt;= X &lt; <paramref name="max"/></summary>
		/// <remarks>The result cannot be negative, and does not include <see cref="int.MaxValue"/></remarks>
		protected static int NextInt32(Random rnd, int max) => rnd.Next(max);

		/// <summary>Return a random 33-bit integer X, such that <paramref name="min"/> &lt;= X &lt; <paramref name="max"/>></summary>
		/// <remarks>The result cannot be negative, and does not include <see cref="int.MaxValue"/></remarks>
		protected static int NextInt32(Random rnd, int min, int max) => rnd.Next(min, max);

		/// <summary>Return a random 31-bit positive integer X, such that <see langword="0"/> &lt;= X &lt; <see cref="uint.MaxValue"/></summary>
		/// <remarks>The result does not include <see cref="uint.MaxValue"/></remarks>
		protected uint NextUInt32() => NextUInt32(TestContext.CurrentContext.Random);

		/// <summary>Return a random 32-bit positive integer X, such that <see langword="0"/> &lt;= X &lt; <paramref name="max"/></summary>
		/// <remarks>The result does not include <paramref name="max"/></remarks>
		protected uint NextUInt32(uint max) => NextUInt32(TestContext.CurrentContext.Random, max);

		/// <summary>Return a random 32-bit positive integer X, such that <paramref name="min"/> &lt;= X &lt; <paramref name="max"/></summary>
		/// <remarks>The result does not include <paramref name="max"/></remarks>
		protected uint NextUInt32(uint min, uint max) => NextUInt32(TestContext.CurrentContext.Random, min, max);

		/// <summary>Return a random 32-bit positive integer X, such that <see langword="0"/> &lt;= X &lt; <see cref="uint.MaxValue"/></summary>
		/// <remarks>The result does not include <see cref="uint.MaxValue"/></remarks>
		protected static uint NextUInt32(Random rnd) => (uint) rnd.NextInt64(0, uint.MaxValue);

		/// <summary>Return a random 32-bit positive integer X, such that <see langword="0"/> &lt;= X &lt; <paramref name="max"/></summary>
		/// <remarks>The result does not include <paramref name="max"/></remarks>
		protected static uint NextUInt32(Random rnd, uint max) => (uint) rnd.NextInt64(0, max);

		/// <summary>Return a random 32-bit positive integer X, such that <paramref name="min"/> &lt;= X &lt; <paramref name="max"/></summary>
		/// <remarks>The result does not include <paramref name="max"/></remarks>
		protected static uint NextUInt32(Random rnd, uint min, uint max) => (uint) rnd.NextInt64(min, max);

		#endregion

		#region 64-bit...

		/// <summary>Return a random 64-bit positive integer</summary>
		/// <remarks>This method can return <see cref="ulong.MaxValue"/></remarks>
		protected static ulong NextRawBits64(Random rnd)
		{
			// genereate 2 x 32 bits of enthropy
			ulong a = (ulong) rnd.NextInt64(0, 0x100000000L);
			ulong b = (ulong) rnd.NextInt64(0, 0x100000000L);
			return (a << 32) | b;
		}

		/// <summary>Return a random 64-bit positive integer, with up to <paramref name="bits"/> bits of enthropy</summary>
		/// <returns>Random value from <see langword="0"/> to <c>(<see langword="1u"/> &lt;&lt; <paramref name="bits"/>) - <see langword="1"/></c> (included)</returns>
		/// <example><c>NextRawBits64(rnd, 42)</c> will return values that range from <see langword="0"/> to <see langword="4_398_046_511_103"/> (included)</example>
		protected static ulong NextRawBits64(Random rnd, int bits) => bits switch
		{
			64 => NextRawBits64(rnd),
			32 => NextRawBits32(rnd),
			>= 1 and < 32 => (ulong) rnd.NextInt64(0, 0x1L << (bits - 1)),
			< 64 => ((ulong) rnd.NextInt64(0, 0x1L << (bits - 32)) << 32) | NextRawBits32(rnd),
			_ => throw new ArgumentOutOfRangeException(nameof(bits), bits, "Can only generate between 1 and 64 bits or randomness")
		};

		/// <summary>Return a random 63-bit positive integer X, such that <see langword="0"/> &lt;= X &lt; <see cref="long.MaxValue"/></summary>
		/// <remarks>The result cannot be negative, and does not include <see cref="long.MaxValue"/></remarks>
		protected long NextInt64() => NextInt64(TestContext.CurrentContext.Random);

		/// <summary>Return a random 63-bit positive integer X, such that <see langword="0"/> &lt;= X &lt; <see cref="long.MaxValue"/></summary>
		/// <remarks>The result cannot be negative, and does not include <see cref="long.MaxValue"/></remarks>
		protected static long NextInt64(Random rnd) => rnd.NextInt64();

		/// <summary>Return a random 63-bit positive integer X, such that <see langword="0"/> &lt;= X &lt; <paramref name="max"/></summary>
		/// <remarks>The result cannot be negative, and does not include <paramref name="max"/></remarks>
		protected static long NextInt64(Random rnd, long max) => rnd.NextInt64(0, max);

		/// <summary>Return a random 63-bit positive integer X, such that <paramref name="min"/> &lt;= X &lt; <paramref name="max"/></summary>
		/// <remarks>The result cannot be negative, and does not include <paramref name="max"/></remarks>
		protected static long NextInt64(Random rnd, long min, long max) => rnd.NextInt64(min, max);

		/// <summary>Return a random 63-bit positive integer X, such that <see langword="0"/> &lt;= X &lt; <see cref="ulong.MaxValue"/></summary>
		/// <remarks>The result does not include <see cref="ulong.MaxValue"/></remarks>
		protected ulong NextUInt64() => NextUInt64(TestContext.CurrentContext.Random);

		/// <summary>Return a random 64-bit positive integer X, such that <see langword="0"/> &lt;= X &lt; <see cref="ulong.MaxValue"/></summary>
		/// <remarks>The result does not include <see cref="ulong.MaxValue"/></remarks>
		protected static ulong NextUInt64(Random rnd) => (ulong) rnd.NextInt64(long.MinValue, long.MaxValue);

		/// <summary>Return a random 64-bit positive integer X, such that <see langword="0"/> &lt;= X &lt; <paramref name="max"/></summary>
		/// <remarks>The result does not include <paramref name="max"/></remarks>
		protected static ulong NextUInt64(Random rnd, ulong max)
		{
			if (max <= long.MaxValue)
			{
				return (ulong) rnd.NextInt64(0, (long) max);
			}

			if (max == ulong.MaxValue)
			{ // full range
				return (ulong) rnd.NextInt64(long.MinValue, long.MaxValue);
			}

			ulong a = (ulong) rnd.NextInt64(); // 0 <= a < long.MaxValue
			ulong b = (ulong) rnd.NextInt64(0, (long) (max + 1 - long.MaxValue)); // 0 <= a <= (max - long.MaxValue)
			return a + b;
		}

		/// <summary>Return a random 64-bit positive integer X, such that <see langword="0"/> &lt;= X &lt; <paramref name="max"/></summary>
		/// <remarks>The result does not include <paramref name="max"/></remarks>
		protected static ulong NextUInt64(Random rnd, ulong min, ulong max)
		{
			if (min <= long.MaxValue && max <= long.MaxValue)
			{ // max 63 bits
				return (ulong) rnd.NextInt64((long) min, (long) max);
			}

			if (min == 0 && max == ulong.MaxValue)
			{ // full range
				return (ulong) rnd.NextInt64(long.MinValue, long.MaxValue);
			}

			//REVIEW: I'm not sure if we are missing 1 value or not? for ex if min=1 && max=ulong.MaxValue ?
			ulong r = max - min;
			ulong a = (ulong) rnd.NextInt64(); // 0 <= a < long.MaxValue
			ulong b = (ulong) rnd.NextInt64(0, (long) (r + 1 - long.MaxValue)); // 0 <= a <= (max - long.MaxValue)
			return min + a + b;
		}

		#endregion

		#region 128-bit...

#if NET8_0_OR_GREATER

		/// <summary>Return a random 127-bit positive integer X, such that <see langword="0"/> &lt;= X &lt; <see cref="Int128.MaxValue"/></summary>
		/// <remarks>The result does not include <see cref="Int128.MaxValue"/></remarks>
		protected Int128 NextInt128() => NextInt128(TestContext.CurrentContext.Random);

		/// <summary>Return a random 127-bit positive integer X, such that <see langword="0"/> &lt;= X &lt; <see cref="Int128.MaxValue"/></summary>
		/// <remarks>The result does not include <see cref="Int128.MaxValue"/></remarks>
		protected static Int128 NextInt128(Random rnd) => new((ulong) NextInt64(rnd), NextRawBits64(rnd));

		/// <summary>Return a random 128-bit positive integer X, such that <see langword="0"/> &lt;= X &lt; <see cref="UInt128.MaxValue"/></summary>
		/// <remarks>The result does not include <see cref="ulong.MaxValue"/></remarks>
		protected static UInt128 NextUInt128() => NextUInt128(TestContext.CurrentContext.Random);

		/// <summary>Return a random 128-bit positive integer X, such that <see langword="0"/> &lt;= X &lt; <see cref="UInt128.MaxValue"/></summary>
		/// <remarks>The result does not include <see cref="ulong.MaxValue"/></remarks>
		protected static UInt128 NextUInt128(Random rnd) => new(NextUInt64(rnd), NextRawBits64(rnd));

#endif

		#endregion

		#region Single...

		/// <summary>Return a random IEEE 32-bit floating point decimal number X, such that <see langword="0.0"/> &lt;= X &lt; <see langword="1.0"/></summary>
		/// <remarks>The result does not include <see langword="1.0"/></remarks>
		protected float NextSingle() => NextSingle(TestContext.CurrentContext.Random);

		/// <summary>Return a random IEEE 32-bit floating point decimal number X, such that <see langword="0.0"/> &lt;= X &lt; <paramref name="max"/></summary>
		/// <remarks>The result does not include <paramref name="max"/></remarks>
		protected float NextSingle(float max) => NextSingle(TestContext.CurrentContext.Random, max);

		/// <summary>Return a random IEEE 32-bit floating point decimal number X, such that <see langword="0.0"/> &lt;= X &lt; <see langword="1.0"/></summary>
		/// <remarks>The result does not include <see langword="1.0"/></remarks>
		protected static float NextSingle(Random rnd) => (float) rnd.NextDouble();

		/// <summary>Return a random IEEE 32-bit floating point decimal number X, such that <see langword="0.0"/> &lt;= X &lt; <see langword="1.0"/></summary>
		/// <remarks>The result does not include <see langword="1.0"/></remarks>
		protected static float NextSingle(Random rnd, float max) => max * (float) rnd.NextDouble();

		#endregion

		#region Double...

		/// <summary>Return a random IEEE 64-bit floating point decimal number X, such that <see langword="0.0"/> &lt;= X &lt; <see langword="1.0"/></summary>
		/// <remarks>The result does not include <see langword="1.0"/></remarks>
		protected double NextDouble() => NextDouble(TestContext.CurrentContext.Random);

		/// <summary>Return a random IEEE 64-bit floating point decimal number X, such that <see langword="0.0"/> &lt;= X &lt; <param name="max"></param></summary>
		/// <remarks>The result does not include <see langword="1.0"/></remarks>
		protected double NextDouble(double max) => NextDouble(TestContext.CurrentContext.Random, max);

		/// <summary>Return a random IEEE 64-bit floating point decimal number X, such that <see langword="0.0"/> &lt;= X &lt; <see langword="1.0"/></summary>
		/// <remarks>The result does not include <see langword="1.0"/></remarks>
		protected static double NextDouble(Random rnd) => rnd.NextDouble();

		/// <summary>Return a random IEEE 64-bit floating point decimal number X, such that 0.0 &lt;= X &lt; <paramref name="max"/></summary>
		/// <remarks>The result does not include <see langword="1.0"/></remarks>
		protected static double NextDouble(Random rnd, double max) => max * rnd.NextDouble();

		/// <summary>Return a random IEEE 64-bit floating point decimal number X, such that <paramref name="min"/> &lt;= X &lt; <paramref name="max"/></summary>
		/// <remarks>The result does not include <paramref name="max"/>/></remarks>
		protected static double NextDouble(Random rnd, double min, double max) => min + ((max - min) * rnd.NextDouble());

		#endregion

		#endregion

		#region It!

		// Fluent API used to write tests broken in steps that give a short description of the sub-operation:
		// {
		//     It("should go to the Moon, and do the other things", () =>
		//     {
		//         Assert.That(DoIt(123, 456), Is.EqualTo(789));
		//     });
		//     It("should not do this bad thing in this weird case that will never ever happen in production, I'm pretty sure...", () =>
		//     {
		//         Assert.That(() => DoIt(null, double.NaN), Throws.Exception);
		//     });
		//     // ...
		// }

		/// <summary>Execute a sub-step of the test, inside a transient scope</summary>
		/// <param name="what">A short description of the operation, usually formatted as "should ..." / "should not ..."</param>
		/// <param name="action">Action that will be performed</param>
		/// <remarks>
		/// <para>The elapsed time will be measured and displayed in the log, along with the action name.</para>
		/// <para>If the action throws an exception, it will be transformed into <see cref="Assert.Fail(string)">a failed assertion</see></para>
		/// </remarks>
		protected static void It(string what, Action action)
		{
			Log($"=== `{what}` {new string('-', Math.Max(0, 60 - what.Length))}");
			var sw = Stopwatch.StartNew();
			try
			{
				action();
			}
			catch (AssertionException) { throw; }
			catch (Exception e)
			{
				Assert.Fail($"Operation '{what}' failed: " + e);
			}
			finally
			{
				sw.Stop();
				Log($"> ({sw.Elapsed})");
			}
		}

		/// <summary>Execute a sub-step of the test, inside a transient scope</summary>
		/// <param name="what">A short description of the operation, usually formatted as "should ..." / "should not ..."</param>
		/// <param name="action">Action that will be performed</param>
		/// <remarks>
		/// <para>The elapsed time will be measured and displayed in the log, along with the action name.</para>
		/// <para>If the action throws an exception, it will be transformed into <see cref="Assert.Fail(string)">a failed assertion</see></para>
		/// </remarks>
		protected static TResult It<TResult>(string what, Func<TResult> action)
		{
			Log($"=== `{what}` {new string('-', Math.Max(0, 60 - what.Length))}");
			var sw = Stopwatch.StartNew();
			try
			{
				return action();
			}
			catch (AssertionException) { throw; }
			catch (Exception e)
			{
				Assert.Fail($"Operation '{what}' failed: {e}");
				return default!; // never reached
			}
			finally
			{
				sw.Stop();
				Log($"> ({sw.Elapsed})");
			}
		}

		/// <summary>Execute a sub-step of the test, inside a transient scope</summary>
		/// <param name="what">A short description of the operation, usually formatted as "should ..." / "should not ..."</param>
		/// <param name="action">Action that will be performed</param>
		/// <remarks>
		/// <para>The elapsed time will be measured and displayed in the log, along with the action name.</para>
		/// <para>If the action throws an exception, it will be transformed into <see cref="Assert.Fail(string)">a failed assertion</see></para>
		/// </remarks>
		protected static async Task It(string what, Func<Task> action)
		{
			Log($"--- `{what}` {new string('-', Math.Max(0, 60 - what.Length))}");
			var sw = Stopwatch.StartNew();
			try
			{
				await action().ConfigureAwait(false);
			}
			catch (AssertionException) { throw; }
			catch (Exception e)
			{
				Assert.Fail($"Operation '{what}' failed: " + e);
			}
			finally
			{
				sw.Stop();
				Log($"> ({sw.Elapsed})");
			}
		}

		/// <summary>Execute a sub-step of the test, inside a transient scope</summary>
		/// <param name="what">A short description of the operation, usually formatted as "should ..." / "should not ..."</param>
		/// <param name="action">Action that will be performed</param>
		/// <remarks>
		/// <para>The elapsed time will be measured and displayed in the log, along with the action name.</para>
		/// <para>If the action throws an exception, it will be transformed into <see cref="Assert.Fail(string)">a failed assertion</see></para>
		/// </remarks>
		protected static async Task<TResult> It<TResult>(string what, Func<Task<TResult>> action)
		{
			Log($"--- `{what}` {new string('-', Math.Max(0, 60 - what.Length))}");
			var sw = Stopwatch.StartNew();
			try
			{
				return await action().ConfigureAwait(false);
			}
			catch (AssertionException) { throw; }
			catch (Exception e)
			{
				Assert.Fail($"Operation '{what}' failed: " + e);
				return default!; // never reached
			}
			finally
			{
				sw.Stop();
				Log($"> ({sw.Elapsed})");
			}
		}

		#endregion

		#region Obsolete Stuff...

		// this will be removed soon !

		[StringFormatMethod(nameof(format)), Obsolete("Use string interpolation instead")]
		public static void Log(string format, object? arg0) => SimpleTest.Log(string.Format(CultureInfo.InvariantCulture, format, arg0));

		[StringFormatMethod(nameof(format)), Obsolete("Use string interpolation instead")]
		public static void Log(string format, object? arg0, object? arg1) => SimpleTest.Log(string.Format(CultureInfo.InvariantCulture, format, arg0, arg1));

		[StringFormatMethod(nameof(format)), Obsolete("Use string interpolation instead")]
		public static void Log(string format, params object?[] args) => SimpleTest.Log(string.Format(CultureInfo.InvariantCulture, format, args));

		[Obsolete("This method is not required anymore. You can call Log() with an interporlated directly", error: true)]
		public static void LogInv(FormattableString msg) => SimpleTest.Log(msg.ToString(CultureInfo.InvariantCulture));

		[Obsolete("Renamed to Await(...)")]
		protected Task WaitFor(Task task, int timeoutMs, [CallerArgumentExpression(nameof(task))] string? taskExpression = null) => Await(task, timeoutMs, taskExpression);

		[Obsolete("Renamed to Await(...)")]
		public Task WaitFor(ValueTask task, int timeoutMs, [CallerArgumentExpression(nameof(task))] string? taskExpression = null) => Await(task, timeoutMs, taskExpression);

		[Obsolete("Renamed to Await(...)")]
		protected Task WaitFor(Task task, TimeSpan timeout, [CallerArgumentExpression(nameof(task))] string? taskExpression = null) => Await(task, timeout, taskExpression);

		[Obsolete("Renamed to Await(...)")]
		protected Task WaitFor(ValueTask task, TimeSpan timeout, [CallerArgumentExpression(nameof(task))] string? taskExpression = null) => Await(task, timeout, taskExpression);

		[Obsolete("Renamed to Await(...)")]
		protected Task<TResult> WaitFor<TResult>(Task<TResult> task, int timeoutMs, [CallerArgumentExpression(nameof(task))] string? taskExpression = null) => Await(task, timeoutMs, taskExpression);

		[Obsolete("Renamed to Await(...)")]
		protected Task<TResult> WaitFor<TResult>(ValueTask<TResult> task, int timeoutMs, [CallerArgumentExpression(nameof(task))] string? taskExpression = null) => Await(task, timeoutMs, taskExpression);

		[Obsolete("Renamed to Await(...)")]
		protected Task<TResult> WaitFor<TResult>(Task<TResult> task, TimeSpan timeout, [CallerArgumentExpression(nameof(task))] string? taskExpression = null) => Await(task, timeout, taskExpression);

		[Obsolete("Renamed to Await(...)")]
		protected Task<TResult> WaitFor<TResult>(ValueTask<TResult> task, TimeSpan timeout, [CallerArgumentExpression(nameof(task))] string? taskExpression = null) => Await(task, timeout, taskExpression);

		#endregion

	}

}
