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

namespace FoundationDB.Layers.Tuples
{
	using System;
	using FoundationDB.Client;

	public sealed class TupleKeyEncoder : IDynamicKeyEncoder
	{

		internal static TupleKeyEncoder Instance = new TupleKeyEncoder();

		private TupleKeyEncoder()
		{ }

		public IFdbKeyEncoding Encoding
		{
			get { return TypeSystem.Tuples; }
		}

		public KeyRange ToRange(Slice prefix)
		{
			return FdbTuple.ToRange(prefix);
		}

		public void PackKey(ref SliceWriter writer, IFdbTuple items)
		{
			var tw = new TupleWriter(writer);
			FdbTuple.Pack(ref tw, items);
			writer = tw.Output;
		}

		public void EncodeKey<T1>(ref SliceWriter writer, T1 item1)
		{
			var tw = new TupleWriter(writer);
			FdbTuplePacker<T1>.SerializeTo(ref tw, item1);
			writer = tw.Output;
		}

		public void EncodeKey<T1, T2>(ref SliceWriter writer, T1 item1, T2 item2)
		{
			var tw = new TupleWriter(writer);
			FdbTuplePacker<T1>.SerializeTo(ref tw, item1);
			FdbTuplePacker<T2>.SerializeTo(ref tw, item2);
			writer = tw.Output;
		}

		public void EncodeKey<T1, T2, T3>(ref SliceWriter writer, T1 item1, T2 item2, T3 item3)
		{
			var tw = new TupleWriter(writer);
			FdbTuplePacker<T1>.SerializeTo(ref tw, item1);
			FdbTuplePacker<T2>.SerializeTo(ref tw, item2);
			FdbTuplePacker<T3>.SerializeTo(ref tw, item3);
			writer = tw.Output;
		}

		public void EncodeKey<T1, T2, T3, T4>(ref SliceWriter writer, T1 item1, T2 item2, T3 item3, T4 item4)
		{
			var tw = new TupleWriter(writer);
			FdbTuplePacker<T1>.SerializeTo(ref tw, item1);
			FdbTuplePacker<T2>.SerializeTo(ref tw, item2);
			FdbTuplePacker<T3>.SerializeTo(ref tw, item3);
			FdbTuplePacker<T4>.SerializeTo(ref tw, item4);
			writer = tw.Output;
		}

		public void EncodeKey<T1, T2, T3, T4, T5>(ref SliceWriter writer, T1 item1, T2 item2, T3 item3, T4 item4, T5 item5)
		{
			var tw = new TupleWriter(writer);
			FdbTuplePacker<T1>.SerializeTo(ref tw, item1);
			FdbTuplePacker<T2>.SerializeTo(ref tw, item2);
			FdbTuplePacker<T3>.SerializeTo(ref tw, item3);
			FdbTuplePacker<T4>.SerializeTo(ref tw, item4);
			FdbTuplePacker<T5>.SerializeTo(ref tw, item5);
			writer = tw.Output;
		}

		public void EncodeKey<T1, T2, T3, T4, T5, T6>(ref SliceWriter writer, T1 item1, T2 item2, T3 item3, T4 item4, T5 item5, T6 item6)
		{
			var tw = new TupleWriter(writer);
			FdbTuplePacker<T1>.SerializeTo(ref tw, item1);
			FdbTuplePacker<T2>.SerializeTo(ref tw, item2);
			FdbTuplePacker<T3>.SerializeTo(ref tw, item3);
			FdbTuplePacker<T4>.SerializeTo(ref tw, item4);
			FdbTuplePacker<T5>.SerializeTo(ref tw, item5);
			FdbTuplePacker<T6>.SerializeTo(ref tw, item6);
			writer = tw.Output;
		}

		public void EncodeKey<T1, T2, T3, T4, T5, T6, T7>(ref SliceWriter writer, T1 item1, T2 item2, T3 item3, T4 item4, T5 item5, T6 item6, T7 item7)
		{
			var tw = new TupleWriter(writer);
			FdbTuplePacker<T1>.SerializeTo(ref tw, item1);
			FdbTuplePacker<T2>.SerializeTo(ref tw, item2);
			FdbTuplePacker<T3>.SerializeTo(ref tw, item3);
			FdbTuplePacker<T4>.SerializeTo(ref tw, item4);
			FdbTuplePacker<T5>.SerializeTo(ref tw, item5);
			FdbTuplePacker<T6>.SerializeTo(ref tw, item6);
			FdbTuplePacker<T7>.SerializeTo(ref tw, item7);
			writer = tw.Output;
		}

