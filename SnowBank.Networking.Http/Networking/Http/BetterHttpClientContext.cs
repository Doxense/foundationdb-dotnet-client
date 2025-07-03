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
	using System.Diagnostics.CodeAnalysis;
	using System.Globalization;
	using System.IO;
	using System.Runtime.CompilerServices;
	using System.Runtime.ExceptionServices;
	using System.Xml.Linq;
	using Microsoft.IO;

	/// <summary>Represents the context of an HTTP request being executed</summary>
	[DebuggerDisplay("{ToString(),nq}")]
	[PublicAPI]
	public class BetterHttpClientContext
	{

		/// <summary>Instance of the <see cref="BetterHttpClient">client</see> executing this request</summary>
		public required BetterHttpClient Client { get; init; }

		/// <summary>Unique ID of this request (for logging purpose)</summary>
		public required string Id { get; init; }

		/// <summary>Cancellation token attached to the lifetime of this request</summary>
		public CancellationToken Cancellation { get; init; }

		/// <summary>Current stage in the execution pipeline</summary>
		public BetterHttpClientStage Stage { get; private set; }

		/// <summary>If non-null, the stage at which the request failed.</summary>
		public BetterHttpClientStage? FailedStage { get; internal set; }

		/// <summary>Bag of items that will be available throughout the lifetime of the request</summary>
		public required Dictionary<string, object?> State { get; init; }

		/// <summary>Request that will be sent to the remote HTTP server</summary>
		public required HttpRequestMessage Request { get; init; }

		/// <summary>Original response object, before it was intercepted.</summary>
		internal HttpResponseMessage? OriginalResponse { get; set; }

		/// <summary>Response that was received from the remote HTTP server</summary>
		public HttpResponseMessage Response
		{
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get => this.OriginalResponse ?? FailErrorNotAvailable();
		}

		[MethodImpl(MethodImplOptions.NoInlining)]
		private static HttpResponseMessage FailErrorNotAvailable() => throw new InvalidOperationException("The response message is not available.");

		/// <summary>Box that captured any error that happened during the processing of the request</summary>
		public ExceptionDispatchInfo? Error { get; internal set; }

		/// <summary>Changes the current stage in the execution pipeline</summary>
		internal void SetStage(BetterHttpClientStage stage)
		{
			this.Stage = stage;
			this.Client.Options.Hooks?.OnStageChanged(this, stage);
		}

		/// <summary>Sets (or clear) an item in the <see cref="State"/> dictionary</summary>
		/// <typeparam name="TState"></typeparam>
		/// <param name="key">Key of the item</param>
		/// <param name="state">New value for this item. If null, the item is removed</param>
		public void SetState<TState>(string key, TState? state)
		{
			Contract.Debug.Requires(key != null);
			if (state is null)
			{
				this.State.Remove(key);
			}
			else
			{
				this.State[key] = state;
			}
		}

		/// <summary>Reads an item that was previously stored in the <see cref="State"/> dictionary</summary>
		public bool TryGetState<TState>(string key, [MaybeNullWhen(false)] out TState state)
		{
			Contract.Debug.Requires(key != null);
			if (!this.State.TryGetValue(key, out var obj) || obj is not TState value)
			{
				state = default;
				return false;
			}

			state = value;
			return true;
		}

		/// <summary>Throws an exception if the <see cref="IsSuccessStatusCode"/> property for the HTTP response is false.</summary>
		public void EnsureSuccessStatusCode()
		{
			this.Response.EnsureSuccessStatusCode();
		}

		/// <summary>Gets a value that indicates if the response was successful</summary>
		public bool IsSuccessStatusCode => this.OriginalResponse?.IsSuccessStatusCode ?? false;

		/// <summary>Reads the response body as a string</summary>
		public Task<string> ReadAsStringAsync()
		{
			return this.Response.Content.ReadAsStringAsync(this.Cancellation);
		}

		/// <summary>Returns a stream that can be used to read the response body</summary>
		public Task<Stream> ReadAsStreamAsync()
		{
			return this.Response.Content.ReadAsStreamAsync(this.Cancellation);
		}

		/// <summary>Copies the response body into the provided stream</summary>
		public Task CopyToAsync(Stream stream)
		{
			return this.Response.Content.CopyToAsync(stream, this.Cancellation);
		}

		#region JSON Helpers...

		/// <summary>Guesses if the response body is <i>likely</i> to be a JSON document</summary>
		/// <returns><c>true</c> if there is a high probability that the body contains a JSON document</returns>
		/// <remarks>
		/// <para>Since we cannot inspect the whole response BODY (which may not have been received yet), this method can only guess by looking at the <c>Content-Type</c> header, and so may return either false-positives, or false-negatives.</para>
		/// <para>This should only be used by error handling logic that could decide whether to parse the body or not, looking for additional details.</para>
		/// </remarks>
		public bool IsLikelyJson()
		{
			//TODO: a better heuristic? The issue is that we may not have received the whole body yet, so we can inspect it until the end to match the '}' or ']' !
			if (this.OriginalResponse == null) return false;
			if (this.Response.Content.Headers.ContentType?.MediaType == "application/json")
			{
				return true;
			}

			return false;
		}

		private static readonly RecyclableMemoryStreamManager DefaultPool = new();

		/// <summary>Reads the response body as a JSON value</summary>
		public async Task<JsonValue> ReadAsJsonAsync(CrystalJsonSettings? settings = null)
		{
			this.Cancellation.ThrowIfCancellationRequested();
			using var activity = BetterHttpInstrumentation.ActivitySource.StartActivity("JSON Parse");

			try
			{
				//BUGBUG: PERF: until we have async JSON parsing, we have to buffer everything to memory
				using (var ms = DefaultPool.GetStream())
				{
					await this.CopyToAsync(ms).ConfigureAwait(false);
					return CrystalJson.Parse(ms.ToSlice(), settings);
				}
			}
			catch (Exception ex)
			{
				activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
				activity?.AddException(ex);
				throw;
			}
		}

		/// <summary>Reads the response body as a JSON Object</summary>
		public async Task<JsonObject?> ReadAsJsonObjectAsync(CrystalJsonSettings? settings = null)
		{
			this.Cancellation.ThrowIfCancellationRequested();
			using var activity = BetterHttpInstrumentation.ActivitySource.StartActivity("JSON Parse");

			try
			{
				//BUGBUG: PERF: until we have async JSON parsing, we have to buffer everything to memory
				using (var ms = DefaultPool.GetStream())
				{
					await CopyToAsync(ms).ConfigureAwait(false);
					activity?.SetTag("json.length", ms.Length);
					return CrystalJson.Parse(ms.ToSlice(), settings).AsObjectOrDefault();
				}
			}
			catch (Exception ex)
			{
				activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
				activity?.AddException(ex);
				throw;
			}
		}

		/// <summary>Reads the response body as a JSON Array</summary>
		public async Task<JsonArray?> ReadAsJsonArrayAsync(CrystalJsonSettings? settings = null)
		{
			this.Cancellation.ThrowIfCancellationRequested();
			using var activity = BetterHttpInstrumentation.ActivitySource.StartActivity("JSON Parse");

			try
			{
				//BUGBUG: PERF: until we have async JSON parsing, we have to buffer everything to memory
				using (var ms = DefaultPool.GetStream())
				{
					await CopyToAsync(ms).ConfigureAwait(false);
					return CrystalJson.Parse(ms.ToSlice(), settings).AsArrayOrDefault();
				}
			}
			catch (Exception ex)
			{
				activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
				activity?.AddException(ex);
				throw;
			}
		}

		/// <summary>Reads the response body as a JSON document, and converts the result into an instance of type <typeparamref name="TResult"/></summary>
		public async Task<TResult?> ReadAsJsonAsync<TResult>(CrystalJsonSettings? settings = null, ICrystalJsonTypeResolver? resolver = null)
		{
			this.Cancellation.ThrowIfCancellationRequested();
			using var activity = BetterHttpInstrumentation.ActivitySource.StartActivity("JSON Parse");

			try
			{
				//BUGBUG: PERF: until we have async JSON parsing, we have to buffer everything to memory
				using (var ms = DefaultPool.GetStream())
				{
					await CopyToAsync(ms).ConfigureAwait(false);
					return CrystalJson.Deserialize<TResult?>(ms.ToSlice(), default, settings, resolver);
				}
			}
			catch (Exception ex)
			{
				activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
				activity?.AddException(ex);
				throw;
			}
		}

		#endregion

		#region XML Helpers...

		/// <summary>Guesses if the response body is <i>likely</i> to be an XML document</summary>
		/// <returns><c>true</c> if there is a high probability that the body contains an XML document</returns>
		/// <remarks>
		/// <para>Since we cannot inspect the whole response BODY (which may not have been received yet), this method can only guess by looking at the <c>Content-Type</c> header, and so may return either false-positives, or false-negatives.</para>
		/// <para>This should only be used by error handling logic that could decide whether to parse the body or not, looking for additional details.</para>
		/// </remarks>
		public bool IsLikelyXml()
		{
			//TODO: a better heuristic? The issue is that we may not have received the whole body yet, so we can inspect it until the end to match the closing tag !
			if (this.OriginalResponse == null) return false;
			if (this.Response.Content.Headers.ContentType?.MediaType == "text/xml")
			{
				return true;
			}
			return false;
		}

		/// <summary>Reads the response body as an XML document</summary>
		public async Task<XDocument?> ReadAsXmlAsync(LoadOptions options = LoadOptions.None)
		{
			this.Cancellation.ThrowIfCancellationRequested();
			using var activity = BetterHttpInstrumentation.ActivitySource.StartActivity("XML Parse");

			try
			{
				var stream = await this.Response.Content.ReadAsStreamAsync(this.Cancellation).ConfigureAwait(false);
				//note: do NOT dispose this stream here!

				return await XDocument.LoadAsync(stream, options, this.Cancellation).ConfigureAwait(false);
			}
			catch (Exception ex)
			{
				activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
				activity?.AddException(ex);
				throw;
			}
		}

		#endregion

		public override string ToString()
		{
			return string.Create(CultureInfo.InvariantCulture, $"{this.Request.Method} {this.Request.RequestUri} => {(this.OriginalResponse != null ? $"{(int) this.Response.StatusCode} {this.Response.ReasonPhrase}" : "<no response>")}");
		}

	}

}
