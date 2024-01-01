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

namespace Doxense.Runtime.Comparison
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using System.Linq.Expressions;
	using System.Reflection;
	using Doxense.Diagnostics.Contracts;
	using Doxense.Serialization;
	using JetBrains.Annotations;

	/// <summary>Defines a property of a model class that is "important"</summary>
	/// <remarks>Primary properties are always compared first, and are also used to compute the HashCode of the instance</remarks>
	public class PrimaryAttribute : Attribute
	{
		public PrimaryAttribute()
		{ }

		public PrimaryAttribute(int order)
		{
			this.Order = order;
		}

		public int Order { get; set; }
	}

	/// <summary>Marks a property or field as non-important</summary>
	/// <remarks>Can be used by serialization or reflection tool to skip this field</remarks>
	public class IgnoreAttribute : Attribute
	{
		// Nothing
	}

	/// <summary>Classe helper pour la comparaison de classe modèles ("POCO")</summary>
	public static class ModelComparer
	{

		private static readonly Dictionary<Type, (Delegate Comparer, Delegate HashFunction, MemberInfo[]? Members)> TypedCache = new Dictionary<Type, (Delegate, Delegate, MemberInfo[]?)>(TypeEqualityComparer.Default);

		private static readonly Dictionary<Type, (Func<object?, object?, bool> Comparer, Func<object, int> HashFunction, MemberInfo[]? Members)> BoxedCache = new Dictionary<Type, (Func<object?, object?, bool> Comparer, Func<object, int> HashFunction, MemberInfo[]? Members)>(TypeEqualityComparer.Default);

		public sealed class Comparer<T> : IEqualityComparer<T>
		{
			public static readonly Comparer<T> Default = new Comparer<T>();

			/// <summary>Lambda function qui peut comparer deux éléments de type <typeparamref name="T"/></summary>
			public Func<T?, T?, bool> Handler { get;}

			/// <summary>Lambda function qui peut calculer le hash code d'un élément de type <typeparamref name="T"/></summary>
			public Func<T, int> HashFunction { get; }

			/// <summary>Liste des fields qui doivent être comparés pour differ deux éléments de type <typeparamref name="T"/></summary>
			public MemberInfo[]? Members { get; }

			private Comparer()
			{
				var (comparer, hashFunction, members) = GetTypedPairFor(typeof(T));
				this.Handler = (Func<T?, T?, bool>) comparer;
				this.HashFunction = (Func<T, int>) hashFunction;
				this.Members = members;
			}

			public bool Equals(T? left, T? right) => this.Handler(left, right);

			public int GetHashCode(T item) => this.HashFunction(item);

			public IEnumerable<(string Name, object? Left, object? Right)> ComputeDifferences(T? left, T? right)
			{
				if (this.Members == null) throw new NotSupportedException($"Cannot list differences between objects of type {typeof(T).GetFriendlyName()}");
				
				foreach(var mbr in this.Members)
				{
					object? leftValue, rightValue;
					Type type;
					switch(mbr)
					{
						case PropertyInfo prop:
						{
							type = prop.PropertyType;
							leftValue = prop.GetValue(left);
							rightValue = prop.GetValue(right);
							break;
						}
						case FieldInfo field:
						{
							type = field.FieldType;
							leftValue = field.GetValue(left);
							rightValue = field.GetValue(right);
							break;
						}
						default:
						{
							throw new NotSupportedException("Unsupported member type " + mbr.GetType().Name);
						}
					}

					if (!GetBoxedComparerFor(type)(leftValue, rightValue))
					{
						yield return (mbr.Name, leftValue, rightValue);
					}
				}
			}
		}

		[Pure]
		public static bool Equals<T>(T? left, T? right)
		{
			return Comparer<T>.Default.Equals(left, right);
		}

		[Pure]
		public static int GetHashCode<T>(T item)
		{
			return Comparer<T>.Default.GetHashCode(item);
		}

		[Pure]
		public static IEnumerable<(string MemberName, object? LeftValue, object? RightValue)> ComputeDifferences<T>(T? left, T? right)
		{
			return Comparer<T>.Default.ComputeDifferences(left, right);
		}

		public static IEnumerable<(string MemberName, object? LeftValue, object? RightValue)> ComputeDifferences(Type type, object? left, object? right)
		{
			var t = typeof(Comparer<>).MakeGenericType(type).GetField(nameof(Comparer<int>.Default), BindingFlags.Static | BindingFlags.Public)!;
			var x = t.GetValue(null)!;
			var m = x.GetType().GetMethod(nameof(Comparer<int>.ComputeDifferences))!;
			return (IEnumerable<(string MemberName, object?, object?)>) m.Invoke(x, new [] { left, right })!;
		}

		[Pure]
		public static Func<T, T, bool> GetTypedComparerFor<T>()
		{
			return (Func<T, T, bool>) GetTypedPairFor(typeof(T)).Comparer;
		}

		[Pure]
		public static Delegate GetTypedComparerFor(Type type)
		{
			return GetTypedPairFor(type).Comparer;
		}

		[Pure]
		public static Func<T, int> GetTypedHashFunctionFor<T>()
		{
			return (Func<T, int>) GetTypedPairFor(typeof(T)).HashFunction;
		}

		[Pure]
		public static Delegate GetTypedHashFunctionFor(Type type)
		{
			return GetTypedPairFor(type).HashFunction;
		}

		internal static (Delegate Comparer, Delegate HashFunction, MemberInfo[]? diffFunction) GetTypedPairFor(Type type)
		{
			lock (TypedCache)
			{
				if (TypedCache.TryGetValue(type, out var entry))
				{
					return entry;
				}
			}
			return CreateTypedPair(type);
		}

		private static (Delegate Comparer, Delegate HashFunction, MemberInfo[]? Members) CreateTypedPair(Type type)
		{
			Contract.Debug.Requires(type != null);
			if (type == typeof(object)) throw new InvalidOperationException("Cannot create comparer for type object!");

			// génère la comparison function
			var expr = ModelComparerExpressionBuilder.GetTypedComparer(type);
			//Console.WriteLine($"{type.GetFriendlyName()}:EQ => {expr.Body.GetDebugView()}");
			var handler = expr.Compile();

			// génère la hash function
			expr = ModelHashFunctionExpressionBuilder.GetTypedHashFunction(type);
			//Console.WriteLine($"{type.GetFriendlyName()}:HF => {expr.Body.GetDebugView()}");
			var hashFunc = expr.Compile();

			var members = ModelComparerExpressionBuilder.GetMembers(type);

			lock (TypedCache)
			{
				if (!TypedCache.TryGetValue(type, out var entry))
				{
					entry = (handler, hashFunc, members);
					TypedCache[type] = entry;
				}
				return entry;
			}
		}

		[Pure]
		public static Func<object?, object?, bool> GetBoxedComparerFor(Type type)
		{
			return GetBoxedPairFor(type).Comparer;
		}

		[Pure]
		public static Func<object, int> GetBoxedHashFunctionFor(Type type)
		{
			return GetBoxedPairFor(type).HashFunction;
		}

		[Pure]
		internal static (Func<object?, object?, bool> Comparer, Func<object, int> HashFunction, MemberInfo[]? Members) GetBoxedPairFor(Type type)
		{
			lock (BoxedCache)
			{
				if (BoxedCache.TryGetValue(type, out var entry)) return entry;
			}
			return CreateBoxedPair(type);
		}

		private static (Func<object?, object?, bool> Comparer, Func<object, int> HashFunction, MemberInfo[]? Members) CreateBoxedPair(Type type)
		{
			var expr = ModelComparerExpressionBuilder.GetBoxedComparer(type);
			var comparer = (Func<object?, object?, bool>) expr.Compile();

			expr = ModelHashFunctionExpressionBuilder.GetBoxedHashFunction(type);
			var hashFunc = (Func<object, int>) expr.Compile();

			var members = ModelComparerExpressionBuilder.GetMembers(type);

			lock (BoxedCache)
			{
				if (!BoxedCache.TryGetValue(type, out var entry))
				{
					entry = (comparer, hashFunc, members);
					BoxedCache[type] = entry;
				}

				return entry;
			}
		}

		/// <summary>Contient la liste des noms de propriétés qui sont considérés comme "plus important" dans une comparaison entre deux objets</summary>
		/// <remarks>Ces champs ont une plus grande probabilité d'être différents entre deux instances, et donc permettre de short-circuiter plus rapidement une comparaison entre deux objets "différents"</remarks>
		private static readonly HashSet<string> ImportantPropertyNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
		{
			"Id",
			"Guid",
			"ItemGuid",
			"Version",
			"Modified",
			//TODO: autres champs?
		};

		/// <summary>Calcule un coefficient de "boost" qui permet de trier les champs par ordre d'importance lors de la comparaison</summary>
		private static int Boost(MemberInfo mi, object[] attributes)
		{
			Contract.Debug.Requires(mi is PropertyInfo || mi is FieldInfo);

			// Le but est d'avoir les champs qui ont le plus de chances d'être différents entre deux instances en premier, pour speeder le cas "not equal" (qui est le plus fréquent)
			// Pour emprunter l'analogie avec les selecteurs CSS, on va utiliser le triplet Emperor >  Vador > Trooper (qui correspond à 100, 10 et 1)
			// - Un attribut [Primary] compte pour un Empereur
			// - Un nom de type "Id", "Guid" ou "Version" compte pour un Vador
			// - Un type qui n'est pas une Liste, un IEnumerable, ou un type "compliqué à comparer" compte pour un Trooper
			// - Les types complexes sont en dernier

			const int EMPEROR = 100;
			const int VADER = 10;
			const int TROOPER = 1;
			//note: le boost maximum est de 111

			int boost = 0;

			var primary = GetPrimaryAttribute(attributes);
			if (primary != null)
			{
				boost += EMPEROR;
				//TODO: récupérer le field Order ? comment le gérer?
			}

			// boost les noms reconnus comme important
			if (ImportantPropertyNames.Contains(mi.Name))
			{
				boost += VADER;
			}

			// boost les types simples
			var type = GetType(mi);
			if (!IsComplexType(type))
			{
				boost += TROOPER;
			}

			return boost;
		}

		internal static Type GetType(MemberInfo mi)
		{
			switch (mi)
			{
				case PropertyInfo prop: return prop.PropertyType;
				case FieldInfo field: return field.FieldType;
				case MethodInfo method: return method.ReturnType;
				case EventInfo evt: return evt.EventHandlerType!;
				case Type t: return t;
				default: throw new ArgumentException("Unknown member type");
			}
		}

		/// <summary>Détermine si le type est "lent" à comparer</summary>
		internal static bool IsComplexType(Type type)
		{
			if (type == typeof(string) || type.IsValueType)
			{
				return false;
			}

			if (type.IsAbstract || type.IsInterface)
			{ // il faudra passer par un virtual dispatch au runtime, ce qui est potentiellement lent
				return true;
			}

			if (type.IsGenericInstanceOf(typeof(ICollection<>)))
			{ // liste ou dictionnaire devrait être énumérée, ce qui est potentiellement lent
				// note: on préfère rechercher ICollection<> que IEnumerable<> car on veut éviter d'embarquer les types qui implémentent IEnumerable<> mais qui ne sont pas vraiment des listes (ex: string, ou Span<T>, etc..)
				return true;
			}

			//TODO: more?

			return false;
		}

		/// <summary>Détermine si au moins un des attributs indique qu'il faut ignorer ce champ</summary>
		private static bool HasIgnoreAttribute(object?[] attrs)
		{
			//note: on fait du "duck typing" en ne regardant que le nom de l'attribut, pas le type exacte (incluant le namespace)
			foreach (var attr in attrs)
			{
				if (attr == null) continue;
				string name = attr.GetType().Name;
				if (name == nameof(System.Xml.Serialization.XmlIgnoreAttribute)) return true;
				if (name == "JsonIgnoreAttribute") return true; // JSON.Net, etc...
				if (name == nameof(IgnoreAttribute)) return true;
				//TODO: more?
			}
			return false;
		}

		private static bool IsMemberConsidered(MemberInfo member)
		{
			return member.MemberType == MemberTypes.Property || member.MemberType == MemberTypes.Field;
		}

		[Pure]
		private static PrimaryAttribute? GetPrimaryAttribute(object[] attrs)
		{
			foreach (var attr in attrs)
			{
				if (attr is PrimaryAttribute primary) return primary;
			}
			return null;
		}

		/// <summary>Retourne la liste des members "principaux" d'un type (marqués avec l'attribut <see cref="PrimaryAttribute"/></summary>
		/// <remarks>Si aucun champ n'est primaire, retourne une liste vide</remarks>
		internal static List<MemberInfo> GetPrimaryMembersForType(Type type)
		{
			var members = new List<MemberInfo>();

			foreach (var member in type.GetMembers(BindingFlags.Instance | BindingFlags.Public))
			{
				if (!IsMemberConsidered(member)) continue;

				var attrs = member.GetCustomAttributes(inherit: true);
				if (HasIgnoreAttribute(attrs)) continue; // ce champ est ignoré
				var primary = GetPrimaryAttribute(attrs);
				if (primary == null) continue;
				members.Add(member);
			}

			return members;
		}

		/// <summary>Détermine la liste des membres qui doivnet être comparés pour un type custom (class ou struct)</summary>
		internal static List<MemberInfo> GetSortedMembersForType(Type type)
		{
			var members = new List<(MemberInfo Member, int Boost)>();

			foreach (var member in type.GetMembers(BindingFlags.Instance | BindingFlags.Public))
			{
				if (!IsMemberConsidered(member)) continue;

				var attrs = member.GetCustomAttributes(inherit: true);
				if (HasIgnoreAttribute(attrs)) continue; // ce champ est ignoré
				int boost = Boost(member, attrs);
				members.Add((member, boost));
			}

			return members.OrderBy(x => -x.Boost).Select(x => x.Member).ToList();
		}

	}

	/// <summary>Builder qui utilise des Expression Tree pour générer du code capable de comparer deux instance de n'importe quel type "POCO"</summary>
	internal static class ModelComparerExpressionBuilder
	{

		#region README

		// Ce builder génère des Expression qui sont toujours sous la forme "EQ(T left, T right) => bool" et retourne true si left "est égual à" right.
		// Le but ici est d'être le plus otpimisé possible, et d'inline au maximum les cas simples (int, bool, string, ...) et deffer les cas complexes dans d'autre lambda (en cache)

		// Au final, les expression générées auront une forme qui ressemble à "EQ<T>(T left, T right) => /* prechecks && */ left.FOO == right.FOO && left.BAR == right.BAR && EQ<TBAZ>(left.BAZ, right.BAZ) && ..."

		// A noter que null n'est égal qu'a lui même, donc "EQ<T>(null, null) => true", mais "EQ<T>(INSTANCE, null) => false" et "EQ<T>(null, INSTANCE) => false"

		// EXPRESSIONS

		// Pour la plupart des types simples, on peut directement invoquer "==" (ou leur équivalent comme par ex pour string) 
		//
		// - EQ<int>(int left, int right) => left == right
		// - EQ<int?>(int? left, int? right) => left == right
		// - EQ<string>(string left, string right) => String.Equals(left, right); // ordinal!

		// Pour les types qui implémentent directement IEquatable<T>, on invoque cette méthode (on suppose que si elle existe, alors il y a un raison, et il faut respecter les souhaits de l'auteur du type!)
		//
		//- EQ<TEquatable>(TEQuatable left, TEQuatable right) => /* nullchecks */ && left.Equals(right);

		// A l'inverse, un champ de type class/struct, sera invoqué via une lambda (avec l'instance left et right passé en paramètre)

		// Note: pour les interfaces ou les classes abstraites, on est obligé de faire un "virutal dispatch" au runtime, c'est à dire qu'on ne peut pas directement invoquer EQ<Animal>(Animal left, Animal right),
		// car EQ<Animal> ne voit que les champs définis sur la classe abstraite. Dans ce cas, on fait un lookup au runtime du type exact des instances (ex: Cat, ou Dog), et s'il sont identique, alors ont obtenir EQ<Catr> et on l'invoque
		// en lui en passant les instances left et right castée en Cat.
		//
		// =>  EQ<Animal>(Animal left, Animal right) => left.GetType() == right.GetType() && EQ<$LEFT_TYPE>(($LEFT_TYPE) left, ($LEFT_TYPE) right)
		//
		// Pour les types qui ne sont pas explicitement abstrait, mais qui ne sont pas sealed, il reste encore une possibilité qu'il soit dérivé quelque part. Dans ce cas, on va juste etre optimiste et mettre en cache la func, mais avec un 
		// test au runtime qui repasse dans du virtual dispatch:
		//
		// => EQ<NonSealed>(NonSealed left, NonSealed right) => left.GetType() == right.GetType() && (left.GetType() == typeof(NonSealed) ? (REGULAR EQ EXPRESSION) : EQ<$LEFT_TYPE>(($LEFT_TYPE) left, ($LEFT_TYPE) right))
		//
		// Pour les types sealed, c'est plus simple, on peut mettre en cache la lambda qui compare les types, sans risques. Pour cette raison, il est recommendé de 'sealed' les types Model quand c'est possible, afin d'optimiser les comparaison!
		//
		// => EQ<Sealed>(Sealed left, Sealed right) => /*nullchecks*/ && <INLINED EXPRESSIONS>

		// CALL SITES
		// 
		// Le "call site" correspond à l'endroit ou on veut obtenir le booleen "true" ou "false" du résultat la comparaison.
		// 
		// La plupart du temps, le call site vu de l'extérieur est la Func<TModel, TModel, bool> qui compare l'objet entier. Il existe aussi une variante Func<object?, object?, bool> ("boxed") qui est utilisée pour le virtual dispatch (deux instances d'une classe abstraite)
		//
		// Pour comparer des classes (ou struct) modèles, on a aussi besoin de comparer leurs champs et propriétés. Pour les type simples, on va inliner directement l'expression correspondante et éviter un appel de lambda.
		// ex: pour un int, on ne fait pas appeler EQ<int>(left.PROP, right.PROP) mais remplacer par "left.PROP == right.PROP"
		//
		// Si par contre, un champ est une classe (ou struct), on ne peut pas inline, sinon on va se retrouver avec "... && left.PROP.FOO == right.PROP.FOO && left.PROP.BAR == right.PROP.BAR && left.PROP.BAZ == right.PROP.baz"
		// ce qui n'est pas efficace car on doit relire le champ 'PROP' plusieurs fois.
		//
		// Dans ce cas, on va mettre en cache la méthode EQ<$PROP_TYPE> dans une instance, et l'invoquer inline avec les valeurs des champs:
		// ex: ... && CACHED_EQ_FUNC_FOR_PROP(left.PROP, right.Prop) && ...

		#endregion

		public static MemberInfo[]? GetMembers(Type type)
		{
			var members = ModelComparer.GetSortedMembersForType(type);
			if (members.Count == 0) return null;

			var res = new List<MemberInfo>(members.Count);
			foreach (var mbr in members)
			{
				switch(mbr)
				{
					case PropertyInfo prop:
					{
						if (prop.IsCustomIndexer()) continue;
						res.Add(prop);
						break;
					}

					case FieldInfo field:
					{
						res.Add(field);
						break;
					}
				}
			}

			return res.ToArray();
		}

		/// <summary>Génère une lambda expression Func&lt;T, T, bool&gt; qui compare deux instances du type T</summary>
		[Pure]
		public static LambdaExpression GetTypedComparer(Type type)
		{
			Contract.NotNull(type);

			var left = Expression.Parameter(type, "left");
			var right = Expression.Parameter(type, "right");

			var body = TryGetComparerForSimpleType(type, left, right);
			if (body == null)
			{
				body = Build(type, left, right);
			}

			return Expression.Lambda(body, tailCall: true, parameters: new[] { left, right });
		}

		/// <summary>Génère une lambda expression Func&lt;object, object, bool&gt; qui compare deux instances dont le type concret dérive de T (abstract ou interface)</summary>
		[Pure]
		public static LambdaExpression GetBoxedComparer(Type type)
		{
			Contract.NotNull(type);

			var prmLeft = Expression.Parameter(typeof(object), "left");
			var prmRight = Expression.Parameter(typeof(object), "right");

			var varLeft = Expression.Variable(type, "l");
			var varRight = Expression.Variable(type, "r");

			var body = TryGetComparerForSimpleType(type, varLeft, varRight);
			if (body == null)
			{
				body = Build(type, varLeft, varRight);
			}

			var block = Expression.Block(
				new [] { varLeft, varRight },
				new []
				{
					Expression.Assign(varLeft, prmLeft.CastFromObject(type)),
					Expression.Assign(varRight, prmRight.CastFromObject(type)),
					body
				}
			);
			return Expression.Lambda(block, tailCall: true, parameters: new[] {prmLeft, prmRight});
		}

		private static Expression Build(Type type, Expression left, Expression right)
		{
			// si le type T implémente IEquatable<T>, on appel l'implémentation sous le capot

			var expr = TryGetComparerForSimpleType(type, left, right);
			if (expr != null) return expr;

			var nullableOfT = Nullable.GetUnderlyingType(type);
			if (nullableOfT != null)
			{
				return MakeNullableComparer(nullableOfT, left, right);
			}

			var equatableOfT = typeof(IEquatable<>).MakeGenericType(type);
			if (equatableOfT.IsAssignableFrom(type))
			{ // le type implémente déja IEquatable<T>, on defer vers cette implémentation
				return MakeEquatableComparer(type, equatableOfT, left, right);
			}

			//REVIEW: TODO: si c un ValueType, qui défini un opérateur "==" on pourrait aussi l'invoquer?

			if (type.IsAbstract || type.IsInterface)
			{ // dans tous les cas, il faudra passer par une query au runtime!
				// (x, y) => DynamicCompare(x, y)
				return Expression.Call(typeof(ModelComparerExpressionBuilder), nameof(RuntimeCompare), Type.EmptyTypes, new[] { Expression.Constant(type, typeof(Type)), left, right});
			}

			// Array-like
			if (type.IsArray)
			{
				if (type.GetArrayRank() == 1)
				{
					return MakeArrayComparer(type.GetElementType()!, left, right);
				}
			}

			// List-like!
			if (type.IsGenericType)
			{
				var args = type.GetGenericArguments();

				//REVIEW: pour le moment on n'implémente que les types concrets List<>, Dictionary<,>, ...
				// il faudra faire les versions IEnumerable<>/ICollection<>/IDictionary<,> etc..

				if (type.IsGenericInstanceOf(typeof(List<>)))
				{ // List<T>
					return MakeListComparer(args[0], left, right);
				}

				if (type.IsGenericInstanceOf(typeof(Dictionary<,>)))
				{ // Dictionary<TKey, TValue>
					return MakeDictionaryComparer(args[0], args[1], left, right);
				}

				if (type.IsGenericInstanceOf(typeof(HashSet<>)))
				{ // HashSet<T>
					return MakeHashSetComparer(args[0], left, right);
				}

				//TODO: other collections!
			}

			return MakeCustomComparer(type, left, right);
		}


		private static readonly MethodInfo StringEqualsOrdinal = typeof(string).GetMethod(
			"Equals",
			BindingFlags.Static | BindingFlags.Public,
			null,
			new[] {typeof(string), typeof(string)}, 
			null
		)!;

		/// <summary>Retourne un comparer pour les cas simples (int, string, guid, int?, etc..)</summary>
		private static Expression? TryGetComparerForSimpleType(Type type, Expression left, Expression right)
		{
			switch (Type.GetTypeCode(type))
			{
				case TypeCode.Boolean: 
				case TypeCode.Char: 
				case TypeCode.SByte: 
				case TypeCode.Byte: 
				case TypeCode.Int16: 
				case TypeCode.UInt16: 
				case TypeCode.Int32: 
				case TypeCode.UInt32: 
				case TypeCode.Int64: 
				case TypeCode.UInt64: 
				case TypeCode.Single: 
				case TypeCode.Double: 
				case TypeCode.Decimal: 
				case TypeCode.DateTime: 
					// "left == right"
					return Expression.Equal(left, right);

				case TypeCode.String:
					// "string.Equals(left, right)"
					return Expression.Call(
						StringEqualsOrdinal ?? throw new InvalidOperationException(),
						left,
						right
					);
			}

			if (type.IsValueType)
			{
				var nullableType = Nullable.GetUnderlyingType(type);
				if (nullableType != null)
				{
					return TryGetComparerForSimpleType(nullableType, left, right);
				}
			}

			return null;

		}

		private static bool RuntimeCompare(Type commonType, object? x, object? y)
		{
			if (ReferenceEquals(x, y)) return true;
			if (ReferenceEquals(x, null) || ReferenceEquals(y, null)) return false;

			var tx = x.GetType();
			var ty = y.GetType();
			if (tx != ty) return false;

			return StaticCompare(tx, x, y);
		}

		private static bool StaticCompare(Type type, object? x, object? y)
		{
			return ModelComparer.GetBoxedComparerFor(type)(x, y);
		}

		[Pure]
		private static Expression MakeArrayComparer(Type elementType, Expression left, Expression right)
		{
			var mi = typeof(ModelComparerExpressionBuilder).GetMethod(nameof(CompareArray), BindingFlags.Static | BindingFlags.NonPublic)!;
			mi = mi.MakeGenericMethod(elementType);

			// (left, right) => CompareArray<T>(left, right, EQ<T>)
			var func = ModelComparer.GetTypedComparerFor(elementType);
			return Expression.Call(mi, left, right, Expression.Constant(func));
		}

		private static bool CompareArray<T>(T?[]? left, T?[]? right, Func<T?, T?, bool> comparer)
		{
			if (object.ReferenceEquals(left, right)) return true; // same instance, or null == null
			if (left == null || right == null) return false;
			if (right.Length != left.Length) return false;
			for (int i = 0; i < left.Length; i++)
			{
				if (!comparer(left[i], right[i])) return false;
			}
			return true;
		}

		[Pure]
		private static Expression MakeListComparer(Type elementType, Expression left, Expression right)
		{
			var mi = typeof(ModelComparerExpressionBuilder).GetMethod(nameof(CompareList), BindingFlags.Static | BindingFlags.NonPublic)!;
			mi = mi.MakeGenericMethod(elementType);

			// (left, right) => CompareList<T>(left, right, EQ<T>)
			var func = ModelComparer.GetTypedComparerFor(elementType);
			return Expression.Call(mi, left, right, Expression.Constant(func));
		}

		private static bool CompareList<T>(List<T?>? left, List<T?>? right, Func<T?, T?, bool> comparer)
		{
			if (object.ReferenceEquals(left, right)) return true; // same instance, or null == null
			if (left == null || right == null) return false;
			if (right.Count != left.Count) return false;
			for (int i = 0; i < left.Count; i++)
			{
				if (!comparer(left[i], right[i])) return false;
			}
			return true;
		}

		[Pure]
		private static Expression MakeDictionaryComparer(Type keyType, Type valueType, Expression left, Expression right)
		{
			var mi = typeof(ModelComparerExpressionBuilder).GetMethod(nameof(CompareDictionary), BindingFlags.Static | BindingFlags.NonPublic)!;
			mi = mi.MakeGenericMethod(keyType, valueType);

			// (left, right) => CompareDictionary<TKey, TValue>(left, right, EQ<TValue>)
			var func = ModelComparer.GetTypedComparerFor(valueType);
			return Expression.Call(mi, left, right, Expression.Constant(func));
		}

		private static bool CompareDictionary<TKey, TValue>(Dictionary<TKey, TValue?>? left, Dictionary<TKey, TValue?>? right, Func<TValue?, TValue?, bool> comparer)
			where TKey: notnull
		{
			if (object.ReferenceEquals(left, right)) return true; // same instance, or null == null
			if (left == null || right == null) return false;
			if (right.Count != left.Count) return false;

			foreach (var kv in left)
			{
				if (!right.TryGetValue(kv.Key, out var value))
				{
					return false; // missing element!
				}

				if (!comparer(kv.Value, value))
				{
					return false; // value mismatch!
				}
			}

			//on a déja vérifié que le count est le même, donc si Count==Count, et que tt les éléments de { left } sont dans { right }, alors on peut en conclure que { left } == { right }
			return true;
		}

		[Pure]
		private static Expression MakeHashSetComparer(Type elementType, Expression left, Expression right)
		{
			var mi = typeof(ModelComparerExpressionBuilder).GetMethod(nameof(CompareHashSet), BindingFlags.Static | BindingFlags.NonPublic)!;
			mi = mi.MakeGenericMethod(elementType);

			//note: on laisse le HashSet se débrouiller tout seul pour la comparaison, car de toute manière il a besoin déja d'un IEqualityComparer<T> pour fonctionner tout court!

			// (left, right) => CompareHashSet<T>(left, right)
			return Expression.Call(mi, left, right);
		}

		private static bool CompareHashSet<T>(HashSet<T>? left, HashSet<T>? right)
		{
			if (object.ReferenceEquals(left, right)) return true; // same instance, or null == null
			if (left == null || right == null) return false;
			if (right.Count != left.Count) return false;
			return left.SetEquals(right);
		}

		private static Expression MakeCustomComparer(Type type, Expression left, Expression right)
		{
			//note: pour l'instant on ne sérialise que les Properties!

			var props = ModelComparer.GetSortedMembersForType(type);
			if (props.Count == 0) throw new NotSupportedException($"Does not know how to serialize class of type {type.GetFriendlyName()}: no properties found!");

			Expression? body = null;
			foreach (var mbr in props)
			{
				Expression xLeft, xRight;
				Type xType;
				switch (mbr)
				{
					case PropertyInfo prop:
					{
						if (prop.IsCustomIndexer()) continue; // skip custom indexers!
						xLeft = Expression.Property(left, prop);
						xRight = Expression.Property(right, prop);
						xType = prop.PropertyType;
						break;
					}
					case FieldInfo field:
					{
						xLeft = Expression.Field(left, field);
						xRight = Expression.Field(right, field);
						xType = field.FieldType;
						break;
					}
					default:
					{
#if DEBUG
						// Normalement on ne doit voir passer que des Fields ou des Properties!
						if (System.Diagnostics.Debugger.IsAttached) System.Diagnostics.Debugger.Break();
#endif
						continue;
					}
				}

				var expr = TryGetComparerForSimpleType(xType, xLeft, xRight);
				if (expr == null)
				{
					//REVIEW: dans certains cas, on peut faire la comparaison inline
					// - ex: struct qui est définie en tant que Field

					var func = ModelComparer.GetTypedPairFor(xType).Comparer;
					expr = Expression.Invoke(Expression.Constant(func), xLeft, xRight);
				}

				if (body == null)
				{
					body = expr;
				}
				else
				{
					body = Expression.AndAlso(body, expr);
				}
			}
			if (body == null) throw new InvalidOperationException($"Don't have any fields or property to compare for type {type.GetFriendlyName()}");

			if (type.CanAssignNull())
			{ // l'une ou l'autre des valeurs peut être null, il faut donc faire les null checks
				body = NullChecks(left, right, body);
			}

			return body;
		}

		private static Expression MakeNullableComparer(Type underlyingType, Expression left, Expression right)
		{

			var expr = TryGetComparerForSimpleType(underlyingType, left, right);
			if (expr != null)
			{
				return expr;
			}

			var func = ModelComparer.GetTypedPairFor(underlyingType).Comparer;

			// il faut construire une expression de type "left.Value ? right.HasValue && <left.Value EQ right.Value> : !right.HasValue"
			//REVIEW: on pourrait optimiser les choses en passant la valeur "by ref" ou "in"?

			expr = Expression.Condition(
				Expression.Property(left, nameof(Nullable<int>.HasValue)),
				Expression.AndAlso(
					Expression.Property(right, nameof(Nullable<int>.HasValue)),
					Expression.Invoke( // "EQ(left.Value, right.Value)"
						Expression.Constant(func),
						Expression.Property(left, nameof(Nullable<int>.Value)),
						Expression.Property(right, nameof(Nullable<int>.Value))
					)
				),
				Expression.Not(Expression.Property(right, nameof(Nullable<int>.HasValue)))
			);
			return expr;
		}

		private static Expression MakeEquatableComparer(Type type, Type equatableOfType, Expression left, Expression right)
		{
			// on préfère passer par la grande porte, ie: si le type (surtout un struct) implémente explicitement Equals(...)
			var mi = type.GetMethod("Equals", BindingFlags.Instance | BindingFlags.Public, null, new[] { type }, null);
			if (mi == null)
			{ // implémentation explicite via l'interface?
				mi = equatableOfType.GetMethod("Equals");
			}
			Contract.Debug.Assert(mi != null, "Could not find Equals(T) method on type that implements IEquatable<T> ?");

			//REVIEW: si jamais on a un "public class Cat : IEquatable<Animal>" il faudrait quand même repasser par le type abstact Animal !!!

			// left.Equals(right)
			Expression body = Expression.Call(left, mi, right);

			if (type.CanAssignNull())
			{ // l'une ou l'autre des valeurs peut être null, il faut donc faire les null checks
				body = NullChecks(left, right, body);
			}

			return body;
		}

		private static Expression NullChecks(Expression left, Expression right, Expression expr)
		{
			// ReferenceEquals(left, right) || (left != null && right != null && <left EQ right>)
			return Expression.OrElse(
				Expression.Equal(left, right), // left == right
				Expression.AndAlso(
					Expression.NotEqual(left, Expression.Default(typeof(object))), // left != null
					Expression.AndAlso(
						Expression.NotEqual(right, Expression.Default(typeof(object))), // right != null
						expr // <left EQ right>
					)
				)
			);
		}

	}

	internal static class ModelHashFunctionExpressionBuilder
	{

		#region README

		// Ici les choses sont un peu plus simples que pour la fonction de comparaison, même si les mêmes points sont toujours d'actualité (notemment les classes abstraites, interfaces, etc...)

		// HashCode(null): par convention, on va toujours retourner 0 pour null!
		// - Les specs disent que deux objets égaux doivent retourner le même hashcode, et on peut considérer que default(Chien) == default(Chat) == default(Chaussure) == null, donc ils devraient tous retourner le même hashcode (0 étant la valeur la plus logique)

		// Pour éviter les collision entre types similaires, on va également inclure le hashcode du type lui-même dans le hashcode généré, comme ca "Chien { Id, Name, Version }" et "Chaussure { Id, Name, Version }" aurront des hashs différents mêmes s'ils ont les mêmes (ID, Name, Version)

		#endregion

		[Pure]
		public static LambdaExpression GetTypedHashFunction(Type type)
		{

			var item = Expression.Parameter(type, "item");

			var body = TryGetHashFunctionForSimpleType(type, item);
			if (body == null)
			{
				body = Build(type, item);
			}

			return Expression.Lambda(body, tailCall: true, parameters: new[] {item});
		}

		/// <summary>Génère une lambda expression <c>Func&lt;object, object, bool&gt;</c> qui compare deux instances dont le type concret dérive de T (abstract ou interface)</summary>
		[Pure]
		public static LambdaExpression GetBoxedHashFunction(Type type)
		{
			Contract.NotNull(type);

			var prmItem = Expression.Parameter(typeof(object), "obj");

			var varItem = Expression.Variable(type, "item");

			var body = TryGetHashFunctionForSimpleType(type, varItem);
			if (body == null)
			{
				body = Build(type, varItem);
			}

			var block = Expression.Block(
				new [] { varItem },
				new []
				{
					Expression.Assign(varItem, prmItem.CastFromObject(type)),
					body
				}
			);
			return Expression.Lambda(block, tailCall: true, parameters: new[] {prmItem });
		}

		public static Expression Build(Type type, Expression item)
		{
			// si le type T implémente IEquatable<T>, on appel l'implémentation sous le capot

			var expr = TryGetHashFunctionForSimpleType(type, item);
			if (expr != null) return expr;

			var nullableOfT = Nullable.GetUnderlyingType(type);
			if (nullableOfT != null)
			{
				return MakeNullableHashFunction(nullableOfT, item);
			}

			//REVIEW: TODO: si c un ValueType, qui défini un opérateur "==" on pourrait aussi l'invoquer?

			// Array-like
			if (type.IsArray)
			{
				if (type.GetArrayRank() == 1)
				{
					return Expression.Constant(666); //BUGBUG: TODO: !!!!
				}
			}

			// List-like!
			if (type.IsGenericType)
			{
				if (type.IsGenericInstanceOf(typeof(List<>)))
				{
					return Expression.Constant(666); //BUGBUG: TODO: !!!!
				}
				//TODO: other collections!
			}


			if (type.IsAbstract || type.IsInterface)
			{ // dans tous les cas, il faudra passer par une query au runtime!

				// (x, y) => RuntimeHash(x, y)
				return Expression.Call(RuntimeHashMethod, new[] { Expression.Constant(type, typeof(Type)), item});
			}

			return MakeCustomHashFunction(type, item);
		}

		private static readonly MethodInfo RuntimeHashMethod = typeof(ModelHashFunctionExpressionBuilder).GetMethod(nameof(RuntimeHash), BindingFlags.Static | BindingFlags.NonPublic)!;

		private static int RuntimeHash(Type baseType, object? item)
		{
			return item != null ? ModelComparer.GetBoxedHashFunctionFor(item.GetType())(item) : 0;
		}

		#region Helpers...

		/// <summary>Valeur 0 en cache</summary>
		private static readonly Expression HashZero = Expression.Default(typeof(int));

		/// <summary>Valeur 5 en cache</summary>
		private static readonly Expression Five = Expression.Constant(5, typeof(int));

		private static readonly Expression MaxValue = Expression.Constant(int.MaxValue, typeof(int));

		[Pure]
		private static Expression CombineHash(Expression h1, Expression h2)
		{
			// ((h1 << 5) + h1) ^ (h2)
			return Expression.ExclusiveOr(
				Expression.Add(
					Expression.LeftShift(h1, Five),
					h1
				),
				h2
			);
		}

		[Pure]
		private static Expression TruncateHash(Expression h)
		{
			// h & int.MaxValue
			return Expression.And(h, MaxValue);
		}

		/// <summary>Protège l'exécution d'un hashcode contre les valeurs null</summary>
		[Pure] 
		private static Expression HashOrZeroIfNull(Expression item, Expression expr)
		{
			Contract.Debug.Requires(item != null && expr != null);
			// item != null ? (EXPR) : 0
			return Expression.Condition(
				item.IsNotNull(),
				expr,
				HashZero
			);
		}

		private static readonly MethodInfo StringComparerOrdinalGetHashCode = typeof(StringComparer).GetMethod(
			nameof(IEqualityComparer<string>.GetHashCode),
			System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public,
			null,
			new[] {typeof(string)},
			null
		)!;

		#endregion

		/// <summary>Retourne une hash function pour les cas simples (int, string, guid, int?, etc...)</summary>
		internal static Expression? TryGetHashFunctionForSimpleType(Type type, Expression item)
		{
			switch (Type.GetTypeCode(type))
			{
				case TypeCode.Boolean: 
				case TypeCode.Char: 
				case TypeCode.SByte: 
				case TypeCode.Byte: 
				case TypeCode.Int16: 
				case TypeCode.UInt16: 
				case TypeCode.Int32: 
				case TypeCode.UInt32: 
				case TypeCode.Int64: 
				case TypeCode.UInt64: 
				case TypeCode.Single: 
				case TypeCode.Double: 
				case TypeCode.Decimal: 
				case TypeCode.DateTime: 
					// "item.GetHashCode()"
					return Expression.Call(item, nameof(object.GetHashCode), Type.EmptyTypes);

				case TypeCode.String:
					// "StringComparer.Ordinal.GetHashCode(item)"
					return HashOrZeroIfNull(item, Expression.Call(Expression.Constant(StringComparer.Ordinal), StringComparerOrdinalGetHashCode, item));
			}

			Type equatableOfT = typeof(IEquatable<>).MakeGenericType(type);
			if (equatableOfT.IsAssignableFrom(type))
			{ // le type implémente déjà IEquatable<T>, donc il a très probablement aussi implémenter GetHashCode()!
				return MakeEquatableHashFunction(type, equatableOfT, item);
			}

			// nullable?
			if (type.IsValueType)
			{
				var nullableType = Nullable.GetUnderlyingType(type);
				if (nullableType != null)
				{
					var itemValue = Expression.Property(item, nameof(Nullable<int>.Value));
					var expr = TryGetHashFunctionForSimpleType(nullableType, itemValue);
					if (expr != null)
					{
						// "item.HasValue ? HASH(item.Value) : 0"
						return Expression.Condition(
							Expression.Property(item, nameof(Nullable<int>.HasValue)),
							expr,
							HashZero
						);
					}
				}
			}

			return null;
		}

		internal static Expression MakeCustomHashFunction(Type type, Expression item)
		{
			//note: pour l'instant on ne sérialise que les Properties!

			var props = ModelComparer.GetPrimaryMembersForType(type);
			if (props.Count == 0)
			{ // pas de champs spécifiés, on va jusqu'à 4 champs dans l'ordre
				props = ModelComparer.GetSortedMembersForType(type).Take(4).ToList();
			}

			if (props.Count == 0) throw new NotSupportedException($"Does not know how to serialize class of type {type.GetFriendlyName()}: no properties found!");

			var varHash = Expression.Variable(typeof(int));
			var returnTarget = Expression.Label(typeof(int));

			var body = new List<Expression>();

			if (type.CanAssignNull())
			{ // l'une ou l'autre des valeurs peut être null, il faut donc faire les null checks

				// "if (item == null) goto done;"
				body.Add(Expression.IfThen(
					item.IsNull(),
					Expression.Return(returnTarget, HashZero)
				));
			}

			bool first = true;

			foreach (var mbr in props)
			{
				Expression xItem;
				Type xType;
				switch (mbr)
				{
					case PropertyInfo prop:
					{
						if (prop.IsCustomIndexer()) continue; // skip custom indexers!
						xItem = Expression.Property(item, prop);
						xType = prop.PropertyType;
						break;
					}
					case FieldInfo field:
					{
						xItem = Expression.Field(item, field);
						xType = field.FieldType;
						break;
					}
					default:
					{
#if DEBUG
						// Normalement on ne doit voir passer que des Fields ou des Properties!
						if (System.Diagnostics.Debugger.IsAttached) System.Diagnostics.Debugger.Break();
#endif
						continue;
					}
				}

				var expr = TryGetHashFunctionForSimpleType(xType, xItem);
				if (expr == null)
				{
					//REVIEW: dans certains cas, on peut faire la comparaison inline
					// - ex: struct qui est définie en tant que Field

					var func = ModelComparer.GetTypedHashFunctionFor(xType);
					expr = Expression.Invoke(Expression.Constant(func), xItem);
				}

				if (first)
				{ // h = (EXPR);
					body.Add(Expression.Assign(varHash, expr));
					first = false;
				}
				else
				{ // h = Combine(h, EXPR);
					body.Add(Expression.Assign(varHash, CombineHash(varHash, expr)));
				}
			}
			if (first) throw new InvalidOperationException($"Don't have any fields or property to hash for type {type.GetFriendlyName()}");

			// pour éviter des collisions entre types "similaires" (ex: plusieurs types qui sont tous hashcodés sur (Id, Name, Version, ...),
			// on combine le hash avec un hashcode du type lui-même

			// "h = Combine(h, TYPE.GetHashCode())"
			body.Add(Expression.Assign(varHash, CombineHash(varHash, Expression.Constant(type.GetHashCode(), typeof(int)))));

			// "h = h & int.MaxValue";
			body.Add(Expression.Label(returnTarget, TruncateHash(varHash)));

			return Expression.Block(new [] { varHash}, body);
		}

		private static Expression MakeNullableHashFunction(Type underlyingType, Expression item)
		{
			// il faut construire une expression de type "item.HasValue ? HASH(item.Value) : 0"

			var func = ModelComparer.GetTypedHashFunctionFor(underlyingType);
			var expr = Expression.Condition(
				Expression.Property(item, nameof(Nullable<int>.HasValue)),
				Expression.Invoke(
					Expression.Constant(func),
					Expression.Property(item, nameof(Nullable<int>.Value))
				),
				HashZero
			);
			return expr;
		}

		private static Expression MakeEquatableHashFunction(Type type, Type equatableOfType, Expression item)
		{
			// on préfère passer par la grande porte, ie: si le type (surtout un struct) implémente explicitement Equals(...)

			// "item.GetHashCode()"
			Expression body = Expression.Call(item, nameof(object.GetHashCode), Type.EmptyTypes);

			if (type.CanAssignNull())
			{ // si c'est nullable, on doit écrire "item != null ? item.GetHashCode() : 0"
				//note: a priori pour l'instant on ne peut pas écrire "item?.GetHashCode()" en Expression Trees (null propagation n'est pas supporté en .NET 4.x a ce jour)
				body = HashOrZeroIfNull(item, body);
			}

			return body;
		}

	}

}
