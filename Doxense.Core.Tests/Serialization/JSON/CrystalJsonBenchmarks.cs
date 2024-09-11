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

// ReSharper disable UnusedParameter.Local
// ReSharper disable UnusedMember.Global
// ReSharper disable ReturnValueOfPureMethodIsNotUsed

#define ENABLE_NEWTONSOFT

namespace Doxense.Serialization.Json.Tests
{
	using System.Collections;
	using System.Collections.Generic;
	using System.Diagnostics;
	using System.Globalization;
	using System.IO;
	using System.Linq;
	using System.Runtime.CompilerServices;
	using System.Text;
	using System.Threading;
	using Doxense.Mathematics.Statistics;
	using Doxense.Runtime;
#if ENABLE_NEWTONSOFT
	using NJ = Newtonsoft.Json;
#endif

	[TestFixture]
	[Category("Core-SDK")]
	[Category("Benchmark")]
	[Parallelizable(ParallelScope.All)]
	public class CrystalJsonBenchmarks : SimpleTest
	{

		static CrystalJsonBenchmarks()
		{
			CrystalJson.Warmup();
			PlatformHelpers.PreJit(typeof(CrystalJsonBenchmarks));
#if DEBUG
			Log("WARNING: benchmark compilé en mode DEBUG! Ne pas tenir compte des temps ci-dessous qui ne sont pas représentatifs !");
#endif
		}

		#region Simple Benchmarks...

		[Test]
		[Category("LongRunning")]
		public void Bench_Compare()
		{

			// pour éviter trop d'interférences avec les autres process, on élève la priorité du thread de bench!
			Thread.CurrentThread.Priority = ThreadPriority.Highest;

			// basic types
			ComparativeBenchmark(0, 10000);
			ComparativeBenchmark(123, 10000);
			ComparativeBenchmark(long.MaxValue, 10000);
			ComparativeBenchmark(Math.PI, 10000);
			ComparativeBenchmark("short string", 10000);
			ComparativeBenchmark("really long string that does not contains a double quote, and is trying to consume aaaall your memory even if you have hundreds of zetabytes!", 10000);
			ComparativeBenchmark("really long string that does indeed contain the \" charac, and is trying to consume aaaall your memory even if you have hundreds of zetabytes!", 10000);
			ComparativeBenchmark(DateTime.Now, 10000);
			ComparativeBenchmark(DateTime.UtcNow, 10000);
			ComparativeBenchmark(DateTimeOffset.Now, 10000);
			ComparativeBenchmark(DateTimeOffset.UtcNow, 10000);
			ComparativeBenchmark(Guid.NewGuid(), 10000);

			// simple class / structs
			ComparativeBenchmark(new DummyJsonStruct(), 1000);
			ComparativeBenchmark(new DummyJsonClass(), 1000);
			ComparativeBenchmark(new DummyJsonClass()
			{
				Name = "James Bond",
				Index = 7,
				Size = 123456789,
				Height = 1.8f,
				Amount = 0.07d,
				Created = new DateTime(1968, 5, 8, 0, 0, 0, DateTimeKind.Utc),
				Modified = new DateTime(2010, 10, 28, 15, 39, 0, DateTimeKind.Utc),
				State = DummyJsonEnum.Bar,
			}, 1000);

			ComparativeBenchmark(new[] {
				"Sheldon Cooper by Jim Parsons",
				"Leonard Hofstadter by Johny Galecki",
				"Penny by Kaley Cuoco",
				"Howard Wolowitz by Simon Helberg",
				"Raj Koothrappali by Kunal Nayyar",
			}, 1000);

			ComparativeBenchmark(new[] { 1, 2, 3, 4, 42, 666, 2403, 999999999 }, 1000);
			ComparativeBenchmark(new[] {
				new { Character="Sheldon Cooper", Actor="Jim Parsons", Female=false },
				new { Character="Leonard Hofstadter", Actor="Johny Galecki", Female=false },
				new { Character="Penny", Actor="Kaley Cuoco", Female=true },
				new { Character="Howard Wolowitz", Actor="Simon Helberg", Female=false },
				new { Character="Raj Koothrappali", Actor="Kunal Nayyar", Female=false },
			}, 1000);

			// Objet composite (worst case ?)
			ComparativeBenchmark(new
			{
				Id = 1,
				Title = "The Big Bang Theory",
				Cancelled = false, // (j'espère que c'est toujours le cas ^^; )
				Cast = new[] {
					new { Character="Sheldon Cooper", Actor="Jim Parsons", Female=false },
					new { Character="Leonard Hofstadter", Actor="Johny Galecki", Female=false },
					new { Character="Penny", Actor="Kaley Cuoco", Female=true },
					new { Character="Howard Wolowitz", Actor="Simon Helberg", Female=false },
					new { Character="Raj Koothrappali", Actor="Kunal Nayyar", Female=false },
				},
				Seasons = 4,
				ScoreIMDB = 8.4, // (26/10/2010)
				Producer = "Chuck Lorre Productions",
				PilotAirDate = new DateTime(2007, 9, 24, 0, 0, 0, DateTimeKind.Utc), // plus simple si UTC
			}, 1000);
		}

