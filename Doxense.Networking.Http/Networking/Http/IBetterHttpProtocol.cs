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

	public interface IBetterHttpProtocol : IDisposable
	{

		/// <summary>Name of the protocol, for logging/troubleshooting purpose</summary>
		public string Name { get; }

		/// <summary>Client used by this protocol</summary>
		public BetterHttpClient Http { get; }

	}

}
