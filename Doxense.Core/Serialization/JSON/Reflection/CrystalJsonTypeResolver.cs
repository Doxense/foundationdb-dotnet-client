#region Copyright (c) 2005-2023 Doxense SAS
//
// All rights are reserved. Reproduction or transmission in whole or in part, in
// any form or by any means, electronic, mechanical or otherwise, is prohibited
// without the prior written consent of the copyright owner.
//
#endregion

//#define DEBUG_JSON_RESOLVER
//#define DEBUG_JSON_BINDER

namespace Doxense.Serialization.Json
{
	using System;
	using System.Collections;
	using System.Collections.Generic;
	using System.Collections.Immutable;
	using System.Collections.ObjectModel;
	using System.Diagnostics.CodeAnalysis;
	using System.Linq;
	using System.Linq.Expressions;
	using System.Reflection;
	using System.Runtime.CompilerServices;
	using Doxense.Collections.Caching;
	using Doxense.Collections.Tuples;
	using Doxense.Diagnostics.Contracts;
	using Doxense.Runtime;
	using Doxense.Runtime.Converters;
	using JetBrains.Annotations;

	/// <summary>Resolver JSON qui utilise la reflection pour �num�rer les membres d'un type</summary>
	public class CrystalJsonTypeResolver : ICrystalJsonTypeResolver
	{
		#region Private Members...

		private readonly QuasiImmutableCache<Type, CrystalJsonTypeDefinition?> m_typeDefinitionCache = new (TypeEqualityComparer.Default);

		/// <summary>Classe contenant les mappings ID => Type d�j� r�solv�s</summary>
		private readonly QuasiImmutableCache<string, Type> m_typesByClassId = new QuasiImmutableCache<string, Type>(StringComparer.Ordinal);

		#endregion

		/// <summary>Inspecte un type pour retrouver la liste de ses membres</summary>
		/// <param name="type">Type � inspecter</param>
		/// <returns>Liste des members compil�e, ou null si le type n'est pas compatible</returns>
		/// <remarks>La liste est mise en cache pour la prochaine fois</remarks>
		public CrystalJsonTypeDefinition? ResolveJsonType(Type type)
		{
#if DEBUG_JSON_RESOLVER
			Debug.WriteLine(this.GetType().Name + ".ResolveType(" + type + ")");
#endif
			Contract.NotNull(type);

			// ReSharper disable once ConvertClosureToMethodGroup
			return m_typeDefinitionCache.GetOrAdd(type, ResolveNewTypeHandler, this);
		}

		private static readonly Func<Type, CrystalJsonTypeResolver, CrystalJsonTypeDefinition?> ResolveNewTypeHandler = (t, self) => self.ResolveJsonTypeNoCache(t);

		private CrystalJsonTypeDefinition? ResolveJsonTypeNoCache(Type type)
		{
			Contract.Debug.Requires(type != null);
			return GetTypeDefinition(type);
		}

		ICrystalTypeDefinition? ICrystalTypeResolver.ResolveType(Type type)
		{
			return ResolveJsonType(type);
		}

		public Type ResolveClassId(string classId)
		{
#if DEBUG_JSON_RESOLVER
			Debug.WriteLine(this.GetType().Name + ".ResolveClassId(" + classId + ")");
#endif
			Contract.NotNullOrEmpty(classId);

			return m_typesByClassId.GetOrAdd(
				classId,
				(id, self) =>
				{
					var type = self.GetTypeByClassId(id);
					if (type == null) throw CrystalJson.Errors.Binding_CouldNotResolveClassId(id);
					return type;
				},
				this
			);
		}

		protected virtual CrystalJsonTypeDefinition? GetTypeDefinition(Type type)
		{
#if DEBUG_JSON_RESOLVER
			Debug.WriteLine(this.GetType().Name + ".ResolveNewTypeWriter(" + type + ") => from reflection");
#endif

			if (type.IsPrimitive)
			{ // int, long, bool, ...
				return null;
			}
			if (typeof(Delegate).IsAssignableFrom(type))
			{ // delegate, Func<..>, Action<..>, WaitCallback, ...
				return null;
			}

			// Cas sp�cial pour les NullableTypes
			var nullable = Nullable.GetUnderlyingType(type);
			if (nullable != null)
			{
				var def = ResolveJsonType(nullable);
				if (def == null) return null; // on ne sait pas g�rer ce type !
				return CreateNullableTypeWrapper(type, def);
			}

			//TEMP HACK: a am�liorer!
			if (type.Name == "KeyValuePair`2")
			{
				return CreateFromKeyValuePair(type);
			}

			return CreateFromReflection(type);
		}

		private CrystalJsonTypeDefinition CreateFromKeyValuePair(Type type)
		{
			// on a en entr�e: "{ Key: ..., Value: ... }" et on veut en sortie un KeyValuePair<TKey, TValue>

			// (value, _, resolver) =>
			// {
			//    var obj = value.AsObject();
			//    return obj == null
			//      ? default(KeyValuePair<TKey, TValue>)
			//      : new KeyValuePair<TKey, TValue>(
			//          obj.GetValue<TKey>("Key", resolver),
			//          obj.GetValue<TValue>("Value", resolver)
			//    );
			// }

			var args = type.GetGenericArguments();
			var keyType = args[0];
			var valueType = args[1];

			var prmValue = Expression.Parameter(typeof(JsonValue), "value");
			var prmType = Expression.Parameter(typeof(Type), "_"); // ignored
			var prmResolver = Expression.Parameter(typeof(ICrystalJsonTypeResolver), "resolver");

			var varObj = Expression.Variable(typeof(JsonObject), "obj");

			var body = Expression.Block(
				new [] { varObj },
				Expression.Assign(varObj, Expression.Call(typeof(JsonValueExtensions), nameof(JsonValueExtensions.AsObject), null, prmValue)), //TODO: ajout de "required: true" ?
				Expression.Convert(
					Expression.Condition(
						Expression.ReferenceEqual(varObj, Expression.Default(typeof(JsonObject))),
						Expression.Default(type),
						Expression.New(
							type.GetConstructors().Single(),
							Expression.Call(varObj, nameof(JsonObject.Get), new [] { keyType }, Expression.Constant("Key"), prmResolver), // Get<TKey>(key, ICrystalTypeResolver)
							Expression.Call(varObj, nameof(JsonObject.Get), new [] { valueType }, Expression.Constant("Value"), prmResolver) // Get<TValue>(key, ICrystalTypeResolver)
						)
					),
					typeof(object)
				)
			);
			var binder = Expression.Lambda<CrystalJsonTypeBinder>(body, "<>_KV_"+ keyType.Name + "_" + valueType.Name + "_Unpack", true, new [] { prmValue, prmType, prmResolver}).Compile();

			//REVIEW: je suis pas sur que ce soit n�cessaire, vu qu'on a d�ja un custom binder!
			var members = new[]
			{
				new CrystalJsonMemberDefinition()
				{
					Name = "Key",
					Type = keyType,
					DefaultValue = keyType.GetDefaultValue(),
					ReadOnly = true,
					Getter = type.GetProperty("Key")!.CompileGetter(),
					Setter = null, //TODO: private setter?
					//TODO: visitor? binder?
				},
				new CrystalJsonMemberDefinition()
				{
					Name = "Value",
					Type = valueType,
					DefaultValue = valueType.GetDefaultValue(),
					ReadOnly = true,
					Getter = type.GetProperty("Value")!.CompileGetter(),
					Setter = null, //TODO: private setter?
					//TODO: visitor? binder?
				}
			};

			return new CrystalJsonTypeDefinition(type, null, null, binder, null, members);
		}

		protected virtual string? GetClassIdByType(Type type)
		{
			return null;
		}

		protected virtual Type? GetTypeByClassId(string classId)
		{
#if DEBUG_JSON_RESOLVER
			Debug.WriteLine(this.GetType().Name + ".GetTypeByClassId(" + classId + ") => Type.GetType(...)");
#endif
			return Type.GetType(classId);
		}

		/// <summary>Bind une valeur JSON en un type CLR sp�cifique</summary>
		public T? BindJson<T>(JsonValue? value)
		{
			var res = BindJsonValue(typeof(T), value);
			return res == null ? default(T)! : (T) res;
		}

