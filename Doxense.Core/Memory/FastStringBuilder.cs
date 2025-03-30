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

namespace Doxense.Memory
{
	using System;
	using System.Buffers;
	using System.Globalization;
	using System.Runtime.InteropServices;
	using System.Text;
	using Doxense.Serialization;

	/// <summary>Span-based implementation of a <see cref="StringBuilder"/>, with reduced safety checks</summary>
	/// <remarks>All formatting uses the invariant culture.</remarks>
	[PublicAPI]
	public ref struct FastStringBuilder
	{

		/// <summary>Buffer that must be returned to the shared array pool</summary>
		private char[]? PooledBuffer;

		/// <summary>Current allocated buffer</summary>
		private Span<char> Chars;

		/// <summary>Current position in the buffer</summary>
		private int Position;

		public FastStringBuilder(Span<char> initialBuffer)
		{
			this.PooledBuffer = null;
			this.Chars = initialBuffer;
			this.Position = 0;
		}

		public FastStringBuilder(int initialCapacity)
		{
			this.PooledBuffer = ArrayPool<char>.Shared.Rent(initialCapacity);
			this.Chars = new Span<char>(this.PooledBuffer);
			this.Position = 0;
		}

		/// <summary>Gets or sets the length of the current <see cref="T:System.Text.StringBuilder" /> object.</summary>
		/// <returns>The length of this instance.</returns>
		public int Length
		{
			get => this.Position;
			set
			{
				Contract.Debug.Assert((uint) value <= this.Chars.Length);
				this.Position = value;
			}
		}

		/// <summary>Gets the maximum number of characters that can be contained in the memory allocated by the current instance.</summary>
		public int Capacity => this.Chars.Length;

		/// <summary>Gets a pinnable reference to the builder.</summary>
		/// <remarks>
		/// <para>Does not ensure there is a null char after <see cref="Length"/>.</para>
		/// <para>This overload is pattern matched in the C# 7.3+ compiler so you can omit the explicit method call, and write e.g. <c>fixed (char* c = builder)</c></para>
		/// </remarks>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void EnsureCapacity(int capacity)
		{
			Contract.Debug.Requires(capacity >= 0);
			if ((uint) capacity > (uint) this.Chars.Length)
			{
				Grow(capacity - Position);
			}
		}

		public ref char GetPinnableReference() => ref MemoryMarshal.GetReference(this.Chars);

		/// <summary>Gets a pinnable reference to the builder.</summary>
		/// <param name="terminate">Ensures that the builder has a null char after <see cref="Length"/></param>
		public ref char GetPinnableReference(bool terminate)
		{
			if (terminate)
			{
				EnsureCapacity(this.Length + 1);
				this.Chars[this.Length] = '\0';
			}
			return ref MemoryMarshal.GetReference(this.Chars);
		}


		public ref char this[int index]
		{
			get
			{
				Contract.Debug.Assert(index < this.Position);
				return ref this.Chars[index];
			}
		}

		public override string ToString()
		{
			string s = this.Chars.Slice(0, this.Position).ToString();
			Dispose();
			return s;
		}

		/// <summary>Returns the underlying storage of the builder.</summary>
		public Span<char> RawChars => this.Chars;

		/// <summary>Returns a span around the contents of the builder.</summary>
		/// <param name="terminate">Ensures that the builder has a null char after <see cref="Length"/></param>
		public ReadOnlySpan<char> AsSpan(bool terminate)
		{
			if (terminate)
			{
				EnsureCapacity(this.Length + 1);
				this.Chars[this.Length] = '\0';
			}
			return this.Chars[..this.Position];
		}

		public ReadOnlySpan<char> AsSpan() => this.Chars[..this.Position];
		public ReadOnlySpan<char> AsSpan(int start) => this.Chars.Slice(start, this.Position - start);
		public ReadOnlySpan<char> AsSpan(int start, int length) => this.Chars.Slice(start, length);

		public bool TryCopyTo(Span<char> destination, out int charsWritten)
		{
			if (this.Chars[..this.Position].TryCopyTo(destination))
			{
				charsWritten = this.Position;
				Dispose();
				return true;
			}
			else
			{
				charsWritten = 0;
				Dispose();
				return false;
			}
		}

		public void Insert(int index, char value, int count)
		{
			if (this.Position > this.Chars.Length - count)
			{
				Grow(count);
			}

			int remaining = this.Position - index;
			this.Chars.Slice(index, remaining).CopyTo(this.Chars[(index + count)..]);
			this.Chars.Slice(index, count).Fill(value);
			this.Position += count;
		}

		public void Insert(int index, string? s)
		{
			if (s == null)
			{
				return;
			}

			int count = s.Length;

			if (this.Position > (this.Chars.Length - count))
			{
				Grow(count);
			}

			int remaining = this.Position - index;
			this.Chars.Slice(index, remaining).CopyTo(this.Chars[(index + count)..]);
			s.CopyTo(this.Chars[index..]);
			this.Position += count;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void Append(char c)
		{
			int pos = this.Position;
			var chars = this.Chars;
			if ((uint) pos < (uint) chars.Length)
			{
				chars[pos] = c;
				this.Position = pos + 1;
			}
			else
			{
				GrowAndAppend(c);
			}
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void Append(string? s)
		{
			if (s == null)
			{
				return;
			}

			int pos = this.Position;
			if (s.Length == 1 && (uint) pos < (uint) this.Chars.Length)
			{ // very common case, e.g. appending strings from NumberFormatInfo like separators, percent symbols, etc.
				this.Chars[pos] = s[0];
				this.Position = pos + 1;
			}
			else
			{
				AppendSlow(s);
			}
		}

		private void AppendSlow(string s)
		{
			int pos = this.Position;
			if (pos > this.Chars.Length - s.Length)
			{
				Grow(s.Length);
			}

			s.CopyTo(this.Chars[pos..]);
			this.Position += s.Length;
		}

#if NET9_0_OR_GREATER

		[StringFormatMethod(nameof(format))]
		public void AppendFormat([StringSyntax("CompositeFormat")] string format, params scoped ReadOnlySpan<object?> args)
		{
			AppendSlow(string.Format(CultureInfo.InvariantCulture, format, args));
		}

#elif NET8_0_OR_GREATER

		[StringFormatMethod(nameof(format))]
		public void AppendFormat([StringSyntax("CompositeFormat")] string format, object? arg0)
		{
			AppendSlow(string.Format(CultureInfo.InvariantCulture, format, arg0));
		}

		[StringFormatMethod(nameof(format))]
		public void AppendFormat([StringSyntax("CompositeFormat")] string format, object? arg0, object? arg1)
		{
			AppendSlow(string.Format(CultureInfo.InvariantCulture, format, arg0, arg1));
		}

		[StringFormatMethod(nameof(format))]
		public void AppendFormat([StringSyntax("CompositeFormat")] string format, object? arg0, object? arg1, object? arg2)
		{
			AppendSlow(string.Format(CultureInfo.InvariantCulture, format, arg0, arg1, arg2));
		}

		[StringFormatMethod(nameof(format))]
		public void AppendFormat([StringSyntax("CompositeFormat")] string format, params object?[] args)
		{
			AppendSlow(string.Format(CultureInfo.InvariantCulture, format, args));
		}

#else

		[StringFormatMethod(nameof(format))]
		public void AppendFormat(string format, object? arg0)
		{
			AppendSlow(string.Format(CultureInfo.InvariantCulture, format, arg0));
		}

		[StringFormatMethod(nameof(format))]
		public void AppendFormat(string format, object? arg0, object? arg1)
		{
			AppendSlow(string.Format(CultureInfo.InvariantCulture, format, arg0, arg1));
		}

		[StringFormatMethod(nameof(format))]
		public void AppendFormat(string format, object? arg0, object? arg1, object? arg2)
		{
			AppendSlow(string.Format(CultureInfo.InvariantCulture, format, arg0, arg1, arg2));
		}

		[StringFormatMethod(nameof(format))]
		public void AppendFormat(string format, params object?[] args)
		{
			AppendSlow(string.Format(CultureInfo.InvariantCulture, format, args));
		}

#endif

		public void Append(char c, int count)
		{
			if (this.Position > this.Chars.Length - count)
			{
				Grow(count);
			}

			var dst = this.Chars.Slice(this.Position, count);
			for (int i = 0; i < dst.Length; i++)
			{
				dst[i] = c;
			}
			this.Position += count;
		}

		public unsafe void Append(char* value, int length)
		{
			int pos = this.Position;
			if (pos > this.Chars.Length - length)
			{
				Grow(length);
			}

			var dst = this.Chars.Slice(this.Position, length);
			for (int i = 0; i < dst.Length; i++)
			{
				dst[i] = *value++;
			}
			this.Position += length;
		}

		public void Append(scoped ReadOnlySpan<char> value)
		{
			int pos = this.Position;
			if (pos > this.Chars.Length - value.Length)
			{
				Grow(value.Length);
			}

			value.CopyTo(this.Chars[this.Position..]);
			this.Position += value.Length;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public Span<char> AppendSpan(int length)
		{
			int origPos = this.Position;
			if (origPos > this.Chars.Length - length)
			{
				Grow(length);
			}

			this.Position = origPos + length;
			return this.Chars.Slice(origPos, length);
		}

		public void Append(bool value)
		{
			AppendSlow(value ? "true" : "false");
		}

		public void Append(int value)
		{
			if (value.TryFormat(this.Chars[this.Position..], out int charsWritten, null, CultureInfo.InvariantCulture))
			{
				this.Position += charsWritten;
				return;
			}

			AppendSpanFormattable(value, StringConverters.CountDigits(value));
		}

		public void Append(uint value)
		{
			//note: uint.TryFormat will call Number.TryUInt32ToDecStr that does not use 'provider', when 'format' is null

			if (value.TryFormat(this.Chars[this.Position..], out int charsWritten, null))
			{
				this.Position += charsWritten;
				return;
			}

			AppendSpanFormattable(value, StringConverters.CountDigits(value));
		}

		public void Append(long value)
		{
			if (value.TryFormat(this.Chars[this.Position..], out int charsWritten, null, CultureInfo.InvariantCulture))
			{
				this.Position += charsWritten;
				return;
			}

			AppendSpanFormattable(value, StringConverters.CountDigits(value));
		}

		public void Append(ulong value)
		{
			//note: ulong.TryFormat will call Number.TryUInt64ToDecStr that does not use 'provider', when 'format' is null

			if (value.TryFormat(this.Chars[this.Position..], out int charsWritten, null))
			{
				this.Position += charsWritten;
				return;
			}

			AppendSpanFormattable(value, StringConverters.CountDigits(value));
		}

		public void Append(double value)
		{
			if (value.TryFormat(this.Chars[this.Position..], out int charsWritten, "R", CultureInfo.InvariantCulture))
			{
				this.Position += charsWritten;
				return;
			}

			// To be safe, we would need close to 32 chars (value used in the runtime internal implementation), but this is somewhat overkill!
			// Values like Math.PI.ToString("R") use 17 characters, which would require up to 18 with a terminal zero
			AppendSpanFormattable(value, 18); // "3.141592653589793" + NUL
		}

		public void Append(float value)
		{
			if (value.TryFormat(this.Chars[this.Position..], out int charsWritten, "R", CultureInfo.InvariantCulture))
			{
				this.Position += charsWritten;
				return;
			}

			// To be safe, we would need close to 32 chars (value used in the runtime internal implementation), but this is somewhat overkill!
			// Values like ((float) Math.PI).ToString("R") use 9 characters, which would require up to 10 with a terminal zero, so we will use 12, and live with a potential double resize!
			AppendSpanFormattable(value, 10); // "3.1415927" + NUL
		}

		internal void AppendSpanFormattable<T>(T value, int sizeHint) where T : ISpanFormattable
		{
			if (this.Position > Chars.Length - sizeHint)
			{
				Grow(sizeHint);
			}

			if (value.TryFormat(this.Chars[this.Position..], out int charsWritten, null, CultureInfo.InvariantCulture))
			{
				this.Position += charsWritten;
			}
			else
			{
				Append(value.ToString(null, CultureInfo.InvariantCulture));
			}
		}

		public void Append<T>(T value) where T : ISpanFormattable
		{
			if (value.TryFormat(this.Chars[this.Position..], out int charsWritten, null, CultureInfo.InvariantCulture))
			{
				this.Position += charsWritten;
			}
			else
			{
				Append(value.ToString(null, CultureInfo.InvariantCulture));
			}
		}

		public void Append(int value, string? format, IFormatProvider? provider = null)
		{
			if (value.TryFormat(this.Chars[this.Position..], out int charsWritten, format, provider ?? CultureInfo.InvariantCulture))
			{
				this.Position += charsWritten;
				return;
			}

			AppendSpanFormattable(value, StringConverters.CountDigits(value), format, provider);
		}

		public void Append(uint value, string? format, IFormatProvider? provider = null)
		{
			//note: uint.TryFormat will call Number.TryUInt32ToDecStr that does not use 'provider', when 'format' is null

			if (value.TryFormat(this.Chars[this.Position..], out int charsWritten, format, provider ?? CultureInfo.InvariantCulture))
			{
				this.Position += charsWritten;
				return;
			}

			AppendSpanFormattable(value, StringConverters.CountDigits(value), format, provider);
		}

		public void Append(long value, string? format, IFormatProvider? provider = null)
		{
			if (value.TryFormat(this.Chars[this.Position..], out int charsWritten, format, provider ?? CultureInfo.InvariantCulture))
			{
				this.Position += charsWritten;
				return;
			}

			AppendSpanFormattable(value, StringConverters.CountDigits(value), format, provider);
		}

		public void Append(ulong value, string? format, IFormatProvider? provider = null)
		{
			//note: ulong.TryFormat will call Number.TryUInt64ToDecStr that does not use 'provider', when 'format' is null

			if (value.TryFormat(this.Chars[this.Position..], out int charsWritten, format, provider ?? CultureInfo.InvariantCulture))
			{
				this.Position += charsWritten;
				return;
			}

			AppendSpanFormattable(value, StringConverters.CountDigits(value), format, provider);
		}

		public void Append(double value, string? format, IFormatProvider? provider = null)
		{
			format ??= "R";
			provider ??= CultureInfo.InvariantCulture;

			if (value.TryFormat(this.Chars[this.Position..], out int charsWritten, format, provider))
			{
				this.Position += charsWritten;
				return;
			}

			// To be safe, we would need close to 32 chars (value used in the runtime internal implementation), but this is somewhat overkill!
			// Values like Math.PI.ToString("R") use 17 characters, which would require up to 18 with a terminal zero
			AppendSpanFormattable(value, 18, format, provider); // "3.141592653589793" + NUL
		}

		public void Append(float value, string? format, IFormatProvider? provider = null)
		{
			format ??= "R";
			provider ??= CultureInfo.InvariantCulture;

			if (value.TryFormat(this.Chars[this.Position..], out int charsWritten, format, provider))
			{
				this.Position += charsWritten;
				return;
			}

			// To be safe, we would need close to 32 chars (value used in the runtime internal implementation), but this is somewhat overkill!
			// Values like ((float) Math.PI).ToString("R") use 9 characters, which would require up to 10 with a terminal zero, so we will use 12, and live with a potential double resize!
			AppendSpanFormattable(value, 10, format, provider); // "3.1415927" + NUL
		}

		internal void AppendSpanFormattable<T>(T value, int sizeHint, string? format, IFormatProvider? provider = null) where T : ISpanFormattable
		{
			if (this.Position > Chars.Length - sizeHint)
			{
				Grow(sizeHint);
			}

			provider ??= CultureInfo.InvariantCulture;
			if (value.TryFormat(this.Chars[this.Position..], out int charsWritten, null, provider))
			{
				this.Position += charsWritten;
			}
			else
			{
				Append(value.ToString(null, provider));
			}
		}

		public void Append<T>(T value, string? format, IFormatProvider? provider = null) where T : ISpanFormattable
		{
			provider ??= CultureInfo.InvariantCulture;
			if (value.TryFormat(this.Chars[this.Position..], out int charsWritten, format, provider))
			{
				this.Position += charsWritten;
			}
			else
			{
				Append(value.ToString(format, provider));
			}
		}

		public void AppendLine()
		{
			AppendSlow("\r\n");
		}

		public void AppendLine(string? s)
		{
			Append(s);
			AppendSlow("\r\n");
		}

		public void AppendLine(ReadOnlySpan<char> s)
		{
			Append(s);
			AppendSlow("\r\n");
		}

		public void AppendHex(int value, int digits, bool lowerCase = false)
		{
			Contract.Positive(digits);
			Contract.Debug.Requires(digits <= 8);

			if (this.Position > this.Chars.Length - digits)
			{
				Grow(digits);
			}

			int pos = this.Position;
			ref char ptr = ref this.Chars[pos];
			int alphaBase = lowerCase ? 87 : 55;

			for (int i = digits - 1; i >= 0; i--)
			{
				int n = value & 0xF;
				Unsafe.Add(ref ptr, i) = (char) (n < 10 ? ('0' + n) : (alphaBase + n));
				value >>= 4;
			}

			this.Position = pos + digits;
		}

		public void AppendHex(long value, int digits, bool lowerCase = false)
		{
			Contract.Positive(digits);
			Contract.Debug.Requires(digits <= 16);

			if (this.Position > this.Chars.Length - digits)
			{
				Grow(digits);
			}

			int pos = this.Position;
			ref char ptr = ref this.Chars[pos];
			int alphaBase = lowerCase ? 87 : 55;

			for (int i = digits - 1; i >= 0; i--)
			{
				int n = (int) (value & 0xF);
				Unsafe.Add(ref ptr, i) = (char) (n < 10 ? ('0' + n) : (alphaBase + n));
				value >>= 4;
			}

			this.Position = pos + digits;
		}

		[MethodImpl(MethodImplOptions.NoInlining)]
		private void GrowAndAppend(char c)
		{
			Grow(1);
			Append(c);
		}

		/// <summary>
		/// Resize the internal buffer either by doubling current buffer size or
		/// by adding <paramref name="additionalCapacityBeyondPos"/> to
		/// <see cref="Position"/> whichever is greater.
		/// </summary>
		/// <param name="additionalCapacityBeyondPos">
		/// Number of chars requested beyond current position.
		/// </param>
		[MethodImpl(MethodImplOptions.NoInlining)]
		private void Grow(int additionalCapacityBeyondPos)
		{
			Contract.Debug.Assert(additionalCapacityBeyondPos > 0);
			Contract.Debug.Assert(this.Position > this.Chars.Length - additionalCapacityBeyondPos, "Grow called incorrectly, no resize is needed.");

			const uint ARRAY_MAX_LENGTH = 0x7FFFFFC7; // same as Array.MaxLength

			// Increase to at least the required size (this.Position + additionalCapacityBeyondPos), but try
			// to double the size if possible, bounding the doubling to not go beyond the max array length.
			int newCapacity = (int) Math.Max(
				(uint) (this.Position + additionalCapacityBeyondPos),
				Math.Min((uint) this.Chars.Length * 2, ARRAY_MAX_LENGTH)
			);

			// Make sure to let Rent throw an exception if the caller has a bug and the desired capacity is negative.
			// This could also go negative if the actual required length wraps around.
			char[] poolArray = ArrayPool<char>.Shared.Rent(newCapacity);

			this.Chars.Slice(0, this.Position).CopyTo(poolArray);

			char[]? toReturn = this.PooledBuffer;
			this.Chars = this.PooledBuffer = poolArray;
			if (toReturn != null)
			{
				ArrayPool<char>.Shared.Return(toReturn);
			}
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void Dispose()
		{
			char[]? toReturn = this.PooledBuffer;
			this = default; // for safety, to avoid using pooled array if this instance is erroneously appended to again
			if (toReturn != null)
			{
				ArrayPool<char>.Shared.Return(toReturn);
			}
		}

	}

}
