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

namespace FoundationDB.Client
{
	using System;
	using System.Collections.Generic;
	using System.Runtime.CompilerServices;
	using Doxense.Collections.Tuples;
	using Doxense.Diagnostics.Contracts;
	using Doxense.Memory;
	using Doxense.Serialization.Encoders;
	using JetBrains.Annotations;

	public class DynamicKeySubspace : KeySubspace, IDynamicKeySubspace
	{

		/// <summary>Encoder for the keys of this subspace</summary>
		public IKeyEncoding Encoding { get; }

		internal IDynamicKeyEncoder KeyEncoder { get; }

		/// <summary>Create a new subspace from a binary prefix</summary>
		/// <param name="prefix">Prefix of the new subspace</param>
		/// <param name="encoding">Type System used to encode keys in this subspace (optional, will use Tuple Encoding by default)</param>
		internal DynamicKeySubspace(Slice prefix, IKeyEncoding encoding)
			: base(prefix)
		{
			this.Encoding = encoding;
			this.KeyEncoder = encoding.GetDynamicEncoder();
			this.Keys = new DynamicKeys(this, this.KeyEncoder);
			this.Partition = new DynamicPartition(this);
		}

		/// <summary>Return a view of all the possible binary keys of this subspace</summary>
		public DynamicKeys Keys { get; }

		/// <summary>Return a view of all the possible binary keys of this subspace</summary>
		public DynamicPartition Partition { get; }

		public Slice this[ITuple item]
		{
			[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
			get => this.Keys.Pack(item);
		}

	}

	/// <summary>Key helper for a dynamic TypeSystem</summary>
	public sealed class DynamicKeys
	{

		/// <summary>Parent subspace</summary>
		[NotNull]
		private readonly DynamicKeySubspace Parent;

		/// <summary>Encoder used to format keys in this subspace</summary>
		[NotNull]
		public IDynamicKeyEncoder Encoder { get; }

		internal DynamicKeys(DynamicKeySubspace parent, IDynamicKeyEncoder encoder)
		{
			Contract.Requires(parent != null && encoder != null);
			this.Parent = parent;
			this.Encoder = encoder;
		}

		/// <summary>Convert a tuple into a key of this subspace</summary>
		/// <param name="tuple">Tuple that will be packed and appended to the subspace prefix</param>
		public Slice Pack<TTuple>([NotNull] TTuple tuple)
			where TTuple : ITuple
		{
			Contract.NotNullAllowStructs(tuple, nameof(tuple));

			var sw = this.Parent.OpenWriter();
			this.Encoder.PackKey(ref sw, tuple);
			return sw.ToSlice();
		}

		/// <summary>Unpack a key of this subspace, back into a tuple</summary>
		/// <param name="packedKey">Key that was produced by a previous call to <see cref="Pack{TTuple}"/></param>
		/// <returns>Original tuple</returns>
		public ITuple Unpack(Slice packedKey)
		{
			return this.Encoder.UnpackKey(this.Parent.ExtractKey(packedKey));
		}

		#region ToRange()...

		/// <summary>Return a key range that encompass all the keys inside this subspace, according to the current key encoder</summary>
		public KeyRange ToRange()
		{
			return this.Encoder.ToRange(this.Parent.GetPrefix());
		}

		/// <summary>Return a key range that encompass all the keys inside a partition of this subspace, according to the current key encoder</summary>
		/// <param name="tuple">Tuple used as a prefix for the range</param>
		public KeyRange ToRange([NotNull] ITuple tuple)
		{
			return this.Encoder.ToRange(this.Parent.GetPrefix(), tuple);
		}

		public KeyRange ToRange<T1>(STuple<T1> tuple)
		{
			return this.Encoder.ToRange(this.Parent.GetPrefix(), tuple);
		}

		public KeyRange ToRange<T1, T2>(STuple<T1, T2> tuple)
		{
			return this.Encoder.ToRange(this.Parent.GetPrefix(), tuple);
		}

		public KeyRange ToRange<T1, T2, T3>(STuple<T1, T2, T3> tuple)
		{
			return this.Encoder.ToRange(this.Parent.GetPrefix(), tuple);
		}

		public KeyRange ToRange<T1, T2, T3, T4>(STuple<T1, T2, T3, T4> tuple)
		{
			return this.Encoder.ToRange(this.Parent.GetPrefix(), tuple);
		}
		public KeyRange ToRange<T1, T2, T3, T4, T5>(STuple<T1, T2, T3, T4, T5> tuple)
		{
			return this.Encoder.ToRange(this.Parent.GetPrefix(), tuple);
		}
		public KeyRange ToRange<T1, T2, T3, T4, T5, T6>(STuple<T1, T2, T3, T4, T5, T6> tuple)
		{
			return this.Encoder.ToRange(this.Parent.GetPrefix(), tuple);
		}

