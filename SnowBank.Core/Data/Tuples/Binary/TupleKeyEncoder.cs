#region Copyright (c) 2023-2025 SnowBank SAS, (c) 2005-2023 Doxense SAS
// All rights reserved.
// 
// Redistribution and use in source and binary forms, with or without
// modification, are permitted provided that the following conditions are met:
// 	* Redistributions of source code must retain the above copyright
// 	  notice, this list of conditions and the following disclaimer.
// 	* Redistributions in binary form must reproduce the above copyright
// 	  notice, this list of conditions and the following disclaimer in the
// 	  documentation and/or other materials provided with the distribution.
// 	* Neither the name of SnowBank nor the
// 	  names of its contributors may be used to endorse or promote products
// 	  derived from this software without specific prior written permission.
// 
// THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND
// ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
// WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
// DISCLAIMED. IN NO EVENT SHALL SNOWBANK SAS BE LIABLE FOR ANY
// DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
// (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
// LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
// ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
// (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
// SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
#endregion

#pragma warning disable IL2091
#pragma warning disable IL2095

namespace SnowBank.Data.Tuples.Binary
{
	using SnowBank.Data.Tuples;
	using SnowBank.Buffers;
	using SnowBank.Data.Binary;

	/// <summary>Encoder for variable-length elements, that uses the Tuple Binary Encoding format</summary>
	[Obsolete("Use a custom IFdbKeyEncoder<T> instead")]
	public sealed class TupleKeyEncoder : IDynamicKeyEncoder
	{

		public static TupleKeyEncoder Instance = new ();

		private TupleKeyEncoder()
		{ }

		IKeyEncoding IKeyEncoder.Encoding => TuPack.Encoding;

		public IDynamicKeyEncoding Encoding => TuPack.Encoding;

		/// <inheritdoc />
		public void PackKey<TTuple>(ref SliceWriter writer, TTuple items) where TTuple : IVarTuple
		{
			var tw = new TupleWriter(ref writer);
			TupleEncoder.WriteTo(tw, items);
		}

		/// <inheritdoc />
		public bool TryPackKey<TTuple>(Span<byte> destination, out int bytesWritten, TTuple items) where TTuple : IVarTuple
		{
			var writer = new TupleSpanWriter(destination, 0);
			if (!TupleEncoder.TryWriteTo(ref writer, in items))
			{
				bytesWritten = 0;
				return false;
			}

			bytesWritten = writer.BytesWritten;
			return true;
		}

		/// <inheritdoc />
		public void EncodeKey<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T1>(ref SliceWriter writer, T1? item1)
		{
			var tw = new TupleWriter(ref writer);
			TuplePacker<T1>.SerializeTo(tw, item1);
		}

		/// <inheritdoc />
		public void EncodeKey<
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T1,
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T2
		>(ref SliceWriter writer, T1? item1, T2? item2)
		{
			var tw = new TupleWriter(ref writer);
			TuplePacker<T1>.SerializeTo(tw, item1);
			TuplePacker<T2>.SerializeTo(tw, item2);
		}

		/// <inheritdoc />
		public void EncodeKey<T1, T2, T3>(ref SliceWriter writer, T1? item1, T2? item2, T3? item3)
		{
			var tw = new TupleWriter(ref writer);
			TuplePacker<T1>.SerializeTo(tw, item1);
			TuplePacker<T2>.SerializeTo(tw, item2);
			TuplePacker<T3>.SerializeTo(tw, item3);
		}

		/// <inheritdoc />
		public void EncodeKey<T1, T2, T3, T4>(ref SliceWriter writer, T1? item1, T2? item2, T3? item3, T4? item4)
		{
			var tw = new TupleWriter(ref writer);
			TuplePacker<T1>.SerializeTo(tw, item1);
			TuplePacker<T2>.SerializeTo(tw, item2);
			TuplePacker<T3>.SerializeTo(tw, item3);
			TuplePacker<T4>.SerializeTo(tw, item4);
		}