		private static void ComparativeBenchmark<T>(T? data, int iterations, ICrystalJsonTypeResolver? resolver = null)
		{
			Log();
			Log("### Comparative Benchmark: " + (data == null ? "<null>" : (data.GetType().GetFriendlyName())));
			// warmup!
			var sb = new StringBuilder(1024);
			var settings = CrystalJsonSettings.JsonCompact;
			_ = CrystalJson.Serialize(data, sb, settings, resolver);
			string jsonText = sb.ToString();
			Log("CJS # [" + jsonText.Length + "] " + jsonText);
			object? jsonValue = CrystalJson.Deserialize<T?>(jsonText, default(T));
			Log("CJS > " + jsonValue);

#if ENABLE_NEWTONSOFT
			var njs = new NJ.JsonSerializer();
			bool newtonOk = true;
			string? njsonText = null;
			try
			{
				sb.Clear();
				njs.Serialize(new StringWriter(sb), data);
				njsonText = sb.ToString();
				Log($"NSJ $ [{sb.Length}] {sb}");
				jsonValue = njs.Deserialize<T>(new NJ.JsonTextReader(new StringReader(sb.ToString())));
				Log($"NSJ > {jsonValue}");
			}
			catch (Exception e)
			{
				newtonOk = false;
				LogError("! NSJ FAILED => " + e.Message);
			}
#endif

			// Benchmarking!

			var report = RobustBenchmark.Run(
				(data, settings, resolver),
				static (g) => new StringBuilder(1024),
				static (g, buffer, _) =>
				{
					buffer.Clear();
					return CrystalJson.Serialize(g.data, buffer, g.settings, g.resolver);
				},
				static (g, buffer) => buffer.Length,
				20,
				iterations
			);
			double crystalOps = report.BestIterationsPerSecond;
			Log($"* CRYSTAL   SERIALIZATION: {report.IterationsPerRun:N0} in {report.BestDuration.TotalMilliseconds:F1} ms at {report.BestIterationsPerSecond:N0} op/s ({report.BestIterationsNanos:N0} nanos), {report.Results[0]}");

			report = RobustBenchmark.Run(
				jsonText,
				(text) => CrystalJson.Deserialize(text, default(T)),
				(text) => text.Length,
				20,
				iterations
			);
			double crystalOps2 = report.BestIterationsPerSecond;
			Log($"* CRYSTAL DESERIALIZATION: {report.IterationsPerRun:N0} in {report.BestDuration.TotalMilliseconds:F1} ms at {report.BestIterationsPerSecond:N0} op/s ({report.BestIterationsNanos:N0} nanos)");

#if ENABLE_NEWTONSOFT
			// comparaison avec NewtonSoft.JSON
			if (newtonOk)
			{
				report = RobustBenchmark.Run(
					new StringBuilder(1024),
					(buffer) =>
					{
						buffer.Clear();
						njs.Serialize(new StringWriter(buffer), data);
					},
					(buffer) => buffer.Length,
					20,
					iterations
				);
				double newtonOps = report.BestIterationsPerSecond;
				Log($"* JSON.NET  SERIALIZATION: {report.IterationsPerRun:N0} in {report.BestDuration.TotalMilliseconds:F1} ms at {report.BestIterationsPerSecond:N0} op/s ({report.BestIterationsNanos:N0} nanos)");

				double ratio = (newtonOps - crystalOps) / newtonOps;
				if (ratio == 0)
					Log("-> Result: EX AEQUO !");
				else if (ratio > 0)
					Log($"-> Result: We are {Math.Round(100 * ratio, 1, MidpointRounding.AwayFromZero)} % slower :( => 1 : {(newtonOps / crystalOps):N2}");
				else
					Log($"-> Result: We are {Math.Round(-100 * ratio, 1, MidpointRounding.AwayFromZero)} % faster :) => {(crystalOps / newtonOps):N2} : 1");
			}

			if (newtonOk)
			{
				report = RobustBenchmark.Run(
					njsonText ?? "",
					(text) => njs.Deserialize<T>(new NJ.JsonTextReader(new StringReader(text))),
					(text) => text?.Length ?? 0,
					20,
					iterations
				);
				double newtonOps = report.BestIterationsPerSecond;
				Log("* JSON.NET DESERIALIZATION:" + report.IterationsPerRun.ToString("N0") + " in " + report.BestDuration.TotalMilliseconds.ToString("F1") + " ms at " + report.BestIterationsPerSecond.ToString("N0") + " op/s (" + report.BestIterationsNanos.ToString("N0") + " nanos)");

				double ratio = (newtonOps - crystalOps2) / newtonOps;
				if (ratio == 0)
					Log("-> Result: EX AEQUO !");
				else if (ratio > 0)
					Log($"-> Result: We are {Math.Round(100 * ratio, 1, MidpointRounding.AwayFromZero)} % slower :( => 1 : {(newtonOps / crystalOps2):N2}");
				else
					Log($"-> Result: We are {Math.Round(-100 * ratio, 1, MidpointRounding.AwayFromZero)} % faster :) => {(crystalOps2 / newtonOps):N2} : 1");
			}
#endif

			Log();
		}

		#endregion

		#region TPC Based Benchmarks...

		// Ces benchmarks sont basés sur le benchmark "Thrift vs ProtocolBuffers", ce qui nous permet de nous mesurer a la concurrence :)
		// Pour plus d'infos
		// * Article: https://github.com/eishay/jvm-serializers/wiki/
		// * GitHub: https://github.com/eishay/jvm-serializers

		// le code sérialise une série de données basée sur des classes Media/MediaContent/Image
		// note: dans le code Java d'origine les données sont des public fields, alors qu'en C# on serait plutot passé par des auto-properties
		// pour rester comparable, j'utilise aussi des public fields en C# ce qui n'est pas forcément représentatif...

#if DEBUG
		public const int BENCH_RUNS = 20; // (max 60 a cause du Pierce Criterion)
		public const int BENCH_ITERS = 1_000;
#else
		public const int BENCH_RUNS = 40; // (max 60 a cause du Pierce Criterion)
		public const int BENCH_ITERS = 2_000;
#endif

