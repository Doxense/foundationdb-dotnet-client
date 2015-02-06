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

using System;
using System.Collections.Generic;
using FoundationDB.Layers.Tuples;
using JetBrains.Annotations;

namespace FoundationDB.Client
{
	public struct FdbEncoderSubspaceKeys<T1, T2, T3>
	{

		public readonly IFdbSubspace Subspace;
		public readonly ICompositeKeyEncoder<T1, T2, T3> Encoder;

		public FdbEncoderSubspaceKeys([NotNull] IFdbSubspace subspace, [NotNull] ICompositeKeyEncoder<T1, T2, T3> encoder)
		{
			this.Subspace = subspace;
			this.Encoder = encoder;
		}

		public Slice this[T1 value1, T2 value2, T3 value3]
		{
			get { return Encode(value1, value2, value3); }
		}

		public Slice Encode(T1 value1, T2 value2, T3 value3)
		{
			return this.Subspace.ConcatKey(this.Encoder.EncodeKey(value1, value2, value3));
		}

		public Slice[] Encode<TSource>([NotNull] IEnumerable<TSource> values, [NotNull] Func<TSource, T1> selector1, [NotNull] Func<TSource, T2> selector2, [NotNull] Func<TSource, T3> selector3)
		{
			if (values == null) throw new ArgumentNullException("values");
			return Batched<TSource, ICompositeKeyEncoder<T1, T2, T3>>.Convert(
				this.Subspace.GetWriter(),
				values,
				(ref SliceWriter writer, TSource value, ICompositeKeyEncoder<T1, T2, T3> encoder) => writer.WriteBytes(encoder.EncodeKey(selector1(value), selector2(value), selector3(value))),
				this.Encoder
			);
		}

		public FdbTuple<T1, T2, T3> Decode(Slice packed)
		{
			return this.Encoder.DecodeKey(this.Subspace.ExtractKey(packed));
		}

		public FdbKeyRange ToRange(T1 value1, T2 value2, T3 value3)
		{
			//REVIEW: which semantic for ToRange() should we use?
			return FdbTuple.ToRange(Encode(value1, value2, value3));
		}

	}
}