﻿#region Copyright (c) 2023-2024 SnowBank SAS, (c) 2005-2023 Doxense SAS
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

namespace Doxense.Networking
{
	using System;
	using System.Net;
	using System.Net.Http;
	using System.Net.NetworkInformation;
	using System.Net.Sockets;
	using Doxense.Networking.Http;

	/// <summary>Implementation of an <see cref="INetworkMap"/> that interacts a real network</summary>
	/// <remarks>This method will passthough all requests to the actual network stack of the host.</remarks>
	[PublicAPI]
	public class NetworkMap : INetworkMap
	{

		public NetworkMap(IClock? clock = null)
		{
			this.Clock = clock ?? SystemClock.Instance;
		}

		/// <inheritdoc />
		public IClock Clock { get; }

		[Obsolete("Use CreateBetterHttpHandler instead")]
		public virtual HttpMessageHandler? CreateHttpHandler(string hostOrAddress, int port)
		{
			//by default, let the caller use its own handler
			return null;
		}

		protected virtual HttpMessageHandler CreateDefaultHttpHandler(Uri baseAddress, BetterHttpClientOptions options)
		{
			return options.Configure(new BetterHttpClientHandler(this));
		}

		/// <inheritdoc />
		public virtual HttpMessageHandler CreateBetterHttpHandler(Uri baseAddress, BetterHttpClientOptions options)
		{
			return CreateDefaultHttpHandler(baseAddress, options);
		}

		[Obsolete("C'est charge a l'appelant de résolver l'IP")]
		public virtual async ValueTask<IPAddress?> GetPublicIPAddressForHost(string hostNameOrAddress)
		{
			//REVIEW: on a déterminé que c'est plutot a l'appelant de déterminer par lui-même l'adresse IP s'il a un hostname
			// => le DNS peut ne pas resolver
			// => en DHCP ca peut changer !
			// => le device peut ne pas etre online au moment où on appele cette méthode!
			// => le wifi peut passer sous un tunnel, etc...

			var hostEntry = await Dns.GetHostEntryAsync(hostNameOrAddress);
			if (hostEntry.AddressList.Length == 0)
			{
				return null;
			}

			if (!IPAddressHelpers.TryGetLocalAddressForRemoteAddress(hostEntry.AddressList.First(ip => ip.AddressFamily == AddressFamily.InterNetwork), out var localAddress))
			{
				return null;
			}
			return localAddress;
		}

		/// <inheritdoc />
		public virtual Task<IPHostEntry> DnsLookup(string hostNameOrAddress, AddressFamily? family, CancellationToken ct)
		{
			return Dns.GetHostEntryAsync(hostNameOrAddress, family ?? AddressFamily.Unspecified, ct);
		}

		/// <inheritdoc />
		public virtual IReadOnlyList<NetworkAdaptorDescriptor> GetNetworkAdaptors()
		{
			return NetworkInterface.GetAllNetworkInterfaces().Where(IsValidNetworkInterface).Select(x =>
			{
				var props = x.GetIPProperties();
				return new NetworkAdaptorDescriptor
				{
					Id = x.Id,
					Index = props.GetIPv4Properties().Index,
					Type = x.NetworkInterfaceType,
					Name = x.Name,
					Description = x.Description,
					PhysicalAddress = x.GetPhysicalAddress().ToString(),
					DnsSuffix = props.DnsSuffix,
					UnicastAddresses = props.UnicastAddresses
						.Select(info => new NetworkAdaptorDescriptor.UnicastAddressDescriptor()
						{
							Address = info.Address,
							IPv4Mask = info.IPv4Mask,
							PrefixLength = info.PrefixLength,
						})
						.ToArray(),
					Speed = x.Speed > 0 ? x.Speed : null,
				};
			}).ToArray();

		}

		private readonly object Lock = new();

		protected Dictionary<IPEndPoint, EndPointQuality> HeatMap { get; } = new(IPEndPointComparer.Default);

		protected Dictionary<string, (string HostName, IPEndPoint RemoteEndPoint, IPAddress? Local, Instant Timestamp)> LastEndPointMapping { get; } = new (StringComparer.Ordinal);

		/// <inheritdoc />
		public EndPointQuality? GetEndpointQuality(IPEndPoint endpoint)
		{
			lock (this.Lock)
			{
				return this.HeatMap.GetValueOrDefault(endpoint);
			}
		}

		/// <inheritdoc />
		public EndPointQuality RecordEndpointConnectionAttempt(IPEndPoint endpoint, TimeSpan duration, Exception? error, string? hostName, IPAddress? localAddress)
		{
			var now = this.Clock.GetCurrentInstant();
			lock (this.Lock)
			{
				if (!this.HeatMap.TryGetValue(endpoint, out var quality))
				{
					quality = new EndPointQuality(endpoint, historySize: 5);
					this.HeatMap[endpoint] = quality;
				}
				quality.Record(now, duration, error, hostName, localAddress);

				if (error == null)
				{ // if success, maybe update the details about this hostname...
					if (hostName != null)
					{
						this.LastEndPointMapping[hostName] = (hostName, endpoint, localAddress, now);
					}
				}

				return quality;
			}
		}

		/// <inheritdoc />
		public bool TryGetLastEndpointForHost(string hostName, out (IPEndPoint? EndPoint, IPAddress? LocalAddress, Instant Timestamp) infos)
		{
			lock (this.Lock)
			{
				if (!this.LastEndPointMapping.TryGetValue(hostName, out var xx))
				{
					infos = default;
					return false;
				}

				infos = (xx.RemoteEndPoint, xx.Local, xx.Timestamp);
				return true;
			}
		}

