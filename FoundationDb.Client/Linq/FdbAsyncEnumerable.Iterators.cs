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
	* Neither the name of the <organization> nor the
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

namespace FoundationDb.Linq
{
	using FoundationDb.Client.Utils;
	using System;
	using System.Collections.Generic;
	using System.Threading;
	using System.Threading.Tasks;

	public static partial class FdbAsyncEnumerable
	{
		// Welcome to the wonderful world of the Monads! 

		internal sealed class AnonymousAsyncSelectable<T, R> : IFdbAsyncEnumerable<R>
		{
			public readonly IFdbAsyncEnumerable<T> Source;
			public readonly Func<IFdbAsyncEnumerator<T>, IFdbAsyncEnumerator<R>> Factory;

			public AnonymousAsyncSelectable(IFdbAsyncEnumerable<T> source, Func<IFdbAsyncEnumerator<T>, IFdbAsyncEnumerator<R>> factory)
			{
				this.Source = source;
				this.Factory = factory;
			}

			public IFdbAsyncEnumerator<R> GetEnumerator()
			{
				IFdbAsyncEnumerator<T> inner = null;
				try
				{
					inner = this.Source.GetEnumerator();
					if (inner == null) throw new InvalidOperationException("The underlying async sequence returned an empty enumerator");

					var outer = this.Factory(inner);
					if (outer == null) throw new InvalidOperationException("The async factory returned en empty enumerator");

					return outer;
				}
				catch(Exception)
				{
					//make sure that the inner iterator gets disposed if something went wrong
					if (inner != null) inner.Dispose();
					throw;
				}
			}
		}

		internal sealed class AnonymousSelectable<T, R> : IFdbAsyncEnumerable<R>
		{
			public readonly IEnumerable<T> Source;
			public readonly Func<IEnumerator<T>, IFdbAsyncEnumerator<R>> Factory;

			public AnonymousSelectable(IEnumerable<T> source, Func<IEnumerator<T>, IFdbAsyncEnumerator<R>> factory)
			{
				this.Source = source;
				this.Factory = factory;
			}

			public IFdbAsyncEnumerator<R> GetEnumerator()
			{
				IEnumerator<T> inner = null;
				try
				{
					inner = this.Source.GetEnumerator();
					if (inner == null) throw new InvalidOperationException("The underlying sequence returned an empty enumerator");

					var outer = this.Factory(inner);
					if (outer == null) throw new InvalidOperationException("The async factory returned en empty enumerator");

					return outer;
				}
				catch (Exception)
				{
					//make sure that the inner iterator gets disposed if something went wrong
					if (inner != null) inner.Dispose();
					throw;
				}
			}
		}

		internal sealed class AsyncSelector<T, R> : IFdbAsyncEnumerator<R>
		{

			private IFdbAsyncEnumerator<T> m_iterator;
			private Func<T, R> m_transformSync;
			private Func<T, Task<R>> m_transformAsync;
			private bool m_closed;
			private R m_current;

			public AsyncSelector(IFdbAsyncEnumerator<T> source, Func<T, R> transform)
			{
				Contract.Requires(source != null && transform != null);

				m_iterator = source;
				m_transformSync = transform;
			}

			public AsyncSelector(IFdbAsyncEnumerator<T> source, Func<T, Task<R>> transform)
			{
				Contract.Requires(source != null && transform != null);

				m_iterator = source;
				m_transformAsync = transform;
			}

			public async Task<bool> MoveNext(CancellationToken cancellationToken)
			{
				if (m_closed)
				{
					if (m_iterator == null) throw new ObjectDisposedException(this.GetType().Name);
					return false;
				}

				cancellationToken.ThrowIfCancellationRequested();

				if (await m_iterator.MoveNext(cancellationToken))
				{
					if (m_transformSync != null)
					{
						m_current = m_transformSync(m_iterator.Current);
					}
					else
					{
						m_current = await m_transformAsync(m_iterator.Current);
					}
					return true;
				}

				m_current = default(R);
				m_closed = true;
				return false;
			}

			public R Current
			{
				get
				{
					if (m_closed) throw new InvalidOperationException();
					return m_current;
				}
			}

			public void Dispose()
			{
				if (m_iterator != null)
				{
					m_iterator.Dispose();
				}
				this.m_iterator = null;
				m_transformSync = null;
				m_transformAsync = null;
				m_closed = true;
				m_current = default(R);
			}

		}

		internal sealed class AsyncIndexedSelector<T, R> : IFdbAsyncEnumerator<R>
		{

			private IFdbAsyncEnumerator<T> m_iterator;
			private Func<T, int, R> m_transformSync;
			private Func<T, int, Task<R>> m_transformAsync;
			private int m_index;
			private R m_current;

			public AsyncIndexedSelector(IFdbAsyncEnumerator<T> source, Func<T, int, R> transform)
			{
				Contract.Requires(source != null && transform != null);

				m_iterator = source;
				m_transformSync = transform;
			}

