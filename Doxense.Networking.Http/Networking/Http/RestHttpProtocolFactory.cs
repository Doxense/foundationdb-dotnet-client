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
	using Microsoft.Extensions.DependencyInjection;

	public class RestHttpProtocolFactory : BetterHttpProtocolFactoryBase<RestHttpProtocol, RestHttpClientOptions>
	{
		public RestHttpProtocolFactory(IServiceProvider services) : base(services)
		{ }

		protected override RestHttpClientOptions CreateOptions()
		{
			return new RestHttpClientOptions();
		}
	}

	public static class RestHttpProtocolFactoryExtensions
	{

		public static IServiceCollection AddRestHttpProtocol(this IServiceCollection services, Action<RestHttpClientOptions>? configure = null)
		{
			services.AddSingleton<RestHttpProtocolFactory>();
			services.Configure<RestHttpClientOptions>(configure ?? ((_) => { }));
			return services;
		}

	}

}
