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
	using JetBrains.Annotations;
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using System.Threading.Tasks;

	/// <summary>Subspace that knows how to encode and decode its key</summary>
	/// <typeparam name="T">Type of the key handled by this subspace</typeparam>
	public class FdbEncoderSubspace<T> : FdbSubspace, IKeyEncoder<T>
	{
		/// <summary>Reference to the wrapped subspace</summary>
		private readonly IFdbSubspace m_base;

		/// <summary>Encoder used to handle keys</summary>
		private readonly IKeyEncoder<T> m_encoder;

		/// <summary>Wrap an existing subspace with a specific key encoder</summary>
		/// <param name="subspace">Original subspace</param>
		/// <param name="encoder">Key encoder</param>
		public FdbEncoderSubspace([NotNull] IFdbSubspace subspace, [NotNull] IKeyEncoder<T> encoder)
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

		/// <summary>Encoder used by this subpsace to format keys</summary>
		public IKeyEncoder<T> Encoder
		{
			[NotNull]
			get { return m_encoder; }
		}

		#region Transaction Helpers...

		public void Set([NotNull] IFdbTransaction trans, T key, Slice value)
		{
			if (trans == null) throw new ArgumentNullException("trans");
			trans.Set(EncodeKey(key), value);
		}

		public void SetValues([NotNull] IFdbTransaction trans, [NotNull] IEnumerable<KeyValuePair<T, Slice>> items)
		{
			if (trans == null) throw new ArgumentNullException("trans");
			if (items == null) throw new ArgumentNullException("items");
			//TODO: find a way to mass convert all the keys using the same buffer?
			trans.SetValues(items.Select(item => new KeyValuePair<Slice, Slice>(EncodeKey(item.Key), item.Value)));
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
			return trans.GetValuesAsync(EncodeKeys(keys));
		}

		public Task<Slice[]> GetValuesAsync([NotNull] IFdbReadOnlyTransaction trans, [NotNull] IEnumerable<T> keys)
		{
			return trans.GetValuesAsync(EncodeKeys(keys));
		}

		#endregion

		#region Key Encoding/Decoding...

		public Slice EncodeKey(T key)
		{
			return this.Key + m_encoder.EncodeKey(key);
		}

		[NotNull]
		public Slice[] EncodeKeys([NotNull] IEnumerable<T> keys)
		{
			return ConcatKeys(m_encoder.EncodeKeys(keys));
		}

		[NotNull]
		public Slice[] EncodeKeys([NotNull] params T[] keys)
		{
			return ConcatKeys(m_encoder.EncodeKeys(keys));
		}

		[NotNull]
		public Slice[] EncodeKeys<TElement>([NotNull] IEnumerable<TElement> elements, Func<TElement, T> selector)
		{
			return ConcatKeys(m_encoder.EncodeKeys(elements, selector));
		}

		[NotNull]
		public Slice[] EncodeKeys<TElement>([NotNull] TElement[] elements, Func<TElement, T> selector)
		{
			return ConcatKeys(m_encoder.EncodeKeys(elements, selector));
		}

		public T DecodeKey(Slice encoded)
		{
			return m_encoder.DecodeKey(ExtractKey(encoded, boundCheck: true));
		}

		[NotNull]
		public T[] DecodeKeys([NotNull] IEnumerable<Slice> encoded)
		{
			return m_encoder.DecodeKeys(ExtractKeys(encoded, boundCheck: true));
		}

		[NotNull]
		public T[] DecodeKeys([NotNull] params Slice[] encoded)
		{
			return m_encoder.DecodeKeys(ExtractKeys(encoded, boundCheck: true));
		}

		public virtual FdbKeyRange ToRange(T key)
		{
			return FdbTuple.ToRange(EncodeKey(key));
		}

		[NotNull]
		public FdbKeyRange[] ToRange([NotNull] T[] keys)
		{
			var packed = EncodeKeys(keys);

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
