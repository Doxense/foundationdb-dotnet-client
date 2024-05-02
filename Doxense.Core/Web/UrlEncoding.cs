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

namespace Doxense.Web
{
	using System.Collections.Specialized;
	using System.Globalization;
	using System.IO;
	using System.Text;
	using Doxense.Serialization;

	/// <summary>Classe helper pour l'encode/décodage d'URLs</summary>
	/// <remarks>Elle a pour but de simuler HttpUtility définie dans System.Web.dll qui n'est pas accessible dans le .NET Client Profile</remarks>
	public static class UrlEncoding
	{
		private static class Tokens
		{
			public const string True = "true";
			public const string False = "false";

			public const string FormatR = "R";
			public const string FormatDate = "yyyyMMdd";
			public const string FormatDateTime = "yyyyMMddHHmmss";
			public const string FormatDateTimeMillis = "yyyyMMddHHmmssfff";
		}

		#region Static Members...

		/// <summary>Map qui contient la catégorie de chaque caractère d'une URL</summary>
		private static readonly byte[] s_charMap = InitializeCharMap();

		private static readonly char[] s_charHex = [ '0', '1', '2', '3', '4', '5', '6', '7', '8', '9', 'a', 'b', 'c', 'd', 'e', 'f' ];

		private const byte CLEAN = 0; // Jamais modifié
		private const byte PATH = 1; // Normalement encodé en Percent, mais traitement spécial ?
		private const byte SPACE = 2; // Soit '+', soit '%20'
		private const byte DELIM = 3; // Délimiteur de chemin ('/', ':', ...)
		private const byte INVALID = 4; // "%XX"

		#endregion

		#region Public Methods...

		/// <summary>Décode une chaîne de texte encodée comme une URL (%XX)</summary>
		/// <param name="value">Chaîne contenant du texte encodé</param>
		/// <param name="encoding">Encoding utilisé (par défaut UTF-8 si null)</param>
		/// <returns>Chaîne décodée</returns>
		[Pure]
		public static string Decode(string? value, Encoding? encoding = null)
		{
			return Decode(value, 0, value?.Length ?? 0, encoding);
		}

		/// <summary>Décode une section d'une chaîne de texte encodée comme une URL (%XX)</summary>
		/// <param name="value">Chaîne contenant une URI ou tout autre texte encodé comme une URL</param>
		/// <param name="offset">Offset à partir du début de la chaîne</param>
		/// <param name="count">Nombre de caractères à décoder</param>
		/// <param name="encoding">Encoding utilisé (par défaut UTF-8 si null)</param>
		/// <returns>Section de la chaîne décodée</returns>
		[Pure]
		public static string Decode(string? value, int offset, int count, Encoding? encoding = null)
		{
			if (value == null || count <= 0)
			{
				return string.Empty;
			}
			if (NeedsDecoding(value, offset, count))
			{
				return DecodeString(value, offset, count, encoding);
			}
			if (offset == 0 && count == value.Length)
			{
				return value;
			}
			return value.Substring(offset, count);
		}