			public AsyncIndexedSelector(IFdbAsyncEnumerator<T> source, Func<T, int, Task<R>> transform)
			{
				Contract.Requires(source != null && transform != null);

				m_iterator = source;
				m_transformAsync = transform;
			}

			public async Task<bool> MoveNext(CancellationToken cancellationToken)
			{
				if (m_index == -1)
				{
					if (m_iterator == null) throw new ObjectDisposedException(this.GetType().Name);
					return false;
				}

				cancellationToken.ThrowIfCancellationRequested();

				if (await m_iterator.MoveNext(cancellationToken))
				{
					if (m_transformSync != null)
					{
						m_current = m_transformSync(m_iterator.Current, m_index);
					}
					else
					{
						m_current = await m_transformAsync(m_iterator.Current, m_index);
					}
					++m_index;
					return true;
				}

				m_current = default(R);
				m_index = -1;
				return false;
			}

			public R Current
			{
				get
				{
					if (m_index == -1) throw new InvalidOperationException();
					return m_current;
				}
			}

			public void Dispose()
			{
				if (m_iterator != null)
				{
					m_iterator.Dispose();
				}
				m_iterator = null;
				m_transformSync = null;
				m_transformAsync = null;
				m_index = -1;
				m_current = default(R);
			}

		}

		internal sealed class Selector<T, R> : IFdbAsyncEnumerator<R>
		{

			private IEnumerator<T> m_iterator;
			private Func<T, Task<R>> m_transform;
			private bool m_closed;
			private R m_current;

			public Selector(IEnumerator<T> iterator, Func<T, Task<R>> transform)
			{
				Contract.Requires(iterator != null && transform != null);

				m_iterator = iterator;
				m_transform = transform;
			}

			public async Task<bool> MoveNext(CancellationToken cancellationToken)
			{
				if (m_closed)
				{
					if (m_iterator == null) throw new ObjectDisposedException(this.GetType().Name);
					return false;
				}

				cancellationToken.ThrowIfCancellationRequested();

				if (m_iterator.MoveNext())
				{
					m_current = await m_transform(m_iterator.Current);
					return true;
				}

				m_current = default(R);
				m_closed = true;
				return false;
			}

			public R Current
			{
				get
				{
					if (m_closed) throw new InvalidOperationException();
					return m_current;
				}
			}

			public void Dispose()
			{
				if (m_iterator != null)
				{
					m_iterator.Dispose();
				}
				m_iterator = null;
				m_transform = null;
				m_closed = true;
				m_current = default(R);
			}

		}

		internal static AnonymousAsyncSelectable<T, R> Create<T, R>(IFdbAsyncEnumerable<T> source, Func<IFdbAsyncEnumerator<T>, IFdbAsyncEnumerator<R>> factory)
		{
			return new AnonymousAsyncSelectable<T, R>(source, factory);
		}

		internal static AnonymousSelectable<T, R> Create<T, R>(IEnumerable<T> source, Func<IEnumerator<T>, IFdbAsyncEnumerator<R>> factory)
		{
			return new AnonymousSelectable<T, R>(source, factory);
		}

		private static async Task<long> Iterate<T>(IFdbAsyncEnumerable<T> source, Action<T> action, CancellationToken ct)
		{
			ct.ThrowIfCancellationRequested();

			long count = 0;
			using (var iterator = source.GetEnumerator())
			{
				if (iterator == null) throw new InvalidOperationException("The underlying sequence returned a null async iterator");

				while (await iterator.MoveNext(ct))
				{
					action(iterator.Current);
					++count;
				}
			}
			return count;
		}

		private static async Task<long> Iterate<T>(IFdbAsyncEnumerable<T> source, Func<T, Task> action, CancellationToken ct)
		{
			ct.ThrowIfCancellationRequested();

			long count = 0;
			using (var iterator = source.GetEnumerator())
			{
				if (iterator == null) throw new InvalidOperationException("The underlying sequence returned a null async iterator");

				while (await iterator.MoveNext(ct))
				{
					await action(iterator.Current);
					++count;
				}
			}
			return count;
		}

		/// <summary>Helper async method to get the first element of an async sequence</summary>
		private static async Task<T> Head<T>(IFdbAsyncEnumerable<T> source, CancellationToken ct, bool single, bool orDefault)
		{
			using (var iterator = source.GetEnumerator())
			{
				if (iterator == null) throw new InvalidOperationException("The sequence returned a null async iterator");

				if (await iterator.MoveNext(ct))
				{
					if (single)
					{
						if (await iterator.MoveNext(ct)) throw new InvalidOperationException("The sequence contained more than one element");
					}
					return iterator.Current;
				}
				if (!orDefault) throw new InvalidOperationException("The sequence was empty");
				return default(T);
			}
		}

	}
}
