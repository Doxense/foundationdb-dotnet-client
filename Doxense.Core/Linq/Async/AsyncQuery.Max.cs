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

		/// <summary>Returns the largest value in the specified async sequence</summary>
		public static Task<T?> MaxAsync<T>(this IAsyncQuery<T> source, IComparer<T>? comparer = null)
		{
			Contract.NotNull(source);
			comparer ??= Comparer<T>.Default;

			if (source is IAsyncLinqQuery<T> query)
			{
				return query.MaxAsync(comparer);
			}

			return MaxAsyncImpl(source, comparer);
		}

		internal static async Task<T?> MaxAsyncImpl<T>(IAsyncQuery<T> source, IComparer<T> comparer)
		{
			await using var iterator = source.GetAsyncEnumerator(AsyncIterationHint.All);

			if (default(T) is null)
			{
				var max = default(T);
				while (await iterator.MoveNextAsync().ConfigureAwait(false))
				{
					var candidate = iterator.Current;
					if (candidate is not null && (max is null || comparer.Compare(candidate, candidate) > 0))
					{
						max = candidate;
					}
				}
				return max;
			}
			else
			{
				// get the first
				if (!await iterator.MoveNextAsync().ConfigureAwait(false))
				{
					throw ErrorNoElements();
				}
				var max = iterator.Current;

				// compare with the rest
				while (await iterator.MoveNextAsync().ConfigureAwait(false))
				{
					var candidate = iterator.Current;
					if (comparer.Compare(candidate, max) > 0)
					{
						max = candidate;
					}
				}

				return max;
			}
		}

	}

}