		/// <summary>Parse une QueryString, et passe le couple (attribut, valeur) à une lambda</summary>
		/// <typeparam name="TState">Type de l'état passé au handler (buffer, liste, ...)</typeparam>
		/// <param name="qs">QueryString à parser (sous la forme 'name1=value1&amp;name2=value2&amp;...')</param>
		/// <param name="state">Variable transmise à chaque appel du handler</param>
		/// <param name="handler">Action appelée pour chaque paramètre, avec le couple name/value (décodés). La value est null si le paramètre n'a pas de section '=xxxx'</param>
		/// <param name="encoding">Encoding utilisé (par défaut UTF-8 si null)</param>
		[Pure]
		internal static TState ParseQueryString<TState>(string? qs, TState state, Action<TState, string, string?> handler, Encoding? encoding = null)
		{
			int length;
			if (qs == null || (length = qs.Length) == 0) return state;

			// on démarre du début, sauf s'il y a un '?'
			int start = 0;
			if (qs[0] == '?') ++start; // skip

			for (int i = start; i < length; i++)
			{
				start = i;
				int end = -1;

				// recherche la fin du couple 'attr=name' (terminé par un '&' ou la fin de la chaîne)
				while (i < length)
				{
					char c = qs[i];
					if (c == '=')
					{ // fin du nom, début de la valeur
						if (end < 0) end = i;
					}
					else if (c == '&')
					{ // fin du couple
						break;
					}
					++i;
				}

				if (start == i)
				{ // un "&" qui se balade tout seul ??
					continue;
				}

				if (end < 0)
				{ // pas de valeur
					handler(state, Decode(qs, start, i - start, encoding), null);
				}
				else
				{ // valeur présente
					handler(state, Decode(qs, start, end - start, encoding), Decode(qs, end + 1, i - end - 1, encoding));
				}
			}
			return state;
		}

		/// <summary>Parse une QueryString, et retourne la liste des paramètres trouvés</summary>
		/// <param name="qs">QueryString à parser (sous la forme 'name1=value1&amp;name2=value2&amp;...')</param>
		/// <param name="encoding">Encoding utilisé (par défaut UTF-8 si null)</param>
		/// <returns>NameValueCollection contenant les paramètres de la querystring</returns>
		/// <remarks>"foo&..." contiendra null, "foo=&..." contiendra String.Empty</remarks>
		[Pure]
		public static NameValueCollection ParseQueryString(string? qs, Encoding? encoding = null)
		{
			return ParseQueryString(qs, new NameValueCollection(), (values, name, value) => values.Add(name, value), encoding);
		}

		/// <summary>Décode une chaîne de texte contenant une URL</summary>
		/// <param name="value">Chaîne à décoder</param>
		/// <param name="offset">Offset à partir du début de la chaîne</param>
		/// <param name="count">Nombre de caractères à décoder</param>
		/// <param name="encoding">Encoding utilisé (par défaut UTF-8 si null)</param>
		/// <returns>Section de l'url décodée</returns>
		[Pure]
		private static string DecodeString(string value, int offset, int count, Encoding? encoding)
		{
			encoding ??= Encoding.UTF8;

			// s'il n'y a rien à décoder, la taille du buffer de sortie est la même que celle de la string
			// s'il y a des choses, elle sera plus petite, avec une taille de 1/3 dans le pire des cas

			unsafe
			{
				fixed (char* chars = value)
				{
					if (count > 1024)
					{ // trop gros pour allouer sur la stack
						// => on alloue en mémoire
						var buffer = new byte[count];
						int size;
						fixed (byte* bytes = buffer)
						{
							size = DecodeBytes(chars, offset, count, bytes, encoding);
						}
						return encoding.GetString(buffer, 0, size);
					}
					else
					{ // ca peut passer sur la stack
						// décode dans un buffer sur la stack
						byte* bytes = stackalloc byte[count];
						int numBytes = DecodeBytes(chars, offset, count, bytes, encoding);
						// détermine le nb de caractères
						int numChars = encoding.GetCharCount(bytes, numBytes);
						// alloue le buffer de chars (sur la stack aussi)
						char* result = stackalloc char[numChars];
						int n = encoding.GetChars(bytes, numBytes, result, numChars);
						// retourne la string correspondante
						return new string(result, 0, n);
					}
				}
			}
		}

		/// <summary>Détermine si la chaîne nécessite d'être décodée (de manière pessimiste)</summary>
		/// <param name="value">Chaîne de texte présente dans une URL</param>
		/// <param name="offset"></param>
		/// <param name="count"></param>
		/// <returns>True si la chaîne contient (éventuellement) des caractères à encoder, false si elle est propre.</returns>
		[ContractAnnotation("value:null => false")]
		private static bool NeedsDecoding(string? value, int offset, int count)
		{
			if (value != null)
			{
				int p = offset;
				while (count-- > 0)
				{
					char c = value[p++];
					if (c == '%' || c == '+') return true;
				}
			}
			return false;
		}

