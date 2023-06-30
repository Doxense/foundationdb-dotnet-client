#region Copyright (c) 2005-2023 Doxense SAS
//
// All rights are reserved. Reproduction or transmission in whole or in part, in
// any form or by any means, electronic, mechanical or otherwise, is prohibited
// without the prior written consent of the copyright owner.
//
#endregion

namespace Doxense.Networking.Http
{
	using System;
	using System.Net.Http;
	using Doxense.Diagnostics.Contracts;
	using Microsoft.Extensions.Logging;
	using Microsoft.Extensions.Logging.Abstractions;
	using Microsoft.Extensions.Options;

	public class DefaultBetterHttpClientFactory : IBetterHttpClientFactory
	{

		private INetworkMap? Map { get; }

		private BetterHttpClientOptionsBuilder Builder { get; }

		private ILogger<BetterHttpClient> Logger { get; }

		private NodaTime.IClock Clock { get; }

		public DefaultBetterHttpClientFactory(INetworkMap? map, IOptions<BetterHttpClientOptionsBuilder> optionsBuilder, ILogger<BetterHttpClient>? logger, NodaTime.IClock? clock)
		{
			this.Map = map;
			this.Builder = optionsBuilder.Value;
			this.Logger = logger ?? NullLogger<BetterHttpClient>.Instance;
			this.Clock = clock ?? NodaTime.SystemClock.Instance;
		}

		public BetterHttpClient CreateClient(Uri hostAddress, BetterHttpClientOptions options, HttpMessageHandler? handler = null)
		{
			Contract.NotNull(hostAddress);
			Contract.NotNull(options);

			options.Filters.AddRange(this.Builder.GlobalFilters);
			this.Builder.Configure?.Invoke(options);

			if (handler == null)
			{
				if (this.Map == null) throw new InvalidOperationException($"You must register an implementation for {nameof(INetworkMap)} during startup, in order to use this method.");
				handler = this.Map.CreateBetterHttpHandler(hostAddress, options);
				Contract.Debug.Assert(handler != null);
			}

			return new BetterHttpClient(
				hostAddress,
				options,
				handler,
				this.Logger,
				this.Clock
			);
		}

	}

}
