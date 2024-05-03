#region Copyright (c) 2023-2024 SnowBank SAS, (c) 2005-2023 Doxense SAS
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

//#define DEBUG_JSON_PARSER

namespace Doxense.Serialization.Json
{
	using System.Buffers;
	using System.Diagnostics;
	using System.Diagnostics.CodeAnalysis;
	using System.IO;
	using System.Reflection;
	using System.Runtime.CompilerServices;
	using System.Text;
	using Doxense.IO;
	using Doxense.Memory;
	using Doxense.Memory.Text;
	using Doxense.Runtime;

	/// <summary>Helper class to serialize, parse or deserialize JSON documents</summary>
	[PublicAPI]
	[DebuggerNonUserCode]
	public static class CrystalJson
	{
		public static readonly CrystalJsonTypeResolver DefaultResolver = new CrystalJsonTypeResolver();
		public static readonly UTF8Encoding Utf8NoBom = CrystalJsonFormatter.Utf8NoBom;

		public static void Warmup()
		{
			PlatformHelpers.PreJit(
				typeof(CrystalJsonSettings), typeof(CrystalJsonNodaPatterns),
				typeof(JsonNull), typeof(JsonBoolean), typeof(JsonString), typeof(JsonNumber), typeof(JsonArray), typeof(JsonObject), typeof(JsonNull), typeof(JsonValue),
				typeof(CrystalJsonVisitor), typeof(CrystalJsonTypeVisitor), 
				typeof(CrystalJsonStreamReader), typeof(CrystalJsonStreamWriter), typeof(CrystalJsonParser), typeof(CrystalJsonDomWriter),
				typeof(CrystalJson)
			);
		}

		[Flags]
		public enum SaveOptions
		{
			None = 0,
			/// <summary>If the file already exists, save first into a temporary file, and swap it with the previous one in a single step</summary>
			AtomicSave = 1,
			/// <summary>If the file already exists, a backup copy will be created (with the ".bak" extension)</summary>
			KeepBackup = 2,
			/// <summary>Append to the end of the file (create it if necessary), instead of overwriting it. Should only be used to JSON fragments, or JSON logs</summary>
			Append = 4,
		}

		[Flags]
		public enum LoadOptions
		{
			None = 0,
			/// <summary>If the file does not exist, return the default value of the type (null, 0, false, ...)</summary>
			ReturnNullIfMissing = 1,
			/// <summary>If the source is using streaming (socket, ...), do not wait to reach the end of the file, and stop once a complete top-level value as been consumed.</summary>
			Streaming = 2
		}

		#region Serialization...

		/// <summary>Serializes a boxed value (of any type)</summary>
		/// <param name="value">Instance to serialize (can be null)</param>
		/// <param name="settings">Serialization settings (use default JSON settings if null)</param>
		/// <param name="resolver">Custom type resolver (use default behavior if null)</param>
		/// <returns><c>`123`</c>, <c>`true`</c>, <c>`"ABC"`</c>, <c>`{ "foo":..., "bar": ... }`</c>, <c>`[ ... ]`</c>, ...</returns>
		/// <exception cref="Doxense.Serialization.Json.JsonSerializationException">If the object fails to serialize properly (non-serializable type, loop in the object graph, ...)</exception>
		[Pure]
		public static string Serialize(object? value, CrystalJsonSettings? settings = null, ICrystalJsonTypeResolver? resolver = null)
		{
			return SerializeInternal(value, typeof(object), null, settings, resolver).ToString();
		}

		/// <summary>Serializes a boxed value (of any type)</summary>
		/// <param name="value">Instance to serialize (can be null)</param>
		/// <param name="declaredType">Type of the field or property, as declared in the parent type.</param>
		/// <param name="settings">Serialization settings (use default JSON settings if null)</param>
		/// <param name="resolver">Custom type resolver (use default behavior if null)</param>
		/// <returns><c>`123`</c>, <c>`true`</c>, <c>`"ABC"`</c>, <c>`{ "foo":..., "bar": ... }`</c>, <c>`[ ... ]`</c>, ...</returns>
		/// <exception cref="Doxense.Serialization.Json.JsonSerializationException">If the object fails to serialize properly (non-serializable type, loop in the object graph, ...)</exception>
		[Pure]
		public static string Serialize(object? value, Type declaredType, CrystalJsonSettings? settings = null, ICrystalJsonTypeResolver? resolver = null)
		{
			return SerializeInternal(value, declaredType, null, settings, resolver).ToString();
		}

		/// <summary>Serializes a value (of any type)</summary>
		/// <param name="value">Instance to serialize (can be null)</param>
		/// <param name="settings">Serialization settings (use default JSON settings if null)</param>
		/// <param name="resolver">Custom type resolver (use default behavior if null)</param>
		/// <returns><c>`123`</c>, <c>`true`</c>, <c>`"ABC"`</c>, <c>`{ "foo":..., "bar": ... }`</c>, <c>`[ ... ]`</c>, ...</returns>
		/// <exception cref="Doxense.Serialization.Json.JsonSerializationException">If the object fails to serialize properly (non-serializable type, loop in the object graph, ...)</exception>
		[Pure]
		public static string Serialize<T>(T? value, CrystalJsonSettings? settings = null, ICrystalJsonTypeResolver? resolver = null)
		{
			return SerializeInternal(value, typeof(T), null, settings, resolver).ToString();
		}

		/// <summary>Serializes a boxed value (of any type) into the specified buffer</summary>
		/// <param name="value">Instance to serialize (can be null)</param>
		/// <param name="buffer">Buffer de destination (créé automatiquement si null)</param>
		/// <param name="settings">Serialization settings (use default JSON settings if null)</param>
		/// <param name="resolver">Custom type resolver (use default behavior if null)</param>
		/// <returns>The value of <paramref name="buffer"/>, for call chaining</returns>
		/// <exception cref="Doxense.Serialization.Json.JsonSerializationException">If the object fails to serialize properly (non-serializable type, loop in the object graph, ...)</exception>
		[Pure]
		public static StringBuilder Serialize(object? value, StringBuilder? buffer, CrystalJsonSettings? settings = null, ICrystalJsonTypeResolver? resolver = null)
		{
			return SerializeInternal(value, typeof(object), buffer, settings, resolver);
		}

		/// <summary>Creates a new empty buffer with the appropriate size</summary>
		/// <param name="settings">Serialization settings (use default JSON settings if null)</param>
		/// <returns>Empty StringBuilder, with an initial capacity that depends on the settings</returns>
		[Pure]
		private static StringBuilder CreateBufferFromSettings(CrystalJsonSettings? settings)
		{
			int capacity = settings?.OptimizeForLargeData == true ? 4096 : 512;
			return new StringBuilder(capacity);
		}

		/// <summary>Serializes a boxed value (of any type) into the specified buffer</summary>
		/// <param name="value">Class, struct, Enumerable, Nullable&lt;T&gt;, ...</param>
		/// <param name="declaredType"></param>
		/// <param name="buffer">Destination buffer (created automically if null)</param>
		/// <param name="settings">Serialization settings (use default JSON settings if null)</param>
		/// <param name="resolver">Custom type resolver (use default behavior if null)</param>
		/// <returns>Value of <paramref name="buffer"/>, or of the newly created buffer if it was null (for call chaining)</returns>
		[Pure]
		private static StringBuilder SerializeInternal(object? value, Type declaredType, StringBuilder? buffer, CrystalJsonSettings? settings, ICrystalJsonTypeResolver? resolver)
		{
			if (value == null)
			{ // special case for null instances
				return buffer?.Append(JsonTokens.Null) ?? new StringBuilder(JsonTokens.Null);
			}

			// grab a new buffer if needed
			buffer ??= CreateBufferFromSettings(settings);

			//REVIEW: use an ObjectPool for FastStringWriter and CrystalJsonWriter?

			using (var fsw = new FastStringWriter(buffer))
			{
				var writer = new CrystalJsonWriter(fsw, settings, resolver);
				CrystalJsonVisitor.VisitValue(value, declaredType, writer);
				return buffer;
			}
		}

		/// <summary>Serializes a boxed value (of any type) into the specified output</summary>
		/// <param name="output">Output for the JSON document</param>
		/// <param name="value">Instance to serialize (of any type)</param>
		/// <param name="settings">Serialization settings (use default JSON settings if null)</param>
		/// <param name="resolver">Custom type resolver (use default behavior if null)</param>
		/// <returns>The <paramref name="output"/> instance, for call chaining</returns>
		/// <exception cref="Doxense.Serialization.Json.JsonSerializationException">If the object fails to serialize properly (non-serializable type, loop in the object graph, ...)</exception>
		public static TextWriter SerializeTo(TextWriter output, object? value, CrystalJsonSettings? settings = null, ICrystalJsonTypeResolver? resolver = null)
		{
			return SerializeToInternal(output, value, typeof(object), settings, resolver);
		}

		/// <summary>Serializes a value (of any type) into the specified output</summary>
		/// <param name="output">Output for the JSON document</param>
		/// <param name="value">Instance to serialize (of any type)</param>
		/// <param name="settings">Serialization settings (use default JSON settings if null)</param>
		/// <param name="resolver">Custom type resolver (use default behavior if null)</param>
		/// <returns>The <paramref name="output"/> instance, for call chaining</returns>
		/// <exception cref="Doxense.Serialization.Json.JsonSerializationException">If the object fails to serialize properly (non-serializable type, loop in the object graph, ...)</exception>
		public static TextWriter SerializeTo<T>(TextWriter output, T value, CrystalJsonSettings? settings = null, ICrystalJsonTypeResolver? resolver = null)
		{
			return SerializeToInternal(output, value, typeof(T), settings, resolver);
		}

		/// <summary>Serializes a value (of any type) into the specified stream</summary>
		/// <param name="output">Output for the JSON document</param>
		/// <param name="value">Instance to serialize (of any type)</param>
		/// <param name="settings">Serialization settings (use default JSON settings if null)</param>
		/// <param name="resolver">Custom type resolver (use default behavior if null)</param>
		/// <exception cref="Doxense.Serialization.Json.JsonSerializationException">If the object fails to serialize properly (non-serializable type, loop in the object graph, ...)</exception>
		public static void SerializeTo<T>(Stream output, T? value, CrystalJsonSettings? settings = null, ICrystalJsonTypeResolver? resolver = null)
		{
			Contract.NotNull(output);
			using (var sw = new StreamWriter(output, Utf8NoBom))
			{
				SerializeToInternal(sw, value, typeof(T), settings, resolver);
			}
		}

