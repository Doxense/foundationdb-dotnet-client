#region Copyright (c) 2023-2024 SnowBank SAS, (c) 2005-2023 Doxense SAS
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

// ReSharper disable RedundantArgumentDefaultValue
// ReSharper disable HeuristicUnreachableCode
// ReSharper disable AccessToDisposedClosure
// ReSharper disable PossibleNullReferenceException
// ReSharper disable ReturnValueOfPureMethodIsNotUsed
// ReSharper disable UnusedMember.Local
// ReSharper disable UnusedParameter.Local
// ReSharper disable UnusedMember.Global
// ReSharper disable UseRawString
// ReSharper disable JoinDeclarationAndInitializer
// ReSharper disable UseObjectOrCollectionInitializer
#pragma warning disable CS8632 // The annotation for nullable reference types should only be used in code within a '#nullable' annotations context.
#pragma warning disable 618
#nullable disable //note: too much nullability warnings!

// ReSharper disable HeapView.BoxingAllocation
// ReSharper disable HeapView.DelegateAllocation
// ReSharper disable HeapView.ClosureAllocation
// ReSharper disable RedundantCast
// ReSharper disable HeapView.ObjectAllocation
// ReSharper disable AssignNullToNotNullAttribute
// ReSharper disable CompareOfFloatsByEqualityOperator
// ReSharper disable PossibleMultipleEnumeration
// ReSharper disable AccessToModifiedClosure

namespace Doxense.Serialization.Json.Tests
{
	using System;
	using System.Collections.Generic;
	using System.Collections.ObjectModel;
	using System.Diagnostics;
	using System.Diagnostics.CodeAnalysis;
	using System.Globalization;
	using System.IO;
	using System.IO.Compression;
	using System.Linq;
	using System.Linq.Expressions;
	using System.Net;
	using System.Runtime.Serialization;
	using System.Text;
	using System.Threading;
	using System.Threading.Tasks;
	using System.Xml.Serialization;
	using Doxense.Collections.Tuples;
	using Doxense.Memory;
	using Doxense.Runtime.Converters;
	using Doxense.Testing;
	using NUnit.Framework;
	using NUnit.Framework.Constraints;

	[TestFixture]
	[Category("Core-SDK")]
	public class CrystalJsonTest : DoxenseTest
	{

		#region Settings...

		[Test]
		public void Test_JsonSettings_DefaultValues()
		{
			// Par défaut, on doit avoir toutes les valeurs par défaut (0 / false)

			var settings = CrystalJsonSettings.Json;
			Assert.That(settings.TargetLanguage, Is.EqualTo(CrystalJsonSettings.Target.Json));
			Assert.That(settings.TextLayout, Is.EqualTo(CrystalJsonSettings.Layout.Formatted));
			Assert.That(settings.DateFormatting, Is.EqualTo(CrystalJsonSettings.DateFormat.Default));
			Assert.That(settings.InterningMode, Is.EqualTo(CrystalJsonSettings.StringInterning.Default));
			Assert.That(settings.HideDefaultValues, Is.False);
			Assert.That(settings.ShowNullMembers, Is.False);
			Assert.That(settings.UseCamelCasingForNames, Is.False);
			Assert.That(settings.OptimizeForLargeData, Is.False);
			Assert.That(settings.Flags, Is.EqualTo(CrystalJsonSettings.OptionFlags.None));
			Assert.That(CrystalJsonSettings.Json, Is.SameAs(settings));

			// JsonIndented: Seul le TextLayout doit être différent

			settings = CrystalJsonSettings.JsonIndented;
			Assert.That(settings.TargetLanguage, Is.EqualTo(CrystalJsonSettings.Target.Json));
			Assert.That(settings.TextLayout, Is.EqualTo(CrystalJsonSettings.Layout.Indented));
			Assert.That(settings.DateFormatting, Is.EqualTo(CrystalJsonSettings.DateFormat.Default));
			Assert.That(settings.InterningMode, Is.EqualTo(CrystalJsonSettings.StringInterning.Default));
			Assert.That(settings.HideDefaultValues, Is.False);
			Assert.That(settings.ShowNullMembers, Is.False);
			Assert.That(settings.UseCamelCasingForNames, Is.False);
			Assert.That(settings.OptimizeForLargeData, Is.False);
			Assert.That(settings.Flags, Is.EqualTo(CrystalJsonSettings.OptionFlags.Layout_Indented));
			Assert.That(CrystalJsonSettings.JsonIndented, Is.SameAs(settings));

			// JsonCompact: Seul le TextLayout doit être différent

			settings = CrystalJsonSettings.JsonCompact;
			Assert.That(settings.TargetLanguage, Is.EqualTo(CrystalJsonSettings.Target.Json));
			Assert.That(settings.TextLayout, Is.EqualTo(CrystalJsonSettings.Layout.Compact));
			Assert.That(settings.DateFormatting, Is.EqualTo(CrystalJsonSettings.DateFormat.Default));
			Assert.That(settings.InterningMode, Is.EqualTo(CrystalJsonSettings.StringInterning.Default));
			Assert.That(settings.HideDefaultValues, Is.False);
			Assert.That(settings.ShowNullMembers, Is.False);
			Assert.That(settings.UseCamelCasingForNames, Is.False);
			Assert.That(settings.OptimizeForLargeData, Is.False);
			Assert.That(settings.Flags, Is.EqualTo(CrystalJsonSettings.OptionFlags.Layout_Compact));
			Assert.That(CrystalJsonSettings.JsonCompact, Is.SameAs(settings));

			// JavaScript: Target le JavaScript (single quotes, les dates au format "new Date(...)")

			settings = CrystalJsonSettings.JavaScript;
			Assert.That(settings.TargetLanguage, Is.EqualTo(CrystalJsonSettings.Target.JavaScript));
			Assert.That(settings.TextLayout, Is.EqualTo(CrystalJsonSettings.Layout.Formatted));
			Assert.That(settings.DateFormatting, Is.EqualTo(CrystalJsonSettings.DateFormat.Default));
			Assert.That(settings.InterningMode, Is.EqualTo(CrystalJsonSettings.StringInterning.Default));
			Assert.That(settings.HideDefaultValues, Is.False);
			Assert.That(settings.ShowNullMembers, Is.False);
			Assert.That(settings.UseCamelCasingForNames, Is.False);
			Assert.That(settings.OptimizeForLargeData, Is.False);
			Assert.That(settings.Flags, Is.EqualTo(CrystalJsonSettings.OptionFlags.Target_JavaScript));
			Assert.That(CrystalJsonSettings.JavaScript, Is.SameAs(settings));

			// JavaScriptIndented: Target le JavaScript (single quotes, les dates au format "new Date(...)") et indenté

			settings = CrystalJsonSettings.JavaScriptIndented;
			Assert.That(settings.TargetLanguage, Is.EqualTo(CrystalJsonSettings.Target.JavaScript));
			Assert.That(settings.TextLayout, Is.EqualTo(CrystalJsonSettings.Layout.Indented));
			Assert.That(settings.DateFormatting, Is.EqualTo(CrystalJsonSettings.DateFormat.Default));
			Assert.That(settings.InterningMode, Is.EqualTo(CrystalJsonSettings.StringInterning.Default));
			Assert.That(settings.HideDefaultValues, Is.False);
			Assert.That(settings.ShowNullMembers, Is.False);
			Assert.That(settings.UseCamelCasingForNames, Is.False);
			Assert.That(settings.OptimizeForLargeData, Is.False);
			Assert.That(settings.Flags, Is.EqualTo(CrystalJsonSettings.OptionFlags.Target_JavaScript | CrystalJsonSettings.OptionFlags.Layout_Indented));
			Assert.That(CrystalJsonSettings.JavaScriptIndented, Is.SameAs(settings));
		}

		[Test]
		public void Test_JsonSettings_Flags()
		{
			var settings = CrystalJsonSettings.Json;

			// Set

			Assert.That(settings.HideDefaultValues, Is.False);
			settings = settings.WithoutDefaultValues();
			Assert.That(settings.Flags, Is.EqualTo(CrystalJsonSettings.OptionFlags.HideDefaultValues));
			Assert.That(settings.HideDefaultValues, Is.True);

			Assert.That(settings.ShowNullMembers, Is.False);
			settings = settings.WithNullMembers();
			Assert.That(settings.Flags, Is.EqualTo(CrystalJsonSettings.OptionFlags.HideDefaultValues | CrystalJsonSettings.OptionFlags.ShowNullMembers));
			Assert.That(settings.ShowNullMembers, Is.True);

			Assert.That(settings.UseCamelCasingForNames, Is.False);
			settings = settings.CamelCased();
			Assert.That(settings.Flags, Is.EqualTo(CrystalJsonSettings.OptionFlags.HideDefaultValues | CrystalJsonSettings.OptionFlags.ShowNullMembers | CrystalJsonSettings.OptionFlags.UseCamelCasingForName));
			Assert.That(settings.UseCamelCasingForNames, Is.True);

			Assert.That(settings.OptimizeForLargeData, Is.False);
			settings = settings.ExpectLargeData();
			Assert.That(settings.Flags, Is.EqualTo(CrystalJsonSettings.OptionFlags.HideDefaultValues | CrystalJsonSettings.OptionFlags.ShowNullMembers | CrystalJsonSettings.OptionFlags.UseCamelCasingForName | CrystalJsonSettings.OptionFlags.OptimizeForLargeData));
			Assert.That(settings.OptimizeForLargeData, Is.True);

			// Clear

			settings = settings.PascalCased();
			Assert.That(settings.Flags, Is.EqualTo(CrystalJsonSettings.OptionFlags.HideDefaultValues | CrystalJsonSettings.OptionFlags.ShowNullMembers | CrystalJsonSettings.OptionFlags.OptimizeForLargeData));
			Assert.That(settings.UseCamelCasingForNames, Is.False);

			settings = settings.WithDefaultValues();
			Assert.That(settings.Flags, Is.EqualTo(CrystalJsonSettings.OptionFlags.ShowNullMembers | CrystalJsonSettings.OptionFlags.OptimizeForLargeData));
			Assert.That(settings.HideDefaultValues, Is.False);

			settings = settings.OptimizedFor(largeData: false);
			Assert.That(settings.Flags, Is.EqualTo(CrystalJsonSettings.OptionFlags.ShowNullMembers));
			Assert.That(settings.OptimizeForLargeData, Is.False);

			settings = settings.WithoutNullMembers();
			Assert.That(settings.Flags, Is.EqualTo(CrystalJsonSettings.OptionFlags.None));
			Assert.That(settings.ShowNullMembers, Is.False);

			// retour à la case départ!
			Assert.That(settings, Is.SameAs(CrystalJsonSettings.Json));
		}

		[Test]
		public void Test_JsonSettings_TextLayout()
		{
			var settings = CrystalJsonSettings.Json;
			Assert.That(settings.Flags, Is.EqualTo(CrystalJsonSettings.OptionFlags.None));
			Assert.That(settings.TextLayout, Is.EqualTo(CrystalJsonSettings.Layout.Formatted));

			// Layout => Indentend
			settings = settings.Indented();
			Assert.That(settings.Flags, Is.EqualTo(CrystalJsonSettings.OptionFlags.Layout_Indented));
			Assert.That(settings.TextLayout, Is.EqualTo(CrystalJsonSettings.Layout.Indented));

			// Layout => Compact
			settings = settings.Compacted();
			Assert.That(settings.Flags, Is.EqualTo(CrystalJsonSettings.OptionFlags.Layout_Compact));
			Assert.That(settings.TextLayout, Is.EqualTo(CrystalJsonSettings.Layout.Compact));

			// Layout => Formatted
			settings = settings.WithTextLayout(CrystalJsonSettings.Layout.Formatted);
			Assert.That(settings.Flags, Is.EqualTo(CrystalJsonSettings.OptionFlags.None));
			Assert.That(settings.TextLayout, Is.EqualTo(CrystalJsonSettings.Layout.Formatted));

			// Ne doit pas écraser les autres settings
			settings = settings.CamelCased();
			settings = settings.WithDateFormat(CrystalJsonSettings.DateFormat.TimeStampIso8601);
			settings = settings.Indented();
			Assert.That(settings.Flags, Is.EqualTo(CrystalJsonSettings.OptionFlags.Layout_Indented | CrystalJsonSettings.OptionFlags.UseCamelCasingForName | CrystalJsonSettings.OptionFlags.DateFormat_TimeStampIso8601));
		}

		[Test]
		public void Test_JsonSettings_DateFormatting()
		{
			var settings = CrystalJsonSettings.Json;
			Assert.That(settings.Flags, Is.EqualTo(CrystalJsonSettings.OptionFlags.None));
			Assert.That(settings.DateFormatting, Is.EqualTo(CrystalJsonSettings.DateFormat.Default));

			// DateFormat => Microsoft
			settings = settings.WithMicrosoftDates();
			Assert.That(settings.Flags, Is.EqualTo(CrystalJsonSettings.OptionFlags.DateFormat_Microsoft));
			Assert.That(settings.DateFormatting, Is.EqualTo(CrystalJsonSettings.DateFormat.Microsoft));

			// DateFormat => JavaScript
			settings = settings.WithJavaScriptDates();
			Assert.That(settings.Flags, Is.EqualTo(CrystalJsonSettings.OptionFlags.DateFormat_JavaScript));
			Assert.That(settings.DateFormatting, Is.EqualTo(CrystalJsonSettings.DateFormat.JavaScript));

			// DateFormat => Iso
			settings = settings.WithIso8601Dates();
			Assert.That(settings.Flags, Is.EqualTo(CrystalJsonSettings.OptionFlags.DateFormat_TimeStampIso8601));
			Assert.That(settings.DateFormatting, Is.EqualTo(CrystalJsonSettings.DateFormat.TimeStampIso8601));

			// DateFormat => Default
			settings = settings.WithDateFormat(CrystalJsonSettings.DateFormat.Default);
			Assert.That(settings.Flags, Is.EqualTo(CrystalJsonSettings.OptionFlags.None));
			Assert.That(settings.DateFormatting, Is.EqualTo(CrystalJsonSettings.DateFormat.Default));

			// Ne doit pas écraser les autres settings
			settings = settings.CamelCased();
			settings = settings.WithInterning(CrystalJsonSettings.StringInterning.IncludeValues);
			settings = settings.WithIso8601Dates();
			Assert.That(settings.Flags, Is.EqualTo(CrystalJsonSettings.OptionFlags.DateFormat_TimeStampIso8601 | CrystalJsonSettings.OptionFlags.StringInterning_IncludeValues | CrystalJsonSettings.OptionFlags.UseCamelCasingForName));
		}

		[Test]
		public void Test_JsonSettings_StringInterning()
		{
			var settings = CrystalJsonSettings.Json;
			Assert.That(settings.Flags, Is.EqualTo(CrystalJsonSettings.OptionFlags.None));
			Assert.That(settings.InterningMode, Is.EqualTo(CrystalJsonSettings.StringInterning.Default));

			// InterningMode => Disabled
			settings = settings.DisableInterning();
			Assert.That(settings.Flags, Is.EqualTo(CrystalJsonSettings.OptionFlags.StringInterning_Disabled));
			Assert.That(settings.InterningMode, Is.EqualTo(CrystalJsonSettings.StringInterning.Disabled));

			// InterningMode => IncludeNumbers
			settings = settings.WithInterning(CrystalJsonSettings.StringInterning.IncludeNumbers);
			Assert.That(settings.Flags, Is.EqualTo(CrystalJsonSettings.OptionFlags.StringInterning_IncludeNumbers));
			Assert.That(settings.InterningMode, Is.EqualTo(CrystalJsonSettings.StringInterning.IncludeNumbers));

			// InterningMode => IncludeValues
			settings = settings.WithInterning(CrystalJsonSettings.StringInterning.IncludeValues);
			Assert.That(settings.Flags, Is.EqualTo(CrystalJsonSettings.OptionFlags.StringInterning_IncludeValues));
			Assert.That(settings.InterningMode, Is.EqualTo(CrystalJsonSettings.StringInterning.IncludeValues));

			// InterningMode => Default
			settings = settings.WithInterning(CrystalJsonSettings.StringInterning.Default);
			Assert.That(settings.Flags, Is.EqualTo(CrystalJsonSettings.OptionFlags.None));
			Assert.That(settings.InterningMode, Is.EqualTo(CrystalJsonSettings.StringInterning.Default));

			// Ne doit pas écraser les autres settings
			settings = settings.CamelCased();
			settings = settings.WithDateFormat(CrystalJsonSettings.DateFormat.TimeStampIso8601);
			settings = settings.DisableInterning();
			Assert.That(settings.Flags, Is.EqualTo(CrystalJsonSettings.OptionFlags.StringInterning_Disabled | CrystalJsonSettings.OptionFlags.UseCamelCasingForName | CrystalJsonSettings.OptionFlags.DateFormat_TimeStampIso8601));
		}

		[Test]
		public void Test_JsonSettings_Immutability()
		{
			var a = CrystalJsonSettings.Json;
			Assert.That(a, Is.Not.Null);
			Assert.That(a.Flags, Is.EqualTo(CrystalJsonSettings.OptionFlags.None));

			var b = a.WithEnumAsStrings();
			Assert.That(b, Is.Not.Null.And.Not.SameAs(a));
			Assert.That(b.Flags, Is.EqualTo(CrystalJsonSettings.OptionFlags.EnumsAsString));
			Assert.That(a.Flags, Is.EqualTo(CrystalJsonSettings.OptionFlags.None));

			var c = a.WithEnumAsNumbers();
			Assert.That(c, Is.SameAs(a));
			Assert.That(c.Flags, Is.EqualTo(CrystalJsonSettings.OptionFlags.None));
			Assert.That(b.Flags, Is.EqualTo(CrystalJsonSettings.OptionFlags.EnumsAsString));
		}

		#endregion

		#region Serialization...

		/// <summary>Helper pour wrapper les appels a SerializeTo(..) dans un StringWriter, et retourne la chaine générée</summary>
		/// <param name="action"></param>
		/// <returns></returns>
		private static string SerializeToString(Func<TextWriter, TextWriter> action)
		{
			using (var sw = new StringWriter())
			{
				var rw = action(sw);
				Assert.That(rw, Is.SameAs(sw), "SerializeTo should return the input TextWriter!");
				return sw.ToString();
			}
		}

		private static Slice SerializeToSlice(JsonValue value)
		{
			var writer = new SliceWriter();
			value.WriteTo(ref writer);
			return writer.ToSlice();
		}

		[Test]
		public void Test_Json_String_Encoding()
		{
			// sans quotes
			Assert.That(CrystalJson.StringEncode(null), Is.EqualTo("null"));
			Assert.That(CrystalJson.StringEncode(""), Is.EqualTo(@""""""));
			Assert.That(CrystalJson.StringEncode("foo"), Is.EqualTo(@"""foo"""));
			Assert.That(CrystalJson.StringEncode("'"), Is.EqualTo(@"""'"""));
			Assert.That(CrystalJson.StringEncode("\""), Is.EqualTo(@"""\"""""));
			Assert.That(CrystalJson.StringEncode("A\""), Is.EqualTo(@"""A\"""""));
			Assert.That(CrystalJson.StringEncode("\"A"), Is.EqualTo(@"""\""A"""));
			Assert.That(CrystalJson.StringEncode("A\"A"), Is.EqualTo(@"""A\""A"""));
			Assert.That(CrystalJson.StringEncode("A\0"), Is.EqualTo("\"A\\u0000\""));
			Assert.That(CrystalJson.StringEncode("\0A"), Is.EqualTo("\"\\u0000A\""));
			Assert.That(CrystalJson.StringEncode("A\0A"), Is.EqualTo("\"A\\u0000A\""));
			Assert.That(CrystalJson.StringEncode("All Your Bases Are Belong To Us"), Is.EqualTo(@"""All Your Bases Are Belong To Us"""));
			Assert.That(CrystalJson.StringEncode("<script>alert('narf!');</script>"), Is.EqualTo(@"""<script>alert('narf!');</script>"""));
			Assert.That(CrystalJson.StringEncode("<script>alert(\"zort!\");</script>"), Is.EqualTo(@"""<script>alert(\""zort!\"");</script>"""));
			Assert.That(CrystalJson.StringEncode("Test de text normal avec juste a la fin un '"), Is.EqualTo(@"""Test de text normal avec juste a la fin un '"""));
			Assert.That(CrystalJson.StringEncode("Test de text normal avec juste a la fin un \""), Is.EqualTo(@"""Test de text normal avec juste a la fin un \"""""));
			Assert.That(CrystalJson.StringEncode("'Test de text normal avec des quotes autour'"), Is.EqualTo(@"""'Test de text normal avec des quotes autour'"""));
			Assert.That(CrystalJson.StringEncode("\"Test de text normal avec des double quotes autour\""), Is.EqualTo(@"""\""Test de text normal avec des double quotes autour\"""""));
			Assert.That(CrystalJson.StringEncode("Test'de\"text'avec\"les'deux\"types"), Is.EqualTo("\"Test'de\\\"text'avec\\\"les'deux\\\"types\""));
			Assert.That(CrystalJson.StringEncode("/"), Is.EqualTo(@"""/""")); // le slash doit etre laissé tel quel (on reserve le \/ pour les dates)
			Assert.That(CrystalJson.StringEncode(@"/\/\\//\\\///"), Is.EqualTo(@"""/\\/\\\\//\\\\\\///"""));
			Assert.That(CrystalJson.StringEncode("\x00\x01\x02\x03\x04\x05\x06\x07\x08\x09\x0A\x0B\x0C\x0D\x0E\x0F"), Is.EqualTo(@"""\u0000\u0001\u0002\u0003\u0004\u0005\u0006\u0007\b\t\n\u000b\f\r\u000e\u000f"""), "ASCII 0..15");
			Assert.That(CrystalJson.StringEncode("\x10\x11\x12\x13\x14\x15\x16\x17\x18\x19\x1A\x1B\x1C\x1D\x1E\x1F"), Is.EqualTo(@"""\u0010\u0011\u0012\u0013\u0014\u0015\u0016\u0017\u0018\u0019\u001a\u001b\u001c\u001d\u001e\u001f"""), "ASCII 16..31");
			Assert.That(CrystalJson.StringEncode(" !\"#$%&'()*+,-./"), Is.EqualTo(@""" !\""#$%&'()*+,-./"""), "ASCII 32..47");
			Assert.That(CrystalJson.StringEncode(":;<=>?@"), Is.EqualTo(@""":;<=>?@"""), "ASCII 58..64");
			Assert.That(CrystalJson.StringEncode("[\\]^_`"), Is.EqualTo(@"""[\\]^_`"""), "ASCII 91..96");
			Assert.That(CrystalJson.StringEncode("{|}~"), Is.EqualTo(@"""{|}~"""), "ASCII 123..126");
			Assert.That(CrystalJson.StringEncode("\x7F"), Is.EqualTo("\"\x7F\""), "ASCI 127");
			Assert.That(CrystalJson.StringEncode("\x80"), Is.EqualTo("\"\x80\""), "ASCI 128");
			Assert.That(CrystalJson.StringEncode("üéâäàåçêëèïîìÄÅÉæÆôöòûùÿÖÜø£Ø×ƒáíóúñÑªº¿®¬½¼¡«»░▒▓│┤ÁÂÀ©╣║╗╝¢¥┐└┴┬├─┼ãÃ"), Is.EqualTo(@"""üéâäàåçêëèïîìÄÅÉæÆôöòûùÿÖÜø£Ø×ƒáíóúñÑªº¿®¬½¼¡«»░▒▓│┤ÁÂÀ©╣║╗╝¢¥┐└┴┬├─┼ãÃ"""), "ASCI 129-199");
			Assert.That(CrystalJson.StringEncode("╚╔╩╦╠═╬¤ðÐÊËÈıÍÎÏ┘┌█▄¦Ì▀ÓßÔÒõÕµþÞÚÛÙýÝ¯´­±‗¾¶§÷¸°¨·¹³²■"), Is.EqualTo(@"""╚╔╩╦╠═╬¤ðÐÊËÈıÍÎÏ┘┌█▄¦Ì▀ÓßÔÒõÕµþÞÚÛÙýÝ¯´­±‗¾¶§÷¸°¨·¹³²■"""), "ASCI 200-254");
			Assert.That(CrystalJson.StringEncode("\xFF"), Is.EqualTo("\"\xFF\""), "ASCI 255");
			Assert.That(CrystalJson.StringEncode("الصفحة_الرئيسية"), Is.EqualTo(@"""الصفحة_الرئيسية""")); // Arabe
			Assert.That(CrystalJson.StringEncode("メインページ"), Is.EqualTo(@"""メインページ""")); // Japonais
			Assert.That(CrystalJson.StringEncode("首页"), Is.EqualTo(@"""首页""")); // Chinois
			Assert.That(CrystalJson.StringEncode("대문"), Is.EqualTo(@"""대문""")); // Corréen
			Assert.That(CrystalJson.StringEncode("Κύρια Σελίδα"), Is.EqualTo(@"""Κύρια Σελίδα""")); // Ellenika
			Assert.That(CrystalJson.StringEncode("\xD7FF"), Is.EqualTo("\"\xD7FF\""), "Juste avant les non-BMP (D7FF)");
			Assert.That(CrystalJson.StringEncode("\xD800\xDFFF"), Is.EqualTo(@"""\ud800\udfff"""), "non-BMP range: D800-DFFF");
			Assert.That(CrystalJson.StringEncode("\xE000"), Is.EqualTo("\"\xE000\""), "Juste après les non-BMP (E000)");
			Assert.That(CrystalJson.StringEncode("\xFFFE"), Is.EqualTo(@"""\ufffe"""), "BOM UTF-16 LE (FFFE)");
			Assert.That(CrystalJson.StringEncode("\xFFFF"), Is.EqualTo(@"""\uffff"""), "BOM UTF-16 BE (FFFF)");

			// test coverage !
			Assert.That(JsonEncoding.NeedsEscaping("a"), Is.False, "LOWER CASE 'a'");
			Assert.That(JsonEncoding.NeedsEscaping("\""), Is.True, "DOUBLE QUOTE");
			Assert.That(JsonEncoding.NeedsEscaping("\\"), Is.True, "ANTI SLASH");
			Assert.That(JsonEncoding.NeedsEscaping("\x00"), Is.True, "ASCII NULL");
			Assert.That(JsonEncoding.NeedsEscaping("\x07"), Is.True, "ASCII 7");
			Assert.That(JsonEncoding.NeedsEscaping("\x1F"), Is.True, "ASCII 31");
			Assert.That(JsonEncoding.NeedsEscaping(" "), Is.False, "SPACE");
			Assert.That(JsonEncoding.NeedsEscaping("\uD7FF"), Is.False, "UNICODE 0xD7FF");
			Assert.That(JsonEncoding.NeedsEscaping("\uD800"), Is.True, "UNICODE 0xD800");
			Assert.That(JsonEncoding.NeedsEscaping("\uE000"), Is.False, "UNICODE 0xE000");
			Assert.That(JsonEncoding.NeedsEscaping("\uFFFD"), Is.False, "UNICODE 0xFFFD");
			Assert.That(JsonEncoding.NeedsEscaping("\uFFFE"), Is.True, "UNICODE 0xFFFE");
			Assert.That(JsonEncoding.NeedsEscaping("\uFFFF"), Is.True, "UNICODE 0xFFFF");

			Assert.That(JsonEncoding.NeedsEscaping("aa"), Is.False);
			Assert.That(JsonEncoding.NeedsEscaping("aaa"), Is.False);
			Assert.That(JsonEncoding.NeedsEscaping("aaaa"), Is.False);
			Assert.That(JsonEncoding.NeedsEscaping("aaaaa"), Is.False);
			Assert.That(JsonEncoding.NeedsEscaping("aaaaaa"), Is.False);
			Assert.That(JsonEncoding.NeedsEscaping("aaaaaaa"), Is.False);
			Assert.That(JsonEncoding.NeedsEscaping("a\""), Is.True);
			Assert.That(JsonEncoding.NeedsEscaping("aa\""), Is.True);
			Assert.That(JsonEncoding.NeedsEscaping("aaa\""), Is.True);
			Assert.That(JsonEncoding.NeedsEscaping("aaaa\""), Is.True);
			Assert.That(JsonEncoding.NeedsEscaping("aaaaa\""), Is.True);
			Assert.That(JsonEncoding.NeedsEscaping("aaaaaa\""), Is.True);
		}

		[Test]
		public void Test_JsonSerialize_Null()
		{
			Assert.That(CrystalJson.Serialize(null), Is.EqualTo("null"));
			Assert.That(CrystalJson.Serialize(null, CrystalJsonSettings.Json), Is.EqualTo("null"));
			Assert.That(CrystalJson.Serialize(null, CrystalJsonSettings.JsonCompact), Is.EqualTo("null"));

			Assert.That(SerializeToString(sw => CrystalJson.SerializeTo(sw, null)), Is.EqualTo("null"));
			Assert.That(SerializeToString(sw => CrystalJson.SerializeTo(sw, null, CrystalJsonSettings.Json)), Is.EqualTo("null"));
			Assert.That(SerializeToString(sw => CrystalJson.SerializeTo(sw, null, CrystalJsonSettings.JsonCompact)), Is.EqualTo("null"));
		}

		[Test]
		public void Test_JsonSerialize_String_Types()
		{
			// on établi les bases...
			Assume.That(typeof(string).IsPrimitive, Is.False);

			// string

			Assert.That(CrystalJson.Serialize(String.Empty), Is.EqualTo("\"\""));
			Assert.That(CrystalJson.Serialize("foo"), Is.EqualTo("\"foo\""));
			Assert.That(CrystalJson.Serialize("foo\"bar"), Is.EqualTo("\"foo\\\"bar\""));
			Assert.That(CrystalJson.Serialize("foo'bar"), Is.EqualTo("\"foo'bar\""));

			Assert.That(SerializeToString(sw => CrystalJson.SerializeTo(sw, String.Empty)), Is.EqualTo("\"\""));
			Assert.That(SerializeToString(sw => CrystalJson.SerializeTo(sw, "foo")), Is.EqualTo("\"foo\""));
			Assert.That(SerializeToString(sw => CrystalJson.SerializeTo(sw, "foo\"bar")), Is.EqualTo("\"foo\\\"bar\""));
			Assert.That(SerializeToString(sw => CrystalJson.SerializeTo(sw, "foo'bar")), Is.EqualTo("\"foo'bar\""));

			// StringBuilder

			Assert.That(CrystalJson.Serialize(new StringBuilder()), Is.EqualTo("\"\""));
			Assert.That(CrystalJson.Serialize(new StringBuilder("Foo")), Is.EqualTo("\"Foo\""));
			Assert.That(CrystalJson.Serialize(new StringBuilder("Foo").Append('"').Append("Bar")), Is.EqualTo("\"Foo\\\"Bar\""));
			Assert.That(CrystalJson.Serialize(new StringBuilder("Foo").Append('\'').Append("Bar")), Is.EqualTo("\"Foo'Bar\""));

			Assert.That(SerializeToString(sw => CrystalJson.SerializeTo(sw, new StringBuilder())), Is.EqualTo("\"\""));
			Assert.That(SerializeToString(sw => CrystalJson.SerializeTo(sw, new StringBuilder("Foo"))), Is.EqualTo("\"Foo\""));
			Assert.That(SerializeToString(sw => CrystalJson.SerializeTo(sw, new StringBuilder("Foo").Append('"').Append("Bar"))), Is.EqualTo("\"Foo\\\"Bar\""));
			Assert.That(SerializeToString(sw => CrystalJson.SerializeTo(sw, new StringBuilder("Foo").Append('\'').Append("Bar"))), Is.EqualTo("\"Foo'Bar\""));
		}

		[Test]
		public void Test_JavaScriptSerialize_String_Types()
		{
			// on établi les bases...
			Assume.That(typeof(string).IsPrimitive, Is.False);

			// string

			Assert.That(CrystalJson.Serialize(String.Empty, CrystalJsonSettings.JavaScript), Is.EqualTo("''"));
			Assert.That(CrystalJson.Serialize("foo", CrystalJsonSettings.JavaScript), Is.EqualTo("'foo'"));
			Assert.That(CrystalJson.Serialize("foo\"bar", CrystalJsonSettings.JavaScript), Is.EqualTo("'foo\\x22bar'"));
			Assert.That(CrystalJson.Serialize("foo'bar", CrystalJsonSettings.JavaScript), Is.EqualTo("'foo\\x27bar'"));

			Assert.That(SerializeToString(sw => CrystalJson.SerializeTo(sw, String.Empty, CrystalJsonSettings.JavaScript)), Is.EqualTo("''"));
			Assert.That(SerializeToString(sw => CrystalJson.SerializeTo(sw, "foo", CrystalJsonSettings.JavaScript)), Is.EqualTo("'foo'"));
			Assert.That(SerializeToString(sw => CrystalJson.SerializeTo(sw, "foo\"bar", CrystalJsonSettings.JavaScript)), Is.EqualTo("'foo\\x22bar'"));
			Assert.That(SerializeToString(sw => CrystalJson.SerializeTo(sw, "foo'bar", CrystalJsonSettings.JavaScript)), Is.EqualTo("'foo\\x27bar'"));

			// StringBuilder

			Assert.That(CrystalJson.Serialize(new StringBuilder(), CrystalJsonSettings.JavaScript), Is.EqualTo("''"));
			Assert.That(CrystalJson.Serialize(new StringBuilder("Foo"), CrystalJsonSettings.JavaScript), Is.EqualTo("'Foo'"));
			Assert.That(CrystalJson.Serialize(new StringBuilder("Foo").Append('"').Append("Bar"), CrystalJsonSettings.JavaScript), Is.EqualTo("'Foo\\x22Bar'"));
			Assert.That(CrystalJson.Serialize(new StringBuilder("Foo").Append('\'').Append("Bar"), CrystalJsonSettings.JavaScript), Is.EqualTo("'Foo\\x27Bar'"));

			Assert.That(SerializeToString(sw => CrystalJson.SerializeTo(sw, new StringBuilder(), CrystalJsonSettings.JavaScript)), Is.EqualTo("''"));
			Assert.That(SerializeToString(sw => CrystalJson.SerializeTo(sw, new StringBuilder("Foo"), CrystalJsonSettings.JavaScript)), Is.EqualTo("'Foo'"));
			Assert.That(SerializeToString(sw => CrystalJson.SerializeTo(sw, new StringBuilder("Foo").Append('"').Append("Bar"), CrystalJsonSettings.JavaScript)), Is.EqualTo("'Foo\\x22Bar'"));
			Assert.That(SerializeToString(sw => CrystalJson.SerializeTo(sw, new StringBuilder("Foo").Append('\'').Append("Bar"), CrystalJsonSettings.JavaScript)), Is.EqualTo("'Foo\\x27Bar'"));
		}

		[Test]
		public void Test_JsonSerialize_Primitive_Types()
		{
			// boolean
			Assert.That(CrystalJson.Serialize(true), Is.EqualTo("true"));
			Assert.That(CrystalJson.Serialize(false), Is.EqualTo("false"));
			// int32
			Assert.That(CrystalJson.Serialize((int)0), Is.EqualTo("0"));
			Assert.That(CrystalJson.Serialize((int)1), Is.EqualTo("1"));
			Assert.That(CrystalJson.Serialize((int)-1), Is.EqualTo("-1"));
			Assert.That(CrystalJson.Serialize((int)123), Is.EqualTo("123"));
			Assert.That(CrystalJson.Serialize((int)-999), Is.EqualTo("-999"));
			Assert.That(CrystalJson.Serialize(int.MaxValue), Is.EqualTo("2147483647"));
			Assert.That(CrystalJson.Serialize(int.MinValue), Is.EqualTo("-2147483648"));
			// int64
			Assert.That(CrystalJson.Serialize((long)0), Is.EqualTo("0"));
			Assert.That(CrystalJson.Serialize((long)1), Is.EqualTo("1"));
			Assert.That(CrystalJson.Serialize((long)-1), Is.EqualTo("-1"));
			Assert.That(CrystalJson.Serialize((long)123), Is.EqualTo("123"));
			Assert.That(CrystalJson.Serialize((long)-999), Is.EqualTo("-999"));
			Assert.That(CrystalJson.Serialize(long.MaxValue), Is.EqualTo("9223372036854775807"));
			Assert.That(CrystalJson.Serialize(long.MinValue), Is.EqualTo("-9223372036854775808"));
			// single
			Assert.That(CrystalJson.Serialize(0f), Is.EqualTo("0"));
			Assert.That(CrystalJson.Serialize(1f), Is.EqualTo("1"));
			Assert.That(CrystalJson.Serialize(-1f), Is.EqualTo("-1"));
			Assert.That(CrystalJson.Serialize(123f), Is.EqualTo("123"));
			Assert.That(CrystalJson.Serialize(123.456f), Is.EqualTo("123.456"));
			Assert.That(CrystalJson.Serialize(-999.9f), Is.EqualTo("-999.9"));
			Assert.That(CrystalJson.Serialize(float.MaxValue), Is.EqualTo(float.MaxValue.ToString("R")));
			Assert.That(CrystalJson.Serialize(float.MinValue), Is.EqualTo(float.MinValue.ToString("R")));
			Assert.That(CrystalJson.Serialize(float.Epsilon), Is.EqualTo(float.Epsilon.ToString("R")));
			//BUGBUG: pour l'instant "default" utilise FloatFormat.Symbol mais on risque de changer en String par défaut!
			Assert.That(CrystalJson.Serialize(float.NaN), Is.EqualTo("NaN"), "Pas standard, mais la plupart des serializers se comportent comme cela");
			Assert.That(CrystalJson.Serialize(float.PositiveInfinity), Is.EqualTo("Infinity"), "Pas standard, mais la plupart des serializers se comportent comme cela");
			Assert.That(CrystalJson.Serialize(float.NegativeInfinity), Is.EqualTo("-Infinity"), "Pas standard, mais la plupart des serializers se comportent comme cela");
			{ // NaN => 'NaN'
				var settings = CrystalJsonSettings.Json.WithFloatFormat(CrystalJsonSettings.FloatFormat.Symbol);
				Assert.That(CrystalJson.Serialize(float.NaN, settings), Is.EqualTo("NaN"), "Pas standard, mais la plupart des serializers se comportent comme cela");
				Assert.That(CrystalJson.Serialize(float.PositiveInfinity, settings), Is.EqualTo("Infinity"), "Pas standard, mais la plupart des serializers se comportent comme cela");
				Assert.That(CrystalJson.Serialize(float.NegativeInfinity, settings), Is.EqualTo("-Infinity"), "Pas standard, mais la plupart des serializers se comportent comme cela");
			}
			{ // NaN => '"NaN"'
				var settings = CrystalJsonSettings.Json.WithFloatFormat(CrystalJsonSettings.FloatFormat.String);
				Assert.That(CrystalJson.Serialize(float.NaN, settings), Is.EqualTo("\"NaN\""), "Comme le fait JSON.Net");
				Assert.That(CrystalJson.Serialize(float.PositiveInfinity, settings), Is.EqualTo("\"Infinity\""), "Comme le fait JSON.Net");
				Assert.That(CrystalJson.Serialize(float.NegativeInfinity, settings), Is.EqualTo("\"-Infinity\""), "Comme le fait JSON.Net");
			}
			{ // NaN => 'null'
				var settings = CrystalJsonSettings.Json.WithFloatFormat(CrystalJsonSettings.FloatFormat.Null);
				Assert.That(CrystalJson.Serialize(float.NaN, settings), Is.EqualTo("null"), "A défaut d'autre chose...");
				Assert.That(CrystalJson.Serialize(float.PositiveInfinity, settings), Is.EqualTo("null"), "A défaut d'autre chose...");
				Assert.That(CrystalJson.Serialize(float.NegativeInfinity, settings), Is.EqualTo("null"), "A défaut d'autre chose...");
			}
			// doublep
			Assert.That(CrystalJson.Serialize(0d), Is.EqualTo("0"));
			Assert.That(CrystalJson.Serialize(1d), Is.EqualTo("1"));
			Assert.That(CrystalJson.Serialize(-1d), Is.EqualTo("-1"));
			Assert.That(CrystalJson.Serialize(123d), Is.EqualTo("123"));
			Assert.That(CrystalJson.Serialize(123.456d), Is.EqualTo("123.456"));
			Assert.That(CrystalJson.Serialize(-999.9d), Is.EqualTo("-999.9"));
			Assert.That(CrystalJson.Serialize(double.MaxValue), Is.EqualTo(double.MaxValue.ToString("R")));
			Assert.That(CrystalJson.Serialize(double.MinValue), Is.EqualTo(double.MinValue.ToString("R")));
			Assert.That(CrystalJson.Serialize(double.Epsilon), Is.EqualTo(double.Epsilon.ToString("R")));
			//BUGBUG: pour l'instant "default" utilise FloatFormat.Symbol mais on risque de changer en String par défaut!
			Assert.That(CrystalJson.Serialize(double.NaN), Is.EqualTo("NaN"), "Pas standard, mais la plupart des serializers se comportent comme cela");
			Assert.That(CrystalJson.Serialize(double.PositiveInfinity), Is.EqualTo("Infinity"), "Pas standard, mais la plupart des serializers se comportent comme cela");
			Assert.That(CrystalJson.Serialize(double.NegativeInfinity), Is.EqualTo("-Infinity"), "Pas standard, mais la plupart des serializers se comportent comme cela");
			{ // NaN => 'NaN'
				var settings = CrystalJsonSettings.Json.WithFloatFormat(CrystalJsonSettings.FloatFormat.Symbol);
				Assert.That(CrystalJson.Serialize(double.NaN, settings), Is.EqualTo("NaN"), "Pas standard, mais la plupart des serializers se comportent comme cela");
				Assert.That(CrystalJson.Serialize(double.PositiveInfinity, settings), Is.EqualTo("Infinity"), "Pas standard, mais la plupart des serializers se comportent comme cela");
				Assert.That(CrystalJson.Serialize(double.NegativeInfinity, settings), Is.EqualTo("-Infinity"), "Pas standard, mais la plupart des serializers se comportent comme cela");
			}
			{ // NaN => '"NaN"'
				var settings = CrystalJsonSettings.Json.WithFloatFormat(CrystalJsonSettings.FloatFormat.String);
				Assert.That(CrystalJson.Serialize(double.NaN, settings), Is.EqualTo("\"NaN\""), "Comme le fait JSON.Net");
				Assert.That(CrystalJson.Serialize(double.PositiveInfinity, settings), Is.EqualTo("\"Infinity\""), "Comme le fait JSON.Net");
				Assert.That(CrystalJson.Serialize(double.NegativeInfinity, settings), Is.EqualTo("\"-Infinity\""), "Comme le fait JSON.Net");
			}
			{ // NaN => 'null'
				var settings = CrystalJsonSettings.Json.WithFloatFormat(CrystalJsonSettings.FloatFormat.Null);
				Assert.That(CrystalJson.Serialize(double.NaN, settings), Is.EqualTo("null"), "A défaut d'autre chose...");
				Assert.That(CrystalJson.Serialize(double.PositiveInfinity, settings), Is.EqualTo("null"), "A défaut d'autre chose...");
				Assert.That(CrystalJson.Serialize(double.NegativeInfinity, settings), Is.EqualTo("null"), "A défaut d'autre chose...");
			}
			// char
			Assert.That(CrystalJson.Serialize('A'), Is.EqualTo("\"A\""));
			Assert.That(CrystalJson.Serialize('\0'), Is.EqualTo("null"));
			Assert.That(CrystalJson.Serialize('\"'), Is.EqualTo("\"\\\"\""));

			// JavaScript exceptions:
			Assert.That(CrystalJson.Serialize(double.NaN, CrystalJsonSettings.JavaScript), Is.EqualTo("Number.NaN"), "Pas standard, mais la plupart des serializers se comportent comme cela");
			Assert.That(CrystalJson.Serialize(double.PositiveInfinity, CrystalJsonSettings.JavaScript), Is.EqualTo("Number.POSITIVE_INFINITY"), "Pas standard, mais la plupart des serializers se comportent comme cela");
			Assert.That(CrystalJson.Serialize(double.NegativeInfinity, CrystalJsonSettings.JavaScript), Is.EqualTo("Number.NEGATIVE_INFINITY"), "Pas standard, mais la plupart des serializers se comportent comme cela");

			var rnd = new Random();
			for (int i = 0; i < 1000; i++)
			{
				Assert.That(CrystalJson.Serialize(i), Is.EqualTo(i.ToString()));
				Assert.That(CrystalJson.Serialize(-i), Is.EqualTo((-i).ToString()));

				int x = rnd.Next() * (rnd.Next(2) == 1 ? -1 : 1);
				Assert.That(CrystalJson.Serialize(x), Is.EqualTo(x.ToString()));
				Assert.That(CrystalJson.Serialize((uint)x), Is.EqualTo(((uint)x).ToString()));

				long y = (long)x * rnd.Next() * (rnd.Next(2) == 1 ? -1L : 1L);
				Assert.That(CrystalJson.Serialize(y), Is.EqualTo(y.ToString()));
				Assert.That(CrystalJson.Serialize((ulong)y), Is.EqualTo(((ulong)y).ToString()));
			}
		}

		private static string GetTimeZoneSuffix(DateTime date)
		{
			TimeSpan utcOffset = TimeZoneInfo.Local.GetUtcOffset(date);
			return (utcOffset < TimeSpan.Zero ? "-" : "+") + utcOffset.ToString("hhmm");
		}

		[Test]
		public void Test_JsonValue_ToString_Formattable()
		{
			JsonValue num = 123;
			JsonValue flag = true;
			JsonValue txt = "Hello\"World";
			JsonValue arr = JsonArray.FromValues(Enumerable.Range(1, 20));
			JsonValue obj = new JsonObject { ["Foo"] = 123, ["Bar"] = "Narf Zort!", ["Baz"] =  JsonObject.Create("X", 1, "Y", 2, "Z", 3), ["Jazz"] = JsonArray.FromValues(Enumerable.Range(1, 5)) };

			// "D" = Default (=> ToJson)
			Assert.That(num.ToString("D"), Is.EqualTo("123"));
			Assert.That(flag.ToString("D"), Is.EqualTo("true"));
			Assert.That(txt.ToString("D"), Is.EqualTo(@"""Hello\""World"""));
			Assert.That(arr.ToString("D"), Is.EqualTo("[ 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20 ]"));
			Assert.That(obj.ToString("D"), Is.EqualTo(@"{ ""Foo"": 123, ""Bar"": ""Narf Zort!"", ""Baz"": { ""X"": 1, ""Y"": 2, ""Z"": 3 }, ""Jazz"": [ 1, 2, 3, 4, 5 ] }"));

			// "C" = Compact (=> ToJsonCompact)
			Assert.That(num.ToString("C"), Is.EqualTo("123"));
			Assert.That(flag.ToString("C"), Is.EqualTo("true"));
			Assert.That(txt.ToString("C"), Is.EqualTo(@"""Hello\""World"""));
			Assert.That(arr.ToString("C"), Is.EqualTo("[1,2,3,4,5,6,7,8,9,10,11,12,13,14,15,16,17,18,19,20]"));
			Assert.That(obj.ToString("C"), Is.EqualTo(@"{""Foo"":123,""Bar"":""Narf Zort!"",""Baz"":{""X"":1,""Y"":2,""Z"":3},""Jazz"":[1,2,3,4,5]}"));

			// "P" = Prettified (=> ToJsonIndented)
			Assert.That(num.ToString("P"), Is.EqualTo("123"));
			Assert.That(flag.ToString("P"), Is.EqualTo("true"));
			Assert.That(txt.ToString("P"), Is.EqualTo(@"""Hello\""World"""));
			Assert.That(arr.ToString("P"), Is.EqualTo("[\r\n\t1,\r\n\t2,\r\n\t3,\r\n\t4,\r\n\t5,\r\n\t6,\r\n\t7,\r\n\t8,\r\n\t9,\r\n\t10,\r\n\t11,\r\n\t12,\r\n\t13,\r\n\t14,\r\n\t15,\r\n\t16,\r\n\t17,\r\n\t18,\r\n\t19,\r\n\t20\r\n]"));
			Assert.That(obj.ToString("P"), Is.EqualTo("{\r\n\t\"Foo\": 123,\r\n\t\"Bar\": \"Narf Zort!\",\r\n\t\"Baz\": {\r\n\t\t\"X\": 1,\r\n\t\t\"Y\": 2,\r\n\t\t\"Z\": 3\r\n\t},\r\n\t\"Jazz\": [\r\n\t\t1,\r\n\t\t2,\r\n\t\t3,\r\n\t\t4,\r\n\t\t5\r\n\t]\r\n}"));

			// "Q" = Quick (=> GetCompactRepresentation)
			Assert.That(num.ToString("Q"), Is.EqualTo("123"));
			Assert.That(flag.ToString("Q"), Is.EqualTo("true"));
			Assert.That(txt.ToString("Q"), Is.EqualTo(@"'Hello""World'"));
			Assert.That(arr.ToString("Q"), Is.EqualTo("[ 1, 2, 3, 4, /* … 16 more */ ]"));
			Assert.That(obj.ToString("Q"), Is.EqualTo("{ Foo: 123, Bar: 'Narf Zort!', Baz: { X: 1, Y: 2, Z: 3 }, Jazz: [ 1, 2, 3, /* … 2 more */ ] }"));
			Assert.That(JsonArray.Create(1, 2, 3, 4).ToString("Q"), Is.EqualTo("[ 1, 2, 3, 4 ]"));
			Assert.That(JsonArray.Create(1, 2, 3, 4, 5).ToString("Q"), Is.EqualTo("[ 1, 2, 3, 4, 5 ]"));
			Assert.That(JsonArray.Create(1, 2, 3, 4, 5, 6).ToString("Q"), Is.EqualTo("[ 1, 2, 3, 4, /* … 2 more */ ]"));
			Assert.That(JsonArray.FromValues(Enumerable.Range(1, 60)).ToString("Q"), Is.EqualTo("[ 1, 2, 3, 4, /* … 56 more */ ]"));
			Assert.That(
				JsonArray.Create(
					"This is a test of the emergency broadcast system!",
					JsonArray.Create("This is a test of the emergency broadcast system!"),
					JsonArray.Create(JsonArray.Create("This is a test of the emergency broadcast system!")),
					JsonArray.Create(JsonArray.Create(JsonArray.Create("This is a test of the emergency broadcast system!")))
				).ToString("Q"),
				Is.EqualTo("[ 'This is a test of the emergency broadcast system!', [ 'This is a test of[…]broadcast system!' ], [ [ 'This is[…]system!' ] ], [ [ [ '…' ] ] ] ]")
			);
			Assert.That(
				JsonArray.Create(
					JsonArray.Create(1, 2, 3),
					JsonArray.FromValues(Enumerable.Range(1, 60)),
					JsonArray.Create(JsonArray.Create(1, 2, 3), JsonArray.FromValues(Enumerable.Range(1, 60)))
				).ToString("Q"),
				Is.EqualTo("[ [ 1, 2, 3 ], [ 1, 2, 3, /* … 57 more */ ], [ [ 1, 2, 3 ], [ /* 60 Numbers */ ] ] ]")
			);

			// "J" = Javascript
			Assert.That(num.ToString("J"), Is.EqualTo("123"));
			Assert.That(flag.ToString("J"), Is.EqualTo("true"));
			Assert.That(txt.ToString("J"), Is.EqualTo(@"'Hello\x22World'"));
			Assert.That(arr.ToString("J"), Is.EqualTo("[ 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20 ]"));
			Assert.That(obj.ToString("J"), Is.EqualTo(@"{ Foo: 123, Bar: 'Narf Zort\x21', Baz: { X: 1, Y: 2, Z: 3 }, Jazz: [ 1, 2, 3, 4, 5 ] }"));
		}

		[Test]
		public void Test_JsonValue_As_Of_T()
		{
			//Value Type
			Assert.That(JsonNumber.Return(123).As<int>(), Is.InstanceOf<int>().And.EqualTo(123));
			Assert.That(JsonNumber.Return(123).As<int>(456), Is.InstanceOf<int>().And.EqualTo(123));
			Assert.That(JsonString.Return("123").As<int>(), Is.InstanceOf<int>().And.EqualTo(123));
			Assert.That(JsonString.Return("123").As<int>(456), Is.InstanceOf<int>().And.EqualTo(123));
			Assert.That(JsonNull.Null.As<int>(), Is.InstanceOf<int>().And.EqualTo(0));
			Assert.That(JsonNull.Null.As<int>(456), Is.InstanceOf<int>().And.EqualTo(456));

			//Nullable Type
			Assert.That(JsonNumber.Return(123).As<int?>(), Is.Not.Null.And.InstanceOf<int>().And.EqualTo(123));
			Assert.That(JsonNumber.Return(123).As<int?>(456), Is.Not.Null.And.InstanceOf<int>().And.EqualTo(123));
			Assert.That(JsonString.Return("123").As<int?>(), Is.Not.Null.And.InstanceOf<int>().And.EqualTo(123));
			Assert.That(JsonString.Return("123").As<int?>(456), Is.Not.Null.And.InstanceOf<int>().And.EqualTo(123));
			Assert.That(JsonNull.Null.As<int?>(), Is.Null);
			Assert.That(JsonNull.Null.As<int?>(456), Is.Not.Null.And.InstanceOf<int>().And.EqualTo(456));
			Assert.That(JsonNull.Null.As<int?>(null), Is.Null);

			//Reference Primitive Type
			Assert.That(JsonNumber.Return(123).As<string>(), Is.Not.Null.And.EqualTo("123"));
			Assert.That(JsonNumber.Return(123).As<string>("foo"), Is.Not.Null.And.EqualTo("123"));
			Assert.That(JsonString.Return("123").As<string>(), Is.Not.Null.And.EqualTo("123"));
			Assert.That(JsonString.Return("123").As<string>("foo"), Is.Not.Null.And.EqualTo("123"));
			Assert.That(JsonNull.Null.As<string>(), Is.Null);
			Assert.That(JsonNull.Null.As<string>("foo"), Is.Not.Null.And.EqualTo("foo"));
			Assert.That(JsonNull.Null.As<string>(null), Is.Null);

			//Value Type Array
			Assert.That(JsonArray.Create(1, 2, 3).As<int[]>(), Is.Not.Null.And.EqualTo(new [] { 1, 2, 3 }));
			Assert.That(JsonNull.Null.As<int[]>(), Is.Null);
			Assert.That(JsonNull.Null.As<int[]>(new [] { 4, 5, 6 }), Is.Not.Null.And.EqualTo(new[] { 4, 5, 6 }));

			//Ref Type Array
			Assert.That(JsonArray.Create("a", "b", "c").As<string[]>(), Is.Not.Null.And.EqualTo(new[] { "a", "b", "c" }));
			Assert.That(JsonNull.Null.As<string[]>(), Is.Null);
			Assert.That(JsonNull.Null.As<string[]>(new[] { "foo" }), Is.Not.Null.And.EqualTo(new[] { "foo" }));

			//Value Type List
			Assert.That(JsonArray.Create(1, 2, 3).As<List<int>>(), Is.Not.Null.And.EqualTo(new[] { 1, 2, 3 }));
			Assert.That(JsonNull.Null.As<List<int>>(), Is.Null);
			Assert.That(JsonNull.Null.As<List<int>>(new List<int> { 4, 5, 6 }), Is.Not.Null.And.EqualTo(new[] { 4, 5, 6 }));

			//Ref Type List
			Assert.That(JsonArray.Create("a", "b", "c").As<List<string>>(), Is.Not.Null.And.EqualTo(new[] { "a", "b", "c" }));
			Assert.That(JsonNull.Null.As<List<string>>(), Is.Null);
			Assert.That(JsonNull.Null.As<List<string>>(new List<string> { "foo" }), Is.Not.Null.And.EqualTo(new[] { "foo" }));

			//Format Exceptions
			Assert.That(() => JsonString.Return("foo").As<int>(), Throws.InstanceOf<FormatException>());
			Assert.That(() => JsonArray.Create("foo").As<int[]>(), Throws.InstanceOf<FormatException>());
			Assert.That(() => JsonArray.Create("foo").As<List<int>>(), Throws.InstanceOf<FormatException>());
		}

		[Test]
		public void Test_JsonFromValue_NodaTime_Types()
		{
			// Instant
			{
				var instant = NodaTime.Instant.FromDateTimeOffset(new DateTimeOffset(new DateTime(2015, 7, 17), new TimeSpan(2, 0, 0)));
				// 17 Juillet 2015 0h00, GMT+2 => 16 Juillet 2015 22h00 UTC
				var json = JsonValue.FromValue<NodaTime.Instant>(instant);
				Assert.That(json.Type, Is.EqualTo(JsonType.String));
				Assert.That(((JsonString) json).Value, Is.EqualTo("2015-07-16T22:00:00Z"));
				Assert.That(json.ToInstant(), Is.EqualTo(instant));
				Assert.That(json.As<NodaTime.Instant>(), Is.EqualTo(instant));
			}

			// Duration
			{
				var duration = NodaTime.Duration.FromHours(1);
				var json = JsonValue.FromValue<NodaTime.Duration>(duration);
				Assert.That(json.Type, Is.EqualTo(JsonType.Number));
				Assert.That(((JsonNumber)json).ToDouble(), Is.EqualTo(3600.0));
				Assert.That(json.ToDuration(), Is.EqualTo(duration));
				Assert.That(json.As<NodaTime.Duration>(), Is.EqualTo(duration));
			}

			//TODO: autre types
		}

		[Test]
		public void Test_JsonSerialize_DateTime_Types_ToMicrosoftFormat()
		{
			var settings = CrystalJsonSettings.Json.WithMicrosoftDates();

			// on établi les bases...
			Assume.That(typeof(DateTime).IsPrimitive, Is.False);
			Assume.That(typeof(DateTime).IsValueType, Is.True);
			long unixEpoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).Ticks;
			Assume.That(unixEpoch, Is.EqualTo(621355968000000000));

			TimeSpan utcOffset = DateTimeOffset.Now.Offset;

			// JSON ne spécifie pas le format de date. On va utiliser le même que Microsoft (cad "\/Date(xxxx)\/").
			// ATTENTION! Les dates sont toujours serializées en UTC ! Le problème c'est que 99.999% des DateTime qu'on va trouver dans un objet sont en LocalTime, qui varie avec les heures d'hiver/été

			// corner cases
			Assert.That(CrystalJson.Serialize(new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc), settings), Is.EqualTo("\"\\/Date(0)\\/\""));
			// note: DateTime.MinValue est en local time (wtf??), mais on le force en UTC pour éviter les même problèmes que le DataContractJsonSerializer (qui plante si on habite a l'est de GMT !)
			Assert.That(CrystalJson.Serialize(new DateTime(0, DateTimeKind.Utc), settings), Is.EqualTo("\"\\/Date(-62135596800000)\\/\""));
			Assert.That(CrystalJson.Serialize(DateTime.MinValue, settings), Is.EqualTo("\"\\/Date(-62135596800000)\\/\""));
			// idem pour MaxValue
			Assert.That(CrystalJson.Serialize(new DateTime(3155378975999999999, DateTimeKind.Utc), settings), Is.EqualTo("\"\\/Date(253402300799999)\\/\""));
			Assert.That(CrystalJson.Serialize(DateTime.MaxValue, settings), Is.EqualTo("\"\\/Date(253402300799999)\\/\""));

			// Now (UTC)
			DateTime utcNow = DateTime.UtcNow;
			Assert.That(utcNow.Kind, Is.EqualTo(DateTimeKind.Utc));
			long ticks = (utcNow.Ticks - unixEpoch) / 10000;
			Assert.That(CrystalJson.Serialize(utcNow, settings), Is.EqualTo("\"\\/Date(" + ticks.ToString() + ")\\/\""));

			// Now (local)
			DateTime localNow = DateTime.Now;
			Assert.That(localNow.Kind, Is.EqualTo(DateTimeKind.Local));
			ticks = (localNow.Ticks - unixEpoch - utcOffset.Ticks) / 10000;
			Assert.That(CrystalJson.Serialize(localNow, settings), Is.EqualTo("\"\\/Date(" + ticks.ToString() + GetTimeZoneSuffix(localNow) + ")\\/\""));

			// Local vs Unspecified vs UTC
			// * 1er Janvier 2000 = GMT + 1 car heure d'hiver
			Assert.That(CrystalJson.Serialize(new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc), settings), Is.EqualTo("\"\\/Date(946684800000)\\/\""), "2000-01-01 UTC");
			Assert.That(CrystalJson.Serialize(new DateTime(2000, 1, 1, 0, 0, 0), settings), Is.EqualTo("\"\\/Date(" + (946684800000 - 1 * 3600 * 1000).ToString() + "+0100)\\/\""), "2000-01-01 GMT+1 (Paris)");
			Assert.That(CrystalJson.Serialize(new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Local), settings), Is.EqualTo("\"\\/Date(" + (946684800000 - 1 * 3600 * 1000).ToString() + "+0100)\\/\""), "2000-01-01 GMT+1 (Paris)");
			// * 1er Août 2000 = GMT + 2 car heure d'été
			Assert.That(CrystalJson.Serialize(new DateTime(2000, 9, 1, 0, 0, 0, DateTimeKind.Utc), settings), Is.EqualTo("\"\\/Date(967766400000)\\/\""), "2000-09-01 UTC");
			Assert.That(CrystalJson.Serialize(new DateTime(2000, 9, 1, 0, 0, 0), settings), Is.EqualTo("\"\\/Date(" + (967766400000 - 2 * 3600 * 1000).ToString() + "+0200)\\/\""), "2000-08-01 GMT+2 (Paris, DST)");
			Assert.That(CrystalJson.Serialize(new DateTime(2000, 9, 1, 0, 0, 0, DateTimeKind.Local), settings), Is.EqualTo("\"\\/Date(" + (967766400000 - 2 * 3600 * 1000).ToString() + "+0200)\\/\""), "2000-08-01 GMT+2 (Paris, DST)");

			//TODO: DateTimeOffset ?
		}

		private string ToUtcOffset(TimeSpan offset)
		{
			// note: peut être négatif! Hours et Minutes seront tt les deux négatifs
			return (offset < TimeSpan.Zero ? "-" : "+") + Math.Abs(offset.Hours).ToString("D2") + ":" + Math.Abs(offset.Minutes).ToString("D2");
		}

		[Test]
		public void Test_JsonSerialize_DateTime_Iso8601()
		{
			var settings = CrystalJsonSettings.Json.WithIso8601Dates();
			Assert.That(settings.DateFormatting, Is.EqualTo(CrystalJsonSettings.DateFormat.TimeStampIso8601));

			// MinValue: doit etre sérialisé comme une chaine vide
			// permet de gérer le cas ou on a sérialisé un DateTime.MinValue, mais qu'on désérialiser dans un Nullable<DateTime>
			Assert.That(CrystalJson.Serialize(DateTime.MinValue, settings), Is.EqualTo("\"\""));
			Assert.That(CrystalJson.Serialize(DateTime.SpecifyKind(DateTime.MinValue, DateTimeKind.Utc), settings), Is.EqualTo("\"\""));
			Assert.That(CrystalJson.Serialize(DateTime.SpecifyKind(DateTime.MinValue, DateTimeKind.Local), settings), Is.EqualTo("\"\""));

			// MaxValue: ne doit pas mentiner de timezone
			Assert.That(CrystalJson.Serialize(DateTime.MaxValue, settings), Is.EqualTo("\"9999-12-31T23:59:59.9999999\""));
			Assert.That(CrystalJson.Serialize(DateTime.SpecifyKind(DateTime.MaxValue, DateTimeKind.Utc), settings), Is.EqualTo("\"9999-12-31T23:59:59.9999999\""), "DateTime.MaxValue should not specify UTC 'Z'");
			Assert.That(CrystalJson.Serialize(DateTime.SpecifyKind(DateTime.MaxValue, DateTimeKind.Local), settings), Is.EqualTo("\"9999-12-31T23:59:59.9999999\""), "DateTime.MaxValue should not specify local TimeZone");

			// Unix Epoch
			Assert.That(CrystalJson.Serialize(new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc), settings), Is.EqualTo("\"1970-01-01T00:00:00Z\""));

			// Unspecified
			Assert.That(CrystalJson.Serialize(new DateTime(2013, 3, 11, 12, 34, 56, 768, DateTimeKind.Unspecified), settings), Is.EqualTo("\"2013-03-11T12:34:56.7680000\""), "Les dates Unspecified ne doivent avoir ni 'Z' ni timezone");

			// UTC
			Assert.That(CrystalJson.Serialize(new DateTime(2013, 3, 11, 12, 34, 56, 768, DateTimeKind.Utc), settings), Is.EqualTo("\"2013-03-11T12:34:56.7680000Z\""), "Les dates UTC doivent finir par 'Z'");

			// Local
			var dt = new DateTime(2013, 3, 11, 12, 34, 56, 768, DateTimeKind.Local);
			Assert.That(CrystalJson.Serialize(dt, settings), Is.EqualTo("\"2013-03-11T12:34:56.7680000" + ToUtcOffset(new DateTimeOffset(dt).Offset) + "\""), "Les dates Local doivent avoir la timezone");

			// Now (UTC)
			DateTime utcNow = DateTime.UtcNow;
			Assert.That(CrystalJson.Serialize(utcNow, settings), Is.EqualTo("\"" + utcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffffff") + "Z\""), "DateTime.UtcNow doit finir par Z");

			// Now (local)
			DateTime localNow = DateTime.Now;
			Assert.That(CrystalJson.Serialize(localNow, settings), Is.EqualTo("\"" + localNow.ToString("yyyy-MM-ddTHH:mm:ss.fffffff") + ToUtcOffset(DateTimeOffset.Now.Offset) + "\""), "DateTime.Now doit inclure la TimeZone");

			// Local vs Unspecified vs UTC
			// IMPORTANT: ce test ne marche que si on est dans la timezone "Romance Standard Time" (Paris, Bruxelles, ...)
			// Paris: GMT+1 l'hivers, GMT+2 l'état

			// * 1er Janvier 2000 = GMT + 1 car heure d'hiver
			Assert.That(CrystalJson.Serialize(new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc), settings), Is.EqualTo("\"2000-01-01T00:00:00Z\""), "2000-01-01 UTC");
			Assert.That(CrystalJson.Serialize(new DateTime(2000, 1, 1, 0, 0, 0), settings), Is.EqualTo("\"2000-01-01T00:00:00\""), "2000-01-01 (unspecified)");
			Assert.That(CrystalJson.Serialize(new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Local), settings), Is.EqualTo("\"2000-01-01T00:00:00+01:00\""), "2000-01-01 GMT+1 (Paris)");

			// * 1er Septembre 2000 = GMT + 2 car heure d'été
			Assert.That(CrystalJson.Serialize(new DateTime(2000, 9, 1, 0, 0, 0, DateTimeKind.Utc), settings), Is.EqualTo("\"2000-09-01T00:00:00Z\""), "2000-09-01 UTC");
			Assert.That(CrystalJson.Serialize(new DateTime(2000, 9, 1, 0, 0, 0), settings), Is.EqualTo("\"2000-09-01T00:00:00\""), "2000-09-01 (unspecified)");
			Assert.That(CrystalJson.Serialize(new DateTime(2000, 9, 1, 0, 0, 0, DateTimeKind.Local), settings), Is.EqualTo("\"2000-09-01T00:00:00+02:00\""), "2000-09-01 GMT+2 (Paris, DST)");
		}

		[Test]
		public void Test_JsonSerialize_DateTimeOffset_Iso8601()
		{
			var settings = CrystalJsonSettings.Json.WithIso8601Dates();
			Assert.That(settings.DateFormatting, Is.EqualTo(CrystalJsonSettings.DateFormat.TimeStampIso8601));

			// MinValue: doit etre sérialisé comme une chaine vide
			// permet de gérer le cas ou on a sérialisé un DateTimeOffset.MinValue, mais qu'on désérialiser dans un Nullable<DateTimeOffset>
			Assert.That(CrystalJson.Serialize(DateTimeOffset.MinValue, settings), Is.EqualTo("\"\""));

			// MaxValue: ne doit pas mentiner de timezone
			Assert.That(CrystalJson.Serialize(DateTimeOffset.MaxValue, settings), Is.EqualTo("\"9999-12-31T23:59:59.9999999\""));

			// Unix Epoch
			Assert.That(CrystalJson.Serialize(new DateTimeOffset(new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)), settings), Is.EqualTo("\"1970-01-01T00:00:00Z\""));

			// Now (Utc, Local)
			Assert.That(CrystalJson.Serialize(new DateTimeOffset(new DateTime(2013, 3, 11, 12, 34, 56, 768, DateTimeKind.Utc)), settings), Is.EqualTo("\"2013-03-11T12:34:56.7680000Z\""));
			Assert.That(CrystalJson.Serialize(new DateTimeOffset(new DateTime(2013, 3, 11, 12, 34, 56, 768, DateTimeKind.Local)), settings), Is.EqualTo("\"2013-03-11T12:34:56.7680000" + ToUtcOffset(TimeZoneInfo.Local.BaseUtcOffset) + "\""));

			// TimeZones
			Assert.That(CrystalJson.Serialize(new DateTimeOffset(2013, 3, 11, 12, 34, 56, 768, TimeSpan.Zero), settings), Is.EqualTo("\"2013-03-11T12:34:56.7680000Z\""));
			Assert.That(CrystalJson.Serialize(new DateTimeOffset(2013, 3, 11, 12, 34, 56, 768, TimeSpan.FromHours(1)), settings), Is.EqualTo("\"2013-03-11T12:34:56.7680000+01:00\""));
			Assert.That(CrystalJson.Serialize(new DateTimeOffset(2013, 3, 11, 12, 34, 56, 768, TimeSpan.FromHours(-1)), settings), Is.EqualTo("\"2013-03-11T12:34:56.7680000-01:00\""));
			Assert.That(CrystalJson.Serialize(new DateTimeOffset(2013, 3, 11, 12, 34, 56, 768, TimeSpan.FromMinutes(11 * 60 + 30)), settings), Is.EqualTo("\"2013-03-11T12:34:56.7680000+11:30\""));
			Assert.That(CrystalJson.Serialize(new DateTimeOffset(2013, 3, 11, 12, 34, 56, 768, TimeSpan.FromMinutes(-11 * 60 - 30)), settings), Is.EqualTo("\"2013-03-11T12:34:56.7680000-11:30\""));

			// Now (UTC)
			var utcNow = DateTimeOffset.Now.ToUniversalTime();
			Assert.That(CrystalJson.Serialize(utcNow, settings), Is.EqualTo("\"" + utcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffffff") + "Z\""), "DateTime.UtcNow doit finir par Z");

			// Now (local)
			var localNow = DateTimeOffset.Now;
			Assert.That(CrystalJson.Serialize(localNow, settings), Is.EqualTo("\"" + localNow.ToString("yyyy-MM-ddTHH:mm:ss.fffffff") + ToUtcOffset(localNow.Offset) + "\""), "DateTime.Now doit inclure la TimeZone");
			//note: ce test ne fonctionne pas si le serveur tourne en TZ = GMT+0 !

			// Local vs Unspecified vs UTC
			// Paris: GMT+1 l'hivers, GMT+2 l'état

			// * 1er Janvier 2000 = GMT + 1 car heure d'hiver
			Assert.That(CrystalJson.Serialize(new DateTimeOffset(2000, 1, 1, 0, 0, 0, TimeSpan.Zero), settings), Is.EqualTo("\"2000-01-01T00:00:00Z\""), "2000-01-01 UTC");
			Assert.That(CrystalJson.Serialize(new DateTimeOffset(2000, 1, 1, 0, 0, 0, TimeSpan.FromHours(1)), settings), Is.EqualTo("\"2000-01-01T00:00:00+01:00\""), "2000-01-01 GMT+1 (Paris)");

			// * 1er Septembre 2000 = GMT + 2 car heure d'été
			Assert.That(CrystalJson.Serialize(new DateTimeOffset(2000, 9, 1, 0, 0, 0, TimeSpan.Zero), settings), Is.EqualTo("\"2000-09-01T00:00:00Z\""), "2000-09-01 UTC");
			Assert.That(CrystalJson.Serialize(new DateTimeOffset(2000, 9, 1, 0, 0, 0, TimeSpan.FromHours(2)), settings), Is.EqualTo("\"2000-09-01T00:00:00+02:00\""), "2000-09-01 GMT+2 (Paris, DST)");
		}

		[Test]
		public void Test_JsonSerialize_DateTime_Types_ToJavaScriptFormat()
		{
			// on établi les bases...
			Assume.That(typeof(DateTime).IsPrimitive, Is.False);
			Assume.That(typeof(DateTime).IsValueType, Is.True);
			long unixEpoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).Ticks;
			Assume.That(unixEpoch, Is.EqualTo(621355968000000000));

			// JSON ne spécifie pas le format de date. On va utiliser le même que Microsoft (cad "\/Date(xxxx)\/").
			// ATTENTION! Les dates sont toujours serializées en UTC ! Le problème c'est que 99.999% des DateTime qu'on va trouver dans un objet sont en LocalTime, qui varie avec les heures d'hiver/été

			// corner cases
			Assert.That(CrystalJson.Serialize(new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc), CrystalJsonSettings.JavaScript), Is.EqualTo("new Date(0)"));
			// note: DateTime.MinValue est en local time (wtf??), mais on le force en UTC pour éviter les même problèmes que le DataContractJsonSerializer (qui plante si on habite a l'est de GMT !)
			Assert.That(CrystalJson.Serialize(new DateTime(0, DateTimeKind.Utc), CrystalJsonSettings.JavaScript), Is.EqualTo("new Date(-62135596800000)"));
			Assert.That(CrystalJson.Serialize(DateTime.MinValue, CrystalJsonSettings.JavaScript), Is.EqualTo("new Date(-62135596800000)"));
			// idem pour MaxValue
			Assert.That(CrystalJson.Serialize(new DateTime(3155378975999999999, DateTimeKind.Utc), CrystalJsonSettings.JavaScript), Is.EqualTo("new Date(253402300799999)"));
			Assert.That(CrystalJson.Serialize(DateTime.MaxValue, CrystalJsonSettings.JavaScript), Is.EqualTo("new Date(253402300799999)"));

			// Now (UTC)
			DateTime utcNow = DateTime.UtcNow;
			Assert.That(utcNow.Kind, Is.EqualTo(DateTimeKind.Utc));
			long ticks = (utcNow.Ticks - unixEpoch) / 10000;
			Assert.That(CrystalJson.Serialize(utcNow, CrystalJsonSettings.JavaScript), Is.EqualTo("new Date(" + ticks.ToString() + ")"));

			// Now (local)
			DateTime localNow = DateTime.Now;
			Assert.That(localNow.Kind, Is.EqualTo(DateTimeKind.Local));
			TimeSpan utcOffset = new DateTimeOffset(localNow).Offset;
			ticks = (localNow.Ticks - unixEpoch - utcOffset.Ticks) / 10000;
			Assert.That(CrystalJson.Serialize(localNow, CrystalJsonSettings.JavaScript), Is.EqualTo("new Date(" + ticks.ToString() + ")"));

			// Local vs Unspecified vs UTC
			// * 1er Janvier 2000 = GMT + 1 car heure d'hiver
			Assert.That(CrystalJson.Serialize(new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc), CrystalJsonSettings.JavaScript), Is.EqualTo("new Date(946684800000)"), "2000-01-01 UTC");
			Assert.That(CrystalJson.Serialize(new DateTime(2000, 1, 1, 0, 0, 0), CrystalJsonSettings.JavaScript), Is.EqualTo("new Date(" + (946684800000 - 1 * 3600 * 1000).ToString() + ")"), "2000-01-01 GMT+1 (Paris)");
			Assert.That(CrystalJson.Serialize(new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Local), CrystalJsonSettings.JavaScript), Is.EqualTo("new Date(" + (946684800000 - 1 * 3600 * 1000).ToString() + ")"), "2000-01-01 GMT+1 (Paris)");
			// * 1er Août 2000 = GMT + 2 car heure d'été
			Assert.That(CrystalJson.Serialize(new DateTime(2000, 9, 1, 0, 0, 0, DateTimeKind.Utc), CrystalJsonSettings.JavaScript), Is.EqualTo("new Date(967766400000)"), "2000-09-01 UTC");
			Assert.That(CrystalJson.Serialize(new DateTime(2000, 9, 1, 0, 0, 0), CrystalJsonSettings.JavaScript), Is.EqualTo("new Date(" + (967766400000 - 2 * 3600 * 1000).ToString() + ")"), "2000-08-01 GMT+2 (Paris, DST)");
			Assert.That(CrystalJson.Serialize(new DateTime(2000, 9, 1, 0, 0, 0, DateTimeKind.Local), CrystalJsonSettings.JavaScript), Is.EqualTo("new Date(" + (967766400000 - 2 * 3600 * 1000).ToString() + ")"), "2000-08-01 GMT+2 (Paris, DST)");

			//TODO: DateTimeOffset ?
		}

		[Test]
		public void Test_JsonSerialize_TimeSpan()
		{
			// TimeSpan
			Assert.That(CrystalJson.Serialize(TimeSpan.Zero), Is.EqualTo("0"));
			Assert.That(CrystalJson.Serialize(TimeSpan.FromSeconds(1)), Is.EqualTo("1"));
			Assert.That(CrystalJson.Serialize(TimeSpan.FromSeconds(1.5)), Is.EqualTo("1.5"));
			Assert.That(CrystalJson.Serialize(TimeSpan.FromMinutes(1)), Is.EqualTo("60"));
			Assert.That(CrystalJson.Serialize(TimeSpan.FromMilliseconds(1)), Is.EqualTo("0.001"));
			Assert.That(CrystalJson.Serialize(TimeSpan.FromTicks(1)), Is.EqualTo("1E-07"));
		}

		[Test]
		public void Test_JsonSerializes_EnumTypes()
		{
			// on établi les bases...
			Assume.That(typeof(DummyJsonEnum).IsPrimitive, Is.False);
			Assume.That(typeof(DummyJsonEnum).IsEnum, Is.True);

			// As Integers

			// enum systemes
			Assert.That(CrystalJson.Serialize(MidpointRounding.AwayFromZero), Is.EqualTo("1"));
			Assert.That(CrystalJson.Serialize(DayOfWeek.Friday), Is.EqualTo("5"));
			// enum custom
			Assert.That(CrystalJson.Serialize(DummyJsonEnum.None), Is.EqualTo("0"));
			Assert.That(CrystalJson.Serialize(DummyJsonEnum.Foo), Is.EqualTo("1"));
			Assert.That(CrystalJson.Serialize(DummyJsonEnum.Bar), Is.EqualTo("42"));
			Assert.That(CrystalJson.Serialize((DummyJsonEnum)123), Is.EqualTo("123"));
			// enum flags
			Assert.That(CrystalJson.Serialize(DummyJsonEnumFlags.None), Is.EqualTo("0"));
			Assert.That(CrystalJson.Serialize(DummyJsonEnumFlags.Foo), Is.EqualTo("1"));
			Assert.That(CrystalJson.Serialize(DummyJsonEnumFlags.Bar), Is.EqualTo("2"));
			Assert.That(CrystalJson.Serialize(DummyJsonEnumFlags.Narf), Is.EqualTo("4"));
			Assert.That(CrystalJson.Serialize(DummyJsonEnumFlags.Foo | DummyJsonEnumFlags.Bar), Is.EqualTo("3"));
			Assert.That(CrystalJson.Serialize(DummyJsonEnumFlags.Bar | DummyJsonEnumFlags.Narf), Is.EqualTo("6"));
			Assert.That(CrystalJson.Serialize((DummyJsonEnumFlags)255), Is.EqualTo("255"));

			// As Strings

			var settings = CrystalJsonSettings.Json.WithEnumAsStrings();

			// enum systemes
			Assert.That(CrystalJson.Serialize(MidpointRounding.AwayFromZero, settings), Is.EqualTo("\"AwayFromZero\""));
			Assert.That(CrystalJson.Serialize(DayOfWeek.Friday, settings), Is.EqualTo("\"Friday\""));
			// enum custom
			Assert.That(CrystalJson.Serialize(DummyJsonEnum.None, settings), Is.EqualTo("\"None\""));
			Assert.That(CrystalJson.Serialize(DummyJsonEnum.Foo, settings), Is.EqualTo("\"Foo\""));
			Assert.That(CrystalJson.Serialize(DummyJsonEnum.Bar, settings), Is.EqualTo("\"Bar\""));
			Assert.That(CrystalJson.Serialize((DummyJsonEnum)123, settings), Is.EqualTo("\"123\""));
			// enum flags
			Assert.That(CrystalJson.Serialize(DummyJsonEnumFlags.None, settings), Is.EqualTo("\"None\""));
			Assert.That(CrystalJson.Serialize(DummyJsonEnumFlags.Foo, settings), Is.EqualTo("\"Foo\""));
			Assert.That(CrystalJson.Serialize(DummyJsonEnumFlags.Bar, settings), Is.EqualTo("\"Bar\""));
			Assert.That(CrystalJson.Serialize(DummyJsonEnumFlags.Narf, settings), Is.EqualTo("\"Narf\""));
			//TODO: comment gérer correctement les flags multiples ?
			Assert.That(CrystalJson.Serialize(DummyJsonEnumFlags.Foo | DummyJsonEnumFlags.Bar, settings), Is.EqualTo("\"Foo, Bar\""));
			Assert.That(CrystalJson.Serialize(DummyJsonEnumFlags.Bar | DummyJsonEnumFlags.Narf, settings), Is.EqualTo("\"Bar, Narf\""));
			Assert.That(CrystalJson.Serialize((DummyJsonEnumFlags)255, settings), Is.EqualTo("\"255\""));

			// Duplicate Values

			settings = CrystalJsonSettings.Json.WithEnumAsStrings();
			Assert.That(CrystalJson.Serialize(DummyJsonEnumTypo.Bar, settings), Is.EqualTo("\"Bar\""));
			Assert.That(CrystalJson.Serialize(DummyJsonEnumTypo.Barrh, settings), Is.EqualTo("\"Bar\""));
			Assert.That(CrystalJson.Serialize((DummyJsonEnumTypo)2, settings), Is.EqualTo("\"Bar\""));
		}

		[Test]
		public void Test_JsonSerialize_Structs()
		{
			// check les bases
			Assume.That(typeof(DummyJsonStruct).IsValueType, Is.True);
			Assume.That(typeof(DummyJsonStruct).IsClass, Is.False);

			// empty struct
			var x = new DummyJsonStruct();
			string expected = "{ \"Valid\": false, \"Index\": 0, \"Size\": 0, \"Height\": 0, \"Amount\": 0, \"Created\": \"\", \"State\": 0, \"RatioOfStuff\": 0 }";
			string jsonText = CrystalJson.Serialize(x);
			Assert.That(jsonText, Is.EqualTo(expected), "Serialize(EMPTY, JSON)");
			expected = "{ Valid: false, Index: 0, Size: 0, Height: 0, Amount: 0, Created: new Date(-62135596800000), State: 0, RatioOfStuff: 0 }";
			jsonText = CrystalJson.Serialize(x, CrystalJsonSettings.JavaScript);
			Assert.That(jsonText, Is.EqualTo(expected), "Serialize(EMPTY, JS)");

			// avec les null visibles
			expected = "{ \"Valid\": false, \"Name\": null, \"Index\": 0, \"Size\": 0, \"Height\": 0, \"Amount\": 0, \"Created\": \"\", \"Modified\": null, \"State\": 0, \"RatioOfStuff\": 0 }";
			jsonText = CrystalJson.Serialize(x, CrystalJsonSettings.Json.WithNullMembers());
			Assert.That(jsonText, Is.EqualTo(expected), "Serialize(EMPTY, JSON+ShowNull)");
			expected = "{ Valid: false, Name: null, Index: 0, Size: 0, Height: 0, Amount: 0, Created: new Date(-62135596800000), Modified: null, State: 0, RatioOfStuff: 0 }";
			jsonText = CrystalJson.Serialize(x, CrystalJsonSettings.JavaScript.WithNullMembers());
			Assert.That(jsonText, Is.EqualTo(expected), "Serialize(EMPTY, JS+ShowNull)");

			// en masquant les valeure vides
			expected = "{ }";
			jsonText = CrystalJson.Serialize(x, CrystalJsonSettings.Json.WithoutDefaultValues());
			Assert.That(jsonText, Is.EqualTo(expected), "Serialize(EMPTY, JSON+HideDefaults)");
			expected = "{ }";
			jsonText = CrystalJson.Serialize(x, CrystalJsonSettings.JavaScript.WithoutDefaultValues());
			Assert.That(jsonText, Is.EqualTo(expected), "Serialize(EMPTY, JS+HideDefaults)");

			// en mode compact
			expected = "{\"Valid\":false,\"Index\":0,\"Size\":0,\"Height\":0,\"Amount\":0,\"Created\":\"\",\"State\":0,\"RatioOfStuff\":0}";
			jsonText = CrystalJson.Serialize(x, CrystalJsonSettings.JsonCompact);
			Assert.That(jsonText, Is.EqualTo(expected), "Serialize(EMPTY, JSON+Compact)");
			expected = "{Valid:false,Index:0,Size:0,Height:0,Amount:0,Created:new Date(-62135596800000),State:0,RatioOfStuff:0}";
			jsonText = CrystalJson.Serialize(x, CrystalJsonSettings.JavaScript.Compacted());
			Assert.That(jsonText, Is.EqualTo(expected), "Serialize(EMPTY, JS+Compact)");

			// en mode indenté
			expected =
				"{\r\n" +
				"\t\"Valid\": false,\r\n" +
				"\t\"Index\": 0,\r\n" +
				"\t\"Size\": 0,\r\n" +
				"\t\"Height\": 0,\r\n" +
				"\t\"Amount\": 0,\r\n" +
				"\t\"Created\": \"\",\r\n" +
				"\t\"State\": 0,\r\n" +
				"\t\"RatioOfStuff\": 0\r\n" +
				"}";
			jsonText = CrystalJson.Serialize(x, CrystalJsonSettings.JsonIndented);
			Assert.That(jsonText, Is.EqualTo(expected), "Serialize(X, JSON+Indented)");
			expected =
				"{\r\n" +
				"\tValid: false,\r\n" +
				"\tIndex: 0,\r\n" +
				"\tSize: 0,\r\n" +
				"\tHeight: 0,\r\n" +
				"\tAmount: 0,\r\n" +
				"\tCreated: new Date(-62135596800000),\r\n" +
				"\tState: 0,\r\n" +
				"\tRatioOfStuff: 0\r\n" +
				"}";
			jsonText = CrystalJson.Serialize(x, CrystalJsonSettings.JavaScriptIndented);
			Assert.That(jsonText, Is.EqualTo(expected), "Serialize(X, JS+Indented)");

			// filled with values
			x.Valid = true;
			x.Name = "James Bond";
			x.Index = 7;
			x.Size = 123456789;
			x.Height = 1.8f;
			x.Amount = 0.07d;
			x.Created = new DateTime(1968, 5, 8);
			x.State = DummyJsonEnum.Foo;

			expected = "{ \"Valid\": true, \"Name\": \"James Bond\", \"Index\": 7, \"Size\": 123456789, \"Height\": 1.8, \"Amount\": 0.07, \"Created\": \"1968-05-08T00:00:00\", \"State\": 1, \"RatioOfStuff\": 8641975.23 }";
			jsonText = CrystalJson.Serialize(x);
			Assert.That(jsonText, Is.EqualTo(expected), "Serialize(BOND, JSON)");
			expected = "{ Valid: true, Name: 'James Bond', Index: 7, Size: 123456789, Height: 1.8, Amount: 0.07, Created: new Date(-52106400000), State: 1, RatioOfStuff: 8641975.23 }";
			jsonText = CrystalJson.Serialize(x, CrystalJsonSettings.JavaScript);
			Assert.That(jsonText, Is.EqualTo(expected), "Serialize(BOND, JS)");

			// en mode compact
			expected = "{\"Valid\":true,\"Name\":\"James Bond\",\"Index\":7,\"Size\":123456789,\"Height\":1.8,\"Amount\":0.07,\"Created\":\"1968-05-08T00:00:00\",\"State\":1,\"RatioOfStuff\":8641975.23}";
			jsonText = CrystalJson.Serialize(x, CrystalJsonSettings.Json.Compacted());
			Assert.That(jsonText, Is.EqualTo(expected), "Serialize(BOND, JSON+Compact)");
			expected = "{Valid:true,Name:'James Bond',Index:7,Size:123456789,Height:1.8,Amount:0.07,Created:new Date(-52106400000),State:1,RatioOfStuff:8641975.23}";
			jsonText = CrystalJson.Serialize(x, CrystalJsonSettings.JavaScript.Compacted());
			Assert.That(jsonText, Is.EqualTo(expected), "Serialize(BOND, JS+Compact)");
		}

		[Test]
		public void Test_JsonSerialize_NullableTypes()
		{
			var x = new DummyNullableStruct();
			// comme tout est null, il ne doit rien y avoir dans l'objet...
			string expected = "{ }";
			string jsonText = CrystalJson.Serialize(x);
			Assert.That(jsonText, Is.EqualTo(expected), "Serialize(EMPTY,JSON)");
			jsonText = CrystalJson.Serialize(x, CrystalJsonSettings.JavaScript);
			Assert.That(jsonText, Is.EqualTo(expected), "Serialize(EMPTY,JS)");
			jsonText = CrystalJson.Serialize(x, CrystalJsonSettings.Json.WithoutDefaultValues());
			Assert.That(jsonText, Is.EqualTo(expected), "Serialize(EMPTY,JSON+HideDefaults)");
			jsonText = CrystalJson.Serialize(x, CrystalJsonSettings.JavaScript.WithoutDefaultValues());
			Assert.That(jsonText, Is.EqualTo(expected), "Serialize(EMPTY,JS+HideDefaults)");

			// par défaut tout doit etre à null
			expected = """{ "Bool": null, "Int32": null, "Int64": null, "Single": null, "Double": null, "DateTime": null, "TimeSpan": null, "Guid": null, "Enum": null, "Struct": null }""";
			jsonText = CrystalJson.Serialize(x, CrystalJsonSettings.Json.WithNullMembers());
			Assert.That(jsonText, Is.EqualTo(expected), "Serialize(EMPTY,JSON+ShowNull)");
			expected = "{ Bool: null, Int32: null, Int64: null, Single: null, Double: null, DateTime: null, TimeSpan: null, Guid: null, Enum: null, Struct: null }";
			jsonText = CrystalJson.Serialize(x, CrystalJsonSettings.JavaScript.WithNullMembers());
			Assert.That(jsonText, Is.EqualTo(expected), "Serialize(EMPTY,JS+ShowNull)");

			// on remplit les champs...
			x.Bool = true;
			x.Int32 = 123;
			x.Int64 = 123;
			x.Single = 1.23f;
			x.Double = 1.23d;
			x.Guid = new Guid("98bd4ed7-7337-4018-9551-ee0825ada7ba");
			x.DateTime = new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc);
			x.TimeSpan = TimeSpan.FromMinutes(1);
			x.Enum = DummyJsonEnum.Bar;
			x.Struct = new DummyJsonStruct(); // vide!
			expected = @"{ ""Bool"": true, ""Int32"": 123, ""Int64"": 123, ""Single"": 1.23, ""Double"": 1.23, ""DateTime"": ""2000-01-01T00:00:00Z"", ""TimeSpan"": 60, ""Guid"": ""98bd4ed7-7337-4018-9551-ee0825ada7ba"", ""Enum"": 42, ""Struct"": { ""Valid"": false, ""Index"": 0, ""Size"": 0, ""Height"": 0, ""Amount"": 0, ""Created"": """", ""State"": 0, ""RatioOfStuff"": 0 } }";
			jsonText = CrystalJson.Serialize(x);
			Assert.That(jsonText, Is.EqualTo(expected), "Serialize(FILLED, JSON)");
			expected = "{ Bool: true, Int32: 123, Int64: 123, Single: 1.23, Double: 1.23, DateTime: new Date(946684800000), TimeSpan: 60, Guid: '98bd4ed7-7337-4018-9551-ee0825ada7ba', Enum: 42, Struct: { Valid: false, Index: 0, Size: 0, Height: 0, Amount: 0, Created: new Date(-62135596800000), State: 0, RatioOfStuff: 0 } }";
			jsonText = CrystalJson.Serialize(x, CrystalJsonSettings.JavaScript);
			Assert.That(jsonText, Is.EqualTo(expected), "Serialize(FILLED, JS)");
		}

		[Test]
		public void Test_JsonSerialize_Class()
		{
			// check les bases
			Assume.That(typeof(DummyJsonClass).IsValueType, Is.False);
			Assume.That(typeof(DummyJsonClass).IsClass, Is.True);

			var x = new DummyJsonClass();
			string expected = "{ \"Valid\": false, \"Index\": 0, \"Size\": 0, \"Height\": 0, \"Amount\": 0, \"Created\": \"\", \"State\": 0, \"RatioOfStuff\": 0 }";
			string jsonText = CrystalJson.Serialize(x);
			Assert.That(jsonText, Is.EqualTo(expected), "Serialize(EMPTY, JSON)");
			expected = "{ Valid: false, Index: 0, Size: 0, Height: 0, Amount: 0, Created: new Date(-62135596800000), State: 0, RatioOfStuff: 0 }";
			string jsText = CrystalJson.Serialize(x, CrystalJsonSettings.JavaScript);
			Assert.That(jsText, Is.EqualTo(expected), "Serialize(EMPTY, JS)");

			// masque les defaults
			expected = "{ }";
			jsonText = CrystalJson.Serialize(x, CrystalJsonSettings.Json.WithoutDefaultValues());
			Assert.That(jsonText, Is.EqualTo(expected), "SerializeObject(EMPTY, JSON+HideDefaults)");
			jsText = CrystalJson.Serialize(x, CrystalJsonSettings.JavaScript.WithoutDefaultValues());
			Assert.That(jsText, Is.EqualTo(expected), "SerializeObject(EMPTY, JS+HideDefaults)");

			// affichage des members null
			expected = "{ \"Valid\": false, \"Name\": null, \"Index\": 0, \"Size\": 0, \"Height\": 0, \"Amount\": 0, \"Created\": \"\", \"Modified\": null, \"State\": 0, \"RatioOfStuff\": 0 }";
			jsonText = CrystalJson.Serialize(x, CrystalJsonSettings.Json.WithNullMembers());
			Assert.That(jsonText, Is.EqualTo(expected), "Serialize(EMPTY, JSON+ShowNullMembers)");
			expected = "{ Valid: false, Name: null, Index: 0, Size: 0, Height: 0, Amount: 0, Created: new Date(-62135596800000), Modified: null, State: 0, RatioOfStuff: 0 }";
			jsText = CrystalJson.Serialize(x, CrystalJsonSettings.JavaScript.WithNullMembers());
			Assert.That(jsText, Is.EqualTo(expected), "Serialize(EMPTY, JS+ShowNullMembers)");

			// filled with values
			x.Name = "James Bond";
			x.Index = 7;
			x.Size = 123456789;
			x.Height = 1.8f;
			x.Amount = 0.07d;
			x.Created = new DateTime(1968, 5, 8, 0, 0, 0, DateTimeKind.Utc);
			x.Modified = new DateTime(2010, 10, 28, 15, 39, 0, DateTimeKind.Utc);
			x.State = DummyJsonEnum.Bar;
			// formatted
			expected = "{ \"Valid\": true, \"Name\": \"James Bond\", \"Index\": 7, \"Size\": 123456789, \"Height\": 1.8, \"Amount\": 0.07, \"Created\": \"1968-05-08T00:00:00Z\", \"Modified\": \"2010-10-28T15:39:00Z\", \"State\": 42, \"RatioOfStuff\": 8641975.23 }";
			jsonText = CrystalJson.Serialize(x);
			Assert.That(jsonText, Is.EqualTo(expected), "Serialize(class, JSON)");
			expected = "{ Valid: true, Name: 'James Bond', Index: 7, Size: 123456789, Height: 1.8, Amount: 0.07, Created: new Date(-52099200000), Modified: new Date(1288280340000), State: 42, RatioOfStuff: 8641975.23 }";
			jsText = CrystalJson.Serialize(x, CrystalJsonSettings.JavaScript);
			Assert.That(jsText, Is.EqualTo(expected), "Serialize(class, JS)");
			// compact
			expected = "{\"Valid\":true,\"Name\":\"James Bond\",\"Index\":7,\"Size\":123456789,\"Height\":1.8,\"Amount\":0.07,\"Created\":\"1968-05-08T00:00:00Z\",\"Modified\":\"2010-10-28T15:39:00Z\",\"State\":42,\"RatioOfStuff\":8641975.23}";
			jsonText = CrystalJson.Serialize(x, CrystalJsonSettings.JsonCompact);
			Assert.That(jsonText, Is.EqualTo(expected), "Serialize(class, JSON+Compact)");
			expected = "{Valid:true,Name:'James Bond',Index:7,Size:123456789,Height:1.8,Amount:0.07,Created:new Date(-52099200000),Modified:new Date(1288280340000),State:42,RatioOfStuff:8641975.23}";
			jsText = CrystalJson.Serialize(x, CrystalJsonSettings.JavaScript.Compacted());
			Assert.That(jsText, Is.EqualTo(expected), "Serialize(class, JS+Compact)");

			// en mode indenté
			expected =
				"{\r\n" +
				"\t\"Valid\": true,\r\n" +
				"\t\"Name\": \"James Bond\",\r\n" +
				"\t\"Index\": 7,\r\n" +
				"\t\"Size\": 123456789,\r\n" +
				"\t\"Height\": 1.8,\r\n" +
				"\t\"Amount\": 0.07,\r\n" +
				"\t\"Created\": \"1968-05-08T00:00:00Z\",\r\n" +
				"\t\"Modified\": \"2010-10-28T15:39:00Z\",\r\n" +
				"\t\"State\": 42,\r\n" +
				"\t\"RatioOfStuff\": 8641975.23\r\n" +
				"}";
			jsonText = CrystalJson.Serialize(x, CrystalJsonSettings.JsonIndented);
			Assert.That(jsonText, Is.EqualTo(expected), "Serialize(class, JSON+Indented)");
			expected =
				"{\r\n" +
				"\tValid: true,\r\n" +
				"\tName: 'James Bond',\r\n" +
				"\tIndex: 7,\r\n" +
				"\tSize: 123456789,\r\n" +
				"\tHeight: 1.8,\r\n" +
				"\tAmount: 0.07,\r\n" +
				"\tCreated: new Date(-52099200000),\r\n" +
				"\tModified: new Date(1288280340000),\r\n" +
				"\tState: 42,\r\n" +
				"\tRatioOfStuff: 8641975.23\r\n" +
				"}";
			jsText = CrystalJson.Serialize(x, CrystalJsonSettings.JavaScriptIndented);
			Assert.That(jsText, Is.EqualTo(expected), "Serialize(class, JS+Indented)");

			// Camel Casing
			expected = "{ \"valid\": true, \"name\": \"James Bond\", \"index\": 7, \"size\": 123456789, \"height\": 1.8, \"amount\": 0.07, \"created\": \"1968-05-08T00:00:00Z\", \"modified\": \"2010-10-28T15:39:00Z\", \"state\": 42, \"ratioOfStuff\": 8641975.23 }";
			jsonText = CrystalJson.Serialize(x, CrystalJsonSettings.Json.CamelCased());
			Assert.That(jsonText, Is.EqualTo(expected), "Serialize(class, JSON+CamelCasing)");
			expected = "{ valid: true, name: 'James Bond', index: 7, size: 123456789, height: 1.8, amount: 0.07, created: new Date(-52099200000), modified: new Date(1288280340000), state: 42, ratioOfStuff: 8641975.23 }";
			jsText = CrystalJson.Serialize(x, CrystalJsonSettings.JavaScript.CamelCased());
			Assert.That(jsText, Is.EqualTo(expected), "Serialize(class, JS+CamelCasing)");
		}

		[Test]
		public void Test_JsonSerialize_InterfaceMember()
		{
			// Problème: une classe qui contient un member de type interface
			// => il faut ne faut pas sérialiser les membres de l'interface, mais ceux de la classe concrète, qui varie au runtime !

			var x = new DummyOuterClass();

			string expected = "{ \"Id\": 0 }";
			string jsonText = CrystalJson.Serialize(x);
			Assert.That(jsonText, Is.EqualTo(expected), "Serialize(EMPTY, JSON)");

			// filled with values
			x.Id = 7;
			var agent = new DummyJsonClass();
			agent.Name = "James Bond";
			agent.Index = 7;
			agent.Size = 123456789;
			agent.Height = 1.8f;
			agent.Amount = 0.07d;
			agent.Created = new DateTime(1968, 5, 8, 0, 0, 0, DateTimeKind.Utc);
			agent.Modified = new DateTime(2010, 10, 28, 15, 39, 0, DateTimeKind.Utc);
			agent.State = DummyJsonEnum.Bar;
			x.Agent = agent;

			// sérialise l'agent lui-même (class)
			// comme il est top-level, on considère que l'appelant CONNAIT le bon type
			string expectedAgent = "{ \"Valid\": true, \"Name\": \"James Bond\", \"Index\": 7, \"Size\": 123456789, \"Height\": 1.8, \"Amount\": 0.07, \"Created\": \"1968-05-08T00:00:00Z\", \"Modified\": \"2010-10-28T15:39:00Z\", \"State\": 42, \"RatioOfStuff\": 8641975.23 }";
			jsonText = CrystalJson.Serialize(agent);
			Assert.That(jsonText, Is.EqualTo(expectedAgent), "Serialize(INNER, JSON)");

			// sérialise le conteneur, qui référence l'agent via une interface
			// vu que l'agent n'est plus top-level, il doit contenir une indication sur son type !
			expectedAgent = "{ \"_class\": \"Doxense.Serialization.Json.Tests.DummyJsonClass, Doxense.Core.Tests\", \"Valid\": true, \"Name\": \"James Bond\", \"Index\": 7, \"Size\": 123456789, \"Height\": 1.8, \"Amount\": 0.07, \"Created\": \"1968-05-08T00:00:00Z\", \"Modified\": \"2010-10-28T15:39:00Z\", \"State\": 42, \"RatioOfStuff\": 8641975.23 }";
			expected = "{ \"Id\": 7, \"Agent\": " + expectedAgent + " }";
			jsonText = CrystalJson.Serialize(x);
			Assert.That(jsonText, Is.EqualTo(expected), "Serialize(OUTER, JSON)");

			// désérialisation
			var y = CrystalJson.Deserialize<DummyOuterClass>(expected);
			Assert.That(y, Is.Not.Null);
			Assert.That(y.Id, Is.EqualTo(7), ".Id");
			Assert.That(y.Agent, Is.Not.Null, ".Agent");
			Assert.That(y.Agent, Is.InstanceOf<DummyJsonClass>(), ".Agent");
			Assert.That(y.Agent.Name, Is.EqualTo("James Bond"), ".Agent.Name");
			Assert.That(y.Agent.Index, Is.EqualTo(7), ".Agent.Index");
			Assert.That(y.Agent.Size, Is.EqualTo(123456789), ".Agent.Size");
			Assert.That(y.Agent.Height, Is.EqualTo(1.8f), ".Agent.Height");
			Assert.That(y.Agent.Amount, Is.EqualTo(0.07d), ".Agent.Amount");
			Assert.That(y.Agent.Created, Is.EqualTo(new DateTime(1968, 5, 8, 0, 0, 0, DateTimeKind.Utc)), ".Agent.Created");
			Assert.That(y.Agent.Modified, Is.EqualTo(new DateTime(2010, 10, 28, 15, 39, 0, DateTimeKind.Utc)), ".Agent.Modified");
			Assert.That(y.Agent.State, Is.EqualTo(DummyJsonEnum.Bar), ".Agent.State");
		}

		[Test]
		public void Test_JsonSerialize_UnsealedClassMember()
		{
			// On a un conteneur qui pointe vers une classe non-sealed, mais dont le type correspond au runtime (ie: pas un objet dérivié)
			// => Dans ce cas, il ne doit pas y avoir de "__class" dans le JSON car il n'y a pas d'ambiguité !
			var x = new DummyOuterDerivedClass();
			x.Id = 7;
			x.Agent = new DummyJsonClass();
			x.Agent.Name = "James Bond";
			x.Agent.Index = 7;
			x.Agent.Size = 123456789;
			x.Agent.Height = 1.8f;
			x.Agent.Amount = 0.07d;
			x.Agent.Created = new DateTime(1968, 5, 8, 0, 0, 0, DateTimeKind.Utc);
			x.Agent.Modified = new DateTime(2010, 10, 28, 15, 39, 0, DateTimeKind.Utc);
			x.Agent.State = DummyJsonEnum.Bar;

			string expectedAgent = "{ \"Valid\": true, \"Name\": \"James Bond\", \"Index\": 7, \"Size\": 123456789, \"Height\": 1.8, \"Amount\": 0.07, \"Created\": \"1968-05-08T00:00:00Z\", \"Modified\": \"2010-10-28T15:39:00Z\", \"State\": 42, \"RatioOfStuff\": 8641975.23 }";
			string jsonText = CrystalJson.Serialize(x.Agent);
			Assert.That(jsonText, Is.EqualTo(expectedAgent), "Serialize(INNER, JSON)");

			string expected = "{ \"Id\": 7, \"Agent\": " + expectedAgent + " }";
			jsonText = CrystalJson.Serialize(x);
			Assert.That(jsonText, Is.EqualTo(expected), "Serialize(OUTER, JSON)");

			// indenté
			expected =
				"{\r\n" +
				"\t\"Id\": 7,\r\n" +
				"\t\"Agent\": {\r\n" +
				"\t\t\"Valid\": true,\r\n" +
				"\t\t\"Name\": \"James Bond\",\r\n" +
				"\t\t\"Index\": 7,\r\n" +
				"\t\t\"Size\": 123456789,\r\n" +
				"\t\t\"Height\": 1.8,\r\n" +
				"\t\t\"Amount\": 0.07,\r\n" +
				"\t\t\"Created\": \"1968-05-08T00:00:00Z\",\r\n" +
				"\t\t\"Modified\": \"2010-10-28T15:39:00Z\",\r\n" +
				"\t\t\"State\": 42,\r\n" +
				"\t\t\"RatioOfStuff\": 8641975.23\r\n" +
				"\t}\r\n" +
				"}";
			jsonText = CrystalJson.Serialize(x, CrystalJsonSettings.Json.Indented());
			//Log(jsonText);
			Assert.That(jsonText, Is.EqualTo(expected), "Serialize(OUTER, JSON)");

			// désérialisation
			var y = CrystalJson.Deserialize<DummyOuterDerivedClass>(expected);
			Assert.That(y, Is.Not.Null);
			Assert.That(y.Id, Is.EqualTo(7), ".Id");
			Assert.That(y.Agent, Is.Not.Null, ".Agent");
			Assert.That(y.Agent, Is.InstanceOf<DummyJsonClass>(), ".Agent");
			Assert.That(y.Agent.Name, Is.EqualTo("James Bond"), ".Agent.Name");
			Assert.That(y.Agent.Index, Is.EqualTo(7), ".Agent.Index");
			Assert.That(y.Agent.Size, Is.EqualTo(123456789), ".Agent.Size");
			Assert.That(y.Agent.Height, Is.EqualTo(1.8f), ".Agent.Height");
			Assert.That(y.Agent.Amount, Is.EqualTo(0.07d), ".Agent.Amount");
			Assert.That(y.Agent.Created, Is.EqualTo(new DateTime(1968, 5, 8, 0, 0, 0, DateTimeKind.Utc)), ".Agent.Created");
			Assert.That(y.Agent.Modified, Is.EqualTo(new DateTime(2010, 10, 28, 15, 39, 0, DateTimeKind.Utc)), ".Agent.Modified");
			Assert.That(y.Agent.State, Is.EqualTo(DummyJsonEnum.Bar), ".Agent.State");
		}

		[Test]
		public void Test_JsonSerialize_DerivedClassMember()
		{
			// Problème: On a un conteneur contient un member de type "FooBase", mais l'objet au runtime est un "FooDerived" (qui dérive de "FooBase")
			// => il faut sérialiser les membres de FooDerived (qui en a probablement plus que FooBase), mais aussi que l'attribut "__class" soit présent
			// pour que la désérialisation soit capable de construire le bon objet !

			var x = new DummyOuterDerivedClass();

			string expected = "{ \"Id\": 0 }";
			string jsonText = CrystalJson.Serialize(x);
			Assert.That(jsonText, Is.EqualTo(expected), "Serialize(EMPTY, JSON)");

			// filled with values
			x.Id = 7;
			var agent = new DummyDerivedJsonClass("Janov Bondovicz");
			x.Agent = agent;
			x.Agent.Name = "James Bond";
			x.Agent.Index = 7;
			x.Agent.Size = 123456789;
			x.Agent.Height = 1.8f;
			x.Agent.Amount = 0.07d;
			x.Agent.Created = new DateTime(1968, 5, 8, 0, 0, 0, DateTimeKind.Utc);
			x.Agent.Modified = new DateTime(2010, 10, 28, 15, 39, 0, DateTimeKind.Utc);
			x.Agent.State = DummyJsonEnum.Bar;

			// sérialise l'agent lui-même (class)
			// comme il est top-level, on considère que l'appelant CONNAIT le bon type
			string expectedAgent = "{ \"IsDoubleAgent\": true, \"DoubleAgentName\": \"Janov Bondovicz\", \"Valid\": true, \"Name\": \"James Bond\", \"Index\": 7, \"Size\": 123456789, \"Height\": 1.8, \"Amount\": 0.07, \"Created\": \"1968-05-08T00:00:00Z\", \"Modified\": \"2010-10-28T15:39:00Z\", \"State\": 42, \"RatioOfStuff\": 8641975.23 }";
			// ATENTION: CrystalJson.Serialize(x.Agent) est est mappé sur Serialize<DummyJsonClass>(...), qui est différent de CrystalJson.Serialize(agent) qui est mappé sur Serialize<DummyDerivedJsonClass>(...) !
			jsonText = CrystalJson.Serialize(agent);
			Assert.That(jsonText, Is.EqualTo(expectedAgent), "Serialize(INNER, JSON)");

			// sérialise le conteneur, qui référence l'agent via une interface
			// vu que l'agent n'est plus top-level, il doit contenir une indication sur son type !
			expectedAgent = "{ \"_class\": \"Doxense.Serialization.Json.Tests.DummyDerivedJsonClass, Doxense.Core.Tests\", \"IsDoubleAgent\": true, \"DoubleAgentName\": \"Janov Bondovicz\", \"Valid\": true, \"Name\": \"James Bond\", \"Index\": 7, \"Size\": 123456789, \"Height\": 1.8, \"Amount\": 0.07, \"Created\": \"1968-05-08T00:00:00Z\", \"Modified\": \"2010-10-28T15:39:00Z\", \"State\": 42, \"RatioOfStuff\": 8641975.23 }";
			expected = "{ \"Id\": 7, \"Agent\": " + expectedAgent + " }";
			jsonText = CrystalJson.Serialize(x);
			Assert.That(jsonText, Is.EqualTo(expected), "Serialize(OUTER, JSON)");

			// désérialisation avec une classe dérivée
			var y = CrystalJson.Deserialize<DummyOuterDerivedClass>(expected);
			Assert.That(y, Is.Not.Null);
			Assert.That(y.Id, Is.EqualTo(7), ".Id");
			Assert.That(y.Agent, Is.Not.Null, ".Agent");
			Assert.That(y.Agent, Is.InstanceOf<DummyDerivedJsonClass>(), ".Agent");
			Assert.That(y.Agent.Name, Is.EqualTo("James Bond"), ".Agent.Name");
			Assert.That(y.Agent.Index, Is.EqualTo(7), ".Agent.Index");
			Assert.That(y.Agent.Size, Is.EqualTo(123456789), ".Agent.Size");
			Assert.That(y.Agent.Height, Is.EqualTo(1.8f), ".Agent.Height");
			Assert.That(y.Agent.Amount, Is.EqualTo(0.07d), ".Agent.Amount");
			Assert.That(y.Agent.Created, Is.EqualTo(new DateTime(1968, 5, 8, 0, 0, 0, DateTimeKind.Utc)), ".Agent.Created");
			Assert.That(y.Agent.Modified, Is.EqualTo(new DateTime(2010, 10, 28, 15, 39, 0, DateTimeKind.Utc)), ".Agent.Modified");
			Assert.That(y.Agent.State, Is.EqualTo(DummyJsonEnum.Bar), ".Agent.State");
			var z = (DummyDerivedJsonClass) y.Agent;
			Assert.That(z.DoubleAgentName, Is.EqualTo("Janov Bondovicz"), "z.DoubleAgentName");
			Assert.That(z.IsDoubleAgent, Is.True, "z.IsDoubleAgent");
		}

		[Test]
		public void Test_Json_Custom_Serializable_Interface()
		{

			// sérialisation
			var x = new DummyJsonCustomClass("foo");
			Assert.That(CrystalJson.Serialize(x), Is.EqualTo("{ \"custom\":\"foo\" }"));

			// désérialisation
			const string JSON_TEXT = "{ \"custom\":\"bar\" }";
			var y = CrystalJson.Deserialize<DummyJsonCustomClass>(JSON_TEXT);
			Assert.That(y, Is.Not.Null);
			Assert.That(y, Is.InstanceOf<DummyJsonCustomClass>());
			Assert.That(y.GetSecret(), Is.EqualTo("bar"));

			// packing
			var value = JsonValue.FromValue(x);
			Assert.That(value, Is.Not.Null);
			Assert.That(value.Type, Is.EqualTo(JsonType.Object));
			var obj = (JsonObject)value;
			Assert.That(obj.Get<string>("custom"), Is.EqualTo("foo"));
			Assert.That(obj.Count, Is.EqualTo(1));

			// unpacking
			var z = value.Bind(typeof(DummyJsonCustomClass));
			Assert.That(z, Is.Not.Null);
			Assert.That(z, Is.InstanceOf<DummyJsonCustomClass>());
			Assert.That(((DummyJsonCustomClass)z).GetSecret(), Is.EqualTo("foo"));
		}

		[Test]
		public void Test_Json_Custom_Serializable_Static()
		{

			// sérialisation
			var x = new DummyStaticCustomJson("foo");
			Assert.That(CrystalJson.Serialize(x), Is.EqualTo("{ \"custom\":\"foo\" }"));

			// désérialisation
			string jsonText = "{ \"custom\":\"bar\" }";
			var y = CrystalJson.Deserialize<DummyStaticCustomJson>(jsonText);
			Assert.That(y, Is.Not.Null);
			Assert.That(y, Is.InstanceOf<DummyStaticCustomJson>());
			Assert.That(y.GetSecret(), Is.EqualTo("bar"));
		}

		[Test]
		public void Test_Json_DuckTyping_Serializable_Class()
		{
			// instance.JsonSerialize(...) + ctor(JsonObject)
			var original = new DummyCtorBasedJsonSerializableClass(123, "Bob", System.Drawing.Color.Red, 5, 7);
			var json = CrystalJson.Serialize(original);
			Log(json);
			Assert.That(
				json,
				Is.EqualTo(@"{ ""Id"": 123, ""Name"": ""Bob"", ""Color"": ""Red"", ""XY"": ""5:7"" }"),
				"Via instance JsonSerialize() method"
			);

			{ // Deserialize<T> should invoke the ctor(JsonObject,...)
				var x = CrystalJson.Deserialize<DummyCtorBasedJsonSerializableClass>(@"{ ""Id"":123,""Name"":""Bob"",""Color"":""Red"",""XY"":""5:7"" }");
				Assert.That(x, Is.Not.Null);
				Assert.That(x.Id, Is.EqualTo(123));
				Assert.That(x.Name, Is.EqualTo("Bob"));
				Assert.That(x.Color, Is.EqualTo(System.Drawing.Color.Red));
				Assert.That(x.X, Is.EqualTo(5));
				Assert.That(x.Y, Is.EqualTo(7));
			}

			{ // As<...> should also use the ctor(JsonObject)
				var x = CrystalJson.ParseObject(json).As<DummyCtorBasedJsonSerializableClass>();
				Assert.That(x, Is.Not.Null);
				Assert.That(x.Id, Is.EqualTo(123));
				Assert.That(x.Name, Is.EqualTo("Bob"));
				Assert.That(x.Color, Is.EqualTo(System.Drawing.Color.Red));
				Assert.That(x.X, Is.EqualTo(5));
				Assert.That(x.Y, Is.EqualTo(7));
			}

			{ // FromValue(...) should find the JsonPack(..) instance method
				var obj = JsonObject.FromObject(original);
				Log(obj);
				Assert.That(obj, Is.Not.Null);
				Assert.That(obj.Type, Is.EqualTo(JsonType.Object));
				Assert.That(obj.Get<int>("Id"), Is.EqualTo(123));
				Assert.That(obj.Get<string>("Name"), Is.EqualTo("Bob"));
				Assert.That(obj.Get<string>("Color"), Is.EqualTo("Red"));
				Assert.That(obj.Get<string>("XY"), Is.EqualTo("5:7"));
				Assert.That(obj.Count, Is.EqualTo(4));
			}
		}

		[Test]
		public void Test_Json_DuckTyping_Serializable_Struct()
		{
			// instance.JsonSerialize(...) + ctor(JsonObject)
			var original = new DummyCtorBasedJsonSerializableStruct(123, "Bob", 5, 7);
			var json = CrystalJson.Serialize(original);
			Log(json);
			Assert.That(
				json,
				Is.EqualTo(@"{ ""Id"": 123, ""Name"": ""Bob"", ""XY"": ""5:7"" }"),
				"Via instance JsonSerialize() method"
			);

			{ // Deserialize<T> should invoke the ctor(JsonObject,...)
				var x = CrystalJson.Deserialize<DummyCtorBasedJsonSerializableStruct>(@"{ ""Id"":123,""Name"":""Bob"",""XY"":""5:7"" }");
				Assert.That(x, Is.Not.Null);
				Assert.That(x.Id, Is.EqualTo(123));
				Assert.That(x.Name, Is.EqualTo("Bob"));
				Assert.That(x.X, Is.EqualTo(5));
				Assert.That(x.Y, Is.EqualTo(7));
			}

			{ // As<...> should also use the ctor(JsonObject)
				var x = CrystalJson.ParseObject(json).As<DummyCtorBasedJsonSerializableStruct>();
				Assert.That(x, Is.Not.Null);
				Assert.That(x.Id, Is.EqualTo(123));
				Assert.That(x.Name, Is.EqualTo("Bob"));
				Assert.That(x.X, Is.EqualTo(5));
				Assert.That(x.Y, Is.EqualTo(7));
			}

			{ // FromValue(...) should find the JsonPack(..) instance method
				var obj = JsonObject.FromObject(original);
				Log(obj);
				Assert.That(obj, Is.Not.Null);
				Assert.That(obj.Type, Is.EqualTo(JsonType.Object));
				Assert.That(obj.Get<int>("Id"), Is.EqualTo(123));
				Assert.That(obj.Get<string>("Name"), Is.EqualTo("Bob"));
				Assert.That(obj.Get<string>("XY"), Is.EqualTo("5:7"));
				Assert.That(obj.Count, Is.EqualTo(3));
			}
		}

		[Test]
		public void Test_Json_DuckTyping_Packable_Only()
		{
			// instance.JsonPack(...) + ctor(JsonObject)
			// note: this class does NOT implement JsonSerialize, so serializating should go through JsonPack(..) before

			var original = new DummyCtorBasedJsonBindableClass(123, "Bob", System.Drawing.Color.Red, 5, 7);
			var json = CrystalJson.Serialize(original);
			Log(json);
			Assert.That(
				json,
				Is.EqualTo(@"{ ""Id"": 123, ""Name"": ""Bob"", ""Color"": ""Red"", ""XY"": ""5:7"" }"),
				"Via instance JsonPack() method"
			);

			{ // Deserialize<T> should invoke the ctor(JsonObject,...)
				var x = CrystalJson.Deserialize<DummyCtorBasedJsonBindableClass>(@"{ ""Id"":123,""Name"":""Bob"",""Color"":""Red"",""XY"":""5:7"" }");
				Assert.That(x, Is.Not.Null);
				Assert.That(x.Id, Is.EqualTo(123));
				Assert.That(x.Name, Is.EqualTo("Bob"));
				Assert.That(x.Color, Is.EqualTo(System.Drawing.Color.Red));
				Assert.That(x.X, Is.EqualTo(5));
				Assert.That(x.Y, Is.EqualTo(7));
			}

			{ // As<...> should also use the ctor(JsonObject)
				var x = CrystalJson.ParseObject(json).As<DummyCtorBasedJsonBindableClass>();
				Assert.That(x, Is.Not.Null);
				Assert.That(x.Id, Is.EqualTo(123));
				Assert.That(x.Name, Is.EqualTo("Bob"));
				Assert.That(x.Color, Is.EqualTo(System.Drawing.Color.Red));
				Assert.That(x.X, Is.EqualTo(5));
				Assert.That(x.Y, Is.EqualTo(7));
			}

			{ // FromValue(...) should find the JsonPack(..) instance method
				var obj = JsonObject.FromObject(original);
				Log(obj);
				Assert.That(obj, Is.Not.Null);
				Assert.That(obj.Type, Is.EqualTo(JsonType.Object));
				Assert.That(obj.Get<int>("Id"), Is.EqualTo(123));
				Assert.That(obj.Get<string>("Name"), Is.EqualTo("Bob"));
				Assert.That(obj.Get<string>("Color"), Is.EqualTo("Red"));
				Assert.That(obj.Get<string>("XY"), Is.EqualTo("5:7"));
				Assert.That(obj.Count, Is.EqualTo(4));
			}
		}

		[Test]
		public void Test_Json_Custom_Resolver()
		{
			var x = new DummyJsonClass();
			var resolver = new DummyCustomJsonResolver();

			string expected = @"{ ""Foo"": ""<nobody>"", ""Narf"": 42 }";
			string jsonText = CrystalJson.Serialize(x, customResolver: resolver);
			Assert.That(jsonText, Is.EqualTo(expected), "Serialize(EMPTY,JSON+CustomResolver)");

			expected = @"{ ""Foo"": ""<nobody>"" }";
			jsonText = CrystalJson.Serialize(x, CrystalJsonSettings.Json.WithoutDefaultValues(), resolver);
			Assert.That(jsonText, Is.EqualTo(expected), "SerializeObject(EMPTY,JSON+CustomResolver+WithoutDefaults)");

			// avec des valeures
			x.Index = 7; // => 42+7 = 49
			x.Name = "James Bond"; // => "<James Bond>"
			x.Height = 1.23f; // => ne devrait pas être visible
			expected = @"{ ""Foo"": ""<James Bond>"", ""Narf"": 49 }";
			jsonText = CrystalJson.Serialize(x, CrystalJsonSettings.Json, resolver);
			Assert.That(jsonText, Is.EqualTo(expected), "Serialize(x,JSON+CustomResolver)");

			// avec le default resolver
			expected = @"{ ""Valid"": true, ""Name"": ""James Bond"", ""Index"": 7, ""Size"": 0, ""Height"": 1.23, ""Amount"": 0, ""Created"": """", ""State"": 0, ""RatioOfStuff"": 0 }";
			jsonText = CrystalJson.Serialize(x, CrystalJsonSettings.Json);
			Assert.That(jsonText, Is.EqualTo(expected), "Serialize(x,JSON+DefaultResolver)");
		}

		[Test]
		public void Test_JsonSerialize_Arrays()
		{
			// int[]
			Assert.That(CrystalJson.Serialize(Array.Empty<int>()), Is.EqualTo("[ ]"));
			Assert.That(CrystalJson.Serialize(new int[1]), Is.EqualTo("[ 0 ]"));
			Assert.That(CrystalJson.Serialize(new int[] { 1, 2, 3 }), Is.EqualTo("[ 1, 2, 3 ]"));

			Assert.That(CrystalJson.Serialize(Array.Empty<int>(), CrystalJsonSettings.JsonCompact), Is.EqualTo("[]"));
			Assert.That(CrystalJson.Serialize(new int[1], CrystalJsonSettings.JsonCompact), Is.EqualTo("[0]"));
			Assert.That(CrystalJson.Serialize(new int[] { 1, 2, 3 }, CrystalJsonSettings.JsonCompact), Is.EqualTo("[1,2,3]"));

			// string[]
			Assert.That(CrystalJson.Serialize(new string[0]), Is.EqualTo("[ ]"));
			Assert.That(CrystalJson.Serialize(new string[1]), Is.EqualTo("[ null ]"));
			Assert.That(CrystalJson.Serialize(new string[] { "foo" }), Is.EqualTo(@"[ ""foo"" ]"));
			Assert.That(CrystalJson.Serialize(new string[] { "foo", "bar", "baz" }), Is.EqualTo(@"[ ""foo"", ""bar"", ""baz"" ]"));
			Assert.That(CrystalJson.Serialize(new string[] { "foo" }, CrystalJsonSettings.JavaScript), Is.EqualTo("[ 'foo' ]"));
			Assert.That(CrystalJson.Serialize(new string[] { "foo", "bar", "baz" }, CrystalJsonSettings.JavaScript), Is.EqualTo("[ 'foo', 'bar', 'baz' ]"));

			// compact
			Assert.That(CrystalJson.Serialize(new string[0], CrystalJsonSettings.JsonCompact), Is.EqualTo("[]"));
			Assert.That(CrystalJson.Serialize(new string[] { "foo", "bar", "baz" }, CrystalJsonSettings.JsonCompact), Is.EqualTo(@"[""foo"",""bar"",""baz""]"));
			Assert.That(CrystalJson.Serialize(new string[] { "foo", "bar", "baz" }, CrystalJsonSettings.JavaScriptCompact), Is.EqualTo("['foo','bar','baz']"));
		}

		[Test]
		public void Test_JsonSerialize_Jagged_Arrays()
		{
			Assert.That(CrystalJson.Serialize<int[][]>(new [] { new int[0], new int[0] }), Is.EqualTo("[ [ ], [ ] ]"));
			Assert.That(CrystalJson.Serialize<int[][]>(new [] { new int[0], new int[0] }, CrystalJsonSettings.JsonCompact), Is.EqualTo("[[],[]]"));

			Assert.That(CrystalJson.Serialize<int[][]>(new [] { new [] { 1, 2, 3 }, new [] { 4, 5, 6 } }), Is.EqualTo("[ [ 1, 2, 3 ], [ 4, 5, 6 ] ]"));
			Assert.That(CrystalJson.Serialize<int[][]>(new [] { new [] { 1, 2, 3 }, new [] { 4, 5, 6 } }, CrystalJsonSettings.JsonCompact), Is.EqualTo("[[1,2,3],[4,5,6]]"));

			// INCEPTION !
			Assert.That(CrystalJson.Serialize(new [] { new [] { new [] { new [] { "INCEPTION" } } } }), Is.EqualTo("[ [ [ [ \"INCEPTION\" ] ] ] ]"));
		}

		[Test]
		public void Test_JsonSerialize_Lists()
		{
			// Collections
			var listOfStrings = new List<string>();
			Assert.That(CrystalJson.Serialize(listOfStrings), Is.EqualTo("[ ]"));
			listOfStrings.Add("foo");
			Assert.That(CrystalJson.Serialize(listOfStrings), Is.EqualTo(@"[ ""foo"" ]"));
			listOfStrings.Add("bar");
			listOfStrings.Add("baz");
			Assert.That(CrystalJson.Serialize(listOfStrings), Is.EqualTo(@"[ ""foo"", ""bar"", ""baz"" ]"));
			Assert.That(CrystalJson.Serialize(listOfStrings, CrystalJsonSettings.JavaScript), Is.EqualTo("[ 'foo', 'bar', 'baz' ]"));

			var listOfObjects = new List<object>();
			listOfObjects.Add(123);
			listOfObjects.Add("Narf");
			listOfObjects.Add(true);
			listOfObjects.Add(DummyJsonEnum.Bar);
			Assert.That(CrystalJson.Serialize(listOfObjects), Is.EqualTo(@"[ 123, ""Narf"", true, 42 ]"));
			Assert.That(CrystalJson.Serialize(listOfObjects, CrystalJsonSettings.JavaScript), Is.EqualTo("[ 123, 'Narf', true, 42 ]"));
		}

		[Test]
		public void Test_JsonSerialize_QueryableCollection()
		{
			// list of objects
			var queryableOfAnonymous = new int[] { 1, 2, 3 }.Select((x) => new { Value = x, Square = x * x, Ascii = (char)(64 + x) });
			// directement le queryable
			Assert.That(
				CrystalJson.Serialize(queryableOfAnonymous),
				Is.EqualTo(@"[ { ""Value"": 1, ""Square"": 1, ""Ascii"": ""A"" }, { ""Value"": 2, ""Square"": 4, ""Ascii"": ""B"" }, { ""Value"": 3, ""Square"": 9, ""Ascii"": ""C"" } ]")
			);
			// convertit en liste
			Assert.That(
				CrystalJson.Serialize(queryableOfAnonymous.ToList()),
				Is.EqualTo(@"[ { ""Value"": 1, ""Square"": 1, ""Ascii"": ""A"" }, { ""Value"": 2, ""Square"": 4, ""Ascii"": ""B"" }, { ""Value"": 3, ""Square"": 9, ""Ascii"": ""C"" } ]")
			);
		}

		[Test]
		public void Test_JsonSerialize_STuples()
		{
			// STuple<...>
			Assert.That(CrystalJson.Serialize(STuple.Empty), Is.EqualTo("[ ]"));
			Assert.That(CrystalJson.Serialize(STuple.Create(123)), Is.EqualTo("[ 123 ]"));
			Assert.That(CrystalJson.Serialize(STuple.Create(123, "Hello")), Is.EqualTo("[ 123, \"Hello\" ]"));
			Assert.That(CrystalJson.Serialize(STuple.Create(123, "Hello", true)), Is.EqualTo("[ 123, \"Hello\", true ]"));
			Assert.That(CrystalJson.Serialize(STuple.Create(123, "Hello", true, -1.5)), Is.EqualTo("[ 123, \"Hello\", true, -1.5 ]"));
			Assert.That(CrystalJson.Serialize(STuple.Create(123, "Hello", true, -1.5, 'Z')), Is.EqualTo("[ 123, \"Hello\", true, -1.5, \"Z\" ]"));
			Assert.That(CrystalJson.Serialize(STuple.Create(123, "Hello", true, -1.5, 'Z', new DateTime(2016, 11, 24, 11, 07, 23))), Is.EqualTo("[ 123, \"Hello\", true, -1.5, \"Z\", \"2016-11-24T11:07:23\" ]"));
			Assert.That(CrystalJson.Serialize(STuple.Create(123, "Hello", true, -1.5, 'Z', new DateTime(2016, 11, 24, 11, 07, 23), "World")), Is.EqualTo("[ 123, \"Hello\", true, -1.5, \"Z\", \"2016-11-24T11:07:23\", \"World\" ]"));

			// (ITuple) STuple<...>
			Assert.That(CrystalJson.Serialize(STuple.Empty), Is.EqualTo("[ ]"));
			Assert.That(CrystalJson.Serialize((IVarTuple) STuple.Create(123)), Is.EqualTo("[ 123 ]"));
			Assert.That(CrystalJson.Serialize((IVarTuple) STuple.Create(123, "Hello")), Is.EqualTo("[ 123, \"Hello\" ]"));
			Assert.That(CrystalJson.Serialize((IVarTuple) STuple.Create(123, "Hello", true)), Is.EqualTo("[ 123, \"Hello\", true ]"));
			Assert.That(CrystalJson.Serialize((IVarTuple) STuple.Create(123, "Hello", true, -1.5)), Is.EqualTo("[ 123, \"Hello\", true, -1.5 ]"));
			Assert.That(CrystalJson.Serialize((IVarTuple) STuple.Create(123, "Hello", true, -1.5, 'Z')), Is.EqualTo("[ 123, \"Hello\", true, -1.5, \"Z\" ]"));
			Assert.That(CrystalJson.Serialize((IVarTuple) STuple.Create(123, "Hello", true, -1.5, 'Z', new DateTime(2016, 11, 24, 11, 07, 23))), Is.EqualTo("[ 123, \"Hello\", true, -1.5, \"Z\", \"2016-11-24T11:07:23\" ]"));
			Assert.That(CrystalJson.Serialize((IVarTuple) STuple.Create(123, "Hello", true, -1.5, 'Z', new DateTime(2016, 11, 24, 11, 07, 23), "World")), Is.EqualTo("[ 123, \"Hello\", true, -1.5, \"Z\", \"2016-11-24T11:07:23\", \"World\" ]"));

			// custom tuple types
			Assert.That(CrystalJson.Serialize(new ListTuple<int>(new [] {1, 2, 3})), Is.EqualTo("[ 1, 2, 3 ]"));
			Assert.That(CrystalJson.Serialize(new ListTuple<string>(new [] {"foo", "bar", "baz"})), Is.EqualTo("[ \"foo\", \"bar\", \"baz\" ]"));
			Assert.That(CrystalJson.Serialize(new ListTuple<object>(new object[] { "hello world", 123, false })), Is.EqualTo("[ \"hello world\", 123, false ]"));
			Assert.That(CrystalJson.Serialize(new LinkedTuple<int>(STuple.Create(1, 2), 3)), Is.EqualTo("[ 1, 2, 3 ]"));
			Assert.That(CrystalJson.Serialize(new JoinedTuple(STuple.Create(1, 2), STuple.Create(3))), Is.EqualTo("[ 1, 2, 3 ]"));
		}

		[Test]
		public void Test_JsonSerialize_ValueTuples()
		{
			// STuple<...>
			Log("ValueTuple...");
			Assert.That(CrystalJson.Serialize(ValueTuple.Create()), Is.EqualTo("[ ]"));
			Assert.That(CrystalJson.Serialize(ValueTuple.Create(123)), Is.EqualTo("[ 123 ]"));
			Assert.That(CrystalJson.Serialize(ValueTuple.Create(123, "Hello")), Is.EqualTo("[ 123, \"Hello\" ]"));
			Assert.That(CrystalJson.Serialize(ValueTuple.Create(123, "Hello", true)), Is.EqualTo("[ 123, \"Hello\", true ]"));
			Assert.That(CrystalJson.Serialize(ValueTuple.Create(123, "Hello", true, -1.5)), Is.EqualTo("[ 123, \"Hello\", true, -1.5 ]"));
			Assert.That(CrystalJson.Serialize(ValueTuple.Create(123, "Hello", true, -1.5, 'Z')), Is.EqualTo("[ 123, \"Hello\", true, -1.5, \"Z\" ]"));
			Assert.That(CrystalJson.Serialize(ValueTuple.Create(123, "Hello", true, -1.5, 'Z', new DateTime(2016, 11, 24, 11, 07, 23))), Is.EqualTo("[ 123, \"Hello\", true, -1.5, \"Z\", \"2016-11-24T11:07:23\" ]"));
			Assert.That(CrystalJson.Serialize(ValueTuple.Create(123, "Hello", true, -1.5, 'Z', new DateTime(2016, 11, 24, 11, 07, 23), "World")), Is.EqualTo("[ 123, \"Hello\", true, -1.5, \"Z\", \"2016-11-24T11:07:23\", \"World\" ]"));

			// (ITuple) STuple<...>
			Log("ITuple...");
			Assert.That(CrystalJson.Serialize((System.Runtime.CompilerServices.ITuple) ValueTuple.Create()), Is.EqualTo("[ ]"));
			Assert.That(CrystalJson.Serialize((System.Runtime.CompilerServices.ITuple) ValueTuple.Create(123)), Is.EqualTo("[ 123 ]"));
			Assert.That(CrystalJson.Serialize((System.Runtime.CompilerServices.ITuple) ValueTuple.Create(123, "Hello")), Is.EqualTo("[ 123, \"Hello\" ]"));
			Assert.That(CrystalJson.Serialize((System.Runtime.CompilerServices.ITuple) ValueTuple.Create(123, "Hello", true)), Is.EqualTo("[ 123, \"Hello\", true ]"));
			Assert.That(CrystalJson.Serialize((System.Runtime.CompilerServices.ITuple) ValueTuple.Create(123, "Hello", true, -1.5)), Is.EqualTo("[ 123, \"Hello\", true, -1.5 ]"));
			Assert.That(CrystalJson.Serialize((System.Runtime.CompilerServices.ITuple) ValueTuple.Create(123, "Hello", true, -1.5, 'Z')), Is.EqualTo("[ 123, \"Hello\", true, -1.5, \"Z\" ]"));
			Assert.That(CrystalJson.Serialize((System.Runtime.CompilerServices.ITuple) ValueTuple.Create(123, "Hello", true, -1.5, 'Z', new DateTime(2016, 11, 24, 11, 07, 23))), Is.EqualTo("[ 123, \"Hello\", true, -1.5, \"Z\", \"2016-11-24T11:07:23\" ]"));
			Assert.That(CrystalJson.Serialize((System.Runtime.CompilerServices.ITuple) ValueTuple.Create(123, "Hello", true, -1.5, 'Z', new DateTime(2016, 11, 24, 11, 07, 23), "World")), Is.EqualTo("[ 123, \"Hello\", true, -1.5, \"Z\", \"2016-11-24T11:07:23\", \"World\" ]"));
		}

		[Test]
		public void Test_JsonSerialize_NodaTime_Types()
		{
			#region Duration

			//secondes completes
			var duration = NodaTime.Duration.FromSeconds(3600);
			Assert.That(CrystalJson.Serialize(duration), Is.EqualTo("3600"));

			//secondes + milisecondes
			duration = NodaTime.Duration.FromMilliseconds(3600272);
			Assert.That(CrystalJson.Serialize(duration), Is.EqualTo("3600.272"));

			//epsilon
			duration = NodaTime.Duration.Epsilon;
			Assert.That(CrystalJson.Serialize(duration), Is.EqualTo("1E-09"));

			#endregion

			#region Instant

			var instant = default(NodaTime.Instant);
			Assert.That(CrystalJson.Serialize(instant), Is.EqualTo("\"1970-01-01T00:00:00Z\""));

			instant = NodaTime.Instant.FromUtc(2013, 6, 7, 11, 06, 58);
			Assert.That(CrystalJson.Serialize(instant), Is.EqualTo("\"2013-06-07T11:06:58Z\""));

			instant = NodaTime.Instant.FromUtc(-52, 8, 27, 12, 12);
			Assert.That(CrystalJson.Serialize(instant), Is.EqualTo("\"-0052-08-27T12:12:00Z\""));

			#endregion

			#region LocalDateTime

			var time = default(NodaTime.LocalDateTime);
			Assert.That(CrystalJson.Serialize(time), Is.EqualTo("\"0001-01-01T00:00:00\""));

			time = new NodaTime.LocalDateTime(1988, 04, 19, 00, 35, 56);
			Assert.That(CrystalJson.Serialize(time), Is.EqualTo("\"1988-04-19T00:35:56\""));

			time = new NodaTime.LocalDateTime(0, 1, 1, 0, 0);
			Assert.That(CrystalJson.Serialize(time), Is.EqualTo("\"0000-01-01T00:00:00\""));

			time = new NodaTime.LocalDateTime(-250, 02, 27, 18, 42);
			Assert.That(CrystalJson.Serialize(time), Is.EqualTo("\"-0250-02-27T18:42:00\""));

			#endregion

			#region ZonedDateTime

			Assert.That(
				CrystalJson.Serialize(default(NodaTime.ZonedDateTime)),
				Is.EqualTo("\"0001-01-01T00:00:00Z UTC\"")
			);

			Assert.That(
				CrystalJson.Serialize(new NodaTime.ZonedDateTime(NodaTime.Instant.FromUtc(1988, 04, 19, 00, 35, 56), NodaTime.DateTimeZoneProviders.Tzdb["Europe/Paris"])),
				Is.EqualTo("\"1988-04-19T02:35:56+02 Europe/Paris\"") // note: GMT+2
			);

			Assert.That(
				CrystalJson.Serialize(new NodaTime.ZonedDateTime(NodaTime.Instant.FromUtc(0, 1, 1, 0, 0), NodaTime.DateTimeZone.Utc)),
				Is.EqualTo("\"0000-01-01T00:00:00Z UTC\"")
			);

			Assert.That(
				CrystalJson.Serialize(new NodaTime.ZonedDateTime(NodaTime.Instant.FromUtc(-250, 02, 27, 18, 42), NodaTime.DateTimeZoneProviders.Tzdb["Africa/Cairo"])),
				Is.EqualTo("\"-0250-02-27T20:47:09+02:05:09 Africa/Cairo\"") // note: avant les calendriers grégoriens, donc il y a des compensations de timezones spécifiques
			);

			// Deliberately give it an ambiguous local time, in both ways.
			var zone = NodaTime.DateTimeZoneProviders.Tzdb["Europe/London"];
			Assert.That(
				CrystalJson.Serialize(new NodaTime.ZonedDateTime(new NodaTime.LocalDateTime(2012, 10, 28, 1, 30), zone, NodaTime.Offset.FromHours(1))),
				Is.EqualTo("\"2012-10-28T01:30:00+01 Europe/London\"")
			);
			Assert.That(
				CrystalJson.Serialize(new NodaTime.ZonedDateTime(new NodaTime.LocalDateTime(2012, 10, 28, 1, 30), zone, NodaTime.Offset.FromHours(0))),
				Is.EqualTo("\"2012-10-28T01:30:00Z Europe/London\"")
			);

			#endregion

			#region DateTimeZone

			Assert.That(CrystalJson.Serialize(NodaTime.DateTimeZone.Utc), Is.EqualTo("\"UTC\""));
			// avec tzdb, c'est au format "Region/City"
			Assert.That(CrystalJson.Serialize(NodaTime.DateTimeZoneProviders.Tzdb["Europe/Paris"]), Is.EqualTo("\"Europe/Paris\""));
			Assert.That(CrystalJson.Serialize(NodaTime.DateTimeZoneProviders.Tzdb["America/New_York"]), Is.EqualTo("\"America/New_York\"")); // espace convertis en '_'
			Assert.That(CrystalJson.Serialize(NodaTime.DateTimeZoneProviders.Tzdb["Asia/Tokyo"]), Is.EqualTo("\"Asia/Tokyo\""));

			#endregion

			#region OffsetDateTime

			Assert.That(
				CrystalJson.Serialize(default(NodaTime.OffsetDateTime)),
				Is.EqualTo("\"0001-01-01T00:00:00Z\"")
			);

			Assert.That(
				CrystalJson.Serialize(new NodaTime.LocalDateTime(2012, 1, 2, 3, 4, 5, 6).PlusTicks(7).WithOffset(NodaTime.Offset.Zero)),
				Is.EqualTo("\"2012-01-02T03:04:05.0060007Z\""),
				"Offset of 0 means UTC"
			);

			Assert.That(
				CrystalJson.Serialize(new NodaTime.LocalDateTime(2012, 1, 2, 3, 4, 5, 6).PlusTicks(7).WithOffset(NodaTime.Offset.FromHours(2))),
				Is.EqualTo("\"2012-01-02T03:04:05.0060007+02:00\""),
				"Only HH:MM for the timezone offset"
			);

			Assert.That(
				CrystalJson.Serialize(new NodaTime.LocalDateTime(2012, 1, 2, 3, 4, 5, 6).PlusTicks(7).WithOffset(NodaTime.Offset.FromHoursAndMinutes(-1, -30))),
				Is.EqualTo("\"2012-01-02T03:04:05.0060007-01:30\""),
				"Allow negative offsets"
			);

			Assert.That(
				CrystalJson.Serialize(new NodaTime.LocalDateTime(2012, 1, 2, 3, 4, 5, 6).PlusTicks(7).WithOffset(NodaTime.Offset.FromHoursAndMinutes(-1, -30) + NodaTime.Offset.FromMilliseconds(-1234))),
				Is.EqualTo("\"2012-01-02T03:04:05.0060007-01:30\""),
				"Seconds and milliseconds in timezone offset should be dropped"
			);

			#endregion

			#region Offset...

			Assert.That(CrystalJson.Serialize(NodaTime.Offset.Zero), Is.EqualTo("\"+00\""));
			Assert.That(CrystalJson.Serialize(NodaTime.Offset.FromHours(2)), Is.EqualTo("\"+02\""));
			Assert.That(CrystalJson.Serialize(NodaTime.Offset.FromHoursAndMinutes(1, 30)), Is.EqualTo("\"+01:30\""));
			Assert.That(CrystalJson.Serialize(NodaTime.Offset.FromHoursAndMinutes(-1, -30)), Is.EqualTo("\"-01:30\""));

			#endregion
		}

		[Test]
		public void Test_JsonSerialize_DateTimeZone()
		{
			var rnd = new Random();
			int index = rnd.Next(NodaTime.DateTimeZoneProviders.Tzdb.Ids.Count);
			var id = NodaTime.DateTimeZoneProviders.Tzdb.Ids[index];
			var zone = NodaTime.DateTimeZoneProviders.Tzdb.GetZoneOrNull(id);
			Assert.That(CrystalJson.Serialize(zone), Is.EqualTo("\"" + id + "\""));
		}

		[Test]
		public void Test_JsonSerialize_Bytes()
		{
			{ // byte[]

				Assert.That(CrystalJson.Serialize(default(byte[])), Is.EqualTo("null"));
				Assert.That(CrystalJson.Serialize(Array.Empty<byte>()), Is.EqualTo(@""""""));

				// note: les binaires sont Base64 encodés!
				var arrayOfBytes = new byte[] {65, 0, 42, 255, 32};
				Assert.That(CrystalJson.Serialize(arrayOfBytes), Is.EqualTo(@"""QQAq/yA="""));

				// random
				arrayOfBytes = new byte[16];
				new Random().NextBytes(arrayOfBytes);
				Assert.That(CrystalJson.Serialize(arrayOfBytes), Is.EqualTo("\"" + Convert.ToBase64String(arrayOfBytes) + "\""), "Random Data!");
			}

			{ //  ArraySegment<byte>
				// note: les binaires sont Base64 encodés!

				Assert.That(CrystalJson.Serialize(default(ArraySegment<byte>)), Is.EqualTo("null"));
				Assert.That(CrystalJson.Serialize(new ArraySegment<byte>(Array.Empty<byte>())), Is.EqualTo(@""""""));

				var arraySegment = new ArraySegment<byte>(new byte[] {123, 123, 123, 65, 0, 42, 255, 32, 123 }, 3, 5);
				Assert.That(CrystalJson.Serialize(arraySegment), Is.EqualTo(@"""QQAq/yA="""));

				// random
				var bytes = new byte[32];
				new Random().NextBytes(bytes);
				arraySegment = new ArraySegment<byte>(bytes, 8, 16);
				Assert.That(CrystalJson.Serialize(arraySegment), Is.EqualTo("\"" + Convert.ToBase64String(bytes, 8, 16) + "\""), "Random Data!");
			}

			{ // Slice
				Assert.That(CrystalJson.Serialize(Slice.Nil), Is.EqualTo("null"));
				Assert.That(CrystalJson.Serialize(Slice.Empty), Is.EqualTo(@""""""));

				var slice = new byte[] { 123, 123, 123, 65, 0, 42, 255, 32, 123 }.AsSlice(3, 5);
				Assert.That(CrystalJson.Serialize(slice), Is.EqualTo(@"""QQAq/yA="""));

				// random
				var bytes = new byte[32];
				new Random().NextBytes(bytes);
				slice = bytes.AsSlice(8, 16);
				Assert.That(CrystalJson.Serialize(slice), Is.EqualTo("\"" + Convert.ToBase64String(bytes, 8, 16) + "\""), "Random Data!");
			}

		}

		[Test]
		public void Test_JsonDeserialize_Bytes()
		{
			{ // byte[]

				Assert.That(CrystalJson.Deserialize<byte[]>("null"), Is.Null);
				Assert.That(CrystalJson.Deserialize<byte[]>(@""""""), Is.EqualTo(Array.Empty<byte>()));

				// note: les binaires sont Base64 encodés!
				Assert.That(CrystalJson.Deserialize<byte[]>(@"""QQAq/yA="""), Is.EqualTo(new byte[] { 65, 0, 42, 255, 32 }));

				// random
				var bytes = new byte[16];
				new Random().NextBytes(bytes);
				Assert.That(CrystalJson.Deserialize<byte[]>("\"" + Convert.ToBase64String(bytes) + "\""), Is.EqualTo(bytes), "Random Data!");
			}

			{ //  ArraySegment<byte>
			  // note: les binaires sont Base64 encodés!

				Assert.That(CrystalJson.Deserialize<ArraySegment<byte>>("null"), Is.EqualTo(default(ArraySegment<byte>)));
				Assert.That(CrystalJson.Deserialize<ArraySegment<byte>>(@""""""), Is.EqualTo(new ArraySegment<byte>(Array.Empty<byte>())));

				Assert.That(CrystalJson.Deserialize<ArraySegment<byte>>(@"""QQAq/yA="""), Is.EqualTo(new ArraySegment<byte>(new byte[] { 65, 0, 42, 255, 32 })));

				// random
				var bytes = new byte[32];
				new Random().NextBytes(bytes);
				Assert.That(CrystalJson.Deserialize<ArraySegment<byte>>("\"" + Convert.ToBase64String(bytes) + "\""), Is.EqualTo(new ArraySegment<byte>(bytes)), "Random Data!");
			}

			{ // Slice
				Assert.That(CrystalJson.Deserialize<Slice>("null"), Is.EqualTo(Slice.Nil));
				Assert.That(CrystalJson.Deserialize<Slice>(@""""""), Is.EqualTo(Slice.Empty));

				Assert.That(CrystalJson.Deserialize<Slice>(@"""QQAq/yA="""), Is.EqualTo(new byte[] { 65, 0, 42, 255, 32 }.AsSlice()));

				// random
				var bytes = new byte[32];
				new Random().NextBytes(bytes);
				Assert.That(CrystalJson.Deserialize<Slice>("\"" + Convert.ToBase64String(bytes) + "\""), Is.EqualTo(bytes.AsSlice()), "Random Data!");
			}

		}

		[Test]
		public void Test_JsonSerialize_Dictionary()
		{
			// Les clés sont transformées en string
			// En JSON les clés doivent toujours être avec des "..."
			// En JavaScript, les clés qui sont des identifiants valides n'ont pas de '...', sinon elle sont escaped

			var dicOfStrings = new Dictionary<string, string>
			{
				["foo"] = "bar",
				["narf"] = "zort",
				["123"] = "456",
				["all your bases"] = "are belong to us"
			};
			// en JSON
			string expected = """{ "foo": "bar", "narf": "zort", "123": "456", "all your bases": "are belong to us" }""";
			Assert.That(CrystalJson.Serialize(dicOfStrings), Is.EqualTo(expected), "JSON");
			// en JS
			expected = "{ foo: 'bar', narf: 'zort', '123': '456', 'all your bases': 'are belong to us' }";
			Assert.That(CrystalJson.Serialize(dicOfStrings, CrystalJsonSettings.JavaScript), Is.EqualTo(expected), "JavaScript");

			var dicOfInts = new Dictionary<string, int>
			{
				["foo"] = 123,
				["bar"] = 456
			};
			// en JSON
			expected = """{ "foo": 123, "bar": 456 }""";
			Assert.That(CrystalJson.Serialize(dicOfInts), Is.EqualTo(expected));

			var dicOfObjects = new Dictionary<string, Tuple<int, string>>
			{
				["foo"] = new Tuple<int, string>(123, "bar"),
				["narf"] = new Tuple<int, string>(456, "zort")
			};
			// en JSON
			expected = """{ "foo": { "Item1": 123, "Item2": "bar" }, "narf": { "Item1": 456, "Item2": "zort" } }""";
			Assert.That(CrystalJson.Serialize(dicOfObjects), Is.EqualTo(expected));
			// en JS (note: les clés restent en string, par précaution !)
			expected = "{ 'foo': { Item1: 123, Item2: 'bar' }, 'narf': { Item1: 456, Item2: 'zort' } }";
			Assert.That(CrystalJson.Serialize(dicOfObjects, CrystalJsonSettings.JavaScript), Is.EqualTo(expected));
		}

		[Test]
		public void Test_JsonDeserialize_Dictionary()
		{
			// key => string
			var text = @"{ ""hello"": ""World"", ""foo"": 123, ""bar"": true }";
			var obj = CrystalJson.ParseObject(text);
			Assert.That(obj, Is.Not.Null.And.InstanceOf<JsonObject>());

			var dic = obj.As<Dictionary<string, string>>();
			Assert.That(dic, Is.Not.Null);

			Assert.That(dic.ContainsKey("hello"), Is.True, "dic[hello]");
			Assert.That(dic.ContainsKey("foo"), Is.True, "dic[foo]");
			Assert.That(dic.ContainsKey("bar"), Is.True, "dic[bar]");

			Assert.That(dic["hello"], Is.EqualTo("World"));
			Assert.That(dic["foo"], Is.EqualTo("123"));
			Assert.That(dic["bar"], Is.EqualTo("true"));

			Assert.That(dic.Count, Is.EqualTo(3));

			// key => int
			text = @"{ ""1"": ""Hello World"", ""42"": ""Narf!"", ""007"": ""James Bond"" }";
			obj = CrystalJson.ParseObject(text);
			Assert.That(obj, Is.Not.Null.And.InstanceOf<JsonObject>());

			var dicInt = obj.As<Dictionary<int, string>>();
			Assert.That(dicInt, Is.Not.Null);

			Assert.That(dicInt.ContainsKey(1), Is.True, "dicInt[1]");
			Assert.That(dicInt.ContainsKey(7), Is.True, "dicInt[7]");
			Assert.That(dicInt.ContainsKey(42), Is.True, "dicInt[42]");

			Assert.That(dicInt[1], Is.EqualTo("Hello World"));
			Assert.That(dicInt[7], Is.EqualTo("James Bond"));
			Assert.That(dicInt[42], Is.EqualTo("Narf!"));

			Assert.That(dicInt.Count, Is.EqualTo(3));
		}

		[Test]
		public void Test_JsonSerialize_Composite()
		{
			var composite = new
			{
				Id = 1,
				Title = "The Big Bang Theory",
				Cancelled = false, // (j'espère que c'est toujours le cas ^^; )
				Cast = new[] {
					new { Character="Sheldon Cooper", Actor="Jim Parsons", Female=false },
					new { Character="Leonard Hofstadter", Actor="Johny Galecki", Female=false },
					new { Character="Penny", Actor="Kaley Cuoco", Female=true },
					new { Character="Howard Wolowitz", Actor="Simon Helberg", Female=false },
					new { Character="Raj Koothrappali", Actor="Kunal Nayyar", Female=false },
				},
				Seasons = 4,
				ScoreIMDB = 8.4, // (26/10/2010)
				Producer = "Chuck Lorre Productions",
				PilotAirDate = new DateTime(2007, 9, 24, 0, 0, 0, DateTimeKind.Utc), // plus simple si UTC
			};

			// JSON
			string expected = @"{ ""Id"": 1, ""Title"": ""The Big Bang Theory"", ""Cancelled"": false"
			+ @", ""Cast"": [ "
			+ /**/@"{ ""Character"": ""Sheldon Cooper"", ""Actor"": ""Jim Parsons"", ""Female"": false }, "
			+ /**/@"{ ""Character"": ""Leonard Hofstadter"", ""Actor"": ""Johny Galecki"", ""Female"": false }, "
			+ /**/@"{ ""Character"": ""Penny"", ""Actor"": ""Kaley Cuoco"", ""Female"": true }, "
			+ /**/@"{ ""Character"": ""Howard Wolowitz"", ""Actor"": ""Simon Helberg"", ""Female"": false }, "
			+ /**/@"{ ""Character"": ""Raj Koothrappali"", ""Actor"": ""Kunal Nayyar"", ""Female"": false } "
			+ @"], ""Seasons"": 4, ""ScoreIMDB"": 8.4, ""Producer"": ""Chuck Lorre Productions"", ""PilotAirDate"": ""2007-09-24T00:00:00Z"" }";
			Assert.That(CrystalJson.Serialize(composite), Is.EqualTo(expected));

			// JS
			expected = "{ Id: 1, Title: 'The Big Bang Theory', Cancelled: false"
			+ ", Cast: [ "
			+ /**/"{ Character: 'Sheldon Cooper', Actor: 'Jim Parsons', Female: false }, "
			+ /**/"{ Character: 'Leonard Hofstadter', Actor: 'Johny Galecki', Female: false }, "
			+ /**/"{ Character: 'Penny', Actor: 'Kaley Cuoco', Female: true }, "
			+ /**/"{ Character: 'Howard Wolowitz', Actor: 'Simon Helberg', Female: false }, "
			+ /**/"{ Character: 'Raj Koothrappali', Actor: 'Kunal Nayyar', Female: false } "
			+ "], Seasons: 4, ScoreIMDB: 8.4, Producer: 'Chuck Lorre Productions', PilotAirDate: new Date(1190592000000) }";
			Assert.That(CrystalJson.Serialize(composite, CrystalJsonSettings.JavaScript), Is.EqualTo(expected));
		}

		[Test]
		public void Test_JsonSerialize_DataContract()
		{
			var x = new DummyDataContractClass();
			string expected = @"{ ""Id"": 0, ""Age"": 0, ""IsFemale"": false, ""VisibleProperty"": ""CanBeSeen"" }";
			Assert.That(CrystalJson.Serialize(x), Is.EqualTo(expected));
			// affiche les null
			expected = @"{ ""Id"": 0, ""Name"": null, ""Age"": 0, ""IsFemale"": false, ""CurrentLoveInterest"": null, ""VisibleProperty"": ""CanBeSeen"" }";
			Assert.That(CrystalJson.Serialize(x, CrystalJsonSettings.Json.WithNullMembers()), Is.EqualTo(expected));

			x.AgentId = 7;
			x.Name = "James Bond";
			x.Age = 69;
			x.Female = false;
			x.CurrentLoveInterest = "Miss Moneypenny";
			x.InvisibleField = "007";
			expected = @"{ ""Id"": 7, ""Name"": ""James Bond"", ""Age"": 69, ""IsFemale"": false, ""CurrentLoveInterest"": ""Miss Moneypenny"", ""VisibleProperty"": ""CanBeSeen"" }";
			Assert.That(CrystalJson.Serialize(x), Is.EqualTo(expected));
		}

		[Test]
		public void Test_JsonSerialize_XmlIgnore()
		{
			var x = new DummyXmlSerializableContractClass();

			string expected = @"{ ""Id"": 0, ""Age"": 0, ""IsFemale"": false, ""VisibleProperty"": ""CanBeSeen"" }";
			Assert.That(CrystalJson.Serialize(x), Is.EqualTo(expected));
			expected = @"{ ""Id"": 0, ""Name"": null, ""Age"": 0, ""IsFemale"": false, ""CurrentLoveInterest"": null, ""VisibleProperty"": ""CanBeSeen"" }";
			Assert.That(CrystalJson.Serialize(x, CrystalJsonSettings.Json.WithNullMembers()), Is.EqualTo(expected));

			x.AgentId = 7;
			x.Name = "James Bond";
			x.Age = 69;
			x.CurrentLoveInterest = "Miss Moneypenny";
			x.InvisibleField = "007";
			expected = @"{ ""Id"": 7, ""Name"": ""James Bond"", ""Age"": 69, ""IsFemale"": false, ""CurrentLoveInterest"": ""Miss Moneypenny"", ""VisibleProperty"": ""CanBeSeen"" }";
			Assert.That(CrystalJson.Serialize(x), Is.EqualTo(expected));
		}

		[Test]
		public void Test_JsonSerialize_Large_List_To_Disk()
		{
			const int N = 100 * 1000;

			var rnd = new Random(1234567890);

			var list = new List<string>();
			for (int i = 0; i < N; i++)
			{
				var rounds = rnd.Next(8) + 1;
				var str = String.Empty;
				for (int k = 0; k < rounds; k++)
				{
					str += new string((char)(rnd.Next(64) + 33), 4);
				}
				list.Add(str);
			}

			{ // WARMUP
				var x = CrystalJson.ToBuffer(list);
				_ = x.ZstdCompress(0);
				_ = x.DeflateCompress(CompressionLevel.Optimal);
				_ = x.GzipCompress(CompressionLevel.Optimal);
			}

			// Clear Text

			string path =  GetTemporaryPath("foo.json");
			File.Delete(path);

			Log("Writing to {0}", path);
			var sw = Stopwatch.StartNew();
			CrystalJson.SaveTo(path, list, CrystalJsonSettings.JsonCompact.ExpectLargeData());
			sw.Stop();
			Assert.That(File.Exists(path), Is.True, "Should have created a file at " + path);
			long rawSize = new FileInfo(path).Length;
			Log($"RAW    : Saved {rawSize,9:N0} bytes in {sw.Elapsed.TotalMilliseconds:N1} ms");


			// relit le fichier
			string text = File.ReadAllText(path);
			Assert.That(text, Is.Not.Null.Or.Empty, "File should contain stuff");
			// désérialise
			var reloaded = CrystalJson.Deserialize<string[]>(text);
			Assert.That(reloaded, Is.Not.Null);
			Assert.That(reloaded.Count, Is.EqualTo(list.Count));
			for (int i = 0; i < list.Count; i++)
			{
				Assert.That(reloaded[i], Is.EqualTo(list[i]), $"Mismatch at index {i}");
			}

			{ // Compresse Deflate
				path = GetTemporaryPath("foo.json.deflate");
				File.Delete(path);

				sw.Restart();
				var data = CrystalJson.ToBuffer(list, CrystalJsonSettings.JsonCompact.ExpectLargeData());
				using (var fs = File.Create(path))
				{
					fs.Write(data.DeflateCompress(CompressionLevel.Optimal).Span);
				}
				sw.Stop();
				Assert.That(File.Exists(path), Is.True, "Should have created a file");
				Log($"Deflate: Saved {new FileInfo(path).Length,9:N0} bytes (1 : {(1.0 * rawSize / new FileInfo(path).Length):F2}) in {sw.Elapsed.TotalMilliseconds:N1} ms");
			}

			{ // Compresse GZip
				path = GetTemporaryPath("foo.json.gz");
				File.Delete(path);

				sw.Restart();
				var data = CrystalJson.ToBuffer(list, CrystalJsonSettings.JsonCompact.ExpectLargeData());
				using (var fs = File.Create(path))
				{
					fs.Write(data.GzipCompress(CompressionLevel.Optimal).Span);
				}
				sw.Stop();

				Assert.That(File.Exists(path), Is.True, "Should have created a file");
				Log($"GZip -5: Saved {new FileInfo(path).Length,9:N0} bytes (1 : {(1.0 * rawSize / new FileInfo(path).Length):F2}) in {sw.Elapsed.TotalMilliseconds:N1} ms");
			}

			{ // Compresse ZSTD -1
				path = GetTemporaryPath("foo.json.1.zstd");
				File.Delete(path);

				sw.Restart();
				var data = CrystalJson.ToBuffer(list, CrystalJsonSettings.JsonCompact.ExpectLargeData());
				File.WriteAllBytes(path, data.ZstdCompress(1).GetBytes());
				sw.Stop();
				Assert.That(File.Exists(path), Is.True, "Should have created a file");
				Log($"ZSTD -1: Saved {new FileInfo(path).Length,9:N0} bytes (1 : {(1.0 * rawSize / new FileInfo(path).Length):F2}) in {sw.Elapsed.TotalMilliseconds:N1} ms");
			}
			{ // Compresse ZSTD -3
				path = GetTemporaryPath("foo.json.3.zstd");
				File.Delete(path);

				sw.Restart();
				var data = CrystalJson.ToBuffer(list, CrystalJsonSettings.JsonCompact.ExpectLargeData());
				File.WriteAllBytes(path, data.ZstdCompress(3).GetBytes());
				sw.Stop();
				Assert.That(File.Exists(path), Is.True, "Should have created a file");
				Log($"ZSTD -3: Saved {new FileInfo(path).Length,9:N0} bytes (1 : {(1.0 * rawSize / new FileInfo(path).Length):F2}) in {sw.Elapsed.TotalMilliseconds:N1} ms");
			}
			{ // Compresse ZSTD -5
				path = GetTemporaryPath("foo.json.5.zstd");
				File.Delete(path);

				sw.Restart();
				var data = CrystalJson.ToBuffer(list, CrystalJsonSettings.JsonCompact.ExpectLargeData());
				File.WriteAllBytes(path, data.ZstdCompress(5).GetBytes());
				sw.Stop();
				Assert.That(File.Exists(path), Is.True, "Should have created a file");
				Log($"ZSTD -5: Saved {new FileInfo(path).Length,9:N0} bytes (1 : {(1.0 * rawSize / new FileInfo(path).Length):F2}) in {sw.Elapsed.TotalMilliseconds:N1} ms");
			}
			{ // Compresse ZSTD -9
				path = GetTemporaryPath("foo.json.9.zstd");
				File.Delete(path);

				sw.Restart();
				var data = CrystalJson.ToBuffer(list, CrystalJsonSettings.JsonCompact.ExpectLargeData());
				File.WriteAllBytes(path, data.ZstdCompress(9).GetBytes());
				sw.Stop();
				Assert.That(File.Exists(path), Is.True, "Should have created a file");
				Log($"ZSTD -9: Saved {new FileInfo(path).Length,9:N0} bytes (1 : {(1.0 * rawSize / new FileInfo(path).Length):F2}) in {sw.Elapsed.TotalMilliseconds:N1} ms");
			}
			{ // Compresse ZSTD -20
				path = GetTemporaryPath("foo.json.20.zstd");
				File.Delete(path);

				sw.Restart();
				var data = CrystalJson.ToBuffer(list, CrystalJsonSettings.JsonCompact.ExpectLargeData());
				File.WriteAllBytes(path, data.ZstdCompress(20).GetBytes());
				sw.Stop();
				Assert.That(File.Exists(path), Is.True, "Should have created a file");
				Log($"ZSTD -20: Saved {new FileInfo(path).Length,9:N0} bytes (1 : {(1.0 * rawSize / new FileInfo(path).Length):F2}) in {sw.Elapsed.TotalMilliseconds:N1} ms");
			}

#if false
			// relit le fichier
			byte[] data = File.ReadAllBytes(path);
			Assert.That(data, Is.Not.Null, "File should contain stuff");
			Assert.AreNotEqual(0, data.Length, "File should contain stuff");
			text = new StreamReader(new System.IO.Compression.GZipStream(new MemoryStream(data), System.IO.Compression.CompressionMode.Decompress)).ReadToEnd();
			// désérialise
			reloaded = CrystalJson.DeserializeArray<string>(text);

			Assert.That(reloaded.Count, Is.EqualTo(list.Count));
			for (int i = 0; i < list.Count; i++)
			{
				Assert.That(reloaded[i], Is.EqualTo(list[i]), "Mismatch at index {0} (COMPRESSED)", i);
			}

			// Cryptoed
#endif

		}

		[Test]
		public void Test_JsonSerialize_Version()
		{
			Version ver;

			ver = new Version(1, 0);
			Assert.That(CrystalJson.Serialize(ver), Is.EqualTo("\"1.0\""));

			ver = new Version(1, 2, 3);
			Assert.That(CrystalJson.Serialize(ver), Is.EqualTo("\"1.2.3\""));

			ver = new Version(1, 2, 3, 4);
			Assert.That(CrystalJson.Serialize(ver), Is.EqualTo("\"1.2.3.4\""));
		}

		[Test]
		public void Test_JsonSerialize_KeyValuePair()
		{
			// les KeyValuePair<K, V> qui ne font pas partie d'un dictionnaire, sont sérialisés sous la forme '[KEY, VALUE]', plutot que '{ "Key": KEY, "Value": VALUE }', pour être plus compacte
			// La seule exception est pour une collection de KVP de même type, qui sera traitée comme un dictionnaire

			Assert.That(CrystalJson.Serialize(new KeyValuePair<string, int>("hello", 42), CrystalJsonSettings.Json), Is.EqualTo(@"[ ""hello"", 42 ]"));
			Assert.That(CrystalJson.Serialize(new KeyValuePair<int, bool>(123, true), CrystalJsonSettings.Json), Is.EqualTo("[ 123, true ]"));

			Assert.That(CrystalJson.Serialize(default(KeyValuePair<string, int>), CrystalJsonSettings.Json), Is.EqualTo("[ null, 0 ]"));
			Assert.That(CrystalJson.Serialize(default(KeyValuePair<int, bool>), CrystalJsonSettings.Json), Is.EqualTo("[ 0, false ]"));

			Assert.That(CrystalJson.Serialize(default(KeyValuePair<string, int>), CrystalJsonSettings.Json.WithoutDefaultValues()), Is.EqualTo("[ null, 0 ]"));
			Assert.That(CrystalJson.Serialize(default(KeyValuePair<int, bool>), CrystalJsonSettings.Json.WithoutDefaultValues()), Is.EqualTo("[ 0, false ]"));

			var blarf = KeyValuePair.Create(KeyValuePair.Create("hello", KeyValuePair.Create("narf", 42)), KeyValuePair.Create(123, KeyValuePair.Create("zort", TimeSpan.Zero)));
			Assert.That(CrystalJson.Serialize(blarf, CrystalJsonSettings.Json), Is.EqualTo(@"[ [ ""hello"", [ ""narf"", 42 ] ], [ 123, [ ""zort"", 0 ] ] ]"));
		}

		[Test]
		public void Test_JsonValue_FromValue_KeyValuePair()
		{
			// les KeyValuePair<K, V> qui ne font pas partie d'un dictionnaire, sont sérialisés sous la forme '[KEY, VALUE]', plutot que '{ "Key": KEY, "Value": VALUE }', pour être plus compacte
			// La seule exception est pour une collection de KVP de même type, qui sera traitée comme un dictionnaire

			Assert.That(JsonValue.FromValue(new KeyValuePair<string, int>("hello", 42)).ToJson(), Is.EqualTo(@"[ ""hello"", 42 ]"));
			Assert.That(JsonValue.FromValue(new KeyValuePair<int, bool>(123, true)).ToJson(), Is.EqualTo("[ 123, true ]"));

			Assert.That(JsonValue.FromValue(default(KeyValuePair<string, int>)).ToJson(), Is.EqualTo("[ null, 0 ]"));
			Assert.That(JsonValue.FromValue(default(KeyValuePair<int, bool>)).ToJson(), Is.EqualTo("[ 0, false ]"));

			var blarf = KeyValuePair.Create(KeyValuePair.Create("hello", KeyValuePair.Create("narf", 42)), KeyValuePair.Create(123, KeyValuePair.Create("zort", TimeSpan.Zero)));
			Assert.That(JsonValue.FromValue(blarf).ToJson(), Is.EqualTo(@"[ [ ""hello"", [ ""narf"", 42 ] ], [ 123, [ ""zort"", 0 ] ] ]"));
		}

		[Test]
		public void Test_JsonDeserialize_KeyValuePair()
		{
			// array variant: [Key, Value]
			Assert.That(CrystalJson.Deserialize<KeyValuePair<string, int>>("[\"hello\",42]"), Is.EqualTo(KeyValuePair.Create("hello", 42)));
			Assert.That(CrystalJson.Deserialize<KeyValuePair<int, bool>>("[123,true]"), Is.EqualTo(KeyValuePair.Create(123, true)));

			Assert.That(CrystalJson.Deserialize<KeyValuePair<string, int>>("[null,0]"), Is.EqualTo(default(KeyValuePair<string, int>)));
			Assert.That(CrystalJson.Deserialize<KeyValuePair<string, int>>("[]"), Is.EqualTo(default(KeyValuePair<string, int>)));
			Assert.That(CrystalJson.Deserialize<KeyValuePair<int, bool>>("[0,false]"), Is.EqualTo(default(KeyValuePair<int, bool>)));
			Assert.That(CrystalJson.Deserialize<KeyValuePair<int, bool>>("[]"), Is.EqualTo(default(KeyValuePair<int, bool>)));

			Assert.That(() => CrystalJson.Deserialize<KeyValuePair<string, int>>("[\"hello\",123,true]"), Throws.InstanceOf<InvalidOperationException>());
			Assert.That(() => CrystalJson.Deserialize<KeyValuePair<string, int>>("[\"hello\"]"), Throws.InstanceOf<InvalidOperationException>());

			// object-variant: {Key:.., Value:..}
			Assert.That(CrystalJson.Deserialize<KeyValuePair<string, int>>("{ \"Key\": \"hello\", \"Value\": 42 }"), Is.EqualTo(KeyValuePair.Create("hello", 42)));
			Assert.That(CrystalJson.Deserialize<KeyValuePair<int, bool>>("{ \"Key\": 123, \"Value\": true }]"), Is.EqualTo(KeyValuePair.Create(123, true)));

			Assert.That(CrystalJson.Deserialize<KeyValuePair<string, int>>("{ \"Key\": null, \"Value\": 0 }"), Is.EqualTo(default(KeyValuePair<string, int>)));
			Assert.That(CrystalJson.Deserialize<KeyValuePair<string, int>>("{}"), Is.EqualTo(default(KeyValuePair<string, int>)));
			Assert.That(CrystalJson.Deserialize<KeyValuePair<int, bool>>("{ \"Key\": 0, \"Value\": false }"), Is.EqualTo(default(KeyValuePair<int, bool>)));
			Assert.That(CrystalJson.Deserialize<KeyValuePair<int, bool>>("{}"), Is.EqualTo(default(KeyValuePair<int, bool>)));
		}

		[Test]
		public void Test_JsonSerialize_Uri()
		{
			Assert.That(CrystalJson.Serialize(new Uri("http://google.com")), Is.EqualTo(@"""http://google.com"""));
			Assert.That(CrystalJson.Serialize(new Uri("http://www.doxense.com/")), Is.EqualTo(@"""http://www.doxense.com/"""));
			Assert.That(CrystalJson.Serialize(new Uri("https://www.youtube.com/watch?v=dQw4w9WgXcQ")), Is.EqualTo(@"""https://www.youtube.com/watch?v=dQw4w9WgXcQ"""));
			Assert.That(CrystalJson.Serialize(new Uri("ftp://root:hunter2@ftp.corporate.com/public/_/COM1:/_/__/Warez/MovieZ/Valhalla_Rising_(2009)_1080p_BrRip_x264_-_YIFY.mkv")), Is.EqualTo(@"""ftp://root:hunter2@ftp.corporate.com/public/_/COM1:/_/__/Warez/MovieZ/Valhalla_Rising_(2009)_1080p_BrRip_x264_-_YIFY.mkv"""));
			Assert.That(CrystalJson.Serialize(default(Uri)), Is.EqualTo("null"));
		}

		[Test]
		public void Test_JsonDeserialize_Uri()
		{
			Assert.That(CrystalJson.Deserialize<Uri>(@"""http://google.com"""), Is.EqualTo(new Uri("http://google.com")));
			Assert.That(CrystalJson.Deserialize<Uri>(@"""http://www.doxense.com/"""), Is.EqualTo(new Uri("http://www.doxense.com/")));
			Assert.That(CrystalJson.Deserialize<Uri>(@"""https://www.youtube.com/watch?v=dQw4w9WgXcQ"""), Is.EqualTo(new Uri("https://www.youtube.com/watch?v=dQw4w9WgXcQ")));
			Assert.That(CrystalJson.Deserialize<Uri>(@"""ftp://root:hunter2@ftp.corporate.com/public/_/COM1:/_/__/Warez/MovieZ/Valhalla_Rising_(2009)_1080p_BrRip_x264_-_YIFY.mkv"""), Is.EqualTo(new Uri("ftp://root:hunter2@ftp.corporate.com/public/_/COM1:/_/__/Warez/MovieZ/Valhalla_Rising_(2009)_1080p_BrRip_x264_-_YIFY.mkv")));
			Assert.That(CrystalJson.Deserialize<Uri>("null"), Is.EqualTo(default(Uri)));
		}

		#endregion

		#region JSON Object Model...

		[Test]
		public void Test_JsonNull_Explicit()
		{
			var jnull = JsonNull.Null;
			Assert.That(jnull, Is.Not.Null);
			Assert.That(jnull, Is.InstanceOf<JsonNull>());
			Assert.That(JsonNull.Null, Is.SameAs(jnull), "JsonNull.Null should be a singleton");

			var value = (JsonNull)jnull;
			Assert.That(value.Type, Is.EqualTo(JsonType.Null), "value.Type");
			Assert.That(value.IsNull, Is.True, "value.IsNull");
			Assert.That(value.IsMissing, Is.False, "value.IsMissing");
			Assert.That(value.IsError, Is.False, "value.IsError");
			Assert.That(value.IsDefault, Is.True, "value.IsDefault");
			Assert.That(value.ToObject(), Is.Null, "value.ToObject()");
			Assert.That(value.ToString(), Is.EqualTo(""), "value.ToString()");

			Assert.That(value.Equals(JsonNull.Null), Is.True, "EQ null");
			Assert.That(value.Equals(JsonNull.Missing), Is.False, "NEQ missing");
			Assert.That(value.Equals(JsonNull.Error), Is.False, "NEQ error");
			Assert.That(value.Equals(default(JsonValue)), Is.True);
			Assert.That(value.Equals(default(object)), Is.True);

			// on doit tester certain cas particulieres pour le binding de Null:
			// - pour des Value Type, null doit se binder en le default(T) correspondant (ex: JsonNull.Null.As<int>() => 0)
			// - pour les types JsonValue (et JsonNull), il doit se binder en le singleton JsonNull.Null
			// - pour les autre ref types, il doit se binder en 'null'

			{ // Bind(typeof(T), ...)
				Assert.That(jnull.Bind(typeof(string)), Is.Null);
				Assert.That(jnull.Bind(typeof(int)), Is.Zero);
				Assert.That(jnull.Bind(typeof(bool)), Is.False);
				Assert.That(jnull.Bind(typeof(Guid)), Is.EqualTo(Guid.Empty));
				Assert.That(jnull.Bind(typeof(int?)), Is.Null);
				Assert.That(jnull.Bind(typeof(string[])), Is.Null);
				Assert.That(jnull.Bind(typeof(List<string>)), Is.Null);
				Assert.That(jnull.Bind(typeof(IList<string>)), Is.Null);

				// special case
				Assert.That(jnull.Bind(typeof(JsonNull)), Is.SameAs(JsonNull.Null), "JsonNull.As<JsonNull>() should bind to JsonNull.Null, and not 'null' !");
				Assert.That(jnull.Bind(typeof(JsonValue)), Is.SameAs(JsonNull.Null), "JsonNull.As<JsonValue>() should bind to JsonNull.Null, and not 'null' !");
				Assert.That(jnull.Bind(typeof(JsonString)), Is.EqualTo(null), "JsonNull.As<JsonString>() should return null, because a JsonString instance cannot represent null itself!");
				Assert.That(jnull.Bind(typeof(JsonNumber)), Is.EqualTo(null), "JsonNull.As<JsonNumber>() should return null, because a JsonNumber instance cannot represent null itself!");
				Assert.That(jnull.Bind(typeof(JsonBoolean)), Is.EqualTo(null), "JsonNull.As<JsonBoolean>() should return null, because a JsonBoolean instance cannot represent null itself!");
				Assert.That(jnull.Bind(typeof(JsonObject)), Is.EqualTo(null), "JsonNull.As<JsonObject>() should return null, because a JsonObject instance cannot represent null itself!");
				Assert.That(jnull.Bind(typeof(JsonArray)), Is.EqualTo(null), "JsonNull.As<JsonArray>() should return null, because a JsonArray instance cannot represent null itself!");
			}

			{ // As<T>()
				Assert.That(jnull.As<string>(), Is.Null);
				Assert.That(jnull.As<int>(), Is.Zero);
				Assert.That(jnull.As<bool>(), Is.False);
				Assert.That(jnull.As<Guid>(), Is.EqualTo(Guid.Empty));
				Assert.That(jnull.As<int?>(), Is.Null);
				Assert.That(jnull.As<string[]>(), Is.Null);
				Assert.That(jnull.As<List<string>>(), Is.Null);
				Assert.That(jnull.As<IList<string>>(), Is.Null);

				// special case
				Assert.That(jnull.As<JsonNull>(), Is.SameAs(JsonNull.Null), "JsonNull.As<JsonNull>() should bind to JsonNull.Null, and not 'null' !");
				Assert.That(jnull.As<JsonValue>(), Is.SameAs(JsonNull.Null), "JsonNull.As<JsonValue>() should bind to JsonNull.Null, and not 'null' !");
				Assert.That(jnull.As<JsonString>(), Is.EqualTo(null), "JsonNull.As<JsonString>() should return null, because a JsonString instance cannot represent null itself!");
				Assert.That(jnull.As<JsonNumber>(), Is.EqualTo(null), "JsonNull.As<JsonNumber>() should return null, because a JsonNumber instance cannot represent null itself!");
				Assert.That(jnull.As<JsonBoolean>(), Is.EqualTo(null), "JsonNull.As<JsonBoolean>() should return null, because a JsonBoolean instance cannot represent null itself!");
				Assert.That(jnull.As<JsonObject>(), Is.EqualTo(null), "JsonNull.As<JsonObject>() should return null, because a JsonObject instance cannot represent null itself!");
				Assert.That(jnull.As<JsonArray>(), Is.EqualTo(null), "JsonNull.As<JsonArray>() should return null, because a JsonArray instance cannot represent null itself!");
			}

			{ // As<T>(required: false)
				Assert.That(jnull.As<string>(required: false), Is.Null);
				Assert.That(jnull.As<int>(required: false), Is.Zero);
				Assert.That(jnull.As<bool>(required: false), Is.False);
				Assert.That(jnull.As<Guid>(required: false), Is.EqualTo(Guid.Empty));
				Assert.That(jnull.As<int?>(required: false), Is.Null);
				Assert.That(jnull.As<string[]>(required: false), Is.Null);
				Assert.That(jnull.As<List<string>>(required: false), Is.Null);
				Assert.That(jnull.As<IList<string>>(required: false), Is.Null);

				// special case
				Assert.That(jnull.As<JsonNull>(required: false), Is.SameAs(JsonNull.Null), "JsonNull.As<JsonNull>() should bind to JsonNull.Null, and not 'null' !");
				Assert.That(jnull.As<JsonValue>(required: false), Is.SameAs(JsonNull.Null), "JsonNull.As<JsonValue>() should bind to JsonNull.Null, and not 'null' !");
				Assert.That(jnull.As<JsonString>(required: false), Is.EqualTo(null), "JsonNull.As<JsonString>() should return null, because a JsonString instance cannot represent null itself!");
				Assert.That(jnull.As<JsonNumber>(required: false), Is.EqualTo(null), "JsonNull.As<JsonNumber>() should return null, because a JsonNumber instance cannot represent null itself!");
				Assert.That(jnull.As<JsonBoolean>(required: false), Is.EqualTo(null), "JsonNull.As<JsonBoolean>() should return null, because a JsonBoolean instance cannot represent null itself!");
				Assert.That(jnull.As<JsonObject>(required: false), Is.EqualTo(null), "JsonNull.As<JsonObject>() should return null, because a JsonObject instance cannot represent null itself!");
				Assert.That(jnull.As<JsonArray>(required: false), Is.EqualTo(null), "JsonNull.As<JsonArray>() should return null, because a JsonArray instance cannot represent null itself!");
			}

			{ // As<T>(required: true)
				Assert.That(() => jnull.As<string>(required: true), Throws.InvalidOperationException);
				Assert.That(() => jnull.As<int>(required: true), Throws.InvalidOperationException);
				Assert.That(() => jnull.As<bool>(required: true), Throws.InvalidOperationException);
				Assert.That(() => jnull.As<Guid>(required: true), Throws.InvalidOperationException);
				Assert.That(() => jnull.As<int?>(required: true), Throws.InvalidOperationException);
				Assert.That(() => jnull.As<string[]>(required: true), Throws.InvalidOperationException);
				Assert.That(() => jnull.As<List<string>>(required: true), Throws.InvalidOperationException);
				Assert.That(() => jnull.As<IList<string>>(required: true), Throws.InvalidOperationException);
				Assert.That(() => jnull.As<JsonNull>(required: true), Throws.InvalidOperationException);
				Assert.That(() => jnull.As<JsonValue>(required: true), Throws.InvalidOperationException);
				Assert.That(() => jnull.As<JsonString>(required: true), Throws.InvalidOperationException);
			}

			{ // Embedded Fields with explicit null

				//note: class anonyme qui sert de template pour créer un classe inline
				var template = new
				{
					/*int*/ Int32 = 666,
					/*bool*/ Bool = true,
					/*string*/ String = "FAILED",
					/*Guid*/ Guid = Guid.Parse("66666666-6666-6666-6666-666666666666"),
					/*int?*/ NullInt32 = (int?) 666,
					/*bool?*/ NullBool = (bool?) true,
					/*Guid?*/ NullGuid = (Guid?) Guid.Parse("66666666-6666-6666-6666-666666666666"),
					/*JsonValue*/ JsonValue = (JsonValue) "FAILED",
					/*JsonNull*/ JsonString = (JsonString) "FAILED",
					/*JsonNull*/ JsonArray = JsonArray.Create("FAILED"),
					/*JsonNull*/ JsonObject = JsonObject.Create("FAILED", "EPIC"),
				};

				// si on désérialiser un objet dont tous les champs valent explicitement null, on option le "default" du type
				var j = JsonValue
					.ParseObject(@"{ ""Int32"": null, ""Bool"": null, ""String"": null, ""Guid"": null, ""NullInt32"": null, ""NullBool"": null, ""NullGuid"": null, ""JsonValue"": null, ""JsonNull"": null, ""JsonArray"": null, ""JsonObject"": null }")
					.As(defaultValue: template);

				Assert.That(j.Int32, Is.Zero);
				Assert.That(j.Bool, Is.False);
				Assert.That(j.String, Is.Null);
				Assert.That(j.Guid, Is.EqualTo(Guid.Empty));
				Assert.That(j.NullInt32, Is.Null);
				Assert.That(j.NullBool, Is.Null);
				Assert.That(j.NullGuid, Is.Null);
				Assert.That(j.JsonValue, Is.SameAs(JsonNull.Null), "Properties with type JsonValue should bind null into JsonNull.Null!");
				Assert.That(j.JsonString, Is.Null);
				Assert.That(j.JsonArray, Is.Null);
				Assert.That(j.JsonObject, Is.Null);
			}

			Assert.That(SerializeToSlice(jnull), Is.EqualTo(Slice.FromString("null")));
		}

		[Test]
		public void Test_JsonNull_Missing()
		{
			var jmissing = JsonNull.Missing;
			Assert.That(jmissing, Is.Not.Null);
			Assert.That(jmissing, Is.InstanceOf<JsonNull>());

			var value = (JsonNull)jmissing;
			Assert.That(value.Type, Is.EqualTo(JsonType.Null), "value.Type");
			Assert.That(value.IsNull, Is.True, "value.IsNull");
			Assert.That(value.IsMissing, Is.True, "value.IsMissing");
			Assert.That(value.IsError, Is.False, "value.IsError");
			Assert.That(value.IsDefault, Is.True, "value.IsDefault");
			Assert.That(value.ToObject(), Is.Null, "value.ToObject()");
			Assert.That(value.ToString(), Is.EqualTo(""), "value.ToString()");

			Assert.That(value.Equals(JsonNull.Missing), Is.True, "EQ missing");
			Assert.That(value.Equals(JsonNull.Null), Is.False, "NEQ null");
			Assert.That(value.Equals(JsonNull.Error), Is.False, "NEQ error");
			Assert.That(value.Equals(default(JsonValue)), Is.True);
			Assert.That(value.Equals(default(object)), Is.True);

			//note: normalement JsonNull.Missing se bind de la même manière que pour JsonNull, (=> default(T))
			// sauf que si T == JsonValue ou JsonNull, alors on doit retourner le même singleton (ie: JsonNull.Missing.As<JsonValue>() => JsonNull.Missing

			Assert.That(jmissing.Bind(typeof(JsonValue)), Is.SameAs(JsonNull.Missing));
			Assert.That(jmissing.As<JsonValue>(), Is.SameAs(JsonNull.Missing));
			Assert.That(jmissing.As<JsonValue>(required: false), Is.SameAs(JsonNull.Missing));
			Assert.That(jmissing.As<JsonValue>(CrystalJson.DefaultResolver), Is.SameAs(JsonNull.Missing));
			Assert.That(jmissing.As<JsonValue>(defaultValue: "hello"), Is.EqualTo("hello"));

			Assert.That(jmissing.Bind(typeof(JsonNull)), Is.SameAs(JsonNull.Missing));
			Assert.That(jmissing.As<JsonNull>(), Is.SameAs(JsonNull.Missing));
			Assert.That(jmissing.As<JsonNull>(required: false), Is.SameAs(JsonNull.Missing));
			Assert.That(jmissing.As<JsonNull>(CrystalJson.DefaultResolver), Is.SameAs(JsonNull.Missing));
			Assert.That(jmissing.As<JsonNull>(defaultValue: (JsonNull) JsonNull.Error), Is.SameAs(JsonNull.Error));

			Assert.That(SerializeToSlice(jmissing), Is.EqualTo(Slice.FromString("null")));
		}

		[Test]
		public void Test_JsonNull_Error()
		{
			var jerror = JsonNull.Error;
			Assert.That(jerror, Is.Not.Null);
			Assert.That(jerror, Is.InstanceOf<JsonNull>());

			var value = (JsonNull)jerror;
			Assert.That(value.Type, Is.EqualTo(JsonType.Null), "value.Type");
			Assert.That(value.IsNull, Is.True, "value.IsNull");
			Assert.That(value.IsMissing, Is.False, "value.IsMissing");
			Assert.That(value.IsError, Is.True, "value.IsError");
			Assert.That(value.IsDefault, Is.True, "value.IsDefault");
			Assert.That(value.ToObject(), Is.Null, "value.ToObject()");
			Assert.That(value.ToString(), Is.EqualTo(""), "value.ToString()");

			Assert.That(value.Equals(JsonNull.Error), Is.True, "EQ error");
			Assert.That(value.Equals(JsonNull.Null), Is.False, "NEQ null");
			Assert.That(value.Equals(JsonNull.Missing), Is.False, "NEQ missing");
			Assert.That(value.Equals(default(JsonValue)), Is.True);
			Assert.That(value.Equals(default(object)), Is.True);

			Assert.That(SerializeToSlice(jerror), Is.EqualTo(Slice.FromString("null")));
		}

		[Test]
		public void Test_JsonBoolean()
		{
			var value = JsonBoolean.True;
			Assert.That(value, Is.Not.Null, "JsonValue.True");
			Assert.That(value.Type, Is.EqualTo(JsonType.Boolean), "JsonValue.True.Type");
			Assert.That(value.IsNull, Is.False, "JsonValue.True.IsNull");
			Assert.That(value.IsDefault, Is.False, "JsonValue.True.IsDefault");
			Assert.That(value.ToObject(), Is.True, "JsonValue.True.ToObject()");
			Assert.That(value.ToString(), Is.EqualTo("true"), "JsonValue.True.ToString()");
			Assert.That(value.Equals(JsonBoolean.True), Is.True);
			Assert.That(value.Equals(JsonBoolean.False), Is.False);
			Assert.That(value.Equals(true), Is.True);
			Assert.That(value.Equals(false), Is.False);

			value = JsonBoolean.False;
			Assert.That(value, Is.Not.Null, "JsonValue.False");
			Assert.That(value.Type, Is.EqualTo(JsonType.Boolean), "JsonValue.False.Type");
			Assert.That(value.IsNull, Is.False, "JsonValue.False.IsNull");
			Assert.That(value.IsDefault, Is.True, "JsonValue.False.IsDefault");
			Assert.That(value.ToObject(), Is.False, "JsonValue.False.ToObject()");
			Assert.That(value.ToString(), Is.EqualTo("false"), "JsonValue.False.ToString()");
			Assert.That(value.Equals(JsonBoolean.True), Is.False);
			Assert.That(value.Equals(JsonBoolean.False), Is.True);
			Assert.That(value.Equals(true), Is.False);
			Assert.That(value.Equals(false), Is.True);

			// Nullables

			Assert.That(JsonBoolean.Return((bool?)null).Type, Is.EqualTo(JsonType.Null));
			Assert.That(JsonBoolean.Return((bool?)true).Type, Is.EqualTo(JsonType.Boolean));

			// Conversions

			Assert.That(JsonBoolean.False.ToString(), Is.EqualTo("false"));
			Assert.That(JsonBoolean.False.Bind(typeof(string)), Is.EqualTo("false"));
			Assert.That(JsonBoolean.True.ToString(), Is.EqualTo("true"));
			Assert.That(JsonBoolean.True.Bind(typeof(string)), Is.InstanceOf<string>());
			Assert.That(JsonBoolean.True.Bind(typeof(string)), Is.EqualTo("true"));

			Assert.That(JsonBoolean.False.ToInt32(), Is.EqualTo(0));
			Assert.That(JsonBoolean.False.Bind(typeof(int)), Is.EqualTo(0));
			Assert.That(JsonBoolean.True.ToInt32(), Is.EqualTo(1));
			Assert.That(JsonBoolean.True.Bind(typeof(int)), Is.InstanceOf<int>());
			Assert.That(JsonBoolean.True.Bind(typeof(int)), Is.EqualTo(1));

			Assert.That(JsonBoolean.False.ToInt64(), Is.EqualTo(0L));
			Assert.That(JsonBoolean.False.Bind(typeof(long)), Is.EqualTo(0L));
			Assert.That(JsonBoolean.True.ToInt64(), Is.EqualTo(1L));
			Assert.That(JsonBoolean.True.Bind(typeof(long)), Is.InstanceOf<long>());
			Assert.That(JsonBoolean.True.Bind(typeof(long)), Is.EqualTo(1L));

			Assert.That(JsonBoolean.False.ToSingle(), Is.EqualTo(0f));
			Assert.That(JsonBoolean.False.Bind(typeof(float)), Is.EqualTo(0f));
			Assert.That(JsonBoolean.True.ToSingle(), Is.EqualTo(1f));
			Assert.That(JsonBoolean.True.Bind(typeof(float)), Is.InstanceOf<float>());
			Assert.That(JsonBoolean.True.Bind(typeof(float)), Is.EqualTo(1f));

			Assert.That(JsonBoolean.False.ToDouble(), Is.EqualTo(0d));
			Assert.That(JsonBoolean.False.Bind(typeof(double)), Is.EqualTo(0d));
			Assert.That(JsonBoolean.True.ToDouble(), Is.EqualTo(1d));
			Assert.That(JsonBoolean.True.Bind(typeof(double)), Is.InstanceOf<double>());
			Assert.That(JsonBoolean.True.Bind(typeof(double)), Is.EqualTo(1d));

			Assert.That(JsonBoolean.False.ToDecimal(), Is.EqualTo(0m));
			Assert.That(JsonBoolean.False.Bind(typeof(decimal)), Is.EqualTo(0m));
			Assert.That(JsonBoolean.True.ToDecimal(), Is.EqualTo(1m));
			Assert.That(JsonBoolean.True.Bind(typeof(decimal)), Is.EqualTo(1m));
			Assert.That(JsonBoolean.True.Bind(typeof(decimal)), Is.InstanceOf<decimal>());

			Assert.That(JsonBoolean.False.ToGuid(), Is.EqualTo(Guid.Empty));
			Assert.That(JsonBoolean.False.Bind(typeof(Guid)), Is.EqualTo(Guid.Empty));
			Assert.That(JsonBoolean.True.ToGuid(), Is.EqualTo(Guid.Parse("FFFFFFFF-FFFF-FFFF-FFFF-FFFFFFFFFFFF")));
			Assert.That(JsonBoolean.True.Bind(typeof(Guid)), Is.EqualTo(Guid.Parse("FFFFFFFF-FFFF-FFFF-FFFF-FFFFFFFFFFFF")));
			Assert.That(JsonBoolean.False.Bind(typeof(Guid)), Is.InstanceOf<Guid>());

			Assert.That(SerializeToSlice(JsonBoolean.False), Is.EqualTo(Slice.FromString("false")), "False ~= \"False\"");
			Assert.That(SerializeToSlice(JsonBoolean.True), Is.EqualTo(Slice.FromString("true")), "True ~= \"True\"");
		}

		[Test]
		public void Test_JsonString()
		{
			{ // empty
				var jval = JsonString.Empty;
				Assert.That(jval, Is.Not.Null);
				Assert.That(jval.Type, Is.EqualTo(JsonType.String));
				Assert.That(jval, Is.InstanceOf<JsonString>());
				Assert.That(jval.IsNull, Is.False);
				Assert.That(jval.IsDefault, Is.False);
				Assert.That(jval.ToObject(), Is.EqualTo(""));
				Assert.That(jval.ToString(), Is.EqualTo(""));
				Assert.That(jval.Equals(JsonString.Empty), Is.True);
				Assert.That(jval.Equals(String.Empty), Is.True);
				Assert.That(jval.Equals(default(string)), Is.False);
				var jstr = (JsonString) jval;
				Assert.That(jstr.IsNullOrEmpty, Is.True);
				Assert.That(jstr.Length, Is.EqualTo(0));
				Assert.That(SerializeToSlice(jval), Is.EqualTo(Slice.FromString("\"\"")));
			}

			{ // from string
				var jval = JsonString.Return("Hello, World!");
				Assert.That(jval, Is.Not.Null);
				Assert.That(jval.Type, Is.EqualTo(JsonType.String));
				Assert.That(jval, Is.InstanceOf<JsonString>());
				Assert.That(jval.IsNull, Is.False);
				Assert.That(jval.IsDefault, Is.False);
				Assert.That(jval.ToObject(), Is.EqualTo("Hello, World!"));
				Assert.That(jval.ToString(), Is.EqualTo("Hello, World!"));
				Assert.That(jval.Equals("Hello, World!"), Is.True);
				Assert.That(jval.Equals(JsonString.Return("Hello, World!")), Is.True);
				var jstr = (JsonString) jval;
				Assert.That(jstr.IsNullOrEmpty, Is.False);
				Assert.That(jstr.Length, Is.EqualTo(13));
				Assert.That(SerializeToSlice(jval), Is.EqualTo(Slice.FromString("\"Hello, World!\"")));
			}

			{ // from StringBuilder
				var sb = new StringBuilder("Hello").Append(", World!");
				var jval = JsonString.Return(sb);
				Assert.That(jval, Is.InstanceOf<JsonString>());
				Assert.That(jval.ToStringOrDefault(), Is.EqualTo("Hello, World!"));
				Assert.That(jval.ToObject(), Is.EqualTo("Hello, World!"));
				Assert.That(jval.ToString(), Is.EqualTo("Hello, World!"));
				sb.Append("?");
				Assert.That(jval.ToStringOrDefault(), Is.EqualTo("Hello, World!"), "Mutating the original StringBuilder should not have any impact on the string");
			}

			{ // from Guid
				var jval = JsonString.Return(Guid.Parse("016f3491-9416-47e2-b627-f84c507056d8"));
				Assert.That(jval, Is.Not.Null);
				Assert.That(jval.Type, Is.EqualTo(JsonType.String));
				Assert.That(jval, Is.InstanceOf<JsonString>());
				Assert.That(jval.IsNull, Is.False);
				Assert.That(jval.IsDefault, Is.False);
				Assert.That(jval.ToObject(), Is.EqualTo("016f3491-9416-47e2-b627-f84c507056d8"));
				Assert.That(jval.ToString(), Is.EqualTo("016f3491-9416-47e2-b627-f84c507056d8"));
				Assert.That(jval.ToGuid(), Is.EqualTo(Guid.Parse("016f3491-9416-47e2-b627-f84c507056d8")));
				Assert.That(jval.Equals("016f3491-9416-47e2-b627-f84c507056d8"), Is.True);
				Assert.That(jval.Equals(Guid.Parse("016f3491-9416-47e2-b627-f84c507056d8")), Is.True);
				Assert.That(jval.Equals(JsonString.Return("016f3491-9416-47e2-b627-f84c507056d8")), Is.True);
				var jstr = (JsonString) jval;
				Assert.That(jstr.IsNullOrEmpty, Is.False);
				Assert.That(jstr.Length, Is.EqualTo(36));
				Assert.That(SerializeToSlice(jval), Is.EqualTo(Slice.FromString("\"016f3491-9416-47e2-b627-f84c507056d8\"")));
			}

			{ // from IP Address
				var jval = JsonString.Return(IPAddress.Parse("192.168.1.2"));
				Assert.That(jval, Is.Not.Null);
				Assert.That(jval.Type, Is.EqualTo(JsonType.String));
				Assert.That(jval, Is.InstanceOf<JsonString>());
				Assert.That(jval.ToObject(), Is.EqualTo("192.168.1.2"));
				Assert.That(jval.ToString(), Is.EqualTo("192.168.1.2"));
				Assert.That(jval.Equals("192.168.1.2"), Is.True);
				Assert.That(jval.Equals(JsonString.Return("192.168.1.2")), Is.True);
				Assert.That(SerializeToSlice(jval), Is.EqualTo(Slice.FromString("\"192.168.1.2\"")));

				Assert.That(JsonString.Return(default(IPAddress)), Is.EqualTo(JsonNull.Null));
				Assert.That(JsonString.Return(IPAddress.Loopback), Is.EqualTo((JsonString) "127.0.0.1"));
				Assert.That(JsonString.Return(IPAddress.Any), Is.EqualTo((JsonString) "0.0.0.0"));
				Assert.That(JsonString.Return(IPAddress.None), Is.EqualTo((JsonString) "255.255.255.255")); //note: None == Broadcast
				Assert.That(JsonString.Return(IPAddress.IPv6Loopback), Is.EqualTo((JsonString) "::1"));
				Assert.That(JsonString.Return(IPAddress.IPv6Any), Is.EqualTo((JsonString) "::")); //note: IPv6None == IPv6Any
			}

			{ // from Type

				// basic types: alias
				Assert.That(JsonString.Return(typeof(string)), Is.EqualTo((JsonValue) "string"));
				Assert.That(JsonString.Return(typeof(bool)), Is.EqualTo((JsonValue) "bool"));
				Assert.That(JsonString.Return(typeof(char)), Is.EqualTo((JsonValue) "char"));
				Assert.That(JsonString.Return(typeof(int)), Is.EqualTo((JsonValue) "int"));
				Assert.That(JsonString.Return(typeof(long)), Is.EqualTo((JsonValue) "long"));
				Assert.That(JsonString.Return(typeof(uint)), Is.EqualTo((JsonValue) "uint"));
				Assert.That(JsonString.Return(typeof(ulong)), Is.EqualTo((JsonValue) "ulong"));
				Assert.That(JsonString.Return(typeof(float)), Is.EqualTo((JsonValue) "float"));
				Assert.That(JsonString.Return(typeof(double)), Is.EqualTo((JsonValue) "double"));
				Assert.That(JsonString.Return(typeof(decimal)), Is.EqualTo((JsonValue) "decimal"));
				Assert.That(JsonString.Return(typeof(DateTime)), Is.EqualTo((JsonValue) "DateTime"));
				Assert.That(JsonString.Return(typeof(TimeSpan)), Is.EqualTo((JsonValue) "TimeSpan"));
				Assert.That(JsonString.Return(typeof(Guid)), Is.EqualTo((JsonValue) "Guid"));

				// basic nullable types: alias?
				Assert.That(JsonString.Return(typeof(bool?)), Is.EqualTo((JsonValue) "bool?"));
				Assert.That(JsonString.Return(typeof(char?)), Is.EqualTo((JsonValue) "char?"));
				Assert.That(JsonString.Return(typeof(int?)), Is.EqualTo((JsonValue) "int?"));
				Assert.That(JsonString.Return(typeof(long?)), Is.EqualTo((JsonValue) "long?"));
				Assert.That(JsonString.Return(typeof(uint?)), Is.EqualTo((JsonValue) "uint?"));
				Assert.That(JsonString.Return(typeof(ulong?)), Is.EqualTo((JsonValue) "ulong?"));
				Assert.That(JsonString.Return(typeof(float?)), Is.EqualTo((JsonValue) "float?"));
				Assert.That(JsonString.Return(typeof(double?)), Is.EqualTo((JsonValue) "double?"));
				Assert.That(JsonString.Return(typeof(decimal?)), Is.EqualTo((JsonValue) "decimal?"));
				Assert.That(JsonString.Return(typeof(DateTime?)), Is.EqualTo((JsonValue) "DateTime?"));
				Assert.That(JsonString.Return(typeof(TimeSpan?)), Is.EqualTo((JsonValue) "TimeSpan?"));
				Assert.That(JsonString.Return(typeof(Guid?)), Is.EqualTo((JsonValue) "Guid?"));

				// system types: Full Name
				Assert.That(
					JsonString.Return(typeof(List<string>)),
					Is.EqualTo((JsonValue) typeof(List<string>).FullName),
					"Core system types should only have NAMESPACE.NAME");

				// third party types: FullName + AssemblyName
				Assert.That(
					JsonString.Return(typeof(JsonValue)),
					Is.EqualTo((JsonValue) typeof(JsonValue).AssemblyQualifiedName),
					"Non-system types should have NEMESPACE.NAME, ASSEMBLY");

				Assert.That(JsonString.Return(default(Type)), Is.EqualTo(JsonNull.Null));
			}
		}

		[Test]
		public void Test_JsonString_Conversions()
		{

			// Conversions
			Assert.That(JsonString.Return("false").ToBoolean(), Is.False);
			Assert.That(JsonString.Return("false").Bind(typeof(bool)), Is.False);
			Assert.That(JsonString.Return("true").ToBoolean(), Is.True);
			Assert.That(JsonString.Return("true").Bind(typeof(bool)), Is.True);
			Assert.That(JsonString.Return("true").Bind(typeof(bool)), Is.InstanceOf<bool>());

			Assert.That(JsonString.Return("0").ToInt32(), Is.EqualTo(0));
			Assert.That(JsonString.Return("1").Bind(typeof(int)), Is.EqualTo(1));
			Assert.That(JsonString.Return("123").ToInt32(), Is.EqualTo(123));
			Assert.That(JsonString.Return("666666666").Bind(typeof(int)), Is.EqualTo(666666666));
			Assert.That(JsonString.Return("2147483647").Bind(typeof(int)), Is.EqualTo(int.MaxValue));
			Assert.That(JsonString.Return("-2147483648").Bind(typeof(int)), Is.EqualTo(int.MinValue));
			Assert.That(JsonString.Return("123").Bind(typeof(int)), Is.InstanceOf<int>());

			Assert.That(JsonString.Return("0").ToInt64(), Is.EqualTo(0));
			Assert.That(JsonString.Return("1").Bind(typeof(long)), Is.EqualTo(1));
			Assert.That(JsonString.Return("123").ToInt64(), Is.EqualTo(123));
			Assert.That(JsonString.Return("666666666").Bind(typeof(long)), Is.EqualTo(666666666));
			Assert.That(JsonString.Return("9223372036854775807").Bind(typeof(long)), Is.EqualTo(long.MaxValue));
			Assert.That(JsonString.Return("-9223372036854775808").Bind(typeof(long)), Is.EqualTo(long.MinValue));
			Assert.That(JsonString.Return("123").Bind(typeof(long)), Is.InstanceOf<long>());

			Assert.That(JsonString.Return("0").ToSingle(), Is.EqualTo(0f));
			Assert.That(JsonString.Return("1").Bind(typeof(float)), Is.EqualTo(1f));
			Assert.That(JsonString.Return("1.23").ToSingle(), Is.EqualTo(1.23f));
			Assert.That(JsonString.Return("3.14159274").Bind(typeof(float)), Is.EqualTo((float) Math.PI));
			Assert.That(JsonString.Return("NaN").Bind(typeof(float)), Is.EqualTo(float.NaN));
			Assert.That(JsonString.Return("Infinity").Bind(typeof(float)), Is.EqualTo(float.PositiveInfinity));
			Assert.That(JsonString.Return("-Infinity").Bind(typeof(float)), Is.EqualTo(float.NegativeInfinity));
			Assert.That(JsonString.Return("1.23").Bind(typeof(float)), Is.InstanceOf<float>());

			Assert.That(JsonString.Return("0").ToDouble(), Is.EqualTo(0d));
			Assert.That(JsonString.Return("1").Bind(typeof(double)), Is.EqualTo(1d));
			Assert.That(JsonString.Return("1.23").ToDouble(), Is.EqualTo(1.23d));
			Assert.That(JsonString.Return("3.1415926535897931").Bind(typeof(double)), Is.EqualTo(Math.PI));
			Assert.That(JsonString.Return("NaN").Bind(typeof(double)), Is.EqualTo(double.NaN));
			Assert.That(JsonString.Return("Infinity").Bind(typeof(double)), Is.EqualTo(double.PositiveInfinity));
			Assert.That(JsonString.Return("-Infinity").Bind(typeof(double)), Is.EqualTo(double.NegativeInfinity));
			Assert.That(JsonString.Return("1.23").Bind(typeof(double)), Is.InstanceOf<double>());

			Assert.That(JsonString.Return("0").ToDecimal(), Is.EqualTo(decimal.Zero));
			Assert.That(JsonString.Return("1").Bind(typeof(decimal)), Is.EqualTo(decimal.One));
			Assert.That(JsonString.Return("-1").Bind(typeof(decimal)), Is.EqualTo(decimal.MinusOne));
			Assert.That(JsonString.Return("1.23").ToDecimal(), Is.EqualTo(1.23m));
			Assert.That(JsonString.Return("3.1415926535897931").Bind(typeof(decimal)), Is.EqualTo(Math.PI));
			Assert.That(JsonString.Return("79228162514264337593543950335").Bind(typeof(decimal)), Is.EqualTo(decimal.MaxValue));
			Assert.That(JsonString.Return("-79228162514264337593543950335").Bind(typeof(decimal)), Is.EqualTo(decimal.MinValue));
			Assert.That(JsonString.Return("1.23").Bind(typeof(decimal)), Is.InstanceOf<decimal>());

			Assert.That(JsonString.Empty.ToGuid(), Is.EqualTo(Guid.Empty));
			Assert.That(JsonString.Return("00000000-0000-0000-0000-000000000000").ToGuid(), Is.EqualTo(Guid.Empty));
			Assert.That(JsonString.Return("b771bab0-7ad2-4945-a501-1dd939ca9bac").ToGuid(), Is.EqualTo(new Guid("b771bab0-7ad2-4945-a501-1dd939ca9bac")));
			Assert.That(JsonString.Return("591d8e31-1b79-4532-b7b9-4f8a9c0d0010").Bind(typeof(Guid)), Is.EqualTo(new Guid("591d8e31-1b79-4532-b7b9-4f8a9c0d0010")));
			Assert.That(JsonString.Return("133a3e6c-9ce5-4e9f-afe4-fa8c59945704").Bind(typeof(Guid)), Is.InstanceOf<Guid>());

			Assert.That(JsonString.Return("2013-03-11T12:34:56.768").ToDateTime(), Is.EqualTo(new DateTime(2013, 3, 11, 12, 34, 56, 768, DateTimeKind.Unspecified)));
			Assert.That(JsonString.Return("2013-03-11T12:34:56.768Z").ToDateTime(), Is.EqualTo(new DateTime(2013, 3, 11, 12, 34, 56, 768, DateTimeKind.Utc)));
			Assert.That(JsonString.Return("2013-03-11T12:34:56.768+01:00").ToDateTime(), Is.EqualTo(new DateTime(2013, 3, 11, 12, 34, 56, 768, DateTimeKind.Local)), "Ne marche que si la local TZ est Paris !");
			Assert.That(JsonString.Return("2013-03-11T12:34:56.768+01").ToDateTime(), Is.EqualTo(new DateTime(2013, 3, 11, 12, 34, 56, 768, DateTimeKind.Local)), "Ne marche que si la local TZ est Paris !");
			Assert.That(JsonString.Return("2013-03-11T12:34:56.768-01").ToDateTime(), Is.EqualTo(new DateTime(2013, 3, 11, 12, 34, 56, 768, DateTimeKind.Local).AddHours(2)), "Ne marche que si la local TZ est Paris !");
			Assert.That(JsonString.Return("2013-03-11T12:34:56.768+11:30").ToDateTime(), Is.EqualTo(new DateTime(2013, 3, 11, 12, 34, 56, 768, DateTimeKind.Local).AddHours(-10).AddMinutes(-30)), "Ne marche que si la local TZ est Paris !");
			Assert.That(JsonString.Return("2013-03-11T12:34:56.768-11:30").ToDateTime(), Is.EqualTo(new DateTime(2013, 3, 11, 12, 34, 56, 768, DateTimeKind.Local).AddHours(12).AddMinutes(30)), "Ne marche que si la local TZ est Paris !");

			Assert.That(JsonString.Return("2013-03-11T12:34:56.768Z").ToDateTimeOffset(), Is.EqualTo(new DateTimeOffset(2013, 3, 11, 12, 34, 56, 768, TimeSpan.Zero)));
			Assert.That(JsonString.Return("2013-03-11T12:34:56.768+01:00").ToDateTimeOffset(), Is.EqualTo(new DateTimeOffset(2013, 3, 11, 12, 34, 56, 768, TimeSpan.FromHours(1))));
			Assert.That(JsonString.Return("2013-03-11T12:34:56.768+04").ToDateTimeOffset(), Is.EqualTo(new DateTimeOffset(2013, 3, 11, 12, 34, 56, 768, TimeSpan.FromHours(4))));
			Assert.That(JsonString.Return("2013-03-11T12:34:56.768-07").ToDateTimeOffset(), Is.EqualTo(new DateTimeOffset(2013, 3, 11, 12, 34, 56, 768, TimeSpan.FromHours(-7))));
			Assert.That(JsonString.Return("2013-03-11T12:34:56.768-11").ToDateTimeOffset(), Is.EqualTo(new DateTimeOffset(2013, 3, 11, 12, 34, 56, 768, TimeSpan.FromHours(-11))));

			Assert.That(JsonString.Return("Foo").As<DummyJsonEnum>(), Is.EqualTo(DummyJsonEnum.Foo));
			Assert.That(JsonString.Return("Bar").As<DummyJsonEnum>(), Is.EqualTo(DummyJsonEnum.Bar));
			Assert.That(JsonString.Return("Bar").As<DummyJsonEnumTypo>(), Is.EqualTo(DummyJsonEnumTypo.Bar));
			Assert.That(JsonString.Return("Barrh").As<DummyJsonEnumTypo>(), Is.EqualTo(DummyJsonEnumTypo.Bar));
			Assert.That(() => JsonString.Return("Barrh").As<DummyJsonEnum>(), Throws.InstanceOf<JsonBindingException>());

			Assert.That(JsonNull.Null.As<IPAddress>(), Is.Null);
			Assert.That(JsonString.Empty.As<IPAddress>(), Is.Null);
			Assert.That(JsonString.Return("127.0.0.1").As<IPAddress>(), Is.EqualTo(IPAddress.Loopback));
			Assert.That(JsonString.Return("0.0.0.0").As<IPAddress>(), Is.EqualTo(IPAddress.Any));
			Assert.That(JsonString.Return("255.255.255.255").As<IPAddress>(), Is.EqualTo(IPAddress.None));
			Assert.That(JsonString.Return("192.168.1.2").As<IPAddress>(), Is.EqualTo(IPAddress.Parse("192.168.1.2")));
			Assert.That(JsonString.Return("::1").As<IPAddress>(), Is.EqualTo(IPAddress.IPv6Loopback));
			Assert.That(JsonString.Return("::").As<IPAddress>(), Is.EqualTo(IPAddress.IPv6Any));
			Assert.That(JsonString.Return("fe80::b8bc:1664:15a0:3a79%11").As<IPAddress>(), Is.EqualTo(IPAddress.Parse("fe80::b8bc:1664:15a0:3a79%11")));
			Assert.That(JsonString.Return("[::1]").As<IPAddress>(), Is.EqualTo(IPAddress.IPv6Loopback));
			Assert.That(JsonString.Return("[::]").As<IPAddress>(), Is.EqualTo(IPAddress.IPv6Any));
			Assert.That(JsonString.Return("[fe80::b8bc:1664:15a0:3a79%11]").As<IPAddress>(), Is.EqualTo(IPAddress.Parse("fe80::b8bc:1664:15a0:3a79%11")));
			Assert.That(() => JsonString.Return("127.0.0.").As<IPAddress>(), Throws.InstanceOf<FormatException>());
			Assert.That(() => JsonString.Return("127.0.0.1.2").As<IPAddress>(), Throws.InstanceOf<FormatException>());

			// empty => T : doit retourner default(T) donc 0/false/...
			Assert.That(JsonString.Empty.As<bool>(), Is.False, "'' -> bool");
			Assert.That(JsonString.Empty.As<int>(), Is.Zero, "'' -> int");
			Assert.That(JsonString.Empty.As<long>(), Is.Zero, "'' -> long");
			Assert.That(JsonString.Empty.As<float>(), Is.Zero, "'' -> float");
			Assert.That(JsonString.Empty.As<double>(), Is.Zero, "'' -> double");
			Assert.That(JsonString.Empty.As<DateTime>(), Is.EqualTo(DateTime.MinValue), "'' -> DateTime");
			Assert.That(JsonString.Empty.As<DateTimeOffset>(), Is.EqualTo(DateTimeOffset.MinValue), "'' -> DateTimeOffset");

			// empty => T?: doit retourner default(T?) donc null
			Assert.That(JsonString.Empty.As<bool?>(), Is.Null, "'' -> bool?");
			Assert.That(JsonString.Empty.As<int?>(), Is.Null, "'' -> int?");
			Assert.That(JsonString.Empty.As<long?>(), Is.Null, "'' -> long?");
			Assert.That(JsonString.Empty.As<float?>(), Is.Null, "'' -> float?");
			Assert.That(JsonString.Empty.As<double?>(), Is.Null, "'' -> double?");
			Assert.That(JsonString.Empty.As<DateTime?>(), Is.Null, "'' -> DateTime?");
			Assert.That(JsonString.Empty.As<DateTimeOffset?>(), Is.Null, "'' -> DateTimeOffset?");

			// auto cast
			{
				JsonValue jval = "hello"; // implicit cast
				Assert.That(jval, Is.Not.Null);
				Assert.That(jval, Is.InstanceOf<JsonString>());
				JsonString jstr = (JsonString) jval;
				Assert.That(jstr.Value, Is.EqualTo("hello"));

				string s = (string) jval; // explicit cast
				Assert.That(s, Is.Not.Null);
				Assert.That(s, Is.EqualTo("hello"));
			}
		}

		[Test]
		public void Test_JsonString_Comparisons()
		{

			// comparisons
			void Compare(string a, string b)
			{
				JsonValue ja = JsonString.Return(a);
				JsonValue jb = JsonString.Return(b);

				Assert.That(Math.Sign(ja.CompareTo(jb)), Is.EqualTo(Math.Sign(string.CompareOrdinal(a, b))), "'{0}' cmp '{1}'", a, b);
				Assert.That(Math.Sign(jb.CompareTo(ja)), Is.EqualTo(Math.Sign(string.CompareOrdinal(b, a))), "'{0}' cmp '{1}'", b, a);
			}

			Compare("", "");
			Compare("abc", "");
			Compare("abc", "abc");
			Compare("aaa", "bbb");
			Compare("aa", "a");
			Compare("aa", "aaa");
			Compare("ABC", "abc");
			Compare("bat", "batman");

			void SortStrings(string message, string[] ss, string[] expected)
			{
				var arr = ss.Select(x => JsonString.Return(x)).ToArray();
				Log(string.Join<JsonValue>(", ", arr));
				Array.Sort(arr);
				Log(string.Join<JsonValue>(", ", arr));
				Assert.That(arr.Select(x => x.ToString()).ToArray(), Is.EqualTo(expected), message);
			}

			SortStrings(
				"sorting should use ordinal, case sensitive",
				new[] { "a", "b", "c", "aa", "ab", "aC", "aaa", "abc" },
				new[] { "a", "aC", "aa", "aaa", "ab", "abc", "b", "c" }
			);
			SortStrings(
				"sorting should use lexicographical order",
				new[] { "cat", "bat", "catamaran", "catZ", "batman" },
				new[] { "bat", "batman", "cat", "catZ", "catamaran" }
			);
			SortStrings(
				"numbers < UPPERs << lowers",
				new[] { "a", "1", "A" },
				new[] { "1", "A", "a" }
			);
			SortStrings(
				"numbers should be sorted lexicographically if comparing strings (1 < 10 < 2)",
				new[] { "0", "1", "2", "7", "10", "42", "100", "1000" },
				new[] { "0", "1", "10", "100", "1000", "2", "42", "7" }
			);

			// cmp with numbers
			Assert.That(JsonString.Return("ABC").CompareTo(JsonNumber.Return(123)),  Is.GreaterThan(0), "'ABC' cmp 123");
			Assert.That(JsonNumber.Return(123).CompareTo(JsonString.Return("ABC")),  Is.LessThan(0),    "123 cmp 'ABC'");

			Assert.That(JsonString.Return("123").CompareTo(JsonNumber.Return(123)),  Is.EqualTo(0),     "'123' cmp 123");
			Assert.That(JsonString.Return("100").CompareTo(JsonNumber.Return(123)),  Is.LessThan(0),    "'100' cmp 123");
			Assert.That(JsonString.Return("1000").CompareTo(JsonNumber.Return(123)), Is.GreaterThan(0), "'1000' cmp 123");
			Assert.That(JsonNumber.Return(123).CompareTo(JsonString.Return("123")),  Is.EqualTo(0),     "123 cmp '123'");
			Assert.That(JsonNumber.Return(123).CompareTo(JsonString.Return("100")),  Is.GreaterThan(0), "123 cmp '100'");
			Assert.That(JsonNumber.Return(123).CompareTo(JsonString.Return("1000")), Is.LessThan(0),    "123 cmp '1000'");
		}

		[Test]
		public void Test_JsonNumber()
		{
			{
				var value = JsonNumber.Zero;
				Assert.That(value.Type, Is.EqualTo(JsonType.Number));
				Assert.That(value.IsNull, Is.False);
				Assert.That(value.IsDefault, Is.True);
				Assert.That(value.ToObject(), Is.EqualTo(0));
				Assert.That(value.ToObject(), Is.InstanceOf<long>());
				Assert.That(value.IsDecimal, Is.False);
				Assert.That(value.IsNegative, Is.False);
				Assert.That(value.ToString(), Is.EqualTo("0"));
				Assert.That(SerializeToSlice(value), Is.EqualTo(Slice.FromString("0")));
			}

			{
				var value = JsonNumber.One;
				Assert.That(value.Type, Is.EqualTo(JsonType.Number));
				Assert.That(value.IsNull, Is.False);
				Assert.That(value.IsDefault, Is.False);
				Assert.That(value.ToObject(), Is.EqualTo(1));
				Assert.That(value.ToObject(), Is.InstanceOf<long>());
				Assert.That(value.IsDecimal, Is.False);
				Assert.That(value.IsNegative, Is.False);
				Assert.That(value.ToString(), Is.EqualTo("1"));
				Assert.That(SerializeToSlice(value), Is.EqualTo(Slice.FromString("1")));
			}

			{
				var value = JsonNumber.MinusOne;
				Assert.That(value.Type, Is.EqualTo(JsonType.Number));
				Assert.That(value.IsNull, Is.False);
				Assert.That(value.IsDefault, Is.False);
				Assert.That(value.ToObject(), Is.EqualTo(-1));
				Assert.That(value.ToObject(), Is.InstanceOf<long>());
				Assert.That(value.IsDecimal, Is.False);
				Assert.That(value.IsNegative, Is.True);
				Assert.That(value.ToString(), Is.EqualTo("-1"));
				Assert.That(SerializeToSlice(value), Is.EqualTo(Slice.FromString("-1")));
			}

			{
				var value = JsonNumber.Return(123); // c'est dans la zone possible pour les ints, donc doit retourner un int !
				Assert.That(value.Type, Is.EqualTo(JsonType.Number));
				Assert.That(value.IsNull, Is.False);
				Assert.That(value.IsDefault, Is.False);
				Assert.That(value.ToObject(), Is.EqualTo(123));
				Assert.That(value.ToObject(), Is.InstanceOf<long>());
				Assert.That(value.IsDecimal, Is.False);
				Assert.That(value.IsNegative, Is.False);
				Assert.That(value.ToString(), Is.EqualTo("123"));
				Assert.That(SerializeToSlice(value), Is.EqualTo(Slice.FromString("123")));
			}

			{
				var value = JsonNumber.Return(1L + int.MaxValue); // juste en dehors de la portée d'un int, donc doit retourner un long !
				Assert.That(value.Type, Is.EqualTo(JsonType.Number));
				Assert.That(value.IsNull, Is.False);
				Assert.That(value.IsDefault, Is.False);
				Assert.That(value.ToObject(), Is.EqualTo(2147483648));
				Assert.That(value.ToObject(), Is.InstanceOf<long>());
				Assert.That(value.IsDecimal, Is.False);
				Assert.That(value.IsNegative, Is.False);
				Assert.That(value.ToString(), Is.EqualTo("2147483648"));
				Assert.That(SerializeToSlice(value), Is.EqualTo(Slice.FromString("2147483648")));
			}

			{
				var value = JsonNumber.Return(123UL);
				Assert.That(value.Type, Is.EqualTo(JsonType.Number));
				Assert.That(value.IsNull, Is.False);
				Assert.That(value.IsDefault, Is.False);
				Assert.That(value.ToObject(), Is.EqualTo(123));
				Assert.That(value.ToObject(), Is.InstanceOf<long>(), "small integers should be converted to 'long'");
				Assert.That(value.IsDecimal, Is.False);
				Assert.That(value.IsNegative, Is.False);
				Assert.That(value.ToString(), Is.EqualTo("123"));
				Assert.That(SerializeToSlice(value), Is.EqualTo(Slice.FromString("123")));
			}

			{
				var value = JsonNumber.Return(1.23f);
				Assert.That(value.Type, Is.EqualTo(JsonType.Number));
				Assert.That(value.IsNull, Is.False);
				Assert.That(value.IsDefault, Is.False);
				Assert.That(value.ToObject(), Is.EqualTo(1.23f));
				Assert.That(value.ToObject(), Is.InstanceOf<double>());
				Assert.That(value.IsDecimal, Is.True);
				Assert.That(value.IsNegative, Is.False);
				Assert.That(value.ToString(), Is.EqualTo("1.23"));
				Assert.That(SerializeToSlice(value), Is.EqualTo(Slice.FromString("1.23")));
			}

			{
				var value = JsonNumber.Return(-1.23f);
				Assert.That(value.Type, Is.EqualTo(JsonType.Number));
				Assert.That(value.IsNull, Is.False);
				Assert.That(value.IsDefault, Is.False);
				Assert.That(value.ToObject(), Is.EqualTo(-1.23f));
				Assert.That(value.ToObject(), Is.InstanceOf<double>());
				Assert.That(value.IsDecimal, Is.True);
				Assert.That(value.IsNegative, Is.True);
				Assert.That(value.ToString(), Is.EqualTo("-1.23"));
				Assert.That(SerializeToSlice(value), Is.EqualTo(Slice.FromString("-1.23")));
			}

			{
				var value = JsonNumber.Return(Math.PI);
				Assert.That(value.Type, Is.EqualTo(JsonType.Number));
				Assert.That(value.IsNull, Is.False);
				Assert.That(value.IsDefault, Is.False);
				Assert.That(value.ToObject(), Is.EqualTo(Math.PI));
				Assert.That(value.ToObject(), Is.InstanceOf<double>());
				Assert.That(value.IsDecimal, Is.True);
				Assert.That(value.IsNegative, Is.False);
				Assert.That(value.ToString(), Is.EqualTo(Math.PI.ToString("R")));
				Assert.That(SerializeToSlice(value), Is.EqualTo(Slice.FromString(Math.PI.ToString("R"))));
			}

			{
				var value = JsonNumber.Return(double.NaN);
				Assert.That(value.Type, Is.EqualTo(JsonType.Number));
				Assert.That(value.IsNull, Is.False);
				Assert.That(value.IsDefault, Is.False);
				Assert.That(value.ToObject(), Is.EqualTo(double.NaN));
				Assert.That(value.ToObject(), Is.InstanceOf<double>());
				Assert.That(value.IsDecimal, Is.True);
				Assert.That(value.ToString(), Is.EqualTo("NaN"));
				Assert.That(SerializeToSlice(value), Is.EqualTo(Slice.FromString("NaN")));
			}

			{
				var value = JsonNumber.DecimalZero;
				Assert.That(value.Type, Is.EqualTo(JsonType.Number));
				Assert.That(value.IsNull, Is.False);
				Assert.That(value.IsDefault, Is.True);
				Assert.That(value.ToObject(), Is.EqualTo(0d));
				Assert.That(value.ToObject(), Is.InstanceOf<double>());
				Assert.That(value.IsDecimal, Is.True);
				Assert.That(value.IsNegative, Is.False);
				Assert.That(value.ToString(), Is.EqualTo("0"));
				Assert.That(SerializeToSlice(value), Is.EqualTo(Slice.FromString("0")));
			}

			{
				var value = JsonNumber.DecimalOne;
				Assert.That(value.Type, Is.EqualTo(JsonType.Number));
				Assert.That(value.IsNull, Is.False);
				Assert.That(value.IsDefault, Is.False);
				Assert.That(value.ToObject(), Is.EqualTo(1d));
				Assert.That(value.ToObject(), Is.InstanceOf<double>());
				Assert.That(value.IsDecimal, Is.True);
				Assert.That(value.IsNegative, Is.False);
				Assert.That(value.ToString(), Is.EqualTo("1"));
				Assert.That(SerializeToSlice(value), Is.EqualTo(Slice.FromString("1")));
			}

			// Nullables

			Assert.That(JsonNumber.Return((int?) null).Type, Is.EqualTo(JsonType.Null));
			Assert.That(JsonNumber.Return((uint?) null).Type, Is.EqualTo(JsonType.Null));
			Assert.That(JsonNumber.Return((long?) null).Type, Is.EqualTo(JsonType.Null));
			Assert.That(JsonNumber.Return((ulong?) null).Type, Is.EqualTo(JsonType.Null));
			Assert.That(JsonNumber.Return((float?) null).Type, Is.EqualTo(JsonType.Null));
			Assert.That(JsonNumber.Return((double?) null).Type, Is.EqualTo(JsonType.Null));

			Assert.That(JsonNumber.Return((int?) 42).Type, Is.EqualTo(JsonType.Number));
			Assert.That(JsonNumber.Return((uint?) 42).Type, Is.EqualTo(JsonType.Number));
			Assert.That(JsonNumber.Return((long?) 42).Type, Is.EqualTo(JsonType.Number));
			Assert.That(JsonNumber.Return((ulong?) 42).Type, Is.EqualTo(JsonType.Number));
			Assert.That(JsonNumber.Return((float?) 3.14f).Type, Is.EqualTo(JsonType.Number));
			Assert.That(JsonNumber.Return((double?) 3.14d).Type, Is.EqualTo(JsonType.Number));

			// Conversions

			// Primitive
			Assert.That(JsonNumber.Return(123).ToInt32(), Is.EqualTo(123), "{123}.ToInt32()");
			Assert.That(JsonNumber.Return(-123).ToInt32(), Is.EqualTo(-123), "{-123}.ToInt32()");
			Assert.That(JsonNumber.Return(123L).ToInt64(), Is.EqualTo(123L), "{123L}.ToInt64()");
			Assert.That(JsonNumber.Return(-123L).ToInt64(), Is.EqualTo(-123L), "{-123L}.ToInt64()");
			Assert.That(JsonNumber.Return(123f).ToSingle(), Is.EqualTo(123f), "{123f}.ToSingle()");
			Assert.That(JsonNumber.Return(123d).ToDouble(), Is.EqualTo(123d), "{123d}.ToDouble()");
			Assert.That(JsonNumber.Return(Math.PI).ToDouble(), Is.EqualTo(Math.PI), "{Math.PI}.ToDouble()");

			Assert.That(JsonNumber.Return(123).As<int>(), Is.EqualTo(123), "{123}.ToInt32()");
			Assert.That(JsonNumber.Return(-123).As<int>(), Is.EqualTo(-123), "{-123}.ToInt32()");
			Assert.That(JsonNumber.Return(123L).As<long>(), Is.EqualTo(123L), "{123L}.ToInt64()");
			Assert.That(JsonNumber.Return(-123L).As<long>(), Is.EqualTo(-123L), "{-123L}.ToInt64()");
			Assert.That(JsonNumber.Return(123f).As<float>(), Is.EqualTo(123f), "{123f}.ToSingle()");
			Assert.That(JsonNumber.Return(123d).As<double>(), Is.EqualTo(123d), "{123d}.ToDouble()");
			Assert.That(JsonNumber.Return(Math.PI).As<double>(), Is.EqualTo(Math.PI), "{Math.PI}.ToDouble()");

			// Enum
			// ... qui dérive de Int32
			Assert.That(JsonNumber.Zero.Bind(typeof (DummyJsonEnum), null), Is.EqualTo(DummyJsonEnum.None), "{0}.Bind(DummyJsonEnum)");
			Assert.That(JsonNumber.One.Bind(typeof (DummyJsonEnum), null), Is.EqualTo(DummyJsonEnum.Foo), "{1}.Bind(DummyJsonEnum)");
			Assert.That(JsonNumber.Return(42).Bind(typeof (DummyJsonEnum), null), Is.EqualTo(DummyJsonEnum.Bar), "{42}.Bind(DummyJsonEnum)");
			Assert.That(JsonNumber.Return(66).Bind(typeof (DummyJsonEnum), null), Is.EqualTo((DummyJsonEnum) 66), "{66}.Bind(DummyJsonEnum)");
			// ... qui ne dérive pas de Int32
			Assert.That(JsonNumber.Zero.Bind(typeof (DummyJsonEnumShort), null), Is.EqualTo(DummyJsonEnumShort.None), "{0}.Bind(DummyJsonEnumShort)");
			Assert.That(JsonNumber.One.Bind(typeof (DummyJsonEnumShort), null), Is.EqualTo(DummyJsonEnumShort.One), "{1}.Bind(DummyJsonEnumShort)");
			Assert.That(JsonNumber.Return(65535).Bind(typeof (DummyJsonEnumShort), null), Is.EqualTo(DummyJsonEnumShort.MaxValue), "{65535}.Bind(DummyJsonEnumShort)");

			// TimeSpan
			Assert.That(JsonNumber.Return(0).ToTimeSpan(), Is.EqualTo(TimeSpan.Zero), "{0}.ToTimeSpan()");
			Assert.That(JsonNumber.Return(3600).ToTimeSpan(), Is.EqualTo(TimeSpan.FromHours(1)), "{3600}.ToTimeSpan()");
			Assert.That(
				JsonNumber.Return(TimeSpan.MaxValue.TotalSeconds + 1).ToTimeSpan(),
				Is.EqualTo(TimeSpan.MaxValue),
				"{TimeSpan.MaxValue.TotalSeconds + 1}.ToTimeSpan()");
			Assert.That(
				JsonNumber.Return(TimeSpan.MinValue.TotalSeconds - 1).ToTimeSpan(),
				Is.EqualTo(TimeSpan.MinValue),
				"{TimeSpan.MinValue.TotalSeconds - 1}.ToTimeSpan()");

			// DateTime
			//note: les dates sont transformées en un nombre de jours (décimal) depuis Unix Epoch, en UTC
			Assert.That(JsonNumber.Return(0).ToDateTime(), Is.EqualTo(new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)), "0.ToDateTime()");
			Assert.That(JsonNumber.Return(1).ToDateTime(), Is.EqualTo(new DateTime(1970, 1, 1, 0, 0, 1, DateTimeKind.Utc)), "1.ToDateTime()");
			Assert.That(JsonNumber.Return(86400).ToDateTime(), Is.EqualTo(new DateTime(1970, 1, 2, 0, 0, 0, DateTimeKind.Utc)), "86400.ToDateTime()");
			Assert.That(JsonNumber.Return(1484830412.854).ToDateTime(), Is.EqualTo(new DateTime(2017, 1, 19, 12, 53, 32, 854, DateTimeKind.Utc)).Within(TimeSpan.FromMilliseconds(1)), "(DAYS).ToDateTime()");
			Assert.That(JsonNumber.Return(new DateTime(2017, 1, 19, 12, 53, 32, 854, DateTimeKind.Utc)).ToDouble(), Is.EqualTo(1484830412.854), "(UTC).Value");
			Assert.That(JsonNumber.Return(new DateTime(2017, 1, 19, 13, 53, 32, 854, DateTimeKind.Local)).ToDouble(), Is.EqualTo(1484830412.854), "(LOCAL).Value");
			Assert.That(JsonNumber.Return(DateTime.MinValue).ToDouble(), Is.EqualTo(0), "MinValue"); // par convention, MinValue == 0 == epoch (a débatre!)
			Assert.That(JsonNumber.Return(DateTime.MaxValue).ToDouble(), Is.EqualTo(double.NaN), "MaxValue"); //par convention, MaxValue == NaN
			Assert.That(JsonNumber.NaN.ToDateTime(), Is.EqualTo(DateTime.MaxValue), "MaxValue"); //par convention, NaN == MaxValue

			// Instant
			//note: les instants sont transformées en un nombre de jours (décimal) depuis Unix Epoch
			Assert.That(JsonNumber.Return(0).ToInstant(), Is.EqualTo(NodaTime.Instant.FromUtc(1970, 1, 1, 0, 0, 0)), "0.ToInstant()");
			Assert.That(JsonNumber.Return(1).ToInstant(), Is.EqualTo(NodaTime.Instant.FromUtc(1970, 1, 1, 0, 0, 1)), "1.ToInstant()");
			Assert.That(JsonNumber.Return(86400).ToInstant(), Is.EqualTo(NodaTime.Instant.FromUtc(1970, 1, 2, 0, 0, 0)), "86400.ToInstant()");
			Assert.That(JsonNumber.Return(1484830412.854).ToInstant(), Is.EqualTo(NodaTime.Instant.FromDateTimeUtc(new DateTime(2017, 1, 19, 12, 53, 32, 854, DateTimeKind.Utc)))/*.Within(NodaTime.Duration.FromMilliseconds(1))*/, "(DAYS).ToInstant()");
			Assert.That(JsonNumber.Return(NodaTime.Instant.FromDateTimeUtc(new DateTime(2017, 1, 19, 12, 53, 32, 854, DateTimeKind.Utc))).ToDouble(), Is.EqualTo(1484830412.854), "(UTC).Value");
			Assert.That(JsonNumber.Return(NodaTime.Instant.MinValue).ToDouble(), Is.EqualTo(NodaTime.Instant.FromUtc(-9998, 1 , 1, 0, 0, 0).ToUnixTimeSeconds()), "MinValue");
			Assert.That(JsonNumber.Return(NodaTime.Instant.MaxValue).ToDouble(), Is.EqualTo(NodaTime.Instant.FromUtc(9999, 12, 31, 23, 59, 59).ToUnixTimeSeconds() + 0.999999999d), "MaxValue");
			Assert.That(JsonNumber.NaN.ToInstant(), Is.EqualTo(NodaTime.Instant.MaxValue), "MaxValue"); //par convention, NaN == MaxValue

			// String
			Assert.That(JsonNumber.Zero.Bind(typeof (string), null), Is.EqualTo("0"), "{0}.Bind(string)");
			Assert.That(JsonNumber.One.Bind(typeof (string), null), Is.EqualTo("1"), "{1}.Bind(string)");
			Assert.That(JsonNumber.Return(123).Bind(typeof (string), null), Is.EqualTo("123"), "{123}.Bind(string)");
			Assert.That(JsonNumber.Return(-123).Bind(typeof (string), null), Is.EqualTo("-123"), "{-123}.Bind(string)");
			Assert.That(JsonNumber.Return(Math.PI).Bind(typeof (string), null), Is.EqualTo(Math.PI.ToString("R")), "{Math.PI}.Bind(string)");

			// auto cast

			JsonValue v;
			JsonNumber j;

			v = int.MaxValue;
			Assert.That(v, Is.Not.Null.And.InstanceOf<JsonNumber>());
			Assert.That(v.ToInt32(), Is.EqualTo(int.MaxValue));
			Assert.That(v.As<int>(), Is.EqualTo(int.MaxValue));
			Assert.That((int) v, Is.EqualTo(int.MaxValue));
			j = (JsonNumber) v;
			Assert.That(j.IsDecimal, Is.False);
			Assert.That(j.IsUnsigned, Is.False);

			v = uint.MaxValue;
			Assert.That(v, Is.Not.Null.And.InstanceOf<JsonNumber>());
			Assert.That(v.ToUInt32(), Is.EqualTo(uint.MaxValue));
			Assert.That(v.As<uint>(), Is.EqualTo(uint.MaxValue));
			Assert.That((uint) v, Is.EqualTo(uint.MaxValue));
			j = (JsonNumber) v;
			Assert.That(j.IsDecimal, Is.False);
			Assert.That(j.IsUnsigned, Is.False, "uint.MaxValue is small enough to fit in a long");

			v = long.MaxValue;
			Assert.That(v, Is.Not.Null.And.InstanceOf<JsonNumber>());
			Assert.That(v.ToInt64(), Is.EqualTo(long.MaxValue));
			Assert.That(v.As<long>(), Is.EqualTo(long.MaxValue));
			Assert.That((long) v, Is.EqualTo(long.MaxValue));
			j = (JsonNumber) v;
			Assert.That(j.IsDecimal, Is.False);
			Assert.That(j.IsUnsigned, Is.False);

			v = ulong.MaxValue;
			Assert.That(v, Is.Not.Null.And.InstanceOf<JsonNumber>());
			Assert.That(v.ToUInt64(), Is.EqualTo(ulong.MaxValue));
			Assert.That(v.As<ulong>(), Is.EqualTo(ulong.MaxValue));
			Assert.That((ulong) v, Is.EqualTo(ulong.MaxValue));
			j = (JsonNumber) v;
			Assert.That(j.IsDecimal, Is.False);
			Assert.That(j.IsUnsigned, Is.True);

			v = Math.PI;
			Assert.That(v, Is.Not.Null.And.InstanceOf<JsonNumber>());
			Assert.That(v.ToDouble(), Is.EqualTo(Math.PI));
			Assert.That(v.As<double>(), Is.EqualTo(Math.PI));
			Assert.That((double) v, Is.EqualTo(Math.PI));
			j = (JsonNumber) v;
			Assert.That(j.IsDecimal, Is.True);
			Assert.That(j.IsUnsigned, Is.False);

			v = 1.234f;
			Assert.That(v, Is.Not.Null.And.InstanceOf<JsonNumber>());
			Assert.That(v.ToSingle(), Is.EqualTo(1.234f));
			Assert.That(v.As<float>(), Is.EqualTo(1.234f));
			Assert.That((float) v, Is.EqualTo(1.234f));
			j = (JsonNumber) v;
			Assert.That(j.IsDecimal, Is.True);
			Assert.That(j.IsUnsigned, Is.False);

			v = (int?) 123;
			Assert.That(v, Is.Not.Null.And.InstanceOf<JsonNumber>());
			Assert.That(v.ToInt32OrDefault(), Is.EqualTo(123));
			Assert.That(v.As<int?>(), Is.EqualTo(123));
			Assert.That((int?) v, Is.EqualTo(123));
			j = (JsonNumber) v;
			Assert.That(j.IsDecimal, Is.False);
			Assert.That(j.IsUnsigned, Is.False);

			v = (uint?) 123;
			Assert.That(v, Is.Not.Null.And.InstanceOf<JsonNumber>());
			Assert.That(v.ToUInt32OrDefault(), Is.EqualTo(123));
			Assert.That(v.As<uint?>(), Is.EqualTo(123));
			Assert.That((uint?) v, Is.EqualTo(123));
			j = (JsonNumber) v;
			Assert.That(j.IsDecimal, Is.False);
			Assert.That(j.IsUnsigned, Is.False, "123u fits in a long");

		}

		[Test, SuppressMessage("ReSharper", "EqualExpressionComparison")]
		public void Test_JsonNumber_CompareTo()
		{
			JsonValue x0 = 0;
			JsonValue x1 = 1;
			JsonValue x2 = 2;

			#pragma warning disable CS1718
			// JsonValue vs JsonValue
			Assert.That(x0 < x0, Is.False);
			Assert.That(x0 < x1, Is.True);
			Assert.That(x0 < x2, Is.True);

			Assert.That(x0 <= x0, Is.True);
			Assert.That(x0 <= x1, Is.True);
			Assert.That(x0 <= x2, Is.True);

			Assert.That(x0 > x0, Is.False);
			Assert.That(x0 > x1, Is.False);
			Assert.That(x0 > x2, Is.False);

			Assert.That(x0 >= x0, Is.True);
			Assert.That(x0 >= x1, Is.False);
			Assert.That(x0 >= x2, Is.False);
			#pragma warning restore CS1718

			// JsonValue vs valuetype (allocation)
			// => l'entier est convertit automatiquement en JsonValue, ce qui provoque une allocation

			Expression<Func<JsonValue, int, bool>> expr2 = (jval, x) => jval < x;
			Assert.That(expr2.Body.NodeType, Is.EqualTo(ExpressionType.LessThan));
			Assert.That(((BinaryExpression)expr2.Body).Method?.Name, Is.EqualTo("op_LessThan"));
			Assert.That(((BinaryExpression)expr2.Body).Method?.GetParameters()[0].ParameterType, Is.EqualTo(typeof(JsonValue)));
			Assert.That(((BinaryExpression)expr2.Body).Method?.GetParameters()[1].ParameterType, Is.EqualTo(typeof(JsonValue)));

			Assert.That(x1 < 1, Is.False);
			Assert.That(x1 <= 1, Is.True);
			Assert.That(x1 > 1, Is.False);
			Assert.That(x1 >= 1, Is.True);

			Assert.That(x1 < 2, Is.True);
			Assert.That(x1 <= 2, Is.True);
			Assert.That(x1 > 2, Is.False);
			Assert.That(x1 >= 2, Is.False);

			// JsonNumber vs valuetype (no allocations)
			// => ces comparaisons ne doivent pas allouer de JsonValue pour le test !

			Expression<Func<JsonNumber, int, bool>> expr1 = (jnum, x) => jnum < x;
			Assert.That(expr1.Body.NodeType, Is.EqualTo(ExpressionType.LessThan));
			Assert.That(((BinaryExpression)expr1.Body).Method?.Name, Is.EqualTo("op_LessThan"));
			Assert.That(((BinaryExpression)expr1.Body).Method?.GetParameters()[0].ParameterType, Is.EqualTo(typeof(JsonNumber)));
			Assert.That(((BinaryExpression)expr1.Body).Method?.GetParameters()[1].ParameterType, Is.EqualTo(typeof(long)));

			var n1 = (JsonNumber) x1;
			Assert.That(n1 < 1, Is.False);
			Assert.That(n1 <= 1, Is.True);
			Assert.That(n1 > 1, Is.False);
			Assert.That(n1 >= 1, Is.True);

			Assert.That(n1 < 2, Is.True);
			Assert.That(n1 <= 2, Is.True);
			Assert.That(n1 > 2, Is.False);
			Assert.That(n1 >= 2, Is.False);
		}

		[Test]
		public void Test_JsonNumber_Between()
		{
			JsonNumber j;

			j = (JsonNumber)123;
			Assert.That(j.IsBetween(0, 100), Is.False);
			Assert.That(j.IsBetween(0, 200), Is.True);
			Assert.That(j.IsBetween(150, 200), Is.False);
			Assert.That(j.IsBetween(100, 123), Is.True);
			Assert.That(j.IsBetween(123, 150), Is.True);
			Assert.That(j.IsBetween(123, 123), Is.True);

			j = (JsonNumber)123.4d;
			Assert.That(j.IsBetween(0, 100), Is.False);
			Assert.That(j.IsBetween(0, 200), Is.True);
			Assert.That(j.IsBetween(150, 200), Is.False);
			Assert.That(j.IsBetween(100, 123), Is.False);
			Assert.That(j.IsBetween(100, 124), Is.True);
			Assert.That(j.IsBetween(123, 150), Is.True);
			Assert.That(j.IsBetween(124, 150), Is.False);
		}

		[Test]
		public void Test_JsonNumber_RoundingBug()
		{
			// Si on sérialise/désérisalise un double du style "7.5318246509562359", il y a un pb lors de la conversion de decimal vers double (la dernière décimale change)
			// => on vérifie que le JsonNumber est capable de gérer correctement ce problème

			double x = 7.5318246509562359d;
			Assert.That((double)((decimal)x), Is.Not.EqualTo(x), $"Check that {x:R} gets corrupted during roundtrip by the CLR");
			Assert.That(JsonNumber.Return(x).ToString(), Is.EqualTo(x.ToString("R")));
			Assert.That(((JsonNumber)CrystalJson.Parse("7.5318246509562359")).ToDouble(), Is.EqualTo(x), $"Rounding Bug check: {x:R} should not change!");

			x = 3.8219629199346357;
			Assert.That((double)((decimal)x), Is.Not.EqualTo(x), $"Check that {x:R} gets corrupted during roundtrip by the CLR");
			Assert.That(JsonNumber.Return(x).ToString(), Is.EqualTo(x.ToString("R")));
			Assert.That(((JsonNumber)CrystalJson.Parse("3.8219629199346357")).ToDouble(), Is.EqualTo(x), $"Rounding Bug check: {x:R} should not change!");

			// meme problème avec les float !
			float y = 7.53182459f;
			Assert.That((float)((decimal)y), Is.Not.EqualTo(y), $"Check that {y:R} gets corrupted during roundtrip by the CLR");
			Assert.That(JsonNumber.Return(y).ToString(), Is.EqualTo(y.ToString("R")));
			Assert.That(((JsonNumber)CrystalJson.Parse("7.53182459")).ToSingle(), Is.EqualTo(y), $"Rounding Bug check: {y:R}");
		}

		[Test]
		public void Test_JsonNumber_Interning()
		{
			//NOTE: l'interning pour le moment ne marche que sur des petits nombres: -128..+127 et 0U..255U
			// => si jamais ce test fail, vérifier juste que le cache n'as pas changé de comportement!

			Assert.That(JsonNumber.Return(0), Is.SameAs(JsonNumber.Zero), "Zero");
			Assert.That(JsonNumber.Return(1), Is.SameAs(JsonNumber.One), "One");
			Assert.That(JsonNumber.Return(-1), Is.SameAs(JsonNumber.MinusOne), "MinusOne");
			Assert.That(JsonNumber.Return(42), Is.SameAs(JsonNumber.Return(42)), "42 should be in the small signed cache");
			Assert.That(JsonNumber.Return(-42), Is.SameAs(JsonNumber.Return(-42)), "-42 should be in the small signed cache");
			Assert.That(JsonNumber.Return(255), Is.SameAs(JsonNumber.Return(255)), "255 should be in the small signed cache");
			Assert.That(JsonNumber.Return(-128), Is.SameAs(JsonNumber.Return(-128)), "-255 should be in the small signed cache");

			// doit aussi intern les valeur d'un tableau ou liste
			var arr = new int[10].ToJsonArray();
			Assert.That(arr, Is.Not.Null.And.Count.EqualTo(10), "array of zeroes");
			Assert.That(arr[0], Is.SameAs(JsonNumber.Zero));
			Assert.That(arr[0].ToInt32(), Is.EqualTo(0));
			for (int i = 1; i < arr.Count; i++)
			{
				Assert.That(arr[i], Is.SameAs(JsonNumber.Zero), $"arr[{i}]");
			}

			// liste
			arr = new long[10].ToList().ToJsonArray();
			Assert.That(arr, Is.Not.Null.And.Count.EqualTo(10), "list of zeroes");
			Assert.That(arr[0], Is.SameAs(JsonNumber.Zero));
			Assert.That(arr[0].ToInt64(), Is.EqualTo(0));
			for (int i = 1; i < arr.Count; i++)
			{
				Assert.That(arr[i], Is.SameAs(arr[0]), $"arr[{i}]");
			}

			// sequence
			arr = Enumerable.Range(0, 10).Select(_ => 42U).ToJsonArray();
			Assert.That(arr, Is.Not.Null.And.Count.EqualTo(10), "sequence of same value");
			Assert.That(arr[0].ToUInt32(), Is.EqualTo(42U));
			for (int i = 1; i < arr.Count; i++)
			{
				Assert.That(arr[i], Is.SameAs(arr[0]), $"arr[{i}]");
			}

			// la même série de données convertie deux fois
			var t1 = new int[] { 0, 1, 42, -6, 3 };
			var t2 = new int[] { 0, 1, 42, -6, 3 };
			Assume.That(t1, Is.EqualTo(t2));
			var arr1 = t1.ToJsonArray();
			var arr2 = t2.ToJsonArray();
			Assert.That(arr1, Is.Not.Null.And.Count.EqualTo(t1.Length));
			Assert.That(arr2, Is.Not.Null.And.Count.EqualTo(t2.Length));
			for (int i = 0; i < t1.Length; i++)
			{
				Assert.That(arr1[i], Is.SameAs(arr2[i]), $"arr1[{i}] same as arr2[{i}]");
				Assert.That(arr1[i].ToInt32(), Is.EqualTo(t1[i]), $"arr1[{i}] == t1[{i}]");
			}

		}

		[Test]
		public void Test_JsonDateTime()
		{
			{
				var value = JsonDateTime.MinValue;
				Assert.That(value.Type, Is.EqualTo(JsonType.DateTime));
				Assert.That(value.IsNull, Is.False);
				Assert.That(value.IsDefault, Is.True);
				Assert.That(value.ToDateTime(), Is.EqualTo(DateTime.MinValue));
				Assert.That(value.IsLocalTime, Is.False, "MinValue should be unspecifed");
				Assert.That(value.IsUtc, Is.False, "MinValue should be unspecified");
			}

			{
				var value = JsonDateTime.MaxValue;
				Assert.That(value.Type, Is.EqualTo(JsonType.DateTime));
				Assert.That(value.IsNull, Is.False);
				Assert.That(value.IsDefault, Is.False);
				Assert.That(value.ToDateTime(), Is.EqualTo(DateTime.MaxValue));
				Assert.That(value.IsLocalTime, Is.False, "MaxValue should be unspecified");
				Assert.That(value.IsUtc, Is.False, "MaxValue should be unspecified");
			}

			{
				var value = new JsonDateTime(1974, 3, 24);
				Assert.That(value.Type, Is.EqualTo(JsonType.DateTime));
				Assert.That(value.IsNull, Is.False);
				Assert.That(value.IsDefault, Is.False);
				Assert.That(value.ToDateTime(), Is.EqualTo(new DateTime(1974, 3, 24)));
				Assert.That(value.IsLocalTime, Is.False, "TZ is unspecified");
				Assert.That(value.IsUtc, Is.False, "TZ is unspecified");
			}

			{
				var value = new JsonDateTime(1974, 3, 24, 12, 34, 56, DateTimeKind.Utc);
				Assert.That(value.Type, Is.EqualTo(JsonType.DateTime));
				Assert.That(value.IsNull, Is.False);
				Assert.That(value.IsDefault, Is.False);
				Assert.That(value.ToDateTime(), Is.EqualTo(new DateTime(1974, 3, 24, 12, 34, 56, DateTimeKind.Utc)));
				Assert.That(value.IsLocalTime, Is.False);
				Assert.That(value.IsUtc, Is.True);
			}

			{
				var value = new JsonDateTime(1974, 3, 24, 12, 34, 56, 789, DateTimeKind.Local);
				Assert.That(value.Type, Is.EqualTo(JsonType.DateTime));
				Assert.That(value.IsNull, Is.False);
				Assert.That(value.IsDefault, Is.False);
				Assert.That(value.ToDateTime(), Is.EqualTo(new DateTime(1974, 3, 24, 12, 34, 56, 789, DateTimeKind.Local)));
				Assert.That(value.IsLocalTime, Is.True);
				Assert.That(value.IsUtc, Is.False);
			}

			{
				var now = DateTime.UtcNow;
				var value = new JsonDateTime(now.Ticks, DateTimeKind.Utc);
				Assert.That(value.Type, Is.EqualTo(JsonType.DateTime));
				Assert.That(value.IsNull, Is.False);
				Assert.That(value.IsDefault, Is.False);
				Assert.That(value.ToDateTime(), Is.EqualTo(now));
				Assert.That(value.IsLocalTime, Is.False);
				Assert.That(value.IsUtc, Is.True);
			}

			// Nullables

			Assert.That(JsonDateTime.Return((DateTime?)null).Type, Is.EqualTo(JsonType.Null));
			Assert.That(JsonDateTime.Return((DateTimeOffset?)null).Type, Is.EqualTo(JsonType.Null));
			Assert.That(JsonDateTime.Return((DateTime?)DateTime.Now).Type, Is.EqualTo(JsonType.DateTime));
			Assert.That(JsonDateTime.Return((DateTimeOffset?)DateTimeOffset.Now).Type, Is.EqualTo(JsonType.DateTime));
		}

		[Test]
		public void Test_JsonGuid()
		{
			var guid = Guid.NewGuid();

			var value = JsonString.Return(guid);
			Assert.That(value, Is.Not.Null);
			Assert.That(value.Type, Is.EqualTo(JsonType.String)); //note: pour le moment les Guid sont stockés comme des strings
			Assert.That(value.IsDefault, Is.False);
			Assert.That(value.IsNull, Is.False);
			Assert.That(value.ToString(), Is.EqualTo(guid.ToString("D"))); // "xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx"
			Assert.That(value.ToGuid(), Is.EqualTo(guid));
			Assert.That(SerializeToSlice(value), Is.EqualTo(Slice.FromString("\"" + guid.ToString("D") + "\"")));

			value = JsonString.Return(Guid.Empty);
			Assert.That(value, Is.Not.Null);
			Assert.That(value.Type, Is.EqualTo(JsonType.Null)); //TODO: pour le moment Guid.Empty => JsonNull.Null. Remplacer par JsonString.Empty?
			Assert.That(value.ToString(), Is.EqualTo(string.Empty));
			Assert.That(SerializeToSlice(value), Is.EqualTo(Slice.FromString("null")), "SerializeToSlice");

			// Nullables

			Assert.That(JsonString.Return((Guid?)null).Type, Is.EqualTo(JsonType.Null));
			Assert.That(JsonString.Return((Guid?)Guid.Empty).Type, Is.EqualTo(JsonType.Null)); //TODO: pour le moment Guid.Empty => JsonNull.Null. Remplacer par JsonString.Empty?
			Assert.That(JsonString.Return((Guid?)Guid.NewGuid()).Type, Is.EqualTo(JsonType.String));
		}

		[Test]
		public void Test_JsonArray()
		{
			{ // []
				var value = JsonArray.Empty;
				Assert.That(value.Type, Is.EqualTo(JsonType.Array));
				Assert.That(value.IsNull, Is.False);
				Assert.That(value.IsDefault, Is.False);
				Assert.That(value.ToObject(), Is.EqualTo(Array.Empty<object>()));
				Assert.That(value.Count, Is.EqualTo(0));
				Assert.That(SerializeToSlice(value), Is.EqualTo(Slice.FromString("[]")));
			}

			{ // [ "Foo" ]
				var value = JsonArray.Create("Foo");
				Assert.That(value.Type, Is.EqualTo(JsonType.Array));
				Assert.That(value.IsNull, Is.False);
				Assert.That(value.IsDefault, Is.False);
				Assert.That(value.ToObject(), Is.EqualTo(new[] { "Foo" }));
				Assert.That(value.ToObject(), Is.InstanceOf<List<object>>());
				Assert.That(value.Count, Is.EqualTo(1));
				Assert.That(value[0], Is.EqualTo(JsonString.Return("Foo")));
				Assert.That(SerializeToSlice(value), Is.EqualTo(Slice.FromString("[\"Foo\"]")));
			}

			{ // [ "Foo", [ 1, 2, 3 ], true ]
				var value = JsonArray.Create(
					"Foo",
					JsonArray.Create(1, 2, 3),
					true
				);
				Assert.That(value.Type, Is.EqualTo(JsonType.Array));
				Assert.That(value.IsNull, Is.False);
				Assert.That(value.IsDefault, Is.False);
				Assert.That(value.ToObject(), Is.EqualTo(new object[] { "Foo", new[] { 1, 2, 3 }, true }));
				Assert.That(value.ToObject(), Is.InstanceOf<List<object>>());
				Assert.That(value.Count, Is.EqualTo(3));
				Assert.That(SerializeToSlice(value), Is.EqualTo(Slice.FromString("[\"Foo\",[1,2,3],true]")));
			}
		}

		[Test]
		public void Test_JsonArray_ToJsonArray()
		{
			// JsonValue[]
			var array = (new[] {JsonNumber.One, JsonBoolean.True, JsonString.Empty}).ToJsonArray();
			Assert.That(array, Is.Not.Null);
			Assert.That(array.Count, Is.EqualTo(3));
			Assert.That(array[0], Is.EqualTo(JsonNumber.One));
			Assert.That(array[1], Is.EqualTo(JsonBoolean.True));
			Assert.That(array[2], Is.EqualTo(JsonString.Empty));

			// ICollection<JsonValue>
			array = Enumerable.Range(0, 10).Select(x => JsonNumber.Return(x)).ToList().ToJsonArray();
			Assert.That(array, Is.Not.Null);
			Assert.That(array.Count, Is.EqualTo(10));
			Assert.That(array.ToArray<int>(), Is.EqualTo(Enumerable.Range(0, 10).ToArray()));

			// IEnumerable<JsonValue>
			array = Enumerable.Range(0, 10).Select(x => JsonNumber.Return(x)).ToJsonArray();
			Assert.That(array, Is.Not.Null);
			Assert.That(array.Count, Is.EqualTo(10));
			Assert.That(array.ToArray<int>(), Is.EqualTo(Enumerable.Range(0, 10).ToArray()));

			// another JsonArray
			array = array.ToJsonArray();
			Assert.That(array, Is.Not.Null);
			Assert.That(array.Count, Is.EqualTo(10));
			Assert.That(array.ToArray<int>(), Is.EqualTo(Enumerable.Range(0, 10).ToArray()));

			// int[]
			array = new int[] {1, 2, 3}.ToJsonArray();
			Assert.That(array, Is.Not.Null);
			Assert.That(array.Count, Is.EqualTo(3));
			Assert.That(array.ToArray<int>(), Is.EqualTo(new[] {1, 2, 3}));

			// ICollection<int>
			array = new List<int>(new[] {1, 2, 3}).ToJsonArray();
			Assert.That(array, Is.Not.Null);
			Assert.That(array.Count, Is.EqualTo(3));
			Assert.That(array.ToArray<int>(), Is.EqualTo(new[] {1, 2, 3}));

			// IEnumerable<int>
			array = Enumerable.Range(1, 3).ToJsonArray();
			Assert.That(array, Is.Not.Null);
			Assert.That(array.Count, Is.EqualTo(3));
			Assert.That(array.ToArray<int>(), Is.EqualTo(new[] {1, 2, 3}));
		}

		[Test]
		public void Test_JsonArray_AddRange_Of_JsonValues()
		{
			var array = new JsonArray();

			// add elements
			array.AddRange(JsonArray.Create(1, 2));
			Assert.That(array.Count, Is.EqualTo(2));
			Assert.That(array.ToArray<int>(), Is.EqualTo(new[] { 1, 2 }));

			// add singleton
			array.AddRange(JsonArray.Create(3));
			Assert.That(array.Count, Is.EqualTo(3));
			Assert.That(array.ToArray<int>(), Is.EqualTo(new[] { 1, 2, 3 }));

			// add empty
			array.AddRange(JsonArray.Empty);
			Assert.That(array.Count, Is.EqualTo(3));
			Assert.That(array.ToArray<int>(), Is.EqualTo(new[] { 1, 2, 3 }));

			// array inception!
			array.AddRange(array);
			Assert.That(array.Count, Is.EqualTo(6));
			Assert.That(array.ToArray<int>(), Is.EqualTo(new[] { 1, 2, 3, 1, 2, 3 }));

			// capacity
			array = new JsonArray(5);
			Assert.That(array.Capacity, Is.EqualTo(5));
			array.AddRange(new JsonValue[] { 1, 2, 3 });
			Assert.That(array.Count, Is.EqualTo(3), "array.Count");
			Assert.That(array.Capacity, Is.EqualTo(5), "array.Capacity was enough");
			array.AddRange(new JsonValue[] { 4, 5 });
			Assert.That(array.Count, Is.EqualTo(5), "array.Count");
			Assert.That(array.Capacity, Is.EqualTo(5), "array.Capacity was still enough");
			array.AddRange(new JsonValue[] { 6 });
			Assert.That(array.Count, Is.EqualTo(6), "array.Count");
			Assert.That(array.Capacity, Is.EqualTo(10), "array.Capacity should have double");

			// errors
			Assert.That(() => array.AddRange(default(IEnumerable<JsonValue>)), Throws.InstanceOf<ArgumentNullException>());
		}

		[Test]
		public void Test_JsonArray_AddRange_Of_T()
		{
			var array = new JsonArray();

			// add elements
			array.AddRange<int>(new [] { 1, 2 });
			Assert.That(array.Count, Is.EqualTo(2));
			Assert.That(array.ToArray<int>(), Is.EqualTo(new[] { 1, 2 }));

			// add singleton
			array.AddRange<int>(new [] { 3 });
			Assert.That(array.Count, Is.EqualTo(3));
			Assert.That(array.ToArray<int>(), Is.EqualTo(new[] { 1, 2, 3 }));

			// add empty
			array.AddRange<int>(new int[0]);
			Assert.That(array.Count, Is.EqualTo(3));
			Assert.That(array.ToArray<int>(), Is.EqualTo(new[] { 1, 2, 3 }));

			// array inception!
			array.AddRange<int>(array.ToArray<int>());
			Assert.That(array.Count, Is.EqualTo(6));
			Assert.That(array.ToArray<int>(), Is.EqualTo(new[] { 1, 2, 3, 1, 2, 3 }));

			// capacity
			array = new JsonArray(5);
			Assert.That(array.Capacity, Is.EqualTo(5));
			array.AddRange<int>(new[] { 1, 2, 3 });
			Assert.That(array.Count, Is.EqualTo(3), "array.Count");
			Assert.That(array.Capacity, Is.EqualTo(5), "array.Capacity was enough");
			array.AddRange<int>(new[] { 4, 5 });
			Assert.That(array.Count, Is.EqualTo(5), "array.Count");
			Assert.That(array.Capacity, Is.EqualTo(5), "array.Capacity was still enough");
			array.AddRange<int>(new[] { 6 });
			Assert.That(array.Count, Is.EqualTo(6), "array.Count");
			Assert.That(array.Capacity, Is.EqualTo(10), "array.Capacity should have double");

			// with regular objects
			array = new JsonArray();
			array.AddRange(Enumerable.Range(1, 3).Select(x => new { Id = x, Name = x.ToString() }));
			Assert.That(array.Count, Is.EqualTo(3));
			for (int i = 0; i < array.Count; i++)
			{
				Assert.That(array[i], Is.Not.Null.And.InstanceOf<JsonObject>(), $"[{i}]");
				Assert.That(((JsonObject)array[i])["Id"], Is.EqualTo(JsonNumber.Return(i + 1)), $"[{i}].Id");
				Assert.That(((JsonObject)array[i])["Name"], Is.EqualTo(JsonString.Return((i + 1).ToString())), $"[{i}].Name");
				Assert.That(((JsonObject)array[i]).Count, Is.EqualTo(2), $"[{i}] Count");
			}

			// errors
			Assert.That(() => array.AddRange<int>(default(IEnumerable<int>)), Throws.InstanceOf<ArgumentNullException>());
		}

		[Test]
		public void Test_JsonArray_Capacity_Allocation()
		{
			var arr = new JsonArray();
			int old = arr.Capacity;
			int resizes = 0;
			for (int i = 0; i < 1000; i++)
			{
				arr.Add(i);
				if (arr.Capacity != old)
				{
					Log("Added {0}th triggered a realloc to {1}", arr.Count, arr.Capacity);
					old = arr.Capacity;
					++resizes;
				}
#if FULL_DEBUG
				Log(" - {0}: {1:N1} % filled, {2:N0} bytes wasted", arr.Count, 100.0 * arr.Count / arr.Capacity, (arr.Capacity - arr.Count) * IntPtr.Size);
#endif

			}
			Log("Array needed {0} to insert {1} items", resizes, arr.Count);
		}

		[Test]
		public void Test_JsonArray_Enumerable_Of_T()
		{
			{ // As<double>()
				var arr = new JsonArray(4) // capacité plus grande que count pour tester si l'iterator s'arrête correctement a 3 éléments!
				{
					123, 456, 789
				};

				var cast = arr.Cast<double>();
				using (var it = cast.GetEnumerator())
				{
					Assert.That(it.Current, Is.EqualTo(0.0), "Before first MoveNext");
					Assert.That(it.MoveNext(), Is.True, "#1");
					Assert.That(it.Current, Is.EqualTo(123.0), "#1");
					Assert.That(it.MoveNext(), Is.True, "#2");
					Assert.That(it.Current, Is.EqualTo(456.0), "#2");
					Assert.That(it.MoveNext(), Is.True, "#3");
					Assert.That(it.Current, Is.EqualTo(789.0), "#3");
					Assert.That(it.MoveNext(), Is.False, "Capacity = 4, mais Count = 3 !");
					Assert.That(it.Current, Is.EqualTo(0.0), "After last MoveNext");
				}

				var res = new List<double>();
				foreach (var d in arr.Cast<double>())
				{
					Assert.That(res, Has.Count.LessThan(3));
					res.Add(d);
				}
				Assert.That(res, Is.EqualTo(new[] { 123.0, 456.0, 789.0 }));

				Assert.That(cast.ToArray(), Is.EqualTo(new [] { 123.0, 456.0, 789.0 }));
				Assert.That(cast.ToList(), Is.EqualTo(new List<double> { 123.0, 456.0, 789.0 }));
			}
			{ // As<string>()
				var arr = new JsonArray(4) // capacité plus grande que count pour tester si l'iterator s'arrête correctement a 3 éléments!
				{
					"Hello", "World", "!!!"
				};

				var cast = arr.Cast<string>();
				using (var it = cast.GetEnumerator())
				{
					Assert.That(it.Current, Is.Null, "Before first MoveNext");
					Assert.That(it.MoveNext(), Is.True, "#1");
					Assert.That(it.Current, Is.EqualTo("Hello"), "#1");
					Assert.That(it.MoveNext(), Is.True, "#2");
					Assert.That(it.Current, Is.EqualTo("World"), "#2");
					Assert.That(it.MoveNext(), Is.True, "#3");
					Assert.That(it.Current, Is.EqualTo("!!!"), "#3");
					Assert.That(it.MoveNext(), Is.False, "Capacity = 4, mais Count = 3 !");
					Assert.That(it.Current, Is.Null, "After last MoveNext");
				}

				var res = new List<string>();
				foreach (var s in arr.Cast<string>())
				{
					Assert.That(res, Has.Count.LessThan(3));
					res.Add(s);
				}
				Assert.That(res, Is.EqualTo(new[] { "Hello", "World", "!!!" }));

				Assert.That(cast.ToArray(), Is.EqualTo(new[] { "Hello", "World", "!!!" }));
				Assert.That(cast.ToList(), Is.EqualTo(new List<string> { "Hello", "World", "!!!" }));

			}
		}

		[Test]
		public void Test_JsonArray_AsObjects()
		{
			var a = new JsonObject { ["X"] = 0, ["Y"] = 0, ["Z"] = 0 };
			var b = new JsonObject { ["X"] = 1, ["Y"] = 1, ["Z"] = 0 };
			var c = new JsonObject { ["X"] = 0, ["Y"] = 0, ["Z"] = 1 };

			{ // tous les éléments sont des objets
				var arr = new JsonArray(4) // capacité plus grande que count pour tester si l'iterator s'arrête correctement a 3 éléments!
				{
					a, b, c
				};

				var cast = arr.AsObjects();
				using (var it = cast.GetEnumerator())
				{
					Assert.That(it.Current, Is.Null, "Before first MoveNext");
					Assert.That(it.MoveNext(), Is.True, "#1");
					Assert.That(it.Current, Is.SameAs(a), "#1");
					Assert.That(it.MoveNext(), Is.True, "#2");
					Assert.That(it.Current, Is.SameAs(b), "#2");
					Assert.That(it.MoveNext(), Is.True, "#3");
					Assert.That(it.Current, Is.SameAs(c), "#3");
					Assert.That(it.MoveNext(), Is.False, "Capacity = 4, mais Count = 3 !");
					Assert.That(it.Current, Is.Null, "After last MoveNext");
				}
				Assert.That(cast.ToArray(), Is.EqualTo(new JsonObject[] {a, b, c}));
				Assert.That(cast.ToList(), Is.EqualTo(new List<JsonObject> {a, b, c}));
			}

			{ // le deuxième est null
				var arr = new JsonArray(4) // capacité plus grande que count pour tester si l'iterator s'arrête correctement a 3 éléments!
				{
					a, JsonNull.Null, c
				};

				var cast = arr.AsObjects();
				using (var it = cast.GetEnumerator())
				{
					Assert.That(it.Current, Is.Null, "Before first MoveNext");
					Assert.That(it.MoveNext(), Is.True, "#1");
					Assert.That(it.Current, Is.SameAs(a), "#1");
					Assert.That(it.MoveNext(), Is.True, "#2");
					Assert.That(it.Current, Is.Null, "#2 should be null!");
					Assert.That(it.MoveNext(), Is.True, "#3");
					Assert.That(it.Current, Is.SameAs(c), "#3");
					Assert.That(it.MoveNext(), Is.False, "Capacity = 4, mais Count = 3 !");
					Assert.That(it.Current, Is.Null, "After last MoveNext");
				}
				Assert.That(cast.ToArray(), Is.EqualTo(new JsonObject[] { a, null, c }));
				Assert.That(cast.ToList(), Is.EqualTo(new List<JsonObject> { a, null, c }));
			}

			{ // le deuxième est null mais chaque élément est required
				var arr = new JsonArray(4) // capacité plus grande que count pour tester si l'iterator s'arrête correctement a 3 éléments!
				{
					a, null, c
				};

				var cast = arr.AsObjects(required: true);
				using (var it = cast.GetEnumerator())
				{
					Assert.That(it.Current, Is.Null, "Before first MoveNext");
					Assert.That(it.MoveNext(), Is.True, "#1");
					Assert.That(it.Current, Is.SameAs(a), "#1");
					Assert.That(() => it.MoveNext(), Throws.InstanceOf<InvalidOperationException>(), "#2 should throw because null is not allowed");
				}
				Assert.That(() => cast.ToArray(), Throws.InstanceOf<InvalidOperationException>(), "ToArray() should throw because null is not allowed");
				Assert.That(() => cast.ToList(), Throws.InstanceOf<InvalidOperationException>(), "ToList() should throw because null is not allowed");
			}

			{ // le deuxième n'est pas un objet
				var arr = new JsonArray(4) // capacité plus grande que count pour tester si l'iterator s'arrête correctement a 3 éléments!
				{
					a, 123, c
				};

				var cast = arr.AsObjects();
				using (var it = cast.GetEnumerator())
				{
					Assert.That(it.Current, Is.Null, "Before first MoveNext");
					Assert.That(it.MoveNext(), Is.True, "#1");
					Assert.That(it.Current, Is.SameAs(a), "#1");
					Assert.That(() => it.MoveNext(), Throws.InstanceOf<InvalidCastException>(), "#2 should throw because it is not an object");
				}
				Assert.That(() => cast.ToArray(), Throws.InstanceOf<InvalidCastException>(), "ToArray() should throw because it is not an object");
				Assert.That(() => cast.ToList(), Throws.InstanceOf<InvalidCastException>(), "ToList() should throw because it is not an object");
			}

		}

		[Test]
		public void Test_JsonArray_AsArrays()
		{
			var a = JsonArray.Create(1, 0, 0);
			var b = JsonArray.Create(0, 1, 0);
			var c = JsonArray.Create(0, 0, 1);

			{ // tous les éléments sont des objets
				var arr = new JsonArray(4) // capacité plus grande que count pour tester si l'iterator s'arrête correctement a 3 éléments!
				{
					a, b, c
				};

				var cast = arr.AsArrays();
				using (var it = cast.GetEnumerator())
				{
					Assert.That(it.Current, Is.Null, "Before first MoveNext");
					Assert.That(it.MoveNext(), Is.True, "#1");
					Assert.That(it.Current, Is.SameAs(a), "#1");
					Assert.That(it.MoveNext(), Is.True, "#2");
					Assert.That(it.Current, Is.SameAs(b), "#2");
					Assert.That(it.MoveNext(), Is.True, "#3");
					Assert.That(it.Current, Is.SameAs(c), "#3");
					Assert.That(it.MoveNext(), Is.False, "Capacity = 4, mais Count = 3 !");
					Assert.That(it.Current, Is.Null, "After last MoveNext");
				}
				Assert.That(cast.ToArray(), Is.EqualTo(new JsonArray[] { a, b, c }));
				Assert.That(cast.ToList(), Is.EqualTo(new List<JsonArray> { a, b, c }));
			}

			{ // le deuxième est null
				var arr = new JsonArray(4) // capacité plus grande que count pour tester si l'iterator s'arrête correctement a 3 éléments!
				{
					a, JsonNull.Null, c
				};

				var cast = arr.AsArrays();
				using (var it = cast.GetEnumerator())
				{
					Assert.That(it.Current, Is.Null, "Before first MoveNext");
					Assert.That(it.MoveNext(), Is.True, "#1");
					Assert.That(it.Current, Is.SameAs(a), "#1");
					Assert.That(it.MoveNext(), Is.True, "#2");
					Assert.That(it.Current, Is.Null, "#2 should be null!");
					Assert.That(it.MoveNext(), Is.True, "#3");
					Assert.That(it.Current, Is.SameAs(c), "#3");
					Assert.That(it.MoveNext(), Is.False, "Capacity = 4, mais Count = 3 !");
					Assert.That(it.Current, Is.Null, "After last MoveNext");
				}
				Assert.That(cast.ToArray(), Is.EqualTo(new JsonArray[] { a, null, c }));
				Assert.That(cast.ToList(), Is.EqualTo(new List<JsonArray> { a, null, c }));
			}

			{ // le deuxième est null mais chaque élément est required
				var arr = new JsonArray(4) // capacité plus grande que count pour tester si l'iterator s'arrête correctement a 3 éléments!
				{
					a, null, c
				};

				var cast = arr.AsArrays(required: true);
				using (var it = cast.GetEnumerator())
				{
					Assert.That(it.Current, Is.Null, "Before first MoveNext");
					Assert.That(it.MoveNext(), Is.True, "#1");
					Assert.That(it.Current, Is.SameAs(a), "#1");
					Assert.That(() => it.MoveNext(), Throws.InstanceOf<InvalidOperationException>(), "#2 should throw because null is not allowed");
				}
				Assert.That(() => cast.ToArray(), Throws.InstanceOf<InvalidOperationException>(), "ToArray() should throw because null is not allowed");
				Assert.That(() => cast.ToList(), Throws.InstanceOf<InvalidOperationException>(), "ToList() should throw because null is not allowed");
			}

			{ // le deuxième n'est pas un objet
				var arr = new JsonArray(4) // capacité plus grande que count pour tester si l'iterator s'arrête correctement a 3 éléments!
				{
					a, 123, c
				};

				var cast = arr.AsArrays();
				using (var it = cast.GetEnumerator())
				{
					Assert.That(it.Current, Is.Null, "Before first MoveNext");
					Assert.That(it.MoveNext(), Is.True, "#1");
					Assert.That(it.Current, Is.SameAs(a), "#1");
					Assert.That(() => it.MoveNext(), Throws.InstanceOf<InvalidCastException>(), "#2 should throw because it is not an array");
				}
				Assert.That(() => cast.ToArray(), Throws.InstanceOf<InvalidCastException>(), "ToArray() should throw because it is not an array");
				Assert.That(() => cast.ToList(), Throws.InstanceOf<InvalidCastException>(), "ToList() should throw because it is not an array");
			}

		}

		[Test]
		public void Test_JsonObject()
		{
			var obj = new JsonObject();
			Assert.That(obj.Count, Is.EqualTo(0));
			Assert.That(obj.IsNull, Is.False);
			Assert.That(obj.IsDefault, Is.True);
			Assert.That(obj.ToJson(), Is.EqualTo("{ }"));
			Assert.That(SerializeToSlice(obj), Is.EqualTo(Slice.FromString("{}")));

			obj["Hello"] = "World";
			Assert.That(obj.Count, Is.EqualTo(1));
			Assert.That(obj.IsDefault, Is.False);
			Assert.That(obj.ContainsKey("Hello"), Is.True);
			Assert.That(obj["Hello"], Is.EqualTo(JsonString.Return("World")));
			Assert.That(obj.GetValue("Hello"), Is.EqualTo(JsonString.Return("World")));
			Assert.That(obj.ToJson(), Is.EqualTo("{ \"Hello\": \"World\" }"));
			Assert.That(SerializeToSlice(obj), Is.EqualTo(Slice.FromString("{\"Hello\":\"World\"}")));

			obj.Add("Foo", 123);
			Assert.That(obj.Count, Is.EqualTo(2));
			Assert.That(obj.ContainsKey("Foo"), Is.True);
			Assert.That(obj["Foo"], Is.EqualTo(JsonNumber.Return(123)));
			Assert.That(obj.GetValue("Foo"), Is.EqualTo(JsonNumber.Return(123)));
			Assert.That(obj.ToJson(), Is.EqualTo("{ \"Hello\": \"World\", \"Foo\": 123 }"));
			Assert.That(SerializeToSlice(obj), Is.EqualTo(Slice.FromString("{\"Hello\":\"World\",\"Foo\":123}")));

			obj.Set("Foo", 456);
			Assert.That(obj.Count, Is.EqualTo(2));
			Assert.That(obj.ContainsKey("Foo"), Is.True);
			Assert.That(obj["Foo"], Is.EqualTo(JsonNumber.Return(456)));
			Assert.That(obj.GetValue("Foo"), Is.EqualTo(JsonNumber.Return(456)));
			Assert.That(obj.ToJson(), Is.EqualTo("{ \"Hello\": \"World\", \"Foo\": 456 }"));
			Assert.That(SerializeToSlice(obj), Is.EqualTo(Slice.FromString("{\"Hello\":\"World\",\"Foo\":456}")));

			obj.Add("Bar", true);
			Assert.That(obj.Count, Is.EqualTo(3));
			Assert.That(obj.ContainsKey("Bar"), Is.True);
			Assert.That(obj["Bar"], Is.EqualTo(JsonBoolean.True));
			Assert.That(obj.GetValue("Bar"), Is.EqualTo(JsonBoolean.True));
			Assert.That(obj.ToJson(), Is.EqualTo("{ \"Hello\": \"World\", \"Foo\": 456, \"Bar\": true }"));
			Assert.That(SerializeToSlice(obj), Is.EqualTo(Slice.FromString("{\"Hello\":\"World\",\"Foo\":456,\"Bar\":true}")));

			// case sensitive! ('Bar' != 'BAR'
			var sub = JsonObject.Create("Alpha", 111, "Omega", 999);
			obj.Add("BAR", sub);
			Assert.That(obj.Count, Is.EqualTo(4));
			Assert.That(obj.ContainsKey("BAR"), Is.True);
			Assert.That(obj["BAR"], Is.SameAs(sub));
			Assert.That(obj.GetValue("BAR"), Is.SameAs(sub));
			Assert.That(obj.ToJson(), Is.EqualTo("{ \"Hello\": \"World\", \"Foo\": 456, \"Bar\": true, \"BAR\": { \"Alpha\": 111, \"Omega\": 999 } }"));
			Assert.That(SerializeToSlice(obj), Is.EqualTo(Slice.FromString("{\"Hello\":\"World\",\"Foo\":456,\"Bar\":true,\"BAR\":{\"Alpha\":111,\"Omega\":999}}")));

			//note: on ne sérialise pas les JsonNull "Missing"/"Error" par défaut!
			obj = JsonObject.Create("Foo", JsonNull.Null, "Bar", JsonNull.Missing, "Baz", JsonNull.Error);
			Assert.That(obj.ToJson(), Is.EqualTo("{ \"Foo\": null }"));
			Assert.That(SerializeToSlice(obj), Is.EqualTo(Slice.FromString("{\"Foo\":null}")));
		}

		[Test]
		public void Test_JsonObject_Get()
		{
			var obj = new JsonObject
			{
				["Hello"] = "World",
				["Foo"] = 123,
				["Bar"] = true,
				["Baz"] = Math.PI,
				["Void"] = null,
				["Empty"] = "",
				["Space"] = "   ", // Space! Space? Space!!!
			};

			Assert.That(obj.Get<string>("Hello"), Is.EqualTo("World"));
			Assert.That(obj.Get<string>("Hello", required: true), Is.EqualTo("World"));

			Assert.That(obj.Get<int>("Foo"), Is.EqualTo(123));
			Assert.That(obj.Get<int>("Foo", required: true), Is.EqualTo(123));

			Assert.That(obj.Get<bool>("Bar"), Is.True);
			Assert.That(obj.Get<bool>("Bar", required: true), Is.True);

			Assert.That(obj.Get<double>("Baz"), Is.EqualTo(Math.PI));
			Assert.That(obj.Get<double>("Baz", required: true), Is.EqualTo(Math.PI));

			// empty doit retourner default(T) pour les ValueType, càd 0/false/...
			Assert.That(obj.Get<string>("Empty"), Is.EqualTo(""), "'' -> string");
			Assert.That(obj.Get<int>("Empty"), Is.EqualTo(0), "'' -> int");
			Assert.That(obj.Get<bool>("Empty"), Is.False, "'' -> bool");
			Assert.That(obj.Get<double>("Empty"), Is.EqualTo(0.0), "'' -> double");
			Assert.That(obj.Get<Guid>("Empty"), Is.EqualTo(Guid.Empty), "'' -> Guid");

			// empty doit doit retourner default(?) pour les Nullable, càd null
			Assert.That(obj.Get<int?>("Empty"), Is.Null, "'' -> int?");
			Assert.That(obj.Get<bool?>("Empty"), Is.Null, "'' -> bool?");
			Assert.That(obj.Get<double?>("Empty"), Is.Null, "'' -> double?");
			Assert.That(obj.Get<Guid?>("Empty"), Is.Null, "'' -> Guid?");

			// missing + nullable
			Assert.That(obj.Get<string>("olleH"), Is.Null, "Si manquant, doit retourner null pour des types nullables");
			Assert.That(obj.Get<int?>("olleH"), Is.Null, "Si manquant, doit retourner null pour des types nullables");
			Assert.That(obj.Get<bool?>("olleH"), Is.Null, "Si manquant, doit retourner null pour des types nullables");
			Assert.That(obj.Get<double?>("olleH"), Is.Null, "Si manquant, doit retourner null pour des types nullables");

			// null + nullable
			Assert.That(obj.Get<string>("Void"), Is.Null, "Si null, doit retourner null pour des types nullables");
			Assert.That(obj.Get<int?>("Void"), Is.Null, "Si null, doit retourner null pour des types nullables");
			Assert.That(obj.Get<bool?>("Void"), Is.Null, "Si null, doit retourner null pour des types nullables");
			Assert.That(obj.Get<double?>("Void"), Is.Null, "Si null, doit retourner null pour des types nullables");

			// missing + required: true
			Assert.That(() => obj.Get<string>("olleH", required: true), Throws.InvalidOperationException.With.Message.Contains("olleH"), "Si manquant et required:true, une exception doit être lancée avec le nom du champ dans le message");
			Assert.That(() => obj.Get<int>("olleH", required: true), Throws.InvalidOperationException.With.Message.Contains("olleH"), "Si manquant et required:true, une exception doit être lancée avec le nom du champ dans le message");
			Assert.That(() => obj.Get<int?>("olleH", required: true), Throws.InvalidOperationException.With.Message.Contains("olleH"), "Si manquant et required:true, une exception doit être lancée avec le nom du champ dans le message");

			// null + required: true
			Assert.That(() => obj.Get<string>("Void", required: true), Throws.InvalidOperationException.With.Message.Contains("Void"), "Si null et required:true, une exception doit être lancée avec le nom du champ dans le message");
			Assert.That(() => obj.Get<int>("Void", required: true), Throws.InvalidOperationException.With.Message.Contains("Void"), "Si null et required:true, une exception doit être lancée avec le nom du champ dans le message");
			Assert.That(() => obj.Get<int?>("Void", required: true), Throws.InvalidOperationException.With.Message.Contains("Void"), "Si null et required:true, une exception doit être lancée avec le nom du champ dans le message");
		}

		[Test]
		public void Test_JsonObject_GetString()
		{
			// Le type string a un traitement spécial vis-à-vis des chaines vides, ce qui justifie la présence de GetString(..) (et pas GetBool, GetInt, ...)
			// - required: si true, rejette null/missing
			// - notEmpty: si true, rejette les chaines vides ou composées uniquement d'espaces

			// note: on peut avoir required:false et notEmpty:true pour des champs optionnels "si présent, alors ne doit pas être vide" (ex: un Guid optionnel, etc...)

			var obj = new JsonObject
			{
				// "Missing": not present
				["Hello"] = "World",
				["Void"] = null,
				["Empty"] = "",
				["Space"] = "   ", // Space! Space? Space!!!
			};

			//Get<string>(..) se comporte comme les autres (ne considère que null/missing)
			Assert.That(obj.Get<string>("Missing"), Is.Null);
			Assert.That(() => obj.Get<string>("Missing", required: true), Throws.InvalidOperationException);
			Assert.That(obj.Get<string>("Void"), Is.Null);
			Assert.That(() => obj.Get<string>("Void", required: true), Throws.InvalidOperationException);
			Assert.That(obj.Get<string>("Empty"), Is.EqualTo(""));
			Assert.That(obj.Get<string>("Empty", required: true), Is.EqualTo(""));
			Assert.That(obj.Get<string>("Space"), Is.EqualTo("   "));
			Assert.That(obj.Get<string>("Space", required: true), Is.EqualTo("   "));
		}

		[Test]
		public void Test_JsonObject_GetPath()
		{
			// GetPath(...) est une sorte d'équivalent à SelectSingleNode(..) qui prend un chemin de type "Foo.Bar.Baz" pour dire "le champ Baz du champ Bar du champ Foo de l'objet actuel
			// ex: obj.GetPath("Foo.Bar.Baz") est l'équivalent de obj["Foo"]["Baz"]["Baz"]

			JsonValue value;

			var obj = JsonObject.FromObject(new
			{
				Hello = "World",
				Coords = new { X = 1, Y = 2, Z = 3 },
				Foo = new { Bar = new { Baz = 123 } },
				Values = new[] {"a", "b", "c"},
				Items = JsonArray.Create(
					JsonObject.Create("Value", "one"),
					JsonObject.Create("Value", "two"),
					JsonObject.Create("Value", "three")
				),
			});
			Dump(obj);

			// Direct descendants...

			value = obj.GetPath("Hello");
			DumpCompact(value);
			Assert.That(value.Type, Is.EqualTo(JsonType.String));
			Assert.That(value.As<string>(), Is.EqualTo("World"));
			Assert.That(obj.GetPath<string>("Hello"), Is.EqualTo("World"));

			value = obj.GetPath("Coords");
			DumpCompact(value);
			Assert.That(value.Type, Is.EqualTo(JsonType.Object));

			value = obj.GetPath("Values");
			DumpCompact(value);
			Assert.That(value.Type, Is.EqualTo(JsonType.Array));

			value = obj.GetPath("NotFound");
			DumpCompact(value);
			Assert.That(value.Type, Is.EqualTo(JsonType.Null));
			Assert.That(value, Is.EqualTo(JsonNull.Missing));

			// Children

			Assert.That(obj.GetPath<int?>("Coords.X"), Is.EqualTo(1));
			Assert.That(obj.GetPath<int?>("Coords.Y"), Is.EqualTo(2));
			Assert.That(obj.GetPath<int?>("Coords.Z"), Is.EqualTo(3));
			Assert.That(obj.GetPath<int?>("Coords.NotFound"), Is.Null);

			Assert.That(obj.GetPath<int?>("Foo.Bar.Baz"), Is.EqualTo(123));
			Assert.That(obj.GetPath<int?>("Foo.Bar.NotFound"), Is.Null);
			Assert.That(obj.GetPath<int?>("Foo.NotFound.Baz"), Is.Null);
			Assert.That(obj.GetPath<int?>("NotFound.Bar.Baz"), Is.Null);

			// Array Indexing

			Assert.That(obj.GetPath<string>("Values[0]"), Is.EqualTo("a"));
			Assert.That(obj.GetPath<string>("Values[1]"), Is.EqualTo("b"));
			Assert.That(obj.GetPath<string>("Values[2]"), Is.EqualTo("c"));
			Assert.That(() => obj.GetPath<string>("Values[3]"), Throws.InstanceOf<ArgumentOutOfRangeException>());
			Assert.That(() => obj.GetPath<string>("Values[2].NotFound"), Throws.InstanceOf<InvalidOperationException>());

			Assert.That(obj.GetPath<string>("Items[0].Value"), Is.EqualTo("one"));
			Assert.That(obj.GetPath<string>("Items[1].Value"), Is.EqualTo("two"));
			Assert.That(obj.GetPath<string>("Items[2].Value"), Is.EqualTo("three"));
			Assert.That(obj.GetPath<string>("Items[0].NotFound"), Is.Null);
			Assert.That(() => obj.GetPath("Items[3]"), Throws.InstanceOf<ArgumentOutOfRangeException>());

			// Required
			Assert.That(() => obj.GetPath<int?>("NotFound.Bar.Baz", required: true), Throws.InvalidOperationException);
			Assert.That(() => obj.GetPath<int?>("Coords.NotFound", required: true), Throws.InvalidOperationException);

			obj = new JsonObject
			{
				["X"] = default(string),
				["Y"] = default(Guid?),
				["Z"] = JsonNull.Missing
			};
			Assert.That(() => obj.GetPath<string>("X", required: true), Throws.InvalidOperationException);
			Assert.That(() => obj.GetPath<Guid?>("Y", required: true), Throws.InvalidOperationException);
			Assert.That(() => obj.GetPath<string>("Z", required: true), Throws.InvalidOperationException);
			Assert.That(() => obj.GetPath<string>("間", required: true), Throws.InvalidOperationException);
		}

		[Test]
		public void Test_JsonObject_SetPath()
		{
			var obj = JsonObject.Empty;

			// create
			obj.SetPath("Hello", "World");
			DumpCompact(obj);
			Assert.That(obj.Count, Is.EqualTo(1));
			Assert.That(obj, Is.EqualTo(JsonValue.Parse(@"{ ""Hello"": ""World"" }")));
			Assert.That(obj.GetPath<string>("Hello"), Is.EqualTo("World"));

			// update
			obj.SetPath("Hello", "Le Monde!");
			DumpCompact(obj);
			Assert.That(obj.Count, Is.EqualTo(1));
			Assert.That(obj, Is.EqualTo(JsonValue.Parse(@"{ ""Hello"": ""Le Monde!"" }")));
			Assert.That(obj.GetPath<string>("Hello"), Is.EqualTo("Le Monde!"));

			// add other
			obj.SetPath("Level", 9001);
			DumpCompact(obj);
			Assert.That(obj.Count, Is.EqualTo(2));
			Assert.That(obj, Is.EqualTo(JsonValue.Parse(@"{ ""Hello"": ""Le Monde!"", ""Level"": 9001 }")));
			Assert.That(obj.GetPath<int?>("Level"), Is.EqualTo(9001));

			// null => JsonNull.Null
			obj.SetPath("Hello", null);
			DumpCompact(obj);
			Assert.That(obj.Count, Is.EqualTo(2));
			Assert.That(obj, Is.EqualTo(JsonValue.Parse(@"{ ""Hello"": null, ""Level"": 9001 }")));
			Assert.That(obj.GetPath<string>("Hello"), Is.Null);

			// remove
			obj.RemovePath("Hello");
			DumpCompact(obj);
			Assert.That(obj.Count, Is.EqualTo(1));
			Assert.That(obj, Is.EqualTo(JsonValue.Parse(@"{ ""Level"": 9001 }")));
			Assert.That(obj.GetPath<string>("Hello"), Is.Null);

			obj.RemovePath("Level");
			DumpCompact(obj);
			Assert.That(obj.Count, Is.EqualTo(0));
			Assert.That(obj.GetPath<int?>("Level"), Is.Null);
		}

		[Test]
		public void Test_JsonObject_SetPath_SubObject()
		{
			var obj = JsonObject.Empty;
			obj.SetPath("Foo.Bar", 123);
			DumpCompact(obj);
			Assert.That(obj, Is.EqualTo(JsonValue.Parse(@"{ ""Foo"": { ""Bar"": 123 } }")));
			Assert.That(obj.GetPath<int?>("Foo.Bar"), Is.EqualTo(123));

			obj.SetPath("Foo.Baz", 456);
			DumpCompact(obj);
			Assert.That(obj, Is.EqualTo(JsonValue.Parse(@"{ ""Foo"": { ""Bar"": 123, ""Baz"": 456 } }")));
			Assert.That(obj.GetPath<int?>("Foo.Baz"), Is.EqualTo(456));

			obj.RemovePath("Foo.Bar");
			DumpCompact(obj);
			Assert.That(obj, Is.EqualTo(JsonValue.Parse(@"{ ""Foo"": { ""Baz"": 456 } }")));
			Assert.That(obj.GetPath<int?>("Foo.Bar"), Is.Null);

			obj.RemovePath("Foo");
			DumpCompact(obj);
			Assert.That(obj.Count, Is.EqualTo(0));
		}

		[Test]
		public void Test_JsonObject_SetPath_SubArray()
		{
			var obj = JsonObject.Empty;

			obj.SetPath("Foos[0]", 123);
			DumpCompact(obj);
			Assert.That(obj, Is.EqualTo(JsonValue.Parse(@"{ ""Foos"": [ 123 ] }")));
			Assert.That(obj.GetPath<int?>("Foos[0]"), Is.EqualTo(123));

			obj.SetPath("Foos[1]", 456);
			DumpCompact(obj);
			Assert.That(obj, Is.EqualTo(JsonValue.Parse(@"{ ""Foos"": [ 123, 456 ] }")));
			Assert.That(obj.GetPath<int?>("Foos[1]"), Is.EqualTo(456));

			obj.SetPath("Foos[3]", 789); //skip one
			DumpCompact(obj);
			Assert.That(obj, Is.EqualTo(JsonValue.Parse(@"{ ""Foos"": [ 123, 456, null, 789 ] }")));
			Assert.That(obj.GetPath<int?>("Foos[2]"), Is.Null);
			Assert.That(obj.GetPath<int?>("Foos[3]"), Is.EqualTo(789));
		}

		[Test]
		public void Test_JsonObject_SetPath_SubArray_Of_Objects()
		{
			var obj = JsonObject.Empty;

			obj.SetPath("Foos[0]", JsonObject.Create("X", 1, "Y", 2, "Z", 3));
			DumpCompact(obj);
			Assert.That(obj, Is.EqualTo(JsonValue.Parse(@"{ ""Foos"" : [ { ""X"": 1, ""Y"": 2, ""Z"": 3 } ] }")));
			Assert.That(obj.GetPath("Foos[0]"), Is.EqualTo(JsonValue.Parse(@"{ ""X"": 1, ""Y"": 2, ""Z"": 3 }")));

			obj.SetPath("Foos[2]", JsonObject.Create("X", 4, "Y", 5, "Z", 6));
			DumpCompact(obj);
			Assert.That(obj, Is.EqualTo(JsonValue.Parse(@"{ ""Foos"" : [ { ""X"": 1, ""Y"": 2, ""Z"": 3 }, null, { ""X"": 4, ""Y"": 5, ""Z"": 6 } ] }")));
			Assert.That(obj.GetPath("Foos[1]"), Is.EqualTo(JsonNull.Null));
			Assert.That(obj.GetPath("Foos[2]"), Is.EqualTo(JsonValue.Parse(@"{ ""X"": 4, ""Y"": 5, ""Z"": 6 }")));

			// auto-created
			obj = JsonObject.Empty;
			obj.SetPath("Foos[0].X", 1);
			obj.SetPath("Foos[0].Y", 2);
			obj.SetPath("Foos[0].Z", 3);
			DumpCompact(obj);
			Assert.That(obj, Is.EqualTo(JsonValue.Parse(@"{ ""Foos"" : [ { ""X"": 1, ""Y"": 2, ""Z"": 3 } ] }")));
			Assert.That(obj.GetPath<int?>("Foos[0].X"), Is.EqualTo(1));
			Assert.That(obj.GetPath<int?>("Foos[0].Y"), Is.EqualTo(2));
			Assert.That(obj.GetPath<int?>("Foos[0].Z"), Is.EqualTo(3));

			obj.SetPath("Foos[2].X", 4);
			obj.SetPath("Foos[2].Y", 5);
			obj.SetPath("Foos[2].Z", 6);
			DumpCompact(obj);
			Assert.That(obj, Is.EqualTo(JsonValue.Parse(@"{ ""Foos"" : [ { ""X"": 1, ""Y"": 2, ""Z"": 3 }, null, { ""X"": 4, ""Y"": 5, ""Z"": 6 } ] }")));
			Assert.That(obj.GetPath<int?>("Foos[2].X"), Is.EqualTo(4));
			Assert.That(obj.GetPath<int?>("Foos[2].Y"), Is.EqualTo(5));
			Assert.That(obj.GetPath<int?>("Foos[2].Z"), Is.EqualTo(6));
		}

		[Test]
		public void Test_JsonObject_SetPath_SubArray_Of_Arrays()
		{
			var obj = JsonObject.Empty;

			obj.SetPath("Matrix[0][2]", 1);
			obj.SetPath("Matrix[1][1]", 2);
			obj.SetPath("Matrix[2][0]", 3);
			DumpCompact(obj);
			Assert.That(obj, Is.EqualTo(JsonValue.Parse(@"{ ""Matrix"" : [ [ null, null, 1 ], [ null, 2 ], [ 3 ] ] }")));
			Assert.That(obj.GetPath<int?>("Matrix[0][2]"), Is.EqualTo(1));
			Assert.That(obj.GetPath<int?>("Matrix[1][1]"), Is.EqualTo(2));
			Assert.That(obj.GetPath<int?>("Matrix[2][0]"), Is.EqualTo(3));

			obj.SetPath("Matrix[0][0]", 4);
			obj.SetPath("Matrix[2][1]", 5);
			DumpCompact(obj);
			Assert.That(obj, Is.EqualTo(JsonValue.Parse(@"{ ""Matrix"" : [ [ 4, null, 1 ], [ null, 2 ], [ 3, 5 ] ] }")));
			Assert.That(obj.GetPath<int?>("Matrix[0][0]"), Is.EqualTo(4));
			Assert.That(obj.GetPath<int?>("Matrix[2][1]"), Is.EqualTo(5));

			obj = JsonObject.Empty;
			obj.SetPath("Foos[0][2].Bar", 123);
			DumpCompact(obj);
			Assert.That(obj, Is.EqualTo(JsonValue.Parse(@"{ ""Foos"" : [ [ null, null, { ""Bar"": 123 } ] ] }")));
			Assert.That(obj.GetPath<int?>("Foos[0][2].Bar"), Is.EqualTo(123));
			obj.SetPath("Foos[0][2].Bar", 456);
			DumpCompact(obj);
			Assert.That(obj, Is.EqualTo(JsonValue.Parse(@"{ ""Foos"" : [ [ null, null, { ""Bar"": 456 } ] ] }")));
			Assert.That(obj.GetPath<int?>("Foos[0][2].Bar"), Is.EqualTo(456));

			obj = JsonObject.Empty;
			obj.SetPath("Foos[0].Bar[2]", 123);
			DumpCompact(obj);
			Assert.That(obj, Is.EqualTo(JsonValue.Parse(@"{ ""Foos"" : [ { ""Bar"": [ null, null, 123 ] } ] }")));
			Assert.That(obj.GetPath<int?>("Foos[0].Bar[2]"), Is.EqualTo(123));
		}

		[Test]
		public void Test_JsonObject_GetOrCreateObject()
		{
			var root = JsonObject.Empty;
			var foo = root.GetOrCreateObject("Foo");
			Assert.That(foo, Is.Not.Null, "Foo");
			Assert.That(root, Is.EqualTo(JsonValue.Parse(@"{ ""Foo"": {} }")));

			root.GetOrCreateObject("Bar").Set("Baz", 123);
			Assert.That(root, Is.EqualTo(JsonValue.Parse(@"{ ""Foo"": {}, ""Bar"": { ""Baz"": 123 } }")));

			root.GetOrCreateObject("Bar").Set("Hello", "World");
			Assert.That(root, Is.EqualTo(JsonValue.Parse(@"{ ""Foo"": {}, ""Bar"": {""Baz"":123, ""Hello"": ""World"" } }")));

			root = JsonObject.Empty;
			root.GetOrCreateObject("Narf.Zort.Poit").Set("MDR", "LOL");
			Assert.That(root, Is.EqualTo(JsonValue.Parse(@"{ ""Narf"": { ""Zort"": { ""Poit"": { ""MDR"": ""LOL"" } } } }")));

			// on doit pouvoir écraser un null
			root = JsonObject.Empty;
			root["Bar"] = JsonNull.Null;
			var bar = root.GetOrCreateObject("Bar");
			Assert.That(bar, Is.Not.Null, "Bar");
			bar.Set("Hello", "World");
			Assert.That(root, Is.EqualTo(JsonValue.Parse(@"{ ""Bar"": { ""Hello"": ""World"" } }")));

			// par contre on doit pas pouvoir écraser un non-object
			root = JsonObject.Create("Baz", "Hello");
			Assert.That(
				() => root.GetOrCreateObject("Baz"),
				Throws.InstanceOf<InvalidOperationException>().With.Message.EqualTo("The specified key 'Baz' exists, but is of type String instead of expected Object"),
				"Expected error message (can change!)"
			);
		}

		[Test]
		public void Test_JsonObject_MergeWith()
		{

			// Différents fields sans conflit
			// { Foo: 123 } u { Bar: 456 } => { Foo: 123, Bar: 456 }
			var root = JsonObject.Empty.Set("Foo", 123);
			var obj = JsonObject.Empty.Set("Bar", 456);

			root.MergeWith(obj);
			Assert.That(root.ToJsonCompact(), Is.EqualTo(@"{""Foo"":123,""Bar"":456}"));

			// Ecrase un value type par un autre
			// { Foo: 123 } u { Foo: 456 } => { Foo: 456 }
			root = JsonObject.Empty.Set("Foo", 123);
			obj = JsonObject.Empty.Set("Foo", 456);

			root.MergeWith(obj);
			Assert.That(root.ToJsonCompact(), Is.EqualTo(@"{""Foo"":456}"));

			// Null overwrite un objet
			// { Foo: { Bar: 42 } } u { Foo: null } => { Foo: null }
			root = JsonObject.Empty;
			root.GetOrCreateObject("Foo").Set("Bar", 42);
			obj = JsonObject.Empty;
			obj["Foo"] = JsonNull.Null;

			root.MergeWith(obj);
			Assert.That(root.ToJsonCompact(), Is.EqualTo(@"{""Foo"":null}"));

			// Merge le contenu d'un même sous objet
			// { Narf: { Zort: 42 } } u  { Narf: { Poit: 666 } } => { Narf: { Zort: 42, Poit: 666 } }
			root = JsonObject.Empty;
			root.GetOrCreateObject("Narf").Set("Zort", 42);
			obj = JsonObject.Empty;
			obj.GetOrCreateObject("Narf").Set("Poit", 666);

			root.MergeWith(obj);
			Assert.That(root.ToJsonCompact(), Is.EqualTo(@"{""Narf"":{""Zort"":42,""Poit"":666}}"));
		}

		[Test]
		public void Test_JsonObject_GetOrCreateArray()
		{
			var root = JsonObject.Empty;

			var foo = root.GetOrCreateArray("Foo");
			Assert.That(foo, Is.Not.Null, "Foo");
			Assert.That(foo.Count, Is.EqualTo(0), "foo.Count");
			Assert.That(root.ToJson(CrystalJsonSettings.JsonCompact), Is.EqualTo(@"{""Foo"":[]}"));

			foo.AddValue(123);
			Assert.That(root.ToJson(CrystalJsonSettings.JsonCompact), Is.EqualTo(@"{""Foo"":[123]}"));

			root.GetOrCreateArray("Foo").AddValue(456);
			Assert.That(root.ToJson(CrystalJsonSettings.JsonCompact), Is.EqualTo(@"{""Foo"":[123,456]}"));

			root = JsonObject.Empty;
			root.GetOrCreateArray("Narf.Zort.Poit").AddValue(789);
			Assert.That(root.ToJson(CrystalJsonSettings.JsonCompact), Is.EqualTo(@"{""Narf"":{""Zort"":{""Poit"":[789]}}}"));

			// on doit pouvoir écraser un null
			root = JsonObject.Empty;
			root["Bar"] = JsonNull.Null;
			var bar = root.GetOrCreateArray("Bar");
			Assert.That(bar, Is.Not.Null, "Bar");
			bar.AddValue("Hello");
			bar.AddValue("World");
			Assert.That(root.ToJson(CrystalJsonSettings.JsonCompact), Is.EqualTo(@"{""Bar"":[""Hello"",""World""]}"));

			// par contre on doit pas pouvoir écraser un non-object
			root = JsonObject.Empty;
			root.Set("Baz", "Hello");
			var x = Assert.Throws<InvalidOperationException>(() => root.GetOrCreateArray("Baz"));
			Assert.That(x?.Message, Is.EqualTo("The specified key 'Baz' exists, but is of type String instead of expected Array"), "Expected error message (can change!)");
		}

		[Test]
		public void Test_JsonObject_FromException_Compact()
		{
			//note: throw and catch the exception to have an actual StackTrace
			Exception ex;
			try
			{
				try
				{
					throw new InvalidOperationException("Oh noes!");
				}
				catch (Exception e1)
				{
					throw new FileNotFoundException("I'm missing a coin", fileName: "C:\\path\\to\\file.ext", innerException: e1);
				}
			}
			catch (Exception e2)
			{
				ex = e2;
			}

			var obj = JsonObject.FromException(ex, includeTypes: false);
			Assert.That(obj, Is.Not.Null);
			Dump(obj);

			Assert.That(obj.Get<string>("ClassName"), Is.EqualTo("System.IO.FileNotFoundException"), ".ClassName");
			Assert.That(obj.Get<string>("Message"), Is.EqualTo("I'm missing a coin"), ".Message");
			Assert.That(obj.Get<string>("FileNotFound_FileName"), Is.EqualTo("C:\\path\\to\\file.ext"), ".Message");
			Assert.That(obj.Get<string>("Source"), Is.EqualTo("Doxense.Core.Tests"), ".Source"); //note: assembly name
			Assert.That(obj.Get<string>("StackTraceString"), Is.Not.Null.Or.Empty, ".StackTraceString");
			Assert.That(obj.Get<int>("HResult"), Is.EqualTo(-2147024894), ".HResult");

			var inner = obj.GetObject("InnerException");
			Assert.That(inner, Is.Not.Null, ".InnerException");
			Assert.That(inner.Get<string>("ClassName"), Is.EqualTo("System.InvalidOperationException"), "InnerException.ClassName");
			Assert.That(inner.Get<string>("Message"), Is.EqualTo("Oh noes!"), "InnerException.Message");
			Assert.That(inner.Get<string>("Source"), Is.EqualTo("Doxense.Core.Tests"), ".InnerException.Source"); //note: assembly name
			Assert.That(inner.Get<string>("StackTraceString"), Is.Not.Null.Or.Empty, "InnerException.StackTraceString");
			Assert.That(inner.Get<int>("HResult"), Is.EqualTo(-2146233079), ".HResult");
		}

		[Test]
		public void Test_JsonObject_FromException_Roundtrip()
		{

			void Check(JsonValue o, string name, string expectedType, IResolveConstraint valueConstraint)
			{
				var arr = o[name].AsArray(required: false);
				Assert.That(arr, Is.Not.Null, "Property '{0}' is missing", name);
				Assert.That(arr.Count, Is.EqualTo(2), $"Array should have exactly 2 elements: {arr:P}");
				Assert.That(arr[0].ToStringOrDefault(), Is.EqualTo(expectedType), "Item type does not match for {0}", name);
				Assert.That(arr[1], valueConstraint, "Value does not match for {0}", name);
			}

			//note: throw and catch the exception to have an actual StackTrace
			Exception ex;
			try
			{
				try
				{
					throw new InvalidOperationException("Oh noes!");
				}
				catch (Exception e1)
				{
					throw new FileNotFoundException("I'm missing a coin", fileName: "C:\\path\\to\\file.ext", innerException: e1);
				}
			}
			catch (Exception e2)
			{
				ex = e2;
			}

			var obj = JsonObject.FromException(ex, includeTypes: true);
			Assert.That(obj, Is.Not.Null);
			Dump(obj);

			Check(obj, "ClassName", "string", Is.EqualTo("System.IO.FileNotFoundException"));
			Check(obj, "Message", "string", Is.EqualTo("I'm missing a coin"));
			Check(obj, "FileNotFound_FileName", "string", Is.EqualTo("C:\\path\\to\\file.ext"));
			Check(obj, "Source", "string", Is.EqualTo("Doxense.Core.Tests")); //note: assembly name
			Check(obj, "StackTraceString", "string", Is.Not.Null.Or.Empty);
			Check(obj, "ExceptionMethod", "string", Is.Not.Null.Or.Empty);
			Check(obj, "HResult", "int", Is.EqualTo(-2147024894));
			Check(obj, "InnerException", "System.Exception", Is.Not.EqualTo(JsonNull.Null));

			var inner = obj.GetArray("InnerException")![1];
			Check(inner, "ClassName", "string", Is.EqualTo("System.InvalidOperationException"));
			Check(inner, "Message", "string", Is.EqualTo("Oh noes!"));
			Check(inner, "Source", "string", Is.EqualTo("Doxense.Core.Tests")); //note: assembly name
			Check(inner, "StackTraceString", "string", Is.Not.Null.Or.Empty);
			Check(inner, "ExceptionMethod", "string", Is.Not.Null.Or.Empty);
			Check(inner, "HResult", "int", Is.EqualTo(-2146233079));
			Check(inner, "InnerException", "System.Exception", Is.EqualTo(JsonNull.Null));
		}

		[Test]
		public void Test_JsonValue_FromValue_Basic_Types()
		{
			// FromValue<T>(T)
			Assert.That(JsonValue.FromValue(null), Is.InstanceOf<JsonNull>());
			Assert.That(JsonValue.FromValue(DBNull.Value), Is.InstanceOf<JsonNull>());
			Assert.That(JsonValue.FromValue(123), Is.InstanceOf<JsonNumber>());
			Assert.That(JsonValue.FromValue(123456L), Is.InstanceOf<JsonNumber>());
			Assert.That(JsonValue.FromValue(123.4f), Is.InstanceOf<JsonNumber>());
			Assert.That(JsonValue.FromValue(123.456d), Is.InstanceOf<JsonNumber>());
			Assert.That(JsonValue.FromValue(default(uint)), Is.InstanceOf<JsonNumber>());
			Assert.That(JsonValue.FromValue(default(ulong)), Is.InstanceOf<JsonNumber>());
			Assert.That(JsonValue.FromValue(default(sbyte)), Is.InstanceOf<JsonNumber>());
			Assert.That(JsonValue.FromValue(default(byte)), Is.InstanceOf<JsonNumber>());
			Assert.That(JsonValue.FromValue(default(short)), Is.InstanceOf<JsonNumber>());
			Assert.That(JsonValue.FromValue(default(ushort)), Is.InstanceOf<JsonNumber>());
			Assert.That(JsonValue.FromValue(default(decimal)), Is.InstanceOf<JsonNumber>());
			Assert.That(JsonValue.FromValue(false), Is.InstanceOf<JsonBoolean>());
			Assert.That(JsonValue.FromValue(true), Is.InstanceOf<JsonBoolean>());
			Assert.That(JsonValue.FromValue("hello"), Is.InstanceOf<JsonString>());
			Assert.That(JsonValue.FromValue(String.Empty), Is.InstanceOf<JsonString>());
			Assert.That(JsonValue.FromValue(Guid.NewGuid()), Is.InstanceOf<JsonString>());
			Assert.That(JsonValue.FromValue(System.Net.IPAddress.Loopback), Is.InstanceOf<JsonString>());
			Assert.That(JsonValue.FromValue(DateTime.Now), Is.InstanceOf<JsonDateTime>());
			Assert.That(JsonValue.FromValue(TimeSpan.FromMinutes(1)), Is.InstanceOf<JsonNumber>());
			Assert.That(JsonValue.FromValue(new Uri("https://www.youtube.com/watch?v=dQw4w9WgXcQ")), Is.InstanceOf<JsonString>());

			// FromValue(object)
			Assert.That(JsonValue.FromValue((object)null), Is.InstanceOf<JsonNull>());
			Assert.That(JsonValue.FromValue((object)DBNull.Value), Is.InstanceOf<JsonNull>());
			Assert.That(JsonValue.FromValue((object)123), Is.InstanceOf<JsonNumber>());
			Assert.That(JsonValue.FromValue((object)123456L), Is.InstanceOf<JsonNumber>());
			Assert.That(JsonValue.FromValue((object)123.4f), Is.InstanceOf<JsonNumber>());
			Assert.That(JsonValue.FromValue((object)123.456d), Is.InstanceOf<JsonNumber>());
			Assert.That(JsonValue.FromValue((object)default(uint)), Is.InstanceOf<JsonNumber>());
			Assert.That(JsonValue.FromValue((object)default(ulong)), Is.InstanceOf<JsonNumber>());
			Assert.That(JsonValue.FromValue((object)default(sbyte)), Is.InstanceOf<JsonNumber>());
			Assert.That(JsonValue.FromValue((object)default(byte)), Is.InstanceOf<JsonNumber>());
			Assert.That(JsonValue.FromValue((object)default(short)), Is.InstanceOf<JsonNumber>());
			Assert.That(JsonValue.FromValue((object)default(ushort)), Is.InstanceOf<JsonNumber>());
			Assert.That(JsonValue.FromValue((object)default(decimal)), Is.InstanceOf<JsonNumber>());
			Assert.That(JsonValue.FromValue((object)false), Is.InstanceOf<JsonBoolean>());
			Assert.That(JsonValue.FromValue((object)true), Is.InstanceOf<JsonBoolean>());
			Assert.That(JsonValue.FromValue((object)"hello"), Is.InstanceOf<JsonString>());
			Assert.That(JsonValue.FromValue((object)String.Empty), Is.InstanceOf<JsonString>());
			Assert.That(JsonValue.FromValue((object)Guid.NewGuid()), Is.InstanceOf<JsonString>());
			Assert.That(JsonValue.FromValue((object)System.Net.IPAddress.Loopback), Is.InstanceOf<JsonString>());
			Assert.That(JsonValue.FromValue((object)DateTime.Now), Is.InstanceOf<JsonDateTime>());
			Assert.That(JsonValue.FromValue((object)TimeSpan.FromMinutes(1)), Is.InstanceOf<JsonNumber>());
			Assert.That(JsonValue.FromValue((object)new Uri("https://www.youtube.com/watch?v=dQw4w9WgXcQ")), Is.InstanceOf<JsonString>());
		}

		[Test]
		public void Test_JsonObject_Project()
		{
			var obj = JsonObject.FromObject(new
			{
				Id = 1,
				Name = "Walter White",
				Pseudo = "Einsenberg",
				Occupation = "Chemistry Teacher",
				Hobby = "Cook"
			});
			Dump(obj);

			JsonObject p;

			p = obj.Pick("Id", "Name");
			Assert.That(p, Is.Not.Null.And.Not.SameAs(obj));
			DumpCompact(p);
			Assert.That(p.Get<int>("Id"), Is.EqualTo(1));
			Assert.That(p.Get<string>("Name"), Is.EqualTo("Walter White"));
			Assert.That(p.Count, Is.EqualTo(2));
			// l'original ne doit pas être modifié
			Assert.That(obj.Count, Is.EqualTo(5));

			p = obj.Pick("Id");
			Assert.That(p, Is.Not.Null.And.Not.SameAs(obj));
			DumpCompact(p);
			Assert.That(p.Get<int>("Id"), Is.EqualTo(1));
			Assert.That(p.Count, Is.EqualTo(1));

			p = obj.Pick("Id", "NotFound");
			Assert.That(p, Is.Not.Null.And.Not.SameAs(obj));
			DumpCompact(p);
			Assert.That(p.Get<int>("Id"), Is.EqualTo(1));
			Assert.That(p.ContainsKey("NotFound"), Is.False);
			Assert.That(p.Count, Is.EqualTo(1));

			p = obj.Pick(new[] {"Id", "NotFound"}, keepMissing: true);
			Assert.That(p, Is.Not.Null.And.Not.SameAs(obj));
			DumpCompact(p);
			Assert.That(p.Get<int>("Id"), Is.EqualTo(1));
			Assert.That(p.ContainsKey("NotFound"), Is.True);
			Assert.That(p["NotFound"], Is.EqualTo(JsonNull.Missing));
			Assert.That(p.Count, Is.EqualTo(2));

			p = obj.Pick("NotFound");
			Assert.That(p, Is.Not.Null.And.Not.SameAs(obj));
			DumpCompact(p);
			Assert.That(p.Count, Is.EqualTo(0));

			p = obj.Pick(new[] {"NotFound"}, keepMissing: true);
			Assert.That(p, Is.Not.Null.And.Not.SameAs(obj));
			DumpCompact(p);
			Assert.That(p.ContainsKey("NotFound"), Is.True);
			Assert.That(p["NotFound"], Is.EqualTo(JsonNull.Missing));
			Assert.That(p.Count, Is.EqualTo(1));
		}

		[Test]
		public void Test_JsonArray_Projection()
		{
			var arr = new JsonArray()
			{
				JsonObject.FromObject(new {Id = 1, Name = "Walter White", Pseudo = "Einsenberg", Job = "Cook", Sickness = "Lung Cancer"}),
				JsonObject.FromObject(new {Id = 2, Name = "Jesse Pinkman", Job = "Drug Dealer"}),
				JsonObject.FromObject(new {Id = 3, Name = "Walter White, Jr", Pseudo = "Flynn", Sickness = "Cerebral Palsy"}),
				JsonObject.FromObject(new {Foo = "bar", Version = 1}), // completely unrelated object (probably a bug)
				JsonObject.Empty, // empty object
				JsonNull.Null, // Null should not be changed
				JsonNull.Missing, // Missing should be converted to Null
				null, // null should be changed to Null
			};
			Log("arr = " + arr.ToJsonIndented());

#region Pick (drop missing)...

			// si la clé n'existe pas dans la source, elle n'est pas non plus dans le résultat

			var proj = arr.Pick("Id", "Name", "Pseudo", "Job", "Version");

			Assert.That(proj, Is.Not.Null.And.Not.SameAs(arr));
			Log("proj = " + proj.ToJsonIndented());
			Assert.That(proj.Count, Is.EqualTo(arr.Count));

			JsonObject p;

			p = (JsonObject) proj[0];
			Assert.That(p, Is.Not.Null.And.Not.SameAs(arr[0]));
			Assert.That(p.Get<int>("Id"), Is.EqualTo(1));
			Assert.That(p.Get<string>("Name"), Is.EqualTo("Walter White"));
			Assert.That(p.Get<string>("Pseudo"), Is.EqualTo("Einsenberg"));
			Assert.That(p.Get<string>("Job"), Is.EqualTo("Cook"));
			Assert.That(p.ContainsKey("Version"), Is.False);
			Assert.That(p.Count, Is.EqualTo(4));

			p = (JsonObject) proj[1];
			Assert.That(p, Is.Not.Null.And.Not.SameAs(arr[1]));
			Assert.That(p.Get<int>("Id"), Is.EqualTo(2));
			Assert.That(p.Get<string>("Name"), Is.EqualTo("Jesse Pinkman"));
			Assert.That(p.ContainsKey("Pseudo"), Is.False);
			Assert.That(p.Get<string>("Job"), Is.EqualTo("Drug Dealer"));
			Assert.That(p.ContainsKey("Version"), Is.False);
			Assert.That(p.Count, Is.EqualTo(3));

			p = (JsonObject) proj[2];
			Assert.That(p, Is.Not.Null.And.Not.SameAs(arr[2]));
			Assert.That(p.Get<int>("Id"), Is.EqualTo(3));
			Assert.That(p.Get<string>("Name"), Is.EqualTo("Walter White, Jr"));
			Assert.That(p.Get<string>("Pseudo"), Is.EqualTo("Flynn"));
			Assert.That(p.ContainsKey("Job"), Is.False);
			Assert.That(p.ContainsKey("Version"), Is.False);
			Assert.That(p.Count, Is.EqualTo(3));

			p = (JsonObject) proj[3];
			Assert.That(p, Is.Not.Null.And.Not.SameAs(arr[3]));
			Assert.That(p.ContainsKey("Id"), Is.False);
			Assert.That(p.ContainsKey("Name"), Is.False);
			Assert.That(p.ContainsKey("Pseudo"), Is.False);
			Assert.That(p.ContainsKey("Job"), Is.False);
			Assert.That(p.Get<int>("Version"), Is.EqualTo(1));
			Assert.That(p.Count, Is.EqualTo(1));

			p = (JsonObject) proj[4];
			Assert.That(p, Is.Not.Null.And.Not.SameAs(arr[5]));
			Assert.That(p.Count, Is.EqualTo(0));

			Assert.That(proj[5], Is.Not.Null);
			Assert.That(proj[5].Type, Is.EqualTo(JsonType.Null));

			Assert.That(proj[6], Is.Not.Null);
			Assert.That(proj[6].Type, Is.EqualTo(JsonType.Null));

			Assert.That(proj[7], Is.Not.Null);
			Assert.That(proj[7].Type, Is.EqualTo(JsonType.Null));

#endregion

#region Pick (keep missing)...

			// si la clé n'existe pas dans la source, elle vaut JsonNull.Missing dans le résultat

			proj = arr.Pick(
				new[] {"Id", "Name", "Pseudo", "Job"},
				keepMissing: true
			);

			Assert.That(proj, Is.Not.Null.And.Not.SameAs(arr));
			Log("proj = " + proj.ToJsonIndented());
			Assert.That(proj.Count, Is.EqualTo(arr.Count));

			p = (JsonObject) proj[0];
			Assert.That(p, Is.Not.Null.And.Not.SameAs(arr[0]));
			Assert.That(p.Get<int>("Id"), Is.EqualTo(1));
			Assert.That(p.Get<string>("Name"), Is.EqualTo("Walter White"));
			Assert.That(p.Get<string>("Pseudo"), Is.EqualTo("Einsenberg"));
			Assert.That(p.Get<string>("Job"), Is.EqualTo("Cook"));
			Assert.That(p.Count, Is.EqualTo(4));

			p = (JsonObject) proj[1];
			Assert.That(p, Is.Not.Null.And.Not.SameAs(arr[1]));
			Assert.That(p.Get<int>("Id"), Is.EqualTo(2));
			Assert.That(p.Get<string>("Name"), Is.EqualTo("Jesse Pinkman"));
			Assert.That(p["Pseudo"], Is.EqualTo(JsonNull.Missing));
			Assert.That(p.Get<string>("Job"), Is.EqualTo("Drug Dealer"));
			Assert.That(p.Count, Is.EqualTo(4));

			p = (JsonObject) proj[2];
			Assert.That(p, Is.Not.Null.And.Not.SameAs(arr[2]));
			Assert.That(p.Get<int>("Id"), Is.EqualTo(3));
			Assert.That(p.Get<string>("Name"), Is.EqualTo("Walter White, Jr"));
			Assert.That(p.Get<string>("Pseudo"), Is.EqualTo("Flynn"));
			Assert.That(p["Job"], Is.EqualTo(JsonNull.Missing));
			Assert.That(p.Count, Is.EqualTo(4));

			p = (JsonObject) proj[3];
			Assert.That(p, Is.Not.Null.And.Not.SameAs(arr[3]));
			Assert.That(p["Id"], Is.EqualTo(JsonNull.Missing));
			Assert.That(p["Name"], Is.EqualTo(JsonNull.Missing));
			Assert.That(p["Pseudo"], Is.EqualTo(JsonNull.Missing));
			Assert.That(p["Job"], Is.EqualTo(JsonNull.Missing));
			Assert.That(p.Count, Is.EqualTo(4));

			p = (JsonObject) proj[4];
			Assert.That(p, Is.Not.Null.And.Not.SameAs(arr[5]));
			Assert.That(p["Id"], Is.EqualTo(JsonNull.Missing));
			Assert.That(p["Name"], Is.EqualTo(JsonNull.Missing));
			Assert.That(p["Pseudo"], Is.EqualTo(JsonNull.Missing));
			Assert.That(p["Job"], Is.EqualTo(JsonNull.Missing));
			Assert.That(p.Count, Is.EqualTo(4));

			Assert.That(proj[5], Is.Not.Null);
			Assert.That(proj[5].Type, Is.EqualTo(JsonType.Null));

			Assert.That(proj[6], Is.Not.Null);
			Assert.That(proj[6].Type, Is.EqualTo(JsonType.Null));

			Assert.That(proj[7], Is.Not.Null);
			Assert.That(proj[7].Type, Is.EqualTo(JsonType.Null));

#endregion

#region Pick (with JSON defaults)

			// si la clé n'existe pas dans la source, elle est remplacée par la valeur par défaut

			proj = arr.Pick(
				new JsonObject()
				{
					["Id"] = JsonNull.Error, // <= équivalent de null, mais qui peut être détecté spécifiquement
					["Name"] = JsonString.Return("John Doe"),
					["Pseudo"] = JsonNull.Null,
					["Job"] = JsonString.Return("NEET"),
					["Version"] = JsonNumber.Zero,
				});

			Assert.That(proj, Is.Not.Null.And.Not.SameAs(arr));
			Log("proj = " + proj.ToJsonIndented());
			Assert.That(proj.Count, Is.EqualTo(arr.Count));

			p = (JsonObject) proj[0];
			Assert.That(p, Is.Not.Null.And.Not.SameAs(arr[0]));
			Assert.That(p.Get<int>("Id"), Is.EqualTo(1));
			Assert.That(p.Get<string>("Name"), Is.EqualTo("Walter White"));
			Assert.That(p.Get<string>("Pseudo"), Is.EqualTo("Einsenberg"));
			Assert.That(p.Get<string>("Job"), Is.EqualTo("Cook"));
			Assert.That(p.Count, Is.EqualTo(5));

			p = (JsonObject) proj[1];
			Assert.That(p, Is.Not.Null.And.Not.SameAs(arr[1]));
			Assert.That(p.Get<int>("Id"), Is.EqualTo(2));
			Assert.That(p.Get<string>("Name"), Is.EqualTo("Jesse Pinkman"));
			Assert.That(p["Pseudo"], Is.EqualTo(JsonNull.Null));
			Assert.That(p.Get<string>("Job"), Is.EqualTo("Drug Dealer"));
			Assert.That(p.Get<int>("Version"), Is.EqualTo(0));
			Assert.That(p.Count, Is.EqualTo(5));

			p = (JsonObject) proj[2];
			Assert.That(p, Is.Not.Null.And.Not.SameAs(arr[2]));
			Assert.That(p.Get<int>("Id"), Is.EqualTo(3));
			Assert.That(p.Get<string>("Name"), Is.EqualTo("Walter White, Jr"));
			Assert.That(p.Get<string>("Pseudo"), Is.EqualTo("Flynn"));
			Assert.That(p.Get<string>("Job"), Is.EqualTo("NEET"));
			Assert.That(p.Get<int>("Version"), Is.EqualTo(0));
			Assert.That(p.Count, Is.EqualTo(5));

			p = (JsonObject) proj[3];
			Assert.That(p, Is.Not.Null.And.Not.SameAs(arr[3]));
			Assert.That(p["Id"], Is.EqualTo(JsonNull.Error));
			Assert.That(p.Get<string>("Name"), Is.EqualTo("John Doe"));
			Assert.That(p["Pseudo"], Is.EqualTo(JsonNull.Null));
			Assert.That(p.Get<string>("Job"), Is.EqualTo("NEET"));
			Assert.That(p.Get<int>("Version"), Is.EqualTo(1));
			Assert.That(p.Count, Is.EqualTo(5));

			p = (JsonObject) proj[4];
			Assert.That(p, Is.Not.Null.And.Not.SameAs(arr[5]));
			Assert.That(p["Id"], Is.EqualTo(JsonNull.Error));
			Assert.That(p.Get<string>("Name"), Is.EqualTo("John Doe"));
			Assert.That(p["Pseudo"], Is.EqualTo(JsonNull.Null));
			Assert.That(p.Get<string>("Job"), Is.EqualTo("NEET"));
			Assert.That(p.Get<int>("Version"), Is.EqualTo(0));
			Assert.That(p.Count, Is.EqualTo(5));

			Assert.That(proj[5], Is.Not.Null);
			Assert.That(proj[5].Type, Is.EqualTo(JsonType.Null));

			Assert.That(proj[6], Is.Not.Null);
			Assert.That(proj[6].Type, Is.EqualTo(JsonType.Null));

			Assert.That(proj[7], Is.Not.Null);
			Assert.That(proj[7].Type, Is.EqualTo(JsonType.Null));

#endregion

#region Pick (with object defaults)

			// si la clé n'existe pas dans la source, elle est remplacée par la valeur par défaut en se basant sur le contenu d'un objet anonyme

			proj = arr.Pick(
				new
				{
					Id = JsonNull.Error, // <= équivalent de null, mais qui peut être détecté spécifiquement
					Name = "John Doe",
					Pseudo = JsonNull.Null,
					Job = "NEET",
					Version = 0,
				});

			Assert.That(proj, Is.Not.Null.And.Not.SameAs(arr));
			Log("proj = " + proj.ToJsonIndented());
			Assert.That(proj.Count, Is.EqualTo(arr.Count));

			p = (JsonObject) proj[0];
			Assert.That(p, Is.Not.Null.And.Not.SameAs(arr[0]));
			Assert.That(p.Get<int>("Id"), Is.EqualTo(1));
			Assert.That(p.Get<string>("Name"), Is.EqualTo("Walter White"));
			Assert.That(p.Get<string>("Pseudo"), Is.EqualTo("Einsenberg"));
			Assert.That(p.Get<string>("Job"), Is.EqualTo("Cook"));
			Assert.That(p.Get<int>("Version"), Is.EqualTo(0));
			Assert.That(p.Count, Is.EqualTo(5));

			p = (JsonObject) proj[1];
			Assert.That(p, Is.Not.Null.And.Not.SameAs(arr[1]));
			Assert.That(p.Get<int>("Id"), Is.EqualTo(2));
			Assert.That(p.Get<string>("Name"), Is.EqualTo("Jesse Pinkman"));
			Assert.That(p["Pseudo"], Is.EqualTo(JsonNull.Null));
			Assert.That(p.Get<string>("Job"), Is.EqualTo("Drug Dealer"));
			Assert.That(p.Get<int>("Version"), Is.EqualTo(0));
			Assert.That(p.Count, Is.EqualTo(5));

			p = (JsonObject) proj[2];
			Assert.That(p, Is.Not.Null.And.Not.SameAs(arr[2]));
			Assert.That(p.Get<int>("Id"), Is.EqualTo(3));
			Assert.That(p.Get<string>("Name"), Is.EqualTo("Walter White, Jr"));
			Assert.That(p.Get<string>("Pseudo"), Is.EqualTo("Flynn"));
			Assert.That(p.Get<string>("Job"), Is.EqualTo("NEET"));
			Assert.That(p.Get<int>("Version"), Is.EqualTo(0));
			Assert.That(p.Count, Is.EqualTo(5));

			p = (JsonObject) proj[3];
			Assert.That(p, Is.Not.Null.And.Not.SameAs(arr[3]));
			Assert.That(p["Id"], Is.EqualTo(JsonNull.Error));
			Assert.That(p.Get<string>("Name"), Is.EqualTo("John Doe"));
			Assert.That(p["Pseudo"], Is.EqualTo(JsonNull.Null));
			Assert.That(p.Get<string>("Job"), Is.EqualTo("NEET"));
			Assert.That(p.Get<int>("Version"), Is.EqualTo(1));
			Assert.That(p.Count, Is.EqualTo(5));

			p = (JsonObject) proj[4];
			Assert.That(p, Is.Not.Null.And.Not.SameAs(arr[5]));
			Assert.That(p["Id"], Is.EqualTo(JsonNull.Error));
			Assert.That(p.Get<string>("Name"), Is.EqualTo("John Doe"));
			Assert.That(p["Pseudo"], Is.EqualTo(JsonNull.Null));
			Assert.That(p.Get<string>("Job"), Is.EqualTo("NEET"));
			Assert.That(p.Get<int>("Version"), Is.EqualTo(0));
			Assert.That(p.Count, Is.EqualTo(5));

			Assert.That(proj[5], Is.Not.Null);
			Assert.That(proj[5].Type, Is.EqualTo(JsonType.Null));

			Assert.That(proj[6], Is.Not.Null);
			Assert.That(proj[6].Type, Is.EqualTo(JsonType.Null));

			Assert.That(proj[7], Is.Not.Null);
			Assert.That(proj[7].Type, Is.EqualTo(JsonType.Null));

#endregion

		}

		[Test]
		public void Test_JsonArray_Flatten()
		{
			var array = JsonArray.Create
			(
				1,
				JsonArray.Create(2, 3, 4),
				5
			);

			var flat = array.Flatten();
			Assert.That(flat, Is.Not.Null);
			Assert.That(flat.ToArray<int>(), Is.EqualTo(new[] { 1, 2, 3, 4, 5 }));

		}

		[Test]
		public void Test_JsonValue_FromObject_Enums()
		{
			var result = JsonValue.FromValue(DateTimeKind.Utc);
			Assert.That(result, Is.InstanceOf<JsonString>());
			Assert.That(result.ToString(), Is.EqualTo("Utc"));
		}

		[Test]
		public void Test_JsonValue_FromObject_Nullable()
		{
			//note: un nullable<int> semble etre boxé en tant que int s'il a une valeur, donc on perd l'information de type si on appele FromObject(object)
			// => du coup je force en appelant FromObject(..., typeof(int?)) pour être certain que cela fonctionne bien dans tous les cas

			Assert.That(JsonValue.FromValue(null, typeof(int?)), Is.InstanceOf<JsonNull>());
			int? x = 123;
			Assert.That(JsonValue.FromValue(x, typeof(int?)), Is.InstanceOf<JsonNumber>());

			Assert.That(JsonValue.FromValue(null, typeof(DateTime?)), Is.InstanceOf<JsonNull>());
			DateTime? d = DateTime.Now;
			Assert.That(JsonValue.FromValue(d, typeof(DateTime?)), Is.InstanceOf<JsonDateTime>());

			Assert.That(JsonValue.FromValue(null, typeof(Guid?)), Is.InstanceOf<JsonNull>());
			Guid? g = Guid.NewGuid();
			Assert.That(JsonValue.FromValue(g, typeof(Guid?)), Is.InstanceOf<JsonString>());

			Assert.That(JsonValue.FromValue(null, typeof(DateTimeKind?)), Is.InstanceOf<JsonNull>());
			DateTimeKind? k = DateTimeKind.Utc;
			Assert.That(JsonValue.FromValue(k, typeof(DateTimeKind?)), Is.InstanceOf<JsonString>());
		}

		[Test]
		public void Test_JsonValue_FromObject_Lists()
		{
			// array of primitive type
			var j = JsonValue.FromValue(new[] { 1, 42, 77 });
			Assert.That(j, Is.Not.Null.And.InstanceOf<JsonArray>());
			Log(j); // => [ 1, 42, 77 ]
			var arr = j.AsArray(required: true);
			Assert.That(arr.Count, Is.EqualTo(3));
			Assert.That(arr[0], Is.Not.Null);
			Assert.That(arr.Get<int>(0), Is.EqualTo(1));
			Assert.That(arr.Get<int>(1), Is.EqualTo(42));
			Assert.That(arr.Get<int>(2), Is.EqualTo(77));

			// list of primitive type
			j = JsonValue.FromValue(new List<int> { 1, 42, 77 });
			Assert.That(j, Is.Not.Null.And.InstanceOf<JsonArray>());
			Log(j); // => [ 1, 42, 77 ]
			arr = j.AsArray(required: true);
			Assert.That(arr.Count, Is.EqualTo(3));
			Assert.That(arr.Get<int>(0), Is.EqualTo(1));
			Assert.That(arr.Get<int>(1), Is.EqualTo(42));
			Assert.That(arr.Get<int>(2), Is.EqualTo(77));

			// array of ref type
			j = JsonValue.FromValue(new[] { "foo", "bar", "baz" });
			Assert.That(j, Is.Not.Null.And.InstanceOf<JsonArray>());
			Log(j); // [ "foo", "bar", "baz" ]
			arr = j.AsArray(required: true);
			Assert.That(arr.Count, Is.EqualTo(3));
			Assert.That(arr.Get<string>(0), Is.EqualTo("foo"));
			Assert.That(arr.Get<string>(1), Is.EqualTo("bar"));
			Assert.That(arr.Get<string>(2), Is.EqualTo("baz"));

			// special collection (read only)
			j = JsonValue.FromValue(new ReadOnlyCollection<string>(new List<string> { "foo", "bar", "baz" }));
			Assert.That(j, Is.Not.Null.And.InstanceOf<JsonArray>());
			Log(j); // [ "foo", "bar", "baz" ]
			arr = j.AsArray(required: true);
			Assert.That(arr.Count, Is.EqualTo(3));
			Assert.That(arr.Get<string>(0), Is.EqualTo("foo"));
			Assert.That(arr.Get<string>(1), Is.EqualTo("bar"));
			Assert.That(arr.Get<string>(2), Is.EqualTo("baz"));

			// LINQ query
			j = JsonValue.FromValue(Enumerable.Range(1, 10));
			Assert.That(j, Is.Not.Null.And.InstanceOf<JsonArray>());
			Log(j); // => [ 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 ]
			arr = j.AsArray(required: true);
			Assert.That(arr.Count, Is.EqualTo(10));
			Assert.That(arr.ToArray<int>(), Is.EqualTo(Enumerable.Range(1, 10).ToArray()));

			j = JsonValue.FromValue(Enumerable.Range(1, 3).Select(x => new KeyValuePair<int, char>(x, (char)(64 + x))).ToList());
			Assert.That(j, Is.Not.Null.And.InstanceOf<JsonArray>());
			Log(j);
			arr = j.AsArray(required: true);
			Assert.That(arr.Count, Is.EqualTo(3));
			//BUGBUG: pour l'instant ca retourne [ { Key: .., Value: .. }, .. ] au lieu de [ [ .., .. ], .. ]
		}

		[Test]
		public void Test_JsonValue_FromObject_Dictionary()
		{
			//string keys...

			var j = JsonValue.FromValue(new Dictionary<string, int> {{"foo", 11}, {"bar", 22}, {"baz", 33}});
			Assert.That(j, Is.Not.Null.And.InstanceOf<JsonObject>());
			Log(j); // { "foo": 11, "bar": 22, "baz": 33 }
			var obj = j.AsObject(required: true);
			Assert.That(obj.Count, Is.EqualTo(3));
			Assert.That(obj.Get<int>("foo"), Is.EqualTo(11));
			Assert.That(obj.Get<int>("bar"), Is.EqualTo(22));
			Assert.That(obj.Get<int>("baz"), Is.EqualTo(33));

			var g1 = Guid.NewGuid();
			var g2 = Guid.NewGuid();
			var g3 = Guid.NewGuid();
			j = JsonValue.FromValue(new Dictionary<string, Guid> { { "foo", g1 }, { "bar", g2 }, { "baz", g3 } });
			Log(j); // { "foo": ..., "bar": ..., "baz": ... }
			obj = j.AsObject(required: true);
			Assert.That(obj.Count, Is.EqualTo(3));
			Assert.That(obj.Get<Guid>("foo"), Is.EqualTo(g1));
			Assert.That(obj.Get<Guid>("bar"), Is.EqualTo(g2));
			Assert.That(obj.Get<Guid>("baz"), Is.EqualTo(g3));

			var dic = Enumerable.Range(0, 3).Select(x => new {Id = x, Name = "User#" + x.ToString(), Level = x * 9000}).ToDictionary(x => x.Name);
			obj = JsonObject.FromObject(dic);
			Log(obj);
			Assert.That(obj.Count, Is.EqualTo(3));

			// non-string keys...

			j = JsonValue.FromValue(new Dictionary<int, string> {{11, "foo"}, {22, "bar"}, {33, "baz"}});
			Assert.That(j, Is.Not.Null.And.InstanceOf<JsonObject>());
			Log(j); // { "11": "foo", "22": "bar", "33": "baz" }
			obj = j.AsObject(required: true);
			Assert.That(obj.Count, Is.EqualTo(3));
			Assert.That(obj.Get<string>("11"), Is.EqualTo("foo"));
			Assert.That(obj.Get<string>("22"), Is.EqualTo("bar"));
			Assert.That(obj.Get<string>("33"), Is.EqualTo("baz"));

			// on peut aussi obtenir un JsonObject directement
			obj = JsonObject.FromObject(new Dictionary<string, int> {{"foo", 11}, {"bar", 22}, {"baz", 33}});
			Assert.That(obj, Is.Not.Null);
			Log(obj);
			Assert.That(obj.Count, Is.EqualTo(3));
			Assert.That(obj.Get<int>("foo"), Is.EqualTo(11));
			Assert.That(obj.Get<int>("bar"), Is.EqualTo(22));
			Assert.That(obj.Get<int>("baz"), Is.EqualTo(33));
		}

		[Test]
		public void Test_JsonValue_FromObject_STuples()
		{
			// STuple<...>
			Assert.That(JsonValue.FromValue(STuple.Empty).ToJson(), Is.EqualTo("[ ]"));
			Assert.That(JsonValue.FromValue(STuple.Create(123)).ToJson(), Is.EqualTo("[ 123 ]"));
			Assert.That(JsonValue.FromValue(STuple.Create(123, "Hello")).ToJson(), Is.EqualTo("[ 123, \"Hello\" ]"));
			Assert.That(JsonValue.FromValue(STuple.Create(123, "Hello", true)).ToJson(), Is.EqualTo("[ 123, \"Hello\", true ]"));
			Assert.That(JsonValue.FromValue(STuple.Create(123, "Hello", true, -1.5)).ToJson(), Is.EqualTo("[ 123, \"Hello\", true, -1.5 ]"));
			Assert.That(JsonValue.FromValue(STuple.Create(123, "Hello", true, -1.5, 'Z')).ToJson(), Is.EqualTo("[ 123, \"Hello\", true, -1.5, \"Z\" ]"));
			Assert.That(JsonValue.FromValue(STuple.Create(123, "Hello", true, -1.5, 'Z', new DateTime(2016, 11, 24, 11, 07, 23))).ToJson(), Is.EqualTo("[ 123, \"Hello\", true, -1.5, \"Z\", \"2016-11-24T11:07:23\" ]"));

			// (ITuple) STuple<...>
			Assert.That(JsonValue.FromValue((IVarTuple) STuple.Empty).ToJson(), Is.EqualTo("[ ]"));
			Assert.That(JsonValue.FromValue((IVarTuple) STuple.Create(123)).ToJson(), Is.EqualTo("[ 123 ]"));
			Assert.That(JsonValue.FromValue((IVarTuple) STuple.Create(123, "Hello")).ToJson(), Is.EqualTo("[ 123, \"Hello\" ]"));
			Assert.That(JsonValue.FromValue((IVarTuple) STuple.Create(123, "Hello", true)).ToJson(), Is.EqualTo("[ 123, \"Hello\", true ]"));
			Assert.That(JsonValue.FromValue((IVarTuple) STuple.Create(123, "Hello", true, -1.5)).ToJson(), Is.EqualTo("[ 123, \"Hello\", true, -1.5 ]"));
			Assert.That(JsonValue.FromValue((IVarTuple) STuple.Create(123, "Hello", true, -1.5, 'Z')).ToJson(), Is.EqualTo("[ 123, \"Hello\", true, -1.5, \"Z\" ]"));
			Assert.That(JsonValue.FromValue((IVarTuple) STuple.Create(123, "Hello", true, -1.5, 'Z', new DateTime(2016, 11, 24, 11, 07, 23))).ToJson(), Is.EqualTo("[ 123, \"Hello\", true, -1.5, \"Z\", \"2016-11-24T11:07:23\" ]"));

			// custom tuple types
			Assert.That(JsonValue.FromValue(new ListTuple<int>(new [] { 1, 2, 3 })).ToJson(), Is.EqualTo("[ 1, 2, 3 ]"));
			Assert.That(JsonValue.FromValue(new ListTuple<string>(new [] { "foo", "bar", "baz" })).ToJson(), Is.EqualTo("[ \"foo\", \"bar\", \"baz\" ]"));
			Assert.That(JsonValue.FromValue(new ListTuple<object>(new object[] { "hello world", 123, false })).ToJson(), Is.EqualTo("[ \"hello world\", 123, false ]"));
			Assert.That(JsonValue.FromValue(new LinkedTuple<int>(STuple.Create(1, 2), 3)).ToJson(), Is.EqualTo("[ 1, 2, 3 ]"));
			Assert.That(JsonValue.FromValue(new JoinedTuple(STuple.Create(1, 2), STuple.Create(3))).ToJson(), Is.EqualTo("[ 1, 2, 3 ]"));
		}

		[Test]
		public void Test_JsonValue_FromObject_ValueTuples()
		{
			// STuple<...>
			Assert.That(JsonValue.FromValue(ValueTuple.Create()).ToJson(), Is.EqualTo("[ ]"));
			Assert.That(JsonValue.FromValue(ValueTuple.Create(123)).ToJson(), Is.EqualTo("[ 123 ]"));
			Assert.That(JsonValue.FromValue(ValueTuple.Create(123, "Hello")).ToJson(), Is.EqualTo("[ 123, \"Hello\" ]"));
			Assert.That(JsonValue.FromValue(ValueTuple.Create(123, "Hello", true)).ToJson(), Is.EqualTo("[ 123, \"Hello\", true ]"));
			Assert.That(JsonValue.FromValue(ValueTuple.Create(123, "Hello", true, -1.5)).ToJson(), Is.EqualTo("[ 123, \"Hello\", true, -1.5 ]"));
			Assert.That(JsonValue.FromValue(ValueTuple.Create(123, "Hello", true, -1.5, 'Z')).ToJson(), Is.EqualTo("[ 123, \"Hello\", true, -1.5, \"Z\" ]"));
			Assert.That(JsonValue.FromValue(ValueTuple.Create(123, "Hello", true, -1.5, 'Z', new DateTime(2016, 11, 24, 11, 07, 23))).ToJson(), Is.EqualTo("[ 123, \"Hello\", true, -1.5, \"Z\", \"2016-11-24T11:07:23\" ]"));

			// (ITuple) STuple<...>
			Assert.That(JsonValue.FromValue((System.Runtime.CompilerServices.ITuple) ValueTuple.Create()).ToJson(), Is.EqualTo("[ ]"));
			Assert.That(JsonValue.FromValue((System.Runtime.CompilerServices.ITuple) ValueTuple.Create(123)).ToJson(), Is.EqualTo("[ 123 ]"));
			Assert.That(JsonValue.FromValue((System.Runtime.CompilerServices.ITuple) ValueTuple.Create(123, "Hello")).ToJson(), Is.EqualTo("[ 123, \"Hello\" ]"));
			Assert.That(JsonValue.FromValue((System.Runtime.CompilerServices.ITuple) ValueTuple.Create(123, "Hello", true)).ToJson(), Is.EqualTo("[ 123, \"Hello\", true ]"));
			Assert.That(JsonValue.FromValue((System.Runtime.CompilerServices.ITuple) ValueTuple.Create(123, "Hello", true, -1.5)).ToJson(), Is.EqualTo("[ 123, \"Hello\", true, -1.5 ]"));
			Assert.That(JsonValue.FromValue((System.Runtime.CompilerServices.ITuple) ValueTuple.Create(123, "Hello", true, -1.5, 'Z')).ToJson(), Is.EqualTo("[ 123, \"Hello\", true, -1.5, \"Z\" ]"));
			Assert.That(JsonValue.FromValue((System.Runtime.CompilerServices.ITuple) ValueTuple.Create(123, "Hello", true, -1.5, 'Z', new DateTime(2016, 11, 24, 11, 07, 23))).ToJson(), Is.EqualTo("[ 123, \"Hello\", true, -1.5, \"Z\", \"2016-11-24T11:07:23\" ]"));
		}

		[Test]
		public void Test_JsonValue_FromObject_AnonymousType()
		{
			var value = new { foo = 123, bar = false, hello = "world" };
			var result = JsonValue.FromValue(value);
			Assert.That(result, Is.InstanceOf<JsonObject>());

			var obj = (JsonObject)result;
			Assert.That(obj.Count, Is.EqualTo(3));
			Assert.That(obj.Get<int?>("foo"), Is.EqualTo(123));
			Assert.That(obj.Get<bool?>("bar"), Is.False);
			Assert.That(obj.Get<string>("hello"), Is.EqualTo("world"));
		}

		[Test]
		public void Test_JsonValue_FromObject_CustomClass()
		{
			var agent = new DummyJsonClass()
			{
				Name = "James Bond",
				Index = 7,
				Size = 123456789,
				Height = 1.8f,
				Amount = 0.07d,
				Created = new DateTime(1968, 5, 8, 0, 0, 0, DateTimeKind.Utc),
				Modified = new DateTime(2010, 10, 28, 15, 39, 0, DateTimeKind.Utc),
				State = DummyJsonEnum.Bar,
			};

			var v = JsonValue.FromValue(agent);
			Assert.That(v, Is.Not.Null.And.Property("Type").EqualTo(JsonType.Object));

			Log(v.ToJsonIndented());

			var j = (JsonObject)v;
			Assert.That(j["Name"], Is.Not.Null.And.Property("Type").EqualTo(JsonType.String));
			Assert.That(j["Index"], Is.Not.Null.And.Property("Type").EqualTo(JsonType.Number));
			Assert.That(j["Size"], Is.Not.Null.And.Property("Type").EqualTo(JsonType.Number));
			Assert.That(j["Height"], Is.Not.Null.And.Property("Type").EqualTo(JsonType.Number));
			Assert.That(j["Amount"], Is.Not.Null.And.Property("Type").EqualTo(JsonType.Number));
			Assert.That(j["Created"], Is.Not.Null.And.Property("Type").EqualTo(JsonType.DateTime));
			Assert.That(j["Modified"], Is.Not.Null.And.Property("Type").EqualTo(JsonType.DateTime));
			Assert.That(j["State"], Is.Not.Null.And.Property("Type").EqualTo(JsonType.String));
			//TODO: ignore defaults?
			//Assert.That(j.Count, Is.EqualTo(8));

			Assert.That(j.Get<string>("Name"), Is.EqualTo(agent.Name), ".Name");
			Assert.That(j.Get<int>("Index"), Is.EqualTo(agent.Index), ".Index");
			Assert.That(j.Get<long>("Size"), Is.EqualTo(agent.Size), ".Size");
			Assert.That(j.Get<float>("Height"), Is.EqualTo(agent.Height), ".Height");
			Assert.That(j.Get<double>("Amount"), Is.EqualTo(agent.Amount), ".Amount");
			Assert.That(j.Get<DateTime>("Created"), Is.EqualTo(agent.Created), ".Created");
			Assert.That(j.Get<DateTime>("Modified"), Is.EqualTo(agent.Modified), ".Modified");
			Assert.That(j["State"].ToEnum<DummyJsonEnum>(), Is.EqualTo(agent.State), ".State");
		}

		[Test]
		public void Test_JsonValue_FromValue_DerivedClassMember()
		{
			var x = new DummyOuterDerivedClass()
			{
				Id = 7,
				Agent = new DummyDerivedJsonClass("Janov Bondovicz")
				{
					Name = "James Bond",
					Index = 7,
					Size = 123456789,
					Height = 1.8f,
					Amount = 0.07d,
					Created = new DateTime(1968, 5, 8, 0, 0, 0, DateTimeKind.Utc),
					Modified = new DateTime(2010, 10, 28, 15, 39, 0, DateTimeKind.Utc),
					State = DummyJsonEnum.Bar,
				},
			};

			var j = JsonValue.FromValue((object)x);
			Assert.That(j, Is.Not.Null);
			Log(j.ToJsonIndented());
			Assert.That(j.Type, Is.EqualTo(JsonType.Object), "FromObject((TClass)obj) should return a JsonObject");
			var obj = (JsonObject)j;
			Assert.That(obj.Get<string>("_class"), Is.Null, "Not on top level");
			Assert.That(obj.ContainsKey("Agent"));
			Assert.That(obj.GetPath("Agent._class").ToString(), Is.EqualTo(typeof(DummyDerivedJsonClass).FullName + ", " + typeof(DummyDerivedJsonClass).Assembly.GetName().Name), "On sub-object");
			var y = obj.As<DummyOuterDerivedClass>();
			Assert.That(y.Agent, Is.Not.Null.And.InstanceOf<DummyDerivedJsonClass>());
			Assert.That(y.Agent.Name, Is.EqualTo("James Bond"));

			j = JsonValue.FromValue((object)x.Agent);
			Assert.That(j, Is.Not.Null);
			Log(j.ToJsonIndented());
			Assert.That(j.Type, Is.EqualTo(JsonType.Object), "FromObject((TDerived)obj) should return a JsonObject");
			obj = (JsonObject)j;
			Assert.That(obj.Get<string>("_class"), Is.Null, "FromObject(foo) assumes that the runtime type is known, and does not need to be output");
			var z = obj.As<DummyDerivedJsonClass>();
			Assert.That(z, Is.Not.Null.And.InstanceOf<DummyDerivedJsonClass>());
			Assert.That(z.Name, Is.EqualTo("James Bond"));

			j = JsonValue.FromValue<DummyJsonClass>(x.Agent);
			Assert.That(j, Is.Not.Null);
			Log(j.ToJsonIndented());
			Assert.That(j.Type, Is.EqualTo(JsonType.Object), "FromValue<TClass>() should return a JsonObject");
			obj = (JsonObject)j;
			Assert.That(obj.Get<string>("_class"), Is.EqualTo(typeof(DummyDerivedJsonClass).FullName + ", " + typeof(DummyDerivedJsonClass).Assembly.GetName().Name), "FromValue<TBase>((TDerived)foo) should output the class id");
			var w = obj.As<DummyJsonClass>();
			Assert.That(w, Is.Not.Null.And.InstanceOf<DummyDerivedJsonClass>());
			Assert.That(w.Name, Is.EqualTo("James Bond"));

		}

		[Test]
		public void Test_JsonValue_FromObject_JsonValue()
		{
			// Si on FromValue(..) quelquechose qui est déja un JsonValue, on doit récupérer la même instance!

			JsonValue value;

			value = JsonNull.Null;
			Assert.That(JsonValue.FromValue(value), Is.SameAs(value));

			value = JsonString.Return("hello world");
			Assert.That(JsonValue.FromValue(value), Is.SameAs(value));

			value = JsonNumber.Return(12345);
			Assert.That(JsonValue.FromValue(value), Is.SameAs(value));

			value = JsonNumber.Return(12345678L);
			Assert.That(JsonValue.FromValue(value), Is.SameAs(value));

			value = JsonNumber.Return(Math.PI);
			Assert.That(JsonValue.FromValue(value), Is.SameAs(value));

			value = JsonNumber.Return(float.NaN);
			Assert.That(JsonValue.FromValue(value), Is.SameAs(value));
		}

#endregion

		#region Parse...

		[Test]
		public void Test_Parse_Null()
		{
			Assert.That(CrystalJson.Parse("null"), Is.EqualTo(JsonNull.Null), "Parse('null')");
			Assert.That(CrystalJson.Parse(String.Empty), Is.EqualTo(JsonNull.Missing), "Parse('')");
			Assert.That(CrystalJson.Parse(default(string)), Is.EqualTo(JsonNull.Missing), "Parse(default(string))");

			Assert.That(CrystalJson.Parse(new byte[0]), Is.EqualTo(JsonNull.Missing), "Parse('')");
			Assert.That(CrystalJson.Parse(default(byte[])), Is.EqualTo(JsonNull.Missing), "Parse(default(string))");
			Assert.That(CrystalJson.Parse(new byte[10], 5, 0), Is.EqualTo(JsonNull.Missing), "Parse('')");

			Assert.That(CrystalJson.Parse(Slice.Empty), Is.EqualTo(JsonNull.Missing), "Parse(Slice.Empty)");
			Assert.That(CrystalJson.Parse(Slice.Nil), Is.EqualTo(JsonNull.Missing), "Parse(Slice.Nil)");

			Assert.That(CrystalJson.Parse(ReadOnlySpan<byte>.Empty), Is.EqualTo(JsonNull.Missing), "Parse(ReadOnlySpan<byte>.Empty)");
		}

		[Test]
		public void Test_Parse_Boolean()
		{
			Assert.That(CrystalJson.Parse("true"), Is.EqualTo(JsonBoolean.True), "Parse('true')");
			Assert.That(CrystalJson.Parse("false"), Is.EqualTo(JsonBoolean.False), "Parse('false')");

			// Il faut le mot complet
			Assert.That(() => CrystalJson.Parse("tru"), Throws.InstanceOf<JsonSyntaxException>(), "Parse('tru')");
			Assert.That(() => CrystalJson.Parse("flse"), Throws.InstanceOf<JsonSyntaxException>(), "Parse('flse')");

			// Mais pas plus que nécessaire
			Assert.That(() => CrystalJson.Parse("truee"), Throws.InstanceOf<JsonSyntaxException>(), "Parse('tru')");
			Assert.That(() => CrystalJson.Parse("falsee"), Throws.InstanceOf<JsonSyntaxException>(), "Parse('flse')");

			// c'est case sensitive!
			Assert.That(() => CrystalJson.Parse("True"), Throws.InstanceOf<JsonSyntaxException>(), "Parse('True')");
			Assert.That(() => CrystalJson.Parse("Frue"), Throws.InstanceOf<JsonSyntaxException>(), "Parse('Frue')");

			// On ne gère pas les variations
			Assert.That(() => CrystalJson.Parse("yes"), Throws.InstanceOf<JsonSyntaxException>(), "Parse('yes')");
			Assert.That(() => CrystalJson.Parse("off"), Throws.InstanceOf<JsonSyntaxException>(), "Parse('off')");
		}

		[Test]
		public void Test_Parse_String()
		{
			// Parsing

			Assert.That(CrystalJson.Parse(@""""""), Is.EqualTo(JsonString.Empty), "Parse('\"\"')");
			Assert.That(CrystalJson.Parse(@"""Hello World"""), Is.EqualTo(JsonString.Return("Hello World")), "Parse('\"Hello World\"')");
			Assert.That(CrystalJson.Parse(@"""0123456789ABCDE"""), Is.EqualTo(JsonString.Return("0123456789ABCDE")), "Parse('\"0123456789ABCDE\"')");
			Assert.That(CrystalJson.Parse(@"""0123456789ABCDEF"""), Is.EqualTo(JsonString.Return("0123456789ABCDEF")), "Parse('\"0123456789ABCDEF\"')");
			Assert.That(CrystalJson.Parse(@"""Chaine de texte très longue qui dépasse 16 caractères"""), Is.EqualTo(JsonString.Return("Chaine de texte très longue qui dépasse 16 caractères")), "Parse('\"Chaine de texte très longue qui dépasse 16 caractères\"')");
			Assert.That(CrystalJson.Parse(@"""Foo\""Bar"""), Is.EqualTo(JsonString.Return("Foo\"Bar")), "Parse('\"Foo\\\"Bar\"')");
			Assert.That(CrystalJson.Parse(@"""\"""""), Is.EqualTo(JsonString.Return("\"")), "Parse('\"\\\"\"')");
			Assert.That(CrystalJson.Parse(@"""\\"""), Is.EqualTo(JsonString.Return("\\")), "Parse('\"\\\\\"')");
			Assert.That(CrystalJson.Parse(@"""\/"""), Is.EqualTo(JsonString.Return("/")), "Parse('\"\\/\"')");
			Assert.That(CrystalJson.Parse(@"""\b"""), Is.EqualTo(JsonString.Return("\b")), "Parse('\"\\b\"')");
			Assert.That(CrystalJson.Parse(@"""\f"""), Is.EqualTo(JsonString.Return("\f")), "Parse('\"\\f\"')");
			Assert.That(CrystalJson.Parse(@"""\n"""), Is.EqualTo(JsonString.Return("\n")), "Parse('\"\\n\"')");
			Assert.That(CrystalJson.Parse(@"""\r"""), Is.EqualTo(JsonString.Return("\r")), "Parse('\"\\r\"')");
			Assert.That(CrystalJson.Parse(@"""\t"""), Is.EqualTo(JsonString.Return("\t")), "Parse('\"\\t\"')");

			// Errors
			Assert.That(() => CrystalJson.Parse("\"incomplete"), Throws.InstanceOf<JsonSyntaxException>(), "Incomplete string should fail");
			Assert.That(() => CrystalJson.Parse("invalid\""), Throws.InstanceOf<JsonSyntaxException>(), "Invalid string should fail");
			Assert.That(() => CrystalJson.Parse("\"\\z\""), Throws.InstanceOf<JsonSyntaxException>(), "Invalid \\z character should fail");
			Assert.That(() => CrystalJson.Parse("\"\\\""), Throws.InstanceOf<JsonSyntaxException>(), "Incomplete \\ character should fail");
		}

		[Test]
		public void Test_Parse_Number()
		{
			// Parsing

			// integers
			Assert.That(CrystalJson.Parse("0"), Is.EqualTo(JsonNumber.Return(0)), "Parse('0')");
			Assert.That(CrystalJson.Parse("1"), Is.EqualTo(JsonNumber.Return(1)), "Parse('1')");
			Assert.That(CrystalJson.Parse("123"), Is.EqualTo(JsonNumber.Return(123)), "Parse('123')");
			Assert.That(CrystalJson.Parse("-1"), Is.EqualTo(JsonNumber.Return(-1)), "Parse('-1')");
			Assert.That(CrystalJson.Parse("-123"), Is.EqualTo(JsonNumber.Return(-123)), "Parse('-123')");

			// decimals
			Assert.That(CrystalJson.Parse("0.1"), Is.EqualTo(JsonNumber.Return(0.1)), "Parse('0.1')");
			Assert.That(CrystalJson.Parse("1.23"), Is.EqualTo(JsonNumber.Return(1.23)), "Parse('1.23')");
			Assert.That(CrystalJson.Parse("-0.1"), Is.EqualTo(JsonNumber.Return(-0.1)), "Parse('-0.1')");
			Assert.That(CrystalJson.Parse("-1.23"), Is.EqualTo(JsonNumber.Return(-1.23)), "Parse('-1.23')");

			// decimals (but only integers)
			Assert.That(CrystalJson.Parse("0"), Is.EqualTo(JsonNumber.Return(0)), "Parse('0.0')");
			Assert.That(CrystalJson.Parse("1"), Is.EqualTo(JsonNumber.Return(1)), "Parse('1.0')");
			Assert.That(CrystalJson.Parse("123"), Is.EqualTo(JsonNumber.Return(123)), "Parse('123.0')");
			Assert.That(CrystalJson.Parse("-1"), Is.EqualTo(JsonNumber.Return(-1)), "Parse('-1.0')");
			Assert.That(CrystalJson.Parse("-123"), Is.EqualTo(JsonNumber.Return(-123)), "Parse('-123.0')");

			// avec exponent
			Assert.That(CrystalJson.Parse("1E1"), Is.EqualTo(JsonNumber.Return(10)), "Parse('1E1')");
			Assert.That(CrystalJson.Parse("1E2"), Is.EqualTo(JsonNumber.Return(100)), "Parse('1E2')");
			Assert.That(CrystalJson.Parse("1.23E2"), Is.EqualTo(JsonNumber.Return(123)), "Parse('1.23E2')");
			Assert.That(CrystalJson.Parse("1E+1"), Is.EqualTo(JsonNumber.Return(10)), "Parse('1E+1')");
			Assert.That(CrystalJson.Parse("1E-1"), Is.EqualTo(JsonNumber.Return(0.1)), "Parse('1E-1')");
			Assert.That(CrystalJson.Parse("1E-2"), Is.EqualTo(JsonNumber.Return(0.01)), "Parse('1E-2')");

			// négatif avec exponent
			Assert.That(CrystalJson.Parse("-1E1"), Is.EqualTo(JsonNumber.Return(-10)), "Parse('-1E1')");
			Assert.That(CrystalJson.Parse("-1E2"), Is.EqualTo(JsonNumber.Return(-100)), "Parse('-1E2')");
			Assert.That(CrystalJson.Parse("-1.23E2"), Is.EqualTo(JsonNumber.Return(-123)), "Parse('-1.23E2')");
			Assert.That(CrystalJson.Parse("-1E1"), Is.EqualTo(JsonNumber.Return(-10)), "Parse('-1E+1')");
			Assert.That(CrystalJson.Parse("-1E-1"), Is.EqualTo(JsonNumber.Return(-0.1)), "Parse('-1E-1')");
			Assert.That(CrystalJson.Parse("-1E-2"), Is.EqualTo(JsonNumber.Return(-0.01)), "Parse('-1E-2')");

			// Special
			Assert.That(CrystalJson.Parse("4.94065645841247E-324"), Is.EqualTo(JsonNumber.Return(double.Epsilon)), "Epsilon");
			Assert.That(CrystalJson.Parse("NaN"), Is.EqualTo(JsonNumber.Return(double.NaN)), "Parse('NaN')");
			Assert.That(CrystalJson.Parse("Infinity"), Is.EqualTo(JsonNumber.Return(double.PositiveInfinity)), "Parse('Infinity')");
			Assert.That(CrystalJson.Parse("+Infinity"), Is.EqualTo(JsonNumber.Return(double.PositiveInfinity)), "Parse('+Infinity')");
			Assert.That(CrystalJson.Parse("-Infinity"), Is.EqualTo(JsonNumber.Return(double.NegativeInfinity)), "Parse('-Infinity')");

			// Errors
			Assert.That(() => CrystalJson.Parse("1Z"), Throws.InstanceOf<JsonSyntaxException>(), "1Z");
			Assert.That(() => CrystalJson.Parse("1."), Throws.InstanceOf<JsonSyntaxException>(), "1.");
			Assert.That(() => CrystalJson.Parse("1-"), Throws.InstanceOf<JsonSyntaxException>(), "1-");
			Assert.That(() => CrystalJson.Parse("1+"), Throws.InstanceOf<JsonSyntaxException>(), "1+");
			Assert.That(() => CrystalJson.Parse("1E"), Throws.InstanceOf<JsonSyntaxException>(), "1E");
			Assert.That(() => CrystalJson.Parse("1E+"), Throws.InstanceOf<JsonSyntaxException>(), "1E+");
			Assert.That(() => CrystalJson.Parse("1E-"), Throws.InstanceOf<JsonSyntaxException>(), "1E-");
			Assert.That(() => CrystalJson.Parse("1.2.3"), Throws.InstanceOf<JsonSyntaxException>(), "Duplicate decimal point should fail");
			Assert.That(() => CrystalJson.Parse("1E1E1"), Throws.InstanceOf<JsonSyntaxException>(), "Duplicate exponent should fail");

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
				var num1 = (JsonNumber) CrystalJson.Parse("42");
				Assert.That(num1.ToInt32(), Is.EqualTo(42));
				Assert.That(num1.Literal, Is.EqualTo("42"));
				var num2 = (JsonNumber) CrystalJson.Parse("42");
				Assert.That(num2, Is.SameAs(num1), "Small positive interger 42 should be interened by default");
			}
			{ // 255 should also be cached
				var num1 = (JsonNumber) CrystalJson.Parse("255");
				Assert.That(num1.ToInt32(), Is.EqualTo(255));
				Assert.That(num1.Literal, Is.EqualTo("255"));
				var num2 = (JsonNumber) CrystalJson.Parse("255");
				Assert.That(num2, Is.SameAs(num1), "Positive interger 255 should be interened by default");
			}

			{ // -128 should also be cached
				var num1 = (JsonNumber) CrystalJson.Parse("-128");
				Assert.That(num1.ToInt32(), Is.EqualTo(-128));
				Assert.That(num1.Literal, Is.EqualTo("-128"));
				var num2 = (JsonNumber) CrystalJson.Parse("-128");
				Assert.That(num2, Is.SameAs(num1), "Negative interger -128 should be interened by default");
			}

			{ // large number should not be interned
				var num1 = (JsonNumber) CrystalJson.Parse("1000");
				Assert.That(num1.ToInt32(), Is.EqualTo(1000));
				Assert.That(num1.Literal, Is.EqualTo("1000"));
				var num2 = (JsonNumber) CrystalJson.Parse("1000");
				Assert.That(num2.ToInt32(), Is.EqualTo(1000));
				Assert.That(num2.Literal, Is.EqualTo("1000"));
				Assert.That(num2.Literal, Is.Not.SameAs(num1.Literal), "Large integers should not be interned by default");
			}

			{ // literal should be same as parsed
				var num1 = (JsonNumber) CrystalJson.Parse("1E3");
				Assert.That(num1.ToInt32(), Is.EqualTo(1000));
				Assert.That(num1.Literal, Is.EqualTo("1E3"));
				Assert.That(num1.IsDecimal, Is.False);
				var num2 = (JsonNumber) CrystalJson.Parse("10E2");
				Assert.That(num2.ToInt32(), Is.EqualTo(1000));
				Assert.That(num2.Literal, Is.EqualTo("10E2"));
				Assert.That(num1.IsDecimal, Is.False);
			}

			{ // false decimal
				var num1 = (JsonNumber) CrystalJson.Parse("0.1234E4");
				Assert.That(num1.ToDouble(), Is.EqualTo(1234.0));
				Assert.That(num1.Literal, Is.EqualTo("0.1234E4"));
				Assert.That(num1.IsDecimal, Is.False);
				var num2 = JsonNumber.Return(0.1234E4);
				Assert.That(num2.ToDouble(), Is.EqualTo(1234.0));
				Assert.That(num2.Literal, Is.EqualTo("1234")); //BUGBUG: devrait être "1234.0" ?
				//Assert.That(num2.IsDecimal, Is.False); //REVIEW: vu qu'on a appelé Return(double), le json est actuellement considéré comme décimal..
			}

			{ // real decimal
				var num1 = (JsonNumber) CrystalJson.Parse("0.1234E3");
				Assert.That(num1.ToDouble(), Is.EqualTo(123.4));
				Assert.That(num1.Literal, Is.EqualTo("0.1234E3"));
				Assert.That(num1.IsDecimal, Is.True);
				var num2 = JsonNumber.Return(0.1234E3);
				Assert.That(num2.ToDouble(), Is.EqualTo(123.4));
				Assert.That(num2.Literal, Is.EqualTo("123.4"));
				Assert.That(num2.IsDecimal, Is.True);
			}

			{ // very long integers should bypass the custom parsing
				var num1 = (JsonNumber) CrystalJson.Parse("18446744073709551615"); // ulong.MaxValue
				Assert.That(num1.ToUInt64(), Is.EqualTo(ulong.MaxValue));
				Assert.That(num1.Literal, Is.EqualTo("18446744073709551615"));
				Assert.That(num1.IsDecimal, Is.False);
			}

		}

		[Test]
		public void Test_Parse_Comment()
		{
			var obj = CrystalJson.ParseObject("{ // hello world\r\n}")!;
			Log(obj);
			Assert.That(obj, Is.Not.Null.And.InstanceOf<JsonObject>());
			Assert.That(obj.Count, Is.EqualTo(0));

			obj = CrystalJson.ParseObject(
				"""
				{
					// comment 1
					"foo": 123,
					//"bar": 456
					// comment2
				}
				""")!;
			Log(obj);
			Assert.That(obj, Is.Not.Null.And.InstanceOf<JsonObject>());
			Assert.That(obj["foo"], Is.EqualTo((JsonValue)123));
			Assert.That(obj["bar"], Is.EqualTo(JsonNull.Missing));
			Assert.That(obj.Count, Is.EqualTo(1));
		}

#if DISABLED
		[Test]
		public void TestParseDateTime()
		{
			// Parsing

			// Unix Epoch (1970-1-1 UTC)
			AssertJson.ParseAreEqual(new JsonDateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc), "\"\\/Date(0)\\/\"");

			// Min/Max Value
			AssertJson.ParseAreEqual(JsonDateTime.MinValue, "\"\\/Date(-62135596800000)\\/\"", "DateTime.MinValue");
			AssertJson.ParseAreEqual(JsonDateTime.MaxValue, "\"\\/Date(253402300799999)\\/\"", "DateTime.MaxValue (auto-ajusted)"); // note: doit ajouter automatiquement les .99999 millisecondes manquantes !

			// 2000-01-01 (heure d'hivers)
			AssertJson.ParseAreEqual(new JsonDateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc), "\"\\/Date(946684800000)\\/\"", "2000-01-01 UTC");
			AssertJson.ParseAreEqual(new JsonDateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Local), "\"\\/Date(946681200000+0100)\\/\"", "2000-01-01 GMT+1 (Paris)");

			// 2000-09-01 (heure d'été)
			AssertJson.ParseAreEqual(new JsonDateTime(2000, 9, 1, 0, 0, 0, DateTimeKind.Utc), "\"\\/Date(967766400000)\\/\"", "2000-09-01 UTC");
			AssertJson.ParseAreEqual(new JsonDateTime(2000, 9, 1, 0, 0, 0, DateTimeKind.Local), "\"\\/Date(967759200000+0200)\\/\"", "2000-09-01 GMT+2 (Paris, DST)");

			// RoundTrip !
			DateTime utcNow = DateTime.UtcNow;
			Assert.That(utcNow.Kind, Is.EqualTo(DateTimeKind.Utc));
			// /!\ JSON a une résolution a la milliseconde mais UtcNow a une précision au 'tick', donc il faut tronquer la date car elle a une précision supérieure
			var utcRoundTrip = CrystalJson.Parse(CrystalJson.Serialize(utcNow));
			Assert.That(utcRoundTrip, Is.EqualTo(new JsonDateTime(utcNow)), "RoundTrip DateTime.UtcNow");

			DateTime localNow = DateTime.Now;
			Assert.That(localNow.Kind, Is.EqualTo(DateTimeKind.Local));
			var localRoundTrip = CrystalJson.Parse(CrystalJson.Serialize(localNow));
			Assert.That(localRoundTrip, Is.EqualTo(new JsonDateTime(localNow)), "RoundTrip DateTime.Now");
		}
#endif

		[Test]
		public void Test_Parse_Array()
		{
			// Parsing

			// empty
			string jsonText = "[]";
			var obj = CrystalJson.Parse(jsonText);
			Assert.That(obj, Is.Not.Null, jsonText);
			Assert.That(obj, Is.InstanceOf<JsonArray>(), jsonText);
			var res = obj as JsonArray;
			Assert.That(res.Count, Is.EqualTo(0), jsonText + ".Count");

			jsonText = "[ ]";
			obj = CrystalJson.Parse(jsonText);
			Assert.That(obj, Is.InstanceOf<JsonArray>(), jsonText);
			res = obj as JsonArray;
			Assert.That(res.Count, Is.EqualTo(0), jsonText + ".Count");

			// single value
			AssertJson.ParseAreEqual(JsonArray.Create(1), "[1]");
			AssertJson.ParseAreEqual(JsonArray.Create(1), "[ 1 ]");

			// multiple value
			AssertJson.ParseAreEqual(JsonArray.Create(1, 2, 3), "[1,2,3]");
			AssertJson.ParseAreEqual(JsonArray.Create(1, 2, 3), "[ 1, 2, 3 ]");

			// strings
			AssertJson.ParseAreEqual(JsonArray.Create("foo", "bar"), @"[""foo"",""bar""]");

			// mixed array
			AssertJson.ParseAreEqual(JsonArray.Create(123, true, "foo"), @"[123,true,""foo""]");

			// jagged arrays
			AssertJson.ParseAreEqual(new JsonArray {
				JsonArray.Create(1, 2, 3),
				JsonArray.Create(true, false),
				JsonArray.Create("foo", "bar")
			}, @"[ [1,2,3], [true,false], [""foo"",""bar""] ]");

			// incomplete
			Assert.That(() => CrystalJson.Parse("[1,2,3"), Throws.InstanceOf<JsonSyntaxException>(), "Incomplete Array should fail");
			Assert.That(() => CrystalJson.Parse("[1,,3]"), Throws.InstanceOf<JsonSyntaxException>(), "Array with missing item should fail");
			Assert.That(() => CrystalJson.Parse("[,]"), Throws.InstanceOf<JsonSyntaxException>(), "Array with empty items should fail");
			Assert.That(() => CrystalJson.Parse("[1,[A,B,C]"), Throws.InstanceOf<JsonSyntaxException>(), "Incomplete inner Array should fail");

			// trailing commas
			Assert.That(() => CrystalJson.Parse("[ 1, 2, 3, ]"), Throws.Nothing, "By default, trailing commas are allowed");
			Assert.That(() => CrystalJson.Parse("[ 1, 2, 3, ]", CrystalJsonSettings.Json), Throws.Nothing, "By default, trailing commas are allowed");
			Assert.That(() => CrystalJson.Parse("[ 1, 2, 3, ]", CrystalJsonSettings.JsonStrict), Throws.InstanceOf<JsonSyntaxException>(), "Should fail is trailing commas are forbidden");
			Assert.That(() => CrystalJson.Parse("[ 1, 2, 3, ]", CrystalJsonSettings.Json.WithoutTrailingCommas()), Throws.InstanceOf<JsonSyntaxException>(), "Should fail when trailing commas are explicitly forbidden");
			Assert.That(CrystalJson.ParseArray("[ 1, 2, 3, ]")?.Count, Is.EqualTo(3), "Ignored trailing commas should not add any extra item to the array");
			Assert.That(CrystalJson.ParseArray("[ 1, 2, 3, ]", CrystalJsonSettings.Json)?.Count, Is.EqualTo(3), "Ignored trailing commas should not add any extra item to the array");

			// interning corner cases

			{ // array of small integers (-128..255) should all be refs to cached instances
				var arr = CrystalJson.ParseArray("[ 0, 1, 42, -1, 255, -128 ]");
				Assert.That(arr, Is.Not.Null.And.Count.EqualTo(6));
				Assert.That(arr[0], Is.SameAs(JsonNumber.Zero));
				Assert.That(arr[1], Is.SameAs(JsonNumber.One));
				Assert.That(arr[2], Is.SameAs(JsonNumber.Return(42)));
				Assert.That(arr[3], Is.SameAs(JsonNumber.MinusOne));
				Assert.That(arr[4], Is.SameAs(JsonNumber.Return(255)));
				Assert.That(arr[5], Is.SameAs(JsonNumber.Return(-128)));
			}
		}

		[Test]
		public void Test_Parse_SimpleObject()
		{
			string jsonText = "{}";
			var obj = CrystalJson.Parse(jsonText);
			Assert.That(obj, Is.Not.Null, jsonText);
			Assert.That(obj, Is.InstanceOf<JsonObject>(), jsonText);
			var parsed = obj as JsonObject;
			Assert.That(parsed.Count, Is.EqualTo(0), jsonText + ".Count");

			jsonText = "{ }";
			parsed = CrystalJson.ParseObject(jsonText);
			Assert.That(parsed, Is.Not.Null, jsonText);
			Assert.That(parsed.Count, Is.EqualTo(0), jsonText + ".Count");
			Assert.That(parsed, Is.EqualTo(JsonObject.Empty), jsonText);

			jsonText = @"{ ""Name"":""James Bond"" }";
			obj = CrystalJson.Parse(jsonText);
			Assert.That(obj, Is.InstanceOf<JsonObject>(), jsonText);
			parsed = obj as JsonObject;
			Assert.That(parsed, Is.Not.Null, jsonText);
			Assert.That(parsed.Count, Is.EqualTo(1), jsonText + ".Count");
			Assert.That(parsed.ContainsKey("Name"), Is.True, jsonText + ".Name?");
			Assert.That(parsed["Name"], Is.EqualTo(JsonString.Return("James Bond")), jsonText + ".Name");

			jsonText = @"{ ""Id"":7, ""Name"":""James Bond"", ""IsDeadly"":true }";
			parsed = CrystalJson.ParseObject(jsonText);
			Assert.That(parsed.Count, Is.EqualTo(3), jsonText + ".Count");
			Assert.That(parsed["Name"], Is.EqualTo(JsonString.Return("James Bond")), jsonText + ".Name");
			Assert.That(parsed["Id"], Is.EqualTo(JsonNumber.Return(7)), jsonText + ".Id");
			Assert.That(parsed["IsDeadly"], Is.EqualTo(JsonBoolean.True), jsonText + ".IsDeadly");

			jsonText = @"{ ""Id"":7, ""Name"":""James Bond"", ""IsDeadly"":true, ""Created"":""\/Date(-52106400000+0200)\/"", ""Weapons"":[{""Name"":""Walter PPK""}] }";
			parsed = CrystalJson.ParseObject(jsonText);
			Assert.That(parsed.Count, Is.EqualTo(5), jsonText + ".Count");
			Assert.That(parsed["Name"], Is.EqualTo(JsonString.Return("James Bond")), jsonText + ".Name");
			Assert.That(parsed["Id"], Is.EqualTo(JsonNumber.Return(7)), jsonText + ".Id");
			Assert.That(parsed["IsDeadly"], Is.EqualTo(JsonBoolean.True), jsonText + ".IsDeadly");
			//Assert.That(parsed["Created"], Is.EqualTo(new JsonDateTime(1968, 5, 8)), jsonText + ".Created"));
			Assert.That(parsed["Created"], Is.EqualTo(JsonString.Return("/Date(-52106400000+0200)/")), jsonText + ".Created"); //BUGBUG
			Assert.That(parsed.ContainsKey("Weapons"), Is.True, jsonText + ".Weapons");
			var weapons = parsed.GetArray("Weapons");
			Assert.That(weapons, Is.Not.Null, jsonText + ".Weapons");
			Assert.That(weapons.Count, Is.EqualTo(1), jsonText + ".Weapons.Count");
			var weapon = (JsonObject)weapons[0];
			Assert.That(weapon, Is.Not.Null, jsonText + ".Weapons[0]");
			Assert.That(weapon["Name"], Is.EqualTo(JsonString.Return("Walter PPK")), jsonText + ".Weapons[0].Name");

			// incomplete
			Assert.That(() => CrystalJson.Parse(@"{""foo""}"), Throws.InstanceOf<JsonSyntaxException>(), "Missing property separator");
			Assert.That(() => CrystalJson.Parse(@"{""foo"":}"), Throws.InstanceOf<JsonSyntaxException>(), "Missing property value");
			Assert.That(() => CrystalJson.Parse(@"{""foo"":123"), Throws.InstanceOf<JsonSyntaxException>(), "Missing '}'");
			Assert.That(() => CrystalJson.Parse(@"{""foo"":{}"), Throws.InstanceOf<JsonSyntaxException>(), "Missing outer '}'");
			Assert.That(() => CrystalJson.Parse("{,}"), Throws.InstanceOf<JsonSyntaxException>(), "Object with empty properties should fail");

			// trailing commas
			jsonText = @"{ ""Foo"": 123, ""Bar"": 456, }";
			Assert.That(() => CrystalJson.Parse(jsonText), Throws.Nothing, "By default, trailing commas are allowed");
			Assert.That(() => CrystalJson.Parse(jsonText, CrystalJsonSettings.Json), Throws.Nothing, "By default, trailing commas are allowed");
			Assert.That(() => CrystalJson.Parse(jsonText, CrystalJsonSettings.JsonStrict), Throws.InstanceOf<JsonSyntaxException>(), "Strict mode does not allow trailing commas");
			Assert.That(() => CrystalJson.Parse(jsonText, CrystalJsonSettings.Json.WithoutTrailingCommas()), Throws.InstanceOf<JsonSyntaxException>(), "Should fail when commas are explicitly forbidden");
			Assert.That(CrystalJson.ParseObject(jsonText)?.Count, Is.EqualTo(2), "Ignored trailing commas should not add any extra item to the object");
			Assert.That(CrystalJson.ParseObject(jsonText, CrystalJsonSettings.Json)?.Count, Is.EqualTo(2), "Ignored trailing commas should not add any extra item to the object");

			// interning corner cases

			{ // values that are small integers (-128..255à should all be refs to cached instances
				obj = CrystalJson.ParseObject(@"{ ""A"": 0, ""B"": 1, ""C"": 42, ""D"": -1, ""E"": 255, ""F"": -128 }");
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
			// Par défaut, l'interning de string n'est activé que sur les noms de propriétés des objets, cad que plusieurs occurences du même texte partageront la même string en mémoire
			// Ce mode doit pouvoir être configuré dans les settings

			// note: il est aussi important que les clés ne soit pas internée via String.Intern() sous peine de plomber complètement la heap !

			const string TEXT = @"[ { ""Foo"":""Bar"" }, { ""Foo"":""Bar"" } ]";

			// par défaut seul les clés sont internées, mais pas les valeurs
			var array = CrystalJson.ParseArray(TEXT, CrystalJsonSettings.Json.WithInterning(CrystalJsonSettings.StringInterning.Default)).Select(x => ((JsonObject)x).First()).ToArray();
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

			// si on désactive l'interning, ni les clés ni les valeurs ne doivent être partagées

			array = CrystalJson.ParseArray(TEXT, CrystalJsonSettings.Json.DisableInterning()).Select(x => ((JsonObject)x).First()).ToArray();
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

			// si on active l'interning complet, les clés et les valeurs doivent être partagées

			array = CrystalJson.ParseArray(TEXT, CrystalJsonSettings.Json.WithInterning(CrystalJsonSettings.StringInterning.IncludeValues)).Select(x => ((JsonObject)x).First()).ToArray();
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
			Slice bytes = CrystalJson.ToBuffer(obj);
			Log(bytes.ToString("P"));

			var json = CrystalJson.ParseObject(bytes);
			Assert.That(json, Is.Not.Null);
			Assert.That(json.Get<string>("Foo"), Is.EqualTo("Héllö"));
			Assert.That(json.Get<string>("Bar"), Is.EqualTo("世界!"));
			Assert.That(json.Get<string>("ಠ_ಠ"), Is.EqualTo("(╯°□°）╯︵ ┻━┻"));
			Assert.That(json.Count, Is.EqualTo(3));
			_ = CrystalJson.ParseObject(bytes);
		}

		[Test]
		public void Test_Duplicate_Object_Fields()
		{
			// par défaut, si un object contient plusieurs fois le même champs, on throw

			Assert.That(
				() => CrystalJson.ParseObject(@"{ ""Foo"": ""1"", ""Bar"": ""Baz"", ""Foo"": ""2"" }"),
				Throws.InstanceOf<JsonSyntaxException>(),
				"JSON Object with duplicate fields should throw by default");

			// mais on peut l'autoriser via les settings, pour ne garder que le dernier
			Assert.That(
				() => CrystalJson.ParseObject(@"{ ""Foo"": ""1"", ""Bar"": ""Baz"", ""Foo"": ""2"" }", CrystalJsonSettings.Json.FlattenDuplicateFields()),
				Throws.Nothing,
				"JSON Object with duplicate fields should not throw is 'FlattenDuplicateFields' option is set"
			);
			var obj = CrystalJson.ParseObject(@"{ ""Foo"": ""1"", ""Bar"": ""Baz"", ""Foo"": ""2"" }", CrystalJsonSettings.Json.FlattenDuplicateFields());
			Assert.That(obj.Get<string>("Foo"), Is.EqualTo("2"), "Duplicate fields should keep the last occurrence");
			Assert.That(obj.Get<string>("Bar"), Is.EqualTo("Baz"));
			Assert.That(obj.Count, Is.EqualTo(2));
		}

		#endregion

		#region Deserialization...

		[Test]
		public void Test_JsonDeserialize_Null()
		{
			Assert.That(CrystalJson.DeserializeBoxed("null"), Is.Null, "Deserialize('null')");
			Assert.That(CrystalJson.DeserializeBoxed(String.Empty), Is.Null, "Deserialize('')");
		}

		[Test]
		public void Test_JsonDeserialize_Boolean()
		{
			// generic

			Assert.That(CrystalJson.DeserializeBoxed("true"), Is.True, "Deseralize('true')");
			Assert.That(CrystalJson.DeserializeBoxed("false"), Is.False, "Deseralize('false')");

			// direct

			Assert.That(CrystalJson.Deserialize<bool>("true"), Is.True, "Deseralize<bool>('true')");
			Assert.That(CrystalJson.Deserialize<bool>("false"), Is.False, "Deseralize<bool>('false')");

			// implicit convert
			Assert.That(CrystalJson.Deserialize<bool>("0"), Is.False, "Deseralize<bool>('0')");
			Assert.That(CrystalJson.Deserialize<bool>("1"), Is.True, "Deseralize<bool>('1')");
			Assert.That(CrystalJson.Deserialize<bool>("123"), Is.True, "Deseralize<bool>('123')");
			Assert.That(CrystalJson.Deserialize<bool>("-1"), Is.True, "Deseralize<bool>('-1')");

			Assert.That(CrystalJson.Deserialize<bool>("\"true\""), Is.True, "Deseralize<bool>('\"true\"')");
			Assert.That(CrystalJson.Deserialize<bool>("\"false\""), Is.False, "Deseralize<bool>('\"false\"')");

			// bool n'est pas nullable, mais on accepte quand même null
			Assert.That(CrystalJson.Deserialize<bool>("null"), Is.False, "Deserialize<bool>('null')");

			// doit rejeter les autres types
			Assert.Throws<FormatException>(() => { CrystalJson.Deserialize<bool>("\"foo\""); }, "Deserialize<bool>('\"foo\"')");
			Assert.Throws<JsonBindingException>(() => { CrystalJson.Deserialize<bool>("{ }"); }, "Deserialize<bool>('{ }')");
			Assert.Throws<JsonBindingException>(() => { CrystalJson.Deserialize<bool>("[ ]"); }, "Deserialize<bool>('[ ]')");
		}

		[Test]
		public void Test_JsonDeserialize_String()
		{
			// generic deserialization

			Assert.That(CrystalJson.DeserializeBoxed(@""""""), Is.EqualTo(String.Empty), "Deseralize('\"\"')");
			Assert.That(CrystalJson.DeserializeBoxed(@"""Hello World"""), Is.EqualTo("Hello World"), "Deseralize('\"Hello World\"')");
			Assert.That(CrystalJson.DeserializeBoxed(@"""Foo\""Bar"""), Is.EqualTo("Foo\"Bar"), "Deseralize('\"Foo\\\"Bar\"')");
			Assert.That(CrystalJson.DeserializeBoxed(@"""\"""""), Is.EqualTo("\""), "Deseralize('\"\\\"\"')");
			Assert.That(CrystalJson.DeserializeBoxed(@"""\\"""), Is.EqualTo("\\"), "Deseralize('\"\\\\\"')");
			Assert.That(CrystalJson.DeserializeBoxed(@"""\/"""), Is.EqualTo("/"), "Deseralize('\"\\/\"')");
			Assert.That(CrystalJson.DeserializeBoxed(@"""\b"""), Is.EqualTo("\b"), "Deseralize('\"\\b\"')");
			Assert.That(CrystalJson.DeserializeBoxed(@"""\f"""), Is.EqualTo("\f"), "Deseralize('\"\\f\"')");
			Assert.That(CrystalJson.DeserializeBoxed(@"""\n"""), Is.EqualTo("\n"), "Deseralize('\"\\n\"')");
			Assert.That(CrystalJson.DeserializeBoxed(@"""\r"""), Is.EqualTo("\r"), "Deseralize('\"\\r\"')");
			Assert.That(CrystalJson.DeserializeBoxed(@"""\t"""), Is.EqualTo("\t"), "Deseralize('\"\\t\"')");

			// directed deserialization

			Assert.That(CrystalJson.Deserialize<string>("null"), Is.EqualTo(null), "Deseralize<string>('null')");
			Assert.That(CrystalJson.Deserialize<string>(@"""Hello World"""), Is.EqualTo("Hello World"), "Deseralize<string>('\"Hello World\"')");

			// with implicit conversion
			Assert.That(CrystalJson.Deserialize<string>("123"), Is.EqualTo("123"), "Deseralize<string>('123') (number!)");
			Assert.That(CrystalJson.Deserialize<string>("1.23"), Is.EqualTo("1.23"), "Deseralize<string>('1.23') (number!)");
			Assert.That(CrystalJson.Deserialize<string>("true"), Is.EqualTo("true"), "Deseralize<string>('true') (boolean!)");

			// doit rejeter les autres types
			Assert.Throws<JsonBindingException>(() => { CrystalJson.Deserialize<string>("{ }"); }, "Deserialize<string>('{ }')");
			Assert.Throws<JsonBindingException>(() => { CrystalJson.Deserialize<string>("[ ]"); }, "Deserialize<string>('[ ]')");

			// un tableau de string n'est PAS une string !
			Assert.Throws<JsonBindingException>(() => { CrystalJson.Deserialize<string>("[ \"foo\" ]"); }, "Deserialize<string>('[ \"foo\" ]')");

		}

		[Test]
		public void Test_JsonDeserialize_Number()
		{
			// integers
			Assert.That(CrystalJson.DeserializeBoxed("0"), Is.EqualTo(0), "Deserialize('0')");
			Assert.That(CrystalJson.DeserializeBoxed("1"), Is.EqualTo(1), "Deserialize('1')");
			Assert.That(CrystalJson.DeserializeBoxed("123"), Is.EqualTo(123), "Deserialize('123')");
			Assert.That(CrystalJson.DeserializeBoxed("-1"), Is.EqualTo(-1), "Deserialize('-1')");
			Assert.That(CrystalJson.DeserializeBoxed("-123"), Is.EqualTo(-123), "Deserialize('-123')");

			// decimals
			Assert.That(CrystalJson.DeserializeBoxed("0.1"), Is.EqualTo(0.1), "Deserialize('0.1')");
			Assert.That(CrystalJson.DeserializeBoxed("1.23"), Is.EqualTo(1.23), "Deserialize('1.23')");
			Assert.That(CrystalJson.DeserializeBoxed("-0.1"), Is.EqualTo(-0.1), "Deserialize('-0.1')");
			Assert.That(CrystalJson.DeserializeBoxed("-1.23"), Is.EqualTo(-1.23), "Deserialize('-1.23')");

			// decimals (but only integers)
			Assert.That(CrystalJson.DeserializeBoxed("0"), Is.EqualTo(0), "Deserialize('0.0')");
			Assert.That(CrystalJson.DeserializeBoxed("1"), Is.EqualTo(1), "Deserialize('1.0')");
			Assert.That(CrystalJson.DeserializeBoxed("123"), Is.EqualTo(123), "Deserialize('123.0')");
			Assert.That(CrystalJson.DeserializeBoxed("-1"), Is.EqualTo(-1), "Deserialize('-1.0')");
			Assert.That(CrystalJson.DeserializeBoxed("-123"), Is.EqualTo(-123), "Deserialize('-123.0')");

			// avec exponent
			Assert.That(CrystalJson.DeserializeBoxed("1E1"), Is.EqualTo(10), "Deserialize('1E1')");
			Assert.That(CrystalJson.DeserializeBoxed("1E2"), Is.EqualTo(100), "Deserialize('1E2')");
			Assert.That(CrystalJson.DeserializeBoxed("1.23E2"), Is.EqualTo(123), "Deserialize('1.23E2')");
			Assert.That(CrystalJson.DeserializeBoxed("1E1"), Is.EqualTo(10), "Deserialize('1E+1')");
			Assert.That(CrystalJson.DeserializeBoxed("1E-1"), Is.EqualTo(0.1), "Deserialize('1E-1')");
			Assert.That(CrystalJson.DeserializeBoxed("1E-2"), Is.EqualTo(0.01), "Deserialize('1E-2')");

			// special
			Assert.That(CrystalJson.DeserializeBoxed("NaN"), Is.EqualTo(double.NaN), "Deserialize('NaN')");
			Assert.That(CrystalJson.DeserializeBoxed("Infinity"), Is.EqualTo(double.PositiveInfinity), "Deserialize('Infinity')");
			Assert.That(CrystalJson.DeserializeBoxed("-Infinity"), Is.EqualTo(double.NegativeInfinity), "Deserialize('-Infinity')");
			Assert.That(CrystalJson.DeserializeBoxed("\"NaN\""), Is.EqualTo("NaN"), "Deserialize('\"NaN\"') ne doit pas être reconnu automatiquement comme un nombre car on n'a pas précisé de type!");

			// directed deserialization
			Assert.That(CrystalJson.Deserialize<decimal>("123"), Is.EqualTo(123), "Deserialize<decimal>('123')");
			Assert.That(CrystalJson.Deserialize<int>("123"), Is.EqualTo(123), "Deserialize<int>('123')");
			Assert.That(CrystalJson.Deserialize<long>("123"), Is.EqualTo(123L), "Deserialize<long>('123')");
			Assert.That(CrystalJson.Deserialize<float>("1.23"), Is.EqualTo(1.23f), "Deserialize<float>('123')");
			Assert.That(CrystalJson.Deserialize<double>("1.23"), Is.EqualTo(1.23d), "Deserialize<double>('123')");
			Assert.That(CrystalJson.Deserialize<float>("NaN"), Is.EqualTo(float.NaN), "Deserialize<float>('NaN')");
			Assert.That(CrystalJson.Deserialize<double>("NaN"), Is.EqualTo(double.NaN), "Deserialize<double>('NaN')");
			Assert.That(CrystalJson.Deserialize<float>("Infinity"), Is.EqualTo(float.PositiveInfinity), "Deserialize<float>('Infinity')");
			Assert.That(CrystalJson.Deserialize<double>("Infinity"), Is.EqualTo(double.PositiveInfinity), "Deserialize<double>('Infinity')");
			Assert.That(CrystalJson.Deserialize<float>("-Infinity"), Is.EqualTo(float.NegativeInfinity), "Deserialize<float>('-Infinity')");
			Assert.That(CrystalJson.Deserialize<double>("-Infinity"), Is.EqualTo(double.NegativeInfinity), "Deserialize<double>('-Infinity')");

			// implicit conversion
			Assert.That(CrystalJson.Deserialize<decimal>("\"123\""), Is.EqualTo(123), "Deserialize<decimal>('\"123\"')");
			Assert.That(CrystalJson.Deserialize<int>("\"123\""), Is.EqualTo(123), "Deserialize<int>('\"123\"')");
			Assert.That(CrystalJson.Deserialize<long>("\"123\""), Is.EqualTo(123L), "Deserialize<long>('\"123\"')");
			Assert.That(CrystalJson.Deserialize<float>("\"1.23\""), Is.EqualTo(1.23f), "Deserialize<float>('\"1.23\"')");
			Assert.That(CrystalJson.Deserialize<double>("\"1.23\""), Is.EqualTo(1.23d), "Deserialize<double>('\"1.23\"')");
			Assert.That(CrystalJson.Deserialize<float>("\"NaN\""), Is.EqualTo(float.NaN), "Deserialize<float>('\"NaN\"')");
			Assert.That(CrystalJson.Deserialize<double>("\"NaN\""), Is.EqualTo(double.NaN), "Deserialize<double>('\"NaN\"')");
			Assert.That(CrystalJson.Deserialize<float>("\"Infinity\""), Is.EqualTo(float.PositiveInfinity), "Deserialize<float>('\"Infinity\"')");
			Assert.That(CrystalJson.Deserialize<double>("\"Infinity\""), Is.EqualTo(double.PositiveInfinity), "Deserialize<double>('\"Infinity\"')");
			Assert.That(CrystalJson.Deserialize<float>("\"-Infinity\""), Is.EqualTo(float.NegativeInfinity), "Deserialize<float>('\"-Infinity\"')");
			Assert.That(CrystalJson.Deserialize<double>("\"-Infinity\""), Is.EqualTo(double.NegativeInfinity), "Deserialize<double>('\"-Infinity\"')");

			// doit rejeter les autres types
			Assert.Throws<JsonBindingException>(() => { CrystalJson.Deserialize<int>("{ }"); }, "Deserialize<int>('{ }')");
			Assert.Throws<JsonBindingException>(() => { CrystalJson.Deserialize<int>("[ ]"); }, "Deserialize<int>('[ ]')");

		}

		[Test]
		public void Test_JsonDeserialize_DateTime()
		{
			// Unix Epoch (1970-1-1 UTC)
			Assert.That(CrystalJson.Deserialize<DateTime>("\"\\/Date(0)\\/\""), Is.EqualTo(new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)));

			// Min/Max Value
			Assert.That(CrystalJson.Deserialize<DateTime>("\"\""), Is.EqualTo(DateTime.MinValue), "DateTime.MinValue");
			Assert.That(CrystalJson.Deserialize<DateTime>("\"0001-01-01T00:00:00.0000000\""), Is.EqualTo(DateTime.MinValue), "DateTime.MinValue");
			Assert.That(CrystalJson.Deserialize<DateTime>("\"0001-01-01T00:00:00\""), Is.EqualTo(DateTime.MinValue), "DateTime.MinValue");
			Assert.That(CrystalJson.Deserialize<DateTime>("\"\\/Date(-62135596800000)\\/\""), Is.EqualTo(DateTime.MinValue), "DateTime.MinValue");
			Assert.That(CrystalJson.Deserialize<DateTime>("\"0001-01-01T00:00:00.0000000Z\""), Is.EqualTo(DateTime.MinValue), "DateTime.MinValue");
			Assert.That(CrystalJson.Deserialize<DateTime>("\"0001-01-01T00:00:00Z\""), Is.EqualTo(DateTime.MinValue), "DateTime.MinValue");

			Assert.That(CrystalJson.Deserialize<DateTime>("\"9999-12-31T23:59:59.9999999\""), Is.EqualTo(DateTime.MaxValue), "DateTime.MaxValue");
			Assert.That(CrystalJson.Deserialize<DateTime>("\"9999-12-31T23:59:59.9999999Z\""), Is.EqualTo(DateTime.MaxValue), "DateTime.MaxValue");
			Assert.That(CrystalJson.Deserialize<DateTime>("\"\\/Date(253402300799999)\\/\""), Is.EqualTo(DateTime.MaxValue), "DateTime.MaxValue (auto-ajusted)"); // note: doit ajouter automatiquement les .99999 millisecondes manquantes !

			// 2000-01-01 (heure d'hivers)
			Assert.That(CrystalJson.Deserialize<DateTime>("\"2000-01-01T00:00:00.0000000Z\""), Is.EqualTo(new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc)), "2000-01-01 UTC");
			Assert.That(CrystalJson.Deserialize<DateTime>("\"2000-01-01T00:00:00Z\""), Is.EqualTo(new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc)), "2000-01-01 UTC");
			Assert.That(CrystalJson.Deserialize<DateTime>("\"\\/Date(946684800000)\\/\""), Is.EqualTo(new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc)), "2000-01-01 UTC");
			Assert.That(CrystalJson.Deserialize<DateTime>("\"2000-01-01T00:00:00.0000000\""), Is.EqualTo(new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Local)), "2000-01-01 GMT+1 (Paris)");
			Assert.That(CrystalJson.Deserialize<DateTime>("\"2000-01-01T00:00:00\""), Is.EqualTo(new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Local)), "2000-01-01 GMT+1 (Paris)");
			Assert.That(CrystalJson.Deserialize<DateTime>("\"2000-01-01T00:00:00.0000000+01:00\""), Is.EqualTo(new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Local)), "2000-01-01 GMT+1 (Paris)");
			Assert.That(CrystalJson.Deserialize<DateTime>("\"2000-01-01T00:00:00+01:00\""), Is.EqualTo(new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Local)), "2000-01-01 GMT+1 (Paris)");
			Assert.That(CrystalJson.Deserialize<DateTime>("\"\\/Date(946681200000+0100)\\/\""), Is.EqualTo(new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Local)), "2000-01-01 GMT+1 (Paris)");
			Assert.That(CrystalJson.Deserialize<DateTime>("\"2000-01-01T00:00:00.0000000-01:00\""), Is.EqualTo(new DateTime(2000, 1, 1, 2, 0, 0, DateTimeKind.Local)), "2000-01-01 GMT-1");
			Assert.That(CrystalJson.Deserialize<DateTime>("\"2000-01-01T00:00:00-01:00\""), Is.EqualTo(new DateTime(2000, 1, 1, 2, 0, 0, DateTimeKind.Local)), "2000-01-01 GMT-1");
			Assert.That(CrystalJson.Deserialize<DateTime>("\"\\/Date(946681200000-0100)\\/\""), Is.EqualTo(new DateTime(2000, 1, 1, 2, 0, 0, DateTimeKind.Local)), "2000-01-01 GMT-1");

			// 2000-09-01 (heure d'été)
			Assert.That(CrystalJson.Deserialize<DateTime>("\"\\/Date(967766400000)\\/\""), Is.EqualTo(new DateTime(2000, 9, 1, 0, 0, 0, DateTimeKind.Utc)), "2000-09-01 UTC");
			Assert.That(CrystalJson.Deserialize<DateTime>("\"\\/Date(967759200000+0200)\\/\""), Is.EqualTo(new DateTime(2000, 9, 1, 0, 0, 0, DateTimeKind.Local)), "2000-09-01 GMT+2 (Paris, DST)");

			// RoundTrip !
			DateTime utcNow = DateTime.UtcNow;
			Assert.That(utcNow.Kind, Is.EqualTo(DateTimeKind.Utc));
			// /!\ JSON a une résolution a la milliseconde mais UtcNow a une précision au 'tick', donc il faut tronquer la date car elle a une précision supérieure
			var utcRoundTrip = CrystalJson.Deserialize<DateTime>(CrystalJson.Serialize(utcNow));
			Assert.That(utcRoundTrip, Is.EqualTo(utcNow), "RoundTrip DateTime.UtcNow");

			DateTime localNow = DateTime.Now;
			Assert.That(localNow.Kind, Is.EqualTo(DateTimeKind.Local));
			var localRoundTrip = CrystalJson.Deserialize<DateTime>(CrystalJson.Serialize(localNow));
			Assert.That(localRoundTrip, Is.EqualTo(localNow), "RoundTrip DateTime.Now");

			// directed deserialization
			Assert.That(CrystalJson.Deserialize<DateTime>("\"\\/Date(-62135596800000)\\/\""), Is.EqualTo(DateTime.MinValue), "DateTime.MinValue");
			Assert.That(CrystalJson.Deserialize<DateTime>("\"\\/Date(946681200000+0100)\\/\""), Is.EqualTo(new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Local)), "2000-01-01 GMT+1 (Paris)");
			// YYYYMMDD
			Assert.That(CrystalJson.Deserialize<DateTime>("\"20000101\""), Is.EqualTo(new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Local)), "2000-01-01 GMT+1 (Paris)");
			// YYYYMMDDHHMMSS
			Assert.That(CrystalJson.Deserialize<DateTime>("\"20000101123456\""), Is.EqualTo(new DateTime(2000, 1, 1, 12, 34, 56, DateTimeKind.Local)), "2000-01-01 12:34:56 GMT+1 (Paris)");
			// ISO 8601
			Assert.That(CrystalJson.Deserialize<DateTime>("\"2000-01-01T12:34:56Z\""), Is.EqualTo(new DateTime(2000, 1, 1, 12, 34, 56, DateTimeKind.Utc)), "2000-01-01 12:34:56 GMT");
		}

		[Test]
		public void Test_JsonDeserialize_NodaTime_Types()
		{
			#region Instant

			Assert.That(CrystalJson.Deserialize<NodaTime.Instant>("\"1970-01-01T00:00:00Z\""), Is.EqualTo(default(NodaTime.Instant)));
			Assert.That(CrystalJson.Deserialize<NodaTime.Instant>("\"1854-06-25T21:33:54.352Z\""), Is.EqualTo(NodaTime.Instant.FromUtc(1854, 06, 25, 21, 33, 54) + NodaTime.Duration.FromMilliseconds(352)));
			Assert.That(CrystalJson.Deserialize<NodaTime.Instant>("\"-1254-06-25T21:33:54.352Z\""), Is.EqualTo(NodaTime.Instant.FromUtc(-1254, 06, 25, 21, 33, 54) + NodaTime.Duration.FromMilliseconds(352)));

			// vérifie que ca round-trip
			var now = NodaTime.SystemClock.Instance.GetCurrentInstant();
			Assert.That(CrystalJson.Deserialize<NodaTime.Instant>(CrystalJson.Serialize(now)), Is.EqualTo(now), "Instant roundtrip");

			// vérifie qu'on puisse aussi lire des dates avec des offets
			Assert.That(CrystalJson.Deserialize<NodaTime.Instant>("\"2015-07-17T00:00:00+02:00\""), Is.EqualTo(NodaTime.Instant.FromUtc(2015, 7, 16, 22, 0, 0)));
			Assert.That(CrystalJson.Deserialize<NodaTime.Instant>("\"2015-07-17T00:00:00-02:00\""), Is.EqualTo(NodaTime.Instant.FromUtc(2015, 7, 17, 2, 0, 0)));
			// et les dates locales
			Assert.That(CrystalJson.Deserialize<NodaTime.Instant>("\"2015-07-17T00:00:00\""), Is.EqualTo(NodaTime.Instant.FromDateTimeUtc(new DateTime(2015,7,17, 0, 0, 0, DateTimeKind.Local).ToUniversalTime())));

			#endregion

			#region Duration

			Assert.That(CrystalJson.Deserialize<NodaTime.Duration>("0"), Is.EqualTo(NodaTime.Duration.Zero), "Duration.Zero");
			Assert.That(CrystalJson.Deserialize<NodaTime.Duration>("3258"), Is.EqualTo(NodaTime.Duration.FromSeconds(3258)), "Duration (seconds)");
			Assert.That(CrystalJson.Deserialize<NodaTime.Duration>("5682.452"), Is.EqualTo(NodaTime.Duration.FromMilliseconds(5682452)), "Duration (seconds + miliseconds)");
			Assert.That(CrystalJson.Deserialize<NodaTime.Duration>("1E-9"), Is.EqualTo(NodaTime.Duration.Epsilon), "Duration (epsilon)");

			NodaTime.Duration elapsed = now - NodaTime.Instant.FromDateTimeUtc(new DateTime(2014, 7, 22, 23, 04, 00, DateTimeKind.Utc));
			Assert.That(CrystalJson.Deserialize<NodaTime.Duration>(CrystalJson.Serialize(elapsed)), Is.EqualTo(elapsed), "Duration roundtrip");

			#endregion

			#region ZonedDateTime

			// note: un ZonedDateTime est un Instance + DateTimeZone + Offset, mais peut être aussi représenté par un Instant (ticks) + un time zone ID
			// (http://stackoverflow.com/questions/14802672/serialize-nodatime-json#comment20786350_14830400)

			var dtz = NodaTime.DateTimeZoneProviders.Tzdb["Europe/Paris"];
			Assert.That(CrystalJson.Deserialize<NodaTime.ZonedDateTime>("\"0001-01-01T00:00:00Z UTC\""), Is.EqualTo(default(NodaTime.ZonedDateTime)));
			Assert.That(CrystalJson.Deserialize<NodaTime.ZonedDateTime>("\"1954-06-25T21:33:54.352+01:00 Europe/Paris\""), Is.EqualTo(new NodaTime.ZonedDateTime(NodaTime.Instant.FromUtc(1954, 06, 25, 20, 33, 54) + NodaTime.Duration.FromMilliseconds(352), dtz)));
			//note: si la TZID est manquante, il est impossible de désérialiser en ZonedDatetime!
			Assert.That(() => CrystalJson.Deserialize<NodaTime.ZonedDateTime>("\"1954-06-25T21:33:54.352+01:00\""), Throws.InstanceOf<FormatException>(), "Missing TimeZone ID should fail");
			//note: si l'offset n'est pas valide pour la date en question, il est impossible de désérialiser en ZonedDatetime!
			Assert.That(() => CrystalJson.Deserialize<NodaTime.ZonedDateTime>("\"1854-06-25T21:33:54.352+01:00 Europe/Paris\""), Throws.InstanceOf<FormatException>(), "Paris was on a different offset in 1854 !");

			// vérifie que ca roundtrip
			var dtzNow = new NodaTime.ZonedDateTime(now, dtz);
			Assert.That(CrystalJson.Deserialize<NodaTime.ZonedDateTime>(CrystalJson.Serialize(dtzNow)), Is.EqualTo(dtzNow), "ZonedDateTime roundtripping");

			#endregion

			#region LocalDateTime

			Assert.That(CrystalJson.Deserialize<NodaTime.LocalDateTime>("\"0001-01-01T00:00:00\""), Is.EqualTo(default(NodaTime.LocalDateTime)));
			Assert.That(CrystalJson.Deserialize<NodaTime.LocalDateTime>("\"1854-06-25T21:33:54.352\""), Is.EqualTo(new NodaTime.LocalDateTime(1854, 06, 25, 21, 33, 54, 352)));
			Assert.That(CrystalJson.Deserialize<NodaTime.LocalDateTime>("\"-1254-06-25T21:33:54.352\""), Is.EqualTo(new NodaTime.LocalDateTime(-1254, 06, 25, 21, 33, 54, 352)));

			// vérifie que ca roundtrip
			var ldtNow = dtzNow.LocalDateTime;
			Assert.That(CrystalJson.Deserialize<NodaTime.LocalDateTime>(CrystalJson.Serialize(ldtNow)), Is.EqualTo(ldtNow), "LocalDatetime roundtripping");

			// vérifie qu'on puisse désérialiser un Instant en heure locale
			Assert.That(CrystalJson.Deserialize<NodaTime.LocalDateTime>("\"2017-06-21T12:34:56Z\""), Is.EqualTo(NodaTime.Instant.FromUtc(2017, 6, 21, 12, 34, 56).InZone(NodaTime.DateTimeZoneProviders.Tzdb.GetSystemDefault()).LocalDateTime));

			#endregion

			#region DateTimeZone

			var rnd = new Random();

			//from tzdb
			string id = NodaTime.DateTimeZoneProviders.Tzdb.Ids[rnd.Next(NodaTime.DateTimeZoneProviders.Tzdb.Ids.Count)];
			Assert.That(CrystalJson.Deserialize<NodaTime.DateTimeZone>(CrystalJson.StringEncode(id)), Is.EqualTo(NodaTime.DateTimeZoneProviders.Tzdb.GetZoneOrNull(id)));

			// roundtrip
			Assert.That(CrystalJson.Deserialize<NodaTime.DateTimeZone>(CrystalJson.Serialize(dtz)), Is.EqualTo(dtz), "DateTimeZone roundtrip");

			#endregion

			#region OffsetDateTime

			Assert.That(
				CrystalJson.Deserialize<NodaTime.OffsetDateTime>("\"2012-01-02T03:04:05.0060007Z\""),
				Is.EqualTo(new NodaTime.LocalDateTime(2012, 1, 2, 3, 4, 5, 6).PlusTicks(7).WithOffset(NodaTime.Offset.Zero)),
				"Offset of 0 means UTC"
			);

			Assert.That(
				CrystalJson.Deserialize<NodaTime.OffsetDateTime>("\"2012-01-02T03:04:05.0060007+02:00\""),
				Is.EqualTo(new NodaTime.LocalDateTime(2012, 1, 2, 3, 4, 5, 6).PlusTicks(7).WithOffset(NodaTime.Offset.FromHours(2))),
				"Only HH:MM for the timezone offset"
			);

			Assert.That(
				CrystalJson.Deserialize<NodaTime.OffsetDateTime>("\"2012-01-02T03:04:05.0060007-01:30\""),
				Is.EqualTo(new NodaTime.LocalDateTime(2012, 1, 2, 3, 4, 5, 6).PlusTicks(7).WithOffset(NodaTime.Offset.FromHoursAndMinutes(-1, -30))),
				"Allow negative offsets"
			);

			#endregion

			#region Offset

			Assert.That(CrystalJson.Deserialize<NodaTime.Offset>("\"+00\""), Is.EqualTo(NodaTime.Offset.Zero));
			Assert.That(CrystalJson.Deserialize<NodaTime.Offset>("\"+00:00\""), Is.EqualTo(NodaTime.Offset.Zero));
			Assert.That(CrystalJson.Deserialize<NodaTime.Offset>("\"+02\""), Is.EqualTo(NodaTime.Offset.FromHours(2)));
			Assert.That(CrystalJson.Deserialize<NodaTime.Offset>("\"+01:30\""), Is.EqualTo(NodaTime.Offset.FromHoursAndMinutes(1, 30)));
			Assert.That(CrystalJson.Deserialize<NodaTime.Offset>("\"-01:30\""), Is.EqualTo(NodaTime.Offset.FromHoursAndMinutes(-1, -30)));

			#endregion
		}

		[Test]
		public void Test_JsonDeserialize_Array()
		{
			// empty
			string jsonText = "[]";
			object obj = CrystalJson.DeserializeBoxed(jsonText);
			Assert.That(obj, Is.Not.Null, jsonText);
			Assert.That(obj, Is.InstanceOf<IList<object>>(), jsonText);
			var res = obj as IList<object>;
			Assert.That(res.Count, Is.EqualTo(0), jsonText + ".Count");

			jsonText = "[ ]";
			obj = CrystalJson.DeserializeBoxed(jsonText);
			Assert.That(obj, Is.InstanceOf<IList<object>>(), jsonText);
			res = obj as IList<object>;
			Assert.That(res.Count, Is.EqualTo(0), jsonText + ".Count");

			// single value
			Assert.That(CrystalJson.DeserializeBoxed("[1]"), Is.EqualTo(new[] { 1 }));
			Assert.That(CrystalJson.DeserializeBoxed("[ 1 ]"), Is.EqualTo(new[] { 1 }));

			// multiple value
			Assert.That(CrystalJson.DeserializeBoxed("[1,2,3]"), Is.EqualTo(new[] { 1, 2, 3 }));
			Assert.That(CrystalJson.DeserializeBoxed("[ 1, 2, 3 ]"), Is.EqualTo(new[] { 1, 2, 3 }));

			// strings
			Assert.That(CrystalJson.DeserializeBoxed(@"[""foo"",""bar""]"), Is.EqualTo(new[] { "foo", "bar" }));

			// mixed array
			Assert.That(CrystalJson.DeserializeBoxed(@"[123,true,""foo""]"), Is.EqualTo(new object[] { 123, true, "foo" }));

			// jagged arrays
			Assert.That(CrystalJson.DeserializeBoxed(@"[ [1,2,3], [true,false], [""foo"",""bar""] ]"), Is.EqualTo(new object[] {
				new[] { 1, 2, 3 },
				new[] { true, false },
				new[] { "foo", "bar" }
			}));

			// directed
			Assert.That(CrystalJson.Deserialize<int[]>("[1,2,3]"), Is.EqualTo(new [] { 1, 2, 3 }));
			Assert.That(CrystalJson.Deserialize<long[]>("[1,2,3]"), Is.EqualTo(new [] { 1L, 2L, 3L }));
			Assert.That(CrystalJson.Deserialize<float[]>("[1.1,2.2,3.3]"), Is.EqualTo(new [] { 1.1f, 2.2f, 3.3f }));
			Assert.That(CrystalJson.Deserialize<double[]>("[1.1,2.2,3.3]"), Is.EqualTo(new [] { 1.1d, 2.2d, 3.3d }));
			Assert.That(CrystalJson.Deserialize<bool[]>("[true,false,true]"), Is.EqualTo(new [] { true, false, true }));
			Assert.That(CrystalJson.Deserialize<string[]>(@"[""foo"",""bar"",""baz""]"), Is.EqualTo(new [] { "foo", "bar", "baz" }));

			// nested
			Assert.That(CrystalJson.Deserialize<int[][]>("[[1,2],[3,4]]"), Is.EqualTo(new [] { new [] { 1, 2 }, new [] { 3, 4 } }));

		}

		[Test]
		public void Test_JsonDeserialize_STuples()
		{
			// STuple<...>
			Assert.That(CrystalJson.Deserialize<IVarTuple>("[ ]"), Is.EqualTo(STuple.Empty));
			Assert.That(CrystalJson.Deserialize<IVarTuple>("[ 123 ]"), Is.EqualTo(STuple.Create(123)));
			Assert.That(CrystalJson.Deserialize<IVarTuple>("[ 123, \"Hello\" ]"), Is.EqualTo(STuple.Create(123, "Hello")));
			Assert.That(CrystalJson.Deserialize<IVarTuple>("[ 123, \"Hello\", true ]"), Is.EqualTo(STuple.Create(123, "Hello", true)));
			Assert.That(CrystalJson.Deserialize<IVarTuple>("[ 123, \"Hello\", true, -1.5 ]"), Is.EqualTo(STuple.Create(123, "Hello", true, -1.5)));
			//note: depuis 3.13, NUnit gère appel directement IStructuralEquatable.Equals(...) avec son propre comparer (qui ne merge pas char et string)
			// => on doit passer notre comparer explicitement!
			Assert.That(CrystalJson.Deserialize<IVarTuple>("[ 123, \"Hello\", true, -1.5, \"Z\" ]"), Is.EqualTo(STuple.Create(123, "Hello", true, -1.5, 'Z')).Using(SimilarValueComparer.Default));
			Assert.That(CrystalJson.Deserialize<IVarTuple>("[ 123, \"Hello\", true, -1.5, \"Z\", \"World\" ]"), Is.EqualTo(STuple.Create(123, "Hello", true, -1.5, 'Z', "World")).Using(SimilarValueComparer.Default));
			Assert.That(CrystalJson.Deserialize<IVarTuple>("[ 123, \"Hello\", true, -1.5, \"Z\", \"World\", 456 ]"), Is.EqualTo(STuple.Create(123, "Hello", true, -1.5, 'Z', "World", 456)).Using(SimilarValueComparer.Default));
		}

		[Test]
		public void Test_JsonDeserialize_ValueTuples()
		{
			// ValueTuple<...>
			Assert.That(CrystalJson.Deserialize<ValueTuple>("[ ]"), Is.EqualTo(ValueTuple.Create()));
			Assert.That(CrystalJson.Deserialize<ValueTuple<int>>("[ 123 ]"), Is.EqualTo(ValueTuple.Create(123)));
			Assert.That(CrystalJson.Deserialize<ValueTuple<int, string>>("[ 123, \"Hello\" ]"), Is.EqualTo(ValueTuple.Create(123, "Hello")));
			Assert.That(CrystalJson.Deserialize<ValueTuple<int, string, bool>>("[ 123, \"Hello\", true ]"), Is.EqualTo(ValueTuple.Create(123, "Hello", true)));
			Assert.That(CrystalJson.Deserialize<ValueTuple<int, string, bool, double>>("[ 123, \"Hello\", true, -1.5 ]"), Is.EqualTo(ValueTuple.Create(123, "Hello", true, -1.5)));
			Assert.That(CrystalJson.Deserialize<ValueTuple<int, string, bool, double, char>>("[ 123, \"Hello\", true, -1.5, \"Z\" ]"), Is.EqualTo(ValueTuple.Create(123, "Hello", true, -1.5, 'Z')));
			Assert.That(CrystalJson.Deserialize<ValueTuple<int, string, bool, double, char, string>>("[ 123, \"Hello\", true, -1.5, \"Z\", \"World\" ]"), Is.EqualTo(ValueTuple.Create(123, "Hello", true, -1.5, 'Z', "World")));
			Assert.That(CrystalJson.Deserialize<ValueTuple<int, string, bool, double, char, string, int>>("[ 123, \"Hello\", true, -1.5, \"Z\", \"World\", 456 ]"), Is.EqualTo(ValueTuple.Create(123, "Hello", true, -1.5, 'Z', "World", 456)));

			//TODO: faut-il supporter Deserialize<ITuple> ? (en fct du .Count, on peut choisir le ValueTuple<...> correspondant, mais ca force un boxing!)
		}

		[Test]
		public void Test_JsonDeserialize_IPAddress()
		{
			Assert.That(CrystalJson.Deserialize<IPAddress>("\"127.0.0.1\""), Is.EqualTo(IPAddress.Loopback));
			Assert.That(CrystalJson.Deserialize<IPAddress>("\"0.0.0.0\""), Is.EqualTo(IPAddress.Any));

			Assert.That(CrystalJson.Deserialize<IPAddress>("\"::1\""), Is.EqualTo(IPAddress.IPv6Loopback));
			Assert.That(CrystalJson.Deserialize<IPAddress>("\"::\""), Is.EqualTo(IPAddress.IPv6Any));

			Assert.That(CrystalJson.Deserialize<IPAddress>("\"172.16.10.194\""), Is.EqualTo(IPAddress.Parse("172.16.10.194")));
			Assert.That(CrystalJson.Deserialize<IPAddress>("\"fe80::fd0b:d3d6:5a2:4549%13\""), Is.EqualTo(IPAddress.Parse("fe80::fd0b:d3d6:5a2:4549%13")));

			// on doit aussi accepter la syntaxe avec des [..] pour IPv6
			Assert.That(CrystalJson.Deserialize<IPAddress>("\"[::1]\""), Is.EqualTo(IPAddress.IPv6Loopback));
			Assert.That(CrystalJson.Deserialize<IPAddress>("\"[::]\""), Is.EqualTo(IPAddress.IPv6Any));
			Assert.That(CrystalJson.Deserialize<IPAddress>("\"[fe80::fd0b:d3d6:5a2:4549%13]\""), Is.EqualTo(IPAddress.Parse("fe80::fd0b:d3d6:5a2:4549%13")));
		}

		[Test]
		public void Test_JsonDeserialize_Version()
		{
			Assert.That(CrystalJson.Deserialize<Version>("\"1.0\""), Is.EqualTo(new Version(1, 0)));
			Assert.That(CrystalJson.Deserialize<Version>("\"1.2.3\""), Is.EqualTo(new Version(1, 2, 3)));
			Assert.That(CrystalJson.Deserialize<Version>("\"1.2.3.4\""), Is.EqualTo(new Version(1, 2, 3, 4)));
		}

		[Test]
		public void Test_JsonDeserialize_SimpleObject()
		{
			string jsonText = "{}";
			object obj = CrystalJson.DeserializeBoxed(jsonText);
			Assert.That(obj, Is.Not.Null, jsonText);
			Assert.That(obj, Is.InstanceOf<IDictionary<string, object>>(), jsonText);
			var res = obj as IDictionary<string, object>;
			Assert.That(res.Count, Is.EqualTo(0), jsonText + ".Count");

			jsonText = "{ }";
			obj = CrystalJson.DeserializeBoxed(jsonText);
			Assert.That(obj, Is.Not.Null, jsonText);
			Assert.That(obj, Is.InstanceOf<IDictionary<string, object>>(), jsonText);
			res = obj as IDictionary<string, object>;
			Assert.That(res.Count, Is.EqualTo(0), jsonText + ".Count");

			jsonText = @"{ ""Name"":""James Bond"" }";
			obj = CrystalJson.DeserializeBoxed(jsonText);
			Assert.That(obj, Is.Not.Null, jsonText);
			Assert.That(obj, Is.InstanceOf<IDictionary<string, object>>(), jsonText);
			res = obj as IDictionary<string, object>;
			Assert.That(res.Count, Is.EqualTo(1), jsonText + ".Count");
			Assert.That(res.ContainsKey("Name"), Is.True, jsonText + ".Name?");
			Assert.That(res["Name"], Is.EqualTo("James Bond"), jsonText + ".Name");

			jsonText = @"{ ""Id"":7, ""Name"":""James Bond"", ""IsDeadly"":true }";
			obj = CrystalJson.DeserializeBoxed(jsonText);
			Assert.That(obj, Is.InstanceOf<IDictionary<string, object>>(), jsonText);
			res = obj as IDictionary<string, object>;
			Assert.That(res.Count, Is.EqualTo(3), jsonText + ".Count");
			Assert.That(res["Name"], Is.EqualTo("James Bond"), jsonText + ".Name");
			Assert.That(res["Id"], Is.EqualTo(7), jsonText + ".Id");
			Assert.That(res["IsDeadly"], Is.True, jsonText + ".IsDeadly");

			jsonText = @"{ ""Id"":7, ""Name"":""James Bond"", ""IsDeadly"":true, ""Created"":""\/Date(-52106400000+0200)\/"", ""Weapons"":[{""Name"":""Walter PPK""}] }";
			obj = CrystalJson.DeserializeBoxed(jsonText);
			Assert.That(obj, Is.InstanceOf<IDictionary<string, object>>(), jsonText);
			res = obj as IDictionary<string, object>;
			Assert.That(res.Count, Is.EqualTo(5), jsonText + ".Count");
			Assert.That(res["Name"], Is.EqualTo("James Bond"), jsonText + ".Name");
			Assert.That(res["Id"], Is.EqualTo(7), jsonText + ".Id");
			Assert.That(res["IsDeadly"], Is.True, jsonText + ".IsDeadly");
			//Assert.That(res["Created"], Is.EqualTo(new DateTime(1968, 5, 8)), jsonText + ".Created");
			Assert.That(res["Created"], Is.EqualTo("/Date(-52106400000+0200)/"), jsonText + ".Created"); //BUGBUG: gérer l'auto-detect de date quand on veut une string en object ?
			var weapons = res["Weapons"] as IList<object>;
			Assert.That(weapons, Is.Not.Null, jsonText + ".Weapons");
			Assert.That(weapons.Count, Is.EqualTo(1), jsonText + ".Weapons.Count");
			var weapon = weapons[0] as IDictionary<string, object>;
			Assert.That(weapon, Is.Not.Null, jsonText + ".Weapons[0]");
			Assert.That(weapon["Name"], Is.EqualTo("Walter PPK"), jsonText + ".Weapons[0].Name");
		}

		[Test]
		public void Test_JsonDeserialize_CustomClass()
		{
			string jsonText = "{ \"Valid\": true, \"Name\": \"James Bond\", \"Index\": 7, \"Size\": 123456789, \"Height\": 1.8, \"Amount\": 0.07, \"Created\": \"1968-05-08T00:00:00Z\", \"Modified\": \"2010-10-28T15:39:00Z\", \"State\": 42, \"RatioOfStuff\": 8641975.23 }";
			var x = CrystalJson.Deserialize<DummyJsonClass>(jsonText);
			Assert.That(x, Is.Not.Null, jsonText);
			Assert.That(x, Is.InstanceOf<DummyJsonClass>());

			Assert.That(x.Valid, Is.True, "x.Valid");
			Assert.That(x.Name, Is.EqualTo("James Bond"), "x.Name");
			Assert.That(x.Index, Is.EqualTo(7), "x.Index");
			Assert.That(x.Size, Is.EqualTo(123456789), "x.Size");
			Assert.That(x.Height, Is.EqualTo(1.8f), "x.Height");
			Assert.That(x.Amount, Is.EqualTo(0.07d), "x.Amount");
			Assert.That(x.Created, Is.EqualTo(new DateTime(1968, 5, 8)), "x.Created");
			Assert.That(x.Modified, Is.EqualTo(new DateTime(2010, 10, 28, 15, 39, 0)), "x.Modified");
			Assert.That(x.State, Is.EqualTo(DummyJsonEnum.Bar), "x.State");
			Assert.That(x.RatioOfStuff, Is.EqualTo(0.07d * 123456789), "x.RatioOfStuff");

			// round trip !
			string roundtripText = CrystalJson.Serialize(x);
			Assert.That(roundtripText, Is.EqualTo(jsonText), "LOOP 2!");
			var x2 = CrystalJson.Deserialize<DummyJsonClass>(roundtripText);
			Assert.That(x2, Is.EqualTo(x), "TRUE LAST BOSS !!!");
		}

		[Test]
		public void Test_JsonDeserialize_CustomStruct()
		{
			string jsonText = "{ \"Valid\": true, \"Name\": \"James Bond\", \"Index\": 7, \"Size\": 123456789, \"Height\": 1.8, \"Amount\": 0.07, \"Created\": \"1968-05-08T00:00:00Z\", \"Modified\": \"2010-10-28T15:39:00Z\", \"State\": 42, \"RatioOfStuff\": 8641975.23 }";
			var x = CrystalJson.Deserialize<DummyJsonStruct>(jsonText);
			Assert.That(x, Is.Not.Null, jsonText);
			Assert.That(x, Is.InstanceOf<DummyJsonStruct>());

			Assert.That(x.Valid, Is.True, "x.Valid");
			Assert.That(x.Name, Is.EqualTo("James Bond"), "x.Name");
			Assert.That(x.Index, Is.EqualTo(7), "x.Index");
			Assert.That(x.Size, Is.EqualTo(123456789), "x.Size");
			Assert.That(x.Height, Is.EqualTo(1.8f), "x.Height");
			Assert.That(x.Amount, Is.EqualTo(0.07d), "x.Amount");
			Assert.That(x.Created, Is.EqualTo(new DateTime(1968, 5, 8)), "x.Created");
			Assert.That(x.Modified, Is.EqualTo(new DateTime(2010, 10, 28, 15, 39, 0)), "x.Modified");
			Assert.That(x.State, Is.EqualTo(DummyJsonEnum.Bar), "x.State");
			Assert.That(x.RatioOfStuff, Is.EqualTo(0.07d * 123456789), "x.RatioOfStuff");

			// round trip !
			string roundtripText = CrystalJson.Serialize(x);
			Assert.That(roundtripText, Is.EqualTo(jsonText), "LOOP 2!");
			var x2 = CrystalJson.Deserialize<DummyJsonStruct>(roundtripText);
			Assert.That(x2, Is.EqualTo(x), "TRUE LAST BOSS !!!");
		}

		[Test]
		public void Test_JsonDeserialize_EvilGadgetObject()
		{
			string token = Guid.NewGuid().ToString();

			string path = GetTemporaryPath(token + ".txt");
			if (File.Exists(path)) File.Delete(path);
			Log("Canary location: " + path);

			Log(CrystalJson.Serialize(@"""hello"""));

			string jsonText = @"{
    ""_class"":""System.Windows.Data.ObjectDataProvider, PresentationFramework, Version=4.0.0.0, Culture=neutral, PublicKeyToken=31bf3856ad364e35"",
    ""ObjectInstance"":{
		""_class"":""System.Diagnostics.Process, System, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089"",
		""StartInfo"": {
			""_class"": ""System.Diagnostics.ProcessStartInfo, System, Version = 4.0.0.0, Culture = neutral, PublicKeyToken = b77a5c561934e089"",
			""FileName"": ""cmd"",
			""Arguments"": " + CrystalJson.Serialize($@"""/c echo foo > {path}""") + @"
		}
	},
    ""MethodName"":""Start""
}";

			Log(jsonText);

			var parsed = CrystalJson.Parse(jsonText);
			Log("Parsed: " + parsed);
			try
			{
				var gadget = parsed.Bind(typeof(object));

				// ATTENTION: ATTENTION: CRITICAL FAILURE!
				// => si la désérialisation n'échoue pas a cet endroit, c'est qu'on a été capable d'executer du code juste en désérialisant du JSON!!!

				Log("FATAL: Created a " + gadget.GetType());

				//note: l'execution de la commande se fait en asynchrone :(
				// => on attend un peu en esperant que ca soit suffisant
				Thread.Sleep(2000);

				if (File.Exists(path))
				{
					// ATTENTION: ATTENTION: CRITICAL FAILURE!
					// => si ce test échoue a cet endroit, c'est qu'on a été capable d'executer du code juste en désérialisant du JSON!!!
					Assert.Fail("FATAL: deserialization of bad JSON was able to execute the payload!!!!");
				}
				else
				{
					Assert.Fail("Deserialization should have failed but did not produce any observable result... THIS IS STILL A BAD THING!");
				}
			}
			catch (Exception e)
			{
				Log($"Binding attempt failed as expected : [{e.GetType().Name}] {e.Message}");
				Assert.That(e, Is.InstanceOf<JsonBindingException>(), "Deserialization should have failed with an expected JSON error");
				// si c'est pas le bon type d'erreur, c'est peut être que la payload allait presque s'executer mais que ca a échoué pour un détail....
			}
		}

		public abstract class DummyBaseClass { }

		public class DummyFooClass : DummyBaseClass { }

		public class DummyBarClass { }// ne dérive PAS de "base"

		[Test]
		public void Test_JsonDeserialize_Incompatible_Type_In_Property()
		{
			// pour essayer de contrer les vulnérabilité de désérialisation de type (cf Test_JsonDeserialize_EvilGadgetObject),
			// on doit aussi vérifier que le binding d'un type qui ne match pas le parent échoue "correctement"

			var json = CrystalJson.Serialize<DummyBaseClass>(new DummyFooClass());
			Log(json);

			// si on demande de désérialiser en type incompatible, on doit avoir une JsonBindingException
			Assert.That(() => CrystalJson.Deserialize<DummyBarClass>(json), Throws.InstanceOf<JsonBindingException>());
			// par contre, si on demande de désérialiser en "object" alors on ne peut pas vraiment savoir
			Assert.That(CrystalJson.Deserialize<object>(json), Is.Not.Null);
		}

		#endregion

		#region Streaming...

		[Test]
		public async Task Test_Can_Stream_Arrays_To_Disk()
		{
			var cancel = CancellationToken.None;
			var rnd = new Random();

			// vide
			var path = GetTemporaryPath("null.json");
			using (var fs = File.Create(path))
			{
				using (new CrystalJsonStreamWriter(fs, CrystalJsonSettings.Json, null, ownStream: false))
				{
					//nothing
				}
				Assert.That(fs.CanWrite, Is.True, "Stream should be kept open (ownStream: false)");
			}
			var arr = CrystalJson.LoadFrom<List<int>>(path);
			Assert.That(arr, Is.Null);

			// batch vide
			path = GetTemporaryPath("empty.json");
			using (var fs = File.Create(path))
			{
				using (var stream = new CrystalJsonStreamWriter(fs, CrystalJsonSettings.Json, null, ownStream: true))
				{
					using (stream.BeginArrayFragment())
					{
						//no-op
					}

					await stream.FlushAsync(cancel);
				}
				Assert.That(fs.CanWrite, Is.False, "Stream should have been closed (ownStream: true)");
			}
			arr = CrystalJson.LoadFrom<List<int>>(path);
			Assert.That(arr, Is.Not.Null);
			Assert.That(arr.Count, Is.EqualTo(0));

			// un seul élément
			path = GetTemporaryPath("forty_two.json");
			using (var stream = CrystalJsonStreamWriter.Create(path))
			{
				using (var array = stream.BeginArrayFragment(cancel))
				{
					await array.WriteItemAsync(42);
				}
				await stream.FlushAsync(cancel);
			}
			arr = CrystalJson.LoadFrom<List<int>>(path);
			Assert.That(arr, Is.Not.Null);
			Assert.That(arr, Is.EqualTo(new[] { 42 }));

			// un seul batch
			path = GetTemporaryPath("one_batch.json");
			using (var stream = CrystalJsonStreamWriter.Create(path))
			{
				await stream.WriteArrayFragmentAsync(Enumerable.Range(0, 1000), cancel);
			}
			arr = CrystalJson.LoadFrom<List<int>>(path);
			Assert.That(arr, Is.Not.Null);
			Assert.That(arr, Is.EqualTo(Enumerable.Range(0, 1000)));

			// plusieurs batchs
			path = GetTemporaryPath("multiple_batchs.json");
			using (var stream = CrystalJsonStreamWriter.Create(path))
			{
				await stream.WriteArrayFragmentAsync(async (array) =>
				{
					for (int i = 0; i < 10; i++)
					{
						await array.WriteBatchAsync(Enumerable.Range(i * 100, 100));
					}
				}, cancel);
			}
			arr = CrystalJson.LoadFrom<List<int>>(path);
			Assert.That(arr, Is.Not.Null);
			Assert.That(arr, Is.EqualTo(Enumerable.Range(0, 1000)));

			// switch de types
			path = GetTemporaryPath("int_long.json");
			using (var stream = CrystalJsonStreamWriter.Create(path))
			{
				await stream.WriteArrayFragmentAsync(async (array) =>
				{
					await array.WriteBatchAsync<int>(Enumerable.Range(0, 500));
					await array.WriteBatchAsync<long>(Enumerable.Range(500, 500).Select(x => (long)x));
				}, cancel);
			}
			arr = CrystalJson.LoadFrom<List<int>>(path);
			Assert.That(arr, Is.Not.Null);
			Assert.That(arr, Is.EqualTo(Enumerable.Range(0, 1000)));

			// guids
			path = GetTemporaryPath("guids.json");
			using (var stream = CrystalJsonStreamWriter.Create(path))
			{
				await stream.WriteArrayFragmentAsync(async (array) =>
				{
					for (int i = 0; i < 100; i++)
					{
						var batch = Enumerable.Range(0, rnd.Next(100)).Select(_ => Guid.NewGuid());
						await array.WriteBatchAsync(batch);
					}
				}, cancel);
			}
			var guids = CrystalJson.LoadFrom<List<Guid>>(path);
			Assert.That(guids, Is.Not.Null);

			// anonymous types
			path = GetTemporaryPath("objects.json");
			using (var stream = CrystalJsonStreamWriter.Create(path))
			{
				await stream.WriteArrayFragmentAsync(async (array) =>
				{
					for (int i = 0; i < 100; i++)
					{
						var batch = Enumerable.Range(i * 100, rnd.Next(100));
						await array.WriteBatchAsync(batch, (x) => new { Id = Guid.NewGuid(), Index = x, Score = Math.Round(rnd.NextDouble() * 100, 3), Rnd = Stopwatch.GetTimestamp() });
					}
				}, cancel);
			}
			//TODO: verify!

			// compress
			path = GetTemporaryPath("objects.json.gz");
			using (var fs = File.Create(path + ".gz"))
			using (var gz = new GZipStream(fs, CompressionMode.Compress, false))
			using (var stream = new CrystalJsonStreamWriter(gz, CrystalJsonSettings.Json))
			{
				await stream.WriteArrayFragmentAsync(async (array) =>
				{
					for (int i = 0; i < 100; i++)
					{
						var batch = Enumerable.Range(i * 100, rnd.Next(100));
						await array.WriteBatchAsync(batch, (x) => new { Id = Guid.NewGuid(), Index = x, Score = Math.Round(rnd.NextDouble() * 100, 3), Rnd = Stopwatch.GetTimestamp() });
					}
				}, cancel);
			}
			//TODO: verify!
		}

		[Test]
		public async Task Test_Can_Stream_Objects_To_Disk()
		{
			var cancel = CancellationToken.None;
			var rnd = new Random();
			var clock = new Stopwatch();

			// object vide
			Log("Saving simple flat object...");
			var path = GetTemporaryPath("empty.json");
			File.Delete(path);
			using (var fs = File.Create(path))
			{
				using (var writer = new CrystalJsonStreamWriter(fs, CrystalJsonSettings.Json, null, true))
				{
					using (writer.BeginObjectFragment(cancel))
					{
						//no-op
					}

					await writer.FlushAsync(cancel);
				}
				Assert.That(fs.CanWrite, Is.False, "Stream should have been closed");
			}
			// vérification
			Log("> reloading...");
			var verify = CrystalJson.ParseObjectFrom(path);
			Dump(verify);
			Log("> verifying...");
			Assert.That(verify, Is.Not.Null);
			Assert.That(verify.Count, Is.EqualTo(0), "Object should be empty!");

			// objet classique (flat)
			Log("Saving simple flat object...");
			path = GetTemporaryPath("hello_world.json");
			File.Delete(path);
			var now = DateTimeOffset.Now;
			using (var writer = CrystalJsonStreamWriter.Create(path))
			{
				using (var obj = writer.BeginObjectFragment(cancel))
				{
					obj.WriteField("Hello", "World");
					obj.WriteField("PowerLevel", 8001); // Over 8000 !!!!
					obj.WriteField("Date", now);
				}
				await writer.FlushAsync(cancel);
			}
			Log($"> {new FileInfo(path).Length:N0} bytes");

			// vérification
			Log("> reloading...");
			verify = CrystalJson.ParseObjectFrom(path);
			Dump(verify);
			Log("> verifying...");
			Assert.That(verify, Is.Not.Null);
			Assert.That(verify.Get<string>("Hello"), Is.EqualTo("World"), ".Hello");
			Assert.That(verify.Get<int>("PowerLevel"), Is.GreaterThan(8000).And.EqualTo(8001), ".PowerLevel");
			Assert.That(verify.Get<DateTimeOffset>("Date"), Is.EqualTo(now), ".Date");

			// objet contenant une array streamée de grande taille

			path = GetTemporaryPath("data.json");
			Log("Saving object with large streamed array...");
			File.Delete(path);
			clock.Restart();
			using (var writer = CrystalJsonStreamWriter.Create(path))
			{
				using (var obj = writer.BeginObjectFragment(cancel))
				{
					obj.WriteField("Id", "FOOBAR9000");
					obj.WriteField("Date", now);
					using (var arr = obj.BeginArrayStream("Values"))
					{
						// on simule un dump de 365j de data à précision 1 minute, où un batch = 1 jour (1440 values)
						for (int i = 0; i < 365; i++)
						{
							var batch = Enumerable.Range(0, 1440).Select(_ => KeyValuePair.Create(Stopwatch.GetTimestamp(), Math.Round(rnd.NextDouble() * 100000.0, 1)));
							await arr.WriteBatchAsync(batch);
						}
					}
				}
				await writer.FlushAsync(cancel);
			}
			clock.Stop();
			var sizeRaw = new FileInfo(path).Length;
			Log($"> {sizeRaw:N0} bytes in {clock.Elapsed.TotalMilliseconds:N1} ms");

			// vérification
			Log("> reloading...");
			verify = CrystalJson.ParseObjectFrom(path);
			//trop gros pour être dumpé!
			Log("> verifying...");
			Assert.That(verify, Is.Not.Null);
			Assert.That(verify.Get<string>("Id"), Is.EqualTo("FOOBAR9000"), ".Id");
			Assert.That(verify.Get<DateTimeOffset>("Date"), Is.EqualTo(now), ".Date");
			var values = verify.GetArray("Values");
			Assert.That(values, Is.Not.Null, ".Values[]");
			Assert.That(values.Count, Is.EqualTo(365 * 1440), ".Values[] should have 365 fragments of 1440 values combined into a single array");
			Assert.That(values.GetElementsTypeOrDefault(), Is.EqualTo(JsonType.Array), ".Values[] should only contain arrays");
			Assert.That(values.AsArrays(), Is.All.Count.EqualTo(2), ".Values[] should only have arrays of size 2");

			// même deal, mais avec compression gzip
			Log("Saving object with large streamed array to compressed file...");
			File.Delete(path + ".gz");
			clock.Restart();
			using (var fs = File.Create(path + ".gz"))
			using (var gz = new GZipStream(fs, CompressionMode.Compress, false))
			using (var writer = new CrystalJsonStreamWriter(gz, CrystalJsonSettings.Json))
			{
				using (var obj = writer.BeginObjectFragment(cancel))
				{
					obj.WriteField("Id", "FOOBAR9000");
					obj.WriteField("Date", now);
					using (var arr = obj.BeginArrayStream("Values"))
					{
						// on simule un dump de 365j de data à précision 1 minute, où un batch = 1 jour (1440 values)
						for (int i = 0; i < 365; i++)
						{
							var batch = Enumerable.Range(0, 1440).Select(_ => KeyValuePair.Create(Stopwatch.GetTimestamp(), Math.Round(rnd.NextDouble() * 100000.0, 1)));
							await arr.WriteBatchAsync(batch);
						}
					}
				}
				await writer.FlushAsync(cancel);
			}
			clock.Stop();
			var sizeCompressed = new FileInfo(path + ".gz").Length;
			Log($"> {sizeCompressed:N0} bytes in {clock.Elapsed.TotalMilliseconds:N1} ms (1 : {(double) sizeRaw / sizeCompressed:N2})");
			Assert.That(sizeCompressed, Is.LessThan(sizeRaw / 2), "Compressed file should be AT MINMUM 50% smaller than original");

			// vérification
			Log("> reloading...");
			using(var fs = File.OpenRead(path + ".gz"))
			using (var gs = new GZipStream(fs, CompressionMode.Decompress, false))
			{
				verify = CrystalJson.ParseObjectFrom(gs);
			}

			Log("> verifying...");
			Assert.That(verify, Is.Not.Null);
			Assert.That(verify.Get<string>("Id"), Is.EqualTo("FOOBAR9000"), ".Id");
			Assert.That(verify.Get<DateTimeOffset>("Date"), Is.EqualTo(now), ".Date");
			values = verify.GetArray("Values");
			Assert.That(values, Is.Not.Null, ".Values[]");
			Assert.That(values.Count, Is.EqualTo(365 * 1440), ".Values[] should have 365 fragments of 1440 values combined into a single array");
			Assert.That(values.GetElementsTypeOrDefault(), Is.EqualTo(JsonType.Array), ".Values[] should only contain arrays");
			Assert.That(values.AsArrays(), Is.All.Count.EqualTo(2), ".Values[] should only have arrays of size 2");

		}

		[Test]
		public async Task Test_Can_Stream_Multiple_Fragments_To_Disk()
		{
			var cancel = CancellationToken.None;
			var rnd = new Random();

			// batch vide
			Log("Saving empty batches...");
			var path = GetTemporaryPath("three_empty_objects.json");
			using (var writer = CrystalJsonStreamWriter.Create(path, CrystalJsonSettings.Json))
			{
				using (writer.BeginObjectFragment(cancel))
				{
					//no-op
				}
				using (writer.BeginObjectFragment(cancel))
				{
					//no-op
				}
				using (writer.BeginObjectFragment(cancel))
				{
					//no-op
				}
				await writer.FlushAsync(cancel);
			}
			Log("> done");

			// objet meta + data series
			path = GetTemporaryPath("device.json");
			Log("Saving multi-fragments 'export'...");
			using (var writer = CrystalJsonStreamWriter.Create(path, CrystalJsonSettings.Json))
			{
				var metric = new {
					Id = "123ABC",
					Vendor = "ACME",
					Model = "HAL 9001",
					Metrics = new[] { "Foo", "Bar", "Baz" },
				};

				// first obj = meta contenant une array d'id
				await writer.WriteFragmentAsync(metric, cancel);

				// ensuite, une array pour chaque id
				foreach(var _ in metric.Metrics)
				{
					using (var arr = writer.BeginArrayFragment(cancel))
					{
						await arr.WriteBatchAsync(Enumerable.Range(0, 10).Select(_ => KeyValuePair.Create(Stopwatch.GetTimestamp(), rnd.Next())));
					}
				}
			}
			Log($"> saved {new FileInfo(path).Length:N0} bytes");

			// read back
			Log("> reloading...");
			using (var reader = CrystalJsonStreamReader.Open(path))
			{
				// metrics meta
				Log("> Readining metadata object...");
				var frag = reader.ReadNextFragment();
				DumpCompact(frag);
				Assert.That(frag, Is.Not.Null);
				Assert.That(frag.Type, Is.EqualTo(JsonType.Object));
				var m = (JsonObject)frag;
				Assert.That(m.Get<string>("Id"), Is.EqualTo("123ABC"));
				Assert.That(m.Get<string>("Vendor"), Is.EqualTo("ACME"));
				Assert.That(m.Get<string>("Model"), Is.EqualTo("HAL 9001"));
				Assert.That(m.Get<string[]>("Metrics"), Is.Not.Null.And.Length.EqualTo(3));

				// metrics value
				foreach (var id in m.GetArray("Metrics", required: true).Cast<string>())
				{
					Log($"> Readining batch for {id}...");
					frag = reader.ReadNextFragment();
					DumpCompact(frag);
					Assert.That(frag, Is.Not.Null);
					Assert.That(frag.Type, Is.EqualTo(JsonType.Array));
					var a = (JsonArray)frag;
					Log($"> {a.Count}");
					Assert.That(a.Count, Is.EqualTo(10));
				}

				// fin de fichier
				frag = reader.ReadNextFragment();
				Assert.That(frag, Is.Null);
			}

		}

#endregion

	}

	#region Helper Classes

	static class AssertJson
	{
		public static void ParseAreEqual(JsonValue expected, string jsonText, string message = null)
		{
			var parsed = CrystalJson.Parse(jsonText);
			Assert.That(parsed, Is.EqualTo(expected), $"CrystalJson.Parse('{jsonText}') into {expected.Type}{(message == null ? string.Empty : (": " + message))}");
		}
	}

	enum DummyJsonEnum
	{
		None,
		Foo = 1,
		Bar = 42,
	}

	[Flags]
	enum DummyJsonEnumFlags
	{
		None,
		Foo = 1,
		Bar = 2,
		Narf = 4
	}

	enum DummyJsonEnumShort : ushort
	{
		None,
		One = 1,
		Two = 2,
		MaxValue = 65535
	}

	enum DummyJsonEnumTypo
	{
		None,
		Foo,
		Bar = 2,   // nouveau nom, sans la typo
		Barrh = 2, // ancienne version avec la typo, présente dans des vieux documents
		Baz
	}

#pragma warning disable 169, 649
	struct DummyJsonStruct
	{
		public bool Valid;
		public string Name;
		public int Index;
		public long Size;
		public float Height;
		public double Amount;
		public DateTime Created;
		public DateTime? Modified;
		public DummyJsonEnum State;
		public double RatioOfStuff => this.Amount * this.Size;

		private string Invisible;
		private string DotNotCall => "ShouldNotBeCalled";
	}
#pragma warning restore 169, 649

	struct DummyNullableStruct
	{
		public bool? Bool;
		public int? Int32;
		public long? Int64;
		public float? Single;
		public double? Double;
		public DateTime? DateTime;
		public TimeSpan? TimeSpan;
		public Guid? Guid;
		public DummyJsonEnum? Enum;
		public DummyJsonStruct? Struct;
	}

	interface IDummyCustomInterface
	{
		string Name { get; }
		int Index { get; }
		long Size { get; }
		float Height { get; }
		double Amount { get; }
		DateTime Created { get; }
		DateTime? Modified { get; }
		DummyJsonEnum State { get; }
	}

	class DummyOuterClass
	{
		public int Id { get; set; }
		public IDummyCustomInterface Agent { get; set; }
	}

	class DummyOuterDerivedClass
	{
		public int Id { get; set; }
		public DummyJsonClass Agent { get; set; }
	}

	class DummyJsonClass : IDummyCustomInterface
	{
		private string m_invisible = "ShoudNotBeVisible";
		private string m_name;
		public bool Valid => m_name != null;
		public string Name { get => m_name; set => m_name = value; }
		public int Index { get; set; }
		public long Size { get; set; }
		public float Height { get; set; }
		public double Amount { get; set; }
		public DateTime Created { get; set; }
		public DateTime? Modified { get; set; }
		public DummyJsonEnum State { get; set; }

		public double RatioOfStuff => Amount * Size;

		// ReSharper disable once UnusedMember.Local
		private string Invisible => m_invisible;

		public string MustNotBeCalled() { return "ShouldNotBeCalled"; }

		public override bool Equals(object? obj)
		{
			if (obj is not DummyJsonClass other) return false;
			return Index == other.Index
				&& m_name == other.m_name
				&& Size == other.Size
				&& Height == other.Height
				&& Amount == other.Amount
				&& Created == other.Created
				&& Modified == other.Modified
				&& State == other.State;
		}

		public override int GetHashCode()
		{
			// ReSharper disable once NonReadonlyMemberInGetHashCode
			return this.Index;
		}
	}

	class DummyDerivedJsonClass : DummyJsonClass
	{
		private string m_doubleAgentName;

		public DummyDerivedJsonClass() { }

		public DummyDerivedJsonClass(string doubleAgentName)
		{
			m_doubleAgentName = doubleAgentName;
		}

		public bool IsDoubleAgent => m_doubleAgentName != null;

		public string DoubleAgentName { get => m_doubleAgentName; set => m_doubleAgentName = value; }
	}

	class DummyJsonCustomClass : IJsonSerializable, IJsonBindable
	{
		public string DontCallThis => "ShouldNotSeeThat";

		private DummyJsonCustomClass()
		{ }

		public DummyJsonCustomClass(string secret)
		{
			m_secret = secret;
		}

		private string m_secret;

		public string GetSecret() { return m_secret; }

		#region IJsonSerializable Members

		void IJsonSerializable.JsonSerialize(CrystalJsonWriter writer)
		{
			Assert.That(writer, Is.Not.Null, "writer");
			Assert.That(writer.Buffer, Is.Not.Null, "writer.Buffer");
			Assert.That(writer.Settings, Is.Not.Null, "writer.Settings");
			// TODO: comment gérer les settings ?
			writer.WriteRaw("{ \"custom\":" + JsonEncoding.Encode(m_secret) + " }");
		}

		void IJsonSerializable.JsonDeserialize(JsonObject value, Type declaredType, ICrystalJsonTypeResolver resolver)
		{
			Assert.Fail("Should never be called because we are also IJsonBindable!");
		}

		#endregion

		#region IJsonBindable Members

		JsonValue IJsonPackable.JsonPack(CrystalJsonSettings settings, ICrystalJsonTypeResolver resolver)
		{
			Assert.That(settings, Is.Not.Null, "settings");
			Assert.That(resolver, Is.Not.Null, "resolver");

			var obj = JsonObject.Empty;
			obj.Set<string>("custom", m_secret);
			return obj;
		}

		void IJsonBindable.JsonUnpack(JsonValue value, ICrystalJsonTypeResolver resolver)
		{
			Assert.That(value, Is.Not.Null, "value");
			Assert.That(resolver, Is.Not.Null, "resolver");

			Assert.That(value.Type, Is.EqualTo(JsonType.Object));
			var obj = (JsonObject)value;

			var customString = obj.Get<string>("custom");
			m_secret = customString ?? throw new ArgumentException("Missing 'custom' value for DummyCustomJson", nameof(value));
		}

		#endregion

	}


	class DummyStaticCustomJson
	{
		public string DontCallThis => "ShouldNotSeeThat";

		private DummyStaticCustomJson()
		{ }

		public DummyStaticCustomJson(string secret)
		{
			m_secret = secret;
		}

		private string m_secret;

		public string GetSecret() { return m_secret; }

		#region IJsonSerializable Members

		/// <summary>Méthode static utilisée pour sérialiser un objet</summary>
		/// <param name="instance"></param>
		/// <param name="writer"></param>
		public static void JsonSerialize(DummyStaticCustomJson instance, CrystalJsonWriter writer)
		{
			Assert.That(writer, Is.Not.Null, "writer");
			Assert.That(writer.Buffer, Is.Not.Null, "writer.Buffer");
			Assert.That(writer.Settings, Is.Not.Null, "writer.Settings");
			// TODO: comment gérer les settings ?
			writer.WriteRaw("{ \"custom\":" + JsonEncoding.Encode(instance.m_secret) + " }");
		}

		/// <summary>Méthode statique utilisée pour désérialiser un objet</summary>
		public static DummyStaticCustomJson JsonDeserialize(JsonObject value, ICrystalJsonTypeResolver resolver)
		{
			Assert.That(value, Is.Not.Null, "value");

			// doit contenir une string "custom"
			var customString = value.Get<string>("custom");
			if (customString == null) throw new ArgumentException("Missing 'custom' value for DummyCustomJson", nameof(value));
			return new DummyStaticCustomJson(customString);

		}

		#endregion
	}

	/// <summary>Pure POCO class with all fields readonly, that uses Duck Typeing for serialization/deserialization</summary>
	internal sealed class DummyCtorBasedJsonSerializableClass // uses Duck Typing instead of IJsonSerializable or IJsonBindable
	{

		public int Id { get; }

		public string Name { get; }

		public System.Drawing.Color Color { get; }
		// Serialized as the name of the color

		public int X { get; }
		public int Y { get; }
		// X & Y are serialized into a combined string field "XY" = "{X}:{Y}"

		/// <summary>Cached value that must not be serialized, and must be recomputed on deserialization</summary>
		private int CacheHashCode { get; }

		/// <summary>Constructeur public, utilisé par l'application pour créé les objets</summary>
		public DummyCtorBasedJsonSerializableClass(int id, string name, System.Drawing.Color color, int x, int y)
		{
			this.Id = id;
			this.Name = name;
			this.Color = color;
			this.X = x;
			this.Y = y;
			this.CacheHashCode = this.X ^ this.Y ^ this.Id ^ this.Name.GetHashCode();
		}

		/// <summary>Constructeur privé, utilisé lors de la désérialisation JSON</summary>
		private DummyCtorBasedJsonSerializableClass(JsonObject json, ICrystalJsonTypeResolver _)
		{
			this.Id = json.Get<int>("Id", required: true);
			this.Name = json.Get<string>("Name", required: true);
			this.Color = System.Drawing.Color.FromName(json.Get<string>("Color") ?? "Black");
			string xy = json.Get<string>("XY", required: true);
			if (!string.IsNullOrEmpty(xy))
			{
				// Don't try this at home!
				int p = xy.IndexOf(":", StringComparison.Ordinal);
				this.X = int.Parse(xy.Substring(0, p), CultureInfo.InvariantCulture);
				this.Y = int.Parse(xy.Substring(p + 1), CultureInfo.InvariantCulture);
			}
			this.CacheHashCode = this.X ^ this.Y ^ this.Id ^ this.Name.GetHashCode();
		}

		public void JsonSerialize(CrystalJsonWriter writer)
		{
			var state = writer.BeginObject();
			{
				writer.WriteField("Id", this.Id);
				writer.WriteField("Name", this.Name);
				writer.WriteField("Color", this.Color.Name);
				// Don't try this at home!
				writer.WriteField("XY", string.Format(CultureInfo.InvariantCulture, "{0}:{1}", this.X, this.Y));
			}
			writer.EndObject(state);
		}

		public JsonValue JsonPack(CrystalJsonSettings settings, ICrystalJsonTypeResolver resolver)
		{
			// Don't try this at home!
			return JsonObject.Create(
				"Id", this.Id,
				"Name", this.Name,
				"Color", this.Color.Name,
				"XY", string.Format(CultureInfo.InvariantCulture, "{0}:{1}", this.X, this.Y)
			);
		}

		public override bool Equals(object? obj)
		{
			if (obj is not DummyCtorBasedJsonBindableClass other) return false;
			return other.Id == this.Id && other.Name == this.Name && other.X == this.X && other.Y == this.Y && other.Color == this.Color;
		}

		public override int GetHashCode()
		{
			return this.CacheHashCode;
		}
	}

	/// <summary>Pure POCO class with all fields readonly, that uses Duck Typeing for serialization/deserialization</summary>
	internal sealed class DummyCtorBasedJsonBindableClass
	{
		//note: the ONLY difference between this and DummyCtorBasedJsonSerializable, is that it does NOT implement JsonSerialize
		// => this is to test the code path that will first call JsonPack(..) then serialize the returned JsonValue (less efficient, but at least will serialize the correct output)

		public int Id { get; }

		public string Name { get; }

		 public System.Drawing.Color Color { get; }
		 // Serialized as the name of the color

		public int X { get; }
		public int Y { get; }
		 // X & Y are serialized into a combined string field "XY" = "{X}:{Y}"

		/// <summary>Cached value that must not be serialized, and must be recomputed on deserialization</summary>
		private int CacheHashCode { get; }

		/// <summary>Constructeur public, utilisé par l'application pour créé les objets</summary>
		public DummyCtorBasedJsonBindableClass(int id, string name, System.Drawing.Color color, int x, int y)
		{
			this.Id = id;
			this.Name = name;
			this.Color = color;
			this.X = x;
			this.Y = y;
			 this.CacheHashCode = this.X ^ this.Y ^ this.Id ^ this.Name.GetHashCode();
		}

		/// <summary>Constructeur privé, utilisé lors de la désérialisation JSON</summary>
		private DummyCtorBasedJsonBindableClass(JsonObject json, ICrystalJsonTypeResolver _)
		{
			this.Id = json.Get<int>("Id", required: true);
			this.Name = json.Get<string>("Name", required: true);
			this.Color = System.Drawing.Color.FromName(json.Get<string>("Color") ?? "Black");
			string xy = json.Get<string>("XY", required: true);
			if (!string.IsNullOrEmpty(xy))
			{
				// Don't try this at home!
				int p = xy.IndexOf(":", StringComparison.Ordinal);
				this.X = int.Parse(xy.Substring(0, p), CultureInfo.InvariantCulture);
				this.Y = int.Parse(xy.Substring(p + 1), CultureInfo.InvariantCulture);
			}
			this.CacheHashCode = this.X ^ this.Y ^ this.Id ^ this.Name.GetHashCode();
		}

		//note: does NOT implement JsonSerialize !

		public JsonValue JsonPack(CrystalJsonSettings settings, ICrystalJsonTypeResolver resolver)
		{
			// Don't try this at home!
			return JsonObject.Create(
				"Id", this.Id,
				"Name", this.Name,
				"Color", this.Color.Name,
				"XY", String.Format(CultureInfo.InvariantCulture, "{0}:{1}", this.X, this.Y)
			);
		}

		public override bool Equals(object? obj)
		{
			if (obj is not DummyCtorBasedJsonBindableClass other) return false;
			return other.Id == this.Id && other.Name == this.Name && other.X == this.X && other.Y == this.Y && other.Color == this.Color;
		}

		public override int GetHashCode()
		{
			return this.CacheHashCode;
		}
	}

	/// <summary>Pure POCO class with all fields readonly, that uses Duck Typeing for serialization/deserialization</summary>
	internal readonly struct DummyCtorBasedJsonSerializableStruct // uses Duck Typing instead of IJsonSerializable or IJsonBindable
	{

		public readonly int Id;

		public readonly string Name;

		public readonly int X;

		public readonly int Y;
		// X & Y are serialized into a combined string field "XY" = "{X}:{Y}"

		/// <summary>Cached value that must not be serialized, and must be recomputed on deserialization</summary>
		private readonly int CacheHashCode;

		/// <summary>Constructeur public, utilisé par l'application pour créé les objets</summary>
		public DummyCtorBasedJsonSerializableStruct(int id, string name, int x, int y)
		{
			this.Id = id;
			this.Name = name;
			this.X = x;
			this.Y = y;
			this.CacheHashCode = this.X ^ this.Y ^ this.Id ^ this.Name.GetHashCode();
		}

		/// <summary>Constructeur privé, utilisé lors de la désérialisation JSON</summary>
		private DummyCtorBasedJsonSerializableStruct(JsonObject json, ICrystalJsonTypeResolver _)
		{
			this.Id = json.Get<int>("Id", required: true);
			this.Name = json.Get<string>("Name", required: true);
			string xy = json.Get<string>("XY", required: true);
			if (!string.IsNullOrEmpty(xy))
			{
				// Don't try this at home!
				int p = xy.IndexOf(":", StringComparison.Ordinal);
				this.X = int.Parse(xy.AsSpan(0, p), CultureInfo.InvariantCulture);
				this.Y = int.Parse(xy.AsSpan(p + 1), CultureInfo.InvariantCulture);
			}
			else
			{
				this.X = 0;
				this.Y = 0;
			}
			this.CacheHashCode = this.X ^ this.Y ^ this.Id ^ this.Name.GetHashCode();
		}

		public void JsonSerialize(CrystalJsonWriter writer)
		{
			var state = writer.BeginObject();
			{
				writer.WriteField("Id", this.Id);
				writer.WriteField("Name", this.Name);
				// Don't try this at home!
				writer.WriteField("XY", string.Format(CultureInfo.InvariantCulture, "{0}:{1}", this.X, this.Y));
			}
			writer.EndObject(state);
		}

		public JsonValue JsonPack(CrystalJsonSettings settings, ICrystalJsonTypeResolver resolver)
		{
			// Don't try this at home!
			return JsonObject.Create(
				"Id", this.Id,
				"Name", this.Name,
				"XY", string.Format(CultureInfo.InvariantCulture, "{0}:{1}", this.X, this.Y)
			);
		}

		public override bool Equals(object? obj)
		{
			if (obj is not DummyCtorBasedJsonBindableClass other) return false;
			return other.Id == this.Id && other.Name == this.Name && other.X == this.X && other.Y == this.Y;
		}

		public override int GetHashCode()
		{
			return this.CacheHashCode;
		}
	}

	[DataContract]
	class DummyDataContractClass
	{
		[DataMember(Name = "Id")]
		public int AgentId;

		[DataMember]
		public string Name;

		[DataMember]
		public int Age;

		[DataMember(Name = "IsFemale")]
		public bool Female;

		// pas d'attribut
		public string InvisibleField;

		[DataMember]
		public string CurrentLoveInterest { get; set; }

		[DataMember]
		public string VisibleProperty => "CanBeSeen";

		// pas d'attributre
		public string InvisibleProperty => "ShouldNotBeSeen";
	}

#pragma warning disable 649
	class DummyXmlSerializableContractClass
	{
		[XmlAttribute(AttributeName = "Id")]
		public int AgentId;

		public string Name;

		public int Age;

		[XmlElement(ElementName = "IsFemale")]
		public bool Female;

		[XmlIgnore]
		public string InvisibleField;

		public string CurrentLoveInterest { get; set; }

		public string VisibleProperty => "CanBeSeen";

		[XmlIgnore]
		public string InvisibleProperty => "ShouldNotBeSeen";
	}
#pragma warning restore 649

	class DummyCustomJsonResolver : CrystalJsonTypeResolver
	{
		protected override CrystalJsonTypeDefinition GetTypeDefinition(Type type)
		{
			if (type != typeof(DummyJsonClass))
				return base.GetTypeDefinition(type);

			CrystalJsonTypeBinder binder = (v, t, r) => v.Bind(t, r);

			return new CrystalJsonTypeDefinition(type, null, "Dummy",
				null,
				() => new DummyJsonClass(),
				new[] {
						new CrystalJsonMemberDefinition()
						{
							Name = "Foo",
							Type = typeof(string),
							DefaultValue = null,
							Getter = (instance) => "<" + (((DummyJsonClass)instance).Name ?? "nobody") + ">",
							Setter = (instance, value) => { ((DummyJsonClass)instance).Name = ((value as string) ?? String.Empty).Replace("<","").Replace(">",""); },
							Binder = binder,
							Visitor = CrystalJsonVisitor.GetVisitorForType(typeof(string))
						},
						new CrystalJsonMemberDefinition()
						{
							Name = "Narf",
							Type = typeof(int),
							DefaultValue = 42, // non-standard default value !
							Getter = (instance) => 42 + ((DummyJsonClass)instance).Index,
							Setter = (instance, value) => { ((DummyJsonClass)instance).Index = ((int)value) - 42; },
							Binder = binder,
							Visitor = CrystalJsonVisitor.GetVisitorForType(typeof(int))
						}
					}
			);
		}

	}

#endregion

}