		/// <inheritdoc />
		public void EncodeKey<T1, T2, T3, T4, T5>(ref SliceWriter writer, T1? item1, T2? item2, T3? item3, T4? item4, T5? item5)
		{
			var tw = new TupleWriter(ref writer);
			TuplePacker<T1>.SerializeTo(tw, item1);
			TuplePacker<T2>.SerializeTo(tw, item2);
			TuplePacker<T3>.SerializeTo(tw, item3);
			TuplePacker<T4>.SerializeTo(tw, item4);
			TuplePacker<T5>.SerializeTo(tw, item5);
		}

		/// <inheritdoc />
		public void EncodeKey<T1, T2, T3, T4, T5, T6>(ref SliceWriter writer, T1? item1, T2? item2, T3? item3, T4? item4, T5? item5, T6? item6)
		{
			var tw = new TupleWriter(ref writer);
			TuplePacker<T1>.SerializeTo(tw, item1);
			TuplePacker<T2>.SerializeTo(tw, item2);
			TuplePacker<T3>.SerializeTo(tw, item3);
			TuplePacker<T4>.SerializeTo(tw, item4);
			TuplePacker<T5>.SerializeTo(tw, item5);
			TuplePacker<T6>.SerializeTo(tw, item6);
		}

		/// <inheritdoc />
		public void EncodeKey<T1, T2, T3, T4, T5, T6, T7>(ref SliceWriter writer, T1? item1, T2? item2, T3? item3, T4? item4, T5? item5, T6? item6, T7? item7)
		{
			var tw = new TupleWriter(ref writer);
			TuplePacker<T1>.SerializeTo(tw, item1);
			TuplePacker<T2>.SerializeTo(tw, item2);
			TuplePacker<T3>.SerializeTo(tw, item3);
			TuplePacker<T4>.SerializeTo(tw, item4);
			TuplePacker<T5>.SerializeTo(tw, item5);
			TuplePacker<T6>.SerializeTo(tw, item6);
			TuplePacker<T7>.SerializeTo(tw, item7);
		}

		/// <inheritdoc />
		public void EncodeKey<T1, T2, T3, T4, T5, T6, T7, T8>(ref SliceWriter writer, T1? item1, T2? item2, T3? item3, T4? item4, T5? item5, T6? item6, T7? item7, T8? item8)
		{
			var tw = new TupleWriter(ref writer);
			TuplePacker<T1>.SerializeTo(tw, item1);
			TuplePacker<T2>.SerializeTo(tw, item2);
			TuplePacker<T3>.SerializeTo(tw, item3);
			TuplePacker<T4>.SerializeTo(tw, item4);
			TuplePacker<T5>.SerializeTo(tw, item5);
			TuplePacker<T6>.SerializeTo(tw, item6);
			TuplePacker<T7>.SerializeTo(tw, item7);
			TuplePacker<T8>.SerializeTo(tw, item8);
		}

		/// <inheritdoc />
		public void EncodeKey<T1, T2, T3, T4, T5, T6, T7, T8, T9>(ref SliceWriter writer, T1? item1, T2? item2, T3? item3, T4? item4, T5? item5, T6? item6, T7? item7, T8? item8, T9? item9)
		{
			var tw = new TupleWriter(ref writer);
			TuplePacker<T1>.SerializeTo(tw, item1);
			TuplePacker<T2>.SerializeTo(tw, item2);
			TuplePacker<T3>.SerializeTo(tw, item3);
			TuplePacker<T4>.SerializeTo(tw, item4);
			TuplePacker<T5>.SerializeTo(tw, item5);
			TuplePacker<T6>.SerializeTo(tw, item6);
			TuplePacker<T7>.SerializeTo(tw, item7);
			TuplePacker<T8>.SerializeTo(tw, item8);
			TuplePacker<T9>.SerializeTo(tw, item9);
		}

		/// <inheritdoc />
		public IVarTuple UnpackKey(Slice packed) => TuPack.Unpack(packed);

		/// <inheritdoc />
		public SpanTuple UnpackKey(ReadOnlySpan<byte> packed) => TuPack.Unpack(packed);