		public void EncodeKey<T1, T2, T3, T4, T5, T6, T7, T8>(ref SliceWriter writer, T1 item1, T2 item2, T3 item3, T4 item4, T5 item5, T6 item6, T7 item7, T8 item8)
		{
			var tw = new TupleWriter(writer);
			FdbTuplePacker<T1>.SerializeTo(ref tw, item1);
			FdbTuplePacker<T2>.SerializeTo(ref tw, item2);
			FdbTuplePacker<T3>.SerializeTo(ref tw, item3);
			FdbTuplePacker<T4>.SerializeTo(ref tw, item4);
			FdbTuplePacker<T5>.SerializeTo(ref tw, item5);
			FdbTuplePacker<T6>.SerializeTo(ref tw, item6);
			FdbTuplePacker<T7>.SerializeTo(ref tw, item7);
			FdbTuplePacker<T8>.SerializeTo(ref tw, item8);
			writer = tw.Output;
		}

		public IFdbTuple UnpackKey(Slice packed)
		{
			return FdbTuple.Unpack(packed);
		}

		public T DecodeKey<T>(Slice packed)
		{
			return FdbTuple.DecodeKey<T>(packed);
		}

		public T DecodeKeyFirst<T>(Slice packed)
		{
			return FdbTuple.DecodeFirst<T>(packed);
		}

		public T DecodeKeyLast<T>(Slice packed)
		{
			return FdbTuple.DecodeLast<T>(packed);
		}

		public FdbTuple<T1, T2> DecodeKey<T1, T2>(Slice packed)
		{
			return FdbTuple.DecodeKey<T1, T2>(packed);
		}

		public FdbTuple<T1, T2, T3> DecodeKey<T1, T2, T3>(Slice packed)
		{
			return FdbTuple.DecodeKey<T1, T2, T3>(packed);
		}

		public FdbTuple<T1, T2, T3, T4> DecodeKey<T1, T2, T3, T4>(Slice packed)
		{
			return FdbTuple.DecodeKey<T1, T2, T3, T4>(packed);
		}

		public FdbTuple<T1, T2, T3, T4, T5> DecodeKey<T1, T2, T3, T4, T5>(Slice packed)
		{
			return FdbTuple.DecodeKey<T1, T2, T3, T4, T5>(packed);
		}

		public KeyRange ToRange(Slice prefix, IFdbTuple items)
		{
			return FdbTuple.ToRange(prefix, items);
		}

		public KeyRange ToKeyRange<T1>(Slice prefix, T1 item1)
		{
			return FdbTuple.ToRange(prefix, FdbTuple.Create(item1));
		}

		public KeyRange ToKeyRange<T1, T2>(Slice prefix, T1 item1, T2 item2)
		{
			return FdbTuple.ToRange(prefix, FdbTuple.Create(item1, item2));
		}

		public KeyRange ToKeyRange<T1, T2, T3>(Slice prefix, T1 item1, T2 item2, T3 item3)
		{
			return FdbTuple.ToRange(prefix, FdbTuple.Create(item1, item3, item3));
		}

		public KeyRange ToKeyRange<T1, T2, T3, T4>(Slice prefix, T1 item1, T2 item2, T3 item3, T4 item4)
		{
			return FdbTuple.ToRange(prefix, FdbTuple.Create(item1, item3, item3, item4));
		}

		public KeyRange ToKeyRange<T1, T2, T3, T4, T5>(Slice prefix, T1 item1, T2 item2, T3 item3, T4 item4, T5 item5)
		{
			return FdbTuple.ToRange(prefix, FdbTuple.Create(item1, item3, item3, item4, item5));
		}

		public KeyRange ToKeyRange<T1, T2, T3, T4, T5, T6>(Slice prefix, T1 item1, T2 item2, T3 item3, T4 item4, T5 item5, T6 item6)
		{
			return FdbTuple.ToRange(prefix, FdbTuple.Create(item1, item3, item3, item4, item5, item6));
		}

		public KeyRange ToKeyRange<T1, T2, T3, T4, T5, T6, T7>(Slice prefix, T1 item1, T2 item2, T3 item3, T4 item4, T5 item5, T6 item6, T7 item7)
		{
			return FdbTuple.ToRange(prefix, FdbTuple.Create(item1, item3, item3, item4, item5, item6, item7));
		}

		public KeyRange ToKeyRange<T1, T2, T3, T4, T5, T6, T7, T8>(Slice prefix, T1 item1, T2 item2, T3 item3, T4 item4, T5 item5, T6 item6, T7 item7, T8 item8)
		{
			return FdbTuple.ToRange(prefix, FdbTuple.Create(item1, item3, item3, item4, item5, item6, item7, item8));
		}
	}

}