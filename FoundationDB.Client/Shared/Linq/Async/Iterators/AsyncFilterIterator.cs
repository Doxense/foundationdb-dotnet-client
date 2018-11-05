#region BSD Licence
/* Copyright (c) 2013-2018, Doxense SAS
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

namespace Doxense.Linq.Async.Iterators
{
	using System;
	using System.Threading;
	using System.Threading.Tasks;
	using Doxense.Diagnostics.Contracts;
	using Doxense.Threading.Tasks;
	using JetBrains.Annotations;

	public abstract class AsyncFilterIterator<TSource, TResult> : AsyncIterator<TResult>
	{
		/// <summary>Source sequence (when in iterable mode)</summary>
		protected IAsyncEnumerable<TSource> m_source;

		/// <summary>Active iterator on the source (when in iterator mode)</summary>
		protected IAsyncEnumerator<TSource> m_iterator;
		protected bool m_innerHasCompleted;

		protected AsyncFilterIterator([NotNull] IAsyncEnumerable<TSource> source)
		{
			Contract.Requires(source != null);
			m_source = source;
		}

		/// <summary>Start the inner iterator</summary>
		protected virtual IAsyncEnumerator<TSource> StartInner(CancellationToken ct)
		{
			ct.ThrowIfCancellationRequested();
			// filtering changes the number of items, so that means that, even if the underlying caller wants one item, we may need to read more.
			// => change all "Head" requests into "Iterator" to prevent any wrong optimizations by the underlying source (ex: using a too small batch size)
			if (m_source is IConfigurableAsyncEnumerable<TSource> configurable)
			{
				var mode = m_mode;
				if (mode == AsyncIterationHint.Head) mode = AsyncIterationHint.Iterator;

				return configurable.GetAsyncEnumerator(m_ct, mode);
			}

			return m_source.GetAsyncEnumerator();
		}

		protected void MarkInnerAsCompleted()
		{
			m_innerHasCompleted = true;

			// we don't need the inerator, so we can dispose of it immediately
			Interlocked.Exchange(ref m_iterator, null)?.Dispose();
		}

		protected override Task<bool> OnFirstAsync()
		{
			// on the first call to MoveNext, we have to hook up with the source iterator

			IAsyncEnumerator<TSource> iterator = null;
			try
			{
				iterator = StartInner(m_ct);
				if (iterator == null) return TaskHelpers.False;
				OnStarted(iterator);
				return TaskHelpers.True;
			}
			catch (Exception)
			{
				// whatever happens, make sure that we released the iterator...
				if (iterator != null)
				{
					iterator.Dispose();
					iterator = null;
				}
				throw;
			}
			finally
			{
				m_iterator = iterator;
			}
		}

		protected virtual void OnStarted(IAsyncEnumerator<TSource> iterator)
		{
			//override this to add custom starting logic once we know that the inner iterator is ready
		}

		protected virtual void OnStopped()
		{
			// override this to add custom stopping logic once the iterator has completed (for whatever reason)
		}

		protected override void Cleanup()
		{
			try
			{
				OnStopped();
			}
			finally
			{
				MarkInnerAsCompleted();
			}
		}

	}

}

#endif
