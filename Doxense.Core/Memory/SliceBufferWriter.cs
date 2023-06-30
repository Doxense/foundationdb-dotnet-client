#region Copyright (c) 2005-2023 Doxense SAS
// All rights reserved.
// 
// Redistribution and use in source and binary forms, with or without
// modification, are permitted provided that the following conditions are met:
// 	* Redistributions of source code must retain the above copyright
// 	  notice, this list of conditions and the following disclaimer.
// 	* Redistributions in binary form must reproduce the above copyright
// 	  notice, this list of conditions and the following disclaimer in the
// 	  documentation and/or other materials provided with the distribution.
// 	* Neither the name of Doxense nor the
// 	  names of its contributors may be used to endorse or promote products
// 	  derived from this software without specific prior written permission.
// 
// THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND
// ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
// WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
// DISCLAIMED. IN NO EVENT SHALL DOXENSE BE LIABLE FOR ANY
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
	using System.Runtime.CompilerServices;
	using JetBrains.Annotations;

	public sealed class SliceBufferWriter : IBufferWriter<byte>
	{

		private SliceWriter Buffer;

		private bool InUse;

		private ArrayPool<byte> Pool { get; }

		public SliceBufferWriter() : this(0)
		{ }

		public SliceBufferWriter(int capacity, ArrayPool<byte>? pool = null)
		{
			this.Pool = pool ?? ArrayPool<byte>.Shared;
			this.Buffer = new SliceWriter(capacity, this.Pool);
			this.InUse = true;
		}

		public void Release(bool clearArray = false)
		{
			var buffer = this.Buffer.Buffer;
			this.Buffer = default;
			this.InUse = false;
			this.Pool.Return(buffer, clearArray);
		}

		/// <summary>Reset this instance to be used for another time</summary>
		public void Reset(int sizeHint = 0)
		{
			if (this.InUse) throw new InvalidOperationException("Writer instance is still in use.");
			this.Buffer = new SliceWriter(sizeHint, this.Pool);
			this.InUse = true;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void Advance(int count)
		{
			EnsureInUse();
			this.Buffer.Advance(count);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public Memory<byte> GetMemory(int sizeHint = 0)
		{
			EnsureInUse();
			return this.Buffer.GetMemory(sizeHint);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public Span<byte> GetSpan(int sizeHint = 0)
		{
			EnsureInUse();
			return this.Buffer.GetSpan(sizeHint);
		}

		public Slice ToSlice()
		{
			EnsureInUse();
			return this.Buffer.ToSlice();
		}

		public Span<byte> ToSpan()
		{
			EnsureInUse();
			return this.Buffer.Buffer.AsSpan(this.Buffer.Position);
		}

		public Memory<byte> ToMemory()
		{
			EnsureInUse();
			return this.Buffer.Buffer.AsMemory(this.Buffer.Position);
		}

		[Pure]
		public Slice CaptureBuffer()
		{
			EnsureInUse();
			var buffer = this.Buffer;
			this.Buffer = default;
			this.InUse = false;
			return buffer.ToSlice();
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private void EnsureInUse()
		{
			if (this.InUse) throw BufferIsUnavailable();
		}

		[Pure, MethodImpl(MethodImplOptions.NoInlining)]
		private static Exception BufferIsUnavailable()
		{
			return new Exception("Writer instance has been returned to the pool, and cannot expose its underlying buffer.");
		}

		[ThreadStatic]
		private static SliceBufferWriter? CachedInstance;

		/// <summary>Alloue une instance d'un writer</summary>
		/// <param name="sizeHint">Taille total estimée nécessaire</param>
		/// <returns>Instance prête à l'emploi</returns>
		[Pure]
		public static SliceBufferWriter Rent(int sizeHint = 0)
		{
			var instance = CachedInstance;
			if (instance != null)
			{
				instance.Reset(sizeHint);
				return instance;
			}

			return new SliceBufferWriter(sizeHint);
		}

		/// <summary>Retourne un writer dans le pool</summary>
		/// <remarks>ATTENTION: le buffer exposé par ce writer NE DOIT PLUS ETRE UTILISE' !!!</remarks>
		public static void Return(SliceBufferWriter writer)
		{
			if (writer != null)
			{
				writer.Release();
				CachedInstance = writer;
			}
		}
	}
}
