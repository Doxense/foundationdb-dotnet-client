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
	using System.Collections.Immutable;
	using SnowBank.Linq.Async.Iterators;

	public static partial class AsyncQuery
	{

		/// <summary>A query that returns nothing</summary>
		internal sealed class EmptyQuery<TSource> : AsyncLinqIterator<TSource>
		{
			public static readonly EmptyQuery<TSource> Default = new();

			private EmptyQuery()
			{ }

			public override CancellationToken Cancellation => CancellationToken.None;

			/// <inheritdoc />
			protected override EmptyQuery<TSource> Clone() => this;

			/// <inheritdoc />
			protected override ValueTask<bool> OnFirstAsync() => new(false);

			/// <inheritdoc />
			protected override ValueTask<bool> OnNextAsync() => Completed();

			/// <inheritdoc />
			protected override ValueTask Cleanup() => default;

			/// <inheritdoc />
			public override Task<bool> AnyAsync() => Task.FromResult(false);

			/// <inheritdoc />
			public override Task<bool> AnyAsync(Func<TSource, bool> predicate) => Task.FromResult(false);

			/// <inheritdoc />
			public override Task<bool> AnyAsync(Func<TSource, CancellationToken, Task<bool>> predicate) => Task.FromResult(false);

			/// <inheritdoc />
			public override Task<bool> AllAsync(Func<TSource, bool> predicate) => Task.FromResult(true);

			/// <inheritdoc />
			public override Task<bool> AllAsync(Func<TSource, CancellationToken, Task<bool>> predicate) => Task.FromResult(true);

			/// <inheritdoc />
			public override Task<int> CountAsync() => Task.FromResult(0);

			/// <inheritdoc />
			public override Task<int> CountAsync(Func<TSource, bool> predicate) => Task.FromResult(0);

			/// <inheritdoc />
			public override Task<int> CountAsync(Func<TSource, CancellationToken, Task<bool>> predicate) => Task.FromResult(0);

			/// <inheritdoc />
			public override IAsyncLinqQuery<TSource> Skip(int count) => this;

			/// <inheritdoc />
			public override IAsyncLinqQuery<TSource> Take(int count) => this;

			/// <inheritdoc />
			public override IAsyncLinqQuery<TSource> TakeWhile(Func<TSource, bool> condition) => this;

			/// <inheritdoc />
			public override Task<TSource[]> ToArrayAsync() => Task.FromResult(Array.Empty<TSource>());

			/// <inheritdoc />
			public override Task<List<TSource>> ToListAsync() => Task.FromResult(new List<TSource>());

			/// <inheritdoc />
			public override Task<HashSet<TSource>> ToHashSetAsync(IEqualityComparer<TSource>? comparer = null) => Task.FromResult(new HashSet<TSource>(comparer));

			/// <inheritdoc />
			public override Task<ImmutableArray<TSource>> ToImmutableArrayAsync() => Task.FromResult(ImmutableArray<TSource>.Empty);

			/// <inheritdoc />
			public override Task<Dictionary<TKey, TSource>> ToDictionaryAsync<TKey>(Func<TSource, TKey> keySelector, IEqualityComparer<TKey>? comparer = null) => Task.FromResult(new Dictionary<TKey, TSource>(comparer));

			/// <inheritdoc />
			public override Task<Dictionary<TKey, TElement>> ToDictionaryAsync<TKey, TElement>(Func<TSource, TKey> keySelector, Func<TSource, TElement> elementSelector, IEqualityComparer<TKey>? comparer = null) => Task.FromResult(new Dictionary<TKey, TElement>(comparer));

			/// <inheritdoc />
			public override Task<TSource> FirstOrDefaultAsync(TSource defaultValue) => Task.FromResult(defaultValue);

			/// <inheritdoc />
			public override Task<TSource> FirstOrDefaultAsync(Func<TSource, bool> predicate, TSource defaultValue) => Task.FromResult(defaultValue);

			/// <inheritdoc />
			public override Task<TSource> FirstOrDefaultAsync(Func<TSource, CancellationToken, Task<bool>> predicate, TSource defaultValue) => Task.FromResult(defaultValue);

			/// <inheritdoc />
			public override Task<TSource> FirstAsync() => Task.FromException<TSource>(ErrorNoElements());

			/// <inheritdoc />
			public override Task<TSource> FirstAsync(Func<TSource, bool> predicate) => Task.FromException<TSource>(ErrorNoElements());

			/// <inheritdoc />
			public override Task<TSource> FirstAsync(Func<TSource, CancellationToken, Task<bool>> predicate) => Task.FromException<TSource>(ErrorNoElements());

			/// <inheritdoc />
			public override Task<TSource> LastOrDefaultAsync(TSource defaultValue) => Task.FromResult(defaultValue);

			/// <inheritdoc />
			public override Task<TSource> LastOrDefaultAsync(Func<TSource, bool> predicate, TSource defaultValue) => Task.FromResult(defaultValue);

			/// <inheritdoc />
			public override Task<TSource> LastOrDefaultAsync(Func<TSource, CancellationToken, Task<bool>> predicate, TSource defaultValue) => Task.FromResult(defaultValue);

			/// <inheritdoc />
			public override Task<TSource> LastAsync() => Task.FromException<TSource>(ErrorNoElements());

			/// <inheritdoc />
			public override Task<TSource> LastAsync(Func<TSource, bool> predicate) => Task.FromException<TSource>(ErrorNoElements());

			/// <inheritdoc />
			public override Task<TSource> LastAsync(Func<TSource, CancellationToken, Task<bool>> predicate) => Task.FromException<TSource>(ErrorNoElements());

			/// <inheritdoc />
			public override Task<TSource> SingleAsync() => Task.FromException<TSource>(ErrorNoElements());

			/// <inheritdoc />
			public override Task<TSource> SingleAsync(Func<TSource, bool> predicate) => Task.FromException<TSource>(ErrorNoElements());

			/// <inheritdoc />
			public override Task<TSource> SingleAsync(Func<TSource, CancellationToken, Task<bool>> predicate) => Task.FromException<TSource>(ErrorNoElements());

			/// <inheritdoc />
			public override Task<TSource> SingleOrDefaultAsync(TSource defaultValue) => Task.FromResult(defaultValue);

			/// <inheritdoc />
			public override Task<TSource> SingleOrDefaultAsync(Func<TSource, bool> predicate, TSource defaultValue) => Task.FromResult(defaultValue);

			/// <inheritdoc />
			public override Task<TSource> SingleOrDefaultAsync(Func<TSource, CancellationToken, Task<bool>> predicate, TSource defaultValue) => Task.FromResult(defaultValue);


			/// <inheritdoc />
			public override IAsyncLinqQuery<TSource> Where(Func<TSource, bool> predicate) => this;

			/// <inheritdoc />
			public override IAsyncLinqQuery<TSource> Where(Func<TSource, int, bool> predicate) => this;

			/// <inheritdoc />
			public override IAsyncLinqQuery<TSource> Where(Func<TSource, CancellationToken, Task<bool>> asyncPredicate) => this;

			/// <inheritdoc />
			public override IAsyncLinqQuery<TSource> Where(Func<TSource, int, CancellationToken, Task<bool>> asyncPredicate) => this;

			/// <inheritdoc />
			public override IAsyncLinqQuery<TNew> Select<TNew>(Func<TSource, TNew> selector) => EmptyQuery<TNew>.Default;

			/// <inheritdoc />
			public override IAsyncLinqQuery<TNew> Select<TNew>(Func<TSource, int, TNew> selector) => EmptyQuery<TNew>.Default;

			/// <inheritdoc />
			public override IAsyncLinqQuery<TNew> Select<TNew>(Func<TSource, CancellationToken, Task<TNew>> asyncSelector) => EmptyQuery<TNew>.Default;

			/// <inheritdoc />
			public override IAsyncLinqQuery<TNew> Select<TNew>(Func<TSource, int, CancellationToken, Task<TNew>> asyncSelector) => EmptyQuery<TNew>.Default;

			/// <inheritdoc />
			public override IAsyncLinqQuery<TNew> SelectMany<TNew>(Func<TSource, IEnumerable<TNew>> selector) => EmptyQuery<TNew>.Default;

			/// <inheritdoc />
			public override IAsyncLinqQuery<TNew> SelectMany<TNew>(Func<TSource, CancellationToken, Task<IEnumerable<TNew>>> asyncSelector) => EmptyQuery<TNew>.Default;

			/// <inheritdoc />
			public override IAsyncLinqQuery<TNew> SelectMany<TCollection, TNew>(Func<TSource, IEnumerable<TCollection>> collectionSelector, Func<TSource, TCollection, TNew> resultSelector) => EmptyQuery<TNew>.Default;

			/// <inheritdoc />
			public override IAsyncLinqQuery<TNew> SelectMany<TCollection, TNew>(Func<TSource, CancellationToken, Task<IEnumerable<TCollection>>> asyncCollectionSelector, Func<TSource, TCollection, TNew> resultSelector) => EmptyQuery<TNew>.Default;

			/// <inheritdoc />
			public override IAsyncLinqQuery<TNew> SelectMany<TNew>(Func<TSource, IAsyncEnumerable<TNew>> selector) => EmptyQuery<TNew>.Default;

			/// <inheritdoc />
			public override IAsyncLinqQuery<TNew> SelectMany<TNew>(Func<TSource, IAsyncQuery<TNew>> selector) => EmptyQuery<TNew>.Default;

		}

		/// <summary>A query that will return only a single element</summary>
		internal sealed class SingletonQuery<TElement> : AsyncLinqIterator<TElement>
		{

			private Delegate? Factory { get; }

			private bool Called { get; set; }

			private SingletonQuery(Delegate lambda, CancellationToken ct)
			{
				Contract.Debug.Requires(lambda != null);
				this.Factory = lambda;
				this.Cancellation = ct;
			}

			public SingletonQuery(Func<TElement> lambda, CancellationToken ct)
				: this((Delegate) lambda, ct)
			{ }

			public SingletonQuery(Func<Task<TElement>> lambda, CancellationToken ct)
				: this((Delegate) lambda, ct)
			{ }

			public SingletonQuery(Func<ValueTask<TElement>> lambda, CancellationToken ct)
				: this((Delegate) lambda, ct)
			{ }

			public SingletonQuery(Func<CancellationToken, Task<TElement>> lambda, CancellationToken ct)
				: this((Delegate) lambda, ct)
			{ }

			public SingletonQuery(Func<CancellationToken, ValueTask<TElement>> lambda, CancellationToken ct)
				: this((Delegate) lambda, ct)
			{ }

			public override CancellationToken Cancellation { get; }

			/// <inheritdoc />
			protected override SingletonQuery<TElement> Clone() => new(this.Factory!, this.Cancellation);

			/// <inheritdoc />
			protected override ValueTask<bool> OnFirstAsync()
			{
				if (this.Factory == null)
				{
					return new(false);
				}

				this.Called = false;
				return new(true);
			}

			/// <inheritdoc />
			protected override async ValueTask<bool> OnNextAsync()
			{
				var factory = this.Factory;
				if (factory == null || this.Called)
				{
					return await this.Completed().ConfigureAwait(false);
				}

				this.Called = true;
				var current = factory switch
				{
					Func<TElement> fn => fn(),
					Func<Task<TElement>> fn => await fn().ConfigureAwait(false),
					Func<CancellationToken, Task<TElement>> fn => await fn(this.Cancellation).ConfigureAwait(false),
					Func<ValueTask<TElement>> fn => await fn().ConfigureAwait(false),
					Func<CancellationToken, ValueTask<TElement>> fn => await fn(this.Cancellation).ConfigureAwait(false),
					_ => throw new InvalidOperationException("Unsupported delegate type")
				};

				return Publish(current);
			}

			/// <inheritdoc />
			protected override ValueTask Cleanup()
			{
				return default;
			}

		}

	}

}
