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

namespace FoundationDB.Layers.Directories
{
	using FoundationDB.Client;
	using FoundationDB.Client.Utils;
	using FoundationDB.Layers.Tuples;
	using System;
	using System.Collections.Generic;
	using System.Diagnostics;
	using System.Threading.Tasks;

	/// <summary>
	/// Represents a directory in the <code>DirectoryLayer</code>. A <code>Directory</code> stores the path at which it is located and the layer that was used to create it.
	/// The IFdbDirectory interface contains methods to operate on itself and its subdirectories.
	/// </summary>
	public interface IFdbDirectory
	{
		/// <summary>Name of this <code>Directory</code>.</summary>
		string Name { get; }

		/// <summary>Gets the path represented by this <code>Directory</code>.</summary>
		IReadOnlyList<string> Path { get;  }

		/// <summary>Gets the layer id slice that was stored when this <code>Directory</code> was created.</summary>
		Slice Layer { get; }

		/// <summary>Get the <code>DirectoryLayer</code> that was used to create this <code>Directory</code>.</summary>
		FdbDirectoryLayer DirectoryLayer { get; }

		/// <summary>Opens a subdirectory with the given path.
		/// If the subdirectory does not exist, it is created (creating intermediate subdirectories if necessary).
		/// If layer is specified, it is checked against the layer of an existing subdirectory or set as the layer of a new subdirectory.
		/// </summary>
		Task<FdbDirectorySubspace> CreateOrOpenAsync(IFdbTransaction trans, IEnumerable<string> subPath, Slice layer = default(Slice));

		/// <summary>Opens a subdirectory with the given <paramref name="path"/>.
		/// An exception is thrown if the subdirectory does not exist, or if a layer is specified and a different layer was specified when the subdirectory was created.
		/// </summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="path">Relative path of the subdirectory to open</param>
		Task<FdbDirectorySubspace> OpenAsync(IFdbTransaction trans, IEnumerable<string> path, Slice layer = default(Slice));

		/// <summary>Opens a subdirectory with the given <paramref name="path"/>.
		/// An exception is thrown if the subdirectory if a layer is specified and a different layer was specified when the subdirectory was created.
		/// </summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="path">Relative path of the subdirectory to open</param>
		/// <returns>Returns the directory if it exists, or null if it was not found</returns>
		Task<FdbDirectorySubspace> TryOpenAsync(IFdbTransaction trans, IEnumerable<string> path, Slice layer = default(Slice));

		/// <summary>Creates a subdirectory with the given <paramref name="path"/> (creating intermediate subdirectories if necessary).
		/// An exception is thrown if the given subdirectory already exists.
		/// </summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="subPath">Relative path of the subdirectory to create</param>
		/// <param name="layer">If <paramref name="layer"/> is specified, it is recorded with the subdirectory and will be checked by future calls to open.</param>
		Task<FdbDirectorySubspace> CreateAsync(IFdbTransaction trans, IEnumerable<string> subPath, Slice layer = default(Slice));

		/// <summary>Creates a subdirectory with the given <paramref name="path"/> (creating intermediate subdirectories if necessary).
		/// An exception is thrown if the given subdirectory already exists.
		/// </summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="subPath">Relative path of the subdirectory to create</param>
		/// <param name="layer">If <paramref name="layer"/> is specified, it is recorded with the subdirectory and will be checked by future calls to open.</param>
		Task<FdbDirectorySubspace> TryCreateAsync(IFdbTransaction trans, IEnumerable<string> subPath, Slice layer = default(Slice));

		/// <summary>Moves the current directory to <paramref name="newPath"/>.
		/// There is no effect on the physical prefix of the given directory, or on clients that already have the directory open.
		/// An error is raised if a directory already exists at `new_path`, or if the new path points to a child of the current directory.
		/// </summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="newPath">Full path (from the root) where this directory will be moved</param>
		Task<FdbDirectorySubspace> MoveAsync(IFdbTransaction trans, IEnumerable<string> oldPath, IEnumerable<string> newPath);

		/// <summary>Attempts to move the current directory to <paramref name="newPath"/>.
		/// There is no effect on the physical prefix of the given directory, or on clients that already have the directory open.
		/// </summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="newPath">Full path (from the root) where this directory will be moved</param>
		Task<FdbDirectorySubspace> TryMoveAsync(IFdbTransaction trans, IEnumerable<string> oldPath, IEnumerable<string> newPath);

		/// <summary>Removes the directory, its contents, and all subdirectories.
		/// Warning: Clients that have already opened the directory might still insert data into its contents after it is removed.
		/// </summary>
		/// <param name="trans">Transaction to use for the operation</param>
		Task RemoveAsync(IFdbTransaction trans, IEnumerable<string> path = null);

		/// <summary>Attempts to remove the directory, its contents, and all subdirectories.
		/// Warning: Clients that have already opened the directory might still insert data into its contents after it is removed.
		/// </summary>
		/// <param name="trans">Transaction to use for the operation</param>
		Task<bool> TryRemoveAsync(IFdbTransaction trans, IEnumerable<string> path = null);

		/// <summary>Checks if this directory exists</summary>
		/// <returns>Returns true if the directory exists, otherwise false.</returns>
		Task<bool> ExistsAsync(IFdbReadOnlyTransaction trans, IEnumerable<string> path = null);

		/// <summary>Returns the list of all the subdirectories of the current directory.</summary>
		Task<List<string>> ListAsync(IFdbReadOnlyTransaction trans, IEnumerable<string> path = null);

		/// <summary>Returns the list of all the subdirectories of the current directory, it it exists.</summary>
		Task<List<string>> TryListAsync(IFdbReadOnlyTransaction trans, IEnumerable<string> path = null);

	}

}
