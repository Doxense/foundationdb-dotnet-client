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
	using System.Collections.Generic;
	using System.Diagnostics;
	using System.IO;
	using System.Linq;
	using System.Net;
	using System.Net.Http;
	using System.Net.Security;
	using System.Net.Sockets;
	using System.Runtime.ExceptionServices;
	using System.Security.Authentication;
	using System.Security.Cryptography.X509Certificates;
	using System.Threading;
	using System.Threading.Tasks;
	using Doxense.Diagnostics.Contracts;
	using Doxense.Tools;

	/// <summary>Base class of generic options for <see cref="BetterHttpClient">HTTP clients</see></summary>
	public record BetterHttpClientOptions
	{

		public IBetterHttpHooks? Hooks { get; set; }

		public Version DefaultRequestVersion { get; set; } = HttpVersion.Version11;

		public HttpVersionPolicy DefaultVersionPolicy { get; set; } = HttpVersionPolicy.RequestVersionOrHigher;

		public List<IBetterHttpFilter> Filters { get; } = new List<IBetterHttpFilter>();

		public BetterDefaultHeaders DefaultRequestHeaders { get; set; } = new BetterDefaultHeaders();

		public bool? AllowAutoRedirect { get; set; }

		public DecompressionMethods? AutomaticDecompression { get; set; }

		public CookieContainer? Cookies { get; set; }

		public ICredentials? Credentials { get; set; }

		public bool? UseDefaultCredentials { get; set; }

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
