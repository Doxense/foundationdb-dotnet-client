#region Copyright (c) 2005-2023 Doxense SAS
//
// All rights are reserved. Reproduction or transmission in whole or in part, in
// any form or by any means, electronic, mechanical or otherwise, is prohibited
// without the prior written consent of the copyright owner.
//
#endregion

namespace Doxense.Networking
{
	using System;
	using System.Collections.Generic;
	using System.Net;
	using System.Net.Sockets;
	using Doxense.Memory;

	public sealed class IPAddressComparer : IComparer<IPAddress>, IEqualityComparer<IPAddress>
	{
		public static readonly IPAddressComparer Default = new IPAddressComparer();

		private IPAddressComparer()
		{ }

		public int Compare(IPAddress? x, IPAddress? y)
		{
			if (object.ReferenceEquals(x, y)) return 0;
			if (x == null) return -1;
			if (y == null) return +1;

			var f = x.AddressFamily;
			if (f != y.AddressFamily)
			{ // sort by familly
				//TODO: support IPv4-mapped IPv6 address? (ie: '::ffff:192.168.1.0' ~= '192.168.1.0') ?
				return f - y.AddressFamily;
			}

			if (f == AddressFamily.InterNetwork)
			{
#pragma warning disable 618
				return UnsafeHelpers.ByteSwap32((uint) x.Address).CompareTo(UnsafeHelpers.ByteSwap32((uint) y.Address));
#pragma warning restore 618
			}

			if (f == AddressFamily.InterNetworkV6)
			{ // je n'ai pas trouvé de moyen d'accéder a "_numbers[]" directement,
			  // le seul compromis que j'ai trouvé c'est utiliser TryWriteBytes(..) dans des buffers stackalloced

#if NETFRAMEWORK || NETSTANDARD
				// we have to allocate memory for this :(
				return x.GetAddressBytes().AsSpan().SequenceCompareTo(y.GetAddressBytes());
#else
				Span<byte> xBytes = stackalloc byte[16];
				Span<byte> yBytes = stackalloc byte[16];
				if (x.TryWriteBytes(xBytes, out _) && y.TryWriteBytes(yBytes, out _))
				{
					return xBytes.SequenceCompareTo(yBytes);
				}
#endif
			}

			// very slow fallback: we will been to allocate memory for the comparison :(
			return x.GetAddressBytes().AsSpan().SequenceCompareTo(y.GetAddressBytes());
		}

		public bool Equals(IPAddress? x, IPAddress? y)
		{
			return object.ReferenceEquals(x, y) || (x == null ? y == null : y != null && x.Equals(y));
		}

		public int GetHashCode(IPAddress obj)
		{
			return obj?.GetHashCode() ?? -1;
		}

	}

}