		/// <summary>Retourne la valeur décimale d'une digit hexa décimal, ou -1 si ce n'en est pas un</summary>
		/// <param name="c">0-9, A-F, a-f</param>
		/// <returns>0-15, ou -1 si ce n'est pas un digit hexa décimal</returns>
		private static int DecodeHexDigit(char c)
		{
			// on accepte A-F, a-f et 0-9
			if (c < '0') return -1;
			if (c <= '9') return c - 48;
			if (c >= 'A' && c <= 'F') return c - 55;
			if (c >= 'a' && c <= 'f') return c - 87;
			return -1;
		}

		/// <summary>Décode une buffer de caractères contenant une URL, vers un buffer d'octets (pour décodage UTF-8)</summary>
		/// <param name="value">Buffer contenant les caractères de l'URL</param>
		/// <param name="offset">Offset dans le début du buffer</param>
		/// <param name="count">Nombre de caractères à décoder</param>
		/// <param name="bytes">Buffer de sortie où écrire les octets décodés</param>
		/// <param name="encoding">Encoding utilisé (UTF-8 par défaut si null)</param>
		/// <returns>Nombre d'octets écrit dans le buffer de sortie</returns>
		private static unsafe int DecodeBytes(char* value, int offset, int count, byte* bytes, Encoding? encoding)
		{
			encoding ??= Encoding.UTF8;

			//IMPORTANT: on se repose sur le fait que l'appelant a tailler 'bytes' suffisamment grand pour qu'il n'y ait pas d'overflow !!!

			int pDst = 0;
			int pSrc = offset;
			while(count-- > 0)
			{
				byte val = (byte) value[pSrc++];
				if (val == '+')
				{ // Space
					val = 32;
				}
				else if (val == '%' && count >= 2)
				{ // Percent-Encoded ?

					// trois possibilités:
					// - '%XX' : percent encoded
					// - '%uXXXX' : unicode encoded
					// - un '%' mal encodé qu'on doit laisser passer

					if (value[pSrc] == 'u' && count >= 5)
					{ // '%uXXXX' ?
						// values[pSrc] == 'u'
						int a = DecodeHexDigit(value[pSrc + 1]);
						int b = DecodeHexDigit(value[pSrc + 2]);
						int c = DecodeHexDigit(value[pSrc + 3]);
						int d = DecodeHexDigit(value[pSrc + 4]);
						if (a >= 0 && b >= 0 && c >= 0 && d >= 0)
						{ // les deux sont en hexa, on accepte le caractère

							// grah, le pb c'est qu'il faut qu'on rajoute les bytes correspondant à de l'UTF-8 :(
							char ch = (char) ((a << 12) | (b << 8)  | (c << 4) | d);
							// "%uXXXX" fait 6 bytes, et normalement, il n'y a rien qui peut faire plus de 5 bytes une fois encodé en UTF-8
							int n = encoding.GetBytes(&ch, 1, bytes + pDst, count);
							pDst += n;
							pSrc += 5;
							count -= 5;
							continue; // => next
						}
					}
					else
					{ // '%XX'
						// les deux suivants doivent être en hexa
						int hi = DecodeHexDigit(value[pSrc]);
						int lo = DecodeHexDigit(value[pSrc + 1]);
						if (hi >= 0 && lo >= 0)
						{ // les deux sont en hexa, on accepte le caractère
							bytes[pDst++] = (byte)((hi << 4) | lo);
							pSrc += 2;
							count -= 2;
							continue; // => next
						}
					}
					// sinon c'est un encodage foireux, on le laisse passer tel quel
				}
				bytes[pDst++] = val;
			}

			return pDst;
		}

		#region Uri...

