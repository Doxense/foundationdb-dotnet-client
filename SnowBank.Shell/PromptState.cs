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
	using System.Collections.Immutable;
	using Doxense.Runtime;
	using Doxense.Serialization;
	using Doxense.Serialization.Json;

	/// <summary>Snapshot of a prompt at a point in time</summary>
	/// <remarks>Any keystroke transforms a current prompt state into a new prompt state (which could be the same)</remarks>
	public sealed record PromptState : ICanExplain
	{

		public static PromptState CreateEmpty(RootPromptCommand root, IPromptTheme theme) => new()
		{
			Change = PromptChange.Empty,
			LastKey = '\0',
			RawText = "",
			RawToken = "",
			Tokens = [ ],
			Token = default,
			Candidates = [ ],
			Command = root,
			CommandBuilder = root.StartNew(),
			Theme = theme,
		};

		/// <summary>The change due to the last user action (from the previous state to this state)</summary>
		/// <remarks>
		/// <para>For example, if the change is <see cref="PromptChange.Completed"/>, we can use this to perform a "soft space" when typing text right after a TAB</para>
		/// <para>Ex: typing <c>hel[TAB]wor[TAB]</c> will autocomplete to <c>hello world</c> without having to type the space between both tokens</para>
		/// </remarks>
		public required PromptChange Change { get; init; }

		public char LastKey { get; init; }

		public bool IsDone() => this.Change is PromptChange.Done or PromptChange.Aborted;

		public PromptState? Parent { get; init; }

		/// <summary>List of completed tokens in the prompt</summary>
		/// <remarks>
		/// <para>Does not include the token currently being edited!</para>
		/// </remarks>
		public required ImmutableArray<PromptToken> Tokens { get; init; }

		/// <summary>The decorated version of <see cref="RawToken"/></summary>
		public required PromptToken Token { get; init; }

		/// <summary>The raw text of the whole prompt</summary>
		public required string RawText { get; init; }

		/// <summary>The raw text of the currently edited token</summary>
		public required string RawToken { get; init; }

		public required IPromptCommandDescriptor Command { get; init; }

		public required IPromptCommandBuilder CommandBuilder { get; init; }

		public required IPromptTheme Theme { get; init; }

		/// <summary>List of all auto-complete candidates</summary>
		public required string[] Candidates { get; init; }
		/// <summary>There is an exact match with an auto-complete</summary>
		public string? ExactMatch { get; init; }

		/// <summary>There is a common prefix to all remaining auto-complete candidates</summary>
		public string? CommonPrefix { get; init; }

		public void Explain(ExplanationBuilder builder)
		{
			builder.WriteLine($"Prompt: '{this.RawText}'");
			builder.Enter();
			builder.WriteLine($"Change : {this.Change}, Char={(this.LastKey < 32 ? $"0x{(int) this.LastKey:x02}" : $"'{this.LastKey}'")}");
			builder.WriteLine($"Tokens : [ {string.Join(", ", this.Tokens)} ] (count={this.Tokens.Length})");
			builder.WriteLine($"Token  : '{this.RawToken}' ({this.Token.ToString()})");
			builder.WriteLine($"Command: <{this.Command.GetType().GetFriendlyName()}>, '{this.Command.SyntaxHint}'");
			builder.WriteLine($"Builder: <{this.CommandBuilder?.GetType().GetFriendlyName()}>, {this.CommandBuilder}");
			if (this.CommandBuilder is ICanExplain explain)
			{
				builder.ExplainChild(explain);
			}

			if (this.Candidates.Length > 0)
			{
				builder.WriteLine($"Candidates: ({this.Candidates.Length}) [ {string.Join(", ", this.Candidates.Select(c => $"'{c}'"))} ]");
			}

			if (this.ExactMatch is not null)
			{
				builder.WriteLine($"Match: '{this.ExactMatch}' (exact)");
			}
			if (this.CommonPrefix is not null)
			{
				builder.WriteLine($"Prefix: '{this.CommonPrefix}'");
			}
			builder.Leave();

		}
	}

}