		public void Bench_Everything()
		{
			Bench_TotalTime();
			Bench_SerializationTime();
			Bench_ParseTime();
			Bench_DeserializationTime();
		}

		private static MediaContent GetMedia1()
		{
			// The standard test value.
			return new MediaContent
			{
				Media = new()
				{
					Uri = "http://javaone.com/keynote.mpg",
					Title = "Javaone Keynote",
					Width = 640,
					Height = 480,
					Format = "video/mpg4",
					Duration = 18000000,    // half hour in milliseconds
					Size = 58982400,        // bitrate * duration in seconds / 8 bits per byte
					Bitrate = 262144,  // 256k
					HasBitrate = true,
					Persons = [ "Bill Gates", "Steve Jobs" ],
					Player = Media.PlayerType.Java,
				},

				Images =
				[
					new()
					{
						Uri = "http://javaone.com/keynote_large.jpg",
						Title = "Javaone Keynote",
						Width = 1024,
						Height = 768,
						Size = Image.ImageSize.Large
					},
					new()
					{
						Uri = "http://javaone.com/keynote_small.jpg",
						Title = "Javaone Keynote",
						Width = 320,
						Height = 240,
						Size = Image.ImageSize.Small
					},
				]
			};
		}

		[Test]
		public void Bench_TotalTime()
		{
			// Create an object, serialize it to a byte array, then deserialize it back to an object.
			var media = GetMedia1();

			#region Warmup
			{ // premier run pour vérifier que tout est ok
				var b = CrystalJson.Serialize(media, CrystalJsonSettings.JsonCompact);
				Log(b);
				var c = CrystalJson.Deserialize<MediaContent>(b);
				Log("json/doxense-runtime: size  = " + b.Length + " chars");
				Assert.That(media, Is.EqualTo(c), "clone != media ??");

				var w = new CrystalJsonWriter(new StringBuilder(), CrystalJsonSettings.JsonCompact, CrystalJson.DefaultResolver);
				media.Manual(w);
				b = w.Buffer.ToString();
				Log("json/doxense-manual : size  = " + b?.Length + " chars");
			}

			#endregion

			RunBenchOnMethod("json/doxense-runtime: total", () =>
			{
				_ = CrystalJson.Deserialize<MediaContent>(CrystalJson.Serialize(media, CrystalJsonSettings.JsonCompact));
			});

#if ENABLE_NEWTONSOFT
			RunBenchOnMethod("json/json.net-runtime: total", () => NJ.JsonConvert.DeserializeObject<MediaContent>(NJ.JsonConvert.SerializeObject(media)));
#endif
		}

		[Test]
		public void Bench_SerializationTime()
		{
			// Create an object, serialize it to a byte array
			var media = GetMedia1();

			#region Warmup
			// premier run pour vérifier que tout est ok
			//Log("CrystalJSON: " + Encoding.UTF8.GetByteCount(CrystalJson.Serialize(media, CrystalJsonSettings.JsonCompact)) + " bytes");
			//media.Manual(new CrystalJsonWriter(new StringBuilder(512), CrystalJsonSettings.JsonCompact, CrystalJson.DefaultResolver));
			#endregion

			RunBenchOnMethod("json/doxense-text   : ser  ", () => { CrystalJson.Serialize(media, CrystalJsonSettings.JsonCompact); });

			RunBenchOnMethod("json/doxense-buffer : ser  ", () => { CrystalJson.ToSlice(media, CrystalJsonSettings.JsonCompact); });

			RunBenchOnMethod("json/doxense-manual : ser  ", () =>
			{
				var writer = new CrystalJsonWriter(new StringBuilder(512), CrystalJsonSettings.JsonCompact, CrystalJson.DefaultResolver);
				media.Manual(writer);
			});

#if ENABLE_NEWTONSOFT
			RunBenchOnMethod("json/json.net-poco: ser  ", () => { NJ.JsonConvert.SerializeObject(media); });
#endif

		}

		[Test]
		public void Bench_DomificationTime()
		{
			// Create an object, serialize it to a byte array
			var media = GetMedia1();

			#region Warmup
			// premier run pour vérifier que tout est ok
			//Log("CrystalJSON: " + Encoding.UTF8.GetByteCount(CrystalJson.Serialize(media, CrystalJsonSettings.JsonCompact)) + " bytes");
			//media.Manual(new CrystalJsonWriter(new StringBuilder(512), CrystalJsonSettings.JsonCompact, CrystalJson.DefaultResolver));
			#endregion

			RunBenchOnMethod("json/doxense-text   : dom  ", () => { _ = JsonObject.FromObject(media); });

		}

		[Test]
		public void Bench_DeserializationTime()
		{
			// deserialize an object from a byte array.
			var media = GetMedia1();

			#region Warmup
			string jsonText = CrystalJson.Serialize(media, CrystalJsonSettings.JsonCompact);
			{ // premier run pour vérifier que tout est ok
				Assert.That(CrystalJson.Deserialize<MediaContent>(jsonText), Is.EqualTo(media), "clone != media ??");
#if ENABLE_NEWTONSOFT
				Assert.That(new NJ.JsonSerializer().Deserialize<MediaContent>(new NJ.JsonTextReader(new StringReader(jsonText))), Is.EqualTo(media), "newtonsoft check");
#endif
			}

			#endregion

			var settings = CrystalJsonSettings.JsonCompact;//.WithInterning(CrystalJsonSettings.StringInterning.Disabled);

			// JSON => STATIC
			RunBenchOnMethod("json/doxense-poco   : deser", () => { CrystalJson.Deserialize<MediaContent>(jsonText, settings); });

#if ENABLE_NEWTONSOFT
			// JSON => STATIC
			RunBenchOnMethod("json/json.net-poco  : deser", () => { new NJ.JsonSerializer().Deserialize<MediaContent>(new NJ.JsonTextReader(new StringReader(jsonText))); });
#endif

		}

