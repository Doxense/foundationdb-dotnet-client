#region Copyright (c) 2023-2025 SnowBank SAS
//
// All rights are reserved. Reproduction or transmission in whole or in part, in
// any form or by any means, electronic, mechanical or otherwise, is prohibited
// without the prior written consent of the copyright owner.
//
#endregion

namespace Doxense.Serialization.Json
{
	using System;
	using System.ComponentModel;
	using System.Diagnostics;
	using System.Diagnostics.CodeAnalysis;
	using System.Runtime.CompilerServices;

	/// <summary>Mutable JSON Object</summary>
	[DebuggerDisplay("Count={Count}, Path={ToString(),nq}")]
	[PublicAPI]
	public sealed class MutableJsonValue : IJsonProxyNode, IJsonSerializable, IJsonPackable, IEquatable<JsonValue>, IEquatable<MutableJsonValue>, IEnumerable<MutableJsonValue>
	{

		public MutableJsonValue(IMutableJsonContext? ctx, MutableJsonValue? parent, JsonPathSegment segment, JsonValue json)
		{
			Contract.Debug.Requires(json != null);
			this.Context = ctx;
			this.Parent = parent;
			this.Segment = segment;
			this.Json = json;
			this.Depth = (parent?.Depth ?? -1) + 1;
		}

		/// <summary>Returns an untracked mutable JSON value</summary>
		[Pure, MustUseReturnValue]
		public static MutableJsonValue Untracked(JsonValue value) => new(null, null, default, value);

		/// <summary>Returns a mutable JSON value tracked by a transaction</summary>
		public static MutableJsonValue Tracked(IMutableJsonContext tr, JsonValue value) => new(tr, null, default, value);

		/// <inheritdoc />
		public override bool Equals(object? obj) => obj switch
		{
			MutableJsonValue value => ReferenceEquals(obj, this) || this.Json.StrictEquals(value.Json),
			JsonValue value => this.Json.StrictEquals(value),
			null => this.IsNullOrMissing(),
			_ => false,
		};

		/// <inheritdoc />
		[EditorBrowsable(EditorBrowsableState.Never)]
		public override int GetHashCode() => throw new NotSupportedException("This instance can change value, and MUST NOT be used as a key in a dictionary!");

		/// <inheritdoc />
		public bool Equals(MutableJsonValue? other) => ReferenceEquals(this, other) || this.Json.StrictEquals(other?.Json);

		/// <inheritdoc />
		public bool Equals(JsonValue? other) => this.Json.StrictEquals(other);

		/// <inheritdoc />
		public override string ToString() => this.Segment.IsEmpty() ? this.Json.ToString("Q") : $"{this.GetPath()}: {this.Json.ToString("Q")}";

		/// <summary>Contains the read-only version of this JSON object</summary>
		internal JsonValue Json { get; private set; }

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public JsonValue ToJson() => this.Json;

		internal IMutableJsonContext? Context { get; }

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public IMutableJsonContext? GetContext() => this.Context;

		#region Path...

		/// <summary>Parent of this value, or <see langword="null"/> if this is the root of the document</summary>
		internal readonly MutableJsonValue? Parent;

		IJsonProxyNode? IJsonProxyNode.Parent => this.Parent;

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public MutableJsonValue? GetParent() => Parent;

		internal int Depth { get; }

		int IJsonProxyNode.Depth => this.Depth;

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public int GetDepth() => this.Depth;

		/// <inheritdoc />
		public JsonType Type => this.Json.Type;

		/// <summary>Name of the field that contains this value in its parent object, or <see langword="null"/> if it was not part of an object</summary>
		internal readonly JsonPathSegment Segment;

		JsonPathSegment IJsonProxyNode.Segment => this.Segment;

		/// <summary>Tests if this is the top-level node of the document</summary>
		public bool IsRoot() => this.Segment.IsEmpty();

		/// <inheritdoc />
		public JsonPath GetPath() => JsonProxyNodeExtensions.ComputePath(this.Parent, in this.Segment);

		/// <inheritdoc />
		public JsonPath GetPath(JsonPathSegment child) => JsonProxyNodeExtensions.ComputePath(this.Parent, in this.Segment, child);

		/// <inheritdoc />
		public void WritePath(ref JsonPathBuilder sb)
		{
			this.Parent?.WritePath(ref sb);
			sb.Append(this.Segment);
		}

		#endregion

		#region IDictionary<TKey, TValue>...

		/// <summary>Number of fields in this object</summary>
		public int Count => this.Json switch
		{
			JsonObject obj => obj.Count,
			JsonArray arr  => arr.Count,
			_              => 0 // what should we do here?
		};

		/// <summary>Tests if the wrapped JSON value is of the given type</summary>
		/// <param name="type">Expected type of the value</param>
		/// <returns><c>true</c> if the value is of this type; otherwise, <c>false</c></returns>
		/// <remarks>This method will record a <see cref="ObservableJsonAccess.Type"/> access.</remarks>
		public bool IsOfType(JsonType type)
		{
			return this.Json.Type == type;
		}

		/// <summary>Tests if the wrapped JSON value is of the given type, or is null-or-missing</summary>
		/// <param name="type">Expected type of the value</param>
		/// <returns><c>true</c> if the value is of this type, null or missing; otherwise, <c>false</c></returns>
		/// <remarks>This method will record a <see cref="ObservableJsonAccess.Type"/> access.</remarks>
		public bool IsOfTypeOrNull(JsonType type)
		{
			return this.Json.Type == type || this.Json.Type == JsonType.Null;
		}

		/// <summary>Tests if the wrapped JSON value is a non-null array.</summary>
		/// <returns><c>true</c> if the value is an array; otherwise, <c>false</c></returns>
		/// <remarks>This method will record a <see cref="ObservableJsonAccess.Type"/> access.</remarks>
		public bool IsArray() => IsOfType(JsonType.Array);

		/// <summary>Tests if the wrapped JSON value is either an array, or null-or-missing.</summary>
		/// <returns><c>true</c> if the value is an array, null or missing; otherwise, <c>false</c></returns>
		/// <remarks>This method will record a <see cref="ObservableJsonAccess.Type"/> access.</remarks>
		public bool IsArrayOrMissing() => IsOfTypeOrNull(JsonType.Array);

		/// <summary>Tests if the wrapped JSON value is a non-null array.</summary>
		/// <param name="value">Receives the underlying JSON array</param>
		/// <returns><c>true</c> if the value is an array; otherwise, <c>false</c></returns>
		/// <remarks>
		/// <para>This method will record a <see cref="ObservableJsonAccess.Type"/> access, but any reads performed on <paramref name="value"/> will not be tracked by the attached context!</para>
		/// <para>It is intended to be used by infrastructure code that will manually record any access to the value.</para>
		/// </remarks>
		public bool IsArrayUnsafe([MaybeNullWhen(false)] out JsonArray value)
		{
			value = this.Json as JsonArray;
			return value is not null;
		}

		/// <summary>Tests if the wrapped JSON value is a non-null object.</summary>
		/// <returns><c>true</c> if the value is an object; otherwise, <c>false</c></returns>
		/// <remarks>This method will record a <see cref="ObservableJsonAccess.Type"/> access.</remarks>
		public bool IsObject() => IsOfType(JsonType.Array);

		/// <summary>Tests if the wrapped JSON value is either an object, or null-or-missing.</summary>
		/// <returns><c>true</c> if the value is an object, null or missing; otherwise, <c>false</c></returns>
		/// <remarks>This method will record a <see cref="ObservableJsonAccess.Type"/> access.</remarks>
		public bool IsObjectOrMissing() => IsOfTypeOrNull(JsonType.Object);

		/// <summary>Tests if the wrapped JSON value is a non-null object.</summary>
		/// <param name="value">Receives the underlying JSON object</param>
		/// <returns><c>true</c> if the value is an object; otherwise, <c>false</c></returns>
		/// <remarks>
		/// <para>This method will record a <see cref="ObservableJsonAccess.Type"/> access, but any reads performed on <paramref name="value"/> will not be tracked by the attached context!</para>
		/// <para>It is intended to be used by infrastructure code that will manually record any access to the value.</para>
		/// </remarks>
		public bool IsObjectUnsafe([MaybeNullWhen(false)] out JsonObject value)
		{
			value = this.Json as JsonObject;
			return value is not null;
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)] 
		public bool IsNullOrMissing() => this.Json is JsonNull;

		public bool Exists() => this.Json is not JsonNull;

		[Pure]
		[return: NotNullIfNotNull(nameof(defaultValue))]
		public TValue? As<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TValue>(TValue? defaultValue = default) => this.Json.As<TValue>(defaultValue);

		public TValue Required<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TValue>() where TValue : notnull
		{
			if (this.Json is null or JsonNull)
			{
				throw CrystalJson.Errors.Parsing_ChildIsNullOrMissing(this, default, null);
			}
			return this.Json.As<TValue>()!;
		}

		public bool ContainsKey(string key) => this.Json is JsonObject obj && obj.ContainsKey(key);

		public bool ContainsKey(ReadOnlyMemory<char> key) => this.Json is JsonObject obj && obj.ContainsKey(key);

		public bool ContainsValue(JsonValue value) => this.Json switch
		{
			JsonObject obj => obj.Contains(value),
			JsonArray arr  => arr.Contains(value),
			_              => false
		};

		public MutableJsonValue this[string key]
		{
			[MustUseReturnValue, MethodImpl(MethodImplOptions.AggressiveInlining)]
			get => Get(key);
		}

		public MutableJsonValue this[JsonPath path]
		{
			[MustUseReturnValue, MethodImpl(MethodImplOptions.AggressiveInlining)]
			get => Get(path);
		}

		public MutableJsonValue this[ReadOnlyMemory<char> key]
		{
			[MustUseReturnValue]
			get => Get(key);
		}

		public MutableJsonValue this[int index]
		{
			[MustUseReturnValue, MethodImpl(MethodImplOptions.AggressiveInlining)]
			get => Get(index);
			//set => Set(index, value.Json);
		}

