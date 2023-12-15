#region Copyright (c) 2023 SnowBank SAS, (c) 2005-2023 Doxense SAS
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

namespace FoundationDB.Client
{
	using System;
	using System.Collections.Generic;
	using System.Diagnostics;
	using System.Linq;
	using System.Threading.Tasks;
	using Doxense.Collections.Tuples;
	using Doxense.Diagnostics.Contracts;
	using Doxense.Serialization.Encoders;

	[DebuggerDisplay("Path={Path}, Layer={Layer}")]
	public sealed class FdbDirectorySubspaceLocation : ISubspaceLocation<FdbDirectorySubspace>, IFdbDirectory
	{

		/// <inheritdoc cref="ISubspaceLocation.Path" />
		public FdbPath Path { get; }

		/// <inheritdoc />
		public string Layer => this.Path.LayerId ?? string.Empty;

		/// <inheritdoc />
		Slice ISubspaceLocation.Prefix => Slice.Nil;

		IKeyEncoding ISubspaceLocation.Encoding => TuPack.Encoding;

		/// <summary>Returns <c>true</c> if this location points to a directory partition.</summary>
		public bool IsPartition { get; }

		FdbDirectorySubspaceLocation IFdbDirectory.Location => this;

		/// <summary>Create a new location that points to the Directory Subspace at the given path.</summary>
		/// <param name="path">Absolute path of the target Directory Subspace</param>
		public FdbDirectorySubspaceLocation(FdbPath path)
		{
			if (!path.IsAbsolute) throw new ArgumentException("Directory Subspace path must absolute.", nameof(path));
			this.Path = path;
			this.IsPartition = path.LayerId == FdbDirectoryPartition.LayerId;
		}

		/// <inheritdoc />
		async ValueTask<IKeySubspace?> ISubspaceLocation.Resolve(IFdbReadOnlyTransaction tr, FdbDirectoryLayer? directory)
		{
			return await Resolve(tr, directory);
		}

		/// <inheritdoc />
		public ValueTask<FdbDirectorySubspace?> Resolve(IFdbReadOnlyTransaction tr, FdbDirectoryLayer? directory = null)
		{
			Contract.NotNull(tr);
			return (directory ?? tr.Context.Database.DirectoryLayer).TryOpenCachedAsync(tr, this.Path);
		}

		public override string ToString()
		{
			return this.Path.ToString();
		}

		public override int GetHashCode()
		{
			return HashCodes.Combine(this.Path.GetHashCode(), this.Layer.GetHashCode());
		}

		public override bool Equals(object obj)
		{
			return obj is FdbDirectorySubspaceLocation loc && Equals(loc);
		}

		public bool Equals(ISubspaceLocation other)
		{
			return other != null && other.Path == this.Path && other.Prefix.Count == 0;
		}

		internal FdbPath GetSafePath()
		{
			if (this.IsPartition && this.Path.Count != 0) throw ThrowHelper.InvalidOperationException($"Cannot create a binary subspace under the root of directory partition '{this.Path}'.");
			return this.Path;
		}

		/// <summary>Append a segment to the current path</summary>
		public FdbDirectorySubspaceLocation this[FdbPathSegment segment] => new FdbDirectorySubspaceLocation(this.Path.Add(segment));

		/// <summary>Append one or more segments to the current path</summary>
		public FdbDirectorySubspaceLocation this[ReadOnlySpan<FdbPathSegment> segments] => segments.Length != 0 ? new FdbDirectorySubspaceLocation(this.Path.Add(segments)) : this;

		/// <summary>Append a segment to the current path</summary>
		/// <param name="name">Name of the segment</param>
		/// <remarks>The new segment will not have a layer id.</remarks>
		public FdbDirectorySubspaceLocation this[string name] => new FdbDirectorySubspaceLocation(this.Path.Add(FdbPathSegment.Create(name)));

		/// <summary>Append a segment - composed of a name and layer id - to the current path</summary>
		/// <param name="name">Name of the segment</param>
		/// <param name="layerId">Layer Id of the segment</param>
		public FdbDirectorySubspaceLocation this[string name, string layerId] => new FdbDirectorySubspaceLocation(this.Path.Add(new FdbPathSegment(name, layerId)));

		/// <summary>Append a relative path to the current path</summary>
		public FdbDirectorySubspaceLocation this[FdbPath relativePath] => !relativePath.IsEmpty ? new FdbDirectorySubspaceLocation(this.Path.Add(relativePath)) : this;

		/// <summary>Append an encoded key to the prefix of the current location</summary>
		/// <typeparam name="T1">Type of the key</typeparam>
		/// <param name="item1">Key that will be appended to the current location's binary prefix</param>
		/// <returns>A new subspace location with an additional binary suffix</returns>
		public DynamicKeySubspaceLocation ByKey<T1>(T1 item1) => new DynamicKeySubspaceLocation(GetSafePath(), TuPack.EncodeKey<T1>(item1), TuPack.Encoding.GetDynamicKeyEncoder());

		/// <summary>Append a pair encoded keys to the prefix of the current location</summary>
		/// <typeparam name="T1">Type of the first key</typeparam>
		/// <typeparam name="T2">Type of the second key</typeparam>
		/// <param name="item1">Key that will be appended first to the current location's binary prefix</param>
		/// <param name="item2">Key that will be appended last to the current location's binary prefix</param>
		/// <returns>A new subspace location with an additional binary suffix</returns>
		public DynamicKeySubspaceLocation ByKey<T1, T2>(T1 item1, T2 item2) => new DynamicKeySubspaceLocation(GetSafePath(), TuPack.EncodeKey<T1, T2>(item1, item2), TuPack.Encoding.GetDynamicKeyEncoder());

