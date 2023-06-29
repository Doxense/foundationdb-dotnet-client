#region Copyright (c) 2005-2023 Doxense SAS
//
// All rights are reserved. Reproduction or transmission in whole or in part, in
// any form or by any means, electronic, mechanical or otherwise, is prohibited
// without the prior written consent of the copyright owner.
//
#endregion

// ReSharper disable CommentTypo
// ReSharper disable StringLiteralTypo
namespace Doxense.Tools.Tests
{
	using System;
	using System.Globalization;
	using NUnit.Framework;
	using Doxense.Serialization;
	using Doxense.Testing;

	[TestFixture]
	[Category("Core-SDK")]
	public class StringConvertersFacts : DoxenseTest
	{

		[Test]
		public void Test_ToInt64()
		{
			// success
			Assert.That(StringConverters.ToInt64("0", 666), Is.EqualTo(0));
			Assert.That(StringConverters.ToInt64("1", 666), Is.EqualTo(1));
			Assert.That(StringConverters.ToInt64("777", 666), Is.EqualTo(777));
			Assert.That(StringConverters.ToInt64("22091978", 666), Is.EqualTo(22091978));
			Assert.That(StringConverters.ToInt64("09221978", 666), Is.EqualTo(9221978));
			Assert.That(StringConverters.ToInt64("10737418235", 666), Is.EqualTo(10737418235));
			Assert.That(StringConverters.ToInt64("-273", 666), Is.EqualTo(-273));
			Assert.That(StringConverters.ToInt64("+273", 666), Is.EqualTo(273));
			Assert.That(StringConverters.ToInt64("-0", 666), Is.EqualTo(0));
			Assert.That(StringConverters.ToInt64("+0", 666), Is.EqualTo(0));
			Assert.That(StringConverters.ToInt64(" 456", 666), Is.EqualTo(456));
			Assert.That(StringConverters.ToInt64("456 ", 666), Is.EqualTo(456));
			Assert.That(StringConverters.ToInt64(" 456 ", 666), Is.EqualTo(456));

			// failures
			Assert.That(StringConverters.ToInt64(null, 666), Is.EqualTo(666));
			Assert.That(StringConverters.ToInt64("", 666), Is.EqualTo(666));
			Assert.That(StringConverters.ToInt64("A", 666), Is.EqualTo(666));
			Assert.That(StringConverters.ToInt64(".", 666), Is.EqualTo(666));
			Assert.That(StringConverters.ToInt64("-", 666), Is.EqualTo(666));
			Assert.That(StringConverters.ToInt64("+", 666), Is.EqualTo(666));
			Assert.That(StringConverters.ToInt64("true", 666), Is.EqualTo(666));
			Assert.That(StringConverters.ToInt64("false", 666), Is.EqualTo(666));
			Assert.That(StringConverters.ToInt64("narfzort", 666), Is.EqualTo(666));
			Assert.That(StringConverters.ToInt64(".123", 666), Is.EqualTo(666));
			Assert.That(StringConverters.ToInt64("=123", 666), Is.EqualTo(666));
			Assert.That(StringConverters.ToInt64("123A", 666), Is.EqualTo(666));
			Assert.That(StringConverters.ToInt64("123 A", 666), Is.EqualTo(666));
		}

