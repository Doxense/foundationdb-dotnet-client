// copy/paste de System.Net.Http.HttpClientHandler, hackée pour être accessible publiquement

//#define FULL_DEBUG

namespace Doxense.Networking.Http
{
	using System.IO;
	using System.Linq.Expressions;
	using System.Net;
	using System.Net.Http;
	using System.Net.Security;
	using System.Net.Sockets;
	using System.Reflection;
	using System.Runtime.ExceptionServices;
	using System.Security.Authentication;
	using System.Security.Cryptography;
	using System.Security.Cryptography.X509Certificates;
	using System.Threading.Tasks;

	/// <summary>Version non-"protected" de <see cref="System.Net.Http.HttpClientHandler"/></summary>
	[PublicAPI]
	public class BetterHttpClientHandler : HttpMessageHandler
	{

		// La verison normale de HttpMessageHandler ne permet pas de lui passer un SocketsHttpHandler via ctor, ni d'accéder a celui qui a été créé.
		// Egalement, on ne peut pas appeler les méthodes Send/SendAsync publiquement, ce qui rend l'intégration très complexe.
		// => cette class rend les champs publique, et donc permet de customiser le SocketsHttpHandler (pour lui passer des callbacks custom, par exemple)

		public SocketsHttpHandler Sockets { get; }

		public INetworkMap Network { get; }

		private ClientCertificateOption m_clientCertificateOptions;

		private Func<HttpRequestMessage, X509Certificate2?, X509Chain?, SslPolicyErrors, bool>? m_serverCustomValidationCallback;

		private volatile bool m_disposed;

		#region Cheat Codes...

		/// <summary>Helper class pour pouvoir invoquer les méthodes "protected internal" de SocketsHttpHandler</summary>
		private static class CheatCodes
		{

