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

namespace SnowBank.Linq.Async.Iterators
{
	using System;
	using System.Buffers;
	using System.Collections.Immutable;
	using System.ComponentModel;
	using System.Diagnostics.CodeAnalysis;
	using System.Numerics;
	using System.Reflection;
	using Doxense.Linq;
	using SnowBank.Linq.Async.Expressions;
	using Doxense.Serialization;

	/// <summary>Base class for all async iterators</summary>
	/// <typeparam name="TResult">Type of elements of the outer async sequence</typeparam>
	[PublicAPI]
	public abstract class AsyncLinqIterator<TResult> : IAsyncLinqQuery<TResult>, IAsyncEnumerable<TResult>, IAsyncEnumerator<TResult>
	{
		//REVIEW: we could need an IAsyncIterator<T> interface that holds all the Select(),Where(),Take(),... so that it can be used by AsyncEnumerable to either call them directly (if the query supports it) or use a generic implementation
		// => this would be implemented by AsyncIterator<T> as well as FdbRangeQuery<T> (and ony other 'self optimizing' class)

		private const int STATE_SEQ = 0;
		private const int STATE_INIT = 1;
		private const int STATE_ITERATING = 2;
		private const int STATE_COMPLETED = 3;
		private const int STATE_DISPOSED = -1;

		protected TResult? m_current;
		protected int m_state;
		protected AsyncIterationHint m_mode;

		#region IAsyncEnumerable<TResult>...

		public abstract CancellationToken Cancellation { get; }

		[MustDisposeResource]
		[EditorBrowsable(EditorBrowsableState.Never)]
		IAsyncEnumerator<TResult> IAsyncEnumerable<TResult>.GetAsyncEnumerator(CancellationToken ct)
			=> AsyncQuery.GetCancellableAsyncEnumerator(this, AsyncIterationHint.All, ct);

		[MustDisposeResource]
		[EditorBrowsable(EditorBrowsableState.Never)]
		IAsyncEnumerator<TResult> IAsyncQuery<TResult>.GetAsyncEnumerator(CancellationToken ct)
			=> AsyncQuery.GetCancellableAsyncEnumerator(this, AsyncIterationHint.All, ct);

		[MustDisposeResource]
		public IAsyncEnumerator<TResult> GetAsyncEnumerator(AsyncIterationHint mode)
		{
			this.Cancellation.ThrowIfCancellationRequested();

			// reuse the same instance the first time
			if (Interlocked.CompareExchange(ref m_state, STATE_INIT, STATE_SEQ) == STATE_SEQ)
			{
				m_mode = mode;
				return this;
			}

			// create a new one
			var iterator = Clone();
			iterator.m_mode = mode;
			Volatile.Write(ref iterator.m_state, STATE_INIT);
			return iterator;
		}

		protected abstract AsyncLinqIterator<TResult> Clone();

		#endregion

		#region IAsyncEnumerator<TResult>...

		public TResult Current
		{
			get
			{
				if (Volatile.Read(ref m_state) != STATE_ITERATING) EnsureIsIterating();
				return m_current!;
			}
		}

		public async ValueTask<bool> MoveNextAsync()
		{
			var state = Volatile.Read(ref m_state);

			if (state == STATE_COMPLETED)
			{
				return false;
			}
			if (state != STATE_INIT && state != STATE_ITERATING)
			{
				EnsureIsIterating();
				return false;
			}

			if (this.Cancellation.IsCancellationRequested)
			{
				return await Canceled().ConfigureAwait(false);
			}

			try
			{
				if (state == STATE_INIT)
				{
					if (!await OnFirstAsync().ConfigureAwait(false))
					{ // did not start at all ?
						return await Completed().ConfigureAwait(false);
					}

					if (Interlocked.CompareExchange(ref m_state, STATE_ITERATING, STATE_INIT) != STATE_INIT)
					{ // something happened while we were starting ?
						return false;
					}
				}

				return await OnNextAsync().ConfigureAwait(false);
			}
			catch (Exception)
			{
				await MarkAsFailed().ConfigureAwait(false);
				throw;
			}
		}

		#endregion

		#region LINQ...

		/// <inheritdoc />
		public virtual IAsyncEnumerable<TResult> ToAsyncEnumerable(AsyncIterationHint hint = AsyncIterationHint.Default)
		{
			return this;
		}

