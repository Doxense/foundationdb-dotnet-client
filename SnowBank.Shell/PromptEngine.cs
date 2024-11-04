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
	using System;

	public class PromptEngine
	{

		public required IPromptKeyHandler KeyHandler { get; init; }

		public required IPromptTheme Theme { get; init; }

		public required IPromptRenderer Renderer { get; init; }

		public required IPromptAutoCompleter AutoCompleter { get; init; }

		public RenderState? State { get; private set; }

		public async Task<string?> Prompt(CancellationToken ct)
		{
			// this is the complete text, (used to render the prompt up to the cursor)

			// this contains the last "token" that is currently being typed,
			// example: "hello, th_" => the token is "th"

			var root = this.AutoCompleter.Root;

			var state = PromptState.CreateEmpty(root);

			var renderState = this.Theme.Paint(state);

			// initial render
			this.Renderer.Render(renderState, null);
			// record the new state
			this.State = renderState;

			while (!ct.IsCancellationRequested && state.Change is not (PromptChange.Done or PromptChange.Aborted))
			{
				// Read the next key
				if (!Console.KeyAvailable)
				{
					await Task.Delay(15, ct);
					continue;
				}

				var key = Console.ReadKey(intercept: true);

				// update the state with this key
				var newState = this.KeyHandler.HandleKeyPress(state, key);

				// if the text changed, we need to update the auto-complete information
				if (newState.Text != state.Text)
				{
					newState = newState.CommandBuilder.Update(newState);
					newState = this.AutoCompleter.HandleAutoComplete(newState);
				}

				// if the state has changed in any way, we need to repaint
				if (!ReferenceEquals(state, newState))
				{
					state = newState;
					var newRenderState = this.Theme.Paint(newState);

					// if the changed produced actual UI change, we need to render it
					this.Renderer.Render(newRenderState, renderState);
					renderState = newRenderState;
				}
			}

			return state.Text;
		}

	}

}