		/// <summary>Encode correctement une URI</summary>
		/// <param name="value">Uri à encoder correctement</param>
		/// <param name="encoding">Encoding optionnel (UTF-8 par défaut)</param>
		/// <returns>Uri encodée correctement</returns>
		/// <remarks>Ne touche pas à la query string s'il y en a une !</remarks>
		/// <example>EncodeUri("http://server/path to the/file.ext?blah=xxxx") => "http://server/path%20to%20the/file.ext?blah=xxx"</example>
		[Pure]
		public static string EncodeUri(string? value, Encoding? encoding = null)
		{
			if (string.IsNullOrEmpty(value))
			{
				return string.Empty;
			}

			// ATTENTION: on ne doit pas toucher à la QueryString !
			int p = value.IndexOf('?');
			if (p >= 0)
			{ // appel récursif pour n'encoder que le path, en recollant la QueryString
				return EncodeUri(value.Substring(0, p)) + value.Substring(p);
			}

			if (!NeedsEncoding(value))
			{ // pas besoin d'encoder
				return value;
			}

			return EncodeString(value, encoding, true, true);
		}

		/// <summary>Ajoute une URI à un StringBuilder, en l'encodant correctement</summary>
		/// <param name="builder">Builder où ajoute l'URI encodée</param>
		/// <param name="value">Uri à encoder correctement</param>
		/// <param name="encoding">Encoding optionnel (UTF-8 par défaut)</param>
		/// <returns>StringBuilder, pour chaînage</returns>
		/// <example>AppendUriTo(sb, "http://server/path to the/file.ext?blah=xxxx") ~= sb.Append("http://server/path%20to%20the/file.ext?blah=xxx")</example>
		public static StringBuilder AppendUriTo(StringBuilder builder, string? value, Encoding? encoding = null)
		{
			if (!string.IsNullOrEmpty(value))
			{
				// ATTENTION: on ne doit pas toucher à la QueryString !
				int p = value.IndexOf('?');
				string? suffix = null;
				if (p >= 0)
				{ // appel récursif pour n'encoder que le path, en recollant la QueryString
					suffix = value.Substring(p);
					value = value.Substring(0, p);
				}

				if (!NeedsEncoding(value))
				{ // pas besoin d'encoder
					builder.Append(value);
				}
				else
				{
					builder.Append(EncodeString(value, encoding, true, true));
				}

				// rajoutes la querystring s'il y en avait une
				if (suffix != null) builder.Append(suffix);
			}
			return builder;
		}

		/// <summary>Ecrit une URI dans un TextWriter, en l'encodant proprement</summary>
		/// <param name="writer">Writer où écrire l'URI encodée</param>
		/// <param name="value">Uri à encoder correctement</param>
		/// <param name="encoding">Encoding optionnel (UTF-8 par défaut)</param>
		/// <returns>TextWriter, pour chaînage</returns>
		/// <example>WriteUriTo(writer, "http://server/path to the/file.ext?blah=xxxx") ~= writer.Write("http://server/path%20to%20the/file.ext?blah=xxx")</example>
		public static TextWriter WriteUriTo(TextWriter writer, string? value, Encoding? encoding = null)
		{
			if (!string.IsNullOrEmpty(value))
			{
				// ATTENTION: on ne doit pas toucher à la QueryString !
				int p = value.IndexOf('?');
				string? suffix = null;
				if (p >= 0)
				{ // appel récursif pour n'encoder que le path, en recollant la QueryString
					suffix = value.Substring(p);
					value = value.Substring(0, p);
				}

				if (!NeedsEncoding(value))
				{ // pas besoin d'encoder
					writer.Write(value);
				}
				else
				{
					writer.Write(EncodeString(value, encoding, true, true));
				}

				// rajoutes la querystring s'il y en avait une
				if (suffix != null) writer.Write(suffix);
			}
			return writer;
		}

		#endregion

		#region Path...

