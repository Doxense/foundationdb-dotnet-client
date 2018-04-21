#region BSD Licence
/* Copyright (c) 2013, Doxense SARL
All rights reserved.

Redistribution and use in source and binary forms, with or without
modification, are permitted provided that the following conditions are met:
	* Redistributions of source code must retain the above copyright
	  notice, this list of conditions and the following disclaimer.
	* Redistributions in binary form must reproduce the above copyright
	  notice, this list of conditions and the following disclaimer in the
	  documentation and/or other materials provided with the distribution.
	* Neither the name of Doxense nor the
	  names of its contributors may be used to endorse or promote products
	  derived from this software without specific prior written permission.

THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND
ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
DISCLAIMED. IN NO EVENT SHALL <COPYRIGHT HOLDER> BE LIABLE FOR ANY
DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
(INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
(INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
 */
#endregion

namespace FoundationDB.Linq
{
	using System;
	using System.Threading;
	using System.Threading.Tasks;
	using Doxense.Diagnostics.Contracts;
	using FoundationDB.Async;

	public static partial class FdbAsyncEnumerable
	{

		/// <summary>An empty sequence</summary>
		private sealed class EmptySequence<TSource> : IFdbAsyncEnumerable<TSource>, IFdbAsyncEnumerator<TSource>
		{
			public static readonly EmptySequence<TSource> Default = new EmptySequence<TSource>();

			private EmptySequence()
			{ }

			Task<bool> IAsyncEnumerator<TSource>.MoveNextAsync(CancellationToken ct)
			{
				ct.ThrowIfCancellationRequested();
				return TaskHelpers.FalseTask;
			}

			TSource IAsyncEnumerator<TSource>.Current
			{
				get { throw new InvalidOperationException("This sequence is emty"); }
			}

			void IDisposable.Dispose()
			{
				// NOOP!
			}

			public IAsyncEnumerator<TSource> GetEnumerator()
			{
				return this;
			}

			public IFdbAsyncEnumerator<TSource> GetEnumerator(FdbAsyncMode mode)
			{
				return this;
			}
		}

		private sealed class SingletonSequence<TElement> : IFdbAsyncEnumerable<TElement>, IFdbAsyncEnumerator<TElement>
		{

			private readonly Delegate m_lambda;
			private TElement m_current;
			private bool m_called;

			private SingletonSequence(Delegate lambda)
			{
				Contract.Requires(lambda != null);
				m_lambda = lambda;
			}

			public SingletonSequence(Func<TElement> lambda)
				: this((Delegate)lambda)
			{ }

			public SingletonSequence(Func<Task<TElement>> lambda)
				: this((Delegate)lambda)
			{ }

			public SingletonSequence(Func<CancellationToken, Task<TElement>> lambda)
				: this((Delegate)lambda)
			{ }

			public IFdbAsyncEnumerator<TElement> GetEnumerator(FdbAsyncMode mode = FdbAsyncMode.Default)
			{
				return new SingletonSequence<TElement>(m_lambda);
			}

			IAsyncEnumerator<TElement> IAsyncEnumerable<TElement>.GetEnumerator()
			{
				return this.GetEnumerator();
			}

			async Task<bool> IAsyncEnumerator<TElement>.MoveNextAsync(CancellationToken ct)
			{
				ct.ThrowIfCancellationRequested();
				if (m_called) return false;

				//note: avoid using local variables as much as possible!
				m_called = true;
				var lambda = m_lambda;
				if (lambda is Func<TElement>)
				{
					m_current = ((Func<TElement>)lambda)();
					return true;
				}

				if (lambda is Func<Task<TElement>>)
				{
					m_current = await ((Func<Task<TElement>>)lambda)().ConfigureAwait(false);
					return true;
				}

				if (lambda is Func<CancellationToken, Task<TElement>>)
				{
					m_current = await ((Func<CancellationToken, Task<TElement>>)lambda)(ct).ConfigureAwait(false);
					return true;
				}

				throw new InvalidOperationException("Unsupported delegate type");
			}

			TElement IAsyncEnumerator<TElement>.Current
			{
				get { return m_current; }
			}

			void IDisposable.Dispose()
			{
				m_called = true;
				m_current = default(TElement);
			}
		}
	}
}
