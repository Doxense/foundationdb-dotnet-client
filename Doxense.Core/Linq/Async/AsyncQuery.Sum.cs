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
	using System.Numerics;

	public static partial class AsyncQuery
	{

		/// <summary>Returns the sum of all elements in the specified async sequence that satisfy a condition.</summary>
		public static Task<T> SumAsync<T>(this IAsyncQuery<T> source)
			where T : INumberBase<T>
		{
			Contract.NotNull(source);

			if (source is IAsyncLinqQuery<T> query)
			{
				return query.SumAsync();
			}

			if (typeof(T) == typeof(int)) return (Task<T>) (object) SumAsyncInt32Impl((IAsyncQuery<int>) source);
			if (typeof(T) == typeof(long)) return (Task<T>) (object) SumAsyncInt64Impl((IAsyncQuery<long>) source);
			if (typeof(T) == typeof(float)) return (Task<T>) (object) SumAsyncFloatImpl((IAsyncQuery<float>) source);
			if (typeof(T) == typeof(double)) return (Task<T>) (object) SumAsyncDoubleImpl((IAsyncQuery<double>) source);
			if (typeof(T) == typeof(decimal)) return (Task<T>) (object) SumAsyncDecimalImpl((IAsyncQuery<decimal>) source);

			return SumAsyncImpl(source);
		}

		internal static async Task<T> SumAsyncImpl<T>(IAsyncQuery<T> source) where T : INumberBase<T>
		{
			await using var iterator = source.GetAsyncEnumerator(AsyncIterationHint.All);

			T sum = T.Zero;
			while (await iterator.MoveNextAsync().ConfigureAwait(false))
			{
				sum = checked(sum + iterator.Current);
			}

			return sum;
		}

		/// <summary>Returns the sum of all elements in the specified async sequence that satisfy a condition.</summary>
		public static Task<T?> SumAsync<T>(this IAsyncQuery<T?> source)
			where T : struct, INumberBase<T>
		{
			Contract.NotNull(source);

			if (source is IAsyncLinqQuery<T?> query)
			{
				return query.SumAsync();
			}

			if (typeof(T) == typeof(int)) return (Task<T?>) (object) SumAsyncInt32Impl((IAsyncQuery<int?>) source);
			if (typeof(T) == typeof(long)) return (Task<T?>) (object) SumAsyncInt64Impl((IAsyncQuery<long?>) source);
			if (typeof(T) == typeof(float)) return (Task<T?>) (object) SumAsyncFloatImpl((IAsyncQuery<float?>) source);
			if (typeof(T) == typeof(double)) return (Task<T?>) (object) SumAsyncDoubleImpl((IAsyncQuery<double?>) source);
			if (typeof(T) == typeof(decimal)) return (Task<T?>) (object) SumAsyncDecimalImpl((IAsyncQuery<decimal?>) source);

			return SumAsyncNullableImpl(source);
		}

		internal static async Task<T?> SumAsyncNullableImpl<T>(IAsyncQuery<T?> source) where T : struct, INumberBase<T>
		{
			await using var iterator = source.GetAsyncEnumerator(AsyncIterationHint.All);

			T sum = T.Zero;
			while (await iterator.MoveNextAsync().ConfigureAwait(false))
			{
				var item = iterator.Current;
				if (item is not null)
				{
					sum = checked(sum + item.GetValueOrDefault());
				}
			}

			return sum;
		}

		/// <summary>Returns the sum of all elements in the specified async sequence.</summary>
		public static Task<int> SumAsync(this IAsyncQuery<int> source)
		{
			Contract.NotNull(source);

			if (source is IAsyncLinqQuery<int> query)
			{
				return query.SumAsync();
			}

			return SumAsyncInt32Impl(source);
		}

		internal static async Task<int> SumAsyncInt32Impl(IAsyncQuery<int> source)
		{
			await using var iterator = source.GetAsyncEnumerator(AsyncIterationHint.All);

			int sum = 0;
			while (await iterator.MoveNextAsync().ConfigureAwait(false))
			{
				checked { sum += iterator.Current; }
			}

			return sum;
		}

		/// <summary>Returns the sum of all elements in the specified async sequence.</summary>
		public static Task<int?> SumAsync(this IAsyncQuery<int?> source)
		{
			Contract.NotNull(source);

			if (source is IAsyncLinqQuery<int?> query)
			{
				return query.SumAsync();
			}

			return SumAsyncInt32Impl(source);
		}

		internal static async Task<int?> SumAsyncInt32Impl(IAsyncQuery<int?> source)
		{
			await using var iterator = source.GetAsyncEnumerator(AsyncIterationHint.All);

			int sum = 0;
			while (await iterator.MoveNextAsync().ConfigureAwait(false))
			{
				var item = iterator.Current;
				if (item is not null)
				{
					sum = checked(sum + item.GetValueOrDefault());
				}
			}

			return sum;
		}

		/// <summary>Returns the sum of all elements in the specified async sequence.</summary>
		public static Task<long> SumAsync(this IAsyncQuery<long> source)
		{
			Contract.NotNull(source);

			if (source is IAsyncLinqQuery<long> query)
			{
				return query.SumAsync();
			}

			return SumAsyncInt64Impl(source);
		}

		internal static async Task<long> SumAsyncInt64Impl(IAsyncQuery<long> source)
		{
			await using var iterator = source.GetAsyncEnumerator(AsyncIterationHint.All);

			long sum = 0;
			while (await iterator.MoveNextAsync().ConfigureAwait(false))
			{
				checked { sum += iterator.Current; }
			}

			return sum;
		}

		/// <summary>Returns the sum of all elements in the specified async sequence.</summary>
		public static Task<long?> SumAsync(this IAsyncQuery<long?> source)
		{
			Contract.NotNull(source);

			if (source is IAsyncLinqQuery<long?> query)
			{
				return query.SumAsync();
			}

			return SumAsyncInt64Impl(source);
		}

		internal static async Task<long?> SumAsyncInt64Impl(IAsyncQuery<long?> source)
		{
			await using var iterator = source.GetAsyncEnumerator(AsyncIterationHint.All);

			long sum = 0;
			while (await iterator.MoveNextAsync().ConfigureAwait(false))
			{
				var item = iterator.Current;
				if (item is not null)
				{
					sum = checked(sum + item.GetValueOrDefault());
				}
			}

			return sum;
		}

		/// <summary>Returns the sum of all elements in the specified async sequence.</summary>
		public static Task<float> SumAsync(this IAsyncQuery<float> source)
		{
			Contract.NotNull(source);

			if (source is IAsyncLinqQuery<float> query)
			{
				return query.SumAsync();
			}

			return SumAsyncFloatImpl(source);
		}

		internal static async Task<float> SumAsyncFloatImpl(IAsyncQuery<float> source)
		{
			await using var iterator = source.GetAsyncEnumerator(AsyncIterationHint.All);

			//note: like Enumerable and AsyncEnumerable, we will also use a double as the accumulator (to reduce precision loss)

			double sum = 0;
			while (await iterator.MoveNextAsync().ConfigureAwait(false))
			{
				sum += iterator.Current;
			}

			return (float) sum;
		}

		/// <summary>Returns the sum of all elements in the specified async sequence.</summary>
		public static Task<float?> SumAsync(this IAsyncQuery<float?> source)
		{
			Contract.NotNull(source);

			if (source is IAsyncLinqQuery<float?> query)
			{
				return query.SumAsync();
			}

			return SumAsyncFloatImpl(source);
		}

		internal static async Task<float?> SumAsyncFloatImpl(IAsyncQuery<float?> source)
		{
			await using var iterator = source.GetAsyncEnumerator(AsyncIterationHint.All);

			//note: like Enumerable and AsyncEnumerable, we will also use a double as the accumulator (to reduce precision loss)
			double sum = 0;
			while (await iterator.MoveNextAsync().ConfigureAwait(false))
			{
				var item = iterator.Current;
				if (item is not null)
				{
					sum += item.GetValueOrDefault();
				}
			}

			return (float) sum;
		}

		/// <summary>Returns the sum of all elements in the specified async sequence.</summary>
		public static Task<double> SumAsync(this IAsyncQuery<double> source)
		{
			Contract.NotNull(source);

			if (source is IAsyncLinqQuery<double> query)
			{
				return query.SumAsync();
			}

			return SumAsyncDoubleImpl(source);
		}

		internal static async Task<double> SumAsyncDoubleImpl(IAsyncQuery<double> source)
		{
			await using var iterator = source.GetAsyncEnumerator(AsyncIterationHint.All);

			double sum = 0;
			while (await iterator.MoveNextAsync().ConfigureAwait(false))
			{
				sum += iterator.Current;
			}

			return sum;
		}

		/// <summary>Returns the sum of all elements in the specified async sequence.</summary>
		public static Task<double?> SumAsync(this IAsyncQuery<double?> source)
		{
			Contract.NotNull(source);

			if (source is IAsyncLinqQuery<double?> query)
			{
				return query.SumAsync();
			}

			return SumAsyncDoubleImpl(source);
		}

		internal static async Task<double?> SumAsyncDoubleImpl(IAsyncQuery<double?> source)
		{
			await using var iterator = source.GetAsyncEnumerator(AsyncIterationHint.All);

			double sum = 0;
			while (await iterator.MoveNextAsync().ConfigureAwait(false))
			{
				var item = iterator.Current;
				if (item is not null)
				{
					sum += item.GetValueOrDefault();
				}
			}

			return sum;
		}

		/// <summary>Returns the sum of all elements in the specified async sequence.</summary>
		public static Task<decimal> SumAsync(this IAsyncQuery<decimal> source)
		{
			Contract.NotNull(source);

			if (source is IAsyncLinqQuery<decimal> query)
			{
				return query.SumAsync();
			}

			return SumAsyncDecimalImpl(source);
		}

		internal static async Task<decimal> SumAsyncDecimalImpl(IAsyncQuery<decimal> source)
		{
			await using var iterator = source.GetAsyncEnumerator(AsyncIterationHint.All);

			decimal sum = 0;
			while (await iterator.MoveNextAsync().ConfigureAwait(false))
			{
				sum += iterator.Current;
			}

			return sum;
		}

		/// <summary>Returns the sum of all elements in the specified async sequence.</summary>
		public static Task<decimal?> SumAsync(this IAsyncQuery<decimal?> source)
		{
			Contract.NotNull(source);

			if (source is IAsyncLinqQuery<decimal?> query)
			{
				return query.SumAsync();
			}

			return SumAsyncDecimalImpl(source);
		}

		internal static async Task<decimal?> SumAsyncDecimalImpl(IAsyncQuery<decimal?> source)
		{
			await using var iterator = source.GetAsyncEnumerator(AsyncIterationHint.All);

			decimal sum = 0;
			while (await iterator.MoveNextAsync().ConfigureAwait(false))
			{
				var item = iterator.Current;
				if (item is not null)
				{
					sum += item.GetValueOrDefault();
				}
			}

			return sum;
		}

	}

}