		[Test]
		public void Test_ToInt32()
		{
			// success
			Assert.That(StringConverters.ToInt32("0", 666), Is.EqualTo(0));
			Assert.That(StringConverters.ToInt32("1", 666), Is.EqualTo(1));
			Assert.That(StringConverters.ToInt32("777", 666), Is.EqualTo(777));
			Assert.That(StringConverters.ToInt32("22091978", 666), Is.EqualTo(22091978));
			Assert.That(StringConverters.ToInt32("09221978", 666), Is.EqualTo(9221978));
			Assert.That(StringConverters.ToInt32("-273", 666), Is.EqualTo(-273));
			Assert.That(StringConverters.ToInt32("+273", 666), Is.EqualTo(273));
			Assert.That(StringConverters.ToInt32("-0", 666), Is.EqualTo(0));
			Assert.That(StringConverters.ToInt32("+0", 666), Is.EqualTo(0));
			Assert.That(StringConverters.ToInt32(" 456", 666), Is.EqualTo(456));
			Assert.That(StringConverters.ToInt32("456 ", 666), Is.EqualTo(456));
			Assert.That(StringConverters.ToInt32(" 456 ", 666), Is.EqualTo(456));

			// failures
			Assert.That(StringConverters.ToInt32(null, 666), Is.EqualTo(666));
			Assert.That(StringConverters.ToInt32("", 666), Is.EqualTo(666));
			Assert.That(StringConverters.ToInt32("10737418235", 666), Is.EqualTo(666)); // overflow!
			Assert.That(StringConverters.ToInt32("A", 666), Is.EqualTo(666));
			Assert.That(StringConverters.ToInt32(".", 666), Is.EqualTo(666));
			Assert.That(StringConverters.ToInt32("-", 666), Is.EqualTo(666));
			Assert.That(StringConverters.ToInt32("+", 666), Is.EqualTo(666));
			Assert.That(StringConverters.ToInt32("true", 666), Is.EqualTo(666));
			Assert.That(StringConverters.ToInt32("false", 666), Is.EqualTo(666));
			Assert.That(StringConverters.ToInt32("narfzort", 666), Is.EqualTo(666));
			Assert.That(StringConverters.ToInt32(".123", 666), Is.EqualTo(666));
			Assert.That(StringConverters.ToInt32("=123", 666), Is.EqualTo(666));
			Assert.That(StringConverters.ToInt32("123A", 666), Is.EqualTo(666));
			Assert.That(StringConverters.ToInt32("123 A", 666), Is.EqualTo(666));
		}

		[Test]
		public void Test_ToBoolean()
		{
			// success
			Assert.That(StringConverters.ToBoolean("true", false), Is.True);
			Assert.That(StringConverters.ToBoolean("false", true), Is.False);
			Assert.That(StringConverters.ToBoolean("True", false), Is.True);
			Assert.That(StringConverters.ToBoolean("False", true), Is.False);
			Assert.That(StringConverters.ToBoolean("TRUE", false), Is.True);
			Assert.That(StringConverters.ToBoolean("FALSE", true), Is.False);
			Assert.That(StringConverters.ToBoolean("tRuE", false), Is.True);
			Assert.That(StringConverters.ToBoolean("FaLsE", true), Is.False);
			Assert.That(StringConverters.ToBoolean("on", false), Is.True);
			Assert.That(StringConverters.ToBoolean("off", true), Is.False);
			Assert.That(StringConverters.ToBoolean("yes", false), Is.True);
			Assert.That(StringConverters.ToBoolean("no", true), Is.False);
			Assert.That(StringConverters.ToBoolean("1", false), Is.True);
			Assert.That(StringConverters.ToBoolean("0", true), Is.False);

			// failures
			Assert.That(StringConverters.ToBoolean(null, true), Is.True);
			Assert.That(StringConverters.ToBoolean("", true), Is.True);
			Assert.That(StringConverters.ToBoolean("a", true), Is.True);
			Assert.That(StringConverters.ToBoolean("a", false), Is.False);
			Assert.That(StringConverters.ToBoolean("666", true), Is.True);
			Assert.That(StringConverters.ToBoolean("666", false), Is.False);
		}

