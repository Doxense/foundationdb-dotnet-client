#region Copyright (c) 2005-2023 Doxense SAS
// All rights reserved.
// 
// Redistribution and use in source and binary forms, with or without
// modification, are permitted provided that the following conditions are met:
// 	* Redistributions of source code must retain the above copyright
// 	  notice, this list of conditions and the following disclaimer.
// 	* Redistributions in binary form must reproduce the above copyright
// 	  notice, this list of conditions and the following disclaimer in the
// 	  documentation and/or other materials provided with the distribution.
// 	* Neither the name of Doxense nor the
// 	  names of its contributors may be used to endorse or promote products
// 	  derived from this software without specific prior written permission.
// 
// THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND
// ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
// WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
// DISCLAIMED. IN NO EVENT SHALL DOXENSE BE LIABLE FOR ANY
// DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
// (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
// LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
// ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
// (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
// SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
#endregion

#nullable enable

namespace Doxense.Testing
{
	using System;
	using System.Collections.Generic;
	using System.Diagnostics;
	using System.Globalization;
	using System.IO;
	using System.Linq;
	using System.Linq.Expressions;
	using System.Net;
	using System.Net.NetworkInformation;
	using System.Reflection;
	using System.Runtime;
	using System.Runtime.CompilerServices;
	using System.Runtime.InteropServices;
	using System.Text;
	using System.Threading;
	using System.Threading.Tasks;
	using System.Xml;
	using Doxense.Diagnostics;
	using Doxense.Diagnostics.Contracts;
	using Doxense.Mathematics.Statistics;
	using Doxense.Reactive.Disposables;
	using Doxense.Runtime.Comparison;
	using Doxense.Serialization;
	using Doxense.Serialization.Json;
	using Doxense.Tools;
	using JetBrains.Annotations;
	using Microsoft.Extensions.Logging;
	using NodaTime;
	using NUnit.Framework;
	using NUnit.Framework.Api;
	using NUnit.Framework.Interfaces;
	using NUnit.Framework.Internal;

	/// <summary>Base classe pour les tests unitaires, fournissant toute une série de services (logging, cancellation, async helpers, ...)</summary>
	[DebuggerNonUserCode]
	public abstract class DoxenseTest
	{
		private Stopwatch? m_testTimer;
		private Instant m_testStart;
		private CancellationTokenSource? m_cts;

		public NodaTime.IClock Clock { get; set; } = SystemClock.Instance;

		static DoxenseTest()
		{
#if !NETFRAMEWORK
			// nécessaire pour que .NET Core puisse gérer les CodePages windows (1252, ...)
			System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);
#endif

			////note: lorsque le code est lancé via un profiler (ex: dotTrace) la dll est réécrite dans un autre rep que l'original
			//// j'ai remarqué que GetExecutingAssembly().CodeBase = "file:\C:\path\to\original\bin\Debug\foo.dll" donc je l'utilise comme base
			//// => on part du principe que les librairies natives ont été copiées dans le même répertoire par msbuild !
			//string nativePath = PathEnv.GetAssemblyLocation(Assembly.GetExecutingAssembly());
			//Debug.WriteLine($"Using the following path for native libraries: {nativePath}");
			//// %lib% est le path de base....
			//PathEnv.MapPathVariable("lib", nativePath);
			//// %lib_arch% est le sous répertoire de ce rep correspondant à l'architecture (en minuscule)
			//PathEnv.MapPathVariable("lib_arch", Path.Combine(nativePath, PathEnv.GetArchitectureName()));

			// warmup stopwatch, utilisé dans les benchs...
			RobustBenchmark.Warmup(); // StopWatch, RobustBenchmark, ...

			// warmup JSON
			CrystalJson.Warmup();

			if (!GCSettings.IsServerGC)
			{
				WriteToLog("#########################################################################");
				WriteToLog("WARN: Server GC is *NOT* enabled! Performance may be slower than expected");
				WriteToLog("#########################################################################");
				WriteToLog("");
				//note: il faut configurer l'App.Config du process pour activer le server concurrent gc!
				//note2: si vous lancez les tests avec R# ou TD.NET, il faut hacker les .exe.config de leurs runners NUnits respectifs!
			}
		}

		[OneTimeSetUp][DebuggerNonUserCode]
		public void BeforeEverything()
		{
			Thread.CurrentThread.CurrentCulture = System.Globalization.CultureInfo.InvariantCulture;
			Thread.CurrentThread.CurrentUICulture = System.Globalization.CultureInfo.InvariantCulture;

			WriteToLog($"### {this.GetType().FullName} [START] @ {DateTime.Now.TimeOfDay}");
			try
			{
				OnBeforeEverything();
			}
			catch (Exception e)
			{
				LogError($"#### {this.GetType().FullName} [SETUP CRASH] => " + e);
				throw;
			}
		}

		protected virtual void OnBeforeEverything()
		{ }

		[OneTimeTearDown][DebuggerNonUserCode]
		public void AfterEverything()
		{
			//WriteToLog($"### {this.GetType().FullName} [TEARDOWN] @ {DateTime.Now.TimeOfDay}");
			try
			{
				OnAfterEverything();
			}
			catch (Exception e) when (!e.IsFatalError())
			{
				LogError($"#### {this.GetType().FullName} [CLEANUP CRASH] => " + e);
				throw;
			}
			finally
			{
				WriteToLog($"### {this.GetType().FullName} [{TestContext.CurrentContext.Result.Outcome}] ({TestContext.CurrentContext.Result.FailCount} failed) @ {DateTime.Now.TimeOfDay}");
			}
		}

