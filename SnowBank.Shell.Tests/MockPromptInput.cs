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

namespace FoundationDB.Client.Tests
{
	using System.Threading;
	using SnowBank.Shell.Prompt;

	/// <summary>Mock input that simulates keystrokes from an initial list</summary>
	public class MockPromptInput : IPromptInput
	{

		/// <summary>List of keystrokes that will be sent</summary>
		public List<ConsoleKeyInfo> KeyStrokes { get; init; } = [];

		/// <summary>Number of keys that have already been sent</summary>
		public int Position { get; private set; }

		/// <summary>Last key that was sent</summary>
		public ConsoleKeyInfo? LastKey { get; private set; }

		public MockPromptInput Add(ConsoleKeyInfo key)
		{
			this.KeyStrokes.Add(key);
			return this;
		}

		public bool TryReadKey(out ConsoleKeyInfo? key)
		{
			key = this.Position < this.KeyStrokes.Count
				? this.KeyStrokes[this.Position++]
				: null;

			this.LastKey = key;

			return true;
		}

		public Task<ConsoleKeyInfo?> ReadKey(CancellationToken ct)
		{
			ConsoleKeyInfo? key = this.Position < this.KeyStrokes.Count
				? this.KeyStrokes[this.Position++]
				: null;

			this.LastKey = key;

			return Task.FromResult(key);
		}

	}

}
