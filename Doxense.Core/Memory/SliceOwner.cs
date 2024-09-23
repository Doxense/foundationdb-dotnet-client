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

namespace System
{
	using System.Buffers;
	using System.Runtime.CompilerServices;

	/// <summary>A container for rented <see cref="Slice"/> that can be returned into a <see cref="ArrayPool{T}"/> after it is not needed anymore.</summary>
	/// <remarks>
	/// <para>Users of this type <b>MUST</b> call <see cref="Dispose"/> after they the data is no longer needed, or the buffers will not be returned to the pool.</para>
	/// <para>Users of this type <b>MUST NOT</b> use any data from this instance after calling <see cref="Dispose"/></para>
	/// <para>This type mirrors <see cref="IMemoryOwner{T}"/> but without allocations and with further optimizations.</para>
	/// </remarks>
	public struct SliceOwner : IDisposable
	{

		/// <summary>The rented slice</summary>
		private Slice m_data;

		/// <summary>The pool where the buffer should be returned</summary>
		private ArrayPool<byte> m_pool;

		public SliceOwner(Slice data, ArrayPool<byte> pool)
		{
			Contract.NotNull(pool);
			m_data = data;
			m_pool = pool;
		}

		/// <inheritdoc />
		public void Dispose()
		{
			var array = m_data.Array;
			var pool = m_pool;
			m_data = default;
			m_pool = null!;
			if (pool != null && array?.Length > 0)
			{
				pool.Return(array);
			}
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private void ThrowIfDisposed()
		{
			if (m_pool == null) throw ThrowHelper.ObjectDisposedException<SliceOwner>("The content has already been returned to the pool");
		}

		/// <summary>Returns <see langword="true"/> if the buffer is still usable, or <see langword="false"/> if <see cref="Dispose"/> has been called at least once.</summary>
		public bool IsValid => m_pool != null;

		/// <summary>Size (in bytes) of the content</summary>
		/// <remarks>Returns <see langword="0"/> once the container is disposed.</remarks>
		public int Count => m_data.Count;

		/// <summary>Returns the content as a <see cref="Slice"/></summary>
		/// <exception cref="T:System.ObjectDisposedException">If the container has already been disposed</exception>
		public Slice Data
		{
			get
			{
				ThrowIfDisposed();
				return m_data;
			}
		}

		/// <summary>Returns the content as a <see cref="Slice"/>, unless the container has been disposed.</summary>
		public bool TryGetSlice(out Slice data)
		{
			if (m_pool == null)
			{
				data = default;
				return false;
			}

			data = m_data;
			return true;
		}

		/// <summary>Returns the content as a <see cref="T:System.ReadOnlySpan`1"/>, unless the container has been disposed.</summary>
		public bool TryGetSpan(out ReadOnlySpan<byte> data)
		{
			if (m_pool == null)
			{
				data = default;
				return false;
			}

			data = m_data.Span;
			return true;
		}

		/// <summary>Returns the content as a <see cref="T:System.ReadOnlySpan`1"/></summary>
		/// <exception cref="T:System.ObjectDisposedException">If the container has already been disposed</exception>
		public ReadOnlySpan<byte> Span
		{
			get
			{
				ThrowIfDisposed();
				return m_data.Span;
			}
		}

		/// <summary>Returns the content as a <see cref="T:System.ReadOnlyMemory`1"/>, unless the container has been disposed.</summary>
		public bool TryGetMemory(out ReadOnlyMemory<byte> data)
		{
			if (m_pool == null)
			{
				data = default;
				return false;
			}

			data = m_data.Memory;
			return true;
		}

		/// <summary>Returns the content as a <see cref="T:System.ReadOnlyMemory`1"/></summary>
		/// <exception cref="T:System.ObjectDisposedException">If the container has already been disposed</exception>
		public ReadOnlyMemory<byte> Memory
		{
			get
			{
				ThrowIfDisposed();
				return m_data.Memory;
			}
		}

		/// <summary>Returns a copy of the content</summary>
		/// <exception cref="T:System.ObjectDisposedException">If the container has already been disposed</exception>
		public byte[] ToArray() => this.Span.ToArray();

		/// <summary>Copies the content to a destination <see cref="T:System.Span`1"/></summary>
		/// <exception cref="T:System.ArgumentException"> <paramref name="destination" /> is not large enough.</exception>
		/// <exception cref="T:System.ObjectDisposedException"> the container has already been disposed</exception>
		public void CopyTo(Span<byte> destination) => this.Span.CopyTo(destination);

		/// <summary>Copies the content to a destination <see cref="T:System.Span`1"/>, if it is large enough.</summary>
		/// <exception cref="T:System.ObjectDisposedException"> the container has already been disposed</exception>
		public bool TryCopyTo(Span<byte> destination) => this.Span.TryCopyTo(destination);

	}

}
