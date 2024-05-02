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

namespace Doxense.Testing
{
	using System.Diagnostics;
	using System.IO;
	using System.Reflection;
	using SnowBank.Testing;

	/// <summary>Legacy base class.</summary>
	[DebuggerNonUserCode]
	[Obsolete("Please derive from SimpleTest instead.")]
	public abstract class DoxenseTest : SimpleTest
	{

		// this contains deprecated methods that must be kept around for a little bit more time...

		/// <summary>[DANGEROUS] Computes the path to a file that is located inside the test project</summary>
		/// <returns>Absolute path to the file</returns>
		[Obsolete("This may not be reliable! Please consider using MapPathRelativeToCallerSourcePath(...) instead!")]
		public string MapPathInSource(params string[] paths)
		{
			return MapPathRelativeToProject(paths.Length == 1 ? paths[0] : Path.Combine(paths), this.GetType().Assembly);
		}

		/// <summary>[TEST ONLY] Computes the path to a file inside the project currently tested</summary>
		/// <param name="relativePath">Relative path (ex: "Foo/bar.png")</param>
		/// <param name="assembly">Assembly that corresponds to the test project (if null, will use the calling assembly)</param>
		/// <returns><c>"C:\Path\To\Your\Solution_X\Project_Y\Foo\bar.png"</c></returns>
		/// <remarks>This function will remove the "\bin\Debug\..." bit from the assembly path.</remarks>
		[Obsolete("This may not be reliable! Please consider using MapPathRelativeToCallerSourcePath(...) instead!")]
		public string MapPathRelativeToProject(string relativePath, Assembly assembly)
		{
			Contract.NotNull(relativePath);
			Contract.NotNull(assembly);
			var res = Path.GetFullPath(Path.Combine(GetCurrentVsProjectPath(assembly), relativePath));
#if DEBUG
			Log($"# MapPathInSource(\"{relativePath}\", {assembly.GetName().Name}) => {res}");
#endif
			return res;
		}

		/// <summary>Attempts to compute the path to the root of the project currently tested</summary>
		[Obsolete("This may not be reliable! Please consider using MapPathRelativeToCallerSourcePath(...) instead!")]
		private static string GetCurrentVsProjectPath(Assembly assembly)
		{
			Contract.Debug.Requires(assembly != null);

			string path = GetAssemblyLocation(assembly);

			// If we are running in DEBUG from Visual Studio, the path will look like "X:\..\Solution\src\Project\bin\Debug" or "X:\..\Solution\src\Project\bin\net8.0\Debug\...",
			// The possible prefixes are usually "*\bin\Debug\" for AnyCPU, but also "*\bin\x86\Debug\" for x86

			int p = path.LastIndexOf(@"\bin\", StringComparison.Ordinal);
			if (p > 0)
			{
				path = path[..p];
			}

			// When running inside TeamCity, the path could also look like "X:\...\work\{guid}\Service\Binaries",
			if (path.Contains(@"\work\"))
			{ // TeamCity ?
				p = path.LastIndexOf(@"\Binaries", StringComparison.Ordinal);
				if (p > 0) return path[..p];
			}

			return path;
		}

		#region Simple Test Runners [DEPRECATED]...

#if DEPRECATED // would need to be rewritten for NUnit 4. Do we still use this??

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

#endif

		#endregion

	}

}
