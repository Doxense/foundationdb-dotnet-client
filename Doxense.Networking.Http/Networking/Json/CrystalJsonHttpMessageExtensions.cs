#region Copyright (c) 2005-2023 Doxense SAS
//
// All rights are reserved. Reproduction or transmission in whole or in part, in
// any form or by any means, electronic, mechanical or otherwise, is prohibited
// without the prior written consent of the copyright owner.
//
#endregion

namespace Doxense.AspNetCore.Common.Json
{
	using System;
	using System.Net.Http;
	using System.Threading;
	using System.Threading.Tasks;
	using Doxense.Diagnostics.Contracts;
	using Doxense.Serialization.Json;

	public static class CrystalJsonHttpMessageExtensions
	{

		public static Task<HttpResponseMessage> PostAsCrystalJsonAsync<TValue>(this HttpClient client, string? requestUri, TValue value, CrystalJsonSettings? options = null, CancellationToken cancellationToken = default)
		{
			Contract.NotNull(client);

			var content = CrystalJsonContent.Create(value, mediaType: null, options);
			return client.PostAsync(requestUri, content, cancellationToken);
		}

		public static Task<HttpResponseMessage> PostAsCrystalJsonAsync(this HttpClient client, string? requestUri, JsonValue value, CrystalJsonSettings? options = null, CancellationToken cancellationToken = default)
		{
			Contract.NotNull(client);

			var content = CrystalJsonContent.Create(value, mediaType: null, options);
			return client.PostAsync(requestUri, content, cancellationToken);
		}

		public static Task<HttpResponseMessage> PostAsCrystalJsonAsync<TValue>(this HttpClient client, Uri? requestUri, TValue value, CrystalJsonSettings? options = null, CancellationToken cancellationToken = default)
		{
			Contract.NotNull(client);

			var content = CrystalJsonContent.Create(value, mediaType: null, options);
			return client.PostAsync(requestUri, content, cancellationToken);
		}

		public static Task<HttpResponseMessage> PostAsCrystalJsonAsync(this HttpClient client, Uri? requestUri, JsonValue value, CrystalJsonSettings? options = null, CancellationToken cancellationToken = default)
		{
			Contract.NotNull(client);

			var content = CrystalJsonContent.Create(value, mediaType: null, options);
			return client.PostAsync(requestUri, content, cancellationToken);
		}

		public static Task<HttpResponseMessage> PostAsCrystalJsonAsync<TValue>(this HttpClient client, string? requestUri, TValue value, CancellationToken cancellationToken)
			=> client.PostAsCrystalJsonAsync(requestUri, value, options: null, cancellationToken);

		public static Task<HttpResponseMessage> PostAsCrystalJsonAsync(this HttpClient client, string? requestUri, JsonValue value, CancellationToken cancellationToken)
			=> client.PostAsCrystalJsonAsync(requestUri, value, options: null, cancellationToken);

		public static Task<HttpResponseMessage> PostAsCrystalJsonAsync<TValue>(this HttpClient client, Uri? requestUri, TValue value, CancellationToken cancellationToken)
			=> client.PostAsCrystalJsonAsync(requestUri, value, options: null, cancellationToken);

		public static Task<HttpResponseMessage> PostAsCrystalJsonAsync(this HttpClient client, Uri? requestUri, JsonValue value, CancellationToken cancellationToken)
			=> client.PostAsCrystalJsonAsync(requestUri, value, options: null, cancellationToken);

	}

}
