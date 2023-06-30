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
	using Doxense.Memory;

	public sealed class JsonSliceSerializer : ISliceSerializer<JsonValue>
	{

		public static readonly JsonSliceSerializer Default = new JsonSliceSerializer();

		private JsonSliceSerializer() { }

		public void WriteTo(ref SliceWriter writer, JsonValue value)
		{
			value.WriteTo(ref writer);
		}

		bool ISliceSerializer<JsonValue>.TryReadFrom(ref SliceReader reader, out JsonValue value)
		{
			//TODO: !!!
			throw new NotImplementedException();
		}
	}
}