		/// <summary>Serializes a boxed <paramref name="value"/> (of any type) into the file at the specified <paramref name="path"/></summary>
		/// <param name="path">Path to the output file</param>
		/// <param name="value">Instance to serialize (of any type)</param>
		/// <param name="settings">Serialization settings (use default JSON settings if null)</param>
		/// <param name="resolver">Custom type resolver (use default behavior if null)</param>
		/// <param name="options">Save settings</param>
		/// <exception cref="Doxense.Serialization.Json.JsonSerializationException">If the object fails to serialize properly (non-serializable type, loop in the object graph, ...)</exception>
		public static void SaveTo(string path, object? value, CrystalJsonSettings? settings = null, ICrystalJsonTypeResolver? resolver = null, SaveOptions options = SaveOptions.None)
		{
			SerializeAndSaveInternal(path, value, typeof(object), settings, resolver, options);
		}

		/// <summary>Serializes a <paramref name="value"/> (of any type) into the file at the specified <paramref name="path"/></summary>
		/// <param name="path">Path to the output file</param>
		/// <param name="value">Instance to serialize (of any type)</param>
		/// <param name="settings">Serialization settings (use default JSON settings if null)</param>
		/// <param name="resolver">Custom type resolver (use default behavior if null)</param>
		/// <param name="options">Save settings</param>
		/// <exception cref="Doxense.Serialization.Json.JsonSerializationException">If the object fails to serialize properly (non-serializable type, loop in the object graph, ...)</exception>
		public static void SaveTo<T>(string path, T? value, CrystalJsonSettings? settings = null, ICrystalJsonTypeResolver? resolver = null, SaveOptions options = SaveOptions.None)
		{
			SerializeAndSaveInternal(path, value, typeof(T), settings, resolver, options);
		}

		/// <summary>Serializes a boxed value into an indented JSON string, suitable for humans (logging, troubleshooting, ...)</summary>
		[Pure]
		public static string Dump(object? value)
		{
			return Serialize(value, CrystalJsonSettings.JsonIndented);
		}

		/// <summary>Serializes a value into an indented JSON string, suitable for humans (logging, troubleshooting, ...)</summary>
		[Pure]
		public static string Dump<TValue>(TValue? value)
		{
			return Serialize(value, CrystalJsonSettings.JsonIndented);
		}

		/// <summary>Serializes a boxed value into an in-memory buffer</summary>
		/// <returns>Byte array that contains the resulting JSON document</returns>
		[Pure]
		public static byte[] ToBytes(object? value, CrystalJsonSettings? settings = null, ICrystalJsonTypeResolver? resolver = null)
		{
			return ToBytesInternal(value, typeof(object), settings, resolver);
		}

		/// <summary>Serializes a value into an in-memory buffer</summary>
		/// <returns>Byte array that contains the resulting JSON document</returns>
		[Pure]
		public static byte[] ToBytes<T>(T? value, CrystalJsonSettings? settings = null, ICrystalJsonTypeResolver? resolver = null)
		{
			return ToBytesInternal(value, typeof(T), settings, resolver);
		}

		[Pure]
		private static byte[] ToBytesInternal(object? value, Type declaredType, CrystalJsonSettings? settings, ICrystalJsonTypeResolver? resolver)
		{
			var bufferSize = 256;
			if (settings != null && settings.OptimizeForLargeData)
			{
				bufferSize = 0x14000; // 80 K
			}

			//REVIEW: we would need a custom TextWriter+MemoryStream combo that writes text straight to UTF8 in memory!
			// => profiling shows a lot of waste in the internal buffer of the StreamWriter, that is only used to copy again to the buffer of the MemoryStream.

			// Assumption: 80K buffer for "large" documents, 256 bytes for smaller documents
			using (var ms = new MemoryStream(bufferSize))
			{
				// note: we are serializing to memory, the size of the StreamWriter's buffer does not matter so make it as small as possible
				using (var sw = new StreamWriter(ms, Utf8NoBom, bufferSize, leaveOpen: true))
				{
					SerializeToInternal(sw, value, declaredType, settings, resolver);
				}

				if (ms.Position == 0) return [ ];
				if (ms.Position == ms.Capacity) return ms.GetBuffer();
				return ms.ToArray();
			}
		}

		/// <summary>Serializes a boxed value into an UTF-8 encoded Slice</summary>
		/// <param name="value">Instance to serialize (of any type)</param>
		/// <param name="settings">Serialization settings (use default JSON settings if null)</param>
		/// <param name="resolver">Custom Resolver utilisé pour la sérialisation (par défaut si null)</param>
		/// <returns>Slice of memory that contains the utf-8 encoded JSON document</returns>
		/// <exception cref="Doxense.Serialization.Json.JsonSerializationException">if the serialization fails</exception>
		[Pure]
		public static Slice ToSlice(object? value, CrystalJsonSettings? settings = null, ICrystalJsonTypeResolver? resolver = null)
		{
			byte[]? _ = null;
			return ToSliceInternal(value, typeof(object), settings, resolver, ref _);
		}

		/// <summary>Serializes a value into an UTF-8 encoded Slice</summary>
		/// <typeparam name="T">Advertized type of the instance.</typeparam>
		/// <param name="value">Instance to serialize (of any type)</param>
		/// <param name="settings">Serialization settings (use default JSON settings if null)</param>
		/// <param name="resolver">Custom Resolver utilisé pour la sérialisation (par défaut si null)</param>
		/// <returns>Slice of memory that contains the utf-8 encoded JSON document</returns>
		/// <remarks>
		/// <para>If <typeparamref name="T"/> is an interface or abstract class, or if <paramref name="value"/> is a derived type of <typeparamref name="T"/>, the serialized document may include an additional attribute with the original type name, which may not be recognized by other libraries or platforms.</para>
		/// </remarks>
		[Pure]
		public static Slice ToSlice<T>(T? value, CrystalJsonSettings? settings = null, ICrystalJsonTypeResolver? resolver = null)
		{
			byte[]? _ = null;
			return ToSliceInternal(value, typeof(T), settings, resolver, ref _);
		}

		/// <summary>Serializes a boxed value into an UTF-8 encoded Slice</summary>
		/// <param name="value">Instance to serialize (of any type)</param>
		/// <param name="settings">Serialization settings (use default JSON settings if null)</param>
		/// <param name="resolver">Custom Resolver utilisé pour la sérialisation (par défaut si null)</param>
		/// <param name="buffer">Pre-allocated buffer used for the serialization. If <see langword="null"/> or too small, it will be replaced with a newly allocated buffer.</param>
		/// <returns>Slice of memory that contains the utf-8 encoded JSON document</returns>
		/// <exception cref="Doxense.Serialization.Json.JsonSerializationException">if the serialization fails</exception>
		/// <remarks>
		/// <para>The Slice that is returned will use <paramref name="buffer"/> as its backing store. The caller should fully consume the result before reusing the buffer, or risk data corruption!</para>
		/// </remarks>
		[Pure]
		public static Slice ToSlice(object? value, CrystalJsonSettings? settings, ICrystalJsonTypeResolver? resolver, ref byte[]? buffer)
		{
			return ToSliceInternal(value, typeof(object), settings, resolver, ref buffer);
		}

		/// <summary>Serializes a boxed value into an UTF-8 encoded Slice</summary>
		/// <param name="value">Instance to serialize (of any type)</param>
		/// <param name="type">Advertized type of the instance, or <see langword="null"/> if it is not known.</param>
		/// <param name="settings">Serialization settings (use default JSON settings if null)</param>
		/// <param name="resolver">Custom Resolver utilisé pour la sérialisation (par défaut si null)</param>
		/// <param name="buffer">Pre-allocated buffer used for the serialization. If <see langword="null"/> or too small, it will be replaced with a newly allocated buffer.</param>
		/// <returns>Slice of memory that contains the utf-8 encoded JSON document</returns>
		/// <exception cref="Doxense.Serialization.Json.JsonSerializationException">if the serialization fails</exception>
		/// <remarks>
		/// <para>The Slice that is returned will use <paramref name="buffer"/> as its backing store. The caller <b>MUST</b> fully consume the result before reusing the buffer, or risk data corruption!</para>
		/// <para>If <paramref name="type"/> is an interface or abstract class, or if <paramref name="value"/> is a derived type of <paramref name="type"/>, the serialized document may include an additional attribute with the original type name, which may not be recognized by other libraries or platforms.</para>
		/// </remarks>
		[Pure]
		public static Slice ToSlice(object? value, Type? type, CrystalJsonSettings? settings, ICrystalJsonTypeResolver? resolver, ref byte[]? buffer)
		{
			return ToSliceInternal(value, type ?? value?.GetType() ?? typeof(object), settings, resolver, ref buffer);
		}

		/// <summary>Serializes a value into an UTF-8 encoded Slice</summary>
		/// <typeparam name="T">Advertized type of the instance.</typeparam>
		/// <param name="value">Instance to serialize (of any type)</param>
		/// <param name="settings">Serialization settings (use default JSON settings if null)</param>
		/// <param name="resolver">Custom Resolver utilisé pour la sérialisation (par défaut si null)</param>
		/// <param name="buffer">Pre-allocated buffer used for the serialization. If <see langword="null"/> or too small, it will be replaced with a newly allocated buffer.</param>
		/// <returns>Slice of memory that contains the utf-8 encoded JSON document</returns>
		/// <remarks>
		/// <para>The Slice that is returned will use <paramref name="buffer"/> as its backing store. The caller <b>MUST</b> fully consume the result before reusing the buffer, or risk data corruption!</para>
		/// <para>If <typeparamref name="T"/> is an interface or abstract class, or if <paramref name="value"/> is a derived type of <typeparamref name="T"/>, the serialized document may include an additional attribute with the original type name, which may not be recognized by other libraries or platforms.</para>
		/// </remarks>
		[Pure]
		public static Slice ToSlice<T>(T? value, CrystalJsonSettings? settings, ICrystalJsonTypeResolver? resolver, ref byte[]? buffer)
		{
			return ToSliceInternal(value, typeof(T), settings, resolver, ref buffer);
		}

