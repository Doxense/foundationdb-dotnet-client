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
	using System.ComponentModel;
	using System.Diagnostics;
	using System.Globalization;
	using System.Text;
	using Doxense.Diagnostics.Contracts;
	using Doxense.Memory;
	using JetBrains.Annotations;

	[Serializable]
	[CannotApplyEqualityOperator]
	[DebuggerNonUserCode]
	[JetBrains.Annotations.PublicAPI]
	public abstract partial class JsonValue : IEquatable<JsonValue>, IComparable<JsonValue>, IJsonDynamic, IJsonSerializable, IJsonConvertible, IFormattable, ISliceSerializable
	{
		/// <summary>Type du token JSON</summary>
		public abstract JsonType Type { [Pure] get; }

		/// <summary>Conversion en object CLR (type automatique)</summary>
		[Pure]
		[EditorBrowsable(EditorBrowsableState.Advanced)]
		public abstract object? ToObject();

		/// <summary>Bind vers un type CLR spécifique</summary>
		/// <param name="type">Type CLR désiré</param>
		/// <param name="resolver">Optional custom resolver used to bind the value into a managed type.</param>
		/// <exception cref="JsonBindingException">If the value cannot be bound to the specified type.</exception>
		[Pure]
		public abstract object? Bind(Type? type, ICrystalJsonTypeResolver? resolver = null);

		/// <summary>Bind vers un type CLR spécifique</summary>
		/// <typeparam name="TValue">Type CLR désiré</typeparam>
		/// <param name="resolver">Optional custom resolver used to bind the value into a managed type.</param>
		/// <exception cref="JsonBindingException">If the value cannot be bound to the specified type.</exception>
		public virtual TValue? Bind<TValue>(ICrystalJsonTypeResolver? resolver = null) => (TValue?) Bind(typeof(TValue), resolver);

		/// <summary>Indique si cette valeur est null</summary>
		[EditorBrowsable(EditorBrowsableState.Advanced)]
		public virtual bool IsNull { [Pure] get => false; }

		/// <summary>Indique si cette valeur correspond au défaut du type (0, null, empty)</summary>
		[EditorBrowsable(EditorBrowsableState.Advanced)]
		public abstract bool IsDefault { [Pure] get; }

		/// <summary>Indique si cette valeur est une array qui contient d'autres valeurs</summary>
		[Obsolete("Either check that the Type property is JsonType.Array, or cast to JsonArray")]
		[EditorBrowsable(EditorBrowsableState.Never)]
		public bool IsArray { [Pure] get => this.Type == JsonType.Array; }

		/// <summary>Indique si cette valeur est une dictionnaire qui contient d'autres valeurs</summary>
		[Obsolete("Either check that the Type property is JsonType.Object, or cast to JsonObject")]
		[EditorBrowsable(EditorBrowsableState.Never)]
		public bool IsMap { [Pure] get => this.Type == JsonType.Object; }

		/// <summary>Returns <see langword="true"/> if this value is read-only, and cannot be modified, or <see langword="false"/> if it allows mutations.</summary>
		/// <remarks>
		/// <para>Only JSON Objects and Arrays can return <see langword="false"/>. All other "value types" (string, boolean, numbers, ...) are always immutable, and will always be read-only.</para>
		/// <para>If you need to modify a JSON Object or Array that is read-only, you should first create a copy, by calling either <see cref="Copy"/> or <see cref="ToMutable"/>, perform any changes required, and then either <see cref="Freeze"/> the copy, or call <see cref="ToReadOnly"/> again.</para>
		/// </remarks>
		[EditorBrowsable(EditorBrowsableState.Advanced)]
		public abstract bool IsReadOnly { [Pure] get; }

		/// <summary>Prevents any future mutations of this JSON value (of type Object or Array), by recursively freezing it and all of its children.</summary>
		/// <returns>The same instance, which is now converted to a read-only instance.</returns>
		/// <remarks>
		/// <para>Any future attempt to modify this object, or any of its children that was previously mutable, will fail.</para>
		/// <para>If this instance is already read-only, this will be a no-op.</para>
		/// <para>This should only be used with care, and only on objects that are entirely owned by the called, or that have not been published yet. Freezing a shared mutable object may cause issues for other threads that still were expecting a mutable isntance.</para>
		/// <para>If you need to modify a JSON Object or Array that is read-only, you should first create a copy, by calling either <see cref="Copy"/> or <see cref="ToMutable"/>, perform any changes required, and then either <see cref="Freeze"/> the copy, or call <see cref="ToReadOnly"/> again.</para>
		/// </remarks>
		[EditorBrowsable(EditorBrowsableState.Advanced)]
		public virtual JsonValue Freeze() => this;

		/// <summary>Returns a read-only copy of this value, unless it is already read-only.</summary>
		/// <returns>The same instance if it is already read-only, or a new read-only deep copy, if it was mutable.</returns>
		/// <remarks>
		/// <para>The value returned is guaranteed to be immutable, and is safe to cache, share, or use as a singleton.</para>
		/// <para>Only JSON Objects and Arrays are impacted. All other "value types" (strings, booleans, numbers, ...) are always immutable, and will not be copied.</para>
		/// <para>If you need to modify a JSON Object or Array that is read-only, you should first create a copy, by calling either <see cref="Copy"/> or <see cref="ToMutable"/>, perform any changes required, and then either <see cref="Freeze"/> the copy, or call <see cref="ToReadOnly"/> again.</para>
		/// </remarks>
		[Pure]
		public virtual JsonValue ToReadOnly() => this;

		/// <summary>Convert this JSON value so that it, or any of its children that were previously read-only, can be mutated.</summary>
		/// <returns>The same instance if it is already fully mutable, OR a copy where any read-only Object or Array has been converted to allow mutations.</returns>
		/// <remarks>
		/// <para>Will return the same instance if it is already mutable, or a new deep copy with all children marked as mutable.</para>
		/// <para>This attempts to only copy what is necessary, and will not copy objects or arrays that are already mutable, or all other "value types" (strings, booleans, numbers, ...) that are always immutable.</para>
		/// </remarks>
		[Pure]
		public virtual JsonValue ToMutable() => this;

		/// <summary>Returns <see langword="true"/> if this value is considered as "small" and can be safely written to a debug log (without flooding).</summary>
		/// <remarks>
		/// <para>For example, any JSON Object or Array must have 5 children or less, all small values, to be considered "small". Likewise, a JSON String literal must have a length or 36 characters or less, to be considered "small".</para>
		/// <para>When generating a compact representation of a JSON value, for troubleshooting purpose, an Object or Array that is not "small", may be written as <c>[ 1, 2, 3, 4, 5, ...]</c> with the rest of the value ommitted.</para>
		/// </remarks>
		[Pure]
		[EditorBrowsable(EditorBrowsableState.Never)]
		internal abstract bool IsSmallValue();

		/// <summary>Returns <see langword="true"/> if this value is considered as "small" enough to be inlined with its parent when generating compact or one-line JSON, like a small array or JSON object.</summary>
		[Pure]
		[EditorBrowsable(EditorBrowsableState.Never)]
		internal abstract bool IsInlinable();

		/// <summary>Serializes this JSON value into a JSON string literal</summary>
		/// <param name="settings">Settings used to change the serialized JSON output (optional)</param>
		/// <returns>JSON text literal that can be written to disk, returned as the body of an HTTP request, or </returns>
		/// <example><c>var jsonText = new JsonObject() { ["hello"] = "world" }.ToJson(); // == "{ \"hello\": \"world\" }"</c></example>
		[Pure]
		[EditorBrowsable(EditorBrowsableState.Always)]
		public virtual string ToJson(CrystalJsonSettings? settings = null)
		{
			// implémentation générique
			if (this.IsNull)
			{
				return JsonTokens.Null;
			}

			var sb = new StringBuilder(this is JsonArray or JsonObject ? 256 : 64);
			var writer = new CrystalJsonWriter(sb, settings ?? CrystalJsonSettings.Json, null);
			JsonSerialize(writer);
			return sb.ToString();
		}

		/// <summary>Returns a "compact" string representation of this value, that can fit into a troubleshooting log.</summary>
		/// <remarks>
		/// <para>If the value is too large, parts of it will be shortened by adding <c>', ...'</c> tokens, so that it is still readable by a human.</para>
		/// <para>Note: the string that is return is not valid JSON and may not be parsable! This is only intended for quick introspection of a JSON document, by a human, using a log file or console output.</para>
		/// </remarks>
		[Pure]
		internal virtual string GetCompactRepresentation(int depth) => this.ToJson();

		/// <summary>Converts this JSON value into a printable string</summary>
		/// <remarks>See <see cref="ToString(string,IFormatProvider)"/> if you need to specify a different format than the default</remarks>
		public override string ToString() => ToString(null, null);

		/// <summary>Converts this JSON value into a printable string, using the specified format</summary>
		/// <param name="format">Desired format, or "D" (default) if omitted</param>
		/// <remarks>See <see cref="ToString(string,IFormatProvider)"/> for the list of supported formats</remarks>
		public string ToString(string? format)
		{
			return ToString(format, null);
		}

		/// <summary>Converts this JSON value into a printable string, using the specified format and provider</summary>
		/// <param name="format">Desired format, or "D" (default) if omitted</param>
		/// <param name="provider">This parameter is ignored. JSON values are always formatted using <see cref="CultureInfo.InvariantCulture"/>.</param>
		/// <remarks>Supported values for <paramref name="format"/> are:
		/// <list type="bullet">
		///   <listheader><term>format</term><description>foo</description></listheader>
		///   <item><term>D</term><description>Default, equivalent to calling <see cref="ToJson"/> with <see cref="CrystalJsonSettings.Json"/></description></item>
		///   <item><term>C</term><description>Compact, equivalent to calling <see cref="ToJson"/> with <see cref="CrystalJsonSettings.JsonCompact"/></description></item>
		///   <item><term>P</term><description>Pretty, equivalent to calling <see cref="ToJson"/> with <see cref="CrystalJsonSettings.JsonIndented"/></description></item>
		///   <item><term>J</term><description>JavaScript, equivalent to calling <see cref="ToJson"/> with <see cref="CrystalJsonSettings.JavaScript"/>.</description></item>
		///   <item><term>Q</term><description>Quick, equivalent to calling <see cref="GetCompactRepresentation"/>, that will return a simplified/partial version, suitable for logs/traces.</description></item>
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
					return this.ToJson(CrystalJsonSettings.JavaScript);
				}
				case "B":
				case "b":
				{ // "B" is for Build!
					return JsonEncoding.Encode(this.ToJsonCompact());
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

		/// <summary>Creates a deep copy of this value (and all of its children)</summary>
		/// <returns>A new instance, isolated from the original.</returns>
		/// <remarks>
		/// <para>Any changes to this copy will not have any effect of the original, and vice-versa</para>
		/// <para>If the value is an immutable type (like <see cref="JsonString"/>, <see cref="JsonNumber"/>, <see cref="JsonBoolean"/>, ...) then the same instance will be returned.</para>
		/// <para>If the value is an array then a new array containing a </para>
		/// </remarks>
		public virtual JsonValue Copy() => Copy(deep: true, readOnly: false);

		/// <summary>Creates a copy of this object</summary>
		/// <param name="deep">If <see langword="true" />, recursively copy the children as well. If <see langword="false" />, perform a shallow copy that reuse the same children.</param>
		/// <param name="readOnly">If <see langword="true" />, the copy will become read-only. If <see langword="false" />, the copy will be writable.</param>
		/// <returns>Copy of the object, and optionally of its children (if <paramref name="deep"/> is <see langword="true" /></returns>
		/// <remarks>Performing a deep copy will protect against any change, but will induce a lot of memory allocations. For example, any child array will be cloned even if they will not be modified later on.</remarks>
		/// <remarks>Immutable JSON values (like strings, numbers, ...) will return themselves without any change.</remarks>
		[Pure]
		protected internal virtual JsonValue Copy(bool deep, bool readOnly) => this;

		public override bool Equals(object? obj) => obj is JsonValue value && Equals(value);

		public abstract bool Equals(JsonValue? other);

		/// <summary>Tests if two JSON values are equivalent</summary>
		public static bool Equals(JsonValue? left, JsonValue? right) => (left ?? JsonNull.Missing).Equals(right ?? JsonNull.Missing);

		/// <summary>Compares two JSON values, and returns an integer that indicates whether the first value precedes, follows, or occurs in the same position in the sort order as the second value.</summary>
		public static int Compare(JsonValue? left, JsonValue? right) => (left ?? JsonNull.Missing).CompareTo(right ?? JsonNull.Missing); 

		/// <summary>Returns a hashcode that can be used to quickly identify a JSON value.</summary>
		/// <remarks>
		/// <para>The hashcode is guaranteed to remain unchanged during the lifetime of the object.</para>
		/// <para>
		/// <b>Caution:</b> there is *NO* guarantee that two equivalent Objects or Arrays will have the same hash code! 
		/// This means that it is *NOT* safe to use a JSON object or array has the key of a Dictionary or other collection that uses hashcodes to quickly compare two instances.
		/// </para>
		/// </remarks>
		public abstract override int GetHashCode();

		public virtual int CompareTo(JsonValue? other)
		{
			if (other == null) return this.IsNull ? 0 : +1;
			if (ReferenceEquals(this, other)) return 0;

			// cannot compare a value type directly with an object or array
			if (other is JsonObject or JsonArray)
			{
				throw ThrowHelper.InvalidOperationException($"Cannot compare a JSON value with another value of type {other.Type}");
			}

			// pas vraiment de solution magique, on va comparer les type et les hashcode (pas pire que mieux)
			int c = ((int)this.Type).CompareTo((int)other.Type);
			if (c == 0)
			{
				c = this.GetHashCode().CompareTo(other.GetHashCode());
			}

			return c;
		}

		public virtual bool Contains(JsonValue? value) => false;

		/// <summary>Retourne la valeur d'un champ, si cette valeur est un objet JSON</summary>
		/// <param name="key">Nom du champ à retourner</param>
		/// <returns>Valeur de ce champ, ou missing si le champ n'existe. Une exception si cette valeur n'est pas un objet JSON</returns>
		/// <exception cref="System.ArgumentNullException">Si <paramref name="key"/> est null.</exception>
		/// <exception cref="System.InvalidOperationException">Cet valeur JSON ne supporte pas la notion d'indexation</exception>
		[EditorBrowsable(EditorBrowsableState.Always)]
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
		[EditorBrowsable(EditorBrowsableState.Always)]
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

		[EditorBrowsable(EditorBrowsableState.Advanced)]
		public abstract void WriteTo(ref SliceWriter writer);

		#endregion

	}
}
