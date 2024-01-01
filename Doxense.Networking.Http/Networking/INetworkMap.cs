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

namespace Doxense.Networking
{
	using System;
	using System.Collections.Generic;
	using System.Net;
	using System.Net.Http;
	using System.Net.NetworkInformation;
	using System.Net.Sockets;
	using System.Threading;
	using System.Threading.Tasks;
	using Doxense.Networking.Http;
	using NodaTime;

	/// <summary>Interface utilisée pour pouvoir gérer réel ou simulé</summary>
	public interface INetworkMap
	{

		IClock Clock { get; }

		[Obsolete("Use CreateBetterHttpHandler instead")]
		HttpMessageHandler? CreateHttpHandler(string hostOrAddress, int port);

		HttpMessageHandler CreateBetterHttpHandler(Uri baseAddress, BetterHttpClientOptions options);

		ValueTask<IPAddress?> GetPublicIPAddressForHost(string hostNameOrAddress);

		Task<IPHostEntry> DnsLookup(string hostNameOrAddress, AddressFamily? family, CancellationToken ct);

		IReadOnlyList<NetworkAdaptorDescriptor> GetNetworkAdaptors();

		/// <summary>Query metadata about the "quality" of an endpoint</summary>
		/// <return>Quality of the endpoint, or null if no connection attempts has been performed yet</return>
		EndPointQuality? GetEndpointQuality(IPEndPoint endpoint);

		/// <summary>Record a connection attempt to a specific endpoint</summary>
		/// <param name="endpoint">Target endpoint of the connection</param>
		/// <param name="duration">Duration of the connection attempt</param>
		/// <param name="error">Error encountered while attempting the connection, or <c>null</c> if it was a success</param>
		/// <param name="hostName"></param>
		/// <param name="localAddress">Local address used to connect to this host (if known)</param>
		/// <returns>Updated quality of the endpoint</returns>
		EndPointQuality RecordEndpointConnectionAttempt(IPEndPoint endpoint, TimeSpan duration, Exception? error, string? hostName, IPAddress? localAddress);

		/// <summary>Try to get details about which endpoint and local address was used to talk to the specific host</summary>
		bool TryGetLastEndpointForHost(string hostName, out (IPEndPoint? EndPoint, IPAddress? LocalAddress, Instant Timestamp) infos);

	}

	public sealed record NetworkAdaptorDescriptor
	{
		public required string Id { get; init; }

		public required int Index { get; init; }

		public required NetworkInterfaceType Type { get; init; }

		public required string Name { get; init; }

		public required string Description { get; init; }

		public string? PhysicalAddress { get; init; }

		public string? DnsSuffix { get; init; }

		public required UnicastAddressDescriptor[] UnicastAddresses { get; init; }

		public long? Speed { get; init; }

		public sealed record UnicastAddressDescriptor
		{

			public required IPAddress Address { get; init; }

			public IPAddress? IPv4Mask { get; init; }

			public int PrefixLength { get; init; }

		}

	}

	public interface IVirtualNetworkTopology
	{

		IVirtualNetworkLocation RegisterLocation(string id, string name, VirtualNetworkType type, VirtualNetworkLocationOptions options);

		IVirtualNetworkLocation GetLocation(string id);

		IVirtualNetworkHost GetHost(string id);

		Task<IPHostEntry> DnsResolve(string hostNameOrAddress, IVirtualNetworkHost? sourceHost, AddressFamily? family, CancellationToken ct);

		string Dump();

	}

	/// <summary>Represents the view of the network, relative to a specific host</summary>
	public interface IVirtualNetworkMap : INetworkMap
	{
		IVirtualNetworkTopology Topology { get; }

		IVirtualNetworkHost Host { get; }

	}

	public record VirtualNetworkLocationOptions
	{
		public string? IpRange { get; set; }
		//TODO: comment modeliser des range ip?

		public string? DnsSuffix { get; set; }

		public bool AllowsIncoming { get; set; } = true;

		public bool AutoDhcp { get; set; } = true;

	}

	public enum VirtualNetworkType
	{
		Unspecified = 0,
		/// <summary>Localhost (127.0.0.0/8)</summary>
		Loopback,
		/// <summary>Local Area Network</summary>
		LocalNetwork,
		/// <summary>Servers in a private datacenter or private cloud</summary>
		DataCenter,
		/// <summary>Servers in a public cloud</summary>
		Cloud,
	}

	public sealed record VirtualHostIdentity
	{

		public string? HostName { get; set; }

		public string? DnsSuffix { get; set; }

		public string? Fqdn { get; set; }

		public List<string> Aliases { get; set; } = new List<string>();

		public List<IPAddress> Addresses { get; set; } = new List<IPAddress>();

		/// <summary>If <c>true</c>, this is an actual physical device on the network, and requests will be sent over the network. If <c>false</c>, this is a virtualized host.</summary>
		public bool PassthroughToPhysicalNetwork { get; set; }

	}

