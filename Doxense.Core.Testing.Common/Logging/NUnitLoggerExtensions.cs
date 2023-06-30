#region Copyright (c) 2005-2023 Doxense SAS
//
// All rights are reserved. Reproduction or transmission in whole or in part, in
// any form or by any means, electronic, mechanical or otherwise, is prohibited
// without the prior written consent of the copyright owner.
//
#endregion

namespace Doxense
{
	using System;
	using Microsoft.Extensions.DependencyInjection;
	using Microsoft.Extensions.DependencyInjection.Extensions;
	using Microsoft.Extensions.Logging;
	using Microsoft.Extensions.Logging.Configuration;

	public static class NUnitLoggerExtensions
	{

		public static ILoggingBuilder AddNUnitLogging(this ILoggingBuilder builder, Action<NUnitLoggerOptions>? configure = null)
		{
			builder.AddConfiguration();
			builder.Services.TryAddSingleton<NUnitLoggerProvider>();
			builder.Services.TryAddEnumerable(ServiceDescriptor.Singleton<ILoggerProvider, NUnitLoggerProvider>((sp) => sp.GetRequiredService<NUnitLoggerProvider>()));
			LoggerProviderOptions.RegisterProviderOptions<NUnitLoggerOptions, NUnitLoggerProvider>(builder.Services);
			if (configure != null)
			{
				builder.Services.Configure(configure);
			}
			return builder;
		}

	}
}
