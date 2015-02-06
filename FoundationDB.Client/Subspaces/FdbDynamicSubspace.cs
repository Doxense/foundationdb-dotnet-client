#region BSD Licence
/* Copyright (c) 2013-2015, Doxense SAS
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

using System;
using System.Diagnostics;
using JetBrains.Annotations;

namespace FoundationDB.Client
{
	public class FdbDynamicSubspace : FdbSubspace, IFdbDynamicSubspace
	{
		/// <summary>Encoder for the keys of this subspace</summary>
		private readonly IDynamicKeyEncoder m_encoder;

		/// <summary>Create a new subspace from a binary prefix</summary>
		/// <param name="rawPrefix">Prefix of the new subspace</param>
		/// <param name="copy">If true, take a copy of the prefix</param>
		/// <param name="encoder">Type System used to encode keys in this subspace (optional, will use Tuple Encoding by default)</param>
		internal FdbDynamicSubspace(Slice rawPrefix, bool copy, IDynamicKeyEncoder encoder)
			:  base (rawPrefix, copy)
		{
			this.m_encoder = encoder ?? TypeSystem.Default.GetDynamicEncoder();
		}

		public FdbDynamicSubspace(Slice rawPrefix, IDynamicKeyEncoder encoder)
			: this(rawPrefix, true, encoder)
		{ }

		protected override IFdbSubspace CreateChildren(Slice suffix)
		{
			return new FdbDynamicSubspace(ConcatKey(suffix), m_encoder);
		}

		public IDynamicKeyEncoder Encoder
		{
			get { return m_encoder; }
		}

		/// <summary>Return a view of all the possible binary keys of this subspace</summary>
		public FdbDynamicSubspaceKeys Keys
		{
			[DebuggerStepThrough]
			get { return new FdbDynamicSubspaceKeys(this, m_encoder); }
		}

		/// <summary>Returns an helper object that knows how to create sub-partitions of this subspace</summary>
		public FdbDynamicSubspacePartition Partition
		{
			//note: not cached, because this is probably not be called frequently (except in the init path)
			[DebuggerStepThrough]
			get { return new FdbDynamicSubspacePartition(this, m_encoder); }
		}

	}
}