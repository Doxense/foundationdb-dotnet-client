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
		public IFdbDatabase Database { get; private set; }
		internal FdbDirectoryLayer Directory { get; private set; }

		public FdbRootDirectory(IFdbDatabase db, FdbDirectoryLayer directory)
		{
			this.Database = db;
			this.Directory = directory;
		}

		public Task<FdbDirectorySubspace> CreateOrOpenAsync(IFdbTuple path, string layer = null, Slice prefix = default(Slice), bool allowCreate = true, bool allowOpen = true, CancellationToken cancellationToken = default(CancellationToken))
		{
			return this.Directory.CreateOrOpenAsync(this.Database, path, layer, prefix, allowCreate, allowOpen, cancellationToken);
		}

		public Task<FdbDirectorySubspace> OpenAsync(IFdbTuple path, string layer = null, CancellationToken cancellationToken = default(CancellationToken))
		{
			return this.Directory.OpenAsync(this.Database, path, layer, cancellationToken);
		}

		public Task<FdbDirectorySubspace> CreateAsync(IFdbTuple path, string layer = null, Slice prefix = default(Slice), CancellationToken cancellationToken = default(CancellationToken))
		{
			return this.Directory.CreateAsync(this.Database, path, layer, prefix, cancellationToken);
		}

		public Task<FdbDirectorySubspace> MoveAsync(IFdbTuple oldPath, IFdbTuple newPath, CancellationToken cancellationToken = default(CancellationToken))
		{
			return this.Directory.MoveAsync(this.Database, oldPath, newPath, cancellationToken);
		}

		public Task<bool> RemoveAsync(IFdbTuple path, CancellationToken cancellationToken = default(CancellationToken))
		{
			return this.Directory.RemoveAsync(this.Database, path, cancellationToken);
		}

		public Task<List<IFdbTuple>> ListAsync(IFdbTuple path = null, CancellationToken cancellationToken = default(CancellationToken))
		{
			return this.Directory.ListAsync(this.Database, path, cancellationToken);
		}

	}

}
