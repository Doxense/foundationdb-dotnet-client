#region BSD License
/* Copyright (c) 2013-2018, Doxense SAS
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

namespace FoundationDB.Layers.Directories
{
	using System;
	using System.Collections;
	using System.Collections.Generic;
	using System.Diagnostics;
	using System.Linq;
	using System.Runtime.CompilerServices;
	using Doxense.Collections.Tuples;
	using Doxense.Diagnostics.Contracts;
	using JetBrains.Annotations;

	/// <summary>Represents a path in a Directory Layer</summary>
	[DebuggerDisplay("{ToString(),nq}")]
	public readonly struct FdbDirectoryPath : IReadOnlyList<string>, IEquatable<FdbDirectoryPath>
	{
		public static readonly FdbDirectoryPath Empty = new FdbDirectoryPath(Array.Empty<string>());

		internal readonly string[] Segments;

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public FdbDirectoryPath(string[] segments)
		{
			this.Segments = segments;
		}

		public bool IsEmpty
		{
			[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
			get => this.Segments == null || this.Segments.Length == 0;
		}

		public int Count => GetSegments().Length;

		public string this[int index] => GetSegments()[index];

		public string Name
		{
			[Pure, NotNull]
			get
			{
				var segments = GetSegments();
				return segments.Length != 0 ? segments[segments.Length - 1] : string.Empty;
			}
		}

		[Pure]
		public FdbDirectoryPath Concat(FdbDirectoryPath path)
		{
			if (path.IsEmpty) return this;
			if (this.IsEmpty) return path;

			var segments = this.Segments;
			int n = segments.Length;
			Array.Resize(ref segments, checked(n + path.Segments.Length));
			path.Segments.CopyTo(segments, n);
			return new FdbDirectoryPath(segments);
		}

		[Pure]
		public FdbDirectoryPath Concat([NotNull] string segment)
		{
			if (this.IsEmpty) return Create(segment);
			var segments = this.Segments;
			int n = segments.Length;
			Array.Resize(ref segments,  checked(n + 1));
			segments[n] = segment;
			return new FdbDirectoryPath(segments);
		}

#if NETCORE
		[Pure]
		public FdbDirectoryPath Concat(ReadOnlySpan<char> segment)
		{
			return Concat(segment.ToString());
		}
#endif

		[Pure]
		public FdbDirectoryPath Concat([NotNull, ItemNotNull] params string[] path)
		{
			if (this.IsEmpty) return Create(path);
			var segments = this.Segments;
			int n = segments.Length;
			Array.Resize(ref segments, checked(n + path.Length));
			path.CopyTo(segments, n);
			return new FdbDirectoryPath(segments);
		}

		[Pure]
		public FdbDirectoryPath Concat([NotNull, ItemNotNull] IEnumerable<string> segments)
		{
			Contract.NotNull(segments, nameof(segments));
			if (this.IsEmpty) return Create(segments);
			var after = new List<string>();
			after.AddRange(this.Segments);
			after.AddRange(segments);
			return new FdbDirectoryPath(after.ToArray());
		}

		[Pure]
		public FdbDirectoryPath Concat([NotNull] IVarTuple segments)
		{
			Contract.NotNull(segments, nameof(segments));
			if (this.IsEmpty) return Create(segments);
			var after = new List<string>();
			after.AddRange(this.Segments);
			after.AddRange(segments.ToArray<string>());
			return new FdbDirectoryPath(after.ToArray());
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public IEnumerator<string> GetEnumerator()
		{
			return ((IEnumerable<string>) GetSegments()).GetEnumerator();
		}

		public override string ToString()
		{
			return FormatPath(GetSegments());
		}

		IEnumerator IEnumerable.GetEnumerator()
		{
			return GetEnumerator();
		}

		[Pure, NotNull, MethodImpl(MethodImplOptions.AggressiveInlining)]
		internal string[] GetSegments() => this.Segments ?? Array.Empty<string>();

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public string[] ToArray()
		{
			return GetSegments().ToArray();
		}

#if NETCORE
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public ReadOnlySpan<string> AsSpan()
		{
			return GetSegments().AsSpan();
		}
#endif

		public void CopyTo(string[] array, int offset)
		{
			GetSegments().CopyTo(array, offset);
		}

		[Pure]
		public static FdbDirectoryPath Create([NotNull, ItemNotNull] IEnumerable<string> segments)
		{
			Contract.NotNull(segments, nameof(segments));
			return new FdbDirectoryPath(segments.ToArray());
		}

		[Pure]
		public static FdbDirectoryPath Create([NotNull] string segment)
		{
			Contract.NotNull(segment, nameof(segment));
			return new FdbDirectoryPath(new [] { segment });
		}

#if NETCORE
		[Pure]
		public static FdbDirectoryPath Create(ReadOnlySpan<char> segment)
		{
			return Create(segment.ToString());
		}
#endif

		/// <summary>Convert a tuple representing a path, into a string array</summary>
		/// <param name="path">Tuple that should only contain strings</param>
		/// <returns>Array of strings</returns>
		[Pure]
		public static FdbDirectoryPath Create([NotNull] IVarTuple path)
		{
			Contract.NotNull(path, nameof(path));
			return Create(path.ToArray<string>());
		}

		[Pure]
		public static FdbDirectoryPath Create([NotNull, ItemNotNull] params string[] segments)
		{
			Contract.NotNull(segments, nameof(segments));
			return new FdbDirectoryPath(segments);
		}

		[Pure]
		public static FdbDirectoryPath Parse([CanBeNull] string path)
		{
			if (string.IsNullOrEmpty(path)) return Empty;

			var paths = new List<string>();
			var sb = new System.Text.StringBuilder();
			bool escaped = false;
			foreach (var c in path)
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
						if (sb.Length == 0 && paths.Count == 0)
						{ // ignore the first '/'
							continue;
						}
						paths.Add(sb.ToString());
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
				paths.Add(sb.ToString());
			}
			return new FdbDirectoryPath(paths.ToArray());
		}


		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static implicit operator FdbDirectoryPath([NotNull] string segment)
		{
			return Create(segment);
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static implicit operator FdbDirectoryPath([NotNull, ItemNotNull] string[] segments)
		{
			return Create(segments);
		}

		[Pure, NotNull]
		internal static string FormatPath([NotNull, ItemNotNull] string[] paths)
		{
			Contract.NotNull(paths, nameof(paths));

			return string.Join("/", paths.Select(path => path.Contains('\\') || path.Contains('/')
				? path.Replace("\\", "\\\\").Replace("/", "\\/")
				: path));
		}

		[Pure, NotNull]
		internal IVarTuple ToTuple()
		{
			return this.IsEmpty ? STuple.Empty : STuple.FromArray(this.Segments);
		}

		public override int GetHashCode()
		{
			var segments = GetSegments();
			int h = segments.Length;
			foreach (var s in segments)
			{
				h = (h * 31) ^ s.GetHashCode();
			}
			return h;
		}

		public override bool Equals(object obj)
		{
			return obj is FdbDirectoryPath path && Equals(path);
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool Equals(FdbDirectoryPath other)
		{
			return this == other;
		}

		public static bool operator ==(FdbDirectoryPath left, FdbDirectoryPath right)
		{
			var l = left.GetSegments();
			var r = right.GetSegments();
			if (l.Length != r.Length) return false;
			for (int i = 0; i < l.Length; i++)
			{
				if (l[i] != r[i]) return false;
			}
			return true;
		}

		public static bool operator !=(FdbDirectoryPath left, FdbDirectoryPath right)
		{
			var l = left.GetSegments();
			var r = right.GetSegments();
			if (l.Length != r.Length) return true;
			for (int i = 0; i < l.Length; i++)
			{
				if (l[i] != r[i]) return true;
			}
			return false;
		}

	}

}
