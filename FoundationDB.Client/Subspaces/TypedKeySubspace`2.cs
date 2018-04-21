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

using FoundationDB.Client.Utils;

namespace FoundationDB.Client
{
	using System;
	using System.Collections.Generic;
	using Doxense.Diagnostics.Contracts;
	using JetBrains.Annotations;
	using FoundationDB.Layers.Tuples;

	/// <summary>Subspace that knows how to encode and decode its key</summary>
	/// <typeparam name="T1">Type of the first item of the keys handled by this subspace</typeparam>
	/// <typeparam name="T2">Type of the second item of the keys handled by this subspace</typeparam>
	public sealed class TypedKeySubspace<T1, T2> : KeySubspace, ITypedKeySubspace<T1, T2>
	{
		// ReSharper disable once FieldCanBeMadeReadOnly.Local

		public TypedKeySubspace(Slice rawPrefix, [NotNull] ICompositeKeyEncoder<T1, T2> encoder)
			: this(rawPrefix, true, encoder)
		{ }

		internal TypedKeySubspace(Slice rawPrefix, bool copy, [NotNull] ICompositeKeyEncoder<T1, T2> encoder)
			: base(rawPrefix, copy)
		{
			this.Encoder = encoder ?? throw new ArgumentNullException(nameof(encoder));
			this.Keys = new TypedKeys<T1, T2>(this, encoder);
			this.Partition = new TypedSubspacePartition<T1, T2>(this, Encoder);
		}

		public ITypedKeySubspace<T1> Partial => m_partial ?? (m_partial = new TypedKeySubspace<T1>(GetKeyPrefix(), false, KeyValueEncoders.Head(Encoder)));
		private TypedKeySubspace<T1> m_partial;

		public ICompositeKeyEncoder<T1, T2> Encoder { get; }

		public TypedKeys<T1, T2> Keys { get; }

		public TypedSubspacePartition<T1, T2> Partition { get; }

	}

	public /*readonly*/ struct TypedKeys<T1, T2>
	{

		[NotNull]
		public readonly IKeySubspace Subspace;

		[NotNull]
		public readonly ICompositeKeyEncoder<T1, T2> Encoder;

		public TypedKeys([NotNull] IKeySubspace subspace, [NotNull] ICompositeKeyEncoder<T1, T2> encoder)
		{
			Contract.Requires(subspace != null && encoder != null);
			this.Subspace = subspace;
			this.Encoder = encoder;
		}

		public Slice this[T1 value1, T2 value2] => Encode(value1, value2);

		public Slice Encode(T1 value1, T2 value2)
		{
			return this.Subspace.ConcatKey(this.Encoder.EncodeKey(value1, value2));
		}

		public Slice[] Encode<TSource>([NotNull] IEnumerable<TSource> values, [NotNull] Func<TSource, T1> selector1, [NotNull] Func<TSource, T2> selector2)
		{
			Contract.NotNull(values, nameof(values));
			return Batched<TSource, ICompositeKeyEncoder<T1, T2>>.Convert(
				this.Subspace.GetWriter(),
				values,
				(ref SliceWriter writer, TSource value, ICompositeKeyEncoder<T1, T2> encoder) => writer.WriteBytes(encoder.EncodeKey(selector1(value), selector2(value))),
				this.Encoder
			);
		}

		public STuple<T1, T2> Decode(Slice packed)
		{
			return this.Encoder.DecodeKey(this.Subspace.ExtractKey(packed));
		}

		public KeyRange ToRange(T1 value1, T2 value2)
		{
			//REVIEW: which semantic for ToRange() should we use?
			return STuple.ToRange(Encode(value1, value2));
		}

	}

	public /*readonly*/ struct TypedSubspacePartition<T1, T2>
	{
		[NotNull]
		public readonly IKeySubspace Subspace;

		[NotNull]
		public readonly ICompositeKeyEncoder<T1, T2> Encoder;

		public TypedSubspacePartition([NotNull] IKeySubspace subspace, [NotNull] ICompositeKeyEncoder<T1, T2> encoder)
		{
			Contract.Requires(subspace != null && encoder != null);
			this.Subspace = subspace;
			this.Encoder = encoder;
		}

		[NotNull]
		public IKeySubspace this[T1 value1, T2 value2] => ByKey(value1, value2);

		[NotNull]
		public IKeySubspace ByKey(T1 value1, T2 value2)
		{
			return this.Subspace[this.Encoder.EncodeKey(value1, value2)];
		}

		[NotNull]
		public IDynamicKeySubspace ByKey(T1 value1, T2 value2, IKeyEncoding encoding)
		{
			return KeySubspace.CreateDynamic(this.Subspace.ConcatKey(this.Encoder.EncodeKey(value1, value2)), encoding);
		}

		[NotNull]
		public IDynamicKeySubspace ByKey(T1 value1, T2 value2, IDynamicKeyEncoder encoder)
		{
			return KeySubspace.CreateDynamic(this.Subspace.ConcatKey(this.Encoder.EncodeKey(value1, value2)), encoder);
		}

		[NotNull]
		public ITypedKeySubspace<TNext> ByKey<TNext>(T1 value1, T2 value2, IKeyEncoding encoding)
		{
			return KeySubspace.CreateEncoder<TNext>(this.Subspace.ConcatKey(this.Encoder.EncodeKey(value1, value2)), encoding);
		}

		[NotNull]
		public ITypedKeySubspace<TNext> ByKey<TNext>(T1 value1, T2 value2, IKeyEncoder<TNext> encoder)
		{
			return KeySubspace.CreateEncoder<TNext>(this.Subspace.ConcatKey(this.Encoder.EncodeKey(value1, value2)), encoder);
		}

	}

}
