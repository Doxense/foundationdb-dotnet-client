#region Copyright (c) 2005-2023 Doxense SAS
//
// All rights are reserved. Reproduction or transmission in whole or in part, in
// any form or by any means, electronic, mechanical or otherwise, is prohibited
// without the prior written consent of the copyright owner.
//
#endregion

//#define DEBUG_JSON_PARSER

namespace Doxense.Serialization.Json
{
	using System;
	using System.Buffers;
	using System.Collections.Generic;
	using System.Diagnostics;
	using System.Diagnostics.CodeAnalysis;
	using System.IO;
	using System.Linq;
	using System.Reflection;
	using System.Runtime.CompilerServices;
	using System.Text;
	using Doxense.Diagnostics.Contracts;
	using Doxense.IO;
	using Doxense.Memory;
	using Doxense.Memory.Text;
	using Doxense.Runtime;
	using JetBrains.Annotations;

	/// <summary>Classe helper pour la s�rialisation d'objets en JSON</summary>
	[PublicAPI]
	[DebuggerNonUserCode]
	public static class CrystalJson
	{
		public static readonly CrystalJsonTypeResolver DefaultResolver = new CrystalJsonTypeResolver();
		public static readonly UTF8Encoding Utf8NoBom = CrystalJsonFormatter.Utf8NoBom; //note: le but ici --est de forcer le JIT � initialiser CrystalJsonParser imm�diatement d�s qu'on touche � CrystalJson!

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
			/// <summary>Si le fichier existe d�j�, sauve les donn�es dans un fichier temporaire, et swap l'ancien et le nouveau � la fin</summary>
			AtomicSave = 1,
			/// <summary>Si le fichier existe d�j�, il sera backup� (avec l'extension ".bak")</summary>
			KeepBackup = 2,
			/// <summary>Ajoute les donn�es � la fin d'un fichier existant (en le cr�ant s'il n'existe pas)</summary>
			Append = 4,
		}

		[Flags]
		public enum LoadOptions
		{
			None = 0,
			/// <summary>Si le fichier n'existe pas, retourne la valeur par d�faut du type (null, 0, false, ...)</summary>
			ReturnNullIfMissing = 1,
			/// <summary>Le stream source est de type streaming, il faut ne faut pas attendre la fin du fichier</summary>
			Streaming = 2
		}

		#region Serialization...

		/// <summary>S�rialise une valeur (de n'importe quel type)</summary>
		/// <param name="value">Valeur � s�rialiser (de n'importe quel type)</param>
		/// <param name="settings">Param�tres de s�rialisation (JSON par d�faut si null)</param>
		/// <param name="customResolver">Custom Resolver utilis� pour la s�rialisation (par d�faut si null)</param>
		/// <returns>"123", "\"ABC\"", "{ obj }", "[ ... ]", ...</returns>
		/// <exception cref="Doxense.Serialization.Json.JsonSerializationException">En cas d'erreur de s�rialisation</exception>
		[Pure]
		public static string Serialize(object? value, CrystalJsonSettings? settings = null, ICrystalJsonTypeResolver? customResolver = null)
		{
			return SerializeInternal(value, typeof(object), null, settings, customResolver).ToString();
		}

		/// <summary>S�rialise une valeur (de n'importe quel type)</summary>
		/// <param name="value">Valeur � s�rialiser (de n'importe quel type)</param>
		/// <param name="declaredType">Type de la valeur telle que d�clar�e dans le conteneur parent</param>
		/// <param name="settings">Param�tres de s�rialisation (JSON par d�faut si null)</param>
		/// <param name="customResolver">Custom Resolver utilis� pour la s�rialisation (par d�faut si null)</param>
		/// <returns>"123", "\"ABC\"", "{ obj }", "[ ... ]", ...</returns>
		/// <exception cref="Doxense.Serialization.Json.JsonSerializationException">En cas d'erreur de s�rialisation</exception>
		[Pure]
		public static string Serialize(object? value, Type declaredType, CrystalJsonSettings? settings = null, ICrystalJsonTypeResolver? customResolver = null)
		{
			return SerializeInternal(value, declaredType, null, settings, customResolver).ToString();
		}

		/// <summary>S�rialise une valeur (de n'importe quel type)</summary>
		/// <param name="value">Valeur � s�rialiser (de n'importe quel type)</param>
		/// <param name="settings">Param�tres de s�rialisation (JSON par d�faut si null)</param>
		/// <param name="customResolver">Custom Resolver utilis� pour la s�rialisation (par d�faut si null)</param>
		/// <returns>"123", "\"ABC\"", "{ obj }", "[ ... ]", ...</returns>
		/// <exception cref="Doxense.Serialization.Json.JsonSerializationException">En cas d'erreur de s�rialisation</exception>
		[Pure]
		public static string Serialize<T>(T? value, CrystalJsonSettings? settings = null, ICrystalJsonTypeResolver? customResolver = null)
		{
			return SerializeInternal(value, typeof(T), null, settings, customResolver).ToString();
		}

		/// <summary>S�rialise une valeur (de n'importe quel type)</summary>
		/// <param name="value">Valeur � s�rialiser (de n'importe quel type)</param>
		/// <param name="buffer">Buffer de destination (cr�� automatiquement si null)</param>
		/// <param name="settings">Param�tres de s�rialisation (JSON par d�faut si null)</param>
		/// <param name="customResolver">Custom Resolver utilis� pour la s�rialisation (par d�faut si null)</param>
		/// <returns>Buffer contenant l'objet s�rialis�</returns>
		/// <exception cref="Doxense.Serialization.Json.JsonSerializationException">En cas d'erreur de s�rialisation</exception>
		[Pure]
		public static StringBuilder Serialize(object? value, StringBuilder? buffer, CrystalJsonSettings? settings = null, ICrystalJsonTypeResolver? customResolver = null)
		{
			return SerializeInternal(value, typeof(object), buffer, settings, customResolver);
		}

		/// <summary>Cr�e un buffer en m�moire dont la taille d�pend des settings</summary>
		/// <param name="settings">Settings de s�rialisation (peut �tre null, dans ce cas on consid�re un param�trage par d�faut)</param>
		/// <returns>StringBuilder vide, dont la capacit� d�pend des settings</returns>
		[Pure]
		private static StringBuilder CreateBufferFromSettings(CrystalJsonSettings? settings)
		{
			//note: ca ne sert pas � grand chose d'utiliser le StringBuilderCache, car les tailles de buffers (512 / 4096) sont d�j� au dessus de la limite interne du cache!
			int capacity = settings?.OptimizeForLargeData == true ? 4096 : 512;
			return new StringBuilder(capacity);
		}

		/// <summary>S�rialise un objet ou une valeur</summary>
		/// <param name="value">Classe, structure, Enumerable, Nullable&lt;T&gt;, ...</param>
		/// <param name="declaredType"></param>
		/// <param name="buffer">Buffer de destination (cr�� automatiquement si null)</param>
		/// <param name="settings">Param�tres de s�rialisation (JSON par d�faut si null)</param>
		/// <param name="customResolver">Custom Resolver utilis� pour la s�rialisation (par d�faut si null)</param>
		/// <returns>Buffer contenant l'objet s�rialis�</returns>
		[Pure]
		private static StringBuilder SerializeInternal(object? value, Type declaredType, StringBuilder? buffer, CrystalJsonSettings? settings, ICrystalJsonTypeResolver? customResolver)
		{
			if (value == null)
			{ // cas sp�cial pour null
				return buffer?.Append(JsonTokens.Null) ?? new StringBuilder(JsonTokens.Null);
			}

			// initialise le buffer si besoin
			if (buffer == null) buffer = CreateBufferFromSettings(settings);

			//REVIEW: ObjectPool pour le FastStringWriter et le CrystalJsonWriter?

			// seule les struct/class sont autoris�es
			using (var fsw = new FastStringWriter(buffer))
			{
				var writer = new CrystalJsonWriter(fsw, settings, customResolver);
				CrystalJsonVisitor.VisitValue(value, declaredType, writer);
				return buffer;
			}
		}

		/// <summary>S�rialise une valeur (de n'importe quel type)</summary>
		/// <param name="output">Output o� �crire le JSON g�n�r�</param>
		/// <param name="value">Valeur � s�rialiser (de n'importe quel type)</param>
		/// <param name="settings">Param�tres de s�rialisation (JSON par d�faut si null)</param>
		/// <param name="customResolver">Custom Resolver utilis� pour la s�rialisation (par d�faut si null)</param>
		/// <returns>"123", "\"ABC\"", "{ obj }", "[ ... ]", ...</returns>
		/// <exception cref="Doxense.Serialization.Json.JsonSerializationException">En cas d'erreur de s�rialisation</exception>
		public static TextWriter SerializeTo(TextWriter output, object value, CrystalJsonSettings? settings = null, ICrystalJsonTypeResolver? customResolver = null)
		{
			return SerializeToInternal(output, value, typeof(object), settings, customResolver);
		}

		/// <summary>S�rialise une valeur (de n'importe quel type)</summary>
		/// <param name="output">Output o� �crire le JSON g�n�r�</param>
		/// <param name="value">Valeur � s�rialiser (de n'importe quel type)</param>
		/// <param name="settings">Param�tres de s�rialisation (JSON par d�faut si null)</param>
		/// <param name="customResolver">Custom Resolver utilis� pour la s�rialisation (par d�faut si null)</param>
		/// <returns>"123", "\"ABC\"", "{ obj }", "[ ... ]", ...</returns>
		/// <exception cref="Doxense.Serialization.Json.JsonSerializationException">En cas d'erreur de s�rialisation</exception>
		public static TextWriter SerializeTo<T>(TextWriter output, T value, CrystalJsonSettings? settings = null, ICrystalJsonTypeResolver? customResolver = null)
		{
			return SerializeToInternal(output, value, typeof(T), settings, customResolver);
		}

		/// <summary>S�rialise une valeur (de n'importe quel type)</summary>
		/// <param name="output">Output o� �crire le JSON g�n�r�</param>
		/// <param name="value">Valeur � s�rialiser (de n'importe quel type)</param>
		/// <param name="settings">Param�tres de s�rialisation (JSON par d�faut si null)</param>
		/// <param name="customResolver">Custom Resolver utilis� pour la s�rialisation (par d�faut si null)</param>
		/// <returns>"123", "\"ABC\"", "{ obj }", "[ ... ]", ...</returns>
		/// <exception cref="Doxense.Serialization.Json.JsonSerializationException">En cas d'erreur de s�rialisation</exception>
		public static void SerializeTo<T>(Stream output, T? value, CrystalJsonSettings? settings = null, ICrystalJsonTypeResolver? customResolver = null)
		{
			Contract.NotNull(output);
			using (var sw = new StreamWriter(output, Utf8NoBom))
			{
				SerializeToInternal(sw, value, typeof(T), settings, customResolver);
			}
		}

