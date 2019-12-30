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
	using System.Threading.Tasks;
	using Doxense.Collections.Tuples;
	using Doxense.Diagnostics.Contracts;
	using Doxense.Serialization.Encoders;

	public sealed class FdbDirectorySubspaceLocation : ISubspaceLocation<FdbDirectorySubspace>
	{

		public IFdbDirectory Directory { get; }

		public FdbDirectoryPath Path { get; }

		Slice ISubspaceLocation.Prefix => Slice.Nil;

		IKeyEncoding ISubspaceLocation.Encoding => TuPack.Encoding;

		public FdbDirectorySubspaceLocation(IFdbDirectory directory, FdbDirectoryPath path)
		{
			Contract.NotNull(directory, nameof(directory));

			//REVIEW: is it legal if path is empty? (can't really "open" the root)

			this.Directory = directory;
			this.Path = path;
		}

		public ValueTask<FdbDirectorySubspace> Resolve(IFdbReadOnlyTransaction tr, IFdbDirectory directory = null)
		{
			Contract.NotNull(tr, nameof(tr));

			// using a different directory instance is most certainly an error, so it is not allowed
			if (directory != null && directory != this.Directory) throw new InvalidOperationException("Cannot resolve a directory subspace location using a different DirectoryLayer instance.");

			return this.Directory.TryOpenCachedAsync(tr, this.Path);
		}

		public FdbDirectorySubspaceLocation this[string segment] => new FdbDirectorySubspaceLocation(this.Directory, this.Path[segment]);

		public FdbDirectorySubspaceLocation this[string segment1, string segment2] => new FdbDirectorySubspaceLocation(this.Directory, this.Path.Add(segment1, segment2));

		public FdbDirectorySubspaceLocation this[ReadOnlySpan<string> segments] => new FdbDirectorySubspaceLocation(this.Directory, this.Path[segments]);

		public DynamicKeySubspaceLocation ByKey<T1>(T1 item1) => new DynamicKeySubspaceLocation(this.Path, TuPack.EncodeKey<T1>(item1), TuPack.Encoding.GetDynamicKeyEncoder());

		public DynamicKeySubspaceLocation ByKey<T1, T2>(T1 item1, T2 item2) => new DynamicKeySubspaceLocation(this.Path, TuPack.EncodeKey<T1, T2>(item1, item2), TuPack.Encoding.GetDynamicKeyEncoder());

	}
}
