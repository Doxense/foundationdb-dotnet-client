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
	using System.Net.Sockets;
	using System.Runtime.ExceptionServices;

	public interface IBetterHttpHooks
	{

		void OnStageChanged(BetterHttpClientContext context, BetterHttpClientStage stage);

		void OnError(BetterHttpClientContext context, Exception error);

		bool OnFilterError(BetterHttpClientContext context, Exception error);

		void OnConfigured(BetterHttpClientContext context);

		void OnRequestPrepared(BetterHttpClientContext context);

		void OnRequestCompleted(BetterHttpClientContext context);

		void OnPrepareResponse(BetterHttpClientContext context);

		void OnCompleteResponse(BetterHttpClientContext context);

		void OnFinalizeQuery(BetterHttpClientContext context);

		void OnSocketConnected(BetterHttpClientContext context, Socket socket);

		void OnSocketFailed(BetterHttpClientContext context, Socket socket, Exception error);

	}

	public enum BetterHttpClientStage
	{
		Completed = -1,
		Prepare = 0,
		Configure,
		Send,
		Connecting,
		PrepareRequest,
		CompleteRequest,
		PrepareResponse,
		HandleResponse,
		CompleteResponse,
		Finalize,
	}

}
