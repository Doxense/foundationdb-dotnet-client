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
	using SnowBank.Linq.Async.Iterators;

	public static partial class AsyncQuery
	{

		/// <summary>Wraps a sequence of items into an async sequence of items</summary>
		/// <typeparam name="TSource">Type of elements of the inner sequence</typeparam>
		internal sealed class EnumerableAsyncQuery<TSource> : AsyncLinqIterator<TSource>
		{

			public EnumerableAsyncQuery(IEnumerable<TSource> source, CancellationToken ct)
			{
				this.Source = source;
				this.Cancellation = ct;
			}

			private IEnumerable<TSource> Source { get; }

			public override CancellationToken Cancellation { get; }

			/// <inheritdoc />
			protected override EnumerableAsyncQuery<TSource> Clone() => new(this.Source, this.Cancellation);

			private IEnumerator<TSource>? m_inner;

			/// <inheritdoc />
			protected override ValueTask<bool> OnFirstAsync()
			{
				var inner = this.Source.GetEnumerator();
				Contract.Debug.Assert(inner != null, "The underlying sequence returned an empty enumerator.");
				m_inner = inner;
				return new(true);
			}

			/// <inheritdoc />
			protected override ValueTask<bool> OnNextAsync()
			{
				var inner = m_inner;
				if (inner == null || !inner.MoveNext())
				{
					return this.Completed();
				}
				return new(Publish(inner.Current));
			}

			/// <inheritdoc />
			protected override ValueTask Cleanup()
			{
				m_inner?.Dispose();
				m_inner = null;
				return default;
			}

			/// <inheritdoc />
			public override Task<int> CountAsync() => Task.FromResult(this.Source.Count());

			/// <inheritdoc />
			public override Task<int> CountAsync(Func<TSource, bool> predicate) => Task.FromResult(this.Source.Count(predicate));

			/// <inheritdoc />
			public override Task<bool> AnyAsync() => Task.FromResult(this.Source.Any());

			/// <inheritdoc />
			public override Task<bool> AnyAsync(Func<TSource, bool> predicate) => Task.FromResult(this.Source.Any(predicate));

			/// <inheritdoc />
			public override Task<bool> AllAsync(Func<TSource, bool> predicate) => Task.FromResult(this.Source.All(predicate));

			/// <inheritdoc />
			public override Task<TSource> FirstOrDefaultAsync(TSource defaultValue) => Task.FromResult(this.Source.FirstOrDefault(defaultValue));

			/// <inheritdoc />
			public override Task<TSource> FirstOrDefaultAsync(Func<TSource, bool> predicate, TSource defaultValue) => Task.FromResult(this.Source.FirstOrDefault(predicate, defaultValue));

			/// <inheritdoc />
			public override Task<TSource> FirstAsync() => Task.FromResult(this.Source.First());

			/// <inheritdoc />
			public override Task<TSource> FirstAsync(Func<TSource, bool> predicate) => Task.FromResult(this.Source.First(predicate));

			/// <inheritdoc />
			public override Task<TSource> LastOrDefaultAsync(TSource defaultValue) => Task.FromResult(this.Source.LastOrDefault(defaultValue));

			/// <inheritdoc />
			public override Task<TSource> LastOrDefaultAsync(Func<TSource, bool> predicate, TSource defaultValue) => Task.FromResult(this.Source.LastOrDefault(predicate, defaultValue));

			/// <inheritdoc />
			public override Task<TSource> LastAsync() => Task.FromResult(this.Source.Last());

			/// <inheritdoc />
			public override Task<TSource> LastAsync(Func<TSource, bool> predicate) => Task.FromResult(this.Source.Last(predicate));

			/// <inheritdoc />
			public override Task<TSource> SingleAsync() => Task.FromResult(this.Source.Single());

			/// <inheritdoc />
			public override Task<TSource> SingleAsync(Func<TSource, bool> predicate) => Task.FromResult(this.Source.Single(predicate));

			/// <inheritdoc />
			public override Task<TSource[]> ToArrayAsync() => Task.FromResult(this.Source.ToArray());

			/// <inheritdoc />
			public override Task<List<TSource>> ToListAsync() => Task.FromResult(this.Source.ToList());

			/// <inheritdoc />
			public override Task<Dictionary<TKey, TSource>> ToDictionaryAsync<TKey>(Func<TSource, TKey> keySelector, IEqualityComparer<TKey>? comparer = null)
				=> Task.FromResult(this.Source.ToDictionary(keySelector, comparer));

			/// <inheritdoc />
			public override Task<Dictionary<TKey, TElement>> ToDictionaryAsync<TKey, TElement>(Func<TSource, TKey> keySelector, Func<TSource, TElement> elementSelector, IEqualityComparer<TKey>? comparer = null)
				=> Task.FromResult(this.Source.ToDictionary(keySelector, elementSelector, comparer));

			/// <inheritdoc />
			public override Task<HashSet<TSource>> ToHashSetAsync(IEqualityComparer<TSource>? comparer = null) => Task.FromResult(this.Source.ToHashSet(comparer));

			/// <inheritdoc />
			public override IAsyncLinqQuery<TSource> Skip(int count) => new EnumerableAsyncQuery<TSource>(this.Source.Skip(count), this.Cancellation);

			/// <inheritdoc />
			public override IAsyncLinqQuery<TSource> Take(int count) => new EnumerableAsyncQuery<TSource>(this.Source.Take(count), this.Cancellation);

			/// <inheritdoc />
			public override IAsyncLinqQuery<TSource> TakeWhile(Func<TSource, bool> condition) => new EnumerableAsyncQuery<TSource>(this.Source.TakeWhile(condition), this.Cancellation);

			/// <inheritdoc />
			public override IAsyncLinqQuery<TNew> Select<TNew>(Func<TSource, TNew> selector)
				=> new EnumerableAsyncQuery<TNew>(this.Source.Select(selector), this.Cancellation);

			/// <inheritdoc />
			public override IAsyncLinqQuery<TNew> Select<TNew>(Func<TSource, int, TNew> selector)
				=> new EnumerableAsyncQuery<TNew>(this.Source.Select(selector), this.Cancellation);

			/// <inheritdoc />
			public override IAsyncLinqQuery<TNew> SelectMany<TNew>(Func<TSource, IEnumerable<TNew>> selector)
				=> new EnumerableAsyncQuery<TNew>(this.Source.SelectMany(selector), this.Cancellation);

			/// <inheritdoc />
			public override IAsyncLinqQuery<TNew> SelectMany<TCollection, TNew>(Func<TSource, IEnumerable<TCollection>> collectionSelector, Func<TSource, TCollection, TNew> resultSelector)
				=> new EnumerableAsyncQuery<TNew>(this.Source.SelectMany(collectionSelector, resultSelector), this.Cancellation);

			/// <inheritdoc />
			public override IAsyncLinqQuery<TSource> Where(Func<TSource, bool> predicate) => new EnumerableAsyncQuery<TSource>(this.Source.Where(predicate), this.Cancellation);

			/// <inheritdoc />
			public override IAsyncLinqQuery<TSource> Where(Func<TSource, int, bool> predicate) => new EnumerableAsyncQuery<TSource>(this.Source.Where(predicate), this.Cancellation);

		}

	}

}
