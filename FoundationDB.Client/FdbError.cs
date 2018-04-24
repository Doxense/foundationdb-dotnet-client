#region BSD Licence
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

namespace FoundationDB.Client
{
	using System;

	/// <summary>FoundationDB API Error Code</summary>
	public enum FdbError
	{
		/// <summary>Success</summary>
		Success = 0,

		/// <summary>Operation failed</summary>
		OperationFailed = 1000,
		/// <summary> Operation timed out</summary>
		TimedOut = 1004,
		/// <summary>Version no longer available</summary>
		PastVersion = 1007,
		/// <summary>Request for future version</summary>
		FutureVersion = 1009,
		/// <summary>Transaction not committed</summary>
		NotCommitted = 1020,
		/// <summary>Transaction may or may not have committed</summary>
		CommitUnknownResult = 1021,
		/// <summary>Operation aborted because the transaction was cancelled</summary>
		TransactionCancelled = 1025,
		/// <summary>Operation aborted because the transaction timed out</summary>
		TransactionTimedOut = 1031,
		/// <summary>Too many watches are currently set</summary>
		TooManyWatches = 1032,
		/// <summary>Disabling read your writes also disables watches</summary>
		WatchesDisabled = 1034,
		/// <summary>Broken Promise [UNDOCUMENTED]</summary>
		BrokenPromise = 1100,
		/// <summary>Asynchronous operation cancelled</summary>
		OperationCancelled = 1101,
		/// <summary>The future has been released</summary>
		FutureReleased = 1102,
		/// <summary>A platform error occurred</summary>
		PlatformError = 1500,
		/// <summary>Large block allocation failed</summary>
		LargeAllocFailed = 1501,
		/// <summary>QueryPerformanceCounter doesn’t work</summary>
		PerformanceCounterError = 1502,
		/// <summary>A disk i/o operation failed</summary>
		IOError = 1510,
		/// <summary>File not found</summary>
		FileNotFound = 1511,
		/// <summary>Unable to bind to network</summary>
		BindFailed = 1512,
		/// <summary>File could not be read from</summary>
		FileNotReadable = 1513,
		/// <summary>File could not be written to</summary>
		FileNotWriteable = 1514,
		/// <summary>No cluster file found in current directory or default location</summary>
		NoClusterFileFound = 1515,
		/// <summary>Cluster file to large to be read</summary>
		ClusterFileTooLarge = 1516,
		/// <summary>The client made an invalid API call</summary>
		ClientInvalidOperation = 2000,
		/// <summary>Commit with incomplete read</summary>
		CommitReadIncomplete = 2002,
		/// <summary>The test specification is invalid</summary>
		TestSpecificationInvalid = 2003,
		/// <summary>The specified key was outside the legal range</summary>
		KeyOutsideLegalRange = 2004,
		/// <summary>The specified range has a begin key larger than the end key</summary>
		InvertedRange = 2005,
		/// <summary>An invalid value was passed with the specified option</summary>
		InvalidOptionValue = 2006,
		/// <summary>Option not valid in this context</summary>
		InvalidOption = 2007,
		/// <summary>Action not possible before the network is configured</summary>
		NetworkNotSetup = 2008,
		/// <summary>Network can be configured only once</summary>
		NetworkAlreadySetup = 2009,
		/// <summary>Transaction already has a read version set</summary>
		ReadVersionAlreadySet = 2010,
		/// <summary>Version not valid</summary>
		VersionInvalid = 2011,
		/// <summary>getRange limits not valid</summary>
		RangeLimitsInvalid = 2012,
		/// <summary>Database name not supported in this version</summary>
		InvalidDatabaseName = 2013,
		/// <summary>Attribute not found in string</summary>
		AttributeNotFound = 2014,
		/// <summary>The future has not been set</summary>
		FutureNotSet = 2015,
		/// <summary>The future is not an error</summary>
		FutureNotError = 2016,
		/// <summary>An operation was issued while a commit was outstanding</summary>
		UsedDuringCommit = 2017,
		/// <summary>An invalid atomic mutation type was issued</summary>
		InvalidMutationType = 2018,
		/// <summary>Incompatible protocol version</summary>
		IncompatibleProtocolVersion = 2100,
		/// <summary>Transaction too large</summary>
		TransactionTooLarge = 2101,
		/// <summary>Key too large</summary>
		KeyTooLarge = 2102,
		/// <summary>Value too large</summary>
		ValueTooLarge = 2103,
		/// <summary>Connection string invalid</summary>
		ConnectionStringInvalid = 2104,
		/// <summary>Local address in use</summary>
		AddressInUse = 2105,
		/// <summary>Invalid local address</summary>
		InvalidLocalAddress = 2106,
		/// <summary>TLS error</summary>
		TlsError = 2107,
		/// <summary>Api version must be set</summary>
		ApiVersionUnset = 2200,
		/// <summary>Api version may be set only once</summary>
		ApiVersionAlreadySet = 2201,
		/// <summary>Api version not valid</summary>
		ApiVersionInvalid = 2202,
		/// <summary>Api version not supported in this version or binding</summary>
		ApiVersionNotSupported = 2203,
		/// <summary>EXACT streaming mode requires limits, but none were given</summary>
		ExactModeWithoutLimits = 2210,
		/// <summary>An unknown error occurred</summary>
		UnknownError = 4000,
		/// <summary>An internal error occurred</summary>
		InternalError = 4100,
	}

}
