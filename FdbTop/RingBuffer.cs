using FoundationDB.Client;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace FdbTop
{
	public class RingBuffer<T> : IReadOnlyCollection<T>
	{
		private readonly Queue<T> m_store;
		private int m_size;

		public RingBuffer(int capacity)
		{
			if (capacity < 0) throw new ArgumentOutOfRangeException("capacity");

			m_store = new Queue<T>(capacity);
			m_size = capacity;
		}

		public int Count
		{
			get { return m_store.Count; }
		}

		public int Capacity
		{
			get { return m_size; }
		}

		public void Clear()
		{
			m_store.Clear();
		}

		public void Resize(int newCapacity)
		{
			if (newCapacity < 0) throw new ArgumentOutOfRangeException("newCapacity");

			var store = m_store;
			if (newCapacity < store.Count)
			{
				while (store.Count > newCapacity) store.Dequeue();
				store.TrimExcess();
			}
			m_size = newCapacity;
		}

		public void Enqueue(T item)
		{
			var store = m_store;
			if (store.Count >= m_size) store.Dequeue();
			store.Enqueue(item);
		}

		public T Dequeue()
		{
			return m_store.Dequeue();
		}

		public bool TryDequeue(out T item)
		{
			var store = m_store;
			if (store.Count == 0)
			{
				item = default(T);
				return false;
			}
			item = store.Dequeue();
			return true;
		}

		public T[] ToArray()
		{
			return m_store.ToArray();
		}

		public void ForEach(Action<T> handler)
		{
			foreach (var item in m_store)
			{
				handler(item);
			}
		}

		public TResult Aggregate<TResult>(TResult seed, Func<T, TResult, TResult> aggregate)
		{
			TResult value = seed;
			foreach (var item in m_store)
			{
				value = aggregate(item, value);
			}
			return value;
		}

		public TResult Aggregate<TState, TResult>(Func<TState> init, Func<T, TState, TState> aggregate, Func<TState, TResult> finish)
		{
			TState state = init();
			foreach (var item in m_store)
			{
				state = aggregate(item, state);
			}
			return finish(state);
		}

		public Queue<T>.Enumerator GetEnumerator()
		{
			return m_store.GetEnumerator();
		}

		IEnumerator<T> IEnumerable<T>.GetEnumerator()
		{
			return this.GetEnumerator();
		}

		System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
		{
			return this.GetEnumerator();
		}

	}

}
