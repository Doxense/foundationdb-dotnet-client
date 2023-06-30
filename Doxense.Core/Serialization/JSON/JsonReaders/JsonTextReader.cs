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
	using System.IO;
	using System.Runtime.CompilerServices;
	using Doxense.Diagnostics.Contracts;

	/// <summary>JSON text reader that wraps an underlying TextReader instance</summary>
	public struct JsonTextReader : IJsonReader
	{
		internal readonly TextReader Reader;

		public JsonTextReader(TextReader reader)
		{
			Contract.NotNull(reader);
			this.Reader = reader;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public int Read()
		{
			return this.Reader.Read();
		}

	}
}
