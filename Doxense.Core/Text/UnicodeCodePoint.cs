// adapted from https://github.com/dotnet/corefxlab/tree/master/src/System.Text.Primitives/System/Text/Encoding/Utf8

namespace SnowBank.Text
{
	using System.Globalization;
	using SnowBank.Buffers.Text;

	[DebuggerDisplay("{ToChar()}")]
	[PublicAPI]
	public readonly struct UnicodeCodePoint
	{

		public readonly uint Value;

		public UnicodeCodePoint(uint value)
		{
			this.Value = value;
		}

		public static explicit operator uint(UnicodeCodePoint codePoint) => codePoint.Value;
		public static explicit operator char(UnicodeCodePoint codePoint) => (char) codePoint.Value;
		public static explicit operator UnicodeCodePoint(uint value) => new(value);
		public static explicit operator UnicodeCodePoint(int value) => new(unchecked((uint) value));
		public static explicit operator UnicodeCodePoint(char value) => new(value);

		/// <summary>Gets the equivalent character for this code point</summary>
		/// <returns></returns>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public char ToChar()
		{
			return (char) this.Value;
		}

		/// <summary>Gets the Unicode category of this code point</summary>
		[Pure]
		public UnicodeCategory GetCategory()
		{
			return CharUnicodeInfo.GetUnicodeCategory((char) this.Value);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator ==(UnicodeCodePoint x, UnicodeCodePoint y)
		{
			return x.Value == y.Value;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator !=(UnicodeCodePoint x, UnicodeCodePoint y)
		{
			return x.Value != y.Value;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool Equals(UnicodeCodePoint other)
		{
			return this.Value == other.Value;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool Equals(uint other)
		{
			return this.Value == other;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool Equals(char other)
		{
			return this.Value == other;
		}

		public override bool Equals(object? obj)
		{
			return obj is UnicodeCodePoint cp && cp.Value == this.Value;
		}
		public override int GetHashCode()
		{
			return this.Value.GetHashCode();
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public UnicodeCodePoint ToLower()
		{
			return (UnicodeCodePoint) char.ToLowerInvariant((char) this.Value);
		}

		[Pure]
		public static bool IsSurrogate(UnicodeCodePoint codePoint)
		{
			return codePoint.Value >= UnicodeConstants.Utf16SurrogateRangeStart && codePoint.Value <= UnicodeConstants.Utf16SurrogateRangeEnd;
		}

		//TODO: REVIEW: decide on a real hash function to compute string hash codes!

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		internal static uint ContinueHashCode(uint h, UnicodeCodePoint cp)
		{
			return (h * 31) ^ cp.Value;
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		internal static int CompleteHashCode(uint h, int len)
		{
			return (int) (h * 31) ^ len;
		}

		/// <summary>Compute the hashcode of a string in memory</summary>
		/// <param name="s">String in memory</param>
		/// <returns>Hashcode of the string</returns>
		[Pure]
		public static int ComputeHashCode(string s)
		{
			if (string.IsNullOrEmpty(s)) return 0;
			uint h = 0;
			foreach (var c in s)
			{
				h = ContinueHashCode(h, new UnicodeCodePoint(c));
			}
			return CompleteHashCode(h, s.Length);
		}

		/// <summary>Compute the hashcode of a sub-section of a string in memory</summary>
		/// <param name="s">String in memory</param>
		/// <param name="offset">Start of the substring</param>
		/// <param name="count">Length of the substring</param>
		/// <returns>Hashcode of the substring</returns>
		[Pure]
		public static int ComputeHashCode(string s, int offset, int count)
		{
			if (count == 0) return 0;
			Contract.DoesNotOverflow(s, offset, count);

			uint h = 0;
			unsafe
			{
				fixed (char* chars = s)
				{
					char* inp = chars + offset;
					for (int i = 0; i < count; i++)
					{
						h = ContinueHashCode(h, new UnicodeCodePoint(*inp++));
					}
				}
				return CompleteHashCode(h, s.Length);
			}
		}

		/// <summary>Compute the hashcode of a buffer known to contain UTF-8 encoded code points</summary>
		/// <param name="buffer">Buffer that contains UTF-8 bytes</param>
		/// <param name="length">Length (in characters) of the corresponding string</param>
		/// <returns>Hashcode of the string</returns>
		[Pure]
		public static int ComputeHashCodeUtf8(ReadOnlySpan<byte> buffer, int length)
		{
			uint h = 0;
			var it = new Utf8String.SpanEnumerator(buffer);
			while (it.MoveNext())
			{
				h = ContinueHashCode(h, it.Current);
			}
			return CompleteHashCode(h, length);
		}

		/// <summary>Compute the hashcode of a buffer known to contain only ASCII characters</summary>
		/// <param name="buffer">Buffer that contains ASCII bytes</param>
		/// <returns>Hashcode of the string</returns>
		[Pure]
		public static unsafe int ComputeHashCodeAscii(ReadOnlySpan<byte> buffer)
		{
			uint h = 0;
			fixed (byte* ptr = buffer)
			{
				byte* inp = ptr;
				byte* stop = ptr + buffer.Length;
				while (inp < stop)
				{
					h = ContinueHashCode(h, (UnicodeCodePoint) (char) *inp++);
				}
				return CompleteHashCode(h, buffer.Length);
			}
		}

	}

}
