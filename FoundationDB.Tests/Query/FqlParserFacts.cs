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

#if NET8_0_OR_GREATER

namespace FoundationDB.Client.Tests
{
	using System;

	using FoundationDB.Client.Native;

	[TestFixture]
	[Parallelizable(ParallelScope.All)]
	public class FqlParserFacts : FdbSimpleTest
	{

		[Test]
		public void Test_Fql_Parse_Directories()
		{
			// `/user`
			{
				var q = FqlQueryParser.Parse("/user");
				Assert.That(q, Is.Not.Null);
				Log(q.Explain());
				Assert.That(q.Directory, Is.EqualTo(FqlDirectoryExpression.Create().AddLiteral("user")));
				Assert.That(q.Tuple, Is.Null);
			}

			// `/foo/bar/baz`
			{
				var q = FqlQueryParser.Parse("/foo/bar/baz");
				Assert.That(q, Is.Not.Null);
				Log(q.Explain());
				Assert.That(q.Directory, Is.EqualTo(FqlDirectoryExpression.Create().AddLiteral("foo").AddLiteral("bar").AddLiteral("baz")));
				Assert.That(q.Tuple, Is.Null);
			}

			// `/foo/"bar"/baz`
			{
				var q = FqlQueryParser.Parse("/foo/\"bar\"/baz");
				Assert.That(q, Is.Not.Null);
				Log(q.Explain());
				Assert.That(q.Directory, Is.EqualTo(FqlDirectoryExpression.Create().AddLiteral("foo").AddLiteral("bar").AddLiteral("baz")));
				Assert.That(q.Tuple, Is.Null);
			}

			// `/"foo\"bar\"baz"`
			{
				var q = FqlQueryParser.Parse("/\"foo\\\"bar\\\"baz\"");
				Assert.That(q, Is.Not.Null);
				Log(q.Explain());
				Assert.That(q.Directory, Is.EqualTo(FqlDirectoryExpression.Create().AddLiteral("foo\"bar\"baz")));
				Assert.That(q.Tuple, Is.Null);
			}

			// `/user(...)`
			{
				var q = FqlQueryParser.Parse("/user(...)");
				Assert.That(q, Is.Not.Null);
				Log(q.Explain());
				Assert.That(q.Directory, Is.EqualTo(FqlDirectoryExpression.Create().AddLiteral("user")));
				Assert.That(q.Tuple, Is.EqualTo(FqlTupleExpression.Create().AddMaybeMore()));
			}

			// `/user(<int>,"Goodwin",...)`
			{
				var q = FqlQueryParser.Parse("/user(<int>,\"Goodwin\",...)");
				Assert.That(q, Is.Not.Null);
				Log(q.Explain());
				Assert.That(q.Directory, Is.EqualTo(FqlDirectoryExpression.Create().AddLiteral("user")));
				Assert.That(q.Tuple, Is.EqualTo(FqlTupleExpression.Create().AddVariable(FqlVariableTypes.Int).AddString("Goodwin").AddMaybeMore()));
			}

			// `(0xFF, "thing", ...)`
			{
				var q = FqlQueryParser.Parse("(0xFF, \"thing\", ...)");
				Assert.That(q, Is.Not.Null);
				Log(q.Explain());
				Assert.That(q.Directory, Is.Null);
				Assert.That(q.Tuple, Is.EqualTo(FqlTupleExpression.Create().AddBytes(Slice.FromByte(255)).AddString("thing").AddMaybeMore()));
			}

			{ // `("one", 2, 0x03, ( "subtuple" ), 5825d3f8-de5b-40c6-ac32-47ea8b98f7b4)`
				var q = FqlQueryParser.Parse("(\"one\", 2, 0x03, ( \"subtuple\" ), 5825d3f8-de5b-40c6-ac32-47ea8b98f7b4)");
				Assert.That(q, Is.Not.Null);
				Log(q.Explain());
				Assert.That(q.Directory, Is.Null);
				Assert.That(q.Tuple, Is.EqualTo(FqlTupleExpression.Create().AddString("one").AddInt(2).AddBytes(Slice.FromByte(3)).AddTuple(FqlTupleExpression.Create().AddString("subtuple")).AddUuid(Uuid128.Parse("5825d3f8-de5b-40c6-ac32-47ea8b98f7b4"))));
			}

			// 

			//{ // `/my/dir("this", 0)=0xabcf03`
			//	var q = FqlQueryParser.Parse("/my/dir(\"this\", 0)=0xabcf03");
			//	Assert.That(q, Is.Not.Null);
			//	Log(q.Explain());
			//}

			// 
			// /my/dir(22.3, -8)=("another", "tuple")
			// /some/where("home", "town", 88.3)=clear

			// /my/dir("hello", "world")=42
			// /my/dir("hello", "world")=clear
			// /my/dir(99.8, 7dfb10d1-2493-4fb5-928e-889fdc6a7136)=<int|string>
			// /people(3392, <string|int>, <>)=(<uint>, ...)
			// /root/<>/items/<>


		}

