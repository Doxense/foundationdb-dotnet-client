#region Copyright (c) 2023-2025 SnowBank SAS, (c) 2005-2023 Doxense SAS
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

namespace SnowBank.Networking.Http
{

	/// <summary>Type that can create <see cref="BetterHttpClient"/> instances with optional customization</summary>
	public interface IBetterHttpClientFactory
	{

		/// <summary>Creates a new <see cref="HttpMessageHandler"/> that can be used to connect to the specified host</summary>
		/// <param name="hostAddress">Host name or IP address of the remote target</param>
		/// <param name="options">Custom options used to customize the handler</param>
		/// <returns>Configured handler that will connect to the specified host</returns>
		HttpMessageHandler CreateHttpHandler(Uri hostAddress, BetterHttpClientOptions options);

		/// <summary>Creates a new <see cref="BetterHttpClient"/> that can be used to send requests to the specified host</summary>
		/// <param name="hostAddress">Host name or IP address of the remote target</param>
		/// <param name="options">Custom options used to customize the client</param>
		/// <param name="handler">HTTP handler that should be used. If <c>null</c>, a new handler will be created and configured automatically.</param>
		/// <returns>Configured client that will send requests to the specified host</returns>
		BetterHttpClient CreateClient(Uri hostAddress, BetterHttpClientOptions options, HttpMessageHandler? handler = null);

	}

}
