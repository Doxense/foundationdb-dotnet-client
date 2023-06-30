﻿#region Copyright (c) 2005-2023 Doxense SAS
//
// All rights are reserved. Reproduction or transmission in whole or in part, in
// any form or by any means, electronic, mechanical or otherwise, is prohibited
// without the prior written consent of the copyright owner.
//
#endregion

namespace Doxense
{
	using System;
	using System.Globalization;
	using System.Text;
	using Doxense.Diagnostics.Contracts;
	using Microsoft.Extensions.Logging;
	using NUnit.Framework;

	internal class NUnitLogger : ILogger
	{

		private static readonly string NewLineWithMessagePadding = Environment.NewLine + "# > ";

		public string Name { get; }

		private string LoggedName { get; }

		private string? LoggedActor { get; }

		private NUnitLoggerOptions Options { get; }

		public IExternalScopeProvider ScopeProvider { get; set; }

		public NUnitLogger(string name, NUnitLoggerOptions options, IExternalScopeProvider provider)
		{
			this.Name = name;
			this.Options = options;
			this.ScopeProvider = provider;
			if (options.UseShortName)
			{
				int p = name.LastIndexOf('.');
				this.LoggedName = "[" + (p < 0 ? name : name.Substring(p + 1)) + "]";
			}
			else
			{
				this.LoggedName = "[" + name + "]";
			}

			if (options.ActorId != null)
			{
				this.LoggedActor = "@" + options.ActorId;
			}
			else
			{
				this.LoggedActor = null;
			}
		}

		private static string GetLogLevelString(LogLevel logLevel)
		{
			switch (logLevel)
			{
				case LogLevel.Trace:
					return "  ~~~~~";
				case LogLevel.Debug:
					return "  =====";
				case LogLevel.Information:
					return "  INFO ";
				case LogLevel.Warning:
					return "! WARN ";
				case LogLevel.Error:
					return "* ERROR";
				case LogLevel.Critical:
					return "**FATAL";
				default:
					throw new ArgumentOutOfRangeException(nameof(logLevel));
			}
		}

		public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string?> formatter)
		{
			if (!IsEnabled(logLevel))
			{
				return;
			}
			Contract.NotNull(formatter);

			var now = DateTime.Now;
			var message = formatter(state, exception);

			if (!string.IsNullOrEmpty(message) || exception != null)
			{
				WriteMessage(now, logLevel, this.LoggedActor, this.LoggedName, eventId.Id, message, exception);
			}
		}

		[ThreadStatic]
		private static StringBuilder? CachedBuilderInstance;

		public void WriteMessage(DateTime now, LogLevel logLevel, string? actorId, string logName, int eventId, string? message, Exception? exception)
		{
			var logBuilder = CachedBuilderInstance;
			CachedBuilderInstance = null;

			if (logBuilder == null)
			{
				logBuilder = new StringBuilder();
			}

			CreateDefaultLogMessage(logBuilder, now, logLevel, actorId, logName, eventId, message, exception);

			var output = (logLevel >= LogLevel.Error ? this.Options.OutputError : null) ?? this.Options.Output ?? TestContext.Progress;
			string text = logBuilder.ToString();
			output.WriteLine(text);

			System.Diagnostics.Debug.WriteLine(text);

			if (logLevel >= LogLevel.Error)
			{
				this.Options.ErrorHandler?.Invoke(logLevel, text);
			}

			logBuilder.Clear();
			if (logBuilder.Capacity > 1024)
			{
				logBuilder.Capacity = 1024;
			}
			CachedBuilderInstance = logBuilder;
		}

		private void CreateDefaultLogMessage(StringBuilder logBuilder, DateTime now, LogLevel logLevel, string? actorId, string logName, int eventId, string? message, Exception? exception)
		{
			logBuilder.Append("# ");

			// timestamp
			if (this.Options.TraceTimestamp)
			{
				var origin = this.Options.DateOrigin;
				if (origin != null)
				{
					logBuilder.Append((now - origin.Value).TotalSeconds.ToString("N3", CultureInfo.InvariantCulture)).Append(' ');
				}
				else
				{
					logBuilder.Append(now.ToString("HH:mm:ss.fff", CultureInfo.InvariantCulture)).Append(' ');
				}
			}

			// log level
			logBuilder.Append(GetLogLevelString(logLevel));

			// logger id
			if (actorId != null)
			{
				logBuilder.Append(' ').Append(actorId);
				if (actorId.Length < 10) logBuilder.Append(' ', 10 - actorId.Length);
			}

			// category
			logBuilder.Append(' ').Append(logName);
			if (logName.Length < 32) logBuilder.Append(' ', 32 - logName.Length);

			// event id
			if (this.Options.IncludeEventId && eventId != 0)
			{
				logBuilder.Append(" (").Append(eventId).Append(')');
			}

			// scope information
			if (this.Options.IncludeScopes)
			{
				GetScopeInformation(logBuilder, multiLine: true);
			}

			// message
			if (!string.IsNullOrEmpty(message))
			{
				logBuilder.Append(" \"").Append(message).Append('"');
			}

			// Example:
			// System.InvalidOperationException
			//    at Namespace.Class.Function() in File:line X
			if (exception != null)
			{
				// exception message
				logBuilder.AppendLine().Append("Error: ").Append(exception.ToString());
			}

			logBuilder.Replace(Environment.NewLine, NewLineWithMessagePadding);
		}

		private void GetScopeInformation(StringBuilder stringBuilder, bool multiLine)
		{
			var scopeProvider = ScopeProvider;
			if (scopeProvider != null)
			{
				var initialLength = stringBuilder.Length;

				scopeProvider.ForEachScope((scope, state) =>
				{
					var (builder, paddAt) = state;
					var padd = paddAt == builder.Length;
					if (padd)
					{
						builder.AppendLine().Append("        => ");
					}
					else
					{
						builder.AppendLine().Append(" => ");
					}
					builder.Append(scope);
				}, (stringBuilder, multiLine ? initialLength : -1));

				if (stringBuilder.Length > initialLength && multiLine)
				{
					stringBuilder.AppendLine();
				}
			}
		}

		public bool IsEnabled(LogLevel logLevel)
		{
			return logLevel >= this.Options.LogLevel && logLevel < LogLevel.None;
		}

		public IDisposable BeginScope<TState>(TState state) => ScopeProvider?.Push(state) ?? NullScope.Instance;

	}

}