#region Copyright Doxense SAS 2013-2019
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
	using Doxense.Diagnostics.Contracts;

	/// <summary>Observe the items of a sequence</summary>
	/// <typeparam name="TSource">Type of the observed elements</typeparam>
	public sealed class ObserverIterator<TSource> : FilterIterator<TSource, TSource>
	{

		private readonly Action<TSource> m_observer;

		public ObserverIterator(IEnumerable<TSource> source, Action<TSource> observer)
			: base(source)
		{
			Contract.NotNull(observer);
			m_observer = observer;
		}

		protected override Iterator<TSource> Clone()
		{
			return new ObserverIterator<TSource>(m_source, m_observer);
		}

		protected override bool OnNext()
		{
			var iterator = m_iterator;
			if (iterator == null) throw ThrowHelper.ObjectDisposedException(this);

			if (!iterator.MoveNext())
			{ // completed
				return Completed();
			}

			TSource current = iterator.Current;
			m_observer(current);

			return Publish(current);
		}
	}

}
