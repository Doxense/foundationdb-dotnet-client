﻿#region Copyright (c) 2023-2024 SnowBank SAS, (c) 2005-2023 Doxense SAS
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

namespace FoundationDB.Client.Tests
{
	using System;
	using NUnit.Framework;
	using FoundationDB.Client;
	using FoundationDB.Client.Native;

	[TestFixture]
	public class ErrorFacts
	{

		[Test]
		public void Test_Fdb_GetErrorMessage()
		{
			Assert.That(FdbNative.GetErrorMessage(FdbError.Success), Is.EqualTo("Success"));
			Assert.That(FdbNative.GetErrorMessage(FdbError.OperationFailed), Is.EqualTo("Operation failed"));
			Assert.That(FdbNative.GetErrorMessage(FdbError.TransactionTooOld), Is.EqualTo("Transaction is too old to perform reads or be committed"));
			Assert.That(FdbNative.GetErrorMessage(FdbError.FutureVersion), Is.EqualTo("Request for future version"));
			Assert.That(FdbNative.GetErrorMessage(FdbError.TimedOut), Is.EqualTo("Operation timed out"));
			Assert.That(FdbNative.GetErrorMessage(FdbError.NotCommitted), Is.EqualTo("Transaction not committed due to conflict with another transaction"));
			Assert.That(FdbNative.GetErrorMessage(FdbError.CommitUnknownResult), Is.EqualTo("Transaction may or may not have committed"));
			Assert.That(FdbNative.GetErrorMessage(FdbError.TransactionCancelled), Is.EqualTo("Operation aborted because the transaction was cancelled"));
			Assert.That(FdbNative.GetErrorMessage(FdbError.TransactionTimedOut), Is.EqualTo("Operation aborted because the transaction timed out"));
			Assert.That(FdbNative.GetErrorMessage(FdbError.ClientInvalidOperation), Is.EqualTo("Invalid API call"));
			Assert.That(FdbNative.GetErrorMessage(FdbError.LargeAllocFailed), Is.EqualTo("Large block allocation failed"));
		}

		[Test]
		public void Test_Fdb_MapToException()
		{
			Assert.That(FdbNative.MapToException(FdbError.Success), Is.Null, "Success");
			Assert.That(FdbNative.MapToException(FdbError.OperationFailed), Is.InstanceOf<FdbException>().And.Property("Code").EqualTo(FdbError.OperationFailed), "OperationFailed");
			Assert.That(FdbNative.MapToException(FdbError.TransactionTooOld), Is.InstanceOf<FdbException>().And.Property("Code").EqualTo(FdbError.TransactionTooOld), "PastVersion");
			Assert.That(FdbNative.MapToException(FdbError.FutureVersion), Is.InstanceOf<FdbException>().And.Property("Code").EqualTo(FdbError.FutureVersion), "FutureVersion");
			Assert.That(FdbNative.MapToException(FdbError.TimedOut), Is.InstanceOf<TimeoutException>(), "TimedOut");
			Assert.That(FdbNative.MapToException(FdbError.NotCommitted), Is.InstanceOf<FdbException>().And.Property("Code").EqualTo(FdbError.NotCommitted), "NotCommitted");
			Assert.That(FdbNative.MapToException(FdbError.CommitUnknownResult), Is.InstanceOf<FdbException>().And.Property("Code").EqualTo(FdbError.CommitUnknownResult), "CommitUnknownResult");
			Assert.That(FdbNative.MapToException(FdbError.TransactionCancelled), Is.InstanceOf<FdbException>().And.Property("Code").EqualTo(FdbError.TransactionCancelled), "TrasactionCancelled"); //REVIEW => OperationCancelledException?
			Assert.That(FdbNative.MapToException(FdbError.TransactionTimedOut), Is.InstanceOf<FdbException>().And.Property("Code").EqualTo(FdbError.TransactionTimedOut), "TransactionTimedOut"); //REVIEW => TimeoutException ?
			Assert.That(FdbNative.MapToException(FdbError.ClientInvalidOperation), Is.InstanceOf<FdbException>().And.Property("Code").EqualTo(FdbError.ClientInvalidOperation), "ClientInvalidOperation"); //REVIEW => InvalidOperationException?
			Assert.That(FdbNative.MapToException(FdbError.LargeAllocFailed), Is.InstanceOf<OutOfMemoryException>(), "LargeAllocFailed");
			Assert.That(FdbNative.MapToException(FdbError.InvalidOption), Is.InstanceOf<ArgumentException>(), "InvalidOption");
		}

