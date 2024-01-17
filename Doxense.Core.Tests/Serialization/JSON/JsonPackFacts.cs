﻿#region Copyright (c) 2023-2024 SnowBank SAS, (c) 2005-2023 Doxense SAS
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

#nullable disable

// ReSharper disable StringLiteralTypo
// ReSharper disable CommentTypo

namespace Doxense.Serialization.Json.Binary.Tests
{
	using System;
	using System.Collections.Generic;
	using System.IO.Compression;
	using System.Linq;
	using Doxense.Testing;
	using NUnit.Framework;

	[TestFixture]
	[Category("Core-SDK")]
	public class JsonPackFacts : DoxenseTest
	{

		private void VerifyRoundtrip(JsonValue value)
		{
			Log("-----------------");

			DumpCompact("ORIGINAL", value);
			var bytes = JsonPack.Encode(value);
			Log("RAW: ({0}) {1}", bytes.Count, bytes.ToString("X"));
#if DEBUG
			DumpHexa(bytes);
#endif
			var decoded = JsonPack.Decode(bytes);
			DumpCompact("DECODED", decoded);

			Assert.That(decoded, Is.EqualTo(value), "Decoded JSON value does not match original");

			Log("done");
#if !DEBUG
			// mini-bench (uniquement en mode RELEASE)
			var jbytes = value.ToJsonBuffer(CrystalJsonSettings.JsonCompact);
			_ = CrystalJson.Parse(jbytes); // warmup

			const int N = 1000;
			var writer = new Doxense.Memory.SliceWriter(128);
			var sw = System.Diagnostics.Stopwatch.StartNew();
			for (int i = 0; i < N; i++)
			{
				writer.Reset();
				var _ = JsonPack.EncodeTo(ref writer, value);
			}
			sw.Stop();
			Log("Bench: JsonPack encode {0:N1} nanos", sw.Elapsed.TotalMilliseconds * 1_000_000 / N);
			sw.Restart();
			for (int i = 0; i < N; i++)
			{
				var _ = JsonPack.Decode(bytes);
			}
			sw.Stop();
			Log("Bench: JsonPack decode {0:N1} nanos", sw.Elapsed.TotalMilliseconds * 1_000_000 / N);

			sw.Restart();
			for (int i = 0; i < N; i++)
			{
				var _ = value.ToJsonBuffer(CrystalJsonSettings.JsonCompact);
			}
			sw.Stop();
			Log("Bench: JSON     encode {0:N1} nanos", sw.Elapsed.TotalMilliseconds * 1_000_000 / N);
			sw.Restart();
			for (int i = 0; i < N; i++)
			{
				var _ = CrystalJson.Parse(jbytes);
			}
			sw.Stop();
			Log("Bench: JSON     decode {0:N1} nanos", sw.Elapsed.TotalMilliseconds * 1_000_000 / N);
#endif
		}

		private static long HexDecode(string hexa)
		{
			if (string.IsNullOrEmpty(hexa)) return 0;

			int offset = 0;
			if (hexa.Length > 2 && hexa[0] == '0' && (hexa[1] == 'X' || hexa[1] == 'x'))
			{ // forme "0x..."
				offset = 2;
			}

			long res = 0;
			for (int i = offset; i < hexa.Length; i++)
			{
				// 4 bits par caractère
				res <<= 4;
				int c = hexa[i];
				if (c >= 48 && c <= 57) // '0'..'9'
					res += c - 48;
				else if (c >= 65 && c <= 90) // 'A'..'F'
					res += c - 55;
				else if (c >= 97 && c <= 102) // 'a'..'f'
					res += c - 87;
				else
					return res >> 4; // ERREUR !
			}
			return res;
		}

		private static byte[] HexDecodeArray(string value)
		{
			if (string.IsNullOrEmpty(value)) return value == null ? null : Array.Empty<byte>();
			value = value.Trim().Replace(" ", String.Empty);
			int n = value.Length;
			if ((n & 1) > 0) throw new ArgumentException("Invalid input size! Hexadecimal string size must be even!");
			byte[] r = new byte[n >> 1];
			int p = 0;
			for (int i = 0; i < n; i += 2)
			{
				r[p++] = (byte) HexDecode(value.Substring(i, 2));
			}
			return r;
		}

		private void VerifyReferenceEncoding(string packed, JsonValue expected)
		{
			Log(packed);
			var bytes = HexDecodeArray(packed);
#if FULL_DEBUG
			DumpHexa(bytes);
#endif
			var decoded = JsonPack.Decode(bytes);
			Log("=> {0}", decoded.ToJson());
			Assert.That(decoded, Is.EqualTo(expected));
		}

