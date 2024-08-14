#region Copyright (c) 2023-2024 SnowBank SAS, (c) 2005-2023 Doxense SAS
// All rights reserved.
// 
// Redistribution and use in source and binary forms, with or without
// modification, are permitted provided that the following conditions are met:
// 	* Redistributions of source code must retain the above copyright
// 	  notice, this list of conditions and the following disclaimer.
// 	* Redistributions in binary form must reproduce the above copyright
// 	  notice, this list of conditions and the following disclaimer in the
// 	  documentation and/or other materials provided with the distribution.
// 	* Neither the name of SnowBank nor the
// 	  names of its contributors may be used to endorse or promote products
// 	  derived from this software without specific prior written permission.
// 
// THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND
// ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
// WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
// DISCLAIMED. IN NO EVENT SHALL SNOWBANK SAS BE LIABLE FOR ANY
// DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
// (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
// LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
// ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
// (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
// SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
#endregion

namespace Doxense.Networking.Http
{
	using System.Net;
	using System.Net.Http;
	using System.Net.Security;
	using System.Security.Authentication;
	using System.Security.Cryptography.X509Certificates;

	/// <summary>Base class of generic options for <see cref="BetterHttpClient">HTTP clients</see></summary>
	public record BetterHttpClientOptions
	{

		/// <summary>Optional hooks</summary>
		/// <remarks>Mostly used for unit testing or low-level debugging</remarks>
		public IBetterHttpHooks? Hooks { get; set; }

		/// <summary>Default initial HTTP version for all requests</summary>
		public Version DefaultRequestVersion { get; set; } = HttpVersion.Version11;

		/// <summary>Default policy for selecting the HTTP version of a request</summary>
		public HttpVersionPolicy DefaultVersionPolicy { get; set; } = HttpVersionPolicy.RequestVersionOrHigher;

		/// <summary>List of filters that will be able to intercept and or modify the request and response</summary>
		public List<IBetterHttpFilter> Filters { get; } = new();

		/// <summary>List of wrappers that can be applied to the underlying HTTP message handler</summary>
		public List<Func<HttpMessageHandler, IServiceProvider, HttpMessageHandler>> Handlers { get; set; } = new();

		/// <summary>List of default headers applied to each requests</summary>
		public BetterDefaultHeaders DefaultRequestHeaders { get; set; } = new();

		/// <summary>Specifies whether the client should follow redirection responses.</summary>
		public bool? AllowAutoRedirect { get; set; }

		/// <summary>Specifies the type of decompression method used by the handler for automatic decompression of the HTTP content response.</summary>
		public DecompressionMethods? AutomaticDecompression { get; set; }

		/// <summary>Default cookie container that will be used by each requests.</summary>
		public CookieContainer? Cookies { get; set; }

		/// <summary>Default credentials that will be used by each requests.</summary>
		public ICredentials? Credentials { get; set; }

		/// <summary>Specifies whether default credentials are sent with requests by the client.</summary>
		public bool? UseDefaultCredentials { get; set; }

		/// <summary>Specifies the proxy information used by the client.</summary>
		public IWebProxy? Proxy { get; set; }

		public ICredentials? DefaultProxyCredentials { get; set; }

		public Func<HttpRequestMessage, X509Certificate2?, X509Chain?, SslPolicyErrors, bool>? ServerCertificateCustomValidationCallback { get; set; }

		public ClientCertificateOption? ClientCertificateOptions { get; set; }

		public bool? CheckCertificateRevocationList { get; set; }

		public SslProtocols? SslProtocols { get; set; } //REVIEW: est-ce qu'on force un meilleur défaut?

		public X509CertificateCollection? ClientCertificates { get; set; }

		[Obsolete("This is dangerous! Please acknowledge this by using a #pragma to disable this warning.")]
		public void DangerousAcceptAnyServerCertificate()
		{
			this.ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;
		}

		protected virtual HttpMessageHandler ConfigureDefaults(HttpMessageHandler handler)
		{
			if (handler is HttpClientHandler clientHandler)
			{
				if (this.AllowAutoRedirect != null) clientHandler.AllowAutoRedirect = this.AllowAutoRedirect.Value;
				if (this.AutomaticDecompression != null) clientHandler.AutomaticDecompression = this.AutomaticDecompression.Value;
			}

			return handler;
		}

		protected virtual HttpMessageHandler ConfigureCookies(HttpMessageHandler handler)
		{
			if (this.Cookies != null)
			{
				if (handler is HttpClientHandler clientHandler)
				{
					clientHandler.UseCookies = true;
					clientHandler.CookieContainer = this.Cookies;
				}
				else
				{
					return new CookieContainerMessageHandler(this.Cookies, handler);
				}
			}
			return handler;
		}

		protected virtual HttpMessageHandler ConfigureAuthentication(HttpMessageHandler handler)
		{
			if (handler is HttpClientHandler clientHandler)
			{
				if (this.Credentials != null) clientHandler.Credentials = this.Credentials;
				if (this.UseDefaultCredentials != null) clientHandler.UseDefaultCredentials = this.UseDefaultCredentials.Value;
			}

			return handler;
		}

		protected virtual HttpMessageHandler ConfigureProxy(HttpMessageHandler handler)
		{
			if (handler is HttpClientHandler clientHandler)
			{
				if (this.Proxy != null)
				{
					clientHandler.UseProxy = true;
					clientHandler.Proxy = this.Proxy;
				}
				if (this.DefaultProxyCredentials != null)
				{
					clientHandler.DefaultProxyCredentials = this.DefaultProxyCredentials;
				}
			}

			return handler;
		}

		protected virtual HttpMessageHandler ConfigureHttps(HttpMessageHandler handler)
		{
			if (handler is BetterHttpClientHandler betterHandler)
			{
				if (this.ClientCertificates != null) betterHandler.ClientCertificates.AddRange(this.ClientCertificates);
				if (this.ServerCertificateCustomValidationCallback != null) betterHandler.ServerCertificateCustomValidationCallback = this.ServerCertificateCustomValidationCallback;
				if (this.ClientCertificateOptions != null) betterHandler.ClientCertificateOptions = this.ClientCertificateOptions.Value;
				if (this.CheckCertificateRevocationList != null) betterHandler.CheckCertificateRevocationList = this.CheckCertificateRevocationList.Value;
				if (this.SslProtocols != null) betterHandler.SslProtocols = this.SslProtocols.Value;
			}
			else if (handler is HttpClientHandler clientHandler)
			{
				if (this.ClientCertificates != null) clientHandler.ClientCertificates.AddRange(this.ClientCertificates);
				if (this.ServerCertificateCustomValidationCallback != null) clientHandler.ServerCertificateCustomValidationCallback = this.ServerCertificateCustomValidationCallback;
				if (this.ClientCertificateOptions != null) clientHandler.ClientCertificateOptions = this.ClientCertificateOptions.Value;
				if (this.CheckCertificateRevocationList != null) clientHandler.CheckCertificateRevocationList = this.CheckCertificateRevocationList.Value;
				if (this.SslProtocols != null) clientHandler.SslProtocols = this.SslProtocols.Value;
			}
			return handler;
		}

		public HttpMessageHandler Configure(HttpMessageHandler handler)
		{
			Contract.Debug.Requires(handler != null);

			if (handler is BetterHttpClientHandler betterHandler)
			{
				betterHandler.Setup(this);
			}

			handler = ConfigureDefaults(handler);

			handler = ConfigureCookies(handler);

			handler = ConfigureAuthentication(handler);

			handler = ConfigureProxy(handler);

			handler = ConfigureHttps(handler);

			return handler;
		}

	}

}
