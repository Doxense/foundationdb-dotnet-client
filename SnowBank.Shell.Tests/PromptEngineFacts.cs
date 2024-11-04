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

// ReSharper disable StringLiteralTypo
namespace FoundationDB.Client.Tests
{
	using System;
	using System.Collections.Generic;
	using System.Diagnostics.CodeAnalysis;
	using JetBrains.Annotations;
	using NUnit.Framework;
	using SnowBank.Shell.Prompt;
	using SnowBank.Testing;

	[TestFixture]
	public class PromptEngineFacts : SimpleTest
	{

		public sealed record FakeHello : PromptCommandDescriptor
		{
			public override string Token => "hello";

			public override string Description => "Hello, there!";

			public override string SyntaxHint => "hello <word>";

			public override IPromptCommandBuilder StartNew() => new Builder() { Argument = "" };

			public override void GetTokens(List<string> candidates, string command, string tok)
			{
				ReadOnlySpan<string> words = ["world", "there"];
				foreach (var w in words)
				{
					if (w.StartsWith(tok)) candidates.Add(w);
				}
			}

			public override string ToString() => this.SyntaxHint;


			public sealed class Command : PromptCommand<FakeHello>
			{
				public Command(FakeHello descriptor, string commandText)
					: base(descriptor, commandText)
				{ }

				public string Argument { get; init; }

			}

			public sealed record Builder : PromptCommandBuilder<FakeHello, FakeHello.Command>
			{

				public required string Argument { get; init; }

				public override bool IsValid() => !string.IsNullOrWhiteSpace(this.Argument);

				public override PromptState Update(PromptState state)
				{
					return state with { CommandBuilder = new Builder { Argument = state.Token } };
				}

				public override Command Build(PromptState state)
				{
					if (!IsValid())
					{
						throw new InvalidOperationException("Argument is missing");
					}

					return new((FakeHello) state.Command, state.Text) { Argument = this.Argument };
				}
			}

		}

		public sealed record FakeHelp : PromptCommandDescriptor
		{
			public override string Token => "help";

			public override string Description => "Help me, Obiwan Kenobi!";

			public override string SyntaxHint => "help <command>";

			public override IPromptCommandBuilder StartNew()
			{
				return new Builder() { CommandName = "" };
			}

			public override void GetTokens(List<string> candidates, string command, string tok)
			{
				ReadOnlySpan<string> cmds = ["cd", "dir", "help", "exit", "dir"];
				foreach (var cmd in cmds)
				{
					if (cmd.StartsWith(tok)) candidates.Add(cmd);
				}
			}

			public override string ToString() => this.SyntaxHint;

			public sealed class Command : PromptCommand<FakeHelp>
			{
				public Command(FakeHelp descriptor, string commandText)
					: base(descriptor, commandText)
				{ }

				public required string CommandName { get; init; }

			}

			public sealed record Builder : PromptCommandBuilder<FakeHelp, FakeHelp.Command>
			{

				public required string CommandName { get; init; }

				public override bool IsValid() => !string.IsNullOrWhiteSpace(this.CommandName);

				public override PromptState Update(PromptState state) => throw new NotImplementedException();

				public override Command Build(PromptState state) => new Command((FakeHelp) state.Command, state.Text) { CommandName = this.CommandName, };

			}

		}

		private static void DumpState(PromptState? state, string? label = null)
		{
			Log($"# {(label ?? "State")}:");
			if (state is null)
			{
				Log("<null>");
			}
			else
			{
				Log(state.Explain());
			}
		}

