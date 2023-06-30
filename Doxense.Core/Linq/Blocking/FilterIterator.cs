#region Copyright (c) 2005-2023 Doxense SAS
//
// All rights are reserved. Reproduction or transmission in whole or in part, in
// any form or by any means, electronic, mechanical or otherwise, is prohibited
// without the prior written consent of the copyright owner.
//
#endregion

namespace Doxense.Linq.Iterators
{
	using System;
	using System.Collections.Generic;
	using System.Threading;
	using Doxense.Diagnostics.Contracts;

	public abstract class FilterIterator<TSource, TResult> : Iterator<TResult>
	{
		/// <summary>Source sequence (when in iterable mode)</summary>
		protected IEnumerable<TSource> m_source;

		/// <summary>Active iterator on the source (when in iterator mode)</summary>
		protected IEnumerator<TSource>? m_iterator;
		protected bool m_innerHasCompleted;

		protected FilterIterator(IEnumerable<TSource> source)
		{
			Contract.Debug.Requires(source != null);
			m_source = source;
		}

		/// <summary>Start the inner iterator</summary>
		protected virtual IEnumerator<TSource> StartInner()
		{
			// filtering changes the number of items, so that means that, even if the underlying caller wants one item, we may need to read more.
			// => change all "Head" requests into "Iterator" to prevent any wrong optimizations by the underlying source (ex: using a too small batch size)
			return m_source.GetEnumerator();
		}

		protected void MarkInnerAsCompleted()
		{
			m_innerHasCompleted = true;

			// we don't need the iterator, so we can dispose of it immediately
			Interlocked.Exchange(ref m_iterator, null)?.Dispose();
		}

		protected override bool OnFirst()
		{
			// on the first call to MoveNext, we have to hook up with the source iterator

			IEnumerator<TSource>? iterator = null;
			try
			{
				iterator = StartInner();
				if (iterator == null) return false;
				OnStarted(iterator);
				return true;
			}
			catch (Exception)
			{
				// whatever happens, make sure that we released the iterator...
				if (iterator != null)
				{
					iterator.Dispose();
					iterator = null;
				}
				throw;
			}
			finally
			{
				m_iterator = iterator;
			}
		}

		protected virtual void OnStarted(IEnumerator<TSource> iterator)
		{
			//override this to add custom starting logic once we know that the inner iterator is ready
		}

		protected virtual void OnStopped()
		{
			// override this to add custom stopping logic once the iterator has completed (for whatever reason)
		}

		protected override void Cleanup()
		{
			try
			{
				OnStopped();
			}
			finally
			{
				MarkInnerAsCompleted();
			}
		}

	}

}
