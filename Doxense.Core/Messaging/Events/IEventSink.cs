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
	using System.Threading;
	using System.Threading.Tasks;

	public interface IEventSink
	{

		/// <summary>If true, this event sinks requires async dispatching. If false, it will always run inline and will not block the calling thread</summary>
		bool Async { get; }

		/// <summary>Dispatch un event vers ce log</summary>
		Task Dispatch(IEvent evt, CancellationToken ct);

		/// <summary>Dispatch un batch d'events vers ce log</summary>
		Task Dispatch(ReadOnlyMemory<IEvent> batch, CancellationToken ct);

	}

}
