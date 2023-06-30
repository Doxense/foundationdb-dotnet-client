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

	public interface IBetterHttpClientFactory
	{

		BetterHttpClient CreateClient(Uri hostAddress, BetterHttpClientOptions options, HttpMessageHandler? handler = null);

	}

}
