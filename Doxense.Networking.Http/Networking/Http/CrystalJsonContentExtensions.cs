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
	using System.Text;
	using System.Threading;
	using System.Threading.Tasks;
	using Doxense.Diagnostics.Contracts;
	using Doxense.Serialization.Json;

	public static class CrystalJsonContentExtensions
	{
		#region Reading...

		public static Task<JsonObject?> ReadFromCrystalJsonObjectAsync(this HttpContent content, CancellationToken ct)
		{
			return ReadFromCrystalJsonObjectAsync(content, null, ct);
		}

		public static Task<JsonObject?> ReadFromCrystalJsonObjectAsync(this HttpContent content, CrystalJsonSettings? settings, CancellationToken ct)
		{
			Contract.NotNull(content);

			//Encoding? sourceEncoding = JsonContent.GetEncoding(content.Headers.ContentType?.CharSet);
			Encoding? sourceEncoding = null; //TODO!

			return ReadFromJsonObjectAsyncCore(content, sourceEncoding, settings, ct);
		}

		private static async Task<JsonObject?> ReadFromJsonObjectAsyncCore(HttpContent content, Encoding? sourceEncoding, CrystalJsonSettings? settings, CancellationToken ct)
		{
			var bytes  = await content.ReadAsByteArrayAsync(ct).ConfigureAwait(false);

			return CrystalJson.ParseObject(bytes.AsSlice(), settings);
		}

		public static Task<T?> ReadFromCrystalJsonAsync<T>(this HttpContent content, CancellationToken ct)
		{
			return ReadFromCrystalJsonAsync<T>(content, null, null, ct);
		}

		public static Task<T?> ReadFromCrystalJsonAsync<T>(this HttpContent content, CrystalJsonSettings? settings, ICrystalJsonTypeResolver? resolver, CancellationToken ct)
		{
			Contract.NotNull(content);

			//Encoding? sourceEncoding = JsonContent.GetEncoding(content.Headers.ContentType?.CharSet);
			Encoding? sourceEncoding = null; //TODO!

			return ReadFromJsonAsyncCore<T>(content, sourceEncoding, settings, resolver, ct);
		}

		private static async Task<T?> ReadFromJsonAsyncCore<T>(HttpContent content, Encoding? sourceEncoding, CrystalJsonSettings? settings, ICrystalJsonTypeResolver? resolver, CancellationToken ct)
		{
			var bytes  = await content.ReadAsByteArrayAsync(ct).ConfigureAwait(false);

			return CrystalJson.Deserialize<T>(bytes.AsSlice(), settings, resolver);
		}

		#endregion
	}

}
