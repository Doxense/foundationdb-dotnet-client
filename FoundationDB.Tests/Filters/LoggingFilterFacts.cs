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

		[Test]
		public async Task Test_Can_Log_A_Transaction()
		{
			using (var db = await TestHelpers.OpenTestPartitionAsync())
			{
				var location = await db.CreateOrOpenDirectoryAsync(new[] { "Logging" });
				await db.ClearRangeAsync(location);

				using(var tr = new LoggingTransactionFilter(db.BeginTransaction(), true))
				{
					tr.Set(location.Pack("Hello"), Slice.FromString("World"));
					tr.Clear(location.Pack("Clear", "0"));
					tr.ClearRange(location.Pack("Clear", "A"), location.Pack("Clear", "Z"));

					await tr.GetAsync(location.Pack("Hello"));
					await tr.GetAsync(location.Pack("NotFound"));
					await tr.GetRange(FdbKeyRange.StartsWith(location.Pack("Range"))).ToListAsync();

					await tr.CommitAsync();

					var duration = tr.Log.Clock.Elapsed;
					foreach(var cmd in tr.Log.Commands)
					{
						Console.WriteLine("> T+" + TimeSpan.FromTicks(cmd.StartOffset).TotalSeconds.ToString("N3") + "s : " + cmd.Duration.TotalMilliseconds.ToString("N3") + "ms] " + cmd.ToString());
					}
					Console.WriteLine("Duration: " + duration.TotalMilliseconds.ToString("N3") + " ms");

				}
			}
		}

	}

}