		[Test]
		public void Bench_ParseTime()
		{
			// Parse un object from a byte array.

			var media = GetMedia1();

			var settings = CrystalJsonSettings.Json;//.WithInterning(CrystalJsonSettings.StringInterning.Disabled);

			#region Warmup
			string jsonText = CrystalJson.Serialize(media, CrystalJsonSettings.JsonCompact);
			CrystalJson.Parse(jsonText, settings);
#if ENABLE_NEWTONSOFT
			_ = NJ.Linq.JObject.Parse(jsonText);
#endif
			#endregion

			// JSON => DOM
			RunBenchOnMethod("json/doxense-token: parse", () => { CrystalJson.Parse(jsonText, settings); });

#if ENABLE_NEWTONSOFT
			RunBenchOnMethod("json/json.net-token: parse", () => { NJ.Linq.JObject.Parse(jsonText); });
#endif

#if DEBUG_STRINGTABLE_PERFS
			Log("ST.addCalls   = " + CrystalJsonParser.StringTable.addCalls);
			Log("ST.localHits  = " + CrystalJsonParser.StringTable.localHits);
			Log("ST.sharedHits = " + CrystalJsonParser.StringTable.sharedHits);
#endif
		}

		[Test]
		public void Bench_String()
		{
			// Mesure des temps d'execution pour l'encodage de String en JSON

			string[] phrases =
			[
				"\"",
				"a",
				"ab",
				"abc",
				"abcd",
				"abcde",
				"abcdef",
				"abcdefg",
				"abcdefgh",
				"Hello World!",
				"0123456789ABCDEF",
				"This is a test of the emergency broadcast system",
				"This is a test of the emergency broadcast system!",
				"This is a test of the emergency broadcast system!!",
				"This is a test of the emergency broadcast system!!!",
				"Lorem ipsum dolor sit amet, consectetur adipiscing elit. Praesent ac urna nisl, ut placerat nibh. Quisque molestie feugiat tellus et feugiat.",
			];

			#region Warmup...
			JsonEncoding.NeedsEscaping("foo");
			JsonEncoding.NeedsEscaping("fo\"o");
			JsonEncoding.NeedsEscaping("hello world");
			JsonEncoding.NeedsEscaping("hello world\"hello world");
			#endregion

			double[][] nanos = new double[phrases.Length][];

			for (int i = 0; i < phrases.Length; i++)
			{
				string s = phrases[i];
				var ts = new double[6];
				int p = 0;
				var sb = new StringBuilder(1024);
				Trace.WriteLine("\"" + s + "\"");
				ts[p++] = RunBenchOnMethod($"  check best  #{i}", () => { JsonEncoding.NeedsEscaping(s); }, measure: false);
				ts[p++] = RunBenchOnMethod($" append       #{i}", () => { sb.Clear(); sb.Append('"').Append(s).Append('"'); }, measure: false);
				ts[p  ] = RunBenchOnMethod($" append best  #{i}", () => { sb.Clear(); JsonEncoding.Append(sb, s); }, measure: false);
				nanos[i] = ts;
			}

#if DUMP_TO_CSV
			Log("value;Length;Check_Best;Check_Short;Check_Long;Append;Append_Best;Append_Short;Append_Long;Append_Slow;Check_Best_CPC;Check_Short_NPC;Check_Long_NPC;Append_NPC;Append_Best_NPC;Append_Slow_NPC");
			for (int i = 0;i<S.Length; i++)
			{
				string s = S[i];
				Console.Write("\"" + s.Replace("\"", "\"\"") + "\";" + s.Length);
				foreach (var n in nanos[i])
				{
					Console.Write(";" + n.ToString());
				}
				foreach (var n in nanos[i])
				{
					Console.Write(";" + (n / s.Length).ToString());
				}
				Log();
			}
#endif

		}

		private static double RunBenchOnMethod(string name, Action method, bool measure = true)
		{
			GC.Collect();
			GC.WaitForPendingFinalizers();
			GC.Collect();

			long gen0 = GC.CollectionCount(0);
			long gen1 = GC.CollectionCount(1);
			long gen2 = GC.CollectionCount(2);

			var histo = measure ? new RobustHistogram() : null;

			var report = RobustBenchmark.Run(
				method,
				(m) => { m(); },
				(_) => 0,
				BENCH_RUNS,
				BENCH_ITERS,
				histo
			);

			gen0 = GC.CollectionCount(0) - gen0;
			gen1 = GC.CollectionCount(1) - gen1;
			gen2 = GC.CollectionCount(2) - gen2;
			Log($"{name} = {report.BestIterationsNanos:N0} nanos [{report.MedianIterationsNanos:N0}] (~ {report.BestIterationsPerSecond:N0} ips [{report.MedianIterationsPerSecond:N0}], gen0={gen0}, gen1={gen1}, gen2={gen2})");
			if (histo != null)
			{
				Log(histo.GetReport(true));
			}
			return report.BestIterationsNanos;
		}

		#endregion

		#region NextGen Bench

