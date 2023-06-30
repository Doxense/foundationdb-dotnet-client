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

	/// <summary>Erreur survenue lors de la désérialisation d'une valeur JSON en objet CLR</summary>
	[Serializable]
	public class JsonBindingException : InvalidOperationException
	{

		public JsonValue? Value { get; }

		public string? Path { get; }

		public JsonBindingException()
		{ }

		public JsonBindingException(string message)
			: base(message)
		{ }

		public JsonBindingException(string message, Exception? innerException)
			: base(message, innerException)
		{ }

		public JsonBindingException(string message, JsonValue? value, Exception? innerException = null)
			: base(message, innerException)
		{
			this.Value = value;
		}

		public JsonBindingException(string message, string? path, JsonValue? value, Exception? innerException = null)
			: base(message, innerException)
		{
			this.Path = path;
			this.Value = value;
		}

		protected JsonBindingException(SerializationInfo info, StreamingContext context)
			: base(info, context)
		{
			this.Value = JsonValue.Parse(info.GetString("Value"));
			this.Path = info.GetString("Path");
		}

		public override void GetObjectData(SerializationInfo info, StreamingContext context)
		{
			base.GetObjectData(info, context);
			info.AddValue("Value", this.Value?.ToJson());
			info.AddValue("Path", this.Path);
		}

	}

}
