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

namespace FoundationDB.Client
{
	/// <summary>Represents a key subspace than can statically typed values to and from their binary representation</summary>
	/// <typeparam name="T1">Type of values</typeparam>
	[PublicAPI]
	public interface ITypedKeySubspace<T1> : IKeySubspace
	{

		/// <summary>Encoding used to generate and parse the keys of this subspace</summary>
		IKeyEncoder<T1> KeyEncoder { get; }

		/// <summary>Encode a pair of values into a key in this subspace</summary>
		/// <param name="item1">Value</param>
		/// <returns>Encoded key in this subspace</returns>
		/// <remarks>
		/// The key can be decoded back into its original components using <see cref="Decode(Slice)"/>.
		/// This class is a shortcut to calling <see cref="Encode"/>
		/// </remarks>
		Slice this[T1? item1] { get; }

		/// <summary>Pack a 1-tuple into a key in this subspace</summary>
		/// <param name="items">Value</param>
		/// <returns>Encoded key in this subspace</returns>
		/// <remarks>
		/// This class is a shortcut to calling <see cref="Encode">Encode(items.Item1)</see>
		/// </remarks>
		Slice this[ValueTuple<T1> items] { get; }

		/// <summary>Encode a value into a key in this subspace</summary>
		/// <param name="item1">Value of the key</param>
		/// <returns>Encoded key in this subspace</returns>
		/// <remarks>The key can be decoded back into its original components using <see cref="Decode(Slice)"/></remarks>
		Slice Encode(T1? item1);

		/// <summary>Encode a value into a key in this subspace</summary>
		/// <param name="destination">Buffer where the full binary key should be written</param>
		/// <param name="bytesWritten">Number of bytes written to the buffer</param>
		/// <param name="item1">Value of the key</param>
		/// <returns>Encoded key in this subspace</returns>
		/// <remarks>The key can be decoded back into its original components using <see cref="Decode(Slice)"/></remarks>
		bool TryEncode(Span<byte> destination, out int bytesWritten, T1? item1);

		/// <summary>Decode a key from this subspace back into a value</summary>
		/// <param name="packedKey">Key previously generated by calling <see cref="Encode"/></param>
		T1? Decode(Slice packedKey);

	}

	/// <summary>Represents a key subspace than can statically typed values to and from their binary representation</summary>
	/// <typeparam name="T1">Type of values</typeparam>
	[PublicAPI]
	public sealed class TypedKeySubspace<T1> : KeySubspace, ITypedKeySubspace<T1>
	{
		public IKeyEncoder<T1> KeyEncoder { get; }

		internal TypedKeySubspace(Slice prefix, IKeyEncoder<T1> encoder, ISubspaceContext context)
			: base(prefix, context)
		{
			Contract.Debug.Requires(encoder != null);
			this.KeyEncoder = encoder;
		}

		public Slice this[T1? item1]
		{
			[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
			get => Encode(item1);
		}

		public Slice this[ValueTuple<T1> items]
		{
			[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
			get => Encode(items.Item1);
		}

		[Pure]
		public Slice Encode(T1? item1)
		{
			var sw = this.OpenWriter(12);
			this.KeyEncoder.WriteKeyTo(ref sw, item1);
			return sw.ToSlice();
		}

		/// <inheritdoc />
		public bool TryEncode(Span<byte> destination, out int bytesWritten, T1? item1)
		{
			if (!this.GetPrefix().TryCopyTo(destination, out var prefixLen)
			 || !KeyEncoder.TryWriteKeyTo(destination[prefixLen..], out var keyLen, item1))
			{
				bytesWritten = 0;
				return false;
			}
			bytesWritten = prefixLen + keyLen;
			return true;
		}

		[Pure]
		public T1? Decode(Slice packedKey)
		{
			return this.KeyEncoder.DecodeKey(ExtractKey(packedKey));
		}

		#region Dump()

		/// <summary>Return a user-friendly string representation of a key of this subspace</summary>
		[Pure]
		public override string PrettyPrint(Slice packedKey)
		{
			if (packedKey.IsNull) return "<null>";
			//TODO: defer to the encoding itself?
			var key = ExtractKey(packedKey, boundCheck: true);

			if (this.KeyEncoder.TryDecodeKey(key, out T1? value))
			{
				return STuple.Formatter.Stringify(value);
			}

			// decoding failed, or some other non-trivial error
			return key.PrettyPrint();
		}

		#endregion

	}

	/// <summary>Encodes and Decodes keys composed of a single element</summary>
	public static partial class TypedKeysExtensions
	{

		#region <T1>

		#region Ranges...

		/// <summary>Return the range of all legal keys in this subspace, that start with the specified value</summary>
		/// <returns>Range that encompass all keys that start with (tuple.Item1, ...)</returns>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static KeyRange PackRange<T1>(this ITypedKeySubspace<T1> self, STuple<T1> tuple)
		{
			return KeyRange.PrefixedBy(self.Encode(tuple.Item1));
		}

		/// <summary>Return the range of all legal keys in this subspace, that start with the specified value</summary>
		/// <returns>Range that encompass all keys that start with (tuple.Item1, ...)</returns>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static KeyRange PackRange<T1>(this ITypedKeySubspace<T1> self, ValueTuple<T1> tuple)
		{
			return KeyRange.PrefixedBy(self.Encode(tuple.Item1));
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static KeyRange EncodeRange<T1>(this ITypedKeySubspace<T1> self, T1? item1)
		{
			//TODO: add concept of "range" on IKeyEncoder ?
			return KeyRange.PrefixedBy(self.Encode(item1));
		}

		#endregion

		#region Pack()

		/// <summary>Pack a 1-tuple into a key</summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Slice Pack<T1>(this ITypedKeySubspace<T1> self, ValueTuple<T1> tuple)
		{
			return self.Encode(tuple.Item1);
		}

		/// <summary>Pack a 1-tuple into a key</summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Slice Pack<T1, TTuple>(this ITypedKeySubspace<T1> self, TTuple tuple)
			where TTuple : IVarTuple
		{
			return self.Encode(tuple.OfSize(1).Get<T1>(0));
		}

		#endregion

		#region Encode()

		/// <summary>Encodes an array of items into an array of keys</summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Slice[] Encode<T1>(this ITypedKeySubspace<T1> self, params T1[] items)
		{
			return self.KeyEncoder.EncodeKeys(self.GetPrefix(), items);
		}

		/// <summary>Encodes a span of items into an array of keys</summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Slice[] Encode<T1>(this ITypedKeySubspace<T1> self, params ReadOnlySpan<T1> items)
		{
			return self.KeyEncoder.EncodeKeys(self.GetPrefix(), items);
		}

		/// <summary>Encodes a sequence of items into a sequence of keys</summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static IEnumerable<Slice> Encode<T1>(this ITypedKeySubspace<T1> self, IEnumerable<T1> items)
		{
			return self.KeyEncoder.EncodeKeys(self.GetPrefix(), items);
		}

		#endregion

		#region Decode()

		/// <summary>Decode a key from this subspace back into a value</summary>
		public static void Decode<T1>(this ITypedKeySubspace<T1> self, Slice packedKey, out T1? item1)
		{
			item1 = self.Decode(packedKey)!;
		}

		#endregion

		#endregion
	}

}
