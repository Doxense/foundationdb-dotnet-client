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
	using System.Diagnostics;
	using System.Diagnostics.CodeAnalysis;
	using System.IO;
	using System.Linq;
	using System.Runtime.InteropServices;
	using System.Text;

	public enum FqlPathSegmentType
	{
		Invalid = 0,
		Literal,
		Any,
	}

	[DebuggerDisplay("{ToString(),nq}")]
	[PublicAPI]
	public readonly struct FqlPathSegment : IEquatable<FqlPathSegment>, IFqlExpression
	{

		public readonly FqlPathSegmentType Type;

		public readonly FdbPathSegment Value;

		public FqlPathSegment(FqlPathSegmentType type, FdbPathSegment? value)
		{
			this.Type = type;
			this.Value = value ?? FdbPathSegment.Empty;
		}

		/// <inheritdoc />
		public bool IsPattern => this.Type is FqlPathSegmentType.Any;

		public static FqlPathSegment Literal(FdbPathSegment segment) => new(FqlPathSegmentType.Literal, segment);

		public static FqlPathSegment Literal(string value, string? layerId) => new(FqlPathSegmentType.Literal, layerId != null ? FdbPathSegment.Create(value, layerId) : FdbPathSegment.Create(value));

		public static FqlPathSegment Any() => new(FqlPathSegmentType.Any, null);

		public bool Matches(FdbPathSegment segment)
		{
			switch (this.Type)
			{
				case FqlPathSegmentType.Any:
				{
					return true;
				}
				case FqlPathSegmentType.Literal:
				{
					return string.IsNullOrEmpty(this.Value.LayerId)
						? this.Value.Name == segment.Name
						: this.Value.Name == segment.Name && this.Value.LayerId == segment.LayerId;
				}
				default:
				{
					return false;
				}
			}

		}

		/// <inheritdoc />
		public override bool Equals([NotNullWhen(true)] object? obj) => obj is FqlPathSegment seg && Equals(seg);

		/// <inheritdoc />
		public override int GetHashCode() => HashCode.Combine(this.Type, this.Value);

		/// <inheritdoc />
		public bool Equals(FqlPathSegment other) => other.Type == this.Type && this.Value.Equals(other.Value);

		/// <inheritdoc />
		public override string ToString() => this.Type switch
		{
			FqlPathSegmentType.Any => "<>",
			FqlPathSegmentType.Literal => (this.Value.Name.Contains('"') ? $"\"{this.Value.Name.Replace("\"", "\\\"")}\"" : this.Value.Name) + (!string.IsNullOrEmpty(this.Value.LayerId) ? $"[{this.Value.LayerId}]" : ""),
			_ => "<invalid>"
		};

		/// <inheritdoc />
		public void Explain(TextWriter output, int depth = 0, bool recursive = true)
		{
			var indent = new string('\t', depth) + (depth == 0 ? "" : "- ");
			output.WriteLine($"{indent}{ToString()}");
		}

	}

	[DebuggerDisplay("{ToString(),nq}")]
	[PublicAPI]
	public sealed class FqlDirectoryExpression : IEquatable<FqlDirectoryExpression>, IFqlExpression
	{

		public List<FqlPathSegment> Segments { get; } = [ ];

		/// <inheritdoc />
		public bool IsPattern => this.Segments.Any(x => x.IsPattern);

		public static FqlDirectoryExpression Create() => new();

		public FqlDirectoryExpression Add(FqlPathSegment segment)
		{
			this.Segments.Add(segment);
			return this;
		}

		public FqlDirectoryExpression AddLiteral(FdbPathSegment segment)
		{
			this.Segments.Add(FqlPathSegment.Literal(segment));
			return this;
		}

		public FqlDirectoryExpression AddLiteral(string name)
		{
			this.Segments.Add(FqlPathSegment.Literal(FdbPathSegment.Create(name)));
			return this;
		}

		public FqlDirectoryExpression AddLiteral(string name, string layerId)
		{
			this.Segments.Add(FqlPathSegment.Literal(FdbPathSegment.Create(name, layerId)));
			return this;
		}

		public FqlDirectoryExpression AddAny()
		{
			this.Segments.Add(FqlPathSegment.Any());
			return this;
		}

		public bool Matches(FdbPath path)
		{
			var segments = CollectionsMarshal.AsSpan(this.Segments);

			if (path.Count != segments.Length)
			{ // must have the same number of segments
				return false;
			}

			for (int i = 0; i < segments.Length; i++)
			{
				if (!segments[i].Matches(path[i])) return false;
			}

			return true;
		}

		/// <inheritdoc />
		public override string ToString()
		{

			var sb = new StringBuilder();

			foreach (var seg in this.Segments)
			{
				sb.Append('/');
				switch (seg.Type)
				{
					case FqlPathSegmentType.Literal: sb.Append(seg.ToString()); break;
					case FqlPathSegmentType.Any: sb.Append("<>"); break;
					default: sb.Append("<???>"); break;
				}
			}

			return sb.ToString();
		}

		/// <inheritdoc />
		public void Explain(TextWriter output, int depth = 0, bool recursive = true)
		{
			var indent = new string('\t', depth) + (depth == 0 ? "" : " -");

			if (!recursive)
			{
				output.WriteLine($"{indent}Directory: [{this.Segments.Count}] {ToString()}");
				return;
			}

			output.WriteLine($"{indent}- Directory: [{this.Segments.Count}]");
			depth++;
			foreach (var segment in this.Segments)
			{
				segment.Explain(output, depth);
			}
		}

		/// <inheritdoc />
		public override bool Equals([NotNullWhen(true)] object? obj) => obj is FqlDirectoryExpression dir && Equals(dir);

		/// <inheritdoc />
		public override int GetHashCode()
		{
			var segments = this.Segments;
			var h = new HashCode();
			foreach (var seg in segments)
			{
				h.Add(seg.GetHashCode());
			}
			return h.ToHashCode();
		}

		/// <inheritdoc />
		public bool Equals(FqlDirectoryExpression? other)
		{
			if (other is null) return false;
			if (ReferenceEquals(this, other)) return true;
			var segments = this.Segments;
			var otherSegments = other.Segments;
			if (segments.Count != otherSegments.Count) return false;
			for (int i = 0; i < segments.Count; i++)
			{
				if (!segments[i].Equals(otherSegments[i])) return false;
			}
			return true;
		}

	}

}

#endif
