#region BSD License
/* Copyright (c) 2013-2020, Doxense SAS
All rights reserved.

Redistribution and use in source and binary forms, with or without
modification, are permitted provided that the following conditions are met:
	* Redistributions of source code must retain the above copyright
	  notice, this list of conditions and the following disclaimer.
	* Redistributions in binary form must reproduce the above copyright
	  notice, this list of conditions and the following disclaimer in the
	  documentation and/or other materials provided with the distribution.
	* Neither the name of Doxense nor the
	  names of its contributors may be used to endorse or promote products
	  derived from this software without specific prior written permission.

THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND
ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
DISCLAIMED. IN NO EVENT SHALL <COPYRIGHT HOLDER> BE LIABLE FOR ANY
DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
(INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
(INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
 */
#endregion

namespace FoundationDB.Client
{
	using System;
	using System.Collections;
	using System.Collections.Generic;
	using System.Diagnostics;
	using System.Linq;
	using System.Runtime.CompilerServices;
	using System.Text;
	using Doxense.Diagnostics.Contracts;
	using JetBrains.Annotations;

	/// <summary>Represents a path in a Directory Layer</summary>
	[DebuggerDisplay("{ToString(),nq}")]
	public readonly struct FdbPath : IReadOnlyList<string>, IEquatable<FdbPath>
	{
		/// <summary>The "empty" path</summary>
		public static readonly FdbPath Empty = new FdbPath(default, absolute: false);

		/// <summary>The "root" path ("/").</summary>
		public static readonly FdbPath Root = new FdbPath(default, absolute: true);

		/// <summary>Segments of this path</summary>
		public readonly ReadOnlyMemory<string> Segments;

		public readonly bool IsAbsolute;

		//REVIEW: TODO: support the notion of relative vs absolute path?
		// - "/Foo/Bar" could be considered as an absolute path (starts with a '/')
		// - "Foo/Bar" could be bonsidered as a relative path (does not start with a '/')
		// Valid combinations:
		// - "/Foo/Bar" + "Baz" => "/Foo/Bar/Baz" (absolute)
		// - "Foo/Bar" + "Baz" => "Foo/Bar/Baz" (still relative)
		// - "/Foo/Bar" + "/Baz" => ERROR (dangerous, we don't want to introduce relative path vulnerabilities!)
		// - "Foo/Bar" + "/Baz" => ERROR (dangerous, we don't want to introduce relative path vulnerabilities!)

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		internal FdbPath(ReadOnlyMemory<string> path, bool absolute)
		{
			this.Segments = path;
			this.IsAbsolute = absolute;
		}

		/// <summary>Test if this is the empty directory</summary>
		/// <remarks>The <see cref="Root"/> path ("/") is NOT considered empty!</remarks>
		public bool IsEmpty
		{
			[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
			get => !this.IsAbsolute & (this.Segments.Length == 0);
		}

		/// <summary>Test if this is the root directory ("/")</summary>
		/// <remarks>The <see cref="Empty"/> path is NOT considered to be the root!</remarks>
		public bool IsRoot
		{
			[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
			get => this.IsAbsolute & this.Segments.Length == 0;
		}

		public int Count => this.Segments.Length;

		/// <summary>Return the segment at the specified index (0-based)</summary>
		public string this[int index] => this.Segments.Span[index];

#if USE_RANGE_API

		/// <summary>Return the segment at the specified index</summary>
		public string this[Index index]
		{
			get
			{
				var path = this.Segments.Span;
				return path[index.GetOffset(path.Length)];
			}
		}

		/// <summary>Return a sub-section of the current path</summary>
		public FdbPath this[Range range] => new FdbPath(this.Segments[range], this.IsAbsolute && range.GetOffsetAndLength(this.Count).Offset == 0);

#endif

		/// <summary>Return the name of the last segment of this path</summary>
		/// <example><see cref="MakeRelative(string[])"/>Combine("Foo", "Bar", "Baz").Name => "Baz"</example>
		public string Name
		{
			[Pure]
			get
			{
				var path = this.Segments.Span;
				return path.Length != 0 ? path[path.Length - 1] : string.Empty;
			}
		}

		/// <summary>Return the parent path of the current path, if it is not empty.</summary>
		/// <example>"/Foo/Bar/Baz".TryGetParent() => (true, "/Foo/Bar")</example>
		public bool TryGetParent(out FdbPath parent)
		{
			var path = this.Segments;
			if (path.Length == 0)
			{
				parent = default;
				return false;
			}
			parent = new FdbPath(path.Slice(0, path.Length - 1), this.IsAbsolute);
			return true;
		}

		/// <summary>Return the parent path of the current path</summary>
		/// <example>"/Foo/Bar/Baz".GetParent() => "/Foo/Bar"</example>
		/// <exception cref="InvalidOperationException">If this path is empty.</exception>
		[Pure]
		public FdbPath GetParent()
			=> TryGetParent(out var parent)
				? parent
				: throw new InvalidOperationException("The root path does not have a parent path.");

		/// <summary>Append a new segment to the curent path</summary>
		public FdbPath this[string segment] => Add(segment);

		/// <summary>Append one or more new segments to the curent path</summary>
		public FdbPath this[ReadOnlySpan<string> segments] => Add(segments);

		/// <summary>Append a relative path to the curent path</summary>
		public FdbPath this[FdbPath path] => Add(path);

		private static ReadOnlyMemory<string> AppendSegment(ReadOnlySpan<string> head, string segment)
		{
			Contract.NotNull(segment, nameof(segment));
			int n = head.Length;
			var tmp = new string[n + 1];
			head.CopyTo(tmp);
			tmp[n] = segment;
			return tmp;
		}

		private static ReadOnlyMemory<string> AppendSegments(ReadOnlySpan<string> head, string segment1, string segment2)
		{
			Contract.NotNull(segment1, nameof(segment1));
			Contract.NotNull(segment2, nameof(segment2));
			int n = head.Length;
			var tmp = new string[n + 2];
			head.CopyTo(tmp);
			tmp[n] = segment1;
			tmp[n + 1] = segment2;
			return tmp;
		}

		private static ReadOnlyMemory<string> AppendSegments(ReadOnlySpan<string> head, ReadOnlySpan<string> tail)
		{
			var tmp = new string[head.Length + tail.Length];
			head.CopyTo(tmp);
			tail.CopyTo(tmp.AsSpan(head.Length));
			return tmp;
		}

		private static ReadOnlyMemory<string> AppendSegments(ReadOnlySpan<string> head, IEnumerable<string> suffix)
		{
			var list = new List<string>(head.Length + ((suffix as ICollection<string>)?.Count ?? 4));
			foreach (var segment in head) list.Add(segment);
			foreach (var segment in suffix) list.Add(segment);
			return list.ToArray();
		}

		/// <summary>Append a new segment to the curent path</summary>
		[Pure]
		public FdbPath Add(string segment)
			=> new FdbPath(AppendSegment(this.Segments.Span, segment), this.IsAbsolute);

		/// <summary>Append a new segment to the curent path</summary>
		[Pure]
		public FdbPath Add(string segment1, string segment2)
			=> new FdbPath(AppendSegments(this.Segments.Span, segment1, segment2), this.IsAbsolute);

		/// <summary>Append a new segment to the curent path</summary>
		[Pure]
		public FdbPath Add(ReadOnlySpan<char> segment)
			=> new FdbPath(AppendSegment(this.Segments.Span, segment.ToString()), this.IsAbsolute);

		/// <summary>Add new segments to the current path</summary>
		[Pure]
		public FdbPath Add(ReadOnlySpan<string> segments)
			=> new FdbPath(AppendSegments(this.Segments.Span, segments), this.IsAbsolute);

		/// <summary>Add new segments to the current path</summary>
		[Pure]
		public FdbPath Add(params string[] segments)
			=> new FdbPath(AppendSegments(this.Segments.Span, segments.AsSpan()), this.IsAbsolute);

		/// <summary>Add new segments to the current path</summary>
		[Pure]
		public FdbPath Add(IEnumerable<string> segments)
			=> new FdbPath(AppendSegments(this.Segments.Span, segments), this.IsAbsolute);

		/// <summary>Add a path to the current path</summary>
		[Pure]
		public FdbPath Add(FdbPath path)
		{
			if (this.IsAbsolute)
			{
				// Can only add relative to an absolute
				if (path.IsAbsolute) throw new InvalidOperationException("Cannot add two absolute directory path together.");
				if (path.IsEmpty) return this;
			}
			else
			{
				// we still empty Empty + "/Foo/Bar"
				if (!this.IsEmpty && path.IsAbsolute) throw new InvalidOperationException("Cannot add an absolute directory path to a relative path.");
				if (this.IsEmpty) return path;
			}
			return new FdbPath(AppendSegments(this.Segments.Span, path.Segments.Span), this.IsAbsolute);
		}

		/// <summary>Return a suffix of the current path</summary>
		/// <param name="offset">Number of segments to skip</param>
		[Pure]
		public FdbPath Substring(int offset)
			=> new FdbPath(this.Segments.Slice(offset), absolute: this.IsAbsolute && offset == 0);

		/// <summary>Return a sub-section of the current path</summary>
		/// <param name="offset">Number of segments to skip</param>
		/// <param name="count">Number of segments to keep</param>
		[Pure]
		public FdbPath Substring(int offset, int count)
			=> new FdbPath(this.Segments.Slice(offset, count), absolute: this.IsAbsolute && offset == 0);

		/// <summary>Test if the current path is a child of another path</summary>
		/// <remarks>This differs from <see cref="StartsWith"/> in that a path is not a child of itself</remarks>
		[Pure]
		public bool IsChildOf(FdbPath prefix)
		{
			if (this.IsAbsolute != prefix.IsAbsolute) return false;
			return prefix.Count < this.Count && this.Segments.Span.StartsWith(prefix.Segments.Span);
		}

		/// <summary>Test if the current path is the same, or a child of another path</summary>
		/// <remarks>This differs from <see cref="IsChildOf"/> in that a path always starts with itself</remarks>
		[Pure]
		public bool StartsWith(FdbPath prefix)
		{
			if (this.IsAbsolute != prefix.IsAbsolute) return false;
			return this.Segments.Span.StartsWith(prefix.Segments.Span);
		}

		/// <summary>Test if the current path is a parent of another path</summary>
		/// <remarks>This differs from <see cref="EndsWith"/> in that a path is not a parent of itself</remarks>
		[Pure]
		public bool IsParentOf(FdbPath suffix)
		{
			if (this.IsAbsolute != suffix.IsAbsolute) return false;
			return suffix.Count > this.Count && this.Segments.Span.EndsWith(suffix.Segments.Span);
		}

		/// <summary>Test if the current path is the same or a parent of another path</summary>
		/// <remarks>This differs from <see cref="IsParentOf"/> in that a path always ends with itself</remarks>
		[Pure]
		public bool EndsWith(FdbPath suffix)
		{
			if (suffix.IsEmpty) return true; // everything ends with Empty
			if (suffix.IsAbsolute) return suffix.Equals(this);
			return this.Segments.Span.EndsWith(suffix.Segments.Span);
		}

		/// <summary>Return the relative part of this path inside its <paramref name="parent"/></summary>
		/// <param name="parent">Parent of this path</param>
		/// <param name="relativePath">If this path is equal to, or a child or <paramref name="parent"/>, receives the relative path; otherwise, <see cref="Empty"/>.</param>
		/// <returns>Returns <c>true</c> if path is equal to, or a child of <paramref name="parent"/>; otherwise, false.</returns>
		/// <remarks>If this path is equal to <paramref name="parent"/>, still returns true but <paramref name="relativePath"/> will be empty.</remarks>
		/// <example>"/Foo/Bar/Baz".TryGetRelativePath("/Foo") => (true, "Bar/Baz")</example>
		public bool TryGetRelativePath(FdbPath parent, out FdbPath relativePath)
		{
			if (!StartsWith(parent))
			{
				relativePath = default;
				return false;
			}
			relativePath = new FdbPath(this.Segments.Slice(parent.Segments.Length), absolute: false);
			Contract.Ensures(!relativePath.IsAbsolute);
			return true;
		}

		/// <summary>Return the relative part of this path inside its <paramref name="parent"/></summary>
		/// <param name="parent">Parent of this path</param>
		/// <returns>Returns the relative portion of the current path under its <paramref name="parent"/>. It this path is equal to <paramref name="parent"/>, returns <see cref="Empty"/>.</returns>
		/// <exception cref="ArgumentException">If this path is not equal to, or a child of <paramref name="parent"/>.</exception>
		/// <example>"/Foo/Bar/Baz".GetRelativePath("/Foo") => "Bar/Baz"</example>
		public FdbPath GetRelativePath(FdbPath parent)
			=> TryGetRelativePath(parent, out var relative)
				? relative
				: throw new ArgumentException("The current path is not equal to, or a child of, the specified parent path.", nameof(parent));

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public IEnumerator<string> GetEnumerator()
		{
			//note: ReadOnlyMemory<> does not implement IEnumerable<> and we can't foreach(...) on a span inside an enumerator...
			var segments = this.Segments;
			for (int i = 0; i < segments.Length; i++)
			{
				yield return segments.Span[i];
			}
		}

		public override string ToString()
		{
			return FormatPath(this.Segments.Span, this.IsAbsolute);
		}

		IEnumerator IEnumerable.GetEnumerator()
		{
			return GetEnumerator();
		}

		/// <summary>Add a segment to a parent path</summary>
		[Pure]
		public static FdbPath Combine(FdbPath path, string segment)
		{
			return path.Add(segment);
		}

		/// <summary>Add a pair of segments to a root path</summary>
		[Pure]
		public static FdbPath Combine(FdbPath path, string segment1, string segment2)
		{
			return path.Add(segment1, segment2);
		}

		/// <summary>Add one or more segments to a parent path</summary>
		[Pure]
		public static FdbPath Combine(FdbPath path, params string[] segments)
		{
			return path.Add(segments);
		}

		/// <summary>Add one or more segments to a parent path</summary>
		[Pure]
		public static FdbPath Combine(FdbPath path, ReadOnlySpan<string> segments)
		{
			return path.Add(segments);
		}

		/// <summary>Return a relative path composed of a single segment</summary>
		[Pure]
		public static FdbPath MakeRelative(string segment)
		{
			Contract.NotNull(segment, nameof(segment));
			return new FdbPath(new [] { segment }, absolute: false);
		}

		/// <summary>Return a relative path composed of the specified segments</summary>
		[Pure]
		public static FdbPath MakeRelative(params string[] segments)
		{
			Contract.NotNull(segments, nameof(segments));
			if (segments.Length == 0) return FdbPath.Empty;
			return new FdbPath(segments, absolute: false);
		}

		/// <summary>Return a relative path composed of the specified segments</summary>
		[Pure]
		public static FdbPath MakeRelative(ReadOnlySpan<string> segments)
		{
			// we have to copy the buffer!
			return new FdbPath(segments.ToArray(), absolute: false);
		}

		/// <summary>Return a relative path composed of the specified segments</summary>
		[Pure]
		public static FdbPath MakeRelative(ReadOnlyMemory<string> segments)
		{
			return new FdbPath(segments, absolute: false);
		}

		[Pure]
		public static FdbPath MakeRelative(IEnumerable<string> segments)
		{
			Contract.NotNull(segments, nameof(segments));
			return new FdbPath(segments.ToArray(), absolute: false);
		}

		/// <summary>Return a relative path composed of a single segment</summary>
		[Pure]
		public static FdbPath MakeAbsolute(string segment)
		{
			Contract.NotNull(segment, nameof(segment));
			return new FdbPath(new [] { segment }, absolute: true);
		}

		/// <summary>Return a relative path composed of the specified segments</summary>
		[Pure]
		public static FdbPath MakeAbsolute(params string[] segments)
		{
			Contract.NotNull(segments, nameof(segments));
			if (segments.Length == 0) return FdbPath.Empty;
			return new FdbPath(segments, absolute: true);
		}

		/// <summary>Return a relative path composed of the specified segments</summary>
		[Pure]
		public static FdbPath MakeAbsolute(ReadOnlySpan<string> segments)
		{
			// we have to copy the buffer!
			return new FdbPath(segments.ToArray(), absolute: true);
		}

		/// <summary>Return a relative path composed of the specified segments</summary>
		[Pure]
		public static FdbPath MakeAbsolute(ReadOnlyMemory<string> segments)
		{
			return new FdbPath(segments, absolute: true);
		}

		[Pure]
		public static FdbPath MakeAbsolute(IEnumerable<string> segments)
		{
			Contract.NotNull(segments, nameof(segments));
			return new FdbPath(segments.ToArray(), absolute: true);
		}

		[Pure]
		public static FdbPath Parse(string? path)
		{
			if (string.IsNullOrEmpty(path)) return Empty;
			if (path == "/") return FdbPath.Root;

			var segments = new List<string>();
			var sb = new StringBuilder();
			bool absolute = false;
			bool escaped = false;
			foreach (var c in path!)
			{
				if (escaped)
				{
					escaped = false;
					sb.Append(c);
					continue;
				}

				switch (c)
				{
					case '\\':
					{
						escaped = true;
						continue;
					}
					case '/':
					{
						if (sb.Length == 0 && segments.Count == 0)
						{ // first '/' means that it is an absolute path
							absolute = true;
							continue;
						}

						segments.Add(sb.ToString());
						sb.Clear();
						break;
					}
					default:
					{
						sb.Append(c);
						break;
					}
				}
			}

			if (sb.Length > 0)
			{
				segments.Add(sb.ToString());
			}

			return new FdbPath(segments.ToArray(), absolute);
		}

		//[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		//public static implicit operator FdbDirectoryPath(string segment)
		//{
		//	return Combine(segment);
		//}

		//[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		//public static implicit operator FdbDirectoryPath(string[] segments)
		//{
		//	return Combine(segments);
		//}

		[Pure]
		internal static string FormatPath(ReadOnlySpan<string> paths, bool absolute)
		{
			if (paths.Length == 0) return absolute ? "/" : string.Empty;

			var sb = new StringBuilder();
			foreach(var seg in paths)
			{
				if (absolute || sb.Length != 0) sb.Append('/');
				if (seg.Contains('\\') || seg.Contains('/') || seg.Contains('['))
				{
					sb.Append(seg.Replace("\\", "\\\\").Replace("/", "\\/")).Replace("[", "\\[");
				}
				else
				{
					sb.Append(seg);
				}
			}
			return sb.ToString();
		}

		public override int GetHashCode()
		{
			//this must be fast because we will use paths as keys in directories a lot (cache context)

			//note: we cannot use this.Segments.GetHashCode() because it will vary depending on the underlying backing store!
			// => we will use the head, middle point and tail of the segment to compute the hash

			var segments = this.Segments.Span;
			switch (segments.Length)
			{
				case 0: return this.IsAbsolute ? -1 : 0;
				case 1: return HashCodes.Combine(this.IsAbsolute ? -1 : 1, segments[0]?.GetHashCode() ?? -1);
				case 2: return HashCodes.Combine(this.IsAbsolute ? -2 : 2, segments[0]?.GetHashCode() ?? -1, segments[1]?.GetHashCode() ?? -1);
				default: return HashCodes.Combine(this.IsAbsolute ? -3 : 3, segments[0]?.GetHashCode() ?? -1, segments[segments.Length >> 1]?.GetHashCode() ?? -1, segments[segments.Length - 1]?.GetHashCode() ?? -1);
			}
		}

		public override bool Equals(object obj)
		{
			return obj is FdbPath other && this.Segments.Span.SequenceEqual(other.Segments.Span);
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool Equals(FdbPath other)
		{
			return this.IsAbsolute == other.IsAbsolute && this.Segments.Span.SequenceEqual(other.Segments.Span);
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator ==(FdbPath left, FdbPath right)
		{
			return left.IsAbsolute == right.IsAbsolute && left.Segments.Span.SequenceEqual(right.Segments.Span);
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator !=(FdbPath left, FdbPath right)
		{
			return left.IsAbsolute != right.IsAbsolute || !left.Segments.Span.SequenceEqual(right.Segments.Span);
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static FdbPath operator +(FdbPath head, FdbPath tail)
			=> head.Add(tail);

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static FdbPath operator +(FdbPath path, string segment)
			=> path.Add(segment);

	}
}
