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
	using FoundationDB.Client;
	using FoundationDB.Layers.Tuples;
	using System;
	public class FdbDirectoryPartition : FdbDirectorySubspace
	{

		/// <summary>Returns a slice with the ASCII string "partition"</summary>
		public static Slice LayerId { get { return Slice.FromString("partition"); } }

		private readonly FdbDirectoryLayer m_parentDirectoryLayer;

		internal FdbDirectoryPartition(IFdbTuple location, IFdbTuple relativeLocation, Slice prefix, FdbDirectoryLayer directoryLayer)
			: base(location, relativeLocation, prefix, new FdbDirectoryLayer(FdbSubspace.CreateDynamic(prefix + FdbKey.Directory, TypeSystem.Tuples), FdbSubspace.CreateDynamic(prefix, TypeSystem.Tuples), location), LayerId, TypeSystem.Tuples.GetDynamicEncoder())
		{
			m_parentDirectoryLayer = directoryLayer;
		}

		internal FdbDirectoryLayer ParentDirectoryLayer { get { return m_parentDirectoryLayer; } }

		protected override Slice GetKeyPrefix()
		{
			throw new InvalidOperationException("Cannot create keys in the root of a directory partition.");
		}

		public override bool Contains(Slice key)
		{
			throw new InvalidOperationException("Cannot check whether a key belongs to the root of a directory partition.");
		}

		protected override IFdbTuple ToRelativePath(IFdbTuple location)
		{
			return location ?? FdbTuple.Empty;
		}

		protected override FdbDirectoryLayer GetLayerForPath(IFdbTuple relativeLocation)
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
			return String.Format("DirectoryPartition(path={0}, prefix={1})", this.FullName, this.InternalKey.ToAsciiOrHexaString());
		}

	}

}
