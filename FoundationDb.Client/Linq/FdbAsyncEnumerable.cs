#region BSD Licence
/* Copyright (c) 2013, Doxense SARL
All rights reserved.

Redistribution and use in source and binary forms, with or without
modification, are permitted provided that the following conditions are met:
	* Redistributions of source code must retain the above copyright
	  notice, this list of conditions and the following disclaimer.
	* Redistributions in binary form must reproduce the above copyright
	  notice, this list of conditions and the following disclaimer in the
	  documentation and/or other materials provided with the distribution.
	* Neither the name of the <organization> nor the
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

namespace FoundationDb.Linq
{
	using System;
	using System.Collections.Generic;
	using System.Threading;
	using System.Threading.Tasks;

	public static partial class FdbAsyncEnumerable
	{
		// Welcome to the wonderful world of the Monads! 

		private sealed class EmptySequence<T> : IFdbAsyncEnumerable<T>, IFdbAsyncEnumerator<T>
		{

			Task<bool> IFdbAsyncEnumerator<T>.MoveNext(CancellationToken cancellationToken)
			{
				cancellationToken.ThrowIfCancellationRequested();
				return FalseTask;
			}

			T IFdbAsyncEnumerator<T>.Current
			{
				get { throw new InvalidOperationException("This sequence is emty"); }
			}

			void IDisposable.Dispose()
			{
				// NOOP!
			}

			public IFdbAsyncEnumerator<T> GetEnumerator()
			{
				return this;
			}
		}

		public static IFdbAsyncEnumerable<T> Empty<T>()
		{
			return new EmptySequence<T>();
		}

		/// <summary>Transform each elements of an inner async sequence into a new async sequence</summary>
		public static IFdbAsyncEnumerable<R> Select<T, R>(this IFdbAsyncEnumerable<T> source, Func<T, R> lambda)
		{
			if (source == null) throw new ArgumentNullException("source");
			if (lambda == null) throw new ArgumentNullException("lambda");

			return Create(source, (iterator) => Iterate<T, R>(iterator, lambda));
		}

		/// <summary>Transform each elements of an inner async sequence into a new async sequence</summary>
		public static IFdbAsyncEnumerable<R> Select<T, R>(this IFdbAsyncEnumerable<T> source, Func<T, int, R> lambda)
		{
			if (source == null) throw new ArgumentNullException("source");
			if (lambda == null) throw new ArgumentNullException("lambda");

			return Create<T, R>(source, (iterator) =>
			{
				int index = 0;
				return Iterate<T, R>(
					iterator,
					(x) =>
					{
						return lambda(x, index++);
					}
				);
			});		}

		/// <summary>Transform each elements of an inner async sequence into a new async sequence</summary>
		public static IFdbAsyncEnumerable<R> Select<T, R>(this IFdbAsyncEnumerable<T> source, Func<T, Task<R>> lambda)
		{
			if (source == null) throw new ArgumentNullException("source");
			if (lambda == null) throw new ArgumentNullException("lambda");

			return Create<T, R>(source, (iterator) => Iterate<T, R>(iterator, lambda));
		}

		/// <summary>Transform each elements of an inner async sequence into a new async sequence</summary>
		public static IFdbAsyncEnumerable<R> Select<T, R>(this IFdbAsyncEnumerable<T> source, Func<T, int, Task<R>> lambda)
		{
			if (source == null) throw new ArgumentNullException("source");
			if (lambda == null) throw new ArgumentNullException("lambda");

			return Create<T, R>(source, (iterator) =>
			{
				int index = 0;
				return Iterate<T, R>(
					iterator,
					(x) =>
					{
						return lambda(x, index++);
					}
				);
			});
		}

		/// <summary>Apply an async lambda to a sequence of elements to transform it into an async sequence</summary>
		public static IFdbAsyncEnumerable<R> ToAsyncEnumerable<T, R>(this IEnumerable<T> source, Func<T, Task<R>> lambda)
		{
			if (source == null) throw new ArgumentNullException("source");
			if (lambda == null) throw new ArgumentNullException("lambda");

			return new EnumerableSelectable<T, R>(source, (iterator) => new EnumerableSelector<T, R>(iterator, lambda));
		}

		/// <summary>Apply an async lambda to a sequence of elements to transform it into an async sequence</summary>
		public static IFdbAsyncEnumerable<T> ToAsyncEnumerable<T>(this IEnumerable<T> source)
		{
			if (source == null) throw new ArgumentNullException("source");

			return new EnumerableSelectable<T, T>(source, (iterator) => new EnumerableSelector<T, T>(iterator, x => Task.FromResult(x)));
		}

		/// <summary>Execute an action for each element of an async sequence</summary>
		public static Task ForEach<T>(this IFdbAsyncEnumerable<T> source, Action<T> action, CancellationToken ct = default(CancellationToken))
		{
			if (source == null) throw new ArgumentNullException("source");
			if (action == null) throw new ArgumentNullException("action");

			return Run<T>(source, action, ct);
		}

		/// <summary>Execute an async action for each element of an async sequence</summary>
		public static Task ForEach<T>(this IFdbAsyncEnumerable<T> source, Func<T, Task> action, CancellationToken ct = default(CancellationToken))
		{
			if (source == null) throw new ArgumentNullException("source");
			if (action == null) throw new ArgumentNullException("action");

			return Run<T>(source, action, ct);
		}

		/// <summary>Return a list of all the elements from an async sequence</summary>
		public static async Task<List<T>> ToListAsync<T>(this IFdbAsyncEnumerable<T> source, CancellationToken ct = default(CancellationToken))
		{
			var list = new List<T>();
			await Run<T>(source, (x) => list.Add(x), ct);
			return list;
		}
		
		/// <summary>Returns the first element of an async sequence, or an exception if it is empty</summary>
		public static Task<T> First<T>(this IFdbAsyncEnumerable<T> source, CancellationToken ct = default(CancellationToken))
		{
			return Head<T>(source, single: false, orDefault: false, ct: ct);
		}

		/// <summary>Returns the first element of an async sequence, or the default value for the type if it is empty</summary>
		public static Task<T> FirstOrDefault<T>(this IFdbAsyncEnumerable<T> source, CancellationToken ct = default(CancellationToken))
		{
			return Head<T>(source, single: false, orDefault: true, ct: ct);
		}

		/// <summary>Returns the first and only element of an async sequence, or an exception if it is empty or have two or more elements</summary>
		/// <remarks>Will need to call MoveNext at least twice to ensure that there is no second element.</remarks>
		public static Task<T> Single<T>(this IFdbAsyncEnumerable<T> source, CancellationToken ct = default(CancellationToken))
		{
			return Head<T>(source, single: true, orDefault: false, ct: ct);
		}

		/// <summary>Returns the first and only element of an async sequence, the default value for the type if it is empty, or an exception if it has two or more elements</summary>
		/// <remarks>Will need to call MoveNext at least twice to ensure that there is no second element.</remarks>
		public static Task<T> SingleOrDefault<T>(this IFdbAsyncEnumerable<T> source, CancellationToken ct = default(CancellationToken))
		{
			return Head<T>(source, single: true, orDefault: true, ct: ct);
		}

		/// <summary>Returns the last element of an async sequence, or an exception if it is empty</summary>
		public static async Task<T> Last<T>(this IFdbAsyncEnumerable<T> source, CancellationToken ct = default(CancellationToken))
		{
			bool found = false;
			T last = default(T);

			await Run<T>(source, (x) => { found = true; last = x; }, ct);

			if (!found) throw new InvalidOperationException("The sequence was empty");
			return last;
		}

		/// <summary>Returns the last element of an async sequence, or the default value for the type if it is empty</summary>
		public static async Task<T> LastOrDefault<T>(this IFdbAsyncEnumerable<T> source, CancellationToken ct = default(CancellationToken))
		{
			bool found = false;
			T last = default(T);

			await Run<T>(source, (x) => { found = true; last = x; }, ct);

			return found ? last : default(T);
		}

	}

}