		[Test]
		public void Test_Fdb_Error_Predicate()
		{
			// Retryable
			Assert.That(FdbNative.TestErrorPredicate(FdbErrorPredicate.Retryable, FdbError.NotCommitted), Is.True, "Retryable + NotCommited: YES");
			Assert.That(FdbNative.TestErrorPredicate(FdbErrorPredicate.Retryable, FdbError.FutureVersion), Is.True, "Retryable + FutureVersion: YES");
			Assert.That(FdbNative.TestErrorPredicate(FdbErrorPredicate.Retryable, FdbError.TransactionTooOld), Is.True, "Retryable + PastVersion: YES");
			Assert.That(FdbNative.TestErrorPredicate(FdbErrorPredicate.Retryable, FdbError.ApiVersionInvalid), Is.False, "Retryable + ApiVersionInvalid: NO");
			Assert.That(FdbNative.TestErrorPredicate(FdbErrorPredicate.Retryable, FdbError.CommitUnknownResult), Is.True, "Retryable + CommitUnknownResult: YES"); // may have committed => true (but check on retries!)

			// MaybeCommitted
			Assert.That(FdbNative.TestErrorPredicate(FdbErrorPredicate.MaybeCommitted, FdbError.NotCommitted), Is.False, "MaybeCommitted + NotCommited: NO");
			Assert.That(FdbNative.TestErrorPredicate(FdbErrorPredicate.MaybeCommitted, FdbError.FutureVersion), Is.False, "MaybeCommitted + FutureVersion: YES");
			Assert.That(FdbNative.TestErrorPredicate(FdbErrorPredicate.MaybeCommitted, FdbError.TransactionTooOld), Is.False, "MaybeCommitted + PastVersion: YES");
			Assert.That(FdbNative.TestErrorPredicate(FdbErrorPredicate.MaybeCommitted, FdbError.ApiVersionInvalid), Is.False, "MaybeCommitted + ApiVersionInvalid: NO");
			Assert.That(FdbNative.TestErrorPredicate(FdbErrorPredicate.MaybeCommitted, FdbError.CommitUnknownResult), Is.True, "MaybeCommitted + CommitUnknownResult: YES"); // may have committed => true

			// RetryableNotCommited
			Assert.That(FdbNative.TestErrorPredicate(FdbErrorPredicate.RetryableNotCommited, FdbError.NotCommitted), Is.True, "RetryableNotCommited + NotCommited: YES");
			Assert.That(FdbNative.TestErrorPredicate(FdbErrorPredicate.RetryableNotCommited, FdbError.FutureVersion), Is.True, "RetryableNotCommited + FutureVersion: YES");
			Assert.That(FdbNative.TestErrorPredicate(FdbErrorPredicate.RetryableNotCommited, FdbError.TransactionTooOld), Is.True, "RetryableNotCommited + PastVersion: YES");
			Assert.That(FdbNative.TestErrorPredicate(FdbErrorPredicate.RetryableNotCommited, FdbError.ApiVersionInvalid), Is.False, "RetryableNotCommited + ApiVersionInvalid: NO");
			Assert.That(FdbNative.TestErrorPredicate(FdbErrorPredicate.RetryableNotCommited, FdbError.CommitUnknownResult), Is.False, "RetryableNotCommited + CommitUnknownResult: YES"); // may have committed => false
		}

	}
}
