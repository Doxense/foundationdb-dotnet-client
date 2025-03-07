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

namespace SnowBank.Linq
{

	public static partial class AsyncQuery
	{

		#region Aggregate...

		/// <summary>Applies an accumulator function over an async sequence.</summary>
		public static async Task<TSource> AggregateAsync<TSource>(this IAsyncQuery<TSource> source, [InstantHandle] Func<TSource, TSource, TSource> aggregator)
		{
			Contract.NotNull(source);
			Contract.NotNull(aggregator);

			await using (var iterator = source.GetAsyncEnumerator(AsyncIterationHint.All))
			{
				Contract.Debug.Assert(iterator != null, "The sequence returned a null async iterator");

				if (!(await iterator.MoveNextAsync().ConfigureAwait(false)))
				{
					throw ErrorNoElements();
				}

				var item = iterator.Current;
				while (await iterator.MoveNextAsync().ConfigureAwait(false))
				{
					item = aggregator(item, iterator.Current);
				}

				return item;
			}
		}

		/// <summary>Applies an accumulator function over an async sequence.</summary>
		public static async Task<TAggregate> AggregateAsync<TSource, TAggregate>(this IAsyncQuery<TSource> source, TAggregate seed, [InstantHandle] Func<TAggregate, TSource, TAggregate> aggregator)
		{
			Contract.NotNull(source);
			Contract.NotNull(aggregator);

			//TODO: optimize this to not have to allocate lambdas!
			var accumulate = seed;
			await ForEachAsync(source, (x) => { accumulate = aggregator(accumulate, x); }).ConfigureAwait(false);
			return accumulate;
		}

		/// <summary>Applies an accumulator function over an async sequence.</summary>
		public static async Task<TAggregate> AggregateAsync<TSource, TAggregate>(this IAsyncQuery<TSource> source, TAggregate seed, [InstantHandle] Action<TAggregate, TSource> aggregator)
		{
			Contract.NotNull(source);
			Contract.NotNull(aggregator);

			await ForEachAsync(
				source,
				(Fn: aggregator, Acc: seed),
				static (s, x) => s.Fn(s.Acc, x)
			).ConfigureAwait(false);

			return seed;
		}

		/// <summary>Applies an accumulator function over an async sequence.</summary>
		public static async Task<TResult> AggregateAsync<TSource, TAggregate, TResult>(this IAsyncQuery<TSource> source, TAggregate seed, [InstantHandle] Func<TAggregate, TSource, TAggregate> aggregator, [InstantHandle] Func<TAggregate, TResult> resultSelector)
		{
			Contract.NotNull(source);
			Contract.NotNull(aggregator);
			Contract.NotNull(resultSelector);

			var accumulate = seed;
			await ForEachAsync(source, (x) => { accumulate = aggregator(accumulate, x); }).ConfigureAwait(false);
			return resultSelector(accumulate);
		}

		/// <summary>Applies an accumulator function over an async sequence.</summary>
		public static async Task<TResult> AggregateAsync<TSource, TAggregate, TResult>(this IAsyncQuery<TSource> source, TAggregate seed, [InstantHandle] Action<TAggregate, TSource> aggregator, [InstantHandle] Func<TAggregate, TResult> resultSelector)
		{
			Contract.NotNull(source);
			Contract.NotNull(aggregator);
			Contract.NotNull(resultSelector);

			await ForEachAsync(
				source,
				(Fn: aggregator, Acc: seed),
				static (s, x) => s.Fn(s.Acc, x)
			).ConfigureAwait(false);

			return resultSelector(seed);
		}

		#endregion

	}

}
