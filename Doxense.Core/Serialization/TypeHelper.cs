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

//#define PROTOTYPE_REFLECTION_EMIT

namespace Doxense.Serialization
{
	using System;
	using System.Collections.Generic;
	using System.Collections.ObjectModel;
	using System.Diagnostics.CodeAnalysis;
	using System.Dynamic;
	using System.Globalization;
	using System.Linq;
	using System.Linq.Expressions;
	using System.Reflection;
	using Doxense.Diagnostics.Contracts;
	using Doxense.Runtime;
	using JetBrains.Annotations;

	public static class TypeHelper
	{

		/// <summary>Retourne la version string d'un clé d'un dictionnaire, en faisant le moins d'efforts possible</summary>
		/// <param name="key">Clé d'un dictionnaire</param>
		/// <returns>Version string de la clé</returns>
		[Pure, ContractAnnotation("key:notnull => notnull")]
		[return: NotNullIfNotNull("key")]
		public static string? ConvertKeyToString(object? key)
		{
			if (key == null) return null;

			if (key is string name)
			{ // c'est une string
				return name;
			}

			// special cases
			// => float/double: on doit utiliser "R" pour que ne pas perdre les dernières décimales
			if (key is double d) return d.ToString("R", CultureInfo.InvariantCulture);
			if (key is float f) return f.ToString("R", CultureInfo.InvariantCulture);
			// => DateTime: ISO !
			if (key is DateTime dt) return dt.ToString("O", CultureInfo.InvariantCulture);

			// La plupart des valuetypes supportent IFormattable

			// Au cas où la clé serait un Type
			if (key is Type type)
			{
				return GetFriendlyName(type);
			}

			// note: pour le moment tous les IConvertible sont aussi IFormattable...
			// Le problème de IFormattable c'est qu'hormis "G" et null, il n'y a pas de convention sur le format,
			// donc cela ne sert pas à grand chose ...
			if (key is IConvertible convertible)
			{ // l'objet sait se convertir en string
				return convertible.ToString(CultureInfo.InvariantCulture);
			}

			// TODO: utiliser TypeDescriptor.GetConverter ? A priori cela se base sur l'attribut [TypeConverter]

			// on tente notre chance ...
			return key.ToString()!;
		}

		[Pure]
		public static Type? FindReplacementType(Type type)
		{
			Contract.Debug.Requires(type != null);
			if (type.IsGenericType)
			{
				if (type.IsGenericInstanceOf(typeof(IDictionary<,>)))
				{
					return typeof(Dictionary<,>).MakeGenericType(type.GetGenericArguments());
				}
				else if (type.IsGenericInstanceOf(typeof(ICollection<>)))
				{
					return typeof(List<>).MakeGenericType(type.GetGenericArguments());
				}
				else if (type.IsGenericInstanceOf(typeof(IEnumerable<>)))
				{
					return typeof(ReadOnlyCollection<>).MakeGenericType(type.GetGenericArguments());
				}
			}

			//TODO: Hashtable/ArrayList ?

			return null;
		}

		/// <summary>Crée un générateur d'objet en fonction du type, si c'est possible</summary>
		/// <param name="type">Type de l'objet à créer</param>
		/// <returns>Fonction qui créer l'objet via un constructeur par défaut (sans paramètre), ou null s'il est impossible de créer un objet de ce type (interface, abstract, pas de constructeur par défaut, ...)</returns>
		[Pure]
		public static Func<object>? CompileGenerator(this Type type)
		{
			Contract.Debug.Requires(type != null);
			if (type.IsInterface || type.IsAbstract)
			{ // impossible de créer une interface oue une classe abstraite!
				var replacementType = FindReplacementType(type);
				if (replacementType == null)
				{
					return null;
				}
				// on a trouvé un type qui devrait correspondre
				type = replacementType;
			}

			if (type.IsClass)
			{ // "object func() { return new T(); }"
				var defaultConstructor = type.GetConstructor(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance, null, Type.EmptyTypes, null);
				if (defaultConstructor != null)
					return Expression.Lambda<Func<object>>(Expression.TypeAs(Expression.New(defaultConstructor), typeof(object))).Compile();
				else
					return () => Activator.CreateInstance(type)!;

				//NOTE: il y a assi FormatterServices.GetUninitializedObject(Type) qui permet de créer une instance sans appeler le constructeur
				// ( http://msdn.microsoft.com/en-us/library/system.runtime.serialization.formatterservices.getuninitializedobject.aspx )
				// Problème: certains types risquent de ne pas fonctionner correctement si le constructeur n'est pas appelé (ex: des private fields ne seront pas initialisés correctement)
			}
			else
			{
				return Expression.Lambda<Func<object>>(Expression.Convert(Expression.Default(type), typeof(object))).Compile();
			}
		}

