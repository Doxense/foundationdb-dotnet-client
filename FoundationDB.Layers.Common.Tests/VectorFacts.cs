#region BSD License
/* Copyright (c) 2005-2023 Doxense SAS
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
	using Doxense.Serialization.Encoders;
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
				var location = db.Root["vector"];
				await CleanLocation(db, location);

				var vector = new FdbVector<Slice>(location, Slice.Empty, BinaryEncoding.SliceEncoder);

				using (var tr = await db.BeginTransactionAsync(this.Cancellation))
				{
					var xs = await vector.Resolve(tr);

					Log("Clearing any previous values in the vector");
					xs.Clear(tr);

					Log();
					Log("MODIFIERS");

					// Set + Push
					xs.Set(tr, 0, Slice.FromInt32(1));
					xs.Set(tr, 1, Slice.FromInt32(2));
					await xs.PushAsync(tr, Slice.FromInt32(3));
					await PrintVector(xs, tr);

					// Swap
					await xs.SwapAsync(tr, 0, 2);
					await PrintVector(xs, tr);

					// Pop
					Log("> Popped: " + await xs.PopAsync(tr));
					await PrintVector(xs, tr);

					// Clear
					xs.Clear(tr);

					Log("> Pop empty: " + await xs.PopAsync(tr));
					await PrintVector(xs, tr);

					await xs.PushAsync(tr, Value("foo"));
					Log("> Pop size 1: " + await xs.PopAsync(tr));
					await PrintVector(xs, tr);

					Log();
					Log("CAPACITY OPERATIONS");

					Log("> Size: " + await xs.SizeAsync(tr));
					Log("> Empty: " + await xs.EmptyAsync(tr));

					Log("> Resizing to length 5");
					await xs.ResizeAsync(tr, 5);
					await PrintVector(xs, tr);
					Log("> Size: " + await xs.SizeAsync(tr));

					Log("Settings values");
					xs.Set(tr, 0, Value("Portez"));
					xs.Set(tr, 1, Value("ce vieux"));
					xs.Set(tr, 2, Value("whisky"));
					xs.Set(tr, 3, Value("au juge"));
					xs.Set(tr, 4, Value("blond qui"));
					xs.Set(tr, 5, Value("fume"));
					await PrintVector(xs, tr);

					Log("FRONT");
					Log("> " + await xs.FrontAsync(tr));

					Log("BACK");
					Log("> " + await xs.BackAsync(tr));

					Log();
					Log("ELEMENT ACCESS");
					Log("> Index 0: " + await xs.GetAsync(tr, 0));
					Log("> Index 5: " + await xs.GetAsync(tr, 5));
					Log("> Size: " + await xs.SizeAsync(tr));

					Log();
					Log("RESIZING");
					Log("> Resizing to 3");
					await xs.ResizeAsync(tr, 3);
					await PrintVector(xs, tr);
					Log("> Size: " + await xs.SizeAsync(tr));

					Log("> Resizing to 3 again");
					await xs.ResizeAsync(tr, 3);
					await PrintVector(xs, tr);
					Log("> Size: " + await xs.SizeAsync(tr));

					Log("> Resizing to 6");
					await xs.ResizeAsync(tr, 6);
					await PrintVector(xs, tr);
					Log("> Size: " + await xs.SizeAsync(tr));

					Log();
					Log("SPARSE TEST");

					Log("> Popping sparse vector");
					await xs.PopAsync(tr);
					await PrintVector(xs, tr);
					Log("> Size: " + await xs.SizeAsync(tr));

					Log("> Resizing to 4");
					await xs.ResizeAsync(tr, 4);
					await PrintVector(xs, tr);
					Log("> Size: " + await xs.SizeAsync(tr));

					Log("> Adding 'word' to index 10, resize to 25");
					xs.Set(tr, 10, Value("word"));
					await xs.ResizeAsync(tr, 25);
					await PrintVector(xs, tr);
					Log("> Size: " + await xs.SizeAsync(tr));

					Log("> Swapping with sparse element");
					await xs.SwapAsync(tr, 10, 15);
					await PrintVector(xs, tr);
					Log("> Size: " + await xs.SizeAsync(tr));

					Log("> Swapping sparse elements");
					await xs.SwapAsync(tr, 12, 13);
					await PrintVector(xs, tr);
					Log("> Size: " + await xs.SizeAsync(tr));
				}
			}
		}

		private static async Task PrintVector<T>(FdbVector<T>.State vector, IFdbReadOnlyTransaction tr)
		{
			bool first = true;
			var sb = new StringBuilder();

			await tr.GetRange(vector.Subspace.ToRange()).ForEachAsync((kvp) =>
			{
				if (!first) sb.Append(", "); else first = false;
				sb.Append($"{vector.Subspace.DecodeLast<long>(kvp.Key)}:{kvp.Value:P}");
			});

			Log("> Vector: (" + sb.ToString() + ")");
		}

	}

}
