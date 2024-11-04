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
	using Spectre.Console;
	using Console = System.Console;

	public class AnsiConsolePromptRenderer : IPromptRenderer
	{

		private static readonly string[] CachedClearMasks = GenerateClearMasks();

		private static string[] GenerateClearMasks()
		{
			var res = new string[10];
			for (int i = 0; i < res.Length; i++)
			{
				res[i] = new string(' ', i);
			}
			return res;
		}

		private string GetClearMask(int count) => (uint) count < AnsiConsolePromptRenderer.CachedClearMasks.Length ? AnsiConsolePromptRenderer.CachedClearMasks[count] : new string(' ', count);

		public void Render(RenderState state, RenderState? prev)
		{
			// hide the cursor while we are moving around during repaint
			Console.CursorVisible = false;

			int skippedRows = 0;

			#region Main Prompt Line:

			// render the prompt
			Console.CursorLeft = 0;
			AnsiConsole.Markup(state.PromptMarkup);

			// render the user input
			int cursorStart = Console.CursorLeft;
			AnsiConsole.Markup(state.TextMarkup);

			int removed = prev == null ? 0 : (prev.TextRaw.Length + prev.Extra) - (state.TextRaw.Length + state.Extra);
			if (removed > 0)
			{
				Console.Write(this.GetClearMask(removed));
			}

			#endregion

			#region Extra Rows...

			//TODO: clear any difference!

			for (int i = 0; i < state.Rows.Count; i++)
			{
				var row = state.Rows[i];
				Console.WriteLine();
				++skippedRows;
				AnsiConsole.Markup(row.Markup);
				removed = prev == null || i >= prev.Rows.Count ? 0 : prev.Rows[i].Raw.Length - row.Raw.Length;
				if (removed > 0)
				{
					Console.Write(this.GetClearMask(removed));
				}
			}

			// clear out any rows from the previous state that don't exist anymore
			if (prev != null)
			{
				for (int i = state.Rows.Count; i < prev.Rows.Count; i++)
				{
					if (prev.Rows[i].Raw.Length > 0)
					{
						Console.WriteLine();
						++skippedRows;
						Console.Write(this.GetClearMask(prev.Rows[i].Raw.Length));
					}
				}
			}

			#endregion

			// now go back up to the main prompt line
			if (skippedRows > 0)
			{
				Console.CursorTop -= skippedRows;
			}

			// and set the cursor to the expected position
			Console.CursorLeft = cursorStart + state.Cursor;
			Console.CursorVisible = true;

		}

	}

}
