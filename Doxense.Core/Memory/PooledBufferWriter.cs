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
	using System.Runtime.InteropServices;
	using Doxense.Diagnostics.Contracts;
	using JetBrains.Annotations;

	//REVIEW: TODO: c'est un clone de ArrayBufferWriter<byte> qui est dans .NET Core 3.0 mais pas encore dans .NET Standard 2.1 preview!
	// => dés qu'il sera dispo dans .NET Standard 2.1, on pourra virer cette implémentation locale!

	public sealed class PooledBufferWriter : IBufferWriter<byte>, IDisposable
	{
		private readonly MemoryPool<byte> m_pool;

		private IMemoryOwner<byte>? m_rent;
		private Memory<byte> m_buffer;
		private int m_index;

		private const int DefaultInitialBufferSize = 256;

		/// <summary>
		/// Creates an instance of an <see cref="PooledBufferWriter"/>, in which data can be written to,
		/// with the default initial capacity.
		/// </summary>
		public PooledBufferWriter(MemoryPool<byte>? pool = null)
			: this(256, pool)
		{ }

		/// <summary>
		/// Creates an instance of an <see cref="PooledBufferWriter"/>, in which data can be written to,
		/// with an initial capacity specified.
		/// </summary>
		/// <param name="initialCapacity">The minimum capacity with which to initialize the underlying buffer.</param>
		/// <param name="pool"></param>
		/// <exception cref="ArgumentException">
		/// Thrown when <paramref name="initialCapacity"/> is not positive (i.e. less than or equal to 0).
		/// </exception>
		public PooledBufferWriter(int initialCapacity, MemoryPool<byte>? pool = null)
		{
			Contract.Positive(initialCapacity);

			pool ??= MemoryPool<byte>.Shared;
			m_pool = pool;
			m_rent = pool.Rent(initialCapacity);
			m_buffer = m_rent.Memory;
			m_index = 0;
		}

		/// <summary>
		/// Returns the data written to the underlying buffer so far, as a <see cref="ReadOnlyMemory{T}"/>.
		/// </summary>
		public ReadOnlyMemory<byte> WrittenMemory => m_buffer.Slice(0, m_index);

		public Slice WrittenSlice
		{
			get
			{
				var block = this.WrittenMemory;
				return MemoryMarshal.TryGetArray(block, out var segment) ? segment.AsSlice() : block.ToArray().AsSlice();
			}
		}

		/// <summary>
		/// Returns the amount of data written to the underlying buffer so far.
		/// </summary>
		public int WrittenCount => m_index;

		/// <summary>
		/// Returns the total amount of space within the underlying buffer.
		/// </summary>
		public int Capacity => m_buffer.Length;

		/// <summary>
		/// Returns the amount of space available that can still be written into without forcing the underlying buffer to grow.
		/// </summary>
		public int FreeCapacity => m_buffer.Length - m_index;

		/// <summary>
		/// Clears the data written to the underlying buffer.
		/// </summary>
		/// <remarks>
		/// You must clear the <see cref="PooledBufferWriter"/> before trying to re-use it.
		/// </remarks>
		public void Clear()
		{
			Contract.Debug.Requires(m_buffer.Length >= m_index);
			m_buffer.Slice(0, m_index).Span.Clear();
			m_index = 0;
		}

		/// <summary>
		/// Notifies <see cref="IBufferWriter{T}"/> that <paramref name="count"/> amount of data was written to the output <see cref="Span{T}"/>/<see cref="Memory{T}"/>
		/// </summary>
		/// <exception cref="ArgumentException">
		/// Thrown when <paramref name="count"/> is negative.
		/// </exception>
		/// <exception cref="InvalidOperationException">
		/// Thrown when attempting to advance past the end of the underlying buffer.
		/// </exception>
		/// <remarks>
		/// You must request a new buffer after calling Advance to continue writing more data and cannot write to a previously acquired buffer.
		/// </remarks>
		public void Advance(int count)
		{
			Contract.Positive(count);

			if (m_index > m_buffer.Length - count)
				ThrowInvalidOperationException_AdvancedTooFar(m_buffer.Length);

			m_index += count;
		}

		/// <summary>
		/// Returns a <see cref="Memory{T}"/> to write to that is at least the requested length (specified by <paramref name="sizeHint"/>).
		/// If no <paramref name="sizeHint"/> is provided (or it's equal to <code>0</code>), some non-empty buffer is returned.
		/// </summary>
		/// <exception cref="ArgumentException">
		/// Thrown when <paramref name="sizeHint"/> is negative.
		/// </exception>
		/// <remarks>
		/// This will never return an empty <see cref="Memory{T}"/>.
		/// </remarks>
		/// <remarks>
		/// There is no guarantee that successive calls will return the same buffer or the same-sized buffer.
		/// </remarks>
		/// <remarks>
		/// You must request a new buffer after calling Advance to continue writing more data and cannot write to a previously acquired buffer.
		/// </remarks>
		public Memory<byte> GetMemory(int sizeHint = 0)
		{
			CheckAndResizeBuffer(sizeHint);
			Contract.Debug.Assert(m_buffer.Length > m_index);
			return m_buffer.Slice(m_index);
		}

		/// <summary>
		/// Returns a <see cref="Span{T}"/> to write to that is at least the requested length (specified by <paramref name="sizeHint"/>).
		/// If no <paramref name="sizeHint"/> is provided (or it's equal to <code>0</code>), some non-empty buffer is returned.
		/// </summary>
		/// <exception cref="ArgumentException">
		/// Thrown when <paramref name="sizeHint"/> is negative.
		/// </exception>
		/// <remarks>
		/// This will never return an empty <see cref="Span{T}"/>.
		/// </remarks>
		/// <remarks>
		/// There is no guarantee that successive calls will return the same buffer or the same-sized buffer.
		/// </remarks>
		/// <remarks>
		/// You must request a new buffer after calling Advance to continue writing more data and cannot write to a previously acquired buffer.
		/// </remarks>
		public Span<byte> GetSpan(int sizeHint = 0)
		{
			CheckAndResizeBuffer(sizeHint);
			Contract.Debug.Assert(m_buffer.Length > m_index);
			return m_buffer.Slice(m_index).Span;
		}

		private void CheckAndResizeBuffer(int sizeHint)
		{
			Contract.Positive(sizeHint);

			if (m_rent == null) throw ThrowObjectDisposedException();

			if (sizeHint == 0)
			{
				sizeHint = 1;
			}

			if (sizeHint > this.FreeCapacity)
			{
				int growBy = Math.Max(sizeHint, m_buffer.Length);

				if (m_buffer.Length == 0)
				{
					growBy = Math.Max(growBy, DefaultInitialBufferSize);
				}

				int newSize = checked(m_buffer.Length + growBy);

				var newRent = m_pool.Rent(newSize);
				var newBuffer = newRent.Memory;
				m_buffer.CopyTo(newBuffer);
				m_rent.Dispose();
				m_rent = newRent;
				m_buffer = newBuffer;
			}

			Contract.Debug.Ensures(this.FreeCapacity > 0 && this.FreeCapacity >= sizeHint);
		}


		/// <summary>Transfer ownership of the internal buffer to the caller</summary>
		/// <returns>Owned memory for the buffer content that MUST be disposed by the caller!</returns>
		/// <remarks>Once called, disposing the writer instance will NOT return the buffer to the pool! The caller is now responsible for the lifetime of the buffer</remarks>
		public IMemoryOwner<byte> AcquireMemory(out int used)
		{
			if (m_rent == null) throw ThrowObjectDisposedException();
			var rent = m_rent;
			used = m_index;
			m_buffer = default;
			m_rent = null;
			m_index = 0;
			return rent;
		}

		[MethodImpl(MethodImplOptions.NoInlining)]
		private static void ThrowInvalidOperationException_AdvancedTooFar(int capacity)
		{
			throw new InvalidOperationException("Buffer writer advanced too far");
		}

		[Pure, MethodImpl(MethodImplOptions.NoInlining)]
		private static ObjectDisposedException ThrowObjectDisposedException()
		{
			return new ObjectDisposedException("Buffer writer has already been disposed, or the buffer has already been acquired by someone else.");
		}

		public void Dispose()
		{
			Dispose(true);
			GC.SuppressFinalize(this);
		}

		private void Dispose(bool disposing)
		{
			if (disposing)
			{
				m_buffer = default;
				m_index = 0;
				m_rent?.Dispose();
			}
		}

	}
}
