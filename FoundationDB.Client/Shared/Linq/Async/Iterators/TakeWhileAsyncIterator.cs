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

namespace Doxense.Linq.Async.Iterators
{
	using System;
	using System.Collections.Generic;
	using System.Threading.Tasks;
	using Doxense.Diagnostics.Contracts;

	/// <summary>Reads an async sequence of items until a condition becomes false</summary>
	/// <typeparam name="TSource">Type of elements of the async sequence</typeparam>
	public sealed class TakeWhileAsyncIterator<TSource> : AsyncFilterIterator<TSource, TSource>
	{
		private readonly Func<TSource, bool> m_condition;
		//TODO: also accept a Func<TSource, CT, Task<bool>> ?

		public TakeWhileAsyncIterator(IAsyncEnumerable<TSource> source, Func<TSource, bool> condition)
			: base(source)
		{
			Contract.Requires(condition != null);

			m_condition = condition;
		}

		protected override AsyncIterator<TSource> Clone()
		{
			return new TakeWhileAsyncIterator<TSource>(m_source, m_condition);
		}

		protected override async ValueTask<bool> OnNextAsync()
		{
			var iterator = m_iterator;
			Contract.Requires(iterator != null);

			while (!m_ct.IsCancellationRequested)
			{
				if (!await iterator.MoveNextAsync().ConfigureAwait(false))
				{ // completed
					return await Completed();
				}

				if (m_ct.IsCancellationRequested) break;

				TSource current = iterator.Current;
				if (!m_condition(current))
				{ // we need to stop
					return await Completed();
				}

				return Publish(current);
			}
			return await Canceled();
		}

	}

}

#endif
