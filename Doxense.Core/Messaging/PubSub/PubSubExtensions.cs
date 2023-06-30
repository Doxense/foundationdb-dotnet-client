#region Copyright (c) 2005-2023 Doxense SAS
// All rights reserved.
// 
// Redistribution and use in source and binary forms, with or without
// modification, are permitted provided that the following conditions are met:
// 	* Redistributions of source code must retain the above copyright
// 	  notice, this list of conditions and the following disclaimer.
// 	* Redistributions in binary form must reproduce the above copyright
// 	  notice, this list of conditions and the following disclaimer in the
// 	  documentation and/or other materials provided with the distribution.
// 	* Neither the name of Doxense nor the
// 	  names of its contributors may be used to endorse or promote products
// 	  derived from this software without specific prior written permission.
// 
// THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND
// ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
// WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
// DISCLAIMED. IN NO EVENT SHALL DOXENSE BE LIABLE FOR ANY
// DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
// (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
// LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
// ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
// (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
// SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
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
