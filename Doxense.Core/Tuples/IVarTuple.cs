#region Copyright (c) 2005-2023 Doxense SAS
// All rights reserved.
// 
// Redistribution and use in source and binary forms, with or without
// modification, are permitted provided that the following conditions are met:
// 	* Redistributions of source code must retain the above copyright
// 	  notice, this list of conditions and the following disclaimer.
// 	* Redistributions in binary form must reproduce the above copyright
// 	  notice, this list of conditions and the following disclaimer in the
// 	  documentation and/or other materials provided with the distribution.
// 	* Neither the name of Doxense nor the
// 	  names of its contributors may be used to endorse or promote products
// 	  derived from this software without specific prior written permission.
// 
// THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND
// ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
// WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
// DISCLAIMED. IN NO EVENT SHALL DOXENSE BE LIABLE FOR ANY
// DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
// (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
// LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
// ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
// (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
// SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
#endregion

#if !USE_SHARED_FRAMEWORK

namespace Doxense.Collections.Tuples
{
	using System;
	using System.Collections;
	using System.Collections.Generic;
	using System.ComponentModel;

	/// <summary>Represents a Tuple of variable length and elements of different types</summary>
	[ImmutableObject(true)]
	[JetBrains.Annotations.CannotApplyEqualityOperator]
	[JetBrains.Annotations.PublicAPI]
	public interface IVarTuple : IEquatable<IVarTuple>, IReadOnlyList<object?>, System.Collections.IStructuralEquatable
	{
		// Tuples should, by default, behave as closely to Python's tuples as possible. See http://docs.python.org/2/tutorial/datastructures.html#tuples-and-sequences

		// Implementation notes:
		// - Tuples are an immutable list of "objects", that can be indexed from the start or the end (negative indexes)
		// - Unless specified otherwise, end offsets are usually EXCLUDED.
		// - Appending to a tuple returns a new tuple (does not mutate the previous)
		// - Getting the substring of a tuple return a new tuple that tries to reuse the objects of the parent tuple
		// - There are no guarantees that two different tuples containing the "same" values return the same HashCode, meaning that it should not be used as keys in a Dictionary

		// Performance notes:
		// - Accessing the Count and Last item should be fast, if possible in O(1)
		// - Appending should also be fast, if possible O(1)
		// - Getting the substring of a tuple should as fast as possible, if possible O(1). For list-based tuples, it should return a view of the list (offset/count) and avoid copying the list
		// - If an operation returns an empty tuple, then it should return the STuple.Empty singleton instance
		// - If an operation does not change the tuple (like Append(STuple.Empty), or tuple.Substring(0)), then the tuple should return itself
		// - If the same tuple will be packed frequently, it should be memoized (converted into a MemoizedTuple)


		//TODO: BUGBUG: the old name (ITuple) collides with System.Runtime.CompilerServices.ITuple which is made public in .NET 4.7.1+
		// This interfaces defines an indexer, and the property "Length", but we are using "Count" which comes from IReadOnlyList<object> ...

		/// <summary>Return the element at the specified index</summary>
		/// <param name="index">Index of the element to return. Supports negative indexing (-1 means last)</param>
		new object? this[int index] { get; }

		/// <summary>Return a section of the tuple</summary>
		/// <param name="fromIncluded">Starting offset of the sub-tuple to return, or null to select from the start. Negative values means from the end</param>
		/// <param name="toExcluded">Ending offset (excluded) of the sub-tuple to return or null to select until the end. Negative values means from the end.</param>
		/// <returns>Tuple that include all items in the current tuple whose offset are greater than or equal to <paramref name="fromIncluded"/> and strictly less than <paramref name="toExcluded"/>. The tuple may be smaller than expected if the range is larger than the parent tuple. If the range does not intersect with the tuple, the Empty tuple will be returned.</returns>
		IVarTuple this[int? fromIncluded, int? toExcluded] { [JetBrains.Annotations.Pure] get; }

#if USE_RANGE_API

		/// <summary>Return the element at the specified index</summary>
		object? this[Index index] { get; }

		/// <summary>Return a section of the tuple</summary>
		/// <param name="range">Range of sub-tuple to return</param>
		/// <returns>Tuple that include all items in the current tuple whose index are in the specified <paramref name="range"/>. If the range does not intersect with the tuple, the Empty tuple will be returned.</returns>
		IVarTuple this[Range range] { get;}

#endif

		/// <summary>Return the typed value of an item of the tuple, given its position</summary>
		/// <typeparam name="TItem">Expected type of the item</typeparam>
		/// <param name="index">Position of the item (if negative, means relative from the end)</param>
		/// <returns>Value of the item at position <paramref name="index"/>, adapted into type <typeparamref name="TItem"/>.</returns>
		/// <exception cref="System.IndexOutOfRangeException">If <paramref name="index"/> is outside the bounds of the tuple</exception>
		/// <example>
		/// ("Hello", "World", 123,).Get&lt;string&gt;(0) => "Hello"
		/// ("Hello", "World", 123,).Get&lt;int&gt;(-1) => 123
		/// ("Hello", "World", 123,).Get&lt;string&gt;(-1) => "123"
		/// </example>
		[JetBrains.Annotations.Pure]
		TItem Get<TItem>(int index);

		/// <summary>Create a new Tuple by appending a single new value at the end of this tuple</summary>
		/// <typeparam name="TItem">Type of the new value</typeparam>
		/// <param name="value">Value that will be appended at the end</param>
		/// <returns>New tuple with the new value</returns>
		/// <example>("Hello,").Append("World") => ("Hello", "World",)</example>
		/// <remarks>If <typeparamref name="TItem"/> is an <see cref="IVarTuple"/>, then it will be appended as a single element. If you need to append the *items* of a tuple, you must call <see cref="IVarTuple.Concat"/></remarks>
		[JetBrains.Annotations.Pure]
		IVarTuple Append<TItem>(TItem value);

		/// <summary>Create a new Tuple by appending the items of another tuple at the end of this tuple</summary>
		/// <param name="tuple">Tuple whose items must be appended at the end of the current tuple</param>
		/// <returns>New tuple with the new values, or the same instance if <paramref name="tuple"/> is empty.</returns>
		[JetBrains.Annotations.Pure]
		IVarTuple Concat(IVarTuple tuple);

		/// <summary>Copy all items of the tuple into an array at a specific location</summary>
		/// <param name="array">Destination array (must be big enough to contains all the items)</param>
		/// <param name="offset">Offset at which to start copying items</param>
		/// <example>
		/// var tmp = new object[3];
		/// ("Hello", "World", 123,).CopyTo(tmp, 0);
		/// </example>
		void CopyTo(object?[] array, int offset);

		int GetItemHashCode(int index, IEqualityComparer comparer);

	}

}

#endif
