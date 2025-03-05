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

namespace Doxense.Linq
{

	public static partial class AsyncQuery
	{

		/// <summary>Query that iterates over a sequence of source elements, and return the result of an asynchronous transformation</summary>
		/// <typeparam name="TSource">Type of elements of the inner sequence</typeparam>
		/// <typeparam name="TResult">Type of elements of the outer async sequence</typeparam>
		/// <remarks>This types adapts a regular <see cref="IEnumerable{TSource}"/> into a </remarks>
		internal sealed class EnumerableIterator<TSource, TResult> : IAsyncEnumerator<TResult>
		{

			private IEnumerator<TSource>? m_iterator;
			private Func<TSource, Task<TResult>>? m_transform;
			private bool m_disposed;
			private TResult? m_current;
			private CancellationToken m_ct;

			public EnumerableIterator(IEnumerator<TSource> iterator, Func<TSource, Task<TResult>> transform, CancellationToken ct)
			{
				Contract.Debug.Requires(iterator != null && transform != null);

				m_iterator = iterator;
				m_transform = transform;
				m_ct = ct;
			}

			public async ValueTask<bool> MoveNextAsync()
			{
				if (m_disposed)
				{
					if (m_iterator == null) throw new ObjectDisposedException(this.GetType().Name);
					return false;
				}

				m_ct.ThrowIfCancellationRequested();

				if (m_iterator!.MoveNext())
				{
					m_current = await m_transform!(m_iterator.Current).ConfigureAwait(false);
					return true;
				}

				m_current = default!;
				m_disposed = true;
				return false;
			}

			public TResult Current
			{
				get
				{
					if (m_disposed) throw new InvalidOperationException();
					return m_current!;
				}
			}

			public ValueTask DisposeAsync()
			{
				m_iterator?.Dispose();
				m_iterator = null;
				m_transform = null;
				m_disposed = true;
				m_current = default;
				m_ct = CancellationToken.None;
				return default;
			}

		}

	}

}
