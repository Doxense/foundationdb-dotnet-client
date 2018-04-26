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

namespace Doxense.Collections.Tuples.Encoding
{
	using System;
	using Doxense.Collections.Tuples;
	using Doxense.Memory;
	using Doxense.Serialization.Encoders;
	using FoundationDB.Client;

	/// <summary>Encoder for variable-length elements, that uses the Tuple Binary Encoding format</summary>
	public sealed class TupleKeyEncoder : IDynamicKeyEncoder
	{

		internal static TupleKeyEncoder Instance = new TupleKeyEncoder();

		private TupleKeyEncoder()
		{ }

		public IKeyEncoding Encoding => TypeSystem.Tuples;

		public void PackKey<TTuple>(ref SliceWriter writer, TTuple items)
			where TTuple : ITuple
		{
			var tw = new TupleWriter(writer);
			TupleEncoder.WriteTo(ref tw, items);
			writer = tw.Output;
		}

		public void EncodeKey<T1>(ref SliceWriter writer, T1 item1)
		{
			var tw = new TupleWriter(writer);
			TuplePacker<T1>.SerializeTo(ref tw, item1);
			writer = tw.Output;
		}

		public void EncodeKey<T1, T2>(ref SliceWriter writer, T1 item1, T2 item2)
		{
			var tw = new TupleWriter(writer);
			TuplePacker<T1>.SerializeTo(ref tw, item1);
			TuplePacker<T2>.SerializeTo(ref tw, item2);
			writer = tw.Output;
		}

		public void EncodeKey<T1, T2, T3>(ref SliceWriter writer, T1 item1, T2 item2, T3 item3)
		{
			var tw = new TupleWriter(writer);
			TuplePacker<T1>.SerializeTo(ref tw, item1);
			TuplePacker<T2>.SerializeTo(ref tw, item2);
			TuplePacker<T3>.SerializeTo(ref tw, item3);
			writer = tw.Output;
		}

		public void EncodeKey<T1, T2, T3, T4>(ref SliceWriter writer, T1 item1, T2 item2, T3 item3, T4 item4)
		{
			var tw = new TupleWriter(writer);
			TuplePacker<T1>.SerializeTo(ref tw, item1);
			TuplePacker<T2>.SerializeTo(ref tw, item2);
			TuplePacker<T3>.SerializeTo(ref tw, item3);
			TuplePacker<T4>.SerializeTo(ref tw, item4);
			writer = tw.Output;
		}

		public void EncodeKey<T1, T2, T3, T4, T5>(ref SliceWriter writer, T1 item1, T2 item2, T3 item3, T4 item4, T5 item5)
		{
			var tw = new TupleWriter(writer);
			TuplePacker<T1>.SerializeTo(ref tw, item1);
			TuplePacker<T2>.SerializeTo(ref tw, item2);
			TuplePacker<T3>.SerializeTo(ref tw, item3);
			TuplePacker<T4>.SerializeTo(ref tw, item4);
			TuplePacker<T5>.SerializeTo(ref tw, item5);
			writer = tw.Output;
		}

		public void EncodeKey<T1, T2, T3, T4, T5, T6>(ref SliceWriter writer, T1 item1, T2 item2, T3 item3, T4 item4, T5 item5, T6 item6)
		{
			var tw = new TupleWriter(writer);
			TuplePacker<T1>.SerializeTo(ref tw, item1);
			TuplePacker<T2>.SerializeTo(ref tw, item2);
			TuplePacker<T3>.SerializeTo(ref tw, item3);
			TuplePacker<T4>.SerializeTo(ref tw, item4);
			TuplePacker<T5>.SerializeTo(ref tw, item5);
			TuplePacker<T6>.SerializeTo(ref tw, item6);
			writer = tw.Output;
		}

		public void EncodeKey<T1, T2, T3, T4, T5, T6, T7>(ref SliceWriter writer, T1 item1, T2 item2, T3 item3, T4 item4, T5 item5, T6 item6, T7 item7)
		{
			var tw = new TupleWriter(writer);
			TuplePacker<T1>.SerializeTo(ref tw, item1);
			TuplePacker<T2>.SerializeTo(ref tw, item2);
			TuplePacker<T3>.SerializeTo(ref tw, item3);
			TuplePacker<T4>.SerializeTo(ref tw, item4);
			TuplePacker<T5>.SerializeTo(ref tw, item5);
			TuplePacker<T6>.SerializeTo(ref tw, item6);
			TuplePacker<T7>.SerializeTo(ref tw, item7);
			writer = tw.Output;
		}

