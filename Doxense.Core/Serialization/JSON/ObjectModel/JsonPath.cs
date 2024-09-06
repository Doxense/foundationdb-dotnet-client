#region Copyright (c) 2023-2024 SnowBank SAS
//
// All rights are reserved. Reproduction or transmission in whole or in part, in
// any form or by any means, electronic, mechanical or otherwise, is prohibited
// without the prior written consent of the copyright owner.
//
#endregion

namespace Doxense.Serialization.Json
{
	using System.Buffers;
	using System.Collections;
	using System.Diagnostics;
	using System.Globalization;
	using System.Runtime.CompilerServices;
	using System.Text;

	/// <summary>Represents a path inside a JSON document to a nested child (ex: <c>"id"</c>, <c>"user.id"</c> <c>"tags[2].id"</c></summary>
	[DebuggerDisplay("{ToString(),nq}")]
	public readonly struct JsonPath : IEnumerable<(JsonPath Parent, ReadOnlyMemory<char> Key, Index Index, bool Last)>, IJsonSerializable, IJsonPackable, IJsonDeserializer<JsonPath>, IEquatable<JsonPath>, IEquatable<string>, ISpanFormattable
	{
		// the goal is to wrap a string with the full path, and expose each "segment" as a ReadOnlySpan<char>, in order to reduce allocations

		/// <summary>The empty path (root of the document)</summary>
		public static readonly JsonPath Empty = default;

		/// <summary>String literal</summary>
		public readonly ReadOnlyMemory<char> Value;

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public JsonPath(ReadOnlyMemory<char> path) => this.Value = path;

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public JsonPath(string? path) => this.Value = string.IsNullOrEmpty(path) ? default : path.AsMemory();

		/// <summary>Returns a JsonPath that wraps a <see cref="ReadOnlySpan{T}">ReadOnlySpan&lt;char&gt;</see> literal</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static JsonPath Create(ReadOnlyMemory<char> path) => path.Length == 0 ? default : new(path);

		/// <summary>Returns a JsonPath that wraps a <see cref="string">ReadOnlySpan&lt;char&gt;</see> literal</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static JsonPath Create(string? path) => path == null ? default : new(path.AsMemory());

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
		public static explicit operator JsonPath(string? path) => path == null ? default : new(path.AsMemory());

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public Tokenizer GetEnumerator() => new(this);

		IEnumerator<(JsonPath, ReadOnlyMemory<char>, Index, bool)> IEnumerable<(JsonPath Parent, ReadOnlyMemory<char> Key, Index Index, bool Last)>.GetEnumerator() => new Tokenizer(this);

		IEnumerator IEnumerable.GetEnumerator() => new Tokenizer(this);

		public List<string> GetParts()
		{
			List<string> res = [ ];
			foreach (var x in this)
			{
				if (x.Key.Length > 0)
				{
					res.Add(x.Key.ToString());
				}
				else
				{
					res.Add($"[{x.Index}]");
				}
			}
			return res;
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public override string ToString() => this.Value.GetStringOrCopy();

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public string ToString(string? format, IFormatProvider? formatProvider) => this.Value.GetStringOrCopy();

		public bool TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format, IFormatProvider? provider)
		{
			//TODO: what kind of formats should be allow?
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
		public override int GetHashCode() => this.Value.GetHashCode();

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
		public bool Equals(string? other) => this.Value.Span.SequenceEqual(other); // null|"" == Empty

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator ==(JsonPath left, JsonPath right) => left.Equals(right);

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator !=(JsonPath left, JsonPath right) => !left.Equals(right);

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator ==(JsonPath left, string? right) => left.Equals(right);

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator !=(JsonPath left, string? right) => !left.Equals(right);

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public ReadOnlySpan<char> AsSpan() => this.Value.Span;

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public ReadOnlyMemory<char> AsMemory() => this.Value;

		/// <summary>Tests if this the empty path (root of the document)</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool IsEmpty() => this.Value.Length == 0;

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

#if NET8_0_OR_GREATER

		private static readonly SearchValues<char> Needle = SearchValues.Create("\\.[]");

		/// <summary>Tests if a field name needs to be escaped</summary>
		/// <param name="name">Name of a field</param>
		/// <returns><see langword="true"/> if name contains at least one of '<c>\</c>', '<c>.</c>' or '<c>[</c>'</returns>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		private static bool RequiresEscaping(ReadOnlySpan<char> name)
		{
			return name.ContainsAny(Needle);
		}

#else

		/// <summary>Tests if a field name needs to be escaped</summary>
		/// <param name="name">Name of a field</param>
		/// <returns><see langword="true"/> if name contains at least one of '<c>\</c>', '<c>.</c>' or '<c>[</c>'</returns>
		private static bool RequiresEscaping(ReadOnlySpan<char> name)
		{
			ReadOnlySpan<char> mustEscapeCharacters = "\\.[]";
			return name.IndexOfAny(mustEscapeCharacters) >= 0;
		}

#endif

		private static string Unescape(ReadOnlySpan<char> literal)
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

		private static string Escape(ReadOnlySpan<char> name)
		{
			return name.Length switch
			{
				0 => string.Empty,
				<= 64 => EscapeSmallString(name),
				_ => EscapeLargeString(name)
			};

			static string EscapeSmallString(ReadOnlySpan<char> name)
			{
				// can take up to twice the size (if all characters must be escaped)
				Span<char> buf = stackalloc char[name.Length * 2];
				int p = 0;
				foreach (var c in name)
				{
					if (c is '.' or '\\' or '[' or ']')
					{
						buf[p++] = '\\';
					}

					buf[p++] = c;
				}

				return buf[..p].ToString();
			}

			static string EscapeLargeString(ReadOnlySpan<char> name)
			{
				var tmp = ArrayPool<char>.Shared.Rent(name.Length * 2);
				Span<char> buf = tmp.AsSpan();
				int p = 0;
				foreach (var c in name)
				{
					if (c is '.' or '\\' or '[' or ']')
					{
						buf[p++] = '\\';
					}

					buf[p++] = c;
				}

				var s = new string(tmp, 0, p);
				ArrayPool<char>.Shared.Return(tmp, clearArray: true);
				return s;
			}
		}

		private static string EscapeWithPrefix(ReadOnlySpan<char> prefix, ReadOnlySpan<char> name)
		{
			if (prefix.Length == 0)
			{
				return Escape(name);
			}

			return name.Length switch
			{
				0 => string.Concat(prefix, "."),
				<= 64 => EscapeSmallString(prefix, name),
				_ => EscapeLargeString(prefix, name)
			};

			static string EscapeSmallString(ReadOnlySpan<char> prefix, ReadOnlySpan<char> name)
			{
				// can take up to twice the size (if all characters must be escaped)
				Span<char> buf = stackalloc char[checked(prefix.Length + 1 + name.Length * 2)];
				prefix.CopyTo(buf);
				buf[prefix.Length] = '.';
				int p = prefix.Length + 1;
				foreach (var c in name)
				{
					if (c is '.' or '\\' or '[' or ']')
					{
						buf[p++] = '\\';
					}

					buf[p++] = c;
				}

				return buf[..p].ToString();
			}

			static string EscapeLargeString(ReadOnlySpan<char> prefix, ReadOnlySpan<char> name)
			{
				var tmp = ArrayPool<char>.Shared.Rent(name.Length * 2);
				Span<char> buf = tmp.AsSpan();
				prefix.CopyTo(buf);
				buf[prefix.Length] = '.';
				int p = prefix.Length + 1;
				foreach (var c in name)
				{
					if (c is '.' or '\\' or '[' or ']')
					{
						buf[p++] = '\\';
					}

					buf[p++] = c;
				}

				var s = new string(tmp, 0, p);
				ArrayPool<char>.Shared.Return(tmp, clearArray: true);
				return s;
			}
		}

		/// <summary>Appends an field to this path (ex: <c>JsonPath.Return("user")["id"]</c> => "user.id")</summary>
		public JsonPath this[string key]
		{
			get
			{
				Contract.NotNull(key);

				if (RequiresEscaping(key))
				{
					return new (EscapeWithPrefix(this.Value.Span, key));
				}

				int l = this.Value.Length;
				if (l == 0)
				{
					return new(key.AsMemory());
				}

				l = checked(l + 1 + key.Length);
				return new(string.Create(l, (Path: this.Value, Key: key), ((span, state) =>
				{
					state.Path.Span.CopyTo(span);
					span = span[state.Path.Length..];
					span[0] = '.';
					span = span[1..];
					Contract.Debug.Assert(span.Length == state.Key.Length);
					state.Key.CopyTo(span);
				})));
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

		/// <summary>Appends an field to this path (ex: <c>JsonPath.Return("user")["xxxidxxx".AsSpan(3, 2)]</c> => "user.id")</summary>
		public JsonPath this[ReadOnlySpan<char> key]
		{
			get
			{

				// we may need to encode the key if it contains any of '\', '.' or '['
				if (RequiresEscaping(key))
				{
					return new (EscapeWithPrefix(this.Value.Span, key));
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
			return string.Create(
				l,
				(Head: head, Tail: tail),
				(span, state) =>
				{
					state.Head.Span.CopyTo(span);
					span = span[state.Head.Length..];
					Contract.Debug.Assert(span.Length == state.Tail.Length);
					state.Tail.Span.CopyTo(span);
				}
			);
		}

		private static string ConcatWithField(ReadOnlyMemory<char> head, ReadOnlyMemory<char> tail)
		{
			int l = checked(head.Length + 1 + tail.Length);
			return string.Create(
				l,
				(Head: head, Tail: tail),
				(span, state) =>
				{
					state.Head.Span.CopyTo(span);
					span = span[state.Head.Length..];
					span[0] = '.';
					span = span[1..];
					Contract.Debug.Assert(span.Length == state.Tail.Length);
					state.Tail.Span.CopyTo(span);
				}
			);
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
		/// <remarks>A path is not is own child, so <c>path.IsChildOf(path)</c> will be <see langword="false"/>.</remarks>
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
		/// <remarks>A path is not is own child, so <c>path.IsChildOf(path)</c> will be <see langword="false"/>.</remarks>
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
		/// <remarks>A path is not is own child, so <c>path.IsChildOf(path)</c> will be <see langword="false"/>.</remarks>
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
		/// <remarks>A path is not is own child, so <c>path.IsChildOf(path)</c> will be <see langword="false"/>.</remarks>
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
		/// <remarks>Returns the emtpy path if the path is already empty</remarks>
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

		private static ReadOnlySpan<char> MaybeUnescape(ReadOnlySpan<char> literal)
		{
			return literal.Contains('\\') ? Unescape(literal) : literal;
		}

		/// <summary>Tests if the last segment is a key</summary>
		/// <example><code>
		/// "" => false
		/// [42] => false
		/// "foo" => true, "foo"
		/// "foo.bar" => true, "bar"
		/// "foo.bar[42]" => false
		/// "foo.bar[42].baz" => true, "baz"
		/// </code></example>
		[Pure]
		public bool TryGetKey(out ReadOnlySpan<char> key)
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
					key = MaybeUnescape(path);
					return true;
				}
				// the path ends with an indeer: "[42]" or "foo[42]"
				key = default;
				return false;
			}
			if (p <= q)
			{ // the last key is before the last indexer: "foo.bar[42]"
				key = default;
				return false;
			}
			// the last key is after the last indexer: "[42].foo" or "foo[42].bar"
			key = MaybeUnescape(path[(p + 1)..]);
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
		public ReadOnlySpan<char> GetKey() => TryGetKey(out var key) ? key : default;

		/// <summary>Tests if the last segment is an index</summary>
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
		public bool TryGetIndex(out Index index)
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

			var lit = path[(q + 1)..];
			p = GetIndexOf(lit, ']');
			if (p <= 0)
			{
				index = default;
				return false;
			}

			lit = lit[..p];
			bool fromEnd = false;
			if (lit[0] == '^')
			{
				lit = lit[1..];
				fromEnd = true;
			}

#if NET8_0_OR_GREATER
			if (!int.TryParse(lit, CultureInfo.InvariantCulture, out var result))
			{
				index = default;
				return false;
			}
#else
			if (!int.TryParse(lit, out var result))
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
		public Index? GetIndex() => TryGetIndex(out var index) ? index : null;

		[Pure]
		public JsonPath ParseNext(out ReadOnlyMemory<char> key, out Index index, out int consumed)
		{
			var path = this.Value.Span;
			if (path.Length == 0)
			{ // empty: "" => "", _, _
				key = default;
				index = default;
				consumed = 0;
				return default;
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
					key = this.Value;
					index = default;
					consumed = key.Length;
					return default;
				}

				// multiple fields access: "foo.bar.baz" => "bar.baz", "foo", _, 4
				key = this.Value[..p];
				index = default;
				consumed = p + 1;
				return new(this.Value[(p + 1)..]);
			}

			if (p < 0)
			{ // no sub-field access: either key[index] or just [index] (or maybe [index][index]...)

				if (q > 0)
				{ // key[index]: "foo[42]" => "[42]", "foo", _, 3
					key = this.Value[..q];
					index = default;
					consumed = q;
					return new(this.Value[q..]);
				}

				//either [x] or [x][y]...
				int r = GetIndexOf(path, ']');
				if (r < q) throw new FormatException("Invalid JSON Path: missing required ']' in indexer.");
				
				// [index]: "[42]" => "", _, 42, 4
				key = default;
				index = ParseIndex(path[1..r]);
				consumed = r + 1;
				return consumed < path.Length ? new(this.Value[consumed..]) : default;
			}

			if (q == 0)
			{ // [index].bar: "[42].bar" => "bar", "[42]", _, 4
				int r = GetIndexOf(path, ']');
				if (r < q) throw new FormatException("Invalid JSON Path: missing required ']' in indexer.");
				key = default;
				index = ParseIndex(path[1..r]);
				if (r + 1 == p)
				{ // [index].key
					consumed = r + 2; // skip the dot
				}
				else
				{ // [index][index].key
					consumed = r + 1;
				}
				return new(this.Value[consumed..]);
			}

			if (p > q)
			{ // key[index].key: "foo[42].bar" => "[42].bar", _, 42, 4
				key = this.Value[..q];
				index = default;
				consumed = q;
				return new(this.Value[consumed..]);
			}

			// key.key[index]: "foo.bar[42]" => "bar[42]", "foo", _, 4
			key = this.Value[..p]; // "foo"
			index = default;
			consumed = p + 1;
			return new(this.Value[(p + 1)..]); // "bar" (must skip the '.')
		}

		[Pure]
		private Index ParseIndex(ReadOnlySpan<char> literal)
		{
			if (literal.Length == 0) throw new FormatException("Invalid JSON Path: empty [] clause.");
			bool fromEnd = false;
			if (literal[0] == '^')
			{
				fromEnd = true;
				literal = literal[1..];
			}

#if NET8_0_OR_GREATER
			if (!int.TryParse(literal, CultureInfo.InvariantCulture, out var result))
			{
				throw new FormatException("Invalid JSON Path: invalid [..] clause.");
			}
#else
			if (!int.TryParse(literal, out var result))
			{
				throw new FormatException("Invalid JSON Path: invalid [..] clause.");
			}
#endif

			return new Index(result, fromEnd);
		}

		/// <summary>Returns a the sub-section of this path, beginning at a specified position and continuing to its end.</summary>
		/// <param name="start">Number of segments to skip</param>
		/// <returns>Slice of the path</returns>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public JsonPath Slice(int start) => new(this.Value.Slice(start));

		/// <summary>Returns a the sub-section of this path, starting at <paramref name="start" /> position for <paramref name="length" /> segments.</summary>
		/// <param name="start">Number of segments to skip</param>
		/// <param name="length">Number of segments to include</param>
		/// <returns>Slice of the path</returns>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public JsonPath Slice(int start, int length) => new(this.Value.Slice(start, length));

		/// <summary>Returns a the sub-section of this path, corresponding to the specified range.</summary>
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

		static JsonPath IJsonDeserializer<JsonPath>.JsonDeserialize(JsonValue value, ICrystalJsonTypeResolver? resolver)
		{
			if (value.IsNullOrMissing()) return default;
			if (value is not JsonString str) throw new JsonBindingException("JsonPath must be represented as a string");
			return new JsonPath(str.Value.AsMemory());
		}

		public struct Tokenizer : IEnumerator<(JsonPath Parent, ReadOnlyMemory<char> Key, Index Index, bool Last)>
		{

			private JsonPath Path;
			private JsonPath Tail;
			private int Offset;
			private int Consumed;
			public ReadOnlyMemory<char> Key;
			public Index Index;
			public int Depth;

			public Tokenizer(JsonPath path)
			{
				this.Path = path;
				this.Tail = path;
				this.Offset = 0;
				this.Consumed = 0;
				this.Key = default;
				this.Index = default;
				this.Depth = -1;
			}

			public void Reset()
			{
				this.Tail = this.Path;
				this.Offset = 0;
				this.Consumed = 0;
				this.Key = default;
				this.Index = default;
				this.Depth = -1;
			}

			public bool MoveNext()
			{
				var tail = this.Tail;
				if (tail.Value.Length == 0)
				{
					return false;
				}
				++this.Depth;
				var next = tail.ParseNext(out this.Key, out this.Index, out int consumed);

				if (this.Key.Length > 0 && RequiresEscaping(this.Key.Span))
				{ // we need to decode the key to remove any '\'
					//HACKHACK: OPTIMIZE: TODO: this allocates inside the tokenizer which is usually in the hot path of JsonValue.GetPath(...) !!
					// => we _could_ use a small temp buffer from a pool, but we would need to make SURE that nobody can capture the Key
					//    this is currently a ReadOnlyMemory<char> and would need to be changed into a ReadOnlySpan<char> ?
					this.Key = Unescape(this.Key.Span).AsMemory();
				}

				if (consumed == 0)
				{
					this.Consumed = 0;
					Contract.Debug.Assert(this.Offset == this.Path.Value.Length);
					return false;
				}
				this.Offset += this.Consumed;
				this.Tail = next;
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

	}

}