		public MutableJsonValue this[Index index]
		{
			[MustUseReturnValue, MethodImpl(MethodImplOptions.AggressiveInlining)]
			get => Get(index);
			//set => Set(index, value.Json);
		}

		#region TryGetValue...

		/// <summary>Returns the value of the given field, if it is not null or missing</summary>
		/// <param name="key">Name of the field in this object</param>
		/// <param name="value">Value that represents this field in the current object.</param>
		/// <returns><see langword="true"/> if the element exists and has a non-null value; otherwise, <see langword="false"/>.</returns>
		/// <remarks><para>This can be used to perform a different operation if the value exists or not (initialize a counter or increment its value, throw a specialized exception, ....)</para></remarks>
		/// <example><code>
		/// if (!root.TryGetValue("Error", out var error))
		/// { // this is the first error, will automatically create a new 'Error' object
		///		error["Attempts"] = 1;
		///		error["FirstAttempt"] = DateTimeOffset.UtcNow;
		///		// root will now have { ..., "Error": { "Attempts": 1, "FirstAttempt": "..." } }
		/// }
		/// else
		/// { // there was already an 'Error' object, record the new attempt
		///		error["Attempts"].Increment();
		///		error["LastAttempt"] = DateTimeOffset.UtcNow;
		///		// root will now have { ..., "Error": { "Attempts": (+1), "FirstAttempt": "...", "LastAttempt": "..." } }
		/// }
		/// </code></example>
		[Pure]
		public bool TryGetValue(string key, out MutableJsonValue value)
		{
			var items = this.Json;
			if (!items.TryGetValue(key, out var child))
			{
				value = new(this.Context, this, new(key), JsonNull.Missing);
				return false;
			}

			value = new(this.Context, this, new(key), child);
			return true;
		}

		/// <summary>Returns the value of the given field, if it is not null or missing</summary>
		/// <param name="key">Name of the field in this object</param>
		/// <param name="value">Value that represents this field in the current object.</param>
		/// <returns><see langword="true"/> if the element exists and has a non-null value; otherwise, <see langword="false"/>.</returns>
		/// <remarks><para>This can be used to perform a different operation if the value exists or not (initialize a counter or increment its value, throw a specialized exception, ....)</para></remarks>
		/// <example><code>
		/// if (!foo.TryGetValue&lt;Bar>("bar", out var bar))
		/// { // first time
		///		bar = new Bar { /* ... */ }
		///     foo.Set("bar", bar);
		/// }
		/// </code></example>
		[Pure]
		public bool TryGetValue<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TValue>(string key, [MaybeNullWhen(false)] out TValue value)
		{
			var items = this.Json;
			if (!items.TryGetValue(key, out var child) || child.IsNullOrMissing())
			{
				value = default;
				return false;
			}

			value = child.As<TValue>()!;
			return true;
		}

		/// <summary>Returns the value of the given field, if it is not null or missing</summary>
		/// <param name="key">Name of the field in this object</param>
		/// <param name="converter">Converter used to unpack the JSON value into a <typeparamref name="TValue"/> instance</param>
		/// <param name="value">Value that represents this field in the current object.</param>
		/// <returns><see langword="true"/> if the element exists and has a non-null value; otherwise, <see langword="false"/>.</returns>
		/// <remarks><para>This can be used to perform a different operation if the value exists or not (initialize a counter or increment its value, throw a specialized exception, ....)</para></remarks>
		/// <example><code>
		/// if (!foo.TryGetValue&lt;Bar>("bar", GeneratedConverters.Bar.Default, out var bar))
		/// { // first time
		///		bar = new Bar { /* ... */ }
		///     foo.Set("bar", bar, GeneratedConverters.Bar.Default);
		/// }
		/// </code></example>
		[Pure]
		public bool TryGetValue<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TValue>(string key, IJsonDeserializer<TValue> converter, [MaybeNullWhen(false)] out TValue value)
		{
			var items = this.Json;
			if (!items.TryGetValue(key, out var child) || child.IsNullOrMissing())
			{
				value = default;
				return false;
			}

			value = converter.Unpack(child);
			return true;
		}

		/// <summary>Returns the value of the given field, if it is not null or missing</summary>
		/// <param name="key">Name of the field in this object</param>
		/// <param name="value">Value that represents this field in the current object.</param>
		/// <returns><see langword="true"/> if the element exists and has a non-null value; otherwise, <see langword="false"/>.</returns>
		/// <remarks><para>This can be used to perform a different operation if the value exists or not (initialize a counter or increment its value, throw a specialized exception, ....)</para></remarks>
		/// <example><code>
		/// if (!root.TryGetValue("Error", out var error))
		/// { // this is the first error, will automatically create a new 'Error' object
		///		error["Attempts"] = 1;
		///		error["FirstAttempt"] = DateTimeOffset.UtcNow;
		///		// root will now have { ..., "Error": { "Attempts": 1, "FirstAttempt": "..." } }
		/// }
		/// else
		/// { // there was already an 'Error' object, record the new attempt
		///		error["Attempts"].Increment();
		///		error["LastAttempt"] = DateTimeOffset.UtcNow;
		///		// root will now have { ..., "Error": { "Attempts": (+1), "FirstAttempt": "...", "LastAttempt": "..." } }
		/// }
		/// </code></example>
		[Pure]
		public bool TryGetValue(ReadOnlyMemory<char> key, out MutableJsonValue value)
		{
			var items = this.Json;
			if (!items.TryGetValue(key, out var child))
			{
				value = new(this.Context, this, new(key), JsonNull.Missing);
				return false;
			}

			value = new(this.Context, this, new(key), child);
			return true;
		}

		/// <summary>Returns the value of the given field, if it is not null or missing</summary>
		/// <param name="key">Name of the field in this object</param>
		/// <param name="value">Value that represents this field in the current object.</param>
		/// <returns><see langword="true"/> if the element exists and has a non-null value; otherwise, <see langword="false"/>.</returns>
		/// <remarks><para>This can be used to perform a different operation if the value exists or not (initialize a counter or increment its value, throw a specialized exception, ....)</para></remarks>
		/// <example><code>
		/// if (!foo.TryGetValue&lt;Bar>("bar".AsMemory(), out var bar))
		/// { // first time
		///		bar = new Bar { /* ... */ }
		///     foo.Set("bar", bar);
		/// }
		/// </code></example>
		[Pure]
		public bool TryGetValue<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TValue>(ReadOnlyMemory<char> key, [MaybeNullWhen(false)] out TValue value)
		{
			var items = this.Json;
			if (!items.TryGetValue(key, out var child) || child.IsNullOrMissing())
			{
				value = default;
				return false;
			}

			value = child.As<TValue>()!;
			return true;
		}

		/// <summary>Returns the value of the given field, if it is not null or missing</summary>
		/// <param name="key">Name of the field in this object</param>
		/// <param name="converter">Converter used to unpack the JSON value into a <typeparamref name="TValue"/> instance</param>
		/// <param name="value">Value that represents this field in the current object.</param>
		/// <returns><see langword="true"/> if the element exists and has a non-null value; otherwise, <see langword="false"/>.</returns>
		/// <remarks><para>This can be used to perform a different operation if the value exists or not (initialize a counter or increment its value, throw a specialized exception, ....)</para></remarks>
		/// <example><code>
		/// if (!foo.TryGetValue&lt;Bar>("bar".AsMemory(), GeneratedConverters.Bar.Default, out var bar))
		/// { // first time
		///		bar = new Bar { /* ... */ }
		///     foo.Set("bar", bar, GeneratedConverters.Bar.Default);
		/// }
		/// </code></example>
		[Pure]
		public bool TryGetValue<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TValue>(ReadOnlyMemory<char> key, IJsonDeserializer<TValue> converter, [MaybeNullWhen(false)] out TValue value)
		{
			var items = this.Json;
			if (!items.TryGetValue(key, out var child) || child.IsNullOrMissing())
			{
				value = default;
				return false;
			}

			value = converter.Unpack(child);
			return true;
		}

		/// <summary>Returns the value at the given location, if it was non-null and inside the bounds of the array</summary>
		/// <param name="index">Index of the element in this array</param>
		/// <param name="value">Value that represents this index in the current array.</param>
		/// <returns><see langword="true"/> if the element exists and has a non-null value; otherwise, <see langword="false"/>.</returns>
		/// <remarks><para>This can be used to perform a different operation if the value exists or not (initialize a counter or increment its value, throw a specialized exception, ....)</para></remarks>
		/// <example><code>
		/// if (!root["Foos"].TryGetValue(2, out var item))
		/// { // either Foos[] has size less than 3, or Foos[2] is null.
		///		item["Version"] = 0;
		///		item["Created"] = DateTimeOffset.UtcNow;
		///		// Foos[2] will now be equal to { "Version": 0, "Created": "..." }
		/// }
		/// else
		/// { // Foos[2] already had a non-null value
		///		item["Version"].Increment();
		///		item["LastModified"] = DateTimeOffset.UtcNow;
		///		// Foos[2] will now be equal to { "Version": (+ 1), "Created": "...", "LastModified": "..." }
		/// }
		/// </code></example>
		[Pure]
		public bool TryGetValue(int index, out MutableJsonValue value)
		{
			if (!this.Json.TryGetValue(index, out var child) || child.IsNullOrMissing())
			{
				value = new(this.Context, this, new(index), JsonNull.Error);
				return false;
			}

			value = new(this.Context, this, new(index), child);
			return true;
		}