		/// <summary>Crée un générateur d'objet en fonction du type, si c'est possible</summary>
		/// <returns>Fonction qui créer l'objet via un constructeur par défaut (sans paramètre), ou null s'il est impossible de créer un objet de ce type (interface, abstract, pas de constructeur par défaut, ...)</returns>
		[Pure]
		public static Func<TInstance>? CompileTypedGenerator<TInstance>()
		{
			var type = typeof(TInstance);
			if (type.IsInterface || type.IsAbstract)
			{ // impossible de créer une interface oue une classe abstraite!
				var replacementType = FindReplacementType(type);
				if (replacementType == null)
				{
					return null;
				}
				// on a trouvé un type qui devrait correspondre
				type = replacementType;
			}

			if (type.IsClass)
			{ // "object func() { return new T(); }"
				var defaultConstructor = type.GetConstructor(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance, null, Type.EmptyTypes, null);
				if (defaultConstructor != null)
					return Expression.Lambda<Func<TInstance>>(Expression.New(defaultConstructor)).Compile();
				else
					return () => (TInstance) Activator.CreateInstance(type)!;

				//NOTE: il y a assi FormatterServices.GetUninitializedObject(Type) qui permet de créer une instance sans appeler le constructeur
				// ( http://msdn.microsoft.com/en-us/library/system.runtime.serialization.formatterservices.getuninitializedobject.aspx )
				// Problème: certains types risquent de ne pas fonctionner correctement si le constructeur n'est pas appelé (ex: des private fields ne seront pas initialisés correctement)
			}
			else
			{
				return Expression.Lambda<Func<TInstance>>(Expression.Default(type)).Compile();
			}
		}

		/// <summary>Crée un générateur d'objet (prenant un paramètre) en fonction du type</summary>
		/// <param name="type">Type de l'objet à créer</param>
		/// <typeparam name="TArg0">Type du paramètre du constructeur</typeparam>
		/// <returns>Fonction qui créer l'objet via un constructeur prenant un paramètre, ou null s'il est impossible de créer un objet de ce type (interface, abstract, pas de constructeur par défaut, ...)</returns>
		[Pure]
		public static Func<TArg0, object> CompileGenerator<TArg0>(this Type type)
		{
			Contract.Debug.Requires(type != null);
			var ctor = type.GetConstructor(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance, null, new[] { typeof(TArg0) }, null);
			if (ctor == null) throw new InvalidOperationException($"Type {type.GetFriendlyName()} does not have a constructor taking one argument of type {typeof(TArg0).GetFriendlyName()}");

			var arg0 = Expression.Parameter(typeof(TArg0), "arg0");
			var body = Expression.New(ctor, arg0).BoxToObject();
			return Expression.Lambda<Func<TArg0, object>>(body, arg0).Compile();
		}

		/// <summary>Crée un générateur d'objet (prenant un seul paramètre) en fonction du type</summary>
		/// <typeparam name="TInstance">Type concret de l'instance a créer</typeparam>
		/// <typeparam name="TArg0">Type du paramètre du constructeur</typeparam>
		/// <returns>Fonction qui créer l'objet via un constructeur prenant un paramètre, ou null s'il est impossible de créer un objet de ce type (interface, abstract, pas de constructeur par défaut, ...)</returns>
		[Pure]
		public static Func<TArg0, TInstance> CompileTypedGenerator<TInstance, TArg0>()
		{
			var type = typeof(TInstance);
			var ctor = type.GetConstructor(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance, null, new[] { typeof(TArg0) }, null);
			if (ctor == null) throw new InvalidOperationException($"Type {type.GetFriendlyName()} does not have a constructor taking one argument of type {typeof(TArg0).GetFriendlyName()}");

			var prms = ctor.GetParameters();
			Contract.Debug.Assert(prms.Length == 1);
			var arg0 = Expression.Parameter(typeof(TArg0), prms[0].Name);
			var body = Expression.New(ctor, arg0);
			return Expression.Lambda<Func<TArg0, TInstance>>(body, arg0).Compile();
		}

		/// <summary>Crée un générateur d'objet (prenant deux paramètres) en fonction du type</summary>
		/// <typeparam name="TInstance">Type concret de l'instance a créer</typeparam>
		/// <typeparam name="TArg0">Type du premier paramètre du constructeur</typeparam>
		/// <typeparam name="TArg1">Type du deuxième paramètre du constructeur</typeparam>
		/// <returns>Fonction qui créer l'objet via un constructeur prenant un paramètre, ou null s'il est impossible de créer un objet de ce type (interface, abstract, pas de constructeur par défaut, ...)</returns>
		[Pure]
		public static Func<TArg0, TArg1, TInstance> CompileTypedGenerator<TInstance, TArg0, TArg1>()
		{
			var type = typeof(TInstance);
			var ctor = type.GetConstructor(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance, null, new[] { typeof(TArg0), typeof(TArg1) }, null);
			if (ctor == null) throw new InvalidOperationException($"Type {type.GetFriendlyName()} does not have a constructor taking two arguments of types ({typeof(TArg0).GetFriendlyName()}, {typeof(TArg1).GetFriendlyName()})");

			var prms = ctor.GetParameters();
			Contract.Debug.Assert(prms.Length == 2);
			var arg0 = Expression.Parameter(typeof(TArg0), prms[0].Name);
			var arg1 = Expression.Parameter(typeof(TArg1), prms[1].Name);
			var body = Expression.New(ctor, arg0, arg1);
			return Expression.Lambda<Func<TArg0, TArg1, TInstance>>(body, arg0, arg1).Compile();
		}

