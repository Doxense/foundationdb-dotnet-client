#region Copyright (c) 2023-2025 SnowBank SAS, (c) 2005-2023 Doxense SAS
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

// ReSharper disable StringLiteralTypo

#pragma warning disable JSON001 // JSON issue: 'x' unexpected

namespace SnowBank.Data.Json.Tests
{

	public partial class CrystalJsonTest
	{

		[Test]
		public void Test_Parse_Null()
		{
			Assert.That(JsonValue.Parse("null"), Is.EqualTo(JsonNull.Null));
			Assert.That(JsonValue.Parse(string.Empty), Is.EqualTo(JsonNull.Missing));
			Assert.That(JsonValue.Parse(default(string)), Is.EqualTo(JsonNull.Missing));

			Assert.That(JsonValue.Parse(Array.Empty<byte>()), Is.EqualTo(JsonNull.Missing));
			Assert.That(JsonValue.Parse(default(byte[])), Is.EqualTo(JsonNull.Missing));
			Assert.That(JsonValue.Parse((new byte[10]).AsSpan(5, 0)), Is.EqualTo(JsonNull.Missing));

			Assert.That(JsonValue.Parse(Slice.Empty), Is.EqualTo(JsonNull.Missing));
			Assert.That(JsonValue.Parse(Slice.Nil), Is.EqualTo(JsonNull.Missing));

			Assert.That(JsonValue.Parse(ReadOnlySpan<byte>.Empty), Is.EqualTo(JsonNull.Missing));
		}

		[Test]
		public void Test_Parse_Boolean()
		{
			Assert.That(JsonValue.Parse("true"), Is.EqualTo(JsonBoolean.True));
			Assert.That(JsonValue.Parse("false"), Is.EqualTo(JsonBoolean.False));

			// we need to whole token
			Assert.That(() => JsonValue.Parse("tru"), Throws.InstanceOf<JsonSyntaxException>());
			Assert.That(() => JsonValue.Parse("flse"), Throws.InstanceOf<JsonSyntaxException>());

			// but without any extra characters
			Assert.That(() => JsonValue.Parse("truee"), Throws.InstanceOf<JsonSyntaxException>());
			Assert.That(() => JsonValue.Parse("falsee"), Throws.InstanceOf<JsonSyntaxException>());

			// it is case senstitive
			Assert.That(() => JsonValue.Parse("True"), Throws.InstanceOf<JsonSyntaxException>());
			Assert.That(() => JsonValue.Parse("Frue"), Throws.InstanceOf<JsonSyntaxException>());

			// we do not allow variations (yes/no, on/off, ...)
			Assert.That(() => JsonValue.Parse("yes"), Throws.InstanceOf<JsonSyntaxException>());
			Assert.That(() => JsonValue.Parse("off"), Throws.InstanceOf<JsonSyntaxException>());
		}

		[Test]
		public void Test_Parse_String()
		{
			// Parsing

			Assert.That(JsonValue.Parse(@""""""), Is.EqualTo(JsonString.Empty));
			Assert.That(JsonValue.Parse(@"""Hello World"""), Is.EqualTo(JsonString.Return("Hello World")));
			Assert.That(JsonValue.Parse(@"""0123456789ABCDE"""), Is.EqualTo(JsonString.Return("0123456789ABCDE")));
			Assert.That(JsonValue.Parse(@"""0123456789ABCDEF"""), Is.EqualTo(JsonString.Return("0123456789ABCDEF")));
			Assert.That(JsonValue.Parse(@"""Very long string of text that is larger than 16 characters"""), Is.EqualTo(JsonString.Return("Very long string of text that is larger than 16 characters")));
			Assert.That(JsonValue.Parse(@"""Foo\""Bar"""), Is.EqualTo(JsonString.Return("Foo\"Bar")));
			Assert.That(JsonValue.Parse(@"""\"""""), Is.EqualTo(JsonString.Return("\"")));
			Assert.That(JsonValue.Parse(@"""\\"""), Is.EqualTo(JsonString.Return("\\")));
			Assert.That(JsonValue.Parse(@"""\/"""), Is.EqualTo(JsonString.Return("/")));
			Assert.That(JsonValue.Parse(@"""\b"""), Is.EqualTo(JsonString.Return("\b")));
			Assert.That(JsonValue.Parse(@"""\f"""), Is.EqualTo(JsonString.Return("\f")));
			Assert.That(JsonValue.Parse(@"""\n"""), Is.EqualTo(JsonString.Return("\n")));
			Assert.That(JsonValue.Parse(@"""\r"""), Is.EqualTo(JsonString.Return("\r")));
			Assert.That(JsonValue.Parse(@"""\t"""), Is.EqualTo(JsonString.Return("\t")));