		/// <summary>Returns the value at the given location, if it was non-null and inside the bounds of the array</summary>
		/// <param name="index">Index of the element in this array</param>
		/// <param name="value">Value that represents this index in the current array, converted into <typeparamref name="TValue"/>.</param>
		/// <returns><see langword="true"/> if the element exists and has a non-null value; otherwise, <see langword="false"/>.</returns>
		/// <remarks><para>This can be used to perform a different operation if the value exists or not (initialize a counter or increment its value, throw a specialized exception, ....)</para></remarks>
		/// <example><code>
		/// if (root["Foos"].TryGetValue(1, out var foo))
		/// {
		///     ProcessFoo(foo);
		/// }
		/// // ...
		/// static void ProcessFoo(Foo instance) { /* ... */ }
		/// </code></example>
		[Pure]
		public bool TryGetValue<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TValue>(int index, [MaybeNullWhen(false)] out TValue value)
		{
			var items = this.Json;
			if (!items.TryGetValue(index, out var child) || child.IsNullOrMissing())
			{
				value = default;
				return false;
			}

			value = child.As<TValue>()!;
			return true;
		}

		/// <summary>Returns the value at the given location, if it was non-null and inside the bounds of the array</summary>
		/// <param name="index">Index of the element in this array</param>
		/// <param name="converter">Converter used to unpack the JSON value into a <typeparamref name="TValue"/> instance</param>
		/// <param name="value">Value that represents this index in the current array, converted into <typeparamref name="TValue"/>.</param>
		/// <returns><see langword="true"/> if the element exists and has a non-null value; otherwise, <see langword="false"/>.</returns>
		/// <remarks><para>This can be used to perform a different operation if the value exists or not (initialize a counter or increment its value, throw a specialized exception, ....)</para></remarks>
		/// <example><code>
		/// if (root["Foos"].TryGetValue(1, GeneratedConverters.Foo.Default, out var foo))
		/// {
		///     ProcessFoo(foo);
		/// }
		/// // ...
		/// static void ProcessFoo(Foo instance) { /* ... */ }
		/// </code></example>
		[Pure]
		public bool TryGetValue<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TValue>(int index, IJsonDeserializer<TValue> converter, [MaybeNullWhen(false)] out TValue value)
		{
			var items = this.Json;
			if (!items.TryGetValue(index, out var child) || child.IsNullOrMissing())
			{
				value = default;
				return false;
			}

			value = converter.Unpack(child);
			return true;
		}

		/// <summary>Returns the value at the given location, if it was non-null and inside the bounds of the array</summary>
		/// <param name="index">Index of the element in this array</param>
		/// <param name="value">Value that represents this index in the current array.</param>
		/// <returns><see langword="true"/> if the element exists and has a non-null value; otherwise, <see langword="false"/>.</returns>
		/// <remarks><para>This can be used to perform a different operation if the value exists or not (initialize a counter or increment its value, throw a specialized exception, ....)</para></remarks>
		/// <example><code>
		/// if (!root["Foos"].TryGetValue(2, out var item))
		/// { // either Foos[] has size less than 3, or Foos[2] is null.
		///		item["Version"] = 0;
		///		item["Created"] = DateTimeOffset.UtcNow;
		///		// Foos[2] will now be equal to { "Version": 0, "Created": "..." }
		/// }
		/// else
		/// { // Foos[2] already had a non-null value
		///		item["Version"].Increment();
		///		item["LastModified"] = DateTimeOffset.UtcNow;
		///		// Foos[2] will now be equal to { "Version": (+ 1), "Created": "...", "LastModified": "..." }
		/// }
		/// </code></example>
		[Pure]
		public bool TryGetValue(Index index, out MutableJsonValue value)
		{
			if (!this.Json.TryGetValue(index, out var child) || child.IsNullOrMissing())
			{
				value = new(this.Context, this, new(index), JsonNull.Error);
				return false;
			}

			value = new(this.Context, this, new(index), child);
			return true;
		}

		/// <summary>Returns the value at the given location, if it was non-null and inside the bounds of the array</summary>
		/// <param name="index">Index of the element in this array</param>
		/// <param name="value">Value that represents this index in the current array, converted into <typeparamref name="TValue"/>.</param>
		/// <returns><see langword="true"/> if the element exists and has a non-null value; otherwise, <see langword="false"/>.</returns>
		/// <remarks><para>This can be used to perform a different operation if the value exists or not (initialize a counter or increment its value, throw a specialized exception, ....)</para></remarks>
		/// <example><code>
		/// if (root["Foos"].TryGetValue(1, out var foo))
		/// {
		///     ProcessFoo(foo);
		/// }
		/// // ...
		/// static void ProcessFoo(Foo instance) { /* ... */ }
		/// </code></example>
		[Pure]
		public bool TryGetValue<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TValue>(Index index, [MaybeNullWhen(false)] out TValue value)
		{
			var items = this.Json;
			if (!items.TryGetValue(index, out var child) || child.IsNullOrMissing())
			{
				value = default;
				return false;
			}

			value = child.As<TValue>()!;
			return true;
		}

		/// <summary>Returns the value at the given location, if it was non-null and inside the bounds of the array</summary>
		/// <param name="index">Index of the element in this array</param>
		/// <param name="converter">Converter used to unpack the JSON value into a <typeparamref name="TValue"/> instance</param>
		/// <param name="value">Value that represents this index in the current array, converted into <typeparamref name="TValue"/>.</param>
		/// <returns><see langword="true"/> if the element exists and has a non-null value; otherwise, <see langword="false"/>.</returns>
		/// <remarks><para>This can be used to perform a different operation if the value exists or not (initialize a counter or increment its value, throw a specialized exception, ....)</para></remarks>
		/// <example><code>
		/// if (root["Foos"].TryGetValue(1, GeneratedConverters.Foo.Default, out var foo))
		/// {
		///     ProcessFoo(foo);
		/// }
		/// // ...
		/// static void ProcessFoo(Foo instance) { /* ... */ }
		/// </code></example>
		[Pure]
		public bool TryGetValue<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TValue>(Index index, IJsonDeserializer<TValue> converter, [MaybeNullWhen(false)] out TValue value)
		{
			var items = this.Json;
			if (!items.TryGetValue(index, out var child) || child.IsNullOrMissing())
			{
				value = default;
				return false;
			}

			value = converter.Unpack(child);
			return true;
		}

		#endregion

		#region Get...

		#region Get(string)...

		[Pure, MustUseReturnValue]
		public MutableJsonValue Get(string name) => new(this.Context, this, new(name), this.Json.GetValueOrDefault(name));

		[Pure, MustUseReturnValue]
		public JsonValue GetValue(string key) => this.Json.GetValueOrDefault(key);

		[Pure, MustUseReturnValue]
		public TValue Get<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TValue>(string name) where TValue : notnull
		{
			var value = this.Json.GetValueOrDefault(name);
			if (value.IsNullOrMissing())
			{
				throw CrystalJson.Errors.Parsing_ChildIsNullOrMissing(this, new(name), null);
			}
			return value.As<TValue>()!;
		}

		[Pure, MustUseReturnValue]
		[return: NotNullIfNotNull(nameof(defaultValue))]
		public TValue? Get<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TValue>(string name, TValue defaultValue) => this.Json.GetValueOrDefault(name).As<TValue>(defaultValue);

		#endregion

		#region Get(ReadOnlyMemory<char>)...

		[Pure, MustUseReturnValue]
		public MutableJsonValue Get(ReadOnlyMemory<char> name)
		{
#if NET9_0_OR_GREATER
			var value = this.Json.GetValueOrDefault(name, JsonNull.Missing, out var actualKey);
			return new(this.Context, this, new(actualKey?.AsMemory() ?? name), value);
#else
			var value = this.Json.GetValueOrDefault(name, JsonNull.Missing);
			return new(this.Context, this, new(name), value);
#endif
		}

		[Pure, MustUseReturnValue]
		public JsonValue GetValue(ReadOnlyMemory<char> name) => this.Json.GetValueOrDefault(name);

		[Pure, MustUseReturnValue]
		public TValue Get<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TValue>(ReadOnlyMemory<char> name) where TValue : notnull
		{
			var value = this.Json.GetValueOrDefault(name);
			if (value.IsNullOrMissing())
			{
				throw CrystalJson.Errors.Parsing_ChildIsNullOrMissing(this, new(name), null);
			}
			return value.As<TValue>()!;
		}

		[Pure, MustUseReturnValue]
		[return: NotNullIfNotNull(nameof(defaultValue))]
		public TValue? Get<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TValue>(ReadOnlyMemory<char> name, TValue defaultValue) => this.Json.GetValueOrDefault(name).As<TValue>(defaultValue);

		#endregion

		#region Get(int)...

		[Pure, MustUseReturnValue]
		public MutableJsonValue Get(int index) => new(this.Context, this, new(index), this.Json.GetValueOrDefault(index));

		[Pure, MustUseReturnValue]
		public JsonValue GetValue(int index) => this.Json.GetValueOrDefault(index);

		[Pure, MustUseReturnValue]
		public TValue Get<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TValue>(int index) where TValue : notnull
		{
			var value = this.Json.GetValueOrDefault(index);
			if (value.IsNullOrMissing())
			{
				throw CrystalJson.Errors.Parsing_ChildIsNullOrMissing(this, new(index), null);
			}
			return value.As<TValue>()!;
		}

		[Pure, MustUseReturnValue]
		[return: NotNullIfNotNull(nameof(defaultValue))]
		public TValue? Get<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TValue>(int index, TValue defaultValue) => this.Json.GetValueOrDefault(index).As<TValue>(defaultValue);

		#endregion

		#region Get(Index)...

		[Pure, MustUseReturnValue]
		public MutableJsonValue Get(Index index) => new(this.Context, this, new(index), this.Json.GetValueOrDefault(index));

		[Pure, MustUseReturnValue]
		public JsonValue GetValue(Index index) => this.Json.GetValueOrDefault(index);

		[Pure, MustUseReturnValue]
		public TValue Get<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TValue>(Index index) where TValue : notnull
		{
			var value = this.Json.GetValueOrDefault(index);
			if (value.IsNullOrMissing())
			{
				throw CrystalJson.Errors.Parsing_ChildIsNullOrMissing(this, new(index), null);
			}
			return value.As<TValue>()!;
		}

