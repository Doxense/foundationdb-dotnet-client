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

namespace FoundationDB.Layers.Collections.Tests
{
	using FoundationDB.Client;
	using FoundationDB.Client.Tests;
	using FoundationDB.Layers.Tuples;
	using NUnit.Framework;
	using System;
	using System.Collections.Generic;
	using System.Diagnostics;
	using System.Linq;
	using System.Text;
	using System.Threading;
	using System.Threading.Tasks;

	[TestFixture]
	[Obsolete]
	public class VectorFacts : FdbTest
	{
		[Test]
		public async Task Test_Vector_Fast()
		{
			using (var db = await OpenTestPartitionAsync())
			{
				var location = await GetCleanDirectory(db, "vector");

				var vector = new FdbVector<Slice>(location, Slice.Empty, KeyValueEncoders.Values.BinaryEncoder);

				using (var tr = db.BeginTransaction(this.Cancellation))
				{

					Console.WriteLine("Clearing any previous values in the vector");
					vector.Clear(tr);

					Console.WriteLine();
					Console.WriteLine("MODIFIERS");

					// Set + Push
					vector.Set(tr, 0, Slice.FromInt32(1));
					vector.Set(tr, 1, Slice.FromInt32(2));
					await vector.PushAsync(tr, Slice.FromInt32(3));
					await PrintVector(vector, tr);

					// Swap
					await vector.SwapAsync(tr, 0, 2);
					await PrintVector(vector, tr);

					// Pop
					Console.WriteLine("> Popped: " + await vector.PopAsync(tr));
					await PrintVector(vector, tr);

					// Clear
					vector.Clear(tr);

					Console.WriteLine("> Pop empty: " + await vector.PopAsync(tr));
					await PrintVector(vector, tr);

					await vector.PushAsync(tr, Slice.FromAscii("foo"));
					Console.WriteLine("> Pop size 1: " + await vector.PopAsync(tr));
					await PrintVector(vector, tr);

					Console.WriteLine();
					Console.WriteLine("CAPACITY OPERATIONS");

					Console.WriteLine("> Size: " + await vector.SizeAsync(tr));
					Console.WriteLine("> Empty: " + await vector.EmptyAsync(tr));

					Console.WriteLine("> Resizing to length 5");
					await vector.ResizeAsync(tr, 5);
					await PrintVector(vector, tr);
					Console.WriteLine("> Size: " + await vector.SizeAsync(tr));

					Console.WriteLine("Settings values");
					vector.Set(tr, 0, Slice.FromAscii("Portez"));
					vector.Set(tr, 1, Slice.FromAscii("ce vieux"));
					vector.Set(tr, 2, Slice.FromAscii("whisky"));
					vector.Set(tr, 3, Slice.FromAscii("au juge"));
					vector.Set(tr, 4, Slice.FromAscii("blond qui"));
					vector.Set(tr, 5, Slice.FromAscii("fume"));
					await PrintVector(vector, tr);

					Console.WriteLine("FRONT");
					Console.WriteLine("> " + await vector.FrontAsync(tr));

					Console.WriteLine("BACK");
					Console.WriteLine("> " + await vector.BackAsync(tr));

					Console.WriteLine();
					Console.WriteLine("ELEMENT ACCESS");
					Console.WriteLine("> Index 0: " + await vector.GetAsync(tr, 0));
					Console.WriteLine("> Index 5: " + await vector.GetAsync(tr, 5));
					Console.WriteLine("> Size: " + await vector.SizeAsync(tr));

					Console.WriteLine();
					Console.WriteLine("RESIZING");
					Console.WriteLine("> Resizing to 3");
					await vector.ResizeAsync(tr, 3);
					await PrintVector(vector, tr);
					Console.WriteLine("> Size: " + await vector.SizeAsync(tr));

					Console.WriteLine("> Resizing to 3 again");
					await vector.ResizeAsync(tr, 3);
					await PrintVector(vector, tr);
					Console.WriteLine("> Size: " + await vector.SizeAsync(tr));

					Console.WriteLine("> Resizing to 6");
					await vector.ResizeAsync(tr, 6);
					await PrintVector(vector, tr);
					Console.WriteLine("> Size: " + await vector.SizeAsync(tr));

					Console.WriteLine();
					Console.WriteLine("SPARSE TEST");

					Console.WriteLine("> Popping sparse vector");
					await vector.PopAsync(tr);
					await PrintVector(vector, tr);
					Console.WriteLine("> Size: " + await vector.SizeAsync(tr));

					Console.WriteLine("> Resizing to 4");
					await vector.ResizeAsync(tr, 4);
					await PrintVector(vector, tr);
					Console.WriteLine("> Size: " + await vector.SizeAsync(tr));

					Console.WriteLine("> Adding 'word' to index 10, resize to 25");
					vector.Set(tr, 10, Slice.FromAscii("word"));
					await vector.ResizeAsync(tr, 25);
					await PrintVector(vector, tr);
					Console.WriteLine("> Size: " + await vector.SizeAsync(tr));

					Console.WriteLine("> Swapping with sparse element");
					await vector.SwapAsync(tr, 10, 15);
					await PrintVector(vector, tr);
					Console.WriteLine("> Size: " + await vector.SizeAsync(tr));

					Console.WriteLine("> Swapping sparse elements");
					await vector.SwapAsync(tr, 12, 13);
					await PrintVector(vector, tr);
					Console.WriteLine("> Size: " + await vector.SizeAsync(tr));
				}
			}
		}

		private static async Task PrintVector<T>(FdbVector<T> vector, IFdbReadOnlyTransaction tr)
		{
			bool first = true;
			var sb = new StringBuilder();

			await tr.GetRange(vector.Subspace.ToRange()).ForEachAsync((kvp) =>
			{
				if (!first) sb.Append(", "); else first = false;
				sb.Append(vector.Subspace.Tuples.DecodeLast<long>(kvp.Key) + ":" + kvp.Value.ToAsciiOrHexaString());
			});

			Console.WriteLine("> Vector: (" + sb.ToString() + ")");
		}

	}

}