		#endregion

		#region ToKeyRange()...

		public KeyRange ToKeyRange<T1>(T1 item1)
		{
			return this.Encoder.ToKeyRange(this.Parent.GetPrefix(), item1);
		}

		public KeyRange ToKeyRange<T1, T2>(T1 item1, T2 item2)
		{
			return this.Encoder.ToKeyRange(this.Parent.GetPrefix(), item1, item2);
		}

		public KeyRange ToKeyRange<T1, T2, T3>(T1 item1, T2 item2, T3 item3)
		{
			return this.Encoder.ToKeyRange(this.Parent.GetPrefix(), item1, item2, item3);
		}

		public KeyRange ToKeyRange<T1, T2, T3, T4>(T1 item1, T2 item2, T3 item3, T4 item4)
		{
			return this.Encoder.ToKeyRange(this.Parent.GetPrefix(), item1, item2, item3, item4);
		}

		public KeyRange ToKeyRange<T1, T2, T3, T4, T5>(T1 item1, T2 item2, T3 item3, T4 item4, T5 item5)
		{
			return this.Encoder.ToKeyRange(this.Parent.GetPrefix(), item1, item2, item3, item4, item5);
		}
		public KeyRange ToKeyRange<T1, T2, T3, T4, T5, T6>(T1 item1, T2 item2, T3 item3, T4 item4, T5 item5, T6 item6)
		{
			return this.Encoder.ToKeyRange(this.Parent.GetPrefix(), item1, item2, item3, item4, item5, item6);
		}
		public KeyRange ToKeyRange<T1, T2, T3, T4, T5, T6, T7>(T1 item1, T2 item2, T3 item3, T4 item4, T5 item5, T6 item6, T7 item7)
		{
			return this.Encoder.ToKeyRange(this.Parent.GetPrefix(), item1, item2, item3, item4, item5, item6, item7);
		}
		public KeyRange ToKeyRange<T1, T2, T3, T4, T5, T6, T7, T8>(T1 item1, T2 item2, T3 item3, T4 item4, T5 item5, T6 item6, T7 item7, T8 item8)
		{
			return this.Encoder.ToKeyRange(this.Parent.GetPrefix(), item1, item2, item3, item4, item5, item6, item7, item8);
		}

		#endregion

		#region Encode...

		/// <summary>Encode a key which is composed of a single element</summary>
		public Slice Encode<T1>(T1 item1)
		{
			var sw = this.Parent.OpenWriter();
			this.Encoder.EncodeKey(ref sw, item1);
			return sw.ToSlice();
		}

		/// <summary>Encode a batch of keys, each one composed of a single element</summary>
		public Slice[] EncodeMany<T>(IEnumerable<T> items)
		{
			return Batched<T, IDynamicKeyEncoder>.Convert(
				this.Parent.OpenWriter(),
				items,
				(ref SliceWriter writer, T item, IDynamicKeyEncoder encoder) => encoder.EncodeKey<T>(ref writer, item),
				this.Encoder
			);
		}

		/// <summary>Encode a batch of keys, each one composed of a single value extracted from each elements</summary>
		public Slice[] EncodeMany<TSource, T>(IEnumerable<TSource> items, Func<TSource, T> selector)
		{
			return Batched<TSource, IDynamicKeyEncoder>.Convert(
				this.Parent.OpenWriter(),
				items,
				(ref SliceWriter writer, TSource item, IDynamicKeyEncoder encoder) => encoder.EncodeKey<T>(ref writer, selector(item)),
				this.Encoder
			);
		}

		/// <summary>Encode a key which is composed of a two elements</summary>
		public Slice Encode<T1, T2>(T1 item1, T2 item2)
		{
			var sw = this.Parent.OpenWriter();
			this.Encoder.EncodeKey(ref sw, item1, item2);
			return sw.ToSlice();
		}

		/// <summary>Encode a key which is composed of three elements</summary>
		public Slice Encode<T1, T2, T3>(T1 item1, T2 item2, T3 item3)
		{
			var sw = this.Parent.OpenWriter();
			this.Encoder.EncodeKey(ref sw, item1, item2, item3);
			return sw.ToSlice();
		}

