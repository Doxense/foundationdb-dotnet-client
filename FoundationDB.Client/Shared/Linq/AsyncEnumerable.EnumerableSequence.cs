#region BSD License
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

namespace Doxense.Linq
{
	using System;
	using System.Collections.Generic;
	using System.Threading;
	using Doxense.Diagnostics.Contracts;

	public static partial class AsyncEnumerable
	{

		/// <summary>Wraps a sequence of items into an async sequence of items</summary>
		/// <typeparam name="TSource">Type of elements of the inner sequence</typeparam>
		/// <typeparam name="TResult">Type of elements of the outer async sequence</typeparam>
		internal sealed class EnumerableSequence<TSource, TResult> : IConfigurableAsyncEnumerable<TResult>
		{
			public readonly IEnumerable<TSource> Source;
			public readonly Func<IEnumerator<TSource>, CancellationToken, IAsyncEnumerator<TResult>> Factory;

			public EnumerableSequence(IEnumerable<TSource> source, Func<IEnumerator<TSource>, CancellationToken, IAsyncEnumerator<TResult>> factory)
			{
				this.Source = source;
				this.Factory = factory;
			}

			public IAsyncEnumerator<TResult> GetAsyncEnumerator(CancellationToken ct)
			{
				return GetAsyncEnumerator(ct, AsyncIterationHint.Default);
			}

			public IAsyncEnumerator<TResult> GetAsyncEnumerator(CancellationToken ct, AsyncIterationHint _)
			{
				IEnumerator<TSource> inner = null;
				try
				{
					inner = this.Source.GetEnumerator();
					Contract.Assert(inner != null, "The underlying sequence returned an empty enumerator.");

					var outer = this.Factory(inner, ct);
					if (outer == null) throw new InvalidOperationException("The async factory returned en empty enumerator.");

					return outer;
				}
				catch (Exception)
				{
					//make sure that the inner iterator gets disposed if something went wrong
					inner?.Dispose();
					throw;
				}
			}
		}

	}
}

#endif
