#region Copyright (c) 2023-2025 SnowBank SAS, (c) 2005-2023 Doxense SAS
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
	using System.Collections;
	using System.Linq.Expressions;
	using System.Reflection;
	using Doxense.Collections.Caching;
	using Doxense.Collections.Tuples;
	using Doxense.Runtime;
	using Doxense.Serialization;

	public sealed class CrystalJsonDomWriter
	{
		//TODO: renommer en Reader? Ou Converter?
		// => techniquement on *écrit* dans un DOM

		//SAME AS FROM JSONWRITER
		private readonly CrystalJsonSettings m_settings;
		private readonly ICrystalJsonTypeResolver m_resolver;
		//REVIEW: => convertir tout ces bools en bitflags!
		private readonly bool m_readOnly;
		private readonly bool m_discardDefaults; //REVIEW: implement!
		private readonly bool m_discardNulls;
		private readonly bool m_discardClass;
		private readonly bool m_markVisited;
		private readonly bool m_camelCase;
		private readonly bool m_enumAsString; //REVIEW: implement!
		private readonly bool m_enumCamelCased; //REVIEW: implement!
		//END
		private readonly IEqualityComparer<string> m_keyComparer;

		internal struct VisitingContext
		{
			public int ObjectGraphDepth;
			public object[]? VisitedObjectsBuffer;
			public int VisitedObjectsCursor;

			public bool Enter(object value)
			{
				var buf = this.VisitedObjectsBuffer;
				int pos = this.VisitedObjectsCursor;
				if (pos >= MaximumObjectGraphDepth) return false;

				// check object is not already contained
				if (pos != 0)
				{
					foreach(var obj in buf.AsSpan(0, pos))
					{
						if (ReferenceEquals(obj, value)) return false;
					}
				}

				// resize if needed
				if ((buf?.Length ?? 0) <= pos)
				{
					Array.Resize(ref buf, Math.Max(pos * 2, 4));
					this.VisitedObjectsBuffer = buf;
				}

				buf![pos] = value;
				this.VisitedObjectsCursor = pos + 1;
				return true;
			}

			public bool Leave(object value)
			{
				int pos = this.VisitedObjectsCursor - 1;
				var buf = this.VisitedObjectsBuffer;
				if (pos < 0) throw new InvalidOperationException("Visited object stack is empty!");

				// check last item matches the expected value
				if (!ReferenceEquals(buf![pos], value)) return false;

				buf[pos] = default!;
				this.VisitedObjectsCursor = pos;
				return true;
			}

		}

		#region Constructors...

		public CrystalJsonDomWriter()
			: this(null, null)
		{ }

		public CrystalJsonDomWriter(CrystalJsonSettings? settings, ICrystalJsonTypeResolver? resolver)
		{
			m_settings = settings ?? CrystalJsonSettings.Json;
			m_resolver = resolver ?? CrystalJson.DefaultResolver;

			m_readOnly = m_settings.ReadOnly;
			m_discardDefaults = m_settings.HideDefaultValues;
			m_discardNulls = m_discardDefaults || !m_settings.ShowNullMembers;
			m_discardClass = m_settings.HideClassId;
			m_camelCase = m_settings.UseCamelCasingForNames;
			m_enumAsString = m_settings.EnumsAsString;
			m_enumCamelCased = m_settings.UseCamelCasingForEnums;
			m_markVisited = !m_settings.DoNotTrackVisitedObjects;

			m_keyComparer = m_settings.IgnoreCaseForNames ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
		}

		/// <summary>JSON DOM writer, with default settings</summary>
		public static readonly CrystalJsonDomWriter Default = new(CrystalJsonSettings.Json, CrystalJson.DefaultResolver);

		/// <summary>JSON DOM writer, with default settings, that produces read-only values</summary>
		public static readonly CrystalJsonDomWriter DefaultReadOnly = new(CrystalJsonSettings.JsonReadOnly, CrystalJson.DefaultResolver);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static CrystalJsonDomWriter Create(CrystalJsonSettings? settings, ICrystalJsonTypeResolver? resolver)
		{
			return AreDefaultSettings(settings, resolver) ? Default : new CrystalJsonDomWriter(settings, resolver);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static CrystalJsonDomWriter CreateReadOnly(CrystalJsonSettings? settings, ICrystalJsonTypeResolver? resolver)
		{
			return AreDefaultSettings(settings, resolver) ? DefaultReadOnly : new CrystalJsonDomWriter(settings?.AsReadOnly() ?? CrystalJsonSettings.JsonReadOnly, resolver);
		}

		public static bool AreDefaultSettings(CrystalJsonSettings? settings, ICrystalJsonTypeResolver? resolver)
		{
			// Pas s'il y a un résolver custom...
			if (resolver != null && resolver != CrystalJson.DefaultResolver) return false;

			// Les options de layout (compact, indenté, ...) n'ont aucun impact en mode DOM!
			return (settings?.Flags ?? 0 & ~CrystalJsonSettings.OptionFlags.Layout_Mask) == 0;
		}

		#endregion

		#region Public Properties...

		public CrystalJsonSettings Settings => m_settings;

		public ICrystalJsonTypeResolver Resolver => m_resolver;

		#endregion

		#region Public Methods...

		/// <summary>Transforme un objet CLR quelconque en une JsonValue correspondante</summary>
		/// <param name="value">Valeur à convertir (primitive, value type ou reference type) </param>
		/// <param name="declaredType">Type déclaré de la valeur au niveau de son parent (ex: Property/Field dans la classe parente, type d'élement dans une collection, ...), qui peut être différent de <paramref name="runtimeType"/> (interface, classe abstraite, ...). Passer typeof(object) pour un objet dont le contexte n'est pas connu, ou passer la même valeur que <paramref name="runtimeType"/> si le contexte est connu exactement.</param>
		/// <param name="runtimeType">Si non null, type actuel de l'instance au runtime. Si null, utilise <paramref name="value"/>.GetType().</param>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public JsonValue ParseObject(object? value, Type declaredType, Type? runtimeType = null)
		{
			var context = default(VisitingContext);
			return ParseObjectInternal(ref context, value, declaredType, runtimeType);
		}

		internal JsonValue ParseObjectInternal(ref VisitingContext context, object? value, Type declaredType, Type? runtimeType)
		{
			Contract.Debug.Requires(declaredType != null);

			if (value == null)
			{
				return JsonNull.Null;
			}

			if (value is string s)
			{
				return JsonString.Return(s);
			}

			// already a JSON DOM element ?
			JsonValue? result;
			if ((result = value as JsonValue) != null)
			{
				return result;
			}

			runtimeType ??= value.GetType();

			if (runtimeType.IsPrimitive)
			{ // int, bool, char, float, ...
				if (TryConvertPrimitiveObject(value, runtimeType, out result))
				{
					return result;
				}
			}
			else if (runtimeType.IsValueType)
			{ // struct, datetime, guids, ...
				if (TryConvertValueTypeObject(ref context, value, runtimeType, out result))
				{
					return result;
				}
			}
			else if (runtimeType.IsArray)
			{ // T[]
				if (TryConvertListObject(ref context, (IList) value, runtimeType, out result))
				{
					return result;
				}
			}

			if (value is IJsonPackable packable)
			{ // the object knows how to do it by itself...
				return packable.JsonPack(m_settings, m_resolver);
			}

			if (value is IDictionary<string, object?> dict)
			{ // capture Dictionary<string, object> mais aussi les ExpandoObject
				if (TryConvertDictionaryObject(ref context, dict, runtimeType, out result))
				{
					return result;
				}
			}

			if (value is IEnumerable enmr)
			{
				if (value is IDictionary idict)
				{ // => { K: V }
					if (TryConvertDictionaryObject(ref context, idict, runtimeType, out result))
					{
						return result;
					}
				}

				if (value is IList ilist)
				{ // => [ x, y, ... ]
					if (TryConvertListObject(ref context, ilist, runtimeType, out result))
					{
						return result;
					}
				}

				if (value is IVarTuple tuple)
				{
					return CrystalJsonVisitor.ConvertTupleToJson(tuple);
				}

				if (TryConvertEnumerableObject(ref context, enmr, runtimeType, out result))
				{ // => [ x, y, ...]
					return result;
				}
			}

			if (runtimeType.IsClass)
			{
				switch (value)
				{
					case System.Text.StringBuilder sb:
					{
						return JsonString.Return(sb);
					}
					case System.Net.IPAddress ip:
					{
						return JsonString.Return(ip);
					}
					case DBNull:
					{
						return JsonNull.Null;
					}
					case NodaTime.DateTimeZone dtz:
					{
						return JsonString.Return(dtz);
					}
					case System.Version v:
					{
						return JsonString.Return(v);
					}
					case System.Uri uri:
					{
						return JsonString.Return(uri);
					}
					case System.Type tp:
					{
						return JsonString.Return(tp);
					}
				}
			}
			else
			{
				if (value is System.Runtime.CompilerServices.ITuple tuple)
				{
					return CrystalJsonVisitor.ConvertTupleToJson(tuple);
				}
			}


			if (TryConvertFromTypeDefinition(ref context, value, declaredType, runtimeType, out result))
			{
				return result;
			}

			if (TryPackViaDuckTyping(value, declaredType, runtimeType, out result))
			{
				return result;
			}

#if DEBUG
			// si vous arrivez ici, c'est que votre type n'implémente pas IJsonBindable!
			if (System.Diagnostics.Debugger.IsAttached) System.Diagnostics.Debugger.Break();
#endif
			//TODO: on pourrait détecter la présence de IJsonSerializable, et si c'est le cas, l'appeler et reparser le texte JSON?
			throw CrystalJson.Errors.Serialization_DoesNotKnowHowToSerializeType(runtimeType ?? declaredType);
		}

		#endregion

		#region DOM Helpers...

		public JsonObject BeginObject(int capacity = 0)
		{
			return new JsonObject(capacity, m_keyComparer);
		}

		public JsonArray BeginArray(int capacity = 0)
		{
			return new JsonArray(capacity);
		}

		#endregion

		#region Type Visitors...

		[ContractAnnotation("=>true,result:notnull; =>false,result:null")]
		internal bool TryConvertPrimitiveObject(object value, Type type, [MaybeNullWhen(false)] out JsonValue result)
		{
			Contract.Debug.Requires(value != null && type != null);

			//REVIEW:TODO: m_discardDefaults si on est dans un JsonObject !

			// Cannot be an Enum because we checked for primitive types only
			switch (Type.GetTypeCode(type))
			{
				case TypeCode.Boolean:
				{
					result = JsonBoolean.Return((bool)value);
					return true;
				}
				case TypeCode.Char:
				{
					result = JsonString.Return(new string((char)value, 1));
					return true;
				}
				case TypeCode.SByte:
				{
					result = JsonNumber.Return((sbyte)value);
					return true;
				}
				case TypeCode.Byte:
				{
					result = JsonNumber.Return((byte)value);
					return true;
				}
				case TypeCode.Int16:
				{
					result = JsonNumber.Return((short)value);
					return true;
				}
				case TypeCode.UInt16:
				{
					result = JsonNumber.Return((ushort)value);
					return true;
				}
				case TypeCode.Int32:
				{
					result = JsonNumber.Return((int)value);
					return true;
				}
				case TypeCode.UInt32:
				{
					result = JsonNumber.Return((uint)value);
					return true;
				}
				case TypeCode.Int64:
				{
					result = JsonNumber.Return((long)value);
					return true;
				}
				case TypeCode.UInt64:
				{
					result = JsonNumber.Return((ulong)value);
					return true;
				}
				case TypeCode.Single:
				{
					result = JsonNumber.Return((float)value);
					return true;
				}
				case TypeCode.Double:
				{
					result = JsonNumber.Return((double)value);
					return true;
				}
				case TypeCode.Object:
				{
					// A ma connaissance, le seul type IsPrimitive/TypeCode.Object est IntPtr
					if (typeof(IntPtr) == type)
					{
						//Note: c'est peut être dangereux de sérialiser des IntPtr qui sont des pointeurs, mais ce n'est pas forcément le cas pour toutes les classes ...
						result = JsonNumber.Return(((IntPtr)value).ToInt64());
						return true;
					}
					break;
				}
				// note: Decimal n'est pas Primitive!
			}
			result = null!;
			return false;
		}

		[ContractAnnotation("=>true,result:notnull; =>false,result:null")]
		internal bool TryConvertValueTypeObject(ref VisitingContext context, object value, Type type, [MaybeNullWhen(false)] out JsonValue result)
		{
			Contract.Debug.Requires(value != null && type != null);

			//TODO: réécrire via une Dictionary<Type, Func<..>> pour éviter le train de if/elseif !

			if (typeof(DateTime) == type)
			{
				result = JsonDateTime.Return((DateTime)value);
				return true;
			}
			if (typeof(DateTimeOffset) == type)
			{
				result = JsonDateTime.Return((DateTimeOffset)value);
				return true;
			}
			if (typeof(TimeSpan) == type)
			{
				result = JsonNumber.Return(((TimeSpan)value).TotalSeconds);
				return true;
			}
			if (typeof(Guid) == type)
			{
				result = JsonString.Return((Guid) value);
				return true;
			}
			if (typeof(Decimal) == type)
			{
				result = JsonNumber.Return((decimal) value);
				return true;
			}
			if (typeof(Uuid128) == type)
			{
				result = JsonString.Return((Uuid128) value);
				return true;
			}
			if (typeof(Uuid64) == type)
			{
				result = JsonString.Return((Uuid64) value);
				return true;
			}
			if (typeof(Uuid96) == type)
			{
				result = JsonString.Return((Uuid96) value);
				return true;
			}
			if (typeof(Uuid80) == type)
			{
				result = JsonString.Return((Uuid80) value);
				return true;
			}
			if (typeof(DateOnly) == type)
			{
				result = JsonDateTime.Return((DateOnly) value);
				return true;
			}
			if (typeof(TimeOnly) == type)
			{
				result = JsonNumber.Return((TimeOnly) value);
				return true;
			}

			if (type.IsEnum)
			{ // on convertit les énumérations en keyword
#pragma warning disable CS0618 // Type or member is obsolete
				result = EnumStringTable.GetName(type, (Enum) value);
#pragma warning restore CS0618 // Type or member is obsolete
				//BUGBUG: vérifier m_enumAsString et m_enumCamelCased
				return true;
			}

			#region NodaTime types...

			if (typeof(NodaTime.Instant) == type)
			{
				result = JsonString.Return((NodaTime.Instant)value);
				return true;
			}
			if (typeof(NodaTime.Duration) == type)
			{
				result = JsonNumber.Return((NodaTime.Duration)value);
				return true;
			}
			if (typeof(NodaTime.LocalDateTime) == type)
			{
				result = JsonString.Return((NodaTime.LocalDateTime)value);
				return true;
			}
			if (typeof(NodaTime.ZonedDateTime) == type)
			{
				result = JsonString.Return((NodaTime.ZonedDateTime)value);
				return true;
			}
			if (typeof(NodaTime.OffsetDateTime) == type)
			{
				result = JsonString.Return((NodaTime.OffsetDateTime)value);
				return true;
			}
			if (typeof(NodaTime.Offset) == type)
			{
				result = JsonString.Return((NodaTime.Offset)value);
				return true;
			}
			if (typeof(NodaTime.LocalDate) == type)
			{
				result = JsonString.Return((NodaTime.LocalDate)value);
				return true;
			}
			if (typeof(NodaTime.LocalTime) == type)
			{
				result = JsonString.Return((NodaTime.LocalTime)value);
				return true;
			}
			//note: DateTimeZone est une class!

			#endregion

			// Nullable: l'objet boxé en mémoire sera directement le value type,
			// on a juste besoin de rerouter vers la bonne méthode
			var nullableType = Nullable.GetUnderlyingType(type);
			if (nullableType != null)
			{
				if (nullableType.IsPrimitive)
				{
					return TryConvertPrimitiveObject(value, nullableType, out result);
				}
				// forcément un ValueType
				// => récursif, mais safe car le underlying type n'est pas nullable (du moins en théorie ....)
				return TryConvertValueTypeObject(ref context, value, nullableType, out result);
			}

			//TODO en attendant d'avoir la version générique , on gère au moins ce cas la
			if (type == typeof(KeyValuePair<string, string>))
			{
				result = VisitKeyValuePair((KeyValuePair<string, string>) value);
				return true;
			}
			if (type == typeof(KeyValuePair<long, long>))
			{
				result = VisitKeyValuePair((KeyValuePair<long, long>) value);
				return true;
			}
			if (type.Name == "KeyValuePair`2")
			{
				result = VisitKeyValuePair(ref context, value, type);
				return true;
			}

			if (typeof(Slice) == type)
			{
				result = JsonString.Return((Slice) value);
				return true;
			}
			if (typeof(ArraySegment<byte>) == type)
			{
				result = JsonString.Return((ArraySegment<byte>)value);
				return true;
			}
			//TODO: Memory<T>/ReadOnlyMemory<T>?

#if DISABLED // ca rame trop
			if (type.IsGenericInstanceOf(typeof(KeyValuePair<,>)))
			{ // KeyValuePair<K, V> => [ K, V ]
				//HACKHACK: supeeeeeer leeeeeent !
				//TODO: cache!
				var genTypes = type.GetGenericArguments();
				var method = typeof(CrystalJsonDomWriter).GetMethod("VisitKeyValuePair", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
				method = method.MakeGenericMethod(genTypes[0], genTypes[1]);
				result = (JsonValue) method.Invoke(null, new object[] { value });
				return result != null;
			}
#endif

			// les custom struct sont gérée ailleurs (via leur type definition)
			result = null!;
			return false;
		}

		internal JsonValue VisitKeyValuePair(ref VisitingContext context, object value, Type type)
		{
			return s_cachedKeyValuePairVisitors.GetOrAdd(type, CompileKeyValuePairVisitor)(this, ref context, value);
		}

		private delegate JsonValue KeyValuePairVisitor(CrystalJsonDomWriter writer, ref VisitingContext context, object value);

		private static readonly QuasiImmutableCache<Type, KeyValuePairVisitor> s_cachedKeyValuePairVisitors = new(TypeEqualityComparer.Default);

		private static KeyValuePairVisitor CompileKeyValuePairVisitor(Type kvType)
		{
			// (writer, ref context, obj) =>  VisitKeyValuePair<TKey, TValue>(writer, (KeyValuePair<TKey, TValue) obj)

			var prmWriter = Expression.Parameter(typeof(CrystalJsonDomWriter), "writer");
			var prmContext = Expression.Parameter(typeof(VisitingContext).MakeByRefType(), "context");
			var prmObj = Expression.Parameter(typeof(object), "obj");
			var m = typeof(CrystalJsonDomWriter).GetMethod(nameof(VisitKeyValuePairGeneric), BindingFlags.Static | BindingFlags.NonPublic)!.MakeGenericMethod(kvType.GetGenericArguments());
			var body = Expression.Call(m, prmWriter, prmContext, Expression.Convert(prmObj, kvType));

			return Expression.Lambda<KeyValuePairVisitor>(body, true, [ prmWriter, prmContext, prmObj ]).Compile();
		}

		private static JsonValue VisitKeyValuePairGeneric<TKey, TValue>(CrystalJsonDomWriter writer, ref VisitingContext context, KeyValuePair<TKey, TValue> value)
		{
			return new JsonArray([
				JsonValue.FromValue<TKey>(value.Key), //REVIEW: on suppose que le type de clé est simple!
				JsonValue.FromValue<TValue>(writer, ref context, value.Value)
			], 2, readOnly: false);
		}

		private static JsonValue VisitKeyValuePair(KeyValuePair<string, string> kv)
		{
			return new JsonArray([
				JsonString.Return(kv.Key),
				JsonString.Return(kv.Value)
			], 2, readOnly: false);
		}

		private static JsonValue VisitKeyValuePair(KeyValuePair<long, long> kv)
		{
			return new JsonArray([
				JsonNumber.Return(kv.Key),
				JsonNumber.Return(kv.Value)
			], 2, readOnly: false);
		}

		[ContractAnnotation("=>true,result:notnull; =>false,result:null")]
		internal bool TryConvertDictionaryObject(ref VisitingContext context, IDictionary<string, object?> value, Type type, out JsonValue result)
		{
			Contract.Debug.Requires(value != null && type != null);

			if (value.Count == 0)
			{
				result = m_readOnly ? JsonObject.EmptyReadOnly : new JsonObject();
				return true;
			}

			MarkVisited(ref context, value, type);
			var obj = BeginObject(value.Count);
			foreach (var kvp in value)
			{
				JsonValue val;
				if (kvp.Value == null)
				{
					if (m_discardNulls) continue;
					val = JsonNull.Null;
				}
				else
				{
					//TODO: test discard defaults !
					val = ParseObjectInternal(ref context, kvp.Value, typeof(object), null);
				}

				obj.Add(
					FormatName(kvp.Key),
					val
				);
			}
			Leave(ref context, value);

			result = obj;
			return true;
		}

		[ContractAnnotation("=>true,result:notnull; =>false,result:null")]
		internal bool TryConvertDictionaryObject(ref VisitingContext context, IDictionary value, Type type, out JsonValue result)
		{
			Contract.Debug.Requires(value != null && type != null);

			if (value.Count == 0)
			{
				result = m_readOnly ? JsonObject.EmptyReadOnly : new JsonObject();
				return true;
			}

			bool stringKeys;
			Type elementType;

			var genType = type.FindGenericType(typeof(IDictionary<,>));
			if (genType != null)
			{
				var argTypes = genType.GetGenericArguments();
				stringKeys = argTypes[0] == typeof(string);
				elementType = argTypes[1];
			}
			else
			{ // on ne connait ni le type des clés, ni des valeurs
				stringKeys = false;
				elementType = typeof(object);
			}

			MarkVisited(ref context, value, type);
			var obj = BeginObject(value.Count);
			foreach (DictionaryEntry kvp in value)
			{
				JsonValue val;
				if (kvp.Value == null)
				{
					if (m_discardNulls) continue;
					val = JsonNull.Null;
				}
				else
				{
					//TODO: test discard defaults !
					val = ParseObjectInternal(ref context, kvp.Value, elementType, null);
				}

				obj.Add(
					FormatName(stringKeys ? (string)kvp.Key : TypeHelper.ConvertKeyToString(kvp.Key)),
					val
				);
			}
			Leave(ref context, value);

			result = obj;
			return true;
		}

		[ContractAnnotation("=>true,result:notnull; =>false,result:null")]
		internal bool TryConvertListObject(ref VisitingContext context, IList values, Type listType, out JsonValue result)
		{
			Contract.Debug.Requires(values != null && listType != null);

			Type elemType = typeof(object);
			var genType = listType.FindGenericType(typeof(IList<>));
			if (genType != null)
			{
				elemType = genType.GetGenericArguments()[0];
			}

			if (values.Count == 0)
			{
				result = m_readOnly ? JsonArray.EmptyReadOnly : new JsonArray();
				return true;
			}

			MarkVisited(ref context, values);
			var array = BeginArray(values.Count);
			foreach (var item in values)
			{
				array.Add(item != null ? ParseObjectInternal(ref context, item, elemType, null) : JsonNull.Null);
			}
			Leave(ref context, values);
			if (m_readOnly)
			{
				array.Freeze();
			}
			result = array;
			return true;
		}

		[ContractAnnotation("=>true,result:notnull; =>false,result:null")]
		internal bool TryConvertEnumerableObject(ref VisitingContext context, IEnumerable values, Type sequenceType, out JsonValue result)
		{
			Contract.Debug.Requires(values != null && sequenceType != null);

			// try to pre-allocate the array if we know the number of elements in advance
			int? l = values is ICollection coll ? coll.Count : null;
			if (l == 0)
			{ // this is empty!
				result = m_readOnly ? JsonArray.EmptyReadOnly : new JsonArray();
				return true;
			}

			MarkVisited(ref context, values);

			var array = BeginArray(l ?? 0);


			foreach (var item in values)
			{
				array.Add(item != null ? ParseObjectInternal(ref context, item, typeof(object), null) : JsonNull.Null);
			}

			Leave(ref context, values);

			if (m_readOnly)
			{
				array.Freeze();
			}

			result = array;
			return true;
		}

		[ContractAnnotation("=>true,result:notnull; =>false,result:null")]
		internal bool TryConvertFromTypeDefinition(ref VisitingContext context, object? value, Type declaredType, Type? runtimeType, [MaybeNullWhen(false)] out JsonValue result)
		{
			Contract.Debug.Requires(value != null && declaredType != null && runtimeType != null);

			var typeDef = m_resolver.ResolveJsonType(runtimeType);
			if (typeDef == null || (typeDef.CustomBinder != null && !typeDef.IsAnonymousType))
			{
				result = null!;
				return false;
			}

			MarkVisited(ref context, value, runtimeType);
			//note: il y aura en général moins de fields (élimination des null/default), mais de toutes manières le Dictionary<> arrondi au nombre premier supérieur, donc on ne perds pas grand chose.
			var obj = BeginObject(typeDef.Members.Length);

			//COPY/PASTA (a peu près identique à CrystalJsonVisitor.VisitCustomClassOrStruct)
			if (!m_discardClass)
			{
				// il faut ajouter l'attribut class si 
				// => le type au runtime n'est pas le même que le type déclaré. Ex: class A { IFoo Foo; } / class B : IFoo { } / new A() { Foo = new B() }

				//note: dans le cas d'un DOM, on a toujours le runtime type, qui ne peut pas être abstract, MAIS qui n'est pas obligatoirement sealed
				if (typeDef.RequiresClassAttribute || (declaredType != runtimeType && !typeDef.IsAnonymousType && !declaredType.IsNullableType() && typeDef.BaseType == null))
				{ // il faut préciser la class !
					//TODO: auto-aliasing
					obj[JsonTokens.CustomClassAttribute] = JsonString.Return(typeDef.ClassId);
				}
			}
			//END

			foreach (var member in typeDef.Members)
			{
				var v = member.Getter(value);
				if (v == null)
				{
					if (!m_discardNulls)
					{
						obj[FormatName(member.Name)] = JsonNull.Null;
					}
				}
				else
				{
					obj[FormatName(member.Name)] = ParseObjectInternal(ref context, v, member.Type, null);
				}
			}
			Leave(ref context, value);

			if (m_readOnly)
			{ //REVIEW: PERF: TODO: find a more efficient way?
				obj.Freeze();
			}

			result = obj;
			return true;
		}

		#endregion

		#region Copy/Pasta from CrystalJsonWriter

		// code copié tel quel, qui pourrait aller sur une classe abstraite commune aux deux classes...

		private const int MaximumObjectGraphDepth = 16;

		/// <summary>Marque l'objet comme étant déjà traité</summary>
		/// <exception cref="System.InvalidOperationException">Si cet objet a déjà été marqué</exception>
		internal void MarkVisited(ref VisitingContext context, object? value, Type? type = null)
		{
			if (context.ObjectGraphDepth >= MaximumObjectGraphDepth)
			{ // protection contre les object graph gigantesques
				throw CrystalJson.Errors.Serialization_FailTooDeep(context.ObjectGraphDepth, value);
			}
			if (m_markVisited && value != null)
			{ // protection contre les chaînes récursives d'objet (=> stack overflow)
				if (!context.Enter(value))
				{
					if (!CrystalJsonWriter.TypeSafeForRecursion(type ?? value.GetType()))
					{
						throw CrystalJson.Errors.Serialization_ObjectRecursionIsNotAllowed(context.VisitedObjectsBuffer.AsSpan(0, context.VisitedObjectsCursor).ToArray(), value, context.ObjectGraphDepth);
					}
				}
			}
			++context.ObjectGraphDepth;
		}

		internal void Leave(ref VisitingContext context, object? value)
		{
			if (context.ObjectGraphDepth == 0) throw CrystalJson.Errors.Serialization_InternalDepthInconsistent();
			if (m_markVisited && value != null && !context.Leave(value))
			{
				throw CrystalJson.Errors.Serialization_LeaveNotSameThanMark(context.ObjectGraphDepth, value);
			}
			--context.ObjectGraphDepth;
		}

		/// <summary>Retourne le nom formaté d'un champ</summary>
		/// <param name="name">Nom d'un champ (ex: "FooBar")</param>
		/// <returns>Nom éventuellement formaté ("fooBar" en Camel Casing)</returns>
		internal string FormatName(string name)
		{
			return m_camelCase ? CrystalJsonWriter.CamelCase(name) : name;
		}

		#endregion

		/// <summary>Utilise une méthode d'instance compatible avec la signature de JsonPack</summary>
		public bool TryPackViaDuckTyping(object? value, Type declaredType, Type? runtimeType, [MaybeNullWhen(false)] out JsonValue result)
		{
			var t = DuckTypedJsonPackMethods.GetOrAdd(runtimeType ?? declaredType);
			if (!t.HasValue)
			{
				result = null!;
				return false;
			}
			result = t.Value(value, this.Settings, this.Resolver);

			//if (runtimeType != declaredType && declaredType != typeof(object))
			//{ // il faut peut être rajouter l'attribut '_class' ?
			//	if (result is JsonObject obj && !this.Settings.HideClassId)
			//	{
			//TODO: BUGBUG: classId !
			//		obj[JsonTokens.CustomClassAttribute] = "....";
			//	}
			//}
			return true;
		}

		internal delegate JsonValue CrystalJsonTypePacker(object? instance, CrystalJsonSettings settings, ICrystalJsonTypeResolver resolver);

		private static QuasiImmutableCache<Type, Maybe<CrystalJsonTypePacker>> DuckTypedJsonPackMethods { get; } = new(GetInstanceJsonPackHandler, TypeEqualityComparer.Default);

		private static Maybe<CrystalJsonTypePacker> GetInstanceJsonPackHandler([DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicMethods | DynamicallyAccessedMemberTypes.NonPublicMethods)] Type type)
		{
			// recherche une méthode d'instance "JsonPack"
			var m = type.GetMethod(nameof(IJsonPackable.JsonPack), BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
			if (m != null)
			{
				var packer = CreatePackerForJsonPackMethod(type, m);
				return new Maybe<CrystalJsonTypePacker>(packer);
			}

			//TODO: autre approche?

			// aucun trouvé
			return Maybe.Nothing<CrystalJsonTypePacker>();
		}

		[Pure]
		private static CrystalJsonTypePacker CreatePackerForJsonPackMethod(Type type, MethodInfo m)
		{
			// génère un packer: (value) => ((Type)value).JsonPack(CrystalJsonSettings, ICrystalJsonTypeResolver)

			var prmValue = Expression.Parameter(typeof(object), "value");
			var prmSettings = Expression.Parameter(typeof(CrystalJsonSettings), "settings");
			var prmResolver = Expression.Parameter(typeof(ICrystalJsonTypeResolver), "resolver");

			// * Il faut éventuellement caster la valeur de object vers le type attendu par la méthode
			var castedInstance = prmValue.CastFromObject(type);

			var prms = m.GetParameters();

			// body == "((Type) value).JsonPack([settings, [resolver]])"
			var body = prms.Length == 0 ? Expression.Call(castedInstance, m)
				: prms.Length == 1 ? Expression.Call(castedInstance, m, prmSettings)
				: prms.Length == 2 ? Expression.Call(castedInstance, m, prmSettings, prmResolver)
				: throw new InvalidOperationException("Invalid signature for JsonPack instance method");

			return Expression.Lambda<CrystalJsonTypePacker>(body, "<>_" + type.Name + "_JsonUnpack", true, [ prmValue, prmSettings, prmResolver ]).Compile();
		}

	}

}
