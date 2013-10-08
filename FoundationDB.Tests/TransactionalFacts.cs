#region BSD Licence
/* Copyright (c) 2013, Doxense SARL
All rights reserved.

Redistribution and use in source and binary forms, with or without
modification, are permitted provided that the following conditions are met:
	* Redistributions of source code must retain the above copyright
	  notice, this list of conditions and the following disclaimer.
	* Redistributions in binary form must reproduce the above copyright
	  notice, this list of conditions and the following disclaimer in the
	  documentation and/or other materials provided with the distribution.
	* Neither the name of Doxense nor the
	  names of its contributors may be used to endorse or promote products
	  derived from this software without specific prior written permission.

THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND
ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
DISCLAIMED. IN NO EVENT SHALL <COPYRIGHT HOLDER> BE LIABLE FOR ANY
DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
(INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
(INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
 */
#endregion

namespace FoundationDB.Client.Tests
{
	using NUnit.Framework;
	using System;
	using System.Threading;
	using System.Threading.Tasks;

	[TestFixture]
	public class TransactionalFacts
	{
		[Test]
		public async Task Test_ReadAsync_Should_Normally_Execute_Only_Once()
		{
			using (var db = await TestHelpers.OpenTestPartitionAsync())
			{
				var location = await db.CreateOrOpenDirectoryAsync(new[] { "Transactionals" });

				string secret = Guid.NewGuid().ToString();

				using(var tr = db.BeginTransaction())
				{
					tr.Set(location.Pack("Hello"), Slice.FromString(secret));
					await tr.CommitAsync();
				}

				int called = 0;
				Slice result = await db.ReadAsync<Slice>((tr) =>
				{
					++called;
					Assert.That(tr, Is.Not.Null);
					Assert.That(tr.Context, Is.Not.Null);
					Assert.That(tr.Context.Database, Is.SameAs(db));
					Assert.That(tr.Context.Shared, Is.True);

					return tr.GetAsync(location.Pack("Hello"));
				});

				Assert.That(called, Is.EqualTo(1)); // note: if this assert fails, first ensure that you did not get a transient error while running this test!
				Assert.That(result.ToUnicode(), Is.EqualTo(secret));
			}
		}

		[Test]
		public async Task Test_Transactionals_Rethrow_Regular_Exceptions()
		{
			using (var db = await TestHelpers.OpenTestPartitionAsync())
			{
				int called = 0;

				// ReadAsync should return a failed Task, and not bubble up the exception.
				var task = db.ReadAsync((tr) =>
				{
					Assert.That(called, Is.EqualTo(0), "ReadAsync should not retry on regular exceptions");
					++called;
					throw new InvalidOperationException("Boom");
				});
				Assert.That(task, Is.Not.Null);
				// the exception should be unwrapped (ie: we should not see an AggregateException, but the actual exception)
				await TestHelpers.AssertThrowsAsync<InvalidOperationException>(() => task, "ReadAsync should rethrow any regular exceptions");
			}

		}

		[Test]
		public async Task Test_Transactionals_Retries_On_Transient_Errors()
		{
			using (var db = await TestHelpers.OpenTestPartitionAsync())
			{
				var location = await db.CreateOrOpenDirectoryAsync(new[] { "Transactionals" });
				await db.ClearRangeAsync(location);

				int called = 0;
				int? id = null;

				using (var go = new CancellationTokenSource())
				{
					// ReadAsync should return a failed Task, and not bubble up the exception.
					var res = await db.ReadAsync((tr) =>
					{
						try
						{
							++called;

							Assert.That(tr, Is.Not.Null);
							Assert.That(tr.Context, Is.Not.Null, "tr.Context should not be null");
							Assert.That(tr.Context.Retries, Is.EqualTo(called - 1), "tr.Context.Retries should equal the number of calls to the handler, minus one");

							if (id == null) id = tr.Id;
							Assert.That(tr.Id, Is.EqualTo(id.Value), "The same transaction should be passed multiple times");

							if (called < 3)
							{ // fool the retry loop into thinking that a retryable error occured
								throw new FdbException(FdbError.PastVersion, "Fake error");
							}
							Assert.That(called, Is.GreaterThanOrEqualTo(3), "The handler should not be called again if it completed previously");
							return Task.FromResult(123);
						}
						catch(AssertionException)
						{ // protection against infinite loops
							go.Cancel();
							throw;
						}
					}, go.Token);

					Assert.That(res, Is.EqualTo(123));
					Assert.That(called, Is.EqualTo(3));
				}
			}

		}

		[Test]
		public async Task Test_Transactionals_Should_Not_Execute_If_Already_Canceled()
		{
			using (var db = await TestHelpers.OpenTestPartitionAsync())
			{
				using (var go = new CancellationTokenSource())
				{
					go.Cancel();

					bool called = false;

					// ReadAsync should return a canceled Task, and never call the handler
					var t = db.ReadAsync((tr) =>
					{
						called = true;
						Console.WriteLine("FAILED");
						throw new InvalidOperationException("Failed");
					}, go.Token);

					Assert.That(t.IsCompleted, "Returned task should already be canceled");
					Assert.That(t.Status, Is.EqualTo(TaskStatus.Canceled), "Returned task should be in the canceled state");
					Assert.That(called, Is.False, "Handler should not be called with an already canceled token");
					var _ = t.Exception;
				}
			}

		}

		[Test]
		public async Task Test_Transactionals_ReadOnly_Should_Deny_Write_Attempts()
		{
			using (var db = await TestHelpers.OpenTestPartitionAsync())
			{
				var location = await db.CreateOrOpenDirectoryAsync(new[] { "Transactionals" });

				var t = db.ReadAsync((IFdbReadOnlyTransaction tr) =>
				{
					Assert.That(tr, Is.Not.Null);

					// force the read-only into a writable interface
					var hijack = tr as IFdbTransaction;
					if (hijack == null) Assert.Inconclusive("This test requires the transaction to implement IFdbTransaction !");

					// this call should fail !
					hijack.Set(location.Pack("Hello"), Slice.FromString("Hijacked"));

					Assert.Fail("Calling Set() on a read-only transaction should fail");
					return Task.FromResult(123);
				});

				await TestHelpers.AssertThrowsAsync<InvalidOperationException>(() => t, "Forcing writes on a read-only transaction should fail");
			}

		}
	}
}