		protected virtual void OnAfterEverything()
		{ }

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
				LogError($"### {TestContext.CurrentContext.Test.FullName} [BEFORE CRASH] => " + e);
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

				OnAfterEachTest();
			}
			catch (Exception e)
			{
				LogError($"### {currentContext.Test.FullName} [AFTER CRASH] => " + e);
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

		/// <summary>Effectue un full GC</summary>
		[DebuggerNonUserCode]
		// ReSharper disable once InconsistentNaming
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
			Task.Delay(delay, cts.Token).ContinueWith((t) =>
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
			await handler();
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
			var result = await handler();
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
		public Task Wait(TimeSpan delay)
		{
			return Task.Delay(delay, this.Cancellation);
		}

		/// <summary>Spin de manière asynchrone jusqu'à ce qu'une condition soit réalisée, l'expiration d'un timeout, ou l'annulation du test</summary>
		public Task<TimeSpan> WaitUntil([InstantHandle] Func<bool> condition, TimeSpan timeout, string message, TimeSpan? ticks = null, [CallerArgumentExpression("condition")] string? conditionExpression = null)
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

		public Task<TimeSpan> WaitUntilEqual<TValue>([InstantHandle] Func<TValue> condition, TValue comparand, TimeSpan timeout, string? message, TimeSpan? ticks = null, IEqualityComparer<TValue> comparer = null, [CallerArgumentExpression("condition")] string? conditionExpression = null)
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
		public async Task<TimeSpan> WaitUntil([InstantHandle] Func<bool> condition, TimeSpan timeout, Action<TimeSpan, Exception?>  onFail, TimeSpan? ticks = null, [CallerArgumentExpression("condition")] string? conditionExpression = null)
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

					await Task.Delay(delay, this.Cancellation);
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
				await OnWaitOperationCompleted("WaitUntil", conditionExpression!, success, error, start, end);
			}
		}

		/// <summary>Spin de manière asynchrone jusqu'à ce qu'une condition soit réalisée, l'expiration d'un timeout, ou l'annulation du test</summary>
		public async Task<TimeSpan> WaitUntil([InstantHandle] Func<Task<bool>> condition, TimeSpan timeout, string message, TimeSpan? ticks = null, [CallerArgumentExpression("condition")] string conditionExpression = null)
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
						if (await condition())
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

					await Task.Delay(delay, this.Cancellation);
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
				await OnWaitOperationCompleted("WaitUntil", conditionExpression, success, error, start, end);
			}
		}

		protected virtual Task OnWaitOperationCompleted(string operation, string conditionExpression, bool success, Exception? error, Instant startedAt, Instant endedAt)
		{
			return Task.CompletedTask;
		}

		#endregion

		#region Async Stuff...

		/// <summary>Attend que toutes les tasks soient exécutées, ou que le timeout d'exécution du test se déclenche</summary>
		public Task WhenAll(IEnumerable<Task> tasks, TimeSpan timeout, [CallerArgumentExpression("tasks")] string? tasksExpression = null)
		{
			var ts = (tasks as Task[]) ?? tasks.ToArray();
			if (m_cts?.IsCancellationRequested ?? false) return Task.FromCanceled(m_cts.Token);
			if (ts.Length == 0) return Task.CompletedTask;
			if (ts.Length == 1 && ts[0].IsCompleted) return ts[0];
			return WaitForInternal(Task.WhenAll(ts), timeout, throwIfExpired: true, $"WhenAll({tasksExpression})");
		}

		/// <summary>Attend que toutes les tasks soient exécutées, ou que le timeout d'exécution du test se déclenche</summary>
		public async Task<TResult[]> WhenAll<TResult>(IEnumerable<Task<TResult>> tasks, TimeSpan timeout, [CallerArgumentExpression("tasks")] string? tasksExpression = null)
		{
			var ts = (tasks as Task<TResult>[]) ?? tasks.ToArray();
			this.Cancellation.ThrowIfCancellationRequested();
			if (ts.Length == 0) return Array.Empty<TResult>();
			await WaitForInternal(Task.WhenAll(ts), timeout, throwIfExpired: true, $"WhenAll({tasksExpression})").ConfigureAwait(false);
			var res = new TResult[ts.Length];
			for (int i = 0; i < res.Length; i++)
			{
				res[i] = ts[i].Result;
			}
			return res;
		}

		/// <summary>Attend que la task s'exécute, avec un délai d'attente maximum, ou que le timeout d'exécution du test se déclenche</summary>
		public Task WaitFor(Task task, int timeoutMs, [CallerArgumentExpression("task")] string? taskExpression = null) //REVIEW: renommer en "Await" ?
		{
			return WaitFor(task, TimeSpan.FromMilliseconds(timeoutMs), taskExpression!);
		}

		/// <summary>Attend que la task s'exécute, avec un délai d'attente maximum, ou que le timeout d'exécution du test se déclenche</summary>
		public Task WaitFor(ValueTask task, int timeoutMs, [CallerArgumentExpression("task")] string? taskExpression = null) //REVIEW: renommer en "Await" ?
		{
			return WaitFor(task, TimeSpan.FromMilliseconds(timeoutMs), taskExpression!);
		}

		/// <summary>Attend que la task s'exécute, avec un délai d'attente maximum, ou que le timeout d'exécution du test se déclenche</summary>
		public Task WaitFor(Task task, TimeSpan timeout, [CallerArgumentExpression("task")] string? taskExpression = null) //REVIEW: renommer en "Await" ?
		{
			return m_cts?.IsCancellationRequested == true ? Task.FromCanceled<bool>(m_cts.Token)
			     : task.IsCompleted ? Task.FromResult(true)
			     : WaitForInternal(task, timeout, throwIfExpired: true, taskExpression!);
		}

		/// <summary>Attend que la task s'exécute, avec un délai d'attente maximum, ou que le timeout d'exécution du test se déclenche</summary>
		public Task WaitFor(ValueTask task, TimeSpan timeout, [CallerArgumentExpression("task")] string? taskExpression = null) //REVIEW: renommer en "Await" ?
		{
			return m_cts?.IsCancellationRequested == true ? Task.FromCanceled<bool>(m_cts.Token)
				: task.IsCompleted ? Task.FromResult(true)
				: WaitForInternal(task.AsTask(), timeout, throwIfExpired: true, taskExpression!);
		}

		/// <summary>Attend que la task s'exécute, avec un délai d'attente maximum, ou que le timeout d'exécution du test se déclenche</summary>
		public Task<TResult> WaitFor<TResult>(Task<TResult> task, int timeoutMs, [CallerArgumentExpression("task")] string? taskExpression = null)
		{
			return WaitFor(task, TimeSpan.FromMilliseconds(timeoutMs), taskExpression);
		}

		/// <summary>Attend que la task s'exécute, avec un délai d'attente maximum, ou que le timeout d'exécution du test se déclenche</summary>
		public Task<TResult> WaitFor<TResult>(ValueTask<TResult> task, int timeoutMs, [CallerArgumentExpression("task")] string? taskExpression = null)
		{
			return WaitFor(task, TimeSpan.FromMilliseconds(timeoutMs), taskExpression);
		}

		/// <summary>Attend que la task s'exécute, avec un délai d'attente maximum, ou que le timeout d'exécution du test se déclenche</summary>
		public Task<TResult> WaitFor<TResult>(Task<TResult> task, TimeSpan timeout, [CallerArgumentExpression("task")] string? taskExpression = null)
		{
			return m_cts?.IsCancellationRequested == true  ? Task.FromCanceled<TResult>(m_cts.Token)
				: task.IsCompleted ? task
				: WaitForInternal(task, timeout, taskExpression!);
		}

		/// <summary>Attend que la task s'exécute, avec un délai d'attente maximum, ou que le timeout d'exécution du test se déclenche</summary>
		public Task<TResult> WaitFor<TResult>(ValueTask<TResult> task, TimeSpan timeout, [CallerArgumentExpression("task")] string? taskExpression = null)
		{
			return m_cts?.IsCancellationRequested == true  ? Task.FromCanceled<TResult>(m_cts.Token)
				: task.IsCompleted ? task.AsTask()
				: WaitForInternal(task.AsTask(), timeout, taskExpression!);
		}

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
					var ex = task.Exception.Unwrap()!;
					error = ex;
					Assert.Fail($"Task '{taskExpression}' failed with following error: {ex}");
				}

				success = true;
				return true;
			}
			finally
			{
				var end = this.Clock.GetCurrentInstant();
				await OnWaitOperationCompleted("WaitFor", taskExpression, success, error, start, end);
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
						Assert.Fail("Task did not complete in time ({0})", task.Status);
					}
				}

				// return result or throw error
				try
				{
					return await task;
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
				await OnWaitOperationCompleted("WaitFor", taskExpression, success ?? true, error, start, end);
			}
		}

		#endregion

		#region Files & Paths...

		/// <summary>[DANGEROUS] Retourne le chemin vers un fichier qui se trouve dans le code source du projet de test</summary>
		/// <returns>Chemin absolu vers le fichier correspondant</returns>
		public string MapPathInSource(params string[] paths)
		{
			return MapPathRelativeToProject(paths.Length == 1 ? paths[0] : Path.Combine(paths), this.GetType().Assembly);
		}

		/// <summary>[TEST ONLY] Génère le chemin d'un fichier à l'intérieur du projet actuellement en cours de debug dans Visual Studio</summary>
		/// <param name="relativePath">Chemin relatif (ex: "Foo/bar.png")</param>
		/// <param name="assembly">Assembly correspondant au projet (si null, utilise l'Assembly qui appelle cette méthode)</param>
		/// <returns>"C:\Path\To\VisualStudio\Solution_X\Project_Y\Foo\bar.png"</returns>
		/// <remarks>Cette fonction supprime ce qui est après "\bin\..." dans le chemin de l'assembly.</remarks>
		public string MapPathRelativeToProject(string relativePath, Assembly assembly)
		{
			Contract.NotNull(relativePath);
			Contract.NotNull(assembly);
			var res = Path.GetFullPath(Path.Combine(GetCurrentVsProjectPath(assembly), relativePath));
#if DEBUG
			Log("# MapPathInSource(\"{0}\", {1}) => {2}", relativePath, assembly.GetName().Name, res);
#endif
			return res;
		}

		/// <summary>Essayes de déterminer le chemin de le Project Visual Studio actuellement en cours de debuggage</summary>
		private static string GetCurrentVsProjectPath(Assembly assembly)
		{
			Contract.Debug.Requires(assembly != null);

			string path = GetAssemblyLocation(assembly);

			// Si on est en mode DEBUG dans Visual Studio, le path ressemble "X:\.....\Project\bin\Debug",
			// Les prefix possibles sont soit "*\bin\Debug\" pour AnyCPU, ou aussi "*\bin\x86\Debug\" pour x86

			int p = path.LastIndexOf("\\bin\\", StringComparison.Ordinal);
			if (p > 0)
			{
				path = path.Substring(0, p);
			}

			// Si on est dans TeamCity, le path ressemble a "X:\...\work\{guid}\Service\Binaries",
			if (path.Contains(@"\work\"))
			{ // TeamCity
				p = path.LastIndexOf(@"\Binaries", StringComparison.Ordinal);
				if (p > 0) return path.Substring(0, p);
			}

			return path;
		}

		private static string GetAssemblyLocation(Assembly assembly)
		{
#if NETFRAMEWORK
			string? path = assembly.CodeBase;
			if (string.IsNullOrEmpty(path)) path = assembly.Location;
#else
			string path = assembly.Location;
#endif
			if (string.IsNullOrEmpty(path)) throw new InvalidOperationException($"Failed to get location of assembly {assembly}");

			// Resharper sometimes returns @"file:///xxxxx" or @"file:\xxxx"
			if (path.StartsWith("file:\\", StringComparison.OrdinalIgnoreCase))
			{
				path = path.Substring(6);
			}
			else if (path.StartsWith("file:///", StringComparison.OrdinalIgnoreCase))
			{
				path = path.Substring(8);
			}

			path = Path.GetDirectoryName(path) ?? ".";
			return path;
		}

		/// <summary>[TEST ONLY] Génère le chemin vers un fichier, relatif au chemin du fichier source qui appele cette méthode</summary>
		/// <param name="relativePath">Path, relative to the current source path, of the target file or folder</param>
		/// <param name="path">DO NOT SPECIFY!!! Compiler will fill this with the correct path!</param>
		public string MapPathRelativeToCallerSourcePath(string relativePath, [CallerFilePath] string? path = null)
		{
			if (string.IsNullOrEmpty(relativePath)) relativePath = ".";
			return Path.Combine(Path.GetDirectoryName(path)!, relativePath);
		}


		/// <summary>Retourne un chemin vers un répertoire temporaire, utilisable pour ce test</summary>
		/// <param name="relative">Si non null, nom de fichier ou chemin ajouté au path généra. Par convention, terminer par un '/' si c'est un folder, afin de pouvoir plus facilement s'y retrouver!</param>
		/// <returns>Chemin vers un répertoire temporaire, qui est garantit comme existant.</returns>
		/// <remarks>ATTENTION: si vous voulez effacer le répertoire à chaque fois, pensez à passer une chaine unique dans <paramref name="relative"/> pour éviter de vider aussi le rep d'autres test (rend difficile le diag de pb après coup)</remarks>
		/// <example>
		/// GetTemporaryPath() => "c:\blah\workdir\testoutput\FooBarFacts"
		/// GetTemporaryPath("Zorglub") => "c:\blah\workdir\testoutput\FooBarFacts\Zorglub"
		/// GetTemporaryPath("Zorglub/") => "c:\blah\workdir\testoutput\FooBarFacts\Zorglub\"
		/// GetTemporaryPath("Frobs.zip") => "c:\blah\workdir\testoutput\FooBarFacts\Frobs.zip"
		/// </example>
		public string GetTemporaryPath(string? relative = null, bool clearFiles = false)
		{
			// on veut construire: "{WORK_DIR}/{TEST_CLASS_NAME}[/sufix]"

			var context = TestContext.CurrentContext;
			string basePath = context != null ? context.TestDirectory : Environment.CurrentDirectory;
			if (basePath.IndexOf(@"\bin\Debug", StringComparison.OrdinalIgnoreCase) > 0 || basePath.IndexOf(@"\bin\Release", StringComparison.OrdinalIgnoreCase) > 0)
			{
				basePath = Path.Combine(basePath, "TestOutput");
			}

			// récupère le nom de la classe courante
			string path = Path.Combine(basePath, this.GetType().Name.Replace(".", "_").Replace("`", "_"));

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
			{ // c'est un folder, il doit exister!

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
			{ // c'est un fichier, son parent doit exister!
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

		// Quand on est exécuté depuis VS en mode debug on préfère écrire dans Trace. Sinon, on écrit dans la Console...
		protected static readonly bool AttachedToDebugger = Debugger.IsAttached;

		/// <summary>Force les logs vers Console.Out/Error, plutot que TestContext.Progress</summary>
		private static TextWriter? ForceToConsole;
		private static TextWriter? ForceToConsoleError;

		/// <summary>Indique si on fonctionne sous un runner qui préfère utiliser la console pour l'output des logs</summary>
		public static readonly bool MustOutputLogsOnConsole = DetectConsoleTestRunner();

		private static bool DetectConsoleTestRunner()
		{
			// TeamCity
			if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("TEAMCITY_VERSION"))) return true;

			string? host = Assembly.GetEntryAssembly()?.GetName().Name;
			return host == "TestDriven.NetCore.AdHoc" // TestDriven.NET
				|| host == "testhost";                // ReSharper Test Runner
		}

		[DebuggerNonUserCode]
		protected static void WriteToLog(string? message, bool lineBreak = true)
		{
			if (MustOutputLogsOnConsole || ForceToConsole != null)
			{ // force sur la console
				if (lineBreak)
					(ForceToConsole ?? Console.Out).WriteLine(message);
				else
					(ForceToConsole ?? Console.Out).Write(message);
			}
			else if (AttachedToDebugger)
			{ // écrit dans la console 'output' de VS
				if (lineBreak)
					Trace.WriteLine(message);
				else
					Trace.Write(message);
			}
			else
			{ // écrit dans stdout
				//note: avant NUnit 3.6, il fallait XML encoder les logs, mais c'est fixed en 3.6.0 (cf https://github.com/nunit/nunit/issues/1891)
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
			if (MustOutputLogsOnConsole || ForceToConsoleError != null)
			{ // force sur la console
				(ForceToConsoleError ?? Console.Error).WriteLine(message);
			}
			else if(AttachedToDebugger)
			{ // écrit dans la console 'output' de VS
				Trace.WriteLine("ERROR: " + message);
				TestContext.Error.WriteLine(message);
			}
			else
			{ // écrit dans stderr de NUnit
				TestContext.Error.WriteLine(message);
			}
		}

		protected void LogElapsed(string? text)
		{
			Log(TestElapsed.ToString() + " " + text);
		}

		[DebuggerNonUserCode, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void Log(string? text)
		{
			WriteToLog(text);
		}

		public static void Log(ref DefaultInterpolatedStringHandler handler)
		{
			WriteToLog(handler.ToStringAndClear());
		}

		protected static void DumpStackTrace(int skip = 2)
		{
			var stack = Environment.StackTrace.Split('\n');

			// drop the bottom of the stack (System and NUnit stuff...
			int last = stack.Length - 1;
			while(last > skip && (stack[last].IndexOf("   at System.", StringComparison.Ordinal) >= 0 || stack[last].IndexOf("   at NUnit.Framework.", StringComparison.Ordinal) >= 0))
			{
				--last;
			}

			Log("> " + string.Join("\n> ", stack, skip, last - skip + 1));
		}

		/// <summary>Convertit un object en JSON tenant sur une seule ligne</summary>
		protected static string Jsonify(object? item)
		{
			return CrystalJson.Serialize(item, CrystalJsonSettings.Json);
		}

		/// <summary>Retourne une représentation textuelle basique d'un object, tenant sur une seule ligne</summary>
		protected static string Stringify(object? item)
		{
			switch (item)
			{
				case null: // Null
					return "<null>";
				case string str:
					// hack pour empecher les CRLF de casser l'affichage
					return str.Length == 0
						? "\"\""
						: "\"" + str.Replace(@"\", @"\\").Replace("\r", @"\r").Replace("\n", @"\n").Replace("\0", @"\0").Replace(@"""", @"\""") + "\"";
				case int i:
					return i.ToString(CultureInfo.InvariantCulture);
				case long l:
					return l.ToString(CultureInfo.InvariantCulture) + "L";
				case uint ui:
					return ui.ToString(CultureInfo.InvariantCulture) + "U";
				case ulong ul:
					return ul.ToString(CultureInfo.InvariantCulture) + "UL";
				case double d:
					return d.ToString("R", CultureInfo.InvariantCulture);
				case float f:
					return f.ToString("R", CultureInfo.InvariantCulture) + "F";
				case Guid g:
					return "{" + g.ToString() + "}";
				case JsonValue json:
					return json.ToJson();
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
				// utilise le format le plus adapté en fonction du type
				string? fmt = null;
				if (item is int || item is uint || item is long || item is ulong)
				{
					fmt = "N0";
				}
				else if (item is double || item is float)
				{
					fmt = "R";
				}
				else if (item is DateTime || item is DateTimeOffset)
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

		[DebuggerNonUserCode]
		public static void Log(object? item)
		{
			Log(item as string ?? Stringify(item));
		}

		[DebuggerNonUserCode]
		protected static void LogPartial(string? text)
		{
			WriteToLog(text, lineBreak: false);
		}

		[DebuggerNonUserCode]
		public static void Log()
		{
			WriteToLog(string.Empty);
		}

		[DebuggerNonUserCode]
		[StringFormatMethod("format")]
		public static void Log(string format, object? arg0)
		{
			WriteToLog(string.Format(CultureInfo.InvariantCulture, format, arg0));
		}

		[DebuggerNonUserCode]
		[StringFormatMethod("format")]
		public static void Log(string format, object? arg0, object? arg1)
		{
			WriteToLog(string.Format(CultureInfo.InvariantCulture, format, arg0, arg1));
		}

		[DebuggerNonUserCode]
		[StringFormatMethod("format")]
		public static void Log(string format, params object?[] args)
		{
			WriteToLog(string.Format(CultureInfo.InvariantCulture, format, args));
		}

		[DebuggerNonUserCode]
		public static void LogInv(FormattableString msg)
		{
			WriteToLog(msg.ToString(CultureInfo.InvariantCulture));
		}

		[DebuggerNonUserCode]
		public static void LogError(string? text)
		{
			WriteToErrorLog(text);
		}

		[DebuggerNonUserCode]
		public static void LogError(string? text, Exception e)
		{
			WriteToErrorLog(text + Environment.NewLine + e.ToString());
		}

		[DebuggerNonUserCode]
		public static void LogErrorInv(FormattableString msg)
		{
			WriteToErrorLog(msg.ToString(CultureInfo.InvariantCulture));
		}

		protected virtual ILoggerFactory Loggers => TestLoggerFactory.Instance;

		private class TestLoggerFactory : ILoggerFactory
		{

			public static readonly ILoggerFactory Instance = new TestLoggerFactory();

			public void Dispose() { }

			public Microsoft.Extensions.Logging.ILogger CreateLogger(string categoryName)
			{
				return new TestLogger(categoryName);
			}

			public void AddProvider(ILoggerProvider provider)
			{
				//
			}
		}

		internal class TestLogger : Microsoft.Extensions.Logging.ILogger
		{

			public string Category { get; }

			private string Prefix { get; }

			public TestLogger(string category)
			{
				this.Category = category;
				this.Prefix = "[" + category + "] ";
			}

			public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
			{
				string msg = this.Prefix + formatter(state, exception);
				switch (logLevel)
				{
					case LogLevel.Trace:
					case LogLevel.Debug:
					case LogLevel.Information:
					{
						DoxenseTest.Log(msg);
						break;
					}
					case LogLevel.Warning:
					case LogLevel.Error:
					case LogLevel.Critical:
					{
						DoxenseTest.LogError(msg);
						break;
					}
				}
			}

			public bool IsEnabled(LogLevel logLevel)
			{
				return true;
			}

			public IDisposable BeginScope<TState>(TState state)
			{
				return Disposable.Create(() => {});
			}
		}

		#endregion

		#region Dump JSON...

		[DebuggerNonUserCode]
		public static void Dump(JsonValue? value)
		{
			WriteToLog(value?.ToJson(CrystalJsonSettings.JsonIndented) ?? "<null>");
		}

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
			{ // vector de nombres!
				WriteToLog(value.ToJson(CrystalJsonSettings.Json));
				return;
			}
			WriteToLog(value.ToJson(CrystalJsonSettings.JsonIndented));
		}

		[DebuggerNonUserCode]
		public static void Dump(JsonObject? value)
		{
			WriteToLog(value?.ToJson(CrystalJsonSettings.JsonIndented) ?? "<null>");
		}

		[DebuggerNonUserCode]
		public static void DumpXml(string xmlText, string? label = null, int indent = 0)
		{
			var doc = new XmlDocument();
			doc.LoadXml(xmlText);
			DumpXml(doc, label, indent);
		}

		[DebuggerNonUserCode]
		public static void DumpXml(XmlDocument? doc, string? label = null, int indent = 0)
		{
			DumpXml(doc?.DocumentElement, label, indent);
		}

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
				writer.WriteNode(node.CreateNavigator(), true);
			}

			string xml = sb.ToString();
			if (indent > 0)
			{ // indentation du pauvre
				xml = xml.Replace("\n", "\n" + new string('\t', indent));
			}
			Log(xml);
		}

		[DebuggerNonUserCode]
		public static void DumpXml(System.Xml.Linq.XDocument? doc, string? label = null, int indent = 0)
		{
			DumpXml(doc?.Root, label, indent);
		}

		public static void DumpXml(System.Xml.Linq.XElement? node, string? label = null, int indent = 0)
		{
			if (node == null) { Log($"{label}: <null>"); return; }

			//TODO: formatter proprement le XML!
			if (label != null) Log($"{label}:");

			string xml = node.ToString();
			if (indent > 0)
			{ // indentation du pauvre
				xml = xml.Replace("\n", "\n" + new string('\t', indent));
			}
			Log(xml);
		}

		protected static bool IsTaskLike(Type t)
		{
			if (typeof(Task).IsAssignableFrom(t)) return true;
			if (t == typeof(ValueTask)) return true;
			//TODO: ValueTask<...>
			return false;
		}

		[Obsolete("You forgot to await the task!", error: true)]
		protected static void Dump<T>(Task<T> value)
		{
			Assert.Fail($"Cannot dump the content of a {typeof(T).GetFriendlyName()}! Most likely you work to 'await' the method that produced this value!");
		}

		/// <summary>Dump une valeur en JSON dans le log du test</summary>
		/// <remarks>ATTENTION: il faut que le type soit sérialisable en JSON! Ne marchera pas avec n'importe quel objet, surtout s'il y a des références cycliques</remarks>
		[DebuggerNonUserCode]
		public static void Dump<T>(T value)
		{
			if (IsTaskLike(typeof(T))) Assert.Fail($"Cannot dump the content of a {typeof(T).GetFriendlyName()}! Most likely you work to 'await' the method that produced this value!");

			WriteToLog(CrystalJson.Serialize<T>(value, CrystalJsonSettings.JsonIndented.WithNullMembers().WithEnumAsStrings()));
		}

		/// <summary>Dump une valeur en JSON dans le log du test</summary>
		/// <remarks>ATTENTION: il faut que le type soit sérialisable en JSON! Ne marchera pas avec n'importe quel objet, surtout s'il y a des références cycliques</remarks>
		[DebuggerNonUserCode]
		public static void Dump<T>(string label, T value)
		{
			WriteToLog($"{label}: <{(value != null ? value.GetType() : typeof(T)).GetFriendlyName()}>");
			WriteToLog(CrystalJson.Serialize<T>(value, CrystalJsonSettings.JsonIndented.WithEnumAsStrings()));
		}

		/// <summary>Dump une valeur en JSON dans le log du test, de manière compacte</summary>
		/// <remarks>ATTENTION: il faut que le type soit sérialisable en JSON! Ne marchera pas avec n'importe quel objet, surtout s'il y a des références cycliques</remarks>
		[DebuggerNonUserCode]
		public static void DumpCompact<T>(T value)
		{
			WriteToLog(CrystalJson.Serialize(value, CrystalJsonSettings.Json));
		}

		/// <summary>Dump une valeur en JSON dans le log du test, de manière compacte</summary>
		/// <remarks>ATTENTION: il faut que le type soit sérialisable en JSON! Ne marchera pas avec n'importe quel objet, surtout s'il y a des références cycliques</remarks>
		[DebuggerNonUserCode]
		public static void DumpCompact<T>(string label, T value)
		{
			WriteToLog($"{label,-10}: {CrystalJson.Serialize(value, CrystalJsonSettings.Json)}");
		}

		/// <summary>Dump les différences observées entre deux instances d'un même type</summary>
		/// <typeparam name="T">Types des objets</typeparam>
		/// <param name="actual">Objet observé</param>
		/// <param name="expected">Objet attendu</param>
		/// <returns>Return <c>true</c> si au moins une différence a été observée, ou <c>false</c> si les objets sont équivalent</returns>
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

		[DebuggerNonUserCode]
		public static void DumpHexa(byte[] buffer, HexaDump.Options options = HexaDump.Options.Default)
		{
			DumpHexa(buffer.AsSlice(), options);
		}

		[DebuggerNonUserCode]
		public static void DumpHexa(Slice buffer, HexaDump.Options options = HexaDump.Options.Default)
		{
			WriteToLog(HexaDump.Format(buffer, options), lineBreak: false);
		}

		[DebuggerNonUserCode]
		public static void DumpHexa(ReadOnlySpan<byte> buffer, HexaDump.Options options = HexaDump.Options.Default)
		{
			WriteToLog(HexaDump.Format(buffer, options), lineBreak: false);
		}

		[DebuggerNonUserCode]
		public static void DumpHexa<T>(ReadOnlySpan<T> array, HexaDump.Options options = HexaDump.Options.Default)
			where T : struct
		{
			WriteToLog($"Dumping memory content of {typeof(T).GetFriendlyName()}[{array.Length:N0}]:");
			WriteToLog(HexaDump.Format(MemoryMarshal.AsBytes(array), options), lineBreak: false);
		}

		[DebuggerNonUserCode]
		public static void DumpVersus(byte[] left, byte[] right)
		{
			DumpVersus(left.AsSlice(), right.AsSlice());
		}

		[DebuggerNonUserCode]
		public static void DumpVersus(Slice left, Slice right)
		{
			WriteToLog(HexaDump.Versus(left, right), lineBreak: false);
		}

		[DebuggerNonUserCode]
		public static void DumpVersus(ReadOnlySpan<byte> left, ReadOnlySpan<byte> right)
		{
			WriteToLog(HexaDump.Versus(left, right), lineBreak: false);
		}

		#endregion

		#region Simple Test Runners...

		// code permettant d'exécuter un test NUnit directement depuis un void Main(), sans devoir passer par un Test Runner externe

		private static void WriteToConsole(TextWriter writer, string msg, ConsoleColor? color = null)
		{
			ConsoleColor? prev = null;
			if (color != null)
			{
				prev = Console.ForegroundColor;
				if (prev != color) Console.ForegroundColor = color.Value;
			}
			writer.WriteLine(msg);
			if (prev != null && prev != color) Console.ForegroundColor = prev.Value;
		}

		/// <summary>Run a unit test directly from your Main() entry point</summary>
		/// <typeparam name="TTest">Type of the Test Fixture</typeparam>
		/// <param name="run">Lambda expression that invokes the test method to execute. Should be of the form '(test) => test.TheTestMethod()'</param>
		/// <param name="waitForKey">If true, the runner will first execute the setup, then wait for a keypress before executing the test method itself. This can be used to attach a debugger or start a profile trace at the right spot.</param>
		/// <example>DoxenseTest.RunTest&lt;MyTestClass>((t) => t.MyTestMethod());</example>
		public static void RunTest<TTest>(Expression<Func<TTest, Task>> run, bool waitForKey = false)
			where TTest : DoxenseTest, new()
		{
			var method = ((MethodCallExpression) run.Body).Method;
			var fixtureType = typeof(TTest);

			RunTest(fixtureType, method.Name);
		}

		/// <summary>Run a unit test directly from your Main() entry point</summary>
		/// <typeparam name="TTest">Type of the Test Fixture</typeparam>
		/// <param name="run">Lambda expression that invokes the test method to execute. Should be of the form '(test) => test.TheTestMethod()'</param>
		/// <param name="waitForKey">If true, the runner will first execute the setup, then wait for a keypress before executing the test method itself. This can be used to attach a debugger or start a profile trace at the right spot.</param>
		/// <example>DoxenseTest.RunTest&lt;MyTestClass>((t) => t.MyTestMethod());</example>
		public static void RunTest<TTest>(Expression<Action<TTest>> run, bool waitForKey = false)
			where TTest : DoxenseTest, new()
		{
			var method = ((MethodCallExpression) run.Body).Method;
			var fixtureType = typeof(TTest);

			RunTest(fixtureType, method.Name);
		}

		/// <summary>Run a unit test directly from your Main() entry point</summary>
		/// <param name="fixtureType">Type of the test class</param>
		/// <param name="methodName">Name of the test method</param>
		/// <param name="waitForKey">If true, the runner will first execute the setup, then wait for a keypress before executing the test method itself. This can be used to attach a debugger or start a profile trace at the right spot.</param>
		/// <example>DoxenseTest.RunTest(typeof(MyTestClass), nameof(MyTestClass.MyTestMethod));</example>
		public static void RunTest(Type fixtureType, string methodName, bool waitForKey = false)
		{
			// workaround pour pouvoir obtenir les logs du test (TestContext.Progress ne fonctionne pas)
			var stdOut = ForceToConsole = Console.Out;
			var stdErr = ForceToConsoleError = Console.Error;

			// create a filter that will only execute that one method in the assembly
			var filter = TestFilter.FromXml(new TNode("filter")
			{
				ChildNodes =
				{
					new TNode("and")
					{
						ChildNodes =
						{
							new TNode("class", string.IsNullOrEmpty(fixtureType.Namespace) ? fixtureType.Name : $"{fixtureType.Namespace}.{fixtureType.Name}"),
							new TNode("method", methodName)
						}
					}
				}
			});

			// configure the runner...
			var options = new Dictionary<string, object>();
			//TODO: setup current directory for test? other settings?

			var runner = new NUnitTestAssemblyRunner(new DefaultTestAssemblyBuilder());
			runner.Load(fixtureType.Assembly, options);

			// need a listener that will display the results on the console...
			bool delimiter = true;
			var listener = new AnonymousTestListener
			{
				TestStarted = (test) =>
				{
					if (test.HasChildren) return; // not a test method

					WriteToConsole(stdOut, "// ==================================================================", ConsoleColor.DarkGray);
					WriteToConsole(stdOut, $"// [{test.Name}] started", ConsoleColor.White);
					if (waitForKey)
					{
						WriteToConsole(stdOut, "Test is ready, PRESS A KEY to continue!", ConsoleColor.Yellow);
						Console.ReadKey();
					}
					delimiter = false;
				},
				TestFinished = (result) =>
				{
					if (result.HasChildren) return; // not a test method

					if (delimiter)
					{ // close output block
						WriteToConsole(stdOut, "// ------------------------------------------------------------------", ConsoleColor.DarkGray);
						WriteToConsole(stdOut, "");
					}

					// If failed, dump the assertions and callstack
					if (result.ResultState.Status != TestStatus.Passed)
					{
						WriteToConsole(stdErr, $"// [{result.Name}] {result.ResultState.Status.ToString().ToUpperInvariant()} in {result.Duration:N3}s", ConsoleColor.Red);
						var asserts = result.AssertionResults;
						switch (asserts.Count)
						{
							case 0:
							{
								WriteToErrorLog(result.Message);
								break;
							}
							case 1:
							{
								var assert = asserts[0];
								WriteToErrorLog(assert.Message);
								if (!string.IsNullOrEmpty(assert.StackTrace))
								{
									WriteToErrorLog(assert.StackTrace);
								}
								break;
							}
							default:
							{
								for (int i = 0; i < asserts.Count; i++)
								{
									var assert = asserts[i];
									WriteToConsole(stdErr, $"// Assertion {i + 1:N0}/{result.AssertionResults.Count:N0}: {assert.Status}", ConsoleColor.DarkRed);
									WriteToConsole(stdErr, assert.Message, ConsoleColor.Red);
									if (!string.IsNullOrEmpty(assert.StackTrace))
									{
										WriteToConsole(stdErr, assert.StackTrace, ConsoleColor.DarkRed);
									}
									WriteToConsole(stdErr, "");
								}
								break;
							}
						}
					}
					else
					{
						WriteToConsole(stdOut, $"// [{result.Name}] PASSED in {result.Duration:N3}s", ConsoleColor.Green);
						WriteToConsole(stdOut, string.Empty);
					}
				},
				TestOutput = (output) =>
				{
					if (!delimiter)
					{ // start new output block
						WriteToConsole(stdOut, "");
						WriteToConsole(stdOut, "// ------------------------------------------------------------------", ConsoleColor.DarkGray);
						delimiter = true;
					}
					WriteToConsole(stdOut, output.Text);
				}
			};

			// execute the test
			Log("// Starting...");
			runner.Run(listener, filter);
			Log("// Done");
		}

		public sealed  class AnonymousTestListener : ITestListener
		{

			public Action<ITest> TestStarted { get; set; } = (_) => { };

			public Action<ITestResult> TestFinished { get; set; } = (_) => { };

			public Action<TestOutput> TestOutput { get; set; } = (_) => { };

			void ITestListener.TestStarted(ITest test)
			{
				this.TestStarted?.Invoke(test);
			}

			void ITestListener.TestFinished(ITestResult result)
			{
				this.TestFinished?.Invoke(result);
			}

			void ITestListener.TestOutput(TestOutput output)
			{
				this.TestOutput?.Invoke(output);
			}

			public void SendMessage(TestMessage message)
			{ }
		}

		#endregion

		#region It!

		protected void It(string what, TestDelegate action)
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

		protected async Task It(string what, AsyncTestDelegate action)
		{
			Log($"--- `{what}` {new string('-', Math.Max(0, 60 - what.Length))}");
			var sw = Stopwatch.StartNew();
			try
			{
				await action();
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

		#endregion

	}

}