			private static Func<SocketsHttpHandler, HttpRequestMessage, CancellationToken, HttpResponseMessage> GetSendHandler()
			{
				var method = typeof(SocketsHttpHandler).GetMethod("Send", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
				if (method == null)
				{
#if DEBUG
					if (Debugger.IsAttached) Debugger.Break();
#endif
					throw new NotSupportedException("Could not find SocketsHttpHandler.Send(). This may be caused by a new version of the .NET Framework that is not supported by this library!");
				}

				var prmHandler = Expression.Parameter(typeof(SocketsHttpHandler), "handler");
				var prmMessage = Expression.Parameter(typeof(HttpRequestMessage), "message");
				var prmCancellationToken = Expression.Parameter(typeof(CancellationToken), "ct");
				var body = Expression.Call(prmHandler, method, prmMessage, prmCancellationToken);

				var lambda = Expression.Lambda<Func<SocketsHttpHandler, HttpRequestMessage, CancellationToken, HttpResponseMessage>>(
					body,
					"SocketsHttpHandler_Send",
					tailCall: true,
					[ prmHandler, prmMessage, prmCancellationToken ]
				);
				return lambda.Compile();
			}

			private static Func<SocketsHttpHandler, HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> GetSendAsyncHandler()
			{
				var method = typeof(SocketsHttpHandler).GetMethod("SendAsync", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
				if (method == null)
				{
#if DEBUG
					if (Debugger.IsAttached) Debugger.Break();
#endif
					throw new NotSupportedException("Could not find SocketsHttpHandler.SendAsync(). This may be caused by a new version of the .NET Framework that is not supported by this library!");
				}

				// lambda(SocketsHttpHandler handler, HttpRequestMessage message, CancellationToken ct) => handler.SendAsync(message, ct);

				var prmHandler = Expression.Parameter(typeof(SocketsHttpHandler), "handler");
				var prmMessage = Expression.Parameter(typeof(HttpRequestMessage), "message");
				var prmCancellationToken = Expression.Parameter(typeof(CancellationToken), "ct");
				var body = Expression.Call(prmHandler, method, prmMessage, prmCancellationToken);

				var lambda = Expression.Lambda<Func<SocketsHttpHandler, HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>>>(
					body,
					"SocketsHttpHandler_SendAsync",
					tailCall: true,
					[ prmHandler, prmMessage, prmCancellationToken ]
				);
				return lambda.Compile();
			}

			private static Func<Func<HttpRequestMessage, X509Certificate2?, X509Chain?, SslPolicyErrors, bool>, RemoteCertificateValidationCallback> GetCertificateCallbackMapper()
			{

				var t = typeof(HttpClientHandler).Assembly.GetType("System.Net.Http.ConnectHelper+CertificateCallbackMapper");
				if (t == null)
				{
#if DEBUG
					if (Debugger.IsAttached) Debugger.Break();
#endif
					throw new NotSupportedException("Could not find System.Net.Http.ConnectHelper+CertificateCallbackMapper. This may be caused by a new version of the .NET Framework that is not supported by this library!");
				}

				var ctor = t.GetConstructor(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public, null, [ typeof(Func<HttpRequestMessage, X509Certificate2?, X509Chain?, SslPolicyErrors, bool>) ], null);
				if (ctor == null)
				{
#if DEBUG
					if (Debugger.IsAttached) Debugger.Break();
#endif
					throw new NotSupportedException($"Could not find {t.FullName}.ctor(...). This may be caused by a new version of the .NET Framework that is not supported by this library!");
				}

				var prmHandler = Expression.Parameter(typeof(Func<HttpRequestMessage, X509Certificate2?, X509Chain?, SslPolicyErrors, bool>), "fromHttpClientHandler");

				var body = Expression.Field(
					Expression.New(ctor, prmHandler),
					"ForSocketsHttpHandler"
				);

				return Expression.Lambda<Func<Func<HttpRequestMessage, X509Certificate2?, X509Chain?, SslPolicyErrors, bool>, RemoteCertificateValidationCallback>>(
					body,
					"ConnectHelper_CertificateCallbackMapper",
					tailCall: true,
					[ prmHandler ]
				).Compile();

			}

			public static readonly Func<SocketsHttpHandler, HttpRequestMessage, CancellationToken, HttpResponseMessage> SendInvoker = GetSendHandler();

			public static readonly Func<SocketsHttpHandler, HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> SendAsyncInvoker = GetSendAsyncHandler();

			public static readonly Func<Func<HttpRequestMessage, X509Certificate2?, X509Chain?, SslPolicyErrors, bool>, RemoteCertificateValidationCallback> CallbackMapperInvoker = GetCertificateCallbackMapper();

		}

		public static readonly bool HasCheatCodes = TestCheatCodes();

		private static bool TestCheatCodes()
		{
			try
			{
				_ = CheatCodes.CallbackMapperInvoker;
				return true;
			}
			catch (Exception)
			{
#if DEBUG
				// Si vous breakez ici, on est dans la mouise! Il faut regarder ce qu'ils ont changé dans la façon dont la classe interne "ConnectHelper.CertificateCallbackMapper" a été modifiée!
				if (Debugger.IsAttached) Debugger.Break();
#endif
				return false;
			}
		}

		#endregion

		public BetterHttpClientHandler(INetworkMap map) : this(map, new SocketsHttpHandler())
		{ }

		public BetterHttpClientHandler(INetworkMap map, SocketsHttpHandler sockets)
		{
			Contract.NotNull(map);
			Contract.NotNull(sockets);

			this.Network = map;
			this.Sockets = sockets;
			this.ClientCertificateOptions = ClientCertificateOption.Manual;
		}

		protected override void Dispose(bool disposing)
		{
			if (disposing && !m_disposed)
			{
				m_disposed = true;
				this.Sockets.Dispose();
			}

			base.Dispose(disposing);
		}
		

		public virtual bool SupportsAutomaticDecompression { get; set; } = true; //note: internal const SocketsHttpHandler.SupportsAutomaticDecompression qui vaut "true"

		public virtual bool SupportsProxy { get; set; } = true; //note: internal const SocketsHttpHandler.SupportsProxy qui vaut "true"

		public virtual bool SupportsRedirectConfiguration { get; set; } = true; //note: internal const SocketsHttpHandler.SupportsRedirectConfiguration qui vaut "true"

		public bool UseCookies
		{
			get => this.Sockets.UseCookies;
			set => this.Sockets.UseCookies = value;
		}

		public CookieContainer CookieContainer
		{
			get => this.Sockets.CookieContainer;
			set
			{
				ArgumentNullException.ThrowIfNull(value);

				this.Sockets.CookieContainer = value;
			}
		}

		public DecompressionMethods AutomaticDecompression
		{
			get => this.Sockets.AutomaticDecompression;
			set => this.Sockets.AutomaticDecompression = value;
		}

		public bool UseProxy
		{
			get => this.Sockets.UseProxy;
			set => this.Sockets.UseProxy = value;
		}

		public IWebProxy? Proxy
		{
			get => this.Sockets.Proxy;
			set => this.Sockets.Proxy = value;
		}

		public ICredentials? DefaultProxyCredentials
		{
			get => this.Sockets.DefaultProxyCredentials;
			set => this.Sockets.DefaultProxyCredentials = value;
		}

		public bool PreAuthenticate
		{
			get => this.Sockets.PreAuthenticate;
			set => this.Sockets.PreAuthenticate = value;
		}

		public bool UseDefaultCredentials
		{
			// SocketsHttpHandler doesn't have a separate UseDefaultCredentials property.  There
			// is just a Credentials property.  So, we need to map the behavior.
			get => this.Sockets.Credentials == CredentialCache.DefaultCredentials;
			set
			{
				if (value)
				{
					this.Sockets.Credentials = CredentialCache.DefaultCredentials;
				}
				else
				{
					if (this.Sockets.Credentials == CredentialCache.DefaultCredentials)
					{
						// Only clear out the Credentials property if it was a DefaultCredentials.
						this.Sockets.Credentials = null;
					}
				}
			}
		}

		public ICredentials? Credentials
		{
			get => this.Sockets.Credentials;
			set => this.Sockets.Credentials = value;
		}

		public bool AllowAutoRedirect
		{
			get => this.Sockets.AllowAutoRedirect;
			set => this.Sockets.AllowAutoRedirect = value;
		}

		public int MaxAutomaticRedirections
		{
			get => this.Sockets.MaxAutomaticRedirections;
			set => this.Sockets.MaxAutomaticRedirections = value;
		}

		public int MaxConnectionsPerServer
		{
			get => this.Sockets.MaxConnectionsPerServer;
			set => this.Sockets.MaxConnectionsPerServer = value;
		}

		public long MaxRequestContentBufferSize
		{
			// This property is not supported. In the .NET Framework it was only used when the handler needed to
			// automatically buffer the request content. That only happened if neither 'Content-Length' nor
			// 'Transfer-Encoding: chunked' request headers were specified. So, the handler thus needed to buffer
			// in the request content to determine its length and then would choose 'Content-Length' semantics when
			// POST'ing. In .NET Core, the handler will resolve the ambiguity by always choosing
			// 'Transfer-Encoding: chunked'. The handler will never automatically buffer in the request content.

			get => 0; // Returning zero is appropriate since in .NET Framework it means no limit.

			set
			{
				if (value < 0)
				{
					throw new ArgumentOutOfRangeException(nameof(value));
				}

				const long MAX_BUFFER_SIZE = int.MaxValue; //TODO: HttpContent.MaxBufferSize
				if (value > MAX_BUFFER_SIZE) 
				{
					throw new ArgumentOutOfRangeException(nameof(value), value, $"Buffering more than {MAX_BUFFER_SIZE:N0} bytes is not supported.");
				}

				CheckDisposed();

				// No-op on property setter.
			}
		}

		public int MaxResponseHeadersLength
		{
			get => this.Sockets.MaxResponseHeadersLength;
			set => this.Sockets.MaxResponseHeadersLength = value;
		}

		public ClientCertificateOption ClientCertificateOptions
		{
			get => m_clientCertificateOptions;
			set
			{
				switch (value)
				{
					case ClientCertificateOption.Manual:
					{
						ThrowForModifiedManagedSslOptionsIfStarted();
						m_clientCertificateOptions = value;
						this.Sockets.SslOptions.LocalCertificateSelectionCallback = (_, _, _, _, _) => CertHelpers.GetEligibleClientCertificate(this.Sockets.SslOptions.ClientCertificates)!;
						break;
					}
					case ClientCertificateOption.Automatic:
					{
						ThrowForModifiedManagedSslOptionsIfStarted();
						m_clientCertificateOptions = value;
						this.Sockets.SslOptions.LocalCertificateSelectionCallback = (_, _, _, _, _) => CertHelpers.GetEligibleClientCertificate()!;
						break;
					}
					default:
					{
						throw new ArgumentOutOfRangeException(nameof(value));
					}
				}
			}
		}

		private static class CertHelpers
		{
			private const string ClientAuthenticationOid = "1.3.6.1.5.5.7.3.2";

			internal static X509Certificate2? GetEligibleClientCertificate()
			{
				// Get initial list of client certificates from the MY store.
				X509Certificate2Collection candidateCerts;
				using (var myStore = new X509Store(StoreName.My, StoreLocation.CurrentUser))
				{
					myStore.Open(OpenFlags.OpenExistingOnly | OpenFlags.ReadOnly);
					candidateCerts = myStore.Certificates;
				}

				return GetEligibleClientCertificate(candidateCerts);
			}

			internal static X509Certificate2? GetEligibleClientCertificate(X509CertificateCollection? candidateCerts)
			{
				if (candidateCerts == null || candidateCerts.Count == 0)
				{
					return null;
				}

				var certs = new X509Certificate2Collection();
				certs.AddRange(candidateCerts);

				return GetEligibleClientCertificate(certs);
			}

			private static X509Certificate2? GetEligibleClientCertificate(X509Certificate2Collection? candidateCerts)
			{
				if (candidateCerts == null || candidateCerts.Count == 0)
				{
					return null;
				}

				foreach (X509Certificate2 cert in candidateCerts)
				{
					if (!cert.HasPrivateKey)
					{
						continue;
					}

					if (IsValidClientCertificate(cert))
					{
						return cert;
					}
				}

				return null;
			}

			private static bool IsValidClientCertificate(X509Certificate2 cert)
			{
				foreach (X509Extension extension in cert.Extensions)
				{
					switch (extension)
					{
						case X509EnhancedKeyUsageExtension eku when !IsValidForClientAuthenticationEku(eku):
						case X509KeyUsageExtension ku when !IsValidForDigitalSignatureUsage(ku):
						{
							return false;
						}
					}
				}
				return true;
			}

			private static bool IsValidForClientAuthenticationEku(X509EnhancedKeyUsageExtension eku)
			{
				foreach (Oid oid in eku.EnhancedKeyUsages)
				{
					if (oid.Value == CertHelpers.ClientAuthenticationOid)
					{
						return true;
					}
				}

				return false;
			}

			private static bool IsValidForDigitalSignatureUsage(X509KeyUsageExtension ku)
			{
				return (ku.KeyUsages & X509KeyUsageFlags.DigitalSignature) == X509KeyUsageFlags.DigitalSignature;
			}
		}

		public X509CertificateCollection ClientCertificates
		{
			get
			{
				if (ClientCertificateOptions != ClientCertificateOption.Manual)
				{
					throw new InvalidOperationException($"The {nameof(ClientCertificateOptions)} property must be set to '{nameof(ClientCertificateOption.Manual)}' to use this property.");
				}

				return this.Sockets.SslOptions.ClientCertificates ??= new X509CertificateCollection();
			}
		}

		public Func<HttpRequestMessage, X509Certificate2?, X509Chain?, SslPolicyErrors, bool>? ServerCertificateCustomValidationCallback
		{
			get => m_serverCustomValidationCallback;
			set
			{
				ThrowForModifiedManagedSslOptionsIfStarted();
				m_serverCustomValidationCallback = value;
				this.Sockets.SslOptions.RemoteCertificateValidationCallback = value != null ? CheatCodes.CallbackMapperInvoker(value) : null;
			}
		}

		public bool CheckCertificateRevocationList
		{
			get => this.Sockets.SslOptions.CertificateRevocationCheckMode == X509RevocationMode.Online;
			set
			{
				ThrowForModifiedManagedSslOptionsIfStarted();
				this.Sockets.SslOptions.CertificateRevocationCheckMode = value ? X509RevocationMode.Online : X509RevocationMode.NoCheck;
			}
		}

		public SslProtocols SslProtocols
		{
			get => this.Sockets.SslOptions.EnabledSslProtocols;
			set
			{
				ThrowForModifiedManagedSslOptionsIfStarted();
				this.Sockets.SslOptions.EnabledSslProtocols = value;
			}
		}

		public IDictionary<string, object?> Properties => this.Sockets.Properties;

		protected override HttpResponseMessage Send(HttpRequestMessage request, CancellationToken ct) => CheatCodes.SendInvoker(Sockets, request, ct);

		protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) => CheatCodes.SendAsyncInvoker(Sockets, request, ct);

