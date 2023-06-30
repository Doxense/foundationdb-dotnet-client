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
	using System.Linq;
	using System.Net;
	using System.Net.Http;
	using System.Threading;
	using System.Threading.Tasks;
	using Microsoft.Net.Http.Headers;

	public class CookieContainerMessageHandler : DelegatingHandler
	{
		public CookieContainer Cookies { get; }

		public CookieContainerMessageHandler(CookieContainer cookies, HttpMessageHandler next)
			: base(next)
		{
			this.Cookies = cookies;
		}

		protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
		{
			var value = this.Cookies.GetCookieHeader(request.RequestUri);
			if (!string.IsNullOrWhiteSpace(value))
			{
				request.Headers.Add(HeaderNames.Cookie, value);
			}

			var res = await base.SendAsync(request, cancellationToken);

			if (res.Headers.TryGetValues(HeaderNames.SetCookie, out var values))
			{
				foreach (SetCookieHeaderValue cookieHeader in SetCookieHeaderValue.ParseList(values.ToList()))
				{
					Cookie cookie = new Cookie(cookieHeader.Name.Value, cookieHeader.Value.Value, cookieHeader.Path.Value);
					if (cookieHeader.Expires.HasValue)
					{
						cookie.Expires = cookieHeader.Expires.Value.DateTime;
					}
					this.Cookies.Add(request.RequestUri, cookie);
				}
			}

			return res;
		}
	}
}