		[Test]
		public void Test_Fql_Compare_Tuples_Exact_Matches()
		{
			{ // ("hello")
				var q = FqlQueryParser.Parse("(\"hello\")");
				Log(q.Explain());
				Assert.That(q.Tuple, Is.EqualTo(FqlTupleExpression.Create().AddString("hello")));
				Assert.That(q.Directory, Is.Null);
				Assert.That(q.IsPattern, Is.False);

				Assert.Multiple(() =>
				{
					var tuple = q.Tuple!;
					Assert.That(tuple.Matches(STuple.Create("hello")), Is.True);

					Assert.That(tuple.Matches(STuple.Create()), Is.False);
					Assert.That(tuple.Matches(STuple.Create("world")), Is.False);
					Assert.That(tuple.Matches(STuple.Create("hello", 123)), Is.False);
				});
			}
			{ // (123)
				var q = FqlQueryParser.Parse("(123)");
				Log(q.Explain());
				Assert.That(q.Tuple, Is.EqualTo(FqlTupleExpression.Create().AddInt(123)));
				Assert.That(q.Directory, Is.Null);
				Assert.That(q.IsPattern, Is.False);

				Assert.Multiple(() =>
				{
					var tuple = q.Tuple!;
					Assert.That(tuple.Matches(STuple.Create(123)), Is.True);
					Assert.That(tuple.Matches(STuple.Create(123L)), Is.True);
					Assert.That(tuple.Matches(STuple.Create(123U)), Is.True);
					Assert.That(tuple.Matches(STuple.Create(123UL)), Is.True);

					Assert.That(tuple.Matches(STuple.Create()), Is.False);
					Assert.That(tuple.Matches(STuple.Create(1)), Is.False);
					Assert.That(tuple.Matches(STuple.Create(12)), Is.False);
					Assert.That(tuple.Matches(STuple.Create(123f)), Is.False);
					Assert.That(tuple.Matches(STuple.Create(123, "hello")), Is.False);
					Assert.That(tuple.Matches(STuple.Create(123, 123)), Is.False);
				});
			}
			{ // ("hello", 123)
				var q = FqlQueryParser.Parse("(\"hello\", 123)");
				Log(q.Explain());
				Assert.That(q.Tuple, Is.EqualTo(FqlTupleExpression.Create().AddString("hello").AddInt(123)));
				Assert.That(q.Directory, Is.Null);
				Assert.That(q.IsPattern, Is.False);

				Assert.Multiple(() =>
				{
					var tuple = q.Tuple!;
					Assert.That(tuple.Matches(STuple.Create("hello", 123)), Is.True);
					Assert.That(tuple.Matches(STuple.Create("hello", 123L)), Is.True);
					Assert.That(tuple.Matches(STuple.Create("hello", 123U)), Is.True);
					Assert.That(tuple.Matches(STuple.Create("hello", 123UL)), Is.True);

					Assert.That(tuple.Matches(STuple.Create("hello")), Is.False);
					Assert.That(tuple.Matches(STuple.Create("hello", 123f)), Is.False);
					Assert.That(tuple.Matches(STuple.Create("hello", 123, "world")), Is.False);
					Assert.That(tuple.Matches(STuple.Create("world", 123)), Is.False);
					Assert.That(tuple.Matches(STuple.Create(123)), Is.False);
					Assert.That(tuple.Matches(STuple.Create(123, "hello")), Is.False);
				});
			}
		}

