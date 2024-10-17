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

namespace Doxense.Linq
{
	using System.Threading.Channels;
	using Doxense.Linq.Async;
	using Doxense.Linq.Async.Expressions;
	using Doxense.Linq.Async.Iterators;

	public static partial class AsyncEnumerable
	{

		#region Create...

		/// <summary>Create a new async sequence that will transform an inner async sequence</summary>
		/// <typeparam name="TSource">Type of elements of the inner async sequence</typeparam>
		/// <typeparam name="TResult">Type of elements of the outer async sequence</typeparam>
		/// <param name="source">Source async sequence that will be wrapped</param>
		/// <param name="factory">Factory method called when the outer sequence starts iterating. Must return an async enumerator</param>
		/// <returns>New async sequence</returns>
		internal static AsyncSequence<TSource, TResult> Create<TSource, TResult>(
			IAsyncEnumerable<TSource> source,
			Func<IAsyncEnumerator<TSource>,
			IAsyncEnumerator<TResult>> factory)
		{
			return new AsyncSequence<TSource, TResult>(source, factory);
		}

		/// <summary>Create a new async sequence that will transform an inner sequence</summary>
		/// <typeparam name="TSource">Type of elements of the inner sequence</typeparam>
		/// <typeparam name="TResult">Type of elements of the outer async sequence</typeparam>
		/// <param name="source">Source sequence that will be wrapped</param>
		/// <param name="factory">Factory method called when the outer sequence starts iterating. Must return an async enumerator</param>
		/// <returns>New async sequence</returns>
		internal static EnumerableSequence<TSource, TResult> Create<TSource, TResult>(
			IEnumerable<TSource> source,
			Func<IEnumerator<TSource>, CancellationToken, IAsyncEnumerator<TResult>> factory)
		{
			return new EnumerableSequence<TSource, TResult>(source, factory);
		}

		/// <summary>Create a new async sequence from a factory method</summary>
		public static IAsyncEnumerable<TResult> Create<TResult>(
			Func<object?, CancellationToken, IAsyncEnumerator<TResult>> factory,
			object? state = null)
		{
			return new AnonymousIterable<TResult>(factory, state);
		}

		internal sealed class AnonymousIterable<T> : IAsyncEnumerable<T>
		{

			private readonly Func<object?, CancellationToken, IAsyncEnumerator<T>> m_factory;
			private readonly object? m_state;

			public AnonymousIterable(Func<object?, CancellationToken, IAsyncEnumerator<T>> factory, object? state)
			{
				Contract.Debug.Requires(factory != null);
				m_factory = factory;
				m_state = state;
			}

			public IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken ct)
			{
				return m_factory(m_state, ct);
			}

			public IAsyncEnumerator<T> GetEnumerator(CancellationToken ct, AsyncIterationHint _)
			{
				ct.ThrowIfCancellationRequested();
				return m_factory(m_state, ct);
			}
		}

		/// <summary>Create a new async sequence from a factory that will be invoked on the first iteration</summary>
		public static IConfigurableAsyncEnumerable<TResult> Defer<TState, TResult>(TState state, Func<TState, CancellationToken, Task<IAsyncEnumerable<TResult>>> factory)
		{
			Contract.NotNull(factory);
			return new DeferredAsyncIterator<TState, TResult, IAsyncEnumerable<TResult>>(state, factory);
		}

		/// <summary>Create a new async sequence from a factory that will be invoked on the first iteration</summary>
		public static IConfigurableAsyncEnumerable<TResult> Defer<TState, TResult, TCollection>(TState state, Func<TState, CancellationToken, Task<TCollection>> factory)
			where TCollection : IAsyncEnumerable<TResult>
		{
			Contract.NotNull(factory);
			return new DeferredAsyncIterator<TState, TResult, TCollection>(state, factory);
		}

		/// <summary>Create a new async sequence from a factory that will be invoked on the first iteration</summary>
		public static IConfigurableAsyncEnumerable<TResult> Defer<TResult>(Func<CancellationToken, Task<IAsyncEnumerable<TResult>>> factory)
		{
			Contract.NotNull(factory);
			return new DeferredAsyncIterator<object, TResult, IAsyncEnumerable<TResult>>(
				factory,
				(s, ct) => ((Func<CancellationToken, Task<IAsyncEnumerable<TResult>>>) s)(ct)
			);
		}