		/// <summary>Crée un générateur typé d'objets en fonction d'un constructeur</summary>
		/// <returns>Fonction qui créer l'objet via un constructeur prenant un paramètre, ou null s'il est impossible de créer un objet de ce type (interface, abstract, pas de constructeur par défaut, ...)</returns>
		[Pure]
		public static Delegate CompileTypedGenerator(ConstructorInfo ctor)
		{
			Contract.NotNull(ctor);
			var type = ctor.DeclaringType;

			var prms = ctor.GetParameters();
			var args = new ParameterExpression[prms.Length];
			for (int i = 0; i < prms.Length; i++)
			{
				args[i] = Expression.Parameter(prms[i].ParameterType, prms[i].Name);
			}
			var body = Expression.New(ctor, args);
			return Expression.Lambda(body, tailCall: true, args).Compile();
		}

		/// <summary>Crée un générateur d'objet (prenant trois paramètres) en fonction du type</summary>
		/// <typeparam name="TInstance">Type concret de l'instance a créer</typeparam>
		/// <typeparam name="TArg0">Type du premier paramètre du constructeur</typeparam>
		/// <typeparam name="TArg1">Type du deuxième paramètre du constructeur</typeparam>
		/// <typeparam name="TArg2">Type du troisième paramètre du constructeur</typeparam>
		/// <returns>Fonction qui créer l'objet via un constructeur prenant un paramètre, ou null s'il est impossible de créer un objet de ce type (interface, abstract, pas de constructeur par défaut, ...)</returns>
		[Pure]
		public static Func<TArg0, TArg1, TArg2, TInstance> CompileTypedGenerator<TInstance, TArg0, TArg1, TArg2>()
		{
			var type = typeof(TInstance);
			var ctor = type.GetConstructor(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance, null, new[] { typeof(TArg0), typeof(TArg1), typeof(TArg2) }, null);
			if (ctor == null) throw new InvalidOperationException($"Type {type.GetFriendlyName()} does not have a constructor taking three arguments of types ({typeof(TArg0).GetFriendlyName()}, {typeof(TArg1).GetFriendlyName()}, {typeof(TArg2).GetFriendlyName()})");

			var prms = ctor.GetParameters();
			Contract.Debug.Assert(prms.Length == 3);
			var arg0 = Expression.Parameter(typeof(TArg0), prms[0].Name);
			var arg1 = Expression.Parameter(typeof(TArg1), prms[1].Name);
			var arg2 = Expression.Parameter(typeof(TArg2), prms[2].Name);
			var body = Expression.New(ctor, arg0, arg1, arg2);
			return Expression.Lambda<Func<TArg0, TArg1, TArg2, TInstance>>(body, arg0, arg1, arg2).Compile();
		}

		#region Getters / Setters...

		/// <summary>Génère une Lambda '(object instance) => (object) (((TYPE) instance).MEMBER)'</summary>
		[Pure]
		public static Func<object, object> CompileGetter(MemberInfo member)
		{
			Contract.NotNull(member);
			switch (member)
			{
				case FieldInfo field:
				{
					return CompileGetter(field);
				}
				case PropertyInfo prop:
				{
					return CompileGetter(prop);
				}
				default:
				{
					throw new ArgumentException("Can only compile getters for fields and properties", nameof(member));
				}
			}
		}

		/// <summary>Génère une Lambda '(object instance, obj value) => (TInstance) instance).MEMBER = (TValue) value'</summary>
		[Pure]
		public static Action<object, object>? CompileSetter(MemberInfo member)
		{
			Contract.NotNull(member);
			switch (member)
			{
				case FieldInfo field:
				{
					return CompileSetter(field);
				}
				case PropertyInfo prop:
				{
					return CompileSetter(prop);
				}
				default:
				{
					throw new ArgumentException("Can only compile setters for fields and properties", nameof(member));
				}
			}
		}

		/// <summary>Génère une Lambda '(TInstance instance) => instance.MEMBER'</summary>
		[Pure]
		public static Func<TInstance, object> CompileGetter<TInstance>(MemberInfo member)
		{
			Contract.NotNull(member);
			switch (member)
			{
				case FieldInfo field:
				{
					return CompileGetter<TInstance>(field);
				}
				case PropertyInfo prop:
				{
					return CompileGetter<TInstance>(prop);
				}
				default:
				{
					throw new ArgumentException("Can only compile getters for fields and properties", nameof(member));
				}
			}
		}

		/// <summary>Génère une Lambda '(TInstance instance, object value) => instance.MEMBER = (TVALUE) value'</summary>
		[Pure]
		public static Action<TInstance, object?>? CompileSetter<TInstance>(MemberInfo member)
		{
			Contract.NotNull(member);

			switch (member)
			{
				case FieldInfo field:
				{
					return CompileSetter<TInstance>(field);
				}
				case PropertyInfo prop:
				{
					return CompileSetter<TInstance>(prop);
				}
				default:
				{
					throw new ArgumentException("Can only compile setters for fields and properties", nameof(member));
				}
			}
		}