		public void EncodeKey<T1, T2, T3, T4, T5, T6, T7, T8>(ref SliceWriter writer, T1 item1, T2 item2, T3 item3, T4 item4, T5 item5, T6 item6, T7 item7, T8 item8)
		{
			var tw = new TupleWriter(writer);
			TuplePacker<T1>.SerializeTo(ref tw, item1);
			TuplePacker<T2>.SerializeTo(ref tw, item2);
			TuplePacker<T3>.SerializeTo(ref tw, item3);
			TuplePacker<T4>.SerializeTo(ref tw, item4);
			TuplePacker<T5>.SerializeTo(ref tw, item5);
			TuplePacker<T6>.SerializeTo(ref tw, item6);
			TuplePacker<T7>.SerializeTo(ref tw, item7);
			TuplePacker<T8>.SerializeTo(ref tw, item8);
			writer = tw.Output;
		}

		public ITuple UnpackKey(Slice packed)
		{
			return TuPack.Unpack(packed);
		}

		public T DecodeKey<T>(Slice packed)
		{
			return TuPack.DecodeKey<T>(packed);
		}

		public T DecodeKeyFirst<T>(Slice packed)
		{
			return TuPack.DecodeFirst<T>(packed);
		}

		public T DecodeKeyLast<T>(Slice packed)
		{
			return TuPack.DecodeLast<T>(packed);
		}

		public STuple<T1, T2> DecodeKey<T1, T2>(Slice packed)
		{
			return TuPack.DecodeKey<T1, T2>(packed);
		}

		public STuple<T1, T2, T3> DecodeKey<T1, T2, T3>(Slice packed)
		{
			return TuPack.DecodeKey<T1, T2, T3>(packed);
		}

		public STuple<T1, T2, T3, T4> DecodeKey<T1, T2, T3, T4>(Slice packed)
		{
			return TuPack.DecodeKey<T1, T2, T3, T4>(packed);
		}

		public STuple<T1, T2, T3, T4, T5> DecodeKey<T1, T2, T3, T4, T5>(Slice packed)
		{
			return TuPack.DecodeKey<T1, T2, T3, T4, T5>(packed);
		}

		public STuple<T1, T2, T3, T4, T5, T6> DecodeKey<T1, T2, T3, T4, T5, T6>(Slice packed)
		{
			return TuPack.DecodeKey<T1, T2, T3, T4, T5, T6>(packed);
		}

		public KeyRange ToRange(Slice prefix)
		{
			return TuPack.ToRange(prefix);
		}

		public KeyRange ToRange(Slice prefix, ITuple items)
		{
			return TuPack.ToPrefixedKeyRange(prefix, items);
		}

		public KeyRange ToKeyRange<T1>(Slice prefix, T1 item1)
		{
			return TuPack.ToPrefixedKeyRange(prefix, item1);
		}

		public KeyRange ToKeyRange<T1, T2>(Slice prefix, T1 item1, T2 item2)
		{
			return TuPack.ToPrefixedKeyRange(prefix, item1, item2);
		}

		public KeyRange ToKeyRange<T1, T2, T3>(Slice prefix, T1 item1, T2 item2, T3 item3)
		{
			return TuPack.ToPrefixedKeyRange(prefix, item1, item2, item3);
		}

		public KeyRange ToKeyRange<T1, T2, T3, T4>(Slice prefix, T1 item1, T2 item2, T3 item3, T4 item4)
		{
			return TuPack.ToPrefixedKeyRange(prefix, item1, item2, item3, item4);
		}

		public KeyRange ToKeyRange<T1, T2, T3, T4, T5>(Slice prefix, T1 item1, T2 item2, T3 item3, T4 item4, T5 item5)
		{
			return TuPack.ToPrefixedKeyRange(prefix, item1, item2, item3, item4, item5);
		}

		public KeyRange ToKeyRange<T1, T2, T3, T4, T5, T6>(Slice prefix, T1 item1, T2 item2, T3 item3, T4 item4, T5 item5, T6 item6)
		{
			return TuPack.ToPrefixedKeyRange(prefix, item1, item2, item3, item4, item5, item6);
		}

		public KeyRange ToKeyRange<T1, T2, T3, T4, T5, T6, T7>(Slice prefix, T1 item1, T2 item2, T3 item3, T4 item4, T5 item5, T6 item6, T7 item7)
		{
			return TuPack.ToPrefixedKeyRange(prefix, item1, item2, item3, item4, item5, item6, item7);
		}

		public KeyRange ToKeyRange<T1, T2, T3, T4, T5, T6, T7, T8>(Slice prefix, T1 item1, T2 item2, T3 item3, T4 item4, T5 item5, T6 item6, T7 item7, T8 item8)
		{
			return TuPack.ToPrefixedKeyRange(prefix, item1, item2, item3, item4, item5, item6, item7, item8);
		}

	}

}
