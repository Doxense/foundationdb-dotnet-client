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

namespace Doxense.Serialization.Json
{
	using System.Buffers;
	using System.Collections;
	using System.Globalization;
	using System.Text;

	/// <summary>Represents a path inside a JSON document to a nested child (ex: <c>"id"</c>, <c>"user.id"</c> <c>"tags[2].id"</c></summary>
	[PublicAPI]
	[DebuggerDisplay("{ToString(),nq}")]
	public readonly struct JsonPath : IEnumerable<(JsonPath Parent, ReadOnlyMemory<char> Key, Index Index, bool Last)>, IJsonSerializable, IJsonPackable, IJsonDeserializable<JsonPath>, IEquatable<JsonPath>, IEquatable<string>, ISpanFormattable
	{
		// the goal is to wrap a string with the full path, and expose each "segment" as a ReadOnlySpan<char>, in order to reduce allocations

		/// <summary>The empty path (root of the document)</summary>
		public static readonly JsonPath Empty;

		/// <summary>String literal</summary>
		public readonly ReadOnlyMemory<char> Value;

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public JsonPath(ReadOnlyMemory<char> path) => this.Value = path;

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public JsonPath(string? path) => this.Value = string.IsNullOrEmpty(path) ? default : path.AsMemory();

		/// <summary>Returns a JsonPath that wraps a <see cref="ReadOnlySpan{T}">ReadOnlySpan&lt;char&gt;</see> literal</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static JsonPath Create(ReadOnlyMemory<char> path) => path.Length == 0 ? default : new(path);

		/// <summary>Returns a JsonPath that wraps a <see cref="ReadOnlySpan{T}">ReadOnlySpan&lt;char&gt;</see> literal</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static JsonPath Create(ReadOnlySpan<char> path) => path.Length == 0 ? default : new(path.ToString());

		/// <summary>Returns a JsonPath that wraps a <see cref="string">ReadOnlySpan&lt;char&gt;</see> literal</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static JsonPath Create(string? path) => string.IsNullOrEmpty(path) ? default : new(path.AsMemory());

		/// <summary>Returns a JsonPath that wraps an index</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static JsonPath Create(int index) => index switch
		{
			0 => new("[0]"),
			1 => new("[1]"),
			2 => new("[2]"),
			_ => new($"[{index}]"),
		};

		/// <summary>Returns a JsonPath that wraps an index</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static JsonPath Create(Index index) => !index.IsFromEnd ? Create(index.Value) : index.Value == 1 ? new("[^1]") : new JsonPath($"[{index}]");

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static explicit operator JsonPath(string? path) => path is null ? default : new(path.AsMemory());

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public Tokenizer GetEnumerator() => new(this);

		IEnumerator<(JsonPath, ReadOnlyMemory<char>, Index, bool)> IEnumerable<(JsonPath Parent, ReadOnlyMemory<char> Key, Index Index, bool Last)>.GetEnumerator() => new Tokenizer(this);

		IEnumerator IEnumerable.GetEnumerator() => new Tokenizer(this);

		public List<string> GetSegments()
		{
			var path = this.Value;
			var tail = path;
			List<string> res = [ ];

			while (tail.Length > 0)
			{
				var consumed = ParseNext(tail.Span, out var keyLength, out var index);
				Contract.Debug.Assert(consumed != 0 && consumed >= keyLength);

				if (keyLength > 0)
				{
					// there is high probably that the key is a single key name, in which case chunk == path
					// => we will try to extract the original string to reduce allocations as much as possible!

					var chunk = tail[..keyLength];

					if (chunk.TryGetString(out var s))
					{ // we got the original string
						res.Add(DecodeKeyName(s)); // returns the same string instance if not escaped
					}
					else if (chunk.Span.Contains('\\'))
					{ // the key is escaped, must be decoded
						int l = GetDecodedKeyNameSize(chunk.Span);
						s = string.Create(l, chunk, (buf, c) =>
						{
							if (!TryDecodeKeyName(c.Span, buf, out int written) || written != buf.Length)
							{ // should NOT happen! => decoded count does not match expected length ???
								throw new InvalidOperationException("Internal decoding error");
							}
						});
						res.Add(s);
					}
					else
					{ // the key does not need to be decoded
						res.Add(chunk.ToString());
					}
				}
				else
				{
					res.Add($"[{index}]");
				}

				tail = tail.Slice(consumed);
			}

			return res;
		}

		public int GetSegmentCount()
		{
			var tail = Value.Span;
			int count = 0;
			while (tail.Length != 0)
			{
				++count;
				int consumed = ParseNext(tail, out _, out _);
				Contract.Debug.Assert(consumed != 0);

				tail = tail.Slice(consumed);
			}
			return count;
		}



		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public override string ToString() => this.Value.GetStringOrCopy();

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public string ToString(string? format, IFormatProvider? formatProvider) => this.Value.GetStringOrCopy();

		public bool TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format, IFormatProvider? provider)
		{
			//TODO: what kind of formats should be allowed?
			var path = this.Value.Span;
			if (!path.TryCopyTo(destination))
			{
				charsWritten = 0;
				return false;
			}

			charsWritten = path.Length;
			return true;
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public override int GetHashCode() => string.GetHashCode(this.Value.Span);

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public override bool Equals(object? obj) => obj switch
		{
			null       => this.Value.Length == 0,
			JsonPath p => Equals(p),
			string s   => Equals(s),
			_          => false
		};

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool Equals(JsonPath other) => this.Value.Span.SequenceEqual(other.Value.Span);

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool Equals(string? other) => this.Value.Span.SequenceEqual(other.AsSpan()); // null|"" == Empty

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool Equals(ReadOnlySpan<char> other) => this.Value.Span.SequenceEqual(other);

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator ==(JsonPath left, JsonPath right) => left.Equals(right);

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator !=(JsonPath left, JsonPath right) => !left.Equals(right);

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator ==(JsonPath left, string? right) => left.Equals(right);

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator !=(JsonPath left, string? right) => !left.Equals(right);

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator ==(JsonPath left, ReadOnlySpan<char> right) => left.Equals(right);

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator !=(JsonPath left, ReadOnlySpan<char> right) => !left.Equals(right);

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public ReadOnlySpan<char> AsSpan() => this.Value.Span;

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public ReadOnlyMemory<char> AsMemory() => this.Value;

		/// <summary>Tests if this the empty path (root of the document)</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool IsEmpty() => this.Value.Length == 0;

		/// <summary>Throws an exception if the specified path is empty</summary>
		/// <param name="path">Path to check</param>
		/// <param name="paramName">Name of the parameter that contains the path</param>
		/// <exception cref="ArgumentException">if <paramref name="path"/> is empty</exception>
		public static void ThrowIfEmpty(JsonPath path, [CallerArgumentExpression(nameof(path))] string? paramName = null)
		{
			if (path.Value.Length == 0)
			{
				throw ThrowHelper.ArgumentException(paramName ?? nameof(path), "Path cannot be empty");
			}
		}

		/// <summary>Throws an exception if the specified path is empty</summary>
		/// <param name="path">Path to check</param>
		/// <param name="paramName">Name of the parameter that contains the path</param>
		/// <exception cref="ArgumentException">if <paramref name="path"/> is empty</exception>
		public static void ThrowIfEmpty(string path, [CallerArgumentExpression(nameof(path))] string? paramName = null)
		{
			if (string.IsNullOrEmpty(path))
			{
				throw ThrowHelper.ArgumentException(paramName ?? nameof(path), "Path cannot be empty");
			}
		}

		/// <summary>Appends an index to this path (ex: <c>JsonPath.Return("tags")[1]</c> => "tags[1]")</summary>
		public JsonPath this[int index]
		{
			get
			{
				Contract.Positive(index);

				if (index < 10)
				{ // "PATH[#]"
					return AppendOneDigit(this.Value.Span, index);
				}

				if (index < 100)
				{ // "PATH[##]"
					return AppendTwoDigits(this.Value.Span, index);
				}

				return AppendIndexSlow(this.Value.Span, index);

				static JsonPath AppendOneDigit(ReadOnlySpan<char> span, int index)
				{
					int len = span.Length;
					var buf = new char[len + 3];
					span.CopyTo(buf);
					buf[len + 0] = '[';
					buf[len + 1] = (char) ('0' + index);
					buf[len + 2] = ']';
					return new(buf);
				}

				static JsonPath AppendTwoDigits(ReadOnlySpan<char> span, int index)
				{
					int len = span.Length;
					var buf = new char[len + 4];
					span.CopyTo(buf);
					buf[len + 0] = '[';
					buf[len + 1] = (char) ('0' + (index / 10));
					buf[len + 2] = (char) ('0' + (index % 10));
					buf[len + 3] = ']';
					return new(buf);
				}

				static JsonPath AppendIndexSlow(ReadOnlySpan<char> span, int index)
				{
					return new($"{span}[{index}]");
				}
			}
		}

		/// <summary>Appends an index to this path (ex: <c>JsonPath.Return("tags")[^1]</c> => "tags[^1]")</summary>
		public JsonPath this[Index index]
		{
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get
			{
				if (!index.IsFromEnd)
				{
					return this[index.Value];
				}

				return AppendIndexSlow(this.Value.Span, index);

				static JsonPath AppendIndexSlow(ReadOnlySpan<char> span, Index index)
				{
					return new($"{span}[{index}]");
				}
			}
		}

		public bool StartsWith(ReadOnlySpan<char> prefix) => this.Value.Span.StartsWith(prefix);

		public bool EndsWith(ReadOnlySpan<char> suffix) => this.Value.Span.EndsWith(suffix);

		public bool StartsWith(char prefix)
#if NET9_0_OR_GREATER
			=> this.Value.Span.Length > 0 && Value.Span[0] == prefix;
#else
		{
			var span = this.Value.Span;
			return span.Length > 0 && span[0] == prefix;
		}
#endif

		public bool EndsWith(char suffix)
#if NET9_0_OR_GREATER
			=> this.Value.Span.EndsWith(suffix);
#else
		{
			var span = this.Value.Span;
			return span.Length > 0 && span[^1] == suffix;
		}
#endif


#if NET8_0_OR_GREATER

		private static readonly SearchValues<char> Needle = SearchValues.Create("\\.[]");

		/// <summary>Tests if a field name needs to be escaped</summary>
		/// <param name="name">Name of a field</param>
		/// <returns><see langword="true"/> if name contains at least one of '<c>\</c>', '<c>.</c>' or '<c>[</c>'</returns>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		internal static bool RequiresEscaping(ReadOnlySpan<char> name)
		{
			return name.ContainsAny(Needle);
		}

#else

		/// <summary>Tests if a field name needs to be escaped</summary>
		/// <param name="name">Name of a field</param>
		/// <returns><see langword="true"/> if name contains at least one of '<c>\</c>', '<c>.</c>' or '<c>[</c>'</returns>
		internal static bool RequiresEscaping(ReadOnlySpan<char> name)
		{
			ReadOnlySpan<char> mustEscapeCharacters = "\\.[]";
			return name.IndexOfAny(mustEscapeCharacters) >= 0;
		}

#endif

		public static string DecodeKeyName(string? key)
		{
			if (string.IsNullOrEmpty(key))
			{
				return "";
			}

			// if the key was escaped previously, it will contain at least one '\' character
			if (key.Contains('\\'))
			{
				return DecodeKeyNameSlow(key.AsSpan());
			}

			return key;
		}

		public static ReadOnlySpan<char> DecodeKeyName(ReadOnlySpan<char> key)
		{
			if (key.Length == 0)
			{
				return default;
			}

			// if the key was escaped previously, it will contain at least one '\' character
			if (key.Contains('\\'))
			{
				return DecodeKeyNameSlow(key).AsSpan();
			}

			return key;
		}

		public static bool TryDecodeKeyName(ReadOnlySpan<char> key, Span<char> buffer, out int written)
		{
			if (key.Length == 0)
			{
				written = 0;
				return true;
			}

			// if the key was escaped previously, it will contain at least one '\' character
			if (key.Contains('\\'))
			{
				return TryDecodeKeyNameSlow(key, buffer, out written);
			}

			key.CopyTo(buffer);
			written = key.Length;
			return true;
		}

		/// <summary>Decode a potentially escaped key name (ex: <c>@"hello\\world"</c> => <c>@"hello\world"</c>)</summary>
		private static string DecodeKeyNameSlow(ReadOnlySpan<char> literal)
		{
			var tmp = ArrayPool<char>.Shared.Rent(literal.Length);
			Span<char> buf = tmp.AsSpan();
			int p = 0;
			bool escaped = false;
			foreach (var c in literal)
			{
				if (c == '\\')
				{
					if (escaped)
					{ // double '\\' means only one
						buf[p++] = '\\';
						escaped = false;
					}
					else
					{
						escaped = true;
					}
				}
				else
				{
					buf[p++] = c;
					escaped = false;
				}
			}
			var s = new string(tmp, 0, p);
			ArrayPool<char>.Shared.Return(tmp, clearArray: true);
			return s;
		}

		/// <summary>Decode a potentially escaped key name (ex: <c>@"hello\\world"</c> => <c>@"hello\world"</c>)</summary>
		private static bool TryDecodeKeyNameSlow(ReadOnlySpan<char> literal, Span<char> buffer, out int written)
		{
			int p = 0;
			bool escaped = false;
			foreach (var c in literal)
			{
				if (c == '\\')
				{
					if (escaped)
					{ // double '\\' means only one
						if (p + 1 > buffer.Length)
						{
							goto overflow;
						}
						buffer[p++] = '\\';
						escaped = false;
					}
					else
					{
						escaped = true;
					}
				}
				else
				{
					if (p + 1 > buffer.Length)
					{
						goto overflow;
					}
					buffer[p++] = c;
					escaped = false;
				}
			}

			written = p;
			return true;

		overflow:
			written = 0;
			return false;
		}

		/// <summary>Decode a potentially escaped key name (ex: <c>@"hello\\world"</c> => <c>@"hello\world"</c>)</summary>
		private static int GetDecodedKeyNameSize(ReadOnlySpan<char> literal)
		{
			int p = 0;
			bool escaped = false;
			foreach (var c in literal)
			{
				if (c == '\\')
				{
					if (escaped)
					{ // double '\\' means only one
						++p;
						escaped = false;
					}
					else
					{
						escaped = true;
					}
				}
				else
				{
					++p;
					escaped = false;
				}
			}

			// if escaped == true, then there is a single backslash at the end of the key
			// => this is probably a malformed key, but we won't deal with it here and simply add 1
			return p + (escaped ? 1 : 0);
		}

		public static string EncodeKeyName(string name)
		{
			if (string.IsNullOrEmpty(name) || !RequiresEscaping(name.AsSpan()))
			{
				return name;
			}
			return EncodeKeyNameWithPrefix(default, name.AsSpan());
		}

		public static ReadOnlySpan<char> EncodeKeyName(ReadOnlySpan<char> name)
		{
			if (name.Length == 0 || !RequiresEscaping(name))
			{
				//TODO: should we make a copy here?
				return name.ToString().AsSpan();
			}
			return EncodeKeyNameWithPrefix(default, name).AsSpan();
		}

		private static string EncodeKeyNameWithPrefix(ReadOnlySpan<char> prefix, ReadOnlySpan<char> name)
		{
			//note: we already know that the name requires escaping!

			if (name.Length == 0)
			{
				return prefix.Length != 0 ? string.Concat(prefix, ".") : "";
			}

			// can take up to twice the size (if all characters must be escaped)
			int capacity = checked(prefix.Length + (prefix.Length != 0 ? 1 : 0) + name.Length * 2);

			char[]? array = null;
			Span<char> buf = capacity > 512 ? (array = ArrayPool<char>.Shared.Rent(capacity)) : stackalloc char[capacity];

			int p = 0;
			if (prefix.Length != 0)
			{
				prefix.CopyTo(buf);
				buf[prefix.Length] = '.';
				p = prefix.Length + 1;
			}

			foreach (var c in name)
			{
				if (c is '.' or '\\' or '[' or ']')
				{
					buf[p++] = '\\';
				}

				buf[p++] = c;
			}

			var res = buf[..p].ToString();
			if (array is not null)
			{
				buf[..p].Clear();
				ArrayPool<char>.Shared.Return(array);
			}
			return res;
		}

		internal static bool TryEncodeKeyNameTo(Span<char> destination, out int charsWritten, ReadOnlySpan<char> name)
		{
			if (name.Length == 0)
			{
				charsWritten = 0;
				return true;
			}

			// it cannot be smaller than the string length
			if (destination.Length < name.Length) goto too_small;

			int p = 0;
			foreach (var c in name)
			{
				if (c is '.' or '\\' or '[' or ']')
				{
					if (p + 2 > destination.Length) goto too_small;
					destination[p ] = '\\';
					destination[p + 1] = c;
					p += 2;
				}
				else
				{
					if (p + 1 > destination.Length) goto too_small;
					destination[p] = c;
					p++;
				}
			}

			charsWritten = p;
			return true;

		too_small:
			charsWritten = 0;
			return false;

		}

		public JsonPath this[JsonPathSegment segment]
		{
			[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
			get => segment.Name.Length != 0 ? this[segment.Name.Span] : this[segment.Index];
		}

		/// <summary>Appends a field to this path (ex: <c>JsonPath.Return("user")["id"]</c> => "user.id")</summary>
		public JsonPath this[string key]
		{
			get
			{
				Contract.NotNull(key);

				if (RequiresEscaping(key.AsSpan()))
				{
					return new (EncodeKeyNameWithPrefix(this.Value.Span, key.AsSpan()));
				}

				int l = this.Value.Length;
				if (l == 0)
				{
					return new(key.AsMemory());
				}

				l = checked(l + 1 + key.Length);
				return new(string.Concat(this.Value.Span, ".", key));
			}
		}

#if NET9_0_OR_GREATER
		/// <summary>Holds a pair of <see cref="ReadOnlySpan{T}">ReadOnlySpan&lt;char&gt;</see></summary>
		/// <remarks>We need this to be able to pass a pair of spans to <see cref="string.Create{TState}"/></remarks>
		private readonly ref struct SpanPair
		{
			public SpanPair(ReadOnlySpan<char> key, ReadOnlySpan<char> value)
			{
				this.Key = key;
				this.Value = value;
			}

			public readonly ReadOnlySpan<char> Key;

			public readonly ReadOnlySpan<char> Value;
		}
#endif

		/// <summary>Appends a field to this path (ex: <c>JsonPath.Return("user")["xxxidxxx".AsSpan(3, 2)]</c> => "user.id")</summary>
		public JsonPath this[ReadOnlySpan<char> key]
		{
			get
			{

				// we may need to encode the key if it contains any of '\', '.' or '['
				if (RequiresEscaping(key))
				{
					return new (EncodeKeyNameWithPrefix(this.Value.Span, key));
				}

				int l = this.Value.Length;
				if (l == 0)
				{
					return new (key.ToString());
				}

				l = checked(l + 1 + key.Length);

#if NET9_0_OR_GREATER

				var path = string.Create(
					l,
					new SpanPair(key, this.Value.Span),
					(span, state) =>
					{
						state.Value.CopyTo(span);
						span = span[l..];
						span[0] = '.';
						span = span[1..];
						Contract.Debug.Assert(span.Length == state.Key.Length);
						state.Key.CopyTo(span);

					}
				);

				return new (path);

#elif NET8_0_OR_GREATER

				//TODO: PERF: we cannot use string.Create(..) here because ReadOnlySpan<char> is not allowed as generic type argument for SpanAction
				// => we will create an empty string and MUTATE it in place (yes, it's bad, but there's not really another way)
				var path = new string('\0', l);

				// DON'T TRY THIS AT HOME: force cast the ReadOnlySpan<char> to a Span<char>
				var span = new Span<char>(ref System.Runtime.InteropServices.MemoryMarshal.GetReference(path.AsSpan()));

				this.Value.Span.CopyTo(span);
				span = span[l..];
				span[0] = '.';
				span = span[1..];
				Contract.Debug.Assert(span.Length == key.Length);
				key.CopyTo(span);

				return new (path);

#else

				// we could use unsafe pointers, but we'll just bite the bullet and allocate a char[]
				var path = new char[l];
				var span = path.AsSpan();

				this.Value.Span.CopyTo(span);
				span = span[l..];
				span[0] = '.';
				span = span[1..];
				Contract.Debug.Assert(span.Length == key.Length);
				key.CopyTo(span);

				return new (path.AsMemory());

#endif
			}
		}

		private static string ConcatWithIndexer(ReadOnlyMemory<char> head, ReadOnlyMemory<char> tail)
		{
			int l = checked(head.Length + tail.Length);
			return string.Concat(head.Span, tail.Span);
		}

		private static string ConcatWithField(ReadOnlyMemory<char> head, ReadOnlyMemory<char> tail)
		{
			int l = checked(head.Length + 1 + tail.Length);
			return string.Concat(head.Span, ".", tail.Span);
		}

		public static JsonPath Combine(JsonPath parent, JsonPath child)
		{
			if (parent.IsEmpty()) return child;
			if (child.IsEmpty()) return parent;
			if (child.Value.Span[0] == '[')
			{
				return new(ConcatWithIndexer(parent.Value, child.Value));
			}
			else
			{
				return new(ConcatWithField(parent.Value, child.Value));
			}
		}

		/// <summary>Tests if this path is a parent of another path</summary>
		/// <example><code>
		/// "foo".IsParentOf("foo") => false
		/// "foo".IsParentOf("foo.bar") => true
		/// "foo".IsParentOf("foo[42]") => true
		/// "[42]".IsParentOf("[42].foo") => true
		/// "foo.bar".IsParentOf("foo") => false
		/// "foo[42]".IsParentOf("foo[42].bar") => true
		/// "foo[42]".IsParentOf("foo[42][^1]") => true
		/// </code></example>
		[Pure]
		public bool IsParentOf(JsonPath child) => IsParentOf(child.Value.Span);

		/// <summary>Tests if this path is a parent of another path</summary>
		/// <example><code>
		/// "foo".IsParentOf("foo") => false
		/// "foo".IsParentOf("foo.bar") => true
		/// "foo".IsParentOf("foo[42]") => true
		/// "[42]".IsParentOf("[42].foo") => true
		/// "foo.bar".IsParentOf("foo") => false
		/// "foo[42]".IsParentOf("foo[42].bar") => true
		/// "foo[42]".IsParentOf("foo[42][^1]") => true
		/// </code></example>
		[Pure]
		public bool IsParentOf(ReadOnlySpan<char> child)
		{
			var path = this.Value.Span;
			if (path.Length == 0) return child.Length != 0;
			if (child.Length <= path.Length) return false;
			if (!child.StartsWith(path)) return false;

			var tail = child[path.Length ..];
			if (tail[0] is not ('.' or '['))
			{ // parent: "foos", child: "foo.bar"
				return false;
			}

			if (path[^1] == ']')
			{ // parent: "foo[42]", child: "foo[42][^1] or "foo[42].bar"
				return tail[0] is '.' or '[';
			}

			return true;
		}

		/// <summary>Tests if this path is a child of another path</summary>
		/// <param name="parent">Path to the parent</param>
		/// <returns><see langword="true"/> if <paramref name="parent"/> is a parent of the current path; otherwise, <see langword="false"/>.</returns>
		/// <remarks>A path is not its own child, so <c>path.IsChildOf(path)</c> will be <see langword="false"/>.</remarks>
		/// <example><code>
		/// "foo".IsChildOf("foo") => false
		/// "foo.bar".IsChildOf("foo") => true
		/// "foo[42]".IsChildOf("foo") => true
		/// "[42].foo".IsChildOf("[42]") => true
		/// "foo".IsChildOf("foo.bar") => false
		/// "foo[42].bar".IsChildOf("foo[42]") => true
		/// "foo[42][^1]".IsChildOf("foo[42]") => true
		/// </code></example>
		[Pure]
		public bool IsChildOf(JsonPath parent) => IsChildOf(parent.Value.Span);

		/// <summary>Tests if this path is a child of another path</summary>
		/// <param name="parent">Path to the parent</param>
		/// <param name="relative">If the method returns <see langword="true"/>, receives the relative path from <paramref name="parent"/> to the current path</param>
		/// <returns><see langword="true"/> if <paramref name="parent"/> is a parent of the current path; otherwise, <see langword="false"/>.</returns>
		/// <remarks>A path is not its own child, so <c>path.IsChildOf(path)</c> will be <see langword="false"/>.</remarks>
		/// <example><code>
		/// "foo".IsChildOf("foo", out ...) => false, relative: ""
		/// "foo.bar".IsChildOf("foo", out ...) => true, relative: "bar"
		/// "foo[42]".IsChildOf("foo", out ...) => true, relative: "[42]
		/// "[42].foo".IsChildOf("[42]", out ...) => true, relative: "foo"
		/// "foo".IsChildOf("foo.bar", out ...) => false, relative: ""
		/// "foo[42].bar".IsChildOf("foo[42]", out ...) => true, relative: "bar"
		/// "foo[42][^1]".IsChildOf("foo[42]", out ...) => true, relative: "[^1]"
		/// </code></example>
		[Pure]
		public bool IsChildOf(JsonPath parent, out JsonPath relative) => IsChildOf(parent.Value.Span, out relative);

		/// <summary>Tests if this path is a child of another path</summary>
		/// <param name="parent">Path to the parent</param>
		/// <returns><see langword="true"/> if <paramref name="parent"/> is a parent of the current path; otherwise, <see langword="false"/>.</returns>
		/// <remarks>A path is not its own child, so <c>path.IsChildOf(path)</c> will be <see langword="false"/>.</remarks>
		/// <example><code>
		/// "foo".IsChildOf("foo") => false
		/// "foo.bar".IsChildOf("foo") => true
		/// "foo[42]".IsChildOf("foo") => true
		/// "[42].foo".IsChildOf("[42]") => true
		/// "foo".IsChildOf("foo.bar") => false
		/// "foo[42].bar".IsChildOf("foo[42]") => true
		/// "foo[42][^1]".IsChildOf("foo[42]") => true
		/// </code></example>
		[Pure]
		public bool IsChildOf(ReadOnlySpan<char> parent)
		{
			var path = this.Value.Span;
			if (parent.Length == 0) return path.Length != 0;
			if (path.Length <= parent.Length) return false;
			if (!path.StartsWith(parent)) return false;

			var tail = path[parent.Length ..];
			if (tail[0] is not ('.' or '['))
			{ // parent: "foos", child: "foo.bar"
				return false;
			}

			if (parent[^1] == ']')
			{ // parent: "foo[42]", child: "foo[42][^1] or "foo[42].bar"
				return tail[0] is '.' or '[';
			}

			return true;
		}

		/// <summary>Tests if this path is a child of another path</summary>
		/// <param name="parent">Path to the parent</param>
		/// <param name="relative">If the method returns <see langword="true"/>, receives the relative path from <paramref name="parent"/> to the current path</param>
		/// <returns><see langword="true"/> if <paramref name="parent"/> is a parent of the current path; otherwise, <see langword="false"/>.</returns>
		/// <remarks>A path is not its own child, so <c>path.IsChildOf(path)</c> will be <see langword="false"/>.</remarks>
		/// <example><code>
		/// "foo".IsChildOf("foo", out ...) => false, relative: ""
		/// "foo.bar".IsChildOf("foo", out ...) => true, relative: "bar"
		/// "foo[42]".IsChildOf("foo", out ...) => true, relative: "[42]
		/// "[42].foo".IsChildOf("[42]", out ...) => true, relative: "foo"
		/// "foo".IsChildOf("foo.bar", out ...) => false, relative: ""
		/// "foo[42].bar".IsChildOf("foo[42]", out ...) => true, relative: "bar"
		/// "foo[42][^1]".IsChildOf("foo[42]", out ...) => true, relative: "[^1]"
		/// </code></example>
		public bool IsChildOf(ReadOnlySpan<char> parent, out JsonPath relative)
		{
			relative = default;
			var path = this.Value.Span;
			if (parent.Length == 0)
			{ // from the root
				if (path.Length == 0)
				{
					return false;
				}
				relative = this;
				return true;
			}

			if (path.Length <= parent.Length) return false;
			if (!path.StartsWith(parent)) return false;

			var tail = path[parent.Length ..];
			if (tail[0] is not ('.' or '['))
			{ // parent: "foos", child: "foo.bar"
				return false;
			}

			if (parent[^1] == ']')
			{ // parent: "foo[42]", child: "foo[42][^1] or "foo[42].bar"
				if (tail[0] is '.' or '[')
				{
					relative = new(this.Value[(parent.Length + (tail[0] == '.' ? 1 : 0))..]);
					return true;
				}
				return false;
			}

			// parent: "foo", child: "foo.bar"
			relative = new (this.Value[(parent.Length + (tail[0] == '.' ? 1 : 0))..]);
			return true;
		}

		/// <summary>Tests if two paths are siblings (have the same parent)</summary>
		[Pure]
		public bool IsSibling(JsonPath sibling) => this.GetParent().Value.Span.SequenceEqual(sibling.GetParent().Value.Span);

#if NET8_0_OR_GREATER

		/// <summary>Return the common ancestor of both paths, as well as both relative branches from this ancestor to both paths</summary>
		[Pure]
		public JsonPath GetCommonAncestor(JsonPath other, out JsonPath left, out JsonPath right)
		{
			var thisSpan = this.Value.Span;
			var otherSpan = other.Value.Span;
			int n = thisSpan.CommonPrefixLength(otherSpan);
			if (n == 0)
			{ // nothing in common
				left = this;
				right = other;
				return default;
			}

			var thisTail = thisSpan[n..];
			var otherTail = otherSpan[n..];

			// test that we are not in the middle of a key name, ex: 'foo.bar' vs 'foo.baz' in which case common ancestor is not 'foo.ba' but 'foo'
			// we could also be in the middle of an indexer, ex: 'foo[41]', 'foo[42]' have 'foo[4' in common
			if ((thisTail.Length > 0 && thisTail[0] is not ('.' or '[')) || (otherTail.Length > 0 && otherTail[0] is not ('.' or '[')))
			{ // go back the to last '.' or ']'
				int p = thisSpan[..n].LastIndexOfAny('.', ']', '[');
				if (p < 0)
				{ // no common ancestor
					left = this;
					right = other;
					return default;
				}

				n = p;
			}

			// test if we are not the case "foo" vs "foobar"
			if (n == thisSpan.Length)
			{ // "foo" vs "foo.bar"
				left = default;
				right = new(other.Value[n..]);
				return new(this.Value[..n]);
			}

			if (n == otherSpan.Length)
			{ // "foo.bar" vs "foo"
				left = new(this.Value[n..]);
				right = default;
				return new(this.Value[..n]);
			}

			switch (thisSpan[n])
			{
				case '.': left = new(this.Value[(n + 1)..]); break;
				case '[': left = new(this.Value[n..]); break;
				default:
				{ // "foobar" vs "foo"
					throw new NotImplementedException();
				}
			}

			switch (otherSpan[n])
			{
				case '.': right = new(other.Value[(n + 1)..]); break;
				case '[': right = new(other.Value[n..]); break;
				default:
				{ // "foo" vs "foobar"
					throw new NotImplementedException();
				}
			}

			return new(this.Value[..n]);
		}

#endif

		/// <summary>Returns the parent path</summary>
		/// <remarks>Returns the empty path if the path is already empty</remarks>
		/// <example><code>
		/// "foo" => ""
		/// "foo.bar" => "foo"
		/// "foo.bar.baz" => "foo.bar"
		/// "foo.bar[1]" => "foo.bar"
		/// "foo.bar[1].baz" => "foo.bar[1]"
		/// </code></example>
		[Pure]
		public JsonPath GetParent()
		{
			var path = this.Value.Span;
			if (path.Length == 0) return default;

			int p = GetLastIndexOf(path, '.');
			int q = GetLastIndexOf(path, '[');
			return p < 0
				? (q < 0 ? default : new(this.Value[..q]))
				: q < 0 ? new(this.Value[..p]) 
					: new(this.Value[..Math.Max(p, q)]);
		}

		/// <summary>Find the position of the first non-escaped occurence of <paramref name="token"/> in <paramref name="literal"/></summary>
		/// <returns>Index of the first position where the token is not preceded by '\', or <see langword="-1"/> if there was none</returns>
		private static int GetIndexOf(ReadOnlySpan<char> literal, char token)
		{
			int p = literal.IndexOf(token);
			if (p < 0) return -1;
			int offset = 0;
			while (p > 0 && literal[p - 1] == '\\')
			{
				literal = literal[(p + 1)..];
				offset += p + 1;
				p = literal.IndexOf(token);
				if (p < 0) return -1;
			}
			return offset + p;
		}

		/// <summary>Find the position of the last non-escaped occurence of <paramref name="token"/> in <paramref name="literal"/></summary>
		/// <returns>Index of the last position where the token is not preceded by '\', or <see langword="-1"/> if there was none</returns>
		private static int GetLastIndexOf(ReadOnlySpan<char> literal, char token)
		{
			int p = literal.LastIndexOf(token);
			while (p > 0 && literal[p - 1] == '\\')
			{
				literal = literal[..(p - 1)];
				p = literal.LastIndexOf(token);
			}
			return p;
		}

		/// <summary>Tests if this path consists of a single key name</summary>
		/// <returns><see langword="true"/> if the path is a single property name (ex: <c>"Foo"</c>), or <see langword="false"/> if it is an array index (<c>"[1]"</c>) or has multiple segments (<c>"Foo.Bar"</c> or <c>"Foo[1]"</c>)</returns>
		/// <remarks>This can be used to speed up processing of selectors where the vast majority of cases is a single child</remarks>
		public bool TryGetSingleKey(out ReadOnlyMemory<char> key)
		{
			int consumed = ParseNext(this.Value.Span, out var keyLength, out _);

			if (keyLength == 0 || consumed >= this.Value.Length)
			{ // starts with an indexer, or more than one segment
				key = default;
				return false;
			}

			key = this.Value;
			return true;
		}
		
		/// <summary>Tests if the last segment of this path is a key</summary>
		/// <returns><see langword="true"/> if the path ends with a key name (ex: <c>"Foo.Bar"</c> or <c>"...[1].Bar"</c>), or <see langword="false"/> if the last segment is a key indexer (ex: <c>"...[1]"</c>), </returns>
		/// <example><code>
		/// "" => false
		/// [42] => false
		/// "foo" => true, "foo"
		/// "foo.bar" => true, "bar"
		/// "foo.bar[42]" => false
		/// "foo.bar[42].baz" => true, "baz"
		/// </code></example>
		[Pure]
		public bool TryGetLastKey(out ReadOnlySpan<char> key)
		{
			var path = this.Value.Span;
			if (path.Length == 0)
			{
				key = default;
				return false;
			}
			int p = GetLastIndexOf(path, '.');
			int q = GetLastIndexOf(path, '[');
			if (p < 0)
			{
				if (q < 0)
				{ // the path is a single key: "foo"
					key = DecodeKeyName(path);
					return true;
				}
				// the path ends with an indexer: "[42]" or "foo[42]"
				key = default;
				return false;
			}
			if (p <= q)
			{ // the last key is before the last indexer: "foo.bar[42]"
				key = default;
				return false;
			}
			// the last key is after the last indexer: "[42].foo" or "foo[42].bar"
			key = DecodeKeyName(path[(p + 1)..]);
			return true;
		}

		/// <summary>Returns the last segment as a key, unless it is an index</summary>
		/// <example><code>
		/// "" => ""
		/// [42] => ""
		/// "foo" => "foo"
		/// "foo.bar" => "bar"
		/// "foo.bar[42]" => ""
		/// "foo.bar[42].baz" => "baz"
		/// </code></example>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public ReadOnlySpan<char> GetLastKey() => TryGetLastKey(out var key) ? key : default;

		/// <summary>Tests if this path consists of a single array index</summary>
		/// <returns><see langword="true"/> if the path is a single array index (ex: <c>"[123]"</c>), or <see langword="false"/> if it is a key name (ex: <c>"foo"</c>) or has multiple segments (<c>"foo[1]"</c>)</returns>
		/// <remarks>This can be used to speed up processing of selectors where the vast majority of cases is a single child</remarks>
		public bool TryGetSingleIndex(out Index index)
		{
			int consumed = ParseNext(this.Value.Span, out var keyLength, out index);

			if (keyLength != 0 || consumed >= this.Value.Length)
			{ // starts with a key name, or more than one segment
				index = default;
				return false;
			}

			return true;
		}

		/// <summary>Tests if the last segment of this path is an index</summary>
		/// <returns><see langword="true"/> if the path ends with an array indexer (ex: <c>"foo[1]"</c> or <c>"foo.bar[1]"</c>), or <see langword="false"/> if the last segment is a key name (ex: <c>"foo.bar"</c> or <c>"foo[1].bar"</c>), </returns>
		/// <example><code>
		/// "" => false
		/// "foo" => false
		/// "[42]" => true, 42
		/// "[^1]" => true, ^1
		/// "foo.bar" => false
		/// "foo.bar[^1]" => true, ^1
		/// "foo.bar[^1].baz" => false
		/// </code></example>
		[Pure]
		public bool TryGetLastIndex(out Index index)
		{
			var path = this.Value.Span;
			if (path.Length == 0)
			{
				index = default;
				return false;
			}

			int p = GetLastIndexOf(path, '.');
			int q = GetLastIndexOf(path, '[');

			if (q < 0)
			{
				if (p < 0)
				{ // no quote and no index, it is a single key
					index = default;
					return false;
				}
				// ends with an index
			}
			else if (q < p)
			{ // key access after the last index
				index = default;
				return false;

			}

			var literal = path[(q + 1)..];
			p = GetIndexOf(literal, ']');
			if (p <= 0)
			{
				index = default;
				return false;
			}

			literal = literal[..p];
			bool fromEnd = false;
			if (literal[0] == '^')
			{
				literal = literal[1..];
				fromEnd = true;
			}

#if NET8_0_OR_GREATER
			if (!int.TryParse(literal, CultureInfo.InvariantCulture, out var result))
			{
				index = default;
				return false;
			}
#else
			if (!int.TryParse(literal.ToString(), out var result))
			{
				index = default;
				return false;
			}
#endif
			index = new Index(result, fromEnd);
			return true;
		}

		/// <summary>Returns the last segment as an index, or null if it is a field access</summary>
		/// <example><code>
		/// "" => false
		/// "foo" => null
		/// "[42]" => 42
		/// "[^1]" => ^1
		/// "foo.bar" => null
		/// "foo.bar[^1]" => ^1
		/// "foo.bar[^1].baz" => null
		/// </code></example>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public Index? GetLastIndex() => TryGetLastIndex(out var index) ? index : null;

		[Pure]
		public static int ParseNext(ReadOnlySpan<char> path, out int keyLength, out Index index)
		{
			if (path.Length == 0)
			{ // empty: "" => "", _, _
				keyLength = 0;
				index = default;
				return 0;
			}
			int p = GetIndexOf(path, '.'); // if >=0, could be "foo.bar" or "foo[42].bar" or "[42].foo"
			int q = GetIndexOf(path, '['); // if >=0, could be "[42]" or "foo[42]"

			// foo         : p < 0, q < 0	=> "foo" / ""
			// foo.bar     : p = 3, q < 0	=> "foo" / "bar"

			// [42]        : p < 0, q = 0   => "[42]" / ""
			// foo[42]     : p < 0, q = 3	=> "foo" / "[42]"

			// [42].foo	   : p = 4, q = 0   => "[42]" / "foo"
			// [42][^1].foo: p = 8, q = 0   => "[42]" / "[^1].foo"

			// foo[42].bar : p = 7, q = 3	=> "foo" / "[42].bar"

			// foo.bar[42] : p = 3, q = 7	=> "foo" / "bar[42]"


			if (q < 0)
			{ // no indexing at all, either key or key.key

				if (p < 0)
				{ // single field access: "foo" => "", "foo", _, 3
					keyLength = path.Length;
					index = default;
					return keyLength;
				}

				// multiple fields access: "foo.bar.baz" => "bar.baz", "foo", _, 4
				keyLength = p;
				index = default;
				return p + 1;
			}

			if (p < 0)
			{ // no sub-field access: either key[index] or just [index] (or maybe [index][index]...)

				if (q > 0)
				{ // key[index]: "foo[42]" => "[42]", "foo", _, 3
					keyLength = q;
					index = default;
					return q;
				}

				//either [x] or [x][y]...
				int r = GetIndexOf(path, ']');
				if (r < q) throw ThrowHelper.FormatException("Invalid JSON Path: missing required ']' in indexer.");
				
				// [index]: "[42]" => "", _, 42, 4
				keyLength = 0;
				index = ParseIndex(path[1..r]);
				return r + 1;
			}

			if (q == 0)
			{ // [index].bar: "[42].bar" => "bar", "[42]", _, 4
				int r = GetIndexOf(path, ']');
				if (r < q) throw ThrowHelper.FormatException("Invalid JSON Path: missing required ']' in indexer.");
				keyLength = 0;
				index = ParseIndex(path[1..r]);
				if (r + 1 == p)
				{ // [index].key
					return r + 2; // skip the dot
				}
				else
				{ // [index][index].key
					return r + 1;
				}
			}

			if (p > q)
			{ // key[index].key: "foo[42].bar" => "[42].bar", _, 42, 4
				keyLength = q;
				index = default;
				return q;
			}

			// key.key[index]: "foo.bar[42]" => "bar[42]", "foo", _, 4
			keyLength = p; // "foo"
			index = default;
			return p + 1;
		}

		[Pure]
		private static Index ParseIndex(ReadOnlySpan<char> literal)
		{
			if (literal.Length == 0) throw ThrowHelper.FormatException("Invalid JSON Path: empty [] clause.");
			bool fromEnd = false;
			if (literal[0] == '^')
			{
				fromEnd = true;
				literal = literal[1..];
			}

#if NET8_0_OR_GREATER
			if (!int.TryParse(literal, CultureInfo.InvariantCulture, out var result))
			{
				throw ThrowHelper.FormatException("Invalid JSON Path: invalid [..] clause.");
			}
#else
			if (!int.TryParse(literal, out var result))
			{
				throw ThrowHelper.FormatException("Invalid JSON Path: invalid [..] clause.");
			}
#endif

			return new Index(result, fromEnd);
		}

		/// <summary>Returns a subsection of this path, beginning at a specified position and continuing to its end.</summary>
		/// <param name="start">Number of segments to skip</param>
		/// <returns>Slice of the path</returns>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public JsonPath Slice(int start) => new(this.Value.Slice(start));

		/// <summary>Returns a subsection of this path, starting at <paramref name="start" /> position for <paramref name="length" /> segments.</summary>
		/// <param name="start">Number of segments to skip</param>
		/// <param name="length">Number of segments to include</param>
		/// <returns>Slice of the path</returns>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public JsonPath Slice(int start, int length) => new(this.Value.Slice(start, length));

		/// <summary>Returns a subsection of this path, corresponding to the specified range.</summary>
		/// <param name="range">Range of the path to return</param>
		/// <returns>Slice of the path</returns>
		public JsonPath this[Range range]
		{
			[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
			get => new(this.Value[range]);
		}

		public JsonValue ToJson() => JsonString.Return(this.Value.GetStringOrCopy());

		void IJsonSerializable.JsonSerialize(CrystalJsonWriter writer) => writer.WriteValue(this.Value.Span);

		JsonValue IJsonPackable.JsonPack(CrystalJsonSettings settings, ICrystalJsonTypeResolver resolver) => ToJson();

		static JsonPath IJsonDeserializable<JsonPath>.JsonDeserialize(JsonValue value, ICrystalJsonTypeResolver? resolver)
		{
			if (value.IsNullOrMissing()) return default;
			if (value is not JsonString str) throw new JsonBindingException("JsonPath must be represented as a string");
			return new JsonPath(str.Value.AsMemory());
		}

		public struct Tokenizer : IEnumerator<(JsonPath Parent, ReadOnlyMemory<char> Key, Index Index, bool Last)>
		{

			private JsonPath Path;
			private ReadOnlyMemory<char> Tail;
			private int Offset;
			private int Consumed;
			public ReadOnlyMemory<char> Key;
			public Index Index;
			public int Depth;

			public Tokenizer(JsonPath path)
			{
				this.Path = path;
				this.Tail = path.Value;
				this.Offset = 0;
				this.Consumed = 0;
				this.Key = default;
				this.Index = default;
				this.Depth = -1;
			}

			public void Reset()
			{
				this.Tail = this.Path.Value;
				this.Offset = 0;
				this.Consumed = 0;
				this.Key = default;
				this.Index = default;
				this.Depth = -1;
			}

			public bool MoveNext()
			{
				var tail = this.Tail;
				if (tail.Length == 0)
				{
					return false;
				}

				++this.Depth;

				var consumed = ParseNext(tail.Span, out var keyLength, out this.Index);
				Contract.Debug.Assert(consumed >= keyLength);

				if (consumed == 0)
				{
					this.Key = default;
					this.Consumed = 0;
					this.Tail = default;
					Contract.Debug.Assert(this.Offset == this.Path.Value.Length);
					return false;
				}

				this.Key = keyLength > 0 ? tail[..keyLength] : default;

				if (keyLength > 0 && RequiresEscaping(this.Key.Span))
				{ // we need to decode the key to remove any '\'
					//HACKHACK: OPTIMIZE: TODO: this allocates inside the tokenizer which is usually in the hot path of JsonValue.GetPath(...) !!
					// => we _could_ use a small temp buffer from a pool, but we would need to make SURE that nobody can capture the Key
					//    this is currently a ReadOnlyMemory<char> and would need to be changed into a ReadOnlySpan<char> ?
					this.Key = DecodeKeyNameSlow(this.Key.Span).AsMemory();
				}

				this.Offset += this.Consumed;
				this.Tail = tail.Slice(consumed);
				this.Consumed = consumed;
				Contract.Debug.Ensures(!(this.Key.Length > 0 && (!this.Index.Equals(default)))); // both cannot be true at the same time
				Contract.Debug.Ensures(this.Consumed > 0); // we should have advanced in the path
				Contract.Debug.Ensures((uint) this.Offset < this.Path.Value.Length); // Path must not be fully consumed (only when we return false)
				Contract.Debug.Ensures(this.Offset + this.Consumed <= this.Path.Value.Length);
				return true;
			}

			public (JsonPath Parent, ReadOnlyMemory<char> Key, Index Index, bool Last) Current
			{
				[MethodImpl(MethodImplOptions.AggressiveInlining)]
				get
				{
					var parent = this.Path.Value[..this.Offset];
					if (parent.Length > 0 && parent.Span[^1] == '.')
					{
						parent = parent[..^1];
					}
					return (new JsonPath(parent), this.Key, this.Index, this.Offset + this.Consumed == this.Path.Value.Length);
				}
			}

			object IEnumerator.Current => this.Current;

			public void Dispose()
			{
				this.Path = default;
				this.Tail = default;
				this.Offset = default;
				this.Consumed = 0;
				this.Key = default;
				this.Index = default;
			}

		}

		public static void WriteTo(StringBuilder sb, string key) => WriteTo(sb, key.AsSpan());

		public static void WriteTo(StringBuilder sb, ReadOnlySpan<char> key)
		{
			if (!RequiresEscaping(key))
			{
				sb.Append(key);
			}
			else
			{
				AppendEscaped(sb, key);
			}

			static void AppendEscaped(StringBuilder sb, ReadOnlySpan<char> key)
			{
				foreach (var c in key)
				{
					if (c is '.' or '\\' or '[' or ']')
					{
						sb.Append('\\');
					}

					sb.Append(c);
				}
			}
		}

		public static void WriteTo(StringBuilder sb, Index index)
		{
			sb.Append('[').Append(index).Append(']');
		}

		public static void WriteTo(StringBuilder sb, int index)
		{
			if (index is >= 0 and <= 9)
			{
				sb.Append('[').Append('0' + index).Append(']');
			}
			else
			{
				sb.Append(CultureInfo.InvariantCulture, $"[{index}]");
			}
		}

		public sealed class Comparer : IEqualityComparer<JsonPath>, IComparer<JsonPath>
		{

			public static readonly Comparer Default = new();

			private Comparer() { }

			/// <inheritdoc />
			public bool Equals(JsonPath x, JsonPath y)
			{
				return x.Equals(y);
			}

			/// <inheritdoc />
			public int GetHashCode(JsonPath obj)
			{
				return obj.GetHashCode();
			}

			/// <inheritdoc />
			public int Compare(JsonPath x, JsonPath y)
			{
				return x.Value.Span.SequenceCompareTo(y.Value.Span);
			}

		}

	}

	[PublicAPI]
	[DebuggerDisplay("{ToString(),nq}")]
	public readonly struct JsonPathSegment : IEquatable<JsonPathSegment>
	{

		public readonly ReadOnlyMemory<char> Name;
		public readonly Index Index;

		public JsonPathSegment(string name)
		{
			this.Name = name.AsMemory();
			this.Index = default;
		}

		public JsonPathSegment(int index)
		{
			this.Name = default;
			this.Index = index;
		}

		public JsonPathSegment(Index index)
		{
			this.Name = default;
			this.Index = index;
		}

		public bool IsField => this.Name.Length != 0;

		public override string ToString() => JsonPath.Empty[this].ToString();

		public override bool Equals([NotNullWhen(true)] object? obj) => obj is JsonPathSegment segment && Equals(segment);

		public override int GetHashCode()
			=> HashCode.Combine(string.GetHashCode(this.Name.Span), this.Index.GetHashCode());

		public bool Equals(JsonPathSegment other)
			=> this.Name.Length != 0
			   ? this.Name.Span.SequenceEqual(other.Name.Span)
			   : (this.Index.Equals(other.Index) && other.Name.Length == 0);

	}

}
