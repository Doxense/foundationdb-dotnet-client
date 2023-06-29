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
	using NodaTime;

	public interface IEventBus : IAsyncDisposable
	{
		List<IEventSink> GetSinks();

		void Dispatch(IEvent evt);

		void Dispatch(ReadOnlyMemory<IEvent> batch);

		Instant Now();
	}

}
