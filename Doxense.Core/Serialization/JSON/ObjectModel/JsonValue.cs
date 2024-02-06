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
	using System.Text;
	using Doxense.Diagnostics.Contracts;
	using Doxense.Memory;
	using JetBrains.Annotations;

	[Serializable]
	[CannotApplyEqualityOperator]
	//[DebuggerNonUserCode]
	public abstract partial class JsonValue : IEquatable<JsonValue>, IComparable<JsonValue>, IJsonDynamic, IJsonSerializable, IJsonConvertible, IFormattable, ISliceSerializable
	{
		/// <summary>Type du token JSON</summary>
		public abstract JsonType Type { [Pure] get; }

		/// <summary>Conversion en object CLR (type automatique)</summary>
		[Pure]
		public abstract object? ToObject();

		/// <summary>Bind vers un type CLR spécifique</summary>
		/// <param name="type">Type CLR désiré</param>
		/// <param name="resolver">Resolver (optionnel)</param>
		[Pure]
		public abstract object? Bind(Type? type, ICrystalJsonTypeResolver? resolver = null);

		/// <summary>Indique si cette valeur est null</summary>
		public virtual bool IsNull { [Pure] get => false; }

		/// <summary>Indique si cette valeur correspond au défaut du type (0, null, empty)</summary>
		public abstract bool IsDefault { [Pure] get; }

		/// <summary>Indique si cette valeur est une array qui contient d'autres valeurs</summary>
		public bool IsArray { [Pure] get => this.Type == JsonType.Array; }

		/// <summary>Indique si cette valeur est une dictionnaire qui contient d'autres valeurs</summary>
		public bool IsMap { [Pure] get => this.Type == JsonType.Object; }

		/// <summary>Heuristique qui détermine si cette valeur est considérée comme "petite" et peut être dumpée dans un log sans flooder</summary>
		internal abstract bool IsSmallValue();

		/// <summary>Indique si ce type de valeur est assez petite pour être affichée en une seule ligne quand présente dans un petit tableau</summary>
		/// <returns></returns>
		internal abstract bool IsInlinable();
		/// <summary>Sérialise une valeur JSON en string</summary>
		/// <param name="settings">Settings JSON à utiliser (optionnel)</param>
		/// <returns>Chaîne de texte correspondant à la valeur JSON</returns>
		[Pure]
		public virtual string ToJson(CrystalJsonSettings? settings = null)
		{
			// implémentation générique
			if (this.IsNull) return JsonTokens.Null;
			var sb = new StringBuilder(this.IsArray || this.IsMap ? 256 : 64);
			var writer = new CrystalJsonWriter(sb, settings ?? CrystalJsonSettings.Json, null);
			JsonSerialize(writer);
			return sb.ToString();
		}

		/// <summary>Retourne une chaîne "compact" représentant tout (ou partie) de cette valeur pour les logs</summary>
		[Pure]
		internal virtual string GetCompactRepresentation(int depth)
		{
			return this.ToJson();
		}

		/// <summary>Formate la valeur JSON en une string</summary>
		/// <remarks>Voir <see cref="ToString(string,IFormatProvider)"/> pour la liste des formats supportés</remarks>
		public override string ToString()
		{
			return ToString(null, null);
		}

		/// <summary>Formate la valeur JSON en une string</summary>
		/// <param name="format">Format désiré (voir remarques), ou "D" par défaut</param>
		/// <remarks>Voir <see cref="ToString(string,IFormatProvider)"/> pour la liste des formats supportés</remarks>
		public string ToString(string? format)
		{
			return ToString(format, null);
		}

		/// <summary>Formate la valeur JSON en une string</summary>
		/// <param name="format">Format désiré (voir remarques), ou "D" par défaut</param>
		/// <param name="provider">Ignoré</param>
		/// <remarks>Les formats supportés sont:
		/// <list type="bullet">
		///   <listheader><term>format</term><description>foo</description></listheader>
		///   <item><term>D</term><description>Default, équivalent de <see cref="ToJson"/> en mode <see cref="CrystalJsonSettings.Json"/></description></item>
		///   <item><term>C</term><description>Compact, équivalent de <see cref="ToJson"/> en mode <see cref="CrystalJsonSettings.JsonCompact"/></description></item>
		///   <item><term>P</term><description>Pretty, équivalent de <see cref="ToJson"/> en mode <see cref="CrystalJsonSettings.JsonIndented"/></description></item>
		///   <item><term>J</term><description>JavaScript, équivalent de <see cref="ToJson"/> en mode <see cref="CrystalJsonSettings.JavaScript"/>.</description></item>
		///   <item><term>Q</term><description>Quick, équivalent de <see cref="GetCompactRepresentation"/>, qui retourne une version simplifiée ou partielle du JSON, adaptée pour des logs/traces.</description></item>
		/// </list>
		/// </remarks>
		public virtual string ToString(string? format, IFormatProvider? provider)
		{
			switch(format ?? "D")
			{
				case "D":
				case "d":
				{ // "D" is for Default!
					return this.ToJson();
				}
				case "C":
				case "c":
				{ // "C" is for Compact!
					return this.ToJsonCompact();
				}
				case "P":
				case "p":
				{ // "P" is for Pretty!
					return this.ToJsonIndented();
				}
				case "Q":
				case "q":
				{ // "Q" is for Quick!
					return GetCompactRepresentation(0);
				}

				case "J":
				case "j":
				{ // "J" is for Javascript!
					return CrystalJson.Serialize(this, CrystalJsonSettings.JavaScript);
				}
				default:
				{
#if DEBUG
					if (format!.EndsWith("}}", StringComparison.Ordinal))
					{
						// ATTENTION! vous avez un bug dans un formatter de string interpolation!
						// Si vous écrivez "... {{{obj:P}}}", ca va parser "P}}" comme le string format!!
						if (System.Diagnostics.Debugger.IsAttached) System.Diagnostics.Debugger.Break();
					}
#endif
					throw new NotSupportedException($"Invalid JSON format '{format}' specification");
				}
			}
		}

		JsonValue IJsonDynamic.GetJsonValue()
		{
			return this;
		}

		/// <summary>Crée une copie de l'objet (en copier les références sur ses fils, s'il en a)</summary>
		/// <returns>Nouvelle version de l'objet (note: peut être le même pour les valeurs immutable)</returns>
		/// <remarks>Ne clone pas les éléments de cet objet (JsonArray, JsonObject)</remarks>
		[Pure]
		public JsonValue Copy()
		{
			return Clone(false);
		}

		/// <summary>Crée une copie complète de l'objet, en clonant éventuellement les éléments de cet objet (s'il en a)</summary>
		/// <param name="deep">Si true, clone également les fils de cet object. Si false, ne copie que les références.</param>
		/// <returns>Nouvelle version de l'objet (note: peut être le même pour les valeurs immutable)</returns>
		[Pure]
		public virtual JsonValue Copy(bool deep)
		{
			return Clone(deep);
		}

		[Pure]
		protected virtual JsonValue Clone(bool deep)
		{
			// la plupart des implémentation sont immutable
			return this;
		}

		public override bool Equals(object? obj)
		{
			return obj is JsonValue value && Equals(value);
		}

		public abstract bool Equals(JsonValue? other);

		public static bool Equals(JsonValue? left, JsonValue? right) => (left ?? JsonNull.Missing).Equals(right ?? JsonNull.Missing);

		public static int Compare(JsonValue? left, JsonValue? right) => (left ?? JsonNull.Missing).CompareTo(right ?? JsonNull.Missing); 

		// force les classes filles a override GetHashCode!
		public abstract override int GetHashCode();

		public virtual int CompareTo(JsonValue? other)
		{
			if (other == null) return this.IsNull ? 0 : +1;
			if (object.ReferenceEquals(this, other)) return 0;

			// protection contre les JsonObject
			if (other.IsMap) throw ThrowHelper.InvalidOperationException("Cannot compare a JSON value with a JsonObject");

			// pas vraiment de solution magique, on va comparer les type et les hashcode (pas pire que mieux)
			int c = ((int)this.Type).CompareTo((int)other.Type);
			if (c == 0) c = this.GetHashCode().CompareTo(other.GetHashCode());
			return c;
		}

		public virtual bool Contains(JsonValue? value) => false;

		/// <summary>Retourne la valeur d'un champ, si cette valeur est un objet JSON</summary>
		/// <param name="key">Nom du champ à retourner</param>
		/// <returns>Valeur de ce champ, ou missing si le champ n'existe. Une exception si cette valeur n'est pas un objet JSON</returns>
		/// <exception cref="System.ArgumentNullException">Si <paramref name="key"/> est null.</exception>
		/// <exception cref="System.InvalidOperationException">Cet valeur JSON ne supporte pas la notion d'indexation</exception>
		public virtual JsonValue this[string key]
		{
			[Pure, CollectionAccess(CollectionAccessType.Read)]
			get => throw ThrowHelper.InvalidOperationException($"Cannot access child '{key}' on {this.GetType().Name}");
			[CollectionAccess(CollectionAccessType.ModifyExistingContent)]
			set => throw ThrowHelper.InvalidOperationException($"Cannot set child '{key}' on {this.GetType().Name}");
		}

		/// <summary>Retourne la valeur d'un élément d'après son index, si cette valeur est une array JSON</summary>
		/// <param name="index">Index de l'élément à retourner</param>
		/// <returns>Valeur de l'élément à l'index spécifié. Une exception si cette valeur n'est pas une array JSON, ou si l'index est en dehors des bornes</returns>
		/// <exception cref="System.InvalidOperationException">Cet valeur JSON ne supporte pas la notion d'indexation</exception>
		/// <exception cref="IndexOutOfRangeException"><paramref name="index"/> est en dehors des bornes du tableau</exception>
		public virtual JsonValue this[int index]
		{
			[Pure, CollectionAccess(CollectionAccessType.Read)]
			get => throw ThrowHelper.InvalidOperationException($"Cannot access value at index {index} on a {this.GetType().Name}");
			[CollectionAccess(CollectionAccessType.ModifyExistingContent)]
			set => throw ThrowHelper.InvalidOperationException($"Cannot set value at index {index} on a {this.GetType().Name}");
		}

		//BLACK MAGIC!

		// Pour pouvoir écrire "if (obj["Hello"]) ... else ...", il faut que JsonValue implémente l'opérateur 'op_true' (et 'op_false')
		// Pour pouvoir écrire "if (obj["Hello"] && obj["World"]) ...." (resp. '||') il faut que JsonValue implémente l'opérateur 'op_&' (resp: 'op_|'),
		// et que celui ci retourne aussi un JsonValue (qui sera passé en paramètre à 'op_true'/'op_false'.

		public static bool operator true(JsonValue? obj)
		{
			return obj != null && obj.ToBoolean();
		}

		public static bool operator false(JsonValue? obj)
		{
			return obj == null || obj.ToBoolean();
		}

		public static JsonValue operator &(JsonValue? left, JsonValue? right)
		{
			//REVIEW:TODO: peut être gérer le cas de deux number pour faire le vrai binary AND ?
			return left != null && right!= null && left.ToBoolean() && right.ToBoolean() ? JsonBoolean.True : JsonBoolean.False;
		}

		public static JsonValue operator |(JsonValue? left, JsonValue? right)
		{
			//REVIEW:TODO: peut être gérer le cas de deux number pour faire le vrai binary AND ?
			return (left != null && left.ToBoolean()) || (right != null && right.ToBoolean()) ? JsonBoolean.True : JsonBoolean.False;
		}

		public static bool operator <(JsonValue? left, JsonValue? right)
		{
			return (left ?? JsonNull.Null).CompareTo(right ?? JsonNull.Null) < 0;
		}

		public static bool operator <=(JsonValue? left, JsonValue? right)
		{
			return (left ?? JsonNull.Null).CompareTo(right ?? JsonNull.Null) <= 0;
		}

		public static bool operator >(JsonValue? left, JsonValue? right)
		{
			return (left ?? JsonNull.Null).CompareTo(right ?? JsonNull.Null) > 0;
		}

		public static bool operator >=(JsonValue? left, JsonValue? right)
		{
			return (left ?? JsonNull.Null).CompareTo(right ?? JsonNull.Null) >= 0;
		}

		#region IJsonSerializable

		public abstract void JsonSerialize(CrystalJsonWriter writer);

		void IJsonSerializable.JsonDeserialize(JsonObject value, Type declaredType, ICrystalJsonTypeResolver resolver)
		{
			throw new NotSupportedException("Don't use this method!");
		}

		#endregion

		#region IJsonConvertible...

		public virtual string? ToStringOrDefault() => ToString();

		public virtual bool ToBoolean() => throw Errors.JsonConversionNotSupported(this, typeof(bool));

		public virtual bool? ToBooleanOrDefault() => ToBoolean();

		public virtual byte ToByte() => throw Errors.JsonConversionNotSupported(this, typeof(byte));

		public virtual byte? ToByteOrDefault() => ToByte();

		public virtual sbyte ToSByte() => throw Errors.JsonConversionNotSupported(this, typeof(sbyte));

		public virtual sbyte? ToSByteOrDefault() => ToSByte();

		public virtual char ToChar() => throw Errors.JsonConversionNotSupported(this, typeof(char));

		public virtual char? ToCharOrDefault() => ToChar();

		public virtual short ToInt16() => throw Errors.JsonConversionNotSupported(this, typeof(short));

		public virtual short? ToInt16OrDefault() => ToInt16();

		public virtual ushort ToUInt16() => throw Errors.JsonConversionNotSupported(this, typeof(ushort));

		public virtual ushort? ToUInt16OrDefault() => ToUInt16();

		public virtual int ToInt32() => throw Errors.JsonConversionNotSupported(this, typeof(int));

		public virtual int? ToInt32OrDefault() => ToInt32();

		public virtual uint ToUInt32() => throw Errors.JsonConversionNotSupported(this, typeof(uint));

		public virtual uint? ToUInt32OrDefault() => ToUInt32();

		public virtual long ToInt64() => throw Errors.JsonConversionNotSupported(this, typeof(long));

		public virtual long? ToInt64OrDefault() => ToInt64();

		public virtual ulong ToUInt64() => throw Errors.JsonConversionNotSupported(this, typeof(ulong));

		public virtual ulong? ToUInt64OrDefault() => ToUInt64();

		public virtual float ToSingle() => throw Errors.JsonConversionNotSupported(this, typeof(float));

		public virtual float? ToSingleOrDefault() => ToSingle();

		public virtual double ToDouble() => throw Errors.JsonConversionNotSupported(this, typeof(double));

		public virtual double? ToDoubleOrDefault() => ToDouble();

		public virtual decimal ToDecimal() => throw Errors.JsonConversionNotSupported(this, typeof(decimal));

		public virtual decimal? ToDecimalOrDefault() => ToDecimal();

		public virtual Guid ToGuid() => throw Errors.JsonConversionNotSupported(this, typeof(Guid));

		public virtual Guid? ToGuidOrDefault() => ToGuid();

		public virtual Uuid128 ToUuid128() => throw Errors.JsonConversionNotSupported(this, typeof(Uuid128));

		public virtual Uuid128? ToUuid128OrDefault() => ToUuid128();

		public virtual Uuid96 ToUuid96() => throw Errors.JsonConversionNotSupported(this, typeof(Uuid96));

		public virtual Uuid96? ToUuid96OrDefault() => ToUuid96();

		public virtual Uuid80 ToUuid80() => throw Errors.JsonConversionNotSupported(this, typeof(Uuid80));

		public virtual Uuid80? ToUuid80OrDefault() => ToUuid80();

		public virtual Uuid64 ToUuid64() => throw Errors.JsonConversionNotSupported(this, typeof(Uuid64));

		public virtual Uuid64? ToUuid64OrDefault() => ToUuid64();

		public virtual DateTime ToDateTime() => throw Errors.JsonConversionNotSupported(this, typeof(DateTime));

		public virtual DateTime? ToDateTimeOrDefault() => ToDateTime();

		public virtual DateTimeOffset ToDateTimeOffset() => throw Errors.JsonConversionNotSupported(this, typeof(DateTimeOffset));

		public virtual DateTimeOffset? ToDateTimeOffsetOrDefault() => ToDateTimeOffset();

		public virtual TimeSpan ToTimeSpan() => throw Errors.JsonConversionNotSupported(this, typeof(TimeSpan));

		public virtual TimeSpan? ToTimeSpanOrDefault() => ToTimeSpan();

		public virtual TEnum ToEnum<TEnum>()
			where TEnum : struct, Enum
			=> throw Errors.JsonConversionNotSupported(this, typeof(TimeSpan));

		public virtual TEnum? ToEnumOrDefault<TEnum>()
			where TEnum : struct, Enum
			=> ToEnum<TEnum>();

		public virtual NodaTime.Instant ToInstant() => throw Errors.JsonConversionNotSupported(this, typeof(NodaTime.Instant));

		public virtual NodaTime.Instant? ToInstantOrDefault() => ToInstant();

		public virtual NodaTime.Duration ToDuration() => throw Errors.JsonConversionNotSupported(this, typeof(NodaTime.Duration));

		public virtual NodaTime.Duration? ToDurationOrDefault() => ToDuration();
		//TODO: ToZonedDateTime, ToLocalDateTime ?

		#endregion

		#region ISliceSerializable...

		public abstract void WriteTo(ref SliceWriter writer);

		#endregion

	}
}
