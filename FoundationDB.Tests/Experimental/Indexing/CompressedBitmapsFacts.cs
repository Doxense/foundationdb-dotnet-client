﻿#region BSD Licence
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
	using MathNet.Numerics.Distributions;
	using System.Diagnostics;
	using System.Globalization;
	using FoundationDB.Layers.Tuples;
	using System.IO;

	[TestFixture]
	[Category("LongRunning")]
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

		public class MemoryIndex<TKey>
		{
			public readonly Dictionary<TKey, CompressedBitmap> Values;
			public readonly Dictionary<TKey, int> Statistics;

			public MemoryIndex(IEqualityComparer<TKey> comparer = null)
			{
				comparer = comparer ?? EqualityComparer<TKey>.Default;
				this.Values = new Dictionary<TKey, CompressedBitmap>(comparer);
				this.Statistics = new Dictionary<TKey, int>(comparer);
			}

			public CompressedBitmap Lookup(TKey value)
			{
				CompressedBitmap bmp;
				return this.Values.TryGetValue(value, out bmp) ? bmp : null;
			}

			public int Count(TKey value)
			{
				int cnt;
				return this.Statistics.TryGetValue(value, out cnt) ? cnt : 0;
			}

			public double Frequency(TKey value)
			{
				return (double)Count(value) / this.Statistics.Values.Sum();
			}
		}

		private static Action<TDoc> MakeInserter<TDoc, TKey>(MemoryIndex<TKey> index, Func<TDoc, int> idFunc, Func<TDoc, TKey> keyFunc)
		{
			return (TDoc doc) =>
			{
				int docId = idFunc(doc);
				TKey indexedValue = keyFunc(doc);
				CompressedBitmap bmp;
				int count;
				if (!index.Values.TryGetValue(indexedValue, out bmp))
				{
					bmp = CompressedBitmap.Empty;
					count = 0;
				}
				else
				{
					count = index.Statistics[indexedValue];
				}

				var builder = bmp.ToBuilder();
				builder.Set(docId);
				index.Values[indexedValue] = builder.ToBitmap();
				index.Statistics[indexedValue] = count + 1;
			};
		}

		private static string MakeHeatMap(int[] map)
		{
			int max = map.Max();
			string scale = "`.:;+=xX$&#";
			double r = (double)(scale.Length - 1) / max;
			var chars = new char[map.Length];
			for (int i = 0; i < map.Length; i++)
			{
				if (map[i] == 0)
					chars[i] = '\xA0';
				else
					chars[i] = scale[(int)Math.Round(r * map[i], MidpointRounding.AwayFromZero)];
			}
			return new string(chars);
		}

		private static void DumpIndex<TKey, TVal>(string label, MemoryIndex<TKey> index, Func<TKey, int, TVal> orderBy, IComparer<TVal> comparer = null, bool heatMaps = false)
		{
			comparer = comparer ?? Comparer<TVal>.Default;

			int total = index.Statistics.Values.Sum();
			long totalLegacy = 0;
			int[] map = new int[100];
			double r = (double)(map.Length - 1) / total;
			Console.WriteLine("__{0}__", label);
			Console.WriteLine("| Indexed Value           |  Count | Total % | Words |  Lit%  | 1-Bits |  Word% |   Bitmap | ratio % |   Legacy  | ratio % |" + (heatMaps ? " HeatMap |" : ""));
			Console.WriteLine("|:------------------------|-------:|--------:|------:|-------:|-------:|-------:|---------:|--------:|----------:|--------:|" + (heatMaps ? ":-----------------------------------------------------------------------|" : ""));
			foreach (var kv in index.Values.OrderBy((kv) => orderBy(kv.Key, index.Count(kv.Key)), comparer))
			{
				var t = FdbTuple.Create(kv.Key);
				var tk = t.ToSlice();

				int bits, words, literals, fillers;
				double ratio;
				kv.Value.GetStatistics(out bits, out words, out literals, out fillers, out ratio);

				long legacyIndexSize = 0; // size estimate of a regular FDB index (..., "Value", GUID) = ""
				Array.Clear(map, 0, map.Length);
				foreach(var p in kv.Value.GetView())
				{
					map[(int)(r * p)]++;
					legacyIndexSize += 3 + tk.Count + 17;
				}
				totalLegacy += legacyIndexSize;

				int bytes = kv.Value.ToSlice().Count;

				Console.WriteLine(string.Format(
					CultureInfo.InvariantCulture,
					"| {0,-24}| {1,6:N0} | {2,6:N2}% | {3,5:N0} | {4,5:N1}% | {5,6:N0} | {6,6:N2} | {7,8:N0} | {8,6:N2}% | {9,9:N0} | {10,6:N2}% |" + (heatMaps ? " `{11}` |" : ""),
					/*0*/ t,
					/*1*/ index.Count(kv.Key),
					/*2*/ 100.0 * index.Frequency(kv.Key),
					/*3*/ words,
					/*4*/ (100.0 * literals) / words,
					/*5*/ bits,
					/*6*/ 1.0 * bits / words,
					/*7*/ bytes,
					/*8*/ 100.0 * ratio,
					/*9*/ legacyIndexSize,
					/*A*/ (100.0 * bytes) / legacyIndexSize,
					/*B*/ heatMaps ? MakeHeatMap(map) : ""
				));
			}

			Console.WriteLine(string.Format(
				CultureInfo.InvariantCulture,
				"> {0:N0} distinct value(s), {1:N0} document(s), {2:N0} bitmap bytes, {3:N0} legacy bytes",
				index.Values.Count,
				total,
				index.Values.Values.Sum(x => x.ToSlice().Count),
				totalLegacy
			));
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
			var indexByGender = new MemoryIndex<string>(StringComparer.OrdinalIgnoreCase);
			var indexByJob = new MemoryIndex<string>(StringComparer.OrdinalIgnoreCase);
			var indexOfTheDead = new MemoryIndex<bool>();

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
			DumpIndex("Genders", indexByGender, (s, _) => s);

			Console.WriteLine();
			DumpIndex("Jobs", indexByJob, (s, _) => s);

			Console.WriteLine();
			DumpIndex("DeadOrAlive", indexOfTheDead, (s, _) => s);

			// Où sont les femmes ?
			Console.WriteLine();
			Console.WriteLine("indexByGender.Lookup('Female')");
			CompressedBitmap females = indexByGender.Lookup("Female");
			Assert.That(females, Is.Not.Null);
			Console.WriteLine("=> {0}", females.Dump());
			DumpIndexQueryResult(database, females);

			// R.I.P
			Console.WriteLine();
			Console.WriteLine("indexOfTheDead.Lookup(dead: true)");
			CompressedBitmap deadPeople = indexOfTheDead.Lookup(true);
			Assert.That(deadPeople, Is.Not.Null);
			Console.WriteLine("=> {0}", deadPeople.Dump());
			DumpIndexQueryResult(database, deadPeople);

			// combination of both
			Console.WriteLine();
			Console.WriteLine("indexByGender.Lookup('Female') AND indexOfTheDead.Lookup(dead: true)");
			var julia = WordAlignHybridEncoder.And(females, deadPeople);
			Console.WriteLine("=> {0}", julia.Dump());
			DumpIndexQueryResult(database, julia);

			// the crew
			Console.WriteLine();
			Console.WriteLine("indexByJob.Lookup('Bounty_Hunter' OR 'Hacker' OR 'Dog')");
			var bmps = new[] { "Bounty_Hunter", "Hacker", "Dog" }.Select(job => indexByJob.Lookup(job)).ToList();
			CompressedBitmap crew = null;
			foreach (var bmp in bmps)
			{
				if (crew == null)
					crew = bmp;
				else
					crew = WordAlignHybridEncoder.Or(crew, bmp);
			}
			crew = crew ?? CompressedBitmap.Empty;
			Console.WriteLine("=> {0}", crew.Dump());
			DumpIndexQueryResult(database, crew);

		}

		[Test]
		public void Test_Logical_Binary_Operations()
		{

			var a = SuperSlowUncompressedBitmap.FromBitString("0101").ToBitmap();
			var b = SuperSlowUncompressedBitmap.FromBitString("0011").ToBitmap();

			var and = a.And(b);
			Assert.That(and, Is.Not.Null);
			Assert.That(new SuperSlowUncompressedBitmap(and).ToBitString(), Is.EqualTo("0001000000000000000000000000000"), "a AND b");

			var or  = a.Or(b);
			Assert.That(or, Is.Not.Null);
			Assert.That(new SuperSlowUncompressedBitmap(or).ToBitString(), Is.EqualTo("0111000000000000000000000000000"), "a OR b");

			var xor = a.Xor(b);
			Assert.That(xor, Is.Not.Null);
			Assert.That(new SuperSlowUncompressedBitmap(xor).ToBitString(), Is.EqualTo("0110000000000000000000000000000"), "a XOR b");

			var andNot = a.AndNot(b);
			Assert.That(andNot, Is.Not.Null);
			Assert.That(new SuperSlowUncompressedBitmap(andNot).ToBitString(), Is.EqualTo("0100000000000000000000000000000"), "a AND NOT b");

			var orNot = a.OrNot(b);
			Assert.That(orNot, Is.Not.Null);
			Assert.That(new SuperSlowUncompressedBitmap(orNot).ToBitString(), Is.EqualTo("1101111111111111111111111111111"), "a OR NOT b");

			var xorNot = a.XorNot(b);
			Assert.That(xorNot, Is.Not.Null);
			Assert.That(new SuperSlowUncompressedBitmap(xorNot).ToBitString(), Is.EqualTo("1001111111111111111111111111111"));

		}

		#region Coin Toss...

		public sealed class CoinToss
		{
			public const int HEAD = 1; // 49.5%
			public const int TAIL = 2; // 49.5%
			public const int EDGE = 0; //  1.0%

			/// <summary>Toss unique id (random guid)</summary>
			public Guid Id { get; set; }
			/// <summary>False if the toss was discarded as invalid</summary>
			public bool Valid { get; set; } // 99.9% true, 0.1% false
			/// <summary>True for head, False for tails, null for edge</summary>
			public int Result { get; set; }
			/// <summary>Number of completed 360° flips</summary>
			public int Flips { get; set; }
			/// <summary>Coin elevation (in cm)</summary>
			public double Elevation { get; set; }
			/// <summary>true for daytime, false for nighttime</summary>
			public bool Daytime { get; set; }
			/// <summary>Name of location where the toss was performed</summary>
			public string Location { get; set; }
		}

		[Test]
		public void Test_Randomized_Data()
		{
			#region Data Generators...

			Random rnd = null; // initialized later

			var dfFlips = new Cauchy(10, 4, rnd);
			Func<int> makeFlips = () =>
			{
				int x = 0;
				while (x <= 0 || x >= 30) { x = (int)Math.Floor(dfFlips.Sample()); }
				return x;
			};

			var dfElev = new Cauchy(10, 1, rnd);
			Func<double> makeElevation = () =>
			{
				double x = 0;
				while (x <= 0.0 || x >= 30) { x = dfElev.Sample(); }
				return x;
			};

			bool flipFlop = false;
			Func<bool> makeFlipFlop = () =>
			{
				if (rnd.NextDouble() < 0.01) flipFlop = !flipFlop;
				return flipFlop;
			};

			var cities = new[]
			{ 
				"Paris", "Marseilles", "Lyon", "Toulouse", "Nice",
				"Nantes", "Strasbourg", "Montpellier", "Bordeaux", "Lille",
				"Rennes", "Reims", "Le Havre", "Saint-Étienne", "Toulon",
				"Grenoble", "Dijon", "Angers", "Saint-Denis", "Villeurbanne",
				"Nîmes", "Le Mans", "Clermont-Ferrand", "Aix-en-Provence", "Brest"
			};
			var dfLoc = new Cauchy(0, 1.25, rnd);
			Func<string> makeLocation = () =>
			{
				int x = cities.Length;
				while (x >= cities.Length) { x = (int)Math.Floor(Math.Abs(dfLoc.Sample())); }
				return cities[x];
			};
			Func<int> makeHeadsOrTails = () => rnd.NextDouble() < 0.01 ? CoinToss.EDGE : rnd.NextDouble() <= 0.5 ? CoinToss.HEAD : CoinToss.TAIL; // biased!
			Func<bool> makeValid = () => rnd.Next(1000) != 666;

			#endregion

			//foreach (var N in new[] { 1000, 2000, 5000, 10 * 1000, 20 * 1000, 50 * 1000, 100 * 1000 })
			const int N = 10 * 1000;
			{
				Console.WriteLine("=================================================================================================================================================================================================================================");
				Console.WriteLine("N = {0:N0}", N);
				Console.WriteLine("=================================================================================================================================================================================================================================");

				rnd = new Random(123456);

				var dataSet = Enumerable
					.Range(0, N)
					.Select(i => new KeyValuePair<int, CoinToss>(i, new CoinToss
					{
						Id = Guid.NewGuid(),
						Valid = makeValid(),
						Result = makeHeadsOrTails(),
						Flips = makeFlips(),
						Elevation = makeElevation(),
						Location = makeLocation(),
						Daytime = makeFlipFlop(),
					}))
					.ToList();

				var indexLoc = new MemoryIndex<string>(StringComparer.Ordinal);
				var indexValid = new MemoryIndex<bool>();
				var indexResult = new MemoryIndex<int>();
				var indexFlips = new MemoryIndex<int>();
				var indexElevation = new MemoryIndex<double>(); // quantized!
				var indexFlipFlop = new MemoryIndex<bool>();

				var inserters = new []
				{
					MakeInserter<KeyValuePair<int, CoinToss>, int>(indexResult, (kv) => kv.Key, (kv) => kv.Value.Result),
					MakeInserter<KeyValuePair<int, CoinToss>, bool>(indexValid, (kv) => kv.Key, (kv) => kv.Value.Valid),
					MakeInserter<KeyValuePair<int, CoinToss>, bool>(indexFlipFlop, (kv) => kv.Key, (kv) => kv.Value.Daytime),
					MakeInserter<KeyValuePair<int, CoinToss>, int>(indexFlips, (kv) => kv.Key, (kv) => kv.Value.Flips),
					MakeInserter<KeyValuePair<int, CoinToss>, double>(indexElevation, (kv) => kv.Key, (kv) => Math.Round(kv.Value.Elevation, 1, MidpointRounding.AwayFromZero)),
					MakeInserter<KeyValuePair<int, CoinToss>, string>(indexLoc, (kv) => kv.Key, (kv) => kv.Value.Location),
				};

				var database = new Dictionary<int, CoinToss>();
				//Console.Write("Inserting data: ...");
				foreach (var data in dataSet)
				{
					//if (database.Count % 1000 == 0) Console.Write("\rInserting data: {0} / {1}", database.Count, N);
					database[data.Key] = data.Value;
					foreach (var inserter in inserters) inserter(data);
				}
				//Console.WriteLine("\rInserting data: {0} / {1}", database.Count, N);

				Console.WriteLine();
				DumpIndex("Result", indexResult, (s, _) => s, heatMaps: true);

				Console.WriteLine();
				DumpIndex("Valid", indexValid, (s, _) => s, heatMaps: true);

				Console.WriteLine();
				DumpIndex("FlipFlops", indexFlipFlop, (s, _) => s, heatMaps: true);

				Console.WriteLine();
				DumpIndex("Flips", indexFlips, (s, _) => s, heatMaps: true);

				Console.WriteLine();
				DumpIndex("Location", indexLoc, (_, n) => -n, heatMaps: true);

				Console.WriteLine();
				DumpIndex("Elevation", indexElevation, (s, _) => s, heatMaps: true);

				//Console.WriteLine(indexValid.Values[true].Dump());
				//Console.WriteLine(indexValid.Values[true].ToSlice().ToHexaString());
				Console.WriteLine();
				Console.WriteLine();
			}

		}

		#endregion

		public void Test_BigBadIndexOfTheDead()
		{
			// simulate a dataset where 50,000 users create a stream of 10,000,000  events, with a non uniform distribution, ie: few users making the bulk, and a long tail of mostly inactive users
			const int N = 10 * 1000 * 1000;
			const int K = 50 * 1000;

			var rnd = new Random(123456);

			#region create a non uniform random distribution for the users

			// step1: create a semi random distribution for the values
			Console.WriteLine("Creating Probability Distribution Function for {0:N0} users...", K);
			var pk = new double[K];
			// step1: each gets a random score
			for (int i = 0; i < pk.Length; i++)
			{
				pk[i] = Math.Pow(rnd.NextDouble(), 10);
			}
			// then sort + reverse
			Array.Sort(pk); Array.Reverse(pk);
			// step2: spread
			double sum = 0;
			for(int i = 0; i < pk.Length;i++)
			{
				var s = pk[i];
				pk[i] = sum;
				sum += s;
			}
			// step3: normalize
			double r = (N - 1) / sum;
			for (int i = 0; i < pk.Length; i++)
			{
				pk[i] = Math.Floor(pk[i] * r);
			}
			sum = N;

			// step4: fudge the tail
			r = pk[pk.Length - 1];
			double delta = 1;
			for (int i = pk.Length - 2; i >= 0; i--)
			{
				if (pk[i] < r) break;
				r -= delta;
				delta *= 1.0001;
				pk[i] = r;
			}

			//for (int i = 0; i < pk.Length; i += 500)
			//{
			//	Console.WriteLine(pk[i].ToString("R", CultureInfo.InvariantCulture));
			//}

			int p25 = Array.BinarySearch(pk, 0.25 * sum); p25 = p25 < 0 ? ~p25 : p25;
			int p50 = Array.BinarySearch(pk, 0.50 * sum); p50 = p50 < 0 ? ~p50 : p50;
			int p75 = Array.BinarySearch(pk, 0.75 * sum); p75 = p75 < 0 ? ~p75 : p75;
			int p95 = Array.BinarySearch(pk, 0.95 * sum); p95 = p95 < 0 ? ~p95 : p95;
			Console.WriteLine("> PDF: P25={0:G2} %, P50={1:G2} %, P75={2:G2} %, P95={3:G2} %", 100.0 * p25 / K, 100.0 * p50 / K, 100.0 * p75 / K, 100.0 * p95 / K);

			#endregion

			#region Create the random event dataset...

			// a user will be selected randomnly, and will be able to produce a random number of consecutive events, until we reach the desired amount of events

			Console.WriteLine("Creating dataset for {0:N0} documents...", N);
			var dataSet = new int[N];
			//int j = 0;
			//for (int i = 0; i < N; i++)
			//{
			//	if (pk[j + 1] <= i) j++;
			//	dataSet[i] = j;
			//}
			//// scramble dataset
			//for (int i = 0; i < N;i++)
			//{
			//	var p = rnd.Next(i);
			//	var tmp = dataSet[i];
			//	dataSet[i] = dataSet[p];
			//	dataSet[p] = tmp;
			//}

			int user = 0;
			int j = 0;
			while (j < N)
			{
				if (rnd.NextDouble() * sum * 1.005 >= pk[user])
				{
					int n = 1 + (int)(Math.Pow(rnd.NextDouble(), 2) * 10);
					while (n-- > 0 && j < N)
					{
						dataSet[j++] = user;
					}
				}
				++user;
				if (user == K) user = 0;
			}

			Console.WriteLine("Computing control statistics...");
			// compute the control value for the counts per value
			var controlStats = dataSet
				.GroupBy(x => x).Select(g => new { Value = g.Key, Count = g.Count() })
				.OrderByDescending(x => x.Count)
				.ToList();
			Console.WriteLine("> Found {0:N0} unique values", controlStats.Count);

			#endregion

			// create pseudo-index
			Console.WriteLine("Indexing {0:N0} documents...", N);
			var sw = Stopwatch.StartNew();
			var index = new Dictionary<int, CompressedBitmapBuilder>(K);
			for (int id = 0; id < dataSet.Length; id++)
			{
				int value = dataSet[id];
				CompressedBitmapBuilder builder;
				if (!index.TryGetValue(value, out builder))
				{
					builder = new CompressedBitmapBuilder(CompressedBitmap.Empty);
					index[value] = builder;
				}
				builder.Set(id);
			}
			sw.Stop();
			Console.WriteLine("> Found {0:N0} unique values in {1:N1} sec", index.Count, sw.Elapsed.TotalSeconds);

			// verify the counts
			Console.WriteLine("Verifying index results...");
			var log = new StringWriter(CultureInfo.InvariantCulture);
			long totalBitmapSize = 0;
			j = 0;
			foreach (var kv in controlStats)
			{
				CompressedBitmapBuilder builder;
				Assert.That(index.TryGetValue(kv.Value, out builder), Is.True, "{0} is missing from index", kv.Value);
				var bmp = builder.ToBitmap();
				int bits, words, a, b;
				double ratio;
				bmp.GetStatistics(out bits, out words, out a, out b, out ratio);
				Assert.That(bits, Is.EqualTo(kv.Count), "{0} has invalid count", kv.Value);
				int sz = bmp.ToSlice().Count;
				log.WriteLine("{0,8} : {1,5} bits, {2} words ({3} lit. / {4} fil.), {5:N0} bytes, {6:N3} bytes/doc, {7:N2}% compression", kv.Value, bits, words, a, b, sz, 1.0 * sz / bits, 100.0 * (4 + 17 + sz) / (17 + (4 + 17) * bits));
				totalBitmapSize += sz;
				//if (j % 500 == 0) Console.WriteLine((100.0 * b / words));
				//if (j % 500 == 0) Console.WriteLine(bmp.Dump());
				j++;
			}
			Assert.That(index.Count, Is.EqualTo(controlStats.Count), "Some values have not been indexed properly");
			Console.WriteLine("> success!");
			Console.WriteLine("Total index size for {0:N0} documents and {1:N0} values is {2:N0} bytes", N, K, totalBitmapSize);

			Console.WriteLine();
			Console.WriteLine("Dumping results:");
			Trace.WriteLine(log.ToString());

		}

	}

}
