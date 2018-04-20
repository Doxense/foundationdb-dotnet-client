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

namespace FoundationDB.Client
{
	using System;
	using System.Collections.Generic;
	using System.Diagnostics.Contracts;
	using FoundationDB.Layers.Tuples;
	using JetBrains.Annotations;

	public struct FdbEncoderSubspaceKeys<T>
	{

		[NotNull]
		public readonly IFdbSubspace Subspace;

		[NotNull]
		public readonly IKeyEncoder<T> Encoder;

		public FdbEncoderSubspaceKeys([NotNull] IFdbSubspace subspace, [NotNull] IKeyEncoder<T> encoder)
		{
			Contract.Requires(subspace != null && encoder != null);
			this.Subspace = subspace;
			this.Encoder = encoder;
		}

		public Slice this[T value]
		{
			get { return Encode(value); }
		}

		public Slice Encode(T value)
		{
			return this.Subspace.ConcatKey(this.Encoder.EncodeKey(value));
		}

		public Slice[] Encode([NotNull] IEnumerable<T> values)
		{
			if (values == null) throw new ArgumentNullException("values");
			return Batched<T, IKeyEncoder<T>>.Convert(
				this.Subspace.GetWriter(),
				values,
				(ref SliceWriter writer, T value, IKeyEncoder<T> encoder) => { writer.WriteBytes(encoder.EncodeKey(value)); },
				this.Encoder
				);
		}

		public T Decode(Slice packed)
		{
			return this.Encoder.DecodeKey(this.Subspace.ExtractKey(packed));
		}

		public FdbKeyRange ToRange(T value)
		{
			//REVIEW: which semantic for ToRange() should we use?
			return FdbTuple.ToRange(Encode(value));
		}

	}
}
