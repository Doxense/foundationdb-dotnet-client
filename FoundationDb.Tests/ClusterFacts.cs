using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;
using FoundationDb.Client;
using System.Threading.Tasks;
using System.Threading;

namespace FoundationDb.Tests
{

	[TestFixture]
	public class ClusterFacts
	{

		[Test]
		public async Task Test_Can_Connect_To_Local_Cluster()
		{
			using(var cluster = await Fdb.OpenLocalClusterAsync())
			{
				Assert.That(cluster, Is.Not.Null, "Should return a valid object");
				Assert.That(cluster.Path, Is.Null, "FdbCluster.Path should be null");
			}
		}

		[Test]
		public void Test_Connecting_To_Cluster_With_Cancelled_Token_Should_Fail()
		{
			using (var cts = new CancellationTokenSource())
			{
				cts.Cancel();

				Assert.Throws<OperationCanceledException>(() => Fdb.OpenLocalClusterAsync(cts.Token).GetAwaiter().GetResult());
			}
		}

	}
}