		/// <summary>Serializes a boxed value into an UTF-8 encoded Slice</summary>
		/// <param name="value">Instance to serialize (of any type)</param>
		/// <param name="declaredType">Advertized type of the serialize instance.</param>
		/// <param name="settings">Serialization settings (use default JSON settings if null)</param>
		/// <param name="resolver">Custom Resolver utilisé pour la sérialisation (par défaut si null)</param>
		/// <param name="buffer">Pre-allocated buffer used for the serialization. If <see langword="null"/> or too small, it will be replaced with a newly allocated buffer.</param>
		/// <returns>Slice of memory that contains the utf-8 encoded JSON document</returns>
		/// <exception cref="Doxense.Serialization.Json.JsonSerializationException">if the serialization fails</exception>
		/// <remarks>
		/// <para>The Slice that is returned will use <paramref name="buffer"/> as its backing store. The caller <b>MUST</b> fully consume the result before reusing the buffer, or risk data corruption!</para>
		/// </remarks>
		[Pure]
		private static Slice ToSliceInternal(object? value, Type declaredType, CrystalJsonSettings? settings, ICrystalJsonTypeResolver? resolver, ref byte[]? buffer)
		{
			//REVIEW: on a besoin de coder un TextWriter+MemoryStream qui écrit directement en UTF8 en mémoire!
			// => le profiler montre qu'on gaspille beaucoup de mémoire dans le buffer du StreamWriter, qui ne fait que copier les bytes directement dans le MemoryStream,
			// pour au final recopier tout ca dans un byte[] :(

			int bufferSize = 256;
			if (settings != null && settings.OptimizeForLargeData)
			{
				bufferSize = 0x14000; // 80 K
			}
			//note: le StreamWriter va allouer un char[buffSize] et un byte[bufferSize*3 + 3] !

			if (buffer == null || buffer.Length < bufferSize)
			{ // recycle le MemoryStream
				buffer = new byte[bufferSize];
			}

			// note: vu qu'on sérialise en mémoire, la taille du buffer du StreamWriter importe peu donc autant la réduire le plus possible pour éviter d'allouer 1K par défaut !
			using (var sw = new Utf8StringWriter(new SliceWriter(buffer)))
			{
				SerializeToInternal(sw, value, declaredType, settings, resolver);
				return sw.GetBuffer();
			}
		}

		/// <summary>Serializes a boxed value into an UTF-8 encoded Slice</summary>
		/// <param name="value">Instance to serialize (of any type)</param>
		/// <param name="settings">Serialization settings (use default JSON settings if null)</param>
		/// <param name="resolver">Custom Resolver utilisé pour la sérialisation (par défaut si null)</param>
		/// <param name="pool">Pool that will provide a buffer for the result. If <see langword="null"/>, will use the default shared pool.</param>
		/// <returns>Slice of memory that contains the utf-8 encoded JSON document</returns>
		/// <exception cref="Doxense.Serialization.Json.JsonSerializationException">if the serialization fails</exception>
		/// <remarks>
		/// <para>The slice that is returned will use a buffer obtained from the <paramref name="pool"/> as its backing store. The caller <b>MUST</b> return the buffer to the pool after use, and <b>MUST NOT</b> expose this buffer in anyway after the opertaion, or risk data corruption!</para>
		/// </remarks>
		[Pure]
		public static Slice ToSlice(object? value, CrystalJsonSettings? settings, ICrystalJsonTypeResolver? resolver, ArrayPool<byte>? pool)
		{
			return ToSliceInternal(value, typeof(object), settings, resolver, pool);
		}

		/// <summary>Serializes a boxed value into an UTF-8 encoded Slice</summary>
		/// <param name="value">Instance to serialize (of any type)</param>
		/// <param name="type">Advertized type of the instance, or <see langword="null"/> if it is not known.</param>
		/// <param name="settings">Serialization settings (use default JSON settings if null)</param>
		/// <param name="resolver">Custom Resolver utilisé pour la sérialisation (par défaut si null)</param>
		/// <returns>Slice of memory that contains the utf-8 encoded JSON document</returns>
		/// <exception cref="Doxense.Serialization.Json.JsonSerializationException">if the serialization fails</exception>
		/// <remarks>
		/// <para>If <paramref name="type"/> is an interface or abstract class, or if <paramref name="value"/> is a derived type of <paramref name="type"/>, the serialized document may include an additional attribute with the original type name, which may not be recognized by other libraries or platforms.</para>
		/// </remarks>
		[Pure]
		public static Slice ToSlice(object? value, Type? type, CrystalJsonSettings? settings, ICrystalJsonTypeResolver? resolver)
		{
			byte[]? _ = null;
			return ToSliceInternal(value, type ?? value?.GetType() ?? typeof(object), settings, resolver, ref _);
		}

		/// <summary>Serializes a boxed value into an UTF-8 encoded Slice</summary>
		/// <param name="value">Instance to serialize (of any type)</param>
		/// <param name="type">Advertized type of the instance, or <see langword="null"/> if it is not known.</param>
		/// <param name="settings">Serialization settings (use default JSON settings if null)</param>
		/// <param name="resolver">Custom Resolver utilisé pour la sérialisation (par défaut si null)</param>
		/// <param name="pool">Pool that will provide a buffer for the result. If <see langword="null"/>, will use the default shared pool.</param>
		/// <returns>Slice of memory that contains the utf-8 encoded JSON document</returns>
		/// <exception cref="Doxense.Serialization.Json.JsonSerializationException">if the serialization fails</exception>
		/// <remarks>
		/// <para>The slice that is returned will use a buffer obtained from the <paramref name="pool"/> as its backing store. The caller <b>MUST</b> return the buffer to the pool after use, and <b>MUST NOT</b> expose this buffer in anyway after the opertaion, or risk data corruption!</para>
		/// <para>If <paramref name="type"/> is an interface or abstract class, or if <paramref name="value"/> is a derived type of <paramref name="type"/>, the serialized document may include an additional attribute with the original type name, which may not be recognized by other libraries or platforms.</para>
		/// </remarks>
		[Pure]
		public static Slice ToSlice(object? value, Type? type, CrystalJsonSettings? settings, ICrystalJsonTypeResolver? resolver, ArrayPool<byte>? pool)
		{
			return ToSliceInternal(value, type ?? value?.GetType() ?? typeof(object), settings, resolver, pool);
		}

		/// <summary>Serializes a boxed value into an UTF-8 encoded Slice</summary>
		/// <typeparam name="T">Advertized type of the instance.</typeparam>
		/// <param name="value">Instance to serialize (of any type)</param>
		/// <param name="settings">Serialization settings (use default JSON settings if null)</param>
		/// <param name="resolver">Custom Resolver utilisé pour la sérialisation (par défaut si null)</param>
		/// <param name="pool">Pool that will provide a buffer for the result. If <see langword="null"/>, will use the default shared pool.</param>
		/// <returns>Slice of memory that contains the utf-8 encoded JSON document</returns>
		/// <exception cref="Doxense.Serialization.Json.JsonSerializationException">if the serialization fails</exception>
		/// <remarks>
		/// <para>The slice that is returned will use a buffer obtained from the <paramref name="pool"/> as its backing store. The caller <b>MUST</b> return the buffer to the pool after use, and <b>MUST NOT</b> expose this buffer in anyway after the opertaion, or risk data corruption!</para>
		/// <para>If <typeparamref name="T"/> is an interface or abstract class, or if <paramref name="value"/> is a derived type of <typeparamref name="T"/>, the serialized document may include an additional attribute with the original type name, which may not be recognized by other libraries or platforms.</para>
		/// </remarks>
		[Pure]
		public static Slice ToSlice<T>(T? value, CrystalJsonSettings? settings, ICrystalJsonTypeResolver? resolver, ArrayPool<byte>? pool)
		{
			return ToSliceInternal(value, typeof(T), settings, resolver, pool);
		}

		/// <summary>Serializes a boxed value into an UTF-8 encoded Slice</summary>
		/// <param name="value">Instance to serialize (of any type)</param>
		/// <param name="declaredType">Advertized type of the instance, or <see langword="null"/> if it is not known.</param>
		/// <param name="settings">Serialization settings (use default JSON settings if null)</param>
		/// <param name="resolver">Custom Resolver utilisé pour la sérialisation (par défaut si null)</param>
		/// <param name="pool">Pool that will provide a buffer for the result. If <see langword="null"/>, will use the default shared pool.</param>
		/// <returns>Slice of memory that contains the utf-8 encoded JSON document</returns>
		/// <exception cref="Doxense.Serialization.Json.JsonSerializationException">if the serialization fails</exception>
		/// <remarks>
		/// <para>The slice that is returned will use a buffer obtained from the <paramref name="pool"/> as its backing store. The caller <b>MUST</b> return the buffer to the pool after use, and <b>MUST NOT</b> expose this buffer in anyway after the opertaion, or risk data corruption!</para>
		/// <para>If <paramref name="declaredType"/> is an interface or abstract class, or if <paramref name="value"/> is a derived type of <paramref name="declaredType"/>, the serialized document may include an additional attribute with the original type name, which may not be recognized by other libraries or platforms.</para>
		/// </remarks>
		[Pure]
		private static Slice ToSliceInternal(object? value, Type declaredType, CrystalJsonSettings? settings, ICrystalJsonTypeResolver? resolver, ArrayPool<byte>? pool)
		{
			//REVIEW: on a besoin de coder un TextWriter+MemoryStream qui écrit directement en UTF8 en mémoire!
			// => le profiler montre qu'on gaspille beaucoup de mémoire dans le buffer du StreamWriter, qui ne fait que copier les bytes directement dans le MemoryStream,
			// pour au final recopier tout ca dans un byte[] :(

			int bufferSize = 256;
			if (settings != null && settings.OptimizeForLargeData)
			{
				bufferSize = 0x14000; // 80 K
			}
			//note: le StreamWriter va allouer un char[buffSize] et un byte[bufferSize*3 + 3] !

			pool ??= ArrayPool<byte>.Shared;

			// note: vu qu'on sérialise en mémoire, la taille du buffer du StreamWriter importe peu donc autant la réduire le plus possible pour éviter d'allouer 1K par défaut !
			using (var sw = new Utf8StringWriter(new SliceWriter(bufferSize, pool)))
			{
				SerializeToInternal(sw, value, declaredType, settings, resolver);
				return sw.GetBuffer();
			}
		}
		/// <summary>Serializes a boxed value into a <see cref="TextWriter"/></summary>
		/// <param name="output">Destination where the serialized JSON document will be written</param>
		/// <param name="value">Instance to serialize, can be <see langword="null"/></param>
		/// <param name="declaredType">Advertized type of the instance. Use <c>typeof(object)</c> is unknown.</param>
		/// <param name="settings">Serialization settings (use default JSON settings if null)</param>
		/// <param name="resolver">Custom Resolver utilisé pour la sérialisation (par défaut si null)</param>
		/// <returns>The same instance as <paramref name="output"/></returns>
		private static TextWriter SerializeToInternal(TextWriter output, object? value, Type declaredType, CrystalJsonSettings? settings, ICrystalJsonTypeResolver? resolver)
		{
			Contract.NotNull(output);

			if (value == null)
			{ // cas spécial pour null
				output.Write(JsonTokens.Null);
			}
			else
			{
				var writer = new CrystalJsonWriter(output, settings, resolver);
				if (value is JsonValue jval)
				{ // shortcurt pour la sérialisation de DOM json
					jval.JsonSerialize(writer);
				}
				else
				{
					CrystalJsonVisitor.VisitValue(value, declaredType, writer);
				}
			}
			return output;
		}

