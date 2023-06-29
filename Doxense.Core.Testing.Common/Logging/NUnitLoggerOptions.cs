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
	using System.IO;
	using Microsoft.Extensions.Logging;

	/// <summary>Options used to configure the <see cref="NUnitLoggerProvider"/></summary>
	public sealed class NUnitLoggerOptions
	{

		/// <summary>Minimum log level during test execution</summary>
		public LogLevel LogLevel { get; set; } = LogLevel.Trace;

		public string? ActorId { get; set; }

		/// <summary>Output used to write the logs.</summary>
		/// <remarks>If null, will use <see cref="NUnit.Framework.TestContext.Out">TestContext.Out</see> by default.</remarks>
		public TextWriter? Output { get; set; }

		/// <summary>If not-null, write Errors (or above) to this output instead of <see cref="Output"/>.</summary>
		public TextWriter? OutputError { get; set; }

		/// <summary>If true, output the timestamp. If <see cref="DateOrigin"/> is not null, display the relative time from the origin.</summary>
		public bool TraceTimestamp { get; set; } = true;

		/// <summary>If non-null, origin date used to compute the elapsed time.</summary>
		public DateTime? DateOrigin { get; set; }

		/// <summary>If true, output the scope of the logger</summary>
		public bool IncludeScopes { get; set; }

		/// <summary>If true, output the event id</summary>
		public bool IncludeEventId { get; set; } = true;

		/// <summary>If true, outputs only the class name (ex: "Foo"). If false, outputs the full name (ex: "Acme.Frobs.Stuff.Foo")</summary>
		public bool UseShortName { get; set; } = true;

		/// <summary>Custom handler called for log with level <see cref="LogLevel">Error</see> or higher</summary>
		public Action<LogLevel, string>? ErrorHandler { get; set; }

	}

}
