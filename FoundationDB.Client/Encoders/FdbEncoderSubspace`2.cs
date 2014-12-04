#region BSD Licence
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
	using System.Threading.Tasks;

	/// <summary>Subspace that knows how to encode and decode its key</summary>
	/// <typeparam name="T1">Type of the first item of the keys handled by this subspace</typeparam>
	/// <typeparam name="T2">Type of the second item of the keys handled by this subspace</typeparam>
	public class FdbEncoderSubspace<T1, T2> : FdbSubspace, ICompositeKeyEncoder<T1, T2>
	{
		/// <summary>Reference to the wrapped subspace</summary>
		private readonly IFdbSubspace m_base;

		/// <summary>Encoder used to handle keys</summary>
		private readonly ICompositeKeyEncoder<T1, T2> m_encoder;

		/// <summary>Version of this subspace that encodes only the first key</summary>
		private volatile FdbEncoderSubspace<T1> m_head;

		public FdbEncoderSubspace([NotNull] IFdbSubspace subspace, [NotNull] ICompositeKeyEncoder<T1, T2> encoder)
			: base(subspace)
		{
			if (subspace == null) throw new ArgumentNullException("subspace");
			if (encoder == null) throw new ArgumentNullException("encoder");
			m_base = subspace;
			m_encoder = encoder;
		}

		/// <summary>Untyped version of this subspace</summary>
		public IFdbSubspace Base
		{
			get { return m_base; }
		}

		/// <summary>Gets the key encoder</summary>
		public ICompositeKeyEncoder<T1, T2> Encoder
		{
			[NotNull]
			get { return m_encoder; }
		}

		/// <summary>Returns a partial encoder for (T1,)</summary>
		public FdbEncoderSubspace<T1> Partial
		{
			[NotNull]
			get { return m_head ?? (m_head = new FdbEncoderSubspace<T1>(m_base, KeyValueEncoders.Head(m_encoder))); }
		}

		#region Transaction Helpers...

		public void Set([NotNull] IFdbTransaction trans, T1 key1, T2 key2, Slice value)
		{
			trans.Set(EncodeKey(key1, key2), value);
		}

		public void Set([NotNull] IFdbTransaction trans, FdbTuple<T1, T2> key, Slice value)
		{
			trans.Set(EncodeKey(key), value);
		}

		public void Clear([NotNull] IFdbTransaction trans, T1 key1, T2 key2)
		{
			trans.Clear(EncodeKey(key1, key2));
		}

		public void Clear([NotNull] IFdbTransaction trans, FdbTuple<T1, T2> key)
		{
			trans.Clear(EncodeKey(key));
		}

		public Task<Slice> GetAsync([NotNull] IFdbReadOnlyTransaction trans, T1 key1, T2 key2)
		{
			return trans.GetAsync(EncodeKey(key1, key2));
		}

		#endregion

		#region Key Encoding/Decoding...

		public virtual Slice EncodeKey(FdbTuple<T1, T2> key)
		{
			return ConcatKey(m_encoder.EncodeKey(key));
		}

		public virtual Slice EncodeKey(T1 key1, T2 key2)
		{
			return ConcatKey(m_encoder.EncodeKey(key1, key2));
		}

		public virtual Slice EncodeKey(T1 key1)
		{
			return ConcatKey(m_encoder.EncodeComposite(FdbTuple.Create<T1, T2>(key1, default(T2)), 1));
		}

		Slice ICompositeKeyEncoder<FdbTuple<T1, T2>>.EncodeComposite(FdbTuple<T1, T2> key, int items)
		{
			return ConcatKey(m_encoder.EncodeComposite(key, items));
		}

		public virtual FdbTuple<T1, T2> DecodeKey(Slice encoded)
		{
			return m_encoder.DecodeKey(ExtractKey(encoded, boundCheck: true));
		}

		FdbTuple<T1, T2> ICompositeKeyEncoder<FdbTuple<T1, T2>>.DecodeComposite(Slice encoded, int items)
		{
			return m_encoder.DecodeComposite(ExtractKey(encoded, boundCheck: true), items);
		}

		public virtual FdbKeyRange ToRange(FdbTuple<T1, T2> key)
		{
			return FdbTuple.ToRange(EncodeKey(key));
		}

		public virtual FdbKeyRange ToRange(T1 key1, T2 key2)
		{
			return FdbTuple.ToRange(EncodeKey(key1, key2));
		}

		public virtual FdbKeyRange ToRange(T1 key1)
		{
			return FdbTuple.ToRange(EncodeKey(key1));
		}

		#endregion

	}

}