		/// <summary>Version "publique" de <see cref="HttpMessageHandler.Send"/> qui est normalement protected internal</summary>
		public HttpResponseMessage InvokeSend(HttpRequestMessage request, CancellationToken ct) => this.Send(request, ct);

		/// <summary>Version "publique" de <see cref="HttpMessageHandler.SendAsync"/> qui est normalement protected internal</summary>
		public Task<HttpResponseMessage> InvokeSendAsync(HttpRequestMessage request, CancellationToken ct) => this.SendAsync(request, ct);

		// lazy-load the validator func so it can be trimmed by the ILLinker if it isn't used.
		public static readonly Func<HttpRequestMessage, X509Certificate2?, X509Chain?, SslPolicyErrors, bool> DangerousAcceptAnyServerCertificateValidator = (_,_,_,_) => true;

		private void ThrowForModifiedManagedSslOptionsIfStarted()
		{
			// Hack to trigger an InvalidOperationException if a property that's stored on
			// SslOptions is changed, since SslOptions itself does not do any such checks.
			this.Sockets.SslOptions = this.Sockets.SslOptions;
		}

		private void CheckDisposed()
		{
			if (m_disposed)
			{
				throw new ObjectDisposedException(GetType().ToString());
			}
		}

		private Socket CreateSocket()
		{
			var socket = new Socket(SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };
			//TODO: custom settings?
			return socket;
		}