			// Errors
			Assert.That(() => JsonValue.Parse("\"incomplete"), Throws.InstanceOf<JsonSyntaxException>(), "Incomplete string should fail");
			Assert.That(() => JsonValue.Parse("invalid\""), Throws.InstanceOf<JsonSyntaxException>(), "Invalid string should fail");
			Assert.That(() => JsonValue.Parse("\"\\z\""), Throws.InstanceOf<JsonSyntaxException>(), "Invalid \\z character should fail");
			Assert.That(() => JsonValue.Parse("\"\\\""), Throws.InstanceOf<JsonSyntaxException>(), "Incomplete \\ character should fail");
		}

		[Test]
		public void Test_Parse_Number()
		{
			// Parsing

			// integers
			Assert.That(JsonValue.Parse("0"), Is.EqualTo(JsonNumber.Return(0)), "Parse('0')");
			Assert.That(JsonValue.Parse("1"), Is.EqualTo(JsonNumber.Return(1)), "Parse('1')");
			Assert.That(JsonValue.Parse("123"), Is.EqualTo(JsonNumber.Return(123)), "Parse('123')");
			Assert.That(JsonValue.Parse("-1"), Is.EqualTo(JsonNumber.Return(-1)), "Parse('-1')");
			Assert.That(JsonValue.Parse("-123"), Is.EqualTo(JsonNumber.Return(-123)), "Parse('-123')");

			// decimals
			Assert.That(JsonValue.Parse("0.1"), Is.EqualTo(JsonNumber.Return(0.1)));
			Assert.That(JsonValue.Parse("1.23"), Is.EqualTo(JsonNumber.Return(1.23)));
			Assert.That(JsonValue.Parse("-0.1"), Is.EqualTo(JsonNumber.Return(-0.1)));
			Assert.That(JsonValue.Parse("-1.23"), Is.EqualTo(JsonNumber.Return(-1.23)));

			// decimals (but only integers)
			Assert.That(JsonValue.Parse("0"), Is.EqualTo(JsonNumber.Return(0)));
			Assert.That(JsonValue.Parse("1"), Is.EqualTo(JsonNumber.Return(1)));
			Assert.That(JsonValue.Parse("123"), Is.EqualTo(JsonNumber.Return(123)));
			Assert.That(JsonValue.Parse("-1"), Is.EqualTo(JsonNumber.Return(-1)));
			Assert.That(JsonValue.Parse("-123"), Is.EqualTo(JsonNumber.Return(-123)));

			// avec exponent
			Assert.That(JsonValue.Parse("1E1"), Is.EqualTo(JsonNumber.Return(10)));
			Assert.That(JsonValue.Parse("1E2"), Is.EqualTo(JsonNumber.Return(100)));
			Assert.That(JsonValue.Parse("1.23E2"), Is.EqualTo(JsonNumber.Return(123)));
			Assert.That(JsonValue.Parse("1E+1"), Is.EqualTo(JsonNumber.Return(10)));
			Assert.That(JsonValue.Parse("1E-1"), Is.EqualTo(JsonNumber.Return(0.1)));
			Assert.That(JsonValue.Parse("1E-2"), Is.EqualTo(JsonNumber.Return(0.01)));

			// négatif avec exponent
			Assert.That(JsonValue.Parse("-1E1"), Is.EqualTo(JsonNumber.Return(-10)));
			Assert.That(JsonValue.Parse("-1E2"), Is.EqualTo(JsonNumber.Return(-100)));
			Assert.That(JsonValue.Parse("-1.23E2"), Is.EqualTo(JsonNumber.Return(-123)));
			Assert.That(JsonValue.Parse("-1E1"), Is.EqualTo(JsonNumber.Return(-10)));
			Assert.That(JsonValue.Parse("-1E-1"), Is.EqualTo(JsonNumber.Return(-0.1)));
			Assert.That(JsonValue.Parse("-1E-2"), Is.EqualTo(JsonNumber.Return(-0.01)));

			// Special
			Assert.That(JsonValue.Parse("4.94065645841247E-324"), Is.EqualTo(JsonNumber.Return(double.Epsilon)));
			Assert.That(JsonValue.Parse("NaN"), Is.EqualTo(JsonNumber.Return(double.NaN)));
			Assert.That(JsonValue.Parse("Infinity"), Is.EqualTo(JsonNumber.Return(double.PositiveInfinity)));
			Assert.That(JsonValue.Parse("+Infinity"), Is.EqualTo(JsonNumber.Return(double.PositiveInfinity)));
			Assert.That(JsonValue.Parse("-Infinity"), Is.EqualTo(JsonNumber.Return(double.NegativeInfinity)));

			// Errors
			Assert.That(() => JsonValue.Parse("1Z"), Throws.InstanceOf<JsonSyntaxException>());
			Assert.That(() => JsonValue.Parse("1."), Throws.InstanceOf<JsonSyntaxException>());
			Assert.That(() => JsonValue.Parse("1-"), Throws.InstanceOf<JsonSyntaxException>());
			Assert.That(() => JsonValue.Parse("1+"), Throws.InstanceOf<JsonSyntaxException>());
			Assert.That(() => JsonValue.Parse("1E"), Throws.InstanceOf<JsonSyntaxException>());
			Assert.That(() => JsonValue.Parse("1E+"), Throws.InstanceOf<JsonSyntaxException>());
			Assert.That(() => JsonValue.Parse("1E-"), Throws.InstanceOf<JsonSyntaxException>());
			Assert.That(() => JsonValue.Parse("1.2.3"), Throws.InstanceOf<JsonSyntaxException>(), "Duplicate decimal point should fail");
			Assert.That(() => JsonValue.Parse("1E1E1"), Throws.InstanceOf<JsonSyntaxException>(), "Duplicate exponent should fail");

			// mixed types
			var x = JsonNumber.Return(123);
			Assert.That(x, Is.EqualTo(JsonNumber.Return(123)));
			Assert.That(x, Is.EqualTo(JsonNumber.Return(123L)));
			Assert.That(x, Is.EqualTo(JsonNumber.Return(123d)));
			Assert.That(x, Is.EqualTo(JsonNumber.Return(123f)));
			Assert.That(x, Is.EqualTo(JsonNumber.Return(123m)));

			Assert.That(x, Is.Not.EqualTo(JsonNumber.Return(124)));
			Assert.That(x, Is.Not.EqualTo(JsonNumber.Return(124L)));
			Assert.That(x, Is.Not.EqualTo(JsonNumber.Return(124d)));
			Assert.That(x, Is.Not.EqualTo(JsonNumber.Return(124f)));
			Assert.That(x, Is.Not.EqualTo(JsonNumber.Return(124m)));

			// parsing and interning corner cases

			{ // in all cases, small numbers should intern the literal
				var num1 = (JsonNumber) JsonValue.Parse("42");
				Assert.That(num1.ToInt32(), Is.EqualTo(42));
				Assert.That(num1.Literal, Is.EqualTo("42"));
				var num2 = (JsonNumber) JsonValue.Parse("42");
				Assert.That(num2, Is.SameAs(num1), "Small positive interger 42 should be interened by default");
			}
			{ // 255 should also be cached
				var num1 = (JsonNumber) JsonValue.Parse("255");
				Assert.That(num1.ToInt32(), Is.EqualTo(255));
				Assert.That(num1.Literal, Is.EqualTo("255"));
				var num2 = (JsonNumber) JsonValue.Parse("255");
				Assert.That(num2, Is.SameAs(num1), "Positive interger 255 should be interened by default");
			}

			{ // -128 should also be cached
				var num1 = (JsonNumber) JsonValue.Parse("-128");
				Assert.That(num1.ToInt32(), Is.EqualTo(-128));
				Assert.That(num1.Literal, Is.EqualTo("-128"));
				var num2 = (JsonNumber) JsonValue.Parse("-128");
				Assert.That(num2, Is.SameAs(num1), "Negative interger -128 should be interened by default");
			}

			{ // large number should not be interned
				var num1 = (JsonNumber) JsonValue.Parse("1000");
				Assert.That(num1.ToInt32(), Is.EqualTo(1000));
				Assert.That(num1.Literal, Is.EqualTo("1000"));
				var num2 = (JsonNumber) JsonValue.Parse("1000");
				Assert.That(num2.ToInt32(), Is.EqualTo(1000));
				Assert.That(num2.Literal, Is.EqualTo("1000"));
				Assert.That(num2.Literal, Is.Not.SameAs(num1.Literal), "Large integers should not be interned by default");
			}

			{ // literal should be same as parsed
				var num1 = (JsonNumber) JsonValue.Parse("1E3");
				Assert.That(num1.ToInt32(), Is.EqualTo(1000));
				Assert.That(num1.Literal, Is.EqualTo("1E3"));
				Assert.That(num1.IsDecimal, Is.False);
				var num2 = (JsonNumber) JsonValue.Parse("10E2");
				Assert.That(num2.ToInt32(), Is.EqualTo(1000));
				Assert.That(num2.Literal, Is.EqualTo("10E2"));
				Assert.That(num1.IsDecimal, Is.False);
			}

			{ // false decimal
				var num1 = (JsonNumber) JsonValue.Parse("0.1234E4");
				Assert.That(num1.ToDouble(), Is.EqualTo(1234.0));
				Assert.That(num1.Literal, Is.EqualTo("0.1234E4"));
				Assert.That(num1.IsDecimal, Is.False);
				var num2 = JsonNumber.Create(0.1234E4);
				Assert.That(num2.ToDouble(), Is.EqualTo(1234.0));
				Assert.That(num2.Literal, Is.EqualTo("1234")); //BUGBUG: devrait être "1234.0" ?
				//Assert.That(num2.IsDecimal, Is.False); //REVIEW: vu qu'on a appelé Return(double), le json est actuellement considéré comme décimal..
			}

			{ // real decimal
				var num1 = (JsonNumber) JsonValue.Parse("0.1234E3");
				Assert.That(num1.ToDouble(), Is.EqualTo(123.4));
				Assert.That(num1.Literal, Is.EqualTo("0.1234E3"));
				Assert.That(num1.IsDecimal, Is.True);
				var num2 = JsonNumber.Create(0.1234E3);
				Assert.That(num2.ToDouble(), Is.EqualTo(123.4));
				Assert.That(num2.Literal, Is.EqualTo("123.4"));
				Assert.That(num2.IsDecimal, Is.True);
			}

			{ // very long integers should bypass the custom parsing
				var num1 = (JsonNumber) JsonValue.Parse("18446744073709551615"); // ulong.MaxValue
				Assert.That(num1.ToUInt64(), Is.EqualTo(ulong.MaxValue));
				Assert.That(num1.Literal, Is.EqualTo("18446744073709551615"));
				Assert.That(num1.IsDecimal, Is.False);
			}

		}

