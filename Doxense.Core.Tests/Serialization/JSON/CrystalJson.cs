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

// ReSharper disable RedundantExplicitArrayCreation
// ReSharper disable UseArrayEmptyMethod
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
// ReSharper disable RedundantTypeArgumentsOfMethod
// ReSharper disable HeapView.BoxingAllocation
// ReSharper disable HeapView.DelegateAllocation
// ReSharper disable HeapView.ClosureAllocation
// ReSharper disable RedundantCast
// ReSharper disable HeapView.ObjectAllocation
// ReSharper disable AssignNullToNotNullAttribute
// ReSharper disable CompareOfFloatsByEqualityOperator
// ReSharper disable PossibleMultipleEnumeration
// ReSharper disable AccessToModifiedClosure
// ReSharper disable RedundantNameQualifier
// ReSharper disable ConvertClosureToMethodGroup
// ReSharper disable RedundantExplicitParamsArrayCreation
// ReSharper disable ConvertToAutoPropertyWhenPossible
// ReSharper disable FieldCanBeMadeReadOnly.Local
// ReSharper disable ConvertToConstant.Local
// ReSharper disable ConvertToAutoProperty
// ReSharper disable VariableLengthStringHexEscapeSequence
// ReSharper disable StringLiteralTypo
// ReSharper disable IdentifierTypo
// ReSharper disable PreferConcreteValueOverDefault

#pragma warning disable JSON001
#pragma warning disable IDE0044
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
#pragma warning disable CS8714 // The type cannot be used as type parameter in the generic type or method. Nullability of type argument doesn't match 'notnull' constraint.
#pragma warning disable CA1861 // Avoid constant arrays as arguments
#pragma warning disable 618
#pragma warning disable NUnit2009
#pragma warning disable NUnit2021
#pragma warning disable NUnit2050

namespace Doxense.Serialization.Json.Tests
{
	using System;
	using System.Collections.Generic;
	using System.Collections.ObjectModel;
	using System.Diagnostics;
	using System.Globalization;
	using System.IO;
	using System.IO.Compression;
	using System.Linq;
	using System.Linq.Expressions;
	using System.Net;
	using System.Runtime.CompilerServices;
	using System.Runtime.Serialization;
	using System.Text;
	using System.Threading;
	using System.Threading.Tasks;
	using System.Xml.Serialization;
	using Doxense.Collections.Tuples;
	using Doxense.Memory;
	using Doxense.Runtime.Converters;
	using NodaTime;
	using NUnit.Framework.Constraints;

	using STJ = System.Text.Json;
	using NJ = Newtonsoft.Json;

	[TestFixture]
	[Category("Core-SDK")]
	[Category("Core-JSON")]
	[Parallelizable(ParallelScope.All)]
	[SetInvariantCulture]
	public class CrystalJsonTest : SimpleTest
	{

		#region Settings...

		[Test]
		public void Test_JsonSettings_DefaultValues()
		{
			// By default, should have mostly default values for the properties (0 / false)

			var settings = CrystalJsonSettings.Json;
			Assert.That(settings.TargetLanguage, Is.EqualTo(CrystalJsonSettings.Target.Json));
			Assert.That(settings.IsJsonTarget(), Is.True);
			Assert.That(settings.IsJavascriptTarget(), Is.False);
			Assert.That(settings.TextLayout, Is.EqualTo(CrystalJsonSettings.Layout.Formatted));
			Assert.That(settings.IsCompactLayout(), Is.False);
			Assert.That(settings.IsFormattedLayout(), Is.True);
			Assert.That(settings.IsIndentedLayout(), Is.False);
			Assert.That(settings.DateFormatting, Is.EqualTo(CrystalJsonSettings.DateFormat.Default));
			Assert.That(settings.InterningMode, Is.EqualTo(CrystalJsonSettings.StringInterning.Default));
			Assert.That(settings.HideDefaultValues, Is.False);
			Assert.That(settings.ShowNullMembers, Is.False);
			Assert.That(settings.UseCamelCasingForNames, Is.False);
			Assert.That(settings.OptimizeForLargeData, Is.False);
			Assert.That(settings.ReadOnly, Is.False);
			Assert.That(settings.Flags, Is.EqualTo(CrystalJsonSettings.OptionFlags.None));
			Assert.That(CrystalJsonSettings.Json, Is.SameAs(settings));

			// JsonImmutable
			settings = CrystalJsonSettings.JsonReadOnly;
			Assert.That(settings.TargetLanguage, Is.EqualTo(CrystalJsonSettings.Target.Json));
			Assert.That(settings.IsJsonTarget(), Is.True);
			Assert.That(settings.IsJavascriptTarget(), Is.False);
			Assert.That(settings.TextLayout, Is.EqualTo(CrystalJsonSettings.Layout.Formatted));
			Assert.That(settings.IsCompactLayout(), Is.False);
			Assert.That(settings.IsFormattedLayout(), Is.True);
			Assert.That(settings.IsIndentedLayout(), Is.False);
			Assert.That(settings.DateFormatting, Is.EqualTo(CrystalJsonSettings.DateFormat.Default));
			Assert.That(settings.InterningMode, Is.EqualTo(CrystalJsonSettings.StringInterning.Default));
			Assert.That(settings.HideDefaultValues, Is.False);
			Assert.That(settings.ShowNullMembers, Is.False);
			Assert.That(settings.UseCamelCasingForNames, Is.False);
			Assert.That(settings.OptimizeForLargeData, Is.False);
			Assert.That(settings.ReadOnly, Is.True);
			Assert.That(settings.Flags, Is.EqualTo(CrystalJsonSettings.OptionFlags.Mutability_ReadOnly));
			Assert.That(CrystalJsonSettings.JsonReadOnly, Is.SameAs(settings));

			// JsonIndented: Only the TextLayout must be different

			settings = CrystalJsonSettings.JsonIndented;
			Assert.That(settings.TargetLanguage, Is.EqualTo(CrystalJsonSettings.Target.Json));
			Assert.That(settings.IsJsonTarget(), Is.True);
			Assert.That(settings.IsJavascriptTarget(), Is.False);
			Assert.That(settings.TextLayout, Is.EqualTo(CrystalJsonSettings.Layout.Indented));
			Assert.That(settings.IsCompactLayout(), Is.False);
			Assert.That(settings.IsFormattedLayout(), Is.False);
			Assert.That(settings.IsIndentedLayout(), Is.True);
			Assert.That(settings.DateFormatting, Is.EqualTo(CrystalJsonSettings.DateFormat.Default));
			Assert.That(settings.InterningMode, Is.EqualTo(CrystalJsonSettings.StringInterning.Default));
			Assert.That(settings.HideDefaultValues, Is.False);
			Assert.That(settings.ShowNullMembers, Is.False);
			Assert.That(settings.UseCamelCasingForNames, Is.False);
			Assert.That(settings.OptimizeForLargeData, Is.False);
			Assert.That(settings.Flags, Is.EqualTo(CrystalJsonSettings.OptionFlags.Layout_Indented));
			Assert.That(CrystalJsonSettings.JsonIndented, Is.SameAs(settings));

			// JsonCompact: Only the TextLayout must be different

			settings = CrystalJsonSettings.JsonCompact;
			Assert.That(settings.TargetLanguage, Is.EqualTo(CrystalJsonSettings.Target.Json));
			Assert.That(settings.IsJsonTarget(), Is.True);
			Assert.That(settings.IsJavascriptTarget(), Is.False);
			Assert.That(settings.TextLayout, Is.EqualTo(CrystalJsonSettings.Layout.Compact));
			Assert.That(settings.IsCompactLayout(), Is.True);
			Assert.That(settings.IsFormattedLayout(), Is.False);
			Assert.That(settings.IsIndentedLayout(), Is.False);
			Assert.That(settings.DateFormatting, Is.EqualTo(CrystalJsonSettings.DateFormat.Default));
			Assert.That(settings.InterningMode, Is.EqualTo(CrystalJsonSettings.StringInterning.Default));
			Assert.That(settings.HideDefaultValues, Is.False);
			Assert.That(settings.ShowNullMembers, Is.False);
			Assert.That(settings.UseCamelCasingForNames, Is.False);
			Assert.That(settings.OptimizeForLargeData, Is.False);
			Assert.That(settings.Flags, Is.EqualTo(CrystalJsonSettings.OptionFlags.Layout_Compact));
			Assert.That(CrystalJsonSettings.JsonCompact, Is.SameAs(settings));

			// JavaScript: should target the JavaScript language (single quotes, dates with the form "new Date(...)")

			settings = CrystalJsonSettings.JavaScript;
			Assert.That(settings.TargetLanguage, Is.EqualTo(CrystalJsonSettings.Target.JavaScript));
			Assert.That(settings.IsJsonTarget(), Is.False);
			Assert.That(settings.IsJavascriptTarget(), Is.True);
			Assert.That(settings.TextLayout, Is.EqualTo(CrystalJsonSettings.Layout.Formatted));
			Assert.That(settings.IsCompactLayout(), Is.False);
			Assert.That(settings.IsFormattedLayout(), Is.True);
			Assert.That(settings.IsIndentedLayout(), Is.False);
			Assert.That(settings.DateFormatting, Is.EqualTo(CrystalJsonSettings.DateFormat.Default));
			Assert.That(settings.InterningMode, Is.EqualTo(CrystalJsonSettings.StringInterning.Default));
			Assert.That(settings.HideDefaultValues, Is.False);
			Assert.That(settings.ShowNullMembers, Is.False);
			Assert.That(settings.UseCamelCasingForNames, Is.False);
			Assert.That(settings.OptimizeForLargeData, Is.False);
			Assert.That(settings.Flags, Is.EqualTo(CrystalJsonSettings.OptionFlags.Target_JavaScript));
			Assert.That(CrystalJsonSettings.JavaScript, Is.SameAs(settings));

			// JavaScriptIndented: same as JavaScript, but with a different TextLayout

			settings = CrystalJsonSettings.JavaScriptIndented;
			Assert.That(settings.TargetLanguage, Is.EqualTo(CrystalJsonSettings.Target.JavaScript));
			Assert.That(settings.IsJsonTarget(), Is.False);
			Assert.That(settings.IsJavascriptTarget(), Is.True);
			Assert.That(settings.TextLayout, Is.EqualTo(CrystalJsonSettings.Layout.Indented));
			Assert.That(settings.IsCompactLayout(), Is.False);
			Assert.That(settings.IsFormattedLayout(), Is.False);
			Assert.That(settings.IsIndentedLayout(), Is.True);
			Assert.That(settings.DateFormatting, Is.EqualTo(CrystalJsonSettings.DateFormat.Default));
			Assert.That(settings.InterningMode, Is.EqualTo(CrystalJsonSettings.StringInterning.Default));
			Assert.That(settings.HideDefaultValues, Is.False);
			Assert.That(settings.ShowNullMembers, Is.False);
			Assert.That(settings.UseCamelCasingForNames, Is.False);
			Assert.That(settings.OptimizeForLargeData, Is.False);
			Assert.That(settings.Flags, Is.EqualTo(CrystalJsonSettings.OptionFlags.Target_JavaScript | CrystalJsonSettings.OptionFlags.Layout_Indented));
			Assert.That(CrystalJsonSettings.JavaScriptIndented, Is.SameAs(settings));

			// null
			Assert.That(default(CrystalJsonSettings).IsJsonTarget(), Is.True);
			Assert.That(default(CrystalJsonSettings).IsJavascriptTarget(), Is.False);
			Assert.That(default(CrystalJsonSettings).IsCompactLayout(), Is.False);
			Assert.That(default(CrystalJsonSettings).IsFormattedLayout(), Is.True);
			Assert.That(default(CrystalJsonSettings).IsIndentedLayout(), Is.False);

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

			// should be now back to the default settings
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

			// Should not override the other settings
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

			// Should not override the other settings
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

			// Should not override the other settings
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

		private static Slice SerializeToSlice(JsonValue value)
		{
			var writer = new SliceWriter();
			value.WriteTo(ref writer);
			return writer.ToSlice();
		}

		[Test]
		public void Test_CrystalJsonWriter_WriteValue()
		{
			static string Execute(Action<CrystalJsonWriter> handler, CrystalJsonSettings? settings = null)
			{
				using var writer = new CrystalJsonWriter(0, settings ?? CrystalJsonSettings.Json, CrystalJson.DefaultResolver);
				handler(writer);
				var json = writer.GetString();
				Log(json);
				return json;
			}

			#region String-like

			// WriteValue(string)
			Assert.That(Execute((w) => w.WriteValue(default(string))), Is.EqualTo("null"));
			Assert.That(Execute((w) => w.WriteValue("")), Is.EqualTo(@""""""));
			Assert.That(Execute((w) => w.WriteValue("Hello, World!")), Is.EqualTo(@"""Hello, World!"""));
			Assert.That(Execute((w) => w.WriteValue("Hello, \"World\"!")), Is.EqualTo(@"""Hello, \""World\""!"""));
			Assert.That(Execute((w) => w.WriteValue("\\o/")), Is.EqualTo(@"""\\o/"""));
			// WriteValue(StringBuilder)
			Assert.That(Execute((w) => w.WriteValue(default(StringBuilder))), Is.EqualTo("null"));
			Assert.That(Execute((w) => w.WriteValue(new StringBuilder())), Is.EqualTo(@""""""));
			Assert.That(Execute((w) => w.WriteValue(new StringBuilder().Append("Hello, ").Append("World!"))), Is.EqualTo(@"""Hello, World!"""));
			Assert.That(Execute((w) => w.WriteValue(new StringBuilder().Append("Hello, ").Append("\"World\"!"))), Is.EqualTo(@"""Hello, \""World\""!"""));
			// WriteValue(ReadOnlySpan<char>)
			Assert.That(Execute((w) => w.WriteValue(default(ReadOnlySpan<char>))), Is.EqualTo(@""""""));
			Assert.That(Execute((w) => w.WriteValue("***Hello, World!***".AsSpan(3, 13))), Is.EqualTo(@"""Hello, World!"""));
			Assert.That(Execute((w) => w.WriteValue("***Hello, \"World\"!***".AsSpan(3, 15))), Is.EqualTo(@"""Hello, \""World\""!"""));
			// WriteValue(ReadOnlyMemory<char>)
			Assert.That(Execute((w) => w.WriteValue(default(ReadOnlyMemory<char>))), Is.EqualTo(@""""""));
			Assert.That(Execute((w) => w.WriteValue("***Hello, World!***".AsMemory(3, 13))), Is.EqualTo(@"""Hello, World!"""));
			Assert.That(Execute((w) => w.WriteValue("***Hello, \"World\"!***".AsMemory(3, 15))), Is.EqualTo(@"""Hello, \""World\""!"""));

			// WriteValue(Guid)
			Assert.That(Execute((w) => w.WriteValue(default(Guid))), Is.EqualTo("null")); // Guid.Empty is mapped to null/missing
			Assert.That(Execute((w) => w.WriteValue(Guid.Parse("8d6643a7-a84d-4eab-8394-0b349798bee2"))), Is.EqualTo(@"""8d6643a7-a84d-4eab-8394-0b349798bee2"""));
			Assert.That(Execute((w) => w.WriteValue((Guid?) Guid.Parse("8d6643a7-a84d-4eab-8394-0b349798bee2"))), Is.EqualTo(@"""8d6643a7-a84d-4eab-8394-0b349798bee2"""));

			#endregion

			#region Booleans...

			Assert.That(Execute(w => w.WriteValue(true)), Is.EqualTo("true"));
			Assert.That(Execute(w => w.WriteValue(false)), Is.EqualTo("false"));
			Assert.That(Execute(w => w.WriteValue(default(bool?))), Is.EqualTo("null"));
			Assert.That(Execute(w => w.WriteValue((bool?) true)), Is.EqualTo("true"));
			Assert.That(Execute(w => w.WriteValue((bool?) false)), Is.EqualTo("false"));

			#endregion

			#region Numbers...

			Assert.That(Execute(w => w.WriteValue(0)), Is.EqualTo("0"));
			Assert.That(Execute(w => w.WriteValue(1)), Is.EqualTo("1"));
			Assert.That(Execute(w => w.WriteValue(10)), Is.EqualTo("10"));
			Assert.That(Execute(w => w.WriteValue(-1)), Is.EqualTo("-1"));
			Assert.That(Execute(w => w.WriteValue(42)), Is.EqualTo("42"));
			Assert.That(Execute(w => w.WriteValue(42U)), Is.EqualTo("42"));
			Assert.That(Execute(w => w.WriteValue(42L)), Is.EqualTo("42"));
			Assert.That(Execute(w => w.WriteValue(42UL)), Is.EqualTo("42"));
			Assert.That(Execute(w => w.WriteValue((byte) 42)), Is.EqualTo("42"));
			Assert.That(Execute(w => w.WriteValue((short) 42)), Is.EqualTo("42"));

			Assert.That(Execute(w => w.WriteValue(-42)), Is.EqualTo("-42"));
			Assert.That(Execute(w => w.WriteValue(-42L)), Is.EqualTo("-42"));
			Assert.That(Execute(w => w.WriteValue((sbyte) -42)), Is.EqualTo("-42"));
			Assert.That(Execute(w => w.WriteValue((short) -42)), Is.EqualTo("-42"));

			Assert.That(Execute(w => w.WriteValue(1234)), Is.EqualTo("1234"));
			Assert.That(Execute(w => w.WriteValue(1234U)), Is.EqualTo("1234"));
			Assert.That(Execute(w => w.WriteValue(1234L)), Is.EqualTo("1234"));
			Assert.That(Execute(w => w.WriteValue(1234UL)), Is.EqualTo("1234"));

			Assert.That(Execute(w => w.WriteValue(-1234)), Is.EqualTo("-1234"));
			Assert.That(Execute(w => w.WriteValue(-1234L)), Is.EqualTo("-1234"));

			Assert.That(Execute(w => w.WriteValue(int.MaxValue)), Is.EqualTo("2147483647"));
			Assert.That(Execute(w => w.WriteValue(int.MinValue)), Is.EqualTo("-2147483648"));
			Assert.That(Execute(w => w.WriteValue(uint.MaxValue)), Is.EqualTo("4294967295"));
			Assert.That(Execute(w => w.WriteValue(long.MaxValue)), Is.EqualTo("9223372036854775807"));
			Assert.That(Execute(w => w.WriteValue(long.MinValue)), Is.EqualTo("-9223372036854775808"));

			Assert.That(Execute(w => w.WriteValue(1f)), Is.EqualTo("1"));
			Assert.That(Execute(w => w.WriteValue((float) Math.PI)), Is.EqualTo("3.1415927"));

			Assert.That(Execute(w => w.WriteValue(1d)), Is.EqualTo("1"));
			Assert.That(Execute(w => w.WriteValue(Math.PI)), Is.EqualTo("3.141592653589793"));
			Assert.That(Execute(w => w.WriteValue(double.MinValue)), Is.EqualTo("-1.7976931348623157E+308"));
			Assert.That(Execute(w => w.WriteValue(double.MaxValue)), Is.EqualTo("1.7976931348623157E+308"));
			Assert.That(Execute(w => w.WriteValue(double.Epsilon)), Is.EqualTo("5E-324"));

			Assert.That(Execute(w => w.WriteValue(default(int?))), Is.EqualTo("null"));
			Assert.That(Execute(w => w.WriteValue((int?) 42)), Is.EqualTo("42"));
			Assert.That(Execute(w => w.WriteValue(default(uint?))), Is.EqualTo("null"));
			Assert.That(Execute(w => w.WriteValue((uint?) 42)), Is.EqualTo("42"));
			Assert.That(Execute(w => w.WriteValue(default(long?))), Is.EqualTo("null"));
			Assert.That(Execute(w => w.WriteValue((long?) 42)), Is.EqualTo("42"));
			Assert.That(Execute(w => w.WriteValue(default(ulong?))), Is.EqualTo("null"));
			Assert.That(Execute(w => w.WriteValue((ulong?) 42)), Is.EqualTo("42"));
			Assert.That(Execute(w => w.WriteValue(default(float?))), Is.EqualTo("null"));
			Assert.That(Execute(w => w.WriteValue((float?) 42)), Is.EqualTo("42"));
			Assert.That(Execute(w => w.WriteValue(default(double?))), Is.EqualTo("null"));
			Assert.That(Execute(w => w.WriteValue((double?) 42)), Is.EqualTo("42"));
			Assert.That(Execute(w => w.WriteValue(default(decimal?))), Is.EqualTo("null"));
			Assert.That(Execute(w => w.WriteValue((decimal?) 42)), Is.EqualTo("42"));

			Assert.That(Execute(w => w.WriteEnumInteger(DummyJsonEnumInt32.Two)), Is.EqualTo("2"));
			Assert.That(Execute(w => w.WriteEnumString(DummyJsonEnumInt32.Two)), Is.EqualTo("\"Two\""));
			Assert.That(Execute(w => w.WriteEnumInteger(DummyJsonEnumShort.Two)), Is.EqualTo("2"));
			Assert.That(Execute(w => w.WriteEnumString(DummyJsonEnumShort.Two)), Is.EqualTo("\"Two\""));
			Assert.That(Execute(w => w.WriteEnumInteger(DummyJsonEnumInt64.Two)), Is.EqualTo("2"));
			Assert.That(Execute(w => w.WriteEnumString(DummyJsonEnumInt64.Two)), Is.EqualTo("\"Two\""));

#if NET8_0_OR_GREATER
			Assert.That(Execute(w => w.WriteValue((Half) Math.PI)), Is.EqualTo("3.14"));
			Assert.That(Execute(w => w.WriteValue(default(Half?))), Is.EqualTo("null"));
			Assert.That(Execute(w => w.WriteValue((Half?) 42)), Is.EqualTo("42"));
#endif

			#endregion
		}

		[Test]
		public void Test_CrystalJsonWriter_WriteField()
		{
			static string Execute(Action<CrystalJsonWriter> handler, CrystalJsonSettings? settings = null)
			{
				using var writer = new CrystalJsonWriter(0, settings ?? CrystalJsonSettings.Json, CrystalJson.DefaultResolver);
				var state = writer.BeginObject();
				handler(writer);
				writer.EndObject(state);
				return writer.GetString();
			}

			#region String-like

			// string
			Assert.That(Execute((w) => w.WriteField("foo", default(string))), Is.EqualTo("{ }"));
			Assert.That(Execute((w) => w.WriteField("foo", "")), Is.EqualTo(@"{ ""foo"": """" }"));
			Assert.That(Execute((w) => w.WriteField("foo", "Hello, World!")), Is.EqualTo(@"{ ""foo"": ""Hello, World!"" }"));
			Assert.That(Execute((w) => w.WriteField("foo", "Hello, \"World\"!")), Is.EqualTo(@"{ ""foo"": ""Hello, \""World\""!"" }"));
			Assert.That(Execute((w) => w.WriteField("foo", "\\o/")), Is.EqualTo(@"{ ""foo"": ""\\o/"" }"));
			// StringBuilder
			Assert.That(Execute((w) => w.WriteField("foo", default(StringBuilder))), Is.EqualTo("{ }"));
			Assert.That(Execute((w) => w.WriteField("foo", new StringBuilder())), Is.EqualTo(@"{ ""foo"": """" }"));
			Assert.That(Execute((w) => w.WriteField("foo", new StringBuilder().Append("Hello, ").Append("World!"))), Is.EqualTo(@"{ ""foo"": ""Hello, World!"" }"));
			Assert.That(Execute((w) => w.WriteField("foo", new StringBuilder().Append("Hello, ").Append("\"World\"!"))), Is.EqualTo(@"{ ""foo"": ""Hello, \""World\""!"" }"));
			// ReadOnlySpan<char>
			Assert.That(Execute((w) => w.WriteField("foo", default(ReadOnlySpan<char>))), Is.EqualTo(@"{ ""foo"": """" }"));
			Assert.That(Execute((w) => w.WriteField("foo", "***Hello, World!***".AsSpan(3, 13))), Is.EqualTo(@"{ ""foo"": ""Hello, World!"" }"));
			Assert.That(Execute((w) => w.WriteField("foo", "***Hello, \"World\"!***".AsSpan(3, 15))), Is.EqualTo(@"{ ""foo"": ""Hello, \""World\""!"" }"));
			// ReadOnlyMemory<char>
			Assert.That(Execute((w) => w.WriteField("foo", default(ReadOnlyMemory<char>))), Is.EqualTo(@"{ ""foo"": """" }"));
			Assert.That(Execute((w) => w.WriteField("foo", "***Hello, World!***".AsMemory(3, 13))), Is.EqualTo(@"{ ""foo"": ""Hello, World!"" }"));
			Assert.That(Execute((w) => w.WriteField("foo", "***Hello, \"World\"!***".AsMemory(3, 15))), Is.EqualTo(@"{ ""foo"": ""Hello, \""World\""!"" }"));
			// Guid
			Assert.That(Execute((w) => w.WriteField("foo", default(Guid))), Is.EqualTo(@"{ ""foo"": null }")); // Guid.Empty is mapped to null/missing
			Assert.That(Execute((w) => w.WriteField("foo", Guid.Parse("8d6643a7-a84d-4eab-8394-0b349798bee2"))), Is.EqualTo(@"{ ""foo"": ""8d6643a7-a84d-4eab-8394-0b349798bee2"" }"));
			Assert.That(Execute((w) => w.WriteField("foo", (Guid?) Guid.Parse("8d6643a7-a84d-4eab-8394-0b349798bee2"))), Is.EqualTo(@"{ ""foo"": ""8d6643a7-a84d-4eab-8394-0b349798bee2"" }"));

			#endregion

			#region Booleans...

			Assert.That(Execute(w => w.WriteField("foo", true)), Is.EqualTo(@"{ ""foo"": true }"));
			Assert.That(Execute(w => w.WriteField("foo", false)), Is.EqualTo(@"{ ""foo"": false }"));
			Assert.That(Execute(w => w.WriteField("foo", default(bool?))), Is.EqualTo("{ }"));
			Assert.That(Execute(w => w.WriteField("foo", (bool?) true)), Is.EqualTo(@"{ ""foo"": true }"));
			Assert.That(Execute(w => w.WriteField("foo", (bool?) false)), Is.EqualTo(@"{ ""foo"": false }"));

			#endregion

			#region Numbers...

			Assert.That(Execute(w => w.WriteField("foo", 0)), Is.EqualTo(@"{ ""foo"": 0 }"));
			Assert.That(Execute(w => w.WriteField("foo", 1)), Is.EqualTo(@"{ ""foo"": 1 }"));
			Assert.That(Execute(w => w.WriteField("foo", 42)), Is.EqualTo(@"{ ""foo"": 42 }"));
			Assert.That(Execute(w => w.WriteField("foo", 42U)), Is.EqualTo(@"{ ""foo"": 42 }"));
			Assert.That(Execute(w => w.WriteField("foo", 42L)), Is.EqualTo(@"{ ""foo"": 42 }"));
			Assert.That(Execute(w => w.WriteField("foo", 42UL)), Is.EqualTo(@"{ ""foo"": 42 }"));
			Assert.That(Execute(w => w.WriteField("foo", (byte) 42)), Is.EqualTo(@"{ ""foo"": 42 }"));
			Assert.That(Execute(w => w.WriteField("foo", (short) 42)), Is.EqualTo(@"{ ""foo"": 42 }"));
			Assert.That(Execute(w => w.WriteField("foo", int.MaxValue)), Is.EqualTo(@"{ ""foo"": 2147483647 }"));
			Assert.That(Execute(w => w.WriteField("foo", int.MinValue)), Is.EqualTo(@"{ ""foo"": -2147483648 }"));
			Assert.That(Execute(w => w.WriteField("foo", uint.MaxValue)), Is.EqualTo(@"{ ""foo"": 4294967295 }"));
			Assert.That(Execute(w => w.WriteField("foo", long.MaxValue)), Is.EqualTo(@"{ ""foo"": 9223372036854775807 }"));
			Assert.That(Execute(w => w.WriteField("foo", long.MinValue)), Is.EqualTo(@"{ ""foo"": -9223372036854775808 }"));
			Assert.That(Execute(w => w.WriteField("foo", Math.PI)), Is.EqualTo(@"{ ""foo"": 3.141592653589793 }"));
			Assert.That(Execute(w => w.WriteField("foo", (float) Math.PI)), Is.EqualTo(@"{ ""foo"": 3.1415927 }"));

			Assert.That(Execute(w => w.WriteField("foo", default(int?))), Is.EqualTo("{ }"));
			Assert.That(Execute(w => w.WriteField("foo", (int?) 42)), Is.EqualTo(@"{ ""foo"": 42 }"));
			Assert.That(Execute(w => w.WriteField("foo", default(uint?))), Is.EqualTo("{ }"));
			Assert.That(Execute(w => w.WriteField("foo", (uint?) 42)), Is.EqualTo(@"{ ""foo"": 42 }"));
			Assert.That(Execute(w => w.WriteField("foo", default(long?))), Is.EqualTo("{ }"));
			Assert.That(Execute(w => w.WriteField("foo", (long?) 42)), Is.EqualTo(@"{ ""foo"": 42 }"));
			Assert.That(Execute(w => w.WriteField("foo", default(ulong?))), Is.EqualTo("{ }"));
			Assert.That(Execute(w => w.WriteField("foo", (ulong?) 42)), Is.EqualTo(@"{ ""foo"": 42 }"));
			Assert.That(Execute(w => w.WriteField("foo", default(float?))), Is.EqualTo("{ }"));
			Assert.That(Execute(w => w.WriteField("foo", (float?) 42)), Is.EqualTo(@"{ ""foo"": 42 }"));
			Assert.That(Execute(w => w.WriteField("foo", default(double?))), Is.EqualTo("{ }"));
			Assert.That(Execute(w => w.WriteField("foo", (double?) 42)), Is.EqualTo(@"{ ""foo"": 42 }"));
			Assert.That(Execute(w => w.WriteField("foo", default(decimal?))), Is.EqualTo("{ }"));
			Assert.That(Execute(w => w.WriteField("foo", (decimal?) 42)), Is.EqualTo(@"{ ""foo"": 42 }"));
			Assert.That(Execute(w => w.WriteField("foo", default(Half?))), Is.EqualTo("{ }"));

#if NET8_0_OR_GREATER
			Assert.That(Execute(w => w.WriteField("foo", (Half) Math.PI)), Is.EqualTo(@"{ ""foo"": 3.14 }"));
			Assert.That(Execute(w => w.WriteField("foo", (Half?) 42)), Is.EqualTo(@"{ ""foo"": 42 }"));
#endif

			#endregion

			#region Combo...

			Assert.That(Execute((w) => { }), Is.EqualTo("{ }"));

			Assert.That(Execute((w) =>
			{
				w.WriteField("foo", "hello");
				w.WriteField("bar", 123);
				w.WriteField("baz", true);
				w.WriteField("missing", default(string));
			}), Is.EqualTo(@"{ ""foo"": ""hello"", ""bar"": 123, ""baz"": true }"));

			#endregion
		}

		static void CheckSerialize<T>(
			T? value,
			CrystalJsonSettings? settings,
			string expected,
			string? message = null,
			[CallerArgumentExpression(nameof(value))] string valueExpression = "",
			[CallerArgumentExpression(nameof(settings))] string settingsExpression = "",
			[CallerArgumentExpression(nameof(expected))] string constraintExpression = ""
		)
		{
			settings ??= CrystalJsonSettings.Json;

			string? expr = value?.ToString();
			if (string.IsNullOrEmpty(expr))
			{
				expr = valueExpression;
			}

			if (value is IFormattable fmt)
			{
				Log($"# <{settings}> {typeof(T).GetFriendlyName()}: {fmt}");
			}
			else
			{
				Log($"# <{settings}> {typeof(T).GetFriendlyName()}");
			}

			var expectedSlice = Slice.FromStringUtf8(expected);

			{ // CrystalJson.Serialize<T>
				string actual = CrystalJson.Serialize<T>(value, settings);
				Log($"> {actual}");
				if (actual != expected)
				{
					Assert.That(actual, Is.EqualTo(expected), message, actualExpression: $"{nameof(CrystalJson)}.{nameof(CrystalJson.Serialize)}({expr}, {settingsExpression})", constraintExpression: "Is.EqualTo(\"\"\"" + expected + "\"\"\")");
				}
			}

			{ // CrystalJson.Serialize(object, Type)
				string actual = CrystalJson.Serialize(value, typeof(T), settings);
				if (actual != expected)
				{
					Assert.That(actual, Is.EqualTo(expected), message, actualExpression: $"{nameof(CrystalJson)}.{nameof(CrystalJson.Serialize)}({expr}, {settingsExpression})", constraintExpression: "Is.EqualTo(\"\"\"" + expected + "\"\"\")");
				}
			}

			{ // CrystalJson.ToSlice<T>
				Slice actualSlice = CrystalJson.ToSlice<T>(value, settings);
				if (!expectedSlice.Equals(actualSlice))
				{
					Assert.That(actualSlice.ToArray(), Is.EqualTo(expectedSlice.ToArray()), message, actualExpression: $"{nameof(CrystalJson)}.{nameof(CrystalJson.ToSlice)}({expr}, {settingsExpression})", constraintExpression: "Is.EqualTo(\"\"\"" + expected + "\"\"\")");
				}
			}

			{ // CrystalJsonWriter.VisitValue<T>
				using var sw = new StringWriter();
				{
					using (var writer = new CrystalJsonWriter(sw, 0, settings, CrystalJson.DefaultResolver))
					{
						writer.VisitValue(value);
					}
					string actual = sw.ToString();
					if (actual != expected)
					{
						Assert.That(actual, Is.EqualTo(expected), message, actualExpression: $"(writer) => writer.{nameof(CrystalJsonWriter.VisitValue)}({expr})", constraintExpression: "Is.EqualTo(\"\"\"" + expected + "\"\"\")");
					}
				}
			}

			{ // Output to TextWriter
				using var sw = new StringWriter();
				CrystalJson.SerializeTo(sw, value, settings);
				string actual = sw.ToString();
				if (actual != expected)
				{
					Assert.That(actual, Is.EqualTo(expected), message, actualExpression: $"{nameof(CrystalJson)}.{nameof(CrystalJson.SerializeTo)}(StringWriter, {expr}, {settingsExpression})", constraintExpression: "Is.EqualTo(\"\"\"" + expected + "\"\"\")");
				}
			}

			{ // Output to Stream
				using var ms = new MemoryStream();
				CrystalJson.SerializeTo(ms, value, settings);
				var actualStream = ms.ToArray();
				if (!expectedSlice.Equals(actualStream))
				{
					Assert.That(actualStream, Is.EqualTo(expectedSlice.ToArray()), message, actualExpression: $"{nameof(CrystalJson)}.{nameof(CrystalJson.SerializeTo)}(MemoryStream, {expr}, {settingsExpression})", constraintExpression: "Is.EqualTo(\"\"\"" + expected + "\"\"\")");
				}
			}

		}

		[Test]
		public void Test_JsonSerialize_Null()
		{
			CheckSerialize<string>(null, default, "null");
			CheckSerialize<string>(null, CrystalJsonSettings.Json, "null");
			CheckSerialize<string>(null, CrystalJsonSettings.JsonCompact, "null");
		}

		[Test]
		public void Test_JsonSerialize_String_Types()
		{
			// trust, but verify...
			Assume.That(typeof(string).IsPrimitive, Is.False);

			// string
			CheckSerialize(string.Empty, default, "\"\"");
			CheckSerialize("foo", default, "\"foo\"");
			CheckSerialize("foo\"bar", default, "\"foo\\\"bar\"");
			CheckSerialize("foo'bar", default, "\"foo'bar\"");

			// StringBuilder
			CheckSerialize(new StringBuilder(), default, "\"\"");
			CheckSerialize(new StringBuilder("Foo"), default, "\"Foo\"");
			CheckSerialize(new StringBuilder("Foo").Append('"').Append("Bar"), default, "\"Foo\\\"Bar\"");
			CheckSerialize(new StringBuilder("Foo").Append('\'').Append("Bar"), default, "\"Foo'Bar\"");
		}

		[Test]
		public void Test_JavaScriptSerialize_String_Types()
		{
			// trust, but verify
			Assume.That(typeof(string).IsPrimitive, Is.False);

			// string
			CheckSerialize(string.Empty, CrystalJsonSettings.JavaScript, "''");
			CheckSerialize("foo", CrystalJsonSettings.JavaScript, "'foo'");
			CheckSerialize("foo\"bar", CrystalJsonSettings.JavaScript, "'foo\\x22bar'");
			CheckSerialize("foo'bar", CrystalJsonSettings.JavaScript, "'foo\\x27bar'");

			// StringBuilder
			CheckSerialize(new StringBuilder(), CrystalJsonSettings.JavaScript, "''");
			CheckSerialize(new StringBuilder("Foo"), CrystalJsonSettings.JavaScript, "'Foo'");
			CheckSerialize(new StringBuilder("Foo").Append('"').Append("Bar"), CrystalJsonSettings.JavaScript, "'Foo\\x22Bar'");
			CheckSerialize(new StringBuilder("Foo").Append('\'').Append("Bar"), CrystalJsonSettings.JavaScript, "'Foo\\x27Bar'");
		}

		[Test]
		public void Test_JsonSerialize_Primitive_Types()
		{
			// boolean
			CheckSerialize(true, default, "true");
			CheckSerialize(false, default, "false");

			// int32
			CheckSerialize((int)0, default, "0");
			CheckSerialize((int)1, default, "1");
			CheckSerialize((int)-1, default, "-1");
			CheckSerialize((int)123, default, "123");
			CheckSerialize((int)-999, default, "-999");
			CheckSerialize(int.MaxValue, default, "2147483647");
			CheckSerialize(int.MinValue, default, "-2147483648");

			// int64
			CheckSerialize((long)0, default, "0");
			CheckSerialize((long)1, default, "1");
			CheckSerialize((long)-1, default, "-1");
			CheckSerialize((long)123, default, "123");
			CheckSerialize((long)-999, default, "-999");
			CheckSerialize(long.MaxValue, default, "9223372036854775807");
			CheckSerialize(long.MinValue, default, "-9223372036854775808");

			// single
			CheckSerialize(0f, default, "0");
			CheckSerialize(1f, default, "1");
			CheckSerialize(-1f, default, "-1");
			CheckSerialize(123f, default, "123");
			CheckSerialize(123.456f, default, "123.456");
			CheckSerialize(-999.9f, default, "-999.9");
			CheckSerialize(float.MaxValue, default, float.MaxValue.ToString("R"));
			CheckSerialize(float.MinValue, default, float.MinValue.ToString("R"));
			CheckSerialize(float.Epsilon, default, float.Epsilon.ToString("R"));
			CheckSerialize(float.NaN, default, "NaN", "Pas standard, mais la plupart des serializers se comportent comme cela");
			CheckSerialize(float.PositiveInfinity, default, "Infinity", "Pas standard, mais la plupart des serializers se comportent comme cela");
			CheckSerialize(float.NegativeInfinity, default, "-Infinity", "Pas standard, mais la plupart des serializers se comportent comme cela");
			{ // NaN => 'NaN'
				var settings = CrystalJsonSettings.Json.WithFloatFormat(CrystalJsonSettings.FloatFormat.Symbol);
				CheckSerialize(float.NaN, settings, "NaN", "Pas standard, mais la plupart des serializers se comportent comme cela");
				CheckSerialize(float.PositiveInfinity, settings, "Infinity", "Pas standard, mais la plupart des serializers se comportent comme cela");
				CheckSerialize(float.NegativeInfinity, settings, "-Infinity", "Pas standard, mais la plupart des serializers se comportent comme cela");
			}
			{ // NaN => '"NaN"'
				var settings = CrystalJsonSettings.Json.WithFloatFormat(CrystalJsonSettings.FloatFormat.String);
				CheckSerialize(float.NaN, settings, "\"NaN\"", "Comme le fait JSON.Net");
				CheckSerialize(float.PositiveInfinity, settings, "\"Infinity\"", "Comme le fait JSON.Net");
				CheckSerialize(float.NegativeInfinity, settings, "\"-Infinity\"", "Comme le fait JSON.Net");
			}
			{ // NaN => 'null'
				var settings = CrystalJsonSettings.Json.WithFloatFormat(CrystalJsonSettings.FloatFormat.Null);
				CheckSerialize(float.NaN, settings, "null", "A défaut d'autre chose...");
				CheckSerialize(float.PositiveInfinity, settings, "null", "A défaut d'autre chose...");
				CheckSerialize(float.NegativeInfinity, settings, "null", "A défaut d'autre chose...");
			}

			// double
			CheckSerialize(0d, default, "0");
			CheckSerialize(1d, default, "1");
			CheckSerialize(-1d, default, "-1");
			CheckSerialize(123d, default, "123");
			CheckSerialize(123.456d, default, "123.456");
			CheckSerialize(-999.9d, default, "-999.9");
			CheckSerialize(double.MaxValue, default, double.MaxValue.ToString("R"));
			CheckSerialize(double.MinValue, default, double.MinValue.ToString("R"));
			CheckSerialize(double.Epsilon, default, double.Epsilon.ToString("R"));
			//BUGBUG: pour l'instant "default" utilise FloatFormat.Symbol mais on risque de changer en String par défaut!
			CheckSerialize(double.NaN, default, "NaN", "Pas standard, mais la plupart des serializers se comportent comme cela");
			CheckSerialize(double.PositiveInfinity, default, "Infinity", "Pas standard, mais la plupart des serializers se comportent comme cela");
			CheckSerialize(double.NegativeInfinity, default, "-Infinity", "Pas standard, mais la plupart des serializers se comportent comme cela");
			{ // NaN => 'NaN'
				var settings = CrystalJsonSettings.Json.WithFloatFormat(CrystalJsonSettings.FloatFormat.Symbol);
				CheckSerialize(double.NaN, settings, "NaN", "Pas standard, mais la plupart des serializers se comportent comme cela");
				CheckSerialize(double.PositiveInfinity, settings, "Infinity", "Pas standard, mais la plupart des serializers se comportent comme cela");
				CheckSerialize(double.NegativeInfinity, settings, "-Infinity", "Pas standard, mais la plupart des serializers se comportent comme cela");
			}
			{ // NaN => '"NaN"'
				var settings = CrystalJsonSettings.Json.WithFloatFormat(CrystalJsonSettings.FloatFormat.String);
				CheckSerialize(double.NaN, settings, "\"NaN\"", "Comme le fait JSON.Net");
				CheckSerialize(double.PositiveInfinity, settings, "\"Infinity\"", "Comme le fait JSON.Net");
				CheckSerialize(double.NegativeInfinity, settings, "\"-Infinity\"", "Comme le fait JSON.Net");
			}
			{ // NaN => 'null'
				var settings = CrystalJsonSettings.Json.WithFloatFormat(CrystalJsonSettings.FloatFormat.Null);
				CheckSerialize(double.NaN, settings, "null"); // by convention
				CheckSerialize(double.PositiveInfinity, settings, "null"); // by convention
				CheckSerialize(double.NegativeInfinity, settings, "null"); // by convention
			}

			// char
			CheckSerialize('A', default, "\"A\"");
			CheckSerialize('\0', default, "null");
			CheckSerialize('\"', default, "\"\\\"\"");

			// JavaScript exceptions:
			CheckSerialize(double.NaN, CrystalJsonSettings.JavaScript, "Number.NaN"); // Not standard, but most serializers behave like this
			CheckSerialize(double.PositiveInfinity, CrystalJsonSettings.JavaScript, "Number.POSITIVE_INFINITY"); // Not standard, but most serializers behave like this
			CheckSerialize(double.NegativeInfinity, CrystalJsonSettings.JavaScript, "Number.NEGATIVE_INFINITY"); // Not standard, but most serializers behave like this

			var rnd = new Random();
			for (int i = 0; i < 1000; i++)
			{
				CheckSerialize(i, default, i.ToString());
				CheckSerialize(-i, default, (-i).ToString());

				int x = rnd.Next() * (rnd.Next(2) == 1 ? -1 : 1);
				CheckSerialize(x, default, x.ToString());
				CheckSerialize((uint) x, default, ((uint) x).ToString());

				long y = (long)x * rnd.Next() * (rnd.Next(2) == 1 ? -1L : 1L);
				CheckSerialize(y, default, y.ToString());
				CheckSerialize((ulong) y, default, ((ulong) y).ToString());
			}
		}

		private static string GetTimeZoneSuffix(DateTime date)
		{
			TimeSpan utcOffset = TimeZoneInfo.Local.GetUtcOffset(date);
			return (utcOffset < TimeSpan.Zero ? "-" : "+") + utcOffset.ToString("hhmm");
		}

		[Test]
		public void Test_JsonValue_ToString_Formatable()
		{
			JsonValue num = 123;
			JsonValue flag = true;
			JsonValue txt = "Hello\"World";
			JsonValue arr = JsonArray.FromValues(Enumerable.Range(1, 20));
			JsonValue obj = JsonObject.Create(
			[
				("Foo", 123),
				("Bar", "Narf Zort!"),
				("Baz", JsonObject.Create([ ("X", 1), ("Y", 2), ("Z", 3) ])),
				("Jazz", JsonArray.FromValues(Enumerable.Range(1, 5)))
			]);

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
			Assert.That(JsonArray.FromValues([ 1, 2, 3, 4 ]).ToString("Q"), Is.EqualTo("[ 1, 2, 3, 4 ]"));
			Assert.That(JsonArray.FromValues([ 1, 2, 3, 4, 5 ]).ToString("Q"), Is.EqualTo("[ 1, 2, 3, 4, 5 ]"));
			Assert.That(JsonArray.FromValues([ 1, 2, 3, 4, 5, 6 ]).ToString("Q"), Is.EqualTo("[ 1, 2, 3, 4, /* … 2 more */ ]"));
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
		public void Test_JsonValue_Required_Of_T()
		{
			//Value Type
			Assert.That(JsonNumber.Return(123).Required<int>(), Is.InstanceOf<int>().And.EqualTo(123));
			Assert.That(JsonString.Return("123").Required<int>(), Is.InstanceOf<int>().And.EqualTo(123));
			Assert.That(() => JsonNull.Null.Required<int>(), Throws.InstanceOf<JsonBindingException>());
			Assert.That(() => JsonNull.Null.Required<int>(null), Throws.InstanceOf<JsonBindingException>());

			//Nullable Type: should induce a compiler warning on '<int?>' since the method can never return null, so expecting a nullable int is probably a mistake ?
			Assert.That(JsonNumber.Return(123).Required<int?>(), Is.InstanceOf<int>().And.EqualTo(123));
			Assert.That(JsonString.Return("123").Required<int?>(), Is.InstanceOf<int>().And.EqualTo(123));
			Assert.That(() => JsonNull.Null.Required<int?>(), Throws.InstanceOf<JsonBindingException>());

			//Reference Primitive Type
			Assert.That(JsonNumber.Return(123).Required<string>(), Is.Not.Null.And.EqualTo("123"));
			Assert.That(JsonString.Return("123").Required<string>(), Is.Not.Null.And.EqualTo("123"));
			Assert.That(() => JsonNull.Null.Required<string>(), Throws.InstanceOf<JsonBindingException>());

			//Value Type Array
			Assert.That(JsonArray.Create(1, 2, 3).Required<int[]>(), Is.Not.Null.And.EqualTo(new [] { 1, 2, 3 }));
			Assert.That(() => JsonNull.Null.Required<int[]>(), Throws.InstanceOf<JsonBindingException>());

			//Ref Type Array
			Assert.That(JsonArray.Create("a", "b", "c").Required<string[]>(), Is.Not.Null.And.EqualTo(new[] { "a", "b", "c" }));
			Assert.That(() => JsonNull.Null.Required<string[]>(), Throws.InstanceOf<JsonBindingException>());

			//Value Type List
			Assert.That(JsonArray.Create(1, 2, 3).Required<List<int>>(), Is.Not.Null.And.EqualTo(new[] { 1, 2, 3 }));
			Assert.That(() => JsonNull.Null.Required<List<int>>(), Throws.InstanceOf<JsonBindingException>());

			//Ref Type List
			Assert.That(JsonArray.Create("a", "b", "c").Required<List<string>>(), Is.Not.Null.And.EqualTo(new[] { "a", "b", "c" }));
			Assert.That(() => JsonNull.Null.Required<List<string>>(), Throws.InstanceOf<JsonBindingException>());

			//Format Exceptions
			Assert.That(() => JsonString.Return("foo").Required<int>(), Throws.InstanceOf<FormatException>());
			Assert.That(() => JsonArray.Create("foo").Required<int[]>(), Throws.InstanceOf<FormatException>());
			Assert.That(() => JsonArray.Create("foo").Required<List<int>>(), Throws.InstanceOf<FormatException>());
		}

		[Test]
		public void Test_JsonValue_As_Of_T()
		{
			var guid = Guid.NewGuid();
			var now = DateTime.Now;

			//Value Type

			Assert.Multiple(() =>
			{
				Assert.That(JsonNumber.Return(123).As<int>(), Is.EqualTo(123));
				Assert.That(JsonString.Return("123").As<int>(), Is.EqualTo(123));
				Assert.That(JsonNumber.Return(123).As<int>(456), Is.EqualTo(123));
				Assert.That(JsonBoolean.False.As<bool>(), Is.False);
				Assert.That(JsonBoolean.True.As<bool>(), Is.True);
				Assert.That(JsonString.Return(guid).As<Guid>(), Is.EqualTo(guid));
				// As<T>()
				Assert.That(JsonDateTime.Return(now).As<DateTime>(), Is.EqualTo(now));
				Assert.That(JsonNull.Null.As<string>(), Is.Null);
				Assert.That(JsonNull.Null.As<string>("hello"), Is.EqualTo("hello"));
				Assert.That(JsonNull.Null.As<string>(null), Is.Null);
				Assert.That(JsonNull.Null.As<int>(), Is.EqualTo(0));
				Assert.That(JsonNull.Null.As<int>(123), Is.EqualTo(123));
				Assert.That(JsonNull.Null.As<int?>(123), Is.EqualTo(123));
				Assert.That(JsonNull.Null.As<int?>(null), Is.Null);
				Assert.That(JsonNull.Null.As<bool>(), Is.False);
				Assert.That(JsonNull.Null.As<bool>(false), Is.False);
				Assert.That(JsonNull.Null.As<bool>(true), Is.True);
				Assert.That(JsonNull.Null.As<Guid>(), Is.EqualTo(Guid.Empty));
				Assert.That(JsonNull.Null.As<Guid>(guid), Is.EqualTo(guid));
				Assert.That(JsonNull.Null.As<DateTime>(), Is.EqualTo(DateTime.MinValue));
				Assert.That(JsonNull.Null.As<DateTime>(now), Is.EqualTo(now));
				// As() non generic
				Assert.That(JsonNull.Null.As("hello"), Is.EqualTo("hello"));
				Assert.That(JsonNull.Null.As(123), Is.EqualTo(123));
				Assert.That(JsonNull.Null.As(false), Is.False);
				Assert.That(JsonNull.Null.As(true), Is.True);
				Assert.That(JsonNull.Null.As(guid), Is.EqualTo(guid));
				Assert.That(JsonNull.Null.As(now), Is.EqualTo(now));
				Assert.That(default(JsonValue).As<int>(), Is.EqualTo(0));
				Assert.That(default(JsonValue).As<int>(123), Is.EqualTo(123));
				Assert.That(default(JsonValue).As<bool>(), Is.False);
				Assert.That(default(JsonValue).As<bool>(false), Is.False);
				Assert.That(default(JsonValue).As<bool>(true), Is.True);
				Assert.That(default(JsonValue).As<Guid>(), Is.EqualTo(Guid.Empty));
				Assert.That(default(JsonValue).As<Guid>(guid), Is.EqualTo(guid));
				Assert.That(default(JsonValue).As<DateTime>(), Is.EqualTo(DateTime.MinValue));
				Assert.That(default(JsonValue).As<DateTime>(now), Is.EqualTo(now));
			});

			//Nullable Type
			Assert.Multiple(() =>
			{
				Assert.That(JsonNumber.Return(123).As<int?>(), Is.Not.Null.And.EqualTo(123));
				Assert.That(JsonString.Return("123").As<int?>(), Is.Not.Null.And.EqualTo(123));
				Assert.That(JsonNumber.Return(123).As<int?>(456), Is.Not.Null.And.EqualTo(123));
				Assert.That(JsonBoolean.True.As<bool?>(), Is.True);
				Assert.That(JsonNull.Null.As<int?>(123), Is.EqualTo(123));
				Assert.That(JsonNull.Null.As<int?>(), Is.Null);
				Assert.That(JsonNull.Null.As<bool?>(), Is.Null);
				Assert.That(JsonNull.Null.As<bool?>(false), Is.False);
				Assert.That(JsonNull.Null.As<bool?>(true), Is.True);
				Assert.That(JsonNull.Null.As<Guid?>(), Is.Null);
				Assert.That(JsonNull.Null.As<Guid?>(guid), Is.EqualTo(guid));
				Assert.That(default(JsonValue).As<int?>(null), Is.Null);
				Assert.That(default(JsonValue).As<int?>(123), Is.EqualTo(123));
				Assert.That(default(JsonValue).As<bool?>(), Is.Null);
				Assert.That(default(JsonValue).As<bool?>(false), Is.False);
				Assert.That(default(JsonValue).As<bool?>(true), Is.True);
				Assert.That(default(JsonValue).As<Guid?>(), Is.Null);
				Assert.That(default(JsonValue).As<Guid?>(guid), Is.EqualTo(guid));
			});

			//Reference Primitive Type

			Assert.Multiple(() =>
			{
				//Nullable Type
				Assert.That(JsonNumber.Return(123).As<string>(), Is.Not.Null.And.EqualTo("123"));
				Assert.That(JsonString.Return("123").As<string>(), Is.Not.Null.And.EqualTo("123"));
				Assert.That(JsonNull.Null.As<string>(), Is.Null);
				Assert.That(JsonNull.Null.As<string>("not_found"), Is.EqualTo("not_found"));
				Assert.That(default(JsonValue).As<string>(), Is.Null);
				Assert.That(default(JsonValue).As<string>("not_found"), Is.EqualTo("not_found"));

				//Value Type Array
				Assert.That(JsonArray.Create(1, 2, 3).As<int[]>(), Is.Not.Null.And.EqualTo(new [] { 1, 2, 3 }));
				Assert.That(JsonNull.Null.As<int[]>(), Is.Null);
				Assert.That(JsonNull.Null.As<int[]>([ 1, 2, 3 ]), Is.EqualTo(new [] { 1, 2, 3 }));
				Assert.That(default(JsonValue).As<int[]>(), Is.Null);
				Assert.That(default(JsonValue).As<int[]>([ 1, 2, 3 ]), Is.EqualTo(new [] { 1, 2, 3 }));

				//Ref Type Array
				Assert.That(JsonArray.Create("a", "b", "c").As<string[]>(), Is.Not.Null.And.EqualTo(new[] { "a", "b", "c" }));
				Assert.That(JsonNull.Null.As<string[]>(), Is.Null);
				Assert.That(JsonNull.Null.As<string[]>([ "a", "b", "c" ]), Is.EqualTo(new[] { "a", "b", "c" }));
				Assert.That(default(JsonValue).As<string[]>(), Is.Null);
				Assert.That(default(JsonValue).As<string[]>([ "a", "b", "c" ]), Is.EqualTo(new[] { "a", "b", "c" }));

				//Value Type List
				Assert.That(JsonArray.Create(1, 2, 3).As<List<int>>(), Is.Not.Null.And.EqualTo(new[] { 1, 2, 3 }));
				Assert.That(JsonNull.Null.As<List<int>>(), Is.Null);

				//Ref Type List
				Assert.That(JsonArray.Create("a", "b", "c").As<List<string>>(), Is.Not.Null.And.EqualTo(new[] { "a", "b", "c" }));
				Assert.That(JsonNull.Null.As<List<string>>(), Is.Null);
			});

			// JsonNull
			Assert.Multiple(() =>
			{
				Assert.That(JsonNull.Null.As<JsonValue>(), Is.SameAs(JsonNull.Null));
				Assert.That(JsonNull.Null.As<JsonNull>(), Is.SameAs(JsonNull.Null));
				Assert.That(JsonNull.Missing.As<JsonValue>(), Is.SameAs(JsonNull.Missing));
				Assert.That(JsonNull.Missing.As<JsonNull>(), Is.SameAs(JsonNull.Missing));
				Assert.That(default(JsonValue).As<JsonValue>(), Is.SameAs(JsonNull.Null));
				Assert.That(default(JsonValue).As<JsonNull>(), Is.SameAs(JsonNull.Null));
			});

			//Format Exceptions
			Assert.Multiple(() =>
			{
				Assert.That(() => JsonString.Return("foo").As<int>(), Throws.InstanceOf<FormatException>());
				Assert.That(() => JsonArray.Create("foo").As<int[]>(), Throws.InstanceOf<FormatException>());
				Assert.That(() => JsonArray.Create("foo").As<List<int>>(), Throws.InstanceOf<FormatException>());
			});
		}

		[Test]
		public void Test_JsonValue_AsObject()
		{
			{ // null
				JsonValue? value = null;
				Assert.That(() => value.AsObject(), Throws.InstanceOf<JsonBindingException>());
				Assert.That(value.AsObjectOrDefault(), Is.Null);
				Assert.That(value.AsObjectOrEmpty(), Is.SameAs(JsonObject.ReadOnly.Empty));
			}
			{ // JsonNull
				JsonValue value = JsonNull.Null;
				Assert.That(() => value.AsObject(), Throws.InstanceOf<JsonBindingException>());
				Assert.That(value.AsObjectOrDefault(), Is.Null);
				Assert.That(value.AsObjectOrEmpty(), Is.SameAs(JsonObject.ReadOnly.Empty));
			}
			{ // empty object
				JsonValue value = JsonObject.Create();
				Assert.That(value.AsObject(), Is.SameAs(value));
				Assert.That(value.AsObjectOrDefault(), Is.SameAs(value));
				Assert.That(value.AsObjectOrEmpty(), Is.SameAs(value));
			}
			{ // non empty object
				JsonValue value = JsonObject.Create("hello", "world");
				Assert.That(value.AsObject(), Is.SameAs(value));
				Assert.That(value.AsObjectOrDefault(), Is.SameAs(value));
				Assert.That(value.AsObjectOrEmpty(), Is.SameAs(value));
			}
			{ // not an object
				JsonValue value = JsonArray.Create("hello", "world");
				Assert.That(() => value.AsObject(), Throws.InstanceOf<JsonBindingException>());
				Assert.That(() => value.AsObjectOrDefault(), Throws.InstanceOf<JsonBindingException>());
				Assert.That(() => value.AsObjectOrEmpty(), Throws.InstanceOf<JsonBindingException>());
			}
		}

		[Test]
		public void Test_JsonValue_GetObject()
		{
			var foo = JsonObject.Create();
			var bar = JsonObject.Create([ ("x", 1), ("y", 2), ("z", 3) ]);
			{
				var obj = JsonObject.Create(
				[
					("foo", foo),
					("bar", bar),
					("baz", JsonNull.Null),
					("other", JsonArray.Create([ "hello", "world" ])),
					("text", "hello, there!"),
					("number", 123),
					("boolean", true),
				]);

				{ // GetObject()
					Assert.That(obj.GetObject("foo"), Is.SameAs(foo));
					Assert.That(obj.GetObject("bar"), Is.SameAs(bar));
					Assert.That(() => obj.GetObject("baz"), Throws.InstanceOf<JsonBindingException>());
					Assert.That(() => obj.GetObject("not_found"), Throws.InstanceOf<JsonBindingException>());
					Assert.That(() => obj.GetObject("other"), Throws.InstanceOf<JsonBindingException>());
					Assert.That(() => obj.GetObject("text"), Throws.InstanceOf<JsonBindingException>());
					Assert.That(() => obj.GetObject("number"), Throws.InstanceOf<JsonBindingException>());
					Assert.That(() => obj.GetObject("boolean"), Throws.InstanceOf<JsonBindingException>());
				}
				{ // GetObjectOrDefault()
					Assert.That(obj.GetObjectOrDefault("foo"), Is.SameAs(foo));
					Assert.That(obj.GetObjectOrDefault("bar"), Is.SameAs(bar));
					Assert.That(() => obj.GetObjectOrDefault("baz"), Is.Null);
					Assert.That(() => obj.GetObjectOrDefault("not_found"), Is.Null);
					Assert.That(() => obj.GetObjectOrDefault("other"), Throws.InstanceOf<JsonBindingException>());
					Assert.That(() => obj.GetObjectOrDefault("text"), Throws.InstanceOf<JsonBindingException>());
					Assert.That(() => obj.GetObjectOrDefault("number"), Throws.InstanceOf<JsonBindingException>());
					Assert.That(() => obj.GetObjectOrDefault("boolean"), Throws.InstanceOf<JsonBindingException>());
				}
				{ // GetObjectOrEmpty()
					Assert.That(obj.GetObjectOrEmpty("foo"), Is.SameAs(foo));
					Assert.That(obj.GetObjectOrEmpty("bar"), Is.SameAs(bar));
					Assert.That(() => obj.GetObjectOrEmpty("baz"), Is.SameAs(JsonObject.ReadOnly.Empty));
					Assert.That(() => obj.GetObjectOrEmpty("not_found"), Is.SameAs(JsonObject.ReadOnly.Empty));
					Assert.That(() => obj.GetObjectOrEmpty("other"), Throws.InstanceOf<JsonBindingException>());
					Assert.That(() => obj.GetObjectOrEmpty("text"), Throws.InstanceOf<JsonBindingException>());
					Assert.That(() => obj.GetObjectOrEmpty("number"), Throws.InstanceOf<JsonBindingException>());
					Assert.That(() => obj.GetObjectOrEmpty("boolean"), Throws.InstanceOf<JsonBindingException>());
				}
			}
			{
				var arr = new JsonArray()
				{
					/*0*/ foo,
					/*1*/ bar,
					/*2*/ JsonNull.Null,
					/*3*/ JsonArray.Create("hello", "world"),
					/*4*/ "hello, there!",
					/*5*/ 123,
					/*6*/ true,
				};

				{ // GetArray()
					Assert.That(arr.GetObject(0), Is.SameAs(foo));
					Assert.That(arr.GetObject(1), Is.SameAs(bar));
					Assert.That(() => arr.GetObject(2), Throws.InstanceOf<JsonBindingException>());
					Assert.That(() => arr.GetObject(3), Throws.InstanceOf<JsonBindingException>());
					Assert.That(() => arr.GetObject(4), Throws.InstanceOf<JsonBindingException>());
					Assert.That(() => arr.GetObject(5), Throws.InstanceOf<JsonBindingException>());
					Assert.That(() => arr.GetObject(6), Throws.InstanceOf<JsonBindingException>());
				}
				{ // GetArrayOrDefault()
					Assert.That(arr.GetObjectOrDefault(0), Is.SameAs(foo));
					Assert.That(arr.GetObjectOrDefault(1), Is.SameAs(bar));
					Assert.That(() => arr.GetObjectOrDefault(2), Is.Null);
					Assert.That(() => arr.GetObjectOrDefault(3), Throws.InstanceOf<JsonBindingException>());
					Assert.That(() => arr.GetObjectOrDefault(4), Throws.InstanceOf<JsonBindingException>());
					Assert.That(() => arr.GetObjectOrDefault(5), Throws.InstanceOf<JsonBindingException>());
					Assert.That(() => arr.GetObjectOrDefault(6), Throws.InstanceOf<JsonBindingException>());
				}
				{ // GetArrayOrEmpty()
					Assert.That(arr.GetObjectOrEmpty(0), Is.SameAs(foo));
					Assert.That(arr.GetObjectOrEmpty(1), Is.SameAs(bar));
					Assert.That(() => arr.GetObjectOrEmpty(2), Is.SameAs(JsonObject.ReadOnly.Empty));
					Assert.That(() => arr.GetObjectOrEmpty(3), Throws.InstanceOf<JsonBindingException>());
					Assert.That(() => arr.GetObjectOrEmpty(4), Throws.InstanceOf<JsonBindingException>());
					Assert.That(() => arr.GetObjectOrEmpty(5), Throws.InstanceOf<JsonBindingException>());
					Assert.That(() => arr.GetObjectOrEmpty(6), Throws.InstanceOf<JsonBindingException>());
				}
			}
		}

		[Test]
		public void Test_JsonValue_AsArray()
		{
			{ // null
				JsonValue? value = null;
				Assert.That(() => value.AsArray(), Throws.InstanceOf<JsonBindingException>());
				Assert.That(value.AsArrayOrDefault(), Is.Null);
				Assert.That(value.AsArrayOrEmpty(), Is.SameAs(JsonArray.ReadOnly.Empty));
			}
			{ // JsonNull
				JsonValue value = JsonNull.Null;
				Assert.That(() => value.AsArray(), Throws.InstanceOf<JsonBindingException>());
				Assert.That(value.AsArrayOrDefault(), Is.Null);
				Assert.That(value.AsArrayOrEmpty(), Is.SameAs(JsonArray.ReadOnly.Empty));
			}
			{ // empty array
				JsonValue value = JsonArray.Create();
				Assert.That(value.AsArray(), Is.SameAs(value));
				Assert.That(value.AsArrayOrDefault(), Is.SameAs(value));
				Assert.That(value.AsArrayOrEmpty(), Is.SameAs(value));
			}
			{ // non empty array
				JsonValue value = JsonArray.Create("hello", "world");
				Assert.That(value.AsArray(), Is.SameAs(value));
				Assert.That(value.AsArrayOrDefault(), Is.SameAs(value));
				Assert.That(value.AsArrayOrEmpty(), Is.SameAs(value));
			}
			{ // not an array
				JsonValue value = JsonObject.Create("hello", "world");
				Assert.That(() => value.AsArray(), Throws.InstanceOf<JsonBindingException>());
				Assert.That(() => value.AsArrayOrDefault(), Throws.InstanceOf<JsonBindingException>());
				Assert.That(() => value.AsArrayOrEmpty(), Throws.InstanceOf<JsonBindingException>());
			}
		}

		[Test]
		public void Test_JsonValue_GetArray()
		{
			var foo = JsonArray.Create();
			var bar = JsonArray.Create("x", 1, "y", 2, "z", 3);
			{
				var obj = new JsonObject()
				{
					["foo"] = foo,
					["bar"] = bar,
					["baz"] = JsonNull.Null,
					["other"] = JsonObject.Create("hello", "world"),
					["text"] = "hello, there!",
					["number"] = 123,
					["boolean"] = true,
				};

				{ // GetArray()
					Assert.That(obj.GetArray("foo"), Is.SameAs(foo));
					Assert.That(obj.GetArray("bar"), Is.SameAs(bar));
					Assert.That(() => obj.GetArray("baz"), Throws.InstanceOf<JsonBindingException>());
					Assert.That(() => obj.GetArray("not_found"), Throws.InstanceOf<JsonBindingException>());
					Assert.That(() => obj.GetArray("other"), Throws.InstanceOf<JsonBindingException>());
					Assert.That(() => obj.GetArray("text"), Throws.InstanceOf<JsonBindingException>());
					Assert.That(() => obj.GetArray("number"), Throws.InstanceOf<JsonBindingException>());
					Assert.That(() => obj.GetArray("boolean"), Throws.InstanceOf<JsonBindingException>());
				}
				{ // GetArrayOrDefault()
					Assert.That(obj.GetArrayOrDefault("foo"), Is.SameAs(foo));
					Assert.That(obj.GetArrayOrDefault("bar"), Is.SameAs(bar));
					Assert.That(obj.GetArrayOrDefault("baz"), Is.Null);
					Assert.That(obj.GetArrayOrDefault("not_found"), Is.Null);
					Assert.That(() => obj.GetArrayOrDefault("other"), Throws.InstanceOf<JsonBindingException>());
					Assert.That(() => obj.GetArrayOrDefault("text"), Throws.InstanceOf<JsonBindingException>());
					Assert.That(() => obj.GetArrayOrDefault("number"), Throws.InstanceOf<JsonBindingException>());
					Assert.That(() => obj.GetArrayOrDefault("boolean"), Throws.InstanceOf<JsonBindingException>());
				}
				{ // GetArrayOrEmpty()
					Assert.That(obj.GetArrayOrEmpty("foo"), Is.SameAs(foo));
					Assert.That(obj.GetArrayOrEmpty("bar"), Is.SameAs(bar));
					Assert.That(obj.GetArrayOrEmpty("baz"), Is.SameAs(JsonArray.ReadOnly.Empty));
					Assert.That(obj.GetArrayOrEmpty("not_found"), Is.SameAs(JsonArray.ReadOnly.Empty));
					Assert.That(() => obj.GetArrayOrEmpty("other"), Throws.InstanceOf<JsonBindingException>());
					Assert.That(() => obj.GetArrayOrEmpty("text"), Throws.InstanceOf<JsonBindingException>());
					Assert.That(() => obj.GetArrayOrEmpty("number"), Throws.InstanceOf<JsonBindingException>());
					Assert.That(() => obj.GetArrayOrEmpty("boolean"), Throws.InstanceOf<JsonBindingException>());
				}
			}
			{
				var arr = new JsonArray()
				{
					/*0*/ foo,
					/*1*/ bar,
					/*2*/ JsonNull.Null,
					/*3*/ JsonObject.Create("hello", "world"),
					/*4*/ "hello, there!",
					/*5*/ 123,
					/*6*/ true,
				};

				{ // GetArray()
					Assert.That(arr.GetArray(0), Is.SameAs(foo));
					Assert.That(arr.GetArray(1), Is.SameAs(bar));
					Assert.That(() => arr.GetArray(2), Throws.InstanceOf<JsonBindingException>());
					Assert.That(() => arr.GetArray(3), Throws.InstanceOf<JsonBindingException>());
					Assert.That(() => arr.GetArray(4), Throws.InstanceOf<JsonBindingException>());
					Assert.That(() => arr.GetArray(5), Throws.InstanceOf<JsonBindingException>());
					Assert.That(() => arr.GetArray(6), Throws.InstanceOf<JsonBindingException>());
					Assert.That(() => arr.GetArray(42), Throws.InstanceOf<IndexOutOfRangeException>());
				}
				{ // GetArrayOrDefault()
					Assert.That(arr.GetArrayOrDefault(0), Is.SameAs(foo));
					Assert.That(arr.GetArrayOrDefault(1), Is.SameAs(bar));
					Assert.That(arr.GetArrayOrDefault(2), Is.Null);
					Assert.That(() => arr.GetArrayOrDefault(3), Throws.InstanceOf<JsonBindingException>());
					Assert.That(() => arr.GetArrayOrDefault(4), Throws.InstanceOf<JsonBindingException>());
					Assert.That(() => arr.GetArrayOrDefault(5), Throws.InstanceOf<JsonBindingException>());
					Assert.That(() => arr.GetArrayOrDefault(6), Throws.InstanceOf<JsonBindingException>());
					Assert.That(() => arr.GetArrayOrDefault(42), Throws.InstanceOf<IndexOutOfRangeException>());
				}
				{ // GetArrayOrEmpty()
					Assert.That(arr.GetArrayOrEmpty(0), Is.SameAs(foo));
					Assert.That(arr.GetArrayOrEmpty(1), Is.SameAs(bar));
					Assert.That(arr.GetArrayOrEmpty(2), Is.SameAs(JsonArray.ReadOnly.Empty));
					Assert.That(() => arr.GetArrayOrEmpty(3), Throws.InstanceOf<JsonBindingException>());
					Assert.That(() => arr.GetArrayOrEmpty(4), Throws.InstanceOf<JsonBindingException>());
					Assert.That(() => arr.GetArrayOrEmpty(5), Throws.InstanceOf<JsonBindingException>());
					Assert.That(() => arr.GetArrayOrEmpty(6), Throws.InstanceOf<JsonBindingException>());
					Assert.That(() => arr.GetArrayOrEmpty(42), Throws.InstanceOf<IndexOutOfRangeException>());
				}
			}
			{
				JsonValue missing = JsonNull.Missing;

				{ // GetArray()
					Assert.That(() => missing.GetArray(0), Throws.InstanceOf<JsonBindingException>());
					Assert.That(() => missing.GetArray(1), Throws.InstanceOf<JsonBindingException>());
					Assert.That(() => missing.GetArray(2), Throws.InstanceOf<JsonBindingException>());
					Assert.That(() => missing.GetArray(3), Throws.InstanceOf<JsonBindingException>());
					Assert.That(() => missing.GetArray(4), Throws.InstanceOf<JsonBindingException>());
					Assert.That(() => missing.GetArray(5), Throws.InstanceOf<JsonBindingException>());
					Assert.That(() => missing.GetArray(6), Throws.InstanceOf<JsonBindingException>());
					Assert.That(() => missing.GetArray(42), Throws.InstanceOf<JsonBindingException>());
				}
				{ // GetArrayOrDefault()
					Assert.That(missing.GetArrayOrDefault(0), Is.Null);
					Assert.That(missing.GetArrayOrDefault(1), Is.Null);
					Assert.That(missing.GetArrayOrDefault(2), Is.Null);
					Assert.That(missing.GetArrayOrDefault(3), Is.Null);
					Assert.That(missing.GetArrayOrDefault(4), Is.Null);
					Assert.That(missing.GetArrayOrDefault(5), Is.Null);
					Assert.That(missing.GetArrayOrDefault(6), Is.Null);
					Assert.That(missing.GetArrayOrDefault(42), Is.Null);
				}
				{ // GetArrayOrEmpty()
					Assert.That(missing.GetArrayOrEmpty(0), Is.SameAs(JsonArray.ReadOnly.Empty));
					Assert.That(missing.GetArrayOrEmpty(1), Is.SameAs(JsonArray.ReadOnly.Empty));
					Assert.That(missing.GetArrayOrEmpty(2), Is.SameAs(JsonArray.ReadOnly.Empty));
					Assert.That(missing.GetArrayOrEmpty(3), Is.SameAs(JsonArray.ReadOnly.Empty));
					Assert.That(missing.GetArrayOrEmpty(4), Is.SameAs(JsonArray.ReadOnly.Empty));
					Assert.That(missing.GetArrayOrEmpty(5), Is.SameAs(JsonArray.ReadOnly.Empty));
					Assert.That(missing.GetArrayOrEmpty(6), Is.SameAs(JsonArray.ReadOnly.Empty));
					Assert.That(missing.GetArrayOrEmpty(42), Is.SameAs(JsonArray.ReadOnly.Empty));
				}
			}
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
				Assert.That(json.Required<NodaTime.Duration>(), Is.EqualTo(duration));
				Assert.That(json.As<NodaTime.Duration>(), Is.EqualTo(duration));
			}
		}

		[Test]
		public void Test_JsonSerialize_DateTime_Types_ToMicrosoftFormat()
		{
			var settings = CrystalJsonSettings.Json.WithMicrosoftDates();

			// trust, but verify...
			Assume.That(typeof(DateTime).IsPrimitive, Is.False);
			Assume.That(typeof(DateTime).IsValueType, Is.True);
			long unixEpoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).Ticks;
			Assume.That(unixEpoch, Is.EqualTo(621355968000000000));

			TimeSpan utcOffset = DateTimeOffset.Now.Offset;

			// corner cases
			CheckSerialize(new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc), settings, "\"\\/Date(0)\\/\"");
			CheckSerialize(new DateTime(0, DateTimeKind.Utc), settings, "\"\\/Date(-62135596800000)\\/\"");
			CheckSerialize(DateTime.MinValue, settings, "\"\\/Date(-62135596800000)\\/\"");
			CheckSerialize(new DateTime(3155378975999999999, DateTimeKind.Utc), settings, "\"\\/Date(253402300799999)\\/\"");
			CheckSerialize(DateTime.MaxValue, settings, "\"\\/Date(253402300799999)\\/\"");

			// Now (UTC)
			DateTime utcNow = DateTime.UtcNow;
			Assert.That(utcNow.Kind, Is.EqualTo(DateTimeKind.Utc));
			long ticks = (utcNow.Ticks - unixEpoch) / 10000;
			CheckSerialize(utcNow, settings, "\"\\/Date(" + ticks.ToString() + ")\\/\"");

			// Now (local)
			DateTime localNow = DateTime.Now;
			Assert.That(localNow.Kind, Is.EqualTo(DateTimeKind.Local));
			ticks = (localNow.Ticks - unixEpoch - utcOffset.Ticks) / 10000;
			CheckSerialize(localNow, settings, "\"\\/Date(" + ticks.ToString() + GetTimeZoneSuffix(localNow) + ")\\/\"");

			// Local vs Unspecified vs UTC
			// * 1er Janvier 2000 = GMT + 1 car heure d'hiver
			CheckSerialize(
				new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc),
				settings,
				"\"\\/Date(946684800000)\\/\"",
				"2000-01-01 UTC"
			);
			CheckSerialize(
				new DateTime(2000, 1, 1, 0, 0, 0),
				settings,
				"\"\\/Date(" + (946684800000 - 1 * 3600 * 1000).ToString() + "+0100)\\/\"",
				"2000-01-01 GMT+1 (Paris)"
			);
			CheckSerialize(
				new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Local),
				settings,
				"\"\\/Date(" + (946684800000 - 1 * 3600 * 1000).ToString() + "+0100)\\/\"",
				"2000-01-01 GMT+1 (Paris)"
			);
			// * 1er Août 2000 = GMT + 2 car heure d'été
			CheckSerialize(
				new DateTime(2000, 9, 1, 0, 0, 0, DateTimeKind.Utc),
				settings,
				"\"\\/Date(967766400000)\\/\"",
				"2000-09-01 UTC"
			);
			CheckSerialize(
				new DateTime(2000, 9, 1, 0, 0, 0),
				settings,
				"\"\\/Date(" + (967766400000 - 2 * 3600 * 1000).ToString() + "+0200)\\/\"",
				"2000-08-01 GMT+2 (Paris, DST)"
			);
			CheckSerialize(
				new DateTime(2000, 9, 1, 0, 0, 0, DateTimeKind.Local),
				settings,
				"\"\\/Date(967759200000" + "+0200)\\/\"",
				"2000-08-01 GMT+2 (Paris, DST)"
			);

			//TODO: DateTimeOffset ?
		}

		private static string ToUtcOffset(TimeSpan offset)
		{
			// note: can be negative! Hours and Minutes will be both negative in this case
			return (offset < TimeSpan.Zero ? "-" : "+") + Math.Abs(offset.Hours).ToString("D2") + ":" + Math.Abs(offset.Minutes).ToString("D2");
		}

		[Test]
		public void Test_JsonSerialize_DateTime_Iso8601()
		{
			var settings = CrystalJsonSettings.Json.WithIso8601Dates();
			Assert.That(settings.DateFormatting, Is.EqualTo(CrystalJsonSettings.DateFormat.TimeStampIso8601));

			// MinValue: must be serialized as an empty string
			// will handle the case where we have serialized DateTime.MinValue, but we are deserializing as Nullable<DateTime>
			CheckSerialize(DateTime.MinValue, settings, "\"\"");
			CheckSerialize(DateTime.SpecifyKind(DateTime.MinValue, DateTimeKind.Utc), settings, "\"\"");
			CheckSerialize(DateTime.SpecifyKind(DateTime.MinValue, DateTimeKind.Local), settings, "\"\"");

			// MaxValue: must NOT specify a timezone
			CheckSerialize(DateTime.MaxValue, settings, "\"9999-12-31T23:59:59.9999999\"");
			CheckSerialize(DateTime.SpecifyKind(DateTime.MaxValue, DateTimeKind.Utc), settings, "\"9999-12-31T23:59:59.9999999\"", "DateTime.MaxValue should not specify UTC 'Z'");
			CheckSerialize(DateTime.SpecifyKind(DateTime.MaxValue, DateTimeKind.Local), settings, "\"9999-12-31T23:59:59.9999999\"", "DateTime.MaxValue should not specify local TimeZone");

			// Unix Epoch
			CheckSerialize(new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc), settings, "\"1970-01-01T00:00:00Z\"");

			// Unspecified
			CheckSerialize(
				new DateTime(2013, 3, 11, 12, 34, 56, 768, DateTimeKind.Unspecified),
				settings,
				"\"2013-03-11T12:34:56.7680000\"",
				"Dates with Unspecified timezone must NOT end with 'Z', NOR include a timezone"
			);

			// UTC
			CheckSerialize(
				new DateTime(2013, 3, 11, 12, 34, 56, 768, DateTimeKind.Utc),
				settings,
				"\"2013-03-11T12:34:56.7680000Z\"",
				"UTC dates must end with 'Z'"
			);

			// Local
			var dt = new DateTime(2013, 3, 11, 12, 34, 56, 768, DateTimeKind.Local);
			CheckSerialize(
				dt,
				settings,
				"\"2013-03-11T12:34:56.7680000" + ToUtcOffset(new DateTimeOffset(dt).Offset) + "\"",
				"Local dates must specify a timezone"
			);

			// Now (UTC)
			DateTime utcNow = DateTime.UtcNow;
			CheckSerialize(utcNow, settings, "\"" + utcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffffff") + "Z\"", "DateTime.UtcNow must end with 'Z'");

			// Now (local)
			DateTime localNow = DateTime.Now;
			CheckSerialize(localNow, settings, "\"" + localNow.ToString("yyyy-MM-ddTHH:mm:ss.fffffff") + ToUtcOffset(DateTimeOffset.Now.Offset) + "\"", "DateTime.Now doit inclure la TimeZone");

			// Local vs Unspecified vs UTC
			// IMPORTANT: this test only works if you are in the "Romance Standard Time" (Paris, Bruxelles, ...), sorry! (or use the pretext to visit Paris, all expenses paid by the QA dept. !)
			// Paris: GMT+1 l'hivers, GMT+2 l'état

			// * 1er Janvier 2000 = GMT + 1 car heure d'hiver
			CheckSerialize(new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc), settings, "\"2000-01-01T00:00:00Z\"", "2000-01-01 UTC");
			CheckSerialize(new DateTime(2000, 1, 1, 0, 0, 0), settings, "\"2000-01-01T00:00:00\"", "2000-01-01 (unspecified)");
			CheckSerialize(new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Local), settings, "\"2000-01-01T00:00:00+01:00\"", "2000-01-01 GMT+1 (Paris)");

			// * 1er Septembre 2000 = GMT + 2 car heure d'été
			CheckSerialize(new DateTime(2000, 9, 1, 0, 0, 0, DateTimeKind.Utc), settings, "\"2000-09-01T00:00:00Z\"", "2000-09-01 UTC");
			CheckSerialize(new DateTime(2000, 9, 1, 0, 0, 0), settings, "\"2000-09-01T00:00:00\"", "2000-09-01 (unspecified)");
			CheckSerialize(new DateTime(2000, 9, 1, 0, 0, 0, DateTimeKind.Local), settings, "\"2000-09-01T00:00:00+02:00\"", "2000-09-01 GMT+2 (Paris, DST)");
		}

		[Test]
		public void Test_JsonSerialize_DateTimeOffset_Iso8601()
		{
			var settings = CrystalJsonSettings.Json.WithIso8601Dates();
			Assert.That(settings.DateFormatting, Is.EqualTo(CrystalJsonSettings.DateFormat.TimeStampIso8601));

			// MinValue: must be serialized as the empty string
			CheckSerialize(DateTimeOffset.MinValue, settings, "\"\"");

			// MaxValue: should NOT specify a timezone
			CheckSerialize(DateTimeOffset.MaxValue, settings, "\"9999-12-31T23:59:59.9999999\"");

			// Unix Epoch
			CheckSerialize(new DateTimeOffset(new(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)), settings, "\"1970-01-01T00:00:00Z\"");

			// Now (Utc, Local)
			CheckSerialize(new DateTimeOffset(new(2013, 3, 11, 12, 34, 56, 768, DateTimeKind.Utc)), settings, "\"2013-03-11T12:34:56.7680000Z\"");
			CheckSerialize(new DateTimeOffset(new(2013, 3, 11, 12, 34, 56, 768, DateTimeKind.Local)), settings, "\"2013-03-11T12:34:56.7680000" + ToUtcOffset(TimeZoneInfo.Local.BaseUtcOffset) + "\"");

			// TimeZones
			CheckSerialize(new DateTimeOffset(2013, 3, 11, 12, 34, 56, 768, TimeSpan.Zero), settings, "\"2013-03-11T12:34:56.7680000Z\"");
			CheckSerialize(new DateTimeOffset(2013, 3, 11, 12, 34, 56, 768, TimeSpan.FromHours(1)), settings, "\"2013-03-11T12:34:56.7680000+01:00\"");
			CheckSerialize(new DateTimeOffset(2013, 3, 11, 12, 34, 56, 768, TimeSpan.FromHours(-1)), settings, "\"2013-03-11T12:34:56.7680000-01:00\"");
			CheckSerialize(new DateTimeOffset(2013, 3, 11, 12, 34, 56, 768, TimeSpan.FromMinutes(11 * 60 + 30)), settings, "\"2013-03-11T12:34:56.7680000+11:30\"");
			CheckSerialize(new DateTimeOffset(2013, 3, 11, 12, 34, 56, 768, TimeSpan.FromMinutes(-11 * 60 - 30)), settings, "\"2013-03-11T12:34:56.7680000-11:30\"");

			// Now (UTC)
			var utcNow = DateTimeOffset.Now.ToUniversalTime();
			CheckSerialize(utcNow, settings, "\"" + utcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffffff") + "Z\"", "DateTime.UtcNow doit finir par Z");

			// Now (local)
			var localNow = DateTimeOffset.Now;
			CheckSerialize(localNow, settings, "\"" + localNow.ToString("yyyy-MM-ddTHH:mm:ss.fffffff") + ToUtcOffset(localNow.Offset) + "\"", "DateTime.Now doit inclure la TimeZone");
			//note: this test will not work if the server is running int the UTC/GMT+0 timezone !

			// Local vs Unspecified vs UTC
			// Paris: GMT+1 l'hivers, GMT+2 l'état

			// * 1er Janvier 2000 = GMT + 1 car heure d'hiver
			CheckSerialize(new DateTimeOffset(2000, 1, 1, 0, 0, 0, TimeSpan.Zero), settings, "\"2000-01-01T00:00:00Z\"", "2000-01-01 UTC");
			CheckSerialize(new DateTimeOffset(2000, 1, 1, 0, 0, 0, TimeSpan.FromHours(1)), settings, "\"2000-01-01T00:00:00+01:00\"", "2000-01-01 GMT+1 (Paris)");

			// * 1er Septembre 2000 = GMT + 2 car heure d'été
			CheckSerialize(new DateTimeOffset(2000, 9, 1, 0, 0, 0, TimeSpan.Zero), settings, "\"2000-09-01T00:00:00Z\"", "2000-09-01 UTC");
			CheckSerialize(new DateTimeOffset(2000, 9, 1, 0, 0, 0, TimeSpan.FromHours(2)), settings, "\"2000-09-01T00:00:00+02:00\"", "2000-09-01 GMT+2 (Paris, DST)");
		}

		[Test]
		public void Test_JsonSerialize_DateTime_Types_ToJavaScriptFormat()
		{
			// trust, but verify...
			Assume.That(typeof(DateTime).IsPrimitive, Is.False);
			Assume.That(typeof(DateTime).IsValueType, Is.True);
			long unixEpoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).Ticks;
			Assume.That(unixEpoch, Is.EqualTo(621355968000000000));

			// corner cases
			CheckSerialize(new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc), CrystalJsonSettings.JavaScript, "new Date(0)");
			CheckSerialize(new DateTime(0, DateTimeKind.Utc), CrystalJsonSettings.JavaScript, "new Date(-62135596800000)");
			CheckSerialize(DateTime.MinValue, CrystalJsonSettings.JavaScript, "new Date(-62135596800000)");
			CheckSerialize(new DateTime(3155378975999999999, DateTimeKind.Utc), CrystalJsonSettings.JavaScript, "new Date(253402300799999)");
			CheckSerialize(DateTime.MaxValue, CrystalJsonSettings.JavaScript, "new Date(253402300799999)");

			// Now (UTC)
			DateTime utcNow = DateTime.UtcNow;
			Assert.That(utcNow.Kind, Is.EqualTo(DateTimeKind.Utc));
			long ticks = (utcNow.Ticks - unixEpoch) / 10000;
			CheckSerialize(utcNow, CrystalJsonSettings.JavaScript, $"new Date({ticks})");

			// Now (local)
			DateTime localNow = DateTime.Now;
			Assert.That(localNow.Kind, Is.EqualTo(DateTimeKind.Local));
			TimeSpan utcOffset = new DateTimeOffset(localNow).Offset;
			ticks = (localNow.Ticks - unixEpoch - utcOffset.Ticks) / 10000;
			CheckSerialize(localNow, CrystalJsonSettings.JavaScript, $"new Date({ticks})");

			// Local vs Unspecified vs UTC
			// * 1er Janvier 2000 = GMT + 1 car heure d'hiver
			CheckSerialize(new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc), CrystalJsonSettings.JavaScript, "new Date(946684800000)", "2000-01-01 UTC");
			CheckSerialize(new DateTime(2000, 1, 1, 0, 0, 0), CrystalJsonSettings.JavaScript, "new Date(946681200000)", "2000-01-01 GMT+1 (Paris)");
			CheckSerialize(new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Local), CrystalJsonSettings.JavaScript, "new Date(946681200000)", "2000-01-01 GMT+1 (Paris)");
			// * 1er Août 2000 = GMT + 2 car heure d'été
			CheckSerialize(new DateTime(2000, 9, 1, 0, 0, 0, DateTimeKind.Utc), CrystalJsonSettings.JavaScript, "new Date(967766400000)", "2000-09-01 UTC");
			CheckSerialize(new DateTime(2000, 9, 1, 0, 0, 0), CrystalJsonSettings.JavaScript, "new Date(967759200000)", "2000-08-01 GMT+2 (Paris, DST)");
			CheckSerialize(new DateTime(2000, 9, 1, 0, 0, 0, DateTimeKind.Local), CrystalJsonSettings.JavaScript, "new Date(967759200000)", "2000-08-01 GMT+2 (Paris, DST)");

			//TODO: DateTimeOffset ?
		}

		[Test]
		public void Test_JsonSerialize_TimeSpan()
		{
			// TimeSpan
			CheckSerialize(TimeSpan.Zero, default, "0");
			CheckSerialize(TimeSpan.FromSeconds(1), default, "1");
			CheckSerialize(TimeSpan.FromSeconds(1.5), default, "1.5");
			CheckSerialize(TimeSpan.FromMinutes(1), default, "60");
			CheckSerialize(TimeSpan.FromMilliseconds(1), default, "0.001");
			CheckSerialize(TimeSpan.FromTicks(1), default, "1E-07");
			CheckSerialize(TimeSpan.MinValue, default, TimeSpan.MinValue.TotalSeconds.ToString("R"));
			CheckSerialize(TimeSpan.MaxValue, default, TimeSpan.MaxValue.TotalSeconds.ToString("R"));
		}

		[Test]
		public void Test_JsonSerializes_EnumTypes()
		{
			// trust, but verify...
			Assume.That(typeof(DummyJsonEnum).IsPrimitive, Is.False);
			Assume.That(typeof(DummyJsonEnum).IsEnum, Is.True);

			// As Integers

			// enum
			CheckSerialize(MidpointRounding.AwayFromZero, default, "1");
			CheckSerialize(DayOfWeek.Friday, default, "5");
			// custom enum
			CheckSerialize(DummyJsonEnum.None, default, "0");
			CheckSerialize(DummyJsonEnum.Foo, default, "1");
			CheckSerialize(DummyJsonEnum.Bar, default, "42");
			CheckSerialize((DummyJsonEnum)123, default, "123");
			// custom [Flags] enum
			CheckSerialize(DummyJsonEnumFlags.None, default, "0");
			CheckSerialize(DummyJsonEnumFlags.Foo, default, "1");
			CheckSerialize(DummyJsonEnumFlags.Bar, default, "2");
			CheckSerialize(DummyJsonEnumFlags.Narf, default, "4");
			CheckSerialize(DummyJsonEnumFlags.Foo | DummyJsonEnumFlags.Bar, default, "3");
			CheckSerialize(DummyJsonEnumFlags.Bar | DummyJsonEnumFlags.Narf, default, "6");
			CheckSerialize((DummyJsonEnumFlags)255, default, "255");

			// As Strings

			var settings = CrystalJsonSettings.Json.WithEnumAsStrings();

			// enum
			CheckSerialize(MidpointRounding.AwayFromZero, settings, "\"AwayFromZero\"");
			CheckSerialize(DayOfWeek.Friday, settings, "\"Friday\"");
			// custom enum
			CheckSerialize(DummyJsonEnum.None, settings, "\"None\"");
			CheckSerialize(DummyJsonEnum.Foo, settings, "\"Foo\"");
			CheckSerialize(DummyJsonEnum.Bar, settings, "\"Bar\"");
			CheckSerialize((DummyJsonEnum)123, settings, "\"123\"");
			// custom [Flags] enum
			CheckSerialize(DummyJsonEnumFlags.None, settings, "\"None\"");
			CheckSerialize(DummyJsonEnumFlags.Foo, settings, "\"Foo\"");
			CheckSerialize(DummyJsonEnumFlags.Bar, settings, "\"Bar\"");
			CheckSerialize(DummyJsonEnumFlags.Narf, settings, "\"Narf\"");
			CheckSerialize(DummyJsonEnumFlags.Foo | DummyJsonEnumFlags.Bar, settings, "\"Foo, Bar\"");
			CheckSerialize(DummyJsonEnumFlags.Bar | DummyJsonEnumFlags.Narf, settings, "\"Bar, Narf\"");
			CheckSerialize((DummyJsonEnumFlags)255, settings, "\"255\"");

			// Duplicate Values

			settings = CrystalJsonSettings.Json.WithEnumAsStrings();
			CheckSerialize(DummyJsonEnumTypo.Bar, settings, "\"Bar\"");
			CheckSerialize(DummyJsonEnumTypo.Barrh, settings, "\"Bar\"");
			CheckSerialize((DummyJsonEnumTypo)2, settings, "\"Bar\"");
		}

		[Test]
		public void Test_JsonSerialize_Structs()
		{
			// trust, but verify...
			Assume.That(typeof(DummyJsonStruct).IsValueType, Is.True);
			Assume.That(typeof(DummyJsonStruct).IsClass, Is.False);

			// empty struct
			var x = new DummyJsonStruct();
			CheckSerialize(
				x,
				default,
				"""{ "Valid": false, "Index": 0, "Size": 0, "Height": 0, "Amount": 0, "Created": "", "State": 0, "RatioOfStuff": 0 }""",
				"Serialize(EMPTY, JSON)"
			);
			CheckSerialize(
				x,
				CrystalJsonSettings.JavaScript,
				"{ Valid: false, Index: 0, Size: 0, Height: 0, Amount: 0, Created: new Date(-62135596800000), State: 0, RatioOfStuff: 0 }",
				"Serialize(EMPTY, JS)"
			);

			// with explicit nulls
			CheckSerialize(
				x,
				CrystalJsonSettings.Json.WithNullMembers(),
				"""{ "Valid": false, "Name": null, "Index": 0, "Size": 0, "Height": 0, "Amount": 0, "Created": "", "Modified": null, "DateOfBirth": null, "State": 0, "RatioOfStuff": 0 }""",
				"Serialize(EMPTY, JSON+ShowNull)"
			);
			CheckSerialize(
				x,
				CrystalJsonSettings.JavaScript.WithNullMembers(),
				"{ Valid: false, Name: null, Index: 0, Size: 0, Height: 0, Amount: 0, Created: new Date(-62135596800000), Modified: null, DateOfBirth: null, State: 0, RatioOfStuff: 0 }",
				"Serialize(EMPTY, JS+ShowNull)"
			);

			// hide default values
			CheckSerialize(
				x,
				CrystalJsonSettings.Json.WithoutDefaultValues(),
				"{ }",
				"Serialize(EMPTY, JSON+HideDefaults)"
			);
			CheckSerialize(
				x,
				CrystalJsonSettings.JavaScript.WithoutDefaultValues(),
				"{ }",
				"Serialize(EMPTY, JS+HideDefaults)"
			);

			// compact mode
			CheckSerialize(
				x,
				CrystalJsonSettings.JsonCompact,
				"""{"Valid":false,"Index":0,"Size":0,"Height":0,"Amount":0,"Created":"","State":0,"RatioOfStuff":0}""",
				"Serialize(EMPTY, JSON+Compact)"
			);
			CheckSerialize(
				x,
				CrystalJsonSettings.JavaScript.Compacted(),
				"{Valid:false,Index:0,Size:0,Height:0,Amount:0,Created:new Date(-62135596800000),State:0,RatioOfStuff:0}",
				"Serialize(EMPTY, JS+Compact)"
			);

			// indented
			CheckSerialize(
				x,
				CrystalJsonSettings.JsonIndented,
				"""
				{
					"Valid": false,
					"Index": 0,
					"Size": 0,
					"Height": 0,
					"Amount": 0,
					"Created": "",
					"State": 0,
					"RatioOfStuff": 0
				}
				""",
				"Serialize(X, JSON+Indented)"
			);
			CheckSerialize(
				x,
				CrystalJsonSettings.JavaScriptIndented,
				"""
				{
					Valid: false,
					Index: 0,
					Size: 0,
					Height: 0,
					Amount: 0,
					Created: new Date(-62135596800000),
					State: 0,
					RatioOfStuff: 0
				}
				""",
				"Serialize(X, JS+Indented)"
			);

			// filled with values
			x.Valid = true;
			x.Name = "James Bond";
			x.Index = 7;
			x.Size = 123456789;
			x.Height = 1.8f;
			x.Amount = 0.07d;
			x.Created = new DateTime(1968, 5, 8);
			x.State = DummyJsonEnum.Foo;

			CheckSerialize(
				x,
				default,
				"""{ "Valid": true, "Name": "James Bond", "Index": 7, "Size": 123456789, "Height": 1.8, "Amount": 0.07, "Created": "1968-05-08T00:00:00", "State": 1, "RatioOfStuff": 8641975.23 }""",
				"Serialize(BOND, JSON)"
			);
			CheckSerialize(
				x,
				CrystalJsonSettings.JavaScript,
				"{ Valid: true, Name: 'James Bond', Index: 7, Size: 123456789, Height: 1.8, Amount: 0.07, Created: new Date(-52106400000), State: 1, RatioOfStuff: 8641975.23 }",
				"Serialize(BOND, JS)"
			);

			// compact mode
			CheckSerialize(
				x,
				CrystalJsonSettings.Json.Compacted(),
				"""{"Valid":true,"Name":"James Bond","Index":7,"Size":123456789,"Height":1.8,"Amount":0.07,"Created":"1968-05-08T00:00:00","State":1,"RatioOfStuff":8641975.23}""",
				"Serialize(BOND, JSON+Compact)"
			);
			CheckSerialize(
				x,
				CrystalJsonSettings.JavaScript.Compacted(),
				"{Valid:true,Name:'James Bond',Index:7,Size:123456789,Height:1.8,Amount:0.07,Created:new Date(-52106400000),State:1,RatioOfStuff:8641975.23}",
				"Serialize(BOND, JS+Compact)"
			);
		}

		[Test]
		public void Test_JsonSerialize_NullableTypes()
		{
			var x = new DummyNullableStruct();
			// since all members are null, the object should be empty
			CheckSerialize(
				x,
				default,
				"{ }",
				"Serialize(EMPTY,JSON)"
			);
			CheckSerialize(
				x,
				CrystalJsonSettings.JavaScript,
				"{ }",
				"Serialize(EMPTY,JS)"
			);
			CheckSerialize(
				x,
				CrystalJsonSettings.Json.WithoutDefaultValues(),
				"{ }",
				"Serialize(EMPTY,JSON+HideDefaults)"
			);
			CheckSerialize(
				x,
				CrystalJsonSettings.JavaScript.WithoutDefaultValues(),
				"{ }",
				"Serialize(EMPTY,JS+HideDefaults)"
			);

			// by default, all should be null
			CheckSerialize(
				x,
				CrystalJsonSettings.Json.WithNullMembers(),
				"""{ "Bool": null, "Int32": null, "Int64": null, "Single": null, "Double": null, "DateTime": null, "TimeSpan": null, "Guid": null, "Enum": null, "Struct": null }""",
				"Serialize(EMPTY,JSON+ShowNull)"
			);
			CheckSerialize(
				x,
				CrystalJsonSettings.JavaScript.WithNullMembers(),
				"{ Bool: null, Int32: null, Int64: null, Single: null, Double: null, DateTime: null, TimeSpan: null, Guid: null, Enum: null, Struct: null }",
				"Serialize(EMPTY,JS+ShowNull)"
			);

			// fill the object with non-null values
			x = new DummyNullableStruct()
			{
				Bool = true,
				Int32 = 123,
				Int64 = 123,
				Single = 1.23f,
				Double = 1.23d,
				Guid = new Guid("98bd4ed7-7337-4018-9551-ee0825ada7ba"),
				DateTime = new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc),
				TimeSpan = TimeSpan.FromMinutes(1),
				Enum = DummyJsonEnum.Bar,
				Struct = new DummyJsonStruct(), // empty
			};
			CheckSerialize(
				x,
				default,
				"""{ "Bool": true, "Int32": 123, "Int64": 123, "Single": 1.23, "Double": 1.23, "DateTime": "2000-01-01T00:00:00Z", "TimeSpan": 60, "Guid": "98bd4ed7-7337-4018-9551-ee0825ada7ba", "Enum": 42, "Struct": { "Valid": false, "Index": 0, "Size": 0, "Height": 0, "Amount": 0, "Created": "", "State": 0, "RatioOfStuff": 0 } }""",
				"Serialize(FILLED, JSON)"
			);
			CheckSerialize(
				x,
				CrystalJsonSettings.JavaScript,
				"{ Bool: true, Int32: 123, Int64: 123, Single: 1.23, Double: 1.23, DateTime: new Date(946684800000), TimeSpan: 60, Guid: '98bd4ed7-7337-4018-9551-ee0825ada7ba', Enum: 42, Struct: { Valid: false, Index: 0, Size: 0, Height: 0, Amount: 0, Created: new Date(-62135596800000), State: 0, RatioOfStuff: 0 } }",
				"Serialize(FILLED, JS)"
			);
		}

		[Test]
		public void Test_JsonSerialize_Class()
		{
			// trust, but verify...
			Assume.That(typeof(DummyJsonClass).IsValueType, Is.False);
			Assume.That(typeof(DummyJsonClass).IsClass, Is.True);

			var x = new DummyJsonClass();
			CheckSerialize(
				x,
				default,
				"""{ "Valid": false, "Index": 0, "Size": 0, "Height": 0, "Amount": 0, "Created": "", "State": 0, "RatioOfStuff": 0 }""",
				"Serialize(EMPTY, JSON)"
			);
			CheckSerialize(
				x,
				CrystalJsonSettings.JavaScript,
				"""{ Valid: false, Index: 0, Size: 0, Height: 0, Amount: 0, Created: new Date(-62135596800000), State: 0, RatioOfStuff: 0 }""",
				"Serialize(EMPTY, JS)"
			);

			// hide default values
			CheckSerialize(
				x,
				CrystalJsonSettings.Json.WithoutDefaultValues(),
				"{ }",
				"SerializeObject(EMPTY, JSON+HideDefaults)"
			);
			CheckSerialize(
				x,
				CrystalJsonSettings.JavaScript.WithoutDefaultValues(),
				"{ }",
				"SerializeObject(EMPTY, JS+HideDefaults)"
			);

			// with explicit nulls
			CheckSerialize(
				x, CrystalJsonSettings.Json.WithNullMembers(),
				"""{ "Valid": false, "Name": null, "Index": 0, "Size": 0, "Height": 0, "Amount": 0, "Created": "", "Modified": null, "DateOfBirth": null, "State": 0, "RatioOfStuff": 0 }""",
				"Serialize(EMPTY, JSON+ShowNullMembers)"
			);
			CheckSerialize(
				x,
				CrystalJsonSettings.JavaScript.WithNullMembers(),
				"""{ Valid: false, Name: null, Index: 0, Size: 0, Height: 0, Amount: 0, Created: new Date(-62135596800000), Modified: null, DateOfBirth: null, State: 0, RatioOfStuff: 0 }""",
				"Serialize(EMPTY, JS+ShowNullMembers)"
			);

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
			CheckSerialize(
				x,
				default,
				"""{ "Valid": true, "Name": "James Bond", "Index": 7, "Size": 123456789, "Height": 1.8, "Amount": 0.07, "Created": "1968-05-08T00:00:00Z", "Modified": "2010-10-28T15:39:00Z", "State": 42, "RatioOfStuff": 8641975.23 }""",
				"Serialize(class, JSON)"
			);
			CheckSerialize(
				x,
				CrystalJsonSettings.JavaScript,
				"""{ Valid: true, Name: 'James Bond', Index: 7, Size: 123456789, Height: 1.8, Amount: 0.07, Created: new Date(-52099200000), Modified: new Date(1288280340000), State: 42, RatioOfStuff: 8641975.23 }""",
				"Serialize(class, JS)"
			);

			// compact
			CheckSerialize(
				x,
				CrystalJsonSettings.JsonCompact,
				"""{"Valid":true,"Name":"James Bond","Index":7,"Size":123456789,"Height":1.8,"Amount":0.07,"Created":"1968-05-08T00:00:00Z","Modified":"2010-10-28T15:39:00Z","State":42,"RatioOfStuff":8641975.23}""",
				"Serialize(class, JSON+Compact)"
			);
			CheckSerialize(
				x,
				CrystalJsonSettings.JavaScript.Compacted(),
				"""{Valid:true,Name:'James Bond',Index:7,Size:123456789,Height:1.8,Amount:0.07,Created:new Date(-52099200000),Modified:new Date(1288280340000),State:42,RatioOfStuff:8641975.23}""",
				"Serialize(class, JS+Compact)"
			);

			// indented
			CheckSerialize(
				x,
				CrystalJsonSettings.JsonIndented,
				"""
				{
					"Valid": true,
					"Name": "James Bond",
					"Index": 7,
					"Size": 123456789,
					"Height": 1.8,
					"Amount": 0.07,
					"Created": "1968-05-08T00:00:00Z",
					"Modified": "2010-10-28T15:39:00Z",
					"State": 42,
					"RatioOfStuff": 8641975.23
				}
				""",
				"Serialize(class, JSON+Indented)"
			);

			CheckSerialize(
				x,
				CrystalJsonSettings.JavaScriptIndented,
				"""
				{
					Valid: true,
					Name: 'James Bond',
					Index: 7,
					Size: 123456789,
					Height: 1.8,
					Amount: 0.07,
					Created: new Date(-52099200000),
					Modified: new Date(1288280340000),
					State: 42,
					RatioOfStuff: 8641975.23
				}
				""",
				"Serialize(class, JS+Indented)"
			);

			// Camel Casing
			CheckSerialize(
				x,
				CrystalJsonSettings.Json.CamelCased(),
				"""{ "valid": true, "name": "James Bond", "index": 7, "size": 123456789, "height": 1.8, "amount": 0.07, "created": "1968-05-08T00:00:00Z", "modified": "2010-10-28T15:39:00Z", "state": 42, "ratioOfStuff": 8641975.23 }""",
				"Serialize(class, JSON+CamelCasing)"
			);
			CheckSerialize(
				x,
				CrystalJsonSettings.JavaScript.CamelCased(),
				"{ valid: true, name: 'James Bond', index: 7, size: 123456789, height: 1.8, amount: 0.07, created: new Date(-52099200000), modified: new Date(1288280340000), state: 42, ratioOfStuff: 8641975.23 }",
				"Serialize(class, JS+CamelCasing)"
			);
		}

		[Test]
		public void Test_JsonSerialize_InterfaceMember()
		{
			// Test: a class that contains a member with an interface type
			// => we should not serialize only the members defined on that interface, but instead serialize the runtime type of the instance, which will not be known in advance

			CheckSerialize(
				new DummyOuterClass(),
				default,
				"""{ "Id": 0 }""",
				"Serialize(EMPTY, JSON)"
			);

			// filled with values
			var agent = new DummyJsonBaseClass()
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

			// Serialize the instance directly (known type)
			CheckSerialize(
				agent,
				default,
				"""{ "$type": "agent", "Valid": true, "Name": "James Bond", "Index": 7, "Size": 123456789, "Height": 1.8, "Amount": 0.07, "Created": "1968-05-08T00:00:00Z", "Modified": "2010-10-28T15:39:00Z", "State": 42, "RatioOfStuff": 8641975.23 }""",
				"Serialize(INNER, JSON)"
			);

			// Serialize the container type that references this instance via the interface
			var x = new DummyOuterClass()
			{
				Id = 7,
				Agent = agent,
			};

			CheckSerialize(
				x,
				default,
				"""{ "Id": 7, "Agent": { "$type": "agent", "Valid": true, "Name": "James Bond", "Index": 7, "Size": 123456789, "Height": 1.8, "Amount": 0.07, "Created": "1968-05-08T00:00:00Z", "Modified": "2010-10-28T15:39:00Z", "State": 42, "RatioOfStuff": 8641975.23 } }""",
				"Serialize(OUTER, JSON)"
			);

			// deserialize the container instance
			var y = CrystalJson.Deserialize<DummyOuterClass>(CrystalJson.Serialize(x));
			Assert.That(y, Is.Not.Null);
			Assert.Multiple(() =>
			{
				Assert.That(y.Id, Is.EqualTo(7));
				Assert.That(y.Agent, Is.Not.Null);
				Assert.That(y.Agent, Is.InstanceOf<DummyJsonBaseClass>(), "Should have used the _class property to find the original type!");
				Assert.That(y.Agent.Name, Is.EqualTo("James Bond"));
				Assert.That(y.Agent.Index, Is.EqualTo(7));
				Assert.That(y.Agent.Size, Is.EqualTo(123456789));
				Assert.That(y.Agent.Height, Is.EqualTo(1.8f));
				Assert.That(y.Agent.Amount, Is.EqualTo(0.07d));
				Assert.That(y.Agent.Created, Is.EqualTo(new DateTime(1968, 5, 8, 0, 0, 0, DateTimeKind.Utc)));
				Assert.That(y.Agent.Modified, Is.EqualTo(new DateTime(2010, 10, 28, 15, 39, 0, DateTimeKind.Utc)));
				Assert.That(y.Agent.State, Is.EqualTo(DummyJsonEnum.Bar));
			});
		}

		[Test]
		public void Test_JsonSerialize_UnsealedClassMember()
		{
			// We have a container type that points to a non-sealed class, but with an instance of the expected type (i.e.: not of a derived type)
			// => there should not be any $type property present, since there are not polymorphic annotations on this type
			var x = new DummyOuterDerivedClass()
			{
				Id = 7,
				Agent = new DummyJsonBaseClass()
				{
					Name = "James Bond",
					Index = 7,
					Size = 123456789,
					Height = 1.8f,
					Amount = 0.07d,
					Created = new DateTime(1968, 5, 8, 0, 0, 0, DateTimeKind.Utc),
					Modified = new DateTime(2010, 10, 28, 15, 39, 0, DateTimeKind.Utc),
					State = DummyJsonEnum.Bar,
				}
			};

			CheckSerialize(
				x.Agent,
				default,
				"""{ "$type": "agent", "Valid": true, "Name": "James Bond", "Index": 7, "Size": 123456789, "Height": 1.8, "Amount": 0.07, "Created": "1968-05-08T00:00:00Z", "Modified": "2010-10-28T15:39:00Z", "State": 42, "RatioOfStuff": 8641975.23 }""",
				"Serialize(INNER, JSON)"
			);

			CheckSerialize(
				x,
				default,
				"""{ "Id": 7, "Agent": { "$type": "agent", "Valid": true, "Name": "James Bond", "Index": 7, "Size": 123456789, "Height": 1.8, "Amount": 0.07, "Created": "1968-05-08T00:00:00Z", "Modified": "2010-10-28T15:39:00Z", "State": 42, "RatioOfStuff": 8641975.23 } }""",
				"Serialize(OUTER, JSON)"
			);

			// indented
			CheckSerialize(
				x,
				CrystalJsonSettings.Json.Indented(),
				"""
				{
					"Id": 7,
					"Agent": {
						"$type": "agent",
						"Valid": true,
						"Name": "James Bond",
						"Index": 7,
						"Size": 123456789,
						"Height": 1.8,
						"Amount": 0.07,
						"Created": "1968-05-08T00:00:00Z",
						"Modified": "2010-10-28T15:39:00Z",
						"State": 42,
						"RatioOfStuff": 8641975.23
					}
				}
				""",
				"Serialize(OUTER, JSON)"
			);

			// Deserialize
			var y = CrystalJson.Deserialize<DummyOuterDerivedClass>(CrystalJson.Serialize(x, CrystalJsonSettings.Json.Indented()));
			Assert.That(y, Is.Not.Null);
			Assert.Multiple(() =>
			{
				Assert.That(y.Id, Is.EqualTo(7));
				Assert.That(y.Agent, Is.Not.Null);
				Assert.That(y.Agent, Is.InstanceOf<DummyJsonBaseClass>());
				Assert.That(y.Agent.Name, Is.EqualTo("James Bond"));
				Assert.That(y.Agent.Index, Is.EqualTo(7));
				Assert.That(y.Agent.Size, Is.EqualTo(123456789));
				Assert.That(y.Agent.Height, Is.EqualTo(1.8f));
				Assert.That(y.Agent.Amount, Is.EqualTo(0.07d));
				Assert.That(y.Agent.Created, Is.EqualTo(new DateTime(1968, 5, 8, 0, 0, 0, DateTimeKind.Utc)));
				Assert.That(y.Agent.Modified, Is.EqualTo(new DateTime(2010, 10, 28, 15, 39, 0, DateTimeKind.Utc)));
				Assert.That(y.Agent.State, Is.EqualTo(DummyJsonEnum.Bar));
			});
		}

		[Test]
		public void Test_JsonSerialize_DerivedClassMember()
		{
			// We have a container type with a member of type "FooBase", but at runtime it contains a "FooDerived" instance (class that derives from "FooBase")
			// => $type should always be present, since there are polymorphic annotations on the types
			// => this "$type" property will also be used to instantiate the correct derived type

			CheckSerialize(
				new DummyOuterDerivedClass(),
				default,
				"""{ "Id": 0 }""",
				"Serialize(EMPTY, JSON)"
			);

			// filled with values
			var agent = new DummyDerivedJsonClass("Janov Bondovicz")
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
			var x = new DummyOuterDerivedClass
			{
				Id = 7,
				Agent = agent,
			};

			// serialize the derived type explicitly (known type)
			// => $type should always be present
			CheckSerialize(
				agent,
				default,
				"""{ "$type": "spy", "DoubleAgentName": "Janov Bondovicz", "Valid": true, "Name": "James Bond", "Index": 7, "Size": 123456789, "Height": 1.8, "Amount": 0.07, "Created": "1968-05-08T00:00:00Z", "Modified": "2010-10-28T15:39:00Z", "State": 42, "RatioOfStuff": 8641975.23 }""",
				"Serialize(INNER, JSON)"
			);

			// serialize the container, which references this instance via the base type
			// => $type should always be present
			CheckSerialize(
				x,
				default,
				"""{ "Id": 7, "Agent": { "$type": "spy", "DoubleAgentName": "Janov Bondovicz", "Valid": true, "Name": "James Bond", "Index": 7, "Size": 123456789, "Height": 1.8, "Amount": 0.07, "Created": "1968-05-08T00:00:00Z", "Modified": "2010-10-28T15:39:00Z", "State": 42, "RatioOfStuff": 8641975.23 } }""",
				"Serialize(OUTER, JSON)"
			);

			// deserialize the container
			var y = CrystalJson.Deserialize<DummyOuterDerivedClass>(CrystalJson.Serialize(x));
			Assert.That(y, Is.Not.Null);
			Assert.That(y.Agent, Is.Not.Null);
			Assert.Multiple(() =>
			{
				Assert.That(y.Id, Is.EqualTo(7));
				Assert.That(y.Agent, Is.InstanceOf<DummyDerivedJsonClass>(), "Should have instantianted the derived inner class, not the base class!");
				Assert.That(y.Agent.Name, Is.EqualTo("James Bond"));
				Assert.That(y.Agent.Index, Is.EqualTo(7));
				Assert.That(y.Agent.Size, Is.EqualTo(123456789));
				Assert.That(y.Agent.Height, Is.EqualTo(1.8f));
				Assert.That(y.Agent.Amount, Is.EqualTo(0.07d));
				Assert.That(y.Agent.Created, Is.EqualTo(new DateTime(1968, 5, 8, 0, 0, 0, DateTimeKind.Utc)));
				Assert.That(y.Agent.Modified, Is.EqualTo(new DateTime(2010, 10, 28, 15, 39, 0, DateTimeKind.Utc)));
				Assert.That(y.Agent.State, Is.EqualTo(DummyJsonEnum.Bar));
			});

			var z = (DummyDerivedJsonClass) y.Agent;
			Assert.That(z.DoubleAgentName, Is.EqualTo("Janov Bondovicz"), "Should have deserialized the members specific to the derived class");
		}

		[Test]
		public void Test_Json_Custom_Serializable_Interface()
		{
			// serialize
			var x = new DummyJsonCustomClass("foo");
			CheckSerialize(x, default, """{ "custom":"foo" }""");

			// deserialize
			var y = CrystalJson.Deserialize<DummyJsonCustomClass>("""{ "custom":"bar" }""");
			Assert.That(y, Is.Not.Null);
			Assert.That(y, Is.InstanceOf<DummyJsonCustomClass>());
			Assert.That(y.GetSecret(), Is.EqualTo("bar"));

			// pack
			var value = JsonValue.FromValue(x);
			Assert.That(value, Is.Not.Null);
			Assert.That(value.Type, Is.EqualTo(JsonType.Object));
			var obj = (JsonObject)value;
			Assert.That(obj.Get<string>("custom"), Is.EqualTo("foo"));
			Assert.That(obj, Has.Count.EqualTo(1));

			// unpack
			var z = value.Bind(typeof(DummyJsonCustomClass))!;
			Assert.That(z, Is.Not.Null);
			Assert.That(z, Is.InstanceOf<DummyJsonCustomClass>());
			Assert.That(((DummyJsonCustomClass) z).GetSecret(), Is.EqualTo("foo"));
		}

		[Test]
		public void Test_Json_Custom_Serializable_Static_Legacy()
		{
			// LEGACY: for back compatibility with old "duck typing" static JsonSerialize method
			// -> new code should use the IJsonDeserializable<T> interface that defines the static method

			// serialize
			var x = new DummyStaticLegacyJson("foo");
			CheckSerialize(x, default, """{ "custom":"foo" }""");

			// deserialize
			var y = CrystalJson.Deserialize<DummyStaticLegacyJson>("""{ "custom":"bar" }""");
			Assert.That(y, Is.Not.Null);
			Assert.That(y, Is.InstanceOf<DummyStaticLegacyJson>());
			Assert.That(y.GetSecret(), Is.EqualTo("bar"));
		}

		[Test]
		public void Test_Json_Custom_Serializable_Static()
		{
			// ensure we can deserialize a type using the static method "JsonDesrialize(...)"
			// - compatible with readonly types and/or types that don't have a parameterless ctor!

			// serialize
			var foo = new DummyStaticCustomJson("foo");
			CheckSerialize(foo, default, """{ "custom":"foo" }""");

			// deserialize
			var foo2 = CrystalJson.Deserialize<DummyStaticCustomJson>("""{ "custom":"bar" }""");
			Assert.That(foo2, Is.Not.Null);
			Assert.That(foo2, Is.InstanceOf<DummyStaticCustomJson>());
			Assert.That(foo2.GetSecret(), Is.EqualTo("bar"));

			// arrays

			var arr = new [] { new DummyStaticCustomJson("foo"), new DummyStaticCustomJson("bar"), };
			CheckSerialize(arr, default, """[ { "custom":"foo" }, { "custom":"bar" } ]""");

			var arr2 = CrystalJson.Deserialize<DummyStaticCustomJson[]>("""[ { "custom":"foo" }, { "custom":"bar" } ]""");
			Assert.That(arr2, Is.Not.Null);
			Assert.That(arr2, Has.Length.EqualTo(2));
			Assert.That(arr2[0].GetSecret(), Is.EqualTo("foo"));
			Assert.That(arr2[1].GetSecret(), Is.EqualTo("bar"));
		}

		[Test]
		public void Test_JsonSerialize_Arrays()
		{
			// int[]
			CheckSerialize(Array.Empty<int>(), default, "[ ]");
			CheckSerialize(new int[1], default, "[ 0 ]");
			CheckSerialize(new int[] { 1, 2, 3 }, default, "[ 1, 2, 3 ]");

			CheckSerialize(Array.Empty<int>(), CrystalJsonSettings.JsonCompact, "[]");
			CheckSerialize(new int[1], CrystalJsonSettings.JsonCompact, "[0]");
			CheckSerialize(new int[] { 1, 2, 3 }, CrystalJsonSettings.JsonCompact, "[1,2,3]");

			// string[]
			CheckSerialize(new string[0], default, "[ ]");
			CheckSerialize(new string[1], default, "[ null ]");
			CheckSerialize(new string[] { "foo" }, default, """[ "foo" ]""");
			CheckSerialize(new string[] { "foo", "bar", "baz" }, default, """[ "foo", "bar", "baz" ]""");
			CheckSerialize(new string[] { "foo" }, CrystalJsonSettings.JavaScript, "[ 'foo' ]");
			CheckSerialize(new string[] { "foo", "bar", "baz" }, CrystalJsonSettings.JavaScript, "[ 'foo', 'bar', 'baz' ]");

			// compact
			CheckSerialize(Array.Empty<string>(), CrystalJsonSettings.JsonCompact, "[]");
			CheckSerialize(new string[] { "foo", "bar", "baz" }, CrystalJsonSettings.JsonCompact, """["foo","bar","baz"]""");
			CheckSerialize(new string[] { "foo", "bar", "baz" }, CrystalJsonSettings.JavaScriptCompact, "['foo','bar','baz']");

			// bool[]
			CheckSerialize(Array.Empty<bool>(), default, "[ ]");
			CheckSerialize(new bool[1], default, "[ false ]");
			CheckSerialize(new bool[] { true, false, true }, default, "[ true, false, true ]");
		}

		[Test]
		public void Test_JsonSerialize_Jagged_Arrays()
		{
			CheckSerialize((int[][]) [ [ ], [ ] ], default, "[ [ ], [ ] ]");
			CheckSerialize((int[][]) [ [ ], [ ] ], CrystalJsonSettings.JsonCompact, "[[],[]]");

			CheckSerialize((int[][]) [ [ 1, 2, 3 ], [ 4, 5, 6 ] ], default, "[ [ 1, 2, 3 ], [ 4, 5, 6 ] ]");
			CheckSerialize((int[][]) [ [ 1, 2, 3 ], [ 4, 5, 6 ] ], CrystalJsonSettings.JsonCompact, "[[1,2,3],[4,5,6]]");

			// INCEPTION !
			CheckSerialize((string[][][][]) [ [ [ [ "INCEPTION" ] ] ] ], default, """[ [ [ [ "INCEPTION" ] ] ] ]""");
		}

		[Test]
		public void Test_JsonSerialize_Lists()
		{
			// Collections
			var listOfStrings = new List<string>();
			CheckSerialize(listOfStrings, default, "[ ]");
			listOfStrings.Add("foo");
			CheckSerialize(listOfStrings, default, """[ "foo" ]""");
			listOfStrings.Add("bar");
			listOfStrings.Add("baz");
			CheckSerialize(listOfStrings, default, """[ "foo", "bar", "baz" ]""");
			CheckSerialize(listOfStrings, CrystalJsonSettings.JavaScript, "[ 'foo', 'bar', 'baz' ]");

			var listOfObjects = new List<object?>() { 123, "Narf", true, DummyJsonEnum.Bar };
			CheckSerialize(listOfObjects, default, """[ 123, "Narf", true, 42 ]""");
			CheckSerialize(listOfObjects, CrystalJsonSettings.JavaScript, "[ 123, 'Narf', true, 42 ]");


			// List<int>
			CheckSerialize(new List<int>(), default, "[ ]");
			CheckSerialize(new List<int> { 0 }, default, "[ 0 ]");
			CheckSerialize(new List<int> { 1, 2, 3 }, default, "[ 1, 2, 3 ]");
			CheckSerialize(new List<int>(), CrystalJsonSettings.JsonCompact, "[]");
			CheckSerialize(new List<int> { 0 }, CrystalJsonSettings.JsonCompact, "[0]");
			CheckSerialize(new List<int> { 1, 2, 3 }, CrystalJsonSettings.JsonCompact, "[1,2,3]");

			// List<string>
			CheckSerialize(new List<string>(), default, "[ ]");
			CheckSerialize(new List<string?> { null }, default, "[ null ]");
			CheckSerialize(new List<string> { "foo" }, default, """[ "foo" ]""");
			CheckSerialize(new List<string> { "foo", "bar", "baz" }, default, """[ "foo", "bar", "baz" ]""");
			CheckSerialize(new List<string> { "foo" }, CrystalJsonSettings.JavaScript, "[ 'foo' ]");
			CheckSerialize(new List<string> { "foo", "bar", "baz" }, CrystalJsonSettings.JavaScript, "[ 'foo', 'bar', 'baz' ]");

			// List<bool>
			CheckSerialize(new List<bool>(), default, "[ ]");
			CheckSerialize(new List<bool> { false }, default, "[ false ]");
			CheckSerialize(new List<bool> { true, false, true }, default, "[ true, false, true ]");
		}

		[Test]
		public void Test_JsonSerialize_Enumerable()
		{
			CheckSerialize(Enumerable.Empty<int>(), default, "[ ]");
			CheckSerialize(Enumerable.Empty<string>(), default, "[ ]");
			CheckSerialize(Enumerable.Empty<bool>(), default, "[ ]");
			CheckSerialize(Enumerable.Empty<System.Net.IPAddress>(), default, "[ ]");
			CheckSerialize(Enumerable.Empty<DateTimeKind>(), default, "[ ]");

			CheckSerialize(Enumerable.Range(1, 1), default, "[ 1 ]");
			CheckSerialize(Enumerable.Range(1, 3), default, "[ 1, 2, 3 ]");
			CheckSerialize(Enumerable.Range(0, 3).Select(i => ((ReadOnlySpan<string>) ["Foo", "Bar", "Baz"])[i]), default, """[ "Foo", "Bar", "Baz" ]""");
			CheckSerialize(Enumerable.Range(1, 3).Select(i => i % 2 == 0), default, "[ false, true, false ]");
			CheckSerialize(Enumerable.Range(1, 3).Select(i => (i, i * i, i * i * i)), default, """[ [ 1, 1, 1 ], [ 2, 4, 8 ], [ 3, 9, 27 ] ]""");

			static IEnumerable<int> RangeUncountable(int count)
			{
				for (int i = 0; i < count; i++)
				{
					yield return 1 + i;
				}
			}

			CheckSerialize(RangeUncountable(0), default, "[ ]");
			CheckSerialize(RangeUncountable(1), default, "[ 1 ]");
			CheckSerialize(RangeUncountable(3), default, "[ 1, 2, 3 ]");
		}

		[Test]
		public void Test_JsonSerialize_QueryableCollection()
		{
			// list of objects
			var queryableOfAnonymous = new int[] { 1, 2, 3 }.Select((x) => new { Value = x, Square = x * x, Ascii = (char)(64 + x) });
			// queryable
			CheckSerialize(queryableOfAnonymous, default, """[ { "Value": 1, "Square": 1, "Ascii": "A" }, { "Value": 2, "Square": 4, "Ascii": "B" }, { "Value": 3, "Square": 9, "Ascii": "C" } ]""");
			// convert to list
			CheckSerialize(queryableOfAnonymous.ToList(), default, """[ { "Value": 1, "Square": 1, "Ascii": "A" }, { "Value": 2, "Square": 4, "Ascii": "B" }, { "Value": 3, "Square": 9, "Ascii": "C" } ]""");
		}

		[Test]
		public void Test_JsonSerialize_STuples()
		{
			// STuple<...>
			CheckSerialize(new STuple(), default, "[ ]");
			CheckSerialize(STuple.Create(123), default, "[ 123 ]");
			CheckSerialize(STuple.Create(123, "Hello"), default, """[ 123, "Hello" ]""");
			CheckSerialize(STuple.Create(123, "Hello", true), default, """[ 123, "Hello", true ]""");
			CheckSerialize(STuple.Create(123, "Hello", true, -1.5), default, """[ 123, "Hello", true, -1.5 ]""");
			CheckSerialize(STuple.Create(123, "Hello", true, -1.5, 'Z'), default, """[ 123, "Hello", true, -1.5, "Z" ]""");
			CheckSerialize(STuple.Create(123, "Hello", true, -1.5, 'Z', new DateTime(2016, 11, 24, 11, 07, 23)), default, """[ 123, "Hello", true, -1.5, "Z", "2016-11-24T11:07:23" ]""");
			CheckSerialize(STuple.Create(123, "Hello", true, -1.5, 'Z', new DateTime(2016, 11, 24, 11, 07, 23), "World"), default, """[ 123, "Hello", true, -1.5, "Z", "2016-11-24T11:07:23", "World" ]""");

			// (ITuple) STuple<...>
			CheckSerialize(STuple.Empty, default, "[ ]");
			CheckSerialize((IVarTuple) STuple.Create(123), default, "[ 123 ]");
			CheckSerialize((IVarTuple) STuple.Create(123, "Hello"), default, """[ 123, "Hello" ]""");
			CheckSerialize((IVarTuple) STuple.Create(123, "Hello", true), default, """[ 123, "Hello", true ]""");
			CheckSerialize((IVarTuple) STuple.Create(123, "Hello", true, -1.5), default, """[ 123, "Hello", true, -1.5 ]""");
			CheckSerialize((IVarTuple) STuple.Create(123, "Hello", true, -1.5, 'Z'), default, """[ 123, "Hello", true, -1.5, "Z" ]""");
			CheckSerialize((IVarTuple) STuple.Create(123, "Hello", true, -1.5, 'Z', new DateTime(2016, 11, 24, 11, 07, 23)), default, """[ 123, "Hello", true, -1.5, "Z", "2016-11-24T11:07:23" ]""");
			CheckSerialize((IVarTuple) STuple.Create(123, "Hello", true, -1.5, 'Z', new DateTime(2016, 11, 24, 11, 07, 23), "World"), default, """[ 123, "Hello", true, -1.5, "Z", "2016-11-24T11:07:23", "World" ]""");

			// custom tuple types
			CheckSerialize(new ListTuple<int>([ 1, 2, 3 ]), default, "[ 1, 2, 3 ]");
			CheckSerialize(new ListTuple<string>([ "foo", "bar", "baz" ]), default, """[ "foo", "bar", "baz" ]""");
			CheckSerialize(new ListTuple<object>([ "hello world", 123, false ]), default, """[ "hello world", 123, false ]""");
			CheckSerialize(new LinkedTuple<int>(STuple.Create(1, 2), 3), default, "[ 1, 2, 3 ]");
			CheckSerialize(new JoinedTuple(STuple.Create(1, 2), STuple.Create(3)), default, "[ 1, 2, 3 ]");
		}

		[Test]
		public void Test_JsonSerialize_ValueTuples()
		{
			// STuple<...>
			Log("ValueTuple...");
			CheckSerialize(ValueTuple.Create(), default, "[ ]");
			CheckSerialize(ValueTuple.Create(123), default, "[ 123 ]");
			CheckSerialize(ValueTuple.Create(123, "Hello"), default, """[ 123, "Hello" ]""");
			CheckSerialize(ValueTuple.Create(123, "Hello", true), default, """[ 123, "Hello", true ]""");
			CheckSerialize(ValueTuple.Create(123, "Hello", true, -1.5), default, """[ 123, "Hello", true, -1.5 ]""");
			CheckSerialize(ValueTuple.Create(123, "Hello", true, -1.5, 'Z'), default, """[ 123, "Hello", true, -1.5, "Z" ]""");
			CheckSerialize(ValueTuple.Create(123, "Hello", true, -1.5, 'Z', new DateTime(2016, 11, 24, 11, 07, 23)), default, """[ 123, "Hello", true, -1.5, "Z", "2016-11-24T11:07:23" ]""");
			CheckSerialize(ValueTuple.Create(123, "Hello", true, -1.5, 'Z', new DateTime(2016, 11, 24, 11, 07, 23), "World"), default, """[ 123, "Hello", true, -1.5, "Z", "2016-11-24T11:07:23", "World" ]""");

			// (ITuple) STuple<...>
			Log("ITuple...");
			CheckSerialize((ITuple) ValueTuple.Create(), default, "[ ]");
			CheckSerialize((ITuple) ValueTuple.Create(123), default, "[ 123 ]");
			CheckSerialize((ITuple) ValueTuple.Create(123, "Hello"), default, """[ 123, "Hello" ]""");
			CheckSerialize((ITuple) ValueTuple.Create(123, "Hello", true), default, """[ 123, "Hello", true ]""");
			CheckSerialize((ITuple) ValueTuple.Create(123, "Hello", true, -1.5), default, """[ 123, "Hello", true, -1.5 ]""");
			CheckSerialize((ITuple) ValueTuple.Create(123, "Hello", true, -1.5, 'Z'), default, """[ 123, "Hello", true, -1.5, "Z" ]""");
			CheckSerialize((ITuple) ValueTuple.Create(123, "Hello", true, -1.5, 'Z', new DateTime(2016, 11, 24, 11, 07, 23)), default, """[ 123, "Hello", true, -1.5, "Z", "2016-11-24T11:07:23" ]""");
			CheckSerialize((ITuple) ValueTuple.Create(123, "Hello", true, -1.5, 'Z', new DateTime(2016, 11, 24, 11, 07, 23), "World"), default, """[ 123, "Hello", true, -1.5, "Z", "2016-11-24T11:07:23", "World" ]""");
		}

		[Test]
		public void Test_JsonSerialize_NodaTime_Types()
		{
			#region Duration

			// seconds (integer)
			var duration = NodaTime.Duration.FromSeconds(3600);
			CheckSerialize(duration, default, "3600");

			// seconds + miliseconds
			duration = NodaTime.Duration.FromMilliseconds(3600272);
			CheckSerialize(duration, default, "3600.272");

			// epsilon
			duration = NodaTime.Duration.Epsilon;
			CheckSerialize(duration, default, "1E-09");

			#endregion

			#region Instant

			var instant = default(NodaTime.Instant);
			CheckSerialize(instant, default, "\"1970-01-01T00:00:00Z\"");

			instant = NodaTime.Instant.FromUtc(2013, 6, 7, 11, 06, 58);
			CheckSerialize(instant, default, "\"2013-06-07T11:06:58Z\"");

			instant = NodaTime.Instant.FromUtc(-52, 8, 27, 12, 12);
			CheckSerialize(instant, default, "\"-0052-08-27T12:12:00Z\"");

			#endregion

			#region LocalDateTime

			var time = default(NodaTime.LocalDateTime);
			CheckSerialize(time, default, "\"0001-01-01T00:00:00\"");

			time = new NodaTime.LocalDateTime(1988, 04, 19, 00, 35, 56);
			CheckSerialize(time, default, "\"1988-04-19T00:35:56\"");

			time = new NodaTime.LocalDateTime(0, 1, 1, 0, 0);
			CheckSerialize(time, default, "\"0000-01-01T00:00:00\"");

			time = new NodaTime.LocalDateTime(-250, 02, 27, 18, 42);
			CheckSerialize(time, default, "\"-0250-02-27T18:42:00\"");

			#endregion

			#region ZonedDateTime

			CheckSerialize(
				default(ZonedDateTime),
				default,
				"\"0001-01-01T00:00:00Z UTC\""
			);

			CheckSerialize(
				new ZonedDateTime(Instant.FromUtc(1988, 04, 19, 00, 35, 56), DateTimeZoneProviders.Tzdb["Europe/Paris"]),
				default,
				"\"1988-04-19T02:35:56+02 Europe/Paris\"" 
				// note: GMT+2
			);

			CheckSerialize(
				new ZonedDateTime(Instant.FromUtc(0, 1, 1, 0, 0), DateTimeZone.Utc),
				default,
				"\"0000-01-01T00:00:00Z UTC\""
			);

			CheckSerialize(
				new ZonedDateTime(Instant.FromUtc(-250, 02, 27, 18, 42), DateTimeZoneProviders.Tzdb["Africa/Cairo"]),
				default,
				"\"-0250-02-27T20:47:09+02:05:09 Africa/Cairo\"" 
				// note: gregorian calendars
			);

			// Intentionaly give it an ambiguous local time, in both ways.
			var zone = NodaTime.DateTimeZoneProviders.Tzdb["Europe/London"];
			CheckSerialize(
				new ZonedDateTime(new LocalDateTime(2012, 10, 28, 1, 30), zone, Offset.FromHours(1)),
				default,
				"\"2012-10-28T01:30:00+01 Europe/London\""
			);
			CheckSerialize(
				new ZonedDateTime(new LocalDateTime(2012, 10, 28, 1, 30), zone, Offset.FromHours(0)),
				default,
				"\"2012-10-28T01:30:00Z Europe/London\""
			);

			#endregion

			#region DateTimeZone

			CheckSerialize(DateTimeZone.Utc, default, "\"UTC\"");
			// with tzdb, the format is "Region/City"
			CheckSerialize(DateTimeZoneProviders.Tzdb["Europe/Paris"], default, "\"Europe/Paris\"");
			CheckSerialize(DateTimeZoneProviders.Tzdb["America/New_York"], default, "\"America/New_York\""); // spaces converted to '_'
			CheckSerialize(DateTimeZoneProviders.Tzdb["Asia/Tokyo"], default, "\"Asia/Tokyo\"");

			#endregion

			#region OffsetDateTime

			CheckSerialize(
				default(OffsetDateTime),
				default,
				"\"0001-01-01T00:00:00Z\"",
				""
			);

			CheckSerialize(
				new LocalDateTime(2012, 1, 2, 3, 4, 5, 6).PlusTicks(7).WithOffset(Offset.Zero),
				default,
				"\"2012-01-02T03:04:05.0060007Z\"",
				"Offset of 0 means UTC"
			);

			CheckSerialize(
				new LocalDateTime(2012, 1, 2, 3, 4, 5, 6).PlusTicks(7).WithOffset(Offset.FromHours(2)),
				default,
				"\"2012-01-02T03:04:05.0060007+02:00\"",
				"Only HH:MM for the timezone offset"
			);

			CheckSerialize(
				new LocalDateTime(2012, 1, 2, 3, 4, 5, 6).PlusTicks(7).WithOffset(Offset.FromHoursAndMinutes(-1, -30)),
				default,
				"\"2012-01-02T03:04:05.0060007-01:30\"",
				"Allow negative offsets"
			);

			CheckSerialize(
				new LocalDateTime(2012, 1, 2, 3, 4, 5, 6).PlusTicks(7).WithOffset(Offset.FromHoursAndMinutes(-1, -30) + Offset.FromMilliseconds(-1234)),
				default,
				"\"2012-01-02T03:04:05.0060007-01:30\"",
				"Seconds and milliseconds in timezone offset should be dropped"
			);

			#endregion

			#region Offset...

			CheckSerialize(Offset.Zero, default, "\"+00\"");
			CheckSerialize(Offset.FromHours(2), default, "\"+02\"");
			CheckSerialize(Offset.FromHoursAndMinutes(1, 30), default, "\"+01:30\"");
			CheckSerialize(Offset.FromHoursAndMinutes(-1, -30), default, "\"-01:30\"");

			#endregion
		}

		[Test]
		public void Test_JsonSerialize_DateTimeZone()
		{
			var rnd = new Random();
			int index = rnd.Next(NodaTime.DateTimeZoneProviders.Tzdb.Ids.Count);
			var id = NodaTime.DateTimeZoneProviders.Tzdb.Ids[index];
			var zone = NodaTime.DateTimeZoneProviders.Tzdb.GetZoneOrNull(id);
			CheckSerialize(zone, default, "\"" + id + "\"");
		}

		[Test]
		public void Test_JsonSerialize_Bytes()
		{
			CheckSerialize(default(byte[]), default, "null");
			CheckSerialize(Array.Empty<byte>(), default, @"""""");

			// note: binaries are encoded as Base64 text
			byte[] arrayOfBytes = [ 65, 0, 42, 255, 32 ];
			CheckSerialize(arrayOfBytes, default, @"""QQAq/yA=""");

			// random
			arrayOfBytes = new byte[16];
			new Random().NextBytes(arrayOfBytes);
			CheckSerialize(arrayOfBytes, default, "\"" + Convert.ToBase64String(arrayOfBytes) + "\"", "Random Data!");
		}

		[Test]
		public void Test_JsonSerialize_Slices()
		{
			CheckSerialize(Slice.Nil, default, "null");
			CheckSerialize(Slice.Empty, default, @"""""");

			var slice = new byte[] { 123, 123, 123, 65, 0, 42, 255, 32, 123 }.AsSlice(3, 5);
			CheckSerialize(slice, default, @"""QQAq/yA=""");

			// random
			var bytes = new byte[32];
			new Random().NextBytes(bytes);
			slice = bytes.AsSlice(8, 16);
			CheckSerialize(slice, default, "\"" + Convert.ToBase64String(bytes, 8, 16) + "\"", "Random Data!");
		}

		[Test]
		public void Test_JsonDeserialize_Bytes()
		{
			Assert.That(CrystalJson.Deserialize<byte[]?>("null", null), Is.Null);
			Assert.That(CrystalJson.Deserialize<byte[]>("\"\""), Is.EqualTo(Array.Empty<byte>()));

			// note: binaries are encoded as Base64 text
			Assert.That(CrystalJson.Deserialize<byte[]>("\"QQAq/yA=\""), Is.EqualTo(new byte[] { 65, 0, 42, 255, 32 }));

			// random
			var bytes = new byte[16];
			new Random().NextBytes(bytes);
			Assert.That(CrystalJson.Deserialize<byte[]>("\"" + Convert.ToBase64String(bytes) + "\""), Is.EqualTo(bytes), "Random Data!");
		}

		[Test]
		public void Test_JsonDeserialize_Slices()
		{
			Assert.That(CrystalJson.Deserialize<Slice>("null", default), Is.EqualTo(Slice.Nil));
			Assert.That(CrystalJson.Deserialize<Slice>(@""""""), Is.EqualTo(Slice.Empty));

			Assert.That(CrystalJson.Deserialize<Slice>(@"""QQAq/yA="""), Is.EqualTo(new byte[] { 65, 0, 42, 255, 32 }.AsSlice()));

			// random
			var bytes = new byte[32];
			new Random().NextBytes(bytes);
			Assert.That(CrystalJson.Deserialize<Slice>("\"" + Convert.ToBase64String(bytes) + "\""), Is.EqualTo(bytes.AsSlice()), "Random Data!");
		}

		[Test]
		public void Test_JsonSerialize_Dictionary()
		{
			// The keys are converted to string
			// - JSON target: keys must always be escaped with double quotes (")
			// - JavaScript target: keys are identifiers and usually do no require escaping, unless required, and in this case will use single quotes (')

			CheckSerialize(new Dictionary<string, string>(), default, "{ }");
			CheckSerialize(new Dictionary<string, string> { ["foo"] = "bar" }, default, """{ "foo": "bar" }""");

			CheckSerialize(new Dictionary<string, string>(), CrystalJsonSettings.JavaScript, "{ }");
			CheckSerialize(new Dictionary<string, string> { ["foo"] = "bar" }, CrystalJsonSettings.JavaScript, "{ foo: 'bar' }");

			var dicOfStrings = new Dictionary<string, string>
			{
				["foo"] = "bar",
				["narf"] = "zort",
				["123"] = "456",
				["all your bases"] = "are belong to us"
			};
			// JSON
			CheckSerialize(
				dicOfStrings,
				default,
				"""{ "foo": "bar", "narf": "zort", "123": "456", "all your bases": "are belong to us" }"""
			);
			CheckSerialize(
				dicOfStrings,
				CrystalJsonSettings.Json.Compacted(),
				"""{"foo":"bar","narf":"zort","123":"456","all your bases":"are belong to us"}"""
			);
			CheckSerialize(
				dicOfStrings,
				CrystalJsonSettings.Json.Indented(),
				"""
				{
					"foo": "bar",
					"narf": "zort",
					"123": "456",
					"all your bases": "are belong to us"
				}
				"""
			);
			// JS
			CheckSerialize(
				dicOfStrings,
				CrystalJsonSettings.JavaScript,
				"{ foo: 'bar', narf: 'zort', '123': '456', 'all your bases': 'are belong to us' }",
				"JavaScript"
			);

			var dicOfInts = new Dictionary<string, int>
			{
				["foo"] = 123,
				["bar"] = 456
			};
			CheckSerialize(dicOfInts, default, """{ "foo": 123, "bar": 456 }""");
			CheckSerialize(dicOfInts, CrystalJsonSettings.JavaScript, "{ 'foo': 123, 'bar': 456 }");

			var dicOfObjects = new Dictionary<string, Tuple<int, string>>
			{
				["foo"] = new(123, "bar"),
				["narf"] = new(456, "zort")
			};
			CheckSerialize(
				dicOfObjects,
				default,
				"""{ "foo": { "Item1": 123, "Item2": "bar" }, "narf": { "Item1": 456, "Item2": "zort" } }"""
			);
			CheckSerialize(
				dicOfObjects,
				CrystalJsonSettings.JavaScript,
				"{ 'foo': { Item1: 123, Item2: 'bar' }, 'narf': { Item1: 456, Item2: 'zort' } }"
			);

			var dicOfTuples = new Dictionary<string, (int, string)>
			{
				["foo"] = new(123, "bar"),
				["narf"] = new(456, "zort")
			};
			CheckSerialize(
				dicOfTuples,
				default,
				"""{ "foo": [ 123, "bar" ], "narf": [ 456, "zort" ] }"""
			);
			CheckSerialize(
				dicOfTuples,
				CrystalJsonSettings.JavaScript,
				"{ 'foo': [ 123, 'bar' ], 'narf': [ 456, 'zort' ] }"
			);

		}

		[Test]
		public void Test_JsonDeserialize_Dictionary()
		{
			// key => string
			var obj = JsonObject.Parse("""{ "hello": "World", "foo": 123, "bar": true }""");
			Assert.That(obj, Is.Not.Null.And.InstanceOf<JsonObject>());

			var dic = obj.Required<Dictionary<string, string>>();
			Assert.That(dic, Is.Not.Null);

			Assert.That(dic.ContainsKey("hello"), Is.True, "dic[hello]");
			Assert.That(dic.ContainsKey("foo"), Is.True, "dic[foo]");
			Assert.That(dic.ContainsKey("bar"), Is.True, "dic[bar]");

			Assert.That(dic["hello"], Is.EqualTo("World"));
			Assert.That(dic["foo"], Is.EqualTo("123"));
			Assert.That(dic["bar"], Is.EqualTo("true"));

			Assert.That(dic, Has.Count.EqualTo(3));

			// key => int
			obj = JsonObject.Parse("""{ "1": "Hello World", "42": "Narf!", "007": "James Bond" }""");
			Assert.That(obj, Is.Not.Null.And.InstanceOf<JsonObject>());

			var dicInt = obj.Required<Dictionary<int, string>>();
			Assert.That(dicInt, Is.Not.Null);

			Assert.That(dicInt.ContainsKey(1), Is.True, "dicInt[1]");
			Assert.That(dicInt.ContainsKey(7), Is.True, "dicInt[7]");
			Assert.That(dicInt.ContainsKey(42), Is.True, "dicInt[42]");

			Assert.That(dicInt[1], Is.EqualTo("Hello World"));
			Assert.That(dicInt[7], Is.EqualTo("James Bond"));
			Assert.That(dicInt[42], Is.EqualTo("Narf!"));

			Assert.That(dicInt, Has.Count.EqualTo(3));
		}

		[Test]
		public void Test_JsonSerialize_Composite()
		{
			var composite = new
			{
				Id = 1,
				Title = "The Big Bang Theory",
				Cancelled = false,
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
				PilotAirDate = new DateTime(2007, 9, 24, 0, 0, 0, DateTimeKind.Utc), // easier with UTC dates
			};

			// JSON
			CheckSerialize(
				composite,
				default,
				"""{ "Id": 1, "Title": "The Big Bang Theory", "Cancelled": false, "Cast": [ { "Character": "Sheldon Cooper", "Actor": "Jim Parsons", "Female": false }, { "Character": "Leonard Hofstadter", "Actor": "Johny Galecki", "Female": false }, { "Character": "Penny", "Actor": "Kaley Cuoco", "Female": true }, { "Character": "Howard Wolowitz", "Actor": "Simon Helberg", "Female": false }, { "Character": "Raj Koothrappali", "Actor": "Kunal Nayyar", "Female": false } ], "Seasons": 4, "ScoreIMDB": 8.4, "Producer": "Chuck Lorre Productions", "PilotAirDate": "2007-09-24T00:00:00Z" }"""
			);

			// JS
			CheckSerialize(
				composite,
				CrystalJsonSettings.JavaScript,
				"{ Id: 1, Title: 'The Big Bang Theory', Cancelled: false, Cast: [ { Character: 'Sheldon Cooper', Actor: 'Jim Parsons', Female: false }, { Character: 'Leonard Hofstadter', Actor: 'Johny Galecki', Female: false }, { Character: 'Penny', Actor: 'Kaley Cuoco', Female: true }, { Character: 'Howard Wolowitz', Actor: 'Simon Helberg', Female: false }, { Character: 'Raj Koothrappali', Actor: 'Kunal Nayyar', Female: false } ], Seasons: 4, ScoreIMDB: 8.4, Producer: 'Chuck Lorre Productions', PilotAirDate: new Date(1190592000000) }"
			);
		}

		[Test]
		public void Test_JsonSerialize_DataContract()
		{
			CheckSerialize(new DummyDataContractClass(), default, """{ "Id": 0, "Age": 0, "IsFemale": false, "VisibleProperty": "CanBeSeen" }""");
			// with explicit nulls
			CheckSerialize(new DummyDataContractClass(), CrystalJsonSettings.Json.WithNullMembers(), """{ "Id": 0, "Name": null, "Age": 0, "IsFemale": false, "CurrentLoveInterest": null, "VisibleProperty": "CanBeSeen" }""");

			CheckSerialize(
				new DummyDataContractClass
				{
					AgentId = 7,
					Name = "James Bond",
					Age = 69,
					Female = false,
					CurrentLoveInterest = "Miss Moneypenny",
					InvisibleField = "007",
				},
				default,
				"""{ "Id": 7, "Name": "James Bond", "Age": 69, "IsFemale": false, "CurrentLoveInterest": "Miss Moneypenny", "VisibleProperty": "CanBeSeen" }"""
			);
		}

		[Test]
		public void Test_JsonSerialize_XmlIgnore()
		{

			CheckSerialize(
				new DummyXmlSerializableContractClass(),
				default,
				"""{ "Id": 0, "Age": 0, "IsFemale": false, "VisibleProperty": "CanBeSeen" }"""
			);
			CheckSerialize(
				new DummyXmlSerializableContractClass(),
				CrystalJsonSettings.Json.WithNullMembers(),
				"""{ "Id": 0, "Name": null, "Age": 0, "IsFemale": false, "CurrentLoveInterest": null, "VisibleProperty": "CanBeSeen" }"""
			);

			CheckSerialize(
				new DummyXmlSerializableContractClass()
				{
					AgentId = 7,
					Name = "James Bond",
					Age = 69,
					CurrentLoveInterest = "Miss Moneypenny",
					InvisibleField = "007",
				},
				default,
				"""{ "Id": 7, "Name": "James Bond", "Age": 69, "IsFemale": false, "CurrentLoveInterest": "Miss Moneypenny", "VisibleProperty": "CanBeSeen" }"""
			);
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
				var str = string.Empty;
				for (int k = 0; k < rounds; k++)
				{
					str += new string((char)(rnd.Next(64) + 33), 4);
				}
				list.Add(str);
			}

			{ // WARMUP
				var x = CrystalJson.ToSlice(list);
				_ = x.ZstdCompress(0);
				_ = x.DeflateCompress(CompressionLevel.Optimal);
				_ = x.GzipCompress(CompressionLevel.Optimal);
			}

			// Clear Text

			string path =  GetTemporaryPath("foo.json");
			File.Delete(path);

			Log($"Writing to {path}");
			var sw = Stopwatch.StartNew();
			CrystalJson.SaveTo(path, list, CrystalJsonSettings.JsonCompact.ExpectLargeData());
			sw.Stop();
			Assert.That(File.Exists(path), Is.True, "Should have created a file at " + path);
			long rawSize = new FileInfo(path).Length;
			Log($"RAW    : Saved {rawSize,9:N0} bytes in {sw.Elapsed.TotalMilliseconds:N1} ms");

			// read the file back
			string text = File.ReadAllText(path);
			Assert.That(text, Is.Not.Null.Or.Empty, "File should contain stuff");
			// deserialize
			var reloaded = CrystalJson.Deserialize<string[]>(text);
			Assert.That(reloaded, Is.Not.Null);
			Assert.That(reloaded, Has.Length.EqualTo(list.Count));
			for (int i = 0; i < list.Count; i++)
			{
				Assert.That(reloaded[i], Is.EqualTo(list[i]), $"Mismatch at index {i}");
			}

			{ // Compress, Deflate
				path = GetTemporaryPath("foo.json.deflate");
				File.Delete(path);

				sw.Restart();
				var data = CrystalJson.ToSlice(list, CrystalJsonSettings.JsonCompact.ExpectLargeData());
				using (var fs = File.Create(path))
				{
					fs.Write(data.DeflateCompress(CompressionLevel.Optimal).Span);
				}
				sw.Stop();
				Assert.That(File.Exists(path), Is.True, "Should have created a file");
				Log($"Deflate: Saved {new FileInfo(path).Length,9:N0} bytes (1 : {(1.0 * rawSize / new FileInfo(path).Length):F2}) in {sw.Elapsed.TotalMilliseconds:N1} ms");
			}

			{ // Compress, GZip
				path = GetTemporaryPath("foo.json.gz");
				File.Delete(path);

				sw.Restart();
				var data = CrystalJson.ToSlice(list, CrystalJsonSettings.JsonCompact.ExpectLargeData());
				using (var fs = File.Create(path))
				{
					fs.Write(data.GzipCompress(CompressionLevel.Optimal).Span);
				}
				sw.Stop();

				Assert.That(File.Exists(path), Is.True, "Should have created a file");
				Log($"GZip -5: Saved {new FileInfo(path).Length,9:N0} bytes (1 : {(1.0 * rawSize / new FileInfo(path).Length):F2}) in {sw.Elapsed.TotalMilliseconds:N1} ms");
			}

			{ // Compress, ZSTD -1
				path = GetTemporaryPath("foo.json.1.zstd");
				File.Delete(path);

				sw.Restart();
				var data = CrystalJson.ToSlice(list, CrystalJsonSettings.JsonCompact.ExpectLargeData());
				File.WriteAllBytes(path, data.ZstdCompress(1).GetBytes()!);
				sw.Stop();
				Assert.That(File.Exists(path), Is.True, "Should have created a file");
				Log($"ZSTD -1: Saved {new FileInfo(path).Length,9:N0} bytes (1 : {(1.0 * rawSize / new FileInfo(path).Length):F2}) in {sw.Elapsed.TotalMilliseconds:N1} ms");
			}
			{ // Compress, ZSTD -3
				path = GetTemporaryPath("foo.json.3.zstd");
				File.Delete(path);

				sw.Restart();
				var data = CrystalJson.ToSlice(list, CrystalJsonSettings.JsonCompact.ExpectLargeData());
				File.WriteAllBytes(path, data.ZstdCompress(3).GetBytes()!);
				sw.Stop();
				Assert.That(File.Exists(path), Is.True, "Should have created a file");
				Log($"ZSTD -3: Saved {new FileInfo(path).Length,9:N0} bytes (1 : {(1.0 * rawSize / new FileInfo(path).Length):F2}) in {sw.Elapsed.TotalMilliseconds:N1} ms");
			}
			{ // Compress, ZSTD -5
				path = GetTemporaryPath("foo.json.5.zstd");
				File.Delete(path);

				sw.Restart();
				var data = CrystalJson.ToSlice(list, CrystalJsonSettings.JsonCompact.ExpectLargeData());
				File.WriteAllBytes(path, data.ZstdCompress(5).GetBytes()!);
				sw.Stop();
				Assert.That(File.Exists(path), Is.True, "Should have created a file");
				Log($"ZSTD -5: Saved {new FileInfo(path).Length,9:N0} bytes (1 : {(1.0 * rawSize / new FileInfo(path).Length):F2}) in {sw.Elapsed.TotalMilliseconds:N1} ms");
			}
			{ // Compress, ZSTD -9
				path = GetTemporaryPath("foo.json.9.zstd");
				File.Delete(path);

				sw.Restart();
				var data = CrystalJson.ToSlice(list, CrystalJsonSettings.JsonCompact.ExpectLargeData());
				File.WriteAllBytes(path, data.ZstdCompress(9).GetBytes()!);
				sw.Stop();
				Assert.That(File.Exists(path), Is.True, "Should have created a file");
				Log($"ZSTD -9: Saved {new FileInfo(path).Length,9:N0} bytes (1 : {(1.0 * rawSize / new FileInfo(path).Length):F2}) in {sw.Elapsed.TotalMilliseconds:N1} ms");
			}
			{ // Compress, ZSTD -20
				path = GetTemporaryPath("foo.json.20.zstd");
				File.Delete(path);

				sw.Restart();
				var data = CrystalJson.ToSlice(list, CrystalJsonSettings.JsonCompact.ExpectLargeData());
				File.WriteAllBytes(path, data.ZstdCompress(20).GetBytes()!);
				sw.Stop();
				Assert.That(File.Exists(path), Is.True, "Should have created a file");
				Log($"ZSTD -20: Saved {new FileInfo(path).Length,9:N0} bytes (1 : {(1.0 * rawSize / new FileInfo(path).Length):F2}) in {sw.Elapsed.TotalMilliseconds:N1} ms");
			}
		}

		[Test]
		public void Test_JsonSerialize_Version()
		{
			CheckSerialize(new Version(1, 0), default, "\"1.0\"");
			CheckSerialize(new Version(1, 2, 3), default, "\"1.2.3\"");
			CheckSerialize(new Version(1, 2, 3, 4), default, "\"1.2.3.4\"");
			CheckSerialize(new Version(int.MaxValue, int.MaxValue, int.MaxValue, int.MaxValue), default, "\"2147483647.2147483647.2147483647.2147483647\"");
		}

		[Test]
		public void Test_JsonSerialize_KeyValuePair()
		{
			// KeyValuePair<K, V> instances, outside a dictionary, will be serialized as the array '[KEY, VALUE]', instead of '{ "Key": KEY, "Value": VALUE }', because it is more compact.
			// The only exception is for a collection of KV pairs (all with the same type), which are serialized as an object

			CheckSerialize(new KeyValuePair<string, int>("hello", 42), CrystalJsonSettings.Json, """[ "hello", 42 ]""");
			CheckSerialize(new KeyValuePair<int, bool>(123, true), CrystalJsonSettings.Json, "[ 123, true ]");

			CheckSerialize(default(KeyValuePair<string, int>), CrystalJsonSettings.Json, "[ null, 0 ]");
			CheckSerialize(default(KeyValuePair<int, bool>), CrystalJsonSettings.Json, "[ 0, false ]");

			CheckSerialize(default(KeyValuePair<string, int>), CrystalJsonSettings.Json.WithoutDefaultValues(), "[ null, 0 ]");
			CheckSerialize(default(KeyValuePair<int, bool>), CrystalJsonSettings.Json.WithoutDefaultValues(), "[ 0, false ]");

			var nested = KeyValuePair.Create(KeyValuePair.Create("hello", KeyValuePair.Create("narf", 42)), KeyValuePair.Create(123, KeyValuePair.Create("zort", TimeSpan.Zero)));
			CheckSerialize(nested, CrystalJsonSettings.Json, """[ [ "hello", [ "narf", 42 ] ], [ 123, [ "zort", 0 ] ] ]""");
		}

		[Test]
		public void Test_JsonValue_FromValue_KeyValuePair()
		{
			// KeyValuePair<K, V> instances, outside a dictionary, will be serialized as the array '[KEY, VALUE]', instead of '{ "Key": KEY, "Value": VALUE }', because it is more compact.
			// The only exception is for a collection of KV pairs (all with the same type), which are serialized as an object

			Assert.That(JsonValue.FromValue(new KeyValuePair<string, int>("hello", 42)).ToJson(), Is.EqualTo("""[ "hello", 42 ]"""));
			Assert.That(JsonValue.FromValue(new KeyValuePair<int, bool>(123, true)).ToJson(), Is.EqualTo("[ 123, true ]"));

			Assert.That(JsonValue.FromValue(default(KeyValuePair<string, int>)).ToJson(), Is.EqualTo("[ null, 0 ]"));
			Assert.That(JsonValue.FromValue(default(KeyValuePair<int, bool>)).ToJson(), Is.EqualTo("[ 0, false ]"));

			var nested = KeyValuePair.Create(KeyValuePair.Create("hello", KeyValuePair.Create("narf", 42)), KeyValuePair.Create(123, KeyValuePair.Create("zort", TimeSpan.Zero)));
			Assert.That(JsonValue.FromValue(nested).ToJson(), Is.EqualTo("""[ [ "hello", [ "narf", 42 ] ], [ 123, [ "zort", 0 ] ] ]"""));
		}

		[Test]
		public void Test_JsonDeserialize_KeyValuePair()
		{
			// array variant: [Key, Value]
			Assert.That(CrystalJson.Deserialize<KeyValuePair<string, int>>("""["hello",42]"""), Is.EqualTo(KeyValuePair.Create("hello", 42)));
			Assert.That(CrystalJson.Deserialize<KeyValuePair<int, bool>>("[123,true]"), Is.EqualTo(KeyValuePair.Create(123, true)));

			Assert.That(CrystalJson.Deserialize<KeyValuePair<string, int>>("[null,0]"), Is.EqualTo(default(KeyValuePair<string, int>)));
			Assert.That(CrystalJson.Deserialize<KeyValuePair<string, int>>("[]"), Is.EqualTo(default(KeyValuePair<string, int>)));
			Assert.That(CrystalJson.Deserialize<KeyValuePair<int, bool>>("[0,false]"), Is.EqualTo(default(KeyValuePair<int, bool>)));
			Assert.That(CrystalJson.Deserialize<KeyValuePair<int, bool>>("[]"), Is.EqualTo(default(KeyValuePair<int, bool>)));

			Assert.That(() => CrystalJson.Deserialize<KeyValuePair<string, int>>("""["hello",123,true]"""), Throws.InstanceOf<InvalidOperationException>());
			Assert.That(() => CrystalJson.Deserialize<KeyValuePair<string, int>>("""["hello"]"""), Throws.InstanceOf<InvalidOperationException>());

			// object-variant: {Key:.., Value:..}
			Assert.That(CrystalJson.Deserialize<KeyValuePair<string, int>>("""{ "Key": "hello", "Value": 42 }"""), Is.EqualTo(KeyValuePair.Create("hello", 42)));
			Assert.That(CrystalJson.Deserialize<KeyValuePair<int, bool>>("""{ "Key": 123, "Value": true }]"""), Is.EqualTo(KeyValuePair.Create(123, true)));

			Assert.That(CrystalJson.Deserialize<KeyValuePair<string, int>>("""{ "Key": null, "Value": 0 }"""), Is.EqualTo(default(KeyValuePair<string, int>)));
			Assert.That(CrystalJson.Deserialize<KeyValuePair<string, int>>("{}"), Is.EqualTo(default(KeyValuePair<string, int>)));
			Assert.That(CrystalJson.Deserialize<KeyValuePair<int, bool>>("""{ "Key": 0, "Value": false }"""), Is.EqualTo(default(KeyValuePair<int, bool>)));
			Assert.That(CrystalJson.Deserialize<KeyValuePair<int, bool>>("{}"), Is.EqualTo(default(KeyValuePair<int, bool>)));
		}

		[Test]
		public void Test_JsonSerialize_Uri()
		{
			CheckSerialize(default(Uri), default, "null");
			CheckSerialize(
				new Uri("http://google.com"),
				default,
				@"""http://google.com"""
			);
			CheckSerialize(
				new Uri("https://www.youtube.com/watch?v=dQw4w9WgXcQ"),
				default,
				@"""https://www.youtube.com/watch?v=dQw4w9WgXcQ"""
			);
			CheckSerialize(
				new Uri("ftp://root:hunter2@ftp.corporate.com/public/_/COM1:/_/__/Warez/MovieZ/Valhalla_Rising_(2009)_1080p_BrRip_x264_-_YIFY.mkv"),
				default,
				@"""ftp://root:hunter2@ftp.corporate.com/public/_/COM1:/_/__/Warez/MovieZ/Valhalla_Rising_(2009)_1080p_BrRip_x264_-_YIFY.mkv"""
			);
		}

		[Test]
		public void Test_JsonDeserialize_Uri()
		{
			Assert.That(CrystalJson.Deserialize<Uri>(@"""http://google.com"""), Is.EqualTo(new Uri("http://google.com")));
			Assert.That(CrystalJson.Deserialize<Uri>(@"""http://www.doxense.com/"""), Is.EqualTo(new Uri("http://www.doxense.com/")));
			Assert.That(CrystalJson.Deserialize<Uri>(@"""https://www.youtube.com/watch?v=dQw4w9WgXcQ"""), Is.EqualTo(new Uri("https://www.youtube.com/watch?v=dQw4w9WgXcQ")));
			Assert.That(CrystalJson.Deserialize<Uri>(@"""ftp://root:hunter2@ftp.corporate.com/public/_/COM1:/_/__/Warez/MovieZ/Valhalla_Rising_(2009)_1080p_BrRip_x264_-_YIFY.mkv"""), Is.EqualTo(new Uri("ftp://root:hunter2@ftp.corporate.com/public/_/COM1:/_/__/Warez/MovieZ/Valhalla_Rising_(2009)_1080p_BrRip_x264_-_YIFY.mkv")));
			Assert.That(() => CrystalJson.Deserialize<Uri>("null"), Throws.InstanceOf<JsonBindingException>());
			Assert.That(CrystalJson.Deserialize<Uri?>("null", null), Is.EqualTo(default(Uri)));
		}

		static void Verify_TryFormat(JsonValue literal)
		{
			string expected = literal.ToJson();
			Log($"# `{literal:P}` => `{expected}`");

			{ // with more space than required
				Span<char> buf = new char[expected.Length + 10];
				buf.Fill('無');
				Assert.That(literal.TryFormat(buf, out int charsWritten, default, null), Is.True, $"{literal}.TryFormat([{buf.Length}])");
				Assert.That(buf.Slice(0, charsWritten).ToString(), Is.EqualTo(expected), $"{literal}.TryFormat([{buf.Length}])");
				Assert.That(charsWritten, Is.EqualTo(expected.Length), $"{literal}.TryFormat([{buf.Length}])");
				Assert.That(buf.Slice(charsWritten).ToString(), Is.EqualTo(new string('無', buf.Length - charsWritten)), $"{literal}.TryFormat([{buf.Length}])");
			}

			{ // with exactly enough space
				Span<char> buf = new char[expected.Length + 10];
				buf.Fill('無');
				var exactSize = buf.Slice(0, expected.Length);
				Assert.That(literal.TryFormat(exactSize, out int charsWritten, default, null), Is.True, $"{literal}.TryFormat([{exactSize.Length}])");
				Assert.That(charsWritten, Is.EqualTo(expected.Length), $"{literal}.TryFormat([{exactSize.Length}])");
				Assert.That(exactSize.ToString(), Is.EqualTo(expected), $"{literal}.TryFormat([{exactSize.Length}])");
				Assert.That(buf.Slice(exactSize.Length).ToString(), Is.EqualTo(new string('無', buf.Length - exactSize.Length)), $"{literal}.TryFormat([{exactSize.Length}])");
			}

			{ // with empty buffer
				Span<char> buf = new char[expected.Length];
				buf.Fill('無');
				var empty = Span<char>.Empty;
				Assert.That(literal.TryFormat(empty, out int charsWritten, default, null), Is.False, $"{literal}.TryFormat([0])");
				Assert.That(charsWritten, Is.Zero, $"{literal}.TryFormat([0])");
				Assert.That(buf.ToString(), Is.EqualTo(new string('無', buf.Length)), $"{literal}.TryFormat([0])");
			}

			{ // with not enough space
				Span<char> buf = new char[expected.Length];
				buf.Fill('無');
				// buffer is too short by 3 characters
				var tooShort = buf.Slice(0, Math.Max(expected.Length - 3, 0));
				Assert.That(literal.TryFormat(tooShort, out int charsWritten, default, null), Is.False, $"{literal}.TryFormat([{tooShort.Length}])");
				Assert.That(charsWritten, Is.Zero, $"{literal}.TryFormat([{tooShort.Length}])");
				Assert.That(buf.Slice(tooShort.Length).ToString(), Is.EqualTo(new string('無', buf.Length - tooShort.Length)), $"{literal}.TryFormat([{tooShort.Length}])");
			}
		}

		[Test]
		public void Test_JsonString_TryFormat()
		{
			Verify_TryFormat(JsonString.Empty);

			Verify_TryFormat("a");
			Verify_TryFormat("hello");

			Verify_TryFormat("\"");
			Verify_TryFormat("hello\"world");
			Verify_TryFormat("Héllö, 世界!");

			Verify_TryFormat("\0");
			Verify_TryFormat("\x12");
			Verify_TryFormat("\x7F");
			Verify_TryFormat("\xFF");
			Verify_TryFormat("\uDF34");
			Verify_TryFormat("\uFFFE");
			Verify_TryFormat("\uFFFF");

			Verify_TryFormat(Slice.Random(Random.Shared, 1024).ToBase64());
		}

		[Test]
		public void Test_JsonNumber_TryFormat()
		{
			Verify_TryFormat(JsonNumber.Zero);
			Verify_TryFormat(JsonNumber.DecimalZero);
			Verify_TryFormat(JsonNumber.One);
			Verify_TryFormat(JsonNumber.DecimalOne);
			Verify_TryFormat(JsonNumber.MinusOne);

			Verify_TryFormat(123);
			Verify_TryFormat(-123);
			Verify_TryFormat(123d);
			Verify_TryFormat(12.3);
			Verify_TryFormat(0.123);

			Verify_TryFormat(Math.PI);
			Verify_TryFormat(double.NaN);
			Verify_TryFormat(double.PositiveInfinity);
			Verify_TryFormat(double.NegativeInfinity);
			Verify_TryFormat(double.Epsilon);

			Verify_TryFormat(JsonValue.Parse("123"));
			Verify_TryFormat(JsonValue.Parse("123.0"));
			Verify_TryFormat(JsonValue.Parse("123.000"));
		}

		[Test]
		public void Test_JsonBoolean_TryFormat()
		{
			Verify_TryFormat(JsonBoolean.False);
			Verify_TryFormat(JsonBoolean.True);
		}

		[Test]
		public void Test_JsonDateTime_TryFormat()
		{
			Verify_TryFormat(DateTime.MinValue);
			Verify_TryFormat(DateTime.MaxValue);
			Verify_TryFormat(DateTime.Now);
			Verify_TryFormat(DateTime.Now.Date);
			Verify_TryFormat(DateTime.UtcNow);
			Verify_TryFormat(DateTime.UtcNow.Date);
			Verify_TryFormat(DateOnly.MinValue);
			Verify_TryFormat(DateOnly.MaxValue);
			Verify_TryFormat(DateOnly.FromDateTime(DateTime.Now));
		}

		[Test]
		public void Test_PropertyNames_CrystalJson()
		{
			var instance = new DummyCrystalJsonTextPropertyNames()
			{
				HelloWorld = "hello", // => "helloWorld": "hello"
				Foo = "world",        // => "bar": "world"
			};

			var json = CrystalJson.Serialize(instance, CrystalJsonSettings.Json);
			Log(json);
			Assert.That(json, Is.EqualTo("{ \"helloWorld\": \"hello\", \"bar\": \"world\" }"));

			var obj = JsonObject.FromObject(instance);
			Dump(obj);
			Assert.That(obj["helloWorld"], IsJson.EqualTo("hello"));
			Assert.That(obj["bar"], IsJson.EqualTo("world"));

			var decoded = obj.As<DummyCrystalJsonTextPropertyNames>()!;
			Assert.That(decoded.HelloWorld, Is.EqualTo("hello"));
			Assert.That(decoded.Foo, Is.EqualTo("world"));
		}

		[Test]
		public void Test_PropertyNames_SystemTextJson()
		{
			var instance = new DummySystemJsonTextPropertyNames()
			{
				HelloWorld = "hello", // => "helloWorld": "hello"
				Foo = "world",        // => "bar": "world"
			};

			var json = CrystalJson.Serialize(instance, CrystalJsonSettings.Json);
			Log(json);
			Assert.That(json, Is.EqualTo("{ \"helloWorld\": \"hello\", \"bar\": \"world\" }"));

			var obj = JsonObject.FromObject(instance);
			Dump(obj);
			Assert.That(obj["helloWorld"], IsJson.EqualTo("hello"));
			Assert.That(obj["bar"], IsJson.EqualTo("world"));

			var decoded = obj.As<DummySystemJsonTextPropertyNames>()!;
			Assert.That(decoded.HelloWorld, Is.EqualTo("hello"));
			Assert.That(decoded.Foo, Is.EqualTo("world"));
		}

		[Test]
		public void Test_PropertyNames_NewtonsoftJson()
		{
			var instance = new DummyNewtonsoftJsonPropertyNames()
			{
				HelloWorld = "hello", // => "helloWorld": "hello"
				Foo = "world",        // => "bar": "world"
			};

			var json = CrystalJson.Serialize(instance, CrystalJsonSettings.Json);
			Log(json);
			Assert.That(json, Is.EqualTo("{ \"helloWorld\": \"hello\", \"bar\": \"world\" }"));

			var obj = JsonObject.FromObject(instance);
			Dump(obj);
			Assert.That(obj["helloWorld"], IsJson.EqualTo("hello"));
			Assert.That(obj["bar"], IsJson.EqualTo("world"));

			var decoded = obj.As<DummyNewtonsoftJsonPropertyNames>()!;
			Assert.That(decoded.HelloWorld, Is.EqualTo("hello"));
			Assert.That(decoded.Foo, Is.EqualTo("world"));
		}

		#endregion

		#region JSON Object Model...

		#region JsonNull...

		[Test]
		public void Test_JsonNull_Explicit()
		{
			var jnull = JsonNull.Null;
			Assert.That(jnull, Is.Not.Null);
			Assert.That(jnull, Is.InstanceOf<JsonNull>());
			Assert.That(JsonNull.Null, Is.SameAs(jnull), "JsonNull.Null should be a singleton");

			var value = (JsonNull) jnull;
			Assert.Multiple(() =>
			{
				Assert.That(value.Type, Is.EqualTo(JsonType.Null), "value.Type");
				Assert.That(value.IsNull, Is.True, "value.IsNull");
				Assert.That(value.IsMissing, Is.False, "value.IsMissing");
				Assert.That(value.IsError, Is.False, "value.IsError");
				Assert.That(value.IsDefault, Is.True, "value.IsDefault");
				Assert.That(value.ToObject(), Is.Null, "value.ToObject()");
				Assert.That(value.ToString(), Is.EqualTo(""), "value.ToString()");
			});

			Assert.Multiple(() =>
			{
				Assert.That(value.Equals(JsonNull.Null), Is.True, "EQ null");
				Assert.That(value.Equals(JsonNull.Missing), Is.False, "NEQ missing");
				Assert.That(value.Equals(JsonNull.Error), Is.False, "NEQ error");
				Assert.That(value.Equals(default(JsonValue)), Is.True);
				Assert.That(value.Equals(default(object)), Is.True);

				Assert.That(value.Equals(0), Is.False);
				Assert.That(value.Equals(123), Is.False);
				Assert.That(value.Equals(false), Is.False);
				Assert.That(value.Equals(""), Is.False);
				Assert.That(value.Equals("hello"), Is.False);
				Assert.That(value.Equals(JsonObject.Create()), Is.False);
				Assert.That(value.Equals(JsonArray.Create()), Is.False);

				Assert.That(value.StrictEquals(JsonNull.Null), Is.True, "EQ null");
				Assert.That(value.StrictEquals(JsonNull.Missing), Is.False, "NEQ missing");
				Assert.That(value.StrictEquals(JsonNull.Error), Is.False, "NEQ error");
				Assert.That(value.StrictEquals(false), Is.False);
				Assert.That(value.StrictEquals(0), Is.False);
				Assert.That(value.StrictEquals(""), Is.False);
				Assert.That(value.StrictEquals("null"), Is.False);

				Assert.That(value.CompareTo(default(JsonValue)), Is.EqualTo(0));
				Assert.That(value.CompareTo(JsonNull.Null), Is.EqualTo(0));
				Assert.That(value.CompareTo(JsonNull.Missing), Is.EqualTo(-1));
				Assert.That(value.CompareTo(JsonNull.Error), Is.EqualTo(-1));
				Assert.That(value.CompareTo(0), Is.EqualTo(-1));
				Assert.That(value.CompareTo(123), Is.EqualTo(-1));
				Assert.That(value.CompareTo(""), Is.EqualTo(-1));
				Assert.That(value.CompareTo("hello"), Is.EqualTo(-1));
			});

			// we must check a few corner cases when binding Null:
			// - for Value Types, null must bind into default(T) (ex: JsonNull.Null.As<int>() => 0)
			// - for JsonValue or JsonNull types, it must bind into the JsonNull.Null singleton (and not return a null reference!)
			// - for all other types, it should bind into a null reference

			Assert.Multiple(() =>
			{ // Bind(typeof(T), ...)
				Assert.That(jnull.Bind<string>(), Is.Null);
				Assert.That(jnull.Bind<int>(), Is.Zero);
				Assert.That(jnull.Bind<bool>(), Is.False);
				Assert.That(jnull.Bind<Guid>(), Is.EqualTo(Guid.Empty));
				Assert.That(jnull.Bind<int?>(), Is.Null);
				Assert.That(jnull.Bind<string[]>(), Is.Null);
				Assert.That(jnull.Bind<List<string>>(), Is.Null);
				Assert.That(jnull.Bind<IList<string>>(), Is.Null);

				// special case
				Assert.That(jnull.Bind<JsonNull>(), Is.SameAs(JsonNull.Null), "JsonNull.Bind<JsonNull>() should bind to JsonNull.Null, and not 'null' !");
				Assert.That(jnull.Bind<JsonValue>(), Is.SameAs(JsonNull.Null), "JsonNull.Bind<JsonValue>() should bind to JsonNull.Null, and not 'null' !");
				Assert.That(jnull.Bind<JsonString>(), Is.Null, "JsonNull.Bind<JsonString>() should return null, because a JsonString instance cannot represent null itself!");
				Assert.That(jnull.Bind<JsonNumber>(), Is.Null, "JsonNull.Bind<JsonNumber>() should return null, because a JsonNumber instance cannot represent null itself!");
				Assert.That(jnull.Bind<JsonBoolean>(), Is.Null, "JsonNull.Bind<JsonBoolean>() should return null, because a JsonBoolean instance cannot represent null itself!");
				Assert.That(jnull.Bind<JsonObject>(), Is.Null, "JsonNull.Bind<JsonObject>() should return null, because a JsonObject instance cannot represent null itself!");
				Assert.That(jnull.Bind<JsonArray>(), Is.Null, "JsonNull.Bind<JsonArray>() should return null, because a JsonArray instance cannot represent null itself!");
			});

			Assert.Multiple(() =>
			{ // Required<T>()
				Assert.That(() => jnull.Required<string>(), Throws.InstanceOf<JsonBindingException>());
				Assert.That(() => jnull.Required<int>(), Throws.InstanceOf<JsonBindingException>());
				Assert.That(() => jnull.Required<bool>(), Throws.InstanceOf<JsonBindingException>());
				Assert.That(() => jnull.Required<Guid>(), Throws.InstanceOf<JsonBindingException>());
				Assert.That(() => jnull.Required<int?>(), Throws.InstanceOf<JsonBindingException>());
				Assert.That(() => jnull.Required<string[]>(), Throws.InstanceOf<JsonBindingException>());
				Assert.That(() => jnull.Required<List<string>>(), Throws.InstanceOf<JsonBindingException>());
				Assert.That(() => jnull.Required<IList<string>>(), Throws.InstanceOf<JsonBindingException>());
				Assert.That(() => jnull.Required<JsonNull>(), Throws.InstanceOf<JsonBindingException>());
				Assert.That(() => jnull.Required<JsonValue>(), Throws.InstanceOf<JsonBindingException>());
				Assert.That(() => jnull.Required<JsonString>(), Throws.InstanceOf<JsonBindingException>());
			});

			Assert.Multiple(() =>
			{ // OrDefault<T>()
				Assert.That(jnull.As<string>(), Is.Null);
				Assert.That(jnull.As<int>(), Is.Zero);
				Assert.That(jnull.As<bool>(), Is.False);
				Assert.That(jnull.As<Guid>(), Is.EqualTo(Guid.Empty));
				Assert.That(jnull.As<int?>(), Is.Null);
				Assert.That(jnull.As<string[]>(), Is.Null);
				Assert.That(jnull.As<List<string>>(), Is.Null);
				Assert.That(jnull.As<IList<string>>(), Is.Null);

				// special case
				Assert.That(jnull.As<JsonNull>(), Is.SameAs(JsonNull.Null), "JsonNull.OrDefault<JsonNull>() should bind to JsonNull.Null, and not 'null' !");
				Assert.That(jnull.As<JsonValue>(), Is.SameAs(JsonNull.Null), "JsonNull.OrDefault<JsonValue>() should bind to JsonNull.Null, and not 'null' !");
				Assert.That(jnull.As<JsonString>(), Is.Null, "JsonNull.OrDefault<JsonString>() should return null, because a JsonString instance cannot represent null itself!");
				Assert.That(jnull.As<JsonNumber>(), Is.Null, "JsonNull.OrDefault<JsonNumber>() should return null, because a JsonNumber instance cannot represent null itself!");
				Assert.That(jnull.As<JsonBoolean>(), Is.Null, "JsonNull.OrDefault<JsonBoolean>() should return null, because a JsonBoolean instance cannot represent null itself!");
				Assert.That(jnull.As<JsonObject>(), Is.Null, "JsonNull.OrDefault<JsonObject>() should return null, because a JsonObject instance cannot represent null itself!");
				Assert.That(jnull.As<JsonArray>(), Is.Null, "JsonNull.OrDefault<JsonArray>() should return null, because a JsonArray instance cannot represent null itself!");
			});

			Assert.Multiple(() =>
			{ // Embedded Fields with explicit null

				//note: anonymous class that is used as a template to create an inline class
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
					/*JsonNull*/ JsonArray = JsonArray.Create([ "FAILED" ]),
					/*JsonNull*/ JsonObject = JsonObject.Create(("FAILED", "EPIC")),
				};

				// when deserializing an object with all members explicitly set to null, we should return the default of this type
				var j = JsonObject
					.Parse("""{ "Int32": null, "Bool": null, "String": null, "Guid": null, "NullInt32": null, "NullBool": null, "NullGuid": null, "JsonValue": null, "JsonNull": null, "JsonArray": null, "JsonObject": null }""")
					.As(template);

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
			});

			Assert.That(SerializeToSlice(jnull), Is.EqualTo(Slice.FromString("null")));

			Assert.Multiple(() =>
			{
				Assert.That(value, Is.Not.EqualTo((object) 0));
				Assert.That(value, Is.Not.EqualTo((object) false));
				Assert.That(value, Is.Not.EqualTo((object?) null));
				Assert.That(value, Is.Not.EqualTo((object) ""));

				Assert.That(value.ValueEquals<int>(0), Is.False);
				Assert.That(value.ValueEquals<bool>(false), Is.False);
				Assert.That(value.ValueEquals<string>(null), Is.True);
				Assert.That(value.ValueEquals<string>(""), Is.False);

				Assert.That(value.ValueEquals<int?>(0), Is.False);
				Assert.That(value.ValueEquals<int?>(null), Is.True);
				Assert.That(value.ValueEquals<bool?>(false), Is.False);
				Assert.That(value.ValueEquals<bool?>(null), Is.True);
			});
		}

		[Test]
		public void Test_JsonNull_Missing()
		{
			var jmissing = JsonNull.Missing;
			Assert.That(jmissing, Is.Not.Null);
			Assert.That(jmissing, Is.InstanceOf<JsonNull>());

			var value = (JsonNull) jmissing;
			Assert.Multiple(() =>
			{
				Assert.That(value.Type, Is.EqualTo(JsonType.Null), "value.Type");
				Assert.That(value.IsNull, Is.True, "value.IsNull");
				Assert.That(value.IsMissing, Is.True, "value.IsMissing");
				Assert.That(value.IsError, Is.False, "value.IsError");
				Assert.That(value.IsDefault, Is.True, "value.IsDefault");
				Assert.That(value.ToObject(), Is.Null, "value.ToObject()");
				Assert.That(value.ToString(), Is.EqualTo(""), "value.ToString()");
			});

			Assert.Multiple(() =>
			{
				Assert.That(value.Equals(JsonNull.Missing), Is.True, "EQ missing");
				Assert.That(value.Equals(JsonNull.Null), Is.False, "NEQ null");
				Assert.That(value.Equals(JsonNull.Error), Is.False, "NEQ error");
				Assert.That(value.Equals(default(JsonValue)), Is.True);
				Assert.That(value!.Equals(default(object)), Is.True);

				Assert.That(value.Equals(0), Is.False);
				Assert.That(value.Equals(123), Is.False);
				Assert.That(value.Equals(false), Is.False);
				Assert.That(value.Equals(""), Is.False);
				Assert.That(value.Equals("hello"), Is.False);
				Assert.That(value.Equals(JsonObject.Create()), Is.False);
				Assert.That(value.Equals(JsonArray.Create()), Is.False);

				Assert.That(value.StrictEquals(JsonNull.Missing), Is.True, "EQ missing");
				Assert.That(value.StrictEquals(JsonNull.Null), Is.False, "NEQ null");
				Assert.That(value.StrictEquals(JsonNull.Error), Is.False, "NEQ error");
				Assert.That(value.StrictEquals(false), Is.False);
				Assert.That(value.StrictEquals(0), Is.False);
				Assert.That(value.StrictEquals(""), Is.False);
				Assert.That(value.StrictEquals("null"), Is.False);

				Assert.That(value.CompareTo(default(JsonValue)), Is.EqualTo(0));
				Assert.That(value.CompareTo(JsonNull.Null), Is.EqualTo(1));
				Assert.That(value.CompareTo(JsonNull.Missing), Is.EqualTo(0));
				Assert.That(value.CompareTo(JsonNull.Error), Is.EqualTo(-1));
				Assert.That(value.CompareTo(0), Is.EqualTo(-1));
				Assert.That(value.CompareTo(123), Is.EqualTo(-1));
				Assert.That(value.CompareTo(""), Is.EqualTo(-1));
				Assert.That(value.CompareTo("hello"), Is.EqualTo(-1));
			});

			//note: JsonNull.Missing sould bind the same way as JsonNull.Null (=> default(T))
			// except for T == JsonValue or JsonNull, in which case it should return itself as the JsonNull.Missing singleton

			Assert.Multiple(() =>
			{
				Assert.That(jmissing.Bind(typeof(JsonValue)), Is.SameAs(JsonNull.Missing));
				Assert.That(jmissing.Bind<JsonValue>(), Is.SameAs(JsonNull.Missing));
				Assert.That(() => jmissing.Required<JsonValue>(), Throws.InstanceOf<JsonBindingException>());
				Assert.That(jmissing.As<JsonValue>(), Is.SameAs(JsonNull.Missing));
				Assert.That(jmissing.As<JsonValue>(null, resolver: CrystalJson.DefaultResolver), Is.SameAs(JsonNull.Missing));
				Assert.That(jmissing.As<JsonValue>(123), Is.EqualTo(JsonNumber.Return(123)));

				Assert.That(jmissing.Bind(typeof(JsonNull)), Is.SameAs(JsonNull.Missing));
				Assert.That(jmissing.Bind<JsonNull>(), Is.SameAs(JsonNull.Missing));
				Assert.That(() => jmissing.Required<JsonNull>(), Throws.InstanceOf<JsonBindingException>());
				Assert.That(jmissing.As<JsonNull>(), Is.SameAs(JsonNull.Missing));
				Assert.That(jmissing.As<JsonNull>(null, resolver: CrystalJson.DefaultResolver), Is.SameAs(JsonNull.Missing));
			});

			Assert.That(SerializeToSlice(jmissing), Is.EqualTo(Slice.FromString("null")));

			Assert.Multiple(() =>
			{
				Assert.That(value, Is.Not.EqualTo((object) 0));
				Assert.That(value, Is.Not.EqualTo((object) false));
				Assert.That(value, Is.Not.EqualTo((object?) null));
				Assert.That(value, Is.Not.EqualTo((object) ""));

				Assert.That(value.ValueEquals<int>(0), Is.False);
				Assert.That(value.ValueEquals<bool>(false), Is.False);
				Assert.That(value.ValueEquals<string>(null), Is.True);
				Assert.That(value.ValueEquals<string>(""), Is.False);

				Assert.That(value.ValueEquals<int?>(0), Is.False);
				Assert.That(value.ValueEquals<int?>(null), Is.True);
				Assert.That(value.ValueEquals<bool?>(false), Is.False);
				Assert.That(value.ValueEquals<bool?>(null), Is.True);
			});
		}

		[Test]
		public void Test_JsonNull_Error()
		{
			var jerror = JsonNull.Error;
			Assert.That(jerror, Is.Not.Null);
			Assert.That(jerror, Is.InstanceOf<JsonNull>());

			var value = (JsonNull) jerror;
			Assert.Multiple(() =>
			{
				Assert.That(value.Type, Is.EqualTo(JsonType.Null), "value.Type");
				Assert.That(value.IsNull, Is.True, "value.IsNull");
				Assert.That(value.IsMissing, Is.False, "value.IsMissing");
				Assert.That(value.IsError, Is.True, "value.IsError");
				Assert.That(value.IsDefault, Is.True, "value.IsDefault");
				Assert.That(value.ToObject(), Is.Null, "value.ToObject()");
				Assert.That(value.ToString(), Is.EqualTo(""), "value.ToString()");
			});

			Assert.Multiple(() =>
			{
				Assert.That(value.Equals(JsonNull.Error), Is.True, "EQ error");
				Assert.That(value.Equals(JsonNull.Null), Is.False, "NEQ null");
				Assert.That(value.Equals(JsonNull.Missing), Is.False, "NEQ missing");
				Assert.That(value.Equals(default(JsonValue)), Is.True);
				Assert.That(value!.Equals(default(object)), Is.True);

				Assert.That(value.Equals(0), Is.False);
				Assert.That(value.Equals(123), Is.False);
				Assert.That(value.Equals(false), Is.False);
				Assert.That(value.Equals(""), Is.False);
				Assert.That(value.Equals("hello"), Is.False);
				Assert.That(value.Equals(JsonObject.Create()), Is.False);
				Assert.That(value.Equals(JsonArray.Create()), Is.False);

				Assert.That(value.StrictEquals(JsonNull.Error), Is.True, "EQ error");
				Assert.That(value.StrictEquals(JsonNull.Null), Is.False, "NEQ null");
				Assert.That(value.StrictEquals(JsonNull.Missing), Is.False, "NEQ missing");
				Assert.That(value.StrictEquals(false), Is.False);
				Assert.That(value.StrictEquals(0), Is.False);
				Assert.That(value.StrictEquals(""), Is.False);
				Assert.That(value.StrictEquals("null"), Is.False);

				Assert.That(value.CompareTo(default(JsonValue)), Is.EqualTo(0));
				Assert.That(value.CompareTo(JsonNull.Null), Is.EqualTo(1));
				Assert.That(value.CompareTo(JsonNull.Missing), Is.EqualTo(1));
				Assert.That(value.CompareTo(JsonNull.Error), Is.EqualTo(0));
				Assert.That(value.CompareTo(0), Is.EqualTo(-1));
				Assert.That(value.CompareTo(123), Is.EqualTo(-1));
				Assert.That(value.CompareTo(""), Is.EqualTo(-1));
				Assert.That(value.CompareTo("hello"), Is.EqualTo(-1));
			});

			Assert.That(SerializeToSlice(jerror), Is.EqualTo(Slice.FromString("null")));

			Assert.Multiple(() =>
			{
				Assert.That(value, Is.Not.EqualTo((object) 0));
				Assert.That(value, Is.Not.EqualTo((object) false));
				Assert.That(value, Is.Not.EqualTo((object?) null));
				Assert.That(value, Is.Not.EqualTo((object) ""));

				Assert.That(value.ValueEquals<int>(0), Is.False);
				Assert.That(value.ValueEquals<bool>(false), Is.False);
				Assert.That(value.ValueEquals<string>(null), Is.True);
				Assert.That(value.ValueEquals<string>(""), Is.False);

				Assert.That(value.ValueEquals<int?>(0), Is.False);
				Assert.That(value.ValueEquals<int?>(null), Is.True);
				Assert.That(value.ValueEquals<bool?>(false), Is.False);
				Assert.That(value.ValueEquals<bool?>(null), Is.True);
			});
		}

		#endregion

		#region JsonBoolean...

		[Test]
		public void Test_JsonBoolean()
		{
			// JsonBoolean.True

			Assert.That(JsonBoolean.True, Is.Not.Null);
			Assert.That(JsonBoolean.True.Type, Is.EqualTo(JsonType.Boolean));
			Assert.That(JsonBoolean.True.IsNull, Is.False);
			Assert.That(JsonBoolean.True.IsDefault, Is.False);
			Assert.That(JsonBoolean.True.IsReadOnly, Is.True);
			Assert.That(JsonBoolean.True.ToObject(), Is.True);
			Assert.That(JsonBoolean.True.ToString(), Is.EqualTo("true"));
			Assert.That(JsonBoolean.True.Equals(JsonBoolean.True), Is.True);
			Assert.That(JsonBoolean.True.Equals(JsonBoolean.False), Is.False);
			Assert.That(JsonBoolean.True.Equals(JsonNull.Null), Is.False);
			Assert.That(JsonBoolean.True.Equals(true), Is.True);
			Assert.That(JsonBoolean.True.Equals(false), Is.False);
			Assert.That(JsonBoolean.True, Is.SameAs(JsonBoolean.True));

			// JsonBoolean.False

			Assert.That(JsonBoolean.False, Is.Not.Null);
			Assert.That(JsonBoolean.False.Type, Is.EqualTo(JsonType.Boolean));
			Assert.That(JsonBoolean.False.IsNull, Is.False);
			Assert.That(JsonBoolean.False.IsDefault, Is.True);
			Assert.That(JsonBoolean.False.IsReadOnly, Is.True);
			Assert.That(JsonBoolean.False.ToObject(), Is.False);
			Assert.That(JsonBoolean.False.ToString(), Is.EqualTo("false"));
			Assert.That(JsonBoolean.False.Equals((JsonBoolean) JsonBoolean.True), Is.False);
			Assert.That(JsonBoolean.False.Equals((JsonBoolean) JsonBoolean.False), Is.True);
			Assert.That(JsonBoolean.False.Equals((JsonValue) JsonBoolean.True), Is.False);
			Assert.That(JsonBoolean.False.Equals((JsonValue) JsonBoolean.False), Is.True);
			Assert.That(JsonBoolean.False.Equals(JsonNull.Null), Is.False);
			Assert.That(JsonBoolean.False.Equals(true), Is.False);
			Assert.That(JsonBoolean.False.Equals(false), Is.True);

			// JsonBoolean.Return

			Assert.That(JsonBoolean.Return(false), Is.SameAs(JsonBoolean.False), "JsonBoolean.Return(false) should return the False singleton");
			Assert.That(JsonBoolean.Return(true), Is.SameAs(JsonBoolean.True), "JsonBoolean.Return(true) should return the True singleton");
			Assert.That(JsonBoolean.Return((bool?) null), Is.SameAs(JsonNull.Null), "JsonBoolean.Return(null) should return the Null singleton");
			Assert.That(JsonBoolean.Return((bool?) false), Is.SameAs(JsonBoolean.False), "JsonBoolean.Return(false) should return the False singleton");
			Assert.That(JsonBoolean.Return((bool?) true), Is.SameAs(JsonBoolean.True), "JsonBoolean.Return(true) should return the True singleton");

			// Conversions

			Assert.That(JsonBoolean.False.ToString(), Is.EqualTo("false"));
			Assert.That(JsonBoolean.False.Bind<string>(), Is.EqualTo("false"));
			Assert.That(JsonBoolean.False.Bind(typeof(string)), Is.EqualTo("false"));
			Assert.That(JsonBoolean.True.ToString(), Is.EqualTo("true"));
			Assert.That(JsonBoolean.True.Bind<string>(), Is.EqualTo("true"));
			Assert.That(JsonBoolean.True.Bind(typeof(string)), Is.InstanceOf<string>());

			Assert.That(JsonBoolean.False.ToInt32(), Is.EqualTo(0));
			Assert.That(JsonBoolean.False.Bind<int>(), Is.EqualTo(0));
			Assert.That(JsonBoolean.False.Bind(typeof(int)), Is.InstanceOf<int>());
			Assert.That(JsonBoolean.False.Bind(typeof(int)), Is.EqualTo(0));
			Assert.That(JsonBoolean.True.ToInt32(), Is.EqualTo(1));
			Assert.That(JsonBoolean.True.Bind<int>(), Is.EqualTo(1));
			Assert.That(JsonBoolean.True.Bind(typeof(int)), Is.InstanceOf<int>());
			Assert.That(JsonBoolean.True.Bind(typeof(int)), Is.EqualTo(1));

			Assert.That(JsonBoolean.False.ToInt64(), Is.EqualTo(0L));
			Assert.That(JsonBoolean.False.Bind<long>(), Is.EqualTo(0L));
			Assert.That(JsonBoolean.False.Bind(typeof(long)), Is.EqualTo(0L));
			Assert.That(JsonBoolean.True.ToInt64(), Is.EqualTo(1L));
			Assert.That(JsonBoolean.True.Bind<long>(), Is.EqualTo(1L));
			Assert.That(JsonBoolean.True.Bind(typeof(long)), Is.InstanceOf<long>());
			Assert.That(JsonBoolean.True.Bind(typeof(long)), Is.EqualTo(1L));

			Assert.That(JsonBoolean.False.ToSingle(), Is.EqualTo(0f));
			Assert.That(JsonBoolean.False.Bind<float>(), Is.EqualTo(0f));
			Assert.That(JsonBoolean.False.Bind(typeof(float)), Is.EqualTo(0f));
			Assert.That(JsonBoolean.True.ToSingle(), Is.EqualTo(1f));
			Assert.That(JsonBoolean.True.Bind<float>(), Is.EqualTo(1f));
			Assert.That(JsonBoolean.True.Bind(typeof(float)), Is.InstanceOf<float>());
			Assert.That(JsonBoolean.True.Bind(typeof(float)), Is.EqualTo(1f));

			Assert.That(JsonBoolean.False.ToDouble(), Is.EqualTo(0d));
			Assert.That(JsonBoolean.False.Bind<double>(), Is.EqualTo(0d));
			Assert.That(JsonBoolean.False.Bind(typeof(double)), Is.EqualTo(0d));
			Assert.That(JsonBoolean.True.ToDouble(), Is.EqualTo(1d));
			Assert.That(JsonBoolean.True.Bind<double>(), Is.EqualTo(1d));
			Assert.That(JsonBoolean.True.Bind(typeof(double)), Is.InstanceOf<double>());
			Assert.That(JsonBoolean.True.Bind(typeof(double)), Is.EqualTo(1d));

			Assert.That(JsonBoolean.False.ToDecimal(), Is.EqualTo(0m));
			Assert.That(JsonBoolean.False.Bind<decimal>(), Is.EqualTo(0m));
			Assert.That(JsonBoolean.False.Bind(typeof(decimal)), Is.EqualTo(0m));
			Assert.That(JsonBoolean.True.ToDecimal(), Is.EqualTo(1m));
			Assert.That(JsonBoolean.True.Bind<decimal>(), Is.EqualTo(1m));
			Assert.That(JsonBoolean.True.Bind(typeof(decimal)), Is.EqualTo(1m));
			Assert.That(JsonBoolean.True.Bind(typeof(decimal)), Is.InstanceOf<decimal>());

			Assert.That(JsonBoolean.False.ToGuid(), Is.EqualTo(Guid.Empty));
			Assert.That(JsonBoolean.False.Bind<Guid>(), Is.EqualTo(Guid.Empty));
			Assert.That(JsonBoolean.False.Bind(typeof(Guid)), Is.EqualTo(Guid.Empty));
			Assert.That(JsonBoolean.True.ToGuid(), Is.EqualTo(Guid.Parse("FFFFFFFF-FFFF-FFFF-FFFF-FFFFFFFFFFFF")));
			Assert.That(JsonBoolean.True.Bind<Guid>(), Is.EqualTo(Guid.Parse("FFFFFFFF-FFFF-FFFF-FFFF-FFFFFFFFFFFF")));
			Assert.That(JsonBoolean.True.Bind(typeof(Guid)), Is.EqualTo(Guid.Parse("FFFFFFFF-FFFF-FFFF-FFFF-FFFFFFFFFFFF")));
			Assert.That(JsonBoolean.True.Bind(typeof(Guid)), Is.InstanceOf<Guid>());

			Assert.That(SerializeToSlice(JsonBoolean.False), Is.EqualTo(Slice.Copy("false"u8)));
			Assert.That(SerializeToSlice(JsonBoolean.True), Is.EqualTo(Slice.Copy("true"u8)));
		}

		[Test]
		public void Test_JsonBoolean_ValueEquals()
		{
			Assert.That(JsonBoolean.False.ValueEquals<bool>(false), Is.True);
			Assert.That(JsonBoolean.False.ValueEquals<bool>(true), Is.False);
			Assert.That(JsonBoolean.False.ValueEquals(0), Is.False);
			Assert.That(JsonBoolean.False.ValueEquals(1), Is.False);
			Assert.That(JsonBoolean.False.ValueEquals(""), Is.False);

			Assert.That(JsonBoolean.True.ValueEquals<bool>(false), Is.False);
			Assert.That(JsonBoolean.True.ValueEquals<bool>(true), Is.True);
			Assert.That(JsonBoolean.True.ValueEquals(0), Is.False);
			Assert.That(JsonBoolean.True.ValueEquals(1), Is.False);
			Assert.That(JsonBoolean.True.ValueEquals("true"), Is.False);
		}

		[Test]
		public void Test_JsonBoolean_StrictEquals()
		{
			Assert.That(JsonBoolean.False.StrictEquals(JsonBoolean.False), Is.True);
			Assert.That(JsonBoolean.True.StrictEquals(JsonBoolean.True), Is.True);
			Assert.That(JsonBoolean.False.StrictEquals(false), Is.True);
			Assert.That(JsonBoolean.True.StrictEquals(true), Is.True);

			Assert.That(JsonBoolean.False.StrictEquals(JsonNull.Missing), Is.False);
			Assert.That(JsonBoolean.False.StrictEquals(0), Is.False);
			Assert.That(JsonBoolean.False.StrictEquals(""), Is.False);
			Assert.That(JsonBoolean.False.StrictEquals("false"), Is.False);
			Assert.That(JsonBoolean.True.StrictEquals(JsonArray.ReadOnly.Empty), Is.False);
			Assert.That(JsonBoolean.True.StrictEquals(1), Is.False);
			Assert.That(JsonBoolean.True.StrictEquals(""), Is.False);
			Assert.That(JsonBoolean.True.StrictEquals("true"), Is.False);
		}

		#endregion

		#region JsonString...

		[Test]
		public void Test_JsonString()
		{
			{ // empty
				JsonValue jval = JsonString.Empty;
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
				var jstr = (JsonString) jval!;
				Assert.That(jstr.IsNullOrEmpty, Is.True);
				Assert.That(jstr.Length, Is.EqualTo(0));
				Assert.That(SerializeToSlice(jstr), Is.EqualTo(Slice.FromString("\"\"")));
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
				Assert.That(SerializeToSlice(jstr), Is.EqualTo(Slice.FromString("\"Hello, World!\"")));
			}

			{ // from StringBuilder
				var sb = new StringBuilder("Hello").Append(", World!");
				var jval = JsonString.Return(sb);
				Assert.That(jval, Is.InstanceOf<JsonString>());
				Assert.That(jval.ToStringOrDefault(), Is.EqualTo("Hello, World!"));
				Assert.That(jval.ToObject(), Is.EqualTo("Hello, World!"));
				Assert.That(jval.ToString(), Is.EqualTo("Hello, World!"));
				sb.Append('?');
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
		public void Test_JsonString_Convert_ToBoolean()
		{
			Assert.That(JsonString.Return("false").ToBoolean(), Is.False);
			Assert.That(JsonString.Return("false").ToBooleanOrDefault(), Is.False);
			Assert.That(JsonString.Return("false").Bind<bool>(), Is.False);
			Assert.That(JsonString.Return("false").Bind(typeof(bool)), Is.False);
			Assert.That(JsonString.Return("true").ToBoolean(), Is.True);
			Assert.That(JsonString.Return("true").ToBooleanOrDefault(), Is.True);
			Assert.That(JsonString.Return("true").Bind<bool>(), Is.True);
			Assert.That(JsonString.Return("true").Bind(typeof(bool)), Is.True);
		}

		[Test]
		public void Test_JsonString_Convert_ToInt32()
		{
			Assert.That(JsonString.Return("0").ToInt32(), Is.EqualTo(0));
			Assert.That(JsonString.Return("0").ToInt32OrDefault(), Is.EqualTo(0));
			Assert.That(JsonString.Return("0").Bind<int>(), Is.EqualTo(0));
			Assert.That(JsonString.Return("0").Bind(typeof(int)), Is.InstanceOf<int>().And.EqualTo(0));
			Assert.That(JsonString.Return("1").ToInt32(), Is.EqualTo(1));
			Assert.That(JsonString.Return("1").ToInt32OrDefault(), Is.EqualTo(1));
			Assert.That(JsonString.Return("1").Bind<int>(), Is.EqualTo(1));
			Assert.That(JsonString.Return("1").Bind(typeof(int)), Is.InstanceOf<int>().And.EqualTo(1));
			Assert.That(JsonString.Return("123").ToInt32(), Is.EqualTo(123));
			Assert.That(JsonString.Return("123").ToInt32OrDefault(), Is.EqualTo(123));
			Assert.That(JsonString.Return("123").Bind<int>(), Is.EqualTo(123));
			Assert.That(JsonString.Return("123").Bind(typeof(int)), Is.InstanceOf<int>().And.EqualTo(123));
			Assert.That(JsonString.Return("666666666").Bind<int>(), Is.EqualTo(666666666));
			Assert.That(JsonString.Return("666666666").Bind(typeof(int)), Is.InstanceOf<int>().And.EqualTo(666666666));
			Assert.That(JsonString.Return("2147483647").Bind<int>(), Is.EqualTo(int.MaxValue));
			Assert.That(JsonString.Return("2147483647").Bind(typeof(int)), Is.InstanceOf<int>().And.EqualTo(int.MaxValue));
			Assert.That(JsonString.Return("-2147483648").Bind<int>(), Is.EqualTo(int.MinValue));
			Assert.That(JsonString.Return("-2147483648").Bind(typeof(int)), Is.InstanceOf<int>().And.EqualTo(int.MinValue));

			Assert.That(((JsonString) JsonString.Return("123")).TryConvertToInt32(out var result), Is.True);
			Assert.That(result, Is.EqualTo(123));

			Assert.That(((JsonString) JsonString.Return("hello")).TryConvertToInt32(out result), Is.False);
			Assert.That(result, Is.Zero);

			Assert.That(((JsonString) JsonString.Return("123abc")).TryConvertToInt32(out result), Is.False);
			Assert.That(result, Is.Zero);
		}

		[Test]
		public void Test_JsonString_Convert_ToInt64()
		{
			Assert.That(JsonString.Return("0").ToInt64(), Is.EqualTo(0));
			Assert.That(JsonString.Return("1").Bind<long>(), Is.EqualTo(1));
			Assert.That(JsonString.Return("1").Bind(typeof(long)), Is.InstanceOf<long>().And.EqualTo(1));
			Assert.That(JsonString.Return("123").ToInt64(), Is.EqualTo(123));
			Assert.That(JsonString.Return("666666666").Bind<long>(), Is.EqualTo(666666666));
			Assert.That(JsonString.Return("666666666").Bind(typeof(long)), Is.InstanceOf<long>().And.EqualTo(666666666));
			Assert.That(JsonString.Return("9223372036854775807").Bind<long>(), Is.EqualTo(long.MaxValue));
			Assert.That(JsonString.Return("9223372036854775807").Bind(typeof(long)), Is.InstanceOf<long>().And.EqualTo(long.MaxValue));
			Assert.That(JsonString.Return("-9223372036854775808").Bind<long>(), Is.EqualTo(long.MinValue));
			Assert.That(JsonString.Return("-9223372036854775808").Bind(typeof(long)), Is.InstanceOf<long>().And.EqualTo(long.MinValue));
			Assert.That(JsonString.Return("123").Bind(typeof(long)), Is.InstanceOf<long>());

			Assert.That(((JsonString) JsonString.Return("123")).TryConvertToInt64(out var result), Is.True);
			Assert.That(result, Is.EqualTo(123L));

			Assert.That(((JsonString) JsonString.Return("hello")).TryConvertToInt64(out result), Is.False);
			Assert.That(result, Is.Zero);

			Assert.That(((JsonString) JsonString.Return("123abc")).TryConvertToInt64(out result), Is.False);
			Assert.That(result, Is.Zero);

		}

		[Test]
		public void Test_JsonString_Convert_ToSingle()
		{
			Assert.That(JsonString.Return("0").ToSingle(), Is.EqualTo(0f));
			Assert.That(JsonString.Return("1").Bind<float>(), Is.EqualTo(1f));
			Assert.That(JsonString.Return("1").Bind(typeof(float)), Is.EqualTo(1f));
			Assert.That(JsonString.Return("1.23").ToSingle(), Is.EqualTo(1.23f));
			Assert.That(JsonString.Return("1.23").Bind<float>(), Is.EqualTo(1.23f));
			Assert.That(JsonString.Return("1.23").Bind(typeof(float)), Is.InstanceOf<float>().And.EqualTo(1.23f));
			Assert.That(JsonString.Return("3.14159274").Bind<float>(), Is.EqualTo((float) Math.PI));
			Assert.That(JsonString.Return("3.14159274").Bind(typeof(float)), Is.InstanceOf<float>().And.EqualTo((float) Math.PI));
			Assert.That(JsonString.Return("NaN").Bind<float>(), Is.EqualTo(float.NaN));
			Assert.That(JsonString.Return("NaN").Bind(typeof(float)), Is.InstanceOf<float>().And.EqualTo(float.NaN));
			Assert.That(JsonString.Return("Infinity").Bind<float>(), Is.EqualTo(float.PositiveInfinity));
			Assert.That(JsonString.Return("Infinity").Bind(typeof(float)), Is.InstanceOf<float>().And.EqualTo(float.PositiveInfinity));
			Assert.That(JsonString.Return("-Infinity").Bind<float>(), Is.EqualTo(float.NegativeInfinity));
			Assert.That(JsonString.Return("-Infinity").Bind(typeof(float)), Is.InstanceOf<float>().And.EqualTo(float.NegativeInfinity));

			Assert.That(((JsonString) JsonString.Return("1.23")).TryConvertToSingle(out var result), Is.True);
			Assert.That(result, Is.EqualTo(1.23f));

			Assert.That(((JsonString) JsonString.Return("hello")).TryConvertToSingle(out result), Is.False);
			Assert.That(result, Is.Zero);

			Assert.That(((JsonString) JsonString.Return("123abc")).TryConvertToSingle(out result), Is.False);
			Assert.That(result, Is.Zero);
		}

		[Test]
		public void Test_JsonString_Convert_ToDouble()
		{
			Assert.That(JsonString.Return("0").ToDouble(), Is.EqualTo(0d));
			Assert.That(JsonString.Return("1").Bind(typeof(double)), Is.EqualTo(1d));
			Assert.That(JsonString.Return("1.23").ToDouble(), Is.EqualTo(1.23d));
			Assert.That(JsonString.Return("3.1415926535897931").Bind(typeof(double)), Is.EqualTo(Math.PI));
			Assert.That(JsonString.Return("NaN").Bind(typeof(double)), Is.EqualTo(double.NaN));
			Assert.That(JsonString.Return("Infinity").Bind(typeof(double)), Is.EqualTo(double.PositiveInfinity));
			Assert.That(JsonString.Return("-Infinity").Bind(typeof(double)), Is.EqualTo(double.NegativeInfinity));
			Assert.That(JsonString.Return("1.23").Bind(typeof(double)), Is.InstanceOf<double>());

			Assert.That(((JsonString) JsonString.Return("1.23")).TryConvertToDouble(out var result), Is.True);
			Assert.That(result, Is.EqualTo(1.23d));

			Assert.That(((JsonString) JsonString.Return("hello")).TryConvertToDouble(out result), Is.False);
			Assert.That(result, Is.Zero);

			Assert.That(((JsonString) JsonString.Return("123abc")).TryConvertToDouble(out result), Is.False);
			Assert.That(result, Is.Zero);
		}

		[Test]
		public void Test_JsonString_Convert_ToDecimal()
		{
			Assert.That(JsonString.Return("0").ToDecimal(), Is.EqualTo(decimal.Zero));
			Assert.That(JsonString.Return("1").Bind(typeof(decimal)), Is.EqualTo(decimal.One));
			Assert.That(JsonString.Return("-1").Bind(typeof(decimal)), Is.EqualTo(decimal.MinusOne));
			Assert.That(JsonString.Return("1.23").ToDecimal(), Is.EqualTo(1.23m));
			Assert.That(JsonString.Return("3.1415926535897931").Bind(typeof(decimal)), Is.EqualTo(Math.PI));
			Assert.That(JsonString.Return("79228162514264337593543950335").Bind(typeof(decimal)), Is.EqualTo(decimal.MaxValue));
			Assert.That(JsonString.Return("-79228162514264337593543950335").Bind(typeof(decimal)), Is.EqualTo(decimal.MinValue));
			Assert.That(JsonString.Return("1.23").Bind(typeof(decimal)), Is.InstanceOf<decimal>());

			Assert.That(((JsonString) JsonString.Return("1.23")).TryConvertToDecimal(out var result), Is.True);
			Assert.That(result, Is.EqualTo(1.23m));

			Assert.That(((JsonString) JsonString.Return("hello")).TryConvertToDecimal(out result), Is.False);
			Assert.That(result, Is.Zero);

			Assert.That(((JsonString) JsonString.Return("123abc")).TryConvertToDecimal(out result), Is.False);
			Assert.That(result, Is.Zero);
		}

		[Test]
		public void Test_JsonString_Convert_ToGuid()
		{
			Assert.That(JsonString.Empty.ToGuid(), Is.EqualTo(Guid.Empty));
			Assert.That(JsonString.Empty.ToGuidOrDefault(), Is.Null);
			Assert.That(JsonString.Return("00000000-0000-0000-0000-000000000000").ToGuid(), Is.EqualTo(Guid.Empty));
			Assert.That(JsonString.Return("00000000-0000-0000-0000-000000000000").ToGuidOrDefault(), Is.EqualTo(Guid.Empty));
			Assert.That(JsonString.Return("b771bab0-7ad2-4945-a501-1dd939ca9bac").ToGuid(), Is.EqualTo(new Guid("b771bab0-7ad2-4945-a501-1dd939ca9bac")));
			Assert.That(JsonString.Return("591d8e31-1b79-4532-b7b9-4f8a9c0d0010").Bind(typeof(Guid)), Is.EqualTo(new Guid("591d8e31-1b79-4532-b7b9-4f8a9c0d0010")));

			Assert.That(((JsonString) JsonString.Return("133a3e6c-9ce5-4e9f-afe4-fa8c59945704")).TryConvertToGuid(out var result), Is.True);
			Assert.That(result, Is.EqualTo(new Guid("133a3e6c-9ce5-4e9f-afe4-fa8c59945704")));

			Assert.That(((JsonString) JsonString.Return("133a3e6c-9ce5-4e9f-afe4-fa8c5994570")).TryConvertToGuid(out result), Is.False);
			Assert.That(result, Is.EqualTo(Guid.Empty));

			Assert.That(((JsonString) JsonString.Return("133a3e6c-9ce5-4e9f-afe4-fa8c599457045")).TryConvertToGuid(out result), Is.False);
			Assert.That(result, Is.EqualTo(Guid.Empty));

			Assert.That(((JsonString) JsonString.Return("123abc")).TryConvertToGuid(out result), Is.False);
			Assert.That(result, Is.EqualTo(Guid.Empty));
		}

		[Test]
		public void Test_JsonString_Convert_ToDateTime()
		{
			Assert.That(JsonString.Return("2013-03-11T12:34:56.768").ToDateTime(), Is.EqualTo(new DateTime(2013, 3, 11, 12, 34, 56, 768, DateTimeKind.Unspecified)));
			Assert.That(JsonString.Return("2013-03-11T12:34:56.768Z").ToDateTime(), Is.EqualTo(new DateTime(2013, 3, 11, 12, 34, 56, 768, DateTimeKind.Utc)));
			Assert.That(JsonString.Return("2013-03-11T12:34:56.768+01:00").ToDateTime(), Is.EqualTo(new DateTime(2013, 3, 11, 12, 34, 56, 768, DateTimeKind.Local)), "Ne marche que si la local TZ est Paris !");
			Assert.That(JsonString.Return("2013-03-11T12:34:56.768+01").ToDateTime(), Is.EqualTo(new DateTime(2013, 3, 11, 12, 34, 56, 768, DateTimeKind.Local)), "Ne marche que si la local TZ est Paris !");
			Assert.That(JsonString.Return("2013-03-11T12:34:56.768-01").ToDateTime(), Is.EqualTo(new DateTime(2013, 3, 11, 12, 34, 56, 768, DateTimeKind.Local).AddHours(2)), "Ne marche que si la local TZ est Paris !");
			Assert.That(JsonString.Return("2013-03-11T12:34:56.768+11:30").ToDateTime(), Is.EqualTo(new DateTime(2013, 3, 11, 12, 34, 56, 768, DateTimeKind.Local).AddHours(-10).AddMinutes(-30)), "Ne marche que si la local TZ est Paris !");
			Assert.That(JsonString.Return("2013-03-11T12:34:56.768-11:30").ToDateTime(), Is.EqualTo(new DateTime(2013, 3, 11, 12, 34, 56, 768, DateTimeKind.Local).AddHours(12).AddMinutes(30)), "Ne marche que si la local TZ est Paris !");

			Assert.That(((JsonString) JsonString.Return("2013-03-11T12:34:56.768")).TryConvertToDateTime(out var result), Is.True);
			Assert.That(result, Is.EqualTo(new DateTime(2013, 3, 11, 12, 34, 56, 768, DateTimeKind.Unspecified)));

			Assert.That(((JsonString) JsonString.Return("2013-03-11T12:34:56.768Z")).TryConvertToDateTime(out result), Is.True);
			Assert.That(result, Is.EqualTo(new DateTime(2013, 3, 11, 12, 34, 56, 768, DateTimeKind.Utc)));

			Assert.That(((JsonString) JsonString.Return("2013-03-11T12:34:56.768+01:00")).TryConvertToDateTime(out result), Is.True);
			Assert.That(result, Is.EqualTo(new DateTime(2013, 3, 11, 12, 34, 56, 768, DateTimeKind.Local)));

			Assert.That(((JsonString) JsonString.Return("hello")).TryConvertToDateTime(out result), Is.False);
			Assert.That(result, Is.EqualTo(DateTime.MinValue));

			Assert.That(((JsonString) JsonString.Return("yyyy-mm-ddThh:mm:ss.fff")).TryConvertToDateTime(out result), Is.False);
			Assert.That(result, Is.EqualTo(DateTime.MinValue));

			Assert.That(((JsonString) JsonString.Return("2013-13-01T12:34:56.768")).TryConvertToDateTime(out result), Is.False);
			Assert.That(result, Is.EqualTo(DateTime.MinValue));

			Assert.That(((JsonString) JsonString.Return("2013-03-32T12:34:56.768")).TryConvertToDateTime(out result), Is.False);
			Assert.That(result, Is.EqualTo(DateTime.MinValue));

			Assert.That(((JsonString) JsonString.Return("2013-03-11T25:34:56.768")).TryConvertToDateTime(out result), Is.False);
			Assert.That(result, Is.EqualTo(DateTime.MinValue));

			Assert.That(((JsonString) JsonString.Return("2013-03-11T12:60:56.768")).TryConvertToDateTime(out result), Is.False);
			Assert.That(result, Is.EqualTo(DateTime.MinValue));

			Assert.That(((JsonString) JsonString.Return("2013-03-11T12:34:60.768")).TryConvertToDateTime(out result), Is.False);
			Assert.That(result, Is.EqualTo(DateTime.MinValue));

			Assert.That(((JsonString) JsonString.Return("2013-03-11T12:34:56.00a")).TryConvertToDateTime(out result), Is.False);
			Assert.That(result, Is.EqualTo(DateTime.MinValue));
		}

		[Test]
		public void Test_JsonString_Convert_ToDateTimeOffset()
		{
			Assert.That(JsonString.Return("2013-03-11T12:34:56.768Z").ToDateTimeOffset(), Is.EqualTo(new DateTimeOffset(2013, 3, 11, 12, 34, 56, 768, TimeSpan.Zero)));
			Assert.That(JsonString.Return("2013-03-11T12:34:56.768+01:00").ToDateTimeOffset(), Is.EqualTo(new DateTimeOffset(2013, 3, 11, 12, 34, 56, 768, TimeSpan.FromHours(1))));
			Assert.That(JsonString.Return("2013-03-11T12:34:56.768+04").ToDateTimeOffset(), Is.EqualTo(new DateTimeOffset(2013, 3, 11, 12, 34, 56, 768, TimeSpan.FromHours(4))));
			Assert.That(JsonString.Return("2013-03-11T12:34:56.768-07").ToDateTimeOffset(), Is.EqualTo(new DateTimeOffset(2013, 3, 11, 12, 34, 56, 768, TimeSpan.FromHours(-7))));
			Assert.That(JsonString.Return("2013-03-11T12:34:56.768-11").ToDateTimeOffset(), Is.EqualTo(new DateTimeOffset(2013, 3, 11, 12, 34, 56, 768, TimeSpan.FromHours(-11))));
		}

		[Test]
		public void Test_JsonString_Convert_Custom_Enum()
		{
			Assert.That(JsonString.Return("Foo").Required<DummyJsonEnum>(), Is.EqualTo(DummyJsonEnum.Foo));
			Assert.That(JsonString.Return("Bar").Required<DummyJsonEnum>(), Is.EqualTo(DummyJsonEnum.Bar));
			Assert.That(JsonString.Return("Bar").Required<DummyJsonEnumTypo>(), Is.EqualTo(DummyJsonEnumTypo.Bar));
			Assert.That(JsonString.Return("Barrh").Required<DummyJsonEnumTypo>(), Is.EqualTo(DummyJsonEnumTypo.Bar));
			Assert.That(() => JsonString.Return("Barrh").Required<DummyJsonEnum>(), Throws.InstanceOf<JsonBindingException>());
		}

		[Test]
		public void Test_JsonString_Convert_IpAddress()
		{
			Assert.That(JsonNull.Null.As<IPAddress>(), Is.Null);
			Assert.That(JsonString.Empty.As<IPAddress>(), Is.Null);
			Assert.That(JsonString.Return("127.0.0.1").Required<IPAddress>(), Is.EqualTo(IPAddress.Loopback));
			Assert.That(JsonString.Return("0.0.0.0").Required<IPAddress>(), Is.EqualTo(IPAddress.Any));
			Assert.That(JsonString.Return("255.255.255.255").Required<IPAddress>(), Is.EqualTo(IPAddress.None));
			Assert.That(JsonString.Return("192.168.1.2").Required<IPAddress>(), Is.EqualTo(IPAddress.Parse("192.168.1.2")));
			Assert.That(JsonString.Return("::1").Required<IPAddress>(), Is.EqualTo(IPAddress.IPv6Loopback));
			Assert.That(JsonString.Return("::").Required<IPAddress>(), Is.EqualTo(IPAddress.IPv6Any));
			Assert.That(JsonString.Return("fe80::b8bc:1664:15a0:3a79%11").Required<IPAddress>(), Is.EqualTo(IPAddress.Parse("fe80::b8bc:1664:15a0:3a79%11")));
			Assert.That(JsonString.Return("[::1]").Required<IPAddress>(), Is.EqualTo(IPAddress.IPv6Loopback));
			Assert.That(JsonString.Return("[::]").Required<IPAddress>(), Is.EqualTo(IPAddress.IPv6Any));
			Assert.That(JsonString.Return("[fe80::b8bc:1664:15a0:3a79%11]").Required<IPAddress>(), Is.EqualTo(IPAddress.Parse("fe80::b8bc:1664:15a0:3a79%11")));
			Assert.That(() => JsonString.Return("127.0.0.").Required<IPAddress>(), Throws.InstanceOf<FormatException>());
			Assert.That(() => JsonString.Return("127.0.0.1.2").Required<IPAddress>(), Throws.InstanceOf<FormatException>());
		}

		[Test]
		public void Test_JsonString_Convert_Empty_String()
		{

			{ // empty => T : must return default(T) so 0/false/...
				Assert.That(JsonString.Empty.Required<bool>(), Is.False, "'' -> bool");
				Assert.That(JsonString.Empty.Required<int>(), Is.Zero, "'' -> int");
				Assert.That(JsonString.Empty.Required<long>(), Is.Zero, "'' -> long");
				Assert.That(JsonString.Empty.Required<float>(), Is.Zero, "'' -> float");
				Assert.That(JsonString.Empty.Required<double>(), Is.Zero, "'' -> double");
				Assert.That(JsonString.Empty.Required<DateTime>(), Is.EqualTo(DateTime.MinValue), "'' -> DateTime");
				Assert.That(JsonString.Empty.Required<DateTimeOffset>(), Is.EqualTo(DateTimeOffset.MinValue), "'' -> DateTimeOffset");
			}

			{ // empty => T?: must return default(T?) so null
				Assert.That(JsonString.Empty.As<bool?>(), Is.Null, "'' -> bool?");
				Assert.That(JsonString.Empty.As<int?>(), Is.Null, "'' -> int?");
				Assert.That(JsonString.Empty.As<long?>(), Is.Null, "'' -> long?");
				Assert.That(JsonString.Empty.As<float?>(), Is.Null, "'' -> float?");
				Assert.That(JsonString.Empty.As<double?>(), Is.Null, "'' -> double?");
				Assert.That(JsonString.Empty.As<DateTime?>(), Is.Null, "'' -> DateTime?");
				Assert.That(JsonString.Empty.As<DateTimeOffset?>(), Is.Null, "'' -> DateTimeOffset?");
			}
		}

		[Test]
		public void Test_JsonString_Convert_AutoCast()
		{
			JsonValue jval = "hello"; // implicit cast
			Assert.That(jval, Is.Not.Null);
			Assert.That(jval, Is.InstanceOf<JsonString>());
			JsonString jstr = (JsonString) jval;
			Assert.That(jstr.Value, Is.EqualTo("hello"));

			var s = (string?) jval; // explicit cast
			Assert.That(s, Is.Not.Null);
			Assert.That(s, Is.EqualTo("hello"));
		}

		[Test]
		public void Test_JsonString_Comparisons()
		{

			// comparisons
			void Compare(string a, string b)
			{
				JsonValue ja = JsonString.Return(a);
				JsonValue jb = JsonString.Return(b);

				Assert.That(Math.Sign(ja.CompareTo(jb)), Is.EqualTo(Math.Sign(string.CompareOrdinal(a, b))), $"'{a}' cmp '{b}'");
				Assert.That(Math.Sign(jb.CompareTo(ja)), Is.EqualTo(Math.Sign(string.CompareOrdinal(b, a))), $"'{b}' cmp '{a}'");
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
				["a", "b", "c", "aa", "ab", "aC", "aaa", "abc"],
				["a", "aC", "aa", "aaa", "ab", "abc", "b", "c"]
			);
			SortStrings(
				"sorting should use lexicographical order",
				["cat", "bat", "catamaran", "catZ", "batman"],
				["bat", "batman", "cat", "catZ", "catamaran"]
			);
			SortStrings(
				"numbers < UPPERs << lowers",
				["a", "1", "A"],
				["1", "A", "a"]
			);
			SortStrings(
				"numbers should be sorted lexicographically if comparing strings (1 < 10 < 2)",
				["0", "1", "2", "7", "10", "42", "100", "1000"],
				["0", "1", "10", "100", "1000", "2", "42", "7"]
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
		public void Test_JsonString_ValueEquals()
		{
			Assert.That(JsonString.Return("hello").ValueEquals<string>("hello"), Is.True);
			Assert.That(JsonString.Return("hello").ValueEquals<string>(""), Is.False);
			Assert.That(JsonString.Return("hello").ValueEquals<string>("hello"), Is.True);
			Assert.That(JsonString.Return("").ValueEquals<string>(""), Is.True);
			Assert.That(JsonString.Return("").ValueEquals<string>("hello"), Is.False);
			Assert.That(JsonString.Return("").ValueEquals<string>(null), Is.False);

			Assert.That(JsonString.Return("hello").ValueEquals<int>(123), Is.False);
			Assert.That(JsonString.Return("123").ValueEquals<int>(123), Is.False);
			Assert.That(JsonString.Return("").ValueEquals<bool>(false), Is.False);
		}

		[Test]
		public void Test_JsonString_StrictEquals()
		{
			Assert.That(JsonString.Return("").StrictEquals(""), Is.True);
			Assert.That(JsonString.Return("hello").StrictEquals("hello"), Is.True);

			Assert.That(JsonString.Return("hello").StrictEquals("world"), Is.False);
			Assert.That(JsonString.Return("hello").StrictEquals("Hello"), Is.False);
			Assert.That(JsonString.Return("123").StrictEquals(123), Is.False);
			Assert.That(JsonString.Return("").StrictEquals(false), Is.False);
			Assert.That(JsonString.Return("false").StrictEquals(false), Is.False);
			Assert.That(JsonString.Return("true").StrictEquals(true), Is.False);
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
				Assert.That(value.ToDateOnly(), Is.EqualTo(DateOnly.MinValue));
				Assert.That(value.IsLocalTime, Is.False, "MinValue should be unspecifed");
				Assert.That(value.IsUtc, Is.False, "MinValue should be unspecified");
			}

			{
				var value = JsonDateTime.MaxValue;
				Assert.That(value.Type, Is.EqualTo(JsonType.DateTime));
				Assert.That(value.IsNull, Is.False);
				Assert.That(value.IsDefault, Is.False);
				Assert.That(value.ToDateTime(), Is.EqualTo(DateTime.MaxValue));
				Assert.That(value.ToDateOnly(), Is.EqualTo(DateOnly.MaxValue));
				Assert.That(value.IsLocalTime, Is.False, "MaxValue should be unspecified");
				Assert.That(value.IsUtc, Is.False, "MaxValue should be unspecified");
			}

			{
				var value = JsonDateTime.DateOnlyMaxValue;
				Assert.That(value.Type, Is.EqualTo(JsonType.DateTime));
				Assert.That(value.IsNull, Is.False);
				Assert.That(value.IsDefault, Is.False);
				Assert.That(value.ToDateTime(), Is.EqualTo(DateTime.MaxValue.Date));
				Assert.That(value.ToDateOnly(), Is.EqualTo(DateOnly.MaxValue));
				Assert.That(value.IsLocalTime, Is.False, "MaxValue should be unspecified");
				Assert.That(value.IsUtc, Is.False, "MaxValue should be unspecified");
			}

			{
				var value = new JsonDateTime(1974, 3, 24);
				Assert.That(value.Type, Is.EqualTo(JsonType.DateTime));
				Assert.That(value.IsNull, Is.False);
				Assert.That(value.IsDefault, Is.False);
				Assert.That(value.ToDateTime(), Is.EqualTo(new DateTime(1974, 3, 24)));
				Assert.That(value.ToDateOnly(), Is.EqualTo(new DateOnly(1974, 3, 24)));
				Assert.That(value.IsLocalTime, Is.False, "TZ is unspecified");
				Assert.That(value.IsUtc, Is.False, "TZ is unspecified");
			}

			{
				var value = new JsonDateTime(1974, 3, 24, 12, 34, 56, DateTimeKind.Utc);
				Assert.That(value.Type, Is.EqualTo(JsonType.DateTime));
				Assert.That(value.IsNull, Is.False);
				Assert.That(value.IsDefault, Is.False);
				Assert.That(value.ToDateTime(), Is.EqualTo(new DateTime(1974, 3, 24, 12, 34, 56, DateTimeKind.Utc)));
				Assert.That(value.ToDateOnly(), Is.EqualTo(new DateOnly(1974, 3, 24)));
				Assert.That(value.IsLocalTime, Is.False);
				Assert.That(value.IsUtc, Is.True);
			}

			{
				var value = new JsonDateTime(1974, 3, 24, 12, 34, 56, 789, DateTimeKind.Local);
				Assert.That(value.Type, Is.EqualTo(JsonType.DateTime));
				Assert.That(value.IsNull, Is.False);
				Assert.That(value.IsDefault, Is.False);
				Assert.That(value.ToDateTime(), Is.EqualTo(new DateTime(1974, 3, 24, 12, 34, 56, 789, DateTimeKind.Local)));
				Assert.That(value.ToDateOnly(), Is.EqualTo(new DateOnly(1974, 3, 24)));
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
				Assert.That(value.ToDateOnly(), Is.EqualTo(DateOnly.FromDateTime(now)));
				Assert.That(value.IsLocalTime, Is.False);
				Assert.That(value.IsUtc, Is.True);
			}

			{
				var now = DateTimeOffset.Now;
				var today = DateOnly.FromDateTime(now.LocalDateTime);
				var value = new JsonDateTime(today);
				Assert.That(value.Type, Is.EqualTo(JsonType.DateTime));
				Assert.That(value.IsNull, Is.False);
				Assert.That(value.IsDefault, Is.False);
				Assert.That(value.ToDateTime(), Is.EqualTo(today.ToDateTime(default)));
				Assert.That(value.ToDateOnly(), Is.EqualTo(today));
				Assert.That(value.ToDateTimeOffset(), Is.EqualTo(new DateTimeOffset(today.ToDateTime(default), now.Offset)));
				Assert.That(value.IsLocalTime, Is.False); // DateOnly are "unspecified"
				Assert.That(value.IsUtc, Is.False);
			}

			// Nullables

			Assert.That(JsonDateTime.Return((DateTime?) null).Type, Is.EqualTo(JsonType.Null));
			Assert.That(JsonDateTime.Return((DateTimeOffset?) null).Type, Is.EqualTo(JsonType.Null));
			Assert.That(JsonDateTime.Return((DateOnly?) null).Type, Is.EqualTo(JsonType.Null));
			Assert.That(JsonDateTime.Return((DateTime?) DateTime.Now).Type, Is.EqualTo(JsonType.DateTime));
			Assert.That(JsonDateTime.Return((DateTimeOffset?) DateTimeOffset.Now).Type, Is.EqualTo(JsonType.DateTime));
			Assert.That(JsonDateTime.Return((DateOnly?) DateOnly.FromDateTime(DateTime.Now)).Type, Is.EqualTo(JsonType.DateTime));
		}

		[Test]
		public void Test_JsonGuid()
		{
			{
				var guid = Guid.NewGuid();
				var value = JsonString.Return(guid);
				Assert.That(value, Is.Not.Null);
				Assert.That(value.Type, Is.EqualTo(JsonType.String)); //note: for now, GUIDs are represented as strings with format "xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx"
				Assert.That(value.IsDefault, Is.False);
				Assert.That(value.IsNull, Is.False);
				Assert.That(value.ToString(), Is.EqualTo(guid.ToString("D"))); // we expected something like "xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx"
				Assert.That(value.ToStringOrDefault(), Is.EqualTo(guid.ToString("D"))); // we expected something like "xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx"
				Assert.That(value.ToGuid(), Is.EqualTo(guid));
				Assert.That(value.ToGuidOrDefault(), Is.EqualTo(guid));
				Assert.That(SerializeToSlice(value), Is.EqualTo(Slice.FromString("\"" + guid.ToString("D") + "\"")));
			}

			{
				var value = JsonString.Return(Guid.Empty);
				Assert.That(value, Is.Not.Null);
				Assert.That(value, Is.SameAs(JsonNull.Null)); //REVIEW: for now, Guid.Empty => JsonNull.Null. Maybe change this to return JsonString.Empty?
				Assert.That(value.ToString(), Is.EqualTo(string.Empty));
				Assert.That(value.ToStringOrDefault(), Is.Null);
				Assert.That(value.ToGuid(), Is.EqualTo(Guid.Empty));
				Assert.That(value.ToGuidOrDefault(), Is.Null);
				Assert.That(SerializeToSlice(value), Is.EqualTo(Slice.FromString("null")), "SerializeToSlice");
			}

			// Nullables
			{
				Assert.That(JsonString.Return((Guid?) null), Is.SameAs(JsonNull.Null));
				Assert.That(JsonString.Return((Guid?) Guid.Empty), Is.SameAs(JsonNull.Null)); //REVIEW: for now, Guid.Empty => JsonNull.Null. Maybe change this to return JsonString.Empty?
				Assert.That(JsonString.Return((Guid?) Guid.NewGuid()).Type, Is.EqualTo(JsonType.String));
			}
		}

		#endregion

		#region JsonNumber...

		[Test]
		public void Test_JsonNumber()
		{
			{ // 0 (singleton)
				var value = JsonNumber.Zero;
				Assert.That(value.Type, Is.EqualTo(JsonType.Number));
				Assert.That(value.IsNull, Is.False);
				Assert.That(value.IsDefault, Is.True);
				Assert.That(value.IsReadOnly, Is.True);
				Assert.That(value.ToObject(), Is.EqualTo(0));
				Assert.That(value.ToObject(), Is.InstanceOf<long>());
				Assert.That(value.IsDecimal, Is.False);
				Assert.That(value.IsNegative, Is.False);
				Assert.That(value.ToString(), Is.EqualTo("0"));
				Assert.That(SerializeToSlice(value), Is.EqualTo(Slice.FromString("0")));
			}

			{ // 1 (singleton)
				var value = JsonNumber.One;
				Assert.That(value.Type, Is.EqualTo(JsonType.Number));
				Assert.That(value.IsNull, Is.False);
				Assert.That(value.IsDefault, Is.False);
				Assert.That(value.IsReadOnly, Is.True);
				Assert.That(value.ToObject(), Is.EqualTo(1));
				Assert.That(value.ToObject(), Is.InstanceOf<long>());
				Assert.That(value.IsDecimal, Is.False);
				Assert.That(value.IsNegative, Is.False);
				Assert.That(value.ToString(), Is.EqualTo("1"));
				Assert.That(SerializeToSlice(value), Is.EqualTo(Slice.FromString("1")));
			}

			{ // -1 (singleton)
				var value = JsonNumber.MinusOne;
				Assert.That(value.Type, Is.EqualTo(JsonType.Number));
				Assert.That(value.IsNull, Is.False);
				Assert.That(value.IsDefault, Is.False);
				Assert.That(value.IsReadOnly, Is.True);
				Assert.That(value.ToObject(), Is.EqualTo(-1));
				Assert.That(value.ToObject(), Is.InstanceOf<long>());
				Assert.That(value.IsDecimal, Is.False);
				Assert.That(value.IsNegative, Is.True);
				Assert.That(value.ToString(), Is.EqualTo("-1"));
				Assert.That(SerializeToSlice(value), Is.EqualTo(Slice.FromString("-1")));
			}

			{ // 123 (cached)
				var value = JsonNumber.Return(123);
				Assert.That(value.Type, Is.EqualTo(JsonType.Number));
				Assert.That(value.IsNull, Is.False);
				Assert.That(value.IsDefault, Is.False);
				Assert.That(value.IsReadOnly, Is.True);
				Assert.That(value.ToInt32(), Is.EqualTo(123));
				Assert.That(value.ToObject(), Is.EqualTo(123));
				Assert.That(value.ToObject(), Is.InstanceOf<long>());
				Assert.That(value.IsDecimal, Is.False);
				Assert.That(value.IsNegative, Is.False);
				Assert.That(value.Literal, Is.EqualTo("123"));
				Assert.That(value.ToString(), Is.EqualTo("123"));
				Assert.That(SerializeToSlice(value), Is.EqualTo(Slice.FromString("123")));
			}

			{ // -123 (cached)
				var value = JsonNumber.Return(-123);
				Assert.That(value.Type, Is.EqualTo(JsonType.Number));
				Assert.That(value.IsNull, Is.False);
				Assert.That(value.IsDefault, Is.False);
				Assert.That(value.IsReadOnly, Is.True);
				Assert.That(value.ToInt32(), Is.EqualTo(-123));
				Assert.That(value.ToObject(), Is.EqualTo(-123));
				Assert.That(value.ToObject(), Is.InstanceOf<long>());
				Assert.That(value.IsDecimal, Is.False);
				Assert.That(value.IsNegative, Is.True);
				Assert.That(value.Literal, Is.EqualTo("-123"));
				Assert.That(value.ToString(), Is.EqualTo("-123"));
				Assert.That(SerializeToSlice(value), Is.EqualTo(Slice.FromString("-123")));
			}

			{ // 123456 (not cached)
				var value = JsonNumber.Return(123456);
				Assert.That(value.Type, Is.EqualTo(JsonType.Number));
				Assert.That(value.IsNull, Is.False);
				Assert.That(value.IsDefault, Is.False);
				Assert.That(value.IsReadOnly, Is.True);
				Assert.That(value.ToInt32(), Is.EqualTo(123456));
				Assert.That(value.ToObject(), Is.EqualTo(123456));
				Assert.That(value.ToObject(), Is.InstanceOf<long>());
				Assert.That(value.IsDecimal, Is.False);
				Assert.That(value.IsNegative, Is.False);
				Assert.That(value.Literal, Is.EqualTo("123456"));
				Assert.That(value.ToString(), Is.EqualTo("123456"));
				Assert.That(SerializeToSlice(value), Is.EqualTo(Slice.FromString("123456")));
			}

			{ // -123456 (not cached)
				var value = JsonNumber.Return(-123456);
				Assert.That(value.Type, Is.EqualTo(JsonType.Number));
				Assert.That(value.IsNull, Is.False);
				Assert.That(value.IsDefault, Is.False);
				Assert.That(value.IsReadOnly, Is.True);
				Assert.That(value.ToInt32(), Is.EqualTo(-123456));
				Assert.That(value.ToObject(), Is.EqualTo(-123456));
				Assert.That(value.ToObject(), Is.InstanceOf<long>());
				Assert.That(value.IsDecimal, Is.False);
				Assert.That(value.IsNegative, Is.True);
				Assert.That(value.Literal, Is.EqualTo("-123456"));
				Assert.That(value.ToString(), Is.EqualTo("-123456"));
				Assert.That(SerializeToSlice(value), Is.EqualTo(Slice.FromString("-123456")));
			}

			{ // int.MaxValue + 1 (long)
				var value = JsonNumber.Return(1L + int.MaxValue); // outside the range of Int32, so should be stored as an unsigned long
				Assert.That(value.Type, Is.EqualTo(JsonType.Number));
				Assert.That(value.IsNull, Is.False);
				Assert.That(value.IsDefault, Is.False);
				Assert.That(value.IsReadOnly, Is.True);
				Assert.That(value.ToObject(), Is.EqualTo(2147483648));
				Assert.That(value.ToObject(), Is.InstanceOf<long>());
				Assert.That(value.IsDecimal, Is.False);
				Assert.That(value.IsNegative, Is.False);
				Assert.That(value.ToString(), Is.EqualTo("2147483648"));
				Assert.That(SerializeToSlice(value), Is.EqualTo(Slice.FromString("2147483648")));
			}

			{ // 123UL (cached)
				var value = JsonNumber.Return(123UL);
				Assert.That(value.Type, Is.EqualTo(JsonType.Number));
				Assert.That(value.IsNull, Is.False);
				Assert.That(value.IsDefault, Is.False);
				Assert.That(value.IsReadOnly, Is.True);
				Assert.That(value.ToObject(), Is.EqualTo(123));
				Assert.That(value.ToObject(), Is.InstanceOf<long>(), "small integers should be converted to 'long'");
				Assert.That(value.IsDecimal, Is.False);
				Assert.That(value.IsNegative, Is.False);
				Assert.That(value.ToString(), Is.EqualTo("123"));
				Assert.That(SerializeToSlice(value), Is.EqualTo(Slice.FromString("123")));

				Assert.That(value, Is.SameAs(JsonNumber.Return(123)), "should return the same cached instance");
			}

			{
				var value = JsonNumber.Return(1.23f);
				Assert.That(value.Type, Is.EqualTo(JsonType.Number));
				Assert.That(value.IsNull, Is.False);
				Assert.That(value.IsDefault, Is.False);
				Assert.That(value.IsReadOnly, Is.True);
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
				Assert.That(value.IsReadOnly, Is.True);
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
				Assert.That(value.IsReadOnly, Is.True);
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
				Assert.That(value.IsReadOnly, Is.True);
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
				Assert.That(value.IsReadOnly, Is.True);
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
				Assert.That(value.IsReadOnly, Is.True);
				Assert.That(value.ToObject(), Is.EqualTo(1d));
				Assert.That(value.ToObject(), Is.InstanceOf<double>());
				Assert.That(value.IsDecimal, Is.True);
				Assert.That(value.IsNegative, Is.False);
				Assert.That(value.ToString(), Is.EqualTo("1"));
				Assert.That(SerializeToSlice(value), Is.EqualTo(Slice.FromString("1")));
			}

			// Nullables

			Assert.That(JsonNumber.Return((int?) null), Is.SameAs(JsonNull.Null));
			Assert.That(JsonNumber.Return((uint?) null), Is.SameAs(JsonNull.Null));
			Assert.That(JsonNumber.Return((long?) null), Is.SameAs(JsonNull.Null));
			Assert.That(JsonNumber.Return((ulong?) null), Is.SameAs(JsonNull.Null));
			Assert.That(JsonNumber.Return((float?) null), Is.SameAs(JsonNull.Null));
			Assert.That(JsonNumber.Return((double?) null), Is.SameAs(JsonNull.Null));

			Assert.That(JsonNumber.Return((int?) 42), Is.InstanceOf<JsonNumber>().And.EqualTo(42));
			Assert.That(JsonNumber.Return((uint?) 42), Is.InstanceOf<JsonNumber>().And.EqualTo(42U));
			Assert.That(JsonNumber.Return((long?) 42), Is.InstanceOf<JsonNumber>().And.EqualTo(42L));
			Assert.That(JsonNumber.Return((ulong?) 42), Is.InstanceOf<JsonNumber>().And.EqualTo(42UL));
			Assert.That(JsonNumber.Return((float?) 3.14f), Is.InstanceOf<JsonNumber>().And.EqualTo(3.14f));
			Assert.That(JsonNumber.Return((double?) 3.14d), Is.InstanceOf<JsonNumber>().And.EqualTo(3.14d));

			// Conversions

			// Primitive
			Assert.That(JsonNumber.Return(123).ToInt32(), Is.EqualTo(123));
			Assert.That(JsonNumber.Return(-123).ToInt32(), Is.EqualTo(-123));
			Assert.That(JsonNumber.Return(123L).ToInt64(), Is.EqualTo(123L));
			Assert.That(JsonNumber.Return(-123L).ToInt64(), Is.EqualTo(-123L));
			Assert.That(JsonNumber.Return(123f).ToSingle(), Is.EqualTo(123f));
			Assert.That(JsonNumber.Return(123d).ToDouble(), Is.EqualTo(123d));
			Assert.That(JsonNumber.Return(Math.PI).ToDouble(), Is.EqualTo(Math.PI));

			Assert.That(JsonNumber.Return(123).Required<int>(), Is.EqualTo(123));
			Assert.That(JsonNumber.Return(-123).Required<int>(), Is.EqualTo(-123));
			Assert.That(JsonNumber.Return(123L).Required<long>(), Is.EqualTo(123L));
			Assert.That(JsonNumber.Return(-123L).Required<long>(), Is.EqualTo(-123L));
			Assert.That(JsonNumber.Return(123f).Required<float>(), Is.EqualTo(123f));
			Assert.That(JsonNumber.Return(123d).Required<double>(), Is.EqualTo(123d));
			Assert.That(JsonNumber.Return(Math.PI).Required<double>(), Is.EqualTo(Math.PI));

			// Enum
			// ... that derives from Int32
			Assert.That(JsonNumber.Zero.Bind(typeof (DummyJsonEnum), null), Is.EqualTo(DummyJsonEnum.None), "{0}.Bind(DummyJsonEnum)");
			Assert.That(JsonNumber.One.Bind(typeof (DummyJsonEnum), null), Is.EqualTo(DummyJsonEnum.Foo), "{1}.Bind(DummyJsonEnum)");
			Assert.That(JsonNumber.Return(42).Bind(typeof (DummyJsonEnum), null), Is.EqualTo(DummyJsonEnum.Bar), "{42}.Bind(DummyJsonEnum)");
			Assert.That(JsonNumber.Return(66).Bind(typeof (DummyJsonEnum), null), Is.EqualTo((DummyJsonEnum) 66), "{66}.Bind(DummyJsonEnum)");
			// ... that does not derive from Int32
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
			//note: dates are converted into the number of days (floating point) since Unix Epoch, using UTC as the reference timezone
			Assert.That(JsonNumber.Return(0).ToDateTime(), Is.EqualTo(new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)), "0.ToDateTime()");
			Assert.That(JsonNumber.Return(1).ToDateTime(), Is.EqualTo(new DateTime(1970, 1, 1, 0, 0, 1, DateTimeKind.Utc)), "1.ToDateTime()");
			Assert.That(JsonNumber.Return(86400).ToDateTime(), Is.EqualTo(new DateTime(1970, 1, 2, 0, 0, 0, DateTimeKind.Utc)), "86400.ToDateTime()");
			Assert.That(JsonNumber.Return(1484830412.854).ToDateTime(), Is.EqualTo(new DateTime(2017, 1, 19, 12, 53, 32, 854, DateTimeKind.Utc)).Within(TimeSpan.FromMilliseconds(1)), "(DAYS).ToDateTime()");
			Assert.That(JsonNumber.Return(new DateTime(2017, 1, 19, 12, 53, 32, 854, DateTimeKind.Utc)).ToDouble(), Is.EqualTo(1484830412.854), "(UTC).Value");
			Assert.That(JsonNumber.Return(new DateTime(2017, 1, 19, 13, 53, 32, 854, DateTimeKind.Local)).ToDouble(), Is.EqualTo(1484830412.854), "(LOCAL).Value");
			Assert.That(JsonNumber.Return(DateTime.MinValue).ToDouble(), Is.EqualTo(0), "MinValue"); // by convention, MinValue == 0 == epoch
			Assert.That(JsonNumber.Return(DateTime.MaxValue).ToDouble(), Is.EqualTo(double.NaN), "MaxValue"); // by convention, MaxValue == NaN
			Assert.That(JsonNumber.NaN.ToDateTime(), Is.EqualTo(DateTime.MaxValue), "MaxValue"); // by convention, NaN == MaxValue

			// DateTimeOffset
			//note: dates are converted into the number of days (floating point) since Unix Epoch, using UTC as the reference timezone
			Assert.That(JsonNumber.Return(0).ToDateTimeOffset(), Is.EqualTo(new DateTimeOffset(1970, 1, 1, 0, 0, 0, TimeSpan.Zero)), "0.ToDateTimeOffset()");
			Assert.That(JsonNumber.Return(1).ToDateTimeOffset(), Is.EqualTo(new DateTimeOffset(1970, 1, 1, 0, 0, 1, TimeSpan.Zero)), "1.ToDateTimeOffset()");
			Assert.That(JsonNumber.Return(86400).ToDateTimeOffset(), Is.EqualTo(new DateTimeOffset(1970, 1, 2, 0, 0, 0, TimeSpan.Zero)), "86400.ToDateTimeOffset()");
			Assert.That(JsonNumber.Return(1484830412.854).ToDateTimeOffset(), Is.EqualTo(new DateTimeOffset(2017, 1, 19, 12, 53, 32, 854, TimeSpan.Zero)).Within(TimeSpan.FromMilliseconds(1)), "(DAYS).ToDateTimeOffset()");
			Assert.That(JsonNumber.Return(new DateTimeOffset(2017, 1, 19, 12, 53, 32, 854, offset: TimeSpan.Zero)).ToDouble(), Is.EqualTo(1484830412.854), "(UTC).Value");
			Assert.That(JsonNumber.Return(new DateTimeOffset(2017, 1, 19, 13, 53, 32, 854, offset: TimeSpan.FromHours(1))).ToDouble(), Is.EqualTo(1484830412.854), "(LOCAL).Value");
			Assert.That(JsonNumber.Return(DateTimeOffset.MinValue).ToDouble(), Is.EqualTo(0), "MinValue"); // by convention, MinValue == 0 == epoch
			Assert.That(JsonNumber.Return(DateTimeOffset.MaxValue).ToDouble(), Is.EqualTo(double.NaN), "MaxValue"); // by convention, MaxValue == NaN
			Assert.That(JsonNumber.NaN.ToDateTimeOffset(), Is.EqualTo(DateTimeOffset.MaxValue), "MaxValue"); // by convention, NaN == MaxValue

			// DateOnly
			//note: dates are converted into the number of days (floating point) since Unix Epoch, using UTC as the reference timezone
			Assert.That(JsonNumber.Return(0).ToDateOnly(), Is.EqualTo(new DateOnly(1970, 1, 1)), "0.ToDateOnly()");
			Assert.That(JsonNumber.Return(1).ToDateOnly(), Is.EqualTo(new DateOnly(1970, 1, 2)), "1.ToDateOnly()");
			Assert.That(JsonNumber.Return(31).ToDateOnly(), Is.EqualTo(new DateOnly(1970, 2, 1)), "31.ToDateOnly()");
			Assert.That(JsonNumber.Return(new DateOnly(2017, 1, 19)).ToDouble(), Is.EqualTo(17185), "(DATE).Value");
			Assert.That(JsonNumber.Return(17185).ToDateOnly(), Is.EqualTo(new DateOnly(2017, 1, 19)), "(DAYS).ToDateOnly()");
			Assert.That(JsonNumber.Return(DateOnly.MinValue).ToDouble(), Is.EqualTo(0), "MinValue"); // by convention, MinValue == 0 == epoch
			Assert.That(JsonNumber.Return(DateOnly.MaxValue).ToDouble(), Is.EqualTo(double.NaN), "MaxValue"); // by convention, MaxValue == NaN
			Assert.That(JsonNumber.NaN.ToDateOnly(), Is.EqualTo(DateOnly.MaxValue), "MaxValue"); // by convention, NaN == MaxValue

			// TimeOnly
			//note: times are encoded as the number of seconds since midnight
			Assert.That(JsonNumber.Return(0).ToTimeOnly(), Is.EqualTo(new TimeOnly(0, 0, 0)), "0.ToTimeOnly()");
			Assert.That(JsonNumber.Return(1).ToTimeOnly(), Is.EqualTo(new TimeOnly(0, 0, 1)), "1.ToTimeOnly()");
			Assert.That(JsonNumber.Return(60).ToTimeOnly(), Is.EqualTo(new TimeOnly(0, 1, 0)), "31.ToTimeOnly()");
			Assert.That(JsonNumber.Return(3600).ToTimeOnly(), Is.EqualTo(new TimeOnly(1, 0, 0)), "31.ToTimeOnly()");
			Assert.That(JsonNumber.Return(new TimeOnly(12, 34, 56)).ToDouble(), Is.EqualTo(45296), "(TIME).Value");
			Assert.That(JsonNumber.Return(45296).ToTimeOnly(), Is.EqualTo(new TimeOnly(12, 34, 56)), "(SECONDS).ToTimeOnly()");
			Assert.That(JsonNumber.Return(TimeOnly.MinValue).ToDouble(), Is.EqualTo(0d), "MinValue"); // by convention, MinValue == 0 == midnight
			Assert.That(JsonNumber.Return(TimeOnly.MaxValue).ToDouble(), Is.EqualTo(86399.9999999d), "MaxValue"); // by convention, MaxValue == NaN
			Assert.That(JsonNumber.NaN.ToTimeOnly(), Is.EqualTo(TimeOnly.MaxValue), "MaxValue"); // by convention, NaN == MaxValue

			// Instant
			//note: instants are converted into the number of days (floating point) since Unix Epoch
			Assert.That(JsonNumber.Return(0).ToInstant(), Is.EqualTo(NodaTime.Instant.FromUtc(1970, 1, 1, 0, 0, 0)), "0.ToInstant()");
			Assert.That(JsonNumber.Return(1).ToInstant(), Is.EqualTo(NodaTime.Instant.FromUtc(1970, 1, 1, 0, 0, 1)), "1.ToInstant()");
			Assert.That(JsonNumber.Return(86400).ToInstant(), Is.EqualTo(NodaTime.Instant.FromUtc(1970, 1, 2, 0, 0, 0)), "86400.ToInstant()");
			Assert.That(JsonNumber.Return(1484830412.854).ToInstant(), Is.EqualTo(NodaTime.Instant.FromDateTimeUtc(new DateTime(2017, 1, 19, 12, 53, 32, 854, DateTimeKind.Utc)))/*.Within(NodaTime.Duration.FromMilliseconds(1))*/, "(DAYS).ToInstant()");
			Assert.That(JsonNumber.Return(NodaTime.Instant.FromDateTimeUtc(new DateTime(2017, 1, 19, 12, 53, 32, 854, DateTimeKind.Utc))).ToDouble(), Is.EqualTo(1484830412.854), "(UTC).Value");
			Assert.That(JsonNumber.Return(NodaTime.Instant.MinValue).ToDouble(), Is.EqualTo(NodaTime.Instant.FromUtc(-9998, 1 , 1, 0, 0, 0).ToUnixTimeSeconds()), "MinValue");
			Assert.That(JsonNumber.Return(NodaTime.Instant.MaxValue).ToDouble(), Is.EqualTo(NodaTime.Instant.FromUtc(9999, 12, 31, 23, 59, 59).ToUnixTimeSeconds() + 0.999999999d), "MaxValue");
			Assert.That(JsonNumber.NaN.ToInstant(), Is.EqualTo(NodaTime.Instant.MaxValue), "MaxValue"); //par convention, NaN == MaxValue

			// String
			Assert.That(JsonNumber.Zero.Bind<string>(), Is.EqualTo("0"));
			Assert.That(JsonNumber.One.Bind<string>(), Is.EqualTo("1"));
			Assert.That(JsonNumber.Return(123).Bind<string>(), Is.EqualTo("123"));
			Assert.That(JsonNumber.Return(-123).Bind<string>(), Is.EqualTo("-123"));
			Assert.That(JsonNumber.Return(Math.PI).Bind<string>(), Is.EqualTo(Math.PI.ToString("R")));

			// auto cast
			JsonNumber v;

			v = JsonNumber.Return(int.MaxValue);
			Assert.That(v, Is.Not.Null);
			Assert.That(v.IsDecimal, Is.False);
			Assert.That(v.IsUnsigned, Is.False);
			Assert.That(v.ToInt32(), Is.EqualTo(int.MaxValue));
			Assert.That(v.Required<int>(), Is.EqualTo(int.MaxValue));
			Assert.That((int) v, Is.EqualTo(int.MaxValue));

			v = JsonNumber.Return(uint.MaxValue);
			Assert.That(v.IsUnsigned, Is.False, "uint.MaxValue is small enough to fit in a long");
			Assert.That(v.IsDecimal, Is.False);
			Assert.That(v, Is.Not.Null);
			Assert.That(v.ToUInt32(), Is.EqualTo(uint.MaxValue));
			Assert.That(v.Required<uint>(), Is.EqualTo(uint.MaxValue));
			Assert.That((uint) v, Is.EqualTo(uint.MaxValue));

			v = JsonNumber.Return(long.MaxValue);
			Assert.That(v, Is.Not.Null);
			Assert.That(v.IsDecimal, Is.False);
			Assert.That(v.IsUnsigned, Is.False);
			Assert.That(v.ToInt64(), Is.EqualTo(long.MaxValue));
			Assert.That(v.Required<long>(), Is.EqualTo(long.MaxValue));
			Assert.That((long) v, Is.EqualTo(long.MaxValue));

			v = JsonNumber.Return(ulong.MaxValue);
			Assert.That(v, Is.Not.Null);
			Assert.That(v.IsDecimal, Is.False);
			Assert.That(v.IsUnsigned, Is.True);
			Assert.That(v.ToUInt64(), Is.EqualTo(ulong.MaxValue));
			Assert.That(v.Required<ulong>(), Is.EqualTo(ulong.MaxValue));
			Assert.That((ulong) v, Is.EqualTo(ulong.MaxValue));

			v = JsonNumber.Return(Math.PI);
			Assert.That(v, Is.Not.Null);
			Assert.That(v.IsDecimal, Is.True);
			Assert.That(v.IsUnsigned, Is.False);
			Assert.That(v.ToDouble(), Is.EqualTo(Math.PI));
			Assert.That(v.Required<double>(), Is.EqualTo(Math.PI));
			Assert.That((double) v, Is.EqualTo(Math.PI));

			v = JsonNumber.Return(1.234f);
			Assert.That(v, Is.Not.Null);
			Assert.That(v.IsDecimal, Is.True);
			Assert.That(v.IsUnsigned, Is.False);
			Assert.That(v.ToSingle(), Is.EqualTo(1.234f));
			Assert.That(v.Required<float>(), Is.EqualTo(1.234f));
			Assert.That((float) v, Is.EqualTo(1.234f));

			v = (JsonNumber) JsonNumber.Return((int?) 123);
			Assert.That(v, Is.Not.Null);
			Assert.That(v.IsDecimal, Is.False);
			Assert.That(v.IsUnsigned, Is.False);
			Assert.That(v.ToInt32OrDefault(), Is.EqualTo(123));
			Assert.That(v.As<int?>(), Is.EqualTo(123));
			Assert.That((int?) v, Is.EqualTo(123));

			v = (JsonNumber) JsonNumber.Return((uint?) 123);
			Assert.That(v, Is.Not.Null);
			Assert.That(v.IsDecimal, Is.False);
			Assert.That(v.IsUnsigned, Is.False, "123u fits in a long");
			Assert.That(v.ToUInt32OrDefault(), Is.EqualTo(123));
			Assert.That(v.As<uint?>(), Is.EqualTo(123));
			Assert.That((uint?) v, Is.EqualTo(123));
		}

		[Test]
		public void Test_JsonNumber_CompareTo()
		{
			#pragma warning disable CS1718
			#pragma warning disable NUnit2010
			#pragma warning disable NUnit2043
			// ReSharper disable EqualExpressionComparison
			// ReSharper disable CannotApplyEqualityOperatorToType

			// use random numbers, so that we don't end up just testing reference equality between cached small numbers
			int[] nums = [ NextInt32(), NextInt32(), NextInt32() ];
			Assume.That(nums, Is.Unique);
			Array.Sort(nums);

			JsonNumber x0 = JsonNumber.Return(nums[0]);
			JsonNumber x1 = JsonNumber.Return(nums[1]);
			JsonNumber x2 = JsonNumber.Return(nums[2]);

			// JsonNumber vs JsonNumber

			Assert.That(x0 == x0, Is.True);
			Assert.That(x0 == x1, Is.False);
			Assert.That(x0 == x2, Is.False);

			Assert.That(x0 != x0, Is.False);
			Assert.That(x0 != x1, Is.True);
			Assert.That(x0 != x2, Is.True);

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

			// JsonNumber vs ValueType (no allocations)
			// => this comparisons should not allocate any JsonValue instance during the operation

			Expression<Func<JsonNumber, int, bool>> expr1 = (jnum, x) => jnum < x;
			Assert.That(expr1.Body.NodeType, Is.EqualTo(ExpressionType.LessThan));
			Assert.That(((BinaryExpression)expr1.Body).Method?.Name, Is.EqualTo("op_LessThan"));
			Assert.That(((BinaryExpression)expr1.Body).Method?.GetParameters()[0].ParameterType, Is.EqualTo(typeof(JsonNumber)));
			Assert.That(((BinaryExpression)expr1.Body).Method?.GetParameters()[1].ParameterType, Is.EqualTo(typeof(long)));

			JsonNumber x = 1;

			Assert.That(x < 1, Is.False);
			Assert.That(x <= 1, Is.True);
			Assert.That(x > 1, Is.False);
			Assert.That(x >= 1, Is.True);

			Assert.That(x < 2, Is.True);
			Assert.That(x <= 2, Is.True);
			Assert.That(x > 2, Is.False);
			Assert.That(x >= 2, Is.False);

			// ReSharper restore CannotApplyEqualityOperatorToType
			// ReSharper restore EqualExpressionComparison
			#pragma warning restore NUnit2043
			#pragma warning restore NUnit2010
			#pragma warning restore CS1718
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
		public void Test_JsonNumber_ValueEquals()
		{
			// int
			Assert.That(JsonNumber.Return(123).ValueEquals<int>(123), Is.True);
			Assert.That(JsonNumber.Return(123).ValueEquals<int>(456), Is.False);
			Assert.That(JsonNumber.Return(int.MaxValue).ValueEquals<int>(int.MaxValue), Is.True);
			Assert.That(JsonNumber.Return(int.MinValue).ValueEquals<int>(int.MinValue), Is.True);
			Assert.That(JsonNumber.Return(int.MaxValue).ValueEquals<int>(int.MinValue), Is.False);
			Assert.That(JsonNumber.Return(123).ValueEquals<int?>(123), Is.True);
			Assert.That(JsonNumber.Return(123).ValueEquals<int?>(456), Is.False);
			Assert.That(JsonNumber.Return(123).ValueEquals<int?>(null), Is.False);

			// uint
			Assert.That(JsonNumber.Return(123U).ValueEquals<uint>(123), Is.True);
			Assert.That(JsonNumber.Return(123U).ValueEquals<uint>(456), Is.False);
			Assert.That(JsonNumber.Return(uint.MaxValue).ValueEquals<uint>(uint.MaxValue), Is.True);
			Assert.That(JsonNumber.Return(123U).ValueEquals<uint?>(123), Is.True);
			Assert.That(JsonNumber.Return(123U).ValueEquals<uint?>(456), Is.False);
			Assert.That(JsonNumber.Return(123U).ValueEquals<uint?>(null), Is.False);

			// long
			Assert.That(JsonNumber.Return(123L).ValueEquals<long>(123), Is.True);
			Assert.That(JsonNumber.Return(123L).ValueEquals<long>(456), Is.False);
			Assert.That(JsonNumber.Return(long.MaxValue).ValueEquals<long>(long.MaxValue), Is.True);
			Assert.That(JsonNumber.Return(long.MinValue).ValueEquals<long>(long.MinValue), Is.True);
			Assert.That(JsonNumber.Return(long.MaxValue).ValueEquals<long>(long.MinValue), Is.False);
			Assert.That(JsonNumber.Return(123L).ValueEquals<long?>(123), Is.True);
			Assert.That(JsonNumber.Return(123L).ValueEquals<long?>(456), Is.False);
			Assert.That(JsonNumber.Return(123L).ValueEquals<long?>(null), Is.False);

			// ulong
			Assert.That(JsonNumber.Return(123UL).ValueEquals<ulong>(123), Is.True);
			Assert.That(JsonNumber.Return(123UL).ValueEquals<ulong>(456), Is.False);
			Assert.That(JsonNumber.Return(ulong.MaxValue).ValueEquals<ulong>(ulong.MaxValue), Is.True);
			Assert.That(JsonNumber.Return(123UL).ValueEquals<ulong?>(123), Is.True);
			Assert.That(JsonNumber.Return(123UL).ValueEquals<ulong?>(456), Is.False);
			Assert.That(JsonNumber.Return(123UL).ValueEquals<ulong?>(null), Is.False);

			// short
			Assert.That(JsonNumber.Return(123).ValueEquals<short>(123), Is.True);
			Assert.That(JsonNumber.Return(123).ValueEquals<short>(456), Is.False);
			Assert.That(JsonNumber.Return(short.MaxValue).ValueEquals<short>(short.MaxValue), Is.True);
			Assert.That(JsonNumber.Return(short.MinValue).ValueEquals<short>(short.MinValue), Is.True);
			Assert.That(JsonNumber.Return(short.MaxValue).ValueEquals<short>(short.MinValue), Is.False);
			Assert.That(JsonNumber.Return(123).ValueEquals<short?>(123), Is.True);
			Assert.That(JsonNumber.Return(123).ValueEquals<short?>(456), Is.False);
			Assert.That(JsonNumber.Return(123).ValueEquals<short?>(null), Is.False);

			// ushort
			Assert.That(JsonNumber.Return(123U).ValueEquals<ushort>(123), Is.True);
			Assert.That(JsonNumber.Return(123U).ValueEquals<ushort>(456), Is.False);
			Assert.That(JsonNumber.Return(ushort.MaxValue).ValueEquals<ushort>(ushort.MaxValue), Is.True);
			Assert.That(JsonNumber.Return(123U).ValueEquals<ushort?>(123), Is.True);
			Assert.That(JsonNumber.Return(123U).ValueEquals<ushort?>(456), Is.False);
			Assert.That(JsonNumber.Return(123U).ValueEquals<ushort?>(null), Is.False);

			// float
			Assert.That(JsonNumber.Return(1.23f).ValueEquals<float>(1.23f), Is.True);
			Assert.That(JsonNumber.Return(1.23f).ValueEquals<float>(4.56f), Is.False);
			Assert.That(JsonNumber.Return(float.NaN).ValueEquals<float>(float.NaN), Is.True);
			Assert.That(JsonNumber.Return(1.23f).ValueEquals<float>(float.NaN), Is.False);
			Assert.That(JsonNumber.Return(float.MaxValue).ValueEquals<float>(float.MaxValue), Is.True);
			Assert.That(JsonNumber.Return(float.MinValue).ValueEquals<float>(float.MinValue), Is.True);
			Assert.That(JsonNumber.Return(float.MaxValue).ValueEquals<float>(float.MinValue), Is.False);
			Assert.That(JsonNumber.Return(1.23f).ValueEquals<float?>(1.23f), Is.True);
			Assert.That(JsonNumber.Return(1.23f).ValueEquals<float?>(4.56f), Is.False);
			Assert.That(JsonNumber.Return(1.23f).ValueEquals<float?>(null), Is.False);

			// double
			Assert.That(JsonNumber.Return(Math.PI).ValueEquals<double>(Math.PI), Is.True);
			Assert.That(JsonNumber.Return(Math.PI).ValueEquals<double>(3.14), Is.False);
			Assert.That(JsonNumber.Return(double.NaN).ValueEquals<double>(double.NaN), Is.True);
			Assert.That(JsonNumber.Return(Math.PI).ValueEquals<double>(double.NaN), Is.False);
			Assert.That(JsonNumber.Return(double.MaxValue).ValueEquals<double>(double.MaxValue), Is.True);
			Assert.That(JsonNumber.Return(double.MinValue).ValueEquals<double>(double.MinValue), Is.True);
			Assert.That(JsonNumber.Return(double.MaxValue).ValueEquals<double>(double.MinValue), Is.False);
			Assert.That(JsonNumber.Return(Math.PI).ValueEquals<double?>(Math.PI), Is.True);
			Assert.That(JsonNumber.Return(Math.PI).ValueEquals<double?>(3.14), Is.False);
			Assert.That(JsonNumber.Return(Math.PI).ValueEquals<double?>(null), Is.False);

			// decimal
			Assert.That(JsonNumber.Return(decimal.One).ValueEquals<decimal>(decimal.One), Is.True);
			Assert.That(JsonNumber.Return(decimal.One).ValueEquals<decimal>(decimal.Zero), Is.False);
			Assert.That(JsonNumber.Return(decimal.MaxValue).ValueEquals<decimal>(decimal.MaxValue), Is.True);
			Assert.That(JsonNumber.Return(decimal.MinValue).ValueEquals<decimal>(decimal.MinValue), Is.True);
			Assert.That(JsonNumber.Return(decimal.MaxValue).ValueEquals<decimal>(decimal.MinValue), Is.False);
			Assert.That(JsonNumber.Return(decimal.One).ValueEquals<decimal?>(decimal.One), Is.True);
			Assert.That(JsonNumber.Return(decimal.One).ValueEquals<decimal?>(decimal.Zero), Is.False);
			Assert.That(JsonNumber.Return(decimal.One).ValueEquals<decimal?>(null), Is.False);
		}

		[Test]
		public void Test_JsonNumber_StrictEquals()
		{
			Assert.That(JsonNumber.Return(123).StrictEquals(123), Is.True);
			Assert.That(JsonNumber.Return(123).StrictEquals(123L), Is.True);
			Assert.That(JsonNumber.Return(123).StrictEquals(123.0), Is.True);

			Assert.That(JsonNumber.Return(0).StrictEquals(false), Is.False);
			Assert.That(JsonNumber.Return(0).StrictEquals(""), Is.False);
			Assert.That(JsonNumber.Return(1).StrictEquals(true), Is.False);
			Assert.That(JsonNumber.Return(123).StrictEquals("123"), Is.False);
		}

		[Test]
		public void Test_JsonNumber_RoundingBug()
		{
			// When serializing/deserializing a double with the form "7.5318246509562359", there is an issue when convertion from decimal to double (the ULPS will change due to the difference in precision)
			// => on vérifie que le JsonNumber est capable de gérer correctement ce problème

			double x = 7.5318246509562359d;
			Assert.That((double)((decimal)x), Is.Not.EqualTo(x), $"Check that {x:R} gets corrupted during roundtrip by the CLR");
			Assert.That(JsonNumber.Return(x).ToString(), Is.EqualTo(x.ToString("R")));
			Assert.That(((JsonNumber) JsonValue.Parse("7.5318246509562359")).ToDouble(), Is.EqualTo(x), $"Rounding Bug check: {x:R} should not change!");

			x = 3.8219629199346357;
			Assert.That((double)((decimal)x), Is.Not.EqualTo(x), $"Check that {x:R} gets corrupted during roundtrip by the CLR");
			Assert.That(JsonNumber.Return(x).ToString(), Is.EqualTo(x.ToString("R")));
			Assert.That(((JsonNumber) JsonValue.Parse("3.8219629199346357")).ToDouble(), Is.EqualTo(x), $"Rounding Bug check: {x:R} should not change!");

			// meme problème avec les float !
			float y = 7.53182459f;
			Assert.That((float)((decimal)y), Is.Not.EqualTo(y), $"Check that {y:R} gets corrupted during roundtrip by the CLR");
			Assert.That(JsonNumber.Return(y).ToString(), Is.EqualTo(y.ToString("R")));
			Assert.That(((JsonNumber) JsonValue.Parse("7.53182459")).ToSingle(), Is.EqualTo(y), $"Rounding Bug check: {y:R}");
		}

		[Test]
		public void Test_JsonNumber_Interning()
		{
			//NOTE: currently, interning should only be used for small numbers: -128..+127 et 0U...255U
			// => if this test fails, please check that this range hasn't changed !

			Assert.That(JsonNumber.Return(0), Is.SameAs(JsonNumber.Zero), "Zero");
			Assert.That(JsonNumber.Return(1), Is.SameAs(JsonNumber.One), "One");
			Assert.That(JsonNumber.Return(-1), Is.SameAs(JsonNumber.MinusOne), "MinusOne");
			Assert.That(JsonNumber.Return(42), Is.SameAs(JsonNumber.Return(42)), "42 should be in the small signed cache");
			Assert.That(JsonNumber.Return(-42), Is.SameAs(JsonNumber.Return(-42)), "-42 should be in the small signed cache");
			Assert.That(JsonNumber.Return(255), Is.SameAs(JsonNumber.Return(255)), "255 should be in the small signed cache");
			Assert.That(JsonNumber.Return(-128), Is.SameAs(JsonNumber.Return(-128)), "-255 should be in the small signed cache");

			// must also intern values in an array or list
			var arr = new int[10].ToJsonArray();
			Assert.That(arr, Is.Not.Null.And.Count.EqualTo(10), "array of zeroes");
			Assert.That(arr[0], Is.SameAs(JsonNumber.Zero));
			Assert.That(arr[0].ToInt32(), Is.EqualTo(0));
			for (int i = 1; i < arr.Count; i++)
			{
				Assert.That(arr[i], Is.SameAs(JsonNumber.Zero), $"arr[{i}]");
			}

			// list
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

			// convert the same sequence twice should yield the same number singletons
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

		#endregion

		#region JsonArray...

		[Test]
		public void Test_JsonArray()
		{
			// [ ]
			var arr = new JsonArray();
			Assert.That(arr, Has.Count.Zero);
			Assert.That(arr.IsReadOnly, Is.False);

			// [ "hello" ]
			arr.Add("hello");
			Assert.That(arr, Has.Count.EqualTo(1));
			Assert.That(arr[0], Is.EqualTo(JsonString.Return("hello")));
			Assert.That(arr.Get<string>(0), Is.EqualTo("hello"));
			Assert.That(arr.ToArray(), Is.EqualTo(new[] { JsonString.Return("hello") }));
			Assert.That(arr.ToArray<string>(), Is.EqualTo(new[] { "hello" }));
			Assert.That(arr.ToList<string>(), Is.EqualTo(new List<string> { "hello" }));
			Assert.That(arr.ToJsonCompact(), Is.EqualTo("""["hello"]"""));
			Assert.That(arr.Required<ValueTuple<string>>(), Is.EqualTo(ValueTuple.Create("hello")));

			// [ "hello", "world" ]
			arr.Add("world");
			Assert.That(arr, Has.Count.EqualTo(2));
			Assert.That(arr[0], Is.EqualTo(JsonString.Return("hello")));
			Assert.That(arr[1], Is.EqualTo(JsonString.Return("world")));
			Assert.That(arr.Get<string>(0), Is.EqualTo("hello"));
			Assert.That(arr.Get<string>(1), Is.EqualTo("world"));
			Assert.That(arr.ToArray(), Is.EqualTo(new[] { JsonString.Return("hello"), JsonString.Return("world") }));
			Assert.That(arr.ToArray<string>(), Is.EqualTo(new[] { "hello", "world" }));
			Assert.That(arr.ToList<string>(), Is.EqualTo(new List<string> { "hello", "world" }));
			Assert.That(arr.ToJsonCompact(), Is.EqualTo("""["hello","world"]"""));

			// [ "hello", "le monde" ]
			arr[1] = "le monde";
			Assert.That(arr, Has.Count.EqualTo(2));
			Assert.That(arr[0], Is.EqualTo(JsonString.Return("hello")));
			Assert.That(arr[1], Is.EqualTo(JsonString.Return("le monde")));
			Assert.That(arr.Get<string>(0), Is.EqualTo("hello"));
			Assert.That(arr.Get<string>(1), Is.EqualTo("le monde"));
			Assert.That(arr.ToArray(), Is.EqualTo(new[] { JsonString.Return("hello"), JsonString.Return("le monde") }));
			Assert.That(arr.ToArray<string>(), Is.EqualTo(new[] { "hello", "le monde" }));
			Assert.That(arr.ToList<string>(), Is.EqualTo(new List<string> { "hello", "le monde" }));
			Assert.That(arr.ToJsonCompact(), Is.EqualTo("""["hello","le monde"]"""));

			// [ "hello", "le monde", 123 ]
			arr.Add(123);
			Assert.That(arr, Has.Count.EqualTo(3));
			Assert.That(arr[0], Is.EqualTo(JsonString.Return("hello")));
			Assert.That(arr[1], Is.EqualTo(JsonString.Return("le monde")));
			Assert.That(arr[2], Is.EqualTo(JsonNumber.Return(123)));
			Assert.That(arr.Get<string>(0), Is.EqualTo("hello"));
			Assert.That(arr.Get<string>(1), Is.EqualTo("le monde"));
			Assert.That(arr.Get<int>(2), Is.EqualTo(123));
			Assert.That(arr.ToArray(), Is.EqualTo(new[] { JsonString.Return("hello"), JsonString.Return("le monde"), JsonNumber.Return(123) }));
			Assert.That(arr.ToJsonCompact(), Is.EqualTo("""["hello","le monde",123]"""));

			// [ "hello", 123 ]
			arr.RemoveAt(1);
			Assert.That(arr, Has.Count.EqualTo(2));
			Assert.That(arr[0], Is.EqualTo(JsonString.Return("hello")));
			Assert.That(arr[1], Is.EqualTo(JsonNumber.Return(123)));
			Assert.That(arr.Get<string>(0), Is.EqualTo("hello"));
			Assert.That(arr.Get<int>(1), Is.EqualTo(123));
			Assert.That(arr.ToArray(), Is.EqualTo(new[] { JsonString.Return("hello"), JsonNumber.Return(123) }));
			Assert.That(arr.ToJsonCompact(), Is.EqualTo("""["hello",123]"""));

			Assert.That(JsonArray.FromValues([ "A", "B", "C", "D" ]).ToArray<string>(), Is.EqualTo((string[]) [ "A", "B", "C", "D" ]));
			Assert.That(JsonArray.FromValues([ "A", "B", "C", "D" ]).ToList<string>(),Is.EqualTo((string[]) [ "A", "B", "C", "D" ]));
			Assert.That(JsonArray.FromValues([ 1, 2, 3, 4 ]).ToArray<int>(), Is.EqualTo((int[]) [ 1, 2, 3, 4 ]));
			Assert.That(JsonArray.FromValues([ 1, 2, 3, 4 ]).ToList<int>(), Is.EqualTo((int[]) [ 1, 2, 3, 4 ]));
			Assert.That(JsonArray.FromValues([ 1, 2, 3, 4 ]).ToArray<double>(), Is.EqualTo((double[]) [ 1, 2, 3, 4 ]));
			Assert.That(JsonArray.FromValues([ 1, 2, 3, 4 ]).ToList<double>(), Is.EqualTo((double[]) [ 1, 2, 3, 4 ]));
			Assert.That(JsonArray.FromValues([ 1, 2, 3, 4 ]).ToArray<string>(), Is.EqualTo((string[]) [ "1", "2", "3", "4" ]));
			Assert.That(JsonArray.FromValues([ 1, 2, 3, 4 ]).ToList<string>(), Is.EqualTo((string[]) [ "1", "2", "3", "4" ]));
			Assert.That(JsonArray.FromValues([ "1", "2", "3", "4" ]).ToArray<int>(), Is.EqualTo((int[]) [ 1, 2, 3, 4 ]));
			Assert.That(JsonArray.FromValues([ "1", "2", "3", "4" ]).ToList<int>(), Is.EqualTo((int[]) [ 1, 2, 3, 4 ]));

			Assert.That(JsonArray.FromValues((string[]) [ "A", "B", "C", "D" ]), IsJson.EqualTo([ "A", "B", "C", "D" ]));
			Assert.That(JsonArray.FromValues((int[]) [ 1, 2, 3, 4 ]), IsJson.EqualTo((int[]) [ 1, 2, 3, 4 ]));
			Assert.That(JsonArray.FromValues((ReadOnlySpan<string>) [ "A", "B", "C", "D" ]), IsJson.EqualTo([ "A", "B", "C", "D" ]));
			Assert.That(JsonArray.FromValues((ReadOnlySpan<int>) [ 1, 2, 3, 4 ]), IsJson.EqualTo((int[]) [ 1, 2, 3, 4 ]));
			Assert.That(JsonArray.ReadOnly.FromValues((string[]) [ "A", "B", "C", "D" ]), IsJson.ReadOnly.And.EqualTo([ "A", "B", "C", "D" ]));
			Assert.That(JsonArray.ReadOnly.FromValues((int[]) [ 1, 2, 3, 4 ]), IsJson.ReadOnly.And.EqualTo((int[]) [ 1, 2, 3, 4 ]));
			Assert.That(JsonArray.ReadOnly.FromValues((ReadOnlySpan<string>) [ "A", "B", "C", "D" ]), IsJson.ReadOnly.And.EqualTo([ "A", "B", "C", "D" ]));
			Assert.That(JsonArray.ReadOnly.FromValues((ReadOnlySpan<int>) [ 1, 2, 3, 4 ]), IsJson.ReadOnly.And.EqualTo((int[]) [ 1, 2, 3, 4 ]));

			Assert.That(JsonArray.FromValues((char[]) [ 'a', 'b', 'c', 'd' ], s => new string(char.ToUpperInvariant(s), 3)), IsJson.EqualTo([ "AAA", "BBB", "CCC", "DDD" ]));
			Assert.That(JsonArray.FromValues((int[]) [ 1, 2, 3, 4 ], x => new string((char) (64 + x), x)), IsJson.EqualTo([ "A", "BB", "CCC", "DDDD" ]));
			Assert.That(JsonArray.FromValues((ReadOnlySpan<char>) [ 'a', 'b', 'c', 'd' ], s => new string(char.ToUpperInvariant(s), 3)), IsJson.EqualTo([ "AAA", "BBB", "CCC", "DDD" ]));
			Assert.That(JsonArray.FromValues((ReadOnlySpan<int>) [ 1, 2, 3, 4 ], x => new string((char) (64 + x), x)), IsJson.EqualTo([ "A", "BB", "CCC", "DDDD" ]));
			Assert.That(JsonArray.FromValues((IEnumerable<char>) [ 'a', 'b', 'c', 'd' ], s => new string(char.ToUpperInvariant(s), 3)), IsJson.EqualTo([ "AAA", "BBB", "CCC", "DDD" ]));
			Assert.That(JsonArray.FromValues((IEnumerable<char>) ((List<char>) [ 'a', 'b', 'c', 'd' ]), s => new string(char.ToUpperInvariant(s), 3)), IsJson.EqualTo([ "AAA", "BBB", "CCC", "DDD" ]));
			Assert.That(JsonArray.FromValues(((char[]) [ 'a', 'b', 'c', 'd' ]).Select(x => x), s => new string(char.ToUpperInvariant(s), 3)), IsJson.EqualTo([ "AAA", "BBB", "CCC", "DDD" ]));
			Assert.That(JsonArray.ReadOnly.FromValues((char[]) [ 'a', 'b', 'c', 'd' ], s => new string(char.ToUpperInvariant(s), 3)), IsJson.ReadOnly.And.EqualTo([ "AAA", "BBB", "CCC", "DDD" ]));
			Assert.That(JsonArray.ReadOnly.FromValues((int[]) [ 1, 2, 3, 4 ], x => new string((char) (64 + x), x)), IsJson.ReadOnly.And.EqualTo([ "A", "BB", "CCC", "DDDD" ]));
			Assert.That(JsonArray.ReadOnly.FromValues((ReadOnlySpan<char>) [ 'a', 'b', 'c', 'd' ], s => new string(char.ToUpperInvariant(s), 3)), IsJson.ReadOnly.And.EqualTo([ "AAA", "BBB", "CCC", "DDD" ]));
			Assert.That(JsonArray.ReadOnly.FromValues((ReadOnlySpan<int>) [ 1, 2, 3, 4 ], x => new string((char) (64 + x), x)), IsJson.ReadOnly.And.EqualTo([ "A", "BB", "CCC", "DDDD" ]));
			Assert.That(JsonArray.ReadOnly.FromValues((IEnumerable<int>) [ 1, 2, 3, 4 ], x => new string((char) (64 + x), x)), IsJson.ReadOnly.And.EqualTo([ "A", "BB", "CCC", "DDDD" ]));
			Assert.That(JsonArray.ReadOnly.FromValues((IEnumerable<int>) ((List<int>) [ 1, 2, 3, 4 ]), x => new string((char) (64 + x), x)), IsJson.ReadOnly.And.EqualTo([ "A", "BB", "CCC", "DDDD" ]));
			Assert.That(JsonArray.ReadOnly.FromValues(((int[]) [ 1, 2, 3, 4 ]).Select(x => x), x => new string((char) (64 + x), x)), IsJson.ReadOnly.And.EqualTo([ "A", "BB", "CCC", "DDDD" ]));
		}

		[Test]
		public void Test_JsonArray_Create()
		{
			{ // []
				var value = JsonArray.Create();
				Assert.That(value.Type, Is.EqualTo(JsonType.Array));
				Assert.That(value.IsNull, Is.False);
				Assert.That(value.IsDefault, Is.False);
				Assert.That(value.IsReadOnly, Is.False);
				Assert.That(value, Has.Count.EqualTo(0));
				Assert.That(value.ToObject(), Is.EqualTo(Array.Empty<object>()));
				Assert.That(SerializeToSlice(value), Is.EqualTo(Slice.FromString("[]")));
			}

			{ // [ "Foo" ]
				var value = JsonArray.Create("Foo");
				Assert.That(value.Type, Is.EqualTo(JsonType.Array));
				Assert.That(value.IsNull, Is.False);
				Assert.That(value.IsDefault, Is.False);
				Assert.That(value.IsReadOnly, Is.False);
				Assert.That(value, Has.Count.EqualTo(1));
				Assert.That(value[0], Is.EqualTo(JsonString.Return("Foo")));
				Assert.That(value.ToObject(), Is.EqualTo(new[] { "Foo" }));
				Assert.That(value.ToObject(), Is.InstanceOf<List<object>>());
				Assert.That(SerializeToSlice(value), Is.EqualTo(Slice.FromString("""["Foo"]""")));
			}

			{ // [ "Foo", 123 ]
				var value = JsonArray.Create("Foo", 123);
				Assert.That(value.Type, Is.EqualTo(JsonType.Array));
				Assert.That(value.IsNull, Is.False);
				Assert.That(value.IsDefault, Is.False);
				Assert.That(value.IsReadOnly, Is.False);
				Assert.That(value, Has.Count.EqualTo(2));
				Assert.That(value[0], Is.EqualTo(JsonString.Return("Foo")));
				Assert.That(value[1], Is.EqualTo(JsonNumber.Return(123)));
				Assert.That(value.ToObject(), Is.EqualTo(new object[] { "Foo", 123 }));
				Assert.That(SerializeToSlice(value), Is.EqualTo(Slice.FromString("""["Foo",123]""")));
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
				Assert.That(value.IsReadOnly, Is.False);
				Assert.That(value, Has.Count.EqualTo(3));
				Assert.That(value[0], Is.EqualTo(JsonString.Return("Foo")));
				Assert.That(value[1], Is.EqualTo(JsonArray.Create(1, 2, 3)));
				Assert.That(value[2], Is.EqualTo(JsonBoolean.True));
				Assert.That(value.ToObject(), Is.EqualTo(new object[] { "Foo", new[] { 1, 2, 3 }, true }));
				Assert.That(value.ToObject(), Is.InstanceOf<List<object>>());
				Assert.That(SerializeToSlice(value), Is.EqualTo(Slice.FromString("""["Foo",[1,2,3],true]""")));
			}

			{ // [ "one", 2, "three", 4 ]
				var value = JsonArray.Create("one", 2, "three", 4);
				Assert.That(value.Type, Is.EqualTo(JsonType.Array));
				Assert.That(value.IsNull, Is.False);
				Assert.That(value.IsDefault, Is.False);
				Assert.That(value, Has.Count.EqualTo(4));
				Assert.That(SerializeToSlice(value), Is.EqualTo(Slice.FromString("""["one",2,"three",4]""")));
			}

			{ // null entries are converted to JsonNull.Null
				var arr = JsonArray.Create([ null, JsonNull.Null, JsonNull.Missing ]);
				Assert.That(arr[0], Is.Not.Null.And.SameAs(JsonNull.Null));
				Assert.That(arr[1], Is.Not.Null.And.SameAs(JsonNull.Null));
				Assert.That(arr[2], Is.Not.Null.And.SameAs(JsonNull.Missing));
				Assert.That(arr[3], Is.Not.Null.And.SameAs(JsonNull.Error));

				Assert.That(arr[^3], Is.Not.Null.And.SameAs(JsonNull.Null));
				Assert.That(arr[^2], Is.Not.Null.And.SameAs(JsonNull.Null));
				Assert.That(arr[^1], Is.Not.Null.And.SameAs(JsonNull.Missing));
				Assert.That(arr[^4], Is.Not.Null.And.SameAs(JsonNull.Error));
			}

			{ // mutating original array should NOT change the JsonArray created (and vice versa)
				var tmp = new JsonValue[] { "one", 2, "three" };
				var arr = JsonArray.Create(tmp);
				Assert.That(tmp[1], IsJson.Number.And.EqualTo(2));
				Assert.That(arr[1], IsJson.Number.And.EqualTo(2));
				tmp[1] = "two";
				Assert.That(tmp[1], IsJson.String.And.EqualTo("two"));
				Assert.That(arr[1], IsJson.Number.And.EqualTo(2));

				arr[2] = 3;
				Assert.That(tmp[2], IsJson.String.And.EqualTo("three"));
				Assert.That(arr[2], IsJson.Number.And.EqualTo(3));
			}

		}

		[Test]
		public void Test_JsonArray_Create_Compiler_Trivia()
		{
			Assert.That(JsonArray.Create([ "one" ]), IsJson.Array.And.Mutable.EqualTo(new JsonArray() { JsonString.Return("one") }));
			Assert.That(JsonArray.Create([ "one", "two" ]), IsJson.Array.And.Mutable.EqualTo(new JsonArray() { JsonString.Return("one"), JsonString.Return("two") }));
			Assert.That(JsonArray.Create([ "one", null, 123 ]), IsJson.Array.And.Mutable.EqualTo(new JsonArray() { JsonString.Return("one"), JsonNull.Null, JsonNumber.Return(123) }));
			Assert.That(JsonArray.Create(new JsonValue?[] { "before", "one", null, 123, "after" }.AsSpan(1, 3)), IsJson.Array.And.Mutable.EqualTo(new JsonArray() { JsonString.Return("one"), JsonNull.Null, JsonNumber.Return(123) }));

			Assert.That(JsonArray.Create("one"), IsJson.Array.And.Mutable.EqualTo(new JsonArray() { JsonString.Return("one") }));
			Assert.That(JsonArray.Create("one", "two"), IsJson.Array.And.Mutable.EqualTo(new JsonArray() { JsonString.Return("one"), JsonString.Return("two") }));
			Assert.That(JsonArray.Create("one", null, 123), IsJson.Array.And.Mutable.EqualTo(new JsonArray() { JsonString.Return("one"), JsonNull.Null, JsonNumber.Return(123) }));
			Assert.That(JsonArray.Create(1, 2, 3, 4, 5), IsJson.Array.And.Mutable.EqualTo(new JsonArray() { JsonNumber.Return(1), JsonNumber.Return(2), JsonNumber.Return(3), JsonNumber.Return(4), JsonNumber.Return(5) }));

			Assert.That(JsonArray.Create([ "one", JsonArray.Create([ "two", "three" ]) ]), IsJson.Array.And.Mutable.EqualTo(new JsonArray() { JsonString.Return("one"), new JsonArray() { JsonString.Return("two"), JsonString.Return("three") } }));
			Assert.That(JsonArray.Create("one", JsonArray.Create("two", "three")), IsJson.Array.And.Mutable.EqualTo(new JsonArray() { JsonString.Return("one"), new JsonArray() { JsonString.Return("two"), JsonString.Return("three") } }));

			Assert.That(JsonArray.ReadOnly.Create([ "one" ]), IsJson.Array.And.ReadOnly.EqualTo(new JsonArray() { JsonString.Return("one") }));
			Assert.That(JsonArray.ReadOnly.Create([ "one", "two" ]), IsJson.Array.And.ReadOnly.EqualTo(new JsonArray() { JsonString.Return("one"), JsonString.Return("two") }));
			Assert.That(JsonArray.ReadOnly.Create([ "one", null, 123 ]), IsJson.Array.And.ReadOnly.EqualTo(new JsonArray() { JsonString.Return("one"), JsonNull.Null, JsonNumber.Return(123) }));

			Assert.That(JsonArray.ReadOnly.Create("one"), IsJson.Array.And.ReadOnly.EqualTo(new JsonArray() { JsonString.Return("one") }));
			Assert.That(JsonArray.ReadOnly.Create("one", "two"), IsJson.Array.And.ReadOnly.EqualTo(new JsonArray() { JsonString.Return("one"), JsonString.Return("two") }));
			Assert.That(JsonArray.ReadOnly.Create("one", null, 123), IsJson.Array.And.ReadOnly.EqualTo(new JsonArray() { JsonString.Return("one"), JsonNull.Null, JsonNumber.Return(123) }));
			Assert.That(JsonArray.ReadOnly.Create(1, 2, 3, 4, 5), IsJson.Array.And.ReadOnly.EqualTo(new JsonArray() { JsonNumber.Return(1), JsonNumber.Return(2), JsonNumber.Return(3), JsonNumber.Return(4), JsonNumber.Return(5) }));

			Assert.That(JsonArray.ReadOnly.Create([ "one", JsonArray.ReadOnly.Create([ "two", "three" ]) ]), IsJson.Array.And.ReadOnly.EqualTo(new JsonArray() { JsonString.Return("one"), new JsonArray() { JsonString.Return("two"), JsonString.Return("three") } }));
			Assert.That(JsonArray.ReadOnly.Create("one", JsonArray.ReadOnly.Create("two", "three")), IsJson.Array.And.ReadOnly.EqualTo(new JsonArray() { JsonString.Return("one"), new JsonArray() { JsonString.Return("two"), JsonString.Return("three") } }));

			// check that Span<...> will use the Create(ReadOnlySpan<..>) overload
			Span<JsonValue?> buf = [ JsonNumber.Return(1), JsonNumber.Return(2), JsonNumber.Return(3) ];
			Assert.That(JsonArray.Create(buf), IsJson.Array.And.Mutable.EqualTo(new JsonArray() { JsonNumber.Return(1), JsonNumber.Return(2), JsonNumber.Return(3) }));

#if NET9_0_OR_GREATER

			// check that there is no ambiguous call with other types

			Assert.That(JsonArray.Create(Enumerable.Range(1, 3).Select(i => JsonNumber.Return(i))), IsJson.Array.And.Mutable.EqualTo(new JsonArray() { JsonNumber.Return(1), JsonNumber.Return(2), JsonNumber.Return(3) }));
			Assert.That(JsonArray.ReadOnly.Create(Enumerable.Range(1, 3).Select(i => JsonNumber.Return(i))), IsJson.Array.And.ReadOnly.EqualTo(new JsonArray() { JsonNumber.Return(1), JsonNumber.Return(2), JsonNumber.Return(3) }));

#endif
		}

		[Test]
		public void Test_JsonArray_Indexing()
		{
			var arr = JsonArray.Create([ "one", "two", "three" ]);
			Log(arr);

			// this[int]
			Assert.That(arr[0], IsJson.EqualTo("one"));
			Assert.That(arr[1], IsJson.EqualTo("two"));
			Assert.That(arr[2], IsJson.EqualTo("three"));
			Assert.That(arr[4], IsJson.Error);
			Assert.That(arr[-1], IsJson.Error);
			// this[Index]
			Assert.That(arr[^3], IsJson.EqualTo("one"));
			Assert.That(arr[^2], IsJson.EqualTo("two"));
			Assert.That(arr[^1], IsJson.EqualTo("three"));
			// ReSharper disable once ZeroIndexFromEnd
			Assert.That(arr[^0], IsJson.Error);
			Assert.That(arr[^4], IsJson.Error);

			// GetValue(int)
			Assert.That(arr.GetValue(0), IsJson.EqualTo("one"));
			Assert.That(arr.GetValue(1), IsJson.EqualTo("two"));
			Assert.That(arr.GetValue(2), IsJson.EqualTo("three"));
			Assert.That(() => arr.GetValue(3), Throws.InstanceOf<IndexOutOfRangeException>());
			Assert.That(() => arr.GetValue(-1), Throws.InstanceOf<IndexOutOfRangeException>());
			// GetValue(Index)
			Assert.That(arr.GetValue(^3), IsJson.EqualTo("one"));
			Assert.That(arr.GetValue(^2), IsJson.EqualTo("two"));
			Assert.That(arr.GetValue(^1), IsJson.EqualTo("three"));
			// ReSharper disable once ZeroIndexFromEnd
			Assert.That(() => arr.GetValue(^0), Throws.InstanceOf<IndexOutOfRangeException>());
			Assert.That(() => arr.GetValue(^4), Throws.InstanceOf<IndexOutOfRangeException>());

			// TryGetValue(int)
			Assert.That(arr.TryGetValue(0, out var res), Is.True.WithOutput(res).EqualTo("one"));
			Assert.That(arr.TryGetValue(1, out res), Is.True.WithOutput(res).EqualTo("two"));
			Assert.That(arr.TryGetValue(2, out res), Is.True.WithOutput(res).EqualTo("three"));
			Assert.That(arr.TryGetValue(-1, out res), Is.False.WithOutput(res).Default);
			Assert.That(arr.TryGetValue(4, out res), Is.False.WithOutput(res).Default);
			// TryGetValue(Index)
			Assert.That(arr.TryGetValue(^3, out res), Is.True.WithOutput(res).EqualTo("one"));
			Assert.That(arr.TryGetValue(^2, out res), Is.True.WithOutput(res).EqualTo("two"));
			Assert.That(arr.TryGetValue(^1, out res), Is.True.WithOutput(res).EqualTo("three"));
			Assert.That(arr.TryGetValue(^0, out res), Is.False.WithOutput(res).Default);
			Assert.That(arr.TryGetValue(^4, out res), Is.False.WithOutput(res).Default);

			// GetPath("[int]")
			Assert.That(arr.GetPathValue("[0]"), IsJson.EqualTo("one"));
			Assert.That(arr.GetPathValue("[1]"), IsJson.EqualTo("two"));
			Assert.That(arr.GetPathValue("[2]"), IsJson.EqualTo("three"));
			Assert.That(() => arr.GetPathValue("[3]"), Throws.InstanceOf<JsonBindingException>());
			Assert.That(arr.GetPathValueOrDefault("[0]"), IsJson.EqualTo("one"));
			Assert.That(arr.GetPathValueOrDefault("[1]"), IsJson.EqualTo("two"));
			Assert.That(arr.GetPathValueOrDefault("[2]"), IsJson.EqualTo("three"));
			Assert.That(arr.GetPathValueOrDefault("[3]"), IsJson.EqualTo(JsonNull.Error));
			// GetPath("[Index]")
			Assert.That(arr.GetPathValue("[^3]"), IsJson.EqualTo("one"));
			Assert.That(arr.GetPathValue("[^2]"), IsJson.EqualTo("two"));
			Assert.That(arr.GetPathValue("[^1]"), IsJson.EqualTo("three"));
			Assert.That(() => arr.GetPathValue("[^0]"), Throws.InstanceOf<JsonBindingException>());
			Assert.That(() => arr.GetPathValue("[^4]"), Throws.InstanceOf<JsonBindingException>());
			Assert.That(arr.GetPathValueOrDefault("[^3]"), IsJson.EqualTo("one"));
			Assert.That(arr.GetPathValueOrDefault("[^2]"), IsJson.EqualTo("two"));
			Assert.That(arr.GetPathValueOrDefault("[^1]"), IsJson.EqualTo("three"));
			Assert.That(arr.GetPathValueOrDefault("[^0]"), IsJson.EqualTo(JsonNull.Error));
			Assert.That(arr.GetPathValueOrDefault("[^4]"), IsJson.EqualTo(JsonNull.Error));

			Assert.That(arr.GetPath<string>("[0]"), Is.EqualTo("one"));
			Assert.That(arr.GetPath<string>("[1]"), Is.EqualTo("two"));
			Assert.That(arr.GetPath<string>("[2]"), Is.EqualTo("three"));
			Assert.That(() => arr.GetPath<string>("[3]"), Throws.InstanceOf<JsonBindingException>());
			Assert.That(arr.GetPath<string>("[^3]"), Is.EqualTo("one"));
			Assert.That(arr.GetPath<string>("[^2]"), Is.EqualTo("two"));
			Assert.That(arr.GetPath<string>("[^1]"), Is.EqualTo("three"));
			Assert.That(() => arr.GetPath<string>("[^4]"), Throws.InstanceOf<JsonBindingException>());
			Assert.That(() => arr.GetPath<string>("[0].foo"), Throws.InstanceOf<JsonBindingException>());
			Assert.That(() => arr.GetPath<string>("foo"), Throws.InstanceOf<JsonBindingException>());

			Assert.That(arr.GetPath<string>("[0]", "not_found"), Is.EqualTo("one"));
			Assert.That(arr.GetPath<string>("[1]", "not_found"), Is.EqualTo("two"));
			Assert.That(arr.GetPath<string>("[2]", "not_found"), Is.EqualTo("three"));
			Assert.That(arr.GetPath<string>("[3]", "not_found"), Is.EqualTo("not_found"));
			Assert.That(arr.GetPath<string>("[^3]", "not_found"), Is.EqualTo("one"));
			Assert.That(arr.GetPath<string>("[^2]", "not_found"), Is.EqualTo("two"));
			Assert.That(arr.GetPath<string>("[^1]", "not_found"), Is.EqualTo("three"));
			Assert.That(arr.GetPath<string>("[^4]", "not_found"), Is.EqualTo("not_found"));
			Assert.That(arr.GetPath<string>("[0].foo", "not_found"), Is.EqualTo("not_found"));
			Assert.That(arr.GetPath<string>("foo", "not_found"), Is.EqualTo("not_found"));
		}

		[Test]
		public void Test_JsonArray_ToJsonArray()
		{
			// JsonValue[]
			var array = (new[] { JsonNumber.One, JsonBoolean.True, JsonString.Empty }).ToJsonArray();
			Assert.That(array, Is.Not.Null);
			Assert.That(array, Has.Count.EqualTo(3));
			Assert.That(array[0], Is.SameAs(JsonNumber.One));
			Assert.That(array[1], Is.SameAs(JsonBoolean.True));
			Assert.That(array[2], Is.SameAs(JsonString.Empty));

			// Span<JsonValue>
			array = (new[] { JsonNumber.One, JsonBoolean.True, JsonString.Empty }.AsSpan()).ToJsonArray();
			Assert.That(array, Is.Not.Null);
			Assert.That(array, Has.Count.EqualTo(3));
			Assert.That(array[0], Is.SameAs(JsonNumber.One));
			Assert.That(array[1], Is.SameAs(JsonBoolean.True));
			Assert.That(array[2], Is.SameAs(JsonString.Empty));

			// ICollection<JsonValue>
			array = Enumerable.Range(0, 10).Select(x => JsonNumber.Return(x)).ToList().ToJsonArray();
			Assert.That(array, Is.Not.Null);
			Assert.That(array, Has.Count.EqualTo(10));
			Assert.That(array.ToArray<int>(), Is.EqualTo(Enumerable.Range(0, 10).ToArray()));

			// IEnumerable<JsonValue>
			array = Enumerable.Range(0, 10).Select(x => JsonNumber.Return(x)).ToJsonArray();
			Assert.That(array, Is.Not.Null);
			Assert.That(array, Has.Count.EqualTo(10));
			Assert.That(array.ToArray<int>(), Is.EqualTo(Enumerable.Range(0, 10).ToArray()));

			// another JsonArray
			array = array.ToJsonArray();
			Assert.That(array, Is.Not.Null);
			Assert.That(array, Has.Count.EqualTo(10));
			Assert.That(array.ToArray<int>(), Is.EqualTo(Enumerable.Range(0, 10).ToArray()));

			// int[]
			array = new int[] { 1, 2, 3 }.ToJsonArray();
			Assert.That(array, Is.Not.Null);
			Assert.That(array, Has.Count.EqualTo(3));
			Assert.That(array.ToArray<int>(), Is.EqualTo(new[] {1, 2, 3}));

			// ICollection<int>
			array = new List<int>([ 1, 2, 3 ]).ToJsonArray();
			Assert.That(array, Is.Not.Null);
			Assert.That(array, Has.Count.EqualTo(3));
			Assert.That(array.ToArray<int>(), Is.EqualTo(new[] { 1, 2, 3 }));

			// IEnumerable<int>
			array = Enumerable.Range(1, 3).ToJsonArray();
			Assert.That(array, Is.Not.Null);
			Assert.That(array, Has.Count.EqualTo(3));
			Assert.That(array.ToArray<int>(), Is.EqualTo(new[] { 1, 2, 3 }));
		}

		[Test]
		public void Test_JsonArray_ToJsonArrayReadOnly()
		{
			// JsonValue[]
			var array = (new[] {JsonNumber.One, JsonBoolean.True, JsonString.Empty}).ToJsonArrayReadOnly();
			Assert.That(array, Is.Not.Null);
			Assert.That(array, Has.Count.EqualTo(3));
			Assert.That(array[0], Is.SameAs(JsonNumber.One));
			Assert.That(array[1], Is.SameAs(JsonBoolean.True));
			Assert.That(array[2], Is.SameAs(JsonString.Empty));
			EnsureDeepImmutabilityInvariant(array);

			// Span<JsonValue>
			array = (new[] { JsonNumber.One, JsonBoolean.True, JsonString.Empty }.AsSpan()).ToJsonArrayReadOnly();
			Assert.That(array, Is.Not.Null);
			Assert.That(array, Has.Count.EqualTo(3));
			Assert.That(array[0], Is.SameAs(JsonNumber.One));
			Assert.That(array[1], Is.SameAs(JsonBoolean.True));
			Assert.That(array[2], Is.SameAs(JsonString.Empty));
			EnsureDeepImmutabilityInvariant(array);

			// ICollection<JsonValue>
			array = Enumerable.Range(0, 10).Select(x => JsonNumber.Return(x)).ToList().ToJsonArrayReadOnly();
			Assert.That(array, Is.Not.Null);
			Assert.That(array, Has.Count.EqualTo(10));
			Assert.That(array.ToArray<int>(), Is.EqualTo(Enumerable.Range(0, 10).ToArray()));
			EnsureDeepImmutabilityInvariant(array);

			// IEnumerable<JsonValue>
			array = Enumerable.Range(0, 10).Select(x => JsonNumber.Return(x)).ToJsonArrayReadOnly();
			Assert.That(array, Is.Not.Null);
			Assert.That(array, Has.Count.EqualTo(10));
			Assert.That(array.ToArray<int>(), Is.EqualTo(Enumerable.Range(0, 10).ToArray()));
			EnsureDeepImmutabilityInvariant(array);

			// another JsonArray
			array = array.ToJsonArrayReadOnly();
			Assert.That(array, Is.Not.Null);
			Assert.That(array, Has.Count.EqualTo(10));
			Assert.That(array.ToArray<int>(), Is.EqualTo(Enumerable.Range(0, 10).ToArray()));
			EnsureDeepImmutabilityInvariant(array);

			// int[]
			array = new int[] {1, 2, 3}.ToJsonArrayReadOnly();
			Assert.That(array, Is.Not.Null);
			Assert.That(array, Has.Count.EqualTo(3));
			Assert.That(array.ToArray<int>(), Is.EqualTo(new[] {1, 2, 3}));
			EnsureDeepImmutabilityInvariant(array);

			// ICollection<int>
			array = new List<int>([1, 2, 3]).ToJsonArrayReadOnly();
			Assert.That(array, Is.Not.Null);
			Assert.That(array, Has.Count.EqualTo(3));
			Assert.That(array.ToArray<int>(), Is.EqualTo(new[] {1, 2, 3}));
			EnsureDeepImmutabilityInvariant(array);

			// IEnumerable<int>
			array = Enumerable.Range(1, 3).ToJsonArrayReadOnly();
			Assert.That(array, Is.Not.Null);
			Assert.That(array, Has.Count.EqualTo(3));
			Assert.That(array.ToArray<int>(), Is.EqualTo(new[] {1, 2, 3}));
			EnsureDeepImmutabilityInvariant(array);
		}

		[Test]
		public void Test_JsonArray_Truncate()
		{
			Assert.That(new JsonArray().Truncate(0), IsJson.Empty);
			Assert.That(new JsonArray().Truncate(3), IsJson.EqualTo(JsonArray.Create([ null, null, null ])));
			Assert.That(JsonArray.Create([ 1, 2, 3 ]).Truncate(3), IsJson.EqualTo(JsonArray.Create([ 1, 2, 3 ])));
			Assert.That(JsonArray.Create([ 1, 2, 3 ]).Truncate(5), IsJson.EqualTo(JsonArray.Create([ 1, 2, 3, null, null ])));
			Assert.That(JsonArray.Create([ 1, 2, 3, 4, 5 ]).Truncate(3), IsJson.EqualTo(JsonArray.Create([ 1, 2, 3 ])));
			Assert.That(JsonArray.Create([ 1, 2, 3 ]).Truncate(0), IsJson.Empty);
		}

		[Test]
		public void Test_JsonArray_Combine()
		{
			Assert.That(JsonArray.Combine(JsonArray.ReadOnly.Empty, JsonArray.ReadOnly.Empty), Is.Not.SameAs(JsonArray.ReadOnly.Empty).And.Empty);
			Assert.That(JsonArray.Combine(JsonArray.Create("hello", "world"), JsonArray.ReadOnly.Empty), IsJson.EqualTo([ "hello", "world" ]));
			Assert.That(JsonArray.Combine(JsonArray.ReadOnly.Empty, JsonArray.Create("hello", "world")), IsJson.EqualTo([ "hello", "world" ]));
			Assert.That(JsonArray.Combine(JsonArray.Create("hello"), JsonArray.Create("world")), IsJson.EqualTo([ "hello", "world" ]));

			Assert.That(JsonArray.Combine(JsonArray.ReadOnly.Empty, JsonArray.ReadOnly.Empty, JsonArray.ReadOnly.Empty), Is.Not.SameAs(JsonArray.ReadOnly.Empty).And.Empty);
			Assert.That(JsonArray.Combine(JsonArray.Create("hello", "world"), JsonArray.ReadOnly.Empty, JsonArray.ReadOnly.Empty), IsJson.EqualTo([ "hello", "world" ]));
			Assert.That(JsonArray.Combine(JsonArray.ReadOnly.Empty, JsonArray.Create("hello", "world"), JsonArray.ReadOnly.Empty), IsJson.EqualTo([ "hello", "world" ]));
			Assert.That(JsonArray.Combine(JsonArray.ReadOnly.Empty, JsonArray.ReadOnly.Empty, JsonArray.Create("hello", "world")), IsJson.EqualTo([ "hello", "world" ]));
			Assert.That(JsonArray.Combine(JsonArray.Create("hello"), JsonArray.Create("world"), JsonArray.Create("!")), IsJson.EqualTo([ "hello", "world", "!" ]));
			Assert.That(JsonArray.Combine(JsonArray.Create(1), JsonArray.Create(2, 3), JsonArray.Create(4, 5, 6)), IsJson.EqualTo([ 1, 2, 3, 4, 5, 6 ]));
		}

		[Test]
		public void Test_JsonArray_AddRange_Of_JsonValues()
		{
			var array = new JsonArray();

			// add elements
			array.AddRange(JsonArray.Create(1, 2));
			Assert.That(array, Has.Count.EqualTo(2));
			Assert.That(array.ToArray<int>(), Is.EqualTo((int[]) [ 1, 2 ]));

			// add singleton
			array.AddRange(JsonArray.Create(3));
			Assert.That(array, Has.Count.EqualTo(3));
			Assert.That(array.ToArray<int>(), Is.EqualTo((int[]) [ 1, 2, 3 ]));

			// add empty
			array.AddRange(JsonArray.Create());
			Assert.That(array, Has.Count.EqualTo(3));
			Assert.That(array.ToArray<int>(), Is.EqualTo((int[]) [ 1, 2, 3 ]));

			// add empty (collection expression)
			array.AddRange([ ]);
			Assert.That(array, Has.Count.EqualTo(3));
			Assert.That(array.ToArray<int>(), Is.EqualTo((int[]) [ 1, 2, 3 ]));

			// array inception!
			array.AddRange(array);
			Assert.That(array, Has.Count.EqualTo(6));
			Assert.That(array.ToArray<int>(), Is.EqualTo((int[]) [ 1, 2, 3, 1, 2, 3 ]));

			// capacity
			array = new JsonArray(5);
			Assert.That(array.Capacity, Is.EqualTo(5));

			array.AddRange([ 1, 2, 3 ]);
			Assert.That(array, Has.Count.EqualTo(3), "array.Count");
			Assert.That(array.Capacity, Is.EqualTo(5), "array.Capacity was enough");

			array.AddRange([ 4, 5 ]);
			Assert.That(array, Has.Count.EqualTo(5), "array.Count");
			Assert.That(array.Capacity, Is.EqualTo(5), "array.Capacity was still enough");

			array.AddRange([ 6 ]);
			Assert.That(array, Has.Count.EqualTo(6), "array.Count");
			Assert.That(array.Capacity, Is.EqualTo(10), "array.Capacity should have double");

			// errors
			Assert.That(() => array.AddRange(default(JsonValue[])!), Throws.InstanceOf<ArgumentNullException>());
			Assert.That(() => array.AddRange(default(IEnumerable<JsonValue>)!), Throws.InstanceOf<ArgumentNullException>());
		}

		[Test]
		public void Test_JsonArray_AddRange_Of_T()
		{
			var array = new JsonArray();

			// add elements
			array.AddValues<int>(new [] { 1, 2 });
			Assert.That(array, Has.Count.EqualTo(2));
			Assert.That(array.ToArray<int>(), Is.EqualTo(new[] { 1, 2 }));

			// add singleton
			array.AddValues<int>(new [] { 3 });
			Assert.That(array, Has.Count.EqualTo(3));
			Assert.That(array.ToArray<int>(), Is.EqualTo(new[] { 1, 2, 3 }));

			// add empty
			array.AddValues<int>(new int[0]);
			Assert.That(array, Has.Count.EqualTo(3));
			Assert.That(array.ToArray<int>(), Is.EqualTo(new[] { 1, 2, 3 }));

			// array inception!
			array.AddValues<int>(array.ToArray<int>());
			Assert.That(array, Has.Count.EqualTo(6));
			Assert.That(array.ToArray<int>(), Is.EqualTo(new[] { 1, 2, 3, 1, 2, 3 }));

			// capacity
			array = new JsonArray(5);
			Assert.That(array.Capacity, Is.EqualTo(5));
			array.AddValues<int>(new[] { 1, 2, 3 });
			Assert.That(array, Has.Count.EqualTo(3), "array.Count");
			Assert.That(array.Capacity, Is.EqualTo(5), "array.Capacity was enough");
			array.AddValues<int>(new[] { 4, 5 });
			Assert.That(array, Has.Count.EqualTo(5), "array.Count");
			Assert.That(array.Capacity, Is.EqualTo(5), "array.Capacity was still enough");
			array.AddValues<int>(new[] { 6 });
			Assert.That(array, Has.Count.EqualTo(6), "array.Count");
			Assert.That(array.Capacity, Is.EqualTo(10), "array.Capacity should have double");

			// with regular objects
			array = new JsonArray();
			array.AddValues(Enumerable.Range(1, 3).Select(x => new { Id = x, Name = x.ToString() }));
			Assert.That(array, Has.Count.EqualTo(3));
			for (int i = 0; i < array.Count; i++)
			{
				Assert.That(array[i], Is.Not.Null.And.InstanceOf<JsonObject>(), $"[{i}]");
				Assert.That(((JsonObject) array[i])["Id"], Is.EqualTo(JsonNumber.Return(i + 1)), $"[{i}].Id");
				Assert.That(((JsonObject) array[i])["Name"], Is.EqualTo(JsonString.Return((i + 1).ToString())), $"[{i}].Name");
				Assert.That(((JsonObject) array[i]), Has.Count.EqualTo(2), $"[{i}] Count");
			}

			// errors
			Assert.That(() => array.AddValues<int>(default(int[])!), Throws.InstanceOf<ArgumentNullException>());
			Assert.That(() => array.AddValues<int>(default(IEnumerable<int>)!), Throws.InstanceOf<ArgumentNullException>());
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
					Log($"Added {arr.Count}th triggered a realloc to {arr.Capacity}");
					old = arr.Capacity;
					++resizes;
				}
#if FULL_DEBUG
				Log(" - {0}: {1:N1} % filled, {2:N0} bytes wasted", arr.Count, 100.0 * arr.Count / arr.Capacity, (arr.Capacity - arr.Count) * IntPtr.Size);
#endif

			}

			Log($"Array needed {resizes} to insert {arr.Count} items");
		}

		[Test]
		public void Test_JsonArray_Enumerable_Of_T()
		{
			{ // As<double>()
				var arr = new JsonArray(4) // with a larger capacity than the Count, to check that the iterator does not overflow past 3 elements!
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
				var arr = new JsonArray(4) // with a larger capacity than the Count, to check that the iterator does not overflow past 3 elements!
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

			{ // all elements are objects
				var arr = new JsonArray(4) // with a larger capacity than the Count, to check that the iterator does not overflow past 3 elements!
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

				Assert.That(cast.ToArray(), Is.EqualTo(new JsonObject[] { a, b, c }));
				Assert.That(cast.ToList(), Is.EqualTo(new List<JsonObject> { a, b, c }));
			}

			{ // the second element is null
				var arr = JsonArray.Create(a, JsonNull.Null, c);

				var cast = arr.AsObjectsOrDefault();
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
				Assert.That(cast.ToArray(), Is.EqualTo(new JsonObject?[] { a, null, c }));
				Assert.That(cast.ToList(), Is.EqualTo(new List<JsonObject?> { a, null, c }));
			}

			{ // the second element is null
				var arr = JsonArray.Create(a, JsonNull.Null, c);

				var cast = arr.AsObjectsOrEmpty();
				using (var it = cast.GetEnumerator())
				{
					Assert.That(it.Current, Is.Null, "Before first MoveNext");
					Assert.That(it.MoveNext(), Is.True, "#1");
					Assert.That(it.Current, Is.SameAs(a), "#1");
					Assert.That(it.MoveNext(), Is.True, "#2");
					Assert.That(it.Current, Is.SameAs(JsonObject.ReadOnly.Empty), "#2 should be empty singleton!");
					Assert.That(it.MoveNext(), Is.True, "#3");
					Assert.That(it.Current, Is.SameAs(c), "#3");
					Assert.That(it.MoveNext(), Is.False, "Capacity = 4, mais Count = 3 !");
					Assert.That(it.Current, Is.Null, "After last MoveNext");
				}
				Assert.That(cast.ToArray(), Is.EqualTo(new JsonObject?[] { a, JsonObject.ReadOnly.Empty, c }));
				Assert.That(cast.ToList(), Is.EqualTo(new List<JsonObject?> { a, JsonObject.ReadOnly.Empty, c }));
			}

			{ // the second elements is null, but they are all required
				var arr = JsonArray.Create(a, null, c);

				var cast = arr.AsObjects();
				using (var it = cast.GetEnumerator())
				{
					Assert.That(it.Current, Is.Null, "Before first MoveNext");
					Assert.That(it.MoveNext(), Is.True, "#1");
					Assert.That(it.Current, Is.SameAs(a), "#1");
					Assert.That(() => it.MoveNext(), Throws.InstanceOf<JsonBindingException>(), "#2 should throw because null is not allowed");
				}
				Assert.That(() => cast.ToArray(), Throws.InstanceOf<JsonBindingException>(), "ToArray() should throw because null is not allowed");
				Assert.That(() => cast.ToList(), Throws.InstanceOf<JsonBindingException>(), "ToList() should throw because null is not allowed");
			}

			{ // the second element is not an object (and not null)
				var arr = JsonArray.Create(a, 123, c);

				var cast = arr.AsObjects();
				using (var it = cast.GetEnumerator())
				{
					Assert.That(it.Current, Is.Null, "Before first MoveNext");
					Assert.That(it.MoveNext(), Is.True, "#1");
					Assert.That(it.Current, Is.SameAs(a), "#1");
					Assert.That(() => it.MoveNext(), Throws.InstanceOf<JsonBindingException>(), "#2 should throw because it is not an object");
				}
				Assert.That(() => cast.ToArray(), Throws.InstanceOf<JsonBindingException>(), "ToArray() should throw because it is not an object");
				Assert.That(() => cast.ToList(), Throws.InstanceOf<JsonBindingException>(), "ToList() should throw because it is not an object");
			}

		}

		[Test]
		public void Test_JsonArray_AsArrays()
		{
			var a = JsonArray.Create(1, 0, 0);
			var b = JsonArray.Create(0, 1, 0);
			var c = JsonArray.Create(0, 0, 1);

			{ // all elements are arrays
				var arr = new JsonArray(4) // with a larger capacity than the Count, to check that the iterator does not overflow past 3 elements!
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

			{ // second element is null
				var arr = JsonArray.Create(a, JsonNull.Null, c);

				var cast = arr.AsArraysOrDefault();
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
				Assert.That(cast.ToArray(), Is.EqualTo(new JsonArray?[] { a, null, c }));
				Assert.That(cast.ToList(), Is.EqualTo(new List<JsonArray?> { a, null, c }));
			}

			{ // second element is null
				var arr = JsonArray.Create(a, JsonNull.Null, c);

				var cast = arr.AsArraysOrEmpty();
				using (var it = cast.GetEnumerator())
				{
					Assert.That(it.Current, Is.Null, "Before first MoveNext");
					Assert.That(it.MoveNext(), Is.True, "#1");
					Assert.That(it.Current, Is.SameAs(a), "#1");
					Assert.That(it.MoveNext(), Is.True, "#2");
					Assert.That(it.Current, Is.SameAs(JsonArray.ReadOnly.Empty), "#2 should be empty singleton!");
					Assert.That(it.MoveNext(), Is.True, "#3");
					Assert.That(it.Current, Is.SameAs(c), "#3");
					Assert.That(it.MoveNext(), Is.False, "Capacity = 4, mais Count = 3 !");
					Assert.That(it.Current, Is.Null, "After last MoveNext");
				}
				Assert.That(cast.ToArray(), Is.EqualTo(new JsonArray?[] { a, JsonArray.ReadOnly.Empty, c }));
				Assert.That(cast.ToList(), Is.EqualTo(new List<JsonArray?> { a, JsonArray.ReadOnly.Empty, c }));
			}

			{ // second element is null, and all are required
				var arr = JsonArray.Create(a, null, c);

				var cast = arr.AsArrays();
				using (var it = cast.GetEnumerator())
				{
					Assert.That(it.Current, Is.Null, "Before first MoveNext");
					Assert.That(it.MoveNext(), Is.True, "#1");
					Assert.That(it.Current, Is.SameAs(a), "#1");
					Assert.That(() => it.MoveNext(), Throws.InstanceOf<JsonBindingException>(), "#2 should throw because null is not allowed");
				}
				Assert.That(() => cast.ToArray(), Throws.InstanceOf<JsonBindingException>(), "ToArray() should throw because null is not allowed");
				Assert.That(() => cast.ToList(), Throws.InstanceOf<JsonBindingException>(), "ToList() should throw because null is not allowed");
			}

			{ // second element is not an array, and not null
				var arr = JsonArray.Create(a, 123, c);

				var cast = arr.AsArrays();
				using (var it = cast.GetEnumerator())
				{
					Assert.That(it.Current, Is.Null, "Before first MoveNext");
					Assert.That(it.MoveNext(), Is.True, "#1");
					Assert.That(it.Current, Is.SameAs(a), "#1");
					Assert.That(() => it.MoveNext(), Throws.InstanceOf<JsonBindingException>(), "#2 should throw because it is not an array");
				}
				Assert.That(() => cast.ToArray(), Throws.InstanceOf<JsonBindingException>(), "ToArray() should throw because it is not an array");
				Assert.That(() => cast.ToList(), Throws.InstanceOf<JsonBindingException>(), "ToList() should throw because it is not an array");
			}

		}

		[Test]
		public void Test_JsonArray_Cast()
		{

			Assert.Multiple(() =>
			{
				var cast = JsonArray.Create().Cast<int>();
				Assert.That(cast, Has.Count.EqualTo(0));
				Assert.That(cast.ToArray(), Is.Empty);
				Assert.That(cast.ToList(), Is.Empty);
			});

			Assert.Multiple(() =>
			{
				var arr = JsonArray.Create(123, 456, 789);

				var cast = arr.Cast<int>();

				Assert.That(cast, Has.Count.EqualTo(3));
				Assert.That(cast[0], Is.EqualTo(123));
				Assert.That(cast[1], Is.EqualTo(456));
				Assert.That(cast[2], Is.EqualTo(789));
				Assert.That(cast[^1], Is.EqualTo(789));

				Assert.That(cast.ToArray(), Is.EqualTo((int[]) [ 123, 456, 789 ]));
				Assert.That(cast.ToList(), Is.EqualTo((List<int>) [ 123, 456, 789 ]));

				int p = 0;
				foreach (var x in cast)
				{
					switch (p)
					{
						case 0:
						{
							Assert.That(x, Is.EqualTo(123));
							break;
						}
						case 1:
						{
							Assert.That(x, Is.EqualTo(456));
							break;
						}
						case 2:
						{
							Assert.That(x, Is.EqualTo(789));
							break;
						}
						default:
						{
							Assert.Fail("Should only iterate 3 items");
							break;
						}
					}
					++p;
				}
			});

			Assert.Multiple(() =>
			{ // should fail if no default and value if missing
				var cast = JsonArray.Create(123, null, 789).Cast<int>();
				Assert.That(cast, Has.Count.EqualTo(3));
				Assert.That(cast[0], Is.EqualTo(123));
				Assert.That(() => cast[1], Throws.InstanceOf<JsonBindingException>());
				Assert.That(cast[2], Is.EqualTo(789));
				Assert.That(cast[^1], Is.EqualTo(789));
				Assert.That(() => cast.ToArray(), Throws.InstanceOf<JsonBindingException>());
				Assert.That(() => cast.ToList(), Throws.InstanceOf<JsonBindingException>());
			});

			Assert.Multiple(() =>
			{ // should return default value if missing
				var cast = JsonArray.Create(123, null, 789).Cast<int?>(null);
				Assert.That(cast, Has.Count.EqualTo(3));
				Assert.That(cast[0], Is.EqualTo(123));
				Assert.That(cast[1], Is.Null);
				Assert.That(cast[2], Is.EqualTo(789));
				Assert.That(cast[^1], Is.EqualTo(789));
				Assert.That(cast.ToArray(), Is.EqualTo((int?[]) [ 123, null, 789 ]));
				Assert.That(cast.ToList(), Is.EqualTo((List<int?>) [ 123, null, 789 ]));
			});

			Assert.Multiple(() =>
			{
				var cast = JsonArray.Create(123, null, 789).Cast<int>(-1);
				Assert.That(cast, Has.Count.EqualTo(3));
				Assert.That(cast[0], Is.EqualTo(123));
				Assert.That(cast[1], Is.EqualTo(-1));
				Assert.That(cast[2], Is.EqualTo(789));
				Assert.That(cast[^1], Is.EqualTo(789));
				Assert.That(cast.ToArray(), Is.EqualTo((int[]) [ 123, -1, 789 ]));
				Assert.That(cast.ToList(), Is.EqualTo((List<int>) [ 123, -1, 789 ]));
			});

			Assert.Multiple(() =>
			{
				var cast = JsonArray.Create("hello", "world", "!!!").Cast<string>();
				Assert.That(cast, Has.Count.EqualTo(3));
				Assert.That(cast[0], Is.EqualTo("hello"));
				Assert.That(cast[1], Is.EqualTo("world"));
				Assert.That(cast[2], Is.EqualTo("!!!"));
				Assert.That(cast[^1], Is.EqualTo("!!!"));
				Assert.That(cast.ToArray(), Is.EqualTo((string[]) [ "hello", "world", "!!!" ]));
				Assert.That(cast.ToList(), Is.EqualTo((List<string>) [ "hello", "world", "!!!" ]));
			});

			Assert.Multiple(() =>
			{
				var cast = JsonArray.Create("hello", null, "!!!").Cast<string?>("???");
				Assert.That(cast, Has.Count.EqualTo(3));
				Assert.That(cast[0], Is.EqualTo("hello"));
				Assert.That(cast[1], Is.EqualTo("???"));
				Assert.That(cast[2], Is.EqualTo("!!!"));
				Assert.That(cast[^1], Is.EqualTo("!!!"));
				Assert.That(cast.ToArray(), Is.EqualTo((string[]) [ "hello", "???", "!!!" ]));
				Assert.That(cast.ToList(), Is.EqualTo((List<string>) [ "hello", "???", "!!!" ]));
			});

			Assert.Multiple(() =>
			{ // we can cast to tuples
				var arr = JsonArray.Create([
					JsonArray.Create("one", 111),
					JsonArray.Create("two", 222),
					JsonArray.Create("three", 333)
				]);

				var cast = arr.Cast<(string, int)>();

				Assert.That(cast, Has.Count.EqualTo(3));
				Assert.That(cast[0], Is.EqualTo(("one", 111)));
				Assert.That(cast[1], Is.EqualTo(("two", 222)));
				Assert.That(cast[2], Is.EqualTo(("three", 333)));
				Assert.That(cast[^1], Is.EqualTo(("three", 333)));
				Assert.That(cast.ToArray(), Is.EqualTo(((string, int)[]) [ ("one", 111), ("two", 222), ("three", 333) ]));
				Assert.That(cast.ToList(), Is.EqualTo((List<(string, int)>) [ ("one", 111), ("two", 222), ("three", 333) ]));
			});

			Assert.Multiple(() =>
			{ // we can cast to anonymous types

				var a = new { GivenName = "James", FamilyName = "Bond" };
				var b = new { GivenName = "Hubert", FamilyName = "Bonisseur de La Bath" };
				var c = new { GivenName = "Janov", FamilyName = "Bondovicz" };

				var arr = JsonArray.FromValues([ a, b, c ]);

				var cast = arr.Cast(new { GivenName = "", FamilyName = "" });

				Assert.That(cast, Has.Count.EqualTo(3));
				Assert.That(cast[0], Is.EqualTo(a));
				Assert.That(cast[1], Is.EqualTo(b));
				Assert.That(cast[2], Is.EqualTo(c));
				Assert.That(cast[^1], Is.EqualTo(c));
				Assert.That(cast.ToArray(), Is.EqualTo((object[]) [ a, b, c]));
				Assert.That(cast.ToList(), Is.EqualTo((List<object>) [ a, b,c ]));
			});
		}

		[Test]
		public void Test_JsonArray_Projection()
		{
			var arr = new JsonArray()
			{
				JsonObject.ReadOnly.FromObject(new { Id = 1, Name = "Walter White", Pseudo = "Einsenberg", Job = "Cook", Sickness = "Lung Cancer" }),
				JsonObject.ReadOnly.FromObject(new { Id = 2, Name = "Jesse Pinkman", Job = "Drug Dealer" }),
				JsonObject.ReadOnly.FromObject(new { Id = 3, Name = "Walter White, Jr", Pseudo = "Flynn", Sickness = "Cerebral Palsy" }),
				JsonObject.ReadOnly.FromObject(new { Foo = "bar", Version = 1 }), // completely unrelated object (probably a bug)
				JsonObject.ReadOnly.Empty, // empty object
				JsonNull.Null, // Null should not be changed
				JsonNull.Missing, // Missing should be converted to Null
				null, // null should be changed to Null
			};
			Log("arr = " + arr.ToJsonIndented());

			#region Pick (drop missing)...

			// if the key does not exist in the source, it will not be present in the result either

			var proj = arr.Pick([ "Id", "Name", "Pseudo", "Job", "Version" ]);

			Assert.That(proj, Is.Not.Null.And.Not.SameAs(arr));
			Log("proj = " + proj.ToJsonIndented());
			Assert.That(proj, Has.Count.EqualTo(arr.Count));

			JsonObject p;

			p = (JsonObject) proj[0];
			Assert.That(p, Is.Not.Null.And.Not.SameAs(arr[0]));
			Assert.That(p.Get<int>("Id"), Is.EqualTo(1));
			Assert.That(p.Get<string>("Name"), Is.EqualTo("Walter White"));
			Assert.That(p.Get<string>("Pseudo"), Is.EqualTo("Einsenberg"));
			Assert.That(p.Get<string>("Job"), Is.EqualTo("Cook"));
			Assert.That(p.ContainsKey("Version"), Is.False);
			Assert.That(p, Has.Count.EqualTo(4));

			p = (JsonObject) proj[1];
			Assert.That(p, Is.Not.Null.And.Not.SameAs(arr[1]));
			Assert.That(p.Get<int>("Id"), Is.EqualTo(2));
			Assert.That(p.Get<string>("Name"), Is.EqualTo("Jesse Pinkman"));
			Assert.That(p.ContainsKey("Pseudo"), Is.False);
			Assert.That(p.Get<string>("Job"), Is.EqualTo("Drug Dealer"));
			Assert.That(p.ContainsKey("Version"), Is.False);
			Assert.That(p, Has.Count.EqualTo(3));

			p = (JsonObject) proj[2];
			Assert.That(p, Is.Not.Null.And.Not.SameAs(arr[2]));
			Assert.That(p.Get<int>("Id"), Is.EqualTo(3));
			Assert.That(p.Get<string>("Name"), Is.EqualTo("Walter White, Jr"));
			Assert.That(p.Get<string>("Pseudo"), Is.EqualTo("Flynn"));
			Assert.That(p.ContainsKey("Job"), Is.False);
			Assert.That(p.ContainsKey("Version"), Is.False);
			Assert.That(p, Has.Count.EqualTo(3));

			p = (JsonObject) proj[3];
			Assert.That(p, Is.Not.Null.And.Not.SameAs(arr[3]));
			Assert.That(p.ContainsKey("Id"), Is.False);
			Assert.That(p.ContainsKey("Name"), Is.False);
			Assert.That(p.ContainsKey("Pseudo"), Is.False);
			Assert.That(p.ContainsKey("Job"), Is.False);
			Assert.That(p.Get<int>("Version"), Is.EqualTo(1));
			Assert.That(p, Has.Count.EqualTo(1));

			p = (JsonObject) proj[4];
			Assert.That(p, Is.Not.Null.And.Not.SameAs(arr[5]));
			Assert.That(p, Has.Count.EqualTo(0));

			Assert.That(proj[5], Is.Not.Null);
			Assert.That(proj[5].Type, Is.EqualTo(JsonType.Null));

			Assert.That(proj[6], Is.Not.Null);
			Assert.That(proj[6].Type, Is.EqualTo(JsonType.Null));

			Assert.That(proj[7], Is.Not.Null);
			Assert.That(proj[7].Type, Is.EqualTo(JsonType.Null));

			#endregion

			#region Pick (keep missing)...

			// if the key does not exist in the source, it will be replaced by JsonNull.Missing

			proj = arr.Pick(
				[ "Id", "Name", "Pseudo", "Job" ],
				keepMissing: true
			);

			Assert.That(proj, Is.Not.Null.And.Not.SameAs(arr));
			Log("proj = " + proj.ToJsonIndented());
			Assert.That(proj, Has.Count.EqualTo(arr.Count));

			p = (JsonObject) proj[0];
			Assert.That(p, Is.Not.Null.And.Not.SameAs(arr[0]));
			Assert.That(p.Get<int>("Id"), Is.EqualTo(1));
			Assert.That(p.Get<string>("Name"), Is.EqualTo("Walter White"));
			Assert.That(p.Get<string>("Pseudo"), Is.EqualTo("Einsenberg"));
			Assert.That(p.Get<string>("Job"), Is.EqualTo("Cook"));
			Assert.That(p, Has.Count.EqualTo(4));

			p = (JsonObject) proj[1];
			Assert.That(p, Is.Not.Null.And.Not.SameAs(arr[1]));
			Assert.That(p.Get<int>("Id"), Is.EqualTo(2));
			Assert.That(p.Get<string>("Name"), Is.EqualTo("Jesse Pinkman"));
			Assert.That(p["Pseudo"], Is.EqualTo(JsonNull.Missing));
			Assert.That(p.Get<string>("Job"), Is.EqualTo("Drug Dealer"));
			Assert.That(p, Has.Count.EqualTo(4));

			p = (JsonObject) proj[2];
			Assert.That(p, Is.Not.Null.And.Not.SameAs(arr[2]));
			Assert.That(p.Get<int>("Id"), Is.EqualTo(3));
			Assert.That(p.Get<string>("Name"), Is.EqualTo("Walter White, Jr"));
			Assert.That(p.Get<string>("Pseudo"), Is.EqualTo("Flynn"));
			Assert.That(p["Job"], Is.EqualTo(JsonNull.Missing));
			Assert.That(p, Has.Count.EqualTo(4));

			p = (JsonObject) proj[3];
			Assert.That(p, Is.Not.Null.And.Not.SameAs(arr[3]));
			Assert.That(p["Id"], Is.EqualTo(JsonNull.Missing));
			Assert.That(p["Name"], Is.EqualTo(JsonNull.Missing));
			Assert.That(p["Pseudo"], Is.EqualTo(JsonNull.Missing));
			Assert.That(p["Job"], Is.EqualTo(JsonNull.Missing));
			Assert.That(p, Has.Count.EqualTo(4));

			p = (JsonObject) proj[4];
			Assert.That(p, Is.Not.Null.And.Not.SameAs(arr[5]));
			Assert.That(p["Id"], Is.EqualTo(JsonNull.Missing));
			Assert.That(p["Name"], Is.EqualTo(JsonNull.Missing));
			Assert.That(p["Pseudo"], Is.EqualTo(JsonNull.Missing));
			Assert.That(p["Job"], Is.EqualTo(JsonNull.Missing));
			Assert.That(p, Has.Count.EqualTo(4));

			Assert.That(proj[5], Is.Not.Null);
			Assert.That(proj[5].Type, Is.EqualTo(JsonType.Null));

			Assert.That(proj[6], Is.Not.Null);
			Assert.That(proj[6].Type, Is.EqualTo(JsonType.Null));

			Assert.That(proj[7], Is.Not.Null);
			Assert.That(proj[7].Type, Is.EqualTo(JsonType.Null));

			#endregion

			#region Pick (with JSON defaults)

			// if the key does not exist in the source, it will be replaced by the default value

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
			Assert.That(proj, Has.Count.EqualTo(arr.Count));

			p = (JsonObject) proj[0];
			Assert.That(p, Is.Not.Null.And.Not.SameAs(arr[0]));
			Assert.That(p.Get<int>("Id"), Is.EqualTo(1));
			Assert.That(p.Get<string>("Name"), Is.EqualTo("Walter White"));
			Assert.That(p.Get<string>("Pseudo"), Is.EqualTo("Einsenberg"));
			Assert.That(p.Get<string>("Job"), Is.EqualTo("Cook"));
			Assert.That(p, Has.Count.EqualTo(5));

			p = (JsonObject) proj[1];
			Assert.That(p, Is.Not.Null.And.Not.SameAs(arr[1]));
			Assert.That(p.Get<int>("Id"), Is.EqualTo(2));
			Assert.That(p.Get<string>("Name"), Is.EqualTo("Jesse Pinkman"));
			Assert.That(p["Pseudo"], Is.EqualTo(JsonNull.Null));
			Assert.That(p.Get<string>("Job"), Is.EqualTo("Drug Dealer"));
			Assert.That(p.Get<int>("Version"), Is.EqualTo(0));
			Assert.That(p, Has.Count.EqualTo(5));

			p = (JsonObject) proj[2];
			Assert.That(p, Is.Not.Null.And.Not.SameAs(arr[2]));
			Assert.That(p.Get<int>("Id"), Is.EqualTo(3));
			Assert.That(p.Get<string>("Name"), Is.EqualTo("Walter White, Jr"));
			Assert.That(p.Get<string>("Pseudo"), Is.EqualTo("Flynn"));
			Assert.That(p.Get<string>("Job"), Is.EqualTo("NEET"));
			Assert.That(p.Get<int>("Version"), Is.EqualTo(0));
			Assert.That(p, Has.Count.EqualTo(5));

			p = (JsonObject) proj[3];
			Assert.That(p, Is.Not.Null.And.Not.SameAs(arr[3]));
			Assert.That(p["Id"], Is.EqualTo(JsonNull.Error));
			Assert.That(p.Get<string>("Name"), Is.EqualTo("John Doe"));
			Assert.That(p["Pseudo"], Is.EqualTo(JsonNull.Null));
			Assert.That(p.Get<string>("Job"), Is.EqualTo("NEET"));
			Assert.That(p.Get<int>("Version"), Is.EqualTo(1));
			Assert.That(p, Has.Count.EqualTo(5));

			p = (JsonObject) proj[4];
			Assert.That(p, Is.Not.Null.And.Not.SameAs(arr[5]));
			Assert.That(p["Id"], Is.EqualTo(JsonNull.Error));
			Assert.That(p.Get<string>("Name"), Is.EqualTo("John Doe"));
			Assert.That(p["Pseudo"], Is.EqualTo(JsonNull.Null));
			Assert.That(p.Get<string>("Job"), Is.EqualTo("NEET"));
			Assert.That(p.Get<int>("Version"), Is.EqualTo(0));
			Assert.That(p, Has.Count.EqualTo(5));

			Assert.That(proj[5], Is.Not.Null);
			Assert.That(proj[5].Type, Is.EqualTo(JsonType.Null));

			Assert.That(proj[6], Is.Not.Null);
			Assert.That(proj[6].Type, Is.EqualTo(JsonType.Null));

			Assert.That(proj[7], Is.Not.Null);
			Assert.That(proj[7].Type, Is.EqualTo(JsonType.Null));

			#endregion

			#region Pick (with object defaults)

			// if the key does not exist in the source, it is replaced by the default value present in the anonymous template

			proj = arr.Pick(
				new
				{
					Id = JsonNull.Error, // <= equivalent to null, but can be tested by the caller (it would not be produced by deserializing)
					Name = "John Doe",
					Pseudo = JsonNull.Null,
					Job = "NEET",
					Version = 0,
				});

			Assert.That(proj, Is.Not.Null.And.Not.SameAs(arr));
			Log("proj = " + proj.ToJsonIndented());
			Assert.That(proj, Has.Count.EqualTo(arr.Count));

			p = (JsonObject) proj[0];
			Assert.That(p, Is.Not.Null.And.Not.SameAs(arr[0]));
			Assert.That(p.Get<int>("Id"), Is.EqualTo(1));
			Assert.That(p.Get<string>("Name"), Is.EqualTo("Walter White"));
			Assert.That(p.Get<string>("Pseudo"), Is.EqualTo("Einsenberg"));
			Assert.That(p.Get<string>("Job"), Is.EqualTo("Cook"));
			Assert.That(p.Get<int>("Version"), Is.EqualTo(0));
			Assert.That(p, Has.Count.EqualTo(5));

			p = (JsonObject) proj[1];
			Assert.That(p, Is.Not.Null.And.Not.SameAs(arr[1]));
			Assert.That(p.Get<int>("Id"), Is.EqualTo(2));
			Assert.That(p.Get<string>("Name"), Is.EqualTo("Jesse Pinkman"));
			Assert.That(p["Pseudo"], Is.EqualTo(JsonNull.Null));
			Assert.That(p.Get<string>("Job"), Is.EqualTo("Drug Dealer"));
			Assert.That(p.Get<int>("Version"), Is.EqualTo(0));
			Assert.That(p, Has.Count.EqualTo(5));

			p = (JsonObject) proj[2];
			Assert.That(p, Is.Not.Null.And.Not.SameAs(arr[2]));
			Assert.That(p.Get<int>("Id"), Is.EqualTo(3));
			Assert.That(p.Get<string>("Name"), Is.EqualTo("Walter White, Jr"));
			Assert.That(p.Get<string>("Pseudo"), Is.EqualTo("Flynn"));
			Assert.That(p.Get<string>("Job"), Is.EqualTo("NEET"));
			Assert.That(p.Get<int>("Version"), Is.EqualTo(0));
			Assert.That(p, Has.Count.EqualTo(5));

			p = (JsonObject) proj[3];
			Assert.That(p, Is.Not.Null.And.Not.SameAs(arr[3]));
			Assert.That(p["Id"], Is.EqualTo(JsonNull.Error));
			Assert.That(p.Get<string>("Name"), Is.EqualTo("John Doe"));
			Assert.That(p["Pseudo"], Is.EqualTo(JsonNull.Null));
			Assert.That(p.Get<string>("Job"), Is.EqualTo("NEET"));
			Assert.That(p.Get<int>("Version"), Is.EqualTo(1));
			Assert.That(p, Has.Count.EqualTo(5));

			p = (JsonObject) proj[4];
			Assert.That(p, Is.Not.Null.And.Not.SameAs(arr[5]));
			Assert.That(p["Id"], Is.EqualTo(JsonNull.Error));
			Assert.That(p.Get<string>("Name"), Is.EqualTo("John Doe"));
			Assert.That(p["Pseudo"], Is.EqualTo(JsonNull.Null));
			Assert.That(p.Get<string>("Job"), Is.EqualTo("NEET"));
			Assert.That(p.Get<int>("Version"), Is.EqualTo(0));
			Assert.That(p, Has.Count.EqualTo(5));

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
		public void Test_JsonArray_ReadOnly_Empty()
		{
			Assert.That(JsonArray.ReadOnly.Empty.IsReadOnly, Is.True);
			//note: we don't want to attempt to modify the empty readonly singleton, because if the test fails, it will completely break ALL the reamining tests!

			static void CheckEmptyReadOnly(JsonArray arr, [CallerArgumentExpression(nameof(arr))] string? expression = null)
			{
				Assert.That(arr, Has.Count.Zero, expression);
				AssertIsImmutable(arr, expression);
				Assert.That(arr, Has.Count.Zero, expression);
			}

			CheckEmptyReadOnly(JsonArray.ReadOnly.Create());
			CheckEmptyReadOnly(JsonArray.ReadOnly.Create([]));
			CheckEmptyReadOnly(JsonArray.Create().ToReadOnly());
			CheckEmptyReadOnly(JsonArray.Copy(JsonArray.Create(), deep: false, readOnly: true));
			CheckEmptyReadOnly(JsonArray.Copy(JsonArray.Create(), deep: true, readOnly: true));

			var arr = JsonArray.Create("hello");
			arr.RemoveAt(0);
			CheckEmptyReadOnly(arr.ToReadOnly());
			CheckEmptyReadOnly(JsonArray.Copy(arr, deep: false, readOnly: true));
			CheckEmptyReadOnly(JsonArray.Copy(arr, deep: true, readOnly: true));
		}

		[Test]
		public void Test_JsonArray_ReadOnly()
		{
			// creating a readonly object with only immutable values should produce an immutable object

			// DEPRECATED: should use collection expressions instead: [ ... ]
			AssertIsImmutable(JsonArray.ReadOnly.Create("one"));
			AssertIsImmutable(JsonArray.ReadOnly.Create("one", "two"));
			AssertIsImmutable(JsonArray.ReadOnly.Create("one", "two", "three"));
			AssertIsImmutable(JsonArray.ReadOnly.Create("one", "two", "three", "four"));

			// collection expressions: should invoke the ReadOnlySpan<> overload
			AssertIsImmutable(JsonArray.ReadOnly.Create(["one"]));
			AssertIsImmutable(JsonArray.ReadOnly.Create(["one", "two"]));
			AssertIsImmutable(JsonArray.ReadOnly.Create(["one", "two", "three"]));
			AssertIsImmutable(JsonArray.ReadOnly.Create(["one", "two", "three", "four"]));
			AssertIsImmutable(JsonArray.ReadOnly.Create(["one", "two", "three", "four", "five"]));

			// params JsonValue[]
			AssertIsImmutable(JsonArray.ReadOnly.Create((JsonValue[]) ["one"]));
			AssertIsImmutable(JsonArray.ReadOnly.Create((JsonValue[]) ["one", "two"]));
			AssertIsImmutable(JsonArray.ReadOnly.Create((JsonValue[]) ["one", "two", "three"]));
			AssertIsImmutable(JsonArray.ReadOnly.Create((JsonValue[]) ["one", "two", "three", "four"]));
			AssertIsImmutable(JsonArray.ReadOnly.Create((JsonValue[]) ["one", "two", "three", "four", "five"]));

			AssertIsImmutable(JsonArray.ReadOnly.FromValues(Enumerable.Range(0, 10).Select(i => KeyValuePair.Create(i.ToString(), i))));
			AssertIsImmutable(JsonArray.ReadOnly.FromValues(Enumerable.Range(0, 10).Select(i => KeyValuePair.Create(i.ToString(), i)).ToArray()));
			AssertIsImmutable(JsonArray.ReadOnly.FromValues(Enumerable.Range(0, 10).Select(i => KeyValuePair.Create(i.ToString(), i)).ToList()));
			AssertIsImmutable(JsonArray.ReadOnly.FromValues(Enumerable.Range(0, 10).Select(i => KeyValuePair.Create(i.ToString(), i)).ToList()));

			// creating an immutable version of a writable object with only immutable should return an immutable object
			AssertIsImmutable(JsonArray.Create("one", 1).ToReadOnly());
			AssertIsImmutable(JsonArray.Create("one", 1, "two", 2, "three", 3).ToReadOnly());

			// parsing with JsonImmutable should return an already immutable object
			var obj = JsonObject.Parse("""{ "hello": "world", "foo": { "id": 123, "name": "Foo", "address" : { "street": 123, "city": "Paris" } }, "bar": [ 1, 2, 3 ], "baz": [ { "jazz": 42 } ] }""", CrystalJsonSettings.JsonReadOnly);
			AssertIsImmutable(obj);
			var foo = obj.GetObject("foo");
			AssertIsImmutable(foo);
			var addr = foo.GetObject("address");
			AssertIsImmutable(addr);
			var bar = obj.GetArray("bar");
			AssertIsImmutable(bar);
			var baz = obj.GetArray("baz");
			AssertIsImmutable(baz);
			var jazz = baz.GetObject(0);
			AssertIsImmutable(jazz);
		}

		[Test]
		public void Test_JsonArray_Freeze()
		{
			// ensure that, given a mutable JSON object, we can create an immutable version that is protected against any changes

			// the original object should be mutable
			var arr = new JsonArray
			{
				"hello",
				"world",
				new JsonObject { ["bar"] = "baz" },
				new JsonArray { 1, 2, 3 }
			};
			Assert.That(arr.IsReadOnly, Is.False);
			// the inner object and array should be mutable as well
			var innerObj = arr.GetObject(2);
			Assert.That(arr.IsReadOnly, Is.False);
			var innerArray = arr.GetArray(3);
			Assert.That(arr.IsReadOnly, Is.False);

			Assert.That(arr.ToJsonCompact(), Is.EqualTo("""["hello","world",{"bar":"baz"},[1,2,3]]"""));

			var arr2 = arr.Freeze();
			Assert.That(arr2, Is.SameAs(arr), "Freeze() should return the same instance");
			AssertIsImmutable(arr2, "obj.Freeze()");

			// the inner object should also have been frozen!
			var innerObj2 = arr2.GetObject(2);
			Assert.That(innerObj2, Is.SameAs(innerObj));
			Assert.That(innerObj2.IsReadOnly, Is.True);
			AssertIsImmutable(innerObj2, "(obj.Freeze())[\"foo\"]");

			// the inner array should also have been frozen!
			var innerArray2 = arr2.GetArray(3);
			Assert.That(innerArray2, Is.SameAs(innerArray));
			Assert.That(innerArray2.IsReadOnly, Is.True);
			AssertIsImmutable(innerArray2, "(obj.Freeze())[\"bar\"]");

			Assert.That(arr2.ToJsonCompact(), Is.EqualTo("""["hello","world",{"bar":"baz"},[1,2,3]]"""));

			// if we want to mutate, we have to create a copy
			var arr3 = arr2.Copy();

			// the copy should still be equal to the original
			Assert.That(arr3, Is.Not.SameAs(arr2), "it should return a new instance");
			Assert.That(arr3, Is.EqualTo(arr2), "It should still be equal");
			var innerObj3 = arr3.GetObject(2);
			Assert.That(innerObj3, Is.Not.SameAs(innerObj2), "inner object should be cloned");
			Assert.That(innerObj3, Is.EqualTo(innerObj2), "It should still be equal");
			var innerArray3 = arr3.GetArray(3);
			Assert.That(innerArray3, Is.Not.SameAs(innerArray2), "inner array should be cloned");
			Assert.That(innerArray3, Is.EqualTo(innerArray2), "It should still be equal");

			// should be aable to change the copy
			Assert.That(arr3, Has.Count.EqualTo(4));
			Assert.That(() => arr3.Add("bonjour"), Throws.Nothing);
			Assert.That(arr3, Has.Count.EqualTo(5));
			Assert.That(arr3, Is.Not.EqualTo(arr2), "It should still not be equal after the change");
			Assert.That(innerObj3, Is.EqualTo(innerObj2), "It should still be equal");
			Assert.That(innerArray3, Is.EqualTo(innerArray2), "It should still be equal");

			// should be able to mutate the inner object
			Assert.That(innerObj3, Has.Count.EqualTo(1));
			Assert.That(() => innerObj3["baz"] = "jazz", Throws.Nothing);
			Assert.That(innerObj3, Has.Count.EqualTo(2));
			Assert.That(innerObj3, Is.Not.EqualTo(innerObj2), "It should still not be equal after the change");

			// should be able to mutate the inner array
			Assert.That(innerArray3, Has.Count.EqualTo(3));
			Assert.That(() => innerArray3.Add(4), Throws.Nothing);
			Assert.That(innerArray3, Has.Count.EqualTo(4));
			Assert.That(innerArray3, Is.Not.EqualTo(innerArray2), "It should still not be equal after the change");

			// verify the final mutated version
			Assert.That(arr3.ToJsonCompact(), Is.EqualTo("""["hello","world",{"bar":"baz","baz":"jazz"},[1,2,3,4],"bonjour"]"""));
			// ensure the original is unmodified
			Assert.That(arr2.ToJsonCompact(), Is.EqualTo("""["hello","world",{"bar":"baz"},[1,2,3]]"""));
		}

		[Test]
		public void Test_JsonArray_Can_Mutate_Frozen()
		{
			// given an immutable object, check that we can create mutable version that will not modifiy the original
			var original = new JsonArray
			{
				"hello",
				"world",
				new JsonObject { ["bar"] = "baz" },
				new JsonArray { 1, 2, 3 }
			}.Freeze();
			Dump("Original", original);
			EnsureDeepImmutabilityInvariant(original);

			// create a "mutable" version of the entire tree
			var obj = original.ToMutable();
			Dump("Copy", obj);
			Assert.That(obj.IsReadOnly, Is.False, "Copy should be not be read-only!");
			Assert.That(obj, Is.Not.SameAs(original));
			Assert.That(obj, Is.EqualTo(original));
			Assert.That(obj[0], Is.SameAs(original[0]));
			Assert.That(obj[1], Is.SameAs(original[1]));
			Assert.That(obj.GetObject(2), Is.Not.SameAs(original.GetObject(2)));
			Assert.That(obj.GetArray(3), Is.Not.SameAs(original.GetArray(3)));
			EnsureDeepMutabilityInvariant(obj);

			obj[0] = "le monde";
			obj.GetObject(2).Remove("bar");
			obj.GetObject(2).Add("baz", "bar");
			obj.GetArray(3).Add(4);
			obj.Add(42);
			Dump("Mutated", obj);
			// ensure the copy have been mutated
			Assert.That(obj.Get<string>(0), Is.EqualTo("le monde"));
			Assert.That(obj.GetObject(2).Get<string?>("bar", null), Is.Null);
			Assert.That(obj.GetObject(2).Get<string>("baz"), Is.EqualTo("bar"));
			Assert.That(obj.GetArray(3), Has.Count.EqualTo(4));
			Assert.That(obj.Get<int>(4), Is.EqualTo(42));
			Assert.That(obj, Is.Not.EqualTo(original));

			// ensure the original is not mutated
			Dump("Original", original);
			Assert.That(original.Get<string>(0), Is.EqualTo("hello"));
			Assert.That(original.GetObject(2).Get<string>("bar"), Is.EqualTo("baz"));
			Assert.That(original.GetObject(2).Get<string?>("baz", null), Is.Null);
			Assert.That(original.GetArray(3), Has.Count.EqualTo(3));
			Assert.That(original, Has.Count.EqualTo(4));
		}

		[Test]
		public void Test_JsonArray_CopyAndMutate()
		{
			// Test the "builder" API that can simplify making changes to a read-only arrays and publishing the new instance.
			// All methods will create return a new copy of the original, with the mutation applied, leaving the original untouched.
			// The new read-only copy should reuse the same JsonValue instances as the original, to reduce memory copies.

			var arr = JsonArray.ReadOnly.Empty;
			Assume.That(arr, IsJson.Empty);
			Assume.That(arr, IsJson.ReadOnly);
			Assume.That(arr[0], IsJson.Error);
			DumpCompact(arr);

			// copy and add first item
			var arr2 = arr.CopyAndAdd("world");
			DumpCompact(arr2);
			Assert.That(arr2, Is.Not.SameAs(arr));
			Assert.That(arr2, Has.Count.EqualTo(1));
			Assert.That(arr2, IsJson.ReadOnly);
			Assert.That(arr2, IsJson.EqualTo([ "world" ]));
			Assert.That(arr, IsJson.Empty);

			// copy and set second item
			var arr3 = arr2.CopyAndSet(1, "bar");
			DumpCompact(arr3);
			Assert.That(arr3, Is.Not.SameAs(arr2));
			Assert.That(arr3, Has.Count.EqualTo(2));
			Assert.That(arr3, IsJson.ReadOnly);
			Assert.That(arr3[0], Is.SameAs(arr2[0]));
			Assert.That(arr3[1], IsJson.EqualTo("bar"));
			Assert.That(arr2, IsJson.EqualTo([ "world" ]));
			Assert.That(arr, IsJson.Empty);

			// copy and set should overwrite existing item
			var arr4 = arr3.CopyAndSet(1, "baz");
			DumpCompact(arr4);
			Assert.That(arr4, Is.Not.SameAs(arr3));
			Assert.That(arr4, Has.Count.EqualTo(2));
			Assert.That(arr4, IsJson.ReadOnly);
			Assert.That(arr4[0], Is.EqualTo("world").And.SameAs(arr3[0]));
			Assert.That(arr4[1], IsJson.EqualTo("baz"));
			Assert.That(arr3, IsJson.EqualTo([ "world", "bar" ]));
			Assert.That(arr2, IsJson.EqualTo([ "world" ]));
			Assert.That(arr, IsJson.Empty);

			// copy and remove
			var arr5 = arr4.CopyAndRemove(0);
			DumpCompact(arr5);
			Assert.That(arr5, Is.Not.SameAs(arr4));
			Assert.That(arr5, Has.Count.EqualTo(1));
			Assert.That(arr5, IsJson.ReadOnly);
			Assert.That(arr5[0], IsJson.EqualTo("baz"));
			Assert.That(arr5[1], IsJson.Error);
			Assert.That(arr4, IsJson.EqualTo([ "world", "baz" ]));
			Assert.That(arr3, IsJson.EqualTo([ "world", "bar" ]));
			Assert.That(arr2, IsJson.EqualTo([ "world" ]));
			Assert.That(arr, IsJson.Empty);

			// copy and try removing last field
			var arr6 = arr5.CopyAndRemove(0, out var prev);
			DumpCompact(arr6);
			Assert.That(arr6, Is.Not.SameAs(arr5));
			Assert.That(arr6, Is.Empty);
			Assert.That(arr6, IsJson.ReadOnly);
			Assert.That(arr6[0], IsJson.Error);
			Assert.That(arr6[1], IsJson.Error);
			Assert.That(arr6, Is.SameAs(JsonArray.ReadOnly.Empty));
			Assert.That(prev, Is.SameAs(arr5[0]));
			Assert.That(arr5, IsJson.EqualTo([ "baz" ]));
			Assert.That(arr4, IsJson.EqualTo([ "world", "baz" ]));
			Assert.That(arr3, IsJson.EqualTo([ "world", "bar" ]));
			Assert.That(arr2, IsJson.EqualTo([ "world" ]));
			Assert.That(arr, IsJson.Empty);

			// maximize test coverage

			static void Check(
				JsonArray actual,
				IResolveConstraint expression,
				NUnitString message = default,
				[CallerArgumentExpression(nameof(actual))] string actualExpression = "",
				[CallerArgumentExpression(nameof(expression))]
				string constraintExpression = "")
			{
				Dump(actualExpression, actual);
				Assert.That(actual, expression, message, actualExpression, constraintExpression);
			}

			Check(JsonArray.ReadOnly.Empty.CopyAndAdd("hello"), IsJson.ReadOnly.And.EqualTo([ "hello" ]));
			Check(JsonArray.Create(["hello"]).CopyAndAdd("world"), IsJson.ReadOnly.And.EqualTo([ "hello", "world" ]));

			Check(JsonArray.ReadOnly.Empty.CopyAndSet(0, "hello"), IsJson.ReadOnly.And.EqualTo([ "hello" ]));
			Check(JsonArray.ReadOnly.Empty.CopyAndSet(1, "hello"), IsJson.ReadOnly.And.EqualTo([ null, "hello" ]));
			Check(JsonArray.Create(["hello", "world"]).CopyAndSet(0, "bonjour"), IsJson.ReadOnly.And.EqualTo([ "bonjour", "world" ]));
			Check(JsonArray.Create(["hello", "world"]).CopyAndSet(1, "le monde"), IsJson.ReadOnly.And.EqualTo([ "hello", "le monde" ]));
			Check(JsonArray.Create(["hello", "world"]).CopyAndSet(2, "!"), IsJson.ReadOnly.And.EqualTo([ "hello", "world", "!" ]));
			Check(JsonArray.Create(["hello", "world"]).CopyAndSet(3, "!"), IsJson.ReadOnly.And.EqualTo([ "hello", "world", null, "!" ]));

			Check(JsonArray.Create(["hello", "world"]).CopyAndSet(^2, "bonjour"), IsJson.ReadOnly.And.EqualTo([ "bonjour", "world" ]));
			Check(JsonArray.Create(["hello", "world"]).CopyAndSet(^1, "le monde"), IsJson.ReadOnly.And.EqualTo([ "hello", "le monde" ]));

			Check(JsonArray.ReadOnly.Empty.CopyAndInsert(0, "hello"), IsJson.ReadOnly.And.EqualTo([ "hello" ]));
			Check(JsonArray.ReadOnly.Empty.CopyAndInsert(1, "hello"), IsJson.ReadOnly.And.EqualTo([ null, "hello" ]));
			Check(JsonArray.Create(["hello", "world"]).CopyAndInsert(0, "say"), IsJson.ReadOnly.And.EqualTo([ "say", "hello", "world" ]));
			Check(JsonArray.Create(["hello", "world"]).CopyAndInsert(1, ", "), IsJson.ReadOnly.And.EqualTo([ "hello", ", ", "world" ]));
			Check(JsonArray.Create(["hello", "world"]).CopyAndInsert(2, "!"), IsJson.ReadOnly.And.EqualTo([ "hello", "world", "!" ]));
			Check(JsonArray.Create(["hello", "world"]).CopyAndInsert(3, "!"), IsJson.ReadOnly.And.EqualTo([ "hello", "world", null, "!" ]));

			Check(JsonArray.ReadOnly.Empty.CopyAndInsert(new Index(0), "hello"), IsJson.ReadOnly.And.EqualTo([ "hello" ]));
			Check(JsonArray.ReadOnly.Empty.CopyAndInsert(new Index(1), "hello"), IsJson.ReadOnly.And.EqualTo([ null, "hello" ]));
			Check(JsonArray.Create(["hello", "world"]).CopyAndInsert(^2, "say"), IsJson.ReadOnly.And.EqualTo([ "say", "hello", "world" ]));
			Check(JsonArray.Create(["hello", "world"]).CopyAndInsert(^1, ", "), IsJson.ReadOnly.And.EqualTo([ "hello", ", ", "world" ]));
			Check(JsonArray.Create(["hello", "world"]).CopyAndInsert(^0, "!"), IsJson.ReadOnly.And.EqualTo([ "hello", "world", "!" ]));
			Check(JsonArray.Create(["hello", "world"]).CopyAndInsert(new Index(3), "!"), IsJson.ReadOnly.And.EqualTo([ "hello", "world", null, "!" ]));

			Check(JsonArray.ReadOnly.Empty.CopyAndRemove(0), Is.SameAs(JsonArray.ReadOnly.Empty));
			Check(JsonArray.ReadOnly.Empty.CopyAndRemove(1), Is.SameAs(JsonArray.ReadOnly.Empty));
			Check(JsonArray.Create(["hello", "world"]).CopyAndRemove(0), IsJson.ReadOnly.And.EqualTo([ "world" ]));
			Check(JsonArray.Create(["hello", "world"]).CopyAndRemove(1), IsJson.ReadOnly.And.EqualTo([ "hello" ]));
			Check(JsonArray.Create(["hello", "world"]).CopyAndRemove(2), IsJson.ReadOnly.And.EqualTo([ "hello", "world" ]));
		}

		[Test]
		public void Test_JsonArray_CopyAndConcat()
		{
			{ // Empty + Empty
				var orig = JsonArray.ReadOnly.Empty;
				var tail = JsonArray.ReadOnly.Empty;
				var arr = orig.CopyAndConcat(tail);
				Assert.That(arr, Is.SameAs(JsonArray.ReadOnly.Empty));
			}
			{ // Empty + TAIL
				var orig = JsonArray.ReadOnly.Empty;
				var tail = JsonArray.Create("hello", "world");
				var arr = orig.CopyAndConcat(tail);
				Assert.That(arr, IsJson.ReadOnly.And.EqualTo([ "hello", "world" ]));
				Assert.That(arr, Is.Not.SameAs(orig));
			}
			{ // HEAD + Empty
				var orig = JsonArray.Create("hello", "world");
				var tail = JsonArray.ReadOnly.Empty;
				var arr = orig.CopyAndConcat(tail);
				Assert.That(arr, IsJson.ReadOnly.And.EqualTo([ "hello", "world" ]));
				Assert.That(arr, Is.Not.SameAs(orig));
			}
			{ // HEAD + TAIL
				var orig = JsonArray.Create("say");
				var tail = JsonArray.Create("hello", "world");
				var arr = orig.CopyAndConcat(tail);
				Assert.That(arr, IsJson.ReadOnly.And.EqualTo([ "say", "hello", "world" ]));
				Assert.That(orig, IsJson.EqualTo([ "say" ]));
				Assert.That(tail, IsJson.EqualTo([ "hello", "world" ]));
			}
			{ // Replace Root
				JsonArray root = JsonArray.ReadOnly.Empty;
				JsonArray v0 = root;
				JsonArray.CopyAndConcat(ref root, JsonArray.Create([ "say" ]));
				Assert.That(root, Is.Not.SameAs(v0));
				Assert.That(root, IsJson.ReadOnly.And.EqualTo([ "say" ]));
				Assert.That(v0, IsJson.Empty);

				JsonArray v1 = root;
				JsonArray.CopyAndConcat(ref root, JsonArray.Create([ "hello" ]));
				Assert.That(root, Is.Not.SameAs(v1));
				Assert.That(root, IsJson.ReadOnly.And.EqualTo([ "say", "hello" ]));
				Assert.That(v1, IsJson.EqualTo([ "say" ]));

				JsonArray v2 = root;
				JsonArray.CopyAndConcat(ref root, JsonArray.Create([ "world" ]));
				Assert.That(root, Is.Not.SameAs(v2));
				Assert.That(root, IsJson.ReadOnly.And.EqualTo([ "say", "hello", "world" ]));
				Assert.That(v2, IsJson.EqualTo([ "say", "hello" ]));
				Assert.That(v1, IsJson.EqualTo([ "say" ]));

				JsonArray v3 = root;
				JsonArray.CopyAndConcat(ref root, JsonArray.ReadOnly.Empty);
				Assert.That(root, Is.SameAs(v3));
				Assert.That(root, IsJson.ReadOnly.And.EqualTo([ "say", "hello", "world" ]));
				Assert.That(v2, IsJson.EqualTo([ "say", "hello" ]));
				Assert.That(v1, IsJson.EqualTo([ "say" ]));
			}
		}

		[Test]
		public void Test_JsonArray_IndexOf()
		{
			Assert.Multiple(() =>
			{
				// find string
				Assert.That(JsonArray.Create().IndexOf("hello"), Is.EqualTo(-1));
				Assert.That(JsonArray.Create("hello").IndexOf("hello"), Is.EqualTo(0));
				Assert.That(JsonArray.Create("hello", 123).IndexOf("hello"), Is.EqualTo(0));
				Assert.That(JsonArray.Create(123, "hello").IndexOf("hello"), Is.EqualTo(1));
				Assert.That(JsonArray.Create("hello", "hello").IndexOf("hello"), Is.EqualTo(0));

				// find number
				Assert.That(JsonArray.Create(123).IndexOf(123), Is.EqualTo(0));
				Assert.That(JsonArray.Create(123, "hello").IndexOf(123), Is.EqualTo(0));
				Assert.That(JsonArray.Create("hello", 123).IndexOf(123), Is.EqualTo(1));
				Assert.That(JsonArray.Create(123).IndexOf(123), Is.EqualTo(0));
				Assert.That(JsonArray.Create(123, 123).IndexOf(123), Is.EqualTo(0));

				// find number
				Assert.That(JsonArray.Create(true).IndexOf(true), Is.EqualTo(0));
				Assert.That(JsonArray.Create(true).IndexOf(false), Is.EqualTo(-1));
				Assert.That(JsonArray.Create("hello", true).IndexOf(true), Is.EqualTo(1));
				Assert.That(JsonArray.Create(JsonNull.Null).IndexOf(false), Is.EqualTo(-1));
				Assert.That(JsonArray.Create("").IndexOf(false), Is.EqualTo(-1));
				Assert.That(JsonArray.Create(0).IndexOf(false), Is.EqualTo(-1));

				// find null-like
				Assert.That(JsonArray.Create("hello", JsonNull.Null, "world").IndexOf(null), Is.EqualTo(1), "Null is null-like");
				Assert.That(JsonArray.Create("hello", JsonNull.Null, "world").IndexOf(JsonNull.Null), Is.EqualTo(1));
				Assert.That(JsonArray.Create("hello", JsonNull.Null, "world").IndexOf(JsonNull.Missing), Is.EqualTo(-1), "Missing does not equal Null");
				Assert.That(JsonArray.Create("hello", JsonNull.Null, "world").IndexOf(JsonNull.Error), Is.EqualTo(-1), "Error does not equal Null");
				Assert.That(JsonArray.Create("hello", JsonNull.Missing, "world").IndexOf(null), Is.EqualTo(1), "Missing is null-like");
				Assert.That(JsonArray.Create("hello", JsonNull.Missing, "world").IndexOf(JsonNull.Null), Is.EqualTo(-1), "Null does not equal Missing");
				Assert.That(JsonArray.Create("hello", JsonNull.Missing, "world").IndexOf(JsonNull.Missing), Is.EqualTo(1));

				// find sub-object
				var obj = JsonObject.Create([ ("hello", "there") ]);
				Assert.That(JsonArray.Create("foo", obj, "bar").IndexOf(obj), Is.EqualTo(1));

				// find sub-array
				var arr = JsonArray.Create([ "hello", "there" ]);
				Assert.That(JsonArray.Create("foo", arr, "bar").IndexOf(arr), Is.EqualTo(1));

			});
		}

		[Test]
		public void Test_JsonArray_ValueEquals()
		{
			// note: do not confuse ValueEquals<TCollection>(TCollection) vs ValuesEqual<TItem>(IEnumerable<TItem>) !

			Assert.Multiple(() =>
			{
				var arr = JsonArray.ReadOnly.Empty;

				Assert.That(arr.ValueEquals<string[]>([ ]), Is.True);
				Assert.That(arr.ValueEquals<List<string>>([ ]), Is.True);
				Assert.That(arr.ValueEquals<IEnumerable<string>>([ ]), Is.True);

				Assert.That(arr.ValuesEqual((string[]) [ ]), Is.True);
				Assert.That(arr.ValuesEqual((ReadOnlySpan<string>) [ ]), Is.True);
				Assert.That(arr.ValuesEqual((List<string>) [ ]), Is.True);
				Assert.That(arr.ValuesEqual((IEnumerable<string>) [ ]), Is.True);

				Assert.That(arr.ValuesEqual(Enumerable.Empty<string>()), Is.True);
				Assert.That(arr.ValuesEqual(Enumerable.Empty<int>()), Is.True);
				Assert.That(arr.ValuesEqual(Enumerable.Empty<bool>()), Is.True);

				Assert.That(arr.ValuesEqual(new JsonArray()), Is.True);
			});
		
			Assert.Multiple(() =>
			{
				var arr = JsonArray.Create([ "foo", "bar" ]);

				Assert.That(arr.ValueEquals(JsonArray.Create("foo", "bar")), Is.True);
				Assert.That(arr.ValueEquals<string[]>([ "foo", "bar" ]), Is.True);
				Assert.That(arr.ValueEquals<List<string>>([ "foo", "bar" ]), Is.True);
				Assert.That(arr.ValueEquals<IEnumerable<string>>([ "foo", "bar" ]), Is.True);
				Assert.That(arr.ValueEquals<IEnumerable<string>>(Enumerable.Range(0, 2).Select(i => i == 0 ? "foo" : "bar")), Is.True);

				Assert.That(arr.ValueEquals(JsonArray.Create("foo", "baz")), Is.False);
				Assert.That(arr.ValueEquals(JsonArray.Create("foo", "bar", "baz")), Is.False);
				Assert.That(arr.ValueEquals<string[]>([ "foo", "baz" ]), Is.False);
				Assert.That(arr.ValueEquals<List<string>>([ "foo", "bar", "baz" ]), Is.False);
				Assert.That(arr.ValueEquals<IEnumerable<string>>(Enumerable.Empty<string>()), Is.False);
				Assert.That(arr.ValueEquals<IEnumerable<string>>(Enumerable.Range(0, 1).Select(_ => "foo")), Is.False);
				Assert.That(arr.ValueEquals<IEnumerable<string>>(Enumerable.Range(0, 2).Select(i => i == 0 ? "foo" : "baz")), Is.False);
				Assert.That(arr.ValueEquals<IEnumerable<string>>(Enumerable.Range(0, 3).Select(i => i == 0 ? "foo" : i == 1 ? "bar" : "baz")), Is.False);

				Assert.That(arr.ValuesEqual([ "foo", "bar" ]), Is.True);
				Assert.That(arr.ValuesEqual(JsonArray.Create("foo", "bar")), Is.True);
				Assert.That(arr.ValuesEqual((ReadOnlySpan<string>) [ "foo", "bar" ]), Is.True);
				Assert.That(arr.ValuesEqual((string[]) [ "foo", "bar" ]), Is.True);
				Assert.That(arr.ValuesEqual((List<string>) [ "foo", "bar" ]), Is.True);
				Assert.That(arr.ValuesEqual(Enumerable.Range(0, 2).Select(i => i == 0 ? "foo" : "bar")), Is.True);

				Assert.That(arr.ValuesEqual(JsonArray.Create("foo", "baz")), Is.False);
				Assert.That(arr.ValuesEqual(JsonArray.Create("foo", "bar", "baz")), Is.False);
				Assert.That(arr.ValuesEqual((string[]) [ "foo", "baz" ]), Is.False);
				Assert.That(arr.ValuesEqual((List<string>) [ "foo", "bar", "baz" ]), Is.False);
				Assert.That(arr.ValuesEqual(Enumerable.Empty<string>()), Is.False);
				Assert.That(arr.ValuesEqual(Enumerable.Range(0, 1).Select(_ => "foo")), Is.False);
				Assert.That(arr.ValuesEqual(Enumerable.Range(0, 2).Select(i => i == 0 ? "foo" : "baz")), Is.False);
				Assert.That(arr.ValuesEqual(Enumerable.Range(0, 3).Select(i => i == 0 ? "foo" : i == 1 ? "bar" : "baz")), Is.False);

			});

			Assert.Multiple(() =>
			{
				var arr = JsonArray.Create([ 1, 2, 3 ]);

				Assert.That(arr.ValuesEqual(JsonArray.Create(1, 2, 3)), Is.True);

				Assert.That(arr.ValueEquals<int[]>([ 1, 2, 3 ]), Is.True);
				Assert.That(arr.ValueEquals<long[]>([ 1, 2, 3 ]), Is.True);
				Assert.That(arr.ValueEquals<double[]>([ 1.0, 2.0, 3.0 ]), Is.True);
				Assert.That(arr.ValueEquals<float[]>([ 1.0f, 2.0f, 3.0f ]), Is.True);
				Assert.That(arr.ValueEquals<List<int>>([ 1, 2, 3 ]), Is.True);
				Assert.That(arr.ValueEquals<List<long>>([ 1, 2, 3 ]), Is.True);
				Assert.That(arr.ValueEquals<List<double>>([ 1.0, 2.0, 3.0 ]), Is.True);
				Assert.That(arr.ValueEquals<List<float>>([ 1.0f, 2.0f, 3.0f ]), Is.True);

				Assert.That(arr.ValueEquals<int[]>([ 1, 2 ]), Is.False);
				Assert.That(arr.ValueEquals<int[]>([ 1, 2, 3, 4 ]), Is.False);
				Assert.That(arr.ValueEquals<string[]>([ "1", "2", "3" ]), Is.False);

				Assert.That(arr.ValuesEqual((ReadOnlySpan<int>) [ 1, 2, 3 ]), Is.True);
				Assert.That(arr.ValuesEqual((int[]) [ 1, 2, 3 ]), Is.True);
				Assert.That(arr.ValuesEqual((List<int>) [ 1, 2, 3 ]), Is.True);
				Assert.That(arr.ValuesEqual((IEnumerable<int>) [ 1, 2, 3 ]), Is.True);
				Assert.That(arr.ValuesEqual(Enumerable.Range(1, 3)), Is.True);

				Assert.That(arr.ValuesEqual<long>([ 1L, 2L, 3L ]), Is.True);
				Assert.That(arr.ValuesEqual<double>([ 1.0, 2.0, 3.0 ]), Is.True);
				Assert.That(arr.ValuesEqual<float>([ 1.0f, 2.0f, 3.0f ]), Is.True);
				Assert.That(arr.ValuesEqual<decimal>([ 1.0m, 2.0m, 3.0m ]), Is.True);

				// collection expression
				Assert.That(arr.ValuesEqual([ 1, 2, 3 ]), Is.True);
				Assert.That(arr.ValuesEqual([ 1L, 2L, 3L ]), Is.True);
				Assert.That(arr.ValuesEqual([ 1.0, 2.0, 3.0 ]), Is.True);
				Assert.That(arr.ValuesEqual([ 1.0f, 2.0f, 3.0f ]), Is.True);
				Assert.That(arr.ValuesEqual([ 1.0m, 2.0m, 3.0m ]), Is.True);

			});
		}

		[Test]
		public void Test_JsonArray_StrictEquals()
		{
			Assert.Multiple(() =>
			{
				var arr = JsonArray.ReadOnly.Empty;

				Assert.That(arr.StrictEquals(JsonArray.ReadOnly.Empty), Is.True);
				Assert.That(arr.StrictEquals(new JsonArray()), Is.True);
				Assert.That(arr.StrictEquals(JsonArray.Create([])), Is.True);

				Assert.That(arr.StrictEquals(JsonNull.Null), Is.False);
				Assert.That(arr.StrictEquals(JsonNull.Missing), Is.False);
				Assert.That(arr.StrictEquals(JsonNull.Error), Is.False);
				Assert.That(arr.StrictEquals(JsonBoolean.False), Is.False);
				Assert.That(arr.StrictEquals(JsonBoolean.True), Is.False);
				Assert.That(arr.StrictEquals(JsonArray.Create("")), Is.False);
				Assert.That(arr.StrictEquals(JsonObject.ReadOnly.Empty), Is.False);
				Assert.That(arr.StrictEquals(JsonString.Return("")), Is.False);

				Assert.That(arr.StrictEquals(default(ReadOnlySpan<JsonValue>)), Is.True);
				Assert.That(arr.StrictEquals(new JsonValue[0]), Is.True);
				Assert.That(arr.StrictEquals(new List<JsonValue>()), Is.True);
				Assert.That(arr.StrictEquals(Enumerable.Empty<JsonValue>()), Is.True);

			});
			Assert.Multiple(() =>
			{
				var arr = JsonArray.Create([ "hello", 123, true ]);

				Assert.That(arr.StrictEquals(JsonArray.Create("hello", 123, true)), Is.True);
				Assert.That(arr.StrictEquals(JsonArray.Create("hello", 123L, true)), Is.True);
				Assert.That(arr.StrictEquals(JsonArray.Create("hello", 123.0, true)), Is.True);
				Assert.That(arr.StrictEquals(JsonArray.Create("hello", 123.0f, true)), Is.True);

				Assert.That(arr.StrictEquals(JsonNull.Null), Is.False);
				Assert.That(arr.StrictEquals(JsonNull.Missing), Is.False);
				Assert.That(arr.StrictEquals(JsonNull.Error), Is.False);
				Assert.That(arr.StrictEquals(JsonBoolean.False), Is.False);
				Assert.That(arr.StrictEquals(JsonBoolean.True), Is.False);
				Assert.That(arr.StrictEquals(JsonArray.ReadOnly.Empty), Is.False);
				Assert.That(arr.StrictEquals(JsonArray.Create("world", 123, true)), Is.False);
				Assert.That(arr.StrictEquals(JsonArray.Create("hello", 456, true)), Is.False);
				Assert.That(arr.StrictEquals(JsonArray.Create("hello", 123, false)), Is.False);
				Assert.That(arr.StrictEquals(JsonArray.Create("hello", 123, true, "world")), Is.False);
				Assert.That(arr.StrictEquals(JsonArray.Create("hello", "123", true)), Is.False);
				Assert.That(arr.StrictEquals(JsonObject.Create([ ("0", "hello"), ("1", 123), ("2", true) ])), Is.False);

				Assert.That(arr.StrictEquals((ReadOnlySpan<JsonValue>) [ "hello", 123, true ]), Is.True);
				Assert.That(arr.StrictEquals((JsonValue[]) [ "hello", 123, true ]), Is.True);
				Assert.That(arr.StrictEquals((List<JsonValue>) [ "hello", 123, true ]), Is.True);
				Assert.That(arr.StrictEquals(Enumerable.Range(0, 3).Select(i => ((ReadOnlySpan<JsonValue>) [ "hello", 123, true ])[i])), Is.True);
			});
			Assert.Multiple(() =>
			{
				var arr = JsonArray.Create(JsonArray.Create(1, 2, 3), JsonArray.Create("foo", "bar", "baz"), JsonArray.Create(true, false), JsonObject.Create("hello", "there"));

				Assert.That(arr.StrictEquals(JsonArray.Create(JsonArray.Create(1, 2, 3), JsonArray.Create("foo", "bar", "baz"), JsonArray.Create(true, false), JsonObject.Create("hello", "there"))), Is.True);
				Assert.That(arr.StrictEquals(JsonArray.Create(JsonArray.Create(1.0, 2.0f, 3m), JsonArray.Create("foo", "bar", "baz"), JsonArray.Create(true, false), JsonObject.Create("hello", "there"))), Is.True);

				Assert.That(arr.StrictEquals(JsonArray.Create(JsonArray.Create("1", 2, 3), JsonArray.Create("foo", "bar", "baz"), JsonArray.Create(true, false), JsonObject.Create("hello", "there"))), Is.False);
				Assert.That(arr.StrictEquals(JsonArray.Create(JsonArray.Create(1, 2, 3), JsonArray.Create("foo", "baz", "bar"), JsonArray.Create(true, false), JsonObject.Create("hello", "there"))), Is.False);
				Assert.That(arr.StrictEquals(JsonArray.Create(JsonArray.Create(1, 2, 3), JsonArray.Create("foo", "bar", "baz"), JsonArray.Create(1, 0), JsonObject.Create("hello", "there"))), Is.False);
			});
			Assert.Multiple(() =>
			{
				Assert.That(JsonArray.Create(default(string)).StrictEquals(JsonArray.Create(JsonNull.Null)), Is.True);
				Assert.That(JsonArray.Create(JsonNull.Null).StrictEquals(JsonArray.Create(default(string))), Is.True);
				Assert.That(JsonArray.Create(default(string)).StrictEquals(JsonArray.Create(JsonNull.Missing)), Is.False);
				Assert.That(JsonArray.Create(JsonNull.Missing).StrictEquals(JsonArray.Create(default(string))), Is.False);

				Assert.That(JsonArray.Create(0).StrictEquals(JsonArray.Create(false)), Is.False);
				Assert.That(JsonArray.Create(1).StrictEquals(JsonArray.Create(true)), Is.False);
				Assert.That(JsonArray.Create(false).StrictEquals(JsonArray.Create(0)), Is.False);
				Assert.That(JsonArray.Create(true).StrictEquals(JsonArray.Create(1)), Is.False);
				Assert.That(JsonArray.Create(false).StrictEquals(JsonArray.Create(JsonNull.Null)), Is.False);

				Assert.That(JsonArray.Create(Guid.Empty).StrictEquals(JsonArray.Create(JsonNull.Null)), Is.True); // Guid.Empty <=> null
				Assert.That(JsonArray.Create(JsonNull.Null).StrictEquals(JsonArray.Create(Guid.Empty)), Is.True); // Guid.Empty <=> null
			});
		}

		#endregion

		#region Checks...

		private static void AssertIsImmutable(JsonObject? obj, [CallerArgumentExpression(nameof(obj))] string? expression = "")
		{
			Assert.That(obj, Is.Not.Null);
			Assert.That(obj!.IsReadOnly, Is.True, "Object should be immutable: " + expression);
			Assert.That(() => obj.Clear(), Throws.InvalidOperationException, expression);
			Assert.That(() => obj.Add("hello", "world"), Throws.InvalidOperationException);
			Assert.That(() => obj["hello"] = "world", Throws.InvalidOperationException, expression);
			Assert.That(() => obj.Add(KeyValuePair.Create("hello", JsonString.Return("world"))), Throws.InvalidOperationException, expression);
			Assert.That(() => obj.AddRange(new [] { KeyValuePair.Create("hello", JsonString.Return("world")) }), Throws.InvalidOperationException, expression);
			Assert.That(() => obj.TryAdd("hello", "world"), Throws.InvalidOperationException, expression);
			Assert.That(() => obj.Remove("hello"), Throws.InvalidOperationException, expression);
			Assert.That(() => obj.Remove("hello", out _), Throws.InvalidOperationException, expression);
			Assert.That(() => obj.Remove(KeyValuePair.Create("hello", JsonString.Return("world"))), Throws.InvalidOperationException, expression);
		}

		private static void AssertIsImmutable(JsonArray? arr, [CallerArgumentExpression(nameof(arr))] string? expression = "")
		{
			if (arr is null)
			{
				Assert.That(arr, Is.Not.Null);
				return;
			}

			Assert.That(arr, IsJson.ReadOnly, expression);
			Assert.That(() => arr.Clear(), Throws.InvalidOperationException, expression);
			Assert.That(() => arr[0] = "world", Throws.InvalidOperationException, expression);
			Assert.That(() => arr.Set(0, "hello"), Throws.InvalidOperationException);
			Assert.That(() => arr.Add("hello"), Throws.InvalidOperationException);
			Assert.That(() => arr.AddValue("hello"), Throws.InvalidOperationException);
			Assert.That(() => arr.AddNull(), Throws.InvalidOperationException);
			Assert.That(() => arr.AddRange(Array.Empty<JsonValue?>().AsSpan()), Throws.InvalidOperationException, expression);
			Assert.That(() => arr.AddRange(Array.Empty<JsonValue>()), Throws.InvalidOperationException, expression);
			Assert.That(() => arr.AddRange(Enumerable.Empty<JsonValue>()), Throws.InvalidOperationException, expression);
			Assert.That(() => arr.AddValues<int>(Array.Empty<int>().AsSpan()), Throws.InvalidOperationException, expression);
			Assert.That(() => arr.AddValues<int>(Array.Empty<int>()), Throws.InvalidOperationException, expression);
			Assert.That(() => arr.AddValues(Enumerable.Empty<int>()), Throws.InvalidOperationException, expression);
			Assert.That(() => arr.AddValues(Enumerable.Empty<int>(), x => x.ToString()), Throws.InvalidOperationException, expression);
			Assert.That(() => arr.Insert(0, 123), Throws.InvalidOperationException, expression);
			Assert.That(() => arr.RemoveAt(0), Throws.InvalidOperationException, expression);
			Assert.That(() => arr.Remove("helo"), Throws.InvalidOperationException, expression);
			Assert.That(() => arr.RemoveDuplicates(), Throws.InvalidOperationException, expression);
			Assert.That(() => arr.RemoveAll(_ => true), Throws.InvalidOperationException, expression);
			Assert.That(() => arr.KeepOnly(_ => true), Throws.InvalidOperationException, expression);
		}

		private static void EnsureDeepImmutabilityInvariant(JsonValue value, string? path = null)
		{
			switch (value)
			{
				case JsonObject obj:
				{
					EnsureDeepImmutabilityInvariant(obj, path);
					break;
				}
				case JsonArray arr:
				{
					EnsureDeepImmutabilityInvariant(arr, path);
					break;
				}
			}
		}

		private static void EnsureDeepImmutabilityInvariant(JsonObject obj, string? path = null)
		{
			Assert.That(obj.IsReadOnly, Is.True, $"Object at {path} should be immutable");

			foreach (var (k, v) in obj)
			{
				switch (v)
				{
					case JsonObject o:
					{
						EnsureDeepImmutabilityInvariant(o, path is not null ? (path + "." + k) : k);
						break;
					}
					case JsonArray a:
					{
						EnsureDeepImmutabilityInvariant(a, path is not null ? (path + "." + k) : k);
						break;
					}
				}
			}
		}

		private static void EnsureDeepImmutabilityInvariant(JsonArray arr, string? path = null)
		{
			Assert.That(arr.IsReadOnly, Is.True, $"Object at {path} should be immutable");

			for(int i = 0; i < arr.Count; i++)
			{
				switch (arr[i])
				{
					case JsonObject o:
					{
						EnsureDeepImmutabilityInvariant(o, path + "[" + i + "]");
						break;
					}
					case JsonArray a:
					{
						EnsureDeepImmutabilityInvariant(a, path + "[" + i + "]");
						break;
					}
				}
			}
		}

		private static void EnsureDeepMutabilityInvariant(JsonValue value, string? path = null)
		{
			switch (value)
			{
				case JsonObject obj:
				{
					EnsureDeepMutabilityInvariant(obj, path);
					break;
				}
				case JsonArray arr:
				{
					EnsureDeepMutabilityInvariant(arr, path);
					break;
				}
			}
		}

		private static void EnsureDeepMutabilityInvariant(JsonObject obj, string? path = null)
		{
			Assert.That(obj.IsReadOnly, Is.False, $"Object at {path} should be mutable");

			foreach (var (k, v) in obj)
			{
				switch (v)
				{
					case JsonObject o:
					{
						EnsureDeepMutabilityInvariant(o, path is not null ? (path + "." + k) : k);
						break;
					}
					case JsonArray a:
					{
						EnsureDeepMutabilityInvariant(a, path is not null ? (path + "." + k) : k);
						break;
					}
				}
			}
		}

		private static void EnsureDeepMutabilityInvariant(JsonArray arr, string? path = null)
		{
			Assert.That(arr.IsReadOnly, Is.False, $"Array at {path} should be mutable");

			for(int i = 0; i < arr.Count; i++)
			{
				switch (arr[i])
				{
					case JsonObject o:
					{
						EnsureDeepMutabilityInvariant(o, path + "[" + i + "]");
						break;
					}
					case JsonArray a:
					{
						EnsureDeepMutabilityInvariant(a, path + "[" + i + "]");
						break;
					}
				}
			}
		}

		#endregion

		#region JsonObject...

		[Test]
		public void Test_JsonObject()
		{
			var obj = new JsonObject();
			Assert.That(obj, Has.Count.EqualTo(0));
			Assert.That(obj.IsNull, Is.False);
			Assert.That(obj.IsDefault, Is.True);
			Assert.That(obj.ToJson(), Is.EqualTo("{ }"));
			Assert.That(SerializeToSlice(obj), Is.EqualTo(Slice.FromString("{}")));

			obj["Hello"] = "World";
			Assert.That(obj, Has.Count.EqualTo(1));
			Assert.That(obj.IsDefault, Is.False);
			Assert.That(obj.ContainsKey("Hello"), Is.True);
			Assert.That(obj["Hello"], Is.EqualTo(JsonString.Return("World")));
			Assert.That(obj.GetValue("Hello"), Is.EqualTo(JsonString.Return("World")));
			Assert.That(obj.ToJson(), Is.EqualTo("{ \"Hello\": \"World\" }"));
			Assert.That(SerializeToSlice(obj), Is.EqualTo(Slice.FromString("{\"Hello\":\"World\"}")));

			obj.Add("Foo", 123);
			Assert.That(obj, Has.Count.EqualTo(2));
			Assert.That(obj.ContainsKey("Foo"), Is.True);
			Assert.That(obj["Foo"], Is.EqualTo(JsonNumber.Return(123)));
			Assert.That(obj.GetValue("Foo"), Is.EqualTo(JsonNumber.Return(123)));
			Assert.That(obj.ToJson(), Is.EqualTo("{ \"Hello\": \"World\", \"Foo\": 123 }"));
			Assert.That(SerializeToSlice(obj), Is.EqualTo(Slice.FromString("{\"Hello\":\"World\",\"Foo\":123}")));

			obj.Set("Foo", 456);
			Assert.That(obj, Has.Count.EqualTo(2));
			Assert.That(obj.ContainsKey("Foo"), Is.True);
			Assert.That(obj["Foo"], Is.EqualTo(JsonNumber.Return(456)));
			Assert.That(obj.GetValue("Foo"), Is.EqualTo(JsonNumber.Return(456)));
			Assert.That(obj.ToJson(), Is.EqualTo("{ \"Hello\": \"World\", \"Foo\": 456 }"));
			Assert.That(SerializeToSlice(obj), Is.EqualTo(Slice.FromString("{\"Hello\":\"World\",\"Foo\":456}")));

			obj.Add("Bar", true);
			Assert.That(obj, Has.Count.EqualTo(3));
			Assert.That(obj.ContainsKey("Bar"), Is.True);
			Assert.That(obj["Bar"], Is.EqualTo(JsonBoolean.True));
			Assert.That(obj.GetValue("Bar"), Is.EqualTo(JsonBoolean.True));
			Assert.That(obj.ToJson(), Is.EqualTo("{ \"Hello\": \"World\", \"Foo\": 456, \"Bar\": true }"));
			Assert.That(SerializeToSlice(obj), Is.EqualTo(Slice.FromString("{\"Hello\":\"World\",\"Foo\":456,\"Bar\":true}")));

			// case sensitive! ('Bar' != 'BAR')
			var sub = JsonObject.Create([ ("Alpha", 111), ("Omega", 999) ]);
			obj.Add("BAR", sub);
			Assert.That(obj, Has.Count.EqualTo(4));
			Assert.That(obj.ContainsKey("BAR"), Is.True);
			Assert.That(obj["BAR"], Is.SameAs(sub));
			Assert.That(obj.GetValue("BAR"), Is.SameAs(sub));
			Assert.That(obj.ToJson(), Is.EqualTo("{ \"Hello\": \"World\", \"Foo\": 456, \"Bar\": true, \"BAR\": { \"Alpha\": 111, \"Omega\": 999 } }"));
			Assert.That(SerializeToSlice(obj), Is.EqualTo(Slice.FromString("{\"Hello\":\"World\",\"Foo\":456,\"Bar\":true,\"BAR\":{\"Alpha\":111,\"Omega\":999}}")));

			// ReadOnlySpan keys
			Assert.That(obj["Hello".AsSpan()], IsJson.EqualTo("World"));
			Assert.That(obj["NotHello".AsSpan(3)], IsJson.EqualTo("World"));

			// property names that require escaping ("..\.." => "..\\..", "\\?\..." => "\\\\?\\....")
			obj = new JsonObject();
			obj["""Hello\World"""] = 123;
			obj["""Hello"World"""] = 456;
			obj["""\\?\GLOBALROOT\Device\Foo\Bar"""] = 789;
			Assert.That(obj, Has.Count.EqualTo(3));
			Assert.That(obj.ContainsKey("""Hello\World"""), Is.True);
			Assert.That(obj.ContainsKey("""Hello"World"""), Is.True);
			Assert.That(obj.ContainsKey("""\\?\GLOBALROOT\Device\Foo\Bar"""), Is.True);
			Assert.That(obj.Get<int>("""Hello\World"""), Is.EqualTo(123));
			Assert.That(obj.Get<int>("""Hello"World"""), Is.EqualTo(456));
			Assert.That(obj.Get<int>("""\\?\GLOBALROOT\Device\Foo\Bar"""), Is.EqualTo(789));
			Assert.That(obj.ToJson(), Is.EqualTo("""{ "Hello\\World": 123, "Hello\"World": 456, "\\\\?\\GLOBALROOT\\Device\\Foo\\Bar": 789 }"""));
			Assert.That(SerializeToSlice(obj), Is.EqualTo(Slice.FromString("""{"Hello\\World":123,"Hello\"World":456,"\\\\?\\GLOBALROOT\\Device\\Foo\\Bar":789}""")));

			//note: we do not deserialize JsonNull singletons "Missing"/"Error" by default
			obj = JsonObject.Create([
				("Foo", JsonNull.Null),
				("Bar", JsonNull.Missing),
				("Baz", JsonNull.Error)
			]);
			Assert.That(obj.ToJson(), Is.EqualTo("{ \"Foo\": null }"));
			Assert.That(SerializeToSlice(obj), Is.EqualTo(Slice.FromString("{\"Foo\":null}")));
		}

		[Test]
		public void Test_JsonObject_Span_Keys()
		{
			Span<char> spanKey = [ 'H', 'e', 'l', 'l', 'o' ]; // we assume that it is allocated on the stack!
			ReadOnlyMemory<char> memKey = "Hello".AsMemory();


			{ // stackalloc'ed keys
				var obj = new JsonObject();
				obj[spanKey] = "World";
				Assert.That(obj["Hello"], IsJson.EqualTo("World"));
				Assert.That(obj[spanKey], IsJson.EqualTo("World"));
				Assert.That(obj.TryGetValue(spanKey, out var value), Is.True.WithOutput(value).EqualTo(JsonString.Return("World")));
				Assert.That(obj[memKey], IsJson.EqualTo("World"));
				Assert.That(obj.TryGetValue(memKey, out value), Is.True.WithOutput(value).EqualTo(JsonString.Return("World")));
#if NET9_0_OR_GREATER
				Assert.That(obj.TryGetValue(spanKey, out var actualKey, out value), Is.True.WithOutput(value).EqualTo(JsonString.Return("World")));
				Assert.That(actualKey, Is.InstanceOf<string>().And.EqualTo("Hello"));
				Assert.That(obj.TryGetValue(memKey, out actualKey, out value), Is.True.WithOutput(value).EqualTo(JsonString.Return("World")));
				Assert.That(actualKey, Is.InstanceOf<string>().And.EqualTo("Hello"));
#endif
			}

			{ // sliced keys
				var obj = new JsonObject();
				obj["NotHello".AsSpan(3)] = "World";
				Assert.That(obj["Hello"], IsJson.EqualTo("World"));
				Assert.That(obj["Hello".AsSpan()], IsJson.EqualTo("World"));
				Assert.That(obj["HelloNot".AsSpan(0, 5)], IsJson.EqualTo("World"));
				Assert.That(obj["Hello".AsMemory()], IsJson.EqualTo("World"));
				Assert.That(obj.TryGetValue("NotHelloNot".AsSpan(3, 5), out var value), Is.True.WithOutput(value).EqualTo(JsonString.Return("World")));
				Assert.That(obj.TryGetValue("NotHelloNot".AsMemory(3, 5), out value), Is.True.WithOutput(value).EqualTo(JsonString.Return("World")));
#if NET9_0_OR_GREATER
				Assert.That(obj.TryGetValue("NotHelloNot".AsSpan(3, 5), out var actualKey, out value), Is.True.WithOutput(value).EqualTo(JsonString.Return("World")));
				Assert.That(actualKey, Is.InstanceOf<string>().And.EqualTo("Hello"));
				Assert.That(obj.TryGetValue("NotHelloNot".AsMemory(3, 5), out actualKey, out value), Is.True.WithOutput(value).EqualTo(JsonString.Return("World")));
				Assert.That(actualKey, Is.InstanceOf<string>().And.EqualTo("Hello"));
#endif
			}

#if NET9_0_OR_GREATER
			{ // TryGetValue(span, ...) returns original key

				// create a unique key, add it to the object, and check that TryGetValue with a span will return the exact same instance (and not a copy!)
				string key = DateTime.Now.Ticks.ToString();
				var obj = new JsonObject();
				obj[key] = "World";
				Span<char> span = stackalloc char[key.Length];
				key.CopyTo(span);
				Assert.That(obj.TryGetValue(span, out var actualKey, out var value), Is.True);
				Assert.That(value, IsJson.EqualTo("World"));
				Assert.That(actualKey, Is.SameAs(key));
			}
			{ // TryGetValue(memory, ...) returns original key

				// create a unique key, add it to the object, and check that TryGetValue with a span will return the exact same instance (and not a copy!)
				string key = DateTime.Now.Ticks.ToString();
				var obj = new JsonObject();
				obj[key] = "World";
				var memKeySpliced = ("hello" + key + "world").AsMemory()[5..^5];
				Assert.That(obj.TryGetValue(memKeySpliced, out var actualKey, out var value), Is.True);
				Assert.That(value, IsJson.EqualTo("World"));
				Assert.That(actualKey, Is.SameAs(key));
			}
#endif

			{ // Set(span, JsonValue)
				var obj = new JsonObject();
				obj.Set(spanKey, "World");
				Assert.That(obj["Hello"], IsJson.EqualTo("World"));
				Assert.That(obj[spanKey], IsJson.EqualTo("World"));
				Assert.That(obj[memKey], IsJson.EqualTo("World"));
			}

			{ // Set(memory, JsonValue)
				var obj = new JsonObject();
				obj.Set(memKey, "World");
				Assert.That(obj["Hello"], IsJson.EqualTo("World"));
				Assert.That(obj[spanKey], IsJson.EqualTo("World"));
				Assert.That(obj[memKey], IsJson.EqualTo("World"));
			}

			{ // Set<T>(span, T)
				var obj = new JsonObject();
				obj.Set<string>(spanKey, "World");
				Assert.That(obj["Hello"], IsJson.EqualTo("World"));
				Assert.That(obj[spanKey], IsJson.EqualTo("World"));
				Assert.That(obj[memKey], IsJson.EqualTo("World"));
			}

			{ // Set<T>(memory, T)
				var obj = new JsonObject();
				obj.Set<string>(memKey, "World");
				Assert.That(obj["Hello"], IsJson.EqualTo("World"));
				Assert.That(obj[spanKey], IsJson.EqualTo("World"));
				Assert.That(obj[memKey], IsJson.EqualTo("World"));
			}

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
			Assert.That(obj.Get<string>("Hello", "not_found"), Is.EqualTo("World"));
			Assert.That(obj.Get<string>("XYZHello".AsSpan(3), "not_found"), Is.EqualTo("World"));

			Assert.That(obj.Get<int>("Foo"), Is.EqualTo(123));
			Assert.That(obj.Get<int>("Foo", -1), Is.EqualTo(123));
			Assert.That(obj.Get<int>("XYZFoo".AsSpan(3), -1), Is.EqualTo(123));

			Assert.That(obj.Get<bool>("Bar"), Is.True);
			Assert.That(obj.Get<bool>("Bar", false), Is.True);
			Assert.That(obj.Get<bool>("XYZBar".AsSpan(3), false), Is.True);

			Assert.That(obj.Get<double>("Baz"), Is.EqualTo(Math.PI));
			Assert.That(obj.Get<double>("Baz", double.NaN), Is.EqualTo(Math.PI));
			Assert.That(obj.Get<double>("XYZBaz".AsSpan(3), double.NaN), Is.EqualTo(Math.PI));

			// empty doit retourner default(T) pour les ValueType, càd 0/false/...
			Assert.That(obj.Get<string>("Empty"), Is.EqualTo(""), "'' -> string");
			Assert.That(obj.Get<int>("Empty"), Is.EqualTo(0), "'' -> int");
			Assert.That(obj.Get<bool>("Empty"), Is.False, "'' -> bool");
			Assert.That(obj.Get<double>("Empty"), Is.EqualTo(0.0), "'' -> double");
			Assert.That(obj.Get<Guid>("Empty"), Is.EqualTo(Guid.Empty), "'' -> Guid");
			Assert.That(obj.Get<string>("NotEmpty".AsSpan(3)), Is.EqualTo(""), "'' -> string");

			// empty doit doit retourner default(T) pour les Nullable, càd null
			Assert.That(obj.Get<int?>("Empty", null), Is.Null, "'' -> int?");
			Assert.That(obj.Get<bool?>("Empty", null), Is.Null, "'' -> bool?");
			Assert.That(obj.Get<double?>("Empty", null), Is.Null, "'' -> double?");
			Assert.That(obj.Get<Guid?>("Empty", null), Is.Null, "'' -> Guid?");

			// missing + nullable
			Assert.That(obj.Get<string?>("olleH", null), Is.Null);
			Assert.That(obj.Get<int?>("olleH", null), Is.Null);
			Assert.That(obj.Get<bool?>("olleH", null), Is.Null);
			Assert.That(obj.Get<double?>("olleH", null), Is.Null);
			Assert.That(obj.Get<string?>("olleH".AsSpan(), null), Is.Null);

			// null + nullable
			Assert.That(() => obj.Get<string>("Void"), Throws.InstanceOf<JsonBindingException>().With.Message.Contains("Void"));
			Assert.That(() => obj.Get<int>("Void"), Throws.InstanceOf<JsonBindingException>().With.Message.Contains("Void"));
			Assert.That(() => obj.Get<bool>("Void"), Throws.InstanceOf<JsonBindingException>().With.Message.Contains("Void"));
			Assert.That(() => obj.Get<double>("Void"), Throws.InstanceOf<JsonBindingException>().With.Message.Contains("Void"));
			Assert.That(obj.Get<string?>("Void", null), Is.Null);
			Assert.That(obj.Get<int?>("Void", null), Is.Null);
			Assert.That(obj.Get<bool?>("Void", null), Is.Null);
			Assert.That(obj.Get<double?>("Void", null), Is.Null);

			// missing + required: true
			Assert.That(() => obj.Get<string>("olleH"), Throws.InstanceOf<JsonBindingException>().With.Message.Contains("olleH"));
			Assert.That(() => obj.Get<int>("olleH"), Throws.InstanceOf<JsonBindingException>().With.Message.Contains("olleH"));
			Assert.That(() => obj.Get<int?>("olleH"), Throws.InstanceOf<JsonBindingException>().With.Message.Contains("olleH"));

			// null + required: true
			Assert.That(() => obj.Get<string>("Void"), Throws.InstanceOf<JsonBindingException>().With.Message.Contains("Void"));
			Assert.That(() => obj.Get<int>("Void"), Throws.InstanceOf<JsonBindingException>().With.Message.Contains("Void"));
			Assert.That(() => obj.Get<int?>("Void"), Throws.InstanceOf<JsonBindingException>().With.Message.Contains("Void"));
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
			Assert.That(obj.Get<string?>("Missing", null), Is.Null);
			Assert.That(() => obj.Get<string>("Missing"), Throws.InstanceOf<JsonBindingException>());
			Assert.That(obj.Get<string?>("Void", null), Is.Null);
			Assert.That(() => obj.Get<string>("Void"), Throws.InstanceOf<JsonBindingException>());
			Assert.That(obj.Get<string>("Empty"), Is.EqualTo(""));
			Assert.That(obj.Get<string>("Space"), Is.EqualTo("   "));
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

			value = obj.GetPathValueOrDefault("Hello", null);
			DumpCompact(value);
			Assert.That(value.Type, Is.EqualTo(JsonType.String));
			Assert.That(value.Required<string>(), Is.EqualTo("World"));
			Assert.That(obj.GetPath<string>("Hello"), Is.EqualTo("World"));

			value = obj.GetPathValueOrDefault("Coords", null);
			DumpCompact(value);
			Assert.That(value.Type, Is.EqualTo(JsonType.Object));

			value = obj.GetPathValueOrDefault("Values", null);
			DumpCompact(value);
			Assert.That(value.Type, Is.EqualTo(JsonType.Array));

			value = obj.GetPathValueOrDefault("NotFound", null);
			DumpCompact(value);
			Assert.That(value.Type, Is.EqualTo(JsonType.Null));
			Assert.That(value, Is.EqualTo(JsonNull.Missing));

			// Children

			Assert.That(obj.GetPath<int>("Coords.X"), Is.EqualTo(1));
			Assert.That(obj.GetPath<int>("Coords.Y"), Is.EqualTo(2));
			Assert.That(obj.GetPath<int>("Coords.Z"), Is.EqualTo(3));
			Assert.That(() => obj.GetPath<int>("Coords.NotFound"), Throws.InstanceOf<JsonBindingException>());
			Assert.That(obj.GetPath<int>("XYZ.Coords.Z".AsMemory(4)), Is.EqualTo(3));

			Assert.That(obj.GetPath<int?>("Foo.Bar.Baz", null), Is.EqualTo(123));
			Assert.That(obj.GetPath<int?>("Foo.Bar.NotFound", null), Is.Null);
			Assert.That(obj.GetPath<int?>("Foo.NotFound.Baz", null), Is.Null);
			Assert.That(obj.GetPath<int?>("NotFound.Bar.Baz", null), Is.Null);
			Assert.That(obj.GetPath<int?>("Foo.Bar.Baz.XYZ".AsMemory()[..^4], null), Is.EqualTo(123));

			// Array Indexing

			Assert.That(obj.GetPath<string>("Values[0]"), Is.EqualTo("a"));
			Assert.That(obj.GetPath<string>("Values[1]"), Is.EqualTo("b"));
			Assert.That(obj.GetPath<string>("Values[2]"), Is.EqualTo("c"));
			Assert.That(() => obj.GetPath<string>("Values[3]"), Throws.InstanceOf<JsonBindingException>());
			Assert.That(() => obj.GetPath<string>("Values[2].NotFound"), Throws.InstanceOf<JsonBindingException>());

			Assert.That(obj.GetPath<string?>("Items[0].Value", null), Is.EqualTo("one"));
			Assert.That(obj.GetPath<string?>("Items[1].Value", null), Is.EqualTo("two"));
			Assert.That(obj.GetPath<string?>("Items[2].Value", null), Is.EqualTo("three"));
			Assert.That(obj.GetPath<string?>("Items[0].NotFound", null), Is.Null);
			Assert.That(obj.GetPath<string?>("Items[3]", null), Is.Null);
			Assert.That(obj.GetPath<string?>("Items[3].Value", null), Is.Null);

			// Required
			Assert.That(() => obj.GetPath<int>("NotFound.Bar.Baz"), Throws.InstanceOf<JsonBindingException>());
			Assert.That(() => obj.GetPath<int>("Coords.NotFound"), Throws.InstanceOf<JsonBindingException>());

			obj = new JsonObject
			{
				["X"] = default(string),
				["Y"] = default(Guid?),
				["Z"] = JsonNull.Missing
			};
			Assert.That(() => obj.GetPath<string>("X"), Throws.InstanceOf<JsonBindingException>());
			Assert.That(() => obj.GetPath<Guid>("Y"), Throws.InstanceOf<JsonBindingException>());
			Assert.That(() => obj.GetPath<string>("Z"), Throws.InstanceOf<JsonBindingException>());
			Assert.That(() => obj.GetPath<string>("間"), Throws.InstanceOf<JsonBindingException>());
		}

		[Test]
		public void Test_JsonObject_GetPath_JsonPath()
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

			value = obj.GetPathValueOrDefault(JsonPath.Create("Hello"), null);
			DumpCompact(value);
			Assert.That(value.Type, Is.EqualTo(JsonType.String));
			Assert.That(value.Required<string>(), Is.EqualTo("World"));
			Assert.That(obj.GetPath<string>("Hello"), Is.EqualTo("World"));

			value = obj.GetPathValueOrDefault(JsonPath.Create("Coords"), null);
			DumpCompact(value);
			Assert.That(value.Type, Is.EqualTo(JsonType.Object));

			value = obj.GetPathValueOrDefault(JsonPath.Create("Values"), null);
			DumpCompact(value);
			Assert.That(value.Type, Is.EqualTo(JsonType.Array));

			value = obj.GetPathValueOrDefault(JsonPath.Create("NotFound"), null);
			DumpCompact(value);
			Assert.That(value.Type, Is.EqualTo(JsonType.Null));
			Assert.That(value, Is.EqualTo(JsonNull.Missing));

			// Children

			Assert.That(obj.GetPath<int>(JsonPath.Create("Coords.X")), Is.EqualTo(1));
			Assert.That(obj.GetPath<int>(JsonPath.Create("Coords.Y")), Is.EqualTo(2));
			Assert.That(obj.GetPath<int>(JsonPath.Create("Coords.Z")), Is.EqualTo(3));
			Assert.That(() => obj.GetPath<int>(JsonPath.Create("Coords.NotFound")), Throws.InstanceOf<JsonBindingException>());

			Assert.That(obj.GetPath<int?>(JsonPath.Create("Foo.Bar.Baz"), null), Is.EqualTo(123));
			Assert.That(obj.GetPath<int?>(JsonPath.Create("Foo.Bar.NotFound"), null), Is.Null);
			Assert.That(obj.GetPath<int?>(JsonPath.Create("Foo.NotFound.Baz"), null), Is.Null);
			Assert.That(obj.GetPath<int?>(JsonPath.Create("NotFound.Bar.Baz"), null), Is.Null);

			// Array Indexing

			Assert.That(obj.GetPath<string>(JsonPath.Create("Values[0]")), Is.EqualTo("a"));
			Assert.That(obj.GetPath<string>(JsonPath.Create("Values[1]")), Is.EqualTo("b"));
			Assert.That(obj.GetPath<string>(JsonPath.Create("Values[2]")), Is.EqualTo("c"));
			Assert.That(() => obj.GetPath<string>(JsonPath.Create("Values[3]")), Throws.InstanceOf<JsonBindingException>());
			Assert.That(() => obj.GetPath<string>(JsonPath.Create("Values[2].NotFound")), Throws.InstanceOf<JsonBindingException>());

			Assert.That(obj.GetPath<string?>(JsonPath.Create("Items[0].Value"), null), Is.EqualTo("one"));
			Assert.That(obj.GetPath<string?>(JsonPath.Create("Items[1].Value"), null), Is.EqualTo("two"));
			Assert.That(obj.GetPath<string?>(JsonPath.Create("Items[2].Value"), null), Is.EqualTo("three"));
			Assert.That(obj.GetPath<string?>(JsonPath.Create("Items[0].NotFound"), null), Is.Null);
			Assert.That(obj.GetPath<string?>(JsonPath.Create("Items[3]"), null), Is.Null);
			Assert.That(obj.GetPath<string?>(JsonPath.Create("Items[3].Value"), null), Is.Null);

			// Required
			Assert.That(() => obj.GetPath<int>(JsonPath.Create("NotFound.Bar.Baz")), Throws.InstanceOf<JsonBindingException>());
			Assert.That(() => obj.GetPath<int>(JsonPath.Create("Coords.NotFound")), Throws.InstanceOf<JsonBindingException>());

			obj = new JsonObject
			{
				["X"] = default(string),
				["Y"] = default(Guid?),
				["Z"] = JsonNull.Missing
			};
			Assert.That(() => obj.GetPath<string>(JsonPath.Create("X")), Throws.InstanceOf<JsonBindingException>());
			Assert.That(() => obj.GetPath<Guid>(JsonPath.Create("Y")), Throws.InstanceOf<JsonBindingException>());
			Assert.That(() => obj.GetPath<string>(JsonPath.Create("Z")), Throws.InstanceOf<JsonBindingException>());
			Assert.That(() => obj.GetPath<string>(JsonPath.Create("間")), Throws.InstanceOf<JsonBindingException>());

			// test that we can access field names that require espacing
			obj = new JsonObject
			{
				["foo.bar"] = "baz",
				["[42]"] = 42,
				[@"\o/"] = "Hello, there!",
			};
			// incorrectly escaped in the path
			Assert.That(obj.GetPathValueOrDefault("foo.bar"), IsJson.Null);
			Assert.That(obj.GetPathValueOrDefault("[42]"), IsJson.Null);
			Assert.That(obj.GetPathValueOrDefault(@"\o/"), IsJson.Null);
			// properly escaped in the path
			Assert.That(obj.GetPathValueOrDefault(@"foo\.bar"), IsJson.EqualTo("baz"));
			Assert.That(obj.GetPathValueOrDefault(@"\[42\]"), IsJson.EqualTo(42));
			Assert.That(obj.GetPathValueOrDefault(@"\\o/"), IsJson.EqualTo("Hello, there!"));
		}

		[Test]
		public void Test_JsonObject_SetPath()
		{
			var obj = JsonObject.Create();

			// create
			obj.SetPath("Hello", "World");
			DumpCompact(obj);
			Assert.That(obj, Has.Count.EqualTo(1));
			Assert.That(obj, Is.EqualTo(JsonValue.Parse("""{ "Hello": "World" }""")));
			Assert.That(obj.GetPath<string>("Hello"), Is.EqualTo("World"));

			// update
			obj.SetPath("Hello", "Le Monde!");
			DumpCompact(obj);
			Assert.That(obj, Has.Count.EqualTo(1));
			Assert.That(obj, Is.EqualTo(JsonValue.Parse("""{ "Hello": "Le Monde!" }""")));
			Assert.That(obj.GetPath<string>("Hello"), Is.EqualTo("Le Monde!"));

			// add other
			obj.SetPath("Level", 9001);
			DumpCompact(obj);
			Assert.That(obj, Has.Count.EqualTo(2));
			Assert.That(obj, Is.EqualTo(JsonValue.Parse("""{ "Hello": "Le Monde!", "Level": 9001 }""")));
			Assert.That(obj.GetPath<int>("Level"), Is.EqualTo(9001));

			// null => JsonNull.Null
			obj.SetPath("Hello", null);
			DumpCompact(obj);
			Assert.That(obj, Has.Count.EqualTo(2));
			Assert.That(obj, Is.EqualTo(JsonValue.Parse("""{ "Hello": null, "Level": 9001 }""")));
			Assert.That(() => obj.GetPath<string>("Hello"), Throws.InstanceOf<JsonBindingException>());
			Assert.That(obj.GetPath<string?>("Hello", null), Is.Null);

			// remove
			obj.RemovePath("Hello");
			DumpCompact(obj);
			Assert.That(obj, Has.Count.EqualTo(1));
			Assert.That(obj, Is.EqualTo(JsonValue.Parse("""{ "Level": 9001 }""")));
			Assert.That(() => obj.GetPath<string>("Hello"), Throws.InstanceOf<JsonBindingException>());
			Assert.That(obj.GetPath<string?>("Hello", null), Is.Null);

			obj.RemovePath("Level");
			DumpCompact(obj);
			Assert.That(obj, Has.Count.EqualTo(0));
			Assert.That(() => obj.GetPath<int>("Level"), Throws.InstanceOf<JsonBindingException>());
			Assert.That(obj.GetPath<int?>("Level", null), Is.Null);
		}

		[Test]
		public void Test_JsonObject_SetPath_SubObject()
		{
			var obj = JsonObject.Create();
			obj.SetPath("Foo.Bar", 123);
			DumpCompact(obj);
			Assert.That(obj, Is.EqualTo(JsonValue.Parse("""{ "Foo": { "Bar": 123 } }""")));
			Assert.That(obj.GetPath<int>("Foo.Bar"), Is.EqualTo(123));

			obj.SetPath("Foo.Baz", 456);
			DumpCompact(obj);
			Assert.That(obj, Is.EqualTo(JsonValue.Parse("""{ "Foo": { "Bar": 123, "Baz": 456 } }""")));
			Assert.That(obj.GetPath<int>("Foo.Baz"), Is.EqualTo(456));

			obj.RemovePath("Foo.Bar");
			DumpCompact(obj);
			Assert.That(obj, Is.EqualTo(JsonValue.Parse("""{ "Foo": { "Baz": 456 } }""")));
			Assert.That(obj.GetPath<int?>("Foo.Bar", null), Is.Null);

			obj.RemovePath("Foo");
			DumpCompact(obj);
			Assert.That(obj, Has.Count.EqualTo(0));
		}

		[Test]
		public void Test_JsonObject_SetPath_SubArray()
		{
			var obj = JsonObject.Create();

			obj.SetPath("Foos[0]", 123);
			DumpCompact(obj);
			Assert.That(obj, Is.EqualTo(JsonValue.Parse("""{ "Foos": [ 123 ] }""")));
			Assert.That(obj.GetPath<int?>("Foos[0]", null), Is.EqualTo(123));

			obj.SetPath("Foos[1]", 456);
			DumpCompact(obj);
			Assert.That(obj, Is.EqualTo(JsonValue.Parse("""{ "Foos": [ 123, 456 ] }""")));
			Assert.That(obj.GetPath<int?>("Foos[1]", null), Is.EqualTo(456));

			obj.SetPath("Foos[3]", 789); //skip one
			DumpCompact(obj);
			Assert.That(obj, Is.EqualTo(JsonValue.Parse("""{ "Foos": [ 123, 456, null, 789 ] }""")));
			Assert.That(obj.GetPath<int?>("Foos[2]", null), Is.Null);
			Assert.That(obj.GetPath<int?>("Foos[3]", null), Is.EqualTo(789));
		}

		[Test]
		public void Test_JsonObject_SetPath_SubArray_Of_Objects()
		{
			var obj = JsonObject.Create();

			obj.SetPath("Foos[0]", JsonObject.Create([ ("X", 1), ("Y", 2), ("Z", 3) ]));
			DumpCompact(obj);
			Assert.That(obj, IsJson.EqualTo(JsonValue.Parse("""{ "Foos" : [ { "X": 1, "Y": 2, "Z": 3 } ] }""")));
			Assert.That(obj.GetPathValueOrDefault("Foos[0]"), IsJson.EqualTo(JsonValue.Parse("""{ "X": 1, "Y": 2, "Z": 3 }""")));

			obj.SetPath("Foos[2]", JsonObject.Create([ ("X", 4), ("Y", 5), ("Z", 6) ]));
			DumpCompact(obj);
			Assert.That(obj, Is.EqualTo(JsonValue.Parse("""{ "Foos" : [ { "X": 1, "Y": 2, "Z": 3 }, null, { "X": 4, "Y": 5, "Z": 6 } ] }""")));
			Assert.That(obj.GetPathValueOrDefault("Foos[1]"), IsJson.ExplicitNull);
			Assert.That(obj.GetPathValueOrDefault("Foos[2]"), IsJson.EqualTo(JsonValue.Parse("""{ "X": 4, "Y": 5, "Z": 6 }""")));

			// auto-created
			obj = JsonObject.Create();
			obj.SetPath("Foos[0].X", 1);
			obj.SetPath("Foos[0].Y", 2);
			obj.SetPath("Foos[0].Z", 3);
			DumpCompact(obj);
			Assert.That(obj, IsJson.EqualTo(JsonValue.Parse("""{ "Foos" : [ { "X": 1, "Y": 2, "Z": 3 } ] }""")));
			Assert.That(obj.GetPath<int?>("Foos[0].X", null), Is.EqualTo(1));
			Assert.That(obj.GetPath<int?>("Foos[0].Y", null), Is.EqualTo(2));
			Assert.That(obj.GetPath<int?>("Foos[0].Z", null), Is.EqualTo(3));

			obj.SetPath("Foos[2].X", 4);
			obj.SetPath("Foos[2].Y", 5);
			obj.SetPath("Foos[2].Z", 6);
			DumpCompact(obj);
			Assert.That(obj, IsJson.EqualTo(JsonValue.Parse("""{ "Foos" : [ { "X": 1, "Y": 2, "Z": 3 }, null, { "X": 4, "Y": 5, "Z": 6 } ] }""")));
			Assert.That(obj.GetPath<int?>("Foos[2].X", null), Is.EqualTo(4));
			Assert.That(obj.GetPath<int?>("Foos[2].Y", null), Is.EqualTo(5));
			Assert.That(obj.GetPath<int?>("Foos[2].Z", null), Is.EqualTo(6));
		}

		[Test]
		public void Test_JsonObject_SetPath_SubArray_Of_Arrays()
		{
			{
				var obj = JsonObject.Create();
				obj.SetPath("Matrix[0][2]", 1);
				obj.SetPath("Matrix[1][1]", 2);
				obj.SetPath("Matrix[2][0]", 3);
				DumpCompact(obj);
				Assert.That(obj, Is.EqualTo(JsonValue.Parse("""{ "Matrix" : [ [ null, null, 1 ], [ null, 2 ], [ 3 ] ] }""")));
				Assert.That(obj.GetPath<int?>("Matrix[0][2]"), Is.EqualTo(1));
				Assert.That(obj.GetPath<int?>("Matrix[1][1]"), Is.EqualTo(2));
				Assert.That(obj.GetPath<int?>("Matrix[2][0]"), Is.EqualTo(3));

				obj.SetPath("Matrix[0][0]", 4);
				obj.SetPath("Matrix[2][1]", 5);
				DumpCompact(obj);
				Assert.That(obj, Is.EqualTo(JsonValue.Parse("""{ "Matrix" : [ [ 4, null, 1 ], [ null, 2 ], [ 3, 5 ] ] }""")));
				Assert.That(obj.GetPath<int?>("Matrix[0][0]"), Is.EqualTo(4));
				Assert.That(obj.GetPath<int?>("Matrix[2][1]"), Is.EqualTo(5));
			}

			{
				var obj = JsonObject.Create();
				obj.SetPath("Foos[0][2].Bar", 123);
				DumpCompact(obj);
				Assert.That(obj, Is.EqualTo(JsonValue.Parse("""{ "Foos" : [ [ null, null, { "Bar": 123 } ] ] }""")));
				Assert.That(obj.GetPath<int>("Foos[0][2].Bar"), Is.EqualTo(123));
				obj.SetPath("Foos[0][2].Bar", 456);
				DumpCompact(obj);
				Assert.That(obj, Is.EqualTo(JsonValue.Parse("""{ "Foos" : [ [ null, null, { "Bar": 456 } ] ] }""")));
				Assert.That(obj.GetPath<int>("Foos[0][2].Bar"), Is.EqualTo(456));
			}

			{
				var obj = JsonObject.Create();
				obj.SetPath("Foos[0].Bar[2]", 123);
				DumpCompact(obj);
				Assert.That(obj, Is.EqualTo(JsonValue.Parse("""{ "Foos" : [ { "Bar": [ null, null, 123 ] } ] }""")));
				Assert.That(obj.GetPath<int>("Foos[0].Bar[2]"), Is.EqualTo(123));
			}
		}

		[Test]
		public void Test_JsonObject_GetOrCreateObject()
		{
			var root = JsonObject.Create();
			var foo = root.GetOrCreateObject("Foo");
			Assert.That(foo, Is.Not.Null, "Foo");
			Assert.That(root, Is.EqualTo(JsonValue.Parse("""{ "Foo": {} }""")));

			root.GetOrCreateObject("Bar").Set("Baz", 123);
			Assert.That(root, Is.EqualTo(JsonValue.Parse("""{ "Foo": {}, "Bar": { "Baz": 123 } }""")));

			root.GetOrCreateObject("Bar").Set("Hello", "World");
			Assert.That(root, Is.EqualTo(JsonValue.Parse("""{ "Foo": {}, "Bar": {"Baz":123, "Hello": "World" } }""")));

			root = JsonObject.Create();
			root.GetOrCreateObject("Narf.Zort.Poit").Set("MDR", "LOL");
			Assert.That(root, Is.EqualTo(JsonValue.Parse("""{ "Narf": { "Zort": { "Poit": { "MDR": "LOL" } } } }""")));

			// on doit pouvoir écraser un null
			root = JsonObject.Create();
			root["Bar"] = JsonNull.Null;
			var bar = root.GetOrCreateObject("Bar");
			Assert.That(bar, Is.Not.Null, "Bar");
			bar.Set("Hello", "World");
			Assert.That(root, Is.EqualTo(JsonValue.Parse("""{ "Bar": { "Hello": "World" } }""")));

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
			static void Merge(JsonObject root, JsonObject obj)
			{
				Log($"  {root}");
				Log($"+ {obj}");
				root.MergeWith(obj);
				Log($"= {root}");
				Log();
			}

			{ // Add a new field
				// { Foo: 123 } u { Bar: 456 } => { Foo: 123, Bar: 456 }
				var root = new JsonObject { ["Foo"] = 123 };
				var obj = new JsonObject { ["Bar"] = 456 };
				Merge(root, obj);
				Assert.That(root.ToJsonCompact(), Is.EqualTo("""{"Foo":123,"Bar":456}"""));
			}

			{ // Overwrite an existing field
				// { Foo: 123 } u { Foo: 456 } => { Foo: 456 }
				var root = new JsonObject { ["Foo"] = 123 };
				var obj = new JsonObject { ["Foo"] = 456 };
				Merge(root, obj);
				Assert.That(root.ToJsonCompact(), Is.EqualTo("""{"Foo":456}"""));
			}

			{ // Merging a field with Null will remove it, if keepNull == false
				// { Foo: { Bar: 42 } } u { Foo: null } => { }
				var root = new JsonObject { ["Foo"] = new JsonObject { ["Bar"] = 42 } };
				var obj = new JsonObject { ["Foo"] = JsonNull.Null };
				Merge(root, obj);
				Assert.That(root, IsJson.Empty);
			}
			{ // Merging a field with Null will set it to null, if keepNull == true
				// { Foo: { Bar: 42 } } u { Foo: null } => { Foo: null }
				var root = new JsonObject { ["Foo"] = new JsonObject { ["Bar"] = 42 } };
				var obj = new JsonObject { ["Foo"] = JsonNull.Null };
				root.MergeWith(obj, keepNull: true);
				Assert.That(root.ToJsonCompact(), Is.EqualTo("""{"Foo":null}"""));
			}
			{ // Merging a field with Missing will remove it, even if keepNull == true
				// { Foo: { Bar: 42 } } u { Foo: missing } => { }
				var root = new JsonObject { ["Foo"] = new JsonObject { ["Bar"] = 42 } };
				var obj = new JsonObject { ["Foo"] = JsonNull.Missing };
				root.MergeWith(obj, keepNull: true); // should have no effect!
				Assert.That(root, IsJson.Empty);
			}

			{ // Merge the contents of a child object
				// { Foo: { Bar: 123 } } u  { Foo: { Baz: 456 } } => { Foo: { Bar: 123, Baz: 456 } }
				var root = new JsonObject { ["Foo"] = new JsonObject { ["Bar"] = 123 } };
				var obj = new JsonObject { ["Foo"] = new JsonObject { ["Baz"] = 456 } };
				Merge(root, obj);
				Assert.That(root, IsJson.EqualTo(new JsonObject { ["Foo"] = new JsonObject { ["Bar"] = 123, ["Baz"] = 456 } }));
			}
			{
				var root = new JsonObject { ["Foo"] = new JsonObject { ["Bar"] = 123, ["Baz"] = 456 } };
				var obj = new JsonObject { ["Foo"] = new JsonObject { ["Bar"] = null, ["Jazz"] = 789 } };
				Merge(root, obj);
				Assert.That(root, IsJson.EqualTo(new JsonObject { ["Foo"] = new JsonObject { ["Baz"] = 456, ["Jazz"] = 789 } }));
			}

			{ // Merge the contents of two child arrays
				var root = new JsonObject { ["Foos"] = JsonArray.Create([
					new JsonObject() { ["x"] = 1, ["y"] = 0, ["z"] = 0 },
					new JsonObject() { ["x"] = 0, ["y"] = 1, ["z"] = 0 },
				])};
				var obj = new JsonObject { ["Foos"] = JsonArray.Create([
					JsonObject.ReadOnly.Empty, // do no change
					new JsonObject() { ["y"] = -1, ["z"] = 1 }, // set y to -1, add z
					new JsonObject() { ["x"] = 0, ["y"] = 1, ["z"] = 0 }, // add new point
				]) };
				Merge(root, obj);
				Assert.That(root, IsJson.EqualTo(new JsonObject { ["Foos"] = JsonArray.Create([
					new JsonObject() { ["x"] = 1, ["y"] = 0, ["z"] = 0 },
					new JsonObject() { ["x"] = 0, ["y"] = -1, ["z"] = 1 },
					new JsonObject() { ["x"] = 0, ["y"] = 1, ["z"] = 0 },
				]) }));
			}

			{ // truncate the last elements of a child array
				var root = new JsonObject { ["Foos"] = JsonArray.Create(1, 2, 3, 4, 5) };
				var obj = new JsonObject { ["Foos"] = JsonArray.Create(1, null, -3, null, null) };
				Merge(root, obj);
				Assert.That(root, IsJson.EqualTo(new JsonObject { ["Foos"] = JsonArray.Create(1, null, -3) }));
			}
		}

		[Test]
		public void Test_JsonObject_Diff()
		{
			static void Verify(JsonObject a, JsonObject b, JsonObject expected)
			{
				Log($"before: {a}");
				Log($"after : {b}");
				var patch = a.ComputePatch(b);
				Log($"patch : {patch}");

				Assert.That(patch, Is.EqualTo(expected), "Patch does not match expected value");

				// applying the diff on 'a' should produce 'b'!
				var b2 = a.Copy();
				b2.ApplyPatch(patch);
				Log("merged: " + b2);
				Assert.That(b2, IsJson.EqualTo(b));

				Log();
			}

			{ // no differences
				var a = new JsonObject()
				{
					["Foo"] = 123,
					["Bar"] = 456
				};
				var b = new JsonObject()
				{
					["Foo"] = 123,
					["Bar"] = 456
				};
				Verify(a, b, JsonObject.ReadOnly.Empty);
			}
			{ // field added
				var a = new JsonObject()
				{
					["Foo"] = 123
				};
				var b = new JsonObject()
				{
					["Foo"] = 123,
					["Bar"] = 456
				};
				Verify(a, b, new JsonObject { ["Bar"] = 456 });
			}
			{ // field changed
				var a = new JsonObject()
				{
					["Foo"] = 123
				};
				var b = new JsonObject()
				{
					["Foo"] = 456
				};
				Verify(a, b, new JsonObject
				{
					["Foo"] = 456
				});
			}
			{ // field removed
				var a = new JsonObject()
				{
					["Foo"] = 123,
					["Bar"] = 456
				};
				var b = new JsonObject()
				{
					["Bar"] = 456
				};
				Verify(a, b, new JsonObject
				{
					["Foo"] = null
				});
			}
			{ // child objects changed
				var a = new JsonObject()
				{
					["A"] = JsonObject.Create([ ("x", 1), ("y", 0), ("z", 0) ]),
					["B"] = JsonObject.Create([ ("x", 0), ("y", 1), ("z", 0) ]),
					["C"] = JsonObject.Create([ ("x", 0), ("y", 0), ("z", 1) ]),
				};
				var b = new JsonObject()
				{
					["A"] = JsonObject.Create([ ("x", 1), ("y",  0), ("z",  0) ]),
					["B"] = JsonObject.Create([ ("x", 0), ("y", -1), ("z",  0) ]),
					["D"] = JsonObject.Create([ ("x",-1), ("y", -1), ("z", -1) ]),
				};
				Verify(a, b, new JsonObject
				{
					["B"] = JsonObject.Create([ ("y", -1) ]),
					["C"] = null,
					["D"] = JsonObject.Create([ ("x", -1), ("y", -1), ("z", -1) ]),
				});
			}
			{ // child arrays added
				var a = JsonObject.Create();
				var b = JsonObject.Create(
				[
					("Foo", JsonArray.Create(1, 2, 3, 4, 5))
				]);
				Verify(a, b, JsonObject.Create(
				[
					("Foo", JsonArray.Create(1, 2, 3, 4, 5))
				]));
			}
			{ // child arrays added (was empty before)
				var a = JsonObject.Create(
				[
					("Foo", JsonArray.ReadOnly.Empty)
				]);
				var b = JsonObject.Create(
				[
					("Foo", JsonArray.Create(1, 2, 3, 4, 5))
				]);
				Verify(a, b, JsonObject.Create(
				[
					("Foo", JsonArray.Create(1, 2, 3, 4, 5))
				]));
			}
			{ // child arrays cleared
				var a = new JsonObject
				{
					["Foo"] = JsonArray.Create(1, 2, 3, 4, 5)
				};
				var b = new JsonObject
				{
					["Foo"] = JsonArray.ReadOnly.Empty
				};
				Verify(a, b, new JsonObject
				{
					["Foo"] = JsonArray.ReadOnly.Empty
				});
			}
			{ // child arrays changed (only literals, with items added)
				var a = new JsonObject
				{
					["Foo"] = JsonArray.Create(1, 2, 3)
				};
				var b = new JsonObject
				{
					["Foo"] = JsonArray.Create(1, -2, 3, 4, 5)
				};
				Verify(a, b, new JsonObject
				{
					["Foo"] = new JsonObject
					{
						["__patch"] = 5,
						["1"] = -2,
						["3"] = 4,
						["4"] = 5,
					}
				});
			}
			{ // child arrays changed (only literals, same size, same tail)
				var a = new JsonObject
				{
					["Foo"] = JsonArray.Create(1, 2, 3, 4, 5)
				};
				var b = new JsonObject
				{
					["Foo"] = JsonArray.Create(1, -2, 3, 4, 5)
				};
				Verify(a, b, new JsonObject
				{
					["Foo"] = new JsonObject()
					{
						["__patch"] = 5,
						["1"] = -2,
					}
				});
			}
			{ // child arrays changed (only literals, items removed at the end)
				var a = new JsonObject
				{
					["Foo"] = JsonArray.Create(1, 2, 3, 4, 5)
				};
				var b = new JsonObject
				{
					["Foo"] = JsonArray.Create(1, 2, 3)
				};
				Verify(a, b, new JsonObject
				{
					["Foo"] = new JsonObject()
					{
						["__patch"] = 3,
					}
				});
			}
			{ // child arrays changed (with sub-objects)
				var a = new JsonObject
				{
					["Foo"] = JsonArray.Create([
						JsonObject.Create([ ("x", 1), ("y", 0), ("z", 0) ]),
						JsonObject.Create([ ("x", 0), ("y", 1), ("z", 0) ]),
						JsonObject.Create([ ("x", 0), ("y", 0), ("z", 1) ]),
					])
				};
				var b = new JsonObject
				{ 
					["Foo"] = JsonArray.Create([
						JsonObject.Create([ ("x",  1), ("y",  0), ("z",  0) ]), // unchanged
						JsonObject.Create([ ("x",  0), ("y", -1), ("z",  0) ]), // y changed
						JsonObject.Create([ ("x",  0), ("y",  0) ]),            // z removed
						JsonObject.Create([ ("x", -1), ("y", -1), ("z", -1) ]), // added
					])
				};
				Verify(a, b, new JsonObject
				{
					["Foo"] = new JsonObject()
					{
						["__patch"] = 4,
						["1"] = JsonObject.Create([ ("y", -1) ]), // y changed
						["2"] = JsonObject.Create([ ("z", null) ]), // z removed
						["3"] = JsonObject.Create([ ("x", -1), ("y", -1), ("z", -1) ]), // added
					}
				});
			}
		}

		[Test]
		public void Test_JsonObject_GetOrCreateArray()
		{
			var root = JsonObject.Create();

			var foo = root.GetOrCreateArray("Foo");
			Assert.That(foo, Is.Not.Null, "Foo");
			Assert.That(foo, Has.Count.EqualTo(0), "foo.Count");
			Assert.That(root.ToJson(CrystalJsonSettings.JsonCompact), Is.EqualTo("""{"Foo":[]}"""));

			foo.AddValue(123);
			Assert.That(root.ToJson(CrystalJsonSettings.JsonCompact), Is.EqualTo("""{"Foo":[123]}"""));

			root.GetOrCreateArray("Foo").AddValue(456);
			Assert.That(root.ToJson(CrystalJsonSettings.JsonCompact), Is.EqualTo("""{"Foo":[123,456]}"""));

			root = JsonObject.Create();
			root.GetOrCreateArray("Narf.Zort.Poit").AddValue(789);
			Assert.That(root.ToJson(CrystalJsonSettings.JsonCompact), Is.EqualTo("""{"Narf":{"Zort":{"Poit":[789]}}}"""));

			// on doit pouvoir écraser un null
			root = JsonObject.Create();
			root["Bar"] = JsonNull.Null;
			var bar = root.GetOrCreateArray("Bar");
			Assert.That(bar, Is.Not.Null, "Bar");
			bar.AddValue("Hello");
			bar.AddValue("World");
			Assert.That(root.ToJson(CrystalJsonSettings.JsonCompact), Is.EqualTo("""{"Bar":["Hello","World"]}"""));

			// par contre on doit pas pouvoir écraser un non-object
			root = JsonObject.Create();
			root.Set("Baz", "Hello");
			Assert.That(
				() => root.GetOrCreateArray("Baz"),
				Throws.InstanceOf<InvalidOperationException>().With.Message.EqualTo("The specified key 'Baz' exists, but is of type String instead of expected Array"),
				"Expected error message (can change!)"
			);
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

			p = obj.Pick([ "Id", "Name" ]);
			Assert.That(p, Is.Not.Null.And.Not.SameAs(obj));
			DumpCompact(p);
			Assert.That(p["Id"], IsJson.EqualTo(1));
			Assert.That(p["Name"], IsJson.EqualTo("Walter White"));
			Assert.That(p, Has.Count.EqualTo(2));
			Assert.That(p, IsJson.Mutable);
			// the original should not be changed
			Assert.That(obj, Has.Count.EqualTo(5));

			p = obj.Pick([ "Id" ]);
			Assert.That(p, Is.Not.Null.And.Not.SameAs(obj));
			DumpCompact(p);
			Assert.That(p["Id"], IsJson.EqualTo(1));
			Assert.That(p, Has.Count.EqualTo(1));

			p = obj.Pick([ "Id", "NotFound" ]);
			Assert.That(p, Is.Not.Null.And.Not.SameAs(obj));
			DumpCompact(p);
			Assert.That(p["Id"], IsJson.EqualTo(1));
			Assert.That(p.ContainsKey("NotFound"), Is.False);
			Assert.That(p, Has.Count.EqualTo(1));

			p = obj.Pick([ "Id", "NotFound" ], keepMissing: true);
			Assert.That(p, Is.Not.Null.And.Not.SameAs(obj));
			DumpCompact(p);
			Assert.That(p["Id"], IsJson.EqualTo(1));
			Assert.That(p.ContainsKey("NotFound"), Is.True);
			Assert.That(p["NotFound"], Is.EqualTo(JsonNull.Missing));
			Assert.That(p, Has.Count.EqualTo(2));

			p = obj.Pick([ "NotFound" ]);
			Assert.That(p, Is.Not.Null.And.Not.SameAs(obj));
			DumpCompact(p);
			Assert.That(p, Has.Count.EqualTo(0));

			p = obj.Pick([ "NotFound" ], keepMissing: true);
			Assert.That(p, Is.Not.Null.And.Not.SameAs(obj));
			DumpCompact(p);
			Assert.That(p.ContainsKey("NotFound"), Is.True);
			Assert.That(p["NotFound"], IsJson.Missing);
			Assert.That(p, Has.Count.EqualTo(1));


			{ // test that we keep the "readonly-ness" or the original
				var source = JsonObject.ReadOnly.Create([("foo", 123), ("bar", 456)]);
				Assume.That(source, IsJson.ReadOnly);
				p = source.Pick(["bar"]);
				Assert.That(p, IsJson.ReadOnly);
				Assert.That(source, IsJson.ReadOnly);
			}
		}

		[Test]
		public void Test_JsonObject_ReadOnly_Empty()
		{
			Assert.That(JsonObject.ReadOnly.Empty.IsReadOnly, Is.True);
			//note: we don't want to attempt to modify the empty readonly singleton, because if the test fails, it will completely break ALL the reamining tests!

			static void CheckEmptyReadOnly(JsonObject obj, [CallerArgumentExpression(nameof(obj))] string? expression = null)
			{
				Assert.That(obj, Has.Count.Zero, expression);
				AssertIsImmutable(obj, expression);
				Assert.That(obj, Has.Count.Zero, expression);
			}

			CheckEmptyReadOnly(JsonObject.ReadOnly.Create());
			CheckEmptyReadOnly(JsonObject.ReadOnly.Create(Array.Empty<KeyValuePair<string, JsonValue>>()));
			CheckEmptyReadOnly(JsonObject.ReadOnly.Create(new Dictionary<string, JsonValue>()));
			CheckEmptyReadOnly(JsonObject.Create().ToReadOnly());
			CheckEmptyReadOnly(JsonObject.Copy(JsonObject.Create(), deep: false, readOnly: true));
			CheckEmptyReadOnly(JsonObject.Copy(JsonObject.Create(), deep: true, readOnly: true));

			var obj = JsonObject.Create("hello", "world");
			obj.Remove("hello");
			CheckEmptyReadOnly(obj.ToReadOnly());
			CheckEmptyReadOnly(JsonObject.Copy(obj, deep: false, readOnly: true));
			CheckEmptyReadOnly(JsonObject.Copy(obj, deep: true, readOnly: true));
		}

		[Test]
		public void Test_JsonObject_ReadOnly()
		{
			// creating a readonly object with only immutable values should produce an immutable object
			AssertIsImmutable(JsonObject.ReadOnly.Create("one", 1));
			AssertIsImmutable(JsonObject.ReadOnly.Create(("one", 1)));
			AssertIsImmutable(JsonObject.ReadOnly.Create([ ("one", 1) ]));
			AssertIsImmutable(JsonObject.ReadOnly.Create([ ("one", 1), ("two", 2) ]));
			AssertIsImmutable(JsonObject.ReadOnly.Create([ ("one", 1), ("two", 2), ("three", 3)]));
			AssertIsImmutable(JsonObject.ReadOnly.Create([ ("one", 1), ("two", 2), ("three", 3), ("four", 4)]));
			AssertIsImmutable(JsonObject.ReadOnly.Create([ ("one", 1), ("two", 2), ("three", 3), ("four", 4), ("five", 5) ]));
			AssertIsImmutable(JsonObject.ReadOnly.FromValues(Enumerable.Range(0, 10).Select(i => KeyValuePair.Create(i.ToString(), i))));

			// creating an immutable version of a writable object with only immutable should return an immutable object
			AssertIsImmutable(JsonObject.Create("one", 1).ToReadOnly());
			AssertIsImmutable(JsonObject.Create([ ("one", 1), ("two", 2), ("three", 3) ]).ToReadOnly());

			// parsing with JsonImmutable should return an already immutable object
			var obj = JsonObject.Parse("""{ "hello": "world", "foo": { "id": 123, "name": "Foo", "address" : { "street": 123, "city": "Paris" } }, "bar": [ 1, 2, 3 ], "baz": [ { "jazz": 42 } ] }""", CrystalJsonSettings.JsonReadOnly);
			AssertIsImmutable(obj);
			var foo = obj.GetObject("foo");
			AssertIsImmutable(foo);
			var addr = foo.GetObject("address");
			AssertIsImmutable(addr);
			var bar = obj.GetArray("bar");
			AssertIsImmutable(bar);
			var baz = obj.GetArray("baz");
			AssertIsImmutable(baz);
			var jazz = baz.GetObject(0);
			AssertIsImmutable(jazz);
		}

		[Test]
		public void Test_JsonObject_Freeze()
		{
			// ensure that, given a mutable JSON object, we can create an immutable version that is protected against any changes

			// the original object should be mutable
			var obj = new JsonObject
			{
				["hello"] = "world",
				["foo"] = new JsonObject { ["bar"] = "baz" },
				["bar"] = new JsonArray { 1, 2, 3 }
			};
			Assert.That(obj.IsReadOnly, Is.False);
			// the inner children 'foo' and 'bar' should be mutable as well
			var foo = obj.GetObject("foo");
			Assert.That(obj.IsReadOnly, Is.False);
			var bar = obj.GetArray("bar");
			Assert.That(obj.IsReadOnly, Is.False);

			Assert.That(obj.ToJsonCompact(), Is.EqualTo("""{"hello":"world","foo":{"bar":"baz"},"bar":[1,2,3]}"""));

			var obj2 = obj.Freeze();
			Assert.That(obj2, Is.SameAs(obj), "Freeze() should return the same instance");
			AssertIsImmutable(obj2, "obj.Freeze()");

			// the inner object 'foo' should also have been frozen!
			var foo2 = obj2.GetObject("foo");
			Assert.That(foo2, Is.SameAs(foo));
			Assert.That(foo2.IsReadOnly, Is.True);
			AssertIsImmutable(foo2, "(obj.Freeze())[\"foo\"]");

			// the inner array 'bar' should also have been frozen!
			var bar2 = obj2.GetArray("bar");
			Assert.That(bar2, Is.SameAs(bar));
			Assert.That(bar2.IsReadOnly, Is.True);
			AssertIsImmutable(bar2, "(obj.Freeze())[\"bar\"]");

			Assert.That(obj2.ToJsonCompact(), Is.EqualTo("""{"hello":"world","foo":{"bar":"baz"},"bar":[1,2,3]}"""));

			// if we want to mutate, we have to create a copy
			var obj3 = obj2.Copy();

			// the copy should still be equal to the original
			Assert.That(obj3, Is.Not.SameAs(obj2), "it should return a new instance");
			Assert.That(obj3, Is.EqualTo(obj2), "It should still be equal");
			var foo3 = obj3.GetObject("foo");
			Assert.That(foo3, Is.Not.SameAs(foo2), "inner object should be cloned");
			Assert.That(foo3, Is.EqualTo(foo2), "It should still be equal");
			var bar3 = obj3.GetArray("bar");
			Assert.That(bar3, Is.Not.SameAs(bar2), "inner array should be cloned");
			Assert.That(bar3, Is.EqualTo(bar2), "It should still be equal");

			// should be aable to change the copy
			Assert.That(obj3, Has.Count.EqualTo(3));
			Assert.That(() => obj3.Add("bonjour", "le monde"), Throws.Nothing);
			Assert.That(obj3, Has.Count.EqualTo(4));
			Assert.That(obj3, Is.Not.EqualTo(obj2), "It should still not be equal after the change");
			Assert.That(foo3, Is.EqualTo(foo2), "It should still be equal");
			Assert.That(bar3, Is.EqualTo(bar2), "It should still be equal");

			// should be able to mutate the inner object
			Assert.That(foo3, Has.Count.EqualTo(1));
			Assert.That(() => foo3["baz"] = "jazz", Throws.Nothing);
			Assert.That(foo3, Has.Count.EqualTo(2));
			Assert.That(foo3, Is.Not.EqualTo(foo2), "It should still not be equal after the change");

			// should be able to mutate the inner array
			Assert.That(bar3, Has.Count.EqualTo(3));
			Assert.That(() => bar3.Add(4), Throws.Nothing);
			Assert.That(bar3, Has.Count.EqualTo(4));
			Assert.That(bar3, Is.Not.EqualTo(bar2), "It should still not be equal after the change");

			// verify the final mutated version
			Assert.That(obj3.ToJsonCompact(), Is.EqualTo("""{"hello":"world","foo":{"bar":"baz","baz":"jazz"},"bar":[1,2,3,4],"bonjour":"le monde"}"""));
			// ensure the original is unmodified
			Assert.That(obj2.ToJsonCompact(), Is.EqualTo("""{"hello":"world","foo":{"bar":"baz"},"bar":[1,2,3]}"""));
		}

		[Test]
		public void Test_JsonObject_Can_Mutate_Frozen()
		{
			// given an immutable object, check that we can create mutable version that will not modifiy the original
			var original = new JsonObject
			{
				["hello"] = "world",
				["foo"] = new JsonObject { ["bar"] = "baz" },
				["bar"] = new JsonArray { 1, 2, 3 }
			}.Freeze();
			Dump("Original", original);
			EnsureDeepImmutabilityInvariant(original);

			// create a "mutable" version of the entire tree
			var obj = original.ToMutable();
			Dump("Copy", obj);
			Assert.That(obj.IsReadOnly, Is.False, "Copy should be not be read-only!");
			Assert.That(obj, Is.Not.SameAs(original));
			Assert.That(obj, Is.EqualTo(original));
			Assert.That(obj["foo"], Is.Not.SameAs(original["foo"]));
			Assert.That(obj["bar"], Is.Not.SameAs(original["bar"]));
			EnsureDeepMutabilityInvariant(obj);

			obj["hello"] = "le monde";
			obj["level"] = 42;
			obj.GetObject("foo").Remove("bar");
			obj.GetObject("foo").Add("baz", "bar");
			obj.GetArray("bar").Add(4);
			Dump("Mutated", obj);
			Assert.That(obj, Is.Not.EqualTo(original));

			// ensure the original is not mutated
			Assert.That(original["hello"], IsJson.EqualTo("world"));
			Assert.That(original["level"], IsJson.Null);
			Assert.That(original["foo"]["bar"], IsJson.EqualTo("baz"));
			Assert.That(original["foo"]["baz"], IsJson.Null);
			Assert.That(original.GetArray("bar"), Has.Count.EqualTo(3));
		}

		[Test]
		public void Test_JsonObject_CopyAndMutate()
		{
			// Test the "builder" API that can simplify making changes to a read-only object and publishing the new instance.
			// All methods will create return a new copy of the original, with the mutation applied, leaving the original untouched.
			// The new read-only copy should reuse the same JsonValue instances as the original, to reduce memory copies.

			var obj = JsonObject.ReadOnly.Empty;
			Assume.That(obj, IsJson.Empty);
			Assume.That(obj, IsJson.ReadOnly);
			Assume.That(obj["hello"], IsJson.Missing);
			DumpCompact(obj);

			// copy and add first field
			var obj2 = obj.CopyAndAdd("hello", "world");
			DumpCompact(obj2);
			Assert.That(obj2, Is.Not.SameAs(obj));
			Assert.That(obj2, Has.Count.EqualTo(1));
			Assert.That(obj2, IsJson.ReadOnly);
			Assert.That(obj2["hello"], IsJson.EqualTo("world"));
			Assert.That(obj, IsJson.Empty);
			Assert.That(obj["hello"], IsJson.Missing);

			// copy and set second field
			var obj3 = obj2.CopyAndSet("XYZfoo".AsSpan(3), "bar");
			DumpCompact(obj3);
			Assert.That(obj3, Is.Not.SameAs(obj2));
			Assert.That(obj3, Has.Count.EqualTo(2));
			Assert.That(obj3, IsJson.ReadOnly);
			Assert.That(obj3["hello"], Is.SameAs(obj2["hello"]));
			Assert.That(obj3["foo"], IsJson.EqualTo("bar"));
			Assert.That(obj2, Has.Count.EqualTo(1));
			Assert.That(obj2["hello"], IsJson.EqualTo("world"));
			Assert.That(obj2["foo"], IsJson.Missing);
			Assert.That(obj, IsJson.Empty);

			// copy and add existing field should fail
			Assert.That(() => obj3.CopyAndAdd("foo", "baz"), Throws.ArgumentException.With.Message.Contains("foo"));
			Assert.That(obj3, Has.Count.EqualTo(2));
			Assert.That(obj3["foo"], IsJson.EqualTo("bar"));

			// copy and set should overwrite existing field
			var obj4 = obj3.CopyAndSet("XYZfoo".AsMemory(3), "baz");
			DumpCompact(obj4);
			Assert.That(obj4, Is.Not.SameAs(obj3));
			Assert.That(obj4, Has.Count.EqualTo(2));
			Assert.That(obj4, IsJson.ReadOnly);
			Assert.That(obj4["hello"], Is.EqualTo("world").And.SameAs(obj3["hello"]));
			Assert.That(obj4["foo"], IsJson.EqualTo("baz"));
			Assert.That(obj3, Has.Count.EqualTo(2));
			Assert.That(obj3["hello"], Is.EqualTo("world").And.SameAs(obj2["hello"]));
			Assert.That(obj3["foo"], IsJson.EqualTo("bar"));
			Assert.That(obj2, Has.Count.EqualTo(1));
			Assert.That(obj2["hello"], Is.EqualTo("world"));
			Assert.That(obj2["foo"], IsJson.Missing);
			Assert.That(obj, IsJson.Empty);
			Assert.That(obj["foo"], IsJson.Missing);

			// copy and remove
			var obj5 = obj4.CopyAndRemove("hello");
			DumpCompact(obj5);
			Assert.That(obj5, Is.Not.SameAs(obj4));
			Assert.That(obj5, Has.Count.EqualTo(1));
			Assert.That(obj5, IsJson.ReadOnly);
			Assert.That(obj5["hello"], IsJson.Missing);
			Assert.That(obj5["foo"], IsJson.EqualTo("baz"));
			Assert.That(obj4, Has.Count.EqualTo(2));
			Assert.That(obj4["hello"], IsJson.EqualTo("world"));
			Assert.That(obj4["foo"], IsJson.EqualTo("baz"));
			Assert.That(obj3, Has.Count.EqualTo(2));
			Assert.That(obj3["hello"], IsJson.EqualTo("world"));
			Assert.That(obj3["foo"], IsJson.EqualTo("bar"));
			Assert.That(obj2, Has.Count.EqualTo(1));
			Assert.That(obj2["hello"], IsJson.EqualTo("world"));
			Assert.That(obj2["foo"], IsJson.Missing);
			Assert.That(obj, IsJson.Empty);
			Assert.That(obj["foo"], IsJson.Missing);

			// copy and try removing last field
			var obj6 = obj5.CopyAndRemove("foo", out var prev);
			DumpCompact(obj6);
			Assert.That(obj6, Is.Not.SameAs(obj5));
			Assert.That(obj6, Is.Empty);
			Assert.That(obj6, IsJson.ReadOnly);
			Assert.That(obj6["hello"], IsJson.Missing);
			Assert.That(obj6["foo"], IsJson.Missing);
			Assert.That(obj6, Is.SameAs(JsonObject.ReadOnly.Empty));
			Assert.That(prev, Is.SameAs(obj5["foo"]));
			Assert.That(obj5, Has.Count.EqualTo(1));
			Assert.That(obj5["hello"], IsJson.Missing);
			Assert.That(obj5["foo"], IsJson.EqualTo("baz"));
			Assert.That(obj4, Has.Count.EqualTo(2));
			Assert.That(obj4["hello"], IsJson.EqualTo("world"));
			Assert.That(obj4["foo"], IsJson.EqualTo("baz"));
			Assert.That(obj3, Has.Count.EqualTo(2));
			Assert.That(obj3["hello"], IsJson.EqualTo("world"));
			Assert.That(obj3["foo"], IsJson.EqualTo("bar"));
			Assert.That(obj2, Has.Count.EqualTo(1));
			Assert.That(obj2["hello"], IsJson.EqualTo("world"));
			Assert.That(obj2["foo"], IsJson.Missing);
			Assert.That(obj, IsJson.Empty);
			Assert.That(obj["foo"], IsJson.Missing);

		}

		[Test]
		public async Task Test_JsonObject_CopyAndPublish()
		{
			var prev = JsonObject.ReadOnly.Empty;

			JsonObject published = prev;

			var obj = JsonObject.CopyAndAdd(ref published, "hello", "world");
			Assert.That(obj, Is.SameAs(published));
			Assert.That(published, Is.Not.SameAs(prev));
			Assert.That(obj, IsJson.ReadOnly);
			Assert.That(obj["hello"], IsJson.EqualTo("world"));

			prev = published;
			obj = JsonObject.CopyAndSet(ref published, "foo", "bar");
			Assert.That(obj, Is.SameAs(published));
			Assert.That(published, Is.Not.SameAs(prev));
			Assert.That(obj, IsJson.ReadOnly);
			Assert.That(obj["hello"], IsJson.EqualTo("world"));
			Assert.That(obj["foo"], IsJson.EqualTo("bar"));
			Assert.That(prev["foo"], IsJson.Missing);

			// attempts to verify the thread safety by spinnig N threads that will all add M fields, and checking that the result is an object with N x M unique fields

			published = JsonObject.ReadOnly.Empty;

			var go = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
			const int N = 10;
			const int M = 100;

			var keys = Enumerable.Range(0, N).Select(idx => Enumerable.Range(0, M).Select(i => $"{idx}_{i}").ToArray()).ToArray();

			var workers = keys.Select(async row =>
			{
				// precompute a maximum so that we can ensure the most contention between threads!
				await go.Task.ConfigureAwait(false);
				var value = JsonBoolean.True;
				foreach(var key in row)
				{
					JsonObject.CopyAndAdd(ref published, key, value);
				}
			}).ToList();

			go.TrySetResult();

			await WhenAll(workers, TimeSpan.FromSeconds(30));
			// Ensure that all the keys and values are accounted for.
			Assert.That(published, Has.Count.EqualTo(N * M));
			foreach (var row in keys)
			{
				foreach (var key in row)
				{
					if (!published.ContainsKey(key))
					{
						Dump(published);
						Assert.That(published, Does.ContainKey(key));
					}
				}
			}
		}

		[Test]
		public void Test_JsonObject_StrictEquals()
		{
			Assert.Multiple(() =>
			{
				Assert.That(JsonObject.ReadOnly.Empty.StrictEquals(JsonObject.ReadOnly.Empty), Is.True);
				Assert.That(JsonObject.ReadOnly.Empty.StrictEquals(new JsonObject()), Is.True);
				Assert.That(new JsonObject().StrictEquals(JsonObject.ReadOnly.Empty), Is.True);

				Assert.That(JsonObject.ReadOnly.Empty.StrictEquals(JsonObject.Create("hello", "world")), Is.False);
				Assert.That(JsonObject.ReadOnly.Empty.StrictEquals(JsonObject.Create("hello", null)), Is.True);
			});
			Assert.Multiple(() =>
			{
				var obj = JsonObject.Create([ ("hello", "world"), ("foo", 123), ("bar", true) ]);

				Assert.That(obj.StrictEquals(JsonObject.Create([ ("hello", "world"), ("foo", 123), ("bar", true) ])), Is.True);
				Assert.That(obj.StrictEquals(JsonObject.Create([ ("hello", "world"), ("foo", 123L), ("bar", true) ])), Is.True);
				Assert.That(obj.StrictEquals(JsonObject.Create([ ("hello", "world"), ("foo", 123.0), ("bar", true) ])), Is.True);
				Assert.That(obj.StrictEquals(JsonObject.Create([ ("hello", "world"), ("foo", 123.0f), ("bar", true) ])), Is.True);
				Assert.That(obj.StrictEquals(JsonObject.Create([ ("hello", "world"), ("foo", 123m), ("bar", true) ])), Is.True);
				Assert.That(obj.StrictEquals(JsonObject.Create([ ("bar", true), ("foo", 123), ("hello", "world") ])), Is.True);

				Assert.That(obj.StrictEquals(JsonObject.ReadOnly.Empty), Is.False);
				Assert.That(obj.StrictEquals(new JsonObject()), Is.False);
				Assert.That(obj.StrictEquals(JsonObject.Create([ ("hello", "world"), ("foo", 123) ])), Is.False);
				Assert.That(obj.StrictEquals(JsonObject.Create([ ("hello", "world"), ("foo", 123), ("bar", true), ("baz", 456) ])), Is.False);
				Assert.That(obj.StrictEquals(JsonObject.Create([ ("hello", "there"), ("foo", 123), ("bar", true) ])), Is.False);
				Assert.That(obj.StrictEquals(JsonObject.Create([ ("hello", "world"), ("foo", 456), ("bar", true) ])), Is.False);
				Assert.That(obj.StrictEquals(JsonObject.Create([ ("hello", "world"), ("foo", "123"), ("bar", true) ])), Is.False);
				Assert.That(obj.StrictEquals(JsonObject.Create([ ("hello", "world"), ("foo", 123), ("bar", false) ])), Is.False);
				Assert.That(obj.StrictEquals(JsonObject.Create([ ("hello", 123), ("foo", true), ("bar", "world") ])), Is.False);

				Assert.That(obj.StrictEquals(new Dictionary<string, JsonValue> { { "hello", "world" }, { "foo", 123 }, { "bar", true } }), Is.True);
				Assert.That(obj.StrictEquals(new Dictionary<string, JsonValue> { { "hello", "there" }, { "foo", 123 }, { "bar", true } }), Is.False);
				Assert.That(obj.StrictEquals(new Dictionary<string, JsonValue> { { "hello", "world" }, { "foo", 456 }, { "bar", true } }), Is.False);
				Assert.That(obj.StrictEquals(new Dictionary<string, JsonValue> { { "hello", "world" }, { "foo", 123 }, { "bar", false } }), Is.False);
			});
			Assert.Multiple(() =>
			{
				var obj = JsonObject.Create("foo", JsonObject.Create("bar", JsonArray.Create("baz", 123, true)));

				Assert.That(obj.StrictEquals(JsonObject.Create("foo", JsonObject.Create("bar", JsonArray.Create("baz", 123, true)))), Is.True);
				Assert.That(obj.StrictEquals(JsonObject.Create("foo", JsonObject.Create("bar", JsonArray.Create("baz", "123", true)))), Is.False);
				Assert.That(obj.StrictEquals(JsonObject.Create("foo", JsonObject.Create("bar", JsonArray.Create("baz", 123, 456)))), Is.False);
			});
			Assert.Multiple(() =>
			{
				// check that explicit null/missing are equivalent to "not present" on the other side
				Assert.That(JsonObject.Create([ ("foo", 123), ("bar", null) ]).StrictEquals(JsonObject.Create([ ("foo", 123), ("baz", null) ])), Is.True);
				Assert.That(JsonObject.Create([ ("foo", 123), ("bar", JsonNull.Missing) ]).StrictEquals(JsonObject.Create([ ("foo", 123), ("baz", JsonNull.Null) ])), Is.True);

				// but if one is null/missing, the other should not have a value
				Assert.That(JsonObject.Create([ ("foo", 123), ("bar", 456) ]).StrictEquals(JsonObject.Create([ ("foo", 123), ("baz", null) ])), Is.False);
				Assert.That(JsonObject.Create([ ("foo", 123), ("bar", 456) ]).StrictEquals(JsonObject.Create([ ("foo", 123), ("baz", JsonNull.Missing) ])), Is.False);
				Assert.That(JsonObject.Create([ ("foo", 123), ("bar", null) ]).StrictEquals(JsonObject.Create([ ("foo", 123), ("baz", 456) ])), Is.False);
				Assert.That(JsonObject.Create([ ("foo", 123), ("bar", JsonNull.Missing) ]).StrictEquals(JsonObject.Create([ ("foo", 123), ("baz", 456) ])), Is.False);
			});
		}

		#endregion

		#region JsonValue.FromValue/FromObject...

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
			Assert.That(JsonValue.FromValue(string.Empty), Is.InstanceOf<JsonString>());
			Assert.That(JsonValue.FromValue(Guid.NewGuid()), Is.InstanceOf<JsonString>());
			Assert.That(JsonValue.FromValue(IPAddress.Loopback), Is.InstanceOf<JsonString>());
			Assert.That(JsonValue.FromValue(DateTime.Now), Is.InstanceOf<JsonDateTime>());
			Assert.That(JsonValue.FromValue(TimeSpan.FromMinutes(1)), Is.InstanceOf<JsonNumber>());
			Assert.That(JsonValue.FromValue(DateOnly.FromDateTime(DateTime.Now)), Is.InstanceOf<JsonDateTime>());
			Assert.That(JsonValue.FromValue(TimeOnly.FromDateTime(DateTime.Now)), Is.InstanceOf<JsonNumber>());
			Assert.That(JsonValue.FromValue(new Uri("https://www.youtube.com/watch?v=dQw4w9WgXcQ")), Is.InstanceOf<JsonString>());

			// FromValue(object)
			Assert.That(JsonValue.FromValue(default(object)!), Is.InstanceOf<JsonNull>());
			Assert.That(JsonValue.FromValue((object) DBNull.Value), Is.InstanceOf<JsonNull>());
			Assert.That(JsonValue.FromValue((object) 123), Is.InstanceOf<JsonNumber>());
			Assert.That(JsonValue.FromValue((object) 123456L), Is.InstanceOf<JsonNumber>());
			Assert.That(JsonValue.FromValue((object) 123.4f), Is.InstanceOf<JsonNumber>());
			Assert.That(JsonValue.FromValue((object) 123.456d), Is.InstanceOf<JsonNumber>());
			Assert.That(JsonValue.FromValue((object) default(uint)), Is.InstanceOf<JsonNumber>());
			Assert.That(JsonValue.FromValue((object) default(ulong)), Is.InstanceOf<JsonNumber>());
			Assert.That(JsonValue.FromValue((object) default(sbyte)), Is.InstanceOf<JsonNumber>());
			Assert.That(JsonValue.FromValue((object) default(byte)), Is.InstanceOf<JsonNumber>());
			Assert.That(JsonValue.FromValue((object) default(short)), Is.InstanceOf<JsonNumber>());
			Assert.That(JsonValue.FromValue((object) default(ushort)), Is.InstanceOf<JsonNumber>());
			Assert.That(JsonValue.FromValue((object) default(decimal)), Is.InstanceOf<JsonNumber>());
			Assert.That(JsonValue.FromValue((object) false), Is.InstanceOf<JsonBoolean>());
			Assert.That(JsonValue.FromValue((object) true), Is.InstanceOf<JsonBoolean>());
			Assert.That(JsonValue.FromValue((object) "hello"), Is.InstanceOf<JsonString>());
			Assert.That(JsonValue.FromValue((object) string.Empty), Is.InstanceOf<JsonString>());
			Assert.That(JsonValue.FromValue((object) Guid.NewGuid()), Is.InstanceOf<JsonString>());
			Assert.That(JsonValue.FromValue((object) IPAddress.Loopback), Is.InstanceOf<JsonString>());
			Assert.That(JsonValue.FromValue((object) DateTime.Now), Is.InstanceOf<JsonDateTime>());
			Assert.That(JsonValue.FromValue((object) TimeSpan.FromMinutes(1)), Is.InstanceOf<JsonNumber>());
			Assert.That(JsonValue.FromValue((object) DateOnly.FromDateTime(DateTime.Now)), Is.InstanceOf<JsonDateTime>());
			Assert.That(JsonValue.FromValue((object) TimeOnly.FromDateTime(DateTime.Now)), Is.InstanceOf<JsonNumber>());
			Assert.That(JsonValue.FromValue((object) new Uri("https://www.youtube.com/watch?v=dQw4w9WgXcQ")), Is.InstanceOf<JsonString>());
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
			//note: nullable<int> is boxed into Int32 if it had a value, so we lose the knowledge that it was a Nullable<int> when calling FromObject(object)
			// => we will force the type by calling FromObject(..., typeof(int?)) in order to ensure that it works as intended

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
			{ // array of primitive type
				var j = JsonValue.FromValue(new[] { 1, 42, 77 });
				Assert.That(j, Is.Not.Null.And.InstanceOf<JsonArray>());
				Log(j); // => [ 1, 42, 77 ]
				var arr = j.AsArray();
				Assert.That(arr, Has.Count.EqualTo(3));
				Assert.That(arr[0], IsJson.EqualTo(1));
				Assert.That(arr[1], IsJson.EqualTo(42));
				Assert.That(arr[2], IsJson.EqualTo(77));
			}

			{ // list of primitive type
				var j = JsonValue.FromValue(new List<int> { 1, 42, 77 });
				Assert.That(j, Is.Not.Null.And.InstanceOf<JsonArray>());
				Log(j); // => [ 1, 42, 77 ]
				var arr = j.AsArray();
				Assert.That(arr, Has.Count.EqualTo(3));
				Assert.That(arr[0], IsJson.EqualTo(1));
				Assert.That(arr[1], IsJson.EqualTo(42));
				Assert.That(arr[2], IsJson.EqualTo(77));
			}

			{ // array of ref type
				var j = JsonValue.FromValue(new[] { "foo", "bar", "baz" });
				Assert.That(j, Is.Not.Null.And.InstanceOf<JsonArray>());
				Log(j); // [ "foo", "bar", "baz" ]
				var arr = j.AsArray();
				Assert.That(arr, Has.Count.EqualTo(3));
				Assert.That(arr[0], IsJson.EqualTo("foo"));
				Assert.That(arr[1], IsJson.EqualTo("bar"));
				Assert.That(arr[2], IsJson.EqualTo("baz"));
			}

			{ // special collection (read only)
				var j = JsonValue.FromValue(new ReadOnlyCollection<string>(new List<string> { "foo", "bar", "baz" }));
				Assert.That(j, Is.Not.Null.And.InstanceOf<JsonArray>());
				Log(j); // [ "foo", "bar", "baz" ]
				var arr = j.AsArray();
				Assert.That(arr, Has.Count.EqualTo(3));
				Assert.That(arr, IsJson.EqualTo([ "foo", "bar", "baz" ]));
				Assert.That(arr, IsJson.EqualTo(arr));
				Assert.That(arr[0], IsJson.EqualTo("foo"));
				Assert.That(arr[1], IsJson.EqualTo("bar"));
				Assert.That(arr[2], IsJson.EqualTo("baz"));
			}

			{ // LINQ query
				var j = JsonValue.FromValue(Enumerable.Range(1, 10));
				Assert.That(j, Is.Not.Null.And.InstanceOf<JsonArray>());
				Log(j); // => [ 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 ]
				var arr = j.AsArray();
				Assert.That(arr, Has.Count.EqualTo(10));
				Assert.That(arr, IsJson.EqualTo(Enumerable.Range(1, 10)));
			}

			{ // LINQ query
				var j = JsonValue.FromValue(Enumerable.Range(1, 3).Select(x => new KeyValuePair<int, char>(x, (char)(64 + x))).ToList());
				Assert.That(j, Is.Not.Null.And.InstanceOf<JsonArray>());
				Log(j);
				var arr = j.AsArray();
				Assert.That(arr, Has.Count.EqualTo(3));
				//TODO: BUGBUG: for now, will return [ { Key: .., Value: .. }, .. ] instead of [ [ .., .. ], .. ]
			}
		}

		[Test]
		public void Test_JsonValue_FromObject_Dictionary()
		{
			//string keys...

			var j = JsonValue.FromValue(new Dictionary<string, int> {{"foo", 11}, {"bar", 22}, {"baz", 33}});
			Assert.That(j, Is.Not.Null.And.InstanceOf<JsonObject>());
			Log(j); // { "foo": 11, "bar": 22, "baz": 33 }
			var obj = j.AsObject();
			Assert.That(obj, Has.Count.EqualTo(3));
			Assert.That(obj.Get<int>("foo"), Is.EqualTo(11));
			Assert.That(obj.Get<int>("bar"), Is.EqualTo(22));
			Assert.That(obj.Get<int>("baz"), Is.EqualTo(33));

			var g1 = Guid.NewGuid();
			var g2 = Guid.NewGuid();
			var g3 = Guid.NewGuid();
			j = JsonValue.FromValue(new Dictionary<string, Guid> { { "foo", g1 }, { "bar", g2 }, { "baz", g3 } });
			Log(j); // { "foo": ..., "bar": ..., "baz": ... }
			obj = j.AsObject();
			Assert.That(obj, Has.Count.EqualTo(3));
			Assert.That(obj.Get<Guid>("foo"), Is.EqualTo(g1));
			Assert.That(obj.Get<Guid>("bar"), Is.EqualTo(g2));
			Assert.That(obj.Get<Guid>("baz"), Is.EqualTo(g3));

			var dic = Enumerable.Range(0, 3).Select(x => new {Id = x, Name = "User#" + x.ToString(), Level = x * 9000}).ToDictionary(x => x.Name);
			obj = JsonObject.FromObject(dic);
			Log(obj);
			Assert.That(obj, Has.Count.EqualTo(3));

			// non-string keys...

			j = JsonValue.FromValue(new Dictionary<int, string> { [11] = "foo", [22] = "bar", [33] = "baz" });
			Assert.That(j, Is.Not.Null.And.InstanceOf<JsonObject>());
			Log(j); // { "11": "foo", "22": "bar", "33": "baz" }
			obj = j.AsObject();
			Assert.That(obj, Has.Count.EqualTo(3));
			Assert.That(obj.Get<string>("11"), Is.EqualTo("foo"));
			Assert.That(obj.Get<string>("22"), Is.EqualTo("bar"));
			Assert.That(obj.Get<string>("33"), Is.EqualTo("baz"));

			// we can also convert directly to JsonObject
			obj = JsonObject.FromObject(new Dictionary<string, int> { ["foo"] = 11, ["bar"] = 22, ["baz"] = 33 });
			Assert.That(obj, Is.Not.Null);
			Log(obj);
			Assert.That(obj, Has.Count.EqualTo(3));
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
			Assert.That(JsonValue.FromValue(new ListTuple<int>([1, 2, 3])).ToJson(), Is.EqualTo("[ 1, 2, 3 ]"));
			Assert.That(JsonValue.FromValue(new ListTuple<string>(["foo", "bar", "baz"])).ToJson(), Is.EqualTo("[ \"foo\", \"bar\", \"baz\" ]"));
			Assert.That(JsonValue.FromValue(new ListTuple<object>(["hello world", 123, false])).ToJson(), Is.EqualTo("[ \"hello world\", 123, false ]"));
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

			var obj = result.AsObject();
			Assert.That(obj, Has.Count.EqualTo(3));
			Assert.That(obj.Get<int>("foo"), Is.EqualTo(123));
			Assert.That(obj.Get<bool>("bar"), Is.False);
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
				DateOfBirth = new DateOnly(1920, 11, 11),
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
			Assert.That(j["DateOfBirth"], Is.Not.Null.And.Property("Type").EqualTo(JsonType.DateTime));
			Assert.That(j["State"], Is.Not.Null.And.Property("Type").EqualTo(JsonType.String));
			//TODO: ignore defaults?
			//Assert.That(j, Has.Count.EqualTo(8));

			Assert.That(j.Get<string>("Name"), Is.EqualTo(agent.Name), ".Name");
			Assert.That(j.Get<int>("Index"), Is.EqualTo(agent.Index), ".Index");
			Assert.That(j.Get<long>("Size"), Is.EqualTo(agent.Size), ".Size");
			Assert.That(j.Get<float>("Height"), Is.EqualTo(agent.Height), ".Height");
			Assert.That(j.Get<double>("Amount"), Is.EqualTo(agent.Amount), ".Amount");
			Assert.That(j.Get<DateTime>("Created"), Is.EqualTo(agent.Created), ".Created");
			Assert.That(j.Get<DateTime>("Modified"), Is.EqualTo(agent.Modified), ".Modified");
			Assert.That(j.Get<DateOnly>("DateOfBirth"), Is.EqualTo(agent.DateOfBirth), ".DateOfBirth");
			Assert.That(j.Get<DummyJsonEnum>("State"), Is.EqualTo(agent.State), ".State");
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
					DateOfBirth = new DateOnly(1920, 11, 11),
					State = DummyJsonEnum.Bar,
				},
			};

			var j = JsonValue.FromValue((object)x);
			Assert.That(j, Is.Not.Null);
			Log(j.ToJsonIndented());
			Assert.That(j.Type, Is.EqualTo(JsonType.Object), "FromObject((TClass)obj) should return a JsonObject");
			var obj = (JsonObject)j;
			Assert.That(obj.Get<string?>("$type", null), Is.Null, "Not on top level");
			Assert.That(obj.ContainsKey("Agent"));
			Assert.That(obj.GetPathValue("Agent.$type").ToString(), Is.EqualTo("spy"), "On sub-object");
			var y = obj.Required<DummyOuterDerivedClass>();
			Assert.That(y.Agent, Is.Not.Null.And.InstanceOf<DummyDerivedJsonClass>());
			Assert.That(y.Agent.Name, Is.EqualTo("James Bond"));

			j = JsonValue.FromValue((object) x.Agent);
			Assert.That(j, Is.Not.Null);
			Log(j.ToJsonIndented());
			Assert.That(j.Type, Is.EqualTo(JsonType.Object), "FromObject((TDerived)obj) should return a JsonObject");
			obj = (JsonObject)j;
			Assert.That(obj.Get<string?>("$type", null), Is.EqualTo("spy"), "FromObject(foo) should output the $type if one is required");
			var z = obj.Required<DummyDerivedJsonClass>();
			Assert.That(z, Is.Not.Null.And.InstanceOf<DummyDerivedJsonClass>());
			Assert.That(z.Name, Is.EqualTo("James Bond"));

			j = JsonValue.FromValue<DummyJsonBaseClass>(x.Agent);
			Assert.That(j, Is.Not.Null);
			Log(j.ToJsonIndented());
			Assert.That(j.Type, Is.EqualTo(JsonType.Object), "FromValue<TClass>() should return a JsonObject");
			obj = (JsonObject)j;
			Assert.That(obj.Get<string?>("$type", null), Is.EqualTo("spy"), "FromValue<TBase>((TDerived)foo) should output the $type");
			var w = obj.Required<DummyJsonBaseClass>();
			Assert.That(w, Is.Not.Null.And.InstanceOf<DummyDerivedJsonClass>());
			Assert.That(w.Name, Is.EqualTo("James Bond"));

		}

		[Test]
		public void Test_JsonValue_FromObject_JsonValue()
		{
			// When calling FromValue<T>(..) on an instance which is already a JsonValue, we should return the same instance

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

		[Test]
		public void Test_JsonValue_Equals()
		{
			// To simplify unit tests, we want to be able to write "Assert.That(<JsonValue>, Is.EqualTo(<any type>)" to bypass the need to call ".Get<any_type>(...)"
			// Unfortunately, there are a few cases that do not work as inteded, like Is.True or Is.Null/Is.Not.Null

			DateTime now = DateTime.Now;
			Guid id = Guid.NewGuid();
			var obj = new JsonObject
			{
				["str"] = "world",
				["int"] = 42,
				["zero"] = 0,
				["true"] = true,
				["false"] = false,
				["id"] = id,
				["date"] = now,
				["null"] = null, // explicit null
			};

			Assert.That(obj["str"], Is.EqualTo("world"));
			Assert.That(obj["int"], Is.EqualTo(42));
			Assert.That(obj["true"], Is.EqualTo(true)); // note: Is.True cannot work because it does true.Equals(actual) instead of actual.Equals(true) :(
			Assert.That(obj["zero"], Is.Zero); // but Is.Zero is fine because it's an alias for EqualTo(0)
			Assert.That(obj["id"], Is.EqualTo(id));
#if DISABLED // this is currently broken in NUnit 4.3.2, see https://github.com/nunit/nunit/issues/4954
			Assert.That(obj["date"], Is.EqualTo(now));
#endif
			Assert.That(obj["null"], Is.EqualTo(JsonNull.Null));

			var top = new JsonObject
			{
				["foo"] = obj.Copy(),
				["bar"] = JsonArray.Create(obj.Copy()),
				["null"] = null, // explicit null
			};

			Assert.That(top["foo"], Is.Not.Null);
			Assert.That(top["foo"]["str"], Is.EqualTo("world"));
			Assert.That(top["foo"]["str"], Is.EqualTo("world"));
			Assert.That(top["foo"]["int"], Is.EqualTo(42));
			Assert.That(top["foo"]["true"], Is.EqualTo(true));
			Assert.That(top["foo"]["false"], Is.EqualTo(false));
			Assert.That(top["foo"]["zero"], Is.Zero);
			Assert.That(top["foo"]["id"], Is.EqualTo(id));
#if DISABLED // this is currently broken in NUnit 4.3.2, see https://github.com/nunit/nunit/issues/4954
			Assert.That(top["foo"]["date"], Is.EqualTo(now));
#endif

			Assert.That(top["bar"], Is.Not.Null);
			Assert.That(top["bar"][0], Is.Not.Null);
			Assert.That(top["bar"][0]["str"], Is.EqualTo("world"));
			Assert.That(top["bar"][0]["str"], Is.EqualTo("world"));
			Assert.That(top["bar"][0]["int"], Is.EqualTo(42));
			Assert.That(top["bar"][0]["true"], Is.EqualTo(true));
			Assert.That(top["bar"][0]["false"], Is.EqualTo(false));
			Assert.That(top["bar"][0]["zero"], Is.Zero);
			Assert.That(top["bar"][0]["id"], Is.EqualTo(id));
#if DISABLED // this is currently broken in NUnit 4.3.2, see https://github.com/nunit/nunit/issues/4954
			Assert.That(top["bar"][0]["date"], Is.EqualTo(now));
#endif

			// ISSUES: the following statement unfortunately will not work as intended:

			// - Is.True is implemented as true.Equals((object) actual) so it will never be able to work, must use Is.Equal(true) instead :(
			// Assert.That(obj["true"], Is.True); // FAIL!
			// Assert.That(obj["false"], Is.False); // FAIL!

			// - Is.Null is implemented as (object) actual == null, so it cannot work either (since we return JsonNull.Missing which is not a null reference
			// Assert.That(top["not_found"], Is.Null); // FAIL!
			// Assert.That(top["foo"], Is.Not.Null); // will never fail
			// Assert.That(top["null"], Is.Null); // FAIL!
			// Assert.That(top["null"], Is.Not.Null); // PASS even though we would expect the reverse!
			// - Is.EqualTo(null) fails for a similar reason because it expected null reference, and will not attempt to call obj.Equals(null)
			// Assert.That(obj["null"], Is.EqualTo(null)); //FAIL!
		}

		#endregion

		#region Parse...

		[Test]
		public void Test_Parse_Null()
		{
			Assert.That(JsonValue.Parse("null"), Is.EqualTo(JsonNull.Null), "Parse('null')");
			Assert.That(JsonValue.Parse(string.Empty), Is.EqualTo(JsonNull.Missing), "Parse('')");
			Assert.That(JsonValue.Parse(default(string)), Is.EqualTo(JsonNull.Missing), "Parse(default(string))");

			Assert.That(JsonValue.Parse(new byte[0]), Is.EqualTo(JsonNull.Missing), "Parse('')");
			Assert.That(JsonValue.Parse(default(byte[])), Is.EqualTo(JsonNull.Missing), "Parse(default(string))");
			Assert.That(JsonValue.Parse((new byte[10]).AsSpan(5, 0)), Is.EqualTo(JsonNull.Missing), "Parse('')");

			Assert.That(JsonValue.Parse(Slice.Empty), Is.EqualTo(JsonNull.Missing), "Parse(Slice.Empty)");
			Assert.That(JsonValue.Parse(Slice.Nil), Is.EqualTo(JsonNull.Missing), "Parse(Slice.Nil)");

			Assert.That(JsonValue.Parse(ReadOnlySpan<byte>.Empty), Is.EqualTo(JsonNull.Missing), "Parse(ReadOnlySpan<byte>.Empty)");
		}

		[Test]
		public void Test_Parse_Boolean()
		{
			Assert.That(JsonValue.Parse("true"), Is.EqualTo(JsonBoolean.True), "Parse('true')");
			Assert.That(JsonValue.Parse("false"), Is.EqualTo(JsonBoolean.False), "Parse('false')");

			// we need to whole token
			Assert.That(() => JsonValue.Parse("tru"), Throws.InstanceOf<JsonSyntaxException>(), "Parse('tru')");
			Assert.That(() => JsonValue.Parse("flse"), Throws.InstanceOf<JsonSyntaxException>(), "Parse('flse')");

			// but without any extra characters
			Assert.That(() => JsonValue.Parse("truee"), Throws.InstanceOf<JsonSyntaxException>(), "Parse('tru')");
			Assert.That(() => JsonValue.Parse("falsee"), Throws.InstanceOf<JsonSyntaxException>(), "Parse('flse')");

			// it is case senstitive
			Assert.That(() => JsonValue.Parse("True"), Throws.InstanceOf<JsonSyntaxException>(), "Parse('True')");
			Assert.That(() => JsonValue.Parse("Frue"), Throws.InstanceOf<JsonSyntaxException>(), "Parse('Frue')");

			// we do not allow variations (yes/no, on/off, ...)
			Assert.That(() => JsonValue.Parse("yes"), Throws.InstanceOf<JsonSyntaxException>(), "Parse('yes')");
			Assert.That(() => JsonValue.Parse("off"), Throws.InstanceOf<JsonSyntaxException>(), "Parse('off')");
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
				var num2 = JsonNumber.Return(0.1234E4);
				Assert.That(num2.ToDouble(), Is.EqualTo(1234.0));
				Assert.That(num2.Literal, Is.EqualTo("1234")); //BUGBUG: devrait être "1234.0" ?
				//Assert.That(num2.IsDecimal, Is.False); //REVIEW: vu qu'on a appelé Return(double), le json est actuellement considéré comme décimal..
			}

			{ // real decimal
				var num1 = (JsonNumber) JsonValue.Parse("0.1234E3");
				Assert.That(num1.ToDouble(), Is.EqualTo(123.4));
				Assert.That(num1.Literal, Is.EqualTo("0.1234E3"));
				Assert.That(num1.IsDecimal, Is.True);
				var num2 = JsonNumber.Return(0.1234E3);
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
					var parsed = JsonValue.ParseReadOnly(jsonText);
					Assert.That(parsed, Is.EqualTo(expected), $"JsonValue.Parse('{jsonText}') into {expected.Type}{(message is null ? string.Empty : (": " + message))}");
					Assert.That(parsed, IsJson.ReadOnly);
				}
				{ // JsonValue, RoS<char>, readonly
					var parsed = JsonValue.ParseReadOnly(("$$$" + jsonText + "%%%").AsSpan()[3..^3]);
					Assert.That(parsed, Is.EqualTo(expected), $"JsonValue.Parse('{jsonText}') into {expected.Type}{(message is null ? string.Empty : (": " + message))}");
					Assert.That(parsed, IsJson.ReadOnly);
				}
				{ // JsonValue, RoS<byte>, readonly
					var parsed = JsonValue.ParseReadOnly(Encoding.UTF8.GetBytes("$$$" + jsonText + "%%%").AsSpan()[3..^3]);
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

		#endregion

		#region Deserialization...

		[Test]
		public void Test_JsonDeserialize_Null()
		{
			Assert.That(CrystalJson.Deserialize<string?>("null", null), Is.Null);
			Assert.That(CrystalJson.Deserialize<string?>("", null), Is.Null);
			Assert.That(CrystalJson.Deserialize<string?>("  ", null), Is.Null);

			Assert.That(CrystalJson.Deserialize<int?>("null", null), Is.Null);
			Assert.That(CrystalJson.Deserialize<int?>("", null), Is.Null);
			Assert.That(CrystalJson.Deserialize<int?>("  ", null), Is.Null);

			Assert.That(CrystalJson.Deserialize<string>("null", "not_found"), Is.EqualTo("not_found"));
			Assert.That(CrystalJson.Deserialize<string>("", "not_found"), Is.EqualTo("not_found"));
			Assert.That(CrystalJson.Deserialize<string>("  ", "not_found"), Is.EqualTo("not_found"));

			Assert.That(CrystalJson.Deserialize<int>("null", 123), Is.EqualTo(123));
			Assert.That(CrystalJson.Deserialize<int>("", 123), Is.EqualTo(123));
			Assert.That(CrystalJson.Deserialize<int>("  ", 123), Is.EqualTo(123));

			Assert.That(() => CrystalJson.Deserialize<string>("null"), Throws.InstanceOf<JsonBindingException>());
			Assert.That(() => CrystalJson.Deserialize<string>(""), Throws.InstanceOf<JsonBindingException>());
			Assert.That(() => CrystalJson.Deserialize<int?>("null"), Throws.InstanceOf<JsonBindingException>());
			Assert.That(() => CrystalJson.Deserialize<int?>(""), Throws.InstanceOf<JsonBindingException>());
			Assert.That(() => CrystalJson.Deserialize<int>("null"), Throws.InstanceOf<JsonBindingException>());
			Assert.That(() => CrystalJson.Deserialize<int>(""), Throws.InstanceOf<JsonBindingException>());

		}

		[Test]
		public void Test_JsonDeserialize_Boolean()
		{
			// generic

#if DEPRECATED
			Assert.That(CrystalJson.DeserializeBoxed("true"), Is.True, "Deseralize('true')");
			Assert.That(CrystalJson.DeserializeBoxed("false"), Is.False, "Deseralize('false')");
#endif

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

			// must reject other types
			Assert.That(() => CrystalJson.Deserialize<bool>("null"), Throws.InstanceOf<JsonBindingException>(), "Deserialize<bool>('null')");
			Assert.That(() => CrystalJson.Deserialize<bool>("\"foo\""), Throws.InstanceOf<FormatException>(), "Deserialize<bool>('\"foo\"')");
			Assert.That(() => CrystalJson.Deserialize<bool>("{ }"), Throws.InstanceOf<JsonBindingException>(), "Deserialize<bool>('{ }')");
			Assert.That(() => CrystalJson.Deserialize<bool>("[ ]"), Throws.InstanceOf<JsonBindingException>(), "Deserialize<bool>('[ ]')");
		}

		[Test]
		public void Test_JsonDeserialize_String()
		{
			// generic deserialization

#if DEPRECATED
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
#endif

			// directed deserialization

			Assert.That(() => CrystalJson.Deserialize<string>("null"), Throws.InstanceOf<JsonBindingException>(), "Deseralize<string>('null')");
			Assert.That(CrystalJson.Deserialize<string?>("null", null), Is.Null, "Deseralize<string>('null')");
			Assert.That(CrystalJson.Deserialize<string?>(@"""Hello World""", null), Is.EqualTo("Hello World"), "Deseralize<string>('\"Hello World\"')");

			// with implicit conversion
			Assert.That(CrystalJson.Deserialize<string>("123"), Is.EqualTo("123"), "Deseralize<string>('123') (number!)");
			Assert.That(CrystalJson.Deserialize<string>("1.23"), Is.EqualTo("1.23"), "Deseralize<string>('1.23') (number!)");
			Assert.That(CrystalJson.Deserialize<string>("true"), Is.EqualTo("true"), "Deseralize<string>('true') (boolean!)");

			// must reject other type
			Assert.That(() => CrystalJson.Deserialize<string>("{ }"), Throws.InstanceOf<JsonBindingException>(), "Deserialize<string>('{ }')");
			Assert.That(() => CrystalJson.Deserialize<string>("[ ]"), Throws.InstanceOf<JsonBindingException>(), "Deserialize<string>('[ ]')");

			// an array of strings is NOT a string!
			Assert.That(() => CrystalJson.Deserialize<string>("[ \"foo\" ]"), Throws.InstanceOf<JsonBindingException>(), "Deserialize<string>('[ \"foo\" ]')");
		}

		[Test]
		public void Test_JsonDeserialize_Number()
		{
			// integers
			Assert.That(CrystalJson.Deserialize<int>("0"), Is.EqualTo(0));
			Assert.That(CrystalJson.Deserialize<int>("1"), Is.EqualTo(1));
			Assert.That(CrystalJson.Deserialize<int>("123"), Is.EqualTo(123));
			Assert.That(CrystalJson.Deserialize<int>("-1"), Is.EqualTo(-1));
			Assert.That(CrystalJson.Deserialize<int>("-123"), Is.EqualTo(-123));
			Assert.That(CrystalJson.Deserialize<int>("1E1"), Is.EqualTo(10));
			Assert.That(CrystalJson.Deserialize<int>("1E2"), Is.EqualTo(100));
			Assert.That(CrystalJson.Deserialize<int>("1.23E2"), Is.EqualTo(123));

			// double
			Assert.That(CrystalJson.Deserialize<double>("0"), Is.EqualTo(0));
			Assert.That(CrystalJson.Deserialize<double>("1"), Is.EqualTo(1));
			Assert.That(CrystalJson.Deserialize<double>("123"), Is.EqualTo(123));
			Assert.That(CrystalJson.Deserialize<double>("-1"), Is.EqualTo(-1));
			Assert.That(CrystalJson.Deserialize<double>("-123"), Is.EqualTo(-123));
			Assert.That(CrystalJson.Deserialize<double>("0.1"), Is.EqualTo(0.1));
			Assert.That(CrystalJson.Deserialize<double>("1.23"), Is.EqualTo(1.23));
			Assert.That(CrystalJson.Deserialize<double>("-0.1"), Is.EqualTo(-0.1));
			Assert.That(CrystalJson.Deserialize<double>("-1.23"), Is.EqualTo(-1.23));
			Assert.That(CrystalJson.Deserialize<double>("1E1"), Is.EqualTo(10));
			Assert.That(CrystalJson.Deserialize<double>("1E2"), Is.EqualTo(100));
			Assert.That(CrystalJson.Deserialize<double>("1.23E2"), Is.EqualTo(123));
			Assert.That(CrystalJson.Deserialize<double>("1E1"), Is.EqualTo(10));
			Assert.That(CrystalJson.Deserialize<double>("1E-1"), Is.EqualTo(0.1));
			Assert.That(CrystalJson.Deserialize<double>("1E-2"), Is.EqualTo(0.01));

			// special
			Assert.That(CrystalJson.Deserialize<int>("2147483647"), Is.EqualTo(int.MaxValue));
			Assert.That(CrystalJson.Deserialize<int>("-2147483648"), Is.EqualTo(int.MinValue));
			Assert.That(CrystalJson.Deserialize<long>("9223372036854775807"), Is.EqualTo(long.MaxValue));
			Assert.That(CrystalJson.Deserialize<long>("-9223372036854775808"), Is.EqualTo(long.MinValue));
			Assert.That(CrystalJson.Deserialize<float>("NaN"), Is.EqualTo(float.NaN));
			Assert.That(CrystalJson.Deserialize<float>("Infinity"), Is.EqualTo(float.PositiveInfinity));
			Assert.That(CrystalJson.Deserialize<float>("-Infinity"), Is.EqualTo(float.NegativeInfinity));
			Assert.That(CrystalJson.Deserialize<double>("NaN"), Is.EqualTo(double.NaN));
			Assert.That(CrystalJson.Deserialize<double>("Infinity"), Is.EqualTo(double.PositiveInfinity));
			Assert.That(CrystalJson.Deserialize<double>("-Infinity"), Is.EqualTo(double.NegativeInfinity));

			// decimal
			Assert.That(CrystalJson.Deserialize<decimal>("0"), Is.EqualTo(0m));
			Assert.That(CrystalJson.Deserialize<decimal>("1"), Is.EqualTo(1m));
			Assert.That(CrystalJson.Deserialize<decimal>("123"), Is.EqualTo(123m));
			Assert.That(CrystalJson.Deserialize<decimal>("1.23"), Is.EqualTo(1.23m));

			// implicit string conversion
			Assert.That(CrystalJson.Deserialize<decimal>("\"123\""), Is.EqualTo(123));
			Assert.That(CrystalJson.Deserialize<int>("\"123\""), Is.EqualTo(123));
			Assert.That(CrystalJson.Deserialize<long>("\"123\""), Is.EqualTo(123L));
			Assert.That(CrystalJson.Deserialize<float>("\"1.23\""), Is.EqualTo(1.23f));
			Assert.That(CrystalJson.Deserialize<double>("\"1.23\""), Is.EqualTo(1.23d));
			Assert.That(CrystalJson.Deserialize<float>("\"NaN\""), Is.EqualTo(float.NaN));
			Assert.That(CrystalJson.Deserialize<double>("\"NaN\""), Is.EqualTo(double.NaN));
			Assert.That(CrystalJson.Deserialize<float>("\"Infinity\""), Is.EqualTo(float.PositiveInfinity));
			Assert.That(CrystalJson.Deserialize<double>("\"Infinity\""), Is.EqualTo(double.PositiveInfinity));
			Assert.That(CrystalJson.Deserialize<float>("\"-Infinity\""), Is.EqualTo(float.NegativeInfinity));
			Assert.That(CrystalJson.Deserialize<double>("\"-Infinity\""), Is.EqualTo(double.NegativeInfinity));

			// must reject other types
			Assert.That(() => CrystalJson.Deserialize<int>("{ }"), Throws.InstanceOf<JsonBindingException>());
			Assert.That(() => CrystalJson.Deserialize<int>("[ ]"), Throws.InstanceOf<JsonBindingException>());

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
			// /!\ JsonDateTime has a resolution to the millisecond, but UtcNow has a resolution up to the 'tick', which mean we have to truncate the value to milliseconds or else it will not roundtrip properly
			var utcRoundTrip = CrystalJson.Deserialize<DateTime>(CrystalJson.Serialize(utcNow));
			Assert.That(utcRoundTrip, Is.EqualTo(utcNow), "RoundTrip DateTime.UtcNow");

			DateTime localNow = DateTime.Now;
			Assert.That(localNow.Kind, Is.EqualTo(DateTimeKind.Local));
			var localRoundTrip = CrystalJson.Deserialize<DateTime>(CrystalJson.Serialize(localNow));
			Assert.That(localRoundTrip, Is.EqualTo(localNow), "RoundTrip DateTime.Now");

			// direct deserialization
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

			// ensure that it roundtrips
			var now = NodaTime.SystemClock.Instance.GetCurrentInstant();
			Assert.That(CrystalJson.Deserialize<NodaTime.Instant>(CrystalJson.Serialize(now)), Is.EqualTo(now), "Instant roundtrip");

			// ensure that we can also read dates with offsets
			Assert.That(CrystalJson.Deserialize<NodaTime.Instant>("\"2015-07-17T00:00:00+02:00\""), Is.EqualTo(NodaTime.Instant.FromUtc(2015, 7, 16, 22, 0, 0)));
			Assert.That(CrystalJson.Deserialize<NodaTime.Instant>("\"2015-07-17T00:00:00-02:00\""), Is.EqualTo(NodaTime.Instant.FromUtc(2015, 7, 17, 2, 0, 0)));
			// and local dates
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

			// note: a ZonedDateTime is an Instant + DateTimeZone + Offset, but it can also be represented by an Instant (ticks) + a time zone ID
			// (http://stackoverflow.com/questions/14802672/serialize-nodatime-json#comment20786350_14830400)

			var dtz = NodaTime.DateTimeZoneProviders.Tzdb["Europe/Paris"];
			Assert.That(CrystalJson.Deserialize<NodaTime.ZonedDateTime>("\"0001-01-01T00:00:00Z UTC\""), Is.EqualTo(default(NodaTime.ZonedDateTime)));
			Assert.That(CrystalJson.Deserialize<NodaTime.ZonedDateTime>("\"1954-06-25T21:33:54.352+01:00 Europe/Paris\""), Is.EqualTo(new NodaTime.ZonedDateTime(NodaTime.Instant.FromUtc(1954, 06, 25, 20, 33, 54) + NodaTime.Duration.FromMilliseconds(352), dtz)));
			//note: if TZID is missing, it is impossible to deserialize a ZonedDatetime!
			Assert.That(() => CrystalJson.Deserialize<NodaTime.ZonedDateTime>("\"1954-06-25T21:33:54.352+01:00\""), Throws.InstanceOf<FormatException>(), "Missing TimeZone ID should fail");
			//note: if the offset is not valid for this date, it is not possible to deserialize a ZonedDatetime!
			Assert.That(() => CrystalJson.Deserialize<NodaTime.ZonedDateTime>("\"1854-06-25T21:33:54.352+01:00 Europe/Paris\""), Throws.InstanceOf<FormatException>(), "Paris was on a different offset in 1854 !");

			// ensure that it roundtrips
			var dtzNow = new NodaTime.ZonedDateTime(now, dtz);
			Assert.That(CrystalJson.Deserialize<NodaTime.ZonedDateTime>(CrystalJson.Serialize(dtzNow)), Is.EqualTo(dtzNow), "ZonedDateTime roundtripping");

			#endregion

			#region LocalDateTime

			Assert.That(CrystalJson.Deserialize<NodaTime.LocalDateTime>("\"0001-01-01T00:00:00\""), Is.EqualTo(default(NodaTime.LocalDateTime)));
			Assert.That(CrystalJson.Deserialize<NodaTime.LocalDateTime>("\"1854-06-25T21:33:54.352\""), Is.EqualTo(new NodaTime.LocalDateTime(1854, 06, 25, 21, 33, 54, 352)));
			Assert.That(CrystalJson.Deserialize<NodaTime.LocalDateTime>("\"-1254-06-25T21:33:54.352\""), Is.EqualTo(new NodaTime.LocalDateTime(-1254, 06, 25, 21, 33, 54, 352)));

			// ensure that it roundtrips
			var ldtNow = dtzNow.LocalDateTime;
			Assert.That(CrystalJson.Deserialize<NodaTime.LocalDateTime>(CrystalJson.Serialize(ldtNow)), Is.EqualTo(ldtNow), "LocalDatetime roundtripping");

			// ensure that we can deserialize an Instant into a local date time
			Assert.That(CrystalJson.Deserialize<NodaTime.LocalDateTime>("\"2017-06-21T12:34:56Z\""), Is.EqualTo(NodaTime.Instant.FromUtc(2017, 6, 21, 12, 34, 56).InZone(NodaTime.DateTimeZoneProviders.Tzdb.GetSystemDefault()).LocalDateTime));

			#endregion

			#region DateTimeZone

			var rnd = new Random();

			// from tzdb
			string id = NodaTime.DateTimeZoneProviders.Tzdb.Ids[rnd.Next(NodaTime.DateTimeZoneProviders.Tzdb.Ids.Count)];
			Assert.That(CrystalJson.Deserialize<NodaTime.DateTimeZone>(JsonEncoding.Encode(id)), Is.EqualTo(NodaTime.DateTimeZoneProviders.Tzdb.GetZoneOrNull(id)));

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
			{ // empty
				var res = CrystalJson.Deserialize<int[]>("[]");
				Assert.That(res, Has.Length.EqualTo(0));
				//Assert.That(res, Is.SameAs(Array.Empty<int[]>()));
			}
			{ // empty, with extra spaces
				var res = CrystalJson.Deserialize<int[]>("[\t\t \t]");
				Assert.That(res, Has.Length.EqualTo(0));
				//Assert.That(res, Is.SameAs(Array.Empty<int[]>()));
			}

			// single value
			Assert.That(CrystalJson.Deserialize<int[]>("[1]"), Is.EqualTo(new[] { 1 }));
			Assert.That(CrystalJson.Deserialize<int[]>("[ 1 ]"), Is.EqualTo(new[] { 1 }));

			// multiple value
			Assert.That(CrystalJson.Deserialize<int[]>("[1,2,3]"), Is.EqualTo(new[] { 1, 2, 3 }));
			Assert.That(CrystalJson.Deserialize<int[]>("[ 1, 2, 3 ]"), Is.EqualTo(new[] { 1, 2, 3 }));

			// strings
			Assert.That(CrystalJson.Deserialize<string[]>(@"[""foo"",""bar""]"), Is.EqualTo(new[] { "foo", "bar" }));

			// mixed array
			Assert.That(CrystalJson.Deserialize<object[]>(@"[123,true,""foo""]"), Is.EqualTo(new object[] { 123, true, "foo" }));

			// jagged arrays
			Assert.That(CrystalJson.Deserialize<object[]>(@"[ [1,2,3], [true,false], [""foo"",""bar""] ]"), Is.EqualTo(new object[] {
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
			Assert.That(CrystalJson.Deserialize<int[][]>("[[1,2],[3,4]]"), Is.EqualTo(new int[][] { [ 1, 2 ], [ 3, 4 ] }));

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
			//note: since 3.13, NUnit handles calls to IStructuralEquatable.Equals(...) with its own comparer (that does not merge char and string)
			// => we must pass our own custom comparer for this to work!
			Assert.That(CrystalJson.Deserialize<IVarTuple>("[ 123, \"Hello\", true, -1.5, \"Z\" ]"), Is.EqualTo(STuple.Create(123, "Hello", true, -1.5, 'Z')).Using(SimilarValueComparer.Default));
			Assert.That(CrystalJson.Deserialize<IVarTuple>("[ 123, \"Hello\", true, -1.5, \"Z\", \"World\" ]"), Is.EqualTo(STuple.Create(123, "Hello", true, -1.5, 'Z', "World")).Using(SimilarValueComparer.Default));
			Assert.That(CrystalJson.Deserialize<IVarTuple>("[ 123, \"Hello\", true, -1.5, \"Z\", \"World\", 456 ]"), Is.EqualTo(STuple.Create(123, "Hello", true, -1.5, 'Z', "World", 456)).Using(SimilarValueComparer.Default));
		}

		[Test]
		public void Test_JsonDeserialize_ValueTuples()
		{
			// ValueTuple
			Assert.That(CrystalJson.Deserialize<ValueTuple>("[ ]").Equals(ValueTuple.Create()), Is.True);

			// ValueTuple<...>
			Assert.That(CrystalJson.Deserialize<ValueTuple<int>>("[ 123 ]"), Is.EqualTo(ValueTuple.Create(123)));
			Assert.That(CrystalJson.Deserialize<ValueTuple<int, string>>("[ 123, \"Hello\" ]"), Is.EqualTo(ValueTuple.Create(123, "Hello")));
			Assert.That(CrystalJson.Deserialize<ValueTuple<int, string, bool>>("[ 123, \"Hello\", true ]"), Is.EqualTo(ValueTuple.Create(123, "Hello", true)));
			Assert.That(CrystalJson.Deserialize<ValueTuple<int, string, bool, double>>("[ 123, \"Hello\", true, -1.5 ]"), Is.EqualTo(ValueTuple.Create(123, "Hello", true, -1.5)));
			Assert.That(CrystalJson.Deserialize<ValueTuple<int, string, bool, double, char>>("[ 123, \"Hello\", true, -1.5, \"Z\" ]"), Is.EqualTo(ValueTuple.Create(123, "Hello", true, -1.5, 'Z')));
			Assert.That(CrystalJson.Deserialize<ValueTuple<int, string, bool, double, char, string>>("[ 123, \"Hello\", true, -1.5, \"Z\", \"World\" ]"), Is.EqualTo(ValueTuple.Create(123, "Hello", true, -1.5, 'Z', "World")));
			Assert.That(CrystalJson.Deserialize<ValueTuple<int, string, bool, double, char, string, int>>("[ 123, \"Hello\", true, -1.5, \"Z\", \"World\", 456 ]"), Is.EqualTo(ValueTuple.Create(123, "Hello", true, -1.5, 'Z', "World", 456)));
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

			// we also must accept the syntax with brackets (ex: '[::1]') for IPv6 because it is found in some URLs
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
			{
				var res = CrystalJson.Deserialize<IDictionary<string, object>>("{}");
				Assert.That(res, Has.Count.EqualTo(0));
			}
			{
				var res = CrystalJson.Deserialize<IDictionary<string, object>>("{\r\n\t\t \r\n}");
				Assert.That(res, Has.Count.EqualTo(0));
			}
			{
				var res = CrystalJson.Deserialize<IDictionary<string, object>>("""{ "Name":"James Bond" }""");
				Assert.That(res, Has.Count.EqualTo(1));
				Assert.That(res.ContainsKey("Name"), Is.True);
				Assert.That(res["Name"], Is.EqualTo("James Bond"));
			}
			{
				var res = CrystalJson.Deserialize<IDictionary<string, object>>("""{ "Id":7, "Name":"James Bond", "IsDeadly":true }""");
				Assert.That(res, Has.Count.EqualTo(3));
				Assert.That(res["Name"], Is.EqualTo("James Bond"));
				Assert.That(res["Id"], Is.EqualTo(7));
				Assert.That(res["IsDeadly"], Is.True);
			}
			{
				var res = CrystalJson.Deserialize<IDictionary<string, object>>("""{ "Id":7, "Name":"James Bond", "IsDeadly":true, "Created":"\/Date(-52106400000+0200)\/", "Weapons":[{"Name":"Walter PPK"}] }""");
				Assert.That(res, Has.Count.EqualTo(5));
				Assert.That(res["Name"], Is.EqualTo("James Bond"));
				Assert.That(res["Id"], Is.EqualTo(7));
				Assert.That(res["IsDeadly"], Is.True);
				//Assert.That(res["Created"], Is.EqualTo(new DateTime(1968, 5, 8)));
				Assert.That(res["Created"], Is.EqualTo("/Date(-52106400000+0200)/")); //BUGBUG: handle the auto-detection of dates when converting from string to object ?
				var weapons = (IList<object>) res["Weapons"];
				Assert.That(weapons, Is.Not.Null);
				Assert.That(weapons, Has.Count.EqualTo(1));
				var weapon = (IDictionary<string, object>) weapons[0];
				Assert.That(weapon, Is.Not.Null);
				Assert.That(weapon["Name"], Is.EqualTo("Walter PPK"));
			}
		}

		[Test]
		public void Test_JsonDeserialize_CustomClass()
		{
			string jsonText = """{ "Valid": true, "Name": "James Bond", "Index": 7, "Size": 123456789, "Height": 1.8, "Amount": 0.07, "Created": "1968-05-08T00:00:00Z", "Modified": "2010-10-28T15:39:00Z", "DateOfBirth": "1920-11-11", "State": 42, "RatioOfStuff": 8641975.23 }""";
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
			Assert.That(x.DateOfBirth, Is.EqualTo(new DateOnly(1920, 11, 11)), "x.DateOfBirth");
			Assert.That(x.State, Is.EqualTo(DummyJsonEnum.Bar), "x.State");
			Assert.That(x.RatioOfStuff, Is.EqualTo(0.07d * 123456789), "x.RatioOfStuff");

			// it should round trip!
			string roundtripText = CrystalJson.Serialize(x);
			Assert.That(roundtripText, Is.EqualTo(jsonText), "LOOP 2!");
			var hibachi = CrystalJson.Deserialize<DummyJsonClass>(roundtripText);
			Assert.That(hibachi, Is.EqualTo(x), "TRUE LAST BOSS !!!");
		}

		[Test]
		public void Test_JsonDeserialize_CustomClass_Polymorphic()
		{
			{ // without any "$type" field, we will bind using the type specified in the parent container, if it is constructible (not abstract, not an interface)
				string jsonText = """{ "Valid": true, "Name": "James Bond", "Index": 7, "Size": 123456789, "Height": 1.8, "Amount": 0.07, "Created": "1968-05-08T00:00:00Z", "Modified": "2010-10-28T15:39:00Z", "DateOfBirth": "1920-11-11", "State": 42, "RatioOfStuff": 8641975.23 }""";
				var x = CrystalJson.Deserialize<DummyJsonBaseClass>(jsonText);
				Assert.That(x, Is.Not.Null, jsonText);
				Assert.That(x, Is.InstanceOf<DummyJsonBaseClass>());

				Assert.That(x.Valid, Is.True, "x.Valid");
				Assert.That(x.Name, Is.EqualTo("James Bond"), "x.Name");
				Assert.That(x.Index, Is.EqualTo(7), "x.Index");
				Assert.That(x.Size, Is.EqualTo(123456789), "x.Size");
				Assert.That(x.Height, Is.EqualTo(1.8f), "x.Height");
				Assert.That(x.Amount, Is.EqualTo(0.07d), "x.Amount");
				Assert.That(x.Created, Is.EqualTo(new DateTime(1968, 5, 8)), "x.Created");
				Assert.That(x.Modified, Is.EqualTo(new DateTime(2010, 10, 28, 15, 39, 0)), "x.Modified");
				Assert.That(x.DateOfBirth, Is.EqualTo(new DateOnly(1920, 11, 11)), "x.DateOfBirth");
				Assert.That(x.State, Is.EqualTo(DummyJsonEnum.Bar), "x.State");
				Assert.That(x.RatioOfStuff, Is.EqualTo(0.07d * 123456789), "x.RatioOfStuff");
			}

			{ // with a "$type" field, we should construct the exact type
				string jsonText = """{ "$type": "agent", "Valid": true, "Name": "James Bond", "Index": 7, "Size": 123456789, "Height": 1.8, "Amount": 0.07, "Created": "1968-05-08T00:00:00Z", "Modified": "2010-10-28T15:39:00Z", "DateOfBirth": "1920-11-11", "State": 42, "RatioOfStuff": 8641975.23 }""";
				var x = CrystalJson.Deserialize<DummyJsonBaseClass>(jsonText);
				Assert.That(x, Is.Not.Null, jsonText);
				Assert.That(x, Is.InstanceOf<DummyJsonBaseClass>());

				Assert.That(x.Valid, Is.True, "x.Valid");
				Assert.That(x.Name, Is.EqualTo("James Bond"), "x.Name");
				Assert.That(x.Index, Is.EqualTo(7), "x.Index");
				Assert.That(x.Size, Is.EqualTo(123456789), "x.Size");
				Assert.That(x.Height, Is.EqualTo(1.8f), "x.Height");
				Assert.That(x.Amount, Is.EqualTo(0.07d), "x.Amount");
				Assert.That(x.Created, Is.EqualTo(new DateTime(1968, 5, 8)), "x.Created");
				Assert.That(x.Modified, Is.EqualTo(new DateTime(2010, 10, 28, 15, 39, 0)), "x.Modified");
				Assert.That(x.DateOfBirth, Is.EqualTo(new DateOnly(1920, 11, 11)), "x.DateOfBirth");
				Assert.That(x.State, Is.EqualTo(DummyJsonEnum.Bar), "x.State");
				Assert.That(x.RatioOfStuff, Is.EqualTo(0.07d * 123456789), "x.RatioOfStuff");

				// it should round trip!
				string roundtripText = CrystalJson.Serialize(x);
				Assert.That(roundtripText, Is.EqualTo(jsonText), "LOOP 2!");
				var hibachi = CrystalJson.Deserialize<DummyJsonBaseClass>(roundtripText);
				Assert.That(hibachi, Is.EqualTo(x), "TRUE LAST BOSS !!!");
			}

			{ // with a "$type" field, we should construct the exact type (again, with a more derived type)
				string jsonText = """{ "$type": "spy", "DoubleAgentName": "Janov Bondovicz", "Valid": true, "Name": "James Bond", "Index": 7, "Size": 123456789, "Height": 1.8, "Amount": 0.07, "Created": "1968-05-08T00:00:00Z", "Modified": "2010-10-28T15:39:00Z", "DateOfBirth": "1920-11-11", "State": 42, "RatioOfStuff": 8641975.23 }""";
				var x = CrystalJson.Deserialize<DummyDerivedJsonClass>(jsonText);
				Assert.That(x, Is.Not.Null, jsonText);
				Assert.That(x, Is.InstanceOf<DummyJsonBaseClass>());

				Assert.That(x.Valid, Is.True, "x.Valid");
				Assert.That(x.Name, Is.EqualTo("James Bond"), "x.Name");
				Assert.That(x.Index, Is.EqualTo(7), "x.Index");
				Assert.That(x.Size, Is.EqualTo(123456789), "x.Size");
				Assert.That(x.Height, Is.EqualTo(1.8f), "x.Height");
				Assert.That(x.Amount, Is.EqualTo(0.07d), "x.Amount");
				Assert.That(x.Created, Is.EqualTo(new DateTime(1968, 5, 8)), "x.Created");
				Assert.That(x.Modified, Is.EqualTo(new DateTime(2010, 10, 28, 15, 39, 0)), "x.Modified");
				Assert.That(x.DateOfBirth, Is.EqualTo(new DateOnly(1920, 11, 11)), "x.DateOfBirth");
				Assert.That(x.State, Is.EqualTo(DummyJsonEnum.Bar), "x.State");
				Assert.That(x.RatioOfStuff, Is.EqualTo(0.07d * 123456789), "x.RatioOfStuff");
				Assert.That(x.DoubleAgentName, Is.EqualTo("Janov Bondovicz"));

				// it should round trip!
				string roundtripText = CrystalJson.Serialize(x);
				Assert.That(roundtripText, Is.EqualTo(jsonText), "LOOP 2!");
				var hibachi = CrystalJson.Deserialize<DummyJsonBaseClass>(roundtripText);
				Assert.That(hibachi, Is.EqualTo(x), "TRUE LAST BOSS !!!");
			}

		}

		[Test]
		public void Test_JsonDeserialize_CustomStruct()
		{
			string jsonText = "{ \"Valid\": true, \"Name\": \"James Bond\", \"Index\": 7, \"Size\": 123456789, \"Height\": 1.8, \"Amount\": 0.07, \"Created\": \"1968-05-08T00:00:00Z\", \"Modified\": \"2010-10-28T15:39:00Z\", \"DateOfBirth\": \"1920-11-11\", \"State\": 42, \"RatioOfStuff\": 8641975.23 }";
			var x = CrystalJson.Deserialize<DummyJsonStruct>(jsonText);
			Assert.That(x, Is.InstanceOf<DummyJsonStruct>());

			Assert.That(x.Valid, Is.True, "x.Valid");
			Assert.That(x.Name, Is.EqualTo("James Bond"), "x.Name");
			Assert.That(x.Index, Is.EqualTo(7), "x.Index");
			Assert.That(x.Size, Is.EqualTo(123456789), "x.Size");
			Assert.That(x.Height, Is.EqualTo(1.8f), "x.Height");
			Assert.That(x.Amount, Is.EqualTo(0.07d), "x.Amount");
			Assert.That(x.Created, Is.EqualTo(new DateTime(1968, 5, 8)), "x.Created");
			Assert.That(x.Modified, Is.EqualTo(new DateTime(2010, 10, 28, 15, 39, 0)), "x.Modified");
			Assert.That(x.DateOfBirth, Is.EqualTo(new DateOnly(1920, 11, 11)), "x.DateOfBirth");
			Assert.That(x.State, Is.EqualTo(DummyJsonEnum.Bar), "x.State");
			Assert.That(x.RatioOfStuff, Is.EqualTo(0.07d * 123456789), "x.RatioOfStuff");

			// round trip !
			string roundtripText = CrystalJson.Serialize(x);
			Assert.That(roundtripText, Is.EqualTo(jsonText), "LOOP 2!");
			var hibachi = CrystalJson.Deserialize<DummyJsonStruct>(roundtripText);
			Assert.That(hibachi, Is.EqualTo(x), "TRUE LAST BOSS !!!");
		}

#if DEPRECATED

		[Test]
		public void Test_JsonDeserialize_EvilGadgetObject()
		{
			//note: this test uses System.Windows.Data.ObjectDataProvider, which may not be available in the latest .NET runtime...
			//TODO: REVIEW: are there any other type of gadgets that works on .NET 8/9 and windows/linux?

			string token = Guid.NewGuid().ToString();

			string path = GetTemporaryPath(token + ".txt");
			if (File.Exists(path)) File.Delete(path);
			Log("# Canary location:");
			Log(path);

			Log("# Gadget:");
			var jsonGadget = new JsonObject()
			{
				["_class"] = "System.Windows.Data.ObjectDataProvider, PresentationFramework, Version=4.0.0.0, Culture=neutral, PublicKeyToken=31bf3856ad364e35",
				["ObjectInstance"] = new JsonObject()
				{
					["_class"] = "System.Diagnostics.Process, System, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089",
					["StartInfo"] = new JsonObject()
					{
						["_class"] = "System.Diagnostics.ProcessStartInfo, System, Version = 4.0.0.0, Culture = neutral, PublicKeyToken = b77a5c561934e089",
						["FileName"] = "cmd",
						["Arguments"] = $@"""/c echo foo > {path}""",
					}
				},
				["MethodName"] = "Start",
			};

			Dump(jsonGadget);
			Log();
			try
			{
				Log("# Deserializing...");
				var gadget = jsonGadget.Bind(typeof(object))!;

				// ATTENTION: ATTENTION: CRITICAL FAILURE!
				// => if the deserialization does not fail here, it means we are able to execute arbitrary injected code when deserializing JSON!!!

				Log($"FATAL: Created a {gadget.GetType()}");

				// note: the command is executed asynchronously, so we need to wait a bit...
				Thread.Sleep(2000);

				if (File.Exists(path))
				{
					// ATTENTION: ATTENTION: CRITICAL FAILURE!
					// => if the test fails here, it means we are able to execute arbitrary injected code when deserializing JSON!!!
					Assert.Fail("FATAL: deserialization of bad JSON was able to execute the payload!!!!");
				}
				else
				{
					Assert.Fail("Deserialization should have failed but did not produce any observable result... THIS IS STILL A BAD THING!");
				}
			}
			catch (Exception e)
			{
				Log($"# Binding attempt failed as expected : [{e.GetType().Name}] {e.Message}");
				// ATTENTION: ATTENTION: CRITICAL FAILURE!
				// if this is not the expected error type, it could be because the exploit failed to run properly, which is still a HUGE issue!
				Assert.That(e, Is.InstanceOf<JsonBindingException>(), "Deserialization should have failed with an expected JSON error");
			}
		}

		public abstract class DummyBaseClass;

		public class DummyFooClass : DummyBaseClass;

		public class DummyBarClass; // does NOT derive from DummyBaseClass

		[Test]
		public void Test_JsonDeserialize_Incompatible_Type_In_Property()
		{
			// to attempt to block deserialization vulnerabilities (see Test_JsonDeserialize_EvilGadgetObject),
			// me must also verify that the binding of a type that does not match the expected base class will fail "as expected"

			var json = CrystalJson.Serialize<DummyBaseClass>(new DummyFooClass());
			Log(json);

			// if we ask to deserialize an incompatible type, it should throw a JsonBindingException
			Assert.That(() => CrystalJson.Deserialize<DummyBarClass>(json), Throws.InstanceOf<JsonBindingException>());
			// but not if we expected an "object", in which case we have no real way to know...
			Assert.That(CrystalJson.Deserialize<object>(json), Is.Not.Null);
			//note: another hint that "ToObject" serialization should be banned?
		}

#endif

		#endregion

		#region Streaming...

		[Test]
		public async Task Test_Can_Stream_Arrays_To_Disk()
		{
			var cancel = CancellationToken.None;
			var rnd = new Random();

			// empty
			var path = GetTemporaryPath("null.json");
			await using (var fs = File.Create(path))
			{
				await using (new CrystalJsonStreamWriter(fs, CrystalJsonSettings.Json, null, ownStream: false))
				{
					//nothing
				}
				Assert.That(fs.CanWrite, Is.True, "Stream should be kept open (ownStream: false)");
			}
			var arr = CrystalJson.LoadFrom<List<int>>(path);
			Assert.That(arr, Is.Null);

			// empty batch
			path = GetTemporaryPath("empty.json");
			await using (var fs = File.Create(path))
			{
				await using (var stream = new CrystalJsonStreamWriter(fs, CrystalJsonSettings.Json, null, ownStream: true))
				{
					using (stream.BeginArrayFragment())
					{
						//no-op
					}

					await stream.FlushAsync(cancel);
				}
				Assert.That(fs.CanWrite, Is.False, "Stream should have been closed (ownStream: true)");
			}
			arr = CrystalJson.LoadFrom<List<int>>(path)!;
			Assert.That(arr, Is.Not.Null);
			Assert.That(arr, Is.Empty);

			// single element
			path = GetTemporaryPath("forty_two.json");
			await using (var stream = CrystalJsonStreamWriter.Create(path))
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

			// single batch
			path = GetTemporaryPath("one_batch.json");
			await using (var stream = CrystalJsonStreamWriter.Create(path))
			{
				await stream.WriteArrayFragmentAsync(Enumerable.Range(0, 1000), cancel);
			}
			arr = CrystalJson.LoadFrom<List<int>>(path);
			Assert.That(arr, Is.Not.Null);
			Assert.That(arr, Is.EqualTo(Enumerable.Range(0, 1000)));

			// multiple batches
			path = GetTemporaryPath("multiple_batchs.json");
			await using (var stream = CrystalJsonStreamWriter.Create(path))
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

			// changes types during the sequence (first 500 are ints, next 500 are longs)
			path = GetTemporaryPath("int_long.json");
			await using (var stream = CrystalJsonStreamWriter.Create(path))
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
			await using (var stream = CrystalJsonStreamWriter.Create(path))
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
			await using (var stream = CrystalJsonStreamWriter.Create(path))
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
			await using (var fs = File.Create(path + ".gz"))
			await using (var gz = new GZipStream(fs, CompressionMode.Compress, false))
			await using (var stream = new CrystalJsonStreamWriter(gz, CrystalJsonSettings.Json))
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
		public async Task Test_Can_Stream_Multiple_Fragments_To_Disk()
		{
			var cancel = CancellationToken.None;
			var rnd = new Random();

			// empty batch
			Log("Saving empty batches...");
			var path = GetTemporaryPath("three_empty_objects.json");
			await using (var writer = CrystalJsonStreamWriter.Create(path, CrystalJsonSettings.Json))
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

			// object meta + data series
			path = GetTemporaryPath("device.json");
			Log("Saving multi-fragments 'export'...");
			await using (var writer = CrystalJsonStreamWriter.Create(path, CrystalJsonSettings.Json))
			{
				var metric = new {
					Id = "123ABC",
					Vendor = "ACME",
					Model = "HAL 9001",
					Metrics = new[] { "Foo", "Bar", "Baz" },
				};

				// first obj = meta containing an array of ids
				await writer.WriteFragmentAsync(metric, cancel);

				// next, une array for each id in the first array
				foreach(var _ in metric.Metrics)
				{
					using var arr = writer.BeginArrayFragment(cancel);

					await arr.WriteBatchAsync(Enumerable.Range(0, 10).Select(_ => KeyValuePair.Create(Stopwatch.GetTimestamp(), rnd.Next())));
				}
			}
			Log($"> saved {new FileInfo(path).Length:N0} bytes");

			// read back
			Log("> reloading...");
			using (var reader = CrystalJsonStreamReader.Open(path))
			{
				// metrics meta
				Log("> Readining metadata object...");
				var frag = reader.ReadNextFragment()!;
				DumpCompact(frag);
				Assert.That(frag, Is.Not.Null);
				Assert.That(frag.Type, Is.EqualTo(JsonType.Object));
				var m = (JsonObject)frag;
				Assert.That(m.Get<string>("Id"), Is.EqualTo("123ABC"));
				Assert.That(m.Get<string>("Vendor"), Is.EqualTo("ACME"));
				Assert.That(m.Get<string>("Model"), Is.EqualTo("HAL 9001"));
				Assert.That(m.Get<string[]>("Metrics"), Has.Length.EqualTo(3));

				// metrics value
				foreach (var id in m.GetArray("Metrics").Cast<string>())
				{
					Log($"> Readining batch for {id}...");
					frag = reader.ReadNextFragment()!;
					DumpCompact(frag);
					Assert.That(frag, Is.Not.Null);
					Assert.That(frag.Type, Is.EqualTo(JsonType.Array));
					var a = (JsonArray)frag;
					Log($"> {a.Count}");
					Assert.That(a, Has.Count.EqualTo(10));
				}

				// end of file
				frag = reader.ReadNextFragment();
				Assert.That(frag, Is.Null);
			}

		}

#endregion

	}

	#region Helper Classes

	public enum DummyJsonEnum
	{
		None,
		Foo = 1,
		Bar = 42,
	}

	[Flags]
	public enum DummyJsonEnumFlags
	{
		None,
		Foo = 1,
		Bar = 2,
		Narf = 4
	}

	public enum DummyJsonEnumInt32
	{
		None,
		One = 1,
		Two = 2,
		MaxValue = 65535
	}

	public enum DummyJsonEnumShort : ushort
	{
		None,
		One = 1,
		Two = 2,
		MaxValue = 65535
	}

	public enum DummyJsonEnumInt64 : long
	{
		None,
		One = 1,
		Two = 2,
		MaxValue = long.MaxValue
	}

	public enum DummyJsonEnumTypo
	{
		None,
		Foo,
		Bar = 2,   // new name, with correct spelling
		Barrh = 2, // old name, with a typo, but is still referenced (in old code, in old JSON documents, etc...)
		Baz
	}

#pragma warning disable 169, 649
	public struct DummyJsonStruct
	{
		public bool Valid;
		public string Name;
		public int Index;
		public long Size;
		public float Height;
		public double Amount;
		public DateTime Created;
		public DateTime? Modified;
		public DateOnly? DateOfBirth;
		public DummyJsonEnum State;
		public readonly double RatioOfStuff => this.Amount * this.Size;

		private string Invisible;
		private readonly string DotNotCall => "ShouldNotBeCalled";
	}
#pragma warning restore 169, 649

	public struct DummyNullableStruct
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

	public class DummyJsonClass
	{
		private string m_invisible = "ShoudNotBeVisible";
		private string? m_name;
		public bool Valid => m_name is not null;
		public string? Name { get => m_name; set => m_name = value; }
		public int Index { get; set; }
		public long Size { get; set; }
		public float Height { get; set; }
		public double Amount { get; set; }
		public DateTime Created { get; set; }
		public DateTime? Modified { get; set; }
		public DateOnly? DateOfBirth { get; set; }
		public DummyJsonEnum State { get; set; }

		public double RatioOfStuff => this.Amount * this.Size;

		// ReSharper disable once UnusedMember.Local
		private string Invisible => m_invisible;

		public string MustNotBeCalled() { return "ShouldNotBeCalled"; }

		public override bool Equals(object? obj)
		{
			if (obj is not DummyJsonClass other) return false;
			return this.Index == other.Index
			       && m_name == other.m_name
			       && this.Size == other.Size
			       && this.Height == other.Height
			       && this.Amount == other.Amount
			       && this.Created == other.Created
			       && this.Modified == other.Modified
			       && this.State == other.State;
		}

		public override int GetHashCode()
		{
			// ReSharper disable once NonReadonlyMemberInGetHashCode
			return this.Index;
		}
	}

	[STJ.Serialization.JsonPolymorphic()]
	[STJ.Serialization.JsonDerivedType(typeof(DummyJsonBaseClass), "agent")]
	[STJ.Serialization.JsonDerivedType(typeof(DummyDerivedJsonClass), "spy")]
	public interface IDummyCustomInterface
	{
		string? Name { get; }
		int Index { get; }
		long Size { get; }
		float Height { get; }
		double Amount { get; }
		DateTime Created { get; }
		DateTime? Modified { get; }
		DummyJsonEnum State { get; }
	}

	public class DummyOuterClass
	{
		public int Id { get; set; }
		public IDummyCustomInterface Agent { get; set; }
	}

	public class DummyOuterDerivedClass
	{
		public int Id { get; set; }
		public DummyJsonBaseClass Agent { get; set; }
	}

	public class DummyJsonBaseClass : IDummyCustomInterface
	{
		// same as "DummyJsonClass", but part of a polymorphic chain

		private string m_invisible = "ShoudNotBeVisible";
		private string? m_name;
		public bool Valid => m_name is not null;
		public string? Name { get => m_name; set => m_name = value; }
		public int Index { get; set; }
		public long Size { get; set; }
		public float Height { get; set; }
		public double Amount { get; set; }
		public DateTime Created { get; set; }
		public DateTime? Modified { get; set; }
		public DateOnly? DateOfBirth { get; set; }
		public DummyJsonEnum State { get; set; }

		public double RatioOfStuff => this.Amount * this.Size;

		// ReSharper disable once UnusedMember.Local
		private string Invisible => m_invisible;

		public string MustNotBeCalled() { return "ShouldNotBeCalled"; }

		public override bool Equals(object? obj)
		{
			if (obj is not DummyJsonBaseClass other) return false;
			return this.Index == other.Index
			       && m_name == other.m_name
			       && this.Size == other.Size
			       && this.Height == other.Height
			       && this.Amount == other.Amount
			       && this.Created == other.Created
			       && this.Modified == other.Modified
			       && this.State == other.State;
		}

		public override int GetHashCode()
		{
			// ReSharper disable once NonReadonlyMemberInGetHashCode
			return this.Index;
		}
	}

	public class DummyDerivedJsonClass : DummyJsonBaseClass
	{

		private string? m_doubleAgentName;

		public DummyDerivedJsonClass() { }

		public DummyDerivedJsonClass(string doubleAgentName)
		{
			m_doubleAgentName = doubleAgentName;
		}

		public string? DoubleAgentName { get => m_doubleAgentName; set => m_doubleAgentName = value; }
	}

	public sealed class DummyJsonCustomClass : IJsonSerializable, IJsonPackable, IJsonDeserializable<DummyJsonCustomClass>
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
			Assert.That(writer, Is.Not.Null);
			Assert.That(writer.Settings, Is.Not.Null);
			Assert.That(writer.Resolver, Is.Not.Null);
			writer.WriteRaw("{ \"custom\":" + JsonEncoding.Encode(m_secret) + " }");
		}

		#endregion

		#region IJsonBindable Members

		JsonValue IJsonPackable.JsonPack(CrystalJsonSettings settings, ICrystalJsonTypeResolver resolver)
		{
			Assert.That(settings, Is.Not.Null, "settings");
			Assert.That(resolver, Is.Not.Null, "resolver");

			return JsonObject.Create("custom", m_secret);
		}

		static DummyJsonCustomClass IJsonDeserializable<DummyJsonCustomClass>.JsonDeserialize(JsonValue value, ICrystalJsonTypeResolver? resolver)
		{
			Assert.That(value, Is.Not.Null, "value");
			Assert.That(resolver, Is.Not.Null, "resolver");

			Assert.That(value.Type, Is.EqualTo(JsonType.Object));
			var obj = (JsonObject)value;

			var secret = obj.Get<string>("custom", message: "Missing 'custom' value for DummyCustomJson");
			return new DummyJsonCustomClass(secret);
		}

		#endregion

	}

	public sealed class DummyStaticLegacyJson
	{
		public string DontCallThis => "ShouldNotSeeThat";

		private DummyStaticLegacyJson()
		{ }

		public DummyStaticLegacyJson(string secret)
		{
			m_secret = secret;
		}

		private string m_secret;

		public string GetSecret() { return m_secret; }

		#region IJsonSerializable Members

		/// <summary>Méthode static utilisée pour sérialiser un objet</summary>
		/// <param name="instance"></param>
		/// <param name="writer"></param>
		public static void JsonSerialize(DummyStaticLegacyJson instance, CrystalJsonWriter writer)
		{
			Assert.That(writer, Is.Not.Null);
			Assert.That(writer.Settings, Is.Not.Null);
			Assert.That(writer.Resolver, Is.Not.Null);
			writer.WriteRaw("{ \"custom\":" + JsonEncoding.Encode(instance.m_secret) + " }");
		}

		/// <summary>Méthode statique utilisée pour désérialiser un objet</summary>
		public static DummyStaticLegacyJson JsonDeserialize(JsonObject value, ICrystalJsonTypeResolver resolver)
		{
			Assert.That(value, Is.Not.Null, "value");

			// doit contenir une string "custom"
			var customString = value.Get<string>("custom", message: "Missing 'custom' value for DummyCustomJson");
			return new DummyStaticLegacyJson(customString);

		}

		#endregion
	}

	public sealed record DummyStaticCustomJson : IJsonSerializable, IJsonDeserializable<DummyStaticCustomJson>
	{
		public string DontCallThis => "ShouldNotSeeThat";

		public DummyStaticCustomJson(string secret)
		{
			m_secret = secret;
		}

		private string m_secret;

		public string GetSecret() { return m_secret; }

		#region IJsonSerializable Members

		void IJsonSerializable.JsonSerialize(CrystalJsonWriter writer)
		{
			Assert.That(writer, Is.Not.Null);
			Assert.That(writer.Settings, Is.Not.Null);
			Assert.That(writer.Resolver, Is.Not.Null);

			writer.WriteRaw("{ \"custom\":" + JsonEncoding.Encode(m_secret) + " }");
		}

		static DummyStaticCustomJson IJsonDeserializable<DummyStaticCustomJson>.JsonDeserialize(JsonValue value, ICrystalJsonTypeResolver? _)
		{
			Assert.That(value, Is.Not.Null);

			var customString = value.Get<string>("custom", message: "Missing 'custom' value for DummyCustomJson");
			return new DummyStaticCustomJson(customString);
		}

		#endregion
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

		// no attributes!
		public string InvisibleField;

		[DataMember]
		public string CurrentLoveInterest { get; set; }

		[DataMember]
		public string VisibleProperty => "CanBeSeen";

		// no attributes!
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

	public sealed class DummyCrystalJsonTextPropertyNames
	{
		// test that we recognize our own JsonPropertyAttribute

		[JsonProperty("helloWorld")]
		public string? HelloWorld { get; set; }

		[JsonProperty("bar")]
		public string? Foo { get; set; }
	}

	public sealed class DummySystemJsonTextPropertyNames
	{
		// test that we recognize System.Text.Json.Serialization.JsonPropertyNameAttribute

		[STJ.Serialization.JsonPropertyName("helloWorld")]
		public string? HelloWorld { get; set; }

		[STJ.Serialization.JsonPropertyName("bar")]
		public string? Foo { get; set; }
	}

	public sealed class DummyNewtonsoftJsonPropertyNames
	{
		// test that we recognize Newtonsoft.Json.JsonPropertyAttribute

		[NJ.JsonProperty("helloWorld")]
		public string? HelloWorld { get; set; }

		[NJ.JsonProperty("bar")]
		public string? Foo { get; set; }
	}

	#endregion

}