		/// <inheritdoc />
		public bool TryUnpackKey(Slice packed, [NotNullWhen(true)] out IVarTuple? tuple)
		{
			if (TuPack.TryUnpack(packed, out var st))
			{
				tuple = st;
				return true;
			}

			tuple = null;
			return false;
		}

		/// <inheritdoc />
		public bool TryUnpackKey(ReadOnlySpan<byte> packed, out SpanTuple tuple)
		{
			return TuPack.TryUnpack(packed, out tuple);
		}

		/// <inheritdoc />
		public T? DecodeKey<T>(Slice packed) => TuPack.DecodeKey<T>(packed);

		/// <inheritdoc />
		public T? DecodeKey<T>(ReadOnlySpan<byte> packed) => TuPack.DecodeKey<T>(packed);

		/// <inheritdoc />
		public T? DecodeKeyAt<T>(Slice packed, int index) => TuPack.DecodeKeyAt<T>(packed, index);

		/// <inheritdoc />
		public T? DecodeKeyAt<T>(ReadOnlySpan<byte> packed, int index) => TuPack.DecodeKeyAt<T>(packed, index);

		/// <inheritdoc />
		public T1? DecodeKeyFirst<T1>(Slice packed, int? expectedSize = null) => TuPack.DecodeFirst<T1>(packed, expectedSize);

		/// <inheritdoc />
		public T1? DecodeKeyFirst<T1>(ReadOnlySpan<byte> packed, int? expectedSize = null) => TuPack.DecodeFirst<T1>(packed, expectedSize);

		/// <inheritdoc />
		public (T1?, T2?) DecodeKeyFirst<T1, T2>(Slice packed, int? expectedSize = null) => TuPack.DecodeFirst<T1, T2>(packed, expectedSize);

		/// <inheritdoc />
		public (T1?, T2?) DecodeKeyFirst<T1, T2>(ReadOnlySpan<byte> packed, int? expectedSize = null) => TuPack.DecodeFirst<T1, T2>(packed, expectedSize);

		/// <inheritdoc />
		public (T1?, T2?, T3?) DecodeKeyFirst<T1, T2, T3>(Slice packed, int? expectedSize = null) => TuPack.DecodeFirst<T1, T2, T3>(packed, expectedSize);

		/// <inheritdoc />
		public (T1?, T2?, T3?) DecodeKeyFirst<T1, T2, T3>(ReadOnlySpan<byte> packed, int? expectedSize = null) => TuPack.DecodeFirst<T1, T2, T3>(packed, expectedSize);

		/// <inheritdoc />
		public T? DecodeKeyLast<T>(Slice packed, int? expectedSize = null) => TuPack.DecodeLast<T>(packed, expectedSize);

		/// <inheritdoc />
		public T? DecodeKeyLast<T>(ReadOnlySpan<byte> packed, int? expectedSize = null) => TuPack.DecodeLast<T>(packed, expectedSize);

		/// <inheritdoc />
		public (T1?, T2?) DecodeKeyLast<T1, T2>(Slice packed, int? expectedSize = null) => TuPack.DecodeLast<T1, T2>(packed, expectedSize);

		/// <inheritdoc />
		public (T1?, T2?) DecodeKeyLast<T1, T2>(ReadOnlySpan<byte> packed, int? expectedSize = null) => TuPack.DecodeLast<T1, T2>(packed, expectedSize);

		/// <inheritdoc />
		public (T1?, T2?, T3?) DecodeKeyLast<T1, T2, T3>(Slice packed, int? expectedSize = null) => TuPack.DecodeLast<T1, T2, T3>(packed, expectedSize);

		/// <inheritdoc />
		public (T1?, T2?, T3?) DecodeKeyLast<T1, T2, T3>(ReadOnlySpan<byte> packed, int? expectedSize = null) => TuPack.DecodeLast<T1, T2, T3>(packed, expectedSize);

