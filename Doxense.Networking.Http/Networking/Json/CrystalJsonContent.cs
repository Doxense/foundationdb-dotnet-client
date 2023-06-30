#region Copyright (c) 2018-2022, Doxense SAS. All rights reserved.
// See License.MD for license information
#endregion

namespace Doxense.Serialization.Json
{
	using System;
	using System.Buffers;
	using System.Diagnostics;
	using System.IO;
	using System.Net;
	using System.Net.Http;
	using System.Net.Http.Headers;
	using System.Text;
	using System.Threading.Tasks;
	using Doxense.Diagnostics.Contracts;
	using Doxense.Serialization;
	using OpenTelemetry.Trace;

	/// <summary><see cref="HttpContent"/> that uses <see cref="CrystalJson"/> to serialize JSON payloads</summary>
	public class CrystalJsonContent : HttpContent
	{

		private static readonly ActivitySource ActivitySource = new("Doxense.Serialization.Json");

		private static readonly MediaTypeHeaderValue DefaultMediaType = new MediaTypeHeaderValue("application/json") { CharSet = "utf-8" };
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

		private (Slice Bytes, ArrayPool<byte>? Pool) CachedBytes;

		private (Slice Bytes, ArrayPool<byte>? Pool) RenderBytes()
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
					return (bytes, null);
				}
				else
				{ // UTF-8: direct to bytes
					var pool = ArrayPool<byte>.Shared;
					var bytes = CrystalJson.ToBuffer(this.Value, this.ObjectType, this.JsonSettings, this.JsonResolver, pool: pool);
					activity?.SetTag("json.length", bytes.Count);
					return (bytes, pool);
				}
			}
			catch (Exception ex)
			{
				activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
				activity?.RecordException(ex);
				throw;
			}
		}

		protected override async Task SerializeToStreamAsync(Stream stream, TransportContext? context)
		{
			var cached = this.CachedBytes;
			if (cached.Bytes.IsNull)
			{
				this.CachedBytes = cached = RenderBytes();
			}

			try
			{
				//REVIEW: where can we get a valid cancellation token??
				await stream.WriteAsync(cached.Bytes.Memory);
			}
			finally
			{
				cached.Pool?.Return(cached.Bytes.Array);
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
			if (cached.Bytes.IsNull)
			{
				this.CachedBytes = cached = RenderBytes();
			}

			length = cached.Bytes.Count;
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