		#region To{Collection}Async...

		/// <inheritdoc />
		public virtual async Task<TResult[]> ToArrayAsync()
		{
			var buffer = new Buffer<TResult>(0, ArrayPool<TResult>.Shared);
			await foreach (var item in this)
			{
				buffer.Add(item);
			}
			return buffer.ToArrayAndClear();
		}

		/// <inheritdoc />
		public virtual async Task<List<TResult>> ToListAsync()
		{
			var buffer = new Buffer<TResult>(0, ArrayPool<TResult>.Shared);
			await foreach (var item in this)
			{
				buffer.Add(item);
			}
			return buffer.ToListAndClear();
		}

		/// <inheritdoc />
		public virtual async Task<ImmutableArray<TResult>> ToImmutableArrayAsync()
		{
			var buffer = new Buffer<TResult>(0, ArrayPool<TResult>.Shared);
			await foreach (var item in this)
			{
				buffer.Add(item);
			}
			return buffer.ToImmutableArrayAndClear();
		}

		/// <inheritdoc />
		public virtual Task<Dictionary<TKey, TResult>> ToDictionaryAsync<TKey>([InstantHandle] Func<TResult, TKey> keySelector, IEqualityComparer<TKey>? comparer = null)
			where TKey : notnull
		{
			return ToDictionaryAsync(keySelector, x => x, comparer);
		}

		/// <inheritdoc />
		public virtual async Task<Dictionary<TKey, TElement>> ToDictionaryAsync<TKey, TElement>([InstantHandle] Func<TResult, TKey> keySelector, [InstantHandle] Func<TResult, TElement> elementSelector, IEqualityComparer<TKey>? comparer = null)
			where TKey : notnull
		{

			var map = new Dictionary<TKey, TElement>(comparer);
			await foreach (var item in this)
			{
				map.Add(keySelector(item), elementSelector(item));
			}

			return map;
		}

		/// <inheritdoc />
		public virtual async Task<HashSet<TResult>> ToHashSetAsync(IEqualityComparer<TResult>? comparer = null)
		{
			var buffer = new Buffer<TResult>(0, ArrayPool<TResult>.Shared);
			await foreach (var item in this)
			{
				buffer.Add(item);
			}
			return buffer.ToHashSetAndClear(comparer);
		}

		#endregion

		#region CountAsync...

		public virtual async Task<int> CountAsync()
		{
			int count = 0;
			await using var iterator = GetAsyncEnumerator(AsyncIterationHint.All);

			while(await iterator.MoveNextAsync().ConfigureAwait(false))
			{
				checked
				{
					count++;
				}
			}
			return count;
		}

		public virtual async Task<int> CountAsync(Func<TResult, bool> predicate)
		{
			Contract.NotNull(predicate);

			int count = 0;
			await using var iterator = GetAsyncEnumerator(AsyncIterationHint.All);

			await foreach(var item in this)
			{
				if (predicate(item))
				{
					checked { count++; }
				}
			}
			return count;
		}

		public virtual async Task<int> CountAsync(Func<TResult, CancellationToken, Task<bool>> predicate)
		{
			Contract.NotNull(predicate);

			int count = 0;
			var ct = this.Cancellation;
			await using var iterator = GetAsyncEnumerator(AsyncIterationHint.All);

			await foreach(var item in this)
			{
				if (await predicate(item, ct).ConfigureAwait(false))
				{
					checked { count++; }
				}
			}
			return count;
		}

		#endregion

		#region AnyAsync...

		/// <inheritdoc />
		public virtual async Task<bool> AnyAsync()
		{
			await using var iterator = this.GetAsyncEnumerator(AsyncIterationHint.All);

			return await iterator.MoveNextAsync().ConfigureAwait(false);
		}

		/// <inheritdoc />
		public virtual async Task<bool> AnyAsync(Func<TResult, bool> predicate)
		{
			Contract.NotNull(predicate);

			await using var iterator = this.GetAsyncEnumerator(AsyncIterationHint.Iterator);

			while (await iterator.MoveNextAsync().ConfigureAwait(false))
			{
				if (predicate(iterator.Current)) return true;
			}
			return false;
		}