		[Flags]
		public enum TestMode
		{
			None = 0,
			Serialize = 1,
			Parse = 2,
			Deserialize = 4,
			All = Serialize | Parse | Deserialize
		}

		public void RunTestMethod<T>(T model, TestMode mode = TestMode.All, bool histos = false)
			where T : notnull
		{
#if DEBUG
			// Build TeamCity
			const int RUNS = 15;
			const int ITER_FAST = 500;
			const int ITER_NORMAL = 250;
			const int ITER_MEDIUM = 100;
			const int ITER_SLOW = 50;
#else
			// Benchmarks
			const int RUNS = 30;
			const int ITER_FAST = 1000;
			const int ITER_NORMAL = 500;
			const int ITER_MEDIUM = 250;
			const int ITER_SLOW = 100;
#endif

			var settings = CrystalJsonSettings.JsonCompact;
			var resolver = CrystalJson.DefaultResolver;

			static void DoFullGc()
			{
				GC.Collect();
				GC.WaitForPendingFinalizers();
				GC.Collect();
			}

			// setup / warmup
			var jsonText = CrystalJson.Serialize<T>(model, settings);
			{
				_ = CrystalJson.Parse(jsonText, settings);
				_ = CrystalJson.Deserialize<T>(jsonText, settings, resolver);
				_ = JsonValue.FromValue<T>(model, settings, resolver);
			}

			int iterationsPerRun = jsonText.Length <= 100 ? ITER_FAST : jsonText.Length <= 1000 ? ITER_NORMAL : jsonText.Length <= 10000 ? ITER_MEDIUM : ITER_SLOW;
			//Log("{0} => {1}", sw.Elapsed.Ticks, ITER_PER_RUN);

			//fullGC();
			var serReport = RobustBenchmark.Run(
				model,
				(value) => CrystalJson.Serialize<T>(value, settings),
				(value) => value,
				RUNS,
				iterationsPerRun,
				histos ? new RobustHistogram(RobustHistogram.TimeScale.Ticks) : null
			);

			DoFullGc();
			var parseReport = RobustBenchmark.Run(
				jsonText,
				(text) => CrystalJson.Parse(text, settings),
				(text) => text,
				RUNS,
				iterationsPerRun,
				histos ? new RobustHistogram(RobustHistogram.TimeScale.Ticks) : null
			);

			DoFullGc();
			var deserReport = RobustBenchmark.Run(
				jsonText,
				(text) => CrystalJson.Deserialize<T>(text, settings, resolver),
				(text) => text,
				RUNS,
				iterationsPerRun,
				histos ? new RobustHistogram(RobustHistogram.TimeScale.Ticks) : null
			);

			DoFullGc();
			var domReport = RobustBenchmark.Run(
				model,
				(value) => JsonValue.FromValue<T>(value),
				(value) => value,
				RUNS,
				iterationsPerRun,
				histos ? new RobustHistogram(RobustHistogram.TimeScale.Ticks) : null
			);

			string name = typeof(T).GetFriendlyName();
			if (name.EndsWith("[]", StringComparison.Ordinal))
			{
				if (model is ICollection x) name = name.Substring(0, name.Length - 1) + x.Count + "]";
			}

			Log(String.Format(CultureInfo.InvariantCulture,
				" {0,-22} {19,5} {1,7:N0} | {2,8:N0} ({3,7:N0} \u00B1{11,7:N1}) {15} | {4,8:N0} ({5,7:N0} \u00B1{12,7:N1}) {16} | {6,8:N0} ({7,7:N0} \u00B1{13,7:N1}) {17} | {8,8:N0} ({9,7:N0} \u00B1{14,7:N1}) {18} | {10}",
				name,
				jsonText.Length,
				serReport.BestIterationsNanos,
				serReport.MedianIterationsNanos,
				parseReport.BestIterationsNanos,
				parseReport.MedianIterationsNanos,
				deserReport.BestIterationsNanos,
				deserReport.MedianIterationsNanos,
				domReport.BestIterationsNanos,
				domReport.MedianIterationsNanos,
				Truncate(jsonText),
				serReport.StdDevIterationNanos,
				parseReport.StdDevIterationNanos,
				deserReport.StdDevIterationNanos,
				domReport.StdDevIterationNanos,
				string.Format(CultureInfo.InvariantCulture, "{0,5} {1,3} {2,3}", serReport.GC0, serReport.GC1, serReport.GC2),
				string.Format(CultureInfo.InvariantCulture, "{0,5} {1,3} {2,3}", parseReport.GC0, parseReport.GC1, parseReport.GC2),
				string.Format(CultureInfo.InvariantCulture, "{0,5} {1,3} {2,3}", deserReport.GC0, deserReport.GC1, deserReport.GC2),
				string.Format(CultureInfo.InvariantCulture, "{0,5} {1,3} {2,3}", domReport.GC0, domReport.GC1, domReport.GC2),
				iterationsPerRun
			));
			//Log(serReport.Histogram.GetReport(true));
		}

		private static string Truncate(string json)
		{
			if (json.Length < 100) return json;
			return json.Substring(0, 70) + "\u2026" + json.Substring(json.Length - 30);
		}

