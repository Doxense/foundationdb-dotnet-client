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
		public FdbDirectoryPath Path { get; }

		/// <inheritdoc />
		public Slice Layer { get; }

		/// <inheritdoc />
		Slice ISubspaceLocation.Prefix => Slice.Nil;

		IKeyEncoding ISubspaceLocation.Encoding => TuPack.Encoding;

		/// <summary>Returns <c>true</c> if this location points to a directory partition.</summary>
		public bool IsPartition { get; }

		FdbDirectorySubspaceLocation IFdbDirectory.Location => this;

		public FdbDirectorySubspaceLocation(FdbDirectoryPath path, Slice layer = default)
		{
			this.Path = path;
			this.Layer = layer;
			this.IsPartition = layer.Equals(FdbDirectoryPartition.LayerId);
		}

		/// <inheritdoc />
		async ValueTask<IKeySubspace?> ISubspaceLocation.Resolve(IFdbReadOnlyTransaction tr, FdbDirectoryLayer? directory)
		{
			return await Resolve(tr, directory);
		}

		/// <inheritdoc />
		public ValueTask<FdbDirectorySubspace?> Resolve(IFdbReadOnlyTransaction tr, FdbDirectoryLayer? directory = null)
		{
			Contract.NotNull(tr, nameof(tr));
			return (directory ?? tr.Context.Database.DirectoryLayer).TryOpenCachedAsync(tr, this.Path);
		}

		public override string ToString()
		{
			return this.Layer.Count == 0 ? this.Path.ToString() : (this.Path.ToString() + "[" + this.Layer.ToString() + "]");
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

		internal FdbDirectoryPath GetSafePath()
		{
			if (this.IsPartition && this.Path.Count != 0) throw ThrowHelper.InvalidOperationException($"Cannot create a binary subspace under the root of directory partition '{this.Path}'.");
			return this.Path;
		}

		/// <summary>Append a segment to the current path</summary>
		public FdbDirectorySubspaceLocation this[string segment] => new FdbDirectorySubspaceLocation(this.Path + segment);

		/// <summary>Append a segment and layer id to the current path</summary>
		public FdbDirectorySubspaceLocation this[string segment, Slice layer] => new FdbDirectorySubspaceLocation(this.Path + segment, layer);

		/// <summary>Append a relative path to the current path</summary>
		public FdbDirectorySubspaceLocation this[FdbDirectoryPath relativePath] => new FdbDirectorySubspaceLocation(this.Path + relativePath);

		/// <summary>Append a relative path and layer id, to the current path</summary>
		public FdbDirectorySubspaceLocation this[FdbDirectoryPath relativePath, Slice layer] => new FdbDirectorySubspaceLocation(this.Path + relativePath, layer);

		/// <summary>Append one or more segments to the current path</summary>
		public FdbDirectorySubspaceLocation this[ReadOnlySpan<string> segments] => new FdbDirectorySubspaceLocation(this.Path.Add(segments));

		/// <summary>Append one or more segments, and a layer id, to the current path</summary>
		public FdbDirectorySubspaceLocation this[ReadOnlySpan<string> segments, Slice layer] => new FdbDirectorySubspaceLocation(this.Path.Add(segments), layer);

		public DynamicKeySubspaceLocation ByKey<T1>(T1 item1) => new DynamicKeySubspaceLocation(GetSafePath(), TuPack.EncodeKey<T1>(item1), TuPack.Encoding.GetDynamicKeyEncoder());

		public DynamicKeySubspaceLocation ByKey<T1, T2>(T1 item1, T2 item2) => new DynamicKeySubspaceLocation(GetSafePath(), TuPack.EncodeKey<T1, T2>(item1, item2), TuPack.Encoding.GetDynamicKeyEncoder());

		#region IFdbDirectory...

		string IFdbDirectory.Name => this.Path.Name;

		string IFdbDirectory.FullName => this.Path.ToString();

		FdbDirectoryLayer IFdbDirectory.DirectoryLayer => throw new NotSupportedException();

		/// <inheritdoc />
		public Task<FdbDirectorySubspace> CreateOrOpenAsync(IFdbTransaction trans, FdbDirectoryPath subPath = default, Slice layer = default)
		{
			return trans.Context.Database.DirectoryLayer.CreateOrOpenAsync(trans, this.Path.Add(subPath), layer);
		}

		/// <inheritdoc />
		public Task<FdbDirectorySubspace> OpenAsync(IFdbReadOnlyTransaction trans, FdbDirectoryPath path = default, Slice layer = default)
		{
			return trans.Context.Database.DirectoryLayer.OpenAsync(trans, this.Path.Add(path), layer);
		}

		/// <inheritdoc />
		public Task<FdbDirectorySubspace?> TryOpenAsync(IFdbReadOnlyTransaction trans, FdbDirectoryPath path = default, Slice layer = default)
		{
			return trans.Context.Database.DirectoryLayer.TryOpenAsync(trans, this.Path.Add(path), layer);
		}

		/// <inheritdoc />
		public ValueTask<FdbDirectorySubspace?> TryOpenCachedAsync(IFdbReadOnlyTransaction trans, FdbDirectoryPath path = default, Slice layer = default)
		{
			return trans.Context.Database.DirectoryLayer.TryOpenCachedAsync(trans, this.Path.Add(path), layer);
		}

		/// <inheritdoc />
		public ValueTask<FdbDirectorySubspace?[]> TryOpenCachedAsync(IFdbReadOnlyTransaction trans, IEnumerable<FdbDirectoryPath> paths)
		{
			return trans.Context.Database.DirectoryLayer.TryOpenCachedAsync(trans, paths.Select(p => this.Path.Add(p)));
		}

		/// <inheritdoc />
		public ValueTask<FdbDirectorySubspace?[]> TryOpenCachedAsync(IFdbReadOnlyTransaction trans, IEnumerable<(FdbDirectoryPath Path, Slice Layer)> paths)
		{
			return trans.Context.Database.DirectoryLayer.TryOpenCachedAsync(trans, paths.Select(x => (this.Path.Add(x.Path), x.Layer)));
		}

		/// <inheritdoc />
		public Task<FdbDirectorySubspace> CreateAsync(IFdbTransaction trans, FdbDirectoryPath subPath = default, Slice layer = default)
		{
			return trans.Context.Database.DirectoryLayer.CreateAsync(trans, this.Path.Add(subPath));
		}

		/// <inheritdoc />
		public Task<FdbDirectorySubspace?> TryCreateAsync(IFdbTransaction trans, FdbDirectoryPath subPath = default, Slice layer = default)
		{
			return trans.Context.Database.DirectoryLayer.TryCreateAsync(trans, this.Path.Add(subPath));
		}

		/// <inheritdoc />
		public Task<FdbDirectorySubspace> MoveToAsync(IFdbTransaction trans, FdbDirectoryPath newAbsolutePath)
		{
			return trans.Context.Database.DirectoryLayer.MoveAsync(trans, this.Path, newAbsolutePath);
		}

		/// <inheritdoc />
		public Task<FdbDirectorySubspace?> TryMoveToAsync(IFdbTransaction trans, FdbDirectoryPath newAbsolutePath)
		{
			return trans.Context.Database.DirectoryLayer.TryMoveAsync(trans, this.Path, newAbsolutePath);
		}

		/// <inheritdoc />
		public Task RemoveAsync(IFdbTransaction trans, FdbDirectoryPath path = default)
		{
			return trans.Context.Database.DirectoryLayer.RemoveAsync(trans, this.Path.Add(path));
		}

		/// <inheritdoc />
		public Task<bool> TryRemoveAsync(IFdbTransaction trans, FdbDirectoryPath path = default)
		{
			return trans.Context.Database.DirectoryLayer.TryRemoveAsync(trans, this.Path.Add(path));
		}

		/// <inheritdoc />
		public Task<bool> ExistsAsync(IFdbReadOnlyTransaction trans, FdbDirectoryPath path = default)
		{
			return trans.Context.Database.DirectoryLayer.ExistsAsync(trans, this.Path.Add(path));
		}

		/// <inheritdoc />
		public Task<List<string>> ListAsync(IFdbReadOnlyTransaction trans, FdbDirectoryPath path = default)
		{
			return trans.Context.Database.DirectoryLayer.ListAsync(trans, this.Path.Add(path));
		}

		/// <inheritdoc />
		public Task<List<string>?> TryListAsync(IFdbReadOnlyTransaction trans, FdbDirectoryPath path = default)
		{
			return trans.Context.Database.DirectoryLayer.TryListAsync(trans, this.Path.Add(path));
		}

		void IFdbDirectory.CheckLayer(Slice layer) => throw new NotSupportedException();

		Task<FdbDirectorySubspace> IFdbDirectory.ChangeLayerAsync(IFdbTransaction trans, Slice newLayer) => throw new NotSupportedException();

		Task<FdbDirectorySubspace> IFdbDirectory.RegisterAsync(IFdbTransaction trans, FdbDirectoryPath subPath, Slice layer, Slice prefix) => throw new NotSupportedException();

		Task<FdbDirectorySubspace> IFdbDirectory.MoveAsync(IFdbTransaction trans, FdbDirectoryPath oldPath, FdbDirectoryPath newPath) => throw new NotSupportedException();

		Task<FdbDirectorySubspace?> IFdbDirectory.TryMoveAsync(IFdbTransaction trans, FdbDirectoryPath oldPath, FdbDirectoryPath newPath) => throw new NotSupportedException();

		#endregion

	}
}
