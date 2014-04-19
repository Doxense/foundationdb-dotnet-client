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

namespace FoundationDB.Client
{
	using FoundationDB.Client.Utils;
	using FoundationDB.Layers.Tuples;
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using System.Threading.Tasks;

	public class FdbEncoderSubspace<T1, T2> : FdbSubspace, ICompositeKeyEncoder<T1, T2>
	{
		protected readonly FdbSubspace m_parent;
		protected readonly ICompositeKeyEncoder<T1, T2> m_encoder;
		protected volatile FdbEncoderSubspace<T1> m_head;

		public FdbEncoderSubspace(FdbSubspace subspace, ICompositeKeyEncoder<T1, T2> encoder)
			: base(subspace)
		{
			if (subspace == null) throw new ArgumentNullException("subspace");
			if (encoder == null) throw new ArgumentNullException("encoder");
			m_parent = subspace;
			m_encoder = encoder;
		}

		/// <summary>Gets the key encoder</summary>
		public ICompositeKeyEncoder<T1, T2> Encoder { get { return m_encoder; } }

		/// <summary>Returns a partial encoder for (T1,)</summary>
		public FdbEncoderSubspace<T1> Partial
		{
			get
			{
				if (m_head == null)
				{
					m_head = new FdbEncoderSubspace<T1>(m_parent, KeyValueEncoders.Head(m_encoder));
				}
				return m_head;
			}
		}

		#region Transaction Helpers...

		public void Set(IFdbTransaction trans, T1 key1, T2 key2, Slice value)
		{
			trans.Set(EncodeKey(key1, key2), value);
		}

		public void Set(IFdbTransaction trans, FdbTuple<T1, T2> key, Slice value)
		{
			trans.Set(EncodeKey(key), value);
		}

		public void Clear(IFdbTransaction trans, T1 key1, T2 key2)
		{
			trans.Clear(EncodeKey(key1, key2));
		}

		public void Clear(IFdbTransaction trans, FdbTuple<T1, T2> key)
		{
			trans.Clear(EncodeKey(key));
		}

		public Task<Slice> GetAsync(IFdbReadOnlyTransaction trans, T1 key1, T2 key2)
		{
			return trans.GetAsync(EncodeKey(key1, key2));
		}

		#endregion

		#region Key Encoding/Decoding...

		public virtual Slice EncodeKey(FdbTuple<T1, T2> key)
		{
			return this.Key + m_encoder.EncodeKey(key);
		}

		public virtual Slice EncodeKey(T1 key1, T2 key2)
		{
			return this.Key + m_encoder.EncodeKey(key1, key2);
		}

		Slice ICompositeKeyEncoder<FdbTuple<T1, T2>>.EncodeComposite(FdbTuple<T1, T2> key, int items)
		{
			return this.Key + m_encoder.EncodeComposite(key, items);
		}

		public virtual FdbTuple<T1, T2> DecodeKey(Slice encoded)
		{
			return m_encoder.DecodeKey(this.ExtractAndCheck(encoded));
		}

		FdbTuple<T1, T2> ICompositeKeyEncoder<FdbTuple<T1, T2>>.DecodeComposite(Slice encoded, int items)
		{
			return m_encoder.DecodeComposite(this.ExtractAndCheck(encoded), items);
		}

		public virtual FdbKeyRange ToRange(T1 key1, T2 key2)
		{
			return FdbTuple.ToRange(this.EncodeKey(key1, key2));
		}

		#endregion

	}

}
