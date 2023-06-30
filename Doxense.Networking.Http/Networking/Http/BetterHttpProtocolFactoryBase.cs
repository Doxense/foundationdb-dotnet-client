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
	using Microsoft.Extensions.DependencyInjection;
	using Microsoft.Extensions.Options;

	public abstract class BetterHttpProtocolFactoryBase<TProtocol, TOptions> : IBetterHttpProtocolFactory<TProtocol, TOptions>
		where TProtocol : IBetterHttpProtocol
		where TOptions : BetterHttpClientOptions
	{

		public IServiceProvider Services { get; }

		protected BetterHttpProtocolFactoryBase(IServiceProvider services)
		{
			this.Services = services;
		}

		protected abstract TOptions CreateOptions();

		protected virtual void OnAfterConfigure(TOptions options)
		{
			//NOP
		}

		//REVIEW: rename to CreateProtocol() ?
		public TProtocol CreateClient(Uri baseAddress, Action<TOptions>? configure = null)
		{
			return CreateClientCore(baseAddress, null, configure);
		}

		//REVIEW: rename to CreateProtocol() ?
		public TProtocol CreateClient(Uri baseAddress, HttpMessageHandler handler, Action<TOptions>? configure = null)
		{
			return CreateClientCore(baseAddress, handler, configure);
		}

		//REVIEW: rename to CreateProtocolCore() ?
		private TProtocol CreateClientCore(Uri baseAddress, HttpMessageHandler? handler, Action<TOptions>? configure = null)
		{
			var options = CreateOptions();

			//BUGBUG: REVIEW: on configure nos options ici, mais le factory.CreateClient(...) va aussi appliquer les options par défaut, mais derrière notre dos! :(

			var localConfigure = this.Services.GetService<IConfigureOptions<TOptions>>();
			localConfigure?.Configure(options);
			//REVIEW: je pense qu'on peut bouger celui la dans le ClientHttpFactory

			configure?.Invoke(options);
			//REVIEW: par contre celui la devrait probablement etre passé en arg au CreateClient comme "post-processing"

			OnAfterConfigure(options);

			var factory = this.Services.GetRequiredService<IBetterHttpClientFactory>();
			var client = factory.CreateClient(baseAddress, options, handler);
			Contract.Debug.Assert(client != null && client.HostAddress != null && client.Options != null);

			try
			{
				return ActivatorUtilities.CreateInstance<TProtocol>(this.Services, client);
			}
			catch (Exception e)
			{
#if DEBUG
				if (System.Diagnostics.Debugger.IsAttached) System.Diagnostics.Debugger.Break();
#endif
				throw;
			}
		}


	}

}