		[Test]
		public void Test_Encode_Scalars()
		{
			VerifyRoundtrip("hello");
			VerifyRoundtrip(String.Empty);
			VerifyRoundtrip("しのぶ一番");
			VerifyRoundtrip(true);
			VerifyRoundtrip(false);
			VerifyRoundtrip(JsonNull.Null);
			VerifyRoundtrip(0);
			VerifyRoundtrip(1);
			VerifyRoundtrip(123);
			VerifyRoundtrip(255);
			VerifyRoundtrip(256);
			VerifyRoundtrip(1234);
			VerifyRoundtrip(32767);
			VerifyRoundtrip(32768);
			VerifyRoundtrip(int.MaxValue);
			VerifyRoundtrip(uint.MaxValue);
			VerifyRoundtrip(long.MaxValue);
			VerifyRoundtrip(ulong.MaxValue);
			VerifyRoundtrip(-1);
			VerifyRoundtrip(-32);
			VerifyRoundtrip(-33);
			VerifyRoundtrip(-123);
			VerifyRoundtrip(-256);
			VerifyRoundtrip(-257);
			VerifyRoundtrip(-1234);
			VerifyRoundtrip(-32768);
			VerifyRoundtrip(-32769);
			VerifyRoundtrip(int.MinValue);
			VerifyRoundtrip(long.MinValue);
			VerifyRoundtrip(Math.PI);
			VerifyRoundtrip(Math.E);
			VerifyRoundtrip(double.NaN);
			VerifyRoundtrip(decimal.MinusOne);
			VerifyRoundtrip(Guid.Empty);
			VerifyRoundtrip(Guid.NewGuid());
		}

		[Test]
		public void Test_Decode_Reference_Scalars()
		{
			// sanity test: on vérifie qu'on est capable de décoder des versions binaires de référence
			// note: si le format binaire change, il ne faut pas oublier de mettre à jour les encodages de référence!

			#region null

			// null
			VerifyReferenceEncoding(
				"80",
				JsonNull.Null
			);

			#endregion

			#region strings

			VerifyReferenceEncoding(
				"A5 68 65 6C 6C 6F",
				"hello"
			);

			// ""
			VerifyReferenceEncoding(
				"A0",
				""
			);

			VerifyReferenceEncoding(
				"AF E3 81 97 E3 81 AE E3 81 B6 E4 B8 80 E7 95 AA",
				"しのぶ一番"
			);

			#endregion

			#region booleans

			// false
			VerifyReferenceEncoding(
				"81",
				false
			);

			// true
			VerifyReferenceEncoding(
				"82",
				true
			);

			#endregion

			#region integers

			// 0 (integer)
			VerifyReferenceEncoding(
				"00",
				0
			);

			// 1 (integer)
			VerifyReferenceEncoding(
				"01",
				1
			);

			// -1 (integer)
			VerifyReferenceEncoding(
				"FF",
				-1
			);

			// -32 (integer)
			VerifyReferenceEncoding(
				"E0",
				-32
			);

			// 1234 (integer)
			VerifyReferenceEncoding(
				"91 D2 04",
				1234
			);

			// zéro (literal)
			VerifyReferenceEncoding(
				"99 01 30",
				0
			);

			#endregion

			#region literal numbers (text)

			// -1 (literal)
			VerifyReferenceEncoding(
				"99 02 2D 31",
				-1
			);

			// 1234 (literal)
			VerifyReferenceEncoding(
				"99 04 31 32 33 34",
				1234
			);

			// 123.4 (literal)
			VerifyReferenceEncoding(
				"99 05 31 32 33 2E 34",
				123.4
			);

			// Math.PI (literal)
			VerifyReferenceEncoding(
				"99 12 33 2E 31 34 31 35 39 32 36 35 33 35 38 39 37 39 33 31",
				Math.PI
			);

			// double.NaN (literal)
			VerifyReferenceEncoding(
				"99 03 4E 61 4E",
				double.NaN
			);

			#endregion

		}

		[Test]
		public void Test_Encode_Arrays()
		{
			// empty array
			VerifyRoundtrip(JsonArray.Empty);

			// simple arrays

			VerifyRoundtrip(JsonArray.Create("hello", "world"));
			VerifyRoundtrip(JsonArray.Create(1, 2, 3));
			VerifyRoundtrip(JsonArray.Create(1.1, 2.2, 3.3));

			// mixed types
			VerifyRoundtrip(JsonArray.Create("hello", 123, true, JsonNull.Null, Math.PI, false, Guid.NewGuid()));

			// 0..99
			VerifyRoundtrip(Enumerable.Range(0, 100).ToJsonArray());

			// random numbers
			var rnd = new Random();
			VerifyRoundtrip(Enumerable.Range(0, 100).Select(_ => rnd.Next()).ToJsonArray());

			// random guids
			VerifyRoundtrip(Enumerable.Range(0, 10).Select(_ => Guid.NewGuid()).ToJsonArray());
			//note: étrangement, ca se compresse très bien avec Zstd (probablement que les GUID textuels de 36 chars repassent sur ~16 bytes)

			// array of arrays
			VerifyRoundtrip(JsonArray.Create(JsonArray.Create(1, 2, 3), JsonArray.Create(4, 5, 6)));

			// array of objects
			VerifyRoundtrip(JsonArray.Create(JsonObject.Create("Foo", "Bar"), JsonObject.Create("Narf", "Zort")));

		}

