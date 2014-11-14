﻿#region BSD Licence
/* Copyright (c) 2013-2014, Doxense SAS
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
	using FoundationDB.Layers.Tuples;
	using JetBrains.Annotations;
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using System.Threading.Tasks;

	public class FdbEncoderSubspace<T1, T2, T3> : FdbSubspace, ICompositeKeyEncoder<T1, T2, T3>
	{
		protected readonly IFdbSubspace m_parent;
		protected readonly ICompositeKeyEncoder<T1, T2, T3> m_encoder;
		protected volatile FdbEncoderSubspace<T1> m_head;
		protected volatile FdbEncoderSubspace<T1, T2> m_partial;

		public FdbEncoderSubspace([NotNull] IFdbSubspace subspace, [NotNull] ICompositeKeyEncoder<T1, T2, T3> encoder)
			: base(subspace)
		{
			if (subspace == null) throw new ArgumentNullException("subspace");
			if (encoder == null) throw new ArgumentNullException("encoder");
			m_parent = subspace;
			m_encoder = encoder;
		}

		public ICompositeKeyEncoder<T1, T2, T3> Encoder
		{
			[NotNull]
			get { return m_encoder; }
		}

		public FdbEncoderSubspace<T1> Head
		{
			[NotNull]
			get { return m_head ?? (m_head = new FdbEncoderSubspace<T1>(m_parent, KeyValueEncoders.Head(m_encoder))); }
		}

		public FdbEncoderSubspace<T1, T2> Partial
		{
			[NotNull]
			get { return m_partial ?? (m_partial = new FdbEncoderSubspace<T1, T2>(m_parent, KeyValueEncoders.Pair(m_encoder))); }
		}

		#region Transaction Helpers...

		public void Set([NotNull] IFdbTransaction trans, T1 key1, T2 key2, T3 key3, Slice value)
		{
			trans.Set(EncodeKey(key1, key2, key3), value);
		}

		public void Set([NotNull] IFdbTransaction trans, FdbTuple<T1, T2, T3> key, Slice value)
		{
			trans.Set(EncodeKey(key), value);
		}

		public void Clear([NotNull] IFdbTransaction trans, T1 key1, T2 key2, T3 key3)
		{
			trans.Clear(EncodeKey(key1, key2, key3));
		}

		public void Clear([NotNull] IFdbTransaction trans, FdbTuple<T1, T2, T3> key)
		{
			trans.Clear(EncodeKey(key));
		}

		public Task<Slice> GetAsync([NotNull] IFdbReadOnlyTransaction trans, T1 key1, T2 key2, T3 key3)
		{
			return trans.GetAsync(EncodeKey(key1, key2, key3));
		}

		#endregion

		#region Key Encoding/Decoding...

		public virtual Slice EncodeKey(FdbTuple<T1, T2, T3> key)
		{
			return this.Key + m_encoder.EncodeKey(key);
		}

		public virtual Slice EncodeKey(T1 key1, T2 key2, T3 key3)
		{
			return this.Key + m_encoder.EncodeKey(key1, key2, key3);
		}

		Slice ICompositeKeyEncoder<FdbTuple<T1, T2, T3>>.EncodeComposite(FdbTuple<T1, T2, T3> key, int items)
		{
			return this.Key + m_encoder.EncodeComposite(key, items);
		}

		public virtual FdbTuple<T1, T2, T3> DecodeKey(Slice encoded)
		{
			return m_encoder.DecodeKey(ExtractKey(encoded, boundCheck: true));
		}

		FdbTuple<T1, T2, T3> ICompositeKeyEncoder<FdbTuple<T1, T2, T3>>.DecodeComposite(Slice encoded, int items)
		{
			return m_encoder.DecodeComposite(ExtractKey(encoded, boundCheck: true), items);
		}

		public virtual FdbKeyRange ToRange(T1 key1, T2 key2, T3 key3)
		{
			return FdbTuple.ToRange(EncodeKey(key1, key2, key3));
		}

		#endregion

	}

}
