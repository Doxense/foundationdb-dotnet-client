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
	using Doxense.Linq.Iterators;

	/// <summary>Provides a set of static methods for querying objects that implement <see cref="IEnumerable{T}"/>.</summary>
	public static class EnumerableExtensions
	{

		//TODO: add Batch(...) ?
		//TODO: maybe merge with CollectionExtensions.cs?

		/// <summary>Determines whether a sequence contains no elements at all.</summary>
		/// <remarks>This is the logical equivalent to "source.Count() == 0" or "!source.Any()" but can be better optimized by some providers</remarks>
		public static bool None<T>([InstantHandle] this IEnumerable<T> source)
		{
			Contract.NotNull(source);

			using (var iterator = source.GetEnumerator())
			{
				return !iterator.MoveNext();
			}
		}

		/// <summary>Determines whether none of the elements of a sequence satisfies a condition.</summary>
		public static bool None<T>([InstantHandle] this IEnumerable<T> source, [InstantHandle] Func<T, bool> predicate)
		{
			Contract.NotNull(source);
			Contract.NotNull(predicate);

			using (var iterator = source.GetEnumerator())
			{
				while (iterator.MoveNext())
				{
					if (predicate(iterator.Current))
					{
						return false;
					}
				}
			}
			return true;
		}

		#region Query Statistics...

		//TODO: move this somewhere else?

		/// <summary>Measure the number of items that pass through this point of the query</summary>
		/// <remarks>The values returned in <paramref name="counter"/> are only safe to read once the query has ended</remarks>
		[LinqTunnel]
		public static IEnumerable<TSource> WithCountStatistics<TSource>(this IEnumerable<TSource> source, out QueryStatistics<int> counter)
		{
			Contract.NotNull(source);

			var signal = new QueryStatistics<int>(0);
			counter = signal;

			// to count, we just increment the signal each type a value flows through here
			return source.Select((x) =>
			{
				signal.Update(checked(signal.Value + 1));
				return x;
			});
		}

		/// <summary>Measure the number and size of slices that pass through this point of the query</summary>
		/// <remarks>The values returned in <paramref name="statistics"/> are only safe to read once the query has ended</remarks>
		[LinqTunnel]
		public static IEnumerable<KeyValuePair<Slice, Slice>> WithSizeStatistics(this IEnumerable<KeyValuePair<Slice, Slice>> source, out QueryStatistics<KeyValueSizeStatistics> statistics)
		{
			Contract.NotNull(source);

			var data = new KeyValueSizeStatistics();
			statistics = new QueryStatistics<KeyValueSizeStatistics>(data);

			// to count, we just increment the signal each type a value flows through here
			return source.Select((kvp) =>
			{
				data.Add(kvp.Key.Count, kvp.Value.Count);
				return kvp;
			});
		}

		/// <summary>Measure the number and sizes of the keys and values that pass through this point of the query</summary>
		/// <remarks>The values returned in <paramref name="statistics"/> are only safe to read once the query has ended</remarks>
		[LinqTunnel]
		public static IEnumerable<Slice> WithSizeStatistics(this IEnumerable<Slice> source, out QueryStatistics<DataSizeStatistics> statistics)
		{
			Contract.NotNull(source);

			var data = new DataSizeStatistics();
			statistics = new QueryStatistics<DataSizeStatistics>(data);

			// to count, we just increment the signal each type a value flows through here
			return source.Select((x) =>
			{
				data.Add(x.Count);
				return x;
			});
		}

		/// <summary>Execute an action on each item passing through the sequence, without modifying the original sequence</summary>
		/// <remarks>The <paramref name="handler"/> is executed inline before passing the item down the line, and should not block</remarks>
		[LinqTunnel]
		public static IEnumerable<TSource> Observe<TSource>(this IEnumerable<TSource> source, Action<TSource> handler)
		{
			Contract.NotNull(source);
			Contract.NotNull(handler);

			return new ObserverIterator<TSource>(source, handler);
		}

		#endregion

	}

}
