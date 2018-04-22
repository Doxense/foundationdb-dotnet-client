#region BSD Licence
/* Copyright (c) 2013-2018, Doxense SAS
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
	using System;
	using System.Text;
	using System.Threading.Tasks;
	using FoundationDB.Client;
	using FoundationDB.Client.Tests;
	using NUnit.Framework;

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

					Log("Clearing any previous values in the vector");
					vector.Clear(tr);

					Log();
					Log("MODIFIERS");

					// Set + Push
					vector.Set(tr, 0, Slice.FromInt32(1));
					vector.Set(tr, 1, Slice.FromInt32(2));
					await vector.PushAsync(tr, Slice.FromInt32(3));
					await PrintVector(vector, tr);

					// Swap
					await vector.SwapAsync(tr, 0, 2);
					await PrintVector(vector, tr);

					// Pop
					Log("> Popped: " + await vector.PopAsync(tr));
					await PrintVector(vector, tr);

					// Clear
					vector.Clear(tr);

					Log("> Pop empty: " + await vector.PopAsync(tr));
					await PrintVector(vector, tr);

					await vector.PushAsync(tr, Slice.FromString("foo"));
					Log("> Pop size 1: " + await vector.PopAsync(tr));
					await PrintVector(vector, tr);

					Log();
					Log("CAPACITY OPERATIONS");

					Log("> Size: " + await vector.SizeAsync(tr));
					Log("> Empty: " + await vector.EmptyAsync(tr));

					Log("> Resizing to length 5");
					await vector.ResizeAsync(tr, 5);
					await PrintVector(vector, tr);
					Log("> Size: " + await vector.SizeAsync(tr));

					Log("Settings values");
					vector.Set(tr, 0, Slice.FromString("Portez"));
					vector.Set(tr, 1, Slice.FromString("ce vieux"));
					vector.Set(tr, 2, Slice.FromString("whisky"));
					vector.Set(tr, 3, Slice.FromString("au juge"));
					vector.Set(tr, 4, Slice.FromString("blond qui"));
					vector.Set(tr, 5, Slice.FromString("fume"));
					await PrintVector(vector, tr);

					Log("FRONT");
					Log("> " + await vector.FrontAsync(tr));

					Log("BACK");
					Log("> " + await vector.BackAsync(tr));

					Log();
					Log("ELEMENT ACCESS");
					Log("> Index 0: " + await vector.GetAsync(tr, 0));
					Log("> Index 5: " + await vector.GetAsync(tr, 5));
					Log("> Size: " + await vector.SizeAsync(tr));

					Log();
					Log("RESIZING");
					Log("> Resizing to 3");
					await vector.ResizeAsync(tr, 3);
					await PrintVector(vector, tr);
					Log("> Size: " + await vector.SizeAsync(tr));

					Log("> Resizing to 3 again");
					await vector.ResizeAsync(tr, 3);
					await PrintVector(vector, tr);
					Log("> Size: " + await vector.SizeAsync(tr));

					Log("> Resizing to 6");
					await vector.ResizeAsync(tr, 6);
					await PrintVector(vector, tr);
					Log("> Size: " + await vector.SizeAsync(tr));

					Log();
					Log("SPARSE TEST");

					Log("> Popping sparse vector");
					await vector.PopAsync(tr);
					await PrintVector(vector, tr);
					Log("> Size: " + await vector.SizeAsync(tr));

					Log("> Resizing to 4");
					await vector.ResizeAsync(tr, 4);
					await PrintVector(vector, tr);
					Log("> Size: " + await vector.SizeAsync(tr));

					Log("> Adding 'word' to index 10, resize to 25");
					vector.Set(tr, 10, Slice.FromString("word"));
					await vector.ResizeAsync(tr, 25);
					await PrintVector(vector, tr);
					Log("> Size: " + await vector.SizeAsync(tr));

					Log("> Swapping with sparse element");
					await vector.SwapAsync(tr, 10, 15);
					await PrintVector(vector, tr);
					Log("> Size: " + await vector.SizeAsync(tr));

					Log("> Swapping sparse elements");
					await vector.SwapAsync(tr, 12, 13);
					await PrintVector(vector, tr);
					Log("> Size: " + await vector.SizeAsync(tr));
				}
			}
		}

		private static async Task PrintVector<T>(FdbVector<T> vector, IFdbReadOnlyTransaction tr)
		{
			bool first = true;
			var sb = new StringBuilder();

			await tr.GetRange(vector.Subspace.Keys.ToRange()).ForEachAsync((kvp) =>
			{
				if (!first) sb.Append(", "); else first = false;
				sb.Append($"{vector.Subspace.Keys.DecodeLast<long>(kvp.Key)}:{kvp.Value:P}");
			});

			Log("> Vector: (" + sb.ToString() + ")");
		}

	}

}