		/// <summary>Bind une valeur JSON en type CLR correspondant (ValueType, Class, List, ...)</summary>
		public virtual object? BindJsonValue(Type? type, JsonValue? value)
		{
#if DEBUG_JSON_BINDER
			Debug.WriteLine(this.GetType().Name + ".BindValue(" + type + ", " + value + ")");
#endif

			if (value == null) return null;

			switch (value.Type)
			{
				case JsonType.Null:
				{
					return null;
				}
				case JsonType.Object:
				{
					return BindJsonObject(type, value as JsonObject);
				}
				case JsonType.Array:
				{
					return BindJsonArray(type, value as JsonArray);
				}
				default:
				{
					return value.Bind(type, this);
				}
			}
		}

		/// <summary>Bind un objet JSON en type CLR</summary>
		public virtual object? BindJsonObject(Type? type, JsonObject? value)
		{
#if DEBUG_JSON_BINDER
			Debug.WriteLine(this.GetType().Name + ".BindObject(" + type + ", " + value + ")");
#endif
			if (value == null) return null;
			type ??= typeof(object);

			if (typeof(JsonObject) == type || typeof(JsonValue) == type)
			{ // c'est d�j� dans le bon type !
				return value;
			}

			// il y a certains types qui ne sont pas des objets...
			if (typeof(string) == type || typeof(DateTime) == type || typeof(decimal) == type || type.IsPrimitive)
				throw CrystalJson.Errors.Binding_CannotBindJsonObjectToThisType(value, type);

			return CrystalJsonParser.DeserializeCustomClassOrStruct(value, type, this);
		}

		internal static readonly QuasiImmutableCache<Type, Func<CrystalJsonTypeResolver, JsonArray?, object?>> DefaultArrayBinders = new QuasiImmutableCache<Type, Func<CrystalJsonTypeResolver, JsonArray?, object?>>(TypeEqualityComparer.Default);

		private static readonly Func<Type, Func<CrystalJsonTypeResolver, JsonArray?, object?>> JsonArrayBinderCallback = CreateDefaultJsonArrayBinder;

		/// <summary>Bind un liste JSON en liste d'objets CLR</summary>
		public virtual object? BindJsonArray(Type? type, JsonArray? array)
		{
			// ReSharper disable once ConvertClosureToMethodGroup
			return DefaultArrayBinders.GetOrAdd(type ?? typeof(object), JsonArrayBinderCallback)(this, array);
		}

