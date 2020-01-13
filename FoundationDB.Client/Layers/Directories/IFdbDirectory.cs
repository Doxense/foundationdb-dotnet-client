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
	using System.Collections.Generic;
	using System.Threading.Tasks;
	using JetBrains.Annotations;

	/// <summary>
	/// Represents a directory in the <code>DirectoryLayer</code>. A <code>Directory</code> stores the path at which it is located and the layer that was used to create it.
	/// The IFdbDirectory interface contains methods to operate on itself and its subdirectories.
	/// </summary>
	[PublicAPI]
	public interface IFdbDirectory
	{
		/// <summary>Name of this <code>Directory</code>.</summary>
		[NotNull]
		string Name { get; }

		/// <summary>Formatted path of this <code>Directory</code></summary>
		[NotNull]
		string FullName { get; }

		/// <summary>Gets the path represented by this <code>Directory</code>.</summary>
		FdbDirectoryPath Path { get; }

		/// <summary>Gets the layer id slice that was stored when this <code>Directory</code> was created.</summary>
		Slice Layer { get; }

		/// <summary>Get the <code>DirectoryLayer</code> that was used to create this <code>Directory</code>.</summary>
		[NotNull]
		FdbDirectoryLayer DirectoryLayer { get; }

		/// <summary>Get the location of the sub-directory with the given path</summary>
		[NotNull]
		FdbDirectorySubspaceLocation this[string segment] { get; }

		/// <summary>Get the location of the sub-directory with the given path</summary>
		[NotNull]
		FdbDirectorySubspaceLocation this[string segment, Slice layer] { get; }

		/// <summary>Get the location of the sub-directory with the given path</summary>
		[NotNull]
		FdbDirectorySubspaceLocation this[FdbDirectoryPath relativePath] { get; }

		/// <summary>Get the location of the sub-directory with the given path</summary>
		[NotNull]
		FdbDirectorySubspaceLocation this[FdbDirectoryPath relativePath, Slice layer] { get; }

		/// <summary>Opens a sub-directory with the given path.
		/// If the sub-directory does not exist, it is created (creating intermediate subdirectories if necessary).
		/// If layer is specified, it is checked against the layer of an existing sub-directory or set as the layer of a new sub-directory.
		/// </summary>
		Task<FdbDirectorySubspace> CreateOrOpenAsync([NotNull] IFdbTransaction trans, FdbDirectoryPath subPath, Slice layer = default);

		/// <summary>Opens a sub-directory with the given <paramref name="path"/>.
		/// An exception is thrown if the sub-directory does not exist, or if a layer is specified and a different layer was specified when the sub-directory was created.
		/// </summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="path">Relative path of the sub-directory to open</param>
		/// <param name="layer">Expected layer id for the sub-directory (optional)</param>
		Task<FdbDirectorySubspace> OpenAsync([NotNull] IFdbReadOnlyTransaction trans, FdbDirectoryPath path, Slice layer = default);

		/// <summary>Opens a sub-directory with the given <paramref name="path"/>.
		/// An exception is thrown if the sub-directory if a layer is specified and a different layer was specified when the sub-directory was created.
		/// </summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="path">Relative path of the sub-directory to open</param>
		/// <param name="layer">Expected layer id for the sub-directory (optional)</param>
		/// <returns>Returns the directory if it exists, or null if it was not found</returns>
		[ItemCanBeNull]
		Task<FdbDirectorySubspace> TryOpenAsync([NotNull] IFdbReadOnlyTransaction trans, FdbDirectoryPath path, Slice layer = default);

		/// <summary>Opens a sub-directory with the given <paramref name="path"/>, using the partition's cache context.</summary>
		/// <returns>Returns the directory if it exists, or null if it was not found</returns>
		/// <remarks>The instance returned MUST NOT be stored or kept outside the context of the transaction!
		/// You must call <see cref="TryOpenCachedAsync(IFdbReadOnlyTransaction, FdbDirectoryPath, Slice)"/> on every new transaction to obtained either the previously cached instance, or a new instance.
		/// Attempting to used a cached instance outside the transaction that produced it may throw exceptions!
		/// </remarks>
		[ItemCanBeNull]
		ValueTask<FdbDirectorySubspace> TryOpenCachedAsync([NotNull] IFdbReadOnlyTransaction trans, FdbDirectoryPath path, Slice layer = default);

		/// <summary>Opens multiple sub-directories with the given <paramref name="paths"/>, using the partition's cache context.</summary>
		/// <returns>Returns the list directories, in the same order. If a directory does not exist, the corresponding slot will contain <c>null</c></returns>
		/// <remarks>The instances returned MUST NOT be stored or kept outside the context of the transaction!
		/// You must call <see cref="TryOpenCachedAsync(IFdbReadOnlyTransaction, IEnumerable{FdbDirectoryPath})"/> on every new transaction to obtained either the previously cached instances, or a new instances.
		/// Attempting to used a cached instances outside the transaction that produced them may throw exceptions!
		/// </remarks>
		ValueTask<FdbDirectorySubspace[]> TryOpenCachedAsync([NotNull] IFdbReadOnlyTransaction trans, [NotNull] IEnumerable<FdbDirectoryPath> paths);
		//REVIEW: only keep the version that accept layers, and use an extension method instead?

		/// <summary>Opens multiple sub-directories with the given <paramref name="paths"/>, using the partition's cache context.</summary>
		/// <returns>Returns the list directories, in the same order. If a directory does not exist, the corresponding slot will contain <c>null</c></returns>
		/// <remarks>The instances returned MUST NOT be stored or kept outside the context of the transaction!
		/// You must call <see cref="TryOpenCachedAsync(IFdbReadOnlyTransaction, IEnumerable{ValueTuple{FdbDirectoryPath, Slice}})"/> on every new transaction to obtained either the previously cached instances, or a new instances.
		/// Attempting to used a cached instances outside the transaction that produced them may throw exceptions!
		/// </remarks>
		ValueTask<FdbDirectorySubspace[]> TryOpenCachedAsync([NotNull] IFdbReadOnlyTransaction trans, [NotNull] IEnumerable<(FdbDirectoryPath Path, Slice Layer)> paths);

		/// <summary>Creates a sub-directory with the given <paramref name="subPath"/> (creating intermediate subdirectories if necessary).
		/// An exception is thrown if the given sub-directory already exists.
		/// </summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="subPath">Relative path of the sub-directory to create</param>
		/// <param name="layer">If <paramref name="layer"/> is specified, it is recorded with the sub-directory and will be checked by future calls to open.</param>
		Task<FdbDirectorySubspace> CreateAsync([NotNull] IFdbTransaction trans, FdbDirectoryPath subPath, Slice layer = default);

		/// <summary>Creates a sub-directory with the given <paramref name="subPath"/> (creating intermediate subdirectories if necessary).
		/// An exception is thrown if the given sub-directory already exists.
		/// </summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="subPath">Relative path of the sub-directory to create</param>
		/// <param name="layer">If <paramref name="layer"/> is specified, it is recorded with the sub-directory and will be checked by future calls to open.</param>
		Task<FdbDirectorySubspace> TryCreateAsync([NotNull] IFdbTransaction trans, FdbDirectoryPath subPath, Slice layer = default);

		/// <summary>Registers an existing prefix as a directory with the given <paramref name="subPath"/> (creating parent directories if necessary). This method is only indented for advanced use cases.</summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="subPath">Path of the directory to create</param>
		/// <param name="layer">If <paramref name="layer"/> is specified, it is recorded with the directory and will be checked by future calls to open.</param>
		/// <param name="prefix">The directory will be created with the given physical prefix; otherwise a prefix is allocated automatically.</param>
		Task<FdbDirectorySubspace> RegisterAsync([NotNull] IFdbTransaction trans, FdbDirectoryPath subPath, Slice layer, Slice prefix);

		/// <summary>Moves the specified sub-directory to <paramref name="newPath"/>.
		/// There is no effect on the physical prefix of the given directory, or on clients that already have the directory open.
		/// An error is raised if a directory already exists at `new_path`.
		/// </summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="oldPath">Relative path under this directory of the sub-directory to be moved</param>
		/// <param name="newPath">Relative path under this directory where the sub-directory will be moved to</param>
		/// <returns>Returns the directory at its new location if successful.</returns>
		Task<FdbDirectorySubspace> MoveAsync([NotNull] IFdbTransaction trans, FdbDirectoryPath oldPath, FdbDirectoryPath newPath);

		/// <summary>Attempts to move the specified sub-directory to <paramref name="newPath"/>.
		/// There is no effect on the physical prefix of the given directory, or on clients that already have the directory open.
		/// An error is raised if a directory already exists at `new_path`.
		/// </summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="oldPath">Relative path under this directory of the sub-directory to be moved</param>
		/// <param name="newPath">Relative path under this directory where the sub-directory will be moved to</param>
		/// <returns>Returns the directory at its new location if successful. If the directory doesn't exist, then null is returned.</returns>
		Task<FdbDirectorySubspace> TryMoveAsync([NotNull] IFdbTransaction trans, FdbDirectoryPath oldPath, FdbDirectoryPath newPath);
		//TODO: merge MoveAsync and TryMoveAsync into a single method!

		/// <summary>Moves the current directory to <paramref name="newAbsolutePath"/>.
		/// There is no effect on the physical prefix of the given directory, or on clients that already have the directory open.
		/// An error is raised if a directory already exists at `new_path`, or if the new path points to a child of the current directory.
		/// </summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="newAbsolutePath">Full path (from the root) where this directory will be moved</param>
		/// <returns>Returns the directory at its new location if successful.</returns>
		Task<FdbDirectorySubspace> MoveToAsync([NotNull] IFdbTransaction trans, FdbDirectoryPath newAbsolutePath);

		/// <summary>Attempts to move the current directory to <paramref name="newAbsolutePath"/>.
		/// There is no effect on the physical prefix of the given directory, or on clients that already have the directory open.
		/// An error is raised if a directory already exists at `new_path`, or if the new path points to a child of the current directory.
		/// </summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="newAbsolutePath">Full path (from the root) where this directory will be moved</param>
		/// <returns>Returns the directory at its new location if successful. If the directory doesn't exist, then null is returned.</returns>
		Task<FdbDirectorySubspace> TryMoveToAsync([NotNull] IFdbTransaction trans, FdbDirectoryPath newAbsolutePath);
		//TODO: merge MoveToAsync and TryMoveToAsync into a single method!

		/// <summary>Removes a directory, its contents, and all subdirectories.
		/// Warning: Clients that have already opened the directory might still insert data into its contents after it is removed.
		/// </summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="subPath">Path of the directory to remove. Will remove the current directory if <paramref name="subPath"/> is empty</param>
		Task RemoveAsync([NotNull] IFdbTransaction trans, FdbDirectoryPath subPath = default);

		/// <summary>Attempts to remove the directory, its contents, and all subdirectories.
		/// Warning: Clients that have already opened the directory might still insert data into its contents after it is removed.
		/// </summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="subPath">Path of the directory to remove. Will remove the current directory if <paramref name="subPath"/> is empty</param>
		Task<bool> TryRemoveAsync([NotNull] IFdbTransaction trans, FdbDirectoryPath subPath = default);
		//TODO: merge RemoveAsync and TryRemoveAsync into a single method!

		/// <summary>Checks if this directory exists</summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="subPath">Path of the directory to test</param>
		/// <returns>Returns true if the directory exists, otherwise false.</returns>
		Task<bool> ExistsAsync([NotNull] IFdbReadOnlyTransaction trans, FdbDirectoryPath subPath = default);

		/// <summary>Returns the list of all the subdirectories of the current directory.</summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="subPath">Path of the directory to list</param>
		Task<List<string>> ListAsync([NotNull] IFdbReadOnlyTransaction trans, FdbDirectoryPath subPath = default);
		//TODO: return a List<FdbDirectoryPath> instead?

		/// <summary>Returns the list of all the subdirectories of the current directory, it it exists.</summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="subPath">Path of the directory to list</param>
		Task<List<string>> TryListAsync([NotNull] IFdbReadOnlyTransaction trans, FdbDirectoryPath subPath = default);
		//TODO: return a List<FdbDirectoryPath> instead?
		//TODO: merge ListAsync and TryListAsync into a single method!

		//TODO: Add BrowseAsync(...) which is the same as ListAsync(...) but returns the FdbDirectorySubspace instances directly?

		/// <summary>Ensure that this directory was registered with the correct layer id</summary>
		/// <param name="layer">Expected layer id (if not empty)</param>
		/// <exception cref="System.InvalidOperationException">If the directory was registered with a different layer id</exception>
		void CheckLayer(Slice layer);

		/// <summary>Change the layer id of this directory</summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="newLayer">New layer id of this directory</param>
		Task<FdbDirectorySubspace> ChangeLayerAsync([NotNull] IFdbTransaction trans, Slice newLayer);

	}

}
