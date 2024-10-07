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

// We cannot use Interlocked.Exchange(ref bool) until .NET 9+
#if NET9_0_OR_GREATER
#define USE_BOOLEAN_DISPOSE_FLAG
#endif

namespace System
{
	using System.Buffers;
	using System.Runtime.CompilerServices;

	/// <summary>A container for rented <see cref="Slice"/> that can be returned into a <see cref="ArrayPool{T}"/> after it is not needed anymore.</summary>
	/// <remarks>
	/// <para>Users of this type <b>MUST</b> call <see cref="Dispose"/> after the data is no longer needed, or the buffers will not be returned to the pool.</para>
	/// <para>Users of this type <b>MUST NOT</b> use any data from this instance after calling <see cref="Dispose"/></para>
	/// <para>This type mirrors <see cref="IMemoryOwner{T}"/> but without allocations and with further optimizations.</para>
	/// </remarks>
	public struct SliceOwner : IDisposable
	{

		public static SliceOwner Nil => new(Slice.Nil);

		public static SliceOwner Empty => new(Slice.Empty);

		/// <summary>The rented slice</summary>
		private Slice m_data;

		/// <summary>The pool where the buffer should be returned</summary>
		private ArrayPool<byte>? m_pool;

		/// <summary>Flag that is set to <see langword="true"/> when <see cref="Dispose"/> is called</summary>
#if USE_BOOLEAN_DISPOSE_FLAG
		private volatile bool m_disposed;
#else
		private volatile int m_disposed;
#endif

		private SliceOwner(Slice data)
		{
			m_data = data;
			m_pool = null;
		}

		private SliceOwner(Slice data, ArrayPool<byte> pool)
		{
			Contract.NotNull(pool);
			if (data.Count > 0)
			{
				m_data = data;
				m_pool = pool;
			}
			else
			{
				m_data = data.IsNull ? Slice.Nil : Slice.Empty;
				m_pool = null;
			}
		}

		/// <summary>Returns a <see cref="SliceOwner"/> that will dispose the content of a slice allocated from a pool</summary>
		/// <param name="data">Slice of data, that is either <see cref="Slice.Nil"/>, <see cref="Slice.Empty"/>, or uses an array rented from <see cref="pool"/></param>
		/// <param name="pool">Pool (optional) that was used to allocate the content of <paramref name="data"/>, or <see langword="null"/> if the content was allocated on the heap or must not be returned to any pool.</param>
		/// <returns><see cref="SliceOwner"/> that will return the content to pool once disposed (if one was provided).</returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static SliceOwner Create(Slice data, ArrayPool<byte>? pool = null)
		{
			return
				  pool == null ? new (data)
				: data.Count > 0 ? new(data, pool)
				: CreateEmpty(data, pool);

			static SliceOwner CreateEmpty(Slice data, ArrayPool<byte> pool)
			{
				// we must make sure to NEVER return the array of Slice.Empty (which has a Length of 1) to the pool!
				byte[]? array = data.Array;

				// Slice.Nil is safe
				if (array == null!)
				{
					return SliceOwner.Nil;
				}

				// the caller used a non-empty rented buffer, but the count was empty,
				// => we will return the buffer immediately to the pool
				if (!ReferenceEquals(array, Slice.Empty.Array))
				{ // we _assume_ that this array has been rented from this pool
					pool.Return(array);
				}

				return SliceOwner.Empty;
			}
		}

