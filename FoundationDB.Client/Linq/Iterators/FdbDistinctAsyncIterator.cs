#region BSD Licence
/* Copyright (c) 2013-2014, Doxense SAS
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
	using FoundationDB.Client.Utils;
	using JetBrains.Annotations;
	using System;
	using System.Collections.Generic;
	using System.Threading;
	using System.Threading.Tasks;

	/// <summary>Filters duplicate items from an async sequence</summary>
	/// <typeparam name="TSource">Type of elements of the async sequence</typeparam>
	internal sealed class FdbDistinctAsyncIterator<TSource> : FdbAsyncFilterIterator<TSource, TSource>
	{

		private readonly IEqualityComparer<TSource> m_comparer;
		private HashSet<TSource> m_set;

		public FdbDistinctAsyncIterator([NotNull] IFdbAsyncEnumerable<TSource> source, IEqualityComparer<TSource> comparer)
			: base(source)
		{
			Contract.Requires(comparer != null);

			m_comparer = comparer;
		}

		protected override FdbAsyncIterator<TSource> Clone()
		{
			return new FdbDistinctAsyncIterator<TSource>(m_source, m_comparer);
		}

		protected override Task<bool> OnFirstAsync(CancellationToken ct)
		{
			// we start with an empty set...
			m_set = new HashSet<TSource>(m_comparer);

			return base.OnFirstAsync(ct);
		}

		protected override async Task<bool> OnNextAsync(CancellationToken cancellationToken)
		{
			while (!cancellationToken.IsCancellationRequested)
			{
				if (!await m_iterator.MoveNext(cancellationToken).ConfigureAwait(false))
				{ // completed
					m_set = null;
					return Completed();
				}

				if (cancellationToken.IsCancellationRequested) break;

				TSource current = m_iterator.Current;
				if (!m_set.Add(current))
				{ // this item has already been seen
					continue;
				}

				return Publish(current);
			}

			m_set = null;
			return Canceled(cancellationToken);
		}

		public override async Task ExecuteAsync(Action<TSource> handler, CancellationToken ct)
		{
			if (handler == null) throw new ArgumentNullException("handler");

			if (ct.IsCancellationRequested) ct.ThrowIfCancellationRequested();

			var mode = m_mode;
			if (mode == FdbAsyncMode.Head) mode = FdbAsyncMode.Iterator;

			using (var iter = m_source.GetEnumerator(mode))
			{
				var set = new HashSet<TSource>(m_comparer);

				while (!ct.IsCancellationRequested && (await iter.MoveNext(ct).ConfigureAwait(false)))
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
			if (asyncHandler == null) throw new ArgumentNullException("asyncHandler");

			if (ct.IsCancellationRequested) ct.ThrowIfCancellationRequested();

			var mode = m_mode;
			if (mode == FdbAsyncMode.Head) mode = FdbAsyncMode.Iterator;

			using (var iter = m_source.GetEnumerator(mode))
			{
				var set = new HashSet<TSource>(m_comparer);

				while (!ct.IsCancellationRequested && (await iter.MoveNext(ct).ConfigureAwait(false)))
				{
					var current = iter.Current;
					if (set.Add(current))
					{ // first occurence of this item
						await asyncHandler(current, ct).ConfigureAwait(false);
					}
				}
			}

			if (ct.IsCancellationRequested) ct.ThrowIfCancellationRequested();
		}

	}

}
