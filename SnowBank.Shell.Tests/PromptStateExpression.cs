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

namespace SnowBank.Shell.Prompt.Tests
{
	using System.Text;
	using JetBrains.Annotations;
	using Microsoft.VisualStudio.TestPlatform.ObjectModel;

	[UsedImplicitly]
	public abstract class PromptStateExpression
	{

		public static PromptStateExpression<TCommand> For<TCommand>(TCommand command, IPromptTheme theme) where TCommand : IPromptCommandDescriptor
			=> new(new()
			{
				Change = PromptChange.None,
				RawText = "",
				RawToken = "",
				Tokens = [ ],
				Token = default,
				Candidates = [ ],
				Command = command,
				CommandBuilder = command.StartNew(),
				Theme = theme,
			});


		public PromptState State { get; }

		protected PromptStateExpression(PromptState state) => this.State = state;

	}

	[UsedImplicitly]
	public sealed class PromptStateExpression<TCommand> : PromptStateExpression
		where TCommand : IPromptCommandDescriptor
		{

		public PromptStateExpression(PromptState state) : base(state) { }

		[MustUseReturnValue]
		public PromptStateExpression<TCommand> Add(char c) => WithState(PromptChange.Add, c);

		[MustUseReturnValue]
		public PromptStateExpression<TCommand> Next() => WithState(PromptChange.NextToken, ' ');

		[MustUseReturnValue]
		public PromptStateExpression<TCommand> Completed() => WithState(PromptChange.Completed, '\t');

		[MustUseReturnValue]
		public PromptStateExpression<TCommand> Done() => WithState(PromptChange.Done, '\n');

		[MustUseReturnValue]
		public PromptStateExpression<TCommand> Aborted() => WithState(PromptChange.Aborted, '\e');

		[MustUseReturnValue]
		public PromptStateExpression<TCommand> WithState(PromptChange change, char lastKey) => new(this.State with { Change = change, LastKey = lastKey });

		private static PromptToken DecodeWord(string word)
		{
			if (string.IsNullOrEmpty(word))
			{
				return default;
			}

			int p = word.IndexOf(':');
			if (p < 1) throw new FormatException($"Word '{word}' must include type");
			return PromptToken.Create(word[..p], word[(p + 1)..]);
		}

		[MustUseReturnValue]
		public PromptStateExpression<TCommand> Tokens(params string[] words)
		{
			if (words.Length == 0) words = [""];

			var tokens = new List<PromptToken>();

			foreach (var word in words)
			{
				tokens.Add(DecodeWord(word));
			}

			var rawText = string.Join(" ", tokens.Select(t => t.RawText));

			PromptToken token = default;
			string rawToken = "";

			if (this.State.Change != PromptChange.Done)
			{
				token = tokens[^1];
				rawToken = token.RawText;
				tokens.RemoveAt(tokens.Count - 1);
			}

			return new(this.State with
			{
				RawText = rawText,
				RawToken = rawToken,
				Tokens = [..tokens],
				Token = token,
			});
		}

		[MustUseReturnValue]
		public PromptStateExpression<TCommand> Builder(IPromptCommandBuilder builder)
		{
			return new(this.State with
			{
				CommandBuilder = builder,
			});
		}

		[MustUseReturnValue]
		public PromptStateExpression<TCommand> Candidates(ReadOnlySpan<string> candidates, string? exactMatch = null, string? commonPrefix = null)
		{
			return new(this.State with
			{
				Candidates = [..candidates],
				ExactMatch = exactMatch,
				CommonPrefix = commonPrefix,
			});
		}

		[MustUseReturnValue]
		public PromptStateExpression<TCommand> Candidates(IEnumerable<string> candidates, string? exactMatch = null, string? commonPrefix = null)
		{
			if (Buffer<string>.TryGetSpan(candidates, out var span))
			{
				return this.Candidates(span, exactMatch, commonPrefix);
			}
			else
			{
				return this.Candidates(candidates.ToArray().AsSpan(), exactMatch, commonPrefix);
			}
		}

	}

}
