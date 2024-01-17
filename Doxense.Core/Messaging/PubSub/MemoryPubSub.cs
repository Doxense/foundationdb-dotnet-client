
namespace Doxense.Messaging.PubSub
{
	using System;
	using System.Collections.Concurrent;
	using System.Threading;
	using System.Threading.Tasks;
	using Microsoft.Extensions.DependencyInjection;

	public class MemoryPubSub : IPubSub
	{

		private sealed class Subscription : IAsyncDisposable
		{

			public MemoryPubSub Parent { get; }

			public Guid Id { get; }

			public string Channel { get; }

			public Func<string, string, CancellationToken, ValueTask> Handler { get; }

			public Subscription(MemoryPubSub parent, Guid id, string channel, Func<string, string, CancellationToken, ValueTask> handler)
			{
				this.Parent = parent;
				this.Id = id;
				this.Channel = channel;
				this.Handler = handler;
			}

			public ValueTask DisposeAsync()
			{
				this.Parent.CancelSubscription(this);
				return default;
			}

		}

		public Task<IAsyncDisposable> SubscribeAsync(string channel, Func<string, string, CancellationToken, ValueTask> onMessageReceived, CancellationToken ct)
		{
			if (this.disposed) throw new ObjectDisposedException(this.GetType().Name);
			var subscription = new Subscription(this, Guid.NewGuid(), channel, onMessageReceived);
			this.SubscribersByChannel.TryAdd(subscription.Id, subscription);
			return Task.FromResult<IAsyncDisposable>(subscription);
		}

		private void CancelSubscription(Subscription subscription)
		{
			this.SubscribersByChannel.TryRemove(subscription.Id, out _);
		}

		public async Task PublishAsync(string channel, string message, CancellationToken ct)
		{
			if (this.disposed) throw new ObjectDisposedException(this.GetType().Name);
			foreach (var subscribersByChannel in this.SubscribersByChannel.Values)
			{
				if (string.Equals(subscribersByChannel.Channel, channel, StringComparison.OrdinalIgnoreCase))
				{
					// Execute method for everyone subscribed on this channel:
					await subscribersByChannel.Handler(channel, message, ct).ConfigureAwait(false);
				}
			}
		}

		private ConcurrentDictionary<Guid, Subscription> SubscribersByChannel { get; set; } = new ConcurrentDictionary<Guid, Subscription>();
		
		private bool disposed;
		public void Dispose()
		{
			Dispose(true);
			GC.SuppressFinalize(this);
		}

		protected virtual void Dispose(bool disposing)
		{
			if (disposed) return;
			disposed = true;
			if (disposing)
			{
				this.SubscribersByChannel.Clear();
			}
		}

	}

	public static class MemoryPubSubExtension
	{
		public static IServiceCollection AddMemoryPubSub(this IServiceCollection services, Action<MemoryPubSubOptions>? configure = null)
		{
			services.AddSingleton<IPubSub, MemoryPubSub>();
			services.Configure<MemoryPubSubOptions>(option => configure?.Invoke(option));
			return services;
		}
	}

	public class MemoryPubSubOptions
	{
		//TODO?
	}

}