		[Test]
		public void Test_Fql_Compare_Tuples_Variable_Matches()
		{
			// (<string>)
			{
				var q = FqlQueryParser.Parse("(<string>)");
				Log(q.Explain());
				Assert.That(q.Tuple, Is.EqualTo(FqlTupleExpression.Create().AddVariable(FqlVariableTypes.String)));
				Assert.That(q.Directory, Is.Null);
				Assert.That(q.IsPattern, Is.True);

				Assert.Multiple(() =>
				{
					var tuple = q.Tuple!;
					Assert.That(tuple.Matches(STuple.Create("")), Is.True);
					Assert.That(tuple.Matches(STuple.Create("hello")), Is.True);

					Assert.That(tuple.Matches(STuple.Create()), Is.False);
					Assert.That(tuple.Matches(STuple.Create(default(string))), Is.False);
					Assert.That(tuple.Matches(STuple.Create(false)), Is.False);
					Assert.That(tuple.Matches(STuple.Create(true)), Is.False);
					Assert.That(tuple.Matches(STuple.Create(123)), Is.False);
					Assert.That(tuple.Matches(STuple.Create(123d)), Is.False);
					Assert.That(tuple.Matches(STuple.Create(Guid.Empty)), Is.False);
					Assert.That(tuple.Matches(STuple.Create(Slice.Empty)), Is.False);
					Assert.That(tuple.Matches(STuple.Create("hello", "world")), Is.False);
					Assert.That(tuple.Matches(STuple.Create(STuple.Create())), Is.False);
					Assert.That(tuple.Matches(STuple.Create(STuple.Create("hello"))), Is.False);
				});
			}

			// (<bool>)
			{
				var q = FqlQueryParser.Parse("(<bool>)");
				Log(q.Explain());
				Assert.That(q.Tuple, Is.EqualTo(FqlTupleExpression.Create().AddVariable(FqlVariableTypes.Bool)));
				Assert.That(q.Directory, Is.Null);
				Assert.That(q.IsPattern, Is.True);

				Assert.Multiple(() =>
				{
					var tuple = q.Tuple!;
					Assert.That(tuple.Matches(STuple.Create(false)), Is.True);
					Assert.That(tuple.Matches(STuple.Create(true)), Is.True);

					Assert.That(tuple.Matches(STuple.Create()), Is.False);
					Assert.That(tuple.Matches(STuple.Create(default(object))), Is.False);
					Assert.That(tuple.Matches(STuple.Create(0)), Is.False);
					Assert.That(tuple.Matches(STuple.Create(123d)), Is.False);
					Assert.That(tuple.Matches(STuple.Create("hello")), Is.False);
					Assert.That(tuple.Matches(STuple.Create(Guid.Empty)), Is.False);
					Assert.That(tuple.Matches(STuple.Create(Slice.Empty)), Is.False);
					Assert.That(tuple.Matches(STuple.Create(STuple.Create())), Is.False);
					Assert.That(tuple.Matches(STuple.Create(STuple.Create(true))), Is.False);
				});
			}

			// (<int>)
			{
				var q = FqlQueryParser.Parse("(<int>)");
				Log(q.Explain());
				Assert.That(q.Tuple, Is.EqualTo(FqlTupleExpression.Create().AddVariable(FqlVariableTypes.Int)));
				Assert.That(q.Directory, Is.Null);
				Assert.That(q.IsPattern, Is.True);

				Assert.Multiple(() =>
				{
					var tuple = q.Tuple!;
					Assert.That(tuple.Matches(STuple.Create(0)), Is.True);
					Assert.That(tuple.Matches(STuple.Create(1)), Is.True);
					Assert.That(tuple.Matches(STuple.Create(1L)), Is.True);
					Assert.That(tuple.Matches(STuple.Create(1U)), Is.True);
					Assert.That(tuple.Matches(STuple.Create(1UL)), Is.True);
					Assert.That(tuple.Matches(STuple.Create(-1)), Is.True);
					Assert.That(tuple.Matches(STuple.Create(-1L)), Is.True);

					Assert.That(tuple.Matches(STuple.Create()), Is.False);
					Assert.That(tuple.Matches(STuple.Create(default(object))), Is.False);
					Assert.That(tuple.Matches(STuple.Create(false)), Is.False);
					Assert.That(tuple.Matches(STuple.Create(true)), Is.False);
					Assert.That(tuple.Matches(STuple.Create(123d)), Is.False);
					Assert.That(tuple.Matches(STuple.Create("hello")), Is.False);
					Assert.That(tuple.Matches(STuple.Create(Guid.Empty)), Is.False);
					Assert.That(tuple.Matches(STuple.Create(Slice.Empty)), Is.False);
					Assert.That(tuple.Matches(STuple.Create(STuple.Create())), Is.False);
					Assert.That(tuple.Matches(STuple.Create(STuple.Create(123))), Is.False);
				});
			}

			// (<uint>)
			{
				var q = FqlQueryParser.Parse("(<uint>)");
				Log(q.Explain());
				Assert.That(q.Tuple, Is.EqualTo(FqlTupleExpression.Create().AddVariable(FqlVariableTypes.UInt)));
				Assert.That(q.Directory, Is.Null);
				Assert.That(q.IsPattern, Is.True);

				Assert.Multiple(() =>
				{
					var tuple = q.Tuple!;
					Assert.That(tuple.Matches(STuple.Create(0)), Is.True);
					Assert.That(tuple.Matches(STuple.Create(1)), Is.True);
					Assert.That(tuple.Matches(STuple.Create(1L)), Is.True);
					Assert.That(tuple.Matches(STuple.Create(1U)), Is.True);
					Assert.That(tuple.Matches(STuple.Create(1UL)), Is.True);

					Assert.That(tuple.Matches(STuple.Create()), Is.False);
					Assert.That(tuple.Matches(STuple.Create(default(object))), Is.False);
					Assert.That(tuple.Matches(STuple.Create(false)), Is.False);
					Assert.That(tuple.Matches(STuple.Create(true)), Is.False);
					Assert.That(tuple.Matches(STuple.Create(-1)), Is.False);
					Assert.That(tuple.Matches(STuple.Create(-1L)), Is.False);
					Assert.That(tuple.Matches(STuple.Create(123d)), Is.False);
					Assert.That(tuple.Matches(STuple.Create("hello")), Is.False);
					Assert.That(tuple.Matches(STuple.Create(Guid.Empty)), Is.False);
					Assert.That(tuple.Matches(STuple.Create(Slice.Empty)), Is.False);
					Assert.That(tuple.Matches(STuple.Create(STuple.Create())), Is.False);
					Assert.That(tuple.Matches(STuple.Create(STuple.Create(123))), Is.False);
				});
			}

			// (<uuid>)
			{
				var q = FqlQueryParser.Parse("(<uuid>)");
				Log(q.Explain());
				Assert.That(q.Tuple, Is.EqualTo(FqlTupleExpression.Create().AddVariable(FqlVariableTypes.Uuid)));
				Assert.That(q.Directory, Is.Null);
				Assert.That(q.IsPattern, Is.True);

				Assert.Multiple(() =>
				{
					var tuple = q.Tuple!;
					Assert.That(tuple.Matches(STuple.Create(Guid.Empty)), Is.True);
					Assert.That(tuple.Matches(STuple.Create(Uuid128.Empty)), Is.True);
					Assert.That(tuple.Matches(STuple.Create(Guid.NewGuid())), Is.True);
					Assert.That(tuple.Matches(STuple.Create(Uuid128.NewUuid())), Is.True);

					Assert.That(tuple.Matches(STuple.Create()), Is.False);
					Assert.That(tuple.Matches(STuple.Create(default(object))), Is.False);
					Assert.That(tuple.Matches(STuple.Create(false)), Is.False);
					Assert.That(tuple.Matches(STuple.Create(true)), Is.False);
					Assert.That(tuple.Matches(STuple.Create(0)), Is.False);
					Assert.That(tuple.Matches(STuple.Create(123d)), Is.False);
					Assert.That(tuple.Matches(STuple.Create("hello")), Is.False);
					Assert.That(tuple.Matches(STuple.Create(Slice.Empty)), Is.False);
					Assert.That(tuple.Matches(STuple.Create(STuple.Create())), Is.False);
					Assert.That(tuple.Matches(STuple.Create(STuple.Create(Guid.Empty))), Is.False);
				});
			}

			// (<bytes>)
			{
				var q = FqlQueryParser.Parse("(<bytes>)");
				Log(q.Explain());
				Assert.That(q.Tuple, Is.EqualTo(FqlTupleExpression.Create().AddVariable(FqlVariableTypes.Bytes)));
				Assert.That(q.Directory, Is.Null);
				Assert.That(q.IsPattern, Is.True);

				Assert.Multiple(() =>
				{
					var tuple = q.Tuple!;
					Assert.That(tuple.Matches(STuple.Create(Slice.Empty)), Is.True);
					Assert.That(tuple.Matches(STuple.Create(Slice.Copy("hello"u8))), Is.True);

					Assert.That(tuple.Matches(STuple.Create()), Is.False);
					Assert.That(tuple.Matches(STuple.Create(default(object))), Is.False);
					Assert.That(tuple.Matches(STuple.Create(false)), Is.False);
					Assert.That(tuple.Matches(STuple.Create(true)), Is.False);
					Assert.That(tuple.Matches(STuple.Create(0)), Is.False);
					Assert.That(tuple.Matches(STuple.Create(123d)), Is.False);
					Assert.That(tuple.Matches(STuple.Create("hello")), Is.False);
					Assert.That(tuple.Matches(STuple.Create(STuple.Create())), Is.False);
					Assert.That(tuple.Matches(STuple.Create(STuple.Create(Slice.Empty))), Is.False);
				});
			}

		}