		/// <summary>S�rialise une valeur (de n'importe quel type) vers un fichier</summary>
		/// <param name="path">Chemin du fichier dans lequel �crire les donn�es s�rialis�es</param>
		/// <param name="value">Valeur � s�rialiser (de n'importe quel type)</param>
		/// <param name="settings">Param�tres de s�rialisation (JSON par d�faut si null)</param>
		/// <param name="customResolver">Custom Resolver utilis� pour la s�rialisation (par d�faut si null)</param>
		/// <param name="options">Options de sauvegarde</param>
		/// <returns>"123", "\"ABC\"", "{ obj }", "[ ... ]", ...</returns>
		/// <exception cref="Doxense.Serialization.Json.JsonSerializationException">En cas d'erreur de s�rialisation</exception>
		public static void SaveTo(string path, object? value, CrystalJsonSettings? settings = null, ICrystalJsonTypeResolver? customResolver = null, SaveOptions options = SaveOptions.None)
		{
			SerializeAndSaveInternal(path, value, typeof(object), settings, customResolver, options);
		}

		/// <summary>S�rialise une valeur (d'un type sp�cifique) vers un fichier</summary>
		/// <typeparam name="T">Type des donn�es s�rialis�es</typeparam>
		/// <param name="path">Chemin du fichier dans lequel �crire les donn�es s�rialis�es</param>
		/// <param name="value">Valeur � s�rialiser (de n'importe quel type)</param>
		/// <param name="settings">Param�tres de s�rialisation (JSON par d�faut si null)</param>
		/// <param name="customResolver">Custom Resolver utilis� pour la s�rialisation (par d�faut si null)</param>
		/// <param name="options">Options de sauvegarde</param>
		/// <returns>"123", "\"ABC\"", "{ obj }", "[ ... ]", ...</returns>
		/// <exception cref="Doxense.Serialization.Json.JsonSerializationException">En cas d'erreur de s�rialisation</exception>
		public static void SaveTo<T>(string path, T? value, CrystalJsonSettings? settings = null, ICrystalJsonTypeResolver? customResolver = null, SaveOptions options = SaveOptions.None)
		{
			SerializeAndSaveInternal(path, value, typeof(T), settings, customResolver, options);
		}

		/// <summary>S�ralize un object en JSON indent�, pour debuggage rapide</summary>
		[Pure]
		public static string Dump(object? value)
		{
			return Serialize(value, CrystalJsonSettings.JsonIndented);
		}

		/// <summary>S�ralize un object en JSON indent�, pour debuggage rapide</summary>
		[Pure]
		public static string Dump<T>(T? value)
		{
			return Serialize<T>(value, CrystalJsonSettings.JsonIndented);
		}

		/// <summary>S�rialise une valeur sous forme binaire, en m�moire</summary>
		/// <param name="value">Valeur � s�rialiser (de n'importe quel type)</param>
		/// <param name="settings">Param�tres de s�rialisation (JSON par d�faut si null)</param>
		/// <param name="customResolver">Custom Resolver utilis� pour la s�rialisation (par d�faut si null)</param>
		/// <returns>Tableau de bytes contenant les donn�es s�rialis�es</returns>
		/// <exception cref="Doxense.Serialization.Json.JsonSerializationException">En cas d'erreur de s�rialisation</exception>
		[Pure]
		public static byte[] ToBytes(object? value, CrystalJsonSettings? settings = null, ICrystalJsonTypeResolver? customResolver = null)
		{
			return ToBytesInternal(value, typeof(object), settings, customResolver);
		}

		/// <summary>S�rialise une valeur sous forme binaire, en m�moire</summary>
		/// <param name="value">Valeur � s�rialiser (de n'importe quel type)</param>
		/// <param name="settings">Param�tres de s�rialisation (JSON par d�faut si null)</param>
		/// <param name="customResolver">Custom Resolver utilis� pour la s�rialisation (par d�faut si null)</param>
		/// <returns>Tableau de bytes contenant les donn�es s�rialis�es</returns>
		/// <exception cref="Doxense.Serialization.Json.JsonSerializationException">En cas d'erreur de s�rialisation</exception>
		[Pure]
		public static byte[] ToBytes<T>(T? value, CrystalJsonSettings? settings = null, ICrystalJsonTypeResolver? customResolver = null)
		{
			return ToBytesInternal(value, typeof(T), settings, customResolver);
		}

		/// <summary>S�rialise une valeur sous forme binaire, en m�moire</summary>
		/// <param name="value">Valeur � s�rialiser</param>
		/// <param name="declaredType">Type d�clar� de la valeur (ou typeof(object))</param>
		/// <param name="settings">Param�tres de s�rialisation (JSON par d�faut si null)</param>
		/// <param name="customResolver">Custom Resolver utilis� pour la s�rialisation (par d�faut si null)</param>
		/// <returns>Tableau de bytes contenant les donn�es s�rialis�es</returns>
		[Pure]
		private static byte[] ToBytesInternal(object? value, Type declaredType, CrystalJsonSettings? settings, ICrystalJsonTypeResolver? customResolver)
		{
			var bufferSize = 256;
			if (settings != null && settings.OptimizeForLargeData)
			{
				bufferSize = 0x14000; // 80 K
			}

			//REVIEW: on a besoin de coder un TextWriter+MemoryStream qui �crit directement en UTF8 en m�moire!
			// => le profiler montre qu'on gaspille beaucoup de m�moire dans le buffer du StreamWriter, qui ne fait que copier les bytes directement dans le MemoryStream,
			// pour au final recopier tout ca dans un byte[] :(

			// 64K de buffer pour des "grosses" donn�es, 256 bytes pour des petites
			using (var ms = new MemoryStream(bufferSize))
			{
				// note: vu qu'on s�rialise en m�moire, la taille du buffer du StreamWriter importe peu donc autant la r�duire le plus possible
				using (var sw = new StreamWriter(ms, Utf8NoBom, bufferSize, leaveOpen: true))
				{
					SerializeToInternal(sw, value, declaredType, settings, customResolver);
				}

				if (ms.Position == 0) return Array.Empty<byte>();
				if (ms.Position == ms.Capacity) return ms.GetBuffer();
				return ms.ToArray();
			}
		}

		/// <summary>S�rialise une valeur sous forme binaire, en m�moire</summary>
		/// <param name="value">Valeur � s�rialiser (de n'importe quel type)</param>
		/// <param name="settings">Param�tres de s�rialisation (JSON par d�faut si null)</param>
		/// <param name="customResolver">Custom Resolver utilis� pour la s�rialisation (par d�faut si null)</param>
		/// <returns>Segment de buffer contenant les donn�es s�rialis�es</returns>
		/// <exception cref="Doxense.Serialization.Json.JsonSerializationException">En cas d'erreur de s�rialisation</exception>
		[Pure]
		public static Slice ToBuffer(object? value, CrystalJsonSettings? settings = null, ICrystalJsonTypeResolver? customResolver = null)
		{
			byte[]? _ = null;
			return ToBufferInternal(value, typeof(object), settings, customResolver, ref _);
		}

		/// <summary>S�rialise une valeur sous forme binaire, en m�moire</summary>
		/// <param name="value">Valeur � s�rialiser (de n'importe quel type)</param>
		/// <param name="settings">Param�tres de s�rialisation (JSON par d�faut si null)</param>
		/// <param name="customResolver">Custom Resolver utilis� pour la s�rialisation (par d�faut si null)</param>
		/// <returns>Segment de buffer contenant les donn�es s�rialis�es</returns>
		/// <exception cref="Doxense.Serialization.Json.JsonSerializationException">En cas d'erreur de s�rialisation</exception>
		[Pure]
		public static Slice ToBuffer<T>(T? value, CrystalJsonSettings? settings = null, ICrystalJsonTypeResolver? customResolver = null)
		{
			byte[]? _ = null;
			return ToBufferInternal(value, typeof(T), settings, customResolver, ref _);
		}

		/// <summary>S�rialise une valeur sous forme binaire, en m�moire</summary>
		/// <param name="value">Valeur � s�rialiser (de n'importe quel type)</param>
		/// <param name="settings">Param�tres de s�rialisation (JSON par d�faut si null)</param>
		/// <param name="customResolver">Custom Resolver utilis� pour la s�rialisation (par d�faut si null)</param>
		/// <param name="buffer">Buffer pr�-allou� pour la s�rialisation. Si null, ou trop petit, il sera remplac� par un nouveau buffer</param>
		/// <returns>Segment de buffer contenant les donn�es s�rialis�es</returns>
		/// <exception cref="Doxense.Serialization.Json.JsonSerializationException">En cas d'erreur de s�rialisation</exception>
		/// <remarks>Attention: le segment retourn� utilise le buffer <paramref name="buffer"/>. L'appelant doit enti�rement consommer le r�sultat, avant de r�utiliser ce buffer, sous risque de corrompre les donn�es g�n�r�es!</remarks>
		[Pure]
		public static Slice ToBuffer(object? value, CrystalJsonSettings? settings, ICrystalJsonTypeResolver? customResolver, ref byte[]? buffer)
		{
			return ToBufferInternal(value, typeof(object), settings, customResolver, ref buffer);
		}

		/// <summary>S�rialise une valeur sous forme binaire, en m�moire</summary>
		/// <param name="value">Valeur � s�rialiser (de type compatible avec <paramref name="type"/>)</param>
		/// <param name="type">Type attendu</param>
		/// <param name="settings">Param�tres de s�rialisation (JSON par d�faut si null)</param>
		/// <param name="customResolver">Custom Resolver utilis� pour la s�rialisation (par d�faut si null)</param>
		/// <param name="buffer">Buffer pr�-allou� pour la s�rialisation. Si null, ou trop petit, il sera remplac� par un nouveau buffer</param>
		/// <returns>Segment de buffer contenant les donn�es s�rialis�es</returns>
		/// <exception cref="Doxense.Serialization.Json.JsonSerializationException">En cas d'erreur de s�rialisation</exception>
		/// <remarks>Attention: le segment retourn� utilise le buffer <paramref name="buffer"/>. L'appelant doit enti�rement consommer le r�sultat, avant de r�utiliser ce buffer, sous risque de corrompre les donn�es g�n�r�es!</remarks>
		[Pure]
		public static Slice ToBuffer(object? value, Type? type, CrystalJsonSettings? settings, ICrystalJsonTypeResolver? customResolver, ref byte[]? buffer)
		{
			return ToBufferInternal(value, type ?? value?.GetType() ?? typeof(object), settings, customResolver, ref buffer);
		}