		/// <inheritdoc />
		public virtual async Task<bool> AnyAsync(Func<TResult, CancellationToken, Task<bool>> predicate)
		{
			Contract.NotNull(predicate);

			await using var iterator = this.GetAsyncEnumerator(AsyncIterationHint.Iterator);

			var ct = this.Cancellation;
			while (await iterator.MoveNextAsync().ConfigureAwait(false))
			{
				if (await predicate(iterator.Current, ct).ConfigureAwait(false)) return true;
			}
			return false;
		}

		#endregion

		#region AllAsync...

		/// <inheritdoc />
		public virtual async Task<bool> AllAsync(Func<TResult, bool> predicate)
		{
			Contract.NotNull(predicate);

			await using var iterator = this.GetAsyncEnumerator(AsyncIterationHint.Iterator);

			while (await iterator.MoveNextAsync().ConfigureAwait(false))
			{
				if (!predicate(iterator.Current)) return false;
			}
			return true;
		}

		/// <inheritdoc />
		public virtual async Task<bool> AllAsync(Func<TResult, CancellationToken, Task<bool>> predicate)
		{
			Contract.NotNull(predicate);

			await using var iterator = this.GetAsyncEnumerator(AsyncIterationHint.Iterator);

			var ct = this.Cancellation;
			while (await iterator.MoveNextAsync().ConfigureAwait(false))
			{
				if (!(await predicate(iterator.Current, ct).ConfigureAwait(false))) return false;
			}
			return true;
		}

		#endregion

		#region FirstOrDefaultAsync...

		/// <inheritdoc />
		public Task<TResult?> FirstOrDefaultAsync() => FirstOrDefaultAsync(default(TResult)!)!;

		/// <inheritdoc />
		public virtual async Task<TResult> FirstOrDefaultAsync(TResult defaultValue)
		{
			await using var iterator = this.GetAsyncEnumerator(AsyncIterationHint.Head);

			if (await iterator.MoveNextAsync().ConfigureAwait(false))
			{
				return iterator.Current;
			}
			return defaultValue;
		}

		/// <inheritdoc />
		public Task<TResult?> FirstOrDefaultAsync(Func<TResult, bool> predicate) => FirstOrDefaultAsync(predicate, default(TResult)!)!;

		/// <inheritdoc />
		public virtual async Task<TResult> FirstOrDefaultAsync(Func<TResult, bool> predicate, TResult defaultValue)
		{
			await using var iterator = this.GetAsyncEnumerator(AsyncIterationHint.Iterator);

			while(await iterator.MoveNextAsync().ConfigureAwait(false))
			{
				var item = iterator.Current;
				if (predicate(item))
				{
					return item;
				}
			}
			return defaultValue;
		}

		/// <inheritdoc />
		public Task<TResult?> FirstOrDefaultAsync(Func<TResult, CancellationToken, Task<bool>> predicate) => FirstOrDefaultAsync(predicate, default(TResult)!)!;

		/// <inheritdoc />
		public virtual async Task<TResult> FirstOrDefaultAsync(Func<TResult, CancellationToken, Task<bool>> predicate, TResult defaultValue)
		{
			await using var iterator = this.GetAsyncEnumerator(AsyncIterationHint.Iterator);

			var ct = this.Cancellation;
			while (await iterator.MoveNextAsync().ConfigureAwait(false))
			{
				var item = iterator.Current;
				if (await predicate(item, ct).ConfigureAwait(false))
				{
					return item;
				}
			}
			return defaultValue;
		}

		#endregion

		#region FirstAsync...

		/// <inheritdoc />
		public virtual async Task<TResult> FirstAsync()
		{
			await using var iterator = this.GetAsyncEnumerator(AsyncIterationHint.Head);

			if (!await iterator.MoveNextAsync().ConfigureAwait(false))
			{
				throw AsyncQuery.ErrorNoElements();
			}

			var item = iterator.Current;
			return item;
		}

		/// <inheritdoc />
		public virtual async Task<TResult> FirstAsync(Func<TResult, bool> predicate)
		{
			await using var iterator = this.GetAsyncEnumerator(AsyncIterationHint.Iterator);

			while (await iterator.MoveNextAsync().ConfigureAwait(false))
			{
				var item = iterator.Current;
				if (predicate(item))
				{
					return item;
				}
			}
			throw AsyncQuery.ErrorNoMatch();
		}

