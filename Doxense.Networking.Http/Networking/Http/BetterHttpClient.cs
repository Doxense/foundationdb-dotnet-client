#region Copyright (c) 2023-2025 SnowBank SAS, (c) 2005-2023 Doxense SAS
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

namespace SnowBank.Networking.Http
{
	using System.Net.Http.Headers;
	using System.Runtime.CompilerServices;
	using System.Runtime.ExceptionServices;
	using Microsoft.Extensions.Logging;

	[DebuggerDisplay("Id={Id}, BaseAddress={HostAddress}")]
	public sealed class BetterHttpClient : IDisposable
	{

		private HttpClient Client { get; }

		private HttpMessageHandler Handler { get; }

		public string Id { get; }

		private int RequestCounter; //note: MUST be a field!

		public Uri HostAddress { get; }

		public BetterHttpClientOptions Options { get; }

		private ILogger Logger { get; }

		public CookieContainer? Cookies { get; }

		public Instant CreatedAt { get; }

		public IClock Clock { get; }

		public IServiceProvider Services { get; }

		internal static readonly HttpRequestOptionsKey<BetterHttpClientContext> OptionKey = new ("BetterHttp");

		internal sealed class MagicalHandler : DelegatingHandler
		{
			//note: due to how HttpHeaders are handled by the HttpClient, we are forced to hook our logic in a delegating handler,
			// which will be invoked once the HttpClient has completely set up the request (added default headers, etc...)

			public MagicalHandler(HttpMessageHandler innerHandler)
				: base(innerHandler)
			{ }

			protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
			{
				if (!request.Options.TryGetValue(OptionKey, out var context))
				{ // not enabled? skip it!
					return await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
				}

				var client = context.Client;
				var filters = client.Options.Filters;

				context.SetStage(BetterHttpClientStage.PrepareRequest);
				foreach (var filter in filters)
				{
					try
					{
						await filter.PrepareRequest(context).ConfigureAwait(false);
					}
					catch (Exception e)
					{
						if (!(client.Options.Hooks?.OnFilterError(context, e) ?? false))
						{
							throw;
						}
					}
				}
				client.Options.Hooks?.OnRequestPrepared(context);

				context.SetStage(BetterHttpClientStage.Send);
				var res = await base.SendAsync(request, cancellationToken);

				context.SetStage(BetterHttpClientStage.CompleteRequest);
				foreach (var filter in filters)
				{
					try
					{
						await filter.CompleteRequest(context).ConfigureAwait(false);
					}
					catch (Exception e)
					{
						if (!(client.Options.Hooks?.OnFilterError(context, e) ?? false))
						{
							throw;
						}
					}
				}
				client.Options.Hooks?.OnRequestCompleted(context);

				//note: handling of the response is deferred to the caller!
				return res;
			}
		}

		public BetterHttpClient(
			Uri hostAddress,
			BetterHttpClientOptions options,
			HttpMessageHandler handler,
			ILogger<BetterHttpClient> logger,
			NodaTime.IClock? clock,
			IServiceProvider services)
		{
			this.Id = CorrelationIdGenerator.GetNextId(); //BUGBUG: ca doit etre par requête! (le client est un singleton réutilisé plein de fois!)
			this.HostAddress = hostAddress;
			this.Handler = handler;
			this.Options = options;
			this.Cookies = options.Cookies;

			this.Logger = logger;
			this.Clock = clock ?? NodaTime.SystemClock.Instance;
			this.CreatedAt = this.Clock.GetCurrentInstant();
			this.Services = services;

			this.Client = CreateClientState();
		}

		public HttpRequestHeaders DefaultRequestHeaders => this.Client.DefaultRequestHeaders;

		private HttpClient CreateClientState()
		{
			//NOTE: on pourrait se passer de HttpClient et directement invoquer le HttpMessageHandler, mais:
			// 1) il y a beaucoup de logique de cancellation/timeout/erreur qui est déja implémentée dans HttpClient
			// 2) toutes les méthodes pour transférer les headers par défaut son internal donc on n'y a pas accès directement! :(
			// => pour l'instant on continue d'utiliser la classe directement!

			var client = CreateClient(this.Handler);

			// copy all the default headers
			this.Options.DefaultRequestHeaders.Apply(client.DefaultRequestHeaders);

			return client;
		}

		private HttpClient CreateClient(HttpMessageHandler handler)
		{
			// add our own delegating handler that will be able to hook into the request lifecycle
			handler = new MagicalHandler(handler);

			// add any optional wrappers on top of that
			if (this.Options.Handlers.Count > 0)
			{
				foreach (var factory in this.Options.Handlers)
				{
					handler = factory(handler, this.Services);
				}
			}

			var client = new HttpClient(handler, disposeHandler: true)
			{
				BaseAddress = this.HostAddress,
				DefaultRequestVersion = this.Options.DefaultRequestVersion,
				DefaultVersionPolicy = this.Options.DefaultVersionPolicy,
			};
			return client;
		}

