using JetBrains.Annotations;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Dynamic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FoundationDB.Client.Utils
{

	/// <summary>Tiny JSON parser</summary>
	internal sealed class TinyJsonParser
	{

		// This is a quick&dirty JSON parser whose only goal in life is to parse the result of the "\xff\xff/status/json" data returned by the cluster, without having to take a dependency on a JSON library.
		// There is no object models: maps are Dictionary<string, object>, arrays are List<object>, and values are string, doubles and booleans
		// This is an rough port of parser logic from the nanojson JAVA library: https://github.com/mmastrac/nanojson

		//TODO: clean this file!!!

		private char[] m_buffer;
		private int m_cursor;
		private int m_end;

		private object m_current;
		private StringBuilder m_scratch = new StringBuilder();

		internal TinyJsonParser(char[] buffer, int offset, int count)
		{
			if (buffer == null) throw new ArgumentNullException("buffer");
			if (offset < 0 || offset > buffer.Length) throw new ArgumentOutOfRangeException("offset");
			if (count < 0 || offset + count > buffer.Length) throw new ArgumentOutOfRangeException("count");

			m_buffer = buffer;
			m_cursor = offset;
			m_end = offset + count;
		}

		private const int EOF = -1;

		private int ReadNextChar()
		{
			return m_cursor < m_end ? m_buffer[m_cursor++] : EOF;
		}

		private static bool IsWhiteSpace(int c)
		{
			return c <= 32 && (c == ' ' || c == '\t' || c == '\n' || c == '\r');
		}

		private enum Token
		{
			Eof = 0,
			Literal,
			Colon,
			Comma,
			MapBegin,
			MapEnd,
			ArrayBegin,
			ArrayEnd,
			Null,
			True,
			False,
			Number,
		}

		/// <summary>Singleton for the False value</summary>
		private static readonly object s_false = false;
		/// <summary>Singleton for the True value</summary>
		private static readonly object s_true = true;
		/// <summary>Singleton for the empty map</summary>
		private static readonly Dictionary<string, object> s_missingMap = new Dictionary<string, object>();
		/// <summary>Singleton for the empty array</summary>
		private static readonly List<object> s_missingArray = new List<object>();

		private Token ReadToken()
		{
			int c = ReadNextChar();
			while (IsWhiteSpace(c)) { c = ReadNextChar(); }

			m_current = null;
			switch (c)
			{
				case EOF: return Token.Eof;
				case '{':
				{
					var map = new Dictionary<string, object>(StringComparer.Ordinal);
					var token = ReadToken();
					if (token != Token.MapEnd)
					{
						while (true)
						{
							if (token != Token.Literal) throw SyntaxError("Expected field name in map, but found {0}", token);
							string key = (string)m_current;
							Contract.Assert(key != null);

							if ((token = ReadToken()) != Token.Colon) throw SyntaxError("Expected ':' in map, but found {0}", token);

							token = ReadToken();
							switch(token)
							{
								case Token.Null: map.Add(key, null); break;
								case Token.False: map.Add(key, s_false); break;
								case Token.True: map.Add(key, s_true); break;
								default: map.Add(key, m_current); break;
							}

							token = ReadToken();
							if (token == Token.MapEnd) break;
							if (token != Token.Comma) throw SyntaxError("Expected comma in map, but found {0}", token);
							token = ReadToken();
							// note: we will allow trailing ',' at the end of a map!
							if (token == Token.MapEnd) break;
						}
					}
					m_current = map;
					return Token.MapBegin;
				}
				case '}':
				{
					return Token.MapEnd;
				}
				case '[':
				{
					var array = new List<object>();
					var token = ReadToken();
					if (token != Token.ArrayEnd)
					{
						while (true)
						{
							array.Add(m_current);
							if ((token = ReadToken()) == Token.ArrayEnd) break;
							if (token != Token.Comma) throw SyntaxError("Expected a comma, or end of the array, but found {0}", token);
							token = ReadToken();
							//note: we will allow trailng ',' at the end of an array!
							if (token == Token.ArrayEnd) break;
						}
					}
					m_current = array;
					return Token.ArrayBegin;
				}
				case ']':
				{
					return Token.ArrayEnd;
				}
				case ',':
				{
					return Token.Comma;
				}
				case ':':
				{
					return Token.Colon;
				}
				case 't':
				case 'T':
				{   // true/True ?
					ReadNextChar();//'r'
					ReadNextChar();//'u'
					ReadNextChar();//'e'
					return Token.True;
				}
				case 'f':
				case 'F':
				{   // false/False?
					ReadNextChar();//'a'
					ReadNextChar();//'l'
					ReadNextChar();//'s'
					ReadNextChar();//'e'
					return Token.False;
				}
				case 'n':
				case 'N':
				{   // null/Null?
					ReadNextChar();//'u'
					ReadNextChar();//'l'
					ReadNextChar();//'l'
					return Token.Null;
				}

				case '\"':
				{
					m_current = ReadStringLiteral();
					return Token.Literal;
				}

				case '0':
				case '1':
				case '2':
				case '3':
				case '4':
				case '5':
				case '6':
				case '7':
				case '8':
				case '9':
				case '-':
				case '+':
				{
					m_current = ReadNumberLiteral();
					return Token.Number;
				}

				default:
				{
					throw SyntaxError("Unexpected character '{0}'", new string((char)c, 1));
				}
			}
		}

		[NotNull]
		private string ReadStringLiteral()
		{
			var buffer = m_buffer;
			int cursor = m_cursor;
			int end = m_end;

			var sb = m_scratch;
			sb.Clear();

			while (cursor < end)
			{
				char c = buffer[cursor++];
				switch (c)
				{
					case '\"':
					{
						m_cursor = cursor;
						return sb.Length == 0 ? String.Empty : sb.ToString();
					}

					case '\\':
					{
						if (cursor >= end) break;
						c = buffer[cursor++];
						switch (c)
						{
							case '\\': sb.Append('\\'); break;
							case 'n': sb.Append('\n'); break;
							case 'r': sb.Append('\r'); break;
							case 't': sb.Append('\t'); break;
							case 'f': sb.Append('\f'); break;
							case 'b': sb.Append('\b'); break;
							case 'u':
							{
								if (cursor + 4 >= end) throw SyntaxError("Truncated unicode escape sequence in string literal");
								int x = 0;
								for (int i = 0; i < 4; i++)
								{
									c = buffer[cursor++];
									x <<= 4;
									if (c >= '0' && c <= '9') x |= (c - '0');
									else if (c >= 'A' && c <= 'F') x |= (c - 'A');
									else if (c >= 'a' && c <= 'f') x |= (c - 'a');
									else throw SyntaxError("Invalid unicode escape character '{0}' in string literal", c);
								}
								sb.Append((char) x);
								break;
							}
							default:
							{
								throw SyntaxError("Invalid escape character '{0}' in string literal", new string(c, 1));
							}
						}
						break;
					}

					default:
					{
						sb.Append(c);
						break;
					}

				}
			}

			throw SyntaxError("Truncated literal");
		}

		private double ReadNumberLiteral()
		{
			var buffer = m_buffer;
			int cursor = m_cursor - 1; // roll back the char that has already been read
			int end = m_end;

			int start = cursor;
			while(cursor < end)
			{
				char c = buffer[cursor];
				if (char.IsDigit(c) || c == '.' || c == '+' || c == '-' || c == 'e' || c == 'E')
				{
					++cursor;
					continue;
				}

				break;
			}

			string val = new string(buffer, start, cursor - start);
			double x;
			if (!double.TryParse(val, NumberStyles.Float, CultureInfo.InvariantCulture, out x))
			{
				throw SyntaxError("Malformed number literal '{0}'", val);
			}
			m_cursor = cursor;
			return x;
		}

		[NotNull]
		private FormatException SyntaxError(string msg)
		{
			return new FormatException(String.Format(CultureInfo.InvariantCulture, "Invalid JSON Syntax: {0} at {1}", msg, m_cursor));
		}

		[NotNull][StringFormatMethod("msg")]
		private FormatException SyntaxError(string msg, object arg0)
		{
			return new FormatException(String.Format(CultureInfo.InvariantCulture, "Invalid JSON Syntax: {0} at {1}", String.Format(CultureInfo.InvariantCulture, msg, arg0), m_cursor));
		}

		[CanBeNull]
		public static Dictionary<string, object> ParseObject(Slice data)
		{
			if (data.Count == 0) return null;
			char[] chars = Encoding.UTF8.GetChars(data.Array, data.Offset, data.Count);
			return ParseObject(chars, 0, chars.Length);
		}

		[ContractAnnotation("null => null")]
		public static Dictionary<string, object> ParseObject(string jsonText)
		{
			if (string.IsNullOrEmpty(jsonText)) return null;
			char[] chars = jsonText.ToCharArray();
			return ParseObject(chars, 0, chars.Length);
		}

		[CanBeNull]
		internal static Dictionary<string, object> ParseObject([NotNull] char[] chars, int offset, int count)
		{
			Contract.Requires(chars != null && offset >= 0 && count >= 0);

			var parser = new TinyJsonParser(chars, offset, count);
			var token = parser.ReadToken();
			if (token == Token.Eof) return null;

			// ensure we got an object
			if (token != Token.MapBegin) throw new InvalidOperationException(String.Format("JSON object expected, but got a {0}", token));
			var map = (Dictionary<string, object>)parser.m_current;

			// ensure that there is nothing after the object
			token = parser.ReadToken();
			if (token != Token.Eof) throw new InvalidOperationException("Extra data at the end of the JSON object");
			return map;
		}

		[CanBeNull]
		public static List<object> ParseArray(Slice data)
		{
			if (data.Count == 0) return null;
			char[] chars = Encoding.UTF8.GetChars(data.Array, data.Offset, data.Count);
			var parser = new TinyJsonParser(chars, 0, chars.Length);
			var token = parser.ReadToken();
			if (token == Token.Eof) return null;
			var array = (List<object>)parser.m_current;
			if (token != Token.ArrayBegin) throw new FormatException("Invalid JSON document: array expected");
			token = parser.ReadToken();
			if (token != Token.Eof) throw new FormatException("Invalid JSON document: extra data after array");
			return array;
		}

		[NotNull]
		internal static Dictionary<string, object> GetMapField(Dictionary<string,object> map, string field)
		{
			object item;
			return map != null && map.TryGetValue(field, out item) ? (Dictionary<string, object>)item : s_missingMap;
		}

		[NotNull]
		internal static List<object> GetArrayField(Dictionary<string, object> map, string field)
		{
			object item;
			return map != null && map.TryGetValue(field, out item) ? (List<object>)item : s_missingArray;
		}

		internal static string GetStringField(Dictionary<string, object> map, string field)
		{
			object item;
			return map != null && map.TryGetValue(field, out item) ? (string)item : null;
		}

		internal static double? GetNumberField(Dictionary<string, object> map, string field)
		{
			object item;
			return map != null && map.TryGetValue(field, out item) ? (double)item : default(double?);
		}

		internal static bool? GetBooleanField(Dictionary<string, object> map, string field)
		{
			object item;
			return map != null && map.TryGetValue(field, out item) ? (bool)item : default(bool?);
		}

		internal static KeyValuePair<string, string> GetStringPair(Dictionary<string, object> map, string key, string value)
		{
			object item;
			return new KeyValuePair<string, string>(
				map != null && map.TryGetValue(key, out item) ? (string)item : null,
				map != null && map.TryGetValue(value, out item) ? (string)item : null
			);
		}
	}

}