		/// <summary>Create a new async sequence from a factory that will be invoked on the first iteration</summary>
		public static IConfigurableAsyncEnumerable<TResult> Defer<TResult, TCollection>(Func<CancellationToken, Task<TCollection>> factory)
			where TCollection: IAsyncEnumerable<TResult>
		{
			Contract.NotNull(factory);
			return new DeferredAsyncIterator<object, TResult, TCollection>(
				factory,
				(s, ct) => ((Func<CancellationToken, Task<TCollection>>) s)(ct)
			);
		}

		#endregion

		#region Channel...

		public static async IAsyncEnumerable<TResult> Pump<TResult>(Func<ChannelWriter<TResult>, Task> handler, [EnumeratorCancellation] CancellationToken ct)
		{
			var channel = Channel.CreateUnbounded<TResult>(new() { SingleReader = true, SingleWriter = true });
			try
			{
				using var ctr = ct.Register((c) => ((Channel<TResult>) c!).Writer.TryComplete(new OperationCanceledException(ct)), channel);

				var t = Task.Run(async () =>
				{
					try
					{
						ct.ThrowIfCancellationRequested();
						await handler(channel.Writer).ConfigureAwait(false);
						channel.Writer.TryComplete();
					}
					catch (Exception ex)
					{
						channel.Writer.TryComplete(ex);
					}
				}, ct);

				await foreach (var r in channel.Reader.ReadAllAsync().WithCancellation(ct).ConfigureAwait(false))
				{
					yield return r;
				}

				await t.ConfigureAwait(false);
			}
			finally
			{
				channel.Writer.TryComplete(null);
			}

		}

		#endregion

		#region Helpers...

		internal static SelectManyAsyncIterator<TSource, TResult> Flatten<TSource, TResult>(
			IAsyncEnumerable<TSource> source,
			AsyncTransformExpression<TSource, IEnumerable<TResult>> selector)
		{
			return new SelectManyAsyncIterator<TSource, TResult>(source, selector);
		}

		internal static SelectManyAsyncIterator<TSource, TCollection, TResult> Flatten<TSource, TCollection, TResult>(
			IAsyncEnumerable<TSource> source,
			AsyncTransformExpression<TSource, IEnumerable<TCollection>> collectionSelector,
			Func<TSource, TCollection, TResult> resultSelector)
		{
			return new SelectManyAsyncIterator<TSource, TCollection, TResult>(
				source,
				collectionSelector,
				resultSelector
			);
		}

		internal static WhereSelectAsyncIterator<TSource, TResult> Map<TSource, TResult>(
			IAsyncEnumerable<TSource> source,
			AsyncTransformExpression<TSource, TResult> selector,
			int? limit = null, int?
			offset = null)
		{
			return new WhereSelectAsyncIterator<TSource, TResult>(source, filter: null, transform: selector, limit: limit, offset: offset);
		}

		internal static WhereAsyncIterator<TResult> Filter<TResult>(
			IAsyncEnumerable<TResult> source,
			AsyncFilterExpression<TResult> filter)
		{
			return new WhereAsyncIterator<TResult>(source, filter);
		}

		internal static WhereSelectAsyncIterator<TResult, TResult> Offset<TResult>(
			IAsyncEnumerable<TResult> source,
			int offset)
		{
			return new WhereSelectAsyncIterator<TResult, TResult>(source, filter: null, transform: new AsyncTransformExpression<TResult, TResult>(), limit: null, offset: offset);
		}

		internal static WhereSelectAsyncIterator<TResult, TResult> Limit<TResult>(
			IAsyncEnumerable<TResult> source,
			int limit)
		{
			return new WhereSelectAsyncIterator<TResult, TResult>(source, filter: null, transform: new AsyncTransformExpression<TResult, TResult>(), limit: limit, offset: null);
		}

		internal static TakeWhileAsyncIterator<TResult> Limit<TResult>(
			IAsyncEnumerable<TResult> source,
			Func<TResult, bool> condition)
		{
			return new TakeWhileAsyncIterator<TResult>(source, condition);
		}