		public bool IsValidNetworkInterface(NetworkInterface net)
		{
			if (net.NetworkInterfaceType == NetworkInterfaceType.Loopback)
			{
				//REVIEW: on ne retourne pas localhost car en prod ca n'a pas de sens... mais en mode dev peut etre ?
				return false;
			}

			if (!net.Supports(NetworkInterfaceComponent.IPv4))
			{
				return false;
			}

			if (net.OperationalStatus != OperationalStatus.Up)
			{
				return false;
			}

			return true;
		}

	}

	[DebuggerDisplay("EndPoint={EndPoint}, Score={Score}, Total={TotalConnections}, Errors={TotalErrors}")]
	[PublicAPI]
	public sealed class EndPointQuality
	{

		public EndPointQuality(IPEndPoint ep, int historySize)
		{
			Contract.NotNull(ep);
			Contract.GreaterThan(historySize, 0);

			this.EndPoint = ep;
			this.MovingDuration = new MovingSum<long>(historySize);
			this.MovingErrors = new MovingSum<double>(historySize);
		}

		public IPEndPoint EndPoint { get; }

		/// <summary>Last computed score for this endpoint (lower == better)</summary>
		public double Score { get; private set; }

		/// <summary>Total number of connection attempts</summary>
		public long TotalConnections { get; private set; }

		/// <summary>Total number of failed connection attempts</summary>
		public long TotalErrors { get; private set; }

		/// <summary>Moving sum of the last connection attempts</summary>
		private MovingSum<long> MovingDuration;

		/// <summary>Moving sum of the last connection attempts (between 0 and 1, can be interpreted as the probability that the connection attempts fails)</summary>
		private MovingSum<double> MovingErrors;

		/// <summary>Time of the last attempt</summary>
		public Instant LastAttempt { get; private set; }

		/// <summary>Time of the end of a mandatory cooldown</summary>
		/// <remarks>The end point will have a very high score handicap until the end of the cooldown</remarks>
		public Instant? Cooldown { get; private set; }

		/// <summary>Last known local address used to connect to this endpoint</summary>
		public IPAddress? LastLocalAddress { get; private set; }

		/// <summary>Error if the last connection failed, or <see langword="null"/> if it was successfull</summary>
		public Exception? LastError { get; private set; }

		/// <summary>Last hostname that resolved to this endpoint</summary>
		public string? LastHostName { get; private set; }

		/// <summary>Computes a score that attempts to quantify the likelyhood that is endpoint will be available and responsive, at the given time (lower == better)</summary>
		/// <param name="now">Time of the computation (may be in the future)</param>
		/// <returns>Score that is lower if the host is expected to be reachable and fast, or higher if it is less likely (failed to resolve or timed out in the recent past)</returns>
		public double ComputeScore(Instant now)
		{
			var score = this.Score;
			if (this.Cooldown != null)
			{
				if (now < this.Cooldown.Value)
				{
					score += (this.Cooldown.Value - now).TotalSeconds;
				}
				else
				{
					this.Cooldown = null;
				}
			}
			return score;
		}

		/// <summary>Average duration of recent connection attempts</summary>
		public TimeSpan AverageDuration => this.TotalConnections == 0 ? TimeSpan.Zero : TimeSpan.FromTicks(this.MovingDuration.Total / this.MovingDuration.Count);

		/// <summary>Error rate recent connection attempts (normalized 0..1)</summary>
		public double AverageErrors => this.TotalConnections == 0 ? 0 : (this.MovingErrors.Total / this.MovingErrors.Count);

		/// <summary>Duration of the last connection attempt</summary>
		public TimeSpan LastDuration => TimeSpan.FromTicks(this.MovingDuration.Last);

		/// <summary>Has the last connection attempt failed?</summary>
		public bool LastFailed => this.MovingDuration.Last > 0.0;

		/// <summary>Records a new connection attempt</summary>
		/// <param name="now">Time of the attempt (usually the when the connection was established, or the timeout expired)</param>
		/// <param name="duration">Duration of the connection attempt</param>
		/// <param name="error">Exception recorded if the attempt failed</param>
		/// <param name="hostName">Host name or ip address used for the connection attempt</param>
		/// <param name="localAddress">Address of the local network adapter used if known; otherwise, <see langword="null"/></param>
		public void Record(Instant now, TimeSpan duration, Exception? error, string? hostName, IPAddress? localAddress)
		{
			this.LastAttempt = now;
			this.TotalConnections++;
			if (error != null)
			{
				this.TotalErrors++;
			}

			var deltaError = error == null ? 0.0 : 1.0; //TODO: ajouter des scores plus graves suivant le type d'erreur?
			var movingError = this.MovingErrors.Add(deltaError);
			var deltaDuration = duration.Ticks;
			var movingDuration = this.MovingDuration.Add(deltaDuration);

			this.Score = (TimeSpan.FromTicks(deltaDuration + (movingDuration.Total / 2)).TotalSeconds / movingDuration.Samples) * Math.Pow(1 + deltaError + (movingError.Total / 2), 2);
			if (error != null)
			{
				this.LastError = error;
				this.Cooldown = now.Plus(Duration.FromMinutes(2)); //TODO: ajouter un cooldown différent suivant le type d'erreur?
			}
			else
			{
				this.LastError = null;
				this.Cooldown = null;
			}

			if (localAddress != null)
			{
				this.LastLocalAddress = localAddress;
			}

			if (hostName != null)
			{
				this.LastHostName = hostName;
			}
		}

	}

}
