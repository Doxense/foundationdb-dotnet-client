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
	using System.Linq;
	using System.Threading;
	using System.Threading.Tasks;
	using Doxense.Diagnostics.Contracts;
	using NodaTime;

	public class EventBus : IEventBus
	{

		public IEventSink[] Sinks { get; }

		public IClock Clock { get; }

		private IMessageDispatcher<IEvent> Dispatcher { get; }

		public EventBus(IEnumerable<IEventSink> sinks, IClock? clock)
		{
			this.Sinks = sinks.ToArray();
			this.Clock = clock ?? SystemClock.Instance;

			this.Dispatcher = this.Sinks.Length switch
			{
				0 => MessageDispatcher.Null<IEvent>(),
				1 => CreateDispatcher(this.Sinks[0]),
				_ => MessageDispatcher.Combine<IEvent>(this.Sinks.Select(sink => CreateDispatcher(sink)))
			};

			this.Dispatcher.Start();

			static IMessageDispatcher<IEvent> CreateDispatcher(IEventSink sink)
			{
				return sink.Async
					? MessageDispatcher.Buffered<IEvent, IEventSink>(
						sink,
						(batch, sink, ct) => sink.Dispatch(batch, ct)
					)
					: MessageDispatcher.Direct<IEvent, IEventSink>(
						sink,
						(one, sink) => sink.Dispatch(one, CancellationToken.None).GetAwaiter().GetResult(),
						(batch, sink) => sink.Dispatch(batch, CancellationToken.None).GetAwaiter().GetResult()
					);
			}
		}

		public Instant Now() => this.Clock.GetCurrentInstant();

		public static EventBus Create(IEventSink sink, IClock? clock = null) => new EventBus(new[] { sink }, clock);

		public static EventBus CreateInMemory(IEventFilter? filter = null, IClock? clock = null) => new EventBus(new[] { InMemoryEventSink.Create(filter) }, clock);

		public List<IEventSink> GetSinks()
		{
			return new List<IEventSink>(this.Sinks);
		}

		public void Dispatch(IEvent evt)
		{
			Contract.Debug.Requires(evt != null);
			this.Dispatcher.Dispatch(evt);
		}

		public void Dispatch(ReadOnlyMemory<IEvent> batch)
		{
			if (batch.Length != 0)
			{
				this.Dispatcher.Dispatch(batch);
			}
		}

		public Task DrainAsync(CancellationToken ct)
		{
			return this.Dispatcher.DrainAsync(false, ct);
		}

		public ValueTask DisposeAsync()
		{
			return this.Dispatcher.DisposeAsync();
		}

	}

}
