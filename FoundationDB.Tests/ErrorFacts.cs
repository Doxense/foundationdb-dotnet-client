#region BSD License
/* Copyright (c) 2013-2018, Doxense SAS
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
	using FoundationDB.Client;
	using NUnit.Framework;
	using System;

	[TestFixture]
	public class ErrorFacts
	{

		[Test]
		public void Test_Fdb_GetErrorMessage()
		{
			Assert.That(Fdb.GetErrorMessage(FdbError.Success), Is.EqualTo("Success"));
			Assert.That(Fdb.GetErrorMessage(FdbError.OperationFailed), Is.EqualTo("Operation failed"));
			Assert.That(Fdb.GetErrorMessage(FdbError.PastVersion), Is.EqualTo("Transaction is too old to perform reads or be committed"));
			Assert.That(Fdb.GetErrorMessage(FdbError.FutureVersion), Is.EqualTo("Request for future version"));
			Assert.That(Fdb.GetErrorMessage(FdbError.TimedOut), Is.EqualTo("Operation timed out"));
			Assert.That(Fdb.GetErrorMessage(FdbError.NotCommitted), Is.EqualTo("Transaction not committed due to conflict with another transaction"));
			Assert.That(Fdb.GetErrorMessage(FdbError.CommitUnknownResult), Is.EqualTo("Transaction may or may not have committed"));
			Assert.That(Fdb.GetErrorMessage(FdbError.TransactionCancelled), Is.EqualTo("Operation aborted because the transaction was cancelled"));
			Assert.That(Fdb.GetErrorMessage(FdbError.TransactionTimedOut), Is.EqualTo("Operation aborted because the transaction timed out"));
			Assert.That(Fdb.GetErrorMessage(FdbError.ClientInvalidOperation), Is.EqualTo("Invalid API call"));
			Assert.That(Fdb.GetErrorMessage(FdbError.LargeAllocFailed), Is.EqualTo("Large block allocation failed"));
		}

		[Test]
		public void Test_Fdb_MapToException()
		{
			Assert.That(Fdb.MapToException(FdbError.Success), Is.Null, "Success");
			Assert.That(Fdb.MapToException(FdbError.OperationFailed), Is.InstanceOf<FdbException>().And.Property("Code").EqualTo(FdbError.OperationFailed), "OperationFailed");
			Assert.That(Fdb.MapToException(FdbError.PastVersion), Is.InstanceOf<FdbException>().And.Property("Code").EqualTo(FdbError.PastVersion), "PastVersion");
			Assert.That(Fdb.MapToException(FdbError.FutureVersion), Is.InstanceOf<FdbException>().And.Property("Code").EqualTo(FdbError.FutureVersion), "FutureVersion");
			Assert.That(Fdb.MapToException(FdbError.TimedOut), Is.InstanceOf<TimeoutException>(), "TimedOut");
			Assert.That(Fdb.MapToException(FdbError.NotCommitted), Is.InstanceOf<FdbException>().And.Property("Code").EqualTo(FdbError.NotCommitted), "NotCommitted");
			Assert.That(Fdb.MapToException(FdbError.CommitUnknownResult), Is.InstanceOf<FdbException>().And.Property("Code").EqualTo(FdbError.CommitUnknownResult), "CommitUnknownResult");
			Assert.That(Fdb.MapToException(FdbError.TransactionCancelled), Is.InstanceOf<FdbException>().And.Property("Code").EqualTo(FdbError.TransactionCancelled), "TrasactionCancelled"); //REVIEW => OperationCancelledException?
			Assert.That(Fdb.MapToException(FdbError.TransactionTimedOut), Is.InstanceOf<FdbException>().And.Property("Code").EqualTo(FdbError.TransactionTimedOut), "TransactionTimedOut"); //REVIEW => TimeoutException ?
			Assert.That(Fdb.MapToException(FdbError.ClientInvalidOperation), Is.InstanceOf<FdbException>().And.Property("Code").EqualTo(FdbError.ClientInvalidOperation), "ClientInvalidOperation"); //REVIEW => InvalidOperationException?
			Assert.That(Fdb.MapToException(FdbError.LargeAllocFailed), Is.InstanceOf<OutOfMemoryException>(), "LargeAllocFailed");
		}

	}
}