		#endregion

		#region Run...

		/// <summary>Immediately execute an action on each element of an async sequence</summary>
		/// <typeparam name="TSource">Type of elements of the async sequence</typeparam>
		/// <param name="source">Source async sequence</param>
		/// <param name="mode">If different than default, can be used to optimise the way the source will produce the items</param>
		/// <param name="action">Action to perform on each element as it arrives</param>
		/// <param name="ct">Cancellation token that can be used to cancel the operation</param>
		/// <returns>Number of items that have been processed</returns>
		internal static async Task Run<TSource>(
			IAsyncEnumerable<TSource> source,
			AsyncIterationHint mode,
			[InstantHandle] Action<TSource> action,
			CancellationToken ct)
		{
			Contract.NotNull(source);
			Contract.NotNull(action);

			ct.ThrowIfCancellationRequested();

			await using (var iterator = source is IConfigurableAsyncEnumerable<TSource> configurable ? configurable.GetAsyncEnumerator(ct, mode) : source.GetAsyncEnumerator(ct))
			{
				Contract.Debug.Assert(iterator != null, "The underlying sequence returned a null async iterator");

				while (await iterator.MoveNextAsync().ConfigureAwait(false))
				{
					action(iterator.Current);
				}
			}
		}

		/// <summary>Immediately execute an action on each element of an async sequence</summary>
		/// <typeparam name="TSource">Type of elements of the async sequence</typeparam>
		/// <param name="source">Source async sequence</param>
		/// <param name="mode">If different than default, can be used to optimise the way the source will produce the items</param>
		/// <param name="action">Action to perform on each element as it arrives</param>
		/// <param name="ct">Cancellation token that can be used to cancel the operation</param>
		/// <returns>Number of items that have been processed</returns>
		internal static async Task Run<TState, TSource>(
			IAsyncEnumerable<TSource> source,
			AsyncIterationHint mode,
			TState state,
			[InstantHandle] Action<TState, TSource> action,
			CancellationToken ct)
		{
			Contract.NotNull(source);
			Contract.NotNull(action);

			ct.ThrowIfCancellationRequested();

			await using (var iterator = source is IConfigurableAsyncEnumerable<TSource> configurable ? configurable.GetAsyncEnumerator(ct, mode) : source.GetAsyncEnumerator(ct))
			{
				Contract.Debug.Assert(iterator != null, "The underlying sequence returned a null async iterator");

				while (await iterator.MoveNextAsync().ConfigureAwait(false))
				{
					action(state, iterator.Current);
				}
			}
		}

		/// <summary>Immediately execute an action on each element of an async sequence</summary>
		/// <typeparam name="TSource">Type of elements of the async sequence</typeparam>
		/// <typeparam name="TAggregate"></typeparam>
		/// <param name="source">Source async sequence</param>
		/// <param name="mode">If different than default, can be used to optimise the way the source will produce the items</param>
		/// <param name="seed"></param>
		/// <param name="action">Action to perform on each element as it arrives</param>
		/// <param name="ct">Cancellation token that can be used to cancel the operation</param>
		/// <returns>Number of items that have been processed</returns>
		internal static async Task<TAggregate> Run<TAggregate, TSource>(
			IAsyncEnumerable<TSource> source,
			AsyncIterationHint mode,
			TAggregate seed,
			[InstantHandle] Func<TAggregate, TSource, TAggregate> action,
			CancellationToken ct)
		{
			Contract.NotNull(source);
			Contract.NotNull(action);

			ct.ThrowIfCancellationRequested();

			await using (var iterator = source is IConfigurableAsyncEnumerable<TSource> configurable ? configurable.GetAsyncEnumerator(ct, mode) : source.GetAsyncEnumerator(ct))
			{
				Contract.Debug.Assert(iterator != null, "The underlying sequence returned a null async iterator");

				while (await iterator.MoveNextAsync().ConfigureAwait(false))
				{
					seed = action(seed, iterator.Current);
				}
			}
			return seed;
		}