		/// <summary>Encode une valeur qui sera utilisée comme segment du chemin d'une URI</summary>
		/// <param name="value">Valeur à encoder correctement (' ' => '%20')</param>
		/// <param name="encoding">Encoding optionnel (UTF-8 par défaut)</param>
		/// <returns>Texte pouvant être intégré dans le chemin d'une URI</returns>
		/// <example>EncodePath("foo bar/baz") => "foo%20bar%2fbaz"</example>
		[Pure]
		public static string EncodePath(string? value, Encoding? encoding = null)
		{
			if (string.IsNullOrEmpty(value))
			{
				return string.Empty;
			}
			if (!NeedsEncoding(value))
			{ // pas besoin d'encoder
				return value;
			}
			return EncodeString(value, encoding, true, false);
		}

		[Pure]
		public static string EncodePathObject(object? value, Encoding? encoding = null)
		{
			return EncodePath(ObjectToString(value), encoding);
		}

		/// <summary>Concatène une valeur qui sera utilisée comme segment du chemin d'une URI</summary>
		/// <param name="builder">Builder où ajouter la valeur encodée</param>
		/// <param name="value">Valeur à encoder correctement (' ' => '%20')</param>
		/// <param name="encoding">Encoding optionnel (UTF-8 par défaut)</param>
		/// <returns>StringBuilder, pour chaînage</returns>
		/// <example>AppendPathTo(sb, "foo bar/baz") ~= sb.Append("foo%20bar%2fbaz")</example>
		public static StringBuilder AppendPathTo(StringBuilder builder, string? value, Encoding? encoding = null)
		{
			if (!string.IsNullOrEmpty(value))
			{
				if (!NeedsEncoding(value))
				{ // pas besoin d'encoder
					builder.Append(value);
				}
				else
				{
					builder.Append(EncodeString(value, encoding, true, false));
				}
			}
			return builder;
		}

		/// <summary>Ecrit une valeur qui sera utilisée comme segment du chemin d'une URI</summary>
		/// <param name="writer">Writer où écrire la valeur encodée</param>
		/// <param name="value">Valeur à encoder correctement (' ' => '%20')</param>
		/// <param name="encoding">Encoding optionnel (UTF-8 par défaut)</param>
		/// <returns>TextWriter, pour chaînage</returns>
		/// <example>WritePathTo(writer, "foo bar/baz") ~= writer.Write("foo%20bar%2fbaz")</example>
		public static TextWriter WritePathTo(TextWriter writer, string? value, Encoding? encoding = null)
		{
			if (!string.IsNullOrEmpty(value))
			{
				if (!NeedsEncoding(value))
				{ // pas besoin d'encoder
					writer.Write(value);
				}
				else
				{
					writer.Write(EncodeString(value, encoding, true, false));
				}
			}
			return writer;
		}

		#endregion

		#region Data...

