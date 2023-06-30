#region Copyright (c) 2005-2023 Doxense SAS
//
// All rights are reserved. Reproduction or transmission in whole or in part, in
// any form or by any means, electronic, mechanical or otherwise, is prohibited
// without the prior written consent of the copyright owner.
//
#endregion

namespace Doxense.Networking.Tests
{
	using System;
	using System.Net;
	using System.Net.Sockets;
	using Doxense.Networking;
	using Doxense.Testing;
	using NUnit.Framework;

	[TestFixture]
	[Category("Core-SDK")]
	public class IPAddressHelpersFacts : DoxenseTest
	{

		[Test]
		public void Test_IsValidIP_V4()
		{
			static void ShouldPassV4(string ip)
			{
				Assert.That(IPAddressHelpers.IsValidIP(ip), Is.True, "'{0}' is a valid IP address", ip);
				Assert.That(IPAddressHelpers.IsValidIPv4(ip), Is.True, "'{0}' is a valid IPv4 address", ip);
				Assert.That(IPAddressHelpers.IsValidIPv6(ip), Is.False, "'{0}' is NOT an IPv4 address", ip);
			}

			static void ShouldFailV4(string? ip)
			{
				Assert.That(IPAddressHelpers.IsValidIPv4(ip), Is.False, "'{0}' is NOT a valid IPv4 address", ip);
				if (IPAddress.TryParse(ip, out var x) && x.AddressFamily == AddressFamily.InterNetworkV6)
				{
					Assert.That(IPAddressHelpers.IsValidIP(ip), Is.True, "'{0}' is valid but no an IPv4 address", ip);
				}
				else
				{
					Assert.That(IPAddressHelpers.IsValidIP(ip), Is.False, "'{0}' is NOT a valid IP address", ip);
				}
			}

			ShouldPassV4("127.0.0.1");
			ShouldPassV4("192.168.1.0");
			ShouldPassV4("192.168.1.23");
			ShouldPassV4("192.168.1.123");
			ShouldPassV4("192.168.1.255");
			ShouldPassV4("8.8.4.4");
			ShouldPassV4("0.0.0.0");
			ShouldPassV4("255.255.255.255");

			ShouldFailV4(null);
			ShouldFailV4("");
			ShouldFailV4("192.168.1.256");
			ShouldFailV4("192.168.256.1");
			ShouldFailV4("192.256.1.2");
			ShouldFailV4("256.1.2.3");
			ShouldFailV4("1.2.3.4.5");
			ShouldFailV4(".1.2.3.4");
			ShouldFailV4("1..2.3");
			ShouldFailV4("8.8.4.4 ");
			ShouldFailV4(" 8.8.4.4");
			ShouldFailV4("8.8 .4.4 ");

			//IPv6 should fail
			ShouldFailV4("::1");
			ShouldFailV4("fe80::40d0:a961:a90a:4fc1");
			ShouldFailV4("2001:db8::ff00:42:8329");
		}

		[Test]
		public void Test_IsValidIP_V6()
		{
			static void ShouldPassV6(string ip)
			{
				Assert.That(IPAddressHelpers.IsValidIP(ip), Is.True, "'{0}' is a valid IP address", ip);
				Assert.That(IPAddressHelpers.IsValidIPv6(ip), Is.True, "'{0}' is a valid IPv6 address", ip);
				Assert.That(IPAddressHelpers.IsValidIPv4(ip), Is.False, "'{0}' is NOT an IPv6 address", ip);
			}

			static void ShouldFailV6(string? ip)
			{
				Assert.That(IPAddressHelpers.IsValidIPv6(ip), Is.False, "'{0}' is NOT a valid IPv6 address", ip);
				if (IPAddress.TryParse(ip, out var x) && x.AddressFamily == AddressFamily.InterNetwork)
				{
					Assert.That(IPAddressHelpers.IsValidIP(ip), Is.True, "'{0}' is valid but no an IPv6 address", ip);
				}
				else
				{
					Assert.That(IPAddressHelpers.IsValidIP(ip), Is.False, "'{0}' is NOT a valid IP address", ip);
				}
			}

			ShouldPassV6("::1");
			ShouldPassV6("fe80::40d0:a961:a90a:4fc1");
			ShouldPassV6("fe80::40d0:a961:a90a:4fc1%100");
			ShouldPassV6("2001:0db8:0000:0000:0000:ff00:0042:8329");
			ShouldPassV6("2001:db8:0:0:0:ff00:42:8329");
			ShouldPassV6("2001:db8::ff00:42:8329");

			ShouldFailV6(null);
			ShouldFailV6("");
			ShouldFailV6(" fe80::40d0:a961:a90a:4fc1");
			ShouldFailV6("fe80::40d0:a961:a90a:4fc1 ");
			ShouldFailV6("fe80::40d0::a961:a90a:4fc1");
			ShouldFailV6(" fe80:40d0:a961:a90a:4fc1");

			//IPv4 sould fail
			ShouldFailV6("127.0.0.1");
			ShouldFailV6("192.168.1.123");
			ShouldFailV6("0.0.0.0");
			ShouldFailV6("255.255.255.255");

		}

		[Test]
		public void Test_IsPrivateNetwork()
		{
			Assert.That(() => IPAddressHelpers.IsPrivateRange(null!), Throws.ArgumentNullException.With.Property("ParamName").EqualTo("address"));

			static void ShouldPass(string ip)
			{
				Assert.That(IPAddressHelpers.IsPrivateRange(IPAddress.Parse(ip)), Is.True, "'{0}' is an adress in the Private Network range", ip);
			}

			static void ShouldFail(string ip)
			{
				Assert.That(IPAddressHelpers.IsPrivateRange(IPAddress.Parse(ip)), Is.False, "'{0}' is NOT an adress in the Private Network range", ip);
			}

			// 192.168/16
			ShouldFail("192.167.255.255");
			ShouldPass("192.168.0.0");
			ShouldPass("192.168.1.23");
			ShouldPass("192.168.12.34");
			ShouldPass("192.168.255.255");
			ShouldFail("192.169.0.0");
			ShouldFail("1.2.168.192");

			// 10/8
			ShouldFail("9.255.255.255");
			ShouldPass("10.0.0.0");
			ShouldPass("10.1.2.3");
			ShouldPass("10.255.255.255");
			ShouldFail("11.0.0.0");
			ShouldFail("1.2.3.10");

			// 172.16/20
			ShouldFail("172.15.255.255");
			ShouldPass("172.16.0.0");
			ShouldPass("172.23.45.67");
			ShouldPass("172.31.255.255");
			ShouldFail("172.32.0.0");
			ShouldFail("1.2.16.172");

			// Not Private
			ShouldFail("0.0.0.0");
			ShouldFail("255.255.255.255");
			ShouldFail("127.0.0.1");
			ShouldFail("127.0.0.123");
			ShouldFail("8.8.8.8");
		}

	}

}