		public string NewRequestId()
		{
			return this.Id + ":" + Interlocked.Increment(ref this.RequestCounter).ToString("D8");
		}

		#region Request Creation...

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private Uri ConvertPathToUri(string path)
			=> EnsureRelativeUri(new Uri(path, UriKind.RelativeOrAbsolute));

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public Uri EnsureRelativeUri(Uri path)
		{
			if (path.IsAbsoluteUri)
			{
				// must be the same base address!
				if (!this.HostAddress.IsBaseOf(path))
				{
					throw ErrorPathMustBeRelative();
				}
			}
			return path;
		}

		[MethodImpl(MethodImplOptions.NoInlining)]
		private static Exception ErrorPathMustBeRelative()
			// ReSharper disable once NotResolvedInText
			=> new ArgumentException("The query path must be a relative URI.", "path");

		public HttpRequestMessage CreateRequestMessage(HttpMethod method, string path, HttpContent? content = null)
			=> CreateRequestMessage(method, ConvertPathToUri(path), content);

		public HttpRequestMessage CreateRequestMessage(HttpMethod method, Uri path, HttpContent? content = null)
		{
			Contract.Debug.Requires(method != null && path != null);
			var req = new HttpRequestMessage(method, EnsureRelativeUri(path))
			{
				Version = this.Options.DefaultRequestVersion,
				VersionPolicy = this.Options.DefaultVersionPolicy,
				Content = content,
			};
			//note: les default headers sont ajoutés plus tards
			return req;
		}

		public HttpRequestMessage CreateGetRequest(string path)
			=> CreateGetRequest(ConvertPathToUri(path));

		public HttpRequestMessage CreateGetRequest(Uri path)
			=> CreateRequestMessage(HttpMethod.Get, path);

		public HttpRequestMessage CreatePostRequest(string path, HttpContent? content)
			=> CreatePostRequest(ConvertPathToUri(path), content);

		public HttpRequestMessage CreatePostRequest(Uri path, HttpContent? content)
			=> CreateRequestMessage(HttpMethod.Post, path, content);

		public HttpRequestMessage CreatePutRequest(string path, HttpContent content)
			=> CreatePutRequest(ConvertPathToUri(path), content);

		public HttpRequestMessage CreatePutRequest(Uri path, HttpContent content)
			=> CreateRequestMessage(HttpMethod.Put, path, content);

		public HttpRequestMessage CreatePatchRequest(string path, HttpContent content)
			=> CreatePatchRequest(ConvertPathToUri(path), content);

		public HttpRequestMessage CreatePatchRequest(Uri path, HttpContent content)
			=> CreateRequestMessage(HttpMethod.Patch, path, content);

		public HttpRequestMessage CreateDeleteRequest(string path)
			=> CreateDeleteRequest(ConvertPathToUri(path));

		public HttpRequestMessage CreateDeleteRequest(Uri path)
			=> CreateRequestMessage(HttpMethod.Delete, path);

		public HttpRequestMessage CreateHeadRequest(string path)
			=> CreateHeadRequest(ConvertPathToUri(path));

		public HttpRequestMessage CreateHeadRequest(Uri path)
			=> CreateRequestMessage(HttpMethod.Head, path);

		public HttpRequestMessage CreateOptionsRequest(string path)
			=> CreateHeadRequest(ConvertPathToUri(path));

		public HttpRequestMessage CreateOptionsRequest(Uri path)
			=> CreateRequestMessage(HttpMethod.Options, path);

		public HttpRequestMessage CreateTraceRequest(string path)
			=> CreateTraceRequest(ConvertPathToUri(path));

		public HttpRequestMessage CreateTraceRequest(Uri path)
			=> CreateRequestMessage(HttpMethod.Trace, path);

		#endregion

		public Task<TResult> SendAsync<TResult>(HttpRequestMessage request, Func<BetterHttpClientContext, Task<TResult>> handler, CancellationToken ct)
			=> SendCoreAsync<TResult>(request, handler, ct);

		public Task SendAsync(HttpRequestMessage request, Func<BetterHttpClientContext, Task> handler, CancellationToken ct)
			=> SendCoreAsync<object?>(request, handler, ct);

		public Task<TResult> SendAsync<TResult>(HttpRequestMessage request, Func<BetterHttpClientContext, TResult> handler, CancellationToken ct)
			=> SendCoreAsync<TResult>(request, handler, ct);

		public Task SendAsync(HttpRequestMessage request, Action<BetterHttpClientContext> handler, CancellationToken ct)
			=> SendCoreAsync<object?>(request, handler, ct);

