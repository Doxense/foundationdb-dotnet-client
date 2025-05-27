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

namespace Doxense.Serialization.Json
{
	using System.Buffers;
	using System.IO;
	using System.Net;
	using System.Net.Http;
	using System.Net.Http.Headers;
	using System.Text;
	using SnowBank.Runtime;

	/// <summary><see cref="HttpContent"/> that uses <see cref="CrystalJson"/> to serialize JSON payloads</summary>
	[PublicAPI]
	public class CrystalJsonContent : HttpContent
	{

		private static readonly ActivitySource ActivitySource = new("SnowBank.Sdk.Serialization.Json");

		private static readonly MediaTypeHeaderValue DefaultMediaType = new("application/json") { CharSet = "utf-8" };
		//REVIEW: c'est un peu dangereux d'utiliser un singleton: le type est mutable, et est exposé via le "content.Headers.ContentType"...
		// A priori on est entre gens de culture, donc on va pas aller muter les content type, mais c'est quand meme un risque!
		// L'implémentation de JsonHttpContent recrée une instance a chaque content par sécurité, ce qui consomme plus de mémoire.
		// => pour l'instant on YOLO, mais a revisiter si besoin!

		private CrystalJsonContent(
			object? inputValue,
			Type inputType,
			MediaTypeHeaderValue? mediaType,
			CrystalJsonSettings? jsonSettings,
			ICrystalJsonTypeResolver? jsonResolver)
		{
			Contract.NotNull(inputType);
#if DEBUG
			if (inputValue != null && !inputType.IsInstanceOfType(inputValue))
			{
				throw new ArgumentException($"Input value is of type {inputValue.GetType().GetFriendlyName()} which is not compatible with specified type {inputType.GetFriendlyName()}");
			}
#endif

			this.Value = inputValue;
			this.ObjectType = inputType;
			this.Headers.ContentType = mediaType ?? DefaultMediaType;
			this.JsonSettings = jsonSettings ?? CrystalJsonSettings.JsonIndented;
			this.JsonResolver = jsonResolver ?? CrystalJson.DefaultResolver;
		}

		public  object? Value { get; }

		public Type ObjectType { get;}

		private CrystalJsonSettings JsonSettings { get; }

		private ICrystalJsonTypeResolver JsonResolver { get; }

		public static CrystalJsonContent Create(JsonValue? inputValue, MediaTypeHeaderValue? mediaType = null, CrystalJsonSettings? settings = null, ICrystalJsonTypeResolver? resolver = null)
			=> new CrystalJsonContent(inputValue ?? JsonNull.Null, inputValue?.GetType() ?? typeof(JsonValue), mediaType, settings, resolver);

		public static CrystalJsonContent Create(object? inputValue, Type inputType, MediaTypeHeaderValue? mediaType = null, CrystalJsonSettings? settings = null, ICrystalJsonTypeResolver? resolver = null)
			=> new CrystalJsonContent(inputValue, inputType, mediaType, settings, resolver);

		public static CrystalJsonContent Create<T>(T? inputValue, MediaTypeHeaderValue? mediaType = null, CrystalJsonSettings? settings = null, ICrystalJsonTypeResolver? resolver = null)
			=> new CrystalJsonContent(inputValue, typeof(T), mediaType, settings, resolver);

		private SliceOwner CachedBytes;

		private SliceOwner RenderBytes()
		{
			using var activity = ActivitySource.StartActivity("JSON Serialize");

			Encoding? targetEncoding = GetEncoding(Headers.ContentType?.CharSet);

			try
			{
				if (targetEncoding != null && !Encoding.UTF8.Equals(targetEncoding))
				{ // exotic encoding!
					string jsonText = CrystalJson.Serialize(this.Value, this.ObjectType, this.JsonSettings, this.JsonResolver);
					var bytes = targetEncoding.GetBytes(jsonText).AsSlice();
					activity?.SetTag("json.length", bytes.Count);
					return SliceOwner.Create(bytes);
				}
				else
				{ // UTF-8: direct to bytes
					var pool = ArrayPool<byte>.Shared;
					var bytes = CrystalJson.ToSlice(this.Value, this.ObjectType, pool, this.JsonSettings, this.JsonResolver);
					activity?.SetTag("json.length", bytes.Count);
					return bytes;
				}
			}
			catch (Exception ex)
			{
				activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
				activity?.AddException(ex);
				throw;
			}
		}

		protected override async Task SerializeToStreamAsync(Stream stream, TransportContext? context)
		{
			var cached = this.CachedBytes;
			if (!cached.IsValid)
			{
				this.CachedBytes = cached = RenderBytes();
			}

			try
			{
				//REVIEW: where can we get a valid cancellation token??
				await stream.WriteAsync(cached.Memory).ConfigureAwait(false);
			}
			finally
			{
				cached.Dispose();
				// on part du principe qu'il va appeler SerializeToStreamAsync qu'une seule fois, donc on peut pré-emptivement retourner notre buffer!
				this.CachedBytes = default;
			}
		}

		protected override bool TryComputeLength(out long length)
		{
			//REVIEW: PERF: TODO: Pour optimiser les requetes, ca serait une bonne idée de connaitre la taille (en bytes) a l'avance, pour que le Content-Length soit renseigné
			// PB: si le JSON est vraiment trop gros, ca va consommer beaucoup de mémoire localement.
			// => idéalement tester a l'avance si le json est "petit" ou "gros" (de manière cheap), et en fonction décider si on pre-render (et on specifie le Content-Length) ou non (et c'est du chunked?)

			var cached = this.CachedBytes;
			if (!cached.IsValid)
			{
				this.CachedBytes = cached = RenderBytes();
			}

			length = cached.Count;
			return true;
		}

		internal static Encoding? GetEncoding(string? charset)
		{
			Encoding? encoding = null;

			if (charset != null)
			{
				try
				{
					// Remove at most a single set of quotes.
					if (charset.Length > 2 && charset[0] == '\"' && charset[^1] == '\"')
					{
						encoding = Encoding.GetEncoding(charset[1..^1]);
					}
					else
					{
						encoding = Encoding.GetEncoding(charset);
					}
				}
				catch (ArgumentException e)
				{
					throw new InvalidOperationException("Invalid charset", e);
				}

				Contract.Debug.Ensures(encoding != null);
			}

			return encoding;
		}

	}

}