		[Pure]
		private static string ObjectToString(object? value)
		{
			// most frequent types
			if (value == null) return string.Empty;
			if (value is string s) return s;

			var type = value.GetType();
			if (type.IsPrimitive)
			{
				// Attention: GetTypeCode retourne 'TypeCode.Int32' pour une Enum !
				switch (Type.GetTypeCode(type))
				{
					case TypeCode.Boolean: return ((bool)value) ? Tokens.True : Tokens.False;
					case TypeCode.Char: return new string((char)value, 1);
					case TypeCode.SByte: return StringConverters.ToString((sbyte) value);
					case TypeCode.Byte: return StringConverters.ToString((byte) value);
					case TypeCode.Int16: return StringConverters.ToString((short) value);
					case TypeCode.UInt16: return StringConverters.ToString((ushort) value);
					case TypeCode.Int32: return StringConverters.ToString((int) value);
					case TypeCode.UInt32: return StringConverters.ToString((uint) value);
					case TypeCode.Int64: return StringConverters.ToString((long) value);
					case TypeCode.UInt64: return ((ulong)value).ToString(null, CultureInfo.InvariantCulture);
					case TypeCode.Single: return ((float)value).ToString(Tokens.FormatR, CultureInfo.InvariantCulture);
					case TypeCode.Double: return ((double)value).ToString(Tokens.FormatR, CultureInfo.InvariantCulture);
					//note: decimal n'est pas primitive !
				}
			}

			if (value is TimeSpan ts)
			{ // TimeSpan => nombre de secondes
				return ts.TotalSeconds.ToString(Tokens.FormatR, CultureInfo.InvariantCulture);
			}

			if (value is DateTime date)
			{ // Date => YYYYMMDD[HHMMSS[fff]]
				var time = date.TimeOfDay;
				if (time == TimeSpan.Zero) return date.ToString(Tokens.FormatDate);
				if (time.Milliseconds == 0) return date.ToString(Tokens.FormatDateTime);
				return date.ToString(Tokens.FormatDateTimeMillis);
			}

			if (value is decimal dec)
			{
				return dec.ToString(null, CultureInfo.InvariantCulture);
			}

			if (value is Enum e)
			{
				return e.ToString();
			}

			if (value is IFormattable fmt)
			{
				return fmt.ToString(null, CultureInfo.InvariantCulture);
			}

			// on croise les doigts...
			return value.ToString() ?? string.Empty;
		}

		/// <summary>Encode une valeur qui sera utilisée comme valeur dans une QueryString</summary>
		/// <param name="value">Valeur à encoder correctement (' ' => '+')</param>
		/// <param name="encoding">Encoding optionnel (UTF-8 par défaut)</param>
		/// <returns>Texte pouvant être utilisé comme valeur dans une QueryString</returns>
		/// <example>EncodeData("foo bar/baz") => "foo+bar%2fbaz"</example>
		[Pure]
		public static string EncodeData(string? value, Encoding? encoding = null)
		{
			if (string.IsNullOrEmpty(value))
			{
				return string.Empty;
			}
			if (!NeedsEncoding(value))
			{ // pas besoin d'encoder
				return value;
			}
			return EncodeString(value, encoding, path: false, delims: false);
		}

		[Pure]
		public static string EncodeDataObject(object? value, Encoding? encoding = null)
		{
			return EncodeData(ObjectToString(value), encoding);
		}

		/// <summary>Concatène une valeur qui sera utilisée comme valeur dans une QueryString</summary>
		/// <param name="builder">Builder où ajouter la valeur encodée</param>
		/// <param name="value">Valeur à encoder correctement (' ' => '+')</param>
		/// <param name="encoding">Encoding optionnel (UTF-8 par défaut)</param>
		/// <returns>StringBuilder, pour chaînage</returns>
		/// <example>AppendDataTo(sb, "foo bar/baz") ~= sb.Append("foo+bar%2fbaz")</example>
		public static StringBuilder AppendDataTo(StringBuilder builder, string? value, Encoding? encoding = null)
		{
			if (!string.IsNullOrEmpty(value))
			{
				if (!NeedsEncoding(value))
				{ // pas besoin d'encoder
					builder.Append(value);
				}
				else
				{
					//REVIEW: PERF: écrire une version de EncodeString qui append directement dans un StringBuilder (sans allouer de string)
					builder.Append(EncodeString(value, encoding, path: false, delims: false));
				}
			}
			return builder;
		}