		public static Func<CrystalJsonTypeResolver, JsonArray?, object?> CreateDefaultJsonArrayBinder(Type? type)
		{
#if DEBUG_JSON_BINDER
			Debug.WriteLine(this.GetType().Name + ".BindArray(" + type + ", " + array + ")");
#endif

			if (type == null || typeof(object) == type)
			{ // Auto-detection ?
				return (_, array) => array?.ToObject();
			}

			if (typeof(JsonArray) == type || typeof(JsonValue) == type)
			{ // C'est d�j� une Array !
				return (_, array) => array;
			}

			if (typeof(string) == type)
			{ // on ne peut pas convertir une JSON Array en string
				return CreateDefaultJsonArrayBinder_Invalid(type); //note: pour �viter que ca matche le cas IEnumerable plus bas!
			}

			// ex: si on nous appel avec int[] c'est qu'on bind un int[][]
			// si on nous appel avec int c'est qu'on ne veut probablement plus de sous arrays...
			if (type.IsArray)
			{ // on veut binder dans un tableau
				Type elementType = type.GetElementType()!;
				if (elementType == typeof(bool)) return (_, array) => array?.ToBoolArray();
				if (elementType == typeof(string)) return (_, array) => array?.ToStringArray();
				if (elementType == typeof(int)) return (_, array) => array?.ToInt32Array();
				if (elementType == typeof(long)) return (_, array) => array?.ToInt64Array();
				if (elementType == typeof(double)) return (_, array) => array?.ToDoubleArray();
				if (elementType == typeof(float)) return (_, array) => array?.ToSingleArray();
				if (elementType == typeof(Guid)) return (_, array) => array?.ToGuidArray();
				if (elementType == typeof(NodaTime.Instant)) return (_, array) => array?.ToInstantArray();
				return CreateDefaultJsonArrayBinder_Filler(nameof(FillArray), elementType);
			}

			// est-ce une liste g�n�rique ?
			if (type.IsGenericType)
			{
				// Si le type d�rive de ICollection<> / IList<> on va cr�er une List<T> modifiable
				// Si le type ne d�rive que de IEnumerable<> on va cr�er une ReadOnlyCollection<T>

				// Si l'appelant nous donne une interface, on va chercher la classe la plus adapt�e
				// Si l'appelant nous donne une classe sp�cifique
				// > Si c'est un type connu (List<T>, ReadOnlyCollection<T>, ImmutableList<T>) on utilise le filler correspondant
				// > Sinon, on construit le type manuellement, et on appelle Add() ou AddRange() (comme le fait le compilateur avec les collection initializers)

				string? filler = null;

				if (type.IsGenericInstanceOf(typeof(IEnumerable<>)))
				{
					if (type.IsInterface)
					{ // on va rechercher une impl�mentation par d�faut la plus adapt�e

						if (type.IsGenericInstanceOf(typeof(IReadOnlyCollection<>)))
						{ // IReadOnlyCollection<T>, IImmutableXYZ<T>, ...
							if (type.IsGenericInstanceOf(typeof(IImmutableList<>)))
							{ // => ImmutableList<T>
								filler = nameof(FillImmutableList);
							}
							else if (type.IsGenericInstanceOf(typeof(IImmutableSet<>)))
							{ // => ImmutableHashSet<T>
								filler = nameof(FillImmutableHashSet);
							}
							else
							{ // => ReadOnlyCollection<T>
								filler = nameof(FillReadOnlyCollection);
							}
						}
						else if (type.IsGenericInstanceOf(typeof(ICollection<>)))
						{  // List<T>, HashSet<T>, ...
							if (type.IsGenericInstanceOf(typeof(IList<>)))
							{ // => List<T>
								filler = nameof(FillList);
							}
							else if (type.IsGenericInstanceOf(typeof(ISet<>)))
							{ // => HashSet<T>
								filler = nameof(FillHashSet);
							}
							else
							{ // => List<T> aussi ?
								filler = nameof(FillList);
							}
						}
						else
						{ // => l'appelant accepte n'importe quoi
							filler = nameof(FillEnumerable);
						}
					}
					else
					{ // on va regarder si c'est quelque chose qu'on conna�t

						if (type.IsGenericInstanceOf(typeof(List<>)))
						{
							filler = nameof(FillList);
						}
						else if (type.IsGenericInstanceOf(typeof(HashSet<>)))
						{
							filler = nameof(FillHashSet);
						}
						else if (type.IsGenericInstanceOf(typeof(ReadOnlyCollection<>)))
						{
							filler = nameof(FillReadOnlyCollection);
						}
						else if (type.IsGenericInstanceOf(typeof(ImmutableList<>)))
						{
							filler = nameof(FillImmutableList);
						}
						else if (type.IsGenericInstanceOf(typeof(IImmutableSet<>)))
						{ // => ImmutableHashSet<T>
							filler = nameof(FillImmutableHashSet);
						}
						else if (type.IsGenericInstanceOf(typeof(Dictionary<,>)) || type.IsGenericInstanceOf(typeof(ImmutableDictionary<,>)))
						{
							throw new JsonBindingException($"Cannot create a binder from a JSON Array to dictionary type '{type.GetFriendlyName()}'.");
						}
					}
				}

				if (filler != null)
				{
#if FULL_DEBUG
					System.Diagnostics.Debug.WriteLine("JsonArray: Using filler '" + filler + "' for type " + type.GetFriendlyName());
#endif
					var elementType = type.GetGenericArguments()[0];
					if (filler == nameof(FillList))
					{ // special cases!
						if (elementType == typeof(bool)) return (resolver, array) => array?.ToBoolList();
						if (elementType == typeof(string)) return (resolver, array) => array?.ToStringList();
						if (elementType == typeof(int)) return (resolver, array) => array?.ToInt32List();
						if (elementType == typeof(long)) return (resolver, array) => array?.ToInt64List();
						if (elementType == typeof(double)) return (resolver, array) => array?.ToDoubleList();
						if (elementType == typeof(float)) return (resolver, array) => array?.ToSingleList();
						if (elementType == typeof(Guid)) return (resolver, array) => array?.ToGuidList();
					}
					return CreateDefaultJsonArrayBinder_Filler(filler, elementType);
				}

				if (type.IsGenericInstanceOf(typeof(KeyValuePair<,>)))
				{
					var binder = CreateBinderForKeyValuePair(type);
					if (binder != null) return CreateDefaultJsonArrayBinder_Binder(type, binder);
				}
			}

			if (type == typeof(IVarTuple))
			{
				return CreateDefaultJsonArrayBinder_STuple(type);
			}

#if !NETSTANDARD && !NETFRAMEWORK
			if (typeof(System.Runtime.CompilerServices.ITuple).IsAssignableFrom(type))
			{
				return CreateDefaultJsonArrayBinder_ITuple(type);
			}
#endif

			var staticMethod = type.GetMethod("JsonUnpack", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
			if (staticMethod != null)
			{
				var binder = CreateBinderStaticJsonBindable(type, staticMethod);
				Contract.Debug.Assert(binder != null);
				return CreateDefaultJsonArrayBinder_Binder(type, binder);
			}

			//TODO: ducktyping! aka ctor(JsonValue)

			if (type.IsInstanceOf<IJsonBindable>())
			{
				var generator = type.CompileGenerator();
				if (generator != null)
				{
					var binder = CreateBinderForIJsonBindable(type, generator);
					Contract.Debug.Assert(binder != null);
					return CreateDefaultJsonArrayBinder_Binder(type, binder);
				}
			}

			staticMethod = type.GetMethod("JsonDeserialize", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
			if (staticMethod != null)
			{
				var binder = CreateBinderStaticJsonSerializable(type, staticMethod);
				Contract.Debug.Assert(binder != null);
				return CreateDefaultJsonArrayBinder_Binder(type, binder);
			}

			if (type.IsInstanceOf<IJsonSerializable>())
			{
				var generator = type.CompileGenerator();
				if (generator != null)
				{
					var binder = CreateBinderForIJsonSerializable(type, generator);
					Contract.Debug.Assert(binder != null);
					return CreateDefaultJsonArrayBinder_Binder(type, binder);
				}
			}

			if (type.IsInstanceOf<IEnumerable>())
			{
				return CreateDefaultJsonArrayBinder_Boxed(type);
			}

			// on ne sait pas g�rer ce type de collection
			return CreateDefaultJsonArrayBinder_Invalid(type);
		}

		private static Func<CrystalJsonTypeResolver, JsonArray?, object?> CreateDefaultJsonArrayBinder_Boxed(Type type)
		{
			return (resolver, array) => ConvertToBoxedEnumerable(type, array, resolver.BindJsonValue);
		}

		private static Func<CrystalJsonTypeResolver, JsonArray?, object?> CreateDefaultJsonArrayBinder_STuple(Type type)
		{
			return (resolver, array) => ConvertToSTuple(type, array, resolver.BindJsonValue);
		}

		private static Func<CrystalJsonTypeResolver, JsonArray?, object?> CreateDefaultJsonArrayBinder_ITuple(Type type)
		{
			var filler = CreateBinderForValueTuple(type);
			return (resolver, array) => filler(array, type, resolver);
		}

		private static Func<CrystalJsonTypeResolver, JsonArray?, object?> CreateDefaultJsonArrayBinder_Filler(string filler, Type elementType)
		{
			return (resolver, array) => resolver.InvokeFiller(filler, elementType, array);
		}

		private static Func<CrystalJsonTypeResolver, JsonArray?, object?> CreateDefaultJsonArrayBinder_Binder(Type type, CrystalJsonTypeBinder binder)
		{
			return (resolver, array) => binder(array, type, resolver);
		}

		private static Func<CrystalJsonTypeResolver, JsonArray?, object?> CreateDefaultJsonArrayBinder_Invalid(Type type)
		{
			return (_, array) => throw CrystalJson.Errors.Binding_CannotBindJsonObjectToThisType(array, type);
		}

		private static object? ConvertToBoxedEnumerable(Type type, JsonArray? array, Func<Type, JsonValue?, object?> convert)
		{
			if (array == null) return null;

			var res = new List<object?>(array.Count);
			foreach (var item in array)
			{
				res.Add(convert(typeof(object), item));
			}

			if (type.IsInstanceOf<ICollection>())
			{ // Modifiable ?
				return res;
			}
			else
			{ // Non modifiable
				return new ReadOnlyCollection<object?>(res);
			}
		}

		private static object? ConvertToSTuple(Type type, JsonArray? array, Func<Type, JsonValue?, object?> convert)
		{
			if (array == null) return null;
			var res = array.Count != 0 ? new object?[array.Count] : Array.Empty<object?>();
			int p = 0;
			foreach (var item in array)
			{
				res[p++] = convert(typeof(object), item);
			}
			return new ListTuple<object?>(res, 0, res.Length);
		}

		private Func<Type, JsonValue?, object?>? m_cachedValueBinder;

		private object? InvokeFiller(string name, Type type, JsonArray? array)
		{
			return GetFillerMethod(this.GetType(), name, type).Invoke(this, new object?[] { array, m_cachedValueBinder ??= this.BindJsonValue });
		}

		#region Filler Methods...

		private static readonly QuasiImmutableCache<string, MethodInfo> s_fillers = new QuasiImmutableCache<string, MethodInfo>(null, StringComparer.Ordinal, null);

		[Pure]
		private static MethodInfo GetFillerMethod(Type resolverType, string name, Type resultType)
		{
			string key = resultType.FullName + ":" + name;
			return s_fillers.TryGetValue(key, out MethodInfo? mi) ? mi : MakeFillerMethod(resolverType, name, resultType, key);
		}

		[MethodImpl(MethodImplOptions.NoInlining)]
		private static MethodInfo MakeFillerMethod(Type resolverType, string name, Type resultType, string key)
		{

			// g�n�re la m�thode
			var mi = resolverType.MakeGenericMethod(name, BindingFlags.Static | BindingFlags.Public | BindingFlags.FlattenHierarchy, new [] { typeof(JsonValue), resultType });
			if (mi == null) throw new ArgumentException($"No matching filler method {name} found on {resolverType.GetFriendlyName()}", nameof(name));

			s_fillers.SetItem(key, mi);
			return mi;
		}

		[UsedImplicitly]
		public static TOutput[] FillArray<TInput, TOutput>(IList<TInput> array, [InstantHandle] Func<Type, TInput, object> convert)
		{
			var type = typeof(TOutput);
			var list = new TOutput[array.Count];
			for (int i = 0; i < list.Length; i++)
			{
				list[i] = (TOutput)convert(type, array[i]);
			}
			return list;
		}

		[UsedImplicitly]
		public static List<TOutput> FillList<TInput, TOutput>(IList<TInput> array, [InstantHandle] Func<Type, TInput, object> convert)
		{
			var type = typeof(TOutput);
			var list = new List<TOutput>(array.Count);
			foreach (var item in array)
			{
				list.Add((TOutput)convert(type, item));
			}
			return list;
		}

		[UsedImplicitly]
		public static HashSet<TOutput> FillHashSet<TInput, TOutput>(IList<TInput> array, [InstantHandle] Func<Type, TInput, object> convert)
		{
			var type = typeof(TOutput);
			var list = new HashSet<TOutput>();
			foreach (var item in array)
			{
				list.Add((TOutput)convert(type, item));
			}
			return list;
		}

		public static ImmutableList<TOutput> FillImmutableList<TInput, TOutput>(IList<TInput> array, [InstantHandle] Func<Type, TInput, object> convert)
		{
			var type = typeof(TOutput);
			var list = ImmutableList.CreateBuilder<TOutput>();
			foreach (var item in array)
			{
				list.Add((TOutput) convert(type, item));
			}
			return list.ToImmutable();
		}

		[UsedImplicitly]
		public static ImmutableHashSet<TOutput> FillImmutableHashSet<TInput, TOutput>(IList<TInput> array, [InstantHandle] Func<Type, TInput, object> convert)
		{
			var type = typeof(TOutput);
			var hashSet = ImmutableHashSet.CreateBuilder<TOutput>();
			foreach (var item in array)
			{
				hashSet.Add((TOutput) convert(type, item));
			}
			return hashSet.ToImmutable();
		}

		[UsedImplicitly]
		public static ReadOnlyCollection<TOutput> FillReadOnlyCollection<TInput, TOutput>(IList<TInput> array, [InstantHandle] Func<Type, TInput, object> convert)
		{
			return new ReadOnlyCollection<TOutput>(FillList<TInput, TOutput>(array, convert));
		}

		[UsedImplicitly]
		public static IEnumerable<TOutput> FillEnumerable<TInput, TOutput>(IList<TInput> array, [InstantHandle] Func<Type, TInput, object> convert)
		{
			// si l'appelant veut un IEnumerable<T> c'est qu'il veut l'utiliser avec foreach/LINQ, n'as pas besoin du Count, et ne la modifiera pas
			// => List<T> est le plus simple, mais est modifiable (Add, Remove)
			// => T[] est le plus efficace, mais est aussi modifiable (list[2] = xxx)
			// => ImmutableList<T> est un peu overkill
			// => ReadOnlyCollection<T>(...) semble �tre le meilleur compromis ?

			return new ReadOnlyCollection<TOutput>(FillArray<TInput, TOutput>(array, convert));
		}

		#endregion

		private static bool FilterMemberByAttributes(MemberInfo member, bool hasDataContract, ref string name, ref object? defaultValue)
		{
			Attribute? attr;
			if (hasDataContract)
			{ // il doit avoir l'attribute "DataMember" pour etre s�lectionn�
				if (!member.TryGetCustomAttribute("DataMemberAttribute", true, out attr) || attr == null)
				{ // skip!
					return false;
				}
				// regarde s'il override le nom...
				name = attr.GetProperty<string>("Name") ?? name;
			}
			else
			{ // regarde du cot� des attributs de s�rialisation XML

				// il ne doit pas avoir l'attribute "XmlIgnore"
				if (member.TryGetCustomAttribute("XmlIgnoreAttribute", true, out attr) && attr != null)
				{ // skip!
					return false;
				}

				// il ne doit pas avoir d'attribut "JsonIgnore"
				if (member.TryGetCustomAttribute("JsonIgnoreAttribute", true, out attr) && attr != null)
				{ // skip!
					return false;
				}

				// recherche un attribut qui contiendrait le nom d�sir�
				if (member.TryGetCustomAttribute("XmlElementAttribute", true, out attr) && attr != null)
				{ // [XmlElement(ElementName="xxx")]
					name = attr.GetProperty<string>("ElementName") ?? name;
				}
				else if (member.TryGetCustomAttribute("XmlAttributeAttribute", true, out attr) && attr != null)
				{ // [XmlAttribute(AttributeName="xxx")]
					name = attr.GetProperty<string>("AttributeName") ?? name;
				}
			}
			return true;
		}

		private static bool FilterMemberByType(MemberInfo member, Type type)
		{
			if (typeof(Delegate).IsAssignableFrom(type))
			{
				// les Delegate ne peuvent pas �tre s�rialis�s, mais ils sont fr�quents lorsqu'on dump des objets dans des tests unitaires
				// => l'objet ne pourra probablement pas �tre d�s�rialis� correctement, mais dans ce scenario on s'en fiche
				return false;
			}

			return true;
		}

		private static JsonTypeAttribute? FindTypeAttribute(TypeInfo type)
		{
			foreach (var attr in type.GetCustomAttributes(true))
			{
				if (attr is JsonTypeAttribute jtype)
				{
					return jtype;
				}
			}
			return null;
		}

		private static JsonPropertyAttribute? FindPropertyAttribute(FieldInfo field)
		{
			foreach (var attr in field.GetCustomAttributes(true))
			{
				if (attr is JsonPropertyAttribute jprop)
				{
					return jprop;
				}
			}
			return null;
		}

		private static JsonPropertyAttribute? FindPropertyAttribute(PropertyInfo prop)
		{
			foreach (var attr in prop.GetCustomAttributes(true))
			{
				if (attr is JsonPropertyAttribute jprop)
				{
					return jprop;
				}
			}
			return null;
		}


		public static CrystalJsonMemberDefinition[] GetMembersFromReflection(Type type)
		{
			var members = new List<CrystalJsonMemberDefinition>();

			// regarde s'il y a des attributs de s�rialisation
			// note: on ne les r�f�rence pas directement pour �viter une d�pendances sur ces Assemblies
			bool hasDataContract = type.TryGetCustomAttribute("DataContractAttribute", true, out _);

			var fields = type.GetFields(BindingFlags.Instance | BindingFlags.Public);
			foreach (var field in fields)
			{
				if (field == null) continue;
				Type fieldType = field.FieldType;

				if (!FilterMemberByType(field, fieldType))
				{
					continue;
				}

				var jprop = FindPropertyAttribute(field);

				string name = jprop?.PropertyName ?? field.Name;
				object? defaultValue;
				try
				{
					defaultValue = jprop?.DefaultValue ?? fieldType.GetDefaultValue();
				}
				catch (Exception e)
				{
					throw CrystalJson.Errors.Serialization_CouldNotGetDefaultValueForMember(type, field, e);
				}

				if (!FilterMemberByAttributes(field, hasDataContract, ref name, ref defaultValue))
					continue; // skip

				var visitor = CrystalJsonVisitor.GetVisitorForType(fieldType, atRuntime: false);
				if (visitor == null) throw new ArgumentException($"Doesn't know how to serialize field {field.Name} of type {fieldType.GetFriendlyName()}", nameof(type));

				members.Add(new CrystalJsonMemberDefinition()
				{
					Name = name,
					Type = fieldType,
					Attributes = jprop,
					DefaultValue = defaultValue,
					ReadOnly = false,
					Getter = field.CompileGetter(),
					Setter = field.CompileSetter(),
					Visitor = visitor,
					Binder = GenericJsonValueBinder,
				});
			}

			var properties = type.GetProperties(BindingFlags.GetProperty | BindingFlags.Instance | BindingFlags.Public);
			foreach (var property in properties)
			{
				if (property == null) continue;
				Type propertyType = property.PropertyType;

				if (!FilterMemberByType(property, propertyType))
				{ // skip it!
					continue;
				}

				var jprop = FindPropertyAttribute(property);

				string name = jprop?.PropertyName ?? property.Name;
				object? defaultValue;
				try
				{
					defaultValue = jprop?.DefaultValue ?? propertyType.GetDefaultValue();
				}
				catch (Exception e)
				{
					throw CrystalJson.Errors.Serialization_CouldNotGetDefaultValueForMember(type, property, e);
				}

				if (!FilterMemberByAttributes(property, hasDataContract, ref name, ref defaultValue))
				{ // skip it !
					continue; // skip
				}

				// uniquement les properties qui ne prennent pas de param�tre (possible en VB.NET)
				var method = property.GetGetMethod();
				if (method == null || method.GetParameters().Length > 0) continue;

				var visitor = CrystalJsonVisitor.GetVisitorForType(propertyType);
				Contract.Debug.Assert(visitor != null);

				var getter = property.CompileGetter();

				Action<object, object?>? setter;
				method = property.GetSetMethod();
				if (method != null && method.GetParameters().Length == 1)
				{
					setter = property.CompileSetter();
				}
				else
				{ // essayes de trouver un moyen d�tourner pour quand m�me remplir le champ?
					setter = TryCompileAdderForReadOnlyCollection(property);
				}

				members.Add(new CrystalJsonMemberDefinition
				{
					Name = name,
					Type = propertyType,
					Attributes = jprop,
					DefaultValue = defaultValue,
					ReadOnly = setter == null,
					Getter = getter,
					Setter = setter,
					Visitor = visitor,
					Binder = GenericJsonValueBinder,
				});
			}

			return members.ToArray();
		}

		private static Action<object, object?>? TryCompileAdderForReadOnlyCollection(PropertyInfo property)
		{
			Contract.Debug.Requires(property?.DeclaringType != null);

			// On est dans le cas o� on a un objet avec une collection "get-only" qui est initialis�e par le ctor:
			// ex: public List<Foo> Foos { get; } = new List<Foo>();
			// => On peut quand m�me d�s�rialiser la collection en appelant Add(..) au lieu de set_Foo()

			var collectionType = property.PropertyType.FindGenericType(typeof(ICollection<>));
			if (collectionType == null)
			{
				return null;
			}

			// on doit compiler:
			//	AddRangeTo<TItem>((IEnumerable<TItem>) value, (PROPERTY_TYPE) instance.PROPERTY);

			var prmInstance = Expression.Parameter(typeof(object), "instance");
			var prmValue = Expression.Parameter(typeof(object), "value");

			var src = prmValue.CastFromObject(collectionType);
			var dst = Expression.Property(prmInstance.CastFromObject(property.DeclaringType), property);

			var m = typeof(CrystalJsonTypeResolver)
				.GetMethod(nameof(AddRangeTo), BindingFlags.NonPublic | BindingFlags.Static)!
				.MakeGenericMethod(collectionType.GetGenericArguments()[0]);

			var body = Expression.Call(m, src, dst);

			var lambda = Expression.Lambda<Action<object, object?>>(
				body,
				"<>_Append_" + property.DeclaringType.GetFriendlyName() + "_" + property.Name,
				tailCall: true,
				new [] { prmInstance, prmValue }
			);
			return lambda.Compile();

		}

		private static void AddRangeTo<T>(IEnumerable<T>? source, ICollection<T>? destination)
		{
			if (source != null && destination != null)
			{
				foreach (var item in source)
				{
					destination.Add(item);
				}
			}
		}

		private static readonly CrystalJsonTypeBinder GenericJsonValueBinder = (v, t, r) => (v ?? JsonNull.Missing).Bind(t, r);

		/// <summary>R�cup�re les informations d'un type par reflection, et g�n�re la liste des binders compil�s</summary>
		/// <param name="type">Classe, struct, interface. Primitive type non support�</param>
		private CrystalJsonTypeDefinition CreateFromReflection(Type type)
		{
			Contract.Debug.Requires(type != null && !type.IsPrimitive);

			var jtype = FindTypeAttribute(type.GetTypeInfo());

			// r�cup�re la liste des members
			var members = GetMembersFromReflection(type);

			// regarde si on n'a pas un custom binder sp�cifique...
			var binder = FindCustomBinder(type, out Func<object>? generator, members);

			if (binder == null)
			{ // pas de custom binder, on aura besoin de cr�er nous m�me des instances du type
				// g�n�re un g�n�rateur pour ce type
				generator ??= RequireGeneratorForType(type);
			}

			return new CrystalJsonTypeDefinition(type, jtype?.BaseType, jtype?.ClassId ?? GetClassIdByType(type), binder, generator, members);
		}

		/// <summary>G�n�re les informations sur une version Nullable d'un type Struct</summary>
		/// <param name="nullableType">Type Nullable (ex: Nullable&lt;AcmeStruct&gt;)</param>
		/// <param name="definition">D�finition du type sous-jacent (AcmeStruct)</param>
		/// <returns>D�finition utilisant les informations du type sous-jacent pour pouvoir binder une version Nullable</returns>
		private CrystalJsonTypeDefinition CreateNullableTypeWrapper(Type nullableType, CrystalJsonTypeDefinition definition)
		{
			Contract.NotNull(nullableType);
			Contract.NotNull(definition);

			object? NullableBinder(JsonValue? value, Type bindingType, ICrystalJsonTypeResolver resolver)
			{
				if (value.IsNullOrMissing()) return null;

				// bind comme si c'�tait le type sous-jacent
				var obj = resolver.BindJsonValue(definition.Type, value);
				if (obj == null) return null; // ??

				//TODO: trouver un moyen de le convertir correctement ?
				// note: on retourne un T a la place d'un Nullable<T>,
				// mais ce n'est pas tr�s grave car la CLR va automatiquement le convertir quand il sera assign�
				// le seul pb serait si quelqu'un utilise obj.GetType() juste derri�re ?

				return obj;
			}

			return new CrystalJsonTypeDefinition(definition.Type, definition.BaseType, definition.ClassId, NullableBinder, null, definition.Members);
		}

		private static Func<object> RequireGeneratorForType(Type type)
		{
			var generator = type.CompileGenerator();
			if (generator == null) throw new InvalidOperationException($"Could not find any parameterless constructor required to deserialize instances of type '{type.GetFriendlyName()}'");
			return generator;
		}

		private static CrystalJsonTypeBinder? FindCustomBinder(Type type, out Func<object>? generator, CrystalJsonMemberDefinition[] members)
		{
			generator = null;
			CrystalJsonTypeBinder? binder;

			// Static Bindable
			var staticMethod = type.GetMethod("JsonUnpack", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
			if (staticMethod != null)
			{
				binder = CreateBinderStaticJsonBindable(type, staticMethod);
				Contract.Debug.Assert(binder != null);
				return binder;
			}

			// IJsonBindable...
			if (type.IsInstanceOf<IJsonBindable>())
			{
				generator = RequireGeneratorForType(type);
				binder = CreateBinderForIJsonBindable(type, generator);
				Contract.Debug.Assert(binder != null);
				return binder;
			}

			// constructeur qui prend un JsonValue en entr�e?
			var ctor = FindJsonConstructor(type);
			if (ctor != null)
			{
				binder = CreateBinderJsonConstructor(type, ctor);
				return binder;
			}

			// Static Serializable
			staticMethod = type.GetMethod("JsonDeserialize", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
			if (staticMethod != null)
			{
				binder = CreateBinderStaticJsonSerializable(type, staticMethod);
				Contract.Debug.Assert(binder != null);
				return binder;
			}

			// IJsonSerialiable...
			if (type.IsInstanceOf<IJsonSerializable>())
			{
				generator = RequireGeneratorForType(type);
				binder = CreateBinderForIJsonSerializable(type, generator);
				Contract.Debug.Assert(binder != null);
				return binder;
			}

			// Dictionnaires?
			if (type.IsGenericInstanceOf(typeof(IDictionary<,>)))
			{
				if (type.Name == "ImmutableDictionary`2" && type.IsGenericInstanceOf(typeof(ImmutableDictionary<,>)))
				{
					return CreateBinderForImmutableDictionary(type);
				}

				generator = RequireGeneratorForType(type);
				binder = CreateBinderForDictionary(type, generator);
				if (binder != null) return binder;
			}

			if (type.IsAnonymousType())
			{ // les types anonymes ont un constructeur private qui prend toutes les valeurs d'un coup...
				binder = CreateBinderForAnonymousType(type, members);
				if (binder != null) return binder;

			}

			//TODO: d'autres cas sp�ciaux ?

			return null;
		}

		private static ConstructorInfo? FindJsonConstructor(Type type)
		{
			foreach (var ctor in type.GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
			{
				if (IsJsonConstructor(ctor)) return ctor;
			}
			return null;
		}

		private static bool IsJsonConstructor(ConstructorInfo ctor)
		{
			// Reconnait un constructor pour d�s�rialisation JSON
			// - ctor(JsonValue value)
			// - ctor(JsonValue value, ICrystalJsonTypeResolver resolver)

			var args = ctor.GetParameters();
			if (args.Length == 0) return false;
			var arg0Type = args[0].ParameterType;
			if (arg0Type != typeof (JsonValue) && arg0Type != typeof(JsonObject) && arg0Type != typeof(JsonArray)) return false;
			if (args.Length > 1)
			{
				if (!typeof (ICrystalJsonTypeResolver).IsAssignableFrom(args[1].ParameterType)) return false;
				if (args.Length > 2)
				{
					//TODO: g�rer les params optionnels (resolver, ...)
					return false;
				}
			}
			return true;
		}

		private static CrystalJsonTypeBinder? CreateBinderForAnonymousType(Type type, CrystalJsonMemberDefinition[] members)
		{
			// r�cup�re la liste des propri�t�s
			var props = type.GetProperties(BindingFlags.Public | BindingFlags.Instance).Where(prop => prop.CanRead && !prop.CanWrite).ToArray();

			var ctors = type.GetConstructors(BindingFlags.Public | BindingFlags.Instance);
			if (ctors.Length != 1) return null; // pas un type anonyme, ou quelque chose de fondamental a chang� au niveau du compilateur ?
			var ctor = ctors[0];

			var args = ctor.GetParameters();
			if (args.Length != props.Length) return null; // idem, pas un type anonyme, ou alors qqchose a chang�

			var map = members.ToDictionary(m => m.Name, StringComparer.Ordinal);
			var fields = props.Select(p =>
			(
				Name: p.Name,
				Type: p.PropertyType,
				Binder: map[p.Name].Binder
			)).ToArray();

			return CreateBinderForAnonymousType_NoScope(ctor, fields);
		}

		private static CrystalJsonTypeBinder CreateBinderForAnonymousType_NoScope(ConstructorInfo ctor, (string Name, Type Type, CrystalJsonTypeBinder Binder)[] fields)
		{
			return (v, t, r) =>
			{
				if (v == null || v.IsNull) return null;
				if (!v.IsMap) throw FailCannotDeserializeNotJsonObject(t);
				var obj = (JsonObject) v;

				var items = new object?[fields.Length];
				for (int i = 0; i < fields.Length; i++)
				{
					var field = fields[i];
					var value = obj[field.Name];
					items[i] = field.Binder(value, field.Type, r);
				}
				//REVIEW: PERF: compiler l'invocation du ctore dans une lambda?
				return ctor.Invoke(items);
			};
		}

		/// <summary>G�n�re un binder qui invoque la m�thode IJsonSerializable.JsonDeserialize(...) d'un objet</summary>
		private static CrystalJsonTypeBinder CreateBinderForIJsonSerializable(Type type, Func<object> generator)
		{
			Contract.Debug.Requires(type != null && typeof(IJsonSerializable).IsAssignableFrom(type));
			Contract.NotNull(generator);

			// L'instance dispose d'une m�thode this.JsonDeserialize(...)
			return (v, t, r) =>
			{
				if (v == null || v.IsNull) return null;
				if (!v.IsMap) throw FailCannotDeserializeNotJsonObject(t);

				// create new instance
				var instance = (IJsonSerializable) generator();
#pragma warning disable 618
				instance.JsonDeserialize((JsonObject) v, t, r);
#pragma warning restore 618
				return instance;
			};
		}

		/// <summary>G�n�re un binder qui invoque la m�thode IJsonSerializable.JsonUnpack(...) d'un objet</summary>
		internal static CrystalJsonTypeBinder CreateBinderForIJsonBindable(Type type, Func<object> generator)
		{
			Contract.Debug.Requires(type != null && typeof(IJsonBindable).IsAssignableFrom(type));
			Contract.NotNull(generator);

			// L'instance dispose d'une m�thode this.JsonDeserialize(...)
			return (v, t, r) =>
			{
				if (v == null || v.IsNull) return null;

				// create new instance
				var instance = (IJsonBindable)generator();
				instance.JsonUnpack(v, r);
				return instance;
			};
		}

		/// <summary>Cr�e un binder qui invoque la m�thode statique JsonSerialize d'un type</summary>
		private static CrystalJsonTypeBinder CreateBinderStaticJsonSerializable(Type type, MethodInfo staticMethod)
		{
			// la classe dispose d'une m�thode statique "factory" qui peut cr�er et d�s�rialiser un objet

			// on est appel� en tant que: object binder(JsonValue value, Type bindingType, CrystalJsonTypeResolver resolver)
			// on appel une m�thode qui ressemble �: Type [Type].JsonDeserialize([JsonValue|JsonObject] value, CrystalJsonTypeResolver resolver)

			var prms = staticMethod.GetParameters();
			if (prms.Length != 2)
			{
				throw new InvalidOperationException($"Static serialization method '{type.GetFriendlyName()}.{staticMethod.Name}' method must take two parameters");
			}
			var prmJsonType = prms[0].ParameterType;
			if (prmJsonType != typeof(JsonValue) && prmJsonType != typeof(JsonObject) && prmJsonType != typeof(JsonArray))
			{
				throw new InvalidOperationException($"First parameter of static method '{type.GetFriendlyName()}.{staticMethod.Name}' must either be of type JsonValue, JsonObject or JsonArray (it was {prmJsonType.GetFriendlyName()})");
			}
			if (prms[1].ParameterType != typeof(ICrystalJsonTypeResolver))
			{
				throw new InvalidOperationException($"Second parameter of static method '{type.GetFriendlyName()}.{staticMethod.Name}' must be of type ICrystalJsonTypeResolver (it was {prmJsonType.GetFriendlyName()})");
			}

			if (!type.IsAssignableFrom(staticMethod.ReturnType))
			{
				throw new InvalidOperationException($"Return type of static method '{type.GetFriendlyName()}.{staticMethod.Name}' must be assignable to type {type.GetFriendlyName()} (it was {prmJsonType.GetFriendlyName()})");
			}

			// on veut construire par exemple: (value, bindingType, resolver) => { return (object) [Type].JsonDeserialize((JsonObject)value, resolver); }

			var prmValue = Expression.Parameter(typeof(JsonValue), "value");
			var prmBindingType = Expression.Parameter(typeof(Type), "bindingType");
			var prmResolver = Expression.Parameter(typeof(ICrystalJsonTypeResolver), "resolver");

			// * Il faut �ventuellement caster la valeur de JsonValue vers JsonObject
			var castedJsonObject =
				prmJsonType == typeof(JsonObject) ? prmValue.CastFromObject(typeof(JsonObject)) :
				prmJsonType == typeof(JsonArray) ? prmValue.CastFromObject(typeof(JsonArray)) :
				prmValue;

			// body == "(object) Type.JsonDeserialize((JsonObject) value, resolver)"
			var body = Expression.Call(staticMethod, castedJsonObject, prmResolver).BoxToObject();

			// * Si l'objet est null, on retourne default(Type) automatiquement

			// "(value == null)"
			var isNull = Expression.Equal(prmValue, Expression.Constant(null, typeof(JsonValue)));

			// "(value == null) ? null : ( ... )"
			body = Expression.Condition(isNull, Expression.Constant(null), body, typeof(object));

			// body == "(value == null) ? null : (object) Type.JsonSerialize((JsonObject) value, resolver)"

			return Expression.Lambda<CrystalJsonTypeBinder>(body, "<>_" + type.Name + "_JsonDeserialize", true, new[] { prmValue, prmBindingType, prmResolver }).Compile();
		}

		/// <summary>Cr�er un binder qui invoque la m�thode static JsonUnpack d'un type</summary>
		private static CrystalJsonTypeBinder CreateBinderStaticJsonBindable(Type type, MethodInfo staticMethod)
		{
			// la classe dispose d'une m�thode statique "factory" qui peut binder un JsonValue en un objet

			// on est appel� en tant que: object binder(JsonValue value, Type bindingType, CrystalJsonTypeResolver resolver)
			// on appel une m�thode qui ressemble �: [Type].JsonUnpack([TJsonValue] value, CrystalJsonTypeResolver resolver)

			var prms = staticMethod.GetParameters();
			if (prms.Length != 2)
			{
				throw new InvalidOperationException($"Static deserialization method '{type.GetFriendlyName()}.{staticMethod.Name}' method must take two parameters");
			}
			Type prmJsonType = prms[0].ParameterType;
			if (prmJsonType != typeof(JsonValue) && !typeof(JsonValue).IsAssignableFrom(prmJsonType))
			{
				throw new InvalidOperationException($"First parameter of static method '{type.GetFriendlyName()}.{staticMethod.Name}' must be an JsonValue (it was {prmJsonType.GetFriendlyName()})");
			}
			if (prms[1].ParameterType != typeof(ICrystalJsonTypeResolver))
			{
				throw new InvalidOperationException($"Second parameter of static method '{type.GetFriendlyName()}.{staticMethod.Name}' must be a ICrystalJsonTypeResolver (it was {prmJsonType.GetFriendlyName()})");
			}
			if (!type.IsAssignableFrom(staticMethod.ReturnType))
			{
				throw new InvalidOperationException($"Return type of static method '{type.GetFriendlyName()}.{staticMethod.Name}' must be assignable to type {type.GetFriendlyName()} (it was {prmJsonType.GetFriendlyName()})");
			}

			// on veut construire par exemple: (value, bindingType, resolver) => { return (object) [Type].JsonUnpack((JsonObject)value, resolver); }

			var prmValue = Expression.Parameter(typeof(JsonValue), "value");
			var prmBindingType = Expression.Parameter(typeof(Type), "bindingType");
			var prmResolver = Expression.Parameter(typeof(ICrystalJsonTypeResolver), "resolver");

			// * Il faut �ventuellement caster la valeur de JsonValue vers le type attendu par la m�thode
			var castedJsonObject = prmJsonType != typeof(JsonValue) ? prmValue.CastFromObject(prmJsonType) : prmValue;

			// body == "(object) Type.JsonUnpack((TJsonValue) value, resolver)"
			var body = Expression.Call(staticMethod, castedJsonObject, prmResolver).BoxToObject();

			// * Si l'objet est null, on retourne default(Type) automatiquement

			// "(value == null)"
			var isNull = Expression.Equal(prmValue, Expression.Constant(null, typeof(JsonValue)));

			// "(value == null) ? null : ( ... )"
			body = Expression.Condition(isNull, Expression.Constant(null), body, typeof(object));

			// body == "(value == null) ? null : (object) Type.JsonUnpack((TJsonValue) value, resolver)"

			return Expression.Lambda<CrystalJsonTypeBinder>(body, "<>_" + type.Name + "_JsonUnpack", true, new[] { prmValue, prmBindingType, prmResolver }).Compile();
		}

		/// <summary>Cr�e un binder qui invoque le constructeur d'un type prenant un JsonValue (ou d�riv�e) en param�tre</summary>
		private static CrystalJsonTypeBinder CreateBinderJsonConstructor(Type type, ConstructorInfo ctor)
		{
			// la classe dispose d'un constructeur "factory" qui peut cr�er et d�s�rialiser un objet directement

			// on est appel� en tant que: object binder(JsonValue value, Type bindingType, CrystalJsonTypeResolver resolver)
			// on appel une m�thode qui ressemble �: new Type([JsonValue|JsonObject|JsonArray|....] value [, CrystalJsonTypeResolver resolver])

			var prms = ctor.GetParameters();

			if (prms.Length == 0 || prms.Length > 2) ThrowHelper.ThrowInvalidOperationException($"Private constructor for type {type.GetFriendlyName()} must take either one or two arguments");
			var prmJsonType = prms[0].ParameterType;
			if (prmJsonType != typeof(JsonValue) && prmJsonType != typeof(JsonObject) && prmJsonType != typeof(JsonArray))
				ThrowHelper.ThrowInvalidOperationException($"First parameter of constructor '{type.GetFriendlyName()}' must either be of type {nameof(JsonValue)}, {nameof(JsonObject)} or {nameof(JsonArray)} (it was {prmJsonType.GetFriendlyName()})");

			if (prms.Length > 1 && prms[1].ParameterType != typeof(ICrystalJsonTypeResolver))
				ThrowHelper.ThrowInvalidOperationException($"Second parameter of constructor '{type.GetFriendlyName()}' must be of type {nameof(ICrystalJsonTypeResolver)} (it was {prms[1].ParameterType.GetFriendlyName()})");

			// on veut construire par exemple: (value, bindingType, resolver) => { return (object) new [Type]((JsonObject)value, resolver); }

			var prmValue = Expression.Parameter(typeof(JsonValue), "value");
			var prmBindingType = Expression.Parameter(typeof(Type), "bindingType");
			var prmResolver = Expression.Parameter(typeof(ICrystalJsonTypeResolver), "resolver");

			// * Il faut �ventuellement caster la valeur de JsonValue vers JsonObject/JsonArray
			var castedJsonObject =
				prmJsonType == typeof(JsonObject) ? prmValue.CastFromObject(typeof(JsonObject)) :
				prmJsonType == typeof(JsonArray) ? prmValue.CastFromObject(typeof(JsonArray)) :
				prmValue;

			// body == "(object) Type.JsonDeserialize((JsonObject) value, resolver)"
			Expression body = Expression.New(ctor, prms.Length == 1 ? new [] { castedJsonObject } : new [] { castedJsonObject, prmResolver}).BoxToObject();

			// * Si l'objet est null, on retourne default(Type) automatiquement

			// "value.IsNullOrMissing()"
			var isNull = Expression.Call(typeof(JsonValueExtensions).GetMethod(nameof(JsonValueExtensions.IsNullOrMissing))!, prmValue);

			// "(value == null) ? null : ( ... )"
			body = Expression.Condition(isNull, Expression.Constant(null), body, typeof(object));

			// body == "(value == null) ? null : (object) Type.JsonSerialize((JsonObject) value, resolver)"

			return Expression.Lambda<CrystalJsonTypeBinder>(body, "<>_" + type.Name + "_JsonSerialize", true, new[] { prmValue, prmBindingType, prmResolver }).Compile();
		}

		private static CrystalJsonTypeBinder? CreateBinderForImmutableDictionary(Type type)
		{
			var dicType = type.FindGenericType(typeof(ImmutableDictionary<,>));
			if (dicType == null) return null; // <= on ne supporte que les ImmutableDictionary<K, V> pour le moment

			var typeArgs = dicType.GetGenericArguments();
			var keyType = typeArgs[0];
			var valueType = typeArgs[1];

			if (keyType == typeof(string))
			{
				return CreateBinderForImmutableDictionary_StringKey(valueType);
			}
			if (keyType == typeof(int))
			{
				return CreateBinderForImmutableDictionary_Int32Key(valueType);
			}

			if (keyType == typeof(int)
				|| keyType == typeof(uint)
				|| keyType == typeof(long)
				|| keyType == typeof(ulong)
				|| keyType == typeof(Guid)
				|| keyType == typeof(Uuid64)
				|| keyType == typeof(Uuid96)
				|| keyType == typeof(Uuid80)
				|| keyType == typeof(Uuid128)
				//TODO: rajouter d'autres types ?
			)
			{ // d�s�rialisation de type de cl�s simple (integers, guids, ...)
				var convert = TypeConverters.CreateBoxedConverter<string>(keyType);
				Contract.Debug.Assert(convert != null);
				//TODO: IMPLEMENTED BOXED IMMUTABLE DICT !!!
				return null;
			}

			// ce type n'est pas support� pour les cl�s
			//TODO: fail? (sinon on va probablement finir dans la d�s�rialisation custom class/struct ...
			return null;
		}

		private static CrystalJsonTypeBinder CreateBinderForImmutableDictionary_StringKey(Type valueType)
		{
			var m = typeof(CrystalJsonTypeResolver)
				.GetMethod(nameof(BindImmutableDictionary_StringKey), BindingFlags.Static | BindingFlags.NonPublic)!
				.MakeGenericMethod(valueType);

#if NETFRAMEWORK || NETSTANDARD
			return (CrystalJsonTypeBinder) m.CreateDelegate(typeof(CrystalJsonTypeBinder));
#else
			return m.CreateDelegate<CrystalJsonTypeBinder>();
#endif
		}

		private static object? BindImmutableDictionary_StringKey<TValue>(JsonValue? value, Type type, ICrystalJsonTypeResolver resolver)
		{
			if (value.IsNullOrMissing()) return null;
			if (value is not JsonObject obj) throw FailCannotDeserializeNotJsonObject(type);

			var instance = ImmutableDictionary.CreateBuilder<string, TValue?>(obj.Comparer);
			foreach (var item in obj)
			{
				object? o = resolver.BindJsonValue(typeof(TValue), item.Value);
				TValue? v = o is null ? default(TValue?) : (TValue?) o;
				instance.Add(item.Key, v);
			}
			return instance.ToImmutable();
		}

		private static CrystalJsonTypeBinder CreateBinderForImmutableDictionary_Int32Key(Type valueType)
		{
			var m = typeof(CrystalJsonTypeResolver)
				.GetMethod(nameof(BindImmutableDictionary_Int32Key), BindingFlags.Static | BindingFlags.NonPublic)!
				.MakeGenericMethod(valueType);

#if NETFRAMEWORK || NETSTANDARD
			return (CrystalJsonTypeBinder) m.CreateDelegate(typeof(CrystalJsonTypeBinder));
#else
			return m.CreateDelegate<CrystalJsonTypeBinder>();
#endif
		}

		private static object? BindImmutableDictionary_Int32Key<TValue>(JsonValue? value, Type type, ICrystalJsonTypeResolver resolver)
		{
			if (value.IsNullOrMissing()) return null;
			if (value is not JsonObject obj) throw FailCannotDeserializeNotJsonObject(type);

			var instance = ImmutableDictionary.CreateBuilder<int, TValue?>();
			foreach (var item in obj)
			{
				object? o = resolver.BindJsonValue(typeof(TValue), item.Value);
				TValue? v = o is null ? default(TValue?) : (TValue?) o;
				instance.Add(StringConverters.ToInt32(item.Key, 0), v);
			}
			return instance.ToImmutable();
		}

		private static CrystalJsonTypeBinder? CreateBinderForDictionary(Type type, Func<object> generator)
		{
			// il faut retrouver le type des valeurs de cet annuaire, pour le binding

			var dicType = type.FindGenericType(typeof(IDictionary<,>));
			if (dicType == null) return null; // <= on ne supporte que les IDictionary<K, V> pour le moment

			var typeArgs = dicType.GetGenericArguments();
			var keyType = typeArgs[0];
			var valueType = typeArgs[1];

			if (keyType == typeof(string))
			{ // d�s�rialisation de cl�s string
				return CreateBinderForDictionary_StringKey(generator, valueType);
			}

			if (keyType == typeof(int)
			 || keyType == typeof(uint)
			 || keyType == typeof(long)
			 || keyType == typeof(ulong)
			 || keyType == typeof(Guid)
			 || keyType == typeof(Uuid64)
			 || keyType == typeof(Uuid96)
			 || keyType == typeof(Uuid80)
			 || keyType == typeof(Uuid128)
			//TODO: rajouter d'autres types ?
			)
			{ // d�s�rialisation de type de cl�s simple (integers, guids, ...)
				var convert = TypeConverters.CreateBoxedConverter<string>(keyType);
				Contract.Debug.Assert(convert != null);
				return CreateBinderForDictionary_BoxedKey(generator, valueType, convert);
			}

			// ce type n'est pas support� pour les cl�s
			//TODO: fail? (sinon on va probablement finir dans la d�s�rialisation custom class/struct ...
			return null;
		}

		private static CrystalJsonTypeBinder CreateBinderForDictionary_StringKey(Func<object> generator, Type valueType)
		{
			return (v, t, r) =>
			{
				if (v == null || v.IsNull) return null;
				if (!v.IsMap) throw FailCannotDeserializeNotJsonObject(t);

				var obj = (JsonObject) v;
				var instance = (IDictionary) generator();
				foreach (var item in obj)
				{
					instance.Add(item.Key, r.BindJsonValue(valueType, item.Value));
				}

				return instance;
			};
		}

		private static CrystalJsonTypeBinder CreateBinderForDictionary_BoxedKey(Func<object> generator, Type valueType, Func<string, object?> convert)
		{
			return (v, t, r) =>
			{
				if (v == null || v.IsNull) return null;
				if (!v.IsMap) throw FailCannotDeserializeNotJsonObject(t);

				var obj = (JsonObject) v;
				var instance = (IDictionary) generator();
				foreach (var item in obj)
				{
					instance.Add(convert(item.Key), r.BindJsonValue(valueType, item.Value));
				}

				return instance;
			};
		}

		[Pure, MethodImpl(MethodImplOptions.NoInlining)]
		private static InvalidOperationException FailCannotDeserializeNotJsonObject(Type t)
		{
			return new InvalidOperationException($"Cannot deserialize {t.GetFriendlyName()} type because input value is not a JsonObject");
		}

		private static CrystalJsonTypeBinder? CreateBinderForKeyValuePair(Type type)
		{
			var args = type.GetGenericArguments();
			if (args.Length != 2) return null; // <= on ne supporte que les KeyValuePair<K, V>
			var keyType = args[0];
			var valueType = args[1];

			var prmKey = Expression.Parameter(typeof(object), "key");
			var prmValue = Expression.Parameter(typeof(object), "value");
			var empty = type.GetDefaultValue();

			var ctor = type.GetConstructor(args);
			Contract.Debug.Assert(ctor != null);

			// (object key, object value) => (object) new KeyValuePair<K, V>((K)key, (V)value)
			var expr = Expression
				.New(ctor, prmKey.CastFromObject(keyType), prmValue.CastFromObject(valueType))
				.BoxToObject();

			var lambda = Expression.Lambda<Func<object?, object?, object>>(expr, $"<>_KV_{keyType.Name}_{valueType.Name}_Unpack", true, new[] { prmKey, prmValue }).Compile();
			return CreateBinderForKeyValuePair_NoScope(keyType, valueType, empty, lambda);
		}

		private static CrystalJsonTypeBinder CreateBinderForKeyValuePair_NoScope(Type keyType, Type valueType, object? empty, Func<object?, object?, object> converter)
		{
			return (v, t, r) =>
			{
				if (v == null || v.IsNull) return empty;

				var arr = (JsonArray) v;
				if (arr.Count == 0) return empty;
				if (arr.Count != 2) throw FailCannotDeserializeNotJsonArrayPair(t);

				return converter(
					r.BindJsonValue(keyType, arr[0]),
					r.BindJsonValue(valueType, arr[1])
				);
			};
		}

		[Pure, MethodImpl(MethodImplOptions.NoInlining)]
		private static InvalidOperationException FailCannotDeserializeNotJsonArrayPair(Type t)
		{
			return new InvalidOperationException($"Cannot deserialize {t.GetFriendlyName()} type because input value is not a JsonArray with 2 elements");
		}

		private static CrystalJsonTypeBinder CreateBinderForValueTuple(Type type)
		{
			// on veut �crire: (value, ..., resolver) => (object) ValueTuple.Create(..., array[i].As<Ti>(resolver), ...)

			var prmValue = Expression.Parameter(typeof(JsonValue), "value");
			var prmType = Expression.Parameter(typeof(Type), "type");
			var prmResolver = Expression.Parameter(typeof(ICrystalJsonTypeResolver), "resolver");

			Expression expr;

			var args = type.GetGenericArguments();
			if (args.Length > 0)
			{
				// JsonValue JsonValue[int index]
				var arrayIndexer = typeof(JsonValue).GetProperty("Item", typeof(JsonValue), new[] { typeof(int) });

				// JsonValue.As<T>(resolver)
				var asMethod = typeof(JsonValueExtensions).GetMethod(
					nameof(JsonValueExtensions.As),
					BindingFlags.Static | BindingFlags.Public,
					null,
					new [] {typeof(JsonValue), typeof(ICrystalJsonTypeResolver)},
					null);
				Contract.Debug.Assert(asMethod != null, "Could not find the JsonValue.As<...>(...) extension method!");

				var items = new Expression[args.Length];
				for (int i = 0; i < items.Length; i++)
				{
					// value[i].As<Ti>(resolver)
					items[i] = Expression.Call(
						asMethod.MakeGenericMethod(args[i]),
						Expression.MakeIndex(prmValue, arrayIndexer, new Expression[] {Expression.Constant(i)}),
						prmResolver
					);
				}

				// ValueTuple<T...> ValueTuple.Create<T...>(..., ..., ...)
				var tupleMethod = typeof(ValueTuple)
					.GetMethods(BindingFlags.Static | BindingFlags.Public)
					.SingleOrDefault(m => m.Name == nameof(ValueTuple.Create) && m.GetGenericArguments().Length == args.Length);
				Contract.Debug.Assert(tupleMethod != null, "Could not find a compatible ValueTuple.Create(...) method for this type!");

				expr = Expression
					.Call(tupleMethod.MakeGenericMethod(args), items)
					.BoxToObject();
			}
			else
			{
				expr = Expression.Constant(ValueTuple.Create()).BoxToObject();
			}

			return Expression.Lambda<CrystalJsonTypeBinder>(expr, "<>_VT_" + args.Length + "_" + type.Name + "_Unpack", true, new[] {prmValue, prmType, prmResolver}).Compile();
		}

	}

}