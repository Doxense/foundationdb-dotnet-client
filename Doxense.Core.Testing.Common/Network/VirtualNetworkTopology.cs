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
	using System.Diagnostics;
	using System.Diagnostics.CodeAnalysis;
	using System.Linq;
	using System.Net;
	using System.Net.Http;
	using System.Net.NetworkInformation;
	using System.Net.Sockets;
	using System.Text;
	using System.Threading;
	using System.Threading.Tasks;
	using Doxense.Diagnostics.Contracts;
	using Doxense.IO.Hashing;
	using Doxense.Threading;

	public class VirtualNetworkTopology : IVirtualNetworkTopology
	{

		public sealed record SimulatedNetworkAdapter : IVirtualNetworkAdapter
		{

			public required SimulatedNetwork Location { get; init; }

			IVirtualNetworkLocation IVirtualNetworkAdapter.Location => this.Location;

			public required string Id { get; init; }

			public required NetworkInterfaceType Type { get; init; }

			public required string Name { get; init; }

			public required string Description { get; init; }

			public required (IPAddress Address, IPAddress Mask, int PrefixLength)[] UnicastAddresses { get; init; }

			public string? PhysicalAddress { get; init; }

		}

		// By convention:
		// - If hostname is an fqdns that ends in ".simulated", it is a virtual device
		// - If hostname is an IPv4 in the range 83.73.77.0/24 ("SIM*" in ascii), it is also a virtual device

		[DebuggerDisplay("{ToString(),nq}")]
		public sealed class SimulatedHost : IVirtualNetworkHost
		{
			/// <summary>Unique ID of this host in the global network</summary>
			public string Id { get; }

			public SimulatedNetworkAdapter[] Adapters { get; }
			IReadOnlyList<IVirtualNetworkAdapter> IVirtualNetworkHost.Adapters => this.Adapters;

			public SimulatedNetwork[] Locations { get; }
			IReadOnlyList<IVirtualNetworkLocation> IVirtualNetworkHost.Locations => this.Locations;

			public SimulatedNetwork? Loopback { get; }
			IVirtualNetworkLocation? IVirtualNetworkHost.Loopback => this.Loopback;

			/// <summary>Nom de domaine complet (fqdn) du host (ex: pc123.domain.local)</summary>
			public string Fqdn { get; }

			/// <summary>Nom principal du host, excluant le domaine (ex: PC123)</summary>
			public string HostName { get; }

			/// <summary>Noms aditionnels optionnels du host (exclant le nom d'host et son fqdn)</summary>
			public string[] Aliases { get; }

			/// <summary>Static ip address (optionnel)</summary>
			/// <remarks>Par convention, on utilise des IP dans la range 83.73.77.0/24 pour les simulated virtual devices</remarks>
			public IPAddress[] Addresses { get; }

			public bool Passthrough { get; }

			/// <summary>Flag that is <see langword="true"/> when this host is 'offline' and should not respond to any external request.</summary>
			public bool Offline { get; private set; }

			/// <summary>Map of all handlers attached to each network location</summary>
			/// <remarks>The key is the network location id, and the value is the map of the ports that are bound: <c>Location => (Port => Handler)</c></remarks>
			public Dictionary<string, Dictionary<int, Func<HttpMessageHandler>>> Handlers { get; } = new(StringComparer.Ordinal);

			/// <summary>Create a new simulated host</summary>
			/// <param name="adapters">List of the network adapters that are available for this host (note: must include at least one adapter for 'localhost')</param>
			/// <param name="id">Unique id of this host</param>
			/// <param name="hostName">Primary host name (ex: "pc042")</param>
			/// <param name="fqdn">Primary fully qualified domain name (ex: "pc042.acme.local")</param>
			/// <param name="aliases">Optional list of aliases (including short names or other fqdn)</param>
			/// <param name="addresses">List of IP addresses owned by this host</param>
			/// <param name="passthrough">If true, this host represents an actual physical host, accessible on the network by the testing framework, and all requests will be forwarded to this physical host (instead of being simulated).</param>
			/// <param name="offline">If true, this host starts in "offline" mode, and will not respond to requests.</param>
			public SimulatedHost(SimulatedNetworkAdapter[] adapters, string id, string hostName, string fqdn, string[] aliases, IPAddress[] addresses, bool passthrough, bool offline)
			{
				Contract.Debug.Requires(adapters != null && adapters.Length != 0 && id != null && hostName != null && fqdn != null && aliases != null && addresses != null);
				this.Adapters = adapters;
				this.Locations = adapters.Select(x => x.Location).ToArray();
				this.Loopback = adapters.SingleOrDefault(l => l.Location.Type == VirtualNetworkType.Loopback)?.Location;
				this.Id = id;
				this.HostName = hostName;
				this.Fqdn = fqdn;
				this.Aliases = aliases;
				this.Addresses = addresses;
				this.Passthrough = passthrough;
				this.Offline = offline;
			}

			/// <summary>Change the <see cref="Offline">offline state</see> of this host</summary>
			/// <param name="offline">Mark the host as offline if <see langword="true"/>, or back online if <see langword="false"/>.</param>
			/// <remarks>If the host is offline, all simulated requests will start to fail</remarks>
			public void SetOffline(bool offline)
			{
				//TODO: maybe add a parameter to specify which kind of "offline" fault the host should simulate: powered-down? ethernet is off? currently rebooting but not yet ready? some big crash?
				this.Offline = offline;
				//TODO: maybe have a way to "cancel" any in-flight requests to this host?
			}

			/// <summary>Bind a "virtual socket" on the specified port</summary>
			/// <param name="location">Network location (ie: network adapter) on which to bind this handler</param>
			/// <param name="port">Port of the virtual socket</param>
			/// <param name="handler">Handler that will be called on each connection attempt on this port. It should return an HTTP handler that will process (or fail) the request.</param>
			/// <exception cref="InvalidOperationException">If the host is not able to bind virtual socket (ex: passthrough host)</exception>
			public void Bind(IVirtualNetworkLocation location, int port, Func<HttpMessageHandler> handler)
			{
				if (this.Passthrough) throw new InvalidOperationException("Cannot bind ports to passthrough hosts!");

				lock (this.Handlers)
				{
					if (!this.Handlers.TryGetValue(location.Id, out var ports))
					{
						ports = new Dictionary<int, Func<HttpMessageHandler>>();
						this.Handlers[location.Id] = ports;
					}

					ports.Add(port, handler);
				}
			}

			/// <summary>Find a message handler that is bound to the specified port</summary>
			/// <param name="location">Network location (ie: network adapter) from which the request is coming</param>
			/// <param name="port">Port of the connection attempt</param>
			/// <returns>If the port is bound, return a new HTTP message handler that will process request. If the port is unassigned, it will return <see langword="null"/>.</returns>
			public Func<HttpMessageHandler>? FindHandler(IVirtualNetworkLocation location, int port)
			{
				// si c'est un vrai host, on n'a pas de handler custom
				if (this.Passthrough) return null;

				lock (this.Handlers)
				{
					if (!this.Handlers.TryGetValue(location.Id, out var ports)) return null;
					// is there an exact match for this host?
					if (ports.TryGetValue(port, out var handler)) return handler;
					// if port 0 is defined, it captures all ports for this host
					if (port != 0 && ports.TryGetValue(0, out handler)) return handler;
					return null;
				}
			}

			/// <summary>Return all the variations of host names, fqdn and aliases that this host responds to.</summary>
			public IEnumerable<string> GetHostKeys()
			{
				if (!string.IsNullOrEmpty(this.Fqdn))
				{
					yield return this.Fqdn;
				}

				if (!string.IsNullOrEmpty(this.HostName) && !string.Equals(this.Fqdn, this.HostName, StringComparison.OrdinalIgnoreCase))
				{
					yield return this.HostName;
				}

				foreach (var name in this.Aliases)
				{
					if (name != null!) yield return name;
				}
				foreach (var addr in this.Addresses)
				{
					if (addr != null!) yield return addr.ToString();
				}
			}

			public override string ToString()
			{
				return $"Host<{this.Id}>(Fqdn={this.Fqdn}, IP={string.Join<IPAddress>(", ", this.Addresses)}, Aliases={string.Join<string>(", ", this.Aliases)})";
			}

			public override bool Equals(object? obj) => obj is IVirtualNetworkHost host && Equals(host);

			public bool Equals(IVirtualNetworkHost? other) => ReferenceEquals(other, this) || (!ReferenceEquals(other, null) && other.Id == this.Id);

			public override int GetHashCode() => this.Id.GetHashCode();

		}

		public Dictionary<string, SimulatedHost> HostsById { get; } = new Dictionary<string, SimulatedHost>(StringComparer.Ordinal);

		public Dictionary<string, string> HostsByNameOrAddress { get; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

		[DebuggerDisplay("Id={Id}, Name={Name}, Type={Type}, IpRange={Options.IpRange}, DnsSuffix={Options.DnsSuffix}")]
		public sealed class SimulatedNetwork : IVirtualNetworkLocation
		{

			public SimulatedNetwork(VirtualNetworkTopology topology, string id, string name, VirtualNetworkType type, VirtualNetworkLocationOptions options)
			{
				Contract.Debug.Requires(id != null && name != null && options != null);
				this.Id = id;
				this.Name = name;
				this.Type = type;
				this.Options = options;
				this.Topology = topology;

				if (type != VirtualNetworkType.Loopback && options.IpRange != null && options.AutoDhcp)
				{
					IPAddressHelpers.DecodeIPRange(options.IpRange, out var first, out var last);
					this.DhcpAddressFirst = first;
					this.DhcpAddressLast = last;
					this.DhcpAddressNextFree = first;
				}
			}

			public string Id { get; }

			public string Name { get; }

			public VirtualNetworkType Type { get; }

			public VirtualNetworkLocationOptions Options { get; }

			public VirtualNetworkTopology Topology { get; }
			IVirtualNetworkTopology IVirtualNetworkLocation.Topology => this.Topology;

			/// <summary>Host présent dans cet emplacement</summary>
			public Dictionary<string, SimulatedHost> HostsById { get; } = new Dictionary<string, SimulatedHost>(StringComparer.OrdinalIgnoreCase);
			public Dictionary<string, string> HostsByNameOrAddress { get; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

			public Dictionary<string, List<(string Id, string? Argument)>> NetworkServices { get; } = new Dictionary<string, List<(string, string?)>>(StringComparer.Ordinal);

			IVirtualNetworkMap IVirtualNetworkLocation.RegisterHost(string id, VirtualHostIdentity identity) => RegisterHost(id, identity);

			public VirtualNetworkMap RegisterHost(string id, VirtualHostIdentity identity)
			{
				var host = this.Topology.RegisterHost(this, id, identity);
				this.HostsById.Add(id, host);
				foreach (var key in host.GetHostKeys())
				{
					this.HostsByNameOrAddress.Add(key, id);
				}
				return new VirtualNetworkMap(this.Topology, host);
			}

			IVirtualNetworkHost IVirtualNetworkLocation.AddHostPassthrough(string id, VirtualHostIdentity identity) => AddHostPassthrough(id, identity);

			public SimulatedHost AddHostPassthrough(string id, VirtualHostIdentity identity)
			{
				identity.PassthroughToPhysicalNetwork = true;
				return this.Topology.RegisterHost(this, id, identity);
			}

			public IVirtualNetworkHost? GetHost(string id)
			{
				return this.HostsById.GetValueOrDefault(id);
			}

			public bool CanSendTo(IVirtualNetworkLocation target)
			{
				if (this.Equals(target)) return true;
				//TODO: do we have a valid gateway, or are we isolated from the rest of the world?
				return true; // par défaut c'est ouvert!
			}

			public bool CanReceiveFrom(IVirtualNetworkLocation source)
			{
				if (this.Equals(source)) return true;
				return this.Options.AllowsIncoming;
			}

			public void RegisterNetworkService(string serviceType, string componentId, string? argument)
			{
				Contract.NotNullOrEmpty(serviceType);
				Contract.NotNullOrEmpty(componentId);

				if (!this.NetworkServices.TryGetValue(serviceType, out var services))
				{
					services = new List<(string, string?)>();
					this.NetworkServices[serviceType] = services;
				}
				services.Add((componentId, argument));
			}

			public (string Id, string? Argument)[] BrowseNetworkService(string serviceType)
			{
				if (!this.NetworkServices.TryGetValue(serviceType, out var services))
				{
					return Array.Empty<(string, string?)>();
				}
				return services.ToArray();
			}

			public override string ToString() => $"Network<{this.Id}>(Type={this.Type}, Name={this.Name}, IP={this.Options.IpRange})";

			public override bool Equals(object? obj) => obj is IVirtualNetworkLocation net && Equals(net);

			public bool Equals(IVirtualNetworkLocation? other) => ReferenceEquals(this, other) || (!ReferenceEquals(other, null) && other.Id == this.Id);

			public override int GetHashCode() => this.Id.GetHashCode();

			private SortedSet<IPAddress> AllocatedAddresses { get; } = new(IPAddressComparer.Default);

			private IPAddress? DhcpAddressFirst { get; }
			private IPAddress? DhcpAddressLast { get; }
			private IPAddress? DhcpAddressNextFree { get; set; }

			public void RegisterIpAddress(IPAddress address)
			{
				if (!this.AllocatedAddresses.Add(address))
				{
					throw new InvalidOperationException($"IP Address {address} is already allocated in Network Location {this.Id} ({this.Name})");
				}
			}

			/// <summary>Allocate a new IP address (pseudo-DHCP) on this network location</summary>
			public IPAddress AllocateIpAddress()
			{
				if (this.Type == VirtualNetworkType.Loopback) throw new InvalidOperationException($"Network Location {this.Id} ({this.Name}) does not support DHCP because it is a loopback adaptaer!");
				if (this.DhcpAddressFirst == null) throw new InvalidOperationException($"Network Location {this.Id} ({this.Name}) does not have any IP range for address allocation");

				var candidate = this.DhcpAddressNextFree;
				while (candidate != null)
				{
					var next = IPAddressHelpers.AddOffset(candidate, 1);
					if (IPAddressComparer.Default.Compare(next, this.DhcpAddressLast!) > 0)
					{
						next = null;
					}
					this.DhcpAddressNextFree = next;
					if (this.AllocatedAddresses.Add(candidate))
					{
						return candidate;
					}
					candidate = next;
				}
				throw new InvalidOperationException($"Network Location {this.Id} ({this.Name}) allocation pool is full!");
			}

			/// <summary>Allocate a new MAC address on this network location</summary>
			/// <param name="ouiPrefix">First 24 bits of the MAC address, in the format <c>"XX:XX:XX"</c> (with X = hex digit)</param>
			/// <param name="seed">Optional seed used to generate a deterministic address (like the host name, its IP address, etc...). If <see langword="null"/>, a randomly generated seed will be used instead.</param>
			/// <returns>A newly generated MAC address, in the format <c>"XX:XX:XX:YY:YY:YY"</c> with X coming from <paramref name="ouiPrefix"/> and Y being generated from the <paramref name="seed"/></returns>
			/// <example><c>AllocateMacAddress("12:34:56") => "12:34:56:C0:FF:EE"</c></example>
			public string AllocateMacAddress(string ouiPrefix, string? seed = null)
			{
				Contract.NotNullOrEmpty(ouiPrefix);
				if (ouiPrefix.Length != 8) throw new ArgumentException("OUI prefix must be of the form 'XX:XX:XX'", nameof(ouiPrefix));

				seed ??= Guid.NewGuid().ToString();
				ulong h = Fnv1Hash32.FromString(seed);
				// We only need 3 bytes, so we will folder the 32 bits down to 24 bits.
				int tail = (int) ((h & 0xFFFFFFUL) ^ ((h >> 24) & 0xFFFFFFUL) ^ ((h >> 48) & 0xFFFFUL));
				return $"{ouiPrefix}:{((tail >> 16) & 0xFF):X02}:{((tail >> 8) & 0xFF):X02}:{(tail & 0xFF):X02}";
			}

			/// <summary>Generate a new unique serial number, within this network location</summary>
			/// <param name="pattern">Pattern string where any '#' will be replaced by a digit (0-9), and '?' by an uppercase letter (A-Z)</param>
			/// <param name="seed">Optional seed used to generate a deterministic serial numbers (like the host name, its IP address, etc...). If <see langword="null"/>, a randomly generated seed will be used instead.</param>
			/// <returns>A string where all '#' and '?' in the <paramref name="pattern"/> have been replaced.</returns>
			/// <exception cref="ArgumentException">If the pattern did not include any '#' or '?'</exception>
			/// <example><c>AllocateSerialNumber("ACME-???###-T") => "ACME-ZOB420-T"</c></example>
			public string AllocateSerialNumber(string pattern, string? seed = null)
			{
				Contract.NotNullOrEmpty(pattern);

				seed ??= Guid.NewGuid().ToString();
				ulong h = Fnv1Hash32.FromString(seed);

				bool changed = false;
				var buffer = pattern.ToCharArray();
				for (int i = 0; i < buffer.Length; i++)
				{
					char c = buffer[i];
					if (c == '#')
					{
						changed = true;
						buffer[i] = (char) ('0' + (int) (h % 10));
						h /= 10;
					}
					else if (c == '?')
					{
						changed = true;
						buffer[i] = (char) ('A' + (int) (h % 26));
						h /= 26;
					}
				}
				Contract.Debug.Ensures(h != 0, "Ran out of bits! Please reduce the number of replaced characters!");

				if (!changed) throw new ArgumentException("Invalid pattern: must be of the form 'A###??##' with '#' replaced with digits, and '?' replaced with letters.", nameof(pattern));
				return new string(buffer);
			}

		}

		internal ReaderWriterLockSlim Lock { get; } = new ReaderWriterLockSlim();

		internal Dictionary<string, SimulatedNetwork> Locations { get; } = new Dictionary<string, SimulatedNetwork>(StringComparer.Ordinal);

		/// <summary>Register a new network location to this topology</summary>
		public IVirtualNetworkLocation RegisterLocation(string id, string name, VirtualNetworkType type, VirtualNetworkLocationOptions options)
		{
			Contract.Debug.Requires(id != null && name != null && options != null);
			Contract.Debug.Requires(options.DnsSuffix == null || options.DnsSuffix.StartsWith('.'));

			using (this.Lock.GetWriteLock())
			{
				if (this.Locations.TryGetValue(id, out var previous))
				{
					throw new InvalidOperationException($"There is already a network location with id '{previous.Id}' ('{previous.Name}')");
				}

				var location = new SimulatedNetwork(this, id, name, type, options);
				this.Locations.Add(location.Id, location);
				return location;
			}
		}

		/// <summary>Get a network location, given its identifier.</summary>
		/// <param name="id">Id of the network location</param>
		/// <returns>The corresponding <see cref="IVirtualNetworkLocation"/> if it exists, or an exception if it does not.</returns>
		/// <exception cref="InvalidOperationException">If there is no network location with the given <paramref name="id"/></exception>
		public IVirtualNetworkLocation GetLocation(string id)
		{
			using (this.Lock.GetReadLock())
			{
				return this.Locations.TryGetValue(id, out var location)
					? location
					: throw new InvalidOperationException($"There is no network location '{id}' in the test environment!");
			}
		}

		/// <summary>Generate a multi-line textual dump of the network topology</summary>
		public string Dump()
		{
			using (this.Lock.GetReadLock())
			{
				var sb = new StringBuilder();
				sb.AppendLine("# Network Topology:");
				sb.AppendLine($"# - Hosts: {this.HostsById.Count:N0}");
				foreach (var host in this.HostsById.Values)
				{
					sb.AppendLine($"#   - {host}:");
					foreach (var adapter in host.Adapters)
					{
						host.Handlers.TryGetValue(adapter.Location.Id, out var ports);
						sb.AppendLine($"#     - {adapter.Location.Id}: {(ports != null ? string.Join(", ", ports.Keys) : "<none>")}");
					}
				}

				sb.AppendLine($"# - Locations: {this.Locations.Count:N0}");
				foreach (var loc in this.Locations.Values)
				{
					sb.AppendLine("#   - " + loc);
					foreach (var host in loc.HostsById.Values)
					{
						sb.AppendLine($"#     - {host.Id}: {host.Fqdn}, {string.Join<IPAddress>(", ", host.Addresses)}, {string.Join<string>(", ", host.Aliases)}");
					}
				}

				return sb.ToString();
			}
		}

		/// <summary>Register a new host in the global network topology</summary>
		/// <param name="location">Network location where this host is located</param>
		/// <param name="id">Unique id of this host</param>
		/// <param name="identity">Configuration of this host</param>
		public SimulatedHost RegisterHost(SimulatedNetwork location, string id, VirtualHostIdentity identity)
		{
			Contract.NotNull(location);
			Contract.NotNull(id);
			Contract.NotNull(identity);

			if (location.Topology != this)
			{
				throw new ArgumentException("Network location is attached to a different network topology", nameof(location));
			}

			if (location.Topology.HostsById.TryGetValue(id, out var previous))
			{
				throw new ArgumentException($"There is already an host with id '{id}' defined on this network: {previous.Fqdn}", nameof(id));
			}

			var hostName = identity.HostName;
			var fqdn = identity.Fqdn;

			if (hostName == null)
			{
				if (fqdn != null)
				{
					int p = fqdn.IndexOf('.');
					hostName = p < 1 ? identity.Fqdn : fqdn[..(p - 1)];
				}
				else
				{
					hostName = id.ToLowerInvariant();
				}
				Contract.Debug.Assert(hostName != null);
			}

			if (fqdn == null)
			{
				fqdn = hostName + (identity.DnsSuffix ?? ".simulated");
			}

			var aliases = identity.Aliases.ToArray();
			var addresses = identity.Addresses.ToArray();
			var netMask = IPAddress.Parse("255.0.0.0"); //HACKHACK: BUGBUG: must parse from the IP range!
			var prefixLen = 8; //HACKHACK: BUGBUG: must parse from the IP range!

			var offline = identity.StartAsOffline;

			var adapters = new List<SimulatedNetworkAdapter>();

			using (this.Lock.GetWriteLock())
			{
				adapters.Add(new SimulatedNetworkAdapter()
				{
					Location = location,
					Id = "ethernet",
					Name = "Local Area Connection",
					Description = "Ethernet Network Adapter (virtual)",
					Type = NetworkInterfaceType.Ethernet,
					UnicastAddresses = addresses.Select(ip => (ip, netMask, prefixLen)).ToArray(),
					PhysicalAddress =  "00:11:22:33:44:55", //TODO: BUGBUG: !!
				});

				SimulatedNetwork? loopback = null;
				if (location.Type != VirtualNetworkType.Loopback && !identity.PassthroughToPhysicalNetwork)
				{ // génère automatiquement un "localhost" attaché à ce host
					loopback = new SimulatedNetwork(
						this,
						id + ":loopback",
						"Loopback for " + id,
						VirtualNetworkType.Loopback,
						new() { AllowsIncoming = false, IpRange = "127.0.0.1/24" }
					);
					this.Locations.Add(loopback.Id, loopback);
					adapters.Add(new SimulatedNetworkAdapter()
					{
						Location = loopback,
						Id = "loopback",
						Name = "Loopback",
						Description = "Loopback Network Adapter (virtual)",
						Type = NetworkInterfaceType.Loopback,
						UnicastAddresses = new [] { (IPAddress.Loopback, IPAddress.Parse("255.0.0.0"), 8) },
						PhysicalAddress = null //REVIEW: est-ce que localhost a une MAC Address?
					});
				}

				var host = new SimulatedHost(adapters.ToArray(), id, hostName, fqdn, aliases, addresses, identity.PassthroughToPhysicalNetwork, offline);
				this.HostsById.Add(host.Id, host);
				foreach (var key in host.GetHostKeys())
				{
					this.HostsByNameOrAddress.Add(key, host.Id);
				}

				if (loopback != null)
				{
					loopback.HostsById.Add(host.Id, host);
					loopback.HostsByNameOrAddress.Add("localhost", host.Id);
					loopback.HostsByNameOrAddress.Add("localhost.localdomain", host.Id);
					loopback.HostsByNameOrAddress.Add("127.0.0.1", host.Id);
					loopback.HostsByNameOrAddress.Add("::1", host.Id);
				}

				return host;
			}
		}

		IVirtualNetworkHost IVirtualNetworkTopology.GetHost(string id) => GetHost(id);

		private static InvalidOperationException ErrorMissingHost(string id) => new($"Simulated host '{id}' does not exists");

		/// <summary>Get a virtual host, given its identifier</summary>
		/// <param name="id">Id of the virtual host</param>
		/// <returns>The corresponding <see cref="IVirtualNetworkHost"/> if it exists, or an exception if it does not.</returns>
		/// <exception cref="InvalidOperationException">If there is no virtual host with the given <paramref name="id"/></exception>
		public SimulatedHost GetHost(string id)
		{
			using (this.Lock.GetReadLock())
			{
				return this.HostsById.TryGetValue(id, out var host) ? host : throw ErrorMissingHost(id);
			}
		}

		/// <summary>Get a virtual host, given its IP address</summary>
		/// <param name="address">Known IP address of the host</param>
		/// <param name="host">Receives the host if there is a match.</param>
		/// <returns><see langword="true"/> if the host was found; otherwise, <see langword="false"/></returns>
		public bool TryGetHostByIpAddress(IPAddress address, [MaybeNullWhen(false)] out SimulatedHost host)
		{
			using (this.Lock.GetReadLock())
			{
				if (!this.HostsByNameOrAddress.TryGetValue(address.ToString(), out var hostId))
				{
					host = null;
					return false;
				}
				if (!this.HostsById.TryGetValue(hostId, out host))
				{
					throw ErrorMissingHost(hostId);
				}
				return true;
			}
		}

		/// <summary>Get a virtual host, given its host name</summary>
		/// <param name="hostName">Known name of the host (could be host name, fdqn, one of its aliases, ...)</param>
		/// <param name="host">Receives the host if there is a match.</param>
		/// <returns><see langword="true"/> if the host was found; otherwise, <see langword="false"/></returns>
		public bool TryGetHostByHostName(string hostName, [MaybeNullWhen(false)] out SimulatedHost host)
		{
			using (this.Lock.GetReadLock())
			{
				if (!this.HostsByNameOrAddress.TryGetValue(hostName, out var hostId))
				{
					host = null;
					return false;
				}
				if (!this.HostsById.TryGetValue(hostId, out host)) throw ErrorMissingHost(hostId);
				return true;
			}
		}

		/// <summary>Perform a simulated DNS resolution of the given host name or ip address, from the point of view of a simulated host.</summary>
		/// <param name="hostNameOrAddress">Host name, fqdn, or IP address</param>
		/// <param name="source">Virtual host that is perfoming the DNS resolution</param>
		/// <param name="family">If specified, the type of address resolved (A, AAA, ...)</param>
		/// <param name="ct">Token used to cancel the DNS resolution</param>
		/// <returns>Result of the resolution.</returns>
		/// <exception cref="SocketException">Simulated socket exception, if the resolution has failed</exception>
		/// <exception cref="ArgumentException">If any argument is invalid.</exception>
		/// <remarks>This method attempts to emulates the behavior of <see cref="Dns.GetHostEntryAsync(System.Net.IPAddress)"/></remarks>
		public async Task<IPHostEntry> DnsResolve(string hostNameOrAddress, IVirtualNetworkHost? source, AddressFamily? family, CancellationToken ct)
		{
			Contract.NotNullOrEmpty(hostNameOrAddress);

			ct.ThrowIfCancellationRequested();

			if (!this.HostsByNameOrAddress.TryGetValue(hostNameOrAddress, out var hostId))
			{
				throw new SocketException(11001);
			}

			// simulate context switch
			await Task.Yield();

			var host = GetHost(hostId);

			var addresses = (family ?? AddressFamily.Unspecified) switch
			{
				AddressFamily.Unspecified    => host.Addresses.ToArray(),
				AddressFamily.InterNetwork   => host.Addresses.Where(x => x.AddressFamily == AddressFamily.InterNetwork).ToArray(),
				AddressFamily.InterNetworkV6 => host.Addresses.Where(x => x.AddressFamily == AddressFamily.InterNetworkV6).ToArray(),
				_                            => throw new ArgumentException("Unsupported address family", nameof(family))
			};

			return new IPHostEntry()
			{
				HostName = host.Fqdn,
				Aliases = host.Aliases.ToArray(),
				AddressList = addresses,
			};
		}

	}

}
