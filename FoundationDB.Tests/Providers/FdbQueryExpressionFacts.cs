#region Copyright (c) 2023 SnowBank SAS, (c) 2005-2023 Doxense SAS
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

 namespace FoundationDB.Linq.Expressions.Tests
{
	using System;
	using System.Collections.Generic;
	using System.Linq.Expressions;
	using Doxense.Collections.Tuples;
	using FoundationDB.Client;
	using FoundationDB.Client.Tests;
	using FoundationDB.Layers.Indexing;
	using NUnit.Framework;

	[TestFixture]
	public class FdbQueryExpressionFacts : FdbTest

	{

		private readonly FdbIndex<int, string> FooBarIndex = new FdbIndex<int, string>(DynamicKeySubspaceLocation.Root.ByKey("Foos", 1));

		private readonly FdbIndex<int, long> FooBazIndex = new FdbIndex<int, long>(DynamicKeySubspaceLocation.Root.ByKey("Foos", 2));

		[Test]
		public void Test_FdbQueryIndexLookupExpression()
		{
			var expr = FdbQueryIndexLookupExpression<int, string>.Lookup(
				FooBarIndex,
				ExpressionType.Equal,
				Expression.Constant("world")
			);
			Dump(expr);

			Assert.That(expr, Is.Not.Null);
			Assert.That(expr.Index, Is.SameAs(FooBarIndex)); //TODO: .Index.Index does not look very nice
			Assert.That(expr.Operator, Is.EqualTo(ExpressionType.Equal));
			Assert.That(expr.Value, Is.Not.Null);
			Assert.That(expr.Value, Is.InstanceOf<ConstantExpression>().With.Property("Value").EqualTo("world"));

			Assert.That(expr.Type, Is.EqualTo(typeof(IAsyncEnumerable<int>)));
			Assert.That(expr.ElementType, Is.EqualTo(typeof(int)));

			Log(FdbQueryExpressions.ExplainSequence(expr));

		}

		[Test]
		public void Test_FdbQueryIndexLookupExpression_From_Lambda()
		{
			var expr = FdbQueryIndexLookupExpression<int, string>.Lookup(
				FooBarIndex,
				(bar) => bar == "world"
			);
			Dump(expr);

			Assert.That(expr, Is.Not.Null);
			Assert.That(expr.Index, Is.SameAs(FooBarIndex)); //TODO: .Index.Index does not look very nice
			Assert.That(expr.Operator, Is.EqualTo(ExpressionType.Equal));
			Assert.That(expr.Value, Is.Not.Null);
			Assert.That(expr.Value, Is.InstanceOf<ConstantExpression>().With.Property("Value").EqualTo("world"));

			Assert.That(expr.Type, Is.EqualTo(typeof(IAsyncEnumerable<int>)));
			Assert.That(expr.ElementType, Is.EqualTo(typeof(int)));

			Log(FdbQueryExpressions.ExplainSequence(expr));

		}

		[Test]
		public void Test_FdbQueryRangeExpression()
		{
			var expr = FdbQueryExpressions.Range(
				KeySelectorPair.Create(TuPack.ToKeyRange("Foo"))
			);
			Dump(expr);

			Assert.That(expr, Is.Not.Null);
			Assert.That(expr.Range.Begin.Key.ToString(), Is.EqualTo("<02>Foo<00><00>"));
			Assert.That(expr.Range.End.Key.ToString(), Is.EqualTo("<02>Foo<00><FF>"));

			Assert.That(expr.Type, Is.EqualTo(typeof(IAsyncEnumerable<KeyValuePair<Slice, Slice>>)));
			Assert.That(expr.ElementType, Is.EqualTo(typeof(KeyValuePair<Slice, Slice>)));

			Log(FdbQueryExpressions.ExplainSequence(expr));
		}

		[Test]
		public void Test_FdbQueryIntersectExpression()
		{
			var expr1 = FdbQueryIndexLookupExpression<int, string>.Lookup(
				FooBarIndex,
				(x) => x == "world"
			);
			var expr2 = FdbQueryIndexLookupExpression<int, long>.Lookup(
				FooBazIndex,
				(x) => x == 1234L
			);

			var expr = FdbQueryExpressions.Intersect(
				expr1,
				expr2
			);
			Dump(expr);

			Assert.That(expr, Is.Not.Null);
			Assert.That(expr.Terms, Is.Not.Null);
			Assert.That(expr.Terms.Count, Is.EqualTo(2));
			Assert.That(expr.Terms[0], Is.SameAs(expr1));
			Assert.That(expr.Terms[1], Is.SameAs(expr2));

			Assert.That(expr.Type, Is.EqualTo(typeof(IAsyncEnumerable<int>)));
			Assert.That(expr.ElementType, Is.EqualTo(typeof(int)));

			Log(FdbQueryExpressions.ExplainSequence(expr));
		}

		[Test]
		public void Test_FdbQueryUnionExpression()
		{
			var expr1 = FdbQueryIndexLookupExpression<int, string>.Lookup(
				FooBarIndex,
				(x) => x == "world"
			);
			var expr2 = FdbQueryIndexLookupExpression<int, long>.Lookup(
				FooBazIndex,
				(x) => x == 1234L
			);

			var expr = FdbQueryExpressions.Union(
				expr1,
				expr2
			);
			Dump(expr);

			Assert.That(expr, Is.Not.Null);
			Assert.That(expr.Terms, Is.Not.Null);
			Assert.That(expr.Terms.Count, Is.EqualTo(2));
			Assert.That(expr.Terms[0], Is.SameAs(expr1));
			Assert.That(expr.Terms[1], Is.SameAs(expr2));

			Assert.That(expr.Type, Is.EqualTo(typeof(IAsyncEnumerable<int>)));
			Assert.That(expr.ElementType, Is.EqualTo(typeof(int)));

			Log(FdbQueryExpressions.ExplainSequence(expr));
		}

		[Test]
		public void Test_FdbQueryTransformExpression()
		{
			var expr = FdbQueryExpressions.Transform(
				FdbQueryExpressions.RangeStartsWith(TuPack.EncodeKey("Hello", "World")),
				(kvp) => kvp.Value.ToUnicode()
			);
			Dump(expr);

			Assert.That(expr, Is.Not.Null);
			Assert.That(expr.Source, Is.Not.Null.And.InstanceOf<FdbQueryRangeExpression>());
			Assert.That(expr.Transform, Is.Not.Null);

			Assert.That(expr.Type, Is.EqualTo(typeof(IAsyncEnumerable<string>)));
			Assert.That(expr.ElementType, Is.EqualTo(typeof(string)));

			Log(FdbQueryExpressions.ExplainSequence(expr));
		}

		[Test]
		public void Test_FdbQueryFilterExpression()
		{
			var expr = FdbQueryExpressions.Filter(
				FdbQueryExpressions.RangeStartsWith(TuPack.EncodeKey("Hello", "World")),
				(kvp) => kvp.Value.ToInt32() % 2 == 0
			);
			Dump(expr);

			Assert.That(expr, Is.Not.Null);
			Assert.That(expr.Source, Is.Not.Null.And.InstanceOf<FdbQueryRangeExpression>());
			Assert.That(expr.Filter, Is.Not.Null);

			Assert.That(expr.Type, Is.EqualTo(typeof(IAsyncEnumerable<KeyValuePair<Slice, Slice>>)));
			Assert.That(expr.ElementType, Is.EqualTo(typeof(KeyValuePair<Slice, Slice>)));

			Log(FdbQueryExpressions.ExplainSequence(expr));
		}

	}

}