		[Test]
		public void Test_Parse_Comment()
		{
			var obj = JsonObject.Parse("{ // hello world\r\n}");
			Log(obj);
			Assert.That(obj, Is.Not.Null.And.InstanceOf<JsonObject>());
			Assert.That(obj, Has.Count.EqualTo(0));

			obj = JsonObject.Parse(
				"""
				{
					// comment 1
					"foo": 123,
					//"bar": 456
					// comment2
				}
				""");
			Log(obj);
			Assert.That(obj, Is.Not.Null.And.InstanceOf<JsonObject>());
			Assert.That(obj["foo"], Is.EqualTo((JsonValue)123));
			Assert.That(obj["bar"], Is.EqualTo(JsonNull.Missing));
			Assert.That(obj, Has.Count.EqualTo(1));
		}

#if DISABLED
		[Test]
		public void TestParseDateTime()
		{
			// Parsing

			// Unix Epoch (1970-1-1 UTC)
			ParseAreEqual(new JsonDateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc), "\"\\/Date(0)\\/\"");

			// Min/Max Value
			ParseAreEqual(JsonDateTime.MinValue, "\"\\/Date(-62135596800000)\\/\"", "DateTime.MinValue");
			ParseAreEqual(JsonDateTime.MaxValue, "\"\\/Date(253402300799999)\\/\"", "DateTime.MaxValue (auto-ajusted)"); // note: doit ajouter automatiquement les .99999 millisecondes manquantes !

