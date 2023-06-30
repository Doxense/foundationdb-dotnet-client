// adapted from https://github.com/dotnet/corefxlab/tree/master/src/System.Text.Utf8/System/Text/Utf8

namespace Doxense.Memory.Text
{
	using System;
	using System.Collections;
	using System.Collections.Generic;
	using System.Diagnostics;
	using System.Runtime.CompilerServices;
	using Doxense.Diagnostics.Contracts;
	using Doxense.Text;
	using JetBrains.Annotations;

	[DebuggerDisplay("Length={Length}, Size={Buffer.Count}, HashCode=0x{HashCode,h}")]
	public partial struct Utf8String
	{

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

		public struct Enumerator : IEnumerator<UnicodeCodePoint>
		{
			private const int INIT = -1;
			private const int EOF = -2;

			private readonly Slice Buffer;
			public readonly bool AsciiOnly;
			private int Index;
			private int Offset;

			public Enumerator(Slice buffer, bool asciiOnly = false)
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

			public void Reset()
			{
				this.Index = INIT;
				this.Offset = 0;
				this.Current = default(UnicodeCodePoint);
			}

			public Slice GetBuffer() => this.Buffer;

			public int ByteOffset => this.Offset;

			public int ByteRemaining => this.Buffer.Count - this.Offset;

			public UnicodeCodePoint Current { get; private set; }

			object IEnumerator.Current => this.Current;
		}

		public ref struct SpanEnumerator
		{
			private const int INIT = -1;
			private const int EOF = -2;

			private readonly ReadOnlySpan<byte> Buffer;
			public readonly bool AsciiOnly;
			private int Index;
			private int Offset;

			public SpanEnumerator(ReadOnlySpan<byte> buffer, bool asciiOnly = false)
			{
				this.Buffer = buffer;
				this.AsciiOnly = asciiOnly;
				this.Current = default;
				this.Index = INIT;
				this.Offset = 0;
			}

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
					if (!Utf8Encoder.TryDecodeCodePoint(this.Buffer.Slice(index), out var current, out len))
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

			public void Reset()
			{
				this.Index = INIT;
				this.Offset = 0;
				this.Current = default;
			}

			public ReadOnlySpan<byte> GetBuffer() => this.Buffer;

			public int ByteOffset => this.Offset;

			public int ByteRemaining => this.Buffer.Length - this.Offset;

			public UnicodeCodePoint Current { get; private set; }

		}
	}
}
