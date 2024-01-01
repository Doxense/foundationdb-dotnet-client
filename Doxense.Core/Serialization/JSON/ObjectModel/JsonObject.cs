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
	using Doxense.Diagnostics.Contracts;
	using JetBrains.Annotations;
	using System;
	using System.Collections.Generic;
	using System.Diagnostics;
	using System.Diagnostics.CodeAnalysis;
	using System.Dynamic;
	using System.Linq;
	using System.Runtime.CompilerServices;
	using System.Runtime.Serialization;
	using System.Text;
	using Doxense.Memory;

	/// <summary>JSON Object with fields</summary>
	[Serializable]
	[DebuggerDisplay("JSON Object[{Count}] {GetCompactRepresentation(0),nq}")]
	[DebuggerTypeProxy(typeof(JsonObject.DebugView))]
	[DebuggerNonUserCode]
	public sealed class JsonObject : JsonValue, IDictionary<string, JsonValue>, IReadOnlyDictionary<string, JsonValue>, IEquatable<JsonObject>
	{
		// TODO: temporaire! a terme il faudrait implementer notre propre dico !
		private readonly Dictionary<string, JsonValue?> m_items;
		//INVARIANT: il ne doit pas y avoir de key ou de value == à null!

		//REVIEW: s'inspirer de FrugalMap ( http://referencesource.microsoft.com/#WindowsBase/src/Shared/MS/Utility/FrugalMap.cs )
		// => plusieurs variantes d'une map, suivant sa taille, qui essayent de minimiser le plus possible la taille des objets
		// => on pourrait avoir une version avec 1 field, 3, 6, peut etre jusqu'a 12, et au dela wrapper un vrai Dictionary<,>

		/// <summary>Objet vide</summary>
		public static JsonObject Empty => new JsonObject();

		[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
		private class DebugView
		{
			private readonly JsonObject m_obj;

			public DebugView(JsonObject obj)
			{
				m_obj = obj;
			}

			[DebuggerBrowsable(DebuggerBrowsableState.RootHidden)]
			public KeyValuePair<string, JsonValue>[] Items => m_obj.ToArray();
		}

		#region Constructors...

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public JsonObject()
			: this(0, StringComparer.Ordinal)
		{ }

		public JsonObject(bool ignoreCase)
			: this(0, ignoreCase ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal)
		{ }

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public JsonObject(int capacity)
			: this(capacity, StringComparer.Ordinal)
		{ }

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public JsonObject(IEqualityComparer<string>? comparer)
			: this(0, comparer ?? StringComparer.Ordinal)
		{ }

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public JsonObject(JsonObject copy)
		{
			Contract.NotNull(copy);
			m_items = new Dictionary<string, JsonValue?>(copy.m_items, copy.Comparer);
		}

		public JsonObject(int capacity, IEqualityComparer<string>? comparer)
		{
			if (capacity < 0) throw ThrowHelper.ArgumentOutOfRangeNeedNonNegNum(nameof(capacity));
			m_items = new Dictionary<string, JsonValue?>(capacity, comparer ?? StringComparer.Ordinal);
		}

		/// <summary>Crée un JsonObject à partir d'une liste de clé/valeurs</summary>
		/// <param name="items">Séquence de noms et valeurs</param>
		/// <param name="comparer">Comparateur utilisé pour les clés de ce JsonObject</param>
		/// <remarks> Si <paramref name="comparer"/> n'est pas précisé, mais que <paramref name="items"/> est un <see cref="Dictionary{K,V}"/> ou un <see cref="JsonObject"/>, le même Comparer sera utilisé. Sinon, <see cref="StringComparer.Ordinal"/> sera utilisé. </remarks>
		public JsonObject(IEnumerable<KeyValuePair<string, JsonValue>> items, IEqualityComparer<string>? comparer = null)
		{
			Contract.Debug.Requires(items != null);

			if (items is JsonObject obj)
			{ // Clone l'objet directement
				m_items = new Dictionary<string, JsonValue?>(obj.m_items, comparer ?? obj.Comparer);
				return;
			}

			// devines la capacité et le comparer...
			var map = new Dictionary<string, JsonValue?>(
				(items as ICollection<KeyValuePair<string, JsonValue>>)?.Count ?? 0,
				comparer ?? ExtractKeyComparer(items) ?? StringComparer.Ordinal
			);
			foreach (var item in items)
			{
				//note: on utilise Add(..) pour détecter les doublons
				//REVIEW: faut-il rejeter ou accepter les doublons? si on rempalce Add par [], le dernier écrase le premier...
				map.Add(item.Key, item.Value ?? JsonNull.Null);
			}
			m_items = map;

		}

		/// <summary>Crée un JsonObject à partir d'un dictionnaire de JsonValue existant</summary>
		/// <param name="items">Contenu pré-calculé du JsonObject</param>
		/// <param name="owner">Si true, l'instance utilisera <paramref name="items"/> directement. Si false, elle transfert les items dans un autre dictionnaire</param>
		/// <remarks>Cette instance utilisera <paramref name="items"/> directement, sans faire de copie</remarks>
		internal JsonObject(Dictionary<string, JsonValue?> items, bool owner)
		{
			Contract.Debug.Requires(items != null);
			m_items = owner ? items : new Dictionary<string, JsonValue?>(items, items.Comparer);
		}

		/// <summary>Crée un JsonObject à partir d'une liste dynamique de noms et de valeures</summary>
		/// <param name="items">Liste des noms des champs et des valeurs (de taille supérieure ou égale à <paramref name="count"/>)</param>
		/// <param name="count">Nombre de valeurs à lire dans <paramref name="items"/></param>
		/// <param name="comparer">Comparateur utilisé pour les clés de ce JsonObject</param>
		internal JsonObject(KeyValuePair<string, JsonValue>[] items, int count, IEqualityComparer<string> comparer)
		{
			Contract.Debug.Requires(items != null && count >= 0 && count <= items.Length && comparer != null);

			//note: on écrase en cas de doublons. L'appelant peut détecter la présence de doublons en comparent 'count' et 'dictionary.Count'
			var map = new Dictionary<string, JsonValue?>(count, comparer);
			for (int i = 0; i < count; i++)
			{
				map[items[i].Key] = items[i].Value;
			}
			m_items = map;
		}

		/// <summary>Crée un JsonObject à partir d'une liste dynamique de noms et de valeures</summary>
		/// <param name="keys">Liste des noms des champs (de taille supérieure ou égale à <paramref name="count"/>)</param>
		/// <param name="values">List des valeurs (de taille supérieure ou égale à <paramref name="count"/>)</param>
		/// <param name="count">Nombre de valeurs à lire dans <paramref name="keys"/> et <paramref name="values"/></param>
		/// <param name="comparer">Comparateur utilisé pour les clés de ce JsonObject</param>
		internal JsonObject(string[] keys, JsonValue[] values, int count, IEqualityComparer<string> comparer)
		{
			Contract.Debug.Requires(keys != null && values != null && count >= 0 && count <= keys.Length && count <= values.Length && comparer != null);

			//note: redondant avec le ctor qui prend un KeyValuePair<string, JsonValue>[], mais c'est plus pratique, pour les decodeurs qui stockent les clés et les valeurs séparemment, d'avoir deux array distinctes (vu que KVP<K,V> est immutable!)

			var map = new Dictionary<string, JsonValue?>(count, comparer);
			for (int i = 0; i < count; i++)
			{
				map[keys[i]] = values[i];
			}
			m_items = map;
		}

		/// <summary>Essayes d'extraire le KeyComparer d'un dictionnaire existant</summary>
		internal static IEqualityComparer<string>? ExtractKeyComparer<T>([NoEnumeration] IEnumerable<KeyValuePair<string, T>> items)
		{
			//note: pour le cas où T == JsonValue, on check quand même si c'est un JsonObject!
			// ReSharper disable once SuspiciousTypeConversion.Global
			// ReSharper disable once ConstantNullCoalescingCondition
			return (items as JsonObject)?.Comparer ?? (items as Dictionary<string, T>)?.Comparer;
		}

		/// <summary>Crée un objet JSON qui ne contient qu'un seul champ</summary>
		/// <param name="key0">Clé du champ</param>
		/// <param name="value0">Valeur du champ</param>
		/// <returns>JsonObject contenant un seul item</returns>
		[Pure]
		public static JsonObject Create(string key0, JsonValue value0)
		{
			return new JsonObject(1, StringComparer.Ordinal)
			{
				[key0] = value0 ?? JsonNull.Null
			};
		}

		/// <summary>Crée un objet JSON qui ne contient que deux champs</summary>
		/// <param name="key0">Clé du premier champ</param>
		/// <param name="value0">Valeur du premier champ</param>
		/// <param name="key1">Clé du deuxième champ</param>
		/// <param name="value1">Valeur du deuxième champ</param>
		/// <returns>JsonObject contenant deux items</returns>
		[Pure]
		public static JsonObject Create(string key0, JsonValue value0, string key1, JsonValue value1)
		{
			return new JsonObject(2, StringComparer.Ordinal)
			{
				[key0] = value0 ?? JsonNull.Null,
				[key1] = value1 ?? JsonNull.Null,
			};
		}

		/// <summary>Crée un objet JSON qui ne contient que trois champs</summary>
		/// <param name="key0">Clé du premier champ</param>
		/// <param name="value0">Valeur du premier champ</param>
		/// <param name="key1">Clé du deuxième champ</param>
		/// <param name="value1">Valeur du deuxième champ</param>
		/// <param name="key2">Clé du troisième champ</param>
		/// <param name="value2">Valeur du troisième champ</param>
		/// <returns>JsonObject contenant trois items</returns>
		[Pure]
		public static JsonObject Create(string key0, JsonValue value0, string key1, JsonValue value1, string key2, JsonValue value2)
		{
			return new JsonObject(3, StringComparer.Ordinal)
			{
				[key0] = value0 ?? JsonNull.Null,
				[key1] = value1 ?? JsonNull.Null,
				[key2] = value2 ?? JsonNull.Null
			};
		}

		/// <summary>Crée un objet JSON qui ne contient que quatre champs</summary>
		/// <param name="key0">Clé du premier champ</param>
		/// <param name="value0">Valeur du premier champ</param>
		/// <param name="key1">Clé du deuxième champ</param>
		/// <param name="value1">Valeur du deuxième champ</param>
		/// <param name="key2">Clé du troisième champ</param>
		/// <param name="value2">Valeur du troisième champ</param>
		/// <param name="key3">Clé du quatrième champ</param>
		/// <param name="value3">Valeur du quatrième champ</param>
		/// <returns>JsonObject contenant trois items</returns>
		[Pure]
		public static JsonObject Create(string key0, JsonValue value0, string key1, JsonValue value1, string key2, JsonValue value2, string key3, JsonValue value3)
		{
			return new JsonObject(4, StringComparer.Ordinal)
			{
				[key0] = value0 ?? JsonNull.Null,
				[key1] = value1 ?? JsonNull.Null,
				[key2] = value2 ?? JsonNull.Null,
				[key3] = value3 ?? JsonNull.Null
			};
		}

		/// <summary>Transforme un objet CLR en un JsonObject</summary>
		/// <typeparam name="T">Type de l'objet à convertir</typeparam>
		/// <param name="value">Instance de l'objet à convertir</param>
		/// <returns>JsonObject correspondant, ou null si <paramref name="value"/> est null</returns>
		[ContractAnnotation("value:notnull => notnull")]
		[return: NotNullIfNotNull("value")]
		public static JsonObject? FromObject<T>(T value)
		{
			//REVIEW: que faire si c'est null? Json.Net throw une ArgumentNullException dans ce cas, et ServiceStack ne gère pas de DOM de toutes manières...
			return CrystalJsonDomWriter.Default.ParseObject(value, typeof(T)).AsObject(required: false);
		}

		[ContractAnnotation("value:notnull => notnull")]
		[return: NotNullIfNotNull("value")]
		public static JsonObject? FromObject<T>(T value, CrystalJsonSettings settings, ICrystalJsonTypeResolver? resolver = null)
		{
			//REVIEW: que faire si c'est null? Json.Net throw une ArgumentNullException dans ce cas, et ServiceStack ne gère pas de DOM de toutes manières...
			return CrystalJsonDomWriter.Create(settings, resolver).ParseObject(value, typeof(T)).AsObject(required: false);
		}

		/// <summary>Convertit un dictionnaire en JsonObject, en convertissant chaque valeur du dictionnaire en JsonValue</summary>
		/// <returns>Ne pas utiliser cette méthode pour *construire* un JsonObject! Elle n'est à utiliser que pour s'interfacer avec une API qui utilise des dictionnaires, comme par exemple OWIN</returns>
		public static JsonObject FromDictionary(IDictionary<string, object> members, IEqualityComparer<string>? comparer = null)
		{
			Contract.NotNull(members);

			comparer ??= ExtractKeyComparer(members) ?? StringComparer.Ordinal;

			var items = new Dictionary<string, JsonValue?>(members.Count, comparer);
			foreach (var kvp in members)
			{
				items.Add(kvp.Key, JsonValue.FromValue(kvp.Value));
			}
			return new JsonObject(items, owner: true);
		}

		public static JsonObject FromDictionary<TValue>(IDictionary<string, TValue> members, Func<TValue, JsonValue?> valueSelector, IEqualityComparer<string>? comparer = null)
		{
			Contract.NotNull(members);

			comparer ??= ExtractKeyComparer(members) ?? StringComparer.Ordinal;

			var items = new Dictionary<string, JsonValue?>(members.Count, comparer);
			foreach (var kvp in members)
			{
				items.Add(kvp.Key, valueSelector(kvp.Value) ?? JsonNull.Missing);
			}
			return new JsonObject(items, owner: true);
		}

		public static JsonObject FromDictionary<TValue>(IDictionary<string, TValue> members, Func<TValue, TValue> valueSelector, IEqualityComparer<string>? comparer = null)
		{
			Contract.NotNull(members);

			comparer ??= ExtractKeyComparer(members) ?? StringComparer.Ordinal;

			var items = new Dictionary<string, JsonValue?>(members.Count, comparer);
			var context = new CrystalJsonDomWriter.VisitingContext();
			foreach (var kvp in members)
			{
				items.Add(kvp.Key, JsonValue.FromValue(CrystalJsonDomWriter.Default, ref context, valueSelector(kvp.Value)));
			}
			return new JsonObject(items, owner: true);
		}

		public static JsonObject FromValues<TElement>(IEnumerable<TElement> source, Func<TElement, string> keySelector, Func<TElement, JsonValue?> valueSelector, IEqualityComparer<string>? comparer = null)
		{
			var obj = new JsonObject((source as ICollection<TElement>)?.Count ?? 0, comparer ?? StringComparer.Ordinal);
			foreach (var item in source)
			{
				obj.Add(keySelector(item), valueSelector(item) ?? JsonNull.Missing);
			}
			return obj;
		}

		public static JsonObject FromValues<TElement, TValue>(IEnumerable<TElement> source, Func<TElement, string> keySelector, Func<TElement, TValue> valueSelector, IEqualityComparer<string>? comparer = null)
		{
			var obj = new JsonObject((source as ICollection<TElement>)?.Count ?? 0, comparer ?? StringComparer.Ordinal);
			var context = new CrystalJsonDomWriter.VisitingContext();
			foreach (var item in source)
			{
				obj.Add(keySelector(item), JsonValue.FromValue<TValue>(CrystalJsonDomWriter.Default, ref context, valueSelector(item)));
			}
			return obj;
		}

		private static System.Runtime.Serialization.FormatterConverter? CachedFormatterConverter;

		/// <summary>Serialize an <see cref="Exception"/> into a JSON object</summary>
		/// <returns></returns>
		/// <remarks>
		/// The exception must implement <see cref="ISerializable"/>, and CANNOT contain cycles or self-references!
		/// The JSON object produced MAY NOT be deserializable back into the original exception type!
		/// </remarks>
		[Pure]
#if NET8_0_OR_GREATER
		[Obsolete("Formatter-based serialization is obsolete and should not be used.")]
#endif
		public static JsonObject FromException(Exception ex, bool includeTypes = true)
		{
			Contract.NotNull(ex);
			if (!(ex is ISerializable ser))
			{
				throw new JsonSerializationException($"Cannot serialize exception of type '{ex.GetType().FullName}' because it is not marked as Serializable.");
			}

			return FromISerializable(ser, includeTypes);
		}

		/// <summary>Serialize a type that implements <see cref="ISerializable"/> into a JSON object representation</summary>
		/// <remarks>
		/// The JSON object produced MAY NOT be deserializable back into the original exception type!
		/// </remarks>
		[Pure]
#if NET8_0_OR_GREATER
		[Obsolete("Formatter-based serialization is obsolete and should not be used.")]
#endif
		public static JsonObject FromISerializable(ISerializable value, bool includeTypes = true, CrystalJsonSettings? settings = null, ICrystalJsonTypeResolver? resolver = null)
		{
			Contract.NotNull(value);

			settings ??= CrystalJsonSettings.Json;
			resolver ??= CrystalJson.DefaultResolver;

			var formatter = CachedFormatterConverter ??= new FormatterConverter();
			var info = new SerializationInfo(value.GetType(), formatter);
			var ctx = new StreamingContext(StreamingContextStates.Persistence);

			value.GetObjectData(info, ctx);

			var obj = new JsonObject();
			var it = info.GetEnumerator();
			{
				while (it.MoveNext())
				{
					object? x = it.Value;
					if (includeTypes)
					{ // round-trip mode: "NAME: [ TYPE, VALUE ]"
						var v = x is ISerializable ser
							? FromISerializable(ser, includeTypes: true, settings: settings, resolver: resolver)
							: FromValue(x, it.ObjectType, settings, resolver);
						// even if the value is null, we still have to provide the type!
						obj[it.Name] = JsonArray.Create(JsonString.Return(it.ObjectType), v);
					}
					else
					{ // compact mode: "NAME: VALUE"

						// since we don't care to be deserializable, we can ommit 'null' items
						if (x == null) continue;

						var v = x is ISerializable ser
							? FromISerializable(ser, includeTypes: false, settings: settings, resolver: resolver)
							: FromValue(x, settings, resolver);

						obj[it.Name] = v;
					}
				}
			}

			return obj;
		}

		#endregion

		public int Count => m_items.Count;

		ICollection<string> IDictionary<string, JsonValue>.Keys => m_items.Keys;

		IEnumerable<string> IReadOnlyDictionary<string, JsonValue>.Keys => m_items.Keys;

		public Dictionary<string, JsonValue>.KeyCollection Keys => m_items.Keys!;

		ICollection<JsonValue> IDictionary<string, JsonValue>.Values => m_items.Values!;

		IEnumerable<JsonValue> IReadOnlyDictionary<string, JsonValue>.Values => m_items.Values!;

		public Dictionary<string, JsonValue>.ValueCollection Values => m_items.Values!;

		public Dictionary<string, JsonValue>.Enumerator GetEnumerator()
		{
			return m_items.GetEnumerator()!;
		}

		IEnumerator<KeyValuePair<string, JsonValue>> IEnumerable<KeyValuePair<string, JsonValue>>.GetEnumerator()
		{
			return m_items.GetEnumerator();
		}

		System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
		{
			return m_items.GetEnumerator();
		}

		public bool IsReadOnly => false;

		public IEqualityComparer<string> Comparer => m_items.Comparer;

		public override JsonValue this[string key]
		{
			get
			{
				Contract.Debug.Requires(key != null);
				return m_items.TryGetValue(key, out var value) ? (value ?? JsonNull.Null) : JsonNull.Missing;
			}
			set
			{
				Contract.Debug.Requires(key != null && !object.ReferenceEquals(this, value));
				m_items[key] = value ?? JsonNull.Null;
			}
		}

		[ContractAnnotation("halt<=key:null; =>true,value:notnull; =>false,value:null")]
		public bool TryGetValue(string key, [MaybeNullWhen(false)] out JsonValue value)
		{
			Contract.NotNull(key);
			if (m_items.TryGetValue(key, out value))
			{
				value ??= JsonNull.Null;
				return true;
			}
			return false;
		}

		[ContractAnnotation("halt<=key:null; =>true,array:notnull; =>false,array:null")]
		public bool TryGetArray(string key, [MaybeNullWhen(false)] out JsonArray array)
		{
			Contract.NotNull(key);
			m_items.TryGetValue(key, out var value);
			array = value as JsonArray;
			return array != null;
		}

		[ContractAnnotation("halt<=key:null; =>true,obj:notnull; =>false,obj:null")]
		public bool TryGetObject(string key, [MaybeNullWhen(false)] out JsonObject obj)
		{
			Contract.NotNull(key);
			m_items.TryGetValue(key, out var value);
			obj = value as JsonObject;
			return obj != null;
		}

		public void Add(string key, JsonValue value)
		{
			Contract.Debug.Requires(key != null && !object.ReferenceEquals(this, value));
			m_items.Add(key, value ?? JsonNull.Null);
		}

		public void Add(KeyValuePair<string, JsonValue> item)
		{
			Contract.Debug.Requires(item.Key != null && item.Value != null);
			((ICollection<KeyValuePair<string, JsonValue>>) m_items).Add(item);
		}

		public bool Remove(string key)
		{
			Contract.Debug.Requires(key != null);
			return m_items.Remove(key);
		}

		public bool Remove(KeyValuePair<string, JsonValue> keyValuePair)
		{
			Contract.Debug.Requires(keyValuePair.Key != null);
			//BUGBUG: gérer la comparaison avec .Value avant de delete!
			return m_items.Remove(keyValuePair.Key);
		}

		public void Clear()
		{
			m_items.Clear();
		}

		protected override JsonValue Clone(bool deep)
		{
			return this.Copy(deep);
		}

		public new JsonObject Copy()
		{
			return this.Copy(false);
		}

		public new JsonObject Copy(bool deep)
		{
			return JsonObject.Copy(m_items, deep);
		}

		public static JsonObject Copy(IDictionary<string, JsonValue?> value, bool deep)
		{
			Contract.NotNull(value);

			Dictionary<string, JsonValue?> obj;
			if (deep)
			{
				obj = new Dictionary<string, JsonValue?>(value.Count, ExtractKeyComparer(value));
				foreach (var kvp in value)
				{
					obj[kvp.Key] = kvp.Value?.Copy(deep: true);
				}
			}
			else
			{
				obj = new Dictionary<string, JsonValue?>(value, ExtractKeyComparer(value));
			}
			return new JsonObject(obj, owner:  true);
		}

		#region Public Properties...

		/// <summary>Type d'objet JSON</summary>
		public override JsonType Type => JsonType.Object;

		/// <summary>Indique s'il s'agit de la valeur par défaut du type ("vide")</summary>
		public override bool IsDefault => this.Count == 0;

		/// <summary>Indique si l'objet contient des valeurs</summary>
		public bool HasValues => this.Count > 0;

		/// <summary>Retourne la valeur de l'attribut "__class", ou null si absent (ou pas une chaine)</summary>
		public string? CustomClassName => Get<string>(JsonTokens.CustomClassAttribute);

		#endregion

		#region Getters...

		[ContractAnnotation("required:true => notnull")]
		private TJson? InternalGet<TJson>(JsonType expectedType, string key, bool required)
			where TJson : JsonValue
		{
			if (!TryGetValue(key, out var value))
			{ // La propriété n'est pas définie dans l'objet
				if (required) JsonValueExtensions.FailFieldIsNullOrMissing(key);
				return null;
			}
			if (value.Type == JsonType.Null)
			{ // La propriété existe mais contient null
				if (required) JsonValueExtensions.FailFieldIsNullOrMissing(key);
				return null;
			}
			if (value.Type != expectedType)
			{ // existe mais pas du type attendu ? :(
				throw Error_ExistingKeyTypeMismatch(key, value, expectedType);
			}
			return (TJson)value;
		}

		[Pure, MethodImpl(MethodImplOptions.NoInlining)]
		private static ArgumentException Error_ExistingKeyTypeMismatch(string key, JsonValue value, JsonType expectedType)
		{
			return new ArgumentException($"The specified key '{key}' exists, but is a {value.Type} instead of expected {expectedType}", nameof(key));
		}

		public bool ContainsKey(string key)
		{
			//note: retourne true même si la propriété vaut null
			return m_items.ContainsKey(key);
		}

		bool ICollection<KeyValuePair<string, JsonValue>>.Contains(KeyValuePair<string, JsonValue> keyValuePair)
		{
			return ((ICollection<KeyValuePair<string, JsonValue>>)m_items).Contains(keyValuePair);
		}


		/// <summary>Indique si la propriété <paramref name="key"/> existe et contient une valeur différent de null</summary>
		/// <param name="key">Nom de la clé à vérifier</param>
		/// <returns>Retourne false si la clé n'existe pas, ou s'il est contient null.</returns>
		/// <example>
		/// { Foo: "..." }.Has("Foo") => true
		/// { Foo: ""    }.Has("Foo") => true    // présent même si vide
		/// { Foo: null  }.Has("Foo") => false // présent mais null!
		/// { Bar: ".."  }.Has("Foo") => false // absent!
		/// </example>
		public bool Has(string key)
		{
			return TryGetValue(key, out var value) && !value.IsNullOrMissing();
		}

		/// <summary>Retourne la valeur d'une propriété de cet objet</summary>
		/// <param name="key">Nom de la propriété recherchée</param>
		/// <returns>Valeur de la propriété <paramref name="key"/> convertit en <typeparamref name="T"/>, ou default(<typeparamref name="T"/>) si la propriété contient null ou n'existe pas.</returns>
		/// <example>
		/// obj.Get&lt;string&gt;("FieldThatExists") // returns the value of the field as an int
		/// obj.Get&lt;string&gt;("FieldThatIsMissing") // returns null
		/// obj.Get&lt;string&gt;("FieldThatIsNull") // returns null
		/// obj.Get&lt;int&gt;("FieldThatIsMissing") // returns 0
		/// obj.Get&lt;int&gt;("FieldThatIsNull") // returns 0
		/// </example>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public T? Get<T>(string key)
		{
			return this[key].As<T>();
		}

		/// <summary>Retourne la valeur d'une propriété de cet objet</summary>
		/// <param name="key">Nom de la propriété recherchée</param>
		/// <param name="value">Valeur de la propriété <paramref name="key"/> convertit en <typeparamref name="T"/>, ou default(<typeparamref name="T"/>) si la propriété contient null ou n'existe pas.</param>
		/// <example>
		/// obj.TryGet&lt;string&gt;("FieldThatExists") // returns the value of the field as an int
		/// obj.Get&lt;string&gt;("FieldThatIsMissing") // returns null
		/// obj.Get&lt;string&gt;("FieldThatIsNull") // returns null
		/// obj.Get&lt;int&gt;("FieldThatIsMissing") // returns 0
		/// obj.Get&lt;int&gt;("FieldThatIsNull") // returns 0
		/// </example>
		public bool TryGet<T>(string key, out T? value)
		{
			if (TryGetValue(key, out var item) && !item.IsNullOrMissing())
			{
				value = item.As<T>();
				return true;
			}
			value = default;
			return true;
		}

		/// <summary>Retourne la valeur d'une propriété de cet objet, avec une contrainte de présence optionnelle</summary>
		/// <param name="key">Nom de la propriété recherchée</param>
		/// <param name="required">Si true, une exception est lancée si la propriété n'existe pas où vaut null. Si false, retourne default(<typeparamref name="T"/>) si la propriété est manquante ou vaut explicitement null</param>
		/// <returns>
		/// Valeur de la propriété <paramref name="key"/> convertit en <typeparamref name="T"/>, ou default(<typeparamref name="T"/>) si la propriété contient null ou n'existe pas.
		/// Si <typeparamref name="T"/> est un ValueType qui n'est pas nullable, et que le champ est manquant (avec <paramref name="required"/> == false), alors c'est le "zéro" du type qui sera retourné (0, false, Guid.Empty, ...)
		/// Si par contre <typeparamref name="T"/> est un <see cref="Nullable{T}"/> alors c'est bien 'null' qui sera retourné dans ce cas.
		/// </returns>
		/// <example>
		/// obj.Get&lt;int&gt;"FieldThatExists", required: true) // returns the value of the field as an int
		/// obj.Get&lt;bool&gt;"FieldThatExists", required: true) // ALWAYS specify 'required' to make sure not to call the Get&lt;bool&gt; with defaultValue by mistake!
		/// obj.Get&lt;string&gt;"FieldThatIsMissing", required: true) // => throws
		/// obj.Get&lt;string&gt;"FieldThatIsNull", required: true) // => throws
		/// </example>
		/// <remarks> Cette méthode est équivalente à <code>obj[key].RequiredField(key).As&lt;T&gt;()</code> </remarks>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		[ContractAnnotation("required:true => notnull")]
		public T? Get<T>(string key, bool required)
		{
			var val = this[key];
			if (required) val = val.RequiredField(key);
			return val.As<T>();
		}

		/// <summary>Retourne la valeur d'une propriété de cet objet, ou une valeur par défaut si elle n'existe pas</summary>
		/// <param name="key">Nom de la propriété recherchée</param>
		/// <param name="resolver">Résolveur spécifique utilisé pour la conversion.</param>
		/// <returns>Valeur de la propriété <paramref name="key"/> convertit en <typeparamref name="T"/>, ou default(<typeparamref name="T"/>} si la propriété contient null ou n'existe pas.</returns>
		/// <remarks>Cette méthode est équivalente à <code>obj[key].As&lt;T&gt;(defaultValue, resolver)</code></remarks>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public T? Get<T>(string key, ICrystalJsonTypeResolver resolver)
		{
			return this[key].As<T>(resolver);
		}

		/// <summary>Retourne la valeur JSON d'une propriété de cet objet</summary>
		/// <param name="key">Nom de la propriété recherchée</param>
		/// <returns>Valeur de la propriété <paramref name="key"/> castée en JsonObject, <see cref="JsonNull.Null"/> si la propriété contient null, ou <see cref="JsonNull.Missing"/> si la propriété n'existe pas.</returns>
		/// <remarks>Si la valeur est un vrai nul (ie: default(objet)), alors JsonNull.Null est retourné à la place.</remarks>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public JsonValue GetValue(string key)
		{
			return this[key];
		}

		/// <summary>Retourne la valeur JSON d'une propriété de cet objet</summary>
		/// <param name="key">Nom de la propriété recherchée</param>
		/// <param name="required"></param>
		/// <returns>Valeur de la propriété <paramref name="key"/> castée en JsonObject, <see cref="JsonNull.Null"/> si la propriété contient null, ou <see cref="JsonNull.Missing"/> si la propriété n'existe pas.</returns>
		/// <remarks>Si la valeur est un vrai nul (ie: default(objet)), alors JsonNull.Null est retourné à la place.</remarks>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public JsonValue GetValue(string key, bool required)
		{
			var val = this[key];
			if (required) val = val.RequiredField(key);
			return val;
		}

		/// <summary>Retourne la valeur JSON d'une propriété de cet objet, ou une valeur JSON par défaut</summary>
		/// <param name="key">Nom de la propriété recherchée</param>
		/// <param name="missingValue">Valeur par défaut retournée si l'objet ne contient cette propriété</param>
		/// <returns>Valeur de la propriété <paramref name="key"/> castée en JsonObject, <see cref="JsonNull.Null"/> si la propriété existe et contient null, ou <paramref name="missingValue"/> si la propriété n'existe pas.</returns>
		/// <remarks>Si la valeur est un vrai null (ie: default(object)), alors JsonNull.Null est retourné à la place.</remarks>
		[Pure, ContractAnnotation("halt<=key:null")]
		public JsonValue GetValueOrDefault(string key, JsonValue missingValue)
		{
			return TryGetValue(key, out var value) ? (value ?? JsonNull.Null) : (missingValue ?? JsonNull.Missing);
		}

		/// <summary>Retourne la valeur d'une propriété de type JsonObject</summary>
		/// <param name="key">Nom de la propriété qui contient le sous-objet recherché</param>
		/// <returns>Valeur de la propriété <paramref name="key"/> castée en JsonObject, ou null si la propriété contient null ou n'existe pas. Génère une exception si la propriété ne contient pas un object.</returns>
		/// <exception cref="ArgumentException">Si l'objet contient une propriété nommée <paramref name="key"/>, mais qui n'est ni un JsonObject, ni null.</exception>
		[Pure]
		public JsonObject? GetObject(string key)
		{
			return InternalGet<JsonObject>(JsonType.Object, key, required: false);
		}

		/// <summary>Retourne la valeur d'une propriété de type JsonObject</summary>
		/// <param name="key">Nom de la propriété qui contient le sous-objet recherché</param>
		/// <param name="required">Si <b>true</b> et que le champ n'existe pas, ou contient null, une exception est lancée. Sinon, la méthode retourne null.</param>
		/// <returns>Valeur de la propriété <paramref name="key"/> castée en JsonObject, ou null si la propriété contient null ou n'existe pas et <paramref name="required"/> est <b>false</b>. Génère une exception si la propriété ne contient pas un object.</returns>
		/// <exception cref="ArgumentException">Si l'objet contient une propriété nommée <paramref name="key"/>, mais qui n'est ni un JsonObject, ni null.</exception>
		[Pure, ContractAnnotation("required:true => notnull")]
		public JsonObject? GetObject(string key, bool required)
		{
			return InternalGet<JsonObject>(JsonType.Object, key, required);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public JsonObject? GetObjectPath(string path)
		{
			return GetPath(path).AsObject(required: false);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining), ContractAnnotation("required:true => notnull")]
		public JsonObject? GetObjectPath(string path, bool required)
		{
			return GetPath(path).AsObject(required);
		}

		/// <summary>Retourne la valeur d'une propriété de type JsonArray</summary>
		/// <param name="key">Nom de la propriété qui contient l'array recherchée</param>
		/// <returns>Valeur de la propriété <paramref name="key"/> castée en JsonArray, ou null si la propriété contient null ou n'existe pas. Génère une exception si la propriété ne contient pas une array.</returns>
		/// <exception cref="ArgumentException">Si l'objet contient une propriété nommée <paramref name="key"/>, mais qui n'est ni une JsonArray, ni null.</exception>
		[Pure]
		public JsonArray? GetArray(string key)
		{
			return InternalGet<JsonArray>(JsonType.Array, key, required: false);
		}

		/// <summary>Retourne la valeur d'une propriété de type JsonArray</summary>
		/// <param name="key">Nom de la propriété qui contient l'array recherchée</param>
		/// <param name="required">Si <b>true</b> et que le champ n'existe pas, ou contient <b>null</b>, une exception est lancée. Sinon, la méthode retourne null.</param>
		/// <returns>Valeur de la propriété <paramref name="key"/> castée en JsonArray, ou null si la propriété contient null ou n'existe pas et que <paramref name="required"/> est <b>false</b>. Génère une exception si la propriété ne contient pas une array.</returns>
		/// <exception cref="ArgumentException">Si l'objet contient une propriété nommée <paramref name="key"/>, mais qui n'est ni une JsonArray, ni null.</exception>
		[Pure, ContractAnnotation("required:true => notnull")]
		public JsonArray? GetArray(string key, bool required)
		{
			return InternalGet<JsonArray>(JsonType.Array, key, required);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public JsonArray? GetArrayPath(string path)
		{
			return GetPath(path).AsArray(required: false);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining), ContractAnnotation("required:true => notnull")]
		public JsonArray? GetArrayPath(string path, bool required)
		{
			return GetPath(path).AsArray(required);
		}

		/// <summary>Retourne un objet fils, en le créant (vide) au besoin</summary>
		/// <param name="path">Path vers le fils (peut inclure des '.')</param>
		/// <returns>Object correspondant, ou object vide</returns>
		/// <example>{ }.GetOrCreate("foo").Set("bar", 123) => { "foo": { "bar": 123 } }
		/// { }.GetOrCreate("foo.bar").Set("baz", 123) => { "foo": { "bar": { "baz": 123 } } }</example>
		/// <remarks>Si un parent n'existe pas, il est également créé</remarks>
		/// <exception cref="System.ArgumentNullException">Si <paramref name="path"/> est null (ou vide)</exception>
		/// <exception cref="System.ArgumentException">Si un des élément dans <paramref name="path"/> existe et n'est pas un objet</exception>
		public JsonObject GetOrCreateObject(string path)
		{
			Contract.NotNullOrEmpty(path);

			return (JsonObject) SetPathInternal(path, null, JsonType.Object);
		}

		/// <summary>Retourne un objet fils, en le créant (vide) au besoin</summary>
		/// <param name="path">Path vers le fils (peut inclure des '.')</param>
		/// <returns>Object correspondant, ou object vide</returns>
		/// <example>{ }.GetOrCreate("foo").Set("bar", 123) => { "foo": { "bar": 123 } }
		/// { }.GetOrCreate("foo.bar").Set("baz", 123) => { "foo": { "bar": { "baz": 123 } } }</example>
		/// <remarks>Si un parent n'existe pas, il est également créé</remarks>
		/// <exception cref="System.ArgumentNullException">Si key est null (ou vide)</exception>
		/// <exception cref="System.ArgumentException">Si un des élément dans key existe et n'est pas un objet</exception>
		public JsonArray GetOrCreateArray(string path)
		{
			Contract.NotNullOrEmpty(path);

			return (JsonArray) SetPathInternal(path, null, JsonType.Array);
		}

		/// <summary>Retourne une valeur à partir de son chemin</summary>
		/// <param name="path">Chemin vers la valeur à lire, au format "foo", "foo.bar" ou "foo[2].baz"</param>
		/// <returns>Valeur correspondante, ou <see cref="JsonNull.Missing"/> si au moins une des composantes du path n'est pas trouvée</returns>
		[Pure]
		public JsonValue GetPath(string path)
		{
			Contract.NotNullOrEmpty(path);

			JsonValue current = this;
			var tokenizer = new JPathTokenizer(path);
			string? name = null;

			while (true)
			{
				var token = tokenizer.ReadNext();
				switch (token)
				{
					case JPathToken.End:
					{
						return name != null ? current[name] : current;
					}
					case JPathToken.Identifier:
					{
						name = tokenizer.GetIdentifierName();
						break;
					}
					case JPathToken.ObjectAccess:
					case JPathToken.ArrayIndex:
					{
						if (name != null)
						{
							var child = current[name];
							if (child.IsNullOrMissing()) return JsonNull.Missing;
							current = child;
							name = null;
						}
						if (token == JPathToken.ArrayIndex)
						{
							int index = tokenizer.GetArrayIndex();
							var child = current[index];
							if (child.IsNullOrMissing()) return JsonNull.Null;
							current = child;
						}
						break;
					}
					default:
					{
						throw ThrowHelper.InvalidOperationException("Unexpected {0} at offset {1}: '{2}'", token, tokenizer.Offset, path);
					}
				}
			}
		}

		/// <summary>Retourne une valeur à partir de son chemin</summary>
		/// <param name="path">Chemin vers la valeur à lire, au format "foo", "foo.bar" ou "foo[2].baz"</param>
		/// <param name="required">Si true et que le champ n'existe pas dans l'objet, une exception est générée</param>
		/// <returns>Valeur correspondante, ou <see cref="JsonNull.Missing"/> si au moins une des composantes du path n'est pas trouvée</returns>
		[Pure, ContractAnnotation("required:true => notnull")]
		public T? GetPath<T>(string path, bool required = false)
		{
			var val = GetPath(path);
			if (required) val = val.RequiredPath(path);
			return val.As<T>();
		}

		/// <summary>Retourne ou crée le fils d'un objet, qui doit lui-même être un objet</summary>
		/// <param name="current">Noeud courant (doit être un objet)</param>
		/// <param name="name">Nom du fils de <paramref name="current"/> qui devrait être un objet (ou null)</param>
		/// <param name="createIfMissing">Si true, crée l'objet s'il n'existait pas. Si false, retourne null</param>
		/// <returns>Valeur du fils, initialisée à un objet vide si manquante</returns>
		[Pure, ContractAnnotation("createIfMissing:true => notnull")]
		private static JsonObject? GetOrCreateChildObject(JsonValue current, string? name, bool createIfMissing)
		{
			Contract.Debug.Assert(current != null && current.Type == JsonType.Object);

			JsonValue child;
			if (name != null)
			{
				child = current[name];
				if (child.IsNullOrMissing())
				{
					if (!createIfMissing) return null;
					child = JsonObject.Empty;
					current[name] = child;
				}
				else if (!child.IsMap)
				{
					throw ThrowHelper.InvalidOperationException("The specified key '{0}' exists, but is of type {1} instead of expected Object", name, child.Type);
				}
			}
			else
			{
				if (!current.IsMap) throw ThrowHelper.InvalidOperationException("Selected value was of type {0} instead of expected Object", current.Type);
				child = current;
			}
			return (JsonObject) child;
		}

		/// <summary>Retourne ou crée le fils d'un objet, qui doit être une array</summary>
		/// <param name="current">Noeud courrant (doit être un objet)</param>
		/// <param name="name">Nom du fils de <paramref name="current"/> qui devrait être un objet (ou null)</param>
		/// <param name="createIfMissing">Si true, crée l'array si elle n'existait pas. Si false, retourne null</param>
		/// <returns>Valeur du fils, initialisée à une array vide si manquante</returns>
		[Pure, ContractAnnotation("createIfMissing:true => notnull")]
		private static JsonArray? GetOrCreateChildArray(JsonValue current, string? name, bool createIfMissing)
		{
			Contract.Debug.Assert(current != null && current.Type == JsonType.Object);

			JsonValue child;
			if (name != null)
			{
				child = current[name];
				if (child.IsNullOrMissing())
				{
					if (!createIfMissing) return null;
					child = JsonArray.Empty;
					current[name] = child;
				}
				else if (!child.IsArray)
				{
					throw ThrowHelper.InvalidOperationException("The specified key '{0}' exists, but is of type {1} instead of expected Array", name, child.Type);
				}
			}
			else
			{
				if (!current.IsArray) throw ThrowHelper.InvalidOperationException("Selected value was of type {0} instead of expected Array", current.Type);
				child = current;
			}
			return (JsonArray) child;
		}

		/// <summary>Retourne ou crée une entrée d'une array, qui doit être un objet</summary>
		/// <param name="array">Noeud courrant (doit être une array)</param>
		/// <param name="index">Index de l'entrée dans <paramref name="array"/> qui devrait être un objet (ou null)</param>
		/// <param name="createIfMissing">Si true, crée l'objet s'il n'existait pas. Si false, retourne null</param>
		[Pure, ContractAnnotation("createIfMissing:true => notnull")]
		private static JsonObject? GetOrCreateEntryObject(JsonArray array, int index, bool createIfMissing)
		{
			var child = index < array.Count ? array[index] : null;
			if (child.IsNullOrMissing())
			{
				if (!createIfMissing) return null;
				child = JsonObject.Empty;
				array.Set(index, child);
			}
			else if (!child.IsMap)
			{
				throw ThrowHelper.InvalidOperationException("Selected item at position {0} was of type {1} instead of expected Object", index, child.Type);
			}
			return (JsonObject) child;
		}

		/// <summary>Retourne ou crée une entrée d'une array, qui doit être aussi une array</summary>
		/// <param name="array">Noeud courrant (doit être une array)</param>
		/// <param name="index">Index de l'entrée dans <paramref name="array"/> qui devrait être une array (ou null)</param>
		/// <param name="createIfMissing">Si true, crée l'array si elle n'existait pas. Si false, retourne null</param>
		[Pure, ContractAnnotation("createIfMissing:true => notnull")]
		private static JsonArray? GetOrCreateEntryArray(JsonArray array, int index, bool createIfMissing)
		{
			var child = index < array.Count ? array[index] : null;
			if (child.IsNullOrMissing())
			{
				if (!createIfMissing) return null;
				child = JsonArray.Empty;
				array.Set(index, child);
			}
			else if (!child.IsArray)
			{
				throw ThrowHelper.InvalidOperationException("Selected item at position {0} was of type {1} instead of expected Array", index, child.Type);
			}
			return (JsonArray)child;
		}

		/// <summary>Crée ou modifie une valeur à partir de son chemin</summary>
		/// <param name="path">Chemin vers la valeur à créer ou modifier.</param>
		/// <param name="value">Nouvelle valeur</param>
		public void SetPath(string path, JsonValue value)
		{
			Contract.NotNullOrEmpty(path);

			value ??= JsonNull.Null;
			SetPathInternal(path, value, value.Type);
		}

		private JsonValue SetPathInternal(string path, JsonValue? valueToSet, JsonType expectedType)
		{
			JsonValue current = this;
			var tokenizer = new JPathTokenizer(path);
			int? index = null;
			string? name = null;
			while (true)
			{
				var token = tokenizer.ReadNext();
				//Console.WriteLine("{0}@{1} = '{2}'; name={3}, index={4}, current = {5}, total = {6}", token, tokenizer.Offset, tokenizer.GetSourceToken(), name, index, current.ToJsonCompact(), this.ToJsonCompact());
				switch (token)
				{
					case JPathToken.Identifier:
					{ // "Foo"
						name = tokenizer.GetIdentifierName();
						index = null;
						break;
					}
					case JPathToken.ArrayIndex:
					{
						if (index.HasValue)
						{ // combo d'indexer: foo[1][2]..
							var array = GetOrCreateChildArray(current, name, createIfMissing: true)!;
							current = GetOrCreateEntryArray(array, index.Value, createIfMissing: true)!;
							name = null;
						}
						index = tokenizer.GetArrayIndex();
						Contract.Debug.Assert(current != null);
						break;
					}
					case JPathToken.ObjectAccess:
					{
						// "(current.)name>.<" ou

						if (index.HasValue)
						{
							JsonArray array = name == null
								? current.AsArray(required: true)!
								: GetOrCreateChildArray(current, name, createIfMissing: true)!;

							current = GetOrCreateEntryObject(array, index.Value, createIfMissing: true)!;
						}
						else
						{
							current = GetOrCreateChildObject(current, name, createIfMissing: true)!;
						}
						Contract.Debug.Assert(current != null);
						index = null;
						name = null;
						break;
					}
					case JPathToken.End:
					{ // "(current).(name)" ou "(current).(name)[index]"
						if (index.HasValue)
						{
							// current.name doit être une array
							JsonArray array = name == null
								? current.AsArray(required: true)!
								: GetOrCreateChildArray(current, name, createIfMissing: true)!;

							if (valueToSet != null)
							{ // set value
								array.Set(index.Value, valueToSet);
							}
							else if (expectedType == JsonType.Array)
							{ // empty array
								valueToSet = GetOrCreateEntryArray(array, index.Value, createIfMissing: true)!;
							}
							else
							{ // empty object
								Contract.Debug.Assert(expectedType == JsonType.Object);
								valueToSet = GetOrCreateEntryObject(array, index.Value, createIfMissing: true)!;
							}
							Contract.Debug.Assert(valueToSet.Type == expectedType);
							return valueToSet;
						}

						// current doit être un objet
						if (!current.IsMap) throw ThrowHelper.InvalidOperationException("TODO: object expected");

						if (name == null) throw ThrowHelper.FormatException("TODO: missing identifier at end of JPath");

						// update
						if (valueToSet != null)
						{
							current[name] = valueToSet;
						}
						else if (expectedType == JsonType.Array)
						{ // empty array
							valueToSet = GetOrCreateChildArray(current, name, createIfMissing: true)!;
						}
						else
						{ // empty object
							Contract.Debug.Assert(expectedType == JsonType.Object);
							valueToSet = GetOrCreateChildObject(current, name, createIfMissing: true)!;
						}
						Contract.Debug.Assert(valueToSet.Type == expectedType);
						return valueToSet;
					}
					default:
					{
						throw ThrowHelper.FormatException("Invalid JPath token {0} at {1}: '{2}'", token, tokenizer.Offset, path);
					}
				}
				//Console.WriteLine(" => name={3}, index={4}, current = {5}, total = {6}", token, tokenizer.Offset, tokenizer.GetSourceToken(), name, index, current.ToJsonCompact(), this.ToJsonCompact());

			}
		}

		/// <summary>Crée ou modifie une valeur à partir de son chemin</summary>
		/// <param name="path">Chemin vers la valeur à supprimer.</param>
		/// <returns>True si la valeur existait. False si elle n'a pas été trouvée</returns>
		public bool RemovePath(string path)
		{
			Contract.NotNullOrEmpty(path);

			JsonValue? current = this;
			var tokenizer = new JPathTokenizer(path);
			int? index = null;
			string? name = null;
			while (true)
			{
				var token = tokenizer.ReadNext();
				switch (token)
				{
					case JPathToken.Identifier:
					{ // "Foo"
						name = tokenizer.GetIdentifierName();
						index = null;
						break;
					}
					case JPathToken.ArrayIndex:
					{
						if (index.HasValue)
						{ // combo d'indexer: foo[1][2]..
							var array = GetOrCreateChildArray(current, name, createIfMissing: false);
							if (array.IsNullOrMissing()) return false;
							current = GetOrCreateEntryArray(array!, index.Value, createIfMissing: false);
							if (current.IsNullOrMissing()) return false;
							name = null;
						}
						index = tokenizer.GetArrayIndex();
						break;
					}
					case JPathToken.ObjectAccess:
					{
						// "(current.)name>.<" ou

						Contract.Debug.Assert(name != null);

						if (index.HasValue)
						{
							var array = GetOrCreateChildArray(current, name, createIfMissing: false);
							if (array.IsNullOrMissing()) return false;
							current = GetOrCreateEntryObject(array!, index.Value, createIfMissing: false);
							index = null;
						}
						else
						{
							current = GetOrCreateChildObject(current, name, createIfMissing: false);
						}
						if (current.IsNullOrMissing()) return false;
						name = null;
						break;
					}
					case JPathToken.End:
					{ // "(current).(name)" ou "(current).(name)[index]"


						if (index.HasValue)
						{
							// current.name doit être une array
							JsonArray? array;
							if (name == null)
							{
								array = current.AsArray(required: true)!;
							}
							else
							{
								array = GetOrCreateChildArray(current, name, createIfMissing: false);
								if (array.IsNullOrMissing()) return false;
							}
							//TODO: set to null? removeAt?
							array.RemoveAt(index.Value);
							return true;
						}

						// current doit être un objet
						if (!current.IsMap) throw ThrowHelper.InvalidOperationException("TODO: object expected");

						if (name == null) throw ThrowHelper.FormatException("TODO: missing identifier at end of JPath");

						// update
						return ((JsonObject) current).Remove(name);
					}
					default:
					{
						throw ThrowHelper.FormatException("Invalid JPath token {0} at {1}: '{2}'", token, tokenizer.Offset, path);
					}
				}
			}
		}

		#endregion

		#region Setters...

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public JsonObject Set<T>(string key, T value)
		{
			m_items[key] = JsonValue.FromValue<T>(value);
			return this;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public JsonObject Set(string key, JsonValue value)
		{
			m_items[key] = value ?? JsonNull.Null;
			return this;
		}


		/// <summary>Ajoute l'attribut "_class" avec l'id résolvé du type</summary>
		/// <typeparam name="T">Type à résolver</typeparam>
		/// <param name="resolver">Résolveur utilisé (optionnel)</param>
		public JsonObject SetClassId<T>(ICrystalJsonTypeResolver? resolver = null)
		{
			return SetClassId(typeof(T), resolver);
		}

		/// <summary>Ajoute l'attribut "_class" avec l'id résolvé du type</summary>
		/// <param name="type">Type à résolver</param>
		/// <param name="resolver">Résolveur utilisé (optionnel)</param>
		public JsonObject SetClassId(Type type, ICrystalJsonTypeResolver? resolver = null)
		{
			Contract.NotNull(type);

			var typeDef = (resolver ?? CrystalJson.DefaultResolver).ResolveJsonType(type) ?? throw CrystalJson.Errors.Serialization_CouldNotResolveTypeDefinition(type);
			this.ClassId = typeDef.ClassId;
			return this;
		}

		public string? ClassId
		{
			get => this[JsonTokens.CustomClassAttribute].ToStringOrDefault();
			set
			{
				if (string.IsNullOrEmpty(value))
				{
					Remove(JsonTokens.CustomClassAttribute);
				}
				else
				{
					this[JsonTokens.CustomClassAttribute] = value;
				}
			}
		}

		#endregion

		#region Merging...

		/// <summary>Copie les champs d'un autre objet dans l'objet courrant</summary>
		/// <param name="other">Autre objet JSON dont les champs vont être copié dans l'objet courrant. Tout champ déjà existant sera écrasé.</param>
		/// <param name="deepCopy">Si true, clone tous les champs de <paramref name="other"/> avant de le copier. Sinon, ils sont copiés par référence.</param>
		public void MergeWith(JsonObject other, bool deepCopy = false)
		{
			Merge(this, other, deepCopy);
		}

		public static JsonObject Merge(JsonObject parent, JsonObject other, bool deepCopy = false)
		{
			Contract.NotNull(parent);

			if (other != null && other.Count > 0)
			{

				// merge récursivement les propriétés
				// * Copie tout ce qu'il y a dans other, en mergeant les propriétés qui existent déjà
				// * Le merge de properties
				// * > Ecrase si "value type" (string, bool, int, ...)
				// * > Merge si object
				// * > Union si Array (?)

				foreach (var kvp in other)
				{
					if (!parent.TryGetValue(kvp.Key, out var mine))
					{
						// note: on ignore les Missing
						if (!kvp.Value.IsMissing())
						{
							parent[kvp.Key] = deepCopy ? kvp.Value.Copy(deep: true) : kvp.Value;
						}
						continue;
					}

					// Gestion particulière du cas 'missing'
					if (kvp.Value.IsMissing())
					{ // Missing = "remove"
						parent.Remove(kvp.Key);
						continue;
					}

					switch (mine.Type)
					{
						case JsonType.String:
						case JsonType.Number:
						case JsonType.Boolean:
						case JsonType.DateTime:
						{
							parent[kvp.Key] = deepCopy ? kvp.Value.Copy(deep: true) : kvp.Value;
							break;
						}


						case JsonType.Object:
						{
							if (kvp.Value.IsNull)
							{
								parent[kvp.Key] = JsonNull.Null;
								break;
							}

							if (!kvp.Value.IsMap)
							{ // on ne peut merger qu'un objet avec un autre object
								throw ThrowHelper.InvalidOperationException($"Cannot merge a JSON '{kvp.Value.Type}' into an Object for key '{kvp.Key}'");
							}

							((JsonObject)mine).MergeWith((JsonObject)kvp.Value, deepCopy);
							break;
						}

						case JsonType.Null:
						{
							break;
						}

						case JsonType.Array:
						{
							if (kvp.Value.IsNull)
							{
								parent[kvp.Key] = JsonNull.Null;
								break;
							}
							if (!kvp.Value.IsArray)
							{ // on ne peut merger qu'un objet avec une autre array
								throw ThrowHelper.InvalidOperationException($"Cannot merge a JSON '{kvp.Value.Type}' into an Array for key '{kvp.Key}'");
							}
							((JsonArray) mine).MergeWith((JsonArray) kvp.Value, deepCopy);
							break;
						}

						default:
						{
							throw ThrowHelper.InvalidOperationException($"Doesn't know how to merge JSON values of type {mine.Type} with type {kvp.Value.Type} for key '{kvp.Key}'");
						}
					}
				}
			}
			return parent;
		}

		#endregion

		#region Projection...

		/// <summary>Génère un Picker en cache, capable d'extraire une liste de champs d'objet JSON</summary>
		public static Func<JsonObject, JsonObject> Picker(params string[] fields)
		{
			var projections = JsonObject.CheckProjectionFields(fields, false);
			return (obj) => JsonObject.Project(obj, projections);
		}

		/// <summary>Génère un Picker en cache, capable d'extraire une liste de champs d'objet JSON</summary>
		public static Func<JsonObject, JsonObject> Picker(IEnumerable<string> fields, bool keepMissing, bool removeFromSource = false)
		{
			var projections = JsonObject.CheckProjectionFields(fields, keepMissing);
			return (obj) => JsonObject.Project(obj, projections, removeFromSource);
		}

		/// <summary>Génère un Picker en cache, capable d'extraire une liste de champs d'objet JSON</summary>
		public static Func<JsonObject, JsonObject> Picker(IDictionary<string, JsonValue?> defaults, bool removeFromSource = false)
		{
			var projections = JsonObject.CheckProjectionDefaults(defaults);
			return (obj) => JsonObject.Project(obj, projections, removeFromSource);
		}

		public JsonObject Pick(string field)
		{
			//TODO: uniquement la pour éviter ambiguïté avec Pick(object defaults).
			return JsonObject.Project(this, JsonObject.CheckProjectionFields(new[] { field }, false));
		}

		/// <summary>Retourne un nouvel objet ne contenant que certains champs spécifiques de cet objet</summary>
		/// <param name="fields">Liste des noms des champs à conserver</param>
		/// <returns>Nouvel objet qui ne contient que les champs spécifiés dans <paramref name="fields"/></returns>
		public JsonObject Pick(params string[] fields)
		{
			return JsonObject.Project(this, JsonObject.CheckProjectionFields(fields, false));
		}

		/// <summary>Retourne un nouvel objet ne contenant que certains champs spécifiques de cet objet</summary>
		/// <param name="fields">Liste des noms des champs à conserver</param>
		/// <param name="keepMissing">Si false, les champs projetés qui n'existent pas dans l'objet source ne seront pas présent dans le résultat. Si true, les champs seront présents dans le résultat avec une valeur à 'null'</param>
		/// <returns>Nouvel objet qui ne contient que les champs spécifiés dans <paramref name="fields"/></returns>
		public JsonObject Pick(IEnumerable<string> fields, bool keepMissing)
		{
			return JsonObject.Project(this, JsonObject.CheckProjectionFields(fields, keepMissing));
		}

		/// <summary>Retourne un nouvel objet ne contenant que certains champs spécifiques de cet objet</summary>
		/// <param name="defaults">Liste des des champs à conserver, avec une éventuelle valeur par défaut</param>
		/// <returns>Nouvel objet qui ne contient que les champs spécifiés dans <paramref name="defaults"/></returns>
		public JsonObject Pick(IDictionary<string, JsonValue?> defaults)
		{
			//TODO: renommer en PickWithDefaults() ?
			return JsonObject.Project(this, JsonObject.CheckProjectionDefaults(defaults));
		}

		/// <summary>Retourne un nouvel objet ne contenant que certains champs spécifiques de cet objet</summary>
		/// <param name="defaults">Liste des des champs à conserver, avec une éventuelle valeur par défaut</param>
		/// <returns>Nouvel objet qui ne contient que les champs spécifiés dans <paramref name="defaults"/></returns>
		public JsonObject Pick(object defaults)
		{
			//REVIEW: crée un conflit avec Pick(params[] string) si on passe un seul argument!
			//TODO: renommer en PickWithDefaults() ?
			return JsonObject.Project(this, JsonObject.CheckProjectionDefaults(defaults));
		}

		/// <summary>Vérifie que la liste de champs de projection ne contient pas de null, empty ou doublons</summary>
		/// <param name="keys">Liste de nom de champs à projeter</param>
		/// <param name="keepMissing"></param>
		[ContractAnnotation("keys:null => halt")]
		internal static KeyValuePair<string, JsonValue?>[] CheckProjectionFields(IEnumerable<string> keys, bool keepMissing)
		{
			if (keys == null) throw ThrowHelper.ArgumentNullException(nameof(keys));

			var copy = keys as string[] ?? keys.ToArray();

			var res = new KeyValuePair<string, JsonValue?>[copy.Length];
			var set = new HashSet<string>();
			int p = 0;

			foreach (var key in copy)
			{
				if (string.IsNullOrEmpty(key)) throw ThrowHelper.InvalidOperationException("Cannot project empty or null field name: [{0}]", string.Join(", ", copy));
				set.Add(key);
				res[p++] = new KeyValuePair<string, JsonValue?>(key, keepMissing ? JsonNull.Missing : null);
			}
			if (set.Count != copy.Length) throw ThrowHelper.InvalidOperationException("Cannot project duplicate field name: [{0}]", string.Join(", ", copy));

			return res;
		}

		/// <summary>Vérifie que la liste de champs de projection ne contient pas de null, empty ou doublons</summary>
		/// <param name="defaults">Liste des clés à projeter, avec leur valeur par défaut</param>
		/// <remarks>Si un champ est manquant dans l'objet source, la valeur par défaut est utilisée, sauf si elle est égale à null.</remarks>
		[ContractAnnotation("defaults:null => halt")]
		internal static KeyValuePair<string, JsonValue?>[] CheckProjectionDefaults(IDictionary<string, JsonValue?> defaults)
		{
			if (defaults == null) throw ThrowHelper.ArgumentNullException(nameof(defaults));

			var res = new KeyValuePair<string, JsonValue?>[defaults.Count];
			var set = new HashSet<string>();
			int p = 0;

			foreach(var kvp in defaults)
			{
				if (string.IsNullOrEmpty(kvp.Key)) throw ThrowHelper.InvalidOperationException("Cannot project empty or null field name: [{0}]", string.Join(", ", defaults.Select(x => x.Key)));
				set.Add(kvp.Key);
				res[p++] = kvp;
			}
			if (set.Count != defaults.Count) throw ThrowHelper.InvalidOperationException("Cannot project duplicate field name: [{0}]", string.Join(", ", defaults.Select(x => x.Key)));

			return res;
		}

		[ContractAnnotation("defaults:null => halt")]
		internal static KeyValuePair<string, JsonValue?>[] CheckProjectionDefaults(object defaults)
		{
			if (defaults == null) throw ThrowHelper.ArgumentNullException(nameof(defaults));

			var obj = JsonObject.FromObject(defaults);
			Contract.Debug.Assert(obj != null);
			//note: garantit sans doublons et sans clés vides
			return obj.ToArray()!;
		}

		/// <summary>Retourne un nouvel objet ne contenant que certains champs spécifiques de cet objet</summary>
		/// <param name="item">Objet source</param>
		/// <param name="defaults">Liste des propriétés à conserver, avec leur valeur par défaut si elle n'existe pas dans la source</param>
		/// <param name="removeFromSource">Si true, retire les champs sélectionnés de <paramref name="item"/>. Si false, ils sont copiés dans le résultat</param>
		/// <returns>Nouvel objet qui ne contient que les champs de <paramref name="item"/> présents dans <paramref name="defaults"/></returns>
		/// <remarks>{ A: 1, C: false }.Project({ A: 0, B: 42, C: true}) => { A: 1, B: 42, C: false }</remarks>
		internal static JsonObject Project(JsonObject item, KeyValuePair<string, JsonValue?>[] defaults, bool removeFromSource = false)
		{
			Contract.Debug.Requires(item != null && defaults != null);

			var obj = new JsonObject(defaults.Length, item.Comparer);
			foreach (var prop in defaults)
			{
				if (item.TryGetValue(prop.Key, out var value))
				{
					obj[prop.Key] = value;
					if (removeFromSource) item.Remove(prop.Key);
				}
				else if (prop.Value != null)
				{
					obj[prop.Key] = prop.Value;
				}
			}
			return obj;
		}

		#endregion

		#region Filtering...

		/// <summary>Retourne un nouvel objet qui ne contient que les champs d'un objet source qui passent le filtre</summary>
		/// <param name="value">Objet source à filtrer</param>
		/// <param name="filter">Test appliqué sur le nom de chaque champ de <paramref name="value"/>. Les champs qui passent le filtre ne sont pas copiés dans le résultat.</param>
		/// <param name="deepCopy">Si true, fait une copie complète des champs conservés. Si false, copie la référence.</param>
		/// <returns>Nouvel objet filtré</returns>
		/// <remarks>Si aucun champ ne passe le filtre, un nouvel objet vide est retourné.</remarks>
		internal static JsonObject Without(JsonObject value, Func<string, bool> filter, bool deepCopy)
		{
			Contract.Debug.Requires(value != null && filter != null);

			// comme on ne peut pas savoir a l'avance combien de champs vont matcher, on fait quand même une copie de l'objet, qu'on drop si aucun champ n'a été modifié (le GC s'en occupera)
			// on espère que si quelqu'un appelle cette méthode, c'est que la probabilité d'au moins un match est élevée (et donc si ça match, on aurait du allouer l'objet de toute manière)

			var obj = new JsonObject(value.Count, value.Comparer);
			foreach(var item in value)
			{
				if (!filter(item.Key))
				{
					obj[item.Key] = deepCopy ? item.Value.Copy(true) :  item.Value;
				}
			}
			return obj;
		}

		/// <summary>Retourne un nouvel objet qui ne contient que les champs d'un objet source qui passent le filtre</summary>
		/// <param name="value">Objet source à filtrer</param>
		/// <param name="filtered">Test appliqué sur le nom de chaque champ de <paramref name="value"/>. Les champs qui passent le filtre ne sont pas copiés dans le résultat.</param>
		/// <param name="deepCopy">Si true, fait une copie complète des champs conservés. Si false, copie la référence.</param>
		/// <returns>Nouvel objet filtré</returns>
		/// <remarks>Si aucun champ ne passe le filtre, un nouvel objet vide est retourné.</remarks>
		internal static JsonObject Without(JsonObject value, HashSet<string> filtered, bool deepCopy)
		{
			Contract.Debug.Requires(value != null && filtered != null);

			// comme on ne peut pas savoir a l'avance combien de champs vont matcher, on fait quand même une copie de l'objet, qu'on drop si aucun champ n'a été modifié (le GC s'en occupera)
			// on espère que si quelqu'un appelle cette méthode, c'est que la probabilité d'au moins un match est élevée (et donc si ça match, on aurait du allouer l'objet de toute manière)

			var obj = new JsonObject(value.Count, value.Comparer);
			foreach (var item in value)
			{
				if (!filtered.Contains(item.Key))
				{
					obj[item.Key] = deepCopy ? item.Value.Copy(true) : item.Value;
				}
			}
			return obj;
		}

		/// <summary>Retourne une copie d'un objet sans un champ spécifique, s'il existe dans la source</summary>
		/// <param name="value">Objet qui contient éventuellement le champ <paramref name="field"/></param>
		/// <param name="field">Nom du champ à supprimer s'il existe</param>
		/// <param name="deepCopy">Si true, copie également les fils de cet objet</param>
		/// <returns>Nouvel objet sans le champ <paramref name="field"/>.</returns>
		internal static JsonObject Without(JsonObject value, string field, bool deepCopy)
		{
			Contract.Debug.Requires(value != null && field != null);

			//TODO: actuellement, on risque de faire une deepCopy du champ qui sera supprimé ensuite!
			var obj = value.Copy(deepCopy);
			obj.Remove(field);
			return obj;
		}

		/// <summary>Retourne une copie de cet objet, à l'exception du champ spécifié</summary>
		/// <param name="filter">Nom du champ</param>
		/// <param name="deepCopy">Si true, effectue une copie complète de l'objet et de ses fils (récursivement). Sinon, ne copie que l'objet top-level.</param>
		/// <returns>Nouvel objet contenant les mêmes champs que, sauf <paramref name="filter"/>.</returns>
		public JsonObject Without(Func<string, bool> filter, bool deepCopy = false)
		{
			Contract.NotNull(filter);
			return Without(this, filter, deepCopy);
		}

		/// <summary>Retourne une copie de cet objet, à l'exception du champ spécifié</summary>
		/// <param name="fieldToRemove">Nom du champ</param>
		/// <param name="deepCopy">Si true, effectue une copie complète de l'objet et de ses fils (récursivement). Sinon, ne copie que l'objet top-level.</param>
		/// <returns>Nouvel objet contenant les mêmes champs que, sauf <paramref name="fieldToRemove"/>.</returns>
		public JsonObject Without(string fieldToRemove, bool deepCopy = false)
		{
			Contract.NotNullOrEmpty(fieldToRemove);
			return Without(this, fieldToRemove, deepCopy);
		}

		/// <summary>Supprime un champ de l'objet</summary>
		/// <param name="fieldToRemove">Nom du champ</param>
		/// <returns>Le même objet (éventuellement modifié)</returns>
		/// <remarks>Cette méthode est un alias sur <see cref="Remove(string)"/>, utilisable en mode Fluent</remarks>
		public JsonObject Erase(string fieldToRemove)
		{
			Contract.NotNullOrEmpty(fieldToRemove);
			this.Remove(fieldToRemove);
			return this;
		}

		#endregion

		#region Sorting...

		private static bool TrySortValue(JsonValue item, IComparer<string> comparer, [MaybeNullWhen(false)] out JsonValue result)
		{
			result = null!;

			if (item.IsMap)
			{
				var obj = (JsonObject)item;
				if (TrySortByKeys(obj.m_items, comparer, out var subItems))
				{
					result = new JsonObject(subItems, owner: true);
					return true;
				}
				return false;
			}

			if (item.IsArray)
			{
				var arr = (JsonArray)item;

				// on n'alloue le buffer d'items que si au moins un a changé!
				JsonValue[]? items = null;
				for (int i = 0; i < arr.Count;i++)
				{
					if (TrySortValue(arr[i], comparer, out var val))
					{
						if (items == null) items = arr.ToArray();
						items[i] = val;
					}
				}
				if (items != null)
				{ // au moins un item a changé
					result = new JsonArray(items, items.Length);
					return true;
				}
				return false;
			}

			return false;
		}

		/// <summary>Tri les clés d'un dictionnaire, en utilisant un comparer spécifique</summary>
		/// <param name="items">Dictionnaire contenant les items à trier</param>
		/// <param name="comparer">Comparer à utiliser</param>
		/// <param name="result">Dictionnaire dont les clés ont été insérées dans le bon ordre</param>
		private static bool TrySortByKeys(Dictionary<string, JsonValue?> items, IComparer<string> comparer, [MaybeNullWhen(false)] out Dictionary<string, JsonValue?> result)
		{
			//ATTENTION: cet algo se base sur le fait qu'actuellement (.NET 4.0 / 4.5) un Dictionary<K,V> conserve l'ordre d'insertion des clés, tant que personne ne supprime de clés.
			// => si jamais cela n'est plus vrai dans une nouvelle version de .NET, il faudra trouver une nouvelle méthode!

			Contract.Debug.Requires(items != null && comparer != null);
			result = null!;

			if (items.Count == 0)
			{ // pas besoin de trier
				return false;
			}

			//TODO: optimizer le cas Count == 1?

			bool changed = false;

			// capture l'état de l'objet
			var keys = new string[items.Count];
			var values = new JsonValue[items.Count];
			items.Keys.CopyTo(keys, 0);
			items.Values.CopyTo(values, 0);

			// il faut aussi trier les sous-éléments de cet objet
			for (int i = 0; i < values.Length; i++)
			{
				if (TrySortValue(values[i], comparer, out var val))
				{
					values[i] = val;
					changed = true;
				}
			}

			// tri des clés

			var indexes = new int[keys.Length];
			for (int i = 0; i < indexes.Length; i++) indexes[i] = i;
			Array.Sort(keys, indexes, comparer);

			if (!changed)
			{
				// Si toutes les clés étaient déjà dans le bon ordre, indexes var rester trié [0, 1, 2, ..., N-1].
				// Dans ce cas, on peut éviter de modifier cette objet.
				for (int i = 0; i < indexes.Length; i++)
				{
					if (indexes[i] != i)
					{ // il y a eu au moins une modification!
						changed = true;
						break;
					}
				}
			}

			if (changed)
			{ // aucune modification n'a été faite dans la sous-branche correspondant à cet objet
				// génère la nouvelle version de cet objet
				result = new Dictionary<string, JsonValue?>(keys.Length, items.Comparer);
				for (int i = 0; i < keys.Length; i++)
				{
					result[keys[i]] = values[indexes[i]];
				}
				return true;
			}

			return false;
		}

		/// <summary>Tri les clés de ce dictionnaire dans un ordre spécifique</summary>
		/// <remarks>L'instance est modifiée si les clés n'étaient pas dans le bon ordre</remarks>
		public void SortKeys(IComparer<string>? comparer = null)
		{
			if (TrySortByKeys(m_items, comparer ?? StringComparer.Ordinal, out var items))
			{
				m_items.Clear();
				foreach (var kvp in items)
				{
					m_items[kvp.Key] = kvp.Value;
				}
			}
		}

		/// <summary>Retourne un nouveau document JSON, identique au premier, mais avec les clés triées suivant un ordre spécifique</summary>
		/// <param name="map">Object JSON source. Cet objet n'est pas modifié.</param>
		/// <param name="comparer">Comparer à utiliser (Ordinal par défaut)</param>
		/// <returns>Copie (non deep) de <paramref name="map"/> dont les clés sont triées selon <paramref name="comparer"/></returns>
		public static JsonObject OrderedByKeys(JsonObject map, IComparer<string>? comparer = null)
		{
			Contract.NotNull(map);

			if (TrySortByKeys(map.m_items, comparer ?? StringComparer.Ordinal, out var items))
			{
				return new JsonObject(items, owner: true);
			}

			//TODO: to copy or not to copy?
			return map;
		}

		/// <summary>Retourne un nouvel objet, identique à celui-ci, mais avec les clés dans un ordre spécifique</summary>
		public JsonObject OrderedByKeys(IComparer<string>? comparer = null)
		{
			return OrderedByKeys(this, comparer);
		}

		#endregion

		#region Conversion...

		internal override bool IsSmallValue()
		{
			const int LARGE_OBJECT = 5;
			if (m_items.Count >= LARGE_OBJECT) return false;
			foreach(var v in m_items.Values)
			{
				if (v?.IsSmallValue() == false) return false;
			}
			return true;
		}

		internal override bool IsInlinable() => false;

		internal override string GetCompactRepresentation(int depth)
		{
			const int MAX_ITEMS = 4;

			if (m_items.Count == 0) return "{ }"; // empty

			// On va dumper jusqu'à 4 champs (ce qui couvre la majorité des "petits" objets
			// Si la valeur d'un field est "small" elle est dumpée intégralement, sinon elle est remplacer par des '...'

			var sb = new StringBuilder("{ ");
			int i = 0;
			foreach(var kv in m_items)
			{
				if (i >= MAX_ITEMS) { sb.Append($", /* \u2026 {(m_items.Count - MAX_ITEMS):N0} more */"); break; }
				if (i > 0) sb.Append(", ");

				sb.Append(kv.Key).Append(": ");
				if (depth == 0 || (kv.Value?.IsSmallValue() ?? true))
				{ 
					sb.Append(kv.Value?.GetCompactRepresentation(depth + 1));
				}
				else
				{
					switch (kv.Value.Type)
					{
						case JsonType.Object: sb.Append("{\u2026}"); break;
						case JsonType.Array: sb.Append("[\u2026]"); break;
						case JsonType.String: sb.Append("\"\u2026\""); break;
						default: sb.Append('\u2026'); break;
					}
				}
				i++;
			}
			sb.Append(" }");
			return sb.ToString();
		}

		public override object? ToObject()
		{
			return CrystalJsonParser.DeserializeCustomClassOrStruct(this, typeof(object), CrystalJson.DefaultResolver);
		}

		public override object? Bind(Type? type, ICrystalJsonTypeResolver? resolver = null)
		{
			return (resolver ?? CrystalJson.DefaultResolver).BindJsonObject(type, this);
		}

		public T Deserialize<T>(ICrystalJsonTypeResolver? customResolver = null)
		{
			return (T) Bind(typeof(T), customResolver)!;
		}

		/// <summary>Retourne une JsonArray contenant les valeurs de cet objet</summary>
		/// <returns>JsonArray contenant toutes les valeurs (sans les clés) de cet objet</returns>
		public JsonArray ToJsonArray()
		{
			return this.Count == 0 ? JsonArray.Empty : new JsonArray(this.Values);
		}

		#endregion

		#region IJsonSerializable

		public override void JsonSerialize(CrystalJsonWriter writer)
		{
			var state = writer.BeginObject();
			foreach (var item in this)
			{
				writer.WriteField(item.Key, item.Value);
			}
			writer.EndObject(state);
		}

		#endregion

		#region IEquatable<...>

		public override bool Equals(JsonValue? obj)
		{
			if (object.ReferenceEquals(obj, null)) return false;
			if (obj.Type == JsonType.Object) return Equals(obj as JsonObject);
			return false;
		}

		public bool Equals(JsonObject? obj)
		{
			if (object.ReferenceEquals(obj, null) || obj.Count != this.Count)
			{
				return false;
			}
			var cmp = JsonValueComparer.Default;
			foreach (var kvp in this)
			{
				if (!obj.TryGetValue(kvp.Key, out var o) || !cmp.Equals(o, kvp.Value))
					return false;
			}
			return true;
		}

		public override int GetHashCode()
		{
			// le hashcode de l'objet ne doit pas changer meme s'il est modifié (sinon on casse les hashtables!)
			return System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(this);

			//TODO: si on jour on gère les Read-Only dictionaries, on peut utiliser ce code
			//// on n'est pas obligé de calculer le hash code de tous les éléments de l'objet!
			//var items = m_items;
			//int h = 17;
			//int n = 4;
			//foreach(var kvp in items)
			//{
			//	h = (h * 31) + kvp.Key.GetHashCode();
			//	h = (h * 31) + kvp.Value.GetHashCode();
			//	if (n-- == 0) break;
			//}
			//h ^= items.Count;
			//return h;
		}

		public override int CompareTo(JsonValue? other)
		{
			throw new NotSupportedException("JSON Object cannot be compared with other elements");
		}

		#endregion

		public ExpandoObject ToExpando()
		{
			var expando = new ExpandoObject();
			var map = (IDictionary<string, object?>) expando;
			foreach (var kvp in m_items)
			{
				map.Add(kvp.Key, kvp.Value?.ToObject());
			}
			return expando;
		}

		public KeyValuePair<string, JsonValue>[] ToArray()
		{
			var res = new KeyValuePair<string, JsonValue>[m_items.Count];
			CopyTo(res, 0);
			return res;
		}

		public void CopyTo(KeyValuePair<string, JsonValue>[] array)
		{
			((ICollection<KeyValuePair<string, JsonValue>>)m_items).CopyTo(array, 0);
		}

		public void CopyTo(KeyValuePair<string, JsonValue>[] array, int arrayIndex)
		{
			((ICollection<KeyValuePair<string, JsonValue>>)m_items).CopyTo(array, arrayIndex);
		}

		public override void WriteTo(ref SliceWriter writer)
		{
			writer.WriteByte('{');
			bool first = true;
			foreach (var kv in this)
			{
				// par défaut, on ne sérialise pas les "Missing"
				if (kv.Value.IsMissing()) break;

				if (first) first = false; else writer.WriteByte(',');

				if (JsonEncoding.NeedsEscaping(kv.Key))
				{
					writer.WriteStringUtf8(JsonEncoding.EncodeSlow(kv.Key));
				}
				else
				{
					writer.WriteByte('"');
					writer.WriteStringUtf8(kv.Key);
					writer.WriteByte('"');
				}
				writer.WriteByte(':');
				kv.Value.WriteTo(ref writer);
			}
			writer.WriteByte('}');
		}

	}

}
