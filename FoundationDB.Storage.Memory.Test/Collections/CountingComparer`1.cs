using FoundationDB.Client;
using FoundationDB.Layers.Tuples;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace FoundationDB.Storage.Memory.Core.Test
{

	/// <summary>Wrapper for an IComparer&lt;&gt; that counts the number of calls to the Compare() method</summary>
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