		[Test]
		[Category("LongRunning")]
		public void Test_NG_Bench()
		{
			Log(String.Format(CultureInfo.InvariantCulture, "{0,-30} {1,6} | {2,41} | {3,41} | {4,41} | {5,41} | {6}", "Model Type", "Size", "Ser<T> (nanos)", "Parse (nanos)", "Deser<T> (nanos)", "Domify (nanos)", "JSON Text")); //RobustHistogram.GetDistributionScale(RobustHistogram.HorizontalShade, 1, 10000)));

			var rnd = new Random(24031974);

			// primitive types
			RunTestMethod(true);
			RunTestMethod(0);
			RunTestMethod(123);
			RunTestMethod(-666);
			RunTestMethod(DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond);
			RunTestMethod(DateTime.Now.Ticks);
			RunTestMethod(0.0d);
			RunTestMethod(1.234d);
			RunTestMethod(Math.PI);
			RunTestMethod(1.234f);
			RunTestMethod(1.234m);
			RunTestMethod(DummyJsonEnum.Foo);
			RunTestMethod("hello world");
			RunTestMethod("Ph'nglui mglw'nafh Cthulhu R'lyeh wgah'nagl fhtan");
			RunTestMethod("This is the super long string that looks innocent until you look closer and realize that it ends with a \"");
			RunTestMethod(Guid.NewGuid().ToString());
			RunTestMethod(Guid.NewGuid());
			RunTestMethod(DateTime.Now);
			RunTestMethod(DateTimeOffset.Now);
			RunTestMethod(DateTime.Now.Subtract(new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)));
			RunTestMethod(NodaTime.SystemClock.Instance.GetCurrentInstant());
			RunTestMethod(NodaTime.SystemClock.Instance.GetCurrentInstant().InZone(NodaTime.DateTimeZoneProviders.Tzdb.GetSystemDefault()));
			RunTestMethod(NodaTime.SystemClock.Instance.GetCurrentInstant().InZone(NodaTime.DateTimeZoneProviders.Tzdb.GetSystemDefault()).ToOffsetDateTime());
			RunTestMethod(NodaTime.SystemClock.Instance.GetCurrentInstant().InUtc().LocalDateTime);

			// small arrays
			RunTestMethod(Enumerable.Range(0, 7).Select(i => i).ToArray());
			RunTestMethod(Enumerable.Range(0, 7).Select(i => rnd.Next(100)).ToArray());
			RunTestMethod(Enumerable.Range(0, 7).Select(i => rnd.Next(int.MaxValue)).ToArray());
			RunTestMethod(Enumerable.Range(0, 7).Select(i => (uint)rnd.Next(100)).ToArray());
			RunTestMethod(Enumerable.Range(0, 7).Select(i => (double)rnd.Next(100)).ToArray());
			RunTestMethod(Enumerable.Range(0, 7).Select(i => Math.Round(rnd.NextDouble() * 10, 2, MidpointRounding.AwayFromZero)).ToArray());
			RunTestMethod(Enumerable.Range(0, 7).Select(i => rnd.NextDouble() * 10).ToArray());
			RunTestMethod(new[] { "hello", "world", "foo", "bar", "baz", "narfzort", "Leeroy Jenkins" });
			RunTestMethod(Enumerable.Range(0, 7).Select(i => Guid.NewGuid().ToString()).ToList());
			// medium arrays
			RunTestMethod(Enumerable.Range(0, 60).Select(i => i).ToArray());
			RunTestMethod(Enumerable.Range(0, 60).Select(i => rnd.Next(100)).ToArray());
			RunTestMethod(Enumerable.Range(0, 60).Select(i => rnd.Next(int.MaxValue)).ToArray());
			RunTestMethod(Enumerable.Range(0, 60).Select(i => (double)rnd.Next(100)).ToArray());
			RunTestMethod(Enumerable.Range(0, 60).Select(i => Math.Round(rnd.NextDouble() * 10, 2, MidpointRounding.AwayFromZero)).ToArray());
			RunTestMethod(Enumerable.Range(0, 60).Select(i => rnd.NextDouble() * 10).ToArray());
			RunTestMethod(Enumerable.Range(0, 60).Select(i => Guid.NewGuid().ToString()).ToList());
			// jagged
			RunTestMethod(Enumerable.Range(0, 7).Select(i => Enumerable.Range(0, 24).Select(j => rnd.Next(10000)).ToArray()).ToArray());
			RunTestMethod(Enumerable.Range(0, 7).Select(i => Enumerable.Range(0, 24).Select(j => (double)rnd.Next(10000)).ToArray()).ToArray());
			RunTestMethod(Enumerable.Range(0, 7).Select(i => Enumerable.Range(0, 24).Select(j => Math.Round(rnd.NextDouble() * 1000, 2, MidpointRounding.AwayFromZero)).ToArray()).ToArray());
			// big ugly array
			RunTestMethod(Enumerable.Range(0, 365).Select(i => rnd.Next(1001) - 500).ToArray()); // -500..+500
			RunTestMethod(Enumerable.Range(0, 365).Select(i => rnd.Next(1001) - 500.0).ToArray()); // -500..+500
			RunTestMethod(Enumerable.Range(0, 365).Select(i => rnd.Next()).ToArray()); // big numbers
			RunTestMethod(Enumerable.Range(0, 365).Select(i => ((long) rnd.Next()) * rnd.Next()).ToArray());
			RunTestMethod(Enumerable.Range(0, 365).Select(i => (ulong) rnd.Next() * (ulong) rnd.Next()).ToArray());
			RunTestMethod(Enumerable.Range(0, 365).Select(i => Math.Round((rnd.NextDouble() - 0.5) * 500, 2, MidpointRounding.AwayFromZero)).ToArray()); // -500.00..+500.00

			// objects
			RunTestMethod(GetMedia1());
			RunTestMethod(Enumerable.Range(0, 7).Select(i => GetMedia1()).ToArray());
			RunTestMethod(Enumerable.Range(0, 32).Select(i => GetMedia1()).ToArray());

