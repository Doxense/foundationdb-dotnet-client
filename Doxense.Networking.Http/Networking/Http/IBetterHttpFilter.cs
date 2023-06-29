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
	using System.Threading.Tasks;

	public interface IBetterHttpFilter
	{

		string Name { get; }

		ValueTask Configure(BetterHttpClientContext context);

		ValueTask PrepareRequest(BetterHttpClientContext context);

		ValueTask CompleteRequest(BetterHttpClientContext context);

		ValueTask PrepareResponse(BetterHttpClientContext context);

		ValueTask CompleteResponse(BetterHttpClientContext context);

		ValueTask Finalize(BetterHttpClientContext context);

	}

}