		/// <summary>Ecrit une valeur qui sera utilisée comme valeur dans une QueryString</summary>
		/// <param name="writer">Writer où écrire la valeur encodée</param>
		/// <param name="value">Valeur à encoder correctement (' ' => '+')</param>
		/// <param name="encoding">Encoding optionnel (UTF-8 par défaut)</param>
		/// <returns>TextWriter, pour chaînage</returns>
		/// <example>WriteDataTo(writer, "foo bar/baz") ~= writer.Write("foo+bar%2fbaz")</example>
		public static TextWriter WriteDataTo(TextWriter writer, string? value, Encoding? encoding = null)
		{
			if (!string.IsNullOrEmpty(value))
			{
				if (!NeedsEncoding(value))
				{ // pas besoin d'encoder
					writer.Write(value);
				}
				else
				{
					//REVIEW: PERF: écrire une version de EncodeString qui append directement dans un TextWriter (sans allouer de string)
					writer.Write(EncodeString(value, encoding, path: false, delims: false));
				}
			}
			return writer;
		}

		#endregion

		#endregion

		#region Internal Helpers...

		/// <summary>Génère la map de conversion des caractères</summary>
		[Pure]
		private static byte[] InitializeCharMap()
		{
			// Note: vu qu'on encode la version UTF-8 (bytes), on ne travaille que sur des octets !
			var map = new byte[256];
			for (int i = 0; i < map.Length; i++)
			{
				char c = (char)i;

				if (IsNeverEncoded(c))
					map[i] = CLEAN;
				else if (IsPathDelimOrSpecial(c))
					map[i] = DELIM;
				else if (IsValidOnlyInPath(c))
					map[i] = PATH;
				else if (c == ' ')
					map[i] = SPACE;
				else
					map[i] = INVALID;
			}
			return map;
		}

