#region BSD Licence
/* Copyright (c) 2013-2014, Doxense SAS
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

namespace FoundationDB.Layers.Experimental.Indexing.Tests
{
	using FoundationDB.Client;
	using NUnit.Framework;
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using System.Text;

	[TestFixture]
	public class CompressedBitmapsFacts
	{

		[Test]
		public void Test_EmptyBitmap()
		{
			// the empty bitmap shouldn't have any words
			var empty = CompressedBitmap.Empty;
			Assert.That(empty, Is.Not.Null.And.Count.EqualTo(0));

			//REVIEW: what should the bounds be for an empty bitmap?
			Assert.That(empty.Bounds.Lowest, Is.EqualTo(0), "empty.Bounds.Lowest");
			Assert.That(empty.Bounds.Highest, Is.EqualTo(-1), "empty.Bounds.Highest");
			Assert.That(empty.Bounds.IsEmpty, Is.True, "empty.Bounds.IsEmpty");

			// all bits should be unset
			Assert.That(empty.Test(0), Is.False);
			Assert.That(empty.Test(31), Is.False);
			Assert.That(empty.Test(32), Is.False);
			Assert.That(empty.Test(1234), Is.False);

			// binary representation should be 0 bytes
			var packed = empty.ToSlice();
			Assert.That(packed, Is.EqualTo(Slice.Empty));
		}

		private static void Verify(CompressedBitmapBuilder builder, SuperSlowUncompressedBitmap witness)
		{
			var bmpBuilder = builder.ToBitmap();
			var bmpWitness = witness.ToBitmap();
			Console.WriteLine("> B: {0,12} ({1,3}) {2}", bmpBuilder.Bounds, bmpBuilder.CountBits(), bmpBuilder.ToSlice().ToHexaString());
			Console.WriteLine("> W: {0,12} ({1,3}) {2}", bmpWitness.Bounds, bmpWitness.CountBits(), bmpWitness.ToSlice().ToHexaString());
			var rawBuilder = builder.ToBooleanArray();
			var rawWitness = witness.ToBooleanArray();
			Console.WriteLine("> B: " + bmpBuilder.Dump());
			Console.WriteLine("> W: " + bmpWitness.Dump());

			var a = SuperSlowUncompressedBitmap.Dump(rawBuilder).ToString().Split('\n');
			var b = SuperSlowUncompressedBitmap.Dump(rawWitness).ToString().Split('\n');

			Console.WriteLine(String.Join("\n", a.Zip(b, (x, y) => (x == y ? "= " : "##") + x + "\n  " + y)));

			Assert.That(rawBuilder, Is.EqualTo(rawWitness), "Uncompressed bitmap does not match");
		}

		private static bool SetBitAndVerify(CompressedBitmapBuilder builder, SuperSlowUncompressedBitmap witness, int offset)
		{
			Console.WriteLine();
			Console.WriteLine("Set({0}):", offset);
			bool actual = builder.Set(offset);
			bool expected = witness.Set(offset);
			Assert.That(actual, Is.EqualTo(expected), "Set({0})", offset);

			Verify(builder, witness);
			return actual;
		}

		private static bool ClearBitAndVerify(CompressedBitmapBuilder builder, SuperSlowUncompressedBitmap witness, int offset)
		{
			Console.WriteLine();
			Console.WriteLine("Clear({0}):", offset);
			bool actual = builder.Clear(offset);
			bool expected = witness.Clear(offset);
			Assert.That(actual, Is.EqualTo(expected), "Clear({0})", offset);

			Verify(builder, witness);
			return actual;
		}

		[Test]
		public void Test_CompressedBitmapBuilder_Set_Bits()
		{
			// start with an empty bitmap
			var builder = CompressedBitmap.Empty.ToBuilder();
			Assert.That(builder, Is.Not.Null.And.Count.EqualTo(0));
			var witness = new SuperSlowUncompressedBitmap();

			Assert.That(SetBitAndVerify(builder, witness, 0), Is.True);
			Assert.That(SetBitAndVerify(builder, witness, 17), Is.True);
			Assert.That(SetBitAndVerify(builder, witness, 17), Is.False);
			Assert.That(SetBitAndVerify(builder, witness, 31), Is.True);
			Assert.That(SetBitAndVerify(builder, witness, 1234), Is.True);
			Assert.That(SetBitAndVerify(builder, witness, 777), Is.True);
			Assert.That(SetBitAndVerify(builder, witness, 62), Is.True);
			Assert.That(SetBitAndVerify(builder, witness, 774), Is.True);
			Assert.That(SetBitAndVerify(builder, witness, 124), Is.True);
			Assert.That(SetBitAndVerify(builder, witness, 93), Is.True);
		}

		[Test]
		public void Test_CompressedBitmapBuilder_Clear_Bits()
		{
			var builder = CompressedBitmap.Empty.ToBuilder();
			Assert.That(builder, Is.Not.Null.And.Count.EqualTo(0));
			var witness = new SuperSlowUncompressedBitmap();

			// clearing anything in the empty bitmap is a no-op
			Assert.That(ClearBitAndVerify(builder, witness, 0), Is.False);
			Assert.That(ClearBitAndVerify(builder, witness, 42), Is.False);
			Assert.That(ClearBitAndVerify(builder, witness, int.MaxValue), Is.False);

			Assert.That(SetBitAndVerify(builder, witness, 42), Is.True);
			Assert.That(ClearBitAndVerify(builder, witness, 42), Is.True, "Clear just after set");
			Assert.That(ClearBitAndVerify(builder, witness, 42), Is.False, "Clear just after clear");

		}

		[Test]
		public void Test_CompressedBitmapBuilder_Linear_Sets()
		{
			// for each bit, from 0 to N-1, set it with proability P
			// => this test linear insertion, that always need to patch or append at the end of the bitmap

			var builder = CompressedBitmap.Empty.ToBuilder();
			var witness = new SuperSlowUncompressedBitmap();

			int N = 5 * 1000;
			int P = 100;

			var rnd = new Random(12345678);
			for (int i = 0; i < N; i++)
			{
				if (rnd.Next(P) == 42)
				{
					SetBitAndVerify(builder, witness, rnd.Next(N));
				}
			}
		}

		[Test]
		public void Test_CompressedBitmapBuilder_Random_Sets()
		{
			// randomly set K bits in a set of N possible bits (with possible overlap)
			// => this test random insertions that need to modifiy the inside of a bitmap

			var builder = CompressedBitmap.Empty.ToBuilder();
			var witness = new SuperSlowUncompressedBitmap();

			int N = 5 * 1000;
			int K = 100;

			var rnd = new Random(12345678);
			for (int i = 0; i < K; i++)
			{
				SetBitAndVerify(builder, witness, rnd.Next(N));
			}
		}

		[Test]
		public void Test_CompressedBitmapBuilder_Random_Sets_And_Clears()
		{
			// randomly alternate between setting and clearing random bits

			int K = 20;
			int S = 100;
			int C = 100;
			int N = 5 * 1000;

			var bmp = CompressedBitmap.Empty;

			var witness = new SuperSlowUncompressedBitmap();

			var rnd = new Random(12345678);
			for (int k = 0; k < K; k++)
			{
				Console.WriteLine("### Generation " + k);

				// convert to builder
				var builder = bmp.ToBuilder();
				Verify(builder, witness);

				// set S bits
				for (int i = 0; i < S; i++)
				{
					int p = rnd.Next(N);
					builder.Set(p);
					witness.Set(p);
					//SetBitAndVerify(builder, witness, p);
				}

				// clear C bits
				for (int i = 0; i < C; i++)
				{
					int p = rnd.Next(N);
					//ClearBitAndVerify(builder, witness, p);
					builder.Clear(p);
					witness.Clear(p);
				}

				// pack back to bitmap
				bmp = builder.ToBitmap();
				Console.WriteLine();
				Console.WriteLine("> Result of gen #{0}: {1}", k, bmp.Dump());
				Console.WriteLine("> " + bmp.ToSlice().ToHexaString());
				Console.WriteLine();
			}
		}

		[Test]
		public void TestFoo()
		{
			var rnd = new Random();

			Func<Slice, Slice> compress = (input) =>
			{
				Console.WriteLine("IN  [{0}] => {1}", input.Count, input);

				var writer = new CompressedBitmapWriter();
				int r = WordAlignHybridEncoder.CompressTo(input, writer);

				Slice compressed = writer.GetBuffer();
				Console.WriteLine("OUT [{0}] => {1} [r={2}]", compressed.Count, compressed, r);
				var sb = new StringBuilder();
				Console.WriteLine(WordAlignHybridEncoder.DumpCompressed(compressed).ToString());
				Console.WriteLine();
				return compressed;
			};

			compress(Slice.FromString("This is a test of the emergency broadcast system"));

			// all zeroes (multiple of 31 bits)
			compress(Slice.Repeat(0, 62));
			// all zeroes (with padding)
			compress(Slice.Repeat(0, 42));

			// all ones (multiple of 31 bits)
			compress(Slice.Repeat(255, 62));
			// all ones (with padding)
			compress(Slice.Repeat(255, 42));

			// random stuff (multiple of 31 bits)
			compress(Slice.Random(rnd, 42));
			// random stuff (with padding)
			compress(Slice.Random(rnd, 42));

			// mostly zeroes
			Action<byte[], int> setBit = (b, p) => { b[p >> 3] |= (byte)(1 << (p & 7)); };
			Func<int, byte[]> mostlyZeroes = (count) =>
			{
				var buf = new byte[1024];
				for (int i = 0; i < count; i++)
				{
					setBit(buf, rnd.Next(buf.Length * 8));
				}
				Console.WriteLine("Mostly zeroes: " + count);
				return buf;
			};

			compress(Slice.Create(mostlyZeroes(1)));
			compress(Slice.Create(mostlyZeroes(10)));
			compress(Slice.Create(mostlyZeroes(42)));
			compress(Slice.Create(mostlyZeroes(100)));


			// mostly ones
			Action<byte[], int> clearBit = (b, p) => { b[p >> 3] &= (byte)~(1 << (p & 7)); };
			Func<int, byte[]> mostlyOnes = (count) =>
			{
				var buf = new byte[1024];
				for (int i = 0; i < buf.Length; i++) buf[i] = 0xFF;
				for (int i = 0; i < 10; i++)
				{
					clearBit(buf, rnd.Next(buf.Length * 8));
				}
				Console.WriteLine("Mostly ones: " + count);
				return buf;
			};

			compress(Slice.Create(mostlyOnes(1)));
			compress(Slice.Create(mostlyOnes(10)));
			compress(Slice.Create(mostlyOnes(42)));
			compress(Slice.Create(mostlyOnes(100)));

			// progressive
			Func<byte[], int, bool> testBit = (b, p) => (b[p >> 3] & (1 << (p & 7))) != 0;

			const int VALUES = 8192;
			var buffer = new byte[VALUES / 8];
			var output = new CompressedBitmapWriter();
			WordAlignHybridEncoder.CompressTo(Slice.Create(buffer), output);
			Console.WriteLine("{0}\t{1}\t1024", 0, output.Length);
			for (int i = 0; i < VALUES / 8; i++)
			{
				int p;
				do
				{
					p = rnd.Next(VALUES);
				}
				while (testBit(buffer, p));

				setBit(buffer, p);

				output.Reset();
				WordAlignHybridEncoder.CompressTo(Slice.Create(buffer), output);
				Console.WriteLine("{0}\t{1}\t1024", 1.0d * (i + 1) / VALUES, output.Length);
			}

		}

		private class Character
		{
			public int Id { get; set; }
			public string Name { get; set; } // will mostly be used for sorting
			public string Gender { get; set; } // poor man's enum with 49%/49%/1% distribution
			public string Job { get; set; } // regular enum with random distribution
			public DateTime? Born { get; set; } // accurate to the day, usually used in range queries
			public bool Dead { get; set; } // zomg, spoilers ahead! probably used as an exclusion flag (like IsDeleted)

		}

		private static Action<TDoc> MakeInserter<TDoc, TKey>(Dictionary<TKey, CompressedBitmap> index, Func<TDoc, int> idFunc, Func<TDoc, TKey> keyFunc)
		{
			return (TDoc doc) =>
			{
				int docId = idFunc(doc);
				TKey indexedValue = keyFunc(doc);
				CompressedBitmap bmp;
				if (!index.TryGetValue(indexedValue, out bmp)) bmp = CompressedBitmap.Empty;

				var builder = bmp.ToBuilder();
				builder.Set(docId);
				index[indexedValue] = builder.ToBitmap();
			};
		}

		private static List<Character> DumpIndexQueryResult(Dictionary<int, Character> characters, CompressedBitmap bitmap)
		{
			var results = new List<Character>();
			foreach (var docId in bitmap.GetView())
			{
				Character charac;
				Assert.That(characters.TryGetValue(docId, out charac), Is.True);

				results.Add(charac);
				Console.WriteLine("- {0}: {1} {2}{3}", docId, charac.Name, charac.Gender == "Male" ? "\u2642" : charac.Gender == "Female" ? "\u2640" : charac.Gender, charac.Dead ? " (\u271D)" : "");
			}
			return results;
		}

		[Test]
		public void Test_Merging_Multiple_Bitmaps()
		{
			var dataSet = new List<Character>()
			{
				new Character { Id = 1, Name = "Spike Spiegel", Gender = "Male", Job="Bounty_Hunter", Born = new DateTime(2044, 6, 26), Dead = true /* bang! */ },
				new Character { Id = 2, Name = "Jet Black", Gender = "Male", Job="Bounty_Hunter", Born = new DateTime(2035, 12, 13) },
				new Character { Id = 3, Name = "Faye Valentine", Gender = "Female", Job="Bounty_Hunter", Born = new DateTime(1994, 8, 14) },
				new Character { Id = 4, Name = "Edward Wong Hau Pepelu Tivruski IV", Gender = "Female", Job="Hacker", Born = new DateTime(2058, 1, 1) },
				new Character { Id = 5, Name = "Ein", Gender = "Male", Job="Dog" },
				new Character { Id = 6, Name = "Vicious", Gender = "Male", Job = "Vilain", Dead = true },
				new Character { Id = 7, Name = "Julia", Gender = "Female", Job = "Damsel_In_Distress", Dead = true /* It's all a dream */ },
				new Character { Id = 8, Name = "Victoria Tepsichore", Gender = "Female", Job = "Space_Trucker" },
				new Character { Id = 9, Name = "Punch", Gender = "Male", Job = "TV_Host" },
				new Character { Id = 10, Name = "Judy", Gender = "Female", Job = "TV_Host" },
			};

			// poor man's in memory database
			var database = new Dictionary<int, Character>();
			var indexByGender = new Dictionary<string, CompressedBitmap>(StringComparer.OrdinalIgnoreCase);
			var indexByJob = new Dictionary<string, CompressedBitmap>(StringComparer.OrdinalIgnoreCase);
			var indexOfTheDead = new Dictionary<bool, CompressedBitmap>();

			// simulate building the indexes one document at a time
			var indexers = new[]
			{
				MakeInserter<Character, string>(indexByGender, (doc) => doc.Id, (doc) => doc.Gender),
				MakeInserter<Character, string>(indexByJob, (doc) => doc.Id, (doc) => doc.Job),
				MakeInserter<Character, bool>(indexOfTheDead, (doc) => doc.Id, (doc) => doc.Dead),
			};

			Console.WriteLine("Inserting into database...");
			foreach (var character in dataSet)
			{
				database[character.Id] = character;
				foreach (var indexer in indexers)
				{
					indexer(character);
				}
			}

			// dump the indexes
			Console.WriteLine();
			Console.WriteLine("Genders:");
			foreach (var kv in indexByGender)
			{
				Console.WriteLine("- {0}: {1}", kv.Key, kv.Value.Dump());
			}
			Console.WriteLine();
			Console.WriteLine("Jobs:");
			foreach (var kv in indexByJob)
			{
				Console.WriteLine("- {0}: {1}", kv.Key, kv.Value.Dump());
			}
			Console.WriteLine();
			Console.WriteLine("DeadOrAlive:");
			foreach (var kv in indexOfTheDead)
			{
				Console.WriteLine("- {0}: {1}", kv.Key ? "dead" : "alive", kv.Value.Dump());
			}

			// Où sont les femmes ?
			Console.WriteLine();
			Console.WriteLine("indexByGender.Lookup('Female')");
			CompressedBitmap females;
			Assert.That(indexByGender.TryGetValue("Female", out females), Is.True);
			Console.WriteLine("=> {0}", females.Dump());
			DumpIndexQueryResult(database, females);

			// R.I.P
			Console.WriteLine();
			Console.WriteLine("indexOfTheDead.Lookup(dead: true)");
			CompressedBitmap deadPeople;
			Assert.That(indexOfTheDead.TryGetValue(true, out deadPeople), Is.True);
			Console.WriteLine("=> {0}", deadPeople.Dump());
			DumpIndexQueryResult(database, deadPeople);

			// combination of both
			Console.WriteLine();
			Console.WriteLine("indexByGender.Lookup('Female') AND indexOfTheDead.Lookup(dead: true)");
			var julia = WordAlignHybridEncoder.LogicalAndCompressed(females, deadPeople);
			Console.WriteLine("=> {0}", julia.Dump());
			DumpIndexQueryResult(database, julia);

			// the crew
			Console.WriteLine();
			Console.WriteLine("indexByJob.Lookup('Bounty_Hunter' OR 'Hacker' OR 'Dog')");
			var bmps = new[] { "Bounty_Hunter", "Hacker", "Dog" }.Select(job => indexByJob[job]).ToList();
			var crew = CompressedBitmap.Empty;
			foreach (var bmp in bmps)
			{
				crew = WordAlignHybridEncoder.LogicalOrCompressed(crew, bmp);
			}
			Console.WriteLine("=> {0}", crew.Dump());
			DumpIndexQueryResult(database, crew);

		}

	}

}