			// 2000-01-01 (heure d'hivers)
			ParseAreEqual(new JsonDateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc), "\"\\/Date(946684800000)\\/\"", "2000-01-01 UTC");
			ParseAreEqual(new JsonDateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Local), "\"\\/Date(946681200000+0100)\\/\"", "2000-01-01 GMT+1 (Paris)");

			// 2000-09-01 (heure d'été)
			ParseAreEqual(new JsonDateTime(2000, 9, 1, 0, 0, 0, DateTimeKind.Utc), "\"\\/Date(967766400000)\\/\"", "2000-09-01 UTC");
			ParseAreEqual(new JsonDateTime(2000, 9, 1, 0, 0, 0, DateTimeKind.Local), "\"\\/Date(967759200000+0200)\\/\"", "2000-09-01 GMT+2 (Paris, DST)");

			// RoundTrip !
			DateTime utcNow = DateTime.UtcNow;
			Assert.That(utcNow.Kind, Is.EqualTo(DateTimeKind.Utc));
			// /!\ JSON a une résolution a la milliseconde mais UtcNow a une précision au 'tick', donc il faut tronquer la date car elle a une précision supérieure
			var utcRoundTrip = JsonValue._Parse(CrystalJson.Serialize(utcNow));
			Assert.That(utcRoundTrip, Is.EqualTo(new JsonDateTime(utcNow)), "RoundTrip DateTime.UtcNow");

			DateTime localNow = DateTime.Now;
			Assert.That(localNow.Kind, Is.EqualTo(DateTimeKind.Local));
			var localRoundTrip = JsonValue._Parse(CrystalJson.Serialize(localNow));
			Assert.That(localRoundTrip, Is.EqualTo(new JsonDateTime(localNow)), "RoundTrip DateTime.Now");
		}