		[Pure]
		private static bool IsNeverEncoded(char c)
		{
			// ces caractères ne seront JAMAIS encodés !
			return (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9') || c == '-' || c == '_' || c == '.' || c == '~';
		}

		[Pure]
		private static bool IsValidOnlyInPath(char c)
		{
			// ces caractères ne sont pas encodés dans une URI
			return c == '!' || c == '*' || c == '\'' || c == '(' || c == ')' || c == ';' || c == ':' || c == '@' || c == '&' || c == '=' || c == '+' || c == '$' || c == ',' || c == '/' || c == '?' || c == '#' || c == '[' || c == ']'
				|| c == '\\' || c == '"' || c == '<' || c == '>' || c == '^' || c == '`' || c == '{' || c == '|' || c == '}';
		}

		[Pure]
		private static bool IsPathDelimOrSpecial(char c)
		{
			// ces caractères correspondant a des séparateurs de path (qui sont encodés si on génère un path)
			return c == ':' || c == '/' || c == '?' || c == '#' || c == '[' || c == ']' || c == '@' || c == '%';
		}

		/// <summary>Détermine si la chaîne nécessite d'être encodée (de manière pessimiste)</summary>
		/// <param name="value">Chaîne de texte à insérer dans une URL</param>
		/// <returns>True si la chaîne contient (éventuellement) des caractères à encoder, false si elle est propre.</returns>
		[Pure, ContractAnnotation("null => false")]
		private static bool NeedsEncoding(string? value)
		{
			if (value != null)
			{
				byte[] map = s_charMap;
				int n = value.Length;
				for (int i = 0; i < n; i++)
				{
					char c = value[i];
					if (c > 255 || map[c] != CLEAN) return true;
				}
			}
			return false;
		}

		/// <summary>Encode une chaîne de texte</summary>
		/// <param name="value">Chaîne de texte à encoder</param>
		/// <param name="encoding">Encoding à utiliser (UTF-8 par défaut)</param>
		/// <param name="path">Indique si on est dans la section 'PATH' (true) d'une URI (ou false si on est dans la Query String)</param>
		/// <param name="delims">Indique si on doit laisser passer les délimiteurs de path</param>
		/// <returns>Chaîne encodée</returns>
		private static unsafe string EncodeString(string value, Encoding? encoding, bool path, bool delims)
		{
			var bytes = (encoding ?? Encoding.UTF8).GetBytes(value);
			int size = ComputeEncodedSize(bytes, path, delims);

			// on alloue directement la chaîne, en modifiant "in place" son contenu
			var result = new string('\0', size);
			fixed (char* chars = result)
			{
				EncodeChars(bytes, 0, bytes.Length, chars, 0, path, delims);
			}
			return result;
		}

		/// <summary>Calcule la taille (en caractères) nécessaire pour encoder une chaîne de texte</summary>
		/// <param name="bytes">Octets représentant la chaîne de texte initiale (en UTF-8 par exemple)</param>
		/// <param name="path">Indique si on est dans la section 'PATH' (true) d'une URI (ou false si on est dans la Query String)</param>
		/// <param name="delims">Indique si on doit laisser passer les délimiteurs de path</param>
		/// <returns></returns>
		[Pure]
		private static int ComputeEncodedSize(byte[] bytes, bool path, bool delims)
		{
			int extra = 0;
			byte[] map = s_charMap;
			foreach (byte b in bytes) {
				byte category;
				//note: comme la valeur est un octet, elle est forcément <= 255, donc pas la peine de faire de bound-check sur map !
				if ((category = map[b]) != CLEAN)
				{
					switch (category)
					{
						case PATH:
						{
							// il n'est pas modifié dans un PATH
							if (!path) ++extra;
							break;
						}
						case SPACE:
						{
							// dans la querystring, on n'augmente pas la taille !
							if (path) ++extra;  // %20
							break;
						}
						case DELIM:
						{
							// il n'est pas modifié si on sanitize
							if (!delims) ++extra; // %25
							break;
						}
						case INVALID:
						{ // passe de 1 à 3 ("%XX")
							++extra;
							break;
						}
					}
				}
			}
			return bytes.Length + (extra << 1);
		}

		/// <summary>Encode un buffer contenant du texte UTF-8 vers un tableau de chars</summary>
		/// <returns>Offset dans le buffer suivant le dernier caractère encodé</returns>
		private static unsafe void EncodeChars(byte[]? value, int offset, int count, char* chars, int index, bool path, bool delims)
		{
			if (value == null || value.Length == 0)
			{
				return;
			}

			char[] hexes = s_charHex; // en encode en minuscules
			byte[] map = s_charMap;

			int p = index;
			for (int i = 0; i < count; i++)
			{
				byte c = value[offset + i];
				//note: comme la valeur est un octet, elle est forcément <= 255, donc pas la peine de faire de bound-check sur map !
				switch (map[c])
				{
					case CLEAN:
					{ // caractère qui n'est jamais encodé
						chars[p++] = (char) c;
						break;
					}
					case PATH:
					{ // caractère qui n'est autorisé que dans un path
						if (path)
						{ // on ne laisse passer que dans le path
							chars[p++] = (char) c;
						}
						else
						{ // sinon on encode
							chars[p] = '%';
							chars[p + 1] = hexes[(c >> 4) & 0xF];
							chars[p + 2] = hexes[c & 0xF];
							p += 3;
						}
						break;
					}
					case SPACE:
					{ // Espace
						if (path)
						{ // dans le path
							chars[p] = '%';
							chars[p + 1] = '2';
							chars[p + 2] = '0';
							p += 3;
						}
						else
						{ // dans la query string
							chars[p++] = '+';
						}
						break;
					}
					case DELIM:
					{ // délimiteur de path
						if (delims)
						{ // on ne laisse passer que si on nettoie une URI
							chars[p++] = (char) c;
						}
						else
						{ // sinon on encode
							chars[p] = '%';
							chars[p + 1] = hexes[(c >> 4) & 0xF];
							chars[p + 2] = hexes[c & 0xF];
							p += 3;
						}
						break;
					}
					case INVALID:
					{ // caractère encodé quoi qu'il arrive
						// '%xx' (en minuscules!)
						chars[p] = '%';
						chars[p + 1] = hexes[(c >> 4) & 0xF];
						chars[p + 2] = hexes[c & 0xF];
						p += 3;
						break;
					}
				}
			}
		}

		#endregion

	}

}