		private static IPAddress? GetLocalEndpointAddress(Socket socket)
		{
			var ip = socket.LocalEndPoint is IPEndPoint ipep ? ipep.Address : null;
			if (ip == null) return null;
			//note: si on a "::ffff:192.168.1.23" on veut ressortir 192.168.1.23 directement!
			if (ip.IsIPv4MappedToIPv6)
			{
				ip = ip.MapToIPv4();
			}
			return ip;
		}

		public ValueTask<Stream> SocketsConnectCallback(SocketsHttpConnectionContext context, CancellationToken ct)
		{
			if (ct.IsCancellationRequested) return new ValueTask<Stream>(Task.FromCanceled<Stream>(ct));

			// grab the BetterHttpClientContext (if there is one)
			context.InitialRequestMessage.Options.TryGetValue(BetterHttpClient.OptionKey, out var clientContext);

			if (IPAddress.TryParse(context.DnsEndPoint.Host, out var address))
			{ // c'est déja une IP!
				return ConnectToSingleAsync(clientContext, new IPEndPoint(address, context.DnsEndPoint.Port), context.DnsEndPoint.Host, ct);
			}

			// c'est un nom DNS!
			return ConnectToHostNameAsync(clientContext, context.DnsEndPoint, ct);
		}

		private async ValueTask<Stream> ConnectToSingleAsync(BetterHttpClientContext? context, IPEndPoint endpoint, string? hostName, CancellationToken ct)
		{
			context?.SetStage(BetterHttpClientStage.Connecting);
			var socket = CreateSocket();
			var sw = Stopwatch.StartNew();
			try
			{
				await socket.ConnectAsync(endpoint, ct).ConfigureAwait(false);
				sw.Stop();
				this.Network.RecordEndpointConnectionAttempt(endpoint, sw.Elapsed, null, hostName, GetLocalEndpointAddress(socket));
				context?.Client.Options.Hooks?.OnSocketConnected(context, socket);
				return new NetworkStream(socket);
			}
			catch (Exception e)
			{
				sw.Stop();
				this.Network.RecordEndpointConnectionAttempt(endpoint, sw.Elapsed, e, hostName, localAddress: null);
				context?.Client.Options.Hooks?.OnSocketFailed(context, socket, e);
				socket.Dispose();
				throw;
			}
		}

