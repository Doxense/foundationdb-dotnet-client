#region Copyright (c) 2023 SnowBank SAS, (c) 2005-2023 Doxense SAS
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
	using System.Collections;
	using System.Collections.Generic;
	using System.Diagnostics.CodeAnalysis;
	using System.Globalization;
	using System.Linq.Expressions;
	using System.Reflection;
	using System.Runtime.CompilerServices;
	using System.Xml;
	using Doxense.Collections.Caching;
	using Doxense.Collections.Tuples;
	using Doxense.Diagnostics.Contracts;
	using Doxense.Runtime;
	using Doxense.Serialization;
	using JetBrains.Annotations;

	/// <summary>Internal helper pour la conversion de données en string JSON</summary>
	public static class CrystalJsonVisitor
	{

		public static class CustomVisitor<T>
		{
			/// <summary>Fixe ou retourne le sérialiseur spécifique pour le type <typeparamref name="T"/></summary>
			public static CrystalJsonTypeVisitor? Handler
			{
				get => s_typeVisitorCache[typeof(T)];
				set => s_typeVisitorCache.SetItem(typeof(T), value);
			}
		}

		#region Type Writers ...

		private static readonly QuasiImmutableCache<Type, CrystalJsonTypeVisitor> s_typeVisitorCache = new QuasiImmutableCache<Type, CrystalJsonTypeVisitor>(TypeEqualityComparer.Default);

		private static readonly Func<Type, bool, CrystalJsonTypeVisitor> CreateVisitorForTypeCallback = CreateVisitorForType;

		/// <summary>Retourne un visiteur capable de sérialiser le type indiqué</summary>
		/// <param name="type">Type du member parent (mapping time) ou de l'objet actuel (au runtime)</param>
		/// <param name="atRuntime">false lors du mapping initial, true si on est au runtime (avec le vrai objet)</param>
		public static CrystalJsonTypeVisitor GetVisitorForType(Type type, bool atRuntime = false)
		{
			bool cacheable = !atRuntime || type.IsConcrete();

			if (cacheable)
			{ // on peut mettre en cache ce type
			  //TODO: gérer un cache différent pour AtRuntime true/false ?
			  // note: normalement le seul qui nous appelle avec atRuntime==true sont VisitObjectAtRuntime et VisitInterfaceAtRuntime
				return s_typeVisitorCache.GetOrAdd(type, CreateVisitorForTypeCallback, atRuntime);
			}

			// ne peut pas être mit en cache, on doit le créer à chaque fois ... :/
			return CreateVisitorForType(type, true);
		}

		/// <summary>Génère le convertisseur pour un type</summary>
		/// <param name="type">Type déclaré par l'objet parent</param>
		/// <param name="atRuntime">Si true, <paramref name="type"/> est le type réel de l'instance. Si false, c'est potentiellement un type de base, tel que déclaré dans le Field ou Property du parent</param>
		/// <returns>Delegate capable de sérialiser ce type en JSON</returns>
		private static CrystalJsonTypeVisitor CreateVisitorForType(
#if USE_ANNOTATIONS
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicMethods | DynamicallyAccessedMemberTypes.NonPublicMethods | DynamicallyAccessedMemberTypes.Interfaces)]
#endif
			Type type,
			bool atRuntime)
		{
			//	Type		Primitive	ValueType	Class	Enum	Interface
			// string		   _           _          X       _
			// int32, bool     X           X          _       _
			// decimal         _           X          _       _
			// enums           _           X          _       X
			// DateTime        _           X          _       _
			// TimeSpan        _           X          _       _
			// structs         _           X          _       _
			// classes         _           _          X       _
			// T[]             _           _          X       _		IEnumerable<T>
			// Dictionary<K,V> _           _          X       _		ICollection<KeyValuePair<K,V>>
			// dynamic         _           _          X       _		IDictionary<string, object>
			// interface	   _           _          ?       _

			//bool atRuntime = runtimeType != null;
			//Type type = runtimeType ?? declaringType;

			if (typeof(string) == type)
			{
				return VisitStringInternal;
			}

			if (type.IsPrimitive)
			{ // Uniquement les types de base de la CLR (bool, int, double, char, ...)
				return CreateVisitorForPrimitiveType(type);
			}

			var nullableType = Nullable.GetUnderlyingType(type);
			if (nullableType != null)
			{ // C'est un type nullable!  (on va utiliser un wrapper)
				return CreateVisitorForNullableType(type, nullableType);
			}

			if (typeof(object) == type || type.IsAbstract)
			{
				// si une class/struct à un field de type object, on est obligé de faire la résolution AU RUNTIME !
				// ie: le writer de la classe va regarder le type de l'objet au moment de la sérialisation, et
				// reswitcher vers le bon writer. Si c'est vraiment un object (cad un new object()), la on écrit "{ }".
				if (atRuntime)
					return (_, _, _, writer) => { writer.WriteEmptyObject(); };
				else
					return VisitObjectAtRuntime;
			}

			// si la class à une méthode statique "JsonSerialize", il faut l'utiliser
			var visitor = TryGetSerializeMethodVisitor(type);
			if (visitor != null) return visitor;

			if (type.IsInstanceOf<IJsonSerializable>())
			{
				return VisitJsonSerializable;
			}

			// si la class à une méthode statique "JsonPack", il faut l'utiliser
			visitor = TryGetBindableMethodVisitor(type);
			if (visitor != null) return visitor;

			if (type.IsInstanceOf<IJsonPackable>())
			{
				return VisitJsonPackable;
			}

			if (type.IsValueType)
			{ // Structs, DateTime, Enums, decimal, ...
				return CreateVisitorForValueType(type);
			}

			// Reference Types (string, classes, ...)

			if (type.IsInstanceOf<System.Xml.XmlNode>())
			{
				return (v, _, r, writer) => writer.WriteValue(((System.Xml.XmlNode?) v)?.OuterXml);
			}

			if (type.IsInstanceOf<System.Collections.IEnumerable>())
			{ // collection d'objets...
				return CreateVisitorForEnumerableType(type);
			}

			if (type.IsInterface)
			{ // pour les interfaces, il faut consulter le type au runtime, et sérialiser CELUI LA !
				if (atRuntime)
					return (_, _, _, writer) => writer.WriteEmptyObject();
				else
					return VisitInterfaceAtRuntime;
			}

			#region Common Class Types...

			if (typeof(System.Text.StringBuilder) == type)
			{
				return (v, _, _, writer) => writer.WriteValue(v as System.Text.StringBuilder);
			}

			if (typeof(System.Net.IPAddress) == type)
			{
				return (v, _, _, writer) => writer.WriteValue(v as System.Net.IPAddress);
			}

			if (typeof(System.Version) == type)
			{
				return (v, _, _, writer) => writer.WriteValue(v as System.Version);
			}

			if (typeof(System.Uri) == type)
			{
				return (v, _, _, writer) => writer.WriteValue(v as System.Uri);
			}

			// obligé de faire un IsAssignableFrom il y a plein de types dérivés pour tous les différents types de timezone...
			if (typeof(NodaTime.DateTimeZone).IsAssignableFrom(type))
			{
				return (v, _, _, writer) => writer.WriteValue(v as NodaTime.DateTimeZone);
			}
			// obligé de faire un IsAssignableFrom il y a plein de types dérivés pour tous les différents types de timezone...
			if (typeof(NodaTime.DateTimeZone).IsAssignableFrom(type))
			{
				return (v, _, _, writer) => writer.WriteValue(v as NodaTime.DateTimeZone);
			}

			if (typeof(Type).IsAssignableFrom(type))
			{
				return (v, _, _, writer) => writer.WriteValue(((JsonString) JsonString.Return((Type) v!)).Value);
			}
			#endregion

			// class ?
			return VisitCustomClassOrStruct;
		}

		private static void VisitStringInternal(object? value, Type declaredType, Type? runtimeType, CrystalJsonWriter writer)
		{
			writer.WriteValue(value as string);
		}

		private static CrystalJsonTypeVisitor? TryGetSerializeMethodVisitor(
#if USE_ANNOTATIONS
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicMethods | DynamicallyAccessedMemberTypes.NonPublicMethods)]
#endif
			Type type)
		{
			//Duck Typing: on reconnaît les patterns suivants
			// - static JsonSerialize(T, CrystalJsonWriter) => JsonValue (ou dérivée)
			// - instance.JsonSerialize(CrystalJsonWriter) => JsonValue (ou dérivée)

			// on premier on regarde la méthode statique
			var staticMethod = type.GetMethod("JsonSerialize", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.FlattenHierarchy);
			if (staticMethod != null)
			{ // crée un writer qui va appeler Type.JsonSerialize(instance, ...)
				return CreateVisitorForStaticSerializableMethod(type, staticMethod);
			}

			// en deuxième la méthode d'instance
			var instanceMethod = type.GetMethod("JsonSerialize", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.FlattenHierarchy);
			if (instanceMethod != null)
			{ // crée un writer qui va appeler instance.JsonSerialize(...)
				return CreateVisitorForInstanceSerializableMethod(type, instanceMethod);
			}

			return null;
		}

		private static CrystalJsonTypeVisitor? TryGetBindableMethodVisitor(
#if USE_ANNOTATIONS
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicMethods | DynamicallyAccessedMemberTypes.NonPublicMethods)]
#endif
			Type type)
		{
			//Duck Typing: on reconnaît les patterns suivants
			// - static JsonPack(T instance, ...) => JsonValue (ou dérivée)
			// - instance.JsonPack(...) => JsonValue (ou dérivée)

			// en premier on regarde la méthode statique
			var staticMethod = type.GetMethod("JsonPack", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.FlattenHierarchy);
			if (staticMethod != null)
			{ // crée un writer qui va appeler Type.JsonSerialize(instance, ...)
				return CreateVisitorForStaticBindabledMethod(type, staticMethod);
			}
			// en deuxième, la méthode d'instance
			var instanceMethod = type.GetMethod("JsonPack", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.FlattenHierarchy);
			if (instanceMethod != null)
			{ // crée un writer qui va appeler Type.JsonSerialize(instance, ...)
				return CreateVisitorForInstanceBindabledMethod(type, instanceMethod);
			}

			return null;
		}

		/// <summary>Génère le convertisseur pour un type primitive (int, bool, char, ..)</summary>
		/// <param name="type">Type primitif (Int32, Boolean, Single, ...)</param>
		/// <returns>Delegate capable de sérialiser ce type en JSON</returns>
		private static CrystalJsonTypeVisitor CreateVisitorForPrimitiveType(Type type)
		{
			if (typeof (bool) == type)
			{ // Boolean
				return (v, _, _, writer) => writer.WriteValue((bool) v!);
			}
			if (typeof(byte) == type)
			{ // UInt8
				return (v, _, _, writer) => writer.WriteValue((int) (byte) v!);
			}
			if (typeof(sbyte) == type)
			{ // Int8
				return (v, _, _, writer) => writer.WriteValue((sbyte) v!);
			}
			if (typeof (short) == type)
			{ // Int16
				return (v, _, _, writer) => writer.WriteValue((short) v!);
			}
			if (typeof (ushort) == type)
			{ // UInt16
				return (v, _, _, writer) => writer.WriteValue((uint) (ushort) v!);
			}
			if (typeof (int) == type)
			{ // Int32
				return (v, _, _, writer) => writer.WriteValue((int) v!);
			}
			if (typeof (uint) == type)
			{ // Int32
				return (v, _, _, writer) => writer.WriteValue((uint) v!);
			}
			if (typeof (long) == type)
			{ // Int64
				return (v, _, _, writer) => writer.WriteValue((long) v!);
			}
			if (typeof (ulong) == type)
			{ // Int64
				return (v, _, _, writer) => writer.WriteValue((ulong) v!);
			}
			if (typeof (float) == type)
			{ // Single
				return (v, _, _, writer) => writer.WriteValue((float) v!);
			}
			if (typeof (double) == type)
			{ // Double
				return (v, _, _, writer) => writer.WriteValue((double) v!);
			}
			if (typeof (char) == type)
			{ // char
				return (v, _, _, writer) => writer.WriteValue((char) v!);
			}

			if (type.IsInstanceOf<IConvertible>())
			{ // le type sait se convertir tout seul !
				return (v, _, _, writer) => writer.WriteRaw(((IConvertible) v!).ToString(CultureInfo.InvariantCulture));
			}

			// ramasse miette pour les sbyte, uint64 et autres ...
			return (v, _, _, writer) => writer.WriteRaw(v!.ToString()!);
		}

		/// <summary>Génère le convertisseur pour un Nullable&lt;T&gt; (int?, bool?, TimeSpan?, Enum?, ...)</summary>
		/// <param name="type">Type nullable</param>
		/// <param name="realType">Type réel sous-jacent ('int' pour un 'int?</param>
		/// <returns>Delegate capable de sérialiser ce type en JSON</returns>
		private static CrystalJsonTypeVisitor CreateVisitorForNullableType(Type type, Type? realType)
		{
			realType ??= Nullable.GetUnderlyingType(type) ?? throw new InvalidOperationException("Could not get underlying type for " + type.GetFriendlyName());
			var visitor = GetVisitorForType(realType, atRuntime: false);
			if (visitor == null) throw CrystalJson.Errors.Serialization_DoesNotKnowHowToSerializeNullableType(type);
			// note: pour éviter de tout dupliquer, on utiliser un delegate avec une closure (donc il sera pas static comme les autres :( )
			return (v, _, r, writer) =>
			{
				if (v == null)
					writer.WriteNull(); // "null"
				else
					visitor(v, realType, r, writer);
			};
		}

		/// <summary>Génère un convertisseur pour une KeyValuePair&lt;TKey, TValue&gt;</summary>
		/// <param name="type">Type qui doit être un KeyValuePair&lt;,&gt;</param>
		/// <returns>Delegate capable de sérialiser des KVP en array de deux éléments</returns>
		private static CrystalJsonTypeVisitor CreateVisitorForKeyValuePairType(Type type)
		{
			var args = type.GetGenericArguments();
			if (args.Length != 2) throw new InvalidOperationException("Type must be a KeyValuePair<K,V>");

			// en fonction du type de clé, on a des implémentations plus ou moins optimisées...
			MethodInfo method;
			if (args[0] == typeof(string))
			{
				if (args[1] == typeof(string))
				{
					method = typeof(CrystalJsonVisitor).GetMethod(nameof(VisitStringKeyStringValuePair), BindingFlags.Static | BindingFlags.NonPublic)!;
					if (method == null) throw new InvalidOperationException("Missing method CrystalJsonVisitor.VisitStringKeyStringValuePair()");
				}
				else
				{ // optimized implementation for KV<string, T>
					var gen = typeof (CrystalJsonVisitor).GetMethod(nameof(VisitStringKeyValuePair), BindingFlags.Static | BindingFlags.NonPublic);
					if (gen== null) throw new InvalidOperationException("Missing method CrystalJsonVisitor.VisitStringKeyValuePair<V>()");
					method  = gen.MakeGenericMethod(args[1]);
				}
			}
			else if (args[0] == typeof(int))
			{ // optimized implementation for KV<int, T>
				var gen = typeof (CrystalJsonVisitor).GetMethod(nameof(VisitInt32KeyValuePair), BindingFlags.Static | BindingFlags.NonPublic);
				if (gen== null) throw new InvalidOperationException("Missing method CrystalJsonVisitor.VisitInt32KeyValuePair<K,V>()");
				method  = gen.MakeGenericMethod(args[1]);
			}
			else
			{ // generic implementation for KV<TK, TV>
				var gen = typeof (CrystalJsonVisitor).GetMethod(nameof(VisitKeyValuePair), BindingFlags.Static | BindingFlags.NonPublic);
				if (gen== null) throw new InvalidOperationException("Missing method CrystalJsonVisitor.VisitKeyValuePair<K,V>()");
				method  = gen.MakeGenericMethod(args);
			}
			return (CrystalJsonTypeVisitor) method.CreateDelegate(typeof (CrystalJsonTypeVisitor));
		}

		[UsedImplicitly]
		private static void VisitInt32KeyValuePair<TValue>(object? value, Type _, Type? __, CrystalJsonWriter writer)
		{
			if (value == null)
			{
				writer.WriteNull();
				return;
			}

			// '[ KEY, VALUE ]'
			var kvp = (KeyValuePair<int, TValue>) value;
			writer.WriteInlinePair<TValue>(kvp.Key, kvp.Value);
		}

		[UsedImplicitly]
		private static void VisitStringKeyStringValuePair(object? value, Type _, Type? __, CrystalJsonWriter writer)
		{
			if (value == null)
			{
				writer.WriteNull();
				return;
			}

			// '[ KEY, VALUE ]'
			var kvp = (KeyValuePair<string, string>) value;
			writer.WriteInlinePair(kvp.Key, kvp.Value);
		}

		[UsedImplicitly]
		private static void VisitStringKeyValuePair<TValue>(object? value, Type _, Type? __, CrystalJsonWriter writer)
		{
			if (value == null)
			{
				writer.WriteNull();
				return;
			}

			// '[ KEY, VALUE ]'
			var kvp = (KeyValuePair<string, TValue>) value;
			writer.WriteInlinePair<TValue>(kvp.Key, kvp.Value);
		}

		[UsedImplicitly]
		private static void VisitKeyValuePair<TKey, TValue>(object? value, Type _, Type? __, CrystalJsonWriter writer)
		{
			if (value == null)
			{
				writer.WriteNull();
				return;
			}

			// '[ KEY, VALUE ]'
			var kvp = (KeyValuePair<TKey, TValue>)value;
			var state = writer.BeginArray();
			{
				writer.WriteHeadSeparator();
				VisitValue<TKey>(kvp.Key, writer);
				writer.WriteTailSeparator();
				VisitValue<TValue>(kvp.Value, writer);
			}
			writer.EndArray(state);
		}

		private static CrystalJsonTypeVisitor CreateVisitorForArraySegmentType(Type type)
		{
			var args = type.GetGenericArguments();
			if (args.Length != 1) throw new InvalidOperationException("Type must be an ArraySegment<T>");

			if (args[0] == typeof(byte))
			{ // byte => Base64
				return (v, _, _, w) => w.WriteBuffer(((ArraySegment<byte>) v!).AsSlice());
			}
			else
			{ // T => array
				var method = typeof(CrystalJsonVisitor).GetMethod(nameof(VisitArraySegment), BindingFlags.Static | BindingFlags.NonPublic);
				if (method == null) throw new InvalidOperationException("Missing method CrystalJsonVisitor.VisitArraySegment<K,V>()");

				// crée la bonne version de la méthode, et transforme la en delegate
				var gen = method.MakeGenericMethod(args);
				return (CrystalJsonTypeVisitor) gen.CreateDelegate(typeof(CrystalJsonTypeVisitor));
			}
		}

		[UsedImplicitly]
		private static void VisitArraySegment<T>(object? value, Type declaringType, Type? runtimeType, CrystalJsonWriter writer)
		{
			var array = value != null ? (ArraySegment<T>) value : default;
			if (array.Count == 0)
			{
				if (array.Array == null)
				{
					writer.WriteNull();
				}
				else
				{
					writer.WriteEmptyArray();
				}
				return;
			}

			// '[ KEY, VALUE ]'
			var state = writer.BeginArray();
			{
				for (int i = 0; i < array.Count; i++)
				{
					writer.WriteFieldSeparator();
					VisitValue<T>(array.Array![array.Offset + i], writer);
				}
			}
			writer.EndArray(state);
		}

		/// <summary>Génère le convertisseur pour un Value Type (Datetime, TimeSpan, Guid, struct, enum, ...)</summary>
		/// <param name="type">Type ValueType</param>
		/// <returns>Delegate capable de sérialiser ce type en JSON</returns>
		private static CrystalJsonTypeVisitor CreateVisitorForValueType(Type type)
		{
			if (type == typeof (DateTime))
			{ // DateTime
				return (v, _, _, writer) => writer.WriteValue((DateTime) v!);
			}

			if (type == typeof (TimeSpan))
			{
				return (v, _, _, writer) => writer.WriteValue((TimeSpan) v!);
			}

			if (type == typeof (Guid))
			{ // Guid
				return (v, _, _, writer) => writer.WriteValue((Guid) v!);
			}

			if (type == typeof (DateTimeOffset))
			{ // DateTime
				return (v, _, _, writer) => writer.WriteValue((DateTimeOffset) v!);
			}

			if (type == typeof (decimal))
			{ // decimal (mais peut être n'importe quoi dans la pratique !)
				return (v, _, _, writer) => writer.WriteValue((decimal) v!);
			}

			if (type == typeof(Uuid128))
			{ // 128-bit UUID
				return (v, _, _, writer) => writer.WriteValue((Uuid128) v!);
			}

			if (type == typeof(Uuid96))
			{ // 96-bit UUID
				return (v, _, _, writer) => writer.WriteValue((Uuid96) v!);
			}

			if (type == typeof(Uuid80))
			{ // 80-bit UUID
				return (v, _, _, writer) => writer.WriteValue((Uuid80) v!);
			}

			if (type == typeof(Uuid64))
			{ // 64-bit UUID
				return (v, _, _, writer) => writer.WriteValue((Uuid64) v!);
			}


			if (type.IsInstanceOf<IVarTuple>())
			{ // Variable-Length Tuple (struct)
				return CreateVisitorForSTupleType(type);
			}

#if !NETFRAMEWORK && !NETSTANDARD
			if (type.IsInstanceOf<System.Runtime.CompilerServices.ITuple>())
			{ // Value-Tuple
				return CreateVisitorForITupleType(type);
			}
#endif

			if (type.IsEnum)
			{ // Enum => sous forme numérique (et pas chaîne)

				if (!type.IsDefined(typeof(FlagsAttribute), false))
				{ // pour les enum non-flags, on peut utiliser un cache
					return CreateCachedEnumVisitor(type);
				}
				else
				{ // sinon on va passer par le chemin classique (plus lent)
					return (v, _, _, writer) => writer.WriteEnum((Enum) v!);
				}
			}

			if (type.IsGenericType)
			{
				if (type.IsGenericInstanceOf(typeof (KeyValuePair<,>)))
				{
					return CreateVisitorForKeyValuePairType(type);
				}
				if (type.IsGenericInstanceOf(typeof(ArraySegment<>)))
				{
					return CreateVisitorForArraySegmentType(type);
				}
			}

			if (type == typeof(Slice))
			{
				return (v, _, _, writer) => writer.WriteBuffer((Slice) v!);
			}

			if (type == typeof (System.Drawing.Color))
			{ // Color => on sérialise le ".Name"
#if !NET461 && !NET472
				//TODO: HACKHACK: how to convert color into HTML name in .NET Standard 2.0 ?
				return (v, _, _, writer) => writer.WriteValue(((System.Drawing.Color) v!).ToString());
#else
				return (v, _, _, writer) => writer.WriteValue(System.Drawing.ColorTranslator.ToHtml((System.Drawing.Color) v));
#endif
			}

			#region NodaTime...

			if (type == typeof (NodaTime.Instant))
			{
				return (v, _, _, writer) => writer.WriteValue((NodaTime.Instant) v!);
			}
			if (type == typeof (NodaTime.Duration))
			{
				return (v, _, _, writer) => writer.WriteValue((NodaTime.Duration) v!);
			}
			if (type == typeof (NodaTime.LocalDateTime))
			{
				return (v, _, _, writer) => writer.WriteValue((NodaTime.LocalDateTime) v!);
			}
			if (type == typeof (NodaTime.ZonedDateTime))
			{
				return (v, _, _, writer) => writer.WriteValue((NodaTime.ZonedDateTime) v!);
			}
			if (type == typeof (NodaTime.Offset))
			{
				return (v, _, _, writer) => writer.WriteValue((NodaTime.Offset) v!);
			}
			if (type == typeof (NodaTime.OffsetDateTime))
			{
				return (v, _, _, writer) => writer.WriteValue((NodaTime.OffsetDateTime) v!);
			}
			if (type == typeof (NodaTime.LocalDate))
			{
				return (v, _, _, writer) => writer.WriteValue((NodaTime.LocalDate) v!);
			}
			if (type == typeof (NodaTime.LocalTime))
			{
				return (v, _, _, writer) => writer.WriteValue((NodaTime.LocalTime) v!);
			}

			#endregion

			// struct ?
			return VisitCustomClassOrStruct;
		}

		private static CrystalJsonTypeVisitor CreateCachedEnumVisitor(Type type)
		{
			var cache = EnumStringTable.GetCacheForType(type);
			return (v, _, _, writer) => writer.WriteEnum((Enum) v!, cache);
		}

		/// <summary>Génère le convertisseur pour un type énumérable (tableau, liste, dictionnaire, ...)</summary>
		/// <param name="type">Type implémentant IEnumerable</param>
		/// <returns>Delegate capable de sérialiser ce type en JSON</returns>
		private static CrystalJsonTypeVisitor CreateVisitorForEnumerableType(
#if USE_ANNOTATIONS
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.Interfaces)]
#endif
			Type type)
		{
			if (type.IsInstanceOf<ICollection<KeyValuePair<string, string>>>())
			{ // Key/Value store
				return (v, _, _, writer) => VisitStringDictionary(v as ICollection<KeyValuePair<string, string>>, writer);
			}

			if (type.IsInstanceOf<ICollection<KeyValuePair<string, object>>>())
			{ // Key/Value store / ExpandoObject
				return (v, _, _, writer) => VisitGenericObjectDictionary(v as ICollection<KeyValuePair<string, object>>, writer);
			}

			if (type.IsInstanceOf<IVarTuple>())
			{ // Tuple (non struct)
				return CreateVisitorForSTupleType(type);
			}

			//TODO: détecter s'il s'agit d'un Dictionary<string, V> et appeler VisitGenericDictionary<V>(...) qui est optimisé pour ce cas

			if (type.IsInstanceOf<IDictionary>())
			{ // Key/Value store

				if (type.IsGenericType)
				{ // Annuaire générique, on va traiter le cas Dictionary<string, T> de manière particulière
					var args = type.GetGenericArguments();
					if (args.Length == 2)
					{  // Dictionary<string, T>
						var arg0 = args[0];
						var arg1 = args[1];
						if (arg0 == typeof(string)) return arg1 == typeof(string) ? CreateIDictionaryVisitor_StringKeyAndValue() : CreateIDictionaryVisitor_StringKey(arg1);
						if (arg0 == typeof(int)) return arg1 == typeof(string) ? CreateIDictionaryVisitor_Int32Key_StringValue() : CreateIDictionaryVisitor_Int32Key(arg1);
						return CreateIDictionaryVisitor(arg0, arg1);
					}
				}
				return CreateIDictionaryVisitor(typeof(object), typeof(object));
			}

			if (type == typeof(byte[]))
			{ // Tableau de bytes
				return CreateByteBufferVisitor();
			}

			if (type == typeof(string[]))
			{ // Tableau de strings
				return VisitStringArrayInternal;
			}

			if (type.IsArray)
			{ // c'est un tableau : T[]
				var elemType = type.GetElementType() ?? throw new InvalidOperationException("Failed to get array element type for " + type.Name);
				return CompileGenericVisitorMethod(nameof(VisitArrayInternal), nameof(VisitArray), elemType);
			}

			if (type.IsInstanceOf<IEnumerable<string>>())
			{ // Enumeration de strings
				return (v, _, _, writer) => VisitEnumerableOfString((IEnumerable<string>?) v, writer);
			}

			// est-ce un IEnumerable<T> ?
			if (type.IsGenericType)
			{ // Collection générique, on essaye de regarder le type des éléments
				var args = type.GetGenericArguments();
				if (args.Length == 1)
				{
					return CreateIEnumerableVisitor(args[0]);
				}
			}

			// est-ce que c'est un type qui dérive d'une interface IEnumerable<T> ?
			var interfaces = type.GetInterfaces();
			if (interfaces.Length > 0)
			{
				foreach (var interf in interfaces)
				{
					if (interf.IsGenericInstanceOf(typeof(IEnumerable<>)))
					{
						var args = interf.GetGenericArguments();
						if (args.Length == 1)
						{
							return CreateIEnumerableVisitor(args[0]);
						}
					}
				}
			}

			// Enumeration non-générique
			return CreateIEnumerableVisitor(typeof(object));
		}

		private static CrystalJsonTypeVisitor CreateIEnumerableVisitor(Type itemType)
		{
			return (v, _, _, writer) => VisitEnumerable(v as IEnumerable, itemType, writer);
		}

		private static CrystalJsonTypeVisitor CreateIDictionaryVisitor(Type keyType, Type valueType)
		{
			return (v, _, _, writer) => VisitDictionary((IDictionary?) v, keyType, valueType, writer);
		}

		private static CrystalJsonTypeVisitor CreateIDictionaryVisitor_StringKeyAndValue()
		{
			return (v, _, _, writer) => VisitDictionary_StringKeyAndValue((IDictionary<string, string>?) v , writer);
		}

		private static CrystalJsonTypeVisitor CreateIDictionaryVisitor_StringKey(Type valueType)
		{
			var visitor = GetVisitorForType(valueType, atRuntime: false);
			return (v, _, _, writer) => VisitDictionary_StringKey(v as IDictionary, valueType, visitor, writer);
		}
		
		private static CrystalJsonTypeVisitor CreateIDictionaryVisitor_Int32Key_StringValue()
		{
			return (v, _, _, writer) => VisitDictionary_Int32Key_StringValue((IDictionary<int, string>?) v, writer);
		}
		
		private static CrystalJsonTypeVisitor CreateIDictionaryVisitor_Int32Key(Type valueType)
		{
			var visitor = GetVisitorForType(valueType, atRuntime: false);
			return (v, _, _, writer) => VisitDictionary_Int32Key(v as IDictionary, valueType, visitor, writer);
		}
		
		private static CrystalJsonTypeVisitor CreateByteBufferVisitor()
		{
			return (v, _, _, writer) => writer.WriteBuffer(v as byte[]);
		}

		/// <summary>Génère le convertisseur pour un type respectant le pattern JsonSerializable static</summary>
		/// <param name="type">Type de la classe</param>
		/// <param name="method">Informations sur la méthode static "JsonSerializable"</param>
		/// <returns>Delegate capable de sérialiser ce type en JSON</returns>
		private static CrystalJsonTypeVisitor CreateVisitorForStaticSerializableMethod(Type type, MethodInfo method)
		{
			Contract.Debug.Requires(type != null && method != null);
			var prms = method.GetParameters();
			// on accepte:
			// * void TValue.JsonSeralize(TValue value, CrystalJsonWriter writer)
			if (prms.Length != 2) throw CrystalJson.Errors.Serialization_StaticJsonSerializeMethodInvalidSignature(type, method);
			var prmType = prms[0].ParameterType;
			if (!type.IsAssignableFrom(prmType)) throw CrystalJson.Errors.Serialization_StaticJsonSerializeMethodInvalidFirstParam(type, method, prmType);
			if (prms[1].ParameterType != typeof(CrystalJsonWriter)) throw CrystalJson.Errors.Serialization_StaticJsonSerializeMethodInvalidSecondParam(type, method, prmType);

			var prmValue = Expression.Parameter(typeof(object), "v");
			var prmDeclaringType = Expression.Parameter(typeof(Type), "dt");
			var prmRuntimeType = Expression.Parameter(typeof(Type), "rt");
			var prmWriter = Expression.Parameter(typeof(CrystalJsonWriter), "w");
			// il faut caster l'objet
			var castedValue = prmValue.CastFromObject(prmType);
			var body = Expression.Call(method, castedValue, prmWriter);
			return Expression.Lambda<CrystalJsonTypeVisitor>(body, true, new[] { prmValue, prmDeclaringType, prmRuntimeType, prmWriter }).Compile();
		}

		/// <summary>Génère le convertisseur pour un type respectant le pattern JsonSerializable static</summary>
		/// <param name="type">Type de la classe</param>
		/// <param name="method">Informations sur la méthode static "JsonSerializable"</param>
		/// <returns>Delegate capable de sérialiser ce type en JSON</returns>
		private static CrystalJsonTypeVisitor CreateVisitorForInstanceSerializableMethod(Type type, MethodInfo method)
		{
#if DEBUG_JSON_CONVERTER
			Debug.WriteLine("CrystalJsonConverter.CreateConverterForStaticSerializable(" + type + ", " + method + ")");
#endif
			var prms = method.GetParameters();
			// on accepte: (TValue value).JsonSeralize(CrystalJsonWriter writer)
			if (prms.Length != 1 || prms[0].ParameterType != typeof (CrystalJsonWriter)) throw new InvalidOperationException($"Instance serialization method must take a single parameter of type '{nameof(CrystalJsonWriter)}'."); //TODO

			var prmValue = Expression.Parameter(typeof(object), "value");
			var prmDeclaringType = Expression.Parameter(typeof(Type), "declaringType");
			var prmRuntimeType = Expression.Parameter(typeof(Type), "runtimeType");
			var prmWriter = Expression.Parameter(typeof(CrystalJsonWriter), "writer");
			// il faut caster l'objet
			var castedValue = prmValue.CastFromObject(type);
			var body = Expression.Call(castedValue, method, prmWriter);
			return Expression.Lambda<CrystalJsonTypeVisitor>(body, new[] { prmValue, prmDeclaringType, prmRuntimeType, prmWriter }).Compile();
		}


		/// <summary>Génère le convertisseur pour un type respectant le pattern JsonPack static</summary>
		/// <param name="type">Type de la classe</param>
		/// <param name="method">Informations sur la méthode static "JsonPack"</param>
		/// <returns>Delegate capable de sérialiser ce type en JSON</returns>
		private static CrystalJsonTypeVisitor CreateVisitorForStaticBindabledMethod(Type type, MethodInfo method)
		{
			Contract.Debug.Requires(type != null && method != null);
			var prms = method.GetParameters();
			// on accepte:
			// * TValue.JsonPack(TValue value, CrystalJsonSettings settings, ICrystalJsonTypeResolver resolver)
			if (prms.Length != 3) throw CrystalJson.Errors.Serialization_StaticJsonSerializeMethodInvalidSignature(type, method);
			var prmType = prms[0].ParameterType;
			if (!type.IsAssignableFrom(prmType)) throw CrystalJson.Errors.Serialization_StaticJsonSerializeMethodInvalidFirstParam(type, method, prmType);
			if (prms[1].ParameterType != typeof(CrystalJsonSettings)) throw CrystalJson.Errors.Serialization_StaticJsonSerializeMethodInvalidSecondParam(type, method, prmType);

			var prmWriter = Expression.Parameter(typeof(CrystalJsonWriter), "w");
			var prmValue = Expression.Parameter(typeof(object), "v");
			var prmDeclaringType = Expression.Parameter(typeof(Type), "dt"); //NOT USED?
			var prmRuntimeType = Expression.Parameter(typeof(Type), "rt"); //NOT USED?
			var varSettings = Expression.Property(prmWriter, nameof(CrystalJsonWriter.Settings));
			var varResolver = Expression.Property(prmWriter, nameof(CrystalJsonWriter.Resolver));
			// il faut caster l'objet
			var castedValue = prmValue.CastFromObject(prmType);
			// body = (Type).JsonPack(instance, settings, resolver)
			var body = Expression.Call(method, castedValue, varSettings, varResolver);

			// body = (Type).JsonPack(instance, settings, resolver).JsonSerialize(writer)
			body = Expression.Call(body, nameof(JsonValue.JsonSerialize), null, prmWriter);

			var lambda = Expression.Lambda<CrystalJsonTypeVisitor>(body, true, new[] { prmValue, prmDeclaringType, prmRuntimeType, prmWriter });
			return lambda.Compile();
		}

		/// <summary>Génère le convertisseur pour un type respectant le pattern JsonSerialize static</summary>
		/// <param name="type">Type de la classe</param>
		/// <param name="method">Informations sur la méthode static "JsonSerialize"</param>
		/// <returns>Delegate capable de sérialiser ce type en JSON</returns>
		private static CrystalJsonTypeVisitor CreateVisitorForInstanceBindabledMethod(Type type, MethodInfo method)
		{
#if DEBUG_JSON_CONVERTER
			Debug.WriteLine("CrystalJsonConverter.CreateVisitorForInstanceBindabledMethod(" + type + ", " + method + ")");
#endif
			Contract.Debug.Requires(type != null && method != null && !method.IsStatic);

			var prms = method.GetParameters();
			// on accepte: (TValue value).JsonPack(CrystalJsonSettings settings, ICrystalJsonTypeResolver resolver)
			if (prms.Length != 2) throw CrystalJson.Errors.Serialization_InstanceJsonPackMethodInvalidSignature(type, method);
			if (prms[0].ParameterType != typeof(CrystalJsonSettings)) throw new InvalidOperationException($"First parameter must be a {nameof(CrystalJsonSettings)}"); //TODO
			if (prms[1].ParameterType != typeof(ICrystalJsonTypeResolver)) throw new InvalidOperationException($"Second parameter must be a {nameof(ICrystalJsonTypeResolver)}"); //TODO

			var prmWriter = Expression.Parameter(typeof(CrystalJsonWriter), "writer");
			var prmValue = Expression.Parameter(typeof(object), "value");
			var prmDeclaringType = Expression.Parameter(typeof(Type), "declaringType"); //NOT USED?
			var prmRuntimeType = Expression.Parameter(typeof(Type), "runtimeType"); //NOT USED?
			var varSettings = Expression.Property(prmWriter, nameof(CrystalJsonWriter.Settings));
			var varResolver = Expression.Property(prmWriter, nameof(CrystalJsonWriter.Resolver));

			// il faut caster l'objet dans le type target
			var castedValue = prmValue.CastFromObject(type);
			// body = (instance)?.JsonPack(settings, resolver) ?? JsonNull.Null
			Expression body = Expression.Call(castedValue, method, varSettings, varResolver);

			// body = ((instance)?.JsonPack(settings, resolver) ?? JsonNull.Null).JsonSerialize(writer)

			body = Expression.Condition(
				Expression.Equal(prmValue, Expression.Default(type)),
				Expression.Constant(JsonNull.Missing, typeof(JsonValue)),
				body
			);

			body = Expression.Call(body, nameof(JsonValue.JsonSerialize), null, prmWriter);

			var lambda = Expression.Lambda<CrystalJsonTypeVisitor>(body, new[] { prmValue, prmDeclaringType, prmRuntimeType, prmWriter });
			return lambda.Compile();
		}

		#endregion

		#region Custom Serializers...

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void VisitValue<T>(T value, CrystalJsonWriter writer)
		{
			#region <JIT_HACK>
#if !DEBUG
			if (typeof(T) == typeof(bool)) { writer.WriteValue((bool)(object)value); return; }
			if (typeof(T) == typeof(char)) { writer.WriteValue((char)(object)value); return; }
			if (typeof(T) == typeof(int)) { writer.WriteValue((int) (object) value); return; }
			if (typeof(T) == typeof(long)) { writer.WriteValue((long)(object)value); return; }
			if (typeof(T) == typeof(uint)) { writer.WriteValue((uint)(object)value); return; }
			if (typeof(T) == typeof(ulong)) { writer.WriteValue((ulong)(object)value); return; }
			if (typeof(T) == typeof(double)) { writer.WriteValue((double)(object)value); return; }
			if (typeof(T) == typeof(float)) { writer.WriteValue((float)(object)value); return; }
			if (typeof(T) == typeof(Guid)) { writer.WriteValue((Guid)(object)value); return; }
			if (typeof(T) == typeof(TimeSpan)) { writer.WriteValue((TimeSpan)(object)value); return; }
			if (typeof(T) == typeof(DateTime)) { writer.WriteValue((DateTime)(object)value); return; }
			if (typeof(T) == typeof(DateTimeOffset)) { writer.WriteValue((DateTimeOffset)(object)value); return; }
			if (typeof(T) == typeof(NodaTime.Duration)) { writer.WriteValue((NodaTime.Duration)(object)value); return; }
			if (typeof(T) == typeof(NodaTime.Instant)) { writer.WriteValue((NodaTime.Instant)(object)value); return; }

			if (typeof(T) == typeof(bool?)) { writer.WriteValue((bool?)(object)value); return; }
			if (typeof(T) == typeof(char?)) { writer.WriteValue((char?)(object)value); return; }
			if (typeof(T) == typeof(int?)) { writer.WriteValue((int?)(object)value); return; }
			if (typeof(T) == typeof(long?)) { writer.WriteValue((long?)(object)value); return; }
			if (typeof(T) == typeof(uint?)) { writer.WriteValue((uint?)(object)value); return; }
			if (typeof(T) == typeof(ulong?)) { writer.WriteValue((ulong?)(object)value); return; }
			if (typeof(T) == typeof(double?)) { writer.WriteValue((double?)(object)value); return; }
			if (typeof(T) == typeof(float?)) { writer.WriteValue((float?)(object)value); return; }
			if (typeof(T) == typeof(Guid?)) { writer.WriteValue((Guid?)(object)value); return; }
			if (typeof(T) == typeof(TimeSpan?)) { writer.WriteValue((TimeSpan?)(object)value); return; }
			if (typeof(T) == typeof(DateTime?)) { writer.WriteValue((DateTime?)(object)value); return; }
			if (typeof(T) == typeof(DateTimeOffset?)) { writer.WriteValue((DateTimeOffset?)(object)value); return; }
			if (typeof(T) == typeof(NodaTime.Duration?)) { writer.WriteValue((NodaTime.Duration?)(object)value); return; }
			if (typeof(T) == typeof(NodaTime.Instant?)) { writer.WriteValue((NodaTime.Instant?)(object)value); return; }
#endif
			#endregion </JIT_HACK>

			VisitValue(value, typeof(T), writer);
		}

		/// <summary>Sérialise une valeur (primitive, valuetype, classe, struct, ...)</summary>
		/// <param name="value">Valeur à sérialiser (de n'importe quel type)</param>
		/// <param name="declaredType">Type du member qui déclare cette valeur (ou null si top level ou non connu)</param>
		/// <param name="writer">Contexte de la sérialisation</param>
		public static void VisitValue(object? value, Type declaredType, CrystalJsonWriter writer)
		{
			// On élimine tout de suite les cas les plus courants...
			if (value == null)
			{ // "null"
				writer.WriteNull(); // "null"
				return;
			}

			if (value is string s) { writer.WriteValue(s); return; }

			Type type = value.GetType();
			var visitor = GetVisitorForType(type);
			if (visitor == null) throw CrystalJson.Errors.Serialization_DoesNotKnowHowToSerializeType(type);
			visitor(value, declaredType, type, writer);
		}

		#region Arrays, Lists, Sequences, ...

		/// <summary>Sérialise une liste d'objet ou de valeurs (Array, IEnumerable, ...)</summary>
		private static void VisitStringArrayInternal(object? value, Type declaredType, Type? runtimeType, CrystalJsonWriter writer)
		{
			if (value == null)
			{
				writer.WriteNull();
				return;
			}

			var array = (string[])value;
			if (array.Length == 0)
			{
				writer.WriteEmptyArray();
				return;
			}

			// dans notre cas, on contient des string donc il n'y a pas de risque d'appels récursifs!
			var state = writer.BeginArray();

			// premier élément
			writer.WriteHeadSeparator();
			writer.WriteValue(array[0]);
			// reste de la liste
			for (int i = 1; i < array.Length; i++)
			{
				writer.WriteTailSeparator();
				writer.WriteValue(array[i]);
			}

			writer.EndArray(state);
		}

		/// <summary>Sérialise une liste d'objet ou de valeurs (Array, IEnumerable, ...)</summary>
		public static void VisitEnumerableOfString(IEnumerable<string>? enumerable, CrystalJsonWriter writer)
		{
			if (enumerable == null)
			{
				writer.WriteNull();
				return;
			}

			writer.MarkVisited(enumerable);

			// les valeurs sont déjà des string
			var enmr = enumerable.GetEnumerator();
			try
			{
				if (enmr.MoveNext())
				{
					var state = writer.BeginArray();
					writer.WriteHeadSeparator();
					writer.WriteValue(enmr.Current);

					while (enmr.MoveNext())
					{
						writer.WriteFieldSeparator();
						writer.WriteValue(enmr.Current);
					}
					writer.EndArray(state);
				}
				else
				{
					writer.WriteEmptyArray();
				}
			}
			finally
			{
				enmr.Dispose();
			}

			writer.Leave(enumerable);
		}

		private static CrystalJsonTypeVisitor CompileGenericVisitorMethod(string methodName, string visitorName, Type type)
		{
			var method = typeof(CrystalJsonVisitor).GetMethod(methodName, BindingFlags.Static | BindingFlags.NonPublic);
			Contract.Debug.Assert(method != null);
			method = method!.MakeGenericMethod(type);
			var prms = new[] {
				Expression.Parameter(typeof(object), "v"),
				Expression.Parameter(typeof(Type), "t"),
				Expression.Parameter(typeof(Type), "r"),
				Expression.Parameter(typeof(CrystalJsonWriter), "w")
			};
			// ReSharper disable once CoVariantArrayConversion
			var call = Expression.Call(null, method, prms);
			var lambda = Expression.Lambda<CrystalJsonTypeVisitor>(call, "CrystalJsonVisitor.<" + type.GetFriendlyName() + ">_" + visitorName, true, prms);
			return lambda.Compile();
		}

		private static void VisitArrayInternal<T>(object? value, Type declaredType, Type? runtimeType, CrystalJsonWriter writer)
		{
			if (value == null)
			{
				writer.WriteNull();
				return;
			}

			var array = (T[])value;

			if (array.Length == 0)
			{
				writer.WriteEmptyArray();
				return;
			}

			var elemType = typeof(T);
			var visitor = GetVisitorForType(elemType, atRuntime: false);

			// dans notre cas, on contient des string donc il n'y a pas de risque d'appels récursifs!
			var state = writer.BeginArray();

			// premier élément
			writer.WriteHeadSeparator();
			visitor(array[0], elemType, null, writer);
			//VisitValue(array[0], elemType, writer);

			// reste de la liste
			for (int i = 1; i < array.Length; i++)
			{
				writer.WriteTailSeparator();
				//VisitValue(array[i], elemType, writer);
				visitor(array[i], elemType, null, writer);
			}

			writer.EndArray(state);
		}

		//REVIEW: NOT USED!
		private static void VisitListInternal<T>(object? value, Type declaredType, Type? runtimeType, CrystalJsonWriter writer)
		{
			if (value == null)
			{
				writer.WriteNull();
				return;
			}

			var list = (IList<T>)value;

			int count = list.Count;
			if (count == 0)
			{
				writer.WriteEmptyArray();
			}
			else
			{
				// dans notre cas, on contient des string donc il n'y a pas de risque d'appels récursifs!
				var state = writer.BeginArray();

				// premier élément
				writer.WriteHeadSeparator();
				VisitValue<T>(list[0], writer);

				// reste de la liste
				for (int i = 1; i < count; i++)
				{
					writer.WriteTailSeparator();
					VisitValue<T>(list[i], writer);
				}
				writer.EndArray(state);
			}
		}

		public static void VisitArray<T>(T[]? array, CrystalJsonWriter writer)
		{
			if (array == null)
			{
				writer.WriteNull();
				return;
			}

			if (array.Length == 0)
			{
				writer.WriteEmptyArray();
				return;
			}

			// dans notre cas, on contient des string donc il n'y a pas de risque d'appels récursifs!
			var state = writer.BeginArray();

			// premier élément
			writer.WriteHeadSeparator();
			VisitValue<T>(array[0], writer);

			// reste de la liste
			for (int i = 1; i < array.Length; i++)
			{
				writer.WriteTailSeparator();
				VisitValue<T>(array[i], writer);
			}

			writer.EndArray(state);
		}

		/// <summary>Sérialise une liste d'objet ou de valeurs (Array, IEnumerable, ...)</summary>
		public static void VisitEnumerable(IEnumerable? enumerable, Type elementType, CrystalJsonWriter writer)
		{
			if (enumerable == null)
			{
				writer.WriteNull();
				return;
			}

			writer.MarkVisited(enumerable);

			// il va falloir convertir les valeur en string ...
			var iter = enumerable.GetEnumerator();

			try
			{
				if (iter.MoveNext())
				{
					var state = writer.BeginArray();
					// premier élément
					writer.WriteHeadSeparator();
					VisitValue(iter.Current, elementType, writer);
					// reste de la liste
					while (iter.MoveNext())
					{
						writer.WriteTailSeparator();
						VisitValue(iter.Current, elementType, writer);
					}
					writer.EndArray(state);
				}
				else
				{
					writer.WriteEmptyArray();
				}
			}
			finally
			{
				// tous les IEnumerator ne sont pas disposables, mais tous les IEnumerator<T> le sont!
				(iter as IDisposable)?.Dispose();
			}

			writer.Leave(enumerable);
		}

		#endregion

		#region Dictionaries...

		/// <summary>Sérialise un Dictionary&lt;string, string&gt;</summary>
		public static void VisitStringDictionary(ICollection<KeyValuePair<string, string>>? dictionary, CrystalJsonWriter writer)
		{
			if (dictionary == null)
			{ // "null" ou "{}"
				writer.WriteNull(); // "null"
				return;
			}

			if (dictionary.Count == 0)
			{ // vide => "{}"
				writer.WriteEmptyObject(); // "{}"
				return;
			}

			writer.MarkVisited(dictionary);
			var state = writer.BeginObject();

			// le cas le plus facile à gérer est lors que key et value sont des strings
			foreach (var kvp in dictionary)
			{
				writer.WriteField(kvp.Key, kvp.Value);
			}

			// done!
			writer.EndObject(state); // "}"
			writer.Leave(dictionary);
		}

		/// <summary>Sérialise un Dictionnaire&lt;string, object&gt;</summary>
		public static void VisitGenericDictionary<TValue>(Dictionary<string, TValue>? dictionary, CrystalJsonWriter writer)
		{
			if (dictionary == null)
			{ // "null" ou "{}"
				writer.WriteNull(); // "null"
				return;
			}

			if (dictionary.Count == 0)
			{ // vide => "{}"
				writer.WriteEmptyObject(); // "{}"
				return;
			}

			writer.MarkVisited(dictionary);
			var state = writer.BeginObject();

			// le cas le plus facile à gérer est lors que key et value sont des strings
			foreach (var kvp in dictionary)
			{
				writer.WriteUnsafeName(kvp.Key);
				VisitValue<TValue>(kvp.Value, writer);
			}
			// done!
			writer.EndObject(state); // "}"
			writer.Leave(dictionary);
		}

		/// <summary>Sérialise un Dictionnaire&lt;string, object&gt;</summary>
		public static void VisitGenericObjectDictionary(ICollection<KeyValuePair<string, object>>? dictionary, CrystalJsonWriter writer)
		{
			if (dictionary == null)
			{ // "null" ou "{}"
				writer.WriteNull(); // "null"
				return;
			}

			if (dictionary.Count == 0)
			{ // vide => "{}"
				writer.WriteEmptyObject(); // "{}"
				return;
			}

			writer.MarkVisited(dictionary);
			var state = writer.BeginObject();

			// le cas le plus facile à gérer est lors que key et value sont des strings
			foreach (var kvp in dictionary)
			{
				writer.WriteUnsafeName(kvp.Key);
				VisitValue(kvp.Value, typeof(object), writer);
			}
			// done!
			writer.EndObject(state); // "}"
			writer.Leave(dictionary);
		}

		/// <summary>Sérialise un dictionnaire de key/value</summary>
		public static void VisitDictionary(IDictionary? dictionary, Type keyType, Type valueType, CrystalJsonWriter writer)
		{
			if (dictionary == null)
			{ // "null" ou "{}"
				writer.WriteNull(); // "null"
				return;
			}

			if (dictionary.Count == 0)
			{ // vide => "{}"
				writer.WriteEmptyObject(); // "{}"
				return;
			}

			writer.MarkVisited(dictionary);
			var state = writer.BeginObject();

			bool keyIsString = keyType == typeof(string);
			bool valueIsString = valueType == typeof(string);

			// il va falloir convertir les key et/ou values :(
			foreach (DictionaryEntry entry in dictionary)
			{
				var name = keyIsString ? (entry.Key as string) : TypeHelper.ConvertKeyToString(entry.Key);
				if (name == null) continue; // not supported!
				// key
				writer.WriteUnsafeName(name);
				// value
				if (valueIsString)
					writer.WriteValue(entry.Value as string);
				else
					VisitValue(entry.Value, valueType, writer);
			}

			// done!
			writer.EndObject(state); // "}"
			writer.Leave(dictionary);
		}

		/// <summary>Sérialise un dictionnaire de key/value</summary>
		public static void VisitDictionary_StringKey(IDictionary? dictionary, Type valueType, CrystalJsonTypeVisitor visitor, CrystalJsonWriter writer)
		{
			if (dictionary == null)
			{ // "null" ou "{}"
				writer.WriteNull(); // "null"
				return;
			}

			if (dictionary.Count == 0)
			{ // vide => "{}"
				writer.WriteEmptyObject(); // "{}"
				return;
			}

			writer.MarkVisited(dictionary);
			var state = writer.BeginObject();

			// il va falloir convertir les key et/ou values :(
			foreach (DictionaryEntry entry in dictionary)
			{
				// key
				writer.WriteUnsafeName((string) entry.Key);
				// value
				visitor(entry.Value, valueType, entry.Value?.GetType() ?? valueType, writer);
			}

			// done!
			writer.EndObject(state); // "}"
			writer.Leave(dictionary);
		}

		/// <summary>Sérialise un dictionnaire de key/value</summary>
		public static void VisitDictionary_StringKeyAndValue(IDictionary<string, string>? dictionary, CrystalJsonWriter writer)
		{
			if (dictionary == null)
			{ // "null" ou "{}"
				writer.WriteNull(); // "null"
				return;
			}

			if (dictionary.Count == 0)
			{ // vide => "{}"
				writer.WriteEmptyObject(); // "{}"
				return;
			}

			writer.MarkVisited(dictionary);
			var state = writer.BeginObject();

			// il va falloir convertir les key et/ou values :(
			foreach (var kv in dictionary)
			{
				// key
				writer.WriteUnsafeName(kv.Key);
				// value
				writer.WriteValue(kv.Value);
			}

			// done!
			writer.EndObject(state); // "}"
			writer.Leave(dictionary);
		}

		/// <summary>Sérialise un dictionnaire de key/value</summary>
		public static void VisitDictionary_Int32Key(IDictionary? dictionary, Type valueType, CrystalJsonTypeVisitor visitor, CrystalJsonWriter writer)
		{
			if (dictionary == null)
			{ // "null" ou "{}"
				writer.WriteNull(); // "null"
				return;
			}

			if (dictionary.Count == 0)
			{ // vide => "{}"
				writer.WriteEmptyObject(); // "{}"
				return;
			}

			writer.MarkVisited(dictionary);
			var state = writer.BeginObject();

			// il va falloir convertir les key et/ou values :(
			foreach (DictionaryEntry entry in dictionary)
			{
				// key
				writer.WriteUnsafeName((int) entry.Key);
				// value
				visitor(entry.Value, valueType, entry.Value?.GetType() ?? valueType, writer);
			}

			// done!
			writer.EndObject(state); // "}"
			writer.Leave(dictionary);
		}

		/// <summary>Sérialise un dictionnaire de key/value</summary>
		public static void VisitDictionary_Int32Key_StringValue(IDictionary<int, string>? dictionary, CrystalJsonWriter writer)
		{
			if (dictionary == null)
			{ // "null" ou "{}"
				writer.WriteNull(); // "null"
				return;
			}

			if (dictionary.Count == 0)
			{ // vide => "{}"
				writer.WriteEmptyObject(); // "{}"
				return;
			}

			writer.MarkVisited(dictionary);
			var state = writer.BeginObject();

			// il va falloir convertir les key et/ou values :(
			foreach (var entry in dictionary)
			{
				// key
				writer.WriteUnsafeName(entry.Key);
				// value
				writer.WriteValue(entry.Value);
			}

			// done!
			writer.EndObject(state); // "}"
			writer.Leave(dictionary);
		}

		#endregion

		#region VarTuples...

		[Pure]
		public static CrystalJsonTypeVisitor CreateVisitorForSTupleType(Type type)
		{
			Contract.Debug.Requires(type != null && typeof(IVarTuple).IsAssignableFrom(type));

			// pour les STuple<..> on peut utiliser des versions plus efficaces
			if (type.IsValueType && type.IsGenericType && type.Name.StartsWith(nameof(STuple) + "`", StringComparison.Ordinal))
			{
				var args = type.GetGenericArguments();
				if (args.Length <= 6)
				{
					// récupère la méthode VisitSTupleX correspondante
					MethodInfo m;
					switch (args.Length)
					{
						case 1: m = typeof(CrystalJsonVisitor).GetMethod(nameof(VisitSTuple1))!; break;
						case 2: m = typeof(CrystalJsonVisitor).GetMethod(nameof(VisitSTuple2))!; break;
						case 3: m = typeof(CrystalJsonVisitor).GetMethod(nameof(VisitSTuple3))!; break;
						case 4: m = typeof(CrystalJsonVisitor).GetMethod(nameof(VisitSTuple4))!; break;
						case 5: m = typeof(CrystalJsonVisitor).GetMethod(nameof(VisitSTuple5))!; break;
						case 6: m = typeof(CrystalJsonVisitor).GetMethod(nameof(VisitSTuple6))!; break;
						default: throw new InvalidOperationException();
					}
					Contract.Debug.Assert(m != null, "Missing method to serialize generic tuple");
					// génère le delegate avec les bons types génériques
					return (CrystalJsonTypeVisitor)m.MakeGenericMethod(args).CreateDelegate(typeof(CrystalJsonTypeVisitor));
				}
			}
			// pour le reste, on passe par une version plus lente mais qui marche avec tous les types
			return VisitSTupleGeneric;
		}

		/// <summary>Sérialise un tuple dont le type n'est pas connu à l'avance</summary>
		public static void VisitSTupleGeneric(object? value, Type declaredType, Type? runtimeType, CrystalJsonWriter writer)
		{
			if (value == null)
			{
				writer.WriteNull();
				return;
			}

			var tuple = (IVarTuple)value;
			int n = tuple.Count;
			if (n == 0)
			{
				writer.WriteEmptyArray();
				return;
			}

			var state = writer.BeginArray();
			// Head
			writer.WriteHeadSeparator();
			VisitValue(tuple[0], writer);
			// Tail
			for (int i = 1; i < n; i++)
			{
				writer.WriteFieldSeparator();
				VisitValue(tuple[i], writer);
			}
			writer.EndArray(state);
		}

		/// <summary>Sérialise une tuple de taille 1</summary>
		public static void VisitSTuple1<T1>(object? tuple, Type declaredType, Type? runtimeType, CrystalJsonWriter writer)
		{
			if (tuple == null) { writer.WriteNull(); return; }
			var t = (STuple<T1>)tuple;
			var state = writer.BeginInlineArray();
			writer.WriteHeadSeparator();
			writer.VisitValue(t.Item1);
			writer.EndInlineArray(state);
		}

		/// <summary>Sérialise une tuple de taille 2</summary>
		public static void VisitSTuple2<T1, T2>(object? tuple, Type declaredType, Type? runtimeType, CrystalJsonWriter writer)
		{
			if (tuple == null) { writer.WriteNull(); return; }
			var t = (STuple<T1, T2>)tuple;
			var state = writer.BeginArray();
			writer.WriteHeadSeparator();
			writer.VisitValue(t.Item1);
			writer.WriteTailSeparator();
			writer.VisitValue(t.Item2);
			writer.EndArray(state);
		}

		/// <summary>Sérialise une tuple de taille 3</summary>
		public static void VisitSTuple3<T1, T2, T3>(object? tuple, Type declaredType, Type? runtimeType, CrystalJsonWriter writer)
		{
			if (tuple == null) { writer.WriteNull(); return; }
			var t = (STuple<T1, T2, T3>)tuple;
			var state = writer.BeginArray();
			writer.WriteHeadSeparator();
			writer.VisitValue(t.Item1);
			writer.WriteTailSeparator();
			writer.VisitValue(t.Item2);
			writer.WriteTailSeparator();
			writer.VisitValue(t.Item3);
			writer.EndArray(state);
		}

		/// <summary>Sérialise une tuple de taille 4</summary>
		public static void VisitSTuple4<T1, T2, T3, T4>(object? tuple, Type declaredType, Type? runtimeType, CrystalJsonWriter writer)
		{
			if (tuple == null) { writer.WriteNull(); return; }
			var t = (STuple<T1, T2, T3, T4>)tuple;
			var state = writer.BeginArray();
			writer.WriteHeadSeparator();
			writer.VisitValue(t.Item1);
			writer.WriteTailSeparator();
			writer.VisitValue(t.Item2);
			writer.WriteTailSeparator();
			writer.VisitValue(t.Item3);
			writer.WriteTailSeparator();
			writer.VisitValue(t.Item4);
			writer.EndArray(state);
		}

		/// <summary>Sérialise une tuple de taille 5</summary>
		public static void VisitSTuple5<T1, T2, T3, T4, T5>(object? tuple, Type declaredType, Type? runtimeType, CrystalJsonWriter writer)
		{
			if (tuple == null) { writer.WriteNull(); return; }
			var t = (STuple<T1, T2, T3, T4, T5>)tuple;
			var state = writer.BeginArray();
			writer.WriteHeadSeparator();
			writer.VisitValue(t.Item1);
			writer.WriteTailSeparator();
			writer.VisitValue(t.Item2);
			writer.WriteTailSeparator();
			writer.VisitValue(t.Item3);
			writer.WriteTailSeparator();
			writer.VisitValue(t.Item4);
			writer.WriteTailSeparator();
			writer.VisitValue(t.Item5);
			writer.EndArray(state);
		}

		/// <summary>Sérialise une tuple de taille 5</summary>
		public static void VisitSTuple6<T1, T2, T3, T4, T5, T6>(object? tuple, Type declaredType, Type? runtimeType, CrystalJsonWriter writer)
		{
			if (tuple == null) { writer.WriteNull(); return; }
			var t = (STuple<T1, T2, T3, T4, T5, T6>)tuple;
			var state = writer.BeginArray();
			writer.WriteHeadSeparator();
			writer.VisitValue(t.Item1);
			writer.WriteTailSeparator();
			writer.VisitValue(t.Item2);
			writer.WriteTailSeparator();
			writer.VisitValue(t.Item3);
			writer.WriteTailSeparator();
			writer.VisitValue(t.Item4);
			writer.WriteTailSeparator();
			writer.VisitValue(t.Item5);
			writer.WriteTailSeparator();
			writer.VisitValue(t.Item6);
			writer.EndArray(state);
		}

		#endregion

		#region ValueTuples...

