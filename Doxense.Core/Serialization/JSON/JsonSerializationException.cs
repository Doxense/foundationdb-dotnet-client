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

	/// <summary>Erreur survenue lors de la sérialisation d'un objet en document JSON</summary>
	[Serializable]
	public class JsonSerializationException : InvalidOperationException
	{

		public JsonSerializationException()
		{ }

		public JsonSerializationException(string message)
			: base(message)
		{ }

		public JsonSerializationException(string message, Exception? innerException)
			: base(message, innerException)
		{ }

		protected JsonSerializationException(SerializationInfo info, StreamingContext context)
			: base(info, context)
		{ }

	}

}