		/// <inheritdoc />
		public void Dispose()
		{
#if USE_BOOLEAN_DISPOSE_FLAG
			if (Interlocked.Exchange(ref m_disposed, false))
#else
			if (Interlocked.Exchange(ref m_disposed, 1) != 0)
#endif
			{ // already disposed
				return;
			}

			byte[]? array = m_data.Array;
			var pool = m_pool;
			m_data = default;
			if (pool != null)
			{
				Contract.Debug.Assert(array is not null && array.Length > 0);
				m_pool = null;
				pool.Return(array);
			}
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private readonly void ThrowIfDisposed()
		{
#if USE_BOOLEAN_DISPOSE_FLAG
			if (m_disposed)
#else
			if (m_disposed != 0)
#endif
			{
				throw ThrowHelper.ObjectDisposedException<SliceOwner>("The content has already been returned to the pool");
			}
		}

		/// <summary>Returns <see langword="true"/> if the buffer is still usable, or <see langword="false"/> if <see cref="Dispose"/> has been called at least once.</summary>
		public readonly bool IsValid => m_data.Array != null!;

		/// <summary>Returns <see langword="true"/> if the buffer is rented from a pool and is temporary, or <see langword="false"/> if it is allocated from the heap and can be exposed</summary>
		public readonly bool IsPooled => m_pool != null;

		/// <summary>Size (in bytes) of the content</summary>
		/// <remarks>Returns <see langword="0"/> once the container is disposed.</remarks>
		public readonly int Count => m_data.Count;

		/// <summary>Returns either the original content (if it is not pooled), or a copy of the content (if it is pooled)</summary>
		/// <returns>Slice that is safe to use, even after this instance has been disposed</returns>
		public readonly Slice GetOrCopy()
		{
			var data = this.Data;
			return m_pool == null ? data : data.Copy();
		}

		/// <summary>Returns a copy the original content (whether it was pooled or not)</summary>
		public readonly Slice Copy()
		{
			var data = this.Data;
			return data.Copy();
		}

		/// <summary>Returns a copy of the original content, stored using the specified pool</summary>
		public readonly SliceOwner Copy(ArrayPool<byte> pool)
		{
			Contract.NotNull(pool);

			var data = this.Data;

			if (data.Count == 0)
			{ // empty or nil
				return this;
			}

			//note: even if pool is the same as m_pool, we still have to copy,
			// since the new copy could be disposed _before_ us,
			// and we will need to be able to access the content!

			// rent a new buffer and copy
			var tmp = pool.Rent(data.Count);
			data.Span.CopyTo(tmp);

			return new (tmp.AsSlice(0, data.Count), pool);
		}

		public ArrayPool<byte>? Pool => m_pool;

		/// <summary>Returns the content as a <see cref="Slice"/></summary>
		/// <exception cref="T:System.ObjectDisposedException">If the container has already been disposed</exception>
		public readonly Slice Data
		{
			get
			{
				ThrowIfDisposed();
				return m_data.Count != 0 ? m_data : m_data.IsEmpty ? Slice.Empty : default;
			}
		}

		/// <summary>Returns the content as a <see cref="Slice"/>, unless the container has been disposed.</summary>
		public readonly bool TryGetSlice(out Slice data)
		{
#if USE_BOOLEAN_DISPOSE_FLAG
			if (m_disposed)
#else
			if (m_disposed != 0)
#endif
			{
				data = default;
				return false;
			}

			data = m_data;
			return true;
		}

		/// <summary>Returns the content as a <see cref="T:System.ReadOnlySpan`1"/>, unless the container has been disposed.</summary>
		public readonly bool TryGetSpan(out ReadOnlySpan<byte> data)
		{
#if USE_BOOLEAN_DISPOSE_FLAG
			if (m_disposed)
#else
			if (m_disposed != 0)
#endif
			{
				data = default;
				return false;
			}

			data = m_data.Span;
			return true;
		}

		/// <summary>Returns the content as a <see cref="T:System.ReadOnlySpan`1"/></summary>
		/// <exception cref="T:System.ObjectDisposedException">If the container has already been disposed</exception>
		public readonly ReadOnlySpan<byte> Span
		{
			get
			{
				ThrowIfDisposed();
				return m_data.Span;
			}
		}

		/// <summary>Returns the content as a <see cref="T:System.ReadOnlyMemory`1"/>, unless the container has been disposed.</summary>
		public readonly bool TryGetMemory(out ReadOnlyMemory<byte> data)
		{
#if USE_BOOLEAN_DISPOSE_FLAG
			if (m_disposed)
#else
			if (m_disposed != 0)
#endif
			{
				data = default;
				return false;
			}

			data = m_data.Memory;
			return true;
		}

		/// <summary>Returns the content as a <see cref="T:System.ReadOnlyMemory`1"/></summary>
		/// <exception cref="T:System.ObjectDisposedException">If the container has already been disposed</exception>
		public readonly ReadOnlyMemory<byte> Memory
		{
			get
			{
				ThrowIfDisposed();
				return m_data.Memory;
			}
		}

		/// <summary>Returns a copy of the content</summary>
		/// <exception cref="T:System.ObjectDisposedException">If the container has already been disposed</exception>
		public readonly byte[] ToArray() => this.Span.ToArray();

		/// <summary>Copies the content to a destination <see cref="T:System.Span`1"/></summary>
		/// <exception cref="T:System.ArgumentException"> <paramref name="destination" /> is not large enough.</exception>
		/// <exception cref="T:System.ObjectDisposedException"> the container has already been disposed</exception>
		public readonly int CopyTo(Span<byte> destination)
		{
			var span = this.Span;
			span.CopyTo(destination);
			return span.Length;
		}

		/// <summary>Copies the content to a destination <see cref="T:System.Span`1"/>, if it is large enough.</summary>
		/// <exception cref="T:System.ObjectDisposedException"> the container has already been disposed</exception>
		public readonly bool TryCopyTo(Span<byte> destination) => this.Span.TryCopyTo(destination);

		/// <summary>Copies the content to a destination <see cref="T:System.Span`1"/>, if it is large enough.</summary>
		/// <exception cref="T:System.ObjectDisposedException"> the container has already been disposed</exception>
		public readonly bool TryCopyTo(Span<byte> destination, out int bytesWritten)
		{
			var span = Span;
			if (!span.TryCopyTo(destination))
			{
				bytesWritten = 0;
				return false;
			}

			bytesWritten = span.Length;
			return true;
		}

	}

}
