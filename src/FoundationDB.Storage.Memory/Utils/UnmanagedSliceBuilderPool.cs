#region Copyright (c) 2013-2014, Doxense SAS. All rights reserved.
// See License.MD for license information
#endregion

namespace FoundationDB.Storage.Memory.Utils
{
	using System;
	using System.Collections.Generic;
	using System.Diagnostics;
	using System.Diagnostics.Contracts;
	using System.Threading;

	[DebuggerDisplay("Count={m_buckets.Count}, Used={m_memoryUsed}, Loaned={m_memoryLoaned}")]
	public class UnmanagedSliceBuilderPool : IDisposable
	{
		public readonly Stack<UnmanagedSliceBuilder> m_buckets;
		private uint m_initialCapacity;
		private int m_maxCount;
		private long m_memoryUsed;
		private long m_memoryLoaned;
		private bool m_disposed;

		public UnmanagedSliceBuilderPool(uint initialCapacity, int maxCount)
		{
			m_initialCapacity = UnmanagedHelpers.NextPowerOfTwo(Math.Min(initialCapacity, 64));
			m_maxCount = Math.Max(1, maxCount);
			m_buckets = new Stack<UnmanagedSliceBuilder>(Math.Max(m_maxCount, 100));
		}

		/// <summary>Subscription to a scratch buffer from the pool. DO NOT COPY BY VALUE!</summary>
		/// <remarks>Copying this struct by value will break the pool. Only use it as a local variable in a method or in a class !</remarks>
		public struct Subscription : IDisposable
		{
			private readonly UnmanagedSliceBuilderPool m_pool;
			private UnmanagedSliceBuilder m_builder;

			internal Subscription(UnmanagedSliceBuilderPool pool, UnmanagedSliceBuilder builder)
			{
				Contract.Requires(pool != null && builder != null);
				m_pool = pool;
				m_builder = builder;
			}

			public UnmanagedSliceBuilder Builder
			{
				get
				{
					Contract.Assert(m_builder != null, "Builder already returned to the pool");
					return m_builder;
				}
			}

			public bool Allocated
			{
				get { return m_builder != null; }
			}

			public void Dispose()
			{
#pragma warning disable 420
				var builder = Interlocked.Exchange(ref m_builder, null);
#pragma warning restore 420
				if (builder != null && builder.Buffer != null)
				{
					m_pool.Return(builder);
				}
			}
		}

		/// <summary>Borrow a builder from this pool</summary>
		/// <returns>Builder subscription that should be disposed as soon as the buffer is not needed anymore</returns>
		/// <remarks>ALWAYS wrap the subscription in a using(...) statement! Do NOT pass the subscription by value, always pass the Builder by reference ! Do NOT keep a reference on the Builder or reuse it after it has been disposed! Do NOT return or store slices that point to this buffer! </remarks>
		public Subscription Use()
		{
			UnmanagedSliceBuilder builder = null;
			lock (m_buckets)
			{
				if (m_disposed) ThrowDisposed();

				while(m_buckets.Count > 0)
				{
					builder = m_buckets.Pop();
					if (builder != null && builder.Buffer != null)
					{
						Interlocked.Add(ref m_memoryUsed, -((long)builder.Capacity));
						Contract.Assert(m_memoryUsed >= 0, "m_memoryUsed desync");
						break;
					}
					builder = null;
				}
			}
			if (builder == null)
			{
				builder = new UnmanagedSliceBuilder(m_initialCapacity);
			}
			Interlocked.Add(ref m_memoryLoaned, builder.Capacity);
			Contract.Assert(builder != null && builder.Buffer != null);
			return new Subscription(this, builder);
		}

		/// <summary>Return a builder into the pool</summary>
		/// <param name="builder">Builder that is no longer in use</param>
		internal void Return(UnmanagedSliceBuilder builder)
		{
			if (m_disposed || builder == null) return;

			lock (m_buckets)
			{
				if (m_disposed) return;

				var size = builder.Capacity;
				Contract.Assert(size == UnmanagedHelpers.NextPowerOfTwo(size), "builder size should always be a power of two");

				Interlocked.Add(ref m_memoryLoaned, -((long)builder.Capacity));
				Contract.Assert(m_memoryUsed >= 0, "m_memoryLoaned desync");

				if (m_buckets.Count < m_maxCount)
				{
					m_buckets.Push(builder);
					Interlocked.Add(ref m_memoryUsed, builder.Capacity);
				}
				else
				{
					builder.Dispose();
				}
			}
		}

		private static void ThrowDisposed()
		{
			throw new InvalidOperationException("The buffer pool as already been disposed");
		}

		public void Dispose()
		{
			Dispose(true);
			GC.SuppressFinalize(this);
		}

		protected virtual void Dispose(bool disposing)
		{
			if (!m_disposed)
			{
				m_disposed = true;
				if (disposing)
				{
					lock (m_buckets)
					{
						foreach(var builder in m_buckets)
						{
							if (builder != null) builder.Dispose();
						}
						m_buckets.Clear();
					}
				}
			}
		}
	}

}