		private async Task<Socket> AttemptConnectAsync(IPEndPoint ep, CancellationToken ct)
		{
			var socket = CreateSocket();
			try
			{
				await socket.ConnectAsync(ep, ct).ConfigureAwait(false);
				return socket;
			}
			catch (Exception)
			{
				socket.Dispose();
				throw;
			}
		}

		private async ValueTask<Stream> ConnectToHostNameAsync(BetterHttpClientContext? clientContext, DnsEndPoint endpoint, CancellationToken ct)
		{
			var entries = await this.Network.DnsLookup(endpoint.Host, endpoint.AddressFamily, ct).ConfigureAwait(false);

			var addresses = entries.AddressList;
			if (addresses.Length == 0)
			{
				throw new SocketException(11001); // "No such host is known"
			}

			if (addresses.Length == 1)
			{ // only a single IP address, no need to perform any load balacing
				return await ConnectToSingleAsync(clientContext, new IPEndPoint(addresses[0], endpoint.Port), endpoint.Host, ct).ConfigureAwait(false);
			}

			// multiple candidates! we will test them in parallel, using any previous attempt as a hint for ordering
			// - on the first call, we use the order returned by the DNS server, with IPv4 preferred over IPv6
			// - on following calls, we use the "quality" to compute a score (hosts that responded faster before will have a better score, timeouts will have a penalty...)

			var endpoints = new IPEndPoint[addresses.Length];
			var scores = new double[endpoints.Length];
			var now = this.Network.Clock.GetCurrentInstant();
			for(var i = 0; i < endpoints.Length; i++)
			{
				var ep = new IPEndPoint(addresses[i], endpoint.Port);
				endpoints[i] = ep;
				var quality = this.Network.GetEndpointQuality(ep);
				scores[i] = quality?.ComputeScore(now) ?? (ep.AddressFamily == AddressFamily.InterNetworkV6 ? 4d : 2d);
			}
			Array.Sort(scores, endpoints);
#if FULL_DEBUG
			Debug.WriteLine("Will try to connect (in order) to: " + string.Join(", ", endpoints.Select((x, i) => $"{i}) {x} @ {scores[i]}")));
#endif
			return await ConnectToMultipleAsync(clientContext, endpoints, endpoint.Host, ct).ConfigureAwait(false);
		}