#if !NETFRAMEWORK && !NETSTANDARD

		[Pure]
		public static CrystalJsonTypeVisitor CreateVisitorForITupleType(Type type)
		{
			Contract.Debug.Requires(type != null);
			Contract.Debug.Requires(typeof(System.Runtime.CompilerServices.ITuple).IsAssignableFrom(type));

			// pour les STuple<..> on peut utiliser des versions plus efficaces
			if (type.IsValueType && type.IsGenericType && type.Name.StartsWith(nameof(ValueTuple) + "`", StringComparison.Ordinal))
			{
				var args = type.GetGenericArguments();
				if (args.Length <= 6)
				{
					// récupère la méthode VisitSTupleX correspondante
					MethodInfo m;
					switch (args.Length)
					{
						case 1: m = typeof(CrystalJsonVisitor).GetMethod(nameof(VisitValueTuple1))!; break;
						case 2: m = typeof(CrystalJsonVisitor).GetMethod(nameof(VisitValueTuple2))!; break;
						case 3: m = typeof(CrystalJsonVisitor).GetMethod(nameof(VisitValueTuple3))!; break;
						case 4: m = typeof(CrystalJsonVisitor).GetMethod(nameof(VisitValueTuple4))!; break;
						case 5: m = typeof(CrystalJsonVisitor).GetMethod(nameof(VisitValueTuple5))!; break;
						case 6: m = typeof(CrystalJsonVisitor).GetMethod(nameof(VisitValueTuple6))!; break;
						case 7: m = typeof(CrystalJsonVisitor).GetMethod(nameof(VisitValueTuple7))!; break;
						//TODO: ValueTuple<...., TRest> ?
						default: throw new InvalidOperationException();
					}
					Contract.Debug.Assert(m != null, "Missing method to serialize generic tuple");
					// génère le delegate avec les bons types génériques
					return (CrystalJsonTypeVisitor) m.MakeGenericMethod(args).CreateDelegate(typeof(CrystalJsonTypeVisitor));
				}
			}
			// pour le reste, on passe par une version plus lente mais qui marche avec tous les types
			return VisitITupleGeneric;
		}

		/// <summary>Sérialise un tuple dont le type n'est pas connu à l'avance</summary>
		public static void VisitITupleGeneric(object? value, Type declaredType, Type? runtimeType, CrystalJsonWriter writer)
		{
			if (value == null)
			{
				writer.WriteNull();
				return;
			}

			var tuple = (System.Runtime.CompilerServices.ITuple) value;
			int n = tuple.Length;
			if (n == 0)
			{
				writer.WriteEmptyArray();
				return;
			}

			var state = writer.BeginArray();
			// Head
			writer.WriteHeadSeparator();
			VisitValue(tuple[0], writer);
			// Tail
			for (int i = 1; i < n; i++)
			{
				writer.WriteFieldSeparator();
				VisitValue(tuple[i], writer);
			}
			writer.EndArray(state);
		}

