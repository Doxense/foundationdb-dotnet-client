#region Copyright (c) 2023-2024 SnowBank SAS
//
// All rights are reserved. Reproduction or transmission in whole or in part, in
// any form or by any means, electronic, mechanical or otherwise, is prohibited
// without the prior written consent of the copyright owner.
//
#endregion

namespace Doxense.Serialization.Json
{
	using System;
	using System.Collections;
	using System.Collections.Generic;
	using System.Diagnostics;
	using System.Globalization;
	using System.Runtime.CompilerServices;
	using Doxense.Diagnostics.Contracts;
	using Pure = System.Diagnostics.Contracts.PureAttribute;

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

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static explicit operator JsonPath(string? path) => path == null ? default : new(path.AsMemory());

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public Tokenizer GetEnumerator() => new(this);

		IEnumerator<(JsonPath, ReadOnlyMemory<char>, Index, bool)> IEnumerable<(JsonPath Parent, ReadOnlyMemory<char> Key, Index Index, bool Last)>.GetEnumerator() => new Tokenizer(this);

		IEnumerator IEnumerable.GetEnumerator() => new Tokenizer(this);

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public override string ToString() => this.Value.ToString();

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public string ToString(string? format, IFormatProvider? formatProvider)
		{
			//TODO: what kind of formats should be allow?
			return this.Value.ToString();
		}

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

		public ReadOnlySpan<char> AsSpan() => this.Value.Span;

		public ReadOnlyMemory<char> AsMemory() => this.Value;

		/// <summary>Tests if this the empty path (root of the document)</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool IsEmpty() => this.Value.Length == 0;

		/// <summary>Appends an index to this path (ex: <c>JsonPath.Return("tags")[1]</c> => "tags[1]")</summary>
		public JsonPath this[int index] => new((this.Value.Span.ToString() + "[" + index.ToString(CultureInfo.InvariantCulture) +  "]").AsMemory());

		/// <summary>Appends an index to this path (ex: <c>JsonPath.Return("tags")[^1]</c> => "tags[^1]")</summary>
		public JsonPath this[Index index] => new(this.Value.Span.ToString() + "[" + index.ToString() + "]");

		/// <summary>Appends an field to this path (ex: <c>JsonPath.Return("user")["id"]</c> => "user.id")</summary>
		public JsonPath this[string key]
		{
			get
			{
				Contract.NotNull(key);
				int l = this.Value.Length;
				if (l == 0) return new(key.AsMemory());
				l = checked(l + 1 + key.Length);
				return new(string.Create(l, (Path: this.Value, Key: key), ((span, state) =>
				{
					// (this.Value.Span.ToString() + "." + key)
					state.Path.Span.CopyTo(span);
					span = span[state.Path.Length..];
					span[0] = '.';
					span = span[1..];
					Contract.Debug.Assert(span.Length == key.Length);
					key.CopyTo(span);
				})));
			}
		}

		/// <summary>Appends an field to this path (ex: <c>JsonPath.Return("user")["xxxidxxx".AsSpan(3, 2)]</c> => "user.id")</summary>
		public JsonPath this[ReadOnlySpan<char> key] => new(this.Value.Length == 0 ? key.ToString() : (this.Value.Span.ToString() + "." + key.ToString()));

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

			int p = path.LastIndexOf('.');
			int q = path.LastIndexOf('[');
			return p < 0
				? (q < 0 ? default : new(this.Value[..q]))
				: q < 0 ? new(this.Value[..p]) 
					: new(this.Value[..Math.Max(p, q)]);
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
			int p = path.LastIndexOf('.');
			int q = path.LastIndexOf('[');
			if (p < 0)
			{
				if (q < 0)
				{ // the path is a single key: "foo"
					key = path;
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
			key = path[(p + 1)..];
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

			int p = path.LastIndexOf('.');
			int q = path.LastIndexOf('[');

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
			p = lit.IndexOf(']');
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
			int p = path.IndexOf('.'); // if >=0, could be "foo.bar" or "foo[42].bar" or "[42].foo"
			int q = path.IndexOf('['); // if >=0, could be "[42]" or "foo[42]"

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
				int r = path.IndexOf(']');
				if (r < q) throw new FormatException("Invalid JSON Path: missing required ']' in indexer.");
				
				// [index]: "[42]" => "", _, 42, 4
				key = default;
				index = ParseIndex(path[1..r]);
				consumed = r + 1;
				return consumed < path.Length ? new(this.Value[consumed..]) : default;
			}

			if (q == 0)
			{ // [index].bar: "[42].bar" => "bar", "[42]", _, 4
				int r = path.IndexOf(']');
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

		/// <summary>Returns a path from the slice of the </summary>
		/// <param name="start"></param>
		/// <returns></returns>
		public JsonPath Slice(int start) => new(this.Value.Slice(start));

		public JsonPath Slice(int start, int length) => new(this.Value.Slice(start, length));

		/// <summary></summary>
		public JsonPath this[Range range] => new(this.Value[range]);

		void IJsonSerializable.JsonSerialize(CrystalJsonWriter writer) => writer.WriteValue(this.Value.Span);

		JsonValue IJsonPackable.JsonPack(CrystalJsonSettings settings, ICrystalJsonTypeResolver resolver) => JsonString.Return(this.Value.Span);

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

	}

}