		[Test]
		public void Test_ToDouble()
		{
			// "int" success
			Assert.That(StringConverters.ToDouble("0", 666), Is.EqualTo(0.0));
			Assert.That(StringConverters.ToDouble("1", 666), Is.EqualTo(1.0));
			Assert.That(StringConverters.ToDouble("777", 666), Is.EqualTo(777.0));
			Assert.That(StringConverters.ToDouble("22091978", 666), Is.EqualTo(22091978.0));
			Assert.That(StringConverters.ToDouble("09221978", 666), Is.EqualTo(9221978.0));
			Assert.That(StringConverters.ToDouble("10737418235", 666), Is.EqualTo(10737418235.0));
			Assert.That(StringConverters.ToDouble("-273", 666), Is.EqualTo(-273.0));
			Assert.That(StringConverters.ToDouble("+273", 666), Is.EqualTo(273.0));
			Assert.That(StringConverters.ToDouble("-0", 666), Is.EqualTo(0.0));
			Assert.That(StringConverters.ToDouble("+0", 666), Is.EqualTo(0.0));
			Assert.That(StringConverters.ToDouble(" 456", 666), Is.EqualTo(456.0));
			Assert.That(StringConverters.ToDouble("456 ", 666), Is.EqualTo(456.0));
			Assert.That(StringConverters.ToDouble(" 456 ", 666), Is.EqualTo(456.0));
			// "dec" success
			Assert.That(StringConverters.ToDouble("3.14", 666), Is.EqualTo(3.14));
			Assert.That(StringConverters.ToDouble("3.1415", 666), Is.EqualTo(3.1415));
			Assert.That(StringConverters.ToDouble("12.34", 666), Is.EqualTo(12.34));
			Assert.That(StringConverters.ToDouble("123.0", 666), Is.EqualTo(123.0));
			Assert.That(StringConverters.ToDouble("0.0000", 666), Is.EqualTo(0.0));
			Assert.That(StringConverters.ToDouble("1.2345E4", 666), Is.EqualTo(12345));
			Assert.That(StringConverters.ToDouble("1.2345e4", 666), Is.EqualTo(12345));
			Assert.That(StringConverters.ToDouble("123E-3", 666), Is.EqualTo(0.123));
			Assert.That(StringConverters.ToDouble("3,1415", 666), Is.EqualTo(3.1415));
			Assert.That(StringConverters.ToDouble("  3.1415", 666), Is.EqualTo(3.1415));

			// failures
			Assert.That(StringConverters.ToDouble(null, 666.0), Is.EqualTo(666.0));
			Assert.That(StringConverters.ToDouble("", 666.0), Is.EqualTo(666.0));
			Assert.That(StringConverters.ToDouble("A", 666.0), Is.EqualTo(666.0));
			Assert.That(StringConverters.ToDouble(".", 666.0), Is.EqualTo(666.0));
			Assert.That(StringConverters.ToDouble(",", 666.0), Is.EqualTo(666.0));
			Assert.That(StringConverters.ToDouble("-", 666.0), Is.EqualTo(666.0));
			Assert.That(StringConverters.ToDouble("+", 666.0), Is.EqualTo(666.0));
			Assert.That(StringConverters.ToDouble("E", 666.0), Is.EqualTo(666.0));
		}

		[Test]
		public void Test_ToDateString()
		{

			//AAAAMMJJ
			Assert.That(StringConverters.ToDateString(new DateTime(1978, 09, 22)), Is.EqualTo("19780922"));
			Assert.That(StringConverters.ToDateString(new DateTime(1978, 09, 22, 23, 59, 59, 999)), Is.EqualTo("19780922"));
			Assert.That(StringConverters.ToDateString(new DateTime(2078, 09, 22)), Is.EqualTo("20780922")); // Y2K ?
			Assert.That(StringConverters.ToDateString(new DateTime(6978, 09, 22)), Is.EqualTo("69780922"));  // Unix time ?
			Assert.That(StringConverters.ToDateString(DateTime.MinValue), Is.EqualTo("00010101"));
			Assert.That(StringConverters.ToDateString(DateTime.MaxValue), Is.EqualTo("99991231"));
		}

		[Test]
		public void Test_ToDateTimeString()
		{
			//AAAAMMJJHHMMSS
			Assert.That(StringConverters.ToDateTimeString(new DateTime(1978, 09, 22)), Is.EqualTo("19780922000000")); // sans heures
			Assert.That(StringConverters.ToDateTimeString(new DateTime(1978, 09, 22, 23, 59, 59, 999)), Is.EqualTo("19780922235959"));
			Assert.That(StringConverters.ToDateTimeString(new DateTime(2078, 09, 22, 12, 34, 56, 666)), Is.EqualTo("20780922123456")); // Y2K ?
			Assert.That(StringConverters.ToDateTimeString(new DateTime(6978, 09, 22, 01, 23, 45, 555)), Is.EqualTo("69780922012345"));  // Unix time ?
			Assert.That(StringConverters.ToDateTimeString(new DateTime(1978, 09, 22, 12, 30, 00)), Is.EqualTo("19780922123000")); // normal
			Assert.That(StringConverters.ToDateTimeString(new DateTime(1978, 09, 22, 18, 30, 00)), Is.EqualTo("19780922183000")); // ampm
			Assert.That(StringConverters.ToDateTimeString(new DateTime(1978, 09, 22, 23, 59, 59, 999)), Is.EqualTo("19780922235959")); // presque jour suivant
			Assert.That(StringConverters.ToDateTimeString(DateTime.MinValue), Is.EqualTo("00010101000000"));
			Assert.That(StringConverters.ToDateTimeString(DateTime.MaxValue), Is.EqualTo("99991231235959"));

		}

