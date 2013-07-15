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
	* Neither the name of Doxense nor the
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

namespace FoundationDB.Client
{
	using FoundationDB.Async;
	using FoundationDB.Linq;
	using System;
	using System.Collections.Generic;
	using System.Linq;

	public static class FdbMergeQueryExtensions
	{

		#region MergeSort (x OR y)

		public static IFdbAsyncEnumerable<KeyValuePair<Slice, Slice>> MergeSort<TKey>(this IFdbReadTransaction trans, IEnumerable<FdbKeySelectorPair> ranges, Func<KeyValuePair<Slice, Slice>, TKey> keySelector, IComparer<TKey> keyComparer = null)
		{
			//TODO: Range options ?

			trans.EnsureCanRead();
			return new FdbMergeSortIterator<KeyValuePair<Slice, Slice>, TKey, KeyValuePair<Slice, Slice>>(
				ranges.Select(range => trans.GetRange(range, new FdbRangeOptions { StreamingMode = FdbStreamingMode.Iterator })),
				default(int?),
				keySelector,
				TaskHelpers.Cache<KeyValuePair<Slice, Slice>>.Identity,
				keyComparer
			);
		}

		public static IFdbAsyncEnumerable<TResult> MergeSort<TKey, TResult>(this IFdbReadTransaction trans, IEnumerable<FdbKeySelectorPair> ranges, Func<KeyValuePair<Slice, Slice>, TKey> keySelector, Func<KeyValuePair<Slice, Slice>, TResult> resultSelector, IComparer<TKey> keyComparer = null)
		{
			//TODO: Range options ?

			trans.EnsureCanRead();
			return new FdbMergeSortIterator<KeyValuePair<Slice, Slice>, TKey, TResult>(
				ranges.Select(range => trans.GetRange(range, new FdbRangeOptions { StreamingMode = FdbStreamingMode.Iterator })),
				default(int?),
				keySelector,
				resultSelector,
				keyComparer
			);
		}

		#endregion

		#region Intersect (x AND y)

		public static IFdbAsyncEnumerable<KeyValuePair<Slice, Slice>> Intersect<TKey>(this IFdbReadTransaction trans, IEnumerable<FdbKeySelectorPair> ranges, Func<KeyValuePair<Slice, Slice>, TKey> keySelector, IComparer<TKey> keyComparer = null)
		{
			//TODO: Range options ?

			trans.EnsureCanRead();
			return new FdbIntersectIterator<KeyValuePair<Slice, Slice>, TKey, KeyValuePair<Slice, Slice>>(
				ranges.Select(range => trans.GetRange(range, new FdbRangeOptions { StreamingMode = FdbStreamingMode.Iterator })),
				default(int?),
				keySelector,
				TaskHelpers.Cache<KeyValuePair<Slice, Slice>>.Identity,
				keyComparer
			);
		}

		public static IFdbAsyncEnumerable<TResult> Intersect<TKey, TResult>(this IFdbReadTransaction trans, IEnumerable<FdbKeySelectorPair> ranges, Func<KeyValuePair<Slice, Slice>, TKey> keySelector, Func<KeyValuePair<Slice, Slice>, TResult> resultSelector, IComparer<TKey> keyComparer = null)
		{
			//TODO: Range options ?

			trans.EnsureCanRead();
			return new FdbIntersectIterator<KeyValuePair<Slice, Slice>, TKey, TResult>(
				ranges.Select(range => trans.GetRange(range, new FdbRangeOptions { StreamingMode = FdbStreamingMode.Iterator })),
				default(int?),
				keySelector,
				resultSelector,
				keyComparer
			);
		}

		public static IFdbAsyncEnumerable<TResult> Intersect<TKey, TResult>(this IFdbAsyncEnumerable<TResult> first, IFdbAsyncEnumerable<TResult> second, Func<TResult, TKey> keySelector, IComparer<TKey> keyComparer = null)
		{
			return new FdbIntersectIterator<TResult, TKey, TResult>(
				new[] { first, second },
				null,
				keySelector,
				TaskHelpers.Cache<TResult>.Identity,
				keyComparer
			);
		}

		public static IFdbAsyncEnumerable<TResult> Intersect<TResult>(this IFdbAsyncEnumerable<TResult> first, IFdbAsyncEnumerable<TResult> second, IComparer<TResult> comparer = null)
		{
			return new FdbIntersectIterator<TResult, TResult, TResult>(
				new [] { first, second },
				null,
				TaskHelpers.Cache<TResult>.Identity,
				TaskHelpers.Cache<TResult>.Identity,
				comparer
			);
		}

		#endregion

		#region Except (x AND NOT y)

		public static IFdbAsyncEnumerable<KeyValuePair<Slice, Slice>> Except<TKey>(this IFdbReadTransaction trans, IEnumerable<FdbKeySelectorPair> ranges, Func<KeyValuePair<Slice, Slice>, TKey> keySelector, IComparer<TKey> keyComparer = null)
		{
			//TODO: Range options ?

			trans.EnsureCanRead();
			return new FdbExceptIterator<KeyValuePair<Slice, Slice>, TKey, KeyValuePair<Slice, Slice>>(
				ranges.Select(range => trans.GetRange(range, new FdbRangeOptions { StreamingMode = FdbStreamingMode.Iterator })),
				default(int?),
				keySelector,
				TaskHelpers.Cache<KeyValuePair<Slice, Slice>>.Identity,
				keyComparer
			);
		}

		public static IFdbAsyncEnumerable<TResult> Except<TKey, TResult>(this IFdbReadTransaction trans, IEnumerable<FdbKeySelectorPair> ranges, Func<KeyValuePair<Slice, Slice>, TKey> keySelector, Func<KeyValuePair<Slice, Slice>, TResult> resultSelector, IComparer<TKey> keyComparer = null)
		{
			//TODO: Range options ?

			trans.EnsureCanRead();
			return new FdbExceptIterator<KeyValuePair<Slice, Slice>, TKey, TResult>(
				ranges.Select(range => trans.GetRange(range, new FdbRangeOptions { StreamingMode = FdbStreamingMode.Iterator })),
				default(int?),
				keySelector,
				resultSelector,
				keyComparer
			);
		}

		public static IFdbAsyncEnumerable<TResult> Except<TKey, TResult>(this IFdbAsyncEnumerable<TResult> first, IFdbAsyncEnumerable<TResult> second, Func<TResult, TKey> keySelector, IComparer<TKey> keyComparer = null)
		{
			return new FdbExceptIterator<TResult, TKey, TResult>(
				new[] { first, second },
				null,
				keySelector,
				TaskHelpers.Cache<TResult>.Identity,
				keyComparer
			);
		}

		public static IFdbAsyncEnumerable<TResult> Except<TResult>(this IFdbAsyncEnumerable<TResult> first, IFdbAsyncEnumerable<TResult> second, IComparer<TResult> comparer = null)
		{
			return new FdbExceptIterator<TResult, TResult, TResult>(
				new[] { first, second },
				null,
				TaskHelpers.Cache<TResult>.Identity,
				TaskHelpers.Cache<TResult>.Identity,
				comparer
			);
		}

		#endregion

	}

}