		[Pure, MustUseReturnValue]
		[return: NotNullIfNotNull(nameof(defaultValue))]
		public TValue? Get<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TValue>(Index index, TValue defaultValue) => this.Json.Get<TValue>(index, defaultValue);

		#endregion

		#region Get(JsonPath)...

		[Pure, MustUseReturnValue]
		public MutableJsonValue Get(JsonPath path)
		{
			var current = this;
			foreach (var segment in path)
			{
				current = current.Get(segment);
			}
			return current;
		}

		[Pure, MustUseReturnValue]
		public JsonValue GetValue(JsonPath path) => this.Json.GetPathValueOrDefault(path);

		[Pure, MustUseReturnValue]
		public TValue Get<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TValue>(JsonPath path) where TValue : notnull
		{
			var value = this.Json.GetPathValueOrDefault(path);
			if (value.IsNullOrMissing())
			{
				throw CrystalJson.Errors.Parsing_DescendantIsNullOrMissing(this, path, null);
			}
			return value.As<TValue>()!;
		}

		[Pure, MustUseReturnValue]
		[return: NotNullIfNotNull(nameof(defaultValue))]
		public TValue? Get<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TValue>(JsonPath path, TValue defaultValue) => this.Json.GetPathValueOrDefault(path).As<TValue>(defaultValue);

		#endregion

		#region Get(JsonPathSegment)...

		[Pure, MustUseReturnValue]
		public MutableJsonValue Get(JsonPathSegment segment)
			=> segment.TryGetName(out var name) ? Get(name)
			 : segment.TryGetIndex(out var idx) ? Get(idx)
			 : this;

		[Pure, MustUseReturnValue]
		public JsonValue GetValue(JsonPathSegment segment)
			=> segment.TryGetName(out var name) ? GetValue(name)
			 : segment.TryGetIndex(out var idx) ? GetValue(idx)
			 : this.Json;

		[Pure, MustUseReturnValue]
		public TValue Get<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TValue>(JsonPathSegment segment) where TValue : notnull
		{
			var value = GetValue(segment);
			if (value.IsNullOrMissing())
			{
				throw CrystalJson.Errors.Parsing_ChildIsNullOrMissing(this, segment, null);
			}
			return value.As<TValue>()!;
		}

		[Pure, MustUseReturnValue]
		[return: NotNullIfNotNull(nameof(defaultValue))]
		public TValue? Get<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TValue>(JsonPathSegment segment, TValue defaultValue) => GetValue(segment).As<TValue>(defaultValue);

		#endregion

		[MustUseReturnValue]
		public MutableJsonValue GetOrCreateObject(string path)
		{
			var child = this.Get(path);
			if (child.IsNullOrMissing())
			{
				child.Set(JsonObject.ReadOnly.Empty);
			}
			return child;
		}

		[MustUseReturnValue]
		public MutableJsonValue GetOrCreateArray(string path)
		{
			var child = this.Get(path);
			if (child.IsNullOrMissing())
			{
				child.Set(JsonArray.ReadOnly.Empty);
			}
			return child;
		}

		#endregion

		#region ValueEquals...

		[RequiresUnreferencedCode(AotMessages.TypeMightBeRemoved)]
		public bool ValueEquals<TValue>(TValue value, IEqualityComparer<TValue>? comparer = null) => this.Json.ValueEquals(value, comparer);

		[RequiresUnreferencedCode(AotMessages.TypeMightBeRemoved)]
		public bool ValueEquals<TValue>(string name, TValue value, IEqualityComparer<TValue>? comparer = null) => this.GetValue(name).ValueEquals(value, comparer);

		[RequiresUnreferencedCode(AotMessages.TypeMightBeRemoved)]
		public bool ValueEquals<TValue>(ReadOnlyMemory<char> name, TValue value, IEqualityComparer<TValue>? comparer = null) => this.GetValue(name).ValueEquals(value, comparer);

		[RequiresUnreferencedCode(AotMessages.TypeMightBeRemoved)]
		public bool ValueEquals<TValue>(int index, TValue value, IEqualityComparer<TValue>? comparer = null) => this.GetValue(index).ValueEquals(value, comparer);

		[RequiresUnreferencedCode(AotMessages.TypeMightBeRemoved)]
		public bool ValueEquals<TValue>(Index index, TValue value, IEqualityComparer<TValue>? comparer = null) => this.GetValue(index).ValueEquals(value, comparer);

		[RequiresUnreferencedCode(AotMessages.TypeMightBeRemoved)]
		public bool ValueEquals<TValue>(JsonPathSegment segment, TValue value, IEqualityComparer<TValue>? comparer = null) => this.GetValue(segment).ValueEquals(value, comparer);

		[RequiresUnreferencedCode(AotMessages.TypeMightBeRemoved)]
		public bool ValueEquals<TValue>(JsonPath path, TValue value, IEqualityComparer<TValue>? comparer = null) => this.GetValue(path).ValueEquals(value, comparer);

		#endregion

		public void ApplyPatch(JsonObject patch, bool deepCopy = false)
		{
			Contract.NotNull(patch);

			if (patch.ContainsKey("__patch"))
			{ // we are patching an array
				ApplyPatchToArray(patch, deepCopy);
			}
			else
			{
				ApplyPatchToObject(patch, deepCopy);
			}
		}

		private void ApplyPatchToObject(JsonObject patch, bool deepCopy)
		{
			RealizeObjectIfRequired();

			foreach (var kv in patch)
			{
				switch (kv.Value)
				{
					case JsonObject b:
					{ // merge two objects together
						this[kv.Key].ApplyPatch(b, deepCopy);
						break;
					}
					case JsonNull:
					{ // remove value (or set to null)
						Remove(kv.Key);
						break;
					}
					default:
					{ // overwrite previous value
						Set(kv.Key, deepCopy ? kv.Value.Copy() : kv.Value);
						break;
					}
				}
			}
		}

		private void ApplyPatchToArray(JsonObject patch, bool deepCopy)
		{
			int newSize = patch.Get<int?>("__patch", null) ?? throw new ArgumentException("Object is not a valid patch for an array: required '__patch' field is missing");
			if (newSize == 0)
			{ // clear all items
				Clear();
				return;
			}

			RealizeArrayIfRequired();

			// patch all changed items
			foreach (var kv in patch)
			{
				if (kv.Key == "__patch") continue;
				if (!int.TryParse(kv.Key, out var idx)) throw new ArgumentException($"Object is not a valid patch for an array: unexpected '{kv.Key}' field");
				if (idx >= newSize) throw new ArgumentException($"Object is not a valid patch for an array: key '{kv.Key}' is greater than the final size");

				switch (kv.Value)
				{
					case JsonObject subPatch:
					{
						this[idx].ApplyPatch(subPatch, deepCopy);
						break;
					}
					case (JsonNull):
					{
						RemoveAt(idx);
						break;
					}
					default:
					{
						Set(idx, deepCopy ? kv.Value.Copy() : kv.Value);
						break;
					}
				}
			}
		}

