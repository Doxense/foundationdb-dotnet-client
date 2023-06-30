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

	/// <summary>Compares two <see cref="IPEndPoint"/> instances</summary>
	public sealed class IPEndPointComparer : IEqualityComparer<IPEndPoint>, IComparer<IPEndPoint>
	{

		public static readonly IPEndPointComparer Default = new();

		private IPEndPointComparer() { }

		public int Compare(IPEndPoint? x, IPEndPoint? y)
		{
			if (object.ReferenceEquals(x, y)) return 0;
			if (x == null) return -1;
			if (y == null) return +1;

			int cmp = IPAddressComparer.Default.Compare(x.Address, y.Address);
			return cmp == 0 ? x.Port.CompareTo(y.Port) : cmp;
		}

		public bool Equals(IPEndPoint? x, IPEndPoint? y)
		{
			if (object.ReferenceEquals(x, y)) return true;
			if (object.ReferenceEquals(x, null)) return false;
			if (object.ReferenceEquals(y, null)) return false;
			return x.Port == y.Port && x.Address.Equals(y.Address);
		}

		public int GetHashCode(IPEndPoint? obj)
		{
			return obj != null ? HashCodes.Combine(obj.Address.GetHashCode(), obj.Port) : -1;
		}

	}

}
