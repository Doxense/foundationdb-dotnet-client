#region BSD Licence
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

namespace FoundationDB.Client.Filters.Tests
{
	using FoundationDB.Client;
	using FoundationDB.Client.Tests;
	using FoundationDB.Layers.Tuples;
	using NUnit.Framework;
	using System;
	using System.Threading.Tasks;

	[TestFixture]
	public class LoggingFilterFacts
	{

		private static char GetFancyChar(int pos, int count, double start, double end)
		{
			double cb = 1.0 * pos / count;
			double ce = 1.0 * (pos + 1) / count;

			if (cb >= end) return ' ';
			if (ce < start) return '_';

			double x = count * (Math.Min(ce, end) - Math.Max(cb, start));
			if (x < 0) x = 0;
			if (x > 1) x = 1;

			int p = (int)Math.Round(x * 10);
			return "`.:;+=xX$&#"[p];
		}

		private static string GetFancyGraph(int width, long offset, long duration, long total)
		{
			double begin = 1.0d * offset / total;
			double end = 1.0d * (offset + duration) / total;

			var tmp = new char[width];
			for(int i=0;i<tmp.Length;i++)
			{
				tmp[i] = GetFancyChar(i, tmp.Length, begin, end);
			}
			return new string(tmp);
		}

		[Test]
		public async Task Test_Can_Log_A_Transaction()
		{
			//await Task.Delay(10).ConfigureAwait(false);

			using (var db = await TestHelpers.OpenTestPartitionAsync())
			{
				var location = await db.CreateOrOpenDirectoryAsync(new[] { "Logging" });
				await db.ClearRangeAsync(location);

				await db.WriteAsync((tr) =>
				{
					var rnd = new Random();
					tr.Set(location.Pack("One"), Slice.FromString("111111"));
					tr.Set(location.Pack("Two"), Slice.FromString("222222"));
					for (int j = 0; j < 4; j++)
					{
						for (int i = 0; i < 100; i++)
						{
							tr.Set(location.Pack("Range", j, rnd.Next(1000)), Slice.Empty);
						}
					}
				});

				for (int k = 0; k < 10; k++)
				{
					Console.WriteLine("==== " + k + " ==== ");
					Console.WriteLine();
					using (var tr = new LoggingTransactionFilter(db.BeginTransaction(), true))
					{
						tr.Set(location.Pack("Write"), Slice.FromString("abcdef"));
						tr.Clear(location.Pack("Clear", "0"));
						tr.ClearRange(location.Pack("Clear", "A"), location.Pack("Clear", "Z"));

						await tr.GetAsync(location.Pack("One"));
						await tr.GetAsync(location.Pack("NotFound"));

						await tr.GetRangeAsync(FdbKeySelector.LastLessOrEqual(location.Pack("A")),FdbKeySelector.FirstGreaterThan(location.Pack("Z")));

						await Task.WhenAll(
							tr.GetRange(FdbKeyRange.StartsWith(location.Pack("Range", 0))).ToListAsync(),
							tr.GetRange(location.Pack("Range", 1, 0), location.Pack("Range", 1, 200)).ToListAsync(),
							tr.GetRange(location.Pack("Range", 2, 400), location.Pack("Range", 2, 600)).ToListAsync(),
							tr.GetRange(location.Pack("Range", 3, 800), location.Pack("Range", 3, 1000)).ToListAsync()
						);

						await tr.GetAsync(location.Pack("Two"));

						await tr.CommitAsync();

						long duration = tr.Log.Clock.ElapsedTicks;
						System.Threading.Thread.CurrentThread.CurrentCulture = System.Globalization.CultureInfo.InvariantCulture;
						Console.WriteLine("Run completed in " + TimeSpan.FromTicks(duration).TotalMilliseconds + " ms, size " + tr.Size + " bytes");
						foreach (var cmd in tr.Log.Commands)
						{
							//const int W = 50;
							int W = (int) Math.Ceiling(2*TimeSpan.FromTicks(duration).TotalMilliseconds);
							double r = 1.0d * cmd.Duration.Ticks / duration;
							string w = GetFancyGraph(W, cmd.StartOffset, cmd.Duration.Ticks, duration);

							switch (cmd.Mode)
							{
								case LoggingTransactionFilter.Mode.Read: Console.Write("[R] "); break;
								case LoggingTransactionFilter.Mode.Write: Console.Write("(w) "); break;
								case LoggingTransactionFilter.Mode.Meta: Console.Write("<M> "); break;
							}
							Console.Write("|" + w + "| T+" + (cmd.StartOffset / 10000.0).ToString("N3") + " ~ " + ((cmd.EndOffset ?? 0) / 10000.0).ToString("N3") + " (" + (cmd.Duration.Ticks / 10000.0).ToString("N3") + ") ");
							Console.WriteLine(cmd.ToString());
						}
						Console.WriteLine("Duration: " + TimeSpan.FromTicks(duration).TotalMilliseconds.ToString("N3") + " ms");

						Console.WriteLine();
					}
				}
			}
		}

	}

}
