#region Copyright (c) 2005-2023 Doxense SAS
// See License.MD for license information
#endregion

namespace Doxense.Messaging.PubSub
{
	using System;
	using System.Threading;
	using System.Threading.Tasks;

	public interface IPubSub : IDisposable
	{

		/// <summary>Subscribe to a channel</summary>
		Task<IAsyncDisposable> SubscribeAsync(string channel, Func<string, string, CancellationToken, ValueTask> onMessageReceived, CancellationToken ct);

		/// <summary>Publish a message to a channel</summary>
		Task PublishAsync(string channel, string message, CancellationToken ct);

	}
}
