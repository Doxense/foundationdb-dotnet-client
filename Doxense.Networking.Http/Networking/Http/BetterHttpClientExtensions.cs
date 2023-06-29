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
	using System.Collections.Generic;
	using System.Linq;
	using Microsoft.Extensions.DependencyInjection;
	using Microsoft.Extensions.DependencyInjection.Extensions;

	public record BetterHttpClientOptionsBuilder
	{

		public Action<BetterHttpClientOptions>? Configure { get; set; }

		public List<IBetterHttpFilter> GlobalFilters { get; set; } = new List<IBetterHttpFilter>();

	}

	public static class BetterHttpClientExtensions
	{

		/// <summary>Add support for <see cref="IBetterHttpClientFactory"/> and configure the global HTTP options</summary>
		public static IServiceCollection AddBetterHttpClient(this IServiceCollection services, Action<BetterHttpClientOptions>? configure = null)
		{
			services.TryAddSingleton<IBetterHttpClientFactory, DefaultBetterHttpClientFactory>();
			services
				.AddOptions<BetterHttpClientOptionsBuilder>()
				.Configure(options =>
				{
					if (configure != null) options.Configure += configure;
				});
			return services;
		}

		/// <summary>Add a global <see cref="IBetterHttpFilter">HTTP filter</see> to all clients used by this process</summary>
		public static IServiceCollection AddGlobalHttpFilter<TFilter>(this IServiceCollection services, Action<TFilter>? configure = null)
			where TFilter: class, IBetterHttpFilter
		{
#if DEBUG
			if (services.Any(x => x.ServiceType == typeof(TFilter))) throw new InvalidOperationException($"Global HTTP filter '{typeof(TFilter).Name}' has already been registered!");
#endif

			services.TryAddSingleton<TFilter>();
			services
				.AddOptions<BetterHttpClientOptionsBuilder>()
				.Configure<IServiceProvider>((options, sp) =>
				{
					var filter = sp.GetRequiredService<TFilter>();
					configure?.Invoke(filter);
					options.GlobalFilters.Add(filter);
				});
			return services;
		}

		/// <summary>Add support for a specific <see cref="IBetterHttpProtocol">HTTP protocol handler</see></summary>
		/// <typeparam name="TFactory">Type of the protocol handler factory</typeparam>
		/// <typeparam name="TProtocol">Type of the protocol handler</typeparam>
		/// <typeparam name="TOptions">Type of the options supported by the protocol handler</typeparam>
		/// <remarks>This should be called by implementors of protocols, via a dedicated extension method.</remarks>
		public static IServiceCollection AddBetterHttpProtocol<TFactory, TProtocol, TOptions>(this IServiceCollection services, Action<TOptions>? configure = null)
			where TFactory : class, IBetterHttpProtocolFactory<TProtocol, TOptions>
			where TProtocol : IBetterHttpProtocol
			where TOptions : BetterHttpClientOptions
		{
			services.TryAddSingleton<TFactory>();
			services.Configure<TOptions>(configure ?? (_ => { }));
			return services;
		}

		public static TProtocol CreateClient<TProtocol, TOptions>(this IBetterHttpProtocolFactory<TProtocol, TOptions> factory, string baseAddress, Action<TOptions>? configure = null)
			where TProtocol : IBetterHttpProtocol
			where TOptions : BetterHttpClientOptions
		{
			return factory.CreateClient(new Uri(baseAddress, UriKind.Absolute), configure);
		}

	}

}
