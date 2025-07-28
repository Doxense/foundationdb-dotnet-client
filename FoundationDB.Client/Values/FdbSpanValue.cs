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

	/// <summary>Value that wraps a span of bytes or characters</summary>
	/// <typeparam name="TElement">Type of the elements. Can only be <see cref="byte"/> or <see cref="char"/></typeparam>
	[DebuggerDisplay("Data={Data}")]
	public readonly ref struct FdbSpanValue<TElement> : IFdbValue
	{

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public FdbSpanValue(ReadOnlySpan<TElement> data)
		{
			if (typeof(TElement) != typeof(byte) && typeof(TElement) != typeof(char))
			{
				throw new NotSupportedException("Can only store bytes of characters");
			}
			this.Data = data;
		}

		/// <summary>Wrapped span</summary>
		public readonly ReadOnlySpan<TElement> Data;

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private ReadOnlySpan<byte> GetAsBytes()
		{
			if (typeof(TElement) != typeof(byte))
			{
				throw new NotSupportedException();
			}

			ref byte ptr = ref Unsafe.As<TElement, byte>(ref MemoryMarshal.GetReference(this.Data));
			return MemoryMarshal.CreateSpan(ref ptr, this.Data.Length);

		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private ReadOnlySpan<char> GetAsChars()
		{
			if (typeof(TElement) != typeof(char))
			{
				throw new NotSupportedException();
			}

			ref char ptr = ref Unsafe.As<TElement, char>(ref MemoryMarshal.GetReference(this.Data));
			return MemoryMarshal.CreateSpan(ref ptr, this.Data.Length);

		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool TryGetSpan(out ReadOnlySpan<byte> span)
		{
			if (typeof(TElement) == typeof(byte))
			{
				span = GetAsBytes();
				return true;
			}

			if (typeof(TElement) == typeof(char))
			{
				span = default;
				return this.Data.Length == 0;
			}

			throw new NotSupportedException();
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool TryGetSizeHint(out int sizeHint)
		{
			if (typeof(TElement) == typeof(byte))
			{
				sizeHint = this.Data.Length;
				return true;
			}

			if (typeof(TElement) == typeof(char))
			{
				return SpanEncoders.Utf8Encoder.TryGetSizeHint(GetAsChars(), out sizeHint);
			}

			sizeHint = 0;
			return false;
		}

		[MustUseReturnValue, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool TryEncode(Span<byte> destination, out int bytesWritten)
		{
			if (typeof(TElement) == typeof(byte))
			{
				return GetAsBytes().TryCopyTo(destination, out bytesWritten);
			}
			if (typeof(TElement) == typeof(char))
			{
				return SpanEncoders.Utf8Encoder.TryEncode(destination, out bytesWritten, GetAsChars());
			}
			throw new NotSupportedException();
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public Slice ToSlice()
		{
			if (typeof(TElement) == typeof(byte))
			{
				return Slice.FromBytes(GetAsBytes());
			}

			if (typeof(TElement) == typeof(char))
			{
				return Slice.FromStringUtf8(GetAsChars());
			}

			throw new NotSupportedException();
		}

		[MustDisposeResource, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public SliceOwner ToSlice(ArrayPool<byte>? pool)
		{
			if (typeof(TElement) == typeof(byte))
			{
				return Slice.FromBytes(GetAsBytes(), pool ?? ArrayPool<byte>.Shared);
			}

			if (typeof(TElement) == typeof(char))
			{
				return Slice.FromStringUtf8(GetAsChars(), pool ?? ArrayPool<byte>.Shared);
			}

			throw new NotSupportedException();
		}

		/// <inheritdoc />
		public override string ToString() => ToString(null);

		/// <inheritdoc />
		public string ToString(string? format, IFormatProvider? provider = null)
		{
			if (typeof(TElement) == typeof(byte))
			{
				return $"`{Slice.Dump(GetAsBytes())}`";
			}

			if (typeof(TElement) == typeof(char))
			{
				return $"\"{GetAsChars()}\"";
			}

			throw new NotSupportedException();
		}

		/// <inheritdoc />
		public bool TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format, IFormatProvider? provider)
		{
			if (typeof(TElement) == typeof(byte))
			{
				return destination.TryWrite($"`{Slice.Dump(GetAsBytes())}`", out charsWritten);
			}

			if (typeof(TElement) == typeof(char))
			{
				return destination.TryWrite($"\"{GetAsChars()}\"", out charsWritten);
			}

			throw new NotSupportedException();
		}

	}

}
