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

namespace FoundationDB.Layers.Directories
{
	using System;
	using Doxense.Collections.Tuples;
	using Doxense.Serialization.Encoders;
	using FoundationDB.Client;
	using JetBrains.Annotations;

	public class FdbDirectoryPartition : FdbDirectorySubspace
	{

		/// <summary>Returns a slice with the ASCII string "partition"</summary>
		public static Slice LayerId => Slice.FromString("partition");

		internal FdbDirectoryPartition([NotNull] ITuple location, [NotNull] ITuple relativeLocation, Slice prefix, [NotNull] FdbDirectoryLayer directoryLayer, [NotNull] IKeyEncoding keyEncoding)
			: base(location, relativeLocation, prefix, new FdbDirectoryLayer(FromKey(prefix + FdbKey.Directory).AsDynamic(keyEncoding), FromKey(prefix).AsDynamic(keyEncoding), location), LayerId, keyEncoding)
		{
			this.ParentDirectoryLayer = directoryLayer;
		}

		internal FdbDirectoryLayer ParentDirectoryLayer { get; }

		protected override Slice GetKeyPrefix()
		{
			throw new InvalidOperationException("Cannot create keys in the root of a directory partition.");
		}

		protected override KeyRange GetKeyRange()
		{
			throw new InvalidOperationException("Cannot create a key range in the root of a directory partition.");
		}

		public override bool Contains(Slice key)
		{
			throw new InvalidOperationException("Cannot check whether a key belongs to the root of a directory partition.");
		}

		protected override ITuple ToRelativePath(ITuple location)
		{
			return location ?? STuple.Empty;
		}

		protected override FdbDirectoryLayer GetLayerForPath(ITuple relativeLocation)
		{
			if (relativeLocation.Count == 0)
			{ // Forward all actions on the Partition itself (empty path) to its parent's DL
				return this.ParentDirectoryLayer;
			}
			else
			{ // For everything else, use the Partition's DL
				return this.DirectoryLayer;
			}
		}

		public override string ToString()
		{
			return $"DirectoryPartition(path={this.FullName}, prefix={GetPrefixUnsafe():K})";
		}

	}

}
