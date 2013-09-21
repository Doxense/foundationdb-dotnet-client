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

namespace FoundationDB.Layers.Tuples
{
	using FoundationDB.Client.Converters;
	using System;
	using System.Collections;
	using System.Collections.Generic;
	using System.Runtime.CompilerServices;

	/// <summary>Helper class for tuple comparisons</summary>
	public static class FdbTupleComparisons
	{
		/// <summary>Tuple comparer that treats similar values as equal ("123" = 123 = 123L = 123.0d)</summary>
		public static readonly EqualityComparer Default = new EqualityComparer(SimilarValueComparer.Default);

		/// <summary>Tuple comparer that uses the default BCL object comparison ("123" != 123 != 123L != 123.0d)</summary>
		public static readonly EqualityComparer Bcl = new EqualityComparer(EqualityComparer<object>.Default);

		/// <summary>Tuple comparer that compared the packed bytes (slow!)</summary>
		public static readonly BinaryComparer Binary = new BinaryComparer();

		public sealed class EqualityComparer : IEqualityComparer<IFdbTuple>, IEqualityComparer
		{
			private readonly IEqualityComparer m_comparer;

			internal EqualityComparer(IEqualityComparer comparer)
			{
				m_comparer = comparer;
			}

			public bool Equals(IFdbTuple x, IFdbTuple y)
			{
				if (object.ReferenceEquals(x, y)) return true;
				if (object.ReferenceEquals(x, null) || object.ReferenceEquals(y, null)) return false;

				return x.Equals(y, m_comparer);
			}

			public int GetHashCode(IFdbTuple obj)
			{
				return obj != null ? obj.GetHashCode(m_comparer) : 0;
			}

			public new bool Equals(object x, object y)
			{
				if (object.ReferenceEquals(x, y)) return true;
				if (x == null || y == null) return false;

				var t = x as IFdbTuple;
				if (t != null) return t.Equals(y, m_comparer);

				t = y as IFdbTuple;
				if (t != null) t.Equals(x, m_comparer);

				return false;
			}

			public int GetHashCode(object obj)
			{
				if (obj == null) return 0;

				var t = obj as IFdbTuple;
				if (!object.ReferenceEquals(t, null)) return t.GetHashCode(m_comparer);

				// returns a hash base on the pointers
				return RuntimeHelpers.GetHashCode(obj);
			}
		}
	
		public sealed class BinaryComparer : IEqualityComparer<IFdbTuple>, IEqualityComparer
		{
			internal BinaryComparer()
			{ }


			public bool Equals(IFdbTuple x, IFdbTuple y)
			{
				if (object.ReferenceEquals(x, y)) return true;
				if (object.ReferenceEquals(x, null) || object.ReferenceEquals(y, null)) return false;

				return x.ToSlice().Equals(y.ToSlice());
			}

			public int GetHashCode(IFdbTuple obj)
			{
				return object.ReferenceEquals(obj, null) ? 0 : obj.ToSlice().GetHashCode();
			}

			public new bool Equals(object x, object y)
			{
				if (object.ReferenceEquals(x, y)) return true;
				if (x == null || y == null) return false;

				var tx = x as IFdbTuple;
				var ty = y as IFdbTuple;
				if (object.ReferenceEquals(tx, null) || object.ReferenceEquals(ty, null)) return false;
				return tx.ToSlice().Equals(ty.ToSlice());
			}

			public int GetHashCode(object obj)
			{
				if (obj == null) return 0;

				var tuple = obj as IFdbTuple;
				if (!object.ReferenceEquals(tuple, null)) return tuple.ToSlice().GetHashCode();

				return RuntimeHelpers.GetHashCode(obj);
			}
		}

	}



}
