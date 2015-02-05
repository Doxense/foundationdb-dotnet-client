using System;
using FoundationDB.Layers.Tuples;

namespace FoundationDB.Client
{

	public sealed class TupleTypeSystem : IFdbTypeSystem
	{

		public FdbKeyRange ToRange(Slice key)
		{
			return FdbTuple.ToRange(key);
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

	}
}