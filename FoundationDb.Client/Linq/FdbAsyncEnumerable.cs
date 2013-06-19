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

		/// <summary>Transform each elements of an inner async sequence into a new async sequence</summary>
		public static IFdbAsyncEnumerable<R> Select<T, R>(this IFdbAsyncEnumerable<T> source, Func<T, R> lambda)
		{
			if (source == null) throw new ArgumentNullException("source");
			if (lambda == null) throw new ArgumentNullException("lambda");

			return Create(source, (iterator) => new AsyncSelector<T, R>(iterator, lambda));
		}

		/// <summary>Transform each elements of an inner async sequence into a new async sequence</summary>
		public static IFdbAsyncEnumerable<R> Select<T, R>(this IFdbAsyncEnumerable<T> source, Func<T, int, R> lambda)
		{
			if (source == null) throw new ArgumentNullException("source");
			if (lambda == null) throw new ArgumentNullException("lambda");

			return Create(source, (iterator) => new AsyncIndexedSelector<T, R>(iterator, lambda));
		}

		/// <summary>Apply an async lambda to a sequence of elements to transform it into an async sequence</summary>
		public static IFdbAsyncEnumerable<R> ToAsyncEnumerable<T, R>(this IEnumerable<T> source, Func<T, Task<R>> lambda)
		{
			if (source == null) throw new ArgumentNullException("source");
			if (lambda == null) throw new ArgumentNullException("lambda");

			return new AnonymousSelectable<T, R>(source, (iterator) => new Selector<T, R>(iterator, lambda));
		}

		/// <summary>Execute an action for each element of an async sequence</summary>
		public static Task ForEach<T>(this IFdbAsyncEnumerable<T> source, Action<T> action, CancellationToken ct = default(CancellationToken))
		{
			if (source == null) throw new ArgumentNullException("source");
			if (action == null) throw new ArgumentNullException("action");

			return Iterate<T>(source, action, ct);
		}

		/// <summary>Execute an async action for each element of an async sequence</summary>
		public static Task ForEach<T>(this IFdbAsyncEnumerable<T> source, Func<T, Task> action, CancellationToken ct = default(CancellationToken))
		{
			if (source == null) throw new ArgumentNullException("source");
			if (action == null) throw new ArgumentNullException("action");

			return Iterate<T>(source, action, ct);
		}

		/// <summary>Return a list of all the elements from an async sequence</summary>
		public static async Task<List<T>> ToListAsync<T>(this IFdbAsyncEnumerable<T> source, CancellationToken ct = default(CancellationToken))
		{
			var list = new List<T>();
			await Iterate<T>(source, (x) => list.Add(x), ct);
			return list;
		}
		
		/// <summary>Returns the first element of an async sequence, or an exception if it is empty</summary>
		public static Task<T> First<T>(this IFdbAsyncEnumerable<T> source, CancellationToken ct = default(CancellationToken))
		{
			return Head<T>(source, ct, single: false, orDefault: false);
		}

		/// <summary>Returns the first element of an async sequence, or the default value for the type if it is empty</summary>
		public static Task<T> FirstOrDefault<T>(this IFdbAsyncEnumerable<T> source, CancellationToken ct = default(CancellationToken))
		{
			return Head<T>(source, ct, single: false, orDefault: true);
		}

		/// <summary>Returns the first and only element of an async sequence, or an exception if it is empty or have two or more elements</summary>
		/// <remarks>Will need to call MoveNext at least twice to ensure that there is no second element.</remarks>
		public static Task<T> Single<T>(this IFdbAsyncEnumerable<T> source, CancellationToken ct = default(CancellationToken))
		{
			return Head<T>(source, ct, single: true, orDefault: false);
		}

		/// <summary>Returns the first and only element of an async sequence, the default value for the type if it is empty, or an exception if it has two or more elements</summary>
		/// <remarks>Will need to call MoveNext at least twice to ensure that there is no second element.</remarks>
		public static Task<T> SingleOrDefault<T>(this IFdbAsyncEnumerable<T> source, CancellationToken ct = default(CancellationToken))
		{
			return Head<T>(source, ct, single: true, orDefault: true);
		}

		/// <summary>Returns the last element of an async sequence, or an exception if it is empty</summary>
		public static async Task<T> Last<T>(this IFdbAsyncEnumerable<T> source, CancellationToken ct = default(CancellationToken))
		{
			bool found = false;
			T last = default(T);

			await Iterate<T>(source, (x) => { found = true; last = x; }, ct);

			if (!found) throw new InvalidOperationException("The sequence was empty");
			return last;
		}

		/// <summary>Returns the last element of an async sequence, or the default value for the type if it is empty</summary>
		public static async Task<T> LastOrDefault<T>(this IFdbAsyncEnumerable<T> source, CancellationToken ct = default(CancellationToken))
		{
			bool found = false;
			T last = default(T);

			await Iterate<T>(source, (x) => { found = true; last = x; }, ct);

			return found ? last : default(T);
		}

	}

}
