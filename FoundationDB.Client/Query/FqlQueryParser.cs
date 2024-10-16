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

#if NET8_0_OR_GREATER

namespace FoundationDB.Client
{
	using System;
	using System.Buffers;
	using System.Text;

	public static class FqlQueryParser
	{

		public static FqlQuery Parse(ReadOnlySpan<char> text)
		{

			FqlDirectoryExpression? directoryExpr = null;
			FqlTupleExpression? tupleExpr = null;

			var remaining = text;

			while(remaining.Length != 0)
			{
				switch (remaining[0])
				{
					case '/':
					{
						if (tupleExpr != null) throw new FormatException("Invalid directory after tuple");

						remaining = remaining[1..];
						var segment = ReadDirectory(ref remaining);

						(directoryExpr ??= new ()).Add(segment);

						break;
					}
					case '(':
					{
						if (tupleExpr != null) throw new FormatException("Only one tuple expression per query");
						remaining = remaining[1..];
						tupleExpr = ReadTuple(ref remaining);
						break;
					}
					default:
					{
						throw new InvalidOperationException($"oops! {text} : [{remaining}]");
					}
				}
			}

			return new() { Directory = directoryExpr, Tuple = tupleExpr };
		}

		public static readonly SearchValues<char> CategoryWhitespace = SearchValues.Create("\t ");
		public static readonly SearchValues<char> CategoryNewLine = SearchValues.Create("\t\r\n ");
		public static readonly SearchValues<char> CategoryDigit = SearchValues.Create("0123456789");
		public static readonly SearchValues<char> CategoryHexDigit = SearchValues.Create("0123456789ABCDEF");
		public static readonly SearchValues<char> CategoryName = SearchValues.Create("-.0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ_abcdefghijklmnopqrstuvwxyz");
		public static readonly SearchValues<char> CategoryText = SearchValues.Create(" !#$%&'()*+,-./0123456789:;<=>?@ABCDEFGHIJKLMNOPQRSTUVWXYZ[\\]^_`abcdefghijklmnopqrstuvwxyz{|}~");

		private static bool IsWhitespace(char c) => c is ' ' or '\t';
		private static bool IsNewLine(char c) => c is '\r' or '\n';

		private static FqlPathSegment ReadDirectory(ref ReadOnlySpan<char> text)
		{
			// directory = '/' ( '<>' | name | string ) [ directory ]

			// note: we already have consumed the '/'

			if (text.Length == 0)
			{
				return FqlPathSegment.Literal("", null);
			}

			char next = text[0];
			switch (next)
			{
				case '<':
				{ // "<>"?
					if (text.Length == 1)
					{
						throw new FormatException("Truncated '<'");
					}
					text = text[2..];
					return FqlPathSegment.Any();
				}
				case '"':
				{ // text?

					text = text[1..];
					var literal = ReadString(ref text);
					return FqlPathSegment.Literal(FdbPathSegment.Create(literal));
				}
				default:
				{
					if (CategoryName.Contains(next))
					{
						var literal = ReadName(ref text);
						return FqlPathSegment.Literal(FdbPathSegment.Create(literal));
					}

					throw new FormatException("TODO: unexpected char while reading directory");
				}
			}
		}

		private static string ReadString(ref ReadOnlySpan<char> text)
		{
			// we already consumed the '"'

			StringBuilder? sb = null;

			while(text.Length > 0)
			{
				// find the next "non-string" character
				int p = text.IndexOfAnyExcept(FqlQueryParser.CategoryText);

				// must be either a `"` or a `\`
				if (p < 0)
				{ // reached the end
					break;
				}

				if (text[p] != '"')
				{
					throw new FormatException("Invalid escape sequence in string");
				}

				// it is escaped if the previous character is a '\'
				if (p > 0 && text[p - 1] == '\\')
				{ // this is an escaped double-quote
					(sb ??= new()).Append(text[..(p - 1)]).Append('"');
					text = text[(p + 1)..];
					continue;
				}

				// end of string
				string name;
				if (sb == null)
				{
					// unescaped string
					name = text[..p].ToString();
				}
				else
				{
					sb.Append(text[..p]);
					name = sb.ToString();
				}
				text = text[(p + 1)..];
				return name;
			}

			throw new FormatException("TODO: truncated string");
		}

		private static string ReadName(ref ReadOnlySpan<char> text)
		{
			int p = text.IndexOfAnyExcept(CategoryName);

			string name;
			if (p < 0)
			{ // the rest of the string is a name
				name = text.ToString();
				text = default;
			}
			else
			{
				name = text[..p].ToString();
				text = text[p..];
			}
			return name;
		}

