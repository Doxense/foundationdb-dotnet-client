#region BSD License
/* Copyright (c) 2013-2020, Doxense SAS
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

#if !USE_SHARED_FRAMEWORK

namespace Doxense.Linq
{
	using System;
	using System.Collections.Generic;
	using System.Threading;
	using System.Threading.Tasks;
	using Doxense.Diagnostics.Contracts;

	public static partial class AsyncEnumerable
	{

		/// <summary>An empty sequence</summary>
		private sealed class EmptySequence<TSource> : IConfigurableAsyncEnumerable<TSource>, IAsyncEnumerator<TSource>
		{
			public static readonly EmptySequence<TSource> Default = new EmptySequence<TSource>();

			private EmptySequence()
			{ }

			public IAsyncEnumerator<TSource> GetAsyncEnumerator(CancellationToken ct) => this;

			public IAsyncEnumerator<TSource> GetAsyncEnumerator(CancellationToken ct, AsyncIterationHint hint) => this;

			ValueTask<bool> IAsyncEnumerator<TSource>.MoveNextAsync() => new ValueTask<bool>(false);

			TSource IAsyncEnumerator<TSource>.Current => throw new InvalidOperationException();

			ValueTask IAsyncDisposable.DisposeAsync() => default;

		}

		private sealed class SingletonSequence<TElement> : IConfigurableAsyncEnumerable<TElement>
		{

			private readonly Delegate m_lambda;

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

			public IAsyncEnumerator<TElement> GetAsyncEnumerator(CancellationToken ct) => new Enumerator(m_lambda, ct);

			public IAsyncEnumerator<TElement> GetAsyncEnumerator(CancellationToken ct, AsyncIterationHint mode)
			{
				ct.ThrowIfCancellationRequested();
				return new Enumerator(m_lambda, ct);
			}

			private sealed class Enumerator : IAsyncEnumerator<TElement>
			{
				//REVIEW: we could have specialized version for Task returning vs non-Task returning lambdas

				private CancellationToken m_ct;
				private readonly Delegate m_lambda;
				private bool m_called;
				private TElement m_current;

				public Enumerator(Delegate lambda, CancellationToken ct)
				{
					m_ct = ct;
					m_lambda = lambda;
				}

				public async ValueTask<bool> MoveNextAsync()
				{
					m_ct.ThrowIfCancellationRequested();
					if (m_called)
					{
						m_current = default(TElement);
						return false;
					}

					//note: avoid using local variables as much as possible!
					m_called = true;
					var lambda = m_lambda;
					if (lambda is Func<TElement> f)
					{
						m_current = f();
						return true;
					}

					if (lambda is Func<Task<TElement>> ft)
					{
						m_current = await ft().ConfigureAwait(false);
						return true;
					}

					if (lambda is Func<CancellationToken, Task<TElement>> fct)
					{
						m_current = await fct(m_ct).ConfigureAwait(false);
						return true;
					}

					throw new InvalidOperationException("Unsupported delegate type");
				}

				public TElement Current => m_current;

				public ValueTask DisposeAsync()
				{
					m_called = true;
					m_current = default;
					return default;
				}
			}
		}
	}
}

#endif
