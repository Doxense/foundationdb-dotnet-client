#region Copyright Doxense 2018-2022
//
// All rights are reserved. Reproduction or transmission in whole or in part, in
// any form or by any means, electronic, mechanical or otherwise, is prohibited
// without the prior written consent of the copyright owner.
//
#endregion

namespace Doxense.Messaging.Events
{
	using System;

	public interface IEventFilter
	{
		bool Accept(IEvent evt);
	}

	public static class EventFilters
	{
		public static readonly IEventFilter All = new AcceptAllEventFilter();

		internal sealed class AcceptAllEventFilter : IEventFilter
		{

			public bool Accept(IEvent evt) => true;

		}

		public static IEventFilter Create(Func<IEvent, bool> predicate) => new LambdaEventFilter(predicate);

		internal sealed class LambdaEventFilter : IEventFilter
		{

			public LambdaEventFilter(Func<IEvent, bool> predicate)
			{
				this.Predicate = predicate;
			}

			private Func<IEvent, bool> Predicate { get; }

			public bool Accept(IEvent evt) => this.Predicate(evt);

		}

	}

}