		[Test]
		public void Test_Fql_Compare_Tuples_Hybrid_Matches()
		{
			// ("hello", <int>)
			{
				var q = FqlQueryParser.Parse("(\"hello\", <int>)");
				Log(q.Explain());
				Assert.That(q.Tuple, Is.EqualTo(FqlTupleExpression.Create().AddString("hello").AddVariable(FqlVariableTypes.Int)));
				Assert.That(q.Directory, Is.Null);
				Assert.That(q.IsPattern, Is.True);

				Assert.Multiple(() =>
				{
					var tuple = q.Tuple!;
					Assert.That(tuple.Matches(STuple.Create("hello", 0)), Is.True);
					Assert.That(tuple.Matches(STuple.Create("hello", 1)), Is.True);
					Assert.That(tuple.Matches(STuple.Create("hello", 12)), Is.True);
					Assert.That(tuple.Matches(STuple.Create("hello", 123)), Is.True);
					Assert.That(tuple.Matches(STuple.Create("hello", 123L)), Is.True);
					Assert.That(tuple.Matches(STuple.Create("hello", -1)), Is.True);
					Assert.That(tuple.Matches(STuple.Create("hello", -12)), Is.True);
					Assert.That(tuple.Matches(STuple.Create("hello", -123)), Is.True);
					Assert.That(tuple.Matches(STuple.Create("hello", -123L)), Is.True);
					Assert.That(tuple.Matches(STuple.Create("hello", 123U)), Is.True);
					Assert.That(tuple.Matches(STuple.Create("hello", 123UL)), Is.True);

					Assert.That(tuple.Matches(STuple.Create()), Is.False);
					Assert.That(tuple.Matches(STuple.Create("hello")), Is.False);
					Assert.That(tuple.Matches(STuple.Create("hello", default(object))), Is.False);
					Assert.That(tuple.Matches(STuple.Create("hello", "123")), Is.False);
					Assert.That(tuple.Matches(STuple.Create("hello", 123f)), Is.False);
					Assert.That(tuple.Matches(STuple.Create("hello", 123, "world")), Is.False);
					Assert.That(tuple.Matches(STuple.Create("world", 123)), Is.False);
					Assert.That(tuple.Matches(STuple.Create(123)), Is.False);
					Assert.That(tuple.Matches(STuple.Create(123, "hello")), Is.False);
				});
			}

			// (<string>, 123)
			{
				var q = FqlQueryParser.Parse("(<string>, 123)");
				Log(q.Explain());
				Assert.That(q.Tuple, Is.EqualTo(FqlTupleExpression.Create().AddVariable(FqlVariableTypes.String).AddInt(123)));
				Assert.That(q.Directory, Is.Null);
				Assert.That(q.IsPattern, Is.True);

				Assert.Multiple(() =>
				{
					var tuple = q.Tuple!;
					Assert.That(tuple.Matches(STuple.Create("", 123)), Is.True);
					Assert.That(tuple.Matches(STuple.Create("hello", 123)), Is.True);
					Assert.That(tuple.Matches(STuple.Create("hello", 123L)), Is.True);
					Assert.That(tuple.Matches(STuple.Create("hello", 123U)), Is.True);
					Assert.That(tuple.Matches(STuple.Create("hello", 123UL)), Is.True);

					Assert.That(tuple.Matches(STuple.Create()), Is.False);
					Assert.That(tuple.Matches(STuple.Create("hello")), Is.False);
					Assert.That(tuple.Matches(STuple.Create("hello", 123, "world")), Is.False);
				});
			}

			// ("hello", <int>, "world")
			{
				var q = FqlQueryParser.Parse("(\"hello\", <int>, \"world\")");
				Log(q.Explain());
				Assert.That(q.Tuple, Is.EqualTo(FqlTupleExpression.Create().AddString("hello").AddVariable(FqlVariableTypes.Int).AddString("world")));
				Assert.That(q.Directory, Is.Null);
				Assert.That(q.IsPattern, Is.True);

				Assert.Multiple(() =>
				{
					var tuple = q.Tuple!;
					Assert.That(tuple.Matches(STuple.Create("hello", 123, "world")), Is.True);

					Assert.That(tuple.Matches(STuple.Create()), Is.False);
					Assert.That(tuple.Matches(STuple.Create("hello")), Is.False);
					Assert.That(tuple.Matches(STuple.Create("hello", 123)), Is.False);
					Assert.That(tuple.Matches(STuple.Create("hello", default(object), "world")), Is.False);
					Assert.That(tuple.Matches(STuple.Create("hello", "123", "world")), Is.False);
					Assert.That(tuple.Matches(STuple.Create("hello", 123, "world?")), Is.False);
					Assert.That(tuple.Matches(STuple.Create("hello", 123, "world", "!!!")), Is.False);
					Assert.That(tuple.Matches(STuple.Create("hello", "world", 123)), Is.False);
				});
			}
		}