		/// <summary>Encode a key which is composed of four elements</summary>
		public Slice Encode<T1, T2, T3, T4>(T1 item1, T2 item2, T3 item3, T4 item4)
		{
			var sw = this.Parent.OpenWriter();
			this.Encoder.EncodeKey(ref sw, item1, item2, item3, item4);
			return sw.ToSlice();
		}

		/// <summary>Encode a key which is composed of five elements</summary>
		public Slice Encode<T1, T2, T3, T4, T5>(T1 item1, T2 item2, T3 item3, T4 item4, T5 item5)
		{
			var sw = this.Parent.OpenWriter();
			this.Encoder.EncodeKey(ref sw, item1, item2, item3, item4, item5);
			return sw.ToSlice();
		}

		/// <summary>Encode a key which is composed of six elements</summary>
		public Slice Encode<T1, T2, T3, T4, T5, T6>(T1 item1, T2 item2, T3 item3, T4 item4, T5 item5, T6 item6)
		{
			var sw = this.Parent.OpenWriter();
			this.Encoder.EncodeKey(ref sw, item1, item2, item3, item4, item5, item6);
			return sw.ToSlice();
		}

		#endregion

		#region Decode...

		/// <summary>Decode a key of this subspace, composed of a single element</summary>
		public T1 Decode<T1>(Slice packedKey)
		{
			return this.Encoder.DecodeKey<T1>(this.Parent.ExtractKey(packedKey));
		}

		/// <summary>Decode a key of this subspace, composed of exactly two elements</summary>
		public STuple<T1, T2> Decode<T1, T2>(Slice packedKey)
		{
			return this.Encoder.DecodeKey<T1, T2>(this.Parent.ExtractKey(packedKey));
		}

		/// <summary>Decode a key of this subspace, composed of exactly three elements</summary>
		public STuple<T1, T2, T3> Decode<T1, T2, T3>(Slice packedKey)
		{
			return this.Encoder.DecodeKey<T1, T2, T3>(this.Parent.ExtractKey(packedKey));
		}

		/// <summary>Decode a key of this subspace, composed of exactly four elements</summary>
		public STuple<T1, T2, T3, T4> Decode<T1, T2, T3, T4>(Slice packedKey)
		{
			return this.Encoder.DecodeKey<T1, T2, T3, T4>(this.Parent.ExtractKey(packedKey));
		}

		/// <summary>Decode a key of this subspace, composed of exactly five elements</summary>
		public STuple<T1, T2, T3, T4, T5> Decode<T1, T2, T3, T4, T5>(Slice packedKey)
		{
			return this.Encoder.DecodeKey<T1, T2, T3, T4, T5>(this.Parent.ExtractKey(packedKey));
		}

		public STuple<T1, T2, T3, T4, T5, T6> Decode<T1, T2, T3, T4, T5, T6>(Slice packedKey)
		{
			return this.Encoder.DecodeKey<T1, T2, T3, T4, T5, T6>(this.Parent.ExtractKey(packedKey));
		}

		/// <summary>Decode a key of this subspace, and return only the first element without decoding the rest the key.</summary>
		/// <remarks>This method is faster than unpacking the complete key and reading only the first element.</remarks>
		public TFirst DecodeFirst<TFirst>(Slice packedKey)
		{
			return this.Encoder.DecodeKeyFirst<TFirst>(this.Parent.ExtractKey(packedKey));
		}

		/// <summary>Decode a key of this subspace, and return only the last element without decoding the rest.</summary>
		/// <remarks>This method is faster than unpacking the complete key and reading only the last element.</remarks>
		public TLast DecodeLast<TLast>(Slice packedKey)
		{
			return this.Encoder.DecodeKeyLast<TLast>(this.Parent.ExtractKey(packedKey));
		}

		#endregion

		/// <summary>Return a user-friendly string representation of a key of this subspace</summary>
		public string Dump(Slice packedKey)
		{
			//TODO: defer to the encoding itself?
			var key = this.Parent.ExtractKey(packedKey);
			try
			{
				var tuple = TuPack.Unpack(key);
				return tuple.ToString();
			}
			catch (FormatException)
			{
				// this is not a tuple???
			}
			return key.PrettyPrint();
		}

	}

