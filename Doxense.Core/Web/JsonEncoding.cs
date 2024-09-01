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

namespace Doxense.Serialization.Json
{
	using System.Globalization;
	using System.Runtime.CompilerServices;
	using System.Text;
	using Doxense.Text;

	public static class JsonEncoding
	{

		/// <summary>Lookup table used to test if a character must be escaped</summary>
		/// <remarks>lookup[index] contains <c>true</c> if the corresponding UNICODE character must be escaped</remarks>
		private static readonly bool[] EscapingLookupTable = InitializeEscapingLookupTable();

		private static bool[] InitializeEscapingLookupTable()
		{
			// IMPORTANT: the array MUST have a length of 65536 because it will be indexed with the UNICODE value of each character!
			var table = new bool[65536];
			for (int i = 0; i < table.Length; i++)
			{
				table[i] = NeedsEscaping((char) i);
			}

			//JIT_HACK: ensure that StringTable is already JITed
			StringTable.EnsureJit();

			return table;
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool NeedsEscaping(char c)
		{
			// Encode double-quote ("), anti-slash (\), and ASCII control codes (0..31), as well as special UNICODE characters (0xD800-0xDFFF, 0xFFFE and 0xFFFF)
			return (c < 32 || c == '"' || c == '\\') || (c >= 0xD800 && (c < 0xE000 | c >= 0xFFFE));
		}

		/// <summary>Check if a string requires escaping before being written to a JSON document</summary>
		/// <param name="s">Text to inspect</param>
		/// <returns><see langword="false"/> if all characters are valid, or <see langword="true"/> if at least one character must be escaped</returns>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool NeedsEscaping(string? s)
		{
			return s != null && NeedsEscaping(s.AsSpan());
		}

		/// <summary>Check if a string requires escaping before being written to a JSON document</summary>
		/// <param name="s">Text to inspect</param>
		/// <returns><see langword="false"/> if all characters are valid, or <see langword="true"/> if at least one character must be escaped</returns>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool NeedsEscaping(ReadOnlySpan<char> s)
		{
			// A string is a collection of zero or more Unicode characters, wrapped in double quotes, using backslash escapes.
			// A character is represented as a single character string. A string is very much like a C or Java string.

			return s.Length <= 6
				? NeedsEscapingShort(s)
				: NeedsEscapingLong(s);

			static bool NeedsEscapingShort(ReadOnlySpan<char> s)
			{
				var lookup = EscapingLookupTable;
				foreach (var c in s)
				{
					if (lookup[c]) return true;
				}
				return false;
			}

			static unsafe bool NeedsEscapingLong(ReadOnlySpan<char> s)
			{
				// We assume that 99.99+% of string will NOT require escaping, and so lookup[c] will (almost) always be false.
				// If we use a bitwise OR (|), we only need one test/branch per batch of 4 characters, compared to a logical OR (||).

				fixed (char* p = s)
				{
					var lookup = EscapingLookupTable;
					int n = s.Length;
					char* ptr = p;

					// loop unrolling (4 chars = 8 bytes)
					while (n >= 4)
					{
						if (lookup[*(ptr)] | lookup[*(ptr + 1)] | lookup[*(ptr + 2)] | lookup[*(ptr + 3)]) return true;
						ptr += 4;
						n -= 4;
					}
					// tail
					while (n-- > 0)
					{
						if (lookup[*ptr++]) return true;
					}
					return false;
				}
			}

		}

		/// <summary>Encode a string of text that must be written to a JSON document</summary>
		/// <param name="text">Text to encode</param>
		/// <returns>'null', '""', '"foo"', '"\""', '"\u0000"', ...</returns>
		/// <remarks>String with the correct escaping and surrounded by double-quotes (<c>"..."</c>), or <c>"null"</c> if <paramref name="text"/> is <c>null</c></remarks>
		/// <example>EncodeJsonString("foo") => "\"foo\""</example>
		public static string Encode(ReadOnlySpan<char> text)
		{
			if (text.Length == 0)
			{ // => ""
				return "\"\"";
			}
			
			// first check if we actually need to encode anything
			if (NeedsEscaping(text))
			{ // yes => slow path
				return EncodeSlow(text);
			}

			// nothing to do, except add the double quotes
			return string.Concat("\"", text, "\"");
		}

		internal static string EncodeSlow(ReadOnlySpan<char> text)
		{
			// note: we assume that the typical overhead of escaping characters will be up to 6 characters if there is only one or two "invalid" characters
			// this assumption totally breaks down for non-latin languages!
			var sb = StringBuilderCache.Acquire(checked(text.Length + 2 + 6));
			return StringBuilderCache.GetStringAndRelease(AppendSlow(sb, text, true));
		}

		/// <summary>Encodes a string literal that must be written to a JSON document</summary>
		/// <param name="text">Text to encode</param>
		/// <returns>'null', '""', '"foo"', '"\""', '"\u0000"', ...</returns>
		/// <remarks>String with the correct escaping and surrounded by double-quotes (<c>"..."</c>), or <c>"null"</c> if <paramref name="text"/> is <c>null</c></remarks>
		/// <example>EncodeJsonString("foo") => "\"foo\""</example>
		public static string Encode(string? text)
		{
			// handle quickly the easy cases
			if (text == null)
			{ // => null
				return "null";
			}
			if (text.Length == 0)
			{ // => ""
				return "\"\"";
			}
			
			// first check if we actually need to encode anything
			if (NeedsEscaping(text))
			{ // yes => slow path
				return EncodeSlow(text);
			}

			// nothing to do, except add the double quotes
			return string.Concat("\"", text, "\"");
		}

		internal static string EncodeSlow(string text)
		{
			// note: we assume that the typical overhead of escaping characters will be up to 6 characters if there is only one or two "invalid" characters
			// this assumption totally breaks down for non-latin languages!
			var sb = StringBuilderCache.Acquire(checked(text.Length + 2 + 6));
			return StringBuilderCache.GetStringAndRelease(AppendSlow(sb, text, true));
		}

		/// <summary>Encodes a string literal that must be written to a JSON document (slow path)</summary>
		internal static StringBuilder AppendSlow(StringBuilder sb, string? text, bool includeQuotes)
		{
			if (text == null)
			{ // bypass
				return sb.Append("null");
			}
			return AppendSlow(sb, text.AsSpan(), includeQuotes);
		}

		/// <summary>Encodes a string literal that must be written to a JSON document (slow path)</summary>
		internal static unsafe StringBuilder AppendSlow(StringBuilder sb, ReadOnlySpan<char> text, bool includeQuotes)
		{
			// We check and encode in a single pass:
			// - we have a cursor on the last changed character (initially set to 0)
			// - as long as we see valid characters, we advance the cursor
			// - if we find a character that needs to be escaped (or reach the end of the string):
			//   - we copy the clean text from the previous cursor to the current position,
			//   - we encode the current character,
			//   - we advance the cursor to the next character
			//
			// A string that did not require any replacement will end up with the cursor still set to 0
			//
			// note: we do not encode the forward slash ('/'), to help distinguish with it, and '\/' that is frequently used to encode dates.

			if (includeQuotes) sb.Append('"');
			int i = 0, last = 0;
			int n = text.Length;
			fixed (char* str = text)
			{
				char* ptr = str;
				while (n-- > 0)
				{
					char c = *ptr++;
					if (c <= '/')
					{ // ASCII 0..47
						if (c == '"')
						{ // " -> \"
							goto escape_backslash;
						}
						else if (c >= ' ')
						{ // ASCII 32..47 : from space to '/'
							goto next; // => not modified
						}
						// ASCII 0..31 : encoded
						// - we directly escape any of \n, \r, \t, \b and \f
						// - all others will be escaped as Unicode: \uXXXX
						switch (c)
						{
							case '\n': c = 'n'; goto escape_backslash;
							case '\r': c = 'r'; goto escape_backslash;
							case '\t': c = 't'; goto escape_backslash;
							case '\b': c = 'b'; goto escape_backslash;
							case '\f': c = 'f'; goto escape_backslash;
						}
						// encode as \uXXXX
						goto escape_unicode;
					}
					else if (c == '\\')
					{ // \ -> \\
						goto escape_backslash;
					}
					if (c >= 0xD800 && (c < 0xE000 || c >= 0xFFFE))
					{ // warning, the Unicode range D800 - DFFF is used to escape non-BMP characters (> 0x10000), and FFFE/FFFF corresponds to BOM UTF-16 (LE/BE)
						goto escape_unicode;
					}
					// => skip
					goto next;

					// character encoded with a single backslash => \c
				escape_backslash:
					if (i > last) sb.Append(text.Slice(last, i - last));
					last = i + 1;
					sb.Append('\\').Append(c);
					goto next;

					// character encoded as Unicode using 16 bits
				escape_unicode:
					if (i > last) sb.Append(text.Slice(last, i - last));
					last = i + 1;
					sb.Append(@"\u").Append(((int) c).ToString("x4", NumberFormatInfo.InvariantInfo)); //TODO: PERF: optimize this!
					goto next;

				next:
					// no encoding required.
					++i;

				} // while
			} // fixed

			if (last == 0)
			{ // the text did not require any escaping
				sb.Append(text);
			}
			else if (last < text.Length)
			{ // append the tail that did not need any escaping
				sb.Append(text.Slice(last, text.Length - last));
			}
			return includeQuotes ? sb.Append('"') : sb;
		}

		/// <summary>Encodes a string literal into a JSON string, and appends the result to a StringBuilder</summary>
		/// <param name="sb">Target string builder</param>
		/// <param name="text">string literal to encode</param>
		/// <returns>The same StringBuilder builder instance</returns>
		/// <remarks>Note: appends <c>null</c> if <paramref name="text"/> is <see langword="null"/></remarks>
		public static StringBuilder Append(StringBuilder sb, string? text)
		{
			if (text == null)
			{ // null -> "null"
				return sb.Append("null");
			}
			if (text.Length == 0)
			{ // chaîne vide -> ""
				return sb.Append("\"\"");
			}
			if (!JsonEncoding.NeedsEscaping(text))
			{ // chaîne propre
				return sb.Append('"').Append(text).Append('"');
			}
			// chaîne qui nécessite (a priori) un encoding
			return AppendSlow(sb, text, true);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private static bool TryAppendChar(Span<char> buf, ref int cursor, char c)
		{
			if (cursor >= buf.Length)
			{
				return false;
			}
			buf[cursor++] = c;
			return true;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private static bool TryAppendChars(Span<char> buf, ref int cursor, char c1, char c2)
		{
			if (cursor + 1 >= buf.Length)
			{
				return false;
			}
			buf[cursor] = c1;
			buf[cursor + 1] = c2;
			cursor += 2;
			return true;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private static bool TryAppendChars(Span<char> buf, ref int cursor, ReadOnlySpan<char> literal)
		{
			if (cursor + literal.Length > buf.Length)
			{
				return false;
			}

			literal.CopyTo(buf.Slice(cursor));
			cursor += literal.Length;
			return true;
		}

		private static bool TryAppendUnicode(Span<char> buf, ref int cursor, int unicode)
		{
			// "\u####" = 6 characters
			if (cursor + 5 >= buf.Length)
			{
				return false;
			}

			ref char ptr = ref buf[cursor];

			// "\u" prefix
			Unsafe.Add(ref ptr, 0) = '\\';
			Unsafe.Add(ref ptr, 1) = 'u';

			// lower-case hexadecimal
			int b;

			b = (unicode >> 12) & 0xF;
			Unsafe.Add(ref ptr, 2) = (char) (b + (b < 10 ? 48 : 87));
			b = (unicode >> 8) & 0xF;
			Unsafe.Add(ref ptr, 3) = (char) (b + (b < 10 ? 48 : 87));
			b = (unicode >> 4) & 0xF;
			Unsafe.Add(ref ptr, 4) = (char) (b + (b < 10 ? 48 : 87));
			b = unicode & 0xF;
			Unsafe.Add(ref ptr, 5) = (char) (b + (b < 10 ? 48 : 87));

			cursor += 6;
			return true;
		}

		/// <summary>Encode a string of text that must be written to a JSON document (slow path)</summary>
		private static unsafe bool TryEncodeToSlow(Span<char> destination, ReadOnlySpan<char> text, bool includeQuotes, out int charsWritten)
		{
			// We check and encode in a single pass:
			// - we have a cursor on the last changed character (initially set to 0)
			// - as long as we see valid characters, we advance the cursor
			// - if we find a character that needs to be escaped (or reach the end of the string):
			//   - we copy the clean text from the previous cursor to the current position,
			//   - we encode the current character,
			//   - we advance the cursor to the next character
			//
			// A string that did not require any replacement will end up with the cursor still set to 0
			//
			// note: we do not encode the forward slash ('/'), to help distinguish with it, and '\/' that is frequently used to encode dates.

			charsWritten = 0;
			int cursor = 0;

			if (includeQuotes)
			{
				if (!TryAppendChar(destination, ref cursor, '"'))
				{
					return false;
				}
			}

			int i = 0, last = 0;
			int n = text.Length;
			fixed (char* str = text)
			{
				char* ptr = str;
				while (n-- > 0)
				{
					char c = *ptr++;
					if (c <= '/')
					{ // ASCII 0..47
						if (c == '"')
						{ // " -> \"
							goto escape_backslash;
						}
						if (c >= ' ')
						{ // ASCII 32..47 : from space to '/'
							goto next; // => not modified
						}
						// ASCII 0..31 : encoded
						// - we directly escape any of \n, \r, \t, \b and \f
						// - all others will be escaped as Unicode: \uXXXX
						switch (c)
						{
							case '\n': c = 'n'; goto escape_backslash;
							case '\r': c = 'r'; goto escape_backslash;
							case '\t': c = 't'; goto escape_backslash;
							case '\b': c = 'b'; goto escape_backslash;
							case '\f': c = 'f'; goto escape_backslash;
						}
						// encode as \uXXXX
						goto escape_unicode;
					}
					if (c == '\\')
					{ // \ -> \\
						goto escape_backslash;
					}
					if (c >= 0xD800 && (c < 0xE000 || c >= 0xFFFE))
					{ // warning, the Unicode range D800 - DFFF is used to escape non-BMP characters (> 0x10000), and FFFE/FFFF corresponds to BOM UTF-16 (LE/BE)
						goto escape_unicode;
					}
					// => skip
					goto next;

					// character encoded with a single backslash => \c
				escape_backslash:
					if (i > last)
					{
						if (!TryAppendChars(destination, ref cursor, text.Slice(last, i - last)))
						{
							return false;
						}
					}
					last = i + 1;
					if (!TryAppendChars(destination, ref cursor, '\\', c))
					{
						return false;
					}
					goto next;

					// character encoded as Unicode using 16 bits
				escape_unicode:
					if (i > last)
					{
						if (!TryAppendChars(destination, ref cursor, text.Slice(last, i - last)))
						{
							return false;
						}
					}
					last = i + 1;
					if (!TryAppendUnicode(destination, ref cursor, (int) c))
					{
						return false;
					}
					goto next;

				next:
					// no encoding required.
					++i;

				} // while
			} // fixed

			if (last == 0)
			{ // the text did not require any escaping
				if (!TryAppendChars(destination, ref cursor, text))
				{
					return false;
				}
			}
			else if (last < text.Length)
			{ // append the tail that did not need any escaping
				if (!TryAppendChars(destination, ref cursor, text.Slice(last, text.Length - last)))
				{
					return false;
				}
			}

			if (includeQuotes)
			{
				if (!TryAppendChar(destination, ref cursor, '"'))
				{
					return false;
				}
			}

			charsWritten = cursor;
			return true;
		}

		/// <summary>Encodes a string literal into a JSON string, and appends the result to a StringBuilder</summary>
		/// <param name="destination">Target buffer where the encoding JSON literal will be written</param>
		/// <param name="text">input string literal to encode</param>
		/// <param name="charsWritten">Receives the number of characters written to <paramref name="destination"/>, if it was large enough</param>
		/// <returns><see langword="true"/> if the buffer was large enough; otherwise, <see langword="false"/>.</returns>
		public static bool TryEncodeTo(Span<char> destination, ReadOnlySpan<char> text, out int charsWritten)
		{
			if (text.Length == 0)
			{ // empty string-> ""

				if (destination.Length < 2)
				{
					charsWritten = 0;
					return false;
				}

				destination[0] = '"';
				destination[1] = '"';
				charsWritten = 2;
				return true;
			}

			if (!JsonEncoding.NeedsEscaping(text))
			{ // no encoding required

				if (destination.Length < text.Length + 2)
				{
					charsWritten = 0;
					return false;
				}

				destination[0] = '"';
				text.CopyTo(destination.Slice(1));
				destination[text.Length + 1] = '"';
				charsWritten = text.Length + 2;
				return true;
			}

			// must encode
			return TryEncodeToSlow(destination, text, includeQuotes: true, out charsWritten);
		}

	}

	/// <summary>Very basic (and slow!) JSON text builder</summary>
	/// <remarks>Should be used for very small and infrequent JSON needs, when you don't want to reference a full JSON serializer</remarks>
	[PublicAPI]
	public sealed class SimpleJsonBuilder
	{
		internal enum Context
		{
			Top = 0,
			Object,
			Array
		}

		public struct State
		{
			internal int Index;
			internal Context Context;
		}

		public readonly StringBuilder Buffer;
		private State Current;

		public SimpleJsonBuilder(StringBuilder? buffer = null)
		{
			this.Buffer = buffer ?? new StringBuilder();
		}

		public State BeginObject()
		{
			this.Buffer.Append('{');
			var state = this.Current;
			this.Current = new State { Context = Context.Object };
			return state;
		}

		public void EndObject(State state)
		{
			if (this.Current.Context != Context.Object) throw new InvalidOperationException("Should be inside an object");
			this.Buffer.Append(this.Current.Index == 0 ? "}" : " }");
			this.Current = state;
		}

		public void WriteField(string field)
		{
			if (this.Current.Context != Context.Object) throw new InvalidOperationException("Must be inside an object");
			this.Buffer.Append(this.Current.Index++ > 0 ? ", \"" : "\"").Append(field).Append("\": ");
		}

		public void Add(string field, string value)
		{
			var sb = this.Buffer;
			WriteField(field);
			JsonEncoding.Append(sb, value);
		}

		public void Add(string field, bool value)
		{
			WriteField(field);
			this.Buffer.Append(value ? "true" : "false");
		}

		public void Add(string field, int value)
		{
			WriteField(field);
			this.Buffer.Append(StringConverters.ToString(value));
		}

		public void Add(string field, long value)
		{
			WriteField(field);
			this.Buffer.Append(StringConverters.ToString(value));
		}

		public void Add(string field, float value)
		{
			WriteField(field);
			this.Buffer.Append(StringConverters.ToString(value));
		}

		public void Add(string field, double value)
		{
			WriteField(field);
			this.Buffer.Append(StringConverters.ToString(value));
		}

		public void Add(string field, Guid value)
		{
			WriteField(field);
			this.Buffer.Append('"').Append(value == Guid.Empty ? string.Empty : value.ToString()).Append('"');
		}

		public State BeginArray()
		{
			this.Buffer.Append('[');
			var state = this.Current;
			this.Current = new State { Context = Context.Array };
			return state;
		}

		public void EndArray(State state)
		{
			if (this.Current.Context != Context.Array) throw new InvalidOperationException("Should be inside an array");
			this.Buffer.Append(this.Current.Index == 0 ? "]" : " ]");
			this.Current = state;
		}

		public void WriteArraySeparator()
		{
			if (this.Current.Context != Context.Array) throw new InvalidOperationException("Should be inside an array");
			if (this.Current.Index++ > 0) this.Buffer.Append(", ");
		}

		public void Add(string value)
		{
			WriteArraySeparator();
			JsonEncoding.Append(this.Buffer, value);
		}

		public void Add(bool value)
		{
			WriteArraySeparator();
			this.Buffer.Append(value ? "true" : "false");
		}

		public void Add(int value)
		{
			WriteArraySeparator();
			this.Buffer.Append(StringConverters.ToString(value));
		}

		public void Add(long value)
		{
			WriteArraySeparator();
			this.Buffer.Append(StringConverters.ToString(value));
		}

		public void Add(float value)
		{
			WriteArraySeparator();
			this.Buffer.Append(StringConverters.ToString(value));
		}

		public void Add(double value)
		{
			WriteArraySeparator();
			this.Buffer.Append(StringConverters.ToString(value));
		}

		public void Add(Guid value)
		{
			WriteArraySeparator();
			this.Buffer.Append('"').Append(value == Guid.Empty ? string.Empty : value.ToString()).Append('"');
		}

		public override string ToString()
		{
			return this.Buffer.ToString();
		}
	}

}