		/// <summary>Génère une Lambda '(object instance) => (object) ((TInstance) instance).FIELD)'</summary>
		[Pure]
		public static Func<object, object> CompileGetter(this FieldInfo field)
		{
			Contract.NotNull(field);
			// "object func(object instance) { return (object) ((instance.TYPE)instance).Field; }"
			var prmInstance = Expression.Parameter(typeof(object), "instance");
			var body = Expression.Field(prmInstance.CastFromObject(field.DeclaringType!), field).BoxToObject();
			return Expression.Lambda<Func<object, object>>(body, prmInstance).Compile();
		}

		/// <summary>Génère une Lambda '(TInstance instance) => (object) (instance.FIELD)'</summary>
		[Pure]
		public static Func<TInstance, object> CompileGetter<TInstance>(this FieldInfo field)
		{
			Contract.NotNull(field);
			// "object func(instance.TYPE instance) { return (object) instance.Field; }"
			var prmInstance = Expression.Parameter(typeof(TInstance), "instance");
			var body = Expression.Field(prmInstance, field).BoxToObject();
			return Expression.Lambda<Func<TInstance, object>>(body, prmInstance).Compile();
		}

		/// <summary>Génère une Lambda '(TInstance instance) => instance.FIELD'</summary>
		public static Func<TInstance, TValue> CompileGetter<TInstance, TValue>(FieldInfo field)
		{
			Contract.NotNull(field);
			Contract.Debug.Requires(typeof(TValue).IsAssignableFrom(field.FieldType));
			// "TValue func(instance.TYPE target) { return (TValue) instance.Field; }"
			var prmInstance = Expression.Parameter(typeof(TInstance), "instance");
			var body = Expression.Field(prmInstance, field);
			return Expression.Lambda<Func<TInstance, TValue>>(body, prmInstance).Compile();
		}

		/// <summary>Génère une Lambda '(object instance, object value) => ((TInstance) instance).FIELD = (TValue) value', si c'est possible</summary>
		/// <returns>Attention, retourne null si la propriété est readonly !</returns>
		[Pure]
		public static Action<object, object?>? CompileSetter(this FieldInfo field)
		{
			Contract.NotNull(field);
			if (field.IsInitOnly) return null; // readonly !

			// "void func(object instance, object value) { ((instance.TYPE)instance).Field = (value.TYPE)value); }"
			var prmInstance = Expression.Parameter(typeof(object), "instance");
			var prmValue = Expression.Parameter(typeof(object), "value");

			var body = Expression.Assign(Expression.Field(prmInstance.CastFromObject(field.DeclaringType!), field), prmValue.CastFromObject(field.FieldType));
			return Expression.Lambda<Action<object, object?>>(body, prmInstance, prmValue).Compile();
		}

		/// <summary>Génère une Lambda '(TInstance instance, object value) => instance.FIELD = (TValue) value', si c'est possible</summary>
		[Pure]
		public static Action<TInstance, object?>? CompileSetter<TInstance>(this FieldInfo field)
		{
			Contract.NotNull(field);
			if (field.IsInitOnly) return null; // readonly !

			// "void func(instance.TYPE instance, object value) { instance.Field = (value.TYPE)value); }"
			var prmInstance = Expression.Parameter(typeof(TInstance), "instance");
			var prmValue = Expression.Parameter(typeof(object), "value");

			var body = Expression.Assign(Expression.Field(prmInstance, field), prmValue.CastFromObject(field.FieldType));
			return Expression.Lambda<Action<TInstance, object?>>(body, prmInstance, prmValue).Compile();
		}

		/// <summary>Génère une Lambda '(object instance) => (object) ((TInstance) instance).PROPERTY)'</summary>
		[Pure]
		public static Func<object, object> CompileGetter(this PropertyInfo property)
		{
			Contract.NotNull(property);
			// "object func(object target) { return (object) ((instance.TYPE)instance).get_Property(); }"
			var prmInstance = Expression.Parameter(typeof(object), "instance");
			var body = Expression.Call(prmInstance.CastFromObject(property.DeclaringType!), property.GetGetMethod()!).BoxToObject();
			return Expression.Lambda<Func<object, object>>(body, prmInstance).Compile();
		}

		/// <summary>Génère une Lambda '(TInstance instance) => (object) (instance.PROPERTY)'</summary>
		[Pure]
		public static Func<TInstance, object> CompileGetter<TInstance>(this PropertyInfo property)
		{
			Contract.NotNull(property);
			// "object func(instance.TYPE target) { return (object) instance.get_Property(); }"
			var prmInstance = Expression.Parameter(typeof(TInstance), "instance");
			var body = Expression.Call(prmInstance, property.GetGetMethod()!).BoxToObject();
			return Expression.Lambda<Func<TInstance, object>>(body, prmInstance).Compile();
		}

		/// <summary>Génère une Lambda '(TInstance instance) => instance.PROPERTY'</summary>
		[Pure]
		public static Func<TInstance, TValue> CompileGetter<TInstance, TValue>(PropertyInfo property)
		{
			Contract.NotNull(property);
			Contract.Debug.Requires(typeof(TValue).IsAssignableFrom(property.PropertyType));
			// "TValue func(instance.TYPE target) { return (TValue) instance.get_Property(); }"
			var prmInstance = Expression.Parameter(typeof(TInstance), "instance");
			var body = Expression.Call(prmInstance, property.GetGetMethod()!);
			return Expression.Lambda<Func<TInstance, TValue>>(body, prmInstance).Compile();
		}