	/// <summary>Représente un "LAN" où tous les hosts se voient entre eux directement</summary>
	public interface IVirtualNetworkLocation : IEquatable<IVirtualNetworkLocation>
	{

		IVirtualNetworkTopology Topology { get; }

		string Id { get; }

		string Name { get; }

		VirtualNetworkType Type { get; }

		VirtualNetworkLocationOptions Options { get; }

		IVirtualNetworkMap RegisterHost(string id, VirtualHostIdentity identity);

		IVirtualNetworkHost? GetHost(string id);

		void RegisterIpAddress(IPAddress address);

		IPAddress AllocateIpAddress();

		/// <summary>Generate a new virtual MAC address</summary>
		/// <param name="ouiPrefix">OUI prefix of this vender (ex: <c>'12:34:56'</c>)</param>
		/// <param name="seed">Deterministic seed, or null to generate a truly random address</param>
		/// <returns>Completed MAC address (ex: <c>'12:34:56:A2:79:3F'</c>)</returns>
		string AllocateMacAddress(string ouiPrefix, string? seed = null);

		/// <summary>Generate a new virtual Serial Number from a pattern</summary>
		/// <param name="pattern">Pattern where '#' are replaced with digits and '?' are replaced with uppercase letters (ex: 'ABC###??#####Z')</param>
		/// <param name="seed">Deterministic seed, or null to generate a truly random address</param>
		/// <returns>Serial number (ex: (ex: 'ABC666XY42069Z')</returns>
		string AllocateSerialNumber(string pattern, string? seed = null);

		/// <summary>Test if this virtual network can send packets to a <paramref name="target"/> network</summary>
		bool CanSendTo(IVirtualNetworkLocation target);

		/// <summary>Test if this virtual network can receive packets from a <paramref name="source"/> network</summary>
		bool CanReceiveFrom(IVirtualNetworkLocation source);

		/// <summary>Register a new service available in this virtual network</summary>
		/// <param name="serviceType">Type of service (ex: "endpoint:wes")</param>
		/// <param name="componentId">Id of the test component that handles this service (ex: "SMITH")</param>
		/// <param name="argument">Optional argument that depends on the type of service (ex: "agents:SMITH:wes:uri")</param>
		void RegisterNetworkService(string serviceType, string componentId, string? argument);

		/// <summary>Return of list of hosts that support a given service type in this virtual network</summary>
		/// <param name="serviceType">Type of service (ex: "endpoint:wes")</param>
		/// <returns>List of matching hosts, or an empty array if none were found</returns>
		(string Id, string? Argument)[] BrowseNetworkService(string serviceType);

		IVirtualNetworkHost AddHostPassthrough(string id, VirtualHostIdentity identity);

	}

	public interface IVirtualNetworkHost : IEquatable<IVirtualNetworkHost>
	{

		IReadOnlyList<IVirtualNetworkAdapter> Adapters { get; }

		IReadOnlyList<IVirtualNetworkLocation> Locations { get; }

		IVirtualNetworkLocation? Loopback { get; }

		string Id { get; }

		/// <summary>Nom de domaine complet (fqdn) du host (ex: pc123.domain.local)</summary>
		string Fqdn { get; }

		/// <summary>Nom principal du host, excluant le domaine (ex: PC123)</summary>
		string HostName { get; }

		/// <summary>Noms aditionnels optionnels du host (exclant le nom d'host et son fqdn)</summary>
		string[] Aliases { get; }

		/// <summary>Static ip address (optionnel)</summary>
		/// <remarks>Par convention, on utilise des IP dans la range 83.73.77.0/24 pour les simulated virtual devices</remarks>
		IPAddress[] Addresses { get; }

		void Bind(IVirtualNetworkLocation location, int port, Func<HttpMessageHandler> handler);

		Func<HttpMessageHandler>? FindHandler(IVirtualNetworkLocation location, int port);

	}

	public interface IVirtualNetworkAdapter
	{

		IVirtualNetworkLocation Location { get; }

		string Id { get; }

		int Index { get; }

		NetworkInterfaceType Type { get; }

		string Name { get; }

		string Description { get; }

		(IPAddress Address, IPAddress Mask, int PrefixLength)[] UnicastAddresses { get; }

	}

	public static class NetworkMapExtensions
	{

		public static IVirtualNetworkLocation RegisterLoopbackNetwork(this IVirtualNetworkTopology topology, string id, string name)
		{
			return topology.RegisterLocation(
				id,
				name,
				VirtualNetworkType.LocalNetwork,
				new VirtualNetworkLocationOptions()
				{
					AllowsIncoming = false,
					IpRange = "127.0.0.0/8",
					DnsSuffix = ".localhost"
				}
			);
		}

	}

}
