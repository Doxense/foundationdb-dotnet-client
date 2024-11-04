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
namespace SnowBank.Shell.Prompt.Tests
{
	using System;
	using System.Collections.Generic;
	using System.Threading.Tasks;
	using Doxense.Serialization;
	using FoundationDB.Client;
	using FoundationDB.Client.Tests;
	using NUnit.Framework;
	using SnowBank.Testing;

	[TestFixture]
	[Category("Core-SDK")]
	[Parallelizable(ParallelScope.All)]
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

				public override string ToString() => $"Hello {{ Argument = \"{Argument}\" }}";

				public override PromptState Update(PromptState state)
				{
					if (state.IsDone())
					{
						return state;
					}

					return state with { CommandBuilder = new Builder { Argument = state.Token } };
				}

				public override Command Build(PromptState state, FakeHello descriptor)
				{
					if (!IsValid())
					{
						throw new InvalidOperationException("Argument is missing");
					}

					return new(descriptor, state.Text) { Argument = this.Argument };
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

				public override string ToString() => $"Help {{ CommandName = \"{CommandName}\" }}";

				public override PromptState Update(PromptState state)
				{
					if (state.IsDone())
					{
						return state;
					}

					return state with { CommandBuilder = new Builder { CommandName = state.Token } };
				}

				public override Command Build(PromptState state, FakeHelp descriptor)
				{
					if (!IsValid())
					{
						throw new InvalidOperationException("Argument is missing");
					}

					return new(descriptor, state.Text) { CommandName = this.CommandName, };
				}

			}

		}

		private static void DumpState(PromptState? state, string? label = null)
		{
			if (label != null)
			{
				Log($"# {label}:");
			}

			if (state is null)
			{
				Log("# <null>");
			}
			else
			{
				Log(state.Explain(prefix: "# ").Trim());
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

		private static PromptStateExpression<TCommand> Expr<TCommand>(TCommand command) where TCommand : IPromptCommandDescriptor => PromptStateExpression.For<TCommand>(command);

		/// <summary>Sends keystrokes to a mock prompt, checking the state at every step</summary>
		/// <param name="keyHandler"></param>
		/// <param name="autoCompleter"></param>
		/// <param name="initial"></param>
		/// <param name="steps"></param>
		/// <returns>Final state, after the last keystroke</returns>
		private async Task<PromptState> Run(IPromptKeyHandler keyHandler, IPromptAutoCompleter autoCompleter, PromptState initial, (ConsoleKeyInfo Key, PromptStateExpression Expected)[] steps)
		{
			var input = new MockPromptInput();
			foreach (var step in steps)
			{
				input.KeyStrokes.Add(step.Key);
			}

			var theme = new MockPromptTheme()
			{
				Prompt = "fake> ",
				MaxRows = 5,
			};
			var renderer = new MockPromptRenderer()
			{

			};

			PromptState lastState = initial;
			int index = 0;

			var engine = new PromptEngine()
			{
				Input = input,
				KeyHandler = keyHandler,
				AutoCompleter = autoCompleter,
				Theme = theme,
				Renderer = renderer,

				OnBefore = (state, renderState) =>
				{
					Assert.That(input.Position, Is.EqualTo(index));
					var step = steps[index];

					Log($"[{(index + 1):N0}/{steps.Length:N0}]: '{state.Text}' + {Keyboard.GetKeyName(step.Key)} => '{step.Expected.State.Text}'");
				},

				OnAfter = (key, state, renderState) =>
				{
					Assert.That(input.Position, Is.EqualTo(index + 1));
					var step = steps[index];
					try
					{
						VerifyState(state, step.Expected.State, $"'{state.Text}' + {Keyboard.GetKeyName(step.Key)} => '{step.Expected.State.Text}'");
					}
					catch (AssertionException)
					{
						DumpState(step.Expected.State, "Expected");
						DumpState(state, "Actual");
						throw;
					}

					DumpState(state);
					Log();

					lastState = state;
					++index;
				},
			};

			await engine.Prompt(this.Cancellation);

			return lastState;
		}

		[Test]
		public async Task Test_Can_Type_Hello_Command()
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

			var state = await Run(
				keyHandler,
				autoComplete,
				PromptState.CreateEmpty(root),
				[
					(Keyboard.h,     Expr(root).Add().Text("h", "h").Candidates(["hello", "help"], commonPrefix: "hel")),
					(Keyboard.e,     Expr(root).Add().Text("he", "he").Candidates(["hello", "help"], commonPrefix: "hel")),
					(Keyboard.l,     Expr(root).Add().Text("hel", "hel").Candidates(["hello", "help"], commonPrefix: "hel")),
					(Keyboard.l,     Expr(root).Add().Text("hell", "hell").Candidates(["hello"], commonPrefix: "hello")),
					(Keyboard.o,     Expr(root).Add().Text("hello", "hello").Candidates(["hello"], exactMatch: "hello")),
					(Keyboard.Space, Expr(hello).Token().Text("hello ", "", start: 6).Candidates(["world", "there"])),
					(Keyboard.w,     Expr(hello).Add().Text("hello w", "w", start: 6).Candidates(["world"], commonPrefix: "world")),
					(Keyboard.o,     Expr(hello).Add().Text("hello wo", "wo", start: 6).Candidates(["world"], commonPrefix: "world")),
					(Keyboard.r,     Expr(hello).Add().Text("hello wor", "wor", start: 6).Candidates(["world"], commonPrefix: "world")),
					(Keyboard.l,     Expr(hello).Add().Text("hello worl", "worl", start: 6).Candidates(["world"], commonPrefix: "world")),
					(Keyboard.d,     Expr(hello).Add().Text("hello world", "world", start: 6).Candidates(["world"], exactMatch: "world")),
					(Keyboard.Enter, Expr(hello).Done().Text("hello world", "", start: 11)),
				]
			);

			// create the query command that should contain the parsed FqlQuery 
			Log($"Command: {state.Command.GetType()?.GetFriendlyName()}");
			Assert.That(state.Command, Is.SameAs(hello));
			Assert.That(state.CommandBuilder, Is.InstanceOf<FakeHello.Builder>());
			var result = state.CommandBuilder.Build(state);
			Assert.That(result, Is.InstanceOf<FakeHello.Command>());

			// verify the argument
			var cmd = (FakeHello.Command) result;
			Assert.That(cmd.Argument, Is.EqualTo("world"));
		}

		[Test]
		public async Task Test_Can_Type_Hello_World_With_AutoComplete_Shortcut()
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

			var state = await Run(
				keyHandler,
				autoComplete,
				PromptState.CreateEmpty(root),
				[
					(Keyboard.h,     Expr(root).Add().Text("h", "h").Candidates(["hello", "help"], commonPrefix: "hel")),
					(Keyboard.e,     Expr(root).Add().Text("he", "he").Candidates(["hello", "help"], commonPrefix: "hel")),
					(Keyboard.l,     Expr(root).Add().Text("hel", "hel").Candidates(["hello", "help"], commonPrefix: "hel")),
					(Keyboard.l,     Expr(root).Add().Text("hell", "hell").Candidates(["hello"], commonPrefix: "hello")),
					(Keyboard.Tab,   Expr(root).Completed().Text("hello", "hello").Candidates(["hello"], exactMatch: "hello")),
					(Keyboard.Space, Expr(hello).Token().Text("hello ", "", start: 6).Candidates(["world", "there"])),
					(Keyboard.w,     Expr(hello).Add().Text("hello w", "w", start: 6).Candidates(["world"], commonPrefix: "world")),
					(Keyboard.Tab,   Expr(hello).Completed().Text("hello world", "world", start: 6).Candidates(["world"], exactMatch: "world")),
					(Keyboard.Enter, Expr(hello).Done().Text("hello world", "")),
				]
			);

			// create the query command that should contain the parsed FqlQuery 
			Log($"Command: {state.Command.GetType()?.GetFriendlyName()}");
			Assert.That(state.Command, Is.SameAs(hello));
			Assert.That(state.CommandBuilder, Is.InstanceOf<FakeHello.Builder>());
			var result = state.CommandBuilder.Build(state);
			Assert.That(result, Is.InstanceOf<FakeHello.Command>());

			// verify the argument
			var cmd = (FakeHello.Command) result;
			Assert.That(cmd.Argument, Is.EqualTo("world"));
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

				public override string ToString() => this.Query != null
					? $"Query {{ Expr = {this.Query} }}"
					: $"Query {{ Error = {this.Error?.Message} }}";

				public override PromptState Update(PromptState state)
				{
					if (state.IsDone())
					{
						return state;
					}

					if (!FqlQueryParser.ParseNext(state.Token, out _).Check(out var query, out var error))
					{ // incomplete or invalid query
						return state with { CommandBuilder = new Builder { Error = error.Error } };
					}

					return state with { CommandBuilder = new Builder { Query = query } };
				}

				public override Command Build(PromptState state, FakeQuery descriptor)
				{
					if (this.Query == null)
					{
						throw new InvalidOperationException("Invalid FQL query", this.Error);
					}

					return new(descriptor, state.Text) { Query = this.Query };
				}

			}

		}

		[Test]
		public async Task Test_Can_Type_FqlQuery()
		{
			var query = new FakeQuery();
			var root = new RootPromptCommand() { Commands = [query], };
			var keyHandler = new DefaultPromptKeyHandler();
			var autoComplete = new DefaultPromptAutoCompleter() { Root = root };

			Log("Typing...");

			var state = await Run(
				keyHandler,
				autoComplete,
				PromptState.CreateEmpty(root),
				[
					(Keyboard.q,           Expr(root).Add().Text("q", "q").Candidates(["query"], commonPrefix: "query")),
					(Keyboard.Tab,         Expr(root).Completed().Text("query", "query").Candidates(["query"], exactMatch: "query")),
					(Keyboard.Space,       Expr(query).Token().Text("query ", "")),
					(Keyboard.Slash,       Expr(query).Add().Text("query /", "/")),
					(Keyboard.a,           Expr(query).Add().Text("query /a", "/a")),
					(Keyboard.b,           Expr(query).Add().Text("query /ab", "/ab")),
					(Keyboard.c,           Expr(query).Add().Text("query /abc", "/abc")),
					(Keyboard.OpenParens,  Expr(query).Add().Text("query /abc(", "/abc(")),
					(Keyboard.Digit1,      Expr(query).Add().Text("query /abc(1", "/abc(1")),
					(Keyboard.Comma,       Expr(query).Add().Text("query /abc(1,", "/abc(1,")),
					(Keyboard.Dot,         Expr(query).Add().Text("query /abc(1,.", "/abc(1,.")),
					(Keyboard.Dot,         Expr(query).Add().Text("query /abc(1,..", "/abc(1,..")),
					(Keyboard.Dot,         Expr(query).Add().Text("query /abc(1,...", "/abc(1,...")),
					(Keyboard.CloseParens, Expr(query).Add().Text("query /abc(1,...)", "/abc(1,...)")),
					(Keyboard.Enter,       Expr(query).Done().Text("query /abc(1,...)", "")),
				]
			);

			// create the query command that should contain the parsed FqlQuery 
			Log($"Command: {state.Command.GetType().GetFriendlyName()}");
			Assert.That(state.Command, Is.SameAs(query));
			Assert.That(state.CommandBuilder, Is.InstanceOf<FakeQuery.Builder>());
			var result = state.CommandBuilder.Build(state);
			Assert.That(result, Is.InstanceOf<FakeQuery.Command>());

			// verify the query
			var cmd = (FakeQuery.Command) result;
			Assert.That(cmd.Query, Is.Not.Null);
			Log(cmd.Query.Explain(prefix: "# "));
			Assert.That(cmd.Query.Directory, Is.EqualTo(FqlDirectoryExpression.Create().AddRoot().Add("abc")));
			Assert.That(cmd.Query.Tuple, Is.EqualTo(FqlTupleExpression.Create().AddIntConst(1).AddMaybeMore()));
			Log($"Markup: {FqlSyntaxHighlighter.GetMarkup(cmd.Query)}");
		}

	}

}