		[Test]
		public void Test_ParseDateTime()
		{
			//AAAAMMJJ
			Assert.That(StringConverters.ParseDateTime("19780922"), Is.EqualTo(new DateTime(1978, 09, 22)));
			Assert.That(StringConverters.ParseDateTime("20780922"), Is.EqualTo(new DateTime(2078, 09, 22))); // Y2K ?
			Assert.That(StringConverters.ParseDateTime("69780922"), Is.EqualTo(new DateTime(6978, 09, 22)));  // Unix time ?
			Assert.That(StringConverters.ParseDateTime("00010101"), Is.EqualTo(DateTime.MinValue));
			Assert.That(StringConverters.ParseDateTime("99991231"), Is.EqualTo(DateTime.MaxValue.Date));
			Assert.That(StringConverters.ParseDateTime("1978"), Is.EqualTo(new DateTime(1978, 01, 01)));
			Assert.That(StringConverters.ParseDateTime("2078"), Is.EqualTo(new DateTime(2078, 01, 01)));
			Assert.That(StringConverters.ParseDateTime("6978"), Is.EqualTo(new DateTime(6978, 01, 01)));
			Assert.That(StringConverters.ParseDateTime("197809"), Is.EqualTo(new DateTime(1978, 09, 01)));
			Assert.That(StringConverters.ParseDateTime("207809"), Is.EqualTo(new DateTime(2078, 09, 01)));
			Assert.That(StringConverters.ParseDateTime("697809"), Is.EqualTo(new DateTime(6978, 09, 01)));

			// Default Values
			Assert.That(StringConverters.ParseDateTime("narfzort", DateTime.MinValue, CultureInfo.InvariantCulture), Is.EqualTo(DateTime.MinValue));
			Assert.That(StringConverters.ParseDateTime("narfzort", DateTime.MaxValue, CultureInfo.InvariantCulture), Is.EqualTo(DateTime.MaxValue));
			Assert.That(StringConverters.ParseDateTime("Z20060101", new DateTime(2006, 2, 2), CultureInfo.InvariantCulture), Is.EqualTo(new DateTime(2006, 2, 2)));
			Assert.That(StringConverters.ParseDateTime("20060101", new DateTime(2006, 2, 2), CultureInfo.InvariantCulture), Is.EqualTo(new DateTime(2006, 1, 1)));
			Assert.That(StringConverters.ParseDateTime("20070230", DateTime.MaxValue, CultureInfo.InvariantCulture), Is.EqualTo(DateTime.MaxValue));

			//AAAAMMJJHHMMSS
			Assert.That(StringConverters.ParseDateTime("19780922123000"), Is.EqualTo(new DateTime(1978, 09, 22, 12, 30, 00))); // normal
			Assert.That(StringConverters.ParseDateTime("19780922183000"), Is.EqualTo(new DateTime(1978, 09, 22, 18, 30, 00))); // ampm
			Assert.That(StringConverters.ParseDateTime("19780922000000"), Is.EqualTo(new DateTime(1978, 09, 22))); // sans heures
			Assert.That(StringConverters.ParseDateTime("19780922235959"), Is.EqualTo(new DateTime(1978, 09, 22, 23, 59, 59))); // presque jour suivant

			//exceptions
			Assert.That(() => StringConverters.ParseDateTime("20070230"), Throws.InstanceOf<ArgumentOutOfRangeException>());
			Assert.That(() => StringConverters.ParseDateTime(null), Throws.InstanceOf<ArgumentNullException>());
			Assert.That(() => StringConverters.ParseDateTime("206"), Throws.InstanceOf<FormatException>());
			Assert.That(() => StringConverters.ParseDateTime("Z20060101"), Throws.InstanceOf<FormatException>());

			Assert.That(StringConverters.ParseDateTime(null, DateTime.MaxValue, CultureInfo.InvariantCulture), Is.EqualTo(DateTime.MaxValue));
			Assert.That(StringConverters.ParseDateTime("206", DateTime.MaxValue, CultureInfo.InvariantCulture), Is.EqualTo(DateTime.MaxValue));
			Assert.That(StringConverters.ParseDateTime("Z20060101", DateTime.MaxValue, CultureInfo.InvariantCulture), Is.EqualTo(DateTime.MaxValue));
		}

