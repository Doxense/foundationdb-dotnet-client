
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
	using SnowBank.Data.Tuples.Binary;

	[TestFixture]
	[Category("Fdb-Client-InProc")]
	[Parallelizable(ParallelScope.All)]
	public class FqlRoutingFacts : FdbSimpleTest
	{

		[Test]
		public void Test_Can_Build_Routing_Tree()
		{
			var builder = FqlTupleExpressionTree.CreateBuilder();

			builder.AddExpression(FqlTupleExpression.Create().Integer(0, "RECORDS").VarString("id").Integer(0, "EVENTS").VarInteger("gen").VarVStamp("rev").MaybeMore());
			builder.AddExpression(FqlTupleExpression.Create().Integer(0, "RECORDS").VarString("id").Integer(1, "SNAPSHOTS").VarInteger("gen").VarVStamp("rev"));
			builder.AddExpression(FqlTupleExpression.Create().Integer(1, "LOGS").VarVStamp("cursor"));
			builder.AddExpression(FqlTupleExpression.Create().Integer(2, "SERVERS").VarString("id"));
			builder.AddExpression(FqlTupleExpression.Create().Integer(3, "CURSORS").VarVStamp("cursor").VarString("id"));

			var tree = builder.BuildDfaTree();
			//Dump(tree);

			{
				var tuple = STuple.Create(0, "user42", 0, 123, VersionStamp.Complete(456, 789));
				Log($"> key: {tuple}");
				var match = tree.Match(SlicedTuple.Repack(tuple));
				Assert.That(match, Is.Not.Null);
				Log($"> result: {match}");
			}
			{
				var tuple = STuple.Create(0, "user42", 1, 123, VersionStamp.Complete(456, 789));
				Log($"> key: {tuple}");
				var match = tree.Match(SlicedTuple.Repack(tuple));
				Assert.That(match, Is.Not.Null);
				Log($"> result: {match}");
			}
			{
				var tuple = STuple.Create(1, VersionStamp.Complete(123, 456));
				Log($"> key: {tuple}");
				var match = tree.Match(SlicedTuple.Repack(tuple));
				Assert.That(match, Is.Not.Null);
				Log($"> result: {match}");
			}
			{
				var tuple = STuple.Create(2, "host123");
				Log($"> key: {tuple}");
				var match = tree.Match(SlicedTuple.Repack(tuple));
				Assert.That(match, Is.Not.Null);
				Log($"> result: {match}");
			}
			{
				var tuple = STuple.Create(3, VersionStamp.Complete(123, 456), "host123");
				Log($"> key: {tuple}");
				var match = tree.Match(SlicedTuple.Repack(tuple));
				Assert.That(match, Is.Not.Null);
				Log($"> result: {match}");
			}

		}

	}

}

#endif
