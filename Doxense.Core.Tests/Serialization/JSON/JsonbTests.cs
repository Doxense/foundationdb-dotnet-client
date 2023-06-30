#region Copyright (c) 2005-2023 Doxense SAS
// All rights reserved.
// 
// Redistribution and use in source and binary forms, with or without
// modification, are permitted provided that the following conditions are met:
// 	* Redistributions of source code must retain the above copyright
// 	  notice, this list of conditions and the following disclaimer.
// 	* Redistributions in binary form must reproduce the above copyright
// 	  notice, this list of conditions and the following disclaimer in the
// 	  documentation and/or other materials provided with the distribution.
// 	* Neither the name of Doxense nor the
// 	  names of its contributors may be used to endorse or promote products
// 	  derived from this software without specific prior written permission.
// 
// THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND
// ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
// WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
// DISCLAIMED. IN NO EVENT SHALL DOXENSE BE LIABLE FOR ANY
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
	public class JsonbTest : DoxenseTest
	{

		private void VerifyRoundtrip(JsonValue value)
		{
			Log("-----------------");

			DumpCompact("ORIGINAL", value);
			var bytes = Jsonb.EncodeBuffer(value);
			//Log("RAW: ({0}) {1}", bytes.Length, bytes.AsSlice().ToString("X"));
			DumpHexa(bytes);

			var decoded = Jsonb.Decode(bytes);
			DumpCompact("DECODED", decoded);
			Assert.That(decoded, Is.EqualTo(value), "Decoded JSON value does not match original");

			var compressed = bytes.ZstdCompress(0);
			if (compressed.Count < bytes.Count)
			{
				Log("Zstd: {0} bytes (1 : {1:N2})", compressed.Count, bytes.Count * 1.0 / compressed.Count);
#if FULL_DEBUG
				DumpHexa(compressed);
#endif
			}
			else
			{
				Log("Zstd: no gain");
			}
			Log("done");
			// mini-bench (uniquement en mode RELEASE)
#if !DEBUG
			// WARMUP
			var jbytes = value.ToJsonBytes(CrystalJsonSettings.JsonCompact).AsSlice();
			_ = CrystalJson.Parse(jbytes);
#if FULL_DEBUG
			DumpVersus(bytes, jbytes);
#endif

			const int N = 1000;
			var sw = System.Diagnostics.Stopwatch.StartNew();
			for (int i = 0; i < N; i++)
			{
				_ = Jsonb.EncodeBuffer(value);
			}
			sw.Stop();
			Log("Bench: jsonb encode {0:N1} nanos", sw.Elapsed.TotalMilliseconds * 1000000 / N);
			sw = System.Diagnostics.Stopwatch.StartNew();
			for (int i = 0; i < N; i++)
			{
				_ = Jsonb.Decode(bytes);
			}
			sw.Stop();
			Log("Bench: jsonb decode {0:N1} nanos", sw.Elapsed.TotalMilliseconds * 1000000 / N);

			sw = System.Diagnostics.Stopwatch.StartNew();
			for (int i = 0; i < N; i++)
			{
				_ = value.ToJsonBytes(CrystalJsonSettings.JsonCompact);
			}
			sw.Stop();
			Log("Bench: JSON  encode {0:N1} nanos", sw.Elapsed.TotalMilliseconds * 1000000 / N);
			sw = System.Diagnostics.Stopwatch.StartNew();
			for (int i = 0; i < N; i++)
			{
				_ = CrystalJson.Parse(jbytes);
			}
			sw.Stop();
			Log("Bench: JSON  decode {0:N1} nanos", sw.Elapsed.TotalMilliseconds * 1000000 / N);
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
			var decoded = Jsonb.Decode(bytes);
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
		}

		[Test]
		public void Test_Decode_Reference_Scalars()
		{
			// sanitity test: on vérifie qu'on est capable de décoder des versions binaires de référence
			// note: si le format binaire change, il ne faut pas oublier de mettre à jour les encodages de référence!

			#region strings

			VerifyReferenceEncoding(
				"11 00 00 00 01 00 00 50 05 00 00 80 68 65 6C 6C 6F",
				"hello"
			);

			// ""
			VerifyReferenceEncoding(
				"0C 00 00 00 01 00 00 50 00 00 00 80",
				""
			);

			VerifyReferenceEncoding(
				"1B 00 00 00 01 00 00 50 0F 00 00 80 E3 81 97 E3 81 AE E3 81 B6 E4 B8 80 E7 95 AA",
				"しのぶ一番"
			);

			#endregion

			#region booleans

			// false
			VerifyReferenceEncoding(
				"0C 00 00 00 01 00 00 50 00 00 00 A0",
				false
			);

			// true
			VerifyReferenceEncoding(
				"0C 00 00 00 01 00 00 50 00 00 00 B0",
				true
			);

			#endregion

			#region null

			// null
			VerifyReferenceEncoding(
				"0C 00 00 00 01 00 00 50 00 00 00 C0",
				JsonNull.Null
			);

			#endregion

			#region integers

			// 0 (integer)
			VerifyReferenceEncoding(
				"0D 00 00 00 01 00 00 50 01 00 00 90 00",
				0
			);

			// -1 (integer)
			VerifyReferenceEncoding(
				"0D 00 00 00 01 00 00 50 01 00 00 90 FF",
				-1
			);

			// 1234 (integer)
			VerifyReferenceEncoding(
				"0E 00 00 00 01 00 00 50 02 00 00 90 D2 04",
				1234
			);

			// zéro (literal)
			VerifyReferenceEncoding(
				"0D 00 00 00 01 00 00 50 01 00 00 E0 30",
				0
			);

			#endregion

			#region literal numbers (text)

			// -1 (literal)
			VerifyReferenceEncoding(
				"0E 00 00 00 01 00 00 50 02 00 00 E0 2D 31",
				-1
			);

			// 1234 (literal)
			VerifyReferenceEncoding(
				"10 00 00 00 01 00 00 50 04 00 00 E0 31 32 33 34",
				1234
			);

			// 123.4 (literal)
			VerifyReferenceEncoding(
				"11 00 00 00 01 00 00 50 05 00 00 E0 31 32 33 2E 34",
				123.4
			);

			// Math.PI (literal)
			VerifyReferenceEncoding(
				"1E 00 00 00 01 00 00 50 12 00 00 E0 33 2E 31 34 31 35 39 32 36 35 33 35 38 39 37 39 33 31",
				Math.PI
			);

			// double.NaN (literal)
			VerifyReferenceEncoding(
				"0F 00 00 00 01 00 00 50 03 00 00 E0 4E 61 4E",
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
			VerifyRoundtrip(Enumerable.Range(0, 100).Select(x => rnd.Next()).ToJsonArray());

			// random guids
			VerifyRoundtrip(Enumerable.Range(0, 10).Select(x => Guid.NewGuid()).ToJsonArray());
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
				"1A 00 00 00 02 00 00 40 05 00 00 00 05 00 00 00 68 65 6C 6C 6F 77 6F 72 6C 64",
				JsonArray.Create("hello", "world")
			);

			//	[]
			VerifyReferenceEncoding(
				"08 00 00 00 00 00 00 40",
				JsonArray.Empty
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

		[Test, Category("Benchmark")]
		public void Test_Generate_Large_DataSet()
		{
			var db = FooDb.CreateTestData();
			var obj = JsonValue.FromValue(db);

			var json = obj.ToJsonBytes(CrystalJsonSettings.JsonCompact);
			Log("JSON : {0:N0} bytes", json.Length);
			Log("JSON : {0:N0} bytes, compressed with Zstd-2", json.AsSlice().ZstdCompress(2).Count);
			Log("JSON : {0:N0} bytes, compressed with Zstd-9", json.AsSlice().ZstdCompress(9).Count);
			Log("JSON : {0:N0} bytes, compressed with Zstd-20", json.AsSlice().ZstdCompress(20).Count);
			Log("JSON : {0:N0} bytes, compressed with Deflate (5)", json.AsSlice().DeflateCompress(CompressionLevel.Optimal).Count);
			Log("JSON : {0:N0} bytes, compressed with Deflate (9)", json.AsSlice().DeflateCompress(CompressionLevel.SmallestSize).Count);

			// jsonb raw
			var jsonb = Jsonb.Encode(obj);
			Log("JSONB: {0:N0} bytes", jsonb.Length);

			// jsonb compressed
			Log("JSONB: {0:N0} bytes, compressed with Zstd-2", jsonb.AsSlice().ZstdCompress(2).Count);
			Log("JSONB: {0:N0} bytes, compressed with Zstd-9", jsonb.AsSlice().ZstdCompress(9).Count);
			Log("JSONB: {0:N0} bytes, compressed with Zstd-20", jsonb.AsSlice().ZstdCompress(20).Count);
			Log("JSONB: {0:N0} bytes, compressed with Deflate (5)", jsonb.AsSlice().DeflateCompress(CompressionLevel.Optimal).Count);
			Log("JSONB: {0:N0} bytes, compressed with Deflate (9)", jsonb.AsSlice().DeflateCompress(CompressionLevel.SmallestSize).Count);
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