		/// <inheritdoc />
		public virtual async Task<TResult> FirstAsync(Func<TResult, CancellationToken, Task<bool>> predicate)
		{
			await using var iterator = this.GetAsyncEnumerator(AsyncIterationHint.Iterator);

			var ct = this.Cancellation;
			while (await iterator.MoveNextAsync().ConfigureAwait(false))
			{
				var item = iterator.Current;
				if (await predicate(item, ct).ConfigureAwait(false))
				{
					return item;
				}
			}
			throw AsyncQuery.ErrorNoMatch();
		}

		#endregion

		#region SingleOrDefaultAsync...

		/// <inheritdoc />
		public Task<TResult?> SingleOrDefaultAsync() => SingleOrDefaultAsync(default(TResult)!)!;

		/// <inheritdoc />
		public virtual async Task<TResult> SingleOrDefaultAsync(TResult defaultValue)
		{
			await using var iterator = this.GetAsyncEnumerator(AsyncIterationHint.Head);

			if (await iterator.MoveNextAsync().ConfigureAwait(false))
			{
				var item = iterator.Current;
				if (await iterator.MoveNextAsync().ConfigureAwait(false))
				{
					throw AsyncQuery.ErrorMoreThenOneElement();
				}
				return item;
			}
			return defaultValue;
		}

		/// <inheritdoc />
		public Task<TResult?> SingleOrDefaultAsync(Func<TResult, bool> predicate) => SingleOrDefaultAsync(predicate, default(TResult)!)!;

		/// <inheritdoc />
		public virtual async Task<TResult> SingleOrDefaultAsync(Func<TResult, bool> predicate, TResult defaultValue)
		{
			await using var iterator = this.GetAsyncEnumerator(AsyncIterationHint.Iterator);

			TResult result = defaultValue;
			bool found = false;
			while(await iterator.MoveNextAsync().ConfigureAwait(false))
			{
				var item = iterator.Current;
				if (predicate(item))
				{
					if (found) throw AsyncQuery.ErrorMoreThanOneMatch();
					result = item;
				}
			}
			return result;
		}

		/// <inheritdoc />
		public Task<TResult?> SingleOrDefaultAsync(Func<TResult, CancellationToken, Task<bool>> predicate) => SingleOrDefaultAsync(predicate, default(TResult)!)!;

		/// <inheritdoc />
		public virtual async Task<TResult> SingleOrDefaultAsync(Func<TResult, CancellationToken, Task<bool>> predicate, TResult defaultValue)
		{
			await using var iterator = this.GetAsyncEnumerator(AsyncIterationHint.Iterator);

			TResult result = defaultValue;
			bool found = false;
			var ct = this.Cancellation;
			while (await iterator.MoveNextAsync().ConfigureAwait(false))
			{
				var item = iterator.Current;
				if (await predicate(item, ct).ConfigureAwait(false))
				{
					if (found) throw AsyncQuery.ErrorMoreThanOneMatch();
					result = item;
				}
			}
			return result;
		}

		#endregion

		#region SingleAsync...

		/// <inheritdoc />
		public virtual async Task<TResult> SingleAsync()
		{
			await using var iterator = this.GetAsyncEnumerator(AsyncIterationHint.Head);

			if (!await iterator.MoveNextAsync().ConfigureAwait(false))
			{
				throw AsyncQuery.ErrorNoElements();
			}

			var item = iterator.Current;

			if (await iterator.MoveNextAsync().ConfigureAwait(false))
			{
				throw AsyncQuery.ErrorMoreThenOneElement();
			}

			return item;
		}

		/// <inheritdoc />
		public virtual async Task<TResult> SingleAsync(Func<TResult, bool> predicate)
		{
			await using var iterator = this.GetAsyncEnumerator(AsyncIterationHint.Iterator);

			TResult? single = default;
			bool found = false;

			while (await iterator.MoveNextAsync().ConfigureAwait(false))
			{
				var item = iterator.Current;
				if (predicate(item))
				{
					if (found)
					{
						throw AsyncQuery.ErrorMoreThanOneMatch();
					}
					single = item;
					found = true;
				}
			}

			if (!found)
			{
				throw AsyncQuery.ErrorNoMatch();
			}

			return single!;
		}