		[Test]
		public void Test_Fql_Compare_Tuples_MaybeMore()
		{
			// ("hello", ...)
			{
				var q = FqlQueryParser.Parse("(\"hello\", ...)");
				Log(q.Explain());
				Assert.That(q.Tuple, Is.EqualTo(FqlTupleExpression.Create().AddString("hello").AddMaybeMore()));
				Assert.That(q.Directory, Is.Null);
				Assert.That(q.IsPattern, Is.True);

				Assert.Multiple(() =>
				{
					var tuple = q.Tuple!;
					Assert.That(tuple.Matches(STuple.Create("hello")), Is.True);
					Assert.That(tuple.Matches(STuple.Create("hello", default(object))), Is.True);
					Assert.That(tuple.Matches(STuple.Create("hello", 123)), Is.True);
					Assert.That(tuple.Matches(STuple.Create("hello", 123f)), Is.True);
					Assert.That(tuple.Matches(STuple.Create("hello", "world")), Is.True);
					Assert.That(tuple.Matches(STuple.Create("hello", Guid.Empty)), Is.True);
					Assert.That(tuple.Matches(STuple.Create("hello", Slice.Empty)), Is.True);
					Assert.That(tuple.Matches(STuple.Create("hello", STuple.Create())), Is.True);

					Assert.That(tuple.Matches(STuple.Create()), Is.False);
					Assert.That(tuple.Matches(STuple.Create("world")), Is.False);
					Assert.That(tuple.Matches(STuple.Create("world", "hello")), Is.False);
					Assert.That(tuple.Matches(STuple.Create(default(object))), Is.False);
					Assert.That(tuple.Matches(STuple.Create(123)), Is.False);
					Assert.That(tuple.Matches(STuple.Create(123d)), Is.False);
					Assert.That(tuple.Matches(STuple.Create(Guid.Empty)), Is.False);
					Assert.That(tuple.Matches(STuple.Create(Slice.Empty)), Is.False);
					Assert.That(tuple.Matches(STuple.Create(STuple.Create("hello"))), Is.False);
				});
			}

			// (1, <int>, ...)
			{
				var q = FqlQueryParser.Parse("(1, <int>, ...)");
				Log(q.Explain());
				Assert.That(q.Tuple, Is.EqualTo(FqlTupleExpression.Create().AddInt(1).AddVariable(FqlVariableTypes.Int).AddMaybeMore()));
				Assert.That(q.Directory, Is.Null);
				Assert.That(q.IsPattern, Is.True);

				Assert.Multiple(() =>
				{
					var tuple = q.Tuple!;
					Assert.That(tuple.Matches(STuple.Create(1, 123)), Is.True);
					Assert.That(tuple.Matches(STuple.Create(1, 123, default(object))), Is.True);
					Assert.That(tuple.Matches(STuple.Create(1, 123, 456)), Is.True);
					Assert.That(tuple.Matches(STuple.Create(1, 123, 456f)), Is.True);
					Assert.That(tuple.Matches(STuple.Create(1, 123, "world")), Is.True);
					Assert.That(tuple.Matches(STuple.Create(1, 123, Guid.Empty)), Is.True);
					Assert.That(tuple.Matches(STuple.Create(1, 123, Slice.Empty)), Is.True);
					Assert.That(tuple.Matches(STuple.Create(1, 123, STuple.Create())), Is.True);

					Assert.That(tuple.Matches(STuple.Create()), Is.False);
					Assert.That(tuple.Matches(STuple.Create(1)), Is.False);
					Assert.That(tuple.Matches(STuple.Create("world")), Is.False);
					Assert.That(tuple.Matches(STuple.Create(2, 123)), Is.False);
					Assert.That(tuple.Matches(STuple.Create(2, "hello")), Is.False);
					Assert.That(tuple.Matches(STuple.Create(1, default(object))), Is.False);
					Assert.That(tuple.Matches(STuple.Create(1d)), Is.False);
					Assert.That(tuple.Matches(STuple.Create(Guid.Empty, 123)), Is.False);
					Assert.That(tuple.Matches(STuple.Create(Slice.Empty, 123)), Is.False);
					Assert.That(tuple.Matches(STuple.Create(STuple.Create(1), 123)), Is.False);
				});
			}

		}

	}

}

#endif