		/// <inheritdoc />
		public (T1?, T2?, T3?, T4?) DecodeKeyLast<T1, T2, T3, T4>(Slice packed, int? expectedSize = null) => TuPack.DecodeLast<T1, T2, T3, T4>(packed, expectedSize);

		/// <inheritdoc />
		public (T1?, T2?, T3?, T4?) DecodeKeyLast<T1, T2, T3, T4>(ReadOnlySpan<byte> packed, int? expectedSize = null) => TuPack.DecodeLast<T1, T2, T3, T4>(packed, expectedSize);

		/// <inheritdoc />
		public (T1?, T2?) DecodeKey<T1, T2>(Slice packed) => TuPack.DecodeKey<T1, T2>(packed);

		/// <inheritdoc />
		public (T1?, T2?) DecodeKey<T1, T2>(ReadOnlySpan<byte> packed) => TuPack.DecodeKey<T1, T2>(packed);

		/// <inheritdoc />
		public (T1?, T2?, T3?) DecodeKey<T1, T2, T3>(Slice packed) => TuPack.DecodeKey<T1, T2, T3>(packed);

		/// <inheritdoc />
		public (T1?, T2?, T3?) DecodeKey<T1, T2, T3>(ReadOnlySpan<byte> packed) => TuPack.DecodeKey<T1, T2, T3>(packed);

		/// <inheritdoc />
		public (T1?, T2?, T3?, T4?) DecodeKey<T1, T2, T3, T4>(Slice packed) => TuPack.DecodeKey<T1, T2, T3, T4>(packed);

		/// <inheritdoc />
		public (T1?, T2?, T3?, T4?) DecodeKey<T1, T2, T3, T4>(ReadOnlySpan<byte> packed) => TuPack.DecodeKey<T1, T2, T3, T4>(packed);

		/// <inheritdoc />
		public (T1?, T2?, T3?, T4?, T5?) DecodeKey<T1, T2, T3, T4, T5>(Slice packed) => TuPack.DecodeKey<T1, T2, T3, T4, T5>(packed);

		/// <inheritdoc />
		public (T1?, T2?, T3?, T4?, T5?) DecodeKey<T1, T2, T3, T4, T5>(ReadOnlySpan<byte> packed) => TuPack.DecodeKey<T1, T2, T3, T4, T5>(packed);

		/// <inheritdoc />
		public (T1?, T2?, T3?, T4?, T5?, T6?) DecodeKey<T1, T2, T3, T4, T5, T6>(Slice packed) => TuPack.DecodeKey<T1, T2, T3, T4, T5, T6>(packed);

		/// <inheritdoc />
		public (T1?, T2?, T3?, T4?, T5?, T6?) DecodeKey<T1, T2, T3, T4, T5, T6>(ReadOnlySpan<byte> packed) => TuPack.DecodeKey<T1, T2, T3, T4, T5, T6>(packed);

		/// <inheritdoc />
		public (T1?, T2?, T3?, T4?, T5?, T6?, T7?) DecodeKey<T1, T2, T3, T4, T5, T6, T7>(Slice packed) => TuPack.DecodeKey<T1, T2, T3, T4, T5, T6, T7>(packed);

		/// <inheritdoc />
		public (T1?, T2?, T3?, T4?, T5?, T6?, T7?) DecodeKey<T1, T2, T3, T4, T5, T6, T7>(ReadOnlySpan<byte> packed) => TuPack.DecodeKey<T1, T2, T3, T4, T5, T6, T7>(packed);

		/// <inheritdoc />
		public (T1?, T2?, T3?, T4?, T5?, T6?, T7?, T8?) DecodeKey<T1, T2, T3, T4, T5, T6, T7, T8>(Slice packed) => TuPack.DecodeKey<T1, T2, T3, T4, T5, T6, T7, T8>(packed);

		/// <inheritdoc />
		public (T1?, T2?, T3?, T4?, T5?, T6?, T7?, T8?) DecodeKey<T1, T2, T3, T4, T5, T6, T7, T8>(ReadOnlySpan<byte> packed) => TuPack.DecodeKey<T1, T2, T3, T4, T5, T6, T7, T8>(packed);