		/// <summary>Génère une Lambda '(object instance, object value) => ((TInstance) instance).PROPERTY = (TValue) value', si c'est possible</summary>
		/// <returns>Attention, retourne null si la propriété est readonly !</returns>
		[Pure]
		public static Action<object, object?>? CompileSetter(this PropertyInfo property)
		{
			Contract.NotNull(property);
			var setMethod = property.GetSetMethod();
			if (setMethod == null) return null; // non modifiable !?
			// "void func(object target, object value) { ((instance.TYPE)instance).set_Property((value.TYPE)value)); }"
			var prmInstance = Expression.Parameter(typeof(object), "instance");
			var prmValue = Expression.Parameter(typeof(object), "value");
			var body = Expression.Call(prmInstance.CastFromObject(property.DeclaringType!), setMethod, prmValue.CastFromObject(property.PropertyType));
			return Expression.Lambda<Action<object, object?>>(body, prmInstance, prmValue).Compile();
		}

		/// <summary>Génère une Lambda '(TInstance instance, object value) => instance.PROPERTY = (TValue) value', si c'est possible</summary>
		/// <returns>Attention, retourne null si la propriété est readonly !</returns>
		[Pure]
		public static Action<TInstance, object?>? CompileSetter<TInstance>(this PropertyInfo property)
		{
			Contract.NotNull(property);
			var setMethod = property.GetSetMethod();
			if (setMethod == null) return null; // non modifiable !?
			// "void func(instance.TYPE target, object value) { instance.set_Property((value.TYPE)value)); }"
			var prmInstance = Expression.Parameter(typeof(TInstance), "instance");
			var prmValue = Expression.Parameter(typeof(object), "value");
			var body = Expression.Call(prmInstance, setMethod, prmValue.CastFromObject(property.PropertyType));
			return Expression.Lambda<Action<TInstance, object?>>(body, prmInstance, prmValue).Compile();
		}

		#endregion

		/// <summary>Retourne un Attribute d'un member (type, méthode, field, ...) à partir de son nom</summary>
		/// <param name="member">Field d'un type</param>
		/// <param name="attributeName">Nom (court) de l'attribut (ex: "DataMemberAttribute")</param>
		/// <param name="inherit">True pour remonter la chaine de dérivation</param>
		/// <param name="attribute">Attribute correspondant, ou null</param>
		/// <returns>Retourne true si l'attribut existe (accessible via <paramref name="attribute"/>).</returns>
		[ContractAnnotation("=> true, attribute:notnull; => false, attribute:null")]
		public static bool TryGetCustomAttribute(this MemberInfo member, string attributeName, bool inherit, out Attribute? attribute)
		{
			Contract.Debug.Requires(member != null && attributeName != null);
			attribute = member.GetCustomAttributes(inherit).FirstOrDefault(attr => attr.GetType().Name == attributeName) as Attribute;
			return attribute != null;
		}

		/// <summary>Retourne une propriété d'un attribut, par reflexion, qui doit être une string</summary>
		/// <param name="attribute">Objet de type attribute</param>
		/// <param name="name">Nom de la propriété de l'attribut recherchée</param>
		/// <returns>Valeur de cette propriété, ou null</returns>
		[Pure]
		[return:MaybeNull]
		public static T GetProperty<T>(this Attribute attribute, string name)
		{
			Contract.Debug.Requires(attribute != null && name != null);
			var prop = attribute.GetType().GetProperty(name);
			if (prop == null) return default!;
			var get = prop.GetGetMethod();
			if (get == null) return default!;
			return (T) (get.Invoke(attribute, null));
		}

		#region Type Extension Methods..

		/// <summary>Retourne la liste de tous les types d'une assembly, de manière sécurisée</summary>
		/// <param name="assembly">Assembly source</param>
		/// <returns>Liste des types de cette assembly</returns>
		/// <remarks>Immunisé contrel es ReflectioTypeLoadException !</remarks>
		[Pure]
		public static IEnumerable<Type> GetLoadableTypes(this Assembly assembly)
		{
			// Voir http://stackoverflow.com/questions/7889228/how-to-prevent-reflectiontypeloadexception-when-calling-assembly-gettypes
			// et http://haacked.com/archive/2012/07/23/get-all-types-in-an-assembly.aspx

			Contract.NotNull(assembly);
			try
			{
				return assembly.GetTypes();
			}
			catch (ReflectionTypeLoadException e)
			{
				return e.Types.Where(t => t != null)!;
			}
		}

		/// <summary>Retourne la valeur par défaut (null, 0, false, ...) d'un type</summary>
		/// <param name="type">Type à instantier</param>
		/// <returns>null, 0, false, DateTime.MinValue, ...</returns>
		[Pure]
		public static object? GetDefaultValue(this Type type)
		{
			if (type.IsValueType)
				return Activator.CreateInstance(type);
			else
				return null; // Reference Type / Interface
		}