		/// <inheritdoc />
		public virtual async Task<TResult> SingleAsync(Func<TResult, CancellationToken, Task<bool>> predicate)
		{
			await using var iterator = this.GetAsyncEnumerator(AsyncIterationHint.Iterator);

			TResult? single = default;
			bool found = false;

			var ct = this.Cancellation;
			while (await iterator.MoveNextAsync().ConfigureAwait(false))
			{
				var item = iterator.Current;
				if (await predicate(item, ct).ConfigureAwait(false))
				{
					if (found)
					{
						throw AsyncQuery.ErrorMoreThanOneMatch();
					}
					single = item;
					found = true;
				}
			}

			if (!found)
			{
				throw AsyncQuery.ErrorNoMatch();
			}

			return single!;
		}

		#endregion

		#region LastOrDefaultAsync...

		/// <inheritdoc />
		public Task<TResult?> LastOrDefaultAsync() => LastOrDefaultAsync(default(TResult)!)!;

		/// <inheritdoc />
		public virtual async Task<TResult> LastOrDefaultAsync(TResult defaultValue)
		{
			await using var iterator = this.GetAsyncEnumerator(AsyncIterationHint.Head);

			var result = defaultValue;
			while(await iterator.MoveNextAsync().ConfigureAwait(false))
			{
				result = iterator.Current;
			}

			return result;
		}

		/// <inheritdoc />
		public Task<TResult?> LastOrDefaultAsync(Func<TResult, bool> predicate) => LastOrDefaultAsync(predicate, default(TResult)!)!;

		/// <inheritdoc />
		public virtual async Task<TResult> LastOrDefaultAsync(Func<TResult, bool> predicate, TResult defaultValue)
		{
			await using var iterator = this.GetAsyncEnumerator(AsyncIterationHint.Head);

			var result = defaultValue;
			while(await iterator.MoveNextAsync().ConfigureAwait(false))
			{
				var item = iterator.Current;
				if (predicate(item))
				{
					result = item;
				}
			}

			return result;
		}

		/// <inheritdoc />
		public Task<TResult?> LastOrDefaultAsync(Func<TResult, CancellationToken, Task<bool>> predicate) => LastOrDefaultAsync(predicate, default(TResult)!)!;

		/// <inheritdoc />
		public virtual async Task<TResult> LastOrDefaultAsync(Func<TResult, CancellationToken, Task<bool>> predicate, TResult defaultValue)
		{
			await using var iterator = this.GetAsyncEnumerator(AsyncIterationHint.Head);

			var result = defaultValue;
			var ct = this.Cancellation;
			while(await iterator.MoveNextAsync().ConfigureAwait(false))
			{
				var item = iterator.Current;
				if (await predicate(item, ct).ConfigureAwait(false))
				{
					result = item;
				}
			}

			return result;
		}

		#endregion

		#region LastAsync...

		/// <inheritdoc />
		public virtual async Task<TResult> LastAsync()
		{
			await using var iterator = this.GetAsyncEnumerator(AsyncIterationHint.Head);

			if (!await iterator.MoveNextAsync().ConfigureAwait(false))
			{
				throw AsyncQuery.ErrorNoElements();
			}

			TResult item;
			do
			{
				item = iterator.Current;
			}
			while (await iterator.MoveNextAsync().ConfigureAwait(false));

			return item;
		}

		/// <inheritdoc />
		public virtual async Task<TResult> LastAsync(Func<TResult, bool> predicate)
		{
			await using var iterator = this.GetAsyncEnumerator(AsyncIterationHint.Head);

			if (!await iterator.MoveNextAsync().ConfigureAwait(false))
			{
				throw AsyncQuery.ErrorNoElements();
			}

			TResult result = default!;
			bool found = false;
			do
			{
				var item = iterator.Current;
				if (predicate(item))
				{
					found = true;
					result = item;
				}
			}
			while (await iterator.MoveNextAsync().ConfigureAwait(false));

			return found ? result : throw AsyncQuery.ErrorNoMatch();
		}

		/// <inheritdoc />
		public virtual async Task<TResult> LastAsync(Func<TResult, CancellationToken, Task<bool>> predicate)
		{
			await using var iterator = this.GetAsyncEnumerator(AsyncIterationHint.Head);

			if (!await iterator.MoveNextAsync().ConfigureAwait(false))
			{
				throw AsyncQuery.ErrorNoElements();
			}

			TResult result = default!;
			bool found = false;
			var ct = this.Cancellation;
			do
			{
				var item = iterator.Current;
				if (await predicate(item, ct).ConfigureAwait(false))
				{
					found = true;
					result = item;
				}
			}
			while (await iterator.MoveNextAsync().ConfigureAwait(false));

			return found ? result : throw AsyncQuery.ErrorNoMatch();
		}