		/// <inheritdoc />
		public (T1?, T2?, T3?, T4?, T5?, T6?, T7?, T8?, T9?) DecodeKey<T1, T2, T3, T4, T5, T6, T7, T8, T9>(Slice packed) => TuPack.DecodeKey<T1, T2, T3, T4, T5, T6, T7, T8, T9>(packed);

		/// <inheritdoc />
		public (T1?, T2?, T3?, T4?, T5?, T6?, T7?, T8?, T9?) DecodeKey<T1, T2, T3, T4, T5, T6, T7, T8, T9>(ReadOnlySpan<byte> packed) => TuPack.DecodeKey<T1, T2, T3, T4, T5, T6, T7, T8, T9>(packed);

		/// <inheritdoc />
		public (Slice Begin, Slice End) ToRange(Slice prefix) => TuPack.ToRange(prefix);

		/// <inheritdoc />
		public (Slice Begin, Slice End) ToRange<TTuple>(Slice prefix, TTuple items) where TTuple : IVarTuple => TuPack.ToRange(prefix, items);

		/// <inheritdoc />
		public (Slice Begin, Slice End) ToKeyRange<T1>(Slice prefix, T1? item1) => TuPack.ToPrefixedKeyRange(prefix, item1);

		/// <inheritdoc />
		public (Slice Begin, Slice End) ToKeyRange<T1, T2>(Slice prefix, T1? item1, T2? item2) => TuPack.ToPrefixedKeyRange(prefix, item1, item2);

		/// <inheritdoc />
		public (Slice Begin, Slice End) ToKeyRange<T1, T2, T3>(Slice prefix, T1? item1, T2? item2, T3? item3) => TuPack.ToPrefixedKeyRange(prefix, item1, item2, item3);

		/// <inheritdoc />
		public (Slice Begin, Slice End) ToKeyRange<T1, T2, T3, T4>(Slice prefix, T1? item1, T2? item2, T3? item3, T4? item4) => TuPack.ToPrefixedKeyRange(prefix, item1, item2, item3, item4);

		/// <inheritdoc />
		public (Slice Begin, Slice End) ToKeyRange<T1, T2, T3, T4, T5>(Slice prefix, T1? item1, T2? item2, T3? item3, T4? item4, T5? item5) => TuPack.ToPrefixedKeyRange(prefix, item1, item2, item3, item4, item5);

		/// <inheritdoc />
		public (Slice Begin, Slice End) ToKeyRange<T1, T2, T3, T4, T5, T6>(Slice prefix, T1? item1, T2? item2, T3? item3, T4? item4, T5? item5, T6? item6) => TuPack.ToPrefixedKeyRange(prefix, item1, item2, item3, item4, item5, item6);

		/// <inheritdoc />
		public (Slice Begin, Slice End) ToKeyRange<T1, T2, T3, T4, T5, T6, T7>(Slice prefix, T1? item1, T2? item2, T3? item3, T4? item4, T5? item5, T6? item6, T7? item7) => TuPack.ToPrefixedKeyRange(prefix, item1, item2, item3, item4, item5, item6, item7);

		/// <inheritdoc />
		public (Slice Begin, Slice End) ToKeyRange<T1, T2, T3, T4, T5, T6, T7, T8>(Slice prefix, T1? item1, T2? item2, T3? item3, T4? item4, T5? item5, T6? item6, T7? item7, T8? item8) => TuPack.ToPrefixedKeyRange(prefix, item1, item2, item3, item4, item5, item6, item7, item8);

		/// <inheritdoc />
		public (Slice Begin, Slice End) ToKeyRange<T1, T2, T3, T4, T5, T6, T7, T8, T9>(Slice prefix, T1? item1, T2? item2, T3? item3, T4? item4, T5? item5, T6? item6, T7? item7, T8? item8, T9? item9) => TuPack.ToPrefixedKeyRange(prefix, item1, item2, item3, item4, item5, item6, item7, item8, item9);

	}

}
