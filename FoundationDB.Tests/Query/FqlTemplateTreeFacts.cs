
#region Copyright (c) 2023-2025 SnowBank SAS
//
// All rights are reserved. Reproduction or transmission in whole or in part, in
// any form or by any means, electronic, mechanical or otherwise, is prohibited
// without the prior written consent of the copyright owner.
//
#endregion

#if NET8_0_OR_GREATER

namespace FoundationDB.Client.Tests
{
	using NUnit.Framework.Internal.Execution;
	using SnowBank.Data.Tuples.Binary;

	[TestFixture]
	[Category("Fdb-Client-InProc")]
	[Parallelizable(ParallelScope.All)]
	public class FqlTemplateTreeFacts : FdbSimpleTest
	{

		[Test]
		public void Test_Can_Build_Routing_Tree()
		{
			var builder = FqlTemplateTree.CreateBuilder();

			builder.Add("Events", FqlTupleExpression.Create().Integer(0, "RECORDS").VarString("id").Integer(0, "EVENTS").VarInteger("gen").VarVStamp("rev").MaybeMore(), (_) => FqlValueTypeHint.Json);
			builder.Add("Snapshots", FqlTupleExpression.Create().Integer(0, "RECORDS").VarString("id").Integer(1, "SNAPSHOTS").VarInteger("gen").VarVStamp("rev"), (_) => FqlValueTypeHint.VersionStamp);
			builder.Add("Logs", FqlTupleExpression.Create().Integer(1, "LOGS").VarVStamp("cursor"), (_) => FqlValueTypeHint.Tuple);
			builder.Add("Servers", FqlTupleExpression.Create().Integer(2, "SERVERS").VarString("id"), (_) => FqlValueTypeHint.Tuple);
			builder.Add("Cursors", FqlTupleExpression.Create().Integer(3, "CURSORS").VarVStamp("cursor").VarString("id"), (_) => FqlValueTypeHint.None);

			var tree = builder.BuildTree();
			//Dump(tree);

			Assert.That(tree, Is.Not.Null);
			Assert.That(tree.GetTemplates().ToArray(), Has.Length.EqualTo(5));

			static void VerifyIsMatch<TTuple>(FqlTemplateTree tree, TTuple tuple, string expectedName, FqlValueTypeHint expectedHint)
				where TTuple : IVarTuple
			{
				var packed = TuPack.Pack(tuple);
				var unpacked = SpanTuple.Unpack(packed);
				Log($"# key: {tuple}");
				Assert.That(tree.TryMatch(unpacked, out var match), Is.True, $"No match found for key {tuple}");
				Assert.That(match, Is.Not.Null);

				Log($"> result: Name = {match!.Name}, Expr={match.Expression}");
				Assert.That(match.Name, Is.EqualTo(expectedName));
				var hint = match.Hinter(unpacked);
				Log($"> hint: {hint}");
				Assert.That(hint, Is.EqualTo(expectedHint));
				Log();
			}

			VerifyIsMatch(
				tree,
				STuple.Create(0, "user42", 0, 123, VersionStamp.Complete(456, 789)),
				"Events",
				FqlValueTypeHint.Json
			);

			VerifyIsMatch(
				tree,
				STuple.Create(0, "user42", 1, 123, VersionStamp.Complete(456, 789)),
				"Snapshots",
				FqlValueTypeHint.VersionStamp
			);

			VerifyIsMatch(
				tree,
				STuple.Create(1, VersionStamp.Complete(123, 456)),
				"Logs",
				FqlValueTypeHint.Tuple
			);

			VerifyIsMatch(
				tree,
				STuple.Create(2, "host123"),
				"Servers",
				FqlValueTypeHint.Tuple
			);

			VerifyIsMatch(
				tree,
				STuple.Create(3, VersionStamp.Complete(123, 456), "host123"),
				"Cursors",
				FqlValueTypeHint.None
			);

		}

	}

}

#endif
