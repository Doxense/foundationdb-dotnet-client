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

	using System.Numerics;

	/// <summary>Wraps a <see cref="Slice"/> that contains a pre-encoded key in the database</summary>
	/// <remarks>
	/// <para>This can help in situations where the same key will be frequently used in the same transaction, and where the cost of allocating on the heap will be less than re-encoding the key over and over.</para>
	/// <para>Please refrain from capturing a key in once transaction and reusing it in another. It is possible that the original subspace would be moved or re-created between both transactions, with a different prefix, which could create silent data corruption!</para>
	/// </remarks>
	public readonly struct FdbRawKey : IFdbKey
		, IComparisonOperators<FdbRawKey, FdbRawKey, bool>
		, IComparisonOperators<FdbRawKey, Slice, bool>
	{

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		[SkipLocalsInit]
		internal FdbRawKey(Slice data)
		{
			this.Data = data;
		}

		/// <summary>Pre-encoded bytes for this key</summary>
		public readonly Slice Data;

		[Pure]
		public ReadOnlySpan<byte> Span => this.Data.Span;

		/// <inheritdoc />
		IKeySubspace? IFdbKey.GetSubspace() => null;

		#region Equals(...)

		/// <inheritdoc />
		public override bool Equals([NotNullWhen(true)] object? other) => other switch
		{
			Slice bytes => this.Data.Equals(bytes),
			FdbRawKey key => this.Data.Equals(key.Data),
			FdbTupleKey key => key.Equals(this.Data),
			FdbSuffixKey key => key.Equals(this.Data),
			FdbSubspaceKey key => key.Equals(this.Data),
			IFdbKey key => FdbKeyHelpers.AreEqual(in this, key),
			_ => false,
		};

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public override int GetHashCode() => this.Data.GetHashCode();

		/// <inheritdoc />
		public bool Equals([NotNullWhen(true)] IFdbKey? other) => other switch
		{
			null => false,
			FdbRawKey key => this.Data.Equals(key.Data),
			FdbTupleKey key => key.Equals(this.Data),
			FdbSuffixKey key => key.Equals(this.Data),
			FdbSubspaceKey key => key.Equals(this.Data),
			_ => FdbKeyHelpers.AreEqual(in this, other),
		};

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool FastEqualTo<TOtherKey>(in TOtherKey other)
			where TOtherKey : struct, IFdbKey
		{
			if (typeof(TOtherKey) == typeof(FdbRawKey))      { return (     (FdbRawKey) (object) other).Data.Equals(this.Data); }
			if (typeof(TOtherKey) == typeof(FdbTupleKey))    { return (   (FdbTupleKey) (object) other).Equals(this.Data); }
			if (typeof(TOtherKey) == typeof(FdbSuffixKey))   { return (  (FdbSuffixKey) (object) other).Equals(this.Data); }
			if (typeof(TOtherKey) == typeof(FdbSubspaceKey)) { return ((FdbSubspaceKey) (object) other).Equals(this.Data); }
			return FdbKeyHelpers.AreEqual(in this, in other);
		}

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool Equals(FdbRawKey other) => this.Data.Equals(other.Data);

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool Equals(Slice other) => this.Data.Equals(other);

		/// <inheritdoc cref="Equals(Slice)"/>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool Equals(ReadOnlySpan<byte> other) => this.Data.Equals(other);

		/// <inheritdoc />
		public int CompareTo(object? obj) => obj switch
		{
			Slice key => CompareTo(key.Span),
			FdbRawKey key => CompareTo(key.Span),
			FdbTupleKey key => -key.CompareTo(this.Span),
			FdbSuffixKey key => -key.CompareTo(this.Span),
			IFdbKey other => FdbKeyHelpers.Compare(in this, other),
			_ => throw new NotSupportedException()
		};

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public int CompareTo(FdbRawKey other) => this.Data.CompareTo(other.Data);

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public int CompareTo(Slice other) => this.Data.CompareTo(other);

		/// <inheritdoc cref="CompareTo(Slice)" />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public int CompareTo(ReadOnlySpan<byte> other) => this.Data.Span.SequenceCompareTo(other);

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public int FastCompareTo<TOtherKey>(in TOtherKey other)
			where TOtherKey : struct, IFdbKey
		{
			if (typeof(TOtherKey) == typeof(FdbRawKey))
			{
				return this.Data.CompareTo(((FdbRawKey) (object) other).Data);
			}
			return FdbKeyHelpers.Compare(in this, in other);
		}

		#region Operators...

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator ==(FdbRawKey left, FdbRawKey right) => left.Data.Equals(right.Data);

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator ==(FdbRawKey left, Slice right) => left.Data.Equals(right);

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator !=(FdbRawKey left, FdbRawKey right) => !left.Data.Equals(right.Data);

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator !=(FdbRawKey left, Slice right) => !left.Data.Equals(right);

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator <(FdbRawKey left, FdbRawKey right) => left.Data.CompareTo(right.Data) < 0;

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator <(FdbRawKey left, Slice right) => left.Data.CompareTo(right) < 0;

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator <=(FdbRawKey left, FdbRawKey right) => left.Data.CompareTo(right.Data) <= 0;

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator <=(FdbRawKey left, Slice right) => left.Data.CompareTo(right) <= 0;

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator >(FdbRawKey left, FdbRawKey right) => left.Data.CompareTo(right.Data) > 0;

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator >(FdbRawKey left, Slice right) => left.Data.CompareTo(right) > 0;

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator >=(FdbRawKey left, FdbRawKey right) => left.Data.CompareTo(right.Data) >= 0;

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator >=(FdbRawKey left, Slice right) => left.Data.CompareTo(right) >= 0;

		#endregion

		#endregion

		/// <inheritdoc />
		public override string ToString() => ToString(null);

		/// <inheritdoc />
		public string ToString(string? format, IFormatProvider? formatProvider = null) => (format ?? "") switch
		{
			"" or "D" or "d" or "K" or "k" or "P" or "p" => FdbKey.PrettyPrint(this.Data, FdbKey.PrettyPrintMode.Single),
			"B" or "b" => FdbKey.PrettyPrint(this.Data, FdbKey.PrettyPrintMode.Begin),
			"E" or "e" => FdbKey.PrettyPrint(this.Data, FdbKey.PrettyPrintMode.End),
			"X" or "x" => this.Data.ToString(format),
			"G" or "g" => $"{nameof(FdbRawKey)}({Data:K})",
			_ => throw new FormatException(),
		};

		/// <inheritdoc />
		public bool TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format = default, IFormatProvider? provider = null) => format switch
		{
			"" or "D" or "d" or "K" or "k" or "P" or "p" => FdbKey.PrettyPrint(this.Data, FdbKey.PrettyPrintMode.Single).TryCopyTo(destination, out charsWritten),
			"B" or "b" => FdbKey.PrettyPrint(this.Data, FdbKey.PrettyPrintMode.Begin).TryCopyTo(destination, out charsWritten),
			"E" or "e" => FdbKey.PrettyPrint(this.Data, FdbKey.PrettyPrintMode.End).TryCopyTo(destination, out charsWritten),
			"X" or "x" => this.Data.TryFormat(destination, out charsWritten, format, provider),
			"G" or "g" => destination.TryWrite($"{nameof(FdbRawKey)}({this.Data:K})", out charsWritten),
			_ => throw new FormatException(),
		};

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool TryGetSpan(out ReadOnlySpan<byte> span)
		{
			span = this.Data.Span;
			return true;
		}

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool TryGetSizeHint(out int sizeHint)
		{
			sizeHint = this.Data.Count;
			return true;
		}

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool TryEncode(Span<byte> destination, out int bytesWritten)
		{
			return this.Data.TryCopyTo(destination, out bytesWritten);
		}


	}

}
