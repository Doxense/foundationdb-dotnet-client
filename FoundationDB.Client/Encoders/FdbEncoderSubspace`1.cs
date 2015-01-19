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
	using FoundationDB.Layers.Tuples;
	using JetBrains.Annotations;
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using System.Threading.Tasks;

	public class FdbEncoderSubspace<T> : FdbSubspace, IKeyEncoder<T>
	{
		protected readonly FdbSubspace m_parent;
		protected readonly IKeyEncoder<T> m_encoder;

		public FdbEncoderSubspace([NotNull] FdbSubspace subspace, [NotNull] IKeyEncoder<T> encoder)
			: base(subspace)
		{
			if (subspace == null) throw new ArgumentNullException("subspace");
			if (encoder == null) throw new ArgumentNullException("encoder");
			m_parent = subspace;
			m_encoder = encoder;
		}

		public IKeyEncoder<T> Encoder
		{
			[NotNull]
			get { return m_encoder; }
		}

		#region Transaction Helpers...

		public void Set([NotNull] IFdbTransaction trans, T key, Slice value)
		{
			trans.Set(EncodeKey(key), value);
		}

		public void Clear([NotNull] IFdbTransaction trans, T key)
		{
			trans.Clear(EncodeKey(key));
		}

		public Task<Slice> GetAsync([NotNull] IFdbReadOnlyTransaction trans, T key)
		{
			return trans.GetAsync(EncodeKey(key));
		}

		public Task<Slice[]> GetValuesAsync([NotNull] IFdbReadOnlyTransaction trans, [NotNull] T[] keys)
		{
			return trans.GetValuesAsync(EncodeKeyRange(keys));
		}

		public Task<Slice[]> GetValuesAsync([NotNull] IFdbReadOnlyTransaction trans, [NotNull] IEnumerable<T> keys)
		{
			return trans.GetValuesAsync(EncodeKeyRange(keys));
		}

		#endregion

		#region Key Encoding/Decoding...

		public Slice EncodeKey(T key)
		{
			return this.Key + m_encoder.EncodeKey(key);
		}

		[NotNull]
		public Slice[] EncodeKeyRange([NotNull] T[] keys)
		{
			return FdbKey.Merge(this.Key, m_encoder.EncodeRange(keys));
		}

		[NotNull]
		public Slice[] EncodeKeyRange<TElement>([NotNull] TElement[] elements, Func<TElement, T> selector)
		{
			return FdbKey.Merge(this.Key, m_encoder.EncodeRange(elements, selector));
		}

		[NotNull]
		public Slice[] EncodeKeyRange([NotNull] IEnumerable<T> keys)
		{
			return FdbKey.Merge(this.Key, m_encoder.EncodeRange(keys));
		}

		public T DecodeKey(Slice encoded)
		{
			return m_encoder.DecodeKey(this.ExtractAndCheck(encoded));
		}

		[NotNull]
		public T[] DecodeKeyRange([NotNull] Slice[] encoded)
		{
			var extracted = new Slice[encoded.Length];
			for (int i = 0; i < encoded.Length; i++)
			{
				extracted[i] = ExtractAndCheck(encoded[i]);
			}
			return m_encoder.DecodeRange(extracted);
		}

		[NotNull]
		public IEnumerable<T> DecodeKeys([NotNull] IEnumerable<Slice> source)
		{
			return source.Select(key => m_encoder.DecodeKey(key));
		}

		public virtual FdbKeyRange ToRange(T key)
		{
			return FdbTuple.ToRange(EncodeKey(key));
		}

		[NotNull]
		public FdbKeyRange[] ToRange([NotNull] T[] keys)
		{
			var packed = EncodeKeyRange(keys);

			var ranges = new FdbKeyRange[keys.Length];
			for (int i = 0; i < ranges.Length; i++)
			{
				ranges[i] = FdbTuple.ToRange(packed[i]);
			}
			return ranges;
		}

		#endregion


	}
	
}
