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
	using System;

	public static partial class FdbKey
	{

		/// <summary>Maximum allowed size for a key in the database (10,000 bytes)</summary>
		public const int MaxSize = Fdb.MaxKeySize;

		/// <summary>Represents the <c>null</c> key, or the absence of key</summary>
		public static readonly FdbRawKey Nil;

		#region Generic...

		/// <summary>Returns a key that wraps a value that will be encoded into bytes using a given encoder</summary>
		/// <typeparam name="TKey">Type of the key, that implements <see cref="ISpanEncodable"/></typeparam>
		/// <param name="subspace">Subspace that contains the key in the database</param>
		/// <param name="key">Value of the key</param>
		public static FdbKey<TKey> Create<TKey>(IKeySubspace subspace, TKey key)
			where TKey : struct, ISpanEncodable
		{
			return new(subspace, key);
		}

		/// <summary>Returns a key that wraps a value that will be encoded into bytes using a given encoder</summary>
		/// <typeparam name="TKey">Type of the key that is encoded</typeparam>
		/// <typeparam name="TEncoder">Type of the encoder for this key, that implements <see cref="ISpanEncoder{TKey}"/></typeparam>
		/// <param name="subspace">Subspace that contains the key in the database</param>
		/// <param name="key">Value of the key</param>
		public static FdbKey<TKey, TEncoder> Create<TKey, TEncoder>(IKeySubspace subspace, TKey key)
			where TEncoder : struct, ISpanEncoder<TKey>
		{
			return new(subspace, key);
		}

		#endregion

		#region Binary

		#region No Subspace...

		/// <summary>Returns a key that wraps a pre-encoded <see cref="Slice"/></summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static FdbRawKey ToBytes(Slice key) => new(key);

		/// <summary>Returns a key that wraps a pre-encoded byte array</summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static FdbRawKey ToBytes(byte[] key) => new(key.AsSlice());

		/// <summary>Returns a key that wraps a pre-encoded byte array</summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static FdbRawKey ToBytes(byte[] key, int start, int length) => new(key.AsSlice(start, length));

		#endregion

		#region With Subspace...

		/// <summary>Returns a key that wraps a pre-encoded binary suffix inside a <see cref="IBinaryKeySubspace"/></summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static FdbSuffixKey ToBytes(IKeySubspace subspace, Slice relativeKey)
			=> new(subspace, relativeKey);

		/// <summary>Returns a key that wraps a pre-encoded binary suffix inside a <see cref="IBinaryKeySubspace"/></summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static FdbSuffixKey ToBytes(IKeySubspace subspace, byte[] relativeKey)
			=> new(subspace, relativeKey.AsSlice());

		/// <summary>Returns a key that wraps a pre-encoded binary suffix inside a <see cref="IBinaryKeySubspace"/></summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static FdbSuffixKey ToBytes(IKeySubspace subspace, byte[] relativeKey, int start, int length)
			=> new(subspace, relativeKey.AsSlice(start, length));

		#endregion

		#endregion

		#region Tuples...

		#region ToTuple(IKeySubspace, ValueTuple<...>)...

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static FdbTupleKey<T1> ToTuple<T1>(IKeySubspace subspace, ValueTuple<T1> key) => new(subspace, key.Item1);

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static FdbTupleKey<T1, T2> ToTuple<T1, T2>(IKeySubspace subspace, in ValueTuple<T1, T2> key) => new(subspace, in key);

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static FdbTupleKey<T1, T2, T3> ToTuple<T1, T2, T3>(IKeySubspace subspace, in ValueTuple<T1, T2, T3> key) => new(subspace, in key);

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static FdbTupleKey<T1, T2, T3, T4> ToTuple<T1, T2, T3, T4>(IKeySubspace subspace, in ValueTuple<T1, T2, T3, T4> key) => new(subspace, in key);

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static FdbTupleKey<T1, T2, T3, T4, T5> ToTuple<T1, T2, T3, T4, T5>(IKeySubspace subspace, in ValueTuple<T1, T2, T3, T4, T5> key) => new(subspace, in key);

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static FdbTupleKey<T1, T2, T3, T4, T5, T6> ToTuple<T1, T2, T3, T4, T5, T6>(IKeySubspace subspace, in ValueTuple<T1, T2, T3, T4, T5, T6> key) => new(subspace, in key);

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static FdbTupleKey<T1, T2, T3, T4, T5, T6, T7> ToTuple<T1, T2, T3, T4, T5, T6, T7>(IKeySubspace subspace, in ValueTuple<T1, T2, T3, T4, T5, T6, T7> key) => new(subspace, in key);

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static FdbTupleKey<T1, T2, T3, T4, T5, T6, T7, T8> ToTuple<T1, T2, T3, T4, T5, T6, T7, T8>(IKeySubspace subspace, in ValueTuple<T1, T2, T3, T4, T5, T6, T7, ValueTuple<T8>> key) => new(subspace, in key);

		#endregion

		#region ToTuple(IKeySubspace, STuple<...>)...

		/// <summary>Returns a key that packs the given items inside a subspace</summary>
		/// <param name="subspace">Subspace that contains the key</param>
		/// <param name="items">Elements of the key</param>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static FdbVarTupleKey ToTuple(IKeySubspace subspace, IVarTuple items) => new(subspace, items);

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static FdbTupleKey<T1> ToTuple<T1>(IKeySubspace subspace, STuple<T1> key) => new(subspace, key.Item1);

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static FdbTupleKey<T1, T2> ToTuple<T1, T2>(IKeySubspace subspace, in STuple<T1, T2> key) => new(subspace, in key);

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static FdbTupleKey<T1, T2, T3> ToTuple<T1, T2, T3>(IKeySubspace subspace, in STuple<T1, T2, T3> key) => new(subspace, in key);

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static FdbTupleKey<T1, T2, T3, T4> ToTuple<T1, T2, T3, T4>(IKeySubspace subspace, in STuple<T1, T2, T3, T4> key) => new(subspace, in key);

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static FdbTupleKey<T1, T2, T3, T4, T5> ToTuple<T1, T2, T3, T4, T5>(IKeySubspace subspace, in STuple<T1, T2, T3, T4, T5> key) => new(subspace, in key);

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static FdbTupleKey<T1, T2, T3, T4, T5, T6> ToTuple<T1, T2, T3, T4, T5, T6>(IKeySubspace subspace, in STuple<T1, T2, T3, T4, T5, T6> key) => new(subspace, in key);

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static FdbTupleKey<T1, T2, T3, T4, T5, T6, T7> ToTuple<T1, T2, T3, T4, T5, T6, T7>(IKeySubspace subspace, in STuple<T1, T2, T3, T4, T5, T6, T7> key) => new(subspace, in key);

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static FdbTupleKey<T1, T2, T3, T4, T5, T6, T7, T8> ToTuple<T1, T2, T3, T4, T5, T6, T7, T8>(IKeySubspace subspace, in STuple<T1, T2, T3, T4, T5, T6, T7, T8> key) => new(subspace, in key);

		#endregion

		#endregion

	}

	/// <summary>Represents a key in the database, that can be serialized into bytes</summary>
	/// <remarks>
	/// <para>Types that implement this interface usually wrap a parent subspace, as well as the elements that make up a key inside this subspace.</para>
	/// <para>For example, a <see cref="FdbTupleKey{string,int}"/> will wrap the items <c>("hello", 123)</c>, which will be later converted into bytes using the Tuple Encoding</para>
	/// <para>Keys that are reused multiple times can be converted into a <see cref="FdbRawKey"/> using the <see cref="FdbKeyExtensions.Memoize{TKey}"/> method, which wraps the complete key in a <see cref="Slice"/>.</para>
	/// </remarks>
	public interface IFdbKey : ISpanEncodable, ISpanFormattable
	{

		/// <summary>Optional subspace that contains this key</summary>
		/// <remarks>If <c>null</c>, this is most probably a key that as already been encoded.</remarks>
		[Pure]
		IKeySubspace? GetSubspace();

	}

	#region Binary Keys...

	/// <summary>Wraps a <see cref="Slice"/> that contains a pre-encoded key in the database</summary>
	public readonly struct FdbRawKey : IFdbKey
		, IEquatable<FdbRawKey>, IEquatable<Slice>
