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
	using System.Diagnostics.CodeAnalysis;
	using JetBrains.Annotations;

	[UsedImplicitly]
	[SuppressMessage("ReSharper", "InconsistentNaming")]
	[SuppressMessage("ReSharper", "UnusedMember.Global")]
	public static class Keyboard
	{

		public static string GetKeyName(ConsoleKeyInfo key)
		{
			return key.Key switch
			{
				ConsoleKey.Enter => "[ENTER]",
				ConsoleKey.Spacebar => "[SPACE]",
				ConsoleKey.Tab => "[TAB]",
				ConsoleKey.Escape => "[ESC]",
				ConsoleKey.Backspace => "[BACKSPACE]",
				ConsoleKey.Delete => "[DEL]",
				ConsoleKey.LeftArrow => "[<-]",
				ConsoleKey.RightArrow => "[->]",

				_ => "[" + key.KeyChar + "]",
			};
		}

		public static readonly ConsoleKeyInfo Enter = new('\n', ConsoleKey.Enter, false, false, false);

		public static readonly ConsoleKeyInfo Space = new(' ', ConsoleKey.Spacebar, false, false, false);

		public static readonly ConsoleKeyInfo Tab = new('\t', ConsoleKey.Tab, false, false, false);

		public static readonly ConsoleKeyInfo Backspace = new('\b', ConsoleKey.Backspace, false, false, false);

		public static readonly ConsoleKeyInfo Dot = new('.', ConsoleKey.Decimal, false, false, false);

		public static readonly ConsoleKeyInfo Comma = new(',', ConsoleKey.OemComma, false, false, false);

		public static readonly ConsoleKeyInfo Slash = new('/', ConsoleKey.Divide, false, false, false);

		public static readonly ConsoleKeyInfo BackSlash = new('\\', 0, false, false, false);

		public static readonly ConsoleKeyInfo OpenParens = new('(', 0, false, false, false);

		public static readonly ConsoleKeyInfo CloseParens = new(')', 0, false, false, false);

		public static readonly ConsoleKeyInfo Digit0 = new('0', ConsoleKey.D0, false, false, false);

		public static readonly ConsoleKeyInfo Digit1 = new('1', ConsoleKey.D1, false, false, false);

		public static readonly ConsoleKeyInfo Digit2 = new('2', ConsoleKey.D2, false, false, false);

		public static readonly ConsoleKeyInfo Digit3 = new('3', ConsoleKey.D3, false, false, false);

		public static readonly ConsoleKeyInfo Digit4 = new('4', ConsoleKey.D4, false, false, false);

		public static readonly ConsoleKeyInfo Digit5 = new('5', ConsoleKey.D5, false, false, false);

		public static readonly ConsoleKeyInfo Digit6 = new('6', ConsoleKey.D6, false, false, false);

		public static readonly ConsoleKeyInfo Digit7 = new('7', ConsoleKey.D7, false, false, false);

		public static readonly ConsoleKeyInfo Digit8 = new('8', ConsoleKey.D8, false, false, false);

		public static readonly ConsoleKeyInfo Digit9 = new('9', ConsoleKey.D9, false, false, false);

		#region Uppercase

		public static readonly ConsoleKeyInfo A = new('A', ConsoleKey.A, false, false, false);

		public static readonly ConsoleKeyInfo B = new('B', ConsoleKey.B, false, false, false);

		public static readonly ConsoleKeyInfo C = new('C', ConsoleKey.C, false, false, false);

		public static readonly ConsoleKeyInfo D = new('D', ConsoleKey.D, false, false, false);

		public static readonly ConsoleKeyInfo E = new('E', ConsoleKey.E, false, false, false);

		public static readonly ConsoleKeyInfo F = new('F', ConsoleKey.F, false, false, false);

		public static readonly ConsoleKeyInfo G = new('G', ConsoleKey.G, false, false, false);

		public static readonly ConsoleKeyInfo H = new('H', ConsoleKey.H, false, false, false);

		public static readonly ConsoleKeyInfo I = new('I', ConsoleKey.I, false, false, false);

		public static readonly ConsoleKeyInfo J = new('J', ConsoleKey.J, false, false, false);

		public static readonly ConsoleKeyInfo K = new('K', ConsoleKey.K, false, false, false);

		public static readonly ConsoleKeyInfo L = new('L', ConsoleKey.L, false, false, false);

		public static readonly ConsoleKeyInfo M = new('M', ConsoleKey.M, false, false, false);

		public static readonly ConsoleKeyInfo N = new('N', ConsoleKey.N, false, false, false);

		public static readonly ConsoleKeyInfo O = new('O', ConsoleKey.O, false, false, false);

		public static readonly ConsoleKeyInfo P = new('P', ConsoleKey.P, false, false, false);

		public static readonly ConsoleKeyInfo Q = new('Q', ConsoleKey.Q, false, false, false);

		public static readonly ConsoleKeyInfo R = new('R', ConsoleKey.R, false, false, false);

		public static readonly ConsoleKeyInfo S = new('S', ConsoleKey.S, false, false, false);

		public static readonly ConsoleKeyInfo T = new('T', ConsoleKey.T, false, false, false);

		public static readonly ConsoleKeyInfo U = new('U', ConsoleKey.U, false, false, false);

		public static readonly ConsoleKeyInfo V = new('V', ConsoleKey.V, false, false, false);

		public static readonly ConsoleKeyInfo W = new('W', ConsoleKey.W, false, false, false);

		public static readonly ConsoleKeyInfo X = new('X', ConsoleKey.X, false, false, false);

		public static readonly ConsoleKeyInfo Y = new('Y', ConsoleKey.Y, false, false, false);

		public static readonly ConsoleKeyInfo Z = new('Z', ConsoleKey.Z, false, false, false);

		#endregion

		#region Lowercase

		public static readonly ConsoleKeyInfo a = new('a', ConsoleKey.A, false, false, false);

		public static readonly ConsoleKeyInfo b = new('b', ConsoleKey.B, false, false, false);

		public static readonly ConsoleKeyInfo c = new('c', ConsoleKey.C, false, false, false);

		public static readonly ConsoleKeyInfo d = new('d', ConsoleKey.D, false, false, false);

		public static readonly ConsoleKeyInfo e = new('e', ConsoleKey.E, false, false, false);

		public static readonly ConsoleKeyInfo f = new('f', ConsoleKey.F, false, false, false);

		public static readonly ConsoleKeyInfo g = new('g', ConsoleKey.G, false, false, false);

		public static readonly ConsoleKeyInfo h = new('h', ConsoleKey.H, false, false, false);

		public static readonly ConsoleKeyInfo i = new('i', ConsoleKey.I, false, false, false);

		public static readonly ConsoleKeyInfo j = new('j', ConsoleKey.J, false, false, false);

		public static readonly ConsoleKeyInfo k = new('k', ConsoleKey.K, false, false, false);

		public static readonly ConsoleKeyInfo l = new('l', ConsoleKey.L, false, false, false);

		public static readonly ConsoleKeyInfo m = new('m', ConsoleKey.M, false, false, false);

		public static readonly ConsoleKeyInfo n = new('n', ConsoleKey.N, false, false, false);

		public static readonly ConsoleKeyInfo o = new('o', ConsoleKey.O, false, false, false);

		public static readonly ConsoleKeyInfo p = new('p', ConsoleKey.P, false, false, false);

		public static readonly ConsoleKeyInfo q = new('q', ConsoleKey.Q, false, false, false);

		public static readonly ConsoleKeyInfo r = new('r', ConsoleKey.R, false, false, false);

		public static readonly ConsoleKeyInfo s = new('s', ConsoleKey.S, false, false, false);

		public static readonly ConsoleKeyInfo t = new('t', ConsoleKey.T, false, false, false);

		public static readonly ConsoleKeyInfo u = new('u', ConsoleKey.U, false, false, false);

		public static readonly ConsoleKeyInfo v = new('v', ConsoleKey.V, false, false, false);

		public static readonly ConsoleKeyInfo w = new('w', ConsoleKey.W, false, false, false);

		public static readonly ConsoleKeyInfo x = new('x', ConsoleKey.X, false, false, false);

		public static readonly ConsoleKeyInfo y = new('y', ConsoleKey.Y, false, false, false);

		public static readonly ConsoleKeyInfo z = new('z', ConsoleKey.Z, false, false, false);

		#endregion

	}

}
