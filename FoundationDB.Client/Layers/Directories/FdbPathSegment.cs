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
	using System.Runtime.CompilerServices;
	using System.Text;

	/// <summary>Represent a segment in a <see cref="FdbPath">path</see> to a <see cref="IFdbDirectory">Directory</see>.</summary>
	/// <remark>A path segment is composed of a <see cref="Name"/> and optional <see cref="LayerId"/> field.</remark>
	public readonly struct FdbPathSegment : IEquatable<FdbPathSegment>
	{

		// Rules for encoding a segment into a string: '/', '\', '[' and ']' are escaped by prefixing them by another '\'
		// - (Name: "Hello", LayerId: "") => "Hello"
		// - (Name: "Hello", LayerId: "Foo") => "Hello[Foo]"
		// - (Name: "Hello/World", "") => "Hello\/World"
		// - (Name: "Hello[World]", "") => "Hello\[World\]"
		// - (Name: "Hello[World]", "Foo") => "Hello\[World\][Foo]"
		// - (Name: "Hello, "Foo/Bar") => "Hello[Foo\/Bar]"
		// - (Name: "Hello, "Foo[Bar]") => "Hello[Foo\[Bar\]]"

		/// <summary>Name of the segment</summary>
		/// <remarks>This is the equivalent of the name of the "folder"</remarks>
		public readonly string Name;

		/// <summary>Id of the layer used by the corresponding directory; or <c>null</c> if it is not specified</summary>
		/// <remarks>If present: when opening a directory, its layer id will compared to this value; when creating a directory, this value will be used as its layer id.</remarks>
		public readonly string LayerId;

		public FdbPathSegment(string name, string? layerId = null)
		{
			this.Name = name;
			this.LayerId = layerId ?? string.Empty;
		}

		public bool IsEmpty => string.IsNullOrEmpty(this.Name);

		private static readonly char[] EscapedLiterals = "\\/[]".ToCharArray();

		private static string Escape(string value)
		{
			if (value.AsSpan().IndexOfAny(EscapedLiterals) >= 0)
			{
				return value.Replace("\\", "\\\\").Replace("/", "\\/").Replace("[", "\\[").Replace("]", "\\]");
			}
			return value;
		}

		/// <summary>Return a path segment composed of only a name, but without any layer id specified</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static FdbPathSegment Create(string name)
			=> new FdbPathSegment(name, string.Empty);

		/// <summary>Return a path segment composed both a name, and a layer id</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static FdbPathSegment Create(string name, string layerId)
			=> new FdbPathSegment(name, layerId);

		/// <summary>Return a path segment composed a name, and the "partition" layer id</summary>
		public static FdbPathSegment Partition(string name) => new FdbPathSegment(name, FdbDirectoryPartition.LayerId);

		public static string Encode(string name)
			=> Escape(name);

		public static string Encode(string name, string? layerId)
			=> string.IsNullOrEmpty(layerId)
				? Escape(name)
				: Escape(name) + "[" + Escape(layerId) + "]";

		internal static StringBuilder AppendTo(StringBuilder sb, string name)
			=> sb.Append(Escape(name));

		internal static StringBuilder AppendTo(StringBuilder sb, string name, string? layerId)
		{
			sb.Append(Escape(name));
			if (!string.IsNullOrEmpty(layerId))
			{
				sb.Append('[').Append(Escape(layerId)).Append(']');
			}
			return sb;
		}

		public static FdbPathSegment[] Parse(ReadOnlySpan<string> segments)
		{
			var tmp = new FdbPathSegment[segments.Length];
			for (int i = 0; i < segments.Length; i++)
			{
				tmp[i] = Parse(segments[i]);
			}
			return tmp;
		}

		/// <summary>Parse a string representation of a path segment (name with optional layer id)</summary>
		/// <param name="value">Encoded path segment</param>
		/// <returns>Decoded path segment (may include an optional layer id)</returns>
		/// <example>Parse("Foo") == FdbPathSegment.Create("Foo"); Parse("Foo[SomeLayer]") == FdbPathSegment.Create("Foo", "SomeLayer")</example>
		public static FdbPathSegment Parse(string value)
		{
			return Parse(value.AsSpan());
		}

		/// <summary>Parse a string representation of a path segment (name with optional layer id)</summary>
		/// <param name="value">Encoded path segment</param>
		/// <returns>Decoded path segment (may include an optional layer id)</returns>
		/// <example>Parse("Foo") == FdbPathSegment.Create("Foo"); Parse("Foo[SomeLayer]") == FdbPathSegment.Create("Foo", "SomeLayer")</example>
		public static FdbPathSegment Parse(ReadOnlySpan<char> value)
		{
			var sb = new StringBuilder(value.Length);
			bool escaped = false;
			bool inLayer = false;

			string? name = null;
			string? layerId = null;

			foreach (var c in value)
			{
				switch (c)
				{
					case '\\':
					{
						if (escaped)
						{
							sb.Append('\\');
							escaped = false;
						}
						else
						{
							escaped = true;
						}
						break;
					}
					case '[':
					{
						if (escaped)
						{
							sb.Append('[');
							escaped = false;
							break;
						}
						if (inLayer) throw new FormatException("Invalid path segment: unescaped '[' inside layer keyword");
						name = sb.ToString();
						sb.Clear();
						inLayer = true;
						break;
					}
					case ']':
					{
						if (escaped || !inLayer)
						{
							sb.Append(']');
							escaped = false;
							break;
						}
						//note: the layer string can be empty '[]'
						layerId = sb.ToString();
						sb.Clear();
						inLayer = false;
						break;
					}
					default:
					{
						sb.Append(c);
						escaped = false;
						break;
					}
				}
			}

			if (name == null && sb.Length != 0)
			{
				name = sb.ToString();
			}

			if (string.IsNullOrEmpty(name)) throw new FormatException("Invalid path segment: name cannot be empty");

			return new FdbPathSegment(name, layerId);
		}

		/// <summary>Return an encoded string representation of this path segment</summary>
		/// <returns>Encoded string, with optional layer id</returns>
		/// <example>FdbPathSegment.Create("Foo").ToString() == "Foo"; FdbPathSegment.Create("Foo", "SomeLayer") == "Foo[SomeLayer]"</example>
		/// <remarks>The string retured can be parsed back into the original segment via <see cref="Parse(string)"/>.</remarks>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public override string ToString()
		{
			return Encode(this.Name, this.LayerId);
		}

		/// <summary>Extract the pair of name and layer id from this segment</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void Deconstruct(out string name, out string? layerId)
		{
			name = this.Name;
			layerId = this.LayerId ?? string.Empty;
		}

		#region Equality...

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public override bool Equals(object obj)
			=> obj is FdbPathSegment other && Equals(other);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public override int GetHashCode()
			=> HashCodes.Combine(this.Name?.GetHashCode() ?? -1, (this.LayerId ?? string.Empty).GetHashCode());

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool Equals(FdbPathSegment other)
			=> string.Equals(this.Name, other.Name) && string.Equals(this.LayerId ?? string.Empty, other.LayerId ?? string.Empty);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator ==(FdbPathSegment left, FdbPathSegment right)
			=> left.Equals(right);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator !=(FdbPathSegment left, FdbPathSegment right)
			=> !left.Equals(right);

		#endregion

	}
}