		[DebuggerNonUserCode]
		private static void VerifyState(PromptState state, PromptState expected, string label)
		{
			var message = "Expected state does not match for step: " + label;

			Assert.That(state.Change, Is.EqualTo(expected.Change), message);
			Assert.That(state.Text, Is.EqualTo(expected.Text), message);
			Assert.That(state.Token, Is.EqualTo(expected.Token), message);
			Assert.That(state.TokenStart, Is.EqualTo(expected.TokenStart), message);
			Assert.That(state.Command, Is.SameAs(expected.Command), message);
			Assert.That(state.CommandBuilder, Is.Not.Null, message);

			if (expected.Candidates is null)
			{
				Assert.That(state.Candidates, Is.Null.Or.Empty, message);
			}
			else
			{
				// REVIEW: does order matter?
				Assert.That(state.Candidates, Is.EquivalentTo(expected.Candidates), message);
			}

			Assert.That(state.ExactMatch, Is.EqualTo(expected.ExactMatch), message);
			Assert.That(state.CommonPrefix, Is.EqualTo(expected.CommonPrefix), message);
		}

		[UsedImplicitly]
		[SuppressMessage("ReSharper", "InconsistentNaming")]
		[SuppressMessage("ReSharper", "UnusedMember.Global")]
		public static class Keyboard
		{

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

		public static string KeyName(ConsoleKeyInfo key)
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

		private static PromptState Run(IPromptKeyHandler keyHandler, IPromptAutoCompleter autoCompleter, PromptState initial, ReadOnlySpan<(ConsoleKeyInfo Key, PromptStateExpression Expected)> steps)
		{
			var state = initial;

			for (int i = 0; i < steps.Length; i++)
			{
				var key = steps[i].Key;
				var expr = steps[i].Expected;

				var label = $"'{state.Text}' + {KeyName(key)} => '{expr.State.Text}'";

				Log($"# {label}");

				state = keyHandler.HandleKeyPress(state, key);
				state = state.CommandBuilder.Update(state);
				state = autoCompleter.HandleAutoComplete(state);
				//DumpState(state);
				try
				{
					VerifyState(state, expr.State, label);
				}
				catch (AssertionException)
				{
					DumpState(expr.State, "Expected");
					DumpState(state, "Actual");
					throw;
				}
			}

			return state;
		}

		[Test]
		public void Test_Can_Type_Hello_Command()
		{
			// Fully type the "hello world" command, character by character
			// - The shell has two commands "hello" and "help", which have common prefix "hel".
			// - The "hello" command has 2 possible arguments "world" and "there"
			// - Up to 'hel_', we should have both commands proposed.
			// - From 'hell_' to 'hello_' we should only see the hello command proposed.
			// - At 'hello _' we should see "world" and "there" proposed
			// - After 'hello w_' we should only have "world" proposed
			// - ENTER should generate the HelloCommand with parameter "world"

			var hello = new FakeHello();
			var help = new FakeHelp();
			var root = new RootPromptCommand() { Commands = [hello, help], };
			var keyHandler = new DefaultPromptKeyHandler();
			var autoComplete = new DefaultPromptAutoCompleter() { Root = root };

			Run(
				keyHandler,
				autoComplete,
				PromptState.CreateEmpty(root),
				[
					(Keyboard.h, PromptStateExpression.For(root).Add().Text("h", "h").Candidates(["hello", "help"], commonPrefix: "hel")),
					(Keyboard.e, PromptStateExpression.For(root).Add().Text("he", "he").Candidates(["hello", "help"], commonPrefix: "hel")),
					(Keyboard.l, PromptStateExpression.For(root).Add().Text("hel", "hel").Candidates(["hello", "help"], commonPrefix: "hel")),
					(Keyboard.l, PromptStateExpression.For(root).Add().Text("hell", "hell").Candidates(["hello"], commonPrefix: "hello")),
					(Keyboard.o, PromptStateExpression.For(root).Add().Text("hello", "hello").Candidates(["hello"], exactMatch: "hello")),
					(Keyboard.Space, PromptStateExpression.For(hello).Space().Text("hello ", "", start: 6).Candidates(["world", "there"])),
					(Keyboard.w, PromptStateExpression.For(hello).Add().Text("hello w", "w", start: 6).Candidates(["world"], commonPrefix: "world")),
					(Keyboard.o, PromptStateExpression.For(hello).Add().Text("hello wo", "wo", start: 6).Candidates(["world"], commonPrefix: "world")),
					(Keyboard.r, PromptStateExpression.For(hello).Add().Text("hello wor", "wor", start: 6).Candidates(["world"], commonPrefix: "world")),
					(Keyboard.l, PromptStateExpression.For(hello).Add().Text("hello worl", "worl", start: 6).Candidates(["world"], commonPrefix: "world")),
					(Keyboard.d, PromptStateExpression.For(hello).Add().Text("hello world", "world", start: 6).Candidates(["world"], exactMatch: "world")),
					(Keyboard.Enter, PromptStateExpression.For(hello).Done().Text("hello world", "", start: 11)),
				]
			);
		}