		/// <summary>Retourne la valeur par défaut (null, 0, false, ...) d'un paramètre d'une méthode</summary>
		/// <param name="parameter">Paramètre d'une méthode</param>
		/// <returns>Valeur par défaut défini sur ce paramètre (s'il en a une), ou default(T)</returns>
		[Pure]
		public static object? GetDefaultValue(this ParameterInfo parameter)
		{
			return ((parameter.Attributes & ParameterAttributes.HasDefault) != 0) ? parameter.RawDefaultValue : GetDefaultValue(parameter.ParameterType);
		}

		/// <summary>Retourne le nom complet d'un type, utilisable avec Type.GetType(..)</summary>
		/// <param name="type">Type de l'objet</param>
		/// <returns>Nom de l'objet sous la forme "Namespace.ClassName, AssemblyName"</returns>
		[Pure]
		public static string GetAssemblyName(this Type type)
		{
			Contract.NotNull(type);

			// on veut générer "Namespace.ClassName, AssemblyName"

			var assemblyName = type.Assembly.GetName();
			if (assemblyName.Name == "mscorlib")
			{ // pour les types de mscorlib, il n'est pas nécessaire de préciser l'assembly
				return type.FullName!;
			}

			// note: type.AssemblyQualifiedName et type.Assembly.FullName ajoutent également le suffix ", Version=xxx, Culture=xxx, PublicKey=xxx" qu'on va devoir découper à la main
			string displayName = assemblyName.FullName;
			int p = displayName.IndexOf(',');
			if (p > 0) displayName = displayName.Substring(0, p);
			return type.FullName + ", " + displayName;
		}

		/// <summary>Retourne un nom de type "user friendly" comprenant le namespace, et les types génériques</summary>
		[Pure]
		public static string GetFriendlyName(this Type type)
		{
			Contract.NotNull(type);

			if (type.IsPrimitive || !type.IsEnum)
			{
				switch (Type.GetTypeCode(type))
				{
					case TypeCode.Boolean:  return "bool";
					case TypeCode.Char:     return "char";
					case TypeCode.SByte:    return "sbyte";
					case TypeCode.Byte:     return "byte";
					case TypeCode.Int16:    return "short";
					case TypeCode.UInt16:   return "ushort";
					case TypeCode.Int32:    return "int";
					case TypeCode.UInt32:   return "uint";
					case TypeCode.Int64:    return "long";
					case TypeCode.UInt64:   return "ulong";
					case TypeCode.Single:   return "float";
					case TypeCode.Double:   return "double";
					case TypeCode.Decimal:  return "decimal";
					case TypeCode.DateTime: return "DateTime";
					case TypeCode.String:   return "string";
				}
			}

			if (type.IsGenericParameter && type.DeclaringMethod == null)
			{
				return type.Name;
			}

			string? prefix = null;
			int outerOffset = 0;
			if (type.IsNested)
			{
				var outer = type.DeclaringType!;
				if (outer.IsGenericType)
				{
					// If this is of the form Outer<T1, T2>.Inner<T3>, the DeclaringType will not have the actual concrete types,
					// we have to get them from the generic type arguments of the inner type. It *looks* like the convention is
					// that Inner.GetGenericArguments() will return the types in order, so { T1, T2, T3 }, and if we know that the
					// outer types takes only N types (2 in the above example), then it will be the first N types that can be used
					// to construct the correct Outer class.
					// Example: if we have "Type inner = typeof(Outer<string, bool>.Inner<Guid>);" then:
					// > inner.DeclaringType returns "Outer<T1, T2>" instead of expected "Outer<string, bool>."
					// > inner.GetGenericArguments() will return { string, int, Guid }
					// > inner.DeclaringType.GetGenericArguments() will return { <T1>, <T2> } of length 2
					// > inner.DeclaringType.MakeGenericType(inner.GetGenericArguments().Take(2).ToArray()) will return the correct "Outer<string, bool>" !

					var innerArgs = type.GetGenericArguments(); // these will be the concrete types
					var outerArgs = outer.GetGenericArguments(); // these will be the generic types
					var concreteArgs = new Type[outerArgs.Length];
					Array.Copy(innerArgs, 0, concreteArgs, 0, concreteArgs.Length);
					outer = outer.MakeGenericType(concreteArgs);
					outerOffset = outerArgs.Length;
				}

				prefix = GetFriendlyName(outer) + ".";
			}

			if (type.IsGenericType)
			{ // => "Acme<string, bool>"

				var args = type.GetGenericArguments();
				if (outerOffset != 0)
				{
					var tmp = args.Length != outerOffset ? new Type[args.Length - outerOffset] : Array.Empty<Type>();
					Array.Copy(args, outerOffset, tmp, 0, tmp.Length);
					args = tmp;
				}
				string baseName;
				if (type.IsAnonymousType())
				{
					// le compilateur génère "<>f__AnonymousType#<....,....,....>" où # est un compteur unique en hexa.
					// On va plutôt remplacer par "AnonymousType<...,...,...>" qui est plus lisible, et qui est raccord avec ASP.NET MVC
					baseName = "AnonymousType";
				}
				else
				{
					baseName = type.GetGenericTypeDefinition().Name;
					// Les arguments génériques sont après le backtick (`)
					int p = baseName.IndexOf('`');
					if (p > 0) baseName = baseName.Substring(0, p);
				}

				// on va rappeler récursivement GetFriendlyName sur les types arguments (en espérant qu'une boucle est impossible :/)
				if (args.Length == 0) return prefix + baseName;
				return prefix + baseName + "<" + String.Join(", ", args.Select(t => GetFriendlyName(t))) + ">";
			}

			if (type.IsArray)
			{ // => "Acme[]"

				string baseName = prefix + GetFriendlyName(type.GetElementType()!);
				int rank = type.GetArrayRank();
				switch(rank)
				{
					case 1: return baseName + "[]";
					case 2: return baseName + "[,]";
					default: return baseName + type.Name.Substring(type.Name.IndexOf('['));
				}
			}

			if (type.IsPointer)
			{ // => "Acme*"
				string baseName = prefix + GetFriendlyName(type.GetElementType()!);
				return baseName + type.Name.Substring(type.Name.IndexOf('*'));
			}

			// cas standard
			return prefix + type.Name;
		}

