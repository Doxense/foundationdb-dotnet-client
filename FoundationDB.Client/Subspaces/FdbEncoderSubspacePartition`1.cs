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
using JetBrains.Annotations;

namespace FoundationDB.Client
{
	public struct FdbEncoderSubspacePartition<T>
	{
		public readonly IFdbSubspace Subspace;
		public readonly IKeyEncoder<T> Encoder;

		public FdbEncoderSubspacePartition([NotNull] IFdbSubspace subspace, [NotNull] IKeyEncoder<T> encoder)
		{
			this.Subspace = subspace;
			this.Encoder = encoder;
		}

		public IFdbSubspace this[T value]
		{
			[NotNull]
			get { return ByKey(value); }
		}

		[NotNull]
		public IFdbSubspace ByKey(T value)
		{
			return this.Subspace[this.Encoder.EncodeKey(value)];
		}

		[NotNull]
		public IFdbDynamicSubspace ByKey(T value, [NotNull] IFdbKeyEncoding encoding)
		{
			return FdbSubspace.CreateDynamic(this.Subspace.ConcatKey(this.Encoder.EncodeKey(value)), encoding);
		}

		[NotNull]
		public IFdbDynamicSubspace ByKey(T value, [NotNull] IDynamicKeyEncoder encoder)
		{
			return FdbSubspace.CreateDynamic(this.Subspace.ConcatKey(this.Encoder.EncodeKey(value)), encoder);
		}

		[NotNull]
		public IFdbEncoderSubspace<TNext> ByKey<TNext>(T value, [NotNull] IFdbKeyEncoding encoding)
		{
			return FdbSubspace.CreateEncoder<TNext>(this.Subspace.ConcatKey(this.Encoder.EncodeKey(value)), encoding);
		}

		[NotNull]
		public IFdbEncoderSubspace<TNext> ByKey<TNext>(T value, [NotNull] IKeyEncoder<TNext> encoder)
		{
			return FdbSubspace.CreateEncoder<TNext>(this.Subspace.ConcatKey(this.Encoder.EncodeKey(value)), encoder);
		}

	}
}