		/// <summary>Immediately execute an action on each element of an async sequence, with the possibility of stopping before the end</summary>
		/// <typeparam name="TSource">Type of elements of the async sequence</typeparam>
		/// <param name="source">Source async sequence</param>
		/// <param name="mode">If different than default, can be used to optimise the way the source will produce the items</param>
		/// <param name="action">Lambda called for each element as it arrives. If the return value is true, the next value will be processed. If the return value is false, the iterations will stop immediately.</param>
		/// <param name="ct">Cancellation token that can be used to cancel the operation</param>
		/// <returns>Number of items that have been processed successfully</returns>
		internal static async Task Run<TSource>(
			IAsyncEnumerable<TSource> source,
			AsyncIterationHint mode,
			Func<TSource, bool> action,
			CancellationToken ct)
		{
			Contract.NotNull(source);
			Contract.NotNull(action);

			ct.ThrowIfCancellationRequested();

			await using (var iterator = source is IConfigurableAsyncEnumerable<TSource> configurable ? configurable.GetAsyncEnumerator(ct, mode) : source.GetAsyncEnumerator(ct))
			{
				Contract.Debug.Assert(iterator != null, "The underlying sequence returned a null async iterator");

				while (await iterator.MoveNextAsync().ConfigureAwait(false))
				{
					if (!action(iterator.Current))
					{
						break;
					}
				}
			}
		}

		/// <summary>Immediately execute an async action on each element of an async sequence</summary>
		/// <typeparam name="TSource">Type of elements of the async sequence</typeparam>
		/// <param name="source">Source async sequence</param>
		/// <param name="mode">Expected execution mode of the query</param>
		/// <param name="action">Asynchronous action to perform on each element as it arrives</param>
		/// <param name="ct">Cancellation token that can be used to cancel the operation</param>
		/// <returns>Number of items that have been processed</returns>
		internal static async Task Run<TSource>(
			IAsyncEnumerable<TSource> source,
			AsyncIterationHint mode,
			Func<TSource, CancellationToken, Task> action,
			CancellationToken ct)
		{
			ct.ThrowIfCancellationRequested();

			await using (var iterator = source is IConfigurableAsyncEnumerable<TSource> configurable ? configurable.GetAsyncEnumerator(ct, mode) : source.GetAsyncEnumerator(ct))
			{
				Contract.Debug.Assert(iterator != null, "The underlying sequence returned a null async iterator");

				while (await iterator.MoveNextAsync().ConfigureAwait(false))
				{
					await action(iterator.Current, ct).ConfigureAwait(false);
				}
			}
		}

		/// <summary>Helper async method to get the first element of an async sequence</summary>
		/// <typeparam name="TSource">Type of elements of the async sequence</typeparam>
		/// <param name="source">Source async sequence</param>
		/// <param name="single">If true, the sequence must contain at most one element</param>
		/// <param name="orDefault">When the sequence is empty: If true then returns the default value for the type. Otherwise, throws an exception</param>
		/// <param name="ct">Cancellation token that can be used to cancel the operation</param>
		/// <returns>Value of the first element of the <param ref="source"/> sequence, or the default value, or an exception (depending on <paramref name="single"/> and <paramref name="orDefault"/></returns>
		public static async Task<TSource> Head<TSource>(
			IAsyncEnumerable<TSource> source,
			bool single,
			bool orDefault,
			CancellationToken ct)
		{
			ct.ThrowIfCancellationRequested();

			await using (var iterator = source is IConfigurableAsyncEnumerable<TSource> configurable ? configurable.GetAsyncEnumerator(ct, AsyncIterationHint.Head) : source.GetAsyncEnumerator(ct))
			{
				Contract.Debug.Assert(iterator != null, "The underlying sequence returned a null async iterator");

				if (await iterator.MoveNextAsync().ConfigureAwait(false))
				{
					TSource first = iterator.Current;
					if (single)
					{
						if (await iterator.MoveNextAsync().ConfigureAwait(false)) throw new InvalidOperationException("The sequence contained more than one element");
					}
					return first;
				}
				if (!orDefault) throw new InvalidOperationException("The sequence was empty");
				return default!;
			}
		}

		#endregion

	}
}
