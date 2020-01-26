#region BSD License
/* Copyright (c) 2013-2020, Doxense SAS
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
	using JetBrains.Annotations;

	/// <summary>Defines a set of options for the network thread</summary>
	[PublicAPI]
	public enum FdbNetworkOption
	{
		/// <summary>None</summary>
		None = 0,

		/// <summary>Deprecated</summary>
		[Obsolete]
		LocalAddress = 10,

		/// <summary>Enable the object serializer for network communication</summary>
		/// <remarks>0 is false, every other value is true</remarks>
		UseObjectSerializer = 11,

		/// <summary>Deprecated</summary>
		[Obsolete]
		ClusterFile = 20, //TODO: should we remove this? We don't support older API versions ....

		/// <summary>Enables trace output to a file in a directory of the clients choosing.</summary>
		/// <remarks>Parameter: (String) path to output directory (or NULL for current working directory)</remarks>
		TraceEnable = 30,

		/// <summary>Sets the maximum size in bytes of a single trace output file.
		/// This value should be in the range ``[0, long.MaxValue]``.
		/// If the value is set to 0, there is no limit on individual file size.
		/// The default is a maximum size of 10,485,760 bytes.
		/// </summary>
		/// <remarks>Parameter: (Int64) max size of a single trace output file</remarks>
		TraceRollSize = 31,

		/// <summary>Sets the maximum size of a all the trace output files put together.
		/// This value should be in the range ``[0, long.MaxValue]``.
		/// If the value is set to 0, there is no limit on the total size of the files.
		/// The default is a maximum size of 104,857,600 bytes.
		/// If the default roll size is used, this means that a maximum of 10 trace files will be written at a time.
		/// </summary>
		/// <remarks>Parameter: (Int64) max total size of trace files</remarks>
		TraceMaxLogsSize = 32,

		/// <summary>Sets the 'LogGroup' attribute with the specified value for all events in the trace output files.
		/// The default log group is 'default'.</summary>
		/// <remarks>Parameter: (String) value of the LogGroup attribute</remarks>
		TraceLogGroup = 33,

		/// <summary>Select the format of the log files. xml (the default) and json are supported.</summary>
		/// <remarks>Parameter: (String) format of trace files</remarks>
		TraceFormat = 34,

		/// <summary>Set internal tuning or debugging knobs </summary>
		/// <remarks>Parameter: (String) knob_name=knob_value</remarks>
		Knob = 40,

		/// <summary>Set the TLS plugin to load.
		/// This option, if used, must be set before any other TLS options
		/// Parameter: (String) file path or linker-resolved name
		/// </summary>
		[Obsolete("This option is deprecated since v6.0")]
		TlsPlugin = 41,

		/// <summary>Set the certificate chain</summary>
		/// <remarks>Parameter: (Bytes) certificates</remarks>
		TlsCertBytes = 42,

		/// <summary>Set the file from which to load the certificate chain</summary>
		/// <remarks>Parameter: (String) File path</remarks>
		TlsCertPath = 43,

		//note: there is no entry for 44

		/// <summary>Set the private key corresponding to your own certificate</summary>
		/// <remarks>Parameter: (Bytes) Key</remarks>
		TlsKeyBytes = 45,

		/// <summary>Set the file from which to load the private key corresponding to your own certificate</summary>
		/// <remarks>Parameter: (String) File path</remarks>
		TlsKeyPath = 46,

		/// <summary>Set the peer certificate field verification criteria</summary>
		/// <remarks>Parameter: (Bytes) Verification pattern</remarks>
		TlsVerifyPeers = 47,

		BuggifyEnable = 48,

		BuggifyDisable = 49,

		/// <summary>Set the probability of a BUGGIFY section being active for the current execution.
		/// Only applies to code paths first traversed AFTER this option is changed.</summary>
		BuggifySectionActivatedProbability = 50,

		/// <summary>Set the probability of an active BUGGIFY section being fired</summary>
		BuggifySectionFiredProbability = 51,

		/// <summary>Set the ca bundle.</summary>
		TlsCaBytes = 52,

		/// <summary>Set the file from which to load the certificate authority bundle.</summary>
		TlsCaPath = 53,

		/// <summary>Set the passphrase for encrypted private key. Password should be set before setting the key for the password to be used.</summary>
		/// <remarks>Parameter: (String) key passphrase</remarks>
		TlsPassword = 54,

		/// <summary>Disables the multi-version client API and instead uses the local client directly. Must be set before setting up the network.</summary>
		DisableMultiVersionClientApi = 60,

		/// <summary>If set, callbacks from external client libraries can be called from threads created by the FoundationDB client library.
		/// Otherwise, callbacks will be called from either the thread used to add the callback or the network thread.
		/// Setting this option can improve performance when connected using an external client, but may not be safe to use in all environments.
		/// Must be set before setting up the network.</summary>
		/// <remarks>WARNING: This feature is considered experimental at this time.</remarks>
		CallbacksOnExternalThreads = 61,

		/// <summary>Adds an external client library for use by the multi-version client API. Must be set before setting up the network.</summary>
		ExternalClientLibrary = 62,

		/// <summary>Searches the specified path for dynamic libraries and adds them to the list of client libraries for use by the multi-version client API. Must be set before setting up the network.</summary>
		ExternalClientDirectory = 63,

		/// <summary>Prevents connections through the local client, allowing only connections through externally loaded client libraries. Intended primarily for testing.</summary>
		DisableLocalClient = 64,

		/// <summary>Disables logging of client statistics, such as sampled transaction activity.</summary>
		DisableClientStatisticsLogging = 70,

		/// <summary>Enables debugging feature to perform slow task profiling. Requires trace logging to be enabled. WARNING: this feature is not recommended for use in production.</summary>
		EnableSlowTaskProfiling = 71,

		/// <summary>Enable client buggify - will make requests randomly fail (intended for client testing)</summary>
		ClientBuggifyEnable = 80,

		/// <summary>Disable client buggify</summary>
		ClientBuggifyDisable = 81,

		/// <summary>Set the probability of a CLIENT_BUGGIFY section being active for the current execution.</summary>
		ClientBuggifySectionActivatedProbability = 82,

		/// <summary>Set the probability of an active CLIENT_BUGGIFY section being fired. A section will only fire if it was activated</summary>
		ClientBuggifySectionFiredProbability = 83,

		/// <summary>This option is set automatically to communicate the list of supported clients to the active client.</summary>
		SupportedClientVersions = 1000,

		/// <summary>This option is set automatically on all clients loaded externally using the multi-version API.</summary>
		ExternalClient = 1001,

		/// <summary>This option tells a child on a multiversion client what transport ID to use.</summary>
		ExternalClientTransportId = 1002,

	}

}
