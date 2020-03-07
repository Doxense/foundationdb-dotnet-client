#region BSD License
/* Copyright (c) 2013-2020, Doxense SAS
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

#if !USE_SHARED_FRAMEWORK

namespace FoundationDB.Client
{
	using System;
	using System.Diagnostics.CodeAnalysis;
	using Doxense.Collections.Tuples;
	using Doxense.Memory;
	using Doxense.Serialization.Encoders;

	public abstract class DynamicKeyEncoderBase : IDynamicKeyEncoder
	{

		IKeyEncoding IKeyEncoder.Encoding => this.Encoding;

		public abstract IDynamicKeyEncoding Encoding { get; }

		public virtual (Slice Begin, Slice End) ToRange(Slice prefix)
		{
			return KeyRange.StartsWith(prefix);
		}

		public abstract void PackKey<TTuple>(ref SliceWriter writer, TTuple items) where TTuple : IVarTuple;

		public virtual void EncodeKey<T1>(ref SliceWriter writer, T1 item1)
		{
			PackKey(ref writer, STuple.Create(item1));
		}

		public virtual void EncodeKey<T1, T2>(ref SliceWriter writer, T1 item1, T2 item2)
		{
			PackKey(ref writer, STuple.Create(item1, item2));
		}

		public virtual void EncodeKey<T1, T2, T3>(ref SliceWriter writer, T1 item1, T2 item2, T3 item3)
		{
			PackKey(ref writer, STuple.Create(item1, item2, item3));
		}

		public virtual void EncodeKey<T1, T2, T3, T4>(ref SliceWriter writer, T1 item1, T2 item2, T3 item3, T4 item4)
		{
			PackKey(ref writer, STuple.Create(item1, item2, item3, item4));
		}

		public virtual void EncodeKey<T1, T2, T3, T4, T5>(ref SliceWriter writer, T1 item1, T2 item2, T3 item3, T4 item4, T5 item5)
		{
			PackKey(ref writer, STuple.Create(item1, item2, item3, item4, item5));
		}

		public virtual void EncodeKey<T1, T2, T3, T4, T5, T6>(ref SliceWriter writer, T1 item1, T2 item2, T3 item3, T4 item4, T5 item5, T6 item6)
		{
			PackKey(ref writer, STuple.Create(item1, item2, item3, item4, item5, item6));
		}

		public virtual void EncodeKey<T1, T2, T3, T4, T5, T6, T7>(ref SliceWriter writer, T1 item1, T2 item2, T3 item3, T4 item4, T5 item5, T6 item6, T7 item7)
		{
			PackKey(ref writer, STuple.Create(item1, item2, item3, item4, item5, item6, item7));
		}

		public virtual void EncodeKey<T1, T2, T3, T4, T5, T6, T7, T8>(ref SliceWriter writer, T1 item1, T2 item2, T3 item3, T4 item4, T5 item5, T6 item6, T7 item7, T8 item8)
		{
			PackKey(ref writer, STuple.Create(item1, item2, item3, item4, item5, item6, item7, item8));
		}

		/// <inheritdoc />
		public abstract IVarTuple UnpackKey(Slice packed);

		/// <inheritdoc />
		public abstract bool TryUnpackKey(Slice packed, out IVarTuple? tuple);

		[return: MaybeNull]
		public virtual T DecodeKey<T>(Slice packed)
		{
			return UnpackKey(packed).OfSize(1).Get<T>(0);
		}

		[return: MaybeNull]
		public virtual T DecodeKeyFirst<T>(Slice packed)
		{
			return UnpackKey(packed).OfSizeAtLeast(1).Get<T>(0);
		}

		[return: MaybeNull]
		public virtual T DecodeKeyLast<T>(Slice packed)
		{
			return UnpackKey(packed).OfSizeAtLeast(1).Get<T>(-1);
		}

		public virtual (T1, T2) DecodeKey<T1, T2>(Slice packed)
		{
			return UnpackKey(packed).As<T1, T2>();
		}

		public virtual (T1, T2, T3) DecodeKey<T1, T2, T3>(Slice packed)
		{
			return UnpackKey(packed).As<T1, T2, T3>();
		}

		public virtual (T1, T2, T3, T4) DecodeKey<T1, T2, T3, T4>(Slice packed)
		{
			return UnpackKey(packed).As<T1, T2, T3, T4>();
		}

		public virtual (T1, T2, T3, T4, T5) DecodeKey<T1, T2, T3, T4, T5>(Slice packed)
		{
			return UnpackKey(packed).As<T1, T2, T3, T4, T5>();
		}

		public virtual (T1, T2, T3, T4, T5, T6) DecodeKey<T1, T2, T3, T4, T5, T6>(Slice packed)
		{
			return UnpackKey(packed).As<T1, T2, T3, T4, T5, T6>();
		}

		public virtual (Slice Begin, Slice End) ToRange<TTuple>(Slice prefix, TTuple items)
			where TTuple : IVarTuple
		{
			var writer = new SliceWriter(prefix, 16);
			PackKey(ref writer, items);
			return ToRange(writer.ToSlice());
		}

		public virtual (Slice Begin, Slice End) ToKeyRange<T1>(Slice prefix, T1 item1)
		{
			return ToRange(prefix, STuple.Create(item1));
		}

		public virtual (Slice Begin, Slice End) ToKeyRange<T1, T2>(Slice prefix, T1 item1, T2 item2)
		{
			return ToRange(prefix, STuple.Create(item1, item2));
		}

		public virtual (Slice Begin, Slice End) ToKeyRange<T1, T2, T3>(Slice prefix, T1 item1, T2 item2, T3 item3)
		{
			return ToRange(prefix, STuple.Create(item1, item3, item3));
		}

		public virtual (Slice Begin, Slice End) ToKeyRange<T1, T2, T3, T4>(Slice prefix, T1 item1, T2 item2, T3 item3, T4 item4)
		{
			return ToRange(prefix, STuple.Create(item1, item3, item3, item4));
		}

		public virtual (Slice Begin, Slice End) ToKeyRange<T1, T2, T3, T4, T5>(Slice prefix, T1 item1, T2 item2, T3 item3, T4 item4, T5 item5)
		{
			return ToRange(prefix, STuple.Create(item1, item3, item3, item4, item5));
		}

		public virtual (Slice Begin, Slice End) ToKeyRange<T1, T2, T3, T4, T5, T6>(Slice prefix, T1 item1, T2 item2, T3 item3, T4 item4, T5 item5, T6 item6)
		{
			return ToRange(prefix, STuple.Create(item1, item3, item3, item4, item5, item6));
		}

		public virtual (Slice Begin, Slice End) ToKeyRange<T1, T2, T3, T4, T5, T6, T7>(Slice prefix, T1 item1, T2 item2, T3 item3, T4 item4, T5 item5, T6 item6, T7 item7)
		{
			return ToRange(prefix, STuple.Create(item1, item3, item3, item4, item5, item6, item7));
		}

		public virtual (Slice Begin, Slice End) ToKeyRange<T1, T2, T3, T4, T5, T6, T7, T8>(Slice prefix, T1 item1, T2 item2, T3 item3, T4 item4, T5 item5, T6 item6, T7 item7, T8 item8)
		{
			return ToRange(prefix, STuple.Create(item1, item3, item3, item4, item5, item6, item7, item8));
		}
	}
}

#endif
