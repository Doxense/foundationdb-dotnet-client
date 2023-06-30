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
	using System.Net.Http;

	public interface IBetterHttpProtocolFactory<out TProtocol, out TOptions>
		where TProtocol : IBetterHttpProtocol
		where TOptions : BetterHttpClientOptions
	{

		TProtocol CreateClient(Uri baseAddress, Action<TOptions>? configure = null);

		TProtocol CreateClient(Uri baseAddress, HttpMessageHandler handler, Action<TOptions>? configure = null);

	}

}