		private async Task<TResult> SendCoreAsync<TResult>(HttpRequestMessage request, Delegate handler, CancellationToken ct)
		{
			Contract.Debug.Requires(request != null && handler != null);

			var filters = this.Options.Filters;

			var context = new BetterHttpClientContext()
			{
				Id = NewRequestId(),
				Client = this,
				Cancellation = ct,
				State = new Dictionary<string, object?>(StringComparer.Ordinal),
				Request = request,
			};

			try
			{
				request.Options.Set(OptionKey, context);

				// throw immediately if already cancelled!
				ct.ThrowIfCancellationRequested();

				#region Configure...

				context.SetStage(BetterHttpClientStage.Configure);
				foreach (var filter in filters)
				{
					Contract.Debug.Assert(filter != null);
					try
					{
						await filter.Configure(context).ConfigureAwait(false);
					}
					catch (Exception e)
					{
						if (!(this.Options.Hooks?.OnFilterError(context, e) ?? false))
						{
							throw;
						}
					}
				}
				this.Options.Hooks?.OnConfigured(context);

				#endregion

				using (request)
				{
					//note: handling of the request is performed inse the delegating handler

					#region Send...

					HttpResponseMessage res;
					try
					{
						res = await this.Client.SendAsync(context.Request, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
						context.OriginalResponse = res;
					}
					catch (Exception)
					{
						context.FailedStage ??= context.Stage;
						throw;
					}

					#endregion

					using (res)
					{
						#region Prepare Response...

						context.SetStage(BetterHttpClientStage.PrepareResponse);
						try
						{
							foreach (var filter in filters)
							{
								try
								{
									await filter.PrepareResponse(context).ConfigureAwait(false);
								}
								catch (Exception e)
								{
									this.Options.Hooks?.OnFilterError(context, e);
								}
							}

							this.Options.Hooks?.OnPrepareResponse(context);
						}
						catch (Exception)
						{
							context.FailedStage ??= context.Stage;
							throw;
						}

						#endregion

						#region Handle Response...

						context.SetStage(BetterHttpClientStage.HandleResponse);
						try
						{
							switch (handler)
							{
								case Func<BetterHttpClientContext, Task<TResult>> asyncResultHandler:
								{
									return await asyncResultHandler(context).ConfigureAwait(false);
								}
								case Func<BetterHttpClientContext, TResult> resultHandler:
								{
									return resultHandler(context);
								}
								case Func<BetterHttpClientContext, Task> asyncVoidHandler:
								{
									Contract.Debug.Requires(typeof(TResult) == typeof(object));
									await asyncVoidHandler(context).ConfigureAwait(false);
									return default!;
								}
								case Action<BetterHttpClientContext> voidHandler:
								{
									Contract.Debug.Requires(typeof(TResult) == typeof(object));
									voidHandler(context);
									return default!;
								}
								default:
								{
#if DEBUG
									// c'est pas normal! normalement on controle exactement le type de handler passé a cette fonction!
									if (System.Diagnostics.Debugger.IsAttached) System.Diagnostics.Debugger.Break();
#endif
									throw new ArgumentException("Unexpected delegate type", nameof(handler));
								}
							}
						}
						catch (Exception)
						{
							context.FailedStage ??= context.Stage;
							throw;
						}
						finally
						{
							#region Complete Response...

							context.SetStage(BetterHttpClientStage.CompleteResponse);
							foreach (var filter in filters)
							{
								try
								{
									await filter.CompleteResponse(context).ConfigureAwait(false);
								}
								catch (Exception e)
								{
									if (!(this.Options.Hooks?.OnFilterError(context, e) ?? false))
									{
										throw;
									}
								}
							}
							this.Options.Hooks?.OnCompleteResponse(context);

							#endregion
						}

						#endregion
					}
				}
			}
			catch (Exception e)
			{
				context.Error = ExceptionDispatchInfo.Capture(e);
				context.FailedStage ??= context.Stage;
				this.Options.Hooks?.OnError(context, e);
				throw;
			}
			finally
			{
				#region Finalize...

				context.SetStage(BetterHttpClientStage.Finalize);
				foreach (var filter in filters)
				{
					try
					{
						await filter.Finalize(context).ConfigureAwait(false);
					}
					catch (Exception e)
					{
						if (!(this.Options.Hooks?.OnFilterError(context, e) ?? false))
						{
							throw;
						}
					}
				}
				this.Options.Hooks?.OnFinalizeQuery(context);

				#endregion
			}
		}

		#region IDisposable...

		public void Dispose()
		{
			Dispose(true);
		}

		private void Dispose(bool disposing)
		{
			if (disposing)
			{
				this.Client.Dispose();
			}
		}

		#endregion

	}

}
