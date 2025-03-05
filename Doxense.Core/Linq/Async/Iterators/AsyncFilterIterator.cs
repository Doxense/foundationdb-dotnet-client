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

namespace SnowBank.Linq.Async.Iterators
{

	public abstract class AsyncFilterIterator<TSource, TResult> : AsyncLinqIterator<TResult>
	{

		/// <summary>Source sequence (when in iterable mode)</summary>
		protected readonly IAsyncQuery<TSource> m_source;

		/// <summary>Active iterator on the source (when in iterator mode)</summary>
		protected IAsyncEnumerator<TSource>? m_iterator;
		protected bool m_innerHasCompleted;

		protected AsyncFilterIterator(IAsyncQuery<TSource> source)
		{
			Contract.Debug.Requires(source != null);
			m_source = source;
		}

		public override CancellationToken Cancellation => m_source.Cancellation;

		/// <summary>Start the inner iterator</summary>
		protected virtual IAsyncEnumerator<TSource> StartInner()
		{
			m_source.Cancellation.ThrowIfCancellationRequested();

			// filtering changes the number of items, so that means that, even if the underlying caller wants one item, we may need to read more.
			// => change all "Head" requests into "Iterator" to prevent any wrong optimizations by the underlying source (ex: using a too small batch size)
			var mode = m_mode;
			if (mode == AsyncIterationHint.Head) mode = AsyncIterationHint.Iterator;

			return m_source.GetAsyncEnumerator(mode);
		}

		protected ValueTask MarkInnerAsCompleted()
		{
			m_innerHasCompleted = true;

			// we don't need the iterator, so we can dispose of it immediately
			return Interlocked.Exchange(ref m_iterator, null)?.DisposeAsync() ?? default;
		}

		protected override async ValueTask<bool> OnFirstAsync()
		{
			// on the first call to MoveNext, we have to hook up with the source iterator

			IAsyncEnumerator<TSource>? iterator = null;
			try
			{
				iterator = StartInner();
				if (iterator == null!) return false;
				OnStarted(iterator);
				return true;
			}
			catch (Exception)
			{
				// whatever happens, make sure that we released the iterator...
				if (iterator != null)
				{
					await iterator.DisposeAsync().ConfigureAwait(false);
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

		protected override async ValueTask Cleanup()
		{
			try
			{
				OnStopped();
			}
			finally
			{
				await MarkInnerAsCompleted().ConfigureAwait(false);
			}
		}

	}

}
