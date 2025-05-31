// adapted from https://github.com/dotnet/corefxlab/tree/master/src/System.Text.Utf8/System/Text/Utf8

namespace SnowBank.Buffers.Text
{
	using System.Collections;
	using SnowBank.Text;

	[DebuggerDisplay("Length={Length}, Size={Buffer.Count}, HashCode=0x{HashCode,h}")]
	public partial struct Utf8String
	{

		/// <summary>Returns an enumerator that will list all the <see cref="UnicodeCodePoint"/> in this string</summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public Enumerator GetEnumerator()
		{
			return new Enumerator(this.Buffer, this.IsAscii);
		}

		IEnumerator<UnicodeCodePoint> IEnumerable<UnicodeCodePoint>.GetEnumerator()
		{
			return this.GetEnumerator();
		}

		IEnumerator IEnumerable.GetEnumerator()
		{
			return this.GetEnumerator();
		}

		/// <summary>Enumerator that lists all the <see cref="UnicodeCodePoint"/> in a <see cref="Utf8String"/></summary>
		public struct Enumerator : IEnumerator<UnicodeCodePoint>
		{
			private const int INIT = -1;
			private const int EOF = -2;

			private readonly Slice Buffer;
			private readonly bool AsciiOnly;
			private int Index;
			private int Offset;

			internal Enumerator(Slice buffer, bool asciiOnly = false)
			{
				Contract.Debug.Requires(buffer.Count == 0 || buffer.Array != null);
				this.Buffer = buffer;
				this.AsciiOnly = asciiOnly;
				this.Current = default(UnicodeCodePoint);
				this.Index = INIT;
				this.Offset = 0;
			}

			void IDisposable.Dispose()
			{
			}

			/// <inheritdoc />
			public bool MoveNext()
			{
				int index = this.Index + 1;
				if (index >= this.Buffer.Count) return MoveNextRare();

				int len;
				this.Offset = index;
				var next = this.Buffer[index];
				if (this.AsciiOnly || next < 0x80)
				{
					this.Current = (UnicodeCodePoint) (char) next;
					len = 1;
				}
				else
				{
					if (!Utf8Encoder.TryDecodeCodePoint(this.Buffer.Substring(index), out var current, out len))
					{
						return MoveNextRare();
					}
					this.Current = current;
				}
				this.Index = index + len - 1;
				return true;
			}

			private bool MoveNextRare()
			{
				this.Index = EOF;
				this.Offset = this.Buffer.Count;
				this.Current = default(UnicodeCodePoint);
				return false;
			}

			/// <inheritdoc />
			public void Reset()
			{
				this.Index = INIT;
				this.Offset = 0;
				this.Current = default(UnicodeCodePoint);
			}

			/// <summary>UTF-8 buffer of the string</summary>
			public Slice GetBuffer() => this.Buffer;

			/// <summary>Current offset in the UTF-8 buffer</summary>
			public int ByteOffset => this.Offset;

			/// <summary>Number of bytes remaining in the UTF-8 buffer</summary>
			public int ByteRemaining => this.Buffer.Count - this.Offset;

			/// <summary>Current code point</summary>
			public UnicodeCodePoint Current { get; private set; }

			/// <inheritdoc />
			object IEnumerator.Current => this.Current;

		}

		/// <summary>Enumerator that lists all the <see cref="UnicodeCodePoint"/> in a <see cref="Utf8String"/></summary>
		public ref struct SpanEnumerator
#if NET9_0_OR_GREATER
			: IEnumerator<UnicodeCodePoint>
#endif
		{
			private const int INIT = -1;
			private const int EOF = -2;

			private readonly ReadOnlySpan<byte> Buffer;
			private readonly bool AsciiOnly;
			private int Index;
			private int Offset;

			internal SpanEnumerator(ReadOnlySpan<byte> buffer, bool asciiOnly = false)
			{
				this.Buffer = buffer;
				this.AsciiOnly = asciiOnly;
				this.Current = default;
				this.Index = INIT;
				this.Offset = 0;
			}

			/// <inheritdoc />
			public bool MoveNext()
			{
				int index = this.Index + 1;
				if (index >= this.Buffer.Length) return MoveNextRare();

				int len;
				this.Offset = index;
				var next = this.Buffer[index];
				if (this.AsciiOnly || next < 0x80)
				{
					this.Current = (UnicodeCodePoint) (char) next;
					len = 1;
				}
				else
				{
					if (!Utf8Encoder.TryDecodeCodePoint(this.Buffer[index..], out var current, out len))
					{
						return MoveNextRare();
					}
					this.Current = current;
				}
				this.Index = index + len - 1;
				return true;
			}

			private bool MoveNextRare()
			{
				this.Index = EOF;
				this.Offset = this.Buffer.Length;
				this.Current = default;
				return false;
			}

			/// <inheritdoc />
			public void Reset()
			{
				this.Index = INIT;
				this.Offset = 0;
				this.Current = default;
			}

			/// <summary>UTF-8 buffer of the string</summary>
			public ReadOnlySpan<byte> GetBuffer() => this.Buffer;

			/// <summary>Current offset in the UTF-8 buffer</summary>
			public int ByteOffset => this.Offset;

			/// <summary>Number of bytes remaining in the UTF-8 buffer</summary>
			public int ByteRemaining => this.Buffer.Length - this.Offset;

			/// <inheritdoc />
			public UnicodeCodePoint Current { get; private set; }

#if NET9_0_OR_GREATER

			void IDisposable.Dispose()
			{
			}

			/// <inheritdoc />
			object IEnumerator.Current => this.Current;

#endif

		}

	}

}
