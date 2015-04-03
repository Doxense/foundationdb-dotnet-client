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
	/// <typeparam name="T1">Type of the first item of the keys handled by this subspace</typeparam>
	/// <typeparam name="T2">Type of the second item of the keys handled by this subspace</typeparam>
	/// <typeparam name="T3">Type of the third item of the keys handled by this subspace</typeparam>
	public class FdbEncoderSubspace<T1, T2, T3, T4> : FdbSubspace, IFdbEncoderSubspace<T1, T2, T3, T4>
	{
		private readonly ICompositeKeyEncoder<T1, T2, T3, T4> m_encoder;

		// ReSharper disable once FieldCanBeMadeReadOnly.Local
		private /*readonly*/ FdbEncoderSubspaceKeys<T1, T2, T3, T4> m_keys;
		private FdbEncoderSubspace<T1> m_head;
		private FdbEncoderSubspace<T1, T2> m_partial;

		public FdbEncoderSubspace(Slice rawPrefix, [NotNull] ICompositeKeyEncoder<T1, T2, T3, T4> encoder)
			: this(rawPrefix, true, encoder)
		{ }

		internal FdbEncoderSubspace(Slice rawPrefix, bool copy, [NotNull] ICompositeKeyEncoder<T1, T2, T3, T4> encoder)
			: base(rawPrefix, copy)
		{
			if (encoder == null) throw new ArgumentNullException("encoder");
			m_encoder = encoder;
			m_keys = new FdbEncoderSubspaceKeys<T1, T2, T3, T4>(this, encoder);
		}

		public IFdbEncoderSubspace<T1> Head
		{
			get { return m_head ?? (m_head = new FdbEncoderSubspace<T1>(GetKeyPrefix(), false, KeyValueEncoders.Head(m_encoder))); }
		}

		public IFdbEncoderSubspace<T1, T2> Partial
		{
			get { return m_partial ?? (m_partial = new FdbEncoderSubspace<T1, T2>(GetKeyPrefix(), false, KeyValueEncoders.Pair(m_encoder))); }
		}

		public ICompositeKeyEncoder<T1, T2, T3, T4> Encoder
		{
			get { return m_encoder; }
		}

		public FdbEncoderSubspaceKeys<T1, T2, T3, T4> Keys
		{
			get { return m_keys; }
		}

		public FdbEncoderSubspacePartition<T1, T2, T3, T4> Partition
		{
			get { return new FdbEncoderSubspacePartition<T1, T2, T3, T4>(this, m_encoder); }
		}

	}

}