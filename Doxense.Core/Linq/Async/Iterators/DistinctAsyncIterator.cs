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

namespace Doxense.Linq.Async.Iterators
{
	using System;
	using System.Collections.Generic;
	using System.Threading;
	using System.Threading.Tasks;
	using Doxense.Diagnostics.Contracts;

	/// <summary>Filters duplicate items from an async sequence</summary>
	/// <typeparam name="TSource">Type of elements of the async sequence</typeparam>
	public sealed class DistinctAsyncIterator<TSource> : AsyncFilterIterator<TSource, TSource>
	{

		private readonly IEqualityComparer<TSource> m_comparer;
		private HashSet<TSource>? m_set;

		public DistinctAsyncIterator(IAsyncEnumerable<TSource> source, IEqualityComparer<TSource> comparer)
			: base(source)
		{
			Contract.Debug.Requires(comparer != null);

			m_comparer = comparer;
		}

		protected override AsyncIterator<TSource> Clone()
		{
			return new DistinctAsyncIterator<TSource>(m_source, m_comparer);
		}

		protected override ValueTask<bool> OnFirstAsync()
		{
			// we start with an empty set...
			m_set = new HashSet<TSource>(m_comparer);

			return base.OnFirstAsync();
		}

		protected override async ValueTask<bool> OnNextAsync()
		{
			var iterator = m_iterator;
			var set = m_set;
			var ct = m_ct;
			Contract.Debug.Requires(iterator != null && set != null);

			while (!ct.IsCancellationRequested)
			{
				if (!await iterator.MoveNextAsync().ConfigureAwait(false))
				{ // completed
					m_set = null;
					return await Completed().ConfigureAwait(false);
				}

				if (ct.IsCancellationRequested) break;

				TSource current = iterator.Current;
				if (!set.Add(current))
				{ // this item has already been seen
					continue;
				}

				return Publish(current);
			}

			m_set = null;
			return await Canceled().ConfigureAwait(false);
		}

		public override async Task ExecuteAsync(Action<TSource> handler, CancellationToken ct)
		{
			Contract.NotNull(handler);

			if (ct.IsCancellationRequested) ct.ThrowIfCancellationRequested();

			var mode = m_mode;
			if (mode == AsyncIterationHint.Head) mode = AsyncIterationHint.Iterator;

			await using (var iter = m_source is IConfigurableAsyncEnumerable<TSource> configurable ? configurable.GetAsyncEnumerator(ct, mode) : m_source.GetAsyncEnumerator(ct))
			{
				var set = new HashSet<TSource>(m_comparer);

				while (!ct.IsCancellationRequested && (await iter.MoveNextAsync().ConfigureAwait(false)))
				{
					var current = iter.Current;
					if (set.Add(current))
					{ // first occurrence of this item
						handler(current);
					}
				}
			}

			if (ct.IsCancellationRequested) ct.ThrowIfCancellationRequested();

		}

		public override async Task ExecuteAsync(Func<TSource, CancellationToken, Task> asyncHandler, CancellationToken ct)
		{
			Contract.NotNull(asyncHandler);

			if (ct.IsCancellationRequested) ct.ThrowIfCancellationRequested();

			var mode = m_mode;
			if (mode == AsyncIterationHint.Head) mode = AsyncIterationHint.Iterator;

			await using (var iter = m_source is IConfigurableAsyncEnumerable<TSource> configurable ? configurable.GetAsyncEnumerator(ct, mode) : m_source.GetAsyncEnumerator(ct))
			{
				var set = new HashSet<TSource>(m_comparer);

				while (!ct.IsCancellationRequested && (await iter.MoveNextAsync().ConfigureAwait(false)))
				{
					var current = iter.Current;
					if (set.Add(current))
					{ // first occurence of this item
						await asyncHandler(current, ct).ConfigureAwait(false);
					}
				}
			}

			ct.ThrowIfCancellationRequested();
		}

	}

}
