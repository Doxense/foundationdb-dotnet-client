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
	using FoundationDB.Layers.Tuples;
	using System;
	using System.Collections.Generic;
	using System.Diagnostics;
	using System.Threading;
	using System.Threading.Tasks;

	[DebuggerDisplay("Nodes={Directory.NodeSubspace}, Contents={Directory.ContentsSubspace}")]
	public class FdbRootDirectory
	{
		public FdbDatabase Db { get; private set; }
		internal FdbDirectoryLayer Directory { get; private set; }

		public FdbRootDirectory(FdbDatabase db, FdbDirectoryLayer directory)
		{
			this.Db = db;
			this.Directory = directory;
		}

		public Task<FdbDirectorySubspace> CreateOrOpenAsync(IFdbTuple path, string layer = null, Slice prefix = default(Slice), bool allowCreate = true, bool allowOpen = true, CancellationToken ct = default(CancellationToken))
		{
			return this.Directory.CreateOrOpenAsync(this.Db, path, layer, prefix, allowCreate, allowOpen, ct);
		}

		public Task<FdbDirectorySubspace> OpenAsync(IFdbTuple path, string layer = null, CancellationToken ct = default(CancellationToken))
		{
			return this.Directory.OpenAsync(this.Db, path, layer, ct);
		}

		public Task<FdbDirectorySubspace> CreateAsync(IFdbTuple path, string layer = null, Slice prefix = default(Slice), CancellationToken ct = default(CancellationToken))
		{
			return this.Directory.CreateAsync(this.Db, path, layer, prefix, ct);
		}

		public Task<FdbDirectorySubspace> MoveAsync(IFdbTuple oldPath, IFdbTuple newPath, CancellationToken ct = default(CancellationToken))
		{
			return this.Directory.MoveAsync(this.Db, oldPath, newPath, ct);
		}

		public Task<bool> RemoveAsync(IFdbTuple path, CancellationToken ct = default(CancellationToken))
		{
			return this.Directory.RemoveAsync(this.Db, path, ct);
		}

		public Task<List<IFdbTuple>> ListAsync(IFdbTuple path = null, CancellationToken ct = default(CancellationToken))
		{
			return this.Directory.ListAsync(this.Db, path, ct);
		}

	}

}
