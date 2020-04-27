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
	public readonly struct FdbDirectoryPath : IReadOnlyList<string>, IEquatable<FdbDirectoryPath>
	{
		public static readonly FdbDirectoryPath Empty = new FdbDirectoryPath(default);

		/// <summary>Segments of this path</summary>
		public readonly ReadOnlyMemory<string> Segments;

		//REVIEW: TODO: support the notion of relative vs absolute path?
		// - "/Foo/Bar" could be considered as an absolute path (starts with a '/')
		// - "Foo/Bar" could be bonsidered as a relative path (does not start with a '/')
		// Valid combinations:
		// - "/Foo/Bar" + "Baz" => "/Foo/Bar/Baz" (absolute)
		// - "Foo/Bar" + "Baz" => "Foo/Bar/Baz" (still relative)
		// - "/Foo/Bar" + "/Baz" => ERROR (dangerous, we don't want to introduce relative path vulnerabilities!)
		// - "Foo/Bar" + "/Baz" => ERROR (dangerous, we don't want to introduce relative path vulnerabilities!)

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		internal FdbDirectoryPath(ReadOnlyMemory<string> path)
		{
			this.Segments = path;
		}

		public bool IsEmpty
		{
			[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
			get => this.Segments.Length == 0;
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
		public FdbDirectoryPath this[Range range] => new FdbDirectoryPath(this.Segments[range]);

#endif

		/// <summary>Return the name of the last segment of this path</summary>
		/// <example><see cref="Combine(string[])"/>Combine("Foo", "Bar", "Baz").Name => "Baz"</example>
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
		public bool TryGetParent(out FdbDirectoryPath parent)
		{
			var path = this.Segments;
			if (path.Length == 0)
			{
				parent = default;
				return false;
			}
			parent = new FdbDirectoryPath(path.Slice(0, path.Length - 1));
			return true;
		}

		/// <summary>Return the parent path of the current path</summary>
		/// <example>"/Foo/Bar/Baz".GetParent() => "/Foo/Bar"</example>
		/// <exception cref="InvalidOperationException">If this path is empty.</exception>
		[Pure]
		public FdbDirectoryPath GetParent()
			=> TryGetParent(out var parent)
				? parent
				: throw new InvalidOperationException("The root path does not have a parent path.");

		/// <summary>Append a new segment to the curent path</summary>
		public FdbDirectoryPath this[string segment] => Add(segment);

		/// <summary>Append one or more new segments to the curent path</summary>
		public FdbDirectoryPath this[ReadOnlySpan<string> segments] => Add(segments);

		/// <summary>Append a relative path to the curent path</summary>
		public FdbDirectoryPath this[FdbDirectoryPath path] => Add(path);

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
		public FdbDirectoryPath Add(string segment) => new FdbDirectoryPath(AppendSegment(this.Segments.Span, segment));

		/// <summary>Append a new segment to the curent path</summary>
		[Pure]
		public FdbDirectoryPath Add(string segment1, string segment2) => new FdbDirectoryPath(AppendSegments(this.Segments.Span, segment1, segment2));

		/// <summary>Append a new segment to the curent path</summary>
		[Pure]
		public FdbDirectoryPath Add(ReadOnlySpan<char> segment) => new FdbDirectoryPath(AppendSegment(this.Segments.Span, segment.ToString()));

		/// <summary>Add new segments to the current path</summary>
		[Pure]
		public FdbDirectoryPath Add(ReadOnlySpan<string> segments) => new FdbDirectoryPath(AppendSegments(this.Segments.Span, segments));

		/// <summary>Add new segments to the current path</summary>
		[Pure]
		public FdbDirectoryPath Add(params string[] segments) => new FdbDirectoryPath(AppendSegments(this.Segments.Span, segments.AsSpan()));

		/// <summary>Add new segments to the current path</summary>
		[Pure]
		public FdbDirectoryPath Add(IEnumerable<string> segments) => new FdbDirectoryPath(AppendSegments(this.Segments.Span, segments));

		/// <summary>Add a path to the current path</summary>
		[Pure]
		public FdbDirectoryPath Add(FdbDirectoryPath path) => new FdbDirectoryPath(AppendSegments(this.Segments.Span, path.Segments.Span));

		/// <summary>Return a suffix of the current path</summary>
		/// <param name="offset">Number of segments to skip</param>
		[Pure]
		public FdbDirectoryPath Substring(int offset) => new FdbDirectoryPath(this.Segments.Slice(offset));

		/// <summary>Return a sub-section of the current path</summary>
		/// <param name="offset">Number of segments to skip</param>
		/// <param name="count">Number of segments to keep</param>
		[Pure]
		public FdbDirectoryPath Substring(int offset, int count) => new FdbDirectoryPath(this.Segments.Slice(offset, count));

		/// <summary>Test if the current path is a child of another path</summary>
		/// <remarks>This differs from <see cref="StartsWith"/> in that a path is not a child of itself</remarks>
		[Pure]
		public bool IsChildOf(FdbDirectoryPath prefix)
		{
			return prefix.Count < this.Count && this.Segments.Span.StartsWith(prefix.Segments.Span);
		}

		/// <summary>Test if the current path is the same, or a child of another path</summary>
		/// <remarks>This differs from <see cref="IsChildOf"/> in that a path always starts with itself</remarks>
		[Pure]
		public bool StartsWith(FdbDirectoryPath prefix)
		{
			return this.Segments.Span.StartsWith(prefix.Segments.Span);
		}

		/// <summary>Test if the current path is a parent of another path</summary>
		/// <remarks>This differs from <see cref="EndsWith"/> in that a path is not a parent of itself</remarks>
		[Pure]
		public bool IsParentOf(FdbDirectoryPath suffix)
		{
			return suffix.Count > this.Count && this.Segments.Span.EndsWith(suffix.Segments.Span);
		}

		/// <summary>Test if the current path is the same or a parent of another path</summary>
		/// <remarks>This differs from <see cref="IsParentOf"/> in that a path always ends with itself</remarks>
		[Pure]
		public bool EndsWith(FdbDirectoryPath suffix)
		{
			return this.Segments.Span.EndsWith(suffix.Segments.Span);
		}

		/// <summary>Return the relative part of this path inside its <paramref name="parent"/></summary>
		/// <param name="parent">Parent of this path</param>
		/// <param name="relativePath">If this path is equal to, or a child or <paramref name="parent"/>, receives the relative path; otherwise, <see cref="Empty"/>.</param>
		/// <returns>Returns <c>true</c> if path is equal to, or a child of <paramref name="parent"/>; otherwise, false.</returns>
		/// <remarks>If this path is equal to <paramref name="parent"/>, still returns true but <paramref name="relativePath"/> will be empty.</remarks>
		/// <example>"/Foo/Bar/Baz".TryGetRelativePath("/Foo") => (true, "Bar/Baz")</example>
		public bool TryGetRelativePath(FdbDirectoryPath parent, out FdbDirectoryPath relativePath)
		{
			if (!StartsWith(parent))
			{
				relativePath = default;
				return false;
			}
			relativePath = new FdbDirectoryPath(this.Segments.Slice(parent.Segments.Length));
			return true;
		}

		/// <summary>Return the relative part of this path inside its <paramref name="parent"/></summary>
		/// <param name="parent">Parent of this path</param>
		/// <returns>Returns the relative portion of the current path under its <paramref name="parent"/>. It this path is equal to <paramref name="parent"/>, returns <see cref="Empty"/>.</returns>
		/// <exception cref="ArgumentException">If this path is not equal to, or a child of <paramref name="parent"/>.</exception>
		/// <example>"/Foo/Bar/Baz".GetRelativePath("/Foo") => "Bar/Baz"</example>
		public FdbDirectoryPath GetRelativePath(FdbDirectoryPath parent)
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
			return FormatPath(this.Segments.Span);
		}

		IEnumerator IEnumerable.GetEnumerator()
		{
			return GetEnumerator();
		}

		private static void EnsureCompatibleType<T>()
		{
			// test against easy mistakes
			if (typeof(T) == typeof(FdbDirectoryPath)) throw new InvalidOperationException($"You must call {nameof(Combine)}() to append paths!");
		}

		[Pure]
		public static FdbDirectoryPath Combine(string segment)
		{
			Contract.NotNull(segment, nameof(segment));
			return new FdbDirectoryPath(new [] { segment });
		}

		[Pure]
		public static FdbDirectoryPath Combine(params string[] segments)
		{
			Contract.NotNull(segments, nameof(segments));
			return new FdbDirectoryPath(segments);
		}

		[Pure]
		public static FdbDirectoryPath Combine(ReadOnlySpan<string> segments)
		{
			// we have to copy the buffer!
			return new FdbDirectoryPath(segments.ToArray());
		}


		[Pure]
		public static FdbDirectoryPath Combine(ReadOnlyMemory<string> segments)
		{
			return new FdbDirectoryPath(segments);
		}

		[Pure]
		public static FdbDirectoryPath Combine(IEnumerable<string> segments)
		{
			Contract.NotNull(segments, nameof(segments));
			return new FdbDirectoryPath(segments.ToArray());
		}

		[Pure]
		public static FdbDirectoryPath Parse(string? path)
		{
			if (string.IsNullOrEmpty(path)) return Empty;

			var segments = new List<string>();
			var sb = new StringBuilder();
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
						{ // ignore the first '/'
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

			return new FdbDirectoryPath(segments.ToArray());
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static implicit operator FdbDirectoryPath(string segment)
		{
			return Combine(segment);
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static implicit operator FdbDirectoryPath(string[] segments)
		{
			return Combine(segments);
		}

		[Pure]
		internal static string FormatPath(ReadOnlySpan<string> paths)
		{
			var sb = new StringBuilder();
			foreach(var seg in paths)
			{
				if (sb.Length != 0) sb.Append('/');
				if (seg.Contains('\\') || seg.Contains('/'))
				{
					sb.Append(seg.Replace("\\", "\\\\").Replace("/", "\\/"));
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
				case 0: return 0;
				case 1: return HashCodes.Combine(1, segments[0]?.GetHashCode() ?? -1);
				case 2: return HashCodes.Combine(2, segments[0]?.GetHashCode() ?? -1, segments[1]?.GetHashCode() ?? -1);
				default: return HashCodes.Combine(3, segments[0]?.GetHashCode() ?? -1, segments[segments.Length >> 1]?.GetHashCode() ?? -1, segments[segments.Length - 1]?.GetHashCode() ?? -1);
			}
		}

		public override bool Equals(object obj)
		{
			return obj is FdbDirectoryPath other && this.Segments.Span.SequenceEqual(other.Segments.Span);
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool Equals(FdbDirectoryPath other)
		{
			return this.Segments.Span.SequenceEqual(other.Segments.Span);
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator ==(FdbDirectoryPath left, FdbDirectoryPath right)
		{
			return left.Segments.Span.SequenceEqual(right.Segments.Span);
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator !=(FdbDirectoryPath left, FdbDirectoryPath right)
		{
			return !left.Segments.Span.SequenceEqual(right.Segments.Span);
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static FdbDirectoryPath operator +(FdbDirectoryPath head, FdbDirectoryPath tail)
			=> head.Add(tail);

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static FdbDirectoryPath operator +(FdbDirectoryPath path, string segment)
			=> path.Add(segment);

	}
}