#endif

		/// <summary>Sérialise une tuple de taille 1</summary>
		public static void VisitValueTuple1<T1>(object? tuple, Type declaredType, Type? runtimeType, CrystalJsonWriter writer)
		{
			if (tuple == null) { writer.WriteNull(); return; }
			var t = (ValueTuple<T1>) tuple;
			var state = writer.BeginInlineArray();
			writer.WriteHeadSeparator();
			writer.VisitValue(t.Item1);
			writer.EndInlineArray(state);
		}

		/// <summary>Sérialise une tuple de taille 2</summary>
		public static void VisitValueTuple2<T1, T2>(object? tuple, Type declaredType, Type? runtimeType, CrystalJsonWriter writer)
		{
			if (tuple == null) { writer.WriteNull(); return; }
			var t = (ValueTuple<T1, T2>) tuple;
			var state = writer.BeginArray();
			writer.WriteHeadSeparator();
			writer.VisitValue(t.Item1);
			writer.WriteTailSeparator();
			writer.VisitValue(t.Item2);
			writer.EndArray(state);
		}

		/// <summary>Sérialise une tuple de taille 3</summary>
		public static void VisitValueTuple3<T1, T2, T3>(object? tuple, Type declaredType, Type? runtimeType, CrystalJsonWriter writer)
		{
			if (tuple == null) { writer.WriteNull(); return; }
			var t = (ValueTuple<T1, T2, T3>) tuple;
			var state = writer.BeginArray();
			writer.WriteHeadSeparator();
			writer.VisitValue(t.Item1);
			writer.WriteTailSeparator();
			writer.VisitValue(t.Item2);
			writer.WriteTailSeparator();
			writer.VisitValue(t.Item3);
			writer.EndArray(state);
		}

		/// <summary>Sérialise une tuple de taille 4</summary>
		public static void VisitValueTuple4<T1, T2, T3, T4>(object? tuple, Type declaredType, Type? runtimeType, CrystalJsonWriter writer)
		{
			if (tuple == null) { writer.WriteNull(); return; }
			var t = (ValueTuple<T1, T2, T3, T4>) tuple;
			var state = writer.BeginArray();
			writer.WriteHeadSeparator();
			writer.VisitValue(t.Item1);
			writer.WriteTailSeparator();
			writer.VisitValue(t.Item2);
			writer.WriteTailSeparator();
			writer.VisitValue(t.Item3);
			writer.WriteTailSeparator();
			writer.VisitValue(t.Item4);
			writer.EndArray(state);
		}

		/// <summary>Sérialise une tuple de taille 5</summary>
		public static void VisitValueTuple5<T1, T2, T3, T4, T5>(object? tuple, Type declaredType, Type? runtimeType, CrystalJsonWriter writer)
		{
			if (tuple == null) { writer.WriteNull(); return; }
			var t = (ValueTuple<T1, T2, T3, T4, T5>) tuple;
			var state = writer.BeginArray();
			writer.WriteHeadSeparator();
			writer.VisitValue(t.Item1);
			writer.WriteTailSeparator();
			writer.VisitValue(t.Item2);
			writer.WriteTailSeparator();
			writer.VisitValue(t.Item3);
			writer.WriteTailSeparator();
			writer.VisitValue(t.Item4);
			writer.WriteTailSeparator();
			writer.VisitValue(t.Item5);
			writer.EndArray(state);
		}

		/// <summary>Sérialise une tuple de taille 6</summary>
		public static void VisitValueTuple6<T1, T2, T3, T4, T5, T6>(object? tuple, Type declaredType, Type? runtimeType, CrystalJsonWriter writer)
		{
			if (tuple == null) { writer.WriteNull(); return; }
			var t = (ValueTuple<T1, T2, T3, T4, T5, T6>) tuple;
			var state = writer.BeginArray();
			writer.WriteHeadSeparator();
			writer.VisitValue(t.Item1);
			writer.WriteTailSeparator();
			writer.VisitValue(t.Item2);
			writer.WriteTailSeparator();
			writer.VisitValue(t.Item3);
			writer.WriteTailSeparator();
			writer.VisitValue(t.Item4);
			writer.WriteTailSeparator();
			writer.VisitValue(t.Item5);
			writer.WriteTailSeparator();
			writer.VisitValue(t.Item6);
			writer.EndArray(state);
		}

		/// <summary>Sérialise une tuple de taille 7</summary>
		public static void VisitValueTuple7<T1, T2, T3, T4, T5, T6, T7>(object? tuple, Type declaredType, Type? runtimeType, CrystalJsonWriter writer)
		{
			if (tuple == null) { writer.WriteNull(); return; }
			var t = (ValueTuple<T1, T2, T3, T4, T5, T6, T7>) tuple;
			var state = writer.BeginArray();
			writer.WriteHeadSeparator();
			writer.VisitValue(t.Item1);
			writer.WriteTailSeparator();
			writer.VisitValue(t.Item2);
			writer.WriteTailSeparator();
			writer.VisitValue(t.Item3);
			writer.WriteTailSeparator();
			writer.VisitValue(t.Item4);
			writer.WriteTailSeparator();
			writer.VisitValue(t.Item5);
			writer.WriteTailSeparator();
			writer.VisitValue(t.Item6);
			writer.WriteTailSeparator();
			writer.VisitValue(t.Item7);
			writer.EndArray(state);
		}

		#endregion

		/// <summary>Sérialise une classe implémentant IJsonSerializable</summary>
		public static void VisitJsonSerializable(object? value, Type declaringType, Type? runtimeType, CrystalJsonWriter writer)
		{
			if (value == null)
				writer.WriteNull(); // "null"
			else
				((IJsonSerializable)value).JsonSerialize(writer);
		}

		/// <summary>Sérialise une classe implémentant IJsonBindable</summary>
		public static void VisitJsonPackable(object? value, Type declaringType, Type? runtimeType, CrystalJsonWriter writer)
		{
			if (value == null)
				writer.WriteNull(); // "null"
			else
			{
				var json = ((IJsonPackable) value).JsonPack(writer.Settings, writer.Resolver);
				json.JsonSerialize(writer);
			}
		}

		/// <summary>Sérialise une classe ou une struct</summary>
		public static void VisitCustomClassOrStruct(object? value, Type declaringType, Type? runtimeType, CrystalJsonWriter writer)
		{
			if (value == null)
			{
				writer.WriteNull(); // "null"
				return;
			}

			writer.MarkVisited(value);
			var state = writer.BeginObject();

			// récupère les infos sur l'objet
			runtimeType ??= value.GetType();
			var typeDef = writer.Resolver.ResolveJsonType(runtimeType);
			if (typeDef == null)
			{ // uhoh, pas normal du tout
				throw CrystalJson.Errors.Serialization_CouldNotResolveTypeDefinition(runtimeType);
			}

			// traiement des membres
			bool discardDefaults = writer.DiscardDefaults;
			bool discardNulls = writer.DiscardNulls;

			if (!writer.DiscardClass)
			{
				// il faut ajouter l'attribut class si 
				// => le type au runtime n'est pas le même que le type déclaré. Ex: class A { IFoo Foo; } / class B : IFoo { } / new A() { Foo = new B() }

				if (typeDef.RequiresClassAttribute || (declaringType != null && runtimeType != declaringType && !typeDef.IsAnonymousType && !declaringType.IsNullableType() && typeDef.BaseType == null))
				{ // il faut préciser la class !
					//TODO: auto-aliasing
					writer.WriteField(JsonTokens.CustomClassAttribute, typeDef.ClassId);
				}
			}

			foreach (var member in typeDef.Members)
			{
				var child = member.Getter(value);
				if (discardDefaults)
				{ // ignore 0, false, DateTime.MinValue, ...
					if (member.IsDefaultValue(child)) continue;
				}
				else if (discardNulls)
				{ // ignore uniquement null (classes, Nullable<T>, ...)
					if (child == null) continue;
				}

				writer.WriteName(member.Name);
				if (member.Attributes != null)
				{
					var tmp = writer.PushAttributes(member.Attributes);
					member.Visitor(child, member.Type, null, writer);
					writer.PopAttributes(tmp);
				}
				else
				{
					member.Visitor(child, member.Type, null, writer);
				}
			}

			writer.EndObject(state); // "}"
			writer.Leave(value);
		}

		/// <summary>Helper appelé au runtime pour sérialiser un membre d'une classe dont le type réel n'est pas connu à l'avance (comme 'object' ou une classe abstraite)</summary>
		/// <param name="value">Valeur actuelle de l'objet</param>
		/// <param name="declaringType">Type (object, ou abstrait) tel qu'il est déclaré au niveau du parent</param>
		/// <param name="runtimeType">Type réel de l'objet au runtime</param>
		/// <param name="writer">Contexte de la sérialisation</param>
		private static void VisitObjectAtRuntime(object? value, Type declaringType, Type? runtimeType, CrystalJsonWriter writer)
		{
			// si on arrive ici, c'est qu'un membre d'une struct/class à pour type "object",
			// et donc qu'on est obligé de regarder le vrai type du champ au runtime!
			// => donc on doit avoir type == typeof(object)

			// si value.GetType() est aussi object, c'est qu'on avait vraiment un "new object()", on sérialiser "{ }".
			// sinon, on chaîne vers le writer correspondant au vrai type de l'objet.

			if (value == null)
			{
				writer.WriteNull(); // "null"
				return;
			}

			runtimeType ??= value.GetType();
			if (runtimeType == typeof(object))
			{ // c'était vraiment un object !
				writer.WriteEmptyObject();
				return;
			}

			// détermine le bon writer
			var visitor = GetVisitorForType(runtimeType, atRuntime: true); // atRuntime = true, pour éviter une boucle infinie (si false, ca retourne un callback qui va nous rappeler directement !)
			if (visitor == null) throw CrystalJson.Errors.Serialization_DoesNotKnowHowToSerializeType(runtimeType);

			// chaînage
			visitor(value, declaringType, runtimeType, writer);
		}

		/// <summary>Helper appelé au runtime pour sérialiser un membre d'une classe dont le type déclaré est une interface</summary>
		/// <param name="value">Valeur actuelle de l'objet</param>
		/// <param name="declaringType">Type (interface) tel qu'il est déclaré au niveau du parent</param>
		/// <param name="runtimeType">Type réel de l'objet au runtime</param>
		/// <param name="writer">Contexte de la sérialisation</param>
		private static void VisitInterfaceAtRuntime(object? value, Type declaringType, Type? runtimeType, CrystalJsonWriter writer)
		{
			// si on arrive ici, c'est qu'un membre d'une struct/class à pour type une interface, qu'il faut résolver au runtime
			if (value == null)
			{
				writer.WriteNull(); // "null"
				return;
			}

			runtimeType ??= value.GetType();
			if (runtimeType == typeof(object))
			{ // c'était vraiment un object !
				writer.WriteEmptyObject();
				return;
			}

			// détermine le bon writer
			var visitor = GetVisitorForType(runtimeType, atRuntime: true); // atRuntime = true, pour éviter une boucle infinie (si false,  ca retourne un callback qui va nous rappeler directement !)
			if (visitor == null) throw CrystalJson.Errors.Serialization_DoesNotKnowHowToSerializeType(runtimeType);

			// chaînage
			visitor(value, declaringType, runtimeType, writer);
		}

		public static void VisitXmlNode(XmlNode? node, CrystalJsonWriter writer)
		{
			if (node == null)
			{
				writer.WriteNull(); // "null"
			}

			var converted = ConvertXmlNodeToJson(node);
			if (converted.IsNull)
			{
				writer.WriteNull(); // "null"
			}
			else
			{
				converted.JsonSerialize(writer);
			}
		}

		public static void ConvertDictionaryToJson(JsonObject root, IEnumerable<KeyValuePair<string, string>> wmlDictionary, char separator = '.')
		{
			foreach (var kvp in wmlDictionary)
			{
				string path = kvp.Key;
				int k = path.LastIndexOf(separator);
				if (k >= 0)
				{// enlève la key du Xpath si elle contient un séparateur
					string key = path.Substring(k + 1);
					path = path.Substring(0, k);
					root.GetOrCreateObject(path).Set(key, kvp.Value);
				}
				else
				{
					root.Set(kvp.Key, kvp.Value);
				}
			}
		}

		/// <summary>Convertit un document XML en JSON</summary>
		/// <param name="node"></param>
		/// <param name="flatten">Si false, les attributs sont préfixés par '@'. Si true, les attributs sont mixés avec les éléments fils</param>
		/// <example><code>ConvertXmlNodeToJson(&lt;foo id="123">&lt;bar>hello&lt;/bar>&lt;baz>world&lt;/baz>&lt;/foo>) => { "foo": { "@id": "123", "bar": "hello", "baz": "world" } }</code></example>
		[Pure]
		public static JsonValue ConvertXmlNodeToJson(XmlNode? node, bool flatten = false)
		{
			if (node == null) return JsonNull.Null;

			switch (node.NodeType)
			{
				case XmlNodeType.Element:
				{
					var elem = (XmlElement)node;

					var obj = new JsonObject();
					if (elem.HasAttributes)
					{
						foreach (XmlAttribute attr in elem.Attributes)
						{
							obj[flatten ? attr.Name : ("@" + attr.Name)] = attr.Value;
						}
					}
					if (elem.HasChildNodes)
					{
						XmlNodeList children = elem.ChildNodes;
						if (children.Count == 1)
						{
							var first = children[0]!;
							if (first.NodeType == XmlNodeType.Text || first.NodeType == XmlNodeType.CDATA)
							{
								return first.Value;
							}
						}

						foreach (XmlNode child in children)
						{
							var converted = ConvertXmlNodeToJson(child, flatten);
							if (JsonNull.Missing.Equals(converted))
							{ // par défaut, on ignore les "missing" (mais on garde les null explicites)
								continue;
							}

							string name = child.Name;

							// s'il y a deja un child avec le même nom, on va transformer en List
							if (obj.TryGetValue(name, out var previous))
							{
								if (previous.IsArray)
								{
									((JsonArray)previous).Add(converted);
								}
								else
								{
									var arr = new JsonArray { previous, converted };
									obj[name] = arr;
								}
							}
							else
							{
								obj.Add(name, converted);
							}
						}
					}
					return obj;
				}
				case XmlNodeType.Text:
				case XmlNodeType.Attribute:
				case XmlNodeType.CDATA:
				{
					return node.Value;
				}
				default:
				{
					return JsonNull.Missing;
				}
			}
		}

		[Pure]
		public static JsonValue ConvertTupleToJson(IVarTuple? tuple)
		{
			if (tuple == null) return JsonNull.Null;
			int n = tuple.Count;
			if (n == 0) return JsonArray.Empty;
			var arr = new JsonArray(n);
			for (int i = 0; i < n; i++)
			{
				arr.Add(JsonValue.FromValue(tuple[i]));
			}
			return arr;
		}

#if !NETFRAMEWORK && !NETSTANDARD

		[Pure]
		public static JsonValue ConvertTupleToJson(System.Runtime.CompilerServices.ITuple? tuple)
		{
			if (tuple == null) return JsonNull.Null;
			int n = tuple.Length;
			if (n == 0) return JsonArray.Empty;
			var arr = new JsonArray(n);
			for (int i = 0; i < n; i++)
			{
				//TODO: si c'est des ValueTuple<...> on peut connaître exactement le type de chaque item!
				arr.Add(JsonValue.FromValue(tuple[i]));
			}
			return arr;
		}

#endif

		#endregion

	}

}
