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
	using System.Threading;
	using System.Threading.Tasks;

	/// <summary>Ignore received messages</summary>
	public sealed class NullEventSink : IEventSink
	{

		public static NullEventSink Create() => new NullEventSink();

		public bool Async => false;

		public Task Dispatch(IEvent evt, CancellationToken ct)
		{
			// NOP
			return Task.CompletedTask;
		}

		public Task Dispatch(ReadOnlyMemory<IEvent> batch, CancellationToken ct)
		{
			// NOP
			return Task.CompletedTask;
		}

	}
}