#if NET9_0_OR_GREATER
		, IEquatable<ReadOnlySpan<byte>>
#endif
	{

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static FdbRawKey Return(Slice slice) => new(slice);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		[SkipLocalsInit]
		internal FdbRawKey(Slice data)
		{
			this.Data = data;
		}

		public readonly Slice Data;

		/// <summary>Returns <see langword="true"/> if the key is null</summary>
		public bool IsNull
		{
			[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
			get => this.Data.IsNull;
		}

		/// <inheritdoc />
		IKeySubspace? IFdbKey.GetSubspace() => null;

		#region Equals(...)

		/// <inheritdoc />
		public override bool Equals([NotNullWhen(true)] object? other)
		{
			return other switch
			{
				FdbRawKey key => this.Equals(key.Data),
				FdbVarTupleKey key => key.Equals(this),
				Slice bytes => this.Equals(bytes),
				_ => false,
			};
		}

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public override int GetHashCode() => this.Data.GetHashCode();

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool Equals(FdbRawKey other) => FdbKeyExtensions.Equals(in this, other.Data);

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool Equals(Slice other) => FdbKeyExtensions.Equals(in this, other);

		/// <inheritdoc cref="Equals(Slice)"/>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool Equals(ReadOnlySpan<byte> other) => FdbKeyExtensions.Equals(in this, other);

		#endregion

		/// <inheritdoc />
		public override string ToString() => this.Data.ToString();

		/// <inheritdoc />
		public string ToString(string? format, IFormatProvider? formatProvider)
			=> this.Data.ToString(format, formatProvider);

		/// <inheritdoc />
		public bool TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format, IFormatProvider? provider)
			=> this.Data.TryFormat(destination, out charsWritten, format, provider);

		/// <inheritdoc />
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool TryGetSpan(out ReadOnlySpan<byte> span)
		{
			span = this.Data.Span;
			return true;
		}

		/// <inheritdoc />
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool TryGetSizeHint(out int sizeHint)
		{
			sizeHint = this.Data.Count;
			return true;
		}

		/// <inheritdoc />
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool TryEncode(Span<byte> destination, out int bytesWritten)
		{
			return this.Data.TryCopyTo(destination, out bytesWritten);
		}

		/// <summary>Converts a slice into a <see cref="FdbRawKey"/></summary>
		public static implicit operator FdbRawKey(Slice key) => new(key);

	}

	/// <summary>Wraps a <see cref="Slice"/> that wraps a pre-encoded binary suffix, relative to a subspace</summary>
	public readonly struct FdbSuffixKey : IFdbKey
		, IEquatable<FdbSuffixKey>, IEquatable<Slice>, IEquatable<FdbRawKey>
#if NET9_0_OR_GREATER
		, IEquatable<ReadOnlySpan<byte>>
#endif
	{

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		[SkipLocalsInit]
		internal FdbSuffixKey(IKeySubspace subspace, Slice suffix)
		{
			this.Suffix = suffix;
			this.Subspace = subspace;
		}

		public readonly IKeySubspace Subspace;

		public readonly Slice Suffix;

		/// <inheritdoc />
		IKeySubspace? IFdbKey.GetSubspace() => this.Subspace;

		#region Equals(...)

		/// <inheritdoc />
		public override bool Equals([NotNullWhen(true)] object? other)
		{
			return other switch
			{
				FdbSuffixKey key => this.Equals(key),
				FdbRawKey key => this.Equals(key),
				Slice bytes => this.Equals(bytes),
				_ => false,
			};
		}

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public override int GetHashCode() => this.Suffix.GetHashCode();

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool Equals(FdbSuffixKey other) => Equals(this.Subspace, other.Subspace) && this.Suffix.Equals(other.Suffix);

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool Equals(FdbRawKey other) => FdbKeyExtensions.Equals(in this, other.Data);

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool Equals(Slice other) => FdbKeyExtensions.Equals(in this, other);

		/// <inheritdoc cref="Equals(Slice)"/>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool Equals(ReadOnlySpan<byte> other) => FdbKeyExtensions.Equals(in this, other);

		#endregion

		/// <inheritdoc />
		public override string ToString() => ToString(null);

		/// <inheritdoc />
		public string ToString(string? format, IFormatProvider? formatProvider = null)
			=> string.Create(formatProvider, $"[{this.Subspace}] {this.Suffix}");

		/// <inheritdoc />
		public bool TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format, IFormatProvider? provider)
			=> destination.TryWrite(provider, $"[{this.Subspace}] {this.Suffix}", out charsWritten);

		/// <inheritdoc />
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool TryGetSpan(out ReadOnlySpan<byte> span)
		{
			span = default;
			return false;
		}

		/// <inheritdoc />
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool TryGetSizeHint(out int sizeHint)
		{
			sizeHint = this.Subspace.GetPrefix().Count + this.Suffix.Count;
			return true;
		}

		/// <inheritdoc />
		public bool TryEncode(Span<byte> destination, out int bytesWritten)
		{
			if (this.Subspace.GetPrefix().TryCopyTo(destination, out var prefixLen)
			 && this.Suffix.TryCopyTo(destination[prefixLen..], out var dataLen))
			{
				bytesWritten = prefixLen + dataLen;
				return true;
			}

			bytesWritten = 0;
			return false;
		}

	}

	#endregion

	#region Encoded Keys...

	/// <summary>Wraps a value that will be encoded to get the corresponding key, relative to a subspace</summary>
	/// <typeparam name="TKey">Type of the key</typeparam>
	[DebuggerDisplay("Data={Data}")]
	public readonly struct FdbKey<TKey>: IFdbKey
		where TKey : struct, ISpanEncodable
	{

		public FdbKey(IKeySubspace subspace, TKey data)
		{
			this.Data = data;
			this.Subspace = subspace;
		}

		/// <summary>Content of the key</summary>
		public readonly TKey Data;

		/// <summary>Optional subspace that contains this key</summary>
		public readonly IKeySubspace Subspace;

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public IKeySubspace? GetSubspace() => this.Subspace;

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool TryGetSpan(out ReadOnlySpan<byte> span)
		{
			//note: we could if the parent subspace did not have any prefix, which is not allowed
			span = default;
			return false;
		}

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool TryGetSizeHint(out int sizeHint)
		{
			if (!this.Data.TryGetSizeHint(out var dataSize))
			{
				sizeHint = 0;
				return false;
			}

			sizeHint = checked(dataSize + this.Subspace.GetPrefix().Count);
			return true;
		}

		/// <inheritdoc />
		[MustUseReturnValue, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool TryEncode(Span<byte> destination, out int bytesWritten)
		{
			{
				if (this.Subspace.GetPrefix().TryCopyTo(destination, out var prefixLen)
				 && this.Data.TryEncode(destination, out var dataLen))
				{
					bytesWritten = prefixLen + dataLen;
					return true;
				}

				bytesWritten = 0;
				return false;
			}
		}

		/// <inheritdoc />
		public override string ToString() => ToString(null);

		/// <inheritdoc />
		public string ToString(string? format, IFormatProvider? provider = null)
		{
			return string.Create(CultureInfo.InvariantCulture, $"{this.Subspace}:{this.Data}");
		}

		/// <inheritdoc />
		public bool TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format, IFormatProvider? provider)
		{
			return destination.TryWrite(CultureInfo.InvariantCulture, $"{this.Subspace}:{this.Data}", out charsWritten);
		}

	}

	/// <summary>Wraps a value that will be encoded to get the corresponding key, relative to a subspace</summary>
	/// <typeparam name="TKey">Type of the key</typeparam>
	/// <typeparam name="TEncoder">Type of the <see cref="ISpanEncoder{TValue}"/> that can convert this key into a binary representation</typeparam>
	[DebuggerDisplay("Data={Data}")]
	public readonly struct FdbKey<TKey, TEncoder>: IFdbKey
		where TEncoder: struct, ISpanEncoder<TKey>
	{

		public FdbKey(IKeySubspace subspace, TKey data)
		{
			this.Data = data;
			this.Subspace = subspace;
		}

		/// <summary>Content of the key</summary>
		public readonly TKey Data;

		/// <summary>Optional subspace that contains this key</summary>
		public readonly IKeySubspace Subspace;

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public IKeySubspace? GetSubspace() => this.Subspace;

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool TryGetSpan(out ReadOnlySpan<byte> span)
		{
			//note: we could if the parent subspace did not have any prefix, which is not allowed
			span = default;
			return false;
		}

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool TryGetSizeHint(out int sizeHint)
		{
			if (!TEncoder.TryGetSizeHint(this.Data, out var dataSize))
			{
				sizeHint = 0;
				return false;
			}

			sizeHint = checked(dataSize + this.Subspace.GetPrefix().Count);
			return true;
		}

		/// <inheritdoc />
		[MustUseReturnValue, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool TryEncode(Span<byte> destination, out int bytesWritten)
		{
			{
				if (this.Subspace.GetPrefix().TryCopyTo(destination, out var prefixLen)
				 && TEncoder.TryEncode(destination, out var dataLen, in this.Data))
				{
					bytesWritten = prefixLen + dataLen;
					return true;
				}

				bytesWritten = 0;
				return false;
			}
		}

		/// <inheritdoc />
		public override string ToString() => ToString(null);

		/// <inheritdoc />
		public string ToString(string? format, IFormatProvider? provider = null)
		{
			return string.Create(CultureInfo.InvariantCulture, $"{this.Subspace}:{this.Data}");
		}

		/// <inheritdoc />
		public bool TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format, IFormatProvider? provider)
		{
			return destination.TryWrite(CultureInfo.InvariantCulture, $"{this.Subspace}:{this.Data}", out charsWritten);
		}

	}

	public readonly struct FdbSuccessorKey<TKey> : IFdbKey
		where TKey : struct, IFdbKey
	{

		public FdbSuccessorKey(in TKey parent)
		{
			this.Parent = parent;
		}

		public readonly TKey Parent;
		
		/// <inheritdoc />
		public override string ToString() => ToString(null);

		/// <inheritdoc />
		public string ToString(string? format, IFormatProvider? formatProvider = null)
			=> $"SuccessorOf({this.Parent.ToString(format, formatProvider)})";

		/// <inheritdoc />
		public bool TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format, IFormatProvider? provider)
			=> destination.TryWrite(provider, $"SuccessorOf({this.Parent})", out charsWritten);

		/// <inheritdoc />
		IKeySubspace? IFdbKey.GetSubspace() => this.Parent.GetSubspace();

		/// <inheritdoc />
		public bool TryGetSpan(out ReadOnlySpan<byte> span)
		{
			// we cannot modify the original span to append 0x00, so we don't support this operation
			span = default;
			return false;
		}

		/// <inheritdoc />
		public bool TryGetSizeHint(out int sizeHint)
		{
			if (!this.Parent.TryGetSizeHint(out var size))
			{
				sizeHint = 0;
				return true;
			}

			// we always add 1 to the original size
			sizeHint = checked(size + 1);
			return true;
		}

		/// <inheritdoc />
		public bool TryEncode(scoped Span<byte> destination, out int bytesWritten)
		{
			// first generate the wrapped key
			if (!this.Parent.TryEncode(destination, out var len)
			 || destination.Length <= len)
			{
				bytesWritten = 0;
				return false;
			}

			// appends the 0x00 at the end
			destination[len] = 0;
			bytesWritten = checked(len + 1);
			return true;
		}

	}

	public readonly struct FdbNextKey<TKey> : IFdbKey
		where TKey : struct, IFdbKey
	{

		public FdbNextKey(in TKey parent)
		{
			this.Parent = parent;
		}

		public readonly TKey Parent;
		
		/// <inheritdoc />
		public override string ToString() => ToString(null);

		/// <inheritdoc />
		public string ToString(string? format, IFormatProvider? formatProvider = null)
			=> $"Increment({this.Parent.ToString(format, formatProvider)})";

		/// <inheritdoc />
		public bool TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format, IFormatProvider? provider)
			=> destination.TryWrite(provider, $"Increment({this.Parent})", out charsWritten);

		/// <inheritdoc />
		IKeySubspace? IFdbKey.GetSubspace() => this.Parent.GetSubspace();

		/// <inheritdoc />
		public bool TryGetSpan(out ReadOnlySpan<byte> span)
		{
			// we cannot modify the original span, so we don't support this operation
			span = default;
			return false;
		}

		/// <inheritdoc />
		public bool TryGetSizeHint(out int sizeHint)
		{
			if (!this.Parent.TryGetSizeHint(out var size))
			{
				sizeHint = 0;
				return true;
			}

			// incrementing the key can only produce a key of the same size or smaller, except when the key is empty in which case the successor has size 1
			sizeHint = Math.Max(size, 1);
			return true;
		}

		/// <inheritdoc />
		public bool TryEncode(scoped Span<byte> destination, out int bytesWritten)
		{
			// first generate the wrapped key
			if (!this.Parent.TryEncode(destination, out var len))
			{
				goto too_small;
			}

			// if the key is empty (??) we need to return 'x00'
			if (len == 0)
			{
				goto empty_key;
			}

			// increment the buffer in-place
			// => throws if the key is all FF
			FdbKey.Increment(destination[..len], out len);
			bytesWritten = len;
			return true;

		empty_key:
			if (destination.Length == 0)
			{
				goto too_small;
			}
			destination[0] = 0;
			bytesWritten = 1;
			return true;

		too_small:
			bytesWritten = 0;
			return false;

		}

	}

	#endregion

	#region Tuple Keys...

	[DebuggerDisplay("{ToString(),nq}")]
	public readonly struct FdbVarTupleKey : IFdbKey
		, IEquatable<FdbVarTupleKey>, IEquatable<IVarTuple>, IEquatable<Slice>, IEquatable<FdbRawKey>