		/// <summary>Retourne une version générique d'une méthode d'un type</summary>
		/// <param name="type"></param>
		/// <param name="name">Nom de la méthode</param>
		/// <param name="flags"></param>
		/// <param name="types">Types arguments de la méthode générique</param>
		/// <returns>Methode générique prête à l'emploi</returns>
		[Pure]
		public static MethodInfo? MakeGenericMethod(this Type type, string name, BindingFlags flags, params Type[] types)
		{
			Contract.Debug.Requires(type != null && name != null && types != null && types.Length > 0);
			var mi = type.GetMethod(name, flags);
			if (mi == null) return null;
			Contract.Debug.Assert(mi.IsGenericMethod, "Should reference a generic method");
			Contract.Debug.Assert(mi.GetGenericArguments().Length == types.Length, "Should have the correct number of generic type arguments");
			return mi.MakeGenericMethod(types);
		}

		/// <summary>Indique si un type est une instance d'une autre type ou interface</summary>
		/// <typeparam name="T">Type parent</typeparam>
		/// <param name="type">Type inspecté</param>
		/// <returns>Retourne true si notre type dérive ou est un implémentation d'un autre type</returns>
		/// <remarks>Equivalent de typeof(T).IsAssignableFrom(...)</remarks>
		[Pure]
		public static bool IsInstanceOf<T>(this Type type)
		{
			return typeof(T).IsAssignableFrom(type);
		}

		/// <summary>Indique si un type est une implémentation d'un type ou d'une interface générique</summary>
		/// <param name="type">Type de l'objet inspecté (ex: List&lt;string&gt;)</param>
		/// <param name="genericType">Type ou interface generic (ex: IList&lt;T&gt;)</param>
		/// <returns>True si le type est une implémentation de ce type générique</returns>
		/// <example><code>
		/// typeof(List&lt;string&gt;).IsGenericInstanceOf(typeof(IList&lt;&gt;)) == true
		/// typeof(HashSet&lt;string&gt;).IsGenericInstanceOf(typeof(IList&lt;&gt;)) == false
		/// </code></example>
		[Pure]
		public static bool IsGenericInstanceOf(this Type type, Type genericType)
		{
			return FindGenericType(type, genericType) != null;
		}

		/// <summary>Retrouve la version d'un type générique implémentée par un type</summary>
		/// <param name="type">Type à inspecter (ex: List&lt;string&gt;)</param>
		/// <param name="genericType">Type ou interface générique recherché (ex: IList&lt;&gt;)</param>
		/// <returns>Version du type implémentée par cet objet (ex: IList&lt;string&gt;) ou null si cet objet n'implémente pas ce type</returns>
		[Pure]
		public static Type? FindGenericType(this Type type, Type genericType)
		{
			Contract.NotNull(genericType);

			// on veut vérifier si type (ex: List<string>) implémente une interface générique (ex: IList<>)
			// hélas, IsAssignableFrom / IsSubclassOf ne marchent pas avec les version génériques des type, sinon ca serait trop facile :)

			// c'est peut-être directement la bonne ?
			if (type.IsSameGenericType(genericType)) return type;

			if (genericType.IsInterface)
			{ // regarde dans les interfaces implémentées?
				foreach (var interf in type.GetInterfaces())
				{
					// Attention: GetInterfaces() retourne des version "closed" des types génériques, qui ne sont pas identiques aux version typeof(IFoo<>)
					// => il faut passer par GetGenericTypeDefinition() pour les comparer
					if (interf.IsSameGenericType(genericType)) return interf;
				}
				return null;
			}
			else
			{ // regarde dans les classes parentes
				Type? parent = type.BaseType;
				while (parent != null && typeof(object) != parent)
				{
					if (parent.IsSameGenericType(genericType)) return parent;
					parent = parent.BaseType;
				}
				return null;
			}
		}

		/// <summary>Indique si le type est de la même famille qu'un type générique</summary>
		/// <param name="type">Type à inspecter (ex: IDictionary&lt;string, string&gt;)</param>
		/// <param name="genericType">Type générique (ex: IDictionary&lt;,&gt;)</param>
		/// <returns>True si les types sont équivalent, false dans le cas contraire</returns>
		[Pure]
		public static bool IsSameGenericType(this Type type, Type genericType)
		{
			return type == genericType || (genericType != null && type.IsGenericType && genericType == type.GetGenericTypeDefinition());
		}