			// sealed
			{
				var users = CrystalJson.LoadFrom<UserSealed[]>(MapPathRelativeToCallerSourcePath("Samples/Users.json"))!;
				RunTestMethod(users[0]);
				RunTestMethod(users.Take(7).ToArray());
				RunTestMethod(users.Take(100).ToArray());
			}

			// unsealed
			{
				var users = CrystalJson.LoadFrom<UserUnsealed[]>(MapPathRelativeToCallerSourcePath("Samples/Users.json"))!;
				RunTestMethod(users[0]);
				RunTestMethod(users.Take(7).ToArray());
				RunTestMethod(users.Take(100).ToArray());
			}

			// abstract
			{
				var users = CrystalJson.LoadFrom<UserDerived[]>(MapPathRelativeToCallerSourcePath("Samples/Users.json"))!.Cast<UserAbstract>().ToList();
				RunTestMethod(users[0]);
				RunTestMethod(users.Take(7).ToArray());
				RunTestMethod(users.Take(100).ToArray());
			}

		}

		#endregion

		#region Readers...

		[Test]
		public void Bench_Compare_Utf8_Readers()
		{
			// Ce test compare les différentes façons de parser du JSON: via strings, via des Slice, ou via un Stream, avec ASCII vs UTF-8

			Log("# Warming up...");

			Slice bytesUtf8 = Slice.FromStringUtf8(@"{ ""Id"": 1234, ""Created"": ""2016-12-20T17:10:59.0662814+01:00"", ""Foo"": ""Hello"", ""Bar"": ""世界!"", ""ಠ_ಠ"": ""(╯°□°）╯︵ ┻━┻"", ""Baz"": false }");
			Log($"UTF8 : {bytesUtf8.ToString("P")}");
			Slice bytesAscii = Slice.FromStringAscii(@"{ ""Id"": 1234, ""Created"": ""2016-12-20T17:10:59.0662814+01:00"", ""Foo"": ""Hello"", ""Bar"": ""WD!"", ""O_O"": ""(/*v*)/ ^ +-+"", ""Baz"": false }");
			Log($"ASCII: {bytesAscii.ToString("P")}");

			//WARMUP:
			{
				Log(CrystalJson.Parse(bytesUtf8.ToStringUtf8()));
				Log(CrystalJson.Parse(bytesAscii.ToStringUtf8()));
				Log(CrystalJson.Parse(bytesUtf8));
				Log(CrystalJson.Parse(bytesAscii));
			}
			using (var sr = new StreamReader(new MemoryStream(bytesUtf8.Array, bytesUtf8.Offset, bytesUtf8.Count), Encoding.UTF8))
			{
				Log(CrystalJson.ParseFrom(sr));
			}
			using (var sr = new StreamReader(new MemoryStream(bytesAscii.Array, bytesAscii.Offset, bytesAscii.Count), Encoding.UTF8))
			{
				Log(CrystalJson.ParseFrom(sr));
			}

			FullGc();

			int iterations = 1_000_000;
#if DEBUG
			iterations /= 10;
#endif

			Log($"# Running test with {iterations:N0} iterations...");

			void Trace(string label, TimeSpan duration)
			{
				Log($"> {label,18} = {duration} ({iterations / duration.TotalSeconds:N0} ips)");
			}

			//ASCII

			string strAscii = bytesAscii.ToStringUtf8() ?? "";
			Trace("String:ASCII", Run_Bench_Utf8_ViaString(strAscii, iterations));
			FullGc();

			Trace("StreamReader:ASCII", Run_Bench_Utf8_ViaStreamReader(bytesAscii, iterations));
			FullGc();

			Trace("Slice:ASCII", Run_Bench_Utf8_ViaUtf8StringReader(bytesAscii, iterations));
			FullGc();

			// UTF8

			string strUtf8 = bytesUtf8.ToStringUtf8() ?? "";
			Trace("String:UTF8", Run_Bench_Utf8_ViaString(strUtf8, iterations));
			FullGc();

			Trace("StreamReader:UTF8", Run_Bench_Utf8_ViaStreamReader(bytesUtf8, iterations));
			FullGc();
		}

		[MethodImpl(MethodImplOptions.NoInlining)]
		private static TimeSpan Run_Bench_Utf8_ViaString(string data, int iterations)
		{
			var sw = Stopwatch.StartNew();
			for (int i = 0; i < iterations; i++)
			{
				var _ = CrystalJson.Parse(data);
			}
			sw.Stop();
			return sw.Elapsed;
		}

		[MethodImpl(MethodImplOptions.NoInlining)]
		private static TimeSpan Run_Bench_Utf8_ViaStreamReader(Slice data, int iterations)
		{
			var sw = Stopwatch.StartNew();
			for (int i = 0; i < iterations; i++)
			{
				using (var sr = new StreamReader(new MemoryStream(data.Array, data.Offset, data.Count), Encoding.UTF8))
				{
					var _ = CrystalJson.ParseFrom(sr);
				}
			}
			sw.Stop();
			return sw.Elapsed;
		}

		[MethodImpl(MethodImplOptions.NoInlining)]
		private static TimeSpan Run_Bench_Utf8_ViaUtf8StringReader(Slice data, int iterations)
		{
			var sw = Stopwatch.StartNew();
			for (int i = 0; i < iterations; i++)
			{
				var _ = CrystalJson.Parse(data);
			}
			sw.Stop();
			return sw.Elapsed;
		}

