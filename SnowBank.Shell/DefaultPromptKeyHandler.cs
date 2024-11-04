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

	public class DefaultPromptKeyHandler : IPromptKeyHandler
	{

		/// <inheritdoc />
		public PromptState HandleKeyPress(PromptState state, ConsoleKeyInfo key)
		{
			switch (key.Key)
			{
				case ConsoleKey.Enter:
				{
					return state with
					{
						Text = state.Text.Trim(),
						Token = "",
						TokenStart = state.Text.Length,
						Change = PromptChange.Done,
					};
				}
				case ConsoleKey.Escape:
				{
					return state with
					{
						Text = "",
						Token = "",
						TokenStart = 0,
						Change = PromptChange.Aborted,
					};
				}
				case ConsoleKey.Tab:
				{
					if (state.Token.Length == 0)
					{ // nothing to complete?
						return state with { Change = PromptChange.None }; //TODO: "ding!" ?
					}

					if (state.ExactMatch != null)
					{ // we already have written the word, only add a space!

						if (state.Change == PromptChange.Completed)
						{ // double "TAB" after an auto-complete, equivalent to a space

							//TODO: implemented looping through all the possible candidates
							return state with
							{
								Change = PromptChange.None,
							};
						}

						if (string.IsNullOrEmpty(state.Text))
						{
							return state with { Change = PromptChange.None }; // DING!
						}

						state = state with
						{
							Change = PromptChange.Completed,
							Text = state.Text,
							Token = state.ExactMatch,
						};
						break;
					}

					if (state.Candidates?.Count == 1)
					{ // we have only one candidate, output it
						var c = state.Candidates[0];
						var text = state.TokenStart == 0 ? c : (state.Text![..state.TokenStart] + c);
						state = state with
						{
							Change = PromptChange.Completed,
							Text = text,
							Token = c,
						};
						break;
					}

					if (state.CommonPrefix != null)
					{ // we have a common prefix with all candiates, we can jump forward
						var text = state.Text + state.CommonPrefix[state.Token.Length..];
						state = state with
						{
							Change = PromptChange.Completed,
							Text = text,
							Token = text[state.TokenStart..],
						};
						break;
					}

					return state with { Change = PromptChange.None };
				}
				case ConsoleKey.Backspace:
				{
					if (string.IsNullOrEmpty(state.Text))
					{ // nothing to delete
						return state with { Change = PromptChange.None };
					}

					var text = state.Text[..^1];
					var tokenStart = text.Length == 0 ? 0 : Math.Max(0, text.LastIndexOf(' '));

					if (tokenStart == 0)
					{
						//TODO: go back to the root command!
					}

					state = state with
					{
						Change = PromptChange.BackSpace,
						Text = text,
						TokenStart = tokenStart,
						Token = text[tokenStart..],
					};
					break;
				}
				default:
				{
					char c = key.KeyChar;

					var text = state.Text;
					// simply add the character

					text += c;
					state = state with
					{
						Change = PromptChange.Add,
						Text = text,
						Token = text[state.TokenStart..],
					};

					break;
				}
			}

			return state;
		}
		
	}

}