		#endregion

		#region MinAsync/MaxAsync...

		/// <inheritdoc />
		public virtual Task<TResult?> MinAsync(IComparer<TResult>? comparer = null)
		{
			return AsyncQuery.MinAsyncImpl<TResult>(this, comparer ?? Comparer<TResult>.Default);
		}

		/// <inheritdoc />
		public virtual Task<TResult?> MaxAsync(IComparer<TResult>? comparer = null)
		{
			return AsyncQuery.MaxAsyncImpl<TResult>(this, comparer ?? Comparer<TResult>.Default);
		}

		#endregion

		#region SumAsync...

#if NET8_0_OR_GREATER
		[RequiresDynamicCode("The native code for this instantiation might not be available at runtime.")]
#endif
		public virtual Task<TResult> SumAsync()
		{
			if (default(TResult) is not null)
			{
				if (typeof(TResult) == typeof(int)) return (Task<TResult>) (object) AsyncQuery.SumAsyncInt32Impl((IAsyncQuery<int>) this);
				if (typeof(TResult) == typeof(long)) return (Task<TResult>) (object) AsyncQuery.SumAsyncInt64Impl((IAsyncQuery<long>) this);
				if (typeof(TResult) == typeof(float)) return (Task<TResult>) (object) AsyncQuery.SumAsyncFloatImpl((IAsyncQuery<float>) this);
				if (typeof(TResult) == typeof(double)) return (Task<TResult>) (object) AsyncQuery.SumAsyncDoubleImpl((IAsyncQuery<double>) this);
				if (typeof(TResult) == typeof(decimal)) return (Task<TResult>) (object) AsyncQuery.SumAsyncDecimalImpl((IAsyncQuery<decimal>) this);
			}
			else
			{
				if (typeof(TResult) == typeof(int?)) return (Task<TResult>) (object) AsyncQuery.SumAsyncInt32Impl((IAsyncQuery<int?>) this);
				if (typeof(TResult) == typeof(long?)) return (Task<TResult>) (object) AsyncQuery.SumAsyncInt64Impl((IAsyncQuery<long?>) this);
				if (typeof(TResult) == typeof(float?)) return (Task<TResult>) (object) AsyncQuery.SumAsyncFloatImpl((IAsyncQuery<float?>) this);
				if (typeof(TResult) == typeof(double?)) return (Task<TResult>) (object) AsyncQuery.SumAsyncDoubleImpl((IAsyncQuery<double?>) this);
				if (typeof(TResult) == typeof(decimal?)) return (Task<TResult>) (object) AsyncQuery.SumAsyncDecimalImpl((IAsyncQuery<decimal?>) this);
			}

			var nullable = Nullable.GetUnderlyingType(typeof(TResult));
			if (nullable != null)
			{
				if (nullable.IsGenericInstanceOf(typeof(INumberBase<>)))
				{
					var m = s_sumAsyncNullableImplMethod ??= (typeof(AsyncQuery).GetMethod(nameof(AsyncQuery.SumAsyncNullableImpl), BindingFlags.Static | BindingFlags.NonPublic)?.MakeGenericMethod(nullable));
					if (m != null)
					{
						return (Task<TResult>) m.Invoke(this, [ this ])!;
					}
				}
			}
			else
			{
				if (typeof(TResult).IsGenericInstanceOf(typeof(INumberBase<>)))
				{
					var m = s_sumAsyncImplMethod ??= (typeof(AsyncQuery).GetMethod(nameof(AsyncQuery.SumAsyncImpl), BindingFlags.Static | BindingFlags.NonPublic)?.MakeGenericMethod(typeof(TResult)));
					if (m != null)
					{
						return (Task<TResult>) m.Invoke(this, [ this ])!;
					}
				}
			}

			throw new NotSupportedException();
		}

		private static MethodInfo? s_sumAsyncImplMethod;
		private static MethodInfo? s_sumAsyncNullableImplMethod;

		#endregion

		#region Where...

