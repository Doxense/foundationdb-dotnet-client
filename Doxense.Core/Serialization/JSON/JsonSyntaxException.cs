#region Copyright (c) 2005-2023 Doxense SAS
//
// All rights are reserved. Reproduction or transmission in whole or in part, in
// any form or by any means, electronic, mechanical or otherwise, is prohibited
// without the prior written consent of the copyright owner.
//
#endregion

namespace Doxense.Serialization.Json
{
	using System;
	using System.Runtime.Serialization;

	/// <summary>Erreur de syntaxe lors du parsing d'un document JSON</summary>
	[Serializable]
	public class JsonSyntaxException : FormatException
	{
		private readonly string? m_reason;
		private readonly long m_offset;
		private readonly int m_line;
		private readonly int m_position;

		public JsonSyntaxException()
		{ }

		public JsonSyntaxException(string message)
			: base(message)
		{ }

		public JsonSyntaxException(string message, Exception innerException)
			: base(message, innerException)
		{ }

		public JsonSyntaxException(string message, string? reason)
			: base(reason == null ? message : $"{message}: {reason}")
		{
			m_reason = reason;
		}

		public JsonSyntaxException(string message, string? reason, long offset, int line, int position)
			: base(String.Format(reason == null ? "{0} at ln {1} col {2}" : "{0} at ln {1} col {2}: {3}", message, line, position, reason))
		{
			m_reason = reason;
			m_offset = offset;
			m_line = line;
			m_position = position;
		}

		protected JsonSyntaxException(SerializationInfo info, StreamingContext context)
			: base(info, context)
		{
			m_reason = info.GetString("Reason");
			m_offset = info.GetInt64("Offset");
			m_line = info.GetInt32("Line");
			m_position = info.GetInt32("Position");
		}

		public override void GetObjectData(SerializationInfo info, StreamingContext context)
		{
			base.GetObjectData(info, context);
			info.AddValue("Reason", m_reason);
			info.AddValue("Offset", m_offset);
			info.AddValue("Line", m_line);
			info.AddValue("Position", m_position);
		}

		public string Reason => m_reason ?? string.Empty;

		public long Offset => m_offset;

		public int Line => m_line;

		public int Position => m_position;

	}

}
