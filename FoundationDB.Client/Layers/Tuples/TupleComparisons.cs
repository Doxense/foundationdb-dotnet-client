#region BSD Licence
/* Copyright (c) 2013-2018, Doxense SAS
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

namespace Doxense.Collections.Tuples
{
	using System;
	using System.Collections;
	using System.Collections.Generic;
	using System.Runtime.CompilerServices;
	using Doxense.Runtime.Converters;
	using JetBrains.Annotations;

	/// <summary>Helper class for tuple comparisons</summary>
	public static class TupleComparisons
	{
		/// <summary>Tuple comparer that treats similar values as equal ("123" = 123 = 123L = 123.0d)</summary>
		public static readonly EqualityComparer Default = new EqualityComparer(SimilarValueComparer.Default);

		/// <summary>Tuple comparer that uses the default BCL object comparison ("123" != 123 != 123L != 123.0d)</summary>
		public static readonly EqualityComparer Bcl = new EqualityComparer(EqualityComparer<object>.Default);

#if false
		/// <summary>Tuple comparer that compared the packed bytes (slow!)</summary>
		public static readonly BinaryComparer Binary = new BinaryComparer();
#endif

		public sealed class EqualityComparer : IEqualityComparer<ITuple>, IEqualityComparer
		{
			private readonly IEqualityComparer m_comparer;

			internal EqualityComparer(IEqualityComparer comparer)
			{
				m_comparer = comparer;
			}

			public bool Equals(ITuple x, ITuple y)
			{
				if (object.ReferenceEquals(x, y)) return true;
				if (object.ReferenceEquals(x, null) || object.ReferenceEquals(y, null)) return false;

				return x.Equals(y, m_comparer);
			}

			public int GetHashCode(ITuple obj)
			{
				return HashCodes.Compute(obj, m_comparer);
			}

			public new bool Equals(object x, object y)
			{
				if (object.ReferenceEquals(x, y)) return true;
				if (x == null || y == null) return false;

				if (x is ITuple t) return t.Equals(y, m_comparer);

				t = y as ITuple;
				if (t != null) return t.Equals(x, m_comparer);

				return false;
			}

			public int GetHashCode(object obj)
			{
				if (obj == null) return 0;

				var t = obj as ITuple;
				if (!object.ReferenceEquals(t, null)) return t.GetHashCode(m_comparer);

				// returns a hash base on the pointers
				return RuntimeHelpers.GetHashCode(obj);
			}
		}

#if false

		public sealed class BinaryComparer : IEqualityComparer<ITuple>, IEqualityComparer
		{
			internal BinaryComparer()
			{ }


			public bool Equals(ITuple x, ITuple y)
			{
				if (object.ReferenceEquals(x, y)) return true;
				if (object.ReferenceEquals(x, null) || object.ReferenceEquals(y, null)) return false;

				return x.ToSlice().Equals(y.ToSlice());
			}

			public int GetHashCode(ITuple obj)
			{
				return object.ReferenceEquals(obj, null) ? 0 : obj.ToSlice().GetHashCode();
			}

			public new bool Equals(object x, object y)
			{
				if (object.ReferenceEquals(x, y)) return true;
				if (x == null || y == null) return false;

				var tx = x as ITuple;
				var ty = y as ITuple;
				if (object.ReferenceEquals(tx, null) || object.ReferenceEquals(ty, null)) return false;
				return tx.ToSlice().Equals(ty.ToSlice());
			}

			public int GetHashCode(object obj)
			{
				if (obj == null) return 0;

				var tuple = obj as ITuple;
				if (!object.ReferenceEquals(tuple, null)) return tuple.ToSlice().GetHashCode();

				return RuntimeHelpers.GetHashCode(obj);
			}
		}

#endif

		/// <summary>Create a new instance that compares a single item position in two tuples</summary>
		/// <typeparam name="T1">Type of the item to compare</typeparam>
		/// <param name="offset">Offset of the item to compare (can be negative)</param>
		/// <param name="comparer">Comparer for the item's type</param>
		/// <returns>New comparer instance</returns>
		[NotNull]
		public static IComparer<ITuple> Composite<T1>(int offset = 0, IComparer<T1> comparer = null)
		{
			return new CompositeComparer<T1>(offset, comparer);
		}

		/// <summary>Create a new instance that compares two consecutive items in two tuples</summary>
		/// <typeparam name="T1">Type of the first item to compare</typeparam>
		/// <typeparam name="T2">Type of the second item to compare</typeparam>
		/// <param name="offset">Offset of the first item to compare (can be negative)</param>
		/// <param name="comparer1">Comparer for the first item's type</param>
		/// <param name="comparer2">Comparer for the second item's type</param>
		/// <returns>New comparer instance</returns>
		[NotNull]
		public static IComparer<ITuple> Composite<T1, T2>(int offset = 0, IComparer<T1> comparer1 = null, IComparer<T2> comparer2 = null)
		{
			return new CompositeComparer<T1, T2>(offset, comparer1, comparer2);
		}

		/// <summary>Create a new instance that compares three consecutive items in two tuples</summary>
		/// <typeparam name="T1">Type of the first item to compare</typeparam>
		/// <typeparam name="T2">Type of the second item to compare</typeparam>
		/// <typeparam name="T3">Type of the third item to compare</typeparam>
		/// <param name="offset">Offset of the first item to compare (can be negative)</param>
		/// <param name="comparer1">Comparer for the first item's type</param>
		/// <param name="comparer2">Comparer for the second item's type</param>
		/// <param name="comparer3">Comparer for the third item's type</param>
		/// <returns>New comparer instance</returns>
		[NotNull]
		public static IComparer<ITuple> Composite<T1, T2, T3>(int offset = 0, IComparer<T1> comparer1 = null, IComparer<T2> comparer2 = null, IComparer<T3> comparer3 = null)
		{
			return new CompositeComparer<T1, T2, T3>(offset, comparer1, comparer2, comparer3);
		}

		/// <summary>Comparer that compares tuples with at least 1 item</summary>
		/// <typeparam name="T1">Type of the item</typeparam>
		public sealed class CompositeComparer<T1> : IComparer<ITuple>
		{

			public static readonly IComparer<ITuple> Default = new CompositeComparer<T1>();

			/// <summary>Constructor for a new tuple comparer</summary>
			public CompositeComparer()
				: this(0, null)
			{ }

			/// <summary>Constructor for a new tuple comparer</summary>
			public CompositeComparer(IComparer<T1> comparer)
				: this(0, comparer)
			{ }

			/// <summary>Constructor for a new tuple comparer</summary>
			/// <param name="offset">Offset in the tuples of the element to compare (can be negative)</param>
			/// <param name="comparer">Comparer for the element type</param>
			public CompositeComparer(int offset, IComparer<T1> comparer)
			{
				this.Offset = offset;
				this.Comparer = comparer ?? Comparer<T1>.Default;
			}

			/// <summary>Offset in the tuples where the comparison starts</summary>
			/// <remarks>If negative, comparison starts from the end.</remarks>
			public int Offset { get; }

			/// <summary>Comparer for the first element (at possition <see cref="Offset"/>)</summary>
			public IComparer<T1> Comparer { get; }

			/// <summary>Compare a single item in both tuples</summary>
			/// <param name="x">First tuple</param>
			/// <param name="y">Second tuple</param>
			/// <returns>Returns a positive value if x is greater than y, a negative value if x is less than y and 0 if x is equal to y.</returns>
			public int Compare(ITuple x, ITuple y)
			{
				if (y == null) return x == null ? 0 : +1;
				if (x == null) return -1;

				int nx = x.Count;
				int ny = y.Count;
				if (ny == 0 || nx == 0) return nx - ny;

				int p = this.Offset;
				return this.Comparer.Compare(x.Get<T1>(p), y.Get<T1>(p));
			}

		}

		/// <summary>Comparer that compares tuples with at least 2 items</summary>
		/// <typeparam name="T1">Type of the first item</typeparam>
		/// <typeparam name="T2">Type of the second item</typeparam>
		public sealed class CompositeComparer<T1, T2> : IComparer<ITuple>
		{

			public static readonly IComparer<ITuple> Default = new CompositeComparer<T1, T2>();

			/// <summary>Constructor for a new tuple comparer</summary>
			public CompositeComparer()
				: this(0, null, null)
			{ }

			/// <summary>Constructor for a new tuple comparer</summary>
			public CompositeComparer(IComparer<T1> comparer1, IComparer<T2> comparer2)
				: this(0, comparer1, comparer2)
			{ }

			/// <summary>Constructor for a new tuple comparer</summary>
			/// <param name="offset">Offset in the tuples of the first element to compare (can be negative)</param>
			/// <param name="comparer1">Comparer for the first element type</param>
			/// <param name="comparer2">Comparer for the second element type</param>
			public CompositeComparer(int offset, IComparer<T1> comparer1, IComparer<T2> comparer2)
			{
				this.Offset = offset;
				this.Comparer1 = comparer1 ?? Comparer<T1>.Default;
				this.Comparer2 = comparer2 ?? Comparer<T2>.Default;
			}

			/// <summary>Offset in the tuples where the comparison starts</summary>
			/// <remarks>If negative, comparison starts from the end.</remarks>
			public int Offset { get; }

			/// <summary>Comparer for the first element (at possition <see cref="Offset"/>)</summary>
			public IComparer<T1> Comparer1 { get; }

			/// <summary>Comparer for the second element (at possition <see cref="Offset"/> + 1)</summary>
			public IComparer<T2> Comparer2 { get; }

			/// <summary>Compare up to two items in both tuples</summary>
			/// <param name="x">First tuple</param>
			/// <param name="y">Second tuple</param>
			/// <returns>Returns a positive value if x is greater than y, a negative value if x is less than y and 0 if x is equal to y.</returns>
			public int Compare(ITuple x, ITuple y)
			{
				if (y == null) return x == null ? 0 : +1;
				if (x == null) return -1;

				int nx = x.Count;
				int ny = y.Count;
				if (ny == 0 || nx == 0) return nx - ny;

				int p = this.Offset;

				int c = this.Comparer1.Compare(x.Get<T1>(p), y.Get<T1>(p));
				if (c != 0) return c;

				if (ny == 1 || nx == 1) return nx - ny;
				c = this.Comparer2.Compare(x.Get<T2>(p + 1), y.Get<T2>(p + 1));

				return c;
			}

		}

		/// <summary>Comparer that compares tuples with at least 3 items</summary>
		/// <typeparam name="T1">Type of the first item</typeparam>
		/// <typeparam name="T2">Type of the second item</typeparam>
		/// <typeparam name="T3">Type of the thrid item</typeparam>
		public sealed class CompositeComparer<T1, T2, T3> : IComparer<ITuple>
		{

			public static readonly IComparer<ITuple> Default = new CompositeComparer<T1, T2, T3>();

			/// <summary>Constructor for a new tuple comparer</summary>
			public CompositeComparer()
				: this(0, null, null, null)
			{ }

			/// <summary>Constructor for a new tuple comparer</summary>
			public CompositeComparer(IComparer<T1> comparer1, IComparer<T2> comparer2, IComparer<T3> comparer3)
				: this(0, comparer1, comparer2, comparer3)
			{ }

			/// <summary>Constructor for a new tuple comparer</summary>
			/// <param name="offset">Offset in the tuples of the first element to compare (can be negative)</param>
			/// <param name="comparer1">Comparer for the first element type</param>
			/// <param name="comparer2">Comparer for the second element type</param>
			/// <param name="comparer3">Comparer for the thrid element type</param>
			public CompositeComparer(int offset, IComparer<T1> comparer1, IComparer<T2> comparer2, IComparer<T3> comparer3)
			{
				this.Offset = offset;
				this.Comparer1 = comparer1 ?? Comparer<T1>.Default;
				this.Comparer2 = comparer2 ?? Comparer<T2>.Default;
				this.Comparer3 = comparer3 ?? Comparer<T3>.Default;
			}

			/// <summary>Offset in the tuples where the comparison starts</summary>
			/// <remarks>If negative, comparison starts from the end.</remarks>
			public int Offset { get; }

			/// <summary>Comparer for the first element (at possition <see cref="Offset"/>)</summary>
			public IComparer<T1> Comparer1 { get; }

			/// <summary>Comparer for the second element (at possition <see cref="Offset"/> + 1)</summary>
			public IComparer<T2> Comparer2 { get; }

			/// <summary>Comparer for the third element (at possition <see cref="Offset"/> + 2)</summary>
			public IComparer<T3> Comparer3 { get; }

			/// <summary>Compare up to three items in both tuples</summary>
			/// <param name="x">First tuple</param>
			/// <param name="y">Second tuple</param>
			/// <returns>Returns a positive value if x is greater than y, a negative value if x is less than y and 0 if x is equal to y.</returns>
			public int Compare(ITuple x, ITuple y)
			{
				if (y == null) return x == null ? 0 : +1;
				if (x == null) return -1;

				int nx = x.Count;
				int ny = y.Count;
				if (ny == 0 || nx == 0) return nx - ny;

				int p = this.Offset;

				int c = this.Comparer1.Compare(x.Get<T1>(p), y.Get<T1>(p));
				if (c != 0) return c;

				if (ny == 1 || nx == 1) return nx - ny;
				c = this.Comparer2.Compare(x.Get<T2>(p + 1), y.Get<T2>(p + 1));
				if (c != 0) return c;

				if (ny == 2 || nx == 2) return nx - ny;
				c = this.Comparer3.Compare(x.Get<T3>(p + 2), y.Get<T3>(p + 2));

				return c;
			}

		}

	}



}
