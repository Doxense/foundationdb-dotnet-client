#region Copyright (c) 2023-2024 SnowBank SAS, (c) 2005-2023 Doxense SAS
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

namespace Doxense.Collections.Tuples.Encoding
{
	using System.Diagnostics;
	using System.Diagnostics.CodeAnalysis;
	using System.Runtime.CompilerServices;
	using Doxense.Memory;

	[DebuggerDisplay("{Cursor}/{Input.Length} @ {Depth}")]
	public ref struct TupleReader
	{
		public readonly ReadOnlySpan<byte> Input;
		public int Depth;
		public int Cursor;

		public TupleReader(ReadOnlySpan<byte> input)
		{
			this.Input = input;
			this.Depth = 0;
			this.Cursor = 0;
		}

		public TupleReader(ReadOnlySpan<byte> input, int depth)
		{
			this.Input = input;
			this.Depth = depth;
			this.Cursor = 0;
		}

		public TupleReader(ReadOnlySpan<byte> input, int depth, int cursor)
		{
			this.Input = input;
			this.Depth = depth;
			this.Cursor = cursor;
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static TupleReader Embedded(ReadOnlySpan<byte> packed)
		{
			Contract.Debug.Requires(packed.Length >= 2 && packed[0] == TupleTypes.EmbeddedTuple && packed[^1] == 0);
			return new TupleReader(packed.Slice(1, packed.Length - 2));
		}

		public readonly int Remaining => this.Input.Length - this.Cursor;

		public readonly bool HasMore => this.Cursor < this.Input.Length;

		public readonly int Peek() => this.Input[this.Cursor];

		public readonly int PeekAt(int offset) => this.Input[this.Cursor + offset];

		public void Advance(int bytes) => this.Cursor += bytes;

		public bool TryReadBytes(int count, out Range token, [NotNullWhen(false)] out Exception? error)
		{
			int start = this.Cursor;
			int end = start + count;
			if (end > this.Input.Length)
			{
				token = default;
				error = SliceReader.NotEnoughBytes(count);
				return false;
			}

			token = new Range(start, end);
			this.Cursor = end;
			error = null;
			return true;
		}

		public bool TryReadByteString(out Range token, [NotNullWhen(false)] out Exception? error)
		{
			int p = this.Cursor;
			var buffer = this.Input;
			int end = buffer.Length;
			while (p < end)
			{
				byte b = buffer[p++];
				if (b == 0)
				{
					//TODO: decode \0\xFF ?
					if (p < end && buffer[p] == 0xFF)
					{
						// skip the next byte and continue
						p++;
						continue;
					}

					token = new Range(this.Cursor, p);
					this.Cursor = p;
					error = null;
					return true;
				}
			}

			token = default;
			error = new FormatException("Truncated byte string (expected terminal NUL not found)");
			return false;
		}

	}

}
