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
	using System.Diagnostics.Contracts;
	using FoundationDB.Layers.Tuples;
	using JetBrains.Annotations;

	/// <summary>Subspace that knows how to encode and decode its key</summary>
	/// <typeparam name="T">Type of the key handled by this subspace</typeparam>
	public sealed class TypedKeySubspace<T> : KeySubspace, ITypedKeySubspace<T>
	{
		public TypedKeySubspace(Slice rawPrefix, [NotNull] IKeyEncoder<T> encoder)
			: this(rawPrefix, true, encoder)
		{ }

		internal TypedKeySubspace(Slice rawPrefix, bool copy, [NotNull] IKeyEncoder<T> encoder)
			: base(rawPrefix, copy)
		{
			this.Encoder = encoder ?? throw new ArgumentNullException(nameof(encoder));
			this.Keys = new TypedKeys<T>(this, encoder);
		}

		[NotNull]
		public IKeyEncoder<T> Encoder { get; }

		public TypedKeys<T> Keys { get; }

		public TypedSubspacePartition<T> Partition => new TypedSubspacePartition<T>(this, Encoder);
	}

	/// <summary>Encodes and Decodes keys composed of a single element</summary>
	/// <typeparam name="T">Type of the key handled by this subspace</typeparam>
	public /*readonly*/ struct TypedKeys<T>
	{

		[NotNull]
		public readonly IKeySubspace Subspace;

		[NotNull]
		public readonly IKeyEncoder<T> Encoder;

		public TypedKeys([NotNull] IKeySubspace subspace, [NotNull] IKeyEncoder<T> encoder)
		{
			Contract.Requires(subspace != null && encoder != null);
			this.Subspace = subspace;
			this.Encoder = encoder;
		}

		public Slice this[T value] => Encode(value);

		public Slice Encode(T value)
		{
			return this.Subspace.ConcatKey(this.Encoder.EncodeKey(value));
		}

		public Slice[] Encode([NotNull] IEnumerable<T> values)
		{
			if (values == null) throw new ArgumentNullException(nameof(values));
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

		public KeyRange ToRange(T value)
		{
			//REVIEW: which semantic for ToRange() should we use?
			return STuple.ToRange(Encode(value));
		}

	}

	public /*readonly*/ struct TypedSubspacePartition<T>
	{

		[NotNull]
		public readonly IKeySubspace Subspace;

		[NotNull]
		public readonly IKeyEncoder<T> Encoder;

		public TypedSubspacePartition([NotNull] IKeySubspace subspace, [NotNull] IKeyEncoder<T> encoder)
		{
			Contract.Requires(subspace != null && encoder != null);
			this.Subspace = subspace;
			this.Encoder = encoder;
		}

		[NotNull]
		public IKeySubspace this[T value] => ByKey(value);

		[NotNull]
		public IKeySubspace ByKey(T value)
		{
			return this.Subspace[this.Encoder.EncodeKey(value)];
		}

		[NotNull]
		public IDynamicKeySubspace ByKey(T value, [NotNull] IKeyEncoding encoding)
		{
			return KeySubspace.CreateDynamic(this.Subspace.ConcatKey(this.Encoder.EncodeKey(value)), encoding);
		}

		[NotNull]
		public IDynamicKeySubspace ByKey(T value, [NotNull] IDynamicKeyEncoder encoder)
		{
			return KeySubspace.CreateDynamic(this.Subspace.ConcatKey(this.Encoder.EncodeKey(value)), encoder);
		}

		[NotNull]
		public ITypedKeySubspace<TNext> ByKey<TNext>(T value, [NotNull] IKeyEncoding encoding)
		{
			return KeySubspace.CreateEncoder<TNext>(this.Subspace.ConcatKey(this.Encoder.EncodeKey(value)), encoding);
		}

		[NotNull]
		public ITypedKeySubspace<TNext> ByKey<TNext>(T value, [NotNull] IKeyEncoder<TNext> encoder)
		{
			return KeySubspace.CreateEncoder<TNext>(this.Subspace.ConcatKey(this.Encoder.EncodeKey(value)), encoder);
		}

	}

}
