// #region Copyright (c) 2023-2024 SnowBank SAS
// //
// // All rights are reserved. Reproduction or transmission in whole or in part, in
// // any form or by any means, electronic, mechanical or otherwise, is prohibited
// // without the prior written consent of the copyright owner.
// //
// #endregion

namespace SnowBank.Linq
{
	using Async.Iterators;

	public static partial class AsyncQuery
	{

		public static IAsyncLinqQuery<T> ToAsyncQuery<T>(this IAsyncEnumerable<T> source, CancellationToken ct = default)
		{
			if (source is IAsyncLinqQuery<T> query)
			{
				if (ct.CanBeCanceled && !ct.Equals(query.Cancellation))
				{
					throw new InvalidOperationException("The CancellationToken that is passed to ToAsyncQuery() MUST be the same as the source!");
				}
				return query;
			}
			return new AsyncEnumerableQuery<T>(source, ct);
		}

		/// <summary>Wraps an <see cref="IAsyncEnumerable{T}"/> into an <see cref="IAsyncLinqQuery{T}"/></summary>
		internal sealed class AsyncEnumerableQuery<T> : AsyncLinqIterator<T>
		{

			private IAsyncEnumerable<T> Source { get; }

			private IAsyncEnumerator<T>? Iterator { get; set; }

			public AsyncEnumerableQuery(IAsyncEnumerable<T> source, CancellationToken ct)
			{
				this.Source = source;
				this.Cancellation = ct;
			}

			public override CancellationToken Cancellation { get; }

			/// <inheritdoc />
			protected override AsyncEnumerableQuery<T> Clone() => new(this.Source, this.Cancellation);

			/// <inheritdoc />
			protected override ValueTask<bool> OnFirstAsync()
			{
				this.Iterator = this.Source.GetAsyncEnumerator(this.Cancellation);
				return new(true);
			}

			/// <inheritdoc />
			protected override async ValueTask<bool> OnNextAsync()
			{
				var iterator = this.Iterator;
				if (iterator == null || !(await iterator.MoveNextAsync().ConfigureAwait(false)))
				{
					return await this.Completed().ConfigureAwait(false);
				}
				return Publish(iterator.Current);
			}

			/// <inheritdoc />
			protected override async ValueTask Cleanup()
			{
				var iterator = this.Iterator;
				this.Iterator = null;
				if (iterator != null)
				{
					await iterator.DisposeAsync().ConfigureAwait(false);
				}
			}

#if NET10_0_OR_GREATER

			/// <inheritdoc />
			public override Task<T[]> ToArrayAsync() => this.Source.ToArrayAsync(this.Cancellation).AsTask();

			/// <inheritdoc />
			public override Task<List<T>> ToListAsync() => this.Source.ToListAsync(this.Cancellation).AsTask();

			/// <inheritdoc />
			public override Task<bool> AnyAsync() => this.Source.AnyAsync(this.Cancellation).AsTask();

			/// <inheritdoc />
			public override Task<bool> AnyAsync(Func<T, bool> predicate) => this.Source.AnyAsync(predicate, this.Cancellation).AsTask();

			/// <inheritdoc />
			public override IAsyncLinqQuery<TNew> Select<TNew>(Func<T, TNew> selector) => new AsyncEnumerableQuery<TNew>(this.Source.Select(selector), this.Cancellation);

			/// <inheritdoc />
			public override IAsyncLinqQuery<TNew> Select<TNew>(Func<T, int, TNew> selector) => new AsyncEnumerableQuery<TNew>(this.Source.Select(selector), this.Cancellation);

			/// <inheritdoc />
			public override IAsyncLinqQuery<T> Where(Func<T, bool> predicate) => new AsyncEnumerableQuery<T>(this.Source.Where(predicate), this.Cancellation);

			/// <inheritdoc />
			public override IAsyncLinqQuery<T> Where(Func<T, int, bool> predicate) => new AsyncEnumerableQuery<T>(this.Source.Where(predicate), this.Cancellation);

#endif

		}

	}
}