		[Test]
		public void Test_TryParseDateTime()
		{
			// TryParse
			Assert.That(StringConverters.TryParseDateTime("19780922", CultureInfo.InvariantCulture, out _, false), Is.True);
			Assert.That(StringConverters.TryParseDateTime("20780922", CultureInfo.InvariantCulture, out _, false), Is.True); // Y2K ?
			Assert.That(StringConverters.TryParseDateTime("69780922", CultureInfo.InvariantCulture, out _, false), Is.True);  // Unix time ?
			Assert.That(StringConverters.TryParseDateTime("00010101", CultureInfo.InvariantCulture, out _, false), Is.True);
			Assert.That(StringConverters.TryParseDateTime("99991231", CultureInfo.InvariantCulture, out _, false), Is.True);
			Assert.That(StringConverters.TryParseDateTime("1978", CultureInfo.InvariantCulture, out _, false), Is.True);
			Assert.That(StringConverters.TryParseDateTime("2078", CultureInfo.InvariantCulture, out _, false), Is.True);
			Assert.That(StringConverters.TryParseDateTime("6978", CultureInfo.InvariantCulture, out _, false), Is.True);
			Assert.That(StringConverters.TryParseDateTime("197809", CultureInfo.InvariantCulture, out _, false), Is.True);
			Assert.That(StringConverters.TryParseDateTime("207809", CultureInfo.InvariantCulture, out _, false), Is.True);
			Assert.That(StringConverters.TryParseDateTime("697809", CultureInfo.InvariantCulture, out _, false), Is.True);

			// exceptions ...
			Assert.That(StringConverters.TryParseDateTime(null, CultureInfo.InvariantCulture, out _, false), Is.False);
			Assert.That(StringConverters.TryParseDateTime("206", CultureInfo.InvariantCulture, out _, false), Is.False);
			Assert.That(StringConverters.TryParseDateTime("Z20060101", CultureInfo.InvariantCulture, out _, false), Is.False);
			Assert.That(StringConverters.TryParseDateTime("20070230", CultureInfo.InvariantCulture, out _, false), Is.False);

			// Dates vraiment à la con
			Assert.That(StringConverters.TryParseDateTime("0000", null, out _, false), Is.False);
			Assert.That(StringConverters.TryParseDateTime("000001", null, out _, false), Is.False);
			Assert.That(StringConverters.TryParseDateTime("200700", null, out _, false), Is.False);
			Assert.That(StringConverters.TryParseDateTime("00000000", null, out _, false), Is.False);
			Assert.That(StringConverters.TryParseDateTime("20070000", null, out _, false), Is.False);
			Assert.That(StringConverters.TryParseDateTime("20070100", null, out _, false), Is.False);
			Assert.That(StringConverters.TryParseDateTime("20071301", null, out _, false), Is.False);
			Assert.That(StringConverters.TryParseDateTime("20071232", null, out _, false), Is.False);
			Assert.That(StringConverters.TryParseDateTime("20070230", null, out _, false), Is.False);
			Assert.That(StringConverters.TryParseDateTime("00000000000000", null, out _, false), Is.False);
			Assert.That(StringConverters.TryParseDateTime("20070000000000", null, out _, false), Is.False);
			Assert.That(StringConverters.TryParseDateTime("20070100000000", null, out _, false), Is.False);
			Assert.That(StringConverters.TryParseDateTime("20070103250000", null, out _, false), Is.False);
			Assert.That(StringConverters.TryParseDateTime("20070103237500", null, out _, false), Is.False);
			Assert.That(StringConverters.TryParseDateTime("20070103235960", null, out _, false), Is.False);

		}