		[Test]
		public void Test_Can_Type_Hello_World_With_AutoComplete_Shortcut()
		{
			// Type "hell[TAB]w[TAB]" which should produce the "hello world" via autocompletion.
			// - The shell has two commands "hello" and "help", which have common prefix "hel".
			// - The "hello" command has 2 possible arguments "world" and "there"
			// - Up to 'hel_', we should have 2 possible candidates
			// - At "hell_", there should be an exact match for "hello"
			// - The first TAB should autocomplete to "hello_"
			// - The next key 'w' should auto-insert a space to produce "hello w_"
			// - We should now have an exact match for "word"
			// - The second TAB should autocomplete to "hello world_"
			// - ENTER should generate the HelloCommand with parameter "world"

			var hello = new FakeHello();
			var help = new FakeHelp();
			var root = new RootPromptCommand() { Commands = [hello, help], };
			var keyHandler = new DefaultPromptKeyHandler();
			var autoComplete = new DefaultPromptAutoCompleter() { Root = root };

			Run(
				keyHandler,
				autoComplete,
				PromptState.CreateEmpty(root),
				[
					(Keyboard.h, PromptStateExpression.For(root).Add().Text("h", "h").Candidates(["hello", "help"], commonPrefix: "hel")),
					(Keyboard.e, PromptStateExpression.For(root).Add().Text("he", "he").Candidates(["hello", "help"], commonPrefix: "hel")),
					(Keyboard.l, PromptStateExpression.For(root).Add().Text("hel", "hel").Candidates(["hello", "help"], commonPrefix: "hel")),
					(Keyboard.l, PromptStateExpression.For(root).Add().Text("hell", "hell").Candidates(["hello"], commonPrefix: "hello")),
					(Keyboard.Tab, PromptStateExpression.For(root).Completed().Text("hello", "hello").Candidates(["hello"], exactMatch: "hello")),
					(Keyboard.Space, PromptStateExpression.For(hello).Space().Text("hello ", "", start: 6).Candidates(["world", "there"])),
					(Keyboard.w, PromptStateExpression.For(hello).Add().Text("hello w", "w", start: 6).Candidates(["world"], commonPrefix: "world")),
					(Keyboard.Tab, PromptStateExpression.For(hello).Completed().Text("hello world", "world", start: 6).Candidates(["world"], exactMatch: "world")),
					(Keyboard.Enter, PromptStateExpression.For(hello).Done().Text("hello world", "")),
				]
			);

		}

		public sealed record FakeQuery : PromptCommandDescriptor
		{
			public override string Token => "query";

			public override string Description => "Executes a query in the database";

			public override string SyntaxHint => "query <fql>";

			public override IPromptCommandBuilder StartNew() => new Builder();

			public override void GetTokens(List<string> candidates, string command, string tok)
			{ }

			public override string ToString() => this.SyntaxHint;

			public sealed class Command : PromptCommand<FakeQuery>
			{
				public Command(FakeQuery descriptor, string commandText)
					: base(descriptor, commandText)
				{ }

				public required FqlQuery Query { get; init; }

			}

			public sealed record Builder : PromptCommandBuilder<FakeQuery, FakeQuery.Command>
			{

				public FqlQuery? Query { get; init; }

				public Exception? Error { get; init; }

				public override bool IsValid() => this.Query != null;