#if NET9_0_OR_GREATER
		, IEquatable<ReadOnlySpan<byte>>
#endif
	{

		[SkipLocalsInit]
		public FdbVarTupleKey(IKeySubspace? subspace, IVarTuple items)
		{
			this.Subspace = subspace;
			this.Items = items;
		}

		public readonly IKeySubspace? Subspace;

		public readonly IVarTuple Items;

		#region Equals(...)

		/// <inheritdoc />
		public override bool Equals([NotNullWhen(true)] object? other)
		{
			return other switch
			{
				FdbVarTupleKey key => this.Equals(key),
				FdbRawKey key => this.Equals(key.Data),
				IVarTuple tuple => this.Equals(tuple),
				Slice bytes => this.Equals(bytes),
				_ => false,
			};
		}

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public override int GetHashCode() => this.Items.GetHashCode();

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool Equals(FdbVarTupleKey other) => Equals(this.Subspace, other.Subspace) && this.Items.Equals(other.Items);

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool Equals(IVarTuple? other) => this.Items.Equals(other);

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool Equals(FdbRawKey other) => FdbKeyExtensions.Equals(in this, other.Data);

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool Equals(Slice other) => FdbKeyExtensions.Equals(in this, other);

		/// <inheritdoc cref="Equals(Slice)"/>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool Equals(ReadOnlySpan<byte> other) => FdbKeyExtensions.Equals(in this, other);

		#endregion

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public FdbVarTupleKey Append<T1>(T1 item1) => new(this.Subspace, this.Items.Append(item1));

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public FdbVarTupleKey Append<T1, T2>(T1 item1, T2 item2) => new(this.Subspace, this.Items.Append(item1, item2));

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public FdbVarTupleKey Append<T1, T2, T3>(T1 item1, T2 item2, T3 item3) => new(this.Subspace, this.Items.Append(item1, item2, item3));

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public FdbVarTupleKey Append<T1, T2, T3, T4>(T1 item1, T2 item2, T3 item3, T4 item4) => new(this.Subspace, this.Items.Append(item1, item2, item3, item4));

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public FdbVarTupleKey Append<T1, T2, T3, T4, T5>(T1 item1, T2 item2, T3 item3, T4 item4, T5 item5) => new(this.Subspace, this.Items.Append(item1, item2, item3, item4, item5));

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public FdbVarTupleKey Append<T1, T2, T3, T4, T5, T6>(T1 item1, T2 item2, T3 item3, T4 item4, T5 item5, T6 item6) => new(this.Subspace, this.Items.Append(item1, item2, item3, item4, item5, item6));

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public FdbVarTupleKey Append<T1, T2, T3, T4, T5, T6, T7>(T1 item1, T2 item2, T3 item3, T4 item4, T5 item5, T6 item6, T7 item7) => new(this.Subspace, this.Items.Append(item1, item2, item3, item4, item5, item6, item7));

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public FdbVarTupleKey Append<T1, T2, T3, T4, T5, T6, T7, T8>(T1 item1, T2 item2, T3 item3, T4 item4, T5 item5, T6 item6, T7 item7, T8 item8) => new(this.Subspace, this.Items.Append(item1, item2, item3, item4, item5, item6, item7, item8));

		/// <inheritdoc />
		public string ToString(string? format, IFormatProvider? formatProvider = null) => this.Items.ToString()!;

		/// <inheritdoc />
		public bool TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format, IFormatProvider? provider) => destination.TryWrite($"{this.Items}", out charsWritten);

		/// <inheritdoc />
		public override string ToString() => ToString(null);

		/// <inheritdoc />
		IKeySubspace? IFdbKey.GetSubspace() => this.Subspace;

		/// <inheritdoc />
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool TryGetSpan(out ReadOnlySpan<byte> span) { span = default; return false; }

		/// <inheritdoc />
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool TryGetSizeHint(out int sizeHint) { sizeHint = 0; return false; }

		/// <inheritdoc />
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool TryEncode(Span<byte> destination, out int bytesWritten) => this.Subspace is not null
			? TupleEncoder.TryPackTo(destination, out bytesWritten, this.Subspace.GetPrefix().Span, this.Items)
			: TupleEncoder.TryPackTo(destination, out bytesWritten, this.Items);

	}

	[DebuggerDisplay("{ToString(),nq}")]
	public readonly struct FdbTupleKey<T1> : IFdbKey
		, IEquatable<FdbTupleKey<T1>>, IEquatable<STuple<T1>>, IEquatable<IVarTuple>, IEquatable<Slice>, IEquatable<FdbRawKey>
