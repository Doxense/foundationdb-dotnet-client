﻿#region BSD License
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
	using System.Buffers;
	using System.Collections;
	using System.Collections.Generic;
	using System.Diagnostics;
	using System.Linq;
	using System.Runtime.CompilerServices;
	using System.Runtime.InteropServices;
	using System.Text;
	using Doxense.Diagnostics.Contracts;
	using JetBrains.Annotations;

	/// <summary>Represents a path in a Directory Layer</summary>
	[DebuggerDisplay("{ToString(),nq}")]
	public readonly struct FdbPath : IReadOnlyList<FdbPathSegment>, IEquatable<FdbPath>
	{
		/// <summary>The "empty" path</summary>
		/// <remarks>This path is relative</remarks>
		public static readonly FdbPath Empty = new FdbPath(default, absolute: false);

		/// <summary>The "root" path ("/").</summary>
		/// <remarks>This path is absolute</remarks>
		public static readonly FdbPath Root = new FdbPath(default, absolute: true);

		/// <summary>Segments of this path</summary>
		public readonly ReadOnlyMemory<FdbPathSegment> Segments;

		/// <summary>If <c>true</c>, this is an absolute path (ex: "/Foo/Bar"); otherwise, this a relative path ("Foo/Bar")</summary>
		public readonly bool IsAbsolute;

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		internal FdbPath(ReadOnlyMemory<FdbPathSegment> path, bool absolute)
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

		/// <summary>Number of segments in the path</summary>
		public int Count => this.Segments.Length;

		/// <summary>Return the segment at the specified index (0-based)</summary>
		public FdbPathSegment this[int index] => this.Segments.Span[index];

#if USE_RANGE_API

		/// <summary>Return the segment at the specified index</summary>
		public FdbPathSegment this[Index index]
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
		/// <example><see cref="Relative(string[])"/>Combine("Foo", "Bar", "Baz").Name => "Baz"</example>
		/// <remarks>The name of the <see cref="Empty"/> or <see cref="Root"/> paths is, by convention, the empty string.</remarks>
		public string Name
		{
			[Pure]
			get
			{
				var path = this.Segments.Span;
				return path.Length != 0 ? path[path.Length - 1].Name : string.Empty;
			}
		}

		/// <summary>Return the Layer Id of the last segment of this path</summary>
		/// <example><see cref="Relative(string[])"/>Combine("Foo", "Bar", "Baz").Name => "Baz"</example>
		/// <remarks>Be convention, the layer id of the <see cref="Empty"/> path is the <c>empty</c> string, and the layer id of the <see cref="Root"/> path is <c>"partition"</c></remarks>
		public string LayerId
		{
			[Pure]
			get
			{
				var path = this.Segments.Span;
				return path.Length != 0 ? path[path.Length - 1].LayerId
				       : this.IsAbsolute ? FdbDirectoryPartition.LayerId
				       : string.Empty;
			}
		}

		public FdbPath WithLayer(string? layerId)
		{
			layerId ??= string.Empty;

			var path = this.Segments.Span;
			if (path.Length == 0) throw new InvalidOperationException("Cannot change the layer of the empty path.");
			var tail = path[path.Length - 1];
			if (tail.LayerId == layerId) return this;

			var tmp = this.Segments.ToArray();
			tmp[tmp.Length - 1] = new FdbPathSegment(tail.Name, layerId);
			return new FdbPath(tmp, this.IsAbsolute);
		}

		/// <summary>Return the equivalent relative path, using the same path segments.</summary>
		/// <returns>The relative version of this path</returns>
		/// <example>"/Foo/Bar".AsRelative() == "Foo/Bar"; "Foo/Bar".AsRelative() == "Foo/Bar"</example>
		public FdbPath AsRelative() => new FdbPath(this.Segments, absolute: false);

		/// <summary>Return the equivalent absolute path, using the same path segments.</summary>
		/// <returns>The absolute version of this path</returns>
		/// <example>"Foo/Bar".AsAbsolute() == "/Foo/Bar"; "/Foo/Bar".AsAbsolute() == "/Foo/Bar"</example>
		/// <remarks>This is the equivalent of adding this path to the <see cref="Root"/> path</remarks>
		public FdbPath AsAbsolute() => new FdbPath(this.Segments, absolute: true);

		/// <summary>Return the relative path that, if added to <paramref name="parent"/>, would be equal to the current path.</summary>
		/// <param name="parent">Parent path. Must be of the same type (absolute/relative) as the current path.</param>
		/// <param name="relative">If the method returns <c>true</c>, receives the relative path from <paramref name="parent"/> to the current path.</param>
		/// <returns>Returns <c>true</c> if <paramref name="parent"/> is an ancestor or equal to the current path; otherwise, returns <c>false</c>.</returns>
		public bool TryGetRelative(FdbPath parent, out FdbPath relative)
		{
			if (this.IsAbsolute)
			{
				if (!parent.IsAbsolute)
				{ // We cannot compare an absolute path with a relative parent path
#if DEBUG
					if (System.Diagnostics.Debugger.IsAttached) System.Diagnostics.Debugger.Break();
#endif
					throw new ArgumentException("Parent path cannot be relative.", nameof(parent));
				}
			}
			else
			{
				if (parent.IsAbsolute)
				{ // We cannot compare a relative path with an absolute parent path
#if DEBUG
					if (System.Diagnostics.Debugger.IsAttached) System.Diagnostics.Debugger.Break();
#endif
					throw new ArgumentException("Parent path cannot be absolute.", nameof(parent));
				}
			}

			if (!StartsWith(parent))
			{
				relative = default;
				return false;
			}

			if (parent.Count == this.Count)
			{ // both paths are the same!
				relative = FdbPath.Empty;
				return true;
			}

			relative = new FdbPath(this.Segments.Slice(parent.Count), absolute: false);
			return true;
		}

		/// <summary>Get the parent path of the current path, if it is not empty.</summary>
		/// <param name="parent">Receive the path of the parent, if there is one.</param>
		/// <returns>If <c>true</c>, <paramref name="parent"/> contains the parent path. If <c>false</c>, the current path was <see cref="Empty"/> or the <see cref="Root"/>, and does not have a parent.</returns>>
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
		/// <param name="name">Name of the segment</param>
		/// <remarks>The new segment will not have a layer id.</remarks>
		public FdbPath this[string name] => Add(FdbPathSegment.Create(name));

		/// <summary>Append a new segment - composed of a name and layer id - to the curent path</summary>
		/// <param name="name">Name of the segment</param>
		/// <param name="layerId">Layer Id of the segment</param>
		public FdbPath this[string name, string layerId] => Add(FdbPathSegment.Create(name, layerId));

		/// <summary>Append a new segment to the curent path</summary>
		public FdbPath this[FdbPathSegment segment] => Add(segment);

		/// <summary>Append a relative path to the curent path</summary>
		public FdbPath this[FdbPath path] => Add(path);

		private static ReadOnlyMemory<FdbPathSegment> AppendSegment(ReadOnlySpan<FdbPathSegment> head, FdbPathSegment segment)
		{
			if (segment.IsEmpty) throw new ArgumentException("Segment cannot be empty", nameof(segment));
			int n = head.Length;
			var tmp = new FdbPathSegment[n + 1];
			head.CopyTo(tmp);
			tmp[n] = segment;
			return tmp;
		}

		private static ReadOnlyMemory<FdbPathSegment> AppendSegments(ReadOnlySpan<FdbPathSegment> head, FdbPathSegment segment1, FdbPathSegment segment2)
		{
			if (segment1.IsEmpty) throw new ArgumentException("Segment cannot be empty", nameof(segment1));
			if (segment2.IsEmpty) throw new ArgumentException("Segment cannot be empty", nameof(segment2));
			int n = head.Length;
			var tmp = new FdbPathSegment[n + 2];
			head.CopyTo(tmp);
			tmp[n] = segment1;
			tmp[n + 1] = segment2;
			return tmp;
		}

		private static ReadOnlyMemory<FdbPathSegment> AppendSegments(ReadOnlySpan<FdbPathSegment> head, ReadOnlySpan<FdbPathSegment> tail)
		{
			var tmp = new FdbPathSegment[head.Length + tail.Length];
			head.CopyTo(tmp);
			tail.CopyTo(tmp.AsSpan(head.Length));
			return tmp;
		}

		private static ReadOnlyMemory<FdbPathSegment> AppendSegments(ReadOnlySpan<FdbPathSegment> head, IEnumerable<FdbPathSegment> suffix)
		{
			var list = new List<FdbPathSegment>(head.Length + ((suffix as ICollection<FdbPathSegment>)?.Count ?? 4));
			foreach (var segment in head) list.Add(segment);
			foreach (var segment in suffix) list.Add(segment);
			return list.ToArray();
		}

		/// <summary>Append a new segment to the curent path</summary>
		/// <param name="name">Name of the segment</param>
		/// <remarks>This segment will not have any layer id defined.</remarks>
		[Pure]
		public FdbPath Add(string name)
			=> new FdbPath(AppendSegment(this.Segments.Span, FdbPathSegment.Create(name)), this.IsAbsolute);

		/// <summary>Append a new segment to the curent path</summary>
		/// <param name="name">Name of the segment</param>
		/// <param name="layerId">Layer Id of the segment</param>
		[Pure]
		public FdbPath Add(string name, string layerId)
			=> new FdbPath(AppendSegment(this.Segments.Span, FdbPathSegment.Create(name, layerId)), this.IsAbsolute);

		/// <summary>Append a new segment to the curent path</summary>
		[Pure]
		public FdbPath Add(FdbPathSegment segment)
			=> new FdbPath(AppendSegment(this.Segments.Span, segment), this.IsAbsolute);

		/// <summary>Add new segments to the current path</summary>
		[Pure]
		public FdbPath Add(ReadOnlySpan<FdbPathSegment> segments)
			=> segments.Length != 0 ? new FdbPath(AppendSegments(this.Segments.Span, segments), this.IsAbsolute) : this;

		/// <summary>Add new segments to the current path</summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public FdbPath Add(params FdbPathSegment[] segments)
			=> Add(segments.AsSpan());

		/// <summary>Add new segments to the current path</summary>
		[Pure]
		public FdbPath Add(IEnumerable<FdbPathSegment> segments)
			=> segments is FdbPathSegment[] arr ? Add(arr.AsSpan()) : new FdbPath(AppendSegments(this.Segments.Span, segments), this.IsAbsolute);

		/// <summary>Add a path to the current path</summary>
		[Pure]
		public FdbPath Add(FdbPath path)
		{
			if (this.IsAbsolute)
			{
				// Can only add relative to an absolute
				if (path.IsAbsolute)
				{
#if DEBUG
					if (System.Diagnostics.Debugger.IsAttached) System.Diagnostics.Debugger.Break();
#endif
					throw new InvalidOperationException("Cannot add two absolute directory path together.");
				}
				if (path.IsEmpty) return this;
			}
			else
			{
				// we still empty Empty + "/Foo/Bar"
				if (!this.IsEmpty && path.IsAbsolute)
				{
#if DEBUG
					if (System.Diagnostics.Debugger.IsAttached) System.Diagnostics.Debugger.Break();
#endif
					throw new InvalidOperationException("Cannot add an absolute directory path to a relative path.");
				}
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

		/// <summary>Return a serialized string that represents this path.</summary>
		/// <returns>All the path segments joined with a '/'. If the path is absolute, starts with a leading '/'</returns>
		/// <remarks>Any string produced by this method can be passed back to <see cref="Parse(string)"/> to get back the original path.</remarks>
		public override string ToString()
			=> FormatPath(this.Segments.Span, this.IsAbsolute, namesOnly: false);

		/// <summary>Encode a path into a string representation</summary>
		/// <param name="path">Path to encode</param>
		/// <param name="namesOnly">If <c>true</c>, ommit the layer ids in the resulting string.</param>
		/// <returns>String representation of the path (with or without the layer id)</returns>
		/// <remarks>If <paramref name="namesOnly"/> is <c>true</c>, the result will not <see cref="Parse(string)">round-trip</see> into the original path (layer ids will be lost).</remarks>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static string Encode(FdbPath path, bool namesOnly = false)
			=> FormatPath(path.Segments.Span, path.IsAbsolute, namesOnly);

		[Pure]
		internal static string FormatPath(ReadOnlySpan<FdbPathSegment> paths, bool absolute, bool namesOnly)
		{
			if (paths.Length == 0) return absolute ? "/" : string.Empty;

			//TODO: maybe use a Span<char> buffer instead of a string builder?
			var sb = new StringBuilder();
			if (namesOnly)
			{
				foreach (var seg in paths)
				{
					if (absolute || sb.Length != 0) sb.Append('/');
					FdbPathSegment.AppendTo(sb, seg.Name);
				}
			}
			else
			{
				foreach (var seg in paths)
				{
					if (absolute || sb.Length != 0) sb.Append('/');
					FdbPathSegment.AppendTo(sb, seg.Name, seg.LayerId);
				}
			}

			return sb.ToString();
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public IEnumerator<FdbPathSegment> GetEnumerator()
		{
			return MemoryMarshal.ToEnumerable(this.Segments).GetEnumerator();
		}

		IEnumerator IEnumerable.GetEnumerator()
		{
			return GetEnumerator();
		}

		#region Factory Methods...

		/// <summary>Add a segment to a parent path</summary>
		/// <param name="path">Parent path</param>
		/// <param name="segment">Encoded path segment, composed of just a name (<c>"Foo"</c>), with an optional layer id (<c>"Foo[SomeLayer]"</c>)</param>
		[Pure]
		public static FdbPath Combine(FdbPath path, FdbPathSegment segment)
		{
			return path.Add(segment);
		}

		/// <summary>Add a pair of segments to a root path</summary>
		[Pure]
		public static FdbPath Combine(FdbPath path, FdbPathSegment segment1, FdbPathSegment segment2)
		{
			return path.Add(segment1, segment2);
		}

		/// <summary>Add one or more segments to a parent path</summary>
		[Pure]
		public static FdbPath Combine(FdbPath path, params FdbPathSegment[] segments)
		{
			Contract.NotNull(segments, nameof(segments));
			if (segments.Length == 0) return path;

			return path.Add(segments);
		}

		/// <summary>Add one or more segments to a parent path</summary>
		[Pure]
		public static FdbPath Combine(FdbPath path, ReadOnlySpan<FdbPathSegment> segments)
		{
			if (segments.Length == 0) return path;

			return path.Add(segments);
		}

		/// <summary>Return a relative path composed of a single segment</summary>
		/// <param name="segment">Encoded path segment, composed of just a name (<c>"Foo"</c>), with an optional layer id (<c>"Foo[SomeLayer]"</c>)</param>
		[Pure]
		public static FdbPath Relative(string segment)
		{
			Contract.NotNull(segment, nameof(segment));
			return new FdbPath(new [] { FdbPathSegment.Parse(segment) }, absolute: false);
		}

		/// <summary>Return a relative path composed of a single segment</summary>
		[Pure]
		public static FdbPath Relative(FdbPathSegment segment)
		{
			if (segment.IsEmpty) throw new ArgumentException("Segment cannot be empty", nameof(segment));
			return new FdbPath(new [] { segment }, absolute: false);
		}

		/// <summary>Return a relative path composed of the specified segments</summary>
		/// <param name="segments">Array of encoded path segments, composed of just a name (<c>"Foo"</c>), with an optional layer id (<c>"Foo[SomeLayer]"</c>)</param>
		[Pure]
		public static FdbPath Relative(params string[] segments)
		{
			Contract.NotNull(segments, nameof(segments));
			if (segments.Length == 0) return FdbPath.Empty;
			return new FdbPath(FdbPathSegment.Parse(segments.AsSpan()), absolute: false);
		}

		/// <summary>Return a relative path composed of the specified segments</summary>
		[Pure]
		public static FdbPath Relative(params FdbPathSegment[] segments)
		{
			Contract.NotNull(segments, nameof(segments));
			if (segments.Length == 0) return FdbPath.Empty;
			return new FdbPath(segments, absolute: false);
		}

		/// <summary>Return a relative path composed of the specified segments</summary>
		[Pure]
		public static FdbPath Relative(ReadOnlySpan<FdbPathSegment> segments)
		{
			// we have to copy the buffer!
			return new FdbPath(segments.ToArray(), absolute: false);
		}

		/// <summary>Return a relative path composed of the specified segments</summary>
		[Pure]
		public static FdbPath Relative(ReadOnlyMemory<FdbPathSegment> segments)
		{
			return new FdbPath(segments, absolute: false);
		}

		/// <summary>Return a relative path composed of the specified segments</summary>
		/// <param name="segments">Sequence of path segment, composed of just a name (<c>"Foo"</c>), with an optional layer id (<c>"Foo[SomeLayer]"</c>)</param>
		[Pure]
		public static FdbPath Relative(IEnumerable<FdbPathSegment> segments)
		{
			Contract.NotNull(segments, nameof(segments));
			return new FdbPath(segments.ToArray(), absolute: false);
		}

		/// <summary>Return a relative path composed of a single segment</summary>
		[Pure]
		public static FdbPath Absolute(string segment)
		{
			Contract.NotNull(segment, nameof(segment));
			return new FdbPath(new [] { FdbPathSegment.Parse(segment) }, absolute: true);
		}

		/// <summary>Return a relative path composed of a single segment</summary>
		[Pure]
		public static FdbPath Absolute(FdbPathSegment segment)
		{
			if (segment.IsEmpty) throw new ArgumentException("Segment cannot be empty.", nameof(segment));
			return new FdbPath(new [] { segment }, absolute: true);
		}

		/// <summary>Return a relative path composed of the specified segments</summary>
		[Pure]
		public static FdbPath Absolute(params string[] segments)
		{
			Contract.NotNull(segments, nameof(segments));
			if (segments.Length == 0) return FdbPath.Root;
			return new FdbPath(FdbPathSegment.Parse(segments.AsSpan()), absolute: true);
		}

		/// <summary>Return a relative path composed of the specified segments</summary>
		[Pure]
		public static FdbPath Absolute(params FdbPathSegment[] segments)
		{
			Contract.NotNull(segments, nameof(segments));
			if (segments.Length == 0) return FdbPath.Root;
			return new FdbPath(segments, absolute: true);
		}

		/// <summary>Return a relative path composed of the specified segments</summary>
		[Pure]
		public static FdbPath Absolute(ReadOnlySpan<FdbPathSegment> segments)
		{
			// we have to copy the buffer!
			return new FdbPath(segments.ToArray(), absolute: true);
		}

		/// <summary>Return a relative path composed of the specified segments</summary>
		[Pure]
		public static FdbPath Absolute(ReadOnlyMemory<FdbPathSegment> segments)
		{
			return new FdbPath(segments, absolute: true);
		}

		[Pure]
		public static FdbPath Absolute(IEnumerable<FdbPathSegment> segments)
		{
			Contract.NotNull(segments, nameof(segments));
			return new FdbPath(segments.ToArray(), absolute: true);
		}

		/// <summary>Parse a string representing a path into the corresponding <see cref="FdbPath"/> instance.</summary>
		/// <param name="path">Path (either absolute or relative).</param>
		/// <returns>Corresponding path.</returns>
		/// <remarks>This method can take the out of <see cref="ToString()"/> and return the original path.</remarks>
		[Pure]
		public static FdbPath Parse(string? path)
			=> !string.IsNullOrWhiteSpace(path) ? Parse(path.AsSpan()) : Empty;

		/// <summary>Parse a string representing a path into the corresponding <see cref="FdbPath"/> instance.</summary>
		/// <param name="path">Path (either absolute or relative).</param>
		/// <returns>Corresponding path.</returns>
		/// <remarks>This method can take the out of <see cref="ToString()"/> and return the original path.</remarks>
		[Pure]
		public static FdbPath Parse(ReadOnlySpan<char> path)
		{
			if (path.Length == 0) return Empty;
			if (path.Length == 1 && path[0] == '/') return FdbPath.Root;

			if (path.Length < 1024)
			{ // for small path, use the stack as buffer
				Span<char> buf = stackalloc char[path.Length];
				return ParseInternal(path, buf);
			}
			else
			{ // else rent a buffer from a pool
				char[] buf = ArrayPool<char>.Shared.Rent(path.Length);
				var res = ParseInternal(path, buf);
				ArrayPool<char>.Shared.Return(buf);
				return res;
			}
		}

		private static FdbPath ParseInternal(ReadOnlySpan<char> path, Span<char> buffer)
		{
			var segments = new List<FdbPathSegment>();

			// the main loop only only attempts to split the segments by finding valid '/' separators (note that '/' can be escaped (via '\/') and is NOT a segment separator)
			// => when we find a segment, we slice it and then use FdbPathSegment.Parse(...) to actual unescape the name (and optional layer Id)

			bool absolute = false;
			bool escaped = false;
			int start = 0;
			int pos = 0;
			foreach (var c in path)
			{
				switch (c)
				{
					case '\\':
					{
						escaped = !escaped;
						++pos;
						break;
					}
					case '/':
					{
						if (escaped)
						{
							escaped = false;
							++pos;
							break;
						}

						if (pos == 0)
						{
							if (segments.Count == 0)
							{ // first '/' means that it is an absolute path
								absolute = true;
								pos = 1;
								start = 1;
								continue;
							}
							throw new FormatException("Invalid directory path: segment cannot be empty.");
						}

						segments.Add(FdbPathSegment.Parse(path.Slice(start, pos - start)));
						++pos;
						start = pos;
						break;
					}
					default:
					{
						if (escaped)
						{
							escaped = false;
						}
						++pos;
						break;
					}
				}
			}

			if (pos != start)
			{ // add last segment
				segments.Add(FdbPathSegment.Parse(path.Slice(start, pos - start)));
			}
			else if (segments.Count > 0 && path[path.Length - 1] == '/')
			{ // extra '/' at the end !
				throw new FormatException("Invalid directory path: last segment cannot be empty.");
			}

			return new FdbPath(segments.ToArray(), absolute);
		}

		#endregion

		#region Equality...

		public override int GetHashCode()
		{
			//this must be fast because we will use paths as keys in directories a lot (cache context)

			//note: we cannot use this.Segments.GetHashCode() because it will vary depending on the underlying backing store!
			// => we will use the head, middle point and tail of the segment to compute the hash

			var segments = this.Segments.Span;
			switch (segments.Length)
			{
				case 0: return this.IsAbsolute ? -1 : 0;
				case 1: return HashCodes.Combine(this.IsAbsolute ? -1 : 1, segments[0].GetHashCode());
				case 2: return HashCodes.Combine(this.IsAbsolute ? -2 : 2, segments[0].GetHashCode(), segments[1].GetHashCode());
				default: return HashCodes.Combine(this.IsAbsolute ? -3 : 3, segments[0].GetHashCode(), segments[segments.Length >> 1].GetHashCode(), segments[segments.Length - 1].GetHashCode());
			}
		}

		public override bool Equals(object obj)
		{
			return obj is FdbPath other && Equals(other);
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
		public static FdbPath operator +(FdbPath path, FdbPathSegment segment)
			=> path.Add(segment);

		#endregion

	}
}