		[Test]
		public void Test_ToDateTime()
		{
			// Cultures
			Assert.That(StringConverters.ToDateTime("22/09/1978 12:30:45", DateTime.MinValue, new CultureInfo("fr-FR", false)), Is.EqualTo(new DateTime(1978, 09, 22, 12, 30, 45)));
			Assert.That(StringConverters.ToDateTime("22-09-1978 12:30:45", DateTime.MinValue, new CultureInfo("fr-FR", false)), Is.EqualTo(new DateTime(1978, 09, 22, 12, 30, 45)));
			Assert.That(StringConverters.ToDateTime("22/9/1978 12:30", DateTime.MinValue, new CultureInfo("fr-FR", false)), Is.EqualTo(new DateTime(1978, 09, 22, 12, 30, 00)));
			Assert.That(StringConverters.ToDateTime("22/09/1978", DateTime.MinValue, new CultureInfo("fr-FR", false)), Is.EqualTo(new DateTime(1978, 09, 22, 00, 00, 00)));
			Assert.That(StringConverters.ToDateTime("9/22/1978 12:30:45", DateTime.MinValue, new CultureInfo("en-US", false)), Is.EqualTo(new DateTime(1978, 09, 22, 12, 30, 45)));
			Assert.That(StringConverters.ToDateTime("9-22-1978 12:30:45", DateTime.MinValue, new CultureInfo("en-US", false)), Is.EqualTo(new DateTime(1978, 09, 22, 12, 30, 45)));
			Assert.That(StringConverters.ToDateTime("9/22/1978 3:30pm", DateTime.MinValue, new CultureInfo("en-US", false)), Is.EqualTo(new DateTime(1978, 09, 22, 15, 30, 00)));
			Assert.That(StringConverters.ToDateTime("9/22/1978", DateTime.MinValue, new CultureInfo("en-US", false)), Is.EqualTo(new DateTime(1978, 09, 22, 00, 00, 00)));
			Assert.That(StringConverters.ToDateTime("1978-9-22 12:30:45", DateTime.MinValue, new CultureInfo("de-DE", false)), Is.EqualTo(new DateTime(1978, 09, 22, 12, 30, 45)));
			Assert.That(StringConverters.ToDateTime("1978-9-22", DateTime.MinValue, new CultureInfo("de-DE", false)), Is.EqualTo(new DateTime(1978, 09, 22, 00, 00, 00)));

			// Timestamps with culture
			Assert.That(StringConverters.ToDateTime("19780922123045", DateTime.MinValue, new CultureInfo("fr-FR", false)), Is.EqualTo(new DateTime(1978, 09, 22, 12, 30, 45)));
			Assert.That(StringConverters.ToDateTime("19780922123045", DateTime.MinValue, new CultureInfo("en-US", false)), Is.EqualTo(new DateTime(1978, 09, 22, 12, 30, 45)));
			Assert.That(StringConverters.ToDateTime("19780922123045", DateTime.MinValue, new CultureInfo("de-DE", false)), Is.EqualTo(new DateTime(1978, 09, 22, 12, 30, 45)));

			// Dates à la con
			Assert.That(StringConverters.ToDateTime("22 Septembre 1978 13:30:45", DateTime.MinValue, new CultureInfo("fr-FR", false)), Is.EqualTo(new DateTime(1978, 09, 22, 13, 30, 45)), "22 Septembre 1978 13:30:45");
			Assert.That(StringConverters.ToDateTime("22 September 1978 1:30:45 PM", DateTime.MinValue, new CultureInfo("en-US", false)), Is.EqualTo(new DateTime(1978, 09, 22, 13, 30, 45)), "22 September 1978 1:30:45 PM");
			Assert.That(StringConverters.ToDateTime("22. September 1978 13:30:45", DateTime.MinValue, new CultureInfo("de-DE", false)), Is.EqualTo(new DateTime(1978, 09, 22, 13, 30, 45)), "22. September 1978 13:30:45");

			// Dates encore plus à la con
			Assert.That(StringConverters.ToDateTime("Vendredi 22 Septembre 1978 13:30:45", DateTime.MinValue, new CultureInfo("fr-FR", false)), Is.EqualTo(new DateTime(1978, 09, 22, 13, 30, 45)), "Vendredi 22 Septembre 1978 13:30:45");
			Assert.That(StringConverters.ToDateTime("Vendredi 22 Septembre 1978", DateTime.MinValue, new CultureInfo("fr-FR", false)), Is.EqualTo(new DateTime(1978, 09, 22, 0, 0, 0)), "Vendredi 22 Septembre 1978");
			Assert.That(StringConverters.ToDateTime("Friday, September 22, 1978 1:30:45 PM", DateTime.MinValue, new CultureInfo("en-US", false)), Is.EqualTo(new DateTime(1978, 09, 22, 13, 30, 45)), "Friday, September 22, 1978 1:30:45 PM");
			Assert.That(StringConverters.ToDateTime("Friday, September 22, 1978", DateTime.MinValue, new CultureInfo("en-US", false)), Is.EqualTo(new DateTime(1978, 09, 22, 0, 0, 0)), "Friday, September 22, 1978");
			Assert.That(StringConverters.ToDateTime("Freitag, 22. September 1978 13:30:45", DateTime.MinValue, new CultureInfo("de-DE", false)), Is.EqualTo(new DateTime(1978, 09, 22, 13, 30, 45)), "Freitag, 22. September 1978 13:30:45");
			Assert.That(StringConverters.ToDateTime("Freitag, 22. September 1978", DateTime.MinValue, new CultureInfo("de-DE", false)), Is.EqualTo(new DateTime(1978, 09, 22, 0, 0, 0)), "Freitag, 22. September 1978");


		}

	}

}