		/// <summary>Creates a new <see cref="StreamWriter"/> that can be used to serialize JSON into a file, with the specified settings</summary>
		/// <param name="path">Path to the output file</param>
		/// <param name="settings">Serialization settings (use default JSON settings if null)</param>
		/// <returns>Writer that will output bytes into the specified file</returns>
		/// <remarks>The stream will be tuned according to the serialization settings used, especially if planning to write a large amount of data.</remarks>
		private static StreamWriter OpenJsonStreamWriter(string path, CrystalJsonSettings? settings)
		{
			var bufferSize = 0x400;
			if (settings != null && settings.OptimizeForLargeData)
			{
				bufferSize = 0x14000; // 80K
			}
			//note: le StreamWriter va allouer un char[bufferSize] et un byte[3*bufferSize + 3]!

			FileStream? fileStream = null;
			try
			{
				fileStream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None, bufferSize << 2, FileOptions.SequentialScan | FileOptions.WriteThrough);

				// note: C'est le StreamWriter va fermera le FileStream quand il sera Dispose()
				return new StreamWriter(fileStream, Encoding.UTF8, bufferSize); //REVIEW: To BOM or not to BOM ?
			}
			catch (Exception)
			{
				fileStream?.Dispose();
				throw;
			}
		}

		private static void SerializeAndSaveInternal(string path, object? value, Type declaredType, CrystalJsonSettings? settings, ICrystalJsonTypeResolver? resolver, SaveOptions options)
		{
			Contract.NotNullOrEmpty(path);
			path = Path.GetFullPath(path);

			string savePath = path;
			string? bakPath = null;

			if ((options & SaveOptions.Append) == SaveOptions.Append)
			{ //TODO: Gérer le mode append !
				throw new NotSupportedException("Append save is not supported");
			}

			#region Settings...

			bool doAtomicUpdate = false;
			if (File.Exists(path))
			{ // Le fichier existe déjà, il va falloir faire un replace
				if ((options & SaveOptions.AtomicSave) == SaveOptions.AtomicSave)
				{
					doAtomicUpdate = true;
					savePath += ".new";
				}
				if ((options & SaveOptions.KeepBackup) == SaveOptions.KeepBackup)
				{
					bakPath = savePath + ".bak";
				}
			}
			else
			{ // Le fichier n'existait pas, on va vérifier si le répertoire parent existe, et si ce n'est pas le cas, on le crée
				string parent = Path.GetDirectoryName(path)!;
				Contract.Debug.Assert(parent != null);
				if (!Directory.Exists(parent))
				{
					Directory.CreateDirectory(parent);
				}
			}

			#endregion

			// note: si on est arrivé jusqu'ici, c'est que le chemin pointe bien vers un répertoire valide,
			// donc le reste du code n'a pas a se préoccuper des erreurs venant de problèmes de chemins

			// Différents scenarios:
			if (doAtomicUpdate)
			{ // Remplace de manière atomique le fichier:
				// * On sauve les données dans un fichier temporaire
				// * On swap l'ancien et le nouveau après écriture des données (et on backup l'ancien si besoin, sinon il est supprimé)
				// * En cas d'erreur, on supprime le fichier temporaire et il n'y a rien d'autre a faire

				try
				{
					using (var output = OpenJsonStreamWriter(savePath, settings))
					{
						SerializeToInternal(output, value, declaredType, settings, resolver);
						// note: certains implémentation "bugguée" de streams (GzipStream, etc..) requiert un flush pour finir d'écrire les data...

						output.Flush();
					}
				}
				catch (Exception)
				{
					if (File.Exists(savePath)) File.Delete(savePath);
					throw;
				}

				// swap les fichiers (update atomique, mais sans retombées radio-actives)
				File.Replace(savePath, path, bakPath);
				// note: si ca foire... tant pis :)

			}
			else if (bakPath != null)
			{ // Ecrase le fichier, mais en gardant un backup du précédent:
				// * On renomme l'ancien fichier en backup (en écrasant le backup précédent s'il y en a un)
				// * On sauve les données dans le fichier de destination
				// * En cas d'erreur, on supprime le fichier généré, et on restaure le backup

				bool swapped = false;
				try
				{
					File.Replace(savePath, bakPath, null);
					swapped = true;

					using (var output = OpenJsonStreamWriter(savePath, settings))
					{
						SerializeToInternal(output, value, declaredType, settings, resolver);
					}
				}
				catch (Exception)
				{
					if (swapped)
					{ // remet le backup en place!
						File.Replace(bakPath, savePath, null);
					}
					throw;
				}

			}
			else
			{ // Le fichier n'existait pas, on le sauve directement
				// * On sauve les données dans le fichier de destination
				// * En cas d'erreur, on le supprime

				try
				{
					using (var output = OpenJsonStreamWriter(savePath, settings))
					{
						SerializeToInternal(output, value, declaredType, settings, resolver);
					}
				}
				catch (Exception)
				{
					if (File.Exists(savePath)) File.Delete(savePath);
					throw;
				}
			}
		}

		#endregion

		#region Parsing...