		#endregion
	}

	#region Models...

	public sealed class MediaContent : IEquatable<MediaContent>
	{

		public required Media Media { get; init; }

		public List<Image>? Images { get; init; }

		public bool Equals(MediaContent? other)
		{
			if (ReferenceEquals(other, this)) return true;
			if (other == null) return false;
			if (!this.Media.Equals(other.Media)) return false;
			if (this.Images == null || other.Images == null) return this.Images == other.Images;
			if (this.Images.Count != other.Images.Count) return false;
			return this.Images.Zip(other.Images, (left, right) => left.Equals(right)).All(b => b);
		}

		public override bool Equals(object? obj)
		{
			return obj is MediaContent content && Equals(content);
		}

		/// <inheritdoc />
		public override int GetHashCode()
		{
			return this.Media.GetHashCode();
		}

		public void Manual(CrystalJsonWriter writer)
		{
			var state = writer.BeginObject();

			writer.WriteName("Media");
			this.Media.Manual(writer);

			if (this.Images != null && this.Images.Count > 0)
			{
				writer.WriteName("Images");
				var state2 = writer.BeginArray();
				foreach (var image in this.Images)
				{
					writer.WriteFieldSeparator();
					image.Manual(writer);
				}
				writer.EndArray(state2);
			}

			writer.EndObject(state);
		}
	}

	public sealed class Media : IEquatable<Media>
	{
		public enum PlayerType
		{
			Java, Flash
		}

		public required string Uri { get; init; }
		public string? Title { get; init; }
		public required int Width { get; init; }
		public required int Height { get; init; }
		public required string Format { get; init; }
		public required long Duration { get; init; }
		public required long Size { get; init; }
		public int Bitrate { get; init; }
		public bool HasBitrate { get; init; }
		public List<string>? Persons { get; init; }
		public PlayerType Player { get; init; }
		public string? Copyright { get; init; }

		public bool Equals(Media? other)
		{
			if (ReferenceEquals(other, this)) return true;
			return other != null
				&& this.Uri == other.Uri
				&& this.Title == other.Title
				&& this.Width == other.Width
				&& this.Height == other.Height
				&& this.Format == other.Format
				&& this.Duration == other.Duration
				&& this.Size == other.Size
				&& this.Bitrate == other.Bitrate
				&& this.HasBitrate == other.HasBitrate
				&& (this.Persons ?? []).Zip(other.Persons ?? [], (left, right) => left == right).All(b => b)
				&& this.Player == other.Player
				&& this.Copyright == other.Copyright;
		}

		public override bool Equals(object? obj)
		{
			return obj is Media media && Equals(media);
		}

		/// <inheritdoc />
		public override int GetHashCode()
		{
			return this.Uri.GetHashCode();
		}

		public void Manual(CrystalJsonWriter writer)
		{
			var state = writer.BeginObject();
			writer.WriteField("Uri", this.Uri);
			if (this.Title != null) writer.WriteField("Title", this.Title);
			writer.WriteField("Width", this.Width);
			writer.WriteField("Height", this.Height);
			writer.WriteField("Format", this.Format);
			writer.WriteField("Duration", this.Duration);
			writer.WriteField("Size", this.Size);
			if (this.HasBitrate) writer.WriteField("Bitrate", this.Bitrate);
			if (this.Persons != null) writer.WriteField("Persons", this.Persons);
			writer.WriteField("Player", this.Player);
			if (this.Copyright != null) writer.WriteField("Copyright", this.Copyright);
			writer.EndObject(state);
		}

	}

	public sealed class Image : IEquatable<Image>
	{
		public enum ImageSize
		{
			Small, Large
		}

		public required string Uri { get; init; }
		public string? Title { get; init; }  // Can be null
		public required int Width { get; init; }
		public required int Height { get; init; }
		public required ImageSize Size { get; init; }

		public bool Equals(Image? other)
		{
			return other != null
				&& this.Uri == other.Uri
				&& this.Title == other.Title
				&& this.Width == other.Width
				&& this.Height == other.Height
				&& this.Size == other.Size;
		}

		public override bool Equals(object? obj)
		{
			return obj is Image img && Equals(img);
		}

		public override int GetHashCode()
		{
			return this.Uri.GetHashCode();
		}

		public void Manual(CrystalJsonWriter writer)
		{
			var state = writer.BeginObject();
			writer.WriteField("Uri", this.Uri);
			if (this.Title != null) writer.WriteField("Title", this.Title);
			writer.WriteField("Width", this.Width);
			writer.WriteField("Height", this.Height);
			writer.WriteField("Size", this.Size);
			writer.EndObject(state);
		}
	}

	public sealed class UserSealed
	{
		public required Guid Id { get; init; }
		public required string FirstName { get; init; }
		public required string LastName { get; init; }
		public required string Email { get; init; }
		public required string Country { get; init; }
		public required string IpAddress { get; init; }
		public required bool IsActive { get; init; }
		public required DateTime Joined { get; init; }
	}

	public class UserUnsealed
	{
		public required Guid Id { get; init; }
		public required string FirstName { get; init; }
		public required string LastName { get; init; }
		public required string Email { get; init; }
		public required string Country { get; init; }
		public required string IpAddress { get; init; }
		public required bool IsActive { get; init; }
		public required DateTime Joined { get; init; }
	}

	public abstract class UserAbstract
	{
		public required Guid Id { get; init; }
		public required string FirstName { get; init; }
		public required string LastName { get; init; }
		public required string Email { get; init; }
		public required string Country { get; init; }
		public required string IpAddress { get; init; }
		public required bool IsActive { get; init; }
		public required DateTime Joined { get; init; }
	}

	public class UserDerived : UserAbstract
	{ }

	#endregion

}