		/// <summary>Returns the list of keys on this object</summary>
		/// <exception cref="InvalidOperationException">If the wrapped JSON value is neither null nor an object</exception>
		public ICollection<string> Keys
		{
			get
			{
				if (this.Json is not JsonObject obj)
				{
					if (this.Json is JsonNull)
					{
						return Array.Empty<string>();
					}
					throw new InvalidOperationException("Cannot iterate keys on a non-array");
				}
				return obj.Keys;
			}
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private void NotifyParent(MutableJsonValue child)
		{
			this.Parent?.NotifyChildChanged(child, this.Segment);
		}

		private static void InsertInParent(MutableJsonValue parent, in JsonPathSegment segment, MutableJsonValue value)
		{
			Contract.Debug.Assert(parent != null);
			if (segment.TryGetName(out var name))
			{
				parent.Set(name, value.Json);
			}
			else if (segment.TryGetIndex(out var index))
			{
				parent.Set(index, value.Json);
			}
			else
			{
				throw new InvalidOperationException();
			}
		}

		private JsonObject RealizeObjectIfRequired()
		{
			var json = this.Json;
			if (json.IsNullOrMissing())
			{ // the parent should have been an object

				json = this.Context?.NewObject() ?? new JsonObject();
				this.Json = json;

				var res = new MutableJsonValue(this.Context, this.Parent, this.Segment, json);
				if (this.Parent != null)
				{
					InsertInParent(this.Parent, in this.Segment, res);
				}
			}
			if (json is not JsonObject obj)
			{
				throw new InvalidOperationException($"Cannot add a field to a JSON {json.Type}");
			}
			return obj;
		}

		private JsonArray RealizeArrayIfRequired()
		{
			var json = this.Json;
			if (json.IsNullOrMissing())
			{ // the parent should have been an object

				json = this.Context?.NewArray() ?? new JsonArray();
				this.Json = json;

				var res = new MutableJsonValue(this.Context, this.Parent, this.Segment, json);
				if (this.Parent != null)
				{
					InsertInParent(this.Parent, in this.Segment, res);
				}
			}
			if (json is not JsonArray arr)
			{
				throw new InvalidOperationException($"Cannot add a field to a JSON {json.Type}");
			}
			return arr;
		}

		internal void NotifyChildChanged(MutableJsonValue child, JsonPathSegment segment)
		{
			JsonValue newJson;
			if (segment.TryGetName(out var name))
			{
				var prevObj = RealizeObjectIfRequired();
				if (prevObj.IsReadOnly)
				{ // first mutation
					prevObj = JsonObject.Copy(prevObj, deep: false, readOnly: false);
					prevObj[name] = child.Json;
					newJson = prevObj;
				}
				else
				{ // additional mutation
					prevObj[name] = child.Json;
					return;
				}
			}
			else if (segment.TryGetIndex(out var index))
			{
				var prevArr = RealizeArrayIfRequired();
				if (prevArr.IsReadOnly)
				{
					prevArr = JsonArray.Copy(prevArr, deep: false, readOnly: false);
					prevArr[index] = child.Json;
					newJson = prevArr;
				}
				else
				{ // additional mutation
					prevArr[index] = child.Json;
					return;
				}
			}
			else
			{
				throw new NotSupportedException();
			}

			this.Json = newJson;
			this.NotifyParent(this);
		}

		#region InsertOrUpdate...

		internal enum InsertionBehavior : byte
		{
			None,
			OverwriteExisting,
			ThrowOnExisting,
		}

		/// <summary>Changes the value of the current instance in its parent</summary>
		internal bool InsertOrUpdate(JsonValue? value, InsertionBehavior behavior = InsertionBehavior.OverwriteExisting)
		{
			// validate the value
			value ??= JsonNull.Missing;

			if (this.Parent == null) throw new InvalidOperationException("Cannot replace the top level value");

			var prevJson = this.Json;
			if (!prevJson.IsNullOrMissing())
			{
				switch (behavior)
				{
					case InsertionBehavior.None:
					{
						return false;
					}
					case InsertionBehavior.ThrowOnExisting:
					{
						if (!this.Segment.IsIndex())
						{
							throw new ArgumentException("A field with this same name already exists in this object");
						}
						break;
					}
					default:
					{
						if (prevJson.StrictEquals(value))
						{ // idempotent
							return true;
						}
						break;
					}
				}
			}

			//TODO: if the new value is a JsonObject that is somewhat complex,
			// it could better to compare it to the previous, and only update the actual things that changed ?
			// for ex, sometimes only one (or two) subtrees are changed
			// => HOW DO WE CHEAPLY COMPUTE THE COST OF THE CHECK VS THE REWARDS?

			if (this.Context == null)
			{
				if (prevJson is JsonNull && value is JsonNull)
				{
					return true;
				}
				this.Json = value;
				this.NotifyParent(this);
				return true;
			}
			else
			{
				JsonValue? patch;
				switch (prevJson, value)
				{
					case (JsonObject before, JsonObject after):
					{ // Object patch
						patch = before.ComputePatch(after);
						break;
					}
					case (JsonArray before, JsonArray after):
					{ // Array patch
						patch = before.ComputePatch(after);
						break;
					}
					case (JsonNull, JsonNull):
					{
						return true;
					}
					default:
					{ // overwrite
						patch = null;
						break;
					}
				}

				this.Json = value;
				this.NotifyParent(this);

				if (this.Segment.TryGetName(out var name))
				{
					if (patch == null)
					{
						this.Context.RecordUpdate(this.Parent, name, value);
					}
					else
					{
						this.Context.RecordPatch(this.Parent, name, patch);
					}
				}
				else if (this.Segment.TryGetIndex(out var idx))
				{
					// the pattern "[^0]" is used to append a new item to an array
					// -> since the parent now contains the appended item, its Count is already incremented, so we have to replace it with "^1" instead!
					if (idx.Equals(^0))
					{
						idx = ^1;
					}

					var index = idx.GetOffset(this.Parent.Count);
					if (patch == null)
					{
						this.Context.RecordUpdate(this.Parent, index, value);
					}
					else
					{
						this.Context.RecordPatch(this.Parent, index, patch);
					}
				}
				return true;
			}
		}

		internal bool InsertOrUpdate(ReadOnlyMemory<char> name, JsonValue? value, InsertionBehavior behavior = InsertionBehavior.OverwriteExisting)
		{
			// validate the value
			value ??= JsonNull.Missing;

			var selfJson = RealizeObjectIfRequired();

			var prevJson = selfJson.GetValueOrDefault(name);
			if (!prevJson.IsNullOrMissing())
			{
				switch (behavior)
				{
					case InsertionBehavior.None:
					{
						return false;
					}
					case InsertionBehavior.ThrowOnExisting:
					{
						throw new ArgumentException("A field with the same name already exists in the object.");
					}
					default:
					{
						if (prevJson.StrictEquals(value))
						{ // idempotent
							return true;
						}
						break;
					}
				}
			}
			else
			{
				if (value is JsonNull)
				{ // idempotent
					return true;
				}
			}

			JsonValue? patch;
			switch (prevJson, value)
			{
				case (JsonObject before, JsonObject after):
				{ // Object patch
					patch = this.Context != null ? before.ComputePatch(after) : null;
					break;
				}
				case (JsonArray before, JsonArray after):
				{ // Array patch
					patch = this.Context != null ? before.ComputePatch(after) : null;
					break;
				}
				case (_, JsonNull):
				{ // delete
					this.Context?.RecordDelete(this, name);
					return true;
				}
				default:
				{ // overwrite
					patch = null;
					break;
				}
			}

			JsonObject newJson;
			if (selfJson.IsReadOnly)
			{ // first mutation, replace the object with a mutable version
				newJson = JsonObject.Copy(selfJson, deep: false, readOnly: false);
			}
			else
			{ // additional mutation, we can update in-place
				newJson = selfJson;
			}
			newJson[name] = value;

			// notify the parent chain if we changed the current json instance
			if (!ReferenceEquals(this.Json, newJson))
			{
				this.Json = newJson;
				this.NotifyParent(this);
			}

			if (this.Context != null)
			{
				if (patch == null)
				{
					this.Context.RecordUpdate(this, name, value);
				}
				else
				{
					this.Context.RecordPatch(this, name, patch);
				}
			}
			return true;
		}

		private JsonValue Convert<T>(T? value) => JsonValue.ReadOnly.FromValue(value); //TODO: pass the parent settings?

		private JsonValue Convert<T>(T? value, IJsonPacker<T> converter) => value is not null ? converter.Pack(value, CrystalJsonSettings.JsonReadOnly) : JsonNull.Null;

		#endregion

		#region Set(value)

		/// <summary>Changes the value of the current instance in its parent</summary>
		/// <param name="value">New value for this element</param>
		/// <remarks>
		/// <para>If the current element is a field of an object, the field will either be added or updated in the parent object.</para>
		/// <para>If the current element is an index in an array, the entry will be set to the new value. If the array was smaller, it will automatically be expanded, and any gaps will be filled with null values.</para>
		/// </remarks>
		public void Set(JsonValue? value) => InsertOrUpdate(value);

		/// <summary>Changes the value of the current instance in its parent</summary>
		/// <param name="value">New value for this element</param>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void Set(JsonObject? value) => InsertOrUpdate(value);

		/// <summary>Changes the value of the current instance in its parent</summary>
		/// <param name="value">New value for this element</param>
		public void Set(JsonArray? value) => InsertOrUpdate(value);

		/// <summary>Changes the value of the current instance in its parent</summary>
		/// <param name="value">New value for this element</param>
		public void Set(MutableJsonValue? value) => InsertOrUpdate(value?.Json);

		/// <summary>Changes the value of the current instance in its parent</summary>
		/// <param name="value">New value for this element</param>
		public void Set<TValue>(TValue? value) => InsertOrUpdate(Convert<TValue>(value));

		/// <summary>Changes the value of the current instance in its parent</summary>
		/// <param name="value">New value for this element</param>
		/// <param name="converter">Converter used to unpack the JSON value into a <typeparamref name="TValue"/> instance</param>
		public void Set<TValue>(TValue? value, IJsonPacker<TValue> converter) => InsertOrUpdate(Convert<TValue>(value, converter));

		#region Primitive Types...

		/// <summary>Changes the value of the current instance in its parent</summary>
		public void Set(bool value) => InsertOrUpdate(value ? JsonBoolean.True : JsonBoolean.False);

		/// <summary>Changes the value of the current instance in its parent</summary>
		public void Set(int value) => InsertOrUpdate(JsonNumber.Return(value));

		/// <summary>Changes the value of the current instance in its parent</summary>
		public void Set(uint value) => InsertOrUpdate(JsonNumber.Return(value));

		/// <summary>Changes the value of the current instance in its parent</summary>
		public void Set(long value) => InsertOrUpdate(JsonNumber.Return(value));

		/// <summary>Changes the value of the current instance in its parent</summary>
		public void Set(ulong value) => InsertOrUpdate(JsonNumber.Return(value));

		/// <summary>Changes the value of the current instance in its parent</summary>
		public void Set(float value) => InsertOrUpdate(JsonNumber.Return(value));

		/// <summary>Changes the value of the current instance in its parent</summary>
		public void Set(double value) => InsertOrUpdate(JsonNumber.Return(value));

		/// <summary>Changes the value of the current instance in its parent</summary>
		public void Set(decimal value) => InsertOrUpdate(JsonNumber.Return(value));

#if NET8_0_OR_GREATER

		/// <summary>Changes the value of the current instance in its parent</summary>
		public void Set(Half value) => InsertOrUpdate(JsonNumber.Return(value));

		/// <summary>Changes the value of the current instance in its parent</summary>
		public void Set(Int128 value) => InsertOrUpdate(JsonNumber.Return(value));

		/// <summary>Changes the value of the current instance in its parent</summary>
		public void Set(UInt128 value) => InsertOrUpdate(JsonNumber.Return(value));

#endif

		/// <summary>Changes the value of the current instance in its parent</summary>
		public void Set(string? value) => InsertOrUpdate(JsonString.Return(value));

		/// <summary>Changes the value of the current instance in its parent</summary>
		public void Set(DateTime value) => InsertOrUpdate(JsonDateTime.Return(value));

		/// <summary>Changes the value of the current instance in its parent</summary>
		public void Set(DateTimeOffset value) => InsertOrUpdate(JsonDateTime.Return(value));

		/// <summary>Changes the value of the current instance in its parent</summary>
		public void Set(NodaTime.Instant value) => InsertOrUpdate(JsonDateTime.Return(value));

		/// <summary>Changes the value of the current instance in its parent</summary>
		public void Set(DateOnly value) => InsertOrUpdate(JsonDateTime.Return(value));

		/// <summary>Changes the value of the current instance in its parent</summary>
		public void Set(TimeSpan value) => InsertOrUpdate(JsonNumber.Return(value));

		/// <summary>Changes the value of the current instance in its parent</summary>
		public void Set(NodaTime.Duration value) => InsertOrUpdate(JsonNumber.Return(value));

		/// <summary>Changes the value of the current instance in its parent</summary>
		public void Set(TimeOnly value) => InsertOrUpdate(JsonNumber.Return(value));

		#endregion

		#endregion

		#region Set(string, ...)

		public void Set(string name, JsonValue? value) => InsertOrUpdate(name.AsMemory(), value);

		public void Set(string name, JsonObject? value) => InsertOrUpdate(name.AsMemory(), value);

		public void Set(string name, JsonArray? value) => InsertOrUpdate(name.AsMemory(), value);

		public void Set(string name, MutableJsonValue? value) => InsertOrUpdate(name.AsMemory(), value?.Json);

		public void Set<TValue>(string name, TValue? value) => InsertOrUpdate(name.AsMemory(), Convert<TValue>(value));

		public void Set<TValue>(string name, TValue? value, IJsonPacker<TValue> converter) => InsertOrUpdate(name.AsMemory(), Convert<TValue>(value, converter));

		#region Primitive Types...

		public void Set(string name, bool value) => InsertOrUpdate(name.AsMemory(), value ? JsonBoolean.True : JsonBoolean.False);

		public void Set(string name, int value) => InsertOrUpdate(name.AsMemory(), JsonNumber.Return(value));

		public void Set(string name, uint value) => InsertOrUpdate(name.AsMemory(), JsonNumber.Return(value));

		public void Set(string name, long value) => InsertOrUpdate(name.AsMemory(), JsonNumber.Return(value));

		public void Set(string name, ulong value) => InsertOrUpdate(name.AsMemory(), JsonNumber.Return(value));

		public void Set(string name, float value) => InsertOrUpdate(name.AsMemory(), JsonNumber.Return(value));

		public void Set(string name, double value) => InsertOrUpdate(name.AsMemory(), JsonNumber.Return(value));

		public void Set(string name, decimal value) => InsertOrUpdate(name.AsMemory(), JsonNumber.Return(value));

#if NET8_0_OR_GREATER

		public void Set(string name, Half value) => InsertOrUpdate(name.AsMemory(), JsonNumber.Return(value));

		public void Set(string name, Int128 value) => InsertOrUpdate(name.AsMemory(), JsonNumber.Return(value));

		public void Set(string name, UInt128 value) => InsertOrUpdate(name.AsMemory(), JsonNumber.Return(value));

#endif

		public void Set(string name, string? value) => InsertOrUpdate(name.AsMemory(), JsonString.Return(value));

		public void Set(string name, DateTime value) => InsertOrUpdate(name.AsMemory(), JsonDateTime.Return(value));

		public void Set(string name, DateTimeOffset value) => InsertOrUpdate(name.AsMemory(), JsonDateTime.Return(value));

		public void Set(string name, NodaTime.Instant value) => InsertOrUpdate(name.AsMemory(), JsonDateTime.Return(value));

		public void Set(string name, DateOnly value) => InsertOrUpdate(name.AsMemory(), JsonDateTime.Return(value));

		public void Set(string name, TimeSpan value) => InsertOrUpdate(name.AsMemory(), JsonNumber.Return(value));

		public void Set(string name, NodaTime.Duration value) => InsertOrUpdate(name.AsMemory(), JsonNumber.Return(value));

		public void Set(string name, TimeOnly value) => InsertOrUpdate(name.AsMemory(), JsonNumber.Return(value));

		#endregion

		#endregion

		#region Set(ReadOnlyMemory<char>, ...)

		public void Set(ReadOnlyMemory<char> name, JsonValue? value) => InsertOrUpdate(name, value);

		public void Set(ReadOnlyMemory<char> name, JsonObject? value) => InsertOrUpdate(name, value);

		public void Set(ReadOnlyMemory<char> name, JsonArray? value) => InsertOrUpdate(name, value);

		public void Set(ReadOnlyMemory<char> name, MutableJsonValue? value) => InsertOrUpdate(name, value?.Json);

		public void Set<TValue>(ReadOnlyMemory<char> name, TValue? value) => InsertOrUpdate(name, Convert<TValue>(value));

		public void Set<TValue>(ReadOnlyMemory<char> name, TValue? value, IJsonPacker<TValue> converter) => InsertOrUpdate(name, Convert<TValue>(value, converter));

		#region Primitive Types...

		public void Set(ReadOnlyMemory<char> name, bool value) => InsertOrUpdate(name, value ? JsonBoolean.True : JsonBoolean.False);

		public void Set(ReadOnlyMemory<char> name, int value) => InsertOrUpdate(name, JsonNumber.Return(value));

		public void Set(ReadOnlyMemory<char> name, uint value) => InsertOrUpdate(name, JsonNumber.Return(value));

		public void Set(ReadOnlyMemory<char> name, long value) => InsertOrUpdate(name, JsonNumber.Return(value));

		public void Set(ReadOnlyMemory<char> name, ulong value) => InsertOrUpdate(name, JsonNumber.Return(value));

		public void Set(ReadOnlyMemory<char> name, float value) => InsertOrUpdate(name, JsonNumber.Return(value));

		public void Set(ReadOnlyMemory<char> name, double value) => InsertOrUpdate(name, JsonNumber.Return(value));

		public void Set(ReadOnlyMemory<char> name, decimal value) => InsertOrUpdate(name, JsonNumber.Return(value));

#if NET8_0_OR_GREATER

		public void Set(ReadOnlyMemory<char> name, Half value) => InsertOrUpdate(name, JsonNumber.Return(value));

		public void Set(ReadOnlyMemory<char> name, Int128 value) => InsertOrUpdate(name, JsonNumber.Return(value));

		public void Set(ReadOnlyMemory<char> name, UInt128 value) => InsertOrUpdate(name, JsonNumber.Return(value));

#endif

		public void Set(ReadOnlyMemory<char> name, string? value) => InsertOrUpdate(name, JsonString.Return(value));

		public void Set(ReadOnlyMemory<char> name, DateTime value) => InsertOrUpdate(name, JsonDateTime.Return(value));

		public void Set(ReadOnlyMemory<char> name, DateTimeOffset value) => InsertOrUpdate(name, JsonDateTime.Return(value));

		public void Set(ReadOnlyMemory<char> name, NodaTime.Instant value) => InsertOrUpdate(name, JsonDateTime.Return(value));

		public void Set(ReadOnlyMemory<char> name, DateOnly value) => InsertOrUpdate(name, JsonDateTime.Return(value));

		public void Set(ReadOnlyMemory<char> name, TimeSpan value) => InsertOrUpdate(name, JsonNumber.Return(value));

		public void Set(ReadOnlyMemory<char> name, NodaTime.Duration value) => InsertOrUpdate(name, JsonNumber.Return(value));

		public void Set(ReadOnlyMemory<char> name, TimeOnly value) => InsertOrUpdate(name, JsonNumber.Return(value));

		#endregion

		#endregion

		#region Set(path, value)

		public void Set(JsonPath path, JsonValue? value) => Get(path).Set(value);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void Set(JsonPath path, JsonNull? value) => Set(path, (JsonValue?) value);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void Set(JsonPath path, JsonBoolean? value) => Set(path, (JsonValue?) value);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void Set(JsonPath path, JsonNumber? value) => Set(path, (JsonValue?) value);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void Set(JsonPath path, JsonString? value) => Set(path, (JsonValue?) value);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void Set(JsonPath path, JsonDateTime? value) => Set(path, (JsonValue?) value);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void Set(JsonPath path, JsonObject? value) => Set(path, (JsonValue?) value);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void Set(JsonPath path, JsonArray? value) => Set(path, (JsonValue?) value);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void Set(JsonPath path, MutableJsonValue? value) => Set(path, value?.Json);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void Set<TValue>(JsonPath path, TValue? value) => Set(path, Convert<TValue>(value)); //TODO: pass the parent settings?

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void Set<TValue>(JsonPath path, TValue? value, IJsonPacker<TValue> converter) => Set(path, Convert<TValue>(value, converter)); //TODO: pass the parent settings?

		#endregion

		#region Set(index, value)

		internal bool SetOrAdd(Index index, JsonValue? value)
		{
			// validate the value
			value ??= JsonNull.Missing;

			var selfJson = this.RealizeArrayIfRequired();

			var prevJson = selfJson.GetValueOrDefault(index);
			if (!prevJson.IsNullOrMissing())
			{
				if (prevJson.StrictEquals(value))
				{ // idempotent
					return true;
				}
			}

			JsonValue? patch;
			switch (prevJson, value)
			{
				case (JsonObject before, JsonObject after):
				{ // Object patch
					patch = this.Context != null ? before.ComputePatch(after) : null;
					break;
				}
				case (JsonArray before, JsonArray after):
				{ // Array patch
					patch = this.Context != null ? before.ComputePatch(after) : null;
					break;
				}
				default:
				{ // overwrite
					patch = null;
					break;
				}
			}

			var offset = index.GetOffset(selfJson.Count);
			JsonArray newJson;
			if (selfJson.IsReadOnly)
			{ // first mutation, replace the array with a mutable version
				newJson = JsonArray.Copy(selfJson, deep: false, readOnly: false);
			}
			else
			{ // additional mutation, we can modify the array in-place
				newJson = selfJson;
			}
			selfJson[offset] = value;

			// notify the parent chain if we changed the current json instance
			if (!ReferenceEquals(this.Json, newJson))
			{
				this.Json = newJson;
				this.NotifyParent(this);
			}

			if (this.Context != null)
			{
				if (patch == null)
				{
					this.Context.RecordUpdate(this, offset, value);
				}
				else
				{
					this.Context.RecordPatch(this, offset, patch);
				}
			}

			return true;
		}

		public void Set(int index, JsonValue? value) => SetOrAdd(index, value);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void Set(int index, JsonNull? value) => Set(index, (JsonValue?) value);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void Set(int index, JsonBoolean? value) => Set(index, (JsonValue?) value);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void Set(int index, JsonNumber? value) => Set(index, (JsonValue?) value);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void Set(int index, JsonString? value) => Set(index, (JsonValue?) value);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void Set(int index, JsonDateTime? value) => Set(index, (JsonValue?) value);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void Set(int index, JsonObject? value) => Set(index, (JsonValue?) value);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void Set(int index, JsonArray? value) => Set(index, (JsonValue?) value);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void Set(int index, MutableJsonValue? value) => Set(index, value?.Json);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void Set<TValue>(int index, TValue? value) => Set(index, Convert<TValue>(value)); //TODO: pass the parent settings?

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void Set<TValue>(int index, TValue? value, IJsonPacker<TValue> converter) => Set(index, Convert<TValue>(value, converter)); //TODO: pass the parent settings?

		public void Set(Index index, JsonValue? value) => SetOrAdd(index, value);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void Set(Index index, JsonNull? value) => Set(index, (JsonValue?) value);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void Set(Index index, JsonBoolean? value) => Set(index, (JsonValue?) value);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void Set(Index index, JsonNumber? value) => Set(index, (JsonValue?) value);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void Set(Index index, JsonString? value) => Set(index, (JsonValue?) value);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void Set(Index index, JsonDateTime? value) => Set(index, (JsonValue?) value);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void Set(Index index, JsonObject? value) => Set(index, (JsonValue?) value);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void Set(Index index, JsonArray? value) => Set(index, (JsonValue?) value);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void Set(Index index, MutableJsonValue? value) => Set(index, value?.Json);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void Set<TValue>(Index index, TValue? value) => Set(index, Convert<TValue>(value)); //TODO: pass the parent settings?

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void Set<TValue>(Index index, TValue? value, IJsonPacker<TValue> converter) => Set(index, Convert<TValue>(value, converter)); //TODO: pass the parent settings?

		#endregion

		#region Add(...)

		/// <summary>Adds a new value to the parent object or array</summary>
		/// <param name="value">Value of the new field or item</param>
		/// <remarks>
		/// <para>If the current element is null or missing, it will automatically be promoted to an array with <paramref name="value"/> as its single element.</para>
		/// <para>If the current element is a field of an object, then it will be created in the parent object, unless it already has a non-null value, in which case an exception will be thrown.</para>
		/// <para>If the current element is an index in an array, then it will be filled with <paramref name="value"/>, unless it previously had a non-null value, in which case an exception will be thrown.</para>
		/// </remarks>
		/// <exception cref="ArgumentException">If the current element already exists.</exception>
		public void Add(JsonValue? value) => InsertOrAdd(^0, value);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void Add(JsonNull? value) => Add((JsonValue?) value);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void Add(JsonBoolean? value) => Add((JsonValue?) value);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void Add(JsonNumber? value) => Add((JsonValue?) value);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void Add(JsonString? value) => Add((JsonValue?) value);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void Add(JsonDateTime? value) => Add((JsonValue?) value);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void Add(JsonObject? value) => Add((JsonValue?) value);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void Add(JsonArray? value) => Add((JsonValue?) value);

		/// <summary>Adds a new field to the object</summary>
		/// <param name="value">Value of the field</param>
		/// <remarks>If the current value is null or missing, it will automatically be promoted to an empty object.</remarks>
		/// <exception cref="NotSupportedException">If the current value is not an Object.</exception>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void Add(MutableJsonValue? value) => Add(value?.Json);

		/// <summary>Adds a new field to the object</summary>
		/// <param name="value">Value of the field</param>
		/// <remarks>If the current value is null or missing, it will automatically be promoted to an empty object.</remarks>
		/// <exception cref="NotSupportedException">If the current value is not an Object.</exception>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void Add<TValue>(TValue value) => Add(Convert<TValue>(value));

		/// <summary>Adds a new field to the object</summary>
		/// <param name="name">Name of the field</param>
		/// <param name="value">Value of the field</param>
		/// <remarks>If the current value is null or missing, it will automatically be promoted to an empty object.</remarks>
		/// <exception cref="NotSupportedException">If the current value is not an Object.</exception>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void Add(string name, JsonValue value) => Get(name).Add(value);

		/// <summary>Adds a new field to the object</summary>
		/// <param name="path">Name of the field</param>
		/// <param name="value">Value of the field</param>
		/// <remarks>If the current value is null or missing, it will automatically be promoted to an empty object.</remarks>
		/// <exception cref="NotSupportedException">If the current value is not an Object.</exception>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void Add(JsonPath path, JsonValue value) => Get(path).Add(value);

		/// <summary>Adds a new field to the object</summary>
		/// <param name="name">Name of the field</param>
		/// <param name="value">Value of the field</param>
		/// <remarks>If the current value is null or missing, it will automatically be promoted to an empty object.</remarks>
		/// <exception cref="NotSupportedException">If the current value is not an Object.</exception>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void Add(string name, MutableJsonValue? value) => Get(name).Add(value?.Json);

		#endregion

		#region TryAdd...

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool TryAdd(string name, JsonValue? value)
		{
			if (ContainsKey(name))
			{
				return false;
			}
			InsertOrUpdate(name.AsMemory(), value);
			return true;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool TryAdd(string name, MutableJsonValue? value)
		{
			if (ContainsKey(name))
			{
				return false;
			}
			InsertOrUpdate(name.AsMemory(), value?.Json);
			return true;
		}

		public bool TryAdd<TValue>(string name, TValue? value)
		{
			// check before packing
			if (ContainsKey(name))
			{
				return false;
			}

			InsertOrUpdate(name.AsMemory(), Convert<TValue>(value));
			return true;
		}

		public bool TryAdd<TValue>(string name, TValue? value, IJsonPacker<TValue> converter)
		{
			// check before packing
			if (ContainsKey(name))
			{
				return false;
			}

			InsertOrUpdate(name.AsMemory(), Convert<TValue>(value, converter));
			return true;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool TryAdd(ReadOnlyMemory<char> name, JsonValue? value)
		{
			if (ContainsKey(name))
			{
				return false;
			}
			InsertOrUpdate(name, value);
			return true;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool TryAdd(ReadOnlyMemory<char> name, MutableJsonValue? value)
		{
			if (ContainsKey(name))
			{
				return false;
			}
			InsertOrUpdate(name, value?.Json);
			return true;
		}

		public bool TryAdd<TValue>(ReadOnlyMemory<char> name, TValue? value)
		{
			// check before packing
			if (ContainsKey(name))
			{
				return false;
			}

			InsertOrUpdate(name, Convert<TValue>(value));
			return true;
		}

		public bool TryAdd<TValue>(ReadOnlyMemory<char> name, TValue? value, IJsonPacker<TValue> converter)
		{
			// check before packing
			if (ContainsKey(name))
			{
				return false;
			}

			InsertOrUpdate(name, Convert<TValue>(value, converter));
			return true;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool TryAdd(JsonPath path, JsonValue? value) => Get(path).InsertOrUpdate(value, InsertionBehavior.None);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool TryAdd(JsonPath path, MutableJsonValue? value) => Get(path).InsertOrUpdate(value?.Json, InsertionBehavior.None);

		public bool TryAdd<TValue>(JsonPath path, TValue? value)
		{
			var child = Get(path);
			// check before packing
			if (child.Exists())
			{
				return false;
			}

			child.Set(value);
			return true;
		}

		public bool TryAdd<TValue>(JsonPath path, TValue? value, IJsonPacker<TValue> converter)
		{
			var child = Get(path);
			// check before packing
			if (child.Exists())
			{
				return false;
			}

			child.Set(value, converter);
			return true;
		}

		#endregion

		#region Insert...

		internal bool InsertOrAdd(Index index, JsonValue? value)
		{
			// validate the value
			value ??= JsonNull.Missing;

			var selfJson = this.RealizeArrayIfRequired();

			var offset = index.GetOffset(selfJson.Count);
			JsonArray newJson;
			if (selfJson.IsReadOnly)
			{ // first mutation, replace the array with a mutable version
				newJson = JsonArray.Copy(selfJson, deep: false, readOnly: false);
			}
			else
			{ // additional mutation, we can modify the array in-place
				newJson = selfJson;
			}

			newJson.Insert(offset, value);

			// notify the parent chain if we changed the current json instance
			if (!ReferenceEquals(this.Json, newJson))
			{
				this.Json = newJson;
				this.NotifyParent(this);
			}

			this.Context?.RecordAdd(this, offset, value);
			return true;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void Insert(int index, JsonValue value) => InsertOrAdd(index, value);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void Insert(int index, JsonNull? value) => InsertOrAdd(index, value);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void Insert(int index, JsonBoolean? value) => InsertOrAdd(index, value);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void Insert(int index, JsonNumber? value) => InsertOrAdd(index, value);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void Insert(int index, JsonString? value) => InsertOrAdd(index, value);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void Insert(int index, JsonDateTime? value) => InsertOrAdd(index, value);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void Insert(int index, JsonObject? value) => InsertOrAdd(index, value);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void Insert(int index, JsonArray? value) => InsertOrAdd(index, value);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void Insert(int index, MutableJsonValue? value) => InsertOrAdd(index, value?.Json);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void Insert<TValue>(int index, TValue value) => InsertOrAdd(index, Convert<TValue>(value)); //TODO: pass the parent settings?

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void Insert<TValue>(int index, TValue value, IJsonPacker<TValue> converter) => InsertOrAdd(index, Convert<TValue>(value, converter)); //TODO: pass the parent settings?

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void Insert(Index index, JsonValue value) => InsertOrAdd(index, value);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void Insert(Index index, JsonNull? value) => InsertOrAdd(index, value);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void Insert(Index index, JsonBoolean? value) => InsertOrAdd(index, value);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void Insert(Index index, JsonNumber? value) => InsertOrAdd(index, value);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void Insert(Index index, JsonString? value) => InsertOrAdd(index, value);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void Insert(Index index, JsonDateTime? value) => InsertOrAdd(index, value);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void Insert(Index index, JsonObject? value) => InsertOrAdd(index, value);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void Insert(Index index, JsonArray? value) => InsertOrAdd(index, value);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void Insert(Index index, MutableJsonValue? value) => InsertOrAdd(index, value?.Json);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void Insert<TValue>(Index index, TValue value) => InsertOrAdd(index, Convert<TValue>(value)); //TODO: pass the parent settings?

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void Insert<TValue>(Index index, TValue value, IJsonPacker<TValue> converter) => InsertOrAdd(index, Convert<TValue>(value, converter)); //TODO: pass the parent settings?

		#endregion

		#region Remove...

		/// <summary>Removes this node from its parent</summary>
		public bool Remove()
		{
			// simulate the value to be Missing
			this.Json = JsonNull.Missing;
			if (this.Segment.TryGetName(out var name))
			{
				return this.Parent?.Remove(name) ?? false;
			}
			if (this.Segment.TryGetIndex(out var index))
			{
				return this.Parent?.RemoveAt(index) ?? false;
			}

			throw new NotSupportedException();
		}

		/// <summary>Removes a field from this node</summary>
		public bool Remove(string name)
		{
			if (this.Json is not JsonObject prevJson) throw new NotSupportedException();
			if (!prevJson.ContainsKey(name))
			{ // not found
				return false;
			}

			var newJson = prevJson.CopyAndRemove(name);
			this.Json = newJson;
			this.NotifyParent(this);
			this.Context?.RecordDelete(this, name.AsMemory());
			return true;
		}

		/// <summary>Removes a field from this node</summary>
		public bool Remove(ReadOnlyMemory<char> name)
		{
			if (this.Json is not JsonObject prevJson) throw new NotSupportedException();
			if (!prevJson.ContainsKey(name))
			{ // not found
				return false;
			}

			var newJson = prevJson.CopyAndRemove(name);
			this.Json = newJson;
			this.NotifyParent(this);
			this.Context?.RecordDelete(this, name);
			return true;
		}

		/// <summary>Removes a field from this node</summary>
		public bool Remove(string name, [MaybeNullWhen(false)] out JsonValue value)
		{
			if (this.Json is not JsonObject prevJson) throw new NotSupportedException();
			if (!prevJson.TryGetValue(name, out value))
			{ // not found
				return false;
			}

			var newJson = prevJson.CopyAndRemove(name);
			this.Json = newJson;
			this.NotifyParent(this);
			this.Context?.RecordDelete(this, name.AsMemory());
			return true;
		}

		/// <summary>Removes a field from this node</summary>
		public bool Remove(ReadOnlyMemory<char> name, [MaybeNullWhen(false)] out JsonValue value)
		{
			if (this.Json is not JsonObject prevJson) throw new NotSupportedException();
			if (!prevJson.TryGetValue(name, out value))
			{ // not found
				return false;
			}

			var newJson = prevJson.CopyAndRemove(name);
			this.Json = newJson;
			this.NotifyParent(this);
			this.Context?.RecordDelete(this, name);
			return true;
		}

		/// <summary>Removes the element at the specified index from this node</summary>
		public bool RemoveAt(int index)
		{
			if (this.Json is not JsonArray prevJson) throw new NotSupportedException();
			if ((uint) index >= prevJson.Count)
			{ // not found
				return false;
			}

			var newJson = prevJson.CopyAndRemove(index);
			this.Json = newJson;
			this.NotifyParent(this);
			this.Context?.RecordDelete(this, index);
			return true;
		}

		/// <summary>Removes the element at the specified index from this node</summary>
		public bool RemoveAt(int index, [MaybeNullWhen(false)] out JsonValue value)
		{
			if (this.Json is not JsonArray prevJson) throw new NotSupportedException();
			if ((uint) index >= prevJson.Count)
			{ // not found
				value = null;
				return false;
			}

			value = prevJson[index];
			var newJson = prevJson.CopyAndRemove(index);
			this.Json = newJson;
			this.NotifyParent(this);
			this.Context?.RecordDelete(this, index);
			return true;
		}

		/// <summary>Removes the element at the specified index from this node</summary>
		public bool RemoveAt(Index index)
		{
			if (this.Json is not JsonArray prevJson) throw new NotSupportedException();
			var offset = index.GetOffset(prevJson.Count);
			if ((uint) offset >= prevJson.Count)
			{ // not found
				return false;
			}

			var newJson = prevJson.CopyAndRemove(offset);
			this.Json = newJson;
			this.NotifyParent(this);
			this.Context?.RecordDelete(this, offset);
			return true;
		}

		/// <summary>Removes the element at the specified index from this node</summary>
		public bool RemoveAt(Index index, [MaybeNullWhen(false)] out JsonValue value)
		{
			if (this.Json is not JsonArray prevJson) throw new NotSupportedException();
			var offset = index.GetOffset(prevJson.Count);
			if ((uint) offset >= prevJson.Count)
			{ // not found
				value = null;
				return false;
			}

			value = prevJson[offset];
			var newJson = prevJson.CopyAndRemove(offset);
			this.Json = newJson;
			this.NotifyParent(this);
			this.Context?.RecordDelete(this, offset);
			return true;
		}

		/// <summary>Removes the specified child from this node</summary>
		public bool Remove(JsonPathSegment segment)
			=> segment.TryGetName(out var name) ? Remove(name)
			 : segment.TryGetIndex(out var index) ? RemoveAt(index)
			 : Remove();

		/// <summary>Removes the child at the specified path</summary>
		public bool Remove(JsonPath path)
		{
			var current = this;
			foreach (var token in path.Tokenize())
			{
				if (!current.Exists()) return false;
				if (token.Last)
				{
					return current.Remove(token.Segment);
				}
				current = current.Get(token.Segment);
			}
			return false;
		}

		#endregion

		#region Increment...

		private static void Increment(MutableJsonValue value)
		{
			switch (value.Json)
			{
				case JsonNumber num:
				{
					value.Set((JsonValue) (num + 1));
					break;
				}
				case JsonNull:
				{
					value.Set((JsonValue) JsonNumber.One);
					break;
				}
				default:
				{
					throw new InvalidOperationException($"Cannot increment '{value.GetPath()}' because it is a {value.Json.Type}");
				}
			}
		}

		public void Increment() => Increment(this);

		public MutableJsonValue Increment(string name)
		{
			var prev = Get(name);
			Increment(prev);
			return prev;
		}

		public MutableJsonValue Increment(int index)
		{
			var prev = Get(index);
			Increment(prev);
			return prev;
		}

		public MutableJsonValue Increment(Index index)
		{
			var prev = Get(index);
			Increment(prev);
			return prev;
		}

		#endregion

		#region Exchange...

		[RequiresUnreferencedCode(AotMessages.TypeMightBeRemoved)]
		public bool CompareExchange<TValue>(TValue? value, TValue? comparand)
		{
			if (!this.Json.ValueEquals(comparand))
			{
				return false;
			}
			Set(Convert<TValue>(value));
			return true;
		}

		public JsonValue Exchange(JsonValue? value)
		{
			var prev = this.Json;
			Set(value);
			return prev;
		}

		public TValue? Exchange<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TValue>(TValue? value)
		{
			var prev = this.As<TValue>();
			Set(Convert<TValue>(value));
			return prev;
		}

		public JsonValue Exchange(string name, JsonValue? value)
		{
			var prev = this.Json[name];
			Set(name, value);
			return prev;
		}

		public JsonValue Exchange(int index, JsonValue? value)
		{
			var prev = this.Json[index];
			Set(index, value);
			return prev;
		}

		public JsonValue Exchange(Index index, JsonValue? value)
		{
			var prev = this.Json[index];
			Set(index, value);
			return prev;
		}

		public TValue? Exchange<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TValue>(string name, TValue? value)
		{
			var prev = Get<TValue?>(name, default);
			Set(name, value);
			return prev;
		}

		public TValue? Exchange<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TValue>(int index, TValue? value)
		{
			var prev = Get<TValue?>(index, default);
			Set(index, value);
			return prev;
		}

		public TValue? Exchange<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TValue>(Index index, TValue? value)
		{
			var prev = Get<TValue?>(index, default);
			Set(index, value);
			return prev;
		}

		#endregion

		#region Clear...

		public void Clear()
		{
			JsonValue newJson;
			switch (this.Json)
			{
				case JsonObject obj:
				{
					if (obj.Count == 0) return; // already empty!
					newJson = JsonObject.ReadOnly.Create(obj.Comparer);
					break;
				}
				case JsonArray arr:
				{
					if (arr.Count == 0) return; // already empty!
					newJson = JsonArray.ReadOnly.Empty;
					break;
				}
				default:
				{
					throw new NotSupportedException();
				}
			}
			this.Json = newJson;
			this.NotifyParent(this);
			this.Context?.RecordClear(this);
		}

		#endregion

		#endregion

		#region IEnumerable<...>

		/// <inheritdoc />
		System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();

		/// <inheritdoc />
		public IEnumerator<MutableJsonValue> GetEnumerator()
		{
			// we cannot know how the result will be used, so mark this a full "Value" read.

			if (this.Json is not JsonArray array)
			{
				if (this.Json is JsonNull)
				{
					yield break;
				}

				throw new InvalidOperationException("Cannot iterate a non-array");
			}

			// but we don't know how the items will be consumed
			for(int i = 0; i < array.Count; i++)
			{
				yield return new(this.Context, this, new(i), array[i]);
			}
		}

		#endregion

		#region JSON Serialization...

		void IJsonSerializable.JsonSerialize(CrystalJsonWriter writer) => this.Json.JsonSerialize(writer);

		JsonValue IJsonPackable.JsonPack(CrystalJsonSettings settings, ICrystalJsonTypeResolver resolver) => this.Json;

		#endregion

	}

}