		/// <summary>S�rialise une valeur sous forme binaire, en m�moire</summary>
		/// <param name="value">Valeur � s�rialiser (de n'importe quel type)</param>
		/// <param name="settings">Param�tres de s�rialisation (JSON par d�faut si null)</param>
		/// <param name="customResolver">Custom Resolver utilis� pour la s�rialisation (par d�faut si null)</param>
		/// <param name="buffer">Buffer pr�-allou� pour la s�rialisation. Si null, ou trop petit, il sera remplac� par un nouveau buffer</param>
		/// <returns>Segment de buffer contenant les donn�es s�rialis�es</returns>
		/// <exception cref="Doxense.Serialization.Json.JsonSerializationException">En cas d'erreur de s�rialisation</exception>
		/// <remarks>Attention: le segment retourn� utilise le buffer <paramref name="buffer"/>. L'appelant doit enti�rement consommer le r�sultat, avant de r�utiliser ce buffer, sous risque de corrompre les donn�es g�n�r�es!</remarks>
		[Pure]
		public static Slice ToBuffer<T>(T? value, CrystalJsonSettings? settings, ICrystalJsonTypeResolver? customResolver, ref byte[]? buffer)
		{
			return ToBufferInternal(value, typeof(T), settings, customResolver, ref buffer);
		}

		/// <summary>S�rialise une valeur sous forme binaire, en m�moire</summary>
		/// <param name="value">Valeur � s�rialiser</param>
		/// <param name="declaredType">Type d�clar� de la valeur (ou typeof(object))</param>
		/// <param name="settings">Param�tres de s�rialisation (JSON par d�faut si null)</param>
		/// <param name="customResolver">Custom Resolver utilis� pour la s�rialisation (par d�faut si null)</param>
		/// <param name="buffer">Buffer utilis� en interne pour la s�rialisation, qui sera redimensionn� au besoin.</param>
		/// <returns>Segment de buffer contenant les donn�es s�rialis�es</returns>
		/// <remarks>Attention: le r�sultat retourn� utilise <paramref name="buffer"/>. L'appelant doit enti�rement consommer le r�sultat, avant de r�utiliser cette instance, sous risque de corrompre les donn�es g�n�r�es!</remarks>
		[Pure]
		private static Slice ToBufferInternal(object? value, Type declaredType, CrystalJsonSettings? settings, ICrystalJsonTypeResolver? customResolver, ref byte[]? buffer)
		{
			//REVIEW: on a besoin de coder un TextWriter+MemoryStream qui �crit directement en UTF8 en m�moire!
			// => le profiler montre qu'on gaspille beaucoup de m�moire dans le buffer du StreamWriter, qui ne fait que copier les bytes directement dans le MemoryStream,
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

			// note: vu qu'on s�rialise en m�moire, la taille du buffer du StreamWriter importe peu donc autant la r�duire le plus possible pour �viter d'allouer 1K par d�faut !
			using (var sw = new Utf8StringWriter(new SliceWriter(buffer)))
			{
				SerializeToInternal(sw, value, declaredType, settings, customResolver);
				return sw.GetBuffer();
			}
		}

		/// <summary>S�rialise une valeur sous forme binaire, en m�moire</summary>
		/// <param name="value">Valeur � s�rialiser (de n'importe quel type)</param>
		/// <param name="settings">Param�tres de s�rialisation (JSON par d�faut si null)</param>
		/// <param name="customResolver">Custom Resolver utilis� pour la s�rialisation (par d�faut si null)</param>
		/// <param name="pool">Pool utilis� en interne pour l'obtention de buffers.</param>
		/// <returns>Segment de buffer contenant les donn�es s�rialis�es</returns>
		/// <exception cref="Doxense.Serialization.Json.JsonSerializationException">En cas d'erreur de s�rialisation</exception>
		/// <remarks>Attention: le r�sultat retourn� utilise <paramref name="pool"/>. L'appelant doit enti�rement consommer le r�sultat, avant de le retourner dans le pool!</remarks>
		[Pure]
		public static Slice ToBuffer(object? value, CrystalJsonSettings? settings, ICrystalJsonTypeResolver? customResolver, ArrayPool<byte>? pool)
		{
			return ToBufferInternal(value, typeof(object), settings, customResolver, pool);
		}

		/// <summary>S�rialise une valeur sous forme binaire, en m�moire</summary>
		/// <param name="value">Valeur � s�rialiser (de type compatible avec <paramref name="type"/>)</param>
		/// <param name="type">Type attendu</param>
		/// <param name="settings">Param�tres de s�rialisation (JSON par d�faut si null)</param>
		/// <param name="customResolver">Custom Resolver utilis� pour la s�rialisation (par d�faut si null)</param>
		/// <returns>Segment de buffer contenant les donn�es s�rialis�es</returns>
		/// <exception cref="Doxense.Serialization.Json.JsonSerializationException">En cas d'erreur de s�rialisation</exception>
		[Pure]
		public static Slice ToBuffer(object? value, Type? type, CrystalJsonSettings? settings, ICrystalJsonTypeResolver? customResolver)
		{
			byte[]? _ = null;
			return ToBufferInternal(value, type ?? value?.GetType() ?? typeof(object), settings, customResolver, ref _);
		}

		/// <summary>S�rialise une valeur sous forme binaire, en m�moire</summary>
		/// <param name="value">Valeur � s�rialiser (de type compatible avec <paramref name="type"/>)</param>
		/// <param name="type">Type attendu</param>
		/// <param name="settings">Param�tres de s�rialisation (JSON par d�faut si null)</param>
		/// <param name="customResolver">Custom Resolver utilis� pour la s�rialisation (par d�faut si null)</param>
		/// <param name="pool">Pool utilis� en interne pour l'obtention de buffers.</param>
		/// <returns>Segment de buffer contenant les donn�es s�rialis�es</returns>
		/// <exception cref="Doxense.Serialization.Json.JsonSerializationException">En cas d'erreur de s�rialisation</exception>
		/// <remarks>Attention: le r�sultat retourn� utilise <paramref name="pool"/>. L'appelant doit enti�rement consommer le r�sultat, avant de le retourner dans le pool!</remarks>
		[Pure]
		public static Slice ToBuffer(object? value, Type? type, CrystalJsonSettings? settings, ICrystalJsonTypeResolver? customResolver, ArrayPool<byte>? pool)
		{
			return ToBufferInternal(value, type ?? value?.GetType() ?? typeof(object), settings, customResolver, pool);
		}

		/// <summary>S�rialise une valeur sous forme binaire, en m�moire</summary>
		/// <param name="value">Valeur � s�rialiser (de n'importe quel type)</param>
		/// <param name="settings">Param�tres de s�rialisation (JSON par d�faut si null)</param>
		/// <param name="customResolver">Custom Resolver utilis� pour la s�rialisation (par d�faut si null)</param>
		/// <param name="pool">Pool utilis� en interne pour l'obtention de buffers.</param>
		/// <returns>Segment de buffer contenant les donn�es s�rialis�es</returns>
		/// <exception cref="Doxense.Serialization.Json.JsonSerializationException">En cas d'erreur de s�rialisation</exception>
		/// <remarks>Attention: le r�sultat retourn� utilise <paramref name="pool"/>. L'appelant doit enti�rement consommer le r�sultat, avant de le retourner dans le pool!</remarks>
		[Pure]
		public static Slice ToBuffer<T>(T? value, CrystalJsonSettings? settings, ICrystalJsonTypeResolver? customResolver, ArrayPool<byte>? pool)
		{
			return ToBufferInternal(value, typeof(T), settings, customResolver, pool);
		}

		/// <summary>S�rialise une valeur sous forme binaire, en m�moire</summary>
		/// <param name="value">Valeur � s�rialiser</param>
		/// <param name="declaredType">Type d�clarer de la valeur (ou typeof(object))</param>
		/// <param name="settings">Param�tres de s�rialisation (JSON par d�faut si null)</param>
		/// <param name="customResolver">Custom Resolver utilis� pour la s�rialisation (par d�faut si null)</param>
		/// <param name="pool">Pool utilis� en interne pour l'obtention de buffers.</param>
		/// <returns>Segment de buffer contenant les donn�es s�rialis�es</returns>
		/// <remarks>Attention: le r�sultat retourn� utilise <paramref name="pool"/>. L'appelant doit enti�rement consommer le r�sultat, avant de le retourner dans le pool!</remarks>
		[Pure]
		private static Slice ToBufferInternal(object? value, Type declaredType, CrystalJsonSettings? settings, ICrystalJsonTypeResolver? customResolver, ArrayPool<byte>? pool)
		{
			//REVIEW: on a besoin de coder un TextWriter+MemoryStream qui �crit directement en UTF8 en m�moire!
			// => le profiler montre qu'on gaspille beaucoup de m�moire dans le buffer du StreamWriter, qui ne fait que copier les bytes directement dans le MemoryStream,
			// pour au final recopier tout ca dans un byte[] :(

			int bufferSize = 256;
			if (settings != null && settings.OptimizeForLargeData)
			{
				bufferSize = 0x14000; // 80 K
			}
			//note: le StreamWriter va allouer un char[buffSize] et un byte[bufferSize*3 + 3] !

			pool ??= ArrayPool<byte>.Shared;

			// note: vu qu'on s�rialise en m�moire, la taille du buffer du StreamWriter importe peu donc autant la r�duire le plus possible pour �viter d'allouer 1K par d�faut !
			using (var sw = new Utf8StringWriter(new SliceWriter(bufferSize, pool)))
			{
				SerializeToInternal(sw, value, declaredType, settings, customResolver);
				return sw.GetBuffer();
			}
		}
		/// <summary>S�rialise un objet ou une valeur</summary>
		/// <param name="output">Stream de destination</param>
		/// <param name="value">Classe, structure, Enumerable, Nullable&lt;T&gt;, ...</param>
		/// <param name="declaredType">Type d�clar� de l'objet (ou typeof(object) si seulement connu au runtime)</param>
		/// <param name="settings">Param�tres de s�rialisation (JSON par d�faut si null)</param>
		/// <param name="customResolver">Custom Resolver utilis� pour la s�rialisation (par d�faut si null)</param>
		/// <returns>Buffer contenant l'objet s�rialis�</returns>
		private static TextWriter SerializeToInternal(TextWriter output, object? value, Type declaredType, CrystalJsonSettings? settings, ICrystalJsonTypeResolver? customResolver)
		{
			Contract.NotNull(output);

			if (value == null)
			{ // cas sp�cial pour null
				output.Write(JsonTokens.Null);
			}
			else
			{
				var writer = new CrystalJsonWriter(output, settings, customResolver);
				if (value is JsonValue jval)
				{ // shortcurt pour la s�rialisation de DOM json
					jval.JsonSerialize(writer);
				}
				else
				{
					CrystalJsonVisitor.VisitValue(value, declaredType, writer);
				}
			}
			return output;
		}

