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

		public IFdbDirectory Directory { get; }

		public FdbDirectoryPath Path { get; }

		public Slice Layer { get; }

		Slice ISubspaceLocation.Prefix => Slice.Nil;

		IKeyEncoding ISubspaceLocation.Encoding => TuPack.Encoding;

		public FdbDirectorySubspaceLocation(IFdbDirectory directory, FdbDirectoryPath path, Slice layer = default)
		{
			Contract.NotNull(directory, nameof(directory));

			//REVIEW: is it legal if path is empty? (can't really "open" the root)

			this.Directory = directory;
			this.Path = path;
			this.Layer = layer;
		}

		public ValueTask<FdbDirectorySubspace> Resolve(IFdbReadOnlyTransaction tr, IFdbDirectory directory = null)
		{
			Contract.NotNull(tr, nameof(tr));

			// using a different directory instance is most certainly an error, so it is not allowed
			if (directory != null && !directory.Equals(this.Directory)) throw new InvalidOperationException("Cannot resolve a directory subspace location using a different DirectoryLayer instance.");

			return this.Directory.TryOpenCachedAsync(tr, this.Path);
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

		public FdbDirectorySubspaceLocation this[string segment] => new FdbDirectorySubspaceLocation(this.Directory, this.Path[segment]);

		public FdbDirectorySubspaceLocation this[FdbDirectoryPath relativePath] => throw new NotImplementedException();

		public FdbDirectorySubspaceLocation this[string segment1, string segment2] => new FdbDirectorySubspaceLocation(this.Directory, this.Path.Add(segment1, segment2));

		public FdbDirectorySubspaceLocation this[ReadOnlySpan<string> segments] => new FdbDirectorySubspaceLocation(this.Directory, this.Path[segments]);

		public DynamicKeySubspaceLocation ByKey<T1>(T1 item1) => new DynamicKeySubspaceLocation(this.Path, TuPack.EncodeKey<T1>(item1), TuPack.Encoding.GetDynamicKeyEncoder());

		public DynamicKeySubspaceLocation ByKey<T1, T2>(T1 item1, T2 item2) => new DynamicKeySubspaceLocation(this.Path, TuPack.EncodeKey<T1, T2>(item1, item2), TuPack.Encoding.GetDynamicKeyEncoder());

		#region IFdbDirectory...

		string IFdbDirectory.Name => this.Path.Name;

		string IFdbDirectory.FullName => this.Path.ToString();

		FdbDirectoryLayer IFdbDirectory.DirectoryLayer => this.Directory.DirectoryLayer;

		public Task<FdbDirectorySubspace> CreateOrOpenAsync(IFdbTransaction trans, FdbDirectoryPath subPath = default, Slice layer = default)
		{
			return this.Directory.CreateOrOpenAsync(trans, this.Path.Add(subPath), layer);
		}

		public Task<FdbDirectorySubspace> OpenAsync(IFdbReadOnlyTransaction trans, FdbDirectoryPath path = default, Slice layer = default)
		{
			return this.Directory.OpenAsync(trans, this.Path.Add(path), layer);
		}

		public Task<FdbDirectorySubspace> TryOpenAsync(IFdbReadOnlyTransaction trans, FdbDirectoryPath path = default, Slice layer = default)
		{
			return this.Directory.TryOpenAsync(trans, this.Path.Add(path), layer);
		}

		public ValueTask<FdbDirectorySubspace> TryOpenCachedAsync(IFdbReadOnlyTransaction trans, FdbDirectoryPath path = default, Slice layer = default)
		{
			return this.Directory.TryOpenCachedAsync(trans, this.Path.Add(path), layer);
		}

		public ValueTask<FdbDirectorySubspace[]> TryOpenCachedAsync(IFdbReadOnlyTransaction trans, IEnumerable<FdbDirectoryPath> paths)
		{
			return this.Directory.TryOpenCachedAsync(trans, paths.Select(p => this.Path.Add(p)));
		}

		public ValueTask<FdbDirectorySubspace[]> TryOpenCachedAsync(IFdbReadOnlyTransaction trans, IEnumerable<(FdbDirectoryPath Path, Slice Layer)> paths)
		{
			return this.Directory.TryOpenCachedAsync(trans, paths.Select(x => (this.Path.Add(x.Path), x.Layer)));
		}

		public Task<FdbDirectorySubspace> CreateAsync(IFdbTransaction trans, FdbDirectoryPath subPath = default, Slice layer = default)
		{
			return this.Directory.CreateAsync(trans, this.Path.Add(subPath));
		}

		public Task<FdbDirectorySubspace> TryCreateAsync(IFdbTransaction trans, FdbDirectoryPath subPath = default, Slice layer = default)
		{
			return this.Directory.TryCreateAsync(trans, this.Path.Add(subPath));
		}

		public Task<FdbDirectorySubspace> MoveToAsync(IFdbTransaction trans, FdbDirectoryPath newAbsolutePath)
		{
			return this.Directory.MoveAsync(trans, this.Path, newAbsolutePath);
		}

		public Task<FdbDirectorySubspace> TryMoveToAsync(IFdbTransaction trans, FdbDirectoryPath newAbsolutePath)
		{
			return this.Directory.TryMoveAsync(trans, this.Path, newAbsolutePath);
		}

		public Task RemoveAsync(IFdbTransaction trans, FdbDirectoryPath path = default)
		{
			return this.Directory.RemoveAsync(trans, this.Path.Add(path));
		}

		public Task<bool> TryRemoveAsync(IFdbTransaction trans, FdbDirectoryPath path = default)
		{
			return this.Directory.TryRemoveAsync(trans, this.Path.Add(path));
		}

		public Task<bool> ExistsAsync(IFdbReadOnlyTransaction trans, FdbDirectoryPath path = default)
		{
			return this.Directory.ExistsAsync(trans, this.Path.Add(path));
		}

		public Task<List<string>> ListAsync(IFdbReadOnlyTransaction trans, FdbDirectoryPath path = default)
		{
			return this.Directory.ListAsync(trans, this.Path.Add(path));
		}

		public Task<List<string>> TryListAsync(IFdbReadOnlyTransaction trans, FdbDirectoryPath path = default)
		{
			return this.Directory.TryListAsync(trans, this.Path.Add(path));
		}

		void IFdbDirectory.CheckLayer(Slice layer) => throw new NotSupportedException();

		Task<FdbDirectorySubspace> IFdbDirectory.ChangeLayerAsync(IFdbTransaction trans, Slice newLayer) => throw new NotSupportedException();

		Task<FdbDirectorySubspace> IFdbDirectory.RegisterAsync(IFdbTransaction trans, FdbDirectoryPath subPath, Slice layer, Slice prefix) => throw new NotSupportedException();

		Task<FdbDirectorySubspace> IFdbDirectory.MoveAsync(IFdbTransaction trans, FdbDirectoryPath oldPath, FdbDirectoryPath newPath) => throw new NotSupportedException();

		Task<FdbDirectorySubspace> IFdbDirectory.TryMoveAsync(IFdbTransaction trans, FdbDirectoryPath oldPath, FdbDirectoryPath newPath) => throw new NotSupportedException();

		#endregion

	}
}