		private async ValueTask<Stream> ConnectToMultipleAsync(BetterHttpClientContext? clientContext, IPEndPoint[] endpoints, string? hostName, CancellationToken ct)
		{
			Socket? theOneTrueSocket = null;

			//note: le "connection timeout" global est géré au niveau du SocketsHttpHandler et c'est 'ct' qui va trigger s'il se déclenche!
			var staggerTimeout = TimeSpan.FromMilliseconds(500);

			var attempts = new (IPEndPoint? Endpoint, TimeSpan Started, Task<Socket>? Task)[endpoints.Length];
			var tasks = new List<Task>();
			ExceptionDispatchInfo? error = null;

			var sw = Stopwatch.StartNew();

			// Le but est de tester la connection 

			bool aborted = false;

			try
			{
				using (var cts = CancellationTokenSource.CreateLinkedTokenSource(ct))
				{
					var go = cts.Token;

					int index = 0;

					clientContext?.SetStage(BetterHttpClientStage.Connecting);

					while (theOneTrueSocket == null && !go.IsCancellationRequested)
					{
						if (index < endpoints.Length)
						{ // we can spin another one!
							var ep = endpoints[index];
#if FULL_DEBUG
							Debug.WriteLine($"[{sw.Elapsed.TotalMilliseconds:N1} ms] Attempting connection to {ep}...");
#endif

							var now = sw.Elapsed;
							var attempt = AttemptConnectAsync(ep, go);
							attempts[index] = (ep, now, attempt);
							++index;
						}

						// reset la liste des tasks en attente
						tasks.Clear();
						Task? timeout = null;
						if (index < endpoints.Length)
						{ // il y a encore des candidats derrière...
							timeout = Task.Delay(staggerTimeout, go);
							tasks.Add(timeout);
						}

						foreach (var x in attempts)
						{
							if (x.Task != null)
							{
								tasks.Add(x.Task);
							}
						}

						if (tasks.Count == 0)
						{ // nothing more to do?
							break;
						}

						{ // attente de la prochaine activité (timeout ou task socket qui se termine...)
							var t = await Task.WhenAny(tasks).ConfigureAwait(false);
							if (t == timeout)
							{ // not yet, start another one!
#if FULL_DEBUG
								Debug.WriteLine($"[{sw.Elapsed.TotalMilliseconds:N1} ms] No response yet? maybe start another one!");
#endif
								continue;
							}
						}

						// inspect the results looking for a completed connction...
						for (var i = 0; i < attempts.Length; i++)
						{
							var ts = attempts[i].Task;
							if (ts == null) continue;
							if (ts.IsCompleted)
							{
								attempts[i].Task = null;
								try
								{
									theOneTrueSocket = await ts.ConfigureAwait(false);
									var completed = sw.Elapsed;
									this.Network.RecordEndpointConnectionAttempt(attempts[i].Endpoint!, completed - attempts[i].Started, null, hostName, GetLocalEndpointAddress(theOneTrueSocket));
#if FULL_DEBUG
									Debug.WriteLine($"[{sw.Elapsed.TotalMilliseconds:N1} ms] {attempts[i].Endpoint} connected succesffully after {(completed - attempts[i].Started).TotalMilliseconds:N1} ms !");
#endif
									// exit early
									break;
								}
								catch (Exception e)
								{
									var completed = sw.Elapsed;
									this.Network.RecordEndpointConnectionAttempt(attempts[i].Endpoint!, completed - attempts[i].Started, e, hostName, null);
#if FULL_DEBUG
									Debug.WriteLine($"[{sw.Elapsed.TotalMilliseconds:N1} ms] {attempts[i].Endpoint} has failed after {(completed - attempts[i].Started).TotalMilliseconds:N1} ms: [{e.GetType().Name}] {e.Message}");
#endif
									if (error == null && e is not OperationCanceledException)
									{
										error = ExceptionDispatchInfo.Capture(e);
									}
									// continue!
								}
							}
						}
					}

					await cts.CancelAsync().ConfigureAwait(false);
				}
			}
			catch (Exception e)
			{
				error ??= ExceptionDispatchInfo.Capture(e);
				aborted = true;
			}
			finally
			{
				// if we are cancelled we need to get rid of our socket, even if we have one :(
				aborted |= ct.IsCancellationRequested;

				if (aborted && theOneTrueSocket != null)
				{
					theOneTrueSocket.Dispose();
					theOneTrueSocket = null;
				}

				// s'il y a encore des taches pending, il faut les finir!
				foreach (var attempt in attempts)
				{
					if (attempt.Endpoint == null || attempt.Task == null) continue;

					// all pending tasks should complete since we have killed their cancellation token...
					try
					{
						var socket = await attempt.Task.ConfigureAwait(false);
						var completed = sw.Elapsed;
						// too late! but we will still record it has a success
						this.Network.RecordEndpointConnectionAttempt(attempt.Endpoint, completed - attempt.Started, null, hostName, GetLocalEndpointAddress(socket));
						// get rid of it!
						socket.Dispose();
#if FULL_DEBUG
						Debug.WriteLine($"[{sw.Elapsed.TotalMilliseconds:N1} ms] draining {attempt.Endpoint} started at {attempt.Started.TotalMilliseconds:N1} ms");
#endif
					}
					catch (Exception e)
					{
						var completed = sw.Elapsed;
						this.Network.RecordEndpointConnectionAttempt(attempt.Endpoint, completed - attempt.Started, e, hostName, null);
#if FULL_DEBUG
						Debug.WriteLine($"[{sw.Elapsed.TotalMilliseconds:N1} ms] drained {attempt.Endpoint} started at {attempt.Started.TotalMilliseconds:N1} ms: [{e.GetType().Name}] {e.Message}");
#endif
						if (error == null && e is not OperationCanceledException)
						{
							error = ExceptionDispatchInfo.Capture(e);
						}
					}
				}
			}

			if (aborted || error != null)
			{
				// rethrow first error we got
				error?.Throw();

				// or maybe we got cancelled?
				if (ct.IsCancellationRequested) ct.ThrowIfCancellationRequested();
				
				throw new InvalidOperationException("This should not happen!");
			}

			Contract.Debug.Assert(theOneTrueSocket != null);

			clientContext?.Client.Options.Hooks?.OnSocketConnected(clientContext, theOneTrueSocket);

			return new NetworkStream(theOneTrueSocket, ownsSocket: true);
		}

		internal void Setup(BetterHttpClientOptions options)
		{
			this.Sockets.Properties[nameof(BetterHttpClientOptions)] = options;
			this.Sockets.ConnectCallback = this.SocketsConnectCallback;
		}

	}

}
