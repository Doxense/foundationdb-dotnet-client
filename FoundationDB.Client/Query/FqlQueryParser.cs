﻿#region Copyright (c) 2023-2024 SnowBank SAS, (c) 2005-2023 Doxense SAS
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
	using System.Globalization;
	using System.Numerics;
	using System.Text;

	public static class FqlQueryParser
	{

		public static FqlQuery Parse(ReadOnlySpan<char> text)
		{
			if (!ParseNext(text, out _).Check(out var res, out var error))
			{
				error.Throw();
				throw null!;
			}

			return res;
		}

		public static Maybe<FqlQuery> ParseNext(ReadOnlySpan<char> text, out ReadOnlySpan<char> rest)
		{
			rest = default;

			FqlDirectoryExpression? directoryExpr = null;
			FqlTupleExpression? tupleExpr = null;

			var remaining = text;

			bool complete = false;

			while(remaining.Length != 0 && !complete)
			{
				char c = remaining[0];
				switch (c)
				{
					case '/':
					{
						if (tupleExpr != null)
						{
							return new FormatException("Cannot have '/' after a tuple expression");
						}

						remaining = remaining[1..];
						if (!ReadDirectory(ref remaining).Check(out var segment, out var error))
						{
							return error;
						}

						if (directoryExpr == null)
						{
							directoryExpr = new();
							if (!segment.IsRoot)
							{ // add implicit root
								directoryExpr.Name(FqlPathSegment.Root());
							}
						}
						directoryExpr.Name(segment);


						break;
					}
					case '(':
					{
						if (tupleExpr != null)
						{
							return new FormatException("Only one tuple expression per query");
						}

						remaining = remaining[1..];
						if (!ReadTuple(ref remaining).Check(out var tuple, out var error))
						{
							return error;
						}
						tupleExpr = tuple;
						complete = true;
						break;
					}
					case '.':
					{
						if (tupleExpr != null)
						{
							return new FormatException("Cannot have '.' after a tuple expression");
						}

						// supported:
						// - "." => all in the current directory
						// - ".(...)" => all in the current with a tuple expression
						// -" ./foo/..." => a path starting from the current directory

						if (remaining.Length != 1 && remaining[1] is not ('/' or '('))
						{
							return new FormatException("Unexpected '.' in query expression");
						}
						
						directoryExpr ??= new();
						remaining = remaining[1..];
						break;
					}
					default:
					{
						if (char.IsWhiteSpace(c))
						{
							complete = true;
							break;
						}

						return new FormatException($"Unexpected token '{c}'");
					}
				}
			}

			rest = remaining;
			return new FqlQuery()
			{
				Text = text[..^remaining.Length].ToString(),
				Directory = directoryExpr,
				Tuple = tupleExpr
			};
		}

		public static readonly SearchValues<char> CategoryWhitespace = SearchValues.Create("\t ");
		public static readonly SearchValues<char> CategoryNewLine = SearchValues.Create("\t\r\n ");
		public static readonly SearchValues<char> CategoryDigit = SearchValues.Create("0123456789");
		public static readonly SearchValues<char> CategoryHexDigit = SearchValues.Create("0123456789ABCDEFabcdef");
		public static readonly SearchValues<char> CategoryNumber = SearchValues.Create("0123456789+-Ee.");
		public static readonly SearchValues<char> CategoryInteger = SearchValues.Create("0123456789+-");
		public static readonly SearchValues<char> CategoryName = SearchValues.Create("-.0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ_abcdefghijklmnopqrstuvwxyz");
		public static readonly SearchValues<char> CategoryText = SearchValues.Create(" !#$%&'()*+,-./0123456789:;<=>?@ABCDEFGHIJKLMNOPQRSTUVWXYZ[\\]^_`abcdefghijklmnopqrstuvwxyz{|}~");

		private static bool IsWhitespace(char c) => c is ' ' or '\t';
		private static bool IsNewLine(char c) => c is '\r' or '\n';

		private static Maybe<FqlPathSegment> ReadDirectory(ref ReadOnlySpan<char> text)
		{
			// directory = '/' ( '<>' | name | string ) [ directory ]

			// note: we already have consumed the '/'

			if (text.Length == 0)
			{
				return FqlPathSegment.Root();
			}

			char next = text[0];
			if (char.IsWhiteSpace(next))
			{
				return FqlPathSegment.Root();
			}

			switch (next)
			{
				case '<':
				{ // "<>"?
					if (text.Length == 1)
					{
						return new FormatException("Truncated '<'");
					}
					text = text[2..];
					return FqlPathSegment.Any();
				}
				case '"':
				{ // text?

					text = text[1..];
					if (!ReadString(ref text).Check(out var literal, out var error))
					{
						return error;
					}
					return FqlPathSegment.Literal(FdbPathSegment.Create(literal));
				}
				default:
				{

					if (CategoryName.Contains(next))
					{
						var literal = ReadName(ref text);
						return FqlPathSegment.Literal(FdbPathSegment.Create(literal));
					}

					return new FormatException($"TODO: unexpected char '{next}' while reading directory: [{text}]");
				}
			}
		}

		private static Maybe<string> ReadString(ref ReadOnlySpan<char> text)
		{
			// we already consumed the '"'

			StringBuilder? sb = null;

			while(text.Length > 0)
			{
				// find the next "non-string" character
				int p = text.IndexOfAnyExcept(CategoryText);

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

		private static (FqlVariableTypes Types, string? Name) ReadVariable(ref ReadOnlySpan<char> text)
		{
			// we already have consumed the '<';

			FqlVariableTypes types = default;

			while (text.Length != 0)
			{
				int p = text.IndexOfAny('>', '|', ':');
				if (p < 0) break;

				ReadOnlySpan<char> name = default;
				if (text[p] == ':')
				{ // we have a name
					name = text[..p];
					text = text[(p + 1)..];
					p = text.IndexOfAny('>', '|', ':');
					if (p < 0) break;
				}

				var type = FqlTupleItem.ParseVariableTypeLiteral(text[..p]);
				if (type == FqlVariableTypes.None)
				{
					throw new FormatException($"Invalid variable type '{text[..p]}'");
				}
				types |= type;

				if (text[p] == '>')
				{
					text = text[(p + 1)..];
					return (types, name.Length != 0 ? name.ToString() : null);
				}

				Contract.Debug.Assert(text[p] == '|');
				text = text[(p + 1)..];
			}

			throw new FormatException("Truncated variable");
		}

		private static bool CouldBeUuid(ReadOnlySpan<char> text) => text.Length >= 36 && text[8] == '-' && text[13] == '-' && text[18] == '-' && text[23] == '-';

		private static Maybe<FqlTupleExpression> ReadTuple(ref ReadOnlySpan<char> text)
		{
			// we already have consumed the '('

			if (text.Length == 0)
			{
				return new FormatException("TODO: truncated tuple");
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
							tuple.MaybeMore();
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
							tuple.Nil();
							text = text[3..];
							continue;
						}

						break;
					}
					case 'f':
					{
						if (text.StartsWith("false"))
						{
							tuple.Boolean(false);
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
							tuple.Boolean(true);
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

							int p = text.IndexOfAnyExcept(CategoryHexDigit);
							if (p < 2) throw new FormatException("Invalid bytes");

							var slice = Slice.FromHexString(text[..p]);
							text = text[p..];

							tuple.Bytes(slice);
							continue;
						}

						// it could be a uuid, IF the next 36 chars are only hex digits with '-' at the correct place
						//TODO: check for uuid!
						if (CouldBeUuid(text))
						{
							if (Guid.TryParseExact(text[..36], "D", out var uuid))
							{
								text = text[36..];
								tuple.Uuid(uuid);
								continue;
							}

						}

						// it's a number
						//TODO: parse a number!

						// could be an integer or a floating point number
						int q = text.IndexOfAnyExcept(CategoryNumber);
						if (q == 0) throw new FormatException("Invalid number literal: " + text.ToString());

						var chunk = q > 0 ? text[..q] : text;
						if (chunk.ContainsAnyExcept(CategoryInteger))
						{ // floating point

							if (!decimal.TryParse(chunk, CultureInfo.InvariantCulture, out var d128))
							{ // fits in 64 bits
								return new FormatException("Invalid floating point number");
							}

							var d64 = (double) d128;
							if ((decimal) d64 == d128)
							{ // no precision loss, we prefer using double
								tuple.Number(d64);
							}
							else
							{ // requires the full precision of decimal
								tuple.Number(d128);
							}
						}
						else
						{ // integer
							if (chunk[0] == '-')
							{ // negative
								if (long.TryParse(chunk, CultureInfo.InvariantCulture, out var l64))
								{ // fits in 64 bits
									tuple.Integer(l64);
								}
								else if (Int128.TryParse(chunk, CultureInfo.InvariantCulture, out var l128))
								{ // bit integer
									tuple.Integer(l128);
								}
								else if (BigInteger.TryParse(chunk, CultureInfo.InvariantCulture, out var big))
								{ // big integer
									tuple.Integer(big);
								}
								else
								{
									// this is not supposed to happen?
									return new FormatException("Could not parse integer literal");
								}
							}
							else
							{ // positive
								if (ulong.TryParse(chunk, CultureInfo.InvariantCulture, out var l64))
								{ // fits in 64 bits
									tuple.Integer(l64);
								}
								else if (UInt128.TryParse(chunk, CultureInfo.InvariantCulture, out var l128))
								{ // bit integer
									tuple.Integer(l128);
								}
								else if (BigInteger.TryParse(chunk, CultureInfo.InvariantCulture, out var big))
								{ // big integer
									tuple.Integer(big);
								}
								else
								{
									// this is not supposed to happen?
									return new FormatException("Could not parse integer literal");
								}
							}
						}

						text = chunk.Length == text.Length ? default : text[chunk.Length..];

						continue;
					}
					case >= 'a' and <= 'f':
					{
						if (CouldBeUuid(text) && Guid.TryParseExact(text[..36], "D", out var uuid))
						{
							text = text[36..];
							tuple.Uuid(uuid);
							continue;
						}
						break;
					}
					case '"':
					{ // string

						text = text[1..];
						if (!ReadString(ref text).Check(out var x, out var error))
						{
							return error;
						}
						tuple.String(x);
						continue;
					}
					case '<':
					{ // variable

						text = text[1..];
						var (types, name) = ReadVariable(ref text);
						tuple.Var(types, name);
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
						if (!ReadTuple(ref text).Check(out var sub, out var error))
						{
							return error;
						}
						tuple.Tuple(sub);
						continue;
					}

				}

				return new FormatException($"Unexpected token '{next}' in tuple");
			}

			return new FormatException("Truncated tuple");
		}

	}

}

#endif
