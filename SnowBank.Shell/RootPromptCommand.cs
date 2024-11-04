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
	using System.Diagnostics.CodeAnalysis;
	using Doxense.Diagnostics.Contracts;
	using Doxense.Runtime;

	public class RootPromptCommand : IPromptCommandDescriptor
	{

		public List<IPromptCommandDescriptor> Commands { get; init; } = [];

		string IPromptCommandDescriptor.Token => "";

		string IPromptCommandDescriptor.Description => "";

		string IPromptCommandDescriptor.SyntaxHint => "";

		public IPromptCommandBuilder StartNew()
		{
			return new Builder(this);
		}


		public void GetTokens(List<string> candidates, string command, string tok)
		{
			foreach (var cmd in this.Commands)
			{
				if (cmd.Token.StartsWith(tok))
				{
					candidates.Add(cmd.Token);
				}
			}
		}

		public sealed record Builder : IPromptCommandBuilder, ICanExplain
		{

			internal Builder(RootPromptCommand root)
			{
				this.Descriptor = root;
			}

			public string CommandName { get; init; } = "";

			Type IPromptCommandBuilder.GetCommandType() => null!;

			public override string ToString() => $"Root {{ Command = \"{CommandName}\" }}";

			public bool IsValid() => this.Descriptor.Commands.Any(x => x.Token == this.CommandName);

			public bool TryGetCommand(string token, [MaybeNullWhen(false)] out IPromptCommandDescriptor command)
			{
				command = this.Descriptor.Commands.FirstOrDefault(x => x.Token == token);
				return command is not null;
			}

			public RootPromptCommand Descriptor { get; }

			public PromptState Update(PromptState state)
			{
				Contract.Debug.Requires(ReferenceEquals(state.CommandBuilder, this));

				// first we have to detect if we change to the next token, by looking for a space in the token:
				// - "hello_" we are still writing the token (maybe there are more to come)
				// - "hello _" we have written the command, we must switch to it

				if (state.Token.Length > 0 && state.TokenStart == 0 && state.Token[^1] == ' ')
				{
					if (!TryGetCommand(this.CommandName, out var cmd))
					{ // no command with this name!
						return state with
						{
							CommandBuilder = this with
							{
								CommandName = state.Token
							}
						};
					}

					// switch to this command
					return state with
					{
						Change = PromptChange.NextToken,
						TokenStart = state.Text.Length,
						Token = "",
						Command = cmd,
						CommandBuilder = cmd.StartNew(),
					};
				}

				return state with
				{
					CommandBuilder = this with
					{
						CommandName = state.Token,
					}
				};
			}

			public IPromptCommand Build(PromptState state)
			{
				throw new NotSupportedException("No valid command was specified.");
			}

			public void Explain(ExplanationBuilder builder)
			{
				builder.WriteLine($"CommandName: '{this.CommandName}'");
				//TODO!
			}

		}

	}

}
