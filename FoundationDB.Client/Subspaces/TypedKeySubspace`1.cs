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
	using System.Diagnostics;
	using System.Runtime.CompilerServices;
	using Doxense.Collections.Tuples;
	using Doxense.Diagnostics.Contracts;
	using Doxense.Serialization.Encoders;
	using JetBrains.Annotations;

	[PublicAPI]
	public interface ITypedKeySubspace<T1> : IKeySubspace
	{
		/// <summary>Return a view of all the possible keys of this subspace</summary>
		[NotNull]
		TypedKeys<T1> Keys { get; }

		/// <summary>Encoding used to generate and parse the keys of this subspace</summary>
		[NotNull]
		IKeyEncoder<T1> KeyEncoder { get; }

	}

	/// <summary>Subspace that knows how to encode and decode its key</summary>
	/// <typeparam name="T1">Type of the key handled by this subspace</typeparam>
	[PublicAPI]
	public sealed class TypedKeySubspace<T1> : KeySubspace, ITypedKeySubspace<T1>
	{
		public IKeyEncoder<T1> KeyEncoder { get; }

		internal TypedKeySubspace(IKeyContext context, [NotNull] IKeyEncoder<T1> encoder)
			: base(context)
		{
			Contract.Requires(encoder != null);
			this.KeyEncoder = encoder;
			this.Keys = new TypedKeys<T1>(this, this.KeyEncoder);
		}

		public TypedKeys<T1> Keys { get; }

		Slice IKeySubspace.this[Slice relativeKey] => throw new NotSupportedException("This method is not supported by subspaces of this type.");

	}

	/// <summary>Encodes and Decodes keys composed of a single element</summary>
	/// <typeparam name="T1">Type of the key handled by this subspace</typeparam>
	[DebuggerDisplay("{Parent.ToString(),nq)}")]
	[PublicAPI]
	public sealed class TypedKeys<T1>
	{

		[NotNull]
		private readonly TypedKeySubspace<T1> Parent;

		[NotNull]
		public IKeyEncoder<T1> Encoder { get; }

		internal TypedKeys(
			[NotNull] TypedKeySubspace<T1> parent,
			[NotNull] IKeyEncoder<T1> encoder)
		{
			Contract.Requires(parent != null && encoder != null);
			this.Parent = parent;
			this.Encoder = encoder;
		}

		#region ToRange()

		/// <summary>Return the range of all legal keys in this subpsace</summary>
		/// <returns>A "legal" key is one that can be decoded into the original pair of values</returns>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public KeyRange ToRange()
		{
			return this.Parent.ToRange();
		}

		/// <summary>Return the range of all legal keys in this subpsace, that start with the specified value</summary>
		/// <returns>Range that encompass all keys that start with (tuple.Item1, ..)</returns>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public KeyRange ToRange(STuple<T1> tuple)
		{
			return ToRange(tuple.Item1);
		}

		/// <summary>Return the range of all legal keys in this subpsace, that start with the specified value</summary>
		/// <returns>Range that encompass all keys that start with (tuple.Item1, ..)</returns>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public KeyRange ToRange(ValueTuple<T1> tuple)
		{
			return ToRange(tuple.Item1);
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public KeyRange ToRange(T1 item1)
		{
			//TODO: add concept of "range" on IKeyEncoder ?
			return KeyRange.PrefixedBy(Encode(item1));
		}

		#endregion

		#region Pack()

		public Slice this[ValueTuple<T1> items]
		{
			[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
			get => Encode(items.Item1);
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public Slice Pack(STuple<T1> tuple)
		{
			return Encode(tuple.Item1);
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public Slice Pack(ValueTuple<T1> tuple)
		{
			return Encode(tuple.Item1);
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public Slice Pack<TTuple>([NotNull] TTuple tuple)
			where TTuple : IVarTuple
		{
			return Encode(tuple.OfSize(1).Get<T1>(0));
		}

		#endregion

		#region Encode()

		public Slice this[T1 item1]
		{
			[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
			get => Encode(item1);
		}

		[Pure]
		public Slice Encode(T1 item1)
		{
			var bytes = this.Encoder.EncodeKey(item1);
			var sw = this.Parent.OpenWriter(bytes.Count);
			sw.WriteBytes(bytes);
			return sw.ToSlice();
		}

		#endregion

		#region Decode()

		[Pure]
		public T1 Decode(Slice packedKey)
		{
			return this.Encoder.DecodeKey(this.Parent.ExtractKey(packedKey));
		}

		public void Decode(Slice packedKey, out T1 item1)
		{
			item1 = this.Encoder.DecodeKey(this.Parent.ExtractKey(packedKey));
		}

		#endregion

		#region Dump()

		/// <summary>Return a user-friendly string representation of a key of this subspace</summary>
		[Pure]
		public string Dump(Slice packedKey)
		{
			if (packedKey.IsNull) return String.Empty;
			//TODO: defer to the encoding itself?
			var key = this.Parent.ExtractKey(packedKey);
			try
			{
				//REVIEW: we need a TryUnpack!
				return this.Encoder.DecodeKey(key).ToString();
			}
			catch (Exception)
			{ // decoding failed, or some other non-trival
				return key.PrettyPrint();
			}
		}

		#endregion

	}

}
