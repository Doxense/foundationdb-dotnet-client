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

namespace Doxense.Serialization.Json
{
	using System;
	using System.Collections.Generic;
	using System.Diagnostics.CodeAnalysis;
	using System.Linq;
	using System.Runtime.CompilerServices;
	using Doxense.Diagnostics.Contracts;
	using JetBrains.Annotations;

	[PublicAPI]
	public static class JsonValueExtensions
	{

		/// <summary>Test si une valeur JSON est null, ou équivalente à null</summary>
		/// <param name="value">Valeur JSON</param>
		/// <returns>True si <paramref name="value"/> est null, ou une instance de type <see cref="JsonNull"/></returns>
		[Pure, ContractAnnotation("null=>true"), MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool IsNullOrMissing([NotNullWhen(false)] this JsonValue? value)
		{
			return value is (null or JsonNull);
		}

		/// <summary>Test si une valeur JSON est l'équivalent logique de 'missing'</summary>
		/// <param name="value">Valeur JSON</param>
		/// <returns>True si <paramref name="value"/> est null, ou égal à <see cref="JsonNull.Missing"/>.</returns>
		/// <remarks><see cref="JsonNull.Null"/> n'est pas considéré comme manquant (c'est un null explicite)</remarks>
		[Pure, ContractAnnotation("null=>true"), MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool IsMissing([NotNullWhen(false)] this JsonValue? value)
		{
			//note: JsonNull.Error est un singleton, donc on peut le comparer par référence!
			return ReferenceEquals(value, JsonNull.Missing);
		}


		/// <summary>Test si une valeur JSON est manquant pour cause d'une erreur de parsing</summary>
		/// <param name="value">Valeur JSON</param>
		/// <returns>True si <paramref name="value"/> est null, ou égal à <see cref="JsonNull.Error"/>.</returns>
		/// <remarks><see cref="JsonNull.Null"/> n'est pas considéré comme manquant (c'est un null explicite)</remarks>
		[Pure, ContractAnnotation("null=>true")]
		public static bool IsError([NotNullWhen(false)] this JsonValue? value)
		{
			//note: JsonNull.Error est un singleton, donc on peut le comparer par référence!
			return ReferenceEquals(value, JsonNull.Error);
		}

		/// <summary>Vérifie qu'une valeur JSON est bien présente</summary>
		/// <param name="value">Valeur JSON qui ne doit pas être null ou manquante</param>
		/// <returns>La valeur JSON si elle existe. Ou une exception si elle est null ou manquante</returns>
		[ ContractAnnotation("null => halt"), MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static JsonValue Required(this JsonValue? value) => value is not (null or JsonNull) ? value : FailValueIsNullOrMissing();

		/// <summary>Vérifie qu'une valeur JSON est bien présente dans une array</summary>
		/// <param name="value">Valeur JSON qui ne doit pas être null ou manquante</param>
		/// <param name="index">Index dans l'array qui doit être présent</param>
		/// <returns>La valeur JSON si elle existe. Ou une exception si elle est null ou manquante</returns>
		[ContractAnnotation("value:null => halt"), MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static JsonValue RequiredIndex(this JsonValue? value, int index) => value is not (null or JsonNull) ? value : FailIndexIsNullOrMissing(index);

		/// <summary>Vérifie qu'une valeur JSON est bien présente</summary>
		/// <param name="value">Valeur JSON qui ne doit pas être null ou manquante</param>
		/// <param name="field">Nom du champ qui doit être présent</param>
		/// <returns>La valeur JSON si elle existe. Ou une exception si elle est null ou manquante</returns>
		[ContractAnnotation("value:null => halt"), MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static JsonValue RequiredField(this JsonValue? value, string field) => value is not (null or JsonNull) ? value : FailFieldIsNullOrMissing(field);

		/// <summary>Vérifie qu'une valeur JSON est bien présente</summary>
		/// <param name="value">Valeur JSON qui ne doit pas être null ou manquante</param>
		/// <param name="path">Chemin vers le champ qui doit être présent</param>
		/// <returns>La valeur JSON si elle existe. Ou une exception si elle est null ou manquante</returns>
		[ContractAnnotation("value:null => halt"), MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static JsonValue RequiredPath(this JsonValue? value, string path) => value is not (null or JsonNull) ? value : FailPathIsNullOrMissing(path);

		/// <summary>Vérifie qu'une valeur JSON est bien présente</summary>
		/// <param name="value">Valeur JSON qui ne doit pas être null ou manquante</param>
		/// <returns>La valeur JSON si elle existe. Ou une exception si elle est null ou manquante</returns>
		[ContractAnnotation("null => halt")]
		public static JsonArray Required(this JsonArray? value) => value ?? FailArrayIsNullOrMissing();

		/// <summary>Vérifie qu'une valeur JSON est bien présente</summary>
		/// <param name="value">Valeur JSON qui ne doit pas être null ou manquante</param>
		/// <returns>La valeur JSON si elle existe. Ou une exception si elle est null ou manquante</returns>
		[ContractAnnotation("null => halt")]
		public static JsonObject Required(this JsonObject? value) => value ?? FailObjectIsNullOrMissing();

		[DoesNotReturn, MethodImpl(MethodImplOptions.NoInlining)]
		internal static JsonValue FailValueIsNullOrMissing() => throw new InvalidOperationException("Required JSON value was null or missing.");

		[DoesNotReturn, MethodImpl(MethodImplOptions.NoInlining)]
		internal static JsonArray FailArrayIsNullOrMissing() => throw new InvalidOperationException("Required JSON array was null or missing.");

		[DoesNotReturn, MethodImpl(MethodImplOptions.NoInlining)]
		internal static JsonValue FailIndexIsNullOrMissing(int index) => throw new InvalidOperationException($"Required JSON field at index {index} was null or missing.");

		[DoesNotReturn, MethodImpl(MethodImplOptions.NoInlining)]
		internal static JsonValue FailFieldIsNullOrMissing(string field) => throw new InvalidOperationException($"Required JSON field '{field}' was null or missing.");

		[DoesNotReturn, MethodImpl(MethodImplOptions.NoInlining)]
		internal static JsonValue FailFieldIsEmpty(string field) => throw new InvalidOperationException($"Required JSON field '{field}' was empty.");

		[DoesNotReturn, MethodImpl(MethodImplOptions.NoInlining)]
		internal static JsonValue FailPathIsNullOrMissing(string path) => throw new InvalidOperationException($"Required JSON path '{path}' was null or missing.");

		#region ToStuff(...)

		/// <summary>Sérialise cette valeur JSON en texte le plus compact possible (pour du stockage)</summary>
		/// <remarks>Note: si le JSON doit être envoyés en HTTP ou sauvé sur disque, préférer <see cref="ToJsonBuffer(JsonValue)"/> ou <see cref="ToJsonBytes(JsonValue)"/></remarks>
		[Pure]
		public static string ToJsonCompact(this JsonValue? value) => value?.ToJson(CrystalJsonSettings.JsonCompact) ?? JsonTokens.Null;

		/// <summary>Sérialise cette valeur JSON en texte au format indenté (pratique pour des logs ou en mode debug)</summary>
		/// <remarks>Note: si le JSON doit être envoyés en HTTP ou sauvé sur disque, préférer <see cref="ToJsonBuffer(JsonValue)"/> ou <see cref="ToJsonBytes(JsonValue)"/></remarks>
		[Pure]
		public static string ToJsonIndented(this JsonValue? value) => value?.ToJson(CrystalJsonSettings.JsonIndented) ?? JsonTokens.Null;

		/// <summary>Sérialise cette valeur JSON en un tableau de bytes</summary>
		/// <returns>Buffer contenant le texte JSON encodé en UTF-8</returns>
		/// <remarks>A n'utiliser que si l'appelant veut absolument un tableau. Pour de l'IO, préférer <see cref="ToJsonBuffer(JsonValue)"/> qui permet d'éviter une copie inutile en mémoire</remarks>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static byte[] ToJsonBytes(this JsonValue? value) => CrystalJson.ToBytes(value);

		/// <summary>Sérialise cette valeur JSON en un tableau de bytes</summary>
		/// <returns>Buffer contenant le texte JSON encodé en UTF-8</returns>
		/// <remarks>A n'utiliser que si l'appelant veut absolument un tableau. Pour de l'IO, préférer <see cref="ToJsonBuffer(JsonValue, CrystalJsonSettings)"/> qui permet d'éviter une copie inutile en mémoire</remarks>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static byte[] ToJsonBytes(this JsonValue? value, CrystalJsonSettings? settings) => CrystalJson.ToBytes(value, settings);

		/// <summary>Sérialise cette valeur JSON en un buffer de bytes</summary>
		/// <returns>Buffer contenant le texte JSON encodé en UTF-8</returns>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Slice ToJsonBuffer(this JsonValue? value) => CrystalJson.ToBuffer(value);

		/// <summary>Sérialise cette valeur JSON en un buffer de bytes</summary>
		/// <returns>Buffer contenant le texte JSON encodé en UTF-8</returns>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Slice ToJsonBuffer(this JsonValue? value, CrystalJsonSettings? settings) => CrystalJson.ToBuffer(value, settings);

		#endregion

		#region As<T>...

		/// <summary>Convert this value into a the specified CLR type.</summary>
		/// <typeparam name="T">Target CLR type</typeparam>
		/// <exception cref="JsonBindingException">If the value cannot be bound to the specified type.</exception>
		/// <remarks>If the value is <see langword="null"/> or "null-like", this will return the default <typeparam name="T"/> value (<see langword="0"/>, <see langword="false"/>, <see langword="null"/>, ...)</remarks>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static T? As<T>(this JsonValue? value)
		{
			if (value == null)
			{
				return default(T) == null ? JsonNull.Default<T>() : default;
			}

			#region <JIT_HACK>

			// En mode RELEASE, le JIT reconnaît les patterns "if (typeof(T) == typeof(VALUETYPE)) { ... }" dans une méthode générique Foo<T> quand T est un ValueType,
			// et les remplace par des "if (true) { ...}" ce qui permet d'éliminer le reste du code (très efficace si le if contient un return!)
			// Egalement, le JIT optimise le "(VALUE_TYPE)(object)value" si T == VALUE_TYPE pour éviter le boxing inutile (le cast intermédiaire en object est pour faire taire le compilateur)
			// => pour le vérifier, il faut inspecter l'asm généré par le JIT au runtime (en mode release, en dehors du debugger, etc...) ce qui n'est pas facile...
			// => vérifié avec .NET 4.6.1 + RyuJIT x64, la méthode FromValue<int> est directement inlinée en l'appel à JsonNumber.Return(...) !

#if !DEBUG
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
			if (typeof(T) == typeof(Uuid128)) return (T) (object) value.ToUuid128();
			if (typeof(T) == typeof(Uuid96)) return (T) (object) value.ToUuid96();
			if (typeof(T) == typeof(Uuid80)) return (T) (object) value.ToUuid80();
			if (typeof(T) == typeof(Uuid64)) return (T) (object) value.ToUuid64();
			if (typeof(T) == typeof(TimeSpan)) return (T) (object) value.ToTimeSpan();
			if (typeof(T) == typeof(DateTime)) return (T) (object) value.ToDateTime();
			if (typeof(T) == typeof(DateTimeOffset)) return (T) (object) value.ToDateTimeOffset();
			if (typeof(T) == typeof(NodaTime.Instant)) return (T) (object) value.ToInstant();
			if (typeof(T) == typeof(NodaTime.Duration)) return (T) (object) value.ToDuration();

			//note: value peut être un JsonNull, donc on doit invoquer les ...OrDefault() !
			if (typeof(T) == typeof(bool?)) return (T?) (object?) value.ToBooleanOrDefault();
			if (typeof(T) == typeof(char?)) return (T?) (object?) value.ToCharOrDefault();
			if (typeof(T) == typeof(byte?)) return (T?) (object?) value.ToByteOrDefault();
			if (typeof(T) == typeof(sbyte?)) return (T?) (object?) value.ToSByteOrDefault();
			if (typeof(T) == typeof(short?)) return (T?) (object?) value.ToInt16OrDefault();
			if (typeof(T) == typeof(ushort?)) return (T?) (object?) value.ToUInt16OrDefault();
			if (typeof(T) == typeof(int?)) return (T?) (object?) value.ToInt32OrDefault();
			if (typeof(T) == typeof(uint?)) return (T?) (object?) value.ToUInt32OrDefault();
			if (typeof(T) == typeof(long?)) return (T?) (object?) value.ToInt64OrDefault();
			if (typeof(T) == typeof(ulong?)) return (T?) (object?) value.ToUInt64OrDefault();
			if (typeof(T) == typeof(float?)) return (T?) (object?) value.ToSingleOrDefault();
			if (typeof(T) == typeof(double?)) return (T?) (object?) value.ToDoubleOrDefault();
			if (typeof(T) == typeof(decimal?)) return (T?) (object?) value.ToDecimalOrDefault();
			if (typeof(T) == typeof(Guid?)) return (T?) (object?) value.ToGuidOrDefault();
			if (typeof(T) == typeof(Uuid128?)) return (T?) (object?) value.ToUuid128OrDefault();
			if (typeof(T) == typeof(Uuid96?)) return (T?) (object?) value.ToUuid96OrDefault();
			if (typeof(T) == typeof(Uuid80?)) return (T?) (object?) value.ToUuid80OrDefault();
			if (typeof(T) == typeof(Uuid64?)) return (T?) (object?) value.ToUuid64OrDefault();
			if (typeof(T) == typeof(TimeSpan?)) return (T?) (object?) value.ToTimeSpanOrDefault();
			if (typeof(T) == typeof(DateTime?)) return (T?) (object?) value.ToDateTimeOrDefault();
			if (typeof(T) == typeof(DateTimeOffset?)) return (T?) (object?) value.ToDateTimeOffsetOrDefault();
			if (typeof(T) == typeof(NodaTime.Instant?)) return (T?) (object?) value.ToInstantOrDefault();
			if (typeof(T) == typeof(NodaTime.Duration?)) return (T?) (object?) value.ToDurationOrDefault();
#endif

			#endregion </JIT_HACK>

			return value.Bind<T>();
		}

		/// <summary>Convert this value into a the specified CLR type.</summary>
		/// <typeparam name="T">Target CLR type</typeparam>
		/// <exception cref="JsonBindingException">If the value cannot be bound to the specified type.</exception>
		/// <remarks>If the value is <see langword="null"/> or "null-like", this will return the default <typeparam name="T"/> value (<see langword="0"/>, <see langword="false"/>, <see langword="null"/>, ...)</remarks>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static T? As<T>(this JsonValue? value, ICrystalJsonTypeResolver? resolver)
		{
			if (value == null)
			{
				return default(T) == null ? JsonNull.Default<T>() : default;
			}

			#region <JIT_HACK>

			// En mode RELEASE, le JIT reconnaît les patterns "if (typeof(T) == typeof(VALUETYPE)) { ... }" dans une méthode générique Foo<T> quand T est un ValueType,
			// et les remplace par des "if (true) { ...}" ce qui permet d'éliminer le reste du code (très efficace si le if contient un return!)
			// Egalement, le JIT optimise le "(VALUE_TYPE)(object)value" si T == VALUE_TYPE pour éviter le boxing inutile (le cast intermédiaire en object est pour faire taire le compilateur)
			// => pour le vérifier, il faut inspecter l'asm généré par le JIT au runtime (en mode release, en dehors du debugger, etc...) ce qui n'est pas facile...
			// => vérifié avec .NET 4.6.1 + RyuJIT x64, la méthode FromValue<int> est directement inlinée en l'appel à JsonNumber.Return(...) !

#if !DEBUG

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
			if (typeof(T) == typeof(Uuid128)) return (T) (object) value.ToUuid128();
			if (typeof(T) == typeof(Uuid96)) return (T) (object) value.ToUuid96();
			if (typeof(T) == typeof(Uuid80)) return (T) (object) value.ToUuid80();
			if (typeof(T) == typeof(Uuid64)) return (T) (object) value.ToUuid64();
			if (typeof(T) == typeof(TimeSpan)) return (T) (object) value.ToTimeSpan();
			if (typeof(T) == typeof(DateTime)) return (T) (object) value.ToDateTime();
			if (typeof(T) == typeof(DateTimeOffset)) return (T) (object) value.ToDateTimeOffset();
			if (typeof(T) == typeof(NodaTime.Instant)) return (T) (object) value.ToInstant();
			if (typeof(T) == typeof(NodaTime.Duration)) return (T) (object) value.ToDuration();

			//note: value peut être un JsonNull, donc on doit invoquer les ...OrDefault() !
			if (typeof(T) == typeof(bool?)) return (T?) (object?) value.ToBooleanOrDefault();
			if (typeof(T) == typeof(char?)) return (T?) (object?) value.ToCharOrDefault();
			if (typeof(T) == typeof(byte?)) return (T?) (object?) value.ToByteOrDefault();
			if (typeof(T) == typeof(sbyte?)) return (T?) (object?) value.ToSByteOrDefault();
			if (typeof(T) == typeof(short?)) return (T?) (object?) value.ToInt16OrDefault();
			if (typeof(T) == typeof(ushort?)) return (T?) (object?) value.ToUInt16OrDefault();
			if (typeof(T) == typeof(int?)) return (T?) (object?) value.ToInt32OrDefault();
			if (typeof(T) == typeof(uint?)) return (T?) (object?) value.ToUInt32OrDefault();
			if (typeof(T) == typeof(long?)) return (T?) (object?) value.ToInt64OrDefault();
			if (typeof(T) == typeof(ulong?)) return (T?) (object?) value.ToUInt64OrDefault();
			if (typeof(T) == typeof(float?)) return (T?) (object?) value.ToSingleOrDefault();
			if (typeof(T) == typeof(double?)) return (T?) (object?) value.ToDoubleOrDefault();
			if (typeof(T) == typeof(decimal?)) return (T?) (object?) value.ToDecimalOrDefault();
			if (typeof(T) == typeof(Guid?)) return (T?) (object?) value.ToGuidOrDefault();
			if (typeof(T) == typeof(Uuid128?)) return (T?) (object?) value.ToUuid128OrDefault();
			if (typeof(T) == typeof(Uuid96?)) return (T?) (object?) value.ToUuid96OrDefault();
			if (typeof(T) == typeof(Uuid80?)) return (T?) (object?) value.ToUuid80OrDefault();
			if (typeof(T) == typeof(Uuid64?)) return (T?) (object?) value.ToUuid64OrDefault();
			if (typeof(T) == typeof(TimeSpan?)) return (T?) (object?) value.ToTimeSpanOrDefault();
			if (typeof(T) == typeof(DateTime?)) return (T?) (object?) value.ToDateTimeOrDefault();
			if (typeof(T) == typeof(DateTimeOffset?)) return (T?) (object?) value.ToDateTimeOffsetOrDefault();
			if (typeof(T) == typeof(NodaTime.Instant?)) return (T?) (object?) value.ToInstantOrDefault();
			if (typeof(T) == typeof(NodaTime.Duration?)) return (T?) (object?) value.ToDurationOrDefault();
#endif

			#endregion </JIT_HACK>

			return value.Bind<T>(resolver);
		}

		/// <summary>Convert this value into a the specified CLR type.</summary>
		/// <typeparam name="T">Target CLR type</typeparam>
		/// <exception cref="JsonBindingException">If the value cannot be bound to the specified type.</exception>
		/// <remarks>If the value is <see langword="null"/> or "null-like", this will return the default <typeparam name="T"/> value (<see langword="0"/>, <see langword="false"/>, <see langword="null"/>, ...) if <paramref name="required"/> is <see langword="false"/>, or an exception if it is <see langword="true"/>.</remarks>
		[Pure, ContractAnnotation("required:true => notnull")]
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static T? As<T>(this JsonValue? value, bool required, ICrystalJsonTypeResolver? resolver = null)
		{
			if (value == null)
			{
				return required ? FailRequiredValueIsNullOrMissing<T>() : default(T) == null ? JsonNull.Default<T>() : default;
			}
			if (required && value.IsNull)
			{
				return FailRequiredValueIsNullOrMissing<T>();
			}

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
			if (typeof(T) == typeof(Uuid128)) return (T) (object) value.ToUuid128();
			if (typeof(T) == typeof(Uuid96)) return (T) (object) value.ToUuid96();
			if (typeof(T) == typeof(Uuid80)) return (T) (object) value.ToUuid80();
			if (typeof(T) == typeof(Uuid64)) return (T) (object) value.ToUuid64();
			if (typeof(T) == typeof(TimeSpan)) return (T) (object) value.ToTimeSpan();
			if (typeof(T) == typeof(DateTime)) return (T) (object) value.ToDateTime();
			if (typeof(T) == typeof(DateTimeOffset)) return (T) (object) value.ToDateTimeOffset();
			if (typeof(T) == typeof(NodaTime.Instant)) return (T) (object) value.ToInstant();
			if (typeof(T) == typeof(NodaTime.Duration)) return (T) (object) value.ToDuration();

			//note: value peut être un JsonNull, donc on doit invoquer les ...OrDefault() !
			if (typeof(T) == typeof(bool?)) return (T?) (object?) value.ToBooleanOrDefault();
			if (typeof(T) == typeof(char?)) return (T?) (object?) value.ToCharOrDefault();
			if (typeof(T) == typeof(byte?)) return (T?) (object?) value.ToByteOrDefault();
			if (typeof(T) == typeof(sbyte?)) return (T?) (object?) value.ToSByteOrDefault();
			if (typeof(T) == typeof(short?)) return (T?) (object?) value.ToInt16OrDefault();
			if (typeof(T) == typeof(ushort?)) return (T?) (object?) value.ToUInt16OrDefault();
			if (typeof(T) == typeof(int?)) return (T?) (object?) value.ToInt32OrDefault();
			if (typeof(T) == typeof(uint?)) return (T?) (object?) value.ToUInt32OrDefault();
			if (typeof(T) == typeof(long?)) return (T?) (object?) value.ToInt64OrDefault();
			if (typeof(T) == typeof(ulong?)) return (T?) (object?) value.ToUInt64OrDefault();
			if (typeof(T) == typeof(float?)) return (T?) (object?) value.ToSingleOrDefault();
			if (typeof(T) == typeof(double?)) return (T?) (object?) value.ToDoubleOrDefault();
			if (typeof(T) == typeof(decimal?)) return (T?) (object?) value.ToDecimalOrDefault();
			if (typeof(T) == typeof(Guid?)) return (T?) (object?) value.ToGuidOrDefault();
			if (typeof(T) == typeof(Uuid128?)) return (T?) (object?) value.ToUuid128OrDefault();
			if (typeof(T) == typeof(Uuid96?)) return (T?) (object?) value.ToUuid96OrDefault();
			if (typeof(T) == typeof(Uuid80?)) return (T?) (object?) value.ToUuid80OrDefault();
			if (typeof(T) == typeof(Uuid64?)) return (T?) (object?) value.ToUuid64OrDefault();
			if (typeof(T) == typeof(TimeSpan?)) return (T?) (object?) value.ToTimeSpanOrDefault();
			if (typeof(T) == typeof(DateTime?)) return (T?) (object?) value.ToDateTimeOrDefault();
			if (typeof(T) == typeof(DateTimeOffset?)) return (T?) (object?) value.ToDateTimeOffsetOrDefault();
			if (typeof(T) == typeof(NodaTime.Instant?)) return (T?) (object?) value.ToInstantOrDefault();
			if (typeof(T) == typeof(NodaTime.Duration?)) return (T?) (object?) value.ToDurationOrDefault();
#endif

			#endregion </JIT_HACK>

			return value.Bind<T>(resolver);
		}

		/// <summary>Throw an exception when a required value was found to be null or missing</summary>
		/// <exception cref="InvalidOperationException"/>
		[DoesNotReturn, MethodImpl(MethodImplOptions.NoInlining)]
		internal static T FailRequiredValueIsNullOrMissing<T>() => throw new InvalidOperationException($"Required JSON value or type {typeof(T).GetFriendlyName()} was null or missing");

		#endregion

		#region OrDefault...

		// pour simplifier les conversion avec valeur par défaut
		// le but est de n'allouer la JsonValue "missing" que si besoin.
		// pour des raisons de perfs, on a des version typées, et on reserve OrDefault<T>(...) pour les clas les plus complexes

		/// <summary>Convert this value into a the specified CLR type, with a fallback value if it is null or missing.</summary>
		/// <typeparam name="T">Target CLR type</typeparam>
		/// <exception cref="JsonBindingException">If the value cannot be bound to the specified type.</exception>
		/// <remarks>If the value is <see langword="null"/> or "null-like", this will return the <paramref name="defaultValue"/>.</remarks>
		[Pure, ContractAnnotation("defaultValue:notnull => notnull")]
		[return: System.Diagnostics.CodeAnalysis.NotNullIfNotNull(nameof(defaultValue))]
		public static T? OrDefault<T>(this JsonValue? value, T? defaultValue)
		{
			if (value is null or JsonNull)
			{
				return defaultValue;
			}

			if (default(T) is not null)
			{ // use the JIT optimized version for non-nullable value types
				return value.As<T>();
			}

			return value.Bind<T>() ?? defaultValue;
		}

		/// <summary>Convert this value into a the specified CLR type, with a fallback value if it is null or missing.</summary>
		/// <typeparam name="T">Target CLR type</typeparam>
		/// <exception cref="JsonBindingException">If the value cannot be bound to the specified type.</exception>
		/// <remarks>If the value is <see langword="null"/> or "null-like", this will return the <paramref name="defaultValue"/>.</remarks>
		[Pure]
		[return: NotNullIfNotNull(nameof(defaultValue))]
		public static T? OrDefault<T>(this JsonValue? value, T? defaultValue, ICrystalJsonTypeResolver? customResolver)
		{
			if (value is null or JsonNull)
			{
				return defaultValue;
			}

			if (default(T) is not null)
			{ // use the JIT optimized version for non-nullable value types
				return value.As<T>(customResolver);
			}

			return value.Bind<T>(customResolver) ?? defaultValue;
		}

		/// <summary>Returns the converted value, or a fallback value if it is missing</summary>
		/// <param name="value">JSON value to convert</param>
		/// <param name="missingValue">Fallback value</param>
		/// <returns>The converted value, or <paramref name="missingValue"/> if it is <see langword="null"/> or missing</returns>
		[Pure]
		public static JsonValue OrDefault(this JsonValue? value, JsonValue? missingValue) => value is not (null or JsonNull) ? value : (missingValue ?? JsonNull.Missing);

		/// <summary>Returns the converted value, or a fallback value if it is missing</summary>
		/// <param name="value">JSON value to convert</param>
		/// <param name="missingValue">Fallback value</param>
		/// <returns>The converted value, or <paramref name="missingValue"/> if it is <see langword="null"/> or missing</returns>
		[Pure]
		public static string OrDefault(this JsonValue? value, string missingValue) => value is not (null or JsonNull) ? value.ToString() : missingValue;

		/// <summary>Returns the converted value, or a fallback value if it is missing</summary>
		/// <param name="value">JSON value to convert</param>
		/// <param name="missingValue">Fallback value</param>
		/// <returns>The converted value, or <paramref name="missingValue"/> if it is <see langword="null"/> or missing</returns>
		[Pure]
		public static bool OrDefault(this JsonValue? value, bool missingValue) => value is not (null or JsonNull) ? value.ToBoolean() : missingValue;

		/// <summary>Returns the converted value, or a fallback value if it is missing</summary>
		/// <param name="value">JSON value to convert</param>
		/// <param name="missingValue">Fallback value</param>
		/// <returns>The converted value, or <paramref name="missingValue"/> if it is <see langword="null"/> or missing</returns>
		[Pure]
		public static int OrDefault(this JsonValue? value, int missingValue) => value is not (null or JsonNull) ? value.ToInt32() : missingValue;

		/// <summary>Returns the converted value, or a fallback value if it is missing</summary>
		/// <param name="value">JSON value to convert</param>
		/// <param name="missingValue">Fallback value</param>
		/// <returns>The converted value, or <paramref name="missingValue"/> if it is <see langword="null"/> or missing</returns>
		[Pure]
		public static long OrDefault(this JsonValue? value, long missingValue) => value is not (null or JsonNull) ? value.ToInt64() : missingValue;

		/// <summary>Returns the converted value, or a fallback value if it is missing</summary>
		/// <param name="value">JSON value to convert</param>
		/// <param name="missingValue">Fallback value</param>
		/// <returns>The converted value, or <paramref name="missingValue"/> if it is <see langword="null"/> or missing</returns>
		[Pure]
		public static double OrDefault(this JsonValue? value, double missingValue) => value is not (null or JsonNull) ? value.ToDouble() : missingValue;

		/// <summary>Returns the converted value, or a fallback value if it is missing</summary>
		/// <param name="value">JSON value to convert</param>
		/// <param name="missingValue">Fallback value</param>
		/// <returns>The converted value, or <paramref name="missingValue"/> if it is <see langword="null"/> or missing</returns>
		[Pure]
		public static float OrDefault(this JsonValue? value, float missingValue) => value is not (null or JsonNull) ? value.ToSingle() : missingValue;

		/// <summary>Returns the converted value, or a fallback value if it is missing</summary>
		/// <param name="value">JSON value to convert</param>
		/// <param name="missingValue">Fallback value</param>
		/// <returns>The converted value, or <paramref name="missingValue"/> if it is <see langword="null"/> or missing</returns>
		[Pure]
		public static Guid OrDefault(this JsonValue? value, Guid missingValue) => value is not (null or JsonNull) ? value.ToGuid() : missingValue;

		/// <summary>Returns the converted value, or a fallback value if it is missing</summary>
		/// <param name="value">JSON value to convert</param>
		/// <param name="missingValue">Fallback value</param>
		/// <returns>The converted value, or <paramref name="missingValue"/> if it is <see langword="null"/> or missing</returns>
		[Pure]
		public static Uuid128 OrDefault(this JsonValue? value, Uuid128 missingValue) => value is not (null or JsonNull) ? value.ToUuid128() : missingValue;

		/// <summary>Returns the converted value, or a fallback value if it is missing</summary>
		/// <param name="value">JSON value to convert</param>
		/// <param name="missingValue">Fallback value</param>
		/// <returns>The converted value, or <paramref name="missingValue"/> if it is <see langword="null"/> or missing</returns>
		[Pure]
		public static Uuid64 OrDefault(this JsonValue? value, Uuid64 missingValue) => value is not (null or JsonNull) ? value.ToUuid64() : missingValue;

		/// <summary>Returns the converted value, or a fallback value if it is missing</summary>
		/// <param name="value">JSON value to convert</param>
		/// <param name="missingValue">Fallback value</param>
		/// <returns>The converted value, or <paramref name="missingValue"/> if it is <see langword="null"/> or missing</returns>
		[Pure]
		public static TimeSpan OrDefault(this JsonValue? value, TimeSpan missingValue) => value is not (null or JsonNull) ? value.ToTimeSpan() : missingValue;

		/// <summary>Returns the converted value, or a fallback value if it is missing</summary>
		/// <param name="value">JSON value to convert</param>
		/// <param name="missingValue">Fallback value</param>
		/// <returns>The converted value, or <paramref name="missingValue"/> if it is <see langword="null"/> or missing</returns>
		[Pure]
		public static DateTime OrDefault(this JsonValue? value, DateTime missingValue) => value is not (null or JsonNull) ? value.ToDateTime() : missingValue;

		/// <summary>Returns the converted value, or a fallback value if it is missing</summary>
		/// <param name="value">JSON value to convert</param>
		/// <param name="missingValue">Fallback value</param>
		/// <returns>The converted value, or <paramref name="missingValue"/> if it is <see langword="null"/> or missing</returns>
		[Pure]
		public static DateTimeOffset OrDefault(this JsonValue? value, DateTimeOffset missingValue) => value is not (null or JsonNull) ? value.ToDateTimeOffset() : missingValue;

		/// <summary>Returns the converted value, or a fallback value if it is missing</summary>
		/// <param name="value">JSON value to convert</param>
		/// <param name="missingValue">Fallback value</param>
		/// <returns>The converted value, or <paramref name="missingValue"/> if it is <see langword="null"/> or missing</returns>
		[Pure]
		public static NodaTime.Instant OrDefault(this JsonValue? value, NodaTime.Instant missingValue) => value is not (null or JsonNull) ? value.ToInstant() : missingValue;

		/// <summary>Returns the converted value, or a fallback value if it is missing</summary>
		/// <param name="value">JSON value to convert</param>
		/// <param name="missingValue">Fallback value</param>
		/// <returns>The converted value, or <paramref name="missingValue"/> if it is <see langword="null"/> or missing</returns>
		[Pure]
		public static NodaTime.Duration OrDefault(this JsonValue? value, NodaTime.Duration missingValue) => value is not (null or JsonNull) ? value.ToDuration() : missingValue;

		/// <summary>Returns the converted value, or a fallback value created from a factory if it is missing</summary>
		/// <param name="value">JSON value to convert</param>
		/// <param name="factory">Factory method that is invoked to produce a fallback value</param>
		/// <param name="resolver">Optional custom resolver used to bind the value into a managed type.</param>
		/// <returns>The converted value, or the value returned by <paramref name="factory"/> if it is <see langword="null"/> or missing</returns>
		[Pure]
		public static T OrDefault<T>(this JsonValue? value, Func<T> factory, ICrystalJsonTypeResolver? resolver = null)
		{
			if (value is not (null or JsonNull))
			{
				var res = resolver != null ? value.As<T>(resolver) : value.As<T>();
				if (default(T) is not null || res != null)
				{
					return res!;
				}
			}
			return factory();
		}

		/// <summary>Returns the converted value, or a fallback value created from a factory if it is missing</summary>
		/// <param name="value">JSON value to convert</param>
		/// <param name="factory">Factory method that is invoked to produce a fallback value</param>
		/// <param name="arg">Argument passed to <paramref name="factory"/> when it is invoked.</param>
		/// <param name="resolver">Optional custom resolver used to bind the value into a managed type.</param>
		/// <returns>The converted value, or the value returned by <paramref name="factory"/> if it is <see langword="null"/> or missing</returns>
		public static T OrDefault<T, TArg>(this JsonValue? value, Func<TArg, T> factory, TArg arg, ICrystalJsonTypeResolver? resolver = null)
		{
			if (value is not (null or JsonNull))
			{
				var res = resolver != null ? value.As<T>(resolver) : value.As<T>();
				if (default(T) is not null || res != null)
				{
					return res!;
				}
			}
			return factory(arg);
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
			if (value.Type != JsonType.Object) return FailValueIsNotAnObject(value); // => throws
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
			if (value is null || value.IsNull)
			{
				return !required ? null : FailObjectIsNullOrMissing();
			}
			if (value.Type != JsonType.Object)
			{
				return FailValueIsNotAnObject(value);
			}
			return (JsonObject) value;
		}

		[DoesNotReturn, MethodImpl(MethodImplOptions.NoInlining)]
		internal static JsonObject FailObjectIsNullOrMissing() => throw new InvalidOperationException("Required JSON object was null or missing.");

		[DoesNotReturn, MethodImpl(MethodImplOptions.NoInlining)]
		internal static JsonObject FailValueIsNotAnObject(JsonValue value) => throw CrystalJson.Errors.Parsing_CannotCastToJsonObject(value.Type);

		[Pure]
		public static JsonObject ToJsonObject([InstantHandle] this IEnumerable<KeyValuePair<string, JsonValue>> items, IEqualityComparer<string>? comparer = null)
		{
			return JsonObject.Create(items!, comparer);
		}

		[Pure]
		public static JsonObject ToJsonObject<TValue>([InstantHandle] this IEnumerable<KeyValuePair<string, TValue>> items, IEqualityComparer<string>? comparer = null)
		{
			Contract.NotNull(items);

			//TODO: move this as JsonObject.FromValues(...)

			comparer ??= JsonObject.ExtractKeyComparer(items) ?? StringComparer.Ordinal;

			var map = new Dictionary<string, JsonValue>(items.TryGetNonEnumeratedCount(out var count) ? count : 0, comparer);
			var context = new CrystalJsonDomWriter.VisitingContext();
			foreach (var item in items)
			{
				map.Add(item.Key, JsonValue.FromValue(CrystalJsonDomWriter.Default, ref context, item.Value));
			}
			return new JsonObject(map, readOnly: false);
		}

		[Pure]
		public static JsonObject ToJsonObject<TValue>([InstantHandle] this IEnumerable<KeyValuePair<string, TValue>> items, [InstantHandle] Func<TValue, JsonValue?> valueSelector, IEqualityComparer<string>? comparer = null)
		{
			Contract.NotNull(items);
			Contract.NotNull(valueSelector);

			//TODO: move this as JsonObject.FromValues(...)

			comparer ??= JsonObject.ExtractKeyComparer(items) ?? StringComparer.Ordinal;

			var map = new Dictionary<string, JsonValue>(items.TryGetNonEnumeratedCount(out var count) ? count : 0, comparer);
			foreach (var item in items)
			{
				Contract.Debug.Assert(item.Key != null, "Item cannot have a null key");
				map.Add(item.Key, valueSelector(item.Value) ?? JsonNull.Null);
			}
			return new JsonObject(map, readOnly: false);
		}

		[Pure]
		public static JsonObject ToJsonObject<TElement>([InstantHandle] this IEnumerable<TElement> source, [InstantHandle] Func<TElement, string> keySelector, [InstantHandle] Func<TElement, JsonValue?> valueSelector, IEqualityComparer<string>? comparer = null)
		{
			Contract.NotNull(source);
			Contract.NotNull(keySelector);
			Contract.NotNull(valueSelector);

			//TODO: move this as JsonObject.FromValues(...)

			var map = new Dictionary<string, JsonValue>(source.TryGetNonEnumeratedCount(out var count) ? count : 0, comparer ?? StringComparer.Ordinal);
			foreach (var item in source)
			{
				var key = keySelector(item);
				Contract.Debug.Assert(key != null, "key selector should not return null");
				var child = valueSelector(item) ?? JsonNull.Null;
				map.Add(key, child);
			}
			return new JsonObject(map, readOnly: false);
		}

		[Pure]
		public static JsonObject ToJsonObject<TElement, TValue>([InstantHandle] this IEnumerable<TElement> source, [InstantHandle] Func<TElement, string> keySelector, [InstantHandle] Func<TElement, TValue> valueSelector, IEqualityComparer<string>? comparer = null)
		{
			Contract.NotNull(source);
			Contract.NotNull(keySelector);
			Contract.NotNull(valueSelector);

			//TODO: move this as JsonObject.FromValues(...)

			var map = new Dictionary<string, JsonValue>(source.TryGetNonEnumeratedCount(out var count) ? count : 0, comparer ?? StringComparer.Ordinal);
			var context = new CrystalJsonDomWriter.VisitingContext();
			foreach (var item in source)
			{
				var key = keySelector(item);
				Contract.Debug.Assert(key != null, "key selector should not return null");
				var child = valueSelector(item);
				map.Add(key, JsonValue.FromValue(CrystalJsonDomWriter.Default, ref context, child));
			}
			return new JsonObject(map, readOnly: false);
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

		[DoesNotReturn]
		private static JsonNumber FailValueIsNotANumber(JsonValue? value)
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

		[DoesNotReturn]
		private static JsonString FailValueIsNotAString(JsonValue? value)
		{
			if (value.IsNullOrMissing()) ThrowHelper.ThrowInvalidOperationException("Expected JSON string was either null or missing.");
			throw CrystalJson.Errors.Parsing_CannotCastToJsonString(value.Type);
		}

		#endregion

	}

}