		[Test]
		public void Test_Decode_Reference_Arrays()
		{
			//	["hello", "world"]
			VerifyReferenceEncoding(
				"83 A5 68 65 6C 6C 6F A5 77 6F 72 6C 64 84",
				JsonArray.Create("hello", "world")
			);

			//	[1, 2, 3]
			VerifyReferenceEncoding(
				"83 01 02 03 84",
				JsonArray.Create(1, 2, 3)
			);

			//	[]
			VerifyReferenceEncoding(
				"85",
				JsonArray.Empty
			);

			//	[[]]
			VerifyReferenceEncoding(
				"83 85 84",
				JsonArray.Create(JsonArray.Empty)
			);

		}

		[Test]
		public void Test_Encode_Objects()
		{
			// { }
			VerifyRoundtrip(JsonObject.Empty);

			// { "hello": "world" }
			VerifyRoundtrip(JsonObject.Create("hello", "world"));

			// complex object
			VerifyRoundtrip(JsonObject.FromObject(new
			{
				Hello = "World",
				Narf = "Zort",
				Level = 9001,
				Yes = true,
				No = false,
				Void = default(string),
				Ricky = new[] { "Un", "Dos", "Tres" },
				Foo = new { Bar = new { Baz = 123 } }
			}));

			// objet avec beaucoup de fields (hashed!)
			VerifyRoundtrip(JsonObject.FromObject(new
			{
				One = 1,
				Two = 2,
				Three = 3,
				Four = 4,
				Five = 5,
				Six = 6,
				Seven = 7,
				Eight = 8,
				Nine = 9,
				Ten = 10,
			}));

		}

		[Test]
		public void Test_Decode_Reference_Objects()
		{
			//	{ "Hello": "World", "Narf": "Zort" }
			VerifyReferenceEncoding(
				"86 A5 48 65 6C 6C 6F A5 57 6F 72 6C 64 A4 4E 61 72 66 A4 5A 6F 72 74 87",
				new JsonObject { ["Hello"] = "World", ["Narf"]  = "Zort" }
			);

			//	[]
			VerifyReferenceEncoding(
				"88",
				JsonObject.Empty
			);

		}

		[Test, Category("Benchmark")]
		public void Test_Generate_Large_DataSet()
		{
			var db = FooDb.CreateTestData();
			var obj = JsonValue.FromValue(db);

			var json = obj.ToJsonBytes(CrystalJsonSettings.JsonCompact).AsSlice();
			Log("JSON    : {0:N0} bytes", json.Count);
			Log("JSON    : {0:N0} bytes, compressed with Zstd (1)", json.ZstdCompress(1).Count);
			Log("JSON    : {0:N0} bytes, compressed with Zstd (3)", json.ZstdCompress(3).Count);
			Log("JSON    : {0:N0} bytes, compressed with Zstd (9)", json.ZstdCompress(9).Count);
			Log("JSON    : {0:N0} bytes, compressed with Zstd (20)", json.ZstdCompress(20).Count);
			Log("JSON    : {0:N0} bytes, compressed with Deflate (1)", json.DeflateCompress(CompressionLevel.Fastest).Count);
			Log("JSON    : {0:N0} bytes, compressed with Deflate (5)", json.DeflateCompress(CompressionLevel.Optimal).Count);
			Log("JSON    : {0:N0} bytes, compressed with Deflate (9)", json.DeflateCompress(CompressionLevel.SmallestSize).Count);

			// JsonPack raw
			Slice bytes = JsonPack.Encode(obj);
			Log("JsonPack: {0:N0} bytes", bytes.Count);

			// JsonPack compressed (lz4)
			Log("JsonPack: {0:N0} bytes, compressed with Zstd (1)", bytes.ZstdCompress(1).Count);
			Log("JsonPack: {0:N0} bytes, compressed with Zstd (3)", bytes.ZstdCompress(3).Count);
			Log("JsonPack: {0:N0} bytes, compressed with Zstd (9)", bytes.ZstdCompress(9).Count);
			Log("JsonPack: {0:N0} bytes, compressed with Zstd (22)", bytes.ZstdCompress(22).Count);
			Log("JsonPack: {0:N0} bytes, compressed with Deflate (1)", bytes.DeflateCompress(CompressionLevel.Fastest).Count);
			Log("JsonPack: {0:N0} bytes, compressed with Deflate (5)", bytes.DeflateCompress(CompressionLevel.Optimal).Count);
			Log("JsonPack: {0:N0} bytes, compressed with Deflate (9)", bytes.DeflateCompress(CompressionLevel.SmallestSize).Count);
		}