		#region IFdbDirectory...

		/// <inheritdoc />
		string IFdbDirectory.Name => this.Path.Name;

		/// <inheritdoc />
		string IFdbDirectory.FullName => this.Path.ToString();

		/// <inheritdoc />
		FdbDirectoryLayer IFdbDirectory.DirectoryLayer => throw new NotSupportedException();

		/// <inheritdoc />
		public Task<FdbDirectorySubspace> CreateOrOpenAsync(IFdbTransaction trans, FdbPath subPath = default)
		{
			return trans.Context.Database.DirectoryLayer.CreateOrOpenAsync(trans, this.Path.Add(subPath));
		}

		/// <inheritdoc />
		public Task<FdbDirectorySubspace> OpenAsync(IFdbReadOnlyTransaction trans, FdbPath path = default)
		{
			return trans.Context.Database.DirectoryLayer.OpenAsync(trans, this.Path.Add(path));
		}

		/// <inheritdoc />
		public Task<FdbDirectorySubspace?> TryOpenAsync(IFdbReadOnlyTransaction trans, FdbPath path = default)
		{
			return trans.Context.Database.DirectoryLayer.TryOpenAsync(trans, this.Path.Add(path));
		}

		/// <inheritdoc />
		public ValueTask<FdbDirectorySubspace?> TryOpenCachedAsync(IFdbReadOnlyTransaction trans, FdbPath path = default)
		{
			return trans.Context.Database.DirectoryLayer.TryOpenCachedAsync(trans, this.Path.Add(path));
		}

		/// <inheritdoc />
		public ValueTask<FdbDirectorySubspace?[]> TryOpenCachedAsync(IFdbReadOnlyTransaction trans, IEnumerable<FdbPath> paths)
		{
			return trans.Context.Database.DirectoryLayer.TryOpenCachedAsync(trans, paths.Select(p => this.Path.Add(p)));
		}

		/// <inheritdoc />
		public Task<FdbDirectorySubspace> CreateAsync(IFdbTransaction trans, FdbPath subPath = default)
		{
			return trans.Context.Database.DirectoryLayer.CreateAsync(trans, this.Path.Add(subPath));
		}

		/// <inheritdoc />
		public Task<FdbDirectorySubspace?> TryCreateAsync(IFdbTransaction trans, FdbPath subPath = default)
		{
			return trans.Context.Database.DirectoryLayer.TryCreateAsync(trans, this.Path.Add(subPath));
		}

		/// <inheritdoc />
		public Task<FdbDirectorySubspace> MoveToAsync(IFdbTransaction trans, FdbPath newAbsolutePath)
		{
			return trans.Context.Database.DirectoryLayer.MoveAsync(trans, this.Path, newAbsolutePath);
		}

		/// <inheritdoc />
		public Task<FdbDirectorySubspace?> TryMoveToAsync(IFdbTransaction trans, FdbPath newAbsolutePath)
		{
			return trans.Context.Database.DirectoryLayer.TryMoveAsync(trans, this.Path, newAbsolutePath);
		}

		/// <inheritdoc />
		public Task RemoveAsync(IFdbTransaction trans, FdbPath path = default)
		{
			return trans.Context.Database.DirectoryLayer.RemoveAsync(trans, this.Path.Add(path));
		}

		/// <inheritdoc />
		public Task<bool> TryRemoveAsync(IFdbTransaction trans, FdbPath path = default)
		{
			return trans.Context.Database.DirectoryLayer.TryRemoveAsync(trans, this.Path.Add(path));
		}

		/// <inheritdoc />
		public Task<bool> ExistsAsync(IFdbReadOnlyTransaction trans, FdbPath path = default)
		{
			return trans.Context.Database.DirectoryLayer.ExistsAsync(trans, this.Path.Add(path));
		}

		/// <inheritdoc />
		public Task<List<FdbPath>> ListAsync(IFdbReadOnlyTransaction trans, FdbPath path = default)
		{
			return trans.Context.Database.DirectoryLayer.ListAsync(trans, this.Path.Add(path));
		}

		/// <inheritdoc />
		public Task<List<FdbPath>?> TryListAsync(IFdbReadOnlyTransaction trans, FdbPath path = default)
		{
			return trans.Context.Database.DirectoryLayer.TryListAsync(trans, this.Path.Add(path));
		}

		/// <inheritdoc />
		void IFdbDirectory.CheckLayer(string? layer) => throw new NotSupportedException();

		/// <inheritdoc />
		Task<FdbDirectorySubspace> IFdbDirectory.ChangeLayerAsync(IFdbTransaction trans, string newLayer) => throw new NotSupportedException();

		/// <inheritdoc />
		Task<FdbDirectorySubspace> IFdbDirectory.RegisterAsync(IFdbTransaction trans, FdbPath subPath, Slice prefix) => throw new NotSupportedException();

		/// <inheritdoc />
		Task<FdbDirectorySubspace> IFdbDirectory.MoveAsync(IFdbTransaction trans, FdbPath oldPath, FdbPath newPath) => throw new NotSupportedException();

		/// <inheritdoc />
		Task<FdbDirectorySubspace?> IFdbDirectory.TryMoveAsync(IFdbTransaction trans, FdbPath oldPath, FdbPath newPath) => throw new NotSupportedException();

		#endregion

	}
}