		/// <summary>Parses a JSON text literal, and returns the corresponding JSON value</summary>
		/// <param name="jsonText">JSON text document to parse</param>
		/// <param name="settings">Serialization settings (use default JSON settings if null)</param>
		/// <returns>Corresponding JSON value. If <paramref name="jsonText"/> is empty, will return <see cref="JsonNull.Missing"/></returns>
		/// <remarks>
		/// <para>The value may be mutable (for objects and arrays) and can be modified. If you require an immutable thread-safe value, please configure the <paramref name="settings"/> accordingly.</para>
		/// <para>If the result is always expected to be an Array or an Object, please call call <see cref="JsonValueExtensions.AsArray"/> or <see cref="JsonValueExtensions.AsObject"/> on the result.</para>
		/// </remarks>
		/// <exception cref="FormatException">If the JSON document is not syntaxically correct.</exception>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static JsonValue Parse(
#if NET8_0_OR_GREATER
			[System.Diagnostics.CodeAnalysis.StringSyntax("json")]
#endif
			string? jsonText,
			CrystalJsonSettings? settings = null
		)
		{
#if DEBUG_JSON_PARSER
			Debug.WriteLine("CrystalJson.Parse('" + jsonText + "', ...)");
#endif
			return ParseFromReader(new JsonStringReader(jsonText), settings);
		}

		/// <summary>Parses a JSON buffer, and returns the corresponding JSON value</summary>
		/// <param name="jsonBytes">UTF-8 encoded JSON document to parse</param>
		/// <param name="settings">Serialization settings (use default JSON settings if null)</param>
		/// <returns>Corresponding JSON value. If <paramref name="jsonBytes"/> is empty, will return <see cref="JsonNull.Missing"/></returns>
		/// <remarks>
		/// <para>The value may be mutable (for objects and arrays) and can be modified. If you require an immutable thread-safe value, please configure the <paramref name="settings"/> accordingly.</para>
		/// <para>If the result is always expected to be an Array or an Object, please call call <see cref="JsonValueExtensions.AsArray"/> or <see cref="JsonValueExtensions.AsObject"/> on the result.</para>
		/// </remarks>
		/// <exception cref="FormatException">If the JSON document is not syntaxically correct.</exception>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static JsonValue Parse(byte[]? jsonBytes, CrystalJsonSettings? settings = null)
		{
			return Parse(jsonBytes.AsSlice(), settings);
		}

		/// <summary>Parses a JSON buffer, and returns the corresponding JSON value</summary>
		/// <param name="jsonBytes">UTF-8 encoded JSON document to parse</param>
		/// <param name="settings">Serialization settings (use default JSON settings if null)</param>
		/// <returns>Corresponding JSON value. If <paramref name="jsonBytes"/> is empty, will return <see cref="JsonNull.Missing"/></returns>
		/// <remarks>
		/// <para>The value may be mutable (for objects and arrays) and can be modified. If you require an immutable thread-safe value, please configure the <paramref name="settings"/> accordingly.</para>
		/// <para>If the result is always expected to be an Array or an Object, please call call <see cref="JsonValueExtensions.AsArray"/> or <see cref="JsonValueExtensions.AsObject"/> on the result.</para>
		/// </remarks>
		/// <exception cref="FormatException">If the JSON document is not syntaxically correct.</exception>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static JsonValue Parse(Slice jsonBytes, CrystalJsonSettings? settings = null)
		{
			return ParseFromReader(new JsonSliceReader(jsonBytes), settings);
		}

		/// <summary>Parses a JSON buffer, and returns the corresponding JSON value</summary>
		/// <param name="jsonBytes">UTF-8 encoded JSON document to parse</param>
		/// <param name="settings">Serialization settings (use default JSON settings if null)</param>
		/// <returns>Corresponding JSON value. If <paramref name="jsonBytes"/> is empty, will return <see cref="JsonNull.Missing"/></returns>
		/// <remarks>
		/// <para>The value may be mutable (for objects and arrays) and can be modified. If you require an immutable thread-safe value, please configure the <paramref name="settings"/> accordingly.</para>
		/// <para>If the result is always expected to be an Array or an Object, please call call <see cref="JsonValueExtensions.AsArray"/> or <see cref="JsonValueExtensions.AsObject"/> on the result.</para>
		/// </remarks>
		/// <exception cref="FormatException">If the JSON document is not syntaxically correct.</exception>
		[Pure]
		public static JsonValue Parse(ReadOnlySpan<byte> jsonBytes, CrystalJsonSettings? settings = null)
		{
			unsafe
			{
				fixed (byte* first = jsonBytes)
				{
					return ParseFromReader(new JsonUnmanagedReader(first, jsonBytes.Length), settings);
				}
			}
		}

		/// <summary>Parses a JSON buffer, and returns the corresponding JSON value</summary>
		/// <param name="jsonBytes">UTF-8 encoded JSON document to parse</param>
		/// <param name="settings">Serialization settings (use default JSON settings if null)</param>
		/// <returns>Corresponding JSON value. If <paramref name="jsonBytes"/> is empty, will return <see cref="JsonNull.Missing"/></returns>
		/// <remarks>
		/// <para>The value may be mutable (for objects and arrays) and can be modified. If you require an immutable thread-safe value, please configure the <paramref name="settings"/> accordingly.</para>
		/// <para>If the result is always expected to be an Array or an Object, please call call <see cref="JsonValueExtensions.AsArray"/> or <see cref="JsonValueExtensions.AsObject"/> on the result.</para>
		/// </remarks>
		/// <exception cref="FormatException">If the JSON document is not syntaxically correct.</exception>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static JsonValue Parse(ReadOnlyMemory<byte> jsonBytes, CrystalJsonSettings? settings = null)
		{
			return Parse(jsonBytes.Span, settings);
		}

		/// <summary>Parses a JSON sequence of buffer, and returns the corresponding JSON value</summary>
		/// <param name="jsonBytes">Sequence of buffers containing the UTF-8 encoded JSON document to parse</param>
		/// <param name="settings">Serialization settings (use default JSON settings if null)</param>
		/// <returns>Corresponding JSON value. If <paramref name="jsonBytes"/> is empty, will return <see cref="JsonNull.Missing"/></returns>
		/// <remarks>
		/// <para>The value may be mutable (for objects and arrays) and can be modified. If you require an immutable thread-safe value, please configure the <paramref name="settings"/> accordingly.</para>
		/// <para>If the result is always expected to be an Array or an Object, please call call <see cref="JsonValueExtensions.AsArray"/> or <see cref="JsonValueExtensions.AsObject"/> on the result.</para>
		/// </remarks>
		/// <exception cref="FormatException">If the JSON document is not syntaxically correct.</exception>
		[Pure]
		public static JsonValue Parse(ref ReadOnlySequence<byte> jsonBytes, CrystalJsonSettings? settings = null)
		{
			if (jsonBytes.IsSingleSegment)
			{
				return Parse(jsonBytes.First.Span, settings);
			}

			//TODO: un reader de ReadOnlySequence<byte>?
			// en attendant, on va copier les data dans un buffer pooled...
			long len = jsonBytes.Length;
			if (len > int.MaxValue) throw new NotSupportedException("Cannot parse sequence of bytes larger than 2 GiB.");
			using (var scratch = MemoryPool<byte>.Shared.Rent((int) len))
			{
				var mem = scratch.Memory.Span;
				int p = 0;
				foreach (var chunk in jsonBytes)
				{
					chunk.Span.CopyTo(mem.Slice(p, chunk.Length));
					p += chunk.Length;
				}
				Contract.Debug.Assert(p == mem.Length);
				return Parse(mem, settings);
			}
		}

		/// <summary>Parse un buffer contenant du JSON</summary>
		/// <param name="jsonText">Bloc de données contenant du texte JSON</param>
		/// <param name="settings">Serialization settings (use default JSON settings if null)</param>
		/// <returns>Valeur JSON correspondante</returns>
		[Pure]
		public static JsonValue Parse(ReadOnlySpan<char> jsonText, CrystalJsonSettings? settings = null)
		{
			unsafe
			{
				fixed (char* first = jsonText)
				{
					return ParseFromReader(new JsonCharReader(first, jsonText.Length), settings);
				}
			}
		}

		/// <summary>Parses a JSON text literal, and returns the corresponding JSON value</summary>
		/// <param name="jsonText">JSON text document to parse</param>
		/// <param name="settings">Serialization settings (use default JSON settings if null)</param>
		/// <returns>Corresponding JSON value. If <paramref name="jsonText"/> is empty, will return <see cref="JsonNull.Missing"/></returns>
		/// <remarks>
		/// <para>The value may be mutable (for objects and arrays) and can be modified. If you require an immutable thread-safe value, please configure the <paramref name="settings"/> accordingly.</para>
		/// <para>If the result is always expected to be an Array or an Object, please call call <see cref="JsonValueExtensions.AsArray"/> or <see cref="JsonValueExtensions.AsObject"/> on the result.</para>
		/// </remarks>
		/// <exception cref="FormatException">If the JSON document is not syntaxically correct.</exception>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static JsonValue Parse(ReadOnlyMemory<char> jsonText, CrystalJsonSettings? settings = null)
		{
			return Parse(jsonText.Span, settings);
		}

		/// <summary>Parses a JSON text literal sequence, and returns the corresponding JSON value</summary>
		/// <param name="jsonText">Sequence of buffers containing the JSON text document to parse</param>
		/// <param name="settings">Serialization settings (use default JSON settings if null)</param>
		/// <returns>Corresponding JSON value. If <paramref name="jsonText"/> is empty, will return <see cref="JsonNull.Missing"/></returns>
		/// <remarks>
		/// <para>The value may be mutable (for objects and arrays) and can be modified. If you require an immutable thread-safe value, please configure the <paramref name="settings"/> accordingly.</para>
		/// <para>If the result is always expected to be an Array or an Object, please call call <see cref="JsonValueExtensions.AsArray"/> or <see cref="JsonValueExtensions.AsObject"/> on the result.</para>
		/// </remarks>
		/// <exception cref="FormatException">If the JSON document is not syntaxically correct.</exception>
		[Pure]
		public static JsonValue Parse(ref ReadOnlySequence<char> jsonText, CrystalJsonSettings? settings = null)
		{
			if (jsonText.IsSingleSegment)
			{
				return Parse(jsonText.First.Span, settings);
			}

			//TODO: un reader de ReadOnlySequence<char>?
			// en attendant, on va copier les data dans un buffer pooled...
			long len = jsonText.Length;
			if (len > int.MaxValue) throw new NotSupportedException("Cannot parse sequence of chars larger than 4 GiB.");
			using (var scratch = MemoryPool<char>.Shared.Rent((int) len))
			{
				var mem = scratch.Memory.Span;
				int p = 0;
				foreach (var chunk in jsonText)
				{
					chunk.Span.CopyTo(mem.Slice(p, chunk.Length));
					p += chunk.Length;
				}
				Contract.Debug.Assert(p == mem.Length);
				return Parse(mem, settings);
			}
		}

		/// <summary>Reads the content of a reader, and returns the corresponding JSON value</summary>
		/// <param name="reader">Instance from which to read the JSON text document</param>
		/// <param name="settings">Serialization settings (use default JSON settings if null)</param>
		/// <returns>Corresponding JSON value. If <paramref name="reader"/> is empty, will return <see cref="JsonNull.Missing"/></returns>
		/// <remarks>
		/// <para>The value may be mutable (for objects and arrays) and can be modified. If you require an immutable thread-safe value, please configure the <paramref name="settings"/> accordingly.</para>
		/// <para>If the result is always expected to be an Array or an Object, please call call <see cref="JsonValueExtensions.AsArray"/> or <see cref="JsonValueExtensions.AsObject"/> on the result.</para>
		/// </remarks>
		/// <exception cref="FormatException">If the JSON document is not syntaxically correct.</exception>
		[Pure]
		public static JsonValue ParseFrom(TextReader reader, CrystalJsonSettings? settings = null)
		{
			Contract.NotNull(reader);
			return ParseFromReader(new JsonTextReader(reader), settings);
		}

		/// <summary>Reads the content of a reader, and returns the corresponding JSON value</summary>
		/// <param name="source">Instance from which to read the JSON text document</param>
		/// <param name="settings">Serialization settings (use default JSON settings if null)</param>
		/// <returns>Corresponding JSON value. If <paramref name="source"/> is empty, will return <see cref="JsonNull.Missing"/></returns>
		/// <remarks>
		/// <para>The value may be mutable (for objects and arrays) and can be modified. If you require an immutable thread-safe value, please configure the <paramref name="settings"/> accordingly.</para>
		/// <para>If the result is always expected to be an Array or an Object, please call call <see cref="JsonValueExtensions.AsArray"/> or <see cref="JsonValueExtensions.AsObject"/> on the result.</para>
		/// </remarks>
		/// <exception cref="FormatException">If the JSON document is not syntaxically correct.</exception>
		[Pure]
		internal static JsonValue ParseFromReader<TReader>(TReader source, CrystalJsonSettings? settings = null)
			where TReader : struct, IJsonReader
		{
			Contract.NotNullAllowStructs(source);

			var tokenizer = default(CrystalJsonTokenizer<TReader>);
			try
			{
				tokenizer = new CrystalJsonTokenizer<TReader>(source, settings ?? CrystalJsonSettings.Json);
				return CrystalJsonParser<TReader>.ParseJsonValue(ref tokenizer) ?? JsonNull.Missing;
			}
			finally
			{
				tokenizer.Dispose();
			}
		}

		/// <summary>Reads the content of a file, and returns the corresponding JSON value</summary>
		/// <param name="path">Instance from which to read the JSON document</param>
		/// <param name="settings">Serialization settings (use default JSON settings if null)</param>
		/// <param name="options">Options used for streaming operations.</param>
		/// <returns>Corresponding JSON value. If <paramref name="path"/> is empty, will return <see cref="JsonNull.Missing"/></returns>
		/// <remarks>
		/// <para>The value may be mutable (for objects and arrays) and can be modified. If you require an immutable thread-safe value, please configure the <paramref name="settings"/> accordingly.</para>
		/// <para>If the result is always expected to be an Array or an Object, please call call <see cref="JsonValueExtensions.AsArray"/> or <see cref="JsonValueExtensions.AsObject"/> on the result.</para>
		/// </remarks>
		/// <exception cref="FormatException">If the JSON document is not syntaxically correct.</exception>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static JsonValue ParseFrom(string path, CrystalJsonSettings? settings = null, LoadOptions options = LoadOptions.None)
		{
			return LoadAndParseInternal(path, settings, options);
		}

		/// <summary>Reads the content of a file, and returns the corresponding JSON value</summary>
		/// <param name="source">Instance from which to read the JSON document</param>
		/// <param name="settings">Serialization settings (use default JSON settings if null)</param>
		/// <returns>Corresponding JSON value. If <paramref name="source"/> is empty, will return <see cref="JsonNull.Missing"/></returns>
		/// <remarks>
		/// <para>The value may be mutable (for objects and arrays) and can be modified. If you require an immutable thread-safe value, please configure the <paramref name="settings"/> accordingly.</para>
		/// <para>If the result is always expected to be an Array or an Object, please call call <see cref="JsonValueExtensions.AsArray"/> or <see cref="JsonValueExtensions.AsObject"/> on the result.</para>
		/// </remarks>
		/// <exception cref="FormatException">If the JSON document is not syntaxically correct.</exception>
		[Pure]
		public static JsonValue ParseFrom(Stream source, CrystalJsonSettings? settings = null)
		{
			Contract.NotNull(source);

			//REVIEW: on pourrait détecter un MemoryStream et directement lire le buffer s'il est accessible, mais il faut s'assurer
			// que dans tous les cas (succès ou erreur), on seek le MemoryStream exactement comme si on l'avait consommé directement !

			using (var reader = new StreamReader(source, Encoding.UTF8, true))
			{
				return ParseFromReader(new JsonTextReader(reader), settings);
			}
		}

		[Pure, ContractAnnotation("null => false")]
		public static bool MaybeJsonDocument(byte[]? jsonBytes)
		{
			return jsonBytes != null && MaybeJsonDocument(jsonBytes.AsSlice());
		}

		[Pure, ContractAnnotation("jsonBytes:null => false")]
		public static bool MaybeJsonDocument(byte[]? jsonBytes, int offset, int count)
		{
			return jsonBytes != null && MaybeJsonDocument(jsonBytes.AsSlice(offset, count));
		}

		/// <summary>Essayes de déterminer si le buffer contient un document JSON (object ou array)</summary>
		/// <param name="jsonBytes">Buffer contenant un document JSON (encodé en UTF-8 ou ASCII)</param>
		/// <returns>True si le document pourrait être du JSON (object "{...}" ou array "[...]")</returns>
		/// <remarks>Attention: L'heuristique ne garantit pas qu'il s'agit d'un document valide!</remarks>
		[Pure]
		public static bool MaybeJsonDocument(Slice jsonBytes)
		{
			if (jsonBytes.Count < 2) return false;

			// cela peut "null"
			if (jsonBytes.Count == 4
			 && jsonBytes[0] == 110 /*'n'*/
			 && jsonBytes[1] == 117 /*'u'*/
			 && jsonBytes[2] == 108 /*'l'*/
			 && jsonBytes[3] == 108 /*'l'*/)
				return true;

			// on recup le premier et dernier caractère valide (en skippant les espaces de chaque coté)
			int p = jsonBytes.Offset;
			int end = jsonBytes.Offset + jsonBytes.Count;
			char first = (char) jsonBytes.Array[p++];
			while (char.IsWhiteSpace(first) && p < end)
			{
				first = (char) jsonBytes.Array[p++];
			}

			p = end - 1;
			char last = (char) jsonBytes.Array[p--];
			while (char.IsWhiteSpace(last) && p >= jsonBytes.Offset)
			{
				last = (char) jsonBytes.Array[p--];
			}

			// il faut que ca commence par "{" ou "["
			return (first == '{' && last == '}') || (first == '[' && last == ']');
		}

		/// <summary>Crée un reader sur un fichier sur le disque</summary>
		/// <param name="path">Chemin du fichier à lire</param>
		/// <param name="settings">Serialization settings (use default JSON settings if null)</param>
		/// <returns>Reader prêt à lire depuis le fichier</returns>
		[Pure]
		private static StreamReader OpenJsonStreamReader(string path, CrystalJsonSettings? settings)
		{
			var bufferSize = 0x1000; // x4 = 16K
			if (settings != null && settings.OptimizeForLargeData)
			{
				bufferSize = 0x8000; // x4 = 128k
			}

			FileStream? fileStream = null;
			try
			{
				fileStream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize << 2, FileOptions.SequentialScan);

				// note: C'est le StreamWriter va fermera le FileStream quand il sera Dispose()
				return new StreamReader(fileStream, Encoding.UTF8, true, bufferSize);
			}
			catch (Exception)
			{
				fileStream?.Dispose();
				throw;
			}
		}

		[Pure]
		private static JsonValue LoadAndParseInternal(string path, CrystalJsonSettings? settings, LoadOptions options)
		{
			Contract.NotNullOrEmpty(path);
			path = Path.GetFullPath(path);

			if (!File.Exists(path))
			{ // Le fichier n'existe pas

				if ((options & LoadOptions.ReturnNullIfMissing) == LoadOptions.ReturnNullIfMissing)
				{ // L'appelant nous a dit de traiter ce cas comme si le fichier contenait 'null'
					return JsonNull.Missing;
				}
				// 404'ed !
				throw new FileNotFoundException("Specified JSON file could not be found", path);
			}

			using (var reader = OpenJsonStreamReader(path, settings))
			{
				return ParseFromReader(new JsonTextReader(reader), settings);
			}
		}

		#endregion

		#region Deserialization...

		#region Désérialisation directe...

		/// <summary>Désérialise une chaîne de texte JSON en l'objet CLR le plus approprié</summary>
		/// <param name="jsonText">Texte JSON à désérialiser</param>
		/// <param name="settings">Serialization settings (use default JSON settings if null)</param>
		/// <param name="resolver">Resolver optionnel</param>
		/// <returns>Objet correspondant (dont le type dépend du contexte)</returns>
		/// <exception cref="FormatException">En cas d'erreur de parsing JSON</exception>
		/// <remarks>A n'utiliser que si vous ne connaissez absolument pas le type attendu!</remarks>
		[Pure]
		[Obsolete("Please avoid doing untyped deserialization!")]
		public static object? DeserializeBoxed(string jsonText, CrystalJsonSettings? settings = null, ICrystalJsonTypeResolver? resolver = null)
		{
			return BindBoxed(Parse(jsonText, settings), null, resolver);
		}

		/// <summary>Désérialise une valeure JSON en l'objet CLR le plus approprié</summary>
		/// <param name="value">Valeure JSON à désérialiser</param>
		/// <param name="type"></param>
		/// <param name="resolver">Resolver optionnel</param>
		/// <returns>Objet correspondant (dont le type dépend du contexte)</returns>
		/// <remarks>A n'utiliser que si vous ne connaissez absolument pas le type attendu!</remarks>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static object? BindBoxed(JsonValue? value, Type? type, ICrystalJsonTypeResolver? resolver = null)
		{
			return value == null ? null : (resolver ?? CrystalJson.DefaultResolver).BindJsonValue(type, value);
		}

		#endregion

		#region Désérialisation vers un type défini

		/// <summary>Dé-sérialise une chaine de texte JSON vers un type défini</summary>
		/// <param name="jsonText">Texte JSON à dé-sérialiser</param>
		/// <returns>Objet correspondant</returns>
		/// <exception cref="FormatException">En cas d'erreur de parsing JSON</exception>
		/// <exception cref="InvalidOperationException">If the JSON document is <c>"null"</c></exception>
		[Pure]
		public static TValue Deserialize<TValue>(
#if NET8_0_OR_GREATER
			[System.Diagnostics.CodeAnalysis.StringSyntax("json")]
#endif
			string jsonText
		) where TValue : notnull
		{
			return Parse(jsonText).Required<TValue>(resolver: null);
		}

		/// <summary>Dé-sérialise une chaine de texte JSON vers un type défini</summary>
		/// <param name="jsonText">Texte JSON à dé-sérialiser</param>
		/// <param name="settings">Serialization settings (use default JSON settings if null)</param>
		/// <param name="resolver">Resolver optionnel</param>
		/// <returns>Objet correspondant</returns>
		/// <exception cref="FormatException">En cas d'erreur de parsing JSON</exception>
		/// <exception cref="InvalidOperationException">If the JSON document is <c>"null"</c></exception>
		[Pure]
		public static TValue Deserialize<TValue>(
#if NET8_0_OR_GREATER
			[System.Diagnostics.CodeAnalysis.StringSyntax("json")]
#endif
			string jsonText,
			CrystalJsonSettings? settings = null,
			ICrystalJsonTypeResolver? resolver = null
		) where TValue : notnull
		{
			return Parse(jsonText, settings).Required<TValue>(resolver);
		}

		/// <summary>Dé-sérialise une chaine de texte JSON vers un type défini</summary>
		/// <param name="jsonText">Texte JSON à dé-sérialiser</param>
		/// <param name="defaultValue"></param>
		/// <returns>Objet correspondant</returns>
		/// <exception cref="FormatException">En cas d'erreur de parsing JSON</exception>
		/// <exception cref="InvalidOperationException">If the JSON document is <c>"null"</c></exception>
		[Pure]
		[return: NotNullIfNotNull(nameof(defaultValue))]
		public static TValue? Deserialize<TValue>(
#if NET8_0_OR_GREATER
			[System.Diagnostics.CodeAnalysis.StringSyntax("json")]
#endif
			string jsonText,
			TValue defaultValue)
		{
			return Parse(jsonText).As(defaultValue);
		}

		/// <summary>Dé-sérialise une chaine de texte JSON vers un type défini</summary>
		/// <param name="jsonText">Texte JSON à dé-sérialiser</param>
		/// <param name="defaultValue"></param>
		/// <param name="settings">Serialization settings (use default JSON settings if null)</param>
		/// <param name="resolver">Resolver optionnel</param>
		/// <returns>Objet correspondant</returns>
		/// <exception cref="FormatException">En cas d'erreur de parsing JSON</exception>
		/// <exception cref="InvalidOperationException">If the JSON document is <c>"null"</c></exception>
		[Pure]
		[return: NotNullIfNotNull(nameof(defaultValue))]
		public static TValue? Deserialize<TValue>(
#if NET8_0_OR_GREATER
			[System.Diagnostics.CodeAnalysis.StringSyntax("json")]
#endif
			string jsonText,
			TValue defaultValue,
			CrystalJsonSettings? settings = null,
			ICrystalJsonTypeResolver? resolver = null
		)
		{
			return Parse(jsonText, settings).As(defaultValue, resolver);
		}

		/// <summary>Dé-sérialise une source de texte JSON vers un type défini</summary>
		/// <param name="jsonBytes">Buffer contenant du texte JSON encodé en UTF-8</param>
		/// <returns>Objet correspondant</returns>
		/// <exception cref="FormatException">En cas d'erreur de parsing JSON</exception>
		/// <exception cref="InvalidOperationException">If the JSON document is <c>"null"</c></exception>
		[Pure]
		public static TValue Deserialize<TValue>(byte[] jsonBytes) where TValue : notnull
		{
			return Parse(jsonBytes).Required<TValue>();
		}

		/// <summary>Dé-sérialise une source de texte JSON vers un type défini</summary>
		/// <param name="jsonBytes">Buffer contenant du texte JSON encodé en UTF-8</param>
		/// <param name="settings">Serialization settings (use default JSON settings if null)</param>
		/// <param name="resolver">Resolver optionnel</param>
		/// <returns>Objet correspondant</returns>
		/// <exception cref="FormatException">En cas d'erreur de parsing JSON</exception>
		/// <exception cref="InvalidOperationException">If the JSON document is <c>"null"</c></exception>
		[Pure]
		public static TValue Deserialize<TValue>(byte[] jsonBytes, CrystalJsonSettings? settings = null, ICrystalJsonTypeResolver? resolver = null) where TValue : notnull
		{
			return Parse(jsonBytes, settings).Required<TValue>(resolver);
		}

		/// <summary>Dé-sérialise une source de texte JSON vers un type défini</summary>
		/// <param name="jsonBytes">Buffer contenant du texte JSON encodé en UTF-8</param>
		/// <param name="defaultValue"></param>
		/// <returns>Objet correspondant</returns>
		/// <exception cref="FormatException">En cas d'erreur de parsing JSON</exception>
		/// <exception cref="InvalidOperationException">If the JSON document is <c>"null"</c></exception>
		[Pure]
		[return: NotNullIfNotNull(nameof(defaultValue))]
		public static TValue? Deserialize<TValue>(byte[] jsonBytes, TValue defaultValue)
		{
			return Parse(jsonBytes).As(defaultValue);
		}

		/// <summary>Dé-sérialise une source de texte JSON vers un type défini</summary>
		/// <param name="jsonBytes">Buffer contenant du texte JSON encodé en UTF-8</param>
		/// <param name="defaultValue"></param>
		/// <param name="settings">Serialization settings (use default JSON settings if null)</param>
		/// <param name="resolver">Resolver optionnel</param>
		/// <returns>Objet correspondant</returns>
		/// <exception cref="FormatException">En cas d'erreur de parsing JSON</exception>
		/// <exception cref="InvalidOperationException">If the JSON document is <c>"null"</c></exception>
		[Pure]
		[return: NotNullIfNotNull(nameof(defaultValue))]
		public static TValue? Deserialize<TValue>(byte[] jsonBytes, TValue defaultValue, CrystalJsonSettings? settings = null, ICrystalJsonTypeResolver? resolver = null)
		{
			return Parse(jsonBytes, settings).As(defaultValue, resolver);
		}

		/// <summary>Dé-sérialise une source de texte JSON vers un type défini</summary>
		/// <param name="jsonBytes">Buffer contenant du texte JSON encodé en UTF-8</param>
		/// <returns>Objet correspondant</returns>
		/// <exception cref="FormatException">En cas d'erreur de parsing JSON</exception>
		[Pure]
		public static TValue Deserialize<TValue>(Slice jsonBytes) where TValue : notnull
		{
			return Parse(jsonBytes).Required<TValue>(resolver: null);
		}

		/// <summary>Dé-sérialise une source de texte JSON vers un type défini</summary>
		/// <param name="jsonBytes">Buffer contenant du texte JSON encodé en UTF-8</param>
		/// <param name="settings">Serialization settings (use default JSON settings if null)</param>
		/// <param name="resolver">Resolver optionnel</param>
		/// <returns>Objet correspondant</returns>
		/// <exception cref="FormatException">En cas d'erreur de parsing JSON</exception>
		[Pure]
		public static TValue Deserialize<TValue>(Slice jsonBytes, CrystalJsonSettings? settings = null, ICrystalJsonTypeResolver? resolver = null) where TValue : notnull
		{
			return Parse(jsonBytes, settings).Required<TValue>(resolver);
		}

		/// <summary>Dé-sérialise une source de texte JSON vers un type défini</summary>
		/// <param name="jsonBytes">Buffer contenant du texte JSON encodé en UTF-8</param>
		/// <param name="defaultValue"></param>
		/// <returns>Objet correspondant</returns>
		/// <exception cref="FormatException">En cas d'erreur de parsing JSON</exception>
		[Pure]
		[return: NotNullIfNotNull(nameof(defaultValue))]
		public static TValue? Deserialize<TValue>(Slice jsonBytes, TValue defaultValue)
		{
			return Parse(jsonBytes).As(defaultValue);
		}

		/// <summary>Dé-sérialise une source de texte JSON vers un type défini</summary>
		/// <param name="jsonBytes">Buffer contenant du texte JSON encodé en UTF-8</param>
		/// <param name="defaultValue"></param>
		/// <param name="settings">Serialization settings (use default JSON settings if null)</param>
		/// <param name="resolver">Resolver optionnel</param>
		/// <returns>Objet correspondant</returns>
		/// <exception cref="FormatException">En cas d'erreur de parsing JSON</exception>
		[Pure]
		[return: NotNullIfNotNull(nameof(defaultValue))]
		public static TValue? Deserialize<TValue>(Slice jsonBytes, TValue defaultValue, CrystalJsonSettings? settings = null, ICrystalJsonTypeResolver? resolver = null)
		{
			return Parse(jsonBytes, settings).As(defaultValue, resolver);
		}

		/// <summary>Désérialise une source de texte JSON vers un type défini</summary>
		/// <param name="jsonBytes">Buffer contenant du texte JSON encodé en UTF-8</param>
		/// <param name="settings">Serialization settings (use default JSON settings if null)</param>
		/// <param name="resolver">Resolver optionnel</param>
		/// <returns>Objet correspondant</returns>
		/// <exception cref="FormatException">En cas d'erreur de parsing JSON</exception>
		[Pure]
		public static TValue Deserialize<TValue>(ReadOnlySpan<byte> jsonBytes, CrystalJsonSettings? settings = null, ICrystalJsonTypeResolver? resolver = null) where TValue : notnull
		{
			return Parse(jsonBytes, settings).Required<TValue>(resolver);
		}

		/// <summary>Désérialise une source de texte JSON vers un type défini</summary>
		/// <param name="jsonBytes">Buffer contenant du texte JSON encodé en UTF-8</param>
		/// <param name="defaultValue"></param>
		/// <returns>Objet correspondant</returns>
		/// <exception cref="FormatException">En cas d'erreur de parsing JSON</exception>
		[Pure]
		[return: NotNullIfNotNull(nameof(defaultValue))]
		public static TValue? Deserialize<TValue>(ReadOnlySpan<byte> jsonBytes, TValue defaultValue)
		{
			return Parse(jsonBytes).As(defaultValue);
		}

		/// <summary>Désérialise une source de texte JSON vers un type défini</summary>
		/// <param name="jsonBytes">Buffer contenant du texte JSON encodé en UTF-8</param>
		/// <param name="defaultValue"></param>
		/// <param name="settings">Serialization settings (use default JSON settings if null)</param>
		/// <param name="resolver">Resolver optionnel</param>
		/// <returns>Objet correspondant</returns>
		/// <exception cref="FormatException">En cas d'erreur de parsing JSON</exception>
		[Pure]
		[return: NotNullIfNotNull(nameof(defaultValue))]
		public static TValue? Deserialize<TValue>(ReadOnlySpan<byte> jsonBytes, TValue defaultValue, CrystalJsonSettings? settings = null, ICrystalJsonTypeResolver? resolver = null)
		{
			return Parse(jsonBytes, settings).As(defaultValue, resolver);
		}

		/// <summary>Désérialise une source de texte JSON vers un type défini</summary>
		/// <param name="jsonBytes">Buffer contenant du texte JSON encodé en UTF-8</param>
		/// <param name="settings">Serialization settings (use default JSON settings if null)</param>
		/// <param name="resolver">Resolver optionnel</param>
		/// <returns>Objet correspondant</returns>
		/// <exception cref="FormatException">En cas d'erreur de parsing JSON</exception>
		/// <exception cref="InvalidOperationException">If the JSON document is <c>"null"</c></exception>
		[Pure]
		public static TValue Deserialize<TValue>(ReadOnlyMemory<byte> jsonBytes, CrystalJsonSettings? settings = null, ICrystalJsonTypeResolver? resolver = null) where TValue : notnull
		{
			return Parse(jsonBytes, settings).Required<TValue>(resolver);
		}

		/// <summary>Désérialise une source de texte JSON vers un type défini</summary>
		/// <param name="jsonBytes">Buffer contenant du texte JSON encodé en UTF-8</param>
		/// <param name="defaultValue"></param>
		/// <returns>Objet correspondant</returns>
		/// <exception cref="FormatException">En cas d'erreur de parsing JSON</exception>
		[Pure]
		[return: NotNullIfNotNull(nameof(defaultValue))]
		public static TValue? Deserialize<TValue>(ReadOnlyMemory<byte> jsonBytes, TValue defaultValue)
		{
			return Parse(jsonBytes).As(defaultValue);
		}

		/// <summary>Désérialise une source de texte JSON vers un type défini</summary>
		/// <param name="jsonBytes">Buffer contenant du texte JSON encodé en UTF-8</param>
		/// <param name="defaultValue"></param>
		/// <param name="settings">Serialization settings (use default JSON settings if null)</param>
		/// <param name="resolver">Resolver optionnel</param>
		/// <returns>Objet correspondant</returns>
		/// <exception cref="FormatException">En cas d'erreur de parsing JSON</exception>
		[Pure]
		[return: NotNullIfNotNull(nameof(defaultValue))]
		public static TValue? Deserialize<TValue>(ReadOnlyMemory<byte> jsonBytes, TValue defaultValue, CrystalJsonSettings? settings = null, ICrystalJsonTypeResolver? resolver = null)
		{
			return Parse(jsonBytes, settings).As(defaultValue, resolver);
		}

		/// <summary>Dé-sérialise une source de texte JSON vers un type défini</summary>
		/// <param name="source">Source contenant le texte JSON à dé-sérialiser</param>
		/// <param name="settings">Serialization settings (use default JSON settings if null)</param>
		/// <param name="resolver">Resolver optionnel</param>
		/// <returns>Objet correspondant</returns>
		/// <exception cref="FormatException">En cas d'erreur de parsing JSON</exception>
		/// <exception cref="InvalidOperationException">If the JSON document is <c>"null"</c></exception>
		[Pure]
		public static TValue? LoadFrom<TValue>(TextReader source, CrystalJsonSettings? settings = null, ICrystalJsonTypeResolver? resolver = null) where TValue : notnull
		{
			return ParseFrom(source, settings).As<TValue>(resolver);
		}

		/// <summary>Dé-sérialise une source de données JSON vers un type défini</summary>
		/// <param name="source">Source contenant le JSON à dé-sérialiser (encodé en UTF-8)</param>
		/// <param name="settings">Serialization settings (use default JSON settings if null)</param>
		/// <param name="resolver">Resolver optionnel</param>
		/// <returns>Objet correspondant</returns>
		/// <exception cref="FormatException">En cas d'erreur de parsing JSON</exception>
		/// <exception cref="InvalidOperationException">If the JSON document is <c>"null"</c></exception>
		[Pure]
		public static TValue? LoadFrom<TValue>(Stream source, CrystalJsonSettings? settings = null, ICrystalJsonTypeResolver? resolver = null) where TValue : notnull
		{
			Contract.NotNull(source);

			//REVIEW: on pourrait détecter un MemoryStream et directement lire le buffer s'il est accessible, mais il faut s'assurer
			// que dans tous les cas (succès ou erreur), on seek le MemoryStream exactement comme si on l'avait consommé directement !

			using (var sr = new StreamReader(source, Encoding.UTF8, true))
			{
				return ParseFromReader(new JsonTextReader(sr), settings).As<TValue>(resolver);
			}
		}

		/// <summary>Dé-sérialise le contenu d'un fichier JSON sur le disque vers un type défini</summary>
		/// <param name="path">Nom du fichier à lire</param>
		/// <param name="settings">Serialization settings (use default JSON settings if null)</param>
		/// <param name="resolver">Resolver optionnel</param>
		/// <param name="options">Options de lecture</param>
		/// <returns>Objet correspondant</returns>
		/// <exception cref="FormatException">En cas d'erreur de parsing JSON</exception>
		/// <exception cref="InvalidOperationException">If the JSON document is <c>"null"</c></exception>
		[Pure]
		public static TValue? LoadFrom<TValue>(string path, CrystalJsonSettings? settings = null, ICrystalJsonTypeResolver? resolver = null, LoadOptions options = LoadOptions.None) where TValue : notnull
		{
			return LoadAndParseInternal(path, settings ?? CrystalJsonSettings.Json, options).As<TValue>(resolver);
		}

		#endregion

		#endregion

		#region Helpers...

		public const long Ticks1970Jan1 = 621355968000000000; // = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).Ticks;

		/// <summary>Retourne le nombre de ticks JavaScript correspondant à une date</summary>
		internal static long DateToJavaScriptTicks(DateTime date)
		{
			long ticks = ((date != DateTime.MinValue && date != DateTime.MaxValue && date.Kind != DateTimeKind.Utc) ? date.ToUniversalTime() : date).Ticks;
			return (ticks - Ticks1970Jan1) / TimeSpan.TicksPerMillisecond;
		}

		internal static long DateToJavaScriptTicks(DateTimeOffset date)
		{
			return (date.UtcTicks - Ticks1970Jan1) / TimeSpan.TicksPerMillisecond;
		}

		/// <summary>Retourne la date correspondant à un nombre de ticks JavaScript</summary>
		internal static DateTime JavaScriptTicksToDate(long ticks)
		{
			return new DateTime((ticks * TimeSpan.TicksPerMillisecond) + Ticks1970Jan1, DateTimeKind.Utc);
		}

		/// <summary>Encode une chaîne en JSON</summary>
		/// <param name="text">Chaîne à encoder</param>
		/// <returns>'null', '""', '"foo"', '"\""', '"\u0000"', ...</returns>
		/// <remarks>Chaine correctement encodée. Note: retourne "null" si text==null</remarks>
		public static string StringEncode(string? text)
		{
			return JsonEncoding.Encode(text);
		}

		/// <summary>Encode une chaîne en JSON, et append le résultat à un StringBuilder</summary>
		/// <param name="sb">Buffer où écrire le résultat</param>
		/// <param name="text">Chaîne à encoder</param>
		/// <returns>Le StringBuilder passé en paramètre (pour chainage)</returns>
		/// <remarks>Note: Ajoute "null" si text==null && includeQuotes==true</remarks>
		public static StringBuilder StringAppend(StringBuilder sb, string? text)
		{
			return JsonEncoding.Append(sb, text);
		}

		/// <summary>Test if the resolver is the default resolver (<see langword="true"/>) or a customized resolver (<see langword="false"/>)</summary>
		/// <param name="resolver"></param>
		/// <returns></returns>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		internal static bool IsDefaultResolver([NotNullWhen(false)] ICrystalJsonTypeResolver? resolver)
		{
			return resolver == null || ReferenceEquals(resolver, DefaultResolver);
		}

		#endregion

		#region Error Handling...

		internal static class Errors
		{

			#region Serialization Errors...

			[Pure, MethodImpl(MethodImplOptions.NoInlining)]
			internal static JsonSerializationException Serialization_FailTooDeep(int depth, object? current)
			{
				return new JsonSerializationException($"Reached maximum depth of {depth} while serializing child object of type '{current?.GetType().GetFriendlyName() ?? "<null>"}'. Top object is too complex to be serialized this way!");
			}

			[Pure, MethodImpl(MethodImplOptions.NoInlining)]
			internal static InvalidOperationException Serialization_ObjectRecursionIsNotAllowed(IEnumerable<object?> visited, object? value, int depth)
			{
				return new JsonSerializationException($"Object of type '{value?.GetType().FullName}' at depth {depth} already serialized before! Recursive object graphs not supported. Visited path: {string.Join(" <- ", visited.Select(v => v?.GetType().FullName ?? "<null>"))}");
			}

			[Pure, MethodImpl(MethodImplOptions.NoInlining)]
			internal static JsonSerializationException Serialization_InternalDepthInconsistent()
			{
				return new JsonSerializationException("Internal depth is inconsistent.");
			}

			[Pure, MethodImpl(MethodImplOptions.NoInlining)]
			internal static JsonSerializationException Serialization_LeaveNotSameThanMark(int depth, object? current)
			{
				return new JsonSerializationException($"Desynchronization of the visited object stack: Leave() was called with a different value of type '{current?.GetType().GetFriendlyName() ?? "<null>"}' than MarkVisited() at depth {depth}.");
			}

			[Pure, MethodImpl(MethodImplOptions.NoInlining)]
			internal static JsonSerializationException Serialization_DoesNotKnowHowToSerializeType(Type type)
			{
				return new JsonSerializationException($"Doesn't know how to serialize values of type '{type.GetFriendlyName()}'.");
			}

			[Pure, MethodImpl(MethodImplOptions.NoInlining)]
			internal static JsonSerializationException Serialization_DoesNotKnowHowToSerializeNullableType(Type type)
			{
				return new JsonSerializationException($"Doesn't know how to serialize Nullable type '{type.GetFriendlyName()}'.");
			}

			[Pure, MethodImpl(MethodImplOptions.NoInlining)]
			internal static JsonSerializationException Serialization_CouldNotResolveTypeDefinition(Type type)
			{
				return new JsonSerializationException($"Could not get the members list for type '{type.GetFriendlyName()}'.");
			}

			[Pure, MethodImpl(MethodImplOptions.NoInlining)]
			internal static JsonSerializationException Serialization_StaticJsonSerializeMethodInvalidSignature(Type type, MethodInfo method)
			{
				return new JsonSerializationException($"Static serialization method '{type.GetFriendlyName()}.{method.Name}' must take two parameters.");
			}

			[Pure, MethodImpl(MethodImplOptions.NoInlining)]
			internal static JsonSerializationException Serialization_StaticJsonSerializeMethodInvalidFirstParam(Type type, MethodInfo method, Type prmType)
			{
				return new JsonSerializationException($"First parameter of static method '{type.GetFriendlyName()}.{method.Name}' must be assignable to type '{type.GetFriendlyName()}' (it was '{prmType.GetFriendlyName()}').");
			}

			[Pure, MethodImpl(MethodImplOptions.NoInlining)]
			internal static JsonSerializationException Serialization_StaticJsonSerializeMethodInvalidSecondParam(Type type, MethodInfo method, Type prmType)
			{
				return new JsonSerializationException($"Second parameter of static method '{type.GetFriendlyName()}.{method.Name}' must be a {nameof(CrystalJsonWriter)} object (it was '{prmType.GetFriendlyName()}').");
			}

			[Pure, MethodImpl(MethodImplOptions.NoInlining)]
			internal static JsonSerializationException Serialization_StaticJsonPackMethodInvalidSignature(Type type, MethodInfo method)
			{
				return new JsonSerializationException($"Static serialization method '{type.GetFriendlyName()}.{method.Name}' must take three parameters.");
			}

			[Pure, MethodImpl(MethodImplOptions.NoInlining)]
			internal static JsonSerializationException Serialization_InstanceJsonPackMethodInvalidSignature(Type type, MethodInfo method)
			{
				return new JsonSerializationException($"Static serialization method '{type.GetFriendlyName()}.{method.Name}' must take two parameters.");
			}

			[Pure, MethodImpl(MethodImplOptions.NoInlining)]
			internal static JsonSerializationException Serialization_CouldNotGetDefaultValueForMember(Type type, MemberInfo info, Exception? error)
			{
				var memberType = info is PropertyInfo pi ? pi.PropertyType : info is FieldInfo fi ? fi.FieldType : typeof(object);
				if (memberType.IsByRefLike) return new JsonSerializationException($"Cannot serialize {(info is PropertyInfo ? "property" : "field")} {type.GetFriendlyName()}.{info.Name} with type {memberType.GetFriendlyName()}: ref-like types are NOT supported.", error);
				return new JsonSerializationException($"Cannot generate default value for {(info is PropertyInfo ? "property" : "field")} {type.GetFriendlyName()}.{info.Name} with type {memberType.GetFriendlyName()}.", error);
			}


			#endregion

			#region Parsing Errors...

			[Pure, MethodImpl(MethodImplOptions.NoInlining)]
			internal static JsonBindingException Parsing_CannotCastToJsonObject(JsonValue? value)
			{
				return new JsonBindingException($"Cannot parse JSON {(value ?? JsonNull.Missing).Type} as an Object.", value);
			}

			[Pure, MethodImpl(MethodImplOptions.NoInlining)]
			internal static JsonBindingException Parsing_CannotCastToJsonArray(JsonValue? value)
			{
				return new JsonBindingException($"Cannot parse JSON {(value ?? JsonNull.Missing).Type} as an Array.", value);
			}

			[Pure, MethodImpl(MethodImplOptions.NoInlining)]
			internal static JsonBindingException Parsing_CannotCastToJsonNumber(JsonValue? value)
			{
				return new JsonBindingException($"Cannot parse JSON {(value ?? JsonNull.Missing).Type} as a Number.", value);
			}

			[Pure, MethodImpl(MethodImplOptions.NoInlining)]
			internal static JsonBindingException Parsing_CannotCastToJsonString(JsonValue? value)
			{
				return new JsonBindingException($"Cannot parse JSON {(value ?? JsonNull.Missing).Type} as a String.", value);
			}

			#endregion

		}

		#endregion
	}
}