		/// <inheritdoc />
		public virtual IAsyncLinqQuery<TResult> Where(Func<TResult, bool> predicate)
		{
			Contract.NotNull(predicate);

			return AsyncQuery.WhereImpl(this, predicate);
		}

		/// <inheritdoc />
		public virtual IAsyncLinqQuery<TResult> Where(Func<TResult, int, bool> predicate)
		{
			Contract.NotNull(predicate);

			return AsyncQuery.WhereImpl(this, predicate);
		}

		/// <inheritdoc />
		public virtual IAsyncLinqQuery<TResult> Where(Func<TResult, CancellationToken, Task<bool>> predicate)
		{
			Contract.NotNull(predicate);

			return AsyncQuery.WhereImpl(this, predicate);
		}

		/// <inheritdoc />
		public virtual IAsyncLinqQuery<TResult> Where(Func<TResult, int, CancellationToken, Task<bool>> predicate)
		{
			Contract.NotNull(predicate);

			return AsyncQuery.WhereImpl(this, predicate);
		}

		#endregion

		#region Select...

		/// <inheritdoc />
		public virtual IAsyncLinqQuery<TNew> Select<TNew>(Func<TResult, TNew> selector)
		{
			Contract.NotNull(selector);

			return AsyncQuery.SelectImpl(this, selector);
		}

		/// <inheritdoc />
		public virtual IAsyncLinqQuery<TNew> Select<TNew>(Func<TResult, int, TNew> selector)
		{
			Contract.NotNull(selector);

			return AsyncQuery.SelectImpl(this, selector);
		}

		/// <inheritdoc />
		public virtual IAsyncLinqQuery<TNew> Select<TNew>(Func<TResult, CancellationToken, Task<TNew>> selector)
		{
			Contract.NotNull(selector);

			return AsyncQuery.SelectImpl(this, selector);
		}

		/// <inheritdoc />
		public virtual IAsyncLinqQuery<TNew> Select<TNew>(Func<TResult, int, CancellationToken, Task<TNew>> selector)
		{
			Contract.NotNull(selector);

			return AsyncQuery.SelectImpl(this, selector);
		}

		#endregion

		#region SelectMany...

		/// <inheritdoc />
		public virtual IAsyncLinqQuery<TNew> SelectMany<TNew>(Func<TResult, IEnumerable<TNew>> selector)
		{
			Contract.NotNull(selector);

			return AsyncQuery.SelectManyImpl(this, selector);
		}

		/// <inheritdoc />
		public virtual IAsyncLinqQuery<TNew> SelectMany<TNew>(Func<TResult, CancellationToken, Task<IEnumerable<TNew>>> selector)
		{
			Contract.NotNull(selector);

			return AsyncQuery.SelectManyImpl(this, selector);
		}

		/// <inheritdoc />
		public virtual IAsyncLinqQuery<TNew> SelectMany<TCollection, TNew>(Func<TResult, IEnumerable<TCollection>> collectionSelector, Func<TResult, TCollection, TNew> resultSelector)
		{
			Contract.NotNull(collectionSelector);
			Contract.NotNull(resultSelector);

			return AsyncQuery.SelectManyImpl(this, collectionSelector, resultSelector);
		}

		/// <inheritdoc />
		public virtual IAsyncLinqQuery<TNew> SelectMany<TCollection, TNew>(Func<TResult, CancellationToken, Task<IEnumerable<TCollection>>> collectionSelector, Func<TResult, TCollection, TNew> resultSelector)
		{
			Contract.NotNull(collectionSelector);
			Contract.NotNull(resultSelector);

			return AsyncQuery.SelectManyImpl(this, collectionSelector, resultSelector);
		}

		/// <inheritdoc />
		public virtual IAsyncLinqQuery<TNew> SelectMany<TNew>(Func<TResult, IAsyncEnumerable<TNew>> selector)
		{
			Contract.NotNull(selector);

			return AsyncQuery.Flatten(this, new AsyncTransformExpression<TResult, IAsyncEnumerable<TNew>>(selector));
		}

		/// <inheritdoc />
		public virtual IAsyncLinqQuery<TNew> SelectMany<TNew>(Func<TResult, IAsyncQuery<TNew>> selector)
		{
			Contract.NotNull(selector);

			return AsyncQuery.Flatten(this, new AsyncTransformExpression<TResult, IAsyncQuery<TNew>>(selector));
		}

