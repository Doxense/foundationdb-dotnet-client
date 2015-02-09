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
using JetBrains.Annotations;

namespace FoundationDB.Client
{

	/// <summary>Subspace that knows how to encode and decode its key</summary>
	/// <typeparam name="T">Type of the key handled by this subspace</typeparam>
	public class FdbEncoderSubspace<T> : FdbSubspace, IFdbEncoderSubspace<T>
	{
		private readonly IKeyEncoder<T> m_encoder;

		// ReSharper disable once FieldCanBeMadeReadOnly.Local
		private /*readonly*/ FdbEncoderSubspaceKeys<T> m_keys;

		public FdbEncoderSubspace(Slice rawPrefix, [NotNull] IKeyEncoder<T> encoder)
			: this(rawPrefix, true, encoder)
		{ }

		internal FdbEncoderSubspace(Slice rawPrefix, bool copy, [NotNull] IKeyEncoder<T> encoder)
			: base(rawPrefix, copy)
		{
			if (encoder == null) throw new ArgumentNullException("encoder");
			m_encoder = encoder;
			m_keys = new FdbEncoderSubspaceKeys<T>(this, encoder);
		}

		public IKeyEncoder<T> Encoder
		{
			get { return m_encoder; }
		}

		public FdbEncoderSubspaceKeys<T> Keys
		{
			get { return m_keys; }
		}

		public FdbEncoderSubspacePartition<T> Partition
		{
			get { return new FdbEncoderSubspacePartition<T>(this, m_encoder); }
		}

	}

}