				public override string ToString() => this.Query != null ? this.Query.ToString() : this.Error != null ? $"ERR: {this.Error.Message}" : "<none>";

				public override PromptState Update(PromptState state)
				{
					if (state.Change is (PromptChange.Done or PromptChange.Aborted))
					{
						return state;
					}

					if (!FqlQueryParser.ParseNext(state.Token, out _).Check(out var query, out var error))
					{ // incomplete or invalid query
						return state with { CommandBuilder = new Builder { Error = error.Error } };
					}

					return state with { CommandBuilder = new Builder { Query = query } };
				}

				public override Command Build(PromptState state)
				{
					if (this.Query == null)
					{
						throw new InvalidOperationException("Invalid FQL query", this.Error);
					}

					return new((FakeQuery) state.Command, state.Text) { Query = this.Query };
				}
			}

		}

		[Test]
		public void Test_Can_Type_FqlQuery()
		{
			var query = new FakeQuery();
			var root = new RootPromptCommand() { Commands = [query], };
			var keyHandler = new DefaultPromptKeyHandler();
			var autoComplete = new DefaultPromptAutoCompleter() { Root = root };

			var state = Run(
				keyHandler,
				autoComplete,
				PromptState.CreateEmpty(root),
				[
					(Keyboard.q, PromptStateExpression.For(root).Add().Text("q", "q").Candidates(["query"], commonPrefix: "query")),
					(Keyboard.Tab, PromptStateExpression.For(root).Completed().Text("query", "query").Candidates(["query"], exactMatch: "query")),
					(Keyboard.Space, PromptStateExpression.For(query).Space().Text("query ", "")),
					(Keyboard.Slash, PromptStateExpression.For(query).Add().Text("query /", "/", start: 6)),
					(Keyboard.a, PromptStateExpression.For(query).Add().Text("query /a", "/a", start: 6)),
					(Keyboard.b, PromptStateExpression.For(query).Add().Text("query /ab", "/ab", start: 6)),
					(Keyboard.c, PromptStateExpression.For(query).Add().Text("query /abc", "/abc", start: 6)),
					(Keyboard.OpenParens, PromptStateExpression.For(query).Add().Text("query /abc(", "/abc(", start: 6)),
					(Keyboard.Digit1, PromptStateExpression.For(query).Add().Text("query /abc(1", "/abc(1", start: 6)),
					(Keyboard.Comma, PromptStateExpression.For(query).Add().Text("query /abc(1,", "/abc(1,", start: 6)),
					(Keyboard.Dot, PromptStateExpression.For(query).Add().Text("query /abc(1,.", "/abc(1,.", start: 6)),
					(Keyboard.Dot, PromptStateExpression.For(query).Add().Text("query /abc(1,..", "/abc(1,..", start: 6)),
					(Keyboard.Dot, PromptStateExpression.For(query).Add().Text("query /abc(1,...", "/abc(1,...", start: 6)),
					(Keyboard.CloseParens, PromptStateExpression.For(query).Add().Text("query /abc(1,...)", "/abc(1,...)", start: 6)),
					(Keyboard.Enter, PromptStateExpression.For(query).Done().Text("query /abc(1,...)", "")),
				]
			);

			Assert.That(state.Command, Is.SameAs(query));
			Assert.That(state.CommandBuilder, Is.InstanceOf<FakeQuery.Builder>());
			var result = state.CommandBuilder.Build(state);
			Assert.That(result, Is.InstanceOf<FakeQuery.Command>());

			var cmd = (FakeQuery.Command) result;
			Assert.That(cmd.Query, Is.Not.Null);
			Log(cmd.Query.Explain());
			Assert.That(cmd.Query.Directory, Is.EqualTo(FqlDirectoryExpression.Create().AddRoot().Add("abc")));
			Assert.That(cmd.Query.Tuple, Is.EqualTo(FqlTupleExpression.Create().AddIntConst(1).AddMaybeMore()));
		}

	}

}
