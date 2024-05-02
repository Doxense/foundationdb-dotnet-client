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

				Span<byte> xBytes = stackalloc byte[16];
				Span<byte> yBytes = stackalloc byte[16];
				if (x.TryWriteBytes(xBytes, out _) && y.TryWriteBytes(yBytes, out _))
				{
					return xBytes.SequenceCompareTo(yBytes);
				}
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
