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

	/// <summary>Defines a set of options for the network thread</summary>
	public enum FdbNetworkOption
	{
		/// <summary>None</summary>
		None = 0,

		/// <summary>
		/// Deprecated
		/// Parameter: (String) IP:PORT
		/// </summary>
		LocalAddress = 10, //TODO: should we remove this? We don't support older API versions ....

		/// <summary>
		/// Deprecated
		/// Parameter: (String) Path to cluster file
		/// </summary>
		ClusterFile = 20, //TODO: should we remove this? We don't support older API versions ....

		/// <summary>Enables trace output to a file in a directory of the clients choosing.
		/// Parameter: (String) path to output directory (or NULL for current working directory)
		/// </summary>
		TraceEnable = 30,

		/// <summary>Sets the maximum size in bytes of a single trace output file.
		/// This value should be in the range ``[0, long.MaxValue]``.
		/// If the value is set to 0, there is no limit on individual file size.
		/// The default is a maximum size of 10,485,760 bytes.
		/// Parameter: (Int64) max size of a single trace output file
		/// </summary>
		TraceRollSize = 31,

		/// <summary>Sets the maximum size of a all the trace output files put together.
		/// This value should be in the range ``[0, long.MaxValue]``.
		/// If the value is set to 0, there is no limit on the total size of the files.
		/// The default is a maximum size of 104,857,600 bytes.
		/// If the default roll size is used, this means that a maximum of 10 trace files will be written at a time.
		/// Parameter: (Int64) max total size of trace files
		/// </summary>
		TraceMaxLogsSize = 32,

		/// <summary>Set internal tuning or debugging knobs
		/// Parameter: (String) knob_name=knob_value
		/// </summary>
		Knob = 40,

		/// <summary>Set the TLS plugin to load.
		/// This option, if used, must be set before any other TLS options
		/// Parameter: (String) file path or linker-resolved name
		/// </summary>
		TLSPlugin = 41,

		/// <summary>Set the certificate chain
		/// Parameter: (Bytes) certificates
		/// </summary>
		TLSCertBytes = 42,

		/// <summary>Set the file from which to load the certificate chain
		/// Parameter: (String) File path
		/// </summary>
		TLSCertPath = 43,

		/// <summary>Set the private key corresponding to your own certificate
		/// Parameter: (Bytes) Key
		/// </summary>
		TLSKeyBytes = 45,

		/// <summary>Set the file from which to load the private key corresponding to your own certificate
		/// Parameter: (String) File path
		/// </summary>
		TLSKeyPath = 46,

		/// <summary>Set the peer certificate field verification criteria
		/// Parameter: (Bytes) Verification pattern
		/// </summary>
		TLSVerifyPeers = 47,
	}

}
