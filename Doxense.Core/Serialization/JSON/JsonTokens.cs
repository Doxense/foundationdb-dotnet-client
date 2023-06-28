#region Copyright Doxense 2010-2021
//
// All rights are reserved. Reproduction or transmission in whole or in part, in
// any form or by any means, electronic, mechanical or otherwise, is prohibited
// without the prior written consent of the copyright owner.
//
#endregion

namespace Doxense.Serialization.Json
{
	using System;

	/// <summary>Classe interne contenant les différentes strings utilisées pour la sérialisation</summary>
	internal static class JsonTokens
	{
		// On load les différents tokens en static readonly pour éviter d'allouer sans arret les mêmes chaines ...
		public const string Null = "null";
		public const string Undefined = "undefined";
		public const string EmptyString = "\"\"";
		public const string True = "true";
		public const string False = "false";
		public const string EmptyObjectCompact = "{}";
		public const string EmptyObjectFormatted = "{ }";
		public const string EmptyArrayCompact = "[]";
		public const string EmptyArrayFormatted = "[ ]";
		public const string NewLine = "\r\n";
		public const string SymbolNaN = "NaN";
		public const string SymbolInfinityPos = "Infinity";
		public const string SymbolInfinityNeg = "-Infinity";
		public const string StringNaN = "\"NaN\"";
		public const string StringInfinityPos = "\"Infinity\"";
		public const string StringInfinityNeg = "\"-Infinity\"";
		public const string JavaScriptNaN = "Number.NaN";
		public const string JavaScriptInfinityPos = "Number.POSITIVE_INFINITY";
		public const string JavaScriptInfinityNeg = "Number.NEGATIVE_INFINITY";
		public const string Zero = "0";
		public const string DecimalZero = "0.0"; //DEPRECATED
		public const string DotZero = ".0"; //DEPRECATED
		public const string DoubleZero = "00";
		public const string LongMinValue = "-9223372036854775808";
		public const string CurlyCloseFormatted = " }";
		public const string BracketOpenFormatted = " [";
		public const string BracketCloseFormatted = " ]";
		public const string CommaFormatted = ", ";
		public const string CommaIndented = ",\r\n";
		public const string ColonFormatted = ": ";
		public const string ColonCompact = ":";
		public const string QuoteColonCompact = "\":";
		public const string QuoteColonFormatted = "\": ";
		public const string DoubleQuotes = "''";
		public const string DateBeginMicrosoft = "\"\\/Date(";
		public const string DateEndMicrosoft = ")\\/\"";
		public const string DateBeginMicrosoftDecoded = "/Date(";
		public const string DateEndMicrosoftDecoded = ")/";
		public const string DateBeginJavaScript = "new Date(";
		public const string MicrosoftDateTimeMinValue = "\"\\/Date(-62135596800000)\\/\"";
		public const string MicrosoftDateTimeMaxValue = "\"\\/Date(253402300799999)\\/\"";
		public const string JavaScriptDateTimeMinValue = "new Date(-62135596800000)";
		public const string JavaScriptDateTimeMaxValue = "new Date(253402300799999)";
		public const string Iso8601DateTimeMaxValue = "\"9999-12-31T23:59:59.9999999\"";
		public const string CustomClassAttribute = "_class";

	}

}
