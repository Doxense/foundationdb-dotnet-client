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

namespace SnowBank.Shell.Prompt
{
	using System.Runtime.CompilerServices;
	using System.Text;
	using Doxense.Linq;

	/// <summary>Represents a single token in a prompt.</summary>
	/// <remarks>
	/// <para>A token correspond to a single "word" in a prompt, separated by a spaces.</para>
	/// <para>
	/// A token can be subdivided into one or multiple fragments.
	/// If a token has a uniform color, it will be composed of a single fragment.
	/// If a token uses multiple colors, it will split into has many fragments as required.
	/// </para>
	/// </remarks>
	/// <seealso cref="PromptTokenFragment"/>
	[PublicAPI]
	public readonly record struct PromptToken
	{
		// Single fragment:
		// - Fragments = [ ("hello", "yellow") ]
		//   - Raw = "hello"
		//   - Markup = "[yellow]hello[/]"

		// Multiple fragments:
		// - Fragments = [ ("--", "gray"), ("force", "white") ]
		//   - Raw = "--force"
		//   - Markup = "[gray]--[/][white]force[/]"

		public readonly string? Type; //TODO!!

		public readonly ReadOnlyMemory<PromptTokenFragment> Fragments;

		private readonly string? m_rawText;

		public string RawText => m_rawText ?? "";

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static PromptToken Create(string type, string? literal, string? foreground = null, string? background = null)
			=> Create(type, PromptTokenFragment.Create(literal, foreground, background));

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static PromptToken Create(string type, PromptTokenFragment fragment) => new(type, new [] { fragment });

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static PromptToken Create(string type, PromptTokenFragment[]? fragments) => new(type, fragments ?? [ ]);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static PromptToken Create(string type, ReadOnlySpan<PromptTokenFragment> fragments) => new(type, fragments.ToArray());

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static PromptToken Create(string type, ReadOnlyMemory<PromptTokenFragment> fragments) => new(type, fragments);

		public static PromptToken Create(string type, IEnumerable<PromptTokenFragment>? fragments) => new(type, fragments?.ToArray() ?? [ ]);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public PromptToken(string type, ReadOnlyMemory<PromptTokenFragment> fragments)
		{
			this.Type = type;
			this.Fragments = fragments;
			m_rawText = GetRawText(fragments.Span);
		}

		public int Count => this.Fragments.Length;

		public PromptTokenFragment this[int index] => this.Fragments.Span[index];

		public PromptTokenFragment this[Index index] => this.Fragments.Span[index];

		internal static string GetRawText(ReadOnlySpan<PromptTokenFragment> fragments)
		{
			return fragments.Length switch
			{
				0 => "",
				1 => fragments[0].Literal.GetStringOrCopy(),
				2 => string.Concat(fragments[0].Literal.Span, fragments[1].Literal.Span),
				3 => string.Concat(fragments[0].Literal.Span, fragments[1].Literal.Span, fragments[2].Literal.Span),
				4 => string.Concat(fragments[0].Literal.Span, fragments[1].Literal.Span, fragments[2].Literal.Span, fragments[3].Literal.Span),
				_ => ConcatMultipleFragments(fragments),
			};

			static string ConcatMultipleFragments(ReadOnlySpan<PromptTokenFragment> fragments)
			{
				var sw = new ValueStringWriter();
				foreach (var fragment in fragments) sw.Write(fragment.Literal);
				return sw.ToStringAndDispose();
			}

		}

		public void AppendRawTo(ref ValueStringWriter destination)
		{
			foreach (var frag in this.Fragments.Span)
			{
				destination.Write(frag.Literal);
			}
		}

		public override string ToString()
		{
			var sb = new StringBuilder();
			sb.Append(this.Type).Append(':');
			var span = this.Fragments.Span;
			for(int i = 0; i < span.Length; i++)
			{
				ref readonly var frag = ref span[i];
				if (i != 0) sb.Append('|');
				sb.Append('\'').Append(frag.Literal).Append('\'');
				if (frag.HasColor) sb.Append($"[F={frag.Foreground},B={frag.Background}]");
			}
			return sb.ToString();
		}

		public override int GetHashCode()
		{
			var h = new HashCode();
			foreach (var frag in this.Fragments.Span)
			{
				h.Add(frag);
			}
			return h.ToHashCode();
		}

		public bool Equals(PromptToken other)
		{
			if (this.Type != other.Type) return false;
			if (m_rawText != other.m_rawText) return false;

			var thisFrags = this.Fragments.Span;
			var otherFrags = other.Fragments.Span;
			if (otherFrags.Length != thisFrags.Length) return false;
			for (int i = 0; i < thisFrags.Length; i++)
			{
				if (!thisFrags[i].Equals(otherFrags[i]))
				{
					return false;
				}
			}

			return true;
		}
	}

}
