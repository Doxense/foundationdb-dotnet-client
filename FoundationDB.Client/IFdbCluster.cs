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
	using JetBrains.Annotations;
	using System;
	using System.Threading;
	using System.Threading.Tasks;
	using Doxense.Serialization.Encoders;

	/// <summary>Cluster connection context.</summary>
	[PublicAPI]
	public interface IFdbCluster : IDisposable
	{
		/// <summary>Path to the cluster file used by this connection, or null if the default cluster file is being used</summary>
		[CanBeNull]
		string Path { get; }

		/// <summary>Set an option on this cluster that does not take any parameter</summary>
		/// <param name="option">Option to set</param>
		void SetOption(FdbClusterOption option);

		/// <summary>Set an option on this cluster that takes a string value</summary>
		/// <param name="option">Option to set</param>
		/// <param name="value">Value of the parameter (can be null)</param>
		void SetOption(FdbClusterOption option, string value);

		/// <summary>Set an option on this cluster that takes an integer value</summary>
		/// <param name="option">Option to set</param>
		/// <param name="value">Value of the parameter</param>
		void SetOption(FdbClusterOption option, long value);

		/// <summary>Opens a database on this cluster, configured to only access a specific subspace of keys</summary>
		/// <param name="databaseName">Name of the database. Must be 'DB' (as of Beta 2)</param>
		/// <param name="rootContext">Root key context of all the keys that will be accessed.</param>
		/// <param name="keyEncoding">Default key encoding for the global keyspace</param>
		/// <param name="readOnly">If true, the database will only allow read operations.</param>
		/// <param name="ct">Cancellation Token (optionnal) for the connect operation</param>
		/// <returns>Task that will return an FdbDatabase, or an exception</returns>
		[ItemNotNull]
		Task<IFdbDatabase> OpenDatabaseAsync(string databaseName, IKeyContext rootContext, IKeyEncoding keyEncoding, bool readOnly, CancellationToken ct);
	}

}
