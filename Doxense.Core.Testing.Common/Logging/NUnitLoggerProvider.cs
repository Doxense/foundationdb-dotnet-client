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
	using System.Collections.Concurrent;
	using Microsoft.Extensions.Logging;
	using Microsoft.Extensions.Options;

	[ProviderAlias("NUnit")]
	public sealed class NUnitLoggerProvider : ILoggerProvider, ISupportExternalScope
	{

		private IOptionsMonitor<NUnitLoggerOptions> Options { get; }

		private ConcurrentDictionary<string, NUnitLogger> Loggers { get; }= new ConcurrentDictionary<string, NUnitLogger>();

		private IExternalScopeProvider ScopeProvider = NullExternalScopeProvider.Instance;

		public NUnitLoggerProvider(IOptionsMonitor<NUnitLoggerOptions> options)
		{
			this.Options = options;
		}

		public void Dispose() { }

		public void SetLogLevel(LogLevel level)
		{
			this.Options.CurrentValue.LogLevel = level;
		}

		public ILogger CreateLogger(string categoryName)
		{
			return this.Loggers.GetOrAdd(categoryName, loggerName => new NUnitLogger(categoryName, this.Options.CurrentValue, this.ScopeProvider));
		}

		public void SetScopeProvider(IExternalScopeProvider scopeProvider)
		{
			this.ScopeProvider = scopeProvider;

			foreach (var logger in this.Loggers)
			{
				logger.Value.ScopeProvider = scopeProvider;
			}
		}
	}

	/// <summary>
	/// An empty scope without any logic
	/// </summary>
	internal class NullScope : IDisposable
	{
		public static NullScope Instance { get; } = new NullScope();

		private NullScope()
		{
		}

		/// <inheritdoc />
		public void Dispose()
		{
		}
	}

	/// <summary>
	/// Scope provider that does nothing.
	/// </summary>
	internal class NullExternalScopeProvider : IExternalScopeProvider
	{
		private NullExternalScopeProvider()
		{
		}

		/// <summary>
		/// Returns a cached instance of <see cref="NullExternalScopeProvider"/>.
		/// </summary>
		public static IExternalScopeProvider Instance { get; } = new NullExternalScopeProvider();

		/// <inheritdoc />
		void IExternalScopeProvider.ForEachScope<TState>(Action<object, TState> callback, TState state)
		{
		}

		/// <inheritdoc />
		IDisposable IExternalScopeProvider.Push(object state)
		{
			return NullScope.Instance;
		}
	}

}
