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

	[DebuggerDisplay("Path={this.Path}, Prefix={this.Key}")]
	public class FdbDirectorySubspace : FdbSubspace
	{

		internal FdbDirectorySubspace(IFdbTuple path, IFdbTuple prefix, FdbDirectoryLayer directoryLayer, string layer)
			: base(prefix)
		{
			Contract.Requires(path != null);
			Contract.Requires(prefix != null);
			Contract.Requires(directoryLayer != null);

			this.Path = path.Memoize();
			this.DirectoryLayer = directoryLayer;
			this.Layer = layer;
		}

		public FdbMemoizedTuple Path { get; private set; }

		public FdbDirectoryLayer DirectoryLayer { get; private set; }

		public string Layer { get; private set; }

		public void CheckLayer(string layer)
		{
			if (!string.IsNullOrEmpty(layer) && !string.IsNullOrEmpty(this.Layer) && layer != this.Layer)
				throw new InvalidOperationException("The directory was created with an incompatible layer.");
		}

		public Task<FdbDirectorySubspace> CreateOrOpenAsync(IFdbTransaction tr, IFdbTuple subPath, string layer = null, IFdbTuple prefix = null)
		{
			return this.DirectoryLayer.CreateOrOpenAsync(tr, this.Path.Concat(subPath), layer, prefix);
		}

		public Task<FdbDirectorySubspace> OpenAsync(IFdbTransaction tr, IFdbTuple subPath, string layer = null)
		{
			return this.DirectoryLayer.OpenAsync(tr, this.Path.Concat(subPath), layer);
		}

		public Task<FdbDirectorySubspace> CreateAsync(IFdbTransaction tr, IFdbTuple subPath, string layer = null, IFdbTuple prefix = null)
		{
			return this.DirectoryLayer.CreateAsync(tr, this.Path.Concat(subPath), layer, prefix);
		}

		public Task<FdbDirectorySubspace> MoveAsync(IFdbTransaction tr, IFdbTuple newPath)
		{
			return this.DirectoryLayer.MoveAsync(tr, this.Path, newPath);
		}

		public Task RemoveAsync(IFdbTransaction tr)
		{
			return this.DirectoryLayer.RemoveAsync(tr, this.Path);
		}

		public Task<List<IFdbTuple>> ListAsync(IFdbReadTransaction tr)
		{
			return this.DirectoryLayer.ListAsync(tr, this.Path);
		}

		public override string ToString()
		{
			return "DirectorySubspace(" + this.Path + ", " + this.Key + ")";
		}

	}
}