		public sealed class FooDb
		{
			public int Version { get; set; }
			public List<Vendor> Vendors { get; set; }

			public enum DeviceType
			{
				Unknown,
				Printer,
				Mfp,
				Plotter
			}

			public sealed class Vendor
			{
				public Guid Id { get; set; }
				public string Label { get; set; }
				public string Name { get; set; }

				public Dictionary<Guid, Model> Models { get; set; }

			}

			public sealed class Model
			{
				public Guid Id { get; set; }

				public string Name { get; set; }

				public DeviceType Type { get; set; }

				public bool ColorCapable { get; set; }

				public bool DuplexCapable { get; set; }

				public DateTime Updated { get; set; }

				public List<Rule> Rules { get; set; }

				public static Model MakeRandom(Random rnd)
				{
					return new Model
					{
						Id = Guid.NewGuid(),
						Name = String.Join(" ", Enumerable.Range(0, rnd.Next(1, 5)).Select(_ => new string('M', rnd.Next(4, 32)))),
						Type = (DeviceType) rnd.Next((int) DeviceType.Printer, 1 + (int) DeviceType.Plotter),
						ColorCapable = rnd.Next(2) == 1,
						DuplexCapable = rnd.Next(2) == 1,
						Updated = DateTime.Now.Subtract(TimeSpan.FromMilliseconds(rnd.Next())),
						Rules = Enumerable.Range(0, rnd.Next(1, 10)).Select(_ => Rule.MakeRandom(rnd)).ToList()
					};
				}
			}

			public sealed class Rule
			{
				public Guid Id { get; set; }

				public List<Guid> Parents { get; set; }

				public int Level { get; set; }

				public string Expression { get; set; }

				public static Rule MakeRandom(Random rnd)
				{
					return new Rule
					{
						Id = Guid.NewGuid(),
						Parents = Enumerable.Range(0, rnd.Next(3)).Select(_ => Guid.NewGuid()).ToList(),
						Level = rnd.Next(100),
						Expression = String.Join(" ", Enumerable.Range(0, rnd.Next(1, 4)).Select(_ => new string('X', rnd.Next(4, 32))))
					};
				}
			}

			public static FooDb CreateTestData()
			{
				var rnd = new Random(1234);

				var fooDb = new FooDb()
				{
					Version = 1,
					Vendors = new List<Vendor>()
				};

				fooDb.Vendors.Add(new Vendor
				{
					Id = Guid.NewGuid(),
					Label = "hp",
					Name = "Hewlett-Packard",
					Models = Enumerable.Range(0, 500).Select(_ => Model.MakeRandom(rnd)).ToDictionary(x => x.Id)
				});

				fooDb.Vendors.Add(new Vendor
				{
					Id = Guid.NewGuid(),
					Label = "xerox",
					Name = "Xerox",
					Models = Enumerable.Range(0, 500).Select(_ => Model.MakeRandom(rnd)).ToDictionary(x => x.Id)
				});

				fooDb.Vendors.Add(new Vendor
				{
					Id = Guid.NewGuid(),
					Label = "lex",
					Name = "Lexmark",
					Models = Enumerable.Range(0, 500).Select(_ => Model.MakeRandom(rnd)).ToDictionary(x => x.Id)
				});


				fooDb.Vendors.Add(new Vendor
				{
					Id = Guid.NewGuid(),
					Label = "km",
					Name = "Konica Minolta",
					Models = Enumerable.Range(0, 500).Select(_ => Model.MakeRandom(rnd)).ToDictionary(x => x.Id)
				});


				fooDb.Vendors.Add(new Vendor
				{
					Id = Guid.NewGuid(),
					Label = "kyo",
					Name = "Kyocera",
					Models = Enumerable.Range(0, 1000).Select(_ => Model.MakeRandom(rnd)).ToDictionary(x => x.Id)
				});

				fooDb.Vendors.Add(new Vendor
				{
					Id = Guid.NewGuid(),
					Label = "samsung",
					Name = "Samsung",
					Models = Enumerable.Range(0, 500).Select(_ => Model.MakeRandom(rnd)).ToDictionary(x => x.Id)
				});

				fooDb.Vendors.Add(new Vendor
				{
					Id = Guid.NewGuid(),
					Label = "tosh",
					Name = "Toshiba",
					Models = Enumerable.Range(0, 500).Select(_ => Model.MakeRandom(rnd)).ToDictionary(x => x.Id)
				});

				return fooDb;
			}

		}
	}

}