		private static FqlVariableTypes ReadVariable(ref ReadOnlySpan<char> text)
		{
			// we already have consumed the '<';

			FqlVariableTypes types = default;

			while (text.Length != 0)
			{
				int p = text.IndexOfAny('>', '|');
				if (p < 0) break;

				switch (text[..p])
				{
					case "nil": types |= FqlVariableTypes.Nil; break;
					case "bool": types |= FqlVariableTypes.Bool; break;
					case "int": types |= FqlVariableTypes.Int; break;
					case "uint": types |= FqlVariableTypes.UInt; break;
					case "float": types |= FqlVariableTypes.Float; break;
					case "string": types |= FqlVariableTypes.String; break;
					case "uuid": types |= FqlVariableTypes.Uuid; break;
					case "bytes": types |= FqlVariableTypes.Bytes; break;
					case "tuple": types |= FqlVariableTypes.Tuple; break;
					default:
					{
						throw new FormatException($"Invalid variable type '{text[..p]}'");
					}
				}

				if (text[p] == '>')
				{
					text = text[(p + 1)..];
					return types;
				}

				Contract.Debug.Assert(text[p] == '|');
				text = text[(p + 1)..];
			}

			throw new FormatException("Truncated variable");
		}

		private static bool CouldBeUuid(ReadOnlySpan<char> text) => text.Length >= 36 && text[8] == '-' && text[13] == '-' && text[18] == '-' && text[23] == '-';

		private static FqlTupleExpression ReadTuple(ref ReadOnlySpan<char> text)
		{
			// we already have consumed the '('

			if (text.Length == 0)
			{
				throw new FormatException("TODO: truncated tuple");
			}

			var tuple = new FqlTupleExpression();

			while (text.Length != 0)
			{
				var next = text[0];
				if (CategoryNewLine.Contains(next))
				{
					text = text[1..];
					continue;
				}

				switch (next)
				{
					case ')':
					{
						text = text[1..];
						return tuple;
					}
					case '.':
					{ // could be '...'
						if (text.StartsWith("..."))
						{
							tuple.AddMaybeMore();
							text = text[3..];
							continue;
						}

						break;
					}
					case 'n':
					{
						// could be 'nil'
						if (text.StartsWith("nil"))
						{
							tuple.AddNil();
							text = text[3..];
							continue;
						}

						break;
					}
					case 'f':
					{
						if (text.StartsWith("false"))
						{
							tuple.AddBoolean(false);
							text = text[5..];
							continue;
						}

						break;
					}
					case 't':
					{
						// could be 'true'
						if (text.StartsWith("true"))
						{
							tuple.AddBoolean(true);
							text = text[4..];
							continue;
						}

						break;
					}
					case >= '0' and <= '9':
					{
						if (next == '0' && text.Length >= 4 && text[1] == 'x')
						{
							text = text[2..];

							int p = text.IndexOfAnyExcept(FqlQueryParser.CategoryHexDigit);
							if (p < 2) throw new FormatException("Invalid bytes");

							var slice = Slice.FromHexString(text[..p]);
							text = text[p..];

							tuple.AddBytes(slice);
							continue;
						}

						// it could be a uuid, IF the next 36 chars are only hex digits with '-' at the correct place
						//TODO: check for uuid!
						if (CouldBeUuid(text))
						{
							if (Guid.TryParseExact(text[..36], "D", out var uuid))
							{
								text = text[36..];
								tuple.AddUuid(uuid);
								continue;
							}

						}

						// it's a number
						//TODO: parse a number!
						ulong num = (uint) (next - '0');
						text = text[1..];
						while (text.Length > 0)
						{
							next = text[0];
							if (!char.IsDigit(next))
							{
								break;
							}
							num = num * 10 + (uint) (next - '0');
							text = text[1..];
						}

						if (num <= int.MaxValue)
						{
							tuple.AddInt((int) num);
						}
						else if (num <= long.MaxValue)
						{
							tuple.AddInt((long) num);
						}
						else
						{
							tuple.AddUInt(num);
						}

						continue;
					}
					case >= 'a' and <= 'f':
					{
						if (CouldBeUuid(text) && Guid.TryParseExact(text[..36], "D", out var uuid))
						{
							text = text[36..];
							tuple.AddUuid(uuid);
							continue;
						}
						break;
					}
					case '"':
					{ // string

						text = text[1..];
						var x = ReadString(ref text);
						tuple.AddString(x);
						continue;
					}
					case '<':
					{ // variable

						text = text[1..];
						var types = ReadVariable(ref text);
						tuple.AddVariable(types);
						continue;
					}
					case ',':
					{
						text = text[1..];
						continue;
					}

					case '(':
					{ // sub-tuple!

						text = text[1..];
						var sub = ReadTuple(ref text);
						tuple.AddTuple(sub);
						continue;
					}

				}
				throw new FormatException($"Unexpected token '{next}' in tuple");
			}

			throw new FormatException("Truncated tuple");
		}

	}

}

#endif
