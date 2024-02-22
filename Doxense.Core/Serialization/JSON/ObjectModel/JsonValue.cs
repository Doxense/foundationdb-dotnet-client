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
	using System.Diagnostics.CodeAnalysis;
	using System.Globalization;
	using System.Runtime.CompilerServices;
	using System.Text;
	using Doxense.Diagnostics.Contracts;
	using Doxense.Memory;
	using JetBrains.Annotations;
	using PureAttribute = System.Diagnostics.Contracts.PureAttribute;

	[Serializable]
	[CannotApplyEqualityOperator]
	[DebuggerNonUserCode]
	[PublicAPI]
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

		[Pure, MethodImpl(MethodImplOptions.NoInlining)]
		protected static InvalidOperationException FailDoesNotSupportIndexingRead(JsonValue value, string key) => new($"Cannot read property '{key}' on a JSON {value.Type}, because it is not a JSON Object");

		[Pure, MethodImpl(MethodImplOptions.NoInlining)]
		protected static InvalidOperationException FailDoesNotSupportIndexingRead(JsonValue value, int index) => new($"Cannot read value at index '{index}' on a JSON {value.Type}, because it is not a JSON Array");

		[Pure, MethodImpl(MethodImplOptions.NoInlining)]
		protected static InvalidOperationException FailDoesNotSupportIndexingRead(JsonValue value, Index index) => new($"Cannot read value at index '{index}' on a JSON {value.Type}, because it is not a JSON Array");

		[Pure, MethodImpl(MethodImplOptions.NoInlining)]
		protected static InvalidOperationException FailDoesNotSupportIndexingWrite(JsonValue value, string key) => new($"Cannot set proprty '{key}' on a JSON {value.Type}, because it is not a JSON Object");

		[Pure, MethodImpl(MethodImplOptions.NoInlining)]
		protected static InvalidOperationException FailDoesNotSupportIndexingWrite(JsonValue value, int index) => new($"Cannot set value at index '{index}' on a JSON {value.Type}, because it is not a JSON Array");

		[Pure, MethodImpl(MethodImplOptions.NoInlining)]
		protected static InvalidOperationException FailDoesNotSupportIndexingWrite(JsonValue value, Index index) => new($"Cannot set value at index '{index}' on a JSON {value.Type}, because it is not a JSON Array");

		[Pure, MethodImpl(MethodImplOptions.NoInlining)]
		protected static InvalidOperationException FailCannotMutateReadOnlyValue(JsonValue value) => new($"Cannot mutate JSON {value.Type} because it is marked as read-only.");

		[Pure, MethodImpl(MethodImplOptions.NoInlining)]
		protected static InvalidOperationException FailCannotMutateImmutableValue(JsonValue value) => new($"Cannot mutate JSON {value.Type} because it is immutable.");

		[Pure, CollectionAccess(CollectionAccessType.Read)]
		[EditorBrowsable(EditorBrowsableState.Never)]
		public virtual bool TryGetValue(string key, [MaybeNullWhen(false)] out JsonValue value)
		{
			value = null;
			return false;
		}

		[Pure, CollectionAccess(CollectionAccessType.Read)]
		[EditorBrowsable(EditorBrowsableState.Never)]
		public virtual bool TryGetValue(int index, [MaybeNullWhen(false)] out JsonValue value)
		{
			value = null;
			return false;
		}

		[Pure, CollectionAccess(CollectionAccessType.Read)]
		[EditorBrowsable(EditorBrowsableState.Never)]
		public virtual bool TryGetValue(Index index, [MaybeNullWhen(false)] out JsonValue value)
		{
			value = null;
			return false;
		}

		[Pure, CollectionAccess(CollectionAccessType.Read)]
		[EditorBrowsable(EditorBrowsableState.Never)]
		public virtual JsonValue GetValue(string key) => GetValueOrDefault(key, JsonNull.Missing);

		[Pure, CollectionAccess(CollectionAccessType.Read)]
		[EditorBrowsable(EditorBrowsableState.Never)]
		public virtual JsonValue GetValue(int index) => GetValueOrDefault(index, JsonNull.Missing);

		[Pure, CollectionAccess(CollectionAccessType.Read)]
		[EditorBrowsable(EditorBrowsableState.Never)]
		public virtual JsonValue GetValue(Index index) => GetValueOrDefault(index, JsonNull.Missing);

		[Pure, CollectionAccess(CollectionAccessType.Read)]
		[EditorBrowsable(EditorBrowsableState.Never)]
		public virtual JsonValue GetValueOrDefault(string key, JsonValue? defaultValue = null) => throw FailDoesNotSupportIndexingRead(this, key);

		/// <summary>Returns the value at the specified index, if it is contains inside the array's bound.</summary>
		/// <param name="index">Index of the value to retrieve</param>
		/// <param name="defaultValue">The value that is returned if the index is outside the bounds of the array.</param>
		/// <returns>The value located at the specified index, or <paramref name="defaultValue"/> if the index is outside the bounds of the array.</returns>
		[Pure, CollectionAccess(CollectionAccessType.Read)]
		[EditorBrowsable(EditorBrowsableState.Never)]
		public virtual JsonValue GetValueOrDefault(int index, JsonValue? defaultValue = null) => throw FailDoesNotSupportIndexingRead(this, index);

		/// <summary>Returns the value at the specified index, if it is contains inside the array's bound.</summary>
		/// <param name="index">Index of the value to retrieve</param>
		/// <param name="defaultValue">The value that is returned if the index is outside the bounds of the array.</param>
		/// <returns>The value located at the specified index, or <paramref name="defaultValue"/> if the index is outside the bounds of the array.</returns>
		[Pure, CollectionAccess(CollectionAccessType.Read)]
		[EditorBrowsable(EditorBrowsableState.Never)]
		public virtual JsonValue GetValueOrDefault(Index index, JsonValue? defaultValue = null) => throw FailDoesNotSupportIndexingRead(this, index);

		/// <summary>Retourne la valeur d'un champ, si cette valeur est un objet JSON</summary>
		/// <param name="key">Nom du champ à retourner</param>
		/// <returns>Valeur de ce champ, ou missing si le champ n'existe. Une exception si cette valeur n'est pas un objet JSON</returns>
		/// <exception cref="System.ArgumentNullException">Si <paramref name="key"/> est null.</exception>
		/// <exception cref="System.InvalidOperationException">Cet valeur JSON ne supporte pas la notion d'indexation</exception>
		[EditorBrowsable(EditorBrowsableState.Never)]
		public virtual JsonValue this[string key]
		{
			[Pure, CollectionAccess(CollectionAccessType.Read)]
			get => GetValue(key);
			[CollectionAccess(CollectionAccessType.ModifyExistingContent)]
			set => throw (this.IsReadOnly ? FailCannotMutateReadOnlyValue(this) : FailDoesNotSupportIndexingWrite(this, key));
		}

		/// <summary>Returns the element at the specified index, if the current value is a JSON Array</summary>
		/// <param name="index">Index of the element to return</param>
		/// <returns>Value of the element at the specified <paramref name="index"/>. An exception is thrown if the current value is not a JSON Array, or if the element is outside the bounds of the array</returns>
		/// <exception cref="System.InvalidOperationException">The current JSON value does not support indexing</exception>
		/// <exception cref="IndexOutOfRangeException"><paramref name="index"/> is outside the bounds of the array</exception>
		[AllowNull]
		[EditorBrowsable(EditorBrowsableState.Never)]
		public virtual JsonValue this[int index]
		{
			[Pure, CollectionAccess(CollectionAccessType.Read)]
			get => GetValue(index);
			[CollectionAccess(CollectionAccessType.ModifyExistingContent)]
			set => throw (this.IsReadOnly ? FailCannotMutateReadOnlyValue(this) : FailDoesNotSupportIndexingWrite(this, index));
		}

		/// <summary>Returns the element at the specified index, if the current value is a JSON Array</summary>
		/// <param name="index">Index of the element to return</param>
		/// <returns>Value of the element at the specified <paramref name="index"/>. An exception is thrown if the current value is not a JSON Array, or if the element is outside the bounds of the array</returns>
		/// <exception cref="System.InvalidOperationException">The current JSON value does not support indexing</exception>
		/// <exception cref="IndexOutOfRangeException"><paramref name="index"/> is outside the bounds of the array</exception>
		[AllowNull]
		[EditorBrowsable(EditorBrowsableState.Never)]
		public virtual JsonValue this[Index index]
		{
			[Pure, CollectionAccess(CollectionAccessType.Read)]
			get => GetValue(index);
			[CollectionAccess(CollectionAccessType.ModifyExistingContent)]
			set => throw (this.IsReadOnly ? ThrowHelper.InvalidOperationException($"Cannot mutate a read-only JSON {this.Type}") : ThrowHelper.InvalidOperationException($"Cannot set value at index {index} on a JSON {this.Type}"));
		}

		/// <summary>Returns the converted value of the <paramref name="key"/> property of this object.</summary>
		/// <param name="key">Name of the property</param>
		/// <returns>Converted value of the <paramref name="key"/> property into the type <typeparamref name="TValue"/>, or default(<typeparamref name="TValue"/>) if the property does not exist, or contains a <c>null</c> entry.</returns>
		/// <example>
		/// ({ "Hello": "World" }).Get&lt;string&gt;("Hello") // returns <c>"World"</c>
		/// ({ }).Get&lt;string&gt;("Hello") // returns <c>null</c>
		/// ({ }).Get&lt;int&gt;("Hello") // returns <c>0</c>
		/// ({ "Hello": null }).Get&lt;string&gt;("Hello") // returns <c>null</c>
		/// ({ "Hello": null }).Get&lt;int&gt;("Hello") // returns <c>0</c>
		/// </example>
		[Pure, CollectionAccess(CollectionAccessType.Read)]
		[EditorBrowsable(EditorBrowsableState.Never)]
		public virtual TValue? Get<TValue>(string key) => GetValue(key).As<TValue>();

		[Pure, CollectionAccess(CollectionAccessType.Read)]
		[EditorBrowsable(EditorBrowsableState.Never)]
		public virtual TValue? Get<TValue>(int index) => GetValue(index).As<TValue>();

		[Pure, CollectionAccess(CollectionAccessType.Read)]
		[EditorBrowsable(EditorBrowsableState.Never)]
		public virtual TValue? Get<TValue>(Index index) => GetValue(index).As<TValue>();

		/// <summary>Tries to get the value associated with the specified <paramref name="key" /> in the JSON Object.</summary>
		/// <param name="key">Name of the propertyThe key of the value to get.</param>
		/// <param name="defaultValue">The default value to return when the JSON object cannot find a value associated with the specified <paramref name="key" />, or it is null or missing.</param>
		/// <returns>A <typeparamref name="TValue" /> instance. When the method is successful, the returned object is the converted value associated with the specified <paramref name="key" />. When the method fails, it returns <paramref name="defaultValue" />.</returns>
		/// <remarks>Note that this will return <paramref name="defaultValue"/> event if the key exists but is explicitly null.</remarks>
		/// <example>
		/// ({ "Hello": "World"}).GetOrDefault&lt;string&gt;("Hello", "Bonjour") // => <c>"World"</c>
		/// ({ "Hello": "123"}).GetOrDefault&lt;int&gt;("Hello", 456) // => <c>123</c>
		/// ({ }).GetOrDefault&lt;string&gt;("Hello", "Bonjour") // => <c>"Bonjour"</c>
		/// ({ }).GetOrDefault&lt;int&gt;("Hello", 456) // => <c>456</c>
		/// ({ }).GetOrDefault&lt;int?&gt;("Hello", 456) // => <c>456</c>
		/// ({ "Hello": null }).GetOrDefault&lt;string&gt;("Hello", "Bonjour") // => <c>"Bonjour"</c>
		/// ({ "Hello": null }).GetOrDefault&lt;int&gt;("Hello", 456) // => <c>456</c>
		/// ({ "Hello": null }).GetOrDefault&lt;int?&gt;("Hello", 456) // => <c>456</c>
		/// </example>
		[Pure, CollectionAccess(CollectionAccessType.Read)]
		[EditorBrowsable(EditorBrowsableState.Never)]
		[return: NotNullIfNotNull(nameof(defaultValue))]
		public virtual TValue? GetOrDefault<TValue>(string key, TValue? defaultValue = default) => TryGetValue(key, out var child) ? (child.As<TValue>() ?? defaultValue) : defaultValue;

		/// <summary>Returns the converted value at the specified index, if it is contains inside the array's bound.</summary>
		/// <param name="index">Index of the value to retrieve</param>
		/// <param name="defaultValue">The value that is returned if the index is outside the bounds of the array.</param>
		/// <returns>The value located at the specified index converted into type <typeparamref name="TValue"/>, or <paramref name="defaultValue"/> if the index is outside the bounds of the array, OR the value is null or missing.</returns>
		[Pure, CollectionAccess(CollectionAccessType.Read)]
		[EditorBrowsable(EditorBrowsableState.Never)]
		[return: NotNullIfNotNull(nameof(defaultValue))]
		public virtual TValue? GetOrDefault<TValue>(int index, TValue? defaultValue = default) => TryGetValue(index, out var child) ? (child.As<TValue>() ?? defaultValue) : defaultValue;

		/// <summary>Returns the converted value at the specified index, if it is contains inside the array's bound.</summary>
		/// <param name="index">Index of the value to retrieve</param>
		/// <param name="defaultValue">The value that is returned if the index is outside the bounds of the array.</param>
		/// <returns>The value located at the specified index converted into type <typeparamref name="TValue"/>, or <paramref name="defaultValue"/> if the index is outside the bounds of the array, OR the value is null or missing.</returns>
		[Pure, CollectionAccess(CollectionAccessType.Read)]
		[EditorBrowsable(EditorBrowsableState.Never)]
		[return: NotNullIfNotNull(nameof(defaultValue))]
		public virtual TValue? GetOrDefault<TValue>(Index index, TValue? defaultValue = default) => TryGetValue(index, out var child) ? (child.As<TValue>() ?? defaultValue) : defaultValue;

		/// <summary>Returns the converted value of the <paramref name="key"/> property of this object, if it exists.</summary>
		/// <param name="key">Name of the property</param>
		/// <param name="value">If the property exists and is not equal to <c>null</c>, will receive its value converted into type <typeparamref name="TValue"/>.</param>
		/// <returns>Returns <see langword="true" /> if the value was found, and has been converted; otherwise, <see langword="false" />.</returns>
		/// <example>
		/// ({ "Hello": "World"}).TryGet&lt;string&gt;("Hello", out var value) // returns <see langword="true" /> and value will be equal to <c>"World"</c>
		/// ({ "Hello": "123"}).TryGet&lt;int&gt;("Hello", out var value) // returns <see langword="true" /> and value will be equal to <c>123"</c>
		/// ({ }).TryGet&lt;string&gt;("Hello", out var value) // returns <see langword="false" />, and value will be <c>null</c>
		/// ({ }).TryGet&lt;int&gt;("Hello", out var value) // returns <see langword="false" />, and value will be <c>0</c>
		/// ({ "Hello": null }).TryGet&lt;string&gt;("Hello") // returns <see langword="false" />, and value will be <c>null</c>
		/// ({ "Hello": null }).TryGet&lt;int&gt;("Hello") // returns <see langword="false" />, and value will be <c>0</c>
		/// </example>
		[Pure, CollectionAccess(CollectionAccessType.Read)]
		[EditorBrowsable(EditorBrowsableState.Never)]
		public virtual bool TryGet<TValue>(string key, [MaybeNullWhen(false)] out TValue value)
		{
			//TODO: REVIEW: should be return false, or fail if not supported? (note: this[xxx] throws on values that do not support indexing)
			value = default;
			return false;
		}

		[Pure, CollectionAccess(CollectionAccessType.Read)]
		[EditorBrowsable(EditorBrowsableState.Never)]
		public virtual bool TryGet<TValue>(int index, [MaybeNullWhen(false)] out TValue value)
		{
			//TODO: REVIEW: should be return false, or fail if not supported? (note: this[xxx] throws on values that do not support indexing)
			value = default;
			return false;
		}

		[Pure, CollectionAccess(CollectionAccessType.Read)]
		[EditorBrowsable(EditorBrowsableState.Never)]
		public virtual bool TryGet<TValue>(Index index, [MaybeNullWhen(false)] out TValue value)
		{
			//TODO: REVIEW: should be return false, or fail if not supported? (note: this[xxx] throws on values that do not support indexing)
			value = default;
			return false;
		}

		/// <summary>Returns the value at the specified path</summary>
		/// <param name="path">Path to the value. ex: <c>"foo"</c>, <c>"foo.bar"</c> or <c>"foo[2].baz"</c></param>
		/// <returns>Value found at this location, or <see cref="JsonNull.Missing"/> if no match was found</returns>
		[Pure, CollectionAccess(CollectionAccessType.Read)]
		[EditorBrowsable(EditorBrowsableState.Always)]
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
						if (name == null || current.IsNullOrMissing())
						{
							return current;
						}

						if (current is not JsonObject obj)
						{ // equivalent to null, but to notify that we tried to index into a value that is not an object
							return JsonNull.Error;
						}
						//TODO: OPTIMIZE: whenever .NET adds support for indexing Dictionary with RoS<char>, we will be able to skip this memory allocation!
						return obj.GetValue(name);

					}
					case JPathToken.Identifier:
					{
						name = tokenizer.GetIdentifierName();
						break;
					}
					case JPathToken.ObjectAccess:
					case JPathToken.ArrayIndex:
					{
						if (current.IsNullOrMissing())
						{
							return JsonNull.Missing;
						}

						if (name != null)
						{
							if (current is not JsonObject obj)
							{ // equivalent to null, but to notify that we tried to index into a value that is not an object
								return JsonNull.Error;
							}
							//TODO: OPTIMIZE: whenever .NET adds support for indexing Dictionary with RoS<char>, we will be able to skip this memory allocation!
							if (!obj.TryGetValue(name, out var child))
							{ // property not found
								return JsonNull.Missing;
							}
							current = child;
							name = null;
						}
						if (token == JPathToken.ArrayIndex)
						{
							var index = tokenizer.GetArrayIndex();
							if (current is not JsonArray arr)
							{ // equivalent to null, but to notify that we tried to index into a value that is not an array
								return JsonNull.Error;
							}
							if (!arr.TryGetValue(index, out var child))
							{ // index out of bounds
								return JsonNull.Missing;
							}
							current = child;
						}
						break;
					}
					default:
					{
						throw ThrowHelper.InvalidOperationException($"Unexpected {token} at offset {tokenizer.Offset}: '{path}'");
					}
				}
			}
		}

		/// <summary>Returns the converted value at the specified path</summary>
		/// <param name="path">Path to the value. ex: <c>"foo"</c>, <c>"foo.bar"</c> or <c>"foo[2].baz"</c></param>
		/// <param name="required">If <see langword="true"/>, and no match was found, or the value is null or missing, an exception is thrown; otherwise, the <see langword="default"/> of type <typeparamref name="TValue"/> is returned.</param>
		/// <returns>Value found at this location, converted into a instance of type <typeparamref name="TValue"/>, or <see langword="default"/> if no match was found and <paramref name="required"/> is <see langword="false"/>.</returns>
		[Pure, ContractAnnotation("required:true => notnull")]
		[EditorBrowsable(EditorBrowsableState.Always)]
		public TValue? GetPath<TValue>(string path, bool required = false)
		{
			var val = GetPath(path);
			return required ? val.RequiredPath(path).As<TValue>() : val.As<TValue>();
		}

		/// <summary>Returns the converted value at the specified path</summary>
		/// <param name="path">Path to the value. ex: <c>"foo"</c>, <c>"foo.bar"</c> or <c>"foo[2].baz"</c></param>
		/// <returns>Value found at this location, converted into a instance of type <typeparamref name="TValue"/>, or <see langword="default"/> if no match was found.</returns>
		[Pure]
		[EditorBrowsable(EditorBrowsableState.Always)]
		public TValue? GetPathOrDefault<TValue>(string path) => GetPath(path).As<TValue>();

		/// <summary>Returns the converted value at the specified path</summary>
		/// <param name="path">Path to the value. ex: <c>"foo"</c>, <c>"foo.bar"</c> or <c>"foo[2].baz"</c></param>
		/// <param name="defaultValue">The default value to return when the no match is found for the specified <paramref name="path" />, or it is null or missing.</param>
		/// <returns>Value found at this location, converted into a instance of type <typeparamref name="TValue"/>, or <paramref name="defaultValue"/> if no match was found or the value is null or missing.</returns>
		[Pure]
		[EditorBrowsable(EditorBrowsableState.Always)]
		public TValue? GetPathOrDefault<TValue>(string path, TValue? defaultValue)
		{
			var val = GetPath(path);
			return val.IsNullOrMissing() ? defaultValue : (val.As<TValue>() ?? defaultValue);
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

		public virtual Half ToHalf() => throw Errors.JsonConversionNotSupported(this, typeof(Half));

		public virtual Half? ToHalfOrDefault() => ToHalf();

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
