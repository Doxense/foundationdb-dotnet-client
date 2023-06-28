#region Copyright (c) 2005-2023 Doxense SAS
// See License.MD for license information
#endregion

namespace Doxense.Messaging.PubSub
{
	using System;
	using System.Diagnostics;
	using System.Threading;
	using System.Threading.Channels;
	using System.Threading.Tasks;
	using Doxense.Diagnostics.Contracts;

	public static class PubSubExtensions
	{

		[DebuggerDisplay("Channel={Channel}")]
		private sealed class ChannelSubscription : IAsyncDisposable
		{

			public ChannelSubscription(string channel, ChannelWriter<string> writer, bool autoCompleteOnClose)
			{
				this.Channel = channel;
				this.Writer = writer;
				this.AutoCompleteOnClose = autoCompleteOnClose;
			}

			public string Channel { get; }

			public ChannelWriter<string> Writer { get; }

			public IAsyncDisposable? InnerSubscription { get; set; }

			public bool AutoCompleteOnClose { get;}

			public ValueTask Callback(string channelId, string message, CancellationToken ct)
			{
				Contract.Debug.Requires(channelId == this.Channel);
				return this.Writer.WriteAsync(message, ct);
			}

			public ValueTask DisposeAsync()
			{
				if (this.AutoCompleteOnClose)
				{
					this.Writer.TryComplete();
				}
				return this.InnerSubscription?.DisposeAsync() ?? default;
			}
		}

		public static async Task<IAsyncDisposable> SubscribeAsync(this IPubSub pubsub, string channel, ChannelWriter<string> writer, bool autoCompleteOnClose, CancellationToken ct)
		{
			Contract.NotNull(channel);
			Contract.NotNull(writer);

			var sub = new ChannelSubscription(channel, writer, autoCompleteOnClose);

			sub.InnerSubscription = await pubsub.SubscribeAsync(
				channel,
				sub.Callback,
				ct
			);

			return sub;
		}

	}

}
