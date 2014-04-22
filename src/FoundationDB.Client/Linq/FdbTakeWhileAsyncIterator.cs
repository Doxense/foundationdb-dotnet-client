﻿#region BSD Licence
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

	/// <summary>Reads an async sequence of items until a condition becomes false</summary>
	/// <typeparam name="TSource">Type of elements of the async sequence</typeparam>
	internal sealed class FdbTakeWhileAsyncIterator<TSource> : FdbAsyncFilter<TSource, TSource>
	{
		private readonly Func<TSource, bool> m_condition;

		public FdbTakeWhileAsyncIterator(IFdbAsyncEnumerable<TSource> source, Func<TSource, bool> condition)
			: base(source)
		{
			m_condition = condition;
		}

		protected override FdbAsyncIterator<TSource> Clone()
		{
			return new FdbTakeWhileAsyncIterator<TSource>(m_source, m_condition);
		}

		protected override async Task<bool> OnNextAsync(CancellationToken ct)
		{
			while (!ct.IsCancellationRequested)
			{
				if (!await m_iterator.MoveNext(ct).ConfigureAwait(false))
				{ // completed
					return Completed();
				}

				if (ct.IsCancellationRequested) break;

				TSource current = m_iterator.Current;
				if (!m_condition(current))
				{ // we need to stop
					return Completed();
				}

				return Publish(current);
			}
			return Canceled(ct);
		}

	}

}