		/// <summary>Cr�e un writer vers un fichier sur le disque</summary>
		/// <param name="path">Chemin du fichier � �crire</param>
		/// <param name="settings"></param>
		/// <param name="streamFilter">Filtre utilis� pour d�corer le stream (crypto, compression, ...)</param>
		/// <returns>Writer pr�t � �crire dans le fichier</returns>
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

		private static void SerializeAndSaveInternal(string path, object? value, Type declaredType, CrystalJsonSettings? settings, ICrystalJsonTypeResolver? customResolver, SaveOptions options)
		{
			Contract.NotNullOrEmpty(path);
			path = Path.GetFullPath(path);

			string savePath = path;
			string? bakPath = null;

			if ((options & SaveOptions.Append) == SaveOptions.Append)
			{ //TODO: G�rer le mode append !
				throw new NotSupportedException("Append save is not supported");
			}

			#region Settings...

			bool doAtomicUpdate = false;
			if (File.Exists(path))
			{ // Le fichier existe d�j�, il va falloir faire un replace
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
			{ // Le fichier n'existait pas, on va v�rifier si le r�pertoire parent existe, et si ce n'est pas le cas, on le cr�e
				string parent = Path.GetDirectoryName(path)!;
				Contract.Debug.Assert(parent != null);
				if (!Directory.Exists(parent))
				{
					Directory.CreateDirectory(parent);
				}
			}

			#endregion

			// note: si on est arriv� jusqu'ici, c'est que le chemin pointe bien vers un r�pertoire valide,
			// donc le reste du code n'a pas a se pr�occuper des erreurs venant de probl�mes de chemins

			// Diff�rents scenarios:
			if (doAtomicUpdate)
			{ // Remplace de mani�re atomique le fichier:
				// * On sauve les donn�es dans un fichier temporaire
				// * On swap l'ancien et le nouveau apr�s �criture des donn�es (et on backup l'ancien si besoin, sinon il est supprim�)
				// * En cas d'erreur, on supprime le fichier temporaire et il n'y a rien d'autre a faire

				try
				{
					using (var output = OpenJsonStreamWriter(savePath, settings))
					{
						SerializeToInternal(output, value, declaredType, settings, customResolver);
						// note: certains impl�mentation "buggu�e" de streams (GzipStream, etc..) requiert un flush pour finir d'�crire les data...

						output.Flush();
					}
				}
				catch (Exception)
				{
					if (File.Exists(savePath)) File.Delete(savePath);
					throw;
				}

				// swap les fichiers (update atomique, mais sans retomb�es radio-actives)
				File.Replace(savePath, path, bakPath);
				// note: si ca foire... tant pis :)

			}
			else if (bakPath != null)
			{ // Ecrase le fichier, mais en gardant un backup du pr�c�dent:
				// * On renomme l'ancien fichier en backup (en �crasant le backup pr�c�dent s'il y en a un)
				// * On sauve les donn�es dans le fichier de destination
				// * En cas d'erreur, on supprime le fichier g�n�r�, et on restaure le backup

				bool swapped = false;
				try
				{
					File.Replace(savePath, bakPath, null);
					swapped = true;

					using (var output = OpenJsonStreamWriter(savePath, settings))
					{
						SerializeToInternal(output, value, declaredType, settings, customResolver);
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
				// * On sauve les donn�es dans le fichier de destination
				// * En cas d'erreur, on le supprime

				try
				{
					using (var output = OpenJsonStreamWriter(savePath, settings))
					{
						SerializeToInternal(output, value, declaredType, settings, customResolver);
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

		//note: Parse(....) est g�n�rique, ParseArray(...)/ParseObject(...) sont � utiliser si on veut absolument une JsonArray ou JsonObject

		/// <summary>Parse une cha�ne de texte JSON</summary>
		/// <param name="jsonText">Cha�ne de texte JSON � parser</param>
		/// <param name="settings">Param�tres de parsing (optionnels)</param>
		/// <returns>Valeur JSON correspondante</returns>
		/// <exception cref="System.FormatException">En cas d'erreur de syntaxe JSON</exception>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static JsonValue Parse(string? jsonText, CrystalJsonSettings? settings = null)
		{
#if DEBUG_JSON_PARSER
			Debug.WriteLine("CrystalJson.Parse('" + jsonText + "', ...)");
#endif
			return ParseFromReader(new JsonStringReader(jsonText), settings);
		}

		/// <summary>Parse un buffer contenant du JSON</summary>
		/// <param name="jsonBytes">Bloc de donn�es contenant du texte JSON encod� en UTF-8</param>
		/// <param name="settings">Param�tres de parsing (optionnels)</param>
		/// <returns>Valeur JSON correspondante, ou null si <paramref name="jsonBytes"/> est vide</returns>
		/// <exception cref="System.FormatException">En cas d'erreur de syntaxe JSON</exception>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static JsonValue Parse(byte[]? jsonBytes, CrystalJsonSettings? settings = null)
		{
			return Parse(jsonBytes.AsSlice(), settings);
		}

		/// <summary>Parse un buffer contenant du JSON</summary>
		/// <param name="jsonBytes">Bloc de donn�es contenant du texte JSON encod� en UTF-8</param>
		/// <param name="index">Offset dans <paramref name="jsonBytes"/> du premier octet</param>
		/// <param name="count">Nombre d'octets dans <paramref name="jsonBytes"/></param>
		/// <param name="settings">Param�tres de parsing (optionnels)</param>
		/// <returns>Valeur JSON correspondante</returns>
		/// <exception cref="FormatException">En cas d'erreur de syntaxe JSON</exception>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static JsonValue Parse(byte[] jsonBytes, int index, int count, CrystalJsonSettings? settings = null)
		{
			return ParseFromReader(new JsonSliceReader(jsonBytes.AsSlice(index, count)), settings);
		}

		/// <summary>Parse un buffer contenant du JSON</summary>
		/// <param name="jsonBytes">Bloc de donn�es contenant du texte JSON encod� en UTF-8</param>
		/// <param name="settings">Param�tres de parsing (optionnels)</param>
		/// <returns>Valeur JSON correspondante</returns>
		/// <exception cref="FormatException">En cas d'erreur de syntaxe JSON</exception>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static JsonValue Parse(Slice jsonBytes, CrystalJsonSettings? settings = null)
		{
			return ParseFromReader(new JsonSliceReader(jsonBytes), settings);
		}

		/// <summary>Parse un buffer contenant du JSON</summary>
		/// <param name="jsonBytes">Bloc de donn�es contenant du texte JSON encod� en UTF-8</param>
		/// <param name="settings">Param�tres de parsing (optionnels)</param>
		/// <returns>Valeur JSON correspondante</returns>
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

		/// <summary>Parse un buffer contenant du JSON</summary>
		/// <param name="jsonBytes">Bloc de donn�es contenant du texte JSON encod� en UTF-8</param>
		/// <param name="settings">Param�tres de parsing (optionnels)</param>
		/// <returns>Valeur JSON correspondante</returns>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static JsonValue Parse(ReadOnlyMemory<byte> jsonBytes, CrystalJsonSettings? settings = null)
		{
			return Parse(jsonBytes.Span, settings);
		}

		/// <summary>Parse un buffer contenant du JSON</summary>
		/// <param name="jsonBytes">Bloc de donn�es contenant du texte JSON encod� en UTF-8</param>
		/// <param name="settings">Param�tres de parsing (optionnels)</param>
		/// <returns>Valeur JSON correspondante</returns>
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
		/// <param name="jsonText">Bloc de donn�es contenant du texte JSON</param>
		/// <param name="settings">Param�tres de parsing (optionnels)</param>
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

		/// <summary>Parse un buffer contenant du JSON</summary>
		/// <param name="jsonText">Bloc de donn�es contenant du texte JSON</param>
		/// <param name="settings">Param�tres de parsing (optionnels)</param>
		/// <returns>Valeur JSON correspondante</returns>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static JsonValue Parse(ReadOnlyMemory<char> jsonText, CrystalJsonSettings? settings = null)
		{
			return Parse(jsonText.Span, settings);
		}

		/// <summary>Parse un buffer contenant du JSON</summary>
		/// <param name="jsonText">Bloc de donn�es contenant du texte JSON</param>
		/// <param name="settings">Param�tres de parsing (optionnels)</param>
		/// <returns>Valeur JSON correspondante</returns>
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

		/// <summary>Parse une source de texte JSON</summary>
		/// <param name="reader">Source de texte JSON � parser</param>
		/// <param name="settings">Param�tres de parsing (optionnels)</param>
		/// <returns>Valeur JSON correspondante</returns>
		/// <exception cref="FormatException">En cas d'erreur de syntaxe JSON</exception>
		[Pure]
		public static JsonValue ParseFrom(TextReader reader, CrystalJsonSettings? settings = null)
		{
			Contract.NotNull(reader);
			return ParseFromReader(new JsonTextReader(reader), settings);
		}

		/// <summary>Parse une source de texte JSON</summary>
		/// <param name="source">Source de texte JSON � parser</param>
		/// <param name="settings">Param�tres de parsing (optionnels)</param>
		/// <returns>Valeur JSON correspondante</returns>
		/// <exception cref="System.FormatException">En cas d'erreur de syntaxe JSON</exception>
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

		/// <summary>Parse le contenu d'un fichier JSON sur le disque</summary>
		/// <param name="path">Nom du fichier � lire sur le disque</param>
		/// <param name="settings">Param�tres de parsing (optionnels)</param>
		/// <param name="streamFilter"></param>
		/// <param name="options"></param>
		/// <returns>Valeur JSON correspondante (ou JsonNull.Missing si le fichier est vide)</returns>
		/// <exception cref="System.FormatException">En cas d'erreur de syntaxe JSON</exception>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static JsonValue ParseFrom(string path, CrystalJsonSettings? settings = null, LoadOptions options = LoadOptions.None)
		{
			return LoadAndParseInternal(path, settings, options);
		}

		/// <summary>Parse le contenu d'un stream contenant du JSON </summary>
		/// <param name="source">Stream � d�coder</param>
		/// <param name="settings">Param�tres de parsing (optionnels)</param>
		/// <returns>Valeur JSON correspondante (ou JsonNull.Missing si le fichier est vide)</returns>
		/// <exception cref="System.FormatException">En cas d'erreur de syntaxe JSON</exception>
		[Pure]
		public static JsonValue ParseFrom(Stream source, CrystalJsonSettings? settings = null)
		{
			Contract.NotNull(source);

			//REVIEW: on pourrait d�tecter un MemoryStream et directement lire le buffer s'il est accessible, mais il faut s'assurer
			// que dans tous les cas (succ�s ou erreur), on seek le MemoryStream exactement comme si on l'avait consomm� directement !

			using (var reader = new StreamReader(source, Encoding.UTF8, true))
			{
				return ParseFromReader(new JsonTextReader(reader), settings);
			}
		}

		/// <summary>Parse une cha�ne de texte JSON repr�sentant un objet</summary>
		/// <param name="jsonText">Cha�ne de texte JSON � parser (de type "{...}")</param>
		/// <param name="settings">Param�tres de parsing (optionnels)</param>
		/// <param name="required"></param>
		/// <returns>Objet JSON correspondant</returns>
		/// <exception cref="System.FormatException">En cas d'erreur de syntaxe JSON</exception>
		/// <exception cref="System.ArgumentException">Si l'objet JSON pars� n'est pas un objet</exception>
		[Pure, ContractAnnotation("required:true => notnull")]
		public static JsonObject? ParseObject(string? jsonText, CrystalJsonSettings? settings = null, bool required = false)
		{
			return Parse(jsonText, settings).AsObject(required);
		}

		/// <summary>Parse un buffer contenant un object JSON</summary>
		/// <param name="jsonBytes">Tableau contenant le document JSON � parser (de type "{...}")</param>
		/// <param name="settings">Param�tres de parsing (optionnels)</param>
		/// <param name="required"></param>
		/// <returns>Objet JSON correspondant</returns>
		/// <exception cref="System.FormatException">En cas d'erreur de syntaxe JSON</exception>
		/// <exception cref="System.ArgumentException">Si l'objet JSON pars� n'est pas un objet</exception>
		[Pure, ContractAnnotation("required:true => notnull")]
		public static JsonObject? ParseObject(byte[]? jsonBytes, CrystalJsonSettings? settings = null, bool required = false)
		{
			return Parse(jsonBytes.AsSlice(), settings).AsObject(required);
		}

		/// <summary>Parse un buffer contenant un object JSON</summary>
		/// <param name="jsonBytes">Tableau contenant le document JSON � parser (de type "{...}")</param>
		/// <param name="count"></param>
		/// <param name="offset"></param>
		/// <param name="settings">Param�tres de parsing (optionnels)</param>
		/// <param name="required"></param>
		/// <returns>Objet JSON correspondant</returns>
		/// <exception cref="System.FormatException">En cas d'erreur de syntaxe JSON</exception>
		/// <exception cref="System.ArgumentException">Si l'objet JSON pars� n'est pas un objet</exception>
		[Pure, ContractAnnotation("required:true => notnull")]
		public static JsonObject? ParseObject(byte[]? jsonBytes, int offset, int count, CrystalJsonSettings? settings = null, bool required = false)
		{
			return Parse(jsonBytes.AsSlice(offset, count), settings).AsObject(required);
		}

		/// <summary>Parse un buffer contenant un object JSON</summary>
		/// <param name="jsonBytes">Buffer contenant le document JSON � parser (de type "{...}")</param>
		/// <param name="settings"></param>
		/// <param name="required"></param>
		/// <returns></returns>
		[Pure, ContractAnnotation("required:true => notnull")]
		public static JsonObject? ParseObject(Slice jsonBytes, CrystalJsonSettings? settings = null, bool required = false)
		{
			return Parse(jsonBytes, settings).AsObject(required);
		}

		/// <summary>Parse un buffer contenant un object JSON</summary>
		/// <param name="jsonBytes">Buffer contenant le document JSON � parser (de type "{...}")</param>
		/// <param name="settings"></param>
		/// <param name="required"></param>
		/// <returns></returns>
		[Pure, ContractAnnotation("required:true => notnull")]
		public static JsonObject? ParseObject(ReadOnlySpan<byte> jsonBytes, CrystalJsonSettings? settings = null, bool required = false)
		{
			return Parse(jsonBytes, settings).AsObject(required);
		}

		/// <summary>Parse un buffer contenant un object JSON</summary>
		/// <param name="jsonBytes">Buffer contenant le document JSON � parser (de type "{...}")</param>
		/// <param name="settings"></param>
		/// <param name="required"></param>
		/// <returns></returns>
		[Pure, ContractAnnotation("required:true => notnull")]
		public static JsonObject? ParseObject(ReadOnlyMemory<byte> jsonBytes, CrystalJsonSettings? settings = null, bool required = false)
		{
			return Parse(jsonBytes, settings).AsObject(required);
		}

		/// <summary>Parse un buffer contenant un object JSON</summary>
		/// <param name="jsonBytes">Buffer contenant le document JSON � parser (de type "{...}")</param>
		/// <param name="settings"></param>
		/// <param name="required"></param>
		/// <returns></returns>
		[Pure, ContractAnnotation("required:true => notnull")]
		public static JsonObject? ParseObject(ref ReadOnlySequence<byte> jsonBytes, CrystalJsonSettings? settings = null, bool required = false)
		{
			return Parse(ref jsonBytes, settings).AsObject(required);
		}

		/// <summary>Parse un buffer contenant un object JSON</summary>
		/// <param name="jsonText">Buffer contenant le document JSON � parser (de type "{...}")</param>
		/// <param name="settings"></param>
		/// <param name="required"></param>
		/// <returns></returns>
		[Pure, ContractAnnotation("required:true => notnull")]
		public static JsonObject? ParseObject(ReadOnlySpan<char> jsonText, CrystalJsonSettings? settings = null, bool required = false)
		{
			return Parse(jsonText, settings).AsObject(required);
		}

		/// <summary>Parse un buffer contenant un object JSON</summary>
		/// <param name="jsonText">Buffer contenant le document JSON � parser (de type "{...}")</param>
		/// <param name="settings"></param>
		/// <param name="required"></param>
		/// <returns></returns>
		[Pure, ContractAnnotation("required:true => notnull")]
		public static JsonObject? ParseObject(ReadOnlyMemory<char> jsonText, CrystalJsonSettings? settings = null, bool required = false)
		{
			return Parse(jsonText, settings).AsObject(required);
		}

		/// <summary>Parse un buffer contenant un object JSON</summary>
		/// <param name="jsonText">Buffer contenant le document JSON � parser (de type "{...}")</param>
		/// <param name="settings"></param>
		/// <param name="required"></param>
		/// <returns></returns>
		[Pure, ContractAnnotation("required:true => notnull")]
		public static JsonObject? ParseObject(ref ReadOnlySequence<char> jsonText, CrystalJsonSettings? settings = null, bool required = false)
		{
			return Parse(ref jsonText, settings).AsObject(required);
		}

		/// <summary>Parse une source de texte JSON repr�sentant un objet</summary>
		/// <param name="source">Source de texte JSON � parser (de type "{...}")</param>
		/// <param name="settings">Param�tres de parsing (optionnels)</param>
		/// <param name="required"></param>
		/// <returns>Objet JSON correspondant</returns>
		/// <exception cref="System.FormatException">En cas d'erreur de syntaxe JSON</exception>
		/// <exception cref="System.ArgumentException">Si l'objet JSON pars� n'est pas un objet</exception>
		[Pure, ContractAnnotation("required:true => notnull")]
		public static JsonObject? ParseObjectFrom(TextReader source, CrystalJsonSettings? settings = null, bool required = false)
		{
			return ParseFromReader(new JsonTextReader(source), settings).AsObject(required);
		}

		/// <summary>Parse le contenu d'un stream contenant du JSON </summary>
		/// <param name="source">Stream � d�coder</param>
		/// <param name="settings">Param�tres de parsing (optionnels)</param>
		/// <param name="required"></param>
		/// <returns>Valeur JSON correspondante (ou JsonNull.Missing si le fichier est vide)</returns>
		/// <exception cref="System.FormatException">En cas d'erreur de syntaxe JSON</exception>
		[Pure, ContractAnnotation("required:true => notnull")]
		public static JsonObject? ParseObjectFrom(Stream source, CrystalJsonSettings? settings = null, bool required = false)
		{
			return ParseFrom(source, settings).AsObject(required);
		}

		/// <summary>Parse un fichier JSON sur le disque repr�sentant un objet</summary>
		/// <param name="path">Chemin du fichier � lire</param>
		/// <param name="settings">Param�tres de parsing (optionnels)</param>
		/// <param name="required"></param>
		/// <param name="streamFilter">Filtres utilis� pour d�corer le stream (crypto, d�compression, ...)</param>
		/// <param name="options">Options de lecture</param>
		/// <returns>Objet JSON correspondant</returns>
		/// <exception cref="System.FormatException">En cas d'erreur de syntaxe JSON</exception>
		/// <exception cref="System.ArgumentException">Si l'objet JSON pars� n'est pas un objet</exception>
		[Pure, ContractAnnotation("required:true => notnull")]
		public static JsonObject? ParseObjectFrom(string path, CrystalJsonSettings? settings = null, bool required = false, LoadOptions options = LoadOptions.None)
		{
			return LoadAndParseInternal(path, settings, options).AsObject(required);
		}

		/// <summary>Parse un buffer contenant une array JSON</summary>
		/// <param name="jsonBytes">Tableau contenant le document JSON � parser (de type "[...]")</param>
		/// <param name="settings">Param�tres de parsing (optionnels)</param>
		/// <param name="required"></param>
		/// <returns>Array JSON correspondante</returns>
		/// <exception cref="System.FormatException">En cas d'erreur de syntaxe JSON</exception>
		/// <exception cref="System.ArgumentException">Si l'objet JSON pars� n'est pas une array</exception>
		[Pure, ContractAnnotation("required:true => notnull")]
		public static JsonArray? ParseArray(byte[]? jsonBytes, CrystalJsonSettings? settings = null, bool required = false)
		{
			return Parse(jsonBytes.AsSlice(), settings).AsArray(required);
		}

		/// <summary>Parse un buffer contenant une array JSON</summary>
		/// <param name="jsonBytes">Tableau contenant le document JSON � parser (de type "[...]")</param>
		/// <param name="count"></param>
		/// <param name="offset"></param>
		/// <param name="settings">Param�tres de parsing (optionnels)</param>
		/// <param name="required"></param>
		/// <returns>Array JSON correspondante</returns>
		/// <exception cref="System.FormatException">En cas d'erreur de syntaxe JSON</exception>
		/// <exception cref="System.ArgumentException">Si l'objet JSON pars� n'est pas une array</exception>
		[Pure, ContractAnnotation("required:true => notnull")]
		public static JsonArray? ParseArray(byte[]? jsonBytes, int offset, int count, CrystalJsonSettings? settings = null, bool required = false)
		{
			return Parse(jsonBytes.AsSlice(offset, count), settings).AsArray(required);
		}

		/// <summary>Parse un buffer contenant une array JSON</summary>
		/// <param name="jsonBytes">Buffer contenant le document JSON � parser (de type "[...]")</param>
		/// <param name="settings">Param�tres de parsing (optionnels)</param>
		/// <param name="required"></param>
		/// <returns>Array JSON correspondante</returns>
		/// <exception cref="System.FormatException">En cas d'erreur de syntaxe JSON</exception>
		/// <exception cref="System.ArgumentException">Si l'objet JSON pars� n'est pas une array</exception>
		[Pure, ContractAnnotation("required:true => notnull")]
		public static JsonArray? ParseArray(Slice jsonBytes, CrystalJsonSettings? settings = null, bool required = false)
		{
			return Parse(jsonBytes, settings).AsArray(required);
		}

		/// <summary>Parse un buffer contenant une array JSON</summary>
		/// <param name="jsonBytes">Buffer contenant le document JSON � parser (de type "[...]")</param>
		/// <param name="settings">Param�tres de parsing (optionnels)</param>
		/// <param name="required"></param>
		/// <returns>Array JSON correspondante</returns>
		/// <exception cref="System.FormatException">En cas d'erreur de syntaxe JSON</exception>
		/// <exception cref="System.ArgumentException">Si l'objet JSON pars� n'est pas une array</exception>
		[Pure, ContractAnnotation("required:true => notnull")]
		public static JsonArray? ParseArray(ReadOnlySpan<byte> jsonBytes, CrystalJsonSettings? settings = null, bool required = false)
		{
			return Parse(jsonBytes, settings).AsArray(required);
		}

		/// <summary>Parse un buffer contenant une array JSON</summary>
		/// <param name="jsonBytes">Buffer contenant le document JSON � parser (de type "[...]")</param>
		/// <param name="settings">Param�tres de parsing (optionnels)</param>
		/// <param name="required"></param>
		/// <returns>Array JSON correspondante</returns>
		/// <exception cref="System.FormatException">En cas d'erreur de syntaxe JSON</exception>
		/// <exception cref="System.ArgumentException">Si l'objet JSON pars� n'est pas une array</exception>
		[Pure, ContractAnnotation("required:true => notnull")]
		public static JsonArray? ParseArray(ReadOnlyMemory<byte> jsonBytes, CrystalJsonSettings? settings = null, bool required = false)
		{
			return Parse(jsonBytes, settings).AsArray(required);
		}

		/// <summary>Parse un buffer contenant une array JSON</summary>
		/// <param name="jsonBytes">Buffer contenant le document JSON � parser (de type "[...]")</param>
		/// <param name="settings">Param�tres de parsing (optionnels)</param>
		/// <param name="required"></param>
		/// <returns>Array JSON correspondante</returns>
		/// <exception cref="System.FormatException">En cas d'erreur de syntaxe JSON</exception>
		/// <exception cref="System.ArgumentException">Si l'objet JSON pars� n'est pas une array</exception>
		[Pure, ContractAnnotation("required:true => notnull")]
		public static JsonArray? ParseArray(ref ReadOnlySequence<byte> jsonBytes, CrystalJsonSettings? settings = null, bool required = false)
		{
			return Parse(ref jsonBytes, settings).AsArray(required);
		}

		/// <summary>Parse un buffer contenant une array JSON</summary>
		/// <param name="jsonText">Buffer contenant le document JSON � parser (de type "[...]")</param>
		/// <param name="settings">Param�tres de parsing (optionnels)</param>
		/// <param name="required"></param>
		/// <returns>Array JSON correspondante</returns>
		/// <exception cref="System.FormatException">En cas d'erreur de syntaxe JSON</exception>
		/// <exception cref="System.ArgumentException">Si l'objet JSON pars� n'est pas une array</exception>
		[Pure, ContractAnnotation("required:true => notnull")]
		public static JsonArray? ParseArray(ReadOnlySpan<char> jsonText, CrystalJsonSettings? settings = null, bool required = false)
		{
			return Parse(jsonText, settings).AsArray(required);
		}

		/// <summary>Parse un buffer contenant une array JSON</summary>
		/// <param name="jsonText">Buffer contenant le document JSON � parser (de type "[...]")</param>
		/// <param name="settings">Param�tres de parsing (optionnels)</param>
		/// <param name="required"></param>
		/// <returns>Array JSON correspondante</returns>
		/// <exception cref="System.FormatException">En cas d'erreur de syntaxe JSON</exception>
		/// <exception cref="System.ArgumentException">Si l'objet JSON pars� n'est pas une array</exception>
		[Pure, ContractAnnotation("required:true => notnull")]
		public static JsonArray? ParseArray(ReadOnlyMemory<char> jsonText, CrystalJsonSettings? settings = null, bool required = false)
		{
			return Parse(jsonText, settings).AsArray(required);
		}

		/// <summary>Parse un buffer contenant une array JSON</summary>
		/// <param name="jsonText">Buffer contenant le document JSON � parser (de type "[...]")</param>
		/// <param name="settings">Param�tres de parsing (optionnels)</param>
		/// <param name="required"></param>
		/// <returns>Array JSON correspondante</returns>
		/// <exception cref="System.FormatException">En cas d'erreur de syntaxe JSON</exception>
		/// <exception cref="System.ArgumentException">Si l'objet JSON pars� n'est pas une array</exception>
		[Pure, ContractAnnotation("required:true => notnull")]
		public static JsonArray? ParseArray(ref ReadOnlySequence<char> jsonText, CrystalJsonSettings? settings = null, bool required = false)
		{
			return Parse(ref jsonText, settings).AsArray(required);
		}

		/// <summary>Parse une cha�ne de texte JSON repr�sentant un tableau</summary>
		/// <param name="jsonText">Cha�ne de texte JSON � parser (de type "[...]")</param>
		/// <param name="settings">Param�tres de parsing (optionnels)</param>
		/// <param name="required"></param>
		/// <returns>Tableau JSON correspondant</returns>
		/// <exception cref="System.FormatException">En cas d'erreur de syntaxe JSON</exception>
		/// <exception cref="System.ArgumentException">Si l'objet JSON pars� n'est pas un tableau</exception>
		[Pure, ContractAnnotation("required:true => notnull")]
		public static JsonArray? ParseArray(string? jsonText, CrystalJsonSettings? settings = null, bool required = false)
		{
			return Parse(jsonText, settings).AsArray(required);
		}

		/// <summary>Parse une source de texte JSON repr�sentant un tableau</summary>
		/// <param name="source">Source de texte JSON � parser (de type "[...]")</param>
		/// <param name="settings">Param�tres de parsing (optionnels)</param>
		/// <param name="required"></param>
		/// <returns>Tableau JSON correspondant</returns>
		/// <exception cref="System.FormatException">En cas d'erreur de syntaxe JSON</exception>
		/// <exception cref="System.ArgumentException">Si l'objet JSON pars� n'est pas un tableau</exception>
		[Pure, ContractAnnotation("required:true => notnull")]
		public static JsonArray? ParseArrayFrom(TextReader source, CrystalJsonSettings? settings = null, bool required = false)
		{
			return ParseFrom(source, settings).AsArray(required);
		}

		/// <summary>Parse un fichier JSON sur le disque repr�sentant une array</summary>
		/// <param name="path">Chemin du fichier � lire</param>
		/// <param name="settings">Param�tres de parsing (optionnels)</param>
		/// <param name="required"></param>
		/// <param name="streamFilter">Filtres utilis� pour d�corer le stream (crypto, d�compression, ...)</param>
		/// <param name="options">Options de lecture</param>
		/// <returns>Array JSON correspondante</returns>
		/// <exception cref="System.FormatException">En cas d'erreur de syntaxe JSON</exception>
		/// <exception cref="System.ArgumentException">Si l'objet JSON pars� n'est pas une array</exception>
		[Pure, ContractAnnotation("required:true => notnull")]
		public static JsonArray? ParseArrayFrom(string path, CrystalJsonSettings? settings = null, bool required = false, LoadOptions options = LoadOptions.None)
		{
			return LoadAndParseInternal(path, settings, options).AsArray(required);
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

		/// <summary>Essayes de d�terminer si le buffer contient un document JSON (object ou array)</summary>
		/// <param name="jsonBytes">Buffer contenant un document JSON (encod� en UTF-8 ou ASCII)</param>
		/// <returns>True si le document pourrait �tre du JSON (object "{...}" ou array "[...]")</returns>
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

			// on recup le premier et dernier caract�re valide (en skippant les espaces de chaque cot�)
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

		/// <summary>Cr�e un reader sur un fichier sur le disque</summary>
		/// <param name="path">Chemin du fichier � lire</param>
		/// <param name="settings"></param>
		/// <param name="streamFilter">Filtre utilis� pour d�corer le stream (crypto, d�compression, ...)</param>
		/// <returns>Reader pr�t � lire depuis le fichier</returns>
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

		#region D�s�rialisation directe...

		/// <summary>D�s�rialise une cha�ne de texte JSON en l'objet CLR le plus appropri�</summary>
		/// <param name="jsonText">Texte JSON � d�s�rialiser</param>
		/// <param name="settings">Param�tres utilis�s pour le parsing JSON</param>
		/// <param name="customResolver">Resolver optionnel</param>
		/// <returns>Objet correspondant (dont le type d�pend du contexte)</returns>
		/// <exception cref="FormatException">En cas d'erreur de parsing JSON</exception>
		/// <remarks>A n'utiliser que si vous ne connaissez absolument pas le type attendu!</remarks>
		[Pure]
		[Obsolete("Please avoid doing untyped deserialization!")]
		public static object? DeserializeBoxed(string jsonText, CrystalJsonSettings? settings = null, ICrystalJsonTypeResolver? customResolver = null)
		{
			return BindBoxed(Parse(jsonText, settings), null, customResolver);
		}

		/// <summary>D�s�rialise une valeure JSON en l'objet CLR le plus appropri�</summary>
		/// <param name="value">Valeure JSON � d�s�rialiser</param>
		/// <param name="type"></param>
		/// <param name="customResolver">Resolver optionnel</param>
		/// <returns>Objet correspondant (dont le type d�pend du contexte)</returns>
		/// <remarks>A n'utiliser que si vous ne connaissez absolument pas le type attendu!</remarks>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static object? BindBoxed(JsonValue? value, Type? type, ICrystalJsonTypeResolver? customResolver = null)
		{
			return value == null ? null : (customResolver ?? CrystalJson.DefaultResolver).BindJsonValue(type, value);
		}

		#endregion

		#region D�s�rialisation vers un type d�fini

		/// <summary>D�-s�rialise une chaine de texte JSON vers un type d�fini</summary>
		/// <param name="jsonText">Texte JSON � d�-s�rialiser</param>
		/// <param name="settings">Param�tres utilis�s pour le parsing JSON</param>
		/// <param name="customResolver">Resolver optionnel</param>
		/// <param name="required">Si true, g�n�re une exception si le text JSON est vide ou "null"</param>
		/// <returns>Objet correspondant</returns>
		/// <remarks>Si <paramref name="required"/> vaut true, et que <typeparamref name="T"/> est un ValueType, une exception sera tout de m�me g�n�r�e si <paramref name="jsonText"/> est vide ou vaut 'null' explicitement</remarks>
		/// <exception cref="FormatException">En cas d'erreur de parsing JSON</exception>
		/// <exception cref="InvalidOperationException">Si l'objet d�-s�rialis� est 'null' et <paramref name="required"/> vaut true</exception>
		[Pure, ContractAnnotation("required:true => notnull"), MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static T? Deserialize<T>(string jsonText, CrystalJsonSettings? settings = null, ICrystalJsonTypeResolver? customResolver = null, bool required = false)
		{
			return Parse(jsonText, settings).As<T>(required, customResolver);
		}

		/// <summary>D�-s�rialise une source de texte JSON vers un type d�fini</summary>
		/// <param name="jsonBytes">Buffer contenant du texte JSON encod� en UTF-8</param>
		/// <param name="settings">Param�tres utilis�s pour le parsing JSON</param>
		/// <param name="customResolver">Resolver optionnel</param>
		/// <param name="required">Si true, g�n�re une exception si le text JSON est vide ou "null"</param>
		/// <returns>Objet correspondant</returns>
		/// <exception cref="FormatException">En cas d'erreur de parsing JSON</exception>
		/// <exception cref="InvalidOperationException">Si l'objet d�-s�rialis� est 'null' et <paramref name="required"/> vaut true</exception>
		[Pure, ContractAnnotation("required:true => notnull"), MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static T? Deserialize<T>(byte[] jsonBytes, CrystalJsonSettings? settings = null, ICrystalJsonTypeResolver? customResolver = null, bool required = false)
		{
			return Parse(jsonBytes.AsSlice(), settings).As<T>(required, customResolver);
		}

		/// <summary>D�-s�rialise une source de texte JSON vers un type d�fini</summary>
		/// <param name="jsonBytes">Buffer contenant du texte JSON encod� en UTF-8</param>
		/// <param name="index">Offset de d�part dans <paramref name="jsonBytes"/></param>
		/// <param name="count">Nombre d'octets � parser</param>
		/// <param name="settings">Param�tres utilis�s pour le parsing JSON</param>
		/// <param name="customResolver">Resolver optionnel</param>
		/// <param name="required">Si true, g�n�re une exception si le text JSON est vide ou "null"</param>
		/// <returns>Objet correspondant</returns>
		/// <exception cref="FormatException">En cas d'erreur de parsing JSON</exception>
		/// <exception cref="InvalidOperationException">Si l'objet d�-s�rialis� est 'null' et <paramref name="required"/> vaut true</exception>
		[Pure, ContractAnnotation("required:true => notnull"), MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static T? Deserialize<T>(byte[]? jsonBytes, int index, int count, CrystalJsonSettings? settings = null, ICrystalJsonTypeResolver? customResolver = null, bool required = false)
		{
			return Parse(jsonBytes.AsSlice(index, count), settings).As<T>(required, customResolver);
		}

		/// <summary>D�-s�rialise une source de texte JSON vers un type d�fini</summary>
		/// <param name="jsonBytes">Buffer contenant du texte JSON encod� en UTF-8</param>
		/// <param name="settings">Param�tres utilis�s pour le parsing JSON</param>
		/// <param name="customResolver">Resolver optionnel</param>
		/// <param name="required">Si true, g�n�re une exception si le text JSON est vide ou "null"</param>
		/// <returns>Objet correspondant</returns>
		/// <exception cref="FormatException">En cas d'erreur de parsing JSON</exception>
		[Pure, ContractAnnotation("required:true => notnull"), MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static T? Deserialize<T>(Slice jsonBytes, CrystalJsonSettings? settings = null, ICrystalJsonTypeResolver? customResolver = null, bool required = false)
		{
			return Parse(jsonBytes, settings).As<T>(required, customResolver);
		}

		/// <summary>D�s�rialise une source de texte JSON vers un type d�fini</summary>
		/// <param name="jsonBytes">Buffer contenant du texte JSON encod� en UTF-8</param>
		/// <param name="settings">Param�tres utilis�s pour le parsing JSON</param>
		/// <param name="customResolver">Resolver optionnel</param>
		/// <param name="required">Si true, g�n�re une exception si le text JSON est vide ou "null"</param>
		/// <returns>Objet correspondant</returns>
		/// <exception cref="FormatException">En cas d'erreur de parsing JSON</exception>
		[Pure, ContractAnnotation("required:true => notnull"), MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static T? Deserialize<T>(ReadOnlySpan<byte> jsonBytes, CrystalJsonSettings? settings = null, ICrystalJsonTypeResolver? customResolver = null, bool required = false)
		{
			return Parse(jsonBytes, settings).As<T>(required, customResolver);
		}

		/// <summary>D�s�rialise une source de texte JSON vers un type d�fini</summary>
		/// <param name="jsonBytes">Buffer contenant du texte JSON encod� en UTF-8</param>
		/// <param name="settings">Param�tres utilis�s pour le parsing JSON</param>
		/// <param name="customResolver">Resolver optionnel</param>
		/// <param name="required">Si true, g�n�re une exception si le text JSON est vide ou "null"</param>
		/// <returns>Objet correspondant</returns>
		/// <exception cref="FormatException">En cas d'erreur de parsing JSON</exception>
		[Pure, ContractAnnotation("required:true => notnull"), MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static T? Deserialize<T>(ReadOnlyMemory<byte> jsonBytes, CrystalJsonSettings? settings = null, ICrystalJsonTypeResolver? customResolver = null, bool required = false)
		{
			return Parse(jsonBytes, settings).As<T>(required, customResolver);
		}

		/// <summary>D�-s�rialise une source de texte JSON vers un type d�fini</summary>
		/// <param name="source">Source contenant le texte JSON � d�-s�rialiser</param>
		/// <param name="settings">Param�tres utilis�s pour le parsing JSON</param>
		/// <param name="customResolver">Resolver optionnel</param>
		/// <param name="required">Si true, l'objet retourne ne peut pas �tre null (si c'est le cas, un exception est g�n�r�e � la place</param>
		/// <returns>Objet correspondant</returns>
		/// <exception cref="FormatException">En cas d'erreur de parsing JSON</exception>
		/// <exception cref="InvalidOperationException">Si l'objet d�-s�rialis� est 'null' alors que <paramref name="required"/> vaut true</exception>
		[Pure, ContractAnnotation("required:true => notnull"), MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static T? LoadFrom<T>(TextReader source, CrystalJsonSettings? settings = null, ICrystalJsonTypeResolver? customResolver = null, bool required = false)
		{
			return ParseFrom(source, settings).As<T>(required, customResolver);
		}

		/// <summary>D�-s�rialise une source de donn�es JSON vers un type d�fini</summary>
		/// <param name="source">Source contenant le JSON � d�-s�rialiser (encod� en UTF-8)</param>
		/// <param name="settings">Param�tres utilis�s pour le parsing JSON</param>
		/// <param name="customResolver">Resolver optionnel</param>
		/// <param name="required">Si true, l'objet retourne ne peut pas �tre null (si c'est le cas, un exception est g�n�r�e � la place</param>
		/// <returns>Objet correspondant</returns>
		/// <exception cref="FormatException">En cas d'erreur de parsing JSON</exception>
		/// <exception cref="InvalidOperationException">Si l'objet d�-s�rialis� est 'null' alors que <paramref name="required"/> vaut true</exception>
		public static T? LoadFrom<T>(Stream source, CrystalJsonSettings? settings = null, ICrystalJsonTypeResolver? customResolver = null, bool required = false)
		{
			Contract.NotNull(source);

			//REVIEW: on pourrait d�tecter un MemoryStream et directement lire le buffer s'il est accessible, mais il faut s'assurer
			// que dans tous les cas (succ�s ou erreur), on seek le MemoryStream exactement comme si on l'avait consomm� directement !

			using (var sr = new StreamReader(source, Encoding.UTF8, true))
			{
				return ParseFromReader(new JsonTextReader(sr), settings).As<T>(required, customResolver);
			}
		}

		/// <summary>D�-s�rialise le contenu d'un fichier JSON sur le disque vers un type d�fini</summary>
		/// <param name="path">Nom du fichier � lire</param>
		/// <param name="settings">Param�tres utilis�s pour le parsing JSON</param>
		/// <param name="customResolver">Resolver optionnel</param>
		/// <param name="options">Options de lecture</param>
		/// <param name="required">Si true et que l'objet est null, une exception est g�n�r�e � la place</param>
		/// <returns>Objet correspondant</returns>
		/// <exception cref="FormatException">En cas d'erreur de parsing JSON</exception>
		/// <exception cref="InvalidOperationException">Si l'objet d�-s�rialis� est 'null' et <paramref name="required"/> vaut true</exception>
		[Pure, ContractAnnotation("required:true => notnull")]
		public static T? LoadFrom<T>(string path, CrystalJsonSettings? settings = null, ICrystalJsonTypeResolver? customResolver = null, bool required = false, LoadOptions options = LoadOptions.None)
		{
			return LoadAndParseInternal(path, settings ?? CrystalJsonSettings.Json, options).As<T>(required, customResolver);
		}

		#endregion

		#endregion

		#region Helpers...

		public const long Ticks1970Jan1 = 621355968000000000; // = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).Ticks;

		/// <summary>Retourne le nombre de ticks JavaScript correspondant � une date</summary>
		internal static long DateToJavaScriptTicks(DateTime date)
		{
			long ticks = ((date != DateTime.MinValue && date != DateTime.MaxValue && date.Kind != DateTimeKind.Utc) ? date.ToUniversalTime() : date).Ticks;
			return (ticks - Ticks1970Jan1) / TimeSpan.TicksPerMillisecond;
		}

		internal static long DateToJavaScriptTicks(DateTimeOffset date)
		{
			return (date.UtcTicks - Ticks1970Jan1) / TimeSpan.TicksPerMillisecond;
		}

		/// <summary>Retourne la date correspondant � un nombre de ticks JavaScript</summary>
		internal static DateTime JavaScriptTicksToDate(long ticks)
		{
			return new DateTime((ticks * TimeSpan.TicksPerMillisecond) + Ticks1970Jan1, DateTimeKind.Utc);
		}

		/// <summary>Encode une cha�ne en JSON</summary>
		/// <param name="text">Cha�ne � encoder</param>
		/// <returns>'null', '""', '"foo"', '"\""', '"\u0000"', ...</returns>
		/// <remarks>Chaine correctement encod�e. Note: retourne "null" si text==null</remarks>
		public static string StringEncode(string text)
		{
			return JsonEncoding.Encode(text);
		}

		/// <summary>Encode une cha�ne en JSON, et append le r�sultat � un StringBuilder</summary>
		/// <param name="sb">Buffer o� �crire le r�sultat</param>
		/// <param name="text">Cha�ne � encoder</param>
		/// <returns>Le StringBuilder pass� en param�tre (pour chainage)</returns>
		/// <remarks>Note: Ajoute "null" si text==null && includeQuotes==true</remarks>
		public static StringBuilder StringAppend(StringBuilder sb, string text)
		{
			return JsonEncoding.Append(sb, text);
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
			internal static InvalidOperationException Serialization_ObjectRecursionIsNotAllowed(IEnumerable<object> visited, object value, int depth)
			{
				return new JsonSerializationException($"Object of type '{value.GetType().FullName}' at depth {depth} already serialized before! Recursive object graphs not supported. Visited path: {string.Join(" <- ", visited.Select(v => v?.GetType().FullName ?? "<null>"))}");
			}

			[Pure, MethodImpl(MethodImplOptions.NoInlining)]
			internal static JsonSerializationException Serialization_InternalDepthInconsistent()
			{
				return new JsonSerializationException("Internal depth is inconsistent.");
			}

			[Pure, MethodImpl(MethodImplOptions.NoInlining)]
			internal static JsonSerializationException Serialization_LeaveNotSameThanMark(int depth, object current)
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
#if !NETFRAMEWORK && !NETSTANDARD
				if (memberType.IsByRefLike) return new JsonSerializationException($"Cannot serialize {(info is PropertyInfo ? "property" : "field")} {type.GetFriendlyName()}.{info.Name} with type {memberType.GetFriendlyName()}: ref-like types are NOT supported.", error);
#endif
				return new JsonSerializationException($"Cannot generate default value for {(info is PropertyInfo ? "property" : "field")} {type.GetFriendlyName()}.{info.Name} with type {memberType.GetFriendlyName()}.", error);
			}


			#endregion

			#region Parsing Errors...

			[Pure, MethodImpl(MethodImplOptions.NoInlining)]
			internal static JsonSyntaxException Parsing_CannotCastToJsonObject(JsonType valueType)
			{
				return new JsonSyntaxException($"Cannot parse JSON {valueType} as an Object.");
			}

			[Pure, MethodImpl(MethodImplOptions.NoInlining)]
			internal static JsonSyntaxException Parsing_CannotCastToJsonArray(JsonType valueType)
			{
				return new JsonSyntaxException($"Cannot parse JSON {valueType} as an Array.");
			}

			[Pure, MethodImpl(MethodImplOptions.NoInlining)]
			internal static JsonSyntaxException Parsing_CannotCastToJsonNumber(JsonType valueType)
			{
				return new JsonSyntaxException($"Cannot parse JSON {valueType} as a Number.");
			}

			[Pure, MethodImpl(MethodImplOptions.NoInlining)]
			internal static JsonSyntaxException Parsing_CannotCastToJsonString(JsonType valueType)
			{
				return new JsonSyntaxException($"Cannot parse JSON {valueType} as a String.");
			}

			#endregion

			#region Deserialization Errors...

			[Pure, MethodImpl(MethodImplOptions.NoInlining)]
			internal static JsonBindingException Binding_CouldNotResolveClassId(string classId)
			{
				return new JsonBindingException($"Could not find any Type named '{classId}' during deserialization.");
			}

			[Pure, MethodImpl(MethodImplOptions.NoInlining)]
			internal static JsonBindingException Binding_CannotDeserializeCustomTypeNoBinderOrGenerator(JsonValue value, Type type)
			{
				return new JsonBindingException($"Cannot deserialize custom type '{type.GetFriendlyName()}' because it has no default generator and no custom binder.", value);
			}

			[Pure, MethodImpl(MethodImplOptions.NoInlining)]
			internal static JsonBindingException Binding_CannotDeserializeCustomTypeNoTypeDefinition(JsonValue value, Type type)
			{
				return new JsonBindingException($"Could not find any Type Definition while deserializing custom type '{type.GetFriendlyName()}'.", value);
			}

			[Pure, MethodImpl(MethodImplOptions.NoInlining)]
			internal static JsonBindingException Binding_CannotDeserializeCustomTypeNoConcreteClassFound(JsonValue value, Type type, string customClass)
			{
				return new JsonBindingException($"Could not find a concrete type to deserialize object of type '{type.GetFriendlyName()}' with custom class name '{customClass}'.", value);
			}

			internal static JsonBindingException Binding_CannotDeserializeCustomTypeBadType(JsonValue value, string customClass)
			{
				return new JsonBindingException($"Cannot bind custom class name '{customClass}' because it is not a safe type in this context.", value);
			}

			internal static JsonBindingException Binding_CannotDeserializeCustomTypeIncompatibleType(JsonValue value, Type type, string customClass)
			{
				return new JsonBindingException($"Cannot bind custom class name '{customClass}' into object of type '{type.GetFriendlyName()}' because there are no known valid cast between them.", value);
			}

			[Pure, MethodImpl(MethodImplOptions.NoInlining)]
			internal static JsonBindingException Binding_FailedToConstructTypeInstanceErrorOccurred(JsonValue value, Type type, Exception e)
			{
				return new JsonBindingException($"Failed to construct a new instance of type '{type.GetFriendlyName()}' while deserializing a {nameof(JsonObject)}.", value, e);
			}

			[Pure, MethodImpl(MethodImplOptions.NoInlining)]
			internal static JsonBindingException Binding_FailedToConstructTypeInstanceReturnedNull(JsonValue value, Type type)
			{
				return new JsonBindingException($"Cannot deserialize custom type '{type.GetFriendlyName()}' because the generator returned a null instance.", value);
			}

			[Pure, MethodImpl(MethodImplOptions.NoInlining)]
			internal static JsonBindingException Binding_CannotDeserializeCustomTypeNoReaderForMember(JsonValue value, CrystalJsonMemberDefinition member, Type type)
			{
				return new JsonBindingException($"No reader found for member {member.Name} of type '{type.GetFriendlyName()}'.", value);
			}

			[Pure, MethodImpl(MethodImplOptions.NoInlining)]
			internal static JsonBindingException Binding_CannotDeserializeCustomTypeNoBinderForMember(JsonValue value, CrystalJsonMemberDefinition member, Type type)
			{
				return new JsonBindingException($"No 'set' operation found for member {member.Name} of type '{type.GetFriendlyName()}'.", value);
			}

			[Pure, MethodImpl(MethodImplOptions.NoInlining)]
			internal static JsonBindingException Binding_CannotBindJsonObjectToThisType(JsonValue? value, Type type)
			{
				return new JsonBindingException($"Cannot bind a JSON object to type '{type.GetFriendlyName()}'.", value);
			}

			[Pure, MethodImpl(MethodImplOptions.NoInlining)]
			internal static JsonBindingException Binding_CannotBindJsonArrayToThisType(JsonValue value, Type type)
			{
				return new JsonBindingException($"Cannot bind a JSON array to type '{type.GetFriendlyName()}'.", value);
			}

			[Pure, MethodImpl(MethodImplOptions.NoInlining)]
			internal static JsonBindingException Binding_CanOnlyDeserializeIntoExpandObject(JsonValue value, Type type)
			{
				return new JsonBindingException($"This object can only be deserialized into an ExpandObject, and not '{type.GetFriendlyName()}'.", value);
			}

			[Pure, MethodImpl(MethodImplOptions.NoInlining)]
			internal static JsonBindingException Binding_CannotDeserializeJsonNullToThisType(JsonValue value, Type type)
			{
				return new JsonBindingException($"Cannot deserialize JSON null to type '{type.GetFriendlyName()}'.", value);
			}

			[Pure, MethodImpl(MethodImplOptions.NoInlining)]
			internal static JsonBindingException Binding_CannotDeserializeJsonTypeNoAssignableTo(JsonValue value, Type resultType, Type type)
			{
				return new JsonBindingException($"Deserialized JSON value type '{resultType.GetFriendlyName()}' is not assignable to expected type '{type.GetFriendlyName()}'.", value);
			}

			[Pure, MethodImpl(MethodImplOptions.NoInlining)]
			internal static JsonBindingException Binding_CannotDeserializeJsonTypeIntoArrayOf(JsonValue value, Type type)
			{
				return new JsonBindingException($"Cannot deserialize JSON type {value.Type} into an array of '{type.GetFriendlyName()}'.", value);
			}

			[Pure, MethodImpl(MethodImplOptions.NoInlining)]
			internal static JsonBindingException Binding_UnsupportedInternalJsonArrayType(JsonValue value)
			{
				return new JsonBindingException($"Unsupported internal type '{value.GetType().GetFriendlyName()}' for JSON Array.", value);
			}

			[Pure, MethodImpl(MethodImplOptions.NoInlining)]
			internal static JsonBindingException Binding_CannotBindJsonStringToThisType(JsonValue value, Type type, Exception? innerException = null)
			{
				return new JsonBindingException($"Cannot convert JSON String to type '{type.GetFriendlyName()}'.", value, innerException);
			}

			#endregion

		}

		#endregion
	}
}
