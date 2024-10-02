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

	using System.Reflection;
	using System.Runtime.CompilerServices;
	using System.Runtime.InteropServices;
	using System.Text;
	using Doxense.Diagnostics;
	using Doxense.Runtime.Comparison;
	using Doxense.Serialization;
	using Doxense.Serialization.Json;
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
			get => m_timer?.Elapsed ?? TimeSpan.Zero;
		}

		/// <summary>Cancellation token usable by any test</summary>
		protected CancellationToken Cancellation
		{
			[DebuggerStepThrough]
			get
			{
				if (m_cts == null)
				{
					SetupCancellation();
				}
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

			// we are looking for types that are most probably data types that can be safely serialized to JSON
			if (typeof(IJsonSerializable).IsAssignableFrom(type) || IsRecordType(type))
			{
				return $"({type.GetFriendlyName()}) {CrystalJson.Serialize(item)}";
			}

			return $"({type.GetFriendlyName()}) {item.ToString()}";
		}

		protected static bool IsRecordType(Type type)
		{
			// Based on the state of the art as described in https://github.com/dotnet/roslyn/issues/45777
			var cloneMethod = type.GetMethod("<Clone>$", BindingFlags.Public | BindingFlags.Instance);
			return cloneMethod != null && cloneMethod.ReturnType == type;
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

		#region Key/Value Helpers...

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

		#endregion

		#region Read/Write Helpers...

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

		#endregion

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
