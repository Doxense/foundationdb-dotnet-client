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

	public class AnsiConsolePromptTheme : IPromptTheme
	{

		/// <summary>The prompt, decorated with markup, (part before the user input)</summary>
		public required string Prompt { get; init; }

		public required int MaxRows { get; init; } = 5;

		/// <inheritdoc />
		public RenderState Paint(PromptState state)
		{
			var promptMarkup = "[gray]" + this.Prompt + "[/]";
			var text = state.Text;
			var token = state.Token;
			var candidates = state.Candidates;
			var rows = new List<(string Raw, string Markup)>();

			// if we have multiple candidates, first jump ahead, write them, and then go back up!
			if (candidates?.Count > 0)
			{
				for (var i = 0; i < candidates.Count; i++)
				{
					if (i >= this.MaxRows) break;

					var c = candidates[i];
					if (c == state.ExactMatch)
					{
						rows.Add((c, $"[bold cyan]{c}[/]"));
					}
					else if (state.CommonPrefix == null || state.CommonPrefix.Length == token.Length)
					{
						rows.Add((c, $"[cyan]{c[..token.Length]}[/][gray]{c[token.Length..]}[/]"));
					}
					else
					{
						rows.Add((c, $"[cyan]{c[..token.Length]}[/][darkcyan]{state.CommonPrefix[token.Length..]}[/][gray]{c[state.CommonPrefix.Length..]}[/]"));
					}

				}
			}

			string markup = "";
			if (state.TokenStart > 0)
			{
				markup += (!state.CommandBuilder.IsValid() ? "[red]" : "[silver]") + text[..state.TokenStart] + "[/]";
			}

			int extra = 0;

			if (state.TokenStart < text.Length)
			{
				// if we have an exact match => green
				// if we have a single incomplete match => cyan
				// if we have multiple incomplete matches => yellow
				// otherwise, white
				string color =
					state.ExactMatch != null ? "[green]"
					: candidates?.Count == 1 ? "[cyan]"
					: (candidates?.Count ?? 0) != 0 ? "[yellow]"
					: "[white]";

				markup += color + text[state.TokenStart..] + "[/]";

				if (candidates?.Count == 1)
				{ // we have a candidate, write a "ghost" with the remainder (TAB will auto-complete)
					var suffix = candidates[0][state.Token.Length..];
					markup = markup + "[darkcyan]" + suffix + "[/]";
					extra = suffix.Length;
				}
			}

			return new()
			{
				PromptRaw = this.Prompt,
				TextRaw = text,
				PromptMarkup = promptMarkup,
				TextMarkup = markup,
				Extra = extra,
				Cursor = text.Length,
				Rows = rows,
			};
		}

	}

}
