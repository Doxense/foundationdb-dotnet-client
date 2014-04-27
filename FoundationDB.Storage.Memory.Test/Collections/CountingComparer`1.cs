#region Copyright (c) 2013-2014, Doxense SAS. All rights reserved.
// See License.MD for license information
#endregion

namespace FoundationDB.Storage.Memory.Core.Test
{
	using System;
	using System.Collections.Generic;
	using System.Threading;

	/// <summary>Wrapper for an <see cref="IComparer{T}"/> that counts the number of calls to the <see cref="Compare()"/> method</summary>
	public class CountingComparer<T> : IComparer<T>
	{

		private int m_count;
		private IComparer<T> m_comparer;


		public CountingComparer()
			: this(Comparer<T>.Default)
		{ }

		public CountingComparer(IComparer<T> comparer)
		{
			m_comparer = comparer;
		}

		public int Count { get { return Volatile.Read(ref m_count); } }

		public void Reset()
		{
			Volatile.Write(ref m_count, 0);
		}

		public int Compare(T x, T y)
		{
			Interlocked.Increment(ref m_count);
			return m_comparer.Compare(x, y);
		}
	}

}