#if NET9_0_OR_GREATER
		, IEquatable<ReadOnlySpan<byte>>
#endif
	{

		[SkipLocalsInit]
		public FdbTupleKey(IKeySubspace subspace, T1 item1)
		{
			this.Subspace = subspace;
			this.Item1 = item1;
		}

		[SkipLocalsInit]
		public FdbTupleKey(IKeySubspace subspace, in STuple<T1> items)
		{
			this.Subspace = subspace;
			this.Item1 = items.Item1;
		}

		[SkipLocalsInit]
		public FdbTupleKey(IKeySubspace subspace, in ValueTuple<T1> items)
		{
			this.Subspace = subspace;
			this.Item1 = items.Item1;
		}

		public readonly IKeySubspace Subspace;

		public readonly T1 Item1;

		#region Equals(...)

		/// <inheritdoc />
		public override bool Equals([NotNullWhen(true)] object? other)
		{
			return other switch
			{
				FdbTupleKey<T1> key => this.Equals(key),
				FdbRawKey key => this.Equals(key.Data),
				IVarTuple tuple => this.Equals(tuple),
				Slice bytes => this.Equals(bytes),
				_ => false,
			};
		}

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public override int GetHashCode() => STuple.Create(this.Item1).GetHashCode();

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool Equals(FdbTupleKey<T1> other) => Equals(this.Subspace, other.Subspace) && EqualityComparer<T1>.Default.Equals(this.Item1, other.Item1);

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool Equals(STuple<T1> other) => STuple.Create(this.Item1).Equals(other);

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool Equals(IVarTuple? other) => STuple.Create(this.Item1).Equals(other);

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool Equals(FdbRawKey other) => FdbKeyExtensions.Equals(in this, other.Data);

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool Equals(Slice other) => FdbKeyExtensions.Equals(in this, other);

		/// <inheritdoc cref="Equals(Slice)"/>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool Equals(ReadOnlySpan<byte> other) => FdbKeyExtensions.Equals(in this, other);

		#endregion

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public FdbTupleKey<T1, T2> Append<T2>(T2 item2) => new(Subspace, this.Item1, item2);

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public FdbTupleKey<T1, T2, T3> Append<T2, T3>(T2 item2, T3 item3) => new(this.Subspace, STuple.Create(this.Item1, item2, item3));

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public FdbTupleKey<T1, T2, T3, T4, T5> Append<T2, T3, T4, T5>(T2 item2, T3 item3, T4 item4, T5 item5) => new(this.Subspace, STuple.Create(this.Item1, item2, item3, item4, item5));

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public FdbTupleKey<T1, T2, T3, T4, T5, T6> Append<T2, T3, T4, T5, T6>(T2 item2, T3 item3, T4 item4, T5 item5, T6 item6) => new(this.Subspace, STuple.Create(this.Item1, item2, item3, item4, item5, item6));

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public FdbTupleKey<T1, T2, T3, T4, T5, T6, T7> Append<T2, T3, T4, T5, T6, T7>(T2 item2, T3 item3, T4 item4, T5 item5, T6 item6, T7 item7) => new(this.Subspace, STuple.Create(this.Item1, item2, item3, item4, item5, item6, item7));

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public FdbTupleKey<T1, T2, T3, T4, T5, T6, T7, T8> Append<T2, T3, T4, T5, T6, T7, T8>(T2 item2, T3 item3, T4 item4, T5 item5, T6 item6, T7 item7, T8 item8) => new(this.Subspace, STuple.Create(this.Item1, item2, item3, item4, item5, item6, item7, item8));

		/// <inheritdoc />
		public string ToString(string? format, IFormatProvider? formatProvider = null) => $"[{this.Subspace}] {STuple.Create(this.Item1)}";

		/// <inheritdoc />
		public bool TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format, IFormatProvider? provider) => destination.TryWrite($"[{this.Subspace}] {STuple.Create(this.Item1)}", out charsWritten);

		/// <inheritdoc />
		public override string ToString() => ToString(null);

		/// <inheritdoc />
		IKeySubspace? IFdbKey.GetSubspace() => this.Subspace;

		/// <inheritdoc />
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool TryGetSpan(out ReadOnlySpan<byte> span) { span = default; return false; }

		/// <inheritdoc />
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool TryGetSizeHint(out int sizeHint)
		{
			if (!TupleEncoder.TryGetSizeHint<T1>(this.Item1, out var size))
			{
				sizeHint = 0;
				return false;
			}

			sizeHint = checked(this.Subspace.GetPrefix().Count + size);
			return true;
		}

		/// <inheritdoc />
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool TryEncode(Span<byte> destination, out int bytesWritten) => TupleEncoder.TryEncodeKey(destination, out bytesWritten, this.Subspace.GetPrefix().Span, this.Item1);

	}

	[DebuggerDisplay("{ToString(),nq}")]
	public readonly struct FdbTupleKey<T1, T2> : IFdbKey
		, IEquatable<FdbTupleKey<T1, T2>>, IEquatable<STuple<T1, T2>>, IEquatable<IVarTuple>, IEquatable<Slice>, IEquatable<FdbRawKey>
	#if NET9_0_OR_GREATER
		, IEquatable<ReadOnlySpan<byte>>
	#endif
	{

		[SkipLocalsInit, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public FdbTupleKey(IKeySubspace subspace, in STuple<T1, T2> items)
		{
			this.Subspace = subspace;
			this.Items = items;
		}

		[SkipLocalsInit, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public FdbTupleKey(IKeySubspace subspace, in ValueTuple<T1, T2> items)
		{
			this.Subspace = subspace;
			this.Items = items.ToSTuple();
		}

		[SkipLocalsInit, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public FdbTupleKey(IKeySubspace subspace, T1 item1, T2 item2)
		{
			this.Subspace = subspace;
			this.Items = STuple.Create(item1, item2);
		}

		public readonly IKeySubspace Subspace;

		public readonly STuple<T1, T2> Items;

		#region Equals(...)

		/// <inheritdoc />
		public override bool Equals([NotNullWhen(true)] object? other)
		{
			return other switch
			{
				FdbTupleKey<T1, T2> tuple => this.Equals(tuple),
				FdbRawKey key => this.Equals(key.Data),
				IVarTuple tuple => this.Equals(tuple),
				Slice bytes => this.Equals(bytes),
				_ => false,
			};
		}

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public override int GetHashCode() => this.Items.GetHashCode();

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool Equals(FdbTupleKey<T1, T2> other) => Equals(this.Subspace, other.Subspace) && this.Items.Equals(other.Items);

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool Equals(STuple<T1, T2> other) => this.Items.Equals(other);

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool Equals(IVarTuple? other) => this.Items.Equals(other);

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool Equals(FdbRawKey other) => FdbKeyExtensions.Equals(in this, other.Data);

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool Equals(Slice other) => FdbKeyExtensions.Equals(in this, other);

		/// <inheritdoc cref="Equals(Slice)"/>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool Equals(ReadOnlySpan<byte> other) => FdbKeyExtensions.Equals(in this, other);

		#endregion

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public FdbTupleKey<T1, T2, T3> Append<T3>(T3 item3) => new(this.Subspace, STuple.Create(this.Items.Item1, this.Items.Item2, item3));

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public FdbTupleKey<T1, T2, T3, T4, T5> Append<T3, T4, T5>(T3 item3, T4 item4, T5 item5) => new(this.Subspace, STuple.Create(this.Items.Item1, this.Items.Item2, item3, item4, item5));

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public FdbTupleKey<T1, T2, T3, T4, T5, T6> Append<T3, T4, T5, T6>(T3 item3, T4 item4, T5 item5, T6 item6) => new(this.Subspace, STuple.Create(this.Items.Item1, this.Items.Item2, item3, item4, item5, item6));

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public FdbTupleKey<T1, T2, T3, T4, T5, T6, T7> Append<T3, T4, T5, T6, T7>(T3 item3, T4 item4, T5 item5, T6 item6, T7 item7) => new(this.Subspace, STuple.Create(this.Items.Item1, this.Items.Item2, item3, item4, item5, item6, item7));

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public FdbTupleKey<T1, T2, T3, T4, T5, T6, T7, T8> Append<T3, T4, T5, T6, T7, T8>(T3 item3, T4 item4, T5 item5, T6 item6, T7 item7, T8 item8) => new(this.Subspace, STuple.Create(this.Items.Item1, this.Items.Item2, item3, item4, item5, item6, item7, item8));

		/// <inheritdoc />
		public string ToString(string? format, IFormatProvider? formatProvider) => $"[{this.Subspace}] {this.Items}";

		/// <inheritdoc />
		public bool TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format, IFormatProvider? provider) => destination.TryWrite($"[{this.Subspace}] {this.Items}", out charsWritten);

		/// <inheritdoc />
		public override string ToString() => ToString(null, null);

		/// <inheritdoc />
		IKeySubspace? IFdbKey.GetSubspace() => this.Subspace;

		/// <inheritdoc />
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool TryGetSpan(out ReadOnlySpan<byte> span) { span = default; return false; }

		/// <inheritdoc />
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool TryGetSizeHint(out int sizeHint)
		{
			if (!TupleEncoder.TryGetSizeHint(this.Items.Item1, out var size1)
			 || !TupleEncoder.TryGetSizeHint(this.Items.Item2, out var size2))
			{
				sizeHint = 0;
				return false;
			}

			sizeHint = checked(this.Subspace.GetPrefix().Count + size1 + size2);
			return true;
		}

		/// <inheritdoc />
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool TryEncode(Span<byte> destination, out int bytesWritten) => TupleEncoder.TryPackTo(destination, out bytesWritten, this.Subspace.GetPrefix().Span, in this.Items);

	}

	[DebuggerDisplay("{ToString(),nq}")]
	public readonly struct FdbTupleKey<T1, T2, T3> : IFdbKey
		, IEquatable<FdbTupleKey<T1, T2, T3>>, IEquatable<STuple<T1, T2, T3>>, IEquatable<IVarTuple>, IEquatable<Slice>, IEquatable<FdbRawKey>
#if NET9_0_OR_GREATER
		, IEquatable<ReadOnlySpan<byte>>
#endif
	{

		[SkipLocalsInit, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public FdbTupleKey(IKeySubspace subspace, in STuple<T1, T2, T3> items)
		{
			this.Subspace = subspace;
			this.Items = items;
		}

		[SkipLocalsInit, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public FdbTupleKey(IKeySubspace subspace, in ValueTuple<T1, T2, T3> items)
		{
			this.Subspace = subspace;
			this.Items = items.ToSTuple();
		}

		[SkipLocalsInit, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public FdbTupleKey(IKeySubspace subspace, T1 item1, T2 item2, T3 item3)
		{
			this.Subspace = subspace;
			this.Items = STuple.Create(item1, item2, item3);
		}

		public readonly IKeySubspace Subspace;

		public readonly STuple<T1, T2, T3> Items;

		#region Equals(...)

		/// <inheritdoc />
		public override bool Equals([NotNullWhen(true)] object? other)
		{
			return other switch
			{
				FdbTupleKey<T1, T2, T3> key => this.Equals(key),
				FdbRawKey key => this.Equals(key.Data),
				IVarTuple tuple => this.Equals(tuple),
				Slice bytes => this.Equals(bytes),
				_ => false,
			};
		}

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public override int GetHashCode() => this.Items.GetHashCode();

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool Equals(FdbTupleKey<T1, T2, T3> other) => Equals(this.Subspace, other.Subspace) && this.Items.Equals(other.Items);

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool Equals(STuple<T1, T2, T3> other) => this.Items.Equals(other);

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool Equals(IVarTuple? other) => this.Items.Equals(other);

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool Equals(FdbRawKey other) => FdbKeyExtensions.Equals(in this, other.Data);

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool Equals(Slice other) => FdbKeyExtensions.Equals(in this, other);

		/// <inheritdoc cref="Equals(Slice)"/>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool Equals(ReadOnlySpan<byte> other) => FdbKeyExtensions.Equals(in this, other);

		#endregion

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public FdbTupleKey<T1, T2, T3, T4> Append<T4>(T4 item4) => new(this.Subspace, STuple.Create(this.Items.Item1, this.Items.Item2, this.Items.Item3, item4));

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public FdbTupleKey<T1, T2, T3, T4, T5> Append<T4, T5>(T4 item4, T5 item5) => new(this.Subspace, STuple.Create(this.Items.Item1, this.Items.Item2, this.Items.Item3, item4, item5));

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public FdbTupleKey<T1, T2, T3, T4, T5, T6> Append<T4, T5, T6>(T4 item4, T5 item5, T6 item6) => new(this.Subspace, STuple.Create(this.Items.Item1, this.Items.Item2, this.Items.Item3, item4, item5, item6));

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public FdbTupleKey<T1, T2, T3, T4, T5, T6, T7> Append<T4, T5, T6, T7>(T4 item4, T5 item5, T6 item6, T7 item7) => new(this.Subspace, STuple.Create(this.Items.Item1, this.Items.Item2, this.Items.Item3, item4, item5, item6, item7));

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public FdbTupleKey<T1, T2, T3, T4, T5, T6, T7, T8> Append<T4, T5, T6, T7, T8>(T4 item4, T5 item5, T6 item6, T7 item7, T8 item8) => new(this.Subspace, STuple.Create(this.Items.Item1, this.Items.Item2, this.Items.Item3, item4, item5, item6, item7, item8));

		/// <inheritdoc />
		public string ToString(string? format, IFormatProvider? formatProvider) => $"[{this.Subspace}] {this.Items}";

		/// <inheritdoc />
		public bool TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format, IFormatProvider? provider) => destination.TryWrite($"[{this.Subspace}] {this.Items}", out charsWritten);

		/// <inheritdoc />
		public override string ToString() => ToString(null, null);

		/// <inheritdoc />
		IKeySubspace? IFdbKey.GetSubspace() => this.Subspace;

		/// <inheritdoc />
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool TryGetSpan(out ReadOnlySpan<byte> span) { span = default; return false; }

		/// <inheritdoc />
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool TryGetSizeHint(out int sizeHint)
		{
			if (!TupleEncoder.TryGetSizeHint(this.Items.Item1, out var size1)
			 || !TupleEncoder.TryGetSizeHint(this.Items.Item2, out var size2)
			 || !TupleEncoder.TryGetSizeHint(this.Items.Item3, out var size3))
			{
				sizeHint = 0;
				return false;
			}

			sizeHint = checked(this.Subspace.GetPrefix().Count + size1 + size2 + size3);
			return true;
		}

		/// <inheritdoc />
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool TryEncode(Span<byte> destination, out int bytesWritten) => TupleEncoder.TryPackTo(destination, out bytesWritten, this.Subspace.GetPrefix().Span, in this.Items);

	}

	[DebuggerDisplay("{ToString(),nq}")]
	public readonly struct FdbTupleKey<T1, T2, T3, T4> : IFdbKey
		, IEquatable<FdbTupleKey<T1, T2, T3, T4>>, IEquatable<STuple<T1, T2, T3, T4>>, IEquatable<IVarTuple>, IEquatable<Slice>, IEquatable<FdbRawKey>
#if NET9_0_OR_GREATER
		, IEquatable<ReadOnlySpan<byte>>
#endif
	{

		[SkipLocalsInit, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public FdbTupleKey(IKeySubspace subspace, in STuple<T1, T2, T3, T4> items)
		{
			this.Subspace = subspace;
			this.Items = items;
		}

		[SkipLocalsInit, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public FdbTupleKey(IKeySubspace subspace, in ValueTuple<T1, T2, T3, T4> items)
		{
			this.Subspace = subspace;
			this.Items = items.ToSTuple();
		}

		[SkipLocalsInit, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public FdbTupleKey(IKeySubspace subspace, T1 item1, T2 item2, T3 item3, T4 item4)
		{
			this.Subspace = subspace;
			this.Items = STuple.Create(item1, item2, item3, item4);
		}

		public readonly IKeySubspace Subspace;

		public readonly STuple<T1, T2, T3, T4> Items;

		#region Equals(...)

		/// <inheritdoc />
		public override bool Equals([NotNullWhen(true)] object? other)
		{
			return other switch
			{
				FdbTupleKey<T1, T2, T3, T4> key => this.Equals(key),
				FdbRawKey key => this.Equals(key.Data),
				IVarTuple tuple => this.Equals(tuple),
				Slice bytes => this.Equals(bytes),
				_ => false,
			};
		}

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public override int GetHashCode() => this.Items.GetHashCode();

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool Equals(FdbTupleKey<T1, T2, T3, T4> other) => Equals(this.Subspace, other.Subspace) && this.Items.Equals(other.Items);

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool Equals(STuple<T1, T2, T3, T4> other) => this.Items.Equals(other);

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool Equals(IVarTuple? other) => this.Items.Equals(other);

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool Equals(FdbRawKey other) => FdbKeyExtensions.Equals(in this, other.Data);

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool Equals(Slice other) => FdbKeyExtensions.Equals(in this, other);

		/// <inheritdoc cref="Equals(Slice)"/>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool Equals(ReadOnlySpan<byte> other) => FdbKeyExtensions.Equals(in this, other);

		#endregion

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public FdbTupleKey<T1, T2, T3, T4, T5> Append<T5>(T5 item5) => new(this.Subspace, STuple.Create(this.Items.Item1, this.Items.Item2, this.Items.Item3, this.Items.Item4, item5));

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public FdbTupleKey<T1, T2, T3, T4, T5, T6> Append<T5, T6>(T5 item5, T6 item6) => new(this.Subspace, STuple.Create(this.Items.Item1, this.Items.Item2, this.Items.Item3, this.Items.Item4, item5, item6));

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public FdbTupleKey<T1, T2, T3, T4, T5, T6, T7> Append<T5, T6, T7>(T5 item5, T6 item6, T7 item7) => new(this.Subspace, STuple.Create(this.Items.Item1, this.Items.Item2, this.Items.Item3, this.Items.Item4, item5, item6, item7));

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public FdbTupleKey<T1, T2, T3, T4, T5, T6, T7, T8> Append<T5, T6, T7, T8>(T5 item5, T6 item6, T7 item7, T8 item8) => new(this.Subspace, STuple.Create(this.Items.Item1, this.Items.Item2, this.Items.Item3, this.Items.Item4, item5, item6, item7, item8));

		/// <inheritdoc />
		public string ToString(string? format, IFormatProvider? formatProvider) => $"[{this.Subspace}] {this.Items}";

		/// <inheritdoc />
		public bool TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format, IFormatProvider? provider) => destination.TryWrite($"[{this.Subspace}] {this.Items}", out charsWritten);

		/// <inheritdoc />
		public override string ToString() => ToString(null, null);

		/// <inheritdoc />
		IKeySubspace? IFdbKey.GetSubspace() => this.Subspace;

		/// <inheritdoc />
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool TryGetSpan(out ReadOnlySpan<byte> span) { span = default; return false; }

		/// <inheritdoc />
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool TryGetSizeHint(out int sizeHint)
		{
			if (!TupleEncoder.TryGetSizeHint(this.Items.Item1, out var size1)
			 || !TupleEncoder.TryGetSizeHint(this.Items.Item2, out var size2)
			 || !TupleEncoder.TryGetSizeHint(this.Items.Item3, out var size3)
			 || !TupleEncoder.TryGetSizeHint(this.Items.Item4, out var size4))
			{
				sizeHint = 0;
				return false;
			}

			sizeHint = checked(this.Subspace.GetPrefix().Count + size1 + size2 + size3 + size4);
			return true;
		}

		/// <inheritdoc />
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool TryEncode(Span<byte> destination, out int bytesWritten) => TupleEncoder.TryPackTo(destination, out bytesWritten, this.Subspace.GetPrefix().Span, in this.Items);

	}

	[DebuggerDisplay("{ToString(),nq}")]
	public readonly struct FdbTupleKey<T1, T2, T3, T4, T5> : IFdbKey
		, IEquatable<FdbTupleKey<T1, T2, T3, T4, T5>>, IEquatable<STuple<T1, T2, T3, T4, T5>>, IEquatable<IVarTuple>, IEquatable<Slice>, IEquatable<FdbRawKey>
#if NET9_0_OR_GREATER
		, IEquatable<ReadOnlySpan<byte>>
#endif
	{

		[SkipLocalsInit, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public FdbTupleKey(IKeySubspace subspace, in STuple<T1, T2, T3, T4, T5> items)
		{
			this.Subspace = subspace;
			this.Items = items;
		}

		[SkipLocalsInit, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public FdbTupleKey(IKeySubspace subspace, in ValueTuple<T1, T2, T3, T4, T5> items)
		{
			this.Subspace = subspace;
			this.Items = items.ToSTuple();
		}

		[SkipLocalsInit, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public FdbTupleKey(IKeySubspace subspace, T1 item1, T2 item2, T3 item3, T4 item4, T5 item5)
		{
			this.Subspace = subspace;
			this.Items = STuple.Create(item1, item2, item3, item4, item5);
		}

		public readonly IKeySubspace Subspace;

		public readonly STuple<T1, T2, T3, T4, T5> Items;

		#region Equals(...)

		/// <inheritdoc />
		public override bool Equals([NotNullWhen(true)] object? other)
		{
			return other switch
			{
				FdbTupleKey<T1, T2, T3, T4, T5> key => this.Equals(key),
				FdbRawKey key => this.Equals(key.Data),
				IVarTuple tuple => this.Equals(tuple),
				Slice bytes => this.Equals(bytes),
				_ => false,
			};
		}

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public override int GetHashCode() => this.Items.GetHashCode();

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool Equals(FdbTupleKey<T1, T2, T3, T4, T5> other) => Equals(this.Subspace, other.Subspace) && this.Items.Equals(other.Items);

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool Equals(STuple<T1, T2, T3, T4, T5> other) => this.Items.Equals(other);

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool Equals(IVarTuple? other) => this.Items.Equals(other);

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool Equals(FdbRawKey other) => FdbKeyExtensions.Equals(in this, other.Data);

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool Equals(Slice other) => FdbKeyExtensions.Equals(in this, other);

		/// <inheritdoc cref="Equals(Slice)"/>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool Equals(ReadOnlySpan<byte> other) => FdbKeyExtensions.Equals(in this, other);

		#endregion

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public FdbTupleKey<T1, T2, T3, T4, T5, T6> Append<T6>(T6 item6) => new(this.Subspace, STuple.Create(this.Items.Item1, this.Items.Item2, this.Items.Item3, this.Items.Item4, this.Items.Item5, item6));

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public FdbTupleKey<T1, T2, T3, T4, T5, T6, T7> Append<T6, T7>(T6 item6, T7 item7) => new(this.Subspace, STuple.Create(this.Items.Item1, this.Items.Item2, this.Items.Item3, this.Items.Item4, this.Items.Item5, item6, item7));

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public FdbTupleKey<T1, T2, T3, T4, T5, T6, T7, T8> Append<T6, T7, T8>(T6 item6, T7 item7, T8 item8) => new(this.Subspace, STuple.Create(this.Items.Item1, this.Items.Item2, this.Items.Item3, this.Items.Item4, this.Items.Item5, item6, item7, item8));

		/// <inheritdoc />
		public string ToString(string? format, IFormatProvider? formatProvider) => $"[{this.Subspace}] {this.Items}";

		/// <inheritdoc />
		public bool TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format, IFormatProvider? provider) => destination.TryWrite($"[{this.Subspace}] {this.Items}", out charsWritten);

		/// <inheritdoc />
		public override string ToString() => ToString(null, null);

		/// <inheritdoc />
		IKeySubspace? IFdbKey.GetSubspace() => this.Subspace;

		/// <inheritdoc />
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool TryGetSpan(out ReadOnlySpan<byte> span) { span = default; return false; }

		/// <inheritdoc />
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool TryGetSizeHint(out int sizeHint)
		{
			if (!TupleEncoder.TryGetSizeHint(this.Items.Item1, out var size1)
			 || !TupleEncoder.TryGetSizeHint(this.Items.Item2, out var size2)
			 || !TupleEncoder.TryGetSizeHint(this.Items.Item3, out var size3)
			 || !TupleEncoder.TryGetSizeHint(this.Items.Item4, out var size4)
			 || !TupleEncoder.TryGetSizeHint(this.Items.Item5, out var size5))
			{
				sizeHint = 0;
				return false;
			}

			sizeHint = checked(this.Subspace.GetPrefix().Count + size1 + size2 + size3 + size4 + size5);
			return true;
		}

		/// <inheritdoc />
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool TryEncode(Span<byte> destination, out int bytesWritten) => TupleEncoder.TryPackTo(destination, out bytesWritten, this.Subspace.GetPrefix().Span, in this.Items);

	}

	[DebuggerDisplay("{ToString(),nq}")]
	public readonly struct FdbTupleKey<T1, T2, T3, T4, T5, T6> : IFdbKey
		, IEquatable<FdbTupleKey<T1, T2, T3, T4, T5, T6>>, IEquatable<STuple<T1, T2, T3, T4, T5, T6>>, IEquatable<IVarTuple>, IEquatable<Slice>, IEquatable<FdbRawKey>
#if NET9_0_OR_GREATER
		, IEquatable<ReadOnlySpan<byte>>
#endif
	{

		[SkipLocalsInit, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public FdbTupleKey(IKeySubspace subspace, in STuple<T1, T2, T3, T4, T5, T6> items)
		{
			this.Subspace = subspace;
			this.Items = items;
		}

		[SkipLocalsInit, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public FdbTupleKey(IKeySubspace subspace, in ValueTuple<T1, T2, T3, T4, T5, T6> items)
		{
			this.Subspace = subspace;
			this.Items = items.ToSTuple();
		}

		[SkipLocalsInit, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public FdbTupleKey(IKeySubspace subspace, T1 item1, T2 item2, T3 item3, T4 item4, T5 item5, T6 item6)
		{
			this.Subspace = subspace;
			this.Items = STuple.Create(item1, item2, item3, item4, item5, item6);
		}

		public readonly IKeySubspace Subspace;

		public readonly STuple<T1, T2, T3, T4, T5, T6> Items;

		#region Equals(...)

		/// <inheritdoc />
		public override bool Equals([NotNullWhen(true)] object? other)
		{
			return other switch
			{
				FdbTupleKey<T1, T2, T3, T4, T5, T6> key => this.Equals(key),
				FdbRawKey key => this.Equals(key.Data),
				IVarTuple tuple => this.Equals(tuple),
				Slice bytes => this.Equals(bytes),
				_ => false,
			};
		}

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public override int GetHashCode() => this.Items.GetHashCode();

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool Equals(FdbTupleKey<T1, T2, T3, T4, T5, T6> other) => Equals(this.Subspace, other.Subspace) && this.Items.Equals(other.Items);

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool Equals(STuple<T1, T2, T3, T4, T5, T6> other) => this.Items.Equals(other);

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool Equals(IVarTuple? other) => this.Items.Equals(other);

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool Equals(FdbRawKey other) => FdbKeyExtensions.Equals(in this, other.Data);

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool Equals(Slice other) => FdbKeyExtensions.Equals(in this, other);

		/// <inheritdoc cref="Equals(Slice)"/>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool Equals(ReadOnlySpan<byte> other) => FdbKeyExtensions.Equals(in this, other);

		#endregion

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public FdbTupleKey<T1, T2, T3, T4, T5, T6, T7> Append<T7>(T7 item7) => new(this.Subspace, this.Items.Item1, this.Items.Item2, this.Items.Item3, this.Items.Item4, this.Items.Item5, this.Items.Item6, item7);

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public FdbTupleKey<T1, T2, T3, T4, T5, T6, T7, T8> Append<T7, T8>(T7 item7, T8 item8) => new(this.Subspace, this.Items.Item1, this.Items.Item2, this.Items.Item3, this.Items.Item4, this.Items.Item5, this.Items.Item6, item7, item8);

		/// <inheritdoc />
		public string ToString(string? format, IFormatProvider? formatProvider) => $"[{this.Subspace}] {this.Items}";

		/// <inheritdoc />
		public bool TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format, IFormatProvider? provider) => destination.TryWrite($"[{this.Subspace}] {this.Items}", out charsWritten);

		/// <inheritdoc />
		public override string ToString() => ToString(null, null);

		/// <inheritdoc />
		IKeySubspace? IFdbKey.GetSubspace() => this.Subspace;

		/// <inheritdoc />
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool TryGetSpan(out ReadOnlySpan<byte> span) { span = default; return false; }

		/// <inheritdoc />
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool TryGetSizeHint(out int sizeHint)
		{
			if (!TupleEncoder.TryGetSizeHint(this.Items.Item1, out var size1)
			 || !TupleEncoder.TryGetSizeHint(this.Items.Item2, out var size2)
			 || !TupleEncoder.TryGetSizeHint(this.Items.Item3, out var size3)
			 || !TupleEncoder.TryGetSizeHint(this.Items.Item4, out var size4)
			 || !TupleEncoder.TryGetSizeHint(this.Items.Item5, out var size5)
			 || !TupleEncoder.TryGetSizeHint(this.Items.Item6, out var size6))
			{
				sizeHint = 0;
				return false;
			}

			sizeHint = checked(this.Subspace.GetPrefix().Count + size1 + size2 + size3 + size4 + size5 + size6);
			return true;
		}

		/// <inheritdoc />
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool TryEncode(Span<byte> destination, out int bytesWritten) => TupleEncoder.TryPackTo(destination, out bytesWritten, this.Subspace.GetPrefix().Span, in this.Items);

	}

	[DebuggerDisplay("{ToString(),nq}")]
	public readonly struct FdbTupleKey<T1, T2, T3, T4, T5, T6, T7> : IFdbKey
		, IEquatable<FdbTupleKey<T1, T2, T3, T4, T5, T6, T7>>, IEquatable<STuple<T1, T2, T3, T4, T5, T6, T7>>, IEquatable<IVarTuple>, IEquatable<Slice>, IEquatable<FdbRawKey>
#if NET9_0_OR_GREATER
		, IEquatable<ReadOnlySpan<byte>>
#endif
	{

		[SkipLocalsInit, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public FdbTupleKey(IKeySubspace subspace, in STuple<T1, T2, T3, T4, T5, T6, T7> items)
		{
			this.Subspace = subspace;
			this.Items = items;
		}

		[SkipLocalsInit, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public FdbTupleKey(IKeySubspace subspace, in ValueTuple<T1, T2, T3, T4, T5, T6, T7> items)
		{
			this.Subspace = subspace;
			this.Items = items.ToSTuple();
		}

		[SkipLocalsInit, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public FdbTupleKey(IKeySubspace subspace, T1 item1, T2 item2, T3 item3, T4 item4, T5 item5, T6 item6, T7 item7)
		{
			this.Subspace = subspace;
			this.Items = STuple.Create(item1, item2, item3, item4, item5, item6, item7);
		}

		public readonly IKeySubspace Subspace;

		public readonly STuple<T1, T2, T3, T4, T5, T6, T7> Items;

		#region Equals(...)

		/// <inheritdoc />
		public override bool Equals([NotNullWhen(true)] object? other)
		{
			return other switch
			{
				FdbTupleKey<T1, T2, T3, T4, T5, T6, T7> key => this.Equals(key),
				FdbRawKey key => this.Equals(key.Data),
				IVarTuple tuple => this.Equals(tuple),
				Slice bytes => this.Equals(bytes),
				_ => false,
			};
		}

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public override int GetHashCode() => this.Items.GetHashCode();

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool Equals(FdbTupleKey<T1, T2, T3, T4, T5, T6, T7> other) => Equals(this.Subspace, other.Subspace) && this.Items.Equals(other.Items);

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool Equals(STuple<T1, T2, T3, T4, T5, T6, T7> other) => this.Items.Equals(other);

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool Equals(IVarTuple? other) => this.Items.Equals(other);

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool Equals(FdbRawKey other) => FdbKeyExtensions.Equals(in this, other.Data);

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool Equals(Slice other) => FdbKeyExtensions.Equals(in this, other);

		/// <inheritdoc cref="Equals(Slice)"/>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool Equals(ReadOnlySpan<byte> other) => FdbKeyExtensions.Equals(in this, other);

		#endregion

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public FdbTupleKey<T1, T2, T3, T4, T5, T6, T7, T8> Append<T8>(T8 item8) => new(this.Subspace, this.Items.Item1, this.Items.Item2, this.Items.Item3, this.Items.Item4, this.Items.Item5, this.Items.Item6, this.Items.Item7, item8);

		/// <inheritdoc />
		public string ToString(string? format, IFormatProvider? formatProvider) => $"[{this.Subspace}] {this.Items}";

		/// <inheritdoc />
		public bool TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format, IFormatProvider? provider) => destination.TryWrite($"[{this.Subspace}] {this.Items}", out charsWritten);

		/// <inheritdoc />
		public override string ToString() => ToString(null, null);

		/// <inheritdoc />
		IKeySubspace? IFdbKey.GetSubspace() => this.Subspace;

		/// <inheritdoc />
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool TryGetSpan(out ReadOnlySpan<byte> span) { span = default; return false; }

		/// <inheritdoc />
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool TryGetSizeHint(out int sizeHint)
		{
			if (!TupleEncoder.TryGetSizeHint(this.Items.Item1, out var size1)
			 || !TupleEncoder.TryGetSizeHint(this.Items.Item2, out var size2)
			 || !TupleEncoder.TryGetSizeHint(this.Items.Item3, out var size3)
			 || !TupleEncoder.TryGetSizeHint(this.Items.Item4, out var size4)
			 || !TupleEncoder.TryGetSizeHint(this.Items.Item5, out var size5)
			 || !TupleEncoder.TryGetSizeHint(this.Items.Item6, out var size6)
			 || !TupleEncoder.TryGetSizeHint(this.Items.Item7, out var size7))
			{
				sizeHint = 0;
				return false;
			}

			sizeHint = checked(this.Subspace.GetPrefix().Count + size1 + size2 + size3 + size4 + size5 + size6 + size7);
			return true;
		}

		/// <inheritdoc />
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool TryEncode(Span<byte> destination, out int bytesWritten) => TupleEncoder.TryPackTo(destination, out bytesWritten, this.Subspace.GetPrefix().Span, in this.Items);

	}

	[DebuggerDisplay("{ToString(),nq}")]
	public readonly struct FdbTupleKey<T1, T2, T3, T4, T5, T6, T7, T8> : IFdbKey
		, IEquatable<FdbTupleKey<T1, T2, T3, T4, T5, T6, T7, T8>>, IEquatable<STuple<T1, T2, T3, T4, T5, T6, T7, T8>>, IEquatable<IVarTuple>, IEquatable<Slice>, IEquatable<FdbRawKey>
#if NET9_0_OR_GREATER
		, IEquatable<ReadOnlySpan<byte>>
#endif
	{

		[SkipLocalsInit, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public FdbTupleKey(IKeySubspace subspace, in STuple<T1, T2, T3, T4, T5, T6, T7, T8> items)
		{
			this.Subspace = subspace;
			this.Items = items;
		}

		[SkipLocalsInit, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public FdbTupleKey(IKeySubspace subspace, in ValueTuple<T1, T2, T3, T4, T5, T6, T7, ValueTuple<T8>> items)
		{
			this.Subspace = subspace;
			this.Items = items.ToSTuple();
		}

		[SkipLocalsInit, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public FdbTupleKey(IKeySubspace subspace, T1 item1, T2 item2, T3 item3, T4 item4, T5 item5, T6 item6, T7 item7, T8 item8)
		{
			this.Subspace = subspace;
			this.Items = STuple.Create(item1, item2, item3, item4, item5, item6, item7, item8);
		}

		public readonly IKeySubspace Subspace;

		public readonly STuple<T1, T2, T3, T4, T5, T6, T7, T8> Items;

		#region Equals(...)

		/// <inheritdoc />
		public override bool Equals([NotNullWhen(true)] object? other)
		{
			return other switch
			{
				FdbTupleKey<T1, T2, T3, T4, T5, T6, T7, T8> key => this.Equals(key),
				FdbRawKey key => this.Equals(key.Data),
				IVarTuple tuple => this.Equals(tuple),
				Slice bytes => this.Equals(bytes),
				_ => false,
			};
		}

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public override int GetHashCode() => this.Items.GetHashCode();

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool Equals(FdbTupleKey<T1, T2, T3, T4, T5, T6, T7, T8> other) => Equals(this.Subspace, other.Subspace) && this.Items.Equals(other.Items);

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool Equals(STuple<T1, T2, T3, T4, T5, T6, T7, T8> other) => this.Items.Equals(other);

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool Equals(IVarTuple? other) => this.Items.Equals(other);

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool Equals(FdbRawKey other) => FdbKeyExtensions.Equals(in this, other.Data);

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool Equals(Slice other) => FdbKeyExtensions.Equals(in this, other);

		/// <inheritdoc cref="Equals(Slice)"/>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool Equals(ReadOnlySpan<byte> other) => FdbKeyExtensions.Equals(in this, other);

		#endregion

		/// <inheritdoc />
		public string ToString(string? format, IFormatProvider? formatProvider) => $"[{this.Subspace}] {this.Items}";

		/// <inheritdoc />
		public bool TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format, IFormatProvider? provider) => destination.TryWrite($"[{this.Subspace}] {this.Items}", out charsWritten);

		/// <inheritdoc />
		public override string ToString() => ToString(null, null);

		/// <inheritdoc />
		IKeySubspace? IFdbKey.GetSubspace() => this.Subspace;

		/// <inheritdoc />
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool TryGetSpan(out ReadOnlySpan<byte> span) { span = default; return false; }

		/// <inheritdoc />
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool TryGetSizeHint(out int sizeHint)
		{
			if (!TupleEncoder.TryGetSizeHint(this.Items.Item1, out var size1)
			 || !TupleEncoder.TryGetSizeHint(this.Items.Item2, out var size2)
			 || !TupleEncoder.TryGetSizeHint(this.Items.Item3, out var size3)
			 || !TupleEncoder.TryGetSizeHint(this.Items.Item4, out var size4)
			 || !TupleEncoder.TryGetSizeHint(this.Items.Item5, out var size5)
			 || !TupleEncoder.TryGetSizeHint(this.Items.Item6, out var size6)
			 || !TupleEncoder.TryGetSizeHint(this.Items.Item7, out var size7)
			 || !TupleEncoder.TryGetSizeHint(this.Items.Item8, out var size8))
			{
				sizeHint = 0;
				return false;
			}

			sizeHint = checked(this.Subspace.GetPrefix().Count + size1 + size2 + size3 + size4 + size5 + size6 + size7 + size8);
			return true;
		}

		/// <inheritdoc />
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool TryEncode(Span<byte> destination, out int bytesWritten) => TupleEncoder.TryPackTo(destination, out bytesWritten, this.Subspace.GetPrefix().Span, in this.Items);

	}

	#endregion

}