#endif

		[Test]
		public void Test_Parse_Array()
		{
			// Parsing

			// empty
			string jsonText = "[]";
			var obj = JsonValue.Parse(jsonText);
			Assert.That(obj, Is.Not.Null, jsonText);
			Assert.That(obj, Is.InstanceOf<JsonArray>(), jsonText);
			var res = (JsonArray) obj;
			Assert.That(res, Has.Count.EqualTo(0), jsonText + ".Count");

			jsonText = "[ ]";
			obj = JsonValue.Parse(jsonText);
			Assert.That(obj, Is.InstanceOf<JsonArray>(), jsonText);
			res = (JsonArray) obj;
			Assert.That(res, Has.Count.EqualTo(0), jsonText + ".Count");

			// single value
			ParseAreEqual(JsonArray.Create(1), "[1]");
			ParseAreEqual(JsonArray.Create(1), "[ 1 ]");

			// multiple value
			ParseAreEqual(JsonArray.Create(1, 2, 3), "[1,2,3]");
			ParseAreEqual(JsonArray.Create(1, 2, 3), "[ 1, 2, 3 ]");

			// strings
			ParseAreEqual(JsonArray.Create("foo", "bar"), @"[""foo"",""bar""]");

			// mixed array
			ParseAreEqual(JsonArray.Create(123, true, "foo"), @"[123,true,""foo""]");

			// jagged arrays
			ParseAreEqual(new JsonArray {
				JsonArray.Create(1, 2, 3),
				JsonArray.Create(true, false),
				JsonArray.Create("foo", "bar")
			}, @"[ [1,2,3], [true,false], [""foo"",""bar""] ]");

			// incomplete
			Assert.That(() => JsonValue.Parse("[1,2,3"), Throws.InstanceOf<JsonSyntaxException>(), "Incomplete Array should fail");
			Assert.That(() => JsonValue.Parse("[1,,3]"), Throws.InstanceOf<JsonSyntaxException>(), "Array with missing item should fail");
			Assert.That(() => JsonValue.Parse("[,]"), Throws.InstanceOf<JsonSyntaxException>(), "Array with empty items should fail");
			Assert.That(() => JsonValue.Parse("[1,[A,B,C]"), Throws.InstanceOf<JsonSyntaxException>(), "Incomplete inner Array should fail");

			// trailing commas
			Assert.That(() => JsonValue.Parse("[ 1, 2, 3, ]"), Throws.Nothing, "By default, trailing commas are allowed");
			Assert.That(() => JsonValue.Parse("[ 1, 2, 3, ]", CrystalJsonSettings.Json), Throws.Nothing, "By default, trailing commas are allowed");
			Assert.That(() => JsonValue.Parse("[ 1, 2, 3, ]", CrystalJsonSettings.JsonStrict), Throws.InstanceOf<JsonSyntaxException>(), "Should fail is trailing commas are forbidden");
			Assert.That(() => JsonValue.Parse("[ 1, 2, 3, ]", CrystalJsonSettings.Json.WithoutTrailingCommas()), Throws.InstanceOf<JsonSyntaxException>(), "Should fail when trailing commas are explicitly forbidden");
			Assert.That(JsonArray.Parse("[ 1, 2, 3, ]"), Has.Count.EqualTo(3), "Ignored trailing commas should not add any extra item to the array");
			Assert.That(JsonArray.Parse("[ 1, 2, 3, ]", CrystalJsonSettings.Json), Has.Count.EqualTo(3), "Ignored trailing commas should not add any extra item to the array");

			// interning corner cases

			{ // array of small integers (-128..255) should all be refs to cached instances
				var arr = JsonArray.Parse("[ 0, 1, 42, -1, 255, -128 ]");
				Assert.That(arr, Is.Not.Null.And.Count.EqualTo(6));
				Assert.That(arr[0], Is.SameAs(JsonNumber.Zero));
				Assert.That(arr[1], Is.SameAs(JsonNumber.One));
				Assert.That(arr[2], Is.SameAs(JsonNumber.Return(42)));
				Assert.That(arr[3], Is.SameAs(JsonNumber.MinusOne));
				Assert.That(arr[4], Is.SameAs(JsonNumber.Return(255)));
				Assert.That(arr[5], Is.SameAs(JsonNumber.Return(-128)));
			}

			static void ParseAreEqual(JsonValue expected, string jsonText, string? message = null)
			{
				{ // JsonValue, string
					var parsed = JsonValue.Parse(jsonText);
					Assert.That(parsed, Is.EqualTo(expected), $"JsonValue.Parse('{jsonText}') into {expected.Type}{(message is null ? string.Empty : (": " + message))}");
					Assert.That(parsed, IsJson.Not.ReadOnly);
				}
				{ // JsonValue, RoS<char>
					var parsed = JsonValue.Parse(("$$$" + jsonText + "%%%").AsSpan()[3..^3]);
					Assert.That(parsed, Is.EqualTo(expected), $"JsonValue.Parse('{jsonText}') into {expected.Type}{(message is null ? string.Empty : (": " + message))}");
					Assert.That(parsed, IsJson.Not.ReadOnly);
				}
				{ // JsonValue, RoS<byte>
					var parsed = JsonValue.Parse(Encoding.UTF8.GetBytes("$$$" + jsonText + "%%%").AsSpan()[3..^3]);
					Assert.That(parsed, Is.EqualTo(expected), $"JsonValue.Parse('{jsonText}') into {expected.Type}{(message is null ? string.Empty : (": " + message))}");
					Assert.That(parsed, IsJson.Not.ReadOnly);
				}
				{ // JsonArray, string
					var parsed = JsonArray.Parse(jsonText);
					Assert.That(parsed, Is.EqualTo(expected), $"JsonValue.Parse('{jsonText}') into {expected.Type}{(message is null ? string.Empty : (": " + message))}");
					Assert.That(parsed, IsJson.Not.ReadOnly);
				}
				{ // JsonArray, RoS<char>
					var parsed = JsonArray.Parse(("$$$" + jsonText + "%%%").AsSpan()[3..^3]);
					Assert.That(parsed, Is.EqualTo(expected), $"JsonValue.Parse('{jsonText}') into {expected.Type}{(message is null ? string.Empty : (": " + message))}");
					Assert.That(parsed, IsJson.Not.ReadOnly);
				}
				{ // JsonArray, RoS<char>
					var parsed = JsonArray.Parse(Encoding.UTF8.GetBytes("$$$" + jsonText + "%%%").AsSpan()[3..^3]);
					Assert.That(parsed, Is.EqualTo(expected), $"JsonValue.Parse('{jsonText}') into {expected.Type}{(message is null ? string.Empty : (": " + message))}");
					Assert.That(parsed, IsJson.Not.ReadOnly);
				}

				{ // JsonValue, string, readonly
					var parsed = JsonValue.ReadOnly.Parse(jsonText);
					Assert.That(parsed, Is.EqualTo(expected), $"JsonValue.Parse('{jsonText}') into {expected.Type}{(message is null ? string.Empty : (": " + message))}");
					Assert.That(parsed, IsJson.ReadOnly);
				}
				{ // JsonValue, RoS<char>, readonly
					var parsed = JsonValue.ReadOnly.Parse(("$$$" + jsonText + "%%%").AsSpan()[3..^3]);
					Assert.That(parsed, Is.EqualTo(expected), $"JsonValue.Parse('{jsonText}') into {expected.Type}{(message is null ? string.Empty : (": " + message))}");
					Assert.That(parsed, IsJson.ReadOnly);
				}
				{ // JsonValue, RoS<byte>, readonly
					var parsed = JsonValue.ReadOnly.Parse(Encoding.UTF8.GetBytes("$$$" + jsonText + "%%%").AsSpan()[3..^3]);
					Assert.That(parsed, Is.EqualTo(expected), $"JsonValue.Parse('{jsonText}') into {expected.Type}{(message is null ? string.Empty : (": " + message))}");
					Assert.That(parsed, IsJson.ReadOnly);
				}
				{ // JsonArray, string, readonly
					var parsed = JsonArray.ReadOnly.Parse(jsonText);
					Assert.That(parsed, Is.EqualTo(expected), $"JsonValue.Parse('{jsonText}') into {expected.Type}{(message is null ? string.Empty : (": " + message))}");
					Assert.That(parsed, IsJson.ReadOnly);
				}
				{ // JsonArray, RoS<char>, readonly
					var parsed = JsonArray.ReadOnly.Parse(("$$$" + jsonText + "%%%").AsSpan()[3..^3]);
					Assert.That(parsed, Is.EqualTo(expected), $"JsonValue.Parse('{jsonText}') into {expected.Type}{(message is null ? string.Empty : (": " + message))}");
					Assert.That(parsed, IsJson.ReadOnly);
				}
				{ // JsonArray, RoS<byte>, readonly
					var parsed = JsonArray.ReadOnly.Parse(Encoding.UTF8.GetBytes("$$$" + jsonText + "%%%").AsSpan()[3..^3]);
					Assert.That(parsed, Is.EqualTo(expected), $"JsonValue.Parse('{jsonText}') into {expected.Type}{(message is null ? string.Empty : (": " + message))}");
					Assert.That(parsed, IsJson.ReadOnly);
				}
			}

		}

		[Test]
		public void Test_Parse_SimpleObject()
		{
			string jsonText = "{}";
			var obj = JsonValue.Parse(jsonText);
			Assert.That(obj, Is.Not.Null, jsonText);
			Assert.That(obj, Is.InstanceOf<JsonObject>(), jsonText);
			var parsed = obj.AsObject();
			Assert.That(parsed, Has.Count.EqualTo(0));

			jsonText = "{ }";
			parsed = JsonObject.Parse(jsonText);
			Assert.That(parsed, Is.Not.Null, jsonText);
			Assert.That(parsed, Has.Count.EqualTo(0));
			Assert.That(parsed, Is.EqualTo(JsonObject.Create()), jsonText);

			jsonText = """{ "Name":"James Bond" }""";
			obj = JsonValue.Parse(jsonText);
			Assert.That(obj, Is.InstanceOf<JsonObject>(), jsonText);
			parsed = obj.AsObject();
			Assert.That(parsed, Is.Not.Null, jsonText);
			Assert.That(parsed, Has.Count.EqualTo(1));
			Assert.That(parsed.ContainsKey("Name"), Is.True);
			Assert.That(parsed["Name"], IsJson.EqualTo("James Bond"));

			jsonText = """{ "Id":7, "Name":"James Bond", "IsDeadly":true }""";
			parsed = JsonObject.Parse(jsonText);
			Assert.That(parsed, Has.Count.EqualTo(3));
			Assert.That(parsed["Name"], IsJson.EqualTo("James Bond"));
			Assert.That(parsed["Id"], IsJson.EqualTo(7));
			Assert.That(parsed["IsDeadly"], IsJson.True);

			jsonText = """{ "Id":7, "Name":"James Bond", "IsDeadly":true, "Created":"\/Date(-52106400000+0200)\/", "Weapons":[{"Name":"Walter PPK"}] }""";
			parsed = JsonObject.Parse(jsonText);
			Assert.That(parsed, Has.Count.EqualTo(5));
			Assert.That(parsed["Name"], IsJson.EqualTo("James Bond"));
			Assert.That(parsed["Id"], IsJson.EqualTo(7));
			Assert.That(parsed["IsDeadly"], IsJson.True);
			Assert.That(parsed["Created"], IsJson.EqualTo("/Date(-52106400000+0200)/")); //BUGBUG
			Assert.That(parsed.ContainsKey("Weapons"), Is.True);
			var weapons = parsed.GetArray("Weapons");
			Assert.That(weapons, Is.Not.Null);
			Assert.That(weapons, Has.Count.EqualTo(1));
			var weapon = weapons.GetObject(0);
			Assert.That(weapon, Is.Not.Null);
			Assert.That(weapon["Name"], IsJson.EqualTo("Walter PPK"));

			// incomplete
			Assert.That(() => JsonValue.Parse("""{"foo"}"""), Throws.InstanceOf<JsonSyntaxException>(), "Missing property separator");
			Assert.That(() => JsonValue.Parse("""{"foo":}"""), Throws.InstanceOf<JsonSyntaxException>(), "Missing property value");
			Assert.That(() => JsonValue.Parse("""{"foo":123"""), Throws.InstanceOf<JsonSyntaxException>(), "Missing '}'");
			Assert.That(() => JsonValue.Parse("""{"foo":{}"""), Throws.InstanceOf<JsonSyntaxException>(), "Missing outer '}'");
			Assert.That(() => JsonValue.Parse("{,}"), Throws.InstanceOf<JsonSyntaxException>(), "Object with empty properties should fail");

			// trailing commas
			jsonText = """{ "Foo": 123, "Bar": 456, }""";
			Assert.That(() => JsonValue.Parse(jsonText), Throws.Nothing, "By default, trailing commas are allowed");
			Assert.That(() => JsonValue.Parse(jsonText, CrystalJsonSettings.Json), Throws.Nothing, "By default, trailing commas are allowed");
			Assert.That(() => JsonValue.Parse(jsonText, CrystalJsonSettings.JsonStrict), Throws.InstanceOf<JsonSyntaxException>(), "Strict mode does not allow trailing commas");
			Assert.That(() => JsonValue.Parse(jsonText, CrystalJsonSettings.Json.WithoutTrailingCommas()), Throws.InstanceOf<JsonSyntaxException>(), "Should fail when commas are explicitly forbidden");
			Assert.That(JsonObject.Parse(jsonText), Has.Count.EqualTo(2), "Ignored trailing commas should not add any extra item to the object");
			Assert.That(JsonObject.Parse(jsonText, CrystalJsonSettings.Json), Has.Count.EqualTo(2), "Ignored trailing commas should not add any extra item to the object");

			// interning corner cases

			{ // values that are small integers (-128..255à should all be refs to cached instances
				obj = JsonObject.Parse("""{ "A": 0, "B": 1, "C": 42, "D": -1, "E": 255, "F": -128 }""");
				Assert.That(obj, Is.Not.Null.And.Count.EqualTo(6));
				Assert.That(obj["A"], Is.SameAs(JsonNumber.Zero));
				Assert.That(obj["B"], Is.SameAs(JsonNumber.One));
				Assert.That(obj["C"], Is.SameAs(JsonNumber.Return(42)));
				Assert.That(obj["D"], Is.SameAs(JsonNumber.MinusOne));
				Assert.That(obj["E"], Is.SameAs(JsonNumber.Return(255)));
				Assert.That(obj["F"], Is.SameAs(JsonNumber.Return(-128)));
			}
		}

		[Test]
		public void Test_String_Interning()
		{
			// By default, string interning is only enabled on the names of object properties, meaning that multiple objects with the same fields will share the same string key in memory
			// It can be overriden in the deserialization settings

			const string TEXT = @"[ { ""Foo"":""Bar"" }, { ""Foo"":""Bar"" } ]";

			// by default, only the keys are interned, not the values
			var array = JsonArray.Parse(TEXT, CrystalJsonSettings.Json.WithInterning(CrystalJsonSettings.StringInterning.Default)).Select(x => ((JsonObject) x).First()).ToArray();
			var one = array[0];
			var two = array[1];

			Assert.That(one.Key, Is.EqualTo("Foo"));
			Assert.That(two.Key, Is.EqualTo(one.Key), "Keys should be EQUAL");
			Assert.That(two.Key, Is.SameAs(one.Key), "Keys SHOULD be the SAME reference");

			Assert.That(one.Value.Type, Is.EqualTo(JsonType.String));
			Assert.That(one.Value.ToString(), Is.EqualTo("Bar"));
			Assert.That(two.Value.Type, Is.EqualTo(JsonType.String));
			Assert.That(two.Value.ToString(), Is.EqualTo("Bar"));
			Assert.That(((JsonString)two.Value).Value, Is.Not.SameAs(((JsonString)one.Value).Value), "Values should NOT be the SAME reference");

			// when disabling interning, neither keys nor values should be interned

			array = JsonArray.Parse(TEXT, CrystalJsonSettings.Json.DisableInterning()).Select(x => ((JsonObject)x).First()).ToArray();
			one = array[0];
			two = array[1];

			Assert.That(one.Key, Is.EqualTo("Foo"));
			Assert.That(two.Key, Is.EqualTo("Foo"), "Keys should be EQUAL");
			Assert.That(two.Key, Is.Not.SameAs(one.Key), "Keys should NOT be the SAME reference");

			Assert.That(one.Value.Type, Is.EqualTo(JsonType.String));
			Assert.That(one.Value.ToString(), Is.EqualTo("Bar"));
			Assert.That(two.Value.Type, Is.EqualTo(JsonType.String));
			Assert.That(two.Value.ToString(), Is.EqualTo("Bar"));
			Assert.That(((JsonString)two.Value).Value, Is.Not.SameAs(((JsonString)one.Value).Value), "Values should NOT be the SAME reference");

			// when enabling full interning, both keys and values should be interned

			array = JsonArray.Parse(TEXT, CrystalJsonSettings.Json.WithInterning(CrystalJsonSettings.StringInterning.IncludeValues)).Select(x => ((JsonObject)x).First()).ToArray();
			one = array[0];
			two = array[1];

			Assert.That(one.Key, Is.EqualTo("Foo"));
			Assert.That(two.Key, Is.EqualTo("Foo"), "Keys should be EQUAL");
			Assert.That(two.Key, Is.SameAs(one.Key), "Keys SHOULD be the SAME reference");

			Assert.That(one.Value.Type, Is.EqualTo(JsonType.String));
			Assert.That(one.Value.ToString(), Is.EqualTo("Bar"));
			Assert.That(two.Value.Type, Is.EqualTo(JsonType.String));
			Assert.That(two.Value.ToString(), Is.EqualTo("Bar"));
			Assert.That(((JsonString)two.Value).Value, Is.SameAs(((JsonString)one.Value).Value), "Values SHOULD be the SAME reference");
		}

		[Test]
		public void Test_Parse_Via_Utf8StringReader()
		{
			var obj = new
			{
				Foo = "Héllö",
				Bar = "世界!",
				ಠ_ಠ = "(╯°□°）╯︵ ┻━┻",
			};
			Slice bytes = CrystalJson.ToSlice(obj);
			Log(bytes.ToString("P"));

			var json = JsonObject.Parse(bytes);
			Assert.That(json, Is.Not.Null);
			Assert.That(json.Get<string>("Foo"), Is.EqualTo("Héllö"));
			Assert.That(json.Get<string>("Bar"), Is.EqualTo("世界!"));
			Assert.That(json.Get<string>("ಠ_ಠ"), Is.EqualTo("(╯°□°）╯︵ ┻━┻"));
			Assert.That(json, Has.Count.EqualTo(3));
		}

		[Test]
		public void Test_Duplicate_Object_Fields()
		{
			// by default, an object with a duplicate field should throw

			Assert.That(
				() => JsonObject.Parse("""{ "Foo": "1", "Bar": "Baz", "Foo": "2" }"""),
				Throws.InstanceOf<JsonSyntaxException>(),
				"JSON Object with duplicate fields should throw by default");

			// but it can be overriden via the settings, and in this case the last value wins
			Assert.That(
				() => JsonObject.Parse("""{ "Foo": "1", "Bar": "Baz", "Foo": "2" }""", CrystalJsonSettings.Json.FlattenDuplicateFields()),
				Throws.Nothing,
				"JSON Object with duplicate fields should not throw is 'FlattenDuplicateFields' option is set"
			);
			var obj = JsonObject.Parse("""{ "Foo": "1", "Bar": "Baz", "Foo": "2" }""", CrystalJsonSettings.Json.FlattenDuplicateFields());
			Assert.That(obj.Get<string>("Foo"), Is.EqualTo("2"), "Duplicate fields should keep the last occurrence");
			Assert.That(obj.Get<string>("Bar"), Is.EqualTo("Baz"));
			Assert.That(obj, Has.Count.EqualTo(2));
		}

	}

}
