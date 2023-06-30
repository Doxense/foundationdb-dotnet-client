#region Copyright (c) 2005-2023 Doxense SAS
//
// All rights are reserved. Reproduction or transmission in whole or in part, in
// any form or by any means, electronic, mechanical or otherwise, is prohibited
// without the prior written consent of the copyright owner.
//
#endregion

namespace Doxense.Messaging.Events
{
	using System;
	using System.Collections.Generic;
	using System.Threading;
	using System.Threading.Tasks;
	using Doxense.Diagnostics.Contracts;

	/// <summary>Implementation of a Job Event Log that keeps all events in memory, mostly intended for unit testing</summary>
	public sealed class InMemoryEventSink : IEventSink
	{

		public InMemoryEventSink(IEventFilter filter)
			: this(filter, null)
		{
			this.Filter = filter;
		}

		public InMemoryEventSink(IEventFilter filter, IEnumerable<IEvent>? initialEvents)
		{
			Contract.NotNull(filter);
			this.Filter = filter;
			if (initialEvents != null)
			{
				this.Events.AddRange(initialEvents);
			}
		}

		public static InMemoryEventSink Create(IEventFilter? filter = null) => new InMemoryEventSink(filter ?? EventFilters.All);

		private IEventFilter Filter { get; }

		private List<IEvent> Events { get; } = new ();

		public bool Async => false;
		//note: techniquement c'est un mensonge car on lock() mais c'est pas très grave!

		public Task Dispatch(IEvent evt, CancellationToken ct)
		{
			lock(this.Events)
			{
				this.Events.Add(evt);
			}
			return Task.CompletedTask;
		}

		public Task Dispatch(ReadOnlyMemory<IEvent> batch, CancellationToken ct)
		{
			lock(this.Events)
			{
				foreach(var evt in batch.Span)
				{
					this.Events.Add(evt);
				}
			}
			return Task.CompletedTask;
		}

		public IReadOnlyList<IEvent> GetAllEvents()
		{
			lock(this.Events)
			{
				//note: on retourne une copie!
				return new List<IEvent>(this.Events);
			}
		}

		public InMemoryEventSink Copy()
		{
			lock (this.Events)
			{
				return new InMemoryEventSink(this.Filter, this.Events);
			}
		}

	}

}
