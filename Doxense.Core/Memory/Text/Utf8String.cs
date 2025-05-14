// adapted from https://github.com/dotnet/corefxlab/tree/master/src/System.Text.Utf8

namespace Doxense.Memory.Text
{
	using System.ComponentModel;
	using System.Globalization;
	using System.Text;
	using Doxense.Text;

	/// <summary>Represents a string that is stored as UTF-8 bytes in managed memory</summary>
	/// <remarks>This type can be used as a replacement for <see cref="string"/> in parsers that wants to reduce memory allocations</remarks>
	[DebuggerDisplay("[{Length}] {ToString()}")] //REVIEW: ToString() may be dangerous for long string or corrupted data?
	public readonly unsafe partial struct Utf8String : IFormattable, IEnumerable<UnicodeCodePoint>, IEquatable<Utf8String>, IEquatable<string>, IEquatable<Slice> //TODO: IComparable<Utf8String> !
	{
		/// <summary>Buffer that points to the UTF-8 bytes of the string in memory</summary>
		public readonly Slice Buffer;

		/// <summary>Length (in characters) of the string</summary>
		public readonly int Length;

		/// <summary>Pre-computed Hashcode of the string, or null if not known</summary>
		/// <remarks>If hashcode is known, it will be used to speed up string comparisons.</remarks>
		internal readonly int? HashCode;

		/// <summary>Utf8String ctor</summary>
		/// <param name="buffer">Buffer that contains the UTF-8 encoded bytes</param>
		/// <param name="numChars">Number of characters in the string</param>
		/// <param name="hashCode">Pre-computed hash code</param>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		internal Utf8String(Slice buffer, int numChars, int? hashCode)
		{
			Contract.Debug.Requires(buffer.Count == 0 || buffer.Array != null);
			this.Buffer = buffer;
			this.Length = numChars;
			this.HashCode = hashCode;
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Utf8String FromString(string? text, bool includeBom = false, bool noHashCode = false)
		{
			if (text == null) return default;
			return FromString(text.AsSpan(), includeBom, noHashCode);
		}

		[Pure]
		[EditorBrowsable(EditorBrowsableState.Never)]
		public static Utf8String FromString(string? text, int offset, int count, bool includeBom = false, bool noHashCode = false)
		{
			if (text == null) return count == 0 ? default : throw new ArgumentNullException(nameof(text));
			return FromString(text.AsSpan(offset, count), includeBom, noHashCode);
		}

		public static Utf8String FromString(ReadOnlySpan<char> text, bool includeBom = false, bool noHashCode = false)
		{
			if (text.Length == 0) return Utf8String.Empty;
			var bytes = includeBom ? Slice.FromStringUtf8WithBom(text) : Slice.FromStringUtf8(text);
			// we already know the length of the string, so we only need to compute the hashcode if required
			return new Utf8String(bytes, text.Length, !noHashCode ? default(int?) : ComputeHashCode(bytes, text.Length, asciiOnly: text.Length == bytes.Count));
		}

		[Pure]
		[EditorBrowsable(EditorBrowsableState.Never)]
		public static Utf8String FromString(string? text, int offset, int count, ref byte[]? buffer, bool noHashCode = false)
		{
			if (text == null) return count == 0 ? default : throw new ArgumentNullException(nameof(text));
			return FromString(text.AsSpan(offset, count), ref buffer, noHashCode);
		}

		[Pure]
		public static Utf8String FromString(ReadOnlySpan<char> text, ref byte[]? buffer, bool noHashCode = false)
		{
			if (text.Length == 0) return Utf8String.Empty;
			var bytes = Slice.FromStringUtf8(text, ref buffer, out var asciiOnly);
			// we already know the length of the string, so we only need to compute the hashcode if required
			return new Utf8String(bytes, text.Length, !noHashCode ? default(int?) : ComputeHashCode(bytes, text.Length, asciiOnly));
		}

		/// <summary>Return a string view of a native buffer that contains UTF-8 bytes</summary>
		/// <param name="buffer">Bytes that contain UTF-8 encoded characters</param>
		/// <param name="discardBom">If true, discard any UTF-8 BOM if present</param>
		/// <param name="noHashCode">If false, pre-compute the hashcode of the string. If you will not use this string for comparisons or as a key in a dictionary, you can skip this step by passing true.</param>
		/// <returns><see cref="Utf8String"/> that maps to the corresponding <paramref name="buffer"/></returns>
		/// <remarks>This method needs to compute the length (and hashcode) of the string. You should cache the result if you need it more than once.</remarks>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Utf8String FromBuffer(Slice buffer, bool discardBom = false, bool noHashCode = false)
		{
			return buffer.IsNull ? default(Utf8String)
		         : buffer.IsEmpty ? Utf8String.Empty
			     : FromBuffer(buffer.Array, buffer.Offset, buffer.Count, discardBom, noHashCode);
		}

		/// <summary>Return a string view of a native buffer that contains UTF-8 bytes</summary>
		/// <param name="buffer">Buffer that contains UTF-8 encoded characters</param>
		/// <param name="offset">Offset (in bytes) of the start of the string in <paramref name="buffer"/></param>
		/// <param name="count">Size (in bytes) of the string in <paramref name="buffer"/></param>
		/// <param name="discardBom">If true, discard any UTF-8 BOM if present</param>
		/// <param name="noHashCode">If false, pre-compute the hashcode of the string. If you will not use this string for comparisons or as a key in a dictionary, you can skip this step by passing true.</param>
		/// <returns><see cref="Utf8String"/> that maps to the corresponding <paramref name="buffer"/></returns>
		/// <remarks>This method needs to compute the length (and hashcode) of the string. You should cache the result if you need it more than once.</remarks>
		[Pure]
		public static Utf8String FromBuffer(byte[] buffer, int offset, int count, bool discardBom = false, bool noHashCode = false)
		{
			Contract.DoesNotOverflow(buffer, offset, count);

			if (discardBom && count >= 3)
			{
				// If the buffer starts with EF BB BF, then discard it
				if (buffer[offset] == 0xEF && buffer[offset + 1] == 0xBB && buffer[offset + 2] == 0xBF)
				{
					offset += 3;
					count -= 3;
				}
			}

			if (count == 0) return Utf8String.Empty;

			int? hashCode;
			int length;
			fixed (byte* ptr = &buffer[offset])
			{
				if (noHashCode)
				{ // we don't need the hashcode, only get the length
					if (!Utf8Encoder.TryGetLength(ptr, ptr + count, out length))
					{
						throw new DecoderFallbackException("Unable to translate invalid UTF-8 code point in string"); //TODO: better message
					}
					hashCode = null;
				}
				else
				{ // compute both length and hashcode at the same time
					if (!Utf8Encoder.TryGetLengthAndHashCode(ptr, ptr + count, out length, out int h))
					{
						throw new DecoderFallbackException("Unable to translate invalid UTF-8 code point in string"); //TODO: better message
					}
					hashCode = h;
				}
			}
			return new Utf8String(new Slice(buffer, offset, count), length, hashCode);
		}

		[Pure]
		public static Utf8String CreateUnsafe(Slice buffer, int length, int? hashCode)
		{
			Contract.Debug.Requires((buffer.Count == 0 || buffer.Array != null) && length >= 0 && buffer.Count >= length);
			return new Utf8String(buffer, length, hashCode);
		}

		/// <summary>Truncate the UTF-8 BOM prefix from a buffer, it is exists</summary>
		[Pure]
		public static Slice RemoveBom(Slice buffer)
		{
			// If the buffer starts with EF BB BF, then discard it
			if (buffer.Count >= 3 && buffer[0] == 0xEF && buffer[1] == 0xBB && buffer[2] == 0xBF)
			{
				return buffer.Substring(3);
			}
			return buffer;
		}

		/// <summary>Truncate the UTF-8 BOM prefix from a buffer, it is exists</summary>
		[Pure]
		public static ReadOnlySpan<byte> RemoveBom(ReadOnlySpan<byte> buffer)
		{
			// If the buffer starts with EF BB BF, then discard it
			if (buffer.Length >= 3 && buffer[0] == 0xEF && buffer[1] == 0xBB && buffer[2] == 0xBF)
			{
				return buffer.Slice(3);
			}
			return buffer;
		}

		/// <summary>
		/// Returns a reference to the first byte in the encoded string.
		/// If the string is empty, returns a reference to the location where the first byte would have been stored.
		/// Such a reference can be used for pinning but must never be dereferenced.
		/// </summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public ref readonly byte DangerousGetPinnableReference()
		{
			return ref this.Buffer.DangerousGetPinnableReference();
		}

		/// <summary>
		/// Returns a reference to the first character in the string. If the string is empty, returns null reference.
		/// </summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public ref readonly byte GetPinnableReference()
		{
			return ref this.Buffer.GetPinnableReference();
		}

		public static readonly Utf8String Nil = default;

		public static readonly Utf8String Empty = new(Slice.Empty, 0, 0);

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static explicit operator string(Utf8String s)
		{
			return s.ToString();
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static explicit operator Slice(Utf8String s)
		{
			return s.Buffer;
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static explicit operator ReadOnlySpan<byte>(Utf8String s)
		{
			return s.Buffer.Span;
		}

		/// <summary>Test if this string is null</summary>
		public bool IsNull => this.Buffer.IsNull;

		/// <summary>Test if this string is null or empty</summary>
		public bool IsNullOrEmpty => this.Length == 0;

		/// <summary>Test if this string only contains ASCII characters</summary>
		public bool IsAscii => this.Buffer.Count == this.Length;

		public override bool Equals(object? obj)
		{
			switch (obj)
			{
				case string s:
					return Equals(s);
				case Utf8String utf8:
					return Equals(utf8);
				case Slice sl:
					return Equals(sl);
			}
			return false;
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool Equals(Utf8String other)
		{
			return this.Length == other.Length
			    && SameOrMissingHashcode(this.HashCode, other.HashCode)
			    && this.Buffer.Equals(other.Buffer);
		}

		/// <summary>Test that both hash codes, if present, have the same value</summary>
		/// <returns>False IIF h1 != nul &amp;&amp; h2 != null &amp;&amp; h1 != h2; otherwise, True</returns>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		private static bool SameOrMissingHashcode(int? h1, int? h2)
		{
			return !h1.HasValue || !h2.HasValue || h1.Value == h2.Value;
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool Equals(ReadOnlySpan<byte> other)
		{
			return this.Span.SequenceEqual(other);
		}

		[Pure]
		public bool Equals(string? other)
		{
			if (string.IsNullOrEmpty(other)) return this.Length == 0;
			if (other.Length != this.Length) return false;
			using (var it = GetEnumerator())
			{
				foreach (var ch in other)
				{
					if (!it.MoveNext()) goto unexpected; // something's wrong, captain!
					if (ch != (char) it.Current) return false;
				}
			}
			return true;
		unexpected:
			throw new InvalidOperationException();
		}

		[Pure]
		public bool Equals(ReadOnlySpan<char> other)
		{
			if (other.Length == 0) return this.Length == 0;
			if (other.Length != this.Length) return false;
			using (var it = GetEnumerator())
			{
				foreach (var ch in other)
				{
					if (!it.MoveNext()) goto unexpected; // something's wrong, captain!
					if (ch != (char) it.Current) return false;
				}
			}
			return true; 
		unexpected:
			throw new InvalidOperationException();
		}

		public bool Equals(ArraySegment<char> other)
		{
			return Equals(other.AsSpan());
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool Equals(Slice other)
		{
			return other.Equals(this.Buffer);
		}

		public static bool Equals(ReadOnlySpan<byte> left, string right)
		{
			fixed (byte* start = left)
			{
				byte* ptr = start;
				byte* end = start + left.Length;
				foreach (var ch in right)
				{
					if (!Utf8Encoder.TryDecodeCodePoint(ptr, end, out var cp, out var len))
					{
						return false;
					}
					if ((char) cp != ch) return false;
					ptr += len;
				}
				return ptr >= end;
			}
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator ==(Utf8String a, Utf8String b)
		{
			return a.Equals(b);
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator !=(Utf8String a, Utf8String b)
		{
			return !a.Equals(b);
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator ==(Utf8String a, string b)
		{
			return a.Equals(b);
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator !=(Utf8String a, string b)
		{
			return !a.Equals(b);
		}

		/// <summary>Returns the hashcode of the string only if it has been pre-computed; otherwise, returns 0.</summary>
		/// <remarks>Only use this method if the cost of computing the hashcode would be too high</remarks>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public int GetCachedHashCode()
		{
			return this.HashCode ?? 0;
		}

		/// <summary>Returns the hashcode of the string</summary>
		public override int GetHashCode()
		{
			// ReSharper disable once NonReadonlyMemberInGetHashCode
			return this.HashCode ?? ComputeHashCode(this.Buffer, this.Length, this.IsAscii);
		}

		[Pure]
		internal static int ComputeHashCode(Slice buffer, int length, bool asciiOnly)
		{
			var it = new Enumerator(buffer, asciiOnly);
			uint h = 0;
			while (it.MoveNext())
			{
				h = UnicodeCodePoint.ContinueHashCode(h, it.Current);
			}
			return UnicodeCodePoint.CompleteHashCode(h, length);
		}

		public override string ToString()
		{
			return ToString(this.Buffer, this.Length, this.IsAscii);
		}

		[Pure]
		internal static string ToString(Slice buffer, [Positive] int length, bool asciiOnly = false)
		{
			if (length == 0) return String.Empty;

			if (asciiOnly)
			{ // ASCII only, direct conversion
				Contract.Debug.Requires((uint) length <= buffer.Count);
				fixed (byte* ptr = &buffer.DangerousGetPinnableReference())
				{
					return new string((sbyte*) ptr, 0, length);
				}
			}

			var str = new string('\0', length);
			fixed (char* pChars = str)
			{
				char* res = pChars;
				char* stop = pChars + length;
				var it = new Enumerator(buffer);
				while(it.MoveNext())
				{
					if (res >= stop) throw new InvalidOperationException();
					*res++ = (char) it.Current.Value;
				}
			}
			return str;
		}

		internal static string ToStringPrintable(Slice buffer, int length, char quotes = '\0')
		{
			if (length == 0)
			{
				return buffer.IsNull ? "null"
					: quotes == 0 ? string.Empty
					: new string(quotes, 2);
			}

			var sb = new StringBuilder(length + (length >> 2) + (quotes != 0 ? 2 : 0));
			var it = new Enumerator(buffer, asciiOnly: false);
			if (quotes != 0) sb.Append(quotes);
			while (it.MoveNext())
			{
				char c = (char) it.Current.Value;
				if (c == '\\')
				{
					sb.Append(@"\\");
				}
				else if (c == quotes && quotes != 0)
				{
					sb.Append('\\').Append(c);
				}
				else if ((c >= ' ' && c <= '~') || (c >= 880 && c <= 2047) || (c >= 12352 && c <= 12591))
				{
					sb.Append(c);
				}
				else if (c == 0)
				{
					sb.Append(@"\0");
				}
				else if (c == '\n')
				{
					sb.Append(@"\n");
				}
				else if (c == '\r')
				{
					sb.Append(@"\r");
				}
				else if (c == '\t')
				{
					sb.Append(@"\t");
				}
				else if (c > 255)
					sb.Append(@"\u").Append(((int) c).ToString("x4", CultureInfo.InvariantCulture));
				else // pas clean!
					sb.Append(@"\x").Append(((int) c).ToString("x2", CultureInfo.InvariantCulture));
			}
			if (quotes != 0) sb.Append(quotes);
			return sb.ToString();
		}

		public string ToString(string? fmt)
		{
			return ToString(fmt, null);
		}

		public string ToString(string? fmt, IFormatProvider? provider)
		{
			switch (fmt ?? "D")
			{
				case "D":
				case "d":
				{
					return ToString(this.Buffer, this.Length, this.IsAscii);
				}
				case "P":
				{
					return ToStringPrintable(this.Buffer, this.Length, quotes: '"');
				}
				default:
				{
					throw new FormatException("Format is invalid or not supported");
				}
			}
		}

		/// <summary>Return an array with the decoded characters of this string</summary>
		[Pure]
		public char[] ToCharArray()
		{
			if (this.Length == 0)
			{
				return [ ];
			}

			var res = new char[this.Length];
			int p = 0;
			foreach (var cp in this)
			{
				res[p++] = (char) cp;
			}
			Contract.Debug.Ensures(p == res.Length);
			return res;
		}

		/// <summary>Return an <see cref="Slice"/> that points to the UTF-8 encoded bytes of this string</summary>
		/// <remarks>CAUTION: you should NOT mutate the content of the buffer. Doing so will invalidate the pre-computed <see cref="Length"/> and <see cref="HashCode"/> and potentially generate corrupted data.</remarks>
		[Pure]
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public Slice GetBuffer()
		{
			return this.Buffer;
		}

		/// <summary>Return an array with a copy of the UTF-8 encoded bytes of this string</summary>
		[Pure]
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public byte[] GetBytes()
		{
			return this.Buffer.ToArray();
		}

		/// <summary>Return a span over the UTF-8 encoded bytes of this string</summary>
		/// <remarks>The length of the span may be greater than the <see cref="Length"/> of this string, but nether smaller.</remarks>
		public ReadOnlySpan<byte> Span
		{
			[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
			get => this.Buffer.Span;
		}

		/// <summary>Return a substring that shares the same buffer as this one</summary>
		[Pure]
		public Utf8String Substring(int startIndex, bool noHashCode = false)
		{
			if (startIndex == 0) return this;
			if (startIndex == this.Length) return Utf8String.Empty;
			if ((uint) startIndex > this.Length) ThrowHelper.ThrowArgumentOutOfRangeException(nameof(startIndex));

			int count = this.Length - startIndex;
			Slice buf;
			int? hashCode;

			if (this.IsAscii)
			{ // FAST: we can directly compute the offset
				buf = this.Buffer.Substring(startIndex);
				hashCode = noHashCode ? default(int?) : UnicodeCodePoint.ComputeHashCodeAscii(buf.Span);
			}
			else
			{ // SLOW: we need to iterate the codepoints to know where the substring starts in memory
				var buffer = this.Buffer;
				using (var it = GetEnumerator())
				{
					for (int i = 0; i <= startIndex; i++)
					{
						if (!it.MoveNext()) throw new InvalidOperationException(); // something's wrong, captain!
					}
					buf = buffer.Substring(it.ByteOffset);
					hashCode = noHashCode ? default(int?) : UnicodeCodePoint.ComputeHashCodeUtf8(buf.Span, count);
				}
			}
			return new Utf8String(buf, count, hashCode);
		}

		/// <summary>Return a substring that shares the same buffer as this one</summary>
		[Pure]
		public Utf8String Substring(int startIndex, int count, bool noHashCode = false)
		{
			if (startIndex == 0 && count == this.Length) return this;
			if (startIndex == this.Length && count == 0) return Utf8String.Empty;
			Contract.DoesNotOverflow(this.Length, startIndex, count);

			Slice segment;
			int? hashCode;


			if (this.IsAscii)
			{ // FAST: we can directly compute the offset
				segment = this.Buffer.Substring(startIndex, count);
				hashCode = noHashCode ? default(int?) : UnicodeCodePoint.ComputeHashCodeAscii(segment.Span);
			}
			else
			{ // SLOW: we need to iterate the codepoints to know where the substring starts in memory

				// since there are non-ASCII code points, we will need to walk the string and count characters,
				// in order to determine where we need to cut the underlying bytes buffers

				var buffer = this.Buffer;

				using (var it = GetEnumerator())
				{
					for (int i = 0; i <= startIndex; i++)
					{
						if (!it.MoveNext()) throw new InvalidOperationException(); // something's wrong, captain!
					}
					int from = it.ByteOffset;
					if (count == this.Length - startIndex)
					{ // the rest of the string contains only ASCII
						segment = buffer.Substring(from);
						hashCode = noHashCode ? default(int?) : UnicodeCodePoint.ComputeHashCodeUtf8(segment.Span, count);
					}
					else
					{ // the rest of the string contains some Unicode code points
						// we must compute how many bytes to take to get the next 'count' characters
						uint h = 0;
						for (int i = 0; i < count; i++)
						{
							if (!it.MoveNext()) throw new InvalidOperationException(); // something's wrong, captain!
							h = UnicodeCodePoint.ContinueHashCode(h, it.Current);
						}
						int to = it.ByteOffset;
						segment = buffer.Substring(from, to - from);
						hashCode = noHashCode ? default(int?) : UnicodeCodePoint.CompleteHashCode(h, count);
					}
				}
			}

			return new Utf8String(segment, count, hashCode);
		}

		/// <summary>Test if this string starts with the given character</summary>
		public bool StartsWith(char value)
		{
			if (this.Length == 0) return false;

			// if the first byte is ASCII, then the check is easy
			int first = this.Buffer[0];
			if (first < 128) return (char) first == value;

			// need to decode the codepoint
			using (var it = GetEnumerator())
			{
				return it.MoveNext() && it.Current.ToChar() == value;
			}
		}

		/// <summary>Test if this string starts with the given prefix</summary>
		/// <remarks>
		/// This method is O(<paramref name="prefix"/>.Length).
		/// Convention is that all strings start with the null or empty prefix.
		/// </remarks>
		[Pure]
		public bool StartsWith(ReadOnlySpan<char> prefix)
		{
			if (prefix.Length == 0) return true;
			if (prefix.Length > this.Length) return false;


			if (prefix.Length == 1)
			{ // fast path for single-char prefix (ex: StartsWith("/"))
				int first = this.Buffer[0];
				if (first < 128) return first == prefix[0];
			}

			fixed (char* chars = prefix)
			{
				char* inp = chars;
				char* stop = inp + prefix.Length;
				foreach (var cp in this)
				{
					if (*inp++ != (char) cp) return false;
					if (inp >= stop) break;
				}
			}
			return true;
		}

		/// <summary>Test if this string starts with the given prefix</summary>
		/// <remarks>
		/// This method is O(<paramref name="prefix"/>.Length).
		/// Convention is that all strings start with the null or empty prefix.
		/// </remarks>
		[Pure]
		public bool StartsWith(string prefix)
		{
			if (string.IsNullOrEmpty(prefix)) return true;
			if (prefix.Length > this.Length) return false;


			if (prefix.Length == 1)
			{ // fast path for single-char prefix (ex: StartsWith("/"))
				int first = this.Buffer[0];
				if (first < 128) return first == prefix[0];
			}

			fixed (char* chars = prefix)
			{
				char* inp = chars;
				char* stop = inp + prefix.Length;
				foreach (var cp in this)
				{
					if (*inp++ != (char) cp) return false;
					if (inp >= stop) break;
				}
			}
			return true;
		}

		/// <summary>Test if this string starts with the given prefix</summary>
		/// <remarks>
		/// This method is O(<paramref name="count"/>).
		/// Convention is that all strings start with the empty prefix.
		/// </remarks>
		[Pure]
		public bool StartsWith(string prefix, int offset, int count)
		{
			return StartsWith(prefix.AsSpan(offset, count));
		}

		/// <summary>Test if this string starts with the given prefix</summary>
		/// <remarks>
		/// This method is O(<paramref name="prefix"/>.Length).
		/// Convention is that all strings start with the null or empty prefix.
		/// </remarks>
		[Pure]
		public bool StartsWith(Utf8String prefix)
		{
			if (prefix.Length == 0) return true;
			if (prefix.Length > this.Length) return false;

			if (this.IsAscii && prefix.IsAscii)
			{ // fast path
				return this.Span.Slice(0, prefix.Length).SequenceEqual(prefix.Span);
			}

			using (var it = GetEnumerator())
			{
				foreach (var cp in prefix)
				{
					if (!it.MoveNext()) throw new InvalidOperationException(); // something's wrong, captain!
					if (it.Current != cp) return false;
				}
			}
			return true;
		}

		/// <summary>Test if this string ends with the given suffix</summary>
		/// <remarks>
		/// Warning: This method is O(this.Length), which can be a lot larger than the length of the suffix!
		/// Convention is that all strings end with the null or empty suffix.
		/// </remarks>
		public bool EndsWith(string suffix)
		{
			if (string.IsNullOrEmpty(suffix)) return true;
			if (suffix.Length > this.Length) return false;

			//note: we defer to Substring(..) the task of extracting the suffix from the underlying buffer
			return Substring(this.Length - suffix.Length).Equals(suffix);
		}

		/// <summary>Test if this string ends with the given suffix</summary>
		/// <remarks>
		/// Warning: This method is O(this.Length), which can be a lot larger than the length of the suffix!
		/// Convention is that all strings end with the null or empty suffix.
		/// </remarks>
		public bool EndsWith(ReadOnlySpan<char> suffix)
		{
			if (suffix.Length == 0) return true;
			if (suffix.Length > this.Length) return false;

			//note: we defer to Substring(..) the task of extracting the suffix from the underlying buffer
			return Substring(this.Length - suffix.Length).Equals(suffix);
		}

		/// <summary>Test if this string ends with the given segment of a suffix</summary>
		/// <param name="suffix">String that contains the suffix</param>
		/// <param name="offset">Offset in <paramref name="suffix"/> of the first character of the suffix</param>
		/// <param name="count">Size of the suffix</param>
		/// <remarks>
		/// Warning: This method is O(this.Length), which can be a lot larger than the length of the suffix!
		/// Convention is that all strings end with the null or empty suffix.
		/// </remarks>
		[Pure]
		public bool EndsWith(string suffix, int offset, int count)
		{
			return EndsWith(suffix.AsSpan(offset, count));
		}

		/// <summary>Test if this string ends with the given suffix</summary>
		/// <remarks>
		/// Warning: This method is O(this.Length), which can be a lot larger than the length of the suffix!
		/// Convention is that all strings end with the null or empty suffix.
		/// </remarks>
		public bool EndsWith(Utf8String suffix)
		{
			if (suffix.IsNullOrEmpty) return true;
			if (suffix.Length > this.Length) return false;

			//note: we defer to Substring(..) the task of extracting the suffix from the underlying buffer
			return Substring(this.Length - suffix.Length).Equals(suffix);
		}

		[Pure]
		public bool Contains(char ch)
		{
			return IndexOf(ch) >= 0;
		}

		[Pure]
		public bool Contains(char ch, [Positive] int startIndex)
		{
			return IndexOf(ch, startIndex) >= 0;
		}

		//TODO: Contains(string) + offset,count
		//TODO: Contains(Utf8String)

		[Pure]
		public int IndexOf(char ch)
		{
			const int NOT_FOUND = -1;

			if (this.Length <= 0) return NOT_FOUND;

			if (this.IsAscii)
			{ // ASCII string: we just need to find the corresponding byte, and its offset will be the position (in characters)
				if (ch > 0x7F)
				{ // by convention, if the string is ASCII-only, it cannot contain a character >= 0x80)
					return NOT_FOUND;
				}
				// find the byte in the buffer
				return this.Buffer.IndexOf((byte) ch);
			}

			// UTF8 string: we need to scan the code points, looking for a match
			int p = 0;
			foreach (var cp in this)
			{
				if (cp.Value == ch) return p;
				++p;

			}
			return NOT_FOUND;
		}

		[Pure]
		public int IndexOf(char ch, [Positive] int startIndex)
		{
			const int NOT_FOUND = -1;

			if ((uint) startIndex >= this.Length) throw new ArgumentException("Start index is outside the string", nameof(startIndex));

			int count = this.Length - startIndex;
			if (count <= 0) return NOT_FOUND;

			if (this.IsAscii)
			{ // ASCII string: we just need to find the corresponding byte, and its offset will be the position (in characters)
				if (ch > 0x7F)
				{ // by convention, if the string is ASCII-only, it cannot contain a character >= 0x80)
					return NOT_FOUND;
				}
				// find the byte in the buffer
				return this.Buffer.IndexOf((byte)ch, startIndex);
			}

			// UTF8 string: we need to scan the code points, looking for a match
			int p = 0;
			int skip = startIndex;
			foreach (var cp in this)
			{
				if (skip > 0) { --skip; continue; }
				if (cp.Value == ch) return checked(startIndex + p);
				++p;

			}
			return NOT_FOUND;
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public Utf8String Concat(Utf8String b)
		{
			return new Utf8String(this.Buffer.Concat(b.Buffer), this.Length + b.Length, null);
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Utf8String operator +(Utf8String a, Utf8String b)
		{
			return a.Concat(b);
		}

		[Pure]
		public Utf8String Concat(string b)
		{
			if (string.IsNullOrEmpty(b)) return this;
			if (this.Length == 0) return FromString(b);

			int len = Slice.Utf8NoBomEncoding.GetByteCount(b);
			byte[] tmp = new byte[checked(this.Buffer.Count + len)];
			this.Buffer.CopyTo(tmp, 0);

			int n = Slice.Utf8NoBomEncoding.GetBytes(b, 0, b.Length, tmp, this.Buffer.Count);
			Contract.Debug.Assert(n == len);
			return new Utf8String(new Slice(tmp, 0, tmp.Length), this.Length + b.Length, null);
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Utf8String operator +(Utf8String a, string b)
		{
			return a.Concat(b);
		}

		//TODO: IndexOf(string) + offset,count
		//TODO: IndexOf(Utf8String)

		//TODO: LastIndexOf(..)
	}

}
