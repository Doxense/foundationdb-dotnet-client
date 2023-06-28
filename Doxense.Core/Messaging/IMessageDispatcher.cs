#region Copyright Doxense 2018-2022
//
// All rights are reserved. Reproduction or transmission in whole or in part, in
// any form or by any means, electronic, mechanical or otherwise, is prohibited
// without the prior written consent of the copyright owner.
//
#endregion

namespace Doxense.Messaging
{
	using System;
	using System.Threading;
	using System.Threading.Tasks;

	public interface IMessageDispatcher<TMessage> : IAsyncDisposable
		where TMessage : class
	{

		void Start();

		void Dispatch(TMessage message);

		void Dispatch(ReadOnlyMemory<TMessage> batch);

		Task DrainAsync(bool final, CancellationToken ct);

		void Complete();

	}

}