		/// <summary>Indique si un type est "concret" c'est à dire que c'est une custom class/struct qui ne soit pas abstraite</summary>
		/// <param name="type">Type à inspecter</param>
		/// <returns>True si type n'est pas une interface, une classe abstraite ou System.Object</returns>
		[Pure]
		public static bool IsConcrete(this Type type)
		{
			return typeof(object) != type && !type.IsInterface && !type.IsAbstract;
		}

		/// <summary>Indique si un type est une classe concrète (pas abstract) qui implémente une interface particulière</summary>
		[Pure]
		public static bool IsConcreteImplementationOfInterface(this Type type, Type interfaceType)
		{
			return type.IsConcrete() && interfaceType.IsAssignableFrom(type);
		}

		/// <summary>Détermine si le type est un Nullable&lt;T&gt; (tel que 'int?' ou 'DateTime?')</summary>
		[Pure]
		public static bool IsNullableType(this Type type)
		{
			//note: techniquement, il faudrait faire tester si le type est générique et si sa définition générique est 'Nullable<>' mais c'est assez long au runtime.
			// CORRECT but SLOW: return type.IsGenericType && type.Name == "Nullable`1" && typeof(Nullable<>) == type.GetGenericTypeDefinition();
			// => On exploite le fait que que la property 'Name' de typeof(T?) ou typeof(Nullabe<T>) est toujours "Nullable`1", et qu'il est impossible de dériver une struct...
			return type.Name == "Nullable`1";
		}

		/// <summary>Détermine si une instance de ce type peut être null (Reference Type, Nullable Type)</summary>
		/// <param name="type"></param>
		/// <returns></returns>
		[Pure]
		public static bool CanAssignNull(this Type type)
		{
			return !type.IsValueType || IsNullableType(type);
		}

		[Pure]
		public static bool IsDynamicType(this Type type)
		{
			// Tous les objets dynamiques (ExpandoObject, DynamicObject, ....) implémentent l'interface IDynamicMetaObjectProvider
			return typeof(IDynamicMetaObjectProvider).IsAssignableFrom(type);
		}

		[Pure]
		public static bool IsAnonymousType(this Type type)
		{
			// Il n'est pas possible facilement de détecter les classes anonymes,
			// Les seuls éléments distinctifs sont : (cf http://stackoverflow.com/questions/315146/anonymous-types-are-there-any-distingushing-characteristics )
			// * C'est une classe
			// * Elle a l'attribut [CompilerGeneratedAttribute]
			// * Elle est sealed
			// * Elle dérive de object
			// * Elle est générique, avec autant de Type Parameters que de propriétés
			// * Elle un seul constructeur, qui prend autant de paramètres qu'il y a de propriétés
			// * Elle override Equals, GetHashcode et ToString(), et rien d'autre
			// * Son nom ressemble à "<>f_AnonymousType...." en C#, et à "VB$AnonymousType..." en VB.NET

			return type.IsClass
				&& type.IsSealed
				&& type.IsGenericType
				&& typeof(object) == type.BaseType
				&& (type.Name.StartsWith("<>f__AnonymousType", StringComparison.Ordinal) || type.Name.StartsWith("VB$AnonymousType", StringComparison.Ordinal));
				//&& type.IsDefined(typeof(System.Runtime.CompilerServices.CompilerGeneratedAttribute), false);
		}

		[Pure]
		public static bool IsOverriding(Type type, string methodName)
		{
			Contract.NotNull(type);
			var method = type.GetMethod(methodName);
			if (method == null) throw new InvalidOperationException(String.Format(CultureInfo.InvariantCulture, "There is no method {0} on type {1}", methodName, type.GetFriendlyName()));
			return IsOverriding(method);
		}

		[Pure]
		public static bool IsOverriding(MethodInfo method)
		{
			// cf http://stackoverflow.com/questions/5746447/determine-whether-a-c-sharp-method-has-keyword-override-using-reflection
			Contract.NotNull(method);
			return method.DeclaringType == method.GetBaseDefinition().DeclaringType;
		}

		/// <summary>Retourne le type de résultats retourné par un 'Task-like' (Task, Task&gt;T&lt;, ValueTask&lt;T&gt;, ...), ou null si ce n'est pas un "TaskLike"</summary>
		[Pure]
		public static Type? GetTaskLikeType(this Type taskLike)
		{
			if (taskLike == typeof(System.Threading.Tasks.Task)) return typeof(void);
			if (taskLike == typeof(System.Threading.Tasks.ValueTask)) return typeof(void);

			var t = taskLike.FindGenericType(typeof(System.Threading.Tasks.Task<>));
			if (t != null) return t.GenericTypeArguments[0];

			t = taskLike.FindGenericType(typeof(System.Threading.Tasks.ValueTask<>));
			if (t != null) return t.GenericTypeArguments[0];

			return null;
		}

		/// <summary>Indique si la propriété est un custom index (ex: "get_Item[string key]")</summary>
		[Pure]
		public static bool IsCustomIndexer(this PropertyInfo prop)
		{
			// Les indexer properties ont au moins 1 argument!
			return prop.GetIndexParameters().Length != 0;
		}

		#endregion

	}

}