		#endregion

		#region Take/TakeWhile/Skip...

		/// <inheritdoc />
		public virtual IAsyncLinqQuery<TResult> Take(int count)
		{
			return AsyncQuery.TakeImpl(this, count);
		}

		/// <inheritdoc />
		public virtual IAsyncLinqQuery<TResult> TakeWhile(Func<TResult, bool> condition)
		{
			return AsyncQuery.TakeWhileImpl(this, condition);
		}

		/// <inheritdoc />
		public virtual IAsyncLinqQuery<TResult> Skip(int count)
		{
			return AsyncQuery.SkipImpl(this, count);
		}

		#endregion

		/// <summary>Execute an action on the result of this async sequence</summary>
		public virtual Task ExecuteAsync(Action<TResult> handler)
		{
			return AsyncQuery.Run(this, AsyncIterationHint.All, handler);
		}

		/// <summary>Execute an action on the result of this async sequence</summary>
		public virtual Task ExecuteAsync<TState>(TState state, Action<TState, TResult> handler)
		{
			return AsyncQuery.Run(this, AsyncIterationHint.All, state, handler);
		}

		/// <summary>Execute an action on the result of this async sequence</summary>
		public virtual Task<TAggregate> ExecuteAsync<TAggregate>(TAggregate seed, Func<TAggregate, TResult, TAggregate> handler)
		{
			return AsyncQuery.Run(this, AsyncIterationHint.All, seed, handler);
		}

		public virtual Task ExecuteAsync(Func<TResult, CancellationToken, Task> handler)
		{
			return AsyncQuery.Run(this, AsyncIterationHint.All, handler);
		}

		public virtual Task ExecuteAsync<TState>(TState state, Func<TState, TResult, CancellationToken, Task> handler)
		{
			return AsyncQuery.Run(this, AsyncIterationHint.All, state, handler);
		}

		#endregion

		#region Iterator Impl...

		protected abstract ValueTask<bool> OnFirstAsync();

		protected abstract ValueTask<bool> OnNextAsync();

		protected bool Publish(TResult current)
		{
			if (Volatile.Read(ref m_state) == STATE_ITERATING)
			{
				m_current = current;
				return true;
			}
			return false;
		}

		protected async ValueTask<bool> Completed()
		{
			if (Volatile.Read(ref m_state) == STATE_INIT)
			{ // nothing should have been done by the iterator
				Interlocked.CompareExchange(ref m_state, STATE_COMPLETED, STATE_INIT);
			}
			else if (Interlocked.CompareExchange(ref m_state, STATE_COMPLETED, STATE_ITERATING) == STATE_ITERATING)
			{ // the iterator has done at least something, so we can clean it up
				await Cleanup().ConfigureAwait(false);
			}
			return false;
		}

		/// <summary>Mark the current iterator as failed, and clean up the state</summary>
		protected ValueTask MarkAsFailed()
		{
			//TODO: store the state "failed" somewhere?
			return DisposeAsync();
		}

		protected async ValueTask<bool> Canceled()
		{
			//TODO: store the state "canceled" somewhere?
			await DisposeAsync().ConfigureAwait(false);
			this.Cancellation.ThrowIfCancellationRequested(); // should throw here!
			return false; //note: should not be reached
		}

		[MethodImpl(MethodImplOptions.NoInlining)]
		protected void EnsureIsIterating()
		{
			switch (Volatile.Read(ref m_state))
			{
				case STATE_SEQ:
					throw ThrowHelper.InvalidOperationException("The async iterator should have been initialized with a call to GetEnumerator()");

				case STATE_ITERATING:
					break;

				case STATE_DISPOSED:
					throw ThrowHelper.ObjectDisposedException(this, "The async iterator has already been closed");

				default:
				{
					throw ThrowHelper.InvalidOperationException("Unexpected state");
				}
			}
		}

		protected abstract ValueTask Cleanup();

		#endregion

		#region IAsyncDisposable...

		public virtual async ValueTask DisposeAsync()
		{
			if (Interlocked.Exchange(ref m_state, STATE_DISPOSED) != STATE_DISPOSED)
			{
				try
				{
					await Cleanup().ConfigureAwait(false);
				}
				finally
				{
					m_current = default!;
					GC.SuppressFinalize(this);
				}
			}
		}

		#endregion

	}

}