	public /*readonly*/ struct DynamicPartition
	{

		[NotNull]
		public readonly IDynamicKeySubspace Subspace;


		internal DynamicPartition([NotNull] DynamicKeySubspace subspace)
		{
			Contract.Requires(subspace != null);
			this.Subspace = subspace;
		}

		public IDynamicKeySubspace this[Slice binarySuffix]
		{
			[Pure, NotNull]
			get => new DynamicKeySubspace(this.Subspace[binarySuffix], this.Subspace.Encoding);
		}

		public IDynamicKeySubspace this[ITuple suffix]
		{
			[Pure, NotNull]
			get => new DynamicKeySubspace(this.Subspace.Keys.Pack(suffix), this.Subspace.Encoding);
		}

		/// <summary>Partition this subspace into a child subspace</summary>
		/// <typeparam name="T">Type of the child subspace key</typeparam>
		/// <param name="value">Value of the child subspace</param>
		/// <returns>New subspace that is logically contained by the current subspace</returns>
		/// <remarks>Subspace([Foo, ]).Partition(Bar) is equivalent to Subspace([Foo, Bar, ])</remarks>
		/// <example>
		/// new FdbSubspace(["Users", ]).Partition("Contacts") == new FdbSubspace(["Users", "Contacts", ])
		/// </example>
		[Pure, NotNull]
		public IDynamicKeySubspace ByKey<T>(T value)
		{
			return new DynamicKeySubspace(this.Subspace.Keys.Encode<T>(value), this.Subspace.Encoding);
		}

		/// <summary>Partition this subspace into a child subspace</summary>
		/// <typeparam name="T1">Type of the first subspace key</typeparam>
		/// <typeparam name="T2">Type of the second subspace key</typeparam>
		/// <param name="value1">Value of the first subspace key</param>
		/// <param name="value2">Value of the second subspace key</param>
		/// <returns>New subspace that is logically contained by the current subspace</returns>
		/// <remarks>Subspace([Foo, ]).Partition(Bar, Baz) is equivalent to Subspace([Foo, Bar, Baz])</remarks>
		/// <example>
		/// new FdbSubspace(["Users", ]).Partition("Contacts", "Friends") == new FdbSubspace(["Users", "Contacts", "Friends", ])
		/// </example>
		[Pure, NotNull]
		public IDynamicKeySubspace ByKey<T1, T2>(T1 value1, T2 value2)
		{
			return new DynamicKeySubspace(this.Subspace.Keys.Encode<T1, T2>(value1, value2), this.Subspace.Encoding);
		}

		/// <summary>Partition this subspace into a child subspace</summary>
		/// <typeparam name="T1">Type of the first subspace key</typeparam>
		/// <typeparam name="T2">Type of the second subspace key</typeparam>
		/// <typeparam name="T3">Type of the third subspace key</typeparam>
		/// <param name="value1">Value of the first subspace key</param>
		/// <param name="value2">Value of the second subspace key</param>
		/// <param name="value3">Value of the third subspace key</param>
		/// <returns>New subspace that is logically contained by the current subspace</returns>
		/// <example>
		/// new FdbSubspace(["Users", ]).Partition("John Smith", "Contacts", "Friends") == new FdbSubspace(["Users", "John Smith", "Contacts", "Friends", ])
		/// </example>
		[Pure, NotNull]
		public IDynamicKeySubspace ByKey<T1, T2, T3>(T1 value1, T2 value2, T3 value3)
		{
			return new DynamicKeySubspace(this.Subspace.Keys.Encode<T1, T2, T3>(value1, value2, value3), this.Subspace.Encoding);
		}

		/// <summary>Partition this subspace into a child subspace</summary>
		/// <typeparam name="T1">Type of the first subspace key</typeparam>
		/// <typeparam name="T2">Type of the second subspace key</typeparam>
		/// <typeparam name="T3">Type of the third subspace key</typeparam>
		/// <typeparam name="T4">Type of the fourth subspace key</typeparam>
		/// <param name="value1">Value of the first subspace key</param>
		/// <param name="value2">Value of the second subspace key</param>
		/// <param name="value3">Value of the third subspace key</param>
		/// <param name="value4">Value of the fourth subspace key</param>
		/// <returns>New subspace that is logically contained by the current subspace</returns>
		/// <example>
		/// new FdbSubspace(["Users", ]).Partition("John Smith", "Contacts", "Friends", "Messages") == new FdbSubspace(["Users", "John Smith", "Contacts", "Friends", "Messages", ])
		/// </example>
		[Pure, NotNull]
		public IDynamicKeySubspace ByKey<T1, T2, T3, T4>(T1 value1, T2 value2, T3 value3, T4 value4)
		{
			return new DynamicKeySubspace(this.Subspace.Keys.Encode<T1, T2, T3, T4>(value1, value2, value3, value4), this.Subspace.Encoding);
		}

	}

}
