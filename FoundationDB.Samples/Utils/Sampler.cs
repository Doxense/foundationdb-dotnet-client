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

namespace FoundationDB.Samples.Benchmarks
{
	public class SamplerTest : IAsyncTest
	{

		public SamplerTest(double ratio)
		{
			this.Ratio = ratio;
		}

		public double Ratio { get; }

		#region IAsyncTest...

		public string Name => "SamplerTest";

		private static string FormatSize(long size)
		{
			if (size < 10000) return size.ToString("N0");
			double x = size / 1024.0;
			if (x < 800) return x.ToString("N1") + " kB";
			x /= 1024.0;
			if (x < 800) return x.ToString("N2") + " MB";
			x /= 1024.0;
			return x.ToString("N2") + " GB";
		}

		public async Task Run(IFdbDatabase db, TextWriter log, CancellationToken ct)
		{
			// estimate the number of machines...
			Console.WriteLine("# Detecting cluster topology...");
			var servers = await db.QueryAsync(tr => tr
				.WithOptions(options => options.WithReadAccessToSystemKeys())
				.GetRange(KeyRange.StartsWith(Fdb.System.ServerList))
				.Select(kvp => new
				{
					Node = kvp.Value.Substring(8, 16).ToHexString(),
					Machine = kvp.Value.Substring(24, 16).ToHexString(),
					DataCenter = kvp.Value.Substring(40, 16).ToHexString()
				}),
				ct
			);

			var numNodes = servers.Select(s => s.Node).Distinct().Count();
			var numMachines = servers.Select(s => s.Machine).Distinct().Count();
			var numDCs = servers.Select(s => s.DataCenter).Distinct().Count();

			Console.WriteLine($"# > Found {numNodes} process(es) on {numMachines} machine(s) in {numDCs} datacenter(s)");
			Console.WriteLine("# Reading list of shards...");
			// dump keyServers
			var ranges = await Fdb.System.GetChunksAsync(db, FdbKey.MinValue, FdbKey.MaxValue, ct);
			Console.WriteLine($"# > Found {ranges.Count} shards:");

			// take a sample
			var rnd = new Random(1234);
			int sz = Math.Max((int)Math.Ceiling(this.Ratio * ranges.Count), 1);
			if (sz > 500) sz = 500; //SAFETY
			if (sz < 50) sz = Math.Max(sz, Math.Min(50, ranges.Count));

			var samples = new List<KeyRange>();
			for (int i = 0; i < sz; i++)
			{
				int p = rnd.Next(ranges.Count);
				samples.Add(ranges[p]);
				ranges.RemoveAt(p);
			}

			Console.WriteLine($"# Sampling {sz} out of {ranges.Count} shards ({(100.0 * sz / ranges.Count):N1}%) ...");
			Console.WriteLine($"{"Count",9}{"Keys",10}{"Values",10}{"Total",10} : K+V size distribution");

			samples = samples.OrderBy(x => x.Begin).ToList();

			long total = 0;
			int workers = Math.Min(numMachines, 8);

			var sw = Stopwatch.StartNew();
			var tasks = new List<Task>();
			while(samples.Count > 0)
			{
				while (tasks.Count < workers && samples.Count > 0)
				{
					var range = samples[0];
					samples.RemoveAt(0);
					tasks.Add(Task.Run(async () =>
					{
						var hh = new RobustHistogram(RobustHistogram.TimeScale.Ticks);

						#region Method 1: get_range everything...

						using (var tr = db.BeginTransaction(ct))
						{
							long keySize = 0;
							long valueSize = 0;
							long count = 0;

							int iter = 0;
							var beginSelector = KeySelector.FirstGreaterOrEqual(range.Begin);
							var endSelector = KeySelector.FirstGreaterOrEqual(range.End);
							while (true)
							{
								var data = default(FdbRangeChunk);
								var error = default(FdbException);
								try
								{
									data = await tr.Snapshot.GetRangeAsync(
										beginSelector,
										endSelector,
										FdbRangeOptions.WantAll,
										iter
									).ConfigureAwait(false);
								}
								catch (FdbException e)
								{
									error = e;
								}

								if (error != null)
								{
									await tr.OnErrorAsync(error.Code).ConfigureAwait(false);
									continue;
								}

								if (data == null || data.Count == 0)
								{
									break;
								}

								count += data.Count;
								foreach (var kvp in data)
								{
									keySize += kvp.Key.Count;
									valueSize += kvp.Value.Count;

									hh.Add(TimeSpan.FromTicks(kvp.Key.Count + kvp.Value.Count));
								}

								if (!data.HasMore) break;

								beginSelector = KeySelector.FirstGreaterThan(data.Last);
								++iter;
							}

							long totalSize = keySize + valueSize;
							Interlocked.Add(ref total, totalSize);

							Console.WriteLine($"{count,9:N0}{FormatSize(keySize),10}{FormatSize(valueSize),10}{FormatSize(totalSize),10} : {hh.GetDistribution(begin: 1, end: 10000, fold:2)}");
						}
						#endregion

						#region Method 2: estimate the count using key selectors...

						//long counter = await Fdb.System.EstimateCountAsync(db, range, ct);
						//Console.WriteLine("COUNT = " + counter.ToString("N0"));

						#endregion
					}, ct));
				}

				var done = await Task.WhenAny(tasks);
				tasks.Remove(done);
			}

			await Task.WhenAll(tasks);
			sw.Stop();

			Console.WriteLine($"> Sampled {FormatSize(total)} ({total:N0} bytes) in {sw.Elapsed.TotalSeconds:N1} sec");
			Console.WriteLine($"> Estimated total size is {FormatSize(total * ranges.Count / sz)}");
		}

		#endregion

	}
}
