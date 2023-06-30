#region Copyright (c) 2005-2023 Doxense SAS
// All rights reserved.
// 
// Redistribution and use in source and binary forms, with or without
// modification, are permitted provided that the following conditions are met:
// 	* Redistributions of source code must retain the above copyright
// 	  notice, this list of conditions and the following disclaimer.
// 	* Redistributions in binary form must reproduce the above copyright
// 	  notice, this list of conditions and the following disclaimer in the
// 	  documentation and/or other materials provided with the distribution.
// 	* Neither the name of Doxense nor the
// 	  names of its contributors may be used to endorse or promote products
// 	  derived from this software without specific prior written permission.
// 
// THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND
// ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
// WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
// DISCLAIMED. IN NO EVENT SHALL DOXENSE BE LIABLE FOR ANY
// DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
// (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
// LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
// ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
// (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
// SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
#endregion

namespace Doxense.Serialization.Json
{
	using System;
	using System.Collections.Generic;
	using System.Diagnostics.CodeAnalysis;
	using System.Runtime.CompilerServices;
	using Doxense.Diagnostics.Contracts;
	using JetBrains.Annotations;

	public static class JsonValueExtensions
	{

		/// <summary>Test si une valeur JSON est null, ou équivalente à null</summary>
		/// <param name="value">Valeur JSON</param>
		/// <returns>True si <paramref name="value"/> est null, ou une instance de type <see cref="JsonNull"/></returns>
		[Pure, ContractAnnotation("null=>true"), MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool IsNullOrMissing([NotNullWhen(false)] this JsonValue? value)
		{
			return object.ReferenceEquals(value, null) || value.IsNull;
		}

		/// <summary>Test si une valeur JSON est l'équivalent logique de 'missing'</summary>
		/// <param name="value">Valeur JSON</param>
		/// <returns>True si <paramref name="value"/> est null, ou égal à <see cref="JsonNull.Missing"/>.</returns>
		/// <remarks><see cref="JsonNull.Null"/> n'est pas considéré comme manquant (c'est un null explicite)</remarks>
		[Pure, ContractAnnotation("null=>true"), MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool IsMissing([NotNullWhen(false)] this JsonValue? value)
		{
			//note: JsonNull.Error est un singleton, donc on peut le comparer par référence!
			return !object.ReferenceEquals(value, null) && object.ReferenceEquals(value, JsonNull.Missing);
		}


		/// <summary>Test si une valeur JSON est manquant pour cause d'une erreur de parsing</summary>
		/// <param name="value">Valeur JSON</param>
		/// <returns>True si <paramref name="value"/> est null, ou égal à <see cref="JsonNull.Error"/>.</returns>
		/// <remarks><see cref="JsonNull.Null"/> n'est pas considéré comme manquant (c'est un null explicite)</remarks>
		[Pure, ContractAnnotation("null=>true")]
		public static bool IsError([NotNullWhen(false)] this JsonValue? value)
		{
			//note: JsonNull.Error est un singleton, donc on peut le comparer par référence!
			return !object.ReferenceEquals(value, null) && object.ReferenceEquals(value, JsonNull.Error);
		}

		/// <summary>Vérifie qu'une valeur JSON est bien présente</summary>
		/// <param name="value">Valeur JSON qui ne doit pas être null ou manquante</param>
		/// <returns>La valeur JSON si elle existe. Ou une exception si elle est null ou manquante</returns>
		[ ContractAnnotation("null => halt"), MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static JsonValue Required(this JsonValue? value)
		{
			return IsNullOrMissing(value) ? FailValueIsNullOrMissing() : value;
		}

		/// <summary>Vérifie qu'une valeur JSON est bien présente dans une array</summary>
		/// <param name="value">Valeur JSON qui ne doit pas être null ou manquante</param>
		/// <param name="index">Index dans l'array qui doit être présent</param>
		/// <returns>La valeur JSON si elle existe. Ou une exception si elle est null ou manquante</returns>
		[ContractAnnotation("value:null => halt"), MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static JsonValue RequiredIndex(this JsonValue? value, int index)
		{
			return IsNullOrMissing(value) ? FailIndexIsNullOrMissing(index) : value;
		}

		/// <summary>Vérifie qu'une valeur JSON est bien présente</summary>
		/// <param name="value">Valeur JSON qui ne doit pas être null ou manquante</param>
		/// <param name="field">Nom du champ qui doit être présent</param>
		/// <returns>La valeur JSON si elle existe. Ou une exception si elle est null ou manquante</returns>
		[ContractAnnotation("value:null => halt"), MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static JsonValue RequiredField(this JsonValue? value, string field)
		{
			return IsNullOrMissing(value) ? FailFieldIsNullOrMissing(field) : value;
		}

		/// <summary>Vérifie qu'une valeur JSON est bien présente</summary>
		/// <param name="value">Valeur JSON qui ne doit pas être null ou manquante</param>
		/// <param name="path">Chemin vers le champ qui doit être présent</param>
		/// <returns>La valeur JSON si elle existe. Ou une exception si elle est null ou manquante</returns>
		[ContractAnnotation("value:null => halt"), MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static JsonValue RequiredPath(this JsonValue? value, string path)
		{
			return IsNullOrMissing(value) ? FailPathIsNullOrMissing(path) : value;
		}

		/// <summary>Vérifie qu'une valeur JSON est bien présente</summary>
		/// <param name="value">Valeur JSON qui ne doit pas être null ou manquante</param>
		/// <returns>La valeur JSON si elle existe. Ou une exception si elle est null ou manquante</returns>
		[ContractAnnotation("null => halt")]
		public static JsonArray Required(this JsonArray? value)
		{
			if (IsNullOrMissing(value)) return FailArrayIsNullOrMissing();
			return value;
		}

		/// <summary>Vérifie qu'une valeur JSON est bien présente</summary>
		/// <param name="value">Valeur JSON qui ne doit pas être null ou manquante</param>
		/// <returns>La valeur JSON si elle existe. Ou une exception si elle est null ou manquante</returns>
		[ContractAnnotation("null => halt")]
		public static JsonObject Required(this JsonObject? value)
		{
			if (IsNullOrMissing(value)) return FailObjectIsNullOrMissing();
			return value;
		}

		[DoesNotReturn, ContractAnnotation("=> halt"), MethodImpl(MethodImplOptions.NoInlining)]
		internal static JsonValue FailValueIsNullOrMissing()
		{
			throw new InvalidOperationException("Required JSON value was null or missing.");
		}

		[DoesNotReturn, ContractAnnotation("=> halt"), MethodImpl(MethodImplOptions.NoInlining)]
		internal static JsonArray FailArrayIsNullOrMissing()
		{
			throw new InvalidOperationException("Required JSON array was null or missing.");
		}

		[DoesNotReturn, ContractAnnotation("=> halt"), MethodImpl(MethodImplOptions.NoInlining)]
		internal static JsonValue FailIndexIsNullOrMissing(int index)
		{
			throw new InvalidOperationException($"Required JSON field at index {index} was null or missing.");
		}

		[DoesNotReturn, ContractAnnotation("=> halt"), MethodImpl(MethodImplOptions.NoInlining)]
		internal static JsonValue FailFieldIsNullOrMissing(string field)
		{
			throw new InvalidOperationException($"Required JSON field '{field}' was null or missing.");
		}

		[DoesNotReturn, ContractAnnotation("=> halt"), MethodImpl(MethodImplOptions.NoInlining)]
		internal static JsonValue FailFieldIsEmpty(string field)
		{
			throw new InvalidOperationException($"Required JSON field '{field}' was empty.");
		}

		[DoesNotReturn, ContractAnnotation("=> halt"), MethodImpl(MethodImplOptions.NoInlining)]
		internal static JsonValue FailPathIsNullOrMissing(string path)
		{
			throw new InvalidOperationException($"Required JSON path '{path}' was null or missing.");
		}

		#region ToStuff(...)

		/// <summary>Sérialise cette valeur JSON en texte le plus compact possible (pour du stockage)</summary>
		/// <remarks>Note: si le JSON doit être envoyés en HTTP ou sauvé sur disque, préférer <see cref="ToJsonBuffer(JsonValue)"/> ou <see cref="ToJsonBytes(JsonValue)"/></remarks>
		[Pure]
		public static string ToJsonCompact(this JsonValue? value)
		{
			return value?.ToJson(CrystalJsonSettings.JsonCompact) ?? JsonTokens.Null;
		}

		/// <summary>Sérialise cette valeur JSON en texte au format indenté (pratique pour des logs ou en mode debug)</summary>
		/// <remarks>Note: si le JSON doit être envoyés en HTTP ou sauvé sur disque, préférer <see cref="ToJsonBuffer(JsonValue)"/> ou <see cref="ToJsonBytes(JsonValue)"/></remarks>
		[Pure]
		public static string ToJsonIndented(this JsonValue? value)
		{
			return value?.ToJson(CrystalJsonSettings.JsonIndented) ?? JsonTokens.Null;
		}

		/// <summary>Sérialise cette valeur JSON en un tableau de bytes</summary>
		/// <returns>Buffer contenant le texte JSON encodé en UTF-8</returns>
		/// <remarks>A n'utiliser que si l'appelant veut absolument un tableau. Pour de l'IO, préférer <see cref="ToJsonBuffer(JsonValue)"/> qui permet d'éviter une copie inutile en mémoire</remarks>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static byte[] ToJsonBytes(this JsonValue? value)
		{
			return CrystalJson.ToBytes(value, null);
		}

		/// <summary>Sérialise cette valeur JSON en un tableau de bytes</summary>
		/// <returns>Buffer contenant le texte JSON encodé en UTF-8</returns>
		/// <remarks>A n'utiliser que si l'appelant veut absolument un tableau. Pour de l'IO, préférer <see cref="ToJsonBuffer(JsonValue, CrystalJsonSettings)"/> qui permet d'éviter une copie inutile en mémoire</remarks>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static byte[] ToJsonBytes(this JsonValue? value, CrystalJsonSettings? settings)
		{
			return CrystalJson.ToBytes(value, settings);
		}

		/// <summary>Sérialise cette valeur JSON en un buffer de bytes</summary>
		/// <returns>Buffer contenant le texte JSON encodé en UTF-8</returns>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Slice ToJsonBuffer(this JsonValue? value)
		{
			return CrystalJson.ToBuffer(value, null);
		}

		/// <summary>Sérialise cette valeur JSON en un buffer de bytes</summary>
		/// <returns>Buffer contenant le texte JSON encodé en UTF-8</returns>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Slice ToJsonBuffer(this JsonValue? value, CrystalJsonSettings? settings)
		{
			return CrystalJson.ToBuffer(value, settings);
		}

		#endregion

		#region As<T>...

		//REVIEW: "AsXXX()" en général c'est pour du casting, et "ToXXX()" pour de la conversion...
		//note: appelé ToObject<T> dans Json.NET

		/// <summary>Bind cette valeur JSON en une instance d'un type CLR</summary>
		/// <remarks>Si la valeur est null, retourne default(<typeparam name="T"/>)</remarks>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static T? As<T>(this JsonValue? value)
		{
			#region <JIT_HACK>

			// En mode RELEASE, le JIT reconnaît les patterns "if (typeof(T) == typeof(VALUETYPE)) { ... }" dans une méthode générique Foo<T> quand T est un ValueType,
			// et les remplace par des "if (true) { ...}" ce qui permet d'éliminer le reste du code (très efficace si le if contient un return!)
			// Egalement, le JIT optimise le "(VALUE_TYPE)(object)value" si T == VALUE_TYPE pour éviter le boxing inutile (le cast intermédiaire en object est pour faire taire le compilateur)
			// => pour le vérifier, il faut inspecter l'asm généré par le JIT au runtime (en mode release, en dehors du debugger, etc...) ce qui n'est pas facile...
			// => vérifié avec .NET 4.6.1 + RyuJIT x64, la méthode FromValue<int> est directement inlinée en l'appel à JsonNumber.Return(...) !

#if !DEBUG // trop lent en debug !

			//note: si value est un JsonNull, toutes les versions de ToVALUETYPE() retourne le type attendu!

			if (typeof (T) == typeof (bool)) return value != null ? (T) (object) value.ToBoolean() : default(T);
			if (typeof (T) == typeof (char)) return value != null ? (T) (object) value.ToChar() : default(T);
			if (typeof (T) == typeof (byte)) return value != null ? (T) (object) value.ToByte() : default(T);
			if (typeof (T) == typeof (sbyte)) return value != null ? (T) (object) value.ToSByte() : default(T);
			if (typeof (T) == typeof (short)) return value != null ? (T) (object) value.ToInt16() : default(T);
			if (typeof (T) == typeof (ushort)) return value != null ? (T) (object) value.ToUInt16() : default(T);
			if (typeof (T) == typeof (int)) return value != null ? (T) (object) value.ToInt32() : default(T);
			if (typeof (T) == typeof (uint)) return value != null ? (T) (object) value.ToUInt32() : default(T);
			if (typeof (T) == typeof (long)) return value != null ? (T) (object) value.ToInt64() : default(T);
			if (typeof (T) == typeof (ulong)) return value != null ? (T) (object) value.ToUInt64() : default(T);
			if (typeof (T) == typeof (float)) return value != null ? (T) (object) value.ToSingle() : default(T);
			if (typeof (T) == typeof (double)) return value != null ? (T) (object) value.ToDouble() : default(T);
			if (typeof (T) == typeof (decimal)) return value != null ? (T) (object) value.ToDecimal() : default(T);
			if (typeof (T) == typeof (Guid)) return value != null ? (T) (object) value.ToGuid() : default(T);
			if (typeof (T) == typeof (TimeSpan)) return value != null ? (T) (object) value.ToTimeSpan() : default(T);
			if (typeof (T) == typeof (DateTime)) return value != null ? (T) (object) value.ToDateTime() : default(T);
			if (typeof (T) == typeof (DateTimeOffset)) return value != null ? (T) (object) value.ToDateTimeOffset() : default(T);
			if (typeof (T) == typeof (NodaTime.Instant)) return value != null ? (T) (object) value.ToInstant() : default(T);
			if (typeof (T) == typeof (NodaTime.Duration)) return value != null ? (T) (object) value.ToDuration() : default(T);

			//note: value peut être un JsonNull, donc on doit invoquer les ...OrDefault() !
			if (typeof (T) == typeof (bool?)) return value != null ? (T) (object) value.ToBooleanOrDefault()! : default(T);
			if (typeof (T) == typeof (char?)) return value != null ? (T) (object) value.ToCharOrDefault()! : default(T);
			if (typeof (T) == typeof (byte?)) return value != null ? (T) (object) value.ToByteOrDefault()! : default(T);
			if (typeof (T) == typeof (sbyte?)) return value != null ? (T) (object) value.ToSByteOrDefault()! : default(T);
			if (typeof (T) == typeof (short?)) return value != null ? (T) (object) value.ToInt16OrDefault()! : default(T);
			if (typeof (T) == typeof (ushort?)) return value != null ? (T) (object) value.ToUInt16OrDefault()! : default(T);
			if (typeof (T) == typeof (int?)) return value != null ? (T) (object) value.ToInt32OrDefault()! : default(T);
			if (typeof (T) == typeof (uint?)) return value != null ? (T) (object) value.ToUInt32OrDefault()! : default(T);
			if (typeof (T) == typeof (long?)) return value != null ? (T) (object) value.ToInt64OrDefault()! : default(T);
			if (typeof (T) == typeof (ulong?)) return value != null ? (T) (object) value.ToUInt64OrDefault()! : default(T);
			if (typeof (T) == typeof (float?)) return value != null ? (T) (object) value.ToSingleOrDefault()! : default(T);
			if (typeof (T) == typeof (double?)) return value != null ? (T) (object) value.ToDoubleOrDefault()! : default(T);
			if (typeof (T) == typeof (decimal?)) return value != null ? (T) (object) value.ToDecimalOrDefault()! : default(T);
			if (typeof (T) == typeof (Guid?)) return value != null ? (T) (object) value.ToGuidOrDefault()! : default(T);
			if (typeof (T) == typeof (TimeSpan?)) return value != null ? (T) (object) value.ToTimeSpanOrDefault()! : default(T);
			if (typeof (T) == typeof (DateTime?)) return value != null ? (T) (object) value.ToDateTimeOrDefault()! : default(T);
			if (typeof (T) == typeof (DateTimeOffset?)) return value != null ? (T) (object) value.ToDateTimeOffsetOrDefault()! : default(T);
			if (typeof (T) == typeof (NodaTime.Instant?)) return value != null ? (T) (object) value.ToInstantOrDefault()! : default(T);
			if (typeof (T) == typeof (NodaTime.Duration?)) return value != null ? (T) (object) value.ToDurationOrDefault()! : default(T);

#endif
			#endregion </JIT_HACK>

			if (default(T) == null)
			{ // ref type
				return (T) (value ?? JsonNull.Null).Bind(typeof(T), null)!;
			}
			// value type
			return value?.IsNull != false ? JsonNull.Default<T>() : (T) value.Bind(typeof(T), null)!;
		}

		/// <summary>Bind cette valeur JSON en une instance d'un type CLR</summary>
		/// <remarks>Si la valeur est null, retourne default(<typeparam name="T"/>)</remarks>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static T? As<T>(this JsonValue? value, ICrystalJsonTypeResolver resolver)
		{
			#region <JIT_HACK>

			// En mode RELEASE, le JIT reconnaît les patterns "if (typeof(T) == typeof(VALUETYPE)) { ... }" dans une méthode générique Foo<T> quand T est un ValueType,
			// et les remplace par des "if (true) { ...}" ce qui permet d'éliminer le reste du code (très efficace si le if contient un return!)
			// Egalement, le JIT optimise le "(VALUE_TYPE)(object)value" si T == VALUE_TYPE pour éviter le boxing inutile (le cast intermédiaire en object est pour faire taire le compilateur)
			// => pour le vérifier, il faut inspecter l'asm généré par le JIT au runtime (en mode release, en dehors du debugger, etc...) ce qui n'est pas facile...
			// => vérifié avec .NET 4.6.1 + RyuJIT x64, la méthode FromValue<int> est directement inlinée en l'appel à JsonNumber.Return(...) !

#if !DEBUG // trop lent en debug !

			//note: si value est un JsonNull, toutes les versions de ToVALUETYPE() retourne le type attendu!

			if (typeof (T) == typeof (bool)) return value != null ? (T) (object) value.ToBoolean() : default(T);
			if (typeof (T) == typeof (char)) return value != null ? (T) (object) value.ToChar() : default(T);
			if (typeof (T) == typeof (byte)) return value != null ? (T) (object) value.ToByte() : default(T);
			if (typeof (T) == typeof (sbyte)) return value != null ? (T) (object) value.ToSByte() : default(T);
			if (typeof (T) == typeof (short)) return value != null ? (T) (object) value.ToInt16() : default(T);
			if (typeof (T) == typeof (ushort)) return value != null ? (T) (object) value.ToUInt16() : default(T);
			if (typeof (T) == typeof (int)) return value != null ? (T) (object) value.ToInt32() : default(T);
			if (typeof (T) == typeof (uint)) return value != null ? (T) (object) value.ToUInt32() : default(T);
			if (typeof (T) == typeof (long)) return value != null ? (T) (object) value.ToInt64() : default(T);
			if (typeof (T) == typeof (ulong)) return value != null ? (T) (object) value.ToUInt64() : default(T);
			if (typeof (T) == typeof (float)) return value != null ? (T) (object) value.ToSingle() : default(T);
			if (typeof (T) == typeof (double)) return value != null ? (T) (object) value.ToDouble() : default(T);
			if (typeof (T) == typeof (decimal)) return value != null ? (T) (object) value.ToDecimal() : default(T);
			if (typeof (T) == typeof (Guid)) return value != null ? (T) (object) value.ToGuid() : default(T);
			if (typeof (T) == typeof (TimeSpan)) return value != null ? (T) (object) value.ToTimeSpan() : default(T);
			if (typeof (T) == typeof (DateTime)) return value != null ? (T) (object) value.ToDateTime() : default(T);
			if (typeof (T) == typeof (DateTimeOffset)) return value != null ? (T) (object) value.ToDateTimeOffset() : default(T);
			if (typeof (T) == typeof (NodaTime.Instant)) return value != null ? (T) (object) value.ToInstant() : default(T);
			if (typeof (T) == typeof (NodaTime.Duration)) return value != null ? (T) (object) value.ToDuration() : default(T);

			//note: value peut être un JsonNull, donc on doit invoquer les ...OrDefault() !
			if (typeof (T) == typeof (bool?)) return value != null ? (T) (object) value.ToBooleanOrDefault()! : default(T);
			if (typeof (T) == typeof (char?)) return value != null ? (T) (object) value.ToCharOrDefault()! : default(T);
			if (typeof (T) == typeof (byte?)) return value != null ? (T) (object) value.ToByteOrDefault()! : default(T);
			if (typeof (T) == typeof (sbyte?)) return value != null ? (T) (object) value.ToSByteOrDefault()! : default(T);
			if (typeof (T) == typeof (short?)) return value != null ? (T) (object) value.ToInt16OrDefault()! : default(T);
			if (typeof (T) == typeof (ushort?)) return value != null ? (T) (object) value.ToUInt16OrDefault()! : default(T);
			if (typeof (T) == typeof (int?)) return value != null ? (T) (object) value.ToInt32OrDefault()! : default(T);
			if (typeof (T) == typeof (uint?)) return value != null ? (T) (object) value.ToUInt32OrDefault()! : default(T);
			if (typeof (T) == typeof (long?)) return value != null ? (T) (object) value.ToInt64OrDefault()! : default(T);
			if (typeof (T) == typeof (ulong?)) return value != null ? (T) (object) value.ToUInt64OrDefault()! : default(T);
			if (typeof (T) == typeof (float?)) return value != null ? (T) (object) value.ToSingleOrDefault()! : default(T);
			if (typeof (T) == typeof (double?)) return value != null ? (T) (object) value.ToDoubleOrDefault()! : default(T);
			if (typeof (T) == typeof (decimal?)) return value != null ? (T) (object) value.ToDecimalOrDefault()! : default(T);
			if (typeof (T) == typeof (Guid?)) return value != null ? (T) (object) value.ToGuidOrDefault()! : default(T);
			if (typeof (T) == typeof (TimeSpan?)) return value != null ? (T) (object) value.ToTimeSpanOrDefault()! : default(T);
			if (typeof (T) == typeof (DateTime?)) return value != null ? (T) (object) value.ToDateTimeOrDefault()! : default(T);
			if (typeof (T) == typeof (DateTimeOffset?)) return value != null ? (T) (object) value.ToDateTimeOffsetOrDefault()! : default(T);
			if (typeof (T) == typeof (NodaTime.Instant?)) return value != null ? (T) (object) value.ToInstantOrDefault()! : default(T);
			if (typeof (T) == typeof (NodaTime.Duration?)) return value != null ? (T) (object) value.ToDurationOrDefault()! : default(T);
#endif

			#endregion </JIT_HACK>

			if (default(T) == null)
			{ // ref type
				return (T?) (value ?? JsonNull.Null).Bind(typeof(T), resolver)!;
			}
			// value type
			return value?.IsNull != false ? JsonNull.Default<T>() : (T) value.Bind(typeof(T), resolver)!;
		}

		/// <summary>Bind cette valeur JSON en une instance d'un type CLR</summary>
		/// <remarks>Si la valeur est null, retourne default(<typeparam name="T"/>) si <paramref name="required"/> vaut false, ou une exception si <paramref name="required"/> vaut true</remarks>
		/// <exception cref="InvalidOperationException">Si <paramref name="value"/> est 'null' ou manquant, et que <paramref name="required"/> vaut true</exception>
		[Pure, ContractAnnotation("required:true => notnull")]
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static T? As<T>(this JsonValue? value, bool required, ICrystalJsonTypeResolver? resolver = null)
		{
			if (required && value?.IsNull != false) return FailRequiredValueIsNullOrMissing<T>();
			if (value == null) return JsonNull.Default<T>();

			#region <JIT_HACK>

			// En mode RELEASE, le JIT reconnaît les patterns "if (typeof(T) == typeof(VALUETYPE)) { ... }" dans une méthode générique Foo<T> quand T est un ValueType,
			// et les remplace par des "if (true) { ...}" ce qui permet d'éliminer le reste du code (très efficace si le if contient un return!)
			// Egalement, le JIT optimise le "(VALUE_TYPE)(object)value" si T == VALUE_TYPE pour éviter le boxing inutile (le cast intermédiaire en object est pour faire taire le compilateur)
			// => pour le vérifier, il faut inspecter l'asm généré par le JIT au runtime (en mode release, en dehors du debugger, etc...) ce qui n'est pas facile...
			// => vérifié avec .NET 4.6.1 + RyuJIT x64, la méthode FromValue<int> est directement inlinée en l'appel à JsonNumber.Return(...) !

#if !DEBUG // trop lent en debug !

			//note: si value est un JsonNull, toutes les versions de ToVALUETYPE() retourne le type attendu!

			if (typeof(T) == typeof(bool)) return (T) (object) value.ToBoolean();
			if (typeof(T) == typeof(char)) return (T) (object) value.ToChar();
			if (typeof(T) == typeof(byte)) return (T) (object) value.ToByte();
			if (typeof(T) == typeof(sbyte)) return (T) (object) value.ToSByte();
			if (typeof(T) == typeof(short)) return (T) (object) value.ToInt16();
			if (typeof(T) == typeof(ushort)) return (T) (object) value.ToUInt16();
			if (typeof(T) == typeof(int)) return (T) (object) value.ToInt32();
			if (typeof(T) == typeof(uint)) return (T) (object) value.ToUInt32();
			if (typeof(T) == typeof(long)) return (T) (object) value.ToInt64();
			if (typeof(T) == typeof(ulong)) return (T) (object) value.ToUInt64();
			if (typeof(T) == typeof(float)) return (T) (object) value.ToSingle();
			if (typeof(T) == typeof(double)) return (T) (object) value.ToDouble();
			if (typeof(T) == typeof(decimal)) return (T) (object) value.ToDecimal();
			if (typeof(T) == typeof(Guid)) return (T) (object) value.ToGuid();
			if (typeof(T) == typeof(TimeSpan)) return (T) (object) value.ToTimeSpan();
			if (typeof(T) == typeof(DateTime)) return (T) (object) value.ToDateTime();
			if (typeof(T) == typeof(DateTimeOffset)) return (T) (object) value.ToDateTimeOffset();
			if (typeof(T) == typeof(NodaTime.Instant)) return (T) (object) value.ToInstant();
			if (typeof(T) == typeof(NodaTime.Duration)) return (T) (object) value.ToDuration();

			//note: value peut être un JsonNull, donc on doit invoquer les ...OrDefault() !
			if (typeof(T) == typeof(bool?)) return (T) (object) value.ToBooleanOrDefault()!;
			if (typeof(T) == typeof(char?)) return (T) (object) value.ToCharOrDefault()!;
			if (typeof(T) == typeof(byte?)) return (T) (object) value.ToByteOrDefault()!;
			if (typeof(T) == typeof(sbyte?)) return (T) (object) value.ToSByteOrDefault()!;
			if (typeof(T) == typeof(short?)) return (T) (object) value.ToInt16OrDefault()!;
			if (typeof(T) == typeof(ushort?)) return (T) (object) value.ToUInt16OrDefault()!;
			if (typeof(T) == typeof(int?)) return (T) (object) value.ToInt32OrDefault()!;
			if (typeof(T) == typeof(uint?)) return (T) (object) value.ToUInt32OrDefault()!;
			if (typeof(T) == typeof(long?)) return (T) (object) value.ToInt64OrDefault()!;
			if (typeof(T) == typeof(ulong?)) return (T) (object) value.ToUInt64OrDefault()!;
			if (typeof(T) == typeof(float?)) return (T) (object) value.ToSingleOrDefault()!;
			if (typeof(T) == typeof(double?)) return (T) (object) value.ToDoubleOrDefault()!;
			if (typeof(T) == typeof(decimal?)) return (T) (object) value.ToDecimalOrDefault()!;
			if (typeof(T) == typeof(Guid?)) return (T) (object) value.ToGuidOrDefault()!;
			if (typeof(T) == typeof(TimeSpan?)) return (T) (object) value.ToTimeSpanOrDefault()!;
			if (typeof(T) == typeof(DateTime?)) return (T) (object) value.ToDateTimeOrDefault()!;
			if (typeof(T) == typeof(DateTimeOffset?)) return (T) (object) value.ToDateTimeOffsetOrDefault()!;
			if (typeof(T) == typeof(NodaTime.Instant?)) return (T) (object) value.ToInstantOrDefault()!;
			if (typeof(T) == typeof(NodaTime.Duration?)) return (T) (object) value.ToDurationOrDefault()!;
#endif

			#endregion </JIT_HACK>

			return (T?) value.Bind(typeof(T), resolver);
		}

		/// <summary>Bind cette valeur JSON en une instance d'un type CLR, avec une valeur par défaut en cas de null</summary>
		/// <remarks>Pour les types simples (int, string, guid, ....) préférez utiliser les ToInt32(), ToGuid(), etc...</remarks>
		[Pure, ContractAnnotation("defaultValue:notnull => notnull")]
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		[Obsolete("Use '??' operator instead")]
		public static T? As<T>(this JsonValue? value, T? defaultValue, ICrystalJsonTypeResolver? customResolver = null)
		{
			if (value?.IsNull != false) return defaultValue;

			#region <JIT_HACK>

			// En mode RELEASE, le JIT reconnait les patterns "if (typeof(T) == typeof(VALUETYPE)) { ... }" dans une méthode générique Foo<T> quand T est un ValueType,
			// et les remplace par des "if (true) { ...}" ce qui permet d'éliminer le reste du code (très efficace si le if contient un return!)
			// Egalement, le JIT optimise le "(VALUE_TYPE)(object)value" si T == VALUE_TYPE pour éviter le boxing inutile (le cast intermédiaire en object est pour faire taire le compilateur)
			// => pour le vérifier, il faut inspecter l'asm généré par le JIT au runtime (en mode release, en dehors du debugger, etc...) ce qui n'est pas facile...
			// => vérifié avec .NET 4.6.1 + RyuJIT x64, la méthode FromValue<int> est directement inlinée en l'appel à JsonNumber.Return(...) !

#if !DEBUG // trop lent en debug !

			//note: si value est un JsonNull, toutes les versions de ToVALUETYPE() retourne le type attendu!

			if (typeof (T) == typeof (bool)) return (T) (object) value.ToBoolean();
			if (typeof (T) == typeof (char)) return (T) (object) value.ToChar();
			if (typeof (T) == typeof (byte)) return (T) (object) value.ToByte();
			if (typeof (T) == typeof (sbyte)) return (T) (object) value.ToSByte();
			if (typeof (T) == typeof (short)) return (T) (object) value.ToInt16();
			if (typeof (T) == typeof (ushort)) return (T) (object) value.ToUInt16();
			if (typeof (T) == typeof (int)) return (T) (object) value.ToInt32();
			if (typeof (T) == typeof (uint)) return (T) (object) value.ToUInt32();
			if (typeof (T) == typeof (long)) return (T) (object) value.ToInt64();
			if (typeof (T) == typeof (ulong)) return (T) (object) value.ToUInt64();
			if (typeof (T) == typeof (float)) return (T) (object) value.ToSingle();
			if (typeof (T) == typeof (double)) return (T) (object) value.ToDouble();
			if (typeof (T) == typeof (decimal)) return (T) (object) value.ToDecimal();
			if (typeof (T) == typeof (Guid)) return (T) (object) value.ToGuid();
			if (typeof (T) == typeof (TimeSpan)) return (T) (object) value.ToTimeSpan();
			if (typeof (T) == typeof (DateTime)) return (T) (object) value.ToDateTime();
			if (typeof (T) == typeof (DateTimeOffset)) return (T) (object) value.ToDateTimeOffset();
			if (typeof (T) == typeof (NodaTime.Instant)) return (T) (object) value.ToInstant();
			if (typeof (T) == typeof (NodaTime.Duration)) return (T) (object) value.ToDuration();

			//note: on a déja testé le null plus haut, donc pas la peine de rappeler les overload "..OrDefault"!
			if (typeof (T) == typeof (bool?)) return (T) (object) value.ToBoolean();
			if (typeof (T) == typeof (char?)) return (T) (object) value.ToChar();
			if (typeof (T) == typeof (byte?)) return (T) (object) value.ToByte();
			if (typeof (T) == typeof (sbyte?)) return (T) (object) value.ToSByte();
			if (typeof (T) == typeof (short?)) return (T) (object) value.ToInt16();
			if (typeof (T) == typeof (ushort?)) return (T) (object) value.ToUInt16();
			if (typeof (T) == typeof (int?)) return (T) (object) value.ToInt32();
			if (typeof (T) == typeof (uint?)) return (T) (object) value.ToUInt32();
			if (typeof (T) == typeof (long?)) return (T) (object) value.ToInt64();
			if (typeof (T) == typeof (ulong?)) return (T) (object) value.ToUInt64();
			if (typeof (T) == typeof (float?)) return (T) (object) value.ToSingle();
			if (typeof (T) == typeof (double?)) return (T) (object) value.ToDouble();
			if (typeof (T) == typeof (decimal?)) return (T) (object) value.ToDecimal();
			if (typeof (T) == typeof (Guid?)) return (T) (object) value.ToGuid();
			if (typeof (T) == typeof (TimeSpan?)) return (T) (object) value.ToTimeSpan();
			if (typeof (T) == typeof (DateTime?)) return (T) (object) value.ToDateTime();
			if (typeof (T) == typeof (DateTimeOffset?)) return (T) (object) value.ToDateTimeOffset();
			if (typeof (T) == typeof (NodaTime.Instant?)) return (T) (object) value.ToInstant();
			if (typeof (T) == typeof (NodaTime.Duration?)) return (T) (object) value.ToDuration();
#endif

			#endregion </JIT_HACK>

			var o = value.Bind(typeof (T), customResolver);
			return o != null ? (T) o : defaultValue;
		}

		/// <summary>Retourne une exception indiquant qu'une valeur JSON était requise</summary>
		/// <exception cref="InvalidOperationException">Toujours</exception>
		[ContractAnnotation("=> halt"), MethodImpl(MethodImplOptions.NoInlining)]
		internal static T FailRequiredValueIsNullOrMissing<T>()
		{
			throw new InvalidOperationException($"Required JSON value or type {typeof(T).GetFriendlyName()} was null or missing");
		}

		/// <summary>Retourne soit le default(T) correspondant, ou une exception si required == true</summary>
		/// <remarks>Si T est JsonValue ou JsonNull, alors retourne JsonNull.Null à la place</remarks>
		[Pure]
		internal static T? DefaultOrRequired<T>(bool required)
		{
			return required ? FailRequiredValueIsNullOrMissing<T>() : JsonNull.Default<T>();
		}

		#endregion

		#region OrDefault...

		// pour simplifier les conversion avec valeur par défaut
		// le but est de n'allouer la JsonValue "missing" que si besoin.
		// pour des raisons de perfs, on a des version typées, et on reserve OrDefault<T>(...) pour les clas les plus complexes

		/// <summary>Retourne une valeur par défaut si la valeur JSON est null ou manquante</summary>
		/// <param name="value">Valeur JSON à valider</param>
		/// <param name="missingValue">Valeur retournée si <paramref name="value"/> est null ou manquante</param>
		[Pure, ContractAnnotation("missingValue:notnull => notnull")]
		public static JsonValue? OrDefault(this JsonValue? value, JsonValue missingValue)
		{
			return value.IsNullOrMissing() ? missingValue : value;
		}

		/// <summary>Retourne une valeur par défaut si la valeur JSON est null ou manquante</summary>
		/// <param name="value">Valeur JSON à valider</param>
		/// <param name="factory">Lambda appelée pour générer la valeur valeur retournée si <paramref name="value"/> est null ou manquante.</param>
		/// <remarks>Si <paramref name="factory"/> return null, la valeur retournée sera <see cref="JsonNull.Null"/></remarks>
		public static JsonValue OrDefault(this JsonValue? value, Func<JsonValue> factory)
		{
			return value.IsNullOrMissing() ? (factory() ?? JsonNull.Null) : value!;
		}

		/// <summary>Retourne une valeur par défaut si la valeur JSON est null ou manquante</summary>
		/// <param name="value">Valeur JSON à valider</param>
		/// <param name="factory">Lambda appelée pour générer la valeur valeur retournée si <paramref name="value"/> est null ou manquante.</param>
		/// <param name="arg">Argument passé à <paramref name="factory"/></param>
		/// <remarks>Si <paramref name="factory"/> return null, la valeur retournée sera <see cref="JsonNull.Null"/></remarks>
		public static JsonValue OrDefault<TArg>(this JsonValue? value, Func<TArg, JsonValue> factory, TArg arg)
		{
			return value.IsNullOrMissing() ? (factory(arg) ?? JsonNull.Null) : value!;
		}

		/// <summary>Retourne une valeur par défaut si la valeur JSON est null ou manquante</summary>
		/// <param name="value">Valeur JSON à valider</param>
		/// <param name="missingValue">Valeur retournée si <paramref name="value"/> est null ou manquante</param>
		[Pure]
		public static JsonValue OrDefault(this JsonValue? value, string missingValue)
		{
			return value.IsNullOrMissing() ? JsonString.Return(missingValue) : value!;
		}

		/// <summary>Retourne une valeur par défaut si la valeur JSON est null ou manquante</summary>
		/// <param name="value">Valeur JSON à valider</param>
		/// <param name="missingValue">Valeur retournée si <paramref name="value"/> est null ou manquante</param>
		[Pure]
		public static JsonValue OrDefault(this JsonValue? value, bool missingValue)
		{
			return value.IsNullOrMissing() ? JsonBoolean.Return(missingValue) : value!;
		}

		/// <summary>Retourne une valeur par défaut si la valeur JSON est null ou manquante</summary>
		/// <param name="value">Valeur JSON à valider</param>
		/// <param name="missingValue">Valeur retournée si <paramref name="value"/> est null ou manquante</param>
		[Pure]
		public static JsonValue OrDefault(this JsonValue? value, int missingValue)
		{
			return value.IsNullOrMissing() ? JsonNumber.Return(missingValue) : value!;
		}

		/// <summary>Retourne une valeur par défaut si la valeur JSON est null ou manquante</summary>
		/// <param name="value">Valeur JSON à valider</param>
		/// <param name="missingValue">Valeur retournée si <paramref name="value"/> est null ou manquante</param>
		[Pure]
		public static JsonValue OrDefault(this JsonValue? value, long missingValue)
		{
			return value.IsNullOrMissing() ? JsonNumber.Return(missingValue) : value!;
		}

		/// <summary>Retourne une valeur par défaut si la valeur JSON est null ou manquante</summary>
		/// <param name="value">Valeur JSON à valider</param>
		/// <param name="missingValue">Valeur retournée si <paramref name="value"/> est null ou manquante</param>
		[Pure]
		public static JsonValue OrDefault(this JsonValue? value, double missingValue)
		{
			return value.IsNullOrMissing() ? JsonNumber.Return(missingValue) : value!;
		}

		/// <summary>Retourne une valeur par défaut si la valeur JSON est null ou manquante</summary>
		/// <param name="value">Valeur JSON à valider</param>
		/// <param name="missingValue">Valeur retournée si <paramref name="value"/> est null ou manquante</param>
		[Pure]
		public static JsonValue OrDefault(this JsonValue? value, float missingValue)
		{
			return value.IsNullOrMissing() ? JsonNumber.Return(missingValue) : value!;
		}

		/// <summary>Retourne une valeur par défaut si la valeur JSON est null ou manquante</summary>
		/// <param name="value">Valeur JSON à valider</param>
		/// <param name="missingValue">Valeur retournée si <paramref name="value"/> est null ou manquante</param>
		[Pure]
		public static JsonValue OrDefault(this JsonValue? value, Guid missingValue)
		{
			return value.IsNullOrMissing() ? JsonString.Return(missingValue) : value!;
		}

		/// <summary>Retourne une valeur par défaut si la valeur JSON est null ou manquante</summary>
		/// <param name="value">Valeur JSON à valider</param>
		/// <param name="missingValue">Valeur retournée si <paramref name="value"/> est null ou manquante</param>
		[Pure]
		public static JsonValue OrDefault(this JsonValue? value, TimeSpan missingValue)
		{
			return value.IsNullOrMissing() ? JsonNumber.Return(missingValue) : value!;
		}

		/// <summary>Retourne une valeur par défaut si la valeur JSON est null ou manquante</summary>
		/// <param name="value">Valeur JSON à valider</param>
		/// <param name="missingValue">Valeur retournée si <paramref name="value"/> est null ou manquante</param>
		[Pure]
		public static JsonValue OrDefault(this JsonValue? value, DateTime missingValue)
		{
			return value.IsNullOrMissing() ? JsonDateTime.Return(missingValue) : value!;
		}

		/// <summary>Retourne une valeur par défaut si la valeur JSON est null ou manquante</summary>
		/// <param name="value">Valeur JSON à valider</param>
		/// <param name="missingValue">Valeur retournée si <paramref name="value"/> est null ou manquante</param>
		[Pure]
		public static JsonValue OrDefault(this JsonValue? value, DateTimeOffset missingValue)
		{
			return value.IsNullOrMissing() ? JsonDateTime.Return(missingValue) : value!;
		}

		/// <summary>Retourne une valeur par défaut si la valeur JSON est null ou manquante</summary>
		/// <param name="value">Valeur JSON à valider</param>
		/// <param name="missingValue">Valeur retournée si <paramref name="value"/> est null ou manquante</param>
		[Pure]
		public static JsonValue OrDefault<TValue>(this JsonValue? value, TValue? missingValue)
		{
			return value.IsNullOrMissing() ? JsonValue.FromValue<TValue>(missingValue) : value!;
		}

		/// <summary>Retourne une valeur par défaut si la valeur JSON est null ou manquante</summary>
		/// <param name="value">Valeur JSON à valider</param>
		/// <param name="factory">Lambda appelée pour générer la valeur retournée, si <paramref name="value"/> est null ou manquante</param>
		/// <remarks>Si <paramref name="factory"/> return null, la valeur retournée sera <see cref="JsonNull.Null"/></remarks>
		public static JsonValue OrDefault<TValue>(this JsonValue? value, Func<TValue> factory)
		{
			return value.IsNullOrMissing() ? JsonValue.FromValue<TValue>(factory()) : value!;
		}

		/// <summary>Retourne une valeur par défaut si la valeur JSON est null ou manquante</summary>
		/// <param name="value">Valeur JSON à valider</param>
		/// <param name="factory">Lambda appelée pour générer la valeur retournée, si <paramref name="value"/> est null ou manquante</param>
		/// <param name="arg">Argument passé à <paramref name="factory"/></param>
		/// <remarks>Si <paramref name="factory"/> return null, la valeur retournée sera <see cref="JsonNull.Null"/></remarks>
		public static JsonValue OrDefault<TValue, TArg>(this JsonValue? value, Func<TArg, TValue> factory, TArg? arg)
		{
			return value.IsNullOrMissing() ? JsonValue.FromValue<TValue>(factory(arg)) : value!;
		}

		#endregion

		#region Object Helpers...

		// magic cast entre JsonValue et JsonObject
		// le but est de réduire les faux positifs de nullref avec des outils d'analyse statique de code (R#, ..)

		/// <summary>Vérifie que la valeur n'est pas vide, et qu'il s'agit bien d'un JsonObject.</summary>
		/// <param name="value">Valeur JSON qui doit être un object</param>
		/// <returns>Valeur castée en JsonObject si elle existe. Une exception si la valeur est null, missing, ou n'est pas une array.</returns>
		/// <exception cref="System.InvalidOperationException">Si <paramref name="value"/> est null, missing, ou n'est pas un object.</exception>
		[Pure]
		[Obsolete("Prefer using AsObject(required: true) instead", error: true)]
		public static JsonObject AsObject(this JsonValue? value)
		{
			if (value.IsNullOrMissing()) return FailObjectIsNullOrMissing();
			if (value!.Type != JsonType.Object) return FailValueIsNotAnObject(value); // => throws
			return (JsonObject) value;
		}

		/// <summary>Vérifie que la valeur n'est pas vide, et qu'il s'agit bien d'un JsonObject.</summary>
		/// <param name="value">Valeur JSON qui doit être un object</param>
		/// <param name="required">Si true, une exception sera générée si l'objet est null.</param>
		/// <returns>Valeur castée en JsonObject si elle existe. Une exception si la valeur est null, missing, ou n'est pas une array.</returns>
		/// <exception cref="System.InvalidOperationException">Si <paramref name="value"/> est null, missing, ou n'est pas un object.</exception>
		[Pure, ContractAnnotation("required:true => notnull")]
		public static JsonObject? AsObject(this JsonValue? value, bool required)
		{
			if (value.IsNullOrMissing())
			{
				if (required) return FailObjectIsNullOrMissing();
				return null;
			}
			if (value!.Type != JsonType.Object) return FailValueIsNotAnObject(value); // => throws
			return (JsonObject) value;
		}

		/// <summary>Retourne la valeur JSON sous forme d'object, ou null si elle est null ou manquante.</summary>
		/// <param name="value">Valeur JSON qui doit être soit un object, soit null/missing.</param>
		/// <returns>Valeur castée en JsonObject si elle existe, ou null si la valeur null ou missing. Une exception si la valeur est d'un type différent.</returns>
		/// <exception cref="System.InvalidOperationException">Si <paramref name="value"/> n'est ni null, ni un object.</exception>
		[Pure]
		[Obsolete("Prefer using AsObject(required: false) instead", error: true)]
		public static JsonObject? AsObjectOrDefault(this JsonValue? value)
		{
			if (value.IsNullOrMissing()) return null;
			if (value!.Type != JsonType.Object) return FailValueIsNotAnObject(value); // => throws
			return (JsonObject) value;
		}

		[ContractAnnotation("=> halt"), MethodImpl(MethodImplOptions.NoInlining)]
		internal static JsonObject FailObjectIsNullOrMissing()
		{
			throw new InvalidOperationException("Required JSON object was null or missing.");
		}

		[ContractAnnotation("=> halt"), MethodImpl(MethodImplOptions.NoInlining)]
		internal static JsonObject FailValueIsNotAnObject(JsonValue value)
		{
			throw CrystalJson.Errors.Parsing_CannotCastToJsonObject(value.Type);
		}

		[Pure]
		public static JsonObject ToJsonObject([InstantHandle] this IEnumerable<KeyValuePair<string, JsonValue>> items, IEqualityComparer<string>? comparer = null)
		{
			Contract.NotNull(items);

			return new JsonObject(items, comparer);
		}

		[Pure]
		public static JsonObject ToJsonObject<TValue>([InstantHandle] this IEnumerable<KeyValuePair<string, TValue>> items, IEqualityComparer<string>? comparer = null)
		{
			Contract.NotNull(items);

			// essayes de garder le même comparer que la source...
			comparer ??= JsonObject.ExtractKeyComparer(items) ?? StringComparer.Ordinal;

			var map = new Dictionary<string, JsonValue>((items as ICollection<KeyValuePair<string, TValue>>)?.Count ?? 0, comparer);
			var context = new CrystalJsonDomWriter.VisitingContext();
			foreach (var item in items)
			{
				map.Add(item.Key, JsonValue.FromValue(CrystalJsonDomWriter.Default, ref context, item.Value));
			}
			return new JsonObject(map, owner: true);
		}

		[Pure]
		public static JsonObject ToJsonObject<TValue>([InstantHandle] this IEnumerable<KeyValuePair<string, TValue>> items, [InstantHandle] Func<TValue, JsonValue> valueSelector, IEqualityComparer<string>? comparer = null)
		{
			Contract.NotNull(items);
			Contract.NotNull(valueSelector);

			// essayes de garder le même comparer que la source...
			comparer ??= JsonObject.ExtractKeyComparer(items) ?? StringComparer.Ordinal;

			var map = new Dictionary<string, JsonValue>((items as ICollection<KeyValuePair<string, TValue>>)?.Count ?? 0, comparer);
			foreach (var item in items)
			{
				map.Add(item.Key, valueSelector(item.Value) ?? JsonNull.Null);
			}
			return new JsonObject(map, owner: true);
		}

		[Pure]
		public static JsonObject ToJsonObject<TElement>([InstantHandle] this IEnumerable<TElement> source, [InstantHandle] Func<TElement, string> keySelector, [InstantHandle] Func<TElement, JsonValue> valueSelector, IEqualityComparer<string>? comparer = null)
		{
			Contract.NotNull(source);
			Contract.NotNull(keySelector);
			Contract.NotNull(valueSelector);

			var coll = source as ICollection<TElement>;

			var map = new Dictionary<string, JsonValue>(coll?.Count ?? 0, comparer ?? StringComparer.Ordinal);
			foreach (var item in source)
			{
				map.Add(keySelector(item), valueSelector(item) ?? JsonNull.Null);
			}
			return new JsonObject(map, owner: true);
		}

		[Pure]
		public static JsonObject ToJsonObject<TElement, TValue>([InstantHandle] this IEnumerable<TElement> source, [InstantHandle] Func<TElement, string> keySelector, [InstantHandle] Func<TElement, TValue> valueSelector, IEqualityComparer<string>? comparer = null)
		{
			Contract.NotNull(source);
			Contract.NotNull(keySelector);
			Contract.NotNull(valueSelector);

			var coll = source as ICollection<TElement>;

			var map = new Dictionary<string, JsonValue>(coll?.Count ?? 0, comparer ?? StringComparer.Ordinal);
			var context = new CrystalJsonDomWriter.VisitingContext();
			foreach (var item in source)
			{
				map.Add(keySelector(item), JsonValue.FromValue<TValue>(CrystalJsonDomWriter.Default, ref context, valueSelector(item)));
			}
			return new JsonObject(map, owner: true);
		}

		#endregion

		#region AsNumber...

		// magic cast entre JsonValue et JsonNumber
		// le but est de réduire les faux positifs de nullref avec des outils d'analyse statique de code (R#, ..)

		/// <summary>Vérifie que la valeur n'est pas vide, et qu'il s'agit bien d'une JsonNumber.</summary>
		/// <param name="value">Valeur JSON qui doit être un number</param>
		/// <returns>Valeur castée en JsonNumber si elle existe. Une exception si la valeur est null, missing, ou n'est pas un number.</returns>
		/// <exception cref="System.InvalidOperationException">Si <paramref name="value"/> est null, missing, ou n'est pas un number.</exception>
		[Pure, ContractAnnotation("null => halt")]
		public static JsonNumber AsNumber(this JsonValue? value)
		{
			if (value == null || value.Type != JsonType.Number) return FailValueIsNotANumber(value);
			return (JsonNumber)value;
		}

		/// <summary>Retourne la valeur JSON sous forme d'un number, ou null si elle est null ou manquante.</summary>
		/// <param name="value">Valeur JSON qui doit être soit un number, soit null/missing.</param>
		/// <returns>Valeur castée en JsonNumber si elle existe, ou null si la valeur null ou missing. Une exception si la valeur est d'un type différent.</returns>
		/// <exception cref="System.InvalidOperationException">Si <paramref name="value"/> n'est ni null, ni un number.</exception>
		[Pure]
		public static JsonNumber? AsNumberOrDefault(this JsonValue? value)
		{
			if (value.IsNullOrMissing()) return null;
			if (value.Type != JsonType.Number) return FailValueIsNotANumber(value);
			return (JsonNumber)value;
		}

		[ContractAnnotation("=> halt")]
		private static JsonNumber FailValueIsNotANumber(JsonValue value)
		{
			if (value.IsNullOrMissing()) ThrowHelper.ThrowInvalidOperationException("Expected JSON number was either null or missing.");
			throw CrystalJson.Errors.Parsing_CannotCastToJsonNumber(value.Type);
		}

		#endregion

		#region AsString...

		// magic cast entre JsonValue et JsonNumber
		// le but est de réduire les faux positifs de nullref avec des outils d'analyse statique de code (R#, ..)

		/// <summary>Vérifie que la valeur n'est pas vide, et qu'il s'agit bien d'une JsonNumber.</summary>
		/// <param name="value">Valeur JSON qui doit être un number</param>
		/// <returns>Valeur castée en JsonNumber si elle existe. Une exception si la valeur est null, missing, ou n'est pas un number.</returns>
		/// <exception cref="System.InvalidOperationException">Si <paramref name="value"/> est null, missing, ou n'est pas un number.</exception>
		[Pure, ContractAnnotation("null => halt")]
		public static JsonString AsString(this JsonValue? value)
		{
			if (value == null || value.Type != JsonType.String) return FailValueIsNotAString(value);
			return (JsonString)value;
		}

		/// <summary>Retourne la valeur JSON sous forme d'un number, ou null si elle est null ou manquante.</summary>
		/// <param name="value">Valeur JSON qui doit être soit un number, soit null/missing.</param>
		/// <returns>Valeur castée en JsonNumber si elle existe, ou null si la valeur null ou missing. Une exception si la valeur est d'un type différent.</returns>
		/// <exception cref="System.InvalidOperationException">Si <paramref name="value"/> n'est ni null, ni un number.</exception>
		[Pure]
		public static JsonString? AsStringOrDefault(this JsonValue? value)
		{
			if (value.IsNullOrMissing()) return null;
			if (value.Type != JsonType.String) return FailValueIsNotAString(value);
			return (JsonString)value;
		}

		[ContractAnnotation("=> halt")]
		private static JsonString FailValueIsNotAString(JsonValue? value)
		{
			if (value.IsNullOrMissing()) ThrowHelper.ThrowInvalidOperationException("Expected JSON string was either null or missing.");
			throw CrystalJson.Errors.Parsing_CannotCastToJsonString(value.Type);
		}

		#endregion

	}

}
