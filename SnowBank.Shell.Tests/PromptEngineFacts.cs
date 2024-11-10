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
	using Doxense.Diagnostics.Contracts;
	using Doxense.Serialization;
	using Doxense.Serialization.Json;
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
						return state with
						{
							Tokens = [ ..state.Tokens, PromptToken.Create("argument", this.Argument), ],
							Token = default,
							RawToken = "",
						};
					}

					bool isKnown = state.RawToken is ("world" or "there");

					return state with
					{
						Token = PromptToken.Create(isKnown ? "argument" : "incomplete", state.RawToken),
						CommandBuilder = new Builder { Argument = state.RawToken },
					};
				}

				public override Command Build(PromptState state, FakeHello descriptor)
				{
					if (!IsValid())
					{
						throw new InvalidOperationException("Argument is missing");
					}

					return new(descriptor, state.RawText) { Argument = this.Argument };
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

					return state with { CommandBuilder = new Builder { CommandName = state.RawToken } };
				}

				public override Command Build(PromptState state, FakeHelp descriptor)
				{
					if (!IsValid())
					{
						throw new InvalidOperationException("Argument is missing");
					}

					return new(descriptor, state.RawText) { CommandName = this.CommandName, };
				}

			}

		}

		private static void DumpState(PromptState? state)
		{
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
			Assert.That(state.RawText, Is.EqualTo(expected.RawText), message);
			Assert.That(state.RawToken, Is.EqualTo(expected.RawToken), message);
			Assert.That(state.Tokens.ToArray(), Is.EqualTo(expected.Tokens.ToArray()), message);
			Assert.That(state.Token, Is.EqualTo(expected.Token), message);
			Assert.That(state.Command, Is.SameAs(expected.Command), message);
			Assert.That(state.CommandBuilder, Is.Not.Null, message);

			Assert.That(expected.Candidates, Is.Not.Null);
			if (expected.Candidates.Length == 0)
			{
				Assert.That(state.Candidates, Is.Empty, message);
			}
			else
			{
				// REVIEW: does order matter?
				Assert.That(state.Candidates, Is.EquivalentTo(expected.Candidates), message);
			}

			Assert.That(state.ExactMatch, Is.EqualTo(expected.ExactMatch), message);
			Assert.That(state.CommonPrefix, Is.EqualTo(expected.CommonPrefix), message);
		}

		private static PromptStateExpression<TCommand> Expr<TCommand>(IPromptTheme theme, TCommand command) where TCommand : IPromptCommandDescriptor
			=> PromptStateExpression.For<TCommand>(command, theme);

		/// <summary>Sends keystrokes to a mock prompt, checking the state at every step</summary>
		/// <param name="keyHandler"></param>
		/// <param name="autoCompleter"></param>
		/// <param name="initial"></param>
		/// <param name="steps"></param>
		/// <returns>Final state, after the last keystroke</returns>
		private async Task<PromptState> Run(IPromptKeyHandler keyHandler, IPromptAutoCompleter autoCompleter, IPromptTheme theme, PromptState initial, (ConsoleKeyInfo Key, PromptStateExpression Expected)[] steps)
		{
			var input = new MockPromptInput();
			foreach (var step in steps)
			{
				input.KeyStrokes.Add(step.Key);
			}
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

					Log($"[{(index + 1):N0}/{steps.Length:N0}]: '{state.RawText}' + {Keyboard.GetKeyName(step.Key)} => '{step.Expected.State.RawText}'");
				},

				OnAfter = (key, state, renderState) =>
				{
					Assert.That(input.Position, Is.EqualTo(index + 1));
					var step = steps[index];
					try
					{
						VerifyState(state, step.Expected.State, $"'{state.RawText}' + {Keyboard.GetKeyName(step.Key)} => '{step.Expected.State.RawText}'");
					}
					catch (AssertionException)
					{
						Log();
						Log("Actual State:");
						DumpState(state);
						Log();
						Log("Expected State:");
						DumpState(step.Expected.State);
						throw;
					}

					DumpState(state);
					Log();

					lastState = state;
					++index;
				},

				OnBeforeRender = (state, renderState) =>
				{
					Log($"# Markup: {renderState.TextMarkup}");
					return renderState;
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
			var theme = new AnsiConsolePromptTheme() { MaxRows = 5, Prompt = "> " };

			var state = await Run(
				keyHandler,
				autoComplete,
				theme,
				PromptState.CreateEmpty(root, theme),
				[
					(Keyboard.h,     Expr(theme, root).Add('h').Tokens("incomplete:h").Candidates(["hello", "help"], commonPrefix: "hel")),
					(Keyboard.e,     Expr(theme, root).Add('e').Tokens("incomplete:he").Candidates(["hello", "help"], commonPrefix: "hel")),
					(Keyboard.l,     Expr(theme, root).Add('l').Tokens("incomplete:hel").Candidates(["hello", "help"], commonPrefix: "hel")),
					(Keyboard.l,     Expr(theme, root).Add('l').Tokens("incomplete:hell").Candidates(["hello"], commonPrefix: "hello")),
					(Keyboard.o,     Expr(theme, root).Add('o').Tokens("command:hello").Candidates(["hello"], exactMatch: "hello")),
					(Keyboard.Space, Expr(theme, hello).Next().Tokens("command:hello", "").Candidates(["world", "there"])),
					(Keyboard.w,     Expr(theme, hello).Add('w').Tokens("command:hello", "incomplete:w").Candidates(["world"], commonPrefix: "world")),
					(Keyboard.o,     Expr(theme, hello).Add('o').Tokens("command:hello", "incomplete:wo").Candidates(["world"], commonPrefix: "world")),
					(Keyboard.r,     Expr(theme, hello).Add('r').Tokens("command:hello", "incomplete:wor").Candidates(["world"], commonPrefix: "world")),
					(Keyboard.l,     Expr(theme, hello).Add('l').Tokens("command:hello", "incomplete:worl").Candidates(["world"], commonPrefix: "world")),
					(Keyboard.d,     Expr(theme, hello).Add('d').Tokens("command:hello", "argument:world").Candidates(["world"], exactMatch: "world")),
					(Keyboard.Enter, Expr(theme, hello).Done().Tokens("command:hello", "argument:world")),
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
		public async Task Test_Can_Type_Hello_Command_With_Typos_And_Backspaces()
		{
			// Fully type the "hello world" command, but simulating some typos + backspaces

			var hello = new FakeHello();
			var help = new FakeHelp();
			var root = new RootPromptCommand() { Commands = [hello, help], };
			var keyHandler = new DefaultPromptKeyHandler();
			var autoComplete = new DefaultPromptAutoCompleter() { Root = root };
			var theme = new AnsiConsolePromptTheme() { MaxRows = 5, Prompt = "> " };

			var state = await Run(
				keyHandler,
				autoComplete,
				theme,
				PromptState.CreateEmpty(root, theme),
				[
					(Keyboard.h,         Expr(theme, root).Add('h').Tokens("incomplete:h").Candidates(["hello", "help"], commonPrefix: "hel")),
					(Keyboard.e,         Expr(theme, root).Add('e').Tokens("incomplete:he").Candidates(["hello", "help"], commonPrefix: "hel")),
					(Keyboard.l,         Expr(theme, root).Add('l').Tokens("incomplete:hel").Candidates(["hello", "help"], commonPrefix: "hel")),
					(Keyboard.l,         Expr(theme, root).Add('l').Tokens("incomplete:hell").Candidates(["hello"], commonPrefix: "hello")),
					(Keyboard.l,         Expr(theme, root).Add('l').Tokens("incomplete:helll")), //TYPO!
					(Keyboard.Backspace, Expr(theme, root).Add('l').Tokens("incomplete:hell").Candidates(["hello"], commonPrefix: "hello")),
					(Keyboard.o,         Expr(theme, root).Add('o').Tokens("command:hello").Candidates(["hello"], exactMatch: "hello")),
					(Keyboard.Space,     Expr(theme, hello).Next().Tokens("command:hello", "").Candidates(["world", "there"])),
					(Keyboard.z,         Expr(theme, hello).Add('z').Tokens("command:hello", "incomplete:z")), //TYPO!
					(Keyboard.Backspace, Expr(theme, hello).Next().Tokens("command:hello", "").Candidates(["world", "there"])),
					(Keyboard.w,         Expr(theme, hello).Add('w').Tokens("command:hello", "incomplete:w").Candidates(["world"], commonPrefix: "world")),
					(Keyboard.o,         Expr(theme, hello).Add('o').Tokens("command:hello", "incomplete:wo").Candidates(["world"], commonPrefix: "world")),
					(Keyboard.r,         Expr(theme, hello).Add('r').Tokens("command:hello", "incomplete:wor").Candidates(["world"], commonPrefix: "world")),
					(Keyboard.l,         Expr(theme, hello).Add('l').Tokens("command:hello", "incomplete:worl").Candidates(["world"], commonPrefix: "world")),
					(Keyboard.d,         Expr(theme, hello).Add('d').Tokens("command:hello", "argument:world").Candidates(["world"], exactMatch: "world")),
					(Keyboard.d,         Expr(theme, hello).Add('d').Tokens("command:hello", "incomplete:worldd")), //TYPO!
					(Keyboard.Backspace, Expr(theme, hello).Add('d').Tokens("command:hello", "argument:world").Candidates(["world"], exactMatch: "world")),
					(Keyboard.Enter,     Expr(theme, hello).Done().Tokens("command:hello", "argument:world")),
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
			var theme = new AnsiConsolePromptTheme() { MaxRows = 5, Prompt = "> " };

			var state = await Run(
				keyHandler,
				autoComplete,
				theme,
				PromptState.CreateEmpty(root, theme),
				[
					(Keyboard.h,     Expr(theme, root).Add('h').Tokens("incomplete:h").Candidates(["hello", "help"], commonPrefix: "hel")),
					(Keyboard.e,     Expr(theme, root).Add('e').Tokens("incomplete:he").Candidates(["hello", "help"], commonPrefix: "hel")),
					(Keyboard.l,     Expr(theme, root).Add('l').Tokens("incomplete:hel").Candidates(["hello", "help"], commonPrefix: "hel")),
					(Keyboard.l,     Expr(theme, root).Add('l').Tokens("incomplete:hell").Candidates(["hello"], commonPrefix: "hello")),
					(Keyboard.Tab,   Expr(theme, root).Completed().Tokens("command:hello").Candidates(["hello"], exactMatch: "hello")),
					(Keyboard.Space, Expr(theme, hello).Next().Tokens("command:hello", "").Candidates(["world", "there"])),
					(Keyboard.w,     Expr(theme, hello).Add('w').Tokens("command:hello", "incomplete:w").Candidates(["world"], commonPrefix: "world")),
					(Keyboard.Tab,   Expr(theme, hello).Completed().Tokens("command:hello", "argument:world").Candidates(["world"], exactMatch: "world")),
					(Keyboard.Enter, Expr(theme, hello).Done().Tokens("command:hello", "argument:world")),
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

				public required JsonObject Options { get; init; }

			}

			public sealed record Builder : PromptCommandBuilder<FakeQuery, FakeQuery.Command>
			{

				/// <summary>We got a full query (either finished by ENTER or SPACE)</summary>
				public bool HasQuery { get; init; }

				/// <summary>Parsed query (maybe incomplete)</summary>
				public FqlQuery? Query { get; init; }

				/// <summary>Error when parsing the query</summary>
				public Exception? Error { get; init; }

				/// <summary>If true, the previous token is an option that expects a value</summary>
				public bool ExpectOptionValue { get; init; }

				public JsonObject? Options { get; init; }

				public string? LastOption { get; init; }

				public bool HasInvalidOption { get; init; }

				public override bool IsValid() => this.Query != null;

				public override string ToString() => this.Query != null
					? $"Query {{ Expr = {this.Query}, Options = {this.Options?.ToJsonCompact() } }}"
					: $"Query {{ Error = {this.Error?.Message} }}";

				public override PromptState Update(PromptState state)
				{
					if (state.IsDone())
					{
						if (!this.HasQuery && this.Query != null)
						{ // the query is valid
							return state with
							{
								Tokens = [..state.Tokens, state.Token],
								Token = default,
								RawToken = "",
								CommandBuilder = this with { HasQuery = true }
							};
						}
						return state with
						{
							Tokens = [..state.Tokens, state.Token],
							Token = default,
							RawToken = "",
						};
					}

					if (!this.HasQuery)
					{ // we are parsing the query...

						Contract.Debug.Assert(state.Tokens.Length == 1);

						if (!FqlQueryParser.ParseNext(state.RawToken, out var rest).Check(out var query, out var error))
						{ // incomplete or invalid query

							//TODO: heuristic to "complete" the query into a temporary query?
							// ex: we have type "/foo/bar(" we could complete with "/foo/bar(...)"

							return state with
							{
								Token = PromptToken.Create("incomplete", state.RawToken), //TODO: detect error vs incomplete!
								CommandBuilder = new Builder
								{
									HasQuery = false,
									Error = error.Error,
								}
							};
						}

						if (rest.Length == 0)
						{ // the query may or may not be complete yet!
							return state with
							{
								Token = PromptToken.Create("fql", state.RawToken), //TODO: detect error vs incomplete!
								CommandBuilder = new Builder
								{
									HasQuery = false,
									Query = query,
								}
							};
						}

						// if we have something more, and it is a space, we are going to the next token
						if (rest[0] == ' ')
						{ // next token!
							return state with
							{
								Change = PromptChange.NextToken,
								Tokens = [ ..state.Tokens, state.Token ],
								Token = default,
								RawToken = "",
								CommandBuilder = new Builder
								{
									HasQuery = true,
									Query = query,
								}
							};
						}
						else
						{ // the query is followed by extra characters!
							return state with
							{
								Token = PromptToken.Create("error", state.RawToken),
								CommandBuilder = new Builder
								{
									HasQuery = false,
									Query = null,
								}
							};
						}
					}

					// we are parsing options

					if (state.RawToken.EndsWith(' '))
					{ // next!
						return state with
						{
							Change = PromptChange.NextToken,
							Tokens = [ ..state.Tokens, state.Token ],
							Token = default,
							RawToken = "",
						};
					}

					var options = this.Options ?? JsonObject.EmptyReadOnly;
					bool invalid = false;

					var res = ParseOptionToken(state.RawToken, options, this.LastOption);
					if (res.Valid == true)
					{
						options = res.Options;
					}
					else if (res.Valid == false)
					{
						invalid = true;
					}

					var builder = this with
					{
						Options = options?.ToReadOnly(),
						HasInvalidOption = this.HasInvalidOption | invalid,
						LastOption = res.Last,
					};

					if (state.RawToken.EndsWith(' '))
					{
						return state with
						{
							Change = PromptChange.NextToken,
							RawToken = "",
							Tokens = [..state.Tokens, state.Token],
							Token = default,
							CommandBuilder = builder,
						};
					}
					if (invalid)
					{
						return state with
						{
							Token = PromptToken.Create("error", state.RawToken),
							CommandBuilder = builder,
						};
					}
					return state with
					{
						Token = PromptToken.Create(res.Valid is null ? "incomplete" : this.LastOption is not null ? "number" : "option", state.RawToken),
						CommandBuilder = builder,
					};
				}

				private (bool? Valid, JsonObject? Options, string? Last) ParseOptionToken(string token, JsonObject options, string? last)
				{
					if (last != null)
					{
						if (last == "top")
						{
							if (!int.TryParse(token, out var x))
							{
								return (false, options, null);
							}
							options = options.CopyAndSet("top", x);
							return (true, options, last);
						}
						return (false, options, last);
					}

					if (!token.StartsWith('-'))
					{
						return (false, options, null);
					}

					switch (token)
					{
						case "-":
						case "--":
						{
							return (null, options, null);
						}
						case "-d":
						case "--dump":
						{
							options = options.CopyAndSet("dump", true);
							return (true, options, null);
						}
						case "-f":
						case "--force":
						{
							options = options.CopyAndSet("force", true);
							return (true, options, null);
						}
						case "--top":
						{
							return (true, options, "top");
						}

						//HACKHACK: until we get auto-complete!
						case "--t":
						case "--to":
						{
							return (null, options, null);
						}

						default:
						{
							return (false, options, null);
						}
					}
				}

				public override Command Build(PromptState state, FakeQuery descriptor)
				{
					if (this.Query == null)
					{
						throw new InvalidOperationException("Invalid FQL query", this.Error);
					}

					return new(descriptor, state.RawText)
					{
						Query = this.Query,
						Options = this.Options ?? JsonObject.EmptyReadOnly,
					};
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
			var theme = new AnsiConsolePromptTheme() { MaxRows = 5, Prompt = "> " };

			Log("Typing...");

			var state = await Run(
				keyHandler,
				autoComplete,
				theme,
				PromptState.CreateEmpty(root, theme),
				[
					(Keyboard.q,           Expr(theme, root).Add('q').Tokens("incomplete:q").Candidates(["query"], commonPrefix: "query")),
					(Keyboard.Tab,         Expr(theme, root).Completed().Tokens("command:query").Candidates(["query"], exactMatch: "query")),
					(Keyboard.Space,       Expr(theme, query).Next().Tokens("command:query", "")),
					(Keyboard.Slash,       Expr(theme, query).Add('/').Tokens("command:query", "fql:/")),
					(Keyboard.a,           Expr(theme, query).Add('a').Tokens("command:query", "fql:/a")),
					(Keyboard.b,           Expr(theme, query).Add('b').Tokens("command:query", "fql:/ab")),
					(Keyboard.c,           Expr(theme, query).Add('c').Tokens("command:query", "fql:/abc")),
					(Keyboard.OpenParens,  Expr(theme, query).Add('(').Tokens("command:query", "incomplete:/abc(")),
					(Keyboard.Digit1,      Expr(theme, query).Add('1').Tokens("command:query", "incomplete:/abc(1")),
					(Keyboard.Comma,       Expr(theme, query).Add(',').Tokens("command:query", "incomplete:/abc(1,")),
					(Keyboard.Dot,         Expr(theme, query).Add('.').Tokens("command:query", "incomplete:/abc(1,.")),
					(Keyboard.Dot,         Expr(theme, query).Add('.').Tokens("command:query", "incomplete:/abc(1,..")),
					(Keyboard.Dot,         Expr(theme, query).Add('.').Tokens("command:query", "incomplete:/abc(1,...")),
					(Keyboard.CloseParens, Expr(theme, query).Add(')').Tokens("command:query", "fql:/abc(1,...)")),
					(Keyboard.Enter,       Expr(theme, query).Done().Tokens("command:query", "fql:/abc(1,...)")),
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
			Assert.That(cmd.Query.Directory, Is.EqualTo(FqlDirectoryExpression.Create().Root().Name("abc")));
			Assert.That(cmd.Query.Tuple, Is.EqualTo(FqlTupleExpression.Create().Integer(1).MaybeMore()));
			Log($"Markup: {FqlSyntaxHighlighter.GetMarkup(cmd.Query)}");

			// we should not have any options
			Assert.That(cmd.Options, IsJson.ReadOnly.And.Empty);
		}

		[Test]
		public async Task Test_Can_Type_FqlQuery_Followed_By_Option()
		{
			var query = new FakeQuery();
			var root = new RootPromptCommand() { Commands = [query], };
			var keyHandler = new DefaultPromptKeyHandler();
			var autoComplete = new DefaultPromptAutoCompleter() { Root = root };
			var theme = new AnsiConsolePromptTheme() { MaxRows = 5, Prompt = "> " };

			Log("Typing...");

			var state = await Run(
				keyHandler,
				autoComplete,
				theme,
				PromptState.CreateEmpty(root, theme),
				[
					(Keyboard.q,           Expr(theme, root).Add('q').Tokens("incomplete:q").Candidates(["query"], commonPrefix: "query")),
					(Keyboard.Tab,         Expr(theme, root).Completed().Tokens("command:query").Candidates(["query"], exactMatch: "query")),
					(Keyboard.Space,       Expr(theme, query).Next().Tokens("command:query", "")),
					(Keyboard.Slash,       Expr(theme, query).Add('/').Tokens("command:query", "fql:/")),
					(Keyboard.a,           Expr(theme, query).Add('a').Tokens("command:query", "fql:/a")),
					(Keyboard.b,           Expr(theme, query).Add('b').Tokens("command:query", "fql:/ab")),
					(Keyboard.c,           Expr(theme, query).Add('c').Tokens("command:query", "fql:/abc")),
					(Keyboard.OpenParens,  Expr(theme, query).Add('(').Tokens("command:query", "incomplete:/abc(")),
					(Keyboard.Dot,         Expr(theme, query).Add('.').Tokens("command:query", "incomplete:/abc(.")),
					(Keyboard.Dot,         Expr(theme, query).Add('.').Tokens("command:query", "incomplete:/abc(..")),
					(Keyboard.Dot,         Expr(theme, query).Add('.').Tokens("command:query", "incomplete:/abc(...")),
					(Keyboard.CloseParens, Expr(theme, query).Add(')').Tokens("command:query", "fql:/abc(...)")),
					(Keyboard.Space,       Expr(theme, query).Next().Tokens("command:query", "fql:/abc(...)", "")),
					(Keyboard.Dash,        Expr(theme, query).Add('-').Tokens("command:query", "fql:/abc(...)", "incomplete:-")),
					(Keyboard.d,           Expr(theme, query).Add('d').Tokens("command:query", "fql:/abc(...)", "option:-d")),
					(Keyboard.Space,       Expr(theme, query).Next().Tokens("command:query", "fql:/abc(...)", "option:-d", "")),
					(Keyboard.Dash,        Expr(theme, query).Add('-').Tokens("command:query", "fql:/abc(...)", "option:-d", "incomplete:-")),
					(Keyboard.Dash,        Expr(theme, query).Add('-').Tokens("command:query", "fql:/abc(...)", "option:-d", "incomplete:--")),
					(Keyboard.t,           Expr(theme, query).Add('t').Tokens("command:query", "fql:/abc(...)", "option:-d", "incomplete:--t")),
					(Keyboard.o,           Expr(theme, query).Add('o').Tokens("command:query", "fql:/abc(...)", "option:-d", "incomplete:--to")),
					(Keyboard.p,           Expr(theme, query).Add('p').Tokens("command:query", "fql:/abc(...)", "option:-d", "option:--top")),
					(Keyboard.Space,       Expr(theme, query).Next().Tokens("command:query", "fql:/abc(...)", "option:-d", "option:--top", "")),
					(Keyboard.Digit1,      Expr(theme, query).Add('1').Tokens("command:query", "fql:/abc(...)", "option:-d", "option:--top", "number:1")),
					(Keyboard.Digit0,      Expr(theme, query).Add('0').Tokens("command:query", "fql:/abc(...)", "option:-d", "option:--top", "number:10")),
					(Keyboard.Enter,       Expr(theme, query).Done().Tokens("command:query", "fql:/abc(...)", "option:-d", "option:--top", "number:10")),
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

			Dump(cmd);

			// we must have the query
			Assert.That(cmd.Query, Is.Not.Null);
			Log(cmd.Query.Explain(prefix: "# "));
			Assert.That(cmd.Query.Directory, Is.EqualTo(FqlDirectoryExpression.Create().Root().Name("abc")));
			Assert.That(cmd.Query.Tuple, Is.EqualTo(FqlTupleExpression.Create().MaybeMore()));

			// we must have the "-d" command
			Assert.That(cmd.Options, IsJson.ReadOnly);
			Assert.That(cmd.Options["dump"], IsJson.True);
			Assert.That(cmd.Options["top"], IsJson.EqualTo(10));
			Assert.That(cmd.Options, IsJson.OfSize(2));
		}

		[Test]
		public void Test_PromptTokenBuilder_Basics()
		{
			var theme = new AnsiConsolePromptTheme() { MaxRows = 5, Prompt = "> " };

			{
				var builder = new PromptBuilder(theme);
				builder.Add("foo", "Hello", "yellow");
				builder.Add("bar", "World", "cyan", "blue");
				Log("Raw   : " + builder.ToString());

				Assert.That(builder.ToString(), Is.EqualTo("Hello World"));
				Assert.That(builder.Count, Is.EqualTo(2));
				Assert.That(builder[0], Is.EqualTo(PromptToken.Create("foo", "Hello", "yellow")));
				Assert.That(builder[1], Is.EqualTo(PromptToken.Create("bar", "World", "cyan", "blue")));
			}

			{
				var builder = new PromptBuilder(theme);
				builder.Add("path", [
					PromptTokenFragment.Create("/", "gray"),
					PromptTokenFragment.Create("foo", "red"),
					PromptTokenFragment.Create("/", "gray"),
					PromptTokenFragment.Create("bar", "green"),
					PromptTokenFragment.Create("/", "gray"),
					PromptTokenFragment.Create("baz", "blue"),
				]);
				Log("Raw   : " + builder.ToString());

				Assert.That(builder.ToString(), Is.EqualTo("/foo/bar/baz"));
				Assert.That(builder.Count, Is.EqualTo(1));
				var token = builder[0];
				Assert.That(token.Count, Is.EqualTo(6));
				Assert.That(token[0], Is.EqualTo(PromptTokenFragment.Create("/", "gray")));
				Assert.That(token[1], Is.EqualTo(PromptTokenFragment.Create("foo", "red")));
				Assert.That(token[2], Is.EqualTo(PromptTokenFragment.Create("/", "gray")));
				Assert.That(token[3], Is.EqualTo(PromptTokenFragment.Create("bar", "green")));
				Assert.That(token[4], Is.EqualTo(PromptTokenFragment.Create("/", "gray")));
				Assert.That(token[5], Is.EqualTo(PromptTokenFragment.Create("baz", "blue")));
			}
		}

		[Test]
		public void Test_PromptTokenBuilder_AnsiConsole_Render_Markup()
		{
			// test that the renderer can generate the markup string
			// - all fragments should be decorated with optional "[color]...[/]", and stitched together to form the token
			// - all tokens should be concatenated with a space between them

			var renderer = new AnsiConsolePromptRenderer();

			{
				var theme = new AnsiConsolePromptTheme()
				{
					MaxRows = 5,
					Prompt = "> ",
				};

				var builder = new PromptBuilder(theme);
				builder.Add("foo", "Hello", "yellow");
				builder.Add("bar", "World", "cyan", "blue");
				var prompt = builder.Build();
				Log($"Raw   : {prompt}");

				var markup = prompt.Render(renderer);
				Log($"Markup: {markup}");
				Assert.That(markup, Is.EqualTo("[yellow]Hello[/] [cyan on blue]World[/]"));
			}

			{
				var theme = new AnsiConsolePromptTheme()
				{
					MaxRows = 5,
					Prompt = "> ",
					DefaultForeground = "gray",
				};

				var builder = new PromptBuilder(theme);
				builder.Add("command", "query", "magenta");
				builder.Add("path",
				[
					PromptTokenFragment.Create("/"),
					PromptTokenFragment.Create("foo", "red"),
					PromptTokenFragment.Create("/"),
					PromptTokenFragment.Create("bar", "green"),
					PromptTokenFragment.Create("/"),
					PromptTokenFragment.Create("baz", "blue"),
				]);
				builder.Add("option", "--top", "yellow");
				builder.Add("number", "10", "cyan");

				var prompt = builder.Build();

				Log($"Raw   : {prompt}");
				Assert.That(prompt.ToString(), Is.EqualTo("query /foo/bar/baz --top 10"));

				var markup = prompt.Render(renderer);
				Log($"Markup: {markup}");
				Assert.That(markup, Is.EqualTo("[gray][magenta]query[/] /[red]foo[/]/[green]bar[/]/[blue]baz[/] [yellow]--top[/] [cyan]10[/][/]"));
			}

		}

	}

}
